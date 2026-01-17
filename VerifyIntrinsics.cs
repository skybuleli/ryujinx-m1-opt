using System;
using System.Runtime.Intrinsics.Arm;

class Program {
    static void Main() {
        Console.WriteLine($"ArmBase.IsSupported: {ArmBase.IsSupported}");
        
        ulong input = 0x1234567890ABCDEF;
        ulong expected = 0xEFCDAB9078563412; // 错误的字节序反转，RBIT 是位反转
        // 手动计算 RBIT(0x12...) 
        // 0x1 = 0001 -> 1000 = 0x8
        // 0x2 = 0010 -> 0100 = 0x4
        // ...
        // 这太复杂了，我们直接对比 Manual 和 Intrinsic 的输出。
        
        ulong manual = ReverseBits64_Manual(input);
        ulong intrinsic = ReverseBits64_Intrinsic(input);
        
        Console.WriteLine($"Input:     {input:X16}");
        Console.WriteLine($"Manual:    {manual:X16}");
        Console.WriteLine($"Intrinsic: {intrinsic:X16}");
        
        if (manual == intrinsic) {
            Console.WriteLine("SUCCESS: Results match!");
        } else {
            Console.WriteLine("FAILURE: Results do not match.");
        }
    }

    static ulong ReverseBits64_Manual(ulong value)
    {
        value = ((value & 0xaaaaaaaaaaaaaaaa) >> 1) | ((value & 0x5555555555555555) << 1);
        value = ((value & 0xcccccccccccccccc) >> 2) | ((value & 0x3333333333333333) << 2);
        value = ((value & 0xf0f0f0f0f0f0f0f0) >> 4) | ((value & 0x0f0f0f0f0f0f0f0f) << 4);
        value = ((value & 0xff00ff00ff00ff00) >> 8) | ((value & 0x00ff00ff00ff00ff) << 8);
        value = ((value & 0xffff0000ffff0000) >> 16) | ((value & 0x0000ffff0000ffff) << 16);
        return (value >> 32) | (value << 32);
    }

    static ulong ReverseBits64_Intrinsic(ulong value)
    {
        if (ArmBase.IsSupported)
        {
            uint low = (uint)value;
            uint high = (uint)(value >> 32);
            uint rLow = ArmBase.ReverseElementBits(low);
            uint rHigh = ArmBase.ReverseElementBits(high);
            return ((ulong)rLow << 32) | rHigh;
        }
        return 0;
    }
}
