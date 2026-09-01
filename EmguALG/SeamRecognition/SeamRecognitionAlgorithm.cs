using System;
using System.ComponentModel;
using System.Globalization;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using AdaWeldSystem.EmguALG;                 // ImageAlgorithm（算子层静态库）
using AdaWeldSystem.EmguALG.Core;            // AlgorithmBase / ProfileTransform / PreprocessConfig / AlgorithmResult / UnitAttribute
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.PCLOperate.Models;
using System.Drawing;

namespace AdaWeldSystem.EmguALG.SeamRecognition
{
    /// <summary>
    /// 焊缝匹配参数（算法架构设计 §6.3 / §6.7 [Match]）。
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class SeamMatchConfig
    {
        [Category("匹配"), Description("匹配置信度阈值 0~1")] public double ScoreThreshold { get; set; }
        [Category("匹配"), Description("参考几何搜索步长(mm)")] public double EdgeSearchStepMM { get; set; }

        public SeamMatchConfig()
        {
            ScoreThreshold = 0.80;
            EdgeSearchStepMM = 0.5;
        }

        public SeamMatchConfig Clone()
        {
            SeamMatchConfig c = new SeamMatchConfig();
            c.ScoreThreshold = ScoreThreshold;
            c.EdgeSearchStepMM = EdgeSearchStepMM;
            return c;
        }

        public void SaveToIni(INIFile ini)
        {
            ini.WriteString("Match", "ScoreThreshold", ScoreThreshold.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("Match", "EdgeSearchStepMM", EdgeSearchStepMM.ToString(CultureInfo.InvariantCulture));
        }

        public void LoadFromIni(INIFile ini)
        {
            string s = ini.ReadString("Match", "ScoreThreshold", null);
            if (!string.IsNullOrEmpty(s)) { double v; if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) ScoreThreshold = v; }
            string e = ini.ReadString("Match", "EdgeSearchStepMM", null);
            if (!string.IsNullOrEmpty(e)) { double v; if (double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) EdgeSearchStepMM = v; }
        }
    }

    /// <summary>
    /// 焊缝识别参数（Job 管理，算法架构设计 §6.3）。
    /// 强类型、自带 mm 语义，并自行承担 INI 契约（SaveToIni 实例方法 + LoadFromIni 静态工厂）。
    /// 彻底替代旧 PipelineConfig（§6.4）。
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class SeamRecognitionConfig
    {
        [Category("接头"), Description("固定焊接接头形式")] public SeamJointType JointType { get; set; }
        [Category("预处理")] public PreprocessConfig Preprocess { get; set; }
        [Category("匹配")] public SeamMatchConfig Match { get; set; }

        [Category("边缝(mm)"), Unit("mm")] public double EdgeSearchWindowMM { get; set; }
        [Category("指引点(mm)"), Unit("mm")] public ProfilePointMM GuideOffsetMM { get; set; }
        [Category("容差(mm)"), Unit("mm")] public double MaxGuideDeviationMM { get; set; }

        public SeamRecognitionConfig()
        {
            JointType = SeamJointType.Straight;
            Preprocess = new PreprocessConfig();
            Match = new SeamMatchConfig();
            EdgeSearchWindowMM = 2.0;
            GuideOffsetMM = new ProfilePointMM();
            MaxGuideDeviationMM = 1.5;
        }

        public SeamRecognitionConfig Clone()
        {
            SeamRecognitionConfig c = new SeamRecognitionConfig();
            c.JointType = JointType;
            c.Preprocess = Preprocess.Clone();
            c.Match = Match.Clone();
            c.EdgeSearchWindowMM = EdgeSearchWindowMM;
            c.GuideOffsetMM = new ProfilePointMM(GuideOffsetMM.Y, GuideOffsetMM.Z);
            c.MaxGuideDeviationMM = MaxGuideDeviationMM;
            return c;
        }

        public void SaveToIni(INIFile ini)
        {
            ini.WriteString("Seam", "JointType", JointType.ToString());
            Preprocess.SaveToIni(ini);
            Match.SaveToIni(ini);
            ini.WriteString("SeamEdges", "EdgeSearchWindowMM", EdgeSearchWindowMM.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("GuidePoint", "OffsetY_mm", GuideOffsetMM.Y.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("GuidePoint", "OffsetZ_mm", GuideOffsetMM.Z.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("Tolerance", "MaxGuideDeviationMM", MaxGuideDeviationMM.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>静态工厂：从 INI 读取并构造新的配置（算法架构设计 §6.4 / §9）。</summary>
        public static SeamRecognitionConfig LoadFromIni(INIFile ini)
        {
            SeamRecognitionConfig c = new SeamRecognitionConfig();
            string jt = ini.ReadString("Seam", "JointType", null);
            if (!string.IsNullOrEmpty(jt)) { SeamJointType parsed; if (Enum.TryParse<SeamJointType>(jt, out parsed)) c.JointType = parsed; }
            c.Preprocess.LoadFromIni(ini);
            c.Match.LoadFromIni(ini);
            string ew = ini.ReadString("SeamEdges", "EdgeSearchWindowMM", null);
            if (!string.IsNullOrEmpty(ew)) { double v; if (double.TryParse(ew, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) c.EdgeSearchWindowMM = v; }
            string oy = ini.ReadString("GuidePoint", "OffsetY_mm", null);
            string oz = ini.ReadString("GuidePoint", "OffsetZ_mm", null);
            double y = c.GuideOffsetMM.Y, z = c.GuideOffsetMM.Z;
            if (!string.IsNullOrEmpty(oy)) { double v; if (double.TryParse(oy, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) y = v; }
            if (!string.IsNullOrEmpty(oz)) { double v; if (double.TryParse(oz, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) z = v; }
            c.GuideOffsetMM = new ProfilePointMM(y, z);
            string md = ini.ReadString("Tolerance", "MaxGuideDeviationMM", null);
            if (!string.IsNullOrEmpty(md)) { double v; if (double.TryParse(md, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) c.MaxGuideDeviationMM = v; }
            return c;
        }
    }

    /// <summary>
    /// 焊缝识别结果（算法架构设计 §6.6）。几何量均为 mm，Visualization 仅用于可视化(px)。
    /// </summary>
    public class SeamRecognitionResult : AlgorithmResult
    {
        public SeamJointType JointType;
        public SeamEdgeMM LeftEdgeMM, RightEdgeMM;
        public ProfilePointMM NominalGuideMM;
        public ProfilePointMM FinalGuideMM;
        public double GuideDeviationMM;
        public Mat Visualization;

        public SeamRecognitionResult()
        {
            JointType = SeamJointType.Straight;
            LeftEdgeMM = new SeamEdgeMM();
            RightEdgeMM = new SeamEdgeMM();
            NominalGuideMM = new ProfilePointMM();
            FinalGuideMM = new ProfilePointMM();
            GuideDeviationMM = 0;
            Visualization = null;
        }
    }

    /// <summary>
    /// 焊缝识别领域算法（Job 管理，算法架构设计 §6）。
    /// 流程：点云(mm) → BuildTransform → 栅格化 → Preprocess → 匹配(SeamMatchResult) → 建模(SeamModel) → 产出 SeamRecognitionResult。
    /// 单例常驻，运行时通过 JobManager.SwitchJob → SetConfig 替换当前参数组。
    /// </summary>
    public sealed class SeamRecognitionAlgorithm : AlgorithmBase
    {
        private static readonly SeamRecognitionAlgorithm _instance = new SeamRecognitionAlgorithm();
        public static SeamRecognitionAlgorithm Instance { get { return _instance; } }

        private SeamRecognitionConfig _config;

        /// <summary>当前生效的焊缝识别参数组（强类型，自带 mm 语义）。</summary>
        public SeamRecognitionConfig Config { get { return _config; } }

        // 当前帧上下文：Execute(PointCloudData) 设置，ExecuteCore(Mat) 内使用（单例逐帧处理）。
        private ProfileTransform _transform;
        private PointCloudData _pc;

        private SeamRecognitionAlgorithm()
        {
            _config = new SeamRecognitionConfig();
        }

        /// <summary>运行时切换 Job → 替换当前参数组（替代旧 PipelineManager.Instance.SetConfig）。</summary>
        public void SetConfig(SeamRecognitionConfig cfg)
        {
            _config = cfg ?? new SeamRecognitionConfig();
        }

        public override string AlgorithmName { get { return "SeamRecognition"; } }

        public override void SaveToIni(INIFile ini) { _config.SaveToIni(ini); }
        public override void LoadFromIni(INIFile ini) { _config = SeamRecognitionConfig.LoadFromIni(ini); }

        /// <summary>
        /// 焊缝识别主入口：点云(mm) → 构建 mm↔px 变换 → 栅格化 → 基类 Execute(Mat) 模板（预处理 → 匹配 → 建模 → 结果）。
        /// </summary>
        public SeamRecognitionResult Execute(PointCloudData pc, int imgW = 640, int imgH = 480)
        {
            if (pc == null || pc.PointCount == 0)
            {
                SeamRecognitionResult fail = new SeamRecognitionResult();
                fail.Success = false;
                fail.ErrorMessage = "点云为空";
                return fail;
            }
            _pc = pc;
            _transform = BuildTransform(pc, imgW, imgH);
            Mat contour = ImageAlgorithm.ConvertContourToMat(pc, _transform);
            AlgorithmResult baseResult = Execute(contour); // 基类模板方法 → ExecuteCore
            SeamRecognitionResult typed = baseResult as SeamRecognitionResult;
            if (typed == null)
            {
                typed = new SeamRecognitionResult();
                typed.Success = false;
                typed.ErrorMessage = (baseResult == null) ? "识别失败" : baseResult.ErrorMessage;
                typed.ElapsedMs = (baseResult == null) ? 0 : baseResult.ElapsedMs;
                typed.Algorithm = (baseResult == null) ? AlgorithmName : baseResult.Algorithm;
            }
            return typed;
        }

        protected override AlgorithmResult ExecuteCore(Mat input)
        {
            SeamRecognitionResult result = new SeamRecognitionResult();
            Mat preprocessed = Preprocess(input, _config.Preprocess);
            VectorOfVectorOfPoint contours = FindContours(preprocessed);
            SeamMatchResult match = MatchSeam(contours);
            SeamModel model = BuildModel(match);
            result.JointType = match.JointType;
            result.LeftEdgeMM = match.LeftEdge;
            result.RightEdgeMM = match.RightEdge;
            result.NominalGuideMM = model.NominalGuide;
            result.FinalGuideMM = model.FinalGuide;
            result.GuideDeviationMM = model.NominalGuide.DistanceTo(model.FinalGuide);
            result.Visualization = DrawResult(input, contours, model);
            result.Success = true;
            return result;
        }

        #region 子步骤（业务判定，纯 mm 语义 + 仅在绘制时转 px）

        private SeamMatchResult MatchSeam(VectorOfVectorOfPoint contours)
        {
            SeamMatchResult m = new SeamMatchResult();
            m.JointType = _config.JointType;
            m.Score = _config.Match.ScoreThreshold; // 占位：真实匹配应基于模板/几何置信度，并与阈值比较
            if (contours != null && contours.Size > 0)
            {
                int best = 0; double bestArea = -1;
                for (int i = 0; i < contours.Size; i++)
                {
                    double area = CvInvoke.ContourArea(contours[i]);
                    if (area > bestArea) { bestArea = area; best = i; }
                }
                VectorOfPoint c = contours[best];
                int minYi = 0, maxYi = 0;
                double minY = double.MaxValue, maxY = double.MinValue;
                for (int i = 0; i < c.Size; i++)
                {
                    System.Drawing.Point p = c[i];
                    double yMm, zMm; _transform.ToMM(p.X, p.Y, out yMm, out zMm);
                    if (yMm < minY) { minY = yMm; minYi = i; }
                    if (yMm > maxY) { maxY = yMm; maxYi = i; }
                }
                ProfilePointMM a = ToMm(c[minYi]);
                ProfilePointMM b = ToMm(c[maxYi]);
                m.LeftEdge = new SeamEdgeMM(a, b);
                m.RightEdge = new SeamEdgeMM(b, a); // 占位：真实应来自第二块板边缘
            }
            return m;
        }

        private SeamModel BuildModel(SeamMatchResult match)
        {
            SeamModel model = new SeamModel(match.JointType, match.LeftEdge, match.RightEdge);
            model.OffsetMM = _config.GuideOffsetMM;
            return model;
        }

        private Mat DrawResult(Mat input, VectorOfVectorOfPoint contours, SeamModel model)
        {
            Mat vis = (input == null) ? new Mat() : input.Clone();
            if (contours != null && contours.Size > 0)
                CvInvoke.DrawContours(vis, contours, -1, new MCvScalar(0, 255, 0), 2);
            if (_transform != null)
            {
                System.Drawing.Point g = _transform.ToPixel(model.FinalGuide.Y, model.FinalGuide.Z);
                CvInvoke.Circle(vis, g, 5, new MCvScalar(0, 0, 255), -1);
            }
            return vis;
        }

        private ProfilePointMM ToMm(System.Drawing.Point p)
        {
            double yMm, zMm; _transform.ToMM(p.X, p.Y, out yMm, out zMm);
            return new ProfilePointMM(yMm, zMm);
        }

        #endregion
    }
}
