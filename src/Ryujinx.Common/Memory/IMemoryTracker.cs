using System;

namespace Ryujinx.Common.Memory
{
    public interface IMemoryTracker
    {
        event EventHandler<MemoryPressureEventArgs> PressureChanged;
        event EventHandler<long> SwapPressureDetected;
        MemorySnapshot LastSnapshot { get; }
    }
}
