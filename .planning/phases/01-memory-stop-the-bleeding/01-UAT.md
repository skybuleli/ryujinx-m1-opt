---
status: testing
phase: 01-memory-stop-the-bleeding
source:
  - 01-SUMMARY.md
  - 01-SUMMARY-02-zero-allocation.md
  - 01-SUMMARY-03-memory-budget.md
  - 01-SUMMARY-04-benchmark-suite.md
started: 2026-04-24
updated: 2026-04-24
---

## Current Test
<!-- OVERWRITE each test - shows where we are -->

number: 1
name: macOS Memory Provider Returns Valid RSS
expected: |
  MacOSMemoryInfoProvider.GetSnapshot() returns a MemorySnapshot with
  non-zero RSS value that is within ~10% of Activity Monitor's value.
  The struct should have ResidentSize > 0 and PressureLevel = Normal
  under normal conditions.
awaiting: user response

## Tests

### 1. macOS Memory Provider Returns Valid RSS
expected: |
  MacOSMemoryInfoProvider.GetSnapshot() returns a MemorySnapshot with
  non-zero RSS value that is within ~10% of Activity Monitor's value.
  The struct should have ResidentSize > 0 and PressureLevel = Normal
  under normal conditions.
result: pending

### 2. Memory Budget Manager Samples and Logs CSV
expected: |
  MemoryBudgetManager starts on Program.cs startup, samples memory every
  1 second, and writes to memory_log.csv with columns:
  Timestamp,RssBytes,GcHeapBytes,UnmanagedBytes,SwapBytes,PressureLevel.
  CSV file is created in the Ryujinx data directory.
result: pending

### 3. PressureChanged Event Fires on Threshold Cross
expected: |
  When memory usage crosses 3.5GB (Warning), 4.0GB (Critical), or 4.5GB (OOM)
  thresholds, the PressureChanged event fires with correct old/new levels.
  Unit tests verify this behavior with simulated snapshots.
result: pending

### 4. AstcDecoder Has Zero Heap Allocation Path
expected: |
  grep -r "new byte\[" src/Ryujinx.Graphics.Texture/Astc/ returns no matches
  in the decode hot path. The deleted TryDecodeToRgba8(out Span<byte>) overload
  no longer exists. Only MemoryOwner<byte>.Rent() is used for output buffers.
result: pending

### 5. LayoutConverter Throws on Empty Span
expected: |
  Calling LayoutConverter methods with an empty Span<byte> output throws
  ArgumentException instead of silently allocating a new byte[].
  Callers must pre-allocate buffers via MemoryOwner<byte>.Rent().
result: pending

### 6. No new MemoryStream() Calls Remain in src/
expected: |
  grep -r "new MemoryStream(" src/ --include="*.cs" returns zero matches
  (excluding comments). All MemoryStream creation uses
  MemoryStreamManager.Shared.GetStream().
result: pending

### 7. Tiered Thresholds Evaluate Correctly
expected: |
  MemoryBudgetManager uses thresholds: Soft=3.5GB (GC trigger),
  Hard=4.0GB (cache eviction), OOM=4.5GB (emergency flush).
  Each threshold triggers the correct action when crossed upward.
result: pending

### 8. Cache Eviction Clear() Methods Exist
expected: |
  TextureCache, ShaderCache, AutoDeleteCache, and underlying hash tables
  all have Clear() methods that dispose entries and reset state while
  keeping the cache reusable (not disposed).
result: pending

### 9. Native Memory Tracking via MemoryBlock Events
expected: |
  MemoryBlock.Commit and MemoryBlock.Decommit raise static events that
  MemoryBudgetManager subscribes to, tracking native/unmanaged memory
  without circular dependencies between Ryujinx.Common and Ryujinx.Memory.
result: pending

### 10. Swap = 0 Monitoring Alerts on Swap Usage
expected: |
  SwapPressureDetected event fires when macOS reports swap/compressed
  memory usage > 0. This runs independently of RSS thresholds.
result: pending

### 11. BenchmarkDotNet Benchmarks Compile
expected: |
  cd tests/Ryujinx.Benchmarks && dotnet build compiles successfully
  with 0 errors. All benchmark classes use [MemoryDiagnoser] and
  [Config(typeof(AppleSiliconConfig))].
result: pending

### 12. All Decoder Formats Have Benchmarks
expected: |
  Benchmarks exist for: BC1-BC7, ASTC 4x4/8x8, ETC2 RGB/PTA/RGBA.
  Each benchmark method calls the corresponding Decode* method with
  seeded Random(42) test data in [GlobalSetup].
result: pending

### 13. AppleSiliconConfig Configures ARM64+RyuJIT
expected: |
  AppleSiliconConfig sets Platform=ARM64, Runtime=.NET 10.0,
  Jit=RyuJIT. Benchmark validation run executes on Apple Silicon
  and produces results in BenchmarkDotNet.Artifacts/.
result: pending

## Summary

total: 13
passed: 0
issues: 0
pending: 13
skipped: 0

## Gaps

[none yet]
