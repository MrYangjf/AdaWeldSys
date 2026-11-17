namespace AdaWeldSystem.UIDesign.SubDialog.DataReviewerDialog
{
    partial class RecordConfigForm
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
            this.uiGroupBox1 = new System.Windows.Forms.GroupBox();
            this.uiTableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.uiLabel1 = new AntdUI.Label();
            this.uiLabelRetention = new AntdUI.Label();
            this.txtRecordPath = new AntdUI.Input();
            this.txtRetention = new AntdUI.Input();
            this.btnBrowseRecord = new AntdUI.Button();
            this.chkUseObfuscation = new AntdUI.Checkbox();
            this.chkRecordEveryFrame = new AntdUI.Checkbox();
            this.uiPanel1 = new System.Windows.Forms.Panel();
            this.btnCancel = new AntdUI.Button();
            this.btnSave = new AntdUI.Button();
            this.uiTableLayoutPanel1.SuspendLayout();
            this.uiGroupBox1.SuspendLayout();
            this.uiTableLayoutPanel2.SuspendLayout();
            this.uiPanel1.SuspendLayout();
            this.SuspendLayout();
            //
            // uiTableLayoutPanel1
            //
            this.uiTableLayoutPanel1.ColumnCount = 1;
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.Controls.Add(this.uiGroupBox1, 0, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanel1, 0, 1);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.RowCount = 2;
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(600, 320);
            this.uiTableLayoutPanel1.TabIndex = 0;
            //
            // uiGroupBox1
            //
            this.uiGroupBox1.Controls.Add(this.uiTableLayoutPanel2);
            this.uiGroupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiGroupBox1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiGroupBox1.Location = new System.Drawing.Point(4, 5);
            this.uiGroupBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiGroupBox1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiGroupBox1.Name = "uiGroupBox1";
            this.uiGroupBox1.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.uiGroupBox1.Size = new System.Drawing.Size(592, 250);
            this.uiGroupBox1.TabIndex = 0;
            this.uiGroupBox1.TabStop = false;
            this.uiGroupBox1.Text = "记录配置";
            //
            // uiTableLayoutPanel2
            //
            this.uiTableLayoutPanel2.ColumnCount = 3;
            this.uiTableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.uiTableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.uiTableLayoutPanel2.Controls.Add(this.uiLabel1, 0, 0);
            this.uiTableLayoutPanel2.Controls.Add(this.uiLabelRetention, 0, 1);
            this.uiTableLayoutPanel2.Controls.Add(this.txtRecordPath, 1, 0);
            this.uiTableLayoutPanel2.Controls.Add(this.txtRetention, 1, 1);
            this.uiTableLayoutPanel2.Controls.Add(this.btnBrowseRecord, 2, 0);
            this.uiTableLayoutPanel2.Controls.Add(this.chkUseObfuscation, 1, 2);
            this.uiTableLayoutPanel2.Controls.Add(this.chkRecordEveryFrame, 1, 3);
            this.uiTableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel2.Location = new System.Drawing.Point(0, 55);
            this.uiTableLayoutPanel2.Name = "uiTableLayoutPanel2";
            this.uiTableLayoutPanel2.RowCount = 5;
            this.uiTableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.uiTableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.uiTableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.uiTableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.uiTableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.uiTableLayoutPanel2.Size = new System.Drawing.Size(592, 195);
            this.uiTableLayoutPanel2.TabIndex = 0;
            //
            // uiLabel1
            //
            this.uiLabel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiLabel1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLabel1.Location = new System.Drawing.Point(3, 3);
            this.uiLabel1.Name = "uiLabel1";
            this.uiLabel1.Size = new System.Drawing.Size(134, 39);
            this.uiLabel1.TabIndex = 0;
            this.uiLabel1.Text = "记录文件路径：";
            this.uiLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // uiLabelRetention
            //
            this.uiLabelRetention.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiLabelRetention.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLabelRetention.Location = new System.Drawing.Point(3, 48);
            this.uiLabelRetention.Name = "uiLabelRetention";
            this.uiLabelRetention.Size = new System.Drawing.Size(134, 39);
            this.uiLabelRetention.TabIndex = 1;
            this.uiLabelRetention.Text = "保留天数(0永久)：";
            this.uiLabelRetention.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtRecordPath
            //
            this.txtRecordPath.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtRecordPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRecordPath.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRecordPath.Location = new System.Drawing.Point(144, 8);
            this.txtRecordPath.Margin = new System.Windows.Forms.Padding(4, 8, 4, 8);
            this.txtRecordPath.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtRecordPath.Name = "txtRecordPath";
            this.txtRecordPath.Size = new System.Drawing.Size(344, 29);
            this.txtRecordPath.TabIndex = 2;
            //
            // txtRetention
            //
            this.txtRetention.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtRetention.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRetention.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRetention.Location = new System.Drawing.Point(144, 53);
            this.txtRetention.Margin = new System.Windows.Forms.Padding(4, 8, 4, 8);
            this.txtRetention.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtRetention.Name = "txtRetention";
            this.txtRetention.Size = new System.Drawing.Size(344, 29);
            this.txtRetention.TabIndex = 3;
            //
            // btnBrowseRecord
            //
            this.btnBrowseRecord.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBrowseRecord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBrowseRecord.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBrowseRecord.Location = new System.Drawing.Point(495, 5);
            this.btnBrowseRecord.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.btnBrowseRecord.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnBrowseRecord.Name = "btnBrowseRecord";
            this.btnBrowseRecord.Size = new System.Drawing.Size(94, 35);
            this.btnBrowseRecord.TabIndex = 4;
            this.btnBrowseRecord.Text = "浏览";
            this.btnBrowseRecord.Click += new System.EventHandler(this.btnBrowseRecord_Click);
            //
            // chkUseObfuscation
            //
            this.chkUseObfuscation.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkUseObfuscation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkUseObfuscation.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkUseObfuscation.Location = new System.Drawing.Point(144, 93);
            this.chkUseObfuscation.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkUseObfuscation.MinimumSize = new System.Drawing.Size(1, 1);
            this.chkUseObfuscation.Name = "chkUseObfuscation";
            this.chkUseObfuscation.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkUseObfuscation.Size = new System.Drawing.Size(344, 39);
            this.chkUseObfuscation.TabIndex = 6;
            this.chkUseObfuscation.Text = "pDat文件使用混淆（TXT打开为乱码）";
            //
            // chkRecordEveryFrame
            //
            this.chkRecordEveryFrame.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkRecordEveryFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkRecordEveryFrame.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.chkRecordEveryFrame.Location = new System.Drawing.Point(144, 138);
            this.chkRecordEveryFrame.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkRecordEveryFrame.MinimumSize = new System.Drawing.Size(1, 1);
            this.chkRecordEveryFrame.Name = "chkRecordEveryFrame";
            this.chkRecordEveryFrame.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.chkRecordEveryFrame.Size = new System.Drawing.Size(344, 39);
            this.chkRecordEveryFrame.TabIndex = 7;
            this.chkRecordEveryFrame.Text = "记录线激光逐帧全帧（取消勾选仅记录过程帧）";
            //
            // uiPanel1
            //
            this.uiPanel1.Controls.Add(this.btnCancel);
            this.uiPanel1.Controls.Add(this.btnSave);
            this.uiPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel1.Location = new System.Drawing.Point(4, 265);
            this.uiPanel1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanel1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel1.Name = "uiPanel1";
            this.uiPanel1.Size = new System.Drawing.Size(592, 50);
            this.uiPanel1.TabIndex = 1;
            //
            // btnCancel
            //
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCancel.Location = new System.Drawing.Point(449, 8);
            this.btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(140, 35);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // btnSave
            //
            this.btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSave.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSave.Location = new System.Drawing.Point(3, 8);
            this.btnSave.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(140, 35);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // RecordConfigForm
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "RecordConfigForm";
            this.Size = new System.Drawing.Size(600, 320);
            this.uiTableLayoutPanel1.ResumeLayout(false);
            this.uiGroupBox1.ResumeLayout(false);
            this.uiTableLayoutPanel2.ResumeLayout(false);
            this.uiPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel uiTableLayoutPanel1;
        private System.Windows.Forms.GroupBox uiGroupBox1;
        private System.Windows.Forms.TableLayoutPanel uiTableLayoutPanel2;
        private AntdUI.Label uiLabel1;
        private AntdUI.Label uiLabelRetention;
        private AntdUI.Input txtRecordPath;
        private AntdUI.Input txtRetention;
        private AntdUI.Button btnBrowseRecord;
        private AntdUI.Checkbox chkUseObfuscation;
        private AntdUI.Checkbox chkRecordEveryFrame;
        private System.Windows.Forms.Panel uiPanel1;
        private AntdUI.Button btnSave;
        private AntdUI.Button btnCancel;
    }
}
