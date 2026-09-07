using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using AdaWeldSystem.Comm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达运控配置</summary>
    /// <remarks>
    /// 承载主站参数、轴表、输入表、输出表、PVT 焊缝跟踪参数与线激光标定参数，
    /// 走 FileOperate/XMLfile.cs 读写，禁止 XmlSerializer。
    /// 配置文件为 Config\XML\DeltaMotionConfig.xml，根元素 ROOT。
    /// 本类为普通实例类，由 MoveControlData 持有，全系统只有 MontionManager.MoveData 一处入口。
    /// 非线程安全：只在初始化与停机阶段调用，运行期禁止在流程循环内读写配置。
    /// </remarks>
    public class DeltaMotionConfig
    {
        #region 私有变量

        private const string Tag = "台达运控";
        private const string RootName = "ROOT";
        private const string MasterPath = "ROOT/MASTER";
        private const string AxesPath = "ROOT/AXES";
        private const string InputsPath = "ROOT/INPUTS";
        private const string OutputsPath = "ROOT/OUTPUTS";
        private const string PVTPath = "ROOT/PVT";
        private const string CalibPath = "ROOT/CALIBRATION";

        private readonly List<DeltaAxisConfig> _axes = new List<DeltaAxisConfig>();
        private readonly List<DeltaIOConfig> _inputs = new List<DeltaIOConfig>();
        private readonly List<DeltaIOConfig> _outputs = new List<DeltaIOConfig>();

        #endregion

        #region 公共变量

        /// <summary>配置文件名（不含扩展名）</summary>
        public string ConfigFileName { get { return "DeltaMotionConfig"; } }

        /// <summary>主站卡号</summary>
        public ushort CardNo { get; set; }

        /// <summary>初始化超时时间（毫秒）</summary>
        public int InitialTimeoutMs { get; set; }

        /// <summary>初始化轮询间隔（毫秒）</summary>
        public int InitialPollIntervalMs { get; set; }

        /// <summary>是否启用硬件看门狗</summary>
        public bool WatchDogEnable { get; set; }

        /// <summary>看门狗超时时间（秒）</summary>
        public double WatchDogSeconds { get; set; }

        /// <summary>轴配置集合</summary>
        public IList<DeltaAxisConfig> Axes { get { return _axes; } }

        /// <summary>数字输入配置集合</summary>
        public IList<DeltaIOConfig> Inputs { get { return _inputs; } }

        /// <summary>数字输出配置集合</summary>
        public IList<DeltaIOConfig> Outputs { get { return _outputs; } }

        /// <summary>PVT 时间模式</summary>
        public PVTTimeMode PVTTimeMode { get; set; }

        /// <summary>PVT 单批下发点数</summary>
        public int PVTBatchSize { get; set; }

        /// <summary>PVT 续喂水位阈值，剩余缓冲区低于该值时追加下一段</summary>
        public int PVTRefillThreshold { get; set; }

        /// <summary>PVT 平滑停止时间（秒）</summary>
        public double PVTSmoothStopTime { get; set; }

        /// <summary>焊缝跟踪最大跟随速度（工程单位/秒）</summary>
        public double TrackMaxVel { get; set; }

        /// <summary>焊缝跟踪最大加速度（工程单位/秒平方）</summary>
        public double TrackMaxAcc { get; set; }

        /// <summary>跟踪前馈补偿时间（秒），用于抵消通信与机械滞后</summary>
        public double TrackFeedForwardSec { get; set; }

        /// <summary>跟踪位置死区（工程单位），偏差小于该值不产生新目标点</summary>
        public double TrackDeadZone { get; set; }

        /// <summary>线激光前置标定距离（工程单位）：线激光相对焊接头的轴向前置安装距离</summary>
        public double LaserFrontOffset { get; set; }

        /// <summary>线激光与水平轴的夹角（度），UI 直接读写该值</summary>
        public double LaserToHorizontalAngleDeg { get; set; }

        /// <summary>线激光 X 1 单位对应水平轴距离（标定系数）</summary>
        public double LaserToHorizontalScaleX { get; set; }

        /// <summary>标定的焊缝中心点 X（线激光图像坐标）</summary>
        public double CalibratedCenterX { get; set; }

        /// <summary>标定的焊缝中心点 Y（线激光图像坐标）</summary>
        public double CalibratedCenterY { get; set; }

        /// <summary>趋近匹配阈值，实际机器人 X 与缓存键差值小于该值时触发水平轴移动</summary>
        public double ApproachThreshold { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建配置并填入默认参数</summary>
        public DeltaMotionConfig()
        {
            CardNo = 0;
            InitialTimeoutMs = 30000;
            InitialPollIntervalMs = 100;
            WatchDogEnable = true;
            WatchDogSeconds = 1.0;

            PVTTimeMode = AdaWeldSystem.MotionControl.IMotion.PVTTimeMode.Relative;
            PVTBatchSize = 200;
            PVTRefillThreshold = 50;
            PVTSmoothStopTime = 0.1;

            TrackMaxVel = 100.0;
            TrackMaxAcc = 1000.0;
            TrackFeedForwardSec = 0.024;
            TrackDeadZone = 0.02;

            LaserFrontOffset = 0.0;
            LaserToHorizontalAngleDeg = 0.0;
            LaserToHorizontalScaleX = 1.0;
            CalibratedCenterX = 0.0;
            CalibratedCenterY = 0.0;
            ApproachThreshold = 5.0;
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>读取双精度配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static double ReadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            double val;
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out val) ? val : def;
        }

        /// <summary>读取整数配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static int ReadInt(XdocumentReaderWriter reader, string path, int def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            int val;
            return int.TryParse(txt, out val) ? val : def;
        }

        /// <summary>读取无符号短整型配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static ushort ReadUShort(XdocumentReaderWriter reader, string path, ushort def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            ushort val;
            return ushort.TryParse(txt, out val) ? val : def;
        }

        /// <summary>读取布尔配置，文本 TRUE 视为真</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static bool ReadBool(XdocumentReaderWriter reader, string path, bool def)
        {
            string txt = reader.GetElementValueByPath(path, def ? "TRUE" : "FALSE");
            return txt.ToUpper() == "TRUE";
        }

        /// <summary>按固定小数位写出文本</summary>
        /// <param name="value">待写出数值</param>
        /// <returns>不变文化下的文本</returns>
        private static string Text(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>读取单轴配置</summary>
        /// <param name="element">AXIS 元素</param>
        /// <returns>轴配置</returns>
        private static DeltaAxisConfig ReadAxis(XElement element)
        {
            DeltaAxisConfig axis = new DeltaAxisConfig
            {
                AxisName = ChildValue(element, "AXIS_NAME", string.Empty),
                NodeId = ParseUShort(ChildValue(element, "NODE_ID", "0")),
                SlotNo = ParseUShort(ChildValue(element, "SLOT_NO", "0")),
                Enabled = ChildValue(element, "ENABLED", "TRUE").ToUpper() == "TRUE",
                Equiv = ParseDouble(ChildValue(element, "EQUIV", "0.001")),
                MinVel = ParseDouble(ChildValue(element, "MIN_VEL", "0")),
                MaxVel = ParseDouble(ChildValue(element, "MAX_VEL", "100")),
                AccTime = ParseDouble(ChildValue(element, "ACC_TIME", "0.1")),
                DecTime = ParseDouble(ChildValue(element, "DEC_TIME", "0.1")),
                STime = ParseDouble(ChildValue(element, "S_TIME", "0")),
                StopVel = ParseDouble(ChildValue(element, "STOP_VEL", "0")),
                HomeMode = ParseUShort(ChildValue(element, "HOME_MODE", "0")),
                HomeDir = ParseUShort(ChildValue(element, "HOME_DIR", "1")),
                HomeMaxVel = ParseDouble(ChildValue(element, "HOME_MAX_VEL", "20")),
                PositiveLimit = ParseDouble(ChildValue(element, "POSITIVE_LIMIT", "0")),
                NegativeLimit = ParseDouble(ChildValue(element, "NEGATIVE_LIMIT", "0"))
            };
            return axis;
        }

        /// <summary>读取单路 IO 配置</summary>
        /// <param name="element">IO 元素</param>
        /// <returns>IO 配置</returns>
        private static DeltaIOConfig ReadIO(XElement element)
        {
            DeltaIOConfig io = new DeltaIOConfig
            {
                IOName = ChildValue(element, "IO_NAME", string.Empty),
                NodeId = ParseUShort(ChildValue(element, "NODE_ID", "0")),
                SlotNo = ParseUShort(ChildValue(element, "SLOT_NO", "0")),
                BitNo = ParseUShort(ChildValue(element, "BIT_NO", "0")),
                Enabled = ChildValue(element, "ENABLED", "TRUE").ToUpper() == "TRUE",
                Remark = ChildValue(element, "REMARK", string.Empty)
            };
            return io;
        }

        /// <summary>写出单路 IO 配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="parent">父元素</param>
        /// <param name="io">IO 配置</param>
        private static void WriteIO(XdocumentReaderWriter reader, XElement parent, DeltaIOConfig io)
        {
            XElement element = reader.AddElement(parent, "IO");
            reader.AddElement(element, "IO_NAME", io.IOName);
            reader.AddElement(element, "NODE_ID", io.NodeId.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(element, "SLOT_NO", io.SlotNo.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(element, "BIT_NO", io.BitNo.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(element, "ENABLED", io.Enabled ? "TRUE" : "FALSE");
            reader.AddElement(element, "REMARK", io.Remark);
        }

        /// <summary>取子元素文本</summary>
        /// <param name="parent">父元素</param>
        /// <param name="name">子元素名</param>
        /// <param name="def">缺失时的默认值</param>
        /// <returns>元素值</returns>
        private static string ChildValue(XElement parent, string name, string def)
        {
            XElement child = parent == null ? null : parent.Element(name);
            return child == null ? def : child.Value;
        }

        /// <summary>解析双精度，失败返回 0</summary>
        /// <param name="txt">文本</param>
        /// <returns>数值</returns>
        private static double ParseDouble(string txt)
        {
            double val;
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out val) ? val : 0.0;
        }

        /// <summary>解析无符号短整型，失败返回 0</summary>
        /// <param name="txt">文本</param>
        /// <returns>数值</returns>
        private static ushort ParseUShort(string txt)
        {
            ushort val;
            return ushort.TryParse(txt, out val) ? val : (ushort)0;
        }

        #endregion

        #region 公共函数

        /// <summary>加载配置，读取失败时保留默认值</summary>
        public void LoadConfig()
        {
            XdocumentReaderWriter reader = new XdocumentReaderWriter(ConfigFileName);
            if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
            {
                Log("配置为空，保留默认参数", MessageLevel.Warning);
                return;
            }

            CardNo = ReadUShort(reader, MasterPath + "/CARD_NO", CardNo);
            InitialTimeoutMs = ReadInt(reader, MasterPath + "/INITIAL_TIMEOUT_MS", InitialTimeoutMs);
            InitialPollIntervalMs = ReadInt(reader, MasterPath + "/INITIAL_POLL_INTERVAL_MS", InitialPollIntervalMs);
            WatchDogEnable = ReadBool(reader, MasterPath + "/WATCHDOG_ENABLE", WatchDogEnable);
            WatchDogSeconds = ReadDouble(reader, MasterPath + "/WATCHDOG_SECONDS", WatchDogSeconds);

            _axes.Clear();
            foreach (XElement element in reader.GetChildElements(AxesPath, "AXIS"))
            {
                _axes.Add(ReadAxis(element));
            }

            _inputs.Clear();
            foreach (XElement element in reader.GetChildElements(InputsPath, "IO"))
            {
                _inputs.Add(ReadIO(element));
            }

            _outputs.Clear();
            foreach (XElement element in reader.GetChildElements(OutputsPath, "IO"))
            {
                _outputs.Add(ReadIO(element));
            }

            PVTTimeMode = (PVTTimeMode)ReadInt(reader, PVTPath + "/TIME_MODE", (int)PVTTimeMode);
            PVTBatchSize = ReadInt(reader, PVTPath + "/BATCH_SIZE", PVTBatchSize);
            PVTRefillThreshold = ReadInt(reader, PVTPath + "/REFILL_THRESHOLD", PVTRefillThreshold);
            PVTSmoothStopTime = ReadDouble(reader, PVTPath + "/SMOOTH_STOP_TIME", PVTSmoothStopTime);
            TrackMaxVel = ReadDouble(reader, PVTPath + "/TRACK_MAX_VEL", TrackMaxVel);
            TrackMaxAcc = ReadDouble(reader, PVTPath + "/TRACK_MAX_ACC", TrackMaxAcc);
            TrackFeedForwardSec = ReadDouble(reader, PVTPath + "/TRACK_FEED_FORWARD_SEC", TrackFeedForwardSec);
            TrackDeadZone = ReadDouble(reader, PVTPath + "/TRACK_DEAD_ZONE", TrackDeadZone);

            LaserFrontOffset = ReadDouble(reader, CalibPath + "/LASER_FRONT_OFFSET", LaserFrontOffset);
            LaserToHorizontalAngleDeg = ReadDouble(reader, CalibPath + "/LASER_ANGLE_DEG", LaserToHorizontalAngleDeg);
            LaserToHorizontalScaleX = ReadDouble(reader, CalibPath + "/LASER_SCALE_X", LaserToHorizontalScaleX);
            CalibratedCenterX = ReadDouble(reader, CalibPath + "/CALIBRATED_CENTER_X", CalibratedCenterX);
            CalibratedCenterY = ReadDouble(reader, CalibPath + "/CALIBRATED_CENTER_Y", CalibratedCenterY);
            ApproachThreshold = ReadDouble(reader, CalibPath + "/APPROACH_THRESHOLD", ApproachThreshold);

            Log(string.Format("配置加载完成 卡号 {0} 轴 {1} 个 输入 {2} 个 输出 {3} 个",
                CardNo, _axes.Count, _inputs.Count, _outputs.Count));
        }

        /// <summary>保存配置，末尾落盘</summary>
        public void SaveConfig()
        {
            XdocumentReaderWriter reader = new XdocumentReaderWriter(ConfigFileName);
            reader.NewXdocument(RootName, "1.0", "UTF-8");

            XElement master = reader.AddElement("MASTER");
            reader.AddElement(master, "CARD_NO", CardNo.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(master, "INITIAL_TIMEOUT_MS", InitialTimeoutMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(master, "INITIAL_POLL_INTERVAL_MS",
                InitialPollIntervalMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(master, "WATCHDOG_ENABLE", WatchDogEnable ? "TRUE" : "FALSE");
            reader.AddElement(master, "WATCHDOG_SECONDS", Text(WatchDogSeconds));

            XElement axes = reader.AddElement("AXES");
            foreach (DeltaAxisConfig axis in _axes)
            {
                XElement element = reader.AddElement(axes, "AXIS");
                reader.AddElement(element, "AXIS_NAME", axis.AxisName);
                reader.AddElement(element, "NODE_ID", axis.NodeId.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "SLOT_NO", axis.SlotNo.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "ENABLED", axis.Enabled ? "TRUE" : "FALSE");
                reader.AddElement(element, "EQUIV", Text(axis.Equiv));
                reader.AddElement(element, "MIN_VEL", Text(axis.MinVel));
                reader.AddElement(element, "MAX_VEL", Text(axis.MaxVel));
                reader.AddElement(element, "ACC_TIME", Text(axis.AccTime));
                reader.AddElement(element, "DEC_TIME", Text(axis.DecTime));
                reader.AddElement(element, "S_TIME", Text(axis.STime));
                reader.AddElement(element, "STOP_VEL", Text(axis.StopVel));
                reader.AddElement(element, "HOME_MODE", axis.HomeMode.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "HOME_DIR", axis.HomeDir.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "HOME_MAX_VEL", Text(axis.HomeMaxVel));
                reader.AddElement(element, "POSITIVE_LIMIT", Text(axis.PositiveLimit));
                reader.AddElement(element, "NEGATIVE_LIMIT", Text(axis.NegativeLimit));
            }

            XElement inputs = reader.AddElement("INPUTS");
            foreach (DeltaIOConfig io in _inputs)
            {
                WriteIO(reader, inputs, io);
            }

            XElement outputs = reader.AddElement("OUTPUTS");
            foreach (DeltaIOConfig io in _outputs)
            {
                WriteIO(reader, outputs, io);
            }

            XElement pvt = reader.AddElement("PVT");
            reader.AddElement(pvt, "TIME_MODE", ((int)PVTTimeMode).ToString(CultureInfo.InvariantCulture));
            reader.AddElement(pvt, "BATCH_SIZE", PVTBatchSize.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(pvt, "REFILL_THRESHOLD", PVTRefillThreshold.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(pvt, "SMOOTH_STOP_TIME", Text(PVTSmoothStopTime));
            reader.AddElement(pvt, "TRACK_MAX_VEL", Text(TrackMaxVel));
            reader.AddElement(pvt, "TRACK_MAX_ACC", Text(TrackMaxAcc));
            reader.AddElement(pvt, "TRACK_FEED_FORWARD_SEC", Text(TrackFeedForwardSec));
            reader.AddElement(pvt, "TRACK_DEAD_ZONE", Text(TrackDeadZone));

            XElement calib = reader.AddElement("CALIBRATION");
            reader.AddElement(calib, "LASER_FRONT_OFFSET", Text(LaserFrontOffset));
            reader.AddElement(calib, "LASER_ANGLE_DEG", Text(LaserToHorizontalAngleDeg));
            reader.AddElement(calib, "LASER_SCALE_X", Text(LaserToHorizontalScaleX));
            reader.AddElement(calib, "CALIBRATED_CENTER_X", Text(CalibratedCenterX));
            reader.AddElement(calib, "CALIBRATED_CENTER_Y", Text(CalibratedCenterY));
            reader.AddElement(calib, "APPROACH_THRESHOLD", Text(ApproachThreshold));

            reader.SaveXdocument();
            Log(string.Format("配置保存完成 卡号 {0} 轴 {1} 个 输入 {2} 个 输出 {3} 个",
                CardNo, _axes.Count, _inputs.Count, _outputs.Count));
        }

        /// <summary>按名称取轴配置，未找到返回 null</summary>
        /// <param name="axisName">轴名称</param>
        /// <returns>轴配置</returns>
        public DeltaAxisConfig GetAxis(string axisName)
        {
            for (int i = 0; i < _axes.Count; i++)
            {
                if (_axes[i].AxisName == axisName)
                    return _axes[i];
            }
            return null;
        }

        /// <summary>按名称取输入配置，未找到返回 null</summary>
        /// <param name="ioName">输入名称</param>
        /// <returns>输入配置</returns>
        public DeltaIOConfig GetInput(string ioName)
        {
            return FindIO(_inputs, ioName);
        }

        /// <summary>按名称取输出配置，未找到返回 null</summary>
        /// <param name="ioName">输出名称</param>
        /// <returns>输出配置</returns>
        public DeltaIOConfig GetOutput(string ioName)
        {
            return FindIO(_outputs, ioName);
        }

        /// <summary>在 IO 表中按名称查找</summary>
        /// <param name="list">待查表</param>
        /// <param name="ioName">IO 名称</param>
        /// <returns>IO 配置，未找到返回 null</returns>
        private static DeltaIOConfig FindIO(List<DeltaIOConfig> list, string ioName)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IOName == ioName)
                    return list[i];
            }
            return null;
        }

        #endregion
    }
}
