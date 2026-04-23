# Phase 1 Patterns: 内存止血 (Stop the Bleeding)

**Phase:** 01-memory-stop-the-bleeding
**Generated:** 2026-04-24
**Source:** 01-CONTEXT.md, 01-RESEARCH.md, live codebase analysis

---

## 1. Memory Monitoring

### 1.1 IMemoryInfoProvider / MacOSMemoryInfoProvider

**Role:** Platform abstraction for process-level memory metrics (RSS, Swap, GC Heap, Unmanaged).
**Data Flow:** Timer-driven sampling (1Hz) → struct snapshot → CSV log + event dispatch.
**Closest Existing Analog:** `src/Ryujinx/Utilities/SystemInfo/MacOSSystemInfo.cs`

**Concrete Code Excerpt (existing interop pattern):**
```csharp
// From MacOSSystemInfo.cs — already uses host_statistics64 for system memory
[LibraryImport("libSystem.dylib", SetLastError = true)]
private static partial int host_statistics64(
    uint hostPriv, int hostFlavor,
    ref VMStatistics64 hostInfo64Out, ref uint hostInfo64OutCnt);

[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct VMStatistics64
{
    public uint FreeCount;
    // ...
    public ulong Swapins;
    public ulong Swapouts;
    public uint CompressorPageCount;
}
```

**New pattern to add (process RSS via task_info):**
```csharp
// New file: src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs
[LibraryImport("libSystem.dylib", SetLastError = true)]
private static partial int task_info(
    uint targetTask, int flavor,
    nint taskInfo, ref int taskInfoCount);

const int TASK_BASIC_INFO = 5;

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
```

**Mapping:** Reuse the exact `LibraryImport("libSystem.dylib")` pattern from `MacOSSystemInfo`. The new provider is a **thin wrapper** around the same interop style, but returns a `MemorySnapshot` struct instead of populating system info properties.

**Disposal / Lifetime:** Stateless provider; no disposable resources. The `IMemoryTracker` that consumes it runs on a `System.Timers.Timer` (or `PeriodicTimer` if async) and should be `IDisposable` to stop sampling.

---

### 1.2 IMemoryTracker / MemoryBudgetManager

**Role:** Orchestrator — drives sampling, logs CSV, fires events on threshold crossing.
**Data Flow:** `IMemoryInfoProvider.GetSnapshot()` → threshold evaluation → `EventHandler<MemoryPressureEventArgs>` → optional `GC.Collect(2)` / cache eviction.
**Closest Existing Analog:** None directly, but the event-driven pattern mirrors `Logger` targets (`ILogTarget`).

**Concrete Code Excerpt (event model inspiration from Logger):**
```csharp
// From src/Ryujinx.Common/Logging/Logger.cs — multi-target dispatch pattern
public static void DebugPrint(LogClass logClass, string message)
{
    foreach (ILogTarget target in _logTargets)
    {
        target.Log(new LogEventArgs(logClass, LogLevel.Debug, message));
    }
}
```

**New pattern:**
```csharp
// New file: src/Ryujinx.Common/Memory/MemoryBudgetManager.cs
public record MemorySnapshot(
    DateTime Timestamp,
    long RssBytes,
    long GcHeapBytes,
    long UnmanagedBytes,
    long SwapBytes,
    MemoryPressureLevel PressureLevel);

public enum MemoryPressureLevel { Normal, Warning, Critical, Oom }

public interface IMemoryTracker
{
    event EventHandler<MemoryPressureEventArgs> PressureChanged;
    MemorySnapshot LastSnapshot { get; }
}

public class MemoryBudgetManager : IMemoryTracker, IDisposable
{
    // Thresholds per 01-RESEARCH.md §8.4
    private const long SoftLimitBytes = 3_500_000_000L;
    private const long HardLimitBytes = 4_000_000_000L;
    private const long OomLimitBytes  = 4_500_000_000L;
    // ...
}
```

**Mapping:** The `EventHandler<>` pattern is idiomatic in this codebase. Use `ILogTarget`-style multi-dispatch if we want multiple consumers (e.g., CSV logger + future UI overlay).

---

## 2. Zero-Allocation Optimization

### 2.1 AstcDecoder.TryDecodeToRgba8

**Role:** Texture decoder output buffer allocation.
**Data Flow:** Caller requests decode → decoder allocates output → returns `Span<byte>` or `MemoryOwner<byte>`.
**Closest Existing Analog:** `BCnDecoder.DecodeBC1` (lines 26-112) and `ETC2Decoder.DecodeRgb` (lines 53-113).

**Concrete Code Excerpt (canonical zero-allocation pattern):**
```csharp
// From BCnDecoder.cs — the target pattern for AstcDecoder
public static MemoryOwner<byte> DecodeBC1(ReadOnlySpan<byte> data, int width, int height, int depth, int levels, int layers)
{
    int size = 0;
    for (int l = 0; l < levels; l++)
    {
        size += Math.Max(1, width >> l) * Math.Max(1, height >> l) * Math.Max(1, depth >> l) * layers * 4;
    }

    MemoryOwner<byte> output = MemoryOwner<byte>.Rent(size);   // POOLED — zero heap alloc
    Span<byte> tile = stackalloc byte[BlockWidth * BlockHeight * 4]; // STACK — zero heap alloc
    // ... decode into output.Span ...
    return output;
}
```

**Current problematic code (AstcDecoder.cs line 232):**
```csharp
public static bool TryDecodeToRgba8(..., out Span<byte> decoded)
{
    byte[] output = new byte[QueryDecompressedSize(width, height, depth, levels, layers)]; // ❌ HEAP ALLOC
    // ...
    decoded = output;
    return decoder.Success;
}
```

**New pattern (match BCnDecoder / ETC2Decoder):**
```csharp
// Option A: Remove the out Span<byte> overload entirely (no callers found in src/)
// Option B: If retention needed, change signature to return MemoryOwner<byte>
public static MemoryOwner<byte> DecodeToRgba8(...)
{
    MemoryOwner<byte> output = MemoryOwner<byte>.Rent(QueryDecompressedSize(...)); // ✅ POOLED
    var decoder = new AstcDecoder(data, output.Memory, ...);
    // ...
    return output;
}
```

**Mapping:** `AstcDecoder` already has `TryDecodeToRgba8P(..., out MemoryOwner<byte> decoded)` (line 295) which **does** use `MemoryOwner<byte>.Rent()`. The goal is to eliminate the `out Span<byte>` overload that allocates `new byte[]`. Grep confirms **zero callers** of `TryDecodeToRgba8(out Span<byte>)` in `src/` — safe to remove or redirect.

**Disposal chain (critical — from Texture.cs lines 797-824):**
```csharp
using (result)
{
    if (!AstcDecoder.TryDecodeToRgba8P(..., out MemoryOwner<byte> decoded)) { ... }
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
**Rule:** Any change to decoder return types must preserve this `using(decoded)` chain. `MemoryOwner<byte>` is the correct type because it is `IDisposable` and returns the array to the pool.

---

### 2.2 LayoutConverter

**Role:** Convert between texture memory layouts (block-linear ↔ linear, strided).
**Data Flow:** Caller provides input buffer + dimensions → converter writes to pre-allocated or rented output.
**Closest Existing Analog:** `LayoutConverter.ConvertBlockLinearToLinear` (line 98) already uses `MemoryOwner<byte>.Rent()`.

**Concrete Code Excerpt (already correct pattern):**
```csharp
// From LayoutConverter.cs lines 98-253 — already zero-allocation
public static MemoryOwner<byte> ConvertBlockLinearToLinear(...)
{
    MemoryOwner<byte> outputOwner = MemoryOwner<byte>.Rent(outSize); // ✅ POOLED
    Span<byte> output = outputOwner.Span;
    // ... write directly into output ...
    return outputOwner;
}
```

**Current problematic code (lines 390-393 and 552-555):**
```csharp
// Line 390-393 — fallback when caller passes Span<byte>.Empty
if (output.Length == 0)
{
    output = new byte[sizeInfo.TotalSize]; // ❌ HEAP ALLOC
}

// Line 552-555 — same issue
if (output.Length == 0)
{
    output = new byte[h * stride]; // ❌ HEAP ALLOC
}
```

**New pattern:**
```csharp
// Strategy: invert allocation responsibility per 01-RESEARCH.md §2.2
// The public API already returns MemoryOwner<byte> in most overloads.
// For the void-return overloads that accept Span<byte>, do NOT fall back to new byte[].
// Instead, require the caller to rent:

public static ReadOnlySpan<byte> ConvertLinearToBlockLinear(
    Span<byte> output, ...)
{
    if (output.Length == 0)
    {
        throw new ArgumentException(
            "Output buffer must be pre-allocated. Use MemoryOwner<byte>.Rent(size) or stackalloc.");
    }
    // ...
}
```

**Mapping:** The codebase already has `MemoryOwner<byte>.Rent()` in the same file. The fix is **policy** (throw instead of allocate) rather than introducing a new pattern. Callers must be audited; there are only two fallback sites.

---

## 3. RecyclableMemoryStream Integration

### 3.1 MemoryStreamManager.Shared.GetStream

**Role:** Replace `new MemoryStream(buffer)` with pooled `RecyclableMemoryStream`.
**Data Flow:** Caller creates stream from byte array → reads/writes → disposes → buffer returned to pool.
**Closest Existing Analog:** `src/Ryujinx.Common/Memory/MemoryStreamManager.cs` (already wraps `RecyclableMemoryStreamManager`).

**Concrete Code Excerpt (existing wrapper):**
```csharp
// From MemoryStreamManager.cs
public static class Shared
{
    public static RecyclableMemoryStream GetStream(byte[] buffer)
        => GetStream(Guid.NewGuid(), null, buffer, 0, buffer.Length);

    public static RecyclableMemoryStream GetStream(ReadOnlySpan<byte> buffer)
        => GetStream(Guid.NewGuid(), null, buffer);
}
```

**Concrete Code Excerpt (existing usage in ARMeilleure PTC):**
```csharp
// From Ptc.cs lines 158-161
_infosStream = MemoryStreamManager.Shared.GetStream();
_relocsStream = MemoryStreamManager.Shared.GetStream();
_unwindInfosStream = MemoryStreamManager.Shared.GetStream();
```

**Remaining sites to fix (3 occurrences):**
```csharp
// BEFORE (SettingsViewModel.cs:454)
using var ms = new MemoryStream(gameIconData);

// AFTER
using var ms = MemoryStreamManager.Shared.GetStream(gameIconData);
```

**Mapping:** Direct substitution — identical API shape. The wrapper already handles `byte[]`, `ReadOnlySpan<byte>`, and empty streams.

---

## 4. BenchmarkDotNet Benchmarks

### 4.1 BCnDecoderBenchmarks (Existing Pattern)

**Role:** Measure decoder execution time and allocations.
**Data Flow:** `[GlobalSetup]` generates seeded random data → `[Benchmark]` calls decoder → BenchmarkDotNet reports ns/op + B/op.
**Closest Existing Analog:** `tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs`.

**Concrete Code Excerpt:**
```csharp
// From BCnDecoderBenchmarks.cs
[MemoryDiagnoser]
public class BCnDecoderBenchmarks
{
    private byte[] _data;
    private int _width = 1024;
    private int _height = 1024;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[_width * _height];
        new Random(42).NextBytes(_data);
    }

    [Benchmark]
    public void DecodeBC3()
    {
        using MemoryOwner<byte> result = BCnDecoder.DecodeBC3(_data, _width, _height, 1, 1, 1);
    }
}
```

**Mapping for new benchmarks:** Copy the class skeleton exactly. Change the decoder call and data size to match the format under test.

---

### 4.2 AstcDecoderBenchmarks (NEW)

**New file:** `tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs`

**Pattern:**
```csharp
[MemoryDiagnoser]
public class AstcDecoderBenchmarks
{
    private byte[] _data4x4;
    private byte[] _data8x8;

    [GlobalSetup]
    public void Setup()
    {
        // ASTC block is 16 bytes regardless of block size
        int blocks4x4 = (1024 / 4) * (1024 / 4);
        int blocks8x8 = (1024 / 8) * (1024 / 8);
        _data4x4 = new byte[blocks4x4 * 16];
        _data8x8 = new byte[blocks8x8 * 16];
        new Random(42).NextBytes(_data4x4);
        new Random(42).NextBytes(_data8x8);
    }

    [Benchmark]
    public void DecodeAstc4x4()
    {
        using MemoryOwner<byte> result = AstcDecoder.TryDecodeToRgba8P(
            _data4x4, 4, 4, 1024, 1024, 1, 1, 1, out MemoryOwner<byte> decoded)
            ? decoded
            : MemoryOwner<byte>.Rent(0);
    }

    [Benchmark]
    public void DecodeAstc8x8()
    {
        using MemoryOwner<byte> result = AstcDecoder.TryDecodeToRgba8P(
            _data8x8, 8, 8, 1024, 1024, 1, 1, 1, out MemoryOwner<byte> decoded)
            ? decoded
            : MemoryOwner<byte>.Rent(0);
    }
}
```

**Key design decisions:**
- Use `TryDecodeToRgba8P(..., out MemoryOwner<byte>)` because it is the optimized path.
- Dispose the result with `using` to avoid leaking pooled arrays across benchmark iterations.
- `[MemoryDiagnoser]` is **mandatory** — Phase 1 validation requires 0 B/op on the optimized path.

---

### 4.3 ETC2DecoderBenchmarks (NEW)

**New file:** `tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs`

**Pattern:** Same skeleton as `BCnDecoderBenchmarks`, calling `ETC2Decoder.DecodeRgb`, `DecodePta`, `DecodeRgba`.

---

### 4.4 MemoryBlockBenchmarks (NEW)

**New file:** `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs`

**Pattern:**
```csharp
[MemoryDiagnoser]
public class MemoryBlockBenchmarks
{
    private MemoryBlock _block;

    [GlobalSetup]
    public void Setup()
    {
        _block = new MemoryBlock(64 * 1024 * 1024, MemoryAllocationFlags.Reserve);
    }

    [GlobalCleanup]
    public void Cleanup() => _block.Dispose();

    [Benchmark]
    public void CommitDecommit4K()
    {
        _block.Commit(0, 4096);
        _block.Decommit(0, 4096);
    }

    [Benchmark]
    public void Write4K()
    {
        _block.Commit(0, 4096);
        _block.Write(0, _data4K);
    }
}
```

**Caution:** `MemoryBlock` uses `mmap`/`mprotect`. Benchmarks must run sequentially (not in parallel) to avoid address-space collisions.

---

## 5. File-to-Pattern Summary Matrix

| File | Role | Data Flow | Closest Analog | Pattern Action |
|------|------|-----------|----------------|----------------|
| `MacOSMemoryInfoProvider.cs` | Platform probe | P/Invoke → struct | `MacOSSystemInfo.cs` | Reuse `LibraryImport` + struct layout |
| `MemoryBudgetManager.cs` | Orchestrator | Timer → snapshot → event | `Logger` / `ILogTarget` | Event-driven multi-dispatch |
| `AstcDecoder.cs` (modify) | Decoder | Input → pooled output | `BCnDecoder.cs` | Replace `new byte[]` with `MemoryOwner<byte>.Rent()` |
| `LayoutConverter.cs` (modify) | Layout transform | Input → caller-provided output | Self (line 98) | Throw on empty output instead of `new byte[]` |
| `SettingsViewModel.cs` (modify) | UI helper | icon bytes → stream | `Ptc.cs` | `new MemoryStream` → `MemoryStreamManager.Shared.GetStream()` |
| `IStaticServiceForPsc.cs` (modify) | HLE service | temp bytes → stream | `Ptc.cs` | Same substitution |
| `VirtualFileSystem.cs` (modify) | FS helper | ticket bytes → stream | `Ptc.cs` | Same substitution |
| `AstcDecoderBenchmarks.cs` | Benchmark | Random blocks → decode | `BCnDecoderBenchmarks.cs` | Clone skeleton, change decoder call |
| `ETC2DecoderBenchmarks.cs` | Benchmark | Random blocks → decode | `BCnDecoderBenchmarks.cs` | Clone skeleton, change decoder call |
| `MemoryBlockBenchmarks.cs` | Benchmark | mmap ops | `BCnDecoderBenchmarks.cs` | Clone skeleton, wrap `MemoryBlock` lifecycle |

---

## 6. Critical Rules Derived from Live Code

1. **MemoryOwner Disposal Chain:** When a decoder returns `MemoryOwner<byte>`, the caller MUST dispose it (usually via `using`). Texture.cs already does this correctly.
2. **No `new byte[]` in Hot Paths:** The codebase convention is `MemoryOwner<byte>.Rent()` for outputs > 1KB and `stackalloc` for temps < 1KB.
3. **LibraryImport for macOS:** All new macOS interop MUST use `[LibraryImport("libSystem.dylib")]` (not `DllImport`). This is already the pattern in `MacOSSystemInfo.cs`.
4. **BenchmarkDotNet Version:** `0.15.8` (from `Directory.Packages.props`). Use `[MemoryDiagnoser]` on every decoder benchmark.
5. **RecyclableMemoryStream Wrapper:** Never instantiate `RecyclableMemoryStream` directly; always go through `MemoryStreamManager.Shared.GetStream()`.
