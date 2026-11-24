using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>焊接头台达 EtherCAT 从站（对象字典读写）</summary>
    public class WeldHeadSlaveDevice : IDisposable
    {
        #region 对象字典常量

        private const ushort OdStatus = 0x6000;
        private const ushort OdGalvoRead = 0x6010;
        private const ushort OdMcuVersion = 0x2004;
        private const ushort OdGalvoWrite = 0x7010;
        private const ushort OdThresholdWrite = 0x7020;
        private const ushort OdMotorWrite = 0x7000;
        private const ushort OdMotorRead = 0x6020;

        private const ushort SubTempProtect = 1;
        private const ushort SubTempAmbient = 2;
        private const ushort SubTempRise = 3;
        private const ushort SubPressure = 4;
        private const ushort SubSensorStatus = 11;

        private const ushort SubMotorTarget = 1;
        private const ushort SubMotorEnable = 2;
        private const ushort SubMotorState = 2;
        private const ushort SubMotorCurrent = 3;
        private const ushort SubMotorReady = 4;

        private const ushort SubGalvoPattern = 1;
        private const ushort SubGalvoFrequency = 2;
        private const ushort SubGalvoWidth = 3;
        private const ushort SubGalvoLength = 4;
        private const ushort SubGalvoAngle = 5;
        private const ushort SubGalvoStartAngle = 6;
        private const ushort SubGalvoEndAngle = 7;
        private const ushort SubGalvoXCenter = 8;
        private const ushort SubGalvoYCenter = 9;
        private const ushort SubGalvoXCompression = 10;
        private const ushort SubGalvoYCompression = 11;
        private const ushort SubGalvoTargetSize = 12;
        private const ushort SubGalvoActualSize = 13;
        private const ushort SubGalvoObliqueCorrection = 14;
        private const ushort SubGalvoEnable = 15;
        private const ushort SubDriverAlarm = 16;

        private const ushort SubThresholdTempWarn = 1;
        private const ushort SubThresholdTempAlarm = 2;
        private const ushort SubThresholdRiseWarn = 3;
        private const ushort SubThresholdRiseAlarm = 4;
        private const ushort SubThresholdPressureAlarm = 5;
        private const ushort SubThresholdPressureDelay = 6;
        private const ushort SubLaserStatus = 7;
        private const ushort SubResetConfiguration = 8;

        private const double ScaleTemp = 0.1;
        private const double ScalePressure = 0.01;
        private const double ScaleSize = 0.1;
        private const double ScaleCenter = 0.1;
        private const double ScaleSubAngle = 0.1;

        #endregion

        #region 私有变量

        private const string Tag = "WeldHeadSlaveDevice";

        private bool _disposed;

        private readonly DeltaSlaveDevice _device;
        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #endregion

        #region 公共变量

        /// <summary>是否已初始化（从站握手成功）</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>出光状态（LaserStatus）</summary>
        public bool IsLaserReady { get; private set; }

        /// <summary>最近一次温度与气压数据</summary>
        public LaserHeadTemperatureData CurrentTemperature { get; private set; }

        /// <summary>最近一次报警状态</summary>
        public HeadAlarmStatus CurrentAlarmStatus { get; private set; }

        /// <summary>温度超阈值事件（阈值由 MontionManager 初始化时下发）</summary>
        public event EventHandler<LaserHeadTemperatureData> TemperatureAlarm;

        /// <summary>保护镜温度报警阈值（℃；≤0 表示未配置，不触发）</summary>
        public double TemperatureAlarmThreshold { get; set; }

        /// <summary>温度越阈报警开关</summary>
        public static readonly bool TemperatureAlarmEnabled = false;

        /// <summary>报警触发事件（SensorStatus 位或驱动板报警）</summary>
        public event EventHandler<HeadAlarmStatus> AlarmTriggered;

        /// <summary>电机脉冲当量</summary>
        public double MotorPulsePerMm { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建焊接头从站设备（内部建立从站通道）</summary>
        /// <param name="cardNo">台达主站卡号</param>
        /// <param name="nodeId">焊接头从站号</param>
        public WeldHeadSlaveDevice(ushort cardNo, ushort nodeId)
        {
            _device = new DeltaSlaveDevice(cardNo, nodeId);
            CurrentTemperature = new LaserHeadTemperatureData();
            CurrentAlarmStatus = new HeadAlarmStatus();
            MotorPulsePerMm = WeldHeadConfig.DefaultMotorPulsePerMm;
        }

        #endregion

        #region 公共函数

        /// <summary>初始化焊接头（读版本与状态）</summary>
        /// <returns>从站握手成功返回 true</returns>
        public bool Initialize()
        {
            if (IsInitialized)
            {
                Log("焊接头已初始化，跳过");
                return true;
            }

            uint version = GetMcuVersion();
            if (version == 0)
            {
                Log("焊接头初始化失败 MCU 版本读取失败", MessageLevel.Error);
                return false;
            }
            Log(string.Format("焊接头从站通讯正常 MCU 版本 0x{0:X8}", version));

            ReadTemperature();
            ReadAlarmStatus();
            IsInitialized = true;
            Log("焊接头初始化完成");
            return true;
        }

        /// <summary>关闭焊接头（关光并复位标志）</summary>
        public void Shutdown()
        {
            try
            {
                DisableLaser();
                IsInitialized = false;
                Log("焊接头已关闭");
            }
            catch (Exception ex)
            {
                Log(string.Format("焊接头关闭异常 原因 {0}", ex.Message), MessageLevel.Error);
            }
        }

        /// <summary>读取温度与气压（0x6000 段）</summary>
        /// <returns>焊接头温度数据</returns>
        public LaserHeadTemperatureData ReadTemperature()
        {
            if (!_device.PdoGetScaledInt16(OdStatus, SubTempProtect, ScaleTemp, out double temp) ||
                !_device.PdoGetScaledInt16(OdStatus, SubTempAmbient, ScaleTemp, out double ambient) ||
                !_device.PdoGetScaledInt16(OdStatus, SubTempRise, ScaleTemp, out double rise) ||
                !_device.PdoGetScaledUInt16(OdStatus, SubPressure, ScalePressure, out double pressure) ||
                !_device.PdoGetByte(OdStatus, SubSensorStatus, out byte status))
            {
                Log("温度气压读取失败", MessageLevel.Warning);
                return null;
            }

            LaserHeadTemperatureData data = new LaserHeadTemperatureData
            {
                ProtectiveLensTemp = temp,
                ProtectiveLensAmbientTemp = ambient,
                ProtectiveLensRiseTemp = rise,
                CuttingAirPressure = pressure,
                SensorStatus = status,
                Timestamp = DateTime.Now
            };
            CurrentTemperature = data;
            if (TemperatureAlarmThreshold > 0.0 && data.ProtectiveLensTemp >= TemperatureAlarmThreshold)
            {
                // 关闭只跳过触发，保留事件
                if (TemperatureAlarmEnabled)
                {
                    TemperatureAlarm?.Invoke(this, data);
                }
            }
            return data;
        }

        /// <summary>读取报警状态（位+驱动板+振镜）</summary>
        /// <returns>焊接头报警状态</returns>
        public HeadAlarmStatus ReadAlarmStatus()
        {
            if (!_device.PdoGetByte(OdStatus, SubSensorStatus, out byte status) ||
                !_device.PdoGetBool(OdGalvoRead, SubDriverAlarm, out bool driverAlarm) ||
                !_device.PdoGetBool(OdGalvoRead, SubGalvoEnable, out bool galvoOn))
            {
                Log("报警状态读取失败", MessageLevel.Warning);
                return null;
            }

            HeadAlarmStatus alarm = new HeadAlarmStatus
            {
                ProtectiveLensTempAlarm = (status & 0x01) != 0,
                ProtectiveLensRiseTempAlarm = (status & 0x02) != 0,
                CuttingAirPressureAlarm = (status & 0x04) != 0,
                DriverAlarm = driverAlarm,
                GalvoOn = galvoOn,
                Timestamp = DateTime.Now
            };
            bool hadAlarm = CurrentAlarmStatus != null && CurrentAlarmStatus.HasAlarm;
            CurrentAlarmStatus = alarm;

            if (alarm.HasAlarm && !hadAlarm)
            {
                AlarmTriggered?.Invoke(this, alarm);
                Log("焊接头报警 " + alarm.Describe(), MessageLevel.Error);
            }
            return alarm;
        }

        /// <summary>写入振镜参数（0x7010 段）</summary>
        /// <returns>是否成功。</returns>
        public bool SetGalvoParams(GalvoParams galvo)
        {
            if (galvo == null) return false;
            try
            {
                bool ok = _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoPattern, 1.0, (double)((int)galvo.Pattern))
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoFrequency, 1.0, galvo.Frequency)
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoWidth, ScaleSize, galvo.Width)
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoLength, ScaleSize, galvo.Length)
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoAngle, 1.0, galvo.Angle)
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoStartAngle, ScaleSubAngle, galvo.StartAngle)
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoEndAngle, ScaleSubAngle, galvo.EndAngle)
                       && _device.PdoSetScaledInt16(OdGalvoWrite, SubGalvoXCenter, ScaleCenter, galvo.XCenter)
                       && _device.PdoSetScaledInt16(OdGalvoWrite, SubGalvoYCenter, ScaleCenter, galvo.YCenter)
                       && _device.PdoSetScaledInt16(OdGalvoWrite, SubGalvoXCompression, 1.0, galvo.XCompression)
                       && _device.PdoSetScaledInt16(OdGalvoWrite, SubGalvoYCompression, 1.0, galvo.YCompression)
                       && _device.PdoSetScaledUInt16(OdGalvoWrite, SubGalvoTargetSize, ScaleSize, galvo.TargetSize)
                       && _device.PdoSetScaledInt16(OdGalvoWrite, SubGalvoObliqueCorrection, 1.0, galvo.ObliqueEllipseCorrection)
                       && _device.PdoSetBool(OdGalvoWrite, SubGalvoEnable, galvo.GalvoEnable);
                if (ok)
                {
                    Log(string.Format("振镜参数已写入 图形 {0} 频率 {1}Hz 使能 {2}",
                        galvo.Pattern, galvo.Frequency, galvo.GalvoEnable));
                }
                return ok;
            }
            catch (Exception ex)
            {
                Log("振镜参数写入异常 原因 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>回读振镜参数（0x6010 段）</summary>
        public GalvoParams ReadGalvoParams()
        {
            // pattern 在首个 out 处就地声明；后续 out 变量保留默认值以满足 && 短路下的确定赋值
            double frequency = 0;
            double width = 0;
            double length = 0;
            double angle = 0;
            double startAngle = 0;
            double endAngle = 0;
            double xCenter = 0;
            double yCenter = 0;
            double xComp = 0;
            double yComp = 0;
            double targetSize = 0;
            double actualSize = 0;
            double oblique = 0;
            bool galvoOn = false;
            try
            {
                bool ok = ReadScaledWithIndex(OdGalvoRead, SubGalvoPattern, 1.0, false, out double pattern)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoFrequency, 1.0, false, out frequency)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoWidth, ScaleSize, false, out width)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoLength, ScaleSize, false, out length)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoAngle, 1.0, false, out angle)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoStartAngle, ScaleSubAngle, false, out startAngle)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoEndAngle, ScaleSubAngle, false, out endAngle)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoXCenter, ScaleCenter, true, out xCenter)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoYCenter, ScaleCenter, true, out yCenter)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoXCompression, 1.0, true, out xComp)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoYCompression, 1.0, true, out yComp)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoTargetSize, ScaleSize, false, out targetSize)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoActualSize, ScaleSize, false, out actualSize)
                       && ReadScaledWithIndex(OdGalvoRead, SubGalvoObliqueCorrection, 1.0, true, out oblique)
                       && _device.PdoGetBool(OdGalvoRead, SubGalvoEnable, out galvoOn);
                if (!ok) return null;

                return new GalvoParams
                {
                    Pattern = (WsgGalvoPattern)(int)pattern,
                    Frequency = (int)frequency,
                    Width = width,
                    Length = length,
                    Angle = (int)angle,
                    StartAngle = startAngle,
                    EndAngle = endAngle,
                    XCenter = xCenter,
                    YCenter = yCenter,
                    XCompression = (int)xComp,
                    YCompression = (int)yComp,
                    TargetSize = targetSize,
                    ActualSize = actualSize,
                    ObliqueEllipseCorrection = (int)oblique,
                    GalvoEnable = galvoOn
                };
            }
            catch (Exception ex)
            {
                Log("振镜参数回读异常 原因 " + ex.Message, MessageLevel.Error);
                return null;
            }
        }

        /// <summary>设置温压报警阈值（0x7020）</summary>
        /// <returns>是否成功。</returns>
        public bool SetAlarmThresholds(double tempWarn, double tempAlarm, double riseWarn, double riseAlarm,
            double pressureAlarm, int pressureDelay)
        {
            try
            {
                bool ok = _device.PdoSetScaledUInt16(OdThresholdWrite, SubThresholdTempWarn, ScaleTemp, tempWarn)
                       && _device.PdoSetScaledUInt16(OdThresholdWrite, SubThresholdTempAlarm, ScaleTemp, tempAlarm)
                       && _device.PdoSetScaledUInt16(OdThresholdWrite, SubThresholdRiseWarn, ScaleTemp, riseWarn)
                       && _device.PdoSetScaledUInt16(OdThresholdWrite, SubThresholdRiseAlarm, ScaleTemp, riseAlarm)
                       && _device.PdoSetScaledUInt16(OdThresholdWrite, SubThresholdPressureAlarm, ScaleTemp, pressureAlarm)
                       && _device.PdoSetScaledUInt16(OdThresholdWrite, SubThresholdPressureDelay, 1.0, pressureDelay);
                if (ok)
                {
                    Log(string.Format("报警阈值已写入 温度警告 {0:F1} 报警 {1:F1} 温升警告 {2:F1} 报警 {3:F1} 气压 {4:F1}Bar 延时 {5}ms",
                        tempWarn, tempAlarm, riseWarn, riseAlarm, pressureAlarm, pressureDelay));
                }
                return ok;
            }
            catch (Exception ex)
            {
                Log("报警阈值写入异常 原因 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>开激光（LaserStatus 置 1）</summary>
        /// <returns>是否成功。</returns>
        public bool EnableLaser()
        {
            bool ok = _device.PdoSetBool(OdThresholdWrite, SubLaserStatus, true);
            IsLaserReady = ok;
            Log(ok ? "激光器已开启" : "激光器开启失败", ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>关激光（LaserStatus 置 0）</summary>
        /// <returns>是否成功。</returns>
        public bool DisableLaser()
        {
            bool ok = _device.PdoSetBool(OdThresholdWrite, SubLaserStatus, false);
            if (ok)
            {
                IsLaserReady = false;
            }
            Log(ok ? "激光器已关闭" : "激光器关闭失败", ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>恢复出厂参数（ResetConfiguration 置 1）</summary>
        /// <returns>是否成功。</returns>
        public bool ResetHead()
        {
            bool ok = _device.PdoSetBool(OdThresholdWrite, SubResetConfiguration, true);
            Log(ok ? "焊接头恢复设置指令已下发" : "焊接头恢复设置失败", ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>读温控 MCU 版本（SDO 0x2004）</summary>
        /// <returns>结果码。</returns>
        public uint GetMcuVersion()
        {
            if (!_device.ReadUInt32(OdMcuVersion, 1, out uint version))
            {
                return 0;
            }
            return version;
        }

        /// <summary>设置对焦轴目标位置并使能</summary>

        /// <param name="positionMm">目标位置（mm）</param>
        /// <param name="enable">是否使能电机</param>
        /// <returns>写入成功返回 true</returns>
        public bool SetMotorPosEnable(double positionMm, bool enable)
        {
            try
            {
                bool ok = _device.PdoSetScaledUInt16(OdMotorWrite, SubMotorTarget, 1.0, MmToMotorCount(positionMm))
                       && _device.PdoSetBool(OdMotorWrite, SubMotorEnable, enable);
                if (ok)
                {
                    Log(string.Format("对焦轴已下发 目标 {0:F3}mm 计数 {1:F0} 使能 {2}", positionMm, MmToMotorCount(positionMm), enable));
                }
                return ok;
            }
            catch (Exception ex)
            {
                Log("对焦轴下发异常 原因 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>读取对焦轴状态</summary>
        /// <returns>电机状态；读取失败返回 null</returns>
        public MotorState? ReadMotorStatus()
        {
            try
            {
                if (!_device.PdoGetScaledUInt16(OdMotorRead, SubMotorState, 1.0, out double state))
                {
                    return null;
                }

                if (!_device.PdoGetScaledUInt16(OdMotorRead, SubMotorCurrent, 1.0, out double current))
                {
                    current = 0;
                }

                if (!_device.PdoGetBool(OdMotorRead, SubMotorReady, out bool ready))
                {
                    ready = false;
                }

                return new MotorState
                {
                    State = (MotorRunState)Convert.ToInt32(state),
                    CurrentPositionMm = current / MotorPulsePerMm,
                    IsReady = ready
                };
            }
            catch (Exception ex)
            {
                Log("对焦轴状态读取异常 原因 " + ex.Message, MessageLevel.Error);
                return null;
            }
        }

        /// <summary>停止对焦轴（撤销使能）</summary>
        /// <returns>写入成功返回 true</returns>
        public bool StopMotor()
        {
            try
            {
                return _device.PdoSetBool(OdMotorWrite, SubMotorEnable, false);
            }
            catch (Exception ex)
            {
                Log("对焦轴停止异常 原因 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        #endregion

        #region 私有函数

        /// <summary>毫米换算为电机计数（量纲换算唯一入口）</summary>
        /// <param name="mm">毫米</param>
        /// <returns>计数</returns>
        private double MmToMotorCount(double mm)
        {
            return mm * MotorPulsePerMm;
        }

        /// <summary>读取指定索引的 16 位定标值（振镜回读段）</summary>
        /// <param name="value">值。</param>
        /// <returns>是否成功。</returns>
        private bool ReadScaledWithIndex(ushort odIndex, ushort subIndex, double scale, bool signed, out double value)
        {
            // 三元两分支都以 out 赋值，无须预置默认值
            return signed
                ? _device.PdoGetScaledInt16(odIndex, subIndex, scale, out value)
                : _device.PdoGetScaledUInt16(odIndex, subIndex, scale, out value);
        }

        /// <summary>温度越界文本（硬件故障树唯一产出点）</summary>
        /// <param name="temp">当前保护镜温度（℃）</param>
        /// <param name="limit">触发阈值（℃）</param>
        /// <param name="isAlarm">true = 报警阈值，false = 警告阈值</param>
        /// <returns>可读文本</returns>
        public static string DescribeTemperatureThreshold(double temp, double limit, bool isAlarm)
        {
            return string.Format(isAlarm
                ? "保护镜温度超报警阈值 当前 {0:F1} 上限 {1:F1}"
                : "保护镜温度超警告阈值 当前 {0:F1} 上限 {1:F1}", temp, limit);
        }

        #endregion

        #region IDisposable

        /// <summary>释放资源（关光并复位状态）</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown();
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>振镜图形枚举（0x7010 子索引 1）</summary>
    public enum WsgGalvoPattern
    {
        /// <summary>圆</summary>
        Circle = 1,
        /// <summary>八字</summary>
        Eight = 2,
        /// <summary>直线</summary>
        Line = 3,
        /// <summary>圆弧</summary>
        Arc = 4
    }

    /// <summary>对焦轴电机状态（0x6020 子索引 2）</summary>
    public enum MotorRunState
    {
        /// <summary>停止</summary>
        Stopped = 0,
        /// <summary>负限位</summary>
        NegativeLimit = 1,
        /// <summary>正限位</summary>
        PositiveLimit = 2,
        /// <summary>运动中</summary>
        Moving = 3
    }

    /// <summary>对焦轴电机状态快照（0x6020 段）</summary>
    public struct MotorState
    {
        /// <summary>运行状态</summary>
        public MotorRunState State { get; set; }

        /// <summary>当前位置（mm，已按量纲换算）</summary>
        public double CurrentPositionMm { get; set; }

        /// <summary>是否就绪（true 就绪 / false 报警）</summary>
        public bool IsReady { get; set; }
    }

    /// <summary>振镜参数（0x7010 写 / 0x6010 读）</summary>
    public class GalvoParams
    {
        /// <summary>振镜图形（1 圆 2 八字 3 直线 4 圆弧）</summary>
        public WsgGalvoPattern Pattern { get; set; }

        /// <summary>频率（Hz，10~500，精度 1）</summary>
        public int Frequency { get; set; }

        /// <summary>宽度（mm，0~5，精度 0.1）</summary>
        public double Width { get; set; }

        /// <summary>长度（mm，0~5，精度 0.1）</summary>
        public double Length { get; set; }

        /// <summary>旋转角度（°，0~360，精度 1）</summary>
        public int Angle { get; set; }

        /// <summary>起始角度（°，0~360，精度 0.1）</summary>
        public double StartAngle { get; set; }

        /// <summary>结束角度（°，0~360，精度 0.1）</summary>
        public double EndAngle { get; set; }

        /// <summary>X 中心（mm，-10~10，精度 0.1）</summary>
        public double XCenter { get; set; }

        /// <summary>Y 中心（mm，-10~10，精度 0.1）</summary>
        public double YCenter { get; set; }

        /// <summary>X 压缩（%，-100~100，精度 1）</summary>
        public int XCompression { get; set; }

        /// <summary>Y 压缩（%，-100~100，精度 1）</summary>
        public int YCompression { get; set; }

        /// <summary>目标尺寸（mm，0~5，精度 0.1）</summary>
        public double TargetSize { get; set; }

        /// <summary>实际尺寸（mm，0~5，精度 0.1，仅回读）</summary>
        public double ActualSize { get; set; }

        /// <summary>斜椭校正（-180~180，精度 1）</summary>
        public int ObliqueEllipseCorrection { get; set; }

        /// <summary>振镜使能（true 打开）</summary>
        public bool GalvoEnable { get; set; }

        /// <summary>创建振镜参数并填入协议默认值</summary>
        public GalvoParams()
        {
            Pattern = WsgGalvoPattern.Circle;
            Frequency = 50;
            Width = 1.0;
            Length = 1.0;
            Angle = 0;
            StartAngle = 0.0;
            EndAngle = 360.0;
            XCenter = 0.0;
            YCenter = 0.0;
            XCompression = 0;
            YCompression = 0;
            TargetSize = 1.0;
            ActualSize = 0.0;
            ObliqueEllipseCorrection = 0;
            GalvoEnable = false;
        }

        /// <summary>创建振镜逻辑零位</summary>

        /// <returns>逻辑零位参数</returns>
        public static GalvoParams CreateLogicalHome()
        {
            return new GalvoParams
            {
                Pattern = WsgGalvoPattern.Line,
                Frequency = 0,
                Width = 0.0,
                Length = 0.0,
                Angle = 0,
                StartAngle = 0.0,
                EndAngle = 0.0,
                XCenter = 0.0,
                YCenter = 0.0,
                XCompression = 0,
                YCompression = 0,
                TargetSize = 0.0,
                ObliqueEllipseCorrection = 0,
                GalvoEnable = false
            };
        }
    }

    /// <summary>振镜全局参数（各图形共用，不随图形分档变化）。</summary>
    public class GalvoGlobalParams
    {
        /// <summary>旋转角度（°，0~360，精度 1）</summary>
        public int Angle { get; set; }

        /// <summary>起始角度（°，0~360，精度 0.1）</summary>
        public double StartAngle { get; set; }

        /// <summary>结束角度（°，0~360，精度 0.1）</summary>
        public double EndAngle { get; set; }

        /// <summary>X 中心（mm，-10~10，精度 0.1）</summary>
        public double XCenter { get; set; }

        /// <summary>Y 中心（mm，-10~10，精度 0.1）</summary>
        public double YCenter { get; set; }

        /// <summary>X 压缩（%，-100~100，精度 1）</summary>
        public int XCompression { get; set; }

        /// <summary>Y 压缩（%，-100~100，精度 1）</summary>
        public int YCompression { get; set; }

        /// <summary>目标尺寸（mm，0~5，精度 0.1）</summary>
        public double TargetSize { get; set; }

        /// <summary>斜椭校正（-180~180，精度 1）</summary>
        public int ObliqueEllipseCorrection { get; set; }

        /// <summary>振镜使能（true 打开）</summary>
        public bool GalvoEnable { get; set; }

        /// <summary>创建并填入协议默认值</summary>
        public GalvoGlobalParams()
        {
            Angle = 0;
            StartAngle = 0.0;
            EndAngle = 360.0;
            XCenter = 0.0;
            YCenter = 0.0;
            XCompression = 0;
            YCompression = 0;
            TargetSize = 1.0;
            ObliqueEllipseCorrection = 0;
            GalvoEnable = false;
        }
    }

    /// <summary>振镜分图形参数（一图形一档：摆宽/摆长/频率）。</summary>
    public class GalvoShapeParams
    {
        /// <summary>摆宽（mm）</summary>
        public double Width { get; set; }

        /// <summary>摆长（mm）</summary>
        public double Length { get; set; }

        /// <summary>摆动频率（Hz）</summary>
        public int Frequency { get; set; }

        /// <summary>创建并填入默认值（协议默认：宽 1.0 / 长 1.0 / 频率 50）</summary>
        public GalvoShapeParams()
        {
            Width = 1.0;
            Length = 1.0;
            Frequency = 50;
        }

        /// <summary>创建并填入指定值</summary>
        /// <param name="width">摆宽（mm）</param>
        /// <param name="length">摆长（mm）</param>
        /// <param name="frequency">摆动频率（Hz）</param>
        public GalvoShapeParams(double width, double length, int frequency)
        {
            Width = width;
            Length = length;
            Frequency = frequency;
        }
    }

    /// <summary>焊接头温度与气压数据（0x6000 段）</summary>
    public class LaserHeadTemperatureData
    {
        /// <summary>保护镜温度（℃，精度 0.1）</summary>
        public double ProtectiveLensTemp { get; set; }

        /// <summary>保护镜腔体温度（℃，精度 0.1）</summary>
        public double ProtectiveLensAmbientTemp { get; set; }

        /// <summary>保护镜温升（℃，精度 0.1）</summary>
        public double ProtectiveLensRiseTemp { get; set; }

        /// <summary>切割气压（Bar，精度 0.01）</summary>
        public double CuttingAirPressure { get; set; }

        /// <summary>传感器状态字节（bit0 镜温 bit1 温升 bit2 气压）</summary>
        public byte SensorStatus { get; set; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>创建温度气压数据</summary>
        public LaserHeadTemperatureData()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>焊接头报警状态（位+驱动板+振镜）</summary>
    public class HeadAlarmStatus
    {
        /// <summary>保护镜温度报警（SensorStatus bit0）</summary>
        public bool ProtectiveLensTempAlarm { get; set; }

        /// <summary>保护镜温升报警（SensorStatus bit1）</summary>
        public bool ProtectiveLensRiseTempAlarm { get; set; }

        /// <summary>切割气压报警（SensorStatus bit2）</summary>
        public bool CuttingAirPressureAlarm { get; set; }

        /// <summary>驱动板报警（0x6010 子索引 16）</summary>
        public bool DriverAlarm { get; set; }

        /// <summary>振镜状态（0x6010 子索引 15，true 打开）</summary>
        public bool GalvoOn { get; set; }

        /// <summary>是否有任何报警</summary>
        public bool HasAlarm
        {
            get
            {
                return ProtectiveLensTempAlarm || ProtectiveLensRiseTempAlarm ||
                       CuttingAirPressureAlarm || DriverAlarm;
            }
        }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>创建报警状态</summary>
        public HeadAlarmStatus()
        {
            Timestamp = DateTime.Now;
        }
        /// <summary>报警原因文本</summary>
        /// <returns>报警原因；无报警返回空串</returns>
        public string Describe()
        {
            string text = string.Empty;
            if (ProtectiveLensTempAlarm) text += "保护镜温度报警 ";
            if (ProtectiveLensRiseTempAlarm) text += "保护镜温升报警 ";
            if (CuttingAirPressureAlarm) text += "切割气压报警 ";
            if (DriverAlarm) text += "驱动板报警 ";
            return text.Trim();
        }

    }

    /// <summary>焊接头从站 PDO 映射条目</summary>
    public class WeldHeadPdoEntry
    {
        /// <summary>序号</summary>
        public string Seq { get; set; }

        /// <summary>变量名（协议变量标识）</summary>
        public string VarName { get; set; }

        /// <summary>中文名称</summary>
        public string DisplayName { get; set; }

        /// <summary>数据类型（UINT8/INT/UINT16/BOOLEAN 等）</summary>
        public string DataType { get; set; }

        /// <summary>SDO 索引</summary>
        public string SdoIndex { get; set; }

        /// <summary>SDO 子索引</summary>
        public string SdoSubIndex { get; set; }

        /// <summary>访问权限（ro/rw）</summary>
        public string Access { get; set; }

        /// <summary>PDO 索引</summary>
        public string PdoIndex { get; set; }

        /// <summary>PDO 子索引</summary>
        public string PdoSubIndex { get; set; }

        /// <summary>说明（单位/精度/范围）</summary>
        public string Description { get; set; }

        /// <summary>创建空条目</summary>
        public WeldHeadPdoEntry()
        {
            Seq = string.Empty;
            VarName = string.Empty;
            DisplayName = string.Empty;
            DataType = string.Empty;
            SdoIndex = string.Empty;
            SdoSubIndex = string.Empty;
            Access = string.Empty;
            PdoIndex = string.Empty;
            PdoSubIndex = string.Empty;
            Description = string.Empty;
        }
    }

    /// <summary>焊接头配置（阈值 + 振镜默认参数 + PDO 映射）</summary>
    public class WeldHeadConfig
    {
        /// <summary>温度警告阈值（℃）</summary>
        public double TempWarnThreshold { get; set; }

        /// <summary>温度报警阈值（℃）</summary>
        public double TempAlarmThreshold { get; set; }

        /// <summary>温升警告阈值（℃）</summary>
        public double RiseWarnThreshold { get; set; }

        /// <summary>温升报警阈值（℃）</summary>
        public double RiseAlarmThreshold { get; set; }

        /// <summary>切割气压报警阈值（Bar）</summary>
        public double PressureAlarmThreshold { get; set; }

        /// <summary>切割气压检测延时（ms）</summary>
        public int PressureDelayMs { get; set; }

        /// <summary>振镜全局参数（各图形共用）</summary>
        public GalvoGlobalParams DefaultGalvo { get; private set; }

        /// <summary>振镜分图形参数表：每种图形各存一份宽/长/频。</summary>
        public System.Collections.Generic.Dictionary<WsgGalvoPattern, GalvoShapeParams> GalvoShapePresets { get; private set; }

        /// <summary>电机脉冲当量默认值（计数/mm）= 10 个计数 1mm</summary>
        public const double DefaultMotorPulsePerMm = 10.0;

        /// <summary>电机脉冲当量（计数/mm），默认 10；量纲可配置</summary>
        public double MotorPulsePerMm { get; set; }

        private readonly List<WeldHeadPdoEntry> _pdoMap = new List<WeldHeadPdoEntry>();

        /// <summary>从站 PDO 映射（由焊接头从站 IO 表生成，空表表示未导入）</summary>
        public IList<WeldHeadPdoEntry> PdoMap { get { return _pdoMap; } }

        /// <summary>IO 表来源文件名（如 EtherCAT协议.xlsx，用于追溯表版本）</summary>
        public string IoMapSourceFile { get; set; }

        /// <summary>IO 表来源文件修改时间（yyyy-MM-dd HH:mm:ss）</summary>
        public string IoMapSourceTime { get; set; }

        /// <summary>IO 表来源数据行数（不含表头）</summary>
        public int IoMapSourceRows { get; set; }

        /// <summary>创建配置并填入默认值</summary>
        public WeldHeadConfig()
        {
            TempWarnThreshold = 50.0;
            TempAlarmThreshold = 60.0;
            RiseWarnThreshold = 40.0;
            RiseAlarmThreshold = 50.0;
            PressureAlarmThreshold = 5.0;
            PressureDelayMs = 500;
            DefaultGalvo = new GalvoGlobalParams();
            GalvoShapePresets = new System.Collections.Generic.Dictionary<WsgGalvoPattern, GalvoShapeParams>();
            foreach (WsgGalvoPattern pattern in GalvoPatternOrder)
            {
                GalvoShapePresets[pattern] = new GalvoShapeParams();
            }
            MotorPulsePerMm = DefaultMotorPulsePerMm;
            IoMapSourceFile = string.Empty;
            IoMapSourceTime = string.Empty;
            IoMapSourceRows = 0;
        }

        /// <summary>振镜图形的标准顺序</summary>
        public static readonly WsgGalvoPattern[] GalvoPatternOrder =
        {
            WsgGalvoPattern.Circle, WsgGalvoPattern.Eight, WsgGalvoPattern.Line, WsgGalvoPattern.Arc
        };

        /// <summary>取指定图形的分档参数；缺档时回退直线档</summary>
        /// <param name="pattern">图形</param>
        /// <returns>该图形的宽/长/频</returns>
        public GalvoShapeParams GetGalvoShape(WsgGalvoPattern pattern)
        {
            if (GalvoShapePresets.TryGetValue(pattern, out GalvoShapeParams shape)) return shape;
            if (GalvoShapePresets.TryGetValue(WsgGalvoPattern.Line, out shape)) return shape;
            return new GalvoShapeParams();
        }

        /// <summary>按图形组装一份完整的振镜下发参数。</summary>
        /// <param name="pattern">图形</param>
        /// <returns>可直接下发的振镜参数</returns>
        public GalvoParams BuildGalvoParamsFor(WsgGalvoPattern pattern)
        {
            GalvoShapeParams shape = GetGalvoShape(pattern);
            GalvoParams galvo = new GalvoParams
            {
                Pattern = pattern,
                Width = shape.Width,
                Length = shape.Length,
                Frequency = shape.Frequency,
                Angle = DefaultGalvo.Angle,
                StartAngle = DefaultGalvo.StartAngle,
                EndAngle = DefaultGalvo.EndAngle,
                XCenter = DefaultGalvo.XCenter,
                YCenter = DefaultGalvo.YCenter,
                XCompression = DefaultGalvo.XCompression,
                YCompression = DefaultGalvo.YCompression,
                TargetSize = DefaultGalvo.TargetSize,
                ObliqueEllipseCorrection = DefaultGalvo.ObliqueEllipseCorrection,
                GalvoEnable = DefaultGalvo.GalvoEnable
            };
            return galvo;
        }
    }
}

