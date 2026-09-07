namespace AdaWeldSystem.Sub1UI
{
    partial class DataReviewerPage
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
            this.panelRight = new AntdUI.Panel();
            this.uiContextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.SetRange = new System.Windows.Forms.ToolStripMenuItem();
            this.uiGridPanelRight = new AntdUI.GridPanel();
            this.btnRenerView = new AntdUI.Button();
            this.uiTrackBar1 = new AntdUI.Slider();
            this.uiSymbolLabel1 = new AntdUI.Label();
            this.formsPlotData = new ScottPlot.FormsPlot();
            this.uiFlowLayoutPanel1 = new AntdUI.FlowPanel();
            this.chkSeamWidth = new AntdUI.Checkbox();
            this.chkRobotSpeed = new AntdUI.Checkbox();
            this.chkFeedSpeed = new AntdUI.Checkbox();
            this.chkLaserPower = new AntdUI.Checkbox();
            this.uiDataGridView1 = new AntdUI.Table();
            this.uiContextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ShowData = new System.Windows.Forms.ToolStripMenuItem();
            this.pagination1 = new AntdUI.Pagination();
            this.panelLeft = new AntdUI.Panel();
            this.divider2 = new AntdUI.Divider();
            this.uiCalendar1 = new AntdUI.DatePicker();
            this.BtnRecordSetting = new AntdUI.Button();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.panelRight.SuspendLayout();
            this.uiContextMenuStrip2.SuspendLayout();
            this.uiGridPanelRight.SuspendLayout();
            this.uiFlowLayoutPanel1.SuspendLayout();
            this.uiContextMenuStrip1.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelRight
            // 
            this.panelRight.Back = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.panelRight.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelRight.ContextMenuStrip = this.uiContextMenuStrip2;
            this.panelRight.Controls.Add(this.uiGridPanelRight);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(253, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Padding = new System.Windows.Forms.Padding(5);
            this.panelRight.Shadow = 5;
            this.panelRight.ShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.panelRight.ShadowOpacity = 0.2F;
            this.panelRight.ShadowOpacityAnimation = true;
            this.panelRight.Size = new System.Drawing.Size(996, 564);
            this.panelRight.TabIndex = 3;
            this.panelRight.Text = "panelRight";
            // 
            // uiContextMenuStrip2
            // 
            this.uiContextMenuStrip2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(249)))), ((int)(((byte)(255)))));
            this.uiContextMenuStrip2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiContextMenuStrip2.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.uiContextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SetRange});
            this.uiContextMenuStrip2.Name = "uiContextMenuStrip1";
            this.uiContextMenuStrip2.Size = new System.Drawing.Size(199, 28);
            // 
            // SetRange
            // 
            this.SetRange.Name = "SetRange";
            this.SetRange.Size = new System.Drawing.Size(198, 24);
            this.SetRange.Text = "设置显示区间";
            this.SetRange.Click += new System.EventHandler(this.SetRange_Click);
            // 
            // uiGridPanelRight
            // 
            this.uiGridPanelRight.Controls.Add(this.btnRenerView);
            this.uiGridPanelRight.Controls.Add(this.uiTrackBar1);
            this.uiGridPanelRight.Controls.Add(this.uiSymbolLabel1);
            this.uiGridPanelRight.Controls.Add(this.formsPlotData);
            this.uiGridPanelRight.Controls.Add(this.uiFlowLayoutPanel1);
            this.uiGridPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiGridPanelRight.Location = new System.Drawing.Point(11, 11);
            this.uiGridPanelRight.Name = "uiGridPanelRight";
            this.uiGridPanelRight.Size = new System.Drawing.Size(974, 542);
            this.uiGridPanelRight.Span = "fill;fill;fill;fill 120;-35 fill 35 35";
            this.uiGridPanelRight.TabIndex = 0;
            // 
            // btnRenerView
            // 
            this.btnRenerView.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnRenerView.Font = new System.Drawing.Font("微软雅黑", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRenerView.Ghost = true;
            this.btnRenerView.IconRatio = 1F;
            this.btnRenerView.IconSvg = "YoutubeOutlined";
            this.btnRenerView.Location = new System.Drawing.Point(827, 501);
            this.btnRenerView.Name = "btnRenerView";
            this.btnRenerView.Size = new System.Drawing.Size(144, 38);
            this.btnRenerView.TabIndex = 14;
            this.btnRenerView.Text = "3D数据回溯";
            this.btnRenerView.Click += new System.EventHandler(this.btnRenerView_Click);
            // 
            // uiTrackBar1
            // 
            this.uiTrackBar1.FillHover = System.Drawing.Color.FromArgb(((int)(((byte)(5)))), ((int)(((byte)(99)))), ((int)(((byte)(232)))));
            this.uiTrackBar1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiTrackBar1.Location = new System.Drawing.Point(3, 501);
            this.uiTrackBar1.Name = "uiTrackBar1";
            this.uiTrackBar1.ShowValue = true;
            this.uiTrackBar1.Size = new System.Drawing.Size(818, 38);
            this.uiTrackBar1.TabIndex = 4;
            // 
            // uiSymbolLabel1
            // 
            this.uiSymbolLabel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSymbolLabel1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel1.Location = new System.Drawing.Point(3, 457);
            this.uiSymbolLabel1.Name = "uiSymbolLabel1";
            this.uiSymbolLabel1.Size = new System.Drawing.Size(968, 38);
            this.uiSymbolLabel1.TabIndex = 12;
            this.uiSymbolLabel1.Text = "进度：";
            // 
            // formsPlotData
            // 
            this.formsPlotData.BackColor = System.Drawing.Color.White;
            this.formsPlotData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlotData.Location = new System.Drawing.Point(5, 47);
            this.formsPlotData.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.formsPlotData.Name = "formsPlotData";
            this.formsPlotData.Size = new System.Drawing.Size(964, 404);
            this.formsPlotData.TabIndex = 2;
            // 
            // uiFlowLayoutPanel1
            // 
            this.uiFlowLayoutPanel1.Controls.Add(this.chkSeamWidth);
            this.uiFlowLayoutPanel1.Controls.Add(this.chkRobotSpeed);
            this.uiFlowLayoutPanel1.Controls.Add(this.chkFeedSpeed);
            this.uiFlowLayoutPanel1.Controls.Add(this.chkLaserPower);
            this.uiFlowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiFlowLayoutPanel1.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiFlowLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.uiFlowLayoutPanel1.Name = "uiFlowLayoutPanel1";
            this.uiFlowLayoutPanel1.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            this.uiFlowLayoutPanel1.Size = new System.Drawing.Size(968, 38);
            this.uiFlowLayoutPanel1.TabIndex = 13;
            // 
            // chkSeamWidth
            // 
            this.chkSeamWidth.Checked = true;
            this.chkSeamWidth.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSeamWidth.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkSeamWidth.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkSeamWidth.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkSeamWidth.Location = new System.Drawing.Point(407, 5);
            this.chkSeamWidth.Name = "chkSeamWidth";
            this.chkSeamWidth.Size = new System.Drawing.Size(120, 28);
            this.chkSeamWidth.TabIndex = 14;
            this.chkSeamWidth.Text = "焊缝宽度";
            // 
            // chkRobotSpeed
            // 
            this.chkRobotSpeed.Checked = true;
            this.chkRobotSpeed.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkRobotSpeed.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkRobotSpeed.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkRobotSpeed.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkRobotSpeed.Location = new System.Drawing.Point(261, 5);
            this.chkRobotSpeed.Name = "chkRobotSpeed";
            this.chkRobotSpeed.Size = new System.Drawing.Size(140, 28);
            this.chkRobotSpeed.TabIndex = 15;
            this.chkRobotSpeed.Text = "机器人运动速度";
            // 
            // chkFeedSpeed
            // 
            this.chkFeedSpeed.Checked = true;
            this.chkFeedSpeed.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFeedSpeed.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkFeedSpeed.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkFeedSpeed.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkFeedSpeed.Location = new System.Drawing.Point(135, 5);
            this.chkFeedSpeed.Name = "chkFeedSpeed";
            this.chkFeedSpeed.Size = new System.Drawing.Size(120, 28);
            this.chkFeedSpeed.TabIndex = 16;
            this.chkFeedSpeed.Text = "送丝速度";
            // 
            // chkLaserPower
            // 
            this.chkLaserPower.Checked = true;
            this.chkLaserPower.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLaserPower.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkLaserPower.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkLaserPower.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkLaserPower.Location = new System.Drawing.Point(9, 5);
            this.chkLaserPower.Name = "chkLaserPower";
            this.chkLaserPower.Size = new System.Drawing.Size(120, 28);
            this.chkLaserPower.TabIndex = 17;
            this.chkLaserPower.Text = "激光功率";
            // 
            // uiDataGridView1
            // 
            this.uiDataGridView1.ContextMenuStrip = this.uiContextMenuStrip1;
            this.uiDataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiDataGridView1.Font = new System.Drawing.Font("宋体", 12F);
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Location = new System.Drawing.Point(9, 43);
            this.uiDataGridView1.Name = "uiDataGridView1";
            this.uiDataGridView1.Size = new System.Drawing.Size(226, 450);
            this.uiDataGridView1.TabIndex = 9;
            // 
            // uiContextMenuStrip1
            // 
            this.uiContextMenuStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(249)))), ((int)(((byte)(255)))));
            this.uiContextMenuStrip1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiContextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.uiContextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ShowData});
            this.uiContextMenuStrip1.Name = "uiContextMenuStrip1";
            this.uiContextMenuStrip1.Size = new System.Drawing.Size(159, 28);
            // 
            // ShowData
            // 
            this.ShowData.Name = "ShowData";
            this.ShowData.Size = new System.Drawing.Size(158, 24);
            this.ShowData.Text = "显示数据";
            this.ShowData.Click += new System.EventHandler(this.ShowData_Click);
            // 
            // pagination1
            // 
            this.pagination1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pagination1.Font = new System.Drawing.Font("宋体", 10F);
            this.pagination1.Location = new System.Drawing.Point(9, 497);
            this.pagination1.Name = "pagination1";
            this.pagination1.PageSize = 50;
            this.pagination1.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.pagination1.Size = new System.Drawing.Size(226, 24);
            this.pagination1.SizeChangerWidth = 80;
            this.pagination1.TabIndex = 5;
            // 
            // panelLeft
            // 
            this.panelLeft.Back = System.Drawing.Color.White;
            this.panelLeft.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelLeft.Controls.Add(this.uiDataGridView1);
            this.panelLeft.Controls.Add(this.divider2);
            this.panelLeft.Controls.Add(this.pagination1);
            this.panelLeft.Controls.Add(this.uiCalendar1);
            this.panelLeft.Controls.Add(this.BtnRecordSetting);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(3, 3);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(3);
            this.panelLeft.Shadow = 5;
            this.panelLeft.ShadowColor = System.Drawing.Color.Black;
            this.panelLeft.ShadowOpacityAnimation = true;
            this.panelLeft.Size = new System.Drawing.Size(244, 564);
            this.panelLeft.TabIndex = 2;
            this.panelLeft.Text = "panelLeft";
            // 
            // divider2
            // 
            this.divider2.ColorSplit = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(172)))), ((int)(((byte)(248)))));
            this.divider2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.divider2.Location = new System.Drawing.Point(9, 493);
            this.divider2.Name = "divider2";
            this.divider2.OrientationMargin = 0F;
            this.divider2.Size = new System.Drawing.Size(226, 4);
            this.divider2.TabIndex = 4;
            this.divider2.Text = "";
            // 
            // uiCalendar1
            // 
            this.uiCalendar1.Dock = System.Windows.Forms.DockStyle.Top;
            this.uiCalendar1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiCalendar1.Location = new System.Drawing.Point(9, 9);
            this.uiCalendar1.Name = "uiCalendar1";
            this.uiCalendar1.Size = new System.Drawing.Size(226, 34);
            this.uiCalendar1.TabIndex = 1;
            // 
            // BtnRecordSetting
            // 
            this.BtnRecordSetting.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.BtnRecordSetting.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.BtnRecordSetting.Font = new System.Drawing.Font("微软雅黑", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.BtnRecordSetting.Ghost = true;
            this.BtnRecordSetting.IconRatio = 1F;
            this.BtnRecordSetting.IconSvg = "ProfileOutlined";
            this.BtnRecordSetting.Location = new System.Drawing.Point(9, 521);
            this.BtnRecordSetting.Name = "BtnRecordSetting";
            this.BtnRecordSetting.Size = new System.Drawing.Size(226, 34);
            this.BtnRecordSetting.TabIndex = 15;
            this.BtnRecordSetting.Text = "记录设置";
            this.BtnRecordSetting.Click += new System.EventHandler(this.BtnRecordSetting_Click);
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 80F));
            this.tableLayoutPanel2.Controls.Add(this.panelLeft, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(1252, 570);
            this.tableLayoutPanel2.TabIndex = 6;
            // 
            // DataReviewPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "DataReviewPage";
            this.Size = new System.Drawing.Size(1252, 570);
            this.Load += new System.EventHandler(this.DataReview_Shown);
            this.panelRight.ResumeLayout(false);
            this.uiContextMenuStrip2.ResumeLayout(false);
            this.uiGridPanelRight.ResumeLayout(false);
            this.uiFlowLayoutPanel1.ResumeLayout(false);
            this.uiContextMenuStrip1.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Panel panelRight;
        private AntdUI.GridPanel uiGridPanelRight;
        private AntdUI.FlowPanel uiFlowLayoutPanel1;
        private AntdUI.Checkbox chkSeamWidth;
        private AntdUI.Checkbox chkRobotSpeed;
        private AntdUI.Checkbox chkFeedSpeed;
        private AntdUI.Checkbox chkLaserPower;
        private AntdUI.Table uiDataGridView1;
        private AntdUI.Pagination pagination1;
        private System.Windows.Forms.ContextMenuStrip uiContextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem ShowData;
        private AntdUI.Slider uiTrackBar1;
        private AntdUI.Label uiSymbolLabel1;
        private System.Windows.Forms.ContextMenuStrip uiContextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem SetRange;
        private AntdUI.Panel panelLeft;
        private AntdUI.DatePicker uiCalendar1;
        private AntdUI.Button btnRenerView;
        private ScottPlot.FormsPlot formsPlotData;
        private AntdUI.Button BtnRecordSetting;
        private AntdUI.Divider divider2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
    }
}
