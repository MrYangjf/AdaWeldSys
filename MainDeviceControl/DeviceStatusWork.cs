using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.PLC.Siemens;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MotionControl;
using S7.Net;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>设备安全/IO/状态管控（单例，三常驻线程）。</summary>
    /// <remarks>thIOWork 安全互锁与 IO 轮询、thEMGWork 急停处置、thLightStatusWork 三色灯反射；异常经 DeviceControlWork 单一控制源变更运行态（ADR-027）。</remarks>
    public class DeviceStatusWork
    {
        #region 常量

        /// <summary>IO 与安全互锁轮询周期（毫秒，20Hz）</summary>
        private const int IoPollIntervalMs = 50;

        /// <summary>急停轮询周期（毫秒，20Hz）</summary>
        private const int EmgPollIntervalMs = 50;

        /// <summary>状态反射轮询周期（毫秒，20Hz）</summary>
        private const int StatusPollIntervalMs = 50;

        /// <summary>运控总线监督扫描节流拍数（50ms×4=200ms，总线检查为 µs~ms 级读共享内存，无需 20Hz 高频）</summary>
        private const int MotionBusCheckBeats = 4;

        #endregion

        #region 私有变量

        private static readonly Lazy<DeviceStatusWork> _lazyInstance =
            new Lazy<DeviceStatusWork>(() => new DeviceStatusWork());

        private readonly string _tag = "设备安全状态管控";

        private readonly SafetyInterlockChecker _checker;

        private volatile bool _running;
        private volatile bool _alarmActive;
        private Thread thIOWork;
        private Thread thEMGWork;
        private Thread thLightStatusWork;

        private readonly object _actionLock = new object();

        /// <summary>运控总线监督节流拍计数器（IOWork 专享）</summary>
        private int _motionBusCheckBeat;
        private volatile bool _actionBusy;
        private Thread thAction;

        private SafetyInterlockResult _lastResult;

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static DeviceStatusWork Instance { get { return _lazyInstance.Value; } }

        /// <summary>三个监控线程是否运行中</summary>
        public bool IsRunning { get { return _running; } }

        /// <summary>IO 安全互锁监控线程是否运行中</summary>
        public bool IsIOWorking { get; private set; }

        /// <summary>急停处置线程是否运行中</summary>
        public bool IsEMGWorking { get; private set; }

        /// <summary>三色灯状态反射线程是否运行中</summary>
        public bool IsLightStatusWorking { get; private set; }

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

        /// <summary>统一日志出口（固定使用本类标签）。</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(_tag, message, level);
        }

        /// <summary>IO 轮询线程主体：Running IO 扫描与 Alarm IO 互锁检查。</summary>
        /// <remarks>急停由 EMGWork 专责，避免与互锁报警态相互翻转。</remarks>
        private void IOWork()
        {
            IsIOWorking = true;
            while (_running)
            {
                try
                {
                    ScanRunningIo();
                    DeviceControlWork.Instance.CheckPreworkFailed();
                    ScanMotionBusFault();

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
                    }
                }
                catch (Exception ex)
                {
                    Log("IO 轮询异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(IoPollIntervalMs);
            }
            IsIOWorking = false;
        }

        /// <summary>读取物理急停位（反逻辑：安全型常闭回路，与普通 IO 相反）。</summary>
        /// <remarks>用户 2026-09-08 裁定：EMGWork 只负责急停且只看物理 IO——
        /// **没有电平信号 = 急停，有电平信号 = 不急停**（按钮按下或回路断线都表现为信号消失，故障安全）。
        /// PLC 未连接时无法判定，返回 false 不主动触发急停（互锁检查器对 PLC 未连接恒判不通过兜底）。</remarks>
        /// <returns>信号消失（急停有效）返回 true</returns>
        private bool ReadPhysicalEmg()
        {
            var cfg = _checker.Thresholds;
            var comm = GlobalCommData.mCommunicationManager;
            var plc = comm != null && comm.IsPlcEnabled ? comm.PlcManager : null;
            if (plc == null) return false;
            return !plc.ReadBit(DataType.DataBlock, cfg.PlcDb, 0, cfg.EmergencyStopBit);
        }

        /// <summary>扫描一条脉冲型虚拟 IO：命中即取走并异步分派（保证每条命令只执行一次）。</summary>
        /// <remarks>
        /// 取走（清位）与记日志都在动作执行前完成，IO 不因动作耗时而滞留；
        /// 动作交由独立线程执行，IO 扫描线程立即返回，避免初始化链等长耗时动作独占扫描。
        /// 运行中子流程类 IO（requireRunning）分派前问询运行态：整机已非 Running（停机/报警/急停）则丢弃，
        /// 避免「排队中的工作指令在停机后被执行」。
        /// </remarks>
        /// <param name="ioName">虚拟 IO 常量</param>
        /// <param name="requireRunning">true 时要求当前运行态为 Running 才分派，否则丢弃</param>
        /// <returns>命中并已分派返回 true；未置位或因运行态变更被丢弃返回 false</returns>
        private bool ScanPulseIo(string ioName, bool requireRunning = false)
        {
            var work = DeviceControlWork.Instance;
            if (!work.TakeVirtualIo(ioName)) return false;

            if (requireRunning && work.Status != MainDeviceStatus.Running)
            {
                Log("Running IO 命中 " + ioName + " 但运行态已变更为 " + work.Status + " 已丢弃", MessageLevel.Warning);
                return false;
            }

            Log("Running IO 命中 " + ioName, MessageLevel.Info);
            DispatchAction(ioName);
            return true;
        }

        /// <summary>把命中 IO 对应的动作拉到独立线程执行（同一时刻只允许一个动作在跑）。</summary>
        /// <remarks>与急停区分：急停必须同步立即生效，故仍由 EMGWork 直接执行，不走本通道。
        /// 动作执行前后各问询一次运行态：执行期间被急停 / 报警抢占时记一次变更日志，供追溯动作是否被新状态打断。</remarks>
        /// <param name="ioName">已取走的虚拟 IO 常量</param>
        private void DispatchAction(string ioName)
        {
            lock (_actionLock)
            {
                if (_actionBusy)
                {
                    Log("动作执行中 " + ioName + " 被丢弃（前一动作未结束）", MessageLevel.Warning);
                    return;
                }
                _actionBusy = true;
            }

            thAction = new Thread(() =>
            {
                MainDeviceStatus before = DeviceControlWork.Instance.Status;
                try
                {
                    DeviceControlWork.Instance.ExecuteIo(ioName);
                }
                catch (Exception ex)
                {
                    Log("动作执行异常 " + ioName + " " + ex.Message, MessageLevel.Error);
                }
                finally
                {
                    MainDeviceStatus after = DeviceControlWork.Instance.Status;
                    if (after != before)
                        Log("动作 " + ioName + " 执行期间运行态变更为 " + after, MessageLevel.Warning);
                    lock (_actionLock) { _actionBusy = false; }
                }
            });
            thAction.Name = "thAction_" + ioName;
            thAction.IsBackground = true;
            thAction.Start();
        }

        /// <summary>扫描全部 Running IO，命中后执行动作。</summary>
        /// <remarks>只关心 IO 是否置位，不区分硬件、机器人或 UI 触发源；
        /// 子流程类 IO（Sub*）要求运行态为 Running，整机被抢占后排队指令一律丢弃。</remarks>
        private void ScanRunningIo()
        {
            ScanPulseIo(DeviceControlWork.IoInit);
            ScanPulseIo(DeviceControlWork.IoReset);
            ScanPulseIo(DeviceControlWork.IoStart);
            ScanPulseIo(DeviceControlWork.IoStop);
            ScanPulseIo(DeviceControlWork.IoClearAlarm);

            ScanPulseIo(DeviceControlWork.IoSubPrework, true);
            ScanPulseIo(DeviceControlWork.IoSubWork, true);
            ScanPulseIo(DeviceControlWork.IoSubStop, true);
            ScanPulseIo(DeviceControlWork.IoSubManualStop, true);
            ScanPulseIo(DeviceControlWork.IoSubAlarmStop, true);
        }

        /// <summary>运控总线健康电平扫描：OK→NG 上升沿置虚拟 IO 并同步执行报警动作链。</summary>
        /// <remarks>
        /// 用户 2026-09-08 裁定：总线故障属普通 Alarm，检查放 IOWork（EMGWork 专责急停）；
        /// 控制器状态变量注册成电平型虚拟 IO——持续 NG 期间 IoMotionBusFault 常置，天然闭锁不重复报（ADR-035 D1）；
        /// NG→OK 不自动消警，归 ClearAlarm 链；消警（状态离开 Alarm）后闭锁自动解除，若仍 NG 在下一拍重新触发。
        /// 未初始化（IsInitialized=false）不检测，避免启动阶段误报。
        /// </remarks>
        private void ScanMotionBusFault()
        {
            var work = DeviceControlWork.Instance;

            // 已触发：闭锁。整机已离开 Alarm（消警/急停解除回 NoReset）则解除闭锁，允许下次故障重新触发
            if (work.IsVirtualIoSet(DeviceControlWork.IoMotionBusFault))
            {
                if (work.Status != MainDeviceStatus.Alarm) work.ClearVirtualIo(DeviceControlWork.IoMotionBusFault);
                return;
            }

            // 节流：50ms×4=200ms 采样一次
            _motionBusCheckBeat++;
            if (_motionBusCheckBeat < MotionBusCheckBeats) return;
            _motionBusCheckBeat = 0;

            if (!MontionManager.Instance.IsInitialized) return;

            string desc;
            bool ok;
            try
            {
                ok = MontionManager.Instance.MontionControl.CheckBusOK(out desc);
            }
            catch (Exception ex)
            {
                Log("运控总线检查异常 " + ex.Message, MessageLevel.Warning);
                return;
            }
            if (ok) return;

            // OK→NG 上升沿：根因归口记一次 Error，置电平型虚拟 IO 并同步执行报警动作链（纯状态迁移 µs 级，与急停同属同步例外）
            Log("运控总线故障 " + desc, MessageLevel.Error);
            work.SetVirtualIo(DeviceControlWork.IoMotionBusFault);
            work.ExecuteIo(DeviceControlWork.IoMotionBusFault);
        }

        /// <summary>急停轮询线程主体（原档 §2 EMG IO Work）。</summary>
        /// <remarks>用户 2026-09-08 裁定：EMGWork 只负责急停且只看物理 IO（无虚拟 IO）——
        /// 反逻辑：信号消失=急停有效（按下/断线均表现为信号消失，故障安全）；
        /// 检测到 IO 上电（设备通知上位机安全）才允许取消急停，未检测到前 EStop 状态无法清除
        /// （EstopCancel 仅在本线程信号恢复分支调用）。</remarks>
        private void EMGWork()
        {
            bool emgShown = false;
            IsEMGWorking = true;
            while (_running)
            {
                try
                {
                    var work = DeviceControlWork.Instance;

                    // 反逻辑物理急停：true=信号消失（急停有效），false=IO 上电（设备通知安全）
                    bool estopActive = ReadPhysicalEmg();
                    var status = work.Status;

                    if (estopActive && !emgShown)
                    {
                        // 上升沿：信号消失 → 同步执行急停（EstopMachine 内部直通 DoEstopMachine，无虚拟 IO）
                        emgShown = true;
                        work.EstopMachine("急停按钮按下");
                    }
                    else if (!estopActive && emgShown)
                    {
                        // 下降沿：IO 上电 = 设备通知上位机安全，才允许取消急停
                        emgShown = false;
                        if (status == MainDeviceStatus.EStop)
                        {
                            work.EstopCancel();
                            work.ReportAlarmCleared();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("急停轮询异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(EmgPollIntervalMs);
            }
            IsEMGWorking = false;
        }

        /// <summary>三色灯反射线程主体。</summary>
        /// <remarks>对应 PDF 架构图 DeviceStatusWork 泳道的 Status 反射；状态切换才写 PLC，避免每拍抖动。</remarks>
        private void LightStatusWork()
        {
            MainDeviceStatus last = MainDeviceStatus.Stop;
            IsLightStatusWorking = true;
            while (_running)
            {
                try
                {
                    var status = DeviceControlWork.Instance.Status;
                    if (status != last)
                    {
                        last = status;
                        DriveTricolorLamp(status);
                        // 状态切换不记日志（ADR-032 报错分层：三色灯动作本身即对外呈现）
                    }
                }
                catch (Exception ex)
                {
                    Log("状态反射异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(StatusPollIntervalMs);
            }
            IsLightStatusWorking = false;
        }

        /// <summary>驱动三色灯 PLC 输出，未配置不写。</summary>
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
                Log("三色灯输出异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>等待线程退出（最多 1s）。</summary>
        /// <param name="thread">待汇合的线程</param>
        private void JoinThread(Thread thread)
        {
            if (thread != null && thread.IsAlive) thread.Join(1000);
        }

        #endregion

        #region 公共函数

        /// <summary>实例化三条监控线程并 Start（幂等）。</summary>
        /// <remarks>thIOWork AboveNormal，thEMGWork Highest，thLightStatusWork Normal。</remarks>
        public void MachineIOWorkOpen()
        {
            if (_running) return;
            _running = true;
            _alarmActive = false;

            thIOWork = new Thread(IOWork)
            {
                Name = "thIOWork",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            thIOWork.Start();

            thEMGWork = new Thread(EMGWork)
            {
                Name = "thEMGWork",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            thEMGWork.Start();

            thLightStatusWork = new Thread(LightStatusWork)
            {
                Name = "thLightStatusWork",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            thLightStatusWork.Start();

            Log("安全/IO/状态监控启动", MessageLevel.Info);
        }

        /// <summary>停止三条监控线程（置标志位 + 有限等待，不 Abort）。</summary>
        public void MachineIOWorkClose()
        {
            if (!_running) return;
            _running = false;
            JoinThread(thIOWork);
            JoinThread(thEMGWork);
            JoinThread(thLightStatusWork);
            thIOWork = null;
            thEMGWork = null;
            thLightStatusWork = null;
            _alarmActive = false;
            Log("安全/IO/状态监控停止", MessageLevel.Info);
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
            MachineIOWorkClose();
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

    /// <summary>安全互锁检查器：硬件与设备态两类互锁。</summary>
    /// <remarks>急停由 DeviceStatusWork.EMGWork 专责处置，此处不检查急停位。</remarks>
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
