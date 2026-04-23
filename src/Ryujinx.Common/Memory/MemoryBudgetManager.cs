using Ryujinx.Common.Logging;
using System;
using System.Timers;

namespace Ryujinx.Common.Memory
{
    public class MemoryBudgetManager : IMemoryTracker, IDisposable
    {
        private readonly IMemoryInfoProvider _provider;
        private readonly System.Timers.Timer _timer;
        private readonly object _lock = new();
        private MemoryPressureLevel _currentLevel = MemoryPressureLevel.Normal;

        public MemorySnapshot LastSnapshot { get; private set; }
        public event EventHandler<MemoryPressureEventArgs> PressureChanged;

        private const long SoftLimitBytes = 3_500_000_000L;
        private const long HardLimitBytes = 4_000_000_000L;
        private const long OomLimitBytes = 4_500_000_000L;

        public MemoryBudgetManager(IMemoryInfoProvider provider, TimeSpan? sampleInterval = null)
        {
            _provider = provider;
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
                    PressureChanged?.Invoke(this, new MemoryPressureEventArgs(snapshot, _currentLevel));
                    _currentLevel = newLevel;
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
