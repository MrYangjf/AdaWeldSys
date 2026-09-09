using System.Collections.Generic;
using System.Globalization;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>滤波阶段配置（强类型，供 AlgorithmBase.Preprocess 直接使用）。
    /// 算子枚举统一引用 AdaWeldSystem.EmguALG 命名空间下的定义（ADR-020）。</summary>
    public class FilterConfig
    {
        public bool Enabled;
        public FilterOperation Operation;
        public int Ksize;
        public double SigmaX;

        public FilterConfig()
        {
            Enabled = true;
            Operation = FilterOperation.GaussianBlur;
            Ksize = 5;
            SigmaX = 1.5;
        }

        /// <summary>导出为参数网格字典（供 EmguAlgoPage 编辑）。</summary>
        public Dictionary<string, string> GetParameters()
        {
            var d = new Dictionary<string, string>();
            d["ksize"] = Ksize.ToString();
            d["sigmaX"] = SigmaX.ToString();
            return d;
        }

        /// <summary>从参数网格字典回写（供 EmguAlgoPage 编辑）。</summary>
        public void SetParameters(Dictionary<string, string> p)
        {
            if (p == null) return;
            string v;
            if (p.TryGetValue("ksize", out v))
            {
                int k; if (int.TryParse(v, out k)) Ksize = k;
            }
            if (p.TryGetValue("sigmaX", out v))
            {
                double s; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out s)) SigmaX = s;
            }
        }

        public FilterConfig Clone()
        {
            return new FilterConfig { Enabled = Enabled, Operation = Operation, Ksize = Ksize, SigmaX = SigmaX };
        }
    }

    /// <summary>形态学阶段配置。</summary>
    public class MorphologyConfig
    {
        public bool Enabled;
        public MorphologyOperation Operation;
        public int Ksize;

        public MorphologyConfig()
        {
            Enabled = false;
            Operation = MorphologyOperation.Open;
            Ksize = 3;
        }

        public Dictionary<string, string> GetParameters()
        {
            var d = new Dictionary<string, string>();
            d["ksize"] = Ksize.ToString();
            return d;
        }

        public void SetParameters(Dictionary<string, string> p)
        {
            if (p == null) return;
            string v;
            if (p.TryGetValue("ksize", out v))
            {
                int k; if (int.TryParse(v, out k)) Ksize = k;
            }
        }

        public MorphologyConfig Clone()
        {
            return new MorphologyConfig { Enabled = Enabled, Operation = Operation, Ksize = Ksize };
        }
    }

    /// <summary>边缘检测阶段配置。</summary>
    public class EdgeConfig
    {
        public bool Enabled;
        public EdgeDetectionOperation Operation;
        public double Threshold1;
        public double Threshold2;

        public EdgeConfig()
        {
            Enabled = false;
            Operation = EdgeDetectionOperation.Canny;
            Threshold1 = 50;
            Threshold2 = 150;
        }

        public Dictionary<string, string> GetParameters()
        {
            var d = new Dictionary<string, string>();
            d["threshold1"] = Threshold1.ToString();
            d["threshold2"] = Threshold2.ToString();
            return d;
        }

        public void SetParameters(Dictionary<string, string> p)
        {
            if (p == null) return;
            string v;
            if (p.TryGetValue("threshold1", out v))
            {
                double t; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out t)) Threshold1 = t;
            }
            if (p.TryGetValue("threshold2", out v))
            {
                double t; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out t)) Threshold2 = t;
            }
        }

        public EdgeConfig Clone()
        {
            return new EdgeConfig { Enabled = Enabled, Operation = Operation, Threshold1 = Threshold1, Threshold2 = Threshold2 };
        }
    }

    /// <summary>线激光专用预处理（离群点剔除 / Z 向抖动平滑 / 断线桥接）。</summary>
    public class LineLaserConfig
    {
        public bool OutlierRemoval;
        public bool ZJitterSmooth;
        public bool GapBridge;

        public LineLaserConfig()
        {
            OutlierRemoval = true;
            ZJitterSmooth = true;
            GapBridge = false;
        }

        public LineLaserConfig Clone()
        {
            return new LineLaserConfig
            {
                OutlierRemoval = OutlierRemoval,
                ZJitterSmooth = ZJitterSmooth,
                GapBridge = GapBridge
            };
        }
    }

    /// <summary>
    /// 预处理配置（算法架构设计 §4.2 / §6.3）。
    /// 在旧 PipelineConfig 基础上增加线激光专用预处理（离群点剔除、Z 向抖动平滑、断线桥接），仍复用 ImageAlgorithm。
    /// </summary>
    [System.ComponentModel.TypeConverter(typeof(System.ComponentModel.ExpandableObjectConverter))]
    public class PreprocessConfig
    {
        public FilterConfig Filter;
        public MorphologyConfig Morphology;
        public EdgeConfig Edge;
        public LineLaserConfig LineLaser;

        public PreprocessConfig()
        {
            Filter = new FilterConfig();
            Morphology = new MorphologyConfig();
            Edge = new EdgeConfig();
            LineLaser = new LineLaserConfig();
        }

        public PreprocessConfig Clone()
        {
            return new PreprocessConfig
            {
                Filter = Filter.Clone(),
                Morphology = Morphology.Clone(),
                Edge = Edge.Clone(),
                LineLaser = LineLaser.Clone()
            };
        }

        #region INI 契约（写入/读取 [Preprocess] 节，算法架构设计 §6.7）

        private const string Section = "Preprocess";

        public void SaveToIni(INIFile ini)
        {
            ini.WriteBool(Section, "Filter_Enabled", Filter.Enabled);
            ini.WriteInt(Section, "Filter_Ksize", Filter.Ksize);
            ini.WriteString(Section, "Filter_SigmaX", Filter.SigmaX.ToString(CultureInfo.InvariantCulture));
            ini.WriteBool(Section, "Morph_Enabled", Morphology.Enabled);
            ini.WriteInt(Section, "Morph_Ksize", Morphology.Ksize);
            ini.WriteBool(Section, "Edge_Enabled", Edge.Enabled);
            ini.WriteString(Section, "Edge_Threshold1", Edge.Threshold1.ToString(CultureInfo.InvariantCulture));
            ini.WriteString(Section, "Edge_Threshold2", Edge.Threshold2.ToString(CultureInfo.InvariantCulture));
            ini.WriteBool(Section, "LineLaser_Outlier", LineLaser.OutlierRemoval);
            ini.WriteBool(Section, "LineLaser_ZJitter", LineLaser.ZJitterSmooth);
            ini.WriteBool(Section, "LineLaser_GapBridge", LineLaser.GapBridge);
        }

        public void LoadFromIni(INIFile ini)
        {
            Filter.Enabled = ini.ReadBool(Section, "Filter_Enabled", Filter.Enabled);
            Filter.Ksize = ini.ReadInt(Section, "Filter_Ksize", Filter.Ksize);
            string sx = ini.ReadString(Section, "Filter_SigmaX", null);
            if (!string.IsNullOrEmpty(sx)) { double v; if (double.TryParse(sx, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Filter.SigmaX = v; }
            Morphology.Enabled = ini.ReadBool(Section, "Morph_Enabled", Morphology.Enabled);
            Morphology.Ksize = ini.ReadInt(Section, "Morph_Ksize", Morphology.Ksize);
            Edge.Enabled = ini.ReadBool(Section, "Edge_Enabled", Edge.Enabled);
            string t1 = ini.ReadString(Section, "Edge_Threshold1", null);
            if (!string.IsNullOrEmpty(t1)) { double v; if (double.TryParse(t1, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Edge.Threshold1 = v; }
            string t2 = ini.ReadString(Section, "Edge_Threshold2", null);
            if (!string.IsNullOrEmpty(t2)) { double v; if (double.TryParse(t2, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Edge.Threshold2 = v; }
            LineLaser.OutlierRemoval = ini.ReadBool(Section, "LineLaser_Outlier", LineLaser.OutlierRemoval);
            LineLaser.ZJitterSmooth = ini.ReadBool(Section, "LineLaser_ZJitter", LineLaser.ZJitterSmooth);
            LineLaser.GapBridge = ini.ReadBool(Section, "LineLaser_GapBridge", LineLaser.GapBridge);
        }

        #endregion
    }
}
