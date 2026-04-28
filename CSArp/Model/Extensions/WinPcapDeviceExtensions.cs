using System.Linq;
using SharpPcap.LibPcap;
using System.Net;
using System.Net.NetworkInformation;
using CSArp.Model;
using System;

namespace CSArp.Model.Extensions
{
    // Getting a readonly collection populated with addreses.
    // If it is an IPv4 interface, you can get IP Address, subnet mask etc.
    // if not, there is only physical address. Therefore, we are checking these here.
    //
    // Beware that AirPcap is an obsolete protocol. Therefore we are using only winpcap devices for now.
    // TODO: Add Mode selection: WinPcap & AirPcap.
    public static class WinPcapDeviceExtensions
    {
        public static IPV4Subnet ReadCurrentSubnet(this LibPcapLiveDevice device)
        {
            var addresses = device.Addresses.FirstOrDefault(addr => addr.Addr.ipAddress != null);
            var currentAddress = addresses.Addr.ipAddress;
            var subnetMask = new IPAddress(addresses.Netmask.ipAddress.GetAddressBytes().Reverse().ToArray());// Sharppcap returns reversed mask

            return new IPV4Subnet(currentAddress, subnetMask);
        }

        public static IPAddress ReadCurrentIpV4Address(this LibPcapLiveDevice device)
        {
            // Type information in WinPcap is plain wrong. IPv4 addresses are assumed to be IPv6 and most of the time @type is just null.
            // So, we are querying address list to find related information by looking at current behavior.
            return device.Addresses.FirstOrDefault(addr => addr.Addr.ipAddress != null).Addr.ipAddress;
        }

        [Obsolete("Use device.MacAddress instead")]
        public static PhysicalAddress ReadCurrentPhysicalAddress(this LibPcapLiveDevice device)
        {
            // Type information in WinPcap is plain wrong. IPv4 addresses are assumed to be IPv6 and most of the time @type is just null.
            // So, we are querying address list to find related information by looking at current behavior.
            return device.Addresses.FirstOrDefault(addr => addr.Addr.ipAddress == null).Addr.hardwareAddress;
        }
    }
}
