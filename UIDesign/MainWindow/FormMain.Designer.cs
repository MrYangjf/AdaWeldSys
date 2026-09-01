namespace AdaWeldSystem
{
    partial class FormMain
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
            AntdUI.Tabs.StyleLine styleLine1 = new AntdUI.Tabs.StyleLine();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel3 = new System.Windows.Forms.ToolStripLabel();
            this.lbl_Status = new System.Windows.Forms.ToolStripLabel();
            this.lbl_SystemTime = new System.Windows.Forms.ToolStripLabel();
            this.headerMain = new AntdUI.PageHeader();
            this.buttonSZ = new AntdUI.Button();
            this.segmentedMain = new AntdUI.Segmented();
            this.tabsMain = new AntdUI.Tabs();
            this.uiMillisecondTimer1 = new System.Windows.Forms.Timer(this.components);
            this.toolStrip1.SuspendLayout();
            this.headerMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.Color.Transparent;
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.Font = new System.Drawing.Font("宋体", 12F);
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel3,
            this.lbl_Status,
            this.lbl_SystemTime});
            this.toolStrip1.Location = new System.Drawing.Point(0, 693);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1252, 31);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripLabel3
            // 
            this.toolStripLabel3.Name = "toolStripLabel3";
            this.toolStripLabel3.Size = new System.Drawing.Size(109, 28);
            this.toolStripLabel3.Text = "流程状态：";
            // 
            // lbl_Status
            // 
            this.lbl_Status.Name = "lbl_Status";
            this.lbl_Status.Size = new System.Drawing.Size(69, 28);
            this.lbl_Status.Text = "Status";
            // 
            // lbl_SystemTime
            // 
            this.lbl_SystemTime.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.lbl_SystemTime.Name = "lbl_SystemTime";
            this.lbl_SystemTime.Size = new System.Drawing.Size(89, 28);
            this.lbl_SystemTime.Text = "系统时间";
            // 
            // headerMain
            // 
            this.headerMain.BackColor = System.Drawing.Color.Transparent;
            this.headerMain.Controls.Add(this.buttonSZ);
            this.headerMain.DividerShow = true;
            this.headerMain.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerMain.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.headerMain.Location = new System.Drawing.Point(0, 0);
            this.headerMain.Name = "headerMain";
            this.headerMain.ShowButton = true;
            this.headerMain.ShowIcon = true;
            this.headerMain.Size = new System.Drawing.Size(1252, 36);
            this.headerMain.SubText = "V1.0.0";
            this.headerMain.TabIndex = 2;
            this.headerMain.Text = "智能填缝焊接系统";
            // 
            // buttonSZ
            // 
            this.buttonSZ.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonSZ.Ghost = true;
            this.buttonSZ.IconSvg = "UserOutlined";
            this.buttonSZ.Location = new System.Drawing.Point(1022, 0);
            this.buttonSZ.Name = "buttonSZ";
            this.buttonSZ.Radius = 0;
            this.buttonSZ.Size = new System.Drawing.Size(50, 36);
            this.buttonSZ.TabIndex = 4;
            this.buttonSZ.WaveSize = 0;
            this.buttonSZ.Click += new System.EventHandler(this.buttonSZ_Click);
            // 
            // segmentedMain
            // 
            this.segmentedMain.BackActive = System.Drawing.Color.White;
            this.segmentedMain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(242)))), ((int)(((byte)(242)))), ((int)(((byte)(242)))));
            this.segmentedMain.BarBg = true;
            this.segmentedMain.BarColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(150)))), ((int)(((byte)(243)))));
            this.segmentedMain.BarPosition = AntdUI.TAlignMini.Bottom;
            this.segmentedMain.Dock = System.Windows.Forms.DockStyle.Top;
            this.segmentedMain.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.segmentedMain.Location = new System.Drawing.Point(0, 36);
            this.segmentedMain.Name = "segmentedMain";
            this.segmentedMain.Size = new System.Drawing.Size(1252, 70);
            this.segmentedMain.TabIndex = 3;
            // 
            // tabsMain
            // 
            this.tabsMain.BackColor = System.Drawing.Color.Transparent;
            this.tabsMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsMain.Location = new System.Drawing.Point(0, 106);
            this.tabsMain.Name = "tabsMain";
            this.tabsMain.Size = new System.Drawing.Size(1252, 587);
            this.tabsMain.Style = styleLine1;
            this.tabsMain.TabIndex = 6;
            this.tabsMain.TabMenuVisible = false;
            // 
            // uiMillisecondTimer1
            // 
            this.uiMillisecondTimer1.Enabled = true;
            this.uiMillisecondTimer1.Tick += new System.EventHandler(this.uiMillisecondTimer1_Tick);
            // 
            // FormMain
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(1252, 724);
            this.ControlBox = false;
            this.Controls.Add(this.tabsMain);
            this.Controls.Add(this.segmentedMain);
            this.Controls.Add(this.headerMain);
            this.Controls.Add(this.toolStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FormMain";
            this.Text = "智能填丝焊接系统";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormMain_FormClosed);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.headerMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    private System.Windows.Forms.ToolStrip toolStrip1;
    private AntdUI.PageHeader headerMain;
    private AntdUI.Segmented segmentedMain;
        private AntdUI.Tabs tabsMain;
        private System.Windows.Forms.ToolStripLabel toolStripLabel3;
        private System.Windows.Forms.ToolStripLabel lbl_SystemTime;
        private System.Windows.Forms.ToolStripLabel lbl_Status;
        private System.Windows.Forms.Timer uiMillisecondTimer1;
        private AntdUI.Button buttonSZ;
    }
}
