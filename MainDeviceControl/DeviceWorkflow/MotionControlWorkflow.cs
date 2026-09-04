using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.ZMotion;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>激光-CAMBOX 跟踪工作流私有相位。</summary>
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

    /// <summary>运动控制工作流（单例，激光-CAMBOX 焊缝跟踪）。</summary>
    /// <remarks>实时运动归硬件 CAMBOX，本类仅启停编排与轨迹写入；不转发 Tracker 事件，订阅方直订 Tracker；差异化职责（原档 R011）送丝位置与焊接质量阈值检测（待落地）。</remarks>
    public class MotionControlWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepResetEncoder = 10;
        private const int StepStartCamBox = 20;
        private const int StepWaitMaster = 30;
        private const int StepTracking = 100;
        private const int StepPaused = 110;
        private const int StepStopping = 700;

        // 单步超时阈值（秒）
        private const int CamBoxStartTimeoutSeconds = 5;
        private const int MasterWaitTimeoutSeconds = 120;
        private const int PausedTimeoutSeconds = 30;

        #endregion

        #region 私有变量

        private CamBoxTracker _tracker;
        private EncoderAxis _encoderAxis;
        private RSIPositionBuffer _positionBuffer;

        /// <summary>私有相位：仅驱动 FlowProcess 与可观测性，对外以 SubDeviceWeldStatus 暴露。</summary>
        private LaserCamBoxWorkflowState _phase = LaserCamBoxWorkflowState.Idle;

        /// <summary>是否已完成初始化（用于区分 Idle 是"未连接就绪"还是"已连接就绪"）。</summary>
        private volatile bool _initialized;

        private readonly object _lock = new object();

        // 失败收尾记录（原基类 FailReason / FailStep 已删除，下沉为子类私有字段）
        private string _failReason = "";
        private int _failStep;

        /// <summary>最近一次光斑对准偏差 X（取自 DeviceControlWork.AlignDatum，供振镜补偿与调试观测）。</summary>
        private double _lastSpotOffsetX;

        /// <summary>最近一次光斑对准偏差 Y（取自 DeviceControlWork.AlignDatum，供振镜补偿与调试观测）。</summary>
        private double _lastSpotOffsetY;

        #endregion

        #region 单例

        private static readonly Lazy<MotionControlWorkflow> _lazyInstance =
            new Lazy<MotionControlWorkflow>(() => new MotionControlWorkflow());

        /// <summary>运动控制工作流单例。</summary>
        public static MotionControlWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        /// <summary>设备名称（日志/事件标识）。</summary>
        public override string StateName { get { return Tag; } }

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
                return _phase == LaserCamBoxWorkflowState.Tracking
                    || _phase == LaserCamBoxWorkflowState.WaitingForMaster;
            }
        }

        /// <summary>子设备配置文件名（不含扩展名），落在 Config/XML/ 下。</summary>
        protected override string ConfigFileName { get { return "MotionControlWorkflow"; } }

        #endregion

        #region 构造函数

        private MotionControlWorkflow()
        {
            Tag = "运动控制工作流";

            _tracker = new CamBoxTracker();
            _encoderAxis = new EncoderAxis();
            _positionBuffer = new RSIPositionBuffer();

            // 跟踪引擎异常只做记录：子设备事件不向上转发，订阅方直接订阅 Tracker（权责边界 R2）
            _tracker.TrackingError += OnTrackerError;
        }

        #endregion

        #region 私有函数

        /// <summary>相位 → 焊接过程态映射。</summary>
        /// <param name="phase">运控流程态</param>
        /// <returns>对应焊接过程态</returns>
        private static SubDeviceWeldStatus MapWeldStatus(LaserCamBoxWorkflowState phase)
        {
            switch (phase)
            {
                case LaserCamBoxWorkflowState.Error:
                    return SubDeviceWeldStatus.ErrorAborted;
                case LaserCamBoxWorkflowState.Initializing:
                    return SubDeviceWeldStatus.PreWork;
                case LaserCamBoxWorkflowState.Stopping:
                    return SubDeviceWeldStatus.Stopping;
                case LaserCamBoxWorkflowState.Idle:
                    return SubDeviceWeldStatus.Standby;
                default:
                    // WaitingForMaster / Tracking / Paused 均为进程态
                    return SubDeviceWeldStatus.Working;
            }
        }

        /// <summary>刷新相位并映射为对外焊接过程态，同时把执行步复位到该阶段入口。</summary>
        /// <param name="phase">目标相位</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetPhase(LaserCamBoxWorkflowState phase, string reason)
        {
            _phase = phase;
            SetWeldStatus(MapWeldStatus(phase), reason);
            switch (phase)
            {
                case LaserCamBoxWorkflowState.WaitingForMaster:
                    AdvanceStep(StepResetEncoder);
                    break;
                case LaserCamBoxWorkflowState.Tracking:
                    AdvanceStep(StepTracking);
                    break;
                case LaserCamBoxWorkflowState.Paused:
                    AdvanceStep(StepPaused);
                    break;
                case LaserCamBoxWorkflowState.Stopping:
                    AdvanceStep(StepStopping);
                    break;
                default:
                    AdvanceStep(StepIdle);
                    break;
            }
        }

        /// <summary>判断当前步停留是否超过指定秒数。</summary>
        /// <param name="seconds">超时阈值（秒）</param>
        /// <returns>超过阈值返回 true</returns>
        private bool IsTimeout(int seconds)
        {
            return StepWorkTime.TotalSeconds > seconds;
        }

        /// <summary>执行步 10：清零编码器轴（必须在 CAMBOX 启动前完成）。</summary>
        /// <returns>清零完成返回 true</returns>
        private bool DoResetEncoder()
        {
            _encoderAxis.Reset();
            return true;
        }

        /// <summary>执行步 20：启动 CAMBOX 跟踪。</summary>
        /// <returns>启动成功返回 true</returns>
        private bool DoStartCamBox()
        {
            if (!_tracker.Start())
            {
                if (!IsTimeout(CamBoxStartTimeoutSeconds)) return false;
                Fail("CAMBOX 启动超时");
                return false;
            }
            Log("CAMBOX 已启动，等待主轴运动");
            return true;
        }

        /// <summary>执行步 30：等待主轴运动，超时则报警。</summary>
        /// <remarks>跟踪态由 UpdateTrackerStatus 同步，不在本步死等。</remarks>
        /// <returns>已进入跟踪返回 true</returns>
        private bool DoWaitMaster()
        {
            if (_phase == LaserCamBoxWorkflowState.Tracking) return true;
            if (!IsTimeout(MasterWaitTimeoutSeconds)) return false;
            Fail("等待主轴运动超时 120秒");
            return false;
        }

        /// <summary>执行步 110：暂停中，超时则报警。</summary>
        /// <remarks>恢复由 UpdateTrackerStatus 同步回 Tracking。</remarks>
        /// <returns>已恢复跟踪返回 true</returns>
        private bool DoPaused()
        {
            if (_phase == LaserCamBoxWorkflowState.Tracking) return true;
            if (!IsTimeout(PausedTimeoutSeconds)) return false;
            Fail("跟踪暂停超时 30秒");
            return false;
        }

        /// <summary>执行步 700：停止收尾（停 CAMBOX → 复位从轴 → 清缓冲 → 回 Idle）。</summary>
        /// <returns>收尾完成返回 true</returns>
        private bool DoStopping()
        {
            if (_tracker != null) _tracker.Stop(false);
            _encoderAxis.Reset();
            _positionBuffer.Clear();
            Log("跟踪工作流已停止");
            SetPhase(LaserCamBoxWorkflowState.Idle, "已停止");
            return true;
        }

        /// <summary>执行步 900：失败收尾三步走。</summary>
        /// <remarks>置 Alarm 须经 SetWeldStatus 映射（禁手工赋值 State），再记日志与故障记录后回待机。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetPhase(LaserCamBoxWorkflowState.Error, _failReason);
            IsAlarm = true;
            Log(string.Format("运控流程异常终止 原因 {0}", _failReason), MessageLevel.Error);
            return true;
        }

        /// <summary>刷新跟踪引擎与编码器状态。</summary>
        /// <remarks>取代原独立监控线程 MonitorLoop，由 FlowProcess 每拍开头调用。</remarks>
        private void UpdateTrackerStatus()
        {
            if (_tracker == null) return;
            try
            {
                _tracker.UpdateStatus();
                _encoderAxis.UpdateStatus();
                SyncPhaseFromTracker();
            }
            catch (Exception ex)
            {
                Log("状态刷新异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>将 CamBoxTracker 状态同步到工作流相位（执行步由 SetPhase 复位）。</summary>
        private void SyncPhaseFromTracker()
        {
            switch (_tracker.State)
            {
                case CamBoxTrackingState.Tracking:
                    if (_phase != LaserCamBoxWorkflowState.Tracking)
                        SetPhase(LaserCamBoxWorkflowState.Tracking, "主轴开始运动");
                    break;
                case CamBoxTrackingState.Paused:
                    if (_phase != LaserCamBoxWorkflowState.Paused)
                        SetPhase(LaserCamBoxWorkflowState.Paused, "激光丢帧暂停");
                    break;
                case CamBoxTrackingState.Error:
                    if (_phase != LaserCamBoxWorkflowState.Error)
                        Fail("跟踪引擎异常");
                    break;
            }
        }

        /// <summary>跟踪引擎错误处理：仅记录日志。</summary>
        /// <remarks>异常不上抛为工作流事件——订阅方直接订阅 Tracker.TrackingError（权责边界 R2）。</remarks>
        /// <param name="sender">事件源（跟踪引擎）</param>
        /// <param name="message">错误信息</param>
        private void OnTrackerError(object sender, string message)
        {
            Log("跟踪错误 " + message, MessageLevel.Error);
        }

        /// <summary>转入失败收尾（替代已删除的基类 FailFlow）。</summary>
        /// <param name="reason">失败原因（纯文本，无符号）</param>
        private void Fail(string reason)
        {
            _failStep = RunStep;
            _failReason = reason;
            AdvanceStep(StepFinishFail);
        }

        #endregion

        #region 公共函数

        /// <summary>初始化跟踪工作流（三参数重载，供 InitializeOn 与外部显式初始化使用）。</summary>
        /// <remarks>配置 CamBoxTracker、EncoderAxis、RSIPositionBuffer。成功置 State=Connected，失败置 State=Disconnected。</remarks>
        /// <param name="trackingConfig">CAMBOX 跟踪配置</param>
        /// <param name="encoderConfig">编码器轴配置</param>
        /// <param name="controllerHandle">正运动控制器句柄</param>
        /// <returns>初始化成功返回 true</returns>
        public bool Initialize(CamBoxTrackingConfig trackingConfig,
            EncoderAxisConfig encoderConfig, IntPtr controllerHandle)
        {
            if (controllerHandle == IntPtr.Zero)
            {
                // 控制器未连接已由设备层（ZMotion/MotionManager）报错，此处不复述
                return false;
            }

            lock (_lock)
            {
                SetPhase(LaserCamBoxWorkflowState.Initializing, "开始初始化");

                // 1. 初始化通讯编码器主轴（ATYPE=25）
                if (!_encoderAxis.Initialize(encoderConfig, controllerHandle))
                {
                    Log("通讯编码器轴初始化失败", MessageLevel.Error);
                    SetPhase(LaserCamBoxWorkflowState.Error, "编码器轴初始化失败");
                    SetState(SubDeviceState.Disconnected, "编码器轴初始化失败");
                    return false;
                }

                // 2. 初始化 CAMBOX 跟踪引擎
                if (!_tracker.Initialize(trackingConfig, controllerHandle, _positionBuffer))
                {
                    Log("CAMBOX 跟踪引擎初始化失败", MessageLevel.Error);
                    SetPhase(LaserCamBoxWorkflowState.Error, "CAMBOX 初始化失败");
                    SetState(SubDeviceState.Disconnected, "CAMBOX 初始化失败");
                    return false;
                }

                // 3. 清空位置缓冲
                _positionBuffer.Clear();

                Log("跟踪工作流初始化完成");
                _initialized = true;
                SetPhase(LaserCamBoxWorkflowState.Idle, "初始化完成");
                SetState(SubDeviceState.Connected, "运控初始化完成");
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化完成，等待复位");
                return true;
            }
        }

        /// <summary>启动 CAMBOX 跟踪。</summary>
        /// <remarks>置 WaitingForMaster，由外部编排线程按执行步完成后续动作。</remarks>
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
                SetPhase(LaserCamBoxWorkflowState.WaitingForMaster, "启动指令受理");
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
                if (_phase == LaserCamBoxWorkflowState.Idle || _phase == LaserCamBoxWorkflowState.Error) return;
                SetPhase(LaserCamBoxWorkflowState.Stopping, "停止指令受理");
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

        /// <summary>添加 RSI 位置采样。</summary>
        /// <param name="timestamp">高精度时间戳（微秒）</param>
        /// <param name="position">ToolRelX 位置（mm）</param>
        /// <param name="velocity">瞬时速度（mm/s）</param>
        public void AddRSIPositionSample(long timestamp, double position, double velocity = 0)
        {
            _positionBuffer.AddSample(timestamp, position, velocity);
        }

        /// <summary>执行 Y 轴偏移跟踪。</summary>
        /// <remarks>焊前阶段由主设备调用；当前为骨架实现。</remarks>
        /// <returns>骨架实现，恒返回 true</returns>
        public bool BeginYAxisTracking()
        {
            // TODO 私有逻辑待实现：基于机器人焊前位执行 Y 轴偏移跟踪并回报结果
            return true;
        }

        #endregion

        #region 全阻塞流程

        /// <summary>流程处理：每拍先刷新跟踪引擎状态，再按 RunStep 由 bool 判定推进。</summary>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            UpdateTrackerStatus();

            switch (RunStep)
            {
                case StepResetEncoder:
                    if (DoResetEncoder()) AdvanceStep(StepStartCamBox);
                    break;
                case StepStartCamBox:
                    if (DoStartCamBox()) AdvanceStep(StepWaitMaster);
                    break;
                case StepWaitMaster:
                    if (DoWaitMaster()) AdvanceStep(StepTracking);
                    break;
                case StepPaused:
                    if (DoPaused()) AdvanceStep(StepTracking);
                    break;
                case StepStopping:
                    if (DoStopping()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                case StepTracking:
                    // 跟踪中持续消费监控相机光斑对准偏差，驱动振镜摆动补偿光斑
                    ApplySpotAlignCompensation();
                    break;
                default:
                    // StepIdle 与未登记步号：空转等待，靠相位切换把执行步复位
                    break;
            }
        }

        /// <summary>消费光斑对准偏差驱动振镜摆动补偿（原档 §7.4.5）。</summary>
        /// <remarks>相机只出结果，偏差由控制器消费，数据取自 DeviceControlWork.AlignDatum 中枢。</remarks>
        private void ApplySpotAlignCompensation()
        {
            var datum = DeviceControlWork.Instance.AlignDatum;
            if (datum == null) return;

            _lastSpotOffsetX = datum.OffsetX;
            _lastSpotOffsetY = datum.OffsetY;

            // todo 振镜摆动控制：待振镜轴指令与 WeldParamControlWorkflow.LastOutput.GalvoAmplitude
            //      摆幅基准落地后，按 OffsetX / OffsetY 驱动振镜摆动实现光斑对中补偿
        }

        /// <summary>流程复位：清空凸轮表、复位统计、状态回到 Idle（线性阻塞链）。</summary>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            lock (_lock)
            {
                if (_tracker == null || _encoderAxis == null) return false;
                _tracker.Reset();
                _positionBuffer.Clear();
                _encoderAxis.Reset();
                SetPhase(LaserCamBoxWorkflowState.Idle, "复位");
                SetWeldStatus(SubDeviceWeldStatus.Standby, "运控复位完成");
                return true;
            }
        }

        /// <summary>状态清理：标志位全复位 + 执行步归零 + 强制未复位态。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _failReason = "";
            _failStep = 0;
            // 直接置相位，不经 SetPhase 的 Idle→Standby 映射（清除后必须是 NoReset，杜绝中间态）
            _phase = LaserCamBoxWorkflowState.Idle;
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入步骤时长导致异常超时。</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：初始化运控管理器与 CAMBOX 跟踪引擎，同步等待完成。</summary>
        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            try
            {
                Log("运控初始化连接开始", MessageLevel.Info);
                MotionManager.Instance.Initialize();

                var zmController = MotionManager.Instance.DefaultController as ZMotionController;
                if (zmController == null)
                {
                    Log("未检测到正运动控制器，CAMBOX 跟踪跳过初始化", MessageLevel.Warning);
                    SetState(SubDeviceState.Connected, "运控连接完成（无 CAMBOX）");
                    return true;
                }

                var config = MotionConfigManager.Instance.Config;
                bool ok = Initialize(config.CamBoxTracking, config.EncoderAxis, zmController.GetHandle());
                Log(string.Format("运控初始化连接{0}", ok ? "完成" : "失败"),
                    ok ? MessageLevel.Info : MessageLevel.Error);
                return ok;
            }
            catch (Exception ex)
            {
                Log("运控初始化连接异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备断开（阻塞）：停止跟踪并释放，调用方阻塞至完成。</summary>
        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            try
            {
                Log("运控初始化断开开始", MessageLevel.Info);
                lock (_lock)
                {
                    if (_tracker != null) _tracker.Stop(true);
                    if (_encoderAxis != null) _encoderAxis.Reset();
                    if (_positionBuffer != null) _positionBuffer.Clear();
                    SetPhase(LaserCamBoxWorkflowState.Idle, "初始化断开");
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

        /// <summary>设备日志：输出运控差异化日志（轴状态 / 送丝位置 / 焊接质量阈值）。</summary>
        public override void SubDeviceLog()
        {
            // todo 送丝位置与焊接质量阈值检测（原档 R011）落地后在此补充
            Log(string.Format("运控 连接态 {0} 焊接过程态 {1} 相位 {2} 步骤 {3} 已初始化 {4}",
                State, WeldStatus, _phase, WorkStepName, _initialized));
        }

        /// <summary>加载本子设备的配置。</summary>
        public override void LoadConfig()
        {
            TimeOutSeconds = ReadInt("TimeOutSeconds", 30);
        }

        /// <summary>保存本子设备的配置。</summary>
        public override void SaveConfig()
        {
            Config.SetElementValue("TimeOutSeconds", TimeOutSeconds.ToString());
            Config.SaveXdocument();
        }

        /// <summary>焊接工作状态切换管控。</summary>
        public override void WeldStatusTrans()
        {
            if (WeldStatus == SubDeviceWeldStatus.NoReset) return;
            if (WeldStatus == SubDeviceWeldStatus.Stopping
                && _phase != LaserCamBoxWorkflowState.Stopping)
            {
                SetPhase(LaserCamBoxWorkflowState.Stopping, "焊接状态切换管控");
                return;
            }
            if ((WeldStatus == SubDeviceWeldStatus.ManualStopped
                || WeldStatus == SubDeviceWeldStatus.ErrorAborted)
                && _phase != LaserCamBoxWorkflowState.Idle)
            {
                SetPhase(LaserCamBoxWorkflowState.Idle, "焊接状态切换管控");
            }
        }

        #endregion

        #region 非阻塞流程

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

        /// <summary>释放钩子。</summary>
        /// <remarks>基类 Dispose 调用（ADR-025）；释放跟踪引擎、编码器轴、位置缓冲。</remarks>
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
                SetState(SubDeviceState.Disconnected, "运控释放");
                SetPhase(LaserCamBoxWorkflowState.Idle, "Dispose 释放资源");
            }
        }

        #endregion
    }
}
