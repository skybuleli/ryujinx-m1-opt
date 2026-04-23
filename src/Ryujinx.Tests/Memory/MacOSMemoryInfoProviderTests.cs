using NUnit.Framework;
using Ryujinx.Ava.Utilities.SystemInfo;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Tests.Memory
{
    [TestFixture]
    [SupportedOSPlatform("macos")]
    internal class MacOSMemoryInfoProviderTests
    {
        [Test]
        [Platform("MacOS")]
        public void GetSnapshot_ReturnsNonZeroRss()
        {
            var provider = new MacOSMemoryInfoProvider();
            var snapshot = provider.GetSnapshot();

            Assert.That(snapshot.RssBytes, Is.GreaterThan(0), "RSS should be greater than 0 for the current process.");
        }

        [Test]
        [Platform("MacOS")]
        public void GetSnapshot_TimestampIsRecent()
        {
            var provider = new MacOSMemoryInfoProvider();
            var before = DateTime.UtcNow.AddSeconds(-1);
            var snapshot = provider.GetSnapshot();
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.That(snapshot.Timestamp, Is.InRange(before, after), "Timestamp should be within 1 second of UtcNow.");
        }
    }
}
