using dznetcut.CLI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace dznetcut.Tests
{
    [TestClass]
    public class CliArgumentsTests
    {
        [TestMethod]
        [DataRow("--gui")]
        [DataRow("--GUI")]
        public void Parse_GuiFlag_LaunchesGui(string flag)
        {
            var args = CliArguments.Parse(new[] { flag });

            Assert.IsTrue(args.LaunchGui);
            Assert.IsFalse(args.ShowHelp);
            Assert.IsNull(args.Command);
        }

        [TestMethod]
        [DataRow("--help")]
        [DataRow("-h")]
        public void Parse_HelpFlags_ShowHelp(string flag)
        {
            var args = CliArguments.Parse(new[] { flag });

            Assert.IsTrue(args.ShowHelp);
            Assert.IsFalse(args.LaunchGui);
        }

        [TestMethod]
        public void Parse_NoArgs_LaunchesGui()
        {
            var args = CliArguments.Parse(System.Array.Empty<string>());

            Assert.IsTrue(args.LaunchGui);
            Assert.IsFalse(args.ShowHelp);
            Assert.IsNull(args.Command);
        }

        [TestMethod]
        [DataRow("--no-arp-protection")]
        [DataRow("-nap")]
        public void Parse_NoArpProtectionFlags_AreRecognized(string flag)
        {
            var args = CliArguments.Parse(new[] { "cut", flag });

            Assert.IsTrue(args.Options.ContainsKey("no-arp-protection"));
        }

        [TestMethod]
        public void Parse_OptionNames_AreCaseInsensitive()
        {
            var args = CliArguments.Parse(new[] { "scan", "--Adapter", "Ethernet" });

            Assert.IsTrue(args.TryGetOption("adapter", out var lower));
            Assert.IsTrue(args.TryGetOption("ADAPTER", out var upper));
            Assert.AreEqual("Ethernet", lower);
            Assert.AreEqual("Ethernet", upper);
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
        public void Parse_UnknownShortFlag_ReturnsParseError()
        {
            var args = CliArguments.Parse(new[] { "scan", "-oops" });

            Assert.AreEqual("Unknown option: -oops", args.ParseError);
            Assert.IsNull(args.Command);
            Assert.IsFalse(args.ShowHelp);
        }

        [TestMethod]
        [DataRow("--verbose")]
        [DataRow("-v")]
        public void Parse_VerboseFlags_AreRecognized(string flag)
        {
            var args = CliArguments.Parse(new[] { "scan", flag });

            Assert.IsTrue(args.Options.ContainsKey("verbose"));
        }

        [TestMethod]
        public void Parse_OptionsWithoutCommand_DefaultsToHelp()
        {
            var args = CliArguments.Parse(new[] { "--verbose" });

            Assert.IsTrue(args.ShowHelp);
            Assert.IsFalse(args.LaunchGui);
            Assert.IsNull(args.Command);
            Assert.IsTrue(args.Options.ContainsKey("verbose"));
        }
    }
}