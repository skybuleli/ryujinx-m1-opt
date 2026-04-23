```

BenchmarkDotNet v0.15.8, macOS Ventura 13.5.2 (22G91) [Darwin 22.6.0]
Apple M1, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.101
  [Host]       : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  AppleSilicon : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=AppleSilicon  Jit=RyuJit  Platform=Arm64  
Runtime=.NET 10.0  IterationCount=1  LaunchCount=1  
WarmupCount=0  

```
| Method        | Mean      | Error | Gen0      | Gen1     | Gen2     | Allocated |
|-------------- |----------:|------:|----------:|---------:|---------:|----------:|
| DecodeAstc4x4 | 132.11 ms |    NA | 3750.0000 | 500.0000 | 500.0000 |  23.74 MB |
| DecodeAstc8x8 |  31.06 ms |    NA | 1375.0000 | 562.5000 | 562.5000 |   9.05 MB |
