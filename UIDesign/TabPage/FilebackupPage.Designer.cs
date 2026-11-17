namespace AdaWeldSystem.UIDesign.TabPage
{
    partial class FilebackupPage
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        private void InitializeComponent()
        {
            AntdUI.Tabs.StyleCard styleCard1 = new AntdUI.Tabs.StyleCard();
            this.tabsBackup = new AntdUI.Tabs();
            this.tabPageCommCfg = new AntdUI.TabPage();
            this.tabPageSystemCfg = new AntdUI.TabPage();
            this.panelSystemCfg = new AntdUI.Panel();
            this.gridSystemRoot = new AntdUI.GridPanel();
            this.gridDeltaConfig = new AntdUI.GridPanel();
            this.btnExportPackage = new AntdUI.Button();
            this.btnImportPackage = new AntdUI.Button();
            this.tableDeltaView = new AntdUI.Table();
            this.segDeltaView = new AntdUI.Segmented();
            this.lblDeltaTitle = new AntdUI.Label();
            this.gridBackupOps = new AntdUI.GridPanel();
            this.lblDeltaStatus = new AntdUI.Label();
            this.btnDeltaGen = new AntdUI.Button();
            this.btnBrowseDir = new AntdUI.Button();
            this.txtCsvDir = new AntdUI.Input();
            this.lblCsvDir = new AntdUI.Label();
            this.lblSystemBackupTitle = new AntdUI.Label();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.btnConfig = new AntdUI.Button();
            this.btnConnect = new AntdUI.Button();
            this.btnRobotDisconnect = new AntdUI.Button();
            this.lblRobotCommMode = new AntdUI.Label();
            this.radioModeRSI = new AntdUI.Radio();
            this.txtRsiRxPacket = new AntdUI.Input();
            this.txtRsiTxPacket = new AntdUI.Input();
            this.btnInjectWeldParams = new AntdUI.Button();
            this.txtTestCutWire = new AntdUI.Input();
            this.lblTestCutWire = new AntdUI.Label();
            this.txtTestWeave = new AntdUI.Input();
            this.lblTestWeave = new AntdUI.Label();
            this.txtTestWire = new AntdUI.Input();
            this.lblTestWire = new AntdUI.Label();
            this.txtTestWeldSpeed = new AntdUI.Input();
            this.lblTestWeldSpeed = new AntdUI.Label();
            this.txtTestPower = new AntdUI.Input();
            this.lblTestPower = new AntdUI.Label();
            this.btnSendTx = new AntdUI.Button();
            this.txtTxPayload = new AntdUI.Input();
            this.selectTxCmd = new AntdUI.Select();
            this.lblTxLabel = new AntdUI.Label();
            this.lblRsiCycle = new AntdUI.Label();
            this.selectRsiCycle = new AntdUI.Select();
            this.inputLog = new AntdUI.Input();
            this.chkEnableRobot = new AntdUI.Checkbox();
            this.lblRsiStat = new AntdUI.Label();
            this.radioRobot = new AntdUI.Radio();
            this.btnKeepConfig = new AntdUI.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tabsBackup.SuspendLayout();
            this.tabPageCommCfg.SuspendLayout();
            this.tabPageSystemCfg.SuspendLayout();
            this.panelSystemCfg.SuspendLayout();
            this.gridSystemRoot.SuspendLayout();
            this.gridDeltaConfig.SuspendLayout();
            this.gridBackupOps.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabsBackup
            // 
            this.tabsBackup.Controls.Add(this.tabPageCommCfg);
            this.tabsBackup.Controls.Add(this.tabPageSystemCfg);
            this.tabsBackup.Cursor = System.Windows.Forms.Cursors.Default;
            this.tabsBackup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsBackup.Location = new System.Drawing.Point(0, 0);
            this.tabsBackup.Name = "tabsBackup";
            this.tabsBackup.Pages.Add(this.tabPageCommCfg);
            this.tabsBackup.Pages.Add(this.tabPageSystemCfg);
            this.tabsBackup.Size = new System.Drawing.Size(1762, 920);
            this.tabsBackup.Style = styleCard1;
            this.tabsBackup.TabIndex = 0;
            this.tabsBackup.Text = "tabsBackup";
            this.tabsBackup.Type = AntdUI.TabType.Card;
            // 
            // tabPageCommCfg
            // 
            this.tabPageCommCfg.Controls.Add(this.tableLayoutPanel1);
            this.tabPageCommCfg.Location = new System.Drawing.Point(0, 31);
            this.tabPageCommCfg.Name = "tabPageCommCfg";
            this.tabPageCommCfg.Size = new System.Drawing.Size(1762, 889);
            this.tabPageCommCfg.TabIndex = 0;
            this.tabPageCommCfg.Text = "通讯配置文件";
            // 
            // tabPageSystemCfg
            // 
            this.tabPageSystemCfg.Controls.Add(this.panelSystemCfg);
            this.tabPageSystemCfg.Location = new System.Drawing.Point(-3524, -1778);
            this.tabPageSystemCfg.Name = "tabPageSystemCfg";
            this.tabPageSystemCfg.Size = new System.Drawing.Size(1762, 889);
            this.tabPageSystemCfg.TabIndex = 2;
            this.tabPageSystemCfg.Text = "系统配置文件";
            // 
            // panelSystemCfg
            // 
            this.panelSystemCfg.AutoScroll = true;
            this.panelSystemCfg.Back = System.Drawing.Color.White;
            this.panelSystemCfg.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelSystemCfg.Controls.Add(this.gridSystemRoot);
            this.panelSystemCfg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelSystemCfg.Location = new System.Drawing.Point(0, 0);
            this.panelSystemCfg.Name = "panelSystemCfg";
            this.panelSystemCfg.Padding = new System.Windows.Forms.Padding(3);
            this.panelSystemCfg.Shadow = 5;
            this.panelSystemCfg.ShadowColor = System.Drawing.Color.Black;
            this.panelSystemCfg.ShadowOpacity = 0.2F;
            this.panelSystemCfg.ShadowOpacityAnimation = true;
            this.panelSystemCfg.Size = new System.Drawing.Size(1762, 889);
            this.panelSystemCfg.TabIndex = 0;
            // 
            // gridSystemRoot
            // 
            this.gridSystemRoot.Controls.Add(this.gridDeltaConfig);
            this.gridSystemRoot.Controls.Add(this.gridBackupOps);
            this.gridSystemRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridSystemRoot.Location = new System.Drawing.Point(9, 9);
            this.gridSystemRoot.Margin = new System.Windows.Forms.Padding(0);
            this.gridSystemRoot.Name = "gridSystemRoot";
            this.gridSystemRoot.Size = new System.Drawing.Size(1744, 871);
            this.gridSystemRoot.Span = "160:fill;fill";
            this.gridSystemRoot.TabIndex = 0;
            // 
            // gridDeltaConfig
            // 
            this.gridDeltaConfig.Controls.Add(this.btnExportPackage);
            this.gridDeltaConfig.Controls.Add(this.btnImportPackage);
            this.gridDeltaConfig.Controls.Add(this.tableDeltaView);
            this.gridDeltaConfig.Controls.Add(this.segDeltaView);
            this.gridDeltaConfig.Controls.Add(this.lblDeltaTitle);
            this.gridDeltaConfig.Location = new System.Drawing.Point(0, 200);
            this.gridDeltaConfig.Margin = new System.Windows.Forms.Padding(0);
            this.gridDeltaConfig.Name = "gridDeltaConfig";
            this.gridDeltaConfig.Size = new System.Drawing.Size(1744, 671);
            this.gridDeltaConfig.Span = "40:fill;40:fill;320:fill;40:160 160 160";
            this.gridDeltaConfig.TabIndex = 0;
            // 
            // btnExportPackage
            // 
            this.btnExportPackage.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnExportPackage.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnExportPackage.Location = new System.Drawing.Point(203, 503);
            this.btnExportPackage.Name = "btnExportPackage";
            this.btnExportPackage.Size = new System.Drawing.Size(194, 44);
            this.btnExportPackage.TabIndex = 1;
            this.btnExportPackage.Text = "一键导出设置(.awcfg)";
            this.btnExportPackage.Type = AntdUI.TTypeMini.Success;
            this.btnExportPackage.Click += new System.EventHandler(this.btnExportPackage_Click);
            // 
            // btnImportPackage
            // 
            this.btnImportPackage.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnImportPackage.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnImportPackage.Location = new System.Drawing.Point(3, 503);
            this.btnImportPackage.Name = "btnImportPackage";
            this.btnImportPackage.Size = new System.Drawing.Size(194, 44);
            this.btnImportPackage.TabIndex = 2;
            this.btnImportPackage.Text = "一键导入设置(.awcfg)";
            this.btnImportPackage.Type = AntdUI.TTypeMini.Warn;
            this.btnImportPackage.Click += new System.EventHandler(this.btnImportPackage_Click);
            // 
            // tableDeltaView
            // 
            this.tableDeltaView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableDeltaView.Gap = 12;
            this.tableDeltaView.Location = new System.Drawing.Point(3, 103);
            this.tableDeltaView.Name = "tableDeltaView";
            this.tableDeltaView.Size = new System.Drawing.Size(1738, 394);
            this.tableDeltaView.TabIndex = 2;
            // 
            // segDeltaView
            // 
            this.segDeltaView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.segDeltaView.Location = new System.Drawing.Point(3, 53);
            this.segDeltaView.Name = "segDeltaView";
            this.segDeltaView.Size = new System.Drawing.Size(1738, 44);
            this.segDeltaView.TabIndex = 1;
            // 
            // lblDeltaTitle
            // 
            this.lblDeltaTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.lblDeltaTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDeltaTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDeltaTitle.Location = new System.Drawing.Point(3, 3);
            this.lblDeltaTitle.Name = "lblDeltaTitle";
            this.lblDeltaTitle.Size = new System.Drawing.Size(1738, 44);
            this.lblDeltaTitle.TabIndex = 0;
            this.lblDeltaTitle.Text = "台达控制器配置（配置文档 → 项目 XML）";
            // 
            // gridBackupOps
            // 
            this.gridBackupOps.Controls.Add(this.lblDeltaStatus);
            this.gridBackupOps.Controls.Add(this.btnDeltaGen);
            this.gridBackupOps.Controls.Add(this.btnBrowseDir);
            this.gridBackupOps.Controls.Add(this.txtCsvDir);
            this.gridBackupOps.Controls.Add(this.lblCsvDir);
            this.gridBackupOps.Controls.Add(this.lblSystemBackupTitle);
            this.gridBackupOps.Location = new System.Drawing.Point(0, 0);
            this.gridBackupOps.Margin = new System.Windows.Forms.Padding(0);
            this.gridBackupOps.Name = "gridBackupOps";
            this.gridBackupOps.Size = new System.Drawing.Size(1744, 200);
            this.gridBackupOps.Span = "40:fill;40:160 160 160 160 ;80:fill";
            this.gridBackupOps.TabIndex = 0;
            // 
            // lblDeltaStatus
            // 
            this.lblDeltaStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDeltaStatus.Location = new System.Drawing.Point(3, 103);
            this.lblDeltaStatus.Name = "lblDeltaStatus";
            this.lblDeltaStatus.Size = new System.Drawing.Size(1738, 94);
            this.lblDeltaStatus.TabIndex = 3;
            this.lblDeltaStatus.Text = "请选择配置文档目录（目录需含轴/IO/气缸三表 + 焊接头 ESI 与 IO 表）";
            // 
            // btnDeltaGen
            // 
            this.btnDeltaGen.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDeltaGen.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnDeltaGen.Location = new System.Drawing.Point(603, 53);
            this.btnDeltaGen.Name = "btnDeltaGen";
            this.btnDeltaGen.Size = new System.Drawing.Size(194, 44);
            this.btnDeltaGen.TabIndex = 0;
            this.btnDeltaGen.Text = "重载保存";
            this.btnDeltaGen.Type = AntdUI.TTypeMini.Primary;
            this.btnDeltaGen.Click += new System.EventHandler(this.btnDeltaGen_Click);
            // 
            // btnBrowseDir
            // 
            this.btnBrowseDir.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBrowseDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBrowseDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBrowseDir.Location = new System.Drawing.Point(403, 53);
            this.btnBrowseDir.Name = "btnBrowseDir";
            this.btnBrowseDir.Size = new System.Drawing.Size(194, 44);
            this.btnBrowseDir.TabIndex = 3;
            this.btnBrowseDir.Text = "选择目录";
            this.btnBrowseDir.Click += new System.EventHandler(this.btnBrowseDir_Click);
            // 
            // txtCsvDir
            // 
            this.txtCsvDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCsvDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtCsvDir.Location = new System.Drawing.Point(203, 53);
            this.txtCsvDir.MinimumSize = new System.Drawing.Size(1, 1);
            this.txtCsvDir.Name = "txtCsvDir";
            this.txtCsvDir.PlaceholderText = "选择存放轴/IO/气缸 CSV + 焊接头 ESI 与 IO 表的目录";
            this.txtCsvDir.Size = new System.Drawing.Size(194, 44);
            this.txtCsvDir.TabIndex = 2;
            // 
            // lblCsvDir
            // 
            this.lblCsvDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCsvDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCsvDir.Location = new System.Drawing.Point(3, 53);
            this.lblCsvDir.Name = "lblCsvDir";
            this.lblCsvDir.Size = new System.Drawing.Size(194, 44);
            this.lblCsvDir.TabIndex = 1;
            this.lblCsvDir.Text = "配置文档目录";
            // 
            // lblSystemBackupTitle
            // 
            this.lblSystemBackupTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.lblSystemBackupTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSystemBackupTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSystemBackupTitle.Location = new System.Drawing.Point(3, 3);
            this.lblSystemBackupTitle.Name = "lblSystemBackupTitle";
            this.lblSystemBackupTitle.Size = new System.Drawing.Size(1738, 44);
            this.lblSystemBackupTitle.TabIndex = 0;
            this.lblSystemBackupTitle.Text = "系统配置文件导入导出与备份";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 9;
            this.tableLayoutPanel1.SetColumnSpan(this.tableLayoutPanel2, 5);
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this.lblRsiStat, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.chkEnableRobot, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.selectRsiCycle, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.lblRsiCycle, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.lblTxLabel, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this.selectTxCmd, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this.txtTxPayload, 2, 4);
            this.tableLayoutPanel2.Controls.Add(this.btnSendTx, 3, 4);
            this.tableLayoutPanel2.Controls.Add(this.lblTestPower, 4, 4);
            this.tableLayoutPanel2.Controls.Add(this.txtTestPower, 5, 4);
            this.tableLayoutPanel2.Controls.Add(this.lblTestWeldSpeed, 6, 4);
            this.tableLayoutPanel2.Controls.Add(this.txtTestWeldSpeed, 7, 4);
            this.tableLayoutPanel2.Controls.Add(this.lblTestWire, 4, 5);
            this.tableLayoutPanel2.Controls.Add(this.txtTestWire, 5, 5);
            this.tableLayoutPanel2.Controls.Add(this.lblTestWeave, 6, 5);
            this.tableLayoutPanel2.Controls.Add(this.txtTestWeave, 7, 5);
            this.tableLayoutPanel2.Controls.Add(this.lblTestCutWire, 4, 6);
            this.tableLayoutPanel2.Controls.Add(this.txtTestCutWire, 5, 6);
            this.tableLayoutPanel2.Controls.Add(this.btnInjectWeldParams, 7, 6);
            this.tableLayoutPanel2.Controls.Add(this.txtRsiTxPacket, 4, 7);
            this.tableLayoutPanel2.Controls.Add(this.txtRsiRxPacket, 0, 7);
            this.tableLayoutPanel2.Controls.Add(this.radioModeRSI, 7, 0);
            this.tableLayoutPanel2.Controls.Add(this.lblRobotCommMode, 6, 0);
            this.tableLayoutPanel2.Controls.Add(this.btnRobotDisconnect, 7, 2);
            this.tableLayoutPanel2.Controls.Add(this.btnConnect, 6, 2);
            this.tableLayoutPanel2.Controls.Add(this.btnConfig, 2, 1);
            this.tableLayoutPanel2.Controls.Add(this.inputLog, 0, 8);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 43);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 9;
            this.tableLayoutPanel1.SetRowSpan(this.tableLayoutPanel2, 2);
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(1756, 843);
            this.tableLayoutPanel2.TabIndex = 2;
            // 
            // btnConfig
            // 
            this.btnConfig.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConfig.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConfig.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConfig.Location = new System.Drawing.Point(323, 43);
            this.btnConfig.Name = "btnConfig";
            this.btnConfig.Size = new System.Drawing.Size(154, 34);
            this.btnConfig.TabIndex = 10;
            this.btnConfig.Text = "打开配置";
            this.btnConfig.Click += new System.EventHandler(this.btnConfig_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConnect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConnect.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnect.Location = new System.Drawing.Point(963, 83);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(154, 34);
            this.btnConnect.TabIndex = 7;
            this.btnConnect.Text = "启动 ";
            this.btnConnect.Click += new System.EventHandler(this.btnRobotStart_Click);
            // 
            // btnRobotDisconnect
            // 
            this.btnRobotDisconnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRobotDisconnect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRobotDisconnect.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRobotDisconnect.Location = new System.Drawing.Point(1123, 83);
            this.btnRobotDisconnect.Name = "btnRobotDisconnect";
            this.btnRobotDisconnect.Size = new System.Drawing.Size(154, 34);
            this.btnRobotDisconnect.TabIndex = 9;
            this.btnRobotDisconnect.Text = "关闭";
            this.btnRobotDisconnect.Click += new System.EventHandler(this.btnRobotStop_Click);
            // 
            // lblRobotCommMode
            // 
            this.lblRobotCommMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRobotCommMode.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRobotCommMode.Location = new System.Drawing.Point(963, 3);
            this.lblRobotCommMode.Name = "lblRobotCommMode";
            this.lblRobotCommMode.Size = new System.Drawing.Size(154, 34);
            this.lblRobotCommMode.TabIndex = 3;
            this.lblRobotCommMode.Text = "通讯模式";
            this.lblRobotCommMode.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioModeRSI
            // 
            this.radioModeRSI.Checked = true;
            this.radioModeRSI.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioModeRSI.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioModeRSI.Location = new System.Drawing.Point(1123, 3);
            this.radioModeRSI.Name = "radioModeRSI";
            this.radioModeRSI.Size = new System.Drawing.Size(154, 34);
            this.radioModeRSI.TabIndex = 4;
            this.radioModeRSI.Text = "RSI";
            this.radioModeRSI.CheckedChanged += new AntdUI.BoolEventHandler(this.radioModeRSI_CheckedChanged);
            // 
            // txtRsiRxPacket
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.txtRsiRxPacket, 4);
            this.txtRsiRxPacket.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRsiRxPacket.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRsiRxPacket.Location = new System.Drawing.Point(3, 283);
            this.txtRsiRxPacket.Multiline = true;
            this.txtRsiRxPacket.Name = "txtRsiRxPacket";
            this.txtRsiRxPacket.ReadOnly = true;
            this.txtRsiRxPacket.Size = new System.Drawing.Size(634, 194);
            this.txtRsiRxPacket.TabIndex = 16;
            // 
            // txtRsiTxPacket
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.txtRsiTxPacket, 4);
            this.txtRsiTxPacket.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRsiTxPacket.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRsiTxPacket.Location = new System.Drawing.Point(643, 283);
            this.txtRsiTxPacket.Multiline = true;
            this.txtRsiTxPacket.Name = "txtRsiTxPacket";
            this.txtRsiTxPacket.ReadOnly = true;
            this.txtRsiTxPacket.Size = new System.Drawing.Size(634, 194);
            this.txtRsiTxPacket.TabIndex = 17;
            // 
            // btnInjectWeldParams
            // 
            this.btnInjectWeldParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnInjectWeldParams.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnInjectWeldParams.Location = new System.Drawing.Point(1123, 243);
            this.btnInjectWeldParams.Name = "btnInjectWeldParams";
            this.btnInjectWeldParams.Size = new System.Drawing.Size(154, 34);
            this.btnInjectWeldParams.TabIndex = 15;
            this.btnInjectWeldParams.Text = "写入焊参通道";
            this.btnInjectWeldParams.Type = AntdUI.TTypeMini.Primary;
            this.btnInjectWeldParams.Click += new System.EventHandler(this.btnInjectWeldParams_Click);
            // 
            // txtTestCutWire
            // 
            this.txtTestCutWire.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTestCutWire.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTestCutWire.Location = new System.Drawing.Point(803, 243);
            this.txtTestCutWire.Name = "txtTestCutWire";
            this.txtTestCutWire.Size = new System.Drawing.Size(154, 34);
            this.txtTestCutWire.TabIndex = 14;
            this.txtTestCutWire.Text = "0";
            // 
            // lblTestCutWire
            // 
            this.lblTestCutWire.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTestCutWire.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTestCutWire.Location = new System.Drawing.Point(643, 243);
            this.lblTestCutWire.Name = "lblTestCutWire";
            this.lblTestCutWire.Size = new System.Drawing.Size(154, 34);
            this.lblTestCutWire.TabIndex = 14;
            this.lblTestCutWire.Text = "剪丝信号(1=请求)";
            // 
            // txtTestWeave
            // 
            this.txtTestWeave.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTestWeave.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTestWeave.Location = new System.Drawing.Point(1123, 203);
            this.txtTestWeave.Name = "txtTestWeave";
            this.txtTestWeave.Size = new System.Drawing.Size(154, 34);
            this.txtTestWeave.TabIndex = 13;
            this.txtTestWeave.Text = "0";
            // 
            // lblTestWeave
            // 
            this.lblTestWeave.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTestWeave.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTestWeave.Location = new System.Drawing.Point(963, 203);
            this.lblTestWeave.Name = "lblTestWeave";
            this.lblTestWeave.Size = new System.Drawing.Size(154, 34);
            this.lblTestWeave.TabIndex = 13;
            this.lblTestWeave.Text = "摆幅(mm)";
            // 
            // txtTestWire
            // 
            this.txtTestWire.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTestWire.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTestWire.Location = new System.Drawing.Point(803, 203);
            this.txtTestWire.Name = "txtTestWire";
            this.txtTestWire.Size = new System.Drawing.Size(154, 34);
            this.txtTestWire.TabIndex = 11;
            this.txtTestWire.Text = "0";
            // 
            // lblTestWire
            // 
            this.lblTestWire.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTestWire.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTestWire.Location = new System.Drawing.Point(643, 203);
            this.lblTestWire.Name = "lblTestWire";
            this.lblTestWire.Size = new System.Drawing.Size(154, 34);
            this.lblTestWire.TabIndex = 11;
            this.lblTestWire.Text = "送丝速度(m/min)";
            // 
            // txtTestWeldSpeed
            // 
            this.txtTestWeldSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTestWeldSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTestWeldSpeed.Location = new System.Drawing.Point(1123, 163);
            this.txtTestWeldSpeed.Name = "txtTestWeldSpeed";
            this.txtTestWeldSpeed.Size = new System.Drawing.Size(154, 34);
            this.txtTestWeldSpeed.TabIndex = 12;
            this.txtTestWeldSpeed.Text = "0";
            // 
            // lblTestWeldSpeed
            // 
            this.lblTestWeldSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTestWeldSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTestWeldSpeed.Location = new System.Drawing.Point(963, 163);
            this.lblTestWeldSpeed.Name = "lblTestWeldSpeed";
            this.lblTestWeldSpeed.Size = new System.Drawing.Size(154, 34);
            this.lblTestWeldSpeed.TabIndex = 12;
            this.lblTestWeldSpeed.Text = "焊接速度(mm/s)";
            // 
            // txtTestPower
            // 
            this.txtTestPower.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTestPower.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTestPower.Location = new System.Drawing.Point(803, 163);
            this.txtTestPower.Name = "txtTestPower";
            this.txtTestPower.Size = new System.Drawing.Size(154, 34);
            this.txtTestPower.TabIndex = 10;
            this.txtTestPower.Text = "0";
            // 
            // lblTestPower
            // 
            this.lblTestPower.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTestPower.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTestPower.Location = new System.Drawing.Point(643, 163);
            this.lblTestPower.Name = "lblTestPower";
            this.lblTestPower.Size = new System.Drawing.Size(154, 34);
            this.lblTestPower.TabIndex = 9;
            this.lblTestPower.Text = "激光功率(W)";
            // 
            // btnSendTx
            // 
            this.btnSendTx.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSendTx.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSendTx.Location = new System.Drawing.Point(483, 163);
            this.btnSendTx.Name = "btnSendTx";
            this.btnSendTx.Size = new System.Drawing.Size(154, 34);
            this.btnSendTx.TabIndex = 8;
            this.btnSendTx.Text = "下发";
            this.btnSendTx.Type = AntdUI.TTypeMini.Primary;
            this.btnSendTx.Click += new System.EventHandler(this.btnSendTx_Click);
            // 
            // txtTxPayload
            // 
            this.txtTxPayload.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTxPayload.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTxPayload.Location = new System.Drawing.Point(323, 163);
            this.txtTxPayload.Name = "txtTxPayload";
            this.txtTxPayload.Size = new System.Drawing.Size(154, 34);
            this.txtTxPayload.TabIndex = 7;
            this.txtTxPayload.Text = "0";
            // 
            // selectTxCmd
            // 
            this.selectTxCmd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selectTxCmd.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.selectTxCmd.Location = new System.Drawing.Point(163, 163);
            this.selectTxCmd.MinimumSize = new System.Drawing.Size(1, 1);
            this.selectTxCmd.Name = "selectTxCmd";
            this.selectTxCmd.Size = new System.Drawing.Size(154, 34);
            this.selectTxCmd.TabIndex = 6;
            // 
            // lblTxLabel
            // 
            this.lblTxLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTxLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTxLabel.Location = new System.Drawing.Point(3, 163);
            this.lblTxLabel.Name = "lblTxLabel";
            this.lblTxLabel.Size = new System.Drawing.Size(154, 34);
            this.lblTxLabel.TabIndex = 5;
            this.lblTxLabel.Text = "下行指令下发";
            // 
            // lblRsiCycle
            // 
            this.lblRsiCycle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRsiCycle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRsiCycle.Location = new System.Drawing.Point(3, 43);
            this.lblRsiCycle.Name = "lblRsiCycle";
            this.lblRsiCycle.Size = new System.Drawing.Size(154, 34);
            this.lblRsiCycle.TabIndex = 11;
            this.lblRsiCycle.Text = "通讯周期";
            this.lblRsiCycle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // selectRsiCycle
            // 
            this.selectRsiCycle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selectRsiCycle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.selectRsiCycle.Location = new System.Drawing.Point(163, 43);
            this.selectRsiCycle.MinimumSize = new System.Drawing.Size(1, 1);
            this.selectRsiCycle.Name = "selectRsiCycle";
            this.selectRsiCycle.Size = new System.Drawing.Size(154, 34);
            this.selectRsiCycle.TabIndex = 12;
            // 
            // inputLog
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.inputLog, 8);
            this.inputLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.inputLog.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.inputLog.Location = new System.Drawing.Point(0, 480);
            this.inputLog.Margin = new System.Windows.Forms.Padding(0);
            this.inputLog.MinimumSize = new System.Drawing.Size(1, 1);
            this.inputLog.Multiline = true;
            this.inputLog.Name = "inputLog";
            this.inputLog.ReadOnly = true;
            this.inputLog.Size = new System.Drawing.Size(1280, 363);
            this.inputLog.TabIndex = 3;
            // 
            // chkEnableRobot
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.chkEnableRobot, 2);
            this.chkEnableRobot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkEnableRobot.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkEnableRobot.Location = new System.Drawing.Point(3, 3);
            this.chkEnableRobot.Name = "chkEnableRobot";
            this.chkEnableRobot.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkEnableRobot.Size = new System.Drawing.Size(314, 34);
            this.chkEnableRobot.TabIndex = 0;
            this.chkEnableRobot.Text = "启用机器人通讯";
            this.chkEnableRobot.CheckedChanged += new AntdUI.BoolEventHandler(this.chkEnable_CheckedChanged);
            // 
            // lblRsiStat
            // 
            this.lblRsiStat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRsiStat.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRsiStat.Location = new System.Drawing.Point(3, 123);
            this.lblRsiStat.Name = "lblRsiStat";
            this.lblRsiStat.Size = new System.Drawing.Size(154, 34);
            this.lblRsiStat.TabIndex = 0;
            this.lblRsiStat.Text = "RSI 状态：未连接";
            this.lblRsiStat.TextMultiLine = false;
            // 
            // radioRobot
            // 
            this.radioRobot.Checked = true;
            this.radioRobot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioRobot.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioRobot.Location = new System.Drawing.Point(3, 3);
            this.radioRobot.Name = "radioRobot";
            this.radioRobot.Size = new System.Drawing.Size(154, 34);
            this.radioRobot.TabIndex = 0;
            this.radioRobot.Text = "KUKA机器人";
            // 
            // btnKeepConfig
            // 
            this.btnKeepConfig.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnKeepConfig.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnKeepConfig.Location = new System.Drawing.Point(1665, 3);
            this.btnKeepConfig.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnKeepConfig.Name = "btnKeepConfig";
            this.btnKeepConfig.Size = new System.Drawing.Size(90, 34);
            this.btnKeepConfig.TabIndex = 3;
            this.btnKeepConfig.Text = "保存配置";
            this.btnKeepConfig.Click += new System.EventHandler(this.btnKeepConfig_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.Controls.Add(this.btnKeepConfig, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.radioRobot, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 1);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 430F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1762, 889);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // FilebackupPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tabsBackup);
            this.Name = "FilebackupPage";
            this.Size = new System.Drawing.Size(1762, 920);
            this.tabsBackup.ResumeLayout(false);
            this.tabPageCommCfg.ResumeLayout(false);
            this.tabPageSystemCfg.ResumeLayout(false);
            this.panelSystemCfg.ResumeLayout(false);
            this.gridSystemRoot.ResumeLayout(false);
            this.gridDeltaConfig.ResumeLayout(false);
            this.gridBackupOps.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Tabs tabsBackup;
        private AntdUI.TabPage tabPageCommCfg;
        private AntdUI.TabPage tabPageSystemCfg;
        private AntdUI.Panel panelSystemCfg;
        private AntdUI.GridPanel gridSystemRoot;
        private AntdUI.GridPanel gridBackupOps;
        private AntdUI.GridPanel gridDeltaConfig;
        private AntdUI.Label lblDeltaTitle;
        private AntdUI.Segmented segDeltaView;
        private AntdUI.Table tableDeltaView;
        private AntdUI.Label lblDeltaStatus;
        private AntdUI.Button btnDeltaGen;
        private AntdUI.Button btnExportPackage;
        private AntdUI.Button btnImportPackage;
        private AntdUI.Label lblSystemBackupTitle;
        private AntdUI.Label lblCsvDir;
        private AntdUI.Input txtCsvDir;
        private AntdUI.Button btnBrowseDir;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private AntdUI.Label lblRsiStat;
        private AntdUI.Checkbox chkEnableRobot;
        private AntdUI.Input inputLog;
        private AntdUI.Select selectRsiCycle;
        private AntdUI.Label lblRsiCycle;
        private AntdUI.Label lblTxLabel;
        private AntdUI.Select selectTxCmd;
        private AntdUI.Input txtTxPayload;
        private AntdUI.Button btnSendTx;
        private AntdUI.Label lblTestPower;
        private AntdUI.Input txtTestPower;
        private AntdUI.Label lblTestWeldSpeed;
        private AntdUI.Input txtTestWeldSpeed;
        private AntdUI.Label lblTestWire;
        private AntdUI.Input txtTestWire;
        private AntdUI.Label lblTestWeave;
        private AntdUI.Input txtTestWeave;
        private AntdUI.Label lblTestCutWire;
        private AntdUI.Input txtTestCutWire;
        private AntdUI.Button btnInjectWeldParams;
        private AntdUI.Input txtRsiTxPacket;
        private AntdUI.Input txtRsiRxPacket;
        private AntdUI.Radio radioModeRSI;
        private AntdUI.Label lblRobotCommMode;
        private AntdUI.Button btnRobotDisconnect;
        private AntdUI.Button btnConnect;
        private AntdUI.Button btnConfig;
        private AntdUI.Button btnKeepConfig;
        private AntdUI.Radio radioRobot;
    }
}