﻿using System;
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
    /// <remarks>不持有状态，只一次读出。</remarks>
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
    /// <remarks>不继承流程基类（原档 R013），状态链由 MainDeviceStatus 表达；7 条常驻线程驱动 5 个子流程，协议见 CommandCore。</remarks>
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

        private volatile bool _isAlarmstop;
        private volatile bool _initializing;
        // 初始化命令已受理（InitializeMachine 置位）但链未必已开始的窗口标志。
        // 用于 IOWork 在 IO_INIT 发出瞬间即旁路互锁/状态变更，消除首拍竞态；
        // 与 _initializing（DoInitializeMachine 的重复受理守卫）分离，避免提前置位导致链被判重复而拒绝。
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
        /// <remarks>机器人按契约保持 RxCmd ≥3 个 IPO 周期，逐包入队会重复执行。</remarks>
        private volatile int _lastRxCmdCode;

        private Thread thLaserHeadWork;
        private Thread thLineLaserWork;
        private Thread thMonitorCamWork;
        private Thread thMotionControlWork;
        private Thread thWeldParamControlWork;
        private Thread thHeartInspectWork;
        private Thread thCommCommandWork;

        /// <summary>线程登记表：名称/句柄/超时/状态。关闭与诊断共用一份清单。</summary>
        /// <remarks>阶段 0 只登记不改变线程启停行为；外部线程（如 thAction）经 <see cref="RegisterWorkThread"/> 纳入。</remarks>
        private readonly Dictionary<string, WorkThreadEntry> _workThreads =
            new Dictionary<string, WorkThreadEntry>();
        private readonly object _workThreadLock = new object();

        /// <summary>机器人指令队列（通讯回调只入队，指令线程消费）。</summary>
        private readonly Queue<string> _commCommandQueue = new Queue<string>();
        private readonly object _commLock = new object();

        // ---- 虚拟 IO 层（三种触发源统一收敛）----

        /// <summary>虚拟 IO 置位表：硬件/机器人/UI 三源统一置位。</summary>
        /// <remarks>由 thIOWork（Running+Alarm）与 thEMGWork（急停）轮询消费。</remarks>
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

        // 模拟是否运行中（单一开关，取代原 SimTestMode A/B/C）
        private volatile bool _simulationRunning;

        // 当前产品码（生产记录 SN；由机器人传输指令下发，空缺时记录侧以 TEST 占位）
        private string _currentProductSn = string.Empty;

        // 共享模拟位姿：线程单入口喂入，流程自 Manager 读
        private bool _robotXSimEnabled;
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
        /// <remarks>父级关系由对象归属决定，不另建配置清单。</remarks>
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

            /// <summary>发起连接；同步节点返回真实结果，异步节点返回是否受理。</summary>
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

        /// <summary>线激光收工行程（毫米）= 焊缝终止行程 − 前置距离；行程到此即让线激光停止工作。</summary>
        private double _laserStopStrokeMm;

        /// <summary>线激光是否已停（一次性，防重复下发）。</summary>
        private bool _laserMeasureFinished;

        private bool _weldCompleteByMonitor;

        // 机器人焊接停止位，行程收口以此为准
        private double _robotStopPoseX;
        private bool _hasRobotStopPose;

        // 模拟参数（UI 设置，持久化）——线激光数据频率（30/60/80/120Hz）
        private int _simLaserFrequencyHz = 30;

        // 模拟测试线程（随模拟启停，不在 §7.2 的 7 条常驻线程内）
        private Thread thSimTestWork;
        private volatile bool _simWorkClosing;

        // 模拟参数配置：文件路径与 INI 读写由 SimulationConfig 独占
        private readonly SimulationConfig _simConfig = new SimulationConfig(
            Path.Combine(Application.StartupPath, "Config", "MainDeviceControl", "Simulation_INI", "SimulationController.ini"));

        /// <summary>模拟停驱的模组使能态，关闭模拟时恢复。</summary>
        private bool _simSavedWeldHeadEnabled;
        private bool _simSavedMonitorCamEnabled;

        #endregion

        #region 公共变量

        /// <summary>单例实例。</summary>
        public static DeviceControlWork Instance { get { return _lazyInstance.Value; } }

        /// <summary>对中偏差数据中枢（原档 §7.4.5 只读）。</summary>
        /// <remarks>OffsetX/Y 供控制器驱动振镜，WireStickoutDeviation 供机器人调送丝；产出方持有，消费方取用。</remarks>
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

        /// <summary>初始化链结束事件（成功或失败均触发；参数=是否全部模组就绪）。UI 据此关闭初始化等待提示。</summary>
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
        /// <remarks>2026-11-17 新增：<b>UI 手动操作的唯一禁入判据</b>——通过界面发起的动作一律是手动模式，
        /// 自动焊接流程一旦启动到结束，这些动作必须拒绝（与生产流程互斥）。
        /// ⛔ 不复用 <see cref="IsWelding"/>：后者是「整机 Running 且已投入工作」，覆盖整段开机期，
        /// 过宽会把正常调试（机器已启动但没在焊）也一并禁掉。
        /// 焊接主流程 = 线激光流程（焊缝跟踪 + 焊接），其焊接过程态是权威来源。</remarks>
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
        /// <remarks>todo 产品码待协议细化后在指令分发处写入。</remarks>
        public string CurrentProductSn
        {
            get { return _currentProductSn; }
            set { _currentProductSn = value ?? string.Empty; }
        }

        /// <summary>是否使用虚拟调试相机（模拟且未连接时）。</summary>
        /// <remarks>取代 A/B/C 模式：已连接复用真实相机，否则用虚拟。
        /// 模拟结束后 State 仍为 Connected，须按相机种类判断。
        /// 否则第二轮误选真实相机，无数据致工作步卡死。</remarks>
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
        /// <remarks>生产恒写轴；模拟仅运控已连接才写轴，否则只记轨迹。</remarks>
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
        /// <remarks>非法迁移只记日志，保证急停/复位链路不被旁路。</remarks>
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
        /// <remarks>非法迁移拒绝并记 Warning，不触发事件。</remarks>
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
        /// <remarks>子流程 WeldStatus 写入口是 protected，整机层统一委托各子流程的 WeldStatusTrans()。</remarks>
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
        /// <remarks>只维护登记表，不启动、不停止线程；供本类与 DeviceStatusWork 的临时动作线程共用。</remarks>
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
        /// <remarks>只读快照。供关闭汇总与现场核对。</remarks>
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
        /// <remarks>非运行态慢节拍空转，运行态每拍驱动一步；单拍异常只记日志不退出。
        /// 仅从 Running 跌出才记停止驱动，启动态不记。</remarks>
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
        /// <remarks>只记日志不改状态，状态由 IO 动作链专门管理。
        /// 启动从未 Running 时不进入本方法。</remarks>
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
        /// <remarks>只做检测分级 重连归通讯层。
        /// 界面在线显示始终按收包刷新，NeedHeartbeat=false 只停分级日志。</remarks>
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
            WeldAlgorithmsConfig config = WeldParamManager.Instance.AlgorithmsConfig;
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
        /// <remarks>队列空时 Sleep(100)（比模组线程的 10 ms 更长），避免空转。</remarks>
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
        /// <remarks>坐标类仅记录，动作类写 IO，统一由扫描器消费。</remarks>
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

                // 动作类：机器人请求进入焊前 → 启动 IO
                case CommandCore.RxPreWeldRequest:
                    return IoStart;

                // 动作类：归零位 / 安全停止位 → 停止 IO
                case CommandCore.RxHomePose:
                case CommandCore.RxStopSafePose:
                    return IoStop;

                // 坐标类：解析后仅记录，不驱动状态转换
                case CommandCore.RxStartSafePose:
                case CommandCore.RxPreWeldPose:
                case CommandCore.RxWeldStartPose:
                case CommandCore.RxPreStopPose:
                case CommandCore.RxWeldStopPose:
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>机器人指令前置条件问询（按目标 IO 校验）。</summary>
        /// <remarks>启动类要求整机 Stop 且无模组报警。
        /// 停止类要求整机 Running，与动作链同口径。</remarks>
        /// <param name="command">已规整的指令常量（日志用）</param>
        /// <param name="ioName">映射到的虚拟 IO 名</param>
        /// <returns>前置条件满足返回 true</returns>
        private bool IsCommandPreconditionMet(string command, string ioName)
        {
            if (ioName == IoStart)
            {
                if (Current != MainDeviceStatus.Stop)
                {
                    Log(string.Format("机器人指令 {0} 拒收 整机非 Stop 态 当前 {1}", command, Current), MessageLevel.Warning);
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

            lock (_commLock) { _commCommandQueue.Enqueue(command); }
        }

        /// <summary>记录机器人停止位 X，并即时更新行程监视目标。</summary>
        /// <remarks>焊缝终止 X 即机器人停止位。
        /// 收工行程＝|终止X − 起点X| − 前置量，下限 0。
        /// 未激活时只缓存，锚定时优先采用。</remarks>
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
        /// <remarks>上行指令只走数值码承载，无文本通道。RxCmd=0 表示无指令，静默忽略。 入队）。</remarks>
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
        /// <remarks>连接能力由 Comm 提供。成功判据以 <c>ConnectRobot</c> 真实返回值为准。
        /// 通道就绪不代表监听已启动。未启用机器人通讯记跳过，不计成功。</remarks>
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
        /// <remarks>发送成功后同步广播，供 <see cref="GlobalCommData.CommunicationCommandReceived"/> 显示下发指令；
        /// 接收侧 OnCommRecived 对 Tx 指令只入队不驱动业务（DispatchCommand 命中 default 返回 null）。</remarks>
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
        /// <remarks>只清指令标志，不触碰焊参载荷与 RSI 收发周期。</remarks>
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
        /// <remarks>逐台等状态落到 Disconnected，单台上限 5s；超时只记不算失败，不中断后续设备。</remarks>
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
        /// <remarks>只算不报，明细由各模组行承载。
        /// 只有真发起连接且失败才计入失败，跳过不计。
        /// 汇总在本链最后打印，此处只落三态计数。</remarks>
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
        /// <remarks>不做轮询重试：存在失败轴即返回 false。</remarks>
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
        /// <remarks>包含「IO_INIT 已发出但 DoInitializeMachine 尚未受理」的竞态窗口（_initAccepted），
        /// 保证 IOWork 从首拍起就旁路状态变更，不会在设备未连时先报 Alarm。</remarks>
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
        /// <remarks>全部已连不重跑连接链。</remarks>
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
        /// <remarks>每步前置问询运行态，被抢占即中止本链。
        /// 不再覆盖被抢占后的新状态。</remarks>
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
        /// <remarks>单流程失败只记日志，不中断后续。
        /// 每步前置问询 Reseting，被抢占立即中止。
        /// 各子流程复位落到各自管理器，主控只编排。</remarks>
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
        /// <remarks>控制器初始化与 CAMBOX 配置已下沉至 <c>MotionControlWorkflow.InitializeOn()</c>（原档 R011）。
        /// 结果行由调用方统一打印，本方法不重复报。</remarks>
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
        /// <remarks>超时记 Warning 并保留句柄，供现场复查。</remarks>
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

        /// <summary>模拟测试线程主体：按节拍推进模拟 robotX 时钟与仿真</summary>
        /// <remarks>器调度状态机。</remarks>
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
        /// <remarks>模拟位姿统一虚拟 XYZ（X 推进，Y/Z 恒 0）。
        /// 经 UpdateRobotPose 单入口喂入，不直接写 CurrentRobotX。</remarks>
        private void SimTick()
        {
            lock (_robotXLock)
            {
                if (!_robotXSimEnabled) return;
                double elapsed = (DateTime.Now - _simStartTime).TotalSeconds;
                _simRobotX = _simStartX + _simSpeed * elapsed;
            }
            // 模拟位姿单入口喂入，数据生成收口在本类
            WeldParamManager.Instance.UpdateRobotPose(_simRobotX, 0.0, 0.0);
        }

        /// <summary>动作入口双闸门：设备态 × 就绪层。</summary>
        /// <remarks>模拟中放行，沿用既有旁路口径。
        /// 就绪拒绝日志由工作流层记录。</remarks>
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
        /// <remarks>起焊前须已有功率与送丝，否则有事故风险。
        /// 会话清空在播种前执行，起焊沿只清跟踪。
        /// 窗口内不清参数，避免产生变动观测。</remarks>
        private void DoSubPrework()
        {
            if (!CheckActionGate("焊前检查", ReadinessStage.Connected, _lineLaser, out _)) return;
            try { WeldParamManager.Instance.ResetSeamTracking("焊前检查会话复位"); }
            catch (Exception ex) { Log("焊前检查会话复位异常 " + ex.Message, MessageLevel.Warning); }
            CascadeWeldStatus(SubDeviceWeldStatus.PreWork, "模组焊前检查");
            try { WeldParamControlWorkflow.Instance.PreWork(); }
            catch (Exception ex) { Log("焊前预置工艺参数异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>模组焊接启动命令（原档 §10.3 序号 10：S3 → S4）。</summary>
        /// <remarks>起焊即开生产会话，缓存产品码与记录状态。
        /// 同时锚定焊缝行程监视，行程判断归主控。
        /// 运控未回标定位时挂起受理（置 _subWorkPending）。
        /// 后补执行，等待靠按拍轮询变量而非线程等待。
        /// 运控未启动或已就绪立即放行，不阻塞。</remarks>
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
            BeginWeldMonitor();
            try { ProductFileRecordManager.Instance.BeginProductSession(CurrentProductSn, IsSimulationEnabled); }
            catch (Exception ex) { Log("开启生产会话异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>焊接启动门控开闭判定（标定位未就绪时关门）。</summary>
        /// <remarks>运控未启动或已回标定即放行，不阻塞。</remarks>
        /// <returns>允许立即下发 Working 返回 true</returns>
        private bool IsSubWorkGateOpen()
        {
            return !(_motion.WeldStatus == SubDeviceWeldStatus.PreWork && !_motion.IsCalibrationYReady);
        }

        /// <summary>焊接启动挂起按拍问询（门开补执行/失败弃单）。</summary>
        /// <remarks>5 条模组线程每拍调用，锁内幂等。
        /// 非阻塞轮询变量，不 Sleep。</remarks>
        private void CheckSubWorkPending()
        {
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
            Log("焊接启动等待运控标定位回归（IsCalibrationYReady 未就绪）", MessageLevel.Warning);
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
        /// <remarks>前置距离处收工，机器人仍须走到终止 X。
        /// 终止位优先取机器人上报停止位 X。
        /// 占位空缺时不做收工判断，只按兜底行程监视。</remarks>
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
        /// <remarks>线程每拍问询，锁内幂等只触发一次动作。
        /// 行程到收工行程即停线激光（关外设与入图）。
        /// 行程到终止行程才下发完成，走完末端焊缝。</remarks>
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
        /// <remarks>仅焊接中的切换入档，避免调试期污染记录。
        /// 只写工艺控制记录（.pDat），不写轮廓记录。
        /// todo 指令表暂无 Job 切换常量，待协议细化后接入。</remarks>
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
        /// <remarks>步号范式：大步 10/20/30、小步 +1。
        /// 下发焊前检查并等 PreWork 完成反馈。
        /// 门控：运控回标定才放行，与真实同门。
        /// 门开先下发焊前检查与参数预置。
        /// 下发启动，等待到程由行程监视收口。
        /// 仿真器不越权直驱子流程步骤，等待均为轮询。</remarks>
        private void SimScheduleTick()
        {
            // 参与设备异常或整机急停即终止模拟，
            // 避免空转累积记录与内存。
            // 不判 Current==Alarm：初始化部分失败（如监控相机缺失）会残留 Alarm 态，
            // 启动窗口期会误杀，模拟不判真实硬件态
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

            switch (_simSchedStep)
            {
                case 10:
                    // 大步 10：等线激光进入焊前检查（UI 侧 StartSimulationLineLaser 完成自动连接与 Start）
                    if (_lineLaser.WeldStatus == SubDeviceWeldStatus.PreWork)
                    {
                        Log("模拟仿真器 线激光已进入焊前检查 等待完成反馈");
                        _simSchedStep = 20;
                    }
                    break;

                case 20:
                    // 大步 20：等 PreWork 完成反馈（变量变 true 推进）
                    if (_lineLaser.IsPreWorkFeedbackDone) _simSchedStep = 21;
                    break;

                case 21:
                    // 小步 21：启动门控，未回标定位则等，2s 限频告警
                    // 门开先下发焊前检查与参数预置（播种）
                    if (!IsSubWorkGateOpen())
                    {
                        LogPendingGateOnce();
                        break;
                    }
                    DoSubPrework();
                    Log("模拟仿真器 焊接启动门开 下发焊前检查与参数预置 等效预焊接点");
                    _simSchedStep = 22;
                    break;

                case 22:
                    // 小步 22：已播种，下发焊接启动
                    DoSubWork();
                    Log("模拟仿真器 参数已播种 下发焊接启动 等效机器人 Start 指令");
                    // 启动：切连续采集 + 开录像，失败只告警
                    SimMonitorCamStartWeld();
                    _simSchedStep = 30;
                    break;

                case 30:
                    // 大步 30：等待，到程由行程监视收口并停模拟
                    break;
            }
        }

        /// <summary>向 5 个模组统一下发焊接态迁移请求。</summary>
        /// <remarks>虚拟设备 WeldParamControl 一并参与（4 实体 + 1 虚拟）。
        /// 级联写入后立即调用 WeldStatusTrans() 委托子流程迁移。
        /// 这是停焊沿 / Stopping 的确定性触发点。
        /// 历史缺陷：调用方曾在 DispatchCommand 移除时丢失。</remarks>
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
        /// <remarks>PreWork 阶段相机无动作（光丝对中未做，跳过）；采集与录像在焊接启动时开启。 备；不在线或打开失败只告警，不阻断模拟。</remarks>
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
        /// <remarks>逐帧分发独立副本，监控调试页据此出图。
        /// 主页面预览经工作流 30Hz 节流转交。
        /// 失败只告警不阻断焊接。</remarks>
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
        /// <remarks>按实际状态动作，重复调用零害。</remarks>
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
        /// <remarks>真实链路：机器人置 RxCmd，下帧解析后调用。
        /// 本地注入：调试面板直调，无真机亦可验证。</remarks>
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
        /// <remarks>硬件/机器人/UI 三路径都调用本方法置位。
        /// 由 IOWork 与 EMGWork 统一扫描，扫描器不区分触发源。</remarks>
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
        /// <remarks>急停等电平型信号用本方法持续监控；脉冲型命令请用 <see cref="TakeVirtualIo"/>。</remarks>
        /// <param name="ioName">虚拟 IO 常量</param>
        /// <returns>已置位返回 true</returns>
        public bool IsVirtualIoSet(string ioName)
        {
            if (string.IsNullOrEmpty(ioName)) return false;
            lock (_virtualIoLock) { return _virtualIoSet.Contains(ioName); }
        }

        /// <summary>取走虚拟 IO 并清位（脉冲型命令，保证每条命令只执</summary>
        /// <remarks>行一次）。</remarks>
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
        /// <remarks>扫描器唯一执行入口：只负责命中 IO。
        /// 不关心 IO 由谁置位、该执行什么。
        /// 调用方须先取走 IO，动作前即消除，不会重复触发。
        /// 除急停外应交给独立动作线程，不占用扫描线程。</remarks>
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
        /// <remarks>未进 Working 却有模组退回待机，整机回落 Stop。
        /// 由 IOWork 逐拍调用。</remarks>
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
        /// <remarks>内部 1 s 节流，只纠偏不推进业务。
        /// 模拟与初始化阶段不参与。
        /// 子流程报警只升不降。</remarks>
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
        /// <remarks>每类一条 Warning，纠正走既有级联。</remarks>
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
        /// <remarks>剪丝走具名元素 CutWire（输出端 3），
        /// 不再占 Tech T13 内部编号；TxCmd=10 为备用，TxCutWire 指令码不在映射表内。</remarks>
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
        /// <remarks>Y 向偏差只报警不自动调整，X 向偏差换算送丝补偿。
        /// 补偿后送丝速度钳不小于 0。
        /// 识别失败/粘连即报警并置 RSI Tech T13 剪丝。
        /// 超时复位重检，超上限则降级报警转人工。
        /// case 内禁 Sleep/while，无回执靠超时兜底。</remarks>
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
        /// <remarks>只前进）。</remarks>
        /// <param name="algoFeedSpeed">工艺算法给出的送丝速度</param>
        /// <returns>补偿后的送丝速度</returns>
        public double ComposeWireFeedSpeed(double algoFeedSpeed)
        {
            double result = algoFeedSpeed + _wireFeedCompensation;
            return result < 0.0 ? 0.0 : result;
        }

        /// <summary>光丝对中偏差驱动振镜中心微调（默认关闭）。</summary>
        /// <remarks>开关 GALVO_AUTO_CENTER_COMP（默认 false ⇒ 仅状态监控）。
        /// 打开后按对中偏差算中心值，交单写口下发。
        /// ⚠️ 像素→振镜中心的符号方向现场标定（增益可负）。</remarks>
        public void ApplySpotAlignCompensation()
        {
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
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
        /// <remarks>态转换）。</remarks>
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
        /// <remarks>5 个模组 = 4 个实体 + 1 个虚拟（WeldParamControl）。</remarks>
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
        /// <remarks>前置条件：M3 停止 / S2 Stanby，运行中禁止修改。
        /// 违反则拒绝并返回 false。</remarks>
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
        /// <remarks>前置条件同 <see cref="SetWeldParam"/>：M3 停止，运行中禁止修改。</remarks>
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
        /// <remarks>非阻塞轮询替代 Sleep 循环：失败与成功同样立即推进。
        /// 无反馈才等满超时，避免失败却仍空等。
        /// 结果行由 RecordInitOutcome 统一打印，本方法不复述。</remarks>
        /// <param name="child">模组状态对象</param>
        /// <returns>出结果（成功 / 失败 / 超时）返回 true 可推进；仍在等待返回 false</returns>
        private bool WaitChildConnectedTick(DeviceStateBase child)
        {
            if (child.ConnectSettled) return true;
            return (DateTime.Now - _initStepTs).TotalMilliseconds >= CascadeConnectTimeoutMs;
        }

        /// <summary>推进到下一个初始化节点。</summary>
        /// <remarks>同时刷新计时，每节点超时从进入时刻起算。</remarks>
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
            _simSavedWeldHeadEnabled = _weldHead.IsEnable;
            _simSavedMonitorCamEnabled = _monitorCam.IsEnable;
            _weldHead.IsEnable = false;
            _monitorCam.IsEnable = false;
        }

        /// <summary>模拟启动前清残留报警并确保进入 Running</summary>
        private void EnsureRunningForSimulation()
        {
            // Running 门禁对 Alarm/EStop 不放开，不清理则模拟永远起不来
            if (Current == MainDeviceStatus.Alarm) DoClearAlarm();
            if (Current != MainDeviceStatus.Running) DoStartMachine();
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
        /// <remarks>关闭顺序固定为 线程 → 设备 → 主站。
        /// 先通知常驻线程并显式等待退出。
        /// 线程退净后再级联断开设备。
        /// 线程还在跑就关设备会触发多余急停。
        /// 置位型虚拟 IO 靠扫描线程消费。
        /// 线程先退会丢信号，故级联断开在此同步执行。
        /// ⛔ 不走 DoStopMachine：状态迁移会向正在销毁的窗体派发事件。
        /// 详见 [[lessons/StartupInitializationOrderGates]] 坑 6。</remarks>
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
        /// <remarks>状态三线程不在登记表内，按运行标志判定；未启动视为已退出。</remarks>
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
        /// <remarks>读-比对-写），失败即降级 TA，不阻断其它轴</remarks>
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
        /// <remarks>只拉起 IO 扫描线程并置位 IoInit，连接链在 DoInitializeMachine；IO 线程必须先就绪，否则 Init_IO 无人消费。
        /// ⛔ 以下线程**不在此启动**，统一延后到 <see cref="FinishInitialization"/>（初始化链拿到结果）之后：
        /// · 5 条模组工作线程（MachineFlowWorkOpen）——初始化期空转，且会提前驱动工作流改状态；
        /// · 机器人指令交互线程（thCommCommandWork，含通讯层自动重连）——期间启动会与初始化链并发写 RSI XML（文件占用），
        ///   且违反「初始化结束拿到结果后才允许重连」的强制要求。
        /// ⚠️ 置位 IoInit **之前**先同步置 _initializing：IOWork 拿到 CPU 即可从首拍起旁路互锁/状态变更，
        /// 消除「IO_INIT 已置位、DoInitializeMachine 尚未置 _initializing」窗口内设备未连被判 Alarm 的竞态。</remarks>
        /// <returns>IO 线程已拉起且 IO 置位成功返回 true</returns>
        public bool InitializeMachine()
        {
            // 受理标志先于 IO_INIT 置位：封堵 IOWork 首拍竞态（未连设备先报 Alarm）。
            // ⚠️ 用独立 _initAccepted 而非 _initializing：后者是 DoInitializeMachine 的重复受理守卫，
            //    若在此提前置位会让真正的初始化链被当作"重复 Init_IO"拒绝。
            _initAccepted = true;
            DeviceStatusWork.Instance.MachineIOWorkOpen();

            SetVirtualIo(IoInit);
            Log("系统初始化命令已发出，等待监控线程执行（IO_INIT）");
            return true;
        }

        /// <summary>初始化未进入推进链即结束的收尾：清受理标志并开启模组线程。</summary>
        /// <remarks>用于 DoInitializeMachine 的拒绝/跳过出口——这些路径不会走 FinishInitialization，
        /// 若不在此开模组线程，线程将永不启动；若不清理 _initAccepted，UI 门控与 IOWork 互锁旁路将永久生效。</remarks>
        /// <param name="message">收尾日志</param>
        /// <param name="level">日志级别</param>
        private void EndInitWithoutChain(string message, MessageLevel level = MessageLevel.Info)
        {
            _initAccepted = false;
            // 与 FinishInitialization 的收尾对齐：初始化已出结果（拒绝/跳过），
            // 模组线程与指令线程在此统一开启，避免线程永不启动（ADR-107 跳过路径此前遗漏）。
            MachineFlowWorkOpen();
            EnsureCommandWorkStarted();
            Log(message, level);
        }

        /// <summary>执行初始化链（由 thIOWork 扫描到 Init_IO 后调用，不对外直接调用）。</summary>
        /// <remarks>按依赖层级推进：父级未就绪则子级记跳过。
        /// 本方法只受理并复位进度即返回，后续每拍推进。</remarks>
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
        /// <remarks>父子关系取自对象归属，不另建设备清单。
        /// 新增轴由运控内部聚合，无需改本表。</remarks>
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
        /// <remarks>只读配置，不查总线；总线在失败预判里查。</remarks>
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
        /// <remarks>配了没连上属真失败，不按跳过处理。</remarks>
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
        /// <remarks>只作 Debug 明细，用于核对层级顺序；面向业主的权威行只报 [n/5] 模组结果。</remarks>
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
        /// <remarks>成功判据以监听真正启动为准；失败只报通讯层，不阻塞其它独立模组。</remarks>
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
        /// <remarks>从站随运控装载，父级未就绪时不会进入本节点。</remarks>
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
        /// <remarks>成功 / 失败 / 跳过分开计数，供界面三态表达；跳过不计入失败。</remarks>
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
        /// <remarks>逐模组已落日志，本方法只做使能与汇总。
        /// 保证汇总恒为初始化序列最后一条。</remarks>
        private void FinishInitialization()
        {
            _initializing = false;
            _initAccepted = false;
            // 初始化链已出结果：5 条模组工作线程此时才启，避免初始化期空转并提前驱动工作流改状态
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
            Log(initSucceeded
                    ? "初始化结束 已连接 已使能 未回零 无运动 待复位"
                    : "初始化结束 未全部就绪 已连接设备保留 未连设备可再次初始化补连",
                initSucceeded ? MessageLevel.Info : MessageLevel.Error);
            Log(string.Format("初始化汇总 成功 {0} 失败 {1} 跳过 {2}",
                _initOkCount, _initFailCount, _initSkipCount),
                initSucceeded ? MessageLevel.Info : MessageLevel.Error);

            // 初始化链已出结果：此时才开启机器人指令交互线程（含通讯层自动重连）。
            // ⛔ 初始化期间开启会与初始化链并发写机器人端 RSI XML 导致文件占用，
            //    且违反「初始化动作结束拿到结果后才允许重连」的强制要求。
            EnsureCommandWorkStarted();
        }

        /// <summary>开启机器人指令交互线程（幂等；初始化链结束的唯一出口）</summary>
        /// <remarks>初始化动作结束、拿到结果后才允许开启：本线程含通讯层自动重连，
        /// 初始化期间开启会与初始化链并发写机器人端 RSI XML 导致「文件正由另一进程使用」。
        /// 正常结束走 FinishInitialization，中止走 InitializeMachineTick 的抢占分支，两处都要调。</remarks>
        private void EnsureCommandWorkStarted()
        {
            MachineCommandWorkOpen();
        }

        #endregion

        /// <summary>初始化节点驱动：按层级表每拍推进。</summary>
        /// <remarks>父级非成功则子级记跳过，不发起连接。
        /// 机器人通讯需界面就绪，按拍重试不 Sleep。
        /// 每拍问询运行态，被抢占即中止。
        /// 连接按拍轮询，超时收敛为 Alarm。</remarks>
        public void InitializeMachineTick()
        {
            if (!_initializing) return;

            // 每拍问询：被抢占即中止，日志只记一次
            if (IsInitPreempted())
            {
                _initializing = false;
                _initAccepted = false;
                Log("初始化链被中止 运行态已变更为 " + Current, MessageLevel.Warning);
                // 中止也是初始化链结束：同样在此开启模组线程与指令线程，避免线程永不启动
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
        /// <remarks>初始化默认加载、复位前强制加载。</remarks>
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
        /// <remarks>按原档 §2.3② 只置位不执行，执行体见 <see cref="DoResetMachine"/>。</remarks>
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
        /// <remarks>一次性 Task + 每步前置问询状态，急停态直接拒绝复位。
        /// 清状态后校验为 NoReset，每步校验为 Reseting。
        /// 任一步被抢占即中止，不覆盖新状态。</remarks>
        /// <returns>受理返回 true；急停态或机器人在位检测不通过返回 false</returns>
        public bool DoResetMachine()
        {
            if (Current == MainDeviceStatus.EStop)
            {
                Log("急停态禁止复位 请先解除急停", MessageLevel.Warning);
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
        /// <remarks>按原档 §2.3② 只置位不执行，执行体见 <see cref="DoStartMachine"/>。</remarks>
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
        /// <remarks>执行由常驻子线程轮询，本方法只切状态并拉起。
        /// 置 Running 后再问询一次，被抢占则中止。</remarks>
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

            Log("主设备运行管控启动，常驻线程已就绪");
            return true;
        }

        /// <summary>设备停止命令（映射 Stop_IO）。</summary>
        /// <remarks>按原档 §2.3② 只置位不执行，执行体见 <see cref="DoStopMachine"/>。</remarks>
        public void StopMachine()
        {
            SetVirtualIo(IoStop);
            Log("设备停止命令已发出，等待监控线程执行（IO_STOP）");
        }

        /// <summary>执行停止链（thIOWork 扫描到 Stop_IO 后调用）。</summary>
        /// <remarks>不关 thIOWork：停机后仍需接收命令，Dispose 时关。</remarks>
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
        /// <remarks>按原档 §2.3② 只置位不执行，执行体见 <see cref="DoClearAlarm"/>。</remarks>
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
        /// <remarks>消除报警一律回 NoReset，只有复位流程成功才到 Stop。</remarks>
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
        /// <remarks>纯状态迁移，与急停同属同步例外。
        /// 报警不被忙丢弃机制拒绝，根因 Error 已由检测点记录。</remarks>
        public void DoMotionBusFault()
        {
            // 焊接中总线故障：先通知机器人停止动作（TX_NG）
            if (IsWelding) SendRobotCommand(CommandCore.TxNG);
            SetStatus(MainDeviceStatus.Alarm, "运控总线故障");
        }

        /// <summary>急停命令（无虚拟 IO，直接同步执行）。</summary>
        /// <remarks>急停只有物理 IO，触发源直接同步执行。
        /// 不走虚拟 IO 扫描，必须立即生效。</remarks>
        /// <param name="reason">急停原因</param>
        public void EstopMachine(string reason)
        {
            _emgReason = reason;
            DoEstopMachine();
        }

        /// <summary>执行急停（由 EstopMachine 直通调用，触发源为 EMGWork 物理信号边沿 / 机</summary>
        /// <remarks>器人 RxAbort 指令）。顺序固定：停运动 → 置 EStop → 停线程 → 置失能。
        /// ⛔ 不断开设备：解除后仍需读状态与复位，T09~T11 保持运行。</remarks>
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
        /// <remarks>机器人中止指令经 CommandCore.TxAbort 下发；运控急停走 DeviceStateBase.EmergencyStop 链。</remarks>
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

        /// <summary>开启模拟测试。</summary>
        /// <remarks>单一开关，不再区分 A/B/C 模式。
        /// 线激光未连则用虚拟相机，已连复用真实相机。
        /// 运控未连接则只记录轨迹与工艺调整、不写轴。
        /// 起点 X 随机 0~20，最后启动时钟与模拟线程。</remarks>
        public void StartSimulation()
        {
            if (_simulationRunning) return; // 已在运行则忽略
            _simulationRunning = true;

            // 启动侧兜底清残留，否则首拍即被异常守卫终止。
            // 异常退出未必走 DisableSimulation，开启侧再清一次。
            _motion.ClearSeamAlarm();
            if (_motion.WeldStatus == SubDeviceWeldStatus.ErrorAborted) _motion.ResetProcess();

            PrepareSimulationCamera();

            // 检查焊接相机在线并打开，预览走监控调试页
            PrepareSimulationMonitorCam();

            // 启动共享模拟 robotX 时钟（起点随机 0~20；线激光/运控统一读取）
            StartRobotXSimulation();

            // 模拟只跑线激光与运控，只做轮廓与轨迹记录
            SuspendRealHardwareForSimulation();

            // 模拟不初始化、不判连接态，清残留后进运行
            EnsureRunningForSimulation();

            // 模拟线程须在清警后拉起，否则首拍被误杀
            SimTestWorkOpen();

            // 仿真器从零起步：等进焊前检查后按命令链驱动
            _simSchedStep = 10;

            SaveSimulation();
            LogSimulationStarted();
        }

        /// <summary>关闭模拟测试：复位运行标志、停止共享模拟 robotX 时钟</summary>
        /// <remarks>与模拟测试线程，并落盘本次模拟记录。</remarks>
        /// <param name="reason">停止原因（人话描述）</param>
        /// <param name="hasError">是否因错误/报警停止</param>
        /// <param name="isAuto">是否为自动停止</param>
        public void DisableSimulation(string reason = "用户手动停止", bool hasError = false, bool isAuto = false)
        {
            bool wasEnabled = _simulationRunning;
            if (!wasEnabled) return; // 幂等：已关闭则不重复停止/记录，消除「模拟关闭」重复日志
            _simulationRunning = false;

            // 线激光反订阅、关采集并归位，避免污染真实启动
            try { _lineLaser.SimReset(); }
            catch (Exception ex) { Log("模拟复位线激光异常 " + ex.Message, MessageLevel.Error); }

            // 焊参回待机，清理级联残留的 Working 态
            try { _weldParam.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位工艺参数控制异常 " + ex.Message, MessageLevel.Error); }

            // 焊接相机收口兜底，异常路径未经停止命令
            SimMonitorCamStopWeld();

            // 监控相机对称回待机，停止须对称复位
            try { _monitorCam.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位监控相机异常 " + ex.Message, MessageLevel.Error); }

            // 运控对称复位，停止须与 5 模组级联对称。
            // 残留 ErrorAborted 会让下次模拟启动即被终止
            try
            {
                _motion.Stop();
                _motion.ClearSeamAlarm();
                _motion.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位");
            }
            catch (Exception ex) { Log("模拟复位运控异常 " + ex.Message, MessageLevel.Error); }

            // 焊接头对称复位，缺一即残留 Working/Stopping
            try { _weldHead.RequestWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位"); }
            catch (Exception ex) { Log("模拟复位焊接头异常 " + ex.Message, MessageLevel.Error); }

            // 恢复被暂停的模组使能态
            _weldHead.IsEnable = _simSavedWeldHeadEnabled;
            _monitorCam.IsEnable = _simSavedMonitorCamEnabled;
            // 仅回落 Stop 让线程空转，避免永久杀线程。
            // 模拟态模组未连接，无线程断开动作。
            if (Current == MainDeviceStatus.Running)
                SetStatus(MainDeviceStatus.Stop, "模拟测试关闭 退出运行");
            StopRobotXSimulation();
            SimTestWorkClose();
            // 模拟终止：记录落库（pass）+ 轨迹缓存落盘 + 三份归档。
            // 归档可达秒级，移后台线程避免冻结 UI；
            // 代价：关闭时归档未完成可能截断，模拟可接受
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { EndProductSessionSafely("pass", string.Empty); }
                catch (Exception ex) { Log("模拟归档后台落盘异常 " + ex.Message, MessageLevel.Error); }
            });
            // 统一触发停止事件，UI 订阅后复位按钮
            try
            {
                SimulationStopped?.Invoke(this, new SimulationStoppedEventArgs(reason, hasError, isAuto));
            }
            catch (Exception ex) { Log("触发模拟停止事件异常 " + ex.Message, MessageLevel.Warning); }

            GlobalCommData.ShowLog(_simTag, string.Format("模拟测试关闭 原因 {0}", reason), MessageLevel.Info);
        }

        /// <summary>启动共享模拟位姿时钟（起点 0~20，目标 1500~2000mm）。</summary>
        /// <remarks>robotX = 起点 + 默认速度 × 流逝时间，Y/Z 恒 0。
        /// 开启后即时喂入初值，避免沿用残留位姿。</remarks>
        public void StartRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _simStartX = _simRand.Next(0, 20);        // 焊接起点随机 0~20
                _simSpeed = SimDefaultSpeedMmPerS;        // 内部默认速度，不再由 UI 设定
                _simWeldLengthMm = _simRand.Next(1500, 2001); // 模拟行程目标随机 1500~2000mm（60mm/s 单程 25~33 秒）
                _simRobotX = _simStartX;
                _simStartTime = DateTime.Now;
                _robotXSimEnabled = true;
            }
            // 喂新起点前须复位跟踪：上会话已走到行程末端。
            // 不复位则首帧被判折返疑似复焊。
            // 参数管控尚未开始，复位在窗口外不产生观测。
            // 显示 0 正确，播种后才变 1.2
            WeldParamManager.Instance.ResetSeamTracking("模拟启动重置");
            WeldParamManager.Instance.UpdateRobotPose(_simStartX, 0.0, 0.0);
        }

        /// <summary>停止共享模拟 robotX 时钟</summary>
        public void StopRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _robotXSimEnabled = false;
            }
        }

        /// <summary>启动模拟用线激光流程。</summary>
        /// <remarks>线激光为级联模组，模拟流程由本类编排。
        /// UI 仅调用本方法，结果经 StateSwitched 消费。
        /// UI 不得越权直驱线激光。</remarks>
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
        /// <remarks>位姿统一落在 WeldParamManager.CurrentRobotX。
        /// 故统一从此读取，消除多源写入问题。
        /// 模拟关闭时返回 0.0，沿用原行为。</remarks>
        /// <returns>当前有效机器人 X 坐标</returns>
        public double GetEffectiveRobotX()
        {
            bool sim;
            lock (_robotXLock) { sim = _robotXSimEnabled; }
            return sim ? WeldParamManager.Instance.CurrentRobotX : 0.0;
        }

        /// <summary>从配置文件加载模拟参数（线激光数据频率）。</summary>
        /// <remarks>须在应用启动、创建任何页面之前调用。文件不存在或读取失败时保留默认值。</remarks>
        public void LoadSimulation()
        {
            try
            {
                SimLaserFrequencyHz = _simConfig.LoadLaserFrequencyHz(SimLaserFrequencyHz);
            }
            catch
            {
                // 加载异常时保留当前值
            }
        }

        /// <summary>保存模拟参数（线激光数据频率）到配置文件。</summary>
        /// <remarks>用户通过界面「保存模拟参数」时调用。</remarks>
        public void SaveSimulation()
        {
            try
            {
                _simConfig.SaveLaserFrequencyHz(SimLaserFrequencyHz);
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
        /// <remarks>阶段 0 只用于登记与核对，不参与线程启停判定。</remarks>
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
