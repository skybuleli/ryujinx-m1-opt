---
phase: 1
plan: 03
type: execute
wave: 2
depends_on:
  - 01-PLAN-01-memory-monitoring.md
files_modified:
  - src/Ryujinx.Common/Memory/MemoryBudgetManager.cs
  - src/Ryujinx.Common/Memory/IMemoryPressureHandler.cs
  - src/Ryujinx.Memory/MemoryBlock.cs
  - src/Ryujinx.Graphics.Gpu/Image/TextureCache.cs
  - src/Ryujinx.Graphics.Gpu/Shader/ShaderCache.cs
  - src/Ryujinx/Utilities/Memory/DefaultMemoryPressureHandler.cs
  - src/Ryujinx/Program.cs
  - tests/Ryujinx.Tests/Memory/MemoryBudgetManagerThresholdTests.cs
autonomous: true
requirements:
  - MEM-02
  - MEM-03
---

# Plan 03: Memory Budget Manager (MEM-02, MEM-03)

## Goal
Implement active memory budget enforcement with tiered thresholds (soft/hard/OOM), triggering GC optimization, cache eviction, and user notification to keep idle RSS < 2GB and BotW 30-min RSS < 4.5GB with Swap = 0.

## Verification Criteria
1. Memory budget manager triggers `GC.Collect(2, Optimized)` when RSS crosses 3.5GB.
2. At 4.0GB, texture cache and shader cache are aggressively evicted.
3. At 4.5GB, an OOM warning is logged and emergency flush is attempted.
4. Unit tests simulate threshold crossings and verify correct handler invocations.

## must_haves
- [ ] `MemoryBudgetManager` evaluates thresholds on every sample tick and fires `PressureChanged` with correct `MemoryPressureLevel` transitions.
- [ ] Soft limit (> 3.5GB) invokes `GC.Collect(2, GCCollectionMode.Optimized, blocking: false)`.
- [ ] Hard limit (> 4.0GB) invokes `TextureCache.Clear()` and `ShaderCache.Clear()` via `IMemoryPressureHandler`.
- [ ] OOM limit (> 4.5GB) logs critical error and attempts emergency flush.
- [ ] `MemoryBlock.Commit()` and `Decommit()` signal the budget manager for native memory tracking.
- [ ] Unit tests verify all three thresholds trigger expected actions.

---

## Tasks

### Task 1-03-01: Add threshold evaluation and GC trigger to `MemoryBudgetManager`

```xml
<task>
  <id>1-03-01</id>
  <description>Implement tiered threshold logic with GC optimization trigger</description>
  <requirement>MEM-02</requirement>
  <read_first>
    - src/Ryujinx.Common/Memory/MemoryBudgetManager.cs (created in Plan 01)
    - src/Ryujinx.Common/Memory/MemorySnapshot.cs
    - src/Ryujinx.Common/Memory/MemoryPressureLevel.cs
    - .planning/phases/01-memory-stop-the-bleeding/01-RESEARCH.md §8.4
  </read_first>
  <action>
    Open `src/Ryujinx.Common/Memory/MemoryBudgetManager.cs`.

    Add threshold constants:
    ```csharp
    public const long SoftLimitBytes = 3_500_000_000L;
    public const long HardLimitBytes = 4_000_000_000L;
    public const long OomLimitBytes  = 4_500_000_000L;
    ```

    Modify the timer callback method (or create `EvaluatePressure(MemorySnapshot snapshot)`):
    ```csharp
    private MemoryPressureLevel EvaluatePressure(MemorySnapshot snapshot)
    {
        long rss = snapshot.RssBytes;
        if (rss > OomLimitBytes)  return MemoryPressureLevel.Oom;
        if (rss > HardLimitBytes) return MemoryPressureLevel.Critical;
        if (rss > SoftLimitBytes) return MemoryPressureLevel.Warning;
        return MemoryPressureLevel.Normal;
    }
    ```

    After detecting a level change, invoke actions:
    ```csharp
    private void OnPressureChanged(MemorySnapshot snapshot, MemoryPressureLevel previousLevel)
    {
        PressureChanged?.Invoke(this, new MemoryPressureEventArgs(snapshot, previousLevel));

        switch (snapshot.PressureLevel)
        {
            case MemoryPressureLevel.Warning:
                Logger.Warning?.Print(LogClass.Emulation, $"Memory soft limit exceeded: {snapshot.RssBytes / 1024 / 1024} MB");
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
                break;
            case MemoryPressureLevel.Critical:
                Logger.Error?.Print(LogClass.Emulation, $"Memory hard limit exceeded: {snapshot.RssBytes / 1024 / 1024} MB — evicting caches");
                _pressureHandler?.OnHardLimitExceeded();
                break;
            case MemoryPressureLevel.Oom:
                Logger.Error?.Print(LogClass.Emulation, $"CRITICAL: Memory OOM limit exceeded: {snapshot.RssBytes / 1024 / 1024} MB — emergency flush");
                _pressureHandler?.OnOomLimitExceeded();
                break;
        }
    }
    ```

    Add constructor overload accepting `IMemoryPressureHandler`:
    ```csharp
    public MemoryBudgetManager(IMemoryInfoProvider provider, IMemoryPressureHandler pressureHandler, TimeSpan? sampleInterval = null)
        : this(provider, sampleInterval)
    {
        _pressureHandler = pressureHandler;
    }
    ```

    Add private field: `private readonly IMemoryPressureHandler _pressureHandler;`
  </action>
  <acceptance_criteria>
    - `grep "SoftLimitBytes = 3_500_000_000L" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "HardLimitBytes = 4_000_000_000L" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "OomLimitBytes = 4_500_000_000L" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "GC.Collect(2, GCCollectionMode.Optimized" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "IMemoryPressureHandler" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 2 matches.
    - `dotnet build src/Ryujinx.Common/Ryujinx.Common.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-03-02: Create `IMemoryPressureHandler` and wire cache eviction

```xml
<task>
  <id>1-03-02</id>
  <description>Define pressure handler interface and implement cache eviction</description>
  <requirement>MEM-02</requirement>
  <read_first>
    - src/Ryujinx.Graphics.Gpu/Image/TextureCache.cs (existing Clear method or equivalent)
    - src/Ryujinx.Graphics.Gpu/Shader/ShaderCache.cs (existing Clear method or equivalent)
    - src/Ryujinx.Common/Memory/MemoryBudgetManager.cs (after Task 1-03-01 changes)
  </read_first>
  <action>
    Create `src/Ryujinx.Common/Memory/IMemoryPressureHandler.cs`:
    ```csharp
    public interface IMemoryPressureHandler
    {
        void OnHardLimitExceeded();
        void OnOomLimitExceeded();
    }
    ```

    Create `src/Ryujinx/Utilities/Memory/DefaultMemoryPressureHandler.cs`:
    ```csharp
    public class DefaultMemoryPressureHandler : IMemoryPressureHandler
    {
        private readonly GpuContext _gpuContext;
        public DefaultMemoryPressureHandler(GpuContext gpuContext)
        {
            _gpuContext = gpuContext;
        }
        public void OnHardLimitExceeded()
        {
            _gpuContext.Renderer.TextureCache?.Clear();
            _gpuContext.Renderer.ShaderCache?.Clear();
        }
        public void OnOomLimitExceeded()
        {
            OnHardLimitExceeded();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
        }
    }
    ```
    Adjust property names (`TextureCache`, `ShaderCache`) to match actual `GpuContext` / renderer API.
    If no `Clear()` method exists on cache classes, add a `Clear()` method that drops all cached entries and disposes their resources.
  </action>
  <acceptance_criteria>
    - `grep "interface IMemoryPressureHandler" src/Ryujinx.Common/Memory/IMemoryPressureHandler.cs` returns 1 match.
    - `grep "void OnHardLimitExceeded" src/Ryujinx.Common/Memory/IMemoryPressureHandler.cs` returns 1 match.
    - `grep "void OnOomLimitExceeded" src/Ryujinx.Common/Memory/IMemoryPressureHandler.cs` returns 1 match.
    - `grep "class DefaultMemoryPressureHandler" src/Ryujinx/Utilities/Memory/DefaultMemoryPressureHandler.cs` returns 1 match.
    - `dotnet build` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-03-03: Hook `MemoryBlock.Commit` / `Decommit` into budget manager

```xml
<task>
  <id>1-03-03</id>
  <description>Signal budget manager on native memory changes</description>
  <requirement>MEM-02</requirement>
  <read_first>
    - src/Ryujinx.Memory/MemoryBlock.cs (Commit/Decommit methods)
    - src/Ryujinx.Memory/MemoryManagementUnix.cs (mmap/mprotect paths)
    - src/Ryujinx.Common/Memory/IMemoryTracker.cs
  </read_first>
  <action>
    Open `src/Ryujinx.Memory/MemoryBlock.cs`.

    Add a static event to signal native memory changes:
    ```csharp
    public static event EventHandler<long> NativeMemoryCommitted;
    public static event EventHandler<long> NativeMemoryDecommitted;
    ```

    In `Commit(nint offset, nint size)`, after the commit succeeds, add:
    ```csharp
    NativeMemoryCommitted?.Invoke(null, size);
    ```

    In `Decommit(nint offset, nint size)`, after decommit succeeds, add:
    ```csharp
    NativeMemoryDecommitted?.Invoke(null, size);
    ```

    Verify `Ryujinx.Common.csproj` references `Ryujinx.Memory`:
    - Open `src/Ryujinx.Common/Ryujinx.Common.csproj`.
    - If `<ProjectReference Include="..\Ryujinx.Memory\Ryujinx.Memory.csproj" />` is missing, add it inside an `<ItemGroup>`.

    Update `MemoryBudgetManager` to subscribe to these events:
    ```csharp
    public void TrackNativeMemory()
    {
        MemoryBlock.NativeMemoryCommitted += (s, size) => _nativeCommittedBytes += size;
        MemoryBlock.NativeMemoryDecommitted += (s, size) => _nativeCommittedBytes -= size;
    }
    ```
    Add private field: `private long _nativeCommittedBytes;`
    Include `_nativeCommittedBytes` in `MemorySnapshot.UnmanagedBytes` calculation or use it as supplementary data in logs.
  </action>
  <acceptance_criteria>
    - `grep "NativeMemoryCommitted" src/Ryujinx.Memory/MemoryBlock.cs` returns at least 2 matches (declaration + invocation).
    - `grep "NativeMemoryDecommitted" src/Ryujinx.Memory/MemoryBlock.cs` returns at least 2 matches.
    - `grep "TrackNativeMemory" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns 1 match.
    - `grep "Ryujinx.Memory" src/Ryujinx.Common/Ryujinx.Common.csproj` returns at least 1 match.
    - `dotnet build src/Ryujinx.Memory/Ryujinx.Memory.csproj` exits 0.
    - `dotnet build src/Ryujinx.Common/Ryujinx.Common.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-03-04: Wire up `DefaultMemoryPressureHandler` in `Program.cs`

```xml
<task>
  <id>1-03-04</id>
  <description>Integrate pressure handler into emulator startup</description>
  <requirement>MEM-02</requirement>
  <read_first>
    - src/Ryujinx/Program.cs (after Plan 01 changes)
    - src/Ryujinx/Utilities/Memory/DefaultMemoryPressureHandler.cs
  </read_first>
  <action>
    Open `src/Ryujinx/Program.cs`.

    After GPU context initialization (where `GpuContext` or renderer is created), add:
    ```csharp
    var pressureHandler = new DefaultMemoryPressureHandler(gpuContext);
    var memoryTracker = new MemoryBudgetManager(memoryProvider, pressureHandler);
    memoryTracker.TrackNativeMemory();
    memoryTracker.Start();
    ```
    Ensure `memoryTracker` is disposed on shutdown.
  </action>
  <acceptance_criteria>
    - `grep "new DefaultMemoryPressureHandler" src/Ryujinx/Program.cs` returns 1 match.
    - `grep "TrackNativeMemory" src/Ryujinx/Program.cs` returns 1 match.
    - `dotnet build src/Ryujinx/Ryujinx.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-03-05: Add unit tests for threshold behavior

```xml
<task>
  <id>1-03-05</id>
  <description>Test threshold crossing triggers correct actions</description>
  <requirement>MEM-02</requirement>
  <read_first>
    - tests/Ryujinx.Tests/Memory/MemoryBudgetManagerTests.cs (from Plan 01)
    - src/Ryujinx.Common/Memory/IMemoryPressureHandler.cs
  </read_first>
  <action>
    Create `tests/Ryujinx.Tests/Memory/MemoryBudgetManagerThresholdTests.cs`:

    ```csharp
    public class MemoryBudgetManagerThresholdTests
    {
        [Fact]
        public void SoftLimit_Triggers_GC_Optimized()
        {
            // Arrange: fake provider returning 3.6GB RSS
            // Act: create manager, force evaluation
            // Assert: GC.Collect was invoked with mode Optimized
        }

        [Fact]
        public void HardLimit_Triggers_CacheEviction()
        {
            // Arrange: fake provider returning 4.1GB RSS
            // Act: create manager with mock IMemoryPressureHandler
            // Assert: mock.OnHardLimitExceeded() was called exactly once
        }

        [Fact]
        public void OomLimit_Triggers_EmergencyFlush()
        {
            // Arrange: fake provider returning 4.6GB RSS
            // Act: create manager with mock IMemoryPressureHandler
            // Assert: mock.OnOomLimitExceeded() was called exactly once
        }

        [Fact]
        public void NormalLevel_DoesNotTriggerActions()
        {
            // Arrange: fake provider returning 2GB RSS
            // Act: create manager with mock IMemoryPressureHandler
            // Assert: no handler methods called
        }
    }
    ```

    Use `Moq` or manual mock for `IMemoryInfoProvider` and `IMemoryPressureHandler`.
    For GC test, use a static flag or `GC.CollectionCount(2)` before/after comparison.
  </action>
  <acceptance_criteria>
    - `dotnet test --filter 'MemoryBudgetManagerThresholdTests'` exits 0.
    - All 4 tests pass.
  </acceptance_criteria>
</task>
```

### Task 1-03-06: Add active Swap = 0 monitoring and alerting

```xml
<task>
  <id>1-03-06</id>
  <description>Monitor swap usage via CompressorPageCount and alert when Swap > 0</description>
  <requirement>MEM-03</requirement>
  <read_first>
    - src/Ryujinx.Common/Memory/MemoryBudgetManager.cs (after Task 1-03-01 changes)
    - src/Ryujinx.Common/Memory/MemorySnapshot.cs
    - .planning/phases/01-memory-stop-the-bleeding/01-RESEARCH.md §8.4
  </read_first>
  <action>
    Open `src/Ryujinx.Common/Memory/MemoryBudgetManager.cs`.

    Add a swap alert threshold constant:
    ```csharp
    public const long SwapAlertThresholdBytes = 0L;
    ```

    In the timer callback (after `EvaluatePressure`), add swap monitoring logic:
    ```csharp
    private void CheckSwapPressure(MemorySnapshot snapshot)
    {
        if (snapshot.SwapBytes > SwapAlertThresholdBytes)
        {
            Logger.Warning?.Print(LogClass.Emulation,
                $"Swap pressure detected: {snapshot.SwapBytes / 1024 / 1024} MB (CompressorPageCount). " +
                "Target is Swap = 0. Consider closing background applications.");
        }
    }
    ```

    Also add a `SwapPressureDetected` event to `IMemoryTracker` (if not already present) or reuse `PressureChanged` with a dedicated check:
    ```csharp
    public event EventHandler<long> SwapPressureDetected;
    ```
    Invoke `SwapPressureDetected?.Invoke(this, snapshot.SwapBytes)` when `SwapBytes > 0`.

    Ensure the swap check runs on every sample tick, independent of RSS threshold evaluation, so that swap usage is actively surfaced even when RSS is below soft/hard limits.
  </action>
  <acceptance_criteria>
    - `grep "SwapAlertThresholdBytes" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 1 match.
    - `grep "Swap pressure detected" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 1 match.
    - `grep "SwapPressureDetected" src/Ryujinx.Common/Memory/MemoryBudgetManager.cs` returns at least 2 matches (declaration + invocation).
    - `grep "SwapPressureDetected" src/Ryujinx.Common/Memory/IMemoryTracker.cs` returns at least 1 match.
    - `dotnet build src/Ryujinx.Common/Ryujinx.Common.csproj` exits 0.
  </acceptance_criteria>
</task>
```
