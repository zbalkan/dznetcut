using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Linq;
using System.Threading.Tasks;

namespace CSArp.View
{
    public static class PhysicalAddressExtensions
    {
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

        /// <summary>
        ///     .NET Core 3.1 and later accepts many formats, but previous versions require strict formatted strings.
        /// </summary>
        /// <see cref="https://docs.microsoft.com/en-us/dotnet/api/system.net.networkinformation.physicaladdress.parse?redirectedfrom=MSDN&view=net-5.0#System_Net_NetworkInformation_PhysicalAddress_Parse_System_String_"/>
        /// <param name="address">string to be parsed as MAC address</param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static PhysicalAddress Parse(this string address, string separator = "-")
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException($"'{nameof(address)}' cannot be null or empty.", nameof(address));
            }

            return PhysicalAddress.Parse(address.ToUpperInvariant().Replace(separator, string.Empty));
        }
    }
}