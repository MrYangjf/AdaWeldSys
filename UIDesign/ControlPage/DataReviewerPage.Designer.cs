namespace AdaWeldSystem.UIDesign.ControlPage
{
    partial class DataReviewerPage
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
            this.panelRight = new AntdUI.Panel();
            this.uiGridPanelRight = new AntdUI.GridPanel();
            this.btnRenerView = new AntdUI.Button();
            this.uiSymbolLabel1 = new AntdUI.Label();
            this.formsPlotData = new ScottPlot.FormsPlot();
            this.SelectFlowPanel = new AntdUI.FlowPanel();
            this.selSeriesGroup = new AntdUI.Select();
            this.uiDataGridView1 = new AntdUI.Table();
            this.pagination1 = new AntdUI.Pagination();
            this.panelLeft = new AntdUI.Panel();
            this.gridPanel1 = new AntdUI.GridPanel();
            this.BtnRecordSetting = new AntdUI.Button();
            this.divider2 = new AntdUI.Divider();
            this.btnSelectInvert = new AntdUI.Button();
            this.btnSelectNone = new AntdUI.Button();
            this.btnSelectAll = new AntdUI.Button();
            this.btnRefreshDate = new AntdUI.Button();
            this.uiDateRange = new AntdUI.DatePickerRange();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.panelRight.SuspendLayout();
            this.uiGridPanelRight.SuspendLayout();
            this.SelectFlowPanel.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.gridPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelRight
            // 
            this.panelRight.Back = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.panelRight.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelRight.Controls.Add(this.uiGridPanelRight);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(387, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Padding = new System.Windows.Forms.Padding(5);
            this.panelRight.Shadow = 5;
            this.panelRight.ShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.panelRight.ShadowOpacity = 0.2F;
            this.panelRight.ShadowOpacityAnimation = true;
            this.panelRight.Size = new System.Drawing.Size(1530, 914);
            this.panelRight.TabIndex = 3;
            this.panelRight.Text = "panelRight";
            // 
            // uiGridPanelRight
            // 
            this.uiGridPanelRight.Controls.Add(this.btnRenerView);
            this.uiGridPanelRight.Controls.Add(this.uiSymbolLabel1);
            this.uiGridPanelRight.Controls.Add(this.formsPlotData);
            this.uiGridPanelRight.Controls.Add(this.SelectFlowPanel);
            this.uiGridPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiGridPanelRight.Location = new System.Drawing.Point(11, 11);
            this.uiGridPanelRight.Name = "uiGridPanelRight";
            this.uiGridPanelRight.Size = new System.Drawing.Size(1508, 892);
            this.uiGridPanelRight.Span = "fill;fill;fill 120;-35 fill 35";
            this.uiGridPanelRight.TabIndex = 0;
            // 
            // btnRenerView
            // 
            this.btnRenerView.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnRenerView.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRenerView.Ghost = true;
            this.btnRenerView.IconRatio = 1F;
            this.btnRenerView.IconSvg = "YoutubeOutlined";
            this.btnRenerView.Location = new System.Drawing.Point(1361, 851);
            this.btnRenerView.Name = "btnRenerView";
            this.btnRenerView.Size = new System.Drawing.Size(144, 38);
            this.btnRenerView.TabIndex = 14;
            this.btnRenerView.Text = "3D数据回溯";
            this.btnRenerView.Click += new System.EventHandler(this.btnRenerView_Click);
            // 
            // uiSymbolLabel1
            // 
            this.uiSymbolLabel1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel1.Location = new System.Drawing.Point(3, 851);
            this.uiSymbolLabel1.Name = "uiSymbolLabel1";
            this.uiSymbolLabel1.Size = new System.Drawing.Size(1352, 38);
            this.uiSymbolLabel1.TabIndex = 12;
            // 
            // formsPlotData
            // 
            this.formsPlotData.BackColor = System.Drawing.Color.White;
            this.formsPlotData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlotData.Location = new System.Drawing.Point(5, 47);
            this.formsPlotData.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.formsPlotData.Name = "formsPlotData";
            this.formsPlotData.Size = new System.Drawing.Size(1498, 798);
            this.formsPlotData.TabIndex = 2;
            // 
            // SelectFlowPanel
            // 
            this.SelectFlowPanel.Controls.Add(this.selSeriesGroup);
            this.SelectFlowPanel.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.SelectFlowPanel.Location = new System.Drawing.Point(3, 3);
            this.SelectFlowPanel.Name = "SelectFlowPanel";
            this.SelectFlowPanel.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            this.SelectFlowPanel.Size = new System.Drawing.Size(1502, 38);
            this.SelectFlowPanel.TabIndex = 13;
            // 
            // selSeriesGroup
            // 
            this.selSeriesGroup.DropDownArrow = true;
            this.selSeriesGroup.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.selSeriesGroup.Location = new System.Drawing.Point(9, 5);
            this.selSeriesGroup.Name = "selSeriesGroup";
            this.selSeriesGroup.Size = new System.Drawing.Size(161, 24);
            this.selSeriesGroup.TabIndex = 21;
            // 
            // uiDataGridView1
            // 
            this.uiDataGridView1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Location = new System.Drawing.Point(3, 103);
            this.uiDataGridView1.Name = "uiDataGridView1";
            this.uiDataGridView1.Size = new System.Drawing.Size(354, 678);
            this.uiDataGridView1.TabIndex = 9;
            // 
            // pagination1
            // 
            this.pagination1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.pagination1.Location = new System.Drawing.Point(3, 799);
            this.pagination1.Name = "pagination1";
            this.pagination1.PageSize = 50;
            this.pagination1.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.pagination1.Size = new System.Drawing.Size(354, 44);
            this.pagination1.SizeChangerWidth = 80;
            this.pagination1.TabIndex = 5;
            // 
            // panelLeft
            // 
            this.panelLeft.Back = System.Drawing.Color.White;
            this.panelLeft.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelLeft.Controls.Add(this.gridPanel1);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(3, 3);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(3);
            this.panelLeft.Shadow = 5;
            this.panelLeft.ShadowColor = System.Drawing.Color.Black;
            this.panelLeft.ShadowOpacityAnimation = true;
            this.panelLeft.Size = new System.Drawing.Size(378, 914);
            this.panelLeft.TabIndex = 2;
            this.panelLeft.Text = "panelLeft";
            // 
            // gridPanel1
            // 
            this.gridPanel1.Controls.Add(this.BtnRecordSetting);
            this.gridPanel1.Controls.Add(this.pagination1);
            this.gridPanel1.Controls.Add(this.divider2);
            this.gridPanel1.Controls.Add(this.uiDataGridView1);
            this.gridPanel1.Controls.Add(this.btnSelectInvert);
            this.gridPanel1.Controls.Add(this.btnSelectNone);
            this.gridPanel1.Controls.Add(this.btnSelectAll);
            this.gridPanel1.Controls.Add(this.btnRefreshDate);
            this.gridPanel1.Controls.Add(this.uiDateRange);
            this.gridPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPanel1.Location = new System.Drawing.Point(9, 9);
            this.gridPanel1.Name = "gridPanel1";
            this.gridPanel1.Size = new System.Drawing.Size(360, 896);
            this.gridPanel1.Span = "40:100% 30;40:33% 33% 33%;100%:fill;10:fill;40:fill;40:fill";
            this.gridPanel1.TabIndex = 18;
            this.gridPanel1.Text = "gridPanel1";
            // 
            // BtnRecordSetting
            // 
            this.BtnRecordSetting.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.BtnRecordSetting.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.BtnRecordSetting.Ghost = true;
            this.BtnRecordSetting.IconRatio = 1F;
            this.BtnRecordSetting.IconSvg = "ProfileOutlined";
            this.BtnRecordSetting.Location = new System.Drawing.Point(3, 849);
            this.BtnRecordSetting.Name = "BtnRecordSetting";
            this.BtnRecordSetting.Size = new System.Drawing.Size(354, 44);
            this.BtnRecordSetting.TabIndex = 15;
            this.BtnRecordSetting.Text = "记录设置";
            this.BtnRecordSetting.Click += new System.EventHandler(this.BtnRecordSetting_Click);
            // 
            // divider2
            // 
            this.divider2.ColorSplit = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(172)))), ((int)(((byte)(248)))));
            this.divider2.Location = new System.Drawing.Point(3, 787);
            this.divider2.Name = "divider2";
            this.divider2.OrientationMargin = 0F;
            this.divider2.Size = new System.Drawing.Size(354, 6);
            this.divider2.TabIndex = 4;
            this.divider2.Text = "";
            // 
            // btnSelectInvert
            // 
            this.btnSelectInvert.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSelectInvert.Location = new System.Drawing.Point(239, 50);
            this.btnSelectInvert.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this.btnSelectInvert.Name = "btnSelectInvert";
            this.btnSelectInvert.Size = new System.Drawing.Size(117, 50);
            this.btnSelectInvert.TabIndex = 2;
            this.btnSelectInvert.Text = "反选";
            this.btnSelectInvert.Click += new System.EventHandler(this.btnSelectInvert_Click);
            // 
            // btnSelectNone
            // 
            this.btnSelectNone.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSelectNone.Location = new System.Drawing.Point(120, 50);
            this.btnSelectNone.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this.btnSelectNone.Name = "btnSelectNone";
            this.btnSelectNone.Size = new System.Drawing.Size(117, 50);
            this.btnSelectNone.TabIndex = 1;
            this.btnSelectNone.Text = "全取消";
            this.btnSelectNone.Click += new System.EventHandler(this.btnSelectNone_Click);
            // 
            // btnSelectAll
            // 
            this.btnSelectAll.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSelectAll.Location = new System.Drawing.Point(1, 50);
            this.btnSelectAll.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(117, 50);
            this.btnSelectAll.TabIndex = 0;
            this.btnSelectAll.Text = "全选";
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            // 
            // btnRefreshDate
            // 
            this.btnRefreshDate.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnRefreshDate.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRefreshDate.Ghost = true;
            this.btnRefreshDate.IconRatio = 1F;
            this.btnRefreshDate.IconSvg = "ReloadOutlined";
            this.btnRefreshDate.Location = new System.Drawing.Point(325, 3);
            this.btnRefreshDate.Name = "btnRefreshDate";
            this.btnRefreshDate.Size = new System.Drawing.Size(32, 44);
            this.btnRefreshDate.TabIndex = 17;
            this.btnRefreshDate.Click += new System.EventHandler(this.btnRefreshDate_Click);
            // 
            // uiDateRange
            // 
            this.uiDateRange.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiDateRange.InteractiveReset = false;
            this.uiDateRange.Location = new System.Drawing.Point(3, 3);
            this.uiDateRange.Name = "uiDateRange";
            this.uiDateRange.PlaceholderEnd = "结束日期";
            this.uiDateRange.PlaceholderStart = "开始日期";
            this.uiDateRange.Size = new System.Drawing.Size(316, 44);
            this.uiDateRange.TabIndex = 1;
            this.uiDateRange.WaveSize = 0;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 80F));
            this.tableLayoutPanel2.Controls.Add(this.panelLeft, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(1920, 920);
            this.tableLayoutPanel2.TabIndex = 6;
            // 
            // DataReviewerPage
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "DataReviewerPage";
            this.Size = new System.Drawing.Size(1920, 920);
            this.panelRight.ResumeLayout(false);
            this.uiGridPanelRight.ResumeLayout(false);
            this.SelectFlowPanel.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.gridPanel1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Panel panelRight;
        private AntdUI.GridPanel uiGridPanelRight;
        private AntdUI.FlowPanel SelectFlowPanel;
        private AntdUI.Select selSeriesGroup;
        private AntdUI.Table uiDataGridView1;
        private AntdUI.Pagination pagination1;
        private AntdUI.Panel panelLeft;
        private AntdUI.Button btnRefreshDate;
        private AntdUI.DatePickerRange uiDateRange;
        private AntdUI.Button btnRenerView;
        private ScottPlot.FormsPlot formsPlotData;
        private AntdUI.Button BtnRecordSetting;
        private AntdUI.Button btnSelectAll;
        private AntdUI.Button btnSelectNone;
        private AntdUI.Button btnSelectInvert;
        private AntdUI.Divider divider2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private AntdUI.Label uiSymbolLabel1;
        private AntdUI.GridPanel gridPanel1;
    }
}
