---
phase: 1
plan: 04
type: execute
wave: 1
depends_on: []
files_modified:
  - tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs
  - tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs
  - tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs
  - tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs
  - tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj
  - tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs
autonomous: true
requirements:
  - QA-01
---

# Plan 04: BenchmarkDotNet Suite (QA-01)

## Goal
Establish a comprehensive micro-benchmark suite covering texture decoders (BCn, ASTC, ETC2) and memory core operations, providing baseline performance data and allocation metrics for Phase 1 optimizations.

## Verification Criteria
1. All benchmarks compile and run successfully on `net10.0` + Apple Silicon.
2. `[MemoryDiagnoser]` reports `0 B/op` for ASTC optimized path.
3. Benchmark output artifacts are written to `BenchmarkDotNet.Artifacts/`.
4. Each decoder format has at least one benchmark method.

## must_haves
- [ ] `BCnDecoderBenchmarks` covers BC1, BC2, BC3, BC4, BC5, BC6, BC7.
- [ ] `AstcDecoderBenchmarks` covers 4x4 and 8x8 block sizes with `TryDecodeToRgba8P`.
- [ ] `ETC2DecoderBenchmarks` covers RGB, PTA, RGBA.
- [ ] `MemoryBlockBenchmarks` covers Commit/Decommit and Read/Write.
- [ ] All decoder benchmarks use `[MemoryDiagnoser]` and `[GlobalSetup]` with seeded random data.
- [ ] `AppleSiliconConfig` configures ARM64 + RyuJIT runtime.

---

## Tasks

### Task 1-04-01: Extend `BCnDecoderBenchmarks` with all BCn formats

```xml
<task>
  <id>1-04-01</id>
  <description>Add BC2, BC4, BC5, BC6, BC7 benchmarks to existing suite</description>
  <requirement>QA-01</requirement>
  <read_first>
    - tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs (existing BC1/BC3 benchmarks)
    - src/Ryujinx.Graphics.Texture/BCnDecoder.cs (all public Decode* method signatures)
    - Directory.Packages.props (BenchmarkDotNet version)
  </read_first>
  <action>
    Open `tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs`.

    Verify existing `BCnDecoderBenchmarks` class has:
    - `[MemoryDiagnoser]` attribute.
    - `[GlobalSetup]` with `new Random(42).NextBytes(_data)`.
    - `_width = 1024`, `_height = 1024`.

    Add the following benchmark methods (following exact pattern of existing `DecodeBC1` / `DecodeBC3`):

    ```csharp
    [Benchmark]
    public void DecodeBC2()
    {
        using var result = BCnDecoder.DecodeBC2(_data, _width, _height, 1, 1, 1);
    }

    [Benchmark]
    public void DecodeBC4()
    {
        using var result = BCnDecoder.DecodeBC4(_data, _width, _height, 1, 1, 1);
    }

    [Benchmark]
    public void DecodeBC5()
    {
        using var result = BCnDecoder.DecodeBC5(_data, _width, _height, 1, 1, 1);
    }

    [Benchmark]
    public void DecodeBC6()
    {
        using var result = BCnDecoder.DecodeBC6(_data, _width, _height, 1, 1, 1);
    }

    [Benchmark]
    public void DecodeBC7()
    {
        using var result = BCnDecoder.DecodeBC7(_data, _width, _height, 1, 1, 1);
    }
    ```

    Ensure `_data` size is sufficient for all formats. BC1/BC4 use 8 bytes per block; BC2/BC3/BC5/BC6/BC7 use 16 bytes per block. For 1024x1024:
    - `_data` must be at least `1024 * 1024` bytes (current size is fine for BC1/BC4; for BC7 it needs `(1024/4)*(1024/4)*16 = 1,048,576` bytes — same size). Confirm `_data = new byte[_width * _height];` is sufficient.
  </action>
  <acceptance_criteria>
    - `grep "DecodeBC2" tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` returns at least 1 match.
    - `grep "DecodeBC4" tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` returns at least 1 match.
    - `grep "DecodeBC5" tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` returns at least 1 match.
    - `grep "DecodeBC6" tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` returns at least 1 match.
    - `grep "DecodeBC7" tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs` returns at least 1 match.
    - `dotnet build tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-04-02: Create `AstcDecoderBenchmarks`

```xml
<task>
  <id>1-04-02</id>
  <description>Create ASTC 4x4 and 8x8 decoder benchmarks with MemoryDiagnoser</description>
  <requirement>QA-01</requirement>
  <read_first>
    - src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs (TryDecodeToRgba8P signature)
    - tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs (skeleton pattern)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §4.2
  </read_first>
  <action>
    Create `tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs`:

    ```csharp
    using BenchmarkDotNet.Attributes;
    using Ryujinx.Common.Memory;
    using Ryujinx.Graphics.Texture.Astc;
    using System;

    [MemoryDiagnoser]
    public class AstcDecoderBenchmarks
    {
        private byte[] _data4x4;
        private byte[] _data8x8;
        private const int Width = 1024;
        private const int Height = 1024;

        [GlobalSetup]
        public void Setup()
        {
            int blocks4x4 = (Width / 4) * (Height / 4);
            int blocks8x8 = (Width / 8) * (Height / 8);
            _data4x4 = new byte[blocks4x4 * 16];
            _data8x8 = new byte[blocks8x8 * 16];
            new Random(42).NextBytes(_data4x4);
            new Random(42).NextBytes(_data8x8);
        }

        [Benchmark]
        public void DecodeAstc4x4()
        {
            if (AstcDecoder.TryDecodeToRgba8P(_data4x4, 4, 4, Width, Height, 1, 1, 1, out MemoryOwner<byte> decoded))
            {
                decoded.Dispose();
            }
        }

        [Benchmark]
        public void DecodeAstc8x8()
        {
            if (AstcDecoder.TryDecodeToRgba8P(_data8x8, 8, 8, Width, Height, 1, 1, 1, out MemoryOwner<byte> decoded))
            {
                decoded.Dispose();
            }
        }
    }
    ```

    Add project reference in `tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` if `Ryujinx.Graphics.Texture` is not already referenced.
  </action>
  <acceptance_criteria>
    - File `tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` exists.
    - `grep "class AstcDecoderBenchmarks" tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` returns 1 match.
    - `grep "MemoryDiagnoser" tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` returns 1 match.
    - `grep "TryDecodeToRgba8P" tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` returns at least 2 matches.
    - `dotnet build tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-04-03: Create `ETC2DecoderBenchmarks`

```xml
<task>
  <id>1-04-03</id>
  <description>Create ETC2 RGB, PTA, RGBA decoder benchmarks</description>
  <requirement>QA-01</requirement>
  <read_first>
    - src/Ryujinx.Graphics.Texture/ETC2Decoder.cs (DecodeRgb, DecodePta, DecodeRgba signatures)
    - tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs (skeleton pattern)
  </read_first>
  <action>
    Create `tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs`:

    ```csharp
    using BenchmarkDotNet.Attributes;
    using Ryujinx.Common.Memory;
    using Ryujinx.Graphics.Texture;
    using System;

    [MemoryDiagnoser]
    public class ETC2DecoderBenchmarks
    {
        private byte[] _data;
        private const int Width = 1024;
        private const int Height = 1024;

        [GlobalSetup]
        public void Setup()
        {
            // ETC2 block is 8 bytes
            int blocks = (Width / 4) * (Height / 4);
            _data = new byte[blocks * 8];
            new Random(42).NextBytes(_data);
        }

        [Benchmark]
        public void DecodeRgb()
        {
            using var result = ETC2Decoder.DecodeRgb(_data, Width, Height, 1, 1, 1);
        }

        [Benchmark]
        public void DecodePta()
        {
            using var result = ETC2Decoder.DecodePta(_data, Width, Height, 1, 1, 1);
        }

        [Benchmark]
        public void DecodeRgba()
        {
            using var result = ETC2Decoder.DecodeRgba(_data, Width, Height, 1, 1, 1);
        }
    }
    ```
  </action>
  <acceptance_criteria>
    - File `tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` exists.
    - `grep "class ETC2DecoderBenchmarks" tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` returns 1 match.
    - `grep "DecodeRgb" tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` returns at least 1 match.
    - `grep "DecodePta" tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` returns at least 1 match.
    - `grep "DecodeRgba" tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` returns at least 1 match.
    - `dotnet build tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-04-04: Create `MemoryBlockBenchmarks`

```xml
<task>
  <id>1-04-04</id>
  <description>Create MemoryBlock Commit/Decommit/Read/Write benchmarks</description>
  <requirement>QA-01</requirement>
  <read_first>
    - src/Ryujinx.Memory/MemoryBlock.cs (public API)
    - tests/Ryujinx.Benchmarks/BCnDecoderBenchmarks.cs (skeleton pattern)
    - .planning/phases/01-memory-stop-the-bleeding/01-PATTERNS.md §4.4
  </read_first>
  <action>
    Create `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs`:

    ```csharp
    using BenchmarkDotNet.Attributes;
    using Ryujinx.Memory;
    using System;

    [MemoryDiagnoser]
    public class MemoryBlockBenchmarks
    {
        private MemoryBlock _block;
        private byte[] _data4K;

        [GlobalSetup]
        public void Setup()
        {
            _block = new MemoryBlock(64 * 1024 * 1024, MemoryAllocationFlags.Reserve);
            _data4K = new byte[4096];
            new Random(42).NextBytes(_data4K);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _block.Dispose();
        }

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

        [Benchmark]
        public void Read4K()
        {
            _block.Commit(0, 4096);
            _block.Write(0, _data4K);
            _block.Read(0, _data4K.Length);
        }
    }
    ```

    Add `Ryujinx.Memory` project reference to `tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` if missing:
    ```xml
    <ProjectReference Include="..\..\src\Ryujinx.Memory\Ryujinx.Memory.csproj" />
    ```
  </action>
  <acceptance_criteria>
    - File `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` exists.
    - `grep "class MemoryBlockBenchmarks" tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` returns 1 match.
    - `grep "CommitDecommit4K" tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` returns 1 match.
    - `grep "Write4K" tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` returns 1 match.
    - `grep "Read4K" tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` returns 1 match.
    - `grep "Ryujinx.Memory" tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` returns at least 1 match.
    - `dotnet build tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj` exits 0.
  </acceptance_criteria>
</task>
```

### Task 1-04-05: Create `AppleSiliconConfig` and run full benchmark suite

```xml
<task>
  <id>1-04-05</id>
  <description>Add Apple Silicon runtime config and validate all benchmarks run</description>
  <requirement>QA-01</requirement>
  <read_first>
    - tests/Ryujinx.Benchmarks/Ryujinx.Benchmarks.csproj
    - .planning/phases/01-memory-stop-the-bleeding/01-RESEARCH.md §4.3
  </read_first>
  <action>
    Create `tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs`:

    ```csharp
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Environments;
    using BenchmarkDotNet.Toolchains.InProcess.Emit;

    public class AppleSiliconConfig : ManualConfig
    {
        public AppleSiliconConfig()
        {
            AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core100)
                .WithPlatform(Platform.Arm64)
                .WithJit(Jit.RyuJit)
                .WithId("AppleSilicon"));

            AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
            AddExporter(BenchmarkDotNet.Exporters.JsonExporter.Full);
            AddDiagnoser(new BenchmarkDotNet.Diagnosers.MemoryDiagnoser());
        }
    }
    ```

    Apply config to all benchmark classes by adding:
    ```csharp
    [Config(typeof(AppleSiliconConfig))]
    ```
    to `BCnDecoderBenchmarks`, `AstcDecoderBenchmarks`, `ETC2DecoderBenchmarks`, and `MemoryBlockBenchmarks`.

    Run a quick validation of all benchmarks (dry-run / 1 iteration each):
    ```bash
    dotnet run --project tests/Ryujinx.Benchmarks/ -- -f '*DecoderBenchmarks*' --job short --iterationCount 1 --warmupCount 0
    ```
    If the above command fails due to timeout, use a more targeted filter:
    ```bash
    dotnet run --project tests/Ryujinx.Benchmarks/ -- -f 'AstcDecoderBenchmarks' --job short --iterationCount 1 --warmupCount 0
    ```
  </action>
  <acceptance_criteria>
    - File `tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs` exists.
    - `grep "class AppleSiliconConfig" tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs` returns 1 match.
    - `grep "CoreRuntime.Core100" tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs` returns 1 match.
    - `grep "Platform.Arm64" tests/Ryujinx.Benchmarks/AppleSiliconConfig.cs` returns 1 match.
    - At least one benchmark class contains `Config(typeof(AppleSiliconConfig))`.
    - `dotnet run --project tests/Ryujinx.Benchmarks/ -- -f 'AstcDecoderBenchmarks' --job short --iterationCount 1 --warmupCount 0` exits 0 and produces output in `BenchmarkDotNet.Artifacts/`.
  </acceptance_criteria>
</task>
```
