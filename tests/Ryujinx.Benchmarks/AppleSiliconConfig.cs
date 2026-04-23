using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;

namespace Ryujinx.Benchmarks
{
    public class AppleSiliconConfig : ManualConfig
    {
        public AppleSiliconConfig()
        {
            AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core10_0)
                .WithPlatform(Platform.Arm64)
                .WithJit(Jit.RyuJit)
                .WithId("AppleSilicon"));

            AddLogger(ConsoleLogger.Default);
        }
    }
}
