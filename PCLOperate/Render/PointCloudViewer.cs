using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.Wpf.SharpDX;
using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Windows.Input;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;

namespace AdaWeldSystem.PCLOperate.Render
{
    /// <summary>
    /// 3D点云查看器 - 基于HelixToolkit.Wpf.SharpDX
    /// 显示逻辑：
    /// 1. 导入文件后加载原始点云显示
    /// 2. 减点显示：清除原有点云，显示减点后的点云
    /// 3. 表面渲染：按最新点云生成表面，清除点云，显示表面
    /// 4. 清除显示：清理所有点云和模型
    /// </summary>
    public class PointCloudViewer : IDisposable
    {
        TransformManipulator3D _manipulator;

        private readonly Viewport3DX _viewport;
        private PointCloudData _currentData;

        // 3D模型
        private PointGeometryModel3D _pointModel;
        private PointGeometry3D _currentGeometry;
        private MeshGeometryModel3D _surfaceMesh;

        // Assimp Scene 直接显示包装器
        private SceneNodeGroupModel3D _sceneModel;

        // 显示控制
        private float _displayRatio = 1.0f;  // 1.0=显示全部, 0.5=显示一半, 0.1=显示10%
        private bool _showByZColor = false; // 是否按Z轴着色
        private float _currentPointSize = 2f; // 保存当前点大小用于恢复

        /// <summary>
        /// 相机控制模式
        /// </summary>
        public enum CameraControlMode
        {
            Rotate,  // 围绕模型中心旋转
            Move,    // 平移/移动相机位置
            Round    // 围绕相机位置旋转（轨道）
        }

        // 相机控制
        private CameraControlMode _cameraMode = CameraControlMode.Rotate;
        private System.Windows.Media.Media3D.Point3D _modelCenter = new System.Windows.Media.Media3D.Point3D(0, 0, 0);

        /// <summary>
        /// 无参构造函数 - 内部自动创建并初始化 Viewport3DX
        /// </summary>
        public PointCloudViewer()
        {
            _viewport = new Viewport3DX();
            InitializeViewport();
            InitializeCamera();
        }

        /// <summary>
        /// 外部传入 Viewport3DX 的构造函数（向后兼容）
        /// </summary>
        public PointCloudViewer(Viewport3DX viewport)
        {
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            InitializeViewport();
        }

        /// <summary>
        /// 内部 Viewport3DX 实例
        /// </summary>
        public Viewport3DX Viewport => _viewport;

        ~PointCloudViewer()
        {
            ClearAll();
        }

        /// <summary>
        /// 当前点云数据
        /// </summary>
        public PointCloudData CurrentData => _currentData;

        /// <summary>
        /// 当前是否显示表面模型
        /// </summary>
        public bool IsSurfaceVisible => _surfaceMesh != null;

        /// <summary>
        /// 当前是否显示点云
        /// </summary>
        public bool IsPointCloudVisible => _pointModel != null;

        private void InitializeViewport()
        {
            // 添加灯光
            var ambientLight = new AmbientLight3D { Color = MediaColor.FromRgb(100, 100, 100) };
            _viewport.Items.Add(ambientLight);

            var directionalLight = new DirectionalLight3D
            {
                Color = Colors.White,
                Direction = new System.Windows.Media.Media3D.Vector3D(-1, -1, -1)
            };
            _viewport.Items.Add(directionalLight);

            var directionalLight2 = new DirectionalLight3D
            {
                Color = MediaColor.FromRgb(180, 180, 180),
                Direction = new System.Windows.Media.Media3D.Vector3D(1, 1, 1)
            };
            _viewport.Items.Add(directionalLight2);

            _manipulator = new TransformManipulator3D();
        }

        /// <summary>
        /// 1. 加载点云数据（显示原始全部点）
        /// 导入文件后调用，清除之前所有内容，显示原始点云，并自适应调整视角
        /// </summary>
        public void LoadPointCloud(PointCloudData data, float pointSize = 2f)
        {
            // 先清除所有内容
            ClearAll();

            _currentData = data ?? throw new ArgumentNullException(nameof(data));
            _displayRatio = 1.0f;
            _showByZColor = false;
            _currentPointSize = pointSize;

            // 构建并显示原始点云
            BuildAndShowPointCloud(pointSize);

            // 自适应调整视角，完整显示整个点云
            FitViewToPointCloud();

            // 重置相机到模型中心
            ResetCameraToCenter();
        }

        /// <summary>
        /// 自适应调整视角，使点云完整显示在视口中
        /// </summary>
        private void FitViewToPointCloud()
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 计算点云的边界框
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            if (_currentData.HasColor)
            {
                foreach (var p in _currentData.ColoredPoints)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.Z > maxZ) maxZ = p.Z;
                }
            }
            else
            {
                foreach (var p in _currentData.Points)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.Z > maxZ) maxZ = p.Z;
                }
            }

            // 计算中心点和半径
            var center = new System.Numerics.Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            _modelCenter = new System.Windows.Media.Media3D.Point3D(center.X, center.Y, center.Z);
            float radius = (float)Math.Sqrt(
                (maxX - minX) * (maxX - minX) +
                (maxY - minY) * (maxY - minY) +
                (maxZ - minZ) * (maxZ - minZ)) / 2;

            // 设置相机位置，使点云完整显示
            if (_viewport.Camera is HelixToolkit.Wpf.SharpDX.PerspectiveCamera perspectiveCamera)
            {
                // 计算合适的相机距离
                float fov = (float)(perspectiveCamera.FieldOfView * Math.PI / 180.0);
                float distance = radius / (float)Math.Tan(fov / 2) * 1.5f; // 1.5倍余量确保完整显示

                // 设置相机位置（从斜上方观察）
                var cameraPosition = new System.Windows.Media.Media3D.Point3D(
                    center.X + distance * 0.7,
                    center.Y - distance * 0.7,
                    center.Z + distance * 0.5);

                perspectiveCamera.Position = cameraPosition;
                perspectiveCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(
                    center.X - cameraPosition.X,
                    center.Y - cameraPosition.Y,
                    center.Z - cameraPosition.Z);
                perspectiveCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
            }
            else if (_viewport.Camera is HelixToolkit.Wpf.SharpDX.OrthographicCamera orthoCamera)
            {
                // 正交相机设置
                orthoCamera.Position = new System.Windows.Media.Media3D.Point3D(
                    center.X + radius * 2,
                    center.Y - radius * 2,
                    center.Z + radius);
                orthoCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(
                    -radius * 2, radius * 2, -radius);
                orthoCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                orthoCamera.Width = radius * 3;
            }
        }

        /// <summary>
        /// 2. 减点显示：清除界面上原始点的显示后显示减点后的点云
        /// </summary>
        /// <param name="ratio">显示比例（0.01-1.0），如0.5表示显示一半点数</param>
        public void ReducePointCloud(float ratio, float pointSize = 2f)
        {
            if (_currentData == null) return;

            // 清除表面模型（如果存在）
            ClearSurface();

            // 更新显示比例
            _displayRatio = Math.Max(0.01f, Math.Min(1.0f, ratio));
            _currentPointSize = pointSize;

            // 重新构建并显示减点后的点云
            BuildAndShowPointCloud(pointSize);
        }

        /// <summary>
        /// 3. 表面渲染：按照最新的点云生成表面渲染对象，窗口清除点云，显示表面渲染
        /// </summary>
        /// <param name="searchRadius">搜索半径</param>
        /// <param name="maxNeighbors">最大邻居数量</param>
        /// <param name="sampleStep">采样步长（1=全部点）</param>
        public void GenerateSurface(float searchRadius = 0.5f, int maxNeighbors = 30, int sampleStep = 1)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除点云显示
            ClearPointCloud();

            // 生成表面
            var mesh = SurfaceTriangulator.GenerateSurface(_currentData, searchRadius, maxNeighbors, sampleStep);
            if (mesh != null)
            {
                ShowSurfaceMesh(mesh, MediaColor.FromRgb(180, 200, 255));
            }
        }

        /// <summary>
        /// 3. 使用Ball Pivoting算法生成表面
        /// </summary>
        /// <param name="ballRadius">球半径</param>
        /// <param name="sampleStep">采样步长（1=全部点）</param>
        /// <param name="maxNeighbors">最大邻居数量</param>
        public void GenerateSurfaceBallPivoting(float ballRadius = 0.5f, int sampleStep = 1, int maxNeighbors = 20)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除点云显示
            ClearPointCloud();

            // 生成表面
            var mesh = SurfaceTriangulator.GenerateSurfaceBallPivoting(_currentData, ballRadius, sampleStep, maxNeighbors);
            if (mesh != null)
            {
                ShowSurfaceMesh(mesh, MediaColor.FromRgb(180, 200, 255));
            }
        }

        /// <summary>
        /// 3. 使用Alpha Shape算法生成表面
        /// 适合需要保留空洞的场景，如建筑模型、医学图像
        /// </summary>
        /// <param name="alpha">Alpha值</param>
        /// <param name="sampleStep">采样步长（1=全部点）</param>
        /// <param name="maxNeighbors">最大邻居数量</param>
        public void GenerateAlphaShape(float alpha = 1.0f, int sampleStep = 1, int maxNeighbors = 15)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除点云显示
            ClearPointCloud();

            // 生成表面
            var mesh = SurfaceTriangulator.GenerateAlphaShape(_currentData, alpha, sampleStep, maxNeighbors);
            if (mesh != null)
            {
                ShowSurfaceMesh(mesh, MediaColor.FromRgb(200, 180, 255));
            }
        }

        /// <summary>
        /// 3. 使用Delaunay三角剖分生成表面
        /// 适合需要高质量网格的场景，如有限元分析
        /// </summary>
        /// <param name="qualityThreshold">质量阈值</param>
        /// <param name="searchRadiusMultiplier">搜索半径倍数</param>
        /// <param name="sampleStep">采样步长（1=全部点）</param>
        /// <param name="maxNeighbors">最大邻居数量</param>
        public void GenerateDelaunay(float qualityThreshold = 0.3f, float searchRadiusMultiplier = 5f, int sampleStep = 1, int maxNeighbors = 15)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除点云显示
            ClearPointCloud();

            // 生成表面
            var mesh = SurfaceTriangulator.GenerateDelaunay3D(_currentData, qualityThreshold, searchRadiusMultiplier, sampleStep, maxNeighbors);
            if (mesh != null)
            {
                ShowSurfaceMesh(mesh, MediaColor.FromRgb(255, 200, 180));
            }
        }

        /// <summary>
        /// 3. 使用Poisson泊松重建生成表面
        /// 适合需要水密表面和抗噪的场景，如扫描数据重建
        /// </summary>
        /// <param name="depth">八叉树深度</param>
        /// <param name="samplesPerNode">每节点最小采样数</param>
        /// <param name="maxCells">最大处理单元数（0=无限制）</param>
        public void GeneratePoisson(int depth = 8, float samplesPerNode = 1.0f, int maxCells = 0)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除点云显示
            ClearPointCloud();

            // 生成表面
            var mesh = SurfaceTriangulator.GeneratePoissonSurface(_currentData, depth, samplesPerNode, maxCells);
            if (mesh != null)
            {
                ShowSurfaceMesh(mesh, MediaColor.FromRgb(180, 255, 200));
            }
        }

        /// <summary>
        /// 3. 使用MLS移动最小二乘生成表面
        /// 适合需要平滑表面和去噪的场景，如地形建模
        /// </summary>
        /// <param name="searchRadius">搜索半径</param>
        /// <param name="sampleStep">采样步长（1=全部点）</param>
        /// <param name="maxNeighbors">最大邻居数量</param>
        public void GenerateMLS(float searchRadius = 0.5f, int sampleStep = 1, int maxNeighbors = 12)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除点云显示
            ClearPointCloud();

            // 生成表面
            var mesh = SurfaceTriangulator.GenerateMLSSurface(_currentData, searchRadius, sampleStep, maxNeighbors);
            if (mesh != null)
            {
                ShowSurfaceMesh(mesh, MediaColor.FromRgb(255, 220, 180));
            }
        }

        /// <summary>
        /// 4. 清除显示：所有点云和模型数据都清理掉
        /// </summary>
        public void ClearAll()
        {
            // 清除点云
            ClearPointCloud();

            // 清除表面
            ClearSurface();

            // 清除 Assimp Scene
            ClearScene();

            // 清除数据引用
            _currentData = null;
            _currentGeometry = null;
            _displayRatio = 1.0f;
            _showByZColor = false;
        }

        /// <summary>
        /// 设置点大小
        /// </summary>
        public void SetPointSize(float pointSize)
        {
            _currentPointSize = pointSize;
            if (_pointModel != null)
            {
                _pointModel.Size = new WpfSize(pointSize, pointSize);
            }
        }

        /// <summary>
        /// 设置是否按Z轴着色
        /// </summary>
        public void SetZColorEnabled(bool enabled, float pointSize = 2f)
        {
            _showByZColor = enabled;
            if (_currentData != null && _pointModel != null)
            {
                // 重新构建点云以应用颜色
                BuildAndShowPointCloud(pointSize);
            }
        }

        /// <summary>
        /// 构建并显示点云（内部方法）
        /// </summary>
        private void BuildAndShowPointCloud(float pointSize = 2f)
        {
            if (_currentData == null || _currentData.PointCount == 0) return;

            // 先清除现有显示
            ClearPointCloud();

            var points = _currentData.Points;
            var coloredPoints = _currentData.ColoredPoints;

            var positions = new HelixToolkit.Vector3Collection();
            var colors = new HelixToolkit.Color4Collection();
            var indices = new HelixToolkit.IntCollection();

            // 获取Z轴范围（用于着色）
            float minZ = float.MaxValue, maxZ = float.MinValue;
            if (coloredPoints != null && coloredPoints.Count > 0)
            {
                foreach (var pt in coloredPoints)
                {
                    if (pt.Z < minZ) minZ = pt.Z;
                    if (pt.Z > maxZ) maxZ = pt.Z;
                }
            }
            else
            {
                foreach (var pt in points)
                {
                    if (pt.Z < minZ) minZ = pt.Z;
                    if (pt.Z > maxZ) maxZ = pt.Z;
                }
            }
            float zRange = maxZ - minZ;

            // 根据显示比例计算步长
            int step = _displayRatio >= 1.0f ? 1 : Math.Max(1, (int)(1.0f / _displayRatio));
            int count = 0;

            if (coloredPoints != null && coloredPoints.Count > 0)
            {
                for (int i = 0; i < coloredPoints.Count; i += step)
                {
                    var pt = coloredPoints[i];
                    positions.Add(new System.Numerics.Vector3(pt.X, pt.Y, pt.Z));

                    // Z轴着色模式
                    if (_showByZColor && zRange > 0.001f)
                    {
                        float t = (pt.Z - minZ) / zRange;
                        var jetColor = JetColorMap(t);
                        colors.Add(jetColor);
                    }
                    else
                    {
                        colors.Add(new HelixToolkit.Maths.Color4(pt.R / 255f, pt.G / 255f, pt.B / 255f, 1f));
                    }

                    indices.Add(count++);
                }
            }
            else
            {
                for (int i = 0; i < points.Count; i += step)
                {
                    var pt = points[i];
                    positions.Add(new System.Numerics.Vector3(pt.X, pt.Y, pt.Z));

                    // Z轴着色模式
                    if (_showByZColor && zRange > 0.001f)
                    {
                        float t = (pt.Z - minZ) / zRange;
                        var jetColor = JetColorMap(t);
                        colors.Add(jetColor);
                    }
                    else
                    {
                        colors.Add(new HelixToolkit.Maths.Color4(1f, 1f, 1f, 1f));
                    }

                    indices.Add(count++);
                }
            }

            // 创建点几何体
            var geometry = new HelixToolkit.SharpDX.PointGeometry3D
            {
                Positions = positions,
                Colors = colors,
                Indices = indices
            };
            _currentGeometry = geometry;

            // 创建点渲染模型
            _pointModel = new PointGeometryModel3D
            {
                Geometry = geometry,
                Size = new WpfSize(pointSize, pointSize),
                Color = Colors.White
            };
            // 添加到视口
            _viewport.Items.Add(_pointModel);

        }

        /// <summary>
        /// 清除点云显示
        /// </summary>
        private void ClearPointCloud()
        {
            if (_pointModel != null)
            {
                _viewport.Items.Remove(_pointModel);
                _pointModel = null;
            }
        }

        /// <summary>
        /// 显示表面网格
        /// </summary>
        private void ShowSurfaceMesh(HelixToolkit.SharpDX.MeshGeometry3D meshGeometry, MediaColor color)
        {
            // 先清除现有表面
            ClearSurface();

            var surfaceMaterial = new PhongMaterial
            {
                DiffuseColor = new HelixToolkit.Maths.Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1f),
                AmbientColor = new HelixToolkit.Maths.Color4(0.2f, 0.2f, 0.2f, 1f),
                SpecularColor = new HelixToolkit.Maths.Color4(0.8f, 0.8f, 0.8f, 1f),
                SpecularShininess = 64
            };

            _surfaceMesh = new MeshGeometryModel3D
            {
                Geometry = meshGeometry,
                Material = surfaceMaterial,
                CullMode = SharpDX.Direct3D11.CullMode.None
            };

            _viewport.Items.Add(_surfaceMesh);
        }

        /// <summary>
        /// 清除表面模型
        /// </summary>
        private void ClearSurface()
        {
            if (_surfaceMesh != null)
            {
                _viewport.Items.Remove(_surfaceMesh);
                _surfaceMesh = null;
            }
        }

        /// <summary>
        /// 清除 Assimp Scene 显示
        /// </summary>
        private void ClearScene()
        {
            if (_sceneModel != null)
            {
                _viewport.Items.Remove(_sceneModel);
                _sceneModel = null;
            }
        }

        /// <summary>
        /// 直接加载 HelixToolkitScene 到视口（通过 SceneNodeGroupModel3D 桥接）
        /// 保留原始网格拓扑、材质和层次结构，不经过 PointCloudData 转换
        /// </summary>
        /// <param name="scene">Assimp 导入返回的 HelixToolkitScene（调用方负责生命周期管理）</param>
        public void LoadScene(HelixToolkitScene scene)
        {
            if (scene == null || scene.Root == null)
                throw new ArgumentNullException("scene", "Scene 或 Scene.Root 不能为 null");

            // 清除现有内容
            ClearAll();

            // 创建 SceneNodeGroupModel3D 作为 SceneNode → Element3D 的桥梁
            _sceneModel = new SceneNodeGroupModel3D();

            // 将 Scene 的根节点整体添加到 SceneNodeGroupModel3D 的 GroupNode 中
            // 注意：不能遍历 scene.Root.Items 将子节点逐个添加，
            // 因为 Importer.Load() 返回的 Scene 中子节点已附加到 scene.Root 上，
            // 再次 AddChildNode 会触发 "SceneNode already attach to a different node" 异常
            _sceneModel.GroupNode.AddChildNode(scene.Root);

            // 添加到视口
            _viewport.Items.Add(_sceneModel);

            // 自适应调整相机视角
            FitViewToScene(scene.Root);
        }

        /// <summary>
        /// 根据 SceneNode 的边界框调整相机位置
        /// 递归累加所有子节点的 Bounds，避免依赖可能未计算的 rootNode.Bounds
        /// </summary>
        private void FitViewToScene(SceneNode rootNode)
        {
            if (rootNode == null) return;

            // 递归累加所有子节点的 Bounds，获得可靠的总边界框
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool hasBounds = false;

            void AccumulateBounds(SceneNode node)
            {
                var b = node.Bounds;

                // 检查是否为无效/空边界框：Minimum >= Maximum 是空边界框的典型特征
                bool isEmpty = b.Minimum.X >= b.Maximum.X
                            && b.Minimum.Y >= b.Maximum.Y
                            && b.Minimum.Z >= b.Maximum.Z;

                if (!isEmpty)
                {
                    // 额外过滤无穷大或 NaN 的异常值
                    if (float.IsInfinity(b.Minimum.X) || float.IsInfinity(b.Maximum.X) ||
                        float.IsNaN(b.Minimum.X) || float.IsNaN(b.Maximum.X) ||
                        float.IsInfinity(b.Minimum.Y) || float.IsInfinity(b.Maximum.Y) ||
                        float.IsNaN(b.Minimum.Y) || float.IsNaN(b.Maximum.Y) ||
                        float.IsInfinity(b.Minimum.Z) || float.IsInfinity(b.Maximum.Z) ||
                        float.IsNaN(b.Minimum.Z) || float.IsNaN(b.Maximum.Z))
                    {
                        isEmpty = true;
                    }
                }

                if (!isEmpty)
                {
                    if (b.Minimum.X < minX) minX = b.Minimum.X;
                    if (b.Maximum.X > maxX) maxX = b.Maximum.X;
                    if (b.Minimum.Y < minY) minY = b.Minimum.Y;
                    if (b.Maximum.Y > maxY) maxY = b.Maximum.Y;
                    if (b.Minimum.Z < minZ) minZ = b.Minimum.Z;
                    if (b.Maximum.Z > maxZ) maxZ = b.Maximum.Z;
                    hasBounds = true;
                }

                if (node is GroupNode group)
                {
                    foreach (var child in group.Items)
                    {
                        AccumulateBounds(child);
                    }
                }
            }

            AccumulateBounds(rootNode);

            // 检查边界框是否有效
            if (!hasBounds || minX > maxX || minY > maxY || minZ > maxZ)
                return;

            var center = new System.Numerics.Vector3(
                (minX + maxX) / 2,
                (minY + maxY) / 2,
                (minZ + maxZ) / 2);
            _modelCenter = new System.Windows.Media.Media3D.Point3D(center.X, center.Y, center.Z);

            float radius = (float)Math.Sqrt(
                (maxX - minX) * (maxX - minX) +
                (maxY - minY) * (maxY - minY) +
                (maxZ - minZ) * (maxZ - minZ)) / 2;

            if (radius < 0.001f) radius = 100f;

            // 过滤计算过程中产生的无效半径
            if (float.IsInfinity(radius) || float.IsNaN(radius))
                return;

            // 设置相机位置
            if (_viewport.Camera is HelixToolkit.Wpf.SharpDX.PerspectiveCamera perspectiveCamera)
            {
                float fov = (float)(perspectiveCamera.FieldOfView * Math.PI / 180.0);
                float distance = radius / (float)Math.Tan(fov / 2) * 1.5f;

                var cameraPosition = new System.Windows.Media.Media3D.Point3D(
                    center.X + distance * 0.7,
                    center.Y - distance * 0.7,
                    center.Z + distance * 0.5);

                perspectiveCamera.Position = cameraPosition;
                perspectiveCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(
                    center.X - cameraPosition.X,
                    center.Y - cameraPosition.Y,
                    center.Z - cameraPosition.Z);
                perspectiveCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
            }
            else if (_viewport.Camera is HelixToolkit.Wpf.SharpDX.OrthographicCamera orthoCamera)
            {
                orthoCamera.Position = new System.Windows.Media.Media3D.Point3D(
                    center.X + radius * 2,
                    center.Y - radius * 2,
                    center.Z + radius);
                orthoCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(
                    -radius * 2, radius * 2, -radius);
                orthoCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                orthoCamera.Width = radius * 3;
            }
        }

        /// <summary>
        /// Jet色图：将[0,1]值映射到蓝→青→绿→黄→红
        /// </summary>
        private static HelixToolkit.Maths.Color4 JetColorMap(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float r, g, b;

            if (t < 0.25f)
            {
                r = 0;
                g = t * 4;
                b = 1f;
            }
            else if (t < 0.5f)
            {
                r = 0;
                g = 1f;
                b = 1f - (t - 0.25f) * 4;
            }
            else if (t < 0.75f)
            {
                r = (t - 0.5f) * 4;
                g = 1f;
                b = 0;
            }
            else
            {
                r = 1f;
                g = 1f - (t - 0.75f) * 4;
                b = 0;
            }

            return new HelixToolkit.Maths.Color4(r, g, b, 1f);
        }

        /// <summary>
        /// 设置点颜色（覆盖原有颜色）
        /// </summary>
        public void SetPointColor(MediaColor color)
        {
            if (_pointModel != null)
            {
                _pointModel.Color = color;
            }
            if (_currentGeometry != null)
            {
                var newColors = new HelixToolkit.Color4Collection();
                for (int i = 0; i < _currentGeometry.Positions?.Count; i++)
                {
                    newColors.Add(new HelixToolkit.Maths.Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1f));
                }
                _currentGeometry.Colors = newColors;
            }
        }

        /// <summary>
        /// 设置表面颜色
        /// </summary>
        public void SetSurfaceColor(MediaColor color)
        {
            if (_surfaceMesh?.Material is PhongMaterial surfaceMaterial)
            {
                surfaceMaterial.DiffuseColor = new HelixToolkit.Maths.Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
            }
        }

        /// <summary>
        /// 设置相机控制模式
        /// </summary>
        /// <param name="mode">相机控制模式</param>
        public void SetCameraMode(CameraControlMode mode)
        {
            _cameraMode = mode;

            // 根据模式更新视口交互标志
            switch (mode)
            {
                case CameraControlMode.Rotate:
                    _viewport.IsRotationEnabled = true;
                    _viewport.IsZoomEnabled = true;
                    _viewport.IsPanEnabled = true;
                    _viewport.IsChangeFieldOfViewEnabled = true;
                    //_viewport.CameraMode = CameraMode.Inspect;
                    _viewport.FixedRotationPointEnabled = true;
                    _viewport.RotateAroundMouseDownPoint = false;
                    break;
                case CameraControlMode.Move:
                    _viewport.IsRotationEnabled = true;
                    _viewport.IsZoomEnabled = true;
                    _viewport.IsPanEnabled = true;
                    _viewport.IsChangeFieldOfViewEnabled = true;

                    _viewport.CameraRotationMode = CameraRotationMode.Turnball;
                    break;
                case CameraControlMode.Round:
                    _viewport.IsRotationEnabled = true;
                    _viewport.IsZoomEnabled = true;
                    _viewport.IsPanEnabled = true;
                    _viewport.IsChangeFieldOfViewEnabled = true;

                    //_viewport.CameraMode = CameraMode.Inspect;
                    _viewport.RotateAroundMouseDownPoint = true;
                    _viewport.FixedRotationPointEnabled = false;
                    break;
            }
        }

        /// <summary>
        /// 重置相机到模型中心
        /// 根据当前点云数据或 SceneModel 计算中心点，并将相机定位到合适距离
        /// </summary>
        public void ResetCameraToCenter()
        {
            System.Numerics.Vector3 center = GetModelCenter();
            _modelCenter = new System.Windows.Media.Media3D.Point3D(center.X, center.Y, center.Z);

            float radius = 100f;
            if (_currentData != null && _currentData.PointCount > 0)
            {
                // 计算点云边界半径
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                if (_currentData.HasColor)
                {
                    foreach (var p in _currentData.ColoredPoints)
                    {
                        if (p.X < minX) minX = p.X;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.Y > maxY) maxY = p.Y;
                        if (p.Z < minZ) minZ = p.Z;
                        if (p.Z > maxZ) maxZ = p.Z;
                    }
                }
                else
                {
                    foreach (var p in _currentData.Points)
                    {
                        if (p.X < minX) minX = p.X;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.Y > maxY) maxY = p.Y;
                        if (p.Z < minZ) minZ = p.Z;
                        if (p.Z > maxZ) maxZ = p.Z;
                    }
                }

                radius = (float)Math.Sqrt(
                    (maxX - minX) * (maxX - minX) +
                    (maxY - minY) * (maxY - minY) +
                    (maxZ - minZ) * (maxZ - minZ)) / 2;

                if (radius < 0.001f) radius = 100f;
            }
            else if (_sceneModel != null && _sceneModel.GroupNode != null)
            {
                // Scene 路径：从 _sceneModel.GroupNode 递归计算边界框
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                bool hasBounds = false;

                void AccumulateBounds(SceneNode node)
                {
                    var b = node.Bounds;

                    // 检查是否为无效/空边界框：Minimum >= Maximum 是空边界框的典型特征
                    bool isEmpty = b.Minimum.X >= b.Maximum.X
                                && b.Minimum.Y >= b.Maximum.Y
                                && b.Minimum.Z >= b.Maximum.Z;

                    if (!isEmpty)
                    {
                        // 额外过滤无穷大或 NaN 的异常值
                        if (float.IsInfinity(b.Minimum.X) || float.IsInfinity(b.Maximum.X) ||
                            float.IsNaN(b.Minimum.X) || float.IsNaN(b.Maximum.X) ||
                            float.IsInfinity(b.Minimum.Y) || float.IsInfinity(b.Maximum.Y) ||
                            float.IsNaN(b.Minimum.Y) || float.IsNaN(b.Maximum.Y) ||
                            float.IsInfinity(b.Minimum.Z) || float.IsInfinity(b.Maximum.Z) ||
                            float.IsNaN(b.Minimum.Z) || float.IsNaN(b.Maximum.Z))
                        {
                            isEmpty = true;
                        }
                    }

                    if (!isEmpty)
                    {
                        if (b.Minimum.X < minX) minX = b.Minimum.X;
                        if (b.Maximum.X > maxX) maxX = b.Maximum.X;
                        if (b.Minimum.Y < minY) minY = b.Minimum.Y;
                        if (b.Maximum.Y > maxY) maxY = b.Maximum.Y;
                        if (b.Minimum.Z < minZ) minZ = b.Minimum.Z;
                        if (b.Maximum.Z > maxZ) maxZ = b.Maximum.Z;
                        hasBounds = true;
                    }

                    if (node is GroupNode group)
                    {
                        foreach (var child in group.Items)
                        {
                            AccumulateBounds(child);
                        }
                    }
                }

                AccumulateBounds(_sceneModel.GroupNode);

                if (hasBounds && minX <= maxX && minY <= maxY && minZ <= maxZ)
                {
                    center = new System.Numerics.Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
                    _modelCenter = new System.Windows.Media.Media3D.Point3D(center.X, center.Y, center.Z);
                    radius = (float)Math.Sqrt(
                        (maxX - minX) * (maxX - minX) +
                        (maxY - minY) * (maxY - minY) +
                        (maxZ - minZ) * (maxZ - minZ)) / 2;
                    if (radius < 0.001f) radius = 100f;
                    if (float.IsInfinity(radius) || float.IsNaN(radius))
                        radius = 100f;
                }
            }

            // 设置相机位置
            if (_viewport.Camera is HelixToolkit.Wpf.SharpDX.PerspectiveCamera perspectiveCamera)
            {
                float fov = (float)(perspectiveCamera.FieldOfView * Math.PI / 180.0);
                float distance = radius / (float)Math.Tan(fov / 2) * 1.5f;

                var cameraPosition = new System.Windows.Media.Media3D.Point3D(
                    center.X + distance * 0.7,
                    center.Y - distance * 0.7,
                    center.Z + distance * 0.5);

                perspectiveCamera.Position = cameraPosition;
                perspectiveCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(
                    center.X - cameraPosition.X,
                    center.Y - cameraPosition.Y,
                    center.Z - cameraPosition.Z);
                perspectiveCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
            }
            else if (_viewport.Camera is HelixToolkit.Wpf.SharpDX.OrthographicCamera orthoCamera)
            {
                orthoCamera.Position = new System.Windows.Media.Media3D.Point3D(
                    center.X + radius * 2,
                    center.Y - radius * 2,
                    center.Z + radius);
                orthoCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(
                    -radius * 2, radius * 2, -radius);
                orthoCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                orthoCamera.Width = radius * 3;
            }
        }

        /// <summary>
        /// 计算当前点云数据的中心点
        /// </summary>
        /// <returns>点云中心点坐标，无数据时返回(0,0,0)</returns>
        public System.Numerics.Vector3 GetModelCenter()
        {
            if (_currentData == null || _currentData.PointCount == 0)
            {
                return new System.Numerics.Vector3(0, 0, 0);
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            if (_currentData.HasColor)
            {
                foreach (var p in _currentData.ColoredPoints)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.Z > maxZ) maxZ = p.Z;
                }
            }
            else
            {
                foreach (var p in _currentData.Points)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.Z > maxZ) maxZ = p.Z;
                }
            }

            return new System.Numerics.Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        }

        /// <summary>
        /// 初始化相机和视口配置（无参构造时调用）
        /// </summary>
        private void InitializeCamera()
        {
            _viewport.EffectsManager = new DefaultEffectsManager();
            _viewport.BackgroundColor = MediaColor.FromRgb(20, 20, 30);
            _viewport.ShowCoordinateSystem = true;
            _viewport.ShowViewCube = true;
            _viewport.ShowCameraTarget = true;
            _viewport.RotateAroundMouseDownPoint = false;
            _viewport.IsRotationEnabled = false;
            _viewport.IsZoomEnabled = false;
            _viewport.IsPanEnabled = false;

            _viewport.Camera = new PerspectiveCamera
            {
                Position = new System.Windows.Media.Media3D.Point3D(5, 5, 10),
                LookDirection = new System.Windows.Media.Media3D.Vector3D(-5, -5, -10),
                UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0),
                FieldOfView = 45,
                NearPlaneDistance = 0.001,
                FarPlaneDistance = 100000
            };
        }

        public void Dispose()
        {
            ClearAll();

            if (_viewport != null)
            {
                var effectsManager = _viewport.EffectsManager;
                if (effectsManager is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _viewport.EffectsManager = null;
                _viewport.Items?.Clear();
                _viewport.Dispose();
            }
        }
    }
}
