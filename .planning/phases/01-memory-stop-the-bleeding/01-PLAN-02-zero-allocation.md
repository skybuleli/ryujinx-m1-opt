---
phase: 1
plan: 02
type: execute
wave: 1
depends_on: []
files_modified:
  - src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs
  - src/Ryujinx.Graphics.Texture/LayoutConverter.cs
  - src/Ryujinx/UI/ViewModels/SettingsViewModel.cs
  - src/Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs
  - src/Ryujinx.HLE/FileSystem/VirtualFileSystem.cs
  - src/Ryujinx.Graphics.Gpu/Image/Texture.cs
autonomous: true
requirements:
  - CPU-03
---

# Plan 02: Zero-Allocation Optimization (CPU-03)

## Goal
Eliminate heap allocations in the hottest texture decoding and stream creation paths, achieving 0 B/op in BenchmarkDotNet `[MemoryDiagnoser]` for targeted decoders.

## Verification Criteria
1. `AstcDecoder` optimized path shows `0 B/op` in BenchmarkDotNet.
2. `LayoutConverter` no longer falls back to `new byte[]` when caller passes empty span.
3. No `new MemoryStream(` calls remain in `src/` except UI (all 3 known instances replaced).
4. `Texture.cs` memory ownership chains remain intact (no leaks, no use-after-free).

## must_haves
- [ ] `AstcDecoder.TryDecodeToRgba8(out Span<byte>)` overload is either removed or changed to return `MemoryOwner<byte>` with `MemoryOwner<byte>.Rent()`.
- [ ] `LayoutConverter` void-return overloads with `Span<byte> output` throw `ArgumentException` when `output.Length == 0` instead of allocating `new byte[]`.
- [ ] All 3 remaining `new MemoryStream()` calls in `src/` are replaced with `MemoryStreamManager.Shared.GetStream()`.
- [ ] `Texture.cs` `using(decoded)` disposal chains compile and tests pass.

---

## Tasks

### Task 1-02-01: Fix `AstcDecoder.TryDecodeToRgba8` heap allocation

```xml
<task>
  <id>1-02-01</id>
  <description>Replace new byte[] with MemoryOwner<byte>.Rent in AstcDecoder</description>
  <requirement>CPU-03</requirement>
  <read_first>
    - src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs (line 232 and surrounding overloads)
    - src/Ryujinx.Graphics.Texture/BCnDecoder.cs (canonical MemoryOwner<byte>.Rent pattern)
    - src/Ryujinx.Graphics.Gpu/Image/Texture.cs (lines 797-824, caller disposal chain)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §2.1
  </read_first>
  <action>
    Open `src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs`.

    Locate the overload at line 232:
    ```csharp
    public static bool TryDecodeToRgba8(..., out Span<byte> decoded)
    {
        byte[] output = new byte[QueryDecompressedSize(...)]; // ❌
        // ...
        decoded = output;
        return decoder.Success;
    }
    ```

    Grep `src/` to confirm zero callers of `TryDecodeToRgba8(..., out Span<byte> decoded)`:
    `grep -r "TryDecodeToRgba8" src/ --include="*.cs" | grep -v "TryDecodeToRgba8P"`
    If zero callers found (only `TryDecodeToRgba8P` remains), **delete** the `out Span<byte>` overload entirely.

    If any callers exist, change the signature to:
    ```csharp
    public static bool TryDecodeToRgba8(..., out MemoryOwner<byte> decoded)
    {
        MemoryOwner<byte> output = MemoryOwner<byte>.Rent(QueryDecompressedSize(...)); // ✅
        var decoder = new AstcDecoder(data, output.Memory, ...);
        // ...
        decoded = output;
        return decoder.Success;
    }
    ```
    Ensure the method returns `MemoryOwner<byte>` so callers can dispose it.

    Verify `Texture.cs` lines 797-824 still compiles:
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
    The `TryDecodeToRgba8P` path must remain unchanged and functional.
  </action>
  <acceptance_criteria>
    - `grep -n "byte\[\] output = new byte\[" src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs` returns 0 matches.
    - `grep -n "MemoryOwner<byte>.Rent" src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs` returns at least 1 match.
    - `dotnet build src/Ryujinx.Graphics.Texture/Ryujinx.Graphics.Texture.csproj` exits 0.
    - `dotnet build src/Ryujinx.Graphics.Gpu/Ryujinx.Graphics.Gpu.csproj` exits 0 (verifies Texture.cs caller compatibility).
  </acceptance_criteria>
</task>
```

### Task 1-02-02: Fix `LayoutConverter.cs` new byte[] fallbacks

```xml
<task>
  <id>1-02-02</id>
  <description>Remove heap allocation fallbacks in LayoutConverter</description>
  <requirement>CPU-03</requirement>
  <read_first>
    - src/Ryujinx.Graphics.Texture/LayoutConverter.cs (lines 390-393 and 552-555)
    - src/Ryujinx.Graphics.Texture/LayoutConverter.cs (line 98, existing MemoryOwner pattern)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §2.2
  </read_first>
  <action>
    Open `src/Ryujinx.Graphics.Texture/LayoutConverter.cs`.

    Find lines 390-393:
    ```csharp
    if (output.Length == 0)
    {
        output = new byte[sizeInfo.TotalSize]; // ❌
    }
    ```
    Replace with:
    ```csharp
    if (output.Length == 0)
    {
        throw new ArgumentException(
            "Output buffer must be pre-allocated. Use MemoryOwner<byte>.Rent(size) or provide a valid Span<byte>.",
            nameof(output));
    }
    ```

    Find lines 552-555:
    ```csharp
    if (output.Length == 0)
    {
        output = new byte[h * stride]; // ❌
    }
    ```
    Replace with identical `ArgumentException` throw.

    Grep all callers of these `ConvertLinearToBlockLinear` / `ConvertBlockLinearToLinear` overloads in `src/`:
    `grep -r "ConvertLinearToBlockLinear\|ConvertBlockLinearToLinear" src/ --include="*.cs" -n`
    For any caller passing `Span<byte>.Empty`, update the caller to rent a buffer first:
    ```csharp
    using var output = MemoryOwner<byte>.Rent(size);
    LayoutConverter.ConvertLinearToBlockLinear(output.Span, ...);
    ```
    There should be very few callers (check `Texture.cs` and any test files).
  </action>
  <acceptance_criteria>
    - `grep -n "new byte\[" src/Ryujinx.Graphics.Texture/LayoutConverter.cs` returns 0 matches.
    - `grep -n "ArgumentException" src/Ryujinx.Graphics.Texture/LayoutConverter.cs` returns at least 2 matches.
    - `dotnet build src/Ryujinx.Graphics.Texture/Ryujinx.Graphics.Texture.csproj` exits 0.
    - `dotnet test tests/Ryujinx.Tests/` exits 0 (no regressions from caller changes).
  </acceptance_criteria>
</task>
```

### Task 1-02-03: Replace remaining `new MemoryStream()` calls with `MemoryStreamManager.Shared.GetStream()`

```xml
<task>
  <id>1-02-03</id>
  <description>Replace all remaining new MemoryStream allocations with pooled streams</description>
  <requirement>CPU-03</requirement>
  <read_first>
    - src/Ryujinx.Common/Memory/MemoryStreamManager.cs (existing wrapper API)
    - src/Ryujinx/UI/ViewModels/SettingsViewModel.cs (line 454)
    - src/Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs (line 422)
    - src/Ryujinx.HLE/FileSystem/VirtualFileSystem.cs (line 288)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §3.1
  </read_first>
  <action>
    For each of the 3 files, replace `new MemoryStream(buffer)` with `MemoryStreamManager.Shared.GetStream(buffer)`.

    File 1: `src/Ryujinx/UI/ViewModels/SettingsViewModel.cs` line 454
    - BEFORE: `using var ms = new MemoryStream(gameIconData);`
    - AFTER: `using var ms = MemoryStreamManager.Shared.GetStream(gameIconData);`
    - Add `using Ryujinx.Common.Memory;` if missing.

    File 2: `src/Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs` line 422
    - BEFORE: `using var ms = new MemoryStream(temp);`
    - AFTER: `using var ms = MemoryStreamManager.Shared.GetStream(temp);`
    - Add `using Ryujinx.Common.Memory;` if missing.

    File 3: `src/Ryujinx.HLE/FileSystem/VirtualFileSystem.cs` line 288
    - BEFORE: `using var ms = new MemoryStream(ticketData);`
    - AFTER: `using var ms = MemoryStreamManager.Shared.GetStream(ticketData);`
    - Add `using Ryujinx.Common.Memory;` if missing.
  </action>
  <acceptance_criteria>
    - `grep -rn "new MemoryStream(" src/Ryujinx/UI/ViewModels/SettingsViewModel.cs src/Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs src/Ryujinx.HLE/FileSystem/VirtualFileSystem.cs` returns 0 matches.
    - `grep "MemoryStreamManager.Shared.GetStream" src/Ryujinx/UI/ViewModels/SettingsViewModel.cs` returns 1 match.
    - `grep "MemoryStreamManager.Shared.GetStream" src/Ryujinx.HLE/HOS/Services/Time/IStaticServiceForPsc.cs` returns 1 match.
    - `grep "MemoryStreamManager.Shared.GetStream" src/Ryujinx.HLE/FileSystem/VirtualFileSystem.cs` returns 1 match.
    - `dotnet build` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-02-04: Verify `Texture.cs` memory ownership chains

```xml
<task>
  <id>1-02-04</id>
  <description>Audit Texture.cs to ensure MemoryOwner disposal chains are intact</description>
  <requirement>CPU-03</requirement>
  <read_first>
    - src/Ryujinx.Graphics.Gpu/Image/Texture.cs (lines 797-824 and all AstcDecoder/BCnEncoder call sites)
    - src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs (after Task 1-02-01 changes)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §2.1 (disposal chain rules)
  </read_first>
  <action>
    Open `src/Ryujinx.Graphics.Gpu/Image/Texture.cs`.

    Search for all occurrences of `AstcDecoder.` and `BCnEncoder.`.
    Verify each `MemoryOwner<byte>` returned by a decoder is either:
    a) Disposed via `using` before method returns, OR
    b) Returned to the caller who assumes disposal responsibility.

    Specifically verify lines 797-824:
    ```csharp
    using (result)
    {
        if (!AstcDecoder.TryDecodeToRgba8P(..., out MemoryOwner<byte> decoded))
        {
            // handle failure
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
    - `result` is disposed by the outer `using`.
    - `decoded` is disposed by inner `using` when recompression is enabled.
    - `decoded` is returned (caller disposes) when recompression is disabled.
    - This chain must remain valid after any AstcDecoder signature changes.

    If `TryDecodeToRgba8P` signature was changed in Task 1-02-01, ensure `Texture.cs` call sites are updated to match.
    Run the full test suite to catch any dispose-after-return or use-after-free issues:
    `dotnet test tests/Ryujinx.Tests/`
  </action>
  <acceptance_criteria>
    - `grep -n "using (result)" src/Ryujinx.Graphics.Gpu/Image/Texture.cs` returns at least 1 match.
    - `grep -n "using (decoded)" src/Ryujinx.Graphics.Gpu/Image/Texture.cs` returns at least 1 match.
    - `grep -n "return decoded;" src/Ryujinx.Graphics.Gpu/Image/Texture.cs` returns at least 1 match.
    - `dotnet test tests/Ryujinx.Tests/` exits 0.
    - No `IDisposable` analyzer warnings in `Texture.cs` after build.
  </acceptance_criteria>
</task>
```
