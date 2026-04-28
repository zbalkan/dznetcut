using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using dznetcut.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace dznetcut.Tests
{
    [TestClass]
    public class EvidenceStoreTests
    {
        [TestMethod]
        public void AddEvidence_DuplicateFingerprint_RefreshesLastSeen()
        {
            var store = new EvidenceStore();
            var sourceIp = IPAddress.Parse("192.168.0.20");
            var gatewayIp = IPAddress.Parse("192.168.0.1");
            var sourceMac = PhysicalAddress.Parse("001122334455");

            var firstSeen = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var secondSeen = firstSeen.AddSeconds(10);

            var firstRecord = new EvidenceRecord(firstSeen, DiscoveryMethod.ArpPassive, sourceIp, gatewayIp, sourceMac, "Passive ARP", 20);
            var duplicateRecord = new EvidenceRecord(secondSeen, DiscoveryMethod.ArpPassive, sourceIp, gatewayIp, sourceMac, "Passive ARP", 20);

            Assert.IsTrue(store.AddEvidence(firstRecord, gatewayIp));
            Assert.IsFalse(store.AddEvidence(duplicateRecord, gatewayIp));

            var host = store.Snapshot().Single(entry => entry.IPv4Address!.Equals(sourceIp));
            Assert.AreEqual(secondSeen, host.LastSeenUtc);
        }
    }
}