using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ConfigFileControl.Implementations;

namespace AdaWeldSystem.WeldParamControl.Curves
{
    /// <summary>曲线插值方式</summary>
    public enum CurveInterpolation
    {
        /// <summary>分段线性（默认）</summary>
        PiecewiseLinear = 0,

        /// <summary>取最近采样点</summary>
        Nearest = 1
    }

    /// <summary>曲线外推方式</summary>
    public enum CurveExtrapolation
    {
        /// <summary>钳位到端点（默认）</summary>
        Clamp = 0,

        /// <summary>拒绝域外输入</summary>
        Reject = 1
    }

    /// <summary>曲线采样点（X = LA 或 TA 位置，Y = 光斑大小或 LA 偏移补偿）</summary>
    public class CurvePoint
    {
        /// <summary>自变量（毫米）：曲线A 为 LA 位置，曲线B 为 TA 位置</summary>
        public double X { get; set; }

        /// <summary>因变量（毫米）：曲线A 为光斑大小，曲线B 为 LA 偏移补偿</summary>
        public double Y { get; set; }

        /// <summary>创建空采样点</summary>
        public CurvePoint()
        {
            X = 0.0;
            Y = 0.0;
        }

        /// <summary>创建采样点</summary>
        /// <param name="x">自变量（毫米）</param>
        /// <param name="y">因变量（毫米）</param>
        public CurvePoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>TA/LA 标定曲线（采样点表 + 分段线性插值）。</summary>
    public class TaLaCurveConfig
    {
        /// <summary>曲线配置所属模块（全局配置目录下的模块层）。</summary>
        private const string ConfigModule = "WeldParam";

        /// <summary>配置文件名</summary>
        private const string CurveFileName = "TaLaCurve.xml";

        /// <summary>曲线A 元素名（LA 位置 → 光斑大小）</summary>
        public const string CurveAKey = "CURVE_A_POINTS";

        /// <summary>曲线B 元素名（TA 位置 → LA 偏移）</summary>
        public const string CurveBKey = "CURVE_B_POINTS";

        /// <summary>内置默认锚点：最聚焦光斑最小值（毫米）。</summary>
        public const double DefaultAnchorSpotMm = 0.6;

        #region 公共变量

        /// <summary>曲线A 采样点（LA 位置 → 光斑大小）</summary>
        public List<CurvePoint> PointsA { get; private set; }

        /// <summary>曲线B 采样点（TA 位置 → LA 偏移补偿）</summary>
        public List<CurvePoint> PointsB { get; private set; }

        /// <summary>插值方式</summary>
        public CurveInterpolation Interpolation { get; set; }

        /// <summary>外推方式</summary>
        public CurveExtrapolation Extrapolation { get; set; }

        /// <summary>数据版本（标定值替换时可溯）</summary>
        public string Version { get; set; }

        /// <summary>曲线A 锚点光斑（毫米），随标定数据变化。</summary>
        public double AnchorSpotMm
        {
            get
            {
                for (int i = 0; i < PointsA.Count; i++)
                {
                    if (Math.Abs(PointsA[i].X) <= 1e-9) return PointsA[i].Y;
                }
                return DefaultAnchorSpotMm;
            }
        }

        #endregion

        #region 构造函数

        /// <summary>创建曲线配置并填入内置默认线性组</summary>
        public TaLaCurveConfig()
        {
            PointsA = new List<CurvePoint>();
            PointsB = new List<CurvePoint>();
            Interpolation = CurveInterpolation.PiecewiseLinear;
            Extrapolation = CurveExtrapolation.Clamp;
            Version = "1.0";
            ResetToDefaultPoints();
        }

        #endregion

        #region 公共函数

        /// <summary>解析一行采样点：取前三个数值的后两个。</summary>

        /// <returns>是否成功。</returns>
        private static bool ParsePoint(string[] cells, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            List<double> nums = new List<double>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (double.TryParse(cells[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    nums.Add(v);
                }
            }
            if (nums.Count < 2) return false;
            if (nums.Count >= 3)
            {
                x = nums[nums.Count - 2];
                y = nums[nums.Count - 1];
                return true;
            }
            x = nums[0];
            y = nums[1];
            return true;
        }

        /// <summary>恢复内置默认采样点组（LA=0↔0.6mm；TA=0↔偏移0；此后 1:1 线性外延）。</summary>
        public void ResetToDefaultPoints()
        {
            PointsA.Clear();
            PointsA.Add(new CurvePoint(0.0, DefaultAnchorSpotMm));

            PointsB.Clear();
            PointsB.Add(new CurvePoint(0.0, 0.0));
        }

        /// <summary>按目标光斑大小反查 LA 位置（曲线A 正查的逆运算）。</summary>
        /// <param name="spotMm">目标光斑大小（毫米）</param>
        /// <returns>LA 位置（毫米）；域外且策略为 Reject 时返回 double.NaN</returns>
        public double QueryLaPositionMm(double spotMm)
        {
            return Interpolate(PointsA, spotMm, true);
        }

        /// <summary>按 LA 位置正查光斑大小（曲线A）。</summary>
        /// <param name="laMm">LA 位置（毫米）</param>
        /// <returns>光斑大小（毫米）；域外且策略为 Reject 时返回 double.NaN</returns>
        public double QuerySpotMm(double laMm)
        {
            return Interpolate(PointsA, laMm, false);
        }

        /// <summary>按 TA 位置正查 LA 偏移补偿（曲线B）。</summary>
        /// <param name="taMm">TA 位置（毫米）</param>
        /// <returns>LA 偏移（毫米）；域外且策略为 Reject 时返回 double.NaN</returns>
        public double QueryLaOffsetMm(double taMm)
        {
            return Interpolate(PointsB, taMm, false);
        }

        /// <summary>从配置文件加载曲线数据。</summary>

        /// <returns>成功加载返回 true</returns>
        public bool LoadFromFile()
        {
            XdocumentReaderWriter reader = ConfigFileManager.Instance.GetModuleXml(ConfigModule, CurveFileName).Raw;
            if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
            {
                return false;
            }

            List<CurvePoint> loadedA = ReadPoints(reader, CurveAKey);
            List<CurvePoint> loadedB = ReadPoints(reader, CurveBKey);

            // 任一曲线缺失或单调性不合法，整批不采用。
            // 保留内置默认：半套曲线比无曲线更危险。
            if (!IsValid(loadedA) || !IsValid(loadedB))
            {
                return false;
            }

            PointsA = loadedA;
            PointsB = loadedB;

            string interp = reader.GetElementValue("INTERPOLATION", "PiecewiseLinear");
            Interpolation = interp == "Nearest" ? CurveInterpolation.Nearest : CurveInterpolation.PiecewiseLinear;
            string extrap = reader.GetElementValue("EXTRAPOLATION", "Clamp");
            Extrapolation = extrap == "Reject" ? CurveExtrapolation.Reject : CurveExtrapolation.Clamp;
            Version = reader.GetElementValue("VERSION", Version);
            return true;
        }

        /// <summary>保存曲线数据到配置文件。</summary>
        public void SaveToFile()
        {
            XmlConfigFile file = ConfigFileManager.Instance.GetModuleXml(ConfigModule, CurveFileName);
            XdocumentReaderWriter reader = file.Raw;
            ConfigFileManager.SaveXmlWithAudit(file, "TA曲线", "配置变更", () =>
            {
                if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
                {
                    reader.NewXdocument("Root");
                }

                WritePoints(reader, CurveAKey, PointsA);
                WritePoints(reader, CurveBKey, PointsB);
                reader.AddElement("INTERPOLATION", Interpolation.ToString());
                reader.AddElement("EXTRAPOLATION", Extrapolation.ToString());
                reader.AddElement("VERSION", Version);
                reader.SaveXdocument();
            });
        }

        /// <summary>从输入表格导入采样点并生成曲线配置。</summary>

        /// <param name="path">输入表格文件路径（CSV/TSV）</param>
        /// <param name="error">失败原因（纯文本）</param>
        /// <returns>导入成功返回 true</returns>
        public bool ImportFromCsv(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "输入表格不存在";
                return false;
            }

            List<CurvePoint> loadedA = new List<CurvePoint>();
            List<CurvePoint> loadedB = new List<CurvePoint>();
            int section = 0;   // 0 未进段 / 1 曲线A / 2 曲线B
            try
            {
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0) continue;
                    if (line.Contains("曲线A")) { section = 1; continue; }
                    if (line.Contains("曲线B")) { section = 2; continue; }
                    if (section == 0) continue;

                    string[] cells = line.Split(new char[] { ',', '\t', ';', '|' });
                    CurvePoint point = ParsePoint(cells, out double x, out double y) ? new CurvePoint(x, y) : null;
                    if (point == null) continue;   // 说明行 / 备注行 / 空行
                    if (section == 1) loadedA.Add(point); else loadedB.Add(point);
                }
            }
            catch (Exception ex)
            {
                error = "读取输入表格失败 " + ex.Message;
                return false;
            }

            if (!IsValid(loadedA) || !IsValid(loadedB))
            {
                error = "曲线缺失或单调性不合法 整批拒收 保留原曲线";
                return false;
            }

            bool anchorA = false;
            bool anchorB = false;
            for (int i = 0; i < loadedA.Count; i++) { if (Math.Abs(loadedA[i].X) <= 1e-9) anchorA = true; }
            for (int i = 0; i < loadedB.Count; i++) { if (Math.Abs(loadedB[i].X) <= 1e-9) anchorB = true; }
            if (!anchorA || !anchorB)
            {
                error = "缺少 X=0 锚点行 出厂缺省行勿删";
                return false;
            }

            PointsA = loadedA;
            PointsB = loadedB;
            Version = "导入-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            SaveToFile();
            return true;
        }

        /// <summary>采样点单调性校验：X 严格递增，Y 单调不减。</summary>

        /// <param name="points">待校验采样点</param>
        /// <returns>合法返回 true</returns>
        public static bool IsValid(List<CurvePoint> points)
        {
            if (points == null || points.Count == 0) return false;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].X <= points[i - 1].X) return false;
                if (points[i].Y < points[i - 1].Y) return false;
            }
            return true;
        }

        #endregion

        #region 私有函数

        /// <summary>插值求值（曲线按 X 升序）。</summary>
        /// <param name="points">采样点（须已通过 <see cref="IsValid"/>）</param>
        /// <param name="value">自变量值</param>
        /// <param name="invert">true = 以 Y 为自变量反查 X（单调不减时有效）；false = 以 X 正查 Y</param>
        /// <returns>求值结果；域外且策略 Reject 时返回 double.NaN</returns>
        private double Interpolate(List<CurvePoint> points, double value, bool invert)
        {
            if (points == null || points.Count == 0) return double.NaN;

            // 单点锚点组按默认线性外延 1:1 处理（斜率 1）。
            // Reject 策略下仅锚点值有效，其余判域外。
            if (points.Count == 1)
            {
                double anchorDomain = Domain(points[0], invert);
                double anchorRange = Range(points[0], invert);
                if (Math.Abs(value - anchorDomain) <= 1e-9) return anchorRange;
                if (Extrapolation == CurveExtrapolation.Reject) return double.NaN;
                return anchorRange + (value - anchorDomain);
            }

            // 自变量域上下界（invert 时自变量为 Y 单调不减；正查时为 X 严格递增）
            double lo = Domain(points[0], invert);
            double hi = Domain(points[points.Count - 1], invert);

            if (value < lo || value > hi)
            {
                if (Extrapolation == CurveExtrapolation.Reject) return double.NaN;
                // Clamp 到对应端点：低端取首点，高端取末点
                return value < lo ? Range(points[0], invert) : Range(points[points.Count - 1], invert);
            }

            if (Interpolation == CurveInterpolation.Nearest)
            {
                CurvePoint nearest = points[0];
                double best = Math.Abs(Domain(points[0], invert) - value);
                for (int i = 1; i < points.Count; i++)
                {
                    double d = Math.Abs(Domain(points[i], invert) - value);
                    if (d < best) { best = d; nearest = points[i]; }
                }
                return Range(nearest, invert);
            }

            // 分段线性：找自变量所在区间
            for (int i = 1; i < points.Count; i++)
            {
                double a = Domain(points[i - 1], invert);
                double b = Domain(points[i], invert);
                if (value > b) continue;
                if (value < a) continue;
                double span = b - a;
                double ratio = span == 0.0 ? 0.0 : (value - a) / span;
                double y0 = Range(points[i - 1], invert);
                double y1 = Range(points[i], invert);
                return y0 + (y1 - y0) * ratio;
            }
            return invert ? points[points.Count - 1].X : points[points.Count - 1].Y;
        }

        /// <summary>取采样点的自变量（invert=true 时为 Y，否则为 X）</summary>
        /// <param name="p">采样点</param>
        /// <param name="invert">是否反查</param>
        /// <returns>自变量值</returns>
        private static double Domain(CurvePoint p, bool invert)
        {
            return invert ? p.Y : p.X;
        }

        /// <summary>取采样点的因变量（invert=true 时为 X，否则为 Y）</summary>
        /// <param name="p">采样点</param>
        /// <param name="invert">是否反查</param>
        /// <returns>因变量值</returns>
        private static double Range(CurvePoint p, bool invert)
        {
            return invert ? p.X : p.Y;
        }

        /// <summary>读取一条曲线的采样点。</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="key">曲线元素名</param>
        /// <returns>采样点列表（无数据返回空列表）</returns>
        private static List<CurvePoint> ReadPoints(XdocumentReaderWriter reader, string key)
        {
            List<CurvePoint> result = new List<CurvePoint>();
            foreach (XElement elem in reader.GetChildElements(key, "Point"))
            {
                if (double.TryParse(elem.Attribute("X") == null ? "0" : elem.Attribute("X").Value,
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(elem.Attribute("Y") == null ? "0" : elem.Attribute("Y").Value,
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    result.Add(new CurvePoint(x, y));
                }
            }
            return result;
        }

        /// <summary>写出一条曲线的采样点。</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="key">曲线元素名</param>
        /// <param name="points">采样点列表</param>
        private static void WritePoints(XdocumentReaderWriter reader, string key, List<CurvePoint> points)
        {
            foreach (CurvePoint p in points)
            {
                Dictionary<string, string> attrs = new Dictionary<string, string>
                {
                    { "X", p.X.ToString("0.####", CultureInfo.InvariantCulture) },
                    { "Y", p.Y.ToString("0.####", CultureInfo.InvariantCulture) }
                };
                reader.AddElementWithAttributes(reader.GetElementByPath(key), "Point", attrs);
            }
        }

        #endregion
    }
}
