namespace AdaWeldSystem.Sub3UI
{
    partial class ILParamPage
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
            this.uiPanelTop = new System.Windows.Forms.Panel();
            this.btnFetch = new AntdUI.Button();
            this.lblConn = new AntdUI.Label();
            this.uiTableParam = new AntdUI.Table();
            this.uiPanelBottom = new System.Windows.Forms.Panel();
            this.btnCancel = new AntdUI.Button();
            this.btnSave = new AntdUI.Button();
            this.uiTableLayoutPanel1.SuspendLayout();
            this.uiPanelTop.SuspendLayout();
            this.uiPanelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiTableLayoutPanel1
            // 
            this.uiTableLayoutPanel1.ColumnCount = 1;
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanelTop, 0, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.uiTableParam, 0, 1);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanelBottom, 0, 2);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.RowCount = 3;
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(760, 500);
            this.uiTableLayoutPanel1.TabIndex = 0;
            // 
            // uiPanelTop
            // 
            this.uiPanelTop.Controls.Add(this.btnFetch);
            this.uiPanelTop.Controls.Add(this.lblConn);
            this.uiPanelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanelTop.Location = new System.Drawing.Point(4, 5);
            this.uiPanelTop.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanelTop.Name = "uiPanelTop";
            this.uiPanelTop.Size = new System.Drawing.Size(752, 34);
            this.uiPanelTop.TabIndex = 0;
            // 
            // btnFetch
            // 
            this.btnFetch.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnFetch.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnFetch.Ghost = true;
            this.btnFetch.Location = new System.Drawing.Point(660, 0);
            this.btnFetch.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnFetch.Name = "btnFetch";
            this.btnFetch.Size = new System.Drawing.Size(88, 34);
            this.btnFetch.TabIndex = 1;
            this.btnFetch.Text = "从设备读取";
            // 
            // lblConn
            // 
            this.lblConn.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblConn.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblConn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.lblConn.Location = new System.Drawing.Point(4, 6);
            this.lblConn.Name = "lblConn";
            this.lblConn.Size = new System.Drawing.Size(200, 24);
            this.lblConn.TabIndex = 0;
            this.lblConn.Text = "英莱设备：未连接";
            // 
            // uiTableParam
            // 
            this.uiTableParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableParam.Font = new System.Drawing.Font("宋体", 12F);
            this.uiTableParam.Gap = 12;
            this.uiTableParam.Location = new System.Drawing.Point(4, 49);
            this.uiTableParam.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiTableParam.Name = "uiTableParam";
            this.uiTableParam.Size = new System.Drawing.Size(752, 386);
            this.uiTableParam.TabIndex = 1;
            // 
            // uiPanelBottom
            // 
            this.uiPanelBottom.Controls.Add(this.btnCancel);
            this.uiPanelBottom.Controls.Add(this.btnSave);
            this.uiPanelBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanelBottom.Location = new System.Drawing.Point(4, 445);
            this.uiPanelBottom.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanelBottom.Name = "uiPanelBottom";
            this.uiPanelBottom.Size = new System.Drawing.Size(752, 50);
            this.uiPanelBottom.TabIndex = 2;
            // 
            // btnCancel
            // 
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCancel.Location = new System.Drawing.Point(668, 8);
            this.btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(76, 35);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            // 
            // btnSave
            // 
            this.btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSave.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSave.Location = new System.Drawing.Point(580, 8);
            this.btnSave.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(76, 35);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "保存";
            this.btnSave.Type = AntdUI.TTypeMini.Primary;
            // 
            // ILParamPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "ILParamPage";
            this.Size = new System.Drawing.Size(760, 500);
            this.uiTableLayoutPanel1.ResumeLayout(false);
            this.uiPanelTop.ResumeLayout(false);
            this.uiPanelTop.PerformLayout();
            this.uiPanelBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel uiTableLayoutPanel1;
        private System.Windows.Forms.Panel uiPanelTop;
        private AntdUI.Label lblConn;
        private AntdUI.Button btnFetch;
        private AntdUI.Table uiTableParam;
        private System.Windows.Forms.Panel uiPanelBottom;
        private AntdUI.Button btnSave;
        private AntdUI.Button btnCancel;
    }
}
