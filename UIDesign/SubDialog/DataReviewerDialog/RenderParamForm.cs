using AdaWeldSystem.PCLOperate.Render;
using AntdUI;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    /// <summary>
    /// 渲染参数配置对话框
    /// 用于配置表面重建算法的参数
    /// </summary>
    public partial class RenderParamForm : UserControl
    {
        #region 构造函数

        // 复刻 AntdUI demo SystemSet 模式：注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        /// <summary>
        /// 创建渲染参数配置对话框（设计时/无父窗口）
        /// </summary>
        public RenderParamForm() : this(null) { }

        /// <summary>
        /// 创建渲染参数配置对话框（复刻 AntdUI demo SystemSet 模式：注入父 Window）
        /// </summary>
        public RenderParamForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            InitializeComboBoxItems();
            LoadConfigValues();
        }

        #endregion

        #region 控件初始化

        /// <summary>
        /// 初始化下拉框选项
        /// 添加所有表面重建算法名称
        /// </summary>
        private void InitializeComboBoxItems()
        {
            uiComboBox1.Items.Clear();
            uiComboBox1.Items.Add("邻域搜索");
            uiComboBox1.Items.Add("Ball Pivoting");
            uiComboBox1.Items.Add("Alpha Shape");
            uiComboBox1.Items.Add("Delaunay 3D");
            uiComboBox1.Items.Add("泊松重建");
            uiComboBox1.Items.Add("移动最小二乘");
        }

        #endregion

        #region 配置加载与保存

        /// <summary>
        /// 从RenderConfig加载当前配置值
        /// </summary>
        private void LoadConfigValues()
        {
            uiComboBox1.SelectedIndex = (int)RenderConfig.SelectedAlgorithm;

            // 加载邻域搜索参数
            NeighborSearchRadius.Value = (decimal)RenderConfig.NeighborSearchRadius;
            NeighborMaxNeighbors.Value = RenderConfig.NeighborMaxNeighbors;
            NeighborSampleStep.Value = RenderConfig.NeighborSampleStep;

            // 加载Ball Pivoting参数
            BallPivotingRadius.Value = (decimal)RenderConfig.BallPivotingRadius;
            BallPivotingSampleStep.Value = RenderConfig.BallPivotingSampleStep;
            BallPivotingMaxNeighbors.Value = RenderConfig.BallPivotingMaxNeighbors;

            // 加载Alpha Shape参数
            AlphaShapeAlpha.Value = (decimal)RenderConfig.AlphaShapeAlpha;
            AlphaShapeSampleStep.Value = RenderConfig.AlphaShapeSampleStep;
            AlphaShapeMaxNeighbors.Value = RenderConfig.AlphaShapeMaxNeighbors;

            // 加载Delaunay参数
            DelaunayQualityThreshold.Value = (decimal)RenderConfig.DelaunayQualityThreshold;
            DelaunaySearchRadiusMultiplier.Value = (decimal)RenderConfig.DelaunaySearchRadiusMultiplier;
            DelaunaySampleStep.Value = RenderConfig.DelaunaySampleStep;
            DelaunayMaxNeighbors.Value = RenderConfig.DelaunayMaxNeighbors;

            // 加载Poisson参数
            PoissonDepth.Value = RenderConfig.PoissonDepth;
            PoissonSamplesPerNode.Value = RenderConfig.PoissonSamplesPerNode;
            PoissonMaxCells.Value = RenderConfig.PoissonMaxCells;

            // 加载MLS参数
            MlsSearchRadius.Value = (decimal)RenderConfig.MlsSearchRadius;
            MlsSampleStep.Value = RenderConfig.MlsSampleStep;
            MlsMaxNeighbors.Value = RenderConfig.MlsMaxNeighbors;

            // 加载点云显示参数
            PointSize.Value = (decimal)RenderConfig.PointSize;
            ZColorEnabled.Checked = RenderConfig.ZColorEnabled;
            DisplayRatio.Value = (decimal)RenderConfig.DisplayRatio;
        }

        /// <summary>
        /// 保存配置到RenderConfig
        /// </summary>
        private void SaveConfigValues()
        {
            RenderConfig.SelectedAlgorithm = (SurfaceRenderAlgorithm)uiComboBox1.SelectedIndex;

            // 保存邻域搜索参数
            RenderConfig.NeighborSearchRadius = (float)NeighborSearchRadius.Value;
            RenderConfig.NeighborMaxNeighbors = (int)NeighborMaxNeighbors.Value;
            RenderConfig.NeighborSampleStep = (int)NeighborSampleStep.Value;

            // 保存Ball Pivoting参数
            RenderConfig.BallPivotingRadius = (float)BallPivotingRadius.Value;
            RenderConfig.BallPivotingSampleStep = (int)BallPivotingSampleStep.Value;
            RenderConfig.BallPivotingMaxNeighbors = (int)BallPivotingMaxNeighbors.Value;

            // 保存Alpha Shape参数
            RenderConfig.AlphaShapeAlpha = (float)AlphaShapeAlpha.Value;
            RenderConfig.AlphaShapeSampleStep = (int)AlphaShapeSampleStep.Value;
            RenderConfig.AlphaShapeMaxNeighbors = (int)AlphaShapeMaxNeighbors.Value;

            // 保存Delaunay参数
            RenderConfig.DelaunayQualityThreshold = (float)DelaunayQualityThreshold.Value;
            RenderConfig.DelaunaySearchRadiusMultiplier = (float)DelaunaySearchRadiusMultiplier.Value;
            RenderConfig.DelaunaySampleStep = (int)DelaunaySampleStep.Value;
            RenderConfig.DelaunayMaxNeighbors = (int)DelaunayMaxNeighbors.Value;

            // 保存Poisson参数
            RenderConfig.PoissonDepth = (int)PoissonDepth.Value;
            RenderConfig.PoissonSamplesPerNode = (int)PoissonSamplesPerNode.Value;
            RenderConfig.PoissonMaxCells = (int)PoissonMaxCells.Value;

            // 保存MLS参数
            RenderConfig.MlsSearchRadius = (float)MlsSearchRadius.Value;
            RenderConfig.MlsSampleStep = (int)MlsSampleStep.Value;
            RenderConfig.MlsMaxNeighbors = (int)MlsMaxNeighbors.Value;

            // 保存点云显示参数
            RenderConfig.PointSize = (float)PointSize.Value;
            RenderConfig.ZColorEnabled = ZColorEnabled.Checked;
            RenderConfig.DisplayRatio = (float)DisplayRatio.Value;

            RenderConfig.SaveToIni();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 算法选择变更事件
        /// </summary>
        private void uiComboBox1_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            UpdateParamVisibility();
        }

        /// <summary>
        /// 更新参数控件可见性
        /// </summary>
        private void UpdateParamVisibility()
        {
            tableLayoutPanelParam.Controls.Clear();

            SurfaceRenderAlgorithm algorithm = (SurfaceRenderAlgorithm)uiComboBox1.SelectedIndex;

            switch (algorithm)
            {
                case SurfaceRenderAlgorithm.NeighborSearch:
                    AddParamRow(0, uiSymbolLabelNeighborRadius, NeighborSearchRadius, uiSymbolLabelNeighborMaxNeighbors, NeighborMaxNeighbors);
                    AddParamRow(1, uiSymbolLabelNeighborSampleStep, NeighborSampleStep, null, null);
                    break;

                case SurfaceRenderAlgorithm.BallPivoting:
                    AddParamRow(0, uiSymbolLabelBallRadius, BallPivotingRadius, uiSymbolLabelBallSampleStep, BallPivotingSampleStep);
                    AddParamRow(1, uiSymbolLabelBallMaxNeighbors, BallPivotingMaxNeighbors, null, null);
                    break;

                case SurfaceRenderAlgorithm.AlphaShape:
                    AddParamRow(0, uiSymbolLabelAlphaValue, AlphaShapeAlpha, uiSymbolLabelAlphaSampleStep, AlphaShapeSampleStep);
                    AddParamRow(1, uiSymbolLabelAlphaMaxNeighbors, AlphaShapeMaxNeighbors, null, null);
                    break;

                case SurfaceRenderAlgorithm.Delaunay3D:
                    AddParamRow(0, uiSymbolLabelDelaunayQuality, DelaunayQualityThreshold, uiSymbolLabelDelaunayRadiusMultiplier, DelaunaySearchRadiusMultiplier);
                    AddParamRow(1, uiSymbolLabelDelaunaySampleStep, DelaunaySampleStep, uiSymbolLabelDelaunayMaxNeighbors, DelaunayMaxNeighbors);
                    break;

                case SurfaceRenderAlgorithm.Poisson:
                    AddParamRow(0, uiSymbolLabelPoissonDepth, PoissonDepth, uiSymbolLabelPoissonSamplesPerNode, PoissonSamplesPerNode);
                    AddParamRow(1, uiSymbolLabelPoissonMaxCells, PoissonMaxCells, null, null);
                    break;

                case SurfaceRenderAlgorithm.MLS:
                    AddParamRow(0, uiSymbolLabelMlsRadius, MlsSearchRadius, uiSymbolLabelMlsSampleStep, MlsSampleStep);
                    AddParamRow(1, uiSymbolLabelMlsMaxNeighbors, MlsMaxNeighbors, null, null);
                    break;
            }
        }

        /// <summary>
        /// 添加参数行到面板
        /// </summary>
        private void AddParamRow(int row, Control label1, Control control1, Control label2, Control control2)
        {
            if (label1 != null && control1 != null)
            {
                tableLayoutPanelParam.Controls.Add(label1, 0, row);
                tableLayoutPanelParam.Controls.Add(control1, 1, row);
            }
            if (label2 != null && control2 != null)
            {
                tableLayoutPanelParam.Controls.Add(label2, 2, row);
                tableLayoutPanelParam.Controls.Add(control2, 3, row);
            }
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveConfigValues();
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        #endregion
    }
}
