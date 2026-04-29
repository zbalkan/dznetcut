using System;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;

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
