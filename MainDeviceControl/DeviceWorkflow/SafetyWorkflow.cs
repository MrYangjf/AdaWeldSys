using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.PLC.Siemens;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.ProductFileManager;
using S7.Net;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>安全检查流程状态枚举</summary>
    public enum SafetyWorkflowState
    {
        /// <summary>未启动监控</summary>
        Uninitialized,

        /// <summary>监控中，互锁检查通过</summary>
        Monitoring,

        /// <summary>互锁检查失败，已报警急停</summary>
        Alarm,

        /// <summary>已停止监控</summary>
        Stopped
    }

    /// <summary>
    /// 安全检查流程（单例）：周期执行安全互锁检查，失败即报警急停并记录故障。
    /// 由三部分内聚而成——配置（<see cref="SafetyFlow"/> 阈值与 PLC 地址）、
    /// 判定（<see cref="SafetyInterlockChecker"/>）、执行（本类高优先级监控循环）。
    /// PLC 地址与阈值需现场标定，默认值为占位。
    ///
    /// 步骤驱动（ADR-047）：原自建监控线程已并入基类常驻监听线程（BeatMs=50，优先级 Highest）。
    /// 执行步段位 10 周期互锁检查 / 900 报警收尾（持续检查直至恢复）。
    /// 注：本流程是「周期节拍」而非「等待完成」，Thread.Sleep 节拍由基类承载，不属红线 3 禁止的步内阻塞。
    /// </summary>
    public class SafetyWorkflow : DeviceWorkflowBase<SafetyWorkflowState>
    {
        #region 常量

        // 执行步段位（0 待机 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepCheck = 10;

        /// <summary>监控轮询周期（毫秒，20Hz）</summary>
        private const int PollIntervalMs = 50;

        #endregion

        #region 私有变量

        private static readonly Lazy<SafetyWorkflow> _lazyInstance =
            new Lazy<SafetyWorkflow>(() => new SafetyWorkflow());

        private readonly string _tag = "安全检查流程";

        private readonly SafetyInterlockChecker _checker;

        private volatile bool _running;
        private volatile bool _alarmRecorded;

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static SafetyWorkflow Instance => _lazyInstance.Value;

        /// <summary>设备名称（日志与事件标识）</summary>
        public override string StateName => _tag;

        /// <summary>仅监控启动后才跑业务；停止/未初始化时空转并刷新步时间戳。</summary>
        protected override bool IsRunLoopActive { get { return _running; } }

        /// <summary>监控线程是否运行中</summary>
        public bool IsRunning => _running;

        /// <summary>最近一次互锁检查结果（由监控循环刷新）</summary>
        public SafetyInterlockResult LastResult { get; private set; }

        /// <summary>互锁阈值与 PLC 地址配置</summary>
        public SafetyFlow Config => _checker.Thresholds;

        /// <summary>当前是否安全。监控未启动时不门控（返回 true），</summary>
        /// <remarks>启动后以最近一次互锁检查结果为准，无结果视为不安全。</remarks>
        public bool IsSafe
        {
            get
            {
                if (!_running) return true;
                return LastResult != null && LastResult.IsPassed;
            }
        }

        #endregion

        #region 构造函数

        private SafetyWorkflow() : base(SafetyWorkflowState.Uninitialized)
        {
            _checker = new SafetyInterlockChecker();
            // 安全监控为最高优先级，节拍 20Hz（取代原自建 SafetyMonitor 线程）
            BeatMs = PollIntervalMs;
            RunLoopPriority = ThreadPriority.Highest;
        }

        #endregion

        #region 私有函数

        /// <summary>启动监控（复用基类常驻监听线程）</summary>
        private void StartMonitor()
        {
            if (_running) return;
            _running = true;
            StartRunLoop();
            SetStep(SafetyWorkflowState.Monitoring, "安全监控启动");
        }

        /// <summary>停止监控</summary>
        private void StopMonitor()
        {
            if (!_running) return;
            _running = false;
            StopRunLoop();
            SetStep(SafetyWorkflowState.Stopped, "安全监控停止");
        }

        /// <summary>执行步 10：执行一次互锁检查。</summary>
        /// <remarks>通过则留在原步等下一拍；不通过转 900 报警收尾（不重复报警）。</remarks>
        private void DoCheck()
        {
            var result = _checker.Check();
            LastResult = result;
            if (result.IsPassed)
            {
                _alarmRecorded = false;
                return;
            }
            GoStep(StepFinishFail);
        }

        /// <summary>执行步 900：报警收尾并持续检查直至恢复。</summary>
        /// <remarks>首次进入触发急停与故障记录（_alarmRecorded 保证一个报警周期只记一次）。</remarks>
        private void DoAlarm()
        {
            var result = _checker.Check();
            LastResult = result;

            if (result.IsPassed)
            {
                _alarmRecorded = false;
                SetStep(SafetyWorkflowState.Monitoring, "安全互锁恢复");
                return;
            }

            if (!_alarmRecorded)
            {
                _alarmRecorded = true;
                TriggerAlarm(result);
            }
            SetStep(SafetyWorkflowState.Alarm, result.Reason);
        }

        /// <summary>处置安全报警</summary>
        /// <param name="result">失败的互锁检查结果</param>
        private void TriggerAlarm(SafetyInterlockResult result)
        {
            EmergencyStop();
            FaultRecoveryManager.Instance.RecordFault("Safety", new FaultRecord
            {
                Device = StateName,
                State = State,
                Category = FaultCategory.Safety,
                ErrorCode = "INTERLOCK_FAILED",
                ParamSnapshot = result.Reason
            });
        }

        #endregion

        #region 公共函数

        /// <summary>流程态映射为设备四态</summary>
        /// <param name="step">安全检查流程状态</param>
        /// <returns>监控中映射 Work，报警映射 Alarm，未初始化与已停止映射 Disconnect</returns>
        protected override DeviceState MapToDeviceState(SafetyWorkflowState step)
        {
            switch (step)
            {
                case SafetyWorkflowState.Monitoring:
                    return DeviceState.Work;
                case SafetyWorkflowState.Alarm:
                    return DeviceState.Alarm;
                default:
                    return DeviceState.Disconnect;
            }
        }

        /// <summary>初始化安全监控</summary>
        /// <returns>初始化是否成功，检查器在构造时已就绪恒为 true</returns>
        protected override bool InitializeFlow()
        {
            return true;
        }

        /// <summary>手动开启安全监控</summary>
        protected override void ManualOn()
        {
            StartMonitor();
        }

        /// <summary>手动关闭安全监控</summary>
        protected override void ManualOff()
        {
            StopMonitor();
        }

        /// <summary>自动运行安全监控。</summary>
        /// <remarks>安全监控是「周期节拍型」而非「等待完成型」（ADR-048 注记）：启动监控后自动序列即返回，
        /// 互锁检查由监听线程 FlowProcess 持续承载（BeatMs=50），不占用自动流程线程阻塞等待。</remarks>
        protected override void AutoRunFlow()
        {
            StartMonitor();
        }

        /// <summary>流程复位：清除报警标志并回到监控中（若监控已启动）。</summary>
        /// <remarks>由基类公共入口 Reset 调用（ADR-048）。</remarks>
        protected override void ResetFlow()
        {
            IsAlarm = false;
            _alarmRecorded = false;
            if (_running)
                SetStep(SafetyWorkflowState.Monitoring, "安全报警复位");
        }

        /// <summary>读取设备安全互锁状态</summary>
        /// <returns>最近一次互锁检查是否安全</returns>
        protected override bool CheckDeviceIOSafe()
        {
            return IsSafe;
        }

        /// <summary>启动安全监控</summary>
        public void Start()
        {
            ManualStart();
        }

        /// <summary>停止安全监控</summary>
        public void Stop()
        {
            ManualStop();
        }

        /// <summary>执行安全互锁检查</summary>
        /// <returns>互锁检查结果，含是否通过与未通过项</returns>
        public SafetyInterlockResult Check()
        {
            var result = _checker.Check();
            LastResult = result;
            return result;
        }

        /// <summary>复位安全报警（转接基类 Reset → ResetFlow，ADR-048）。</summary>
        public void Reset()
        {
            ResetFlow();
        }

        #endregion

        #region 阶段态变更钩子（阶段态切换时复位执行步到该阶段入口）

        /// <summary>阶段态切换时把执行步复位到该阶段入口步。</summary>
        /// <remarks>注意：本方法在 SetStep 同步路径内执行，调用处 SetStep 后须立即 return。</remarks>
        /// <param name="e">流程态变更参数</param>
        protected override void OnStepChanged(WorkflowStepChangedEventArgs<SafetyWorkflowState> e)
        {
            base.OnStepChanged(e);
            switch (e.NewState)
            {
                case SafetyWorkflowState.Monitoring:
                    GoStep(StepCheck);
                    break;
                case SafetyWorkflowState.Alarm:
                    GoStep(StepFinishFail);
                    break;
                default:
                    GoStep(StepIdle);
                    break;
            }
        }

        #endregion

        #region 执行步分派

        /// <summary>执行步分派。</summary>
        /// <remarks>安全流程是周期检查而非等待完成，每拍由基类节拍（50ms）驱动重入。</remarks>
        protected override void FlowProcess()
        {
            switch (WorkStep)
            {
                case StepCheck: DoCheck(); break;
                case StepFinishFail: DoAlarm(); break;
                default:
                    // StepIdle 与未登记步号：空转等待监控启动
                    break;
            }
        }

        /// <summary>执行步号转中文名。</summary>
        /// <remarks>新增步必须登记，否则该步不可观测。</remarks>
        /// <param name="step">执行步号</param>
        /// <returns>步号对应的中文名，未登记步返回含步号的占位文本</returns>
        protected override string GetStepName(int step)
        {
            switch (step)
            {
                case StepIdle: return "监控未启动";
                case StepCheck: return "周期互锁检查";
                case StepFinishFail: return "报警收尾-等待恢复";
                default: return string.Format("未登记步({0})", step);
            }
        }

        #endregion

        #region 监听线程钩子与释放

        /// <summary>释放钩子（基类 Dispose 调用，ADR-034）：停止监控。</summary>
        protected override void DisposeManaged()
        {
            StopMonitor();
        }

        #endregion
    }

    /// <summary>安全互锁阈值与 PLC 地址映射。</summary>
    /// <remarks>数值为 PLC 原始单位（WORD/REAL 经站点标定后填入），阈值比较使用原始单位。</remarks>
    public class SafetyFlow
    {
        /// <summary>保护气压下限（PLC 原始单位）</summary>
        public double MinGasPressure { get; set; } = 40;

        /// <summary>冷却水流量下限（PLC 原始单位）</summary>
        public double MinCoolantFlow { get; set; } = 10;

        /// <summary>冷却水温度上限（PLC 原始单位）</summary>
        public double MaxCoolantTemperature { get; set; } = 35;

        /// <summary>激光头温度上限（PLC 原始单位）</summary>
        public double MaxHeadTemperature { get; set; } = 60;

        /// <summary>是否要求自动模式（PLC 模式字 = 1）</summary>
        public bool RequireAutoMode { get; set; } = true;

        /// <summary>PLC DB 块号</summary>
        public int PlcDb { get; set; } = 1;

        /// <summary>急停按钮位索引</summary>
        public int EmergencyStopBit { get; set; } = 0;

        /// <summary>安全门位索引</summary>
        public int SafetyDoorBit { get; set; } = 1;

        /// <summary>保护气压 WORD 字节地址</summary>
        public int GasPressureWord { get; set; } = 10;

        /// <summary>冷却水流量 WORD 字节地址</summary>
        public int CoolantFlowWord { get; set; } = 12;

        /// <summary>冷却水温度 WORD 字节地址</summary>
        public int CoolantTempWord { get; set; } = 14;

        /// <summary>激光头温度 WORD 字节地址</summary>
        public int HeadTempWord { get; set; } = 16;

        /// <summary>模式字 WORD 字节地址（1=自动）</summary>
        public int ModeWord { get; set; } = 20;
    }

    /// <summary>安全互锁检查结果</summary>
    public class SafetyInterlockResult
    {
        /// <summary>是否全部通过</summary>
        public bool IsPassed { get; set; }

        /// <summary>未通过项说明列表</summary>
        public List<string> FailedItems { get; } = new List<string>();

        /// <summary>汇总原因</summary>
        public string Reason { get; set; }

        /// <summary>检查时间戳</summary>
        public DateTime CheckTime { get; set; } = DateTime.Now;
    }

    /// <summary>安全互锁检查器：覆盖硬件（急停/安全门/气压/冷却水/温度/模式）</summary>
    /// <remarks>与设备态（四个子设备 Connect、机器人在线）两类互锁。 阈值与地址来自 <see cref="SafetyFlow"/>，需现场标定；设备就绪判定统一为 DeviceState.Connect。</remarks>
    public class SafetyInterlockChecker
    {
        #region 私有变量

        private readonly SafetyFlow _thresholds;

        #endregion

        #region 公共变量

        /// <summary>当前生效的阈值与地址配置</summary>
        public SafetyFlow Thresholds => _thresholds;

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

        /// <summary>检查硬件互锁项</summary>
        /// <param name="plc">PLC 通讯实例</param>
        /// <param name="result">待写入的检查结果</param>
        private void CheckPlc(S7PLCCommunication plc, SafetyInterlockResult result)
        {
            var t = _thresholds;
            try
            {
                // 急停位为 1 表示已按下
                if (plc.ReadBit(DataType.DataBlock, t.PlcDb, 0, t.EmergencyStopBit))
                {
                    result.FailedItems.Add("急停按钮已按下");
                }
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
            if (device.State != DeviceState.Connect)
            {
                result.FailedItems.Add(name + " 未就绪 状态 " + device.State);
            }
        }

        #endregion

        #region 公共函数

        /// <summary>执行硬件与设备态互锁检查</summary>
        /// <returns>互锁检查结果，含是否通过与未通过项</returns>
        public SafetyInterlockResult Check()
        {
            var result = new SafetyInterlockResult();

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
