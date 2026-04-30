---
phase: 1
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/Ryujinx/Utilities/SystemInfo/IMemoryInfoProvider.cs
  - src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs
  - src/Ryujinx.Common/Memory/MemorySnapshot.cs
  - src/Ryujinx.Common/Memory/MemoryPressureLevel.cs
  - src/Ryujinx.Common/Memory/MemoryPressureEventArgs.cs
  - src/Ryujinx.Common/Memory/IMemoryTracker.cs
  - src/Ryujinx.Common/Memory/CsvMemoryLogTarget.cs
  - src/Ryujinx.Common/Memory/MemoryBudgetManager.cs
  - src/Ryujinx/Program.cs
  - tests/Ryujinx.Tests/Memory/MacOSMemoryInfoProviderTests.cs
  - tests/Ryujinx.Tests/Memory/MemoryBudgetManagerTests.cs
autonomous: true
requirements:
  - MEM-01
---

# Plan 01: Memory Monitoring Infrastructure (MEM-01)

## Goal
Implement real-time memory monitoring with macOS-native RSS/Swap tracking, structured CSV logging, and an event-driven API for downstream consumers.

## Verification Criteria
1. `MemoryBudgetManager` samples RSS, GC Heap, Unmanaged, Swap at 1Hz and produces CSV logs with correct columns.
2. RSS values are within ±10MB of macOS Activity Monitor.
3. Unit tests verify `MacOSMemoryInfoProvider` returns non-zero RSS for the current process.
4. `IMemoryTracker.PressureChanged` fires when simulated thresholds are crossed.

## must_haves
- [ ] `IMemoryInfoProvider` abstraction exists with `GetSnapshot()` returning `MemorySnapshot`.
- [ ] `MacOSMemoryInfoProvider` uses `[LibraryImport("libSystem.dylib")]` + `task_info(TASK_BASIC_INFO)` for RSS, and `host_statistics64` for Swap/Compression.
- [ ] `MemoryBudgetManager` implements `IMemoryTracker`, samples on `System.Timers.Timer` at 1Hz, and writes CSV with columns: `Timestamp,RssBytes,GcHeapBytes,UnmanagedBytes,SwapBytes,PressureLevel`.
- [ ] `CsvMemoryLogTarget` implements `ILogTarget` and writes append-only CSV to `Ryujinx/memory_log.csv`.
- [ ] Unit tests exist and pass for provider parsing and budget manager threshold events.

---

## Tasks

### Task 1-01-01: Create `IMemoryInfoProvider` and `MacOSMemoryInfoProvider`

```xml
<task>
  <id>1-01-01</id>
  <description>Create platform memory info provider with macOS P/Invoke</description>
  <requirement>MEM-01</requirement>
  <read_first>
    - src/Ryujinx/Utilities/SystemInfo/MacOSSystemInfo.cs (existing LibraryImport pattern for host_statistics64)
    - src/Ryujinx/Utilities/SystemInfo/ISystemInfo.cs (existing interface pattern)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §1.1
  </read_first>
  <action>
    Create `src/Ryujinx/Utilities/SystemInfo/IMemoryInfoProvider.cs`:
    ```csharp
    public interface IMemoryInfoProvider
    {
        MemorySnapshot GetSnapshot();
    }
    ```

    Create `src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs`:
    - Use `[LibraryImport("libSystem.dylib", SetLastError = true)]` for `task_info`.
    - Define `const int TASK_BASIC_INFO = 5;`.
    - Define `[StructLayout(LayoutKind.Sequential)] struct TaskBasicInfo` with fields:
      `public int SuspendCount;`
      `public uint VirtualSize;`
      `public uint ResidentSize;`
      `public uint ResidentSizeMax;`
      `public uint UserTime;`
      `public uint SystemTime;`
      `public int Policy;`
    - `task_info` signature:
      `private static partial int task_info(uint targetTask, int flavor, nint taskInfo, ref int taskInfoCount);`
    - In `GetSnapshot()`, call `task_info(mach_task_self(), TASK_BASIC_INFO, (nint)(&info), ref count)` using `unsafe` block.
    - Read `info.ResidentSize` as RSS bytes.
    - Reuse existing `host_statistics64` pattern from `MacOSSystemInfo.cs` to read `VMStatistics64.CompressorPageCount` and compute Swap bytes as `CompressorPageCount * 16384L`.
    - Compute GC Heap via `GC.GetTotalMemory(false)`.
    - Compute Unmanaged via `GC.GetGCMemoryInfo().TotalCommittedBytes - GC.GetTotalMemory(false)`.
    - Return `new MemorySnapshot(DateTime.UtcNow, rss, gcHeap, unmanaged, swap, MemoryPressureLevel.Normal)`.
  </action>
  <acceptance_criteria>
    - `grep -r "interface IMemoryInfoProvider" src/Ryujinx/Utilities/SystemInfo/` returns 1 match.
    - `grep -r "class MacOSMemoryInfoProvider" src/Ryujinx/Utilities/SystemInfo/` returns 1 match.
    - `grep "TASK_BASIC_INFO" src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs` returns `const int TASK_BASIC_INFO = 5;`.
    - `grep "LibraryImport.*libSystem.dylib" src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs` returns at least 1 match.
    - `grep "task_info" src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs` returns at least 1 match.
    - `grep "GetSnapshot" src/Ryujinx/Utilities/SystemInfo/MacOSMemoryInfoProvider.cs` returns at least 1 match.
    - `dotnet build src/Ryujinx/Utilities/SystemInfo/` exits 0 (or broader build if no separate project).
  </acceptance_criteria>
</task>
```

### Task 1-01-02: Create `MemorySnapshot`, `MemoryPressureLevel`, `MemoryPressureEventArgs`

```xml
<task>
  <id>1-01-02</id>
  <description>Create shared memory data types</description>
  <requirement>MEM-01</requirement>
  <read_first>
    - src/Ryujinx.Common/Memory/MemoryOwner.cs (namespace convention)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §1.2
  </read_first>
  <action>
    Create `src/Ryujinx.Common/Memory/MemorySnapshot.cs` in namespace `Ryujinx.Common.Memory`:
    ```csharp
    public readonly record struct MemorySnapshot(
        DateTime Timestamp,
        long RssBytes,
        long GcHeapBytes,
        long UnmanagedBytes,
        long SwapBytes,
        MemoryPressureLevel PressureLevel);
    ```

    Create `src/Ryujinx.Common/Memory/MemoryPressureLevel.cs`:
    ```csharp
    public enum MemoryPressureLevel { Normal, Warning, Critical, Oom }
    ```

    Create `src/Ryujinx.Common/Memory/MemoryPressureEventArgs.cs`:
    ```csharp
    public class MemoryPressureEventArgs : EventArgs
    {
        public MemorySnapshot Snapshot { get; }
        public MemoryPressureLevel PreviousLevel { get; }
        public MemoryPressureEventArgs(MemorySnapshot snapshot, MemoryPressureLevel previousLevel)
        {
            Snapshot = snapshot;
            PreviousLevel = previousLevel;
        }
    }
    ```
  </action>
  <acceptance_criteria>
    - `grep "readonly record struct MemorySnapshot" src/Ryujinx.Common/Memory/MemorySnapshot.cs` returns 1 match.
    - `grep "enum MemoryPressureLevel" src/Ryujinx.Common/Memory/MemoryPressureLevel.cs` returns 1 match.
    - `grep "class MemoryPressureEventArgs" src/Ryujinx.Common/Memory/MemoryPressureEventArgs.cs` returns 1 match.
    - `grep "Normal, Warning, Critical, Oom" src/Ryujinx.Common/Memory/MemoryPressureLevel.cs` returns 1 match.
  </acceptance_criteria>
</task>
```

### Task 1-01-03: Create `IMemoryTracker` and `MemoryBudgetManager`

```xml
<task>
  <id>1-01-03</id>
  <description>Create memory tracker interface and budget manager with timer sampling</description>
  <requirement>MEM-01</requirement>
  <read_first>
    - src/Ryujinx.Common/Logging/Logger.cs (EventHandler/ILogTarget dispatch pattern)
    - src/Ryujinx.Common/Memory/MemoryOwner.cs (namespace)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §1.2
  </read_first>
  <action>
    Create `src/Ryujinx.Common/Memory/IMemoryTracker.cs`:
    ```csharp
    public interface IMemoryTracker
    {
        event EventHandler<MemoryPressureEventArgs> PressureChanged;
        MemorySnapshot LastSnapshot { get; }
    }
    ```

    Create `src/Ryujinx.Common/Memory/MemoryBudgetManager.cs`:
    - Implement `IMemoryTracker, IDisposable`.
    - Constructor: `public MemoryBudgetManager(IMemoryInfoProvider provider, TimeSpan? sampleInterval = null)`.
    - Default sample interval: `TimeSpan.FromSeconds(1)`.
    - Private fields:
      `private readonly IMemoryInfoProvider _provider;`
      `private readonly System.Timers.Timer _timer;`
      `private readonly object _lock = new();`
      `private MemoryPressureLevel _currentLevel = MemoryPressureLevel.Normal;`
    - `public MemorySnapshot LastSnapshot { get; private set; }`
    - `public event EventHandler<MemoryPressureEventArgs> PressureChanged;`
    - In timer callback:
      1. Call `_provider.GetSnapshot()`.
      2. Update `LastSnapshot`.
      3. Evaluate pressure level against thresholds:
         - `> 4_500_000_000L` → `Oom`
         - `> 4_000_000_000L` → `Critical`
         - `> 3_500_000_000L` → `Warning`
         - else → `Normal`
      4. If level changed from `_currentLevel`, fire `PressureChanged` event with old + new level.
      5. Update `_currentLevel`.
    - `Start()` and `Stop()` methods to control timer.
    - `Dispose()` stops and disposes timer.
  </action>
  <acceptance_criteria>
    - `grep "interface IMemoryTracker" src/Ryujinx.Common/Memory/IMemoryTracker.cs` returns 1 match.
    - `grep "class MemoryBudgetManager" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "PressureChanged" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 2 matches (declaration + invocation).
    - `grep "System.Timers.Timer" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "3_500_000_000L\|4_000_000_000L\|4_500_000_000L" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 1 match.
    - `grep "Start()" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 1 match.
    - `grep "Stop()" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 1 match.
    - `dotnet build src/Ryujinx.Common/Ryujinx.Common.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-01-04: Create `CsvMemoryLogTarget`

```xml
<task>
  <id>1-01-04</id>
  <description>Create CSV log target for structured memory logging</description>
  <requirement>MEM-01</requirement>
  <read_first>
    - src/Ryujinx.Common/Logging/ILogTarget.cs (interface shape)
    - src/Ryujinx.Common/Logging/Logger.cs (target registration)
    - .planning/phases/01-memory-stop-the-bleeding/01-CONTEXT.md §D-09
  </read_first>
  <action>
    Create `src/Ryujinx.Common/Memory/CsvMemoryLogTarget.cs`:
    - Implement `ILogTarget` (from `Ryujinx.Common.Logging`).
    - Constructor: `public CsvMemoryLogTarget(string logDirectory)`.
    - Log file path: `Path.Combine(logDirectory, "memory_log.csv")`.
    - On first write, emit header line exactly:
      `Timestamp,RssBytes,GcHeapBytes,UnmanagedBytes,SwapBytes,PressureLevel`
    - On each `Log(LogEventArgs eventArgs)`:
      - Parse message for MemorySnapshot JSON or accept a dedicated method `LogSnapshot(MemorySnapshot snapshot)`.
      - Write CSV line: `{snapshot.Timestamp:O},{snapshot.RssBytes},{snapshot.GcHeapBytes},{snapshot.UnmanagedBytes},{snapshot.SwapBytes},{snapshot.PressureLevel}`
    - Use `StreamWriter` with `AutoFlush = true` and append mode.
    - Implement `bool Enabled { get; set; } = true;`
    - Implement `void Dispose() => _writer?.Dispose();`
  </action>
  <acceptance_criteria>
    - `grep "class CsvMemoryLogTarget" src/Ryujinx.Common/Memory/CsvMemoryLogTarget.cs` returns 1 match.
    - `grep "Timestamp,RssBytes,GcHeapBytes,UnmanagedBytes,SwapBytes,PressureLevel" src/Ryujinx.Common/Memory/CsvMemoryLogTarget.cs` returns 1 match.
    - `grep "ILogTarget" src/Ryujinx.Common/Memory/CsvMemoryLogTarget.cs` returns 1 match.
    - File compiles: `dotnet build src/Ryujinx.Common/Ryujinx.Common.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-01-05: Wire up `MemoryBudgetManager` in emulator startup and add unit tests

```xml
<task>
  <id>1-01-05</id>
  <description>Integrate memory tracker into Program.cs and add unit tests</description>
  <requirement>MEM-01</requirement>
  <read_first>
    - src/Ryujinx/Program.cs (startup sequence)
    - src/Ryujinx.Common/Logging/Logger.cs (target registration API)
  </read_first>
  <action>
    Modify `src/Ryujinx/Program.cs`:
    - After logger initialization, add:
      ```csharp
      var memoryProvider = new MacOSMemoryInfoProvider();
      var memoryTracker = new MemoryBudgetManager(memoryProvider);
      var csvLogTarget = new CsvMemoryLogTarget(ProgramDir);
      Logger.AddTarget(csvLogTarget);
      memoryTracker.PressureChanged += (sender, e) =>
      {
          Logger.Info?.Print(LogClass.Emulation, $"Memory pressure changed: {e.PreviousLevel} -> {e.Snapshot.PressureLevel} (RSS: {e.Snapshot.RssBytes / 1024 / 1024} MB)");
      };
      memoryTracker.Start();
      ```
    - Ensure `memoryTracker.Dispose()` is called on graceful shutdown.

    Create `tests/Ryujinx.Tests/Memory/MacOSMemoryInfoProviderTests.cs`:
    - Test `GetSnapshot()` returns `RssBytes > 0`.
    - Test `Timestamp` is within 1 second of `DateTime.UtcNow`.

    Create `tests/Ryujinx.Tests/Memory/MemoryBudgetManagerTests.cs`:
    - Create a fake `IMemoryInfoProvider` that returns controlled snapshots.
    - Test that `PressureChanged` fires when crossing from `Normal` to `Warning` (RSS 3.6GB).
    - Test that `LastSnapshot` is updated after timer tick.
    - Test `Dispose()` stops timer and no further events fire.
  </action>
  <acceptance_criteria>
    - `grep "new MacOSMemoryInfoProvider" src/Ryujinx/Program.cs` returns 1 match.
    - `grep "new MemoryBudgetManager" src/Ryujinx/Program.cs` returns 1 match.
    - `grep "memoryTracker.Start" src/Ryujinx/Program.cs` returns 1 match.
    - `grep "memoryTracker.Dispose" src/Ryujinx/Program.cs` returns 1 match.
    - `dotnet test --filter 'MacOSMemoryInfoProviderTests'` exits 0.
    - `dotnet test --filter 'MemoryBudgetManagerTests'` exits 0.
  </acceptance_criteria>
</task>
```
