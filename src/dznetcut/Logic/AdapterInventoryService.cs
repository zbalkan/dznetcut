using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using SharpPcap.LibPcap;

namespace dznetcut.Logic
{
    internal static class AdapterInventoryService
    {
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
                            : AdapterSelectionService.IsLikelyPhysicalAdapter(networkInterface.NetworkInterfaceType, networkInterface.GetPhysicalAddress()),
                        networkInterface.GetIPProperties().UnicastAddresses.Select(address => address.Address).ToArray(),
                        networkInterface.GetIPProperties().GatewayAddresses.Select(address => address.Address).ToArray()))
                .ToArray();
            var deviceSnapshots = devices
                .Select(device =>
                    new AdapterDeviceSnapshot(
                        device.Name,
                        device.Interface?.FriendlyName ?? device.Name,
                        TryReadDeviceIpv4(device),
                        device.MacAddress))
                .ToArray();

            return AdapterSelectionService.BuildOptions(deviceSnapshots, networkInterfaces);
        }

        private static IPAddress? TryReadDeviceIpv4(LibPcapLiveDevice device)
        {
            try { return device.ReadCurrentIpV4Address(); }
            catch (InvalidOperationException) { return null; }
        }
    }
}
