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
        private LibPcapLiveDevice selectedDevice;
        private string selectedInterfaceFriendlyName;

        public ScannerForm()
        {
            InitializeComponent();
            _arpSpoofer = new Spoofer();
            _networkScanner = new NetworkScanner();
            DebugOutput.Init(richTextBoxLog);
        }

        private void StartNetworkScan()
        {
            if (_networkScanner.IsScanning)
            {
                toolStripStatus.Text = "A scan is already running.";
                return;
            }

            if (!TryPrepareSelectedDevice())
            {
                return;
            }

            _arpSpoofer.StopAll();
            toolStripStatus.Text = "Ready";

            _networkScanner.StartScan(
                selectedDevice,
                gatewayIpAddress,
                onScanStarting: () => UpdateOnUiThread(() => clientListView.Items.Clear()),
                onClientFound: AddClientToList,
                onStatusChanged: status => UpdateOnUiThread(() => toolStripStatusScan.Text = status),
                onProgressChanged: progress => UpdateOnUiThread(() => toolStripProgressBarScan.Value = progress));
        }

        private void StopNetworkScan()
        {
            _networkScanner.StopScan();
            CloseSelectedDevice();
        }

        private bool TryPrepareSelectedDevice()
        {
            if (!TrySetSelectedInterface(toolStripComboBoxDevicelist.Text))
            {
                return false;
            }

            if (!TryGetGatewayIpAddress(out gatewayIpAddress))
            {
                _ = MessageBox.Show("Could not detect gateway IP for the selected adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (selectedDevice == null)
            {
                _ = MessageBox.Show("Could not initialize the selected network adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!selectedDevice.Opened)
            {
                var conf = new DeviceConfiguration { Mode = DeviceModes.Promiscuous, ReadTimeout = 1000 };
                selectedDevice.Open(conf);
            }

            return true;
        }

        private bool TrySetSelectedInterface(string interfaceFriendlyName)
        {
            if (string.IsNullOrWhiteSpace(interfaceFriendlyName))
            {
                _ = MessageBox.Show("Pick a device before a scan.");
                return false;
            }

            if (string.Equals(selectedInterfaceFriendlyName, interfaceFriendlyName, StringComparison.Ordinal) && selectedDevice != null)
            {
                return true;
            }

            selectedInterfaceFriendlyName = interfaceFriendlyName;
            CloseSelectedDevice();
            selectedDevice = LibPcapDeviceExtensions.GetWinPcapDevices()
                .FirstOrDefault(dev => string.Equals(dev.Interface.FriendlyName, selectedInterfaceFriendlyName, StringComparison.Ordinal));

            return selectedDevice != null;
        }

        private void CloseSelectedDevice()
        {
            if (selectedDevice == null || !selectedDevice.Opened)
            {
                return;
            }

            try
            {
                selectedDevice.StopCapture();
                selectedDevice.Close();
            }
            catch (PcapException ex)
            {
                DebugOutput.Print("Exception while closing capture device [" + ex.Message + "]");
            }
        }

        private bool TryGetGatewayIpAddress(out IPAddress gatewayAddress)
        {
            gatewayAddress = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => i.Name == selectedInterfaceFriendlyName)?
                .GetIPProperties()
                .GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            return gatewayAddress != null;
        }

        private void DisconnectSelectedClients()
        {
            if (clientListView.SelectedItems.Count == 0)
            {
                return;
            }

            gatewayPhysicalAddress = clientListView.Items
                .OfType<ListViewItem>()
                .Where(item => item.SubItems[0].Text == gatewayIpAddress.ToString())
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
                .Select(item => new KeyValuePair<IPAddress, PhysicalAddress>(IPAddress.Parse(item.SubItems[0].Text), item.SubItems[1].Text.Parse()))
                .ToList();

            foreach (ListViewItem item in clientListView.SelectedItems)
            {
                item.SubItems[2].Text = "Off";
            }

            _arpSpoofer.Start(targets, gatewayIpAddress, gatewayPhysicalAddress, selectedDevice);
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

        #region Event based methods

        private void toolStripMenuItemRefreshClients_Click(object sender, EventArgs e) => StartNetworkScan();

        private void aboutCSArpToolStripMenuItem_Click(object sender, EventArgs e) => _ = MessageBox.Show("Author : globalpolicy\nContact : yciloplabolg@gmail.com\nBlog : c0dew0rth.blogspot.com\nGithub : globalpolicy\nContributions are welcome!\n\nContributors:\nZafer Balkan : zafer@zaferbalkan.com", "About CSArp", MessageBoxButtons.OK);

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Environment.Exit(0);

        private void Form1_Load(object sender, EventArgs e)
        {
            EnumerateNetworkAdaptersforMenu();
            SetSavedInterface();
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

        private void saveStripMenuItem_Click(object sender, EventArgs e) => SaveLog();

        private void clearStripMenuItem_Click(object sender, EventArgs e) => richTextBoxLog.Clear();

        private void notifyIcon1_OnMouseClick(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Show();
            WindowState = FormWindowState.Normal;
        }

        #endregion Event based methods

        #region Private Methods

        private void EnumerateNetworkAdaptersforMenu() => toolStripComboBoxDevicelist.Items.AddRange(EnumerateNetworkAdapters());

        private void SetSavedInterface() => toolStripComboBoxDevicelist.Text = ApplicationSettings.GetSavedPreferredInterfaceFriendlyName() ?? string.Empty;

        private void SaveLog()
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

        private static string[] EnumerateNetworkAdapters() => LibPcapDeviceExtensions.GetWinPcapDevices()
            .Select(device => device.Interface.FriendlyName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        private void AddClientToList(IPAddress ipAddress, PhysicalAddress macAddress, bool isGateway) => UpdateOnUiThread(() =>
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

        private void UpdateOnUiThread(Action action)
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
            _networkScanner.StopScan();
            _arpSpoofer.StopAll();
            CloseSelectedDevice();
        }

        #endregion Private Methods

        private void stopNetworkScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopNetworkScan();
            toolStripStatus.Text = "Scan stopped!";
        }
    }
}
