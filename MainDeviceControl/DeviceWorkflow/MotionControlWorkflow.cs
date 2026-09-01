﻿using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.ZMotion;
using AdaWeldSystem.ProductFileManager;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>
    /// 运动控制工作流（单例）：继承统一泛型基类 DeviceWorkflowBase&lt;LaserCamBoxWorkflowState&gt;。
    /// CAMBOX 跟踪归入运控，由本类统一承载。
    /// 硬件级实时焊缝跟踪：CAMBOX 电子凸轮由硬件总线完成，PC 侧仅做启停编排与轨迹规划。
    ///
    /// 角色：启动/停止编排器（不参与实时运动控制）。
    /// 实时运动完全由硬件 CAMBOX 完成，本工作流仅做非实时的编排与监控：
    ///   1. 启动流程：清零编码器轴 → 启动 CAMBOX → 等待主轴运动
    ///   2. 运行监控：周期（50Hz）刷新跟踪/编码器状态并同步流程态
    ///   3. 轨迹写入：接收激光帧 → 时间戳插值 → 写入 CAMBOX 表（非实时）
    ///   4. 停止流程：停止 CAMBOX → 复位从轴 → 状态回到 Idle
    ///   5. 异常处理：记录跟踪引擎异常日志（不转发子设备事件，订阅方直接订阅 Tracker）
    ///
    /// 步骤驱动（ADR-047）：执行步段位 10 清零编码器 → 20 启动CAMBOX → 30 等主轴
    /// → 100 跟踪中 / 110 暂停中 → 700 停止收尾 / 900 失败收尾。
    /// 原独立后台监控线程（MonitorLoop，50Hz）已并入基类常驻监听线程，
    /// 节拍由 BeatMs=20 承载，状态刷新在 FlowProcess 每拍开头完成。
    ///
    /// 权责边界（ADR-042）：流程不转发 CamBoxTracker 的事件，不产出设备数据；
    /// 需要跟踪点/跟踪异常的订阅方直接订阅 Tracker 属性暴露的 CamBoxTracker。
    /// 非 UI 类，释放走基类 Dispose → 子类 DisposeManaged 钩子（ADR-001：非 UI 类不走 DisposeComponents）。
    /// </summary>
    public class MotionControlWorkflow : DeviceWorkflowBase<LaserCamBoxWorkflowState>
    {
        #region 常量

        private const string Tag = "运动控制工作流";

        // 执行步段位（0 待机 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepResetEncoder = 10;
        private const int StepStartCamBox = 20;
        private const int StepWaitMaster = 30;
        private const int StepTracking = 100;
        private const int StepPaused = 110;
        private const int StepStopping = 700;

        // 超时阈值（毫秒）
        private const int CamBoxStartTimeoutMs = 5000;
        private const int MasterWaitTimeoutMs = 120000;
        private const int PausedTimeoutMs = 30000;

        #endregion

        #region 私有变量

        // ---- 核心组件 ----
        private CamBoxTracker _tracker;
        private EncoderAxis _encoderAxis;
        private RSIPositionBuffer _positionBuffer;

        // 流程态 _step 由泛型基类承载；本类仅持 Idle 两义区分标记
        /// <summary>是否已完成初始化（用于区分 Idle 是"未连接就绪"还是"已连接就绪"）。</summary>
        private volatile bool _initialized;

        private readonly object _lock = new object();

        #endregion

        #region 单例

        private static readonly Lazy<MotionControlWorkflow> _lazyInstance =
            new Lazy<MotionControlWorkflow>(() => new MotionControlWorkflow());

        public static MotionControlWorkflow Instance => _lazyInstance.Value;

        #endregion

        #region 公共变量

        public override string StateName => Tag;

        /// <summary>CAMBOX 跟踪引擎</summary>
        public CamBoxTracker Tracker { get { return _tracker; } }

        /// <summary>通讯编码器主轴</summary>
        public EncoderAxis EncoderAxis { get { return _encoderAxis; } }

        /// <summary>RSI 位置时间戳缓冲</summary>
        public RSIPositionBuffer PositionBuffer { get { return _positionBuffer; } }

        /// <summary>是否正在运行（Tracking 或 WaitingForMaster）</summary>
        public bool IsRunning
        {
            get
            {
                return _step == LaserCamBoxWorkflowState.Tracking
                    || _step == LaserCamBoxWorkflowState.WaitingForMaster;
            }
        }

        /// <summary>仅进程中阶段态才跑业务；空闲/报警时空转并刷新步时间戳。</summary>
        protected override bool IsRunLoopActive
        {
            get
            {
                return _step == LaserCamBoxWorkflowState.WaitingForMaster
                    || _step == LaserCamBoxWorkflowState.Tracking
                    || _step == LaserCamBoxWorkflowState.Paused
                    || _step == LaserCamBoxWorkflowState.Stopping;
            }
        }

        #endregion

        #region 构造函数

        private MotionControlWorkflow() : base(LaserCamBoxWorkflowState.Idle)
        {
            _tracker = new CamBoxTracker();
            _encoderAxis = new EncoderAxis();
            _positionBuffer = new RSIPositionBuffer();

            // 跟踪引擎异常只做记录：子设备事件不向上转发，订阅方直接订阅 Tracker（ADR-042 R2）
            _tracker.TrackingError += OnTrackerError;

            // 原独立监控线程为 50Hz，并入基类监听线程后沿用同一节拍
            BeatMs = 20;
        }

        #endregion

        #region 私有函数

        /// <summary>执行步 10：清零编码器轴（必须在 CAMBOX 启动前完成）。</summary>
        private void DoResetEncoder()
        {
            _encoderAxis.Reset();
            GoStep(StepStartCamBox);
        }

        /// <summary>执行步 20：启动 CAMBOX 跟踪。</summary>
        /// <remarks>动作原语返回 false 即留在原步，下一拍重入重试（失败重试零代码）。</remarks>
        private void DoStartCamBox()
        {
            if (!_tracker.Start())
            {
                if (!IsStepTimeout(CamBoxStartTimeoutMs)) return;
                FailFlow("CAMBOX 启动超时");
                return;
            }
            Log("CAMBOX 已启动，等待主轴运动");
            GoStep(StepWaitMaster);
        }

        /// <summary>执行步 30：等待主轴运动，超时则报警。</summary>
        /// <remarks>跟踪态由 UpdateTrackerStatus 同步，不在本步死等。</remarks>
        private void DoWaitMaster()
        {
            if (!IsStepTimeout(MasterWaitTimeoutMs)) return;
            FailFlow("等待主轴运动超时 120秒");
        }

        /// <summary>执行步 110：暂停中，超时则报警。</summary>
        /// <remarks>恢复由 UpdateTrackerStatus 同步回 Tracking。</remarks>
        private void DoPaused()
        {
            if (!IsStepTimeout(PausedTimeoutMs)) return;
            FailFlow("跟踪暂停超时 30秒");
        }

        /// <summary>执行步 700：停止收尾（停 CAMBOX → 复位从轴 → 清缓冲 → 回 Idle）。</summary>
        private void DoStopping()
        {
            if (_tracker != null) _tracker.Stop(false);
            _encoderAxis.Reset();
            _positionBuffer.Clear();
            Log("跟踪工作流已停止");
            SetStep(LaserCamBoxWorkflowState.Idle, "已停止");
        }

        /// <summary>执行步 900：失败收尾三步走。</summary>
        /// <remarks>置 Alarm 须经 SetStep 映射（禁手工赋值 State），再记日志与故障记录后回待机。</remarks>
        private void DoFinishFail()
        {
            SetStep(LaserCamBoxWorkflowState.Error, FailReason);
            IsAlarm = true;
            FaultRecoveryManager.Instance.RecordFault(Tag, new FaultRecord
            {
                Time = DateTime.Now,
                Device = Tag,
                State = MapToDeviceState(_step),
                Category = FaultCategory.Device,
                ErrorCode = "MotionStepFail",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", FailStep, FailReason),
                AutoRecovered = false,
                RecoveryAction = "停止 CAMBOX 并复位，等待人工复位"
            });
            GlobalCommData.ShowLog(Tag, string.Format("运控流程异常终止 原因 {0}", FailReason), MessageLevel.Error);
            GoStep(StepIdle);
        }

        /// <summary>失败收尾统一入口：记录失败步号与原因后跳 900。</summary>
        /// <summary>每拍刷新跟踪引擎与编码器状态，并把跟踪引擎状态同步为阶段态。</summary>
        /// <remarks>取代原独立监控线程 MonitorLoop（50Hz）。</remarks>
        private void UpdateTrackerStatus()
        {
            if (_tracker == null) return;
            try
            {
                _tracker.UpdateStatus();
                _encoderAxis.UpdateStatus();
                SyncStateFromTracker();
            }
            catch (Exception ex)
            {
                Log("状态刷新异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>将 CamBoxTracker 状态同步到工作流阶段态（执行步由 OnStepChanged 复位）。</summary>
        private void SyncStateFromTracker()
        {
            switch (_tracker.State)
            {
                case CamBoxTrackingState.Tracking:
                    if (_step != LaserCamBoxWorkflowState.Tracking)
                        SetStep(LaserCamBoxWorkflowState.Tracking, "主轴开始运动");
                    break;
                case CamBoxTrackingState.Paused:
                    if (_step != LaserCamBoxWorkflowState.Paused)
                        SetStep(LaserCamBoxWorkflowState.Paused, "激光丢帧暂停");
                    break;
                case CamBoxTrackingState.Error:
                    if (_step != LaserCamBoxWorkflowState.Error)
                        FailFlow("跟踪引擎异常");
                    break;
            }
        }

        /// <summary>跟踪引擎错误处理：仅记录日志。</summary>
        /// <remarks>异常不上抛为工作流事件——订阅方直接订阅 Tracker.TrackingError（ADR-042 R2）。</remarks>
        /// <param name="sender">事件源（跟踪引擎）</param>
        /// <param name="message">错误信息</param>
        private void OnTrackerError(object sender, string message)
        {
            Log("跟踪错误 " + message, MessageLevel.Error);
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #endregion

        #region 公共函数

        /// <summary>初始化跟踪工作流。</summary>
        /// <remarks>配置 CamBoxTracker、EncoderAxis、RSIPositionBuffer。 设备三态：成功置 Connect，失败置 Disconnect。</remarks>
        /// <param name="trackingConfig">CAMBOX 跟踪配置</param>
        /// <param name="encoderConfig">编码器轴配置</param>
        /// <param name="controllerHandle">正运动控制器句柄</param>
        /// <returns>初始化成功返回 true</returns>
        public bool Initialize(CamBoxTrackingConfig trackingConfig,
            EncoderAxisConfig encoderConfig, IntPtr controllerHandle)
        {
            if (controllerHandle == IntPtr.Zero)
            {
                Log("初始化失败 原因是控制器未连接", MessageLevel.Warning);
                return false;
            }

            lock (_lock)
            {
                SetStep(LaserCamBoxWorkflowState.Initializing, "开始初始化");

                // 1. 初始化通讯编码器主轴（ATYPE=25）
                if (!_encoderAxis.Initialize(encoderConfig, controllerHandle))
                {
                    Log("通讯编码器轴初始化失败", MessageLevel.Error);
                    SetStep(LaserCamBoxWorkflowState.Error, "编码器轴初始化失败");
                    return false;
                }

                // 2. 初始化 CAMBOX 跟踪引擎
                if (!_tracker.Initialize(trackingConfig, controllerHandle, _positionBuffer))
                {
                    Log("CAMBOX 跟踪引擎初始化失败", MessageLevel.Error);
                    SetStep(LaserCamBoxWorkflowState.Error, "CAMBOX 初始化失败");
                    return false;
                }

                // 3. 清空位置缓冲
                _positionBuffer.Clear();

                Log("跟踪工作流初始化完成");
                _initialized = true;
                SetStep(LaserCamBoxWorkflowState.Idle, "初始化完成");
                return true;
            }
        }

        /// <summary>启动 CAMBOX 跟踪。</summary>
        /// <remarks>置 WaitingForMaster，由监听线程按执行步完成后续动作。</remarks>
        /// <returns>受理成功返回 true</returns>
        public bool Start()
        {
            lock (_lock)
            {
                if (_tracker == null || _encoderAxis == null)
                {
                    Log("启动失败 原因是未初始化", MessageLevel.Warning);
                    return false;
                }
                SetStep(LaserCamBoxWorkflowState.WaitingForMaster, "启动指令受理");
                StartRunLoop();
                return true;
            }
        }

        /// <summary>停止 CAMBOX 跟踪。</summary>
        /// <remarks>置 Stopping，由执行步 700 完成停 CAMBOX 与复位。</remarks>
        /// <param name="immediate">true 表示立即停止，false 表示按停止段平滑收尾</param>
        public void Stop(bool immediate = false)
        {
            lock (_lock)
            {
                if (immediate && _tracker != null) _tracker.Stop(true);
                if (_step == LaserCamBoxWorkflowState.Idle || _step == LaserCamBoxWorkflowState.Error) return;
                SetStep(LaserCamBoxWorkflowState.Stopping, "停止指令受理");
            }
        }

        /// <summary>写入轨迹点（带时间戳，最常用方法）</summary>
        /// <remarks>转发给 CamBoxTracker，由它完成时间戳插值 + 前置补偿 + 写入。</remarks>
        /// <param name="laserOffset">焊缝横向偏移（Y_offset）</param>
        /// <param name="laserTimestamp">激光硬件采集时间戳</param>
        /// <returns>写入成功返回 true</returns>
        public bool AddTrajectoryPoint(double laserOffset, DateTime laserTimestamp)
        {
            return _tracker.AddTrajectoryPointWithTimestamp(laserOffset, laserTimestamp, _positionBuffer);
        }

        /// <summary>添加 RSI 位置采样（由 RSICommunication 每 4ms 调用）</summary>
        /// <param name="timestamp">高精度时间戳（微秒）</param>
        /// <param name="position">ToolRelX 位置（mm）</param>
        /// <param name="velocity">瞬时速度（mm/s）</param>
        public void AddRSIPositionSample(long timestamp, double position, double velocity = 0)
        {
            _positionBuffer.AddSample(timestamp, position, velocity);
        }

        /// <summary>流程复位：清空凸轮表、复位统计、状态回到 Idle。</summary>
        /// <remarks>由基类公共入口 Reset 调用（ADR-048）。</remarks>
        protected override void ResetFlow()
        {
            lock (_lock)
            {
                _tracker.Reset();
                _positionBuffer.Clear();
                _encoderAxis.Reset();
                SetStep(LaserCamBoxWorkflowState.Idle, "复位");
            }
        }

        #endregion

        #region 对外接口（主设备级联调用）

        /// <summary>执行 Y 轴偏移跟踪。</summary>
        /// <remarks>PreWeldPose 阶段由主设备调用；当前为骨架实现。</remarks>
        /// <returns>骨架实现，恒返回 true</returns>
        public bool BeginYAxisTracking()
        {
            // TODO 私有逻辑待实现：基于机器人 PreWeldPose 执行 Y 轴偏移跟踪并回报结果
            return true;
        }

        #endregion

        #region 流程态 → 设备四态映射

        /// <summary>流程态 → 设备四态映射。</summary>
        /// <remarks>未初始化 Idle（未 Initialize 前）→ Disconnect 初始化中 Initializing          → Work（初始化进行中，属进程态） 就绪 Idle（初始化完成后）       → Connect 进程中 WaitingForMaster/Tracking/Paused/Stopping → Work 报警 Error                     → Alarm 注：Idle 一态两义（未连接就绪 / 已连接就绪），故按初始化标记 _initialized 区分。</remarks>
        /// <param name="step">运控流程态</param>
        /// <returns>对应的设备四态</returns>
        protected override DeviceState MapToDeviceState(LaserCamBoxWorkflowState step)
        {
            switch (step)
            {
                case LaserCamBoxWorkflowState.Initializing:
                case LaserCamBoxWorkflowState.WaitingForMaster:
                case LaserCamBoxWorkflowState.Tracking:
                case LaserCamBoxWorkflowState.Paused:
                case LaserCamBoxWorkflowState.Stopping:
                    return DeviceState.Work;
                case LaserCamBoxWorkflowState.Error:
                    return DeviceState.Alarm;
                default:
                    // Idle：初始化完成即为已连接就绪，否则为未连接
                    return _initialized ? DeviceState.Connect : DeviceState.Disconnect;
            }
        }

        #endregion

        #region 阶段态变更钩子（阶段态切换时复位执行步到该阶段入口）

        /// <summary>阶段态切换时把执行步复位到该阶段入口步。</summary>
        /// <remarks>注意：本方法在 SetStep 同步路径内执行，调用处 SetStep 后须立即 return。</remarks>
        /// <param name="e">流程态变更参数</param>
        protected override void OnStepChanged(WorkflowStepChangedEventArgs<LaserCamBoxWorkflowState> e)
        {
            base.OnStepChanged(e);
            switch (e.NewState)
            {
                case LaserCamBoxWorkflowState.WaitingForMaster:
                    GoStep(StepResetEncoder);
                    break;
                case LaserCamBoxWorkflowState.Tracking:
                    GoStep(StepTracking);
                    break;
                case LaserCamBoxWorkflowState.Paused:
                    GoStep(StepPaused);
                    break;
                case LaserCamBoxWorkflowState.Stopping:
                    GoStep(StepStopping);
                    break;
                default:
                    GoStep(StepIdle);
                    break;
            }
        }

        #endregion

        #region 执行步分派

        /// <summary>执行步分派。</summary>
        /// <remarks>每拍先刷新跟踪引擎状态（取代独立监控线程），再按 WorkStep 分派。</remarks>
        protected override void FlowProcess()
        {
            UpdateTrackerStatus();
            switch (WorkStep)
            {
                case StepResetEncoder: DoResetEncoder(); break;
                case StepStartCamBox: DoStartCamBox(); break;
                case StepWaitMaster: DoWaitMaster(); break;
                case StepPaused: DoPaused(); break;
                case StepStopping: DoStopping(); break;
                case StepFinishFail: DoFinishFail(); break;
                default:
                    // StepTracking / StepIdle 与未登记步号：空转等待，等待靠阶段态切换的 GoStep 复位
                    break;
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
                case StepIdle: return "空闲待机";
                case StepResetEncoder: return "启动-清零编码器轴";
                case StepStartCamBox: return "启动-启动CAMBOX";
                case StepWaitMaster: return "启动-等待主轴运动";
                case StepTracking: return "跟踪中";
                case StepPaused: return "暂停中-等待恢复";
                case StepStopping: return "停止收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        #endregion

        #region 抽象生命周期方法（映射至既有公共方法）

        /// <summary>初始化流程。</summary>
        /// <remarks>连接运控（正运动/PLC）；当前为骨架实现。</remarks>
        /// <returns>初始化成功返回 true</returns>
        protected override bool InitializeFlow()
        {
            // TODO 私有逻辑待实现：连接运动控制器并自检（含 CAMBOX 参数装配）
            return false;
        }

        /// <summary>手动开 = 连接运控。骨架：私有逻辑待实现。</summary>
        protected override void ManualOn()
        {
            // TODO 私有逻辑待实现：手动连接运控
        }

        /// <summary>手动关 = 断开运控。</summary>
        protected override void ManualOff()
        {
            Stop(false);
        }

        /// <summary>自动运行 = 阻塞式执行跟踪序列（ADR-048）。</summary>
        /// <remarks>序列：Start 受理 → 阻塞等待流程回 Idle/Error（ WaitingForMaster→Tracking→…→Stopping→Idle
        /// 由监听线程 FlowProcess 按执行步推进）。中止时立即停 CAMBOX 并复位从轴。</remarks>
        protected override void AutoRunFlow()
        {
            if (!Start())
            {
                // 监听线程在 Idle 不活跃（900 收尾步不会被消费），受理失败只记日志
                Log("自动运行启动受理失败 未初始化", MessageLevel.Error);
                return;
            }
            AutoWait(() => _step == LaserCamBoxWorkflowState.Idle
                || _step == LaserCamBoxWorkflowState.Error, 0);
            if (IsAutoAbortRequested) Stop(true);
        }

        #endregion

        #region 监听线程钩子与释放

        /// <summary>释放钩子。</summary>
        /// <remarks>基类 Dispose 调用（ADR-034）；释放跟踪引擎、编码器轴、位置缓冲。</remarks>
        protected override void DisposeManaged()
        {
            lock (_lock)
            {
                if (_tracker != null)
                {
                    _tracker.TrackingError -= OnTrackerError;
                    _tracker.Dispose();
                }
                if (_encoderAxis != null) _encoderAxis.Dispose();
                if (_positionBuffer != null) _positionBuffer.Dispose();
                _tracker = null;
                _encoderAxis = null;
                _positionBuffer = null;
                _initialized = false;
                SetStep(LaserCamBoxWorkflowState.Idle, "Dispose 释放资源");
            }
        }

        #endregion
    }

    /// <summary>激光-CAMBOX 跟踪工作流状态枚举</summary>
    public enum LaserCamBoxWorkflowState
    {
        /// <summary>空闲，未启动</summary>
        Idle,
        /// <summary>初始化中（配置 CamBoxTracker、RSIPositionBuffer）</summary>
        Initializing,
        /// <summary>等待主轴运动（机器人开始进给）</summary>
        WaitingForMaster,
        /// <summary>正常跟踪中（硬件 CAMBOX 实时跟随）</summary>
        Tracking,
        /// <summary>临时暂停（激光丢帧等可恢复中断）</summary>
        Paused,
        /// <summary>停止中</summary>
        Stopping,
        /// <summary>异常状态（EtherCAT 断连等严重错误）</summary>
        Error
    }
}
