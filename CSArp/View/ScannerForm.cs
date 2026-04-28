using System;
using System.Diagnostics;
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

        private IPAddress? _gatewayIpAddress;
        private LibPcapLiveDevice? _selectedDevice;
        private string? _selectedInterfaceFriendlyName;
        private IPAddress? _sourceIpAddress;
        private PhysicalAddress? _sourceMacAddress;

        public ScannerForm()
        {
            InitializeComponent();
            _arpSpoofer = new Spoofer(Log);
            _networkScanner = new NetworkScanner(Log);
        }

        private void Log(string message)
        {
            Debug.Print(message);
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    richTextBoxLog.AppendText($"{DateTime.Now} : {message}\n");
                    richTextBoxLog.ScrollToCaret();
                }));
            }
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
            clientListView.Items.Clear();
            toolStripStatus.Text = "Ready";

            _networkScanner.StartScan(
                _selectedDevice!,
                _gatewayIpAddress!,
                new Progress<ClientDiscoveredEventArgs>(e => AddClientToList(e.IpAddress, e.MacAddress, e.IsGateway)),
                new Progress<string>(status => toolStripStatusScan.Text = status),
                new Progress<int>(progress => toolStripProgressBarScan.Value = progress));
        }

        private bool TryPrepareSelectedDevice(out LibPcapLiveDevice? selectedDevice, out IPAddress? gatewayIpAddress)
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
                    .FirstOrDefault(dev => string.Equals(dev.Interface?.FriendlyName, _selectedInterfaceFriendlyName, StringComparison.Ordinal));
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

            _sourceIpAddress = selectedDevice.ReadCurrentIpV4Address();
            _sourceMacAddress = selectedDevice.MacAddress;

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
                Log($"Exception while closing capture device [{ex.Message}]");
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
                .Where(item => !IsProtectedTarget(item))
                .ToDictionary(
                    item => IPAddress.Parse(item.SubItems[0].Text),
                    item => item.SubItems[1].Text.Parse());

            if (targets.Count == 0)
            {
                _ = MessageBox.Show("You cannot spoof the source device or the gateway.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                toolStripStatus.Text = "Select a different target.";
                return;
            }

            foreach (ListViewItem item in clientListView.SelectedItems)
            {
                if (!IsProtectedTarget(item))
                {
                    item.SubItems[2].Text = "Off";
                }
            }

            _arpSpoofer.Start(targets, _gatewayIpAddress!, gatewayPhysicalAddress, _selectedDevice!);
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

        private void aboutCSArpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? Application.ProductVersion;
            var aboutText =
                "CSArp\n" +
                $"Version: {version}\n\n" +
                "Authors: globalpolicy, Zafer Balkan (DeltaZulu OÜ)\n" +
                "Copyright: Portions Copyright © 2017 globalpolicy; Copyright © 2024-2026 Zafer Balkan (DeltaZulu OÜ)\n" +
                "License: MIT (see LICENSE file)\n\n" +
                "Contributions are welcome.";

            _ = MessageBox.Show(aboutText, "About CSArp", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Environment.Exit(0);

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripComboBoxDevicelist.Items.AddRange(LibPcapDeviceExtensions.GetWinPcapDevices()
                 .Select(device => device.Interface?.FriendlyName)
                .OfType<string>()
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
                Log($"Log saved to {saveFileDialog1.FileName}");
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
            var isSourceDevice = IsSourceClient(ipAddress, macAddress);
            var name = isGateway
                ? "GATEWAY"
                : isSourceDevice
                    ? "THIS DEVICE"
                    : ApplicationSettings.GetSavedClientNameFromMAC(macAddress.ToString("-"));
            _ = clientListView.Items.Add(new ListViewItem(new[]
            {
                ipAddress.ToString(),
                macAddress.ToString("-"),
                "On",
                name
            })
            {
                ToolTipText = isGateway
                    ? "The gateway cannot be spoofed."
                    : isSourceDevice
                        ? "The source device cannot be spoofed."
                        : string.Empty
            });
        }

        private bool IsSourceClient(IPAddress ipAddress, PhysicalAddress macAddress) =>
            (_sourceIpAddress != null && _sourceIpAddress.Equals(ipAddress)) ||
            (_sourceMacAddress != null && _sourceMacAddress.Equals(macAddress));

        private bool IsGatewayClient(IPAddress ipAddress) =>
            _gatewayIpAddress != null && _gatewayIpAddress.Equals(ipAddress);

        private bool IsProtectedTarget(ListViewItem item)
        {
            if (item == null)
            {
                return false;
            }

            var ip = IPAddress.Parse(item.SubItems[0].Text);
            var mac = item.SubItems[1].Text.Parse();
            return IsGatewayClient(ip) || IsSourceClient(ip, mac);
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

        private void clientListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected || !IsProtectedTarget(e.Item))
            {
                return;
            }

            e.Item.Selected = false;
            var ip = IPAddress.Parse(e.Item.SubItems[0].Text);
            toolStripStatus.Text = IsGatewayClient(ip)
                ? "The gateway cannot be spoofed."
                : "The source device cannot be spoofed.";
        }
    }
}
