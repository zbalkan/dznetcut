using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CSArp.Logic
{
    public sealed class ClientDiscoveredEventArgs
    {
        public ClientDiscoveredEventArgs(IPAddress ipAddress, PhysicalAddress macAddress, bool isGateway, int confidenceScore = 0)
        {
            IpAddress = ipAddress;
            MacAddress = macAddress;
            IsGateway = isGateway;
            ConfidenceScore = confidenceScore;
        }

        public IPAddress IpAddress { get; }
        public PhysicalAddress MacAddress { get; }
        public bool IsGateway { get; }
        public int ConfidenceScore { get; }
    }

    public class NetworkScanner
    {
        private readonly Action<string> _log;
        private readonly object _stateLock = new object();
        private readonly object _reportedHostsLock = new object();
        private readonly HashSet<IPAddress> _reportedHostSet = new HashSet<IPAddress>();
        private readonly Dictionary<IPAddress, int> _lastReportedConfidenceByIp = new Dictionary<IPAddress, int>();
        private readonly ScanPolicyConfig _policy;

        private CancellationTokenSource? _scanCts;
        private PacketArrivalEventHandler? _backgroundHandler;
        private volatile bool _stopRequestedByUser;

        public NetworkScanner(Action<string>? log = null, ScanPolicyConfig? policy = null)
        {
            _log = log ?? (msg => Debug.Print(msg));
            _policy = policy ?? ScanPolicyConfig.Balanced;
        }

        public event Action<bool>? ScanStateChanged;

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

            _stopRequestedByUser = false;
            var subnet = networkAdapter.ReadCurrentSubnet();
            var adaptiveTimeoutSeconds = CalculateAdaptiveTimeoutSeconds(subnet);

            CancellationToken token;
            lock (_stateLock)
            {
                if (_scanCts != null && !_scanCts.IsCancellationRequested)
                {
                    return;
                }

                _scanCts?.Dispose();
                _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(adaptiveTimeoutSeconds));
                token = _scanCts.Token;
            }
            ScanStateChanged?.Invoke(true);

            ClearReportedHosts();
            _log($"Scan started on {networkAdapter.Interface?.FriendlyName ?? networkAdapter.Name} with gateway {gatewayIp}. Timeout={adaptiveTimeoutSeconds}s");
            statusProgress?.Report("Scan started...");
            scanProgress?.Report(0);

            _ = Task.Run(() => RunScanOrchestration(networkAdapter, gatewayIp, token, clientProgress, statusProgress, scanProgress));
        }


        private int CalculateAdaptiveTimeoutSeconds(IPV4Subnet subnet)
        {
            var hostCount = subnet.EnumerateHosts().Count();
            var averageJitterMs = (_policy.ArpMinJitterMs + _policy.ArpMaxJitterMs) / 2;
            var minInterPacketDelayMs = Math.Max(1, 1000 / Math.Max(1, _policy.ArpPacketsPerSecondCap));
            var arpSendMs = hostCount * (_policy.ArpRetries + 1) * (minInterPacketDelayMs + averageJitterMs);

            var estimatedSeconds = (int)Math.Ceiling((arpSendMs / 1000.0)
                + _policy.ArpForegroundCaptureSeconds
                + _policy.PassiveHoldSeconds
                + 8);

            return Math.Max(_policy.TotalTimeoutSeconds, Math.Min(estimatedSeconds, 180));
        }

        public void StopScan()
        {
            _stopRequestedByUser = true;
            lock (_stateLock)
            {
                _scanCts?.Cancel();
            }

        }

        private async Task RunScanOrchestration(
            LibPcapLiveDevice networkAdapter,
            IPAddress gatewayIp,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress,
            IProgress<int> scanProgress)
        {
            var evidenceStore = new EvidenceStore();
            _stopRequestedByUser = false;
            var subnet = networkAdapter.ReadCurrentSubnet();
            var sourceAddress = networkAdapter.ReadCurrentIpV4Address();

            networkAdapter.Filter = BuildCaptureFilter();
            StartPassiveCollector(networkAdapter, subnet, gatewayIp, evidenceStore, clientProgress, statusProgress);

            try
            {
                _log("Phase 1/6: ARP active sweep started");
                statusProgress?.Report("Phase 1/6: ARP sweep");
                await RunArpActiveSweep(networkAdapter, subnet, gatewayIp, sourceAddress, evidenceStore, cancellationToken, clientProgress, statusProgress).ConfigureAwait(false);
                scanProgress?.Report(35);

                var parallelPhases = new List<Task>();

                if (_policy.IcmpEnabled)
                {
                    _log("Phase 2/6: ICMP liveness started");
                    parallelPhases.Add(RunIcmpPhase(subnet, sourceAddress, gatewayIp, evidenceStore, cancellationToken, clientProgress, statusProgress));
                }

                if (_policy.TcpSynEnabled)
                {
                    _log("Phase 3/6: TCP spot checks started");
                    parallelPhases.Add(RunTcpSynPhase(sourceAddress, gatewayIp, evidenceStore, cancellationToken, clientProgress, statusProgress));
                }

                if (_policy.UdpDiscoveryEnabled)
                {
                    _log("Phase 4/6: UDP discovery started");
                    parallelPhases.Add(RunUdpDiscoveryPhase(cancellationToken));
                }

                if (parallelPhases.Count > 0)
                {
                    statusProgress?.Report("Phase 2-4/6: Active probes running in parallel");
                    await Task.WhenAll(parallelPhases).ConfigureAwait(false);
                }

                scanProgress?.Report(85);

                _log("Phase 5/6: Passive hold started");
                statusProgress?.Report("Phase 5/6: Passive hold");
                await Task.Delay(TimeSpan.FromSeconds(_policy.PassiveHoldSeconds), cancellationToken).ConfigureAwait(false);
                scanProgress?.Report(95);

                _log("Phase 6/6: Finalization started");
                statusProgress?.Report("Phase 6/6: Finalizing");
                PublishFinalSnapshot(evidenceStore, clientProgress, gatewayIp);
                scanProgress?.Report(100);
                var finalCount = evidenceStore.Snapshot().Count;
                _log($"Scan finished successfully. {finalCount} host(s) discovered.");
                statusProgress?.Report($"Scan completed: {finalCount} device(s) found");
            }
            catch (OperationCanceledException)
            {
                if (_stopRequestedByUser)
                {
                    _log("Scan canceled by user request.");
                    statusProgress?.Report("Scan canceled");
                }
                else
                {
                    _log("Scan timeout budget reached. Publishing partial results.");
                    PublishFinalSnapshot(evidenceStore, clientProgress, gatewayIp);
                    var partialCount = evidenceStore.Snapshot().Count;
                    statusProgress?.Report($"Scan timeout: {partialCount} device(s) found");
                    scanProgress?.Report(100);
                }
            }
            catch (PcapException ex)
            {
                _log($"PcapException during scan [{ex.Message}]");
                statusProgress?.Report("Refresh for scan");
            }
            catch (Exception ex)
            {
                _log($"Unhandled scan exception [{ex.Message}]");
            }
            finally
            {
                SafeStopCapture(networkAdapter);
                lock (_stateLock)
                {
                    _scanCts?.Dispose();
                    _scanCts = null;
                }
                ScanStateChanged?.Invoke(false);
            }
        }

        private void StartPassiveCollector(
            LibPcapLiveDevice networkAdapter,
            IPV4Subnet subnet,
            IPAddress gatewayIp,
            EvidenceStore evidenceStore,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            _backgroundHandler = (sender, e) =>
            {
                if (TryProcessPassivePacket(e.GetPacket(), subnet, gatewayIp, evidenceStore, out var updatedHost))
                {
                    ReportDiscoveredHost(updatedHost!, clientProgress, statusProgress, evidenceStore.Snapshot().Count);
                }
            };

            try
            {
                networkAdapter.OnPacketArrival += _backgroundHandler;
                networkAdapter.StartCapture();
                _log($"Passive collector started with filter: {networkAdapter.Filter}");
            }
            catch (Exception ex)
            {
                _log($"Exception while starting passive collector [{ex.Message}]");
            }
        }

        private async Task RunArpActiveSweep(
            LibPcapLiveDevice networkAdapter,
            IPV4Subnet subnet,
            IPAddress gatewayIp,
            IPAddress sourceAddress,
            EvidenceStore evidenceStore,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            if (networkAdapter.MacAddress == null)
            {
                _log("MAC address is null, cannot send ARP requests.");
                return;
            }

            AddEvidenceAndReport(
                evidenceStore,
                new EvidenceRecord(DateTime.UtcNow, DiscoveryMethod.ArpActive, sourceAddress, sourceAddress, networkAdapter.MacAddress, "Local adapter", 5),
                gatewayIp,
                clientProgress,
                statusProgress);

            var minInterPacketDelay = Math.Max(1, 1000 / Math.Max(1, _policy.ArpPacketsPerSecondCap));
            _log($"ARP sweep retries={_policy.ArpRetries}, pacing={_policy.ArpPacketsPerSecondCap} pkt/s cap");
            var random = new Random();

            for (var retry = 0; retry <= _policy.ArpRetries; retry++)
            {
                if (!sourceAddress.Equals(gatewayIp))
                {
                    SendArpRequest(networkAdapter, gatewayIp);
                    await Task.Delay(minInterPacketDelay + random.Next(_policy.ArpMinJitterMs, _policy.ArpMaxJitterMs + 1), cancellationToken).ConfigureAwait(false);
                }

                foreach (var targetIpAddress in subnet.EnumerateHosts())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (sourceAddress.Equals(targetIpAddress))
                    {
                        continue;
                    }

                    SendArpRequest(networkAdapter, targetIpAddress);
                    await Task.Delay(minInterPacketDelay + random.Next(_policy.ArpMinJitterMs, _policy.ArpMaxJitterMs + 1), cancellationToken).ConfigureAwait(false);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_policy.ArpForegroundCaptureSeconds), cancellationToken).ConfigureAwait(false);
        }

        private async Task RunIcmpPhase(
            IPV4Subnet subnet,
            IPAddress sourceAddress,
            IPAddress gatewayIp,
            EvidenceStore evidenceStore,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            var targets = evidenceStore.GetLowConfidenceHosts(79)
                .Select(h => h.IPv4Address)
                .OfType<IPAddress>()
                .Distinct()
                .ToList();

            if (!targets.Any())
            {
                targets = subnet.EnumerateHosts().Where(ip => !ip.Equals(sourceAddress)).Take(64).ToList();
            }

            using var ping = new Ping();
            _log($"ICMP phase target count: {targets.Count}");
            foreach (var targetIp in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var retry = 0; retry <= _policy.IcmpRetries; retry++)
                {
                    var reply = await ping.SendPingAsync(targetIp, _policy.IcmpTimeoutMs).ConfigureAwait(false);
                    if (reply.Status == IPStatus.Success)
                    {
                        AddEvidenceAndReport(
                            evidenceStore,
                            new EvidenceRecord(DateTime.UtcNow, DiscoveryMethod.Icmp, targetIp, sourceAddress, null, "ICMP echo reply", 25),
                            gatewayIp,
                            clientProgress,
                            statusProgress);
                        break;
                    }
                }
            }
        }

        private async Task RunTcpSynPhase(
            IPAddress sourceAddress,
            IPAddress gatewayIp,
            EvidenceStore evidenceStore,
            CancellationToken cancellationToken,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            var targets = evidenceStore.GetLowConfidenceHosts(79)
                .Select(h => h.IPv4Address)
                .OfType<IPAddress>()
                .Distinct()
                .Take(128);

            var tcpTargets = targets.ToList();
            _log($"TCP phase target count: {tcpTargets.Count}, ports=[{string.Join(",", _policy.TcpSynPorts)}]");
            foreach (var target in tcpTargets)
            {
                foreach (var port in _policy.TcpSynPorts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var client = new TcpClient();
                    try
                    {
                        var connectTask = client.ConnectAsync(target, port);
                        var completed = await Task.WhenAny(connectTask, Task.Delay(450, cancellationToken)).ConfigureAwait(false);
                        if (completed != connectTask)
                        {
                            continue;
                        }

                        await connectTask.ConfigureAwait(false);
                        AddEvidenceAndReport(
                            evidenceStore,
                            new EvidenceRecord(DateTime.UtcNow, DiscoveryMethod.TcpSyn, target, sourceAddress, null, "TCP connect success", 25, portHint: port),
                            gatewayIp,
                            clientProgress,
                            statusProgress);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                    {
                        AddEvidenceAndReport(
                            evidenceStore,
                            new EvidenceRecord(DateTime.UtcNow, DiscoveryMethod.TcpSyn, target, sourceAddress, null, "TCP refused", 20, portHint: port),
                            gatewayIp,
                            clientProgress,
                            statusProgress);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static async Task RunUdpDiscoveryPhase(CancellationToken cancellationToken)
        {
            using var udp = new UdpClient { EnableBroadcast = true };

            var ssdpRequest = Encoding.ASCII.GetBytes(
                "M-SEARCH * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nMAN:\"ssdp:discover\"\r\nMX:1\r\nST:ssdp:all\r\n\r\n");
            await udp.SendAsync(ssdpRequest, ssdpRequest.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900)).ConfigureAwait(false);

            var llmnrPayload = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            await udp.SendAsync(llmnrPayload, llmnrPayload.Length, new IPEndPoint(IPAddress.Parse("224.0.0.252"), 5355)).ConfigureAwait(false);

            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
        }

        private void PublishFinalSnapshot(EvidenceStore evidenceStore, IProgress<ClientDiscoveredEventArgs> clientProgress, IPAddress gatewayIp)
        {
            foreach (var host in evidenceStore.Snapshot())
            {
                if (host.IPv4Address == null || host.MacAddress == null)
                {
                    continue;
                }

                clientProgress?.Report(new ClientDiscoveredEventArgs(host.IPv4Address, host.MacAddress, host.IsGatewayCandidate || host.IPv4Address.Equals(gatewayIp), host.ConfidenceScore));
            }
        }

        private void AddEvidenceAndReport(
            EvidenceStore evidenceStore,
            EvidenceRecord evidence,
            IPAddress gatewayIp,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress)
        {
            if (!evidenceStore.AddEvidence(evidence, gatewayIp))
            {
                return;
            }

            if (!TryGetHostByIp(evidenceStore, evidence.SourceIp, out var host))
            {
                return;
            }

            ReportDiscoveredHost(host!, clientProgress, statusProgress, evidenceStore.Snapshot().Count);
        }


        private void ClearReportedHosts()
        {
            lock (_reportedHostsLock)
            {
                _reportedHostSet.Clear();
                _lastReportedConfidenceByIp.Clear();
            }
        }

        private bool TryRegisterHostReport(IPAddress ipAddress)
        {
            lock (_reportedHostsLock)
            {
                if (!_reportedHostSet.Add(ipAddress))
                {
                    return false;
                }

                return true;
            }
        }

        private bool ShouldEmitHostUpdate(IPAddress ipAddress, int confidenceScore, bool isNewHost)
        {
            lock (_reportedHostsLock)
            {
                if (isNewHost)
                {
                    _lastReportedConfidenceByIp[ipAddress] = confidenceScore;
                    return true;
                }

                if (!_lastReportedConfidenceByIp.TryGetValue(ipAddress, out var previousConfidence))
                {
                    _lastReportedConfidenceByIp[ipAddress] = confidenceScore;
                    return true;
                }

                if (confidenceScore <= previousConfidence)
                {
                    return false;
                }

                if (confidenceScore - previousConfidence < 5)
                {
                    return false;
                }

                _lastReportedConfidenceByIp[ipAddress] = confidenceScore;
                return true;
            }
        }

        private void ReportDiscoveredHost(
            HostRecord host,
            IProgress<ClientDiscoveredEventArgs> clientProgress,
            IProgress<string> statusProgress,
            int currentCount)
        {
            if (host.IPv4Address == null || host.MacAddress == null)
            {
                return;
            }

            var isNewHost = TryRegisterHostReport(host.IPv4Address);
            if (!ShouldEmitHostUpdate(host.IPv4Address, host.ConfidenceScore, isNewHost))
            {
                return;
            }

            clientProgress?.Report(new ClientDiscoveredEventArgs(host.IPv4Address, host.MacAddress, host.IsGatewayCandidate, host.ConfidenceScore));
            _log($"Host discovered {host.IPv4Address} @ {host.MacAddress.ToString("-")} confidence={host.ConfidenceScore} methods=[{string.Join(",", host.DiscoveryMethods.OrderBy(m => m.ToString()))}]");
            statusProgress?.Report($"{currentCount} device(s) found");
        }

        private bool TryProcessPassivePacket(
            RawCapture rawCapture,
            IPV4Subnet subnet,
            IPAddress gatewayIp,
            EvidenceStore evidenceStore,
            out HostRecord? updatedHost)
        {
            updatedHost = null;

            if (rawCapture?.Data == null || rawCapture.Data.Length == 0)
            {
                return false;
            }

            try
            {
                var packet = Packet.ParsePacket(LinkLayers.Ethernet, rawCapture.Data);

                var arpPacket = packet.Extract<ArpPacket>();
                if (arpPacket != null && !IPAddress.Any.Equals(arpPacket.SenderProtocolAddress) && subnet.Contains(arpPacket.SenderProtocolAddress))
                {
                    evidenceStore.AddEvidence(
                        new EvidenceRecord(DateTime.UtcNow, DiscoveryMethod.ArpPassive, arpPacket.SenderProtocolAddress, arpPacket.TargetProtocolAddress, arpPacket.SenderHardwareAddress, "Passive ARP", 20),
                        gatewayIp);
                    return TryGetHostByIp(evidenceStore, arpPacket.SenderProtocolAddress, out updatedHost);
                }

                var ipPacket = packet.Extract<IPv4Packet>();
                if (ipPacket == null || !subnet.Contains(ipPacket.SourceAddress))
                {
                    return false;
                }

                var method = DiscoveryMethod.ArpPassive;
                string? hostnameHint = null;

                var udpPacket = packet.Extract<UdpPacket>();
                if (udpPacket != null)
                {
                    if (udpPacket.SourcePort == 5353 || udpPacket.DestinationPort == 5353)
                    {
                        method = DiscoveryMethod.Mdns;
                    }
                    else if (udpPacket.SourcePort == 137 || udpPacket.DestinationPort == 137)
                    {
                        method = DiscoveryMethod.Nbns;
                    }
                    else if (udpPacket.SourcePort == 1900 || udpPacket.DestinationPort == 1900)
                    {
                        method = DiscoveryMethod.Ssdp;
                    }
                    else if (udpPacket.SourcePort == 5355 || udpPacket.DestinationPort == 5355)
                    {
                        method = DiscoveryMethod.Llmnr;
                    }

                    if (udpPacket.PayloadData != null && udpPacket.PayloadData.Length > 0)
                    {
                        hostnameHint = TryExtractAsciiToken(udpPacket.PayloadData);
                    }
                }

                evidenceStore.AddEvidence(
                    new EvidenceRecord(DateTime.UtcNow, method, ipPacket.SourceAddress, ipPacket.DestinationAddress, packet.Extract<EthernetPacket>()?.SourceHardwareAddress, "Passive traffic", 15, hostnameHint: hostnameHint),
                    gatewayIp);

                return TryGetHostByIp(evidenceStore, ipPacket.SourceAddress, out updatedHost);
            }
            catch (Exception ex)
            {
                Debug.Print($"Passive packet parse failed [{ex.Message}]");
                return false;
            }
        }

        private static string BuildCaptureFilter()
            => "arp or icmp or (udp and (port 5353 or port 5355 or port 137 or port 1900))";

        private void SafeStopCapture(LibPcapLiveDevice networkAdapter)
        {
            try
            {
                if (_backgroundHandler != null)
                {
                    networkAdapter.OnPacketArrival -= _backgroundHandler;
                }

                networkAdapter.StopCapture();
            }
            catch (Exception ex)
            {
                _log($"Exception while stopping capture [{ex.Message}]");
            }
            finally
            {
                _backgroundHandler = null;
            }
        }

        private static void SendArpRequest(LibPcapLiveDevice networkAdapter, IPAddress targetIpAddress)
        {
            var arpRequestPacket = new ArpPacket(ArpOperation.Request, "00-00-00-00-00-00".Parse(), targetIpAddress, networkAdapter.MacAddress, networkAdapter.ReadCurrentIpV4Address());
            var ethernetPacket = new EthernetPacket(networkAdapter.MacAddress, "FF-FF-FF-FF-FF-FF".Parse(), EthernetType.Arp)
            {
                PayloadPacket = arpRequestPacket
            };

            networkAdapter.SendPacket(ethernetPacket);
        }

        private static string? TryExtractAsciiToken(byte[] bytes)
        {
            try
            {
                var text = Encoding.ASCII.GetString(bytes);
                var token = text
                    .Split(new[] { '\0', '\r', '\n', ' ', '\t', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(segment => segment.Length > 2 && segment.All(ch => ch >= 32 && ch <= 126));
                return token;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetHostByIp(EvidenceStore evidenceStore, IPAddress? ipAddress, out HostRecord? host)
        {
            host = ipAddress == null
                ? null
                : evidenceStore.Snapshot().FirstOrDefault(h => Equals(h.IPv4Address, ipAddress));
            return host != null;
        }
    }
}
