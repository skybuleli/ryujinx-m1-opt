using BenchmarkDotNet.Attributes;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.Texture.Astc;
using System;

namespace Ryujinx.Benchmarks
{
    [Config(typeof(AppleSiliconConfig))]
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
}
