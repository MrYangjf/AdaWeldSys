namespace AdaWeldSystem.UIDesign.ControlPage
{
    partial class DeviceMonitorPage
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.image3D1 = new AntdUI.Image3D();
            this.tlPTemp = new System.Windows.Forms.TableLayoutPanel();
            this.lblTempLensCaption = new AntdUI.Label();
            this.lblTempLensValue = new AntdUI.Label();
            this.lblTempAmbientCaption = new AntdUI.Label();
            this.lblTempAmbientValue = new AntdUI.Label();
            this.lblTempRiseCaption = new AntdUI.Label();
            this.lblTempRiseValue = new AntdUI.Label();
            this.lblTempPressureCaption = new AntdUI.Label();
            this.lblTempPressureValue = new AntdUI.Label();
            this.lblTempState = new AntdUI.Label();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.tlPTemp.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Controls.Add(this.image3D1, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.tlPTemp, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1920, 920);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // image3D1
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.image3D1, 2);
            this.image3D1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.image3D1.HoverBack = System.Drawing.Color.AntiqueWhite;
            this.image3D1.Image = global::AdaWeldSystem.Properties.Resources.weldhead1;
            this.image3D1.ImageFit = AntdUI.TFit.Contain;
            this.image3D1.Location = new System.Drawing.Point(483, 233);
            this.image3D1.Name = "image3D1";
            this.tableLayoutPanel1.SetRowSpan(this.image3D1, 2);
            this.image3D1.ShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(119)))), ((int)(((byte)(255)))));
            this.image3D1.Size = new System.Drawing.Size(954, 454);
            this.image3D1.TabIndex = 0;
            this.image3D1.Text = "image3D1";
            // 
            // tlPTemp
            // 
            this.tlPTemp.ColumnCount = 2;
            this.tlPTemp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlPTemp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlPTemp.Controls.Add(this.lblTempPressureValue, 1, 3);
            this.tlPTemp.Controls.Add(this.lblTempRiseValue, 1, 2);
            this.tlPTemp.Controls.Add(this.lblTempAmbientValue, 1, 1);
            this.tlPTemp.Controls.Add(this.lblTempLensValue, 1, 0);
            this.tlPTemp.Controls.Add(this.lblTempPressureCaption, 0, 3);
            this.tlPTemp.Controls.Add(this.lblTempRiseCaption, 0, 2);
            this.tlPTemp.Controls.Add(this.lblTempAmbientCaption, 0, 1);
            this.tlPTemp.Controls.Add(this.lblTempLensCaption, 0, 0);
            this.tlPTemp.Controls.Add(this.lblTempState, 0, 4);
            this.tlPTemp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlPTemp.Location = new System.Drawing.Point(3, 233);
            this.tlPTemp.Name = "tlPTemp";
            this.tlPTemp.RowCount = 9;
            this.tableLayoutPanel1.SetRowSpan(this.tlPTemp, 2);
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tlPTemp.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlPTemp.Size = new System.Drawing.Size(474, 454);
            this.tlPTemp.TabIndex = 1;
            // 
            // lblTempLensCaption
            // 
            this.lblTempLensCaption.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempLensCaption.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempLensCaption.Name = "lblTempLensCaption";
            this.lblTempLensCaption.Size = new System.Drawing.Size(234, 40);
            this.lblTempLensCaption.TabIndex = 0;
            this.lblTempLensCaption.Text = "保护镜温度(℃)";
            this.lblTempLensCaption.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempLensValue
            // 
            this.lblTempLensValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempLensValue.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempLensValue.Name = "lblTempLensValue";
            this.lblTempLensValue.Size = new System.Drawing.Size(234, 40);
            this.lblTempLensValue.TabIndex = 1;
            this.lblTempLensValue.Text = "—";
            this.lblTempLensValue.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempAmbientCaption
            // 
            this.lblTempAmbientCaption.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempAmbientCaption.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempAmbientCaption.Name = "lblTempAmbientCaption";
            this.lblTempAmbientCaption.Size = new System.Drawing.Size(234, 40);
            this.lblTempAmbientCaption.TabIndex = 2;
            this.lblTempAmbientCaption.Text = "腔体温度(℃)";
            this.lblTempAmbientCaption.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempAmbientValue
            // 
            this.lblTempAmbientValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempAmbientValue.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempAmbientValue.Name = "lblTempAmbientValue";
            this.lblTempAmbientValue.Size = new System.Drawing.Size(234, 40);
            this.lblTempAmbientValue.TabIndex = 3;
            this.lblTempAmbientValue.Text = "—";
            this.lblTempAmbientValue.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempRiseCaption
            // 
            this.lblTempRiseCaption.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempRiseCaption.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempRiseCaption.Name = "lblTempRiseCaption";
            this.lblTempRiseCaption.Size = new System.Drawing.Size(234, 40);
            this.lblTempRiseCaption.TabIndex = 4;
            this.lblTempRiseCaption.Text = "温升(℃)";
            this.lblTempRiseCaption.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempRiseValue
            // 
            this.lblTempRiseValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempRiseValue.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempRiseValue.Name = "lblTempRiseValue";
            this.lblTempRiseValue.Size = new System.Drawing.Size(234, 40);
            this.lblTempRiseValue.TabIndex = 5;
            this.lblTempRiseValue.Text = "—";
            this.lblTempRiseValue.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempPressureCaption
            // 
            this.lblTempPressureCaption.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempPressureCaption.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempPressureCaption.Name = "lblTempPressureCaption";
            this.lblTempPressureCaption.Size = new System.Drawing.Size(234, 40);
            this.lblTempPressureCaption.TabIndex = 6;
            this.lblTempPressureCaption.Text = "切割气压(Bar)";
            this.lblTempPressureCaption.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempPressureValue
            // 
            this.lblTempPressureValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempPressureValue.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempPressureValue.Name = "lblTempPressureValue";
            this.lblTempPressureValue.Size = new System.Drawing.Size(234, 40);
            this.lblTempPressureValue.TabIndex = 7;
            this.lblTempPressureValue.Text = "—";
            this.lblTempPressureValue.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTempState
            // 
            this.tlPTemp.SetColumnSpan(this.lblTempState, 2);
            this.lblTempState.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTempState.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblTempState.Name = "lblTempState";
            this.lblTempState.Size = new System.Drawing.Size(474, 40);
            this.lblTempState.TabIndex = 8;
            this.lblTempState.Text = "报警状态：—";
            this.lblTempState.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // timerRefresh
            // 
            this.timerRefresh.Interval = 500;
            // 
            // DeviceMonitorPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "DeviceMonitorPage";
            this.Size = new System.Drawing.Size(1920, 920);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tlPTemp.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private AntdUI.Image3D image3D1;
        private System.Windows.Forms.TableLayoutPanel tlPTemp;
        private AntdUI.Label lblTempLensCaption;
        private AntdUI.Label lblTempLensValue;
        private AntdUI.Label lblTempAmbientCaption;
        private AntdUI.Label lblTempAmbientValue;
        private AntdUI.Label lblTempRiseCaption;
        private AntdUI.Label lblTempRiseValue;
        private AntdUI.Label lblTempPressureCaption;
        private AntdUI.Label lblTempPressureValue;
        private AntdUI.Label lblTempState;
        private System.Windows.Forms.Timer timerRefresh;
    }
}
