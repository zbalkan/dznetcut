using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace dznetcut.Logic
{
    internal static class NativeArp
    {
        private const int MibIpNetTypeStatic = 4;

        public static uint Add(GatewayBinding binding)
        {
            var row = CreateRow(binding);
            return CreateIpNetEntry(ref row);
        }

        public static bool EntryExists(GatewayBinding binding)
        {
            return GetTable().Any(row =>
                row.dwIndex == binding.InterfaceIndex &&
                row.dwAddr == IpToUInt32(binding.Ip));
        }

        public static uint Remove(GatewayBinding binding)
        {
            var row = CreateRow(binding);
            return DeleteIpNetEntry(ref row);
        }

        private static MibIpNetRow CreateRow(GatewayBinding binding)
        {
            var row = new MibIpNetRow
            {
                dwAddr = IpToUInt32(binding.Ip),
                dwIndex = binding.InterfaceIndex,
                dwPhysAddrLen = 6,
                dwType = MibIpNetTypeStatic
            };

            var mac = binding.Mac.GetAddressBytes();
            Buffer.BlockCopy(mac, 0, row.bPhysAddr, 0, Math.Min(mac.Length, row.bPhysAddr.Length));
            return row;
        }

        public static uint IpToUInt32(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 addresses are supported.", nameof(ipAddress));
            }

            var bytes = ipAddress.GetAddressBytes();
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static MibIpNetRow[] GetTable()
        {
            var size = 0;
            var result = GetIpNetTable(IntPtr.Zero, ref size, false);
            if (result != 122 || size == 0)
            {
                return Array.Empty<MibIpNetRow>();
            }

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                result = GetIpNetTable(buffer, ref size, false);
                if (result != 0)
                {
                    return Array.Empty<MibIpNetRow>();
                }

                var count = Marshal.ReadInt32(buffer);
                var rows = new MibIpNetRow[count];
                var rowPtr = IntPtr.Add(buffer, 4);
                var rowSize = Marshal.SizeOf(typeof(MibIpNetRow));
                for (var i = 0; i < count; i++)
                {
                    rows[i] = Marshal.PtrToStructure<MibIpNetRow>(IntPtr.Add(rowPtr, i * rowSize));
                }

                return rows;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint CreateIpNetEntry(ref MibIpNetRow pArpEntry);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint DeleteIpNetEntry(ref MibIpNetRow pArpEntry);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

        [StructLayout(LayoutKind.Sequential)]
        private struct MibIpNetRow
        {
            public int dwIndex;
            public uint dwPhysAddrLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] bPhysAddr;
            public uint dwAddr;
            public int dwType;

            public MibIpNetRow()
            {
                dwIndex = 0;
                dwPhysAddrLen = 0;
                bPhysAddr = new byte[8];
                dwAddr = 0;
                dwType = 0;
            }
        }
    }
}
