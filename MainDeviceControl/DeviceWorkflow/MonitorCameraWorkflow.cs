using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.EmguALG;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.Api;
using Emgu.CV;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>监控相机工作流（单例）。</summary>
    /// <remarks>数据归 MonitorCameraRun（图像与健康巡检），结果经 LastResult/LastOverlay 只读拉取；差异化职责（原档 R010）轴控制行为 + 送丝距离合规检查。</remarks>
    public class MonitorCameraWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepAcquireTrigger = 10;
        private const int StepAcquireWait = 20;
        private const int StepAnalyze = 30;

        /// <summary>单步采集超时阈值（秒）。</summary>
        private const int AcquireTimeoutSeconds = 5;

        private const string DefaultIp = "192.168.1.100";
        private const string DefaultPort = "5000";

        #endregion

        #region 私有变量

        /// <summary>私有流程态：仅驱动 FlowProcess 与可观测性，对外以 SubDeviceWeldStatus 暴露。</summary>
        private MonitorWorkflowState _flowState = MonitorWorkflowState.Uninitialized;

        private volatile bool _continuous;
        private volatile MonitorCameraPhase _phase = MonitorCameraPhase.PreWeldAlignment;

        /// <summary>手动连接/断开线程是否进行中（防重入）。</summary>
        private volatile bool _manualBusy;

        private readonly object _resultLock = new object();
        private Mat _lastMat;
        private Mat _resultMat;
        private readonly MonitorResult _result = new MonitorResult();

        // 失败收尾记录（原基类 FailReason / FailStep 已删除，下沉为子类私有字段）
        private string _failReason = "";
        private int _failStep;

        #endregion

        #region 单例

        private static readonly Lazy<MonitorCameraWorkflow> _lazyInstance =
            new Lazy<MonitorCameraWorkflow>(() => new MonitorCameraWorkflow());

        /// <summary>监控相机工作流单例。</summary>
        public static MonitorCameraWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        /// <summary>对外名片名（日志与事件标识）。</summary>
        public override string StateName { get { return Tag; } }

        /// <summary>子设备配置文件名（不含扩展名），落在 Config/XML/ 下。</summary>
        protected override string ConfigFileName { get { return "MonitorCameraWorkflow"; } }

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
                        WireStickoutDeviation = _result.WireStickoutDeviation,
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

        #endregion

        #region 构造函数

        private MonitorCameraWorkflow()
        {
            Tag = "监控相机工作流";
        }

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
        private static SubDeviceWeldStatus MapWeldStatus(MonitorWorkflowState state)
        {
            switch (state)
            {
                case MonitorWorkflowState.ErrorAborted:
                    return SubDeviceWeldStatus.ErrorAborted;
                case MonitorWorkflowState.ManualStopped:
                    return SubDeviceWeldStatus.ManualStopped;
                case MonitorWorkflowState.Uninitialized:
                    return SubDeviceWeldStatus.NoReset;
                case MonitorWorkflowState.Standby:
                    return SubDeviceWeldStatus.Standby;
                default:
                    // Acquiring / Analyzing / Completed 均为进程态
                    return SubDeviceWeldStatus.Working;
            }
        }

        /// <summary>刷新流程相位并映射为对外焊接过程态，同时把执行步复位到该阶段入口。</summary>
        /// <param name="state">目标流程态</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetPhase(MonitorWorkflowState state, string reason)
        {
            _flowState = state;
            SetWeldStatus(MapWeldStatus(state), reason);
            switch (state)
            {
                case MonitorWorkflowState.Acquiring:
                    AdvanceStep(StepAcquireTrigger);
                    break;
                case MonitorWorkflowState.Analyzing:
                    AdvanceStep(StepAnalyze);
                    break;
                case MonitorWorkflowState.Completed:
                    AdvanceStep(StepFinishOk);
                    break;
                default:
                    AdvanceStep(StepIdle);
                    break;
            }
        }

        /// <summary>转入失败收尾（替代已删除的基类 FailFlow）。</summary>
        /// <param name="reason">失败原因（纯文本，无符号）</param>
        private void Fail(string reason)
        {
            _failStep = RunStep;
            _failReason = reason;
            AdvanceStep(StepFinishFail);
        }

        /// <summary>执行步 10：下发检测阶段并触发一次采集。</summary>
        /// <returns>触发完成返回 true</returns>
        private bool DoAcquireTrigger()
        {
            var cam = MonitorCameraRun.Instance;
            cam.SetPhase(_phase);
            cam.AcquisitionCompletedSignal.Reset();
            EnsureConnected();
            cam.SensorRunContinue();
            return true;
        }

        /// <summary>执行步 20：等待采集完成信号。</summary>
        /// <remarks>WaitOne(0) 只做非阻塞探测，等待由外部编排线程重入完成，超时由步骤计时判定， 超时即转失败收尾，不留在原步死等（ADR-005 有界可失败语义）。</remarks>
        /// <returns>采集到有效图像返回 true</returns>
        private bool DoAcquireWait()
        {
            var cam = MonitorCameraRun.Instance;
            if (!cam.AcquisitionCompletedSignal.WaitOne(0))
            {
                if (StepWorkTime.TotalSeconds > AcquireTimeoutSeconds)
                    Fail("采集等待信号超时 5秒");
                return false;
            }
            if (!cam.LastAcquisitionSuccess || cam.CurrentMat == null || cam.CurrentMat.IsEmpty)
            {
                Fail("采集结果无效 图像为空或采集失败");
                return false;
            }
            lock (_resultLock) { _lastMat = cam.CurrentMat.Clone(); }
            return true;
        }

        /// <summary>执行步 30：图像分析。</summary>
        /// <remarks>调用算法分析并写入结果；同步直算，算法耗时可控。</remarks>
        /// <returns>分析成功返回 true</returns>
        private bool DoAnalyze()
        {
            if (!AnalyzeImage())
            {
                Fail("分析失败");
                return false;
            }
            return true;
        }

        /// <summary>执行步 800：成功收尾。</summary>
        /// <remarks>连续模式回到 10 起下一轮，单轮模式回 Standby。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishOk()
        {
            if (_continuous)
            {
                SetPhase(MonitorWorkflowState.Acquiring, "连续检测进入下一轮");
                return false;
            }
            SetPhase(MonitorWorkflowState.Standby, "检测完成");
            return true;
        }

        /// <summary>执行步 900：失败收尾三步走 —— 置 Alarm（经 SetWeldStatus 映射，禁手工赋值 State）</summary>
        /// <remarks>→ 记日志与故障记录 → 回待机执行步。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetPhase(MonitorWorkflowState.ErrorAborted, _failReason);
            IsAlarm = true;
            Log(string.Format("监控检测异常终止 原因 {0}", _failReason), MessageLevel.Error);
            return true;
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
            SetPhase(MonitorWorkflowState.Acquiring, "启动监控检测");
        }

        /// <summary>停止（手动终止）并停相机健康巡检。</summary>
        public void Stop()
        {
            MonitorCameraRun.Instance.StopSupervision();
            if (_flowState != MonitorWorkflowState.ManualStopped)
                SetPhase(MonitorWorkflowState.ManualStopped, "用户停止");
        }

        #endregion

        #region 全阻塞流程

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询 / 阻塞 WaitOne。</remarks>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            switch (RunStep)
            {
                case StepAcquireTrigger:
                    if (DoAcquireTrigger()) AdvanceStep(StepAcquireWait);
                    break;
                case StepAcquireWait:
                    if (DoAcquireWait()) SetPhase(MonitorWorkflowState.Analyzing, "采集完成进入分析");
                    break;
                case StepAnalyze:
                    if (DoAnalyze()) SetPhase(MonitorWorkflowState.Completed, "本轮检测完成");
                    break;
                case StepFinishOk:
                    if (DoFinishOk()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                default:
                    // StepIdle 与未登记步号：空转等待，靠流程态切换把执行步复位
                    break;
            }
        }

        /// <summary>流程复位：停止检测并回到待机（线性阻塞链）。</summary>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            Stop();
            if (_flowState != MonitorWorkflowState.Standby)
                SetPhase(MonitorWorkflowState.Standby, "流程复位");
            SetWeldStatus(SubDeviceWeldStatus.Standby, "监控相机复位完成");
            return true;
        }

        /// <summary>状态清理：标志位全复位 + 执行步归零 + 强制未复位态。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _failReason = "";
            _failStep = 0;
            SetPhase(MonitorWorkflowState.Uninitialized, "状态清除");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入步骤时长导致异常超时。</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：同步等待相机打开完成。</summary>
        /// <returns>已连接返回 true</returns>
        public override bool InitializeOn()
        {
            try
            {
                Log("监控相机初始化连接开始", MessageLevel.Info);
                BeginConnectAttempt();
                EnsureConnected();
                bool ok = MonitorCameraRun.Instance.ConnectionState == MonitorCameraConnectionState.Connected;
                if (ok)
                    SetState(SubDeviceState.Connected, "监控相机初始化连接完成");
                else
                    MarkConnectFailed("监控相机初始化连接失败");
                SetPhase(ok ? MonitorWorkflowState.Standby : MonitorWorkflowState.Uninitialized,
                    ok ? "监控相机就绪" : "监控相机未连接");
                if (ok) SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化连接完成，等待复位");
                return ok;
            }
            catch (Exception ex)
            {
                MarkConnectFailed("监控相机初始化连接异常 " + ex.Message);
                Log("监控相机初始化连接异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备断开（阻塞）：停止检测并置 Disconnected。</summary>
        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            try
            {
                Log("监控相机初始化断开开始", MessageLevel.Info);
                Stop();
                SetPhase(MonitorWorkflowState.Uninitialized, "初始化断开");
                SetState(SubDeviceState.Disconnected, "监控相机初始化断开");
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开完成");
                return true;
            }
            catch (Exception ex)
            {
                Log("监控相机初始化断开异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备日志：输出监控相机差异化日志（轴控制行为与送丝距离合规）。</summary>
        public override void SubDeviceLog()
        {
            // todo 轴控制行为与送丝距离合规检查（原档 R010 / §7.7 第 4 项）落地后在此补充
            MonitorResult snap = LastResult;
            Log(string.Format("监控相机 连接态 {0} 焊接过程态 {1} 步骤 {2} 检测阶段 {3} 合格 {4} 缺陷数 {5}",
                State, WeldStatus, WorkStepName, snap.Phase, snap.Pass, snap.DefectCount));
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
            if ((WeldStatus == SubDeviceWeldStatus.ManualStopped
                || WeldStatus == SubDeviceWeldStatus.ErrorAborted)
                && _flowState != MonitorWorkflowState.Standby)
            {
                SetPhase(MonitorWorkflowState.Standby, "焊接状态切换管控");
            }
        }

        #endregion

        #region 非阻塞流程

        /// <summary>开启连接（非阻塞）：自起后台线程执行连接，动作即退出释放。</summary>
        /// <returns>受理返回 true，忙线返回 false</returns>
        public override bool ConnectOn()
        {
            if (_manualBusy) return false;
            _manualBusy = true;
            BeginConnectAttempt();
            var t = new Thread(() =>
            {
                try
                {
                Log("手动连接开始", MessageLevel.Info);
                EnsureConnected();
                bool ok = MonitorCameraRun.Instance.ConnectionState == MonitorCameraConnectionState.Connected;
                if (ok)
                    SetState(SubDeviceState.Connected, "监控相机连接完成");
                else
                    MarkConnectFailed("监控相机连接失败");
                SetPhase(ok ? MonitorWorkflowState.Standby : MonitorWorkflowState.Uninitialized,
                    ok ? "监控相机就绪" : "监控相机未连接");
                Log(ok ? "手动连接完成" : "手动连接失败", MessageLevel.Info);
            }
            catch (Exception ex)
            {
                MarkConnectFailed("监控相机连接异常 " + ex.Message);
                Log("手动连接异常 " + ex.Message, MessageLevel.Info);
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

        /// <summary>关闭连接（非阻塞）：停止检测并置 Disconnected。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOff()
        {
            Stop();
            SetState(SubDeviceState.Disconnected, "监控相机断开");
            return true;
        }

        #endregion

        #region 可观测性与释放

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
        /// <summary>检测所处阶段</summary>
        public MonitorCameraPhase Phase { get; set; }

        /// <summary>检测是否合格</summary>
        public bool Pass { get; set; }

        /// <summary>质量评分</summary>
        public double QualityScore { get; set; }

        /// <summary>光斑对准偏差 X（供控制器驱动振镜摆动光斑，原档20260904 §7.4.5）。</summary>
        public double OffsetX { get; set; }

        /// <summary>光斑对准偏差 Y（供控制器驱动振镜摆动光斑，原档20260904 §7.4.5）。</summary>
        public double OffsetY { get; set; }

        /// <summary>送丝距离差距值（供机器人调整送丝距离，原档20260904 §7.4.5）。</summary>
        /// <remarks>用户 2026-09-04 裁定：监控相机只输出检测结果，
        /// 本值由机器人消费以控制送丝距离，不由相机自行补偿。</remarks>
        public double WireStickoutDeviation { get; set; }

        /// <summary>检测角度</summary>
        public double Angle { get; set; }

        /// <summary>缺陷数量</summary>
        public int DefectCount { get; set; }

        /// <summary>结果描述</summary>
        public string Message { get; set; }

        /// <summary>结果生成时间</summary>
        public DateTime Timestamp { get; set; }
    }
}
