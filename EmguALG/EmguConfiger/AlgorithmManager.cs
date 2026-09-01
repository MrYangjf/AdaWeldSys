using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ProductFileManager;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.EmguALG.EmguConfiger
{
    #region 算子枚举

    /// <summary>滤波算子枚举</summary>
    public enum FilterOperation
    {
        GaussianBlur,
        MedianBlur,
        MeanBlur,
        BilateralFilter,
        Sharpen
    }

    /// <summary>形态学算子枚举</summary>
    public enum MorphologyOperation
    {
        Dilate,
        Erode,
        Open,
        Close
    }

    /// <summary>边缘检测算子枚举</summary>
    public enum EdgeDetectionOperation
    {
        Canny,
        Gradient,
        Laplacian,
        Roberts
    }

    /// <summary>特征提取算子枚举</summary>
    public enum FeatureExtractionOperation
    {
        ContourExtraction,
        SkeletonExtraction
    }

    #endregion

    #region 事件参数与结果

    /// <summary>
    /// 算法执行完成事件参数
    /// </summary>
    public class AlgorithmCompletedEventArgs : EventArgs
    {
        /// <summary>处理后的图像</summary>
        public Mat ResultImage { get; set; }

        /// <summary>处理耗时（毫秒）</summary>
        public double ElapsedMilliseconds { get; set; }

        /// <summary>是否成功完成</summary>
        public bool Success { get; set; }

        /// <summary>错误信息（失败时有效）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>各阶段执行结果</summary>
        public Dictionary<string, object> StageResults { get; set; }

        /// <summary>焊缝特征计算结果（成功时有效）</summary>
        public SeamFeatureResult SeamFeature { get; set; }

        /// <summary>当前机器人X坐标（用于与点云对齐）</summary>
        public double RobotX { get; set; }

        /// <summary>连续失败计数</summary>
        public int ConsecutiveFailureCount { get; set; }

        /// <summary>算法执行总次数</summary>
        public int TotalExecuteCount { get; set; }

        public AlgorithmCompletedEventArgs()
        {
            StageResults = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 焊缝特征计算结果
    /// </summary>
    public class SeamFeatureResult
    {
        /// <summary>计算得到的焊缝宽度（原始值，不做平滑）</summary>
        public double SeamWidth { get; set; }

        /// <summary>焊缝宽度原始值列表</summary>
        public List<double> RawSeamWidths { get; set; }

        /// <summary>焊缝面积</summary>
        public double SeamArea { get; set; }

        /// <summary>焊缝中心点X坐标</summary>
        public double CenterX { get; set; }

        /// <summary>焊缝中心点Y坐标</summary>
        public double CenterY { get; set; }

        /// <summary>计算时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>轮廓点列表（用于可视化）</summary>
        public List<System.Drawing.Point> ContourPoints { get; set; }

        public SeamFeatureResult()
        {
            RawSeamWidths = new List<double>();
            ContourPoints = new List<System.Drawing.Point>();
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 焊缝特征计算完成事件参数
    /// </summary>
    public class SeamFeatureCalculatedEventArgs : EventArgs
    {
        public double SeamWidth { get; }
        public List<double> RawSeamWidths { get; }
        public double SeamArea { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public DateTime Timestamp { get; }

        public SeamFeatureCalculatedEventArgs(double seamWidth, List<double> rawWidths, double seamArea, double centerX, double centerY)
        {
            SeamWidth = seamWidth;
            RawSeamWidths = rawWidths ?? new List<double>();
            SeamArea = seamArea;
            CenterX = centerX;
            CenterY = centerY;
            Timestamp = DateTime.Now;
        }
    }

    #endregion

    #region 算法管理器

    /// <summary>
    /// 算法流水线管理器：配置管理、算法调度、结果通知，不实现具体图像处理
    /// </summary>
    public sealed class PipelineManager
    {
        private static readonly Lazy<PipelineManager> _instance = new Lazy<PipelineManager>(() => new PipelineManager());
        public static PipelineManager Instance => _instance.Value;

        public PipelineConfig Config { get; private set; }

        /// <summary>由 JobManager 切换 Job 时调用，替换当前配置（深拷贝）。</summary>
        /// <param name="config">新配置</param>
        public void SetConfig(PipelineConfig config)
        {
            Config = config ?? new PipelineConfig();
        }

        /// <summary>算法流水线执行完成事件</summary>
        public event EventHandler<AlgorithmCompletedEventArgs> AlgorithmCompleted;

        /// <summary>焊缝特征计算完成事件</summary>
        public event EventHandler<SeamFeatureCalculatedEventArgs> SeamFeatureCalculated;

        /// <summary>焊缝宽度滑动平均计算器</summary>
        public SeamCalData SeamCalData { get; private set; }

        private readonly object _seamDataLock = new object();
        private AlgorithmCompletedEventArgs _completedArgs;

        private PipelineManager()
        {
            Config = new PipelineConfig();
            SeamCalData = new SeamCalData();
            _completedArgs = new AlgorithmCompletedEventArgs();
        }

        #region 配置持久化

        /// <summary>保存当前算法配置到 INI 文件。</summary>
        /// <param name="filePath">INI 文件路径</param>
        public void SaveToIni(string filePath)
        {
            var ini = new INIFile(filePath);
            SaveConfigToIni(ini, Config);
            ini.SaveToFile();
        }

        /// <summary>将 PipelineConfig 保存到 INI 对象（静态，供 JobManager 复用）。</summary>
        public static void SaveConfigToIni(INIFile ini, PipelineConfig config)
        {
            ini.WriteBool("Filter", "Enabled", config.Filter.Enabled);
            ini.WriteString("Filter", "Operation", config.Filter.Operation.ToString());
            foreach (var kv in config.Filter.Parameters)
                ini.WriteString("Filter", string.Format("Param_{0}", kv.Key), kv.Value);

            ini.WriteBool("Morphology", "Enabled", config.Morphology.Enabled);
            ini.WriteString("Morphology", "Operation", config.Morphology.Operation.ToString());
            foreach (var kv in config.Morphology.Parameters)
                ini.WriteString("Morphology", string.Format("Param_{0}", kv.Key), kv.Value);

            ini.WriteBool("EdgeDetection", "Enabled", config.EdgeDetection.Enabled);
            ini.WriteString("EdgeDetection", "Operation", config.EdgeDetection.Operation.ToString());
            foreach (var kv in config.EdgeDetection.Parameters)
                ini.WriteString("EdgeDetection", string.Format("Param_{0}", kv.Key), kv.Value);

            ini.WriteString("FeatureExtraction", "Operation", config.FeatureExtraction.Operation.ToString());
            foreach (var kv in config.FeatureExtraction.Parameters)
                ini.WriteString("FeatureExtraction", string.Format("Param_{0}", kv.Key), kv.Value);

            ini.WriteString("TemplateMatch", "Operation", config.TemplateMatch.Operation.ToString());
            foreach (var kv in config.TemplateMatch.Parameters)
                ini.WriteString("TemplateMatch", string.Format("Param_{0}", kv.Key), kv.Value);
        }

        /// <summary>从 INI 文件加载算法配置，不存在则创建默认配置。</summary>
        /// <param name="filePath">INI 文件路径</param>
        public void LoadFromIni(string filePath)
        {
            EnsureConfigFileExists(filePath);
            var ini = new INIFile(filePath);
            Config = LoadConfigFromIni(ini);
        }

        /// <summary>从 INI 对象加载 PipelineConfig（静态，供 JobManager 复用）。</summary>
        public static PipelineConfig LoadConfigFromIni(INIFile ini)
        {
            var newConfig = new PipelineConfig();

            if (ini.KeyExists("Filter", "Enabled"))
                newConfig.Filter.Enabled = ini.ReadBool("Filter", "Enabled", newConfig.Filter.Enabled);
            if (ini.KeyExists("Filter", "Operation"))
            {
                string opStr = ini.ReadString("Filter", "Operation", newConfig.Filter.Operation.ToString());
                if (Enum.TryParse<FilterOperation>(opStr, out var fop))
                    newConfig.Filter.Operation = fop;
            }
            LoadParametersStatic(ini, "Filter", newConfig.Filter.Parameters);

            if (ini.KeyExists("Morphology", "Enabled"))
                newConfig.Morphology.Enabled = ini.ReadBool("Morphology", "Enabled", newConfig.Morphology.Enabled);
            if (ini.KeyExists("Morphology", "Operation"))
            {
                string opStr = ini.ReadString("Morphology", "Operation", newConfig.Morphology.Operation.ToString());
                if (Enum.TryParse<MorphologyOperation>(opStr, out var mop))
                    newConfig.Morphology.Operation = mop;
            }
            LoadParametersStatic(ini, "Morphology", newConfig.Morphology.Parameters);

            if (ini.KeyExists("EdgeDetection", "Enabled"))
                newConfig.EdgeDetection.Enabled = ini.ReadBool("EdgeDetection", "Enabled", newConfig.EdgeDetection.Enabled);
            if (ini.KeyExists("EdgeDetection", "Operation"))
            {
                string opStr = ini.ReadString("EdgeDetection", "Operation", newConfig.EdgeDetection.Operation.ToString());
                if (Enum.TryParse<EdgeDetectionOperation>(opStr, out var eop))
                    newConfig.EdgeDetection.Operation = eop;
            }
            LoadParametersStatic(ini, "EdgeDetection", newConfig.EdgeDetection.Parameters);

            if (ini.KeyExists("FeatureExtraction", "Operation"))
            {
                string opStr = ini.ReadString("FeatureExtraction", "Operation", newConfig.FeatureExtraction.Operation.ToString());
                if (Enum.TryParse<FeatureExtractionOperation>(opStr, out var feop))
                    newConfig.FeatureExtraction.Operation = feop;
            }
            LoadParametersStatic(ini, "FeatureExtraction", newConfig.FeatureExtraction.Parameters);
            if (newConfig.FeatureExtraction.Parameters.TryGetValue("approximationMethod", out string approxMethod))
            {
                if (Enum.TryParse<ChainApproxMethod>(approxMethod, out ChainApproxMethod method))
                    newConfig.FeatureExtraction.ApproximationMethod = method;
            }

            if (ini.KeyExists("TemplateMatch", "Operation"))
            {
                string opStr = ini.ReadString("TemplateMatch", "Operation", newConfig.TemplateMatch.Operation.ToString());
                if (Enum.TryParse<TemplateMatchOperation>(opStr, out var top))
                    newConfig.TemplateMatch.Operation = top;
            }
            LoadParametersStatic(ini, "TemplateMatch", newConfig.TemplateMatch.Parameters);

            return newConfig;
        }

        private static void LoadParametersStatic(INIFile ini, string section, Dictionary<string, string> parameters)
        {
            var keys = ini.GetSectionKeys(section);
            foreach (var kv in keys)
            {
                if (kv.Key.StartsWith("Param_"))
                {
                    string paramKey = kv.Key.Substring(6);
                    parameters[paramKey] = kv.Value;
                }
            }
        }

        public bool EnsureConfigFileExists(string filePath)
        {
            if (File.Exists(filePath)) return true;
            try
            {
                SaveToIni(filePath);
                return true;
            }
            catch (Exception ex) { GlobalCommData.ShowLog("AlgorithmManager", string.Format("EnsureConfigFileExists失败 原因是 {0}", ex.Message), MessageLevel.Error); return false; }
        }

        #endregion

        #region 算法执行

        /// <summary>算法执行入口（Mat 输入），调度 ImageAlgorithm 算子并触发完成事件。</summary>
        /// <param name="src">输入图像</param>
        /// <param name="robotX">当前机器人 X 坐标，用于点云对齐</param>
        /// <returns>处理后图像</returns>
        public Mat Execute(Mat src, double robotX = 0)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            Mat current = src.Clone();
            if (current.IsEmpty) throw new ArgumentException("输入图像为空");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stageResults = new Dictionary<string, object>();
            bool success = true;
            string errorMessage = null;
            SeamFeatureResult seamFeature = null;

            try
            {
                if (Config.Filter.Enabled)
                {
                    current = ExecuteFilterStage(current);
                    if (current?.IsEmpty != false) throw new Exception("滤波阶段失败");
                    stageResults["Filter"] = "Success";
                }

                if (Config.Morphology.Enabled)
                {
                    current = ExecuteMorphologyStage(current);
                    if (current?.IsEmpty != false) throw new Exception("形态学阶段失败");
                    stageResults["Morphology"] = "Success";
                }

                if (Config.EdgeDetection.Enabled)
                {
                    current = ExecuteEdgeDetectionStage(current);
                    if (current?.IsEmpty != false) throw new Exception("边缘检测阶段失败");
                    stageResults["EdgeDetection"] = "Success";
                }

                // 特征提取阶段始终执行
                {
                    current = ExecuteFeatureExtractionStage(current);
                    if (current?.IsEmpty != false) throw new Exception("特征提取阶段失败");
                    stageResults["FeatureExtraction"] = "Success";

                    // 计算焊缝特征
                    seamFeature = CalculateSeamFeatures(current);
                    stageResults["SeamFeatureCalculated"] = seamFeature != null;

                    if (seamFeature == null)
                    {
                        throw new Exception("焊缝特征计算失败");
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
                stageResults["Error"] = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
            }

            // 触发算法完成事件（类级字段复用，仅赋值不new）
            _completedArgs.ResultImage = current;
            _completedArgs.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            _completedArgs.Success = success;
            _completedArgs.ErrorMessage = errorMessage;
            _completedArgs.StageResults = stageResults;
            _completedArgs.SeamFeature = seamFeature;
            _completedArgs.RobotX = robotX;
            AlgorithmCompleted?.Invoke(this, _completedArgs);

            return current;
        }

        /// <summary>外部算法结果上报（英莱 SDK 自有算法路线），复用 _completedArgs 触发完成事件。</summary>
        /// <param name="seamFeature">由调用方构造的焊缝特征，null 表示失败</param>
        /// <param name="robotX">当前机器人 X 坐标，与点云对齐</param>
        /// <param name="resultImage">用于显示的结果帧</param>
        public void ReportExternalResult(SeamFeatureResult seamFeature, double robotX, Mat resultImage)
        {
            if (_completedArgs.StageResults == null)
                _completedArgs.StageResults = new System.Collections.Generic.Dictionary<string, object>();
            else
                _completedArgs.StageResults.Clear();

            _completedArgs.ResultImage = resultImage;
            _completedArgs.ElapsedMilliseconds = 0;
            _completedArgs.Success = (seamFeature != null);
            _completedArgs.ErrorMessage = null;
            _completedArgs.StageResults["Source"] = "ExternalSDK";
            _completedArgs.SeamFeature = seamFeature;
            _completedArgs.RobotX = robotX;
            AlgorithmCompleted?.Invoke(this, _completedArgs);
        }

        /// <summary>算法执行入口（点云输入），转轮廓 Mat 后走通用流水线。</summary>
        /// <param name="pointCloud">输入点云</param>
        /// <param name="robotX">当前机器人 X 坐标</param>
        /// <returns>处理后图像</returns>
        public Mat Execute(PointCloudData pointCloud, double robotX = 0)
        {
            if (pointCloud == null)
                throw new ArgumentNullException(nameof(pointCloud));

            Mat contourImage = ImageAlgorithm.ConvertContourToMat(pointCloud);
            ImageAlgorithm.JointType jointType = ImageAlgorithm.MatchJointType(contourImage);
            Mat result = Execute(contourImage, robotX);
            _completedArgs.StageResults["JointType"] = jointType.ToString();

            return result;
        }

        #endregion

        #region 阶段执行（调用ImageAlgorithm）

        private Mat ExecuteFilterStage(Mat input)
        {
            var op = Config.Filter.Operation;
            var p = Config.Filter.Parameters;
            switch (op)
            {
                case FilterOperation.GaussianBlur:
                    return ImageAlgorithm.GaussianBlur(input, GetInt(p, "ksize", 5), GetDouble(p, "sigmaX", 1.5));
                case FilterOperation.MedianBlur:
                    return ImageAlgorithm.MedianBlur(input, GetInt(p, "ksize", 5));
                case FilterOperation.MeanBlur:
                    return ImageAlgorithm.MeanBlur(input, GetInt(p, "ksize", 5));
                case FilterOperation.BilateralFilter:
                    return ImageAlgorithm.BilateralFilter(input, GetInt(p, "d", 9), GetDouble(p, "sigmaColor", 75), GetDouble(p, "sigmaSpace", 75));
                case FilterOperation.Sharpen:
                    return ImageAlgorithm.Sharpen(input);
                default:
                    return input;
            }
        }

        private Mat ExecuteMorphologyStage(Mat input)
        {
            var op = Config.Morphology.Operation;
            var p = Config.Morphology.Parameters;
            switch (op)
            {
                case MorphologyOperation.Dilate:
                    return ImageAlgorithm.Dilate(input, GetInt(p, "ksize", 3));
                case MorphologyOperation.Erode:
                    return ImageAlgorithm.Erode(input, GetInt(p, "ksize", 3));
                case MorphologyOperation.Open:
                    return ImageAlgorithm.MorphologyOpen(input, GetInt(p, "ksize", 3));
                case MorphologyOperation.Close:
                    return ImageAlgorithm.MorphologyClose(input, GetInt(p, "ksize", 3));
                default:
                    return input;
            }
        }

        private Mat ExecuteEdgeDetectionStage(Mat input)
        {
            var op = Config.EdgeDetection.Operation;
            var p = Config.EdgeDetection.Parameters;
            switch (op)
            {
                case EdgeDetectionOperation.Canny:
                    return ImageAlgorithm.CannyEdge(input, GetDouble(p, "threshold1", 50), GetDouble(p, "threshold2", 150), GetInt(p, "apertureSize", 3));
                case EdgeDetectionOperation.Gradient:
                    return ImageAlgorithm.GradientEdge(input, GetInt(p, "ksize", 3));
                case EdgeDetectionOperation.Laplacian:
                    return ImageAlgorithm.LaplacianEdge(input, GetInt(p, "ksize", 3));
                case EdgeDetectionOperation.Roberts:
                    return ImageAlgorithm.RobertsEdge(input);
                default:
                    return input;
            }
        }

        private Mat ExecuteFeatureExtractionStage(Mat input)
        {
            var op = Config.FeatureExtraction.Operation;
            var p = Config.FeatureExtraction.Parameters;
            switch (op)
            {
                case FeatureExtractionOperation.ContourExtraction:
                    var contours = ImageAlgorithm.FindContours(input, GetDouble(p, "binaryThreshold", 127));
                    return ImageAlgorithm.DrawContours(input, contours, GetInt(p, "contourThickness", 2));
                case FeatureExtractionOperation.SkeletonExtraction:
                    return ImageAlgorithm.ExtractSkeleton(input, GetDouble(p, "binaryThreshold", 127), GetInt(p, "maxIter", 100));
                default:
                    return input;
            }
        }

        #endregion

        #region 焊缝特征计算

        /// <summary>从特征提取结果中计算焊缝特征（调用 ImageAlgorithm）。</summary>
        private SeamFeatureResult CalculateSeamFeatures(Mat featureImage)
        {
            if (featureImage == null || featureImage.IsEmpty)
                return null;

            try
            {
                var contours = ImageAlgorithm.FindContours(featureImage, 127);
                if (contours.Size == 0)
                    return null;

                int maxContourIdx = 0;
                double maxArea = 0;
                for (int i = 0; i < contours.Size; i++)
                {
                    double area = CvInvoke.ContourArea(contours[i]);
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxContourIdx = i;
                    }
                }

                VectorOfPoint maxContour = contours[maxContourIdx];

                double seamWidth = ImageAlgorithm.CalculateSeamWidth(maxContour);
                double seamArea = ImageAlgorithm.CalculateSeamArea(maxContour);
                PointF centerPoint = ImageAlgorithm.CalculateWeldPoint(maxContour);

                var contourPoints = new List<System.Drawing.Point>();
                for (int i = 0; i < maxContour.Size; i++)
                {
                    contourPoints.Add(maxContour[i]);
                }

                return new SeamFeatureResult
                {
                    SeamWidth = seamWidth,
                    SeamArea = seamArea,
                    CenterX = centerPoint.X,
                    CenterY = centerPoint.Y,
                    ContourPoints = contourPoints,
                    Timestamp = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 参数解析辅助

        private int GetInt(Dictionary<string, string> dict, string key, int defaultValue)
        {
            if (dict.TryGetValue(key, out string val))
            {
                if (int.TryParse(val, out int i)) return i;
            }
            return defaultValue;
        }

        private double GetDouble(Dictionary<string, string> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out string val))
            {
                if (double.TryParse(val, out double d)) return d;
            }
            return defaultValue;
        }

        #endregion
    }

    #endregion
}