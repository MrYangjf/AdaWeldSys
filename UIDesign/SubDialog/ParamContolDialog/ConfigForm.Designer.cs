namespace AdaWeldSystem.Sub3UI
{
    partial class ConfigForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigForm));
            this.uiDataGridView1 = new AntdUI.Table();
            this.uiContextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.SelectConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.DeleteConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.uiPagination1 = new AntdUI.Pagination();
            this.uiContextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiDataGridView1
            // 
            this.uiDataGridView1.ContextMenuStrip = this.uiContextMenuStrip1;
            resources.ApplyResources(this.uiDataGridView1, "uiDataGridView1");
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Name = "uiDataGridView1";
            // 
            // uiContextMenuStrip1
            // 
            this.uiContextMenuStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(249)))), ((int)(((byte)(255)))));
            resources.ApplyResources(this.uiContextMenuStrip1, "uiContextMenuStrip1");
            this.uiContextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.uiContextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SelectConfig,
            this.DeleteConfig});
            this.uiContextMenuStrip1.Name = "uiContextMenuStrip1";
            // 
            // SelectConfig
            // 
            this.SelectConfig.Name = "SelectConfig";
            resources.ApplyResources(this.SelectConfig, "SelectConfig");
            this.SelectConfig.Click += new System.EventHandler(this.SelectConfig_Click);
            // 
            // DeleteConfig
            // 
            this.DeleteConfig.Name = "DeleteConfig";
            resources.ApplyResources(this.DeleteConfig, "DeleteConfig");
            this.DeleteConfig.Click += new System.EventHandler(this.DeleteConfig_Click);
            // 
            // uiPagination1
            // 
            resources.ApplyResources(this.uiPagination1, "uiPagination1");
            this.uiPagination1.Name = "uiPagination1";
            this.uiPagination1.PageSize = 50;
            this.uiPagination1.PageSizeOptions = new int[] {
        20,
        50,
        100};
            // 
            // ConfigForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiDataGridView1);
            this.Controls.Add(this.uiPagination1);
            this.Name = "ConfigForm";
            resources.ApplyResources(this, "$this");
            this.uiContextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Table uiDataGridView1;
        private AntdUI.Pagination uiPagination1;
        private System.Windows.Forms.ContextMenuStrip uiContextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem SelectConfig;
        private System.Windows.Forms.ToolStripMenuItem DeleteConfig;
    }
}
