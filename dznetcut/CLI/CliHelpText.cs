namespace dznetcut.CLI
{
    internal static class CliHelpText
    {
        public static string Build()
            => @"dznetcut CLI
Usage:
  dznetcut [--gui]
  dznetcut <command> [options]

Commands:
  help              Show CLI help
  list-adapters     List capture adapters
  scan              Discover hosts
  cut               Start spoofing targets
  stop              Stop spoofing

Global options:
  --help, -h        Show help
  --gui             Force GUI mode
  --json            Output JSON when supported
  --verbose, -v     Verbose logging

cut option:
  --no-arp-protection, -nap   Disable ARP protection (enabled by default)

Open this help from GUI: Help > Command line parameters";

// Command examples:
//   dznetcut scan --adapter "Ethernet" --gateway-ip 192.168.1.1 --duration 25
//   dznetcut cut --adapter "Ethernet" --gateway-ip 192.168.1.1 --gateway-mac AA-BB-CC-DD-EE-FF --target 192.168.1.42@11-22-33-44-55-66 --duration 30
    }
}
