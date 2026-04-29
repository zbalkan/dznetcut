using System;
using System.Linq;
using System.Net;
using SharpPcap.LibPcap;

namespace dznetcut.Logic
{
    public static class LibPcapDeviceExtensions
    {
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