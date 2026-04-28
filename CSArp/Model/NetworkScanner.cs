using System;
using System.Collections.Concurrent;
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
    public sealed class ClientDiscoveredEventArgs
    {
        public ClientDiscoveredEventArgs(IPAddress ipAddress, PhysicalAddress macAddress, bool isGateway)
        {
            IpAddress = ipAddress;
            MacAddress = macAddress;
            IsGateway = isGateway;
        }

        public IPAddress IpAddress { get; }
        public PhysicalAddress MacAddress { get; }
        public bool IsGateway { get; }
    }

    public class NetworkScanner
    {
        private readonly Action<string> _log;
        private readonly ConcurrentDictionary<IPAddress, PhysicalAddress> _arpTable = new ConcurrentDictionary<IPAddress, PhysicalAddress>();
        private readonly object _stateLock = new object();

        private CancellationTokenSource _scanCts;
        private PacketArrivalEventHandler _backgroundHandler;
        private LibPcapLiveDevice _backgroundAdapter;

        public NetworkScanner(Action<string> log = null)
        {
            _log = log ?? (msg => Debug.Print(msg));
        }

        public bool IsScanning
        {
            get
            {
                lock (_stateLock)
                {
                    return _scanCts != null && !_scanCts.IsCancellationRequested;
                }
            }
        }

        public void StartScan(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress,
            IProgress<int> scanProgress)
        {
            if (networkAdapter == null)
            {
                throw new ArgumentNullException(nameof(networkAdapter));
            }

            if (gatewayIp == null)
            {
                throw new ArgumentNullException(nameof(gatewayIp));
            }

            CancellationToken token;
            lock (_stateLock)
            {
                if (_scanCts != null && !_scanCts.IsCancellationRequested)
                {
                    return;
                }

                _scanCts?.Dispose();
                _scanCts = new CancellationTokenSource();
                token = _scanCts.Token;
            }

            _arpTable.Clear();
            statusProgress?.Report("Please wait...");
            scanProgress?.Report(0);

            _ = Task.Run(() => StartForegroundScan(networkAdapter, gatewayIp, token, clientProgress, statusProgress, scanProgress));
        }

        public void StopScan()
        {
            lock (_stateLock)
            {
                _scanCts?.Cancel();
            }

            if (_backgroundAdapter != null && _backgroundHandler != null)
            {
                _backgroundAdapter.OnPacketArrival -= _backgroundHandler;
                _backgroundHandler = null;
            }
        }

        private async Task StartForegroundScan(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress,
            IProgress<int> scanProgress)
        {
            var subnet = networkAdapter.ReadCurrentSubnet();
            networkAdapter.Filter = "arp";

            var sendTask = Task.Run(
                () => InitiateArpRequestQueue(networkAdapter, gatewayIp, cancellationToken, clientProgress),
                cancellationToken);

            try
            {
                var scanTimeout = TimeSpan.FromSeconds(5);
                var stopwatch = Stopwatch.StartNew();

                PacketCapture packetCapture = default;
                while (!cancellationToken.IsCancellationRequested && stopwatch.Elapsed <= scanTimeout)
                {
                    var status = networkAdapter.GetNextPacket(out packetCapture);
                    if (status != GetPacketStatus.PacketRead)
                    {
                        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!TryExtractArpPacket(packetCapture, out var arpPacket))
                    {
                        continue;
                    }

                    ProcessPacket(arpPacket, subnet, gatewayIp, clientProgress, statusProgress);
                }

                await sendTask.ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    _log($"Discovery task finished. {_arpTable.Count} device(s) discovered.");
                    statusProgress?.Report($"{_arpTable.Count} device(s) found");
                    scanProgress?.Report(100);
                    StartBackgroundScan(networkAdapter, gatewayIp, cancellationToken, clientProgress, statusProgress);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (PcapException ex)
            {
                _log($"PcapException at foreground scan [{ex.Message}]");
                statusProgress?.Report("Refresh for scan");
                scanProgress?.Report(0);
            }
            catch (Exception ex)
            {
                _log(ex.Message);
            }
            finally
            {
                lock (_stateLock)
                {
                    if (_scanCts != null && _scanCts.IsCancellationRequested)
                    {
                        _scanCts.Dispose();
                        _scanCts = null;
                    }
                }
            }
        }

        private void StartBackgroundScan(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            try
            {
                _backgroundAdapter = networkAdapter;
                var subnet = networkAdapter.ReadCurrentSubnet();
                _backgroundHandler = (sender, e) =>
                {
                    if (cancellationToken.IsCancellationRequested || !TryExtractArpPacket(e, out var arpPacket))
                    {
                        return;
                    }

                    ProcessPacket(arpPacket, subnet, gatewayIp, clientProgress, statusProgress);
                };
                networkAdapter.OnPacketArrival += _backgroundHandler;
                networkAdapter.StartCapture();
            }
            catch (Exception ex)
            {
                _log($"Exception at background scan start [{ex.Message}]");
            }
        }

        private void InitiateArpRequestQueue(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress)
        {
            try
            {
                var subnet = networkAdapter.ReadCurrentSubnet();
                var sourceAddress = networkAdapter.ReadCurrentIpV4Address();

                if (_arpTable.TryAdd(sourceAddress, networkAdapter.MacAddress))
                {
                    clientProgress?.Report(new ClientDiscoveredEventArgs(sourceAddress, networkAdapter.MacAddress, false));
                }

                if (!sourceAddress.Equals(gatewayIp) && !cancellationToken.IsCancellationRequested)
                {
                    SendArpRequest(networkAdapter, gatewayIp);
                }

                foreach (var targetIpAddress in subnet.EnumerateHosts())
                {
                    if (cancellationToken.IsCancellationRequested || sourceAddress.Equals(targetIpAddress) || gatewayIp.Equals(targetIpAddress))
                    {
                        continue;
                    }

                    SendArpRequest(networkAdapter, targetIpAddress);
                }
            }
            catch (PcapException ex)
            {
                _log($"PcapException at InitiateArpRequestQueue [{ex.Message}]");
            }
            catch (Exception ex)
            {
                _log($"Exception at InitiateArpRequestQueue [{ex.Message}]");
            }
        }

        private void ProcessPacket(
            ArpPacket arpPacket,
            IPV4Subnet subnet,
            IPAddress gatewayIp,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            if (IPAddress.Any.Equals(arpPacket.SenderProtocolAddress) || !subnet.Contains(arpPacket.SenderProtocolAddress))
            {
                return;
            }

            if (!_arpTable.TryAdd(arpPacket.SenderProtocolAddress, arpPacket.SenderHardwareAddress))
            {
                return;
            }

            var isGateway = arpPacket.SenderProtocolAddress.Equals(gatewayIp);
            if (isGateway)
            {
                _log("Found gateway!");
            }

            _log($"Added {arpPacket.SenderProtocolAddress} @ {arpPacket.SenderHardwareAddress.ToString("-")}");
            clientProgress?.Report(new ClientDiscoveredEventArgs(arpPacket.SenderProtocolAddress, arpPacket.SenderHardwareAddress, isGateway));
            statusProgress?.Report($"{_arpTable.Count} device(s) found");
        }

        private static void SendArpRequest(LibPcapLiveDevice networkAdapter, IPAddress targetIpAddress)
        {
            var arpRequestPacket = new ArpPacket(ArpOperation.Request, "00-00-00-00-00-00".Parse(), targetIpAddress, networkAdapter.MacAddress, networkAdapter.ReadCurrentIpV4Address());
            var ethernetPacket = new EthernetPacket(networkAdapter.MacAddress, "FF-FF-FF-FF-FF-FF".Parse(), EthernetType.Arp)
            {
                PayloadPacket = arpRequestPacket
            };

            networkAdapter.SendPacket(ethernetPacket);
            Debug.WriteLine("ARP request is sent to: {0}", targetIpAddress);
        }

        private static bool TryExtractArpPacket(PacketCapture packetCapture, out ArpPacket arpPacket)
        {
            arpPacket = null;
            var rawcapture = packetCapture.GetPacket();
            if (rawcapture?.Data == null || rawcapture.Data.Length == 0)
            {
                return false;
            }

            try
            {
                var packet = Packet.ParsePacket(LinkLayers.Ethernet, rawcapture.Data);
                arpPacket = packet.Extract<ArpPacket>();
                return arpPacket != null;
            }
            catch (IndexOutOfRangeException ex)
            {
                Debug.Print($"IndexOutOfRangeException while parsing a packet [{ex.Message}]");
                return false;
            }
            catch (ArgumentException ex)
            {
                Debug.Print($"ArgumentException while parsing a packet [{ex.Message}]");
                return false;
            }
        }
    }
}
