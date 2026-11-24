using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AdaWeldSystem.Comm.Communication;
using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.IMonitorCam;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.ProductFileControl;

namespace AdaWeldSystem.MainDeviceControl
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

    /// <summary>模拟测试停止事件参数（通知 UI 模拟已停止）。</summary>
    public class SimulationStoppedEventArgs : EventArgs
    {
        /// <summary>停止原因（人话描述，供 UI 提示与日志）。</summary>
        public string Reason { get; private set; }

        /// <summary>是否因错误/报警停止（true=转异常清理）。</summary>
        public bool HasError { get; private set; }

        /// <summary>是否为自动停止（vs 用户手动点击停止）。</summary>
        public bool IsAuto { get; private set; }

        public SimulationStoppedEventArgs(string reason, bool hasError, bool isAuto)
        {
            Reason = reason;
            HasError = hasError;
            IsAuto = isAuto;
        }
    }

    /// <summary>整机报警信息（GetAlarmInfo 的返回结构）。</summary>
    public class MachineAlarmInfo
    {
        /// <summary>是否存在有效报警。</summary>
        public bool HasAlarm { get; set; }

        /// <summary>故障代码。</summary>
        public string ErrorCode { get; set; }

        /// <summary>故障描述。</summary>
        public string Description { get; set; }

        /// <summary>发生时间。</summary>
        public DateTime OccurTime { get; set; }

        /// <summary>报警发生时的设备运行态。</summary>
        public MainDeviceStatus StatusAtOccur { get; set; }
    }

    /// <summary>整机状态统一快照（只读聚合）。</summary>
    public class MachineStatusSnapshot
    {
        /// <summary>当前设备态。</summary>
        public MainDeviceStatus DeviceStatus { get; set; }

        /// <summary>上一设备态。</summary>
        public MainDeviceStatus LastDeviceStatus { get; set; }

        /// <summary>5 个模组的流程态。</summary>
        public Dictionary<string, SubDeviceWeldStatus> SubStatuses { get; set; }

        /// <summary>运控就绪等级。</summary>
        public ReadinessStage MotionReadiness { get; set; }

        /// <summary>线程登记摘要。</summary>
        public string ThreadSnapshot { get; set; }

        /// <summary>是否已投入工作。</summary>
        public bool IsWorking { get; set; }

        /// <summary>是否复位中。</summary>
        public bool IsReseting { get; set; }

        /// <summary>是否初始化中。</summary>
        public bool IsInitializing { get; set; }

        /// <summary>是否模拟中。</summary>
        public bool IsSimulationEnabled { get; set; }

        /// <summary>是否焊接中。</summary>
        public bool IsWelding { get; set; }

        /// <summary>最近报警信息。</summary>
        public MachineAlarmInfo Alarm { get; set; }

        /// <summary>快照时间。</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>机器人通讯心跳分级 H0~H4</summary>
    public enum RobotHeartbeatLevel
    {
        /// <summary>正常收包</summary>
        H0,
        /// <summary>单拍抖动</summary>
        H1,
        /// <summary>缺值距离超限（阶段 7 接入）</summary>
        H2,
        /// <summary>断流超过 500 ms</summary>
        H3,
        /// <summary>监听未运行</summary>
        H4
    }

    /// <summary>主设备运行管控（单例，整机编排与机器人/PLC 交互）。</summary>
    public class DeviceControlWork : IDisposable
    {
        #region 常量

        /// <summary>模组工作线程运行态节拍（毫秒，防空转）。</summary>
        private const int BeatWorkMs = 10;

        /// <summary>非运行态慢节拍（毫秒，防空转刷屏）。</summary>
        private const int BeatIdleMs = 100;

        /// <summary>指令交互线程空闲节拍（毫秒，比模组线程更长）。</summary>
        private const int BeatCommandMs = 100;

        /// <summary>机器人通讯心跳检测周期（毫秒）。</summary>
        private const int HeartBeatIntervalMs = 1000;

        /// <summary>模拟测试线程节拍（毫秒，50Hz）。</summary>
        private const int SimBeatMs = 20;

        /// <summary>常驻线程退出等待上限，超时只记警告</summary>
        private const int WorkThreadJoinTimeoutMs = 3000;

        /// <summary>模组 Connected 级联等待超时（毫秒，取自 ROOT/FLOW_TIMEOUT/CASCADE_CONNECT_MS，缺省 10000）。</summary>
        private static double CascadeConnectTimeoutMs
        {
            get { return AdaWeldSystem.MotionControl.DeltaEtherCAT.FlowTimeoutSettings.Current.CascadeConnectMs; }
        }
        // 整机初始化节点键（层级由 ParentKey 声明，顺序即执行顺序）
        private const string InitNodeRobotComm = "RobotComm";
        private const string InitNodeMotion = "Motion";
        private const string InitNodeWeldHead = "WeldHead";
        private const string InitNodeLineLaser = "LineLaser";
        private const string InitNodeMonitorCam = "MonitorCam";

        /// <summary>报警后流程态并存宽限（秒）</summary>
        private const int AlarmFlowGraceSeconds = 3;

        /// <summary>状态一致性巡检周期（毫秒）</summary>
        private const int ConsistencyIntervalMs = 1000;

        /// <summary>关闭总预算（毫秒，超时强制退出并留痕）</summary>
        private const int ShutdownBudgetMs = 30000;

        /// <summary>单台设备断开等待上限（毫秒）</summary>
        private const int CascadeDisconnectTimeoutMs = 5000;

        #endregion

        #region 虚拟IO常量

        /// <summary>虚拟 IO：系统初始化（原档 §10.2 序号 1，映射 Init_IO）。</summary>
        public const string IoInit = "IO_INIT";

        /// <summary>虚拟 IO：设备复位（原档 §10.2 序号 2，映射 Reset_IO）。</summary>
        public const string IoReset = "IO_RESET";

        /// <summary>虚拟 IO：设备启动（原档 §10.2 序号 3，映射 Start_IO）。</summary>
        public const string IoStart = "IO_START";

        /// <summary>虚拟 IO：设备停止（原档 §10.2 序号 4，映射 Stop_IO）。</summary>
        public const string IoStop = "IO_STOP";

        /// <summary>虚拟 IO：消除报警（原档 §10.2 序号 5，映射 ClearAlarm_IO）。</summary>
        public const string IoClearAlarm = "IO_CLEAR_ALARM";

        /// <summary>虚拟 IO：运控总线故障（电平型报警源，IOWork 扫描置位）。</summary>
        public const string IoMotionBusFault = "IO_MOTION_BUS_FAULT";

        /// <summary>虚拟 IO：模组连接（原档 §10.3 序号 7，映射 SubConnect_IO）。</summary>
        public const string IoSubConnect = "IO_SUB_CONNECT";

        /// <summary>虚拟 IO：模组复位（原档 §10.3 序号 8，映射 SubReset_IO）。</summary>
        public const string IoSubReset = "IO_SUB_RESET";

        /// <summary>虚拟 IO：模组焊前检查（原档 §10.3 序号 9，映射 SubPrework_IO）。</summary>
        public const string IoSubPrework = "IO_SUB_PREWORK";

        /// <summary>虚拟 IO：模组焊接启动（原档 §10.3 序号 10，映射 SubWork_IO）。</summary>
        public const string IoSubWork = "IO_SUB_WORK";

        /// <summary>虚拟 IO：模组停止流程（原档 §10.3 序号 11，映射 SubStop_IO）。</summary>
        public const string IoSubStop = "IO_SUB_STOP";

        /// <summary>虚拟 IO：模组手动停止（原档 §10.3 序号 12，映射 SubManualStop_IO）。</summary>
        public const string IoSubManualStop = "IO_SUB_MANUAL_STOP";

        /// <summary>虚拟 IO：模组报警停止（原档 §10.3 序号 13，映射 SubAlarmStop_IO）。</summary>
        public const string IoSubAlarmStop = "IO_SUB_ALARM_STOP";

        #endregion

        #region 私有变量

        private static readonly Lazy<DeviceControlWork> _lazyInstance =
            new Lazy<DeviceControlWork>(() => new DeviceControlWork());

        private readonly string _tag = "主设备运行管控";

        private readonly object _statusLock = new object();
        private MainDeviceStatus _status = MainDeviceStatus.NoReset;
        private MainDeviceStatus _lastStatus = MainDeviceStatus.NoReset;

        /// <summary>上一拍一致性巡检时间戳（Ticks）</summary>
        private long _lastConsistencyTicks;

        /// <summary>报警与流程态并存宽限截止（Ticks）</summary>
        private long _alarmGraceTicks;

        // 原档 R013 属性：流程标志位 + 状态快照，命名沿用
        private volatile bool IsReseting;
        private volatile bool IsWorking;
        private volatile bool IsCloseWork;

        // 阶段应答态：阶段完成后置位供机器人判下一步
        private volatile bool _resetAckPending;
        private volatile bool _preWorkAckPending;
        private volatile bool _weldStartAckPending;

        private volatile bool _isAlarmstop;
        private volatile bool _initializing;
        // 初始化受理未开始的窗口标志
        private volatile bool _initAccepted;

        /// <summary>界面未就绪日志已记，避免逐拍刷屏。</summary>
        private bool _robotCommUiWaitLogged;
        private volatile bool _workStarted;
        private volatile bool _commStarted;
        private volatile bool _robotOnline;

        /// <summary>最近一次心跳分级（边沿日志用）</summary>
        private RobotHeartbeatLevel _heartbeatLevel = RobotHeartbeatLevel.H4;

        /// <summary>通讯线程退出标志 与模组分开</summary>
        private volatile bool _commCloseWork;

        /// <summary>关闭进行中（拒绝新业务、巡检停推）</summary>
        private volatile bool _shuttingDown;

        /// <summary>关闭已完成（幂等守卫）</summary>
        private volatile bool _disposed;

        /// <summary>上一拍上行指令码（0=无指令），用于变化沿去重。</summary>
        private volatile int _lastRxCmdCode;

        private Thread thLaserHeadWork;
        private Thread thLineLaserWork;
        private Thread thMonitorCamWork;
        private Thread thMotionControlWork;
        private Thread thWeldParamControlWork;
        private Thread thHeartInspectWork;
        private Thread thCommCommandWork;

        /// <summary>线程登记表</summary>
        private readonly Dictionary<string, WorkThreadEntry> _workThreads =
            new Dictionary<string, WorkThreadEntry>();
        private readonly object _workThreadLock = new object();

        /// <summary>机器人指令队列（通讯回调只入队，指令线程消费）。</summary>
        private readonly Queue<string> _commCommandQueue = new Queue<string>();
        private readonly object _commLock = new object();

        // ---- 虚拟 IO 层（三种触发源统一收敛）----

        /// <summary>虚拟 IO 置位表：硬件/机器人/UI 三源统一置位。</summary>
        private readonly HashSet<string> _virtualIoSet = new HashSet<string>();
        private readonly object _virtualIoLock = new object();

        /// <summary>急停原因（随 EMG_IO 传递，避免置位时丢失上下文）。</summary>
        private volatile string _emgReason;

        /// <summary>最近一次报警信息（原档 §10.4 序号 17 GetAlarmInfo 数据源）。</summary>
        private MachineAlarmInfo _lastAlarm;

        /// <summary>本轮是否已真正进入 Working，用于区分预检失败。</summary>
        private volatile bool _everWorking;

        /// <summary>子流程是否已因抢占停止驱动（0 驱动中，1 已停止）。</summary>
        private int _driveStopped;

        /// <summary>待下发的焊接工艺参数（原档 §10.5 序号 19 SetWeldParam 写入）。</summary>
        private WeldParamAdaptControl _pendingWeldParam;

        /// <summary>机器人坐标偏移与寄存位参数（原档 §10.5 序号 21 SetRobotCoord 写入）。</summary>
        private readonly object _coordLock = new object();
        private double _coordOffsetX;
        private double _coordOffsetY;
        private double _coordOffsetZ;


        /// <summary>运动管理器（构造获取，供第 2 步初始化，原档 R013）。</summary>
        private MontionManager Motion { get; set; }

        // 级联目标（模组）
        private readonly LineLaserWorkflow _lineLaser = LineLaserWorkflow.Instance;
        private readonly LaserWeldHeadWorkflow _weldHead = LaserWeldHeadWorkflow.Instance;
        private readonly MonitorCameraWorkflow _monitorCam = MonitorCameraWorkflow.Instance;
        private readonly MotionControlWorkflow _motion = MotionControlWorkflow.Instance;
        private readonly WeldParamControlWorkflow _weldParam = WeldParamControlWorkflow.Instance;

        // ---- 模拟测试（主链按设备连接态自动判断） ----

        /// <summary>模拟模式日志标签（模拟相关日志统一归入此类）。</summary>
        private readonly string _simTag = "模拟模式控制器";

        private readonly object _robotXLock = new object();
        private readonly Random _simRand = new Random();
        private const double SimDefaultSpeedMmPerS = 60.0; // 内部默认模拟速度（不再由 UI 设定）
        // 模拟命令注入重试节流（毫秒）：上一动作未结束时候派会被丢弃，需重发
        private const int SimInjectRetryIntervalMs = 1000;
        // 等阶段应答的动态重发：超过等待门限才重发，之后按长间隔重发
        private const int SimAckTimeoutSec = 5;
        private const int SimReinjectIntervalMs = 3000;

        // 模拟是否运行中（单一开关，取代原 SimTestMode A/B/C）
        private volatile bool _simulationRunning;

        // 当前产品码（生产记录 SN；由机器人传输指令下发，空缺时记录侧以 TEST 占位）
        private string _currentProductSn = string.Empty;

        // 共享模拟位姿：线程单入口喂入，流程自 Manager 读
        private bool _robotXSimEnabled;
        // 模拟机器人是否推进：PreWork 段移向寻位起点，焊接段沿焊缝前进
        private bool _simRobotXMoving;
        // 模拟 PreWork 段目标 X（毫米）：到达后停下 X 前进
        private double _simPreWorkTargetX;
        // 模拟 PreWork 段是否已到达
        private bool _simPreWorkArrived;
        private double _simStartX;
        private double _simSpeed;
        private double _simRobotX;
        private DateTime _simStartTime;

        // 模拟行程目标：随机 1500~2000mm，每次重取
        private double _simWeldLengthMm = 1500.0;

        // 模拟调度步：10 进入 20 反馈 21 门控 30 焊接
        private int _simSchedStep;

        // 运控未就绪时挂起受理，按拍轮询后补执行
        private volatile bool _subWorkPending;
        private DateTime _subWorkPendingLogTs;
        private readonly object _subWorkGateLock = new object();

        // 初始化层级驱动：父级未成则子级跳过
        private List<InitNode> _initPlan;
        private int _initNodeIndex;
        private bool _initNodeStarted;
        private DateTime _initStepTs;
        private string _initNodeReason;

        /// <summary>当前节点是否因未启用而应记跳过（而非失败）。</summary>
        private bool _initNodeSkip;

        /// <summary>连接阶段运控就绪标志，决定阶段二使能。</summary>
        private bool _initMotionReady;
        private int _initOkCount;
        private int _initFailCount;
        private int _initSkipCount;
        private readonly Dictionary<string, DeviceInitOutcomeItem> _initOutcomes = new Dictionary<string, DeviceInitOutcomeItem>();
        private List<DeviceInitOutcomeItem> _lastInitOutcomes = new List<DeviceInitOutcomeItem>();

        /// <summary>初始化节点：声明依赖位置与发起方式。</summary>
        private sealed class InitNode
        {
            /// <summary>节点键。</summary>
            public string Key;

            /// <summary>设备中文名。</summary>
            public string DisplayName;

            /// <summary>父节点键；空表示顶层。</summary>
            public string ParentKey;

            /// <summary>是否必须等界面就绪（机器人通讯）。</summary>
            public bool RequiresUiReady;

            /// <summary>是否为受理式异步连接（需轮询 ConnectSettled）。</summary>
            public bool WaitSettle;

            /// <summary>是否参与整机失败判定。</summary>
            public bool Mandatory;

            /// <summary>对应模组状态对象；顶层通讯类节点为空。</summary>
            public DeviceStateBase Device;

            /// <summary>发起连接；同步节点返回真实结果，异步节点返回</summary>
            public Func<bool> Start;

            /// <summary>启动前判跳过；返回非空串即跳过并写明原因。</summary>
            public Func<string> SkipReasonProvider;

            /// <summary>启动前判失败；返回非空串即记失败并写明原因。</summary>
            public Func<string> FailReasonProvider;
        }

        // 焊缝行程监视：Working 时锚定起点与目标，到程走 DoSubStop
        private readonly object _weldMonLock = new object();
        private bool _weldMonActive;
        private double _weldStartX;
        private double _weldTargetMm;

        /// <summary>线激光收工行程</summary>
        private double _laserStopStrokeMm;

        /// <summary>线激光是否已停（一次性，防重复下发）。</summary>
        private bool _laserMeasureFinished;

        private bool _weldCompleteByMonitor;

        // 机器人焊接停止位，行程收口以此为准
        private double _robotStopPoseX;
        private bool _hasRobotStopPose;
        // 机器人焊前位 X（毫米）：焊前寻位的 X 到位判据（由 RX_PRE_WELD_POSE 报文记录）
        private double _robotPreWeldPoseX;
        private bool _hasRobotPreWeldPose;

        // 模拟参数（UI 设置，持久化）——线激光数据频率（30/60/80/120Hz）
        private int _simLaserFrequencyHz = 30;
        // 模拟参数（UI 设置，持久化）——焊接前寻位起点 X（毫米）
        private double _simPreWorkStartXmm = SimulationConfig.DefaultPreWorkStartXmm;

        // 模拟测试线程（随模拟启停，不在 §7.2 的 7 条常驻线程内）
        private Thread thSimTestWork;
        private volatile bool _simWorkClosing;

        // 模拟命令注入的重试节流：上一动作未结束时派发会被丢弃（单动作锁），
        // 故未收到阶段应答时按节流周期重发同一条命令（命令幂等）
        private DateTime _simInjectLastTime = DateTime.MinValue;

        // 等阶段应答的计时起点：超过 SimAckTimeoutSec 仍无应答才重发
        private DateTime _simAckWaitStart = DateTime.MinValue;

        // 模拟参数配置：文件路径与 INI 读写由 SimulationConfig 独占
        private readonly SimulationConfig _simConfig = new SimulationConfig(SimulationConfig.DefaultFilePath);

        /// <summary>模拟停驱的模组使能态，关闭模拟时恢复。</summary>
        private bool _simSavedWeldHeadEnabled;
        private bool _simSavedMonitorCamEnabled;
        // 真机使能是否已挂起：二次点击不得用当前已禁用的使能覆盖首次快照
        private bool _simHardwareSuspended;

        #endregion

        #region 公共变量

        /// <summary>单例实例。</summary>
        public static DeviceControlWork Instance { get { return _lazyInstance.Value; } }

        /// <summary>对中偏差数据中枢（原档 §7.4.5 只读）。</summary>
        public MonitorResult AlignDatum
        {
            get { return _monitorCam.LastResult; }
        }

        /// <summary>当前主设备运行态（Current 快照）。</summary>
        public MainDeviceStatus Status { get { return Current; } }

        /// <summary>主设备运行态变更事件。</summary>
        public event EventHandler<MainDeviceStatusChangedEventArgs> StatusChanged;

        /// <summary>模拟停止事件（各停止路径统一触发，供 UI 复位）。</summary>
        public event EventHandler<SimulationStoppedEventArgs> SimulationStopped;

        /// <summary>初始化链结束事件</summary>
        public event Action<bool> InitializationCompleted;

        /// <summary>初始化汇总事件（成功/失败/跳过分开计数）。</summary>
        public event EventHandler<DeviceInitSummaryEventArgs> InitializationSummaryReady;

        /// <summary>最近一次初始化的逐设备结果（顺序同层级表）。</summary>
        public IList<DeviceInitOutcomeItem> LastInitOutcomes
        {
            get
            {
                lock (_initOutcomes)
                {
                    return new List<DeviceInitOutcomeItem>(_lastInitOutcomes);
                }
            }
        }

        /// <summary>是否焊接中（运行态 Running 且整机已投入工作）。</summary>
        public bool IsWelding
        {
            get { return Current == MainDeviceStatus.Running && IsWorking; }
        }

        /// <summary>自动焊接流程是否在运行中（焊接过程态 ∈ PreWork / Working / Stopping）。</summary>
        public bool IsAutoWeldingRunning
        {
            get
            {
                if (_lineLaser == null) return false;
                SubDeviceWeldStatus status = _lineLaser.WeldStatus;
                return status == SubDeviceWeldStatus.PreWork
                    || status == SubDeviceWeldStatus.Working
                    || status == SubDeviceWeldStatus.Stopping;
            }
        }

        /// <summary>报警停止标志位（供状态层 DeviceStatusWork 读写，沿用参考实现 MachineStatusWork）。</summary>
        public bool IsAlarmstop
        {
            get { return _isAlarmstop; }
            set { _isAlarmstop = value; }
        }

        /// <summary>机器人通讯是否在线（由心跳线程刷新）。</summary>
        public bool IsRobotOnline { get { return _robotOnline; } }

        /// <summary>机器人通讯心跳分级（由心跳线程刷新，只读）。</summary>
        public RobotHeartbeatLevel HeartbeatLevel { get { return _heartbeatLevel; } }

        // ---- 模拟测试（按设备连接态自动判断） ----

        /// <summary>模拟测试是否开启</summary>
        public bool IsSimulationEnabled
        {
            get { return _simulationRunning; }
        }

        /// <summary>当前产品码（SN），为空时以 TEST 占位。</summary>
        public string CurrentProductSn
        {
            get { return _currentProductSn; }
            set { _currentProductSn = value ?? string.Empty; }
        }

        /// <summary>是否使用虚拟调试相机（模拟且未连接时）。</summary>
        public bool ShouldUseVirtualCamera
        {
            get
            {
                if (!_simulationRunning) return false;                 // 生产流程用真实相机
                if (_lineLaser.State != SubDeviceState.Connected) return true;   // 模拟：线激光未连接 → 虚拟
                return LineLaserManager.Instance.IsVirtual;            // 已连接：虚拟相机保持虚拟，真实相机复用真实
            }
        }

        /// <summary>运控是否向运动轴输出实际位移指令。</summary>
        public bool ShouldMoveAxis
        {
            get
            {
                if (!_simulationRunning) return true;                  // 生产流程默认运动
                return _motion.State == SubDeviceState.Connected;      // 模拟：仅运动控制器已连才写轴
            }
        }

        /// <summary>线激光数据频率（Hz，UI 设置，30/60/80/120）。</summary>
        public int SimLaserFrequencyHz
        {
            get { lock (_robotXLock) { return _simLaserFrequencyHz; } }
            set { lock (_robotXLock) { _simLaserFrequencyHz = value; } }
        }

        /// <summary>焊接前寻位起点 X（毫米，UI 设置）：模拟机器人 PreWork 段移动到该点后停。</summary>
        public double SimPreWorkStartXmm
        {
            get { lock (_robotXLock) { return _simPreWorkStartXmm; } }
            set { lock (_robotXLock) { _simPreWorkStartXmm = value < 0.0 ? SimulationConfig.DefaultPreWorkStartXmm : value; } }
        }

        /// <summary>模拟机器人是否已到焊接前寻位起点 X（PreWork 寻位的 X 到位判据）。</summary>
        public bool IsSimPreWorkXArrived
        {
            get { lock (_robotXLock) { return _simPreWorkArrived; } }
        }

        /// <summary>焊前寻位的 X 到位判据：模拟看是否已到设定点，真实看机器人是否到焊前位。</summary>
        public bool IsPreWorkXArrived
        {
            get
            {
                if (IsSimulationEnabled) return IsSimPreWorkXArrived;
                lock (_weldMonLock)
                {
                    if (!_hasRobotPreWeldPose) return false;
                    // 机器人当前 X 接近焊前位 X 即视为到位（容差同运动到位判定量级）
                    double currentX = WeldParamManager.Instance.CurrentRobotX;
                    return Math.Abs(currentX - _robotPreWeldPoseX) <= PreWorkXArrivedToleranceMm;
                }
            }
        }

        /// <summary>焊前寻位的 X 到位容差（毫米）。</summary>
        public const double PreWorkXArrivedToleranceMm = 5.0;

        /// <summary>复位阶段应答：设备复位完成，机器人可给下一步指令。</summary>
        public bool IsResetCompleted
        {
            get { return _resetAckPending; }
        }

        /// <summary>焊前阶段应答：焊前寻位完成（Y 已到焊缝焊接点），机器人可给焊接指令。</summary>
        public bool IsPreWorkCompleted
        {
            get { return _preWorkAckPending; }
        }

        /// <summary>焊接阶段应答：焊接启动已下发。</summary>
        public bool IsWeldStartAcknowledged
        {
            get { return _weldStartAckPending; }
        }

        /// <summary>置复位阶段应答并记日志。</summary>
        private void AckResetCompleted()
        {
            _resetAckPending = true;
            Log("阶段应答 设备复位完成 待机器人给下一步指令");
        }

        /// <summary>置焊前阶段应答并记日志。</summary>
        private void AckPreWorkCompleted()
        {
            _preWorkAckPending = true;
            Log("阶段应答 焊前准备完成 Y 已到焊缝焊接点 待机器人给焊接指令");
        }

        /// <summary>置焊接阶段应答并记日志。</summary>
        private void AckWeldStart()
        {
            _weldStartAckPending = true;
            Log("阶段应答 焊接启动已下发 出光与送丝实际点由机器人控制");
        }

        #endregion

        #region 构造函数

        private DeviceControlWork()
        {
            // 原档 R013 / 范式 M8：构造内取运动管理器并持有
            Motion = MontionManager.Instance;
            // 订阅顶层 Comm 的通讯指令（回调只入队，不做业务）
            GlobalCommData.CommunicationCommandReceived += OnCommRecived;
            // 订阅 Job 切换（属调整，仅焊接中入档）
            LineLaserManager.Instance.JobChanged += OnLineLaserJobChanged;
            Log("主设备运行管控实例化", MessageLevel.Info);
        }

        #endregion

        #region 私有函数

        /// <summary>当前运行态快照（Current/Last 双份用于变化检测）。</summary>
        private MainDeviceStatus Current
        {
            get { lock (_statusLock) { return _status; } }
        }

        /// <summary>上一次运行态快照。</summary>
        private MainDeviceStatus Last
        {
            get { lock (_statusLock) { return _lastStatus; } }
        }

        /// <summary>统一日志出口（固定使用本类标签）。</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(_tag, message, level);
        }

        /// <summary>设备状态合法控制接口：校验状态迁移是否合法（§7.3）。</summary>

        /// <param name="target">目标运行态</param>
        /// <returns>允许迁移返回 true</returns>
        private bool MachineStatusTrans(MainDeviceStatus target)
        {
            MainDeviceStatus now = Current;
            if (now == target) return false;
            // 急停态只能经 EstopCancel 解除
            if (now == MainDeviceStatus.EStop && target != MainDeviceStatus.NoReset) return false;
            // 仅 Stop 可启动；模拟模式放开连接态判断
            if (target == MainDeviceStatus.Running && now != MainDeviceStatus.Stop)
            {
                if (!IsSimulationEnabled || now == MainDeviceStatus.Alarm || now == MainDeviceStatus.EStop) return false;
            }
            // 运行中不允许直接回落未复位，须先 Stop
            if (target == MainDeviceStatus.NoReset && now == MainDeviceStatus.Running) return false;
            return true;
        }

        /// <summary>切换主设备运行态（单控制源，校验后触发事件）。</summary>

        /// <param name="newStatus">目标运行态</param>
        /// <param name="reason">切换原因</param>
        private void SetStatus(MainDeviceStatus newStatus, string reason)
        {
            MainDeviceStatus old;
            bool legal;
            lock (_statusLock)
            {
                if (_status == newStatus) return;
                old = _status;
                legal = MachineStatusTrans(newStatus);
                if (legal)
                {
                    _lastStatus = old;
                    _status = newStatus;
                }
            }

            if (!legal)
            {
                Log(string.Format("非法状态迁移被拒绝 {0} 转 {1} 原因 {2}", old, newStatus, reason), MessageLevel.Warning);
                return;
            }

            // 报警后宽限内允许流程态并存
            if (newStatus == MainDeviceStatus.Alarm)
            {
                Interlocked.Exchange(ref _alarmGraceTicks,
                    DateTime.Now.AddSeconds(AlarmFlowGraceSeconds).Ticks);
            }

            StatusChanged?.Invoke(this, new MainDeviceStatusChangedEventArgs
            {
                OldStatus = old,
                NewStatus = newStatus,
                Reason = reason,
                Timestamp = DateTime.Now
            });
            // 变化沿唯一一条日志
            Log(string.Format("设备状态 {0} 转 {1} 原因 {2}", old, newStatus, reason));
        }

        /// <summary>驱动 5 个子流程各自执行焊接工作态迁移。</summary>
        private void WeldStatusTrans()
        {
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, _weldParam };
            var names = new[] { "焊接头", "线激光", "监控相机", "运控", "焊接工艺" };
            for (int i = 0; i < flows.Length; i++)
            {
                try { flows[i].WeldStatusTrans(); }
                catch (Exception ex) { Log(string.Format("{0} 焊接工作态迁移异常 {1}", names[i], ex.Message), MessageLevel.Error); }
            }
            // todo Preworking 五项检查与 Working 四路子流程落地后，
            //      整机按检查结果驱动子流程依次进入
        }

        /// <summary>创建并启动一条后台常驻工作线程（范式 M2：一律 IsBackground）。</summary>
        /// <param name="body">线程体</param>
        /// <param name="name">线程名（调试用）</param>
        /// <returns>已启动的线程</returns>
        private Thread NewWorkThread(ThreadStart body, string name)
        {
            var thread = new Thread(body)
            {
                Name = name,
                IsBackground = true
            };
            RegisterWorkThread(thread, WorkThreadJoinTimeoutMs);
            thread.Start();
            return thread;
        }

        /// <summary>登记线程：名称/句柄/超时/状态。同名重复覆盖旧项。</summary>

        /// <param name="thread">待登记线程（已创建，启动前后均可）</param>
        /// <param name="timeoutMs">退出等待上限（毫秒，≤0 取默认值）</param>
        internal void RegisterWorkThread(Thread thread, int timeoutMs)
        {
            if (thread == null || string.IsNullOrEmpty(thread.Name)) return;
            lock (_workThreadLock)
            {
                _workThreads[thread.Name] = new WorkThreadEntry
                {
                    Name = thread.Name,
                    Thread = thread,
                    TimeoutMs = timeoutMs > 0 ? timeoutMs : WorkThreadJoinTimeoutMs,
                    RegisteredAt = DateTime.Now,
                    Exited = false,
                    JoinTimedOut = false
                };
            }
        }

        /// <summary>线程登记表快照。名称=运行中/已退出/超时。</summary>

        /// <returns>以空格分隔的登记摘要；表为空返回「无线程登记」</returns>
        public string GetWorkThreadSnapshot()
        {
            List<string> items = new List<string>();
            lock (_workThreadLock)
            {
                foreach (KeyValuePair<string, WorkThreadEntry> pair in _workThreads)
                {
                    WorkThreadEntry entry = pair.Value;
                    string state;
                    if (entry.Exited) state = "已退出";
                    else if (entry.JoinTimedOut) state = "等待超时";
                    else if (entry.Thread != null && entry.Thread.IsAlive) state = "运行中";
                    else state = "已停止";
                    items.Add(entry.Name + "=" + state);
                }
            }
            return items.Count == 0 ? "无线程登记" : string.Join(" ", items.ToArray());
        }

        /// <summary>模组工作线程公共体（五段式）。</summary>

        /// <param name="flow">子流程</param>
        /// <param name="name">模组中文名（日志用）</param>
        private void SubDeviceWork(DeviceWorkflowBase flow, string name)
        {
            bool wasDriving = false;   // 上一拍是否处于 Running 驱动中（区分「启动默认态」与「真实被抢占」）
            while (!IsCloseWork)
            {
                // 每拍问询运行态：非 Running 立即停驱
                if (Current != MainDeviceStatus.Running)
                {
                    // 清步骤计时，防暂停时长被计入超时
                    flow.ResetWorkTime();
                    if (wasDriving) LogDriveStopped();
                    wasDriving = false;
                    Thread.Sleep(BeatIdleMs);
                    continue;
                }

                if (_driveStopped != 0) Interlocked.Exchange(ref _driveStopped, 0);
                wasDriving = true;

                try { flow.FlowProcess(); }
                catch (Exception ex) { Log(name + " 流程执行异常 " + ex.Message, MessageLevel.Error); }

                // 到程判断归主控：任一线程轮询，命中即下发完成
                if (_weldMonActive) CheckWeldProgress();

                // 启动门控挂起：按拍轮询就绪即补执行级联
                CheckSubWorkPending();

                Thread.Sleep(BeatWorkMs);
            }
        }

        /// <summary>整机停止驱动提醒：从 Running 跌出时记一次。</summary>
        private void LogDriveStopped()
        {
            if (Interlocked.CompareExchange(ref _driveStopped, 1, 0) != 0) return;
            Log("运行态已变更为 " + Current + " 子流程停止驱动", MessageLevel.Warning);
        }

        /// <summary>焊接头工作线程：驱动 LaserWeldHeadWorkflow。</summary>
        private void LaserHeadWork() { SubDeviceWork(_weldHead, "焊接头"); }

        /// <summary>线激光工作线程：驱动 LineLaserWorkflow。</summary>
        private void LineLaserWork() { SubDeviceWork(_lineLaser, "线激光"); }

        /// <summary>监控相机工作线程：驱动 MonitorCameraWorkflow。</summary>
        private void MonitorCamWork() { SubDeviceWork(_monitorCam, "监控相机"); }

        /// <summary>运控工作线程：驱动 MotionControlWorkflow。</summary>
        private void MotionControlWork() { SubDeviceWork(_motion, "运控"); }

        /// <summary>焊接工艺工作线程：驱动 WeldParamControlWorkflow。</summary>
        private void WeldParamControlWork() { SubDeviceWork(_weldParam, "焊接工艺"); }

        /// <summary>心跳只检测分级 不负责重连。</summary>
        private void HeartInspectWork()
        {
            RobotHeartbeatLevel last = RobotHeartbeatLevel.H4;
            while (!_commCloseWork)
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null || !comm.IsRobotEnabled)
                {
                    _robotOnline = false;
                    Thread.Sleep(HeartBeatIntervalMs);
                    continue;
                }

                // 在线显示与心跳日志解耦
                _robotOnline = comm.IsRobotOnline;
                RobotHeartbeatLevel level = EvaluateHeartbeatLevel(comm);
                _heartbeatLevel = level;
                if (level != last)
                {
                    LogHeartbeatEdge(last, level, comm.NeedHeartbeat);
                    last = level;
                }
                Thread.Sleep(HeartBeatIntervalMs);
            }
        }

        /// <summary>按收包间隔判定心跳分级。</summary>
        /// <param name="comm">通讯管理器</param>
        /// <returns>当前分级；H2 由阶段 7 缺值距离接入</returns>
        private RobotHeartbeatLevel EvaluateHeartbeatLevel(CommunicationManager comm)
        {
            if (!comm.RobotConnected) return RobotHeartbeatLevel.H4;
            DateTime lastReceive = comm.LastRobotReceiveTime;
            if (lastReceive == DateTime.MinValue) return RobotHeartbeatLevel.H3;
            double ageMs = (DateTime.Now - lastReceive).TotalMilliseconds;
            if (ageMs > 500) return RobotHeartbeatLevel.H3;
            if (IsNoValueOverLimit()) return RobotHeartbeatLevel.H2;
            if (ageMs > comm.RsiCycleMs * 2) return RobotHeartbeatLevel.H1;
            return RobotHeartbeatLevel.H0;
        }

        /// <summary>判缺值累计是否已超阈值</summary>
        /// <returns>超阈值返回 true</returns>
        private static bool IsNoValueOverLimit()
        {
            WeldParamConfig config = WeldParamManager.Instance.AlgorithmsConfig;
            if (config == null || config.NoValueToleranceMm <= 0.0) return false;
            return WeldParamManager.Instance.NoValueDistanceMm >= config.NoValueToleranceMm;
        }

        /// <summary>心跳分级变化沿记一条，避免逐拍刷屏。</summary>
        /// <param name="from">原分级</param>
        /// <param name="to">新分级</param>
        /// <param name="needHeartbeat">是否启用分级日志</param>
        private void LogHeartbeatEdge(RobotHeartbeatLevel from, RobotHeartbeatLevel to, bool needHeartbeat)
        {
            if (!needHeartbeat) return;
            if (to == RobotHeartbeatLevel.H4)
                Log("机器人通讯心跳 H4 监听未运行 通讯层自动重连", MessageLevel.Error);
            else if (to == RobotHeartbeatLevel.H3)
                Log("机器人通讯心跳 H3 断流超过 500ms 界面显示离线", MessageLevel.Warning);
            else if (to == RobotHeartbeatLevel.H2)
                Log("机器人通讯心跳 H2 引导缺值超阈值 已由缺值报警停焊处置", MessageLevel.Debug);
            else if (to == RobotHeartbeatLevel.H1)
                Log("机器人通讯心跳 H1 单拍抖动 原分级 " + from, MessageLevel.Debug);
            else if (to == RobotHeartbeatLevel.H0)
                Log("机器人通讯心跳 H0 正常", MessageLevel.Info);
        }

        /// <summary>机器人指令交互线程（常驻循环判断，§7.2）。</summary>
        private void CommCommandWork()
        {
            while (!_commCloseWork)
            {
                // 通讯层自愈 通道未监听按5s重连
                var comm = GlobalCommData.mCommunicationManager;
                if (comm != null && comm.IsRobotEnabled) comm.EnsureRobotChannelHealthy();

                string command = DequeueCommand();
                if (string.IsNullOrEmpty(command))
                {
                    Thread.Sleep(BeatCommandMs);
                    continue;
                }

                try
                {
                    string ioName = MapCommandToIo(command);
                    if (string.IsNullOrEmpty(ioName))
                    {
                        if (command == CommandCore.RxAbort)
                            Log("机器人指令 RxAbort 急停已同步执行", MessageLevel.Error);
                        else if (command == CommandCore.RxWeldStopPose)
                        {
                            // 停止位置不是收终点，仍须走完前置距离才结束
                            double cachedStopX;
                            bool hasStopX;
                            lock (_weldMonLock) { hasStopX = _hasRobotStopPose; cachedStopX = _robotStopPoseX; }
                            Log(hasStopX
                                ? string.Format("机器人指令 RX_WELD_STOP_POSE 收到停止位置 停止位X {0:F1}mm 行程监视将再走前置距离后才结束", cachedStopX)
                                : "机器人指令 RX_WELD_STOP_POSE 收到停止位置 报文未带X 行程监视将再走前置距离后才结束",
                                MessageLevel.Info);
                        }
                        else if (CommandCore.CommandToTxCodeValue(command) > 0)
                        {
                            // 下行指令仅入队留痕，执行方是机器人侧
                            Log("机器人下行指令 " + command + " 已下发 无需本地执行", MessageLevel.Info);
                        }
                        else
                            Log("机器人指令 " + command + " 为坐标上报类，仅记录不驱动状态转换", MessageLevel.Info);
                        continue;
                    }

                    // 动作类指令执行前校验整机与模组状态
                    if (!IsCommandPreconditionMet(command, ioName)) continue;

                    SetVirtualIo(ioName);
                    Log(string.Format("机器人指令 {0} 已转换为虚拟 IO {1}", command, ioName));

                    // 请求进入焊前时同置焊前准备 IO，趁 X 移动中建图
                    if (command == CommandCore.RxPreWeldRequest)
                    {
                        SetVirtualIo(IoSubPrework);
                        Log("机器人指令 RX_PRE_WELD_REQUEST 已转换为虚拟 IO " + IoSubPrework
                            + " 焊前准备在机器人移向焊前位期间启动");
                    }
                    // todo 指令闭环反馈机器人执行结果待补齐
                }
                catch (Exception ex)
                {
                    Log("指令转 IO 异常 " + ex.Message, MessageLevel.Error);
                }
            }
        }

        /// <summary>取出一条待处理机器人指令。</summary>
        /// <returns>指令字符串；队列为空返回 null</returns>
        private string DequeueCommand()
        {
            lock (_commLock)
            {
                return _commCommandQueue.Count > 0 ? _commCommandQueue.Dequeue() : null;
            }
        }

        /// <summary>机器人指令 → 虚拟 IO 映射（原档 §2.3）。</summary>

        /// <param name="command">已规整的指令常量</param>
        /// <returns>映射到的虚拟 IO 名；坐标上报类等无对应动作时返回 null</returns>
        private string MapCommandToIo(string command)
        {
            switch (command)
            {
                // 动作类：机器人急停 → 直接同步执行
                case CommandCore.RxAbort:
                    EstopMachine("机器人主动急停指令");
                    return null;

                // 请求进入焊前 → 整机启动 IO；焊前准备由线激光态触发
                case CommandCore.RxPreWeldRequest:
                    return IoStart;

                // 动作类：机器人到归零位 → 触发设备复位（TA/Y/焊接头回零位）
                // 复位完成后主控置复位应答态，机器人据此给下一步指令
                case CommandCore.RxHomePose:
                    return IoReset;

                // 动作类：机器人到安全停止位 → 停止 IO
                case CommandCore.RxStopSafePose:
                    return IoStop;

                // 坐标类：仅记录焊前位 X，不映射阶段 IO
                case CommandCore.RxPreWeldPose:
                    return null;

                // 动作类：机器人完成「从焊前位移动到焊接起始位」→ 触发焊接启动
                // 真正工作自 PreWork 起，Working 是正式出光闭环
                case CommandCore.RxWeldStartPose:
                    return IoSubWork;

                // 坐标类：解析后仅记录，不驱动状态转换
                case CommandCore.RxStartSafePose:
                case CommandCore.RxPreStopPose:
                case CommandCore.RxWeldStopPose:
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>机器人指令前置条件问询（按目标 IO 校验）。</summary>

        /// <param name="command">已规整的指令常量（日志用）</param>
        /// <param name="ioName">映射到的虚拟 IO 名</param>
        /// <returns>前置条件满足返回 true</returns>
        private bool IsCommandPreconditionMet(string command, string ioName)
        {
            if (ioName == IoStart)
            {
                // 整机已 Running 时放行：RX_PRE_WELD_REQUEST 的语义是「请求进入焊前」，
                // 整机启动只是其前置（真实流程里整机可能已启动）。
                // DoStartMachine 已幂等（Running 态直接返回 true），重复置位无害。
                if (Current != MainDeviceStatus.Stop && Current != MainDeviceStatus.Running)
                {
                    Log(string.Format("机器人指令 {0} 拒收 整机非 Stop/Running 态 当前 {1}", command, Current), MessageLevel.Warning);
                    return false;
                }
                foreach (var pair in GetAllSubDeviceStatus())
                {
                    if (pair.Value != SubDeviceWeldStatus.ErrorAborted) continue;
                    Log(string.Format("机器人指令 {0} 拒收 模组存在报警态 {1}", command, pair.Key), MessageLevel.Warning);
                    return false;
                }
                return true;
            }

            if (ioName == IoStop && Current != MainDeviceStatus.Running)
            {
                Log(string.Format("机器人指令 {0} 拒收 整机非 Running 态 当前 {1}", command, Current), MessageLevel.Warning);
                return false;
            }

            // 焊前/焊接 IO 不拒收，运行态由扫描线程保留重试
            if ((ioName == IoSubPrework || ioName == IoSubWork) && Current != MainDeviceStatus.Running)
            {
                Log(string.Format("机器人指令 {0} 当前非 Running 态 {1} 命令已置位 待运行态就绪后派发",
                    command, Current), MessageLevel.Info);
            }
            return true;
        }

        /// <summary>机器人通讯指令回调与解析（只解码入队）。</summary>
        /// <param name="sender">事件源</param>
        /// <param name="message">通讯原始报文</param>
        private void OnCommRecived(object sender, string message)
        {
            string normalized = CommandCore.Normalize(message);
            if (normalized.Length == 0) return;

            string command = normalized;
            // payload 须先初始化：短路时不进 TrySplitPayload
            string payload = string.Empty;
            if (!CommandCore.IsKnownCommand(normalized)
                && !CommandCore.TrySplitPayload(normalized, out command, out payload))
            {
                Log("收到未知机器人指令 " + normalized, MessageLevel.Warning);
                return;
            }

            // 停止位报文：首个数即机器人停止位 X
            if (command == CommandCore.RxWeldStopPose
                && TryParseFirstDouble(payload, out double stopPoseX))
            {
                ApplyRobotStopPose(stopPoseX);
            }

            // 焊前位报文：首个数即机器人焊前位 X（焊前寻位的 X 到位判据）
            if (command == CommandCore.RxPreWeldPose
                && TryParseFirstDouble(payload, out double preWeldPoseX))
            {
                ApplyRobotPreWeldPose(preWeldPoseX);
            }

            lock (_commLock) { _commCommandQueue.Enqueue(command); }
        }

        /// <summary>模拟注入机器人命令：仿真器扮演机器人角色，与真实指令同队列同处理链。</summary>
        /// <param name="command">机器人指令常量（CommandCore.Rx*）</param>
        /// <param name="payloadX">可选坐标载荷（毫米），会按坐标类指令解析记录</param>
        public void InjectSimRobotCommand(string command, double? payloadX = null)
        {
            if (string.IsNullOrEmpty(command)) return;
            string normalized = CommandCore.Normalize(command);
            if (!CommandCore.IsKnownCommand(normalized))
            {
                Log("模拟注入机器人指令失败 未知指令 " + normalized, MessageLevel.Warning);
                return;
            }

            if (payloadX.HasValue)
            {
                if (normalized == CommandCore.RxPreWeldPose)
                {
                    ApplyRobotPreWeldPose(payloadX.Value);
                }
                else if (normalized == CommandCore.RxWeldStopPose)
                {
                    ApplyRobotStopPose(payloadX.Value);
                }
            }

            lock (_commLock) { _commCommandQueue.Enqueue(normalized); }
            Log("模拟注入机器人指令 " + normalized, MessageLevel.Info);
        }

        /// <summary>记录机器人焊前位 X（焊前寻位到位判据用）。</summary>
        /// <param name="preWeldX">机器人焊前位 X（毫米）</param>
        private void ApplyRobotPreWeldPose(double preWeldX)
        {
            lock (_weldMonLock)
            {
                _robotPreWeldPoseX = preWeldX;
                _hasRobotPreWeldPose = true;
            }
            Log(string.Format("机器人焊前位 X {0:F1}mm 记录 焊前寻位以此为到位判据", preWeldX));
        }

        /// <summary>记录机器人停止位 X，并即时更新行程监视目标。</summary>

        /// <param name="stopX">机器人停止位 X（毫米，焊缝终止 X）</param>
        private void ApplyRobotStopPose(double stopX)
        {
            double lead = WeldParamManager.Instance.AlgorithmsConfig.LeadDistanceMm;
            string logLine = null;
            lock (_weldMonLock)
            {
                _robotStopPoseX = stopX;
                _hasRobotStopPose = true;
                if (_weldMonActive)
                {
                    _weldTargetMm = Math.Abs(stopX - _weldStartX);          // 机器人停止目标 = 焊缝终止行程
                    // 线激光收工行程随终止位更新；已收工则不再回改（避免重复触发）
                    if (!_laserMeasureFinished) _laserStopStrokeMm = Math.Max(0.0, _weldTargetMm - lead);
                    logLine = string.Format("机器人停止位 X {0:F1}mm 焊缝终止行程 {1:F1}mm 线激光收工行程 {2:F1}mm(前置量 {3:F1}mm)",
                        stopX, _weldTargetMm, _laserStopStrokeMm, lead);
                }
            }
            if (logLine != null) Log(logLine);
        }

        /// <summary>从报文载荷提取首个数值（跳过前导非数字字符）。</summary>
        /// <param name="payload">载荷原文</param>
        /// <param name="value">输出数值</param>
        /// <returns>解析成功返回 true</returns>
        private static bool TryParseFirstDouble(string payload, out double value)
        {
            value = 0.0;
            if (string.IsNullOrEmpty(payload)) return false;
            int i = 0;
            while (i < payload.Length && !char.IsDigit(payload[i]) && payload[i] != '-' && payload[i] != '+') i++;
            if (i >= payload.Length) return false;
            int start = i;
            if (payload[i] == '-' || payload[i] == '+') i++;
            bool dot = false;
            while (i < payload.Length && (char.IsDigit(payload[i]) || (payload[i] == '.' && !dot)))
            {
                if (payload[i] == '.') dot = true;
                i++;
            }
            return double.TryParse(payload.Substring(start, i - start), out value);
        }

        /// <summary>机器人数据接收回调（RSI 数值化协议：按 RxCmd 数值码解码</summary>

        /// <param name="sender">事件源（机器人通讯层）</param>
        /// <param name="e">机器人上报数据事件参数</param>
        private void OnRobotDataReceived(object sender, KUKADataReceivedEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            // 机器人置码保持 ≥3 周期，按变化沿去重只处理一次
            if (e.RxCmd == 0)
            {
                _lastRxCmdCode = 0;
                return;
            }
            if (e.RxCmd == _lastRxCmdCode)
            {
                return;
            }
            _lastRxCmdCode = e.RxCmd;
            HandleRxCommand(e.RxCmd, e.RxPayload);
        }

        /// <summary>通讯开启（返回监听是否真正启动成功）。</summary>

        /// <returns>监听真正启动返回 true</returns>
        private bool CommWork()
        {
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null || !comm.IsRobotEnabled)
            {
                _initNodeSkip = true;
                _initNodeReason = "未启用机器人通讯";
                return false;
            }

            try
            {
                comm.InitializeRobotComm();

                // 重复初始化先退订再订阅，避免回调重复注册
                comm.RobotDataReceived -= OnRobotDataReceived;
                comm.RobotDataReceived += OnRobotDataReceived;
                _lastRxCmdCode = 0;
                bool listening = comm.ConnectRobot();
                // 监听开启≠机器人在线：在线判据以实际收包为准
                _robotOnline = comm.IsRobotOnline;
                if (listening)
                {
                    // 监听开启后才启动心跳线程
                    EnsureHeartbeatThread();
                    _initNodeReason = "监听已开启 等待机器人首包";
                    return true;
                }
                string detail = comm.LastRobotConnectError;
                _initNodeReason = string.IsNullOrEmpty(detail) ? "RSI 监听未启动" : detail;
                return false;
            }
            catch (Exception ex)
            {
                _initNodeReason = "通讯层异常 " + ex.Message;
                return false;
            }
        }

        /// <summary>下发机器人指令（经顶层 Comm，连接本身由 Comm 管控）。</summary>

        /// <param name="command">指令常量（CommandCore.Tx*）</param>
        private void SendRobotCommand(string command)
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null) return;
                if (comm.IsRobotEnabled)
                {
                    int code = CommandCore.CommandToTxCodeValue(command);
                    if (code == 0)
                    {
                        Log(string.Format("下发机器人指令失败 未知下行指令 {0}", command), MessageLevel.Warning);
                        return;
                    }
                    comm.SendRobotCommand(code);
                    // 下发侧广播：Dash 指令行同步显示 Tx 指令
                    GlobalCommData.BroadcastCommunicationCommand(command);
                }
            }
            catch (Exception ex)
            {
                Log("下发机器人指令异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>清除已锁存的机器人下行指令（报警解除时停发 TX_NG）。</summary>
        private void ClearRobotCommand()
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null) return;
                comm.ClearRobotCommand();
            }
            catch (Exception ex)
            {
                Log("清除机器人指令异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>单个模组断开受理（容错：异常记日志不抛出）。</summary>
        /// <param name="child">子流程</param>
        /// <param name="name">模组中文名（日志用）</param>
        /// <returns>断开请求已受理返回 true</returns>
        private bool DisconnectChild(DeviceWorkflowBase child, string name)
        {
            try
            {
                // 级联关停属程序编排，显式置自动模式
                child.SetRunMode(WorkflowRunMode.Auto);
                bool accepted = child.ConnectOff();
                Log(name + (accepted ? " 已受理断开" : " 断开请求被拒绝"), MessageLevel.Debug);
                return accepted;
            }
            catch (Exception ex)
            {
                Log(name + " 断开异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>级联模组断开：受理 + 等待 + 超时。</summary>

        /// <returns>确认断开的设备台数（0~4）</returns>
        private int CascadeDisconnectWithWait()
        {
            var devices = new DeviceWorkflowBase[] { _weldHead, _motion, _lineLaser, _monitorCam };
            var names = new[] { "焊接头从站", "运动控制器", "线激光相机", "监控相机" };
            int ok = 0;
            for (int i = 0; i < devices.Length; i++)
            {
                DisconnectChild(devices[i], names[i]);
                if (WaitChildDisconnected(devices[i], names[i])) ok++;
            }
            Log(string.Format("级联断开完成 设备 {0}/4", ok),
                ok == 4 ? MessageLevel.Info : MessageLevel.Warning);
            return ok;
        }

        /// <summary>等待单个模组落到断开态。</summary>
        /// <param name="child">子流程</param>
        /// <param name="name">模组中文名（日志用）</param>
        /// <returns>确认断开返回 true</returns>
        private bool WaitChildDisconnected(DeviceWorkflowBase child, string name)
        {
            if (child == null) return true;
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < CascadeDisconnectTimeoutMs)
            {
                if (child.State == SubDeviceState.Disconnected) return true;
                Thread.Sleep(50);
            }
            Log(name + " 未在 " + CascadeDisconnectTimeoutMs + "ms 内断开 保留现场待复查", MessageLevel.Warning);
            return false;
        }

        /// <summary>级联模组断开（兼容入口）。</summary>
        private void CascadeDisconnect()
        {
            CascadeDisconnectWithWait();
        }

        /// <summary>聚合判定初始化结果。</summary>

        /// <returns>无强制失败返回 true（跳过不算失败）</returns>
        private bool EvaluateInitializationResult()
        {
            int okCount = 0;
            int failCount = 0;
            int skipCount = 0;
            bool mandatoryFailed = false;
            _initMotionReady = false;
            List<InitNode> plan = _initPlan == null ? new List<InitNode>() : _initPlan;

            lock (_initOutcomes)
            {
                foreach (InitNode node in plan)
                {
                    if (!_initOutcomes.TryGetValue(node.Key, out DeviceInitOutcomeItem item)) continue;
                    if (node.Key == InitNodeMotion && item.Outcome == DeviceInitOutcome.Success)
                        _initMotionReady = true;
                    if (item.Outcome == DeviceInitOutcome.Success)
                    {
                        okCount++;
                    }
                    else if (item.Outcome == DeviceInitOutcome.Skipped)
                    {
                        skipCount++;
                    }
                    else
                    {
                        failCount++;
                        if (node.Mandatory) mandatoryFailed = true;
                    }
                }
            }

            _initOkCount = okCount;
            _initFailCount = failCount;
            _initSkipCount = skipCount;
            if (mandatoryFailed) SetStatus(MainDeviceStatus.Alarm, "模组初始化失败");
            return !mandatoryFailed;
        }

        /// <summary>初始化完成后按配置统一使能轴（一次性）。</summary>

        /// <returns>全部轴使能成功（无失败轴）返回 true</returns>
        private bool EnableAxesAfterInit()
        {
            if (Motion == null || !Motion.IsInitialized)
            {
                return false;
            }
            try
            {
                return Motion.EnableAxesAfterInit();
            }
            catch (Exception ex)
            {
                Log("阶段二 使能 异常 原因 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>判断机器人通讯在位（RSI 或 EKI 任一连接）。</summary>
        /// <returns>在位返回 true；管理器未就绪视为不在位</returns>
        private bool IsRobotReachable()
        {
            var comm = GlobalCommData.mCommunicationManager;
            return comm != null && comm.IsRobotEnabled && comm.RobotConnected;
        }

        /// <summary>初始化是否在途（供互锁门控旁路）。</summary>
        public bool IsInitializing
        {
            get { return _initializing || _initAccepted; }
        }

        /// <summary>关闭进行中（拒绝新业务，巡检停推）。</summary>
        public bool IsShuttingDown
        {
            get { return _shuttingDown; }
        }

        /// <summary>初始化目标是否已全部连接（补连判据）。</summary>

        /// <returns>全部已连接返回 true</returns>
        private bool AllInitTargetsConnected()
        {
            List<InitNode> plan = _initPlan ?? BuildInitPlan();
            foreach (InitNode node in plan)
            {
                if (node.Device != null)
                {
                    if (node.Device.State != SubDeviceState.Connected) return false;
                }
                else if (node.Key == InitNodeRobotComm && !IsRobotReachable())
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>长动作链中止判定：运行态被抢占即中止。</summary>

        /// <param name="expected">本链要求的运行态</param>
        /// <param name="action">动作名（日志用）</param>
        /// <returns>已中止返回 true</returns>
        private bool IsActionAborted(MainDeviceStatus expected, string action)
        {
            MainDeviceStatus now = Current;
            if (now == expected) return false;
            Log(string.Format("{0}被中止 运行态已变更为 {1}", action, now), MessageLevel.Warning);
            return true;
        }

        /// <summary>初始化链抢占判定（静默版，供等待循环每拍问询）。</summary>
        /// <returns>运行态已变为 EStop / Reseting / Running 返回 true</returns>
        private bool IsInitPreempted()
        {
            MainDeviceStatus now = Current;
            return now == MainDeviceStatus.EStop || now == MainDeviceStatus.Reseting || now == MainDeviceStatus.Running;
        }

        /// <summary>级联子流程复位。</summary>

        /// <returns>全部受理返回 true；中途被抢占返回 false</returns>
        private bool CascadeResetProcess()
        {
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, _weldParam };
            var names = new[] { "焊接头", "线激光", "监控相机", "运控", "焊接工艺" };
            bool allAccepted = true;
            for (int i = 0; i < flows.Length; i++)
            {
                // 复位链被抢占即中止，日志由调用方记一次
                if (Current != MainDeviceStatus.Reseting) return false;

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
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, _weldParam };
            var names = new[] { "焊接头", "线激光", "监控相机", "运控", "焊接工艺" };
            for (int i = 0; i < flows.Length; i++)
            {
                try { flows[i].ClearStatus(); }
                catch (Exception ex) { Log(string.Format("{0} 清状态异常 {1}", names[i], ex.Message), MessageLevel.Warning); }
            }
        }

        /// <summary>运动控制器 + CAMBOX 跟踪工作流初始化。</summary>
        private void InitializeMotionControl()
        {
            try
            {
                // 初始化链属程序编排，显式置自动模式
                _motion.SetRunMode(WorkflowRunMode.Auto);
                // 失败由工作流/设备层报错，此处不置 Connected、不重复报错
                if (_motion.InitializeOn())
                {
                    _motion.ConnectOn();
                }
            }
            catch (Exception ex)
            {
                _initNodeReason = "异常 " + ex.Message;
            }
        }

        /// <summary>机器人通讯初始化（RSI/EKI 二选一，走 Comm 提供的方法）。</summary>
        /// <returns>监听真正启动返回 true</returns>
        private bool InitializeRobotComm()
        {
            return CommWork();
        }

        /// <summary>模拟挂起的线激光连接回调。</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">连接态变更参数</param>
        private void OnSimLineLaserStateChanged(object sender, SubDeviceStateChangedEventArgs e)
        {
            if (e.NewState != SubDeviceState.Connected) return;
            _lineLaser.StateSwitched -= OnSimLineLaserStateChanged;
            // 初始化期间关闭模拟即放弃启动，避免误进流程
            if (!_simulationRunning) return;
            try { _lineLaser.Start(); }
            catch (Exception ex) { Log("模拟-线激光启动失败 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>开启模拟测试线程。</summary>
        private void SimTestWorkOpen()
        {
            _simWorkClosing = false;
            if (thSimTestWork != null && thSimTestWork.IsAlive) return;
            thSimTestWork = NewWorkThread(SimTestWork, "thSimTestWork");
            GlobalCommData.ShowLog(_simTag, "模拟测试线程已开启", MessageLevel.Info);
        }

        /// <summary>关闭模拟测试线程：置标志并等待退出。</summary>
        private void SimTestWorkClose()
        {
            if (thSimTestWork == null && !_simWorkClosing) return;
            _simWorkClosing = true;

            Thread old = thSimTestWork;
            if (old != null && old.IsAlive)
            {
                if (old.Join(WorkThreadJoinTimeoutMs)) thSimTestWork = null;
                else GlobalCommData.ShowLog(_simTag,
                    "模拟测试线程未在 " + WorkThreadJoinTimeoutMs + "ms 内退出，保留句柄待复查",
                    MessageLevel.Warning);
            }
            GlobalCommData.ShowLog(_simTag, "模拟测试线程已关闭", MessageLevel.Info);
        }

        /// <summary>模拟线程主体：按拍推进位姿时钟与仿真调度。</summary>
        private void SimTestWork()
        {
            while (!_simWorkClosing)
            {
                try
                {
                    SimTick();
                    SimScheduleTick();
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_simTag, "模拟测试线程异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(SimBeatMs);
            }
        }

        /// <summary>模拟单拍：推进模拟位姿时钟并单入口喂入。</summary>
        private void SimTick()
        {
            lock (_robotXLock)
            {
                if (!_robotXSimEnabled) return;
                if (_simRobotXMoving)
                {
                    double elapsed = (DateTime.Now - _simStartTime).TotalSeconds;
                    double next = _simStartX + _simSpeed * elapsed;
                    // 到寻位起点即停，不越位
                    if (!_simPreWorkArrived && _simPreWorkTargetX > 0.0 && next >= _simPreWorkTargetX)
                    {
                        next = _simPreWorkTargetX;
                        _simPreWorkArrived = true;
                        _simRobotXMoving = false;
                        GlobalCommData.ShowLog(_simTag,
                            string.Format("模拟机器人已到焊接前寻位起点 X {0:F1}mm X 停止前进", _simPreWorkTargetX),
                            MessageLevel.Info);
                    }
                    _simRobotX = next;
                }
            }
            // 模拟位姿单入口喂入，数据生成收口在本类
            WeldParamManager.Instance.UpdateRobotPose(_simRobotX, 0.0, 0.0);
        }

        /// <summary>动作入口双闸门：设备态 × 就绪层。</summary>

        /// <param name="action">动作名</param>
        /// <param name="required">要求就绪等级</param>
        /// <param name="flow">就绪主体（可空）</param>
        /// <param name="reason">拒绝原因</param>
        /// <returns>放行返回 true</returns>
        private bool CheckActionGate(string action, ReadinessStage required, DeviceWorkflowBase flow, out string reason)
        {
            reason = string.Empty;
            if (_shuttingDown)
            {
                reason = "正在关闭 拒绝动作 " + action;
                Log("拒绝 " + reason, MessageLevel.Warning);
                return false;
            }
            if (IsSimulationEnabled) return true;
            if (Current != MainDeviceStatus.Running)
            {
                reason = string.Format("设备态 {0} 非 Running 动作 {1}", Current, action);
                Log("拒绝 " + reason, MessageLevel.Warning);
                return false;
            }
            return flow == null || flow.EnsureReadiness(required, action, out reason);
        }

        /// <summary>模组焊前检查命令（S2 → S3）。</summary>
        private void DoSubPrework()
        {
            if (!CheckActionGate("焊前检查", ReadinessStage.Connected, _lineLaser, out _)) return;
            // 幂等守卫：以运控焊前态为准
            if (_motion.WeldStatus == SubDeviceWeldStatus.PreWork)
            {
                Log("焊前检查已在本会话下发 重复派发忽略");
                return;
            }
            try { WeldParamManager.Instance.ResetSeamTracking("焊前检查会话复位"); }
            catch (Exception ex) { Log("焊前检查会话复位异常 " + ex.Message, MessageLevel.Warning); }
            CascadeWeldStatus(SubDeviceWeldStatus.PreWork, "模组焊前检查");
            try { WeldParamControlWorkflow.Instance.PreWork(); }
            catch (Exception ex) { Log("焊前预置工艺参数异常 " + ex.Message, MessageLevel.Warning); }
            // 焊前工作模式：TA/LA/振镜进工作位与摆动，Y 由运控焊前寻位步跟随到焊缝焊接点
            EnterPreWorkWorkingModes();
        }

        /// <summary>焊前阶段工作模式：TA 自动压紧、LA 自动随动、振镜按自适应基准值开启摆动。</summary>
        private void EnterPreWorkWorkingModes()
        {
            try
            {
                if (!_motion.TaExecuteAuto())
                {
                    Log("焊前 TA 自动模式未就绪 后续按现场状态判定", MessageLevel.Warning);
                }
            }
            catch (Exception ex) { Log("焊前 TA 自动模式异常 " + ex.Message, MessageLevel.Warning); }

            // 焊接头从站未装载（节点号撞号等配置缺硬件）属已知状态：
            // 直接跳过 LA/振镜下发，只记一条提示，避免把「缺硬件」记成流程 Error
            if (!_weldHead.IsSlaveReady)
            {
                Log("焊前 LA 与振镜跳过 焊接头从站未装载 后续按现场状态判定", MessageLevel.Warning);
                return;
            }

            try
            {
                if (!_weldHead.LaAutoAuto())
                {
                    Log("焊前 LA 自动模式未就绪 后续按现场状态判定", MessageLevel.Warning);
                }
            }
            catch (Exception ex) { Log("焊前 LA 自动模式异常 " + ex.Message, MessageLevel.Warning); }

            try
            {
                if (!_weldHead.GalvoWeaveStartAuto())
                {
                    Log("焊前 振镜摆动启动未成功 后续按现场状态判定", MessageLevel.Warning);
                }
            }
            catch (Exception ex) { Log("焊前 振镜摆动启动异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>模组焊接启动命令（原档 §10.3 序号 10：S3 → S4）。</summary>
        private void DoSubWork()
        {
            // 未复位或未到工作零位即拒绝并给原因
            if (!CheckActionGate("自动焊接", ReadinessStage.AtWorkZero, _motion, out _)) return;
            if (!IsSubWorkGateOpen())
            {
                _subWorkPending = true;
                LogPendingGateOnce();
                return;
            }
            _subWorkPending = false;
            CascadeWeldStatus(SubDeviceWeldStatus.Working, "模组焊接启动");
            // 模拟机器人此刻起步（等效收到主控 PreWork 完成反馈）。
            // 时钟已在 PreWork 启动，此处只开移动：必须在 BeginWeldMonitor 前，
            // 它读 GetEffectiveRobotX 作为行程起点。
            if (IsSimulationEnabled) StartRobotXMoving();
            BeginWeldMonitor();
            try { ProductFileRecordManager.Instance.BeginProductSession(CurrentProductSn, IsSimulationEnabled); }
            catch (Exception ex) { Log("开启生产会话异常 " + ex.Message, MessageLevel.Warning); }
            // 焊接阶段应答：出光与送丝实际点由机器人控制
            AckWeldStart();
        }

        /// <summary>焊接启动门控开闭判定（焊前寻位未完成时关门）。</summary>

        /// <returns>允许立即下发 Working 返回 true</returns>
        private bool IsSubWorkGateOpen()
        {
            // 寻位未完成时关门
            return !(_motion.WeldStatus == SubDeviceWeldStatus.PreWork && !_motion.IsPreWorkSeekReady);
        }

        /// <summary>焊前阶段完成按拍问询：运控焊前寻位完成即置应答，机器人据此给焊接指令。</summary>
        private void CheckPreWorkCompleted()
        {
            if (_preWorkAckPending) return;
            if (_motion == null) return;
            if (_motion.WeldStatus != SubDeviceWeldStatus.PreWork) return;
            if (!_motion.IsPreWorkSeekReady) return;
            AckPreWorkCompleted();
        }

        /// <summary>焊接启动挂起按拍问询（门开补执行/失败弃单）。</summary>
        private void CheckSubWorkPending()
        {
            CheckPreWorkCompleted();
            if (!_subWorkPending) return;
            lock (_subWorkGateLock)
            {
                if (!_subWorkPending) return;
                if (Current != MainDeviceStatus.Running)
                {
                    _subWorkPending = false;
                    Log("焊接启动挂起弃单 整机已离开运行态");
                    return;
                }
                if (_motion.WeldStatus == SubDeviceWeldStatus.ErrorAborted)
                {
                    _subWorkPending = false;
                    Log("焊接启动弃单 运控 PreWork 失败（标定位回归超时或下发失败）", MessageLevel.Error);
                    return;
                }
                if (!IsSubWorkGateOpen())
                {
                    LogPendingGateOnce();
                    return;
                }
                _subWorkPending = false;
                DoSubWork();
            }
        }

        /// <summary>焊接启动等待告警（2 秒限频，等待期每 2s 提示一次）。</summary>
        private void LogPendingGateOnce()
        {
            if ((DateTime.Now - _subWorkPendingLogTs).TotalSeconds < 2) return;
            _subWorkPendingLogTs = DateTime.Now;
            Log("焊接启动等待焊前寻位完成（Y 未到达焊缝焊接点）", MessageLevel.Warning);
        }

        /// <summary>模组停止流程命令（原档 §10.3 序号 11：S4 → S5）。</summary>
        private void DoSubStop()
        {
            string reason = _weldCompleteByMonitor ? "焊缝行程完成 主控下发焊接完成" : "模组停止流程";
            EndWeldMonitor();
            SimMonitorCamStopWeld();
            CascadeWeldStatus(SubDeviceWeldStatus.Stopping, reason);
            EndProductSessionSafely("pass", string.Empty);
        }

        /// <summary>模组手动停止命令：人工正常收尾，记为 pass。</summary>
        private void DoSubManualStop()
        {
            _subWorkPending = false;
            EndWeldMonitor();
            SimMonitorCamStopWeld();
            CascadeWeldStatus(SubDeviceWeldStatus.ManualStopped, "模组手动停止");
            EndProductSessionSafely("pass", string.Empty);
        }

        /// <summary>模组报警停止命令（原档 §10.3 序号 13：S4 → S6）：异常收尾，记为 fail。</summary>
        private void DoSubAlarmStop()
        {
            _subWorkPending = false;
            EndWeldMonitor();
            SimMonitorCamStopWeld();
            CascadeWeldStatus(SubDeviceWeldStatus.ErrorAborted, "模组报警停止");
            EndProductSessionSafely("fail", "模组报警停止");
        }

        /// <summary>锚定焊缝行程监视（起点取当前等效机器人 X）。</summary>
        private void BeginWeldMonitor()
        {
            double lead = WeldParamManager.Instance.AlgorithmsConfig.LeadDistanceMm;
            bool useRobotPose;
            bool hasTerminal;
            double stopStroke;
            lock (_weldMonLock)
            {
                _weldStartX = GetEffectiveRobotX();
                useRobotPose = _hasRobotStopPose;
                hasTerminal = useRobotPose || IsSimulationEnabled;
                stopStroke = useRobotPose
                    ? Math.Abs(_robotStopPoseX - _weldStartX)
                    : (IsSimulationEnabled ? _simWeldLengthMm : LineLaserWorkflow.Instance.MaxWeldLengthMm);
                _weldTargetMm = stopStroke;                        // 机器人停止目标 = 焊缝终止行程（不再叠加前置量）
                _laserStopStrokeMm = hasTerminal ? Math.Max(0.0, stopStroke - lead) : 0.0;
                _laserMeasureFinished = false;
                _weldMonActive = true;
                _weldCompleteByMonitor = false;
            }
            Log(string.Format("焊缝行程监视开始 起点X {0:F1}mm 焊缝终止X {1:F1}mm 终止行程 {2:F1}mm 前置量 {3:F1}mm 机器人停止目标 {4:F1}mm 线激光收工行程 {5:F1}mm 终止位来源 {6}",
                _weldStartX,
                useRobotPose ? _robotStopPoseX : double.NaN,
                stopStroke, lead, _weldTargetMm, _laserStopStrokeMm,
                useRobotPose ? "机器人上报" : (IsSimulationEnabled ? "模拟长度兜底" : "真实占位空缺(待机器人下发)")));
        }

        /// <summary>结束焊缝行程监视（各停止命令收口时调用）。</summary>
        private void EndWeldMonitor()
        {
            lock (_weldMonLock) { _weldMonActive = false; }
        }

        /// <summary>焊缝行程判断（唯一归属主控）。</summary>
        private void CheckWeldProgress()
        {
            if (!_weldMonActive) return;

            double strokeNow = Math.Abs(GetEffectiveRobotX() - _weldStartX);
            bool laserDue;
            lock (_weldMonLock)
            {
                if (!_weldMonActive) return;
                laserDue = !_laserMeasureFinished && _laserStopStrokeMm > 0.0 && strokeNow >= _laserStopStrokeMm;
                if (laserDue) _laserMeasureFinished = true;
            }
            if (laserDue)
            {
                Log(string.Format("线激光测量行程完成 X行程 {0:F1}mm 收工行程 {1:F1}mm 关激光与传感器 入图结束（CSPoutY 继续输出到焊缝结束 X {2:F1}mm）",
                    strokeNow, _laserStopStrokeMm, _weldStartX + _weldTargetMm));
                // 只收线激光，不动运控跟踪输出，流程态留 Working
                // 运控继续出图记录，直到走到焊缝结束位
                try { LineLaserWorkflow.Instance.FinishMeasureByStroke(); }
                catch (Exception ex) { Log("线激光收工异常 " + ex.Message, MessageLevel.Warning); }
            }

            lock (_weldMonLock)
            {
                if (!_weldMonActive) return;
                if (Math.Abs(GetEffectiveRobotX() - _weldStartX) < _weldTargetMm) return;
                _weldMonActive = false;
                _weldCompleteByMonitor = true;
            }
            Log(string.Format("焊缝行程完成 X行程 {0:F1}mm 终止行程 {1:F1}mm 主控下发焊接完成（线激光已于 {2:F1}mm 收工）",
                Math.Abs(GetEffectiveRobotX() - _weldStartX), _weldTargetMm, _laserStopStrokeMm));
            try { DoSubStop(); }
            catch (Exception ex) { Log("主控下发焊接完成异常 " + ex.Message, MessageLevel.Warning); }
            // 到程即自动终止模拟，避免 X 持续前进
            try { DisableSimulation("焊缝行程完成", hasError: false, isAuto: true); }
            catch (Exception ex) { Log("焊缝行程完成自动停止模拟异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>结束生产会话：一次性落库并归档三份记录。</summary>
        /// <param name="result">焊接结果（正常焊接完成为 pass，异常焊接为 fail）</param>
        /// <param name="info">异常焊接原因（result=fail 时填写）</param>
        private void EndProductSessionSafely(string result, string info)
        {
            try { ProductFileRecordManager.Instance.EndProductSession(result, info); }
            catch (Exception ex) { Log("结束生产会话异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>线激光分析 Job 切换回调（切换属调整的一种）。</summary>

        /// <param name="sender">事件源（线激光管理器）</param>
        /// <param name="jobId">切换到的 Job 编号</param>
        private void OnLineLaserJobChanged(object sender, int jobId)
        {
            if (_lineLaser.WeldStatus != SubDeviceWeldStatus.Working) return;
            try
            {
                ProductFileRecordManager.Instance.RecordJobSwitch(
                    jobId, GetEffectiveRobotX(), WeldParamManager.Instance.CurrentRobotY);
            }
            catch (Exception ex) { Log("记录 Job 切换异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>模拟仿真器调度（由模拟线程每拍驱动）。</summary>
        private void SimScheduleTick()
        {
            // 参与设备异常或急停即终止；不判 Alarm，模拟不依赖真实硬件态
            if (_simulationRunning && (Current == MainDeviceStatus.EStop
                || _motion.WeldStatus == SubDeviceWeldStatus.ErrorAborted
                || _lineLaser.WeldStatus == SubDeviceWeldStatus.ErrorAborted
                || _weldParam.WeldStatus == SubDeviceWeldStatus.ErrorAborted))
            {
                Log("模拟期间检测到参与设备异常或急停 自动终止模拟", MessageLevel.Error);
                try { EndProductSessionSafely("fail", "模拟期间模组异常自动停止"); }
                catch (Exception ex) { Log("模拟异常结束生产会话异常 " + ex.Message, MessageLevel.Warning); }
                try { DisableSimulation("模组报警自动停止", hasError: true, isAuto: true); }
                catch (Exception ex) { Log("模拟异常自动停止异常 " + ex.Message, MessageLevel.Warning); }
                return;
            }

            // 复位在途不推进大步
            if (!SimResetGateTick()) return;

            switch (_simSchedStep)
            {
                case 5:
                    // 到归零位：注入 Home，主控据此复位
                    InjectSimRobotCommand(CommandCore.RxHomePose);
                    Log("模拟仿真器 注入机器人指令 RX_HOME_POSE 等主控完成设备复位");
                    _simSchedStep = 6;
                    break;

                case 6:
                    // 等复位应答，完成后起位姿时钟与子流程
                    if (!IsResetCompleted) break;
                    Log("模拟仿真器 主控复位阶段完成 等下一步指令");
                    // 整机启动由 RX_PRE_WELD_REQUEST 触发，此处不自启
                    if (IsSimulationEnabled) StartRobotXSimulation();
                    StartSimulationSubflowsAfterReset();
                    _simSchedStep = 7;
                    break;

                case 7:
                    // 请求进入焊前：发出才推进，拒发则原地重试
                    if (TryInjectSimCommand(CommandCore.RxPreWeldRequest))
                    {
                        StartRobotXSeeking();
                        Log("模拟仿真器 注入机器人指令 RX_PRE_WELD_REQUEST 等主控下发焊前准备");
                        _simSchedStep = 8;
                    }
                    break;

                case 8:
                    // 等整机运行且线激光进焊前
                    if (Current == MainDeviceStatus.Running
                        && _lineLaser.WeldStatus == SubDeviceWeldStatus.PreWork)
                    {
                        Log("模拟仿真器 整机已运行 线激光已进入焊前检查 等待完成反馈");
                        _simSchedStep = 20;
                    }
                    break;

                case 20:
                    // 等焊前完成反馈
                    if (_lineLaser.IsPreWorkFeedbackDone) _simSchedStep = 21;
                    break;

                case 21:
                    // 到焊前位：记录焊前位 X，发出才推进
                    if (TryInjectSimCommand(CommandCore.RxPreWeldPose, GetSimPreWorkTargetX()))
                    {
                        Log("模拟仿真器 注入机器人指令 RX_PRE_WELD_POSE 记录焊前位 X");
                        _simAckWaitStart = DateTime.Now;
                        _simSchedStep = 23;
                    }
                    break;

                case 23:
                    // 等焊前应答，超时重发请求
                    if (IsPreWorkCompleted)
                    {
                        Log("模拟仿真器 主控焊前阶段完成 模拟机器人从焊前位移动到焊接起始位");
                        _simSchedStep = 24;
                        break;
                    }
                    if (TryReinjectOnAckTimeout(CommandCore.RxPreWeldRequest))
                    {
                        Log("模拟仿真器 焊前阶段应答超时 重发 RX_PRE_WELD_REQUEST");
                    }
                    break;

                case 24:
                    // 到焊接起始位：注入焊接启动命令
                    if (TryInjectSimCommand(CommandCore.RxWeldStartPose, GetSimPreWorkTargetX()))
                    {
                        Log("模拟仿真器 注入机器人指令 RX_WELD_START_POSE 等主控下发焊接启动");
                        _simAckWaitStart = DateTime.Now;
                        _simSchedStep = 25;
                    }
                    break;

                case 25:
                    // 等焊接启动应答，超时重发
                    if (IsWeldStartAcknowledged)
                    {
                        Log("模拟仿真器 主控焊接启动已下发 出光与送丝实际点由机器人控制");
                        SimMonitorCamStartWeld();
                        _simSchedStep = 30;
                        break;
                    }
                    if (TryReinjectOnAckTimeout(CommandCore.RxWeldStartPose, GetSimPreWorkTargetX()))
                    {
                        Log("模拟仿真器 焊接启动应答超时 重发 RX_WELD_START_POSE");
                    }
                    break;

                case 30:
                    // 等待到程收口
                    break;
            }
        }

        /// <summary>限频注入模拟机器人命令（命令幂等）。</summary>
        /// <param name="command">机器人指令常量</param>
        /// <param name="payloadX">可选坐标载荷</param>
        /// <returns>本次发出返回 true；节流窗口内返回 false</returns>
        private bool TryInjectSimCommand(string command, double? payloadX = null)
        {
            DateTime now = DateTime.Now;
            if ((now - _simInjectLastTime).TotalMilliseconds < SimInjectRetryIntervalMs) return false;
            return FireSimCommand(command, payloadX, now);
        }

        /// <summary>应答超时后重发同一命令。</summary>
        /// <param name="command">机器人指令常量</param>
        /// <param name="payloadX">可选坐标载荷</param>
        /// <returns>本次重发返回 true；未超时或未到间隔返回 false</returns>
        private bool TryReinjectOnAckTimeout(string command, double? payloadX = null)
        {
            DateTime now = DateTime.Now;
            if ((now - _simAckWaitStart).TotalSeconds < SimAckTimeoutSec) return false;
            if ((now - _simInjectLastTime).TotalMilliseconds < SimReinjectIntervalMs) return false;
            return FireSimCommand(command, payloadX, now);
        }

        /// <summary>注入并记录节流时刻。</summary>
        private bool FireSimCommand(string command, double? payloadX, DateTime now)
        {
            _simInjectLastTime = now;
            InjectSimRobotCommand(command, payloadX);
            return true;
        }

        /// <summary>取模拟焊前位 X（机器人到位坐标注入用）。</summary>
        /// <returns>配置的焊接前寻位起点 X（毫米）</returns>
        private double GetSimPreWorkTargetX()
        {
            lock (_robotXLock) { return _simPreWorkTargetX; }
        }

        /// <summary>向 5 个模组统一下发焊接态迁移请求。</summary>

        /// <param name="target">目标焊接过程态</param>
        /// <param name="reason">迁移原因</param>
        private void CascadeWeldStatus(SubDeviceWeldStatus target, string reason)
        {
            _weldHead.RequestWeldStatus(target, reason);
            _motion.RequestWeldStatus(target, reason);
            _lineLaser.RequestWeldStatus(target, reason);
            _monitorCam.RequestWeldStatus(target, reason);
            _weldParam.RequestWeldStatus(target, reason);
            Log(string.Format("已向 5 个模组下发焊接态迁移 {0} 原因 {1}", target, reason));
            WeldStatusTrans();
        }

        #region 模拟测试焊接相机（自动运行模式，预览走监控相机调试页）

        /// <summary>模拟 Ready 检查焊接相机：扫描确认配置 IP 在线后打开设</summary>
        private void PrepareSimulationMonitorCam()
        {
            var cam = MonitorCamManager.Instance;
            string ip = cam.Config != null ? cam.Config.IpAddress : string.Empty;
            bool online = false;
            try
            {
                foreach (MonitorCamDeviceInfo dev in cam.ScanDevices())
                {
                    if (!string.IsNullOrEmpty(ip) && string.Equals(dev.Ip, ip, StringComparison.OrdinalIgnoreCase))
                    {
                        online = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("模拟焊接相机在线检查异常 " + ex.Message, MessageLevel.Warning);
            }
            if (!online)
            {
                Log("模拟焊接相机不在线 IP " + ip + " 不阻断模拟 焊接启动时无预览与录像", MessageLevel.Warning);
                return;
            }
            string port = cam.Config != null ? cam.Config.Port : string.Empty;
            if (cam.OpenSensor(ip, port))
                Log("模拟焊接相机在线检查通过 设备已打开 IP " + ip, MessageLevel.Info);
            else
                Log("模拟焊接相机打开失败 IP " + ip + " 不阻断模拟", MessageLevel.Warning);
        }

        /// <summary>焊接启动（等效机器人 Start）：开采集与录像。</summary>
        private void SimMonitorCamStartWeld()
        {
            var cam = MonitorCamManager.Instance;
            if (!cam.StartCapture())
            {
                Log("模拟焊接相机开启连续采集失败 本轮无预览与录像", MessageLevel.Warning);
                return;
            }
            // 焊接开始即开主页面预览，节流在工作流内
            MonitorCameraWorkflow.Instance.StartWeldPreview();
            if (MonitorCameraWorkflow.Instance.StartRecording(MonitorCameraWorkflow.WeldRecordPrefix, out string fileName, out _))
                Log("模拟焊接相机已开启连续采集与录像 " + fileName, MessageLevel.Info);
            else
                Log("模拟焊接相机开启录像失败 仅连续采集预览", MessageLevel.Warning);
        }

        /// <summary>焊接结束收口焊接相机：先关录像再停采集</summary>
        private void SimMonitorCamStopWeld()
        {
            // 预览先退订，采集已停时同样收口
            MonitorCameraWorkflow.Instance.StopWeldPreview();
            var cam = MonitorCamManager.Instance;
            bool wasRecording = cam.IsRecording;
            bool wasCapturing = cam.IsCapturing;
            if (!wasRecording && !wasCapturing) return;
            try
            {
                if (wasRecording) cam.StopRecord();
                if (wasCapturing) cam.StopCapture();
            }
            catch (Exception ex)
            {
                Log("模拟焊接相机关闭异常 " + ex.Message, MessageLevel.Warning);
                return;
            }
            Log(string.Format("模拟焊接相机已收口 关录像 {0} 停采集 {1}", wasRecording, wasCapturing), MessageLevel.Info);
        }

        #endregion

        /// <summary>处理上行指令数值码（解码入队 + 停止位载荷）。</summary>

        /// <param name="rxCmd">上行指令数值码（1~9）</param>
        /// <param name="rxPayload">上行指令载荷（当前仅 RX_WELD_STOP_POSE 使用：停止位 X / mm）</param>
        public void HandleRxCommand(int rxCmd, double rxPayload)
        {
            string command = CommandCore.RxCodeToCommandName(rxCmd);
            if (string.IsNullOrEmpty(command))
            {
                Log(string.Format("收到未知上行指令码 {0}", rxCmd), MessageLevel.Warning);
                return;
            }

            // 停止位报文：RxPayload 即机器人停止位 X
            if (command == CommandCore.RxWeldStopPose && rxPayload != 0.0)
            {
                ApplyRobotStopPose(rxPayload);
            }

            lock (_commLock) { _commCommandQueue.Enqueue(command); }
        }

        #endregion

        #region 公共函数

        /// <summary>虚拟 IO 置位（三种触发源统一入口）。</summary>

        /// <param name="ioName">虚拟 IO 常量（<c>IoInit</c> / <c>IoReset</c> / <c>IoStart</c> 等）</param>
        public void SetVirtualIo(string ioName)
        {
            if (string.IsNullOrEmpty(ioName)) return;
            lock (_virtualIoLock) { _virtualIoSet.Add(ioName); }
        }

        /// <summary>虚拟 IO 清位。</summary>
        /// <param name="ioName">虚拟 IO 常量</param>
        public void ClearVirtualIo(string ioName)
        {
            if (string.IsNullOrEmpty(ioName)) return;
            lock (_virtualIoLock) { _virtualIoSet.Remove(ioName); }
        }

        /// <summary>查询虚拟 IO 是否置位（电平型查询，不清位）。</summary>

        /// <param name="ioName">虚拟 IO 常量</param>
        /// <returns>已置位返回 true</returns>
        public bool IsVirtualIoSet(string ioName)
        {
            if (string.IsNullOrEmpty(ioName)) return false;
            lock (_virtualIoLock) { return _virtualIoSet.Contains(ioName); }
        }

        /// <summary>取走虚拟 IO 并清位（脉冲型命令，保证每条命令只执</summary>

        /// <param name="ioName">虚拟 IO 常量</param>
        /// <returns>命中并已清位返回 true；未置位返回 false</returns>
        public bool TakeVirtualIo(string ioName)
        {
            if (string.IsNullOrEmpty(ioName)) return false;
            lock (_virtualIoLock) { return _virtualIoSet.Remove(ioName); }
        }

        /// <summary>清空全部虚拟 IO（整机停机 / 清状态时使用）。</summary>
        public void ClearAllVirtualIo()
        {
            lock (_virtualIoLock) { _virtualIoSet.Clear(); }
        }

        /// <summary>虚拟 IO 全量快照（原档 §10.4 序号 18 GetIOState）。</summary>
        /// <returns>当前所有已置位的虚拟 IO 名数组；无置位时返回空数组</returns>
        public string[] SnapshotVirtualIo()
        {
            lock (_virtualIoLock)
            {
                var snapshot = new string[_virtualIoSet.Count];
                _virtualIoSet.CopyTo(snapshot);
                return snapshot;
            }
        }

        /// <summary>虚拟 IO 执行分派（由 thIOWork / thEMGWork 扫描命中后调用）。</summary>

        /// <param name="ioName">已命中的虚拟 IO 常量</param>
        /// <returns>已分派执行返回 true；IO 名未登记返回 false</returns>
        public bool ExecuteIo(string ioName)
        {
            if (_shuttingDown)
            {
                Log("正在关闭 忽略 IO " + ioName, MessageLevel.Warning);
                return false;
            }
            switch (ioName)
            {
                case IoInit:
                    DoInitializeMachine();
                    return true;

                case IoReset:
                    DoResetMachine();
                    return true;

                case IoStart:
                    DoStartMachine();
                    return true;

                case IoStop:
                    DoStopMachine();
                    return true;

                case IoClearAlarm:
                    DoClearAlarm();
                    return true;

                case IoMotionBusFault:
                    DoMotionBusFault();
                    return true;

                case IoSubPrework:
                    DoSubPrework();
                    return true;

                case IoSubWork:
                    DoSubWork();
                    return true;

                case IoSubStop:
                    DoSubStop();
                    return true;

                case IoSubManualStop:
                    DoSubManualStop();
                    return true;

                case IoSubAlarmStop:
                    DoSubAlarmStop();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>预检失败收敛（原档 §9：M4 → M3、S3 → S2，不报警可重新发起）。</summary>
        public void CheckPreworkFailed()
        {
            if (Current != MainDeviceStatus.Running) return;
            if (IsSimulationEnabled) return;   // 模拟不判断模组状态，跳过预检失败回落
            if (_everWorking) return;

            var all = GetAllSubDeviceStatus();
            foreach (var pair in all)
            {
                if (pair.Value == SubDeviceWeldStatus.Working)
                {
                    _everWorking = true;
                    return;
                }
                if (pair.Value == SubDeviceWeldStatus.Standby)
                {
                    Log("预检失败 模组 " + pair.Key + " 退回待机，整机回落停止（不报警，可重新发起）",
                        MessageLevel.Warning);
                    SetStatus(MainDeviceStatus.Stop, "预检失败 退回待机");
                    return;
                }
            }
        }

        /// <summary>状态一致性与子流程报警巡检。</summary>
        public void ConsistencyTick()
        {
            long now = DateTime.Now.Ticks;
            if (now - Interlocked.Read(ref _lastConsistencyTicks)
                < ConsistencyIntervalMs * TimeSpan.TicksPerMillisecond) return;
            Interlocked.Exchange(ref _lastConsistencyTicks, now);

            if (IsSimulationEnabled || IsInitializing) return;

            if (Current == MainDeviceStatus.Running || Current == MainDeviceStatus.Stop)
            {
                string failed = FindAbortedFlowName();
                if (failed != null)
                {
                    string reason = "子流程报警终止 " + failed;
                    Log(reason, MessageLevel.Error);
                    ReportAlarm(reason);
                    return;
                }
            }

            CorrectIllegalCombination();
        }

        /// <summary>纠正三类非法状态组合。</summary>
        private void CorrectIllegalCombination()
        {
            if (Current == MainDeviceStatus.Stop && HasFlowStatus(SubDeviceWeldStatus.Working))
            {
                Log("非法状态组合 Stop 与 Working 纠正为待机", MessageLevel.Warning);
                CascadeWeldStatus(SubDeviceWeldStatus.Standby, "一致性巡检纠正");
                return;
            }

            if (Current == MainDeviceStatus.NoReset && HasFlowStatusOtherThan(SubDeviceWeldStatus.NoReset))
            {
                Log("非法状态组合 NoReset 与流程态纠正为未复位", MessageLevel.Warning);
                CascadeWeldStatus(SubDeviceWeldStatus.NoReset, "一致性巡检纠正");
                return;
            }

            if ((Current == MainDeviceStatus.Alarm || Current == MainDeviceStatus.EStop)
                && HasFlowStatus(SubDeviceWeldStatus.Working)
                && DateTime.Now.Ticks > Interlocked.Read(ref _alarmGraceTicks))
            {
                Log("非法状态组合 " + Current + " 与 Working 纠正为报警终止", MessageLevel.Warning);
                CascadeWeldStatus(SubDeviceWeldStatus.ErrorAborted, "一致性巡检纠正");
            }
        }

        /// <summary>是否存在指定流程态。</summary>
        private bool HasFlowStatus(SubDeviceWeldStatus target)
        {
            foreach (var pair in GetAllSubDeviceStatus())
            {
                if (pair.Value == target) return true;
            }
            return false;
        }

        /// <summary>是否存在非指定流程态。</summary>
        private bool HasFlowStatusOtherThan(SubDeviceWeldStatus target)
        {
            foreach (var pair in GetAllSubDeviceStatus())
            {
                if (pair.Value != target) return true;
            }
            return false;
        }

        /// <summary>首个报警终止的流程名。</summary>
        private string FindAbortedFlowName()
        {
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, _weldParam };
            for (int i = 0; i < flows.Length; i++)
            {
                if (flows[i].WeldStatus == SubDeviceWeldStatus.ErrorAborted) return flows[i].Tag;
            }
            return null;
        }

        #region 光丝对中消费与剪丝回检

        /// <summary>Y 向红灯（超报警阈值）报警边沿锁存</summary>
        private bool _alignFailAlarmActive;

        /// <summary>Y 向黄灯提示限频计数（拍）</summary>
        private int _alignWarnLogBeat;

        /// <summary>剪丝回检相位：0 空闲 / 1 等超时 / 2 回检等待。</summary>
        private int _cutWirePhase;

        /// <summary>相位内拍计数（IOWork 节拍，50ms/拍）</summary>
        private int _cutWireBeat;

        /// <summary>本轮异常累计剪丝次数</summary>
        private int _cutWireCycleCount;

        /// <summary>剪丝降级锁存（反复异常超上限，转人工处理）</summary>
        private bool _cutWireDegraded;

        /// <summary>已消费的对中结果时间戳（只消费新结果）</summary>
        private DateTime _lastAlignTimestamp;

        /// <summary>X 向送丝补偿（mm，负值=焊丝偏长需减送）</summary>
        private double _wireFeedCompensation;

        /// <summary>剪丝请求超时拍数（200 拍 = 10s；无回执通道须超时兜底）</summary>
        private const int CutWireRequestTimeoutBeats = 200;

        /// <summary>单轮最大自动剪丝次数，超过降级转人工</summary>
        private const int MaxCutWireCycles = 3;

        /// <summary>X 向送丝补偿（毫米，只读；送丝指令经 <see cref="ComposeWireFeedSpeed"/> 单点消费）。</summary>
        public double WireFeedCompensation { get { return _wireFeedCompensation; } }

        /// <summary>剪丝请求（RSI 具名元素 CutWire 通道，机器人侧执行剪丝程序）。</summary>

        /// <param name="on">true=请求剪丝，false=复位</param>
        private void SendCutWireRequest(bool on)
        {
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null) return;
            WeldParamAdaptControl output = WeldParamControlWorkflow.Instance.LastOutput;
            double power = output != null ? output.LaserPower : 0.0;
            double wire = ComposeWireFeedSpeed(output != null ? output.FeedSpeed : 0.0);
            comm.UpdateRobotWeldParams(power, wire, on ? 1.0 : 0.0);
        }

        /// <summary>光丝对中消费与剪丝回检（IOWork 每拍调用）。</summary>
        public void MonitorAlignTick()
        {
            // 报警停焊 停止对中与剪丝回检
            if (IsAlarmstop)
            {
                if (_cutWirePhase != 0)
                {
                    _cutWirePhase = 0;
                    _cutWireBeat = 0;
                    Log("报警停焊 已终止剪丝回检 不再刷新机器人载荷", MessageLevel.Warning);
                }
                return;
            }

            MonitorResult snap = AlignDatum;
            if (snap == null || snap.Phase != MonitorCameraPhase.PreWeldAlignment) return;
            if (snap.Timestamp == _lastAlignTimestamp) return;
            _lastAlignTimestamp = snap.Timestamp;

            WireFeedConfig wireCfg = AlgorithmManager.Instance.WireFeed;
            double failMm = wireCfg != null ? wireCfg.CenterFailMm : 0.3;

            // Y 向三级（不自动调整）
            if (snap.Grade == WireAlignGrade.Fail && !_alignFailAlarmActive)
            {
                _alignFailAlarmActive = true;
                string reason = string.Format("光丝对中横向偏差超限 当前 {0:F2}mm 上限 {1:F2}mm", snap.LateralDevMm, failMm);
                Log(reason, MessageLevel.Error);
                ReportAlarm(reason);
            }
            else if (snap.Grade != WireAlignGrade.Fail && _alignFailAlarmActive)
            {
                _alignFailAlarmActive = false;
                Log(string.Format("光丝对中横向偏差回到限内 当前 {0:F2}mm", snap.LateralDevMm), MessageLevel.Info);
                ReportAlarmCleared();
            }
            else if (snap.Grade == WireAlignGrade.Warn)
            {
                if (++_alignWarnLogBeat >= 100)
                {
                    _alignWarnLogBeat = 0;
                    Log(string.Format("光丝对中提示 黄灯 横向偏差 {0:F2}mm 请人工关注", snap.LateralDevMm), MessageLevel.Warning);
                }
            }
            else
            {
                _alignWarnLogBeat = 0;
            }

            // X 向送丝补偿（与送丝指令在 ComposeWireFeedSpeed 单点耦合防冲突）
            double gain = wireCfg != null ? wireCfg.WireFeedCompGain : 1.0;
            _wireFeedCompensation = -snap.WireStickoutDeviation * gain;

            // 对中偏差→振镜中心微调，默认关闭仅监控
            ApplySpotAlignCompensation();

            // 剪丝异常与回检循环
            if (!snap.WireAnomalyDetected)
            {
                if (_cutWirePhase == 0)
                {
                    _cutWireCycleCount = 0;
                    _cutWireDegraded = false;
                }
                return;
            }
            if (_cutWireDegraded) return;

            switch (_cutWirePhase)
            {
                case 0:
                    if (wireCfg == null || !wireCfg.WireAnomalyCutWire) return;
                    if (_cutWireCycleCount >= MaxCutWireCycles)
                    {
                        _cutWireDegraded = true;
                        string reason = "焊丝异常反复 剪丝回检超上限 请人工处理";
                        Log(reason, MessageLevel.Error);
                        ReportAlarm(reason);
                        return;
                    }
                    _cutWireCycleCount++;
                    _cutWirePhase = 1;
                    _cutWireBeat = 0;
                    Log(string.Format("焊丝异常 第 {0} 次回检 下发剪丝请求 原因 {1}",
                        _cutWireCycleCount, snap.WireAnomalyReason), MessageLevel.Warning);
                    SendCutWireRequest(true);
                    break;

                case 1:
                    if (++_cutWireBeat < CutWireRequestTimeoutBeats) return;
                    SendCutWireRequest(false);
                    _cutWirePhase = 2;
                    _cutWireBeat = 0;
                    Log("剪丝请求超时复位 回逻辑1 重新检测");
                    break;

                default:
                    _cutWirePhase = 0;
                    break;
            }
        }

        /// <summary>组装送丝速度：工艺送丝 + X 向补偿，钳不小于 0（送丝机</summary>

        /// <param name="algoFeedSpeed">工艺算法给出的送丝速度</param>
        /// <returns>补偿后的送丝速度</returns>
        public double ComposeWireFeedSpeed(double algoFeedSpeed)
        {
            double result = algoFeedSpeed + _wireFeedCompensation;
            return result < 0.0 ? 0.0 : result;
        }

        /// <summary>光丝对中偏差驱动振镜中心微调（默认关闭）。</summary>
        public void ApplySpotAlignCompensation()
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null || !cfg.GalvoAutoCenterComp) return;
            MonitorResult snap = AlignDatum;
            if (snap == null) return;
            WireFeedConfig wireCfg = AlgorithmManager.Instance.WireFeed;
            double scale = wireCfg != null ? wireCfg.PixelMmScale : 0.05;
            double xMm = snap.OffsetX * scale * cfg.GalvoCenterCompGain;
            double yMm = snap.OffsetY * scale * cfg.GalvoCenterCompGain;
            LaserWeldHeadWorkflow.Instance.ApplyGalvoCenterOffset(xMm, yMm);
        }

        #endregion

        #region 查询与参数命令

        /// <summary>查询主设备当前运行态（原档 §10.4 序号 14，只读，不触发状</summary>

        /// <returns>当前 MainDeviceStatus</returns>
        public MainDeviceStatus GetMainDeviceStatus()
        {
            return Current;
        }

        /// <summary>查询指定模组当前焊接状态（原档 §10.4 序号 15，只读）。</summary>
        /// <param name="tag">模组标识（与各 Workflow 的 Tag 一致）</param>
        /// <returns>命中返回该模组焊接态；未命中返回 NoReset</returns>
        public SubDeviceWeldStatus GetSubDeviceStatus(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return SubDeviceWeldStatus.NoReset;
            if (tag == _weldHead.Tag) return _weldHead.WeldStatus;
            if (tag == _motion.Tag) return _motion.WeldStatus;
            if (tag == _lineLaser.Tag) return _lineLaser.WeldStatus;
            if (tag == _monitorCam.Tag) return _monitorCam.WeldStatus;
            if (tag == _weldParam.Tag) return _weldParam.WeldStatus;
            return SubDeviceWeldStatus.NoReset;
        }

        /// <summary>查询全部 5 个模组焊接状态（原档 §10.4 序号 16，只读）。</summary>

        /// <returns>模组标识到焊接态的映射</returns>
        public Dictionary<string, SubDeviceWeldStatus> GetAllSubDeviceStatus()
        {
            return new Dictionary<string, SubDeviceWeldStatus>
            {
                { _weldHead.Tag, _weldHead.WeldStatus },
                { _motion.Tag, _motion.WeldStatus },
                { _lineLaser.Tag, _lineLaser.WeldStatus },
                { _monitorCam.Tag, _monitorCam.WeldStatus },
                { _weldParam.Tag, _weldParam.WeldStatus }
            };
        }

        /// <summary>查询当前报警信息（原档 §10.4 序号 17）。</summary>
        /// <returns>最近一次报警信息；从未报警返回 HasAlarm=false 的实例</returns>
        public MachineAlarmInfo GetAlarmInfo()
        {
            return _lastAlarm ?? new MachineAlarmInfo { HasAlarm = false };
        }

        /// <summary>整机状态统一快照（只读聚合）。</summary>
        /// <returns>设备态/流程态/就绪/线程一次读出</returns>
        public MachineStatusSnapshot GetStatusSnapshot()
        {
            return new MachineStatusSnapshot
            {
                DeviceStatus = Current,
                LastDeviceStatus = Last,
                SubStatuses = GetAllSubDeviceStatus(),
                MotionReadiness = _motion.ReadinessStage,
                ThreadSnapshot = GetWorkThreadSnapshot(),
                IsWorking = IsWorking,
                IsReseting = IsReseting,
                IsInitializing = IsInitializing,
                IsSimulationEnabled = IsSimulationEnabled,
                IsWelding = IsWelding,
                Alarm = GetAlarmInfo(),
                Timestamp = DateTime.Now
            };
        }

        /// <summary>查询当前 IO 状态（原档 §10.4 序号 18，调试用）。</summary>
        /// <returns>所有已置位的虚拟 IO 名数组</returns>
        public string[] GetIOState()
        {
            return SnapshotVirtualIo();
        }

        /// <summary>读取当前焊接工艺参数（原档 §10.5 序号 20，只读）。</summary>
        /// <returns>最近一次工艺计算输出</returns>
        public WeldParamAdaptControl GetWeldParam()
        {
            return _weldParam.LastOutput;
        }

        /// <summary>设置焊接工艺参数（原档 §10.5 序号 19）。</summary>

        /// <param name="param">目标工艺参数</param>
        /// <returns>写入成功返回 true；运行中或参数为空返回 false</returns>
        public bool SetWeldParam(WeldParamAdaptControl param)
        {
            if (param == null) return false;
            if (Current == MainDeviceStatus.Running)
            {
                Log("运行中禁止修改焊接工艺参数", MessageLevel.Warning);
                return false;
            }
            if (Current != MainDeviceStatus.Stop)
            {
                Log("仅设备停止态允许修改焊接工艺参数 当前态 " + Current, MessageLevel.Warning);
                return false;
            }

            _pendingWeldParam = param;
            Log(string.Format("焊接工艺参数已更新 功率{0:F1} 送丝{1:F1} 摆幅{2:F2}",
                param.LaserPower, param.FeedSpeed, param.GalvoAmplitude));
            return true;
        }

        /// <summary>设置机器人坐标偏移与寄存位参数（原档 §10.5 序号 21）。</summary>

        /// <param name="offsetX">X 向坐标偏移</param>
        /// <param name="offsetY">Y 向坐标偏移</param>
        /// <param name="offsetZ">Z 向坐标偏移</param>
        /// <returns>写入成功返回 true；运行中返回 false</returns>
        public bool SetRobotCoord(double offsetX, double offsetY, double offsetZ)
        {
            if (Current == MainDeviceStatus.Running)
            {
                Log("运行中禁止修改机器人坐标偏移", MessageLevel.Warning);
                return false;
            }
            if (Current != MainDeviceStatus.Stop)
            {
                Log("仅设备停止态允许修改机器人坐标偏移 当前态 " + Current, MessageLevel.Warning);
                return false;
            }

            lock (_coordLock)
            {
                _coordOffsetX = offsetX;
                _coordOffsetY = offsetY;
                _coordOffsetZ = offsetZ;
            }
            Log(string.Format("机器人坐标偏移已更新 X{0:F3} Y{1:F3} Z{2:F3}", offsetX, offsetY, offsetZ));
            return true;
        }

        /// <summary>读取当前机器人坐标偏移。</summary>
        /// <param name="offsetX">X 向偏移</param>
        /// <param name="offsetY">Y 向偏移</param>
        /// <param name="offsetZ">Z 向偏移</param>
        public void GetRobotCoord(out double offsetX, out double offsetY, out double offsetZ)
        {
            lock (_coordLock)
            {
                offsetX = _coordOffsetX;
                offsetY = _coordOffsetY;
                offsetZ = _coordOffsetZ;
            }
        }

        #endregion

        /// <summary>按拍等待模组连接出结果。</summary>

        /// <param name="child">模组状态对象</param>
        /// <returns>出结果（成功 / 失败 / 超时）返回 true 可推进；仍在等待返回 false</returns>
        private bool WaitChildConnectedTick(DeviceStateBase child)
        {
            if (child.ConnectSettled) return true;
            return (DateTime.Now - _initStepTs).TotalMilliseconds >= CascadeConnectTimeoutMs;
        }

        /// <summary>推进到下一个初始化节点。</summary>
        private void AdvanceInitNode()
        {
            _initNodeIndex++;
            _initNodeStarted = false;
            _initStepTs = DateTime.Now;
        }

        /// <summary>模拟选相机并套用数据频率</summary>
        private void PrepareSimulationCamera()
        {
            // 模拟且线激光未连 → 虚拟；否则真实英莱
            if (ShouldUseVirtualCamera) LineLaserManager.Instance.SelectVirtual();
            else LineLaserManager.Instance.SelectIntelligentLaser();

            // 仅虚拟相机生效：套用线激光数据频率
            if (!LineLaserManager.Instance.IsVirtual) return;
            var virt = LineLaserManager.Instance.VirtualCamera;
            if (virt != null) virt.FrameRateHz = SimLaserFrequencyHz;
        }

        /// <summary>模拟态停驱真实硬件并记住原使能</summary>
        private void SuspendRealHardwareForSimulation()
        {
            // 仅首次挂起存快照，防二次点击覆盖
            if (!_simHardwareSuspended)
            {
                _simSavedWeldHeadEnabled = _weldHead.IsEnable;
                _simSavedMonitorCamEnabled = _monitorCam.IsEnable;
                _simHardwareSuspended = true;
            }
            _weldHead.IsEnable = false;
            _monitorCam.IsEnable = false;
        }

        /// <summary>清残留报警并回未复位态。</summary>
        private void EnsureRunningForSimulation()
        {
            if (Current == MainDeviceStatus.Alarm) DoClearAlarm();

            if (Current == MainDeviceStatus.EStop)
            {
                Log("急停态不可启动模拟 请先解除急停", MessageLevel.Warning);
                return;
            }

            // 停机后清到未复位态，复位交给 Home 命令触发
            if (Current == MainDeviceStatus.Running)
            {
                DoStopMachine();
            }
            if (Current != MainDeviceStatus.NoReset) ClearMachine();
        }

        /// <summary>复位在途返回 false。</summary>
        /// <returns>复位未在途返回 true</returns>
        private bool SimResetGateTick()
        {
            if (IsReseting || Current == MainDeviceStatus.Reseting) return false;
            return true;
        }

        /// <summary>记录模拟启动日志</summary>
        private void LogSimulationStarted()
        {
            GlobalCommData.ShowLog(_simTag, string.Format(
                "模拟测试开启 频率 {0}Hz 起点X {1:F1} 线激光 {2} 运动控制器 {3}",
                SimLaserFrequencyHz, _simStartX,
                ShouldUseVirtualCamera ? "虚拟" : "真实",
                _motion.State == SubDeviceState.Connected ? "已连" : "未连"), MessageLevel.Info);
        }

        /// <summary>释放托管资源（非 UI 类走 Dispose + DisposeComponents，不得重写 Dispose(bool)）。</summary>
        private void DisposeComponents()
        {
            if (_disposed) return;
            _disposed = true;
            _shuttingDown = true;

            Stopwatch watch = Stopwatch.StartNew();
            Log("程序退出 开始关闭（顺序：线程 → 设备 → 主站） 预算 30s");
            int devices = 0;

            // 1) 清残留信号 防扫描线程继续消费
            ClearAllVirtualIo();

            // 2) 模组线程：通知 + 等待
            if (!ShutdownBudgetExceeded(watch, "模组线程"))
            {
                MachineFlowWorkClose();
                JoinModuleWorkThreads();
            }

            // 3) 通讯线程：与模组标志分离，单独关闭
            if (!ShutdownBudgetExceeded(watch, "通讯线程"))
            {
                MachineCommandWorkClose();
            }

            // 4) 状态线程与动作线程：常驻扫描器仅在此释放
            if (!ShutdownBudgetExceeded(watch, "状态线程"))
            {
                DeviceStatusWork.Instance.MachineIOWorkClose();
            }

            // 5) 模拟线程：置标志并等待退出
            if (!ShutdownBudgetExceeded(watch, "模拟线程"))
            {
                SimTestWorkClose();
            }

            // 6) 线程全部退出后才逐台断设备，每台等确认
            if (!ShutdownBudgetExceeded(watch, "级联断开"))
            {
                try { devices = CascadeDisconnectWithWait(); }
                catch (Exception ex) { Log("级联断开异常 " + ex.Message, MessageLevel.Error); }
            }

            // 7) 关机器人通讯通道
            if (!ShutdownBudgetExceeded(watch, "机器人通道"))
            {
                CloseRobotChannel();
            }

            GlobalCommData.CommunicationCommandReceived -= OnCommRecived;
            LineLaserManager.Instance.JobChanged -= OnLineLaserJobChanged;
            GlobalCommData.mCommunicationManager.RobotDataReceived -= OnRobotDataReceived;

            GetWorkThreadExitSummary(out int exited, out int total);
            Log(string.Format("关闭完成 线程 {0}/{1} 设备 {2}/4 用时 {3}ms",
                exited, total, devices, watch.ElapsedMilliseconds),
                exited == total && devices == 4 ? MessageLevel.Info : MessageLevel.Warning);
        }

        /// <summary>关闭预算判定：超时记强制退出日志并返回 true。</summary>
        /// <param name="watch">关闭计时器</param>
        /// <param name="step">当前步骤名</param>
        /// <returns>已超预算返回 true</returns>
        private bool ShutdownBudgetExceeded(Stopwatch watch, string step)
        {
            if (watch.ElapsedMilliseconds < ShutdownBudgetMs) return false;
            Log(string.Format("强制退出 原因 关闭预算超时 步骤 {0} 时点 {1:yyyy-MM-dd HH:mm:ss}",
                step, DateTime.Now), MessageLevel.Error);
            return true;
        }

        /// <summary>关闭机器人通讯通道。</summary>
        private void CloseRobotChannel()
        {
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null) return;
            try
            {
                comm.DisconnectRobot();
                Log("机器人通讯通道已关闭");
            }
            catch (Exception ex) { Log("关闭机器人通讯通道异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>统计 11 条常驻线程的退出情况。</summary>

        /// <param name="exited">已确认退出的线程数</param>
        /// <param name="total">常驻线程总数（11）</param>
        private void GetWorkThreadExitSummary(out int exited, out int total)
        {
            string[] names =
            {
                "thLaserHeadWork", "thLineLaserWork", "thMonitorCamWork",
                "thMotionControlWork", "thWeldParamControlWork",
                "thHeartInspectWork", "thCommCommandWork",
                "thIOWork", "thEMGWork", "thLightStatusWork", "thAction"
            };
            int alive = 0;
            foreach (string name in names)
            {
                if (name == "thIOWork")
                {
                    if (DeviceStatusWork.Instance.IsIOWorking) alive++;
                    continue;
                }
                if (name == "thEMGWork")
                {
                    if (DeviceStatusWork.Instance.IsEMGWorking) alive++;
                    continue;
                }
                if (name == "thLightStatusWork")
                {
                    if (DeviceStatusWork.Instance.IsLightStatusWorking) alive++;
                    continue;
                }

                Thread thread = null;
                lock (_workThreadLock)
                {
                    if (_workThreads.TryGetValue(name, out WorkThreadEntry entry)) thread = entry.Thread;
                }
                if (thread != null && thread.IsAlive) alive++;
            }
            total = names.Length;
            exited = total - alive;
        }

        #endregion

        /// <summary>阶段二：TA 轴配置加载</summary>

        /// <returns>TA 配置加载成功返回 true</returns>
        private bool LoadTaAxisConfigForInit()
        {
            MontionManager mgr = MontionManager.Instance;
            if (mgr == null || !mgr.IsInitialized)
            {
                Log("阶段二 使能 TA 轴配置加载 跳过 原因 运动管理器未初始化", MessageLevel.Warning);
                return false;
            }

            bool ok = mgr.LoadTaAxisConfig();
            if (!ok)
            {
                Log("阶段二 使能 TA 轴配置加载 失败 原因 " + mgr.TaDegradedReason, MessageLevel.Error);
            }
            else
            {
                Log("阶段二 使能 TA 轴配置加载完成", MessageLevel.Debug);
            }
            return ok;
        }

        /// <summary>实例化所有模组工作线程（5 个）并 Start（§7.5）。</summary>
        public void MachineFlowWorkOpen()
        {
            if (_workStarted) return;

            // 上一批线程可能尚未退净，先等退出
            // 再复位标志，避免叠线程或新线程空转
            if (thLaserHeadWork != null || thLineLaserWork != null || thMonitorCamWork != null
                || thMotionControlWork != null || thWeldParamControlWork != null)
            {
                JoinModuleWorkThreads();
            }
            IsCloseWork = false;
            _workStarted = true;

            thLaserHeadWork = NewWorkThread(LaserHeadWork, "thLaserHeadWork");
            thLineLaserWork = NewWorkThread(LineLaserWork, "thLineLaserWork");
            thMonitorCamWork = NewWorkThread(MonitorCamWork, "thMonitorCamWork");
            thMotionControlWork = NewWorkThread(MotionControlWork, "thMotionControlWork");
            thWeldParamControlWork = NewWorkThread(WeldParamControlWork, "thWeldParamControlWork");

            Log("5 条模组工作线程已开启");
        }

        /// <summary>实例化通讯工作线程（2 个）并 Start（§7.5）。</summary>
        public void MachineCommandWorkOpen()
        {
            if (_commStarted) return;

            if (thHeartInspectWork != null || thCommCommandWork != null)
            {
                JoinCommWorkThreads();
            }
            _commCloseWork = false;
            _commStarted = true;

            thCommCommandWork = NewWorkThread(CommCommandWork, "thCommCommandWork");

            Log("机器人指令交互线程已开启 心跳待通讯连接后启动");
            Log("线程登记表 " + GetWorkThreadSnapshot(), MessageLevel.Debug);
        }

        /// <summary>连接成功后启动心跳线程。</summary>
        private void EnsureHeartbeatThread()
        {
            if (_commCloseWork) return;
            if (thHeartInspectWork != null && thHeartInspectWork.IsAlive) return;
            thHeartInspectWork = NewWorkThread(HeartInspectWork, "thHeartInspectWork");
            Log("机器人通讯已连接 心跳线程已启动");
        }

        /// <summary>关闭整机工作线程（范式 M3：只置标志位，不 Abort 线程）。</summary>
        public void MachineFlowWorkClose()
        {
            IsCloseWork = true;
            _workStarted = false;
            Log("已通知整机工作线程退出");
        }

        /// <summary>关闭通讯线程（只置通讯标志，不影响模组线程）。</summary>
        public void MachineCommandWorkClose()
        {
            _commCloseWork = true;
            _commStarted = false;
            JoinCommWorkThreads();
        }

        /// <summary>等待 5 条模组工作线程退出（显式管理，不 Abort）。</summary>
        private void JoinModuleWorkThreads()
        {
            int exited = 0;
            if (JoinWorkThread(thLaserHeadWork, "thLaserHeadWork")) exited++;
            if (JoinWorkThread(thLineLaserWork, "thLineLaserWork")) exited++;
            if (JoinWorkThread(thMonitorCamWork, "thMonitorCamWork")) exited++;
            if (JoinWorkThread(thMotionControlWork, "thMotionControlWork")) exited++;
            if (JoinWorkThread(thWeldParamControlWork, "thWeldParamControlWork")) exited++;
            Log(string.Format("模组工作线程已退出 {0}/5 条", exited),
                exited == 5 ? MessageLevel.Info : MessageLevel.Warning);
        }

        /// <summary>等待 2 条通讯工作线程退出（显式管理，不 Abort）。</summary>
        private void JoinCommWorkThreads()
        {
            int exited = 0;
            if (JoinWorkThread(thHeartInspectWork, "thHeartInspectWork")) exited++;
            if (JoinWorkThread(thCommCommandWork, "thCommCommandWork")) exited++;
            Log(string.Format("通讯线程已退出 {0}/2 条", exited),
                exited == 2 ? MessageLevel.Info : MessageLevel.Warning);
        }

        /// <summary>等待单条常驻线程退出。</summary>
        /// <param name="thread">目标线程，null 视为已退出</param>
        /// <param name="name">线程名（日志用）</param>
        /// <returns>已退出返回 true，超时返回 false</returns>
        private bool JoinWorkThread(Thread thread, string name)
        {
            if (thread == null || !thread.IsAlive)
            {
                SetWorkThreadState(name, true, false);
                return true;
            }

            int timeoutMs = GetWorkThreadTimeoutMs(name);
            if (thread.Join(timeoutMs))
            {
                SetWorkThreadState(name, true, false);
                return true;
            }

            SetWorkThreadState(name, false, true);
            Log(name + " 未在 " + timeoutMs + "ms 内退出", MessageLevel.Warning);
            return false;
        }

        /// <summary>读取登记的超时；未登记返回默认上限。</summary>
        /// <param name="name">线程名</param>
        /// <returns>等待上限（毫秒）</returns>
        private int GetWorkThreadTimeoutMs(string name)
        {
            lock (_workThreadLock)
            {
                if (!string.IsNullOrEmpty(name) && _workThreads.TryGetValue(name, out WorkThreadEntry entry))
                    return entry.TimeoutMs;
            }
            return WorkThreadJoinTimeoutMs;
        }

        /// <summary>回写登记状态（已退出 / 等待超时）。</summary>
        /// <param name="name">线程名</param>
        /// <param name="exited">是否已确认退出</param>
        /// <param name="timedOut">是否等待超时</param>
        private void SetWorkThreadState(string name, bool exited, bool timedOut)
        {
            lock (_workThreadLock)
            {
                if (string.IsNullOrEmpty(name) || !_workThreads.TryGetValue(name, out WorkThreadEntry entry)) return;
                if (exited) entry.Exited = true;
                if (timedOut) entry.JoinTimedOut = true;
            }
        }

        /// <summary>系统初始化命令（原档 §10.2 序号 1，映射 Init_IO）。</summary>

        /// <returns>IO 线程已拉起且 IO 置位成功返回 true</returns>
        public bool InitializeMachine()
        {
            // 受理标志先于 IO_INIT 置位：封堵 IOWork 首拍竞态（未连设备先报 Alarm）。
            // ⚠️ 用独立 _initAccepted 而非 _initializing：后者是 DoInitializeMachine 的重复受理守卫，
            //    提前置位会使初始化链被误判重复
            _initAccepted = true;
            DeviceStatusWork.Instance.MachineIOWorkOpen();

            SetVirtualIo(IoInit);
            Log("系统初始化命令已发出，等待监控线程执行（IO_INIT）");
            return true;
        }

        /// <summary>初始化未进入推进链即结束的收尾</summary>

        /// <param name="message">收尾日志</param>
        /// <param name="level">日志级别</param>
        private void EndInitWithoutChain(string message, MessageLevel level = MessageLevel.Info)
        {
            _initAccepted = false;
            // 与 FinishInitialization 的收尾对齐：初始化已出结果（拒绝/跳过），
            // 统一开启模组与指令线程
            MachineFlowWorkOpen();
            EnsureCommandWorkStarted();
            Log(message, level);
        }

        /// <summary>执行初始化链（由 thIOWork 扫描到 Init_IO 后调用，不对外直接调用）。</summary>

        /// <returns>受理返回 true；已在初始化中返回 false</returns>
        public bool DoInitializeMachine()
        {
            if (_shuttingDown)
            {
                _initAccepted = false;
                Log("正在关闭 拒绝初始化", MessageLevel.Warning);
                return false;
            }
            if (_initializing)
            {
                Log("模组初始化进行中 忽略重复 Init_IO", MessageLevel.Warning);
                return false;
            }
            // 禁止运行期重复初始化；仅未复位态允许重来并补连未连设备
            if (Current == MainDeviceStatus.Running)
            {
                EndInitWithoutChain("运行中禁止重复初始化", MessageLevel.Warning);
                return false;
            }
            if (Current == MainDeviceStatus.EStop || Current == MainDeviceStatus.Reseting)
            {
                EndInitWithoutChain("当前运行态禁止初始化 " + Current, MessageLevel.Warning);
                return false;
            }
            if (_initPlan == null) _initPlan = BuildInitPlan();
            if (AllInitTargetsConnected())
            {
                EndInitWithoutChain("初始化跳过 模组均已连接 无需补连");
                return true;
            }
            // 受理：_initAccepted 与 _initializing 无缝交接，IOWork 全程看到 IsInitializing=true
            _initAccepted = false;
            _initializing = true;
            _initNodeIndex = 0;
            _initNodeStarted = false;
            _initNodeSkip = false;
            _initStepTs = DateTime.Now;
            _robotCommUiWaitLogged = false;
            lock (_initOutcomes)
            {
                _initOutcomes.Clear();
                _lastInitOutcomes = new List<DeviceInitOutcomeItem>();
            }
            Log(string.Format("模组初始化开始 阶段一 连接 共 {0} 个模组", _initPlan.Count));
            Log("初始化顺序 " + BuildInitOrderText(), MessageLevel.Debug);
            return true;
        }

        #region 初始化层级驱动

        /// <summary>初始化层级节点表：顺序即执行顺序，父在前。</summary>
        private List<InitNode> BuildInitPlan()
        {
            return new List<InitNode>
            {
                new InitNode
                {
                    Key = InitNodeRobotComm, DisplayName = "机器人通讯",
                    RequiresUiReady = true, Mandatory = true, Start = () => StartRobotCommNode()
                },
                new InitNode
                {
                    Key = InitNodeMotion, DisplayName = "运动控制器",
                    Mandatory = true, Device = _motion, Start = () => StartMotionNode()
                },
                new InitNode
                {
                    Key = InitNodeWeldHead, DisplayName = "焊接头从站",
                    ParentKey = InitNodeMotion, Mandatory = true, Device = _weldHead, Start = () => StartWeldHeadNode(),
                    SkipReasonProvider = () => GetWeldHeadSkipReason(),
                    FailReasonProvider = () => IsWeldHeadConfiguredButOffline() ? "从站未连接" : string.Empty
                },
                new InitNode
                {
                    Key = InitNodeLineLaser, DisplayName = "线激光相机",
                    Mandatory = true, WaitSettle = true, Device = _lineLaser, Start = () => StartLineLaserNode()
                },
                new InitNode
                {
                    Key = InitNodeMonitorCam, DisplayName = "监控相机",
                    Mandatory = true, WaitSettle = true, Device = _monitorCam, Start = () => StartMonitorCamNode()
                }
            };
        }

        /// <summary>焊接头跳过原因：未配置 / 未启用 / 空串</summary>

        /// <returns>跳过原因；继续判定返回空串</returns>
        private static string GetWeldHeadSkipReason()
        {
            MontionManager mgr = MontionManager.Instance;
            if (mgr == null || mgr.MoveData == null || mgr.MoveData.MotionConfig == null) return "未配置";
            var cfg = mgr.MoveData.MotionConfig.WeldHeadSlave;
            if (cfg == null) return "未配置";
            return cfg.Enabled ? string.Empty : "未启用";
        }

        /// <summary>配置启用但主站扫描结果无该节点</summary>

        /// <returns>启用且未扫到返回 true</returns>
        private static bool IsWeldHeadConfiguredButOffline()
        {
            MontionManager mgr = MontionManager.Instance;
            if (mgr == null || mgr.MoveData == null || mgr.MoveData.MotionConfig == null) return false;
            var cfg = mgr.MoveData.MotionConfig.WeldHeadSlave;
            if (cfg == null || !cfg.Enabled) return false;
            return !mgr.IsSlaveDetected(cfg.NodeId);
        }

        /// <summary>由节点表生成初始化顺序文案。</summary>
        private string BuildInitOrderText()
        {
            List<string> parts = new List<string>();
            foreach (InitNode node in _initPlan)
            {
                if (string.IsNullOrEmpty(node.ParentKey))
                {
                    parts.Add(node.DisplayName);
                    continue;
                }
                string parentName = node.ParentKey;
                InitNode parent = _initPlan.Find(p => p.Key == node.ParentKey);
                if (parent != null && !string.IsNullOrEmpty(parent.DisplayName)) parentName = parent.DisplayName;
                parts.Add(string.Format("{0}（{1} 下属）", node.DisplayName, parentName));
            }
            return string.Join(" ", parts.ToArray());
        }

        /// <summary>机器人通讯节点开启监听。</summary>
        private bool StartRobotCommNode()
        {
            return InitializeRobotComm();
        }

        /// <summary>运动控制器节点：主站 + 从站注册 + CAMBOX 跟踪工作流。</summary>
        private bool StartMotionNode()
        {
            try { InitializeMotionControl(); }
            catch (Exception ex) { _initNodeReason = "异常 " + ex.Message; }
            bool ok = _motion.State == SubDeviceState.Connected;
            _initNodeReason = ok ? "控制器已连接" : _motion.ConnectResult;
            return ok;
        }

        /// <summary>焊接头节点：连接从站。</summary>
        private bool StartWeldHeadNode()
        {
            try
            {
                WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
                if (weldHead == null || !weldHead.IsInitialized)
                {
                    _initNodeReason = "从站未装载";
                    return false;
                }
                _weldHead.SetRunMode(WorkflowRunMode.Auto);
                _weldHead.ConnectOn();
                bool ok = _weldHead.State == SubDeviceState.Connected;
                _initNodeReason = ok ? "从站已握手" : _weldHead.ConnectResult;
                return ok;
            }
            catch (Exception ex)
            {
                _initNodeReason = "异常 " + ex.Message;
                return false;
            }
        }

        /// <summary>线激光相机节点：独立 TCP + SDK，受理后按拍等待结果。</summary>
        private bool StartLineLaserNode()
        {
            try
            {
                _lineLaser.SetRunMode(WorkflowRunMode.Auto);
                bool accepted = _lineLaser.ConnectOn();
                _initNodeReason = accepted ? "连接受理" : "连接进行中被拒绝";
                return accepted;
            }
            catch (Exception ex)
            {
                _initNodeReason = "异常 " + ex.Message;
                return false;
            }
        }

        /// <summary>监控相机节点：独立网络 + SDK，受理后按拍等待结果。</summary>
        private bool StartMonitorCamNode()
        {
            try
            {
                _monitorCam.SetRunMode(WorkflowRunMode.Auto);
                bool accepted = _monitorCam.ConnectOn();
                _initNodeReason = accepted ? "连接受理" : "连接进行中被拒绝";
                return accepted;
            }
            catch (Exception ex)
            {
                _initNodeReason = "异常 " + ex.Message;
                return false;
            }
        }

        /// <summary>记录单节点初始化结果并按三态打日志。</summary>
        /// <param name="node">节点定义</param>
        /// <param name="outcome">成功 / 失败 / 跳过</param>
        /// <param name="blockedBy">跳过时挡住它的父级名</param>
        /// <param name="reason">原因说明</param>
        private void RecordInitOutcome(InitNode node, DeviceInitOutcome outcome, string blockedBy, string reason)
        {
            DeviceInitOutcomeItem item = new DeviceInitOutcomeItem
            {
                Key = node.Key,
                DisplayName = node.DisplayName,
                Outcome = outcome,
                BlockedBy = blockedBy ?? string.Empty,
                Reason = reason ?? string.Empty
            };
            lock (_initOutcomes)
            {
                _initOutcomes[node.Key] = item;
                _lastInitOutcomes.Add(item);
            }
            int index = _initPlan == null ? 0 : _initPlan.IndexOf(node) + 1;
            int total = _initPlan == null ? 0 : _initPlan.Count;
            string text = outcome == DeviceInitOutcome.Success ? "连接成功"
                : (outcome == DeviceInitOutcome.Failed ? "连接失败，失败原因：" : "连接跳过，跳过原因：");
            MessageLevel level = outcome == DeviceInitOutcome.Success ? MessageLevel.Info
                : (outcome == DeviceInitOutcome.Failed ? MessageLevel.Error : MessageLevel.Warning);
            string reasonText = outcome == DeviceInitOutcome.Success || string.IsNullOrEmpty(item.Reason)
                ? string.Empty : item.Reason;
            Log(string.Format("[{0}/{1}] {2} {3}{4}", index, total, node.DisplayName, text, reasonText), level);
        }

        /// <summary>发布初始化三态汇总。</summary>
        private void RaiseInitSummary()
        {
            EventHandler<DeviceInitSummaryEventArgs> handler = InitializationSummaryReady;
            if (handler == null) return;
            List<DeviceInitOutcomeItem> items;
            lock (_initOutcomes)
            {
                items = new List<DeviceInitOutcomeItem>(_lastInitOutcomes);
            }
            handler(this, new DeviceInitSummaryEventArgs
            {
                SuccessCount = _initOkCount,
                FailedCount = _initFailCount,
                SkippedCount = _initSkipCount,
                Items = items
            });
        }

        /// <summary>初始化链收尾与判定。</summary>
        private void FinishInitialization()
        {
            _initializing = false;
            _initAccepted = false;
            // 初始化出结果后启动模组线程
            MachineFlowWorkOpen();
            bool allReady = EvaluateInitializationResult();
            bool enableOk = false;
            bool taConfigOk = false;
            if (!_initMotionReady)
            {
                // 连接未就绪：使能阶段只报一条跳过
                Log("阶段二 使能 跳过 原因 连接阶段运动控制器未就绪", MessageLevel.Warning);
            }
            else
            {
                Log("阶段二 使能 开始");
                taConfigOk = LoadTaAxisConfigForInit();
                enableOk = EnableAxesAfterInit();
            }
            bool initSucceeded = allReady && enableOk;
            // 使能结束即开闸，单轴成功不连带他轴
            MontionManager.Instance.MarkInitCompleted(initSucceeded);
            if (_initMotionReady)
            {
                if (!(taConfigOk && MontionManager.Instance.TaMotionReady))
                {
                    Log("阶段二 使能 TA 降级 " + MontionManager.Instance.TaDegradedReason,
                        MessageLevel.Warning);
                }
                // 初始化只连接使能，回零归复位入口
                _motion.RunInitAutoHomeIfEnabled();
                // 轴门禁结论在汇总前打印，保证汇总最后
                _motion.LogAxisGateReport();
            }
            RaiseInitSummary();
            // 事件先发、汇总后打，保证汇总最后
            InitializationCompleted?.Invoke(initSucceeded);
            // 接入焊接头后 LA 就位到默认焦平面（LA 默认工作位）
            // LA 回零指返回工作位
            // 非阻塞下发，失败仅记日志
            _weldHead.MoveToDefaultFocus();
            Log(initSucceeded
                    ? "初始化结束 已连接 已使能 未回零 无运动 待复位"
                    : "初始化结束 未全部就绪 已连接设备保留 未连设备可再次初始化补连",
                initSucceeded ? MessageLevel.Info : MessageLevel.Error);
            Log(string.Format("初始化汇总 成功 {0} 失败 {1} 跳过 {2}",
                _initOkCount, _initFailCount, _initSkipCount),
                initSucceeded ? MessageLevel.Info : MessageLevel.Error);

            // 初始化出结果后开启指令线程
            // 初始化完成后才开启，避免文件占用
            //    确保重连在初始化结束后进行
            EnsureCommandWorkStarted();
        }

        /// <summary>开启机器人指令交互线程</summary>
        private void EnsureCommandWorkStarted()
        {
            MachineCommandWorkOpen();
        }

        #endregion

        /// <summary>初始化节点驱动：按层级表每拍推进。</summary>
        public void InitializeMachineTick()
        {
            if (!_initializing) return;

            // 每拍问询：被抢占即中止，日志只记一次
            if (IsInitPreempted())
            {
                _initializing = false;
                _initAccepted = false;
                Log("初始化链被中止 运行态已变更为 " + Current, MessageLevel.Warning);
                // 中止路径同样开启工作线程
                MachineFlowWorkOpen();
                EnsureCommandWorkStarted();
                return;
            }

            if (_initPlan == null) _initPlan = BuildInitPlan();
            if (_initNodeIndex >= _initPlan.Count)
            {
                FinishInitialization();
                return;
            }

            InitNode node = _initPlan[_initNodeIndex];

            // 父级门禁：父级未成功则子级不连接
            if (!string.IsNullOrEmpty(node.ParentKey))
            {
                DeviceInitOutcomeItem parent = null;
                bool parentOk;
                lock (_initOutcomes)
                {
                    parentOk = _initOutcomes.TryGetValue(node.ParentKey, out parent)
                        && parent.Outcome == DeviceInitOutcome.Success;
                }
                if (!parentOk)
                {
                    string parentName = parent == null || string.IsNullOrEmpty(parent.DisplayName)
                        ? node.ParentKey : parent.DisplayName;
                    RecordInitOutcome(node, DeviceInitOutcome.Skipped, parentName,
                        parentName + " 未就绪");
                    AdvanceInitNode();
                    return;
                }
            }

            // 配置未启用：规划阶段即跳过，不发起连接
            if (node.SkipReasonProvider != null)
            {
                string skipReason = node.SkipReasonProvider();
                if (!string.IsNullOrEmpty(skipReason))
                {
                    RecordInitOutcome(node, DeviceInitOutcome.Skipped, null, skipReason);
                    AdvanceInitNode();
                    return;
                }
            }

            // 配置启用但总线无该从站：计真失败，不发起连接
            if (node.FailReasonProvider != null)
            {
                string failReason = node.FailReasonProvider();
                if (!string.IsNullOrEmpty(failReason))
                {
                    RecordInitOutcome(node, DeviceInitOutcome.Failed, null, failReason);
                    AdvanceInitNode();
                    return;
                }
            }

            // 界面就绪门控（机器人通讯）：按拍重试，不 Sleep 等待 UI
            if (node.RequiresUiReady && !GlobalCommData.IsUiReady)
            {
                if (!_robotCommUiWaitLogged)
                {
                    _robotCommUiWaitLogged = true;
                    Log("界面尚未就绪 机器人通讯暂缓开启 等待界面完成后继续", MessageLevel.Warning);
                }
                return;
            }

            // 幂等：已连接直接判成功
            if (node.Device != null && node.Device.State == SubDeviceState.Connected)
            {
                RecordInitOutcome(node, DeviceInitOutcome.Success, null, "已连接");
                AdvanceInitNode();
                return;
            }

            if (!_initNodeStarted)
            {
                _initNodeStarted = true;
                _initStepTs = DateTime.Now;
                _initNodeReason = string.Empty;
                _initNodeSkip = false;
                Log(string.Format("[{0}/{1}] {2} 连接 开始", _initNodeIndex + 1, _initPlan.Count, node.DisplayName));
                bool accepted;
                try { accepted = node.Start == null || node.Start(); }
                catch (Exception ex)
                {
                    _initNodeReason = "异常 " + ex.Message;
                    accepted = false;
                }

                // 同步节点直接落结果；受理式节点等下一拍开始轮询
                if (_initNodeSkip || !node.WaitSettle || !accepted)
                {
                    RecordInitOutcome(node,
                        _initNodeSkip ? DeviceInitOutcome.Skipped
                            : (accepted ? DeviceInitOutcome.Success : DeviceInitOutcome.Failed),
                        null, _initNodeReason);
                    AdvanceInitNode();
                }
                return;
            }

            // 受理式连接：按拍轮询结果（不 Sleep）
            if (node.Device == null)
            {
                RecordInitOutcome(node, DeviceInitOutcome.Success, null, _initNodeReason);
                AdvanceInitNode();
                return;
            }
            if (!WaitChildConnectedTick(node.Device)) return;
            bool connected = node.Device.State == SubDeviceState.Connected;
            RecordInitOutcome(node, connected ? DeviceInitOutcome.Success : DeviceInitOutcome.Failed, null,
                connected ? "连接成功"
                    : (node.Device.ConnectSettled ? node.Device.ConnectResult : "连接等待超时"));
            AdvanceInitNode();
        }

        /// <summary>执行所有模组 ClearStatus 并强制设备状态 NoReset（§7.5）。</summary>
        public void ClearMachine()
        {
            _subWorkPending = false;
            CascadeClearStatus();
            IsWorking = false;
            IsAlarmstop = false;
            SetStatus(MainDeviceStatus.NoReset, "清状态并强制未复位");
            Log("整机清状态完成（强制未复位）");
        }

        /// <summary>设备复位命令（映射 Reset_IO）。</summary>

        /// <returns>受理返回 true；运行中或复位中返回 false</returns>
        public bool ResetMachine()
        {
            if (IsReseting) return false;
            if (Current == MainDeviceStatus.Running)
            {
                Log("运行中不可复位", MessageLevel.Warning);
                return false;
            }

            SetVirtualIo(IoReset);
            Log("设备复位命令已发出，等待监控线程执行（IO_RESET）");
            return true;
        }

        /// <summary>执行复位链（由 thIOWork 扫描到 Reset_IO 后调用）。</summary>

        /// <returns>受理返回 true；急停态或机器人在位检测不通过返回 false</returns>
        public bool DoResetMachine()
        {
            if (Current == MainDeviceStatus.EStop)
            {
                Log("急停态禁止复位 请先解除急停", MessageLevel.Warning);
                return false;
            }

            // 运行中禁止复位：ClearMachine 的 Running→NoReset 迁移会被状态机拒绝，
            // 造成「子流程已清、整机态未变」的不一致。须先停机再复位。
            if (Current == MainDeviceStatus.Running)
            {
                Log("运行中不可复位 请先停止设备", MessageLevel.Warning);
                return false;
            }

            if (!IsRobotReachable() && !IsSimulationEnabled)
            {
                SetStatus(MainDeviceStatus.Alarm, "机器人在位检测失败 复位阻断");
                Log("机器人在位检测失败 复位阻断", MessageLevel.Error);
                return false;
            }

            IsReseting = true;
            new Task(() =>
            {
                try
                {
                    ClearMachine();
                    if (IsActionAborted(MainDeviceStatus.NoReset, "复位链")) return;

                    if (!MachineStatusTrans(MainDeviceStatus.Reseting))
                    {
                        Log("复位链路非法 复位中止 当前态 " + Current, MessageLevel.Warning);
                        return;
                    }
                    SetStatus(MainDeviceStatus.Reseting, "流程复位");

                    bool accepted = CascadeResetProcess();
                    if (IsActionAborted(MainDeviceStatus.Reseting, "复位链")) return;

                    SetStatus(accepted ? MainDeviceStatus.Stop : MainDeviceStatus.Alarm,
                        accepted ? "流程复位完成" : "子流程复位失败");
                    // 复位阶段应答：完成即置位，机器人据此给下一步指令
                    if (accepted) AckResetCompleted();
                }
                catch (Exception ex)
                {
                    Log("复位线程异常 " + ex.Message, MessageLevel.Error);
                }
                finally
                {
                    IsReseting = false;
                }
            }).Start();
            return true;
        }

        /// <summary>设备启动命令（映射 Start_IO）。</summary>

        /// <returns>受理返回 true；当前态非 Stop 返回 false</returns>
        public bool StartMachine()
        {
            if (Current == MainDeviceStatus.Running) return true;

            if (!MachineStatusTrans(MainDeviceStatus.Running))
            {
                Log("设备当前状态无法启动 当前态 " + Current, MessageLevel.Warning);
                return false;
            }

            SetVirtualIo(IoStart);
            Log("设备启动命令已发出，等待监控线程执行（IO_START）");
            return true;
        }

        /// <summary>执行启动链（thIOWork 扫描到 Start_IO 后调用）。</summary>

        /// <returns>状态切换成功返回 true</returns>
        public bool DoStartMachine()
        {
            if (_shuttingDown)
            {
                Log("正在关闭 拒绝启动", MessageLevel.Warning);
                return false;
            }
            if (Current == MainDeviceStatus.Running) return true;

            if (!MachineStatusTrans(MainDeviceStatus.Running))
            {
                Log("设备当前状态无法启动 当前态 " + Current, MessageLevel.Warning);
                return false;
            }

            SetStatus(MainDeviceStatus.Running, "机器启动");
            if (IsActionAborted(MainDeviceStatus.Running, "启动链")) return false;

            IsWorking = true;
            _everWorking = false;

            MachineFlowWorkOpen();
            MachineCommandWorkOpen();
            DeviceStatusWork.Instance.MachineIOWorkOpen();

            // 启动运控步机：使能 → 回归标定位 → 焊前寻位 → 就绪
            // 主控统一启动运控步机
            try
            {
                if (!_motion.Start())
                {
                    Log("运控步机启动失败 焊前寻位不可用", MessageLevel.Warning);
                }
            }
            catch (Exception ex) { Log("运控步机启动异常 " + ex.Message, MessageLevel.Warning); }

            Log("主设备运行管控启动，常驻线程已就绪");
            return true;
        }

        /// <summary>设备停止命令（映射 Stop_IO）。</summary>
        public void StopMachine()
        {
            SetVirtualIo(IoStop);
            Log("设备停止命令已发出，等待监控线程执行（IO_STOP）");
        }

        /// <summary>执行停止链（thIOWork 扫描到 Stop_IO 后调用）。</summary>
        public void DoStopMachine()
        {
            IsWorking = false;
            _subWorkPending = false;
            MachineFlowWorkClose();
            // 先等模组线程退净，再断设备
            JoinModuleWorkThreads();
            CascadeClearStatus();
            CascadeDisconnect();
            if (Current == MainDeviceStatus.Running)
                SetStatus(MainDeviceStatus.Stop, "机器停止");
            Log("主设备运行管控停止");
        }

        /// <summary>消除报警命令（映射 ClearAlarm_IO）。</summary>

        /// <returns>受理返回 true；当前非报警态返回 false</returns>
        public bool ClearAlarm()
        {
            if (Current == MainDeviceStatus.EStop) return false;
            if (Current != MainDeviceStatus.Alarm) return false;

            SetVirtualIo(IoClearAlarm);
            Log("消除报警命令已发出，等待监控线程执行（IO_CLEAR_ALARM）");
            return true;
        }

        /// <summary>执行消除报警（thIOWork 扫描到 ClearAlarm_IO 后调用）。</summary>

        /// <returns>已清除返回 true；当前非报警态返回 false</returns>
        public bool DoClearAlarm()
        {
            if (Current != MainDeviceStatus.Alarm) return false;

            IsAlarmstop = false;
            // 停发 TX_NG 撤机器人侧报警标志
            ClearRobotCommand();
            SetStatus(MainDeviceStatus.NoReset, "报警已清除 待复位");
            return true;
        }

        /// <summary>执行运控总线故障报警（thIOWork 上升沿调用）。</summary>
        public void DoMotionBusFault()
        {
            // 焊接中总线故障：先通知机器人停止动作（TX_NG）
            if (IsWelding) SendRobotCommand(CommandCore.TxNG);
            SetStatus(MainDeviceStatus.Alarm, "运控总线故障");
        }

        /// <summary>急停命令（无虚拟 IO，直接同步执行）。</summary>

        /// <param name="reason">急停原因</param>
        public void EstopMachine(string reason)
        {
            _emgReason = reason;
            DoEstopMachine();
        }

        /// <summary>执行急停（由 EstopMachine 直通调用，触发源为 EMGWork 物理信号边沿 / 机</summary>
        public void DoEstopMachine()
        {
            string reason = string.IsNullOrEmpty(_emgReason) ? "急停信号触发" : _emgReason;

            // ① 先停运动：机器人中止 + 运控轴急停
            EmergencyStop();

            // ② 再置急停态，UI 红色告警
            IsAlarmstop = true;
            IsWorking = false;
            _subWorkPending = false;
            SetStatus(MainDeviceStatus.EStop, reason);

            // ③ 停业务（模组）线程，短超时；通讯与状态线程保持
            MachineFlowWorkClose();
            JoinModuleWorkThreads();

            // ④ 置失能：轴 ServoOff，连接与主站保持
            try
            {
                Motion?.ServoOffAll();
            }
            catch (Exception ex) { Log("急停置失能异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>清除急停状态，设备进入未复位（§7.5）。</summary>
        public void EstopCancel()
        {
            if (Current != MainDeviceStatus.EStop) return;
            IsAlarmstop = false;
            SetStatus(MainDeviceStatus.NoReset, "急停解除 待复位");
        }

        /// <summary>设备安全：急停（下发机器人中止指令 + 运控轴急停）。</summary>
        public void EmergencyStop()
        {
            SendRobotCommand(CommandCore.TxAbort);
            try
            {
                _motion.EmergencyStop();
            }
            catch (Exception ex)
            {
                Log("运控急停异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>安全互锁报警上报（由 DeviceStatusWork 调用）：置报警态。</summary>
        /// <param name="reason">报警原因</param>
        public void ReportAlarm(string reason)
        {
            IsAlarmstop = true;
            _lastAlarm = new MachineAlarmInfo
            {
                HasAlarm = true,
                ErrorCode = "DEVICE_ALARM",
                Description = reason,
                OccurTime = DateTime.Now,
                StatusAtOccur = Current
            };
            // 焊接中异常：先通知机器人停止动作（TX_NG），再切报警态
            bool wasWelding = IsWelding;
            if (wasWelding) SendRobotCommand(CommandCore.TxNG);
            SetStatus(MainDeviceStatus.Alarm, reason);
            // 报警即停：不等宽限期，五路流程同拍收束，
            // 自适应各通道随各自 WeldStatus 停止输出。
            if (wasWelding) CascadeWeldStatus(SubDeviceWeldStatus.ErrorAborted, "报警停焊接管流程");
        }

        /// <summary>安全互锁恢复上报（由 DeviceStatusWork 调用）：转未复位。</summary>
        public void ReportAlarmCleared()
        {
            if (Current == MainDeviceStatus.Alarm)
            {
                _lastAlarm = null;
                // 停发 TX_NG：与清警同一链，载荷保持冻结值
                ClearRobotCommand();
                SetStatus(MainDeviceStatus.NoReset, "安全互锁恢复 待复位");
            }
        }

        // ---- 模拟测试（主链按设备连接态自动判断） ----

        /// <summary>模拟会话变量复位：启停两端都调，保证任意次点击都从零开始。</summary>
        /// <param name="reason">复位原因（仅日志）</param>
        /// <param name="restartFromHome">true = 调度指向 Home 起步步；false = 停在无效步，防停止中的线程再注入复位命令</param>
        private void ResetSimulationSessionState(string reason, bool restartFromHome)
        {
            _resetAckPending = false;
            _preWorkAckPending = false;
            _weldStartAckPending = false;
            _subWorkPending = false;
            _subWorkPendingLogTs = DateTime.MinValue;

            _simSchedStep = restartFromHome ? 5 : 0;
            _simInjectLastTime = DateTime.MinValue;
            _simAckWaitStart = DateTime.MinValue;

            lock (_robotXLock)
            {
                _robotXSimEnabled = false;
                _simRobotXMoving = false;
                _simPreWorkArrived = false;
                _simRobotX = 0.0;
                _simStartX = 0.0;
                _simStartTime = DateTime.Now;
                _simPreWorkTargetX = SimPreWorkStartXmm;
            }

            lock (_weldMonLock)
            {
                _hasRobotPreWeldPose = false;
                _robotPreWeldPoseX = 0.0;
            }

            GlobalCommData.ShowLog(_simTag, "模拟会话变量复位 原因 " + reason, MessageLevel.Info);
        }

        /// <summary>开启模拟测试。</summary>
        public void StartSimulation()
        {
            if (_simulationRunning)
            {
                GlobalCommData.ShowLog(_simTag, "模拟测试已在运行 忽略重复开启", MessageLevel.Warning);
                return;
            }
            _simulationRunning = true;

            _motion.ClearSeamAlarm();
            if (_motion.WeldStatus == SubDeviceWeldStatus.ErrorAborted) _motion.ResetProcess();

            PrepareSimulationCamera();

            PrepareSimulationMonitorCam();

            // 复位早于线程拉起，避免残留步号影响首拍
            ResetSimulationSessionState("模拟开启", true);
            // 开启即清跟踪与缺值，防上一轮残留跨会话
            try { WeldParamManager.Instance.ResetSeamTracking("模拟会话开启复位"); }
            catch (Exception ex) { Log("模拟开启复位焊缝跟踪异常 " + ex.Message, MessageLevel.Warning); }
            SuspendRealHardwareForSimulation();
            EnsureRunningForSimulation();
            SimTestWorkOpen();

            SaveSimulation();
            LogSimulationStarted();
        }

        /// <summary>关闭模拟测试：复位运行标志、停止共享模拟 robotX 时钟</summary>

        /// <param name="reason">停止原因（人话描述）</param>
        /// <param name="hasError">是否因错误/报警停止</param>
        /// <param name="isAuto">是否为自动停止</param>
        public void DisableSimulation(string reason = "用户手动停止", bool hasError = false, bool isAuto = false)
        {
            if (!_simulationRunning) return;
            _simulationRunning = false;
            ResetSimulationSessionState("模拟关闭", false);

            try { _lineLaser.SimReset(); }
            catch (Exception ex) { Log("模拟复位线激光异常 " + ex.Message, MessageLevel.Error); }

            try { WeldParamManager.Instance.ResetSeamTracking("模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位焊缝跟踪异常 " + ex.Message, MessageLevel.Error); }

            try { _weldParam.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位工艺参数控制异常 " + ex.Message, MessageLevel.Error); }

            SimMonitorCamStopWeld();

            try { _monitorCam.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位监控相机异常 " + ex.Message, MessageLevel.Error); }

            try
            {
                _motion.Stop();
                _motion.ClearSeamAlarm();
                _motion.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位");
            }
            catch (Exception ex) { Log("模拟复位运控异常 " + ex.Message, MessageLevel.Error); }

            try { _weldHead.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位焊接头异常 " + ex.Message, MessageLevel.Error); }

            _weldHead.IsEnable = _simSavedWeldHeadEnabled;
            _monitorCam.IsEnable = _simSavedMonitorCamEnabled;
            _simHardwareSuspended = false;
            if (Current == MainDeviceStatus.Running)
                SetStatus(MainDeviceStatus.Stop, "模拟测试关闭 退出运行");
            StopRobotXSimulation();
            SimTestWorkClose();
            // 归档移后台，避免冻结 UI
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { EndProductSessionSafely("pass", string.Empty); }
                catch (Exception ex) { Log("模拟归档后台落盘异常 " + ex.Message, MessageLevel.Error); }
            });
            try
            {
                SimulationStopped?.Invoke(this, new SimulationStoppedEventArgs(reason, hasError, isAuto));
            }
            catch (Exception ex) { Log("触发模拟停止事件异常 " + ex.Message, MessageLevel.Warning); }

            GlobalCommData.ShowLog(_simTag, string.Format("模拟测试关闭 原因 {0}", reason), MessageLevel.Info);
        }

        /// <summary>启动模拟位姿时钟：只喂位姿，X 暂不推进。</summary>
        public void StartRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _simStartX = 0.0;
                _simSpeed = SimDefaultSpeedMmPerS;
                _simWeldLengthMm = _simRand.Next(1500, 2001);
                _simRobotX = _simStartX;
                _simStartTime = DateTime.Now;
                _robotXSimEnabled = true;
                _simPreWorkTargetX = SimPreWorkStartXmm;
                _simPreWorkArrived = false;
                _simRobotXMoving = false;
            }
            // 复位跟踪：防上会话行程末端被判折返
            WeldParamManager.Instance.ResetTrackingForWeldStart("模拟起点重置");
            WeldParamManager.Instance.UpdateRobotPose(_simStartX, 0.0, 0.0);
            GlobalCommData.ShowLog(_simTag,
                string.Format("模拟机器人复位完成 停在 X {0:F1}mm 待焊前准备下发后移向寻位起点 X {1:F1}mm",
                    _simStartX, _simPreWorkTargetX),
                MessageLevel.Info);
        }

        /// <summary>模拟机器人开始寻位段移动：自当前 X 移向配置的寻位起点 X，到达即停。</summary>
        public void StartRobotXSeeking()
        {
            lock (_robotXLock)
            {
                if (!_robotXSimEnabled) return;
                if (_simPreWorkArrived) return;
                if (_simPreWorkTargetX <= 0.0) return;
                _simStartX = _simRobotX;
                _simStartTime = DateTime.Now;
                _simRobotXMoving = true;
            }
            GlobalCommData.ShowLog(_simTag,
                string.Format("模拟机器人自 X {0:F1}mm 移向焊接前寻位起点 X {1:F1}mm 边走边建图寻位",
                    _simStartX, _simPreWorkTargetX),
                MessageLevel.Info);
        }

        /// <summary>模拟机器人沿焊缝继续前进（等效收到主控 PreWork 完成反馈后起步）。</summary>
        public void StartRobotXMoving()
        {
            lock (_robotXLock)
            {
                if (!_robotXSimEnabled) return;
                if (_simPreWorkArrived)
                {
                    // 寻位段已到达：X 从当前点继续沿焊缝前进，计时基准重置
                    _simStartX = _simRobotX;
                    _simStartTime = DateTime.Now;
                    _simRobotXMoving = true;
                }
            }
            GlobalCommData.ShowLog(_simTag,
                string.Format("模拟机器人沿焊缝继续前进 当前 X {0:F1}mm", _simRobotX), MessageLevel.Info);
        }

        /// <summary>停止共享模拟 robotX 时钟</summary>
        public void StopRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _robotXSimEnabled = false;
            }
        }

        /// <summary>复位完成后启动模拟参与子流程（线激光 + 运控）。</summary>
        private void StartSimulationSubflowsAfterReset()
        {
            try { StartSimulationLineLaser(); }
            catch (Exception ex) { Log("模拟复位后线激光启动异常 " + ex.Message, MessageLevel.Error); }
            // 运控步机由 DoStartMachine 统一启动（真实与模拟同一条路径），
            // 此处不再重复调 Start()——Start 非幂等，重复调会重置步机到 StepServoOn
        }

        /// <summary>启动模拟用线激光流程。</summary>
        public void StartSimulationLineLaser()
        {
            if (_lineLaser.State != SubDeviceState.Connected)
            {
                _lineLaser.StateSwitched += OnSimLineLaserStateChanged;
                _lineLaser.ConnectOn();
            }
            else if (_lineLaser.WeldStatus == SubDeviceWeldStatus.Standby)
            {
                _lineLaser.Start();
            }
        }

        /// <summary>停止模拟用线激光流程（级联模组停止）。</summary>
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

        /// <summary>获取当前有效机器人 X（模拟开时为模拟值）。</summary>

        /// <returns>当前有效机器人 X 坐标</returns>
        public double GetEffectiveRobotX()
        {
            bool sim;
            lock (_robotXLock) { sim = _robotXSimEnabled; }
            return sim ? WeldParamManager.Instance.CurrentRobotX : 0.0;
        }

        /// <summary>从配置文件加载模拟参数（线激光数据频率、焊接前寻位起点 X）。</summary>
        public void LoadSimulation()
        {
            try
            {
                SimLaserFrequencyHz = _simConfig.LoadLaserFrequencyHz(SimLaserFrequencyHz);
                SimPreWorkStartXmm = _simConfig.LoadPreWorkStartXmm(SimPreWorkStartXmm);
            }
            catch
            {
                // 加载异常时保留当前值
            }
        }

        /// <summary>保存模拟参数（线激光数据频率）到配置文件。</summary>
        public void SaveSimulation()
        {
            try
            {
                _simConfig.SaveLaserFrequencyHz(SimLaserFrequencyHz);
                _simConfig.SavePreWorkStartXmm(SimPreWorkStartXmm);
            }
            catch
            {
                // 保存失败不影响运行时
            }
        }

        /// <summary>释放：停机、关模拟线程并退订通讯事件。</summary>
        public void Dispose()
        {
            DisposeComponents();
            GC.SuppressFinalize(this);
        }

        /// <summary>线程登记项。含名称/句柄/超时/等待结果。</summary>
        private sealed class WorkThreadEntry
        {
            /// <summary>线程名（与 Thread.Name 一致，登记表主键）</summary>
            public string Name { get; set; }

            /// <summary>线程句柄</summary>
            public Thread Thread { get; set; }

            /// <summary>退出等待上限（毫秒）</summary>
            public int TimeoutMs { get; set; }

            /// <summary>登记时刻</summary>
            public DateTime RegisteredAt { get; set; }

            /// <summary>是否已确认退出</summary>
            public bool Exited { get; set; }

            /// <summary>是否出现过等待超时（句柄未退净）</summary>
            public bool JoinTimedOut { get; set; }
        }
    }
}


