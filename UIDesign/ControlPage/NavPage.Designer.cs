namespace AdaWeldSystem.Sub1UI
{
    partial class NavPage
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
            AntdUI.Tabs.StyleLine styleLine2 = new AntdUI.Tabs.StyleLine();
            this.uiNavMenu1 = new AntdUI.Menu();
            this.uiTabControl1 = new AntdUI.Tabs();
            this.SuspendLayout();
            // 
            // uiNavMenu1
            // 
            this.uiNavMenu1.BackActive = System.Drawing.Color.White;
            this.uiNavMenu1.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(244)))), ((int)(((byte)(244)))), ((int)(((byte)(244)))));
            this.uiNavMenu1.Dock = System.Windows.Forms.DockStyle.Left;
            this.uiNavMenu1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.uiNavMenu1.Indent = true;
            this.uiNavMenu1.Location = new System.Drawing.Point(0, 0);
            this.uiNavMenu1.Mode = AntdUI.TMenuMode.Vertical;
            this.uiNavMenu1.Name = "uiNavMenu1";
            this.uiNavMenu1.Size = new System.Drawing.Size(158, 586);
            this.uiNavMenu1.TabIndex = 0;
            // 
            // uiTabControl1
            // 
            this.uiTabControl1.BackColor = System.Drawing.Color.White;
            this.uiTabControl1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.uiTabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTabControl1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiTabControl1.Gap = 16;
            this.uiTabControl1.Location = new System.Drawing.Point(158, 0);
            this.uiTabControl1.Name = "uiTabControl1";
            this.uiTabControl1.Size = new System.Drawing.Size(834, 586);
            this.uiTabControl1.Style = styleLine2;
            this.uiTabControl1.TabIndex = 6;
            this.uiTabControl1.TabMenuVisible = false;
            // 
            // NavPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTabControl1);
            this.Controls.Add(this.uiNavMenu1);
            this.Name = "NavPage";
            this.Size = new System.Drawing.Size(992, 586);
            this.ResumeLayout(false);

        }

        #endregion

        private AntdUI.Menu uiNavMenu1;
        private AntdUI.Tabs uiTabControl1;
    }
}