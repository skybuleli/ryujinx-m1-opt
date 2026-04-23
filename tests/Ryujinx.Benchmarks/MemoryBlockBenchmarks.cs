using BenchmarkDotNet.Attributes;
using Ryujinx.Memory;
using System;

namespace Ryujinx.Benchmarks
{
    [Config(typeof(AppleSiliconConfig))]
    [MemoryDiagnoser]
    public class MemoryBlockBenchmarks
    {
        private MemoryBlock _block;
        private byte[] _data4K;
        private byte[] _scratch4K;

        [GlobalSetup]
        public void Setup()
        {
            _block = new MemoryBlock(64 * 1024 * 1024, MemoryAllocationFlags.Reserve);
            _data4K = new byte[4096];
            _scratch4K = new byte[4096];
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
            _block.Read(0, _scratch4K);
        }
    }
}
