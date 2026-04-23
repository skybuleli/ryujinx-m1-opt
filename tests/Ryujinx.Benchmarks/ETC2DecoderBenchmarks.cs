using BenchmarkDotNet.Attributes;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.Texture;
using System;

namespace Ryujinx.Benchmarks
{
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
}
