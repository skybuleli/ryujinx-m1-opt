using Ryujinx.Common.Memory;

namespace Ryujinx.Ava.Utilities.SystemInfo
{
    public interface IMemoryInfoProvider
    {
        MemorySnapshot GetSnapshot();
    }
}
