using Ryujinx.Common.Memory;
using Ryujinx.Graphics.Gpu;
using System;

namespace Ryujinx.Ava.Utilities.Memory
{
    public class DefaultMemoryPressureHandler : IMemoryPressureHandler
    {
        private readonly GpuContext _gpuContext;

        public DefaultMemoryPressureHandler(GpuContext gpuContext)
        {
            _gpuContext = gpuContext;
        }

        public void OnHardLimitExceeded()
        {
            foreach (var physicalMemory in _gpuContext.PhysicalMemoryRegistry.Values)
            {
                physicalMemory.TextureCache?.Clear();
                physicalMemory.ShaderCache?.Clear();
            }
        }

        public void OnOomLimitExceeded()
        {
            OnHardLimitExceeded();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
        }
    }
}
