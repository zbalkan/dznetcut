using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using dznetcut.Logic;
using SharpPcap.LibPcap;

namespace dznetcut.CLI
{
    internal sealed class CliCommandRouter
    {
        private readonly Action<string> _writeLine;

        public CliCommandRouter(Action<string> writeLine)
        {
            _writeLine = writeLine;
        }

        public int Execute(CliArguments arguments)
        {
            if (arguments.ShowHelp)
            {
                _writeLine(CliHelpText.Build());
                return 0;
            }

            if (arguments.Command == null)
            {
                _writeLine(CliHelpText.Build());
                return 2;
            }

            try
            {
                switch (arguments.Command.ToLowerInvariant())
                {
                    case "list-adapters":
                        return ListAdapters(arguments);
                    case "scan":
                        return Scan(arguments);
                    case "cut":
                        return Cut(arguments);
                    case "stop":
                        _writeLine("stop command is only supported for future background sessions.");
                        return 0;
                    default:
                        _writeLine($"Unknown command: {arguments.Command}");
                        _writeLine(CliHelpText.Build());
                        return 2;
                }
            }
            catch (Exception ex)
            {
                _writeLine($"Command failed: {ex.Message}");
                return 4;
            }
        }

        private int Scan(CliArguments arguments)
        {
            var adapter = ResolveAdapter(arguments);
            if (adapter == null)
            {
                return 2;
            }

            if (!TryGetIpOption(arguments, "gateway-ip", out var gatewayIp))
            {
                _writeLine("scan requires --gateway-ip <ipv4>");
                return 2;
            }

            var durationSeconds = TryGetIntOption(arguments, "duration", 30);
            var scanner = new NetworkScanner(_writeLine);
            var hosts = new Dictionary<string, ClientDiscoveredEventArgs>(StringComparer.Ordinal);

            scanner.StartScan(
                adapter,
                gatewayIp,
                new Progress<ClientDiscoveredEventArgs>(host => hosts[host.IpAddress.ToString()] = host),
                new Progress<string>(_ => { }),
                new Progress<int>(_ => { }));

            if (!scanner.WaitForStop(TimeSpan.FromSeconds(durationSeconds + 5)))
            {
                scanner.StopScan();
            }

            foreach (var host in hosts.Values.OrderBy(h => h.IpAddress.ToString(), StringComparer.Ordinal))
            {
                _writeLine($"{host.IpAddress} | {host.MacAddress.ToString("-")} | confidence={host.ConfidenceScore} | gateway={host.IsGateway}");
            }

            return 0;
        }

        private int Cut(CliArguments arguments)
        {
            var adapter = ResolveAdapter(arguments);
            if (adapter == null)
            {
                return 2;
            }

            if (!TryGetIpOption(arguments, "gateway-ip", out var gatewayIp))
            {
                _writeLine("cut requires --gateway-ip <ipv4>");
                return 2;
            }

            if (!arguments.TryGetOption("gateway-mac", out var gatewayMacText) || string.IsNullOrWhiteSpace(gatewayMacText))
            {
                _writeLine("cut requires --gateway-mac <mac>");
                return 2;
            }

            if (!arguments.TryGetOption("target", out var targetText) || string.IsNullOrWhiteSpace(targetText))
            {
                _writeLine("cut requires --target <ipv4@mac>");
                return 2;
            }

            var arpProtectionEnabled = !arguments.Options.ContainsKey("no-arp-protection");
            _writeLine($"ARP protection: {(arpProtectionEnabled ? "enabled" : "disabled")}");

            var durationSeconds = TryGetIntOption(arguments, "duration", 30);
            var gatewayMac = gatewayMacText!.Parse();
            var targets = ParseTargets(targetText!);

            var spoofer = new Spoofer(_writeLine);
            spoofer.Start(targets, gatewayIp, gatewayMac, adapter);
            Thread.Sleep(TimeSpan.FromSeconds(durationSeconds));
            spoofer.StopAll();
            return 0;
        }

        private static IReadOnlyDictionary<IPAddress, PhysicalAddress> ParseTargets(string input)
        {
            var map = new Dictionary<IPAddress, PhysicalAddress>();
            var entries = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('@');
                if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip))
                {
                    throw new ArgumentException($"Invalid target format '{entry}'. Use ipv4@mac.");
                }

                map[ip] = parts[1].Parse();
            }

            return map;
        }

        private int ListAdapters(CliArguments arguments)
        {
            var ready = LibPcapDeviceExtensions.TryGetWinPcapDevices(out var devices, out var error);
            if (!ready)
            {
                _writeLine(error ?? "No capture adapters were found.");
                return 3;
            }

            var options = AdapterInventoryService.BuildAdapterOptions(devices.ToArray());
            var rows = new List<string>();
            foreach (var device in devices)
            {
                var option = options.FirstOrDefault(o => string.Equals(o.DeviceId, device.Name, StringComparison.Ordinal));
                var label = option?.DisplayText ?? (device.Interface?.FriendlyName ?? device.Name);
                var gateway = option?.GatewayIpAddress?.ToString() ?? "n/a";
                rows.Add($"{label} | id={device.Name} | gateway={gateway} | physical={option?.IsPhysical}");
            }

            if (arguments.Options.ContainsKey("json"))
            {
                var jsonRows = string.Join(",", rows.Select(item => $"\"{item.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""));
                _writeLine($"{{\"adapters\":[{jsonRows}]}}");
                return 0;
            }

            foreach (var row in rows)
            {
                _writeLine(row);
            }

            return 0;
        }

        private LibPcapLiveDevice? ResolveAdapter(CliArguments arguments)
        {
            var ready = LibPcapDeviceExtensions.TryGetWinPcapDevices(out var devices, out var error);
            if (!ready)
            {
                _writeLine(error ?? "No adapters available.");
                return null;
            }

            if (!arguments.TryGetOption("adapter", out var adapterValue) || string.IsNullOrWhiteSpace(adapterValue))
            {
                _writeLine("Missing required option: --adapter <id|friendly-name>");
                return null;
            }

            var options = AdapterInventoryService.BuildAdapterOptions(devices.ToArray());
            var selectedOption = options.FirstOrDefault(o => string.Equals(o.DeviceId, adapterValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.DisplayText, adapterValue, StringComparison.OrdinalIgnoreCase));
            return selectedOption == null
                ? devices.FirstOrDefault(d => string.Equals(d.Interface?.FriendlyName, adapterValue, StringComparison.OrdinalIgnoreCase))
                : devices.FirstOrDefault(d => string.Equals(d.Name, selectedOption.DeviceId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetIpOption(CliArguments arguments, string key, out IPAddress ip)
        {
            ip = IPAddress.None;
            return arguments.TryGetOption(key, out var value) && !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value, out ip);
        }

        private static int TryGetIntOption(CliArguments arguments, string key, int fallback) => arguments.TryGetOption(key, out var value) && int.TryParse(value, out var parsed)
                ? parsed
                : fallback;

    }
}
