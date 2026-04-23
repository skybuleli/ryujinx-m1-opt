# Phase 1 Research: 内存止血 (Stop the Bleeding)

**Research Date:** 2026-04-24
**Project:** SwitchPro (Ryujinx fork for macOS Apple Silicon M1 8GB)
**Phase Goal:** 建立全面的内存监控体系，消除热点路径的堆分配，确保基础内存占用达标（空闲 < 2GB，游戏运行 30 分钟 < 4.5GB，Swap = 0）

---

## 1. Technical Approaches for Memory Monitoring on macOS

### 1.1 macOS Native APIs for RSS / Swap / Process Memory

The codebase already has **established macOS system interop patterns** in `src/Ryujinx/Utilities/SystemInfo/MacOSSystemInfo.cs` using `libSystem.dylib` P/Invoke via `[LibraryImport]`. We can and should extend this pattern for process-level memory monitoring.

#### Key APIs

| Metric | macOS API | .NET Interop Approach |
|--------|-----------|----------------------|
| **RSS (Resident Set Size)** | `task_info(mach_task_self(), TASK_BASIC_INFO, ...)` | `[LibraryImport("libSystem.dylib")]` with `task_basic_info` struct |
| **Virtual Size** | Same `task_info` call | `virtual_size` field in `task_basic_info` |
| **Swap Used** | `host_statistics64(HOST_VM_INFO64, ...)` | Already used in `MacOSSystemInfo.cs`; `Swapins`/`Swapouts`/`CompressorPageCount` |
| **GC Heap Size** | `GC.GetGCMemoryInfo()` | Pure managed API — `GCMemoryInfo.HeapSizeBytes` |
| **GC Total Allocated** | `GC.GetTotalMemory(false)` | Pure managed API |
| **Unmanaged/Native Memory** | `GC.GetGCMemoryInfo().TotalCommittedBytes - GC.GetTotalMemory(false)` | Derived metric |
| **Memory Pressure** | `host_statistics64` — `VMStatistics64` | `FreeCount + InactiveCount` vs total |

#### Critical macOS Structs

```csharp
// For RSS — task_basic_info (from mach/task_info.h)
[StructLayout(LayoutKind.Sequential)]
struct TaskBasicInfo
{
    public int SuspendCount;
    public uint VirtualSize;
    public uint ResidentSize;      // RSS in bytes
    public uint ResidentSizeMax;
    public uint UserTime;
    public uint SystemTime;
    public int Policy;
}

// For Swap — VMStatistics64 (ALREADY EXISTS in MacOSSystemInfo.cs)
// Swapins, Swapouts, CompressorPageCount are the key fields
```

#### Implementation Notes
- The existing `MacOSSystemInfo` uses `host_statistics64` for **system-wide** available memory. Phase 1 needs **process-specific** RSS via `task_info`.
- `task_info` flavor `TASK_BASIC_INFO` (value 5) returns `task_basic_info` struct.
- On Apple Silicon, "Swap" on macOS is actually **memory compression** + swap to SSD. The `CompressorPageCount` in `VMStatistics64` is the most relevant metric for "swap pressure".
- Sampling frequency: 1Hz (once per second) as decided in `01-CONTEXT.md` (D-07).

### 1.2 Unified Memory on Apple Silicon

M1 uses **unified memory architecture** — CPU and GPU share the same physical RAM. This means:
- GPU texture allocations *do* count toward the 8GB physical limit.
- The `resident_size` from `task_info` includes both CPU and GPU allocations made by the process.
- **Memoryless render targets** (Phase 2) will reduce this, but Phase 1 must measure the baseline.

### 1.3 Cross-Platform Abstraction Strategy

Even though SwitchPro is macOS-only, we should design `IMemoryTracker` with a platform abstraction layer:
- `IMemoryInfoProvider` interface
- `MacOSMemoryInfoProvider : IMemoryInfoProvider` — uses `task_info` + `host_statistics64`
- Future: could add Linux/Windows providers if needed (not required for Phase 1)

---

## 2. Zero-Allocation Optimization Patterns in C#/.NET

### 2.1 Pattern Inventory Used in Codebase

The codebase already demonstrates **excellent zero-allocation patterns** in the optimized paths:

#### Pattern A: `MemoryOwner<T>.Rent()` + `stackalloc` for Temp Buffers
**Location:** `src/Ryujinx.Graphics.Texture/BCnDecoder.cs` (lines 35-37)
```csharp
MemoryOwner<byte> output = MemoryOwner<byte>.Rent(size);  // Pooled array
Span<byte> tile = stackalloc byte[BlockWidth * BlockHeight * 4];  // Stack buffer
```
This is the **canonical pattern** for this codebase. `MemoryOwner<T>` is a custom `IMemoryOwner<T>` backed by an internal array pool (see `src/Ryujinx.Common/Memory/MemoryOwner.cs`).

#### Pattern B: `Span<T>` Slicing Instead of Allocation
**Location:** Throughout BCnDecoder.cs
```csharp
data = data[8..];  // Slice — zero allocation
```

#### Pattern C: `MemoryMarshal.Cast<TFrom, TTo>` for Type Punning
**Location:** BCnDecoder.cs, ETC2Decoder.cs
```csharp
Span<uint> tileAsUint = MemoryMarshal.Cast<byte, uint>(tile);
Span<ulong> data64 = MemoryMarshal.Cast<byte, ulong>(data);
```

#### Pattern D: `ArrayPool<T>.Shared` (Implicit via MemoryOwner)
The custom `MemoryOwner<T>` is preferred over `ArrayPool<T>.Shared` in this codebase because:
- It embeds length information.
- It has a custom pooling strategy with `SkipCount` eviction (lines 21-132 of MemoryOwner.cs).
- It returns `Memory<T>` compatible with async APIs.

### 2.2 High-Impact Optimization Targets

#### Target 1: `AstcDecoder.TryDecodeToRgba8()` — Line 232
```csharp
// BEFORE (allocates on heap):
byte[] output = new byte[QueryDecompressedSize(width, height, depth, levels, layers)];

// AFTER (use MemoryOwner):
MemoryOwner<byte> output = MemoryOwner<byte>.Rent(QueryDecompressedSize(...));
// Then change constructor to accept Memory<byte> or IMemoryOwner<byte>
```
**Impact:** HIGH. ASTC decoding is called for every ASTC texture not handled by passthrough. On BotW, this is frequent.

#### Target 2: `LayoutConverter.cs` — Lines 392 and 554
```csharp
// Line 392:
output = new byte[sizeInfo.TotalSize];

// Line 554:
output = new byte[h * stride];
```
These are `byte[]` allocations inside layout conversion. The method signatures already accept `Span<byte> output`, but callers may pass `Span<byte>.Empty`, triggering the `new byte[]` fallback.

**Strategy:** Audit all callers. If the caller doesn't provide a buffer, have the **caller** rent via `MemoryOwner<byte>.Rent()` and pass the span. This inverts allocation responsibility.

#### Target 3: `BC67Utils.cs` — Static LUT Initialization
```csharp
// Lines 16-22:
_quantizationLut = new byte[5][];
_quantizationLutNoPBit = new byte[5][];
for (...)
{
    byte[] lut = new byte[512];
    byte[] lutNoPBit = new byte[256];
```
These are **one-time static initializations**, not per-frame allocations. Low priority for Phase 1.

#### Target 4: `BC67Tables.cs` — Jagged Array Static Init
```csharp
public static readonly byte[][][] FixUpIndices = new byte[3][][]
{
    new byte[64][], ...
```
Also static one-time init. Not a runtime hotspot.

### 2.3 Patterns to Apply

| Pattern | When to Use | Example in Codebase |
|---------|-------------|---------------------|
| `stackalloc` | Small temp buffers (< 1KB), synchronous method | `BCnDecoder` tile buffers (64-256 bytes) |
| `MemoryOwner<T>.Rent()` | Large output buffers, must outlive method | All decoder `output` buffers |
| `Span<T>.Slice()` | Sub-range operations without copying | `data = data[8..]` in BCnDecoder |
| `MemoryMarshal.Cast<,>()` | Reinterpreting bytes as structs/vectors | Tile decoding SIMD paths |
| `ArrayPool<T>.Shared.Rent()` | If `MemoryOwner` unavailable | Use `MemoryOwner` instead (consistent) |

---

## 3. RecyclableMemoryStream Integration Strategies

### 3.1 Current State

`Microsoft.IO.RecyclableMemoryStream` is **already integrated** (version 3.0.1 in `Directory.Packages.props`).

**Existing wrapper:** `src/Ryujinx.Common/Memory/MemoryStreamManager.cs`
- Provides `MemoryStreamManager.Shared.GetStream()` returning `RecyclableMemoryStream`.
- Already used in ARMeilleure PTC (`src/ARMeilleure/Translation/PTC/Ptc.cs`) and code generators.

### 3.2 Remaining `MemoryStream` Allocations

Grep found only **3** remaining `new MemoryStream()` calls in `src/`:

1. `src/Ryujinx/UI/ViewModels/SettingsViewModel.cs:454` — `new MemoryStream(gameIconData)`
   - **UI path**, infrequent. Low priority.
2. `src/Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs:422` — `new MemoryStream(temp)`
   - HLE service path. Medium priority.
3. `src/Ryujinx.HLE/FileSystem/VirtualFileSystem.cs:288` — `new MemoryStream(ticketData)`
   - File system path. Medium priority.

### 3.3 Integration Strategy

For each remaining `new MemoryStream()`:
```csharp
// BEFORE:
using var ms = new MemoryStream(buffer);

// AFTER:
using var ms = MemoryStreamManager.Shared.GetStream(buffer);
```

**Caution:** The existing `MemoryStreamManager.Shared.GetStream(byte[] buffer)` copies data. If the buffer is large and the stream is short-lived, this is still better than heap allocation of the stream's internal buffer. For zero-copy scenarios where the buffer lifetime matches the stream, we may need a different approach (but likely not needed in Phase 1).

---

## 4. BenchmarkDotNet Best Practices for Texture Decoder Benchmarking

### 4.1 Existing Benchmark Infrastructure

**Project:** `tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj`
- Target framework: `net10.0`
- BenchmarkDotNet version: `0.15.8`
- Existing benchmarks: `BCnDecoderBenchmarks.cs`

### 4.2 Current Benchmark Analysis

The existing `BCnDecoderBenchmarks` is **well-structured but incomplete**:
- Only covers `DecodeBC1` and `DecodeBC3`.
- Uses `[MemoryDiagnoser]` — correct for measuring allocations.
- Uses `Random(42)` seeded data — reproducible.
- Missing: BC2, BC4, BC5, BC6, BC7, ASTC, ETC2.

### 4.3 Benchmark Design Best Practices

#### A. Data Generation Strategy
Per `01-CONTEXT.md` (D-14, D-15):
- **Primary:** Synthesize random data with fixed seed for CI reproducibility.
- **Secondary:** Extract real texture blocks from owned games for manual validation.

```csharp
[GlobalSetup]
public void Setup()
{
    var random = new Random(42);
    _data = new byte[DataSize];
    random.NextBytes(_data);
}
```

#### B. Measurement Dimensions
Per `01-CONTEXT.md` (D-16):
- **Execution Time** (ns/op) — default from BenchmarkDotNet
- **Memory Allocations** (B/op) — via `[MemoryDiagnoser]`
- **Throughput** (MB/s) — custom `[Benchmark]` that reports `OperationsPerSecond * DataSize`

```csharp
[Benchmark]
[BenchmarkCategory("Throughput")]
public void DecodeBC3_Throughput()
{
    using var result = BCnDecoder.DecodeBC3(_data, _width, _height, 1, 1, 1);
}
```

#### C. Configuration for Apple Silicon
```csharp
public class AppleSiliconConfig : ManualConfig
{
    public AppleSiliconConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core100)
            .WithPlatform(Platform.Arm64)
            .WithJit(Jit.RyuJit));
    }
}
```

#### D. Baseline Comparison
Use `[Benchmark(Baseline = true)]` on the unoptimized version to get relative improvement percentages.

### 4.4 Benchmarks to Add for Phase 1

| Decoder | Formats | Priority |
|---------|---------|----------|
| `BCnDecoder` | BC1, BC2, BC3, BC4, BC5, BC6, BC7 | High (exists partially) |
| `AstcDecoder` | 4x4, 8x8 block sizes | High (target of optimization) |
| `ETC2Decoder` | RGB, PTA, RGBA | Medium (already uses MemoryOwner) |
| `MemoryManager` | Allocate/Commit/Decommit | Medium (per D-12) |

---

## 5. Known Pitfalls and Landmines in Ryujinx Codebase

### 5.1 Memory Ownership Traps

**Landmine:** `AstcDecoder` has **two overloads** of `TryDecodeToRgba8`:
1. `TryDecodeToRgba8(..., out Span<byte> decoded)` — allocates `new byte[]` (line 232) ❌
2. `TryDecodeToRgba8(..., Memory<byte> outputBuffer)` — uses external buffer ✅
3. `TryDecodeToRgba8P(..., out MemoryOwner<byte> decoded)` — uses `MemoryOwner` ✅

**The caller in `Texture.cs` (line 799) uses `TryDecodeToRgba8P` with `out MemoryOwner<byte>`** — this path is already optimized! But we must verify no other callers use the `out Span<byte>` overload.

### 5.2 `MemoryOwner<T>` Disposal Chain

In `Texture.cs` (lines 797-824):
```csharp
using (result)
{
    if (!AstcDecoder.TryDecodeToRgba8P(..., out MemoryOwner<byte> decoded))
    {
        // ...
    }
    if (GraphicsConfig.EnableTextureRecompression)
    {
        using (decoded)
        {
            return BCnEncoder.EncodeBC7(...);
        }
    }
    return decoded;
}
```
**Pitfall:** The `result` disposal happens before `decoded` is returned. If `decoded` references `result`'s memory (it shouldn't in current code, but verify), this is a use-after-free. The current implementation copies, so it's safe.

### 5.3 macOS `mmap` and Memory Accounting

`MemoryManagementUnix.cs` uses `mmap(MAP_ANONYMOUS)` for allocations. On macOS:
- `mmap` with `PROT_NONE` (reserve) does **not** count toward RSS.
- `mprotect` to `PROT_READ | PROT_WRITE` (commit) **does** count toward RSS.
- The `_allocations` ConcurrentDictionary tracks sizes but is not used for active monitoring.

### 5.4 `stackalloc` in Async Context

`stackalloc` is safe in the texture decoders because they are **synchronous, CPU-bound** methods. Never use `stackalloc` in async methods.

### 5.5 RecyclableMemoryStreamManager Default Configuration

The current `MemoryStreamManager` uses default `RecyclableMemoryStreamManager` settings:
```csharp
private static readonly RecyclableMemoryStreamManager _shared = new();
```
Default block size is 128KB, max pool size is unlimited. For an emulator, we may want to tune:
- `MaximumFreeLargePoolBytes` to prevent unbounded growth.
- `BlockSize` if most streams are smaller/larger.

### 5.6 Tiered PGO is Enabled

`src/Ryujinx/Ryujinx.csproj` has `<TieredPGO>true</TieredPGO>`. This is good for steady-state performance but can cause:
- Higher initial memory usage during JIT warmup.
- Non-deterministic benchmark results unless warmed up properly.

**Mitigation:** Use `[IterationSetup]` or `[WarmupCount]` in benchmarks.

---

## 6. Existing Patterns to Reuse

### 6.1 Memory Pooling

| Component | Location | Reuse Strategy |
|-----------|----------|----------------|
| `MemoryOwner<T>` | `src/Ryujinx.Common/Memory/MemoryOwner.cs` | **Direct reuse** — already used by all optimized decoders |
| `MemoryStreamManager` | `src/Ryujinx.Common/Memory/MemoryStreamManager.cs` | **Extend to remaining `MemoryStream` sites** |
| `RecyclableMemoryStream` | `Directory.Packages.props` | Already a dependency — just use more |

### 6.2 macOS Interop

| Component | Location | Reuse Strategy |
|-----------|----------|----------------|
| `MacOSSystemInfo` | `src/Ryujinx/Utilities/SystemInfo/MacOSSystemInfo.cs` | **Extend** — add `task_info` for process RSS; reuse `host_statistics64` for swap |
| `LibraryImport` pattern | Same file | Use `[LibraryImport("libSystem.dylib")]` for all new macOS APIs |

### 6.3 Logging

| Component | Location | Reuse Strategy |
|-----------|----------|----------------|
| `Logger` | `src/Ryujinx.Common/Logging/Logger.cs` | Reuse for memory tracker diagnostic logs |
| `ILogTarget` | Same namespace | Could create `CsvLogTarget` for structured memory logs |

### 6.4 Benchmarking

| Component | Location | Reuse Strategy |
|-----------|----------|----------------|
| `BCnDecoderBenchmarks` | `tests/Ryujinx.Benchmarks/` | Extend with ASTC, ETC2, and additional BCn formats |
| Benchmark project | `tests/Ryujinx.Benchmarks.csproj` | Add project references for `Ryujinx.Memory` if benchmarking memory ops |

---

## 7. Specific File Locations and Code Patterns to Target

### 7.1 Priority 1: Direct Heap Allocations (Must Fix)

| File | Line | Pattern | Fix Strategy |
|------|------|---------|--------------|
| `Astc/AstcDecoder.cs` | 232 | `byte[] output = new byte[...]` | Change to `MemoryOwner<byte>.Rent()`; update caller or provide new overload |
| `LayoutConverter.cs` | 392 | `output = new byte[sizeInfo.TotalSize]` | Push allocation to caller; rent `MemoryOwner` |
| `LayoutConverter.cs` | 554 | `output = new byte[h * stride]` | Same as above |

### 7.2 Priority 2: MemoryStream Replacements

| File | Line | Pattern | Fix Strategy |
|------|------|---------|--------------|
| `Ryujinx/UI/ViewModels/SettingsViewModel.cs` | 454 | `new MemoryStream(gameIconData)` | `MemoryStreamManager.Shared.GetStream(gameIconData)` |
| `Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs` | 422 | `new MemoryStream(temp)` | Same |
| `Ryujinx.HLE/FileSystem/VirtualFileSystem.cs` | 288 | `new MemoryStream(ticketData)` | Same |

### 7.3 Priority 3: Memory Monitoring Insertion Points

| File | Purpose | Insertion Point |
|------|---------|-----------------|
| `Ryujinx.Memory/MemoryBlock.cs` | Core memory allocation | Hook into `Commit()` and `Decommit()` to signal budget manager |
| `Ryujinx.Memory/MemoryManagementUnix.cs` | Platform allocator | Track `mmap`/`mprotect` size changes (supplement to `task_info`) |
| `Ryujinx.Graphics.Gpu/Image/Texture.cs` | Texture creation | Signal memory budget manager on large texture upload |

### 7.4 Priority 4: Benchmark Additions

| File | Addition |
|------|----------|
| `tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` | Add BC2, BC4, BC5, BC6, BC7 benchmarks |
| `tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` | **NEW** — ASTC 4x4, 8x8 with random block data |
| `tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` | **NEW** — ETC2 RGB, PTA, RGBA |
| `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` | **NEW** — MemoryBlock Commit/Decommit/Read/Write |

---

## 8. Validation Architecture

### 8.1 Deliverable-to-Validation Matrix

| Deliverable | Requirement | Validation Method | Pass Criteria |
|-------------|-------------|-------------------|---------------|
| **Memory Monitor API** | MEM-01 | Unit test + manual run on macOS | Returns RSS within ±10MB of Activity Monitor |
| **CSV Log Output** | MEM-01, QA-01 | Inspect log file after 5-min run | CSV has columns: Timestamp, RSS, GCHeap, Unmanaged, Swap, PressureLevel |
| **Idle Memory** | MEM-02 | Launch emulator, idle 5 min, read RSS | < 2GB |
| **BotW 30-min Memory** | MEM-02 | Run BotW for 30 min, read peak RSS | < 4.5GB |
| **Swap = 0** | MEM-03 | Monitor `CompressorPageCount` + swapins | Zero or near-zero during gameplay |
| **ASTC Zero Alloc** | CPU-03 | BenchmarkDotNet `MemoryDiagnoser` | 0 B/op allocation in `AstcDecoder` hot path |
| **BCn Baseline** | QA-01 | BenchmarkDotNet before/after | Benchmarks run successfully; baseline data recorded |
| **RecyclableMemoryStream** | CPU-03 | Code review + static analysis | No `new MemoryStream()` in `src/` except UI (3 known, all replaced) |

### 8.2 Testing Strategy

#### Unit Tests
- `MacOSMemoryInfoProviderTests` — mock `task_info` return values, assert correct parsing.
- `MemoryBudgetManagerTests` — simulate threshold crossings, verify event firing.

#### Integration Tests
- Run headless (`Ryujinx.Headless.SDL2`) for 5 minutes with a small homebrew ROM.
- Parse CSV output, assert monotonic RSS tracking (shouldn't decrease unexpectedly).

#### Manual Validation
- Side-by-side with macOS Activity Monitor during BotW gameplay.
- Instruments "Game Performance" template for deeper analysis.

### 8.3 Benchmark Regression Gates

```
Run benchmarks on baseline branch → record results
Run benchmarks on feature branch → compare
If any decoder shows > 10% slowdown OR allocation increase > 0 B/op → BLOCK merge
```

### 8.4 Memory Budget Manager Behavior

Per `01-CONTEXT.md` deferred decisions, the budget manager should implement:

| Threshold | Behavior |
|-----------|----------|
| > 3.5 GB (Soft Limit, 78% of 4.5GB) | Log warning, trigger `GC.Collect(2, GCCollectionMode.Optimized, blocking: false)` |
| > 4.0 GB (Hard Limit, 89% of 4.5GB) | Aggressive cache eviction (texture cache, shader cache), log critical |
| > 4.5 GB (OOM Protection) | Log OOM, attempt emergency flush, notify user |

---

## Appendix A: Quick Reference — macOS Memory API Code Snippets

### A.1 Process RSS via task_info
```csharp
[LibraryImport("libSystem.dylib", SetLastError = true)]
private static partial int task_info(uint targetTask, int flavor, nint taskInfo, ref int taskInfoCount);

const int TASK_BASIC_INFO = 5;

var info = new TaskBasicInfo();
int count = Marshal.SizeOf<TaskBasicInfo>() / sizeof(int);
int result = task_info(mach_task_self(), TASK_BASIC_INFO, (nint)(&info), ref count);
// info.ResidentSize is RSS in bytes
```

### A.2 GC Heap Snapshot
```csharp
long gcHeap = GC.GetTotalMemory(false);
var gcInfo = GC.GetGCMemoryInfo();
long committed = gcInfo.TotalCommittedBytes;
```

### A.3 Swap / Compression via host_statistics64
```csharp
// See existing MacOSSystemInfo.cs — extend to expose:
uint compressorPages = stats.CompressorPageCount;
ulong swapins = stats.Swapins;
ulong swapouts = stats.Swapouts;
```

---

## Appendix B: Decision Checklist for Planning

- [ ] Confirm `AstcDecoder.TryDecodeToRgba8(out Span<byte>)` has **no callers** in `src/` — if it does, migrate them to `MemoryOwner` overload.
- [ ] Decide `IMemoryTracker` event model — `EventHandler<MemoryPressureEventArgs>` or `IObservable<MemorySnapshot>`?
- [ ] Decide CSV log rotation — daily files? Size-based (max 100MB)?
- [ ] Decide memory budget manager integration point — constructor injection or static `MemoryBudget.Instance`?
- [ ] Confirm BenchmarkDotNet project can reference `Ryujinx.Memory` for memory op benchmarks.
- [ ] Verify `task_info` P/Invoke works on both macOS x64 and arm64 (it does — `uint` task port is portable).

---

## RESEARCH COMPLETE

### Key Findings Summary

1. **macOS Memory APIs are well-understood and partially implemented.** The codebase already uses `host_statistics64` for system memory. We need to add `task_info(TASK_BASIC_INFO)` for process RSS. Both use the same `libSystem.dylib` `LibraryImport` pattern already established.

2. **Zero-allocation patterns are already mature in the codebase.** `MemoryOwner<T>.Rent()` + `stackalloc` is the canonical pattern used by `BCnDecoder` and `ETC2Decoder`. The **primary Phase 1 target** is `AstcDecoder.TryDecodeToRgba8()` line 232, which still allocates `new byte[]`.

3. **RecyclableMemoryStream is already integrated.** Only 3 remaining `new MemoryStream()` calls exist outside ARMeilleure, all easily replaceable with `MemoryStreamManager.Shared.GetStream()`.

4. **BenchmarkDotNet infrastructure exists but is thin.** Only `BCnDecoderBenchmarks` exists. We must add ASTC, ETC2, and Memory benchmarks. The project is already on .NET 10 with BenchmarkDotNet 0.15.8.

5. **The biggest landmine is memory ownership chains.** `Texture.cs` carefully manages `using` blocks around `MemoryOwner<byte>`. Any changes to decoder signatures must preserve this disposal chain to avoid leaks or use-after-free.

6. **Apple Silicon unified memory means GPU allocations count toward our 4.5GB limit.** The memory monitor must track total process RSS (which includes GPU-mapped memory on macOS), not just GC heap.

7. **Validation is straightforward.** Activity Monitor provides ground truth for RSS. BenchmarkDotNet `[MemoryDiagnoser]` provides ground truth for allocations. Headless runs with CSV logging provide integration validation.
