namespace AdaWeldSystem.Sub2UI
{
    partial class MonitorCamPage
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelRight = new System.Windows.Forms.Panel();
            this.txtResult = new AntdUI.Input();
            this.btnStopMonitor = new AntdUI.Button();
            this.btnStartMonitor = new AntdUI.Button();
            this.btnSingleCheck = new AntdUI.Button();
            this.cmbPhase = new AntdUI.Select();
            this.lblStatus = new AntdUI.Label();
            this.uiLightStatus = new AntdUI.Signal();
            this.uiGroupBoxAlgorithm = new System.Windows.Forms.GroupBox();
            this.tableLayoutAlgorithm = new System.Windows.Forms.TableLayoutPanel();
            this.lblTolX = new AntdUI.Label();
            this.txtTolX = new AntdUI.Input();
            this.lblTolY = new AntdUI.Label();
            this.txtTolY = new AntdUI.Input();
            this.lblTolAngle = new AntdUI.Label();
            this.txtTolAngle = new AntdUI.Input();
            this.lblPassScore = new AntdUI.Label();
            this.txtPassScore = new AntdUI.Input();
            this.btnSaveAlgorithm = new AntdUI.Button();
            this.imageBox1 = new Emgu.CV.UI.ImageBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.uiGroupBoxAlgorithm.SuspendLayout();
            this.tableLayoutAlgorithm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 320F));
            this.tableLayoutPanel1.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.imageBox1, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1025, 600);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // panelRight
            // 
            this.panelRight.Controls.Add(this.uiGroupBoxAlgorithm);
            this.panelRight.Controls.Add(this.txtResult);
            this.panelRight.Controls.Add(this.btnStopMonitor);
            this.panelRight.Controls.Add(this.btnStartMonitor);
            this.panelRight.Controls.Add(this.btnSingleCheck);
            this.panelRight.Controls.Add(this.cmbPhase);
            this.panelRight.Controls.Add(this.lblStatus);
            this.panelRight.Controls.Add(this.uiLightStatus);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panelRight.Location = new System.Drawing.Point(709, 5);
            this.panelRight.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.panelRight.MinimumSize = new System.Drawing.Size(1, 1);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(312, 590);
            this.panelRight.TabIndex = 0;
            this.panelRight.Text = "监控调试";
            // 
            // 
            // txtResult
            // 
            this.txtResult.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtResult.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtResult.Location = new System.Drawing.Point(10, 125);
            this.txtResult.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtResult.MinimumSize = new System.Drawing.Size(1, 1);
            this.txtResult.Multiline = true;
            this.txtResult.Name = "txtResult";
            this.txtResult.ReadOnly = true;
            this.txtResult.Size = new System.Drawing.Size(296, 70);
            this.txtResult.TabIndex = 10;
            this.txtResult.Text = "判定: --";
            // 
            // btnStopMonitor
            // 
            this.btnStopMonitor.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStopMonitor.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopMonitor.Location = new System.Drawing.Point(210, 80);
            this.btnStopMonitor.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStopMonitor.Name = "btnStopMonitor";
            this.btnStopMonitor.Size = new System.Drawing.Size(96, 35);
            this.btnStopMonitor.TabIndex = 9;
            this.btnStopMonitor.Text = "停止";
            this.btnStopMonitor.Click += new System.EventHandler(this.btnStopMonitor_Click);
            // 
            // btnStartMonitor
            // 
            this.btnStartMonitor.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartMonitor.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartMonitor.Location = new System.Drawing.Point(110, 80);
            this.btnStartMonitor.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStartMonitor.Name = "btnStartMonitor";
            this.btnStartMonitor.Size = new System.Drawing.Size(96, 35);
            this.btnStartMonitor.TabIndex = 8;
            this.btnStartMonitor.Text = "连续监控";
            this.btnStartMonitor.Click += new System.EventHandler(this.btnStartMonitor_Click);
            // 
            // btnSingleCheck
            // 
            this.btnSingleCheck.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSingleCheck.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSingleCheck.Location = new System.Drawing.Point(10, 80);
            this.btnSingleCheck.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSingleCheck.Name = "btnSingleCheck";
            this.btnSingleCheck.Size = new System.Drawing.Size(96, 35);
            this.btnSingleCheck.TabIndex = 7;
            this.btnSingleCheck.Text = "单次检测";
            this.btnSingleCheck.Click += new System.EventHandler(this.btnSingleCheck_Click);
            // 
            // cmbPhase
            // 
            this.cmbPhase.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbPhase.Location = new System.Drawing.Point(10, 40);
            this.cmbPhase.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbPhase.MinimumSize = new System.Drawing.Size(63, 0);
            this.cmbPhase.Name = "cmbPhase";
            this.cmbPhase.Padding = new System.Windows.Forms.Padding(0, 0, 30, 2);
            this.cmbPhase.Size = new System.Drawing.Size(296, 29);
            this.cmbPhase.TabIndex = 6;
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblStatus.Location = new System.Drawing.Point(10, 10);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(296, 20);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "相机: 未连接";
            // 
            // uiLightStatus
            // 
            this.uiLightStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiLightStatus.Location = new System.Drawing.Point(280, 10);
            this.uiLightStatus.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiLightStatus.Name = "uiLightStatus";
            this.uiLightStatus.Size = new System.Drawing.Size(25, 20);
            this.uiLightStatus.TabIndex = 4;
            // 
            // uiGroupBoxAlgorithm
            // 
            this.uiGroupBoxAlgorithm.Controls.Add(this.tableLayoutAlgorithm);
            this.uiGroupBoxAlgorithm.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiGroupBoxAlgorithm.Location = new System.Drawing.Point(10, 205);
            this.uiGroupBoxAlgorithm.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiGroupBoxAlgorithm.Name = "uiGroupBoxAlgorithm";
            this.uiGroupBoxAlgorithm.Size = new System.Drawing.Size(292, 195);
            this.uiGroupBoxAlgorithm.TabIndex = 19;
            this.uiGroupBoxAlgorithm.Text = "算法参数";
            // 
            // tableLayoutAlgorithm
            // 
            this.tableLayoutAlgorithm.ColumnCount = 2;
            this.tableLayoutAlgorithm.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.tableLayoutAlgorithm.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutAlgorithm.Controls.Add(this.lblTolX, 0, 0);
            this.tableLayoutAlgorithm.Controls.Add(this.txtTolX, 1, 0);
            this.tableLayoutAlgorithm.Controls.Add(this.lblTolY, 0, 1);
            this.tableLayoutAlgorithm.Controls.Add(this.txtTolY, 1, 1);
            this.tableLayoutAlgorithm.Controls.Add(this.lblTolAngle, 0, 2);
            this.tableLayoutAlgorithm.Controls.Add(this.txtTolAngle, 1, 2);
            this.tableLayoutAlgorithm.Controls.Add(this.lblPassScore, 0, 3);
            this.tableLayoutAlgorithm.Controls.Add(this.txtPassScore, 1, 3);
            this.tableLayoutAlgorithm.Controls.Add(this.btnSaveAlgorithm, 0, 4);
            this.tableLayoutAlgorithm.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutAlgorithm.Location = new System.Drawing.Point(0, 24);
            this.tableLayoutAlgorithm.Name = "tableLayoutAlgorithm";
            this.tableLayoutAlgorithm.RowCount = 5;
            this.tableLayoutAlgorithm.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutAlgorithm.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutAlgorithm.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutAlgorithm.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutAlgorithm.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutAlgorithm.Size = new System.Drawing.Size(292, 171);
            this.tableLayoutAlgorithm.TabIndex = 0;
            // 
            // lblTolX
            // 
            this.lblTolX.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTolX.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTolX.Location = new System.Drawing.Point(3, 7);
            this.lblTolX.Name = "lblTolX";
            this.lblTolX.Size = new System.Drawing.Size(110, 18);
            this.lblTolX.TabIndex = 0;
            this.lblTolX.Text = "对中容差X(px)";
            // 
            // txtTolX
            // 
            this.txtTolX.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.txtTolX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTolX.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTolX.Location = new System.Drawing.Point(119, 4);
            this.txtTolX.Name = "txtTolX";
            this.txtTolX.Size = new System.Drawing.Size(165, 23);
            this.txtTolX.TabIndex = 1;
            // 
            // lblTolY
            // 
            this.lblTolY.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTolY.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTolY.Location = new System.Drawing.Point(3, 37);
            this.lblTolY.Name = "lblTolY";
            this.lblTolY.Size = new System.Drawing.Size(110, 18);
            this.lblTolY.TabIndex = 2;
            this.lblTolY.Text = "对中容差Y(px)";
            // 
            // txtTolY
            // 
            this.txtTolY.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTolY.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTolY.Location = new System.Drawing.Point(119, 34);
            this.txtTolY.Name = "txtTolY";
            this.txtTolY.Size = new System.Drawing.Size(165, 23);
            this.txtTolY.TabIndex = 3;
            // 
            // lblTolAngle
            // 
            this.lblTolAngle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTolAngle.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTolAngle.Location = new System.Drawing.Point(3, 67);
            this.lblTolAngle.Name = "lblTolAngle";
            this.lblTolAngle.Size = new System.Drawing.Size(110, 18);
            this.lblTolAngle.TabIndex = 4;
            this.lblTolAngle.Text = "对中角度(°)";
            // 
            // txtTolAngle
            // 
            this.txtTolAngle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTolAngle.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtTolAngle.Location = new System.Drawing.Point(119, 64);
            this.txtTolAngle.Name = "txtTolAngle";
            this.txtTolAngle.Size = new System.Drawing.Size(165, 23);
            this.txtTolAngle.TabIndex = 5;
            // 
            // lblPassScore
            // 
            this.lblPassScore.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPassScore.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPassScore.Location = new System.Drawing.Point(3, 97);
            this.lblPassScore.Name = "lblPassScore";
            this.lblPassScore.Size = new System.Drawing.Size(110, 18);
            this.lblPassScore.TabIndex = 6;
            this.lblPassScore.Text = "合格分数";
            // 
            // txtPassScore
            // 
            this.txtPassScore.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtPassScore.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtPassScore.Location = new System.Drawing.Point(119, 94);
            this.txtPassScore.Name = "txtPassScore";
            this.txtPassScore.Size = new System.Drawing.Size(165, 23);
            this.txtPassScore.TabIndex = 7;
            // 
            // btnSaveAlgorithm
            // 
            this.tableLayoutAlgorithm.SetColumnSpan(this.btnSaveAlgorithm, 2);
            this.btnSaveAlgorithm.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveAlgorithm.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSaveAlgorithm.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSaveAlgorithm.Location = new System.Drawing.Point(3, 124);
            this.btnSaveAlgorithm.Name = "btnSaveAlgorithm";
            this.btnSaveAlgorithm.Size = new System.Drawing.Size(286, 34);
            this.btnSaveAlgorithm.TabIndex = 8;
            this.btnSaveAlgorithm.Text = "保存算法参数";
            this.btnSaveAlgorithm.Click += new System.EventHandler(this.btnSaveAlgorithm_Click);
            // 
            // imageBox1
            // 
            this.imageBox1.BackColor = System.Drawing.Color.Silver;
            this.imageBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageBox1.FunctionalMode = Emgu.CV.UI.ImageBox.FunctionalModeOption.RightClickMenu;
            this.imageBox1.Location = new System.Drawing.Point(3, 3);
            this.imageBox1.Name = "imageBox1";
            this.imageBox1.Size = new System.Drawing.Size(699, 594);
            this.imageBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.imageBox1.TabIndex = 1;
            this.imageBox1.TabStop = false;
            // 
            // MonitorCamPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1025, 600);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "MonitorCamPage";
            this.Text = "监控相机";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.panelRight.PerformLayout();
            this.uiGroupBoxAlgorithm.ResumeLayout(false);
            this.tableLayoutAlgorithm.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panelRight;
        private Emgu.CV.UI.ImageBox imageBox1;
        private AntdUI.Signal uiLightStatus;
        private AntdUI.Label lblStatus;
        private AntdUI.Select cmbPhase;
        private AntdUI.Button btnSingleCheck;
        private AntdUI.Button btnStartMonitor;
        private AntdUI.Button btnStopMonitor;
        private AntdUI.Input txtResult;
        private System.Windows.Forms.GroupBox uiGroupBoxAlgorithm;
        private System.Windows.Forms.TableLayoutPanel tableLayoutAlgorithm;
        private AntdUI.Label lblTolX;
        private AntdUI.Input txtTolX;
        private AntdUI.Label lblTolY;
        private AntdUI.Input txtTolY;
        private AntdUI.Label lblTolAngle;
        private AntdUI.Input txtTolAngle;
        private AntdUI.Label lblPassScore;
        private AntdUI.Input txtPassScore;
        private AntdUI.Button btnSaveAlgorithm;
    }
}
