using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using SharpPcap.LibPcap;

namespace dznetcut.Logic
{
    internal sealed class AdapterDeviceSnapshot
    {
        public AdapterDeviceSnapshot(string deviceId, string displayName, IPAddress? ipv4Address, PhysicalAddress? macAddress, string? preferredInterfaceId = null)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
            Ipv4Address = ipv4Address;
            MacAddress = macAddress;
            PreferredInterfaceId = preferredInterfaceId;
        }

        public string DeviceId { get; }
        public string DisplayName { get; }
        public IPAddress? Ipv4Address { get; }
        public PhysicalAddress? MacAddress { get; }
        public string? PreferredInterfaceId { get; }
    }

    internal sealed class AdapterSelectionOptionModel
    {
        public AdapterSelectionOptionModel(string deviceId, string displayText, string? interfaceId, IPAddress? gatewayIpAddress, bool isPhysical)
        {
            DeviceId = deviceId;
            DisplayText = displayText;
            InterfaceId = interfaceId;
            GatewayIpAddress = gatewayIpAddress;
            IsPhysical = isPhysical;
        }

        public string DeviceId { get; }
        public string DisplayText { get; }
        public string? InterfaceId { get; }
        public IPAddress? GatewayIpAddress { get; }
        public bool IsPhysical { get; }
    }

    internal sealed class InterfaceSnapshot
    {
        public InterfaceSnapshot(
            string interfaceId,
            string name,
            PhysicalAddress? macAddress,
            NetworkInterfaceType interfaceType,
            bool isPhysicalAdapter,
            IReadOnlyCollection<IPAddress> unicastAddresses,
            IReadOnlyCollection<IPAddress> gatewayAddresses)
        {
            InterfaceId = interfaceId;
            Name = name;
            MacAddress = macAddress;
            InterfaceType = interfaceType;
            IsPhysicalAdapter = isPhysicalAdapter;
            UnicastAddresses = unicastAddresses;
            GatewayAddresses = gatewayAddresses;
        }

        public string InterfaceId { get; }
        public string Name { get; }
        public PhysicalAddress? MacAddress { get; }
        public NetworkInterfaceType InterfaceType { get; }
        public bool IsPhysicalAdapter { get; }
        public IReadOnlyCollection<IPAddress> UnicastAddresses { get; }
        public IReadOnlyCollection<IPAddress> GatewayAddresses { get; }
    }

    internal static class AdapterInventoryService
    {
        private static readonly Regex DeviceGuidRegex = new Regex(@"\{(?<guid>[0-9A-Fa-f-]{36})\}", RegexOptions.Compiled);

        public static IReadOnlyList<AdapterSelectionOptionModel> BuildAdapterOptions(IReadOnlyList<LibPcapLiveDevice> devices)
        {
            var systemInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var physicalByInterfaceId = AdapterPhysicalClassifier.BuildByInterfaceId(systemInterfaces);
            var networkInterfaces = systemInterfaces
                .Select(networkInterface =>
                    new InterfaceSnapshot(
                        networkInterface.Id,
                        networkInterface.Name,
                        networkInterface.GetPhysicalAddress(),
                        networkInterface.NetworkInterfaceType,
                        physicalByInterfaceId.TryGetValue(networkInterface.Id, out var isPhysical)
                            ? isPhysical
                            : IsLikelyPhysicalAdapter(networkInterface.NetworkInterfaceType, networkInterface.GetPhysicalAddress()),
                        networkInterface.GetIPProperties().UnicastAddresses.Select(address => address.Address).ToArray(),
                        networkInterface.GetIPProperties().GatewayAddresses.Select(address => address.Address).ToArray()))
                .ToArray();

            var compatibleInterfaces = networkInterfaces
                .Where(networkInterface => networkInterface.InterfaceType != NetworkInterfaceType.Loopback
                    && networkInterface.InterfaceType != NetworkInterfaceType.Tunnel
                    && networkInterface.InterfaceType != NetworkInterfaceType.Unknown)
                .Where(networkInterface => HasValidMac(networkInterface.MacAddress) || networkInterface.IsPhysicalAdapter)
                .ToArray();

            var deviceSnapshots = BuildDeviceSnapshotsDzmacStyle(devices, compatibleInterfaces);

            return BuildOptions(deviceSnapshots, compatibleInterfaces);
        }

        // Derived from DZMAC adapter collection approach: one adapter row per resolved interface.
        private static IReadOnlyList<AdapterDeviceSnapshot> BuildDeviceSnapshotsDzmacStyle(
            IReadOnlyList<LibPcapLiveDevice> devices,
            IReadOnlyCollection<InterfaceSnapshot> interfaces)
        {
            var mappedByInterfaceId = new Dictionary<string, AdapterDeviceSnapshot>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in devices)
            {
                var preferredInterfaceId = TryResolveInterfaceId(device, interfaces);
                var snapshot = new AdapterDeviceSnapshot(
                    device.Name,
                    device.Interface?.FriendlyName ?? device.Name,
                    TryReadDeviceIpv4(device),
                    device.MacAddress,
                    preferredInterfaceId);

                if (string.IsNullOrWhiteSpace(preferredInterfaceId))
                {
                    continue;
                }

                if (!mappedByInterfaceId.ContainsKey(preferredInterfaceId!))
                {
                    mappedByInterfaceId[preferredInterfaceId!] = snapshot;
                }
            }

            return mappedByInterfaceId.Values.ToArray();
        }

        private static bool HasValidMac(PhysicalAddress? macAddress)
        {
            var macBytes = macAddress?.GetAddressBytes() ?? Array.Empty<byte>();
            return macBytes.Length == 6 && macBytes.Any(octet => octet != 0);
        }

        private static string? TryResolveInterfaceId(LibPcapLiveDevice device, IReadOnlyCollection<InterfaceSnapshot> interfaces)
        {
            var guid = TryExtractGuid(device.Name);
            if (!string.IsNullOrWhiteSpace(guid))
            {
                var byGuid = interfaces.FirstOrDefault(networkInterface =>
                    string.Equals(networkInterface.InterfaceId, guid, StringComparison.OrdinalIgnoreCase));
                if (byGuid != null)
                {
                    return byGuid.InterfaceId;
                }
            }

            var friendlyName = device.Interface?.FriendlyName;
            if (!string.IsNullOrWhiteSpace(friendlyName))
            {
                var byName = interfaces.FirstOrDefault(networkInterface =>
                    string.Equals(networkInterface.Name, friendlyName, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                {
                    return byName.InterfaceId;
                }
            }

            return null;
        }

        private static string? TryExtractGuid(string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return null;
            }

            var match = DeviceGuidRegex.Match(deviceName);
            return match.Success ? match.Groups["guid"].Value : null;
        }

        private static IPAddress? TryReadDeviceIpv4(LibPcapLiveDevice device)
        {
            try { return device.ReadCurrentIpV4Address(); }
            catch (InvalidOperationException) { return null; }
        }

        internal static IReadOnlyList<AdapterSelectionOptionModel> FilterAdapterOptions(IReadOnlyCollection<AdapterSelectionOptionModel> options, bool includeVirtualAdapters) => (includeVirtualAdapters ? options : options.Where(option => option.IsPhysical))
                .OrderByDescending(option => option.IsPhysical)
                .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        internal static IReadOnlyList<AdapterSelectionOptionModel> BuildOptions(
            IReadOnlyCollection<AdapterDeviceSnapshot> devices,
            IReadOnlyCollection<InterfaceSnapshot> interfaces)
        {
            var options = new List<AdapterSelectionOptionModel>(devices.Count);
            foreach (var device in devices)
            {
                var matchedInterface = ResolveInterfaceMatch(device, interfaces);
                var gatewayIp = matchedInterface?.GatewayAddresses
                    .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                options.Add(new AdapterSelectionOptionModel(
                    device.DeviceId,
                    $"{device.DisplayName} [{device.Ipv4Address?.ToString() ?? "No IPv4"}]",
                    matchedInterface?.InterfaceId,
                    gatewayIp,
                    matchedInterface?.IsPhysicalAdapter == true));
            }

            return options
                .OrderByDescending(option => option.IsPhysical)
                .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static InterfaceSnapshot? ResolveInterfaceMatch(AdapterDeviceSnapshot device, IReadOnlyCollection<InterfaceSnapshot> interfaces)
        {
            if (!string.IsNullOrWhiteSpace(device.PreferredInterfaceId))
            {
                var byId = interfaces.FirstOrDefault(networkInterface =>
                    string.Equals(networkInterface.InterfaceId, device.PreferredInterfaceId, StringComparison.OrdinalIgnoreCase));
                if (byId != null)
                {
                    return byId;
                }
            }

            return interfaces.FirstOrDefault(networkInterface => IsInterfaceMatch(networkInterface, device.Ipv4Address, device.MacAddress));
        }

        internal static bool IsInterfaceMatch(InterfaceSnapshot networkInterface, IPAddress? sourceIp, PhysicalAddress? sourceMac)
        {
            if (sourceMac != null && sourceMac.GetAddressBytes().Length == 6 && networkInterface.MacAddress != null && networkInterface.MacAddress.Equals(sourceMac))
            {
                return true;
            }

            return sourceIp != null && networkInterface.UnicastAddresses.Any(address => sourceIp.Equals(address));
        }

        internal static bool IsLikelyPhysicalAdapter(NetworkInterfaceType interfaceType, PhysicalAddress? macAddressValue)
        {
            switch (interfaceType)
            {
                case NetworkInterfaceType.Loopback:
                case NetworkInterfaceType.Tunnel:
                case NetworkInterfaceType.Unknown:
                    return false;
            }

            var macAddress = macAddressValue?.GetAddressBytes() ?? Array.Empty<byte>();
            return macAddress.Length == 6 && macAddress.Any(octet => octet != 0);
        }
    }
}