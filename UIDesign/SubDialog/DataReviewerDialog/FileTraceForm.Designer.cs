namespace AdaWeldSystem.Sub3UI
{
    partial class FileTraceForm
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
            this.uiPanelRecord = new System.Windows.Forms.Panel();
            this.uiTableRecord = new AntdUI.Table();
            this.uiLabelRecordTitle = new AntdUI.Label();
            this.uiPanelTemp = new System.Windows.Forms.Panel();
            this.uiTableTemp = new AntdUI.Table();
            this.uiLabelTempTitle = new AntdUI.Label();
            this.uiPanel1 = new System.Windows.Forms.Panel();
            this.uiLabelTempCount = new AntdUI.Label();
            this.uiLabelRecordCount = new AntdUI.Label();
            this.btnRefresh = new AntdUI.Button();
            this.uiPanel2 = new System.Windows.Forms.Panel();
            this.btnCancel = new AntdUI.Button();
            this.btnConvertTemp = new AntdUI.Button();
            this.btnSelectTemp = new AntdUI.Button();
            this.btnSelectRecord = new AntdUI.Button();
            this.uiTableLayoutPanel1.SuspendLayout();
            this.uiPanelRecord.SuspendLayout();
            this.uiPanelTemp.SuspendLayout();
            this.uiPanel1.SuspendLayout();
            this.uiPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiTableLayoutPanel1
            // 
            this.uiTableLayoutPanel1.ColumnCount = 2;
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanelRecord, 0, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanelTemp, 1, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanel1, 0, 1);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanel2, 1, 1);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.RowCount = 2;
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(700, 420);
            this.uiTableLayoutPanel1.TabIndex = 0;
            // 
            // uiPanelRecord
            // 
            this.uiPanelRecord.Controls.Add(this.uiTableRecord);
            this.uiPanelRecord.Controls.Add(this.uiLabelRecordTitle);
            this.uiPanelRecord.Location = new System.Drawing.Point(4, 5);
            this.uiPanelRecord.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanelRecord.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanelRecord.Name = "uiPanelRecord";
            this.uiPanelRecord.Size = new System.Drawing.Size(342, 350);
            this.uiPanelRecord.TabIndex = 0;
            // 
            // uiTableRecord
            // 
            this.uiTableRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableRecord.Font = new System.Drawing.Font("宋体", 12F);
            this.uiTableRecord.Gap = 12;
            this.uiTableRecord.Location = new System.Drawing.Point(0, 32);
            this.uiTableRecord.Name = "uiTableRecord";
            this.uiTableRecord.Size = new System.Drawing.Size(342, 318);
            this.uiTableRecord.TabIndex = 1;
            // 
            // uiLabelRecordTitle
            // 
            this.uiLabelRecordTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.uiLabelRecordTitle.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLabelRecordTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.uiLabelRecordTitle.Location = new System.Drawing.Point(0, 0);
            this.uiLabelRecordTitle.Name = "uiLabelRecordTitle";
            this.uiLabelRecordTitle.Size = new System.Drawing.Size(342, 32);
            this.uiLabelRecordTitle.TabIndex = 0;
            this.uiLabelRecordTitle.Text = "正常记录文件";
            // 
            // uiPanelTemp
            // 
            this.uiPanelTemp.Controls.Add(this.uiTableTemp);
            this.uiPanelTemp.Controls.Add(this.uiLabelTempTitle);
            this.uiPanelTemp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanelTemp.Location = new System.Drawing.Point(354, 5);
            this.uiPanelTemp.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanelTemp.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanelTemp.Name = "uiPanelTemp";
            this.uiPanelTemp.Size = new System.Drawing.Size(342, 350);
            this.uiPanelTemp.TabIndex = 1;
            // 
            // uiTableTemp
            // 
            this.uiTableTemp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableTemp.Font = new System.Drawing.Font("宋体", 12F);
            this.uiTableTemp.Gap = 12;
            this.uiTableTemp.Location = new System.Drawing.Point(0, 32);
            this.uiTableTemp.Name = "uiTableTemp";
            this.uiTableTemp.Size = new System.Drawing.Size(342, 318);
            this.uiTableTemp.TabIndex = 1;
            // 
            // uiLabelTempTitle
            // 
            this.uiLabelTempTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.uiLabelTempTitle.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLabelTempTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.uiLabelTempTitle.Location = new System.Drawing.Point(0, 0);
            this.uiLabelTempTitle.Name = "uiLabelTempTitle";
            this.uiLabelTempTitle.Size = new System.Drawing.Size(342, 32);
            this.uiLabelTempTitle.TabIndex = 0;
            this.uiLabelTempTitle.Text = "临时文件（异常保留）";
            // 
            // uiPanel1
            // 
            this.uiPanel1.Controls.Add(this.uiLabelTempCount);
            this.uiPanel1.Controls.Add(this.uiLabelRecordCount);
            this.uiPanel1.Controls.Add(this.btnRefresh);
            this.uiPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel1.Location = new System.Drawing.Point(4, 365);
            this.uiPanel1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanel1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel1.Name = "uiPanel1";
            this.uiPanel1.Size = new System.Drawing.Size(342, 50);
            this.uiPanel1.TabIndex = 2;
            // 
            // uiLabelTempCount
            // 
            this.uiLabelTempCount.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLabelTempCount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.uiLabelTempCount.Location = new System.Drawing.Point(180, 15);
            this.uiLabelTempCount.Name = "uiLabelTempCount";
            this.uiLabelTempCount.Size = new System.Drawing.Size(100, 20);
            this.uiLabelTempCount.TabIndex = 2;
            this.uiLabelTempCount.Text = "共 0 个";
            // 
            // uiLabelRecordCount
            // 
            this.uiLabelRecordCount.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLabelRecordCount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.uiLabelRecordCount.Location = new System.Drawing.Point(50, 15);
            this.uiLabelRecordCount.Name = "uiLabelRecordCount";
            this.uiLabelRecordCount.Size = new System.Drawing.Size(100, 20);
            this.uiLabelRecordCount.TabIndex = 1;
            this.uiLabelRecordCount.Text = "共 0 个";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRefresh.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRefresh.Location = new System.Drawing.Point(280, 8);
            this.btnRefresh.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(55, 35);
            this.btnRefresh.TabIndex = 0;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // uiPanel2
            // 
            this.uiPanel2.Controls.Add(this.btnCancel);
            this.uiPanel2.Controls.Add(this.btnConvertTemp);
            this.uiPanel2.Controls.Add(this.btnSelectTemp);
            this.uiPanel2.Controls.Add(this.btnSelectRecord);
            this.uiPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel2.Location = new System.Drawing.Point(354, 365);
            this.uiPanel2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanel2.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel2.Name = "uiPanel2";
            this.uiPanel2.Size = new System.Drawing.Size(342, 50);
            this.uiPanel2.TabIndex = 3;
            // 
            // btnCancel
            // 
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCancel.Location = new System.Drawing.Point(260, 8);
            this.btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 35);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnConvertTemp
            // 
            this.btnConvertTemp.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConvertTemp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConvertTemp.Location = new System.Drawing.Point(175, 8);
            this.btnConvertTemp.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnConvertTemp.Name = "btnConvertTemp";
            this.btnConvertTemp.Size = new System.Drawing.Size(75, 35);
            this.btnConvertTemp.TabIndex = 2;
            this.btnConvertTemp.Text = "转换";
            this.btnConvertTemp.Click += new System.EventHandler(this.btnConvertTemp_Click);
            // 
            // btnSelectTemp
            // 
            this.btnSelectTemp.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSelectTemp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSelectTemp.Location = new System.Drawing.Point(90, 8);
            this.btnSelectTemp.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSelectTemp.Name = "btnSelectTemp";
            this.btnSelectTemp.Size = new System.Drawing.Size(75, 35);
            this.btnSelectTemp.TabIndex = 1;
            this.btnSelectTemp.Text = "选临时";
            this.btnSelectTemp.Click += new System.EventHandler(this.btnSelectTemp_Click);
            // 
            // btnSelectRecord
            // 
            this.btnSelectRecord.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSelectRecord.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSelectRecord.Location = new System.Drawing.Point(5, 8);
            this.btnSelectRecord.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSelectRecord.Name = "btnSelectRecord";
            this.btnSelectRecord.Size = new System.Drawing.Size(75, 35);
            this.btnSelectRecord.TabIndex = 0;
            this.btnSelectRecord.Text = "选记录";
            this.btnSelectRecord.Click += new System.EventHandler(this.btnSelectRecord_Click);
            // 
            // FileTraceForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "FileTraceForm";
            this.Size = new System.Drawing.Size(700, 420);
            this.uiTableLayoutPanel1.ResumeLayout(false);
            this.uiPanelRecord.ResumeLayout(false);
            this.uiPanelTemp.ResumeLayout(false);
            this.uiPanel1.ResumeLayout(false);
            this.uiPanel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel uiTableLayoutPanel1;
        private System.Windows.Forms.Panel uiPanelRecord;
        private AntdUI.Label uiLabelRecordTitle;
        private AntdUI.Table uiTableRecord;
        private System.Windows.Forms.Panel uiPanelTemp;
        private AntdUI.Label uiLabelTempTitle;
        private AntdUI.Table uiTableTemp;
        private System.Windows.Forms.Panel uiPanel1;
        private AntdUI.Button btnRefresh;
        private System.Windows.Forms.Panel uiPanel2;
        private AntdUI.Button btnSelectRecord;
        private AntdUI.Button btnSelectTemp;
        private AntdUI.Button btnConvertTemp;
        private AntdUI.Button btnCancel;
        private AntdUI.Label uiLabelRecordCount;
        private AntdUI.Label uiLabelTempCount;
    }
}
