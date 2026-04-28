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

        private IPAddress _gatewayIpAddress;
        private LibPcapLiveDevice _selectedDevice;
        private string _selectedInterfaceFriendlyName;

        public ScannerForm()
        {
            InitializeComponent();
            _arpSpoofer = new Spoofer();
            _networkScanner = new NetworkScanner();
            _networkScanner.ScanStarting += (_, __) => RunOnUiThread(() => clientListView.Items.Clear());
            _networkScanner.ClientFound += (_, e) => AddClientToList(e.IpAddress, e.MacAddress, e.IsGateway);
            _networkScanner.StatusChanged += (_, status) => RunOnUiThread(() => toolStripStatusScan.Text = status);
            _networkScanner.ProgressChanged += (_, progress) => RunOnUiThread(() => toolStripProgressBarScan.Value = progress);
            DebugOutput.Init(richTextBoxLog);
        }

        private void StartNetworkScan()
        {
            if (_networkScanner.IsScanning)
            {
                toolStripStatus.Text = "A scan is already running.";
                return;
            }

            if (!TryPrepareSelectedDevice(out var selectedDevice, out var gatewayIpAddress))
            {
                return;
            }

            _selectedDevice = selectedDevice;
            _gatewayIpAddress = gatewayIpAddress;

            _arpSpoofer.StopAll();
            toolStripStatus.Text = "Ready";
            _networkScanner.StartScan(_selectedDevice, _gatewayIpAddress);
        }

        private bool TryPrepareSelectedDevice(out LibPcapLiveDevice selectedDevice, out IPAddress gatewayIpAddress)
        {
            selectedDevice = null;
            gatewayIpAddress = null;

            if (string.IsNullOrWhiteSpace(toolStripComboBoxDevicelist.Text))
            {
                _ = MessageBox.Show("Pick a device before a scan.");
                return false;
            }

            var interfaceFriendlyName = toolStripComboBoxDevicelist.Text;
            if (!string.Equals(_selectedInterfaceFriendlyName, interfaceFriendlyName, StringComparison.Ordinal) || _selectedDevice == null)
            {
                _selectedInterfaceFriendlyName = interfaceFriendlyName;
                CloseSelectedDevice();
                _selectedDevice = LibPcapDeviceExtensions.GetWinPcapDevices()
                    .FirstOrDefault(dev => string.Equals(dev.Interface.FriendlyName, _selectedInterfaceFriendlyName, StringComparison.Ordinal));
            }

            selectedDevice = _selectedDevice;
            if (selectedDevice == null)
            {
                _ = MessageBox.Show("Could not initialize the selected network adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            gatewayIpAddress = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => i.Name == _selectedInterfaceFriendlyName)?
                .GetIPProperties()
                .GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (gatewayIpAddress == null)
            {
                _ = MessageBox.Show("Could not detect gateway IP for the selected adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!selectedDevice.Opened)
            {
                var conf = new DeviceConfiguration { Mode = DeviceModes.Promiscuous, ReadTimeout = 1000 };
                selectedDevice.Open(conf);
            }

            return true;
        }

        private void StopNetworkScan()
        {
            _networkScanner.StopScan();
            CloseSelectedDevice();
        }

        private void CloseSelectedDevice()
        {
            if (_selectedDevice == null || !_selectedDevice.Opened)
            {
                return;
            }

            try
            {
                _selectedDevice.StopCapture();
                _selectedDevice.Close();
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("Exception while closing capture device [" + ex.Message + "]");
            }
        }

        private void DisconnectSelectedClients()
        {
            if (clientListView.SelectedItems.Count == 0)
            {
                return;
            }

            var gatewayPhysicalAddress = clientListView.Items
                .OfType<ListViewItem>()
                .Where(item => item.SubItems[0].Text == _gatewayIpAddress?.ToString())
                .Select(item => item.SubItems[1].Text.Parse())
                .FirstOrDefault();

            if (gatewayPhysicalAddress == null)
            {
                _ = MessageBox.Show("Gateway Physical Address still undiscovered. Please wait and try again.", "Warning", MessageBoxButtons.OK);
                return;
            }

            toolStripStatus.Text = "Arpspoofing active...";

            var targets = clientListView.SelectedItems
                .OfType<ListViewItem>()
                .ToDictionary(
                    item => IPAddress.Parse(item.SubItems[0].Text),
                    item => item.SubItems[1].Text.Parse());

            foreach (ListViewItem item in clientListView.SelectedItems)
            {
                item.SubItems[2].Text = "Off";
            }

            _arpSpoofer.Start(targets, _gatewayIpAddress, gatewayPhysicalAddress, _selectedDevice);
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

        private void toolStripMenuItemRefreshClients_Click(object sender, EventArgs e) => StartNetworkScan();

        private void aboutCSArpToolStripMenuItem_Click(object sender, EventArgs e) => _ = MessageBox.Show("Author : globalpolicy\nContact : yciloplabolg@gmail.com\nBlog : c0dew0rth.blogspot.com\nGithub : globalpolicy\nContributions are welcome!\n\nContributors:\nZafer Balkan : zafer@zaferbalkan.com", "About CSArp", MessageBoxButtons.OK);

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Environment.Exit(0);

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripComboBoxDevicelist.Items.AddRange(LibPcapDeviceExtensions.GetWinPcapDevices()
                .Select(device => device.Interface.FriendlyName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray());

            toolStripComboBoxDevicelist.Text = ApplicationSettings.GetSavedPreferredInterfaceFriendlyName() ?? string.Empty;
        }

        private void cutoffToolStripMenuItem_Click(object sender, EventArgs e) => DisconnectSelectedClients();

        private void reconnectToolStripMenuItem_Click(object sender, EventArgs e) => ReconnectClients();

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
            if (e.KeyCode == Keys.Enter && clientListView.SelectedItems.Count == 1)
            {
                clientListView.SelectedItems[0].SubItems[3].Text = toolStripTextBoxClientName.Text;
                toolStripTextBoxClientName.Text = string.Empty;
            }
        }

        private void toolStripMenuItemMinimize_Click(object sender, EventArgs e) => WindowState = FormWindowState.Minimized;

        private void toolStripMenuItemSaveSettings_Click(object sender, EventArgs e)
        {
            if (ApplicationSettings.SaveSettings(clientListView, toolStripComboBoxDevicelist.Text))
            {
                toolStripStatus.Text = "Settings saved!";
            }
        }

        private void showLogToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            var showLog = showLogToolStripMenuItem.Checked;
            richTextBoxLog.Visible = showLog;
            clientListView.Height = showLog ? Height - 184 : Height - 93;
        }

        private void saveStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            saveFileDialog1.FileName = "CSArp-log";

            if (saveFileDialog1.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(saveFileDialog1.FileName))
            {
                return;
            }

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

        private void clearStripMenuItem_Click(object sender, EventArgs e) => richTextBoxLog.Clear();

        private void notifyIcon1_OnMouseClick(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void AddClientToList(IPAddress ipAddress, PhysicalAddress macAddress, bool isGateway)
        {
            RunOnUiThread(() =>
            {
                var name = isGateway ? "GATEWAY" : ApplicationSettings.GetSavedClientNameFromMAC(macAddress.ToString("-"));
                _ = clientListView.Items.Add(new ListViewItem(new[]
                {
                    ipAddress.ToString(),
                    macAddress.ToString("-"),
                    "On",
                    name
                }));
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (InvokeRequired)
            {
                _ = BeginInvoke(action);
                return;
            }

            action();
        }

        private void ExitGracefully()
        {
            StopNetworkScan();
            _arpSpoofer.StopAll();
        }

        private void stopNetworkScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopNetworkScan();
            toolStripStatus.Text = "Scan stopped!";
        }
    }
}
