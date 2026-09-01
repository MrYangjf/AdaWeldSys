using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using AdaWeldSystem.EmguALG;                 // ImageAlgorithm（算子层静态库）
using AdaWeldSystem.EmguALG.Core;            // AlgorithmBase / AlgorithmResult
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.PCLOperate.Models;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor
{
    /// <summary>对中/质量分级（Pass / Warn / Fail）。</summary>
    public enum CheckGrade
    {
        Pass,
        Warn,
        Fail
    }

    /// <summary>光斑检测参数（Blob/阈值）。</summary>
    public class BlobConfig
    {
        public int Threshold { get; set; }   // 亮度阈值
        public double MinArea { get; set; }  // 最小面积（px²）
        public double MaxArea { get; set; }  // 最大面积（px²）

        public BlobConfig()
        {
            Threshold = 180;
            MinArea = 10;
            MaxArea = 100000;
        }

        public BlobConfig Clone()
        {
            BlobConfig c = new BlobConfig();
            c.Threshold = Threshold;
            c.MinArea = MinArea;
            c.MaxArea = MaxArea;
            return c;
        }
    }

    /// <summary>焊丝检测参数（边缘 + 线拟合）。</summary>
    public class LineFitConfig
    {
        public int Canny1 { get; set; }
        public int Canny2 { get; set; }
        public double MinLineLength { get; set; }
        public double MaxGap { get; set; }

        public LineFitConfig()
        {
            Canny1 = 50;
            Canny2 = 150;
            MinLineLength = 50;
            MaxGap = 10;
        }

        public LineFitConfig Clone()
        {
            LineFitConfig c = new LineFitConfig();
            c.Canny1 = Canny1;
            c.Canny2 = Canny2;
            c.MinLineLength = MinLineLength;
            c.MaxGap = MaxGap;
            return c;
        }
    }

    /// <summary>
    /// 实时监控参数（全局固定，无 Job，算法架构设计 §7.4）。
    /// 单例常驻，配置持久化到 Config/Monitor/Monitor.ini。
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class MonitorConfig
    {
        [Category("光斑检测")] public BlobConfig Spot { get; set; }
        [Category("焊丝检测")] public LineFitConfig Wire { get; set; }

        [Category("对中容差(mm)"), Unit("mm")] public double CenterWarnMM { get; set; }
        [Category("对中容差(mm)"), Unit("mm")] public double CenterFailMM { get; set; }

        [Category("成形质量(mm)"), Unit("mm")] public double BeadWidthMinMM { get; set; }
        [Category("成形质量(mm)"), Unit("mm")] public double BeadWidthMaxMM { get; set; }
        [Category("成形质量")] public int PorosityMaxCount { get; set; }
        [Category("成形质量(mm)"), Unit("mm")] public double UndercutMaxMM { get; set; }
        [Category("成形质量")] public double SpatterMaxArea { get; set; }

        /// <summary>像素→mm 标定系数（未标定时使用；标定后可由相机内参替代）。</summary>
        [Category("标定")] public double PixelMmScale { get; set; }

        public MonitorConfig()
        {
            Spot = new BlobConfig();
            Wire = new LineFitConfig();
            CenterWarnMM = 0.3;
            CenterFailMM = 0.8;
            BeadWidthMinMM = 3.0;
            BeadWidthMaxMM = 6.0;
            PorosityMaxCount = 3;
            UndercutMaxMM = 0.2;
            SpatterMaxArea = 500;
            PixelMmScale = 0.05;
        }

        public MonitorConfig Clone()
        {
            MonitorConfig c = new MonitorConfig();
            c.Spot = Spot.Clone();
            c.Wire = Wire.Clone();
            c.CenterWarnMM = CenterWarnMM;
            c.CenterFailMM = CenterFailMM;
            c.BeadWidthMinMM = BeadWidthMinMM;
            c.BeadWidthMaxMM = BeadWidthMaxMM;
            c.PorosityMaxCount = PorosityMaxCount;
            c.UndercutMaxMM = UndercutMaxMM;
            c.SpatterMaxArea = SpatterMaxArea;
            c.PixelMmScale = PixelMmScale;
            return c;
        }

        public void SaveToIni(INIFile ini)
        {
            ini.WriteInt("Centering", "Spot_Threshold", Spot.Threshold);
            ini.WriteInt("Centering", "Wire_Canny1", Wire.Canny1);
            ini.WriteInt("Centering", "Wire_Canny2", Wire.Canny2);
            ini.WriteString("Centering", "CenterWarnMM", CenterWarnMM.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("Centering", "CenterFailMM", CenterFailMM.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("Centering", "PixelMmScale", PixelMmScale.ToString(CultureInfo.InvariantCulture));

            ini.WriteString("Quality", "BeadWidthMinMM", BeadWidthMinMM.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("Quality", "BeadWidthMaxMM", BeadWidthMaxMM.ToString(CultureInfo.InvariantCulture));
            ini.WriteInt("Quality", "PorosityMaxCount", PorosityMaxCount);
            ini.WriteString("Quality", "UndercutMaxMM", UndercutMaxMM.ToString(CultureInfo.InvariantCulture));
            ini.WriteString("Quality", "SpatterMaxArea", SpatterMaxArea.ToString(CultureInfo.InvariantCulture));
        }

        public void LoadFromIni(INIFile ini)
        {
            Spot.Threshold = ini.ReadInt("Centering", "Spot_Threshold", Spot.Threshold);
            Wire.Canny1 = ini.ReadInt("Centering", "Wire_Canny1", Wire.Canny1);
            Wire.Canny2 = ini.ReadInt("Centering", "Wire_Canny2", Wire.Canny2);
            string cw = ini.ReadString("Centering", "CenterWarnMM", null);
            if (!string.IsNullOrEmpty(cw)) { double v; if (double.TryParse(cw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) CenterWarnMM = v; }
            string cf = ini.ReadString("Centering", "CenterFailMM", null);
            if (!string.IsNullOrEmpty(cf)) { double v; if (double.TryParse(cf, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) CenterFailMM = v; }
            string ps = ini.ReadString("Centering", "PixelMmScale", null);
            if (!string.IsNullOrEmpty(ps)) { double v; if (double.TryParse(ps, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) PixelMmScale = v; }

            string bwMin = ini.ReadString("Quality", "BeadWidthMinMM", null);
            if (!string.IsNullOrEmpty(bwMin)) { double v; if (double.TryParse(bwMin, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) BeadWidthMinMM = v; }
            string bwMax = ini.ReadString("Quality", "BeadWidthMaxMM", null);
            if (!string.IsNullOrEmpty(bwMax)) { double v; if (double.TryParse(bwMax, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) BeadWidthMaxMM = v; }
            PorosityMaxCount = ini.ReadInt("Quality", "PorosityMaxCount", PorosityMaxCount);
            string uc = ini.ReadString("Quality", "UndercutMaxMM", null);
            if (!string.IsNullOrEmpty(uc)) { double v; if (double.TryParse(uc, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) UndercutMaxMM = v; }
            string sa = ini.ReadString("Quality", "SpatterMaxArea", null);
            if (!string.IsNullOrEmpty(sa)) { double v; if (double.TryParse(sa, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) SpatterMaxArea = v; }
        }
    }

    /// <summary>焊前对中检查结果（算法架构设计 §7.2）。</summary>
    public class CenteringCheckResult : AlgorithmResult
    {
        public double DevXmm;   // 水平方向偏差(mm)
        public double DevZmm;   // 高度方向偏差(mm)
        public string Direction; // "左/右/上/下/居中"
        public CheckGrade Grade;
        public PointF SpotCenterPx, WireTipPx;

        public CenteringCheckResult()
        {
            DevXmm = 0;
            DevZmm = 0;
            Direction = "居中";
            Grade = CheckGrade.Pass;
            SpotCenterPx = PointF.Empty;
            WireTipPx = PointF.Empty;
        }
    }

    /// <summary>焊缝成形质量结果（算法架构设计 §7.3）。</summary>
    public class SeamQualityResult : AlgorithmResult
    {
        public int Score;               // 0~100
        public CheckGrade Grade;
        public List<string> Defects;    // 缺陷描述
        public double BeadWidthMM;
        public int PorosityCount;
        public double UndercutMM;
        public double SpatterArea;

        public SeamQualityResult()
        {
            Score = 0;
            Grade = CheckGrade.Fail;
            Defects = new List<string>();
            BeadWidthMM = 0;
            PorosityCount = 0;
            UndercutMM = 0;
            SpatterArea = 0;
        }
    }

    /// <summary>
    /// 实时监控领域算法（无 Job，全局单例，算法架构设计 §7）。
    /// 子任务：焊前对中检查（CheckCentering）+ 焊缝成形质量（CheckQuality）。
    /// </summary>
    public sealed class RealtimeMonitorAlgorithm : AlgorithmBase
    {
        private static readonly RealtimeMonitorAlgorithm _instance = new RealtimeMonitorAlgorithm();
        public static RealtimeMonitorAlgorithm Instance { get { return _instance; } }

        private MonitorConfig _config;
        public MonitorConfig Config { get { return _config; } }

        private RealtimeMonitorAlgorithm()
        {
            _config = new MonitorConfig();
        }

        public void SetConfig(MonitorConfig cfg)
        {
            _config = cfg ?? new MonitorConfig();
        }

        public override string AlgorithmName { get { return "RealtimeMonitor"; } }

        public override void SaveToIni(INIFile ini) { _config.SaveToIni(ini); }
        public override void LoadFromIni(INIFile ini) { _config = new MonitorConfig(); _config.LoadFromIni(ini); }

        /// <summary>基类模板默认走对中检查（保证 AlgorithmBase.Execute(Mat) 可用）。</summary>
        protected override AlgorithmResult ExecuteCore(Mat input)
        {
            return CheckCentering(input);
        }

        #region 子任务一：焊前对中检查（光斑 + 焊丝）

        public CenteringCheckResult CheckCentering(Mat image)
        {
            CenteringCheckResult r = new CenteringCheckResult();
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                Mat gray = PrepareGray(image);
                Mat spotMask;
                PointF spot = DetectCentroid(gray, _config.Spot.Threshold, out spotMask);
                PointF wire = DetectWireTip(gray, _config.Wire.Canny1, _config.Wire.Canny2);
                r.SpotCenterPx = spot;
                r.WireTipPx = wire;

                if (spot == PointF.Empty || wire == PointF.Empty)
                {
                    r.Success = false;
                    r.ErrorMessage = "未检测到光斑或焊丝";
                    r.Grade = CheckGrade.Fail;
                }
                else
                {
                    double devXpx = wire.X - spot.X;
                    double devYpx = wire.Y - spot.Y;
                    r.DevXmm = devXpx * _config.PixelMmScale; // 水平方向(沿焊缝投影) → 左/右
                    r.DevZmm = devYpx * _config.PixelMmScale; // 垂直方向(高度) → 上/下
                    double dev = Math.Sqrt(r.DevXmm * r.DevXmm + r.DevZmm * r.DevZmm);
                    r.Direction = ComputeDirection(r.DevXmm, r.DevZmm);
                    r.Grade = GradeFromDeviation(dev, _config.CenterWarnMM, _config.CenterFailMM);
                    r.Success = true;
                }
            }
            catch (Exception ex)
            {
                r.Success = false;
                r.ErrorMessage = ex.Message;
                r.Grade = CheckGrade.Fail;
            }
            finally { sw.Stop(); }
            r.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            r.Algorithm = AlgorithmName;
            return r;
        }

        #endregion

        #region 子任务二：焊缝成形质量（参考 Weldeye）

        public SeamQualityResult CheckQuality(Mat image)
        {
            SeamQualityResult r = new SeamQualityResult();
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                Mat gray = PrepareGray(image);
                Mat bead = new Mat();
                CvInvoke.Threshold(gray, bead, 128, 255, ThresholdType.Binary);
                Moments m = CvInvoke.Moments(bead);
                r.BeadWidthMM = (m.M00 > 1e-6) ? Math.Sqrt(m.M00) * _config.PixelMmScale : 0;
                r.PorosityCount = CountDarkBlobs(gray);
                r.UndercutMM = 0;          // 占位：真实应由截面轮廓拟合
                r.SpatterArea = 0;         // 占位：真实应由飞溅掩膜面积统计

                int score = 100;
                if (r.BeadWidthMM < _config.BeadWidthMinMM || r.BeadWidthMM > _config.BeadWidthMaxMM) score -= 40;
                if (r.PorosityCount > _config.PorosityMaxCount) score -= 30;
                if (r.UndercutMM > _config.UndercutMaxMM) score -= 15;
                if (r.SpatterArea > _config.SpatterMaxArea) score -= 15;
                score = Math.Max(0, Math.Min(100, score));
                r.Score = score;
                r.Grade = (score >= 80) ? CheckGrade.Pass : (score >= 50) ? CheckGrade.Warn : CheckGrade.Fail;
                if (score < 80) r.Defects = BuildDefects(r);
                r.Success = true;
            }
            catch (Exception ex)
            {
                r.Success = false;
                r.ErrorMessage = ex.Message;
                r.Grade = CheckGrade.Fail;
            }
            finally { sw.Stop(); }
            r.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            r.Algorithm = AlgorithmName;
            return r;
        }

        #endregion

        #region 图像处理辅助

        private Mat PrepareGray(Mat image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.NumberOfChannels == 3)
            {
                Mat gray = new Mat();
                CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);
                return gray;
            }
            return image;
        }

        private PointF DetectCentroid(Mat gray, int threshold, out Mat mask)
        {
            mask = new Mat();
            CvInvoke.Threshold(gray, mask, threshold, 255, ThresholdType.Binary);
            Moments moments = CvInvoke.Moments(mask);
            double m00 = moments.M00;
            if (m00 < 1e-6) return PointF.Empty;
            return new PointF((float)(moments.M10 / m00), (float)(moments.M01 / m00));
        }

        private PointF DetectWireTip(Mat gray, int c1, int c2)
        {
            Mat edges = new Mat();
            CvInvoke.Canny(gray, edges, c1, c2);
            Image<Gray, byte> e2 = edges.ToImage<Gray, byte>();
            int minRow = int.MaxValue;
            float tx = 0, ty = 0;
            for (int row = 0; row < e2.Rows; row++)
            {
                for (int col = 0; col < e2.Cols; col++)
                {
                    if (e2.Data[row, col, 0] > 0 && row < minRow)
                    {
                        minRow = row; tx = col; ty = row;
                    }
                }
            }
            return (minRow == int.MaxValue) ? PointF.Empty : new PointF(tx, ty);
        }

        private int CountDarkBlobs(Mat gray)
        {
            Mat inv = new Mat();
            CvInvoke.Threshold(gray, inv, 100, 255, ThresholdType.BinaryInv);
            VectorOfVectorOfPoint contours = ImageAlgorithm.FindContours(inv, 128);
            int count = 0;
            for (int i = 0; i < contours.Size; i++)
            {
                if (CvInvoke.ContourArea(contours[i]) < 50) count++;
            }
            return count;
        }

        private string ComputeDirection(double dxMm, double dzMm)
        {
            bool horiz = Math.Abs(dxMm) >= Math.Abs(dzMm);
            if (Math.Abs(dxMm) < 1e-6 && Math.Abs(dzMm) < 1e-6) return "居中";
            if (horiz) return dxMm > 0 ? "右" : "左";
            return dzMm > 0 ? "下" : "上";
        }

        private CheckGrade GradeFromDeviation(double dev, double warn, double fail)
        {
            if (dev <= warn) return CheckGrade.Pass;
            if (dev <= fail) return CheckGrade.Warn;
            return CheckGrade.Fail;
        }

        private List<string> BuildDefects(SeamQualityResult r)
        {
            List<string> d = new List<string>();
            if (r.BeadWidthMM < _config.BeadWidthMinMM || r.BeadWidthMM > _config.BeadWidthMaxMM) d.Add("焊道宽度超差");
            if (r.PorosityCount > _config.PorosityMaxCount) d.Add("气孔过多");
            if (r.UndercutMM > _config.UndercutMaxMM) d.Add("咬边超差");
            if (r.SpatterArea > _config.SpatterMaxArea) d.Add("飞溅过多");
            return d;
        }

        #endregion
    }
}
