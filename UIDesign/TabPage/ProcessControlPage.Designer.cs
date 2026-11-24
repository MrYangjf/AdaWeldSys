namespace AdaWeldSystem.UIDesign.TabPage
{
    partial class ProcessControlPage
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            AntdUI.Tabs.StyleCard styleCard1 = new AntdUI.Tabs.StyleCard();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.label7 = new AntdUI.Label();
            this.btnGalvoManualStart = new AntdUI.Button();
            this.btnGalvoManualStop = new AntdUI.Button();
            this.label8 = new AntdUI.Label();
            this.btnCorAutoParam = new AntdUI.Button();
            this.btnCorBaseline = new AntdUI.Button();
            this.btnShowAutoParam = new AntdUI.Button();
            this.Sensitivity = new AntdUI.Input();
            this.uiSymbolLabel14 = new AntdUI.Label();
            this.uiSymbolLabel6 = new AntdUI.Label();
            this.lblBaseTitle = new AntdUI.Label();
            this.LaserDiameter = new AntdUI.Input();
            this.MinSeamWidth = new AntdUI.Input();
            this.uiSymbolLabel8 = new AntdUI.Label();
            this.uiSymbolLabel13 = new AntdUI.Label();
            this.FeedSpeed = new AntdUI.Input();
            this.uiSymbolLabel11 = new AntdUI.Label();
            this.uiSymbolLabel5 = new AntdUI.Label();
            this.MaxSeamWidth = new AntdUI.Input();
            this.LaserPower = new AntdUI.Input();
            this.uiSymbolLabel9 = new AntdUI.Label();
            this.SeamWidth = new AntdUI.Input();
            this.uiSymbolLabel7 = new AntdUI.Label();
            this.RobotSpeed = new AntdUI.Input();
            this.uiSymbolLabel16 = new AntdUI.Label();
            this.uiComboBox1 = new AntdUI.Select();
            this.uiSymbolLabel3 = new AntdUI.Label();
            this.PlateType = new AntdUI.Input();
            this.uiSymbolLabel4 = new AntdUI.Label();
            this.Platethickness = new AntdUI.Input();
            this.lblWireTitle = new AntdUI.Label();
            this.WireDiameter = new AntdUI.Input();
            this.uiSymbolLabel1 = new AntdUI.Label();
            this.uiSymbolLabel2 = new AntdUI.Label();
            this.WireType = new AntdUI.Input();
            this.panelWeldList = new AntdUI.Panel();
            this.gridWeldZones = new System.Windows.Forms.TableLayoutPanel();
            this.panelConfigList = new AntdUI.Panel();
            this.gridConfigList = new System.Windows.Forms.TableLayoutPanel();
            this.lblConfigListTitle = new AntdUI.Label();
            this.tableConfigList = new AntdUI.Table();
            this.btnConfigAddNew = new AntdUI.Button();
            this.btnConfigDelete = new AntdUI.Button();
            this.gridPanelRight = new System.Windows.Forms.TableLayoutPanel();
            this.panelAdaparam = new AntdUI.Panel();
            this.gridAdaparam = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new AntdUI.Label();
            this.panel2 = new AntdUI.Panel();
            this.gridPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.lblWeldAdjTitle = new AntdUI.Label();
            this.btnReadAutoParam = new AntdUI.Button();
            this.lblFlowCountNum = new AntdUI.Label();
            this.txtFlowCountNum = new AntdUI.Input();
            this.lblFlowMaxConsecFail = new AntdUI.Label();
            this.txtFlowMaxConsecFail = new AntdUI.Input();
            this.btnSaveAutoParam = new AntdUI.Button();
            this.tabsProcess = new AntdUI.Tabs();
            this.tabPageAdaptiveSetup = new AntdUI.TabPage();
            this.tabPageQuickSetup = new AntdUI.TabPage();
            this.gridQuickZones = new System.Windows.Forms.TableLayoutPanel();
            this.panelProcessDebug = new AntdUI.Panel();
            this.tlPLASetting = new System.Windows.Forms.TableLayoutPanel();
            this.label4 = new AntdUI.Label();
            this.label3 = new AntdUI.Label();
            this.radioQuickLaDefaultFocus = new AntdUI.Radio();
            this.radioQuickLaPose1 = new AntdUI.Radio();
            this.radioQuickLaAuto = new AntdUI.Radio();
            this.radioQuickLaSpot2 = new AntdUI.Radio();
            this.radioQuickLaSpot1 = new AntdUI.Radio();
            this.label2 = new System.Windows.Forms.Label();
            this.lblLaSpot1 = new System.Windows.Forms.Label();
            this.txtLaSpot1 = new AntdUI.Input();
            this.lblLaSpot2 = new System.Windows.Forms.Label();
            this.txtLaSpot2 = new AntdUI.Input();
            this.txtLaPose1 = new AntdUI.Input();
            this.lblLaDefaultFocus = new System.Windows.Forms.Label();
            this.lblLaPose1 = new System.Windows.Forms.Label();
            this.txtLaDefaultFocus = new AntdUI.Input();
            this.lblSpotTarget = new System.Windows.Forms.Label();
            this.txtLaSpotTarget = new AntdUI.Input();
            this.panelQuickTracking = new AntdUI.Panel();
            this.gridQuickTracking = new System.Windows.Forms.TableLayoutPanel();
            this.label6 = new AntdUI.Label();
            this.label5 = new AntdUI.Label();
            this.lblQuickTracking = new AntdUI.Label();
            this.radioQuickYAuto = new AntdUI.Radio();
            this.radioQuickYForceCalibPose = new AntdUI.Radio();
            this.radioQuickYForceHome = new AntdUI.Radio();
            this.lblCalibCenterY = new AntdUI.Label();
            this.txtCalibCenterY = new AntdUI.Input();
            this.lblCalibCenterZ = new AntdUI.Label();
            this.txtCalibCenterZ = new AntdUI.Input();
            this.lblTeachLockY = new AntdUI.Label();
            this.txtTeachLockY = new AntdUI.Input();
            this.panelQuickTa = new AntdUI.Panel();
            this.gridQuickTa = new System.Windows.Forms.TableLayoutPanel();
            this.label10 = new AntdUI.Label();
            this.label9 = new AntdUI.Label();
            this.lblQuickTaTitle = new System.Windows.Forms.Label();
            this.radioQuickTaAuto = new AntdUI.Radio();
            this.radioQuickTaRetract = new AntdUI.Radio();
            this.radioQuickTaBreak = new AntdUI.Radio();
            this.radioQuickTaPose1 = new AntdUI.Radio();
            this.radioQuickTaPose2 = new AntdUI.Radio();
            this.radioQuickTaZeroPose = new AntdUI.Radio();
            this.lblTaForce = new AntdUI.Label();
            this.txtTaForce = new AntdUI.Input();
            this.lblTaBreakPos = new AntdUI.Label();
            this.txtTaBreakPos = new AntdUI.Input();
            this.lblTaPose1 = new AntdUI.Label();
            this.txtTaPose1 = new AntdUI.Input();
            this.lblTaPose2 = new AntdUI.Label();
            this.txtTaPose2 = new AntdUI.Input();
            this.lblTaZeroPose = new AntdUI.Label();
            this.txtTaZeroPose = new AntdUI.Input();
            this.panelQuickLa = new AntdUI.Panel();
            this.gridQuickLa = new System.Windows.Forms.TableLayoutPanel();
            this.radioQuickLaGalvoArc = new AntdUI.Radio();
            this.lblQuickLaTitle = new System.Windows.Forms.Label();
            this.radioQuickLaGalvoLine = new AntdUI.Radio();
            this.radioQuickLaGalvoEight = new AntdUI.Radio();
            this.radioQuickLaGalvoCircle = new AntdUI.Radio();
            this.lblGalvoLength = new System.Windows.Forms.Label();
            this.txtGalvoLength = new AntdUI.Input();
            this.lblGalvoWidth = new System.Windows.Forms.Label();
            this.txtGalvoWidth = new AntdUI.Input();
            this.lblGalvoFreq = new System.Windows.Forms.Label();
            this.txtGalvoFreq = new AntdUI.Input();
            this.flowPanel2 = new AntdUI.FlowPanel();
            this.btnReadWeldPonts = new AntdUI.Button();
            this.btnApplyLaserCalib = new AntdUI.Button();
            this.btnSaveWeldPonts = new AntdUI.Button();
            this.panelWeldList.SuspendLayout();
            this.gridWeldZones.SuspendLayout();
            this.panelConfigList.SuspendLayout();
            this.gridConfigList.SuspendLayout();
            this.gridPanelRight.SuspendLayout();
            this.panelAdaparam.SuspendLayout();
            this.gridAdaparam.SuspendLayout();
            this.panel2.SuspendLayout();
            this.gridPanel1.SuspendLayout();
            this.tabsProcess.SuspendLayout();
            this.tabPageAdaptiveSetup.SuspendLayout();
            this.tabPageQuickSetup.SuspendLayout();
            this.gridQuickZones.SuspendLayout();
            this.panelProcessDebug.SuspendLayout();
            this.tlPLASetting.SuspendLayout();
            this.panelQuickTracking.SuspendLayout();
            this.gridQuickTracking.SuspendLayout();
            this.panelQuickTa.SuspendLayout();
            this.gridQuickTa.SuspendLayout();
            this.panelQuickLa.SuspendLayout();
            this.gridQuickLa.SuspendLayout();
            this.flowPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // timerRefresh
            // 
            this.timerRefresh.Interval = 500;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            // 
            // label7
            // 
            this.gridQuickLa.SetColumnSpan(this.label7, 4);
            this.label7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label7.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label7.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label7.Location = new System.Drawing.Point(3, 38);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(852, 24);
            this.label7.TabIndex = 27;
            this.label7.Text = "GA 指令";
            // 
            // btnGalvoManualStart
            // 
            this.btnGalvoManualStart.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnGalvoManualStart.Location = new System.Drawing.Point(0, 179);
            this.btnGalvoManualStart.Margin = new System.Windows.Forms.Padding(0);
            this.btnGalvoManualStart.Name = "btnGalvoManualStart";
            this.btnGalvoManualStart.Size = new System.Drawing.Size(214, 28);
            this.btnGalvoManualStart.TabIndex = 30;
            this.btnGalvoManualStart.Text = "手动下发";
            this.btnGalvoManualStart.Click += new System.EventHandler(this.btnGalvoManualStart_Click);
            // 
            // btnGalvoManualStop
            // 
            this.btnGalvoManualStop.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnGalvoManualStop.Location = new System.Drawing.Point(214, 179);
            this.btnGalvoManualStop.Margin = new System.Windows.Forms.Padding(0);
            this.btnGalvoManualStop.Name = "btnGalvoManualStop";
            this.btnGalvoManualStop.Size = new System.Drawing.Size(214, 28);
            this.btnGalvoManualStop.TabIndex = 31;
            this.btnGalvoManualStop.Text = "关闭退出";
            this.btnGalvoManualStop.Click += new System.EventHandler(this.btnGalvoManualStop_Click);
            // 
            // label8
            // 
            this.gridQuickLa.SetColumnSpan(this.label8, 4);
            this.label8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label8.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label8.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label8.Location = new System.Drawing.Point(3, 96);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(852, 24);
            this.label8.TabIndex = 28;
            this.label8.Text = "GA 设置";
            // 
            // btnCorAutoParam
            // 
            this.btnCorAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnCorAutoParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCorAutoParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCorAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnCorAutoParam.Ghost = true;
            this.btnCorAutoParam.IconSvg = "EditOutlined";
            this.btnCorAutoParam.Location = new System.Drawing.Point(641, 94);
            this.btnCorAutoParam.Name = "btnCorAutoParam";
            this.btnCorAutoParam.Size = new System.Drawing.Size(313, 24);
            this.btnCorAutoParam.TabIndex = 0;
            this.btnCorAutoParam.Text = "修改参数";
            this.btnCorAutoParam.Click += new System.EventHandler(this.btnCorAutoParam_Click);
            // 
            // btnCorBaseline
            // 
            this.btnCorBaseline.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnCorBaseline.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCorBaseline.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnCorBaseline.Ghost = true;
            this.btnCorBaseline.IconSvg = "EditOutlined";
            this.btnCorBaseline.Location = new System.Drawing.Point(957, 319);
            this.btnCorBaseline.Margin = new System.Windows.Forms.Padding(0);
            this.btnCorBaseline.Name = "btnCorBaseline";
            this.btnCorBaseline.Size = new System.Drawing.Size(321, 30);
            this.btnCorBaseline.TabIndex = 2;
            this.btnCorBaseline.Text = "修正基准";
            this.btnCorBaseline.Click += new System.EventHandler(this.btnCorBaseline_Click);
            // 
            // btnShowAutoParam
            // 
            this.btnShowAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnShowAutoParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnShowAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnShowAutoParam.Ghost = true;
            this.btnShowAutoParam.IconSvg = "TableOutlined";
            this.btnShowAutoParam.Location = new System.Drawing.Point(638, 319);
            this.btnShowAutoParam.Margin = new System.Windows.Forms.Padding(0);
            this.btnShowAutoParam.Name = "btnShowAutoParam";
            this.btnShowAutoParam.Size = new System.Drawing.Size(319, 30);
            this.btnShowAutoParam.TabIndex = 1;
            this.btnShowAutoParam.Text = "自适应参数表";
            this.btnShowAutoParam.Click += new System.EventHandler(this.btnShowAutoParam_Click);
            // 
            // Sensitivity
            // 
            this.Sensitivity.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.Sensitivity.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Sensitivity.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Sensitivity.Location = new System.Drawing.Point(319, 207);
            this.Sensitivity.Margin = new System.Windows.Forms.Padding(0);
            this.Sensitivity.Name = "Sensitivity";
            this.Sensitivity.Size = new System.Drawing.Size(319, 28);
            this.Sensitivity.TabIndex = 17;
            this.Sensitivity.Text = "0";
            // 
            // uiSymbolLabel14
            // 
            this.uiSymbolLabel14.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel14.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel14.Location = new System.Drawing.Point(3, 210);
            this.uiSymbolLabel14.Name = "uiSymbolLabel14";
            this.uiSymbolLabel14.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel14.TabIndex = 8;
            this.uiSymbolLabel14.Text = "自适应灵敏度(mm)";
            this.uiSymbolLabel14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel6
            // 
            this.uiSymbolLabel6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel6.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel6.Location = new System.Drawing.Point(641, 238);
            this.uiSymbolLabel6.Name = "uiSymbolLabel6";
            this.uiSymbolLabel6.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel6.TabIndex = 0;
            this.uiSymbolLabel6.Text = "光斑基准直径(mm)";
            this.uiSymbolLabel6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblBaseTitle
            // 
            this.gridAdaparam.SetColumnSpan(this.lblBaseTitle, 4);
            this.lblBaseTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblBaseTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lblBaseTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblBaseTitle.Location = new System.Drawing.Point(3, 152);
            this.lblBaseTitle.Name = "lblBaseTitle";
            this.lblBaseTitle.Size = new System.Drawing.Size(1272, 24);
            this.lblBaseTitle.TabIndex = 0;
            this.lblBaseTitle.Text = "自适应基准参数";
            // 
            // LaserDiameter
            // 
            this.LaserDiameter.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.LaserDiameter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LaserDiameter.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.LaserDiameter.Location = new System.Drawing.Point(957, 235);
            this.LaserDiameter.Margin = new System.Windows.Forms.Padding(0);
            this.LaserDiameter.Name = "LaserDiameter";
            this.LaserDiameter.Size = new System.Drawing.Size(321, 28);
            this.LaserDiameter.TabIndex = 9;
            this.LaserDiameter.Text = "0";
            // 
            // MinSeamWidth
            // 
            this.MinSeamWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MinSeamWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MinSeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.MinSeamWidth.Location = new System.Drawing.Point(319, 179);
            this.MinSeamWidth.Margin = new System.Windows.Forms.Padding(0);
            this.MinSeamWidth.Name = "MinSeamWidth";
            this.MinSeamWidth.Size = new System.Drawing.Size(319, 28);
            this.MinSeamWidth.TabIndex = 16;
            this.MinSeamWidth.Text = "0";
            // 
            // uiSymbolLabel8
            // 
            this.uiSymbolLabel8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel8.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel8.Location = new System.Drawing.Point(641, 266);
            this.uiSymbolLabel8.Name = "uiSymbolLabel8";
            this.uiSymbolLabel8.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel8.TabIndex = 3;
            this.uiSymbolLabel8.Text = "送丝基准速度(mm/s)";
            this.uiSymbolLabel8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel13
            // 
            this.uiSymbolLabel13.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel13.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel13.Location = new System.Drawing.Point(3, 182);
            this.uiSymbolLabel13.Name = "uiSymbolLabel13";
            this.uiSymbolLabel13.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel13.TabIndex = 7;
            this.uiSymbolLabel13.Text = "接头自适应宽度下限(mm)";
            this.uiSymbolLabel13.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FeedSpeed
            // 
            this.FeedSpeed.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.FeedSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FeedSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.FeedSpeed.Location = new System.Drawing.Point(957, 263);
            this.FeedSpeed.Margin = new System.Windows.Forms.Padding(0);
            this.FeedSpeed.Name = "FeedSpeed";
            this.FeedSpeed.Size = new System.Drawing.Size(321, 28);
            this.FeedSpeed.TabIndex = 12;
            this.FeedSpeed.Text = "0";
            // 
            // uiSymbolLabel11
            // 
            this.uiSymbolLabel11.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel11.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel11.Location = new System.Drawing.Point(641, 182);
            this.uiSymbolLabel11.Name = "uiSymbolLabel11";
            this.uiSymbolLabel11.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel11.TabIndex = 6;
            this.uiSymbolLabel11.Text = "接头自适应宽度上限(mm)";
            this.uiSymbolLabel11.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel5
            // 
            this.uiSymbolLabel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel5.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel5.Location = new System.Drawing.Point(3, 266);
            this.uiSymbolLabel5.Name = "uiSymbolLabel5";
            this.uiSymbolLabel5.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel5.TabIndex = 1;
            this.uiSymbolLabel5.Text = "激光基准功率(W)";
            this.uiSymbolLabel5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MaxSeamWidth
            // 
            this.MaxSeamWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MaxSeamWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MaxSeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.MaxSeamWidth.Location = new System.Drawing.Point(957, 179);
            this.MaxSeamWidth.Margin = new System.Windows.Forms.Padding(0);
            this.MaxSeamWidth.Name = "MaxSeamWidth";
            this.MaxSeamWidth.Size = new System.Drawing.Size(321, 28);
            this.MaxSeamWidth.TabIndex = 15;
            this.MaxSeamWidth.Text = "0";
            // 
            // LaserPower
            // 
            this.LaserPower.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.LaserPower.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LaserPower.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.LaserPower.Location = new System.Drawing.Point(319, 263);
            this.LaserPower.Margin = new System.Windows.Forms.Padding(0);
            this.LaserPower.Name = "LaserPower";
            this.LaserPower.Size = new System.Drawing.Size(319, 28);
            this.LaserPower.TabIndex = 10;
            this.LaserPower.Text = "0";
            // 
            // uiSymbolLabel9
            // 
            this.uiSymbolLabel9.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel9.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel9.Location = new System.Drawing.Point(3, 238);
            this.uiSymbolLabel9.Name = "uiSymbolLabel9";
            this.uiSymbolLabel9.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel9.TabIndex = 4;
            this.uiSymbolLabel9.Text = "接头基准宽度(mm)";
            this.uiSymbolLabel9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SeamWidth
            // 
            this.SeamWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.SeamWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.SeamWidth.Location = new System.Drawing.Point(319, 235);
            this.SeamWidth.Margin = new System.Windows.Forms.Padding(0);
            this.SeamWidth.Name = "SeamWidth";
            this.SeamWidth.Size = new System.Drawing.Size(319, 28);
            this.SeamWidth.TabIndex = 13;
            this.SeamWidth.Text = "0";
            // 
            // uiSymbolLabel7
            // 
            this.uiSymbolLabel7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel7.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel7.Location = new System.Drawing.Point(3, 294);
            this.uiSymbolLabel7.Name = "uiSymbolLabel7";
            this.uiSymbolLabel7.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel7.TabIndex = 2;
            this.uiSymbolLabel7.Text = "焊接基准速度(mm/s)";
            this.uiSymbolLabel7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // RobotSpeed
            // 
            this.RobotSpeed.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.RobotSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RobotSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.RobotSpeed.Location = new System.Drawing.Point(319, 291);
            this.RobotSpeed.Margin = new System.Windows.Forms.Padding(0);
            this.RobotSpeed.Name = "RobotSpeed";
            this.RobotSpeed.Size = new System.Drawing.Size(319, 28);
            this.RobotSpeed.TabIndex = 11;
            this.RobotSpeed.Text = "0";
            // 
            // uiSymbolLabel16
            // 
            this.uiSymbolLabel16.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel16.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel16.Location = new System.Drawing.Point(3, 124);
            this.uiSymbolLabel16.Name = "uiSymbolLabel16";
            this.uiSymbolLabel16.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel16.TabIndex = 0;
            this.uiSymbolLabel16.Text = "焊缝类型";
            this.uiSymbolLabel16.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiComboBox1
            // 
            this.uiComboBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiComboBox1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiComboBox1.Items.AddRange(new object[] {
            "None",
            "VType",
            "TType"});
            this.uiComboBox1.Location = new System.Drawing.Point(319, 121);
            this.uiComboBox1.Margin = new System.Windows.Forms.Padding(0);
            this.uiComboBox1.Name = "uiComboBox1";
            this.uiComboBox1.PlaceholderText = "";
            this.uiComboBox1.Size = new System.Drawing.Size(319, 28);
            this.uiComboBox1.TabIndex = 1;
            // 
            // uiSymbolLabel3
            // 
            this.uiSymbolLabel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel3.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel3.Location = new System.Drawing.Point(641, 68);
            this.uiSymbolLabel3.Name = "uiSymbolLabel3";
            this.uiSymbolLabel3.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel3.TabIndex = 0;
            this.uiSymbolLabel3.Text = "板材厚度(mm)";
            this.uiSymbolLabel3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // PlateType
            // 
            this.PlateType.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.PlateType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PlateType.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.PlateType.Location = new System.Drawing.Point(957, 93);
            this.PlateType.Margin = new System.Windows.Forms.Padding(0);
            this.PlateType.Name = "PlateType";
            this.PlateType.PlaceholderText = "";
            this.PlateType.Size = new System.Drawing.Size(321, 28);
            this.PlateType.TabIndex = 2;
            // 
            // uiSymbolLabel4
            // 
            this.uiSymbolLabel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel4.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel4.Location = new System.Drawing.Point(641, 96);
            this.uiSymbolLabel4.Name = "uiSymbolLabel4";
            this.uiSymbolLabel4.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel4.TabIndex = 1;
            this.uiSymbolLabel4.Text = "板材材质";
            this.uiSymbolLabel4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Platethickness
            // 
            this.Platethickness.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.Platethickness.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Platethickness.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Platethickness.Location = new System.Drawing.Point(957, 65);
            this.Platethickness.Margin = new System.Windows.Forms.Padding(0);
            this.Platethickness.Name = "Platethickness";
            this.Platethickness.Size = new System.Drawing.Size(321, 28);
            this.Platethickness.TabIndex = 3;
            this.Platethickness.Text = "0";
            // 
            // lblWireTitle
            // 
            this.gridAdaparam.SetColumnSpan(this.lblWireTitle, 4);
            this.lblWireTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblWireTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lblWireTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblWireTitle.Location = new System.Drawing.Point(3, 38);
            this.lblWireTitle.Name = "lblWireTitle";
            this.lblWireTitle.Size = new System.Drawing.Size(1272, 24);
            this.lblWireTitle.TabIndex = 0;
            this.lblWireTitle.Text = "焊接材质";
            // 
            // WireDiameter
            // 
            this.WireDiameter.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.WireDiameter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.WireDiameter.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.WireDiameter.Location = new System.Drawing.Point(319, 65);
            this.WireDiameter.Margin = new System.Windows.Forms.Padding(0);
            this.WireDiameter.Name = "WireDiameter";
            this.WireDiameter.Size = new System.Drawing.Size(319, 28);
            this.WireDiameter.TabIndex = 3;
            this.WireDiameter.Text = "0";
            // 
            // uiSymbolLabel1
            // 
            this.uiSymbolLabel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel1.Location = new System.Drawing.Point(3, 96);
            this.uiSymbolLabel1.Name = "uiSymbolLabel1";
            this.uiSymbolLabel1.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel1.TabIndex = 0;
            this.uiSymbolLabel1.Text = "焊丝类型";
            this.uiSymbolLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel2
            // 
            this.uiSymbolLabel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel2.Location = new System.Drawing.Point(3, 68);
            this.uiSymbolLabel2.Name = "uiSymbolLabel2";
            this.uiSymbolLabel2.Size = new System.Drawing.Size(313, 22);
            this.uiSymbolLabel2.TabIndex = 1;
            this.uiSymbolLabel2.Text = "焊丝直径(mm)";
            this.uiSymbolLabel2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WireType
            // 
            this.WireType.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.WireType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.WireType.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.WireType.Location = new System.Drawing.Point(319, 93);
            this.WireType.Margin = new System.Windows.Forms.Padding(0);
            this.WireType.Name = "WireType";
            this.WireType.PlaceholderText = "";
            this.WireType.Size = new System.Drawing.Size(319, 28);
            this.WireType.TabIndex = 2;
            // 
            // panelWeldList
            // 
            this.panelWeldList.Back = System.Drawing.Color.White;
            this.panelWeldList.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelWeldList.Controls.Add(this.gridWeldZones);
            this.panelWeldList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelWeldList.Location = new System.Drawing.Point(0, 0);
            this.panelWeldList.Name = "panelWeldList";
            this.panelWeldList.Padding = new System.Windows.Forms.Padding(3);
            this.panelWeldList.Shadow = 5;
            this.panelWeldList.ShadowColor = System.Drawing.Color.Black;
            this.panelWeldList.ShadowOpacity = 0.2F;
            this.panelWeldList.Size = new System.Drawing.Size(1762, 881);
            this.panelWeldList.TabIndex = 0;
            // 
            // gridWeldZones
            // 
            this.gridWeldZones.ColumnCount = 4;
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridWeldZones.Controls.Add(this.panelConfigList, 0, 0);
            this.gridWeldZones.Controls.Add(this.gridPanelRight, 1, 0);
            this.gridWeldZones.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridWeldZones.Location = new System.Drawing.Point(9, 9);
            this.gridWeldZones.Name = "gridWeldZones";
            this.gridWeldZones.RowCount = 2;
            this.gridWeldZones.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.gridWeldZones.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridWeldZones.Size = new System.Drawing.Size(1744, 863);
            this.gridWeldZones.TabIndex = 0;
            // 
            // panelConfigList
            // 
            this.panelConfigList.AutoScroll = true;
            this.panelConfigList.Back = System.Drawing.Color.White;
            this.panelConfigList.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelConfigList.Controls.Add(this.gridConfigList);
            this.panelConfigList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelConfigList.Location = new System.Drawing.Point(3, 3);
            this.panelConfigList.Name = "panelConfigList";
            this.panelConfigList.Padding = new System.Windows.Forms.Padding(3);
            this.panelConfigList.Shadow = 5;
            this.panelConfigList.ShadowColor = System.Drawing.Color.Black;
            this.panelConfigList.ShadowOpacity = 0.2F;
            this.panelConfigList.Size = new System.Drawing.Size(430, 857);
            this.panelConfigList.TabIndex = 1;
            // 
            // gridConfigList
            // 
            this.gridConfigList.ColumnCount = 4;
            this.gridConfigList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridConfigList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridConfigList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridConfigList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridConfigList.Controls.Add(this.lblConfigListTitle, 0, 0);
            this.gridConfigList.Controls.Add(this.tableConfigList, 0, 1);
            this.gridConfigList.Controls.Add(this.btnConfigAddNew, 0, 2);
            this.gridConfigList.Controls.Add(this.btnConfigDelete, 3, 2);
            this.gridConfigList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridConfigList.Location = new System.Drawing.Point(9, 9);
            this.gridConfigList.Name = "gridConfigList";
            this.gridConfigList.RowCount = 4;
            this.gridConfigList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridConfigList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.gridConfigList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridConfigList.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridConfigList.Size = new System.Drawing.Size(412, 839);
            this.gridConfigList.TabIndex = 2;
            // 
            // lblConfigListTitle
            // 
            this.lblConfigListTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridConfigList.SetColumnSpan(this.lblConfigListTitle, 4);
            this.lblConfigListTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblConfigListTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.lblConfigListTitle.Location = new System.Drawing.Point(0, 0);
            this.lblConfigListTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblConfigListTitle.Name = "lblConfigListTitle";
            this.lblConfigListTitle.Size = new System.Drawing.Size(412, 35);
            this.lblConfigListTitle.TabIndex = 0;
            this.lblConfigListTitle.Text = "工艺参数列表";
            this.lblConfigListTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tableConfigList
            // 
            this.gridConfigList.SetColumnSpan(this.tableConfigList, 4);
            this.tableConfigList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableConfigList.Gap = 12;
            this.tableConfigList.Location = new System.Drawing.Point(3, 38);
            this.tableConfigList.Name = "tableConfigList";
            this.tableConfigList.Size = new System.Drawing.Size(406, 768);
            this.tableConfigList.TabIndex = 1;
            // 
            // btnConfigAddNew
            // 
            this.btnConfigAddNew.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConfigAddNew.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConfigAddNew.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnConfigAddNew.Location = new System.Drawing.Point(0, 809);
            this.btnConfigAddNew.Margin = new System.Windows.Forms.Padding(0);
            this.btnConfigAddNew.Name = "btnConfigAddNew";
            this.btnConfigAddNew.Size = new System.Drawing.Size(103, 30);
            this.btnConfigAddNew.TabIndex = 2;
            this.btnConfigAddNew.Text = "新建";
            this.btnConfigAddNew.Click += new System.EventHandler(this.btnConfigAddNew_Click);
            // 
            // btnConfigDelete
            // 
            this.btnConfigDelete.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConfigDelete.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConfigDelete.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnConfigDelete.Location = new System.Drawing.Point(309, 809);
            this.btnConfigDelete.Margin = new System.Windows.Forms.Padding(0);
            this.btnConfigDelete.Name = "btnConfigDelete";
            this.btnConfigDelete.Size = new System.Drawing.Size(103, 30);
            this.btnConfigDelete.TabIndex = 3;
            this.btnConfigDelete.Text = "删除";
            this.btnConfigDelete.Click += new System.EventHandler(this.btnConfigDelete_Click);
            // 
            // gridPanelRight
            // 
            this.gridPanelRight.ColumnCount = 4;
            this.gridWeldZones.SetColumnSpan(this.gridPanelRight, 3);
            this.gridPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanelRight.Controls.Add(this.panelAdaparam, 0, 0);
            this.gridPanelRight.Controls.Add(this.panel2, 0, 1);
            this.gridPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPanelRight.Location = new System.Drawing.Point(439, 3);
            this.gridPanelRight.Name = "gridPanelRight";
            this.gridPanelRight.RowCount = 3;
            this.gridPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 501F));
            this.gridPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.gridPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridPanelRight.Size = new System.Drawing.Size(1302, 857);
            this.gridPanelRight.TabIndex = 1;
            this.gridPanelRight.Text = "gridPanel1";
            // 
            // panelAdaparam
            // 
            this.panelAdaparam.AutoScroll = true;
            this.panelAdaparam.Back = System.Drawing.Color.White;
            this.panelAdaparam.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.gridPanelRight.SetColumnSpan(this.panelAdaparam, 4);
            this.panelAdaparam.Controls.Add(this.gridAdaparam);
            this.panelAdaparam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelAdaparam.Location = new System.Drawing.Point(3, 3);
            this.panelAdaparam.Name = "panelAdaparam";
            this.panelAdaparam.Padding = new System.Windows.Forms.Padding(3);
            this.panelAdaparam.Shadow = 5;
            this.panelAdaparam.ShadowColor = System.Drawing.Color.Black;
            this.panelAdaparam.ShadowOpacity = 0.2F;
            this.panelAdaparam.Size = new System.Drawing.Size(1296, 495);
            this.panelAdaparam.TabIndex = 0;
            // 
            // gridAdaparam
            // 
            this.gridAdaparam.ColumnCount = 4;
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridAdaparam.Controls.Add(this.label1, 0, 0);
            this.gridAdaparam.Controls.Add(this.lblWireTitle, 0, 1);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel2, 0, 2);
            this.gridAdaparam.Controls.Add(this.WireDiameter, 1, 2);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel3, 2, 2);
            this.gridAdaparam.Controls.Add(this.Platethickness, 3, 2);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel1, 0, 3);
            this.gridAdaparam.Controls.Add(this.WireType, 1, 3);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel4, 2, 3);
            this.gridAdaparam.Controls.Add(this.PlateType, 3, 3);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel16, 0, 4);
            this.gridAdaparam.Controls.Add(this.uiComboBox1, 1, 4);
            this.gridAdaparam.Controls.Add(this.lblBaseTitle, 0, 5);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel13, 0, 6);
            this.gridAdaparam.Controls.Add(this.MinSeamWidth, 1, 6);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel11, 2, 6);
            this.gridAdaparam.Controls.Add(this.MaxSeamWidth, 3, 6);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel14, 0, 7);
            this.gridAdaparam.Controls.Add(this.Sensitivity, 1, 7);
            this.gridAdaparam.Controls.Add(this.RobotSpeed, 1, 10);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel7, 0, 10);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel9, 0, 8);
            this.gridAdaparam.Controls.Add(this.SeamWidth, 1, 8);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel8, 2, 9);
            this.gridAdaparam.Controls.Add(this.FeedSpeed, 3, 9);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel6, 2, 8);
            this.gridAdaparam.Controls.Add(this.LaserDiameter, 3, 8);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel5, 0, 9);
            this.gridAdaparam.Controls.Add(this.LaserPower, 1, 9);
            this.gridAdaparam.Controls.Add(this.btnCorBaseline, 3, 11);
            this.gridAdaparam.Controls.Add(this.btnShowAutoParam, 2, 11);
            this.gridAdaparam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridAdaparam.Location = new System.Drawing.Point(9, 9);
            this.gridAdaparam.Name = "gridAdaparam";
            this.gridAdaparam.RowCount = 14;
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridAdaparam.Size = new System.Drawing.Size(1278, 477);
            this.gridAdaparam.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridAdaparam.SetColumnSpan(this.label1, 4);
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(1278, 35);
            this.label1.TabIndex = 18;
            this.label1.Text = "焊接工艺自适应调参模型";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.gridPanelRight.SetColumnSpan(this.panel2, 4);
            this.panel2.Controls.Add(this.gridPanel1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(3, 504);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(3);
            this.panel2.Shadow = 5;
            this.panel2.ShadowColor = System.Drawing.Color.Black;
            this.panel2.ShadowOpacity = 0.2F;
            this.panel2.Size = new System.Drawing.Size(1296, 350);
            this.panel2.TabIndex = 1;
            this.panel2.Text = "panel2";
            // 
            // gridPanel1
            // 
            this.gridPanel1.ColumnCount = 4;
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridPanel1.Controls.Add(this.lblWeldAdjTitle, 0, 0);
            this.gridPanel1.Controls.Add(this.btnReadAutoParam, 1, 3);
            this.gridPanel1.Controls.Add(this.lblFlowCountNum, 0, 1);
            this.gridPanel1.Controls.Add(this.btnCorAutoParam, 2, 3);
            this.gridPanel1.Controls.Add(this.txtFlowCountNum, 1, 1);
            this.gridPanel1.Controls.Add(this.lblFlowMaxConsecFail, 0, 2);
            this.gridPanel1.Controls.Add(this.txtFlowMaxConsecFail, 1, 2);
            this.gridPanel1.Controls.Add(this.btnSaveAutoParam, 3, 3);
            this.gridPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPanel1.Location = new System.Drawing.Point(9, 9);
            this.gridPanel1.Name = "gridPanel1";
            this.gridPanel1.RowCount = 6;
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridPanel1.Size = new System.Drawing.Size(1278, 332);
            this.gridPanel1.TabIndex = 0;
            this.gridPanel1.Text = "gridPanel1";
            // 
            // lblWeldAdjTitle
            // 
            this.lblWeldAdjTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridPanel1.SetColumnSpan(this.lblWeldAdjTitle, 4);
            this.lblWeldAdjTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblWeldAdjTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.lblWeldAdjTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblWeldAdjTitle.Location = new System.Drawing.Point(0, 0);
            this.lblWeldAdjTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblWeldAdjTitle.Name = "lblWeldAdjTitle";
            this.lblWeldAdjTitle.Size = new System.Drawing.Size(1278, 35);
            this.lblWeldAdjTitle.TabIndex = 0;
            this.lblWeldAdjTitle.Text = "报警阈值";
            this.lblWeldAdjTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnReadAutoParam
            // 
            this.btnReadAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnReadAutoParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReadAutoParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnReadAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnReadAutoParam.Ghost = true;
            this.btnReadAutoParam.IconSvg = "ReloadOutlined";
            this.btnReadAutoParam.Location = new System.Drawing.Point(322, 94);
            this.btnReadAutoParam.Name = "btnReadAutoParam";
            this.btnReadAutoParam.Size = new System.Drawing.Size(313, 24);
            this.btnReadAutoParam.TabIndex = 5;
            this.btnReadAutoParam.Text = "读取调整";
            this.btnReadAutoParam.Click += new System.EventHandler(this.BtnReadAutoParam_Click);
            // 
            // lblFlowCountNum
            // 
            this.lblFlowCountNum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFlowCountNum.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblFlowCountNum.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblFlowCountNum.Location = new System.Drawing.Point(0, 35);
            this.lblFlowCountNum.Margin = new System.Windows.Forms.Padding(0);
            this.lblFlowCountNum.Name = "lblFlowCountNum";
            this.lblFlowCountNum.Size = new System.Drawing.Size(319, 28);
            this.lblFlowCountNum.TabIndex = 144;
            this.lblFlowCountNum.Text = "段落长度(mm)";
            this.lblFlowCountNum.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtFlowCountNum
            // 
            this.txtFlowCountNum.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtFlowCountNum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFlowCountNum.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtFlowCountNum.Location = new System.Drawing.Point(319, 35);
            this.txtFlowCountNum.Margin = new System.Windows.Forms.Padding(0);
            this.txtFlowCountNum.Name = "txtFlowCountNum";
            this.txtFlowCountNum.Size = new System.Drawing.Size(319, 28);
            this.txtFlowCountNum.TabIndex = 145;
            this.txtFlowCountNum.Text = "0.000";
            // 
            // lblFlowMaxConsecFail
            // 
            this.lblFlowMaxConsecFail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFlowMaxConsecFail.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblFlowMaxConsecFail.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblFlowMaxConsecFail.Location = new System.Drawing.Point(0, 63);
            this.lblFlowMaxConsecFail.Margin = new System.Windows.Forms.Padding(0);
            this.lblFlowMaxConsecFail.Name = "lblFlowMaxConsecFail";
            this.lblFlowMaxConsecFail.Size = new System.Drawing.Size(319, 28);
            this.lblFlowMaxConsecFail.TabIndex = 143;
            this.lblFlowMaxConsecFail.Text = "失效长度(mm)";
            this.lblFlowMaxConsecFail.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtFlowMaxConsecFail
            // 
            this.txtFlowMaxConsecFail.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtFlowMaxConsecFail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFlowMaxConsecFail.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtFlowMaxConsecFail.Location = new System.Drawing.Point(319, 63);
            this.txtFlowMaxConsecFail.Margin = new System.Windows.Forms.Padding(0);
            this.txtFlowMaxConsecFail.Name = "txtFlowMaxConsecFail";
            this.txtFlowMaxConsecFail.Size = new System.Drawing.Size(319, 28);
            this.txtFlowMaxConsecFail.TabIndex = 143;
            this.txtFlowMaxConsecFail.Text = "0.000";
            // 
            // btnSaveAutoParam
            // 
            this.btnSaveAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSaveAutoParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveAutoParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSaveAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnSaveAutoParam.Ghost = true;
            this.btnSaveAutoParam.IconSvg = "SaveOutlined";
            this.btnSaveAutoParam.Location = new System.Drawing.Point(960, 94);
            this.btnSaveAutoParam.Name = "btnSaveAutoParam";
            this.btnSaveAutoParam.Size = new System.Drawing.Size(315, 24);
            this.btnSaveAutoParam.TabIndex = 4;
            this.btnSaveAutoParam.Text = "保存调整";
            this.btnSaveAutoParam.Click += new System.EventHandler(this.BtnSaveAutoParam_Click);
            // 
            // tabsProcess
            // 
            this.tabsProcess.Controls.Add(this.tabPageAdaptiveSetup);
            this.tabsProcess.Controls.Add(this.tabPageQuickSetup);
            this.tabsProcess.Cursor = System.Windows.Forms.Cursors.Default;
            this.tabsProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsProcess.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.tabsProcess.Location = new System.Drawing.Point(0, 0);
            this.tabsProcess.Name = "tabsProcess";
            this.tabsProcess.Pages.Add(this.tabPageQuickSetup);
            this.tabsProcess.Pages.Add(this.tabPageAdaptiveSetup);
            this.tabsProcess.SelectedIndex = 1;
            this.tabsProcess.Size = new System.Drawing.Size(1762, 920);
            this.tabsProcess.Style = styleCard1;
            this.tabsProcess.TabIndex = 2;
            this.tabsProcess.Text = "tabsProcess";
            this.tabsProcess.Type = AntdUI.TabType.Card;
            // 
            // tabPageAdaptiveSetup
            // 
            this.tabPageAdaptiveSetup.Controls.Add(this.panelWeldList);
            this.tabPageAdaptiveSetup.Location = new System.Drawing.Point(0, 39);
            this.tabPageAdaptiveSetup.Name = "tabPageAdaptiveSetup";
            this.tabPageAdaptiveSetup.Size = new System.Drawing.Size(1762, 881);
            this.tabPageAdaptiveSetup.TabIndex = 0;
            this.tabPageAdaptiveSetup.Text = "自适应工艺参数设置";
            // 
            // tabPageQuickSetup
            // 
            this.tabPageQuickSetup.Controls.Add(this.gridQuickZones);
            this.tabPageQuickSetup.Location = new System.Drawing.Point(-3524, -1762);
            this.tabPageQuickSetup.Name = "tabPageQuickSetup";
            this.tabPageQuickSetup.Size = new System.Drawing.Size(1762, 881);
            this.tabPageQuickSetup.TabIndex = 0;
            this.tabPageQuickSetup.Text = "激光工艺设置";
            // 
            // gridQuickZones
            // 
            this.gridQuickZones.ColumnCount = 4;
            this.gridQuickZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickZones.Controls.Add(this.panelProcessDebug, 0, 0);
            this.gridQuickZones.Controls.Add(this.panelQuickTracking, 2, 0);
            this.gridQuickZones.Controls.Add(this.panelQuickTa, 0, 1);
            this.gridQuickZones.Controls.Add(this.panelQuickLa, 2, 1);
            this.gridQuickZones.Controls.Add(this.flowPanel2, 0, 2);
            this.gridQuickZones.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickZones.Location = new System.Drawing.Point(0, 0);
            this.gridQuickZones.Name = "gridQuickZones";
            this.gridQuickZones.RowCount = 4;
            this.gridQuickZones.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.gridQuickZones.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.gridQuickZones.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.gridQuickZones.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridQuickZones.Size = new System.Drawing.Size(1762, 881);
            this.gridQuickZones.TabIndex = 0;
            // 
            // panelProcessDebug
            // 
            this.panelProcessDebug.Back = System.Drawing.Color.White;
            this.panelProcessDebug.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.gridQuickZones.SetColumnSpan(this.panelProcessDebug, 2);
            this.panelProcessDebug.Controls.Add(this.tlPLASetting);
            this.panelProcessDebug.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelProcessDebug.Location = new System.Drawing.Point(3, 3);
            this.panelProcessDebug.Name = "panelProcessDebug";
            this.panelProcessDebug.Padding = new System.Windows.Forms.Padding(3);
            this.panelProcessDebug.Shadow = 5;
            this.panelProcessDebug.ShadowColor = System.Drawing.Color.Black;
            this.panelProcessDebug.ShadowOpacity = 0.2F;
            this.panelProcessDebug.Size = new System.Drawing.Size(874, 414);
            this.panelProcessDebug.TabIndex = 0;
            // 
            // tlPLASetting
            // 
            this.tlPLASetting.ColumnCount = 4;
            this.tlPLASetting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlPLASetting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlPLASetting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlPLASetting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlPLASetting.Controls.Add(this.label4, 0, 4);
            this.tlPLASetting.Controls.Add(this.label3, 0, 1);
            this.tlPLASetting.Controls.Add(this.radioQuickLaDefaultFocus, 0, 3);
            this.tlPLASetting.Controls.Add(this.radioQuickLaPose1, 3, 2);
            this.tlPLASetting.Controls.Add(this.radioQuickLaAuto, 0, 2);
            this.tlPLASetting.Controls.Add(this.radioQuickLaSpot2, 2, 2);
            this.tlPLASetting.Controls.Add(this.radioQuickLaSpot1, 1, 2);
            this.tlPLASetting.Controls.Add(this.label2, 0, 0);
            this.tlPLASetting.Controls.Add(this.lblLaSpot1, 0, 6);
            this.tlPLASetting.Controls.Add(this.txtLaSpot1, 1, 6);
            this.tlPLASetting.Controls.Add(this.lblLaSpot2, 2, 6);
            this.tlPLASetting.Controls.Add(this.txtLaSpot2, 3, 6);
            this.tlPLASetting.Controls.Add(this.txtLaPose1, 1, 5);
            this.tlPLASetting.Controls.Add(this.lblLaDefaultFocus, 2, 5);
            this.tlPLASetting.Controls.Add(this.lblLaPose1, 0, 5);
            this.tlPLASetting.Controls.Add(this.txtLaDefaultFocus, 3, 5);
            this.tlPLASetting.Controls.Add(this.lblSpotTarget, 0, 7);
            this.tlPLASetting.Controls.Add(this.txtLaSpotTarget, 1, 7);
            this.tlPLASetting.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlPLASetting.Location = new System.Drawing.Point(9, 9);
            this.tlPLASetting.Name = "tlPLASetting";
            this.tlPLASetting.RowCount = 10;
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlPLASetting.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlPLASetting.Size = new System.Drawing.Size(856, 396);
            this.tlPLASetting.TabIndex = 0;
            // 
            // label4
            // 
            this.tlPLASetting.SetColumnSpan(this.label4, 4);
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label4.Location = new System.Drawing.Point(3, 124);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(850, 24);
            this.label4.TabIndex = 16;
            this.label4.Text = "LA 设置";
            // 
            // label3
            // 
            this.tlPLASetting.SetColumnSpan(this.label3, 4);
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label3.Location = new System.Drawing.Point(3, 38);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(850, 24);
            this.label3.TabIndex = 15;
            this.label3.Text = "LA 指令";
            // 
            // radioQuickLaDefaultFocus
            // 
            this.radioQuickLaDefaultFocus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaDefaultFocus.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaDefaultFocus.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaDefaultFocus.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaDefaultFocus.Location = new System.Drawing.Point(0, 93);
            this.radioQuickLaDefaultFocus.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaDefaultFocus.Name = "radioQuickLaDefaultFocus";
            this.radioQuickLaDefaultFocus.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaDefaultFocus.TabIndex = 14;
            this.radioQuickLaDefaultFocus.Text = "LA_DefaultFocus";
            this.radioQuickLaDefaultFocus.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaPose1
            // 
            this.radioQuickLaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaPose1.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaPose1.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaPose1.Location = new System.Drawing.Point(642, 65);
            this.radioQuickLaPose1.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaPose1.Name = "radioQuickLaPose1";
            this.radioQuickLaPose1.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaPose1.TabIndex = 13;
            this.radioQuickLaPose1.Text = "LA_Pose1";
            this.radioQuickLaPose1.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaAuto
            // 
            this.radioQuickLaAuto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaAuto.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaAuto.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaAuto.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaAuto.Location = new System.Drawing.Point(0, 65);
            this.radioQuickLaAuto.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaAuto.Name = "radioQuickLaAuto";
            this.radioQuickLaAuto.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaAuto.TabIndex = 10;
            this.radioQuickLaAuto.Text = "LA_Auto";
            this.radioQuickLaAuto.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaSpot2
            // 
            this.radioQuickLaSpot2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaSpot2.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaSpot2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaSpot2.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaSpot2.Location = new System.Drawing.Point(428, 65);
            this.radioQuickLaSpot2.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaSpot2.Name = "radioQuickLaSpot2";
            this.radioQuickLaSpot2.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaSpot2.TabIndex = 12;
            this.radioQuickLaSpot2.Text = "LA_Spot2";
            this.radioQuickLaSpot2.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaSpot1
            // 
            this.radioQuickLaSpot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaSpot1.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaSpot1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaSpot1.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaSpot1.Location = new System.Drawing.Point(214, 65);
            this.radioQuickLaSpot1.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaSpot1.Name = "radioQuickLaSpot1";
            this.radioQuickLaSpot1.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaSpot1.TabIndex = 11;
            this.radioQuickLaSpot1.Text = "LA_Spot1";
            this.radioQuickLaSpot1.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // label2
            // 
            this.label2.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.tlPLASetting.SetColumnSpan(this.label2, 4);
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.label2.Location = new System.Drawing.Point(0, 0);
            this.label2.Margin = new System.Windows.Forms.Padding(0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(856, 35);
            this.label2.TabIndex = 11;
            this.label2.Text = "LA Setting";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblLaSpot1
            // 
            this.lblLaSpot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaSpot1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaSpot1.Location = new System.Drawing.Point(3, 179);
            this.lblLaSpot1.Name = "lblLaSpot1";
            this.lblLaSpot1.Size = new System.Drawing.Size(208, 28);
            this.lblLaSpot1.TabIndex = 10;
            this.lblLaSpot1.Text = "LA Spot1 光斑(mm)";
            this.lblLaSpot1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtLaSpot1
            // 
            this.txtLaSpot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaSpot1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaSpot1.Location = new System.Drawing.Point(214, 179);
            this.txtLaSpot1.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaSpot1.Name = "txtLaSpot1";
            this.txtLaSpot1.Size = new System.Drawing.Size(214, 28);
            this.txtLaSpot1.TabIndex = 10;
            // 
            // lblLaSpot2
            // 
            this.lblLaSpot2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaSpot2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaSpot2.Location = new System.Drawing.Point(431, 179);
            this.lblLaSpot2.Name = "lblLaSpot2";
            this.lblLaSpot2.Size = new System.Drawing.Size(208, 28);
            this.lblLaSpot2.TabIndex = 10;
            this.lblLaSpot2.Text = "LA Spot2 光斑(mm)";
            this.lblLaSpot2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtLaSpot2
            // 
            this.txtLaSpot2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaSpot2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaSpot2.Location = new System.Drawing.Point(642, 179);
            this.txtLaSpot2.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaSpot2.Name = "txtLaSpot2";
            this.txtLaSpot2.Size = new System.Drawing.Size(214, 28);
            this.txtLaSpot2.TabIndex = 10;
            // 
            // txtLaPose1
            // 
            this.txtLaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaPose1.Location = new System.Drawing.Point(214, 151);
            this.txtLaPose1.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaPose1.Name = "txtLaPose1";
            this.txtLaPose1.Size = new System.Drawing.Size(214, 28);
            this.txtLaPose1.TabIndex = 10;
            // 
            // lblLaDefaultFocus
            // 
            this.lblLaDefaultFocus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaDefaultFocus.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaDefaultFocus.Location = new System.Drawing.Point(431, 151);
            this.lblLaDefaultFocus.Name = "lblLaDefaultFocus";
            this.lblLaDefaultFocus.Size = new System.Drawing.Size(208, 28);
            this.lblLaDefaultFocus.TabIndex = 10;
            this.lblLaDefaultFocus.Text = "LA 默认焦平面(mm)";
            this.lblLaDefaultFocus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblLaPose1
            // 
            this.lblLaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaPose1.Location = new System.Drawing.Point(3, 151);
            this.lblLaPose1.Name = "lblLaPose1";
            this.lblLaPose1.Size = new System.Drawing.Size(208, 28);
            this.lblLaPose1.TabIndex = 10;
            this.lblLaPose1.Text = "LA Pose1 位(mm)";
            this.lblLaPose1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtLaDefaultFocus
            // 
            this.txtLaDefaultFocus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaDefaultFocus.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaDefaultFocus.Location = new System.Drawing.Point(642, 151);
            this.txtLaDefaultFocus.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaDefaultFocus.Name = "txtLaDefaultFocus";
            this.txtLaDefaultFocus.Size = new System.Drawing.Size(214, 28);
            this.txtLaDefaultFocus.TabIndex = 10;
            // 
            // lblSpotTarget
            // 
            this.lblSpotTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSpotTarget.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblSpotTarget.Location = new System.Drawing.Point(3, 207);
            this.lblSpotTarget.Name = "lblSpotTarget";
            this.lblSpotTarget.Size = new System.Drawing.Size(208, 28);
            this.lblSpotTarget.TabIndex = 10;
            this.lblSpotTarget.Text = "Auto目标光斑(mm)";
            this.lblSpotTarget.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtLaSpotTarget
            // 
            this.txtLaSpotTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaSpotTarget.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaSpotTarget.Location = new System.Drawing.Point(214, 207);
            this.txtLaSpotTarget.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaSpotTarget.Name = "txtLaSpotTarget";
            this.txtLaSpotTarget.Size = new System.Drawing.Size(214, 28);
            this.txtLaSpotTarget.TabIndex = 10;
            this.txtLaSpotTarget.Text = "0.6";
            // 
            // panelQuickTracking
            // 
            this.panelQuickTracking.Back = System.Drawing.Color.White;
            this.panelQuickTracking.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.gridQuickZones.SetColumnSpan(this.panelQuickTracking, 2);
            this.panelQuickTracking.Controls.Add(this.gridQuickTracking);
            this.panelQuickTracking.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelQuickTracking.Location = new System.Drawing.Point(883, 3);
            this.panelQuickTracking.Name = "panelQuickTracking";
            this.panelQuickTracking.Padding = new System.Windows.Forms.Padding(3);
            this.panelQuickTracking.Shadow = 5;
            this.panelQuickTracking.ShadowColor = System.Drawing.Color.Black;
            this.panelQuickTracking.ShadowOpacity = 0.2F;
            this.panelQuickTracking.Size = new System.Drawing.Size(876, 414);
            this.panelQuickTracking.TabIndex = 0;
            this.panelQuickTracking.Text = "Tracking Setting";
            // 
            // gridQuickTracking
            // 
            this.gridQuickTracking.ColumnCount = 4;
            this.gridQuickTracking.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTracking.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTracking.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTracking.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTracking.Controls.Add(this.label6, 0, 3);
            this.gridQuickTracking.Controls.Add(this.label5, 0, 1);
            this.gridQuickTracking.Controls.Add(this.lblQuickTracking, 0, 0);
            this.gridQuickTracking.Controls.Add(this.radioQuickYAuto, 0, 2);
            this.gridQuickTracking.Controls.Add(this.radioQuickYForceCalibPose, 1, 2);
            this.gridQuickTracking.Controls.Add(this.radioQuickYForceHome, 2, 2);
            this.gridQuickTracking.Controls.Add(this.lblCalibCenterY, 0, 4);
            this.gridQuickTracking.Controls.Add(this.txtCalibCenterY, 1, 4);
            this.gridQuickTracking.Controls.Add(this.lblCalibCenterZ, 2, 4);
            this.gridQuickTracking.Controls.Add(this.txtCalibCenterZ, 3, 4);
            this.gridQuickTracking.Controls.Add(this.lblTeachLockY, 0, 5);
            this.gridQuickTracking.Controls.Add(this.txtTeachLockY, 1, 5);
            this.gridQuickTracking.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickTracking.Location = new System.Drawing.Point(9, 9);
            this.gridQuickTracking.Name = "gridQuickTracking";
            this.gridQuickTracking.RowCount = 10;
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickTracking.Size = new System.Drawing.Size(858, 396);
            this.gridQuickTracking.TabIndex = 11;
            // 
            // label6
            // 
            this.gridQuickTracking.SetColumnSpan(this.label6, 4);
            this.label6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label6.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label6.Location = new System.Drawing.Point(3, 96);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(852, 24);
            this.label6.TabIndex = 122;
            this.label6.Text = "YT 设置";
            // 
            // label5
            // 
            this.gridQuickTracking.SetColumnSpan(this.label5, 4);
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label5.Location = new System.Drawing.Point(3, 38);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(852, 24);
            this.label5.TabIndex = 121;
            this.label5.Text = "YT 指令";
            // 
            // lblQuickTracking
            // 
            this.lblQuickTracking.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridQuickTracking.SetColumnSpan(this.lblQuickTracking, 4);
            this.lblQuickTracking.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblQuickTracking.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.lblQuickTracking.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblQuickTracking.Location = new System.Drawing.Point(0, 0);
            this.lblQuickTracking.Margin = new System.Windows.Forms.Padding(0);
            this.lblQuickTracking.Name = "lblQuickTracking";
            this.lblQuickTracking.Size = new System.Drawing.Size(858, 35);
            this.lblQuickTracking.TabIndex = 0;
            this.lblQuickTracking.Text = "Y-Tracking Setting";
            this.lblQuickTracking.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioQuickYAuto
            // 
            this.radioQuickYAuto.Checked = true;
            this.radioQuickYAuto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickYAuto.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickYAuto.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickYAuto.ForeColor = System.Drawing.Color.Black;
            this.radioQuickYAuto.Location = new System.Drawing.Point(0, 65);
            this.radioQuickYAuto.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickYAuto.Name = "radioQuickYAuto";
            this.radioQuickYAuto.Size = new System.Drawing.Size(214, 28);
            this.radioQuickYAuto.TabIndex = 10;
            this.radioQuickYAuto.Text = "Y_AutoTracking";
            this.radioQuickYAuto.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickY_CheckedChanged);
            // 
            // radioQuickYForceCalibPose
            // 
            this.radioQuickYForceCalibPose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickYForceCalibPose.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickYForceCalibPose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickYForceCalibPose.ForeColor = System.Drawing.Color.Black;
            this.radioQuickYForceCalibPose.Location = new System.Drawing.Point(214, 65);
            this.radioQuickYForceCalibPose.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickYForceCalibPose.Name = "radioQuickYForceCalibPose";
            this.radioQuickYForceCalibPose.Size = new System.Drawing.Size(214, 28);
            this.radioQuickYForceCalibPose.TabIndex = 11;
            this.radioQuickYForceCalibPose.Text = "Y_ForceCalibPose";
            this.radioQuickYForceCalibPose.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickY_CheckedChanged);
            // 
            // radioQuickYForceHome
            // 
            this.radioQuickYForceHome.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickYForceHome.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickYForceHome.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickYForceHome.ForeColor = System.Drawing.Color.Black;
            this.radioQuickYForceHome.Location = new System.Drawing.Point(428, 65);
            this.radioQuickYForceHome.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickYForceHome.Name = "radioQuickYForceHome";
            this.radioQuickYForceHome.Size = new System.Drawing.Size(214, 28);
            this.radioQuickYForceHome.TabIndex = 12;
            this.radioQuickYForceHome.Text = "Y_ForceHome";
            this.radioQuickYForceHome.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickY_CheckedChanged);
            // 
            // lblCalibCenterY
            // 
            this.lblCalibCenterY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCalibCenterY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblCalibCenterY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCalibCenterY.Location = new System.Drawing.Point(3, 126);
            this.lblCalibCenterY.Name = "lblCalibCenterY";
            this.lblCalibCenterY.Size = new System.Drawing.Size(208, 22);
            this.lblCalibCenterY.TabIndex = 13;
            this.lblCalibCenterY.Text = "标定中心点Y(mm)";
            this.lblCalibCenterY.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtCalibCenterY
            // 
            this.txtCalibCenterY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCalibCenterY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtCalibCenterY.Location = new System.Drawing.Point(214, 123);
            this.txtCalibCenterY.Margin = new System.Windows.Forms.Padding(0);
            this.txtCalibCenterY.Name = "txtCalibCenterY";
            this.txtCalibCenterY.Size = new System.Drawing.Size(214, 28);
            this.txtCalibCenterY.TabIndex = 14;
            this.txtCalibCenterY.Text = "0.000";
            // 
            // lblCalibCenterZ
            // 
            this.lblCalibCenterZ.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCalibCenterZ.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblCalibCenterZ.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCalibCenterZ.Location = new System.Drawing.Point(431, 126);
            this.lblCalibCenterZ.Name = "lblCalibCenterZ";
            this.lblCalibCenterZ.Size = new System.Drawing.Size(208, 22);
            this.lblCalibCenterZ.TabIndex = 15;
            this.lblCalibCenterZ.Text = "标定中心点Z(mm)";
            this.lblCalibCenterZ.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtCalibCenterZ
            // 
            this.txtCalibCenterZ.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCalibCenterZ.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtCalibCenterZ.Location = new System.Drawing.Point(642, 123);
            this.txtCalibCenterZ.Margin = new System.Windows.Forms.Padding(0);
            this.txtCalibCenterZ.Name = "txtCalibCenterZ";
            this.txtCalibCenterZ.Size = new System.Drawing.Size(216, 28);
            this.txtCalibCenterZ.TabIndex = 16;
            this.txtCalibCenterZ.Text = "0.000";
            // 
            // lblTeachLockY
            // 
            this.lblTeachLockY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTeachLockY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTeachLockY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTeachLockY.Location = new System.Drawing.Point(3, 154);
            this.lblTeachLockY.Name = "lblTeachLockY";
            this.lblTeachLockY.Size = new System.Drawing.Size(208, 22);
            this.lblTeachLockY.TabIndex = 17;
            this.lblTeachLockY.Text = "示教Y轴锁定坐标(mm)";
            this.lblTeachLockY.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtTeachLockY
            // 
            this.txtTeachLockY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTeachLockY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTeachLockY.Location = new System.Drawing.Point(214, 151);
            this.txtTeachLockY.Margin = new System.Windows.Forms.Padding(0);
            this.txtTeachLockY.Name = "txtTeachLockY";
            this.txtTeachLockY.Size = new System.Drawing.Size(214, 28);
            this.txtTeachLockY.TabIndex = 18;
            this.txtTeachLockY.Text = "0.000";
            // 
            // panelQuickTa
            // 
            this.panelQuickTa.AutoScroll = true;
            this.panelQuickTa.Back = System.Drawing.Color.White;
            this.panelQuickTa.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.gridQuickZones.SetColumnSpan(this.panelQuickTa, 2);
            this.panelQuickTa.Controls.Add(this.gridQuickTa);
            this.panelQuickTa.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelQuickTa.Location = new System.Drawing.Point(3, 423);
            this.panelQuickTa.Name = "panelQuickTa";
            this.panelQuickTa.Padding = new System.Windows.Forms.Padding(3);
            this.panelQuickTa.Shadow = 5;
            this.panelQuickTa.ShadowColor = System.Drawing.Color.Black;
            this.panelQuickTa.ShadowOpacity = 0.2F;
            this.panelQuickTa.Size = new System.Drawing.Size(874, 414);
            this.panelQuickTa.TabIndex = 10;
            // 
            // gridQuickTa
            // 
            this.gridQuickTa.ColumnCount = 4;
            this.gridQuickTa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickTa.Controls.Add(this.label10, 0, 4);
            this.gridQuickTa.Controls.Add(this.label9, 0, 1);
            this.gridQuickTa.Controls.Add(this.lblQuickTaTitle, 0, 0);
            this.gridQuickTa.Controls.Add(this.radioQuickTaAuto, 0, 2);
            this.gridQuickTa.Controls.Add(this.radioQuickTaRetract, 1, 2);
            this.gridQuickTa.Controls.Add(this.radioQuickTaBreak, 2, 2);
            this.gridQuickTa.Controls.Add(this.radioQuickTaPose1, 3, 2);
            this.gridQuickTa.Controls.Add(this.radioQuickTaPose2, 0, 3);
            this.gridQuickTa.Controls.Add(this.radioQuickTaZeroPose, 1, 3);
            this.gridQuickTa.Controls.Add(this.lblTaForce, 0, 5);
            this.gridQuickTa.Controls.Add(this.txtTaForce, 1, 5);
            this.gridQuickTa.Controls.Add(this.lblTaBreakPos, 2, 5);
            this.gridQuickTa.Controls.Add(this.txtTaBreakPos, 3, 5);
            this.gridQuickTa.Controls.Add(this.lblTaPose1, 0, 6);
            this.gridQuickTa.Controls.Add(this.txtTaPose1, 1, 6);
            this.gridQuickTa.Controls.Add(this.lblTaPose2, 2, 6);
            this.gridQuickTa.Controls.Add(this.txtTaPose2, 3, 6);
            this.gridQuickTa.Controls.Add(this.lblTaZeroPose, 0, 7);
            this.gridQuickTa.Controls.Add(this.txtTaZeroPose, 1, 7);
            this.gridQuickTa.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickTa.Location = new System.Drawing.Point(9, 9);
            this.gridQuickTa.Name = "gridQuickTa";
            this.gridQuickTa.RowCount = 12;
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTa.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridQuickTa.Size = new System.Drawing.Size(856, 396);
            this.gridQuickTa.TabIndex = 11;
            // 
            // label10
            // 
            this.gridQuickTa.SetColumnSpan(this.label10, 4);
            this.label10.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label10.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label10.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label10.Location = new System.Drawing.Point(3, 124);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(850, 24);
            this.label10.TabIndex = 27;
            this.label10.Text = "TA 设置";
            // 
            // label9
            // 
            this.gridQuickTa.SetColumnSpan(this.label9, 4);
            this.label9.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label9.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label9.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label9.Location = new System.Drawing.Point(3, 38);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(850, 24);
            this.label9.TabIndex = 26;
            this.label9.Text = "TA 指令";
            // 
            // lblQuickTaTitle
            // 
            this.lblQuickTaTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridQuickTa.SetColumnSpan(this.lblQuickTaTitle, 4);
            this.lblQuickTaTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblQuickTaTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.lblQuickTaTitle.Location = new System.Drawing.Point(0, 0);
            this.lblQuickTaTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblQuickTaTitle.Name = "lblQuickTaTitle";
            this.lblQuickTaTitle.Size = new System.Drawing.Size(856, 35);
            this.lblQuickTaTitle.TabIndex = 10;
            this.lblQuickTaTitle.Text = "TA Setting";
            this.lblQuickTaTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioQuickTaAuto
            // 
            this.radioQuickTaAuto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickTaAuto.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickTaAuto.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickTaAuto.ForeColor = System.Drawing.Color.Black;
            this.radioQuickTaAuto.Location = new System.Drawing.Point(0, 65);
            this.radioQuickTaAuto.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickTaAuto.Name = "radioQuickTaAuto";
            this.radioQuickTaAuto.Size = new System.Drawing.Size(214, 28);
            this.radioQuickTaAuto.TabIndex = 10;
            this.radioQuickTaAuto.Text = "TA_Auto";
            this.radioQuickTaAuto.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickTa_CheckedChanged);
            // 
            // radioQuickTaRetract
            // 
            this.radioQuickTaRetract.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickTaRetract.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickTaRetract.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickTaRetract.ForeColor = System.Drawing.Color.Black;
            this.radioQuickTaRetract.Location = new System.Drawing.Point(214, 65);
            this.radioQuickTaRetract.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickTaRetract.Name = "radioQuickTaRetract";
            this.radioQuickTaRetract.Size = new System.Drawing.Size(214, 28);
            this.radioQuickTaRetract.TabIndex = 14;
            this.radioQuickTaRetract.Text = "TA_Retract";
            this.radioQuickTaRetract.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickTa_CheckedChanged);
            // 
            // radioQuickTaBreak
            // 
            this.radioQuickTaBreak.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickTaBreak.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickTaBreak.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickTaBreak.ForeColor = System.Drawing.Color.Black;
            this.radioQuickTaBreak.Location = new System.Drawing.Point(428, 65);
            this.radioQuickTaBreak.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickTaBreak.Name = "radioQuickTaBreak";
            this.radioQuickTaBreak.Size = new System.Drawing.Size(214, 28);
            this.radioQuickTaBreak.TabIndex = 11;
            this.radioQuickTaBreak.Text = "TA_Break";
            this.radioQuickTaBreak.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickTa_CheckedChanged);
            // 
            // radioQuickTaPose1
            // 
            this.radioQuickTaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickTaPose1.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickTaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickTaPose1.ForeColor = System.Drawing.Color.Black;
            this.radioQuickTaPose1.Location = new System.Drawing.Point(642, 65);
            this.radioQuickTaPose1.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickTaPose1.Name = "radioQuickTaPose1";
            this.radioQuickTaPose1.Size = new System.Drawing.Size(214, 28);
            this.radioQuickTaPose1.TabIndex = 12;
            this.radioQuickTaPose1.Text = "TA_Pose1";
            this.radioQuickTaPose1.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickTa_CheckedChanged);
            // 
            // radioQuickTaPose2
            // 
            this.radioQuickTaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickTaPose2.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickTaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickTaPose2.ForeColor = System.Drawing.Color.Black;
            this.radioQuickTaPose2.Location = new System.Drawing.Point(0, 93);
            this.radioQuickTaPose2.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickTaPose2.Name = "radioQuickTaPose2";
            this.radioQuickTaPose2.Size = new System.Drawing.Size(214, 28);
            this.radioQuickTaPose2.TabIndex = 13;
            this.radioQuickTaPose2.Text = "TA_Pose2";
            this.radioQuickTaPose2.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickTa_CheckedChanged);
            // 
            // radioQuickTaZeroPose
            // 
            this.radioQuickTaZeroPose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickTaZeroPose.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickTaZeroPose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickTaZeroPose.ForeColor = System.Drawing.Color.Black;
            this.radioQuickTaZeroPose.Location = new System.Drawing.Point(214, 93);
            this.radioQuickTaZeroPose.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickTaZeroPose.Name = "radioQuickTaZeroPose";
            this.radioQuickTaZeroPose.Size = new System.Drawing.Size(214, 28);
            this.radioQuickTaZeroPose.TabIndex = 15;
            this.radioQuickTaZeroPose.Text = "TA_ZeroPose";
            this.radioQuickTaZeroPose.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickTa_CheckedChanged);
            // 
            // lblTaForce
            // 
            this.lblTaForce.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaForce.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaForce.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaForce.Location = new System.Drawing.Point(3, 154);
            this.lblTaForce.Name = "lblTaForce";
            this.lblTaForce.Size = new System.Drawing.Size(208, 22);
            this.lblTaForce.TabIndex = 11;
            this.lblTaForce.Text = "TA_Force 下压力大小(N)：";
            // 
            // txtTaForce
            // 
            this.txtTaForce.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaForce.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaForce.Location = new System.Drawing.Point(214, 151);
            this.txtTaForce.Margin = new System.Windows.Forms.Padding(0);
            this.txtTaForce.Name = "txtTaForce";
            this.txtTaForce.Size = new System.Drawing.Size(214, 28);
            this.txtTaForce.TabIndex = 12;
            this.txtTaForce.Text = "0";
            // 
            // lblTaBreakPos
            // 
            this.lblTaBreakPos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaBreakPos.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaBreakPos.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaBreakPos.Location = new System.Drawing.Point(431, 154);
            this.lblTaBreakPos.Name = "lblTaBreakPos";
            this.lblTaBreakPos.Size = new System.Drawing.Size(208, 22);
            this.lblTaBreakPos.TabIndex = 14;
            this.lblTaBreakPos.Text = "TA_Break 停止位：";
            // 
            // txtTaBreakPos
            // 
            this.txtTaBreakPos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaBreakPos.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaBreakPos.Location = new System.Drawing.Point(642, 151);
            this.txtTaBreakPos.Margin = new System.Windows.Forms.Padding(0);
            this.txtTaBreakPos.Name = "txtTaBreakPos";
            this.txtTaBreakPos.ReadOnly = true;
            this.txtTaBreakPos.Size = new System.Drawing.Size(214, 28);
            this.txtTaBreakPos.TabIndex = 15;
            this.txtTaBreakPos.Text = "--";
            // 
            // lblTaPose1
            // 
            this.lblTaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaPose1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaPose1.Location = new System.Drawing.Point(3, 182);
            this.lblTaPose1.Name = "lblTaPose1";
            this.lblTaPose1.Size = new System.Drawing.Size(208, 22);
            this.lblTaPose1.TabIndex = 17;
            this.lblTaPose1.Text = "TA_Pose1：";
            // 
            // txtTaPose1
            // 
            this.txtTaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaPose1.Location = new System.Drawing.Point(214, 179);
            this.txtTaPose1.Margin = new System.Windows.Forms.Padding(0);
            this.txtTaPose1.Name = "txtTaPose1";
            this.txtTaPose1.Size = new System.Drawing.Size(214, 28);
            this.txtTaPose1.TabIndex = 18;
            this.txtTaPose1.Text = "0";
            // 
            // lblTaPose2
            // 
            this.lblTaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaPose2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaPose2.Location = new System.Drawing.Point(431, 182);
            this.lblTaPose2.Name = "lblTaPose2";
            this.lblTaPose2.Size = new System.Drawing.Size(208, 22);
            this.lblTaPose2.TabIndex = 20;
            this.lblTaPose2.Text = "TA_Pose2：";
            // 
            // txtTaPose2
            // 
            this.txtTaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaPose2.Location = new System.Drawing.Point(642, 179);
            this.txtTaPose2.Margin = new System.Windows.Forms.Padding(0);
            this.txtTaPose2.Name = "txtTaPose2";
            this.txtTaPose2.Size = new System.Drawing.Size(214, 28);
            this.txtTaPose2.TabIndex = 21;
            this.txtTaPose2.Text = "0";
            // 
            // lblTaZeroPose
            // 
            this.lblTaZeroPose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaZeroPose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaZeroPose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaZeroPose.Location = new System.Drawing.Point(3, 210);
            this.lblTaZeroPose.Name = "lblTaZeroPose";
            this.lblTaZeroPose.Size = new System.Drawing.Size(208, 22);
            this.lblTaZeroPose.TabIndex = 24;
            this.lblTaZeroPose.Text = "TA_ZeroPose：";
            // 
            // txtTaZeroPose
            // 
            this.txtTaZeroPose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaZeroPose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaZeroPose.Location = new System.Drawing.Point(214, 207);
            this.txtTaZeroPose.Margin = new System.Windows.Forms.Padding(0);
            this.txtTaZeroPose.Name = "txtTaZeroPose";
            this.txtTaZeroPose.Size = new System.Drawing.Size(214, 28);
            this.txtTaZeroPose.TabIndex = 25;
            this.txtTaZeroPose.Text = "0";
            // 
            // panelQuickLa
            // 
            this.panelQuickLa.Back = System.Drawing.Color.White;
            this.panelQuickLa.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.gridQuickZones.SetColumnSpan(this.panelQuickLa, 2);
            this.panelQuickLa.Controls.Add(this.gridQuickLa);
            this.panelQuickLa.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelQuickLa.Location = new System.Drawing.Point(883, 423);
            this.panelQuickLa.Name = "panelQuickLa";
            this.panelQuickLa.Padding = new System.Windows.Forms.Padding(3);
            this.panelQuickLa.Shadow = 5;
            this.panelQuickLa.ShadowColor = System.Drawing.Color.Black;
            this.panelQuickLa.ShadowOpacity = 0.2F;
            this.panelQuickLa.Size = new System.Drawing.Size(876, 414);
            this.panelQuickLa.TabIndex = 10;
            // 
            // gridQuickLa
            // 
            this.gridQuickLa.ColumnCount = 4;
            this.gridQuickLa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickLa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickLa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickLa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.gridQuickLa.Controls.Add(this.label8, 0, 3);
            this.gridQuickLa.Controls.Add(this.label7, 0, 1);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoArc, 3, 2);
            this.gridQuickLa.Controls.Add(this.lblQuickLaTitle, 0, 0);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoLine, 2, 2);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoEight, 1, 2);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoCircle, 0, 2);
            this.gridQuickLa.Controls.Add(this.lblGalvoLength, 0, 4);
            this.gridQuickLa.Controls.Add(this.txtGalvoLength, 1, 4);
            this.gridQuickLa.Controls.Add(this.lblGalvoWidth, 2, 4);
            this.gridQuickLa.Controls.Add(this.txtGalvoWidth, 3, 4);
            this.gridQuickLa.Controls.Add(this.lblGalvoFreq, 0, 5);
            this.gridQuickLa.Controls.Add(this.txtGalvoFreq, 1, 5);
            this.gridQuickLa.Controls.Add(this.btnGalvoManualStart, 0, 6);
            this.gridQuickLa.Controls.Add(this.btnGalvoManualStop, 1, 6);
            this.gridQuickLa.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickLa.Location = new System.Drawing.Point(9, 9);
            this.gridQuickLa.Name = "gridQuickLa";
            this.gridQuickLa.RowCount = 11;
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridQuickLa.Size = new System.Drawing.Size(858, 396);
            this.gridQuickLa.TabIndex = 11;
            // 
            // radioQuickLaGalvoArc
            // 
            this.radioQuickLaGalvoArc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaGalvoArc.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaGalvoArc.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaGalvoArc.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaGalvoArc.Location = new System.Drawing.Point(642, 65);
            this.radioQuickLaGalvoArc.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaGalvoArc.Name = "radioQuickLaGalvoArc";
            this.radioQuickLaGalvoArc.Size = new System.Drawing.Size(216, 28);
            this.radioQuickLaGalvoArc.TabIndex = 23;
            this.radioQuickLaGalvoArc.Text = "GA_Arc";
            this.radioQuickLaGalvoArc.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // lblQuickLaTitle
            // 
            this.lblQuickLaTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridQuickLa.SetColumnSpan(this.lblQuickLaTitle, 4);
            this.lblQuickLaTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblQuickLaTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.lblQuickLaTitle.Location = new System.Drawing.Point(0, 0);
            this.lblQuickLaTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblQuickLaTitle.Name = "lblQuickLaTitle";
            this.lblQuickLaTitle.Size = new System.Drawing.Size(858, 35);
            this.lblQuickLaTitle.TabIndex = 10;
            this.lblQuickLaTitle.Text = "LA/GA Setting";
            this.lblQuickLaTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioQuickLaGalvoLine
            // 
            this.radioQuickLaGalvoLine.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaGalvoLine.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaGalvoLine.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaGalvoLine.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaGalvoLine.Location = new System.Drawing.Point(428, 65);
            this.radioQuickLaGalvoLine.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaGalvoLine.Name = "radioQuickLaGalvoLine";
            this.radioQuickLaGalvoLine.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaGalvoLine.TabIndex = 22;
            this.radioQuickLaGalvoLine.Text = "GA_Line";
            this.radioQuickLaGalvoLine.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaGalvoEight
            // 
            this.radioQuickLaGalvoEight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaGalvoEight.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaGalvoEight.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaGalvoEight.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaGalvoEight.Location = new System.Drawing.Point(214, 65);
            this.radioQuickLaGalvoEight.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaGalvoEight.Name = "radioQuickLaGalvoEight";
            this.radioQuickLaGalvoEight.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaGalvoEight.TabIndex = 21;
            this.radioQuickLaGalvoEight.Text = "GA_Eight";
            this.radioQuickLaGalvoEight.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaGalvoCircle
            // 
            this.radioQuickLaGalvoCircle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaGalvoCircle.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaGalvoCircle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaGalvoCircle.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaGalvoCircle.Location = new System.Drawing.Point(0, 65);
            this.radioQuickLaGalvoCircle.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaGalvoCircle.Name = "radioQuickLaGalvoCircle";
            this.radioQuickLaGalvoCircle.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaGalvoCircle.TabIndex = 20;
            this.radioQuickLaGalvoCircle.Text = "GA_Circle";
            this.radioQuickLaGalvoCircle.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // lblGalvoLength
            // 
            this.lblGalvoLength.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoLength.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoLength.Location = new System.Drawing.Point(0, 123);
            this.lblGalvoLength.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoLength.Name = "lblGalvoLength";
            this.lblGalvoLength.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoLength.TabIndex = 10;
            this.lblGalvoLength.Text = "长度(mm)";
            this.lblGalvoLength.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGalvoLength
            // 
            this.txtGalvoLength.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGalvoLength.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGalvoLength.Location = new System.Drawing.Point(214, 123);
            this.txtGalvoLength.Margin = new System.Windows.Forms.Padding(0);
            this.txtGalvoLength.Name = "txtGalvoLength";
            this.txtGalvoLength.Size = new System.Drawing.Size(214, 28);
            this.txtGalvoLength.TabIndex = 10;
            this.txtGalvoLength.Text = "1.000";
            // 
            // lblGalvoWidth
            // 
            this.lblGalvoWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoWidth.Location = new System.Drawing.Point(428, 123);
            this.lblGalvoWidth.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoWidth.Name = "lblGalvoWidth";
            this.lblGalvoWidth.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoWidth.TabIndex = 10;
            this.lblGalvoWidth.Text = "宽度(mm)";
            this.lblGalvoWidth.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGalvoWidth
            // 
            this.txtGalvoWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGalvoWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGalvoWidth.Location = new System.Drawing.Point(642, 123);
            this.txtGalvoWidth.Margin = new System.Windows.Forms.Padding(0);
            this.txtGalvoWidth.Name = "txtGalvoWidth";
            this.txtGalvoWidth.Size = new System.Drawing.Size(216, 28);
            this.txtGalvoWidth.TabIndex = 10;
            this.txtGalvoWidth.Text = "1.000";
            // 
            // lblGalvoFreq
            // 
            this.lblGalvoFreq.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoFreq.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoFreq.Location = new System.Drawing.Point(0, 151);
            this.lblGalvoFreq.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoFreq.Name = "lblGalvoFreq";
            this.lblGalvoFreq.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoFreq.TabIndex = 10;
            this.lblGalvoFreq.Text = "频率(Hz)";
            this.lblGalvoFreq.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGalvoFreq
            // 
            this.txtGalvoFreq.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGalvoFreq.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGalvoFreq.Location = new System.Drawing.Point(214, 151);
            this.txtGalvoFreq.Margin = new System.Windows.Forms.Padding(0);
            this.txtGalvoFreq.Name = "txtGalvoFreq";
            this.txtGalvoFreq.Size = new System.Drawing.Size(214, 28);
            this.txtGalvoFreq.TabIndex = 10;
            this.txtGalvoFreq.Text = "50";
            // 
            // flowPanel2
            // 
            this.flowPanel2.Align = AntdUI.TAlignFlow.RightCenter;
            this.gridQuickZones.SetColumnSpan(this.flowPanel2, 4);
            this.flowPanel2.Controls.Add(this.btnReadWeldPonts);
            this.flowPanel2.Controls.Add(this.btnApplyLaserCalib);
            this.flowPanel2.Controls.Add(this.btnSaveWeldPonts);
            this.flowPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowPanel2.Location = new System.Drawing.Point(3, 843);
            this.flowPanel2.Name = "flowPanel2";
            this.flowPanel2.Size = new System.Drawing.Size(1756, 34);
            this.flowPanel2.TabIndex = 13;
            this.flowPanel2.Text = "flowPanel2";
            // 
            // btnReadWeldPonts
            // 
            this.btnReadWeldPonts.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnReadWeldPonts.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReadWeldPonts.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadWeldPonts.Ghost = true;
            this.btnReadWeldPonts.Location = new System.Drawing.Point(1559, 3);
            this.btnReadWeldPonts.Name = "btnReadWeldPonts";
            this.btnReadWeldPonts.Size = new System.Drawing.Size(194, 32);
            this.btnReadWeldPonts.TabIndex = 140;
            this.btnReadWeldPonts.Text = "读取参数";
            this.btnReadWeldPonts.Click += new System.EventHandler(this.BtnReadLaserZone_Click);
            // 
            // btnApplyLaserCalib
            // 
            this.btnApplyLaserCalib.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnApplyLaserCalib.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnApplyLaserCalib.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnApplyLaserCalib.Ghost = true;
            this.btnApplyLaserCalib.IconSvg = "EditOutlined";
            this.btnApplyLaserCalib.Location = new System.Drawing.Point(1453, 3);
            this.btnApplyLaserCalib.Name = "btnApplyLaserCalib";
            this.btnApplyLaserCalib.Size = new System.Drawing.Size(100, 32);
            this.btnApplyLaserCalib.TabIndex = 155;
            this.btnApplyLaserCalib.Text = "修改参数";
            this.btnApplyLaserCalib.Click += new System.EventHandler(this.BtnModifyLaserZone_Click);
            // 
            // btnSaveWeldPonts
            // 
            this.btnSaveWeldPonts.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSaveWeldPonts.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveWeldPonts.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSaveWeldPonts.Ghost = true;
            this.btnSaveWeldPonts.Location = new System.Drawing.Point(1253, 3);
            this.btnSaveWeldPonts.Name = "btnSaveWeldPonts";
            this.btnSaveWeldPonts.Size = new System.Drawing.Size(194, 32);
            this.btnSaveWeldPonts.TabIndex = 141;
            this.btnSaveWeldPonts.Text = "保存参数";
            this.btnSaveWeldPonts.Click += new System.EventHandler(this.BtnSaveLaserZone_Click);
            // 
            // ProcessControlPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tabsProcess);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Name = "ProcessControlPage";
            this.Size = new System.Drawing.Size(1762, 920);
            this.Load += new System.EventHandler(this.ParamAutoPage_Shown);
            this.panelWeldList.ResumeLayout(false);
            this.gridWeldZones.ResumeLayout(false);
            this.panelConfigList.ResumeLayout(false);
            this.gridConfigList.ResumeLayout(false);
            this.gridPanelRight.ResumeLayout(false);
            this.panelAdaparam.ResumeLayout(false);
            this.gridAdaparam.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.gridPanel1.ResumeLayout(false);
            this.tabsProcess.ResumeLayout(false);
            this.tabPageAdaptiveSetup.ResumeLayout(false);
            this.tabPageQuickSetup.ResumeLayout(false);
            this.gridQuickZones.ResumeLayout(false);
            this.panelProcessDebug.ResumeLayout(false);
            this.tlPLASetting.ResumeLayout(false);
            this.panelQuickTracking.ResumeLayout(false);
            this.gridQuickTracking.ResumeLayout(false);
            this.panelQuickTa.ResumeLayout(false);
            this.gridQuickTa.ResumeLayout(false);
            this.panelQuickLa.ResumeLayout(false);
            this.gridQuickLa.ResumeLayout(false);
            this.flowPanel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Panel panelWeldList;
        private AntdUI.Label lblWireTitle;
        private AntdUI.Label uiSymbolLabel1;
        private AntdUI.Label uiSymbolLabel2;
        private AntdUI.Input WireType;
        private AntdUI.Input WireDiameter;
        private AntdUI.Label uiSymbolLabel3;
        private AntdUI.Label uiSymbolLabel4;
        private AntdUI.Input PlateType;
        private AntdUI.Input Platethickness;
        private AntdUI.Label uiSymbolLabel16;
        private AntdUI.Select uiComboBox1;
        private AntdUI.Button btnCorAutoParam;
        private AntdUI.Button btnCorBaseline;
        private AntdUI.Button btnShowAutoParam;
        private AntdUI.Label uiSymbolLabel5;
        private AntdUI.Label uiSymbolLabel6;
        private AntdUI.Label uiSymbolLabel7;
        private AntdUI.Label uiSymbolLabel8;
        private AntdUI.Label uiSymbolLabel9;
        private AntdUI.Label uiSymbolLabel11;
        private AntdUI.Label uiSymbolLabel13;
        private AntdUI.Label uiSymbolLabel14;
        private AntdUI.Input LaserDiameter;
        private AntdUI.Input LaserPower;
        private AntdUI.Input RobotSpeed;
        private AntdUI.Input FeedSpeed;
        private AntdUI.Input SeamWidth;
        private AntdUI.Input MaxSeamWidth;
        private AntdUI.Input MinSeamWidth;
        private AntdUI.Input Sensitivity;
        private System.Windows.Forms.TableLayoutPanel gridWeldZones;
        private AntdUI.Panel panelConfigList;
        private System.Windows.Forms.TableLayoutPanel gridConfigList;
        private AntdUI.Panel panelAdaparam;
        private AntdUI.Table tableConfigList;
        private AntdUI.Button btnConfigDelete;
        private AntdUI.Button btnConfigAddNew;
        private System.Windows.Forms.TableLayoutPanel gridAdaparam;
        private AntdUI.Label lblBaseTitle;
        private AntdUI.Tabs tabsProcess;
        private AntdUI.TabPage tabPageAdaptiveSetup;
        private AntdUI.Label label1;
        private AntdUI.Button btnSaveAutoParam;
        private AntdUI.Button btnReadAutoParam;
        private AntdUI.Button btnSaveWeldPonts;
        private AntdUI.Button btnReadWeldPonts;
        private AntdUI.Button btnApplyLaserCalib;

        private AntdUI.Label lblFlowMaxConsecFail;
        private AntdUI.Input txtFlowMaxConsecFail;
        private AntdUI.Label lblFlowCountNum;
        private AntdUI.Input txtFlowCountNum;
        private AntdUI.Label lblWeldAdjTitle;
        private AntdUI.TabPage tabPageQuickSetup;
        private System.Windows.Forms.TableLayoutPanel gridQuickZones;
        private AntdUI.Panel panelQuickTracking;
        private AntdUI.Label lblQuickTracking;
        private AntdUI.Panel panelProcessDebug;
        private System.Windows.Forms.TableLayoutPanel gridQuickTa;
        private System.Windows.Forms.TableLayoutPanel gridQuickLa;
        private System.Windows.Forms.TableLayoutPanel gridQuickTracking;
        private AntdUI.Radio radioQuickYAuto;
        private AntdUI.Radio radioQuickYForceCalibPose;
        private AntdUI.Radio radioQuickYForceHome;
        private AntdUI.Label lblCalibCenterY;
        private AntdUI.Input txtCalibCenterY;
        private AntdUI.Label lblCalibCenterZ;
        private AntdUI.Input txtCalibCenterZ;
        private AntdUI.Label lblTeachLockY;
        private AntdUI.Input txtTeachLockY;
        private AntdUI.Radio radioQuickTaAuto;
        private AntdUI.Radio radioQuickTaBreak;
        private AntdUI.Radio radioQuickTaPose1;
        private AntdUI.Radio radioQuickTaPose2;
        private AntdUI.Radio radioQuickTaRetract;
        private AntdUI.Radio radioQuickTaZeroPose;
        private AntdUI.Label lblTaForce;
        private AntdUI.Input txtTaForce;
        private AntdUI.Label lblTaBreakPos;
        private AntdUI.Input txtTaBreakPos;
        private AntdUI.Label lblTaPose1;
        private AntdUI.Input txtTaPose1;
        private AntdUI.Label lblTaPose2;
        private AntdUI.Input txtTaPose2;
        private AntdUI.Label lblTaZeroPose;
        private AntdUI.Input txtTaZeroPose;
        private System.Windows.Forms.Label lblQuickTaTitle;
        private AntdUI.Panel panelQuickTa;
        private AntdUI.Radio radioQuickLaAuto;
        private AntdUI.Radio radioQuickLaGalvoCircle;
        private AntdUI.Radio radioQuickLaGalvoEight;
        private AntdUI.Radio radioQuickLaGalvoLine;
        private AntdUI.Radio radioQuickLaGalvoArc;
        private AntdUI.Radio radioQuickLaSpot1;
        private AntdUI.Radio radioQuickLaSpot2;
        private AntdUI.Radio radioQuickLaPose1;
        private AntdUI.Radio radioQuickLaDefaultFocus;
        private System.Windows.Forms.Label lblQuickLaTitle;
        private AntdUI.Panel panelQuickLa;
        private System.Windows.Forms.Label lblLaSpot1;
        private AntdUI.Input txtLaSpot1;
        private System.Windows.Forms.Label lblLaSpot2;
        private AntdUI.Input txtLaSpot2;
        private System.Windows.Forms.Label lblLaPose1;
        private AntdUI.Input txtLaPose1;
        private System.Windows.Forms.Label lblLaDefaultFocus;
        private AntdUI.Input txtLaDefaultFocus;
        private System.Windows.Forms.Label lblSpotTarget;
        private AntdUI.Input txtLaSpotTarget;
        private System.Windows.Forms.Label lblGalvoWidth;
        private AntdUI.Input txtGalvoWidth;
        private System.Windows.Forms.Label lblGalvoLength;
        private AntdUI.Input txtGalvoLength;
        private System.Windows.Forms.Label lblGalvoFreq;
        private AntdUI.Input txtGalvoFreq;
        private AntdUI.Label lblConfigListTitle;
        private AntdUI.FlowPanel flowPanel2;
        private System.Windows.Forms.TableLayoutPanel gridPanelRight;
        private AntdUI.Panel panel2;
        private System.Windows.Forms.TableLayoutPanel gridPanel1;
        private System.Windows.Forms.TableLayoutPanel tlPLASetting;
        private System.Windows.Forms.Label label2;
        private AntdUI.Label label3;
        private AntdUI.Label label8;
        private AntdUI.Button btnGalvoManualStart;
        private AntdUI.Button btnGalvoManualStop;
        private AntdUI.Label label7;
        private System.Windows.Forms.Timer timerRefresh;
        private AntdUI.Label label4;
        private AntdUI.Label label6;
        private AntdUI.Label label5;
        private AntdUI.Label label10;
        private AntdUI.Label label9;
    }
}
