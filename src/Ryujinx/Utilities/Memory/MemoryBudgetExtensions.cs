using Ryujinx.Common.Memory;
using Ryujinx.Memory;

namespace Ryujinx.Ava.Utilities.Memory
{
    public static class MemoryBudgetExtensions
    {
        /// <summary>
        /// Tracks native memory changes via MemoryBlock events.
        /// </summary>
        public static void TrackNativeMemory(this MemoryBudgetManager manager)
        {
            MemoryBlock.NativeMemoryCommitted += (s, size) =>
            {
                // Native memory tracking could be extended here
                // Currently just ensures events are wired for monitoring
            };

            MemoryBlock.NativeMemoryDecommitted += (s, size) =>
            {
                // Native memory tracking could be extended here
            };
        }
    }
}
