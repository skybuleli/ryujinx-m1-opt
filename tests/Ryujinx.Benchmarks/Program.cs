using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.CompilerServices;

namespace Ryujinx.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }

    // [DisassemblyDiagnoser(printSource: true)]
    public class BitUtilsBenchmarks
    {
        private ulong _value;

        [GlobalSetup]
        public void Setup()
        {
            _value = 0x1234567890ABCDEF;
        }

        // 基准：手动位移实现 (约 20-30 条指令)
        [Benchmark(Baseline = true)]
        public ulong Manual()
        {
            ulong value = _value;
            value = ((value & 0xaaaaaaaaaaaaaaaa) >> 1) | ((value & 0x5555555555555555) << 1);
            value = ((value & 0xcccccccccccccccc) >> 2) | ((value & 0x3333333333333333) << 2);
            value = ((value & 0xf0f0f0f0f0f0f0f0) >> 4) | ((value & 0x0f0f0f0f0f0f0f0f) << 4);
            value = ((value & 0xff00ff00ff00ff00) >> 8) | ((value & 0x00ff00ff00ff00ff) << 8);
            value = ((value & 0xffff0000ffff0000) >> 16) | ((value & 0x0000ffff0000ffff) << 16);
            return (value >> 32) | (value << 32);
        }

        // 优化：硬件指令实现 (约 5 条指令)
        // 使用 ArmBase.ReverseElementBits (32-bit) x 2
        [Benchmark]
        public ulong Intrinsic()
        {
            if (ArmBase.IsSupported)
            {
                uint low = (uint)_value;
                uint high = (uint)(_value >> 32);

                // 硬件 RBIT 指令
                uint rLow = ArmBase.ReverseElementBits(low);
                uint rHigh = ArmBase.ReverseElementBits(high);

                // 交换位置并合并
                return ((ulong)rLow << 32) | rHigh;
            }
            return 0;
        }
    }
}
