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
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            AntdUI.Tabs.StyleCard styleCard1 = new AntdUI.Tabs.StyleCard();
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
            this.lblNoValueAlarmHint = new AntdUI.Label();
            this.lblFlowCountNum = new AntdUI.Label();
            this.txtFlowCountNum = new AntdUI.Input();
            this.lblFlowMaxConsecFail = new AntdUI.Label();
            this.txtFlowMaxConsecFail = new AntdUI.Input();
            this.lblParamOutputHz = new AntdUI.Label();
            this.selParamOutputHz = new AntdUI.Select();
            this.lblParamSpotGain = new AntdUI.Label();
            this.txtParamSpotGain = new AntdUI.Input();
            this.lblParamManualWidth = new AntdUI.Label();
            this.txtParamManualWidth = new AntdUI.Input();
            this.flowPanel1 = new AntdUI.FlowPanel();
            this.btnReadAutoParam = new AntdUI.Button();
            this.btnSaveAutoParam = new AntdUI.Button();
            this.tabsProcess = new AntdUI.Tabs();
            this.tabPageQuickSetup = new AntdUI.TabPage();
            this.gridQuickZones = new System.Windows.Forms.TableLayoutPanel();
            this.panelProcessDebug = new AntdUI.Panel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.radioQuickLaPose2 = new AntdUI.Radio();
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
            this.lblLaPose2 = new System.Windows.Forms.Label();
            this.lblLaPose1 = new System.Windows.Forms.Label();
            this.txtLaPose2 = new AntdUI.Input();
            this.lblSpotTarget = new System.Windows.Forms.Label();
            this.txtLaSpotTarget = new AntdUI.Input();
            this.panelQuickTracking = new AntdUI.Panel();
            this.gridQuickTracking = new System.Windows.Forms.TableLayoutPanel();
            this.lblQuickTracking = new AntdUI.Label();
            this.radioQuickYAuto = new AntdUI.Radio();
            this.radioQuickYForceCalibPose = new AntdUI.Radio();
            this.radioQuickYForceHome = new AntdUI.Radio();
            this.lblGeoAxisPerLaser = new AntdUI.Label();
            this.txtGeoAxisPerLaser = new AntdUI.Input();
            this.lblGeoGuideSign = new AntdUI.Label();
            this.selGeoGuideSign = new AntdUI.Select();
            this.lblGeoLeadDistance = new AntdUI.Label();
            this.txtGeoLeadDistance = new AntdUI.Input();
            this.lblGeoRobotYGain = new AntdUI.Label();
            this.txtGeoRobotYGain = new AntdUI.Input();
            this.lblGeoYAxisMin = new AntdUI.Label();
            this.txtGeoYAxisMin = new AntdUI.Input();
            this.lblGeoYAxisMax = new AntdUI.Label();
            this.txtGeoYAxisMax = new AntdUI.Input();
            this.lblTeachLockY = new AntdUI.Label();
            this.txtTeachLockY = new AntdUI.Input();
            this.lblCalibCenterY = new AntdUI.Label();
            this.txtCalibCenterY = new AntdUI.Input();
            this.lblCalibCenterZ = new AntdUI.Label();
            this.txtCalibCenterZ = new AntdUI.Input();
            this.panelQuickTa = new AntdUI.Panel();
            this.gridQuickTa = new System.Windows.Forms.TableLayoutPanel();
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
            this.selGalvoPattern = new AntdUI.Select();
            this.lblGalvoPattern = new System.Windows.Forms.Label();
            this.lblGalvoLength = new System.Windows.Forms.Label();
            this.txtGalvoLength = new AntdUI.Input();
            this.lblGalvoWidth = new System.Windows.Forms.Label();
            this.txtGalvoWidth = new AntdUI.Input();
            this.lblGalvoFreq = new System.Windows.Forms.Label();
            this.txtGalvoFreq = new AntdUI.Input();
            this.lblManualWidth = new System.Windows.Forms.Label();
            this.txtManualWidth = new AntdUI.Input();
            this.btnGalvoDemo = new AntdUI.Button();
            this.btnGalvoHome = new AntdUI.Button();
            this.flowPanel2 = new AntdUI.FlowPanel();
            this.btnReadWeldPonts = new AntdUI.Button();
            this.btnApplyLaserCalib = new AntdUI.Button();
            this.btnSaveWeldPonts = new AntdUI.Button();
            this.tabPageAdaptiveSetup = new AntdUI.TabPage();
            this.label3 = new AntdUI.Label();
            this.panelWeldList.SuspendLayout();
            this.gridWeldZones.SuspendLayout();
            this.panelConfigList.SuspendLayout();
            this.gridConfigList.SuspendLayout();
            this.gridPanelRight.SuspendLayout();
            this.panelAdaparam.SuspendLayout();
            this.gridAdaparam.SuspendLayout();
            this.panel2.SuspendLayout();
            this.gridPanel1.SuspendLayout();
            this.flowPanel1.SuspendLayout();
            this.tabsProcess.SuspendLayout();
            this.tabPageQuickSetup.SuspendLayout();
            this.gridQuickZones.SuspendLayout();
            this.panelProcessDebug.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.panelQuickTracking.SuspendLayout();
            this.gridQuickTracking.SuspendLayout();
            this.panelQuickTa.SuspendLayout();
            this.gridQuickTa.SuspendLayout();
            this.panelQuickLa.SuspendLayout();
            this.gridQuickLa.SuspendLayout();
            this.flowPanel2.SuspendLayout();
            this.tabPageAdaptiveSetup.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCorAutoParam
            // 
            this.btnCorAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnCorAutoParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCorAutoParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCorAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCorAutoParam.Ghost = true;
            this.btnCorAutoParam.IconSvg = "EditOutlined";
            this.btnCorAutoParam.Location = new System.Drawing.Point(331, 3);
            this.btnCorAutoParam.Name = "btnCorAutoParam";
            this.btnCorAutoParam.Size = new System.Drawing.Size(100, 32);
            this.btnCorAutoParam.TabIndex = 0;
            this.btnCorAutoParam.Text = "修改参数";
            this.btnCorAutoParam.Click += new System.EventHandler(this.btnCorAutoParam_Click);
            // 
            // btnCorBaseline
            // 
            this.btnCorBaseline.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnCorBaseline.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCorBaseline.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnCorBaseline.Ghost = true;
            this.btnCorBaseline.IconSvg = "EditOutlined";
            this.btnCorBaseline.Location = new System.Drawing.Point(160, 319);
            this.btnCorBaseline.Margin = new System.Windows.Forms.Padding(0);
            this.btnCorBaseline.Name = "btnCorBaseline";
            this.btnCorBaseline.Size = new System.Drawing.Size(160, 28);
            this.btnCorBaseline.TabIndex = 2;
            this.btnCorBaseline.Text = "修正基准";
            this.btnCorBaseline.Click += new System.EventHandler(this.btnCorBaseline_Click);
            // 
            // btnShowAutoParam
            // 
            this.btnShowAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnShowAutoParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnShowAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnShowAutoParam.Ghost = true;
            this.btnShowAutoParam.IconSvg = "TableOutlined";
            this.btnShowAutoParam.Location = new System.Drawing.Point(0, 319);
            this.btnShowAutoParam.Margin = new System.Windows.Forms.Padding(0);
            this.btnShowAutoParam.Name = "btnShowAutoParam";
            this.btnShowAutoParam.Size = new System.Drawing.Size(160, 28);
            this.btnShowAutoParam.TabIndex = 1;
            this.btnShowAutoParam.Text = "自适应参数表";
            this.btnShowAutoParam.Click += new System.EventHandler(this.btnShowAutoParam_Click);
            // 
            // Sensitivity
            // 
            this.Sensitivity.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.Sensitivity.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Sensitivity.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Sensitivity.Location = new System.Drawing.Point(160, 179);
            this.Sensitivity.Margin = new System.Windows.Forms.Padding(0);
            this.Sensitivity.Name = "Sensitivity";
            this.Sensitivity.Size = new System.Drawing.Size(160, 28);
            this.Sensitivity.TabIndex = 17;
            this.Sensitivity.Text = "0";
            // 
            // uiSymbolLabel14
            // 
            this.uiSymbolLabel14.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel14.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel14.Location = new System.Drawing.Point(3, 182);
            this.uiSymbolLabel14.Name = "uiSymbolLabel14";
            this.uiSymbolLabel14.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel14.TabIndex = 8;
            this.uiSymbolLabel14.Text = "自适应灵敏度(mm)";
            this.uiSymbolLabel14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel6
            // 
            this.uiSymbolLabel6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel6.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel6.Location = new System.Drawing.Point(3, 238);
            this.uiSymbolLabel6.Name = "uiSymbolLabel6";
            this.uiSymbolLabel6.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel6.TabIndex = 0;
            this.uiSymbolLabel6.Text = "光斑直径(mm)";
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
            this.lblBaseTitle.Size = new System.Drawing.Size(634, 24);
            this.lblBaseTitle.TabIndex = 0;
            this.lblBaseTitle.Text = "基准工艺参数";
            // 
            // LaserDiameter
            // 
            this.LaserDiameter.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.LaserDiameter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LaserDiameter.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.LaserDiameter.Location = new System.Drawing.Point(160, 235);
            this.LaserDiameter.Margin = new System.Windows.Forms.Padding(0);
            this.LaserDiameter.Name = "LaserDiameter";
            this.LaserDiameter.Size = new System.Drawing.Size(160, 28);
            this.LaserDiameter.TabIndex = 9;
            this.LaserDiameter.Text = "0";
            // 
            // MinSeamWidth
            // 
            this.MinSeamWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MinSeamWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MinSeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.MinSeamWidth.Location = new System.Drawing.Point(160, 291);
            this.MinSeamWidth.Margin = new System.Windows.Forms.Padding(0);
            this.MinSeamWidth.Name = "MinSeamWidth";
            this.MinSeamWidth.Size = new System.Drawing.Size(160, 28);
            this.MinSeamWidth.TabIndex = 16;
            this.MinSeamWidth.Text = "0";
            // 
            // uiSymbolLabel8
            // 
            this.uiSymbolLabel8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel8.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel8.Location = new System.Drawing.Point(323, 210);
            this.uiSymbolLabel8.Name = "uiSymbolLabel8";
            this.uiSymbolLabel8.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel8.TabIndex = 3;
            this.uiSymbolLabel8.Text = "送丝速度(mm/min)";
            this.uiSymbolLabel8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel13
            // 
            this.uiSymbolLabel13.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel13.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel13.Location = new System.Drawing.Point(3, 294);
            this.uiSymbolLabel13.Name = "uiSymbolLabel13";
            this.uiSymbolLabel13.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel13.TabIndex = 7;
            this.uiSymbolLabel13.Text = "焊缝宽度下限(mm)";
            this.uiSymbolLabel13.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FeedSpeed
            // 
            this.FeedSpeed.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.FeedSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FeedSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.FeedSpeed.Location = new System.Drawing.Point(480, 207);
            this.FeedSpeed.Margin = new System.Windows.Forms.Padding(0);
            this.FeedSpeed.Name = "FeedSpeed";
            this.FeedSpeed.Size = new System.Drawing.Size(160, 28);
            this.FeedSpeed.TabIndex = 12;
            this.FeedSpeed.Text = "0";
            // 
            // uiSymbolLabel11
            // 
            this.uiSymbolLabel11.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel11.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel11.Location = new System.Drawing.Point(323, 294);
            this.uiSymbolLabel11.Name = "uiSymbolLabel11";
            this.uiSymbolLabel11.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel11.TabIndex = 6;
            this.uiSymbolLabel11.Text = "焊缝宽度上限(mm)";
            this.uiSymbolLabel11.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel5
            // 
            this.uiSymbolLabel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel5.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel5.Location = new System.Drawing.Point(3, 210);
            this.uiSymbolLabel5.Name = "uiSymbolLabel5";
            this.uiSymbolLabel5.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel5.TabIndex = 1;
            this.uiSymbolLabel5.Text = "激光功率(W)";
            this.uiSymbolLabel5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MaxSeamWidth
            // 
            this.MaxSeamWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MaxSeamWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MaxSeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.MaxSeamWidth.Location = new System.Drawing.Point(480, 291);
            this.MaxSeamWidth.Margin = new System.Windows.Forms.Padding(0);
            this.MaxSeamWidth.Name = "MaxSeamWidth";
            this.MaxSeamWidth.Size = new System.Drawing.Size(160, 28);
            this.MaxSeamWidth.TabIndex = 15;
            this.MaxSeamWidth.Text = "0";
            // 
            // LaserPower
            // 
            this.LaserPower.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.LaserPower.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LaserPower.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.LaserPower.Location = new System.Drawing.Point(160, 207);
            this.LaserPower.Margin = new System.Windows.Forms.Padding(0);
            this.LaserPower.Name = "LaserPower";
            this.LaserPower.Size = new System.Drawing.Size(160, 28);
            this.LaserPower.TabIndex = 10;
            this.LaserPower.Text = "0";
            // 
            // uiSymbolLabel9
            // 
            this.uiSymbolLabel9.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel9.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel9.Location = new System.Drawing.Point(323, 238);
            this.uiSymbolLabel9.Name = "uiSymbolLabel9";
            this.uiSymbolLabel9.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel9.TabIndex = 4;
            this.uiSymbolLabel9.Text = "焊缝宽度(mm)";
            this.uiSymbolLabel9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SeamWidth
            // 
            this.SeamWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.SeamWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.SeamWidth.Location = new System.Drawing.Point(480, 235);
            this.SeamWidth.Margin = new System.Windows.Forms.Padding(0);
            this.SeamWidth.Name = "SeamWidth";
            this.SeamWidth.Size = new System.Drawing.Size(160, 28);
            this.SeamWidth.TabIndex = 13;
            this.SeamWidth.Text = "0";
            // 
            // uiSymbolLabel7
            // 
            this.uiSymbolLabel7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel7.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel7.Location = new System.Drawing.Point(3, 266);
            this.uiSymbolLabel7.Name = "uiSymbolLabel7";
            this.uiSymbolLabel7.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel7.TabIndex = 2;
            this.uiSymbolLabel7.Text = "焊接速度(mm/s)";
            this.uiSymbolLabel7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // RobotSpeed
            // 
            this.RobotSpeed.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.RobotSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RobotSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.RobotSpeed.Location = new System.Drawing.Point(160, 263);
            this.RobotSpeed.Margin = new System.Windows.Forms.Padding(0);
            this.RobotSpeed.Name = "RobotSpeed";
            this.RobotSpeed.Size = new System.Drawing.Size(160, 28);
            this.RobotSpeed.TabIndex = 11;
            this.RobotSpeed.Text = "0";
            // 
            // uiSymbolLabel16
            // 
            this.uiSymbolLabel16.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel16.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel16.Location = new System.Drawing.Point(3, 124);
            this.uiSymbolLabel16.Name = "uiSymbolLabel16";
            this.uiSymbolLabel16.Size = new System.Drawing.Size(154, 22);
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
            this.uiComboBox1.Location = new System.Drawing.Point(160, 121);
            this.uiComboBox1.Margin = new System.Windows.Forms.Padding(0);
            this.uiComboBox1.Name = "uiComboBox1";
            this.uiComboBox1.PlaceholderText = "";
            this.uiComboBox1.Size = new System.Drawing.Size(160, 28);
            this.uiComboBox1.TabIndex = 1;
            // 
            // uiSymbolLabel3
            // 
            this.uiSymbolLabel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel3.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel3.Location = new System.Drawing.Point(323, 68);
            this.uiSymbolLabel3.Name = "uiSymbolLabel3";
            this.uiSymbolLabel3.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel3.TabIndex = 0;
            this.uiSymbolLabel3.Text = "板材厚度(mm)";
            this.uiSymbolLabel3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // PlateType
            // 
            this.PlateType.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.PlateType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PlateType.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.PlateType.Location = new System.Drawing.Point(480, 93);
            this.PlateType.Margin = new System.Windows.Forms.Padding(0);
            this.PlateType.Name = "PlateType";
            this.PlateType.PlaceholderText = "";
            this.PlateType.Size = new System.Drawing.Size(160, 28);
            this.PlateType.TabIndex = 2;
            // 
            // uiSymbolLabel4
            // 
            this.uiSymbolLabel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel4.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel4.Location = new System.Drawing.Point(323, 96);
            this.uiSymbolLabel4.Name = "uiSymbolLabel4";
            this.uiSymbolLabel4.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel4.TabIndex = 1;
            this.uiSymbolLabel4.Text = "板材材质";
            this.uiSymbolLabel4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Platethickness
            // 
            this.Platethickness.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.Platethickness.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Platethickness.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Platethickness.Location = new System.Drawing.Point(480, 65);
            this.Platethickness.Margin = new System.Windows.Forms.Padding(0);
            this.Platethickness.Name = "Platethickness";
            this.Platethickness.Size = new System.Drawing.Size(160, 28);
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
            this.lblWireTitle.Size = new System.Drawing.Size(634, 24);
            this.lblWireTitle.TabIndex = 0;
            this.lblWireTitle.Text = "焊丝工艺参数";
            // 
            // WireDiameter
            // 
            this.WireDiameter.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.WireDiameter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.WireDiameter.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.WireDiameter.Location = new System.Drawing.Point(160, 65);
            this.WireDiameter.Margin = new System.Windows.Forms.Padding(0);
            this.WireDiameter.Name = "WireDiameter";
            this.WireDiameter.Size = new System.Drawing.Size(160, 28);
            this.WireDiameter.TabIndex = 3;
            this.WireDiameter.Text = "0";
            // 
            // uiSymbolLabel1
            // 
            this.uiSymbolLabel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.uiSymbolLabel1.Location = new System.Drawing.Point(3, 96);
            this.uiSymbolLabel1.Name = "uiSymbolLabel1";
            this.uiSymbolLabel1.Size = new System.Drawing.Size(154, 22);
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
            this.uiSymbolLabel2.Size = new System.Drawing.Size(154, 22);
            this.uiSymbolLabel2.TabIndex = 1;
            this.uiSymbolLabel2.Text = "焊丝直径(mm)";
            this.uiSymbolLabel2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WireType
            // 
            this.WireType.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.WireType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.WireType.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.WireType.Location = new System.Drawing.Point(160, 93);
            this.WireType.Margin = new System.Windows.Forms.Padding(0);
            this.WireType.Name = "WireType";
            this.WireType.PlaceholderText = "";
            this.WireType.Size = new System.Drawing.Size(160, 28);
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
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16F));
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 28F));
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 28F));
            this.gridWeldZones.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 28F));
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
            this.panelConfigList.Size = new System.Drawing.Size(273, 857);
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
            this.gridConfigList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridConfigList.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridConfigList.Size = new System.Drawing.Size(255, 839);
            this.gridConfigList.TabIndex = 2;
            // 
            // lblConfigListTitle
            // 
            this.gridConfigList.SetColumnSpan(this.lblConfigListTitle, 4);
            this.lblConfigListTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblConfigListTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblConfigListTitle.Location = new System.Drawing.Point(0, 0);
            this.lblConfigListTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblConfigListTitle.Name = "lblConfigListTitle";
            this.lblConfigListTitle.Size = new System.Drawing.Size(255, 35);
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
            this.tableConfigList.Size = new System.Drawing.Size(249, 770);
            this.tableConfigList.TabIndex = 1;
            // 
            // btnConfigAddNew
            // 
            this.btnConfigAddNew.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConfigAddNew.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConfigAddNew.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnConfigAddNew.Location = new System.Drawing.Point(0, 811);
            this.btnConfigAddNew.Margin = new System.Windows.Forms.Padding(0);
            this.btnConfigAddNew.Name = "btnConfigAddNew";
            this.btnConfigAddNew.Size = new System.Drawing.Size(63, 28);
            this.btnConfigAddNew.TabIndex = 2;
            this.btnConfigAddNew.Text = "新建";
            this.btnConfigAddNew.Click += new System.EventHandler(this.btnConfigAddNew_Click);
            // 
            // btnConfigDelete
            // 
            this.btnConfigDelete.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConfigDelete.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConfigDelete.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnConfigDelete.Location = new System.Drawing.Point(189, 811);
            this.btnConfigDelete.Margin = new System.Windows.Forms.Padding(0);
            this.btnConfigDelete.Name = "btnConfigDelete";
            this.btnConfigDelete.Size = new System.Drawing.Size(66, 28);
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
            this.gridPanelRight.Location = new System.Drawing.Point(282, 3);
            this.gridPanelRight.Name = "gridPanelRight";
            this.gridPanelRight.RowCount = 3;
            this.gridPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 412F));
            this.gridPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.gridPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridPanelRight.Size = new System.Drawing.Size(1459, 857);
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
            this.panelAdaparam.Size = new System.Drawing.Size(1453, 406);
            this.panelAdaparam.TabIndex = 0;
            // 
            // gridAdaparam
            // 
            this.gridAdaparam.ColumnCount = 5;
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridAdaparam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
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
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel14, 0, 6);
            this.gridAdaparam.Controls.Add(this.Sensitivity, 1, 6);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel5, 0, 7);
            this.gridAdaparam.Controls.Add(this.LaserPower, 1, 7);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel8, 2, 7);
            this.gridAdaparam.Controls.Add(this.FeedSpeed, 3, 7);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel6, 0, 8);
            this.gridAdaparam.Controls.Add(this.LaserDiameter, 1, 8);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel9, 2, 8);
            this.gridAdaparam.Controls.Add(this.SeamWidth, 3, 8);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel7, 0, 9);
            this.gridAdaparam.Controls.Add(this.RobotSpeed, 1, 9);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel13, 0, 10);
            this.gridAdaparam.Controls.Add(this.MinSeamWidth, 1, 10);
            this.gridAdaparam.Controls.Add(this.uiSymbolLabel11, 2, 10);
            this.gridAdaparam.Controls.Add(this.MaxSeamWidth, 3, 10);
            this.gridAdaparam.Controls.Add(this.btnShowAutoParam, 0, 11);
            this.gridAdaparam.Controls.Add(this.btnCorBaseline, 1, 11);
            this.gridAdaparam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridAdaparam.Location = new System.Drawing.Point(9, 9);
            this.gridAdaparam.Name = "gridAdaparam";
            this.gridAdaparam.RowCount = 13;
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
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridAdaparam.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridAdaparam.Size = new System.Drawing.Size(1435, 388);
            this.gridAdaparam.TabIndex = 0;
            // 
            // label1
            // 
            this.gridAdaparam.SetColumnSpan(this.label1, 4);
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(640, 35);
            this.label1.TabIndex = 18;
            this.label1.Text = "焊接工艺自适应调参模型";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.gridPanelRight.SetColumnSpan(this.panel2, 4);
            this.panel2.Controls.Add(this.gridPanel1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(3, 415);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(3);
            this.panel2.Shadow = 5;
            this.panel2.ShadowColor = System.Drawing.Color.Black;
            this.panel2.ShadowOpacity = 0.2F;
            this.panel2.Size = new System.Drawing.Size(1453, 439);
            this.panel2.TabIndex = 1;
            this.panel2.Text = "panel2";
            // 
            // gridPanel1
            // 
            this.gridPanel1.ColumnCount = 5;
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.gridPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.gridPanel1.Controls.Add(this.lblWeldAdjTitle, 0, 0);
            this.gridPanel1.Controls.Add(this.lblFlowCountNum, 0, 1);
            this.gridPanel1.Controls.Add(this.txtFlowCountNum, 1, 1);
            this.gridPanel1.Controls.Add(this.lblFlowMaxConsecFail, 0, 2);
            this.gridPanel1.Controls.Add(this.txtFlowMaxConsecFail, 1, 2);
            this.gridPanel1.Controls.Add(this.lblParamOutputHz, 2, 2);
            this.gridPanel1.Controls.Add(this.selParamOutputHz, 3, 2);
            this.gridPanel1.Controls.Add(this.lblParamSpotGain, 0, 3);
            this.gridPanel1.Controls.Add(this.txtParamSpotGain, 1, 3);
            this.gridPanel1.Controls.Add(this.lblParamManualWidth, 2, 3);
            this.gridPanel1.Controls.Add(this.txtParamManualWidth, 3, 3);
            this.gridPanel1.Controls.Add(this.flowPanel1, 0, 4);
            this.gridPanel1.Controls.Add(this.lblNoValueAlarmHint, 0, 5);
            this.gridPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPanel1.Location = new System.Drawing.Point(9, 9);
            this.gridPanel1.Name = "gridPanel1";
            this.gridPanel1.RowCount = 6;
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.gridPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridPanel1.Size = new System.Drawing.Size(1435, 421);
            this.gridPanel1.TabIndex = 0;
            this.gridPanel1.Text = "gridPanel1";
            // 
            // lblWeldAdjTitle
            // 
            this.gridPanel1.SetColumnSpan(this.lblWeldAdjTitle, 4);
            this.lblWeldAdjTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblWeldAdjTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblWeldAdjTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblWeldAdjTitle.Location = new System.Drawing.Point(0, 0);
            this.lblWeldAdjTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblWeldAdjTitle.Name = "lblWeldAdjTitle";
            this.lblWeldAdjTitle.Size = new System.Drawing.Size(640, 35);
            this.lblWeldAdjTitle.TabIndex = 0;
            this.lblWeldAdjTitle.Text = "焊参调整（WeldParamAdjustment）";
            // 
            // lblNoValueAlarmHint
            // 
            this.gridPanel1.SetColumnSpan(this.lblNoValueAlarmHint, 5);
            this.lblNoValueAlarmHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNoValueAlarmHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblNoValueAlarmHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.lblNoValueAlarmHint.Location = new System.Drawing.Point(0, 159);
            this.lblNoValueAlarmHint.Margin = new System.Windows.Forms.Padding(0);
            this.lblNoValueAlarmHint.Name = "lblNoValueAlarmHint";
            this.lblNoValueAlarmHint.Size = new System.Drawing.Size(1435, 28);
            this.lblNoValueAlarmHint.TabIndex = 150;
            this.lblNoValueAlarmHint.Text = "缺值达到阈值即报警停焊：清警后回起始位从头重焊，不支持断点续焊。";
            // 
            // lblFlowCountNum
            // 
            this.lblFlowCountNum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFlowCountNum.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblFlowCountNum.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblFlowCountNum.Location = new System.Drawing.Point(0, 35);
            this.lblFlowCountNum.Margin = new System.Windows.Forms.Padding(0);
            this.lblFlowCountNum.Name = "lblFlowCountNum";
            this.lblFlowCountNum.Size = new System.Drawing.Size(160, 28);
            this.lblFlowCountNum.TabIndex = 144;
            this.lblFlowCountNum.Text = "每段距离(mm)";
            this.lblFlowCountNum.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtFlowCountNum
            // 
            this.txtFlowCountNum.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtFlowCountNum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFlowCountNum.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtFlowCountNum.Location = new System.Drawing.Point(160, 35);
            this.txtFlowCountNum.Margin = new System.Windows.Forms.Padding(0);
            this.txtFlowCountNum.Name = "txtFlowCountNum";
            this.txtFlowCountNum.Size = new System.Drawing.Size(160, 28);
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
            this.lblFlowMaxConsecFail.Size = new System.Drawing.Size(160, 28);
            this.lblFlowMaxConsecFail.TabIndex = 143;
            this.lblFlowMaxConsecFail.Text = "最大连续失败次数";
            this.lblFlowMaxConsecFail.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtFlowMaxConsecFail
            // 
            this.txtFlowMaxConsecFail.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtFlowMaxConsecFail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFlowMaxConsecFail.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtFlowMaxConsecFail.Location = new System.Drawing.Point(160, 63);
            this.txtFlowMaxConsecFail.Margin = new System.Windows.Forms.Padding(0);
            this.txtFlowMaxConsecFail.Name = "txtFlowMaxConsecFail";
            this.txtFlowMaxConsecFail.Size = new System.Drawing.Size(160, 28);
            this.txtFlowMaxConsecFail.TabIndex = 143;
            this.txtFlowMaxConsecFail.Text = "0.000";
            // 
            // lblParamOutputHz
            // 
            this.lblParamOutputHz.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblParamOutputHz.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblParamOutputHz.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblParamOutputHz.Location = new System.Drawing.Point(320, 63);
            this.lblParamOutputHz.Margin = new System.Windows.Forms.Padding(0);
            this.lblParamOutputHz.Name = "lblParamOutputHz";
            this.lblParamOutputHz.Size = new System.Drawing.Size(160, 28);
            this.lblParamOutputHz.TabIndex = 153;
            this.lblParamOutputHz.Text = "工艺输出频率(Hz)";
            this.lblParamOutputHz.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // selParamOutputHz
            // 
            this.selParamOutputHz.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selParamOutputHz.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.selParamOutputHz.Items.AddRange(new object[] {
            "60",
            "30",
            "10"});
            this.selParamOutputHz.Location = new System.Drawing.Point(480, 63);
            this.selParamOutputHz.Margin = new System.Windows.Forms.Padding(0);
            this.selParamOutputHz.Name = "selParamOutputHz";
            this.selParamOutputHz.PlaceholderText = "";
            this.selParamOutputHz.Size = new System.Drawing.Size(160, 28);
            this.selParamOutputHz.TabIndex = 153;
            // 
            // lblParamSpotGain
            // 
            this.lblParamSpotGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblParamSpotGain.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblParamSpotGain.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblParamSpotGain.Location = new System.Drawing.Point(0, 91);
            this.lblParamSpotGain.Margin = new System.Windows.Forms.Padding(0);
            this.lblParamSpotGain.Name = "lblParamSpotGain";
            this.lblParamSpotGain.Size = new System.Drawing.Size(160, 28);
            this.lblParamSpotGain.TabIndex = 152;
            this.lblParamSpotGain.Text = "光斑宽度系数W";
            this.lblParamSpotGain.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtParamSpotGain
            // 
            this.txtParamSpotGain.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtParamSpotGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtParamSpotGain.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtParamSpotGain.Location = new System.Drawing.Point(160, 91);
            this.txtParamSpotGain.Margin = new System.Windows.Forms.Padding(0);
            this.txtParamSpotGain.Name = "txtParamSpotGain";
            this.txtParamSpotGain.Size = new System.Drawing.Size(160, 28);
            this.txtParamSpotGain.TabIndex = 152;
            this.txtParamSpotGain.Text = "1.000";
            // 
            // lblParamManualWidth
            // 
            this.lblParamManualWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblParamManualWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblParamManualWidth.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblParamManualWidth.Location = new System.Drawing.Point(320, 91);
            this.lblParamManualWidth.Margin = new System.Windows.Forms.Padding(0);
            this.lblParamManualWidth.Name = "lblParamManualWidth";
            this.lblParamManualWidth.Size = new System.Drawing.Size(160, 28);
            this.lblParamManualWidth.TabIndex = 151;
            this.lblParamManualWidth.Text = "手动固定缝宽(mm)";
            this.lblParamManualWidth.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtParamManualWidth
            // 
            this.txtParamManualWidth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtParamManualWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtParamManualWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtParamManualWidth.Location = new System.Drawing.Point(480, 91);
            this.txtParamManualWidth.Margin = new System.Windows.Forms.Padding(0);
            this.txtParamManualWidth.Name = "txtParamManualWidth";
            this.txtParamManualWidth.Size = new System.Drawing.Size(160, 28);
            this.txtParamManualWidth.TabIndex = 151;
            this.txtParamManualWidth.Text = "1.200";
            // 
            // flowPanel1
            // 
            this.flowPanel1.Align = AntdUI.TAlignFlow.RightCenter;
            this.gridPanel1.SetColumnSpan(this.flowPanel1, 4);
            this.flowPanel1.Controls.Add(this.btnReadAutoParam);
            this.flowPanel1.Controls.Add(this.btnCorAutoParam);
            this.flowPanel1.Controls.Add(this.btnSaveAutoParam);
            this.flowPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowPanel1.Location = new System.Drawing.Point(3, 122);
            this.flowPanel1.Name = "flowPanel1";
            this.flowPanel1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.flowPanel1.Size = new System.Drawing.Size(634, 34);
            this.flowPanel1.TabIndex = 154;
            this.flowPanel1.Text = "flowPanel1";
            // 
            // btnReadAutoParam
            // 
            this.btnReadAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnReadAutoParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReadAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadAutoParam.Ghost = true;
            this.btnReadAutoParam.IconSvg = "ReloadOutlined";
            this.btnReadAutoParam.Location = new System.Drawing.Point(437, 3);
            this.btnReadAutoParam.Name = "btnReadAutoParam";
            this.btnReadAutoParam.Size = new System.Drawing.Size(194, 32);
            this.btnReadAutoParam.TabIndex = 5;
            this.btnReadAutoParam.Text = "读取焊参调整";
            this.btnReadAutoParam.Click += new System.EventHandler(this.BtnReadAutoParam_Click);
            // 
            // btnSaveAutoParam
            // 
            this.btnSaveAutoParam.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSaveAutoParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveAutoParam.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSaveAutoParam.Ghost = true;
            this.btnSaveAutoParam.IconSvg = "SaveOutlined";
            this.btnSaveAutoParam.Location = new System.Drawing.Point(131, 3);
            this.btnSaveAutoParam.Name = "btnSaveAutoParam";
            this.btnSaveAutoParam.Size = new System.Drawing.Size(194, 32);
            this.btnSaveAutoParam.TabIndex = 4;
            this.btnSaveAutoParam.Text = "保存焊参调整";
            this.btnSaveAutoParam.Click += new System.EventHandler(this.BtnSaveAutoParam_Click);
            // 
            // tabsProcess
            // 
            this.tabsProcess.Controls.Add(this.tabPageQuickSetup);
            this.tabsProcess.Controls.Add(this.tabPageAdaptiveSetup);
            this.tabsProcess.Cursor = System.Windows.Forms.Cursors.Default;
            this.tabsProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsProcess.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.tabsProcess.Location = new System.Drawing.Point(0, 0);
            this.tabsProcess.Name = "tabsProcess";
            this.tabsProcess.Pages.Add(this.tabPageQuickSetup);
            this.tabsProcess.Pages.Add(this.tabPageAdaptiveSetup);
            this.tabsProcess.Size = new System.Drawing.Size(1762, 920);
            this.tabsProcess.Style = styleCard1;
            this.tabsProcess.TabIndex = 2;
            this.tabsProcess.Text = "tabsProcess";
            this.tabsProcess.Type = AntdUI.TabType.Card;
            // 
            // tabPageQuickSetup
            // 
            this.tabPageQuickSetup.Controls.Add(this.gridQuickZones);
            this.tabPageQuickSetup.Location = new System.Drawing.Point(0, 39);
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
            this.panelProcessDebug.Controls.Add(this.tableLayoutPanel2);
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
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 4;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.Controls.Add(this.label3, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.radioQuickLaPose2, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.radioQuickLaPose1, 3, 2);
            this.tableLayoutPanel2.Controls.Add(this.radioQuickLaAuto, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.radioQuickLaSpot2, 2, 2);
            this.tableLayoutPanel2.Controls.Add(this.radioQuickLaSpot1, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.label2, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.lblLaSpot1, 0, 6);
            this.tableLayoutPanel2.Controls.Add(this.txtLaSpot1, 1, 6);
            this.tableLayoutPanel2.Controls.Add(this.lblLaSpot2, 2, 6);
            this.tableLayoutPanel2.Controls.Add(this.txtLaSpot2, 3, 6);
            this.tableLayoutPanel2.Controls.Add(this.txtLaPose1, 1, 5);
            this.tableLayoutPanel2.Controls.Add(this.lblLaPose2, 2, 5);
            this.tableLayoutPanel2.Controls.Add(this.lblLaPose1, 0, 5);
            this.tableLayoutPanel2.Controls.Add(this.txtLaPose2, 3, 5);
            this.tableLayoutPanel2.Controls.Add(this.lblSpotTarget, 0, 7);
            this.tableLayoutPanel2.Controls.Add(this.txtLaSpotTarget, 1, 7);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(9, 9);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 10;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(856, 396);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // radioQuickLaPose2
            // 
            this.radioQuickLaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaPose2.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaPose2.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaPose2.Location = new System.Drawing.Point(0, 84);
            this.radioQuickLaPose2.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaPose2.Name = "radioQuickLaPose2";
            this.radioQuickLaPose2.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaPose2.TabIndex = 14;
            this.radioQuickLaPose2.Text = "LA_Pose2";
            this.radioQuickLaPose2.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // radioQuickLaPose1
            // 
            this.radioQuickLaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaPose1.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaPose1.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaPose1.Location = new System.Drawing.Point(642, 56);
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
            this.radioQuickLaAuto.Location = new System.Drawing.Point(0, 56);
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
            this.radioQuickLaSpot2.Location = new System.Drawing.Point(428, 56);
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
            this.radioQuickLaSpot1.Location = new System.Drawing.Point(214, 56);
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
            this.tableLayoutPanel2.SetColumnSpan(this.label2, 4);
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.label2.Location = new System.Drawing.Point(0, 0);
            this.label2.Margin = new System.Windows.Forms.Padding(0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(856, 28);
            this.label2.TabIndex = 11;
            this.label2.Text = "LA Setting";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblLaSpot1
            // 
            this.lblLaSpot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaSpot1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaSpot1.Location = new System.Drawing.Point(3, 168);
            this.lblLaSpot1.Name = "lblLaSpot1";
            this.lblLaSpot1.Size = new System.Drawing.Size(208, 28);
            this.lblLaSpot1.TabIndex = 10;
            this.lblLaSpot1.Text = "LA Spot1 光斑(mm)";
            // 
            // txtLaSpot1
            // 
            this.txtLaSpot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaSpot1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaSpot1.Location = new System.Drawing.Point(214, 168);
            this.txtLaSpot1.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaSpot1.Name = "txtLaSpot1";
            this.txtLaSpot1.Size = new System.Drawing.Size(214, 28);
            this.txtLaSpot1.TabIndex = 10;
            // 
            // lblLaSpot2
            // 
            this.lblLaSpot2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaSpot2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaSpot2.Location = new System.Drawing.Point(431, 168);
            this.lblLaSpot2.Name = "lblLaSpot2";
            this.lblLaSpot2.Size = new System.Drawing.Size(208, 28);
            this.lblLaSpot2.TabIndex = 10;
            this.lblLaSpot2.Text = "LA Spot2 光斑(mm)";
            // 
            // txtLaSpot2
            // 
            this.txtLaSpot2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaSpot2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaSpot2.Location = new System.Drawing.Point(642, 168);
            this.txtLaSpot2.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaSpot2.Name = "txtLaSpot2";
            this.txtLaSpot2.Size = new System.Drawing.Size(214, 28);
            this.txtLaSpot2.TabIndex = 10;
            // 
            // txtLaPose1
            // 
            this.txtLaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaPose1.Location = new System.Drawing.Point(214, 140);
            this.txtLaPose1.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaPose1.Name = "txtLaPose1";
            this.txtLaPose1.Size = new System.Drawing.Size(214, 28);
            this.txtLaPose1.TabIndex = 10;
            // 
            // lblLaPose2
            // 
            this.lblLaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaPose2.Location = new System.Drawing.Point(431, 140);
            this.lblLaPose2.Name = "lblLaPose2";
            this.lblLaPose2.Size = new System.Drawing.Size(208, 28);
            this.lblLaPose2.TabIndex = 10;
            this.lblLaPose2.Text = "LA Pose2 位(mm)";
            // 
            // lblLaPose1
            // 
            this.lblLaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblLaPose1.Location = new System.Drawing.Point(3, 140);
            this.lblLaPose1.Name = "lblLaPose1";
            this.lblLaPose1.Size = new System.Drawing.Size(208, 28);
            this.lblLaPose1.TabIndex = 10;
            this.lblLaPose1.Text = "LA Pose1 位(mm)";
            // 
            // txtLaPose2
            // 
            this.txtLaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaPose2.Location = new System.Drawing.Point(642, 140);
            this.txtLaPose2.Margin = new System.Windows.Forms.Padding(0);
            this.txtLaPose2.Name = "txtLaPose2";
            this.txtLaPose2.Size = new System.Drawing.Size(214, 28);
            this.txtLaPose2.TabIndex = 10;
            // 
            // lblSpotTarget
            // 
            this.lblSpotTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSpotTarget.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblSpotTarget.Location = new System.Drawing.Point(3, 196);
            this.lblSpotTarget.Name = "lblSpotTarget";
            this.lblSpotTarget.Size = new System.Drawing.Size(208, 28);
            this.lblSpotTarget.TabIndex = 10;
            this.lblSpotTarget.Text = "Auto目标光斑(mm)";
            // 
            // txtLaSpotTarget
            // 
            this.txtLaSpotTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLaSpotTarget.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtLaSpotTarget.Location = new System.Drawing.Point(214, 196);
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
            this.gridQuickTracking.Controls.Add(this.lblQuickTracking, 0, 0);
            this.gridQuickTracking.Controls.Add(this.radioQuickYAuto, 0, 1);
            this.gridQuickTracking.Controls.Add(this.radioQuickYForceCalibPose, 1, 1);
            this.gridQuickTracking.Controls.Add(this.radioQuickYForceHome, 2, 1);
            this.gridQuickTracking.Controls.Add(this.lblGeoAxisPerLaser, 0, 2);
            this.gridQuickTracking.Controls.Add(this.txtGeoAxisPerLaser, 1, 2);
            this.gridQuickTracking.Controls.Add(this.lblGeoGuideSign, 2, 2);
            this.gridQuickTracking.Controls.Add(this.selGeoGuideSign, 3, 2);
            this.gridQuickTracking.Controls.Add(this.lblGeoLeadDistance, 0, 3);
            this.gridQuickTracking.Controls.Add(this.txtGeoLeadDistance, 1, 3);
            this.gridQuickTracking.Controls.Add(this.lblGeoRobotYGain, 2, 3);
            this.gridQuickTracking.Controls.Add(this.txtGeoRobotYGain, 3, 3);
            this.gridQuickTracking.Controls.Add(this.lblGeoYAxisMin, 0, 4);
            this.gridQuickTracking.Controls.Add(this.txtGeoYAxisMin, 1, 4);
            this.gridQuickTracking.Controls.Add(this.lblGeoYAxisMax, 2, 4);
            this.gridQuickTracking.Controls.Add(this.txtGeoYAxisMax, 3, 4);
            this.gridQuickTracking.Controls.Add(this.lblTeachLockY, 0, 5);
            this.gridQuickTracking.Controls.Add(this.txtTeachLockY, 1, 5);
            this.gridQuickTracking.Controls.Add(this.lblCalibCenterY, 0, 6);
            this.gridQuickTracking.Controls.Add(this.txtCalibCenterY, 1, 6);
            this.gridQuickTracking.Controls.Add(this.lblCalibCenterZ, 2, 6);
            this.gridQuickTracking.Controls.Add(this.txtCalibCenterZ, 3, 6);
            this.gridQuickTracking.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickTracking.Location = new System.Drawing.Point(9, 9);
            this.gridQuickTracking.Name = "gridQuickTracking";
            this.gridQuickTracking.RowCount = 8;
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickTracking.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridQuickTracking.Size = new System.Drawing.Size(858, 396);
            this.gridQuickTracking.TabIndex = 11;
            // 
            // lblQuickTracking
            // 
            this.lblQuickTracking.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridQuickTracking.SetColumnSpan(this.lblQuickTracking, 4);
            this.lblQuickTracking.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblQuickTracking.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblQuickTracking.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblQuickTracking.Location = new System.Drawing.Point(0, 0);
            this.lblQuickTracking.Margin = new System.Windows.Forms.Padding(0);
            this.lblQuickTracking.Name = "lblQuickTracking";
            this.lblQuickTracking.Size = new System.Drawing.Size(858, 35);
            this.lblQuickTracking.TabIndex = 0;
            this.lblQuickTracking.Text = "Tracking Setting";
            this.lblQuickTracking.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioQuickYAuto
            // 
            this.radioQuickYAuto.Checked = true;
            this.radioQuickYAuto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickYAuto.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickYAuto.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickYAuto.ForeColor = System.Drawing.Color.Black;
            this.radioQuickYAuto.Location = new System.Drawing.Point(0, 35);
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
            this.radioQuickYForceCalibPose.Location = new System.Drawing.Point(214, 35);
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
            this.radioQuickYForceHome.Location = new System.Drawing.Point(428, 35);
            this.radioQuickYForceHome.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickYForceHome.Name = "radioQuickYForceHome";
            this.radioQuickYForceHome.Size = new System.Drawing.Size(214, 28);
            this.radioQuickYForceHome.TabIndex = 12;
            this.radioQuickYForceHome.Text = "Y_ForceHome";
            this.radioQuickYForceHome.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickY_CheckedChanged);
            // 
            // lblGeoAxisPerLaser
            // 
            this.lblGeoAxisPerLaser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGeoAxisPerLaser.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGeoAxisPerLaser.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblGeoAxisPerLaser.Location = new System.Drawing.Point(3, 66);
            this.lblGeoAxisPerLaser.Name = "lblGeoAxisPerLaser";
            this.lblGeoAxisPerLaser.Size = new System.Drawing.Size(208, 22);
            this.lblGeoAxisPerLaser.TabIndex = 113;
            this.lblGeoAxisPerLaser.Text = "轴/激光比例系数";
            this.lblGeoAxisPerLaser.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGeoAxisPerLaser
            // 
            this.txtGeoAxisPerLaser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGeoAxisPerLaser.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGeoAxisPerLaser.Location = new System.Drawing.Point(214, 63);
            this.txtGeoAxisPerLaser.Margin = new System.Windows.Forms.Padding(0);
            this.txtGeoAxisPerLaser.Name = "txtGeoAxisPerLaser";
            this.txtGeoAxisPerLaser.Size = new System.Drawing.Size(214, 28);
            this.txtGeoAxisPerLaser.TabIndex = 113;
            this.txtGeoAxisPerLaser.Text = "0.000";
            // 
            // lblGeoGuideSign
            // 
            this.lblGeoGuideSign.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGeoGuideSign.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGeoGuideSign.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblGeoGuideSign.Location = new System.Drawing.Point(431, 66);
            this.lblGeoGuideSign.Name = "lblGeoGuideSign";
            this.lblGeoGuideSign.Size = new System.Drawing.Size(208, 22);
            this.lblGeoGuideSign.TabIndex = 115;
            this.lblGeoGuideSign.Text = "偏差方向";
            this.lblGeoGuideSign.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // selGeoGuideSign
            // 
            this.selGeoGuideSign.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selGeoGuideSign.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.selGeoGuideSign.Items.AddRange(new object[] {
            "正向 (+1)",
            "反向 (-1)"});
            this.selGeoGuideSign.Location = new System.Drawing.Point(642, 63);
            this.selGeoGuideSign.Margin = new System.Windows.Forms.Padding(0);
            this.selGeoGuideSign.Name = "selGeoGuideSign";
            this.selGeoGuideSign.PlaceholderText = "";
            this.selGeoGuideSign.Size = new System.Drawing.Size(216, 28);
            this.selGeoGuideSign.TabIndex = 115;
            // 
            // lblGeoLeadDistance
            // 
            this.lblGeoLeadDistance.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGeoLeadDistance.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGeoLeadDistance.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblGeoLeadDistance.Location = new System.Drawing.Point(3, 94);
            this.lblGeoLeadDistance.Name = "lblGeoLeadDistance";
            this.lblGeoLeadDistance.Size = new System.Drawing.Size(208, 22);
            this.lblGeoLeadDistance.TabIndex = 117;
            this.lblGeoLeadDistance.Text = "激光超前量(mm)";
            this.lblGeoLeadDistance.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGeoLeadDistance
            // 
            this.txtGeoLeadDistance.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGeoLeadDistance.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGeoLeadDistance.Location = new System.Drawing.Point(214, 91);
            this.txtGeoLeadDistance.Margin = new System.Windows.Forms.Padding(0);
            this.txtGeoLeadDistance.Name = "txtGeoLeadDistance";
            this.txtGeoLeadDistance.Size = new System.Drawing.Size(214, 28);
            this.txtGeoLeadDistance.TabIndex = 117;
            this.txtGeoLeadDistance.Text = "0.000";
            // 
            // lblGeoRobotYGain
            // 
            this.lblGeoRobotYGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGeoRobotYGain.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGeoRobotYGain.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblGeoRobotYGain.Location = new System.Drawing.Point(431, 94);
            this.lblGeoRobotYGain.Name = "lblGeoRobotYGain";
            this.lblGeoRobotYGain.Size = new System.Drawing.Size(208, 22);
            this.lblGeoRobotYGain.TabIndex = 118;
            this.lblGeoRobotYGain.Text = "机器人Y补偿系数";
            this.lblGeoRobotYGain.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGeoRobotYGain
            // 
            this.txtGeoRobotYGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGeoRobotYGain.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGeoRobotYGain.Location = new System.Drawing.Point(642, 91);
            this.txtGeoRobotYGain.Margin = new System.Windows.Forms.Padding(0);
            this.txtGeoRobotYGain.Name = "txtGeoRobotYGain";
            this.txtGeoRobotYGain.Size = new System.Drawing.Size(216, 28);
            this.txtGeoRobotYGain.TabIndex = 118;
            this.txtGeoRobotYGain.Text = "1.000";
            // 
            // lblGeoYAxisMin
            // 
            this.lblGeoYAxisMin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGeoYAxisMin.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGeoYAxisMin.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblGeoYAxisMin.Location = new System.Drawing.Point(3, 122);
            this.lblGeoYAxisMin.Name = "lblGeoYAxisMin";
            this.lblGeoYAxisMin.Size = new System.Drawing.Size(208, 22);
            this.lblGeoYAxisMin.TabIndex = 119;
            this.lblGeoYAxisMin.Text = "Y轴行程下限(mm)";
            this.lblGeoYAxisMin.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGeoYAxisMin
            // 
            this.txtGeoYAxisMin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGeoYAxisMin.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGeoYAxisMin.Location = new System.Drawing.Point(214, 119);
            this.txtGeoYAxisMin.Margin = new System.Windows.Forms.Padding(0);
            this.txtGeoYAxisMin.Name = "txtGeoYAxisMin";
            this.txtGeoYAxisMin.Size = new System.Drawing.Size(214, 28);
            this.txtGeoYAxisMin.TabIndex = 119;
            this.txtGeoYAxisMin.Text = "0.000";
            // 
            // lblGeoYAxisMax
            // 
            this.lblGeoYAxisMax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGeoYAxisMax.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGeoYAxisMax.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblGeoYAxisMax.Location = new System.Drawing.Point(431, 122);
            this.lblGeoYAxisMax.Name = "lblGeoYAxisMax";
            this.lblGeoYAxisMax.Size = new System.Drawing.Size(208, 22);
            this.lblGeoYAxisMax.TabIndex = 120;
            this.lblGeoYAxisMax.Text = "Y轴行程上限(mm)";
            this.lblGeoYAxisMax.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtGeoYAxisMax
            // 
            this.txtGeoYAxisMax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGeoYAxisMax.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGeoYAxisMax.Location = new System.Drawing.Point(642, 119);
            this.txtGeoYAxisMax.Margin = new System.Windows.Forms.Padding(0);
            this.txtGeoYAxisMax.Name = "txtGeoYAxisMax";
            this.txtGeoYAxisMax.Size = new System.Drawing.Size(216, 28);
            this.txtGeoYAxisMax.TabIndex = 120;
            this.txtGeoYAxisMax.Text = "0.000";
            // 
            // lblTeachLockY
            // 
            this.lblTeachLockY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTeachLockY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTeachLockY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTeachLockY.Location = new System.Drawing.Point(3, 150);
            this.lblTeachLockY.Name = "lblTeachLockY";
            this.lblTeachLockY.Size = new System.Drawing.Size(208, 22);
            this.lblTeachLockY.TabIndex = 8;
            this.lblTeachLockY.Text = "示教Y轴锁定坐标(mm)";
            this.lblTeachLockY.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtTeachLockY
            // 
            this.txtTeachLockY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTeachLockY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTeachLockY.Location = new System.Drawing.Point(214, 147);
            this.txtTeachLockY.Margin = new System.Windows.Forms.Padding(0);
            this.txtTeachLockY.Name = "txtTeachLockY";
            this.txtTeachLockY.Size = new System.Drawing.Size(214, 28);
            this.txtTeachLockY.TabIndex = 9;
            this.txtTeachLockY.Text = "0.000";
            // 
            // lblCalibCenterY
            // 
            this.lblCalibCenterY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCalibCenterY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblCalibCenterY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCalibCenterY.Location = new System.Drawing.Point(3, 178);
            this.lblCalibCenterY.Name = "lblCalibCenterY";
            this.lblCalibCenterY.Size = new System.Drawing.Size(208, 22);
            this.lblCalibCenterY.TabIndex = 6;
            this.lblCalibCenterY.Text = "标定中心点Y(mm)";
            this.lblCalibCenterY.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtCalibCenterY
            // 
            this.txtCalibCenterY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCalibCenterY.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtCalibCenterY.Location = new System.Drawing.Point(214, 175);
            this.txtCalibCenterY.Margin = new System.Windows.Forms.Padding(0);
            this.txtCalibCenterY.Name = "txtCalibCenterY";
            this.txtCalibCenterY.Size = new System.Drawing.Size(214, 28);
            this.txtCalibCenterY.TabIndex = 7;
            this.txtCalibCenterY.Text = "0.000";
            // 
            // lblCalibCenterZ
            // 
            this.lblCalibCenterZ.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCalibCenterZ.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblCalibCenterZ.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCalibCenterZ.Location = new System.Drawing.Point(431, 178);
            this.lblCalibCenterZ.Name = "lblCalibCenterZ";
            this.lblCalibCenterZ.Size = new System.Drawing.Size(208, 22);
            this.lblCalibCenterZ.TabIndex = 4;
            this.lblCalibCenterZ.Text = "标定中心点Z(mm)";
            this.lblCalibCenterZ.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtCalibCenterZ
            // 
            this.txtCalibCenterZ.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCalibCenterZ.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtCalibCenterZ.Location = new System.Drawing.Point(642, 175);
            this.txtCalibCenterZ.Margin = new System.Windows.Forms.Padding(0);
            this.txtCalibCenterZ.Name = "txtCalibCenterZ";
            this.txtCalibCenterZ.Size = new System.Drawing.Size(216, 28);
            this.txtCalibCenterZ.TabIndex = 5;
            this.txtCalibCenterZ.Text = "0.000";
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
            this.gridQuickTa.Controls.Add(this.lblQuickTaTitle, 0, 0);
            this.gridQuickTa.Controls.Add(this.radioQuickTaAuto, 0, 1);
            this.gridQuickTa.Controls.Add(this.radioQuickTaRetract, 1, 1);
            this.gridQuickTa.Controls.Add(this.radioQuickTaBreak, 2, 1);
            this.gridQuickTa.Controls.Add(this.radioQuickTaPose1, 3, 1);
            this.gridQuickTa.Controls.Add(this.radioQuickTaPose2, 0, 2);
            this.gridQuickTa.Controls.Add(this.radioQuickTaZeroPose, 1, 2);
            this.gridQuickTa.Controls.Add(this.lblTaForce, 0, 3);
            this.gridQuickTa.Controls.Add(this.txtTaForce, 1, 3);
            this.gridQuickTa.Controls.Add(this.lblTaBreakPos, 2, 3);
            this.gridQuickTa.Controls.Add(this.txtTaBreakPos, 3, 3);
            this.gridQuickTa.Controls.Add(this.lblTaPose1, 0, 4);
            this.gridQuickTa.Controls.Add(this.txtTaPose1, 1, 4);
            this.gridQuickTa.Controls.Add(this.lblTaPose2, 2, 4);
            this.gridQuickTa.Controls.Add(this.txtTaPose2, 3, 4);
            this.gridQuickTa.Controls.Add(this.lblTaZeroPose, 0, 5);
            this.gridQuickTa.Controls.Add(this.txtTaZeroPose, 1, 5);
            this.gridQuickTa.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickTa.Location = new System.Drawing.Point(9, 9);
            this.gridQuickTa.Name = "gridQuickTa";
            this.gridQuickTa.RowCount = 7;
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
            // lblQuickTaTitle
            // 
            this.lblQuickTaTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.gridQuickTa.SetColumnSpan(this.lblQuickTaTitle, 4);
            this.lblQuickTaTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblQuickTaTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblQuickTaTitle.Location = new System.Drawing.Point(0, 0);
            this.lblQuickTaTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblQuickTaTitle.Name = "lblQuickTaTitle";
            this.lblQuickTaTitle.Size = new System.Drawing.Size(856, 28);
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
            this.radioQuickTaAuto.Location = new System.Drawing.Point(0, 28);
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
            this.radioQuickTaRetract.Location = new System.Drawing.Point(214, 28);
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
            this.radioQuickTaBreak.Location = new System.Drawing.Point(428, 28);
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
            this.radioQuickTaPose1.Location = new System.Drawing.Point(642, 28);
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
            this.radioQuickTaPose2.Location = new System.Drawing.Point(0, 56);
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
            this.radioQuickTaZeroPose.Location = new System.Drawing.Point(214, 56);
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
            this.lblTaForce.Location = new System.Drawing.Point(3, 87);
            this.lblTaForce.Name = "lblTaForce";
            this.lblTaForce.Size = new System.Drawing.Size(208, 22);
            this.lblTaForce.TabIndex = 11;
            this.lblTaForce.Text = "TA_Force 下压力大小(N)：";
            // 
            // txtTaForce
            // 
            this.txtTaForce.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaForce.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaForce.Location = new System.Drawing.Point(217, 87);
            this.txtTaForce.Name = "txtTaForce";
            this.txtTaForce.Size = new System.Drawing.Size(208, 22);
            this.txtTaForce.TabIndex = 12;
            this.txtTaForce.Text = "0";
            // 
            // lblTaBreakPos
            // 
            this.lblTaBreakPos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaBreakPos.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaBreakPos.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaBreakPos.Location = new System.Drawing.Point(431, 87);
            this.lblTaBreakPos.Name = "lblTaBreakPos";
            this.lblTaBreakPos.Size = new System.Drawing.Size(208, 22);
            this.lblTaBreakPos.TabIndex = 14;
            this.lblTaBreakPos.Text = "TA_Break 停止位：";
            // 
            // txtTaBreakPos
            // 
            this.txtTaBreakPos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaBreakPos.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaBreakPos.Location = new System.Drawing.Point(645, 87);
            this.txtTaBreakPos.Name = "txtTaBreakPos";
            this.txtTaBreakPos.ReadOnly = true;
            this.txtTaBreakPos.Size = new System.Drawing.Size(208, 22);
            this.txtTaBreakPos.TabIndex = 15;
            this.txtTaBreakPos.Text = "--";
            // 
            // lblTaPose1
            // 
            this.lblTaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaPose1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaPose1.Location = new System.Drawing.Point(3, 115);
            this.lblTaPose1.Name = "lblTaPose1";
            this.lblTaPose1.Size = new System.Drawing.Size(208, 22);
            this.lblTaPose1.TabIndex = 17;
            this.lblTaPose1.Text = "TA_Pose1：";
            // 
            // txtTaPose1
            // 
            this.txtTaPose1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaPose1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaPose1.Location = new System.Drawing.Point(217, 115);
            this.txtTaPose1.Name = "txtTaPose1";
            this.txtTaPose1.Size = new System.Drawing.Size(208, 22);
            this.txtTaPose1.TabIndex = 18;
            this.txtTaPose1.Text = "0";
            // 
            // lblTaPose2
            // 
            this.lblTaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaPose2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaPose2.Location = new System.Drawing.Point(431, 115);
            this.lblTaPose2.Name = "lblTaPose2";
            this.lblTaPose2.Size = new System.Drawing.Size(208, 22);
            this.lblTaPose2.TabIndex = 20;
            this.lblTaPose2.Text = "TA_Pose2：";
            // 
            // txtTaPose2
            // 
            this.txtTaPose2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaPose2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaPose2.Location = new System.Drawing.Point(645, 115);
            this.txtTaPose2.Name = "txtTaPose2";
            this.txtTaPose2.Size = new System.Drawing.Size(208, 22);
            this.txtTaPose2.TabIndex = 21;
            this.txtTaPose2.Text = "0";
            // 
            // lblTaZeroPose
            // 
            this.lblTaZeroPose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaZeroPose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTaZeroPose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTaZeroPose.Location = new System.Drawing.Point(3, 143);
            this.lblTaZeroPose.Name = "lblTaZeroPose";
            this.lblTaZeroPose.Size = new System.Drawing.Size(208, 22);
            this.lblTaZeroPose.TabIndex = 24;
            this.lblTaZeroPose.Text = "TA_ZeroPose：";
            // 
            // txtTaZeroPose
            // 
            this.txtTaZeroPose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaZeroPose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtTaZeroPose.Location = new System.Drawing.Point(217, 143);
            this.txtTaZeroPose.Name = "txtTaZeroPose";
            this.txtTaZeroPose.Size = new System.Drawing.Size(208, 22);
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
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoArc, 3, 1);
            this.gridQuickLa.Controls.Add(this.lblQuickLaTitle, 0, 0);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoLine, 2, 1);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoEight, 1, 1);
            this.gridQuickLa.Controls.Add(this.radioQuickLaGalvoCircle, 0, 1);
            this.gridQuickLa.Controls.Add(this.selGalvoPattern, 1, 3);
            this.gridQuickLa.Controls.Add(this.lblGalvoPattern, 0, 3);
            this.gridQuickLa.Controls.Add(this.lblGalvoLength, 0, 4);
            this.gridQuickLa.Controls.Add(this.txtGalvoLength, 1, 4);
            this.gridQuickLa.Controls.Add(this.lblGalvoWidth, 2, 3);
            this.gridQuickLa.Controls.Add(this.txtGalvoWidth, 3, 3);
            this.gridQuickLa.Controls.Add(this.lblGalvoFreq, 2, 4);
            this.gridQuickLa.Controls.Add(this.txtGalvoFreq, 3, 4);
            this.gridQuickLa.Controls.Add(this.lblManualWidth, 0, 5);
            this.gridQuickLa.Controls.Add(this.txtManualWidth, 1, 5);
            this.gridQuickLa.Controls.Add(this.btnGalvoDemo, 0, 6);
            this.gridQuickLa.Controls.Add(this.btnGalvoHome, 1, 6);
            this.gridQuickLa.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridQuickLa.Location = new System.Drawing.Point(9, 9);
            this.gridQuickLa.Name = "gridQuickLa";
            this.gridQuickLa.RowCount = 10;
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickLa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridQuickLa.Size = new System.Drawing.Size(858, 396);
            this.gridQuickLa.TabIndex = 11;
            // 
            // radioQuickLaGalvoArc
            // 
            this.radioQuickLaGalvoArc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioQuickLaGalvoArc.Fill = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(196)))), ((int)(((byte)(26)))));
            this.radioQuickLaGalvoArc.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.radioQuickLaGalvoArc.ForeColor = System.Drawing.Color.Black;
            this.radioQuickLaGalvoArc.Location = new System.Drawing.Point(642, 28);
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
            this.lblQuickLaTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblQuickLaTitle.Location = new System.Drawing.Point(0, 0);
            this.lblQuickLaTitle.Margin = new System.Windows.Forms.Padding(0);
            this.lblQuickLaTitle.Name = "lblQuickLaTitle";
            this.lblQuickLaTitle.Size = new System.Drawing.Size(858, 28);
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
            this.radioQuickLaGalvoLine.Location = new System.Drawing.Point(428, 28);
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
            this.radioQuickLaGalvoEight.Location = new System.Drawing.Point(214, 28);
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
            this.radioQuickLaGalvoCircle.Location = new System.Drawing.Point(0, 28);
            this.radioQuickLaGalvoCircle.Margin = new System.Windows.Forms.Padding(0);
            this.radioQuickLaGalvoCircle.Name = "radioQuickLaGalvoCircle";
            this.radioQuickLaGalvoCircle.Size = new System.Drawing.Size(214, 28);
            this.radioQuickLaGalvoCircle.TabIndex = 20;
            this.radioQuickLaGalvoCircle.Text = "GA_Circle";
            this.radioQuickLaGalvoCircle.CheckedChanged += new AntdUI.BoolEventHandler(this.radioQuickLa_CheckedChanged);
            // 
            // selGalvoPattern
            // 
            this.selGalvoPattern.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selGalvoPattern.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.selGalvoPattern.Items.AddRange(new object[] {
            "圆",
            "八字",
            "直线",
            "圆弧"});
            this.selGalvoPattern.Location = new System.Drawing.Point(214, 84);
            this.selGalvoPattern.Margin = new System.Windows.Forms.Padding(0);
            this.selGalvoPattern.Name = "selGalvoPattern";
            this.selGalvoPattern.Size = new System.Drawing.Size(214, 28);
            this.selGalvoPattern.TabIndex = 10;
            this.selGalvoPattern.SelectedIndexChanged += new AntdUI.IntEventHandler(this.selGalvoPattern_SelectedIndexChanged);
            // 
            // lblGalvoPattern
            // 
            this.lblGalvoPattern.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoPattern.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoPattern.Location = new System.Drawing.Point(0, 84);
            this.lblGalvoPattern.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoPattern.Name = "lblGalvoPattern";
            this.lblGalvoPattern.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoPattern.TabIndex = 10;
            this.lblGalvoPattern.Text = "振镜图形";
            this.lblGalvoPattern.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblGalvoLength
            // 
            this.lblGalvoLength.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoLength.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoLength.Location = new System.Drawing.Point(0, 112);
            this.lblGalvoLength.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoLength.Name = "lblGalvoLength";
            this.lblGalvoLength.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoLength.TabIndex = 10;
            this.lblGalvoLength.Text = "长度(mm)";
            this.lblGalvoLength.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtGalvoLength
            // 
            this.txtGalvoLength.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGalvoLength.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGalvoLength.Location = new System.Drawing.Point(217, 115);
            this.txtGalvoLength.Name = "txtGalvoLength";
            this.txtGalvoLength.Size = new System.Drawing.Size(208, 22);
            this.txtGalvoLength.TabIndex = 10;
            this.txtGalvoLength.Text = "1.000";
            // 
            // lblGalvoWidth
            // 
            this.lblGalvoWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoWidth.Location = new System.Drawing.Point(428, 84);
            this.lblGalvoWidth.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoWidth.Name = "lblGalvoWidth";
            this.lblGalvoWidth.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoWidth.TabIndex = 10;
            this.lblGalvoWidth.Text = "宽度(mm)";
            this.lblGalvoWidth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtGalvoWidth
            // 
            this.txtGalvoWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGalvoWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGalvoWidth.Location = new System.Drawing.Point(645, 87);
            this.txtGalvoWidth.Name = "txtGalvoWidth";
            this.txtGalvoWidth.Size = new System.Drawing.Size(210, 22);
            this.txtGalvoWidth.TabIndex = 10;
            this.txtGalvoWidth.Text = "1.000";
            // 
            // lblGalvoFreq
            // 
            this.lblGalvoFreq.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGalvoFreq.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblGalvoFreq.Location = new System.Drawing.Point(428, 112);
            this.lblGalvoFreq.Margin = new System.Windows.Forms.Padding(0);
            this.lblGalvoFreq.Name = "lblGalvoFreq";
            this.lblGalvoFreq.Size = new System.Drawing.Size(214, 28);
            this.lblGalvoFreq.TabIndex = 10;
            this.lblGalvoFreq.Text = "频率(Hz)";
            this.lblGalvoFreq.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtGalvoFreq
            // 
            this.txtGalvoFreq.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtGalvoFreq.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtGalvoFreq.Location = new System.Drawing.Point(645, 115);
            this.txtGalvoFreq.Name = "txtGalvoFreq";
            this.txtGalvoFreq.Size = new System.Drawing.Size(210, 22);
            this.txtGalvoFreq.TabIndex = 10;
            this.txtGalvoFreq.Text = "50";
            // 
            // lblManualWidth
            // 
            this.lblManualWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblManualWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblManualWidth.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblManualWidth.Location = new System.Drawing.Point(3, 140);
            this.lblManualWidth.Name = "lblManualWidth";
            this.lblManualWidth.Size = new System.Drawing.Size(208, 28);
            this.lblManualWidth.TabIndex = 11;
            this.lblManualWidth.Text = "手动焊缝宽度(mm)";
            this.lblManualWidth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtManualWidth
            // 
            this.txtManualWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtManualWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.txtManualWidth.Location = new System.Drawing.Point(214, 140);
            this.txtManualWidth.Margin = new System.Windows.Forms.Padding(0);
            this.txtManualWidth.Name = "txtManualWidth";
            this.txtManualWidth.Size = new System.Drawing.Size(214, 28);
            this.txtManualWidth.TabIndex = 12;
            this.txtManualWidth.Text = "1.200";
            this.txtManualWidth.Leave += new System.EventHandler(this.txtManualWidth_Leave);
            // 
            // btnGalvoDemo
            // 
            this.btnGalvoDemo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnGalvoDemo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnGalvoDemo.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnGalvoDemo.Location = new System.Drawing.Point(0, 168);
            this.btnGalvoDemo.Margin = new System.Windows.Forms.Padding(0);
            this.btnGalvoDemo.Name = "btnGalvoDemo";
            this.btnGalvoDemo.Size = new System.Drawing.Size(214, 28);
            this.btnGalvoDemo.TabIndex = 10;
            this.btnGalvoDemo.Text = "手动演示";
            this.btnGalvoDemo.Click += new System.EventHandler(this.btnGalvoDemo_Click);
            // 
            // btnGalvoHome
            // 
            this.btnGalvoHome.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnGalvoHome.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnGalvoHome.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnGalvoHome.Location = new System.Drawing.Point(214, 168);
            this.btnGalvoHome.Margin = new System.Windows.Forms.Padding(0);
            this.btnGalvoHome.Name = "btnGalvoHome";
            this.btnGalvoHome.Size = new System.Drawing.Size(214, 28);
            this.btnGalvoHome.TabIndex = 10;
            this.btnGalvoHome.Text = "回到零位";
            this.btnGalvoHome.Click += new System.EventHandler(this.btnGalvoHome_Click);
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
            // tabPageAdaptiveSetup
            // 
            this.tabPageAdaptiveSetup.Controls.Add(this.panelWeldList);
            this.tabPageAdaptiveSetup.Location = new System.Drawing.Point(-3524, -1762);
            this.tabPageAdaptiveSetup.Name = "tabPageAdaptiveSetup";
            this.tabPageAdaptiveSetup.Size = new System.Drawing.Size(1762, 881);
            this.tabPageAdaptiveSetup.TabIndex = 0;
            this.tabPageAdaptiveSetup.Text = "自适应工艺参数设置";
            // 
            // label3
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.label3, 4);
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label3.Location = new System.Drawing.Point(3, 31);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(850, 22);
            this.label3.TabIndex = 15;
            this.label3.Text = "焊丝工艺参数";
            // 
            // ProcessControlPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tabsProcess);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Name = "ProcessControlPage";
            this.Size = new System.Drawing.Size(1762, 920);
            this.Load += new System.EventHandler(this.ParamAutoPage_Shown);
            //
            // timerRefresh
            //
            this.timerRefresh.Interval = 500;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            this.panelWeldList.ResumeLayout(false);
            this.gridWeldZones.ResumeLayout(false);
            this.panelConfigList.ResumeLayout(false);
            this.gridConfigList.ResumeLayout(false);
            this.gridPanelRight.ResumeLayout(false);
            this.panelAdaparam.ResumeLayout(false);
            this.gridAdaparam.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.gridPanel1.ResumeLayout(false);
            this.flowPanel1.ResumeLayout(false);
            this.tabsProcess.ResumeLayout(false);
            this.tabPageQuickSetup.ResumeLayout(false);
            this.gridQuickZones.ResumeLayout(false);
            this.panelProcessDebug.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.panelQuickTracking.ResumeLayout(false);
            this.gridQuickTracking.ResumeLayout(false);
            this.panelQuickTa.ResumeLayout(false);
            this.gridQuickTa.ResumeLayout(false);
            this.panelQuickLa.ResumeLayout(false);
            this.gridQuickLa.ResumeLayout(false);
            this.flowPanel2.ResumeLayout(false);
            this.tabPageAdaptiveSetup.ResumeLayout(false);
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
        private AntdUI.Select selParamOutputHz;
        private AntdUI.Label lblParamOutputHz;
        private AntdUI.Input txtParamSpotGain;
        private AntdUI.Label lblParamSpotGain;
        private AntdUI.Input txtParamManualWidth;
        private AntdUI.Label lblParamManualWidth;
        private AntdUI.Label lblBaseTitle;
        private AntdUI.Tabs tabsProcess;
        private AntdUI.TabPage tabPageAdaptiveSetup;
        private AntdUI.Label label1;
        private AntdUI.Button btnSaveAutoParam;
        private AntdUI.Button btnReadAutoParam;
        private AntdUI.Label lblGeoAxisPerLaser;
        private AntdUI.Label lblCalibCenterY;
        private AntdUI.Input txtCalibCenterY;
        private AntdUI.Label lblCalibCenterZ;
        private AntdUI.Input txtCalibCenterZ;
        private AntdUI.Label lblTeachLockY;
        private AntdUI.Input txtTeachLockY;
        private AntdUI.Button btnSaveWeldPonts;
        private AntdUI.Button btnReadWeldPonts;
        private AntdUI.Button btnApplyLaserCalib;
        private AntdUI.Input txtGeoAxisPerLaser;
        private AntdUI.Label lblGeoGuideSign;
        private AntdUI.Select selGeoGuideSign;
        private AntdUI.Label lblGeoLeadDistance;
        private AntdUI.Input txtGeoLeadDistance;
        private AntdUI.Label lblGeoRobotYGain;
        private AntdUI.Input txtGeoRobotYGain;
        private AntdUI.Label lblGeoYAxisMin;
        private AntdUI.Input txtGeoYAxisMin;
        private AntdUI.Label lblGeoYAxisMax;
        private AntdUI.Input txtGeoYAxisMax;

        private AntdUI.Label lblFlowMaxConsecFail;
        private AntdUI.Input txtFlowMaxConsecFail;
        private AntdUI.Label lblFlowCountNum;
        private AntdUI.Label lblNoValueAlarmHint;
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
        private System.Windows.Forms.Label lblManualWidth;
        private AntdUI.Input txtManualWidth;
        private AntdUI.Radio radioQuickYAuto;
        private AntdUI.Radio radioQuickYForceCalibPose;
        private AntdUI.Radio radioQuickYForceHome;
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
        private AntdUI.Radio radioQuickLaPose2;
        private System.Windows.Forms.Label lblQuickLaTitle;
        private AntdUI.Panel panelQuickLa;
        private System.Windows.Forms.Label lblLaSpot1;
        private AntdUI.Input txtLaSpot1;
        private System.Windows.Forms.Label lblLaSpot2;
        private AntdUI.Input txtLaSpot2;
        private System.Windows.Forms.Label lblLaPose1;
        private AntdUI.Input txtLaPose1;
        private System.Windows.Forms.Label lblLaPose2;
        private AntdUI.Input txtLaPose2;
        private System.Windows.Forms.Label lblSpotTarget;
        private AntdUI.Input txtLaSpotTarget;
        private System.Windows.Forms.Label lblGalvoPattern;
        private AntdUI.Select selGalvoPattern;
        private System.Windows.Forms.Label lblGalvoWidth;
        private AntdUI.Input txtGalvoWidth;
        private System.Windows.Forms.Label lblGalvoLength;
        private AntdUI.Input txtGalvoLength;
        private System.Windows.Forms.Label lblGalvoFreq;
        private AntdUI.Input txtGalvoFreq;
        private AntdUI.Button btnGalvoDemo;
        private AntdUI.Button btnGalvoHome;
        private AntdUI.Label lblConfigListTitle;
        private AntdUI.FlowPanel flowPanel1;
        private AntdUI.FlowPanel flowPanel2;
        private System.Windows.Forms.TableLayoutPanel gridPanelRight;
        private AntdUI.Panel panel2;
        private System.Windows.Forms.TableLayoutPanel gridPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label label2;
        private AntdUI.Label label3;
        private System.Windows.Forms.Timer timerRefresh;
    }
}
