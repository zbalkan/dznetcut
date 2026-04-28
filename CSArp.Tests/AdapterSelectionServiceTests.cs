using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using CSArp.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSArp.Tests
{
    [TestClass]
    public class AdapterSelectionServiceTests
    {
        [TestMethod]
        public void BuildOptions_MatchesInterfaceAndGatewayFromMac()
        {
            var deviceMac = PhysicalAddress.Parse("001122334455");
            var devices = new[]
            {
                new AdapterDeviceSnapshot("dev-1", "Ethernet", IPAddress.Parse("192.168.1.20"), deviceMac)
            };
            var interfaces = new[]
            {
                new InterfaceSnapshot(
                    "if-1",
                    "Ethernet",
                    deviceMac,
                    NetworkInterfaceType.Ethernet,
                    isPhysicalAdapter: true,
                    new[] { IPAddress.Parse("192.168.1.20") },
                    new[] { IPAddress.Parse("192.168.1.1") })
            };

            var options = AdapterSelectionService.BuildOptions(devices, interfaces);

            Assert.AreEqual(1, options.Count);
            Assert.AreEqual("dev-1", options[0].DeviceId);
            Assert.AreEqual("if-1", options[0].InterfaceId);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), options[0].GatewayIpAddress);
            Assert.IsTrue(options[0].IsPhysical);
        }

        [TestMethod]
        public void FilterOptions_DefaultsToPhysicalOnly()
        {
            var options = new[]
            {
                new AdapterSelectionOptionModel("phy", "Ethernet [192.168.1.20]", "if-1", IPAddress.Parse("192.168.1.1"), true),
                new AdapterSelectionOptionModel("virt", "Hyper-V [10.0.0.4]", "if-2", IPAddress.Parse("10.0.0.1"), false)
            };

            var filtered = AdapterSelectionService.FilterOptions(options, includeVirtualAdapters: false);

            Assert.AreEqual(1, filtered.Count);
            Assert.AreEqual("phy", filtered[0].DeviceId);
        }

        [TestMethod]
        public void FilterOptions_WithoutPhysicalAdapters_ReturnsEmptyByDefault()
        {
            var options = new[]
            {
                new AdapterSelectionOptionModel("virt-a", "Virtual-A [10.0.0.2]", "if-1", IPAddress.Parse("10.0.0.1"), false),
                new AdapterSelectionOptionModel("virt-b", "Virtual-B [10.0.0.3]", "if-2", IPAddress.Parse("10.0.0.1"), false)
            };

            var filtered = AdapterSelectionService.FilterOptions(options, includeVirtualAdapters: false);

            Assert.AreEqual(0, filtered.Count);
        }

        [TestMethod]
        public void FilterOptions_SelectAllAdaptersIncludesVirtual()
        {
            var options = new[]
            {
                new AdapterSelectionOptionModel("phy", "Ethernet [192.168.1.20]", "if-1", IPAddress.Parse("192.168.1.1"), true),
                new AdapterSelectionOptionModel("virt", "Hyper-V [10.0.0.4]", "if-2", IPAddress.Parse("10.0.0.1"), false)
            };

            var filtered = AdapterSelectionService.FilterOptions(options, includeVirtualAdapters: true);

            Assert.AreEqual(2, filtered.Count);
        }

        [TestMethod]
        public void IsLikelyPhysicalPnpDeviceId_RecognizesPhysicalBusPrefixes()
        {
            Assert.IsTrue(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"PCI\VEN_8086&DEV_15F3"));
            Assert.IsTrue(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"USB\VID_0BDA&PID_8153"));
            Assert.IsFalse(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"ROOT\VMS_MP"));
            Assert.IsFalse(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(null));
        }
    }
}
