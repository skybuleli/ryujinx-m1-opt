using Ryujinx.Common.Logging;
using System;
using System.Timers;

namespace Ryujinx.Common.Memory
{
    public class MemoryBudgetManager : IMemoryTracker, IDisposable
    {
        private readonly IMemoryInfoProvider _provider;
        private IMemoryPressureHandler _pressureHandler;
        private readonly System.Timers.Timer _timer;
        private readonly object _lock = new();
        private MemoryPressureLevel _currentLevel = MemoryPressureLevel.Normal;

        public MemorySnapshot LastSnapshot { get; private set; }
        public event EventHandler<MemoryPressureEventArgs> PressureChanged;

        public const long SoftLimitBytes = 3_500_000_000L;
        public const long HardLimitBytes = 4_000_000_000L;
        public const long OomLimitBytes = 4_500_000_000L;

        public MemoryBudgetManager(IMemoryInfoProvider provider, TimeSpan? sampleInterval = null)
            : this(provider, null, sampleInterval)
        {
        }

        public MemoryBudgetManager(IMemoryInfoProvider provider, IMemoryPressureHandler pressureHandler, TimeSpan? sampleInterval = null)
        {
            _provider = provider;
            _pressureHandler = pressureHandler;
            _timer = new Timer(sampleInterval?.TotalMilliseconds ?? 1000.0)
            {
                AutoReset = true,
            };
            _timer.Elapsed += OnTimerElapsed;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            MemorySnapshot snapshot = _provider.GetSnapshot();

            lock (_lock)
            {
                LastSnapshot = snapshot;

                MemoryPressureLevel newLevel = EvaluatePressureLevel(snapshot.RssBytes);

                if (newLevel != _currentLevel)
                {
                    var previousLevel = _currentLevel;
                    _currentLevel = newLevel;
                    PressureChanged?.Invoke(this, new MemoryPressureEventArgs(snapshot, previousLevel));
                    OnPressureChanged(snapshot, newLevel);
                }
            }

            Logger.Info?.Print(LogClass.Emulation, "Memory snapshot", snapshot);
        }

        private static MemoryPressureLevel EvaluatePressureLevel(long rssBytes)
        {
            if (rssBytes > OomLimitBytes)
            {
                return MemoryPressureLevel.Oom;
            }

            if (rssBytes > HardLimitBytes)
            {
                return MemoryPressureLevel.Critical;
            }

            if (rssBytes > SoftLimitBytes)
            {
                return MemoryPressureLevel.Warning;
            }

            return MemoryPressureLevel.Normal;
        }

        private void OnPressureChanged(MemorySnapshot snapshot, MemoryPressureLevel currentLevel)
        {
            switch (currentLevel)
            {
                case MemoryPressureLevel.Warning:
                    Logger.Warning?.Print(LogClass.Emulation, $"Memory soft limit exceeded: {snapshot.RssBytes / 1024 / 1024} MB");
                    GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
                    break;
                case MemoryPressureLevel.Critical:
                    Logger.Error?.Print(LogClass.Emulation, $"Memory hard limit exceeded: {snapshot.RssBytes / 1024 / 1024} MB — evicting caches");
                    _pressureHandler?.OnHardLimitExceeded();
                    break;
                case MemoryPressureLevel.Oom:
                    Logger.Error?.Print(LogClass.Emulation, $"CRITICAL: Memory OOM limit exceeded: {snapshot.RssBytes / 1024 / 1024} MB — emergency flush");
                    _pressureHandler?.OnOomLimitExceeded();
                    break;
            }
        }

        public void SetPressureHandler(IMemoryPressureHandler pressureHandler)
        {
            _pressureHandler = pressureHandler;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
