using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace CSArp.Logic
{
    internal enum DiscoveryMethod
    {
        ArpActive,
        ArpPassive,
        Icmp,
        TcpSyn,
        Mdns,
        Nbns,
        Ssdp,
        Llmnr
    }

    [Flags]
    internal enum HostFlags
    {
        None = 0,
        ConflictingIdentity = 1,
        PossibleSpoofing = 2,
        SilentHost = 4
    }

    internal sealed class HostnameCandidate
    {
        public HostnameCandidate(string name, DiscoveryMethod sourceMethod, DateTime timestampUtc)
        {
            Name = name;
            SourceMethod = sourceMethod;
            TimestampUtc = timestampUtc;
        }

        public string Name { get; }
        public DiscoveryMethod SourceMethod { get; }
        public DateTime TimestampUtc { get; }
    }

    internal sealed class HostRecord
    {
        public HostRecord()
        {
            DiscoveryMethods = new HashSet<DiscoveryMethod>();
            HostnameCandidates = new List<HostnameCandidate>();
            OpenPortsHints = new HashSet<int>();
            Flags = HostFlags.None;
        }

        public IPAddress? IPv4Address { get; set; }
        public PhysicalAddress? MacAddress { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public HashSet<DiscoveryMethod> DiscoveryMethods { get; }
        public int ConfidenceScore { get; set; }
        public bool IsGatewayCandidate { get; set; }
        public List<HostnameCandidate> HostnameCandidates { get; }
        public HashSet<int> OpenPortsHints { get; }
        public HostFlags Flags { get; set; }

        public string? PreferredHostname => HostnameCandidates
            .OrderByDescending(c => c.TimestampUtc)
            .Select(c => c.Name)
            .FirstOrDefault();
    }

    internal sealed class EvidenceRecord
    {
        public EvidenceRecord(
            DateTime timestampUtc,
            DiscoveryMethod sourceMethod,
            IPAddress? sourceIp,
            IPAddress? destinationIp,
            PhysicalAddress? sourceMac,
            string payloadSummary,
            int qualityWeight,
            int? portHint = null,
            string? hostnameHint = null)
        {
            TimestampUtc = timestampUtc;
            SourceMethod = sourceMethod;
            SourceIp = sourceIp;
            DestinationIp = destinationIp;
            SourceMac = sourceMac;
            PayloadSummary = payloadSummary;
            QualityWeight = qualityWeight;
            PortHint = portHint;
            HostnameHint = hostnameHint;
        }

        public DateTime TimestampUtc { get; }
        public DiscoveryMethod SourceMethod { get; }
        public IPAddress? SourceIp { get; }
        public IPAddress? DestinationIp { get; }
        public PhysicalAddress? SourceMac { get; }
        public string PayloadSummary { get; }
        public int QualityWeight { get; }
        public int? PortHint { get; }
        public string? HostnameHint { get; }
    }

    public sealed class ScanPolicyConfig
    {
        public static ScanPolicyConfig Conservative => new ScanPolicyConfig();

        public static ScanPolicyConfig Balanced => new ScanPolicyConfig
        {
            UdpDiscoveryEnabled = true,
            TcpSynEnabled = false
        };

        public static ScanPolicyConfig Aggressive => new ScanPolicyConfig
        {
            UdpDiscoveryEnabled = true,
            TcpSynEnabled = true
        };

        public int TotalTimeoutSeconds { get; set; } = 30;
        public int ArpRetries { get; set; } = 2;
        public int ArpMinJitterMs { get; set; } = 10;
        public int ArpMaxJitterMs { get; set; } = 40;
        public int ArpForegroundCaptureSeconds { get; set; } = 10;
        public int ArpPacketsPerSecondCap { get; set; } = 450;
        public bool IcmpEnabled { get; set; } = true;
        public int IcmpTimeoutMs { get; set; } = 400;
        public int IcmpRetries { get; set; } = 1;
        public bool TcpSynEnabled { get; set; }
        public int[] TcpSynPorts { get; set; } = { 443, 80, 22, 3389 };
        public bool UdpDiscoveryEnabled { get; set; } = true;
        public int PassiveHoldSeconds { get; set; } = 12;
    }
}
