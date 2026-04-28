using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
    public sealed class ClientDiscoveredEventArgs : EventArgs
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
        private readonly ConcurrentDictionary<IPAddress, PhysicalAddress> _arpTable = new ConcurrentDictionary<IPAddress, PhysicalAddress>();
        private readonly object _stateLock = new object();

        private CancellationTokenSource _scanCts;
        private PacketArrivalEventHandler _backgroundHandler;
        private LibPcapLiveDevice _backgroundAdapter;

        public event EventHandler ScanStarting;

        public event EventHandler<ClientDiscoveredEventArgs> ClientFound;

        public event EventHandler<string> StatusChanged;

        public event EventHandler<int> ProgressChanged;

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

        public void StartScan(LibPcapLiveDevice networkAdapter, IPAddress gatewayIp)
        {
            if (networkAdapter == null)
            {
                throw new ArgumentNullException(nameof(networkAdapter));
            }

            if (gatewayIp == null)
            {
                throw new ArgumentNullException(nameof(gatewayIp));
            }

            lock (_stateLock)
            {
                if (_scanCts != null && !_scanCts.IsCancellationRequested)
                {
                    return;
                }

                _scanCts?.Dispose();
                _scanCts = new CancellationTokenSource();
            }

            _arpTable.Clear();
            ScanStarting?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, "Please wait...");
            ProgressChanged?.Invoke(this, 0);

            _ = Task.Run(() => StartForegroundScan(networkAdapter, gatewayIp, _scanCts.Token));
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

        private async Task StartForegroundScan(LibPcapLiveDevice networkAdapter, IPAddress gatewayIp, CancellationToken cancellationToken)
        {
            var subnet = networkAdapter.ReadCurrentSubnet();
            networkAdapter.Filter = "arp";

            var sendTask = Task.Run(() => InitiateArpRequestQueue(networkAdapter, gatewayIp, cancellationToken), cancellationToken);

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

                    ProcessPacket(arpPacket, subnet, gatewayIp);
                }

                await sendTask.ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    DebugOutput.Print("Discovery task finished. " + _arpTable.Count + " device(s) discovered.");
                    StatusChanged?.Invoke(this, $"{_arpTable.Count} device(s) found");
                    ProgressChanged?.Invoke(this, 100);
                    StartBackgroundScan(networkAdapter, gatewayIp, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("PcapException at foreground scan [" + ex.Message + "]");
                StatusChanged?.Invoke(this, "Refresh for scan");
                ProgressChanged?.Invoke(this, 0);
            }
            catch (Exception ex)
            {
                DebugOutput.Print(ex.Message);
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

        private void StartBackgroundScan(LibPcapLiveDevice networkAdapter, IPAddress gatewayIp, CancellationToken cancellationToken)
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

                    ProcessPacket(arpPacket, subnet, gatewayIp);
                };
                networkAdapter.OnPacketArrival += _backgroundHandler;

                networkAdapter.StartCapture();
            }
            catch (Exception ex)
            {
                DebugOutput.Print("Exception at background scan start [" + ex.Message + "]");
            }
        }

        private void InitiateArpRequestQueue(LibPcapLiveDevice networkAdapter, IPAddress gatewayIp, CancellationToken cancellationToken)
        {
            try
            {
                var subnet = networkAdapter.ReadCurrentSubnet();
                var sourceAddress = networkAdapter.ReadCurrentIpV4Address();

                if (_arpTable.TryAdd(sourceAddress, networkAdapter.MacAddress))
                {
                    ClientFound?.Invoke(this, new ClientDiscoveredEventArgs(sourceAddress, networkAdapter.MacAddress, false));
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
                DebugOutput.Print("PcapException at InitiateArpRequestQueue [" + ex.Message + "]");
            }
            catch (Exception ex)
            {
                DebugOutput.Print("Exception at InitiateArpRequestQueue [" + ex.Message + "]");
            }
        }

        private void ProcessPacket(ArpPacket arpPacket, IPV4Subnet subnet, IPAddress gatewayIp)
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
                DebugOutput.Print("Found gateway!");
            }

            DebugOutput.Print("Added " + arpPacket.SenderProtocolAddress + " @ " + arpPacket.SenderHardwareAddress.ToString("-"));
            ClientFound?.Invoke(this, new ClientDiscoveredEventArgs(arpPacket.SenderProtocolAddress, arpPacket.SenderHardwareAddress, isGateway));
            StatusChanged?.Invoke(this, _arpTable.Count + " device(s) found");
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
                DebugOutput.Print("IndexOutOfRangeException while parsing a packet [" + ex.Message + "]");
                return false;
            }
            catch (ArgumentException ex)
            {
                DebugOutput.Print("ArgumentException while parsing a packet [" + ex.Message + "]");
                return false;
            }
        }
    }
}
