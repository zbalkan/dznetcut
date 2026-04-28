using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CSArp.Logic
{
    internal sealed class IPV4Subnet
    {
        private readonly uint _networkAddress;
        private readonly uint _broadcastAddress;

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

            _networkAddress = ip & mask;
            _broadcastAddress = _networkAddress | ~mask;
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
            return _networkAddress <= address && address <= _broadcastAddress;
        }

        public IEnumerable<IPAddress> EnumerateHosts()
        {
            if (_networkAddress >= _broadcastAddress)
            {
                yield break;
            }

            for (var address = _networkAddress + 1; address < _broadcastAddress; address++)
            {
                yield return ConvertToIPv4Address(address);
            }
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
