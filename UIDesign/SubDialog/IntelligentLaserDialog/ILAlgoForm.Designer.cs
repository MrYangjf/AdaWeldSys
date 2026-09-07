namespace AdaWeldSystem.Sub3UI
{
    partial class ILAlgoForm
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
            this.panelParams = new AntdUI.Panel();
            this.dgvILParams = new AntdUI.Table();
            this.lblConn = new AntdUI.Label();
            this.btnCreate = new AntdUI.Button();
            this.lblJointType = new AntdUI.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new AntdUI.Panel();
            this.flowPanel1 = new AntdUI.FlowPanel();
            this.lblSelectJointType = new AntdUI.Label();
            this.btnDeleteJob = new AntdUI.Button();
            this.btnchangeJob = new AntdUI.Button();
            this.panelParams.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelParams
            // 
            this.panelParams.Back = System.Drawing.Color.White;
            this.panelParams.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.tableLayoutPanel1.SetColumnSpan(this.panelParams, 5);
            this.panelParams.Controls.Add(this.dgvILParams);
            this.panelParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelParams.Location = new System.Drawing.Point(3, 301);
            this.panelParams.Name = "panelParams";
            this.panelParams.Padding = new System.Windows.Forms.Padding(3);
            this.panelParams.Shadow = 5;
            this.panelParams.ShadowColor = System.Drawing.Color.Black;
            this.panelParams.ShadowOpacity = 0.2F;
            this.panelParams.ShadowOpacityAnimation = true;
            this.panelParams.Size = new System.Drawing.Size(816, 212);
            this.panelParams.TabIndex = 1;
            this.panelParams.Text = "Job 参数";
            // 
            // dgvILParams
            // 
            this.dgvILParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvILParams.Gap = 12;
            this.dgvILParams.Location = new System.Drawing.Point(9, 9);
            this.dgvILParams.Name = "dgvILParams";
            this.dgvILParams.Size = new System.Drawing.Size(798, 194);
            this.dgvILParams.TabIndex = 0;
            // 
            // lblConn
            //             this.lblConn.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblConn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblConn.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblConn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.lblConn.Location = new System.Drawing.Point(3, 3);
            this.lblConn.Name = "lblConn";
            this.lblConn.Size = new System.Drawing.Size(160, 23);
            this.lblConn.TabIndex = 0;
            this.lblConn.Text = "英莱设备：未连接";
            // 
            // btnCreate
            // 
            this.btnCreate.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnCreate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCreate.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCreate.Location = new System.Drawing.Point(465, 261);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(174, 34);
            this.btnCreate.TabIndex = 4;
            this.btnCreate.Text = "新建JOB";
            // 
            // lblJointType
            // 
            this.lblJointType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblJointType.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblJointType.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblJointType.Location = new System.Drawing.Point(3, 519);
            this.lblJointType.Name = "lblJointType";
            this.lblJointType.Size = new System.Drawing.Size(174, 34);
            this.lblJointType.TabIndex = 0;
            this.lblJointType.Text = "焊缝类型";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanel1.Controls.Add(this.lblSelectJointType, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblJointType, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.lblConn, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelParams, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.btnCreate, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnDeleteJob, 4, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnchangeJob, 2, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(822, 556);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // panel1
            // 
            this.panel1.Back = System.Drawing.Color.White;
            this.panel1.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.tableLayoutPanel1.SetColumnSpan(this.panel1, 5);
            this.panel1.Controls.Add(this.flowPanel1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 43);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(3);
            this.panel1.Shadow = 5;
            this.panel1.ShadowColor = System.Drawing.Color.Black;
            this.panel1.ShadowOpacity = 0.2F;
            this.panel1.ShadowOpacityAnimation = true;
            this.panel1.Size = new System.Drawing.Size(816, 212);
            this.panel1.TabIndex = 5;
            this.panel1.Text = "Job 参数";
            // 
            // flowPanel1
            // 
            this.flowPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowPanel1.Location = new System.Drawing.Point(9, 9);
            this.flowPanel1.Name = "flowPanel1";
            this.flowPanel1.Size = new System.Drawing.Size(798, 194);
            this.flowPanel1.TabIndex = 0;
            this.flowPanel1.Text = "flowPanel1";
            // 
            // lblSelectJointType
            // 
            this.lblSelectJointType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSelectJointType.Location = new System.Drawing.Point(3, 261);
            this.lblSelectJointType.Name = "lblSelectJointType";
            this.lblSelectJointType.Size = new System.Drawing.Size(174, 34);
            this.lblSelectJointType.TabIndex = 0;
            this.lblSelectJointType.Text = "请选择JOB";
            // 
            // btnDeleteJob
            // 
            this.btnDeleteJob.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnDeleteJob.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnDeleteJob.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnDeleteJob.Location = new System.Drawing.Point(645, 261);
            this.btnDeleteJob.Name = "btnDeleteJob";
            this.btnDeleteJob.Size = new System.Drawing.Size(174, 34);
            this.btnDeleteJob.TabIndex = 6;
            this.btnDeleteJob.Text = "删除JOB";
            // 
            // btnchangeJob
            // 
            this.btnchangeJob.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnchangeJob.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnchangeJob.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnchangeJob.Location = new System.Drawing.Point(285, 261);
            this.btnchangeJob.Name = "btnchangeJob";
            this.btnchangeJob.Size = new System.Drawing.Size(174, 34);
            this.btnchangeJob.TabIndex = 7;
            this.btnchangeJob.Text = "切换JOB";
            // 
            // ILAlgoPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ILAlgoPage";
            this.Size = new System.Drawing.Size(822, 556);
            this.panelParams.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Label lblConn;
        private AntdUI.Panel panelParams;
        private AntdUI.Table dgvILParams;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private AntdUI.Label lblJointType;
        private AntdUI.Button btnCreate;
        private AntdUI.Label lblSelectJointType;
        private AntdUI.Panel panel1;
        private AntdUI.FlowPanel flowPanel1;
        private AntdUI.Button btnDeleteJob;
        private AntdUI.Button btnchangeJob;
    }
}
