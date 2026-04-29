using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using SharpPcap;
using SharpPcap.LibPcap;

namespace dznetcut.Logic
{
    internal static class AdapterCatalogService
    {
        private static readonly IComparer<IPAddress> IpAddressComparer = CreateIpAddressComparer();

        public static AdapterCatalogModel BuildCatalog(IReadOnlyList<LibPcapLiveDevice> devices, bool includeVirtualAdapters)
        {
            var options = AdapterInventoryService.BuildAdapterOptions(devices);
            var visibleOptions = AdapterInventoryService.FilterAdapterOptions(options, includeVirtualAdapters);
            var devicesById = devices.ToDictionary(device => device.Name, StringComparer.Ordinal);
            var optionsByDeviceId = options.ToDictionary(option => option.DeviceId, StringComparer.Ordinal);

            return new AdapterCatalogModel(devicesById, optionsByDeviceId, visibleOptions);
        }

        public static void Cut(LibPcapLiveDevice adapter, IPAddress gatewayIp, PhysicalAddress gatewayMac, IReadOnlyDictionary<IPAddress, PhysicalAddress> targets, int durationSeconds, bool arpProtectionEnabled, Action<string> writeLine)
        {
            var trafficCutter = new TrafficCutter(writeLine);
            var optionsByDeviceId = BuildCatalog(new[] { adapter }, includeVirtualAdapters: true).OptionsByDeviceId;
            _ = optionsByDeviceId.TryGetValue(adapter.Name, out var selectedOption);

            try
            {
                if (arpProtectionEnabled)
                {
                    if (selectedOption?.InterfaceId == null)
                    {
                        throw new InvalidOperationException("Cannot map selected adapter to a Windows interface for ARP protection.");
                    }

                    ArpProtectionService.Enable(selectedOption.InterfaceId, gatewayIp, gatewayMac);
                }

                trafficCutter.Start(targets, gatewayIp, gatewayMac, adapter);
                Thread.Sleep(TimeSpan.FromSeconds(durationSeconds));
            }
            finally
            {
                trafficCutter.StopAll();
                if (arpProtectionEnabled && selectedOption?.InterfaceId != null)
                {
                    try { ArpProtectionService.Disable(selectedOption.InterfaceId, gatewayIp, gatewayMac); }
                    catch (Exception ex) { writeLine($"Unable to disable ARP protection: {ex.Message}"); }
                }
            }
        }

        public static IReadOnlyDictionary<IPAddress, PhysicalAddress> ParseTargets(string input)
        {
            var map = new Dictionary<IPAddress, PhysicalAddress>();
            foreach (var entry in input.Split([','], StringSplitOptions.RemoveEmptyEntries))
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

        public static IReadOnlyList<ClientDiscoveredEventArgs> Scan(LibPcapLiveDevice adapter, IPAddress gatewayIp, int durationSeconds)
        {
            var scanner = new NetworkScanner(_ => { });
            var hostsByIp = new ConcurrentDictionary<IPAddress, ClientDiscoveredEventArgs>();

            scanner.StartScan(adapter, gatewayIp,
                new Progress<ClientDiscoveredEventArgs>(host => hostsByIp[host.IpAddress] = host),
                new Progress<string>(_ => { }),
                new Progress<int>(_ => { }));

            if (!scanner.WaitForStop(TimeSpan.FromSeconds(durationSeconds + 5)))
            {
                scanner.StopScan();
            }

            return hostsByIp.Values
                .OrderBy(host => host.IpAddress, IpAddressComparer)
                .ToList();
        }

        public static bool TryListAdapters(out IReadOnlyList<AdapterListItemModel> adapters, out string? error)
        {
            adapters = Array.Empty<AdapterListItemModel>();

            if (!TryLoadCatalog(includeVirtualAdapters: true, out var catalog, out error))
            {
                return false;
            }

            adapters = catalog.DevicesById.Values.Select(device => {
                _ = catalog.OptionsByDeviceId.TryGetValue(device.Name, out var option);
                return new AdapterListItemModel(
                    deviceId: device.Name,
                    label: BuildAdapterLabel(device, option),
                    gatewayIpAddress: option?.GatewayIpAddress,
                    isPhysical: option?.IsPhysical);
            }).ToList();

            return true;
        }

        public static bool TryLoadCatalog(bool includeVirtualAdapters, out AdapterCatalogModel catalog, out string? error)
        {
            catalog = AdapterCatalogModel.Empty;

            var ready = TryGetCaptureDevices(out var devices, out error);
            if (!ready)
            {
                error ??= "No capture adapters were found.";
                return false;
            }

            catalog = BuildCatalog(devices, includeVirtualAdapters);
            return true;
        }

        public static bool TryResolveAdapter(string? adapterValue, out LibPcapLiveDevice? adapter, out string? error)
        {
            adapter = null;

            if (!TryLoadCatalog(includeVirtualAdapters: true, out var catalog, out error))
            {
                error ??= "No adapters available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(adapterValue))
            {
                error = "Missing required option: --adapter <id|friendly-name>";
                return false;
            }

            var selectedOption = FindOption(catalog, adapterValue!);
            adapter = selectedOption == null
                ? catalog.DevicesById.Values.FirstOrDefault(d => string.Equals(d.Interface?.FriendlyName, adapterValue, StringComparison.OrdinalIgnoreCase))
                : catalog.DevicesById.Values.FirstOrDefault(d => string.Equals(d.Name, selectedOption.DeviceId, StringComparison.OrdinalIgnoreCase));

            if (adapter == null)
            {
                error = $"Unable to resolve adapter '{adapterValue}'. Run list-adapters to inspect available IDs.";
                return false;
            }

            return true;
        }

        private static bool TryGetCaptureDevices(out IReadOnlyList<LibPcapLiveDevice> devices, out string? errorMessage)
        {
            try
            {
                devices = CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>().ToArray();
                errorMessage = null;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                devices = Array.Empty<LibPcapLiveDevice>();
                errorMessage = $"Packet capture driver not found. Install Npcap. [{ex.Message}]";
                return false;
            }
            catch (TypeInitializationException ex)
            {
                devices = Array.Empty<LibPcapLiveDevice>();
                errorMessage = $"Packet capture subsystem failed to initialize. [{ex.Message}]";
                return false;
            }
            catch (BadImageFormatException ex)
            {
                devices = Array.Empty<LibPcapLiveDevice>();
                errorMessage = $"Packet capture library architecture mismatch. [{ex.Message}]";
                return false;
            }
            catch (PcapException ex)
            {
                devices = Array.Empty<LibPcapLiveDevice>();
                errorMessage = $"Packet capture unavailable. [{ex.Message}]";
                return false;
            }
        }

        private static IComparer<IPAddress> CreateIpAddressComparer()
            => Comparer<IPAddress>.Create((x, y) =>
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                var xBytes = x.GetAddressBytes();
                var yBytes = y.GetAddressBytes();
                var length = Math.Min(xBytes.Length, yBytes.Length);
                for (var index = 0; index < length; index++)
                {
                    var comparison = xBytes[index].CompareTo(yBytes[index]);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }

                return xBytes.Length.CompareTo(yBytes.Length);
            });

        private static string BuildAdapterLabel(LibPcapLiveDevice device, AdapterSelectionOptionModel? option)
            => option?.DisplayText ?? (device.Interface?.FriendlyName ?? device.Name);

        private static AdapterSelectionOptionModel? FindOption(AdapterCatalogModel catalog, string adapterValue)
            => catalog.OptionsByDeviceId.Values.FirstOrDefault(o => string.Equals(o.DeviceId, adapterValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.DisplayText, adapterValue, StringComparison.OrdinalIgnoreCase));

    }

    internal sealed class AdapterCatalogModel
    {
        public AdapterCatalogModel(
            IReadOnlyDictionary<string, LibPcapLiveDevice> devicesById,
            IReadOnlyDictionary<string, AdapterSelectionOptionModel> optionsByDeviceId,
            IReadOnlyList<AdapterSelectionOptionModel> visibleOptions)
        {
            DevicesById = devicesById;
            OptionsByDeviceId = optionsByDeviceId;
            VisibleOptions = visibleOptions;
        }

        public static AdapterCatalogModel Empty { get; } = new AdapterCatalogModel(
                    new Dictionary<string, LibPcapLiveDevice>(StringComparer.Ordinal),
            new Dictionary<string, AdapterSelectionOptionModel>(StringComparer.Ordinal),
            Array.Empty<AdapterSelectionOptionModel>());

        public IReadOnlyDictionary<string, LibPcapLiveDevice> DevicesById { get; }
        public IReadOnlyDictionary<string, AdapterSelectionOptionModel> OptionsByDeviceId { get; }
        public IReadOnlyList<AdapterSelectionOptionModel> VisibleOptions { get; }
    }

    internal sealed class AdapterListItemModel
    {
        public AdapterListItemModel(string deviceId, string label, IPAddress? gatewayIpAddress, bool? isPhysical)
        {
            DeviceId = deviceId;
            Label = label;
            GatewayIpAddress = gatewayIpAddress;
            IsPhysical = isPhysical;
        }

        public string DeviceId { get; }
        public IPAddress? GatewayIpAddress { get; }
        public bool? IsPhysical { get; }
        public string Label { get; }
    }
}
