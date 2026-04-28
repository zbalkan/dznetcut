using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using SharpPcap;
using SharpPcap.LibPcap;

namespace dznetcut.Logic
{
    public static class LibPcapDeviceExtensions
    {
        public static IReadOnlyList<LibPcapLiveDevice> GetWinPcapDevices()
        {
            _ = TryGetWinPcapDevices(out var devices, out _);
            return devices;
        }

        public static bool TryGetWinPcapDevices(out IReadOnlyList<LibPcapLiveDevice> devices, out string? errorMessage)
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

        public static IPAddress ReadCurrentIpV4Address(this LibPcapLiveDevice device) =>
            ReadCurrentNetworkInfo(device).ipAddress;

        internal static IPV4Subnet ReadCurrentSubnet(this LibPcapLiveDevice device)
        {
            var (ipAddress, subnetMask) = ReadCurrentNetworkInfo(device);
            return new IPV4Subnet(ipAddress, subnetMask);
        }
        private static (IPAddress ipAddress, IPAddress subnetMask) ReadCurrentNetworkInfo(LibPcapLiveDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            var address = device.Addresses.FirstOrDefault(addr =>
                addr.Addr?.ipAddress != null &&
                addr.Netmask?.ipAddress != null &&
                addr.Addr.ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (address?.Addr?.ipAddress == null || address.Netmask?.ipAddress == null)
            {
                throw new InvalidOperationException("Could not find an IPv4 address for the selected adapter.");
            }

            return (address.Addr.ipAddress, address.Netmask.ipAddress);
        }
    }
}