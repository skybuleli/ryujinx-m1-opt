using NUnit.Framework;
using Ryujinx.Common.Memory;
using System;
using System.Threading;

namespace Ryujinx.Tests.Memory
{
    [TestFixture]
    internal class MemoryBudgetManagerTests
    {
        private class FakeMemoryInfoProvider : IMemoryInfoProvider
        {
            public MemorySnapshot Snapshot { get; set; }

            public MemorySnapshot GetSnapshot() => Snapshot;
        }

        [Test]
        public void PressureChanged_FiresWhenCrossingThreshold()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 1_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            using var manager = new MemoryBudgetManager(provider, TimeSpan.FromMilliseconds(50));
            var eventFired = false;
            MemoryPressureEventArgs capturedArgs = null;

            manager.PressureChanged += (sender, e) =>
            {
                eventFired = true;
                capturedArgs = e;
            };

            manager.Start();

            // Wait for first tick at Normal level
            Thread.Sleep(150);

            // Cross into Warning (3.6 GB)
            provider.Snapshot = new MemorySnapshot(DateTime.UtcNow, 3_600_000_000L, 0, 0, 0, MemoryPressureLevel.Normal);
            Thread.Sleep(150);

            manager.Stop();

            Assert.That(eventFired, Is.True, "PressureChanged should fire when crossing from Normal to Warning.");
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs.PreviousLevel, Is.EqualTo(MemoryPressureLevel.Normal));
            Assert.That(capturedArgs.Snapshot.RssBytes, Is.EqualTo(3_600_000_000L));
        }

        [Test]
        public void LastSnapshot_IsUpdatedAfterTimerTick()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 2_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            using var manager = new MemoryBudgetManager(provider, TimeSpan.FromMilliseconds(50));
            manager.Start();

            Thread.Sleep(150);

            Assert.That(manager.LastSnapshot.RssBytes, Is.EqualTo(2_000_000_000L), "LastSnapshot should be updated after timer tick.");

            manager.Stop();
        }

        [Test]
        public void Dispose_StopsTimerAndNoFurtherEventsFire()
        {
            var provider = new FakeMemoryInfoProvider
            {
                Snapshot = new MemorySnapshot(DateTime.UtcNow, 1_000_000_000L, 0, 0, 0, MemoryPressureLevel.Normal),
            };

            var manager = new MemoryBudgetManager(provider, TimeSpan.FromMilliseconds(50));
            var eventCount = 0;

            manager.PressureChanged += (sender, e) =>
            {
                eventCount++;
            };

            manager.Start();
            Thread.Sleep(150);
            manager.Dispose();

            var countAfterDispose = eventCount;
            Thread.Sleep(150);

            Assert.That(eventCount, Is.EqualTo(countAfterDispose), "No further events should fire after Dispose.");
        }
    }
}
