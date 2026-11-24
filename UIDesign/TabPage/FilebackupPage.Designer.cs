namespace AdaWeldSystem.UIDesign.TabPage
{
    partial class FilebackupPage
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
            this.panelSystemCfg = new AntdUI.Panel();
            this.gridSystemRoot = new AntdUI.GridPanel();
            this.gridBackupOps = new AntdUI.GridPanel();
            this.gridDeltaConfig = new AntdUI.GridPanel();
            this.lblDeltaTitle = new AntdUI.Label();
            this.segDeltaView = new AntdUI.Segmented();
            this.tableDeltaView = new AntdUI.Table();
            this.lblDeltaStatus = new AntdUI.Label();
            this.btnDeltaGen = new AntdUI.Button();
            this.btnExportPackage = new AntdUI.Button();
            this.btnImportPackage = new AntdUI.Button();
            this.lblSystemBackupTitle = new AntdUI.Label();
            this.lblCsvDir = new AntdUI.Label();
            this.txtCsvDir = new AntdUI.Input();
            this.btnBrowseDir = new AntdUI.Button();
            this.panelSystemCfg.SuspendLayout();
            this.gridSystemRoot.SuspendLayout();
            this.gridBackupOps.SuspendLayout();
            this.gridDeltaConfig.SuspendLayout();
            this.lblDeltaTitle.SuspendLayout();
            this.segDeltaView.SuspendLayout();
            this.tableDeltaView.SuspendLayout();
            this.lblDeltaStatus.SuspendLayout();
            this.btnDeltaGen.SuspendLayout();
            this.btnExportPackage.SuspendLayout();
            this.btnImportPackage.SuspendLayout();
            this.lblSystemBackupTitle.SuspendLayout();
            this.lblCsvDir.SuspendLayout();
            this.txtCsvDir.SuspendLayout();
            this.btnBrowseDir.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelSystemCfg
            // 
            this.panelSystemCfg.AutoScroll = true;
            this.panelSystemCfg.Back = System.Drawing.Color.White;
            this.panelSystemCfg.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelSystemCfg.Controls.Add(this.gridSystemRoot);
            this.panelSystemCfg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelSystemCfg.Location = new System.Drawing.Point(0, 0);
            this.panelSystemCfg.Name = "panelSystemCfg";
            this.panelSystemCfg.Padding = new System.Windows.Forms.Padding(3);
            this.panelSystemCfg.Shadow = 5;
            this.panelSystemCfg.ShadowColor = System.Drawing.Color.Black;
            this.panelSystemCfg.ShadowOpacity = 0.2F;
            this.panelSystemCfg.ShadowOpacityAnimation = true;
            this.panelSystemCfg.Size = new System.Drawing.Size(1762, 889);
            this.panelSystemCfg.TabIndex = 0;
            // 
            // gridSystemRoot
            // 
            this.gridSystemRoot.Controls.Add(this.gridDeltaConfig);
            this.gridSystemRoot.Controls.Add(this.gridBackupOps);
            this.gridSystemRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridSystemRoot.Location = new System.Drawing.Point(9, 9);
            this.gridSystemRoot.Margin = new System.Windows.Forms.Padding(0);
            this.gridSystemRoot.Name = "gridSystemRoot";
            this.gridSystemRoot.Size = new System.Drawing.Size(1744, 871);
            this.gridSystemRoot.Span = "160:fill;fill";
            this.gridSystemRoot.TabIndex = 0;
            // 
            // gridBackupOps
            // 
            this.gridBackupOps.Controls.Add(this.lblDeltaStatus);
            this.gridBackupOps.Controls.Add(this.btnDeltaGen);
            this.gridBackupOps.Controls.Add(this.btnBrowseDir);
            this.gridBackupOps.Controls.Add(this.txtCsvDir);
            this.gridBackupOps.Controls.Add(this.lblCsvDir);
            this.gridBackupOps.Controls.Add(this.lblSystemBackupTitle);
            this.gridBackupOps.Location = new System.Drawing.Point(0, 0);
            this.gridBackupOps.Margin = new System.Windows.Forms.Padding(0);
            this.gridBackupOps.Name = "gridBackupOps";
            this.gridBackupOps.Size = new System.Drawing.Size(1744, 200);
            this.gridBackupOps.Span = "40:fill;40:160 160 160 160 ;80:fill";
            this.gridBackupOps.TabIndex = 0;
            // 
            // gridDeltaConfig
            // 
            this.gridDeltaConfig.Controls.Add(this.btnExportPackage);
            this.gridDeltaConfig.Controls.Add(this.btnImportPackage);
            this.gridDeltaConfig.Controls.Add(this.tableDeltaView);
            this.gridDeltaConfig.Controls.Add(this.segDeltaView);
            this.gridDeltaConfig.Controls.Add(this.lblDeltaTitle);
            this.gridDeltaConfig.Location = new System.Drawing.Point(0, 200);
            this.gridDeltaConfig.Margin = new System.Windows.Forms.Padding(0);
            this.gridDeltaConfig.Name = "gridDeltaConfig";
            this.gridDeltaConfig.Size = new System.Drawing.Size(1744, 671);
            this.gridDeltaConfig.Span = "40:fill;40:fill;320:fill;40:160 160 160";
            this.gridDeltaConfig.TabIndex = 0;
            // 
            // lblDeltaTitle
            // 
            this.lblDeltaTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.lblDeltaTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDeltaTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDeltaTitle.Location = new System.Drawing.Point(3, 3);
            this.lblDeltaTitle.Name = "lblDeltaTitle";
            this.lblDeltaTitle.Size = new System.Drawing.Size(1738, 44);
            this.lblDeltaTitle.TabIndex = 0;
            this.lblDeltaTitle.Text = "台达控制器配置（配置文档 → 项目 XML）";
            // 
            // segDeltaView
            // 
            this.segDeltaView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.segDeltaView.Location = new System.Drawing.Point(3, 53);
            this.segDeltaView.Name = "segDeltaView";
            this.segDeltaView.Size = new System.Drawing.Size(1738, 44);
            this.segDeltaView.TabIndex = 1;
            // 
            // tableDeltaView
            // 
            this.tableDeltaView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableDeltaView.Gap = 12;
            this.tableDeltaView.Location = new System.Drawing.Point(3, 103);
            this.tableDeltaView.Name = "tableDeltaView";
            this.tableDeltaView.Size = new System.Drawing.Size(1738, 394);
            this.tableDeltaView.TabIndex = 2;
            // 
            // lblDeltaStatus
            // 
            this.lblDeltaStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDeltaStatus.Location = new System.Drawing.Point(3, 103);
            this.lblDeltaStatus.Name = "lblDeltaStatus";
            this.lblDeltaStatus.Size = new System.Drawing.Size(1738, 94);
            this.lblDeltaStatus.TabIndex = 3;
            this.lblDeltaStatus.Text = "请选择配置文档目录（目录需含轴/IO/气缸三表 + 焊接头 ESI 与 IO 表）";
            // 
            // btnDeltaGen
            // 
            this.btnDeltaGen.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDeltaGen.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnDeltaGen.Location = new System.Drawing.Point(603, 53);
            this.btnDeltaGen.Name = "btnDeltaGen";
            this.btnDeltaGen.Size = new System.Drawing.Size(194, 44);
            this.btnDeltaGen.TabIndex = 0;
            this.btnDeltaGen.Text = "重载保存";
            this.btnDeltaGen.Type = AntdUI.TTypeMini.Primary;
            this.btnDeltaGen.Click += new System.EventHandler(this.btnDeltaGen_Click);
            // 
            // btnExportPackage
            // 
            this.btnExportPackage.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnExportPackage.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnExportPackage.Location = new System.Drawing.Point(203, 503);
            this.btnExportPackage.Name = "btnExportPackage";
            this.btnExportPackage.Size = new System.Drawing.Size(194, 44);
            this.btnExportPackage.TabIndex = 1;
            this.btnExportPackage.Text = "一键导出设置(.awcfg)";
            this.btnExportPackage.Type = AntdUI.TTypeMini.Success;
            this.btnExportPackage.Click += new System.EventHandler(this.btnExportPackage_Click);
            // 
            // btnImportPackage
            // 
            this.btnImportPackage.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnImportPackage.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnImportPackage.Location = new System.Drawing.Point(3, 503);
            this.btnImportPackage.Name = "btnImportPackage";
            this.btnImportPackage.Size = new System.Drawing.Size(194, 44);
            this.btnImportPackage.TabIndex = 2;
            this.btnImportPackage.Text = "一键导入设置(.awcfg)";
            this.btnImportPackage.Type = AntdUI.TTypeMini.Warn;
            this.btnImportPackage.Click += new System.EventHandler(this.btnImportPackage_Click);
            // 
            // lblSystemBackupTitle
            // 
            this.lblSystemBackupTitle.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.lblSystemBackupTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSystemBackupTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSystemBackupTitle.Location = new System.Drawing.Point(3, 3);
            this.lblSystemBackupTitle.Name = "lblSystemBackupTitle";
            this.lblSystemBackupTitle.Size = new System.Drawing.Size(1738, 44);
            this.lblSystemBackupTitle.TabIndex = 0;
            this.lblSystemBackupTitle.Text = "系统配置文件导入导出与备份";
            // 
            // lblCsvDir
            // 
            this.lblCsvDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCsvDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCsvDir.Location = new System.Drawing.Point(3, 53);
            this.lblCsvDir.Name = "lblCsvDir";
            this.lblCsvDir.Size = new System.Drawing.Size(194, 44);
            this.lblCsvDir.TabIndex = 1;
            this.lblCsvDir.Text = "配置文档目录";
            // 
            // txtCsvDir
            // 
            this.txtCsvDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCsvDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtCsvDir.Location = new System.Drawing.Point(203, 53);
            this.txtCsvDir.MinimumSize = new System.Drawing.Size(1, 1);
            this.txtCsvDir.Name = "txtCsvDir";
            this.txtCsvDir.PlaceholderText = "选择存放轴/IO/气缸 CSV + 焊接头 ESI 与 IO 表的目录";
            this.txtCsvDir.Size = new System.Drawing.Size(194, 44);
            this.txtCsvDir.TabIndex = 2;
            // 
            // btnBrowseDir
            // 
            this.btnBrowseDir.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBrowseDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBrowseDir.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBrowseDir.Location = new System.Drawing.Point(403, 53);
            this.btnBrowseDir.Name = "btnBrowseDir";
            this.btnBrowseDir.Size = new System.Drawing.Size(194, 44);
            this.btnBrowseDir.TabIndex = 3;
            this.btnBrowseDir.Text = "选择目录";
            this.btnBrowseDir.Click += new System.EventHandler(this.btnBrowseDir_Click);
            // 
            // FilebackupPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.panelSystemCfg);
            this.Name = "FilebackupPage";
            this.Size = new System.Drawing.Size(1762, 920);
            this.panelSystemCfg.ResumeLayout(false);
            this.gridSystemRoot.ResumeLayout(false);
            this.gridDeltaConfig.ResumeLayout(false);
            this.gridBackupOps.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Panel panelSystemCfg;
        private AntdUI.GridPanel gridSystemRoot;
        private AntdUI.GridPanel gridBackupOps;
        private AntdUI.GridPanel gridDeltaConfig;
        private AntdUI.Label lblDeltaTitle;
        private AntdUI.Segmented segDeltaView;
        private AntdUI.Table tableDeltaView;
        private AntdUI.Label lblDeltaStatus;
        private AntdUI.Button btnDeltaGen;
        private AntdUI.Button btnExportPackage;
        private AntdUI.Button btnImportPackage;
        private AntdUI.Label lblSystemBackupTitle;
        private AntdUI.Label lblCsvDir;
        private AntdUI.Input txtCsvDir;
        private AntdUI.Button btnBrowseDir;
    }
}
