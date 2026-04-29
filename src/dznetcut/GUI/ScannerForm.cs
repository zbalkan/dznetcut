using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows.Forms;
using dznetcut.Logic;
using SharpPcap;
using SharpPcap.LibPcap;
using dznetcut.Utilities;

namespace dznetcut.GUI
{
    public partial class ScannerForm : Form
    {
        private readonly Dictionary<string, AdapterSelectionOptionModel> _adapterOptionsByDisplayText = new Dictionary<string, AdapterSelectionOptionModel>(StringComparer.Ordinal);
        private readonly Spoofer _arpSpoofer;
        private readonly Dictionary<string, ListViewItem> _clientItemsByIp = new Dictionary<string, ListViewItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _displayTextByDeviceId = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly NetworkScanner _networkScanner;
        private readonly Dictionary<string, LibPcapLiveDevice> _pcapDevicesById = new Dictionary<string, LibPcapLiveDevice>(StringComparer.Ordinal);

        private IPAddress? _gatewayIpAddress;
        private bool _isArpProtectionApplied;
        private string? _nameEditorSelectionKey;
        private LibPcapLiveDevice? _selectedDevice;
        private string? _selectedInterfaceFriendlyName;
        private IPAddress? _sourceIpAddress;
        private PhysicalAddress? _sourceMacAddress;

        public ScannerForm()
        {
            InitializeComponent();
            _arpSpoofer = new Spoofer(Log);
            _networkScanner = new NetworkScanner(Log);
            _arpSpoofer.SpoofingStateChanged += _ => SafeUpdateUiState();
            _networkScanner.ScanStateChanged += _ => SafeUpdateUiState();
        }

        private static bool TryReadClientIdentity(ListViewItem? item, out IPAddress ipAddress, out PhysicalAddress macAddress)
        {
            ipAddress = IPAddress.None;
            macAddress = PhysicalAddress.None;

            if (item is null)
            {
                return false;
            }

            if (item.SubItems.Count < 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(item.SubItems[0].Text, out var parsedIpAddress))
            {
                return false;
            }

            ipAddress = parsedIpAddress;

            try
            {
                macAddress = item.SubItems[1].Text.Parse();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private void aboutdznetcutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            const string aboutText =
                "dznetcut v2.0.0\n" +
                "A DeltaZulu Project\n\n" +
                "Licensed under GNU GPL v3.0\n" +
                "Copyright (c) 2024-2026 Zafer Balkan\n\n" +
                "Includes components from dzmac (GPLv3).\n" +
                "Derived from dznetcut-netcut (MIT License).\n\n" +
                "Upstream Hash: 6952d98\n" +
                "DeltaZulu Hash: cbaba0b\n\n" +
                "Source: github.com/DeltaZulu-OU/dznetcut";

            _ = MessageBox.Show(aboutText, "about dznetcut", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (isGateway || isSourceDevice || string.IsNullOrWhiteSpace(existing.SubItems[3].Text))
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

        private void ApplyLogVisibility(bool showLog)
        {
            richTextBoxLog.Visible = showLog;
            clientListView.Height = showLog ? Height - 184 : Height - 93;
            AdjustClientListViewLayout();
        }


        private string GetSelectedInterfaceId()
        {
            if (string.IsNullOrWhiteSpace(_selectedInterfaceFriendlyName)
                || !_adapterOptionsByDisplayText.TryGetValue(_selectedInterfaceFriendlyName!, out var selectedOption)
                || string.IsNullOrWhiteSpace(selectedOption.InterfaceId))
            {
                throw new InvalidOperationException("Cannot map selected interface to a Windows network adapter.");
            }

            return selectedOption.InterfaceId!;
        }

        private void commandLineParametersToolStripMenuItem_Click(object sender, EventArgs e)
            => _ = MessageBox.Show(CLI.CliHelpText.Build(), "Command line parameters", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private string BuildUniqueDisplayText(AdapterSelectionOptionModel option)
        {
            var proposedText = option.DisplayText;
            if (!_adapterOptionsByDisplayText.ContainsKey(proposedText))
            {
                return proposedText;
            }

            var suffix = option.DeviceId.Length > 12
                ? option.DeviceId.Substring(option.DeviceId.Length - 12)
                : option.DeviceId;
            var candidate = $"{proposedText} ({suffix})";
            var increment = 2;
            while (_adapterOptionsByDisplayText.ContainsKey(candidate))
            {
                candidate = $"{proposedText} ({suffix} #{increment})";
                increment++;
            }

            return candidate;
        }

        private void clearStripMenuItem_Click(object sender, EventArgs e) => richTextBoxLog.Clear();

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

        private void cutoffToolStripMenuItem_Click(object sender, EventArgs e) => DisconnectSelectedClients();

        private void DisconnectSelectedClients()
        {
            if (clientListView.SelectedItems.Count == 0)
            {
                return;
            }

            if (_arpSpoofer.IsSpoofing)
            {
                _ = MessageBox.Show("Only one spoofing task can run at a time. Stop the active task first.", "Spoofing in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                toolStripStatus.Text = "Stop spoofing before starting a new target.";
                UpdateUiState();
                return;
            }

            PhysicalAddress gatewayPhysicalAddress;
            try
            {
                gatewayPhysicalAddress = ResolveGatewayMacFromList();
            }
            catch (InvalidOperationException)
            {
                _ = MessageBox.Show("Gateway Physical Address still undiscovered. Please wait and try again.", "Warning", MessageBoxButtons.OK);
                return;
            }

            toolStripStatus.Text = "Arpspoofing active...";

            var targets = new Dictionary<IPAddress, PhysicalAddress>();
            var spoofableSelectedItems = new List<ListViewItem>();
            foreach (ListViewItem item in clientListView.SelectedItems)
            {
                if (!TryReadClientIdentity(item, out var ipAddress, out var macAddress))
                {
                    continue;
                }

                if (IsGatewayClient(ipAddress) || IsSourceClient(ipAddress, macAddress))
                {
                    continue;
                }

                targets[ipAddress] = macAddress;
                spoofableSelectedItems.Add(item);
            }

            if (targets.Count == 0)
            {
                _ = MessageBox.Show("You cannot spoof the source device or the gateway.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                toolStripStatus.Text = "Select a different target.";
                return;
            }

            foreach (var item in spoofableSelectedItems)
            {
                item.SubItems[2].Text = "Off";
            }

            if (toolStripMenuItemArpProtection.Checked)
            {
                TryEnableArpProtection(gatewayPhysicalAddress);
            }

            if (_gatewayIpAddress == null || _selectedDevice == null)
            {
                toolStripStatus.Text = "Select an interface before spoofing.";
                return;
            }

            try
            {
                _arpSpoofer.Start(targets, _gatewayIpAddress, gatewayPhysicalAddress, _selectedDevice);
            }
            catch (Exception ex)
            {
                TryDisableArpProtection();
                _ = MessageBox.Show($"Failed to start spoofing: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatus.Text = "Failed to start spoofing.";
            }
            UpdateUiState();
        }

        private void ExitGracefully()
        {
            StopNetworkScan();
            _arpSpoofer.StopAll();
            TryDisableArpProtection();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private void Form1_Load(object sender, EventArgs e)
        {
            PopulateAdapterDropDown(ApplicationSettings.GetSavedPreferredInterfaceFriendlyName());
            var showLog = ApplicationSettings.GetSavedShowLog() ?? false;
            showLogToolStripMenuItem.Checked = showLog;
            ApplyLogVisibility(showLog);
            AdjustClientListViewLayout();
            toolStripStatus.Text = "Select an interface.";
            UpdateUiState();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                Hide();
            }
        }

        private bool IsGatewayClient(IPAddress ipAddress) =>
            _gatewayIpAddress != null && _gatewayIpAddress.Equals(ipAddress);

        private bool IsProtectedTarget(ListViewItem item) => TryReadClientIdentity(item, out var ipAddress, out var macAddress)
                && (IsGatewayClient(ipAddress) || IsSourceClient(ipAddress, macAddress));

        private bool IsSourceClient(IPAddress ipAddress, PhysicalAddress macAddress) =>
            (_sourceIpAddress != null && _sourceIpAddress.Equals(ipAddress)) ||
            (_sourceMacAddress != null && _sourceMacAddress.Equals(macAddress));

        private void Log(string message)
        {
            Debug.Print(message);
            RunOnUiThread(() => {
                richTextBoxLog.AppendText($"{DateTimeOffset.Now:O} : {message}\n");
                richTextBoxLog.ScrollToCaret();
            });
        }

        private void notifyIcon1_OnMouseClick(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void PopulateAdapterDropDown(string? preferredSelection)
        {
            var currentSelection = string.IsNullOrWhiteSpace(preferredSelection)
                ? toolStripComboBoxDevicelist.Text
                : preferredSelection;

            var includeVirtualAdapters = toolStripMenuItemSelectAllAdapters.Checked;
            var captureReady = AdapterCatalogService.TryLoadCatalog(includeVirtualAdapters, out var catalog, out var captureError);
            if (!captureReady && !string.IsNullOrWhiteSpace(captureError))
            {
                Log(captureError!);
            }

            var visibleOptions = catalog.VisibleOptions;

            _adapterOptionsByDisplayText.Clear();
            _pcapDevicesById.Clear();
            _displayTextByDeviceId.Clear();
            toolStripComboBoxDevicelist.Items.Clear();
            foreach (var entry in catalog.DevicesById)
            {
                _pcapDevicesById[entry.Key] = entry.Value;
            }

            foreach (var option in visibleOptions)
            {
                var displayText = BuildUniqueDisplayText(option);
                _adapterOptionsByDisplayText[displayText] = option;
                _displayTextByDeviceId[option.DeviceId] = displayText;
                toolStripComboBoxDevicelist.Items.Add(displayText);
            }

            if (visibleOptions.Count == 0)
            {
                toolStripComboBoxDevicelist.Text = string.Empty;
                if (_pcapDevicesById.Count == 0)
                {
                    toolStripStatus.Text = "No capture adapters found. Install Npcap and restart dznetcut.";
                }
                else
                {
                    toolStripStatus.Text = includeVirtualAdapters
                        ? "No adapters available."
                        : "No physical adapters found. Enable 'Show All Adapters'.";
                }
                return;
            }

            var selectedText = visibleOptions
                .Select(option => _displayTextByDeviceId.TryGetValue(option.DeviceId, out var mappedText) ? mappedText : option.DisplayText)
                .FirstOrDefault(text => string.Equals(text, currentSelection, StringComparison.Ordinal))
                ?? visibleOptions
                    .Select(option => _displayTextByDeviceId.TryGetValue(option.DeviceId, out var mappedText) ? mappedText : option.DisplayText)
                    .FirstOrDefault()
                ?? string.Empty;

            toolStripComboBoxDevicelist.Text = selectedText;
        }

        private void ReconnectClients()
        {
            _arpSpoofer.StopAll();
            TryDisableArpProtection();
            foreach (ListViewItem entry in clientListView.Items)
            {
                entry.SubItems[2].Text = "On";
            }

            toolStripStatus.Text = "Stopped";
            UpdateUiState();
        }

        private void reconnectToolStripMenuItem_Click(object sender, EventArgs e) => ReconnectClients();

        private PhysicalAddress ResolveGatewayMacFromList()
        {
            foreach (ListViewItem item in clientListView.Items)
            {
                if (item.SubItems[0].Text == _gatewayIpAddress?.ToString())
                {
                    return item.SubItems[1].Text.Parse();
                }
            }

            throw new InvalidOperationException("Gateway MAC address is unavailable.");
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (!InvokeRequired)
            {
                action();
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
            {
            }
        }

        private void SafeUpdateUiState() => RunOnUiThread(UpdateUiState);

        private void saveStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog1.InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            saveFileDialog1.FileName = "dznetcut-log";

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

        private void showLogToolStripMenuItem_CheckStateChanged(object sender, EventArgs e) => ApplyLogVisibility(showLogToolStripMenuItem.Checked);

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

        private void StopNetworkScan()
        {
            _networkScanner.StopScan();
            var stopped = _networkScanner.WaitForStop(TimeSpan.FromSeconds(5));
            if (stopped && !_networkScanner.IsScanning)
            {
                CloseSelectedDevice();
            }
            else
            {
                Log("Scan is still stopping; deferring device close to avoid race with capture operations.");
            }
            UpdateUiState();
        }

        private void stopNetworkScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopNetworkScan();
            toolStripStatus.Text = "Scan stopped!";
        }

        private void toolStripMenuItemArpProtection_CheckStateChanged(object sender, EventArgs e)
        {
            if (!toolStripMenuItemArpProtection.Checked)
            {
                TryDisableArpProtection();
            }
        }

        private void toolStripMenuItemStealthMode_CheckStateChanged(object sender, EventArgs e)
        {
            _networkScanner.SetStealthMode(toolStripMenuItemStealthMode.Checked);
            toolStripStatus.Text = toolStripMenuItemStealthMode.Checked
                ? "Stealth mode enabled."
                : "Stealth mode disabled.";
        }

        private void toolStripMenuItemRefreshClients_Click(object sender, EventArgs e) => StartNetworkScan();

        private void toolStripMenuItemSaveSettings_Click(object sender, EventArgs e)
        {
            if (ApplicationSettings.SaveSettings(clientListView, toolStripComboBoxDevicelist.Text, showLogToolStripMenuItem.Checked))
            {
                toolStripStatus.Text = "Settings saved!";
            }
        }

        private void toolStripMenuItemSelectAllAdapters_CheckStateChanged(object sender, EventArgs e) => PopulateAdapterDropDown(toolStripComboBoxDevicelist.Text);

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

        private void TryDisableArpProtection()
        {
            if (!_isArpProtectionApplied || _gatewayIpAddress == null)
            {
                return;
            }

            try
            {
                ArpProtectionService.Disable(GetSelectedInterfaceId(), _gatewayIpAddress, ResolveGatewayMacFromList());
                _isArpProtectionApplied = false;
                Log("ARP protection disabled.");
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                Log($"Unable to disable ARP protection: {ex.Message}");
            }
        }

        private void TryEnableArpProtection(PhysicalAddress gatewayPhysicalAddress)
        {
            if (_gatewayIpAddress == null)
            {
                return;
            }

            try
            {
                ArpProtectionService.Enable(GetSelectedInterfaceId(), _gatewayIpAddress, gatewayPhysicalAddress);
                _isArpProtectionApplied = true;
                Log("ARP protection enabled.");
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                Log($"Unable to enable ARP protection: {ex.Message}");
            }
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

            try
            {
                _sourceIpAddress = selectedDevice.ReadCurrentIpV4Address();
            }
            catch (InvalidOperationException)
            {
                _ = MessageBox.Show("Could not read IPv4 address for the selected adapter.", "Adapter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            _sourceMacAddress = selectedDevice.MacAddress;

            return true;
        }

        private void UpdateUiState()
        {
            var hasSingleSelection = clientListView.SelectedItems.Count == 1;
            var hasSpoofing = _arpSpoofer.IsSpoofing;
            var hasScan = _networkScanner.IsScanning;
            var hasSpoofableSelection = clientListView.SelectedItems
                .Cast<ListViewItem>()
                .Any(selectedItem => !IsProtectedTarget(selectedItem));

            cutoffToolStripMenuItem.Enabled = hasSpoofableSelection && !hasScan && !hasSpoofing;
            reconnectToolStripMenuItem.Enabled = hasSpoofing;
            stopNetworkScanToolStripMenuItem.Enabled = hasScan;
            toolStripMenuItemRefreshClients.Enabled = !hasScan;
            ClientNametoolStripMenuItem.Enabled = hasSingleSelection && !hasSpoofing;
            toolStripTextBoxClientName.Enabled = hasSingleSelection && !hasSpoofing;

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
    }
}
