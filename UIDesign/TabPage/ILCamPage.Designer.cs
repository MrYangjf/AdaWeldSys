namespace AdaWeldSystem.Sub2UI
{
    partial class ILCamPage
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.uiTableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelControl = new AntdUI.Panel();
            this.btnParamEdit = new AntdUI.Button();
            this.btnAlgoEdit = new AntdUI.Button();
            this.StatustabPanel = new System.Windows.Forms.TableLayoutPanel();
            this.lblIP = new AntdUI.Label();
            this.alert_ConnStatus = new AntdUI.Alert();
            this.txtIP = new AntdUI.Input();
            this.btnScan = new AntdUI.Button();
            this.btnConnect = new AntdUI.Button();
            this.divider1 = new AntdUI.Divider();
            this.panel1 = new AntdUI.Panel();
            this.btnLaser = new AntdUI.Button();
            this.btnCaptureCamera = new AntdUI.Button();
            this.btnApplyFps = new AntdUI.Button();
            this.btnApplyJob = new AntdUI.Button();
            this.cmbFps = new AntdUI.Select();
            this.lblAutoDisplay = new AntdUI.Label();
            this.cmbJob = new AntdUI.Select();
            this.lblFps = new AntdUI.Label();
            this.lblJob = new AntdUI.Label();
            this.swDisplay = new AntdUI.Switch();
            this.imageBox = new Emgu.CV.UI.ImageBox();
            this.panelResult = new AntdUI.Panel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.lblSenorfps = new AntdUI.Label();
            this.lblContourPoint = new AntdUI.Label();
            this.dgvResult = new AntdUI.Table();
            this.lblMeasuredFps = new AntdUI.Label();
            this.uiTableLayoutPanel1.SuspendLayout();
            this.panelControl.SuspendLayout();
            this.StatustabPanel.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageBox)).BeginInit();
            this.panelResult.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiTableLayoutPanel1
            // 
            this.uiTableLayoutPanel1.ColumnCount = 2;
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 500F));
            this.uiTableLayoutPanel1.Controls.Add(this.panelControl, 1, 2);
            this.uiTableLayoutPanel1.Controls.Add(this.StatustabPanel, 0, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.panel1, 1, 1);
            this.uiTableLayoutPanel1.Controls.Add(this.imageBox, 0, 1);
            this.uiTableLayoutPanel1.Controls.Add(this.panelResult, 1, 3);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.RowCount = 4;
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 55F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(1200, 700);
            this.uiTableLayoutPanel1.TabIndex = 0;
            // 
            // panelControl
            // 
            this.panelControl.Back = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.panelControl.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelControl.BorderWidth = 1F;
            this.panelControl.Controls.Add(this.btnParamEdit);
            this.panelControl.Controls.Add(this.btnAlgoEdit);
            this.panelControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelControl.Location = new System.Drawing.Point(703, 258);
            this.panelControl.Name = "panelControl";
            this.panelControl.Padding = new System.Windows.Forms.Padding(8);
            this.panelControl.Shadow = 5;
            this.panelControl.Size = new System.Drawing.Size(494, 114);
            this.panelControl.TabIndex = 1;
            // 
            // btnParamEdit
            // 
            this.btnParamEdit.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnParamEdit.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnParamEdit.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnParamEdit.Ghost = true;
            this.btnParamEdit.IconPosition = AntdUI.TAlignMini.Top;
            this.btnParamEdit.IconRatio = 1.5F;
            this.btnParamEdit.IconSvg = "ProfileOutlined";
            this.btnParamEdit.Location = new System.Drawing.Point(358, 24);
            this.btnParamEdit.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnParamEdit.Name = "btnParamEdit";
            this.btnParamEdit.Size = new System.Drawing.Size(120, 71);
            this.btnParamEdit.TabIndex = 10;
            this.btnParamEdit.Text = "相机参数";
            // 
            // btnAlgoEdit
            // 
            this.btnAlgoEdit.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnAlgoEdit.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAlgoEdit.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAlgoEdit.Ghost = true;
            this.btnAlgoEdit.IconPosition = AntdUI.TAlignMini.Top;
            this.btnAlgoEdit.IconRatio = 1.5F;
            this.btnAlgoEdit.IconSvg = "ProfileOutlined";
            this.btnAlgoEdit.Location = new System.Drawing.Point(23, 24);
            this.btnAlgoEdit.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnAlgoEdit.Name = "btnAlgoEdit";
            this.btnAlgoEdit.Size = new System.Drawing.Size(110, 71);
            this.btnAlgoEdit.TabIndex = 8;
            this.btnAlgoEdit.Text = "Job参数";
            // 
            // StatustabPanel
            // 
            this.StatustabPanel.BackColor = System.Drawing.Color.Transparent;
            this.StatustabPanel.ColumnCount = 6;
            this.uiTableLayoutPanel1.SetColumnSpan(this.StatustabPanel, 2);
            this.StatustabPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.StatustabPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.StatustabPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 79F));
            this.StatustabPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 253F));
            this.StatustabPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.StatustabPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.StatustabPanel.Controls.Add(this.lblIP, 2, 0);
            this.StatustabPanel.Controls.Add(this.alert_ConnStatus, 0, 0);
            this.StatustabPanel.Controls.Add(this.txtIP, 3, 0);
            this.StatustabPanel.Controls.Add(this.btnScan, 4, 0);
            this.StatustabPanel.Controls.Add(this.btnConnect, 5, 0);
            this.StatustabPanel.Controls.Add(this.divider1, 0, 1);
            this.StatustabPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StatustabPanel.Location = new System.Drawing.Point(3, 3);
            this.StatustabPanel.Name = "StatustabPanel";
            this.StatustabPanel.RowCount = 2;
            this.StatustabPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.StatustabPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.StatustabPanel.Size = new System.Drawing.Size(1194, 49);
            this.StatustabPanel.TabIndex = 17;
            // 
            // lblIP
            // 
            this.lblIP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblIP.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblIP.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblIP.Location = new System.Drawing.Point(705, 3);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(73, 24);
            this.lblIP.TabIndex = 1;
            this.lblIP.Text = "相机IP";
            // 
            // alert_ConnStatus
            // 
            this.alert_ConnStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.alert_ConnStatus.Icon = AntdUI.TType.Error;
            this.alert_ConnStatus.Location = new System.Drawing.Point(3, 3);
            this.alert_ConnStatus.Name = "alert_ConnStatus";
            this.alert_ConnStatus.Size = new System.Drawing.Size(114, 24);
            this.alert_ConnStatus.TabIndex = 16;
            this.alert_ConnStatus.Text = "未连接";
            // 
            // txtIP
            // 
            this.txtIP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtIP.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtIP.Location = new System.Drawing.Point(781, 0);
            this.txtIP.Margin = new System.Windows.Forms.Padding(0);
            this.txtIP.Name = "txtIP";
            this.txtIP.ReadOnly = true;
            this.txtIP.Size = new System.Drawing.Size(253, 30);
            this.txtIP.TabIndex = 2;
            this.txtIP.Text = "192.168.178.210";
            // 
            // btnScan
            // 
            this.btnScan.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnScan.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnScan.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnScan.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnScan.Ghost = true;
            this.btnScan.IconSvg = "SecurityScanOutlined";
            this.btnScan.Location = new System.Drawing.Point(1034, 0);
            this.btnScan.Margin = new System.Windows.Forms.Padding(0);
            this.btnScan.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(80, 30);
            this.btnScan.TabIndex = 9;
            this.btnScan.Text = "扫描";
            // 
            // btnConnect
            // 
            this.btnConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConnect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnConnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnect.IconSvg = "ApiOutlined";
            this.btnConnect.Location = new System.Drawing.Point(1114, 0);
            this.btnConnect.Margin = new System.Windows.Forms.Padding(0);
            this.btnConnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(80, 30);
            this.btnConnect.TabIndex = 3;
            this.btnConnect.Text = "连接";
            this.btnConnect.Type = AntdUI.TTypeMini.Error;
            // 
            // divider1
            // 
            this.divider1.ColorSplit = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.StatustabPanel.SetColumnSpan(this.divider1, 6);
            this.divider1.Dock = System.Windows.Forms.DockStyle.Top;
            this.divider1.Location = new System.Drawing.Point(0, 30);
            this.divider1.Margin = new System.Windows.Forms.Padding(0);
            this.divider1.Name = "divider1";
            this.divider1.OrientationMargin = 0F;
            this.divider1.Size = new System.Drawing.Size(1194, 19);
            this.divider1.TabIndex = 17;
            this.divider1.Text = "";
            this.divider1.TextPadding = 0.1F;
            // 
            // panel1
            // 
            this.panel1.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panel1.BorderWidth = 1F;
            this.panel1.Controls.Add(this.btnLaser);
            this.panel1.Controls.Add(this.btnCaptureCamera);
            this.panel1.Controls.Add(this.btnApplyFps);
            this.panel1.Controls.Add(this.btnApplyJob);
            this.panel1.Controls.Add(this.cmbFps);
            this.panel1.Controls.Add(this.lblAutoDisplay);
            this.panel1.Controls.Add(this.cmbJob);
            this.panel1.Controls.Add(this.lblFps);
            this.panel1.Controls.Add(this.lblJob);
            this.panel1.Controls.Add(this.swDisplay);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(703, 58);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(8);
            this.panel1.Shadow = 5;
            this.panel1.Size = new System.Drawing.Size(494, 194);
            this.panel1.TabIndex = 18;
            this.panel1.Text = "panel1";
            // 
            // btnLaser
            // 
            this.btnLaser.BackActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnLaser.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnLaser.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLaser.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnLaser.Ghost = true;
            this.btnLaser.IconSvg = "SunFilled";
            this.btnLaser.Location = new System.Drawing.Point(318, 140);
            this.btnLaser.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnLaser.Name = "btnLaser";
            this.btnLaser.Size = new System.Drawing.Size(160, 35);
            this.btnLaser.TabIndex = 4;
            this.btnLaser.Text = "开启激光";
            // 
            // btnCaptureCamera
            // 
            this.btnCaptureCamera.BackActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnCaptureCamera.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnCaptureCamera.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCaptureCamera.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCaptureCamera.Ghost = true;
            this.btnCaptureCamera.IconSvg = "CameraFilled";
            this.btnCaptureCamera.Location = new System.Drawing.Point(16, 140);
            this.btnCaptureCamera.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnCaptureCamera.Name = "btnCaptureCamera";
            this.btnCaptureCamera.Size = new System.Drawing.Size(160, 35);
            this.btnCaptureCamera.TabIndex = 5;
            this.btnCaptureCamera.Text = "开启传感器";
            // 
            // btnApplyFps
            // 
            this.btnApplyFps.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnApplyFps.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnApplyFps.Location = new System.Drawing.Point(375, 53);
            this.btnApplyFps.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnApplyFps.Name = "btnApplyFps";
            this.btnApplyFps.Size = new System.Drawing.Size(95, 34);
            this.btnApplyFps.TabIndex = 15;
            this.btnApplyFps.Text = "应用";
            this.btnApplyFps.Type = AntdUI.TTypeMini.Primary;
            // 
            // btnApplyJob
            // 
            this.btnApplyJob.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnApplyJob.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnApplyJob.Location = new System.Drawing.Point(375, 97);
            this.btnApplyJob.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnApplyJob.Name = "btnApplyJob";
            this.btnApplyJob.Size = new System.Drawing.Size(95, 32);
            this.btnApplyJob.TabIndex = 8;
            this.btnApplyJob.Text = "应用";
            this.btnApplyJob.Type = AntdUI.TTypeMini.Primary;
            // 
            // cmbFps
            // 
            this.cmbFps.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbFps.Location = new System.Drawing.Point(121, 57);
            this.cmbFps.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbFps.MinimumSize = new System.Drawing.Size(63, 0);
            this.cmbFps.Name = "cmbFps";
            this.cmbFps.PlaceholderText = "选择帧率";
            this.cmbFps.Size = new System.Drawing.Size(247, 30);
            this.cmbFps.TabIndex = 14;
            // 
            // lblAutoDisplay
            // 
            this.lblAutoDisplay.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblAutoDisplay.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblAutoDisplay.Location = new System.Drawing.Point(26, 19);
            this.lblAutoDisplay.Name = "lblAutoDisplay";
            this.lblAutoDisplay.Size = new System.Drawing.Size(90, 32);
            this.lblAutoDisplay.TabIndex = 11;
            this.lblAutoDisplay.Text = "显示画面";
            // 
            // cmbJob
            // 
            this.cmbJob.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbJob.Location = new System.Drawing.Point(121, 97);
            this.cmbJob.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbJob.MinimumSize = new System.Drawing.Size(63, 0);
            this.cmbJob.Name = "cmbJob";
            this.cmbJob.PlaceholderText = "选择JOB";
            this.cmbJob.Size = new System.Drawing.Size(247, 30);
            this.cmbJob.TabIndex = 7;
            // 
            // lblFps
            // 
            this.lblFps.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblFps.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblFps.Location = new System.Drawing.Point(26, 57);
            this.lblFps.Name = "lblFps";
            this.lblFps.Size = new System.Drawing.Size(88, 30);
            this.lblFps.TabIndex = 13;
            this.lblFps.Text = "帧率模式";
            // 
            // lblJob
            // 
            this.lblJob.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblJob.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblJob.Location = new System.Drawing.Point(26, 97);
            this.lblJob.Name = "lblJob";
            this.lblJob.Size = new System.Drawing.Size(88, 30);
            this.lblJob.TabIndex = 6;
            this.lblJob.Text = "JOB选择";
            // 
            // swDisplay
            // 
            this.swDisplay.Location = new System.Drawing.Point(122, 21);
            this.swDisplay.Name = "swDisplay";
            this.swDisplay.Size = new System.Drawing.Size(62, 28);
            this.swDisplay.TabIndex = 12;
            // 
            // imageBox
            // 
            this.imageBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.imageBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageBox.FunctionalMode = Emgu.CV.UI.ImageBox.FunctionalModeOption.RightClickMenu;
            this.imageBox.Location = new System.Drawing.Point(3, 58);
            this.imageBox.Name = "imageBox";
            this.uiTableLayoutPanel1.SetRowSpan(this.imageBox, 3);
            this.imageBox.Size = new System.Drawing.Size(694, 639);
            this.imageBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.imageBox.TabIndex = 0;
            this.imageBox.TabStop = false;
            // 
            // panelResult
            // 
            this.panelResult.Back = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.panelResult.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelResult.BorderWidth = 1F;
            this.panelResult.Controls.Add(this.tableLayoutPanel1);
            this.panelResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelResult.Location = new System.Drawing.Point(703, 378);
            this.panelResult.Name = "panelResult";
            this.panelResult.Padding = new System.Windows.Forms.Padding(8);
            this.panelResult.Shadow = 5;
            this.panelResult.Size = new System.Drawing.Size(494, 319);
            this.panelResult.TabIndex = 2;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20.77922F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45.88745F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.Controls.Add(this.lblSenorfps, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.lblContourPoint, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.dgvResult, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblMeasuredFps, 2, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(16, 16);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(462, 287);
            this.tableLayoutPanel1.TabIndex = 4;
            // 
            // lblSenorfps
            // 
            this.lblSenorfps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSenorfps.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lblSenorfps.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSenorfps.Location = new System.Drawing.Point(99, 3);
            this.lblSenorfps.Name = "lblSenorfps";
            this.lblSenorfps.Size = new System.Drawing.Size(206, 24);
            this.lblSenorfps.TabIndex = 4;
            this.lblSenorfps.Text = "传感器帧率：0.0 Hz";
            // 
            // lblContourPoint
            // 
            this.lblContourPoint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblContourPoint.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lblContourPoint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblContourPoint.Location = new System.Drawing.Point(3, 3);
            this.lblContourPoint.Name = "lblContourPoint";
            this.lblContourPoint.Size = new System.Drawing.Size(90, 24);
            this.lblContourPoint.TabIndex = 0;
            this.lblContourPoint.Text = "轮廓点：0";
            // 
            // dgvResult
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.dgvResult, 3);
            this.dgvResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvResult.Gap = 10;
            this.dgvResult.Gaps = new System.Drawing.Size(10, 10);
            this.dgvResult.Location = new System.Drawing.Point(3, 33);
            this.dgvResult.Name = "dgvResult";
            this.dgvResult.Size = new System.Drawing.Size(456, 251);
            this.dgvResult.TabIndex = 1;
            // 
            // lblMeasuredFps
            // 
            this.lblMeasuredFps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMeasuredFps.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lblMeasuredFps.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblMeasuredFps.Location = new System.Drawing.Point(311, 3);
            this.lblMeasuredFps.Name = "lblMeasuredFps";
            this.lblMeasuredFps.Size = new System.Drawing.Size(148, 24);
            this.lblMeasuredFps.TabIndex = 3;
            this.lblMeasuredFps.Text = "回调帧率：0.0 Hz";
            // 
            // ILCamPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "ILCamPage";
            this.Size = new System.Drawing.Size(1200, 700);
            this.uiTableLayoutPanel1.ResumeLayout(false);
            this.panelControl.ResumeLayout(false);
            this.StatustabPanel.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imageBox)).EndInit();
            this.panelResult.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel uiTableLayoutPanel1;
        private Emgu.CV.UI.ImageBox imageBox;
        private AntdUI.Panel panelControl;
        private AntdUI.Input txtIP;
        private AntdUI.Button btnScan;
        private AntdUI.Button btnConnect;
        private AntdUI.Button btnLaser;
        private AntdUI.Button btnCaptureCamera;
        private AntdUI.Button btnApplyFps;
        private AntdUI.Button btnApplyJob;
        private AntdUI.Select cmbJob;
        private AntdUI.Button btnAlgoEdit;
        private AntdUI.Button btnParamEdit;
        private AntdUI.Panel panelResult;
        private AntdUI.Label lblContourPoint;
        private AntdUI.Label lblMeasuredFps;
        private AntdUI.Table dgvResult;
        private AntdUI.Switch swDisplay;
        private AntdUI.Label lblAutoDisplay;
        private AntdUI.Select cmbFps;
        private AntdUI.Label lblFps;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private AntdUI.Alert alert_ConnStatus;
        private System.Windows.Forms.TableLayoutPanel StatustabPanel;
        private AntdUI.Label lblJob;
        private AntdUI.Label lblIP;
        private AntdUI.Panel panel1;
        private AntdUI.Divider divider1;
        private AntdUI.Label lblSenorfps;
    }
}
