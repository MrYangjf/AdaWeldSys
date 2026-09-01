using System;
using System.IO;
using System.Windows.Forms;
using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.Robot.KUKARobot;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.LaserWeldHead;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.LineLaserCamApi;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.ProductFileManager;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>
    /// 主设备工作流（单例）：整机流程编排与机器人/PLC 信号交互。
    ///
    /// 设计要点：
    ///   1. 与 PLC/机器人的"连接"由顶层 Comm 通讯管理（CommunicationManager）管控，
    ///      本类只做"信号交互"——接收 RobotPose、切换流程态、级联子设备 DeviceState、下发 DeviceCommand。
    ///   2. 整机流程态（MainDeviceFlowState，机器人 Pose 驱动）为本类独有阶段态，定义于本文件；
    ///      流程态机制（Step/SetStep/StateChanged/设备四态映射）与执行步机制（WorkStep/GoStep/监听线程）
    ///      均由泛型基类承载（ADR-042 + ADR-047）。
    ///   3. 步骤驱动：通讯回调只写命令变量（_pendingPose / _pendingCommand / _pendingAbort），
    ///      业务一律在常驻监听线程的 ConsumeCommand + FlowProcess 中执行（ADR-047 红线 4）。
    ///   4. 级联子设备统一走共用接口：Initialize=Connect / AutoRun=Work / ManualStop=Disconnect。
    ///   5. 交互指令见 DeviceState.CommandTable（RobotPose / RobotCommand / DeviceCommand / InteractionTable）。
    ///   6. 模拟模式控制器由原独立 SimulationWorkflow 归并至本单例（ADR-048，部分取代 ADR-017）：
    ///      模拟仍是 MODE 不是线程，对外统一经 MainDeviceWorkflow.Instance 访问。
    ///
    /// 执行步段位：0 待机 · 10~19 焊前准备 · 20~29 焊前位跟踪 · 30~39 焊接中 · 40~49 停止 · 800 成功收尾 · 900 失败收尾。
    /// </summary>
    public class MainDeviceWorkflow : DeviceWorkflowBase<MainDeviceFlowState>
    {
        #region 常量

        /// <summary>执行步 10：焊前准备-等待子设备 Connect 就绪。</summary>
        private const int StepPreworkConnect = 10;

        /// <summary>执行步 11：焊前准备-等待机器人上报焊前位。</summary>
        private const int StepWaitPreWeldPose = 11;

        /// <summary>执行步 20：Y 轴偏移跟踪。</summary>
        private const int StepYAxisTracking = 20;

        /// <summary>执行步 21：等待机器人上报焊接起始位。</summary>
        private const int StepWaitWeldStart = 21;

        /// <summary>执行步 30：进入焊接态并级联子设备 Work。</summary>
        private const int StepStartWelding = 30;

        /// <summary>执行步 31：焊接中-等待预停位或焊接停止位。</summary>
        private const int StepWelding = 31;

        /// <summary>执行步 40：停止-级联断开并下发回安全位。</summary>
        private const int StepStopping = 40;

        /// <summary>子设备 Connect 级联超时（毫秒）。</summary>
        private const double CascadeConnectTimeoutMs = 10000;

        /// <summary>等待机器人到位 Pose 超时（毫秒）。</summary>
        private const double PoseWaitTimeoutMs = 60000;

        /// <summary>Y 轴偏移跟踪超时（毫秒）。</summary>
        private const double YAxisTrackingTimeoutMs = 5000;

        /// <summary>命令变量空值：无待处理机器人 Pose。</summary>
        private const int NoPendingPose = -1;

        /// <summary>命令变量空值：无待处理机器人主动指令（RobotCommand.None）。</summary>
        private const int NoPendingCommand = 0;

        #endregion

        #region 私有变量

        private static readonly Lazy<MainDeviceWorkflow> _lazyInstance =
            new Lazy<MainDeviceWorkflow>(() => new MainDeviceWorkflow());

        private readonly string _tag = "主设备工作流";

        /// <summary>命令变量：待处理的机器人 Pose（NoPendingPose = 无）。通讯回调只写，监听线程消费。</summary>
        private volatile int _pendingPose = NoPendingPose;

        /// <summary>命令变量：待处理的机器人主动指令（NoPendingCommand = 无）。</summary>
        private volatile int _pendingCommand = NoPendingCommand;

        /// <summary>命令变量：待处理的急停请求（Abort 优先级最高，独立存放避免被 Pose 覆盖）。</summary>
        private volatile bool _pendingAbort;

        /// <summary>最近一次到达的机器人 Pose（步间传值走流程字段，局部变量跨拍必然丢失）。</summary>
        private volatile RobotPose _lastPose = RobotPose.Unknown;

        /// <summary>焊前请求标记（机器人 PreWeldRequest 与 StartSafePose 配合触发 Preworking）。</summary>
        private volatile bool _pendingPreWeldRequest;

        private volatile bool _running;
        private volatile bool _initializing;

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

        #endregion

        #region 公共变量

        /// <summary>单例实例。</summary>
        public static MainDeviceWorkflow Instance => _lazyInstance.Value;

        public override string StateName => _tag;

        /// <summary>整机流程态（即泛型基类 Step，本类独有语义：机器人 Pose 驱动）。</summary>
        public MainDeviceFlowState FlowState => Step;

        /// <summary>是否焊接中（流程态处于 Working）。</summary>
        public bool IsWelding => FlowState == MainDeviceFlowState.Working;

        /// <summary>监听线程只在 Start 之后跑业务，Stop 后只刷新时间戳（防误判超时）。</summary>
        protected override bool IsRunLoopActive { get { return _running; } }

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

        private MainDeviceWorkflow() : base(MainDeviceFlowState.Idle)
        {
            // 订阅顶层 Comm 的通讯指令（机器人 Pose/Command 经此到达；回调只写命令变量，不做业务）
            GlobalCommData.CommunicationCommandReceived += OnCommunicationCommand;
            GlobalCommData.ShowLog(_tag, "主设备工作流实例化", MessageLevel.Info);
        }

        #endregion

        #region 私有函数

        /// <summary>执行步 0：待机。</summary>
        /// <remarks>安全起始位；收到焊前请求才发起焊前准备。</remarks>
        private void DoIdle()
        {
            if (_step != MainDeviceFlowState.SafePose || !_pendingPreWeldRequest) return;
            _pendingPreWeldRequest = false;
            SetStep(MainDeviceFlowState.Preworking, "机器人请求焊前准备");
            CascadeConnect();
            GoStep(StepPreworkConnect);
        }

        /// <summary>执行步 10：等子设备 Connect 就绪后下发去焊前位指令。</summary>
        /// <remarks>失败重试零代码：条件不满足即返回、步号不变，下一拍重入重试。</remarks>
        private void DoPreworkConnect()
        {
            if (!IsChildConnected(_lineLaser) || !IsChildConnected(_motion))
            {
                if (IsStepTimeout(CascadeConnectTimeoutMs))
                    FailFlow("子设备 Connect 级联超时");
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
                SetStep(MainDeviceFlowState.PreWeldPose, "机器人到达焊前位");
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

        /// <summary>步 30：进入焊接态，级联子设备 Work（线激光开激光采集）。</summary>
        private void DoStartWelding()
        {
            SetStep(MainDeviceFlowState.Working, "机器人到达焊接起始位");
            try { _lineLaser.AutoRun(); }
            catch (Exception ex)
            {
                Log("线激光进入 Working 异常 " + ex.Message, MessageLevel.Error);
            }
            GoStep(StepWelding);
        }

        /// <summary>执行步 31：焊接中。</summary>
        /// <remarks>收到预停位转停止段；收到焊接停止位直接成功收尾。</remarks>
        private void DoWelding()
        {
            if (_lastPose == RobotPose.PreStopPose)
            {
                SetStep(MainDeviceFlowState.Stopping, "机器人到达预停位");
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

        /// <summary>步 800 成功收尾：置已停止并回待机。</summary>
        private void DoFinishOk()
        {
            SetStep(MainDeviceFlowState.Stopped, "焊接流程正常收尾");
            _lastPose = RobotPose.Unknown;
            GoStep(StepIdle);
        }

        /// <summary>执行步 900：失败收尾。</summary>
        /// <remarks>级联停机；为超时三步走的第三步。</remarks>
        private void DoFinishFail()
        {
            EmergencyStop();
            SetStep(MainDeviceFlowState.Alarm, FailReason);
            FaultRecoveryManager.Instance.RecordFault("MainDevice", new FaultRecord
            {
                Device = StateName,
                State = State,
                Category = FaultCategory.System,
                ErrorCode = "FLOW_TIMEOUT",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", FailStep, FailReason)
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
                    SetStep(MainDeviceFlowState.Idle, "机器人归零位");
                    CascadeDisconnect();
                    SendDeviceCommand(DeviceCommand.CmdGoHome);
                    GoStep(StepIdle);
                    break;
                case RobotPose.StopSafePose:
                    SetStep(MainDeviceFlowState.Stopped, "机器人回到安全停止位");
                    CascadeDisconnect();
                    SendDeviceCommand(DeviceCommand.CmdGoStopSafe);
                    GoStep(StepIdle);
                    break;
                case RobotPose.StartSafePose:
                    if (_step != MainDeviceFlowState.SafePose)
                        SetStep(MainDeviceFlowState.SafePose, "机器人进入安全起始位");
                    break;
            }
        }

        /// <summary>校验机器人姿态是否安全</summary>
        /// <param name="pose">机器人上报的姿态</param>
        /// <returns>允许接受返回 true，安全监控已启动且互锁未通过返回 false</returns>
        private bool IsRobotPoseSafe(RobotPose pose)
        {
            return SafetyWorkflow.Instance.IsSafe;
        }

        private void TriggerAbort(string reason)
        {
            Log("触发急停 " + reason, MessageLevel.Error);
            EmergencyStop();
            SetStep(MainDeviceFlowState.Alarm, reason);
            CascadeDisconnect();
            SendDeviceCommand(DeviceCommand.CmdAbort);
            GoStep(StepIdle);
        }

        /// <summary>级联子设备 Connect（共用接口 Initialize=Connect）。</summary>
        private void CascadeConnect()
        {
            try { _lineLaser.Initialize(); }
            catch (Exception ex) { Log("线激光 Connect 异常 " + ex.Message, MessageLevel.Error); }
            try { _motion.Initialize(); }
            catch (Exception ex) { Log("运控 Connect 异常 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>级联子设备 Disconnect（共用接口 ManualStop=Disconnect）。</summary>
        private void CascadeDisconnect()
        {
            try { _lineLaser.ManualStop(); }
            catch (Exception ex) { Log("线激光 Disconnect 异常 " + ex.Message, MessageLevel.Error); }
            try { _motion.ManualStop(); }
            catch (Exception ex) { Log("运控 Disconnect 异常 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>子设备是否已连接。</summary>
        /// <remarks>Connect 与 Work 均属已连接；Disconnect/Alarm 计为未就绪。</remarks>
        /// <param name="child">子设备状态对象</param>
        /// <returns>子设备处于 Connect 或 Work 返回 true</returns>
        private bool IsChildConnected(DeviceStateBase child)
        {
            DeviceState st = child.State;
            return st == DeviceState.Connect || st == DeviceState.Work;
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

        /// <summary>聚合判定初始化结果并切换整机流程态。</summary>
        /// <remarks>判定口径：子设备 Connect 或 Work 视为正常，Disconnect 或 Alarm 计为未就绪。</remarks>
        private void EvaluateInitializationResult()
        {
            int failCount = 0;
            failCount += CountNotConnected(_weldHead, "焊接头");
            failCount += CountNotConnected(_lineLaser, "线激光");
            failCount += CountNotConnected(_monitorCam, "监控相机");
            failCount += CountNotConnected(_motion, "运控");

            if (failCount == 0)
            {
                SetStep(MainDeviceFlowState.SafePose);
                _initializing = false;
                Log("子设备初始化全部完成（整机进入安全起始位）");
            }
            else
            {
                SetStep(MainDeviceFlowState.Alarm);
                _initializing = false;
                Log(string.Format("子设备初始化失败 存在 {0} 个子设备未连接", failCount), MessageLevel.Error);
            }
        }

        /// <summary>统计未就绪的子设备数。</summary>
        /// <remarks>Disconnect 与 Alarm 计为未就绪，Connect 与 Work 视为正常。</remarks>
        /// <param name="child">子设备状态对象</param>
        /// <param name="name">子设备中文名（用于日志）</param>
        /// <returns>未就绪返回 1，正常返回 0</returns>
        private int CountNotConnected(DeviceStateBase child, string name)
        {
            DeviceState st = child.State;
            if (st == DeviceState.Connect || st == DeviceState.Work) return 0;
            Log(string.Format("子设备异常 {0} 当前设备态 {1}", name, st), MessageLevel.Warning);
            return 1;
        }

        /// <summary>统一日志出口（固定使用本类标签）。</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(_tag, message, level);
        }

        #endregion

        #region 公共函数

        /// <summary>接收机器人 Pose（只写命令变量，业务由监听线程执行）。</summary>
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

        /// <summary>执行上电初始化编排。</summary>
        /// <remarks>
        /// 顺序：焊接头 → 运动控制器（含 CAMBOX 跟踪）→ 线激光相机 → 监控相机 → 机器人通讯。
        /// 全部就绪置 SafePose，否则置 Alarm。机器人与 PLC 的连接由顶层 Comm 管控，此处只做编排。
        /// </remarks>
        public void PowerOnInitialize()
        {
            if (_initializing) return;
            _initializing = true;
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

            // 3. 线激光相机初始化（同步执行，等待四步初始化全部完成后再继续）
            Log("[3/5] 线激光相机初始化开始");
            try
            {
                if (_lineLaser.Step == LineLaserWorkflowState.Uninitialized)
                {
                    bool ok = _lineLaser.InitializeSync();
                    if (ok)
                        Log("[3/5] 线激光相机初始化完成");
                    else
                        Log("[3/5] 线激光相机初始化失败", MessageLevel.Warning);
                }
                else
                {
                    Log("[3/5] 线激光相机已初始化，跳过");
                }
            }
            catch (Exception ex)
            {
                Log("[3/5] 线激光相机初始化异常 " + ex.Message, MessageLevel.Warning);
            }

            // 4. 监控相机初始化
            Log("[4/5] 监控相机初始化开始");
            try
            {
                _monitorCam.Initialize();
                Log("[4/5] 监控相机初始化完成");
            }
            catch (Exception ex)
            {
                Log("[4/5] 监控相机初始化失败 原因是 " + ex.Message, MessageLevel.Warning);
            }

            // 5. 机器人通讯初始化（仅启用时，RSI/EKI 二选一）
            InitializeRobotComm();

            // 聚合判定：全部子设备 Connect 才进安全起始位；否则告警
            EvaluateInitializationResult();
        }

        /// <summary>启动：置运行标记并拉起常驻监听线程（ADR-047）。</summary>
        public void Start()
        {
            _running = true;
            StartRunLoop();
            Log("主设备工作流启动，监听线程已就绪");
        }

        /// <summary>停止：停监听线程并级联子设备断开。</summary>
        public void Stop()
        {
            _running = false;
            StopRunLoop();
            CascadeDisconnect();
            Log("主设备工作流停止");
        }

        /// <summary>执行急停并下发中止指令。</summary>
        public override void EmergencyStop()
        {
            base.EmergencyStop();
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

            GlobalCommData.ShowLog(_simTag, string.Format(
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
            GlobalCommData.ShowLog(_simTag, "模拟模式关闭", MessageLevel.Info);
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

        #region 流程态映射（MainDeviceFlowState 机器人 Pose 驱动，子设备无此概念）

        /// <summary>整机流程态映射为设备四态（ADR-041）。</summary>
        /// <remarks>Preworking/PreWeldPose/Working/Stopping → Work；Alarm → Alarm；Idle/SafePose/Stopped → Connect。</remarks>
        /// <param name="step">整机流程态</param>
        /// <returns>对应的设备四态</returns>
        protected override DeviceState MapToDeviceState(MainDeviceFlowState step)
        {
            switch (step)
            {
                case MainDeviceFlowState.Preworking:
                case MainDeviceFlowState.PreWeldPose:
                case MainDeviceFlowState.Working:
                case MainDeviceFlowState.Stopping:
                    return DeviceState.Work;
                case MainDeviceFlowState.Alarm:
                    return DeviceState.Alarm;
                default:
                    // Idle / SafePose / Stopped 均为"已连接就绪"语义
                    return DeviceState.Connect;
            }
        }

        #endregion

        #region 命令变量消费与单步分派（监听线程内执行）

        /// <summary>消费命令变量：Abort 最优先，其次 Command，最后 Pose。</summary>
        /// <remarks>Pose 与 Command 必须分槽存放：二者 int 编码区间重叠（StartSafePose 与 PreWeldRequest 同为 1），单槽必丢指令。</remarks>
        protected override void ConsumeCommand()
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
        /// <remarks>由监听线程按节拍反复调用，每个 case 必须可重入且不得阻塞。</remarks>
        protected override void FlowProcess()
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
        protected override string GetStepName(int step)
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

        #region 抽象生命周期方法（上电初始化回归基类契约，自动/手动双流程 ADR-048）

        /// <summary>初始化流程 = 执行 5 步上电初始化编排。</summary>
        /// <remarks>焊接头 → 运动控制器 → 线激光相机 → 监控相机 → 机器人通讯；全部就绪（SafePose）视为成功。</remarks>
        /// <returns>初始化后整机进入安全起始位返回 true</returns>
        protected override bool InitializeFlow()
        {
            PowerOnInitialize();
            return Step == MainDeviceFlowState.SafePose;
        }

        /// <summary>手动开 = 启动监听线程（非阻塞，界面 UI 入口）。</summary>
        protected override void ManualOn()
        {
            Start();
        }

        /// <summary>手动关 = 停止监听线程并级联子设备断开。</summary>
        protected override void ManualOff()
        {
            Stop();
        }

        /// <summary>自动运行 = 阻塞式执行完整焊接周期（ADR-048）。</summary>
        /// <remarks>序列：未就绪先上电初始化 → 启动监听线程 → 阻塞等待一个完整周期（SafePose→Preworking→…→Stopped）
        /// 或异常/中止。握手推进仍由监听线程 FlowProcess 按机器人 Pose 驱动，本序列只做编排与等待。</remarks>
        protected override void AutoRunFlow()
        {
            if (Step != MainDeviceFlowState.SafePose && !InitializeFlow())
            {
                // 监听线程尚未 Start（900 收尾步不会被消费），前置失败只记日志（EvaluateInitializationResult 已置 Alarm）
                Log("自动运行前置初始化未就绪", MessageLevel.Error);
                return;
            }
            Start();
            AutoWait(() => FlowState == MainDeviceFlowState.Stopped
                || FlowState == MainDeviceFlowState.Alarm
                || FlowState == MainDeviceFlowState.Idle, 0);
            if (IsAutoAbortRequested)
            {
                EmergencyStop();
                CascadeDisconnect();
            }
        }

        /// <summary>流程复位：清命令变量与握手状态，回到待机。</summary>
        protected override void ResetFlow()
        {
            _pendingPose = NoPendingPose;
            _pendingCommand = NoPendingCommand;
            _pendingAbort = false;
            _pendingPreWeldRequest = false;
            _lastPose = RobotPose.Unknown;
            SetStep(MainDeviceFlowState.Idle, "流程复位");
        }

        #endregion

        #region 释放（基类 Dispose 先停监听线程，再调本钩子）

        /// <summary>释放钩子：停机并退订通讯与机器人数据事件。</summary>
        protected override void DisposeManaged()
        {
            Stop();
            GlobalCommData.CommunicationCommandReceived -= OnCommunicationCommand;
            if (RobotManager != null) RobotManager.DataReceived -= OnRobotDataReceived;
        }

        #endregion
    }

    /// <summary>整机流程态（机器人 Pose 握手驱动，MainDeviceWorkflow 独有，子设备无此概念）。</summary>
    /// <remarks>· Idle        空闲（HomePose） · SafePose    安全起始位（StartSafePose） · Preworking  焊前准备（StartSafePose + 机器人 PreWeldRequest 触发） · PreWeldPose 焊前位（PreWeldPose 到位，执行 Y 轴偏移跟踪，OK 后通知机器人继续） · Working     焊接中（WeldStartPose 到位） · Stopping    停止中（PreStopPose/WeldStopPose） · Stopped     已停止（StopSafePose） · Alarm       安全/急停告警态</remarks>
    public enum MainDeviceFlowState
    {
        Idle = 0,
        SafePose = 1,
        Preworking = 2,
        PreWeldPose = 3,
        Working = 4,
        Stopping = 5,
        Stopped = 6,
        Alarm = 7
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
