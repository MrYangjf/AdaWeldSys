using System;

namespace AdaWeldSystem.LaserWeldHead
{
    /// <summary>
    /// 激光焊接头初始化参数配置
    /// </summary>
    public class LaserWeldHeadConfig
    {
        /// <summary>激光器通讯端口</summary>
        public string LaserComPort { get; set; }

        /// <summary>激光器波特率</summary>
        public int LaserBaudRate { get; set; }

        /// <summary>温度采集间隔（ms）</summary>
        public int TemperaturePollIntervalMs { get; set; }

        /// <summary>准直镜温度阈值（°C）</summary>
        public double CollimatorTempThreshold { get; set; }

        /// <summary>聚焦镜温度阈值（°C）</summary>
        public double FocusLensTempThreshold { get; set; }

        /// <summary>保护镜温度阈值（°C）</summary>
        public double ProtectionLensTempThreshold { get; set; }

        /// <summary>QBH 接口温度阈值（°C）</summary>
        public double QBHInterfaceTempThreshold { get; set; }

        /// <summary>自动对焦电机最大行程（mm）</summary>
        public double AutoFocusMaxTravel { get; set; }

        /// <summary>送丝电机力矩限制（N·m）</summary>
        public double WireFeedTorqueLimit { get; set; }

        public LaserWeldHeadConfig()
        {
            LaserComPort = "COM1";
            LaserBaudRate = 115200;
            TemperaturePollIntervalMs = 1000;
            CollimatorTempThreshold = 60.0;
            FocusLensTempThreshold = 60.0;
            ProtectionLensTempThreshold = 80.0;
            QBHInterfaceTempThreshold = 70.0;
            AutoFocusMaxTravel = 50.0;
            WireFeedTorqueLimit = 5.0;
        }
    }

    /// <summary>
    /// 激光焊接头事件参数
    /// </summary>
    public class LaserWeldHeadEventArgs : EventArgs
    {
        /// <summary>事件类型</summary>
        public LaserWeldHeadEventType EventType { get; set; }

        /// <summary>事件描述</summary>
        public string Description { get; set; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        public LaserWeldHeadEventArgs()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 激光焊接头事件类型
    /// </summary>
    public enum LaserWeldHeadEventType
    {
        LaserOn,
        LaserOff,
        TemperatureWarning,
        TemperatureAlarm,
        SafetyIOTriggered,
        FocusPositionChanged,
        WireFeedPositionChanged,
        InitializationComplete,
        ShutdownComplete
    }

    /// <summary>
    /// 焊接头温度数据
    /// （由 DataModels 迁移而来，原 DeviceStatusModels.LaserHeadTemperatureData）
    /// </summary>
    public class LaserHeadTemperatureData
    {
        /// <summary>准直镜温度（°C）</summary>
        public double CollimatorTemp { get; set; }

        /// <summary>聚焦镜温度（°C）</summary>
        public double FocusLensTemp { get; set; }

        /// <summary>保护镜温度（°C）</summary>
        public double ProtectionLensTemp { get; set; }

        /// <summary>QBH 接口温度（°C）</summary>
        public double QBHInterfaceTemp { get; set; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>温度是否超阈值</summary>
        public bool IsOverThreshold { get; set; }

        /// <summary>超阈值镜片名称</summary>
        public string OverThresholdLens { get; set; }

        public LaserHeadTemperatureData()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 自动对焦协同位置数据
    /// （由 DataModels 迁移而来，原 DeviceStatusModels.AutoFocusPositionData）
    /// </summary>
    public class AutoFocusPositionData
    {
        /// <summary>送丝电机位置（mm）</summary>
        public double WireFeedPos { get; set; }

        /// <summary>对焦电机位置（mm）</summary>
        public double AutoFocusPos { get; set; }

        /// <summary>送丝电机力矩（N·m）</summary>
        public double WireFeedTorque { get; set; }

        /// <summary>是否处于恒压接触状态</summary>
        public bool IsConstantPressure { get; set; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        public AutoFocusPositionData()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 安全 IO 状态
    /// （由 DataModels 迁移而来，原 DeviceStatusModels.SafetyIOStatus）
    /// </summary>
    public class SafetyIOStatus
    {
        /// <summary>急停按钮触发</summary>
        public bool EmergencyStopTriggered { get; set; }

        /// <summary>光栅安全门状态</summary>
        public bool LightCurtainTripped { get; set; }

        /// <summary>激光器互锁状态</summary>
        public bool LaserInterlockActive { get; set; }

        /// <summary>冷却水流量状态</summary>
        public bool CoolingWaterFlowOK { get; set; }

        /// <summary>气路压力状态</summary>
        public bool AirPressureOK { get; set; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>是否有任何安全报警</summary>
        public bool HasAlarm
        {
            get
            {
                return EmergencyStopTriggered || LightCurtainTripped ||
                       LaserInterlockActive || !CoolingWaterFlowOK || !AirPressureOK;
            }
        }

        public SafetyIOStatus()
        {
            CoolingWaterFlowOK = true;
            AirPressureOK = true;
            Timestamp = DateTime.Now;
        }
    }
}