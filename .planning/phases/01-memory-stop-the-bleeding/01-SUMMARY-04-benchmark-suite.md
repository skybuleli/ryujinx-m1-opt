---
phase: 01-memory-stop-the-bleeding
plan: 04
subsystem: testing
tags: [benchmarkdotnet, benchmarking, textures, memory, arm64, ryujit]

requires:
  - phase: 01-memory-stop-the-bleeding
    provides: BenchmarkDotNet project infrastructure (existing BCnDecoderBenchmarks, BDN 0.15.8)

provides:
  - BCnDecoderBenchmarks covering BC1-BC7 with MemoryDiagnoser
  - AstcDecoderBenchmarks for 4x4 and 8x8 block sizes
  - ETC2DecoderBenchmarks for RGB, PTA, RGBA
  - MemoryBlockBenchmarks for Commit/Decommit/Read/Write
  - AppleSiliconConfig with ARM64 + RyuJIT + .NET 10.0 runtime
  - Program.cs updated to BenchmarkSwitcher for CLI filter support

affects:
  - 01-memory-stop-the-bleeding (Phase 1 validation)
  - Future optimization plans (baseline data for regression detection)

tech-stack:
  added: []
  patterns:
    - "[Config(typeof(AppleSiliconConfig))] applied to all benchmark classes"
    - "BenchmarkSwitcher.FromAssembly for command-line driven benchmark execution"
    - "[MemoryDiagnoser] + [GlobalSetup] with Random(42) seeded data for reproducibility"

key-files:
  created:
    - tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs
    - tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs
    - tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs
    - tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs
  modified:
    - tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs (added BC2, BC4, BC5, BC6, BC7)
    - tests/Ryujinx.Benchmarks/Program.cs (switched to BenchmarkSwitcher)
    - tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj (added Ryujinx.Memory reference)

key-decisions:
  - "Used CoreRuntime.Core10_0 instead of CoreRuntime.Core100 because BenchmarkDotNet 0.15.8 uses Core10_0 naming"
  - "Removed explicit JsonExporter and MemoryDiagnoser from ManualConfig because benchmark classes already carry [MemoryDiagnoser] and BDN 0.15.8 API differs from plan assumptions"
  - "Updated Program.cs to BenchmarkSwitcher to enable -f filter arguments for validation runs"

patterns-established:
  - "All decoder benchmarks use [Config(typeof(AppleSiliconConfig))] [MemoryDiagnoser] and Random(42) seeded data"
  - "MemoryBlock benchmarks use [GlobalCleanup] to dispose mmap-backed resources"

requirements-completed:
  - QA-01

duration: 30min
completed: 2026-04-24
---

# Phase 1 Plan 04: BenchmarkDotNet Suite Summary

**BenchmarkDotNet micro-benchmark suite covering BC1-BC7, ASTC 4x4/8x8, ETC2 RGB/PTA/RGBA decoders and MemoryBlock operations with Apple Silicon ARM64+RyuJIT runtime configuration**

## Performance

- **Duration:** 30 min
- **Started:** 2026-04-24T02:50:00Z
- **Completed:** 2026-04-24T03:20:00Z
- **Tasks:** 5
- **Files modified:** 7

## Accomplishments
- Extended BCnDecoderBenchmarks to cover all BC1-BC7 formats
- Created AstcDecoderBenchmarks for 4x4 and 8x8 ASTC block sizes with TryDecodeToRgba8P
- Created ETC2DecoderBenchmarks for RGB, PTA, and RGBA formats
- Created MemoryBlockBenchmarks for Commit/Decommit, Write, and Read operations
- Added AppleSiliconConfig configuring ARM64 + RyuJIT + .NET 10.0 runtime
- Updated Program.cs to BenchmarkSwitcher enabling command-line benchmark filtering
- Validation run executed successfully: AstcDecoderBenchmarks produced results in BenchmarkDotNet.Artifacts/

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend BCnDecoderBenchmarks** - `9b050d5` (feat)
2. **Task 2: Create AstcDecoderBenchmarks** - `74cb94b` (feat)
3. **Task 3: Create ETC2DecoderBenchmarks** - `0fe3d70` (feat)
4. **Task 4: Create MemoryBlockBenchmarks** - `93d9256` (feat)
5. **Task 5: AppleSiliconConfig + validation** - `0e8f7cd` (feat)

**Plan metadata:** `TBD` (docs: complete plan)

## Files Created/Modified
- `tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` - Added BC2, BC4, BC5, BC6, BC7 benchmarks
- `tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` - New ASTC 4x4 and 8x8 decoder benchmarks
- `tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` - New ETC2 RGB/PTA/RGBA decoder benchmarks
- `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` - New MemoryBlock Commit/Decommit/Read/Write benchmarks
- `tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs` - Apple Silicon runtime configuration (ARM64, RyuJIT, .NET 10)
- `tests/Ryujinx.Benchmarks/Program.cs` - Switched from hardcoded BitUtilsBenchmarks to BenchmarkSwitcher
- `tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` - Added Ryujinx.Memory project reference

## Decisions Made
- `CoreRuntime.Core10_0` is the correct BenchmarkDotNet 0.15.8 constant for .NET 10, not `CoreRuntime.Core100`
- Removed explicit `JsonExporter` and `MemoryDiagnoser` from `ManualConfig` because BDN 0.15.8 APIs differed from plan assumptions; `[MemoryDiagnoser]` on classes suffices and default exporters still produce CSV/HTML/MD
- Updated `Program.cs` to `BenchmarkSwitcher` so `-f` CLI filters work for validation runs

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] AppleSiliconConfig compilation failures due to BDN 0.15.8 API differences**
- **Found during:** Task 5 (AppleSiliconConfig creation)
- **Issue:** Plan specified `CoreRuntime.Core100` which does not exist in BDN 0.15.8; `JsonExporter` was not found in expected namespace; `MemoryDiagnoser()` constructor required `MemoryDiagnoserConfig`
- **Fix:** Changed to `CoreRuntime.Core10_0`, removed explicit `JsonExporter` and `MemoryDiagnoser` from config (classes already have `[MemoryDiagnoser]` attribute)
- **Files modified:** `tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs`
- **Verification:** `dotnet build` exits 0; validation run produces benchmark output
- **Committed in:** `0e8f7cd` (Task 5 commit)

**2. [Rule 2 - Missing Critical] MemoryBlock.Read API mismatch in benchmark**
- **Found during:** Task 4 (MemoryBlockBenchmarks creation)
- **Issue:** Plan's `Read4K` method called `_block.Read(0, _data4K.Length)` but `MemoryBlock.Read` accepts `Span<byte>` not `int`
- **Fix:** Added `_scratch4K` field initialized in `[GlobalSetup]` and used `_block.Read(0, _scratch4K)`
- **Files modified:** `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs`
- **Verification:** `dotnet build` exits 0
- **Committed in:** `93d9256` (Task 4 commit)

**3. [Rule 2 - Missing Critical] Missing Ryujinx.Memory project reference**
- **Found during:** Task 4 (MemoryBlockBenchmarks creation)
- **Issue:** Benchmark project did not reference `Ryujinx.Memory`, causing `MemoryBlock` type to be unresolved
- **Fix:** Added `<ProjectReference Include="..\..\src\Ryujinx.Memory\Ryujinx.Memory.csproj" />` to csproj
- **Files modified:** `tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj`
- **Verification:** `dotnet build` exits 0
- **Committed in:** `93d9256` (Task 4 commit)

**4. [Rule 2 - Missing Critical] Program.cs did not support CLI filter arguments**
- **Found during:** Task 5 (validation run)
- **Issue:** Existing `Program.cs` hardcoded `BenchmarkRunner.Run<BitUtilsBenchmarks>(config)` and ignored `args`, so `-f` filter did not work
- **Fix:** Replaced with `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)`
- **Files modified:** `tests/Ryujinx.Benchmarks/Program.cs`
- **Verification:** `dotnet run -- -f '*AstcDecoderBenchmarks*'` successfully runs filtered benchmarks
- **Committed in:** `0e8f7cd` (Task 5 commit)

---

**Total deviations:** 4 auto-fixed (4 missing critical / API mismatch)
**Impact on plan:** All auto-fixes were necessary for compilation and validation correctness. No scope creep.

## Issues Encountered
- NuGet package sources `git.ryujinx.app` are defunct (404), causing NU1900 warnings during build. This is a pre-existing issue noted in STATE.md and does not block compilation or execution.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Benchmark suite is complete and validated
- Baseline performance data can now be collected for all texture decoders and memory operations
- Ready for Plan 03 or subsequent optimization work with measurable regression gates

---
*Phase: 01-memory-stop-the-bleeding*
*Completed: 2026-04-24*
