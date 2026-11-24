﻿using System;
using System.Diagnostics;
using System.Threading;
using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.EmguALG.RealtimeMonitor.Playback;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WeldQuality;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.IMonitorCam;
using Emgu.CV;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>监控相机工作流（单例）。</summary>
    public class MonitorCameraWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 / 900 由基类提供）
        private const int StepAcquireTrigger = 10;
        private const int StepAcquireWait = 20;
        private const int StepAnalyze = 30;

        private const string DefaultIp = "192.168.1.100";
        private const string DefaultPort = "5000";

        /// <summary>主页面焊接预览帧率上限（Hz）</summary>
        private const int WeldPreviewHz = 30;

        /// <summary>调试页录像文件名前缀</summary>
        public const string DebugRecordPrefix = "MonitorCam_";

        /// <summary>主控焊接录像文件名前缀</summary>
        public const string WeldRecordPrefix = "焊接相机_";

        #endregion

        #region 私有变量


        private volatile bool _continuous;
        private volatile MonitorCameraPhase _phase = MonitorCameraPhase.PreWeldAlignment;

        /// <summary>连接/断开线程是否进行中（防重入）。</summary>
        private volatile bool _connectBusy;

        /// <summary>焊接预览是否开启（开=已订阅管理器预览事件）</summary>
        private volatile bool _weldPreviewOn;

        /// <summary>焊接预览开关注入锁</summary>
        private readonly object _weldPreviewLock = new object();

        /// <summary>上次上交预览帧的时间戳（30Hz 节流基准）</summary>
        private long _weldPreviewLastTicks;

        private readonly object _resultLock = new object();
        private Mat _lastMat;
        private Mat _resultMat;
        private readonly MonitorResult _result = new MonitorResult();

        // 失败收尾记录（子类私有字段）
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

        /// <summary>模组配置文件名（不含扩展名），在本流程类别目录。</summary>
        protected override string ConfigFileName { get { return "MonitorCameraWorkflow"; } }

        /// <summary>模组配置所属模块（全局配置目录下的模块层）。</summary>
        protected override string ConfigModule { get { return "MonitorCam"; } }

        /// <summary>焊接过程预览帧（已 30Hz 节流，订阅方拥有帧）</summary>
        public event Action<Mat> WeldPreviewFrame;

        /// <summary>最近一轮检测结果（只读快照，供 UI 拉取）</summary>
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
                        LateralDevMm = _result.LateralDevMm,
                        Grade = _result.Grade,
                        WireMatched = _result.WireMatched,
                        WireHeadAdhesion = _result.WireHeadAdhesion,
                        WireAnomalyDetected = _result.WireAnomalyDetected,
                        WireAnomalyReason = _result.WireAnomalyReason,
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

        #region 回放门面（算法能力在 EmguALG，本类只编排与转发）

        /// <summary>回放算法实例（算法层单例，生命周期不随本类）</summary>
        private static PlaybackAlgorithm Playback
        {
            get { return AlgorithmManager.Instance.Playback; }
        }

        /// <summary>是否已打开回放源</summary>
        public bool HasSource
        {
            get { return Playback.HasSource; }
        }

        /// <summary>是否正在播放</summary>
        public bool IsPlaying
        {
            get { return Playback.IsPlaying; }
        }

        /// <summary>当前回放倍速</summary>
        public double Speed
        {
            get { return Playback.Speed; }
        }

        /// <summary>扫描开始（在调用方线程同步抛出）</summary>
        public event Action<string> ScanStarted;

        /// <summary>扫描完成（后台线程抛出，UI 自行封送）</summary>
        public event Action<PlaybackScanResult> ScanCompleted;

        /// <summary>扫描线程异常（后台线程抛出）</summary>
        public event Action<string> ScanCrashed;

        /// <summary>缩略图就绪（后台线程抛出）</summary>
        public event Action<PlaybackFileInfo> ThumbnailReady;

        /// <summary>播放帧就绪（后台线程抛出）</summary>
        public event Action<Mat> FrameReady;

        /// <summary>播放进度（帧序号 / 总帧数 / 帧率）</summary>
        public event Action<int, int, double> ProgressChanged;

        /// <summary>播放状态（是否播放中 / 当前倍速）</summary>
        public event Action<bool, double> PlayStateChanged;

        /// <summary>把算法层回放事件转发到流程层门面</summary>
        private void WirePlaybackEvents()
        {
            PlaybackAlgorithm playback = Playback;
            playback.ScanStarted += OnPlaybackScanStarted;
            playback.ScanCompleted += OnPlaybackScanCompleted;
            playback.ScanCrashed += OnPlaybackScanCrashed;
            playback.ThumbnailReady += OnPlaybackThumbnailReady;
            playback.FrameReady += OnPlaybackFrameReady;
            playback.ProgressChanged += OnPlaybackProgress;
            playback.PlayStateChanged += OnPlaybackStateChanged;
        }

        private void OnPlaybackScanStarted(string caption)
        {
            ScanStarted?.Invoke(caption);
        }

        private void OnPlaybackScanCompleted(PlaybackScanResult result)
        {
            ScanCompleted?.Invoke(result);
        }

        private void OnPlaybackScanCrashed(string message)
        {
            ScanCrashed?.Invoke(message);
        }

        private void OnPlaybackThumbnailReady(PlaybackFileInfo info)
        {
            ThumbnailReady?.Invoke(info);
        }

        private void OnPlaybackFrameReady(Mat frame)
        {
            FrameReady?.Invoke(frame);
        }

        private void OnPlaybackProgress(int frameIndex, int totalFrames, double fps)
        {
            ProgressChanged?.Invoke(frameIndex, totalFrames, fps);
        }

        private void OnPlaybackStateChanged(bool isPlaying, double speed)
        {
            PlayStateChanged?.Invoke(isPlaying, speed);
        }

        /// <summary>取消进行中的区间扫描与缩略图解码</summary>
        public void CancelScan()
        {
            Playback.CancelScan();
        }

        /// <summary>按日期区间汇总录像（目录取自相机配置）</summary>
        /// <param name="start">起始日期（含）</param>
        /// <param name="end">结束日期（含）</param>
        public void LoadRange(DateTime start, DateTime end)
        {
            Playback.LoadRange(MonitorCamManager.Instance.RecordDirectory, start, end);
        }

        /// <summary>打开录像文件并启动播放</summary>
        /// <param name="path">录像文件完整路径</param>
        /// <returns>空串表示成功，否则为可展示的失败提示</returns>
        public string PlayFile(string path)
        {
            return Playback.PlayFile(path);
        }

        /// <summary>播放/暂停切换</summary>
        /// <returns>空串表示成功，否则为可展示的提示文案</returns>
        public string TogglePlayPause()
        {
            return Playback.TogglePlayPause();
        }

        /// <summary>慢放档位切换</summary>
        public void CycleSlow()
        {
            Playback.CycleSlow();
        }

        /// <summary>快放档位切换</summary>
        public void CycleFast()
        {
            Playback.CycleFast();
        }

        /// <summary>定位到指定帧</summary>
        /// <param name="frameIndex">目标帧序号</param>
        public void SeekToFrame(int frameIndex)
        {
            Playback.SeekToFrame(frameIndex);
        }

        /// <summary>相对跳转（±5 秒）</summary>
        /// <param name="seconds">相对秒数（负为后退）</param>
        public void SeekBySeconds(double seconds)
        {
            Playback.SeekBySeconds(seconds);
        }

        /// <summary>停止回放并释放解码资源</summary>
        public void StopPlayback()
        {
            Playback.Stop();
        }

        #endregion

        #region 构造函数

        private MonitorCameraWorkflow()
        {
            Tag = "监控相机工作流";
            WirePlaybackEvents();
        }

        #endregion

        #region 私有函数

        private void RequireStandby(string method)
        {
            if (WeldStatus != SubDeviceWeldStatus.Standby)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} 要求当前状态为 Standby，实际为 {1}", method, WeldStatus));
            }
        }

        /// <summary>确保相机已连接。</summary>
        private void EnsureConnected()
        {
            var cam = MonitorCamManager.Instance;
            if (cam.ConnectionState == MonitorCameraConnectionState.Connected) return;
            string ip = cam.Config != null ? cam.Config.IpAddress : DefaultIp;
            string port = cam.Config != null ? cam.Config.Port : DefaultPort;
            cam.OpenSensor(ip, port);
        }


        /// <summary>切换焊接过程态并把执行步复位到该阶段入口。</summary>
        /// <param name="status">目标焊接过程态（基类抽象状态）</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void EnterWeldStatus(SubDeviceWeldStatus status, string reason)
        {
            SetWeldStatus(status, reason);
            switch (status)
            {
                case SubDeviceWeldStatus.PreWork:
                    AdvanceStep(StepAcquireTrigger);
                    break;
                case SubDeviceWeldStatus.Working:
                    AdvanceStep(StepAnalyze);
                    break;
                default:
                    AdvanceStep(StepIdle);
                    break;
            }
        }

        /// <summary>转入失败收尾。</summary>
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
            var cam = MonitorCamManager.Instance;
            cam.SetPhase(_phase);
            cam.AcquisitionCompletedSignal.Reset();
            EnsureConnected();
            cam.SensorRunContinue();
            return true;
        }

        /// <summary>执行步 20：等待采集完成信号。</summary>

        /// <returns>采集到有效图像返回 true</returns>
        private bool DoAcquireWait()
        {
            var cam = MonitorCamManager.Instance;
            if (!cam.AcquisitionCompletedSignal.WaitOne(0))
            {
                if (StepWorkTime.TotalSeconds > DeviceWaitSettings.CameraAcquireSeconds)
                    Fail(string.Format("采集等待信号超时 {0:0.#}秒 {1}",
                        DeviceWaitSettings.CameraAcquireSeconds,
                        cam.DescribeLastError(MonitorCamErrorContext.Capture)));
                return false;
            }
            if (!cam.LastAcquisitionSuccess || cam.CurrentMat == null || cam.CurrentMat.IsEmpty)
            {
                Fail("采集结果无效 图像为空或采集失败 " + cam.DescribeLastError(MonitorCamErrorContext.Capture));
                return false;
            }
            lock (_resultLock) { _lastMat = cam.CurrentMat.Clone(); }
            return true;
        }

        /// <summary>执行步 30：图像分析。</summary>

        /// <returns>分析成功返回 true</returns>
        private bool DoAnalyze()
        {
            if (!AnalyzeImage())
            {
                Fail("分析失败 " + MonitorCamManager.Instance.DescribeLastError(MonitorCamErrorContext.Analyze));
                return false;
            }
            return true;
        }

        /// <summary>执行步 800：成功收尾。</summary>

        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishOk()
        {
            if (_continuous)
            {
                SetWeldStatus(SubDeviceWeldStatus.Working, "连续检测进入下一轮");
                return false;
            }
            SetWeldStatus(SubDeviceWeldStatus.Standby, "检测完成");
            return true;
        }

        /// <summary>执行步 900：失败收尾三步走 —— 置 Alarm（经 SetWeldStatus 映射，禁手工赋值 State）</summary>

        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, _failReason);
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
                var algorithm = AlgorithmManager.Instance;
                WireFeedResult alignment = null;
                WeldQualityResult quality = null;
                Mat overlay;

                if (_phase == MonitorCameraPhase.PreWeldAlignment)
                {
                    alignment = algorithm.DetectWireFeed(_lastMat);
                    overlay = alignment.OverlayMat;
                }
                else
                {
                    quality = algorithm.DetectWeldQuality(_lastMat);
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
        private void FillResult(WireFeedResult alignment, WeldQualityResult quality)
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
                // 毫米化 + 三级判定 + 剪丝判据
                _result.WireStickoutDeviation = alignment.WireStickoutDevMm;
                _result.LateralDevMm = alignment.LateralDevMm;
                _result.Grade = alignment.Grade;
                _result.WireMatched = alignment.WireMatched;
                _result.WireHeadAdhesion = alignment.WireHeadAdhesion;
                _result.WireAnomalyDetected = !alignment.WireMatched || alignment.WireHeadAdhesion;
                _result.WireAnomalyReason = _result.WireAnomalyDetected
                    ? (!alignment.WireMatched ? "焊丝未识别" : "焊丝头部粘连")
                    : string.Empty;
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

        /// <summary>管理器预览帧入口：按 30Hz 节流后上交</summary>

        /// <param name="frame">订阅方副本</param>
        private void OnManagerPreviewFrame(Mat frame)
        {
            if (frame == null) return;
            if (!_weldPreviewOn)
            {
                DisposeFrame(frame);
                return;
            }
            long minTicks = Stopwatch.Frequency / WeldPreviewHz;
            long now = Stopwatch.GetTimestamp();
            if (now - _weldPreviewLastTicks < minTicks)
            {
                DisposeFrame(frame);
                return;
            }
            _weldPreviewLastTicks = now;
            Action<Mat> handler = WeldPreviewFrame;
            if (handler == null)
            {
                DisposeFrame(frame);
                return;
            }
            // 正常返回即所有权移交订阅方
            handler(frame);
        }

        /// <summary>释放被节流丢弃的帧</summary>
        /// <param name="frame">待释放帧</param>
        private static void DisposeFrame(Mat frame)
        {
            try { frame.Dispose(); } catch { }   // 释放路径：失败不影响预览
        }

        #endregion

        #region 公共函数

        /// <summary>启动检测（phase 指定检测类型），continuous=true 则连续循环。</summary>

        /// <param name="phase">检测类型</param>
        /// <param name="continuous">true 表示连续循环检测</param>
        public void Start(MonitorCameraPhase phase, bool continuous)
        {
            RequireStandby("Start");
            _phase = phase;
            _continuous = continuous;
            MonitorCamManager.Instance.StartSupervision();
            SetWeldStatus(SubDeviceWeldStatus.Working, "启动监控检测");
        }

        /// <summary>停止检测并停相机健康巡检。</summary>
        public void Stop()
        {
            MonitorCamManager.Instance.StopSupervision();
            if (WeldStatus != SubDeviceWeldStatus.ManualStopped)
                SetWeldStatus(SubDeviceWeldStatus.ManualStopped, ModeText + "停止");
        }

        /// <summary>开始录像（前置条件、文件名与目录策略归本层）</summary>
        /// <param name="prefix">文件名前缀（调试页 / 主控各自口径）</param>
        /// <param name="fileName">成功时为落盘文件名；已在录像或失败时为空串</param>
        /// <param name="failureMessage">失败提示（成功时为空串）</param>
        /// <returns>成功或已在录像返回 true</returns>
        public bool StartRecording(string prefix, out string fileName, out string failureMessage)
        {
            var cam = MonitorCamManager.Instance;
            fileName = string.Empty;
            failureMessage = string.Empty;
            if (cam.ConnectionState != MonitorCameraConnectionState.Connected)
            {
                failureMessage = "请先连接相机！";
                return false;
            }
            if (!cam.IsCapturing)
            {
                failureMessage = "请先开始采集，再开始录像！";
                return false;
            }
            if (cam.IsRecording) return true;

            fileName = string.Format("{0}{1:yyyyMMdd_HHmmss}.avi", prefix, DateTime.Now);
            if (!cam.StartRecord(fileName))
            {
                fileName = string.Empty;
                failureMessage = "开始录像失败，请查看日志！";
                return false;
            }
            return true;
        }

        /// <summary>停止录像（调用方只发起）</summary>
        public void StopRecording()
        {
            MonitorCamManager.Instance.StopRecord();
        }

        /// <summary>开启焊接过程预览（幂等，焊接开始调用）</summary>
        public void StartWeldPreview()
        {
            lock (_weldPreviewLock)
            {
                if (_weldPreviewOn) return;
                _weldPreviewLastTicks = 0;
                _weldPreviewOn = true;
                MonitorCamManager.Instance.PreviewFrameEvent += OnManagerPreviewFrame;
            }
            Log("焊接预览开启 节流 " + WeldPreviewHz + "Hz", MessageLevel.Info);
        }

        /// <summary>关闭焊接过程预览（幂等，焊接结束/失败/急停调用）</summary>
        public void StopWeldPreview()
        {
            lock (_weldPreviewLock)
            {
                if (!_weldPreviewOn) return;
                _weldPreviewOn = false;
                MonitorCamManager.Instance.PreviewFrameEvent -= OnManagerPreviewFrame;
            }
            Log("焊接预览关闭", MessageLevel.Info);
        }

        #endregion

        #region 常驻流程

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进。</summary>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            switch (RunStep)
            {
                case StepAcquireTrigger:
                    if (DoAcquireTrigger()) AdvanceStep(StepAcquireWait);
                    break;
                case StepAcquireWait:
                    if (DoAcquireWait()) SetWeldStatus(SubDeviceWeldStatus.Working, "采集完成进入分析");
                    break;
                case StepAnalyze:
                    if (DoAnalyze()) SetWeldStatus(SubDeviceWeldStatus.Standby, "本轮检测完成");
                    break;
                case StepFinishOk:
                    if (DoFinishOk()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                default:
                    // StepIdle 与未登记步号：空转等待，靠流程态切换复位
                    break;
            }
        }

        /// <summary>流程复位：停止检测并回到待机（线性阻塞链）。</summary>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            Stop();
            if (WeldStatus != SubDeviceWeldStatus.Standby)
                SetWeldStatus(SubDeviceWeldStatus.Standby, "流程复位");
            return true;
        }

        /// <summary>状态清理：标志位全复位 + 执行步归零 + 强制未复位</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _failReason = "";
            _failStep = 0;
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入</summary>
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
                Log("监控相机初始化连接开始", MessageLevel.Debug);
                BeginConnectAttempt();
                EnsureConnected();
                bool ok = MonitorCamManager.Instance.ConnectionState == MonitorCameraConnectionState.Connected;
                if (ok)
                    SetState(SubDeviceState.Connected, "监控相机初始化连接完成");
                else
                    MarkConnectFailed("未枚举到海康相机；"
                        + MonitorCamManager.Instance.DescribeLastError(MonitorCamErrorContext.Connection));
                EnterWeldStatus(ok ? SubDeviceWeldStatus.Standby : SubDeviceWeldStatus.NoReset,
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
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开");
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

        /// <summary>设备日志：输出监控相机差异化日志（轴控制行为</summary>
        public override void SubDeviceLog()
        {
            // todo 轴控制与送丝距离合规检查待补
            MonitorResult snap = LastResult;
            Log(string.Format("监控相机 连接态 {0} 焊接过程态 {1} 步骤 {2} 检测阶段 {3} 合格 {4} 缺陷数 {5}",
                State, WeldStatus, WorkStepName, snap.Phase, snap.Pass, snap.DefectCount));
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
        public override void WeldStatusTrans()
        {
            if (WeldStatus == SubDeviceWeldStatus.NoReset) return;
            if (WeldStatus == SubDeviceWeldStatus.ManualStopped
                || WeldStatus == SubDeviceWeldStatus.ErrorAborted)
            {
                SetWeldStatus(SubDeviceWeldStatus.Standby, "焊接状态切换管控");
            }
        }

        #endregion

        #region 非阻塞动作指令

        /// <summary>开启连接（非阻塞）：自起后台线程执行连接，动作即</summary>

        /// <returns>受理返回 true，忙线返回 false</returns>
        public override bool ConnectOn()
        {
            if (_connectBusy) return false;
            _connectBusy = true;
            BeginConnectAttempt();
            // 模式在入口线程捕获，后台线程只读快照
            WorkflowRunMode mode = RunMode;
            var t = new Thread(() =>
            {
                try
                {
                    LogAction(mode, "连接开始", MessageLevel.Debug);
                    EnsureConnected();
                    bool ok = MonitorCamManager.Instance.ConnectionState == MonitorCameraConnectionState.Connected;
                    if (ok)
                        SetState(SubDeviceState.Connected, "监控相机连接完成");
                    else
                        MarkConnectFailed("未枚举到海康相机；"
                            + MonitorCamManager.Instance.DescribeLastError(MonitorCamErrorContext.Connection));
                    EnterWeldStatus(ok ? SubDeviceWeldStatus.Standby : SubDeviceWeldStatus.NoReset,
                        ok ? "监控相机就绪" : "监控相机未连接");
                    LogAction(mode, ok ? "连接完成" : "连接失败", MessageLevel.Debug);
                }
                catch (Exception ex)
                {
                    MarkConnectFailed("监控相机连接异常 " + ex.Message);
                    LogAction(mode, "连接异常 " + ex.Message, MessageLevel.Debug);
                }
                finally
                {
                    _connectBusy = false;
                }
            })
            {
                Name = "MonitorCamConnect",
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
            try { StopWeldPreview(); } catch { }   // 退订预览：失败不阻断释放
            try { MonitorCamManager.Instance.StopSupervision(); } catch { }   // 停监督失败不阻断后续释放
            lock (_resultLock)
            {
                try { _lastMat?.Dispose(); _lastMat = null; } catch { }   // 释放缓冲：失败不影响功能
                try { _resultMat?.Dispose(); _resultMat = null; } catch { }   // 释放缓冲：失败不影响功能
            }
        }

        #endregion
    }


    /// <summary>监控相机检测结果（流程读取后的只读数据）</summary>
    public class MonitorResult
    {
        /// <summary>检测所处阶段</summary>
        public MonitorCameraPhase Phase { get; set; }

        /// <summary>检测是否合格</summary>
        public bool Pass { get; set; }

        /// <summary>质量评分</summary>
        public double QualityScore { get; set; }

        /// <summary>光斑对准偏差 X（供控制器驱动振镜摆动光斑）。</summary>
        public double OffsetX { get; set; }

        /// <summary>光斑对准偏差 Y（供控制器驱动振镜摆动光斑）。</summary>
        public double OffsetY { get; set; }

        /// <summary>送丝距离差距值（供机器人调整送丝距离）。</summary>
        public double WireStickoutDeviation { get; set; }

        /// <summary>横向偏差（mm，Y 向）：只报警不自动调整，人工修正</summary>
        public double LateralDevMm { get; set; }

        /// <summary>三级判定（Pass 安全 / Warn 黄灯提示 / Fail 错误报警）</summary>
        public WireAlignGrade Grade { get; set; }

        /// <summary>焊丝是否识别成功（false ⇒ 断丝/送丝异常）</summary>
        public bool WireMatched { get; set; }

        /// <summary>焊丝头部是否粘连</summary>
        public bool WireHeadAdhesion { get; set; }

        /// <summary>焊丝异常标志位（剪丝触发：未识别或头部粘连）</summary>
        public bool WireAnomalyDetected { get; set; }

        /// <summary>焊丝异常原因（纯文本）</summary>
        public string WireAnomalyReason { get; set; }

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
