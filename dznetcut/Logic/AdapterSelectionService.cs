using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace dznetcut.Logic
{
    internal static class AdapterSelectionService
    {
        public static IReadOnlyList<AdapterSelectionOptionModel> BuildOptions(
            IReadOnlyCollection<AdapterDeviceSnapshot> devices,
            IReadOnlyCollection<InterfaceSnapshot> interfaces)
        {
            var options = new List<AdapterSelectionOptionModel>();

            foreach (var device in devices)
            {
                var matchedInterface = interfaces.FirstOrDefault(networkInterface =>
                    IsInterfaceMatch(networkInterface, device.Ipv4Address, device.MacAddress));

                var isPhysical = matchedInterface != null && matchedInterface.IsPhysicalAdapter;
                var ipLabel = device.Ipv4Address?.ToString() ?? "No IPv4";
                var displayText = $"{device.DisplayName} [{ipLabel}]";
                var gatewayIp = matchedInterface?.GatewayAddresses
                    .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                options.Add(new AdapterSelectionOptionModel(
                    device.DeviceId,
                    displayText,
                    matchedInterface?.InterfaceId,
                    gatewayIp,
                    isPhysical));
            }

            return options
                .OrderByDescending(option => option.IsPhysical)
                .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<AdapterSelectionOptionModel> FilterOptions(IReadOnlyCollection<AdapterSelectionOptionModel> options, bool includeVirtualAdapters)
        {
            if (includeVirtualAdapters)
            {
                return options.OrderByDescending(option => option.IsPhysical).ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase).ToArray();
            }

            return options
                .Where(option => option.IsPhysical)
                .OrderBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static bool IsInterfaceMatch(InterfaceSnapshot networkInterface, IPAddress? sourceIp, PhysicalAddress? sourceMac)
        {
            if (sourceMac != null && sourceMac.GetAddressBytes().Length == 6 && networkInterface.MacAddress != null && networkInterface.MacAddress.Equals(sourceMac))
            {
                return true;
            }

            if (sourceIp == null)
            {
                return false;
            }

            return networkInterface.UnicastAddresses.Any(address => sourceIp.Equals(address));
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
            if (macAddress.Length != 6)
            {
                return false;
            }

            return macAddress.Any(octet => octet != 0);
        }
    }

    internal sealed class AdapterDeviceSnapshot
    {
        public AdapterDeviceSnapshot(string deviceId, string displayName, IPAddress? ipv4Address, PhysicalAddress? macAddress)
        {
            DeviceId = deviceId;
            DisplayName = displayName;

            Ipv4Address = ipv4Address;
            MacAddress = macAddress;
        }

        public string DeviceId { get; }
        public string DisplayName { get; }
        public IPAddress? Ipv4Address { get; }
        public PhysicalAddress? MacAddress { get; }
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
        public IPAddress? GatewayIpAddress { get; }
        public string? InterfaceId { get; }
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

        public IReadOnlyCollection<IPAddress> GatewayAddresses { get; }
        public string InterfaceId { get; }
        public NetworkInterfaceType InterfaceType { get; }
        public bool IsPhysicalAdapter { get; }
        public PhysicalAddress? MacAddress { get; }
        public string Name { get; }
        public IReadOnlyCollection<IPAddress> UnicastAddresses { get; }
    }
}