using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.EmguALG;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.Api;
using AdaWeldSystem.ProductFileManager;
using Emgu.CV;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>
    /// 监控相机工作流：只负责「触发采集 → 调用算法 → 读取结果 → 推进流程态」的编排。
    /// 不产出设备数据与算法结果：图像由 MonitorCameraRun 的 FrameCompletedEvent 输出，
    /// 健康巡检由 MonitorCameraRun 自身维护，流程不越俎代庖（ADR-042）。
    /// 检测结果以只读属性 LastResult / LastOverlay 暴露，订阅方在流程态转 Completed 后拉取。
    ///
    /// 步骤驱动（ADR-047）：执行步段位 10 触发采集 → 20 等采集完成信号 → 30 分析
    /// → 800 成功收尾（连续模式回到 10） / 900 失败收尾（置 Alarm + 记故障）。
    /// 采集等待不再用 WaitOne(5000) 阻塞，改为「WaitOne(0) 非阻塞探测 + 节拍重入 + IsStepTimeout 判超时」。
    ///
    /// 新主控设计：基类改为非泛型 DeviceWorkflowBase，原流程态枚举降为私有 _flowState 仅驱动 FlowProcess；
    /// 对外焊接过程态经 SetWeldStatus(SubDeviceWeldStatus) 承载，连接态经 ConnectOn/ConnectOff 联动 State。
    /// </summary>
    public class MonitorCameraWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepAcquireTrigger = 10;
        private const int StepAcquireWait = 20;
        private const int StepAnalyze = 30;

        private const int AcquireTimeoutMs = 5000;
        private const string DefaultIp = "192.168.1.100";
        private const string DefaultPort = "5000";

        #endregion

        #region 私有变量

        private readonly string _tag = "监控相机工作流";

        /// <summary>私有流程态（取代旧基类 _step，仅驱动 FlowProcess 与可观测性）。</summary>
        private MonitorWorkflowState _flowState = MonitorWorkflowState.Uninitialized;

        private volatile bool _continuous;
        private volatile MonitorCameraPhase _phase = MonitorCameraPhase.PreWeldAlignment;

        /// <summary>手动连接/断开线程是否进行中（防重入）。</summary>
        private volatile bool _manualBusy;

        private readonly object _resultLock = new object();
        private Mat _lastMat;
        private Mat _resultMat;
        private readonly MonitorResult _result = new MonitorResult();

        #endregion

        #region 单例

        private static readonly Lazy<MonitorCameraWorkflow> _lazyInstance =
            new Lazy<MonitorCameraWorkflow>(() => new MonitorCameraWorkflow());

        public static MonitorCameraWorkflow Instance => _lazyInstance.Value;

        #endregion

        #region 公共变量

        /// <summary>对外名片名（日志与监听线程命名用）。</summary>
        public override string StateName => _tag;

        /// <summary>最近一轮检测结果（只读快照，供 UI 在流程态转 Completed 后拉取）</summary>
        public MonitorResult LastResult
        {
            get
            {
                lock (_resultLock)
                {
                    return new MonitorResult
                    {
                        Phase = _result.Phase,
                        Pass = _result.Pass,
                        QualityScore = _result.QualityScore,
                        OffsetX = _result.OffsetX,
                        OffsetY = _result.OffsetY,
                        Angle = _result.Angle,
                        DefectCount = _result.DefectCount,
                        Message = _result.Message,
                        Timestamp = _result.Timestamp
                    };
                }
            }
        }

        /// <summary>最近一轮算法叠加图（只读，供 UI 在流程态转 Completed 后拉取）</summary>
        public Mat LastOverlay
        {
            get { lock (_resultLock) { return _resultMat; } }
        }

        /// <summary>仅进程中阶段态才跑业务；待机/报警/手动停止时空转并刷新步时间戳。</summary>
        protected override bool IsRunLoopActive
        {
            get
            {
                return _flowState == MonitorWorkflowState.Acquiring
                    || _flowState == MonitorWorkflowState.Analyzing
                    || _flowState == MonitorWorkflowState.Completed;
            }
        }

        #endregion

        #region 构造函数

        private MonitorCameraWorkflow() { }

        #endregion

        #region 私有函数

        private void RequireStandby(string method)
        {
            if (_flowState != MonitorWorkflowState.Standby)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} 要求当前状态为 Standby，实际为 {1}", method, _flowState));
            }
        }

        /// <summary>确保相机已连接。</summary>
        /// <remarks>未连接时用配置地址或默认地址打开。</remarks>
        private void EnsureConnected()
        {
            var cam = MonitorCameraRun.Instance;
            if (cam.ConnectionState == MonitorCameraConnectionState.Connected) return;
            string ip = cam.Config != null ? cam.Config.IpAddress : DefaultIp;
            string port = cam.Config != null ? cam.Config.Port : DefaultPort;
            cam.OpenSensor(ip, port);
        }

        /// <summary>相位 → 焊接过程态映射（取代旧基类 MapToDeviceState）。</summary>
        /// <param name="step">流程态</param>
        /// <returns>对应焊接过程态</returns>
        private static SubDeviceWeldStatus MapWeldStatus(MonitorWorkflowState step)
        {
            switch (step)
            {
                case MonitorWorkflowState.ErrorAborted:
                    return SubDeviceWeldStatus.ErrorAborted;
                case MonitorWorkflowState.Uninitialized:
                case MonitorWorkflowState.Standby:
                case MonitorWorkflowState.ManualStopped:
                    return SubDeviceWeldStatus.Standby;
                default:
                    // Acquiring / Analyzing / Completed 均为进程态
                    return SubDeviceWeldStatus.Working;
            }
        }

        /// <summary>刷新流程相位并映射为对外焊接过程态。</summary>
        /// <remarks>等价旧基类 SetStep + MapToDeviceState + OnStepChanged。</remarks>
        /// <param name="step">目标流程态</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetStep(MonitorWorkflowState step, string reason)
        {
            _flowState = step;
            SetWeldStatus(MapWeldStatus(step), reason);
            switch (step)
            {
                case MonitorWorkflowState.Acquiring:
                    GoStep(StepAcquireTrigger);
                    break;
                case MonitorWorkflowState.Analyzing:
                    GoStep(StepAnalyze);
                    break;
                case MonitorWorkflowState.Completed:
                    GoStep(StepFinishOk);
                    break;
                default:
                    GoStep(StepIdle);
                    break;
            }
        }

        /// <summary>执行步 10：下发检测阶段并触发一次采集。</summary>
        private void DoAcquireTrigger()
        {
            var cam = MonitorCameraRun.Instance;
            cam.SetPhase(_phase);
            cam.AcquisitionCompletedSignal.Reset();
            EnsureConnected();
            cam.SensorRunContinue();
            GoStep(StepAcquireWait);
        }

        /// <summary>执行步 20：等待采集完成信号。</summary>
        /// <remarks>WaitOne(0) 只做非阻塞探测，等待由监听线程节拍重入完成，超时统一由 IsStepTimeout 判定， 超时即转失败收尾，不留在原步死等（ADR-005 有界可失败语义）。</remarks>
        private void DoAcquireWait()
        {
            var cam = MonitorCameraRun.Instance;
            if (!cam.AcquisitionCompletedSignal.WaitOne(0))
            {
                if (IsStepTimeout(AcquireTimeoutMs))
                    FailFlow("采集等待信号超时 5秒");
                return;
            }
            if (!cam.LastAcquisitionSuccess || cam.CurrentMat == null || cam.CurrentMat.IsEmpty)
            {
                FailFlow("采集结果无效 图像为空或采集失败");
                return;
            }
            lock (_resultLock) { _lastMat = cam.CurrentMat.Clone(); }
            SetStep(MonitorWorkflowState.Analyzing, "采集完成进入分析");
        }

        /// <summary>执行步 30：图像分析。</summary>
        /// <remarks>调用算法分析并写入结果；同步直算，算法耗时可控。</remarks>
        private void DoAnalyze()
        {
            if (!AnalyzeImage())
            {
                FailFlow("分析失败");
                return;
            }
            SetStep(MonitorWorkflowState.Completed, "本轮检测完成");
        }

        /// <summary>执行步 800：成功收尾。</summary>
        /// <remarks>连续模式回到 10 起下一轮，单轮模式回 Standby。</remarks>
        private void DoFinishOk()
        {
            if (_continuous)
            {
                SetStep(MonitorWorkflowState.Acquiring, "连续检测进入下一轮");
                return;
            }
            SetStep(MonitorWorkflowState.Standby, "检测完成");
        }

        /// <summary>执行步 900：失败收尾三步走 —— 置 Alarm（经 SetWeldStatus 映射，禁手工赋值 State）</summary>
        /// <remarks>→ 记日志与故障记录 → 回待机执行步。</remarks>
        private void DoFinishFail()
        {
            SetStep(MonitorWorkflowState.ErrorAborted, FailReason);
            IsAlarm = true;
            FaultRecoveryManager.Instance.RecordFault(_tag, new FaultRecord
            {
                Time = DateTime.Now,
                Device = _tag,
                State = MapWeldStatus(_flowState),
                Category = FaultCategory.Device,
                ErrorCode = "MonitorStepFail",
                ParamSnapshot = string.Format("WorkStep={0} Phase={1} Reason={2}", FailStep, _phase, FailReason),
                AutoRecovered = false,
                RecoveryAction = "停止巡检，等待人工复位"
            });
            GlobalCommData.ShowLog(_tag, string.Format("监控检测异常终止 原因 {0}", FailReason), MessageLevel.Error);
            GoStep(StepIdle);
        }

        /// <summary>执行当前帧图像分析并暂存结果。</summary>
        /// <returns>分析成功返回 true</returns>
        private bool AnalyzeImage()
        {
            if (_lastMat == null || _lastMat.IsEmpty) return false;
            try
            {
                var mgr = MonitorAlgorithmManager.Instance;
                PreWeldAlignmentResult alignment = null;
                WeldQualityResult quality = null;
                Mat overlay;

                if (_phase == MonitorCameraPhase.PreWeldAlignment)
                {
                    alignment = ImageAlgorithm.DetectPreWeldAlignment(_lastMat, mgr.AlignmentConfig);
                    overlay = alignment.OverlayMat;
                }
                else
                {
                    quality = ImageAlgorithm.DetectWeldQuality(_lastMat, mgr.QualityConfig);
                    overlay = quality.OverlayMat;
                }

                lock (_resultLock)
                {
                    _resultMat = overlay;
                    FillResult(alignment, quality);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>按检测阶段把算法结果写入 _result（调用方已持有 _resultLock）</summary>
        /// <param name="alignment">焊前对齐结果（单轮模式使用）</param>
        /// <param name="quality">焊缝质量结果（连续模式使用）</param>
        private void FillResult(PreWeldAlignmentResult alignment, WeldQualityResult quality)
        {
            _result.Phase = _phase;
            _result.Timestamp = DateTime.Now;

            if (_phase == MonitorCameraPhase.PreWeldAlignment && alignment != null)
            {
                _result.OffsetX = alignment.OffsetX;
                _result.OffsetY = alignment.OffsetY;
                _result.Angle = alignment.AngleDeg;
                _result.Pass = alignment.Aligned;
                _result.QualityScore = alignment.Aligned ? 100 : 0;
                _result.DefectCount = 0;
                _result.Message = alignment.Aligned ? "对中合格" : "对中偏差超限";
            }
            else if (_phase == MonitorCameraPhase.WeldQuality && quality != null)
            {
                _result.OffsetX = 0;
                _result.OffsetY = 0;
                _result.Angle = 0;
                _result.DefectCount = quality.DefectCount;
                _result.QualityScore = quality.QualityScore;
                _result.Pass = quality.Pass;
                _result.Message = quality.Pass ? "焊接质量合格" : "焊接质量不合格";
            }
            else
            {
                _result.Pass = false;
                _result.QualityScore = 0;
                _result.DefectCount = 0;
                _result.Message = "分析未完成";
            }
        }

        #endregion

        #region 公共函数

        /// <summary>启动检测（phase 指定检测类型），continuous=true 则连续循环。</summary>
        /// <remarks>相机健康巡检由 MonitorCameraRun 自身启停，流程只发指令。</remarks>
        /// <param name="phase">检测类型</param>
        /// <param name="continuous">true 表示连续循环检测</param>
        public void Start(MonitorCameraPhase phase, bool continuous)
        {
            RequireStandby("Start");
            _phase = phase;
            _continuous = continuous;
            MonitorCameraRun.Instance.StartSupervision();
            SetStep(MonitorWorkflowState.Acquiring, "启动监控检测");
            StartRunLoop();
        }

        /// <summary>
        /// 停止（手动终止），同时停止相机健康巡检。
        /// </summary>
        public void Stop()
        {
            MonitorCameraRun.Instance.StopSupervision();
            if (_flowState != MonitorWorkflowState.ManualStopped)
                SetStep(MonitorWorkflowState.ManualStopped, "用户停止");
        }

        #endregion

        #region 执行步分派

        /// <summary>单步分派，由监听线程节拍驱动。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询 / 阻塞 WaitOne。</remarks>
        protected override void FlowProcess()
        {
            switch (WorkStep)
            {
                case StepAcquireTrigger: DoAcquireTrigger(); break;
                case StepAcquireWait: DoAcquireWait(); break;
                case StepAnalyze: DoAnalyze(); break;
                case StepFinishOk: DoFinishOk(); break;
                case StepFinishFail: DoFinishFail(); break;
                default:
                    // StepIdle 与未登记步号：空转等待，等待靠阶段态切换的 GoStep 复位
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
                case StepIdle: return "待机";
                case StepAcquireTrigger: return "触发采集";
                case StepAcquireWait: return "等待采集完成信号";
                case StepAnalyze: return "算法分析";
                case StepFinishOk: return "成功收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        #endregion

        #region 抽象生命周期方法（新主控设计 6 契约）

        /// <summary>开启连接（受理式）。</summary>
        /// <returns>受理返回 true，忙线返回 false</returns>
        public override bool ConnectOn()
        {
            if (_manualBusy) return false;
            _manualBusy = true;
            var t = new Thread(() =>
            {
                try
                {
                    GlobalCommData.ShowLog(_tag, "手动连接开始", MessageLevel.Info);
                    EnsureConnected();
                    bool ok = MonitorCameraRun.Instance.ConnectionState == MonitorCameraConnectionState.Connected;
                    SetState(ok ? SubDeviceState.Connected : SubDeviceState.Disconnected,
                        ok ? "监控相机连接完成" : "监控相机连接失败");
                    SetStep(ok ? MonitorWorkflowState.Standby : MonitorWorkflowState.Uninitialized,
                        ok ? "监控相机就绪" : "监控相机未连接");
                    GlobalCommData.ShowLog(_tag, ok ? "手动连接完成" : "手动连接失败", MessageLevel.Info);
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_tag, "手动连接异常 " + ex.Message, MessageLevel.Info);
                }
                finally
                {
                    _manualBusy = false;
                }
            })
            {
                Name = "MonitorCamManualConnect",
                IsBackground = true
            };
            t.Start();
            return true;
        }

        /// <summary>关闭连接：停止检测并置 Disconnected（非阻塞）。</summary>
        public override void ConnectOff()
        {
            Stop();
            SetState(SubDeviceState.Disconnected, "监控相机断开");
        }

        /// <summary>流程复位：停止检测并回到待机。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ResetProcess()
        {
            Stop();
            if (_flowState != MonitorWorkflowState.Standby)
                SetStep(MonitorWorkflowState.Standby, "流程复位");
            return true;
        }

        /// <summary>清除报警态并回就绪。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            ResetWorkStep();
            SetStep(MonitorWorkflowState.Standby, "状态清除");
        }

        /// <summary>复位时间：重置本流程工作时间计时。</summary>
        public override void ResetWorkTime()
        {
            SetWorkTimeStart();
        }

        #endregion

        #region 监听线程钩子与释放

        /// <summary>停巡检并释放图像资源。</summary>
        protected override void DisposeManaged()
        {
            try { MonitorCameraRun.Instance.StopSupervision(); } catch { }
            lock (_resultLock)
            {
                try { if (_lastMat != null) { _lastMat.Dispose(); _lastMat = null; } } catch { }
                try { if (_resultMat != null) { _resultMat.Dispose(); _resultMat = null; } } catch { }
            }
        }

        #endregion
    }

    /// <summary>监控相机工作流状态（只描述流程，不含 UI 显示等外部职责态）</summary>
    public enum MonitorWorkflowState
    {
        Uninitialized,
        Standby,
        Acquiring,
        Analyzing,
        Completed,
        ErrorAborted,
        ManualStopped
    }

    /// <summary>监控相机检测结果（流程读取算法输出后的只读数据，非事件参数）</summary>
    public class MonitorResult
    {
        public MonitorCameraPhase Phase { get; set; }
        public bool Pass { get; set; }
        public double QualityScore { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double Angle { get; set; }
        public int DefectCount { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
