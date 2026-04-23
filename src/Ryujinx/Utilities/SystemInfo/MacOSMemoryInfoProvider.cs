using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Ava.Utilities.SystemInfo
{
    [SupportedOSPlatform("macos")]
    partial class MacOSMemoryInfoProvider : Ryujinx.Common.Memory.IMemoryInfoProvider
    {
        private const int TASK_BASIC_INFO = 5;

        [StructLayout(LayoutKind.Sequential)]
        struct TaskBasicInfo
        {
            public int SuspendCount;
            public uint VirtualSize;
            public uint ResidentSize;
            public uint ResidentSizeMax;
            public uint UserTime;
            public uint SystemTime;
            public int Policy;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        struct VMStatistics64
        {
            public uint FreeCount;
            public uint ActiveCount;
            public uint InactiveCount;
            public uint WireCount;
            public ulong ZeroFillCount;
            public ulong Reactivations;
            public ulong Pageins;
            public ulong Pageouts;
            public ulong Faults;
            public ulong CowFaults;
            public ulong Lookups;
            public ulong Hits;
            public ulong Purges;
            public uint PurgeableCount;
            public uint SpeculativeCount;
            public ulong Decompressions;
            public ulong Compressions;
            public ulong Swapins;
            public ulong Swapouts;
            public uint CompressorPageCount;
            public uint ThrottledCount;
            public uint ExternalPageCount;
            public uint InternalPageCount;
            public ulong TotalUncompressedPagesInCompressor;
        }

        [LibraryImport("libSystem.dylib", SetLastError = true)]
        private static partial uint mach_task_self();

        [LibraryImport("libSystem.dylib", SetLastError = true)]
        private static partial int task_info(uint targetTask, int flavor, nint taskInfo, ref int taskInfoCount);

        [LibraryImport("libSystem.dylib", SetLastError = true)]
        private static partial uint mach_host_self();

        [LibraryImport("libSystem.dylib", SetLastError = true)]
        private static partial int host_page_size(uint host, ref uint out_page_size);

        [LibraryImport("libSystem.dylib", SetLastError = true)]
        private static partial int host_statistics64(uint hostPriv, int hostFlavor, ref VMStatistics64 hostInfo64Out, ref uint hostInfo64OutCnt);

        public MemorySnapshot GetSnapshot()
        {
            long rss = GetRssBytes();
            long swap = GetSwapBytes();
            long gcHeap = GC.GetTotalMemory(false);
            long unmanaged = GC.GetGCMemoryInfo().TotalCommittedBytes - gcHeap;

            return new MemorySnapshot(DateTime.UtcNow, rss, gcHeap, unmanaged, swap, MemoryPressureLevel.Normal);
        }

        private static long GetRssBytes()
        {
            try
            {
                var info = new TaskBasicInfo();
                int count = Marshal.SizeOf<TaskBasicInfo>() / sizeof(int);

                unsafe
                {
                    int result = task_info(mach_task_self(), TASK_BASIC_INFO, (nint)(&info), ref count);
                    if (result != 0)
                    {
                        Logger.Error?.Print(LogClass.Application, $"Failed to query RSS. task_info() error = {result}");
                        return 0;
                    }
                }

                return info.ResidentSize;
            }
            catch (Exception ex)
            {
                Logger.Error?.Print(LogClass.Application, $"Failed to query RSS: {ex.Message}");
                return 0;
            }
        }

        private static long GetSwapBytes()
        {
            try
            {
                uint port = mach_host_self();

                uint pageSize = 0;
                int result = host_page_size(port, ref pageSize);

                if (result != 0)
                {
                    Logger.Error?.Print(LogClass.Application, $"Failed to query Swap. host_page_size() error = {result}");
                    return 0;
                }

                const int Flavor = 4; // HOST_VM_INFO64
                uint count = (uint)(Marshal.SizeOf<VMStatistics64>() / sizeof(int));
                VMStatistics64 stats = new();
                result = host_statistics64(port, Flavor, ref stats, ref count);

                if (result != 0)
                {
                    Logger.Error?.Print(LogClass.Application, $"Failed to query Swap. host_statistics64() error = {result}");
                    return 0;
                }

                return (long)stats.CompressorPageCount * 16384L;
            }
            catch (Exception ex)
            {
                Logger.Error?.Print(LogClass.Application, $"Failed to query Swap: {ex.Message}");
                return 0;
            }
        }
    }
}
