using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace CSArp.Model
{
    public sealed class ArpTable
    {
        public static ArpTable Instance {
            get {
                return lazy.Value;
            }
        }

        public int Count { get { return _dictionary.Count; } }

        private static readonly Lazy<ArpTable> lazy = new Lazy<ArpTable>(() => new ArpTable());

        private readonly ConcurrentDictionary<IPAddress, PhysicalAddress> _dictionary;

        private ArpTable()
        {
            _dictionary = new ConcurrentDictionary<IPAddress, PhysicalAddress>();
        }

        public void Add(IPAddress ipAddress, PhysicalAddress physicalAddress)
        {
            _ = _dictionary.TryAdd(ipAddress, physicalAddress);
        }

        public bool ContainsKey(IPAddress ipAddress)
        {
            return _dictionary.ContainsKey(ipAddress);
        }

        public void Clear()
        {
            _dictionary.Clear();
        }
    }
}