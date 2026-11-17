namespace AdaWeldSystem.UIDesign.SubDialog.ParamContolDialog
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
            this.uiPagination1 = new AntdUI.Pagination();
            this.SuspendLayout();
            // 
            // uiDataGridView1
            // 
            resources.ApplyResources(this.uiDataGridView1, "uiDataGridView1");
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Name = "uiDataGridView1";
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
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Table uiDataGridView1;
        private AntdUI.Pagination uiPagination1;
    }
}
