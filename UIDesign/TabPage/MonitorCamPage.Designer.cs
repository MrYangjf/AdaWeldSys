﻿﻿﻿﻿﻿namespace AdaWeldSystem.UIDesign.TabPage
{
    partial class MonitorCamPage
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
            AntdUI.Tabs.StyleCard styleCard1 = new AntdUI.Tabs.StyleCard();
            this.tabsMonitor = new AntdUI.Tabs();
            this.tabPageCamSetting = new AntdUI.TabPage();
            this.tableLayoutPanelCam = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutConn = new System.Windows.Forms.TableLayoutPanel();
            this.alert_ConnStatus = new AntdUI.Alert();
            this.lblIP = new AntdUI.Label();
            this.txtIP = new AntdUI.Input();
            this.btnScan = new AntdUI.Button();
            this.btnConnect = new AntdUI.Button();
            this.imageBox1 = new Emgu.CV.UI.ImageBox();
            this.panelRight = new System.Windows.Forms.TableLayoutPanel();
            this.grpCapture = new System.Windows.Forms.GroupBox();
            this.tableLayoutCapture = new System.Windows.Forms.TableLayoutPanel();
            this.btnStartCapture = new AntdUI.Button();
            this.btnStopCapture = new AntdUI.Button();
            this.grpRecord = new System.Windows.Forms.GroupBox();
            this.tableLayoutRecord = new System.Windows.Forms.TableLayoutPanel();
            this.txtRecordDir = new AntdUI.Input();
            this.btnBrowseRecord = new AntdUI.Button();
            this.btnStartRecord = new AntdUI.Button();
            this.btnStopRecord = new AntdUI.Button();
            this.grpParam = new System.Windows.Forms.GroupBox();
            this.tableLayoutParam = new System.Windows.Forms.TableLayoutPanel();
            this.lblExposure = new AntdUI.Label();
            this.txtExposure = new AntdUI.Input();
            this.lblGain = new AntdUI.Label();
            this.txtGain = new AntdUI.Input();
            this.lblAlignWarn = new AntdUI.Label();
            this.txtAlignWarn = new AntdUI.Input();
            this.lblAlignFail = new AntdUI.Label();
            this.txtAlignFail = new AntdUI.Input();
            this.btnApplyParam = new AntdUI.Button();
            this.btnSaveAlign = new AntdUI.Button();
            this.grpCalib = new System.Windows.Forms.GroupBox();
            this.tableLayoutCalib = new System.Windows.Forms.TableLayoutPanel();
            this.lblMmPerPixel = new AntdUI.Label();
            this.txtMmPerPixel = new AntdUI.Input();
            this.txtSpotX = new AntdUI.Input();
            this.lblSpotY = new AntdUI.Label();
            this.txtSpotY = new AntdUI.Input();
            this.btnCalibSave = new AntdUI.Button();
            this.btnFeedTest = new AntdUI.Button();
            this.lblSpotX = new AntdUI.Label();
            this.tabPagePlayback = new AntdUI.TabPage();
            this.gridPlayPage = new AntdUI.GridPanel();
            this.playTable = new AntdUI.Table();
            this.imageBoxPlay = new Emgu.CV.UI.ImageBox();
            this.panelPlayBar = new AntdUI.Panel();
            this.gridPlayBar = new AntdUI.GridPanel();
            this.flowPlayButtons = new AntdUI.FlowPanel();
            this.btnFastPlay = new AntdUI.Button();
            this.btnForward5Play = new AntdUI.Button();
            this.btnPlayPause = new AntdUI.Button();
            this.btnBack5Play = new AntdUI.Button();
            this.btnSlowPlay = new AntdUI.Button();
            this.gridPlayTime = new AntdUI.GridPanel();
            this.lblPlayTime = new AntdUI.Label();
            this.lblPlaySpeed = new AntdUI.Label();
            this.sliderPlay = new AntdUI.Slider();
            this.gridPlayFilter = new AntdUI.GridPanel();
            this.btnRefreshPlay = new AntdUI.Button();
            this.uiPlayDateRange = new AntdUI.DatePickerRange();
            this.label1 = new AntdUI.Label();
            this.timerStatus = new System.Windows.Forms.Timer(this.components);
            this.tabsMonitor.SuspendLayout();
            this.tabPageCamSetting.SuspendLayout();
            this.tableLayoutPanelCam.SuspendLayout();
            this.tableLayoutConn.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).BeginInit();
            this.panelRight.SuspendLayout();
            this.grpCapture.SuspendLayout();
            this.tableLayoutCapture.SuspendLayout();
            this.grpRecord.SuspendLayout();
            this.tableLayoutRecord.SuspendLayout();
            this.grpParam.SuspendLayout();
            this.tableLayoutParam.SuspendLayout();
            this.grpCalib.SuspendLayout();
            this.tableLayoutCalib.SuspendLayout();
            this.tabPagePlayback.SuspendLayout();
            this.gridPlayPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageBoxPlay)).BeginInit();
            this.imageBoxPlay.SuspendLayout();
            this.panelPlayBar.SuspendLayout();
            this.gridPlayBar.SuspendLayout();
            this.flowPlayButtons.SuspendLayout();
            this.gridPlayTime.SuspendLayout();
            this.gridPlayFilter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabsMonitor
            // 
            this.tabsMonitor.Controls.Add(this.tabPageCamSetting);
            this.tabsMonitor.Controls.Add(this.tabPagePlayback);
            this.tabsMonitor.Cursor = System.Windows.Forms.Cursors.Default;
            this.tabsMonitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsMonitor.Location = new System.Drawing.Point(0, 0);
            this.tabsMonitor.Name = "tabsMonitor";
            this.tabsMonitor.Pages.Add(this.tabPageCamSetting);
            this.tabsMonitor.Pages.Add(this.tabPagePlayback);
            this.tabsMonitor.Size = new System.Drawing.Size(1762, 920);
            this.tabsMonitor.Style = styleCard1;
            this.tabsMonitor.TabIndex = 0;
            this.tabsMonitor.Text = "监控相机";
            this.tabsMonitor.Type = AntdUI.TabType.Card;
            // 
            // tabPageCamSetting
            // 
            this.tabPageCamSetting.Controls.Add(this.tableLayoutPanelCam);
            this.tabPageCamSetting.Location = new System.Drawing.Point(0, 31);
            this.tabPageCamSetting.Name = "tabPageCamSetting";
            this.tabPageCamSetting.Size = new System.Drawing.Size(1762, 889);
            this.tabPageCamSetting.TabIndex = 0;
            this.tabPageCamSetting.Text = "焊接相机设置";
            // 
            // tableLayoutPanelCam
            // 
            this.tableLayoutPanelCam.ColumnCount = 2;
            this.tableLayoutPanelCam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelCam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 400F));
            this.tableLayoutPanelCam.Controls.Add(this.tableLayoutConn, 0, 0);
            this.tableLayoutPanelCam.Controls.Add(this.imageBox1, 0, 1);
            this.tableLayoutPanelCam.Controls.Add(this.panelRight, 1, 1);
            this.tableLayoutPanelCam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelCam.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelCam.Name = "tableLayoutPanelCam";
            this.tableLayoutPanelCam.RowCount = 2;
            this.tableLayoutPanelCam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.tableLayoutPanelCam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelCam.Size = new System.Drawing.Size(1762, 889);
            this.tableLayoutPanelCam.TabIndex = 0;
            // 
            // tableLayoutConn
            // 
            this.tableLayoutConn.BackColor = System.Drawing.Color.Transparent;
            this.tableLayoutConn.ColumnCount = 6;
            this.tableLayoutPanelCam.SetColumnSpan(this.tableLayoutConn, 2);
            this.tableLayoutConn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutConn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutConn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 79F));
            this.tableLayoutConn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 253F));
            this.tableLayoutConn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutConn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutConn.Controls.Add(this.alert_ConnStatus, 0, 0);
            this.tableLayoutConn.Controls.Add(this.lblIP, 2, 0);
            this.tableLayoutConn.Controls.Add(this.txtIP, 3, 0);
            this.tableLayoutConn.Controls.Add(this.btnScan, 4, 0);
            this.tableLayoutConn.Controls.Add(this.btnConnect, 5, 0);
            this.tableLayoutConn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutConn.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutConn.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutConn.Name = "tableLayoutConn";
            this.tableLayoutConn.RowCount = 1;
            this.tableLayoutConn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutConn.Size = new System.Drawing.Size(1762, 45);
            this.tableLayoutConn.TabIndex = 20;
            // 
            // alert_ConnStatus
            // 
            this.alert_ConnStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.alert_ConnStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.alert_ConnStatus.Icon = AntdUI.TType.Error;
            this.alert_ConnStatus.Location = new System.Drawing.Point(6, 5);
            this.alert_ConnStatus.Margin = new System.Windows.Forms.Padding(6, 5, 4, 5);
            this.alert_ConnStatus.MinimumSize = new System.Drawing.Size(1, 1);
            this.alert_ConnStatus.Name = "alert_ConnStatus";
            this.alert_ConnStatus.Size = new System.Drawing.Size(110, 35);
            this.alert_ConnStatus.TabIndex = 21;
            this.alert_ConnStatus.Text = "未连接";
            // 
            // lblIP
            // 
            this.lblIP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblIP.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblIP.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblIP.Location = new System.Drawing.Point(1274, 5);
            this.lblIP.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(71, 35);
            this.lblIP.TabIndex = 22;
            this.lblIP.Text = "相机IP";
            // 
            // txtIP
            // 
            this.txtIP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtIP.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtIP.Location = new System.Drawing.Point(1353, 5);
            this.txtIP.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtIP.MinimumSize = new System.Drawing.Size(1, 1);
            this.txtIP.Name = "txtIP";
            this.txtIP.Size = new System.Drawing.Size(245, 35);
            this.txtIP.TabIndex = 23;
            this.txtIP.Text = "192.168.1.100";
            // 
            // btnScan
            // 
            this.btnScan.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnScan.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnScan.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnScan.Ghost = true;
            this.btnScan.IconSvg = "SecurityScanOutlined";
            this.btnScan.Location = new System.Drawing.Point(1602, 5);
            this.btnScan.Margin = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.btnScan.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(80, 35);
            this.btnScan.TabIndex = 24;
            this.btnScan.Text = "扫描";
            // 
            // btnConnect
            // 
            this.btnConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConnect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConnect.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnect.IconSvg = "ApiOutlined";
            this.btnConnect.Location = new System.Drawing.Point(1682, 5);
            this.btnConnect.Margin = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.btnConnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(80, 35);
            this.btnConnect.TabIndex = 25;
            this.btnConnect.Text = "连接";
            this.btnConnect.Type = AntdUI.TTypeMini.Error;
            // 
            // imageBox1
            // 
            this.imageBox1.BackColor = System.Drawing.Color.Silver;
            this.imageBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageBox1.FunctionalMode = Emgu.CV.UI.ImageBox.FunctionalModeOption.RightClickMenu;
            this.imageBox1.Location = new System.Drawing.Point(3, 48);
            this.imageBox1.Name = "imageBox1";
            this.imageBox1.Size = new System.Drawing.Size(1356, 838);
            this.imageBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.imageBox1.TabIndex = 1;
            this.imageBox1.TabStop = false;
            // 
            // panelRight
            // 
            this.panelRight.ColumnCount = 1;
            this.panelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelRight.Controls.Add(this.grpCapture, 0, 0);
            this.panelRight.Controls.Add(this.grpRecord, 0, 1);
            this.panelRight.Controls.Add(this.grpParam, 0, 2);
            this.panelRight.Controls.Add(this.grpCalib, 0, 3);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(1362, 48);
            this.panelRight.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.RowCount = 4;
            this.panelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 78F));
            this.panelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.panelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 198F));
            this.panelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 280F));
            this.panelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.panelRight.Size = new System.Drawing.Size(397, 838);
            this.panelRight.TabIndex = 2;
            // 
            // grpCapture
            // 
            this.grpCapture.Controls.Add(this.tableLayoutCapture);
            this.grpCapture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpCapture.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpCapture.Location = new System.Drawing.Point(3, 3);
            this.grpCapture.Name = "grpCapture";
            this.grpCapture.Size = new System.Drawing.Size(391, 72);
            this.grpCapture.TabIndex = 0;
            this.grpCapture.TabStop = false;
            this.grpCapture.Text = "采集";
            // 
            // tableLayoutCapture
            // 
            this.tableLayoutCapture.ColumnCount = 2;
            this.tableLayoutCapture.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutCapture.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutCapture.Controls.Add(this.btnStartCapture, 0, 0);
            this.tableLayoutCapture.Controls.Add(this.btnStopCapture, 1, 0);
            this.tableLayoutCapture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutCapture.Location = new System.Drawing.Point(3, 29);
            this.tableLayoutCapture.Name = "tableLayoutCapture";
            this.tableLayoutCapture.RowCount = 1;
            this.tableLayoutCapture.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutCapture.Size = new System.Drawing.Size(385, 40);
            this.tableLayoutCapture.TabIndex = 0;
            // 
            // btnStartCapture
            // 
            this.btnStartCapture.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartCapture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnStartCapture.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartCapture.Location = new System.Drawing.Point(3, 3);
            this.btnStartCapture.Name = "btnStartCapture";
            this.btnStartCapture.Size = new System.Drawing.Size(186, 34);
            this.btnStartCapture.TabIndex = 0;
            this.btnStartCapture.Text = "开始采集";
            // 
            // btnStopCapture
            // 
            this.btnStopCapture.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStopCapture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnStopCapture.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopCapture.Location = new System.Drawing.Point(195, 3);
            this.btnStopCapture.Name = "btnStopCapture";
            this.btnStopCapture.Size = new System.Drawing.Size(187, 34);
            this.btnStopCapture.TabIndex = 1;
            this.btnStopCapture.Text = "停止采集";
            // 
            // grpRecord
            // 
            this.grpRecord.Controls.Add(this.tableLayoutRecord);
            this.grpRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpRecord.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpRecord.Location = new System.Drawing.Point(3, 81);
            this.grpRecord.Name = "grpRecord";
            this.grpRecord.Size = new System.Drawing.Size(391, 144);
            this.grpRecord.TabIndex = 1;
            this.grpRecord.TabStop = false;
            this.grpRecord.Text = "录像";
            // 
            // tableLayoutRecord
            // 
            this.tableLayoutRecord.ColumnCount = 2;
            this.tableLayoutRecord.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutRecord.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutRecord.Controls.Add(this.txtRecordDir, 0, 0);
            this.tableLayoutRecord.Controls.Add(this.btnBrowseRecord, 0, 1);
            this.tableLayoutRecord.Controls.Add(this.btnStartRecord, 0, 2);
            this.tableLayoutRecord.Controls.Add(this.btnStopRecord, 1, 2);
            this.tableLayoutRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutRecord.Location = new System.Drawing.Point(3, 29);
            this.tableLayoutRecord.Name = "tableLayoutRecord";
            this.tableLayoutRecord.RowCount = 3;
            this.tableLayoutRecord.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutRecord.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutRecord.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutRecord.Size = new System.Drawing.Size(385, 112);
            this.tableLayoutRecord.TabIndex = 0;
            // 
            // txtRecordDir
            // 
            this.tableLayoutRecord.SetColumnSpan(this.txtRecordDir, 2);
            this.txtRecordDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRecordDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRecordDir.Location = new System.Drawing.Point(3, 3);
            this.txtRecordDir.Name = "txtRecordDir";
            this.txtRecordDir.ReadOnly = true;
            this.txtRecordDir.Size = new System.Drawing.Size(379, 26);
            this.txtRecordDir.TabIndex = 0;
            // 
            // btnBrowseRecord
            // 
            this.tableLayoutRecord.SetColumnSpan(this.btnBrowseRecord, 2);
            this.btnBrowseRecord.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBrowseRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBrowseRecord.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBrowseRecord.Ghost = true;
            this.btnBrowseRecord.IconSvg = "FolderOpenOutlined";
            this.btnBrowseRecord.Location = new System.Drawing.Point(3, 35);
            this.btnBrowseRecord.Name = "btnBrowseRecord";
            this.btnBrowseRecord.Size = new System.Drawing.Size(379, 32);
            this.btnBrowseRecord.TabIndex = 1;
            this.btnBrowseRecord.Text = "选择录像路径";
            // 
            // btnStartRecord
            // 
            this.btnStartRecord.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnStartRecord.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartRecord.Location = new System.Drawing.Point(3, 73);
            this.btnStartRecord.Name = "btnStartRecord";
            this.btnStartRecord.Size = new System.Drawing.Size(186, 36);
            this.btnStartRecord.TabIndex = 2;
            this.btnStartRecord.Text = "开始录像";
            // 
            // btnStopRecord
            // 
            this.btnStopRecord.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStopRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnStopRecord.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopRecord.Location = new System.Drawing.Point(195, 73);
            this.btnStopRecord.Name = "btnStopRecord";
            this.btnStopRecord.Size = new System.Drawing.Size(187, 36);
            this.btnStopRecord.TabIndex = 3;
            this.btnStopRecord.Text = "停止录像";
            // 
            // grpParam
            // 
            this.grpParam.Controls.Add(this.tableLayoutParam);
            this.grpParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpParam.Location = new System.Drawing.Point(3, 231);
            this.grpParam.Name = "grpParam";
            this.grpParam.Size = new System.Drawing.Size(391, 192);
            this.grpParam.TabIndex = 2;
            this.grpParam.TabStop = false;
            this.grpParam.Text = "参数设置";
            // 
            // tableLayoutParam
            // 
            this.tableLayoutParam.ColumnCount = 2;
            this.tableLayoutParam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.tableLayoutParam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutParam.Controls.Add(this.lblExposure, 0, 0);
            this.tableLayoutParam.Controls.Add(this.txtExposure, 1, 0);
            this.tableLayoutParam.Controls.Add(this.lblGain, 0, 1);
            this.tableLayoutParam.Controls.Add(this.txtGain, 1, 1);
            this.tableLayoutParam.Controls.Add(this.lblAlignWarn, 0, 2);
            this.tableLayoutParam.Controls.Add(this.txtAlignWarn, 1, 2);
            this.tableLayoutParam.Controls.Add(this.lblAlignFail, 0, 3);
            this.tableLayoutParam.Controls.Add(this.txtAlignFail, 1, 3);
            this.tableLayoutParam.Controls.Add(this.btnApplyParam, 0, 4);
            this.tableLayoutParam.Controls.Add(this.btnSaveAlign, 1, 4);
            this.tableLayoutParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutParam.Location = new System.Drawing.Point(3, 29);
            this.tableLayoutParam.Name = "tableLayoutParam";
            this.tableLayoutParam.RowCount = 5;
            this.tableLayoutParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutParam.Size = new System.Drawing.Size(385, 160);
            this.tableLayoutParam.TabIndex = 0;
            // 
            // lblExposure
            // 
            this.lblExposure.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblExposure.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblExposure.Location = new System.Drawing.Point(3, 7);
            this.lblExposure.Name = "lblExposure";
            this.lblExposure.Size = new System.Drawing.Size(109, 18);
            this.lblExposure.TabIndex = 0;
            this.lblExposure.Text = "曝光(μs)";
            // 
            // txtExposure
            // 
            this.txtExposure.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtExposure.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtExposure.Location = new System.Drawing.Point(118, 3);
            this.txtExposure.Name = "txtExposure";
            this.txtExposure.Size = new System.Drawing.Size(264, 26);
            this.txtExposure.TabIndex = 1;
            // 
            // lblGain
            // 
            this.lblGain.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblGain.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblGain.Location = new System.Drawing.Point(3, 39);
            this.lblGain.Name = "lblGain";
            this.lblGain.Size = new System.Drawing.Size(109, 18);
            this.lblGain.TabIndex = 2;
            this.lblGain.Text = "增益(dB)";
            // 
            // txtGain
            // 
            this.txtGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGain.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtGain.Location = new System.Drawing.Point(118, 35);
            this.txtGain.Name = "txtGain";
            this.txtGain.Size = new System.Drawing.Size(264, 26);
            this.txtGain.TabIndex = 3;
            // 
            // lblAlignWarn
            // 
            this.lblAlignWarn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblAlignWarn.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblAlignWarn.Location = new System.Drawing.Point(3, 71);
            this.lblAlignWarn.Name = "lblAlignWarn";
            this.lblAlignWarn.Size = new System.Drawing.Size(109, 18);
            this.lblAlignWarn.TabIndex = 5;
            this.lblAlignWarn.Text = "警告(mm)";
            // 
            // txtAlignWarn
            // 
            this.txtAlignWarn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAlignWarn.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtAlignWarn.Location = new System.Drawing.Point(118, 67);
            this.txtAlignWarn.Name = "txtAlignWarn";
            this.txtAlignWarn.Size = new System.Drawing.Size(264, 26);
            this.txtAlignWarn.TabIndex = 6;
            this.txtAlignWarn.Text = "0.1";
            // 
            // lblAlignFail
            // 
            this.lblAlignFail.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblAlignFail.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblAlignFail.Location = new System.Drawing.Point(3, 103);
            this.lblAlignFail.Name = "lblAlignFail";
            this.lblAlignFail.Size = new System.Drawing.Size(109, 18);
            this.lblAlignFail.TabIndex = 7;
            this.lblAlignFail.Text = "报警(mm)";
            // 
            // txtAlignFail
            // 
            this.txtAlignFail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAlignFail.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtAlignFail.Location = new System.Drawing.Point(118, 99);
            this.txtAlignFail.Name = "txtAlignFail";
            this.txtAlignFail.Size = new System.Drawing.Size(264, 26);
            this.txtAlignFail.TabIndex = 8;
            this.txtAlignFail.Text = "0.3";
            // 
            // btnApplyParam
            // 
            this.btnApplyParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnApplyParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnApplyParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnApplyParam.Location = new System.Drawing.Point(3, 131);
            this.btnApplyParam.Name = "btnApplyParam";
            this.btnApplyParam.Size = new System.Drawing.Size(109, 26);
            this.btnApplyParam.TabIndex = 4;
            this.btnApplyParam.Text = "应用参数";
            // 
            // btnSaveAlign
            // 
            this.btnSaveAlign.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveAlign.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSaveAlign.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSaveAlign.Location = new System.Drawing.Point(118, 131);
            this.btnSaveAlign.Name = "btnSaveAlign";
            this.btnSaveAlign.Size = new System.Drawing.Size(264, 26);
            this.btnSaveAlign.TabIndex = 9;
            this.btnSaveAlign.Text = "保存阈值";
            // 
            // grpCalib
            // 
            this.grpCalib.Controls.Add(this.tableLayoutCalib);
            this.grpCalib.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpCalib.Location = new System.Drawing.Point(3, 429);
            this.grpCalib.Name = "grpCalib";
            this.grpCalib.Size = new System.Drawing.Size(391, 406);
            this.grpCalib.TabIndex = 3;
            this.grpCalib.TabStop = false;
            this.grpCalib.Text = "标定设置";
            // 
            // tableLayoutCalib
            // 
            this.tableLayoutCalib.ColumnCount = 2;
            this.tableLayoutCalib.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.tableLayoutCalib.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutCalib.Controls.Add(this.lblMmPerPixel, 0, 0);
            this.tableLayoutCalib.Controls.Add(this.txtMmPerPixel, 1, 0);
            this.tableLayoutCalib.Controls.Add(this.txtSpotX, 1, 1);
            this.tableLayoutCalib.Controls.Add(this.lblSpotY, 0, 2);
            this.tableLayoutCalib.Controls.Add(this.txtSpotY, 1, 2);
            this.tableLayoutCalib.Controls.Add(this.lblSpotX, 0, 1);
            this.tableLayoutCalib.Controls.Add(this.btnCalibSave, 0, 3);
            this.tableLayoutCalib.Controls.Add(this.btnFeedTest, 1, 3);
            this.tableLayoutCalib.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutCalib.Location = new System.Drawing.Point(3, 29);
            this.tableLayoutCalib.Name = "tableLayoutCalib";
            this.tableLayoutCalib.RowCount = 5;
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutCalib.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutCalib.Size = new System.Drawing.Size(385, 374);
            this.tableLayoutCalib.TabIndex = 0;
            // 
            // lblMmPerPixel
            // 
            this.lblMmPerPixel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMmPerPixel.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblMmPerPixel.Location = new System.Drawing.Point(3, 7);
            this.lblMmPerPixel.Name = "lblMmPerPixel";
            this.lblMmPerPixel.Size = new System.Drawing.Size(109, 18);
            this.lblMmPerPixel.TabIndex = 0;
            this.lblMmPerPixel.Text = "尺度(mm/pixel)";
            // 
            // txtMmPerPixel
            // 
            this.txtMmPerPixel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtMmPerPixel.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtMmPerPixel.Location = new System.Drawing.Point(118, 3);
            this.txtMmPerPixel.Name = "txtMmPerPixel";
            this.txtMmPerPixel.Size = new System.Drawing.Size(264, 26);
            this.txtMmPerPixel.TabIndex = 1;
            // 
            // txtSpotX
            // 
            this.txtSpotX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSpotX.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtSpotX.Location = new System.Drawing.Point(118, 35);
            this.txtSpotX.Name = "txtSpotX";
            this.txtSpotX.Size = new System.Drawing.Size(264, 26);
            this.txtSpotX.TabIndex = 3;
            // 
            // lblSpotY
            // 
            this.lblSpotY.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblSpotY.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSpotY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSpotY.Location = new System.Drawing.Point(3, 71);
            this.lblSpotY.Name = "lblSpotY";
            this.lblSpotY.Size = new System.Drawing.Size(109, 18);
            this.lblSpotY.TabIndex = 4;
            this.lblSpotY.Text = "光斑点Y(px)";
            // 
            // txtSpotY
            // 
            this.txtSpotY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSpotY.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtSpotY.Location = new System.Drawing.Point(118, 67);
            this.txtSpotY.Name = "txtSpotY";
            this.txtSpotY.Size = new System.Drawing.Size(264, 26);
            this.txtSpotY.TabIndex = 5;
            // 
            // btnCalibSave
            // 
            this.btnCalibSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCalibSave.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCalibSave.Location = new System.Drawing.Point(3, 99);
            this.btnCalibSave.Name = "btnCalibSave";
            this.btnCalibSave.Size = new System.Drawing.Size(109, 24);
            this.btnCalibSave.TabIndex = 7;
            this.btnCalibSave.Text = "保存标定设置";
            // 
            // btnFeedTest
            // 
            this.btnFeedTest.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnFeedTest.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnFeedTest.Location = new System.Drawing.Point(118, 99);
            this.btnFeedTest.Name = "btnFeedTest";
            this.btnFeedTest.Size = new System.Drawing.Size(264, 24);
            this.btnFeedTest.TabIndex = 8;
            this.btnFeedTest.Text = "送丝距离测试";
            // 
            // lblSpotX
            // 
            this.lblSpotX.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblSpotX.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSpotX.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSpotX.Location = new System.Drawing.Point(3, 39);
            this.lblSpotX.Name = "lblSpotX";
            this.lblSpotX.Size = new System.Drawing.Size(109, 18);
            this.lblSpotX.TabIndex = 2;
            this.lblSpotX.Text = "光斑点X(px)";
            // 
            // tabPagePlayback
            // 
            this.tabPagePlayback.Controls.Add(this.gridPlayPage);
            this.tabPagePlayback.Location = new System.Drawing.Point(-2050, -1138);
            this.tabPagePlayback.Name = "tabPagePlayback";
            this.tabPagePlayback.Size = new System.Drawing.Size(1025, 569);
            this.tabPagePlayback.TabIndex = 1;
            this.tabPagePlayback.Text = "焊接回放";
            // 
            // gridPlayPage
            // 
            this.gridPlayPage.BackColor = System.Drawing.Color.Transparent;
            this.gridPlayPage.Controls.Add(this.playTable);
            this.gridPlayPage.Controls.Add(this.imageBoxPlay);
            this.gridPlayPage.Controls.Add(this.gridPlayFilter);
            this.gridPlayPage.Controls.Add(this.label1);
            this.gridPlayPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPlayPage.Location = new System.Drawing.Point(0, 0);
            this.gridPlayPage.Margin = new System.Windows.Forms.Padding(0);
            this.gridPlayPage.Name = "gridPlayPage";
            this.gridPlayPage.Size = new System.Drawing.Size(1025, 569);
            this.gridPlayPage.Span = "fill 400;fill 400;-45 fill";
            this.gridPlayPage.TabIndex = 0;
            // 
            // playTable
            // 
            this.playTable.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            this.playTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.playTable.Gap = 12;
            this.playTable.Location = new System.Drawing.Point(528, 59);
            this.playTable.Name = "playTable";
            this.playTable.RowHeight = 64;
            this.playTable.Size = new System.Drawing.Size(494, 507);
            this.playTable.TabIndex = 1;
            // 
            // imageBoxPlay
            // 
            this.imageBoxPlay.BackColor = System.Drawing.Color.Silver;
            this.imageBoxPlay.Controls.Add(this.panelPlayBar);
            this.imageBoxPlay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageBoxPlay.FunctionalMode = Emgu.CV.UI.ImageBox.FunctionalModeOption.RightClickMenu;
            this.imageBoxPlay.Location = new System.Drawing.Point(3, 59);
            this.imageBoxPlay.Name = "imageBoxPlay";
            this.imageBoxPlay.Size = new System.Drawing.Size(519, 507);
            this.imageBoxPlay.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.imageBoxPlay.TabIndex = 2;
            this.imageBoxPlay.TabStop = false;
            // 
            // panelPlayBar
            // 
            this.panelPlayBar.Back = System.Drawing.Color.Transparent;
            this.panelPlayBar.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelPlayBar.Controls.Add(this.gridPlayBar);
            this.panelPlayBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelPlayBar.Location = new System.Drawing.Point(0, 429);
            this.panelPlayBar.Name = "panelPlayBar";
            this.panelPlayBar.Padding = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.panelPlayBar.Shadow = 5;
            this.panelPlayBar.ShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.panelPlayBar.Size = new System.Drawing.Size(519, 78);
            this.panelPlayBar.TabIndex = 0;
            this.panelPlayBar.Visible = false;
            // 
            // gridPlayBar
            // 
            this.gridPlayBar.BackColor = System.Drawing.Color.Transparent;
            this.gridPlayBar.Controls.Add(this.flowPlayButtons);
            this.gridPlayBar.Controls.Add(this.gridPlayTime);
            this.gridPlayBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPlayBar.Location = new System.Drawing.Point(9, 6);
            this.gridPlayBar.Margin = new System.Windows.Forms.Padding(0);
            this.gridPlayBar.Name = "gridPlayBar";
            this.gridPlayBar.Size = new System.Drawing.Size(501, 66);
            this.gridPlayBar.Span = "fill;fill;-26 fill";
            this.gridPlayBar.TabIndex = 0;
            // 
            // flowPlayButtons
            // 
            this.flowPlayButtons.Align = AntdUI.TAlignFlow.Center;
            this.flowPlayButtons.BackColor = System.Drawing.Color.Transparent;
            this.flowPlayButtons.Controls.Add(this.btnFastPlay);
            this.flowPlayButtons.Controls.Add(this.btnForward5Play);
            this.flowPlayButtons.Controls.Add(this.btnPlayPause);
            this.flowPlayButtons.Controls.Add(this.btnBack5Play);
            this.flowPlayButtons.Controls.Add(this.btnSlowPlay);
            this.flowPlayButtons.Location = new System.Drawing.Point(0, 32);
            this.flowPlayButtons.Margin = new System.Windows.Forms.Padding(0);
            this.flowPlayButtons.Name = "flowPlayButtons";
            this.flowPlayButtons.Size = new System.Drawing.Size(501, 34);
            this.flowPlayButtons.TabIndex = 1;
            // 
            // btnFastPlay
            // 
            this.btnFastPlay.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnFastPlay.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnFastPlay.ForeColor = System.Drawing.Color.White;
            this.btnFastPlay.ForeHover = System.Drawing.Color.White;
            this.btnFastPlay.Ghost = true;
            this.btnFastPlay.IconSvg = "FastForwardOutlined";
            this.btnFastPlay.Location = new System.Drawing.Point(322, 3);
            this.btnFastPlay.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnFastPlay.Name = "btnFastPlay";
            this.btnFastPlay.Size = new System.Drawing.Size(52, 36);
            this.btnFastPlay.TabIndex = 4;
            // 
            // btnForward5Play
            // 
            this.btnForward5Play.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnForward5Play.Ghost = true;
            this.btnForward5Play.IconSvg = "StepForwardOutlined";
            this.btnForward5Play.Location = new System.Drawing.Point(278, 3);
            this.btnForward5Play.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnForward5Play.Name = "btnForward5Play";
            this.btnForward5Play.Size = new System.Drawing.Size(40, 36);
            this.btnForward5Play.TabIndex = 3;
            // 
            // btnPlayPause
            // 
            this.btnPlayPause.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlayPause.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnPlayPause.Ghost = true;
            this.btnPlayPause.IconSvg = "PlayCircleOutlined";
            this.btnPlayPause.Location = new System.Drawing.Point(226, 3);
            this.btnPlayPause.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnPlayPause.Name = "btnPlayPause";
            this.btnPlayPause.Size = new System.Drawing.Size(48, 36);
            this.btnPlayPause.TabIndex = 2;
            // 
            // btnBack5Play
            // 
            this.btnBack5Play.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBack5Play.Ghost = true;
            this.btnBack5Play.IconSvg = "StepBackwardOutlined";
            this.btnBack5Play.Location = new System.Drawing.Point(182, 3);
            this.btnBack5Play.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnBack5Play.Name = "btnBack5Play";
            this.btnBack5Play.Size = new System.Drawing.Size(40, 36);
            this.btnBack5Play.TabIndex = 1;
            // 
            // btnSlowPlay
            // 
            this.btnSlowPlay.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSlowPlay.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSlowPlay.ForeColor = System.Drawing.Color.White;
            this.btnSlowPlay.ForeHover = System.Drawing.Color.White;
            this.btnSlowPlay.Ghost = true;
            this.btnSlowPlay.IconSvg = "FastBackwardOutlined";
            this.btnSlowPlay.Location = new System.Drawing.Point(126, 3);
            this.btnSlowPlay.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnSlowPlay.Name = "btnSlowPlay";
            this.btnSlowPlay.Size = new System.Drawing.Size(52, 36);
            this.btnSlowPlay.TabIndex = 0;
            // 
            // gridPlayTime
            // 
            this.gridPlayTime.BackColor = System.Drawing.Color.Transparent;
            this.gridPlayTime.Controls.Add(this.lblPlayTime);
            this.gridPlayTime.Controls.Add(this.lblPlaySpeed);
            this.gridPlayTime.Controls.Add(this.sliderPlay);
            this.gridPlayTime.Location = new System.Drawing.Point(0, 0);
            this.gridPlayTime.Margin = new System.Windows.Forms.Padding(0);
            this.gridPlayTime.Name = "gridPlayTime";
            this.gridPlayTime.Size = new System.Drawing.Size(501, 32);
            this.gridPlayTime.Span = "fill 90 128";
            this.gridPlayTime.TabIndex = 0;
            // 
            // lblPlayTime
            // 
            this.lblPlayTime.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPlayTime.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPlayTime.ForeColor = System.Drawing.Color.White;
            this.lblPlayTime.Location = new System.Drawing.Point(341, 0);
            this.lblPlayTime.Margin = new System.Windows.Forms.Padding(0);
            this.lblPlayTime.Name = "lblPlayTime";
            this.lblPlayTime.Size = new System.Drawing.Size(160, 32);
            this.lblPlayTime.TabIndex = 1;
            this.lblPlayTime.Text = "00:00 / 00:00";
            this.lblPlayTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblPlaySpeed
            // 
            this.lblPlaySpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPlaySpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPlaySpeed.ForeColor = System.Drawing.Color.White;
            this.lblPlaySpeed.Location = new System.Drawing.Point(229, 0);
            this.lblPlaySpeed.Margin = new System.Windows.Forms.Padding(0);
            this.lblPlaySpeed.Name = "lblPlaySpeed";
            this.lblPlaySpeed.Size = new System.Drawing.Size(112, 32);
            this.lblPlaySpeed.TabIndex = 2;
            this.lblPlaySpeed.Text = "速度 1.0x";
            this.lblPlaySpeed.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // sliderPlay
            // 
            this.sliderPlay.BackColor = System.Drawing.Color.Transparent;
            this.sliderPlay.Cursor = System.Windows.Forms.Cursors.Hand;
            this.sliderPlay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sliderPlay.Location = new System.Drawing.Point(0, 0);
            this.sliderPlay.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.sliderPlay.Name = "sliderPlay";
            this.sliderPlay.Size = new System.Drawing.Size(221, 32);
            this.sliderPlay.TabIndex = 0;
            // 
            // gridPlayFilter
            // 
            this.gridPlayFilter.BackColor = System.Drawing.Color.Transparent;
            this.gridPlayFilter.Controls.Add(this.btnRefreshPlay);
            this.gridPlayFilter.Controls.Add(this.uiPlayDateRange);
            this.gridPlayFilter.Location = new System.Drawing.Point(525, 0);
            this.gridPlayFilter.Margin = new System.Windows.Forms.Padding(0);
            this.gridPlayFilter.Name = "gridPlayFilter";
            this.gridPlayFilter.Size = new System.Drawing.Size(500, 56);
            this.gridPlayFilter.Span = "fill 105";
            this.gridPlayFilter.TabIndex = 4;
            // 
            // btnRefreshPlay
            // 
            this.btnRefreshPlay.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRefreshPlay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRefreshPlay.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRefreshPlay.Ghost = true;
            this.btnRefreshPlay.IconSvg = "ReloadOutlined";
            this.btnRefreshPlay.Location = new System.Drawing.Point(372, 3);
            this.btnRefreshPlay.Name = "btnRefreshPlay";
            this.btnRefreshPlay.Size = new System.Drawing.Size(125, 50);
            this.btnRefreshPlay.TabIndex = 1;
            this.btnRefreshPlay.Text = "刷新列表";
            // 
            // uiPlayDateRange
            // 
            this.uiPlayDateRange.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPlayDateRange.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPlayDateRange.InteractiveReset = false;
            this.uiPlayDateRange.Location = new System.Drawing.Point(3, 3);
            this.uiPlayDateRange.Name = "uiPlayDateRange";
            this.uiPlayDateRange.PlaceholderEnd = "结束日期";
            this.uiPlayDateRange.PlaceholderStart = "开始日期";
            this.uiPlayDateRange.Size = new System.Drawing.Size(363, 50);
            this.uiPlayDateRange.TabIndex = 0;
            this.uiPlayDateRange.WaveSize = 0;
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(519, 50);
            this.label1.TabIndex = 3;
            this.label1.Text = "标题";
            // 
            // timerStatus
            // 
            this.timerStatus.Interval = 500;
            this.timerStatus.Tick += new System.EventHandler(this.StatusTimer_Tick);
            // 
            // MonitorCamPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tabsMonitor);
            this.Name = "MonitorCamPage";
            this.Size = new System.Drawing.Size(1762, 920);
            this.tabsMonitor.ResumeLayout(false);
            this.tabPageCamSetting.ResumeLayout(false);
            this.tableLayoutPanelCam.ResumeLayout(false);
            this.tableLayoutConn.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).EndInit();
            this.panelRight.ResumeLayout(false);
            this.grpCapture.ResumeLayout(false);
            this.tableLayoutCapture.ResumeLayout(false);
            this.grpRecord.ResumeLayout(false);
            this.tableLayoutRecord.ResumeLayout(false);
            this.grpParam.ResumeLayout(false);
            this.tableLayoutParam.ResumeLayout(false);
            this.grpCalib.ResumeLayout(false);
            this.tableLayoutCalib.ResumeLayout(false);
            this.tabPagePlayback.ResumeLayout(false);
            this.gridPlayPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imageBoxPlay)).EndInit();
            this.imageBoxPlay.ResumeLayout(false);
            this.panelPlayBar.ResumeLayout(false);
            this.gridPlayBar.ResumeLayout(false);
            this.flowPlayButtons.ResumeLayout(false);
            this.gridPlayTime.ResumeLayout(false);
            this.gridPlayFilter.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private AntdUI.Tabs tabsMonitor;
        private AntdUI.TabPage tabPageCamSetting;
        private AntdUI.TabPage tabPagePlayback;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelCam;
        private System.Windows.Forms.TableLayoutPanel tableLayoutConn;
        private AntdUI.Alert alert_ConnStatus;
        private AntdUI.Label lblIP;
        private AntdUI.Input txtIP;
        private AntdUI.Button btnScan;
        private AntdUI.Button btnConnect;
        private Emgu.CV.UI.ImageBox imageBox1;
        private System.Windows.Forms.TableLayoutPanel panelRight;
        private System.Windows.Forms.GroupBox grpCapture;
        private System.Windows.Forms.TableLayoutPanel tableLayoutCapture;
        private AntdUI.Button btnStartCapture;
        private AntdUI.Button btnStopCapture;
        private System.Windows.Forms.GroupBox grpRecord;
        private System.Windows.Forms.TableLayoutPanel tableLayoutRecord;
        private AntdUI.Input txtRecordDir;
        private AntdUI.Button btnBrowseRecord;
        private AntdUI.Button btnStartRecord;
        private AntdUI.Button btnStopRecord;
        private System.Windows.Forms.GroupBox grpParam;
        private System.Windows.Forms.TableLayoutPanel tableLayoutParam;
        private AntdUI.Label lblExposure;
        private AntdUI.Input txtExposure;
        private AntdUI.Label lblGain;
        private AntdUI.Input txtGain;
        private AntdUI.Label lblAlignWarn;
        private AntdUI.Input txtAlignWarn;
        private AntdUI.Label lblAlignFail;
        private AntdUI.Input txtAlignFail;
        private AntdUI.Button btnSaveAlign;
        private AntdUI.Button btnApplyParam;
        private System.Windows.Forms.GroupBox grpCalib;
        private System.Windows.Forms.TableLayoutPanel tableLayoutCalib;
        private AntdUI.Label lblMmPerPixel;
        private AntdUI.Input txtMmPerPixel;
        private AntdUI.Label lblSpotX;
        private AntdUI.Input txtSpotX;
        private AntdUI.Label lblSpotY;
        private AntdUI.Input txtSpotY;
        private AntdUI.Button btnCalibSave;
        private AntdUI.Button btnFeedTest;
        private AntdUI.GridPanel gridPlayPage;
        private AntdUI.GridPanel gridPlayFilter;
        private AntdUI.DatePickerRange uiPlayDateRange;
        private AntdUI.Button btnRefreshPlay;
        private AntdUI.Table playTable;
        private Emgu.CV.UI.ImageBox imageBoxPlay;
        private AntdUI.Panel panelPlayBar;
        private AntdUI.GridPanel gridPlayBar;
        private AntdUI.GridPanel gridPlayTime;
        private AntdUI.Slider sliderPlay;
        private AntdUI.Label lblPlayTime;
        private AntdUI.Label lblPlaySpeed;
        private AntdUI.FlowPanel flowPlayButtons;
        private AntdUI.Button btnSlowPlay;
        private AntdUI.Button btnBack5Play;
        private AntdUI.Button btnPlayPause;
        private AntdUI.Button btnForward5Play;
        private AntdUI.Button btnFastPlay;
        private System.Windows.Forms.Timer timerStatus;
        private AntdUI.Label label1;
    }
}
