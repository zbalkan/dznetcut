using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using CSArp.Model.Extensions;
using CSArp.Model.Utilities;
using CSArp.View;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

/*
 Reference:
 http://stackoverflow.com/questions/14114971/sending-my-own-arp-packet-using-sharppcap-and-packet-net
 https://www.codeproject.com/Articles/12458/SharpPcap-A-Packet-Capture-Framework-for-NET
*/

namespace CSArp.Model
{
    // TODO: Add a scanning bool, to set the state for cancellation.
    // TODO: Remove GUI related code out of the class.
    public class NetworkScanner
    {
        private const string prefix = "Scan";
        private volatile bool scanning = false;

        public bool IsScanning => scanning;

        /// <summary>
        /// Populates listview with machines connected to the LAN
        /// </summary>
        /// <param name="view"></param>
        /// <param name="networkAdapter"></param>
        public void StartScan(ScannerForm form, LibPcapLiveDevice networkAdapter, IPAddress gatewayIp)
        {
            DebugOutput.Print("Refresh client list");

            #region initialization

            _ = form.Invoke(new Action(() => form.ToolStripStatusScan.Text = "Please wait..."));
            _ = form.Invoke(new Action(() => form.ToolStripProgressBarScan.Value = 0));
            _ = form.ClientListView.Invoke(new Action(() => form.ClientListView.Items.Clear()));

            #endregion initialization

            // Change state
            scanning = true;

            // Clear ARP table
            ArpTable.Instance.Clear();

            // Start Foreground Scan with Timeout involved
            StartForegroundScan(form, networkAdapter, gatewayIp, 5000);
        }

        private void StartForegroundScan(ScannerForm form, LibPcapLiveDevice networkAdapter, IPAddress gatewayIp, int foregroundScanTimeout)
        {
            // Obtain subnet information
            var subnet = networkAdapter.ReadCurrentSubnet();

            // Obtain current IP address
            var sourceAddress = networkAdapter.ReadCurrentIpV4Address();

            // TODO: Send and capture ICMP packages for both MAC address and alive status.

            #region Sending ARP requests to probe for all possible IP addresses on LAN

            ThreadBuffer.AddWithPrefix(new Thread(() => {
                InitiateArpRequestQueue(form, networkAdapter, gatewayIp);
            }),
            prefix);

            #endregion Sending ARP requests to probe for all possible IP addresses on LAN

            #region Retrieving ARP packets floating around and finding out the senders' IP and MACs

            networkAdapter.Filter = "arp";
            ThreadBuffer.AddWithPrefix(new Thread(() => {
                try
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    while ((networkAdapter.GetNextPacket(out var rawcapture) == GetPacketStatus.PacketRead) && stopwatch.ElapsedMilliseconds <= foregroundScanTimeout && scanning)
                    {
                        if (!TryExtractArpPacket(rawcapture, out var arppacket))
                        {
                            continue;
                        }

                        if (!ArpTable.Instance.ContainsKey(arppacket.SenderProtocolAddress) && arppacket.SenderProtocolAddress.ToString() != "0.0.0.0" && subnet.Contains(arppacket.SenderProtocolAddress))
                        {
                            var isGateway = false;
                            if (arppacket.SenderProtocolAddress.Equals(gatewayIp))
                            {
                                DebugOutput.Print("Found gateway!");
                                isGateway = true;
                            }
                            DebugOutput.Print("Added " + arppacket.SenderProtocolAddress.ToString() + " @ " + arppacket.SenderHardwareAddress.ToString("-"));
                            ArpTable.Instance.Add(arppacket.SenderProtocolAddress, arppacket.SenderHardwareAddress);
                            _ = form.ClientListView.Invoke(new Action(() => {
                                _ = isGateway
                                ? form.ClientListView.Items.Add(new ListViewItem(new string[] { arppacket.SenderProtocolAddress.ToString(), arppacket.SenderHardwareAddress.ToString("-"), "On", "GATEWAY" }))
                                : form.ClientListView.Items.Add(new ListViewItem(new string[] { arppacket.SenderProtocolAddress.ToString(), arppacket.SenderHardwareAddress.ToString("-"), "On", ApplicationSettings.GetSavedClientNameFromMAC(arppacket.SenderHardwareAddress.ToString("-")) }));
                            }));
                            //Debug.Print("{0} @ {1}", arppacket.SenderProtocolAddress, arppacket.SenderHardwareAddress);
                        }
                        //int percentageprogress = (int)((float)stopwatch.ElapsedMilliseconds / scanduration * 100);
                        //form.Invoke(new Action(() => form.ToolStripStatusScan.Text = "Scanning " + percentageprogress + "%"));
                        //form.Invoke(new Action(() => form.ToolStripProgressBarScan.Value = percentageprogress));
                        //Debug.Print(packet.ToString() + "\n");
                    }
                    stopwatch.Stop();
                    _ = form.Invoke(new Action(() => form.ToolStripStatusScan.Text = ArpTable.Instance.Count.ToString() + " device(s) found"));
                    _ = form.Invoke(new Action(() => form.ToolStripProgressBarScan.Value = 100));
                    StartBackgroundScan(form, networkAdapter, gatewayIp); //start passive monitoring
                }
                catch (PcapException ex)
                {
                    DebugOutput.Print("PcapException @ GetClientList.StartForegroundScan() @ new Thread(()=>{}) while retrieving packets [" + ex.Message + "]");
                    _ = form.Invoke(new Action(() => form.ToolStripStatusScan.Text = "Refresh for scan"));
                    _ = form.Invoke(new Action(() => form.ToolStripProgressBarScan.Value = 0));
                }
                catch (Exception ex)
                {
                    DebugOutput.Print(ex.Message);
                }
            }),
            prefix);

            #endregion Retrieving ARP packets floating around and finding out the senders' IP and MACs
        }

        /// <summary>
        /// Actively monitor ARP packets for signs of new clients after StartForegroundScan active scan is done
        /// </summary>
        private void StartBackgroundScan(ScannerForm form, LibPcapLiveDevice networkAdapter, IPAddress gatewayIp)
        {
            try
            {
                #region Sending ARP requests to probe for all possible IP addresses on LAN

                ThreadBuffer.AddWithPrefix(new Thread(() => {
                    InitiateArpRequestQueue(form, networkAdapter, gatewayIp);
                }),
                prefix);

                #endregion Sending ARP requests to probe for all possible IP addresses on LAN

                #region Assign OnPacketArrival event handler and start capturing

                networkAdapter.OnPacketArrival += (sender, e) => {
                    ParseArpResponse(form, networkAdapter.ReadCurrentSubnet(), gatewayIp, e);
                };

                #endregion Assign OnPacketArrival event handler and start capturing

                networkAdapter.StartCapture();
            }
            catch (Exception ex)
            {
                DebugOutput.Print("Exception at GetClientList.BackgroundScanStart() [" + ex.Message + "]");
            }
        }

        public void StopScan()
        {
            scanning = false; // Prevent new packets
            ThreadBuffer.StopThreadByPrefix(prefix); // kill existing threads
        }

        // TODO: Start spoofing for devices regarding online status.
        private void InitiateArpRequestQueue(ScannerForm form, LibPcapLiveDevice networkAdapter, IPAddress gatewayIp)
        {
            try
            {
                // Obtain subnet information
                var subnet = networkAdapter.ReadCurrentSubnet();

                // Obtain current IP address
                var sourceAddress = networkAdapter.ReadCurrentIpV4Address();

                // Add local host statically.
                ArpTable.Instance.Add(sourceAddress, networkAdapter.MacAddress);
                _ = form.ClientListView.Invoke(new Action(() => {
                    _ = form.ClientListView.Items.Add(
                        new ListViewItem(new string[]
                        {
                            networkAdapter.ReadCurrentIpV4Address().ToString(),
                            networkAdapter.MacAddress.ToString("-"),
                            "On",
                            ApplicationSettings.GetSavedClientNameFromMAC(networkAdapter.MacAddress.ToString("-"))
                        }));
                }));

                // Ensure the ARP request is sent to gateway first.
                if (!sourceAddress.Equals(gatewayIp) && scanning)
                {
                    SendArpRequest(networkAdapter, gatewayIp);
                }

                // Send requests sequentially in this worker thread.
                // Avoid materializing the full subnet as a list to prevent out-of-memory on larger networks.
                foreach (var targetIpAddress in subnet.ToList())
                {
                    if (!scanning || sourceAddress.Equals(targetIpAddress) || gatewayIp.Equals(targetIpAddress))
                    {
                        continue;
                    }
                    SendArpRequest(networkAdapter, targetIpAddress);
                }
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("PcapException @ GetClientList.InitiateArpRequestQueue() probably due to capturedevice being closed by refreshing or by exiting application [" + ex.Message + "]");
            }
            catch (OutOfMemoryException ex)
            {
                DebugOutput.Print($"PcapException @ GetClientList.InitiateArpRequestQueue() out of memory. \nTotal number of threads {ThreadBuffer.Count}\nTotal number of alive threads {ThreadBuffer.AliveCount}\n[" + ex.Message + "]");
            }
            catch (Exception ex)
            {
                DebugOutput.Print("Exception at GetClientList.InitiateArpRequestQueue() inside new Thread(()=>{}) while sending packets [" + ex.Message + "]");
            }
        }

        private void SendArpRequest(LibPcapLiveDevice networkAdapter, IPAddress targetIpAddress)
        {
            var arprequestpacket = new ArpPacket(ArpOperation.Request, "00-00-00-00-00-00".Parse(), targetIpAddress, networkAdapter.MacAddress, networkAdapter.ReadCurrentIpV4Address());
            var ethernetpacket = new EthernetPacket(networkAdapter.MacAddress, "FF-FF-FF-FF-FF-FF".Parse(), EthernetType.Arp);
            ethernetpacket.PayloadPacket = arprequestpacket;
            networkAdapter.SendPacket(ethernetpacket);
            Debug.WriteLine("ARP request is sent to: {0}", targetIpAddress);
        }

        private void ParseArpResponse(ScannerForm form, IPV4Subnet subnet, IPAddress gatewayIp, SharpPcap.PacketCapture e)
        {
            if (!TryExtractArpPacket(e, out var arppacket))
            {
                return;
            }

            if (!ArpTable.Instance.ContainsKey(arppacket.SenderProtocolAddress) && arppacket.SenderProtocolAddress.ToString() != "0.0.0.0" && subnet.Contains(arppacket.SenderProtocolAddress) && scanning)
            {
                var isGateway = false;
                if (arppacket.SenderProtocolAddress.Equals(gatewayIp))
                {
                    DebugOutput.Print("Found gateway!");
                    isGateway = true;
                }
                DebugOutput.Print("Added " + arppacket.SenderProtocolAddress.ToString() + " @ " + arppacket.SenderHardwareAddress.ToString("-") + " from background scan!");
                ArpTable.Instance.Add(arppacket.SenderProtocolAddress, arppacket.SenderHardwareAddress);
                _ = form.ClientListView.Invoke(new Action(() => {
                    _ = isGateway
                        ? form.ClientListView.Items.Add(new ListViewItem(new string[] { arppacket.SenderProtocolAddress.ToString(), arppacket.SenderHardwareAddress.ToString("-"), "On", "GATEWAY" }))
                        : form.ClientListView.Items.Add(new ListViewItem(new string[] { arppacket.SenderProtocolAddress.ToString(), arppacket.SenderHardwareAddress.ToString("-"), "On", ApplicationSettings.GetSavedClientNameFromMAC(arppacket.SenderHardwareAddress.ToString("-")) }));
                }));
                _ = form.Invoke(new Action(() => form.ToolStripStatusScan.Text = ArpTable.Instance.Count + " device(s) found"));
            }
        }

        private static bool TryExtractArpPacket(PacketCapture e, out ArpPacket arppacket)
        {
            arppacket = null;
            RawCapture rawcapture = e.GetPacket();
            if (rawcapture is null || rawcapture.Data == null || rawcapture.Data.Length == 0)
            {
                return false;
            }

            try
            {
                var packet = Packet.ParsePacket(LinkLayers.Ethernet, rawcapture.Data);
                arppacket = packet.Extract<ArpPacket>();
                return arppacket != null;
            }
            catch (IndexOutOfRangeException ex)
            {
                DebugOutput.Print("IndexOutOfRangeException while parsing a capture packet. Packet ignored. [" + ex.Message + "]");
                return false;
            }
            catch (ArgumentException ex)
            {
                DebugOutput.Print("ArgumentException while parsing a capture packet. Packet ignored. [" + ex.Message + "]");
                return false;
            }
        }
    }
}