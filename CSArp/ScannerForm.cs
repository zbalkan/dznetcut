using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows.Forms;
using CSArp.Logic;
using CSArp.Logic.Utilities;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CSArp
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
        private readonly Dictionary<string, ListViewItem> _clientItemsByIp = new Dictionary<string, ListViewItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, AdapterSelectionOptionModel> _adapterOptionsByDisplayText = new Dictionary<string, AdapterSelectionOptionModel>(StringComparer.Ordinal);
        private readonly Dictionary<string, LibPcapLiveDevice> _pcapDevicesById = new Dictionary<string, LibPcapLiveDevice>(StringComparer.Ordinal);
        private string? _nameEditorSelectionKey;

        public ScannerForm()
        {
            InitializeComponent();
            _arpSpoofer = new Spoofer(Log);
            _networkScanner = new NetworkScanner(Log);
            _arpSpoofer.SpoofingStateChanged += _ => SafeUpdateUiState();
            _networkScanner.ScanStateChanged += _ => SafeUpdateUiState();
        }

        private void Log(string message)
        {
            Debug.Print(message);
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    richTextBoxLog.AppendText($"{DateTimeOffset.Now:O} : {message}\n");
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
            _clientItemsByIp.Clear();
            toolStripStatus.Text = "Ready";

            _networkScanner.StartScan(
                _selectedDevice!,
                _gatewayIpAddress!,
                new Progress<ClientDiscoveredEventArgs>(e => AddClientToList(e.IpAddress, e.MacAddress, e.IsGateway)),
                new Progress<string>(status => toolStripStatusScan.Text = status),
                new Progress<int>(progress => toolStripProgressBarScan.Value = progress));
            UpdateUiState();
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

            var selectedDisplayText = toolStripComboBoxDevicelist.Text;
            if (!_adapterOptionsByDisplayText.TryGetValue(selectedDisplayText, out var selectedAdapterOption))
            {
                _ = MessageBox.Show("Could not resolve the selected network adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!_pcapDevicesById.TryGetValue(selectedAdapterOption.DeviceId, out var pcapDevice))
            {
                _ = MessageBox.Show("Could not resolve the selected pcap device.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!string.Equals(_selectedInterfaceFriendlyName, selectedDisplayText, StringComparison.Ordinal) || _selectedDevice == null)
            {
                _selectedInterfaceFriendlyName = selectedDisplayText;
                CloseSelectedDevice();
                _selectedDevice = pcapDevice;
            }

            selectedDevice = _selectedDevice;
            if (selectedDevice == null)
            {
                _ = MessageBox.Show("Could not initialize the selected network adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            gatewayIpAddress = selectedAdapterOption.GatewayIpAddress;

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
            UpdateUiState();
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

            PhysicalAddress? gatewayPhysicalAddress = null;
            foreach (ListViewItem item in clientListView.Items)
            {
                if (item.SubItems[0].Text != _gatewayIpAddress?.ToString())
                {
                    continue;
                }

                gatewayPhysicalAddress = item.SubItems[1].Text.Parse();
                break;
            }

            if (gatewayPhysicalAddress == null)
            {
                _ = MessageBox.Show("Gateway Physical Address still undiscovered. Please wait and try again.", "Warning", MessageBoxButtons.OK);
                return;
            }

            toolStripStatus.Text = "Arpspoofing active...";

            var targets = new Dictionary<IPAddress, PhysicalAddress>();
            foreach (ListViewItem item in clientListView.SelectedItems)
            {
                if (IsProtectedTarget(item))
                {
                    continue;
                }

                targets[IPAddress.Parse(item.SubItems[0].Text)] = item.SubItems[1].Text.Parse();
            }

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
            UpdateUiState();
        }

        private void ReconnectClients()
        {
            _arpSpoofer.StopAll();
            foreach (ListViewItem entry in clientListView.Items)
            {
                entry.SubItems[2].Text = "On";
            }

            toolStripStatus.Text = "Stopped";
            UpdateUiState();
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
            PopulateAdapterDropDown(preferredSelection: null);
            var showLog = ApplicationSettings.GetSavedShowLog() ?? false;
            showLogToolStripMenuItem.Checked = showLog;
            ApplyLogVisibility(showLog);
            AdjustClientListViewLayout();
            toolStripStatus.Text = "Select an interface.";
            UpdateUiState();
        }

        private void toolStripMenuItemSelectAllAdapters_CheckStateChanged(object sender, EventArgs e)
        {
            PopulateAdapterDropDown(toolStripComboBoxDevicelist.Text);
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
                var selectedItem = clientListView.SelectedItems[0];
                selectedItem.SubItems[3].Text = toolStripTextBoxClientName.Text;
                toolStripTextBoxClientName.Text = string.Empty;
                UpdateUiState();
            }
        }

        private void toolStripMenuItemSaveSettings_Click(object sender, EventArgs e)
        {
            if (ApplicationSettings.SaveSettings(clientListView, toolStripComboBoxDevicelist.Text, showLogToolStripMenuItem.Checked))
            {
                toolStripStatus.Text = "Settings saved!";
            }
        }

        private void showLogToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            ApplyLogVisibility(showLogToolStripMenuItem.Checked);
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
            var ipKey = ipAddress.ToString();
            var macValue = macAddress.ToString("-");
            var isSourceDevice = IsSourceClient(ipAddress, macAddress);
            var name = isGateway
                ? "GATEWAY"
                : isSourceDevice
                    ? "THIS DEVICE"
                    : ApplicationSettings.GetSavedClientNameFromMAC(macValue);
            var toolTip = isGateway
                ? "The gateway cannot be spoofed."
                : isSourceDevice
                    ? "The source device cannot be spoofed."
                    : string.Empty;

            if (_clientItemsByIp.TryGetValue(ipKey, out var existing))
            {
                existing.SubItems[1].Text = macValue;
                if (string.IsNullOrWhiteSpace(existing.SubItems[3].Text))
                {
                    existing.SubItems[3].Text = name;
                }
                existing.ToolTipText = toolTip;
                return;
            }

            var entry = new ListViewItem(new[]
            {
                ipKey,
                macValue,
                "On",
                name
            })
            {
                ToolTipText = toolTip
            };

            _clientItemsByIp[ipKey] = entry;
            _ = clientListView.Items.Add(entry);
            UpdateUiState();
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
                UpdateUiState();
                return;
            }

            e.Item.Selected = false;
            var ip = IPAddress.Parse(e.Item.SubItems[0].Text);
            toolStripStatus.Text = IsGatewayClient(ip)
                ? "The gateway cannot be spoofed."
                : "The source device cannot be spoofed.";
            UpdateUiState();
        }

        private void clientListView_Resize(object sender, EventArgs e) => AdjustClientListViewLayout();

        private void ApplyLogVisibility(bool showLog)
        {
            richTextBoxLog.Visible = showLog;
            clientListView.Height = showLog ? Height - 184 : Height - 93;
            AdjustClientListViewLayout();
        }

        private void UpdateUiState()
        {
            var hasSelection = clientListView.SelectedItems.Count > 0;
            var hasSingleSelection = clientListView.SelectedItems.Count == 1;
            var hasSpoofing = _arpSpoofer.IsSpoofing;
            var hasScan = _networkScanner.IsScanning;

            cutoffToolStripMenuItem.Enabled = hasSelection && !hasScan;
            reconnectToolStripMenuItem.Enabled = hasSpoofing;
            stopNetworkScanToolStripMenuItem.Enabled = hasScan;
            toolStripMenuItemRefreshClients.Enabled = !hasScan;
            ClientNametoolStripMenuItem.Enabled = hasSingleSelection;
            toolStripTextBoxClientName.Enabled = hasSingleSelection;

            if (hasSingleSelection)
            {
                var selectedKey = clientListView.SelectedItems[0].SubItems[0].Text;
                if (!string.Equals(_nameEditorSelectionKey, selectedKey, StringComparison.Ordinal))
                {
                    _nameEditorSelectionKey = selectedKey;
                    toolStripTextBoxClientName.Text = clientListView.SelectedItems[0].SubItems[3].Text;
                }
            }
            else
            {
                _nameEditorSelectionKey = null;
                toolStripTextBoxClientName.Text = string.Empty;
            }
        }

        private void SafeUpdateUiState()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new Action(UpdateUiState));
        }

        private void AdjustClientListViewLayout()
        {
            if (clientListView.ClientSize.Width <= 0)
            {
                return;
            }

            var availableWidth = clientListView.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            if (availableWidth < 100)
            {
                return;
            }

            var ipWidth = Math.Max(120, (int)(availableWidth * 0.20));
            var macWidth = Math.Max(160, (int)(availableWidth * 0.28));
            var statusWidth = Math.Max(90, (int)(availableWidth * 0.12));
            var nameWidth = Math.Max(180, availableWidth - (ipWidth + macWidth + statusWidth));

            columnHeaderIP.Width = ipWidth;
            columnHeaderMAC.Width = macWidth;
            columnHeaderCutoffStatus.Width = statusWidth;
            columnHeaderClientname.Width = nameWidth;
        }

        private void PopulateAdapterDropDown(string? preferredSelection)
        {
            var currentSelection = string.IsNullOrWhiteSpace(preferredSelection)
                ? toolStripComboBoxDevicelist.Text
                : preferredSelection!;

            var options = BuildAdapterOptions();
            var includeVirtualAdapters = toolStripMenuItemSelectAllAdapters.Checked;
            var visibleOptions = AdapterSelectionService.FilterOptions(options, includeVirtualAdapters);

            _adapterOptionsByDisplayText.Clear();
            _pcapDevicesById.Clear();
            toolStripComboBoxDevicelist.Items.Clear();
            foreach (var device in LibPcapDeviceExtensions.GetWinPcapDevices())
            {
                _pcapDevicesById[device.Name] = device;
            }

            foreach (var option in visibleOptions)
            {
                _adapterOptionsByDisplayText[option.DisplayText] = option;
                toolStripComboBoxDevicelist.Items.Add(option.DisplayText);
            }

            if (visibleOptions.Count == 0)
            {
                toolStripComboBoxDevicelist.Text = string.Empty;
                toolStripStatus.Text = includeVirtualAdapters
                    ? "No adapters available."
                    : "No physical adapters found. Enable 'Show All Adapters'.";
                return;
            }

            var selectedText = visibleOptions
                .Select(option => option.DisplayText)
                .FirstOrDefault(text => string.Equals(text, currentSelection, StringComparison.Ordinal))
                ?? string.Empty;

            toolStripComboBoxDevicelist.Text = selectedText;
        }

        private static IReadOnlyList<AdapterSelectionOptionModel> BuildAdapterOptions()
        {
            var systemInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var physicalByInterfaceId = AdapterPhysicalClassifier.BuildByInterfaceId(systemInterfaces);
            var networkInterfaces = systemInterfaces
                .Select(networkInterface =>
                    new InterfaceSnapshot(
                        networkInterface.Id,
                        networkInterface.Name,
                        networkInterface.GetPhysicalAddress(),
                        networkInterface.NetworkInterfaceType,
                        physicalByInterfaceId.TryGetValue(networkInterface.Id, out var isPhysical)
                            ? isPhysical
                            : AdapterSelectionService.IsLikelyPhysicalAdapter(networkInterface.NetworkInterfaceType, networkInterface.GetPhysicalAddress()),
                        networkInterface.GetIPProperties().UnicastAddresses.Select(address => address.Address).ToArray(),
                        networkInterface.GetIPProperties().GatewayAddresses.Select(address => address.Address).ToArray()))
                .ToArray();
            var devices = LibPcapDeviceExtensions.GetWinPcapDevices()
                .Select(device =>
                    new AdapterDeviceSnapshot(
                        device.Name,
                        device.Interface?.FriendlyName ?? device.Name,
                        TryReadDeviceIpv4(device),
                        device.MacAddress))
                .ToArray();

            return AdapterSelectionService.BuildOptions(devices, networkInterfaces);
        }

        private static IPAddress? TryReadDeviceIpv4(LibPcapLiveDevice device)
        {
            try
            {
                return device.ReadCurrentIpV4Address();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
