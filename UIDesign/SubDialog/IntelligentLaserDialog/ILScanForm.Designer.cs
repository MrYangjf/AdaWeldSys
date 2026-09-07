namespace AdaWeldSystem.Sub3UI
{
    partial class ILScanForm
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
            this.uiTableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.uiTableSensor = new AntdUI.Table();
            this.uiPanelButtons = new System.Windows.Forms.Panel();
            this.btnCancel = new AntdUI.Button();
            this.btnOK = new AntdUI.Button();
            this.uiTableLayoutPanel1.SuspendLayout();
            this.uiPanelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiTableLayoutPanel1
            // 
            this.uiTableLayoutPanel1.ColumnCount = 1;
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.Controls.Add(this.uiTableSensor, 0, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanelButtons, 0, 1);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.RowCount = 2;
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(720, 400);
            this.uiTableLayoutPanel1.TabIndex = 0;
            // 
            // uiTableSensor
            // 
            this.uiTableSensor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableSensor.Font = new System.Drawing.Font("宋体", 12F);
            this.uiTableSensor.Gap = 12;
            this.uiTableSensor.Location = new System.Drawing.Point(4, 5);
            this.uiTableSensor.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiTableSensor.Name = "uiTableSensor";
            this.uiTableSensor.Size = new System.Drawing.Size(712, 330);
            this.uiTableSensor.TabIndex = 0;
            // 
            // uiPanelButtons
            // 
            this.uiPanelButtons.Controls.Add(this.btnCancel);
            this.uiPanelButtons.Controls.Add(this.btnOK);
            this.uiPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanelButtons.Location = new System.Drawing.Point(4, 345);
            this.uiPanelButtons.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanelButtons.Name = "uiPanelButtons";
            this.uiPanelButtons.Size = new System.Drawing.Size(712, 50);
            this.uiPanelButtons.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCancel.Location = new System.Drawing.Point(628, 8);
            this.btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(76, 35);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            // 
            // btnOK
            // 
            this.btnOK.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnOK.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnOK.Location = new System.Drawing.Point(540, 8);
            this.btnOK.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(76, 35);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "确定";
            this.btnOK.Type = AntdUI.TTypeMini.Primary;
            // 
            // ILScanDialog
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "ILScanDialog";
            this.Size = new System.Drawing.Size(720, 400);
            this.uiTableLayoutPanel1.ResumeLayout(false);
            this.uiPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel uiTableLayoutPanel1;
        private AntdUI.Table uiTableSensor;
        private System.Windows.Forms.Panel uiPanelButtons;
        private AntdUI.Button btnOK;
        private AntdUI.Button btnCancel;
    }
}
