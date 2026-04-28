using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using CSArp.Model;
using CSArp.Model.Utilities;

namespace CSArp.View
{
    public partial class ScannerForm : Form, IView
    {
        private readonly Presenter.Presenter _controller;
        public ScannerForm()
        {
            InitializeComponent();
            _controller = new Presenter.Presenter(this);
            DebugOutput.Init(this);
        }

        #region IView members
        public ListView ClientListView
        {
            get
            {
                return clientListView;
            }
        }
        public ToolStripStatusLabel ToolStripStatus
        {
            get
            {
                return toolStripStatus;
            }
        }
        public ToolStripComboBox ToolStripComboBoxNetworkDeviceList
        {
            get
            {
                return toolStripComboBoxDevicelist;
            }
        }
        public Form MainForm
        {
            get
            {
                return this;
            }
        }
        public NotifyIcon NotifyIcon1
        {
            get
            {
                return notifyIcon1;
            }
        }
        public ToolStripTextBox ToolStripTextBoxClientName
        {
            get
            {
                return toolStripTextBoxClientName;
            }
        }
        public ToolStripStatusLabel ToolStripStatusScan
        {
            get
            {
                return toolStripStatusScan;
            }
        }
        public ToolStripProgressBar ToolStripProgressBarScan
        {
            get
            {
                return toolStripProgressBarScan;
            }
        }
        public ToolStripMenuItem ShowLogToolStripMenuItem
        {
            get
            {
                return showLogToolStripMenuItem;
            }
        }
        public RichTextBox LogRichTextBox
        {
            get
            {
                return richTextBoxLog;
            }
        }
        public SaveFileDialog SaveFileDialogLog
        {
            get
            {
                return saveFileDialog1;
            }
        }
        #endregion
        #region Event based methods
        private void toolStripMenuItemRefreshClients_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ToolStripComboBoxNetworkDeviceList.Text))
            {
                _ = MessageBox.Show("Pick a device before a scan.");
            }
            else
            {
                if (_controller.IsNetworkScanActive())
                {
                    ToolStripStatus.Text = "A scan is already running.";
                    return;
                }

                _controller.SelectedInterfaceFriendlyName = ToolStripComboBoxNetworkDeviceList.Text;
                _controller.GetGatewayInformation();
                _controller.StartCapture();
                _controller.StartNetworkScan();
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
            _controller.DisconnectSelectedClients();
        }

        private void reconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _controller.ReconnectClients();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (MainForm.WindowState == FormWindowState.Minimized)
            {
                NotifyIcon1.Visible = true;
                MainForm.Hide();
            }
        }

        private void toolStripTextBoxClientName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (ClientListView.SelectedItems.Count == 1)
                {
                    ClientListView.SelectedItems[0].SubItems[3].Text = ToolStripTextBoxClientName.Text;
                    ToolStripTextBoxClientName.Text = "";
                }
            }
        }

        private void toolStripMenuItemMinimize_Click(object sender, EventArgs e)
        {
            MainForm.WindowState = FormWindowState.Minimized;
        }

        private void toolStripMenuItemSaveSettings_Click(object sender, EventArgs e)
        {
            if (ApplicationSettings.SaveSettings(ClientListView, ToolStripComboBoxNetworkDeviceList.Text))
            {
                ToolStripStatus.Text = "Settings saved!";
            }
        }

        private void showLogToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (ShowLogToolStripMenuItem.Checked == false)
            {
                LogRichTextBox.Visible = false;
                ClientListView.Height = MainForm.Height - 93;
            }
            else
            {
                LogRichTextBox.Visible = true;
                ClientListView.Height = MainForm.Height - 184;
            }
        }

        private void saveStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveLog();
        }

        private void clearStripMenuItem_Click(object sender, EventArgs e)
        {
            LogRichTextBox.Text = "";
        }
        private void notifyIcon1_OnMouseClick(object sender, EventArgs e)
        {
            NotifyIcon1.Visible = false;
            MainForm.Show();
            MainForm.WindowState = FormWindowState.Normal;
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Populate the available network cards. Excludes bridged network adapters, since they are not applicable to spoofing scenario
        /// <see cref="https://github.com/chmorgan/sharppcap/issues/57"/>
        /// </summary>
        private void EnumerateNetworkAdaptersforMenu()
        {
            ToolStripComboBoxNetworkDeviceList.Items.AddRange(EnumerateNetworkAdapters());
        }

        /// <summary>
        /// Sets the text of interface list combobox to saved value if present
        /// </summary>
        private void SetSavedInterface()
        {
            ToolStripComboBoxNetworkDeviceList.Text = ApplicationSettings.GetSavedPreferredInterfaceFriendlyName() ?? string.Empty;
        }

        private void SaveLog()
        {
            SaveFileDialogLog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            SaveFileDialogLog.InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SaveFileDialogLog.FileName = "CSArp-log";
            SaveFileDialogLog.FileOk += (object sender, System.ComponentModel.CancelEventArgs e) =>
            {
                if (SaveFileDialogLog.FileName != "" && !File.Exists(SaveFileDialogLog.FileName))
                {
                    try
                    {
                        File.WriteAllText(SaveFileDialogLog.FileName, LogRichTextBox.Text);
                        DebugOutput.Print("Log saved to " + SaveFileDialogLog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            _ = SaveFileDialogLog.ShowDialog();
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
            _controller.StopCapture();
        }
        #endregion

        private void stopNetworkScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _controller.StopNetworkScan();
            ToolStripStatus.Text = "Scan stopped!";
        }
    }
}
