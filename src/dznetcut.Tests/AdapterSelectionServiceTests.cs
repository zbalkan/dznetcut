using System.Net;
using System.Net.NetworkInformation;
using dznetcut.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace dznetcut.Tests
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

            Assert.HasCount(1, options);
            Assert.AreEqual("dev-1", options[0].DeviceId);
            Assert.AreEqual("if-1", options[0].InterfaceId);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), options[0].GatewayIpAddress);
            Assert.IsTrue(options[0].IsPhysical);
        }

        [TestMethod]
        public void BuildOptions_NoMatchingInterface_UsesNoIpv4DisplayAndNullGateway()
        {
            var devices = new[]
            {
                new AdapterDeviceSnapshot("dev-1", "Adapter A", null, null)
            };

            var options = AdapterSelectionService.BuildOptions(devices, interfaces: new InterfaceSnapshot[0]);

            Assert.HasCount(1, options);
            Assert.AreEqual("Adapter A [No IPv4]", options[0].DisplayText);
            Assert.IsNull(options[0].InterfaceId);
            Assert.IsNull(options[0].GatewayIpAddress);
            Assert.IsFalse(options[0].IsPhysical);
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

            Assert.HasCount(1, filtered);
            Assert.AreEqual("phy", filtered[0].DeviceId);
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

            Assert.HasCount(2, filtered);
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

            Assert.IsEmpty(filtered);
        }

        [TestMethod]
        public void IsInterfaceMatch_FallsBackToIpMatch_WhenMacDoesNotMatch()
        {
            var networkInterface = new InterfaceSnapshot(
                "if-1",
                "Ethernet",
                PhysicalAddress.Parse("AABBCCDDEEFF"),
                NetworkInterfaceType.Ethernet,
                isPhysicalAdapter: true,
                new[] { IPAddress.Parse("192.168.1.20") },
                new[] { IPAddress.Parse("192.168.1.1") });

            var result = AdapterSelectionService.IsInterfaceMatch(
                networkInterface,
                IPAddress.Parse("192.168.1.20"),
                PhysicalAddress.Parse("001122334455"));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsLikelyPhysicalAdapter_RejectsZeroMacAndLoopback()
        {
            var zeroMac = PhysicalAddress.Parse("000000000000");
            var validMac = PhysicalAddress.Parse("001122334455");

            Assert.IsFalse(AdapterSelectionService.IsLikelyPhysicalAdapter(NetworkInterfaceType.Ethernet, zeroMac));
            Assert.IsFalse(AdapterSelectionService.IsLikelyPhysicalAdapter(NetworkInterfaceType.Loopback, validMac));
            Assert.IsTrue(AdapterSelectionService.IsLikelyPhysicalAdapter(NetworkInterfaceType.Ethernet, validMac));
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