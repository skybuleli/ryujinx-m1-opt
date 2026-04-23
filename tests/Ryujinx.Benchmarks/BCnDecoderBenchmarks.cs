using BenchmarkDotNet.Attributes;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.Texture;
using System;

namespace Ryujinx.Benchmarks
{
    [Config(typeof(AppleSiliconConfig))]
    [MemoryDiagnoser]
    public class BCnDecoderBenchmarks
    {
        private byte[] _data;
        private int _width = 1024;
        private int _height = 1024;

        [GlobalSetup]
        public void Setup()
        {
            // BC3 is 1 byte per pixel (16 bytes per 4x4 block)
            // Width * Height = 1024 * 1024 pixels.
            // Data size = 1024 * 1024 bytes.
            _data = new byte[_width * _height];
            new Random(42).NextBytes(_data);
        }

        [Benchmark]
        public void DecodeBC3()
        {
            // Decode a single layer, single level 1024x1024 texture
            using MemoryOwner<byte> result = BCnDecoder.DecodeBC3(_data, _width, _height, 1, 1, 1);
        }
        
        [Benchmark]
        public void DecodeBC1()
        {
             // BC1 is 0.5 bytes per pixel (8 bytes per 4x4 block)
             // We use half the data for BC1 relative to BC3 for the same resolution
             // But DecodeBC1 expects the buffer size to match the texture dimensions.
             // 1024x1024 * 0.5 = 512KB. 
             // Our _data is 1MB, so we just take a slice.
             
             int bc1Size = (_width * _height) / 2;
             using MemoryOwner<byte> result = BCnDecoder.DecodeBC1(_data.AsSpan().Slice(0, bc1Size), _width, _height, 1, 1, 1);
        }

        [Benchmark]
        public void DecodeBC2()
        {
            using MemoryOwner<byte> result = BCnDecoder.DecodeBC2(_data, _width, _height, 1, 1, 1);
        }

        [Benchmark]
        public void DecodeBC4()
        {
            int bc4Size = (_width * _height) / 2;
            using MemoryOwner<byte> result = BCnDecoder.DecodeBC4(_data.AsSpan().Slice(0, bc4Size), _width, _height, 1, 1, 1, signed: false);
        }

        [Benchmark]
        public void DecodeBC5()
        {
            using MemoryOwner<byte> result = BCnDecoder.DecodeBC5(_data, _width, _height, 1, 1, 1, signed: false);
        }

        [Benchmark]
        public void DecodeBC6()
        {
            using MemoryOwner<byte> result = BCnDecoder.DecodeBC6(_data, _width, _height, 1, 1, 1, signed: false);
        }

        [Benchmark]
        public void DecodeBC7()
        {
            using MemoryOwner<byte> result = BCnDecoder.DecodeBC7(_data, _width, _height, 1, 1, 1);
        }
    }
}
