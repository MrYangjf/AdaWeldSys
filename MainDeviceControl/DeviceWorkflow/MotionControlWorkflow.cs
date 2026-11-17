﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.WeldParamControl.Algorithms;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ProductFileControl;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{

    /// <summary>运动控制工作流（单例）。</summary>
    /// <remarks>负责台达总线的初始化、伺服使能编排与急停；焊缝跟踪方案待定。</remarks>
    public class MotionControlWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 步号：0 待机 900 收尾；10 启动 11 使能 12 回归；20 待机
        private const int StepServoOn = 11;
        private const int StepReturnToCal = 12;
        private const int StepReady = 20;
        private const int StepStopping = 700;

        // 单步超时阈值（秒）
        private const int ServoOnTimeoutSeconds = 10;
        // 回归：先等停再判偏差，未到位重发，超限即失败
        private const int CalReturnTimeoutSeconds = 15;

        // 回归重试上限：不到位即重发，超过即报警
        private const int CalReturnMaxRetry = 3;

        // 软目标到达窗口次数，仅作留痕，主判据仍是偏差
        private const ushort SoftTargetReachWindowTicks = 50;

        // 收尾等停稳上限，超时按现状继续不阻塞
        private const int StoppingSettleSeconds = 5;
        // 标定位回归的到位容差（毫米）
        private const double CalReturnToleranceMm = 0.02;

        // 焊缝 Y 记录等待生产会话基名的告警限频（毫秒）
        private const int SeamTraceYWaitLogIntervalMs = 2000;

        // 跟随时间常数；禁位置贴齐与按剩余距离限速
        private const double CspFollowTauSeconds = 0.150;
        private const int CspErrorLogIntervalMs = 1000;
        private const uint HighResolutionTimerPeriodMs = 1;
        private const double CspMinTickSeconds = 0.001;
        // 前馈：X 速度 EMA 系数 / 遥测毛刺钳位上限
        private const double FeedforwardVxAlpha = 0.2;
        private const double FeedforwardVxSaneLimitMmPerS = 500.0;

        /// <summary>查图 X 取数开关：真取焊参值，假走旧取数。</summary>
        /// <remarks>查图与入图须同源，旧取数在真实模式恒 0。</remarks>
        private static readonly bool _cspQueryXFromWeldParam = true;
        // 段速时基上限，过小高估段速，段末冲过端点
        private const double CspSegSecondsCap = 0.25;

        /// <summary>写段时间门限（秒）：超时且攒够 1 个整脉冲即强写。</summary>
        /// <remarks>慢速时会超时基上限，段速高估致段末干等。</remarks>
        private const double CspWriteIntervalMaxSeconds = 0.15;

        /// <summary>段距写入门限（毫米）：段时长须 ≥ 驱动器 S 曲线时间。</summary>
        /// <remarks>取 0.01mm；早期「门限致停走」实因时基截断高估段速。</remarks>
        private const double CspMinWriteSegMm = 0.01;

        /// <summary>几何钳位上带系数：段速不得高于本段几何均速。</summary>
        /// <remarks>快于墙钟间隔必然抽空 FIFO，轴冲过端点再被拉回。</remarks>
        private const double CspVelGeomHiFactor = 1.0;

        /// <summary>几何钳位下带系数：段速不低于本段几何均速 0.85 倍。</summary>
        /// <remarks>过慢会让队列长期积压、滞后持续累积；0.85 仍保留段间平滑余量。</remarks>
        private const double CspVelGeomLoFactor = 0.85;

        /// <summary>队列积压反馈增益（1/秒）：段速增量 = 增益 ×（积压 − 死区）。</summary>
        /// <remarks>轴落后多为队列积压而非跟不上；比例项随积压趋零自限，不会重蹈「恒定提速 ⇒ FIFO 抽空」。</remarks>
        private const double CspBacklogVelGainPerS = 0.5;

        /// <summary>积压反馈增量上限（毫米/秒）：防起步阶跃拉高速。</summary>
        private const double CspBacklogVelCapMmPerS = 0.5;

        /// <summary>积压反馈死区（毫米）：低于此值不提速，避开抖动。</summary>
        private const double CspBacklogDeadbandMm = 0.02;
        private const double CspMaxTickSeconds = 0.1;
        // 首次启动预载段数，过多会累积滞后
        private const int BufferPreloadSegments = 3;
        // 静止拍数达此值判队列耗干，误判则轴停走
        private const int BufferIdleTicksToDrain = 60;
        // 示教值与轴起点偏差超此值告警，防标定对失效
        private const double CspTeachDeviationWarnMm = 5.0;
        // 当量缺省值（毫米/脉冲）：无轴或 Equiv 非法时兜底
        private const double DefaultAxisEquivMm = 0.001;
        // 停止模式 0 保持 1 平滑 2 急停；收尾必用 1
        private const ushort BufferStopModeSmooth = 1;
        // [DEBUG-REMOVE] 现场验证期 Debug 行限频（毫秒）：热路径 10ms，500ms 即每 50 拍一条
        private const int CspDebugLogIntervalMs = 500;
        // 缓冲诊断日志限频：原值仅作证据，不参与判据
        private const int CspBufferDiagIntervalMs = 5000;
        // 启动连续失败上限：恒返回 4352 须计数收敛
        private const int BufferStartMaxFailures = 3;
        // 行程限幅告警阈值（毫米）：超过即偏差失控，须暴露
        private const double CspStrokeClampWarnMm = 1.0;
        // 限幅告警限频：每拍可能命中，只按间隔报一次
        private const int CspStrokeClampWarnIntervalMs = 2000;
        // 失步判据：写段后轴位移不足即未跟随
        private const double CspAxisFollowEpsilonMm = 0.0005;
        // 连续未跟随拍数达此值判驱动停执行
        private const int CspAxisStallTicksToReset = 30;
        // 失步告警限频：重启仍会立刻再判，按间隔报一次
        private const int CspAxisStallWarnIntervalMs = 2000;

        #endregion

        #region 私有变量

        /// <summary>运控单例，进程内唯一不重建</summary>
        private static MontionManager Manager { get { return MontionManager.Instance; } }

        /// <summary>是否已完成初始化（区分未连接就绪与就绪）</summary>
        private volatile bool _initialized;

        // ---- 大步内小步编号（发指令一步、等完成一步）----
        // 小步 11/12 归大步 10（伺服使能）；小步 21/22 归大步 10 的回归段（大步 12=StepReturnToCal）。
        // 小步分支是热路径，禁写 log；仅发令/完成拍写 log

        /// <summary>小步 11：下发伺服使能指令。</summary>
        private const int SubServoIssue = 11;

        /// <summary>小步 12：按拍轮询使能完成变量。</summary>
        private const int SubServoWait = 12;

        /// <summary>小步 21：判位与下发回归指令。</summary>
        private const int SubCalCheck = 21;

        /// <summary>小步 22：按拍等轴运动停止（CSP 模式取驱动器 Mdone；超时未停即失败报警）。</summary>
        private const int SubCalWait = 22;

        /// <summary>小步 23：停止后判到位偏差（≤ CalReturnToleranceMm 才完成；未到位重试回小步 21）。</summary>
        private const int SubCalVerify = 23;

        /// <summary>伺服使能小步状态（SubServoIssue/SubServoWait）。</summary>
        private int _servoSubStep;

        /// <summary>标定位回归小步状态（SubCalCheck / SubCalWait / SubCalVerify）。</summary>
        private int _calSubStep;

        /// <summary>回归本轮已重试次数（超 CalReturnMaxRetry 即报警）。</summary>
        /// <remarks>Y 轴为 CSP 模式：发令 → 等停 → 判偏差，未到位重发；
        /// 在小步 11 复位，每次启动均为全新计数。</remarks>
        private int _calRetryCount;

        /// <summary>等停止计时起点（每下发移动重置，供小步 22 判超时）。</summary>
        private DateTime _calStopWaitStart;

        /// <summary>本次下发是否已确认启动（小步 22 两段判前半）。</summary>
        /// <remarks>指令刚下发时 Mdone 仍是上次已完成态。
        /// ⇒ 直接判已停会跳判重发。故先等运动中再等已完成。</remarks>
        private bool _calMotionStarted;

        /// <summary>跟踪轴是否已停在标定位（供观测与起焊防呆）。</summary>
        private bool _calReturnReady;

        /// <summary>停止收尾是否已进入（首入只停输出）。</summary>
        /// <remarks>靠本标志保证停输出只做一次。
        /// ⛔ StepStopping 未完成时会按拍重入。</remarks>
        private bool _stopTeardownStarted;

        /// <summary>失败收尾是否已进入：避免重入重复停输出。</summary>
        private bool _failTeardownStarted;

        /// <summary>焊缝 Y 记录是否已随本场 Ready 开启（会话复位清零）。</summary>
        private bool _seamTraceYReadyStarted;

        /// <summary>焊缝 Y 记录等基名的限频留痕时刻（避免刷屏）。</summary>
        private DateTime _seamTraceYWaitLogTime;

        private readonly object _lock = new object();

        // 失败收尾记录（子类私有字段）
        private string _failReason = "";
        private int _failStep;

        /// <summary>焊缝跟踪轴，读取轴位置供记录与观测。</summary>
        private DeltaMontionAxis _seamAxis;

        /// <summary>模拟记录的虚拟轴位置：无控制器时代替轴反馈。</summary>
        private double _simAxisPos;

        /// <summary>跟踪 PVT 点缓冲区（算法产出到控制器写入）。</summary>
        private readonly List<PVTPoint> _seamBuffer = new List<PVTPoint>();

        /// <summary>缓冲区锁。</summary>
        private readonly object _seamBufferLock = new object();

        /// <summary>焊缝跟踪是否已下发起始段。</summary>
        private bool _seamRunning;

        /// <summary>最近一次 PVT 下发返回码。</summary>
        private ushort _seamLastErrorCode;

        /// <summary>最近一次 PVT 下发时间。</summary>
        private DateTime _seamLastOutputTime;

        /// <summary>跟踪激活标志（上周期态，供起止沿判，跨线程读）。</summary>
        private volatile bool _seamTrackingActive;

        /// <summary>起焊沿触发次数（取证）：正常只应为 1。
        /// 反复触发即「手动固定宽度仍变动」的嫌疑源。</summary>
        private int _seamTrackingStartCount;

        /// <summary>CSP 输出节拍基准时刻（Stopwatch 计数，用于实测两次写入间隔）。</summary>
        private long _cspLastTickTimestamp;

        /// <summary>上一拍机器人 X（速度前馈差分基准，NaN=未初始化）。</summary>
        private double _ffRobotXPrev = double.NaN;

        /// <summary>上一拍差分时刻（Stopwatch 计数）。</summary>
        private long _ffRobotXTimePrev;

        /// <summary>机器人 X 向速度 EMA 平滑值（毫米/秒幅值）。</summary>
        private double _ffVxFiltered;

        /// <summary>本拍速度前馈需求（毫米/秒，已乘安全系数；诊断观测）。</summary>
        private double _cspFeedforwardVelMmPerS;

        /// <summary>段速度 EMA 平滑值（毫米/秒）：端点平滑必须配速度平滑。</summary>
        private double _cspVelFiltered;

        /// <summary>本拍写入的段速度（毫米/秒）：留痕用，不参与判据。</summary>
        private double _cspLastSegVelMmPerS;

        /// <summary>本拍几何均速（段距 ÷ 实隔，毫米/秒）：留痕用，比对断供。</summary>
        private double _cspLastGeomVelMmPerS;

        /// <summary>本拍段时基（秒）：留痕用，判断是否高估段速。</summary>
        private double _cspLastSegSeconds;

        /// <summary>本拍积压（毫米，末段端点 − 轴反馈）：反馈输入。</summary>
        private double _cspLastBacklogMm;

        /// <summary>本拍积压提速量（毫米/秒）：留痕用，判是否被钳位。</summary>
        private double _cspLastBacklogBoostMmPerS;

        /// <summary>缓冲重启次数（失步或耗干）：取证用，重启打断段流。</summary>
        private int _cspRestartCount;

        /// <summary>累计写段数（跨耗干重启累计；_cspQueuedSegments 语义是启动阈值计数，会清零，不可作健康度指标）。</summary>
        private long _cspQueuedSegmentsTotal;

        /// <summary>缓冲收尾连续失败场数（成功清零；达 3 场告警）。</summary>
        private int _cspBufferCloseFailStreak;

        /// <summary>上次写段时刻（Stopwatch）：段速按实测间隔折算。</summary>
        private long _cspLastSegTimestamp;

        /// <summary>写段占空比统计：累计输出拍数（随诊断日志清零）。</summary>
        private int _cspTickCount;

        /// <summary>写段占空比统计：累计写段数（随诊断日志清零）。</summary>
        private int _cspWriteCount;

        /// <summary>CSP 输出是否已启用（起焊沿置位，停焊沿复位）。</summary>
        private bool _cspOutputRunning;

        /// <summary>CSP 平滑速度（毫米/秒），跨拍保持以限加速度。</summary>
        private double _cspSmoothedVelocity;

        /// <summary>跟随器内部位置（毫米）：每拍独立推进，与末段解耦。</summary>
        /// <remarks>以末段为基准会丢弃未写拍位移。
        /// 故须自持位置量。</remarks>
        private double _cspFollowPos;

        /// <summary>缓冲是否已 Start 且未判耗尽。</summary>
        private bool _cspBufferRunning;

        /// <summary>缓冲是否已 Start 过（首启预载，断流重启写 1 段）。</summary>
        private bool _cspBufferStartedOnce;

        /// <summary>自上次 Start 起已写入缓冲的段数。</summary>
        private int _cspQueuedSegments;

        /// <summary>队列末端指令位置（毫米）：限幅器以它为基准续算。</summary>
        private double _cspLastQueuedPos;

        /// <summary>缓冲诊断日志时间（原值仅作证据，不参与判据）。</summary>
        private DateTime _cspBufferDiagLogTime;

        /// <summary>[DEBUG-REMOVE] 最近一次闭环 Debug 行时间（验证期限频输出中间量）。</summary>
        private DateTime _cspLastDebugLogTime;

        /// <summary>连续静止拍数，达阈值即判队列耗干。</summary>
        private int _cspIdleTicks;

        /// <summary>最近一次 CSP 目标写入返回码。</summary>
        private ushort _cspLastErrorCode;

        /// <summary>是否启用模拟模式（仅使能并驱动 Y 轴）。</summary>
        private bool IsSimulation { get { return DeviceControlWork.Instance.IsSimulationEnabled; } }

        /// <summary>焊缝跟踪轴名（取自运控配置，消除跨模块读取）。</summary>
        private string SeamTrackAxisName { get { return Manager.MoveData.MotionConfig.SeamTrackAxisName; } }

        /// <summary>焊缝跟踪轴是否已装配（无跟踪轴即纯记录模式）。</summary>
        private bool HasSeamTrackAxis { get { return Manager.GetAxis(SeamTrackAxisName) != null; } }

        /// <summary>跟踪是否处于焊接工作态（线激光已 Working）。</summary>
        private bool IsSeamWelding { get { return LineLaserWorkflow.Instance.WeldStatus == SubDeviceWeldStatus.Working; } }

        /// <summary>是否 CSP 实时目标模式（false 即 PVT 模式）。</summary>
        private bool IsCspMode { get { return WeldParamManager.Instance.IsCspTrackingMode; } }

        /// <summary>是否模拟记录模式：不驱动实轴但仍须采样记录。</summary>
        private bool IsSimRecordOnly
        {
            get { return IsSimulation && !DeviceControlWork.Instance.ShouldMoveAxis; }
        }

        /// <summary>Y 轴已使能（L1 前提）。</summary>
        public override bool IsEnabled { get { return Manager != null && Manager.YReady; } }

        /// <summary>Y 轴位置可信（绝对编码器，免回零）。</summary>
        public override bool IsHomed { get { return IsEnabled && HasSeamTrackAxis; } }

        /// <summary>Y 轴已在标定位（L4）。</summary>
        public override bool IsAtWorkZero
        {
            get
            {
                if (!IsHomed) return false;
                double target = WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm;
                return Math.Abs(SeamAxisPosMm - target) <= CalReturnToleranceMm;
            }
        }

        /// <summary>Y 轴具备绝对编码器，免回零。</summary>
        protected override bool HasAbsoluteEncoder { get { return true; } }

        /// <summary>工作零位来源：标定位即工作零位。</summary>
        public override string WorkZeroSource { get { return "TeachLockYMm"; } }

        /// <summary>运控门槛：寻位 L3；自动焊接另判 L4。</summary>
        public override ReadinessStage MinReadinessStage { get { return ReadinessStage.Homed; } }

        /// <summary>Y 轴不可用原因（就绪返回空串）。</summary>
        public override string ReadinessReason
        {
            get { return Manager == null ? "运动管理器未就绪" : Manager.YGateReason; }
        }

        /// <summary>闭环基准位置（毫米）：有轴取反馈，无轴取虚拟轴。</summary>
        /// <remarks>无轴时以虚拟轴顶替测量时刻轴值。
        /// 使建图与反算同构。</remarks>
        private double SeamAxisPosMm
        {
            get { return _seamAxis != null ? _seamAxis.GetPosition() : _simAxisPos; }
        }

        /// <summary>最近一次 CSP 错误日志时间（热路径日志限频）。</summary>
        private DateTime _cspLastErrorLogTime;

        /// <summary>缓冲启动连续失败次数（达上限即终止闭环，见 BufferStartMaxFailures）。</summary>
        private int _cspBufferStartFailCount;

        /// <summary>最近一次行程限幅告警时间（热路径限频）。</summary>
        private DateTime _cspLastClampWarnTime;

        /// <summary>上一拍轴反馈位置（毫米）：判失步的唯一依据。</summary>
        private double _cspLastAxisFeedbackPos;

        /// <summary>连续未跟随拍数（达阈值即判驱动已停）。</summary>
        private int _cspAxisStallTicks;

        /// <summary>最近一次缓冲执行失步告警时间（热路径限频）。</summary>
        private DateTime _cspLastStallWarnTime;

        /// <summary>折返报警待处置标志（RSI 线程置位，流程线程收尾）。</summary>
        private volatile bool _seamAlarmPending;

        /// <summary>缺值期保持目标是否已建立。</summary>
        private bool _cspHasHeldTarget;

        /// <summary>缺值期保持的绝对 Y 目标（毫米，禁止回落标定位）。</summary>
        private double _cspHeldTargetY;

        /// <summary>缺值报警本次焊段是否已下发（一次性）。</summary>
        private bool _noValueAlarmRaised;

        /// <summary>TA break 锁存位置（mm）：成功时锁存，供页面只读展示。</summary>
        private double _taBreakHoldMm;

        /// <summary>是否已发生过一次成功的 TA break（false 时 UI 显示未触发）。</summary>
        private bool _taBreakHoldValid;

        /// <summary>TA break 锁存位的读写锁（工作流线程写、UI 线程读）。</summary>
        private readonly object _taLock = new object();

        /// <summary>TA_ZeroPose 四态：UI 上色唯一来源，未执行不介入</summary>
        private volatile bool _taZeroPoseRunning;
        private volatile bool _taZeroPoseAttempted;
        private volatile bool _taZeroPoseFailed;
        private volatile bool _taZeroPoseDone;

        /// <summary>力矩换算依据是否已提示（每进程一次，免刷屏）</summary>
        private bool _taForceScaleBasisLogged;

        /// <summary>多媒体定时器精度是否已提升（Windows 默认调度粒度 15.6ms）。</summary>
        private bool _highResolutionTimerRaised;

        #endregion

        #region 单例

        private static readonly Lazy<MotionControlWorkflow> _lazyInstance =
            new Lazy<MotionControlWorkflow>(() => new MotionControlWorkflow());

        /// <summary>运动控制工作流单例。</summary>
        public static MotionControlWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        /// <summary>设备名称（日志与事件标识）。</summary>
        public override string StateName { get { return Tag; } }

        /// <summary>是否处于已使能待机。</summary>
        /// <remarks>判**实际使能信号**（<see cref="MontionAxis.IsServoOn"/>），Standby 下 Ready 与 Idle 同形。
        /// 对外状态只允许一个来源。</remarks>
        public bool IsRunning
        {
            get
            {
                if (WeldStatus != SubDeviceWeldStatus.Standby) return false;
                MontionAxis traceAxis = Manager.GetAxis(SeamTrackAxisName);
                return traceAxis != null && traceAxis.IsServoOn();
            }
        }

        /// <summary>是否已初始化</summary>
        public bool IsInitialized { get { return _initialized; } }

        /// <summary>跟踪轴是否已停在标定位（供观测与起焊防呆）。</summary>
        public bool IsCalibrationYReady { get { return _calReturnReady; } }

        /// <summary>模组配置文件名（不含扩展名），落在本流程目录下。</summary>
        protected override string ConfigFileName { get { return "MotionControlWorkflow"; } }

        /// <summary>配置落 MotionControl\Workflow_XML 子目录。</summary>
        protected override string ConfigRelativeDir { get { return @"Config\MotionControl\Workflow_XML"; } }

        #endregion

        #region 构造函数

        private MotionControlWorkflow()
        {
            Tag = "运动控制工作流";
            _seamLastOutputTime = DateTime.Now;
            _cspLastErrorLogTime = DateTime.Now;
            _servoSubStep = SubServoIssue;
            _calSubStep = SubCalCheck;
            WeldParamManager.Instance.SeamTrackingAlarmRequested += OnSeamTrackingAlarmRequested;
        }

        #endregion

        #region 私有函数


        /// <summary>模拟态回到待机并清报警与失败原因</summary>
        /// <param name="reason">相位变更原因（纯文本，无符号）</param>
        private void ResetToIdle(string reason)
        {
            IsAlarm = false;
            _failReason = "";
            // 回待机不进入使能段，与其他回待机路径一致
            EnterWeldStatus(SubDeviceWeldStatus.Standby, reason, false);
        }

        /// <summary>切换焊接过程态，并按目标态把执行步推到对应</summary>
        /// <remarks>入口。</remarks>
        /// <param name="status">目标焊接过程态（基类抽象状态）</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        /// <param name="enterServoStep">是否进入使能段（大步 10：11 使能 → 12 等 → 21 判位 → 22 等回归）</param>
        private void EnterWeldStatus(SubDeviceWeldStatus status, string reason, bool enterServoStep)
        {
            SetWeldStatus(status, reason);
            if (enterServoStep)
            {
                _servoSubStep = SubServoIssue;
                _calSubStep = SubCalCheck;
                _seamTraceYReadyStarted = false;   // 新会话：记录须重新等 Ready 通过才开
                AdvanceStep(StepServoOn);
                return;
            }
            if (status == SubDeviceWeldStatus.Stopping)
            {
                AdvanceStep(StepStopping);
                return;
            }
            AdvanceStep(StepIdle);
        }

        /// <summary>判断当前步停留是否超过指定秒数。</summary>
        /// <param name="seconds">超时阈值（秒）</param>
        /// <returns>超过阈值返回 true</returns>
        private bool IsTimeout(int seconds)
        {
            return StepWorkTime.TotalSeconds > seconds;
        }

        /// <summary>判断是否所有轴均已使能。</summary>
        /// <returns>全部使能返回 true</returns>
        private static bool IsAllServoOn()
        {
            foreach (MontionAxis axis in Manager.MontionControl.GetAxes())
            {
                if (!axis.IsServoOn()) return false;
            }
            return true;
        }

        /// <summary>执行步 10 小步流（11 发指令 → 12 等完成）：全部轴伺服使能。</summary>
        /// <remarks>小步 12 判定是热路径，静默，仅完成与超时拍写 log。</remarks>
        /// <returns>全部使能返回 true</returns>
        private bool DoServoOn()
        {
            switch (_servoSubStep)
            {
                case SubServoWait:
                {
                    // 小步 12：轮询使能完成变量（false→true 跳步，判断点静默）
                    bool on = IsSimulation
                        ? Manager.GetAxis(SeamTrackAxisName) == null
                          || Manager.GetAxis(SeamTrackAxisName).IsServoOn()
                        : IsAllServoOn();
                    if (on)
                    {
                        // 使能完成 → 回归标定位（标定对成立的前提）
                        // ⛔ 此处不得清缓冲：本会话从未建系，清必报 4352
                        // （ERR_ECAT_NEED_INITIAL），且当时并无残留可清；缓冲清理只在「已建系且轴已停稳」时做。
                        _calSubStep = SubCalCheck;
                        _calReturnReady = false;
                        Log(IsSimulation ? "模拟模式 Y 向轴伺服使能完成" : "全部轴伺服使能完成");
                        _servoSubStep = SubServoIssue;   // 复位供下一会话
                        return true;
                    }
                    if (IsTimeout(ServoOnTimeoutSeconds)) Fail("伺服使能超时");
                    return false;
                }

                case SubServoIssue:
                default:
                {
                    // 小步 11：下发使能指令（模拟仅使能 Y 轴），发令拍写 log
                    // ⚠️ 本流程只管 Y 向跟踪轴，其他轴不归此处
                    // 全轴操作会打到送丝力矩电机并报错误码 256
                    // ⚠️ 必须先复位报警再使能，否则 ServoOn 被拒
                    // 否则回归指令也不响应；每次启动从零计重试
                    _calRetryCount = 0;
                    if (IsSimulation)
                    {
                        MontionAxis simAxis = Manager.GetAxis(SeamTrackAxisName);
                        if (simAxis != null)
                        {
                            simAxis.ResetAlarm();
                            simAxis.ServoOn();
                        }
                    }
                    else if (_seamAxis != null)
                    {
                        _seamAxis.ResetAlarm();
                        _seamAxis.ServoOn();
                    }
                    Log("下发伺服使能指令");
                    _servoSubStep = SubServoWait;
                    return false;
                }
            }
        }

        /// <summary>执行步 12：21 判位下发 → 22 等停 → 23 判到位。</summary>
        /// <remarks>Y 轴 CSP：先判运动停止，再判到位偏差 ≤ 容差。
        /// 未到位重发，超 3 次或等停超时即报警；已静止且在容差内直转 23。
        /// 全程非阻塞按拍推进（⛔ 禁 while/Sleep）；移动门禁只看行程上下限，越界即 Fail。</remarks>
        /// <returns>到位返回 true</returns>
        private bool DoReturnToCalibration()
        {
            switch (_calSubStep)
            {
                case SubCalWait:
                {
                    // 小步 22：两段判，先等启动再等停止
                    bool moving = _seamAxis.IsMoving();
                    double waitedSec = (DateTime.Now - _calStopWaitStart).TotalSeconds;
                    if (!_calMotionStarted)
                    {
                        if (moving)
                        {
                            _calMotionStarted = true;
                            Log(string.Format("PreWork 焊缝跟踪轴运动已启动 等待停止 当前 {0:F3}mm",
                                _seamAxis.GetPosition()));
                            return false;
                        }
                        // 空动作等不到启动，静止在容差内直转 23
                        double posNow = _seamAxis.GetPosition();
                        if (Math.Abs(WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm - posNow)
                            <= CalReturnToleranceMm)
                        {
                            Log(string.Format(
                                "PreWork 焊缝跟踪轴已静止且在标定位容差内 转到位判定 当前 {0:F3}mm 目标 {1:F3}mm",
                                posNow, WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm));
                            _calSubStep = SubCalVerify;
                            return false;
                        }
                        if (waitedSec > CalReturnTimeoutSeconds)
                        {
                            // 超时未见启动：交 23 按位置判定（未动由重试暴露）
                            Log(string.Format(
                                "PreWork 焊缝跟踪轴未见启动 等待 {0}s 转到位判定 当前 {1:F3}mm 目标 {2:F3}mm",
                                CalReturnTimeoutSeconds, _seamAxis.GetPosition(),
                                WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm), MessageLevel.Warning);
                            _calSubStep = SubCalVerify;
                        }
                        return false;
                    }

                    if (!moving)
                    {
                        Log(string.Format("PreWork 焊缝跟踪轴运动已停止 进入到位判定 当前 {0:F3}mm 用时 {1:F1}s",
                            _seamAxis.GetPosition(), waitedSec));
                        _calSubStep = SubCalVerify;
                        return false;
                    }
                    // 超时未停止即报警（每次下发后重新计时）
                    if (waitedSec > CalReturnTimeoutSeconds)
                    {
                        Fail(string.Format("Y 轴标定位回归运动停止超时 等待 {0}s 仍未停止 当前 {1:F3}mm",
                            CalReturnTimeoutSeconds, _seamAxis.GetPosition()));
                    }
                    return false;
                }

                case SubCalVerify:
                {
                    // 小步 23：停止后判到位偏差（跳步拍写 log，判断点静默）
                    double calY = WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm;
                    double pos = _seamAxis.GetPosition();
                    double dev = Math.Abs(calY - pos);
                    // 软到位信号（手册 9.1.13）：留痕佐证，不参与判据
                    ushort softReach = _seamAxis.GetSoftTargetReachStatus();
                    if (dev <= CalReturnToleranceMm)
                    {
                        _calReturnReady = true;
                        Log(string.Format(
                            "焊缝跟踪轴回归到位 {0:F3}mm 目标 {1:F3}mm 偏差 {2:F4}mm 重试 {3} 次 软到位信号 {4}",
                            pos, calY, dev, _calRetryCount, softReach));
                        return true;
                    }
                    // 未到位：重发移动指令重试，超限即失败报警
                    if (_calRetryCount >= CalReturnMaxRetry)
                    {
                        Fail(string.Format(
                            "Y 轴标定位回归未到位 已重试 {0} 次超限 目标 {1:F3}mm 当前 {2:F3}mm 偏差 {3:F4}mm 容差 {4:F3}mm 软到位信号 {5}",
                            _calRetryCount, calY, pos, dev, CalReturnToleranceMm, softReach));
                        return false;
                    }
                    _calRetryCount++;
                    Log(string.Format(
                        "焊缝跟踪轴回归未到位 重发移动指令 重试 {0}/{1} 目标 {2:F3}mm 当前 {3:F3}mm 偏差 {4:F4}mm 软到位信号 {5}",
                        _calRetryCount, CalReturnMaxRetry, calY, pos, dev, softReach), MessageLevel.Warning);
                    _calSubStep = SubCalCheck;
                    return false;
                }

                case SubCalCheck:
                default:
                {
                    // 小步 21：判位与下发（发指令拍写 log）
                    if (_seamAxis == null)
                    {
                        _calReturnReady = true;
                        return true;
                    }
                    double calY = WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm;
                    double pos = _seamAxis.GetPosition();
                    double minLimit = WeldParamManager.Instance.AlgorithmsConfig.YAxisMinMm;
                    double maxLimit = WeldParamManager.Instance.AlgorithmsConfig.YAxisMaxMm;
                    if (Math.Abs(calY - pos) <= CalReturnToleranceMm)
                    {
                        Log(string.Format("焊缝跟踪轴已在 Y 轴标定位 {0:F3}mm", pos));
                        _calReturnReady = true;
                        return true;
                    }
                    // 移动门禁：回归不看固定距离，只看行程上下限
                    // 目标与当前位置都在行程内即放行，多远都可动
                    // 越界即报警暴露，不静默拒绝移动
                    if (!IsWithinYStroke(calY, minLimit, maxLimit) || !IsWithinYStroke(pos, minLimit, maxLimit))
                    {
                        Fail(string.Format(
                            "Y 轴回归标定位越界 目标 {0:F3}mm 当前 {1:F3}mm 行程 [{2:F3} 到 {3:F3}] 请核 TeachLockYMm 与 Y轴行程上下限",
                            calY, pos, minLimit, maxLimit));
                        return false;
                    }
                    // 下发前配置软目标到达窗口（台达手册 9.1.12 / 9.1.13）
                    ConfigureSoftTargetReachWindow();
                    if (!_seamAxis.AbsMoveNoBlock(calY))
                    {
                        Fail("Y 轴标定位回归下发失败");
                        return false;
                    }
                    Log(string.Format("PreWork 焊缝跟踪轴回归标定位 起点 {0:F3}mm 目标 {1:F3}mm 行程 [{2:F3} 到 {3:F3}] 在限内允许移动 重试 {4} 次",
                        pos, calY, minLimit, maxLimit, _calRetryCount));
                    _calStopWaitStart = DateTime.Now;   // 等运动停止的计时起点（每次下发重置）
                    _calMotionStarted = false;          // 两段判前半段复位：本拍起等「运动已启动」
                    _calSubStep = SubCalWait;
                    return false;
                }
            }
        }

        /// <summary>判断一个位置是否落在 Y 轴行程上下限内（闭区间）。</summary>
        /// <remarks>回归门禁只判行程上下限，限内即可移动。
        /// ⛔ 不设距离阈值。</remarks>
        /// <param name="pos">待判位置（毫米）</param>
        /// <param name="minLimit">Y 轴行程下限（毫米）</param>
        /// <param name="maxLimit">Y 轴行程上限（毫米）</param>
        /// <returns>在限内返回 true</returns>
        private static bool IsWithinYStroke(double pos, double minLimit, double maxLimit)
        {
            return pos >= minLimit && pos <= maxLimit;
        }

        /// <summary>执行步 700：停止收尾（停跟踪输出 → 等轴停稳 → 清缓冲 → Y 向</summary>
        /// <remarks>收尾须清报警并恢复初始态，清报警在下电之后。
        /// ⚠️ 只处理 Y 向跟踪轴，其他轴不得下电或清报警。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoStopping()
        {
            if (!_stopTeardownStarted)
            {
                _stopTeardownStarted = true;
                // 收尾：CSP 停发目标保持，PVT 把剩余点作末段下发
                StopSeamTrackingOutput();
                Log("运控停止收尾 已停跟踪输出 等待轴停稳后清缓冲与下电");
            }

            // 等停稳：减速中清理会报 4359，致下次启动失败
            if (!IsSeamAxisSettled()) return false;

            // 已停稳：此处清缓冲才有效，与启动侧复位双保险
            ResetSeamAxisBufferBeforeMove();

            _calSubStep = SubCalCheck;
            _calReturnReady = false;
            _seamTraceYReadyStarted = false;
            ShutdownSeamAxis();
            _servoSubStep = SubServoIssue;
            _stopTeardownStarted = false;
            Log("运控工作流已停止");
            EnterWeldStatus(SubDeviceWeldStatus.Standby, "已停止", false);
            return true;
        }

        /// <summary>收尾轴处理（按需）：无报警则保持使能。</summary>
        /// <remarks>有报警才清报警并重新使能，无报警保持原态。
        /// ⛔ 不无条件下电：断电需二次使能，且易触发报警。
        /// 全轴操作会波及送丝轴并报 256，故只处理跟踪轴；
        /// 报警判据 = `MontionAxis.IsAlarm()`；无轴直接返回。</remarks>
        private void ShutdownSeamAxis()
        {
            if (_seamAxis == null) return;
            if (!_seamAxis.IsAlarm())
            {
                Log("收尾轴检查 无报警 保持使能态直接回初始状态");
                return;
            }
            Log("收尾轴检查 检出报警 清报警并重新失能再使能", MessageLevel.Warning);
            _seamAxis.ServoOff();
            _seamAxis.ResetAlarm();
            _seamAxis.ServoOn();
        }

        /// <summary>配置 CSP 软目标到达窗口（CSP 族专用到位判定，台达手册 9.1.12 / 9.1.13）。</summary>
        /// <remarks>CSP 从站无 target reached 功能时，用此启用到位信号。
        /// `PositionWindow` 单位 = 脈波數、`WindowTime` 单位 = 次數（手册示例 1ms×100 次=0.1s，超窗计数归零）；9.1.13 读取：0 = 未到達 / 1 = 到達。
        /// ⚠️ 主判据仍是位置偏差，本窗口仅作留痕佐证；未装配跟踪轴直接返回。</remarks>
        private void ConfigureSoftTargetReachWindow()
        {
            if (_seamAxis == null) return;
            uint windowPulse = (uint)Math.Max(1.0, Math.Round(CalReturnToleranceMm / AxisEquivMm));
            if (!_seamAxis.SetSoftTargetReach(windowPulse, SoftTargetReachWindowTicks, true))
            {
                Log("软目标到达窗口配置失败 到位判定将只依赖位置偏差", MessageLevel.Warning);
            }
        }

        /// <summary>收尾清缓冲：清掉本会话已建的缓冲段与坐标系。</summary>
        /// <remarks>① `BufferClear` 报 4359 = `ERR_ECAT_NEED_STOP`（需要先停止）⇒ 清缓冲必须在轴停稳态；
        /// ② 从未建系时清缓冲必报 4352 ⇒ 以已启动过门控。
        /// 调用点只有步 700 / 900 停稳后（⛔ 不在 PreWork 前调用）；无轴直接返回。</remarks>
        private void ResetSeamAxisBufferBeforeMove()
        {
            if (_seamAxis == null) return;
            if (!_cspBufferStartedOnce)
            {
                Log("收尾清缓冲跳过 本会话未启动过缓冲 无残留可清");
                return;
            }
            // Mode 1 停即清 FIFO 并解系，无需再清
            ushort stopRet = _seamAxis.BufferStop(BufferStopModeSmooth);
            if (stopRet == 0)
            {
                _cspBufferStartedOnce = false;
                Log("收尾清缓冲 平滑停止完成 已清 FIFO 并解除坐标系 Mode 1");
            }
            else
            {
                ushort clearRet = _seamAxis.BufferClear();
                Log(string.Format("收尾清缓冲异常 停止 {0} 兜底清空 {1}", stopRet, clearRet),
                    MessageLevel.Warning);
            }
        }

        /// <summary>停止收尾等待判据：跟踪轴已停稳或超时。</summary>
        /// <remarks>收尾用 Mode 1 停轴，须等停稳再下电。
        /// ⛔ 未停稳下电会报警。禁 while/Sleep，未停稳由调用方返回 false 重入。</remarks>
        /// <returns>可继续收尾返回 true</returns>
        private bool IsSeamAxisSettled()
        {
            if (_seamAxis == null) return true;
            if (!_seamAxis.IsMoving()) return true;
            // 未使能轴不会再动：Mdone 可能恒 0，只判会白等到超时
            if (!_seamAxis.IsServoOn()) return true;
            if (!IsTimeout(StoppingSettleSeconds)) return false;
            Log(string.Format("收尾等待轴停稳超时 {0}s 按当前状态继续收尾", StoppingSettleSeconds), MessageLevel.Warning);
            return true;
        }

        /// <summary>跟踪输出收尾：关输出、冲尽缓存、复位跟踪态。</summary>
        /// <remarks>DoStopping（步 700）与模拟态 Stop（直接跳 Idle、绕过 DoStopping）共用。
        /// 不收尾会残留跟踪态，致下次起焊沿被吞。</remarks>
        private void StopSeamTrackingOutput()
        {
            StopCspOutput();
            FlushSeamBuffer(true);
            _seamTrackingActive = false;
            _seamAlarmPending = false;
        }

        /// <summary>执行步 900：失败收尾（停输出 → 等停稳 → 清理）。</summary>
        /// <remarks>异常终止同样须收尾，否则残留致下次启动失败。
        /// 与 DoStopping 同构：先停输出、等停稳再清缓冲与下电。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            if (!_failTeardownStarted)
            {
                _failTeardownStarted = true;
                StopSeamTrackingOutput();
                Log("运控异常收尾 已停跟踪输出 等待轴停稳后清缓冲与下电", MessageLevel.Error);
            }

            if (!IsSeamAxisSettled()) return false;

            ResetSeamAxisBufferBeforeMove();
            _calSubStep = SubCalCheck;
            _calReturnReady = false;
            _seamTraceYReadyStarted = false;
            ShutdownSeamAxis();
            _servoSubStep = SubServoIssue;
            _failTeardownStarted = false;
            // 失败收尾不进入使能段（同初始化失败分支）
            EnterWeldStatus(SubDeviceWeldStatus.ErrorAborted, _failReason, false);
            IsAlarm = true;
            Log(string.Format("运控流程异常终止 已收尾(跟踪输出已停 Y向轴下电并清报警) 原因 {0}", _failReason), MessageLevel.Error);
            return true;
        }

        /// <summary>转入失败收尾。</summary>
        /// <param name="reason">失败原因（纯文本，无符号）</param>
        private void Fail(string reason)
        {
            _failStep = RunStep;
            _failReason = reason;
            AdvanceStep(StepFinishFail);
        }

        #endregion

        #region 公共函数

        /// <summary>启动运控，置已使能待机。</summary>
        /// <remarks>置 Ready 相位后由执行步完成伺服使能。</remarks>
        /// <returns>受理成功返回 true</returns>
        public bool Start()
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    Log("启动失败 原因是未初始化", MessageLevel.Warning);
                    return false;
                }
                EnterWeldStatus(SubDeviceWeldStatus.Standby, "启动指令受理", true);
                return true;
            }
        }

        /// <summary>停止运控。</summary>
        /// <param name="immediate">true 表示立即急停，false 表示按停止段收尾</param>
        public void Stop(bool immediate = false)
        {
            lock (_lock)
            {
                if (immediate) Manager.SetEmergencyStop(true);
                if (WeldStatus != SubDeviceWeldStatus.Standby && !IsRunning) return;
                if (IsAlarm)
                {
                    // 模拟态 Error 直接回待机，否则残留致下次启动被拦截
                    if (IsSimulation) ResetToIdle("模拟停止复位");
                    return;
                }
                EnterWeldStatus(SubDeviceWeldStatus.Stopping, "停止指令受理", false);
                // 模拟态无硬件可平滑停：停止后 Current→Stop 使 FlowProcess 不再驱动，
                // Stopping 将永远无法迁移归位（实测 V3 跟踪卡在 Stopping），直接转 Idle。
                // 但 DoStopping 是唯一复位跟踪态处，跳 Idle 会整段跳过，
                // 残留标志会让起焊沿被吞、缓冲不再重建，
                // 二跑首拍即 0x1100（实测 4352）。
                if (IsSimulation)
                {
                    StopSeamTrackingOutput();
                    EnterWeldStatus(SubDeviceWeldStatus.Standby, "模拟停止复位", false);
                }
            }
        }

        /// <summary>清除焊缝跟踪折返报警（标志 + 算法侧）。</summary>
        /// <remarks>会话切换必调，残留标志会致首拍报警。
        /// ⛔ 表现为启动即报警。</remarks>
        public void ClearSeamAlarm()
        {
            _seamAlarmPending = false;
            WeldParamManager.Instance.ClearSeamTrackingAlarm();
        }

        #endregion

        #region 常驻流程

        /// <summary>装配焊缝跟踪轴并订阅线激光结果</summary>
        private void AttachSeamTrackAxis()
        {
            DeltaMontionAxis axis = Manager.GetDeltaAxis(SeamTrackAxisName);
            if (axis == null)
            {
                Log(string.Format("未找到焊缝跟踪轴 {0} 线激光引导未启用", SeamTrackAxisName), MessageLevel.Warning);
                return;
            }

            _seamAxis = axis;
            LineLaserManager.Instance.ResultReady += OnSeamResultReady;
            Log(string.Format("焊缝跟踪轴 {0} 已装配", SeamTrackAxisName), MessageLevel.Debug);
        }

        /// <summary>撤除焊缝跟踪装配并清空缓冲</summary>
        private void DetachSeamTrackAxis()
        {
            LineLaserManager.Instance.ResultReady -= OnSeamResultReady;
            StopCspOutput();
            FlushSeamBuffer(true);
            lock (_seamBufferLock)
            {
                _seamBuffer.Clear();
            }
            _seamRunning = false;
            _seamAxis = null;
            _seamTrackingActive = false;
            _seamAlarmPending = false;
        }

        /// <summary>线激光结果回调把结果喂入轨迹算法</summary>
        /// <remarks>运行在相机 SDK 回调线程；轴位置读取失败按 0 处理不中断跟踪。</remarks>
        /// <param name="sender">事件源</param>
        /// <param name="e">线激光结果事件参数</param>
        private void OnSeamResultReady(object sender, LineLaserResultEventArgs e)
        {
            // 无轴记录仍须喂入：CSP 以虚拟轴顶替反馈，才能建图；
            // PVT 依赖实轴，无轴静默，避免空引用
            if (!_seamTrackingActive) return;
            if (_seamAxis == null && !IsSimRecordOnly) return;
            if (_seamAxis == null && !IsCspMode) return;

            try
            {
                if (IsCspMode)
                {
                    // CSP：本帧结果连同轴位置入图，Y 轴目标写入时反算。
                    // 轴位置即「测量时刻轴值 Ya1」；无轴记录模式取虚拟轴位置
                    WeldParamManager.Instance.OnSeamLaserResult(e.Result, SeamAxisPosMm);
                    return;
                }

                PVTPoint[] points = WeldParamManager.Instance.FeedSeamTracking(
                    e.Result, e.MeasuredFps, SeamAxisPosMm);
                EnqueueSeamPoints(points);
            }
            catch (Exception ex)
            {
                Log("线激光引导轨迹计算异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>把算法返回的 PVT 点加入下发缓冲区。</summary>
        /// <param name="points">PVT 点数组</param>
        private void EnqueueSeamPoints(PVTPoint[] points)
        {
            if (points == null || points.Length == 0) return;
            lock (_seamBufferLock)
            {
                _seamBuffer.AddRange(points);
            }
        }

        /// <summary>Ready 检查通过后开启焊缝 Y 记录会话（一次性）。</summary>
        /// <remarks>记录起点 = Ready 通过，此前不落行。
        /// ⛔ 否则曲线前端压空行。开启动作幂等，会话复位清零本标志，下次重等 Ready。</remarks>
        private void TryStartSeamTraceYOnReady()
        {
            if (_seamTraceYReadyStarted) return;
            if (!_calReturnReady) return;
            // 「有数据」取轮廓快照 HasResult（Manager 无此成员）
            if (!LineLaserManager.Instance.Profile.HasResult) return;
            if (!ProductFileRecordManager.Instance.BeginSeamTraceYSession())
            {
                // 生产会话基名未就绪（Ready 早于 BeginProductSession）：
                // ⛔ 不得自造基名（生成孤儿目录），改为按拍重试
                if (TakeElapsed(ref _seamTraceYWaitLogTime, SeamTraceYWaitLogIntervalMs))
                    Log("焊缝 Y 记录等待生产会话基名 就绪后自动开启（未开启不落任何行）", MessageLevel.Warning);
                return;
            }
            _seamTraceYReadyStarted = true;
            Log("焊缝 Y 记录开启 Ready 通过 轴已在标定位 且 线激光有数据");
        }

        /// <summary>按焊接态起止沿驱动焊缝跟踪输出</summary>
        /// <remarks>CSP 每拍补写段、PVT 每拍下发，起停与输出同序列。</remarks>
        private void UpdateSeamTracking()
        {
            // Ready 后开记录（轴到位且有数据），早于按模式早退，
            // 保证无轴模式同样在 Ready 后开记
            TryStartSeamTraceYOnReady();

            // 模拟记录模式无轴仍须采样，不能整体早退
            if (_seamAxis == null && !IsSimRecordOnly) return;

            // 起焊沿：复位 + CSP 播种启输出；停焊沿：CSP 停输出 / PVT 冲缓冲 + 模拟落盘
            bool active = IsSeamWelding;
            if (active && !_seamTrackingActive) OnSeamTrackingStart(IsCspMode);
            if (!active && _seamTrackingActive) OnSeamTrackingStop(IsCspMode);
            _seamTrackingActive = active;
            if (!active) return;

            // 运行中按模式二选一：CSP 每拍写实时目标；PVT 走缓冲下发
            if (IsCspMode) CspTick();
            else PvtTick();
        }

        /// <summary>起焊沿复位跟踪并启用修正量输出</summary>
        /// <param name="cspMode">是否 CSP 实时目标模式（否即 PVT）</param>
        private void OnSeamTrackingStart(bool cspMode)
        {
            _seamTrackingStartCount++;
            // 起焊沿留痕：只复位跟踪算法，不清工艺调整，
            // 计数 > 1 说明焊接态在焊接中抖动（取线激光 Working）。
            Log(string.Format("焊缝跟踪起焊沿 第 {0} 次 模式 {1}",
                _seamTrackingStartCount, cspMode ? "CSP" : "PVT"),
                _seamTrackingStartCount > 1 ? MessageLevel.Warning : MessageLevel.Info);
            WeldParamManager.Instance.ResetTrackingForWeldStart(cspMode ? "起焊沿 CSP 跟踪启动" : "起焊沿 PVT 跟踪启动");
            _seamRunning = false;
            // 无轴记录：虚拟轴从标定轴值起步，基准一致
            _simAxisPos = WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm;
            // 跟踪须先激活：无轴模式也靠它放开特征门禁，
            // 否则地图恒空、RecordOnlyTick 永远取不到目标
            if (cspMode) WeldParamManager.Instance.BeginSeamTrackingCorrection();
            if (!cspMode || _seamAxis == null)
            {
                // 无轴记录：整形器状态须从零起，否则继承旧值
                // 跟随器位置由虚拟轴带入，避免从 0 起跑飞。
                _cspSmoothedVelocity = 0.0;
                _cspFollowPos = _simAxisPos;
                return;
            }

            _cspSmoothedVelocity = 0.0;
            StartCspOutput();
        }

        /// <summary>停焊沿收尾输出并按需落盘轨迹</summary>
        /// <param name="cspMode">是否 CSP 实时目标模式（否即 PVT）</param>
        private void OnSeamTrackingStop(bool cspMode)
        {
            // 停焊沿留痕：与起焊沿配对，判定焊接态是否抖动
            Log(string.Format("焊缝跟踪停焊沿 模式 {0} 起焊沿已触发 {1} 次",
                cspMode ? "CSP" : "PVT", _seamTrackingStartCount));
            if (cspMode)
            {
                if (_seamAxis != null) StopCspOutput();
            }
            else if (WeldStatus == SubDeviceWeldStatus.ErrorAborted)
            {
                // 报警停焊：丢弃缓冲，不再补发欠点
                lock (_seamBufferLock)
                {
                    _seamBuffer.Clear();
                }
                Log("报警停焊 已丢弃 PVT 缓冲点", MessageLevel.Warning);
            }
            else
            {
                FlushSeamBuffer(true);
            }
            if (IsSimulation)
                ProductFileRecordManager.Instance.FlushSeamTraceY();
        }

        /// <summary>CSP 单拍按报警与轴装配分派</summary>
        private void CspTick()
        {
            if (_seamAlarmPending)
            {
                _seamAlarmPending = false;
                Fail(WeldParamManager.Instance.SeamTrackingAlarmReason);
                return;
            }
            if (_seamAxis == null) RecordOnlyTick();
            else OutputCspTick();
        }

        /// <summary>PVT 单拍：取点入缓冲、按水位下发，并记录 Y 目标与故障。</summary>
        private void PvtTick()
        {
            EnqueueSeamPoints(WeldParamManager.Instance.TickSeamTracking());
            TryFlushSeamBuffer(false);

            if (WeldParamManager.Instance.TryGetSeamTrackingCorrectionMm(out double pvtCorrection))
            {
                double pvtAxis = _seamAxis != null ? _seamAxis.GetPosition() : 0.0;
                RecordCspPoint(pvtAxis + pvtCorrection, pvtAxis);
            }

            if (WeldParamManager.Instance.SeamTrackingHasFault)
                Fail(WeldParamManager.Instance.SeamTrackingFaultReason);
        }

        /// <summary>按水位与节拍条件尝试把缓冲区写入控制器。</summary>
        /// <param name="isFinal">是否为最后一段</param>
        private void TryFlushSeamBuffer(bool isFinal)
        {
            if (!isFinal)
            {
                ushort query = _seamAxis.GetInformation(out PVTInformation info);
                if (query == PVTResult.OK && info.RemainBufferSize >= WeldParamManager.Instance.AlgorithmsConfig.MinRemainBuffer) return;
            }

            FlushSeamBuffer(isFinal);
        }

        /// <summary>把缓冲区中的 PVT 点整批写入控制器。</summary>
        /// <param name="isFinal">是否为最后一段</param>
        private void FlushSeamBuffer(bool isFinal)
        {
            PVTPoint[] segment;
            lock (_seamBufferLock)
            {
                if (_seamBuffer.Count == 0)
                {
                    if (!isFinal || !_seamRunning) return;
                    // 收尾且无待发点：补一个保持点作为最后一段
                    segment = new PVTPoint[] { new PVTPoint(_seamAxis.GetPosition(), WeldParamManager.Instance.AlgorithmsConfig.DefaultFrameSeconds, 0.0) };
                }
                else
                {
                    segment = _seamBuffer.ToArray();
                    _seamBuffer.Clear();
                }
            }

            if (!_seamRunning)
            {
                _seamLastErrorCode = _seamAxis.BeginMove(segment, PVTTimeMode.Relative, isFinal, false, 0.0);
                if (_seamLastErrorCode == PVTResult.OK)
                {
                    _seamRunning = !isFinal;
                }
            }
            else if (!isFinal || _seamRunning)
            {
                _seamLastErrorCode = _seamAxis.ContinueMove(segment, isFinal);
                if (isFinal && _seamLastErrorCode == PVTResult.OK)
                {
                    _seamRunning = false;
                }
            }

            _seamLastOutputTime = DateTime.Now;
            if (_seamLastErrorCode != PVTResult.OK)
            {
                Log("PVT 点下发失败 " + Manager.DescribeAxisCommandReturnCode(_seamLastErrorCode)
                    + " 点数 " + segment.Length, MessageLevel.Error);
            }
        }

        /// <summary>流程处理：按 RunStep 由 bool 判定推进。</summary>
        public override void FlowProcess()
        {
            // 模拟态放开 IsEnable 门禁：模拟无运控仍需跑 UpdateSeamTracking 以记录 CSP 轨迹
            if (!IsEnable && !IsSimulation) return;

            // 模拟跳过使能等准备，Working 到位后直接进就绪采样。
            // 步迁移由命令链触发，不自置 WeldStatus、不自驱
            if (IsSimulation
                && WeldStatus == SubDeviceWeldStatus.Working)
            {
                // 仅纯记录模拟（无跟踪轴）跳过使能直接进就绪；
                // 连接了运动控制器的模拟仍走 StepServoOn，但只使能 Y 向轴
                if (!HasSeamTrackAxis && RunStep != StepReady && RunStep != StepStopping && RunStep != StepFinishFail)
                {
                    AdvanceStep(StepReady);
                }
            }

            switch (RunStep)
            {
                case StepServoOn:
                    if (DoServoOn()) AdvanceStep(StepReturnToCal);
                    break;
                case StepReturnToCal:
                    if (DoReturnToCalibration()) AdvanceStep(StepReady);
                    break;
                case StepReady:
                    // 已使能待机：按拍驱动跟踪输出（CSP 写目标 / PVT 下发）
                    UpdateSeamTracking();
                    break;
                case StepStopping:
                    if (DoStopping()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                default:
                    // 待机与未登记步号：空转等待，靠相位切换复位
                    break;
            }
        }

        /// <summary>流程复位：解除急停并回工作零位。</summary>
        /// <remarks>由主控复位链驱动，本方法只做本流程动作。
        /// 轴动作在锁外执行，避免阻塞流程线程。</remarks>
        /// <returns>复位到位返回 true</returns>
        public override bool ResetProcess()
        {
            lock (_lock)
            {
                if (IsSimulation)
                {
                    // 模拟态运控可能未经真实初始化（_initialized 为 false），
                    // 不得据此拒绝复位，否则残留无法清理、互相锁死。
                    _servoSubStep = SubServoIssue;
                    _calSubStep = SubCalCheck;
                    ResetToIdle("模拟复位");
                    return true;
                }
                if (!_initialized) return false;
                if (!EnsureResetReadiness()) return false;
                Manager.SetEmergencyStop(false);
                _servoSubStep = SubServoIssue;
                _calSubStep = SubCalCheck;
                EnterWeldStatus(SubDeviceWeldStatus.Standby, "复位", false);
            }
            return ResetAxesToWorkZero();
        }

        /// <summary>清理状态并复位执行步与标志位</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _failReason = "";
            _failStep = 0;
            _servoSubStep = SubServoIssue;
            _calSubStep = SubCalCheck;
            // 直接置相位（清除后必须是 NoReset，杜绝中间态）
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤累计用时</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        #region CSP 步骤输出

        /// <summary>提升系统定时器精度，Windows 默认调度粒度 15.6ms 不满足 CSP 周期</summary>
        /// <param name="periodMs">精度（毫秒）</param>
        /// <returns>0 为成功</returns>
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint periodMs);

        /// <summary>恢复系统定时器精度</summary>
        /// <param name="periodMs">精度（毫秒）</param>
        /// <returns>0 为成功</returns>
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint periodMs);

        /// <summary>跟踪轴工程单位当量（毫米/脉冲）。</summary>
        /// <remarks>与驱动器 ToPulse 同口径；无轴或当量非法时回缺省值。</remarks>
        private double AxisEquivMm
        {
            get
            {
                double equiv = _seamAxis != null ? _seamAxis.Equiv : 0.0;
                return equiv > 0.0 ? equiv : DefaultAxisEquivMm;
            }
        }

        /// <summary>启用缓冲流式输出并建坐标系</summary>
        /// <remarks>仅由起焊沿调用；限幅在本步完成，算法只给修正量。</remarks>
        private void StartCspOutput()
        {
            // 残留收口：上一会话未收尾（停焊沿被吞、Stop 绕过）时
            // _cspOutputRunning 仍为真。旧逻辑此时直接 return，本会话便永不重建坐标系，
            // 首拍写段即 0x1100(NEED_INITIAL，实测 4352) 且 BufferStart 恒失败——二跑必然起不来。
            // 收尾幂等（内部按 _cspOutputRunning 守卫），无残留时为空操作。
            if (_cspOutputRunning) StopCspOutput();

            if (!_highResolutionTimerRaised)
            {
                TimeBeginPeriod(HighResolutionTimerPeriodMs);
                _highResolutionTimerRaised = true;
            }

            WeldAlgorithmsConfig config = WeldParamManager.Instance.AlgorithmsConfig;

            // 原点按轴当前坐标系，段目标 = 标定轴值 + 修正量
            if (!_seamAxis.StartCrdBuffer(config.MaxVelMmPerS, config.MaxAccMmPerS2))
            {
                // 残留坐标系 / 缓冲会让建系失败（实测 4627）：
                // 先收尾再重试——Mode 1 已清 FIFO 并解除坐标系，无须再 Clear；
                // 仅当 Stop 自身失败时才兜底 Clear。
                if (_seamAxis.BufferStop(BufferStopModeSmooth) != 0) _seamAxis.BufferClear();
                if (!_seamAxis.StartCrdBuffer(config.MaxVelMmPerS, config.MaxAccMmPerS2))
                {
                    Log("缓冲坐标系建立失败 焊缝跟踪不输出", MessageLevel.Error);
                    return;
                }
                Log("缓冲坐标系已清残留后重建成功", MessageLevel.Warning);
            }

            _cspOutputRunning = true;
            _cspBufferRunning = false;
            _cspBufferStartedOnce = false;
            _cspQueuedSegments = 0;
            _cspIdleTicks = 0;
            _cspBufferStartFailCount = 0;
            _cspAxisStallTicks = 0;
            _cspSmoothedVelocity = 0.0;
            _cspTickCount = 0;
            _cspWriteCount = 0;
            _cspLastQueuedPos = _seamAxis.GetPosition();
            _cspFollowPos = _cspLastQueuedPos;
            _ffRobotXPrev = double.NaN;
            _ffVxFiltered = 0.0;
            _cspFeedforwardVelMmPerS = 0.0;
            _cspVelFiltered = 0.0;
            _cspQueuedSegmentsTotal = 0;
            _cspLastAxisFeedbackPos = _cspLastQueuedPos;
            _cspLastTickTimestamp = Stopwatch.GetTimestamp();
            _cspLastSegTimestamp = _cspLastTickTimestamp;
            _cspLastDebugLogTime = DateTime.Now;
            _cspRestartCount = 0;
            _cspHasHeldTarget = false;
            _cspHeldTargetY = 0.0;
            _noValueAlarmRaised = false;

            // 生效配置整行自证：INI 会覆盖默认值，须打印
            Log(string.Format("缓冲闭环输出已启用 轴起点 {0:F3}mm 标定位 {1:F3}mm 预载 {2} 段 最大速度 {3}mm/s 最大加速度 {4}mm/s2 跟随时间常数 {5:F0}ms 写段门限 {6:F4}mm 输出死区 {7:F4}mm 耗干拍 {8}",
                _cspLastQueuedPos, config.TeachLockYMm, BufferPreloadSegments, config.MaxVelMmPerS, config.MaxAccMmPerS2,
                CspFollowTauSeconds * 1000.0, AxisEquivMm, config.OutputDeadbandMm, BufferIdleTicksToDrain));

            // 防呆：轴起点应停在标定位，偏差过大即告警
            if (Math.Abs(config.TeachLockYMm - _cspLastQueuedPos) > CspTeachDeviationWarnMm)
            {
                Log(string.Format("Y 轴标定位 {0:F3}mm 与轴起点 {1:F3}mm 偏差超过 {2:F0}mm 起焊前轴未停在标定位 标定对可能不成立",
                    config.TeachLockYMm, _cspLastQueuedPos, CspTeachDeviationWarnMm), MessageLevel.Warning);
            }
        }

        /// <summary>停止缓冲输出并清缓冲与坐标系</summary>
        private void StopCspOutput()
        {
            if (!_cspOutputRunning) return;

            ProductFileRecordManager.Instance.FlushSeamTraceY();
            _cspOutputRunning = false;
            _cspBufferRunning = false;
            _cspQueuedSegments = 0;
            if (_highResolutionTimerRaised)
            {
                TimeEndPeriod(HighResolutionTimerPeriodMs);
                _highResolutionTimerRaised = false;
            }

            if (_seamAxis != null && _cspBufferStartedOnce)
            {
                // Mode 1 已清 FIFO 解系，Stop 失败才兜底 Clear
                ushort stopRet = _seamAxis.BufferStop(BufferStopModeSmooth);
                if (stopRet == 0)
                {
                    _cspBufferCloseFailStreak = 0;
                    Log("缓冲收尾 平滑停止完成 已清 FIFO 并解除坐标系 Mode 1");
                }
                else
                {
                    ushort clearRet = _seamAxis.BufferClear();
                    _cspBufferCloseFailStreak++;
                    Log(string.Format("缓冲收尾异常 停止 {0} 兜底清空 {1} 连续 {2} 场",
                        stopRet, clearRet, _cspBufferCloseFailStreak),
                        _cspBufferCloseFailStreak >= 3 ? MessageLevel.Warning : MessageLevel.Info);
                }
            }

            _cspSmoothedVelocity = 0.0;
            _cspLastErrorCode = CspResult.OK;
        }

        /// <summary>执行单拍 CSP 输出并写段</summary>
        /// <remarks>仅由 CspTick 在有轴分支调用；段端点取起焊锚点加修正量。
        /// 平滑基准与段距差分取队列末端。静止达空闲拍数即判耗干。</remarks>
        private void OutputCspTick()
        {
            if (!_cspOutputRunning) return;

            if (IsEmergencyStop())
            {
                // 急停置位后不得写段，否则停机后又会驱动轴
                StopCspOutput();
                Log("缓冲输出已停止 原因 全局急停置位", MessageLevel.Warning);
                return;
            }

            RunCspTick();
        }

        /// <summary>执行单拍闭环并把段写入缓冲</summary>
        /// <remarks>目标按当拍位姿实时查询。
        /// 缺值期保持上次有效目标，不回落标定位。
        /// 达缺值阈值即报警停焊，本拍不再下发。</remarks>
        private void RunCspTick()
        {
            try
            {
                WeldAlgorithmsConfig config = WeldParamManager.Instance.AlgorithmsConfig;
                double tickSeconds = TakeCspElapsedSeconds();

                double robotX = ResolveCspQueryRobotX();
                double robotY = WeldParamManager.Instance.CurrentRobotY;
                bool ready = WeldParamManager.Instance.TryGetSeamTrackingTargetY(robotX, robotY, out double targetY);

                // 缺值达阈值即报警停焊，本拍不再推进
                if (!ready && IsNoValueThresholdReached(config))
                {
                    RaiseNoValueAlarm(config);
                    return;
                }

                // 目标取算法绝对 Y 目标，绝对量直传不折算
                double axisNow = _seamAxis.GetPosition();
                double cmdNow = _seamAxis.GetCommandPosition();   // 数据链遥测：指令位置
                double rawTarget;
                if (ready)
                {
                    _cspHeldTargetY = targetY;
                    _cspHasHeldTarget = true;
                    rawTarget = targetY;
                }
                else if (_cspHasHeldTarget)
                {
                    // 缺值期保持上次有效目标，禁止回落标定位
                    rawTarget = _cspHeldTargetY;
                }
                else
                {
                    // 起焊首个有效目标前允许以标定位起步
                    rawTarget = config.TeachLockYMm;
                }
                double target = ClampToStroke(rawTarget, config);
                ReportStrokeClamp(rawTarget, target, config);
                // 前馈只抬速度与加速度上限，不产生位移
                double minVelocity = 0.0;
                if (WeldParamManager.Instance.TryGetSeamTrackingRequiredVelocityMmPerS(
                        EstimateRobotVelocityX(robotX), out double requiredVelMmPerS))
                {
                    minVelocity = requiredVelMmPerS * config.FeedforwardSafetyFactor;
                }
                _cspFeedforwardVelMmPerS = minVelocity;
                // 命令端点对齐整脉冲，防累积欠走
                double command = ToPulseMm(StepCspTarget(target, tickSeconds, minVelocity)) * AxisEquivMm;
                double prevQueued = _cspLastQueuedPos;   // 写入数据链留痕：本拍写段前的队列末端位置
                double segMm = command - prevQueued;
                bool wroteSeg = QueueCspSegment(command, segMm, config, axisNow);

                // 逐拍记录与占空比计数：记录侧不按 robotX 去重，
                // 回看点距才与真实命令流一致，可见被跳过的拍。
                _cspTickCount++;
                RecordCspPoint(command, axisNow);

                // 失步检测须先于启动：判失步即置假，本拍重建
                DetectCspExecutionStall(wroteSeg, axisNow);

                // 攒够阈值段即 Start，失败达上限即停闭环
                int startThreshold = _cspBufferStartedOnce ? 1 : BufferPreloadSegments;
                if (!_cspBufferRunning && _cspQueuedSegments >= startThreshold)
                {
                    if (_seamAxis.BufferStart())
                    {
                        _cspBufferRunning = true;
                        _cspBufferStartedOnce = true;
                        _cspQueuedSegments = 0;
                        _cspBufferStartFailCount = 0;
                        _cspRestartCount++;   // 写入数据链留痕：重启会打断段流连续性，是回退的嫌疑源之一
                    }
                    else
                    {
                        _cspBufferStartFailCount++;
                        if (_cspBufferStartFailCount >= BufferStartMaxFailures)
                        {
                            Log(string.Format("缓冲启动连续失败 {0} 次 停止缓冲闭环输出 请检查轴是否被手动移动或上一会话缓冲未清",
                                _cspBufferStartFailCount), MessageLevel.Error);
                            StopCspOutput();
                            _seamAlarmPending = true;
                            return;   // 本拍已停闭环，后续诊断查询不再对已停缓冲发起
                        }
                    }
                }

                if (TakeElapsed(ref _cspBufferDiagLogTime, CspBufferDiagIntervalMs)) LogCspBufferDiagnostics();
                if (TakeElapsed(ref _cspLastDebugLogTime, CspDebugLogIntervalMs))
                {
                    LogCspCorrectionDebug(config, target);
                    LogCspMotionDebug(robotX, robotY, ready, target,
                        axisNow, target, command, segMm, ToPulseMm(command), wroteSeg);
                }
            }
            catch (Exception ex)
            {
                Log("缓冲输出异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>判缺值累计是否已达阈值</summary>
        /// <param name="config">算法配置（取缺值阈值）</param>
        /// <returns>达到阈值返回 true</returns>
        private static bool IsNoValueThresholdReached(WeldAlgorithmsConfig config)
        {
            double threshold = config.NoValueToleranceMm;
            if (threshold <= 0.0) return false;
            return WeldParamManager.Instance.NoValueDistanceMm >= threshold;
        }

        /// <summary>缺值报警停焊（一次焊段只下发一次）</summary>
        /// <remarks>不复用 DoStopMachine：不断设备、不关模组线程。
        /// 报警后保持连接与就绪分层，CSP 停发但不清累计器。</remarks>
        /// <param name="config">算法配置（取缺值阈值）</param>
        private void RaiseNoValueAlarm(WeldAlgorithmsConfig config)
        {
            if (_noValueAlarmRaised) return;
            _noValueAlarmRaised = true;
            double distanceMm = WeldParamManager.Instance.NoValueDistanceMm;
            string reason = string.Format("NoValueAlarm 引导缺值报警停焊 累计 {0:F1}mm 超过阈值 {1:F1}mm",
                distanceMm, config.NoValueToleranceMm);
            Log(reason, MessageLevel.Error);
            // 先通知机器人停（TX_NG）→ 停 CSP → 流程异常收尾
            DeviceControlWork.Instance.ReportAlarm(reason);
            StopCspOutput();
            Fail(reason);
        }

        /// <summary>取 CSP 查图用机器人 X（与入图键同源）</summary>
        /// <remarks>入图键取位姿预测值，查图须用同一来源。
        /// 开关置假可回退旧取数（真实模式恒 0）。</remarks>
        /// <returns>查图 X 坐标（毫米）</returns>
        private static double ResolveCspQueryRobotX()
        {
            if (_cspQueryXFromWeldParam) return WeldParamManager.Instance.CurrentRobotX;
            return DeviceControlWork.Instance.GetEffectiveRobotX();
        }

        /// <summary>按段距写入缓冲或处置静止拍</summary>
        /// <param name="command">本拍段端点（毫米）</param>
        /// <param name="segMm">段距（毫米）</param>
        /// <param name="config">算法配置（速度上限、加速度上限）</param>
        /// <param name="axisNowMm">本拍轴反馈位置（毫米，积压反馈输入）</param>
        /// <returns>本拍已写段返回 true</returns>
        private bool QueueCspSegment(double command, double segMm, WeldAlgorithmsConfig config, double axisNowMm)
        {
            // 段距门限与钳位共保段时长，段距照常累计
            long nowTs = Stopwatch.GetTimestamp();
            double sinceWriteSeconds = (nowTs - _cspLastSegTimestamp) / (double)Stopwatch.Frequency;

            // 距离或时间到点即写段，超时基会高估段速
            double minSegMm = Math.Max(CspMinWriteSegMm, AxisEquivMm * 0.5);
            bool reachedDistance = Math.Abs(segMm) >= minSegMm;
            bool reachedInterval = sinceWriteSeconds >= CspWriteIntervalMaxSeconds
                                   && ToPulseMm(command) != ToPulseMm(_cspLastQueuedPos);
            if (!reachedDistance && !reachedInterval)
            {
                _cspIdleTicks++;
                if (_cspIdleTicks >= BufferIdleTicksToDrain) _cspBufferRunning = false;
                return false;
            }

            // 段速时基取实测写段间隔，非单拍折算
            double segSeconds = sinceWriteSeconds;
            if (segSeconds < CspMinTickSeconds) segSeconds = CspMinTickSeconds;
            // 时基上限：越小 ⇒ 段速高估越多 ⇒ 段先跑完 ⇒ 轴回退；
            // 更长间隔说明轴已停或失步；交失步检测处置，不压段速掩盖。
            else if (segSeconds > CspSegSecondsCap) segSeconds = CspSegSecondsCap;
            double geomVel = Math.Abs(segMm) / segSeconds;
            // 段速按加速度限幅跟随几何值，弃 EMA
            double velStep = config.MaxAccMmPerS2 * Math.Max(segSeconds, CspMinTickSeconds);
            if (_cspVelFiltered <= 0.0)
            {
                _cspVelFiltered = geomVel;
            }
            else
            {
                double delta = geomVel - _cspVelFiltered;
                if (delta > velStep) delta = velStep;
                else if (delta < -velStep) delta = -velStep;
                _cspVelFiltered += delta;
            }
            double vel = _cspVelFiltered;
            // 段速与几何一致性钳位；段速显著偏离几何均速会使段时长失配——
            // 偏快则 FIFO 跑空，轴冲过端点被拉回，反复停走回退。
            // 上带 1.0 = 段速不高过几何速度，FIFO 不被抽空。
            if (vel > geomVel * CspVelGeomHiFactor) vel = geomVel * CspVelGeomHiFactor;
            else if (vel < geomVel * CspVelGeomLoFactor) vel = geomVel * CspVelGeomLoFactor;
            // 积压反馈只提段速，有上限且自限
            double dirSign = segMm >= 0.0 ? 1.0 : -1.0;
            double backlogMm = (_cspLastQueuedPos - axisNowMm) * dirSign;
            double boostMmPerS = (backlogMm - CspBacklogDeadbandMm) * CspBacklogVelGainPerS;
            if (boostMmPerS < 0.0) boostMmPerS = 0.0;
            else if (boostMmPerS > CspBacklogVelCapMmPerS) boostMmPerS = CspBacklogVelCapMmPerS;
            vel += boostMmPerS;
            if (vel > config.MaxVelMmPerS) vel = config.MaxVelMmPerS;
            if (vel < AxisEquivMm) vel = AxisEquivMm;   // 保底 1 脉冲/秒，避免段速被 ToPulse 截断成 0
            // 留痕；段时基、几何速度、段速、积压、提速并列；不参与判据。
            // 取证时可直接看出段速是否被时基高估；是否快于几何速度致断供。
            _cspLastSegSeconds = segSeconds;
            _cspLastGeomVelMmPerS = geomVel;
            _cspLastSegVelMmPerS = vel;
            _cspLastBacklogMm = backlogMm;
            _cspLastBacklogBoostMmPerS = boostMmPerS;
            // 返回码限频记录（含缓冲满 0x1200）
            ReportCspResult(_seamAxis.BufferLineTo(command, vel, vel, config.MaxAccMmPerS2));
            _cspLastQueuedPos = command;
            _cspLastSegTimestamp = nowTs;
            _cspQueuedSegments++;
            _cspQueuedSegmentsTotal++;
            _cspWriteCount++;
            _cspIdleTicks = 0;
            return true;
        }

        /// <summary>缓冲执行失步检测：已写段而轴反馈长时间不动</summary>
        /// <remarks>缓冲排空后 Start 只一次，追加段不会自动续跑。
        /// 软件侧靠段距不足计时翻假。目标仍推进时计时器不触发。轴零报错冻结。
        /// 故以位移为第二判据。位移小于阈值即置假。由调用方本拍重建执行。</remarks>
        /// <param name="wroteSeg">本拍是否已写段</param>
        /// <param name="axisNow">本拍轴反馈位置（毫米）</param>
        private void DetectCspExecutionStall(bool wroteSeg, double axisNow)
        {
            double movedMm = Math.Abs(axisNow - _cspLastAxisFeedbackPos);
            _cspLastAxisFeedbackPos = axisNow;

            // 只在本拍确实写了段时累计；未写段由耗干计时器负责，两条判据不重叠
            if (!wroteSeg) return;
            if (movedMm >= CspAxisFollowEpsilonMm)
            {
                _cspAxisStallTicks = 0;
                return;
            }

            _cspAxisStallTicks++;
            if (_cspAxisStallTicks < CspAxisStallTicksToReset) return;

            _cspAxisStallTicks = 0;
            _cspBufferRunning = false;
            if (!TakeElapsed(ref _cspLastStallWarnTime, CspAxisStallWarnIntervalMs)) return;

            // 取证：区分驱动停机/报警与反馈链路问题；本节只追加判定上下文
            Log(string.Format("缓冲执行失步 连续 {0} 拍已写段但轴未跟随 已重启缓冲执行 轴反馈 {1:F3}mm 命令 {2:F3}mm {3}",
                CspAxisStallTicksToReset, axisNow, _cspLastQueuedPos, Manager.DescribeAxisCspRuntime(_seamAxis)),
                MessageLevel.Warning);
        }

        /// <summary>按行程上下限限幅。</summary>
        /// <param name="value">待限幅值（毫米）</param>
        /// <param name="config">算法配置（行程上下限）</param>
        /// <returns>限幅后的值（毫米）</returns>
        private static double ClampToStroke(double value, WeldAlgorithmsConfig config)
        {
            if (value > config.YAxisMaxMm) return config.YAxisMaxMm;
            return value < config.YAxisMinMm ? config.YAxisMinMm : value;
        }

        /// <summary>目标被行程限幅时限频告警。</summary>
        /// <remarks>静默限幅会掩盖失控；目标被截后轴可能冲到行程末端；日志只见稳定斜率，
        /// 故超阈值即按间隔报警；核查方向为增益与标定中心。</remarks>
        /// <param name="rawTarget">限幅前目标（毫米）</param>
        /// <param name="target">限幅后目标（毫米）</param>
        /// <param name="config">算法配置（行程上下限）</param>
        private void ReportStrokeClamp(double rawTarget, double target, WeldAlgorithmsConfig config)
        {
            if (Math.Abs(rawTarget - target) < CspStrokeClampWarnMm) return;
            if (!TakeElapsed(ref _cspLastClampWarnTime, CspStrokeClampWarnIntervalMs)) return;

            Log(string.Format("跟踪目标 {0:F3}mm 超出行程 [{1:F3} 到 {2:F3}] 已限幅到 {3:F3}mm 闭环偏差过大 请核 AxisMmPerLaserMm、CalibratedCenterY 与特征量纲",
                rawTarget, config.YAxisMinMm, config.YAxisMaxMm, target), MessageLevel.Error);
        }

        /// <summary>记录缓冲运行态与两查询原值</summary>
        private void LogCspBufferDiagnostics()
        {
            _seamAxis.BufferMotionStatus(out ushort diagStatus, out int diagBlocks);
            _seamAxis.BufferRemainSize(out int diagRemain);
            // 写段占空比过低会出现无段空档致轴停走
            double duty = _cspTickCount > 0 ? (double)_cspWriteCount / _cspTickCount : 0.0;
            // [DEBUG-REMOVE] 验证期降为 Debug 级；验证后改回 Info（不删）
            Log(string.Format("缓冲诊断 运行 {0} 空闲拍 {1} 状态 {2} 块数 {3} 余量 {4} 写段占空比 {5:P0}({6}/{7})",
                _cspBufferRunning ? 1 : 0, _cspIdleTicks, diagStatus, diagBlocks, diagRemain,
                duty, _cspWriteCount, _cspTickCount), MessageLevel.Debug);
            _cspTickCount = 0;
            _cspWriteCount = 0;
        }

        /// <summary>[DEBUG-REMOVE] 闭环 Debug 行（算法侧）：目标分量与地图健康度。</summary>
        /// <param name="config">算法配置（标定轴值 / 标定中心 / 机器人 Y 系数）</param>
        /// <param name="targetY">本拍绝对 Y 轴目标（毫米）</param>
        private void LogCspCorrectionDebug(WeldAlgorithmsConfig config, double targetY)
        {
            Log(string.Format("调试闭环 标定轴值 {0:F3} 标定中心 {1:F3} 特征Y {2:F3} 特征修正 {3:F3} 机器人项 {4:F3} 目标 {5:F3} 地图点 {6} 平滑跳过 {7} 野值 {8} 无效帧 {9} 抖动回退 {10} 机器人X {11:F1}",
                config.TeachLockYMm,
                config.CalibratedCenterY,
                WeldParamManager.Instance.SeamTrackingLastFeatureY,
                WeldParamManager.Instance.SeamTrackingLastCorrectionMm,
                config.RobotYGain * WeldParamManager.Instance.SeamTrackingPathYOffset,
                targetY,
                WeldParamManager.Instance.SeamTrackingMapPointCount,
                WeldParamManager.Instance.SeamTrackingSmoothSkipCount,
                WeldParamManager.Instance.SeamTrackingOutlierCount,
                WeldParamManager.Instance.SeamTrackingInvalidFrameCount,
                WeldParamManager.Instance.SeamTrackingJitterRejectCount,
                WeldParamManager.Instance.SeamTrackingRobotX), MessageLevel.Debug);
        }

        /// <summary>[DEBUG-REMOVE] 闭环 Debug 行：当拍位姿、目标、命令、段距与缓冲状态</summary>
        /// <remarks>写段三联留痕：时基、几何段速、写入段速。
        /// 轴实速远大于写入段速即被抬到最小值。接近 1 有锯齿即亚脉冲量化。两条均归周期写段处置。</remarks>
        /// <param name="robotX">当拍机器人 X 坐标（毫米）</param>
        /// <param name="robotY">当拍机器人 Y 坐标（毫米）</param>
        /// <param name="ready">本拍是否取到地图目标</param>
        /// <param name="targetY">算法给出的绝对 Y 轴目标（毫米）</param>
        /// <param name="axisNow">轴实际反馈位置（毫米）</param>
        /// <param name="target">限幅钳位后的本拍目标（毫米）</param>
        /// <param name="command">本拍段端点（毫米）</param>
        /// <param name="segMm">段距（毫米）</param>
        /// <param name="commandPulse">本拍段端点（脉冲，与驱动器 ToPulse 同口径）</param>
        /// <param name="wroteSeg">是否已写段</param>
        private void LogCspMotionDebug(double robotX, double robotY, bool ready, double targetY,
            double axisNow, double target, double command, double segMm, int commandPulse, bool wroteSeg)
        {
            Log(string.Format("调试运控 机器人X {0:F1} 机器人Y {1:F3} 取到目标 {2} 目标Y {3:F3} 轴反馈 {4:F3} 目标 {5:F3} 命令 {6:F3} 脉冲 {7} 段距 {8:F4} 写段 {9} 运行 {10} 空闲拍 {11} 队列段 {12} 前馈 {13:F3} 累计段 {14} 段时基 {15:F3} 几何段速 {16:F3} 段速 {17:F3} 积压 {18:F3} 提速 {19:F3}",
                robotX, robotY, ready ? 1 : 0, targetY,
                axisNow, target, command, commandPulse, segMm, wroteSeg ? 1 : 0,
                _cspBufferRunning ? 1 : 0, _cspIdleTicks, _cspQueuedSegments, _cspFeedforwardVelMmPerS, _cspQueuedSegmentsTotal,
                _cspLastSegSeconds, _cspLastGeomVelMmPerS, _cspLastSegVelMmPerS,
                _cspLastBacklogMm, _cspLastBacklogBoostMmPerS), MessageLevel.Debug);
        }

        /// <summary>毫米换算为脉冲数（截断取整，与驱动器 ToPulse 同口径）。</summary>
        /// <param name="valueMm">工程值（毫米）</param>
        /// <returns>脉冲数</returns>
        private int ToPulseMm(double valueMm)
        {
            return (int)(valueMm / AxisEquivMm);
        }

        /// <summary>判定距上次时刻是否已达间隔</summary>
        /// <param name="lastTime">上次时刻</param>
        /// <param name="intervalMs">间隔（毫秒）</param>
        /// <returns>已达间隔返回 true</returns>
        private static bool IsElapsed(DateTime lastTime, int intervalMs)
        {
            return (DateTime.Now - lastTime).TotalMilliseconds >= intervalMs;
        }

        /// <summary>判定间隔并把上次时刻推进到当前</summary>
        /// <param name="lastTime">上次时刻，命中时更新为当前时间</param>
        /// <param name="intervalMs">间隔（毫秒）</param>
        /// <returns>已达间隔返回 true</returns>
        private static bool TakeElapsed(ref DateTime lastTime, int intervalMs)
        {
            if (!IsElapsed(lastTime, intervalMs)) return false;
            lastTime = DateTime.Now;
            return true;
        }

        /// <summary>记录一拍焊缝 Y 入生产记录（SeamTraceY 五列，由 ProductFileRecordManager 独占持有）。</summary>
        /// <param name="cspOutY">本拍查图输出 Y（毫米；查图 X 尚无地图点时 = 标定轴值原值不变）</param>
        /// <param name="axisY">焊缝跟踪轴实际反馈位置（毫米）；与出图并列入档，供判定写入是否被驱动器执行</param>
        private void RecordCspPoint(double cspOutY, double axisY)
        {
            double x = DeviceControlWork.Instance.GetEffectiveRobotX();
            WeldParamManager mgr = WeldParamManager.Instance;
            bool has = mgr.SeamTrackingHasMapEntry;
            double keyX = has ? mgr.SeamTrackingLastMapKeyX : 0.0;
            double inYt = has ? mgr.SeamTrackingLastMapEntryY : 0.0;
            ProductFileRecordManager.Instance.RecordSeamTraceYPoint(x, keyX, inYt, cspOutY, axisY);
        }

        /// <summary>无轴记录模式下平滑并采样轨迹点</summary>
        /// <remarks>无控制器时本方法替代 OutputCspTick，提供连续轨迹供回看；
        /// 目标与有轴同源（当拍位姿查询，基准标定轴值）；两种曲线可比。</remarks>
        private void RecordOnlyTick()
        {
            WeldAlgorithmsConfig config = WeldParamManager.Instance.AlgorithmsConfig;
            double robotX = DeviceControlWork.Instance.GetEffectiveRobotX();
            double robotY = WeldParamManager.Instance.CurrentRobotY;
            bool ready = WeldParamManager.Instance.TryGetSeamTrackingTargetY(robotX, robotY, out double targetY);

            // 虚拟轴：目标与有轴同源（绝对直传），限幅后推进
            double target = ClampToStroke(ready ? targetY : config.TeachLockYMm, config);
            // 不写段：逐拍把虚拟轴位置带入跟随器；两模式共用同一整形器
            _cspFollowPos = _simAxisPos;
            double command = StepCspTarget(target, TakeCspElapsedSeconds(), 0.0);
            _simAxisPos = command;
            RecordCspPoint(command, command);

            // [DEBUG-REMOVE] 无轴记录 Debug 行：限频输出算法分量与轴推进
            if (TakeElapsed(ref _cspLastDebugLogTime, CspDebugLogIntervalMs))
            {
                LogCspCorrectionDebug(config, target);
                Log(string.Format("调试记录 目标 {0:F3} 命令 {1:F3} 取到目标 {2}",
                    target, command, ready ? 1 : 0), MessageLevel.Debug);
            }
        }

        /// <summary>取本拍与上一拍的实际间隔秒值</summary>
        /// <remarks>节拍由编排层模组线程 Sleep 决定，实测值随调度抖动；
        /// 按实测间隔折算速度与加速度才保量纲；两端钳位防跳变。</remarks>
        /// <returns>实测节拍（秒）</returns>
        private double TakeCspElapsedSeconds()
        {
            long now = Stopwatch.GetTimestamp();
            double elapsed = (now - _cspLastTickTimestamp) / (double)Stopwatch.Frequency;
            _cspLastTickTimestamp = now;

            if (elapsed < CspMinTickSeconds) return CspMinTickSeconds;
            if (elapsed > CspMaxTickSeconds) return CspMaxTickSeconds;
            return elapsed;
        }

        /// <summary>全局急停是否已置位。</summary>
        /// <returns>急停置位返回 true</returns>
        private static bool IsEmergencyStop()
        {
            MontionControl control = Manager.MontionControl;
            return control != null && control.EmergencyStop;
        }

        /// <summary>单拍输出整形：跟随 → 速度限幅 → 加速度限幅 → 段端点。</summary>
        /// <remarks>跟随器按内部浮点位置推进，与末段解耦；保留亚脉冲残差，攒够整脉冲才写段。
        /// 前馈 = 机器人 X 速度 × 地图斜率 × 安全系数；上限抬到两者较大值，前馈 0 时同原行为。
        /// ⛔ 不得对残差设死区；也不得用本拍输出作残差（会轴停走）。</remarks>
        /// <param name="target">本拍算法原始目标（毫米，调用方已完成限幅）</param>
        /// <param name="tickSeconds">本拍实测节拍（秒）</param>
        /// <param name="minVelocityMmPerS">前馈速度下限（毫米/秒幅值，0 = 无前馈）</param>
        /// <returns>本拍段端点（毫米）</returns>
        private double StepCspTarget(double target, double tickSeconds, double minVelocityMmPerS)
        {
            WeldAlgorithmsConfig config = WeldParamManager.Instance.AlgorithmsConfig;

            // 残差取跟随器位置，不得抹零致段端点冻结
            double error = target - _cspFollowPos;

            double maxVel = config.MaxVelMmPerS;
            if (minVelocityMmPerS > maxVel) maxVel = minVelocityMmPerS;
            double desiredVelocity = error / CspFollowTauSeconds;
            if (desiredVelocity > maxVel) desiredVelocity = maxVel;
            else if (desiredVelocity < -maxVel) desiredVelocity = -maxVel;

            double maxAcc = config.MaxAccMmPerS2;
            // 前馈须在一个 τ 内建立，否则抬高的上限形同虚设
            double ffAcc = minVelocityMmPerS / CspFollowTauSeconds;
            if (ffAcc > maxAcc) maxAcc = ffAcc;
            double maxVelocityDelta = maxAcc * tickSeconds;
            double velocityDelta = desiredVelocity - _cspSmoothedVelocity;
            if (velocityDelta > maxVelocityDelta) velocityDelta = maxVelocityDelta;
            else if (velocityDelta < -maxVelocityDelta) velocityDelta = -maxVelocityDelta;

            _cspSmoothedVelocity = _cspSmoothedVelocity + velocityDelta;
            _cspFollowPos = _cspFollowPos + _cspSmoothedVelocity * tickSeconds;
            return _cspFollowPos;
        }

        /// <summary>由机器人 X 相邻拍差分推算 X 向速度（EMA 平滑）。</summary>
        /// <remarks>
        /// X 坐标连续可得，差分即速度；折返/毛刺由幅值取绝对 + 合理上限钳位 + EMA 三重吸收。
        /// 首拍基准未建立时返回 0（无前馈，回退旧行为）。</remarks>
        /// <param name="robotX">本拍机器人 X 坐标（毫米）</param>
        /// <returns>X 向速度幅值（毫米/秒）</returns>
        private double EstimateRobotVelocityX(double robotX)
        {
            long now = Stopwatch.GetTimestamp();
            if (!double.IsNaN(_ffRobotXPrev))
            {
                double dt = (now - _ffRobotXTimePrev) / (double)Stopwatch.Frequency;
                if (dt >= CspMinTickSeconds)
                {
                    double vel = Math.Abs(robotX - _ffRobotXPrev) / dt;
                    if (vel > FeedforwardVxSaneLimitMmPerS) vel = FeedforwardVxSaneLimitMmPerS;
                    _ffVxFiltered += (vel - _ffVxFiltered) * FeedforwardVxAlpha;
                }
            }
            else
            {
                _ffVxFiltered = 0.0;
            }
            _ffRobotXPrev = robotX;
            _ffRobotXTimePrev = now;
            return _ffVxFiltered;
        }

        /// <summary>按 CSP 返回码限频记录日志</summary>
        /// <param name="ret">台达返回码</param>
        private void ReportCspResult(ushort ret)
        {
            if (ret == CspResult.OK)
            {
                _cspLastErrorCode = ret;
                return;
            }

            // 同码限频：换码立即记录，同码须间隔已满
            if (ret == _cspLastErrorCode && !IsElapsed(_cspLastErrorLogTime, CspErrorLogIntervalMs)) return;

            _cspLastErrorCode = ret;
            _cspLastErrorLogTime = DateTime.Now;
            Log(string.Format("CSP 缓冲写入失败 返回码 {0}", ret), MessageLevel.Error);
        }

        /// <summary>折返报警回调只置待处置标志</summary>
        /// <remarks>跨线程推进执行步会与流程线程竞争；故只置标志。</remarks>
        /// <param name="sender">事件源</param>
        /// <param name="e">报警请求事件参数</param>
        private void OnSeamTrackingAlarmRequested(object sender, TrackingAlarmEventArgs e)
        {
            _seamAlarmPending = true;
            Log("焊缝跟踪折返报警请求 " + (e != null ? e.Reason : ""), MessageLevel.Error);
        }

        #endregion

        /// <summary>设备连接（阻塞）：初始化台达总线，同步等待完成。</summary>
        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            try
            {
                Log("运控初始化连接开始", MessageLevel.Debug);
                EnterWeldStatus(SubDeviceWeldStatus.PreWork, "开始初始化", false);
                BeginConnectAttempt();

                string msg = "";
                if (!Manager.Initialization("台达运控", ref msg))
                {
                    // 根因已由 DeltaMontionControl 记录，此处只推进状态不复述错误
                    EnterWeldStatus(SubDeviceWeldStatus.ErrorAborted, "运控初始化失败", false);
                    MarkConnectFailed("运控初始化失败 " + msg);
                    return false;
                }

                _initialized = true;
                AttachSeamTrackAxis();
                EnterWeldStatus(SubDeviceWeldStatus.Standby, "初始化完成", false);
                SetState(SubDeviceState.Connected, "运控连接完成");
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化完成 等待复位");
                Log("运控初始化连接完成", MessageLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                Log("运控初始化连接异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备断开并关闭台达总线</summary>
        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            // 幂等：已断开且未初始化时不再二次关闭
            if (State == SubDeviceState.Disconnected && !_initialized) return true;
            try
            {
                Log("运控初始化断开开始");
                lock (_lock)
                {
                    DetachSeamTrackAxis();
                    Manager.Close();
                    _initialized = false;
                    _servoSubStep = SubServoIssue;
                    EnterWeldStatus(SubDeviceWeldStatus.Standby, "初始化断开", false);
                }
                SetState(SubDeviceState.Disconnected, "运控初始化断开");
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开完成");
                return true;
            }
            catch (Exception ex)
            {
                Log("运控初始化断开异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备日志：输出连接态、过程态、步骤与初始化标志。</summary>
        public override void SubDeviceLog()
        {
            // 占位符与实参须一一对应：多写占位符会立即抛 FormatException
            Log(string.Format("运控 连接态 {0} 焊接过程态 {1} 步骤 {2} 已初始化 {3}",
                State, WeldStatus, WorkStepName, _initialized));
        }

        /// <summary>加载本模组的配置。</summary>
        public override void LoadConfig()
        {
            TimeOutSeconds = ReadInt("TimeOutSeconds", 30);
        }

        /// <summary>保存本模组的配置。</summary>
        public override void SaveConfig()
        {
            Config.SetElementValue("TimeOutSeconds", TimeOutSeconds.ToString());
            Config.SaveXdocument();
        }

        /// <summary>焊接工作状态切换管控。</summary>
        /// <remarks>级联迁移后由编排入口调用（WeldStatus 已是目标态）；停焊收尾须落在本入口。
        /// DoStopping 要等步机迁移才跑，而 StepReady 是唯一驱动跟踪的步；
        /// 一离步停焊沿即被吞；跟踪态与缓冲态残留（二跑首拍即 0x1100）。</remarks>
        public override void WeldStatusTrans()
        {
            if (WeldStatus == SubDeviceWeldStatus.NoReset) return;
            if (WeldStatus == SubDeviceWeldStatus.Stopping)
            {
                StopSeamTrackingOutput();
                EnterWeldStatus(SubDeviceWeldStatus.Stopping, "焊接状态切换管控", false);
                return;
            }
            if (WeldStatus == SubDeviceWeldStatus.ManualStopped
                || WeldStatus == SubDeviceWeldStatus.ErrorAborted)
            {
                StopSeamTrackingOutput();
                EnterWeldStatus(SubDeviceWeldStatus.Standby, "焊接状态切换管控", false);
            }
        }

        #endregion

        #region 非阻塞动作指令

        /// <summary>开启连接（非阻塞）：动作即退出释放。</summary>
        /// <returns>已初始化受理返回 true，未初始化返回 false</returns>
        public override bool ConnectOn()
        {
            if (!_initialized) return false;
            SetState(SubDeviceState.Connected, "运控已连接");
            return true;
        }

        /// <summary>关闭连接（非阻塞）：动作即退出释放。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOff()
        {
            Stop(false);
            SetState(SubDeviceState.Disconnected, "运控断开");
            return true;
        }

        #endregion

        #region 按轴就绪与门禁（流程层）

        private Action _axisReadyHandler;
        private bool _axisReadyWired;

        /// <summary>TA（送丝力矩电机）运行轴；运控未初始化或未装配返回 null。</summary>
        private DeltaMontionAxis TaAxis
        {
            get
            {
                if (Manager.MoveData == null || Manager.MoveData.MotionConfig == null) return null;
                return Manager.GetDeltaAxis(Manager.MoveData.MotionConfig.TaAxisName);
            }
        }

        /// <summary>TA 轴配置（力矩参数与软限位）；未配置返回 null。</summary>
        private DeltaAxisConfig TaAxisConfig
        {
            get
            {
                if (Manager.MoveData == null || Manager.MoveData.MotionConfig == null) return null;
                return Manager.MoveData.MotionConfig.GetAxis(Manager.MoveData.MotionConfig.TaAxisName);
            }
        }

        /// <summary>轴就绪变化（转发硬件层事件，按轴过滤）；订阅后刷新指令区。</summary>
        /// <remarks>后台线程触发，订阅方自负封送；⛔ 不用轮询 Timer 掩盖。</remarks>
        public event Action AxisReadyChanged
        {
            add
            {
                if (!_axisReadyWired)
                {
                    _axisReadyWired = true;
                    Manager.AxisReadinessChanged += OnAxisReadinessChanged;
                }
                _axisReadyHandler += value;
            }
            remove
            {
                _axisReadyHandler -= value;
            }
        }

        /// <summary>Y 轴流程层就绪：委托硬件层 MontionManager 判定轴状态。</summary>
        /// <remarks>⛔ 门禁按轴独立：Y 失败只关 Y 闸门，不连带 TA / LA。</remarks>
        public bool YReady { get { return Manager.YReady; } }

        /// <summary>Y 轴不可用原因（就绪返回空串）。</summary>
        public string YGateReason { get { return Manager.YGateReason; } }

        /// <summary>TA 轴是否可执行全部 TA 指令（配置 ∧ 使能 ∧ 工作零位到位）。</summary>
        public bool TaAvailable { get { return Manager.TaAvailable; } }

        /// <summary>TA 轴是否可动（配置 ∧ 使能，不含工作零位到位；仅 TA_ZeroPose 用）。</summary>
        public bool TaMotionReady { get { return Manager.TaMotionReady; } }

        /// <summary>TA 降级原因（就绪返回空串）。</summary>
        public string TaDegradedReason { get { return Manager.TaDegradedReason; } }

        /// <summary>TA 是否处于 auto 力矩输出态（只读，供 UI 显示动作位）。</summary>
        public bool TaTorqueActive
        {
            get
            {
                DeltaMontionAxis axis = TaAxis;
                return axis != null && axis.TorqueOutputEnabled;
            }
        }

        /// <summary>TA 最近一次撤力是否失败（true = 力矩可能仍在输出；只读，供 UI 显示「撤力失败」而非「力矩输出中」）。</summary>
        /// <remarks>与 <see cref="TaTorqueActive"/> 互补。输出态可能是正常出力或撤力失败残留。
        /// UI 只读本属性区分。⛔ 不得自行推导。</remarks>
        public bool TaTorqueRetractFailed
        {
            get
            {
                DeltaMontionAxis axis = TaAxis;
                return axis != null && axis.TorqueRetractFailed;
            }
        }

        /// <summary>撤力失败返回码（0 表示无记录），供界面文案定位。</summary>
        public ushort TaTorqueRetractFailCode
        {
            get
            {
                DeltaMontionAxis axis = TaAxis;
                return axis == null ? (ushort)0 : axis.TorqueRetractFailCode;
            }
        }

        /// <summary>撤力失败成品文案（错误码措辞由 MontionManager 产出，UI 直显）。</summary>
        public string TaTorqueRetractFailureText
        {
            get
            {
                if (!TaTorqueRetractFailed) return string.Empty;
                return "TA 力矩" + Manager.DescribeAxisRetractFailure(TaTorqueRetractFailCode)
                    + "：力矩可能仍在输出，请再次执行 TA_Retract / 伺服下电 / 急停 重试撤力";
            }
        }

        /// <summary>TA 轴当前位置（毫米，只读；LA 随动补偿按该位置查曲线B）。</summary>
        public double TaPositionMm
        {
            get
            {
                DeltaMontionAxis axis = TaAxis;
                return axis == null ? 0.0 : axis.GetPosition();
            }
        }

        /// <summary>TA break 锁存位置（毫米，只读）。</summary>
        public double TaBreakHoldMm
        {
            get { lock (_taLock) { return _taBreakHoldMm; } }
        }

        /// <summary>是否已发生过一次成功的 TA break（只读）。</summary>
        public bool TaBreakHoldValid
        {
            get { lock (_taLock) { return _taBreakHoldValid; } }
        }

        /// <summary>运控未初始化（应用动作入口拒绝码）</summary>
        public const string AxisRejectNotInitialized = "NOT_INITIALIZED";

        /// <summary>角色轴未装配（应用动作入口拒绝码）</summary>
        public const string AxisRejectAxisNotAssigned = "AXIS_NOT_ASSIGNED";

        /// <summary>按轴门禁未就绪（应用动作入口拒绝码）</summary>
        public const string AxisRejectAxisNotReady = "AXIS_NOT_READY";

        /// <summary>同轴单飞被拒（应用动作入口拒绝码）</summary>
        public const string AxisRejectBusy = "BUSY";

        /// <summary>指令下发失败（应用动作入口拒绝码）</summary>
        public const string AxisRejectCommandFailed = "COMMAND_FAILED";

        /// <summary>状态检测未达成（应用动作入口拒绝码）</summary>
        public const string AxisRejectNotReached = "NOT_REACHED";

        /// <summary>自动焊接流程进行中，手动动作与生产流程互斥（应用动作入口拒绝码）</summary>
        public const string AxisRejectAutoWelding = "AUTO_WELDING";

        /// <summary>阶段变化（开始/结束/失败）；据此选中并按结果上色。</summary>
        /// <remarks>后台线程触发，订阅方自负封送。</remarks>
        public event Action TaZeroPoseStageChanged;

        /// <summary>TA_ZeroPose 是否正在执行（初始化自动与手动共用）。</summary>
        public bool IsTaZeroPoseRunning { get { return _taZeroPoseRunning; } }

        /// <summary>本上电周期是否执行过 ZeroPose（区分从未执行与失败）。</summary>
        public bool IsTaZeroPoseAttempted { get { return _taZeroPoseAttempted; } }

        /// <summary>TA_ZeroPose 最近一次是否到位。</summary>
        public bool IsTaZeroPoseDone { get { return _taZeroPoseDone; } }

        /// <summary>TA_ZeroPose 最近一次是否失败（含被停止打断）。</summary>
        public bool IsTaZeroPoseFailed { get { return _taZeroPoseFailed; } }

        private void NotifyTaZeroPoseStageChanged()
        {
            Action handler = TaZeroPoseStageChanged;
            if (handler == null) return;
            try { handler(); }
            catch (Exception ex) { Log("TA_ZeroPose 阶段事件处理异常 " + ex.Message, MessageLevel.Warning); }
        }

        private void MarkTaZeroPoseRunning(bool running)
        {
            _taZeroPoseRunning = running;
            if (running)
            {
                _taZeroPoseAttempted = true;   // 开始即记「跑过」，失败后 UI 才能按失败上色
                _taZeroPoseFailed = false;     // 新一轮开始先清上一轮失败色，运行态呈中性灰
            }
            NotifyTaZeroPoseStageChanged();
            Manager.NotifyAxisReadinessChanged("TA");
        }

        private void MarkTaZeroPoseSucceeded()
        {
            _taZeroPoseDone = true;
            _taZeroPoseFailed = false;
            Manager.SetTaZeroPoseGate(true, string.Empty);
            NotifyTaZeroPoseStageChanged();
            Manager.NotifyAxisReadinessChanged("TA");
        }

        private void MarkTaZeroPoseFailed(string reason)
        {
            _taZeroPoseDone = false;
            _taZeroPoseFailed = true;
            string text = string.IsNullOrEmpty(reason) ? "TA_ZeroPose 未到位" : "TA_ZeroPose 未到位（" + reason + "）";
            Manager.SetTaZeroPoseGate(false, text);
            Log("TA_ZeroPose 失败 工作零位未到位（不阻断 TA 指令，仅观测） 原因 " + text, MessageLevel.Error);
            NotifyTaZeroPoseStageChanged();
            Manager.NotifyAxisReadinessChanged("TA");
        }

        #region 应用动作与初始化叙述（流程层）

        /// <summary>退出 TA 力矩输出（撤 CST 输出并回 CSP 位置模式）。</summary>
        /// <remarks>是否真出力由 <see cref="DeltaMontionAxis.TorqueOutputEnabled"/> 判定，未在输出时不重复下发。</remarks>
        /// <param name="reason">触发来源（日志用）</param>
        /// <returns>撤力成功或本来就不在扭矩输出态返回 true</returns>
        public bool ExitTaTorque(string reason)
        {
            DeltaMontionAxis ta = TaAxis;
            if (ta == null || !ta.TorqueOutputEnabled) return true;
            bool ok = ta.StopTorque();
            Log("TA 退出力矩输出（撤 CST_Output_Enable → 回 CSP）" +
                (string.IsNullOrEmpty(reason) ? "" : " 来源 " + reason),
                ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>设置 TA 轴控制模式</summary>
        /// <remarks>送丝即 TA（同一台达轴），不存在独立送丝轴；唯一写口，页面不得直接改轴配置</remarks>
        /// <param name="enabled">true=允许力矩模式；false=退出力矩模式</param>
        /// <param name="reason">触发来源（日志用）</param>
        /// <returns>false 表示未找到 TA 轴配置，本次未生效</returns>
        public bool SetTaTorqueMode(bool enabled, string reason = null)
        {
            DeltaAxisConfig cfg = TaAxisConfig;
            if (cfg == null)
            {
                Log("TA 力矩模式切换失败：未找到 TA 轴配置", MessageLevel.Warning);
                return false;
            }
            if (cfg.TorqueModeEnabled == enabled) return true;
            cfg.TorqueModeEnabled = enabled;
            string source = string.IsNullOrEmpty(reason) ? string.Empty : "，来源：" + reason;
            Log((enabled ? "TA 轴进入力矩模式" : "TA 轴退出力矩模式") + source, MessageLevel.Info);
            return true;
        }

        /// <summary>TA 力矩下发（CST，唯一写口）。</summary>
        /// <remarks>未进入力矩模式或轴未伺服使能时拒绝下发；底层失败已回滚到 CSP，不会留下半使能状态。</remarks>
        /// <param name="targetTorque">目标力矩（额定千分比）</param>
        /// <param name="slope">扭矩爬升速率（0.1%/s）</param>
        /// <returns>下发成功返回 true</returns>
        public bool SetTaTorque(short targetTorque, uint slope)
        {
            DeltaAxisConfig cfg = TaAxisConfig;
            DeltaMontionAxis axis = TaAxis;
            if (cfg == null || axis == null)
            {
                Log("TA 力矩下发被拒：未找到 TA 轴配置", MessageLevel.Warning);
                return false;
            }
            if (!cfg.TorqueModeEnabled)
            {
                Log("TA 力矩下发被拒：轴未进入力矩模式（TORQUE_MODE=false）", MessageLevel.Warning);
                return false;
            }
            if (!axis.IsServoOn())
            {
                Log("TA 力矩下发被拒：轴未伺服使能", MessageLevel.Warning);
                return false;
            }
            if (!axis.SetTorque(targetTorque, slope))
            {
                Log("TA 力矩下发失败，已回滚位置模式", MessageLevel.Error);
                return false;
            }
            Log("TA 力矩已下发 目标 " + targetTorque + " 扭矩爬升速率 " + slope, MessageLevel.Info);
            return true;
        }

        /// <summary>撤除 TA 力矩输出并切回位置模式（唯一写口）。</summary>
        /// <param name="reason">触发来源（日志用）</param>
        /// <returns>撤除成功返回 true</returns>
        public bool StopTaTorque(string reason = null)
        {
            DeltaMontionAxis axis = TaAxis;
            if (axis == null)
            {
                Log("撤除 TA 力矩失败：轴未装配", MessageLevel.Warning);
                return false;
            }
            bool ok = axis.StopTorque();
            string source = string.IsNullOrEmpty(reason) ? string.Empty : "，来源：" + reason;
            Log(ok ? "TA 力矩已撤除并切回位置模式" + source : "撤除 TA 力矩失败" + source,
                ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>整机初始化汇总（仅展示，不参与任何轴门禁判定）。</summary>
        /// <returns>「全部完成（成功 N 轴）」或「部分完成（成功 N 轴 / 失败 M 轴：轴名）」</returns>
        public string BuildMachineInitSummary()
        {
            int okCount = 0;
            int failCount = 0;
            string failNames = string.Empty;
            foreach (MontionAxis axis in Manager.Axes.GetAll())
            {
                if (axis == null) continue;
                bool on = false;
                try { on = axis.IsServoOn(); }
                // 诊断探测：失败原因不在这里体现，不重复打日志
                catch (Exception) { }
                if (on)
                {
                    okCount++;
                }
                else
                {
                    failCount++;
                    failNames += (failNames.Length == 0 ? string.Empty : "、") + axis.AxisName;
                }
            }
            if (failCount == 0) return string.Format("整机初始化 全部完成（成功 {0} 轴）", okCount);
            return string.Format("整机初始化 部分完成（成功 {0} 轴 / 失败 {1} 轴：{2}）",
                okCount, failCount, failNames);
        }

        /// <summary>逐轴打印门禁结论（一眼看出哪轴开、哪轴关）。</summary>
        public void LogAxisGateReport()
        {
            // 轴汇总降 Debug 并前移：模组汇总恒为最后一条
            Log(BuildMachineInitSummary(), MessageLevel.Debug);

            Log(string.Format("轴闸门 TA {0}{1}", Manager.TaAvailable ? "打开" : "关闭",
                Manager.TaAvailable ? "" : " " + Manager.TaDegradedReason),
                Manager.TaAvailable ? MessageLevel.Info : MessageLevel.Warning);

            Log(string.Format("轴闸门 Y {0}{1}", Manager.YReady ? "打开" : "关闭",
                Manager.YReady ? "" : " " + Manager.YGateReason),
                Manager.YReady ? MessageLevel.Info : MessageLevel.Warning);

            Log(string.Format("轴闸门 LA {0}{1}", Manager.LaReady ? "打开" : "关闭",
                Manager.LaReady ? "" : " " + Manager.LaGateReason),
                Manager.LaReady ? MessageLevel.Info : MessageLevel.Warning);
        }

        #endregion

        /// <summary>TA 指令类型（同一入口按枚举分派）。</summary>
        public enum TaCommand
        {
            /// <summary>TA_Auto：按 TA_FORCE_N / TA_TORQUE_DIRECTION 输出下压力</summary>
            Auto,

            /// <summary>TA_Retract：方向取反的纯力矩输出</summary>
            Retract,

            /// <summary>TA_Break：停止力矩输出并锁在当前位</summary>
            Break,

            /// <summary>TA_Pose1：移到预设调试位 1</summary>
            Pose1,

            /// <summary>TA_Pose2：移到预设调试位 2</summary>
            Pose2,

            /// <summary>TA_ZeroPose：走到 TA 工作零位</summary>
            ZeroPose
        }

        private void OnAxisReadinessChanged(string axisName)
        {
            if (!(axisName == "Y" || axisName == "TA" || axisName == "ALL")) return;
            _axisReadyHandler?.Invoke();
        }

        /// <summary>指令统一前置判定（降级即拒绝并打印原因）</summary>
        /// <param name="actionName">指令名（日志用）</param>
        /// <returns>TA 指令可执行返回 true</returns>
        private bool CheckTaAvailable(string actionName)
        {
            if (Manager != null && Manager.TaAvailable) return true;
            string reason = Manager == null ? "运动管理器未就绪" : Manager.TaDegradedReason;
            Log(actionName + " 被拒 TA 指令不可执行（未使能或未回零） 原因 " + reason, MessageLevel.Error);
            return false;
        }

        /// <summary>自动焊接流程禁入门禁：焊接流程在运行中 ⇒ 拒绝手动动作</summary>
        /// <remarks>2026-11-17 新增（ADR-100 演进）：通过界面 UI 发起的动作一律是手动模式，
        /// 自动焊接流程一旦启动到结束必须拒绝——手动与生产流程同时驱动同一根轴会互相打架。
        /// 判据唯一来源 = <see cref="DeviceControlWork.IsAutoWeldingRunning"/>（主设备级权威）。
        /// ⛔ 不复用 <c>IsWelding</c>：后者覆盖整段开机期，过宽会把正常调试也禁掉。</remarks>
        /// <param name="actionName">动作名（日志用）</param>
        /// <returns>允许执行返回 true</returns>
        private bool CheckAutoWeldingConflict(string actionName)
        {
            if (!DeviceControlWork.Instance.IsAutoWeldingRunning) return true;
            Log(actionName + " 被拒 自动焊接流程进行中，手动动作与生产流程互斥", MessageLevel.Warning);
            return false;
        }

        /// <summary>TA 目标位置绝对移动</summary>
        /// <remarks>Pose 动作共用；调用方负责先退出力矩输出</remarks>
        /// <param name="posMm">目标位置（毫米）</param>
        /// <param name="actionName">动作名（日志用）</param>
        /// <returns>下发成功返回 true</returns>
        private bool TaMoveTo(double posMm, string actionName)
        {
            DeltaMontionAxis axis = TaAxis;
            if (axis == null)
            {
                Log(actionName + " 失败 TA 轴未装配", MessageLevel.Error);
                return false;
            }

            if (!Manager.TryBeginAxisInstruction(axis.AxisName, actionName, out CancellationToken token, out string rejectReason))
            {
                Log(actionName + " 被拒 " + rejectReason, MessageLevel.Warning);
                return false;
            }

            try
            {
                if (!axis.IsServoOn()) axis.ServoOn();
                if (!axis.AbsMove(posMm, 1))
                {
                    Log(actionName + " TA 移动指令下发失败 原因 " + axis.LastCommand, MessageLevel.Error);
                    return false;
                }
                Log(string.Format("{0} TA 目标 {1:F3}mm 已下发，进入状态检测", actionName, posMm));

                bool ok = Manager.WaitAxisAtTarget(axis, posMm, MontionManager.InstructionDefaultTolMm,
                    token, out string evidence);
                if (ok)
                {
                    Log(actionName + " 状态检测达成 " + evidence);
                    return true;
                }
                Log(actionName + " 状态检测失败 " + evidence + "；已减速停止", MessageLevel.Error);
                try { axis.AxisStop((ushort)AdaWeldSystem.MotionControl.IMotion.StopMode.Deceleration); }
                catch (Exception ex)
                {
                    // 停止指令本身失败须留痕；否则看不到轴仍在动的成因
                    Log(actionName + " 减速停止下发异常 " + ex.GetType().Name + " " + ex.Message, MessageLevel.Warning);
                }
                return false;
            }
            finally
            {
                Manager.EndAxisInstruction(axis.AxisName);
            }
        }

        /// <summary>TA 力矩参数解析（TA_Auto 与 TA_Retract 共用）</summary>
        /// <remarks>力值大小 / 方向 / 100% 基准 / 爬升速率校验并换算 CST 千分比。</remarks>
        /// <param name="actionName">指令名（日志用）</param>
        /// <param name="reverse">true=按 TA_Auto 的反方向出力（TA_Retract 回缩）</param>
        /// <param name="axis">TA 轴（输出）</param>
        /// <param name="axisCfg">TA 轴配置（输出）</param>
        /// <param name="torque">带符号目标扭矩（额定千分比，输出）</param>
        /// <param name="forceMag">力值大小（N，输出）</param>
        /// <param name="torqueDirection">实际出力方向（+1 / -1，输出）</param>
        /// <returns>参数全部合法返回 true；否则已打印可读拒绝原因</returns>
        private bool TryResolveTaTorque(string actionName, bool reverse,
            out DeltaMontionAxis axis, out DeltaAxisConfig axisCfg,
            out short torque, out double forceMag, out int torqueDirection)
        {
            axis = TaAxis;
            axisCfg = TaAxisConfig;
            torque = 0;
            forceMag = 0.0;
            torqueDirection = 1;
            if (axis == null || axisCfg == null)
            {
                Log(actionName + " 失败 TA 轴未装配或配置缺失", MessageLevel.Error);
                return false;
            }
            WeldAlgorithmsConfig taCfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (taCfg == null)
            {
                Log(actionName + " 失败 TA 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            if (taCfg.TaForceN < 0)
            {
                Log(string.Format("{0} 被拒 力值只填大小（当前 {1:F2}N 为负），请改为正值并把方向设到 系统设置 → TA 电机 → 参数 → 扭矩方向",
                    actionName, taCfg.TaForceN), MessageLevel.Warning);
                return false;
            }
            if (taCfg.TaForceN == 0)
            {
                Log(actionName + " 被拒 下压力大小为 0（方向在 TA 参数弹窗单独设置，力值此处必须 > 0），台达 CST 目标扭矩 0 属非法参数，请先在工艺设置 TA 参数区填写下压力大小", MessageLevel.Warning);
                return false;
            }
            forceMag = taCfg.TaForceN;
            DeltaTaHomeConfig taHome = Manager.TaHomeConfig;
            int autoDirection = taHome == null || taHome.TorqueDirection < 0 ? -1 : 1;
            torqueDirection = reverse ? -autoDirection : autoDirection;
            double force100 = taHome == null ? 0.0 : taHome.Force100PctN;
            if (!(force100 > 0.0))
            {
                Log(string.Format("{0} 被拒 100% 力量基准非法（TA_FORCE_100PCT_N = {1}N），请先在系统设置 TA 参数填写大于 0 的基准（⛔ 不按 1:1 力值当千分比兜底）",
                    actionName, force100), MessageLevel.Warning);
                return false;
            }
            if (forceMag > force100)
            {
                Log(string.Format("{0} 被拒 力值越界 下压力大小 {1:F2}N 超过 100% 力量基准 {2:F1}N（超出额定能力，拒绝而非静默限幅）",
                    actionName, forceMag, force100), MessageLevel.Warning);
                return false;
            }
            double perMille = Math.Round(forceMag / force100 * 1000.0, MidpointRounding.AwayFromZero);
            double signedPerMille = torqueDirection * perMille;
            short signedTorque = signedPerMille > 1000.0 ? (short)1000 : (signedPerMille < -1000.0 ? (short)-1000 : (short)signedPerMille);
            if (signedTorque == 0)
            {
                Log(string.Format("{0} 被拒 换算后为 0 力值大小 {1:F4}N 低于 1‰ 分辨率 {2:F4}N（基准 {3:F1}N，按最接近档取整不静默吞掉）",
                    actionName, forceMag, force100 / 1000.0, force100), MessageLevel.Warning);
                return false;
            }

            // 目标超扭矩上限即按上限下发，不算已出力
            int torqueLimit = taHome == null ? 1000 : taHome.GetTorqueLimitPerMille();
            int targetMagnitude = Math.Abs((int)signedTorque);
            if (targetMagnitude > torqueLimit)
            {
                Log(string.Format("{0} 目标 {1}‰ 超出扭矩上限 {2}‰（TA_TORQUE_LIMIT_N = {3:F2}N），已按上限下发",
                    actionName, signedTorque, torqueLimit, taHome == null ? 0.0 : taHome.TorqueLimitN),
                    MessageLevel.Warning);
                signedTorque = (short)(torqueDirection * torqueLimit);
            }
            if (!_taForceScaleBasisLogged)
            {
                _taForceScaleBasisLogged = true;
                Log(string.Format("{0} 换算口径 100% 力量基准 {1:F1}N 按 ROOT/TA_HOME/TA_FORCE_100PCT_N 配置；"
                    + "基准 = 额定电流 × 推力常数（台达 PM46/PM49 口径）；CST 千分比按额定扭矩线性折算，仅在线性区成立、不含摩擦",
                    actionName, force100), MessageLevel.Info);
            }
            if (axisCfg.TorqueSlope == 0)
            {
                Log(actionName + " 被拒 扭矩爬升速率为 0（轴配置 TORQUE_SLOPE 未设置），请到 系统设置 → TA 电机 → 参数 弹窗填写 1~65535", MessageLevel.Warning);
                return false;
            }
            torque = signedTorque;
            return true;
        }

        /// <summary>TA 预设位移动（Pose1/Pose2 共用，仅目标坐标与动作名不同）。</summary>
        /// <param name="pose1">true=Pose1，false=Pose2</param>
        /// <returns>下发成功返回 true</returns>
        private bool TaMoveToPreset(bool pose1)
        {
            string actionName = pose1 ? "TA Pose1" : "TA Pose2";
            if (!CheckTaAvailable(actionName)) return false;
            ExitTaTorque(actionName);
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log(actionName + " 失败 TA 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return TaMoveTo(pose1 ? cfg.TaPose1Mm : cfg.TaPose2Mm, actionName);
        }

        /// <summary>TA 力矩指令实现</summary>
        /// <remarks>两条分支不同构，合并须逐字保留差异。
        /// Retract 先读起点，日志措辞也不同。</remarks>
        /// <param name="command">指令类型</param>
        /// <returns>受理并下发成功返回 true</returns>
        private bool ExecuteTaTorque(TaCommand command)
        {
            string actionName;
            string instructionName;
            bool retract;
            switch (command)
            {
                case TaCommand.Retract:
                    actionName = "TA Retract";
                    instructionName = "TA_Retract";
                    retract = true;
                    break;
                default:
                    actionName = "TA auto";
                    instructionName = "TA_Auto";
                    retract = false;
                    break;
            }

            if (!CheckTaAvailable(actionName)) return false;
            if (!TryResolveTaTorque(actionName, retract, out DeltaMontionAxis axis, out DeltaAxisConfig axisCfg, out short torque, out double forceMag, out int torqueDirection))
                return false;
            if (!axis.IsServoOn()) axis.ServoOn();
            if (!axis.IsServoOn())
            {
                Log(actionName + " 被拒 伺服使能失败 " + Manager.DescribeAxisStatusWord(axis), MessageLevel.Error);
                return false;
            }

            if (!Manager.TryBeginAxisInstruction(axis.AxisName, instructionName, out CancellationToken token, out string rejectReason))
            {
                Log(actionName + " 被拒 " + rejectReason, MessageLevel.Warning);
                return false;
            }

            double force100 = Manager.TaHomeConfig == null ? 0.0 : Manager.TaHomeConfig.Force100PctN;
            try
            {
                // Retract 先取起点用于日志；Auto 不读位置（保持原行为，不多调一次 GetPosition）
                double startMm = retract ? axis.GetPosition() : 0.0;
                if (!axis.TorqueOutputEnabled)
                {
                    Log(actionName + " 切换顺序 冷启动=停稳位置运动 → 切 CST(10) → CST_Start_Move(目标, 斜率) → CST_Output_Enable(1)；热切换=直接 CST_Start_Move(目标, 斜率)；反号=撤输出→锁位切 CSP 位置 Stop→切 CST 写反向目标→开输出");
                }
                if (!axis.SetTorque(torque, axisCfg.TorqueSlope))
                {
                    Log(actionName + " 力矩指令下发失败 详见前一条 CST 拦截原因", MessageLevel.Error);
                    return false;
                }
                switch (command)
                {
                    case TaCommand.Retract:
                        Log(string.Format("TA Retract 力矩回缩中 起点 {0:F3}mm 力值 {1:F1}N 方向 {2}（{3}，与 TA_Auto 相反） / 100%基准 {4:F1}N / 目标力矩 {5}‰ / 扭矩爬升速率 {6}（0.1%/s）",
                            startMm, forceMag, torqueDirection < 0 ? "-" : "+", torqueDirection < 0 ? "反向" : "正向",
                            force100, torque, axisCfg.TorqueSlope));
                        break;
                    default:
                        Log(string.Format("TA auto 力值 {0:F1}N 方向 {1}（{2}） / 100%基准 {3:F1}N / 目标力矩 {4}‰ / 扭矩爬升速率 {5}（0.1%/s）",
                            forceMag, torqueDirection < 0 ? "-" : "+", torqueDirection < 0 ? "反向" : "正向", force100, torque, axisCfg.TorqueSlope));
                        break;
                }

                bool ok = Manager.WaitTaTorqueOn(axis, token, out string evidence);
                switch (command)
                {
                    case TaCommand.Retract:
                        Log("TA Retract 力矩生效" + (ok ? "达成 " : "检测失败 ") + evidence,
                            ok ? MessageLevel.Info : MessageLevel.Error);
                        break;
                    default:
                        Log("TA auto 状态检测 " + (ok ? "达成 " : "失败 ") + evidence,
                            ok ? MessageLevel.Info : MessageLevel.Error);
                        break;
                }
                if (!ok)
                {
                    axis.StopTorque();
                    if (retract) Log("TA Retract 力矩生效检测失败 已撤力矩输出并回 CSP", MessageLevel.Error);
                    else Log("TA auto 状态检测失败 已撤力矩输出并回 CSP", MessageLevel.Error);
                }
                return ok;
            }
            finally
            {
                Manager.EndAxisInstruction(axis.AxisName);
            }
        }

        /// <summary>TA break：停止力矩运动并锁在发命令时刻的位置。</summary>
        /// <returns>受理并执行成功返回 true</returns>
        private bool ExecuteTaBreak()
        {
            if (!CheckTaAvailable("TA break")) return false;
            DeltaMontionAxis axis = TaAxis;
            if (axis == null)
            {
                Log("TA break 失败 TA 轴未装配", MessageLevel.Error);
                return false;
            }
            if (!axis.TorqueOutputEnabled)
            {
                Log("TA break 仅在 TA auto 力矩输出态生效，当前非力矩输出态", MessageLevel.Warning);
                return false;
            }
            if (!Manager.TryBeginAxisInstruction(axis.AxisName, "TA_Break", out CancellationToken token, out string rejectReason))
            {
                Log("TA break 被拒 " + rejectReason, MessageLevel.Warning);
                return false;
            }

            try
            {
                double holdPosMm = axis.GetPosition();
                Log(string.Format("TA break 切换顺序 先锁存位置 {0:F3}mm → 撤 CST 输出 → 回 CSP 保持", holdPosMm));
                if (!axis.StopTorque())
                {
                    Log("TA break 撤 CST 输出失败", MessageLevel.Error);
                    return false;
                }
                if (!axis.AbsMove(holdPosMm, 1))
                {
                    Log("TA break 位置保持指令下发失败", MessageLevel.Error);
                    return false;
                }

                bool ok = Manager.WaitTaTorqueOff(axis, holdPosMm,
                    MontionManager.InstructionDefaultTolMm, token, out string evidence);
                if (ok)
                {
                    lock (_taLock)
                    {
                        _taBreakHoldMm = holdPosMm;
                        _taBreakHoldValid = true;
                    }
                    Log("TA break 状态检测达成 " + evidence);
                }
                else
                {
                    Log("TA break 状态检测失败 " + evidence, MessageLevel.Error);
                }
                return ok;
            }
            finally
            {
                Manager.EndAxisInstruction(axis.AxisName);
            }
        }

        /// <summary>TA ZeroPose：走到 TA 工作零位（CSP 绝对定位，⛔ 不是回零 Home）</summary>
        /// <remarks>绝对编码器掉电保留位置，无需 Home_Move。手动点击与初始化第 3 步共用；阻塞到出结果，
        /// 单飞登记与运行态由唯一 finally 释放。</remarks>
        /// <returns>到位返回 true，失败/超时/被停止打断返回 false</returns>
        private bool ExecuteTaZeroPose()
        {
            // ⛔ 本指令不受「工作零位已到位」限制（否则降级后无法重试）；
            // 但 2026-11-17 起与其它 TA 指令同口径：**必须已回零**（位置可信才谈得上定位到工作零位）
            if (!CheckTaAvailable("TA ZeroPose")) return false;
            if (_taZeroPoseRunning)
            {
                Log("TA ZeroPose 被拒 上一次工作零位定位仍在执行", MessageLevel.Warning);
                return false;
            }

            DeltaMontionAxis axis = TaAxis;
            if (axis == null || Manager.MoveData?.MotionConfig?.TaHome == null)
            {
                Log("TA ZeroPose 失败 TA 轴未装配或 TA_HOME 配置缺失", MessageLevel.Error);
                return false;
            }
            if (!Manager.TryBeginAxisInstruction(axis.AxisName, "TA_ZeroPose", out CancellationToken token, out string rejectReason))
            {
                Log("TA ZeroPose 被拒 " + rejectReason, MessageLevel.Warning);
                return false;
            }

            try
            {
                // 开始即上报：已在位分支也产出完整状态序列；界面不空等
                MarkTaZeroPoseRunning(true);
                if (axis.TorqueOutputEnabled)
                {
                    Log("TA ZeroPose 切换顺序 先撤 TA_Auto 力矩输出（撤 CST → 回 CSP）→ 再走工作零位");
                }
                ExitTaTorque("TA ZeroPose");

                // 速度与容差复用同一来源（轴 MaxVel + 通用到位容差）；不再单独配置
                double vel = axis.MaxVel > 0 ? axis.MaxVel : 1.0;
                double tol = MontionManager.InstructionDefaultTolMm;
                double targetMm = Manager.TaZeroPoseTargetMm;   // 坐标唯一来源：工艺设置页 TA 区 TA_ZERO_POSE_MM
                Log(string.Format("TA ZeroPose 目标 {0:F3}mm 速度 {1:F3}mm/s 容差 {2:F3}mm",
                    targetMm, vel, tol));

                if (!Manager.TryMoveAbsoluteNoBlock(axis, targetMm, vel, tol, out bool alreadyInPosition, out string reason))
                {
                    MarkTaZeroPoseFailed(reason);
                    return false;
                }
                if (alreadyInPosition)
                {
                    // 已在位同样是成功：写回零位就绪，指令立即重开
                    MarkTaZeroPoseSucceeded();
                    Log(string.Format("TA ZeroPose 已在位 {0:F3}mm 容差 {1:F3}mm", targetMm, tol));
                    return true;
                }

                // 超时按「行程/速度 × 3、下限/上限取配置」估算；无变化早停沿用 FLOW_TIMEOUT 配置
                FlowTimeoutConfig flow = FlowTimeoutSettings.Current;
                double expectSeconds = Math.Min(flow.PositionReachMaxSeconds,
                    Math.Max(flow.PositionReachMinSeconds,
                        Math.Abs(axis.GetPosition() - targetMm) / Math.Max(vel, 0.01) * 3.0));
                if (Manager.WaitAxisAtTarget(axis, targetMm, tol, expectSeconds,
                    flow.PositionReachNoChangeSeconds, () => token.IsCancellationRequested, out reason))
                {
                    MarkTaZeroPoseSucceeded();
                    Log(string.Format("TA 零位 完成 目标 {0:F3}mm 容差 {1:F3}mm", targetMm, tol));
                    return true;
                }

                Manager.StopAxis(axis);
                // 急停打断与超时一律按失败上色；⛔ 不与从未执行同形
                MarkTaZeroPoseFailed(reason);
                return false;
            }
            catch (Exception ex)
            {
                MarkTaZeroPoseFailed("TA ZeroPose 异常 " + ex.Message);
                return false;
            }
            finally
            {
                MarkTaZeroPoseRunning(false);
                Manager.EndAxisInstruction(axis.AxisName);
            }
        }

        // —— 轴动作入口（UI 只调用、不持轴对象）：回零 / 绝对移动 ——

        /// <summary>按指令取 TA 门禁原因（就绪返回空串）</summary>
        /// <remarks>2026-11-17 改版：<b>allowZeroPose 不再放宽</b>——TA_Zero 与其它 TA 指令同口径，
        /// 一律要求「已使能 + 已回零」（位置可信）；「工作零位到位」仍只是观测事实，不是门禁。</remarks>
        /// <param name="allowZeroPose">true=按 TA_ZeroPose 判据（<b>2026-11-17 起与其它指令同判</b>）</param>
        /// <returns>不可执行原因；空串表示放行</returns>
        public string GetTaGateReason(bool allowZeroPose)
        {
            if (Manager == null) return "运动管理器未初始化";
            if (!Manager.IsInitCompleted) return "初始化未完成（使能阶段进行中）";
            if (Manager.TaAvailable) return string.Empty;
            string reason = Manager.TaDegradedReason;
            // 观测事实：工作零位未到位（不阻断 TA 指令，仅提示当前坐标基准）
            if (!Manager.TaAtWorkZero)
            {
                string zeroReason = Manager.TaZeroPoseReason;
                reason = reason + "；工作零位未到位：" + (string.IsNullOrEmpty(zeroReason) ? "未知" : zeroReason);
            }
            return reason;
        }

        /// <summary>TA 指令统一入口</summary>
        /// <remarks>力矩与定位类均阻塞到结果；ZeroPose 同样阻塞到到位或超时。</remarks>
        /// <param name="command">指令类型</param>
        /// <returns>执行成功返回 true</returns>
        public bool TaExecute(TaCommand command)
        {
            // 自动焊接流程进行中 ⇒ 拒绝一切 TA 手动指令（手动与生产流程互斥）
            if (!CheckAutoWeldingConflict("TA " + command)) return false;
            switch (command)
            {
                case TaCommand.Auto:
                case TaCommand.Retract:
                    return ExecuteTaTorque(command);
                case TaCommand.Break:
                    return ExecuteTaBreak();
                case TaCommand.Pose1:
                    return TaMoveToPreset(true);
                case TaCommand.Pose2:
                    return TaMoveToPreset(false);
                case TaCommand.ZeroPose:
                    return ExecuteTaZeroPose();
                default:
                    return false;
            }
        }

        /// <summary>初始化闸门打开后执行一次 TA 工作零位</summary>
        /// <remarks>阻塞执行；失败即降级，允许后续手动重试。
        /// 初始化闸门、可动、装配与并发判定均由 <see cref="ExecuteTaZeroPose"/> 负责；此处不重复判。</remarks>
        /// <returns>到位返回 true</returns>
        public bool RunTaZeroPoseAfterInit()
        {
            return ExecuteTaZeroPose();
        }

        /// <summary>回退开关：初始化末尾自动回零（默认关闭）</summary>
        /// <remarks>初始化只做连接与使能，回零归复位入口（N01）。
        /// 配置键 INIT_RUN_TA_HOME=1 时恢复旧行为，仅作阶段 3 回退路径。</remarks>
        /// <returns>开关关闭或回零成功返回 true；开启且回零失败返回 false</returns>
        public bool RunInitAutoHomeIfEnabled()
        {
            if (ReadInt("INIT_RUN_TA_HOME", 0) != 1) return true;
            Log("回退开关 INIT_RUN_TA_HOME 已开启 初始化末尾执行回零与工作零位", MessageLevel.Warning);
            return RunTaHomeThenZeroPose();
        }

        /// <summary>复位门槛校验：已装配轴须达 L1</summary>
        /// <returns>达到 L1 返回 true</returns>
        private bool EnsureResetReadiness()
        {
            if (Manager == null || !Manager.IsInitialized)
            {
                Log("复位未执行 运动管理器未初始化", MessageLevel.Warning);
                return false;
            }
            // 按轴能力分支：需回零轴看 TA，免回零轴看 Y；禁按轴名特判
            if (HasHomingRequiredAxis && !Manager.TaMotionReady)
            {
                Log("复位未执行 就绪不足 当前 L0 需 L1 原因 TA " + Manager.TaDegradedReason,
                    MessageLevel.Warning);
                return false;
            }
            if (HasFreeHomingAxis && !Manager.YReady)
            {
                Log("复位未执行 就绪不足 当前 L0 需 L1 原因 Y " + Manager.YGateReason,
                    MessageLevel.Warning);
                return false;
            }
            return true;
        }

        /// <summary>轴复位动作：线性阻塞，落到管理器</summary>
        /// <remarks>免回零轴跳过 Home 只去工作零位；需回零轴先 Home 再去工作零位。
        /// 失败停在 L2 或 L3，可再次复位重试。</remarks>
        /// <returns>全部到位返回 true</returns>
        private bool ResetAxesToWorkZero()
        {
            if (HasHomingRequiredAxis)
            {
                // 需回零轴先回零，未回零不得进工作零位
                if (!Manager.TaHomed)
                {
                    if (!TryHomeAxisWithWait(MontionManager.TaAxisKeys, "复位回零", out string taAxisName,
                            out _, out string rejectDetail, out string evidence))
                    {
                        string why = string.IsNullOrEmpty(evidence) ? rejectDetail : evidence;
                        Log("复位停止 回零失败 轴 " + taAxisName + " 原因 " + why + " 停在 L2 可重试",
                            MessageLevel.Warning);
                        return false;
                    }
                    Log("复位回零 " + taAxisName + " 成功 " + evidence);
                }
                if (!ExecuteTaZeroPose())
                {
                    Log("复位停止 工作零位未到位 停在 L3 可重试", MessageLevel.Warning);
                    return false;
                }
            }

            if (HasFreeHomingAxis)
            {
                // 免回零轴跳过回零，只去工作零位
                double targetMm = WeldParamManager.Instance.AlgorithmsConfig.TeachLockYMm;
                if (!TryMoveAxisToTarget(MontionManager.SeamTrackYKeys, targetMm, "复位回工作零位",
                        out string yAxisName, out _, out string yDetail, out string yEvidence))
                {
                    string why = string.IsNullOrEmpty(yEvidence) ? yDetail : yEvidence;
                    Log("复位停止 工作零位未到位 轴 " + yAxisName + " 目标 "
                        + targetMm.ToString("F3") + "mm 原因 " + why, MessageLevel.Warning);
                    return false;
                }
                Log("复位工作零位达成 " + yAxisName + " 目标 " + targetMm.ToString("F3") + "mm");
            }

            Log("复位完成 至工作零位");
            return true;
        }

        /// <summary>是否装配了需回零轴（能力判据，不按轴名特判）</summary>
        private bool HasHomingRequiredAxis { get { return TaAxis != null; } }

        /// <summary>是否装配了免回零轴（绝对编码器，能力判据）</summary>
        private bool HasFreeHomingAxis { get { return HasAbsoluteEncoder && HasSeamTrackAxis; } }

        /// <summary>初始化收尾：先满扭矩回零，再走 TA 工作零位</summary>
        /// <remarks>伺服下电后位置不可信。未回零不得直接定位。
        /// 回零失败即降级，不继续 ZeroPose；失败原因随日志给出。</remarks>
        /// <returns>回零与 ZeroPose 均达成返回 true</returns>
        public bool RunTaHomeThenZeroPose()
        {
            if (!TryHomeAxisWithWait(MontionManager.TaAxisKeys, "初始化回零", out string axisName,
                    out _, out string rejectDetail, out string evidence))
            {
                string why = string.IsNullOrEmpty(evidence) ? rejectDetail : evidence;
                Log("初始化回零 " + axisName + " 失败 " + why, MessageLevel.Warning);
                return false;
            }
            Log("初始化回零 " + axisName + " 成功 " + evidence, MessageLevel.Info);
            return ExecuteTaZeroPose();
        }

        /// <summary>按轴角色下发回零并等待到位（UI 应用动作入口）</summary>
        /// <param name="roleKeys">轴名角色关键字</param>
        /// <param name="actionName">动作名（单飞隔离键与日志用）</param>
        /// <param name="axisName">命中的轴名；未装配为空串</param>
        /// <param name="rejectCode">拒绝/失败码；空串表示已执行且达成</param>
        /// <param name="rejectDetail">拒绝细节（按轴门禁原因 / 单飞原因 / 停止异常）</param>
        /// <param name="evidence">状态检测证据</param>
        /// <returns>达成返回 true</returns>
        public bool TryHomeAxisWithWait(string[] roleKeys, string actionName,
            out string axisName, out string rejectCode, out string rejectDetail, out string evidence)
        {
            axisName = string.Empty;
            rejectCode = string.Empty;
            rejectDetail = string.Empty;
            evidence = string.Empty;

            if (Manager == null || !Manager.IsInitialized)
            {
                rejectCode = AxisRejectNotInitialized;
                return false;
            }
            // 自动焊接流程进行中 ⇒ 拒绝手动轴动作（手动与生产流程互斥）
            if (DeviceControlWork.Instance.IsAutoWeldingRunning)
            {
                rejectCode = AxisRejectAutoWelding;
                rejectDetail = "自动焊接流程进行中，手动轴操作与生产流程互斥";
                return false;
            }
            DeltaMontionAxis axis = Manager.GetDeltaAxisByKeys(roleKeys);
            if (axis == null)
            {
                rejectCode = AxisRejectAxisNotAssigned;
                return false;
            }
            axisName = axis.AxisName;

            string axisGate = Manager.GetAxisHomingGateReason(axis);
            if (axisGate.Length > 0)
            {
                rejectCode = AxisRejectAxisNotReady;
                rejectDetail = axisGate;
                evidence = "回零未下发 门禁拦截：" + axisGate;
                return false;
            }

            if (!Manager.TryBeginAxisInstruction(axis.AxisName, actionName, out CancellationToken token, out string beginReason))
            {
                rejectCode = AxisRejectBusy;
                rejectDetail = beginReason;
                evidence = "回零未下发 轴忙：" + beginReason;
                return false;
            }
            try
            {
                // 回零必须等驱动器确认，未启动/超时一律中止
                if (!Manager.HomeAxisAndWait(axis))
                {
                    rejectCode = AxisRejectNotReached;
                    rejectDetail = "回零未完成（未启动或超时），已中止后续定位";
                    evidence = axis.HomeEvidence;
                    try { Manager.StopAxis(axis); }
                    catch (Exception ex)
                    {
                        rejectDetail = rejectDetail + " " + ex.GetType().Name;
                    }
                    return false;
                }
                bool ok = Manager.WaitAxisHomed(axis, token, out evidence);
                if (ok)
                {
                    evidence = axis.HomeEvidence;
                }
                else
                {
                    rejectCode = AxisRejectNotReached;
                    if (string.IsNullOrEmpty(evidence)) evidence = axis.HomeEvidence;
                    try { Manager.StopAxis(axis); }
                    catch (Exception ex)
                    {
                        rejectDetail = ex.GetType().Name + " " + ex.Message;
                    }
                }
                return ok;
            }
            finally
            {
                Manager.EndAxisInstruction(axis.AxisName);
            }
        }

        /// <summary>按轴角色下发绝对移动并等待到位</summary>
        /// <remarks>UI 应用动作入口</remarks>
        /// <param name="roleKeys">轴名角色关键字</param>
        /// <param name="targetMm">目标位置（mm）</param>
        /// <param name="actionName">动作名（单飞隔离键与日志用）</param>
        /// <param name="axisName">命中的轴名；未装配为空串</param>
        /// <param name="rejectCode">拒绝/失败码；空串表示已执行且达成</param>
        /// <param name="rejectDetail">拒绝细节</param>
        /// <param name="evidence">状态检测证据</param>
        /// <returns>达成返回 true</returns>
        public bool TryMoveAxisToTarget(string[] roleKeys, double targetMm, string actionName,
            out string axisName, out string rejectCode, out string rejectDetail, out string evidence)
        {
            axisName = string.Empty;
            rejectCode = string.Empty;
            rejectDetail = string.Empty;
            evidence = string.Empty;

            if (Manager == null || !Manager.IsInitialized)
            {
                rejectCode = AxisRejectNotInitialized;
                return false;
            }
            // 自动焊接流程进行中 ⇒ 拒绝手动轴动作（手动与生产流程互斥）
            if (DeviceControlWork.Instance.IsAutoWeldingRunning)
            {
                rejectCode = AxisRejectAutoWelding;
                rejectDetail = "自动焊接流程进行中，手动轴操作与生产流程互斥";
                return false;
            }
            DeltaMontionAxis axis = Manager.GetDeltaAxisByKeys(roleKeys);
            if (axis == null)
            {
                rejectCode = AxisRejectAxisNotAssigned;
                return false;
            }
            axisName = axis.AxisName;

            string axisGate = Manager.GetAxisGateReason(axis);
            if (axisGate.Length > 0)
            {
                rejectCode = AxisRejectAxisNotReady;
                rejectDetail = axisGate;
                return false;
            }

            if (!Manager.TryBeginAxisInstruction(axis.AxisName, actionName, out CancellationToken token, out string beginReason))
            {
                rejectCode = AxisRejectBusy;
                rejectDetail = beginReason;
                return false;
            }
            try
            {
                if (!Manager.MoveTo(axis, targetMm))
                {
                    rejectCode = AxisRejectCommandFailed;
                    return false;
                }
                bool ok = Manager.WaitAxisAtTarget(axis, targetMm,
                    MontionManager.InstructionDefaultTolMm, token, out evidence);
                if (!ok)
                {
                    rejectCode = AxisRejectNotReached;
                    try { Manager.StopAxis(axis); }
                    catch (Exception ex)
                    {
                        rejectDetail = ex.GetType().Name + " " + ex.Message;
                    }
                }
                return ok;
            }
            finally
            {
                Manager.EndAxisInstruction(axis.AxisName);
            }
        }

        #endregion

        #region 可观测性与释放

        /// <summary>执行步号转中文名。</summary>
        /// <remarks>新增步必须登记，否则该步不可观测。</remarks>
        /// <param name="step">执行步号</param>
        /// <returns>步号对应中文名；未登记的步返回「未登记步(N)」</returns>
        protected override string GetStepName(int step)
        {
            switch (step)
            {
                case StepIdle: return "空闲待机";
                case StepServoOn: return "PreWork-伺服使能(小步11)";
                case StepReturnToCal: return "PreWork-标定位回归(小步12)";
                case StepReady: return "已使能待机(大步20)";
                case StepStopping: return "停止收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        /// <summary>按轴角色执行手动操作</summary>
        /// <param name="roleKeys">角色关键字表（<see cref="MontionManager.SeamTrackYKeys"/> / <see cref="MontionManager.TaAxisKeys"/>）</param>
        /// <param name="operation">手动操作种类</param>
        /// <param name="value">MoveTo 目标 / StepMove 位移（其余传 0）</param>
        /// <param name="dir">JogStart 方向（1=负向 0=正向，其余传 0）</param>
        /// <param name="axisName">命中轴名（日志用，未命中为空串）</param>
        /// <param name="servoOn">ToggleServo 执行后的使能状态（其余为 false）</param>
        /// <param name="commandOk">指令下发结果；前置拒绝时为 false</param>
        /// <param name="rejectReason">前置拒绝原因；为空串表示前置通过（此时只看 <paramref name="commandOk"/>）</param>
        /// <returns>前置是否通过（true = 已执行，结果看 <paramref name="commandOk"/>）</returns>
        /// <remarks>分层：应用动作归属本工作流；Manager 只提供按角色取轴、初始化门禁与单轴基础指令。
        /// 门禁与迁移前逐条一致：① 运控已初始化；② 角色轴已装配（配置启用且轴名命中关键字）。
        /// 其余保护（使能、PreMotionCheck、急停）仍在轴实现内，未被绕过。
        /// UI 唯一入口：UI 只表达角色与动作，不持有轴对象。
        /// 2026-11-17 改版：① MoveTo / StepMove 改走非阻塞下发（<see cref="MontionManager.MoveToNoBlock"/>），
        /// 本方法在 UI 线程调用不再等待到位；② 回零不在本入口 —— 统一走 <see cref="TryHomeAxisWithWait"/>，
        /// 使「手动回零」与「复位回零」共用同一实现，TA 回零结论（已回零）不区分来源。</remarks>
        public bool TryManualAxisOperation(string[] roleKeys, AxisManualOperation operation, double value, ushort dir,
            out string axisName, out bool servoOn, out bool commandOk, out string rejectReason)
        {
            axisName = string.Empty;
            servoOn = false;
            commandOk = false;
            rejectReason = string.Empty;

            if (!Manager.IsInitialized)
            {
                rejectReason = "运控未初始化（使能阶段未完成）";
                return false;
            }

            DeltaMontionAxis axis = Manager.GetDeltaAxisByKeys(roleKeys);
            if (axis == null)
            {
                rejectReason = "轴未装配（配置未启用或轴名未匹配角色关键字），请检查运控配置";
                return false;
            }
            axisName = axis.AxisName;

            // 自动焊接流程进行中 ⇒ 拒绝手动轴操作（手动与生产流程互斥）。
            // ⛔ 停止类（Stop / JogStop）永远放行：安全操作必须随时可中断在途运动。
            bool isStopOperation = operation == AxisManualOperation.Stop
                || operation == AxisManualOperation.JogStop;
            if (!isStopOperation && DeviceControlWork.Instance.IsAutoWeldingRunning)
            {
                rejectReason = "自动焊接流程进行中，手动轴操作与生产流程互斥";
                return false;
            }

            switch (operation)
            {
                case AxisManualOperation.ToggleServo:
                    servoOn = Manager.ToggleServo(axis);
                    commandOk = true;
                    return true;
                case AxisManualOperation.Stop:
                case AxisManualOperation.JogStop:
                    Manager.StopAxis(axis);
                    commandOk = true;
                    return true;
                case AxisManualOperation.MoveTo:
                    commandOk = Manager.MoveToNoBlock(axis, value);
                    return true;
                case AxisManualOperation.StepMove:
                    commandOk = Manager.StepMoveNoBlock(axis, value);
                    return true;
                case AxisManualOperation.JogStart:
                    Manager.JogStart(axis, dir);
                    commandOk = true;
                    return true;
                default:
                    rejectReason = "未定义的手动操作 " + operation;
                    return false;
            }
        }

        /// <summary>释放钩子。</summary>
        /// <remarks>基类 Dispose 调用；关闭台达总线统一走 InitializeOff，保持唯一入口与幂等。</remarks>
        protected override void DisposeManaged()
        {
            InitializeOff();
        }

        #endregion
    }
}
