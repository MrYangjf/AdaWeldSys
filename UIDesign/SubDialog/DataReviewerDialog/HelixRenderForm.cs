using AdaWeldSystem.ProductFileManager;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.Wpf.SharpDX;
using AdaWeldSystem.PCLOperate.DataIO;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.PCLOperate.Render;
using AntdUI;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace AdaWeldSystem.Sub3UI
{
    /// <summary>
    /// Helix 3D 渲染子视图（SubView 模式）
    /// 提供点云加载、表面渲染、Z向着色等 3D 可视化功能。
    /// </summary>
    public partial class HelixRenderForm : UserControl
    {
        /// <summary>
        /// 父窗口引用（用于 AntdUI Modal 定位）
        /// </summary>
        public Window Window { get; private set; }

        private PointCloudViewer _viewer;

        /// <summary>
        /// 创建 Helix 渲染子视图（设计时/无父窗口）
        /// </summary>
        public HelixRenderForm() : this(null) { }

        /// <summary>
        /// 创建 Helix 渲染子视图（SubView 固定模式：注入父 Window）
        /// </summary>
        public HelixRenderForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            CreatCamereDir();

            // 页面销毁时释放外部嵌套资源（ADR-001：UI 类用 DisposeComponents，不在 .cs 重写 Dispose(bool)）
            this.HandleDestroyed += HelixRenderForm_HandleDestroyed;
        }

        private void HelixRenderForm_HandleDestroyed(object sender, EventArgs e)
        {
            DisposeComponents();
        }

        /// <summary>
        /// 当前点云数据
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PointCloudData CurrentData => _viewer?.CurrentData;

        /// <summary>
        /// 点大小（从RenderConfig读取/写入）
        /// </summary>
        [Browsable(true)]
        [Category("PointCloud")]
        [Description("点的大小")]
        [DefaultValue(2f)]
        public float PointSize
        {
            get => RenderConfig.PointSize;
            set
            {
                RenderConfig.PointSize = value;
                _viewer?.SetPointSize(value);
            }
        }

        /// <summary>
        /// 是否按Z轴高度着色（蓝→红渐变）（从RenderConfig读取/写入）
        /// </summary>
        [Browsable(true)]
        [Category("PointCloud")]
        [Description("是否按Z轴高度着色（蓝→红渐变）")]
        [DefaultValue(false)]
        public bool ZColorEnabled
        {
            get => RenderConfig.ZColorEnabled;
            set
            {
                RenderConfig.ZColorEnabled = value;
                _viewer?.SetZColorEnabled(value, RenderConfig.PointSize);
            }
        }

        /// <summary>
        /// 当前显示比例（从RenderConfig读取/写入）
        /// </summary>
        [Browsable(true)]
        [Category("PointCloud")]
        [Description("点云显示比例（0.01-1.0）")]
        [DefaultValue(1.0f)]
        public float DisplayRatio
        {
            get => RenderConfig.DisplayRatio;
            set
            {
                RenderConfig.DisplayRatio = Math.Max(0.01f, Math.Min(value, 1.0f));
            }
        }

        /// <summary>
        /// 当前是否显示表面模型
        /// </summary>
        [Browsable(false)]
        public bool IsSurfaceVisible => _viewer?.IsSurfaceVisible ?? false;

        /// <summary>
        /// 当前是否显示点云
        /// </summary>
        [Browsable(false)]
        public bool IsPointCloudVisible => _viewer?.IsPointCloudVisible ?? false;

        void CreatCamereDir()
        {
            _viewer = new PointCloudViewer();
            elementHost1.Child = _viewer.Viewport;
        }

        /// <summary>
        /// 释放外部嵌套资源（ADR-001：UI 设计器类使用 DisposeComponents，不在 .cs 重写 Dispose(bool)）。
        /// 设计器生成的 Dispose(bool) 仅释放 components 容器内控件；
        /// _viewer（PointCloudViewer 内部含 WPF Viewport3DX）为代码创建的独立资源，须在此显式释放。
        /// 由父页面关闭时调用。
        /// </summary>
        public void DisposeComponents()
        {
            if (_viewer != null)
            {
                _viewer.Dispose();
                _viewer = null;
            }
        }

        /// <summary>
        /// 1. 从文件加载点云（显示原始全部点）
        /// 导入文件后调用，清除之前所有内容，显示原始点云
        /// </summary>
        public void LoadPointCloud(string filePath, float pointSize = 2f)
        {
            if (_viewer == null) return;
            var data = PointCloudReader.LoadFromFile(filePath);
            LoadPointCloud(data, pointSize);
        }

        /// <summary>
        /// 1. 加载点云数据（显示原始全部点）
        /// </summary>
        public void LoadPointCloud(PointCloudData data, float pointSize = 2f)
        {
            if (_viewer == null) return;
            RenderConfig.PointSize = pointSize;
            RenderConfig.ZColorEnabled = false;
            RenderConfig.DisplayRatio = 1.0f;
            _viewer.LoadPointCloud(data, pointSize);
        }

        /// <summary>
        /// 2. 减点显示：清除界面上原始点的显示后显示减点后的点云
        /// </summary>
        /// <param name="ratio">显示比例（0.01-1.0），如0.5表示显示一半点数</param>
        public void ReducePointCloud(float ratio)
        {
            if (_viewer == null) return;
            RenderConfig.DisplayRatio = Math.Max(0.01f, Math.Min(1.0f, ratio));
            _viewer.ReducePointCloud(RenderConfig.DisplayRatio, RenderConfig.PointSize);
        }

        /// <summary>
        /// 3. 表面渲染：按照最新的点云生成表面渲染对象，窗口清除点云，显示表面渲染
        /// </summary>
        /// <param name="searchRadius">搜索半径</param>
        /// <param name="maxNeighbors">最大邻居数</param>
        /// <param name="sampleStep">采样步长（1=全部点）</param>
        /// <summary>
        /// 4. 清除显示：所有点云和模型数据都清理掉
        /// </summary>
        public void ClearAll()
        {
            if (_viewer != null)
            {
                _viewer.ClearAll();
                RenderConfig.ZColorEnabled = false;
                RenderConfig.DisplayRatio = 1.0f;
            }
        }

        /// <summary>
        /// 设置点大小
        /// </summary>
        public void SetPointSize(float size)
        {
            RenderConfig.PointSize = size;
            _viewer?.SetPointSize(size);
        }

        /// <summary>
        /// 设置点颜色
        /// </summary>
        public void SetPointColor(DrawingColor color)
        {
            var mediaColor = MediaColor.FromArgb(color.A, color.R, color.G, color.B);
            _viewer?.SetPointColor(mediaColor);
        }

        /// <summary>
        /// 设置表面颜色
        /// </summary>
        public void SetSurfaceColor(DrawingColor color)
        {
            var mediaColor = MediaColor.FromArgb(color.A, color.R, color.G, color.B);
            _viewer?.SetSurfaceColor(mediaColor);
        }

        /// <summary>
        /// 保存当前点云到文件
        /// </summary>
        public void SavePointCloud(string filePath)
        {
            if (_viewer?.CurrentData == null) return;
            PointCloudWriter.SaveToFile(_viewer.CurrentData, filePath);
        }

        private void btnTraceFile_Click(object sender, EventArgs e)
        {
            using (var traceForm = new FileTraceForm(this.FindForm() as AntdUI.Window))
            {
                if (AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "文件追溯", traceForm, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                }) == DialogResult.OK)
                {
                    string selectedFile = traceForm.SelectedFilePath;
                    if (string.IsNullOrEmpty(selectedFile) || !File.Exists(selectedFile))
                    {
                        MessageBox.Show("选择的文件不存在！", "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    try
                    {
                        Application.DoEvents();

                        string extension = Path.GetExtension(selectedFile).ToLowerInvariant();
                        PointCloudData data = null;

                        if (extension == ".tmp")
                        {
                            // 临时文件：先解析再加载
                            if (RunRecordManager.Instance.ParseFromTempFile(selectedFile))
                            {
                                data = RunRecordManager.Instance.GetFramePointCloud(0);
                                if (data == null)
                                {
                                    var frames = RunRecordManager.Instance.GetAllFrames();
                                    if (frames.Count > 0)
                                    {
                                        data = frames[0].PointCloud;
                                    }
                                }
                            }

                            if (data == null)
                            {
                                MessageBox.Show("临时文件解析失败或不含有效点云数据！", "错误",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                        else
                        {
                            // 正常点云文件直接加载
                            data = PointCloudReader.LoadFromFile(selectedFile);
                        }

                        LoadPointCloud(data);
                        ZColorEnabled = false;
                        DisplayRatio = 1.0f;

                        if (CurrentData != null)
                        {
                            ZColorEnabled = true;
                            btnZColor.Text = "Z向着色(开)";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载文件失败:\n{ex.Message}", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnZColor_Click(object sender, EventArgs e)
        {
            if (CurrentData == null)
            {
                MessageBox.Show("请先加载点云文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ZColorEnabled = !ZColorEnabled;
            btnZColor.Text = ZColorEnabled ? "Z向着色(开)" : "Z向着色(关)";

        }

        private void btnSurface_Click(object sender, EventArgs e)
        {
            if (CurrentData == null)
            {
                MessageBox.Show("请先加载点云文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                ExecuteSurfaceRender();
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成表面失败:\n" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 根据RenderConfig中选定的算法和参数执行表面渲染
        /// </summary>
        private void ExecuteSurfaceRender()
        {
            switch (RenderConfig.SelectedAlgorithm)
            {
                case SurfaceRenderAlgorithm.NeighborSearch:
                    _viewer?.GenerateSurface(
                        RenderConfig.NeighborSearchRadius,
                        RenderConfig.NeighborMaxNeighbors,
                        RenderConfig.NeighborSampleStep);
                    break;

                case SurfaceRenderAlgorithm.BallPivoting:
                    _viewer?.GenerateSurfaceBallPivoting(
                        RenderConfig.BallPivotingRadius,
                        RenderConfig.BallPivotingSampleStep,
                        RenderConfig.BallPivotingMaxNeighbors);
                    break;

                case SurfaceRenderAlgorithm.AlphaShape:
                    _viewer?.GenerateAlphaShape(
                        RenderConfig.AlphaShapeAlpha,
                        RenderConfig.AlphaShapeSampleStep,
                        RenderConfig.AlphaShapeMaxNeighbors);
                    break;

                case SurfaceRenderAlgorithm.Delaunay3D:
                    _viewer?.GenerateDelaunay(
                        RenderConfig.DelaunayQualityThreshold,
                        RenderConfig.DelaunaySearchRadiusMultiplier,
                        RenderConfig.DelaunaySampleStep,
                        RenderConfig.DelaunayMaxNeighbors);
                    break;

                case SurfaceRenderAlgorithm.Poisson:
                    _viewer?.GeneratePoisson(
                        RenderConfig.PoissonDepth,
                        RenderConfig.PoissonSamplesPerNode,
                        RenderConfig.PoissonMaxCells);
                    break;

                case SurfaceRenderAlgorithm.MLS:
                    _viewer?.GenerateMLS(
                        RenderConfig.MlsSearchRadius,
                        RenderConfig.MlsSampleStep,
                        RenderConfig.MlsMaxNeighbors);
                    break;

                default:
                    _viewer?.GenerateSurface(
                        RenderConfig.NeighborSearchRadius,
                        RenderConfig.NeighborMaxNeighbors,
                        RenderConfig.NeighborSampleStep);
                    break;
            }
        }

        private void btnClearData_Click(object sender, EventArgs e)
        {
            ClearAll();
        }

        private void btnRotate_Click(object sender, EventArgs e)
        {
            if (_viewer == null) return;
            _viewer.ResetCameraToCenter();
            _viewer.SetCameraMode(PointCloudViewer.CameraControlMode.Rotate);
        }

        private void btnMove_Click(object sender, EventArgs e)
        {
            if (_viewer == null) return;
            _viewer.ResetCameraToCenter();
            _viewer.SetCameraMode(PointCloudViewer.CameraControlMode.Move);
            
        }

        private void btnRound_Click(object sender, EventArgs e)
        {
            if (_viewer == null) return;
            _viewer.ResetCameraToCenter();
            _viewer.SetCameraMode(PointCloudViewer.CameraControlMode.Round);
        }

        private void btnAssimpImport_Click(object sender, EventArgs e)
        {
            // 配置文件对话框，支持 Assimp 可导入的格式
            openFileDialog1.Filter = "3D模型文件|*.ply;*.obj;*.stl;*.fbx;*.3ds;*.dae;*.gltf|PLY文件|*.ply|OBJ文件|*.obj|STL文件|*.stl|所有文件|*.*";
            openFileDialog1.Title = "选择3D模型文件（Assimp导入）";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string selectedFile = openFileDialog1.FileName;
                try
                {
                    Application.DoEvents();

                    // 使用 Assimp 直接加载为 Scene 并显示到 Viewport3DX
                    HelixToolkitScene scene = PointCloudReader.LoadSceneFromFile(selectedFile);
                    _viewer.LoadScene(scene);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Assimp 导入失败:\n" + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // 恢复默认文件对话框 Filter
            openFileDialog1.Filter = "";
            openFileDialog1.Title = "";
        }

        private void btnRenderParam_Click(object sender, EventArgs e)
        {
            using (var paramForm = new RenderParamForm(this.FindForm() as AntdUI.Window))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "渲染参数配置", paramForm, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }

    }
}
