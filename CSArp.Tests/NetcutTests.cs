using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using CSArp.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSArp.Tests
{
    [TestClass]
    public class NetcutTests
    {
        private string _originalDirectory = string.Empty;
        private string _tempDirectory = string.Empty;

        [TestInitialize]
        public void TestInitialize()
        {
            _originalDirectory = Environment.CurrentDirectory;
            _tempDirectory = Path.Combine(Path.GetTempPath(), "csarp-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            Environment.CurrentDirectory = _tempDirectory;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.CurrentDirectory = _originalDirectory;
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [TestMethod]
        public void ClientDiscoveredEventArgs_StoresValues()
        {
            var ip = IPAddress.Parse("192.168.10.15");
            var mac = PhysicalAddress.Parse("001122334455");

            var args = new ClientDiscoveredEventArgs(ip, mac, isGateway: true, confidenceScore: 88);

            Assert.AreEqual(ip, args.IpAddress);
            Assert.AreEqual(mac, args.MacAddress);
            Assert.IsTrue(args.IsGateway);
            Assert.AreEqual(88, args.ConfidenceScore);
        }

        [TestMethod]
        public void NetworkScanner_DefaultState_IsNotScanning()
        {
            var scanner = new NetworkScanner();

            Assert.IsFalse(scanner.IsScanning);
        }

        [TestMethod]
        public void Spoofer_DefaultState_IsNotSpoofing()
        {
            var spoofer = new Spoofer();

            Assert.IsFalse(spoofer.IsSpoofing);
        }

        [TestMethod]
        public void PhysicalAddressExtensions_ParseAndFormat_RoundTrip()
        {
            var parsed = "AA-BB-CC-DD-EE-FF".Parse();

            Assert.AreEqual("AA-BB-CC-DD-EE-FF", parsed.ToString("-"));
        }

        [TestMethod]
        public void ScanPolicyConfig_PresetsExposeExpectedFlags()
        {
            Assert.IsTrue(ScanPolicyConfig.Conservative.UdpDiscoveryEnabled);
            Assert.IsTrue(ScanPolicyConfig.Balanced.UdpDiscoveryEnabled);
            Assert.IsFalse(ScanPolicyConfig.Balanced.TcpSynEnabled);
            Assert.IsTrue(ScanPolicyConfig.Aggressive.TcpSynEnabled);
        }
    }
}
