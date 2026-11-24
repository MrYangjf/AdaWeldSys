﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ConfigFileControl.Implementations;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>焊接头从站配置（万顺兴 ScanCut，非轴设备）</summary>
    public class WeldHeadSlaveConfig
    {
        /// <summary>从站号（主站扫描分配后的节点号）</summary>
        public ushort NodeId { get; set; }

        /// <summary>是否启用焊接头从站</summary>
        public bool Enabled { get; set; }

        /// <summary>厂商号（校验用）</summary>
        public uint VendorId { get; set; }

        /// <summary>产品码（校验用）</summary>
        public uint ProductCode { get; set; }

        /// <summary>版本号（校验用，0 表示不校验）</summary>
        public uint RevisionNo { get; set; }

        /// <summary>创建配置并填入 ESI 默认身份</summary>
        public WeldHeadSlaveConfig()
        {
            NodeId = 2;
            Enabled = false;
            VendorId = 0xE0000002;
            ProductCode = 0x26483052;
            RevisionNo = 0;
        }
    }

    /// <summary>气缸配置（按名称绑定 IO 配置表中的输入与输出）</summary>
    public class DeltaCylinderConfig
    {
        /// <summary>气缸名称</summary>
        public string CylinderName { get; set; }

        /// <summary>伸出到位传感器名（输入 IO 名，空表示未配置）</summary>
        public string StretchSensorName { get; set; }

        /// <summary>缩回到位传感器名（输入 IO 名，空表示未配置）</summary>
        public string RetractSensorName { get; set; }

        /// <summary>中间位传感器名（输入 IO 名，空表示未配置）</summary>
        public string CenterSensorName { get; set; }

        /// <summary>伸出电磁阀名（输出 IO 名，空表示未配置）</summary>
        public string StretchOutputName { get; set; }

        /// <summary>缩回电磁阀名（输出 IO 名，空表示未配置）</summary>
        public string RetractOutputName { get; set; }

        /// <summary>伸出动作显示名（空取默认"伸出"）</summary>
        public string StretchActionName { get; set; }

        /// <summary>缩回动作显示名（空取默认"缩回"）</summary>
        public string RetractActionName { get; set; }

        /// <summary>创建气缸配置并填入空名称</summary>
        public DeltaCylinderConfig()
        {
            CylinderName = string.Empty;
            StretchSensorName = string.Empty;
            RetractSensorName = string.Empty;
            CenterSensorName = string.Empty;
            StretchOutputName = string.Empty;
            RetractOutputName = string.Empty;
            StretchActionName = string.Empty;
            RetractActionName = string.Empty;
        }
    }

    /// <summary>流程等待超时配置（ROOT/FLOW_TIMEOUT，运控唯一真相源）</summary>
    public class FlowTimeoutConfig
    {
        /// <summary>指令状态检测统一超时（秒）</summary>
        public double InstructionDetectSeconds { get; set; }

        /// <summary>指令状态检测采样周期（毫秒）</summary>
        public int InstructionSampleMs { get; set; }

        /// <summary>位置类「连续稳定」采样次数</summary>
        public int InstructionStableSamples { get; set; }

        /// <summary>扭矩类确认窗采样次数</summary>
        public int InstructionTorqueSamples { get; set; }

        /// <summary>单轴运动到位超时（秒）</summary>
        public int MoveDoneSeconds { get; set; }

        /// <summary>单轴回零超时（秒）</summary>
        public int HomeDoneSeconds { get; set; }

        /// <summary>伺服使能超时（秒）</summary>
        public int ServoOnSeconds { get; set; }

        /// <summary>轴状态轮询周期（毫秒）</summary>
        public int AxisPollMs { get; set; }

        /// <summary>停止/使能后的固定沉降（毫秒）</summary>
        public int StopSettleMs { get; set; }

        /// <summary>切 CST 前等待位置运动停稳的上限（秒）</summary>
        public double TorqueEntryStabilizeSeconds { get; set; }

        /// <summary>绝对定位「位置长时间无变化」判失败（秒）</summary>
        public double PositionReachNoChangeSeconds { get; set; }

        /// <summary>绝对定位超时估算下限（秒）</summary>
        public double PositionReachMinSeconds { get; set; }

        /// <summary>绝对定位超时估算硬上限（秒）</summary>
        public double PositionReachMaxSeconds { get; set; }

        /// <summary>界面等待绝对定位结果的上限（秒）</summary>
        public double PositionReachResultWaitSeconds { get; set; }

        /// <summary>SDO 轮询周期（毫秒）</summary>
        public int SdoPollMs { get; set; }

        /// <summary>SDO 轮询最大次数（× 周期 = 单笔上限）</summary>
        public int SdoPollMaxCount { get; set; }

        /// <summary>SDO 发起前等待（毫秒）</summary>
        public int SdoPreDelayMs { get; set; }

        /// <summary>邮箱忙退避重试等待（毫秒）</summary>
        public int SdoMailboxRetryMs { get; set; }

        /// <summary>主站初始化超时（秒）</summary>
        public int MasterInitSeconds { get; set; }

        /// <summary>主站初始化轮询周期（毫秒）</summary>
        public int MasterInitPollMs { get; set; }

        /// <summary>级联连接无反馈超时（毫秒）</summary>
        public int CascadeConnectMs { get; set; }

        /// <summary>相机采集等待信号超时（秒）</summary>
        public double CameraAcquireSeconds { get; set; }

        /// <summary>记录落盘排空等待上限（秒）</summary>
        public double RecordFlushSeconds { get; set; }

        /// <summary>线激光回调排空等待上限（毫秒）</summary>
        public int LineLaserCallbackMs { get; set; }

        /// <summary>Modbus 建立连接超时（毫秒）</summary>
        public int ModbusConnectMs { get; set; }

        /// <summary>Modbus 单次读写超时（毫秒）</summary>
        public int ModbusReadWriteMs { get; set; }

        /// <summary>界面刷新节拍（毫秒，默认 33 ≈ 30Hz）</summary>
        public int UiRefreshMs { get; set; }

        /// <summary>创建配置并填入改造前的现行值</summary>
        public FlowTimeoutConfig()
        {
            InstructionDetectSeconds = 5.0;
            InstructionSampleMs = FlowTimeoutSettings.PollIntervalMs;
            InstructionStableSamples = 3;
            InstructionTorqueSamples = 2;
            MoveDoneSeconds = 60;
            HomeDoneSeconds = 120;
            ServoOnSeconds = 3;
            AxisPollMs = 20;
            StopSettleMs = 50;
            TorqueEntryStabilizeSeconds = 3.0;
            PositionReachNoChangeSeconds = 5.0;
            PositionReachMinSeconds = 10.0;
            PositionReachMaxSeconds = 60.0;
            PositionReachResultWaitSeconds = 70.0;
            SdoPollMs = 2;
            SdoPollMaxCount = 250;
            SdoPreDelayMs = 10;
            SdoMailboxRetryMs = 30;
            MasterInitSeconds = 30;
            MasterInitPollMs = 100;
            CascadeConnectMs = 10000;
            CameraAcquireSeconds = 5.0;
            RecordFlushSeconds = 5.0;
            LineLaserCallbackMs = 200;
            ModbusConnectMs = 3000;
            ModbusReadWriteMs = 5000;
            UiRefreshMs = 33;
        }

        /// <summary>单值钳制：越界/非法回退缺省并告警</summary>
        /// <param name="key">键名。</param>
        /// <param name="value">值。</param>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        /// <param name="def">默认值。</param>
        /// <returns>结果码。</returns>
        private static int Clamp(Action<string> log, string key, int value, int min, int max, int def)
        {
            if (value >= min && value <= max) return value;
            log?.Invoke(string.Format(CultureInfo.InvariantCulture, "{0}={1} 超出 [{2},{3}]，已采用缺省 {4}", key, value, min, max, def));
            return def;
        }

        /// <summary>单值钳制：越界/非法回退缺省并告警（浮点）</summary>
        /// <param name="key">键名。</param>
        /// <param name="value">值。</param>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        /// <param name="def">默认值。</param>
        /// <returns>计算结果。</returns>
        private static double Clamp(Action<string> log, string key, double value, double min, double max, double def)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max) return value;
            log?.Invoke(string.Format(CultureInfo.InvariantCulture, "{0}={1} 超出 [{2},{3}]，已采用缺省 {4}", key, value, min, max, def));
            return def;
        }
        /// <summary>值域校验</summary>

        /// <param name="log">告警输出（键名 + 非法值 + 实际采用值）</param>
        public void Normalize(Action<string> log)
        {
            FlowTimeoutConfig d = new FlowTimeoutConfig();
            InstructionDetectSeconds = Clamp(log, "INSTRUCTION_DETECT_SECONDS", InstructionDetectSeconds, 1.0, 120.0, d.InstructionDetectSeconds);
            InstructionSampleMs = Clamp(log, "INSTRUCTION_SAMPLE_MS", InstructionSampleMs, 5, 200, d.InstructionSampleMs);
            InstructionStableSamples = Clamp(log, "INSTRUCTION_STABLE_SAMPLES", InstructionStableSamples, 1, 20, d.InstructionStableSamples);
            InstructionTorqueSamples = Clamp(log, "INSTRUCTION_TORQUE_SAMPLES", InstructionTorqueSamples, 1, 20, d.InstructionTorqueSamples);
            MoveDoneSeconds = Clamp(log, "MOVE_DONE_SECONDS", MoveDoneSeconds, 5, 600, d.MoveDoneSeconds);
            HomeDoneSeconds = Clamp(log, "HOME_DONE_SECONDS", HomeDoneSeconds, 10, 1200, d.HomeDoneSeconds);
            ServoOnSeconds = Clamp(log, "SERVO_ON_SECONDS", ServoOnSeconds, 1, 60, d.ServoOnSeconds);
            AxisPollMs = Clamp(log, "AXIS_POLL_MS", AxisPollMs, 5, 200, d.AxisPollMs);
            StopSettleMs = Clamp(log, "STOP_SETTLE_MS", StopSettleMs, 0, 500, d.StopSettleMs);
            TorqueEntryStabilizeSeconds = Clamp(log, "TORQUE_ENTRY_STABILIZE_SECONDS", TorqueEntryStabilizeSeconds, 0.5, 30.0, d.TorqueEntryStabilizeSeconds);
            PositionReachNoChangeSeconds = Clamp(log, "ZERO_POSE_NO_CHANGE_SECONDS", PositionReachNoChangeSeconds, 2.0, 60.0, d.PositionReachNoChangeSeconds);
            PositionReachMinSeconds = Clamp(log, "ZERO_POSE_MIN_SECONDS", PositionReachMinSeconds, 5.0, 120.0, d.PositionReachMinSeconds);
            PositionReachMaxSeconds = Clamp(log, "ZERO_POSE_MAX_SECONDS", PositionReachMaxSeconds, 10.0, 600.0, d.PositionReachMaxSeconds);
            if (PositionReachMaxSeconds < PositionReachMinSeconds)
            {
                log?.Invoke("ZERO_POSE_MAX_SECONDS 小于 ZERO_POSE_MIN_SECONDS，已按下限使用");
                PositionReachMaxSeconds = PositionReachMinSeconds;
            }
            PositionReachResultWaitSeconds = Clamp(log, "ZERO_POSE_RESULT_WAIT_SECONDS", PositionReachResultWaitSeconds, 10.0, 900.0, d.PositionReachResultWaitSeconds);
            SdoPollMs = Clamp(log, "SDO_POLL_MS", SdoPollMs, 1, 50, d.SdoPollMs);
            SdoPollMaxCount = Clamp(log, "SDO_POLL_MAX_COUNT", SdoPollMaxCount, 20, 5000, d.SdoPollMaxCount);
            SdoPreDelayMs = Clamp(log, "SDO_PRE_DELAY_MS", SdoPreDelayMs, 0, 200, d.SdoPreDelayMs);
            SdoMailboxRetryMs = Clamp(log, "SDO_MAILBOX_RETRY_MS", SdoMailboxRetryMs, 0, 500, d.SdoMailboxRetryMs);
            MasterInitSeconds = Clamp(log, "MASTER_INIT_SECONDS", MasterInitSeconds, 5, 300, d.MasterInitSeconds);
            MasterInitPollMs = Clamp(log, "MASTER_INIT_POLL_MS", MasterInitPollMs, 10, 1000, d.MasterInitPollMs);
            CascadeConnectMs = Clamp(log, "CASCADE_CONNECT_MS", CascadeConnectMs, 1000, 120000, d.CascadeConnectMs);
            CameraAcquireSeconds = Clamp(log, "CAMERA_ACQUIRE_SECONDS", CameraAcquireSeconds, 1.0, 60.0, d.CameraAcquireSeconds);
            RecordFlushSeconds = Clamp(log, "RECORD_FLUSH_SECONDS", RecordFlushSeconds, 1.0, 60.0, d.RecordFlushSeconds);
            LineLaserCallbackMs = Clamp(log, "LINE_LASER_CALLBACK_MS", LineLaserCallbackMs, 20, 5000, d.LineLaserCallbackMs);
            ModbusConnectMs = Clamp(log, "MODBUS_CONNECT_MS", ModbusConnectMs, 500, 30000, d.ModbusConnectMs);
            ModbusReadWriteMs = Clamp(log, "MODBUS_READ_WRITE_MS", ModbusReadWriteMs, 500, 60000, d.ModbusReadWriteMs);
            UiRefreshMs = Clamp(log, "UI_REFRESH_MS", UiRefreshMs, 10, 500, d.UiRefreshMs);
        }

    }

    /// <summary>流程超时快照发布点：读侧取 <see cref="Current"/>，加载后替换</summary>
    public static class FlowTimeoutSettings
    {
        private static FlowTimeoutConfig _current = new FlowTimeoutConfig();

        /// <summary>执行侧轮询节拍 16 毫秒</summary>
        public const int PollIntervalMs = 16;

        /// <summary>UI 刷新节拍（毫秒）：配置 ROOT/FLOW_TIMEOUT/UI_REFRESH_MS，缺省 33</summary>
        public static int UiRefreshMs { get { return _current.UiRefreshMs; } }

        /// <summary>当前生效的流程超时（改配置即生效）</summary>
        public static FlowTimeoutConfig Current { get { return _current; } }

        /// <summary>发布新快照（只由配置加载/保存调用）</summary>
        /// <param name="config">已校验的配置</param>
        internal static void Publish(FlowTimeoutConfig config)
        {
            if (config != null) _current = config;
        }
    }

    /// <summary>台达运控配置</summary>
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
        private const string SlavePath = "ROOT/WELD_HEAD_SLAVE";
        private const string CylindersPath = "ROOT/CYLINDERS";
        private const string SeamTrackPath = "ROOT/SEAM_TRACK";
        private const string TaPath = "ROOT/TA";
        private const string TaHomePath = "ROOT/TA_HOME";

        /// <summary>流程等待超时节点（与设置页该区一一对应）</summary>
        private const string FlowTimeoutPath = "ROOT/FLOW_TIMEOUT";

        private readonly List<DeltaAxisConfig> _axes = new List<DeltaAxisConfig>();
        private readonly List<DeltaIOConfig> _inputs = new List<DeltaIOConfig>();
        private readonly List<DeltaIOConfig> _outputs = new List<DeltaIOConfig>();
        private readonly List<DeltaCylinderConfig> _cylinders = new List<DeltaCylinderConfig>();

        /// <summary>撞号设备键清单</summary>
        public List<string> ConflictingDeviceKeys { get; private set; }

        /// <summary>焊接头从站设备键（撞号清单用）</summary>
        public const string WeldHeadOwnerKey = "WELDHEAD";

        /// <summary>轴设备键（撞号清单用）</summary>
        /// <param name="axisName">轴名</param>
        /// <returns>设备键</returns>
        public static string AxisOwnerKey(string axisName)
        {
            return "AXIS:" + axisName;
        }

        #endregion

        #region 公共变量

        /// <summary>配置文件名（不含扩展名）</summary>
        public string ConfigFileName { get { return "DeltaMotionConfig"; } }

        /// <summary>运动配置所属模块（全局配置目录下的模块层）。</summary>
        public const string ConfigModule = "MotionControl";

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

        /// <summary>气缸配置集合</summary>
        public IList<DeltaCylinderConfig> Cylinders { get { return _cylinders; } }

        /// <summary>PVT 时间模式</summary>
        public PVTTimeMode PVTTimeMode { get; set; }

        /// <summary>PVT 单批下发点数</summary>
        public int PVTBatchSize { get; set; }

        /// <summary>PVT 续喂水位：缓冲低于该值即追加下一段</summary>
        public int PVTRefillThreshold { get; set; }

        /// <summary>PVT 平滑停止时间（秒）</summary>
        public double PVTSmoothStopTime { get; set; }

        /// <summary>焊缝跟踪最大跟随速度（工程单位/秒）</summary>
        public double TrackMaxVel { get; set; }

        /// <summary>焊缝跟踪最大加速度（工程单位/秒平方）</summary>
        public double TrackMaxAcc { get; set; }

        /// <summary>跟踪前馈补偿时间（秒），用于抵消通信与机械滞后</summary>
        public double TrackFeedForwardSec { get; set; }

        /// <summary>跟踪位置死区：偏差小于该值不产生新点</summary>
        public double TrackDeadZone { get; set; }

        // 线激光标定参数归 WeldParamConfig，本类不承载，避免双数据源

        /// <summary>焊接头从站配置（万顺兴 ScanCut）</summary>
        public WeldHeadSlaveConfig WeldHeadSlave { get; private set; }

        /// <summary>焊缝跟踪轴名，初始化后按名取轴并注入构造器</summary>
        public string SeamTrackAxisName { get; set; }

        /// <summary>TA（送丝力矩电机）轴名，按此名取轴做力矩输出</summary>
        public string TaAxisName { get; set; }

        /// <summary>TA 扭矩回零参数组（ROOT/TA_HOME 唯一真相源）</summary>
        public DeltaTaHomeConfig TaHome { get; private set; }

        /// <summary>流程等待超时（ROOT/FLOW_TIMEOUT 的唯一持有者；运行期由 <see cref="FlowTimeoutSettings"/> 发布）</summary>
        public FlowTimeoutConfig FlowTimeouts { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建配置并填入默认参数</summary>
        public DeltaMotionConfig()
        {
            ConflictingDeviceKeys = new List<string>();
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

            SeamTrackAxisName = "Y向电机";
            TaAxisName = "送丝力矩电机";
            TaHome = new DeltaTaHomeConfig();
            FlowTimeouts = new FlowTimeoutConfig();

            WeldHeadSlave = new WeldHeadSlaveConfig();
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

        /// <summary>保存前记录将被改名的旧键（不静默）</summary>
        private void LogLegacyKeyRename()
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                ConfigFileManager.ModuleRelativeDir(ConfigModule), ConfigFileName + ".xml");
            if (!File.Exists(fullPath)) return;

            try
            {
                XDocument document = XDocument.Load(fullPath);
                int count = 0;
                foreach (XElement element in document.Descendants())
                {
                    string name = element.Name.LocalName;
                    if (string.Equals(name, "MIN_VEL", StringComparison.Ordinal)
                        || string.Equals(name, "MAX_VEL", StringComparison.Ordinal)
                        || string.Equals(name, "STOP_VEL", StringComparison.Ordinal)
                        || string.Equals(name, "HOME_MAX_VEL", StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                if (count > 0)
                {
                    Log("保存将旧键名重命名为新键名，涉及 " + count + " 项"
                        + "（MIN_VEL→STR_VEL / MAX_VEL→CONST_VEL / STOP_VEL→END_VEL / HOME_MAX_VEL→HOME_FIRST_VEL），旧文件已备份",
                        MessageLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log("旧键名检查失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>读取双精度配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static double ReadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : def;
        }

        /// <summary>读取整数配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static int ReadInt(XdocumentReaderWriter reader, string path, int def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(txt, out int val) ? val : def;
        }

        /// <summary>读取无符号短整型配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static ushort ReadUShort(XdocumentReaderWriter reader, string path, ushort def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return ushort.TryParse(txt, out ushort val) ? val : def;
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

        /// <summary>读取焊接头启用开关</summary>

        /// <param name="reader">配置读写器</param>
        /// <returns>启用返回 true</returns>
        private bool ReadWeldHeadEnabled(XdocumentReaderWriter reader)
        {
            string path = SlavePath + "/ENABLED";
            XElement node = reader.GetElementByPath(path);
            if (node == null)
            {
                Log("焊接头从站 ENABLED 键缺失，按未启用处理", MessageLevel.Info);
                return false;
            }

            string txt = (node.Value ?? string.Empty).Trim().ToUpperInvariant();
            if (txt == "TRUE" || txt == "1") return true;
            if (txt == "FALSE" || txt == "0") return false;

            Log("焊接头从站 ENABLED 值无法解析，按未启用处理", MessageLevel.Info);
            return false;
        }

        /// <summary>读取十六进制无符号整数配置，支持 0x 前缀</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static uint ReadHexUInt(XdocumentReaderWriter reader, string path, uint def)
        {
            string txt = reader.GetElementValueByPath(path, string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", def));
            if (string.IsNullOrEmpty(txt)) return def;
            string body = txt.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? txt.Trim().Substring(2) : txt.Trim();
            return uint.TryParse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint val) ? val : def;
        }

        /// <summary>按固定小数位写出文本</summary>
        /// <param name="value">待写出数值</param>
        /// <returns>不变文化下的文本</returns>
        private static string Text(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>读取流程等待超时并发布运行期快照</summary>

        /// <param name="reader">配置读写器</param>
        /// <param name="writeLog">是否输出生效值日志；预览读传 false</param>
        private void LoadFlowTimeouts(XdocumentReaderWriter reader, bool writeLog)
        {
            FlowTimeoutConfig cfg = new FlowTimeoutConfig();
            cfg.InstructionDetectSeconds = ReadDouble(reader, FlowTimeoutPath + "/INSTRUCTION_DETECT_SECONDS", cfg.InstructionDetectSeconds);
            cfg.InstructionSampleMs = ReadInt(reader, FlowTimeoutPath + "/INSTRUCTION_SAMPLE_MS", cfg.InstructionSampleMs);
            cfg.InstructionStableSamples = ReadInt(reader, FlowTimeoutPath + "/INSTRUCTION_STABLE_SAMPLES", cfg.InstructionStableSamples);
            cfg.InstructionTorqueSamples = ReadInt(reader, FlowTimeoutPath + "/INSTRUCTION_TORQUE_SAMPLES", cfg.InstructionTorqueSamples);
            cfg.MoveDoneSeconds = ReadInt(reader, FlowTimeoutPath + "/MOVE_DONE_SECONDS", cfg.MoveDoneSeconds);
            cfg.HomeDoneSeconds = ReadInt(reader, FlowTimeoutPath + "/HOME_DONE_SECONDS", cfg.HomeDoneSeconds);
            cfg.ServoOnSeconds = ReadInt(reader, FlowTimeoutPath + "/SERVO_ON_SECONDS", cfg.ServoOnSeconds);
            cfg.AxisPollMs = ReadInt(reader, FlowTimeoutPath + "/AXIS_POLL_MS", cfg.AxisPollMs);
            cfg.StopSettleMs = ReadInt(reader, FlowTimeoutPath + "/STOP_SETTLE_MS", cfg.StopSettleMs);
            cfg.TorqueEntryStabilizeSeconds = ReadDouble(reader, FlowTimeoutPath + "/TORQUE_ENTRY_STABILIZE_SECONDS", cfg.TorqueEntryStabilizeSeconds);
            cfg.PositionReachNoChangeSeconds = ReadDouble(reader, FlowTimeoutPath + "/ZERO_POSE_NO_CHANGE_SECONDS", cfg.PositionReachNoChangeSeconds);
            cfg.PositionReachMinSeconds = ReadDouble(reader, FlowTimeoutPath + "/ZERO_POSE_MIN_SECONDS", cfg.PositionReachMinSeconds);
            cfg.PositionReachMaxSeconds = ReadDouble(reader, FlowTimeoutPath + "/ZERO_POSE_MAX_SECONDS", cfg.PositionReachMaxSeconds);
            cfg.PositionReachResultWaitSeconds = ReadDouble(reader, FlowTimeoutPath + "/ZERO_POSE_RESULT_WAIT_SECONDS", cfg.PositionReachResultWaitSeconds);
            cfg.SdoPollMs = ReadInt(reader, FlowTimeoutPath + "/SDO_POLL_MS", cfg.SdoPollMs);
            cfg.SdoPollMaxCount = ReadInt(reader, FlowTimeoutPath + "/SDO_POLL_MAX_COUNT", cfg.SdoPollMaxCount);
            cfg.SdoPreDelayMs = ReadInt(reader, FlowTimeoutPath + "/SDO_PRE_DELAY_MS", cfg.SdoPreDelayMs);
            cfg.SdoMailboxRetryMs = ReadInt(reader, FlowTimeoutPath + "/SDO_MAILBOX_RETRY_MS", cfg.SdoMailboxRetryMs);
            cfg.MasterInitSeconds = ReadInt(reader, FlowTimeoutPath + "/MASTER_INIT_SECONDS", cfg.MasterInitSeconds);
            cfg.MasterInitPollMs = ReadInt(reader, FlowTimeoutPath + "/MASTER_INIT_POLL_MS", cfg.MasterInitPollMs);
            cfg.CascadeConnectMs = ReadInt(reader, FlowTimeoutPath + "/CASCADE_CONNECT_MS", cfg.CascadeConnectMs);
            cfg.CameraAcquireSeconds = ReadDouble(reader, FlowTimeoutPath + "/CAMERA_ACQUIRE_SECONDS", cfg.CameraAcquireSeconds);
            cfg.RecordFlushSeconds = ReadDouble(reader, FlowTimeoutPath + "/RECORD_FLUSH_SECONDS", cfg.RecordFlushSeconds);
            cfg.LineLaserCallbackMs = ReadInt(reader, FlowTimeoutPath + "/LINE_LASER_CALLBACK_MS", cfg.LineLaserCallbackMs);
            cfg.ModbusConnectMs = ReadInt(reader, FlowTimeoutPath + "/MODBUS_CONNECT_MS", cfg.ModbusConnectMs);
            cfg.ModbusReadWriteMs = ReadInt(reader, FlowTimeoutPath + "/MODBUS_READ_WRITE_MS", cfg.ModbusReadWriteMs);
            cfg.UiRefreshMs = ReadInt(reader, FlowTimeoutPath + "/UI_REFRESH_MS", cfg.UiRefreshMs);
            // 值域校验：非法值回退缺省并逐项告警
            cfg.Normalize(delegate (string message)
            {
                Log("流程超时配置 " + message, MessageLevel.Warning);
            });

            FlowTimeouts = cfg;
            FlowTimeoutSettings.Publish(cfg);
            DeviceWaitSettings.Publish(cfg.CameraAcquireSeconds, cfg.RecordFlushSeconds,
                cfg.LineLaserCallbackMs, cfg.ModbusConnectMs, cfg.ModbusReadWriteMs);

            // 打印实际生效值（排障用，降 Debug 避免刷屏）；预览读不打印，避免与权威加载重复
            if (writeLog)
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    "流程超时生效 指令检测 {0:F1}s 采样 {1}ms 运动到位 {2}s 回零 {3}s 伺服 {4}s 零位 {5:F1}~{6:F1}s 主站初始化 {7}s",
                    cfg.InstructionDetectSeconds, cfg.InstructionSampleMs, cfg.MoveDoneSeconds,
                    cfg.HomeDoneSeconds, cfg.ServoOnSeconds, cfg.PositionReachMinSeconds, cfg.PositionReachMaxSeconds,
                    cfg.MasterInitSeconds), MessageLevel.Debug);
                Log(string.Format(CultureInfo.InvariantCulture,
                    "设备侧等待生效 相机 {0:F1}s 落盘 {1:F1}s 线激光回调 {2}ms Modbus 连接 {3}ms 读写 {4}ms",
                    cfg.CameraAcquireSeconds, cfg.RecordFlushSeconds, cfg.LineLaserCallbackMs,
                    cfg.ModbusConnectMs, cfg.ModbusReadWriteMs), MessageLevel.Debug);
            }
        }

        /// <summary>读取单轴配置</summary>
        /// <param name="element">AXIS 元素</param>
        /// <returns>轴配置</returns>
        private static DeltaAxisConfig ReadAxis(XElement element)
        {
            string axisName = ChildValue(element, "AXIS_NAME", string.Empty);
            // 力矩类轴（TA/力矩/TORQUE）缺省走力矩模式，其余位置模式。
            // 扭矩模式以 XML 显式值为准
            string torqueDefault = (axisName.IndexOf("力矩", StringComparison.Ordinal) >= 0
                || axisName.ToUpperInvariant().IndexOf("TORQUE", StringComparison.Ordinal) >= 0
                || axisName.ToUpperInvariant().StartsWith("TA", StringComparison.Ordinal)) ? "TRUE" : "FALSE";
            DeltaAxisConfig axis = new DeltaAxisConfig
            {
                AxisName = axisName,
                NodeId = ParseUShort(ChildValue(element, "NODE_ID", "0")),
                SlotNo = ParseUShort(ChildValue(element, "SLOT_NO", "0")),
                Enabled = ChildValue(element, "ENABLED", "TRUE").ToUpper() == "TRUE",
                Equiv = ParseDouble(ChildValueAny(element, "0.001", "EQUIV")),
                StrVel = ParseDouble(ChildValueAny(element, "0", "STR_VEL", "MIN_VEL")),
                ConstVel = ParseDouble(ChildValueAny(element, "100", "CONST_VEL", "MAX_VEL")),
                AccTime = ParseDouble(ChildValueAny(element, "0.1", "ACC_TIME")),
                DecTime = ParseDouble(ChildValueAny(element, "0.1", "DEC_TIME")),
                STime = ParseDouble(ChildValueAny(element, "0.01", "S_TIME")),
                EndVel = ParseDouble(ChildValueAny(element, "0", "END_VEL", "STOP_VEL")),
                HomeMode = ParseShort(ChildValueAny(element, "0", "HOME_MODE")),
                HomeOffset = ParseDouble(ChildValueAny(element, "0", "HOME_OFFSET")),
                HomeFirstVel = ParseDouble(ChildValueAny(element, "20", "HOME_FIRST_VEL", "HOME_MAX_VEL")),
                HomeSecondVel = ParseDouble(ChildValueAny(element, "5", "HOME_SECOND_VEL")),
                HomeAcc = ParseDouble(ChildValueAny(element, "100", "HOME_ACC")),
                PositiveLimit = ParseDouble(ChildValueAny(element, "0", "POSITIVE_LIMIT")),
                NegativeLimit = ParseDouble(ChildValueAny(element, "0", "NEGATIVE_LIMIT")),
                SoftLimitMode = ParseUShort(ChildValueAny(element, "0", "SOFT_LIMIT_MODE")),
                TorqueModeEnabled = ChildValueAny(element, torqueDefault, "TORQUE_MODE").ToUpper() == "TRUE",
                TorqueTarget = unchecked((short)ParseUShort(ChildValueAny(element, "0", "TORQUE_TARGET"))),
                TorqueSlope = ParseUShort(ChildValueAny(element, "0", "TORQUE_SLOPE"))
            };
            return axis;
        }

        /// <summary>读取 TA 扭矩回零参数组并镜像回 TA 的 AXIS 节点</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="writeLog">是否输出加载结论日志；预览读传 false</param>
        private void LoadTaHome(XdocumentReaderWriter reader, bool writeLog)
        {
            DeltaAxisConfig taAxis = GetAxis(TaAxisName);
            string seedMode = reader.GetElementValueByPath(TaHomePath + "/TA_HOME_MODE", string.Empty);
            if (string.IsNullOrEmpty(seedMode))
            {
                TaHome.CopyFrom(taAxis);
                if (writeLog) Log("TA 回零参数组 ROOT/TA_HOME 未配置，已从 AXIS 既有回零参数 seed（旧文件兼容）");
            }
            else
            {
                if (writeLog) Log("TA 回零参数组 ROOT/TA_HOME 已配置，按本节点读取（不取 AXIS 通用值）", MessageLevel.Debug);
                TaHome.HomeMode = (short)ReadInt(reader, TaHomePath + "/TA_HOME_MODE", TaHome.HomeMode);
                TaHome.HomeFirstVel = ReadDouble(reader, TaHomePath + "/TA_HOME_FIRST_VEL", TaHome.HomeFirstVel);
                TaHome.HomeSecondVel = ReadDouble(reader, TaHomePath + "/TA_HOME_SECOND_VEL", TaHome.HomeSecondVel);
                TaHome.HomeAcc = ReadDouble(reader, TaHomePath + "/TA_HOME_ACC", TaHome.HomeAcc);
                // 工作零位：缺键保留默认值，不覆盖现场现值
                TaHome.WorkOriginMm =
                    ReadDouble(reader, TaHomePath + "/TA_HOME_ZERO_POSE_MM", TaHome.WorkOriginMm);
                // 速度/容差旧键已废弃，读出即忽略
                // 100% 力量基准（力→千分比换算），缺键默认 37N
                TaHome.Force100PctN =
                    ReadDouble(reader, TaHomePath + "/TA_FORCE_100PCT_N", TaHome.Force100PctN);
                // 扭矩上限：CSP 与 CST 共用，缺键取 100% 基准
                TaHome.TorqueLimitN =
                    ReadDouble(reader, TaHomePath + "/TA_TORQUE_LIMIT_N", TaHome.TorqueLimitN);
                // 扭矩方向缺键默认 +1；仅 -1 有效，其余按 +1 并记键存在
                string directionRaw = reader.GetElementValueByPath(TaHomePath + "/TA_TORQUE_DIRECTION", "");
                TaHome.TorqueDirectionKeyPresent = directionRaw.Trim().Length > 0;
                TaHome.TorqueDirection = directionRaw.Trim() == "-1" ? -1 : 1;
            }
            NormalizeTaTorqueLimit();
            TaHome.CopyTo(taAxis);
        }

        /// <summary>扭矩上限值域兜底</summary>
        private void NormalizeTaTorqueLimit()
        {
            double basis = TaHome.Force100PctN > 0.0 ? TaHome.Force100PctN : 37.0;
            double raw = TaHome.TorqueLimitN;
            if (!(raw > 0.0))
            {
                TaHome.TorqueLimitN = basis;
                Log(string.Format("TA 扭矩上限非法（{0}N，须 > 0），已按 100% 力量基准 {1:F1}N 兜底（不降额）",
                    raw, basis), MessageLevel.Warning);
            }
            else if (raw > basis)
            {
                TaHome.TorqueLimitN = basis;
                Log(string.Format("TA 扭矩上限 {0:F2}N 超过 100% 力量基准 {1:F1}N，已压回基准（驱动器上限 1000‰）",
                    raw, basis), MessageLevel.Warning);
            }
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

        /// <summary>读取单气缸配置</summary>
        /// <param name="element">CYLINDER 元素</param>
        /// <returns>气缸配置</returns>
        private static DeltaCylinderConfig ReadCylinder(XElement element)
        {
            DeltaCylinderConfig cyl = new DeltaCylinderConfig
            {
                CylinderName = ChildValue(element, "CYLINDER_NAME", string.Empty),
                StretchSensorName = ChildValue(element, "STRETCH_SENSOR", string.Empty),
                RetractSensorName = ChildValue(element, "RETRACT_SENSOR", string.Empty),
                CenterSensorName = ChildValue(element, "CENTER_SENSOR", string.Empty),
                StretchOutputName = ChildValue(element, "STRETCH_OUTPUT", string.Empty),
                RetractOutputName = ChildValue(element, "RETRACT_OUTPUT", string.Empty),
                StretchActionName = ChildValue(element, "STRETCH_ACTION_NAME", string.Empty),
                RetractActionName = ChildValue(element, "RETRACT_ACTION_NAME", string.Empty)
            };
            return cyl;
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

        /// <summary>写出单气缸配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="parent">父元素</param>
        /// <param name="cyl">气缸配置</param>
        private static void WriteCylinder(XdocumentReaderWriter reader, XElement parent, DeltaCylinderConfig cyl)
        {
            XElement element = reader.AddElement(parent, "CYLINDER");
            reader.AddElement(element, "CYLINDER_NAME", cyl.CylinderName);
            reader.AddElement(element, "STRETCH_SENSOR", cyl.StretchSensorName);
            reader.AddElement(element, "RETRACT_SENSOR", cyl.RetractSensorName);
            reader.AddElement(element, "CENTER_SENSOR", cyl.CenterSensorName);
            reader.AddElement(element, "STRETCH_OUTPUT", cyl.StretchOutputName);
            reader.AddElement(element, "RETRACT_OUTPUT", cyl.RetractOutputName);
            reader.AddElement(element, "STRETCH_ACTION_NAME", cyl.StretchActionName);
            reader.AddElement(element, "RETRACT_ACTION_NAME", cyl.RetractActionName);
        }

        /// <summary>取子元素文本</summary>
        /// <param name="parent">父元素</param>
        /// <param name="name">子元素名</param>
        /// <param name="def">缺失时的默认值</param>
        /// <returns>元素值</returns>
        private static string ChildValue(XElement parent, string name, string def)
        {
            XElement child = parent?.Element(name);
            return child == null ? def : child.Value;
        }

        /// <summary>按候选名依次取值，命中第一个存在的子元素</summary>
        /// <param name="parent">父元素</param>
        /// <param name="def">全部缺失时的默认值</param>
        /// <param name="names">候选子元素名（按顺序优先）</param>
        /// <returns>子元素文本或默认值</returns>
        private static string ChildValueAny(XElement parent, string def, params string[] names)
        {
            if (parent == null) return def;
            foreach (string n in names)
            {
                XElement child = parent.Element(n);
                if (child != null) return child.Value;
            }
            return def;
        }

        /// <summary>解析双精度，失败返回 0</summary>
        /// <param name="txt">文本</param>
        /// <returns>数值</returns>
        private static double ParseDouble(string txt)
        {
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : 0.0;
        }

        /// <summary>解析无符号短整型，失败返回 0</summary>
        /// <param name="txt">文本</param>
        /// <returns>数值</returns>
        private static ushort ParseUShort(string txt)
        {
            return ushort.TryParse(txt, out ushort val) ? val : (ushort)0;
        }

        /// <summary>解析有符号短整型</summary>

        /// <param name="txt">文本</param>
        /// <returns>数值</returns>
        private static short ParseShort(string txt)
        {
            return short.TryParse(txt, out short val) ? val : (short)0;
        }

        /// <summary>解析整数，失败返回 0</summary>
        /// <param name="txt">文本</param>
        /// <returns>数值</returns>
        private static int ParseInt(string txt)
        {
            return int.TryParse(txt, out int val) ? val : 0;
        }
        #endregion

        #region 公共函数

        private static DeltaIOConfig FindIO(List<DeltaIOConfig> list, string ioName)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IOName == ioName)
                    return list[i];
            }
            return null;
        }

        /// <summary>把当前内存配置写成完整节点树</summary>

        /// <param name="reader">目标读写器（已 NewXdocument）</param>
        private void WriteConfigNodes(XdocumentReaderWriter reader)
        {
            XElement master = reader.AddElement("MASTER");
            reader.AddElement(master, "CARD_NO", CardNo.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(master, "INITIAL_TIMEOUT_MS", InitialTimeoutMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(master, "INITIAL_POLL_INTERVAL_MS",
                InitialPollIntervalMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(master, "WATCHDOG_ENABLE", WatchDogEnable ? "TRUE" : "FALSE");
            reader.AddElement(master, "WATCHDOG_SECONDS", Text(WatchDogSeconds));

            // TA 回零参数以 ROOT/TA_HOME 为唯一真相源；保存前镜像回 TA 的 AXIS 节点（只改 TA 节点，Y 轴零改动）
            TaHome.CopyTo(GetAxis(TaAxisName));

            XElement axes = reader.AddElement("AXES");
            foreach (DeltaAxisConfig axis in _axes)
            {
                XElement element = reader.AddElement(axes, "AXIS");
                reader.AddElement(element, "AXIS_NAME", axis.AxisName);
                reader.AddElement(element, "NODE_ID", axis.NodeId.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "SLOT_NO", axis.SlotNo.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "ENABLED", axis.Enabled ? "TRUE" : "FALSE");
                reader.AddElement(element, "EQUIV", Text(axis.Equiv));
                reader.AddElement(element, "STR_VEL", Text(axis.StrVel));
                reader.AddElement(element, "CONST_VEL", Text(axis.ConstVel));
                reader.AddElement(element, "ACC_TIME", Text(axis.AccTime));
                reader.AddElement(element, "DEC_TIME", Text(axis.DecTime));
                reader.AddElement(element, "S_TIME", Text(axis.STime));
                reader.AddElement(element, "END_VEL", Text(axis.EndVel));
                reader.AddElement(element, "HOME_MODE", axis.HomeMode.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "HOME_OFFSET", Text(axis.HomeOffset));
                reader.AddElement(element, "HOME_FIRST_VEL", Text(axis.HomeFirstVel));
                reader.AddElement(element, "HOME_SECOND_VEL", Text(axis.HomeSecondVel));
                reader.AddElement(element, "HOME_ACC", Text(axis.HomeAcc));
                reader.AddElement(element, "POSITIVE_LIMIT", Text(axis.PositiveLimit));
                reader.AddElement(element, "NEGATIVE_LIMIT", Text(axis.NegativeLimit));
                reader.AddElement(element, "SOFT_LIMIT_MODE", axis.SoftLimitMode.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "TORQUE_MODE", axis.TorqueModeEnabled ? "TRUE" : "FALSE");
                reader.AddElement(element, "TORQUE_TARGET", axis.TorqueTarget.ToString(CultureInfo.InvariantCulture));
                reader.AddElement(element, "TORQUE_SLOPE", axis.TorqueSlope.ToString(CultureInfo.InvariantCulture));
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

            XElement cylinders = reader.AddElement("CYLINDERS");
            foreach (DeltaCylinderConfig cyl in _cylinders)
            {
                WriteCylinder(reader, cylinders, cyl);
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

            XElement slave = reader.AddElement("WELD_HEAD_SLAVE");
            reader.AddElement(slave, "NODE_ID", WeldHeadSlave.NodeId.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(slave, "ENABLED", WeldHeadSlave.Enabled ? "TRUE" : "FALSE");
            reader.AddElement(slave, "VENDOR_ID", string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", WeldHeadSlave.VendorId));
            reader.AddElement(slave, "PRODUCT_CODE", string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", WeldHeadSlave.ProductCode));
            reader.AddElement(slave, "REVISION_NO", string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", WeldHeadSlave.RevisionNo));

            XElement seamTrack = reader.AddElement("SEAM_TRACK");
            reader.AddElement(seamTrack, "SEAM_TRACK_AXIS", SeamTrackAxisName);

            XElement ta = reader.AddElement("TA");
            reader.AddElement(ta, "TA_AXIS_NAME", TaAxisName);

            // TA 扭矩回零参数独立节点（TA 区唯一真相源；Y 轴仍读 AXIS 原参数）
            XElement taHome = reader.AddElement("TA_HOME");
            reader.AddElement(taHome, "TA_HOME_MODE", TaHome.HomeMode.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(taHome, "TA_HOME_FIRST_VEL", Text(TaHome.HomeFirstVel));
            reader.AddElement(taHome, "TA_HOME_SECOND_VEL", Text(TaHome.HomeSecondVel));
            reader.AddElement(taHome, "TA_HOME_ACC", Text(TaHome.HomeAcc));
            reader.AddElement(taHome, "TA_HOME_ZERO_POSE_MM", Text(TaHome.WorkOriginMm));
            reader.AddElement(taHome, "TA_FORCE_100PCT_N", Text(TaHome.Force100PctN));
            reader.AddElement(taHome, "TA_TORQUE_LIMIT_N", Text(TaHome.TorqueLimitN));
            reader.AddElement(taHome, "TA_TORQUE_DIRECTION", TaHome.TorqueDirection.ToString(CultureInfo.InvariantCulture));

            // 流程等待超时（缺省值单一来源 = FlowTimeoutConfig 构造函数）
            FlowTimeoutConfig flow = FlowTimeouts ?? new FlowTimeoutConfig();
            XElement flowNode = reader.AddElement("FLOW_TIMEOUT");
            reader.AddElement(flowNode, "INSTRUCTION_DETECT_SECONDS", Text(flow.InstructionDetectSeconds));
            reader.AddElement(flowNode, "INSTRUCTION_SAMPLE_MS", flow.InstructionSampleMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "INSTRUCTION_STABLE_SAMPLES", flow.InstructionStableSamples.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "INSTRUCTION_TORQUE_SAMPLES", flow.InstructionTorqueSamples.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "MOVE_DONE_SECONDS", flow.MoveDoneSeconds.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "HOME_DONE_SECONDS", flow.HomeDoneSeconds.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "SERVO_ON_SECONDS", flow.ServoOnSeconds.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "AXIS_POLL_MS", flow.AxisPollMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "STOP_SETTLE_MS", flow.StopSettleMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "TORQUE_ENTRY_STABILIZE_SECONDS", Text(flow.TorqueEntryStabilizeSeconds));
            reader.AddElement(flowNode, "ZERO_POSE_NO_CHANGE_SECONDS", Text(flow.PositionReachNoChangeSeconds));
            reader.AddElement(flowNode, "ZERO_POSE_MIN_SECONDS", Text(flow.PositionReachMinSeconds));
            reader.AddElement(flowNode, "ZERO_POSE_MAX_SECONDS", Text(flow.PositionReachMaxSeconds));
            reader.AddElement(flowNode, "ZERO_POSE_RESULT_WAIT_SECONDS", Text(flow.PositionReachResultWaitSeconds));
            reader.AddElement(flowNode, "SDO_POLL_MS", flow.SdoPollMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "SDO_POLL_MAX_COUNT", flow.SdoPollMaxCount.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "SDO_PRE_DELAY_MS", flow.SdoPreDelayMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "SDO_MAILBOX_RETRY_MS", flow.SdoMailboxRetryMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "MASTER_INIT_SECONDS", flow.MasterInitSeconds.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "MASTER_INIT_POLL_MS", flow.MasterInitPollMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "CASCADE_CONNECT_MS", flow.CascadeConnectMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "CAMERA_ACQUIRE_SECONDS", Text(flow.CameraAcquireSeconds));
            reader.AddElement(flowNode, "RECORD_FLUSH_SECONDS", Text(flow.RecordFlushSeconds));
            reader.AddElement(flowNode, "LINE_LASER_CALLBACK_MS", flow.LineLaserCallbackMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "MODBUS_CONNECT_MS", flow.ModbusConnectMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "MODBUS_READ_WRITE_MS", flow.ModbusReadWriteMs.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(flowNode, "UI_REFRESH_MS", flow.UiRefreshMs.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>加载配置</summary>
        public void LoadConfig()
        {
            LoadConfig(true);
        }

        /// <summary>加载配置</summary>
        /// <param name="writeLog">是否输出「配置加载完成」日志；界面预览读盘传 false，避免反复刷出旧配置结论</param>
        public void LoadConfig(bool writeLog)
        {
            // 权威加载（writeLog=true）允许补缺；预览读（false）一律只读不写
            LoadConfig(writeLog, writeLog);
        }

        /// <summary>加载配置（可显式控制补缺写盘）</summary>
        /// <param name="writeLog">是否输出加载结论日志</param>
        /// <param name="allowHeal">是否允许缺键补写；预览读、差异审计读必须传 false</param>
        public void LoadConfig(bool writeLog, bool allowHeal)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                ConfigFileManager.ModuleRelativeDir(ConfigModule), ConfigFileName + ".xml");
            bool fileMissing = !File.Exists(fullPath);
            if (fileMissing && allowHeal)
            {
                Log("配置缺失，待方向迁移判定后生成默认配置 " + ConfigFileName + ".xml", MessageLevel.Warning);
            }

            // 读取一律 createIfMissing=false：缺失时保持默认
            XdocumentReaderWriter reader = ConfigFileManager.Instance.GetModuleXml(ConfigModule, ConfigFileName, false).Raw;
            if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
            {
                if (writeLog) Log("配置为空，保留默认参数", MessageLevel.Warning);
                FlowTimeoutSettings.Publish(FlowTimeouts);   // 无配置文件：缺省超时立即生效
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
            // 轴参数组自证：Y 与 TA 各取自身 AXIS 节点
            // 预览读不打印参数日志
            if (writeLog)
            {
                foreach (DeltaAxisConfig axis in _axes)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        "轴 {0} 参数已配置 来源 ROOT/AXES/AXIS 回零模式 {1} 当量 {2}",
                        axis.AxisName, axis.HomeMode, axis.Equiv), MessageLevel.Debug);
                }
                if (_axes.Count == 0) Log("轴参数组 ROOT/AXES/AXIS 未配置", MessageLevel.Debug);
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

            _cylinders.Clear();
            foreach (XElement element in reader.GetChildElements(CylindersPath, "CYLINDER"))
            {
                _cylinders.Add(ReadCylinder(element));
            }

            PVTTimeMode = (PVTTimeMode)ReadInt(reader, PVTPath + "/TIME_MODE", (int)PVTTimeMode);
            PVTBatchSize = ReadInt(reader, PVTPath + "/BATCH_SIZE", PVTBatchSize);
            PVTRefillThreshold = ReadInt(reader, PVTPath + "/REFILL_THRESHOLD", PVTRefillThreshold);
            PVTSmoothStopTime = ReadDouble(reader, PVTPath + "/SMOOTH_STOP_TIME", PVTSmoothStopTime);
            TrackMaxVel = ReadDouble(reader, PVTPath + "/TRACK_MAX_VEL", TrackMaxVel);
            TrackMaxAcc = ReadDouble(reader, PVTPath + "/TRACK_MAX_ACC", TrackMaxAcc);
            TrackFeedForwardSec = ReadDouble(reader, PVTPath + "/TRACK_FEED_FORWARD_SEC", TrackFeedForwardSec);
            TrackDeadZone = ReadDouble(reader, PVTPath + "/TRACK_DEAD_ZONE", TrackDeadZone);

            WeldHeadSlave.NodeId = ReadUShort(reader, SlavePath + "/NODE_ID", WeldHeadSlave.NodeId);
            WeldHeadSlave.Enabled = ReadWeldHeadEnabled(reader);
            WeldHeadSlave.VendorId = ReadHexUInt(reader, SlavePath + "/VENDOR_ID", WeldHeadSlave.VendorId);
            WeldHeadSlave.ProductCode = ReadHexUInt(reader, SlavePath + "/PRODUCT_CODE", WeldHeadSlave.ProductCode);
            WeldHeadSlave.RevisionNo = ReadHexUInt(reader, SlavePath + "/REVISION_NO", WeldHeadSlave.RevisionNo);

            SeamTrackAxisName = reader.GetElementValueByPath(SeamTrackPath + "/SEAM_TRACK_AXIS", SeamTrackAxisName);
            TaAxisName = reader.GetElementValueByPath(TaPath + "/TA_AXIS_NAME", TaAxisName);
            LoadTaHome(reader, writeLog);
            LoadFlowTimeouts(reader, writeLog);

            // 方向键已存在 → 方向已定，可一次补其它缺键；
            // 缺省则推迟到迁移后补缺，避免 +1 被当现场值
            if (allowHeal && !fileMissing && TaHome.TorqueDirectionKeyPresent)
            {
                SelfHealMissingKeys(true, true);
            }

            if (writeLog)
            {
                // 只保留影响判断的两项；全量罗列降 Debug（排障仍可查）
                Log(string.Format("运动配置加载完成 卡号 {0} 轴 {1} 焊接头从站 {2}",
                    CardNo, _axes.Count,
                    WeldHeadSlave.Enabled ? "启用 NodeId " + WeldHeadSlave.NodeId : "未启用"),
                    MessageLevel.Debug);
                Log(string.Format("配置明细 输入 {0} 个 输出 {1} 个 气缸 {2} 个",
                    _inputs.Count, _outputs.Count, _cylinders.Count), MessageLevel.Debug);
            }

            ValidateNodeIdConflicts();
        }

        /// <summary>从站节点号撞号校验</summary>
        private void ValidateNodeIdConflicts()
        {
            ConflictingDeviceKeys.Clear();

            // (节点号, 槽号) → 首个占用者（后遇者判为冲突方）
            Dictionary<string, string> occupied = new Dictionary<string, string>();

            foreach (DeltaAxisConfig axis in _axes)
            {
                if (!axis.Enabled) continue;
                RegisterNodeOwner(occupied, axis.NodeId, axis.SlotNo,
                    AxisOwnerKey(axis.AxisName), "轴 " + axis.AxisName);
            }

            if (WeldHeadSlave.Enabled)
            {
                RegisterNodeOwner(occupied, WeldHeadSlave.NodeId, 0,
                    WeldHeadOwnerKey, "焊接头从站");
            }
        }

        /// <summary>登记节点占用者；已被他人占用即记冲突报警</summary>
        /// <param name="occupied">占用键 → 占用者名称</param>
        /// <param name="nodeId">节点号</param>
        /// <param name="slotNo">槽号</param>
        /// <param name="ownerKey">设备键（冲突方登记用）</param>
        /// <param name="ownerLabel">占用者展示名（日志用）</param>
        private void RegisterNodeOwner(Dictionary<string, string> occupied, ushort nodeId, ushort slotNo,
            string ownerKey, string ownerLabel)
        {
            string occupyKey = nodeId.ToString(CultureInfo.InvariantCulture) + ":" +
                slotNo.ToString(CultureInfo.InvariantCulture);
            if (occupied.TryGetValue(occupyKey, out string existing))
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    "节点号撞号 {0} 与 {1} 同用节点 {2} 槽 {3}，{1} 不装载",
                    existing, ownerLabel, nodeId, slotNo), MessageLevel.Error);
                if (!ConflictingDeviceKeys.Contains(ownerKey)) ConflictingDeviceKeys.Add(ownerKey);
                return;
            }
            occupied[occupyKey] = ownerLabel;
        }

        /// <summary>保存配置（自行创建备份目录，末尾落盘）</summary>
        public void SaveConfig()
        {
            SaveConfig(string.Empty);
        }

        /// <summary>保存配置（末尾落盘）</summary>
        /// <param name="backupFolder">上层已创建的备份目录；空 = 本方法自行创建并备份。
        /// 非空即批量保存：不再备份，也不打保存结论</param>
        public void SaveConfig(string backupFolder)
        {
            bool bulkSave = !string.IsNullOrEmpty(backupFolder);
            if (!bulkSave)
            {
                string folder = ConfigBackupHelper.CreateBackupFolder();
                ConfigBackupHelper.BackupFile(ConfigFileManager.ModuleRelativeDir(ConfigModule) + "\\" + ConfigFileName + ".xml", folder);
            }
            LogLegacyKeyRename();

            XmlConfigFile file = ConfigFileManager.Instance.GetModuleXml(ConfigModule, ConfigFileName);
            XdocumentReaderWriter reader = file.Raw;
            ConfigFileManager.SaveXmlWithAudit(file, "运动控制配置", "配置变更", () =>
            {
                reader.NewXdocument(RootName, "1.0", "UTF-8");

                WriteConfigNodes(reader);

                reader.SaveXdocument();
            });
            if (!bulkSave)
            {
                // 写盘结果由上层「重载保存完成」统一输出
                Log(string.Format("配置保存完成 卡号 {0} 轴 {1} 个 输入 {2} 个 输出 {3} 个 气缸 {4} 个",
                    CardNo, _axes.Count, _inputs.Count, _outputs.Count, _cylinders.Count));
            }
        }

        /// <summary>补写配置文件缺失的键</summary>
        /// <param name="allowWrite">false = 只检测不写盘</param>
        /// <param name="includeTorqueDirection">是否把 TA_TORQUE_DIRECTION 纳入模板；
        /// 缺省时必须传 false，迁移判定后合并为一次写盘</param>
        public void SelfHealMissingKeys(bool allowWrite, bool includeTorqueDirection)
        {
            XdocumentReaderWriter mirror = new XdocumentReaderWriter();
            mirror.NewXdocument(RootName, "1.0", "UTF-8");
            WriteConfigNodes(mirror);
            XDocument template = mirror.GetXDocument();
            if (template == null || template.Root == null) return;

            if (!includeTorqueDirection)
            {
                XElement taHome = template.Root.Element("TA_HOME");
                if (taHome != null)
                {
                    XElement direction = taHome.Element("TA_TORQUE_DIRECTION");
                    direction?.Remove();
                }
            }

            string fileName = ConfigFileName + ".xml";
            string fullPath = ConfigFileManager.ModuleAbsolutePath(ConfigModule, fileName);
            string relativePath = ConfigFileManager.ModuleRelativeDir(ConfigModule) + "\\" + fileName;
            bool hadDirectionKey = TaHome.TorqueDirectionKeyPresent;
            bool wrote = ConfigSelfHeal.EnsureXml(fullPath, relativePath, fileName, template, allowWrite);
            if (includeTorqueDirection && (hadDirectionKey || wrote))
            {
                TaHome.TorqueDirectionKeyPresent = true;
            }
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

        /// <summary>按名称取气缸配置，未找到返回 null</summary>
        /// <param name="cylinderName">气缸名称</param>
        /// <returns>气缸配置</returns>
        public DeltaCylinderConfig GetCylinder(string cylinderName)
        {
            for (int i = 0; i < _cylinders.Count; i++)
            {
                if (_cylinders[i].CylinderName == cylinderName)
                    return _cylinders[i];
            }
            return null;
        }

        #endregion
    }
}

