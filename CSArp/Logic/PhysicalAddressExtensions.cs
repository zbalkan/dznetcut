using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace CSArp.Logic
{
    public static class PhysicalAddressExtensions
    {
        public static PhysicalAddress Parse(this string address, string separator = "-")
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException($"'{nameof(address)}' cannot be null or empty.", nameof(address));
            }

            return PhysicalAddress.Parse(address.ToUpperInvariant().Replace(separator, string.Empty));
        }

        public static string ToString(this PhysicalAddress address, string separator)
        {
            if (address is null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if (string.IsNullOrEmpty(separator))
            {
                throw new ArgumentException($"'{nameof(separator)}' cannot be null or empty.", nameof(separator));
            }

            return string.Join(separator, address.GetAddressBytes()
                                                 .Select(x => x.ToString("X2")));
        }
    }
}