using System;

namespace Ryujinx.Common.Memory
{
    public readonly record struct MemorySnapshot(
        DateTime Timestamp,
        long RssBytes,
        long GcHeapBytes,
        long UnmanagedBytes,
        long SwapBytes,
        MemoryPressureLevel PressureLevel);
}
