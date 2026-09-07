using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.Robot.KUKARobot;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.LaserWeldHead;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.LineLaserCamApi;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.MotionControl;

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

    /// <summary>整机报警信息（原档20260904 §10.4 序号 17 GetAlarmInfo 的返回结构）。</summary>
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

    /// <summary>主设备运行管控（单例，整机编排与机器人/PLC 交互）。</summary>
    /// <remarks>不继承流程基类（原档 R013），业务状态链由 MainDeviceStatus 表达；7 条常驻线程驱动 5 个子流程与指令交互，指令协议见 CommandCore；模拟线程按需增开。</remarks>
    public class DeviceControlWork : IDisposable
    {
        #region 常量

        /// <summary>子设备工作线程运行态节拍（毫秒，防空转）。</summary>
        private const int BeatWorkMs = 10;

        /// <summary>非运行态慢节拍（毫秒，防空转刷屏）。</summary>
        private const int BeatIdleMs = 100;

        /// <summary>指令交互线程空闲节拍（毫秒，比子设备线程更长）。</summary>
        private const int BeatCommandMs = 100;

        /// <summary>机器人通讯心跳检测周期（毫秒）。</summary>
        private const int HeartBeatIntervalMs = 1000;

        /// <summary>心跳失效后重连尝试间隔（毫秒）。</summary>
        private const int HeartReconnectIntervalMs = 5000;

        /// <summary>模拟测试线程节拍（毫秒，50Hz）。</summary>
        private const int SimBeatMs = 20;

        /// <summary>子设备 Connected 级联等待超时（毫秒）。</summary>
        private const double CascadeConnectTimeoutMs = 10000;

        /// <summary>等待子设备连接期间的单次轮询间隔（毫秒）。</summary>
        private const int WaitChildPollMs = 100;

        #endregion

        #region 虚拟IO常量（原档20260904 §2.4 / §10）

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

        /// <summary>虚拟 IO：急停（原档 §10.2 序号 6，映射 EMG_IO）。</summary>
        public const string IoEmg = "IO_EMG";

        /// <summary>虚拟 IO：子设备连接（原档 §10.3 序号 7，映射 SubConnect_IO）。</summary>
        public const string IoSubConnect = "IO_SUB_CONNECT";

        /// <summary>虚拟 IO：子设备复位（原档 §10.3 序号 8，映射 SubReset_IO）。</summary>
        public const string IoSubReset = "IO_SUB_RESET";

        /// <summary>虚拟 IO：子设备焊前检查（原档 §10.3 序号 9，映射 SubPrework_IO）。</summary>
        public const string IoSubPrework = "IO_SUB_PREWORK";

        /// <summary>虚拟 IO：子设备焊接启动（原档 §10.3 序号 10，映射 SubWork_IO）。</summary>
        public const string IoSubWork = "IO_SUB_WORK";

        /// <summary>虚拟 IO：子设备停止流程（原档 §10.3 序号 11，映射 SubStop_IO）。</summary>
        public const string IoSubStop = "IO_SUB_STOP";

        /// <summary>虚拟 IO：子设备手动停止（原档 §10.3 序号 12，映射 SubManualStop_IO）。</summary>
        public const string IoSubManualStop = "IO_SUB_MANUAL_STOP";

        /// <summary>虚拟 IO：子设备报警停止（原档 §10.3 序号 13，映射 SubAlarmStop_IO）。</summary>
        public const string IoSubAlarmStop = "IO_SUB_ALARM_STOP";

        #endregion

        #region 私有变量

        private static readonly Lazy<DeviceControlWork> _lazyInstance =
            new Lazy<DeviceControlWork>(() => new DeviceControlWork());

        private readonly string _tag = "主设备运行管控";

        private readonly object _statusLock = new object();
        private MainDeviceStatus _status = MainDeviceStatus.NoReset;
        private MainDeviceStatus _lastStatus = MainDeviceStatus.NoReset;

        // 原档 R013 私有属性：四个流程标志位 + 双份状态快照（命名沿用原档，便于与原档对表）
        private volatile bool IsReseting;
        private volatile bool IsWorking;
        private volatile bool IsCloseWork;

        private volatile bool _isAlarmstop;
        private volatile bool _initializing;
        private volatile bool _workStarted;
        private volatile bool _commStarted;
        private volatile bool _robotOnline;

        private Thread thLaserHeadWork;
        private Thread thLineLaserWork;
        private Thread thMonitorCamWork;
        private Thread thMotionControlWork;
        private Thread thWeldParamControlWork;
        private Thread thHeartInspectWork;
        private Thread thCommCommandWork;

        /// <summary>机器人指令队列（通讯回调只入队，指令线程消费）。</summary>
        private readonly Queue<string> _commCommandQueue = new Queue<string>();
        private readonly object _commLock = new object();

        // ---- 虚拟 IO 层（原档20260904 §2：三种触发源统一收敛，不新建 .cs 文件，用户 2026-09-04 硬约束）----

        /// <summary>虚拟 IO 置位表：硬件按钮 / 机器人命令 / 软件UI 三种触发源统一在此置位。</summary>
        /// <remarks>由 <c>DeviceStatusWork</c> 的 thIOWork（Running+Alarm）与 thEMGWork（急停）扫描消费，
        /// 两线程同时监控本表（虚拟 IO）与控制器给出的物理 IO。</remarks>
        private readonly HashSet<string> _virtualIoSet = new HashSet<string>();
        private readonly object _virtualIoLock = new object();

        /// <summary>急停原因（随 EMG_IO 传递，避免置位时丢失上下文）。</summary>
        private volatile string _emgReason;

        /// <summary>最近一次报警信息（原档 §10.4 序号 17 GetAlarmInfo 数据源）。</summary>
        private MachineAlarmInfo _lastAlarm;

        /// <summary>本轮运行是否已真正进入 Working（用于区分「预检失败」与「焊接完成回落」）。</summary>
        private volatile bool _everWorking;

        /// <summary>子流程是否已因运行态被抢占而停止驱动（0=驱动中，1=已停止；Interlocked 保证停止动作只做一次）。</summary>
        private int _driveStopped;

        /// <summary>待下发的焊接工艺参数（原档 §10.5 序号 19 SetWeldParam 写入）。</summary>
        private WeldParamOutput _pendingWeldParam;

        /// <summary>机器人坐标偏移与寄存位参数（原档 §10.5 序号 21 SetRobotCoord 写入）。</summary>
        private readonly object _coordLock = new object();
        private double _coordOffsetX;
        private double _coordOffsetY;
        private double _coordOffsetZ;

        /// <summary>机器人通讯管理器（上电初始化第 5 步就绪后赋值，来自顶层 Comm）。</summary>
        private KUKARobotManager RobotManager { get; set; }

        /// <summary>运动管理器（构造函数获取，供第 2 步初始化使用，原档 R013）。</summary>
        private MontionManager Motion { get; set; }

        // 级联目标（子设备）
        private readonly LineLaserWorkflow _lineLaser = LineLaserWorkflow.Instance;
        private readonly LaserWeldHeadWorkflow _weldHead = LaserWeldHeadWorkflow.Instance;
        private readonly MonitorCameraWorkflow _monitorCam = MonitorCameraWorkflow.Instance;
        private readonly MotionControlWorkflow _motion = MotionControlWorkflow.Instance;
        private readonly WeldParamControlWorkflow _weldParam = WeldParamControlWorkflow.Instance;

        // ---- 模拟模式（归并自 SimulationWorkflow） ----

        /// <summary>模拟模式日志标签（保持原 SimulationWorkflow 日志归类）。</summary>
        private readonly string _simTag = "模拟模式控制器";

        private readonly object _robotXLock = new object();

        // 模拟模式（None=关闭；其余为开启）——运行时当前是否处于模拟中
        private SimTestMode _currentMode = SimTestMode.None;

        // 用户上次选择的模式（仅界面记忆，不参与"是否开启"判定；持久化后重启显示上次选择）
        private SimTestMode _selectedMode = SimTestMode.None;

        // 共享模拟 robotX 时钟（由模拟线程按节拍发布，LineLaser / Motion 统一读取）
        private bool _robotXSimEnabled;
        private double _simStartX;
        private double _simSpeed;
        private double _simRobotX;
        private DateTime _simStartTime;

        // 模拟参数（UI 设置，持久化）
        private double _simStartXParam;
        private double _simSpeedParam;
        private double _simTargetDistance;
        private SimSeamProfile _seamProfile;
        private bool _randomMode;

        // 模拟测试线程（用户裁定 2026-09-04：随模拟启停，不在 §7.2 的 7 条常驻线程内）
        private Thread thSimTestWork;
        private volatile bool _simWorkClosing;

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

        /// <summary>对中偏差数据中枢（原档 §7.4.5 只读）。</summary>
        /// <remarks>OffsetX/Y 供控制器驱动振镜，WireStickoutDeviation 供机器人调送丝；ADR-027 产出方持有，消费方取用。</remarks>
        public MonitorResult AlignDatum
        {
            get { return _monitorCam.LastResult; }
        }

        /// <summary>当前主设备运行态（Current 快照）。</summary>
        public MainDeviceStatus Status { get { return Current; } }

        /// <summary>主设备运行态变更事件。</summary>
        public event EventHandler<MainDeviceStatusChangedEventArgs> StatusChanged;

        /// <summary>初始化链结束事件（成功或失败均触发；参数=是否全部子设备就绪）。UI 据此关闭初始化等待提示。</summary>
        public event Action<bool> InitializationCompleted;

        /// <summary>是否焊接中（运行态 Running 且整机已投入工作）。</summary>
        public bool IsWelding
        {
            get { return Current == MainDeviceStatus.Running && IsWorking; }
        }

        /// <summary>报警停止标志位（供状态层 DeviceStatusWork 读写，沿用参考实现 MachineStatusWork）。</summary>
        public bool IsAlarmstop
        {
            get { return _isAlarmstop; }
            set { _isAlarmstop = value; }
        }

        /// <summary>机器人通讯是否在线（由心跳线程刷新）。</summary>
        public bool IsRobotOnline { get { return _robotOnline; } }

        // ---- 模拟模式（归并自 SimulationWorkflow） ----

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
            // 原档 R013 / 范式 M8：构造内取运动管理器并持有
            Motion = MontionManager.Instance;
            // 订阅顶层 Comm 的通讯指令（回调只入队，不做业务）
            GlobalCommData.CommunicationCommandReceived += OnCommRecived;
            Log("主设备运行管控实例化", MessageLevel.Info);
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

        /// <summary>当前运行态快照（原档 R013：Current / Last 双份快照用于状态变化检测）。</summary>
        private MainDeviceStatus Current
        {
            get { lock (_statusLock) { return _status; } }
        }

        /// <summary>上一次运行态快照。</summary>
        private MainDeviceStatus Last
        {
            get { lock (_statusLock) { return _lastStatus; } }
        }

        /// <summary>设备状态合法控制接口：校验状态迁移是否合法（§7.3）。</summary>
        /// <remarks>非法迁移只记日志不改状态，保证急停 / 复位链路不被旁路。</remarks>
        /// <param name="target">目标运行态</param>
        /// <returns>允许迁移返回 true</returns>
        private bool MachineStatusTrans(MainDeviceStatus target)
        {
            MainDeviceStatus now = Current;
            if (now == target) return false;
            // 急停态只能经 EstopCancel 解除
            if (now == MainDeviceStatus.EStop && target != MainDeviceStatus.NoReset) return false;
            // 只有 Stop 才允许直接启动
            if (target == MainDeviceStatus.Running && now != MainDeviceStatus.Stop) return false;
            // 运行中不允许直接回落未复位，须先 Stop
            if (target == MainDeviceStatus.NoReset && now == MainDeviceStatus.Running) return false;
            return true;
        }

        /// <summary>切换主设备运行态（单控制源，经 MachineStatusTrans 校验后触发事件并记日志）。</summary>
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
                Log(string.Format("非法状态迁移被拒绝 {0} -> {1} 原因 {2}", old, newStatus, reason), MessageLevel.Warning);
                return;
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
                // 状态切换不记日志（ADR-032 报错分层：内部态迁移由状态栏/事件呈现）
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
            // todo 批次 4.5 / 4.6：Preworking 五项检查与 Working 四路子流程落地后，
            //      整机需按检查结果驱动子流程依次进入 PreWork → Working，此处仅为编排入口
        }

        /// <summary>创建并启动一条后台常驻工作线程（范式 M2：一律 IsBackground）。</summary>
        /// <param name="body">线程体</param>
        /// <param name="name">线程名（调试用）</param>
        /// <returns>已启动的线程</returns>
        private static Thread NewWorkThread(ThreadStart body, string name)
        {
            var thread = new Thread(body)
            {
                Name = name,
                IsBackground = true
            };
            thread.Start();
            return thread;
        }

        /// <summary>子设备工作线程公共体（五段式）。</summary>
        /// <remarks>非运行态慢节拍空转，运行态每拍驱动一步；单拍异常只记日志不退出，避免流程永久停摆。</remarks>
        /// <param name="flow">子流程</param>
        /// <param name="name">子设备中文名（日志用）</param>
        private void SubDeviceWork(DeviceWorkflowBase flow, string name)
        {
            while (!IsCloseWork)
            {
                // 每拍前置问询运行态：一旦被改为非 Running（停机 / 报警 / 急停 / 复位）立即停止驱动
                if (Current != MainDeviceStatus.Running)
                {
                    StopSubDeviceDrive(flow);
                    Thread.Sleep(BeatIdleMs);
                    continue;
                }

                if (_driveStopped != 0) Interlocked.Exchange(ref _driveStopped, 0);

                try { flow.FlowProcess(); }
                catch (Exception ex) { Log(name + " 流程执行异常 " + ex.Message, MessageLevel.Error); }

                Thread.Sleep(BeatWorkMs);
            }
        }

        /// <summary>运行态被抢占后的处置：停止驱动本流程，只记日志，不改任何状态。</summary>
        /// <remarks>参考基线裁定（HonorMachineTest）：线程只判断「状态不对就不执行」，不修改状态——
        /// 状态一律由 IO 动作链（DoXxx → SetStatus）专门管理；Stopping 等焊接态迁移由停止命令链下发，本方法不越权。
        /// 停止提醒整机只做一次（Interlocked 闭锁），清步骤计时仍每拍执行，防暂停时长被计入超时。</remarks>
        /// <param name="flow">子流程</param>
        private void StopSubDeviceDrive(DeviceWorkflowBase flow)
        {
            // 清步骤计时，防暂停时长被计入超时
            flow.ResetWorkTime();

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

        /// <summary>机器人通讯心跳检测线程。</summary>
        /// <remarks>只做**检测**（原档 R013「心跳检测」），不承载报文收发。
        /// 通讯开启成功前不检测、不报警（无通讯则无心跳）；心跳失效即停止本检测并触发通讯重连，不做叠罗汉式循环报警。</remarks>
        private void HeartInspectWork()
        {
            while (!IsCloseWork)
            {
                var robot = RobotManager;
                // 通讯未开启成功：静默等待，不检测心跳
                if (robot == null || !(robot.IsRSIConnected || robot.IsEKIConnected))
                {
                    _robotOnline = false;
                    Thread.Sleep(HeartBeatIntervalMs);
                    continue;
                }

                _robotOnline = true;
                // todo CommandCore 补充心跳指令常量后，在此按周期下发心跳报文
                Thread.Sleep(HeartBeatIntervalMs);

                // 在线期间连接断开：心跳失效，停检 + 重连
                if (robot.IsRSIConnected || robot.IsEKIConnected) continue;
                Log("机器人通讯心跳失效 停止心跳检测 触发通讯重连", MessageLevel.Warning);
                if (TryReconnectRobotComm())
                {
                    Log("机器人通讯重连成功 恢复心跳检测");
                    continue;
                }
                Log("机器人通讯重连失败 心跳检测保持关闭 待通讯层重试", MessageLevel.Warning);
                return;
            }
        }

        /// <summary>机器人通讯重连（心跳失效后调用；最多 3 次，间隔 5 秒）。</summary>
        /// <returns>重连成功返回 true</returns>
        private bool TryReconnectRobotComm()
        {
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null || !comm.IsRobotEnabled || comm.RobotManager == null) return false;
            for (int i = 1; i <= 3; i++)
            {
                if (IsCloseWork) return false;
                Thread.Sleep(HeartReconnectIntervalMs);
                try
                {
                    bool ok = comm.RobotCommMode == CommunicationManager.RobotCommModeType.RSI
                        ? comm.RobotManager.StartRSI()
                        : comm.RobotManager.OpenEKI();
                    if (ok)
                    {
                        _robotOnline = true;
                        return true;
                    }
                    Log(string.Format("机器人通讯重连第 {0} 次失败", i), MessageLevel.Warning);
                }
                catch (Exception ex)
                {
                    Log(string.Format("机器人通讯重连异常 {0}", ex.Message), MessageLevel.Warning);
                }
            }
            return false;
        }

        /// <summary>机器人指令交互线程（常驻循环判断，§7.2）。</summary>
        /// <remarks>队列空时 Sleep(100)（比子设备线程的 10 ms 更长），避免空转。</remarks>
        private void CommCommandWork()
        {
            while (!IsCloseWork)
            {
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
                        Log("机器人指令 " + command + " 为坐标上报类，仅记录不驱动状态转换", MessageLevel.Info);
                        continue;
                    }

                    SetVirtualIo(ioName);
                    Log(string.Format("机器人指令 {0} 已转换为虚拟 IO {1}", command, ioName));
                    // todo 指令闭环反馈机器人执行结果：待 Preworking / Working 子流程（批次 4.5 / 4.6）落地后补齐
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
        /// <remarks>坐标类解析后仅记录，动作类写 IO 量，均收敛到虚拟 IO 层由 thIOWork 统一扫描，扫描器不区分触发源。</remarks>
        /// <param name="command">已规整的指令常量</param>
        /// <returns>映射到的虚拟 IO 名；坐标上报类等无对应动作时返回 null</returns>
        private string MapCommandToIo(string command)
        {
            switch (command)
            {
                // 动作类：机器人主动急停 → 急停 IO
                case CommandCore.RxAbort:
                    _emgReason = "机器人主动急停指令";
                    return IoEmg;

                // 动作类：机器人请求进入焊前 → 启动 IO
                case CommandCore.RxPreWeldRequest:
                    return IoStart;

                // 动作类：归零位 / 安全停止位 → 停止 IO
                case CommandCore.RxHomePose:
                case CommandCore.RxStopSafePose:
                    return IoStop;

                // 坐标类：解析 string 后仅记录，不驱动状态转换（原档 §10.4：查询类不触发转换）
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

        /// <summary>机器人通讯指令回调与解析（§7.3，只解码入队不做业务）。</summary>
        /// <param name="sender">事件源</param>
        /// <param name="message">通讯原始报文</param>
        private void OnCommRecived(object sender, string message)
        {
            string command = CommandCore.Normalize(message);
            if (command.Length == 0) return;

            if (!CommandCore.IsKnownCommand(command))
            {
                Log("收到未知机器人指令 " + command, MessageLevel.Warning);
                return;
            }

            lock (_commLock) { _commCommandQueue.Enqueue(command); }
        }

        /// <summary>机器人数据接收回调（只转发解码，不做业务）。</summary>
        /// <param name="sender">事件源（机器人通讯层）</param>
        /// <param name="e">机器人上报数据事件参数</param>
        private void OnRobotDataReceived(object sender, KUKADataReceivedEventArgs e)
        {
            if (e == null || e.RobotData == null) return;
            OnCommRecived(sender, e.RobotData.EStr);
        }

        /// <summary>通讯开启（§7.3：开启动作完成即释放，非常驻线程）。</summary>
        /// <remarks>按 §7.4 裁定 C5：连接能力由 Comm 提供，本类只调用其方法完成 Open + Connect On。</remarks>
        private void CommWork()
        {
            Log("机器人通讯初始化开始");
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null || !comm.IsRobotEnabled)
            {
                Log("机器人通讯未启用，跳过");
                Log("机器人通讯跳过");
                return;
            }

            try
            {
                comm.InitializeRobotComm();

                RobotManager = comm.RobotManager;
                if (RobotManager == null)
                {
                    Log("RobotManager 未就绪", MessageLevel.Warning);
                    Log("机器人通讯初始化失败", MessageLevel.Warning);
                    return;
                }

                RobotManager.DataReceived += OnRobotDataReceived;
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

                _robotOnline = connected;
                if (connected)
                    Log("机器人通讯初始化完成");
                else
                    Log("机器人通讯连接失败，待机重试", MessageLevel.Warning);
            }
            catch (Exception ex)
            {
                Log("机器人通讯初始化异常 " + ex.Message, MessageLevel.Warning);
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
                if (comm.IsRobotEnabled && comm.RobotManager != null)
                    comm.RobotManager.SendRobotData(new KUKARobotData { EStr = command });
            }
            catch (Exception ex)
            {
                Log("下发机器人指令异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>单个子设备连接（容错：异常记日志不抛出，不中断级联）。</summary>
        /// <param name="child">子流程</param>
        /// <param name="name">子设备中文名（日志用）</param>
        private void ConnectChild(DeviceWorkflowBase child, string name)
        {
            try { child.ConnectOn(); }
            catch (Exception ex) { Log(name + " 连接异常 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>单个子设备断开（容错：异常记日志不抛出）。</summary>
        /// <param name="child">子流程</param>
        /// <param name="name">子设备中文名（日志用）</param>
        private void DisconnectChild(DeviceWorkflowBase child, string name)
        {
            try { child.ConnectOff(); }
            catch (Exception ex) { Log(name + " 断开异常 " + ex.Message, MessageLevel.Error); }
        }

        /// <summary>上电连接级联（§7.6：焊接头 → 控制器 → 线激光 → 监控相机）。</summary>
        /// <remarks>焊接工艺控制无实体设备（§8.3），不入连接级联。</remarks>
        private void CascadeConnect()
        {
            ConnectChild(_weldHead, "焊接头");
            ConnectChild(_motion, "运控");
            ConnectChild(_lineLaser, "线激光");
            ConnectChild(_monitorCam, "监控相机");
        }

        /// <summary>级联子设备断开。</summary>
        private void CascadeDisconnect()
        {
            DisconnectChild(_weldHead, "焊接头");
            DisconnectChild(_motion, "运控");
            DisconnectChild(_lineLaser, "线激光");
            DisconnectChild(_monitorCam, "监控相机");
        }

        /// <summary>子设备是否已连接。</summary>
        /// <param name="child">子设备状态对象</param>
        /// <returns>子设备处于 Connected 返回 true</returns>
        private bool IsChildConnected(DeviceStateBase child)
        {
            return child.State == SubDeviceState.Connected;
        }

        /// <summary>等待子设备连接出结果（有界等待，出结果即退出）。</summary>
        /// <remarks>已确认失败与成功一样立即结束，只有始终无反馈才等满超时——避免出现「失败已返回却仍空等到超时」；
        /// 等待循环每拍问询运行态，初始化被抢占（急停/复位/启动）也立即退出。</remarks>
        /// <param name="child">子设备</param>
        /// <param name="name">子设备中文名（日志用）</param>
        /// <returns>Connected 返回 true；已确认失败、被抢占或超时未反馈返回 false</returns>
        private bool WaitChildConnected(DeviceStateBase child, string name)
        {
            var deadline = DateTime.Now.AddMilliseconds(CascadeConnectTimeoutMs);
            while (!child.ConnectSettled && DateTime.Now < deadline && !IsInitPreempted())
                Thread.Sleep(WaitChildPollMs);

            if (child.State == SubDeviceState.Connected) return true;

            if (child.ConnectSettled)
                Log(string.Format("{0} 连接失败 {1}", name, child.ConnectResult), MessageLevel.Warning);
            else
                Log(string.Format("{0} 连接等待超时（{1} 毫秒无反馈）", name, CascadeConnectTimeoutMs),
                    MessageLevel.Warning);
            return false;
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
                Log("子设备初始化完成 待复位");
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

        /// <summary>长动作链中止判定：运行态已被抢占则记一次中止日志（范式 M5 状态前置问询）。</summary>
        /// <remarks>复位 / 启动链每步前置问询运行态，被抢占即中止本链，且不再覆盖被抢占后的新状态。</remarks>
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

        /// <summary>初始化链中止判定：初始化期间被急停 / 复位 / 启动抢占即中止。</summary>
        /// <remarks>初始化期间合法运行态只有 NoReset 与 Alarm（互锁未通过）；出现 EStop / Reseting / Running 说明被抢占，后续步骤不再执行。</remarks>
        /// <returns>已中止返回 true</returns>
        private bool IsInitAborted()
        {
            if (!IsInitPreempted()) return false;
            Log("初始化链被中止 运行态已变更为 " + Current, MessageLevel.Warning);
            return true;
        }

        /// <summary>初始化链抢占判定（静默版，供等待循环每拍问询，不打日志）。</summary>
        /// <returns>运行态已变为 EStop / Reseting / Running 返回 true</returns>
        private bool IsInitPreempted()
        {
            MainDeviceStatus now = Current;
            return now == MainDeviceStatus.EStop || now == MainDeviceStatus.Reseting || now == MainDeviceStatus.Running;
        }

        /// <summary>级联子流程复位。</summary>
        /// <remarks>单个流程失败记日志不中断后续，与级联连接容错口径一致；每步前置问询 Reseting，被抢占立即中止。</remarks>
        /// <returns>全部受理返回 true；中途被抢占返回 false</returns>
        private bool CascadeResetProcess()
        {
            var flows = new DeviceWorkflowBase[] { _weldHead, _lineLaser, _monitorCam, _motion, _weldParam };
            var names = new[] { "焊接头", "线激光", "监控相机", "运控", "焊接工艺" };
            bool allAccepted = true;
            for (int i = 0; i < flows.Length; i++)
            {
                // 复位链被抢占：立即中止，中止日志由调用方统一记一次
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

        /// <summary>第 2 步：运动控制器 + CAMBOX 跟踪工作流初始化。</summary>
        /// <remarks>控制器初始化与 CAMBOX 配置已下沉至 <c>MotionControlWorkflow.InitializeOn()</c>（原档 R011）。</remarks>
        private void InitializeMotionControl()
        {
            Log("[2/5] 运动控制器初始化开始");
            if (Motion == null)
            {
                Log("[2/5] 运动管理器未就绪 初始化跳过", MessageLevel.Warning);
                return;
            }

            try
            {
                // 失败由工作流/设备层报错，此处不置 Connected、不重复报错
                if (_motion.InitializeOn())
                {
                    _motion.ConnectOn();
                    Log("[2/5] 运动控制器初始化完成");
                }
            }
            catch (Exception ex)
            {
                Log("[2/5] 运动控制器初始化失败 原因是 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>第 5 步：机器人通讯初始化（RSI/EKI 二选一，走 Comm 提供的方法）。</summary>
        private void InitializeRobotComm()
        {
            CommWork();
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

        /// <summary>开启模拟测试线程。</summary>
        private void SimTestWorkOpen()
        {
            _simWorkClosing = false;
            if (thSimTestWork != null && thSimTestWork.IsAlive) return;
            thSimTestWork = NewWorkThread(SimTestWork, "thSimTestWork");
            GlobalCommData.ShowLog(_simTag, "模拟测试线程已开启", MessageLevel.Info);
        }

        /// <summary>关闭模拟测试线程（只置标志位，由线程体自行退出）。</summary>
        private void SimTestWorkClose()
        {
            if (thSimTestWork == null && !_simWorkClosing) return;
            _simWorkClosing = true;
            GlobalCommData.ShowLog(_simTag, "模拟测试线程已关闭", MessageLevel.Info);
        }

        /// <summary>模拟测试线程主体：按节拍推进模拟 robotX 时钟。</summary>
        private void SimTestWork()
        {
            while (!_simWorkClosing)
            {
                try { SimTick(); }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_simTag, "模拟测试线程异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(SimBeatMs);
            }
        }

        /// <summary>模拟单拍：发布当前模拟 robotX（起点 + 速度 × 流逝时间）。</summary>
        /// <remarks>模拟设计的其余部分（虚拟相机喂数 / 坐标下发运控 / 轨迹注入）待用户修正后补全。</remarks>
        private void SimTick()
        {
            lock (_robotXLock)
            {
                if (!_robotXSimEnabled) return;
                double elapsed = (DateTime.Now - _simStartTime).TotalSeconds;
                _simRobotX = _simStartX + _simSpeed * elapsed;
            }
            // todo 模拟测试设计的其余内容待用户修正后在此补全
        }

        /// <summary>子设备焊前检查命令（原档20260904 §10.3 序号 9：S2 → S3）。</summary>
        private void DoSubPrework()
        {
            CascadeWeldStatus(SubDeviceWeldStatus.PreWork, "子设备焊前检查");
        }

        /// <summary>子设备焊接启动命令（原档 §10.3 序号 10：S3 → S4）。</summary>
        private void DoSubWork()
        {
            CascadeWeldStatus(SubDeviceWeldStatus.Working, "子设备焊接启动");
        }

        /// <summary>子设备停止流程命令（原档 §10.3 序号 11：S4 → S5）。</summary>
        private void DoSubStop()
        {
            CascadeWeldStatus(SubDeviceWeldStatus.Stopping, "子设备停止流程");
        }

        /// <summary>子设备手动停止命令（原档 §10.3 序号 12：S4 → S7）。</summary>
        private void DoSubManualStop()
        {
            CascadeWeldStatus(SubDeviceWeldStatus.ManualStopped, "子设备手动停止");
        }

        /// <summary>子设备报警停止命令（原档 §10.3 序号 13：S4 → S6）。</summary>
        private void DoSubAlarmStop()
        {
            CascadeWeldStatus(SubDeviceWeldStatus.ErrorAborted, "子设备报警停止");
        }

        /// <summary>向 5 个子设备统一下发焊接态迁移请求。</summary>
        /// <remarks>虚拟设备 WeldParamControl 一并参与（原档 §1.2：5 个子设备 = 4 实体 + 1 虚拟）。</remarks>
        /// <param name="target">目标焊接过程态</param>
        /// <param name="reason">迁移原因</param>
        private void CascadeWeldStatus(SubDeviceWeldStatus target, string reason)
        {
            _weldHead.RequestWeldStatus(target, reason);
            _motion.RequestWeldStatus(target, reason);
            _lineLaser.RequestWeldStatus(target, reason);
            _monitorCam.RequestWeldStatus(target, reason);
            _weldParam.RequestWeldStatus(target, reason);
            Log(string.Format("已向 5 个子设备下发焊接态迁移 {0} 原因 {1}", target, reason));
        }

        #endregion

        #region 公共函数

        /// <summary>虚拟 IO 置位（三种触发源统一入口，原档20260904 §2.3）。</summary>
        /// <remarks>硬件按钮 / 机器人命令 / 软件UI 三条路径都调用本方法置位，
        /// 由 <c>DeviceStatusWork</c> 的 thIOWork 与 thEMGWork 统一扫描消费，扫描器不区分触发源。</remarks>
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

        /// <summary>取走虚拟 IO 并清位（脉冲型命令，保证每条命令只执行一次）。</summary>
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
        /// <remarks>
        /// 扫描器唯一执行入口：只负责命中 IO，不关心该 IO 由谁置位、该执行什么。
        /// 调用方须先 <see cref="TakeVirtualIo"/> 取走 IO——IO 在动作开始前即已消除，不存在滞留重复触发。
        /// 除急停（须同步立即生效）外，调用方应在独立动作线程上执行本方法，不得占用 IO 扫描线程。
        /// </remarks>
        /// <param name="ioName">已命中的虚拟 IO 常量</param>
        /// <returns>已分派执行返回 true；IO 名未登记返回 false</returns>
        public bool ExecuteIo(string ioName)
        {
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

                case IoEmg:
                    DoEstopMachine();
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
        /// <remarks>Running 但从未进入 Working 且有子设备从 PreWork 退回待机时，整机回落 Stop，由 IOWork 逐拍调用。</remarks>
        public void CheckPreworkFailed()
        {
            if (Current != MainDeviceStatus.Running) return;
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
                    Log("预检失败 子设备 " + pair.Key + " 退回待机，整机回落停止（不报警，可重新发起）",
                        MessageLevel.Warning);
                    SetStatus(MainDeviceStatus.Stop, "预检失败 退回待机");
                    return;
                }
            }
        }

        #region 查询与参数命令（原档20260904 §10.4 / §10.5）

        /// <summary>查询主设备当前运行态（原档 §10.4 序号 14，只读，不触发状态转换）。</summary>
        /// <returns>当前 MainDeviceStatus</returns>
        public MainDeviceStatus GetMainDeviceStatus()
        {
            return Current;
        }

        /// <summary>查询指定子设备当前焊接状态（原档 §10.4 序号 15，只读）。</summary>
        /// <param name="tag">子设备标识（与各 Workflow 的 Tag 一致）</param>
        /// <returns>命中返回该子设备焊接态；未命中返回 NoReset</returns>
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

        /// <summary>查询全部 5 个子设备焊接状态（原档 §10.4 序号 16，只读）。</summary>
        /// <remarks>5 个子设备 = 4 个实体 + 1 个虚拟（WeldParamControl）。</remarks>
        /// <returns>子设备标识到焊接态的映射</returns>
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

        /// <summary>查询当前报警信息（原档 §10.4 序号 17，M5 报警态下有实质内容）。</summary>
        /// <returns>最近一次报警信息；从未报警返回 HasAlarm=false 的实例</returns>
        public MachineAlarmInfo GetAlarmInfo()
        {
            return _lastAlarm ?? new MachineAlarmInfo { HasAlarm = false };
        }

        /// <summary>查询当前 IO 状态（原档 §10.4 序号 18，调试用）。</summary>
        /// <returns>所有已置位的虚拟 IO 名数组</returns>
        public string[] GetIOState()
        {
            return SnapshotVirtualIo();
        }

        /// <summary>读取当前焊接工艺参数（原档 §10.5 序号 20，只读）。</summary>
        /// <returns>最近一次工艺计算输出</returns>
        public WeldParamOutput GetWeldParam()
        {
            return _weldParam.LastOutput;
        }

        /// <summary>设置焊接工艺参数（原档 §10.5 序号 19）。</summary>
        /// <remarks>前置条件：M3 停止 / S2 Stanby，运行中禁止修改——违反则拒绝并返回 false。</remarks>
        /// <param name="param">目标工艺参数</param>
        /// <returns>写入成功返回 true；运行中或参数为空返回 false</returns>
        public bool SetWeldParam(WeldParamOutput param)
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

        /// <summary>实例化所有子设备工作线程（5 个）并 Start（§7.5）。</summary>
        public void MachineFlowWorkOpen()
        {
            if (_workStarted) return;
            _workStarted = true;

            thLaserHeadWork = NewWorkThread(LaserHeadWork, "thLaserHeadWork");
            thLineLaserWork = NewWorkThread(LineLaserWork, "thLineLaserWork");
            thMonitorCamWork = NewWorkThread(MonitorCamWork, "thMonitorCamWork");
            thMotionControlWork = NewWorkThread(MotionControlWork, "thMotionControlWork");
            thWeldParamControlWork = NewWorkThread(WeldParamControlWork, "thWeldParamControlWork");

            Log("5 条子设备工作线程已开启");
        }

        /// <summary>实例化通讯工作线程（2 个）并 Start（§7.5）。</summary>
        public void MachineCommandWorkOpen()
        {
            if (_commStarted) return;
            _commStarted = true;

            thHeartInspectWork = NewWorkThread(HeartInspectWork, "thHeartInspectWork");
            thCommCommandWork = NewWorkThread(CommCommandWork, "thCommCommandWork");

            Log("机器人心跳检测与指令交互线程已开启");
        }

        /// <summary>关闭整机工作线程（范式 M3：只置标志位，不 Abort 线程）。</summary>
        public void MachineFlowWorkClose()
        {
            IsCloseWork = true;
            _workStarted = false;
            _commStarted = false;
            Log("整机工作线程关闭标志已置位");
        }

        /// <summary>系统初始化命令（原档 §10.2 序号 1，映射 Init_IO）。</summary>
        /// <remarks>只拉起常驻线程并置位 IoInit，连接链在 DoInitializeMachine；线程必须先就绪，否则 Init_IO 无人消费。</remarks>
        /// <returns>线程已拉起且 IO 置位成功返回 true</returns>
        public bool InitializeMachine()
        {
            MachineFlowWorkOpen();
            MachineCommandWorkOpen();
            DeviceStatusWork.Instance.MachineIOWorkOpen();

            SetVirtualIo(IoInit);
            Log("系统初始化命令已置位 Init_IO，等待 Running IO Work 扫描执行");
            return true;
        }

        /// <summary>执行初始化链（由 thIOWork 扫描到 Init_IO 后调用，不对外直接调用）。</summary>
        /// <remarks>原档 §7 阶段 1：连接 4 个实体子设备 + 加载虚拟设备，全部 Connected 才置可运行；
        /// 每步前置问询运行态，被急停 / 复位 / 启动抢占即中止，不再执行后续步骤与聚合判定。</remarks>
        /// <returns>全部子设备 Connected 返回 true；中途被抢占返回 false</returns>
        public bool DoInitializeMachine()
        {
            if (_initializing) return false;
            _initializing = true;
            try
            {
                Log("子设备初始化开始");
                Log("初始化顺序 机器人通讯 焊接头 运动控制器 线激光相机 监控相机");

                // 0. 机器人通讯开启（与子设备同级且优先开启；失败仅报通讯层，继续子设备连接）
                InitializeRobotComm();
                if (IsInitAborted()) return false;

                // 1. 焊接头初始化（激光器握手 + 温度模块枚举 + IO 映射）
                Log(" 焊接头初始化开始");
                if (LaserWeldHeadController.Instance.Initialize())
                {
                    _weldHead.ConnectOn();
                    Log("焊接头初始化完成");
                }
                // 初始化失败由控制器层报错，此处不置 Connected、不重复报错
                if (IsInitAborted()) return false;

                // 2. 运动控制器初始化 + CAMBOX 跟踪工作流初始化
                InitializeMotionControl();
                if (IsInitAborted()) return false;

                // 3. 线激光相机连接（受理式 + 等待 Connected）
                Log("线激光相机初始化开始");
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
                if (IsInitAborted()) return false;

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
                if (IsInitAborted()) return false;

                // 聚合判定：全部就绪保持 NoReset 等待复位（只有复位流程成功才到 Stop）；
                // 失败置 Alarm（消除报警后回 NoReset），并通知 UI 初始化结束
                bool allReady = EvaluateInitializationResult();
                var initHandler = InitializationCompleted;
                if (initHandler != null) initHandler(allReady);
                return allReady;
            }
            finally
            {
                _initializing = false;
            }
        }

        /// <summary>执行所有子设备 ClearStatus 并强制设备状态 NoReset（§7.5）。</summary>
        /// <remarks>初始化默认加载、复位前强制加载。</remarks>
        public void ClearMachine()
        {
            CascadeClearStatus();
            IsWorking = false;
            IsAlarmstop = false;
            SetStatus(MainDeviceStatus.NoReset, "清状态并强制未复位");
            Log("整机清状态完成（强制未复位）");
        }

        /// <summary>设备复位命令（原档20260904 §10.2 序号 2，映射 Reset_IO）。</summary>
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
            Log("设备复位命令已置位 Reset_IO，等待 Running IO Work 扫描执行");
            return true;
        }

        /// <summary>执行复位链（由 thIOWork 扫描到 Reset_IO 后调用）。</summary>
        /// <remarks>范式 M4/M5：一次性 Task + 每步前置问询状态——急停态直接拒绝复位，
        /// 清状态后校验仍为 NoReset、级联复位每步校验仍为 Reseting，任一步被抢占即中止且不覆盖新状态。</remarks>
        /// <returns>受理返回 true；急停态或机器人在位检测不通过返回 false</returns>
        public bool DoResetMachine()
        {
            if (Current == MainDeviceStatus.EStop)
            {
                Log("急停态禁止复位 请先解除急停", MessageLevel.Warning);
                return false;
            }

            if (!IsRobotReachable())
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

        /// <summary>设备启动命令（原档20260904 §10.2 序号 3，映射 Start_IO）。</summary>
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
            Log("设备启动命令已置位 Start_IO，等待 Running IO Work 扫描执行");
            return true;
        }

        /// <summary>执行启动链（由 thIOWork 扫描到 Start_IO 后调用，范式 M6）。</summary>
        /// <remarks>真正执行由常驻子线程轮询判断，本方法只切状态并拉起线程；
        /// 置 Running 后仍前置问询一次运行态，被急停 / 报警抢占则中止，不拉起工作线程。</remarks>
        /// <returns>状态切换成功返回 true</returns>
        public bool DoStartMachine()
        {
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

        /// <summary>设备停止命令（原档20260904 §10.2 序号 4，映射 Stop_IO）。</summary>
        /// <remarks>按原档 §2.3② 只置位不执行，执行体见 <see cref="DoStopMachine"/>。</remarks>
        public void StopMachine()
        {
            SetVirtualIo(IoStop);
            Log("设备停止命令已置位 Stop_IO，等待 Running IO Work 扫描执行");
        }

        /// <summary>执行停止链（thIOWork 扫描到 Stop_IO 后调用）。</summary>
        /// <remarks>不关闭 thIOWork（常驻扫描器停机后仍需接收命令），仅在 Dispose 时统一关闭。</remarks>
        public void DoStopMachine()
        {
            IsWorking = false;
            MachineFlowWorkClose();
            CascadeClearStatus();
            CascadeDisconnect();
            if (Current == MainDeviceStatus.Running)
                SetStatus(MainDeviceStatus.Stop, "机器停止");
            Log("主设备运行管控停止");
        }

        /// <summary>消除报警命令（原档20260904 §10.2 序号 5，映射 ClearAlarm_IO）。</summary>
        /// <remarks>按原档 §2.3② 只置位不执行，执行体见 <see cref="DoClearAlarm"/>。</remarks>
        /// <returns>受理返回 true；当前非报警态返回 false</returns>
        public bool ClearAlarm()
        {
            if (Current == MainDeviceStatus.EStop) return false;
            if (Current != MainDeviceStatus.Alarm) return false;

            SetVirtualIo(IoClearAlarm);
            Log("消除报警命令已置位 ClearAlarm_IO，等待 Running IO Work 扫描执行");
            return true;
        }

        /// <summary>执行消除报警（thIOWork 扫描到 ClearAlarm_IO 后调用）。</summary>
        /// <remarks>用户 2026-09-04 裁定（关闭 OPEN-ClearAlarmSemantics）：消除报警一律回 NoReset，只有复位流程成功才到 Stop。</remarks>
        /// <returns>已清除返回 true；当前非报警态返回 false</returns>
        public bool DoClearAlarm()
        {
            if (Current != MainDeviceStatus.Alarm) return false;

            IsAlarmstop = false;
            SetStatus(MainDeviceStatus.NoReset, "报警已清除 待复位");
            return true;
        }

        /// <summary>急停命令（原档20260904 §10.2 序号 6，映射 EMG_IO）。</summary>
        /// <remarks>用户 2026-09-04 裁定：所有 IO 工作一致，急停同样置位后由 thEMGWork 扫描执行。</remarks>
        /// <param name="reason">急停原因</param>
        public void EstopMachine(string reason)
        {
            _emgReason = reason;
            SetVirtualIo(IoEmg);
        }

        /// <summary>执行急停（由 thEMGWork 扫描到 EMG_IO 或物理急停位后调用）。</summary>
        public void DoEstopMachine()
        {
            string reason = string.IsNullOrEmpty(_emgReason) ? "急停信号触发" : _emgReason;

            IsAlarmstop = true;
            IsWorking = false;
            SetStatus(MainDeviceStatus.EStop, reason);
            EmergencyStop();
            CascadeDisconnect();
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
            SetStatus(MainDeviceStatus.Alarm, reason);
        }

        /// <summary>安全互锁恢复上报（由 DeviceStatusWork 调用）：转未复位。</summary>
        public void ReportAlarmCleared()
        {
            if (Current == MainDeviceStatus.Alarm)
            {
                _lastAlarm = null;
                SetStatus(MainDeviceStatus.NoReset, "安全互锁恢复 待复位");
            }
        }

        // ---- 模拟模式（用户裁定 2026-09-04：模拟测试以后台线程承载，其余设计待修正） ----

        /// <summary>开启或关闭模拟模式。</summary>
        /// <remarks>开启时设置模式、选相机、套用虚拟相机轮廓/随机、启动共享模拟 robotX 时钟并拉起模拟测试线程；传入 None 视为关闭。</remarks>
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

            // 拉起模拟测试后台线程（用户裁定 2026-09-04）
            SimTestWorkOpen();

            // 持久化"上次选择的模式"，使重启后下拉框显示本次选择
            SaveSimulation();

            GlobalCommData.ShowLog(_simTag, string.Format(
                "模拟模式开启 模式 {0} 起点X {1:F1} 速度 {2:F1} 目标 {3:F1}",
                mode, SimStartX, SimSpeed, SimTargetDistance), MessageLevel.Info);
        }

        /// <summary>关闭模拟模式：复位模式、停止共享模拟 robotX 时钟与模拟测试线程</summary>
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
            SimTestWorkClose();
            GlobalCommData.ShowLog(_simTag, "模拟模式关闭", MessageLevel.Info);
        }

        /// <summary>启动共享模拟 robotX 时钟：robotX = 起点 + 速度 × 流逝时间</summary>
        public void StartRobotXSimulation()
        {
            lock (_robotXLock)
            {
                _simStartX = _simStartXParam;
                _simSpeed = _simSpeedParam;
                _simRobotX = _simStartXParam;
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

        /// <summary>获取当前有效机器人 X 坐标（模拟开启时为模拟值）。</summary>
        /// <remarks>线激光与运控流程统一经此方法读取，保证模拟坐标一致。</remarks>
        /// <returns>当前有效模拟 X 坐标</returns>
        public double GetEffectiveRobotX()
        {
            lock (_robotXLock)
            {
                if (_robotXSimEnabled) return _simRobotX;
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

        /// <summary>释放：停机、关模拟线程并退订通讯事件。</summary>
        public void Dispose()
        {
            DisposeComponents();
            GC.SuppressFinalize(this);
        }

        /// <summary>释放托管资源（ADR-001：非 UI 类走 Dispose + DisposeComponents，不得重写 Dispose(bool)）。</summary>
        private void DisposeComponents()
        {
            StopMachine();
            // Running IO Work 是常驻扫描器（原档20260904 §2），DoStopMachine 不关闭它，仅在此统一释放
            DeviceStatusWork.Instance.MachineIOWorkClose();
            ClearAllVirtualIo();
            SimTestWorkClose();
            GlobalCommData.CommunicationCommandReceived -= OnCommRecived;
            if (RobotManager != null) RobotManager.DataReceived -= OnRobotDataReceived;
        }

        #endregion
    }

    /// <summary>运控调整模拟测试模式（归并自 SimulationWorkflow）</summary>
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
