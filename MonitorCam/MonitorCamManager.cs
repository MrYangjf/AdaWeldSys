using AdaWeldSystem.Comm;
using AdaWeldSystem.MonitorCam.IMonitorCam;
using AdaWeldSystem.MonitorCam.MecaVisionCam;
using Emgu.CV;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AdaWeldSystem.MonitorCam
{
    /// <summary>
    /// 监控相机业务中枢（单例，对外唯一入口）。
    /// 三层职责（ADR-035）：实现层（MecaVisionCam）只抛帧，本类承担连接编排、模式切换、
    /// 帧分发与健康巡检；抽象层契约见 IMonitorCamApi。
    /// 相机自身的健康巡检（连接态 + 近期有效采集）归属本类，不属工作流职责（权责边界 R1）。
    /// </summary>
    public class MonitorCamManager
    {
        #region 私有变量

        private static readonly Lazy<MonitorCamManager> _lazy =
            new Lazy<MonitorCamManager>(() => new MonitorCamManager());

        private readonly string _tag = "监控相机管理器";
        private readonly object _stateLock = new object();
        private readonly object _matLock = new object();

        private readonly IMonitorCamApi _api;
        private MonitorCamConfig _config;
        private MonitorCameraPhase _currentPhase = MonitorCameraPhase.PreWeldAlignment;
        private Mat _currentMat;
        private bool _isDisposed = false;
        private bool _lastAcquisitionSuccess = false;

        // ---- 健康巡检字段（相机自身状态，按权责边界由工作流下沉至此）----
        private MonitorSupervisionState _supervisionState = MonitorSupervisionState.Idle;
        private DateTime _lastHealthyUtc = DateTime.MinValue;
        private volatile bool _supervisionRunning;
        private Thread _supervisionThread;
        private int _checkIntervalMs = 1000;
        private int _healthyValidMs = 5000;
        private readonly MonitorSupervisionStatusChangedEventArgs _statusArgs =
            new MonitorSupervisionStatusChangedEventArgs();

        #endregion

        #region 公共变量

        /// <summary>采集完成信号（Workflow 回调等待模式，参考 ADR-003）</summary>
        public readonly AutoResetEvent AcquisitionCompletedSignal = new AutoResetEvent(false);

        /// <summary>是否处于 Live（持续采集）模式</summary>
        public bool IsLiveMode = false;

        /// <summary>全局唯一实例</summary>
        public static MonitorCamManager Instance
        {
            get { return _lazy.Value; }
        }

        /// <summary>当前帧（最近一次采集到的图像）</summary>
        public Mat CurrentMat
        {
            get { lock (_matLock) { return _currentMat; } }
        }

        /// <summary>相机是否处于可运行状态（已连接）</summary>
        public bool IsRun
        {
            get { return _api != null && _api.ConnectionState == MonitorCameraConnectionState.Connected; }
        }

        /// <summary>当前连接状态</summary>
        public MonitorCameraConnectionState ConnectionState
        {
            get { return _api != null ? _api.ConnectionState : MonitorCameraConnectionState.Disconnected; }
        }

        /// <summary>相机硬件配置</summary>
        public MonitorCamConfig Config
        {
            get { return _config; }
            set { _config = value; }
        }

        /// <summary>最近一次采集是否成功</summary>
        public bool LastAcquisitionSuccess
        {
            get { return _lastAcquisitionSuccess; }
            private set { _lastAcquisitionSuccess = value; }
        }

        /// <summary>当前健康监督状态</summary>
        public MonitorSupervisionState SupervisionState
        {
            get { lock (_stateLock) { return _supervisionState; } }
            private set { lock (_stateLock) { _supervisionState = value; } }
        }

        /// <summary>相机是否正在正常工作（已连接 + 近期有有效采集）</summary>
        public bool IsMonitorCameraWorking
        {
            get
            {
                if (ConnectionState != MonitorCameraConnectionState.Connected)
                    return false;
                return (DateTime.Now - LastHealthyTime).TotalMilliseconds <= _healthyValidMs;
            }
        }

        /// <summary>最近一次有效采集时间</summary>
        public DateTime LastHealthyTime
        {
            get { lock (_stateLock) { return _lastHealthyUtc; } }
        }

        /// <summary>巡检周期（毫秒），默认 1000</summary>
        public int CheckIntervalMs
        {
            get { return _checkIntervalMs; }
            set { _checkIntervalMs = value > 0 ? value : 1000; }
        }

        /// <summary>健康有效期（毫秒），默认 5000</summary>
        public int HealthyValidMs
        {
            get { return _healthyValidMs; }
            set { _healthyValidMs = value > 0 ? value : 5000; }
        }

        #endregion

        #region 事件

        /// <summary>帧采集完成事件（供工作流与 UI 订阅）</summary>
        public static event MonitorCameraFrameCompletedHandler FrameCompletedEvent;

        /// <summary>帧采集完成事件处理委托</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">帧完成事件参数</param>
        public delegate void MonitorCameraFrameCompletedHandler(object sender, MonitorCameraFrameCompletedEventArgs e);

        /// <summary>健康监督状态变更事件（相机自身状态，供 UI 订阅）</summary>
        public event EventHandler<MonitorSupervisionStatusChangedEventArgs> StatusChanged;

        #endregion

        #region 构造函数

        private MonitorCamManager()
        {
            _config = new MonitorCamConfig();
            _config.LoadFromIni(MonitorCamConfig.DefaultConfigPath);
            _api = new MecaVisionCam.MecaVisionCam();
            _api.FrameReceived += OnApiFrameReceived;
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 打开相机连接
        /// </summary>
        /// <param name="ip">相机 IP</param>
        /// <param name="port">相机端口</param>
        public void OpenSensor(string ip, string port)
        {
            if (ConnectionState == MonitorCameraConnectionState.Connected)
                return;
            if (_config == null) _config = new MonitorCamConfig();
            _config.IpAddress = ip;
            _config.Port = port;
            _api.Connect(ip, port);
        }

        /// <summary>
        /// 设置当前检测阶段（影响帧事件携带的阶段语义）
        /// </summary>
        /// <param name="phase">目标阶段</param>
        public void SetPhase(MonitorCameraPhase phase)
        {
            _currentPhase = phase;
        }

        /// <summary>
        /// 切换 Live / PIL 模式
        /// </summary>
        /// <param name="enableLive">true 进入持续采集，false 停止</param>
        public void ChangeMode(bool enableLive)
        {
            IsLiveMode = enableLive;
            if (IsLiveMode)
                _api.StartAcquisition();
            else
                _api.StopAcquisition();
        }

        /// <summary>
        /// 触发一次采集（PIL 模式）。Live 模式下不响应。
        /// </summary>
        public void SensorRunContinue()
        {
            if (ConnectionState != MonitorCameraConnectionState.Connected)
                return;
            if (IsLiveMode)
                return;
            Task t = new Task(AcquireOnce);
            t.Start();
        }

        /// <summary>停止采集并退出 Live 模式</summary>
        public void SensorStop()
        {
            IsLiveMode = false;
            _api.StopAcquisition();
        }

        /// <summary>启动健康巡检</summary>
        public void StartSupervision()
        {
            if (_supervisionRunning) return;
            _supervisionRunning = true;
            _lastHealthyUtc = DateTime.MinValue;
            SupervisionTransitionTo(MonitorSupervisionState.Checking, "监控相机健康巡检启动");
            _supervisionThread = new Thread(SupervisionLoop);
            _supervisionThread.Name = "MonitorCamSupervision";
            _supervisionThread.IsBackground = true;
            _supervisionThread.Start();
        }

        /// <summary>停止健康巡检</summary>
        public void StopSupervision()
        {
            _supervisionRunning = false;
            SupervisionTransitionTo(MonitorSupervisionState.Idle, "监控相机健康巡检停止");
        }

        /// <summary>释放相机资源</summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopSupervision();
            try { _api.FrameReceived -= OnApiFrameReceived; } catch { }
            try { _api.Dispose(); } catch { }
        }

        #endregion

        #region 私有函数

        /// <summary>实现层帧回调：缓存当前帧 → 标记健康 → 向上抛帧</summary>
        private void OnApiFrameReceived(object sender, MonitorFrameEventArgs e)
        {
            lock (_matLock)
            {
                _currentMat = e.Frame;
            }
            MarkHealthy();
            RaiseFrameCompleted(e.Frame, true, true);
        }

        /// <summary>PIL 模式单次采集（异步任务体）</summary>
        private void AcquireOnce()
        {
            try
            {
                _api.StopAcquisition();
                Mat frame = _api.CaptureSingleFrame();
                lock (_matLock)
                {
                    _currentMat = frame;
                }
                LastAcquisitionSuccess = (frame != null && !frame.IsEmpty);
                AcquisitionCompletedSignal.Set();
                RaiseFrameCompleted(frame, LastAcquisitionSuccess, false);
            }
            catch (Exception)
            {
                LastAcquisitionSuccess = false;
                AcquisitionCompletedSignal.Set();
                RaiseFrameCompleted(null, false, false);
            }
        }

        /// <summary>统一抛帧出口</summary>
        private void RaiseFrameCompleted(Mat frame, bool success, bool isLive)
        {
            if (success) MarkHealthy();
            MonitorCameraFrameCompletedHandler handler = FrameCompletedEvent;
            if (handler != null)
            {
                MonitorCameraFrameCompletedEventArgs args = new MonitorCameraFrameCompletedEventArgs();
                args.Frame = frame;
                args.Phase = _currentPhase;
                args.Success = success;
                args.IsLive = isLive;
                args.Timestamp = DateTime.Now;
                handler(this, args);
            }
        }

        /// <summary>记录一次有效采集时间（供健康评估使用）</summary>
        private void MarkHealthy()
        {
            lock (_stateLock) { _lastHealthyUtc = DateTime.Now; }
        }

        /// <summary>健康巡检线程体</summary>
        private void SupervisionLoop()
        {
            while (_supervisionRunning)
            {
                EvaluateHealth();
                Thread.Sleep(_checkIntervalMs);
            }
        }

        /// <summary>
        /// 评估相机健康度：连接态 + 近期有效采集。
        /// 状态或工作标志变化时经 StatusChanged 通知订阅方。
        /// </summary>
        private void EvaluateHealth()
        {
            MonitorSupervisionState newState;
            bool isWorking;
            string message;

            if (ConnectionState != MonitorCameraConnectionState.Connected)
            {
                newState = MonitorSupervisionState.Error;
                isWorking = false;
                message = "监控相机未连接";
            }
            else if (IsMonitorCameraWorking)
            {
                newState = MonitorSupervisionState.Healthy;
                isWorking = true;
                message = "监控相机工作正常";
            }
            else
            {
                newState = MonitorSupervisionState.Degraded;
                isWorking = false;
                message = "监控相机已连接但近期无有效采集";
            }

            MonitorSupervisionState old = SupervisionState;
            if (newState != old || isWorking != IsMonitorCameraWorking)
                SupervisionTransitionTo(newState, message);
        }

        /// <summary>推进健康监督状态并通知订阅方</summary>
        private void SupervisionTransitionTo(MonitorSupervisionState newState, string reason)
        {
            SupervisionState = newState;
            _statusArgs.State = newState;
            _statusArgs.IsWorking = IsMonitorCameraWorking;
            _statusArgs.Phase = _currentPhase;
            _statusArgs.ConnectionState = ConnectionState;
            _statusArgs.LastHealthyTime = LastHealthyTime;
            _statusArgs.Message = reason;
            _statusArgs.Timestamp = DateTime.Now;
            var handler = StatusChanged;
            if (handler != null) handler(this, _statusArgs);
            GlobalCommData.ShowLog(_tag, string.Format(
                "健康巡检状态 {0} 原因 {1}", newState, reason));
        }

        #endregion
    }
}
