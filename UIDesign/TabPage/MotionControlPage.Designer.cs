namespace AdaWeldSystem.Sub2UI
{
    partial class MotionControlPage
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
            this.gridMain = new AntdUI.GridPanel();
            this.panelAxis = new AntdUI.Panel();
            this.gridAxisInner = new AntdUI.GridPanel();
            this.lblAxisPanelTitle = new AntdUI.Label();
            this.gridRow0 = new AntdUI.GridPanel();
            this.panel1 = new AntdUI.Panel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.cmbProfile = new AntdUI.Select();
            this.chkRandom = new AntdUI.Checkbox();
            this.label1 = new AntdUI.Label();
            this.lblProfile = new AntdUI.Label();
            this.lblTargetDist = new AntdUI.Label();
            this.lblSpeed = new AntdUI.Label();
            this.lblSimMode = new AntdUI.Label();
            this.txtTargetDist = new AntdUI.Input();
            this.txtStartX = new AntdUI.Input();
            this.lblStartX = new AntdUI.Label();
            this.txtSpeed = new AntdUI.Input();
            this.btnStartSim = new AntdUI.Button();
            this.cmbSimMode = new AntdUI.Select();
            this.btnSaveSim = new AntdUI.Button();
            this.panelCalib = new AntdUI.Panel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnSaveCalib = new AntdUI.Button();
            this.lblCalibTitle = new AntdUI.Label();
            this.txtFrontOffset = new AntdUI.Input();
            this.lblApproach = new AntdUI.Label();
            this.lblFrontOffset = new AntdUI.Label();
            this.txtApproach = new AntdUI.Input();
            this.txtAngle = new AntdUI.Input();
            this.lblCenterY = new AntdUI.Label();
            this.lblAngle = new AntdUI.Label();
            this.txtCenterY = new AntdUI.Input();
            this.txtScaleX = new AntdUI.Input();
            this.lblCenterX = new AntdUI.Label();
            this.lblScaleX = new AntdUI.Label();
            this.txtCenterX = new AntdUI.Input();
            this.gridMain.SuspendLayout();
            this.panelAxis.SuspendLayout();
            this.gridAxisInner.SuspendLayout();
            this.gridRow0.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.panelCalib.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // gridMain
            // 
            this.gridMain.Controls.Add(this.panelAxis);
            this.gridMain.Controls.Add(this.gridRow0);
            this.gridMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridMain.Gap = 10;
            this.gridMain.Location = new System.Drawing.Point(0, 0);
            this.gridMain.Name = "gridMain";
            this.gridMain.Padding = new System.Windows.Forms.Padding(10);
            this.gridMain.Size = new System.Drawing.Size(960, 743);
            this.gridMain.Span = "60%:fill;40%:fill";
            this.gridMain.TabIndex = 0;
            // 
            // panelAxis
            // 
            this.panelAxis.Back = System.Drawing.Color.White;
            this.panelAxis.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelAxis.Controls.Add(this.gridAxisInner);
            this.panelAxis.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelAxis.Location = new System.Drawing.Point(25, 459);
            this.panelAxis.Name = "panelAxis";
            this.panelAxis.Padding = new System.Windows.Forms.Padding(3);
            this.panelAxis.Shadow = 5;
            this.panelAxis.ShadowColor = System.Drawing.Color.Black;
            this.panelAxis.ShadowOpacity = 0.2F;
            this.panelAxis.ShadowOpacityAnimation = true;
            this.panelAxis.Size = new System.Drawing.Size(910, 259);
            this.panelAxis.TabIndex = 1;
            // 
            // gridAxisInner
            // 
            this.gridAxisInner.Controls.Add(this.lblAxisPanelTitle);
            this.gridAxisInner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridAxisInner.Location = new System.Drawing.Point(9, 9);
            this.gridAxisInner.Name = "gridAxisInner";
            this.gridAxisInner.Size = new System.Drawing.Size(892, 241);
            this.gridAxisInner.Span = "32:fill;fill";
            this.gridAxisInner.TabIndex = 0;
            // 
            // lblAxisPanelTitle
            // 
            this.lblAxisPanelTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAxisPanelTitle.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblAxisPanelTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblAxisPanelTitle.Location = new System.Drawing.Point(3, 3);
            this.lblAxisPanelTitle.Name = "lblAxisPanelTitle";
            this.lblAxisPanelTitle.Size = new System.Drawing.Size(886, 34);
            this.lblAxisPanelTitle.TabIndex = 0;
            this.lblAxisPanelTitle.Text = "运动轴参数";
            // 
            // gridRow0
            // 
            this.gridRow0.Controls.Add(this.panel1);
            this.gridRow0.Controls.Add(this.panelCalib);
            this.gridRow0.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridRow0.Gap = 10;
            this.gridRow0.Location = new System.Drawing.Point(25, 25);
            this.gridRow0.Name = "gridRow0";
            this.gridRow0.Size = new System.Drawing.Size(910, 404);
            this.gridRow0.Span = "50% 50%";
            this.gridRow0.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.Back = System.Drawing.Color.White;
            this.panel1.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panel1.Controls.Add(this.tableLayoutPanel2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(470, 15);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(3);
            this.panel1.Shadow = 5;
            this.panel1.ShadowColor = System.Drawing.Color.Black;
            this.panel1.ShadowOpacity = 0.2F;
            this.panel1.ShadowOpacityAnimation = true;
            this.panel1.Size = new System.Drawing.Size(425, 374);
            this.panel1.TabIndex = 1;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoScroll = true;
            this.tableLayoutPanel2.ColumnCount = 5;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this.cmbProfile, 2, 6);
            this.tableLayoutPanel2.Controls.Add(this.chkRandom, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.lblProfile, 1, 6);
            this.tableLayoutPanel2.Controls.Add(this.lblTargetDist, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.lblSpeed, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this.lblSimMode, 1, 5);
            this.tableLayoutPanel2.Controls.Add(this.txtTargetDist, 2, 2);
            this.tableLayoutPanel2.Controls.Add(this.txtStartX, 2, 4);
            this.tableLayoutPanel2.Controls.Add(this.lblStartX, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this.txtSpeed, 2, 3);
            this.tableLayoutPanel2.Controls.Add(this.btnStartSim, 3, 8);
            this.tableLayoutPanel2.Controls.Add(this.cmbSimMode, 2, 5);
            this.tableLayoutPanel2.Controls.Add(this.btnSaveSim, 3, 7);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(9, 9);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 10;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(407, 356);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // cmbProfile
            // 
            this.cmbProfile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbProfile.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbProfile.Location = new System.Drawing.Point(223, 213);
            this.cmbProfile.Name = "cmbProfile";
            this.cmbProfile.Size = new System.Drawing.Size(174, 29);
            this.cmbProfile.TabIndex = 1;
            // 
            // chkRandom
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.chkRandom, 2);
            this.chkRandom.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkRandom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkRandom.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkRandom.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkRandom.Location = new System.Drawing.Point(43, 38);
            this.chkRandom.Name = "chkRandom";
            this.chkRandom.Size = new System.Drawing.Size(354, 29);
            this.chkRandom.TabIndex = 6;
            this.chkRandom.Text = "模拟数据随机模式";
            // 
            // label1
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.label1, 3);
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.label1.Location = new System.Drawing.Point(3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(394, 29);
            this.label1.TabIndex = 1;
            this.label1.Text = "模拟测试数据";
            // 
            // lblProfile
            // 
            this.lblProfile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblProfile.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblProfile.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblProfile.Location = new System.Drawing.Point(43, 213);
            this.lblProfile.Name = "lblProfile";
            this.lblProfile.Size = new System.Drawing.Size(174, 29);
            this.lblProfile.TabIndex = 0;
            this.lblProfile.Text = "模拟焊缝轮廓类型";
            // 
            // lblTargetDist
            // 
            this.lblTargetDist.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTargetDist.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTargetDist.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTargetDist.Location = new System.Drawing.Point(43, 73);
            this.lblTargetDist.Name = "lblTargetDist";
            this.lblTargetDist.Size = new System.Drawing.Size(174, 29);
            this.lblTargetDist.TabIndex = 5;
            this.lblTargetDist.Text = "模拟目标距离(mm)";
            // 
            // lblSpeed
            // 
            this.lblSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSpeed.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSpeed.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSpeed.Location = new System.Drawing.Point(43, 108);
            this.lblSpeed.Name = "lblSpeed";
            this.lblSpeed.Size = new System.Drawing.Size(174, 29);
            this.lblSpeed.TabIndex = 4;
            this.lblSpeed.Text = "模拟运动速度(mm/s)";
            // 
            // lblSimMode
            // 
            this.lblSimMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSimMode.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSimMode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSimMode.Location = new System.Drawing.Point(43, 178);
            this.lblSimMode.Name = "lblSimMode";
            this.lblSimMode.Size = new System.Drawing.Size(174, 29);
            this.lblSimMode.TabIndex = 2;
            this.lblSimMode.Text = "模拟测试AB模式";
            // 
            // txtTargetDist
            // 
            this.txtTargetDist.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtTargetDist.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTargetDist.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTargetDist.Location = new System.Drawing.Point(223, 73);
            this.txtTargetDist.Name = "txtTargetDist";
            this.txtTargetDist.Size = new System.Drawing.Size(174, 29);
            this.txtTargetDist.TabIndex = 5;
            this.txtTargetDist.Text = "0.000";
            // 
            // txtStartX
            // 
            this.txtStartX.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtStartX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtStartX.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtStartX.Location = new System.Drawing.Point(223, 143);
            this.txtStartX.Name = "txtStartX";
            this.txtStartX.Size = new System.Drawing.Size(174, 29);
            this.txtStartX.TabIndex = 3;
            this.txtStartX.Text = "0.000";
            // 
            // lblStartX
            // 
            this.lblStartX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStartX.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblStartX.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblStartX.Location = new System.Drawing.Point(43, 143);
            this.lblStartX.Name = "lblStartX";
            this.lblStartX.Size = new System.Drawing.Size(174, 29);
            this.lblStartX.TabIndex = 3;
            this.lblStartX.Text = "模拟起点X(mm)";
            // 
            // txtSpeed
            // 
            this.txtSpeed.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtSpeed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSpeed.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtSpeed.Location = new System.Drawing.Point(223, 108);
            this.txtSpeed.Name = "txtSpeed";
            this.txtSpeed.Size = new System.Drawing.Size(174, 29);
            this.txtSpeed.TabIndex = 4;
            this.txtSpeed.Text = "0.000";
            // 
            // btnStartSim
            // 
            this.btnStartSim.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnStartSim.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartSim.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnStartSim.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartSim.Ghost = true;
            this.btnStartSim.IconSvg = "BugOutlined";
            this.btnStartSim.Location = new System.Drawing.Point(403, 283);
            this.btnStartSim.Name = "btnStartSim";
            this.btnStartSim.Size = new System.Drawing.Size(174, 29);
            this.btnStartSim.TabIndex = 8;
            this.btnStartSim.Text = "开启模拟流程";
            this.btnStartSim.Click += new System.EventHandler(this.BtnStartSim_Click);
            // 
            // cmbSimMode
            // 
            this.cmbSimMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbSimMode.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbSimMode.Location = new System.Drawing.Point(223, 178);
            this.cmbSimMode.Name = "cmbSimMode";
            this.cmbSimMode.Size = new System.Drawing.Size(174, 29);
            this.cmbSimMode.TabIndex = 2;
            // 
            // btnSaveSim
            // 
            this.btnSaveSim.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSaveSim.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveSim.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSaveSim.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSaveSim.Ghost = true;
            this.btnSaveSim.IconSvg = "SaveOutlined";
            this.btnSaveSim.Location = new System.Drawing.Point(403, 248);
            this.btnSaveSim.Name = "btnSaveSim";
            this.btnSaveSim.Size = new System.Drawing.Size(174, 29);
            this.btnSaveSim.TabIndex = 7;
            this.btnSaveSim.Text = "保存模拟参数";
            this.btnSaveSim.Click += new System.EventHandler(this.BtnSaveSim_Click);
            // 
            // panelCalib
            // 
            this.panelCalib.Back = System.Drawing.Color.White;
            this.panelCalib.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelCalib.Controls.Add(this.tableLayoutPanel1);
            this.panelCalib.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCalib.Location = new System.Drawing.Point(15, 15);
            this.panelCalib.Name = "panelCalib";
            this.panelCalib.Padding = new System.Windows.Forms.Padding(3);
            this.panelCalib.Shadow = 5;
            this.panelCalib.ShadowColor = System.Drawing.Color.Black;
            this.panelCalib.ShadowOpacity = 0.2F;
            this.panelCalib.ShadowOpacityAnimation = true;
            this.panelCalib.Size = new System.Drawing.Size(425, 374);
            this.panelCalib.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoScroll = true;
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.btnSaveCalib, 3, 7);
            this.tableLayoutPanel1.Controls.Add(this.lblCalibTitle, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.txtFrontOffset, 2, 6);
            this.tableLayoutPanel1.Controls.Add(this.lblApproach, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblFrontOffset, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.txtApproach, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.txtAngle, 2, 5);
            this.tableLayoutPanel1.Controls.Add(this.lblCenterY, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.lblAngle, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.txtCenterY, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.txtScaleX, 2, 4);
            this.tableLayoutPanel1.Controls.Add(this.lblCenterX, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.lblScaleX, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.txtCenterX, 2, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(9, 9);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 9;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(407, 356);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // btnSaveCalib
            // 
            this.btnSaveCalib.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSaveCalib.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveCalib.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSaveCalib.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSaveCalib.Ghost = true;
            this.btnSaveCalib.IconSvg = "SaveOutlined";
            this.btnSaveCalib.Location = new System.Drawing.Point(403, 248);
            this.btnSaveCalib.Name = "btnSaveCalib";
            this.btnSaveCalib.Size = new System.Drawing.Size(174, 29);
            this.btnSaveCalib.TabIndex = 7;
            this.btnSaveCalib.Text = "保存标定参数";
            this.btnSaveCalib.Click += new System.EventHandler(this.BtnSaveCalib_Click);
            // 
            // lblCalibTitle
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.lblCalibTitle, 3);
            this.lblCalibTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCalibTitle.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCalibTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCalibTitle.Location = new System.Drawing.Point(3, 3);
            this.lblCalibTitle.Name = "lblCalibTitle";
            this.lblCalibTitle.Size = new System.Drawing.Size(394, 29);
            this.lblCalibTitle.TabIndex = 0;
            this.lblCalibTitle.Text = "标定数据参数";
            // 
            // txtFrontOffset
            // 
            this.txtFrontOffset.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtFrontOffset.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFrontOffset.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtFrontOffset.Location = new System.Drawing.Point(223, 213);
            this.txtFrontOffset.Name = "txtFrontOffset";
            this.txtFrontOffset.Size = new System.Drawing.Size(174, 29);
            this.txtFrontOffset.TabIndex = 1;
            this.txtFrontOffset.Text = "0.000";
            // 
            // lblApproach
            // 
            this.lblApproach.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblApproach.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblApproach.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblApproach.Location = new System.Drawing.Point(43, 38);
            this.lblApproach.Name = "lblApproach";
            this.lblApproach.Size = new System.Drawing.Size(174, 29);
            this.lblApproach.TabIndex = 6;
            this.lblApproach.Text = "趋近阈值(mm)";
            // 
            // lblFrontOffset
            // 
            this.lblFrontOffset.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFrontOffset.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblFrontOffset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblFrontOffset.Location = new System.Drawing.Point(43, 213);
            this.lblFrontOffset.Name = "lblFrontOffset";
            this.lblFrontOffset.Size = new System.Drawing.Size(174, 29);
            this.lblFrontOffset.TabIndex = 0;
            this.lblFrontOffset.Text = "线激光前置标定距离(mm)";
            // 
            // txtApproach
            // 
            this.txtApproach.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtApproach.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtApproach.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtApproach.Location = new System.Drawing.Point(223, 38);
            this.txtApproach.Name = "txtApproach";
            this.txtApproach.Size = new System.Drawing.Size(174, 29);
            this.txtApproach.TabIndex = 6;
            this.txtApproach.Text = "0.000";
            // 
            // txtAngle
            // 
            this.txtAngle.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtAngle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAngle.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtAngle.Location = new System.Drawing.Point(223, 178);
            this.txtAngle.Name = "txtAngle";
            this.txtAngle.Size = new System.Drawing.Size(174, 29);
            this.txtAngle.TabIndex = 2;
            this.txtAngle.Text = "0.000";
            // 
            // lblCenterY
            // 
            this.lblCenterY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCenterY.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCenterY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCenterY.Location = new System.Drawing.Point(43, 73);
            this.lblCenterY.Name = "lblCenterY";
            this.lblCenterY.Size = new System.Drawing.Size(174, 29);
            this.lblCenterY.TabIndex = 5;
            this.lblCenterY.Text = "标定中心点Y(mm)";
            // 
            // lblAngle
            // 
            this.lblAngle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAngle.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblAngle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblAngle.Location = new System.Drawing.Point(43, 178);
            this.lblAngle.Name = "lblAngle";
            this.lblAngle.Size = new System.Drawing.Size(174, 29);
            this.lblAngle.TabIndex = 2;
            this.lblAngle.Text = "线激光-水平轴角度(°)";
            // 
            // txtCenterY
            // 
            this.txtCenterY.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtCenterY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCenterY.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtCenterY.Location = new System.Drawing.Point(223, 73);
            this.txtCenterY.Name = "txtCenterY";
            this.txtCenterY.Size = new System.Drawing.Size(174, 29);
            this.txtCenterY.TabIndex = 5;
            this.txtCenterY.Text = "0.000";
            // 
            // txtScaleX
            // 
            this.txtScaleX.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtScaleX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtScaleX.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtScaleX.Location = new System.Drawing.Point(223, 143);
            this.txtScaleX.Name = "txtScaleX";
            this.txtScaleX.Size = new System.Drawing.Size(174, 29);
            this.txtScaleX.TabIndex = 3;
            this.txtScaleX.Text = "0.000";
            // 
            // lblCenterX
            // 
            this.lblCenterX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCenterX.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCenterX.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCenterX.Location = new System.Drawing.Point(43, 108);
            this.lblCenterX.Name = "lblCenterX";
            this.lblCenterX.Size = new System.Drawing.Size(174, 29);
            this.lblCenterX.TabIndex = 4;
            this.lblCenterX.Text = "标定中心点X(mm)";
            // 
            // lblScaleX
            // 
            this.lblScaleX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblScaleX.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblScaleX.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblScaleX.Location = new System.Drawing.Point(43, 143);
            this.lblScaleX.Name = "lblScaleX";
            this.lblScaleX.Size = new System.Drawing.Size(174, 29);
            this.lblScaleX.TabIndex = 3;
            this.lblScaleX.Text = "标定系数";
            // 
            // txtCenterX
            // 
            this.txtCenterX.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtCenterX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCenterX.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtCenterX.Location = new System.Drawing.Point(223, 108);
            this.txtCenterX.Name = "txtCenterX";
            this.txtCenterX.Size = new System.Drawing.Size(174, 29);
            this.txtCenterX.TabIndex = 4;
            this.txtCenterX.Text = "0.000";
            // 
            // MotionControlPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.gridMain);
            this.Name = "MotionControlPage";
            this.Size = new System.Drawing.Size(960, 743);
            this.gridMain.ResumeLayout(false);
            this.panelAxis.ResumeLayout(false);
            this.gridAxisInner.ResumeLayout(false);
            this.gridRow0.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.panelCalib.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private AntdUI.GridPanel gridMain;
        private AntdUI.GridPanel gridRow0;
        private AntdUI.Panel panelCalib;
        private AntdUI.Label lblCalibTitle;
        private AntdUI.Label lblFrontOffset;
        private AntdUI.Input txtFrontOffset;
        private AntdUI.Label lblAngle;
        private AntdUI.Input txtAngle;
        private AntdUI.Label lblScaleX;
        private AntdUI.Input txtScaleX;
        private AntdUI.Label lblCenterX;
        private AntdUI.Input txtCenterX;
        private AntdUI.Label lblCenterY;
        private AntdUI.Input txtCenterY;
        private AntdUI.Label lblApproach;
        private AntdUI.Input txtApproach;
        private AntdUI.Button btnSaveCalib;
        private AntdUI.Label lblProfile;
        private AntdUI.Select cmbProfile;
        private AntdUI.Label lblSimMode;
        private AntdUI.Select cmbSimMode;
        private AntdUI.Label lblStartX;
        private AntdUI.Input txtStartX;
        private AntdUI.Label lblSpeed;
        private AntdUI.Input txtSpeed;
        private AntdUI.Label lblTargetDist;
        private AntdUI.Input txtTargetDist;
        private AntdUI.Checkbox chkRandom;
        private AntdUI.Button btnSaveSim;
        private AntdUI.Button btnStartSim;
        private AntdUI.Panel panelAxis;
        private AntdUI.GridPanel gridAxisInner;
        private AntdUI.Label lblAxisPanelTitle;
        private AntdUI.Panel panel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private AntdUI.Label label1;
    }
}
