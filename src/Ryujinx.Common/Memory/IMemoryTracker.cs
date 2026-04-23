using System;

namespace Ryujinx.Common.Memory
{
    public interface IMemoryTracker
    {
        event EventHandler<MemoryPressureEventArgs> PressureChanged;
        MemorySnapshot LastSnapshot { get; }
    }
}
