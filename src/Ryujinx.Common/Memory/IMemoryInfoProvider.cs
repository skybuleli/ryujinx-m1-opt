using Ryujinx.Common.Memory;

namespace Ryujinx.Common.Memory
{
    public interface IMemoryInfoProvider
    {
        MemorySnapshot GetSnapshot();
    }
}
