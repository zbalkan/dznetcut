using System;
using System.Windows.Forms;

namespace dznetcut.GUI
{
    partial class ScannerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                ExitGracefully();
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ScannerForm));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemSaveSettings = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemChooseInterface = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripComboBoxDevicelist = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripMenuItemSelectAllAdapters = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemArpProtection = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStealthMode = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.cutoffToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reconnectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemRefreshClients = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.commandLineParametersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutdznetcutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelSpringer = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusScan = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripProgressBarScan = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripSplitButton1 = new System.Windows.Forms.ToolStripSplitButton();
            this.clearStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLogToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clientListView = new System.Windows.Forms.ListView();
            this.columnHeaderIP = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderMAC = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderCutoffStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderClientname = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.stopNetworkScanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(570, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemSaveSettings,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // toolStripMenuItemSaveSettings
            // 
            this.toolStripMenuItemSaveSettings.Name = "toolStripMenuItemSaveSettings";
            this.toolStripMenuItemSaveSettings.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.toolStripMenuItemSaveSettings.Size = new System.Drawing.Size(163, 22);
            this.toolStripMenuItemSaveSettings.Text = "Save";
            this.toolStripMenuItemSaveSettings.ToolTipText = "Save current settings";
            this.toolStripMenuItemSaveSettings.Click += new System.EventHandler(this.toolStripMenuItemSaveSettings_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Q)));
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemChooseInterface,
            this.toolStripSeparator1,
            this.toolStripMenuItemSelectAllAdapters,
            this.toolStripMenuItemArpProtection,
            this.toolStripMenuItemStealthMode,
            this.toolStripSeparator2,
            this.cutoffToolStripMenuItem,
            this.reconnectToolStripMenuItem,

            this.toolStripSeparator3,
            this.toolStripMenuItemRefreshClients,
            this.stopNetworkScanToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // toolStripMenuItemChooseInterface
            // 
            this.toolStripMenuItemChooseInterface.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripComboBoxDevicelist});
            this.toolStripMenuItemChooseInterface.Name = "toolStripMenuItemChooseInterface";
            this.toolStripMenuItemChooseInterface.Size = new System.Drawing.Size(193, 22);
            this.toolStripMenuItemChooseInterface.Text = "Choose interface";
            this.toolStripMenuItemChooseInterface.ToolTipText = "Select the network card to operate on";
            // 
            // toolStripComboBoxDevicelist
            // 
            this.toolStripComboBoxDevicelist.Name = "toolStripComboBoxDevicelist";
            this.toolStripComboBoxDevicelist.Size = new System.Drawing.Size(121, 23);
            // 
            // toolStripMenuItemSelectAllAdapters
            // 
            this.toolStripMenuItemSelectAllAdapters.CheckOnClick = true;
            this.toolStripMenuItemSelectAllAdapters.Name = "toolStripMenuItemSelectAllAdapters";
            this.toolStripMenuItemSelectAllAdapters.Size = new System.Drawing.Size(193, 22);
            this.toolStripMenuItemSelectAllAdapters.Text = "Show All Adapters";
            this.toolStripMenuItemSelectAllAdapters.ToolTipText = "Include virtual adapters in addition to physical adapters";
            this.toolStripMenuItemSelectAllAdapters.CheckStateChanged += new System.EventHandler(this.toolStripMenuItemSelectAllAdapters_CheckStateChanged);
            // 
            // 
            // toolStripMenuItemArpProtection
            // 
            this.toolStripMenuItemArpProtection.Checked = true;
            this.toolStripMenuItemArpProtection.CheckOnClick = true;
            this.toolStripMenuItemArpProtection.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolStripMenuItemArpProtection.Name = "toolStripMenuItemArpProtection";
            this.toolStripMenuItemArpProtection.Size = new System.Drawing.Size(193, 22);
            this.toolStripMenuItemArpProtection.Text = "Enable ARP Protection";
            this.toolStripMenuItemArpProtection.ToolTipText = "Pin the gateway ARP entry while traffic cut is active";
            this.toolStripMenuItemArpProtection.CheckStateChanged += new System.EventHandler(this.toolStripMenuItemArpProtection_CheckStateChanged);
            // 
            // toolStripMenuItemStealthMode
            // 
            this.toolStripMenuItemStealthMode.CheckOnClick = true;
            this.toolStripMenuItemStealthMode.Name = "toolStripMenuItemStealthMode";
            this.toolStripMenuItemStealthMode.Size = new System.Drawing.Size(193, 22);
            this.toolStripMenuItemStealthMode.Text = "Stealth mode";
            this.toolStripMenuItemStealthMode.ToolTipText = "Use slower randomized discovery pacing.";
            this.toolStripMenuItemStealthMode.CheckStateChanged += new System.EventHandler(this.toolStripMenuItemStealthMode_CheckStateChanged);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(190, 6);
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(190, 6);
            // 
            // cutoffToolStripMenuItem
            // 
            this.cutoffToolStripMenuItem.Name = "cutoffToolStripMenuItem";
            this.cutoffToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.cutoffToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.cutoffToolStripMenuItem.Text = "Cut Traffic";
            this.cutoffToolStripMenuItem.ToolTipText = "Disconnect selected clients";
            this.cutoffToolStripMenuItem.Click += new System.EventHandler(this.cutoffToolStripMenuItem_Click);
            // 
            // reconnectToolStripMenuItem
            // 
            this.reconnectToolStripMenuItem.Name = "reconnectToolStripMenuItem";
            this.reconnectToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.reconnectToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.reconnectToolStripMenuItem.Text = "Restore Traffic";
            this.reconnectToolStripMenuItem.ToolTipText = "Restore normal ARP traffic";
            this.reconnectToolStripMenuItem.Click += new System.EventHandler(this.reconnectToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(190, 6);
            // 
            // toolStripMenuItemRefreshClients
            // 
            this.toolStripMenuItemRefreshClients.Name = "toolStripMenuItemRefreshClients";
            this.toolStripMenuItemRefreshClients.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.toolStripMenuItemRefreshClients.Size = new System.Drawing.Size(193, 22);
            this.toolStripMenuItemRefreshClients.Text = "Start Network Scan";
            this.toolStripMenuItemRefreshClients.ToolTipText = "Refresh active client list";
            this.toolStripMenuItemRefreshClients.Click += new System.EventHandler(this.toolStripMenuItemRefreshClients_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.commandLineParametersToolStripMenuItem,
            this.aboutdznetcutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // commandLineParametersToolStripMenuItem
            // 
            this.commandLineParametersToolStripMenuItem.Name = "commandLineParametersToolStripMenuItem";
            this.commandLineParametersToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            this.commandLineParametersToolStripMenuItem.Text = "Command line parameters";
            this.commandLineParametersToolStripMenuItem.Click += new System.EventHandler(this.commandLineParametersToolStripMenuItem_Click);
            // 
            // aboutdznetcutToolStripMenuItem
            // 
            this.aboutdznetcutToolStripMenuItem.Name = "aboutdznetcutToolStripMenuItem";
            this.aboutdznetcutToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            this.aboutdznetcutToolStripMenuItem.Text = "About dznetcut";
            this.aboutdznetcutToolStripMenuItem.Click += new System.EventHandler(this.aboutdznetcutToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatus,
            this.toolStripStatusLabelSpringer,
            this.toolStripStatusScan,
            this.toolStripProgressBarScan,
            this.toolStripSplitButton1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 315);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(570, 24);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatus
            // 
            this.toolStripStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right)));
            this.toolStripStatus.Margin = new System.Windows.Forms.Padding(11, 3, 0, 2);
            this.toolStripStatus.Name = "toolStripStatus";
            this.toolStripStatus.Size = new System.Drawing.Size(43, 19);
            this.toolStripStatus.Text = "Ready";
            // 
            // toolStripStatusLabelSpringer
            // 
            this.toolStripStatusLabelSpringer.Name = "toolStripStatusLabelSpringer";
            this.toolStripStatusLabelSpringer.Size = new System.Drawing.Size(261, 19);
            this.toolStripStatusLabelSpringer.Spring = true;
            // 
            // toolStripStatusScan
            // 
            this.toolStripStatusScan.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right)));
            this.toolStripStatusScan.Name = "toolStripStatusScan";
            this.toolStripStatusScan.Size = new System.Drawing.Size(95, 19);
            this.toolStripStatusScan.Text = "Refresh for scan";
            // 
            // toolStripProgressBarScan
            // 
            this.toolStripProgressBarScan.Name = "toolStripProgressBarScan";
            this.toolStripProgressBarScan.Size = new System.Drawing.Size(100, 18);
            // 
            // toolStripSplitButton1
            // 
            this.toolStripSplitButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripSplitButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clearStripMenuItem,
            this.saveStripMenuItem,
            this.showLogToolStripMenuItem});
            this.toolStripSplitButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSplitButton1.Name = "toolStripSplitButton1";
            this.toolStripSplitButton1.Size = new System.Drawing.Size(43, 22);
            this.toolStripSplitButton1.Text = "Log";
            // 
            // clearStripMenuItem
            // 
            this.clearStripMenuItem.Name = "clearStripMenuItem";
            this.clearStripMenuItem.Size = new System.Drawing.Size(123, 22);
            this.clearStripMenuItem.Text = "Clear";
            this.clearStripMenuItem.Click += new System.EventHandler(this.clearStripMenuItem_Click);
            // 
            // saveStripMenuItem
            // 
            this.saveStripMenuItem.Name = "saveStripMenuItem";
            this.saveStripMenuItem.Size = new System.Drawing.Size(123, 22);
            this.saveStripMenuItem.Text = "Save";
            this.saveStripMenuItem.Click += new System.EventHandler(this.saveStripMenuItem_Click);
            // 
            // showLogToolStripMenuItem
            // 
            this.showLogToolStripMenuItem.CheckOnClick = true;
            this.showLogToolStripMenuItem.Name = "showLogToolStripMenuItem";
            this.showLogToolStripMenuItem.Size = new System.Drawing.Size(123, 22);
            this.showLogToolStripMenuItem.Text = "Show log";
            this.showLogToolStripMenuItem.CheckStateChanged += new System.EventHandler(this.showLogToolStripMenuItem_CheckStateChanged);
            // 
            // clientListView
            // 
            this.clientListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.clientListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderIP,
            this.columnHeaderMAC,
            this.columnHeaderCutoffStatus,
            this.columnHeaderClientname});
            this.clientListView.FullRowSelect = true;
            this.clientListView.GridLines = true;
            this.clientListView.HideSelection = false;
            this.clientListView.Location = new System.Drawing.Point(12, 27);
            this.clientListView.Name = "clientListView";
            this.clientListView.ShowItemToolTips = true;
            this.clientListView.Size = new System.Drawing.Size(546, 285);
            this.clientListView.TabIndex = 2;
            this.clientListView.UseCompatibleStateImageBehavior = false;
            this.clientListView.View = System.Windows.Forms.View.Details;
            this.clientListView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.clientListView_ItemSelectionChanged);
            this.clientListView.DoubleClick += new System.EventHandler(this.clientListView_DoubleClick);
            this.clientListView.Resize += new System.EventHandler(this.clientListView_Resize);
            // 
            // columnHeaderIP
            // 
            this.columnHeaderIP.Text = "IP Address";
            this.columnHeaderIP.Width = 131;
            // 
            // columnHeaderMAC
            // 
            this.columnHeaderMAC.Text = "MAC Address";
            this.columnHeaderMAC.Width = 151;
            // 
            // columnHeaderCutoffStatus
            // 
            this.columnHeaderCutoffStatus.Text = "Traffic";
            this.columnHeaderCutoffStatus.Width = 55;
            // 
            // columnHeaderClientname
            // 
            this.columnHeaderClientname.Text = "Client Name";
            this.columnHeaderClientname.Width = 151;
            //
            // notifyIcon1
            // 
            this.notifyIcon1.Text = "dznetcut";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_OnMouseClick);
            // 
            // richTextBoxLog
            // 
            this.richTextBoxLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBoxLog.Location = new System.Drawing.Point(12, 227);
            this.richTextBoxLog.Name = "richTextBoxLog";
            this.richTextBoxLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.richTextBoxLog.Size = new System.Drawing.Size(547, 87);
            this.richTextBoxLog.TabIndex = 3;
            this.richTextBoxLog.Text = "";
            this.richTextBoxLog.Visible = false;
            // 
            // stopNetworkScanToolStripMenuItem
            // 
            this.stopNetworkScanToolStripMenuItem.Name = "stopNetworkScanToolStripMenuItem";
            this.stopNetworkScanToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this.stopNetworkScanToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.stopNetworkScanToolStripMenuItem.Text = "Stop Network Scan";
            this.stopNetworkScanToolStripMenuItem.ToolTipText = "Stop scanning";
            this.stopNetworkScanToolStripMenuItem.Click += new System.EventHandler(this.stopNetworkScanToolStripMenuItem_Click);
            // 
            // ScannerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(570, 339);
            this.Controls.Add(this.richTextBoxLog);
            this.Controls.Add(this.clientListView);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "ScannerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "dznetcut";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem commandLineParametersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutdznetcutToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatus;
        private System.Windows.Forms.ListView clientListView;
        private System.Windows.Forms.ColumnHeader columnHeaderIP;
        private System.Windows.Forms.ColumnHeader columnHeaderMAC;
        private System.Windows.Forms.ColumnHeader columnHeaderCutoffStatus;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cutoffToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reconnectToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ColumnHeader columnHeaderClientname;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSaveSettings;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemChooseInterface;
        private System.Windows.Forms.ToolStripComboBox toolStripComboBoxDevicelist;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSelectAllAdapters;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemArpProtection;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStealthMode;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRefreshClients;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelSpringer;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusScan;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBarScan;
        private System.Windows.Forms.ToolStripSplitButton toolStripSplitButton1;
        private System.Windows.Forms.ToolStripMenuItem showLogToolStripMenuItem;
        private System.Windows.Forms.RichTextBox richTextBoxLog;
        private System.Windows.Forms.ToolStripMenuItem saveStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearStripMenuItem;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private ToolStripMenuItem stopNetworkScanToolStripMenuItem;
    }
}
