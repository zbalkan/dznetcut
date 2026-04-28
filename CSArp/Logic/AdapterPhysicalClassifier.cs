using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;

namespace CSArp.Logic
{
    internal static class AdapterPhysicalClassifier
    {
        private static readonly string[] PhysicalBusPrefixes = { "PCI\\", "USB\\", "ACPI\\" };

        public static IReadOnlyDictionary<string, bool> BuildByInterfaceId(IEnumerable<NetworkInterface> interfaces)
        {
            var msftMap = ReadMsftHardwareInterfaceMap();
            var win32PhysicalMap = ReadWin32PhysicalAdapterMap();
            var pnpMap = ReadWin32PnpDeviceMap();
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var networkInterface in interfaces)
            {
                if (networkInterface == null || string.IsNullOrWhiteSpace(networkInterface.Id))
                {
                    continue;
                }

                if (msftMap.TryGetValue(networkInterface.Id, out var msftPhysical))
                {
                    result[networkInterface.Id] = msftPhysical;
                    continue;
                }

                if (win32PhysicalMap.TryGetValue(networkInterface.Id, out var win32Physical))
                {
                    result[networkInterface.Id] = win32Physical;
                    continue;
                }

                if (pnpMap.TryGetValue(networkInterface.Id, out var pnpDeviceId))
                {
                    result[networkInterface.Id] = IsLikelyPhysicalPnpDeviceId(pnpDeviceId);
                    continue;
                }

                result[networkInterface.Id] = AdapterSelectionService.IsLikelyPhysicalAdapter(networkInterface.NetworkInterfaceType, networkInterface.GetPhysicalAddress());
            }

            return result;
        }

        internal static bool IsLikelyPhysicalPnpDeviceId(string? pnpDeviceId)
        {
            if (string.IsNullOrWhiteSpace(pnpDeviceId))
            {
                return false;
            }

            return PhysicalBusPrefixes.Any(prefix => pnpDeviceId!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, bool> ReadMsftHardwareInterfaceMap()
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var scope = new ManagementScope(@"\\.\root\StandardCimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT InterfaceGuid, HardwareInterface FROM MSFT_NetAdapter WHERE InterfaceGuid IS NOT NULL"));

                foreach (var adapter in searcher.Get().OfType<ManagementObject>())
                {
                    var guid = adapter["InterfaceGuid"] as string;
                    if (string.IsNullOrWhiteSpace(guid) || map.ContainsKey(guid!))
                    {
                        continue;
                    }

                    if (adapter["HardwareInterface"] is bool isPhysical)
                    {
                        map[guid!] = isPhysical;
                    }
                }
            }
            catch
            {
            }

            return map;
        }

        private static Dictionary<string, bool> ReadWin32PhysicalAdapterMap()
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT GUID, PhysicalAdapter FROM Win32_NetworkAdapter WHERE GUID IS NOT NULL AND PhysicalAdapter IS NOT NULL");

                foreach (var adapter in searcher.Get().OfType<ManagementObject>())
                {
                    var guid = adapter["GUID"] as string;
                    if (string.IsNullOrWhiteSpace(guid) || map.ContainsKey(guid!))
                    {
                        continue;
                    }

                    if (adapter["PhysicalAdapter"] is bool isPhysical)
                    {
                        map[guid!] = isPhysical;
                    }
                }
            }
            catch
            {
            }

            return map;
        }

        private static Dictionary<string, string> ReadWin32PnpDeviceMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT GUID, PNPDeviceID FROM Win32_NetworkAdapter WHERE GUID IS NOT NULL AND PNPDeviceID IS NOT NULL");

                foreach (var adapter in searcher.Get().OfType<ManagementObject>())
                {
                    var guid = adapter["GUID"] as string;
                    var pnpDeviceId = adapter["PNPDeviceID"] as string;
                    if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(pnpDeviceId) || map.ContainsKey(guid!))
                    {
                        continue;
                    }

                    map[guid!] = pnpDeviceId!;
                }
            }
            catch
            {
            }

            return map;
        }
    }
}