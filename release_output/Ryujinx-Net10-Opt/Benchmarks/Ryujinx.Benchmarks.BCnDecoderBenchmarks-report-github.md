```

BenchmarkDotNet v0.15.8, macOS Ventura 13.5.2 (22G91) [Darwin 22.6.0]
Apple M1, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.101
  [Host] : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method    | Mean     | Error    | StdDev    | Allocated |
|---------- |---------:|---------:|----------:|----------:|
| DecodeBC3 | 2.333 ms | 1.149 ms | 0.0630 ms |      64 B |
| DecodeBC1 | 1.267 ms | 1.653 ms | 0.0906 ms |      68 B |
