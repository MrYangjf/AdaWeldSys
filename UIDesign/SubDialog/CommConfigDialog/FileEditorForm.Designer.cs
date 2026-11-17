namespace AdaWeldSystem.UIDesign.SubDialog.CommConfigDialog
{
    partial class FileEditorForm
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
            this.panelRight = new AntdUI.Panel();
            this._xmlPropertyGrid = new AntdUI.Table();
            this.panelLeft = new AntdUI.Panel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._btnExpandAll = new AntdUI.Button();
            this._xmlTreeView = new AntdUI.Tree();
            this._btnSave = new AntdUI.Button();
            this._btnRefresh = new AntdUI.Button();
            this._btnCollapseAll = new AntdUI.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelRight.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelRight
            // 
            this.panelRight.Back = System.Drawing.Color.White;
            this.panelRight.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelRight.Controls.Add(this._xmlPropertyGrid);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(304, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Padding = new System.Windows.Forms.Padding(3);
            this.panelRight.Shadow = 5;
            this.panelRight.ShadowColor = System.Drawing.Color.Black;
            this.panelRight.ShadowOpacity = 0.2F;
            this.panelRight.ShadowOpacityAnimation = true;
            this.panelRight.Size = new System.Drawing.Size(659, 602);
            this.panelRight.TabIndex = 1;
            this.panelRight.Text = "属性编辑";
            // 
            // _xmlPropertyGrid
            // 
            this._xmlPropertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._xmlPropertyGrid.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._xmlPropertyGrid.Gap = 12;
            this._xmlPropertyGrid.Location = new System.Drawing.Point(9, 9);
            this._xmlPropertyGrid.Name = "_xmlPropertyGrid";
            this._xmlPropertyGrid.Size = new System.Drawing.Size(641, 584);
            this._xmlPropertyGrid.TabIndex = 0;
            // 
            // panelLeft
            // 
            this.panelLeft.Back = System.Drawing.Color.White;
            this.panelLeft.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.panelLeft.Controls.Add(this.tableLayoutPanel2);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(3, 3);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(3);
            this.panelLeft.Shadow = 5;
            this.panelLeft.ShadowColor = System.Drawing.Color.Black;
            this.panelLeft.ShadowOpacity = 0.2F;
            this.panelLeft.ShadowOpacityAnimation = true;
            this.panelLeft.Size = new System.Drawing.Size(295, 602);
            this.panelLeft.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 5;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.Controls.Add(this._btnExpandAll, 3, 0);
            this.tableLayoutPanel2.Controls.Add(this._xmlTreeView, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._btnSave, 4, 2);
            this.tableLayoutPanel2.Controls.Add(this._btnRefresh, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._btnCollapseAll, 4, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(9, 9);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(277, 584);
            this.tableLayoutPanel2.TabIndex = 1;
            // 
            // _btnExpandAll
            // 
            this._btnExpandAll.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this._btnExpandAll.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnExpandAll.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnExpandAll.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnExpandAll.Ghost = true;
            this._btnExpandAll.IconSvg = "NodeExpandOutlined";
            this._btnExpandAll.Location = new System.Drawing.Point(168, 3);
            this._btnExpandAll.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnExpandAll.Name = "_btnExpandAll";
            this._btnExpandAll.Size = new System.Drawing.Size(49, 34);
            this._btnExpandAll.TabIndex = 3;
            this._btnExpandAll.Text = "展开";
            this._btnExpandAll.Click += new System.EventHandler(this.BtnExpandAll_Click);
            // 
            // _xmlTreeView
            // 
            this.tableLayoutPanel2.SetColumnSpan(this._xmlTreeView, 5);
            this._xmlTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._xmlTreeView.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._xmlTreeView.Location = new System.Drawing.Point(3, 43);
            this._xmlTreeView.MinimumSize = new System.Drawing.Size(1, 1);
            this._xmlTreeView.Name = "_xmlTreeView";
            this._xmlTreeView.Size = new System.Drawing.Size(271, 498);
            this._xmlTreeView.TabIndex = 1;
            this._xmlTreeView.SelectChanged += new AntdUI.TreeSelectEventHandler(this.XmlTreeView_SelectChanged);
            // 
            // _btnSave
            // 
            this._btnSave.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this._btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnSave.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnSave.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnSave.Ghost = true;
            this._btnSave.IconSvg = "SaveOutlined";
            this._btnSave.Location = new System.Drawing.Point(223, 547);
            this._btnSave.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnSave.Name = "_btnSave";
            this._btnSave.Size = new System.Drawing.Size(51, 34);
            this._btnSave.TabIndex = 2;
            this._btnSave.Text = "保存";
            this._btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            // 
            // _btnRefresh
            // 
            this._btnRefresh.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this._btnRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnRefresh.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnRefresh.Ghost = true;
            this._btnRefresh.IconSvg = "ReloadOutlined";
            this._btnRefresh.Location = new System.Drawing.Point(3, 547);
            this._btnRefresh.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnRefresh.Name = "_btnRefresh";
            this._btnRefresh.Size = new System.Drawing.Size(49, 34);
            this._btnRefresh.TabIndex = 1;
            this._btnRefresh.Text = "刷新";
            this._btnRefresh.Click += new System.EventHandler(this.BtnRefresh_Click);
            // 
            // _btnCollapseAll
            // 
            this._btnCollapseAll.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this._btnCollapseAll.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnCollapseAll.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnCollapseAll.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnCollapseAll.Ghost = true;
            this._btnCollapseAll.IconSvg = "NodeCollapseOutlined";
            this._btnCollapseAll.Location = new System.Drawing.Point(223, 3);
            this._btnCollapseAll.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnCollapseAll.Name = "_btnCollapseAll";
            this._btnCollapseAll.Size = new System.Drawing.Size(51, 34);
            this._btnCollapseAll.TabIndex = 4;
            this._btnCollapseAll.Text = "折叠";
            this._btnCollapseAll.Click += new System.EventHandler(this.BtnCollapseAll_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31.15942F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 68.84058F));
            this.tableLayoutPanel1.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelLeft, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(966, 608);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // FileEditorForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "FileEditorForm";
            this.Size = new System.Drawing.Size(966, 608);
            this.panelRight.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Panel panelLeft;
        private AntdUI.Panel panelRight;
        private AntdUI.Button _btnRefresh;
        private AntdUI.Button _btnSave;
        private AntdUI.Button _btnExpandAll;
        private AntdUI.Button _btnCollapseAll;
        private AntdUI.Tree _xmlTreeView;
        private AntdUI.Table _xmlPropertyGrid;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
    }
}
