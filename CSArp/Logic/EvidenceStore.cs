using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace CSArp.Logic
{
    internal sealed class EvidenceStore
    {
        private readonly HashSet<string> _evidenceFingerprints = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<IPAddress, HostRecord> _hostsByIp = new Dictionary<IPAddress, HostRecord>();
        private readonly object _sync = new object();
        public bool AddEvidence(EvidenceRecord evidence, IPAddress gatewayIp)
        {
            if (evidence.SourceIp == null)
            {
                return false;
            }

            lock (_sync)
            {
                var fingerprint = BuildFingerprint(evidence);
                if (!_evidenceFingerprints.Add(fingerprint))
                {
                    if (_hostsByIp.TryGetValue(evidence.SourceIp, out var existingHost) &&
                        evidence.TimestampUtc > existingHost.LastSeenUtc)
                    {
                        existingHost.LastSeenUtc = evidence.TimestampUtc;
                    }

                    return false;
                }

                if (!_hostsByIp.TryGetValue(evidence.SourceIp, out var host))
                {
                    host = new HostRecord
                    {
                        IPv4Address = evidence.SourceIp,
                        FirstSeenUtc = evidence.TimestampUtc,
                        LastSeenUtc = evidence.TimestampUtc
                    };

                    _hostsByIp.Add(evidence.SourceIp, host);
                }

                if (host.MacAddress != null && evidence.SourceMac != null && !host.MacAddress.Equals(evidence.SourceMac))
                {
                    host.Flags |= HostFlags.ConflictingIdentity | HostFlags.PossibleSpoofing;
                }

                if (host.MacAddress == null && evidence.SourceMac != null)
                {
                    host.MacAddress = evidence.SourceMac;
                }

                host.LastSeenUtc = evidence.TimestampUtc;
                host.DiscoveryMethods.Add(evidence.SourceMethod);
                host.IsGatewayCandidate = evidence.SourceIp.Equals(gatewayIp);

                if (evidence.PortHint.HasValue)
                {
                    host.OpenPortsHints.Add(evidence.PortHint.Value);
                }

                if (!string.IsNullOrWhiteSpace(evidence.HostnameHint))
                {
                    host.HostnameCandidates.Add(new HostnameCandidate(evidence.HostnameHint!.Trim(), evidence.SourceMethod, evidence.TimestampUtc));
                }

                var priorConfidence = host.ConfidenceScore;
                host.ConfidenceScore = ComputeConfidenceScore(host);
                return host.ConfidenceScore != priorConfidence;
            }
        }

        public IReadOnlyCollection<HostRecord> GetLowConfidenceHosts(int maxConfidence)
        {
            lock (_sync)
            {
                return _hostsByIp.Values.Where(h => h.ConfidenceScore <= maxConfidence).ToArray();
            }
        }

        public IReadOnlyCollection<HostRecord> Snapshot()
        {
            lock (_sync)
            {
                return _hostsByIp.Values
                    .OrderByDescending(h => h.ConfidenceScore)
                    .ThenBy(h => h.IPv4Address?.ToString(), StringComparer.Ordinal)
                    .ToArray();
            }
        }
        private static string BuildFingerprint(EvidenceRecord evidence)
            => string.Join("|",
                evidence.SourceMethod,
                evidence.SourceIp,
                evidence.DestinationIp,
                evidence.SourceMac,
                evidence.PortHint,
                evidence.HostnameHint,
                evidence.PayloadSummary);

        private static int ComputeConfidenceScore(HostRecord host)
        {
            var score = 0;

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.ArpActive))
            {
                score += 45;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.ArpPassive))
            {
                score += 20;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.Icmp))
            {
                score += 25;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.TcpSyn))
            {
                score += 25;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.Mdns))
            {
                score += 20;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.Nbns))
            {
                score += 20;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.Ssdp))
            {
                score += 20;
            }

            if (host.DiscoveryMethods.Contains(DiscoveryMethod.Llmnr))
            {
                score += 20;
            }

            if (host.DiscoveryMethods.Count >= 2)
            {
                score += 10;
            }

            if ((host.Flags & HostFlags.ConflictingIdentity) == HostFlags.ConflictingIdentity)
            {
                score -= 30;
            }

            if (host.DiscoveryMethods.SetEquals(new[] { DiscoveryMethod.ArpPassive }))
            {
                host.Flags |= HostFlags.SilentHost;
            }

            return Math.Max(0, Math.Min(100, score));
        }
    }
}