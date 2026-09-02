using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.Robot.KUKARobot;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.LaserWeldHead;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.LineLaserCamApi;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.ProductFileManager;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>主设备运行态变更事件参数。</summary>
    public class MainDeviceStatusChangedEventArgs : EventArgs
    {
        /// <summary>切换前运行态。</summary>
        public MainDeviceStatus OldStatus { get; set; }

        /// <summary>切换后运行态。</summary>
        public MainDeviceStatus NewStatus { get; set; }

        /// <summary>切换原因。</summary>
        public string Reason { get; set; }

        /// <summary>变更时间。</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 主设备运行管控（单例）：整机运行编排与机器人/PLC 信号交互（新主控设计，参考 PcomDeviceInterface.MachineControlWork）。
    ///
    /// 设计要点：
    ///   1. 独立管控类，不继承 DeviceWorkflowBase（子设备流程基类）；对外状态为 MainDeviceStatus（6 态），
    ///      原流程态 MainDeviceFlowState 不再存在，转为内部私有执行步常量。
    ///   2. 与 PLC/机器人的"连接"由顶层 Comm 通讯管理（CommunicationManager）管控，
    ///      本类只做"信号交互"——接收 RobotPose、切换运行态、级联子设备 ConnectOn/ConnectOff、下发 DeviceCommand。
    ///   3. 命令变量三槽分存：_pendingAbort &gt; _pendingCommand &gt; _pendingPose
    ///      （RobotPose.StartSafePose=1 与 RobotCommand.PreWeldRequest=1 编码重叠，单槽必丢指令）。
    ///   4. 对外操作契约：StartMachine / StopMachine / ResetMachine / ClearAlarm / EStopMachine / EStopCancel；
    ///      上电初始化级联 = InitializeMachine（焊接头 → 运动控制器 → 线激光 → 监控相机 → 机器人通讯）。
    ///   5. 模拟模式控制器由原 SimulationWorkflow 归并至本单例（ADR-048）：模拟仍是 MODE 不是线程。
    ///
    /// 执行步段位：0 待机 · 10~19 焊前准备 · 20~29 焊前位跟踪 · 30~39 焊接中 · 40~49 停止 · 800 成功收尾 · 900 失败收尾。
    /// </summary>
    public class DeviceControlWork
    {
        #region 常量

        /// <summary>执行步 10：焊前准备-等待子设备 Connected 就绪。</summary>
        private const int StepPreworkConnect = 10;

        /// <summary>执行步 11：焊前准备-等待机器人上报焊前位。</summary>
        private const int StepWaitPreWeldPose = 11;

        /// <summary>执行步 20：Y 轴偏移跟踪。</summary>
        private const int StepYAxisTracking = 20;

        /// <summary>执行步 21：等待机器人上报焊接起始位。</summary>
        private const int StepWaitWeldStart = 21;

        /// <summary>执行步 30：进入焊接态并启动子设备工作。</summary>
        private const int StepStartWelding = 30;

        /// <summary>执行步 31：焊接中-等待预停位或焊接停止位。</summary>
        private const int StepWelding = 31;

        /// <summary>执行步 40：停止-级联断开并下发回安全位。</summary>
        private const int StepStopping = 40;

        /// <summary>通用执行步号：成功收尾。</summary>
        private const int StepFinishOk = 800;

        /// <summary>通用执行步号：失败收尾。</summary>
        private const int StepFinishFail = 900;

        /// <summary>通用执行步号：待机。</summary>
        private const int StepIdle = 0;

        /// <summary>子设备 Connected 级联等待超时（毫秒）。</summary>
        private const double CascadeConnectTimeoutMs = 10000;

        /// <summary>等待机器人到位 Pose 超时（毫秒）。</summary>
        private const double PoseWaitTimeoutMs = 60000;

        /// <summary>Y 轴偏移跟踪超时（毫秒）。</summary>
        private const double YAxisTrackingTimeoutMs = 5000;

        /// <summary>命令变量空值：无待处理机器人 Pose。</summary>
        private const int NoPendingPose = -1;

        /// <summary>命令变量空值：无待处理机器人主动指令（RobotCommand.None）。</summary>
        private const int NoPendingCommand = 0;

        /// <summary>运行线程节拍（毫秒）。</summary>
        private const int BeatMs = 10;

        #endregion

        #region 私有变量

        private static readonly Lazy<DeviceControlWork> _lazyInstance =
            new Lazy<DeviceControlWork>(() => new DeviceControlWork());

        private readonly string _tag = "主设备运行管控";

        private MainDeviceStatus _status = MainDeviceStatus.Stop;
        private readonly object _statusLock = new object();

        private volatile bool _running;
        private volatile bool _initializing;

        private Thread _runLoopThread;
        private volatile bool _runLoopClosing;

        /// <summary>当前执行步号（内部游标）。</summary>
        private volatile int _workStep;

        /// <summary>当前步进入时间戳（Ticks，Interlocked 读写保证 64 位原子，避免 DateTime 撕裂）。</summary>
        private long _workStartTicks;

        private string _failReason = "";
        private int _failStep;

        /// <summary>命令变量：待处理的机器人 Pose（NoPendingPose = 无）。通讯回调只写，运行线程消费。</summary>
        private volatile int _pendingPose = NoPendingPose;

        /// <summary>命令变量：待处理的机器人主动指令（NoPendingCommand = 无）。</summary>
        private volatile int _pendingCommand = NoPendingCommand;

        /// <summary>命令变量：待处理的急停请求（Abort 优先级最高，独立存放避免被 Pose 覆盖）。</summary>
        private volatile bool _pendingAbort;

        /// <summary>最近一次到达的机器人 Pose（步间传值走流程字段，局部变量跨拍必然丢失）。</summary>
        private volatile RobotPose _lastPose = RobotPose.Unknown;

        /// <summary>焊前请求标记（机器人 PreWeldRequest 与 StartSafePose 配合触发 Preworking）。</summary>
        private volatile bool _pendingPreWeldRequest;

        /// <summary>机器人通讯管理器（上电初始化第 5 步就绪后赋值，来自顶层 Comm）。</summary>
        private KUKARobotManager RobotManager { get; set; }

        // 级联目标（子设备）
        private readonly LineLaserWorkflow _lineLaser = LineLaserWorkflow.Instance;
        private readonly LaserWeldHeadWorkflow _weldHead = LaserWeldHeadWorkflow.Instance;
        private readonly MonitorCameraWorkflow _monitorCam = MonitorCameraWorkflow.Instance;
        private readonly MotionControlWorkflow _motion = MotionControlWorkflow.Instance;

        // ---- 模拟模式（归并自 SimulationWorkflow，ADR-048） ----

        /// <summary>模拟模式日志标签（保持原 SimulationWorkflow 日志归类）。</summary>
        private readonly string _simTag = "模拟模式控制器";

        private readonly object _robotXLock = new object();

        // 模拟模式（None=关闭；其余为开启）——运行时当前是否处于模拟中
        private SimTestMode _currentMode = SimTestMode.None;

        // 用户上次选择的模式（仅界面记忆，不参与"是否开启"判定；持久化后重启显示上次选择）
        private SimTestMode _selectedMode = SimTestMode.None;

        // 共享模拟 robotX 时钟
        private bool _robotXSimEnabled;
        private double _simStartX;
        private double _simSpeed;
        private DateTime _simStartTime;

        // 模拟参数（UI 设置，持久化）
        private double _simStartXParam;
        private double _simSpeedParam;
        private double _simTargetDistance;
        private SimSeamProfile _seamProfile;
        private bool _randomMode;

        // INI 文件名沿用历史名 SimulationController.ini，保证既有用户配置无需迁移
        private static readonly string _simConfigFile =
            Path.Combine(Application.StartupPath, "Config", "INI", "SimulationController.ini");

        // 旧版配置文件（MotionControlWorkflow.ini）：新文件缺失且旧文件存在时作为回退来源
        private static readonly string _simLegacyConfigFile =
            Path.Combine(Application.StartupPath, "Config", "INI", "MotionControlWorkflow.ini");

        /// <summary>线激光模拟启动挂起标记：ConnectOn 后台线程转 Connected 后再 Start。</summary>
        private bool _simLineLaserPending;

        #endregion

        #region 公共变量

        /// <summary>单例实例。</summary>
        public static DeviceControlWork Instance { get { return _lazyInstance.Value; } }

        /// <summary>主设备运行态（6 态，单控制源，经 SetStatus 变更并触发 StatusChanged 事件）。</summary>
        public MainDeviceStatus Status
        {
            get { lock (_statusLock) { return _status; } }
        }

        /// <summary>主设备运行态变更事件。</summary>
        public event EventHandler<MainDeviceStatusChangedEventArgs> StatusChanged;

        /// <summary>当前执行步号。</summary>
        public int WorkStep { get { return _workStep; } }

        /// <summary>当前执行步的中文名。</summary>
        public string WorkStepName { get { return GetStepName(_workStep); } }

        /// <summary>是否焊接中（运行态 Running 且执行步处于焊接段）。</summary>
        public bool IsWelding
        {
            get { return Status == MainDeviceStatus.Running && _workStep >= StepStartWelding && _workStep <= StepWelding; }
        }

        // ---- 模拟模式（归并自 SimulationWorkflow，ADR-048） ----

        /// <summary>当前模拟模式（None 表示未开启）</summary>
        public SimTestMode CurrentMode
        {
            get { lock (_robotXLock) { return _currentMode; } }
        }

        /// <summary>用户上次选择的模式（界面记忆，仅用于下拉框默认显示，不参与"是否开启"判定）</summary>
        public SimTestMode SelectedMode
        {
            get { lock (_robotXLock) { return _selectedMode; } }
            set { lock (_robotXLock) { _selectedMode = value; } }
        }

        /// <summary>模拟模式是否开启</summary>
        public bool IsSimulationEnabled
        {
            get { lock (_robotXLock) { return _currentMode != SimTestMode.None; } }
        }

        /// <summary>是否应使用虚拟调试相机（A/B 用虚拟，C 用真实但仍属模拟模式）</summary>
        public bool ShouldUseVirtualCamera
        {
            get { lock (_robotXLock) { return _currentMode == SimTestMode.ModeA || _currentMode == SimTestMode.ModeB; } }
        }

        /// <summary>运控是否应向运动轴输出实际位移指令。</summary>
        /// <remarks>模拟模式：仅 B（虚拟+运动）为 true，A/C 仅记录不运动； 非模拟（生产）流程：始终输出运动指令。</remarks>
        public bool ShouldMoveAxis
        {
            get
            {
                lock (_robotXLock)
                {
                    if (_currentMode == SimTestMode.None)
                        return true; // 生产流程默认运动
                    return _currentMode == SimTestMode.ModeB;
                }
            }
        }

        /// <summary>模拟起点 X（mm，UI 设置）</summary>
        public double SimStartX
        {
            get { lock (_robotXLock) { return _simStartXParam; } }
            set { lock (_robotXLock) { _simStartXParam = value; } }
        }

        /// <summary>模拟运动速度（mm/s，UI 设置）</summary>
        public double SimSpeed
        {
            get { lock (_robotXLock) { return _simSpeedParam; } }
            set { lock (_robotXLock) { _simSpeedParam = value; } }
        }

        /// <summary>模拟目标距离（mm，UI 设置）</summary>
        public double SimTargetDistance
        {
            get { lock (_robotXLock) { return _simTargetDistance; } }
            set { lock (_robotXLock) { _simTargetDistance = value; } }
        }

        /// <summary>虚拟相机焊缝轮廓类型（UI 设置，套用至 VirtualCameraRun）</summary>
        public SimSeamProfile SeamProfile
        {
            get { lock (_robotXLock) { return _seamProfile; } }
            set { lock (_robotXLock) { _seamProfile = value; } }
        }

        /// <summary>模拟数据随机模式（UI 设置，套用至 VirtualCameraRun）</summary>
        public bool RandomMode
        {
            get { lock (_robotXLock) { return _randomMode; } }
            set { lock (_robotXLock) { _randomMode = value; } }
        }

        #endregion

        #region 构造函数

        private DeviceControlWork()
        {
            _workStartTicks = DateTime.Now.Ticks;
            // 订阅顶层 Comm 的通讯指令（机器人 Pose/Command 经此到达；回调只写命令变量，不做业务）
            GlobalCommData.CommunicationCommandReceived += OnCommunicationCommand;
            Log("主设备运行管控实例化", MessageLevel.Info);
        }

        #endregion

        #region 私有函数

        /// <summary>运行线程主体：按节拍消费命令变量并单步分派。</summary>
        /// <remarks>单拍异常只记日志不退出，避免整条流程永久停摆。</remarks>
        private void RunLoop()
        {
            while (!_runLoopClosing)
            {
                try
                {
                    if (_running)
                    {
                        ConsumeCommand();
                        FlowProcess();
                    }
                    else
                    {
                        // 非运行态必须持续刷新步时间戳：否则停机数分钟后重启，
                        // 首拍就会用停机时刻的时间戳判超时，直接误跳失败收尾
                        ResetStepTime();
                    }
                }
                catch (Exception ex)
                {
                    Log("运行线程异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(BeatMs);
            }
        }

        /// <summary>推进执行步（步号与时间成对刷新）。</summary>
        /// <param name="step">目标执行步号</param>
        private void GoStep(int step)
        {
            _workStep = step;
            Interlocked.Exchange(ref _workStartTicks, DateTime.Now.Ticks);
        }

        /// <summary>刷新当前步进入时间（非运行态每拍调用，防止重启瞬间误判超时）。</summary>
        private void ResetStepTime()
        {
            Interlocked.Exchange(ref _workStartTicks, DateTime.Now.Ticks);
        }

        /// <summary>判断当前步停留是否超时。</summary>
        /// <param name="timeoutMs">超时阈值（毫秒）</param>
        /// <returns>停留超过阈值返回 true</returns>
        private bool IsStepTimeout(double timeoutMs)
        {
            return (DateTime.Now - new DateTime(Interlocked.Read(ref _workStartTicks))).TotalMilliseconds > timeoutMs;
        }

        /// <summary>转入失败收尾并跳 900 段。</summary>
        /// <param name="reason">失败原因（纯文本）</param>
        private void FailFlow(string reason)
        {
            _failStep = _workStep;
            _failReason = reason;
            GoStep(StepFinishFail);
        }

        /// <summary>切换主设备运行态（单控制源，触发 StatusChanged 事件并记日志）。</summary>
        /// <param name="newStatus">目标运行态</param>
        /// <param name="reason">切换原因</param>
        private void SetStatus(MainDeviceStatus newStatus, string reason)
        {
            MainDeviceStatus old;
            lock (_statusLock)
            {
                if (_status == newStatus) return;
                old = _status;
                _status = newStatus;
            }
            var handler = StatusChanged;
            if (handler != null)
            {
                handler(this, new MainDeviceStatusChangedEventArgs
                {
                    OldStatus = old,
                    NewStatus = newStatus,
                    Reason = reason,
                    Timestamp = DateTime.Now
                });
            }
            Log(string.Format("运行态切换 {0} -> {1} 原因 {2}", old, newStatus, reason));
        }

        /// <summary>执行步 0：待机。</summary>
        /// <remarks>就绪（Stop）且收到焊前请求才发起焊前准备。</remarks>
        private void DoIdle()
        {
            if (Status != MainDeviceStatus.Stop || !_pendingPreWeldRequest) return;
            _pendingPreWeldRequest = false;
            SetStatus(MainDeviceStatus.Running, "机器人请求焊前准备");
            CascadeConnect();
            GoStep(StepPreworkConnect);
        }

        /// <summary>执行步 10：等子设备 Connected 就绪后下发去焊前位指令。</summary>
        /// <remarks>失败重试零代码：条件不满足即返回、步号不变，下一拍重入重试。</remarks>
        private void DoPreworkConnect()
        {
            if (!IsChildConnected(_lineLaser) || !IsChildConnected(_motion))
            {
                if (IsStepTimeout(CascadeConnectTimeoutMs))
                    FailFlow("子设备连接级联超时");
                return;
            }
            SendDeviceCommand(DeviceCommand.CmdGoPreWeld);
            GoStep(StepWaitPreWeldPose);
        }

        /// <summary>步 11：等机器人上报焊前位到位。</summary>
        private void DoWaitPreWeldPose()
        {
            if (_lastPose == RobotPose.PreWeldPose)
            {
                GoStep(StepYAxisTracking);
                return;
            }
            if (IsStepTimeout(PoseWaitTimeoutMs))
                FailFlow("等待机器人焊前位超时");
        }

        /// <summary>步 20：执行 Y 轴偏移跟踪，成功后下发 CmdOK 允许机器人继续。</summary>
        private void DoYAxisTracking()
        {
            if (!_motion.BeginYAxisTracking())
            {
                if (IsStepTimeout(YAxisTrackingTimeoutMs))
                    FailFlow("Y 轴偏移跟踪超时");
                return;
            }
            SendDeviceCommand(DeviceCommand.CmdOK);
            GoStep(StepWaitWeldStart);
        }

        /// <summary>步 21：等机器人上报焊接起始位。</summary>
        private void DoWaitWeldStart()
        {
            if (_lastPose == RobotPose.WeldStartPose)
            {
                GoStep(StepStartWelding);
                return;
            }
            if (IsStepTimeout(PoseWaitTimeoutMs))
                FailFlow("等待机器人焊接起始位超时");
        }

        /// <summary>进入焊接态，启动子设备工作。</summary>
        private void DoStartWelding()
        {
            try { _lineLaser.Start(); }
            catch (Exception ex)
            {
                Log("线激光进入工作异常 " + ex.Message, MessageLevel.Error);
            }
            GoStep(StepWelding);
        }

        /// <summary>执行步 31：焊接中。</summary>
        /// <remarks>收到预停位转停止段；收到焊接停止位直接成功收尾。</remarks>
        private void DoWelding()
        {
            if (_lastPose == RobotPose.PreStopPose)
            {
                SendDeviceCommand(DeviceCommand.CmdGoPreStop);
                GoStep(StepStopping);
                return;
            }
            if (_lastPose == RobotPose.WeldStopPose)
            {
                CascadeDisconnect();
                SendDeviceCommand(DeviceCommand.CmdGoWeldStop);
                GoStep(StepFinishOk);
            }
        }

        /// <summary>步 40 停止：级联子设备断开并下发回安全位。</summary>
        private void DoStopping()
        {
            CascadeDisconnect();
            SendDeviceCommand(DeviceCommand.CmdGoStopSafe);
            GoStep(StepFinishOk);
        }

        /// <summary>步 800 成功收尾：置停止并回待机。</summary>
        private void DoFinishOk()
        {
            SetStatus(MainDeviceStatus.Stop, "焊接流程正常收尾");
            _lastPose = RobotPose.Unknown;
            GoStep(StepIdle);
        }

        /// <summary>执行步 900：失败收尾。</summary>
        /// <remarks>级联停机并记故障；为超时三步走的第三步。</remarks>
        private void DoFinishFail()
        {
            SetStatus(MainDeviceStatus.EStop, "FailReason");
            EmergencyStop();
            FaultRecoveryManager.Instance.RecordFault("MainDevice", new FaultRecord
            {
                Device = _tag,
                State = SubDeviceWeldStatus.ErrorAborted,
                Category = FaultCategory.System,
                ErrorCode = "FLOW_TIMEOUT",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", "FailStep", "FailReason")
            });
            CascadeDisconnect();
            SendDeviceCommand(DeviceCommand.CmdAbort);
            _lastPose = RobotPose.Unknown;
            GoStep(StepIdle);
        }

        /// <summary>接收机器人 Pose 上报。</summary>
        /// <remarks>全局复位类 Pose 立即响应；其余只更新 _lastPose，由 FlowProcess 按执行步消费。</remarks>
        /// <param name="pose">本次到达的机器人姿态</param>
        private void OnPoseArrived(RobotPose pose)
        {
            switch (pose)
            {
                case RobotPose.HomePose:
                    SetStatus(MainDeviceStatus.Stop, "机器人归零位");
                    CascadeDisconnect();
                    SendDeviceCommand(DeviceCommand.CmdGoHome);
                    GoStep(StepIdle);
                    break;
                case RobotPose.StopSafePose:
                    SetStatus(MainDeviceStatus.Stop, "机器人回到安全停止位");
                    CascadeDisconnect();
                    SendDeviceCommand(DeviceCommand.CmdGoStopSafe);
                    GoStep(StepIdle);
                    break;
                case RobotPose.StartSafePose:
                    if (Status != MainDeviceStatus.Running)
                        SetStatus(MainDeviceStatus.Stop, "机器人进入安全起始位");
                    break;
            }
        }

        /// <summary>校验机器人姿态是否安全</summary>
        /// <param name="pose">机器人上报的姿态</param>
        /// <returns>允许接受返回 true，安全监控已启动且互锁未通过返回 false</returns>
        private bool IsRobotPoseSafe(RobotPose pose)
        {
            return DeviceStatusWork.Instance.IsSafe;
        }

        /// <summary>触发急停（机器人主动 Abort 等场景）。</summary>
        /// <param name="reason">急停原因（纯文本）</param>
        private void TriggerAbort(string reason)
        {
            EStopMachine(reason);
        }

        /// <summary>级联各子设备连接。</summary>
        private void CascadeConnect()
        {
            try { _lineLaser.ConnectOn(); }
            catch (Exception ex) { Log("线激光连接异常 " + ex.Message, MessageLevel.Error); }
            try { _motion.ConnectOn(); }
            catch (Exception ex) { Log("运控连接异常 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>级联子设备断开。</summary>
        private void CascadeDisconnect()
        {
            try { _lineLaser.ConnectOff(); }
            catch (Exception ex) { Log("线激光断开异常 " + ex.Message, MessageLevel.Error); }
            try { _motion.ConnectOff(); }
            catch (Exception ex) { Log("运控断开异常 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>子设备是否已连接。</summary>
        /// <param name="child">子设备状态对象</param>
        /// <returns>子设备处于 Connected 返回 true</returns>
        private bool IsChildConnected(DeviceStateBase child)
        {
            return child.State == SubDeviceState.Connected;
        }

        /// <summary>下发设备指令至机器人。</summary>
        /// <remarks>经顶层 Comm 的机器人通讯下发，连接本身由 Comm 管控。</remarks>
        /// <param name="cmd">下发给机器人的设备指令</param>
        private void SendDeviceCommand(DeviceCommand cmd)
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null) return;
                if (comm.IsRobotEnabled && comm.RobotManager != null)
                {
                    comm.RobotManager.SendRobotData(new KUKARobotData { EStr = ((int)cmd).ToString() });
                }
            }
            catch (Exception ex)
            {
                Log("下发设备指令异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>处理通讯字符串指令。</summary>
        /// <remarks>只解码并写入命令变量，不做任何业务。</remarks>
        /// <param name="sender">事件源</param>
        /// <param name="message">通讯原始报文</param>
        private void OnCommunicationCommand(object sender, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            int code;
            if (!int.TryParse(message.Trim(), out code)) return;

            // Pose 与 Command 编码区间重叠（RobotPose.StartSafePose=1 与 RobotCommand.PreWeldRequest=1 同为 1），
            // 无法按数值区分，故按来源分槽：本字符串通道承载的是 Pose，主动指令走 ReceiveRobotCommand。
            if (code >= (int)RobotPose.HomePose && code <= (int)RobotPose.StopSafePose)
                ReceiveRobotPose((RobotPose)code);
        }

        /// <summary>机器人数据接收回调（只转发解码，不做业务）。</summary>
        /// <param name="sender">事件源（机器人通讯层）</param>
        /// <param name="e">机器人上报数据事件参数</param>
        private void OnRobotDataReceived(object sender, KUKADataReceivedEventArgs e)
        {
            if (e == null || e.RobotData == null) return;
            OnCommunicationCommand(sender, e.RobotData.EStr);
        }

        /// <summary>第 2 步：运动控制器 + CAMBOX 跟踪工作流初始化。</summary>
        private void InitializeMotionControl()
        {
            Log("[2/5] 运动控制器初始化开始");
            try
            {
                MotionManager.Instance.Initialize();

                // 若为正运动控制器，初始化激光-CAMBOX 跟踪工作流
                var zmController = MotionManager.Instance.DefaultController
                    as MotionControl.ZMotion.ZMotionController;
                if (zmController != null)
                {
                    var config = MotionConfigManager.Instance.Config;
                    _motion.Initialize(config.CamBoxTracking, config.EncoderAxis, zmController.GetHandle());
                    Log("激光-CAMBOX 跟踪工作流初始化完成");
                }
                else
                {
                    Log("未检测到正运动控制器，跟踪工作流跳过初始化", MessageLevel.Warning);
                }
                Log("[2/5] 运动控制器初始化完成");
            }
            catch (Exception ex)
            {
                Log("[2/5] 运动控制器初始化失败 原因是 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>第 5 步：机器人通讯初始化（RSI/EKI 二选一）。</summary>
        private void InitializeRobotComm()
        {
            Log("[5/5] 机器人通讯初始化开始");
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null || !comm.IsRobotEnabled)
            {
                Log("机器人通讯未启用，跳过");
                Log("[5/5] 机器人通讯跳过");
                return;
            }

            try
            {
                // 先从 XML 加载通讯配置（IP/Port）
                comm.InitializeRobotComm();

                RobotManager = comm.RobotManager;
                if (RobotManager == null)
                {
                    Log("RobotManager 未就绪", MessageLevel.Warning);
                    Log("[5/5] 机器人通讯初始化失败", MessageLevel.Warning);
                    return;
                }

                RobotManager.DataReceived += OnRobotDataReceived;
                // 根据配置模式启动 RSI 或 EKI（二选一）
                bool connected;
                if (comm.RobotCommMode == CommunicationManager.RobotCommModeType.RSI)
                {
                    connected = RobotManager.StartRSI();
                    Log(string.Format("机器人通讯模式 RSI 连接结果 {0}", connected));
                }
                else
                {
                    connected = RobotManager.OpenEKI();
                    Log(string.Format("机器人通讯模式 EKI 连接结果 {0}", connected));
                }

                if (connected)
                {
                    Log("[5/5] 机器人通讯初始化完成");
                }
                else
                {
                    Log("[5/5] 机器人通讯连接失败，待机重试", MessageLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log("[5/5] 机器人通讯初始化异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>统计未就绪的子设备数。</summary>
        /// <remarks>Connected 视为就绪，Disconnected 计为未就绪。</remarks>
        /// <param name="child">子设备状态对象</param>
        /// <param name="name">子设备中文名（用于日志）</param>
        /// <returns>未就绪返回 1，正常返回 0</returns>
        private int CountNotConnected(DeviceStateBase child, string name)
        {
            if (child.State == SubDeviceState.Connected) return 0;
            Log(string.Format("子设备异常 {0} 当前连接态 {1}", name, child.State), MessageLevel.Warning);
            return 1;
        }

        /// <summary>统一日志出口（固定使用本类标签）。</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            DeviceLog.Write(_tag, message, level);
        }

        /// <summary>模拟挂起的线激光连接回调。</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">连接态变更参数</param>
        private void OnSimLineLaserStateChanged(object sender, SubDeviceStateChangedEventArgs e)
        {
            if (e.NewState != SubDeviceState.Connected) return;
            _lineLaser.StateSwitched -= OnSimLineLaserStateChanged;
            _simLineLaserPending = false;
            // 用户已在初始化期间关闭模拟：放弃启动，避免误进入流程
            if (_currentMode == SimTestMode.None) return;
            try { _lineLaser.Start(); }
            catch (Exception ex) { Log("模拟-线激光启动失败 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>等待子设备连接完成（轮询 State，有界等待）。</summary>
        /// <param name="child">子设备</param>
        /// <param name="name">子设备中文名（日志用）</param>
        private void WaitChildConnected(DeviceStateBase child, string name)
        {
            var deadline = DateTime.Now.AddMilliseconds(CascadeConnectTimeoutMs);
            while (child.State != SubDeviceState.Connected && DateTime.Now < deadline)
                Thread.Sleep(100);
            if (child.State != SubDeviceState.Connected)
                Log(string.Format("{0} 连接等待超时", name), MessageLevel.Warning);
        }

        /// <summary>聚合判定初始化结果并切换运行态。</summary>
        /// <remarks>判定口径：子设备 Connected 视为就绪，Disconnected 计为未就绪。</remarks>
        /// <returns>全部就绪返回 true</returns>
        private bool EvaluateInitializationResult()
        {
            int failCount = 0;
            failCount += CountNotConnected(_weldHead, "焊接头");
            failCount += CountNotConnected(_lineLaser, "线激光");
            failCount += CountNotConnected(_monitorCam, "监控相机");
            failCount += CountNotConnected(_motion, "运控");

            if (failCount == 0)
            {
                SetStatus(MainDeviceStatus.Stop, "子设备初始化全部完成（整机就绪）");
                Log("子设备初始化全部完成（整机就绪）");
                return true;
            }

            SetStatus(MainDeviceStatus.Alarm, "子设备初始化失败");
            Log(string.Format("子设备初始化失败 存在 {0} 个子设备未连接", failCount), MessageLevel.Error);
            return false;
        }

        /// <summary>判断机器人通讯在位（RSI 或 EKI 任一连接）。</summary>
        /// <returns>在位返回 true；管理器未就绪视为不在位</returns>
        private bool IsRobotReachable()
        {
            return RobotManager != null && (RobotManager.IsRSIConnected || RobotManager.IsEKIConnected);
        }

        /// <summary>级联子流程复位。</summary>
        /// <remarks>单个流程失败记日志不中断后续，与级联连接容错口径一致。</remarks>
        /// <returns>全部受理返回 true</returns>
        private bool CascadeResetProcess()
        {
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, WeldParamControlWorkflow.Instance };
            var names = new[] { "焊接头", "线激光", "监控相机", "运控", "焊接工艺" };
            bool allAccepted = true;
            for (int i = 0; i < flows.Length; i++)
            {
                try
                {
                    if (!flows[i].ResetProcess())
                    {
                        allAccepted = false;
                        Log(string.Format("{0} 流程复位未受理", names[i]), MessageLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    allAccepted = false;
                    Log(string.Format("{0} 流程复位异常 {1}", names[i], ex.Message), MessageLevel.Error);
                }
            }
            return allAccepted;
        }

        /// <summary>级联 5 个子流程清状态：清报警回就绪。</summary>
        private void CascadeClearStatus()
        {
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, WeldParamControlWorkflow.Instance };
            var names = new[] { "焊接头", "线激光", "监控相机", "运控", "焊接工艺" };
            for (int i = 0; i < flows.Length; i++)
            {
                try { flows[i].ClearStatus(); }
                catch (Exception ex) { Log(string.Format("{0} 清状态异常 {1}", names[i], ex.Message), MessageLevel.Warning); }
            }
        }

        #endregion

        #region 命令变量消费与单步分派（运行线程内执行）

        /// <summary>按优先级消费命令变量。</summary>
        /// <remarks>Pose 与 Command 必须分槽存放：二者 int 编码区间重叠（StartSafePose 与 PreWeldRequest 同为 1），单槽必丢指令。</remarks>
        private void ConsumeCommand()
        {
            int cmd = _pendingCommand;
            if (cmd != NoPendingCommand)
            {
                _pendingCommand = NoPendingCommand;
                if (cmd == (int)RobotCommand.Abort)
                    _pendingAbort = true;
                else if (cmd == (int)RobotCommand.PreWeldRequest)
                    _pendingPreWeldRequest = true;
            }

            int poseCode = _pendingPose;
            if (poseCode == NoPendingPose) return;
            _pendingPose = NoPendingPose;

            RobotPose pose = (RobotPose)poseCode;
            if (!InteractionTable.IsValidPose(pose))
            {
                Log("收到非法 RobotPose " + pose, MessageLevel.Warning);
                return;
            }
            if (!IsRobotPoseSafe(pose))
            {
                Log("RobotPose 安全校验未通过 " + pose, MessageLevel.Error);
                return;
            }

            _lastPose = pose;
            OnPoseArrived(pose);
        }

        /// <summary>按当前执行步分派单步动作。</summary>
        /// <remarks>由运行线程按节拍反复调用，每个 case 必须可重入且不得阻塞。</remarks>
        private void FlowProcess()
        {
            if (_pendingAbort)
            {
                _pendingAbort = false;
                TriggerAbort("机器人主动急停指令");
                return;
            }

            switch (WorkStep)
            {
                case StepIdle: DoIdle(); break;
                case StepPreworkConnect: DoPreworkConnect(); break;
                case StepWaitPreWeldPose: DoWaitPreWeldPose(); break;
                case StepYAxisTracking: DoYAxisTracking(); break;
                case StepWaitWeldStart: DoWaitWeldStart(); break;
                case StepStartWelding: DoStartWelding(); break;
                case StepWelding: DoWelding(); break;
                case StepStopping: DoStopping(); break;
                case StepFinishOk: DoFinishOk(); break;
                case StepFinishFail: DoFinishFail(); break;
                default: GoStep(StepIdle); break;
            }
        }

        /// <summary>执行步号转中文名。</summary>
        /// <remarks>新增步必须登记，否则该步不可观测。</remarks>
        /// <param name="step">执行步号</param>
        /// <returns>步号对应中文名；未登记的步返回「步骤 N」</returns>
        private string GetStepName(int step)
        {
            switch (step)
            {
                case StepIdle: return "待机-等待焊前请求";
                case StepPreworkConnect: return "焊前准备-子设备连接";
                case StepWaitPreWeldPose: return "焊前准备-等待机器人焊前位";
                case StepYAxisTracking: return "焊前位-Y轴偏移跟踪";
                case StepWaitWeldStart: return "焊前位-等待焊接起始位";
                case StepStartWelding: return "焊接-启动子设备";
                case StepWelding: return "焊接-进行中";
                case StepStopping: return "停止-级联断开与回安全位";
                case StepFinishOk: return "成功收尾";
                case StepFinishFail: return "失败收尾";
                default: return "步骤 " + step;
            }
        }

        #endregion

        #region 模拟模式（归并自 SimulationWorkflow，ADR-048：模拟是 MODE 不是线程）

        /// <summary>开启或关闭模拟模式。</summary>
        /// <remarks>开启时设置模式、选相机、套用虚拟相机轮廓/随机、启动共享模拟 robotX 时钟；传入 None 视为关闭。</remarks>
        /// <param name="mode">目标模拟模式（None 表示关闭）</param>
        public void EnableSimulation(SimTestMode mode)
        {
            if (mode == SimTestMode.None)
            {
                DisableSimulation();
                return;
            }

            lock (_robotXLock)
            {
                _currentMode = mode;
                _selectedMode = mode;
            }

            // 选相机（A/B 虚拟，C 真实英莱）
            if (ShouldUseVirtualCamera)
                CameraSelector.SelectVirtual();
            else
                CameraSelector.SelectIntelligentLaser();

            // 套用虚拟相机轮廓/随机设置（仅当选中虚拟相机时生效）
            var virt = CameraSelector.Active as VirtualCameraRun;
            if (virt != null)
            {
                virt.SeamProfile = SeamProfile;
                virt.RandomMode = RandomMode;
                virt.TargetDistance = SimTargetDistance;
            }

            // 启动共享模拟 robotX 时钟（线激光/运控统一读取）
            StartRobotXSimulation();

            // 持久化"上次选择的模式"，使重启后下拉框显示本次选择
            SaveSimulation();

            DeviceLog.Write(_simTag, string.Format(
                "模拟模式开启 模式 {0} 起点X {1:F1} 速度 {2:F1} 目标 {3:F1}",
                mode, SimStartX, SimSpeed, SimTargetDistance), MessageLevel.Info);
        }

        /// <summary>关闭模拟模式：复位模式、停止共享模拟 robotX 时钟</summary>
        public void DisableSimulation()
        {
            bool wasEnabled;
            lock (_robotXLock)
            {
                wasEnabled = _currentMode != SimTestMode.None;
                _currentMode = SimTestMode.None;
            }
            if (!wasEnabled) return; // 幂等：已关闭则不重复停止/记录，消除「模拟模式关闭」重复日志
            StopRobotXSimulation();
            DeviceLog.Write(_simTag, "模拟模式关闭", MessageLevel.Info);
        }

        /// <summary>启动共享模拟 robotX 时钟：robotX = 起点 + 速度 × 流逝时间</summary>
        public void StartRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _simStartX = _simStartXParam;
                _simSpeed = _simSpeedParam;
                _simStartTime = DateTime.Now;
                _robotXSimEnabled = true;
            }
        }

        /// <summary>停止共享模拟 robotX 时钟</summary>
        public void StopRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _robotXSimEnabled = false;
            }
        }

        #region 模拟流程编排（线激光是主设备级联子设备，模拟生命周期由本类统一编排；UI 页只经此方法触发并消费结果）

        /// <summary>启动模拟用线激光流程。</summary>
        /// <remarks>线激光是主设备级联子设备，模拟流程由本类统一编排；UI 页仅调用本方法触发，结果经 StateSwitched 事件消费，不得越权直驱线激光。</remarks>
        public void StartSimulationLineLaser()
        {
            if (_lineLaser.State != SubDeviceState.Connected)
            {
                _simLineLaserPending = true;
                _lineLaser.StateSwitched += OnSimLineLaserStateChanged;
                _lineLaser.ConnectOn();
            }
            else if (_lineLaser.WeldStatus == SubDeviceWeldStatus.Standby)
            {
                _lineLaser.Start();
            }
        }

        /// <summary>停止模拟用线激光流程（级联子设备停止）。</summary>
        public void StopSimulationLineLaser()
        {
            try { _lineLaser.Stop(); }
            catch (Exception ex) { Log("模拟-线激光停止失败 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>复位模拟用线激光流程到可启动态（Standby）。</summary>
        public void ResetSimulationLineLaser()
        {
            try { _lineLaser.ResetProcess(); }
            catch (Exception ex) { Log("模拟-线激光复位失败 " + ex.Message, MessageLevel.Error); }
        }

        #endregion

        /// <summary>获取当前有效机器人 X 坐标。</summary>
        /// <remarks>模拟开启时返回 起点 + 速度 × 流逝时间；未开启时返回 0（生产流程的 robotX 由调用方自行读取真实坐标）。线激光流程与运控流程统一通过此方法读取模拟坐标，保证一致。</remarks>
        /// <returns>当前有效模拟 X 坐标</returns>
        public double GetEffectiveRobotX()
        {
            lock (_robotXLock)
            {
                if (_robotXSimEnabled)
                {
                    double elapsed = (DateTime.Now - _simStartTime).TotalSeconds;
                    return _simStartX + _simSpeed * elapsed;
                }
            }
            return 0.0;
        }

        /// <summary>从配置文件加载模拟参数。</summary>
        /// <remarks>含起点/速度/目标距离/模式与虚拟相机轮廓/随机；须在应用启动、创建任何页面之前调用。文件不存在或读取失败时保留默认/当前值；新文件缺失时回退读取旧文件并迁移。</remarks>
        public void LoadSimulation()
        {
            try
            {
                // 优先新文件；新文件缺失且旧文件存在时，从旧文件读取（键名一致）
                string sourceFile = _simConfigFile;
                bool migrated = false;
                if (!File.Exists(_simConfigFile) && File.Exists(_simLegacyConfigFile))
                {
                    sourceFile = _simLegacyConfigFile;
                    migrated = true;
                }
                if (!File.Exists(sourceFile))
                    return;

                var ini = new INIFile(sourceFile);
                SimStartX = ini.ReadDouble("Simulation", "SimStartX", SimStartX);
                SimSpeed = ini.ReadDouble("Simulation", "SimSpeed", SimSpeed);
                SimTargetDistance = ini.ReadDouble("Simulation", "SimTargetDistance", SimTargetDistance);
                int modeIdx = ini.ReadInt("Simulation", "CurrentSimMode", (int)SelectedMode);
                if (Enum.IsDefined(typeof(SimTestMode), modeIdx))
                    _selectedMode = (SimTestMode)modeIdx;

                int profileIdx = ini.ReadInt("VirtualCamera", "SeamProfile", (int)SeamProfile);
                if (Enum.IsDefined(typeof(SimSeamProfile), profileIdx))
                    SeamProfile = (SimSeamProfile)profileIdx;
                RandomMode = ini.ReadInt("VirtualCamera", "RandomMode", RandomMode ? 1 : 0) == 1;

                if (CameraSelector.IsVirtual)
                {
                    var virt = CameraSelector.Active as VirtualCameraRun;
                    if (virt != null)
                    {
                        virt.SeamProfile = SeamProfile;
                        virt.RandomMode = RandomMode;
                    }
                }

                // 迁回旧文件时：仅恢复字段、不自动启动流程，避免"模式已选但未运行"的半启动态；
                // 模式置为 None 由用户显式启动，数值参数保持迁移值并已落盘到新文件。
                if (migrated)
                {
                    _currentMode = SimTestMode.None;
                    SaveSimulation();
                }
            }
            catch
            {
                // 加载异常时保留当前值
            }
        }

        /// <summary>保存模拟参数到配置文件。</summary>
        /// <remarks>用户通过界面「保存模拟参数」时调用。</remarks>
        public void SaveSimulation()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_simConfigFile));
                var ini = new INIFile(_simConfigFile);
                ini.WriteDouble("Simulation", "SimStartX", SimStartX);
                ini.WriteDouble("Simulation", "SimSpeed", SimSpeed);
                ini.WriteDouble("Simulation", "SimTargetDistance", SimTargetDistance);
                ini.WriteInt("Simulation", "CurrentSimMode", (int)SelectedMode);
                ini.WriteInt("VirtualCamera", "SeamProfile", (int)SeamProfile);
                ini.WriteInt("VirtualCamera", "RandomMode", RandomMode ? 1 : 0);
                ini.SaveToFile();
            }
            catch
            {
                // 保存失败不影响运行时
            }
        }

        #endregion

        #region 公共函数（设备操作契约）

        /// <summary>接收机器人 Pose（只写命令变量，业务由运行线程执行）。</summary>
        /// <param name="pose">机器人上报的姿态</param>
        public void ReceiveRobotPose(RobotPose pose)
        {
            _pendingPose = (int)pose;
        }

        /// <summary>接收机器人主动指令（只写命令变量）。</summary>
        /// <param name="cmd">机器人主动指令（如 PreWeldRequest / Abort）</param>
        public void ReceiveRobotCommand(RobotCommand cmd)
        {
            _pendingCommand = (int)cmd;
        }

        /// <summary>上电初始化：执行 5 步子设备初始化级联（阻塞同步）。</summary>
        /// <remarks>
        /// 顺序：焊接头 → 运动控制器（含 CAMBOX 跟踪）→ 线激光相机 → 监控相机 → 机器人通讯。
        /// 全部就绪置 Stop（可运行），否则置 Alarm。机器人与 PLC 的连接由顶层 Comm 管控，此处只做编排。
        /// 线激光/监控相机经 ConnectOn 受理后轮询等待 Connected（受理式非阻塞约定的级联等待）。
        /// </remarks>
        /// <returns>初始化后整机就绪返回 true</returns>
        public bool InitializeMachine()
        {
            if (_initializing) return false;
            _initializing = true;
            try
            {
                Log("子设备初始化开始");
                Log("初始化顺序 焊接头 运动控制器 线激光相机 监控相机 机器人通讯");

                // 1. 焊接头初始化（激光器握手 + 温度模块枚举 + IO 映射）
                Log("[1/5] 焊接头初始化开始");
                try
                {
                    LaserWeldHeadController.Instance.Initialize();
                    Log("[1/5] 焊接头初始化完成");
                }
                catch (Exception ex)
                {
                    Log("[1/5] 焊接头初始化失败 原因是 " + ex.Message, MessageLevel.Warning);
                }

                // 2. 运动控制器初始化 + CAMBOX 跟踪工作流初始化
                InitializeMotionControl();

                // 3. 线激光相机连接（受理式 + 等待 Connected）
                Log("[3/5] 线激光相机初始化开始");
                try
                {
                    if (_lineLaser.State != SubDeviceState.Connected)
                    {
                        _lineLaser.ConnectOn();
                        WaitChildConnected(_lineLaser, "线激光相机");
                    }
                    else
                    {
                        Log("[3/5] 线激光相机已连接，跳过");
                    }
                }
                catch (Exception ex)
                {
                    Log("[3/5] 线激光相机初始化异常 " + ex.Message, MessageLevel.Warning);
                }

                // 4. 监控相机连接（受理式 + 等待 Connected）
                Log("[4/5] 监控相机初始化开始");
                try
                {
                    if (_monitorCam.State != SubDeviceState.Connected)
                    {
                        _monitorCam.ConnectOn();
                        WaitChildConnected(_monitorCam, "监控相机");
                    }
                    else
                    {
                        Log("[4/5] 监控相机已连接，跳过");
                    }
                }
                catch (Exception ex)
                {
                    Log("[4/5] 监控相机初始化失败 原因是 " + ex.Message, MessageLevel.Warning);
                }

                // 5. 机器人通讯初始化（仅启用时，RSI/EKI 二选一）
                InitializeRobotComm();

                // 聚合判定：全部子设备 Connected 才置可运行；否则告警
                return EvaluateInitializationResult();
            }
            finally
            {
                _initializing = false;
            }
        }

        /// <summary>启动机器：拉起运行线程并置就绪（幂等）。</summary>
        /// <returns>受理返回 true</returns>
        public bool StartMachine()
        {
            if (_running) return true;
            _running = true;
            _runLoopClosing = false;
            // 重启一律从待机步起算：沿用停机时刻的步号与时间戳会让首拍就判超时
            GoStep(StepIdle);
            if (_runLoopThread == null || !_runLoopThread.IsAlive)
            {
                _runLoopThread = new Thread(RunLoop)
                {
                    Name = "DeviceControlWorkRunLoop",
                    IsBackground = true
                };
                _runLoopThread.Start();
            }
            if (Status != MainDeviceStatus.EStop && Status != MainDeviceStatus.Alarm)
                SetStatus(MainDeviceStatus.Stop, "机器启动");
            DeviceStatusWork.Instance.StartWork();
            Log("主设备运行管控启动，运行线程已就绪");
            return true;
        }

        /// <summary>停机：停运行线程、清子流程状态并级联断开。</summary>
        public void StopMachine()
        {
            _running = false;
            _runLoopClosing = true;
            if (_runLoopThread != null && _runLoopThread.IsAlive)
                _runLoopThread.Join(1000);
            _runLoopThread = null;
            CascadeClearStatus();
            CascadeDisconnect();
            DeviceStatusWork.Instance.CloseWork();
            if (Status == MainDeviceStatus.Running)
                SetStatus(MainDeviceStatus.Stop, "机器停止");
            Log("主设备运行管控停止");
        }

        /// <summary>复位机器到就绪态。</summary>
        /// <returns>受理返回 true；运行中或机器人在位检测不通过返回 false</returns>
        public bool ResetMachine()
        {
            if (_running && Status == MainDeviceStatus.Running) return false;
            if (!IsRobotReachable())
            {
                SetStatus(MainDeviceStatus.Alarm, "机器人在位检测失败 复位阻断");
                FaultRecoveryManager.Instance.RecordFault("MainDevice", new FaultRecord
                {
                    Device = _tag,
                    State = SubDeviceWeldStatus.ErrorAborted,
                    Category = FaultCategory.Communication,
                    ErrorCode = "ROBOT_OFFLINE",
                    ParamSnapshot = RobotManager == null
                        ? "RobotManager 未就绪"
                        : string.Format("RSI={0} EKI={1}", RobotManager.IsRSIConnected, RobotManager.IsEKIConnected)
                });
                Log("机器人在位检测失败 复位阻断", MessageLevel.Error);
                return false;
            }
            _pendingPose = NoPendingPose;
            _pendingCommand = NoPendingCommand;
            _pendingAbort = false;
            _pendingPreWeldRequest = false;
            _lastPose = RobotPose.Unknown;
            GoStep(StepIdle);
            SetStatus(MainDeviceStatus.Reseting, "流程复位");
            CascadeResetProcess();
            SetStatus(MainDeviceStatus.Stop, "流程复位完成");
            return true;
        }

        /// <summary>清除报警：仅清报警标志（与复位正交）。</summary>
        /// <returns>存在报警并已清除返回 true</returns>
        public bool ClearAlarm()
        {
            lock (_statusLock)
            {
                if (_status != MainDeviceStatus.Alarm) return false;
            }
            SetStatus(MainDeviceStatus.NoReset, "报警已清除 待复位");
            return true;
        }

        /// <summary>急停：置急停态 + 设备级急停 + 级联断开 + 下发中止。</summary>
        /// <param name="reason">急停原因</param>
        public void EStopMachine(string reason)
        {
            SetStatus(MainDeviceStatus.EStop, reason);
            EmergencyStop();
            CascadeDisconnect();
            SendDeviceCommand(DeviceCommand.CmdAbort);
            _lastPose = RobotPose.Unknown;
            GoStep(StepIdle);
        }

        /// <summary>取消急停并转未复位。</summary>
        public void EStopCancel()
        {
            if (Status != MainDeviceStatus.EStop) return;
            SetStatus(MainDeviceStatus.NoReset, "急停解除 待复位");
        }

        /// <summary>设备安全：急停（下发机器人 DCmdAbort）。</summary>
        public void EmergencyStop()
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm != null && comm.IsRobotEnabled && comm.RobotManager != null)
                    comm.RobotManager.SendRobotData(new KUKARobotData { EStr = SignalCode.DCmdAbort.ToString() });
            }
            catch (Exception ex)
            {
                Log("急停下发异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>安全互锁报警上报（由 DeviceStatusWork 调用）：置报警态。</summary>
        /// <param name="reason">报警原因</param>
        public void ReportAlarm(string reason)
        {
            SetStatus(MainDeviceStatus.Alarm, reason);
        }

        /// <summary>安全互锁恢复上报（由 DeviceStatusWork 调用）：转未复位。</summary>
        public void ReportAlarmCleared()
        {
            if (Status == MainDeviceStatus.Alarm)
                SetStatus(MainDeviceStatus.NoReset, "安全互锁恢复 待复位");
        }

        /// <summary>释放：停机并退订通讯事件。</summary>
        public void Dispose()
        {
            StopMachine();
            GlobalCommData.CommunicationCommandReceived -= OnCommunicationCommand;
            if (RobotManager != null) RobotManager.DataReceived -= OnRobotDataReceived;
        }

        #endregion
    }

    /// <summary>运控调整模拟测试模式（归并自 SimulationWorkflow，ADR-048）</summary>
    public enum SimTestMode
    {
        /// <summary>正常真实流程</summary>
        None = 0,
        /// <summary>虚拟相机 + 计算 + 记录（不向运动控制输出指令）</summary>
        ModeA = 1,
        /// <summary>虚拟相机 + 计算 + 记录 + 运动执行</summary>
        ModeB = 2,
        /// <summary>真实相机 + 计算 + 记录（不向运动控制输出指令）</summary>
        ModeC = 3
    }
}
