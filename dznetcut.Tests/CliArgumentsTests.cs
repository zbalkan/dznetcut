using dznetcut.CLI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace dznetcut.Tests
{
    [TestClass]
    public class CliArgumentsTests
    {
        [TestMethod]
        public void Parse_NoArgs_LaunchesGui()
        {
            var args = CliArguments.Parse(System.Array.Empty<string>());

            Assert.IsTrue(args.LaunchGui);
            Assert.IsFalse(args.ShowHelp);
            Assert.IsNull(args.Command);
        }

        [TestMethod]
        public void Parse_ScanCommand_ReadsOptionsAndPositionals()
        {
            var args = CliArguments.Parse(new[] { "scan", "--adapter", "Ethernet", "--gateway-ip=192.168.1.1", "--duration", "20" });

            Assert.AreEqual("scan", args.Command);
            Assert.IsTrue(args.TryGetOption("adapter", out var adapter));
            Assert.AreEqual("Ethernet", adapter);
            Assert.IsTrue(args.TryGetOption("gateway-ip", out var gatewayIp));
            Assert.AreEqual("192.168.1.1", gatewayIp);
            Assert.IsTrue(args.TryGetOption("duration", out var duration));
            Assert.AreEqual("20", duration);
        }

        [TestMethod]
        public void Parse_VerboseFlags_AreRecognized()
        {
            var longForm = CliArguments.Parse(new[] { "scan", "--verbose" });
            var shortForm = CliArguments.Parse(new[] { "scan", "-v" });

            Assert.IsTrue(longForm.Options.ContainsKey("verbose"));
            Assert.IsTrue(shortForm.Options.ContainsKey("verbose"));
        }

        [TestMethod]
        public void Parse_NoArpProtectionFlags_AreRecognized()
        {
            var longForm = CliArguments.Parse(new[] { "cut", "--no-arp-protection" });
            var shortForm = CliArguments.Parse(new[] { "cut", "-nap" });

            Assert.IsTrue(longForm.Options.ContainsKey("no-arp-protection"));
            Assert.IsTrue(shortForm.Options.ContainsKey("no-arp-protection"));
        }

        [TestMethod]
        public void Parse_UnknownShortFlag_ReturnsParseError()
        {
            var args = CliArguments.Parse(new[] { "scan", "-oops" });

            Assert.AreEqual("Unknown option: -oops", args.ParseError);
            Assert.IsNull(args.Command);
            Assert.IsFalse(args.ShowHelp);
        }
    }
}
