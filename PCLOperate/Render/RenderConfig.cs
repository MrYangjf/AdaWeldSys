using AdaWeldSystem.FileOperate;
using AdaWeldSystem.Comm;
using System;
using System.IO;

namespace AdaWeldSystem.PCLOperate.Render
{
    /// <summary>
    /// 表面重建算法类型枚举
    /// </summary>
    public enum SurfaceRenderAlgorithm
    {
        /// <summary>邻域搜索重建</summary>
        NeighborSearch,
        /// <summary>Ball Pivoting重建</summary>
        BallPivoting,
        /// <summary>Alpha Shape重建</summary>
        AlphaShape,
        /// <summary>Delaunay 3D重建</summary>
        Delaunay3D,
        /// <summary>Poisson重建</summary>
        Poisson,
        /// <summary>MLS重建</summary>
        MLS
    }

    /// <summary>
    /// 表面渲染参数配置类
    /// 使用INI文件持久化渲染参数配置
    /// </summary>
    public static class RenderConfig
    {
        private const string Tag = "RenderConfig";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region 常量与字段

        /// <summary>INI配置文件路径</summary>
        private static readonly string IniFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "RenderConfig.ini");

        /// <summary>INI文件节名</summary>
        private const string IniSection = "RenderConfig";

        /// <summary>线程同步锁</summary>
        private static readonly object _lock = new object();

        /// <summary>是否已初始化</summary>
        private static bool _isInitialized = false;

        #endregion

        #region 构造函数与初始化

        /// <summary>
        /// 静态构造函数 - 类首次使用时自动初始化
        /// 检查配置文件是否存在，不存在则生成默认配置
        /// </summary>
        static RenderConfig()
        {
            SelectedAlgorithm = SurfaceRenderAlgorithm.NeighborSearch;
            NeighborSearchRadius = 2f;
            NeighborMaxNeighbors = 30;
            NeighborSampleStep = 1;
            BallPivotingRadius = 2f;
            BallPivotingSampleStep = 1;
            BallPivotingMaxNeighbors = 30;
            AlphaShapeAlpha = 2f;
            AlphaShapeSampleStep = 1;
            AlphaShapeMaxNeighbors = 30;
            DelaunayQualityThreshold = 0.1f;
            DelaunaySearchRadiusMultiplier = 1.5f;
            DelaunaySampleStep = 1;
            DelaunayMaxNeighbors = 30;
            PoissonDepth = 8;
            PoissonSamplesPerNode = 1;
            PoissonMaxCells = 100000;
            MlsSearchRadius = 2f;
            MlsSampleStep = 1;
            MlsMaxNeighbors = 30;
            PointSize = 2f;
            ZColorEnabled = false;
            DisplayRatio = 1.0f;

            EnsureInitialized();
        }

        /// <summary>
        /// 确保配置已初始化
        /// 如果配置文件不存在，使用当前属性默认值生成默认INI文件
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    if (!File.Exists(IniFilePath))
                    {
                        // 配置文件不存在，使用当前默认值生成默认配置
                        SaveToIni();
                    }
                    else
                    {
                        // 配置文件存在，加载配置
                        LoadFromIniInternal();
                    }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Log("RenderConfig初始化失败 原因是 " + ex.Message, MessageLevel.Error);
                    // 初始化失败时使用属性默认值继续运行
                    _isInitialized = true;
                }
            }
        }

        #endregion

        #region 算法选择

        /// <summary>当前选定的渲染算法</summary>
        public static SurfaceRenderAlgorithm SelectedAlgorithm { get; set; }
        #endregion

        #region 邻域搜索参数

        /// <summary>邻域搜索半径</summary>
        public static float NeighborSearchRadius { get; set; }
        /// <summary>邻域最大邻居数</summary>
        public static int NeighborMaxNeighbors { get; set; }
        /// <summary>邻域采样步长</summary>
        public static int NeighborSampleStep { get; set; }
        #endregion

        #region Ball Pivoting参数

        /// <summary>Ball Pivoting半径</summary>
        public static float BallPivotingRadius { get; set; }
        /// <summary>Ball Pivoting采样步长</summary>
        public static int BallPivotingSampleStep { get; set; }
        /// <summary>Ball Pivoting最大邻居数</summary>
        public static int BallPivotingMaxNeighbors { get; set; }
        #endregion

        #region Alpha Shape参数

        /// <summary>Alpha值</summary>
        public static float AlphaShapeAlpha { get; set; }
        /// <summary>Alpha Shape采样步长</summary>
        public static int AlphaShapeSampleStep { get; set; }
        /// <summary>Alpha Shape最大邻居数</summary>
        public static int AlphaShapeMaxNeighbors { get; set; }
        #endregion

        #region Delaunay 3D参数

        /// <summary>Delaunay质量阈值</summary>
        public static float DelaunayQualityThreshold { get; set; }
        /// <summary>Delaunay搜索半径倍数</summary>
        public static float DelaunaySearchRadiusMultiplier { get; set; }
        /// <summary>Delaunay采样步长</summary>
        public static int DelaunaySampleStep { get; set; }
        /// <summary>Delaunay最大邻居数</summary>
        public static int DelaunayMaxNeighbors { get; set; }
        #endregion

        #region Poisson参数

        /// <summary>Poisson深度</summary>
        public static int PoissonDepth { get; set; }
        /// <summary>Poisson每节点采样数</summary>
        public static int PoissonSamplesPerNode { get; set; }
        /// <summary>Poisson最大单元格数</summary>
        public static int PoissonMaxCells { get; set; }
        #endregion

        #region MLS参数

        /// <summary>MLS搜索半径</summary>
        public static float MlsSearchRadius { get; set; }
        /// <summary>MLS采样步长</summary>
        public static int MlsSampleStep { get; set; }
        /// <summary>MLS最大邻居数</summary>
        public static int MlsMaxNeighbors { get; set; }
        #endregion

        #region 点云显示参数

        /// <summary>点大小</summary>
        public static float PointSize { get; set; }
        /// <summary>是否按Z轴高度着色</summary>
        public static bool ZColorEnabled { get; set; }
        /// <summary>显示比例（1.0=全部, 0.5=一半, 0.1=10%）</summary>
        public static float DisplayRatio { get; set; }
        #endregion

        #region 持久化方法

        /// <summary>
        /// 保存配置到INI文件
        /// </summary>
        public static void SaveToIni()
        {
            lock (_lock)
            {
                try
                {
                    string configDir = Path.GetDirectoryName(IniFilePath);
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    var ini = new INIFile(IniFilePath);

                    // 保存算法选择
                    ini.WriteInt(IniSection, "SelectedAlgorithm", (int)SelectedAlgorithm);

                    // 保存邻域搜索参数
                    ini.WriteDouble(IniSection, "NeighborSearchRadius", NeighborSearchRadius);
                    ini.WriteInt(IniSection, "NeighborMaxNeighbors", NeighborMaxNeighbors);
                    ini.WriteInt(IniSection, "NeighborSampleStep", NeighborSampleStep);

                    // 保存Ball Pivoting参数
                    ini.WriteDouble(IniSection, "BallPivotingRadius", BallPivotingRadius);
                    ini.WriteInt(IniSection, "BallPivotingSampleStep", BallPivotingSampleStep);
                    ini.WriteInt(IniSection, "BallPivotingMaxNeighbors", BallPivotingMaxNeighbors);

                    // 保存Alpha Shape参数
                    ini.WriteDouble(IniSection, "AlphaShapeAlpha", AlphaShapeAlpha);
                    ini.WriteInt(IniSection, "AlphaShapeSampleStep", AlphaShapeSampleStep);
                    ini.WriteInt(IniSection, "AlphaShapeMaxNeighbors", AlphaShapeMaxNeighbors);

                    // 保存Delaunay参数
                    ini.WriteDouble(IniSection, "DelaunayQualityThreshold", DelaunayQualityThreshold);
                    ini.WriteDouble(IniSection, "DelaunaySearchRadiusMultiplier", DelaunaySearchRadiusMultiplier);
                    ini.WriteInt(IniSection, "DelaunaySampleStep", DelaunaySampleStep);
                    ini.WriteInt(IniSection, "DelaunayMaxNeighbors", DelaunayMaxNeighbors);

                    // 保存Poisson参数
                    ini.WriteInt(IniSection, "PoissonDepth", PoissonDepth);
                    ini.WriteInt(IniSection, "PoissonSamplesPerNode", PoissonSamplesPerNode);
                    ini.WriteInt(IniSection, "PoissonMaxCells", PoissonMaxCells);

                    // 保存MLS参数
                    ini.WriteDouble(IniSection, "MlsSearchRadius", MlsSearchRadius);
                    ini.WriteInt(IniSection, "MlsSampleStep", MlsSampleStep);
                    ini.WriteInt(IniSection, "MlsMaxNeighbors", MlsMaxNeighbors);

                    // 保存点云显示参数
                    ini.WriteDouble(IniSection, "PointSize", PointSize);
                    ini.WriteBool(IniSection, "ZColorEnabled", ZColorEnabled);
                    ini.WriteDouble(IniSection, "DisplayRatio", DisplayRatio);

                    ini.SaveToFile();
                }
                catch (Exception ex)
                {
                    Log("RenderConfig保存失败 " + ex.Message, MessageLevel.Error);
                }
            }
        }

        /// <summary>
        /// 从INI文件加载配置
        /// 公开方法，供外部手动触发配置加载
        /// </summary>
        public static void LoadFromIni()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(IniFilePath))
                    {
                        SaveToIni();
                        return;
                    }

                    LoadFromIniInternal();
                }
                catch (Exception ex)
                {
                    Log("RenderConfig加载失败 " + ex.Message, MessageLevel.Error);
                }
            }
        }

        /// <summary>
        /// 从INI文件加载配置（内部实现）
        /// </summary>
        private static void LoadFromIniInternal()
        {
            var ini = new INIFile(IniFilePath);

            // 加载算法选择
            SelectedAlgorithm = (SurfaceRenderAlgorithm)ini.ReadInt(IniSection, "SelectedAlgorithm", 0);

            // 加载邻域搜索参数
            NeighborSearchRadius = (float)ini.ReadDouble(IniSection, "NeighborSearchRadius", 2.0);
            NeighborMaxNeighbors = ini.ReadInt(IniSection, "NeighborMaxNeighbors", 30);
            NeighborSampleStep = ini.ReadInt(IniSection, "NeighborSampleStep", 1);

            // 加载Ball Pivoting参数
            BallPivotingRadius = (float)ini.ReadDouble(IniSection, "BallPivotingRadius", 2.0);
            BallPivotingSampleStep = ini.ReadInt(IniSection, "BallPivotingSampleStep", 1);
            BallPivotingMaxNeighbors = ini.ReadInt(IniSection, "BallPivotingMaxNeighbors", 30);

            // 加载Alpha Shape参数
            AlphaShapeAlpha = (float)ini.ReadDouble(IniSection, "AlphaShapeAlpha", 2.0);
            AlphaShapeSampleStep = ini.ReadInt(IniSection, "AlphaShapeSampleStep", 1);
            AlphaShapeMaxNeighbors = ini.ReadInt(IniSection, "AlphaShapeMaxNeighbors", 30);

            // 加载Delaunay参数
            DelaunayQualityThreshold = (float)ini.ReadDouble(IniSection, "DelaunayQualityThreshold", 0.1);
            DelaunaySearchRadiusMultiplier = (float)ini.ReadDouble(IniSection, "DelaunaySearchRadiusMultiplier", 1.5);
            DelaunaySampleStep = ini.ReadInt(IniSection, "DelaunaySampleStep", 1);
            DelaunayMaxNeighbors = ini.ReadInt(IniSection, "DelaunayMaxNeighbors", 30);

            // 加载Poisson参数
            PoissonDepth = ini.ReadInt(IniSection, "PoissonDepth", 8);
            PoissonSamplesPerNode = ini.ReadInt(IniSection, "PoissonSamplesPerNode", 1);
            PoissonMaxCells = ini.ReadInt(IniSection, "PoissonMaxCells", 100000);

            // 加载MLS参数
            MlsSearchRadius = (float)ini.ReadDouble(IniSection, "MlsSearchRadius", 2.0);
            MlsSampleStep = ini.ReadInt(IniSection, "MlsSampleStep", 1);
            MlsMaxNeighbors = ini.ReadInt(IniSection, "MlsMaxNeighbors", 30);

            // 加载点云显示参数
            PointSize = (float)ini.ReadDouble(IniSection, "PointSize", 2.0);
            ZColorEnabled = ini.ReadBool(IniSection, "ZColorEnabled", false);
            DisplayRatio = (float)ini.ReadDouble(IniSection, "DisplayRatio", 1.0);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取算法中文名称
        /// </summary>
        /// <param name="algorithm">算法类型</param>
        /// <returns>中文名称</returns>
        public static string GetAlgorithmName(SurfaceRenderAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case SurfaceRenderAlgorithm.NeighborSearch:
                    return "邻域搜索重建";
                case SurfaceRenderAlgorithm.BallPivoting:
                    return "Ball Pivoting重建";
                case SurfaceRenderAlgorithm.AlphaShape:
                    return "Alpha Shape重建";
                case SurfaceRenderAlgorithm.Delaunay3D:
                    return "Delaunay 3D重建";
                case SurfaceRenderAlgorithm.Poisson:
                    return "Poisson重建";
                case SurfaceRenderAlgorithm.MLS:
                    return "MLS重建";
                default:
                    return "未知算法";
            }
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public static void ResetToDefaults()
        {
            SelectedAlgorithm = SurfaceRenderAlgorithm.NeighborSearch;

            NeighborSearchRadius = 2f;
            NeighborMaxNeighbors = 30;
            NeighborSampleStep = 1;

            BallPivotingRadius = 2f;
            BallPivotingSampleStep = 1;
            BallPivotingMaxNeighbors = 30;

            AlphaShapeAlpha = 2f;
            AlphaShapeSampleStep = 1;
            AlphaShapeMaxNeighbors = 30;

            DelaunayQualityThreshold = 0.1f;
            DelaunaySearchRadiusMultiplier = 1.5f;
            DelaunaySampleStep = 1;
            DelaunayMaxNeighbors = 30;

            PoissonDepth = 8;
            PoissonSamplesPerNode = 1;
            PoissonMaxCells = 100000;

            MlsSearchRadius = 2f;
            MlsSampleStep = 1;
            MlsMaxNeighbors = 30;

            PointSize = 2f;
            ZColorEnabled = false;
            DisplayRatio = 1.0f;
        }

        #endregion
    }
}
