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

            var options = AdapterInventoryService.BuildOptions(devices, interfaces);

            Assert.HasCount(1, options);
            Assert.AreEqual("dev-1", options[0].DeviceId);
            Assert.AreEqual("if-1", options[0].InterfaceId);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), options[0].GatewayIpAddress);
            Assert.IsTrue(options[0].IsPhysical);
        }


        [TestMethod]
        public void BuildOptions_PrefersExplicitInterfaceIdMapping()
        {
            var sharedMac = PhysicalAddress.Parse("001122334455");
            var devices = new[]
            {
                new AdapterDeviceSnapshot("dev-1", "Ethernet", null, sharedMac, preferredInterfaceId: "if-virtual")
            };

            var interfaces = new[]
            {
                new InterfaceSnapshot("if-physical", "Ethernet", sharedMac, NetworkInterfaceType.Ethernet, isPhysicalAdapter: true, new[] { IPAddress.Parse("192.168.1.20") }, new[] { IPAddress.Parse("192.168.1.1") }),
                new InterfaceSnapshot("if-virtual", "vEthernet", sharedMac, NetworkInterfaceType.Ethernet, isPhysicalAdapter: false, new[] { IPAddress.Parse("10.0.0.20") }, new[] { IPAddress.Parse("10.0.0.1") })
            };

            var options = AdapterInventoryService.BuildOptions(devices, interfaces);

            Assert.HasCount(1, options);
            Assert.AreEqual("if-virtual", options[0].InterfaceId);
            Assert.IsFalse(options[0].IsPhysical);
            Assert.AreEqual(IPAddress.Parse("10.0.0.1"), options[0].GatewayIpAddress);
        }



        [TestMethod]
        public void BuildOptions_FallsBackToMacIpMatch_WhenPreferredInterfaceIdMissing()
        {
            var mac = PhysicalAddress.Parse("001122334455");
            var devices = new[]
            {
                new AdapterDeviceSnapshot("dev-1", "Ethernet", IPAddress.Parse("192.168.1.20"), mac, preferredInterfaceId: "if-missing")
            };

            var interfaces = new[]
            {
                new InterfaceSnapshot("if-1", "Ethernet", mac, NetworkInterfaceType.Ethernet, isPhysicalAdapter: true, new[] { IPAddress.Parse("192.168.1.20") }, new[] { IPAddress.Parse("192.168.1.1") })
            };

            var options = AdapterInventoryService.BuildOptions(devices, interfaces);

            Assert.HasCount(1, options);
            Assert.AreEqual("if-1", options[0].InterfaceId);
            Assert.IsTrue(options[0].IsPhysical);
        }

        [TestMethod]
        public void BuildOptions_NoResolvedInterface_RemainsNonPhysical()
        {
            var devices = new[]
            {
                new AdapterDeviceSnapshot("dev-1", "Unmapped", IPAddress.Parse("10.10.10.10"), PhysicalAddress.Parse("AAAAAAAAAAAA"), preferredInterfaceId: "if-missing")
            };

            var interfaces = new[]
            {
                new InterfaceSnapshot("if-1", "Ethernet", PhysicalAddress.Parse("001122334455"), NetworkInterfaceType.Ethernet, isPhysicalAdapter: true, new[] { IPAddress.Parse("192.168.1.20") }, new[] { IPAddress.Parse("192.168.1.1") })
            };

            var options = AdapterInventoryService.BuildOptions(devices, interfaces);

            Assert.HasCount(1, options);
            Assert.IsNull(options[0].InterfaceId);
            Assert.IsFalse(options[0].IsPhysical);
            Assert.IsNull(options[0].GatewayIpAddress);
        }

        [TestMethod]
        public void BuildOptions_NoMatchingInterface_UsesNoIpv4DisplayAndNullGateway()
        {
            var devices = new[]
            {
                new AdapterDeviceSnapshot("dev-1", "Adapter A", null, null)
            };

            var options = AdapterInventoryService.BuildOptions(devices, interfaces: new InterfaceSnapshot[0]);

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

            var filtered = AdapterInventoryService.FilterAdapterOptions(options, includeVirtualAdapters: false);

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

            var filtered = AdapterInventoryService.FilterAdapterOptions(options, includeVirtualAdapters: true);

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

            var filtered = AdapterInventoryService.FilterAdapterOptions(options, includeVirtualAdapters: false);

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

            var result = AdapterInventoryService.IsInterfaceMatch(
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

            Assert.IsFalse(AdapterInventoryService.IsLikelyPhysicalAdapter(NetworkInterfaceType.Ethernet, zeroMac));
            Assert.IsFalse(AdapterInventoryService.IsLikelyPhysicalAdapter(NetworkInterfaceType.Loopback, validMac));
            Assert.IsTrue(AdapterInventoryService.IsLikelyPhysicalAdapter(NetworkInterfaceType.Ethernet, validMac));
        }

        [TestMethod]
        public void IsLikelyPhysicalPnpDeviceId_RecognizesPhysicalBusPrefixes()
        {
            Assert.IsTrue(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"PCI\VEN_8086&DEV_15F3"));
            Assert.IsTrue(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"USB\VID_0BDA&PID_8153"));
            Assert.IsTrue(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"ACPI\PNP0A08\0"));
            Assert.IsTrue(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"pci\ven_10ec&dev_8168"));
            Assert.IsFalse(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(@"ROOT\VMS_MP"));
            Assert.IsFalse(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(string.Empty));
            Assert.IsFalse(AdapterPhysicalClassifier.IsLikelyPhysicalPnpDeviceId(null));
        }
    }
}
