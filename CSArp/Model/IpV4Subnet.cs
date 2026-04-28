using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CSArp.Model
{
    public sealed class IPV4Subnet
    {
        public IPAddress First { get; private set; }
        public IPAddress Last { get; private set; }
        public IPAddress NetworkAddress { get; private set; }
        public IPAddress BroadcastAddress { get; private set; }

        private readonly uint networkAddressAsUint;
        private readonly uint broadcastAddressAsUint;
        private readonly uint firstAddressAsUint;
        private readonly uint lastAddressAsUint;

        public IPV4Subnet(IPAddress currentAddress, IPAddress subnetMask)
        {
            if (currentAddress == null)
            {
                throw new ArgumentNullException(nameof(currentAddress));
            }

            if (subnetMask == null)
            {
                throw new ArgumentNullException(nameof(subnetMask));
            }

            if (currentAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 addresses are supported.", nameof(currentAddress));
            }

            if (subnetMask.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 subnet masks are supported.", nameof(subnetMask));
            }

            var ip = ConvertToUint(currentAddress);
            var mask = ConvertToUint(subnetMask);

            if (!IsContiguousMask(mask))
            {
                throw new ArgumentException("Subnet mask must be contiguous.", nameof(subnetMask));
            }

            networkAddressAsUint = ip & mask;
            broadcastAddressAsUint = networkAddressAsUint | ~mask;

            NetworkAddress = ConvertToIPv4Address(networkAddressAsUint);
            BroadcastAddress = ConvertToIPv4Address(broadcastAddressAsUint);

            if (networkAddressAsUint < broadcastAddressAsUint)
            {
                firstAddressAsUint = networkAddressAsUint + 1;
                lastAddressAsUint = broadcastAddressAsUint - 1;
            }
            else
            {
                firstAddressAsUint = networkAddressAsUint;
                lastAddressAsUint = broadcastAddressAsUint;
            }

            First = ConvertToIPv4Address(firstAddressAsUint);
            Last = ConvertToIPv4Address(lastAddressAsUint);
        }

        public long Count => (long)broadcastAddressAsUint - networkAddressAsUint + 1;

        public long UsableHostCount {
            get {
                if (broadcastAddressAsUint <= networkAddressAsUint)
                {
                    return 1;
                }

                if (broadcastAddressAsUint - networkAddressAsUint == 1)
                {
                    return 0;
                }

                return (long)lastAddressAsUint - firstAddressAsUint + 1;
            }
        }

        public bool Contains(IPAddress ipAddress)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var address = ConvertToUint(ipAddress);
            return networkAddressAsUint <= address && address <= broadcastAddressAsUint;
        }

        public bool ContainsUsableHost(IPAddress ipAddress)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            if (UsableHostCount <= 0)
            {
                return false;
            }

            var address = ConvertToUint(ipAddress);
            return firstAddressAsUint <= address && address <= lastAddressAsUint;
        }

        public IEnumerable<IPAddress> Enumerate()
        {
            for (var address = networkAddressAsUint; address <= broadcastAddressAsUint; address++)
            {
                yield return ConvertToIPv4Address(address);

                if (address == uint.MaxValue)
                {
                    yield break;
                }
            }
        }

        public List<IPAddress> ToList()
        {
            if (Count > int.MaxValue)
            {
                throw new InvalidOperationException("Subnet is too large to materialize as a List<IPAddress>.");
            }

            return new List<IPAddress>(Enumerate());
        }

        private static bool IsContiguousMask(uint mask)
        {
            var inverted = ~mask;
            return (inverted & (inverted + 1)) == 0;
        }

        private static uint ConvertToUint(IPAddress ipAddress)
        {
            var addressBytes = ipAddress.GetAddressBytes();

            if (addressBytes.Length != 4)
            {
                throw new ArgumentException("Only IPv4 addresses are supported.", nameof(ipAddress));
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(addressBytes);
            }

            return BitConverter.ToUInt32(addressBytes, 0);
        }

        private static IPAddress ConvertToIPv4Address(uint value)
        {
            var addressBytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(addressBytes);
            }

            return new IPAddress(addressBytes);
        }
    }
}