using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    partial class RenderParamForm
    {
        #region 组件设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.uiGroupBox1 = new System.Windows.Forms.GroupBox();
            this.uiComboBox1 = new AntdUI.Select();
            this.uiGroupBox2 = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelParam = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelButtons = new System.Windows.Forms.TableLayoutPanel();
            this.btnSave = new AntdUI.Button();
            this.btnCancel = new AntdUI.Button();
            this.uiGroupBox3 = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelCloud = new System.Windows.Forms.TableLayoutPanel();
            this.uiSymbolLabelPointSize = new AntdUI.Label();
            this.PointSize = new AntdUI.InputNumber();
            this.uiSymbolLabelDisplayRatio = new AntdUI.Label();
            this.DisplayRatio = new AntdUI.InputNumber();
            this.ZColorEnabled = new AntdUI.Checkbox();
            this.uiSymbolLabelNeighborRadius = new AntdUI.Label();
            this.NeighborSearchRadius = new AntdUI.InputNumber();
            this.uiSymbolLabelNeighborMaxNeighbors = new AntdUI.Label();
            this.NeighborMaxNeighbors = new AntdUI.InputNumber();
            this.uiSymbolLabelNeighborSampleStep = new AntdUI.Label();
            this.NeighborSampleStep = new AntdUI.InputNumber();
            this.uiSymbolLabelBallRadius = new AntdUI.Label();
            this.BallPivotingRadius = new AntdUI.InputNumber();
            this.uiSymbolLabelBallSampleStep = new AntdUI.Label();
            this.BallPivotingSampleStep = new AntdUI.InputNumber();
            this.uiSymbolLabelBallMaxNeighbors = new AntdUI.Label();
            this.BallPivotingMaxNeighbors = new AntdUI.InputNumber();
            this.uiSymbolLabelAlphaValue = new AntdUI.Label();
            this.AlphaShapeAlpha = new AntdUI.InputNumber();
            this.uiSymbolLabelAlphaSampleStep = new AntdUI.Label();
            this.AlphaShapeSampleStep = new AntdUI.InputNumber();
            this.uiSymbolLabelAlphaMaxNeighbors = new AntdUI.Label();
            this.AlphaShapeMaxNeighbors = new AntdUI.InputNumber();
            this.uiSymbolLabelDelaunayQuality = new AntdUI.Label();
            this.DelaunayQualityThreshold = new AntdUI.InputNumber();
            this.uiSymbolLabelDelaunayRadiusMultiplier = new AntdUI.Label();
            this.DelaunaySearchRadiusMultiplier = new AntdUI.InputNumber();
            this.uiSymbolLabelDelaunaySampleStep = new AntdUI.Label();
            this.DelaunaySampleStep = new AntdUI.InputNumber();
            this.uiSymbolLabelDelaunayMaxNeighbors = new AntdUI.Label();
            this.DelaunayMaxNeighbors = new AntdUI.InputNumber();
            this.uiSymbolLabelPoissonDepth = new AntdUI.Label();
            this.PoissonDepth = new AntdUI.InputNumber();
            this.uiSymbolLabelPoissonSamplesPerNode = new AntdUI.Label();
            this.PoissonSamplesPerNode = new AntdUI.InputNumber();
            this.uiSymbolLabelPoissonMaxCells = new AntdUI.Label();
            this.PoissonMaxCells = new AntdUI.InputNumber();
            this.uiSymbolLabelMlsRadius = new AntdUI.Label();
            this.MlsSearchRadius = new AntdUI.InputNumber();
            this.uiSymbolLabelMlsSampleStep = new AntdUI.Label();
            this.MlsSampleStep = new AntdUI.InputNumber();
            this.uiSymbolLabelMlsMaxNeighbors = new AntdUI.Label();
            this.MlsMaxNeighbors = new AntdUI.InputNumber();
            this.tableLayoutPanelMain.SuspendLayout();
            this.uiGroupBox1.SuspendLayout();
            this.uiGroupBox2.SuspendLayout();
            this.tableLayoutPanelButtons.SuspendLayout();
            this.uiGroupBox3.SuspendLayout();
            this.tableLayoutPanelCloud.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.uiGroupBox1, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.uiGroupBox2, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.uiGroupBox3, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelButtons, 0, 3);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayoutPanelMain.RowCount = 4;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(582, 524);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // uiGroupBox1
            // 
            this.uiGroupBox1.Controls.Add(this.uiComboBox1);
            this.uiGroupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiGroupBox1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiGroupBox1.Location = new System.Drawing.Point(10, 10);
            this.uiGroupBox1.Margin = new System.Windows.Forms.Padding(0, 0, 0, 5);
            this.uiGroupBox1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiGroupBox1.Name = "uiGroupBox1";
            this.uiGroupBox1.Padding = new System.Windows.Forms.Padding(10, 32, 10, 10);
            this.uiGroupBox1.Size = new System.Drawing.Size(562, 75);
            this.uiGroupBox1.TabIndex = 0;
            this.uiGroupBox1.Text = "算法选择";
            // 
            // uiComboBox1
            // 
            this.uiComboBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiComboBox1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiComboBox1.Location = new System.Drawing.Point(10, 32);
            this.uiComboBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiComboBox1.MinimumSize = new System.Drawing.Size(63, 0);
            this.uiComboBox1.Name = "uiComboBox1";
            this.uiComboBox1.Padding = new System.Windows.Forms.Padding(0, 0, 30, 2);
            this.uiComboBox1.Size = new System.Drawing.Size(542, 33);
            this.uiComboBox1.TabIndex = 0;
            this.uiComboBox1.SelectedIndexChanged += this.uiComboBox1_SelectedIndexChanged;
            // 
            // uiGroupBox2
            // 
            this.uiGroupBox2.Controls.Add(this.tableLayoutPanelParam);
            this.uiGroupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiGroupBox2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiGroupBox2.Location = new System.Drawing.Point(10, 95);
            this.uiGroupBox2.Margin = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.uiGroupBox2.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiGroupBox2.Name = "uiGroupBox2";
            this.uiGroupBox2.Padding = new System.Windows.Forms.Padding(10, 32, 10, 10);
            this.uiGroupBox2.Size = new System.Drawing.Size(562, 224);
            this.uiGroupBox2.TabIndex = 1;
            this.uiGroupBox2.Text = "算法参数";
            // 
            // tableLayoutPanelParam
            // 
            this.tableLayoutPanelParam.ColumnCount = 4;
            this.tableLayoutPanelParam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelParam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelParam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelParam.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelParam.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelParam.Location = new System.Drawing.Point(10, 32);
            this.tableLayoutPanelParam.Name = "tableLayoutPanelParam";
            this.tableLayoutPanelParam.RowCount = 4;
            this.tableLayoutPanelParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelParam.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelParam.Size = new System.Drawing.Size(542, 182);
            this.tableLayoutPanelParam.TabIndex = 0;
            // 
            // uiGroupBox3
            // 
            this.uiGroupBox3.Controls.Add(this.tableLayoutPanelCloud);
            this.uiGroupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiGroupBox3.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiGroupBox3.Location = new System.Drawing.Point(10, 329);
            this.uiGroupBox3.Margin = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.uiGroupBox3.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiGroupBox3.Name = "uiGroupBox3";
            this.uiGroupBox3.Padding = new System.Windows.Forms.Padding(10, 32, 10, 10);
            this.uiGroupBox3.Size = new System.Drawing.Size(562, 120);
            this.uiGroupBox3.TabIndex = 3;
            this.uiGroupBox3.Text = "点云显示参数";
            // 
            // tableLayoutPanelCloud
            // 
            this.tableLayoutPanelCloud.ColumnCount = 4;
            this.tableLayoutPanelCloud.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelCloud.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelCloud.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelCloud.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelCloud.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelCloud.Location = new System.Drawing.Point(10, 32);
            this.tableLayoutPanelCloud.Name = "tableLayoutPanelCloud";
            this.tableLayoutPanelCloud.RowCount = 2;
            this.tableLayoutPanelCloud.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelCloud.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelCloud.Size = new System.Drawing.Size(542, 78);
            this.tableLayoutPanelCloud.TabIndex = 0;
            this.tableLayoutPanelCloud.Controls.Add(this.uiSymbolLabelPointSize, 0, 0);
            this.tableLayoutPanelCloud.Controls.Add(this.PointSize, 1, 0);
            this.tableLayoutPanelCloud.Controls.Add(this.uiSymbolLabelDisplayRatio, 2, 0);
            this.tableLayoutPanelCloud.Controls.Add(this.DisplayRatio, 3, 0);
            this.tableLayoutPanelCloud.Controls.Add(this.ZColorEnabled, 0, 1);
            this.tableLayoutPanelCloud.SetColumnSpan(this.ZColorEnabled, 4);
            // 
            // uiSymbolLabelPointSize
            // 
            this.uiSymbolLabelPointSize.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelPointSize.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelPointSize.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelPointSize.Name = "uiSymbolLabelPointSize";
            this.uiSymbolLabelPointSize.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelPointSize.TabIndex = 0;
            this.uiSymbolLabelPointSize.Text = "点大小";
            this.uiSymbolLabelPointSize.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PointSize
            // 
            this.PointSize.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.PointSize.DecimalPlaces = 2;
            this.PointSize.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PointSize.Location = new System.Drawing.Point(0, 0);
            this.PointSize.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.PointSize.Maximum = 10M;
            this.PointSize.Minimum = 0.5M;
            this.PointSize.MinimumSize = new System.Drawing.Size(100, 0);
            this.PointSize.Name = "PointSize";
            this.PointSize.Padding = new System.Windows.Forms.Padding(5);
            this.PointSize.Size = new System.Drawing.Size(150, 29);
            this.PointSize.Increment = 0.5M;
            this.PointSize.TabIndex = 1;
            this.PointSize.Text = "2.00";
            this.PointSize.Value = 2M;
            // 
            // uiSymbolLabelDisplayRatio
            // 
            this.uiSymbolLabelDisplayRatio.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelDisplayRatio.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelDisplayRatio.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelDisplayRatio.Name = "uiSymbolLabelDisplayRatio";
            this.uiSymbolLabelDisplayRatio.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelDisplayRatio.TabIndex = 2;
            this.uiSymbolLabelDisplayRatio.Text = "显示比例";
            this.uiSymbolLabelDisplayRatio.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DisplayRatio
            // 
            this.DisplayRatio.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.DisplayRatio.DecimalPlaces = 2;
            this.DisplayRatio.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.DisplayRatio.Location = new System.Drawing.Point(0, 0);
            this.DisplayRatio.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.DisplayRatio.Maximum = 1M;
            this.DisplayRatio.Minimum = 0.01M;
            this.DisplayRatio.MinimumSize = new System.Drawing.Size(100, 0);
            this.DisplayRatio.Name = "DisplayRatio";
            this.DisplayRatio.Padding = new System.Windows.Forms.Padding(5);
            this.DisplayRatio.Size = new System.Drawing.Size(150, 29);
            this.DisplayRatio.Increment = 0.01M;
            this.DisplayRatio.TabIndex = 3;
            this.DisplayRatio.Text = "1.00";
            this.DisplayRatio.Value = 1M;
            // 
            // ZColorEnabled
            // 
            this.ZColorEnabled.Cursor = System.Windows.Forms.Cursors.Hand;
            this.ZColorEnabled.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ZColorEnabled.Location = new System.Drawing.Point(0, 0);
            this.ZColorEnabled.MinimumSize = new System.Drawing.Size(1, 1);
            this.ZColorEnabled.Name = "ZColorEnabled";
            this.ZColorEnabled.Padding = new System.Windows.Forms.Padding(22, 0, 0, 0);
            this.ZColorEnabled.Size = new System.Drawing.Size(200, 29);
            this.ZColorEnabled.TabIndex = 4;
            this.ZColorEnabled.Text = "按Z轴高度着色";
            // 
            // tableLayoutPanelButtons
            // 
            this.tableLayoutPanelButtons.ColumnCount = 3;
            this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tableLayoutPanelButtons.Controls.Add(this.btnSave, 1, 0);
            this.tableLayoutPanelButtons.Controls.Add(this.btnCancel, 2, 0);
            this.tableLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelButtons.Location = new System.Drawing.Point(10, 459);
            this.tableLayoutPanelButtons.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.tableLayoutPanelButtons.Name = "tableLayoutPanelButtons";
            this.tableLayoutPanelButtons.RowCount = 1;
            this.tableLayoutPanelButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelButtons.Size = new System.Drawing.Size(562, 55);
            this.tableLayoutPanelButtons.TabIndex = 2;
            // 
            // btnSave
            // 
            this.btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSave.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSave.Location = new System.Drawing.Point(190, 10);
            this.btnSave.Margin = new System.Windows.Forms.Padding(3, 10, 3, 10);
            this.btnSave.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(140, 35);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnCancel.Location = new System.Drawing.Point(377, 10);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 10, 3, 10);
            this.btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(140, 35);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // uiSymbolLabelNeighborRadius
            // 
            this.uiSymbolLabelNeighborRadius.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelNeighborRadius.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelNeighborRadius.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelNeighborRadius.Name = "uiSymbolLabelNeighborRadius";
            this.uiSymbolLabelNeighborRadius.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelNeighborRadius.TabIndex = 0;
            this.uiSymbolLabelNeighborRadius.Text = "搜索半径";
            this.uiSymbolLabelNeighborRadius.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // NeighborSearchRadius
            // 
            this.NeighborSearchRadius.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.NeighborSearchRadius.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.NeighborSearchRadius.Location = new System.Drawing.Point(0, 0);
            this.NeighborSearchRadius.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.NeighborSearchRadius.Maximum = 100M;
            this.NeighborSearchRadius.Minimum = 0.1M;
            this.NeighborSearchRadius.MinimumSize = new System.Drawing.Size(100, 0);
            this.NeighborSearchRadius.Name = "NeighborSearchRadius";
            this.NeighborSearchRadius.Padding = new System.Windows.Forms.Padding(5);
            this.NeighborSearchRadius.Size = new System.Drawing.Size(150, 29);
            this.NeighborSearchRadius.Increment = 1M;
            this.NeighborSearchRadius.TabIndex = 1;
            this.NeighborSearchRadius.Text = "2.00";
            this.NeighborSearchRadius.Value = 2M;
            // 
            // uiSymbolLabelNeighborMaxNeighbors
            // 
            this.uiSymbolLabelNeighborMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelNeighborMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelNeighborMaxNeighbors.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelNeighborMaxNeighbors.Name = "uiSymbolLabelNeighborMaxNeighbors";
            this.uiSymbolLabelNeighborMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelNeighborMaxNeighbors.TabIndex = 2;
            this.uiSymbolLabelNeighborMaxNeighbors.Text = "最大邻居数";
            this.uiSymbolLabelNeighborMaxNeighbors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // NeighborMaxNeighbors
            // 
            this.NeighborMaxNeighbors.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.NeighborMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.NeighborMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.NeighborMaxNeighbors.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.NeighborMaxNeighbors.Maximum = 1000M;
            this.NeighborMaxNeighbors.Minimum = 1M;
            this.NeighborMaxNeighbors.MinimumSize = new System.Drawing.Size(100, 0);
            this.NeighborMaxNeighbors.Name = "NeighborMaxNeighbors";
            this.NeighborMaxNeighbors.Padding = new System.Windows.Forms.Padding(5);
            this.NeighborMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.NeighborMaxNeighbors.TabIndex = 3;
            this.NeighborMaxNeighbors.Text = "30";
            this.NeighborMaxNeighbors.Value = 30;
            // 
            // uiSymbolLabelNeighborSampleStep
            // 
            this.uiSymbolLabelNeighborSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelNeighborSampleStep.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelNeighborSampleStep.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelNeighborSampleStep.Name = "uiSymbolLabelNeighborSampleStep";
            this.uiSymbolLabelNeighborSampleStep.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelNeighborSampleStep.TabIndex = 4;
            this.uiSymbolLabelNeighborSampleStep.Text = "采样步长";
            this.uiSymbolLabelNeighborSampleStep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // NeighborSampleStep
            // 
            this.NeighborSampleStep.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.NeighborSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.NeighborSampleStep.Location = new System.Drawing.Point(0, 0);
            this.NeighborSampleStep.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.NeighborSampleStep.Maximum = 100M;
            this.NeighborSampleStep.Minimum = 1M;
            this.NeighborSampleStep.MinimumSize = new System.Drawing.Size(100, 0);
            this.NeighborSampleStep.Name = "NeighborSampleStep";
            this.NeighborSampleStep.Padding = new System.Windows.Forms.Padding(5);
            this.NeighborSampleStep.Size = new System.Drawing.Size(150, 29);
            this.NeighborSampleStep.TabIndex = 5;
            this.NeighborSampleStep.Text = "1";
            this.NeighborSampleStep.Value = 1;
            // 
            // uiSymbolLabelBallRadius
            // 
            this.uiSymbolLabelBallRadius.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelBallRadius.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelBallRadius.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelBallRadius.Name = "uiSymbolLabelBallRadius";
            this.uiSymbolLabelBallRadius.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelBallRadius.TabIndex = 6;
            this.uiSymbolLabelBallRadius.Text = "球半径";
            this.uiSymbolLabelBallRadius.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // BallPivotingRadius
            // 
            this.BallPivotingRadius.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.BallPivotingRadius.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.BallPivotingRadius.Location = new System.Drawing.Point(0, 0);
            this.BallPivotingRadius.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.BallPivotingRadius.Maximum = 100M;
            this.BallPivotingRadius.Minimum = 0.1M;
            this.BallPivotingRadius.MinimumSize = new System.Drawing.Size(100, 0);
            this.BallPivotingRadius.Name = "BallPivotingRadius";
            this.BallPivotingRadius.Padding = new System.Windows.Forms.Padding(5);
            this.BallPivotingRadius.Size = new System.Drawing.Size(150, 29);
            this.BallPivotingRadius.Increment = 1M;
            this.BallPivotingRadius.TabIndex = 7;
            this.BallPivotingRadius.Text = "2.00";
            this.BallPivotingRadius.Value = 2M;
            // 
            // uiSymbolLabelBallSampleStep
            // 
            this.uiSymbolLabelBallSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelBallSampleStep.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelBallSampleStep.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelBallSampleStep.Name = "uiSymbolLabelBallSampleStep";
            this.uiSymbolLabelBallSampleStep.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelBallSampleStep.TabIndex = 8;
            this.uiSymbolLabelBallSampleStep.Text = "采样步长";
            this.uiSymbolLabelBallSampleStep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // BallPivotingSampleStep
            // 
            this.BallPivotingSampleStep.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.BallPivotingSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.BallPivotingSampleStep.Location = new System.Drawing.Point(0, 0);
            this.BallPivotingSampleStep.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.BallPivotingSampleStep.Maximum = 100M;
            this.BallPivotingSampleStep.Minimum = 1M;
            this.BallPivotingSampleStep.MinimumSize = new System.Drawing.Size(100, 0);
            this.BallPivotingSampleStep.Name = "BallPivotingSampleStep";
            this.BallPivotingSampleStep.Padding = new System.Windows.Forms.Padding(5);
            this.BallPivotingSampleStep.Size = new System.Drawing.Size(150, 29);
            this.BallPivotingSampleStep.TabIndex = 9;
            this.BallPivotingSampleStep.Text = "1";
            this.BallPivotingSampleStep.Value = 1;
            // 
            // uiSymbolLabelBallMaxNeighbors
            // 
            this.uiSymbolLabelBallMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelBallMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelBallMaxNeighbors.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelBallMaxNeighbors.Name = "uiSymbolLabelBallMaxNeighbors";
            this.uiSymbolLabelBallMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelBallMaxNeighbors.TabIndex = 10;
            this.uiSymbolLabelBallMaxNeighbors.Text = "最大邻居数";
            this.uiSymbolLabelBallMaxNeighbors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // BallPivotingMaxNeighbors
            // 
            this.BallPivotingMaxNeighbors.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.BallPivotingMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.BallPivotingMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.BallPivotingMaxNeighbors.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.BallPivotingMaxNeighbors.Maximum = 1000M;
            this.BallPivotingMaxNeighbors.Minimum = 1M;
            this.BallPivotingMaxNeighbors.MinimumSize = new System.Drawing.Size(100, 0);
            this.BallPivotingMaxNeighbors.Name = "BallPivotingMaxNeighbors";
            this.BallPivotingMaxNeighbors.Padding = new System.Windows.Forms.Padding(5);
            this.BallPivotingMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.BallPivotingMaxNeighbors.TabIndex = 11;
            this.BallPivotingMaxNeighbors.Text = "30";
            this.BallPivotingMaxNeighbors.Value = 30;
            // 
            // uiSymbolLabelAlphaValue
            // 
            this.uiSymbolLabelAlphaValue.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelAlphaValue.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelAlphaValue.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelAlphaValue.Name = "uiSymbolLabelAlphaValue";
            this.uiSymbolLabelAlphaValue.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelAlphaValue.TabIndex = 12;
            this.uiSymbolLabelAlphaValue.Text = "Alpha值";
            this.uiSymbolLabelAlphaValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // AlphaShapeAlpha
            // 
            this.AlphaShapeAlpha.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.AlphaShapeAlpha.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.AlphaShapeAlpha.Location = new System.Drawing.Point(0, 0);
            this.AlphaShapeAlpha.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.AlphaShapeAlpha.Maximum = 100M;
            this.AlphaShapeAlpha.Minimum = 0.1M;
            this.AlphaShapeAlpha.MinimumSize = new System.Drawing.Size(100, 0);
            this.AlphaShapeAlpha.Name = "AlphaShapeAlpha";
            this.AlphaShapeAlpha.Padding = new System.Windows.Forms.Padding(5);
            this.AlphaShapeAlpha.Size = new System.Drawing.Size(150, 29);
            this.AlphaShapeAlpha.Increment = 1M;
            this.AlphaShapeAlpha.TabIndex = 13;
            this.AlphaShapeAlpha.Text = "2.00";
            this.AlphaShapeAlpha.Value = 2M;
            // 
            // uiSymbolLabelAlphaSampleStep
            // 
            this.uiSymbolLabelAlphaSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelAlphaSampleStep.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelAlphaSampleStep.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelAlphaSampleStep.Name = "uiSymbolLabelAlphaSampleStep";
            this.uiSymbolLabelAlphaSampleStep.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelAlphaSampleStep.TabIndex = 14;
            this.uiSymbolLabelAlphaSampleStep.Text = "采样步长";
            this.uiSymbolLabelAlphaSampleStep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // AlphaShapeSampleStep
            // 
            this.AlphaShapeSampleStep.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.AlphaShapeSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.AlphaShapeSampleStep.Location = new System.Drawing.Point(0, 0);
            this.AlphaShapeSampleStep.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.AlphaShapeSampleStep.Maximum = 100M;
            this.AlphaShapeSampleStep.Minimum = 1M;
            this.AlphaShapeSampleStep.MinimumSize = new System.Drawing.Size(100, 0);
            this.AlphaShapeSampleStep.Name = "AlphaShapeSampleStep";
            this.AlphaShapeSampleStep.Padding = new System.Windows.Forms.Padding(5);
            this.AlphaShapeSampleStep.Size = new System.Drawing.Size(150, 29);
            this.AlphaShapeSampleStep.TabIndex = 15;
            this.AlphaShapeSampleStep.Text = "1";
            this.AlphaShapeSampleStep.Value = 1;
            // 
            // uiSymbolLabelAlphaMaxNeighbors
            // 
            this.uiSymbolLabelAlphaMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelAlphaMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelAlphaMaxNeighbors.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelAlphaMaxNeighbors.Name = "uiSymbolLabelAlphaMaxNeighbors";
            this.uiSymbolLabelAlphaMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelAlphaMaxNeighbors.TabIndex = 16;
            this.uiSymbolLabelAlphaMaxNeighbors.Text = "最大邻居数";
            this.uiSymbolLabelAlphaMaxNeighbors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // AlphaShapeMaxNeighbors
            // 
            this.AlphaShapeMaxNeighbors.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.AlphaShapeMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.AlphaShapeMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.AlphaShapeMaxNeighbors.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.AlphaShapeMaxNeighbors.Maximum = 1000M;
            this.AlphaShapeMaxNeighbors.Minimum = 1M;
            this.AlphaShapeMaxNeighbors.MinimumSize = new System.Drawing.Size(100, 0);
            this.AlphaShapeMaxNeighbors.Name = "AlphaShapeMaxNeighbors";
            this.AlphaShapeMaxNeighbors.Padding = new System.Windows.Forms.Padding(5);
            this.AlphaShapeMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.AlphaShapeMaxNeighbors.TabIndex = 17;
            this.AlphaShapeMaxNeighbors.Text = "30";
            this.AlphaShapeMaxNeighbors.Value = 30;
            // 
            // uiSymbolLabelDelaunayQuality
            // 
            this.uiSymbolLabelDelaunayQuality.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelDelaunayQuality.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelDelaunayQuality.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelDelaunayQuality.Name = "uiSymbolLabelDelaunayQuality";
            this.uiSymbolLabelDelaunayQuality.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelDelaunayQuality.TabIndex = 18;
            this.uiSymbolLabelDelaunayQuality.Text = "质量阈值";
            this.uiSymbolLabelDelaunayQuality.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DelaunayQualityThreshold
            // 
            this.DelaunayQualityThreshold.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.DelaunayQualityThreshold.DecimalPlaces = 3;
            this.DelaunayQualityThreshold.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.DelaunayQualityThreshold.Location = new System.Drawing.Point(0, 0);
            this.DelaunayQualityThreshold.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.DelaunayQualityThreshold.Maximum = 1M;
            this.DelaunayQualityThreshold.Minimum = 0.001M;
            this.DelaunayQualityThreshold.MinimumSize = new System.Drawing.Size(100, 0);
            this.DelaunayQualityThreshold.Name = "DelaunayQualityThreshold";
            this.DelaunayQualityThreshold.Padding = new System.Windows.Forms.Padding(5);
            this.DelaunayQualityThreshold.Size = new System.Drawing.Size(150, 29);
            this.DelaunayQualityThreshold.Increment = 1M;
            this.DelaunayQualityThreshold.TabIndex = 19;
            this.DelaunayQualityThreshold.Text = "0.100";
            this.DelaunayQualityThreshold.Value = 0.1M;
            // 
            // uiSymbolLabelDelaunayRadiusMultiplier
            // 
            this.uiSymbolLabelDelaunayRadiusMultiplier.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelDelaunayRadiusMultiplier.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelDelaunayRadiusMultiplier.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelDelaunayRadiusMultiplier.Name = "uiSymbolLabelDelaunayRadiusMultiplier";
            this.uiSymbolLabelDelaunayRadiusMultiplier.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelDelaunayRadiusMultiplier.TabIndex = 20;
            this.uiSymbolLabelDelaunayRadiusMultiplier.Text = "半径倍数";
            this.uiSymbolLabelDelaunayRadiusMultiplier.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DelaunaySearchRadiusMultiplier
            // 
            this.DelaunaySearchRadiusMultiplier.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.DelaunaySearchRadiusMultiplier.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.DelaunaySearchRadiusMultiplier.Location = new System.Drawing.Point(0, 0);
            this.DelaunaySearchRadiusMultiplier.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.DelaunaySearchRadiusMultiplier.Maximum = 10M;
            this.DelaunaySearchRadiusMultiplier.Minimum = 0.1M;
            this.DelaunaySearchRadiusMultiplier.MinimumSize = new System.Drawing.Size(100, 0);
            this.DelaunaySearchRadiusMultiplier.Name = "DelaunaySearchRadiusMultiplier";
            this.DelaunaySearchRadiusMultiplier.Padding = new System.Windows.Forms.Padding(5);
            this.DelaunaySearchRadiusMultiplier.Size = new System.Drawing.Size(150, 29);
            this.DelaunaySearchRadiusMultiplier.Increment = 1M;
            this.DelaunaySearchRadiusMultiplier.TabIndex = 21;
            this.DelaunaySearchRadiusMultiplier.Text = "1.50";
            this.DelaunaySearchRadiusMultiplier.Value = 1.5M;
            // 
            // uiSymbolLabelDelaunaySampleStep
            // 
            this.uiSymbolLabelDelaunaySampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelDelaunaySampleStep.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelDelaunaySampleStep.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelDelaunaySampleStep.Name = "uiSymbolLabelDelaunaySampleStep";
            this.uiSymbolLabelDelaunaySampleStep.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelDelaunaySampleStep.TabIndex = 22;
            this.uiSymbolLabelDelaunaySampleStep.Text = "采样步长";
            this.uiSymbolLabelDelaunaySampleStep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DelaunaySampleStep
            // 
            this.DelaunaySampleStep.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.DelaunaySampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.DelaunaySampleStep.Location = new System.Drawing.Point(0, 0);
            this.DelaunaySampleStep.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.DelaunaySampleStep.Maximum = 100M;
            this.DelaunaySampleStep.Minimum = 1M;
            this.DelaunaySampleStep.MinimumSize = new System.Drawing.Size(100, 0);
            this.DelaunaySampleStep.Name = "DelaunaySampleStep";
            this.DelaunaySampleStep.Padding = new System.Windows.Forms.Padding(5);
            this.DelaunaySampleStep.Size = new System.Drawing.Size(150, 29);
            this.DelaunaySampleStep.TabIndex = 23;
            this.DelaunaySampleStep.Text = "1";
            this.DelaunaySampleStep.Value = 1;
            // 
            // uiSymbolLabelDelaunayMaxNeighbors
            // 
            this.uiSymbolLabelDelaunayMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelDelaunayMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelDelaunayMaxNeighbors.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelDelaunayMaxNeighbors.Name = "uiSymbolLabelDelaunayMaxNeighbors";
            this.uiSymbolLabelDelaunayMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelDelaunayMaxNeighbors.TabIndex = 24;
            this.uiSymbolLabelDelaunayMaxNeighbors.Text = "最大邻居数";
            this.uiSymbolLabelDelaunayMaxNeighbors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DelaunayMaxNeighbors
            // 
            this.DelaunayMaxNeighbors.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.DelaunayMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.DelaunayMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.DelaunayMaxNeighbors.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.DelaunayMaxNeighbors.Maximum = 1000M;
            this.DelaunayMaxNeighbors.Minimum = 1M;
            this.DelaunayMaxNeighbors.MinimumSize = new System.Drawing.Size(100, 0);
            this.DelaunayMaxNeighbors.Name = "DelaunayMaxNeighbors";
            this.DelaunayMaxNeighbors.Padding = new System.Windows.Forms.Padding(5);
            this.DelaunayMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.DelaunayMaxNeighbors.TabIndex = 25;
            this.DelaunayMaxNeighbors.Text = "30";
            this.DelaunayMaxNeighbors.Value = 30;
            // 
            // uiSymbolLabelPoissonDepth
            // 
            this.uiSymbolLabelPoissonDepth.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelPoissonDepth.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelPoissonDepth.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelPoissonDepth.Name = "uiSymbolLabelPoissonDepth";
            this.uiSymbolLabelPoissonDepth.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelPoissonDepth.TabIndex = 26;
            this.uiSymbolLabelPoissonDepth.Text = "深度";
            this.uiSymbolLabelPoissonDepth.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PoissonDepth
            // 
            this.PoissonDepth.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.PoissonDepth.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PoissonDepth.Location = new System.Drawing.Point(0, 0);
            this.PoissonDepth.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.PoissonDepth.Maximum = 20M;
            this.PoissonDepth.Minimum = 1M;
            this.PoissonDepth.MinimumSize = new System.Drawing.Size(100, 0);
            this.PoissonDepth.Name = "PoissonDepth";
            this.PoissonDepth.Padding = new System.Windows.Forms.Padding(5);
            this.PoissonDepth.Size = new System.Drawing.Size(150, 29);
            this.PoissonDepth.TabIndex = 27;
            this.PoissonDepth.Text = "8";
            this.PoissonDepth.Value = 8;
            // 
            // uiSymbolLabelPoissonSamplesPerNode
            // 
            this.uiSymbolLabelPoissonSamplesPerNode.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelPoissonSamplesPerNode.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelPoissonSamplesPerNode.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelPoissonSamplesPerNode.Name = "uiSymbolLabelPoissonSamplesPerNode";
            this.uiSymbolLabelPoissonSamplesPerNode.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelPoissonSamplesPerNode.TabIndex = 28;
            this.uiSymbolLabelPoissonSamplesPerNode.Text = "每节点采样";
            this.uiSymbolLabelPoissonSamplesPerNode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PoissonSamplesPerNode
            // 
            this.PoissonSamplesPerNode.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.PoissonSamplesPerNode.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PoissonSamplesPerNode.Location = new System.Drawing.Point(0, 0);
            this.PoissonSamplesPerNode.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.PoissonSamplesPerNode.Maximum = 100M;
            this.PoissonSamplesPerNode.Minimum = 1M;
            this.PoissonSamplesPerNode.MinimumSize = new System.Drawing.Size(100, 0);
            this.PoissonSamplesPerNode.Name = "PoissonSamplesPerNode";
            this.PoissonSamplesPerNode.Padding = new System.Windows.Forms.Padding(5);
            this.PoissonSamplesPerNode.Size = new System.Drawing.Size(150, 29);
            this.PoissonSamplesPerNode.TabIndex = 29;
            this.PoissonSamplesPerNode.Text = "1";
            this.PoissonSamplesPerNode.Value = 1;
            // 
            // uiSymbolLabelPoissonMaxCells
            // 
            this.uiSymbolLabelPoissonMaxCells.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelPoissonMaxCells.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelPoissonMaxCells.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelPoissonMaxCells.Name = "uiSymbolLabelPoissonMaxCells";
            this.uiSymbolLabelPoissonMaxCells.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelPoissonMaxCells.TabIndex = 30;
            this.uiSymbolLabelPoissonMaxCells.Text = "最大单元格";
            this.uiSymbolLabelPoissonMaxCells.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PoissonMaxCells
            // 
            this.PoissonMaxCells.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.PoissonMaxCells.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PoissonMaxCells.Location = new System.Drawing.Point(0, 0);
            this.PoissonMaxCells.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.PoissonMaxCells.Maximum = 1000000M;
            this.PoissonMaxCells.Minimum = 1000M;
            this.PoissonMaxCells.MinimumSize = new System.Drawing.Size(100, 0);
            this.PoissonMaxCells.Name = "PoissonMaxCells";
            this.PoissonMaxCells.Padding = new System.Windows.Forms.Padding(5);
            this.PoissonMaxCells.Size = new System.Drawing.Size(150, 29);
            this.PoissonMaxCells.TabIndex = 31;
            this.PoissonMaxCells.Text = "100000";
            this.PoissonMaxCells.Value = 100000;
            // 
            // uiSymbolLabelMlsRadius
            // 
            this.uiSymbolLabelMlsRadius.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelMlsRadius.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelMlsRadius.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelMlsRadius.Name = "uiSymbolLabelMlsRadius";
            this.uiSymbolLabelMlsRadius.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelMlsRadius.TabIndex = 32;
            this.uiSymbolLabelMlsRadius.Text = "搜索半径";
            this.uiSymbolLabelMlsRadius.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // MlsSearchRadius
            // 
            this.MlsSearchRadius.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MlsSearchRadius.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.MlsSearchRadius.Location = new System.Drawing.Point(0, 0);
            this.MlsSearchRadius.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.MlsSearchRadius.Maximum = 100M;
            this.MlsSearchRadius.Minimum = 0.1M;
            this.MlsSearchRadius.MinimumSize = new System.Drawing.Size(100, 0);
            this.MlsSearchRadius.Name = "MlsSearchRadius";
            this.MlsSearchRadius.Padding = new System.Windows.Forms.Padding(5);
            this.MlsSearchRadius.Size = new System.Drawing.Size(150, 29);
            this.MlsSearchRadius.Increment = 1M;
            this.MlsSearchRadius.TabIndex = 33;
            this.MlsSearchRadius.Text = "2.00";
            this.MlsSearchRadius.Value = 2M;
            // 
            // uiSymbolLabelMlsSampleStep
            // 
            this.uiSymbolLabelMlsSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelMlsSampleStep.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelMlsSampleStep.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelMlsSampleStep.Name = "uiSymbolLabelMlsSampleStep";
            this.uiSymbolLabelMlsSampleStep.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelMlsSampleStep.TabIndex = 34;
            this.uiSymbolLabelMlsSampleStep.Text = "采样步长";
            this.uiSymbolLabelMlsSampleStep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // MlsSampleStep
            // 
            this.MlsSampleStep.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MlsSampleStep.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.MlsSampleStep.Location = new System.Drawing.Point(0, 0);
            this.MlsSampleStep.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.MlsSampleStep.Maximum = 100M;
            this.MlsSampleStep.Minimum = 1M;
            this.MlsSampleStep.MinimumSize = new System.Drawing.Size(100, 0);
            this.MlsSampleStep.Name = "MlsSampleStep";
            this.MlsSampleStep.Padding = new System.Windows.Forms.Padding(5);
            this.MlsSampleStep.Size = new System.Drawing.Size(150, 29);
            this.MlsSampleStep.TabIndex = 35;
            this.MlsSampleStep.Text = "1";
            this.MlsSampleStep.Value = 1;
            // 
            // uiSymbolLabelMlsMaxNeighbors
            // 
            this.uiSymbolLabelMlsMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabelMlsMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.uiSymbolLabelMlsMaxNeighbors.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabelMlsMaxNeighbors.Name = "uiSymbolLabelMlsMaxNeighbors";
            this.uiSymbolLabelMlsMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.uiSymbolLabelMlsMaxNeighbors.TabIndex = 36;
            this.uiSymbolLabelMlsMaxNeighbors.Text = "最大邻居数";
            this.uiSymbolLabelMlsMaxNeighbors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // MlsMaxNeighbors
            // 
            this.MlsMaxNeighbors.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MlsMaxNeighbors.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.MlsMaxNeighbors.Location = new System.Drawing.Point(0, 0);
            this.MlsMaxNeighbors.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.MlsMaxNeighbors.Maximum = 1000M;
            this.MlsMaxNeighbors.Minimum = 1M;
            this.MlsMaxNeighbors.MinimumSize = new System.Drawing.Size(100, 0);
            this.MlsMaxNeighbors.Name = "MlsMaxNeighbors";
            this.MlsMaxNeighbors.Padding = new System.Windows.Forms.Padding(5);
            this.MlsMaxNeighbors.Size = new System.Drawing.Size(150, 29);
            this.MlsMaxNeighbors.TabIndex = 37;
            this.MlsMaxNeighbors.Text = "30";
            this.MlsMaxNeighbors.Value = 30;
            // 
            // RenderParamForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(582, 559);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Name = "RenderParamForm";
            this.Text = "渲染参数配置";
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.uiGroupBox1.ResumeLayout(false);
            this.uiGroupBox2.ResumeLayout(false);
            this.uiGroupBox3.ResumeLayout(false);
            this.tableLayoutPanelCloud.ResumeLayout(false);
            this.tableLayoutPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        #region 控件字段声明

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.GroupBox uiGroupBox1;
        private AntdUI.Select uiComboBox1;
        private System.Windows.Forms.GroupBox uiGroupBox2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelParam;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelButtons;
        private AntdUI.Button btnSave;
        private AntdUI.Button btnCancel;
        // 邻域搜索参数控件
        private AntdUI.Label uiSymbolLabelNeighborRadius;
        private AntdUI.InputNumber NeighborSearchRadius;
        private AntdUI.Label uiSymbolLabelNeighborMaxNeighbors;
        private AntdUI.InputNumber NeighborMaxNeighbors;
        private AntdUI.Label uiSymbolLabelNeighborSampleStep;
        private AntdUI.InputNumber NeighborSampleStep;
        // Ball Pivoting参数控件
        private AntdUI.Label uiSymbolLabelBallRadius;
        private AntdUI.InputNumber BallPivotingRadius;
        private AntdUI.Label uiSymbolLabelBallSampleStep;
        private AntdUI.InputNumber BallPivotingSampleStep;
        private AntdUI.Label uiSymbolLabelBallMaxNeighbors;
        private AntdUI.InputNumber BallPivotingMaxNeighbors;
        // Alpha Shape参数控件
        private AntdUI.Label uiSymbolLabelAlphaValue;
        private AntdUI.InputNumber AlphaShapeAlpha;
        private AntdUI.Label uiSymbolLabelAlphaSampleStep;
        private AntdUI.InputNumber AlphaShapeSampleStep;
        private AntdUI.Label uiSymbolLabelAlphaMaxNeighbors;
        private AntdUI.InputNumber AlphaShapeMaxNeighbors;
        // Delaunay参数控件
        private AntdUI.Label uiSymbolLabelDelaunayQuality;
        private AntdUI.InputNumber DelaunayQualityThreshold;
        private AntdUI.Label uiSymbolLabelDelaunayRadiusMultiplier;
        private AntdUI.InputNumber DelaunaySearchRadiusMultiplier;
        private AntdUI.Label uiSymbolLabelDelaunaySampleStep;
        private AntdUI.InputNumber DelaunaySampleStep;
        private AntdUI.Label uiSymbolLabelDelaunayMaxNeighbors;
        private AntdUI.InputNumber DelaunayMaxNeighbors;
        // Poisson参数控件
        private AntdUI.Label uiSymbolLabelPoissonDepth;
        private AntdUI.InputNumber PoissonDepth;
        private AntdUI.Label uiSymbolLabelPoissonSamplesPerNode;
        private AntdUI.InputNumber PoissonSamplesPerNode;
        private AntdUI.Label uiSymbolLabelPoissonMaxCells;
        private AntdUI.InputNumber PoissonMaxCells;
        // MLS参数控件
        private AntdUI.Label uiSymbolLabelMlsRadius;
        private AntdUI.InputNumber MlsSearchRadius;
        private AntdUI.Label uiSymbolLabelMlsSampleStep;
        private AntdUI.InputNumber MlsSampleStep;
        private AntdUI.Label uiSymbolLabelMlsMaxNeighbors;
        private AntdUI.InputNumber MlsMaxNeighbors;
        // 点云显示参数控件
        private System.Windows.Forms.GroupBox uiGroupBox3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelCloud;
        private AntdUI.Label uiSymbolLabelPointSize;
        private AntdUI.InputNumber PointSize;
        private AntdUI.Label uiSymbolLabelDisplayRatio;
        private AntdUI.InputNumber DisplayRatio;
        private AntdUI.Checkbox ZColorEnabled;

        #endregion
    }
}
