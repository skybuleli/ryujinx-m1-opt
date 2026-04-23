using System;

namespace Ryujinx.Common.Memory
{
    public class MemoryPressureEventArgs : EventArgs
    {
        public MemorySnapshot Snapshot { get; }
        public MemoryPressureLevel PreviousLevel { get; }

        public MemoryPressureEventArgs(MemorySnapshot snapshot, MemoryPressureLevel previousLevel)
        {
            Snapshot = snapshot;
            PreviousLevel = previousLevel;
        }
    }
}
