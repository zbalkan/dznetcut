using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows.Forms;
using CSArp.Model;
using CSArp.Model.Utilities;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CSArp.View
{
    public partial class ScannerForm : Form
    {
        private readonly Spoofer _arpSpoofer;
        private readonly NetworkScanner _networkScanner;
        private IPAddress gatewayIpAddress;
        private PhysicalAddress gatewayPhysicalAddress;
        private GatewayIPAddressInformation gatewayInfo;
        private LibPcapLiveDevice selectedDevice;
        private string selectedInterfaceFriendlyName;

        public ScannerForm()
        {
            InitializeComponent();
            ThreadBuffer.Init();
            _arpSpoofer = new Spoofer();
            _networkScanner = new NetworkScanner();
            DebugOutput.Init(richTextBoxLog);
        }

        public ListView ClientListView => clientListView;
        public ToolStripStatusLabel ToolStripStatusScan => toolStripStatusScan;
        public ToolStripProgressBar ToolStripProgressBarScan => toolStripProgressBarScan;

        private string SelectedInterfaceFriendlyName {
            get {
                return selectedInterfaceFriendlyName;
            }
            set {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(value));
                }

                selectedInterfaceFriendlyName = value;
                if (selectedDevice != null && selectedDevice.Opened)
                {
                    try
                    {
                        selectedDevice.StopCapture();
                        selectedDevice.Close();
                    }
                    catch (PcapException ex)
                    {
                        DebugOutput.Print("Exception at StartForegroundScan while trying to capturedevice.StopCapture() or capturedevice.Close() [" + ex.Message + "]");
                    }
                }

                selectedDevice = NetworkAdapterManager.WinPcapDevices.Where(dev => dev.Interface.FriendlyName != null)
                                                                     .FirstOrDefault(dev => dev.Interface.FriendlyName.Equals(selectedInterfaceFriendlyName));
            }
        }

        private void StartNetworkScan()
        {
            if (string.IsNullOrEmpty(SelectedInterfaceFriendlyName))
            {
                _ = MessageBox.Show("Please select a network interface!", "Interface", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (_networkScanner.IsScanning)
            {
                return;
            }

            _arpSpoofer.StopAll();
            _ = BeginInvoke(new Action(() => { toolStripStatus.Text = "Ready"; }));
            _networkScanner.StartScan(this, selectedDevice, gatewayIpAddress);
        }

        private void StopNetworkScan()
        {
            _networkScanner.StopScan();
            StopCapture();
        }

        private void DisconnectSelectedClients()
        {
            if (clientListView.SelectedItems.Count == 0)
            {
                return;
            }

            foreach (ListViewItem item in clientListView.Items)
            {
                if (item.SubItems[0].Text == gatewayIpAddress.ToString())
                {
                    gatewayPhysicalAddress = item.SubItems[1].Text.Parse();
                }
            }

            if (gatewayPhysicalAddress == null)
            {
                _ = MessageBox.Show("Gateway Physical Address still undiscovered. Please wait and try again.", "Warning", MessageBoxButtons.OK);
                return;
            }

            _ = Invoke(new Action(() => { toolStripStatus.Text = "Arpspoofing active..."; }));

            var targetlist = new Dictionary<IPAddress, PhysicalAddress>();
            var parseindex = 0;
            foreach (ListViewItem listitem in clientListView.SelectedItems)
            {
                targetlist.Add(IPAddress.Parse(listitem.SubItems[0].Text), listitem.SubItems[1].Text.Parse());
                _ = BeginInvoke(new Action(() => {
                    clientListView.SelectedItems[parseindex++].SubItems[2].Text = "Off";
                }));
            }
            _arpSpoofer.Start(targetlist, gatewayIpAddress, gatewayPhysicalAddress, selectedDevice);
        }

        private void ReconnectClients()
        {
            _arpSpoofer.StopAll();
            foreach (ListViewItem entry in clientListView.Items)
            {
                entry.SubItems[2].Text = "On";
            }
            toolStripStatus.Text = "Stopped";
        }

        private void GetGatewayInformation()
        {
            gatewayInfo = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => i.Name == SelectedInterfaceFriendlyName)?
                .GetIPProperties()
                .GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            gatewayIpAddress = gatewayInfo?.Address;
        }

        private void StartCapture()
        {
            var conf = new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                ReadTimeout = 1000
            };
            selectedDevice.Open(conf);
        }

        public void StopCapture()
        {
            if (selectedDevice != null && selectedDevice.Opened)
            {
                try
                {
                    selectedDevice.StopCapture();
                    selectedDevice.Close();
                }
                catch (Exception)
                {
                    // ignore exceptions on close
                }
            }
        }

        #region Event based methods

        private void toolStripMenuItemRefreshClients_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(toolStripComboBoxDevicelist.Text))
            {
                _ = MessageBox.Show("Pick a device before a scan.");
            }
            else
            {
                if (_networkScanner.IsScanning)
                {
                    toolStripStatus.Text = "A scan is already running.";
                    return;
                }

                SelectedInterfaceFriendlyName = toolStripComboBoxDevicelist.Text;
                GetGatewayInformation();
                StartCapture();
                StartNetworkScan();
            }
        }

        private void aboutCSArpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _ = MessageBox.Show("Author : globalpolicy\nContact : yciloplabolg@gmail.com\nBlog : c0dew0rth.blogspot.com\nGithub : globalpolicy\nContributions are welcome!\n\nContributors:\nZafer Balkan : zafer@zaferbalkan.com", "About CSArp", MessageBoxButtons.OK);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            EnumerateNetworkAdaptersforMenu();
            SetSavedInterface();
        }

        private void cutoffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisconnectSelectedClients();
        }

        private void reconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReconnectClients();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                Hide();
            }
        }

        private void toolStripTextBoxClientName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (clientListView.SelectedItems.Count == 1)
                {
                    clientListView.SelectedItems[0].SubItems[3].Text = toolStripTextBoxClientName.Text;
                    toolStripTextBoxClientName.Text = "";
                }
            }
        }

        private void toolStripMenuItemMinimize_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void toolStripMenuItemSaveSettings_Click(object sender, EventArgs e)
        {
            if (ApplicationSettings.SaveSettings(clientListView, toolStripComboBoxDevicelist.Text))
            {
                toolStripStatus.Text = "Settings saved!";
            }
        }

        private void showLogToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (showLogToolStripMenuItem.Checked == false)
            {
                richTextBoxLog.Visible = false;
                clientListView.Height = Height - 93;
            }
            else
            {
                richTextBoxLog.Visible = true;
                clientListView.Height = Height - 184;
            }
        }

        private void saveStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveLog();
        }

        private void clearStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBoxLog.Text = "";
        }

        private void notifyIcon1_OnMouseClick(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Show();
            WindowState = FormWindowState.Normal;
        }

        #endregion Event based methods

        #region Private Methods

        /// <summary>
        /// Populate the available network cards. Excludes bridged network adapters, since they are not applicable to spoofing scenario
        /// <see cref="https://github.com/chmorgan/sharppcap/issues/57"/>
        /// </summary>
        private void EnumerateNetworkAdaptersforMenu()
        {
            toolStripComboBoxDevicelist.Items.AddRange(EnumerateNetworkAdapters());
        }

        /// <summary>
        /// Sets the text of interface list combobox to saved value if present
        /// </summary>
        private void SetSavedInterface()
        {
            toolStripComboBoxDevicelist.Text = ApplicationSettings.GetSavedPreferredInterfaceFriendlyName() ?? string.Empty;
        }

        private void SaveLog()
        {
            saveFileDialog1.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            saveFileDialog1.FileName = "CSArp-log";
            saveFileDialog1.FileOk += (object sender, System.ComponentModel.CancelEventArgs e) => {
                if (saveFileDialog1.FileName != "" && !File.Exists(saveFileDialog1.FileName))
                {
                    try
                    {
                        File.WriteAllText(saveFileDialog1.FileName, richTextBoxLog.Text);
                        DebugOutput.Print("Log saved to " + saveFileDialog1.FileName);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            _ = saveFileDialog1.ShowDialog();
        }

        private static string[] EnumerateNetworkAdapters()
        {
            return NetworkAdapterManager.WinPcapDevices.Where(device => !string.IsNullOrEmpty(device.Interface.FriendlyName))
                                                       .Select(device => device.Interface.FriendlyName)
                                                       .ToArray();
        }

        private void ExitGracefully()
        {
            ThreadBuffer.Clear();
            StopCapture();
        }

        #endregion Private Methods

        private void stopNetworkScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopNetworkScan();
            toolStripStatus.Text = "Scan stopped!";
        }
    }
}