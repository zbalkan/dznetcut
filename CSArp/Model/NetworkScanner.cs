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
    public class NetworkScanner
    {
        private readonly ConcurrentDictionary<IPAddress, PhysicalAddress> _arpTable = new ConcurrentDictionary<IPAddress, PhysicalAddress>();
        private CancellationTokenSource _scanCts;
        private PacketArrivalEventHandler _backgroundHandler;
        private LibPcapLiveDevice _backgroundAdapter;
        private volatile bool _isScanning;

        public bool IsScanning => _isScanning;

        public void StartScan(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            Action onScanStarting,
            Action<IPAddress, PhysicalAddress, bool> onClientFound,
            Action<string> onStatusChanged,
            Action<int> onProgressChanged)
        {
            if (_isScanning)
            {
                return;
            }

            _scanCts = new CancellationTokenSource();
            _arpTable.Clear();
            _isScanning = true;

            onScanStarting?.Invoke();
            onStatusChanged?.Invoke("Please wait...");
            onProgressChanged?.Invoke(0);

            _ = Task.Run(() => StartForegroundScan(networkAdapter, gatewayIp, onClientFound, onStatusChanged, onProgressChanged, _scanCts.Token));
        }

        public void StopScan()
        {
            _isScanning = false;
            _scanCts?.Cancel();
            if (_backgroundAdapter != null && _backgroundHandler != null)
            {
                _backgroundAdapter.OnPacketArrival -= _backgroundHandler;
                _backgroundHandler = null;
            }
        }

        private async Task StartForegroundScan(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            Action<IPAddress, PhysicalAddress, bool> onClientFound,
            Action<string> onStatusChanged,
            Action<int> onProgressChanged,
            CancellationToken cancellationToken)
        {
            var subnet = networkAdapter.ReadCurrentSubnet();
            networkAdapter.Filter = "arp";

            var sendTask = Task.Run(() => InitiateArpRequestQueue(networkAdapter, gatewayIp, onClientFound, cancellationToken), cancellationToken);

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
                        await Task.Delay(20, cancellationToken);
                        continue;
                    }

                    if (!TryExtractArpPacket(packetCapture, out var arpPacket))
                    {
                        continue;
                    }

                    ProcessPacket(arpPacket, subnet, gatewayIp, onClientFound, onStatusChanged);
                }

                await sendTask;

                if (!cancellationToken.IsCancellationRequested)
                {
                    onStatusChanged?.Invoke($"{_arpTable.Count} device(s) found");
                    onProgressChanged?.Invoke(100);
                    StartBackgroundScan(networkAdapter, gatewayIp, onClientFound, onStatusChanged, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("PcapException at foreground scan [" + ex.Message + "]");
                onStatusChanged?.Invoke("Refresh for scan");
                onProgressChanged?.Invoke(0);
            }
            catch (Exception ex)
            {
                DebugOutput.Print(ex.Message);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _isScanning = false;
                }
            }
        }

        private void StartBackgroundScan(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            Action<IPAddress, PhysicalAddress, bool> onClientFound,
            Action<string> onStatusChanged,
            CancellationToken cancellationToken)
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

                    ProcessPacket(arpPacket, subnet, gatewayIp, onClientFound, onStatusChanged);
                };
                networkAdapter.OnPacketArrival += _backgroundHandler;

                networkAdapter.StartCapture();
            }
            catch (Exception ex)
            {
                DebugOutput.Print("Exception at background scan start [" + ex.Message + "]");
            }
        }

        private void InitiateArpRequestQueue(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            Action<IPAddress, PhysicalAddress, bool> onClientFound,
            CancellationToken cancellationToken)
        {
            try
            {
                var subnet = networkAdapter.ReadCurrentSubnet();
                var sourceAddress = networkAdapter.ReadCurrentIpV4Address();

                if (_arpTable.TryAdd(sourceAddress, networkAdapter.MacAddress))
                {
                    onClientFound?.Invoke(sourceAddress, networkAdapter.MacAddress, false);
                }

                if (!sourceAddress.Equals(gatewayIp) && !cancellationToken.IsCancellationRequested)
                {
                    SendArpRequest(networkAdapter, gatewayIp);
                }

                var targets = subnet.EnumerateHosts();
                foreach (var targetIpAddress in targets)
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

        private void ProcessPacket(
            ArpPacket arpPacket,
            IPV4Subnet subnet,
            IPAddress gatewayIp,
            Action<IPAddress, PhysicalAddress, bool> onClientFound,
            Action<string> onStatusChanged)
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
            onClientFound?.Invoke(arpPacket.SenderProtocolAddress, arpPacket.SenderHardwareAddress, isGateway);
            onStatusChanged?.Invoke(_arpTable.Count + " device(s) found");
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
