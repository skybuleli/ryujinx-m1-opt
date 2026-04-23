namespace Ryujinx.Common.Memory
{
    public interface IMemoryPressureHandler
    {
        void OnHardLimitExceeded();
        void OnOomLimitExceeded();
    }
}
