using System;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;

namespace dznetcut.Logic
{
    internal sealed class GatewayBinding
    {
        public GatewayBinding(IPAddress ip, PhysicalAddress mac, int interfaceIndex)
        {
            Ip = ip;
            Mac = mac;
            InterfaceIndex = interfaceIndex;
        }

        public int InterfaceIndex { get; }
        public IPAddress Ip { get; }
        public PhysicalAddress Mac { get; }
    }

    internal sealed class ArpProtectionService
    {
        private readonly GatewayBinding _binding;

        public ArpProtectionService(GatewayBinding binding) => _binding = binding;

        public static ArpProtectionService Create(string interfaceId, IPAddress gatewayIp, PhysicalAddress gatewayMac)
        {
            if (string.IsNullOrWhiteSpace(interfaceId))
            {
                throw new InvalidOperationException("Cannot map selected interface to a Windows network adapter.");
            }

            var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic => string.Equals(nic.Id, interfaceId, StringComparison.Ordinal));
            var ipv4Properties = networkInterface?.GetIPProperties().GetIPv4Properties()
                ?? throw new InvalidOperationException("Cannot resolve selected interface index.");

            return new ArpProtectionService(new GatewayBinding(gatewayIp, gatewayMac, ipv4Properties.Index));
        }

        public static void Enable(string interfaceId, IPAddress gatewayIp, PhysicalAddress gatewayMac)
            => Create(interfaceId, gatewayIp, gatewayMac).Enabled = true;

        public static void Disable(string interfaceId, IPAddress gatewayIp, PhysicalAddress gatewayMac)
            => Create(interfaceId, gatewayIp, gatewayMac).Enabled = false;

        public bool Enabled
        {
            get => NativeArp.EntryExists(_binding);
            set
            {
                if (value == Enabled)
                {
                    return;
                }

                var error = value
                    ? NativeArp.Add(_binding)
                    : NativeArp.Remove(_binding);
                if (error != 0)
                {
                    throw new Win32Exception((int)error);
                }
            }
        }
    }
}
