using System.Globalization;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>焊接算法配置（Config/XML/WeldParamControl.xml 下的 SEAM_GUIDE + WELD_PARAM_ADJUST 两节）。</summary>
    /// <remarks>
    /// 承载 WeldParamControl 模块全部算法的运行参数：线激光引导（SEAM_GUIDE）与参数调整（WELD_PARAM_ADJUST）。
    /// 走 FileOperate/XdocumentReaderWriter.cs 读写，禁止 XmlSerializer。
    /// 配置实例与读写器均由 <see cref="WeldParamManager"/> 独占持有，外部只读，禁止 new 实例或回灌。
    /// </remarks>
    public class WeldAlgorithmsConfig
    {
        #region 常量

        /// <summary>线激光引导配置节名（Config/XML/WeldParamControl.xml 下的子元素）</summary>
        public const string SeamGuideSection = "SEAM_GUIDE";

        /// <summary>参数调整配置节名（Config/XML/WeldParamControl.xml 下的子元素）</summary>
        public const string AdjustmentSection = "WELD_PARAM_ADJUST";

        private const string PathEnable = SeamGuideSection + "/ENABLE_GUIDE";
        private const string PathAxisPerLaser = SeamGuideSection + "/AXIS_MM_PER_LASER_MM";
        private const string PathLaserZero = SeamGuideSection + "/LASER_ZERO_MM";
        private const string PathGuideSign = SeamGuideSection + "/GUIDE_SIGN";
        private const string PathNoValueTolerance = SeamGuideSection + "/NO_VALUE_TOLERANCE_MM";
        private const string PathOutlierLimit = SeamGuideSection + "/OUTLIER_LIMIT_MM";
        private const string PathMonotonicWindow = SeamGuideSection + "/MONOTONIC_WINDOW_MM";
        private const string PathMonotonicDeadband = SeamGuideSection + "/MONOTONIC_DEADBAND_MM";
        private const string PathBatchSize = SeamGuideSection + "/BATCH_SIZE";
        private const string PathMinRemainBuffer = SeamGuideSection + "/MIN_REMAIN_BUFFER";
        private const string PathFrameSeconds = SeamGuideSection + "/FRAME_SECONDS";
        private const string PathMaxOutputInterval = SeamGuideSection + "/MAX_OUTPUT_INTERVAL_MS";
        private const string PathMaxPendingPoints = SeamGuideSection + "/MAX_PENDING_POINTS";

        private const string PathMaxConsecutiveFailures = AdjustmentSection + "/MAX_CONSECUTIVE_FAILURES";
        private const string PathIsCountType = AdjustmentSection + "/IS_COUNT_TYPE";
        private const string PathCountNum = AdjustmentSection + "/COUNT_NUM";

        #endregion

        #region 公共变量

        /// <summary>是否启用线激光引导 Y 轴，关闭后只转发结果不下发 PVT</summary>
        public bool EnableGuide { get; set; }

        /// <summary>轴与线激光的比例对齐系数（轴毫米 / 线激光毫米），轴 1mm 对应线激光 0.9mm 时为 1/0.9</summary>
        public double AxisMmPerLaserMm { get; set; }

        /// <summary>线激光标定零点（毫米），偏差 = (特征Y - 零点) × 比例 × 方向</summary>
        public double LaserZeroMm { get; set; }

        /// <summary>偏差方向（+1 或 -1），与轴正方向对齐</summary>
        public int GuideSign { get; set; }

        /// <summary>机器人行程内无有效结果的容差（毫米），容差内偏差记 0 继续移动，超出即报警</summary>
        public double NoValueToleranceMm { get; set; }

        /// <summary>超限点剔除阈值（毫米），相邻偏差跳变超过此值不归入轨迹计算</summary>
        public double OutlierLimitMm { get; set; }

        /// <summary>单调窗口（机器人行程，毫米），窗口内轨迹只朝一个方向移动</summary>
        public double MonotonicWindowMm { get; set; }

        /// <summary>方向判定死区（毫米），窗口内位移超过死区才锁定移动方向</summary>
        public double MonotonicDeadbandMm { get; set; }

        /// <summary>单批下发点数，攒够即返回</summary>
        public int BatchSize { get; set; }

        /// <summary>剩余缓冲区续喂下限，低于此值暂缓续喂避免点表溢出</summary>
        public int MinRemainBuffer { get; set; }

        /// <summary>实测帧率不可用时的默认帧间隔（秒）</summary>
        public double DefaultFrameSeconds { get; set; }

        /// <summary>最长输出间隔（毫秒），无论攒批是否够都必须输出一段轨迹</summary>
        public int MaxOutputIntervalMs { get; set; }

        /// <summary>PVT 点表容量上限，超过后丢弃最旧点防止内存膨胀</summary>
        public int MaxPendingPoints { get; set; }

        /// <summary>最大允许连续失败次数，达到即中断调整并进入保持态</summary>
        public int MaxConsecutiveFailures { get; set; }

        /// <summary>是否按点数分段计算（false 为按距离分段，待实现）</summary>
        public bool IsCountType { get; set; }

        /// <summary>每段点数，按点数分段时累计满该数量才参与滑动平均</summary>
        public int CountNum { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建配置并填入默认值</summary>
        public WeldAlgorithmsConfig()
        {
            EnableGuide = true;
            AxisMmPerLaserMm = 1.0;
            LaserZeroMm = 0.0;
            GuideSign = 1;
            NoValueToleranceMm = 2.0;
            OutlierLimitMm = 2.0;
            MonotonicWindowMm = 50.0;
            MonotonicDeadbandMm = 0.2;
            BatchSize = 4;
            MinRemainBuffer = 200;
            DefaultFrameSeconds = 1.0 / 60.0;
            MaxOutputIntervalMs = 250;
            MaxPendingPoints = 8000;

            MaxConsecutiveFailures = 5;
            IsCountType = true;
            CountNum = 5;
        }

        #endregion

        #region 私有函数

        private static int ReadInt(XdocumentReaderWriter reader, string path, int def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            int val;
            return int.TryParse(txt, out val) ? val : def;
        }

        private static double ReadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            double val;
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out val) ? val : def;
        }

        private static bool ReadBool(XdocumentReaderWriter reader, string path, bool def)
        {
            string txt = reader.GetElementValueByPath(path, def ? "TRUE" : "FALSE");
            return txt != null && txt.ToUpper() == "TRUE";
        }

        private static string Text(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>写入子元素，不存在则创建，避免重复保存产生重复节点</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="parent">父元素</param>
        /// <param name="name">子元素名</param>
        /// <param name="value">子元素值</param>
        private static void SetChild(XdocumentReaderWriter reader, XElement parent, string name, string value)
        {
            XElement child = parent.Element(name);
            if (child == null)
            {
                reader.AddElement(parent, name, value);
            }
            else
            {
                child.Value = value;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>从配置读写器加载 SEAM_GUIDE 与 WELD_PARAM_ADJUST 两节参数，缺失项保留默认值。</summary>
        /// <param name="reader">配置读写器</param>
        public void LoadConfig(XdocumentReaderWriter reader)
        {
            if (reader == null || reader.GetXDocument() == null || reader.GetXDocument().Root == null) return;

            LoadSeamGuide(reader);
            LoadAdjustment(reader);
        }

        /// <summary>把两节参数写入配置读写器（仅更新对应段，不重建整篇）。</summary>
        /// <param name="reader">配置读写器</param>
        public void SaveConfig(XdocumentReaderWriter reader)
        {
            if (reader == null || reader.GetXDocument() == null || reader.GetXDocument().Root == null) return;

            SaveSeamGuide(reader);
            SaveAdjustment(reader);
        }

        #endregion

        #region 私有函数（分节读写）

        /// <summary>加载线激光引导段</summary>
        /// <param name="reader">配置读写器</param>
        private void LoadSeamGuide(XdocumentReaderWriter reader)
        {
            EnableGuide = ReadBool(reader, PathEnable, EnableGuide);
            AxisMmPerLaserMm = ReadDouble(reader, PathAxisPerLaser, AxisMmPerLaserMm);
            LaserZeroMm = ReadDouble(reader, PathLaserZero, LaserZeroMm);
            GuideSign = ReadInt(reader, PathGuideSign, GuideSign);
            NoValueToleranceMm = ReadDouble(reader, PathNoValueTolerance, NoValueToleranceMm);
            OutlierLimitMm = ReadDouble(reader, PathOutlierLimit, OutlierLimitMm);
            MonotonicWindowMm = ReadDouble(reader, PathMonotonicWindow, MonotonicWindowMm);
            MonotonicDeadbandMm = ReadDouble(reader, PathMonotonicDeadband, MonotonicDeadbandMm);
            BatchSize = ReadInt(reader, PathBatchSize, BatchSize);
            MinRemainBuffer = ReadInt(reader, PathMinRemainBuffer, MinRemainBuffer);
            DefaultFrameSeconds = ReadDouble(reader, PathFrameSeconds, DefaultFrameSeconds);
            MaxOutputIntervalMs = ReadInt(reader, PathMaxOutputInterval, MaxOutputIntervalMs);
            MaxPendingPoints = ReadInt(reader, PathMaxPendingPoints, MaxPendingPoints);

            if (AxisMmPerLaserMm <= 0.0) AxisMmPerLaserMm = 1.0;
            if (GuideSign >= 0) GuideSign = 1; else GuideSign = -1;
            if (BatchSize < 1) BatchSize = 1;
            if (MaxOutputIntervalMs < 1) MaxOutputIntervalMs = 1;
            if (MaxPendingPoints < 1) MaxPendingPoints = 1;
            if (DefaultFrameSeconds <= 0.0) DefaultFrameSeconds = 1.0 / 60.0;
        }

        /// <summary>保存线激光引导段</summary>
        /// <param name="reader">配置读写器</param>
        private void SaveSeamGuide(XdocumentReaderWriter reader)
        {
            XElement section = reader.GetElementByPath(SeamGuideSection);
            if (section == null) section = reader.AddElement(SeamGuideSection);

            SetChild(reader, section, "ENABLE_GUIDE", EnableGuide ? "TRUE" : "FALSE");
            SetChild(reader, section, "AXIS_MM_PER_LASER_MM", Text(AxisMmPerLaserMm));
            SetChild(reader, section, "LASER_ZERO_MM", Text(LaserZeroMm));
            SetChild(reader, section, "GUIDE_SIGN", GuideSign.ToString(CultureInfo.InvariantCulture));
            SetChild(reader, section, "NO_VALUE_TOLERANCE_MM", Text(NoValueToleranceMm));
            SetChild(reader, section, "OUTLIER_LIMIT_MM", Text(OutlierLimitMm));
            SetChild(reader, section, "MONOTONIC_WINDOW_MM", Text(MonotonicWindowMm));
            SetChild(reader, section, "MONOTONIC_DEADBAND_MM", Text(MonotonicDeadbandMm));
            SetChild(reader, section, "BATCH_SIZE", BatchSize.ToString(CultureInfo.InvariantCulture));
            SetChild(reader, section, "MIN_REMAIN_BUFFER", MinRemainBuffer.ToString(CultureInfo.InvariantCulture));
            SetChild(reader, section, "FRAME_SECONDS", Text(DefaultFrameSeconds));
            SetChild(reader, section, "MAX_OUTPUT_INTERVAL_MS", MaxOutputIntervalMs.ToString(CultureInfo.InvariantCulture));
            SetChild(reader, section, "MAX_PENDING_POINTS", MaxPendingPoints.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>加载参数调整段</summary>
        /// <param name="reader">配置读写器</param>
        private void LoadAdjustment(XdocumentReaderWriter reader)
        {
            MaxConsecutiveFailures = ReadInt(reader, PathMaxConsecutiveFailures, MaxConsecutiveFailures);
            IsCountType = ReadBool(reader, PathIsCountType, IsCountType);
            CountNum = ReadInt(reader, PathCountNum, CountNum);

            if (MaxConsecutiveFailures < 1) MaxConsecutiveFailures = 1;
            if (CountNum < 1) CountNum = 1;
        }

        /// <summary>保存参数调整段</summary>
        /// <param name="reader">配置读写器</param>
        private void SaveAdjustment(XdocumentReaderWriter reader)
        {
            XElement section = reader.GetElementByPath(AdjustmentSection);
            if (section == null) section = reader.AddElement(AdjustmentSection);

            SetChild(reader, section, "MAX_CONSECUTIVE_FAILURES", MaxConsecutiveFailures.ToString(CultureInfo.InvariantCulture));
            SetChild(reader, section, "IS_COUNT_TYPE", IsCountType ? "TRUE" : "FALSE");
            SetChild(reader, section, "COUNT_NUM", CountNum.ToString(CultureInfo.InvariantCulture));
        }

        #endregion
    }
}
