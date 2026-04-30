using NUnit.Framework;
using Ryujinx.Common.Memory;
using System;
using System.Threading;

namespace Ryujinx.Tests.Memory
{
    [TestFixture]
    internal class MemoryBudgetManagerThresholdTests
    {
        private class FakeMemoryInfoProvider : IMemoryInfoProvider
        {
            public MemorySnapshot Snapshot { get; set; }

            public MemorySnapshot GetSnapshot() => Snapshot;
        }

        private class MockMemoryPressureHandler : IMemoryPressureHandler
        {
            public int HardLimitCallCount { get; private set; }
            public int OomLimitCallCount { get; private set; }

            public void OnHardLimitExceeded()
            {
                HardLimitCallCount++;
            }

            public void OnOomLimitExceeded()
            {
                OomLimitCallCount++;
            }

            public void Reset()
            {
                HardLimitCallCount = 0;
                OomLimitCallCount = 0;
            }
        }

        [Test]
        public void SoftLimit_Triggers_GC_Optimized()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 1_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            var handler = new MockMemoryPressureHandler();
            using var manager = new MemoryBudgetManager(provider, handler, TimeSpan.FromMilliseconds(50));
            manager.Start();

            // Wait for first tick at Normal level
            Thread.Sleep(150);

            // Cross into Warning (3.6 GB)
            int gcCountBefore = GC.CollectionCount(2);
            provider.Snapshot = new MemorySnapshot(DateTime.UtcNow, 3_600_000_000L, 0, 0, 0, MemoryPressureLevel.Normal);
            Thread.Sleep(150);

            manager.Stop();

            // GC.Collect(2, Optimized) should have been called, incrementing collection count
            int gcCountAfter = GC.CollectionCount(2);
            Assert.That(gcCountAfter, Is.GreaterThanOrEqualTo(gcCountBefore), "GC.CollectionCount(2) should increase after soft limit trigger.");

            // Handler should NOT be called for soft limit
            Assert.That(handler.HardLimitCallCount, Is.EqualTo(0), "Hard limit handler should not be called for soft limit.");
            Assert.That(handler.OomLimitCallCount, Is.EqualTo(0), "OOM handler should not be called for soft limit.");
        }

        [Test]
        public void HardLimit_Triggers_CacheEviction()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 1_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            var handler = new MockMemoryPressureHandler();
            using var manager = new MemoryBudgetManager(provider, handler, TimeSpan.FromMilliseconds(50));
            manager.Start();

            // Wait for first tick at Normal level
            Thread.Sleep(150);

            // Cross into Critical (4.1 GB)
            provider.Snapshot = new MemorySnapshot(DateTime.UtcNow, 4_100_000_000L, 0, 0, 0, MemoryPressureLevel.Normal);
            Thread.Sleep(150);

            manager.Stop();

            Assert.That(handler.HardLimitCallCount, Is.EqualTo(1), "OnHardLimitExceeded should be called exactly once when crossing into Critical.");
            Assert.That(handler.OomLimitCallCount, Is.EqualTo(0), "OnOomLimitExceeded should not be called for hard limit.");
        }

        [Test]
        public void OomLimit_Triggers_EmergencyFlush()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 1_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            var handler = new MockMemoryPressureHandler();
            using var manager = new MemoryBudgetManager(provider, handler, TimeSpan.FromMilliseconds(50));
            manager.Start();

            // Wait for first tick at Normal level
            Thread.Sleep(150);

            // Cross into OOM (4.6 GB)
            provider.Snapshot = new MemorySnapshot(DateTime.UtcNow, 4_600_000_000L, 0, 0, 0, MemoryPressureLevel.Normal);
            Thread.Sleep(150);

            manager.Stop();

            // OOM calls both hard limit and OOM handler
            Assert.That(handler.HardLimitCallCount, Is.EqualTo(1), "OnHardLimitExceeded should be called as part of OOM handling.");
            Assert.That(handler.OomLimitCallCount, Is.EqualTo(1), "OnOomLimitExceeded should be called exactly once when crossing into OOM.");
        }

        [Test]
        public void NormalLevel_DoesNotTriggerActions()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 2_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            var handler = new MockMemoryPressureHandler();
            using var manager = new MemoryBudgetManager(provider, handler, TimeSpan.FromMilliseconds(50));
            manager.Start();

            // Wait for multiple ticks at Normal level
            Thread.Sleep(300);

            manager.Stop();

            Assert.That(handler.HardLimitCallCount, Is.EqualTo(0), "Hard limit handler should not be called at normal level.");
            Assert.That(handler.OomLimitCallCount, Is.EqualTo(0), "OOM handler should not be called at normal level.");
        }
    }
}
