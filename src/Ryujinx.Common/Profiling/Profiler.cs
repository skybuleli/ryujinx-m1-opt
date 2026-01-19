using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Common.Profiling
{
    public static class Profiler
    {
        // 只有定义了 USE_TRACY 宏时才启用
#if USE_TRACY
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (!_initialized)
            {
                // 初始化 Tracy 逻辑 (如果是 Tracy-CSharp，通常在第一次调用时自动初始化)
                _initialized = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TracyZone Zone([CallerMemberName] string name = "")
        {
            return new TracyZone(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Plot(string name, float value)
        {
            Tracy.TracyC.EmitPlot(name, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FrameMark()
        {
            Tracy.TracyC.EmitFrameMark(null);
        }
#else
        public static void Initialize() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EmptyDisposable Zone([CallerMemberName] string name = "") => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Plot(string name, float value) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FrameMark() { }
#endif
    }

#if USE_TRACY
    public readonly struct TracyZone : IDisposable
    {
        private readonly Tracy.TracyC.ZoneScope _scope;

        public TracyZone(string name)
        {
            _scope = Tracy.TracyC.EmitZoneBegin(name, true);
        }

        public void Dispose()
        {
            Tracy.TracyC.EmitZoneEnd(_scope);
        }
    }
#else
    public readonly struct EmptyDisposable : IDisposable
    {
        public void Dispose() { }
    }
#endif
}
