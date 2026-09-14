using System;
using System.Collections.Generic;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>运控工作流私有相位。</summary>
    public enum MotionControlPhase
    {
        /// <summary>空闲，未初始化</summary>
        Idle,
        /// <summary>初始化中</summary>
        Initializing,
        /// <summary>已使能待机</summary>
        Ready,
        /// <summary>停止中</summary>
        Stopping,
        /// <summary>异常</summary>
        Error
    }

    /// <summary>运动控制工作流（单例）。</summary>
    /// <remarks>负责台达总线的初始化、伺服使能编排与急停；焊缝跟踪方案待定。</remarks>
    public class MotionControlWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepServoOn = 10;
        private const int StepReady = 20;
        private const int StepStopping = 700;

        // 单步超时阈值（秒）
        private const int ServoOnTimeoutSeconds = 10;

        #endregion

        #region 私有变量

        /// <summary>私有相位：仅驱动 FlowProcess 与可观测性，对外以 SubDeviceWeldStatus 暴露。</summary>
        private MotionControlPhase _phase = MotionControlPhase.Idle;

        /// <summary>是否已完成初始化（用于区分 Idle 是未连接就绪还是已连接就绪）。</summary>
        private volatile bool _initialized;

        /// <summary>是否已下发伺服使能指令，避免等待期间重复下发。</summary>
        private bool _servoOnIssued;

        private readonly object _lock = new object();

        // 失败收尾记录（原基类 FailReason / FailStep 已删除，下沉为子类私有字段）
        private string _failReason = "";
        private int _failStep;

        /// <summary>焊缝跟踪轴，读取轴当前位置用于叠加偏差（运动目标 = 轴当前值 + 偏差）</summary>
        private MontionAxis _seamAxis;

        /// <summary>焊缝跟踪 PVT 点缓冲区（算法产出 → 控制器写入之间的缓冲）。</summary>
        private readonly List<PVTPoint> _seamBuffer = new List<PVTPoint>();

        /// <summary>缓冲区锁。</summary>
        private readonly object _seamBufferLock = new object();

        /// <summary>焊缝跟踪是否已下发起始段。</summary>
        private bool _seamRunning;

        /// <summary>最近一次 PVT 下发返回码。</summary>
        private ushort _seamLastErrorCode;

        /// <summary>最近一次 PVT 下发时间。</summary>
        private DateTime _seamLastOutputTime;

        /// <summary>焊缝跟踪激活标志（上一周期状态，用于起止沿判定），跨线程读。</summary>
        private volatile bool _seamTrackingActive;

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

        /// <summary>是否处于已使能待机</summary>
        public bool IsRunning { get { return _phase == MotionControlPhase.Ready; } }

        /// <summary>是否已初始化</summary>
        public bool IsInitialized { get { return _initialized; } }

        /// <summary>子设备配置文件名（不含扩展名），落在 Config/XML/ 下。</summary>
        protected override string ConfigFileName { get { return "MotionControlWorkflow"; } }

        #endregion

        #region 构造函数

        private MotionControlWorkflow()
        {
            Tag = "运动控制工作流";
            _seamLastOutputTime = DateTime.Now;
        }

        #endregion

        #region 私有函数

        /// <summary>相位 → 焊接过程态映射。</summary>
        /// <param name="phase">运控流程态</param>
        /// <returns>对应焊接过程态</returns>
        private static SubDeviceWeldStatus MapWeldStatus(MotionControlPhase phase)
        {
            switch (phase)
            {
                case MotionControlPhase.Error:
                    return SubDeviceWeldStatus.ErrorAborted;
                case MotionControlPhase.Initializing:
                    return SubDeviceWeldStatus.PreWork;
                case MotionControlPhase.Stopping:
                    return SubDeviceWeldStatus.Stopping;
                default:
                    return SubDeviceWeldStatus.Standby;
            }
        }

        /// <summary>刷新相位并映射为对外焊接过程态，同时把执行步复位到该阶段入口。</summary>
        /// <param name="phase">目标相位</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetPhase(MotionControlPhase phase, string reason)
        {
            _phase = phase;
            SetWeldStatus(MapWeldStatus(phase), reason);
            switch (phase)
            {
                case MotionControlPhase.Ready:
                    AdvanceStep(StepServoOn);
                    break;
                case MotionControlPhase.Stopping:
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

        /// <summary>判断是否所有轴均已使能。</summary>
        /// <returns>全部使能返回 true</returns>
        private static bool IsAllServoOn()
        {
            foreach (MontionAxis axis in MontionManager.Instance.MontionControl.GetAxes())
            {
                if (!axis.IsServoOn()) return false;
            }
            return true;
        }

        /// <summary>执行步 10：全部轴伺服使能。</summary>
        /// <returns>全部使能返回 true</returns>
        private bool DoServoOn()
        {
            if (!_servoOnIssued)
            {
                MontionManager.Instance.ServoOnAll();
                _servoOnIssued = true;
            }

            if (IsAllServoOn())
            {
                Log("全部轴伺服使能完成");
                return true;
            }

            if (!IsTimeout(ServoOnTimeoutSeconds)) return false;
            Fail("伺服使能超时");
            return false;
        }

        /// <summary>执行步 700：停止收尾（全轴下电 → 回 Idle）。</summary>
        /// <returns>收尾完成返回 true</returns>
        private bool DoStopping()
        {
            // 收尾线激光引导：把剩余点作为最后一段下发，避免轴停在半截轨迹上
            FlushSeamBuffer(true);
            _seamTrackingActive = false;
            MontionManager.Instance.ServoOffAll();
            _servoOnIssued = false;
            Log("运控工作流已停止");
            SetPhase(MotionControlPhase.Idle, "已停止");
            return true;
        }

        /// <summary>执行步 900：失败收尾。</summary>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetPhase(MotionControlPhase.Error, _failReason);
            IsAlarm = true;
            Log(string.Format("运控流程异常终止 原因 {0}", _failReason), MessageLevel.Error);
            return true;
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
                SetPhase(MotionControlPhase.Ready, "启动指令受理");
                return true;
            }
        }

        /// <summary>停止运控。</summary>
        /// <param name="immediate">true 表示立即急停，false 表示按停止段收尾</param>
        public void Stop(bool immediate = false)
        {
            lock (_lock)
            {
                if (immediate) MontionManager.Instance.SetEmergencyStop(true);
                if (_phase == MotionControlPhase.Idle || _phase == MotionControlPhase.Error) return;
                SetPhase(MotionControlPhase.Stopping, "停止指令受理");
            }
        }

        #endregion

        #region 全阻塞流程

        /// <summary>流程处理：按 RunStep 由 bool 判定推进。</summary>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            switch (RunStep)
            {
                case StepServoOn:
                    if (DoServoOn()) AdvanceStep(StepReady);
                    break;
                case StepReady:
                    // 已使能待机：周期驱动线激光引导（起止沿复位/收尾、按节拍输出 PVT 段）
                    UpdateSeamTracking();
                    break;
                case StepStopping:
                    if (DoStopping()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                default:
                    // StepIdle 与未登记步号：空转等待，靠相位切换把执行步复位
                    break;
            }
        }

        /// <summary>流程复位：解除急停并回到待机。</summary>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            lock (_lock)
            {
                if (!_initialized) return false;
                MontionManager.Instance.SetEmergencyStop(false);
                _servoOnIssued = false;
                SetPhase(MotionControlPhase.Idle, "复位");
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
            _servoOnIssued = false;
            // 直接置相位，不经 SetPhase 的映射（清除后必须是 NoReset，杜绝中间态）
            _phase = MotionControlPhase.Idle;
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止停止期间的时间被计入步骤时长导致异常超时。</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>
        /// 装配焊缝跟踪：把线激光引导配置注入轨迹算法，并订阅线激光结果。
        /// 轴名取自线激光配置 SeamTrackAxisName；未找到轴时记录警告。
        /// </summary>
        private void AttachSeamTrackAxis()
        {
            string axisName = LineLaserManager.Instance.Config.SeamTrackAxisName;
            MontionAxis axis = MontionManager.Instance.GetAxis(axisName);
            if (axis == null)
            {
                Log(string.Format("未找到焊缝跟踪轴 {0} 线激光引导未启用", axisName), MessageLevel.Warning);
                return;
            }

            _seamAxis = axis;
            LineLaserManager.Instance.ResultReady += OnSeamResultReady;
            Log(string.Format("焊缝跟踪轴 {0} 已装配 线激光引导就绪", axisName));
        }

        /// <summary>撤除焊缝跟踪装配：收尾当前段、退订结果、清空缓冲区。</summary>
        private void DetachSeamTrackAxis()
        {
            LineLaserManager.Instance.ResultReady -= OnSeamResultReady;
            FlushSeamBuffer(true);
            lock (_seamBufferLock)
            {
                _seamBuffer.Clear();
            }
            _seamRunning = false;
            _seamAxis = null;
            _seamTrackingActive = false;
        }

        /// <summary>
        /// 线激光结果回调：焊接工作态下把结果喂入轨迹算法，返回点加入下发缓冲区。
        /// 运行在相机 SDK 回调线程；轴位置读取失败按 0 处理不中断跟踪。
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">线激光结果事件参数</param>
        private void OnSeamResultReady(object sender, LineLaserResultEventArgs e)
        {
            if (!_seamTrackingActive || _seamAxis == null) return;

            try
            {
                PVTPoint[] points = WeldParamManager.Instance.FeedSeamTracking(
                    e.Result, e.MeasuredFps, _seamAxis.GetPosition());
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

        /// <summary>周期驱动焊缝跟踪：按焊接工作态起止沿复位/收尾，按节拍把缓冲区写入控制器。</summary>
        private void UpdateSeamTracking()
        {
            if (_seamAxis == null) return;

            bool active = LineLaserWorkflow.Instance.WeldStatus == SubDeviceWeldStatus.Working;

            if (active && !_seamTrackingActive)
            {
                WeldParamManager.Instance.ResetSeamTracking();
                _seamRunning = false;
            }
            if (!active && _seamTrackingActive)
            {
                FlushSeamBuffer(true);
            }
            _seamTrackingActive = active;

            if (!active) return;

            PVTPoint[] tickPoints = WeldParamManager.Instance.TickSeamTracking();
            EnqueueSeamPoints(tickPoints);

            TryFlushSeamBuffer(false);

            if (WeldParamManager.Instance.SeamTrackingHasFault)
            {
                Fail(WeldParamManager.Instance.SeamTrackingFaultReason);
            }
        }

        /// <summary>按水位与节拍条件尝试把缓冲区写入控制器。</summary>
        /// <param name="isFinal">是否为最后一段</param>
        private void TryFlushSeamBuffer(bool isFinal)
        {
            if (!isFinal)
            {
                PVTInformation info;
                ushort query = _seamAxis.GetInformation(out info);
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
                Log(string.Format("PVT 点下发失败 返回码 0x{0:X} 点数 {1}", _seamLastErrorCode, segment.Length), MessageLevel.Error);
            }
        }

        /// <summary>设备连接（阻塞）：初始化台达总线，同步等待完成。</summary>
        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            try
            {
                Log("运控初始化连接开始");
                SetPhase(MotionControlPhase.Initializing, "开始初始化");
                BeginConnectAttempt();

                string msg = "";
                if (!MontionManager.Instance.Initialization("台达运控", ref msg))
                {
                    // 根因已由 DeltaMontionControl 记录（ADR-031 D1），此处只推进状态不复述错误
                    SetPhase(MotionControlPhase.Error, "运控初始化失败");
                    MarkConnectFailed("运控初始化失败 " + msg);
                    return false;
                }

                _initialized = true;
                AttachSeamTrackAxis();
                SetPhase(MotionControlPhase.Idle, "初始化完成");
                SetState(SubDeviceState.Connected, "运控连接完成");
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化完成 等待复位");
                Log("运控初始化连接完成");
                return true;
            }
            catch (Exception ex)
            {
                Log("运控初始化连接异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备断开（阻塞）：关闭台达总线，调用方阻塞至完成。</summary>
        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            try
            {
                Log("运控初始化断开开始");
                lock (_lock)
                {
                    DetachSeamTrackAxis();
                    MontionManager.Instance.Close();
                    _initialized = false;
                    _servoOnIssued = false;
                    SetPhase(MotionControlPhase.Idle, "初始化断开");
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

        /// <summary>设备日志：输出运控连接态与相位。</summary>
        public override void SubDeviceLog()
        {
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
                && _phase != MotionControlPhase.Stopping)
            {
                SetPhase(MotionControlPhase.Stopping, "焊接状态切换管控");
                return;
            }
            if ((WeldStatus == SubDeviceWeldStatus.ManualStopped
                || WeldStatus == SubDeviceWeldStatus.ErrorAborted)
                && _phase != MotionControlPhase.Idle)
            {
                SetPhase(MotionControlPhase.Idle, "焊接状态切换管控");
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
                case StepServoOn: return "启动-伺服使能";
                case StepReady: return "已使能待机";
                case StepStopping: return "停止收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        /// <summary>释放钩子。</summary>
        /// <remarks>基类 Dispose 调用（ADR-020）；关闭台达总线。</remarks>
        protected override void DisposeManaged()
        {
            lock (_lock)
            {
                DetachSeamTrackAxis();
                MontionManager.Instance.Close();
                _initialized = false;
                _servoOnIssued = false;
                SetState(SubDeviceState.Disconnected, "运控释放");
                SetPhase(MotionControlPhase.Idle, "Dispose 释放资源");
            }
        }

        #endregion
    }
}
