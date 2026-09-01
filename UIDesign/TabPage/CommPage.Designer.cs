namespace AdaWeldSystem.Sub2UI
{
    partial class CommPage
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
            AntdUI.Tabs.StyleLine styleLine1 = new AntdUI.Tabs.StyleLine();
            this.gridMain = new AntdUI.GridPanel();
            this.inputLog = new AntdUI.Input();
            this.chkShowCommLog = new AntdUI.Checkbox();
            this.tabsSettings = new AntdUI.Tabs();
            this.tabRobot = new AntdUI.TabPage();
            this.panelRobot = new System.Windows.Forms.Panel();
            this.radioModeEKI = new AntdUI.Radio();
            this.radioModeRSI = new AntdUI.Radio();
            this.lblRobotCommMode = new AntdUI.Label();
            this.chkEnableRobot = new AntdUI.Checkbox();
            this.grpRobotConfig = new System.Windows.Forms.GroupBox();
            this.btnRobotDisconnect = new AntdUI.Button();
            this.btnEkiConnect = new AntdUI.Button();
            this.btnRsiConnect = new AntdUI.Button();
            this.txtRobotPort = new AntdUI.Input();
            this.lblRobotPort = new AntdUI.Label();
            this.txtRobotIp = new AntdUI.Input();
            this.lblRobotIp = new AntdUI.Label();
            this.cmbRobotType = new AntdUI.Select();
            this.lblRobotType = new AntdUI.Label();
            this.tabGeneral = new AntdUI.TabPage();
            this.panelGeneral = new System.Windows.Forms.Panel();
            this.chkEnableGeneral = new AntdUI.Checkbox();
            this.grpServer = new System.Windows.Forms.GroupBox();
            this.lblServerIp = new AntdUI.Label();
            this.txtServerIp = new AntdUI.Input();
            this.lblServerPort = new AntdUI.Label();
            this.txtServerPort = new AntdUI.Input();
            this.btnServerStart = new AntdUI.Button();
            this.btnServerStop = new AntdUI.Button();
            this.grpClient = new System.Windows.Forms.GroupBox();
            this.lblClientIp = new AntdUI.Label();
            this.txtClientIp = new AntdUI.Input();
            this.lblClientPort = new AntdUI.Label();
            this.txtClientPort = new AntdUI.Input();
            this.btnClientConnect = new AntdUI.Button();
            this.btnClientDisconnect = new AntdUI.Button();
            this.txtSend = new AntdUI.Input();
            this.btnSend = new AntdUI.Button();
            this.tabPLC = new AntdUI.TabPage();
            this.panelPLC = new System.Windows.Forms.Panel();
            this.chkEnablePLC = new AntdUI.Checkbox();
            this.grpPLCConfig = new System.Windows.Forms.GroupBox();
            this.btnPlcGsdGenerate = new AntdUI.Button();
            this.btnPlcDisconnect = new AntdUI.Button();
            this.btnPlcConnect = new AntdUI.Button();
            this.txtPlcSlot = new AntdUI.Input();
            this.lblPlcSlot = new AntdUI.Label();
            this.txtPlcRack = new AntdUI.Input();
            this.lblPlcRack = new AntdUI.Label();
            this.txtPlcIp = new AntdUI.Input();
            this.lblPlcIp = new AntdUI.Label();
            this.cmbPlcType = new AntdUI.Select();
            this.lblPlcType = new AntdUI.Label();
            this.panelRadio = new System.Windows.Forms.Panel();
            this.btnKeepConfig = new AntdUI.Button();
            this.radioGeneral = new AntdUI.Radio();
            this.radioRobot = new AntdUI.Radio();
            this.radioPLC = new AntdUI.Radio();
            this.gridMain.SuspendLayout();
            this.tabsSettings.SuspendLayout();
            this.tabRobot.SuspendLayout();
            this.panelRobot.SuspendLayout();
            this.grpRobotConfig.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.panelGeneral.SuspendLayout();
            this.grpServer.SuspendLayout();
            this.grpClient.SuspendLayout();
            this.tabPLC.SuspendLayout();
            this.panelPLC.SuspendLayout();
            this.grpPLCConfig.SuspendLayout();
            this.panelRadio.SuspendLayout();
            this.SuspendLayout();
            // 
            // gridMain
            // 
            this.gridMain.Controls.Add(this.inputLog);
            this.gridMain.Controls.Add(this.chkShowCommLog);
            this.gridMain.Controls.Add(this.tabsSettings);
            this.gridMain.Controls.Add(this.panelRadio);
            this.gridMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridMain.Gap = 4;
            this.gridMain.Location = new System.Drawing.Point(0, 0);
            this.gridMain.Name = "gridMain";
            this.gridMain.Size = new System.Drawing.Size(1218, 1007);
            this.gridMain.Span = "fill;fill;fill;fill;-5% 20% 5% 70%";
            this.gridMain.TabIndex = 0;
            // 
            // inputLog
            // 
            this.inputLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.inputLog.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.inputLog.Location = new System.Drawing.Point(5, 307);
            this.inputLog.Margin = new System.Windows.Forms.Padding(0);
            this.inputLog.MinimumSize = new System.Drawing.Size(1, 1);
            this.inputLog.Multiline = true;
            this.inputLog.Name = "inputLog";
            this.inputLog.ReadOnly = true;
            this.inputLog.Size = new System.Drawing.Size(1208, 695);
            this.inputLog.TabIndex = 3;
            // 
            // chkShowCommLog
            // 
            this.chkShowCommLog.Checked = true;
            this.chkShowCommLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowCommLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkShowCommLog.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkShowCommLog.Location = new System.Drawing.Point(5, 257);
            this.chkShowCommLog.Margin = new System.Windows.Forms.Padding(0);
            this.chkShowCommLog.Name = "chkShowCommLog";
            this.chkShowCommLog.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkShowCommLog.Size = new System.Drawing.Size(1208, 40);
            this.chkShowCommLog.TabIndex = 2;
            this.chkShowCommLog.Text = "显示通讯日志";
            this.chkShowCommLog.CheckedChanged += new AntdUI.BoolEventHandler(this.chkShowCommLog_CheckedChanged);
            // 
            // tabsSettings
            // 
            this.tabsSettings.Controls.Add(this.tabRobot);
            this.tabsSettings.Controls.Add(this.tabGeneral);
            this.tabsSettings.Controls.Add(this.tabPLC);
            this.tabsSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsSettings.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tabsSettings.Location = new System.Drawing.Point(5, 55);
            this.tabsSettings.Margin = new System.Windows.Forms.Padding(0);
            this.tabsSettings.Name = "tabsSettings";
            this.tabsSettings.Pages.Add(this.tabRobot);
            this.tabsSettings.Pages.Add(this.tabGeneral);
            this.tabsSettings.Pages.Add(this.tabPLC);
            this.tabsSettings.Size = new System.Drawing.Size(1208, 191);
            this.tabsSettings.Style = styleLine1;
            this.tabsSettings.TabIndex = 1;
            this.tabsSettings.TabMenuVisible = false;
            this.tabsSettings.SelectedIndexChanged += new AntdUI.IntEventHandler(this.OnTabsSelectedChanged);
            // 
            // tabRobot
            // 
            this.tabRobot.Controls.Add(this.panelRobot);
            this.tabRobot.Location = new System.Drawing.Point(0, 0);
            this.tabRobot.Name = "tabRobot";
            this.tabRobot.Size = new System.Drawing.Size(1208, 191);
            this.tabRobot.TabIndex = 1;
            this.tabRobot.Text = "机器人通讯";
            // 
            // panelRobot
            // 
            this.panelRobot.AutoScroll = true;
            this.panelRobot.Controls.Add(this.radioModeEKI);
            this.panelRobot.Controls.Add(this.radioModeRSI);
            this.panelRobot.Controls.Add(this.lblRobotCommMode);
            this.panelRobot.Controls.Add(this.chkEnableRobot);
            this.panelRobot.Controls.Add(this.grpRobotConfig);
            this.panelRobot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRobot.Location = new System.Drawing.Point(0, 0);
            this.panelRobot.Name = "panelRobot";
            this.panelRobot.Size = new System.Drawing.Size(1208, 191);
            this.panelRobot.TabIndex = 1;
            // 
            // radioModeEKI
            // 
            this.radioModeEKI.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioModeEKI.Location = new System.Drawing.Point(922, 8);
            this.radioModeEKI.Name = "radioModeEKI";
            this.radioModeEKI.Size = new System.Drawing.Size(60, 27);
            this.radioModeEKI.TabIndex = 5;
            this.radioModeEKI.Text = "EKI";
            this.radioModeEKI.CheckedChanged += new AntdUI.BoolEventHandler(this.radioModeEKI_CheckedChanged);
            // 
            // radioModeRSI
            // 
            this.radioModeRSI.Checked = true;
            this.radioModeRSI.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioModeRSI.Location = new System.Drawing.Point(856, 8);
            this.radioModeRSI.Name = "radioModeRSI";
            this.radioModeRSI.Size = new System.Drawing.Size(60, 27);
            this.radioModeRSI.TabIndex = 4;
            this.radioModeRSI.Text = "RSI";
            this.radioModeRSI.CheckedChanged += new AntdUI.BoolEventHandler(this.radioModeRSI_CheckedChanged);
            // 
            // lblRobotCommMode
            // 
            this.lblRobotCommMode.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblRobotCommMode.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRobotCommMode.Location = new System.Drawing.Point(770, 12);
            this.lblRobotCommMode.Name = "lblRobotCommMode";
            this.lblRobotCommMode.Size = new System.Drawing.Size(80, 23);
            this.lblRobotCommMode.TabIndex = 3;
            this.lblRobotCommMode.Text = "通讯模式";
            // 
            // chkEnableRobot
            // 
            this.chkEnableRobot.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkEnableRobot.Location = new System.Drawing.Point(15, 3);
            this.chkEnableRobot.Name = "chkEnableRobot";
            this.chkEnableRobot.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkEnableRobot.Size = new System.Drawing.Size(219, 32);
            this.chkEnableRobot.TabIndex = 0;
            this.chkEnableRobot.Text = "启用机器人通讯";
            this.chkEnableRobot.CheckedChanged += new AntdUI.BoolEventHandler(this.chkEnable_CheckedChanged);
            // 
            // grpRobotConfig
            // 
            this.grpRobotConfig.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpRobotConfig.Controls.Add(this.btnRobotDisconnect);
            this.grpRobotConfig.Controls.Add(this.btnEkiConnect);
            this.grpRobotConfig.Controls.Add(this.btnRsiConnect);
            this.grpRobotConfig.Controls.Add(this.txtRobotPort);
            this.grpRobotConfig.Controls.Add(this.lblRobotPort);
            this.grpRobotConfig.Controls.Add(this.txtRobotIp);
            this.grpRobotConfig.Controls.Add(this.lblRobotIp);
            this.grpRobotConfig.Controls.Add(this.cmbRobotType);
            this.grpRobotConfig.Controls.Add(this.lblRobotType);
            this.grpRobotConfig.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpRobotConfig.Location = new System.Drawing.Point(15, 41);
            this.grpRobotConfig.Name = "grpRobotConfig";
            this.grpRobotConfig.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.grpRobotConfig.Size = new System.Drawing.Size(1170, 138);
            this.grpRobotConfig.TabIndex = 10;
            this.grpRobotConfig.TabStop = false;
            this.grpRobotConfig.Text = "机器人配置";
            // 
            // btnRobotDisconnect
            // 
            this.btnRobotDisconnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRobotDisconnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRobotDisconnect.Location = new System.Drawing.Point(285, 98);
            this.btnRobotDisconnect.Name = "btnRobotDisconnect";
            this.btnRobotDisconnect.Size = new System.Drawing.Size(120, 35);
            this.btnRobotDisconnect.TabIndex = 9;
            this.btnRobotDisconnect.Text = "断开";
            this.btnRobotDisconnect.Click += new System.EventHandler(this.btnRobotDisconnect_Click);
            // 
            // btnEkiConnect
            // 
            this.btnEkiConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnEkiConnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnEkiConnect.Location = new System.Drawing.Point(159, 98);
            this.btnEkiConnect.Name = "btnEkiConnect";
            this.btnEkiConnect.Size = new System.Drawing.Size(120, 35);
            this.btnEkiConnect.TabIndex = 8;
            this.btnEkiConnect.Text = "连接 EKI";
            this.btnEkiConnect.Click += new System.EventHandler(this.btnEkiConnect_Click);
            // 
            // btnRsiConnect
            // 
            this.btnRsiConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRsiConnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRsiConnect.Location = new System.Drawing.Point(19, 98);
            this.btnRsiConnect.Name = "btnRsiConnect";
            this.btnRsiConnect.Size = new System.Drawing.Size(120, 35);
            this.btnRsiConnect.TabIndex = 7;
            this.btnRsiConnect.Text = "启动 RSI";
            this.btnRsiConnect.Click += new System.EventHandler(this.btnRsiConnect_Click);
            // 
            // txtRobotPort
            // 
            this.txtRobotPort.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtRobotPort.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRobotPort.Location = new System.Drawing.Point(516, 41);
            this.txtRobotPort.Name = "txtRobotPort";
            this.txtRobotPort.Size = new System.Drawing.Size(100, 36);
            this.txtRobotPort.TabIndex = 6;
            // 
            // lblRobotPort
            // 
            this.lblRobotPort.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblRobotPort.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRobotPort.Location = new System.Drawing.Point(470, 47);
            this.lblRobotPort.Name = "lblRobotPort";
            this.lblRobotPort.Size = new System.Drawing.Size(40, 23);
            this.lblRobotPort.TabIndex = 5;
            this.lblRobotPort.Text = "端口";
            // 
            // txtRobotIp
            // 
            this.txtRobotIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRobotIp.Location = new System.Drawing.Point(266, 41);
            this.txtRobotIp.Name = "txtRobotIp";
            this.txtRobotIp.Size = new System.Drawing.Size(180, 29);
            this.txtRobotIp.TabIndex = 4;
            // 
            // lblRobotIp
            // 
            this.lblRobotIp.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblRobotIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRobotIp.Location = new System.Drawing.Point(240, 44);
            this.lblRobotIp.Name = "lblRobotIp";
            this.lblRobotIp.Size = new System.Drawing.Size(20, 20);
            this.lblRobotIp.TabIndex = 3;
            this.lblRobotIp.Text = "IP";
            // 
            // cmbRobotType
            // 
            this.cmbRobotType.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbRobotType.Items.AddRange(new object[] {
            "KUKA"});
            this.cmbRobotType.Location = new System.Drawing.Point(83, 41);
            this.cmbRobotType.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbRobotType.MinimumSize = new System.Drawing.Size(63, 0);
            this.cmbRobotType.Name = "cmbRobotType";
            this.cmbRobotType.Size = new System.Drawing.Size(116, 29);
            this.cmbRobotType.TabIndex = 2;
            this.cmbRobotType.Text = "KUKA";
            // 
            // lblRobotType
            // 
            this.lblRobotType.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblRobotType.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRobotType.Location = new System.Drawing.Point(19, 47);
            this.lblRobotType.Name = "lblRobotType";
            this.lblRobotType.Size = new System.Drawing.Size(40, 23);
            this.lblRobotType.TabIndex = 1;
            this.lblRobotType.Text = "类型";
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.panelGeneral);
            this.tabGeneral.Location = new System.Drawing.Point(-2416, -584);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Size = new System.Drawing.Size(1208, 292);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "通用通讯";
            // 
            // panelGeneral
            // 
            this.panelGeneral.AutoScroll = true;
            this.panelGeneral.Controls.Add(this.chkEnableGeneral);
            this.panelGeneral.Controls.Add(this.grpServer);
            this.panelGeneral.Controls.Add(this.grpClient);
            this.panelGeneral.Controls.Add(this.txtSend);
            this.panelGeneral.Controls.Add(this.btnSend);
            this.panelGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelGeneral.Location = new System.Drawing.Point(0, 0);
            this.panelGeneral.Name = "panelGeneral";
            this.panelGeneral.Size = new System.Drawing.Size(1208, 292);
            this.panelGeneral.TabIndex = 2;
            // 
            // chkEnableGeneral
            // 
            this.chkEnableGeneral.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkEnableGeneral.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkEnableGeneral.Location = new System.Drawing.Point(15, 8);
            this.chkEnableGeneral.Name = "chkEnableGeneral";
            this.chkEnableGeneral.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkEnableGeneral.Size = new System.Drawing.Size(166, 46);
            this.chkEnableGeneral.TabIndex = 0;
            this.chkEnableGeneral.Text = "启用通用通讯";
            this.chkEnableGeneral.CheckedChanged += new AntdUI.BoolEventHandler(this.chkEnable_CheckedChanged);
            // 
            // grpServer
            // 
            this.grpServer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpServer.Controls.Add(this.lblServerIp);
            this.grpServer.Controls.Add(this.txtServerIp);
            this.grpServer.Controls.Add(this.lblServerPort);
            this.grpServer.Controls.Add(this.txtServerPort);
            this.grpServer.Controls.Add(this.btnServerStart);
            this.grpServer.Controls.Add(this.btnServerStop);
            this.grpServer.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpServer.Location = new System.Drawing.Point(15, 38);
            this.grpServer.Name = "grpServer";
            this.grpServer.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.grpServer.Size = new System.Drawing.Size(580, 82);
            this.grpServer.TabIndex = 1;
            this.grpServer.TabStop = false;
            this.grpServer.Text = "TCP 服务端";
            // 
            // lblServerIp
            // 
            this.lblServerIp.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblServerIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblServerIp.Location = new System.Drawing.Point(12, 47);
            this.lblServerIp.Name = "lblServerIp";
            this.lblServerIp.Size = new System.Drawing.Size(20, 20);
            this.lblServerIp.TabIndex = 0;
            this.lblServerIp.Text = "IP";
            // 
            // txtServerIp
            // 
            this.txtServerIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtServerIp.Location = new System.Drawing.Point(45, 42);
            this.txtServerIp.MinimumSize = new System.Drawing.Size(1, 1);
            this.txtServerIp.Name = "txtServerIp";
            this.txtServerIp.Padding = new System.Windows.Forms.Padding(1);
            this.txtServerIp.Size = new System.Drawing.Size(180, 29);
            this.txtServerIp.TabIndex = 1;
            // 
            // lblServerPort
            // 
            this.lblServerPort.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblServerPort.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblServerPort.Location = new System.Drawing.Point(242, 47);
            this.lblServerPort.Name = "lblServerPort";
            this.lblServerPort.Size = new System.Drawing.Size(40, 23);
            this.lblServerPort.TabIndex = 2;
            this.lblServerPort.Text = "端口";
            // 
            // txtServerPort
            // 
            this.txtServerPort.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtServerPort.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtServerPort.Location = new System.Drawing.Point(291, 42);
            this.txtServerPort.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtServerPort.Name = "txtServerPort";
            this.txtServerPort.Padding = new System.Windows.Forms.Padding(5);
            this.txtServerPort.Size = new System.Drawing.Size(100, 29);
            this.txtServerPort.TabIndex = 3;
            // 
            // btnServerStart
            // 
            this.btnServerStart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnServerStart.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnServerStart.Location = new System.Drawing.Point(410, 40);
            this.btnServerStart.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnServerStart.Name = "btnServerStart";
            this.btnServerStart.Size = new System.Drawing.Size(30, 30);
            this.btnServerStart.TabIndex = 4;
            this.btnServerStart.Text = "▶";
            this.btnServerStart.Click += new System.EventHandler(this.btnServerStart_Click);
            // 
            // btnServerStop
            // 
            this.btnServerStop.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnServerStop.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnServerStop.Location = new System.Drawing.Point(440, 40);
            this.btnServerStop.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnServerStop.Name = "btnServerStop";
            this.btnServerStop.Size = new System.Drawing.Size(30, 30);
            this.btnServerStop.TabIndex = 5;
            this.btnServerStop.Text = "■";
            this.btnServerStop.Click += new System.EventHandler(this.btnServerStop_Click);
            // 
            // grpClient
            // 
            this.grpClient.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpClient.Controls.Add(this.lblClientIp);
            this.grpClient.Controls.Add(this.txtClientIp);
            this.grpClient.Controls.Add(this.lblClientPort);
            this.grpClient.Controls.Add(this.txtClientPort);
            this.grpClient.Controls.Add(this.btnClientConnect);
            this.grpClient.Controls.Add(this.btnClientDisconnect);
            this.grpClient.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpClient.Location = new System.Drawing.Point(605, 38);
            this.grpClient.Name = "grpClient";
            this.grpClient.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.grpClient.Size = new System.Drawing.Size(580, 82);
            this.grpClient.TabIndex = 2;
            this.grpClient.TabStop = false;
            this.grpClient.Text = "TCP 客户端";
            // 
            // lblClientIp
            // 
            this.lblClientIp.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblClientIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblClientIp.Location = new System.Drawing.Point(12, 47);
            this.lblClientIp.Name = "lblClientIp";
            this.lblClientIp.Size = new System.Drawing.Size(20, 20);
            this.lblClientIp.TabIndex = 0;
            this.lblClientIp.Text = "IP";
            // 
            // txtClientIp
            // 
            this.txtClientIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtClientIp.Location = new System.Drawing.Point(45, 42);
            this.txtClientIp.MinimumSize = new System.Drawing.Size(1, 1);
            this.txtClientIp.Name = "txtClientIp";
            this.txtClientIp.Padding = new System.Windows.Forms.Padding(1);
            this.txtClientIp.Size = new System.Drawing.Size(180, 29);
            this.txtClientIp.TabIndex = 1;
            // 
            // lblClientPort
            // 
            this.lblClientPort.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblClientPort.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblClientPort.Location = new System.Drawing.Point(242, 47);
            this.lblClientPort.Name = "lblClientPort";
            this.lblClientPort.Size = new System.Drawing.Size(40, 23);
            this.lblClientPort.TabIndex = 2;
            this.lblClientPort.Text = "端口";
            // 
            // txtClientPort
            // 
            this.txtClientPort.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtClientPort.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtClientPort.Location = new System.Drawing.Point(291, 42);
            this.txtClientPort.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtClientPort.Name = "txtClientPort";
            this.txtClientPort.Padding = new System.Windows.Forms.Padding(5);
            this.txtClientPort.Size = new System.Drawing.Size(100, 29);
            this.txtClientPort.TabIndex = 3;
            // 
            // btnClientConnect
            // 
            this.btnClientConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClientConnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClientConnect.Location = new System.Drawing.Point(410, 40);
            this.btnClientConnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnClientConnect.Name = "btnClientConnect";
            this.btnClientConnect.Size = new System.Drawing.Size(30, 30);
            this.btnClientConnect.TabIndex = 4;
            this.btnClientConnect.Text = "▶";
            this.btnClientConnect.Click += new System.EventHandler(this.btnClientConnect_Click);
            // 
            // btnClientDisconnect
            // 
            this.btnClientDisconnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClientDisconnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClientDisconnect.Location = new System.Drawing.Point(440, 40);
            this.btnClientDisconnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnClientDisconnect.Name = "btnClientDisconnect";
            this.btnClientDisconnect.Size = new System.Drawing.Size(30, 30);
            this.btnClientDisconnect.TabIndex = 5;
            this.btnClientDisconnect.Text = "■";
            this.btnClientDisconnect.Click += new System.EventHandler(this.btnClientDisconnect_Click);
            // 
            // txtSend
            // 
            this.txtSend.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSend.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtSend.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtSend.Location = new System.Drawing.Point(14, 129);
            this.txtSend.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtSend.Name = "txtSend";
            this.txtSend.Padding = new System.Windows.Forms.Padding(5);
            this.txtSend.PlaceholderText = "输入要发送的消息";
            this.txtSend.Size = new System.Drawing.Size(1080, 29);
            this.txtSend.TabIndex = 3;
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSend.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSend.Location = new System.Drawing.Point(1100, 128);
            this.btnSend.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(80, 30);
            this.btnSend.TabIndex = 4;
            this.btnSend.Text = "发送";
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // tabPLC
            // 
            this.tabPLC.Controls.Add(this.panelPLC);
            this.tabPLC.Location = new System.Drawing.Point(-2416, -584);
            this.tabPLC.Name = "tabPLC";
            this.tabPLC.Size = new System.Drawing.Size(1208, 292);
            this.tabPLC.TabIndex = 2;
            this.tabPLC.Text = "PLC通讯";
            // 
            // panelPLC
            // 
            this.panelPLC.AutoScroll = true;
            this.panelPLC.Controls.Add(this.chkEnablePLC);
            this.panelPLC.Controls.Add(this.grpPLCConfig);
            this.panelPLC.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPLC.Location = new System.Drawing.Point(0, 0);
            this.panelPLC.Name = "panelPLC";
            this.panelPLC.Size = new System.Drawing.Size(1208, 292);
            this.panelPLC.TabIndex = 0;
            // 
            // chkEnablePLC
            // 
            this.chkEnablePLC.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkEnablePLC.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkEnablePLC.Location = new System.Drawing.Point(15, 8);
            this.chkEnablePLC.Name = "chkEnablePLC";
            this.chkEnablePLC.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkEnablePLC.Size = new System.Drawing.Size(156, 46);
            this.chkEnablePLC.TabIndex = 0;
            this.chkEnablePLC.Text = "启用PLC通讯";
            this.chkEnablePLC.CheckedChanged += new AntdUI.BoolEventHandler(this.chkEnable_CheckedChanged);
            // 
            // grpPLCConfig
            // 
            this.grpPLCConfig.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPLCConfig.Controls.Add(this.btnPlcGsdGenerate);
            this.grpPLCConfig.Controls.Add(this.btnPlcDisconnect);
            this.grpPLCConfig.Controls.Add(this.btnPlcConnect);
            this.grpPLCConfig.Controls.Add(this.txtPlcSlot);
            this.grpPLCConfig.Controls.Add(this.lblPlcSlot);
            this.grpPLCConfig.Controls.Add(this.txtPlcRack);
            this.grpPLCConfig.Controls.Add(this.lblPlcRack);
            this.grpPLCConfig.Controls.Add(this.txtPlcIp);
            this.grpPLCConfig.Controls.Add(this.lblPlcIp);
            this.grpPLCConfig.Controls.Add(this.cmbPlcType);
            this.grpPLCConfig.Controls.Add(this.lblPlcType);
            this.grpPLCConfig.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.grpPLCConfig.Location = new System.Drawing.Point(15, 38);
            this.grpPLCConfig.Name = "grpPLCConfig";
            this.grpPLCConfig.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.grpPLCConfig.Size = new System.Drawing.Size(1170, 175);
            this.grpPLCConfig.TabIndex = 12;
            this.grpPLCConfig.TabStop = false;
            this.grpPLCConfig.Text = "PLC 配置";
            // 
            // btnPlcGsdGenerate
            // 
            this.btnPlcGsdGenerate.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlcGsdGenerate.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnPlcGsdGenerate.Location = new System.Drawing.Point(290, 95);
            this.btnPlcGsdGenerate.Name = "btnPlcGsdGenerate";
            this.btnPlcGsdGenerate.Size = new System.Drawing.Size(160, 35);
            this.btnPlcGsdGenerate.TabIndex = 11;
            this.btnPlcGsdGenerate.Text = "生成 GSDML";
            this.btnPlcGsdGenerate.Click += new System.EventHandler(this.btnPlcGsdGenerate_Click);
            // 
            // btnPlcDisconnect
            // 
            this.btnPlcDisconnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlcDisconnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnPlcDisconnect.Location = new System.Drawing.Point(150, 95);
            this.btnPlcDisconnect.Name = "btnPlcDisconnect";
            this.btnPlcDisconnect.Size = new System.Drawing.Size(120, 35);
            this.btnPlcDisconnect.TabIndex = 10;
            this.btnPlcDisconnect.Text = "断开";
            this.btnPlcDisconnect.Click += new System.EventHandler(this.btnPlcDisconnect_Click);
            // 
            // btnPlcConnect
            // 
            this.btnPlcConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlcConnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnPlcConnect.Location = new System.Drawing.Point(10, 95);
            this.btnPlcConnect.Name = "btnPlcConnect";
            this.btnPlcConnect.Size = new System.Drawing.Size(120, 35);
            this.btnPlcConnect.TabIndex = 9;
            this.btnPlcConnect.Text = "连接";
            this.btnPlcConnect.Click += new System.EventHandler(this.btnPlcConnect_Click);
            // 
            // txtPlcSlot
            // 
            this.txtPlcSlot.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtPlcSlot.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtPlcSlot.Location = new System.Drawing.Point(636, 42);
            this.txtPlcSlot.Name = "txtPlcSlot";
            this.txtPlcSlot.Padding = new System.Windows.Forms.Padding(5);
            this.txtPlcSlot.Size = new System.Drawing.Size(60, 29);
            this.txtPlcSlot.TabIndex = 8;
            this.txtPlcSlot.Text = "1";
            // 
            // lblPlcSlot
            // 
            this.lblPlcSlot.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblPlcSlot.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPlcSlot.Location = new System.Drawing.Point(590, 45);
            this.lblPlcSlot.Name = "lblPlcSlot";
            this.lblPlcSlot.Size = new System.Drawing.Size(40, 23);
            this.lblPlcSlot.TabIndex = 7;
            this.lblPlcSlot.Text = "插槽";
            // 
            // txtPlcRack
            // 
            this.txtPlcRack.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtPlcRack.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtPlcRack.Location = new System.Drawing.Point(516, 42);
            this.txtPlcRack.Name = "txtPlcRack";
            this.txtPlcRack.Padding = new System.Windows.Forms.Padding(5);
            this.txtPlcRack.Size = new System.Drawing.Size(60, 29);
            this.txtPlcRack.TabIndex = 6;
            this.txtPlcRack.Text = "0";
            // 
            // lblPlcRack
            // 
            this.lblPlcRack.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblPlcRack.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPlcRack.Location = new System.Drawing.Point(470, 45);
            this.lblPlcRack.Name = "lblPlcRack";
            this.lblPlcRack.Size = new System.Drawing.Size(40, 23);
            this.lblPlcRack.TabIndex = 5;
            this.lblPlcRack.Text = "机架";
            // 
            // txtPlcIp
            // 
            this.txtPlcIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtPlcIp.Location = new System.Drawing.Point(270, 42);
            this.txtPlcIp.Name = "txtPlcIp";
            this.txtPlcIp.Padding = new System.Windows.Forms.Padding(1);
            this.txtPlcIp.Size = new System.Drawing.Size(180, 29);
            this.txtPlcIp.TabIndex = 4;
            // 
            // lblPlcIp
            // 
            this.lblPlcIp.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblPlcIp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPlcIp.Location = new System.Drawing.Point(240, 45);
            this.lblPlcIp.Name = "lblPlcIp";
            this.lblPlcIp.Size = new System.Drawing.Size(20, 20);
            this.lblPlcIp.TabIndex = 3;
            this.lblPlcIp.Text = "IP";
            // 
            // cmbPlcType
            // 
            this.cmbPlcType.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbPlcType.Items.AddRange(new object[] {
            "Siemens S7",
            "Profinet"});
            this.cmbPlcType.Location = new System.Drawing.Point(56, 42);
            this.cmbPlcType.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbPlcType.MinimumSize = new System.Drawing.Size(63, 0);
            this.cmbPlcType.Name = "cmbPlcType";
            this.cmbPlcType.Padding = new System.Windows.Forms.Padding(0, 0, 30, 2);
            this.cmbPlcType.Size = new System.Drawing.Size(150, 29);
            this.cmbPlcType.TabIndex = 2;
            this.cmbPlcType.Text = "Siemens S7";
            // 
            // lblPlcType
            // 
            this.lblPlcType.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblPlcType.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPlcType.Location = new System.Drawing.Point(10, 45);
            this.lblPlcType.Name = "lblPlcType";
            this.lblPlcType.Size = new System.Drawing.Size(40, 23);
            this.lblPlcType.TabIndex = 1;
            this.lblPlcType.Text = "类型";
            // 
            // panelRadio
            // 
            this.panelRadio.Controls.Add(this.btnKeepConfig);
            this.panelRadio.Controls.Add(this.radioGeneral);
            this.panelRadio.Controls.Add(this.radioRobot);
            this.panelRadio.Controls.Add(this.radioPLC);
            this.panelRadio.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRadio.Location = new System.Drawing.Point(5, 5);
            this.panelRadio.Margin = new System.Windows.Forms.Padding(0);
            this.panelRadio.Name = "panelRadio";
            this.panelRadio.Size = new System.Drawing.Size(1208, 40);
            this.panelRadio.TabIndex = 0;
            // 
            // btnKeepConfig
            // 
            this.btnKeepConfig.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnKeepConfig.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnKeepConfig.Location = new System.Drawing.Point(1115, 3);
            this.btnKeepConfig.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnKeepConfig.Name = "btnKeepConfig";
            this.btnKeepConfig.Size = new System.Drawing.Size(90, 35);
            this.btnKeepConfig.TabIndex = 3;
            this.btnKeepConfig.Text = "保存配置";
            this.btnKeepConfig.Click += new System.EventHandler(this.btnKeepConfig_Click);
            // 
            // radioGeneral
            // 
            this.radioGeneral.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioGeneral.Location = new System.Drawing.Point(157, 3);
            this.radioGeneral.Name = "radioGeneral";
            this.radioGeneral.Size = new System.Drawing.Size(126, 27);
            this.radioGeneral.TabIndex = 1;
            this.radioGeneral.Text = "通用通讯";
            this.radioGeneral.CheckedChanged += new AntdUI.BoolEventHandler(this.OnRadioModeChanged);
            // 
            // radioRobot
            // 
            this.radioRobot.Checked = true;
            this.radioRobot.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioRobot.Location = new System.Drawing.Point(15, 3);
            this.radioRobot.Name = "radioRobot";
            this.radioRobot.Size = new System.Drawing.Size(136, 27);
            this.radioRobot.TabIndex = 0;
            this.radioRobot.Text = "机器人通讯";
            this.radioRobot.CheckedChanged += new AntdUI.BoolEventHandler(this.OnRadioModeChanged);
            // 
            // radioPLC
            // 
            this.radioPLC.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioPLC.Location = new System.Drawing.Point(289, 3);
            this.radioPLC.Name = "radioPLC";
            this.radioPLC.Size = new System.Drawing.Size(116, 27);
            this.radioPLC.TabIndex = 2;
            this.radioPLC.Text = "PLC通讯";
            this.radioPLC.CheckedChanged += new AntdUI.BoolEventHandler(this.OnRadioModeChanged);
            // 
            // CommPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.gridMain);
            this.Name = "CommPage";
            this.Size = new System.Drawing.Size(1218, 1007);
            this.gridMain.ResumeLayout(false);
            this.tabsSettings.ResumeLayout(false);
            this.tabRobot.ResumeLayout(false);
            this.panelRobot.ResumeLayout(false);
            this.panelRobot.PerformLayout();
            this.grpRobotConfig.ResumeLayout(false);
            this.grpRobotConfig.PerformLayout();
            this.tabGeneral.ResumeLayout(false);
            this.panelGeneral.ResumeLayout(false);
            this.panelGeneral.PerformLayout();
            this.grpServer.ResumeLayout(false);
            this.grpServer.PerformLayout();
            this.grpClient.ResumeLayout(false);
            this.grpClient.PerformLayout();
            this.tabPLC.ResumeLayout(false);
            this.panelPLC.ResumeLayout(false);
            this.panelPLC.PerformLayout();
            this.grpPLCConfig.ResumeLayout(false);
            this.grpPLCConfig.PerformLayout();
            this.panelRadio.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        // 顶层布局
        private AntdUI.GridPanel gridMain;
        private System.Windows.Forms.Panel panelRadio;
        private AntdUI.Radio radioPLC;
        private AntdUI.Radio radioRobot;
        private AntdUI.Radio radioGeneral;
        private AntdUI.Button btnKeepConfig;
        private AntdUI.Tabs tabsSettings;
        private AntdUI.TabPage tabPLC;
        private AntdUI.TabPage tabRobot;
        private AntdUI.TabPage tabGeneral;
        private AntdUI.Checkbox chkShowCommLog;
        private AntdUI.Input inputLog;

        // PLC 模式
        private System.Windows.Forms.Panel panelPLC;
        private System.Windows.Forms.GroupBox grpPLCConfig;
        private AntdUI.Checkbox chkEnablePLC;
        private AntdUI.Label lblPlcType;
        private AntdUI.Select cmbPlcType;
        private AntdUI.Label lblPlcIp;
        private AntdUI.Input txtPlcIp;
        private AntdUI.Label lblPlcRack;
        private AntdUI.Input txtPlcRack;
        private AntdUI.Label lblPlcSlot;
        private AntdUI.Input txtPlcSlot;
        private AntdUI.Button btnPlcConnect;
        private AntdUI.Button btnPlcDisconnect;
        private AntdUI.Button btnPlcGsdGenerate;

        // 机器人模式
        private System.Windows.Forms.Panel panelRobot;
        private System.Windows.Forms.GroupBox grpRobotConfig;
        private AntdUI.Checkbox chkEnableRobot;
        private AntdUI.Label lblRobotType;
        private AntdUI.Select cmbRobotType;
        private AntdUI.Label lblRobotIp;
        private AntdUI.Input txtRobotIp;
        private AntdUI.Label lblRobotPort;
        private AntdUI.Input txtRobotPort;
        private AntdUI.Button btnRsiConnect;
        private AntdUI.Button btnEkiConnect;
        private AntdUI.Button btnRobotDisconnect;
        private AntdUI.Label lblRobotCommMode;
        private AntdUI.Radio radioModeRSI;
        private AntdUI.Radio radioModeEKI;

        // 通用通讯模式
        private System.Windows.Forms.Panel panelGeneral;
        private AntdUI.Checkbox chkEnableGeneral;
        private System.Windows.Forms.GroupBox grpServer;
        private AntdUI.Label lblServerIp;
        private AntdUI.Input txtServerIp;
        private AntdUI.Label lblServerPort;
        private AntdUI.Input txtServerPort;
        private AntdUI.Button btnServerStart;
        private AntdUI.Button btnServerStop;
        private System.Windows.Forms.GroupBox grpClient;
        private AntdUI.Label lblClientIp;
        private AntdUI.Input txtClientIp;
        private AntdUI.Label lblClientPort;
        private AntdUI.Input txtClientPort;
        private AntdUI.Button btnClientConnect;
        private AntdUI.Button btnClientDisconnect;
        private AntdUI.Input txtSend;
        private AntdUI.Button btnSend;
    }
}