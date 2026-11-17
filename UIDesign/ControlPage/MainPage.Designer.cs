namespace AdaWeldSystem.UIDesign.ControlPage
{
    partial class MainPage
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
            this.inputLog = new AntdUI.Input();
            this.panelChart2 = new AntdUI.Panel();
            this.chkSeamWidth = new AntdUI.Checkbox();
            this.chkWireZ = new AntdUI.Checkbox();
            this.chkLaserPower = new AntdUI.Checkbox();
            this.chkFeedSpeed = new AntdUI.Checkbox();
            this.chkRobotSpeed = new AntdUI.Checkbox();
            this.formsPlot2 = new ScottPlot.FormsPlot();
            this.PanelDashborad = new AntdUI.Panel();
            this.StatusGridPanel = new AntdUI.GridPanel();
            this.panelfloatbutton = new AntdUI.In.Panel();
            this.divider1 = new AntdUI.Divider();
            this.gridTopBlock = new AntdUI.GridPanel();
            this.imb_WeldViewer = new Emgu.CV.UI.ImageBox();
            this.divider3 = new AntdUI.Divider();
            this.steps1 = new AntdUI.Steps();
            this.divider2 = new AntdUI.Divider();
            this.LabelStatus = new AntdUI.Label();
            this.panel1 = new AntdUI.Panel();
            this.formsPlot1 = new ScottPlot.FormsPlot();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.panelChart2.SuspendLayout();
            this.PanelDashborad.SuspendLayout();
            this.StatusGridPanel.SuspendLayout();
            this.gridTopBlock.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imb_WeldViewer)).BeginInit();
            this.panel1.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // inputLog
            // 
            this.inputLog.AutoScroll = true;
            this.tableLayoutPanel3.SetColumnSpan(this.inputLog, 4);
            this.inputLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.inputLog.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.inputLog.Location = new System.Drawing.Point(8, 560);
            this.inputLog.Margin = new System.Windows.Forms.Padding(8);
            this.inputLog.Multiline = true;
            this.inputLog.Name = "inputLog";
            this.inputLog.ReadOnly = true;
            this.inputLog.Size = new System.Drawing.Size(1264, 352);
            this.inputLog.TabIndex = 0;
            this.inputLog.WaveSize = 0;
            this.inputLog.TextChanged += new System.EventHandler(this.inputLog_TextChanged);
            // 
            // panelChart2
            // 
            this.panelChart2.Back = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.panelChart2.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.tableLayoutPanel3.SetColumnSpan(this.panelChart2, 2);
            this.panelChart2.Controls.Add(this.chkSeamWidth);
            this.panelChart2.Controls.Add(this.chkWireZ);
            this.panelChart2.Controls.Add(this.chkLaserPower);
            this.panelChart2.Controls.Add(this.chkFeedSpeed);
            this.panelChart2.Controls.Add(this.chkRobotSpeed);
            this.panelChart2.Controls.Add(this.formsPlot2);
            this.panelChart2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelChart2.Location = new System.Drawing.Point(643, 3);
            this.panelChart2.Name = "panelChart2";
            this.panelChart2.Padding = new System.Windows.Forms.Padding(2);
            this.panelChart2.Shadow = 5;
            this.panelChart2.ShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.panelChart2.ShadowOpacity = 0.2F;
            this.panelChart2.Size = new System.Drawing.Size(634, 546);
            this.panelChart2.TabIndex = 12;
            this.panelChart2.Text = "panelChart2";
            // 
            // chkSeamWidth
            // 
            this.chkSeamWidth.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkSeamWidth.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkSeamWidth.BackColor = System.Drawing.Color.White;
            this.chkSeamWidth.Checked = true;
            this.chkSeamWidth.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSeamWidth.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.chkSeamWidth.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkSeamWidth.Location = new System.Drawing.Point(83, 9);
            this.chkSeamWidth.Name = "chkSeamWidth";
            this.chkSeamWidth.Size = new System.Drawing.Size(131, 44);
            this.chkSeamWidth.TabIndex = 1;
            this.chkSeamWidth.Text = "JointWidth";
            // 
            // chkWireZ
            // 
            this.chkWireZ.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkWireZ.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkWireZ.BackColor = System.Drawing.Color.White;
            this.chkWireZ.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.chkWireZ.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkWireZ.Location = new System.Drawing.Point(315, 8);
            this.chkWireZ.Name = "chkWireZ";
            this.chkWireZ.Size = new System.Drawing.Size(66, 44);
            this.chkWireZ.TabIndex = 5;
            this.chkWireZ.Text = "TA";
            // 
            // chkLaserPower
            // 
            this.chkLaserPower.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkLaserPower.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkLaserPower.BackColor = System.Drawing.Color.White;
            this.chkLaserPower.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.chkLaserPower.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkLaserPower.Location = new System.Drawing.Point(387, 11);
            this.chkLaserPower.Name = "chkLaserPower";
            this.chkLaserPower.Size = new System.Drawing.Size(136, 44);
            this.chkLaserPower.TabIndex = 4;
            this.chkLaserPower.Text = "LaserPower";
            // 
            // chkFeedSpeed
            // 
            this.chkFeedSpeed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkFeedSpeed.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkFeedSpeed.BackColor = System.Drawing.Color.White;
            this.chkFeedSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.chkFeedSpeed.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkFeedSpeed.Location = new System.Drawing.Point(220, 8);
            this.chkFeedSpeed.Name = "chkFeedSpeed";
            this.chkFeedSpeed.Size = new System.Drawing.Size(89, 44);
            this.chkFeedSpeed.TabIndex = 3;
            this.chkFeedSpeed.Text = "Vwire";
            // 
            // chkRobotSpeed
            // 
            this.chkRobotSpeed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkRobotSpeed.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.chkRobotSpeed.BackColor = System.Drawing.Color.White;
            this.chkRobotSpeed.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.chkRobotSpeed.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chkRobotSpeed.Location = new System.Drawing.Point(526, 11);
            this.chkRobotSpeed.Name = "chkRobotSpeed";
            this.chkRobotSpeed.Size = new System.Drawing.Size(100, 44);
            this.chkRobotSpeed.TabIndex = 2;
            this.chkRobotSpeed.Text = "Vrobot";
            // 
            // formsPlot2
            // 
            this.formsPlot2.BackColor = System.Drawing.Color.White;
            this.formsPlot2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot2.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.formsPlot2.Location = new System.Drawing.Point(8, 8);
            this.formsPlot2.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.formsPlot2.Name = "formsPlot2";
            this.formsPlot2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.formsPlot2.Size = new System.Drawing.Size(618, 530);
            this.formsPlot2.TabIndex = 2;
            // 
            // PanelDashborad
            // 
            this.tableLayoutPanel3.SetColumnSpan(this.PanelDashborad, 2);
            this.PanelDashborad.Controls.Add(this.StatusGridPanel);
            this.PanelDashborad.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PanelDashborad.Location = new System.Drawing.Point(1283, 3);
            this.PanelDashborad.Name = "PanelDashborad";
            this.PanelDashborad.Padding = new System.Windows.Forms.Padding(3);
            this.tableLayoutPanel3.SetRowSpan(this.PanelDashborad, 2);
            this.PanelDashborad.Shadow = 5;
            this.PanelDashborad.ShadowOpacityAnimation = true;
            this.PanelDashborad.Size = new System.Drawing.Size(634, 914);
            this.PanelDashborad.TabIndex = 21;
            this.PanelDashborad.Text = "panel2";
            // 
            // StatusGridPanel
            // 
            this.StatusGridPanel.BackColor = System.Drawing.Color.Transparent;
            this.StatusGridPanel.Controls.Add(this.panelfloatbutton);
            this.StatusGridPanel.Controls.Add(this.divider1);
            this.StatusGridPanel.Controls.Add(this.gridTopBlock);
            this.StatusGridPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StatusGridPanel.Location = new System.Drawing.Point(9, 9);
            this.StatusGridPanel.Name = "StatusGridPanel";
            this.StatusGridPanel.Size = new System.Drawing.Size(616, 896);
            this.StatusGridPanel.Span = "fill;20:fill;150:fill";
            this.StatusGridPanel.TabIndex = 19;
            // 
            // panelfloatbutton
            // 
            this.panelfloatbutton.Location = new System.Drawing.Point(3, 711);
            this.panelfloatbutton.Name = "panelfloatbutton";
            this.panelfloatbutton.Size = new System.Drawing.Size(610, 182);
            this.panelfloatbutton.TabIndex = 23;
            this.panelfloatbutton.Text = "panel2";
            // 
            // divider1
            // 
            this.divider1.ColorSplit = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(150)))), ((int)(((byte)(243)))));
            this.divider1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.divider1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.divider1.Location = new System.Drawing.Point(3, 686);
            this.divider1.Name = "divider1";
            this.divider1.OrientationMargin = 0F;
            this.divider1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.divider1.Size = new System.Drawing.Size(610, 19);
            this.divider1.TabIndex = 21;
            this.divider1.Text = "通讯状态";
            this.divider1.TextPadding = 0F;
            this.divider1.Thickness = 0.3F;
            // 
            // gridTopBlock
            // 
            this.gridTopBlock.Controls.Add(this.imb_WeldViewer);
            this.gridTopBlock.Controls.Add(this.divider3);
            this.gridTopBlock.Controls.Add(this.steps1);
            this.gridTopBlock.Controls.Add(this.divider2);
            this.gridTopBlock.Controls.Add(this.LabelStatus);
            this.gridTopBlock.Location = new System.Drawing.Point(0, 0);
            this.gridTopBlock.Margin = new System.Windows.Forms.Padding(0);
            this.gridTopBlock.Name = "gridTopBlock";
            this.gridTopBlock.Size = new System.Drawing.Size(616, 683);
            this.gridTopBlock.Span = "60:fill;20:fill;80:fill;20:fill;fill";
            this.gridTopBlock.TabIndex = 22;
            // 
            // imb_WeldViewer
            // 
            this.imb_WeldViewer.BackColor = System.Drawing.Color.Silver;
            this.imb_WeldViewer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imb_WeldViewer.FunctionalMode = Emgu.CV.UI.ImageBox.FunctionalModeOption.RightClickMenu;
            this.imb_WeldViewer.Location = new System.Drawing.Point(3, 228);
            this.imb_WeldViewer.Name = "imb_WeldViewer";
            this.imb_WeldViewer.Size = new System.Drawing.Size(610, 452);
            this.imb_WeldViewer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.imb_WeldViewer.TabIndex = 30;
            this.imb_WeldViewer.TabStop = false;
            // 
            // divider3
            // 
            this.divider3.ColorSplit = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(150)))), ((int)(((byte)(243)))));
            this.divider3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.divider3.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.divider3.Location = new System.Drawing.Point(3, 203);
            this.divider3.Name = "divider3";
            this.divider3.OrientationMargin = 0F;
            this.divider3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.divider3.Size = new System.Drawing.Size(610, 19);
            this.divider3.TabIndex = 29;
            this.divider3.Text = "监控画面";
            this.divider3.TextPadding = 0F;
            this.divider3.Thickness = 0.3F;
            // 
            // steps1
            // 
            this.steps1.Location = new System.Drawing.Point(3, 103);
            this.steps1.Name = "steps1";
            this.steps1.Size = new System.Drawing.Size(610, 94);
            this.steps1.TabIndex = 27;
            this.steps1.Text = "steps1";
            // 
            // divider2
            // 
            this.divider2.ColorSplit = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(150)))), ((int)(((byte)(243)))));
            this.divider2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.divider2.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.divider2.Location = new System.Drawing.Point(3, 78);
            this.divider2.Name = "divider2";
            this.divider2.OrientationMargin = 0F;
            this.divider2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.divider2.Size = new System.Drawing.Size(610, 19);
            this.divider2.TabIndex = 28;
            this.divider2.Text = "流程状态";
            this.divider2.TextPadding = 0F;
            this.divider2.Thickness = 0.3F;
            // 
            // LabelStatus
            // 
            this.LabelStatus.BackColor = System.Drawing.Color.Transparent;
            this.LabelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LabelStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 15F);
            this.LabelStatus.IconGap = 5;
            this.LabelStatus.IconRatio = 1F;
            this.LabelStatus.Location = new System.Drawing.Point(3, 3);
            this.LabelStatus.Name = "LabelStatus";
            this.LabelStatus.Prefix = "";
            this.LabelStatus.PrefixSvg = "AlertOutlined";
            this.LabelStatus.Size = new System.Drawing.Size(610, 69);
            this.LabelStatus.TabIndex = 0;
            this.LabelStatus.Text = "设备状态";
            this.LabelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel1
            // 
            this.panel1.Back = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.panel1.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.tableLayoutPanel3.SetColumnSpan(this.panel1, 2);
            this.panel1.Controls.Add(this.formsPlot1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(3);
            this.panel1.Shadow = 5;
            this.panel1.ShadowColor = System.Drawing.Color.Black;
            this.panel1.ShadowOpacity = 0.2F;
            this.panel1.ShadowOpacityAnimation = true;
            this.panel1.ShadowOpacityHover = 0.5F;
            this.panel1.Size = new System.Drawing.Size(634, 546);
            this.panel1.TabIndex = 3;
            this.panel1.Text = "panel1";
            // 
            // formsPlot1
            // 
            this.formsPlot1.BackColor = System.Drawing.Color.Transparent;
            this.formsPlot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.formsPlot1.Location = new System.Drawing.Point(9, 9);
            this.formsPlot1.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(616, 528);
            this.formsPlot1.TabIndex = 2;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 6;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel3.Controls.Add(this.panelChart2, 2, 0);
            this.tableLayoutPanel3.Controls.Add(this.panel1, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.PanelDashborad, 4, 0);
            this.tableLayoutPanel3.Controls.Add(this.inputLog, 0, 1);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 2;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(1920, 920);
            this.tableLayoutPanel3.TabIndex = 1;
            // 
            // MainPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel3);
            this.Name = "MainPage";
            this.Size = new System.Drawing.Size(1920, 920);
            this.Load += new System.EventHandler(this.MainPage_Shown);
            this.panelChart2.ResumeLayout(false);
            this.panelChart2.PerformLayout();
            this.PanelDashborad.ResumeLayout(false);
            this.StatusGridPanel.ResumeLayout(false);
            this.gridTopBlock.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imb_WeldViewer)).EndInit();
            this.panel1.ResumeLayout(false);
            this.tableLayoutPanel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private AntdUI.Input inputLog;
        private AntdUI.Panel panel1;
        private AntdUI.Panel panelChart2;
        private ScottPlot.FormsPlot formsPlot1;
        private ScottPlot.FormsPlot formsPlot2;
        private AntdUI.GridPanel StatusGridPanel;
        private AntdUI.Label LabelStatus;
        private AntdUI.Steps steps1;
        private AntdUI.Panel PanelDashborad;
        private AntdUI.GridPanel gridTopBlock;
        private AntdUI.Divider divider1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private AntdUI.Checkbox chkLaserPower;
        private AntdUI.Checkbox chkFeedSpeed;
        private AntdUI.Checkbox chkRobotSpeed;
        private AntdUI.Checkbox chkSeamWidth;
        private AntdUI.Checkbox chkWireZ;
        private AntdUI.Divider divider2;
        private AntdUI.Divider divider3;
        private Emgu.CV.UI.ImageBox imb_WeldViewer;
        private AntdUI.In.Panel panelfloatbutton;
    }
}