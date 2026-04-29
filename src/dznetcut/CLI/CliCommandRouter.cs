using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
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
                if (!string.IsNullOrWhiteSpace(arguments.ParseError))
                {
                    _writeLine(arguments.ParseError!);
                }

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
            arguments.TryGetOption("adapter", out var adapterValue);
            if (!AdapterCatalogService.TryResolveAdapter(adapterValue, out var adapter, out var error))
            {
                _writeLine(error!);
                return 2;
            }

            if (!TryGetIpOption(arguments, "gateway-ip", out var gatewayIp))
            {
                _writeLine("scan requires --gateway-ip <ipv4>");
                return 2;
            }

            if (!TryGetDurationSeconds(arguments, out var durationSeconds))
            {
                return 2;
            }
            var hosts = AdapterCatalogService.Scan(adapter!, gatewayIp, durationSeconds);

            foreach (var host in hosts)
            {
                _writeLine($"{host.IpAddress} | {host.MacAddress.ToString("-")} | confidence={host.ConfidenceScore} | gateway={host.IsGateway}");
            }

            return 0;
        }

        private int Cut(CliArguments arguments)
        {
            arguments.TryGetOption("adapter", out var adapterValue);
            if (!AdapterCatalogService.TryResolveAdapter(adapterValue, out var adapter, out var error))
            {
                _writeLine(error!);
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

            if (!TryGetDurationSeconds(arguments, out var durationSeconds))
            {
                return 2;
            }
            var gatewayMac = gatewayMacText!.Parse();
            var targets = AdapterCatalogService.ParseTargets(targetText!);

            AdapterCatalogService.Cut(adapter!, gatewayIp, gatewayMac, targets, durationSeconds, arpProtectionEnabled, _writeLine);
            return 0;
        }

        private int ListAdapters(CliArguments arguments)
        {
            if (!AdapterCatalogService.TryListAdapters(out var adapters, out var error))
            {
                _writeLine(error ?? "No capture adapters were found.");
                return 3;
            }

            if (arguments.Options.ContainsKey("json"))
            {
                _writeLine(JsonSerializer.Serialize(new { adapters = adapters.Select(FormatAdapterRow).ToArray() }));
                return 0;
            }

            foreach (var adapter in adapters)
            {
                _writeLine(FormatAdapterRow(adapter));
            }

            return 0;
        }

        private static string FormatAdapterRow(AdapterListItemModel adapter)
        {
            var gateway = adapter.GatewayIpAddress?.ToString() ?? "n/a";
            return $"{adapter.Label} | id={adapter.DeviceId} | gateway={gateway} | physical={adapter.IsPhysical}";
        }

        private static bool TryGetIpOption(CliArguments arguments, string key, out IPAddress ip)
        {
            ip = IPAddress.None;
            return arguments.TryGetOption(key, out var value) && !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value, out ip);
        }

        private static int TryGetIntOption(CliArguments arguments, string key, int fallback) => arguments.TryGetOption(key, out var value) && int.TryParse(value, out var parsed)
                ? parsed
                : fallback;

        private bool TryGetDurationSeconds(CliArguments arguments, out int durationSeconds)
        {
            durationSeconds = TryGetIntOption(arguments, "duration", 30);
            if (durationSeconds < 1)
            {
                _writeLine("duration must be a positive integer number of seconds.");
                return false;
            }

            return true;
        }

    }
}
