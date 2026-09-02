using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.PLC.Siemens;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.ProductFileManager;
using S7.Net;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>
    /// 设备安全/IO/状态管控（单例，参考 PcomDeviceInterface.MachineStatusWork）：
    /// 由三个常驻监控线程内聚而成——IOWork（安全互锁与 IO 轮询）、
    /// EMGWork（急停按钮处置）、StatusWork（主设备运行态三色灯反射）。
    /// 任何线程检测到异常均经 DeviceControlWork 单一控制源变更主设备运行态并记故障，
    /// 不自行维护设备态/流程态（权责边界见 ADR-043）。
    ///
    /// 独立管控类，不继承 DeviceWorkflowBase；启动 StartWork() / 停止 CloseWork()（亦经 Dispose 释放）。
    /// </summary>
    public class DeviceStatusWork
    {
        #region 常量

        /// <summary>IO 与安全互锁轮询周期（毫秒，20Hz）</summary>
        private const int IoPollIntervalMs = 50;

        /// <summary>急停轮询周期（毫秒，20Hz）</summary>
        private const int EmgPollIntervalMs = 50;

        /// <summary>状态反射轮询周期（毫秒，20Hz）</summary>
        private const int StatusPollIntervalMs = 50;

        #endregion

        #region 私有变量

        private static readonly Lazy<DeviceStatusWork> _lazyInstance =
            new Lazy<DeviceStatusWork>(() => new DeviceStatusWork());

        private readonly string _tag = "设备安全状态管控";

        private readonly SafetyInterlockChecker _checker;

        private volatile bool _running;
        private volatile bool _alarmActive;
        private Thread _thIOWork;
        private Thread _thEMGWork;
        private Thread _thStatusWork;

        private SafetyInterlockResult _lastResult;

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static DeviceStatusWork Instance { get { return _lazyInstance.Value; } }

        /// <summary>三个监控线程是否运行中</summary>
        public bool IsRunning { get { return _running; } }

        /// <summary>最近一次互锁检查结果（由 IOWork 刷新）</summary>
        public SafetyInterlockResult LastResult
        {
            get { return _lastResult; }
            private set { _lastResult = value; }
        }

        /// <summary>互锁阈值与 PLC 地址配置</summary>
        public SafetyFlow Config { get { return _checker.Thresholds; } }

        /// <summary>当前是否安全。监控未启动时不门控（返回 true），避免 PLC 未标定阻断整机；</summary>
        /// <remarks>启动后以最近一次互锁检查结果为准，无结果视为不安全。</remarks>
        public bool IsSafe
        {
            get
            {
                if (!_running) return true;
                return _lastResult != null && _lastResult.IsPassed;
            }
        }

        #endregion

        #region 构造函数

        private DeviceStatusWork()
        {
            _checker = new SafetyInterlockChecker();
        }

        #endregion

        #region 私有函数

        /// <summary>IO 轮询线程主体：互锁检查与设备态监控。</summary>
        /// <remarks>对应 PDF 架构图 DeviceStatusWork 泳道的 IO 轮询与设备态（Connect/Comm）监控。
        /// 急停由 EMGWork 专责，避免与互锁报警态相互翻转。</remarks>
        private void IOWork()
        {
            while (_running)
            {
                try
                {
                    var result = _checker.Check();
                    LastResult = result;

                    if (result.IsPassed)
                    {
                        if (_alarmActive)
                        {
                            _alarmActive = false;
                            DeviceControlWork.Instance.ReportAlarmCleared();
                        }
                    }
                    else if (!_alarmActive)
                    {
                        _alarmActive = true;
                        DeviceControlWork.Instance.ReportAlarm(result.Reason);
                        RecordFault(result.Reason);
                    }
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_tag, "安全互锁异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(IoPollIntervalMs);
            }
        }

        /// <summary>急停轮询线程主体。</summary>
        /// <remarks>对应 PDF 架构图 DeviceStatusWork 泳道的 EMG 处置；急停为最高优先级，线程置 Highest。</remarks>
        private void EMGWork()
        {
            bool emgShown = false;
            while (_running)
            {
                try
                {
                    var cfg = _checker.Thresholds;
                    var comm = GlobalCommData.mCommunicationManager;
                    var plc = comm != null && comm.IsPlcEnabled ? comm.PlcManager : null;
                    bool pressed = plc != null &&
                        plc.ReadBit(DataType.DataBlock, cfg.PlcDb, 0, cfg.EmergencyStopBit);

                    var status = DeviceControlWork.Instance.Status;
                    if (pressed && !emgShown)
                    {
                        emgShown = true;
                        DeviceControlWork.Instance.EStopMachine("急停按钮按下");
                        GlobalCommData.ShowLog(_tag, "急停按钮按下", MessageLevel.Error);
                    }
                    else if (!pressed && emgShown)
                    {
                        emgShown = false;
                        if (status == MainDeviceStatus.EStop)
                        {
                            DeviceControlWork.Instance.EStopCancel();
                            DeviceControlWork.Instance.ReportAlarmCleared();
                        }
                    }
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_tag, "急停轮询异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(EmgPollIntervalMs);
            }
        }

        /// <summary>三色灯反射线程主体。</summary>
        /// <remarks>对应 PDF 架构图 DeviceStatusWork 泳道的 Status 反射；状态切换才写 PLC，避免每拍抖动。</remarks>
        private void StatusWork()
        {
            MainDeviceStatus last = MainDeviceStatus.Stop;
            while (_running)
            {
                try
                {
                    var status = DeviceControlWork.Instance.Status;
                    if (status != last)
                    {
                        last = status;
                        DriveTricolorLamp(status);
                        GlobalCommData.ShowLog(_tag, "状态反射 " + status, MessageLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_tag, "状态反射异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(StatusPollIntervalMs);
            }
        }

        /// <summary>驱动三色灯 PLC 输出（地址待标定，未配置 StatusLampDb 则不写）。</summary>
        /// <param name="status">主设备运行态</param>
        private void DriveTricolorLamp(MainDeviceStatus status)
        {
            var cfg = _checker.Thresholds;
            if (cfg.StatusLampDb <= 0) return;
            var comm = GlobalCommData.mCommunicationManager;
            var plc = comm != null && comm.IsPlcEnabled ? comm.PlcManager : null;
            if (plc == null) return;

            bool green = false, yellow = false, red = false, buzzer = false;
            switch (status)
            {
                case MainDeviceStatus.Running:
                    green = true;
                    break;
                case MainDeviceStatus.Stop:
                case MainDeviceStatus.Reseting:
                    green = true; yellow = true;
                    break;
                case MainDeviceStatus.NoReset:
                    yellow = true;
                    break;
                case MainDeviceStatus.Alarm:
                case MainDeviceStatus.EStop:
                    red = true; buzzer = true;
                    break;
            }

            try
            {
                plc.WriteBit(DataType.DataBlock, cfg.StatusLampDb, cfg.GreenLightByte, cfg.GreenLightBit, green);
                plc.WriteBit(DataType.DataBlock, cfg.StatusLampDb, cfg.YellowLightByte, cfg.YellowLightBit, yellow);
                plc.WriteBit(DataType.DataBlock, cfg.StatusLampDb, cfg.RedLightByte, cfg.RedLightBit, red);
                plc.WriteBit(DataType.DataBlock, cfg.StatusLampDb, cfg.BuzzerByte, cfg.BuzzerBit, buzzer);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, "三色灯输出异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>记录安全故障到持久化（经 FaultRecoveryManager）。</summary>
        /// <param name="reason">故障原因</param>
        private void RecordFault(string reason)
        {
            FaultRecoveryManager.Instance.RecordFault(_tag, new FaultRecord
            {
                Device = _tag,
                State = SubDeviceWeldStatus.ErrorAborted,
                Category = FaultCategory.Safety,
                ErrorCode = "INTERLOCK_FAILED",
                ParamSnapshot = reason
            });
        }

        /// <summary>等待线程退出（最多 1s）。</summary>
        /// <param name="thread">待汇合的线程</param>
        private void JoinThread(Thread thread)
        {
            if (thread != null && thread.IsAlive) thread.Join(1000);
        }

        #endregion

        #region 公共函数

        /// <summary>启动三个监控线程（幂等）。</summary>
        /// <remarks>由 DeviceControlWork.StartMachine 拉起；IOWork 置 AboveNormal、EMGWork 置 Highest、StatusWork 置 Normal。</remarks>
        public void StartWork()
        {
            if (_running) return;
            _running = true;
            _alarmActive = false;

            _thIOWork = new Thread(IOWork)
            {
                Name = "DeviceStatusIO",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _thIOWork.Start();

            _thEMGWork = new Thread(EMGWork)
            {
                Name = "DeviceStatusEMG",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _thEMGWork.Start();

            _thStatusWork = new Thread(StatusWork)
            {
                Name = "DeviceStatusLamp",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            _thStatusWork.Start();

            GlobalCommData.ShowLog(_tag, "安全/IO/状态监控启动", MessageLevel.Info);
        }

        /// <summary>停止三个监控线程。</summary>
        public void CloseWork()
        {
            if (!_running) return;
            _running = false;
            JoinThread(_thIOWork);
            JoinThread(_thEMGWork);
            JoinThread(_thStatusWork);
            _thIOWork = null;
            _thEMGWork = null;
            _thStatusWork = null;
            _alarmActive = false;
            GlobalCommData.ShowLog(_tag, "安全/IO/状态监控停止", MessageLevel.Info);
        }

        /// <summary>执行一次安全互锁检查。</summary>
        /// <returns>互锁检查结果，含是否通过与未通过项</returns>
        public SafetyInterlockResult Check()
        {
            var result = _checker.Check();
            LastResult = result;
            return result;
        }

        /// <summary>复位安全报警标志（清报警态由 DeviceControlWork 统一处置）。</summary>
        public void Reset()
        {
            _alarmActive = false;
        }

        /// <summary>释放：停止监控线程。</summary>
        public void Dispose()
        {
            CloseWork();
        }

        #endregion
    }

    /// <summary>安全互锁阈值与 PLC 地址映射。</summary>
    /// <remarks>数值为 PLC 原始单位（WORD/REAL 经站点标定后填入），阈值比较使用原始单位；
    /// StatusLamp* 为三色灯输出地址，StatusLampDb 置 0 表示未配置（不写）。</remarks>
    public class SafetyFlow
    {
        /// <summary>保护气压下限（PLC 原始单位）</summary>
        public double MinGasPressure { get; set; }

        /// <summary>冷却水流量下限（PLC 原始单位）</summary>
        public double MinCoolantFlow { get; set; }

        /// <summary>冷却水温度上限（PLC 原始单位）</summary>
        public double MaxCoolantTemperature { get; set; }

        /// <summary>激光头温度上限（PLC 原始单位）</summary>
        public double MaxHeadTemperature { get; set; }

        /// <summary>是否要求自动模式（PLC 模式字 = 1）</summary>
        public bool RequireAutoMode { get; set; }

        /// <summary>PLC DB 块号</summary>
        public int PlcDb { get; set; }

        /// <summary>急停按钮位索引（EMGWork 专责轮询）</summary>
        public int EmergencyStopBit { get; set; }

        /// <summary>安全门位索引</summary>
        public int SafetyDoorBit { get; set; }

        /// <summary>保护气压 WORD 字节地址</summary>
        public int GasPressureWord { get; set; }

        /// <summary>冷却水流量 WORD 字节地址</summary>
        public int CoolantFlowWord { get; set; }

        /// <summary>冷却水温度 WORD 字节地址</summary>
        public int CoolantTempWord { get; set; }

        /// <summary>激光头温度 WORD 字节地址</summary>
        public int HeadTempWord { get; set; }

        /// <summary>模式字 WORD 字节地址（1=自动）</summary>
        public int ModeWord { get; set; }

        /// <summary>三色灯输出 DB 块号（0 表示未配置）</summary>
        public int StatusLampDb { get; set; }

        /// <summary>绿灯字节地址</summary>
        public int GreenLightByte { get; set; }

        /// <summary>绿灯位索引</summary>
        public int GreenLightBit { get; set; }

        /// <summary>黄灯字节地址</summary>
        public int YellowLightByte { get; set; }

        /// <summary>黄灯位索引</summary>
        public int YellowLightBit { get; set; }

        /// <summary>红灯字节地址</summary>
        public int RedLightByte { get; set; }

        /// <summary>红灯位索引</summary>
        public int RedLightBit { get; set; }

        /// <summary>蜂鸣器字节地址</summary>
        public int BuzzerByte { get; set; }

        /// <summary>蜂鸣器位索引</summary>
        public int BuzzerBit { get; set; }

        /// <summary>构造：应用默认占位值（现场标定后覆盖）。</summary>
        public SafetyFlow()
        {
            MinGasPressure = 40;
            MinCoolantFlow = 10;
            MaxCoolantTemperature = 35;
            MaxHeadTemperature = 60;
            RequireAutoMode = true;
            PlcDb = 1;
            EmergencyStopBit = 0;
            SafetyDoorBit = 1;
            GasPressureWord = 10;
            CoolantFlowWord = 12;
            CoolantTempWord = 14;
            HeadTempWord = 16;
            ModeWord = 20;
            StatusLampDb = 0;
            GreenLightByte = 0;
            GreenLightBit = 0;
            YellowLightByte = 0;
            YellowLightBit = 1;
            RedLightByte = 0;
            RedLightBit = 2;
            BuzzerByte = 0;
            BuzzerBit = 3;
        }
    }

    /// <summary>安全互锁检查结果</summary>
    public class SafetyInterlockResult
    {
        /// <summary>是否全部通过</summary>
        public bool IsPassed { get; set; }

        /// <summary>未通过项说明列表</summary>
        public List<string> FailedItems { get { return _failedItems; } }

        private readonly List<string> _failedItems = new List<string>();

        /// <summary>汇总原因</summary>
        public string Reason { get; set; }

        /// <summary>检查时间戳</summary>
        public DateTime CheckTime { get; set; }
    }

    /// <summary>安全互锁检查器：覆盖硬件（安全门/气压/冷却水/温度/模式）与设备态（四个子设备 Connected、机器人在线）两类互锁。</summary>
    /// <remarks>阈值与地址来自 <see cref="SafetyFlow"/>，需现场标定；设备就绪判定统一为 SubDeviceState.Connected。
    /// 急停按钮由 DeviceStatusWork.EMGWork 专责处置（置 EStop），故此处不再检查急停位。</remarks>
    public class SafetyInterlockChecker
    {
        #region 私有变量

        private readonly SafetyFlow _thresholds;

        #endregion

        #region 公共变量

        /// <summary>当前生效的阈值与地址配置</summary>
        public SafetyFlow Thresholds { get { return _thresholds; } }

        #endregion

        #region 构造函数

        /// <summary>构造互锁检查器</summary>
        /// <param name="thresholds">阈值与 PLC 地址配置，传 null 使用默认值</param>
        public SafetyInterlockChecker(SafetyFlow thresholds = null)
        {
            _thresholds = thresholds ?? new SafetyFlow();
        }

        #endregion

        #region 私有函数

        /// <summary>读取数据块字</summary>
        /// <param name="plc">PLC 通讯实例</param>
        /// <param name="db">数据块号</param>
        /// <param name="word">字节地址</param>
        /// <returns>该地址的 WORD 值</returns>
        private static ushort ReadWord(S7PLCCommunication plc, int db, int word)
        {
            return plc.Read<ushort>(string.Format("DB{0}.DBW{1}", db, word));
        }

        /// <summary>判定数值是否低于下限</summary>
        /// <param name="result">待写入的检查结果</param>
        /// <param name="name">检查项名称</param>
        /// <param name="value">实测值</param>
        /// <param name="min">下限阈值</param>
        private static void CheckLowerBound(SafetyInterlockResult result, string name, double value, double min)
        {
            if (value < min)
            {
                result.FailedItems.Add(string.Format("{0}不足 当前 {1} 低于下限 {2}", name, value, min));
            }
        }

        /// <summary>判定数值是否高于上限</summary>
        /// <param name="result">待写入的检查结果</param>
        /// <param name="name">检查项名称</param>
        /// <param name="value">实测值</param>
        /// <param name="max">上限阈值</param>
        private static void CheckUpperBound(SafetyInterlockResult result, string name, double value, double max)
        {
            if (value > max)
            {
                result.FailedItems.Add(string.Format("{0}过高 当前 {1} 高于上限 {2}", name, value, max));
            }
        }

        /// <summary>检查硬件互锁项（急停除外，由 EMGWork 专责）</summary>
        /// <param name="plc">PLC 通讯实例</param>
        /// <param name="result">待写入的检查结果</param>
        private void CheckPlc(S7PLCCommunication plc, SafetyInterlockResult result)
        {
            var t = _thresholds;
            try
            {
                // 安全门位为 0 表示未关闭或未锁定
                if (!plc.ReadBit(DataType.DataBlock, t.PlcDb, 0, t.SafetyDoorBit))
                {
                    result.FailedItems.Add("安全门未关闭或未锁定");
                }

                CheckLowerBound(result, "保护气压",
                    ReadWord(plc, t.PlcDb, t.GasPressureWord), t.MinGasPressure);
                CheckLowerBound(result, "冷却水流量",
                    ReadWord(plc, t.PlcDb, t.CoolantFlowWord), t.MinCoolantFlow);
                CheckUpperBound(result, "冷却水温度",
                    ReadWord(plc, t.PlcDb, t.CoolantTempWord), t.MaxCoolantTemperature);
                CheckUpperBound(result, "激光头温度",
                    ReadWord(plc, t.PlcDb, t.HeadTempWord), t.MaxHeadTemperature);

                if (t.RequireAutoMode && ReadWord(plc, t.PlcDb, t.ModeWord) != 1)
                {
                    result.FailedItems.Add("PLC 模式非自动");
                }
            }
            catch (Exception ex)
            {
                result.FailedItems.Add("PLC 读取异常 " + ex.Message);
            }
        }

        /// <summary>检查设备态互锁项</summary>
        /// <param name="result">待写入的检查结果</param>
        private void CheckDeviceStates(SafetyInterlockResult result)
        {
            CheckDevice("激光焊接头", LaserWeldHeadWorkflow.Instance, result);
            CheckDevice("线激光相机", LineLaserWorkflow.Instance, result);
            CheckDevice("监控相机", MonitorCameraWorkflow.Instance, result);
            CheckDevice("运动控制", MotionControlWorkflow.Instance, result);

            if (!GlobalCommData.mCommunicationManager.IsRobotEnabled) return;

            var robot = GlobalCommData.mCommunicationManager.RobotManager;
            bool online = robot != null && (robot.IsRSIConnected || robot.IsEKIConnected);
            if (!online)
            {
                result.FailedItems.Add("机器人未在线");
            }
        }

        /// <summary>检查单个设备就绪状态</summary>
        /// <param name="name">设备显示名称</param>
        /// <param name="device">设备状态实例</param>
        /// <param name="result">待写入的检查结果</param>
        private void CheckDevice(string name, DeviceStateBase device, SafetyInterlockResult result)
        {
            if (device == null)
            {
                result.FailedItems.Add(name + " 状态缺失");
                return;
            }
            if (device.State != SubDeviceState.Connected)
            {
                result.FailedItems.Add(name + " 未就绪 状态 " + device.State);
            }
        }

        #endregion

        #region 公共函数

        /// <summary>执行硬件与设备态互锁检查（急停除外）</summary>
        /// <returns>互锁检查结果，含是否通过与未通过项</returns>
        public SafetyInterlockResult Check()
        {
            var result = new SafetyInterlockResult();
            result.CheckTime = DateTime.Now;

            var comm = GlobalCommData.mCommunicationManager;
            var plc = comm != null ? comm.PlcManager : null;
            if (plc != null && comm.IsPlcEnabled)
            {
                CheckPlc(plc, result);
            }
            else
            {
                result.FailedItems.Add("PLC 未连接或未启用 跳过硬件互锁项 仅做设备态检查");
            }

            CheckDeviceStates(result);

            result.IsPassed = result.FailedItems.Count == 0;
            result.Reason = result.IsPassed
                ? "安全互锁全部通过"
                : string.Join("，", result.FailedItems);
            return result;
        }

        #endregion
    }
}
