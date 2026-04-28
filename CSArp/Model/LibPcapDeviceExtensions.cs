using System.Linq;
using System.Net;
using SharpPcap.LibPcap;

namespace CSArp.Model
{
    // Getting a readonly collection populated with addreses.
    // If it is an IPv4 interface, you can get IP Address, subnet mask etc.
    // if not, there is only physical address. Therefore, we are checking these here.
    public static class LibPcapDeviceExtensions
    {
        public static IPV4Subnet ReadCurrentSubnet(this LibPcapLiveDevice device)
        {
            var addresses = device.Addresses.FirstOrDefault(addr => addr.Addr.ipAddress != null);
            var currentAddress = addresses.Addr.ipAddress;
            var subnetMask = addresses.Netmask.ipAddress;

            return new IPV4Subnet(currentAddress, subnetMask);
        }

        public static IPAddress ReadCurrentIpV4Address(this LibPcapLiveDevice device) =>
            // Type information in WinPcap is plain wrong. IPv4 addresses are assumed to be IPv6 and most of the time @type is just null.
            // So, we are querying address list to find related information by looking at current behavior.
            device.Addresses.FirstOrDefault(addr => addr.Addr.ipAddress != null).Addr.ipAddress;
    }
}