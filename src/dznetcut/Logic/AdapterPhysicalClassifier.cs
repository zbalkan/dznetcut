using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;

namespace dznetcut.Logic
{
    internal static class AdapterPhysicalClassifier
    {
        private static readonly string[] PhysicalBusPrefixes = { "PCI\\", "USB\\", "ACPI\\" };

        public static IReadOnlyDictionary<string, bool> BuildByInterfaceId(IEnumerable<NetworkInterface> interfaces)
        {
            var pnpByConfigId = ReadWin32PnpDeviceMap();
            var classificationByConfigId = BuildByConfigIdMap(pnpByConfigId);
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var networkInterface in interfaces)
            {
                if (networkInterface == null || string.IsNullOrWhiteSpace(networkInterface.Id))
                {
                    continue;
                }

                if (classificationByConfigId.TryGetValue(networkInterface.Id, out var classification))
                {
                    result[networkInterface.Id] = classification;
                    continue;
                }

                result[networkInterface.Id] = IsLikelyPhysicalPnpDeviceId(
                    pnpByConfigId.TryGetValue(networkInterface.Id, out var pnp) ? pnp : null);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, bool> BuildByConfigIdMap(IReadOnlyDictionary<string, string> pnpByConfigId)
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            BuildFromMsftNetAdapter(map);
            BuildFromWin32PhysicalAdapter(map);
            BuildFromPnpPrefixes(map, pnpByConfigId);
            return map;
        }

        internal static bool IsLikelyPhysicalPnpDeviceId(string? pnpDeviceId)
        {
            if (string.IsNullOrWhiteSpace(pnpDeviceId))
            {
                return false;
            }

            return PhysicalBusPrefixes.Any(prefix => pnpDeviceId!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static void BuildFromMsftNetAdapter(IDictionary<string, bool> map)
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\StandardCimv2");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT InterfaceGuid, HardwareInterface FROM MSFT_NetAdapter WHERE InterfaceGuid IS NOT NULL"));
                foreach (var adapter in searcher.Get().Cast<ManagementObject>())
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
        }

        private static void BuildFromWin32PhysicalAdapter(IDictionary<string, bool> map)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT GUID, PhysicalAdapter FROM Win32_NetworkAdapter WHERE GUID IS NOT NULL AND PhysicalAdapter IS NOT NULL");
                foreach (var adapter in searcher.Get().Cast<ManagementObject>())
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
        }

        private static void BuildFromPnpPrefixes(IDictionary<string, bool> map, IReadOnlyDictionary<string, string> pnpByConfigId)
        {
            foreach (var pair in pnpByConfigId)
            {
                if (map.ContainsKey(pair.Key))
                {
                    continue;
                }

                map[pair.Key] = IsLikelyPhysicalPnpDeviceId(pair.Value);
            }
        }

        private static Dictionary<string, string> ReadWin32PnpDeviceMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT GUID, PNPDeviceID FROM Win32_NetworkAdapter WHERE GUID IS NOT NULL AND PNPDeviceID IS NOT NULL");
                foreach (var adapter in searcher.Get().Cast<ManagementObject>())
                {
                    var guid = adapter["GUID"] as string;
                    var pnpDeviceId = adapter["PNPDeviceID"] as string;
                    if (!string.IsNullOrWhiteSpace(guid) && !string.IsNullOrWhiteSpace(pnpDeviceId))
                    {
                        map[guid!] = pnpDeviceId!;
                    }
                }
            }
            catch
            {
            }

            return map;
        }
    }
}