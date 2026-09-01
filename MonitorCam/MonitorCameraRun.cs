using AdaWeldSystem.Comm;
using AdaWeldSystem.MonitorCam.Api;
using AdaWeldSystem.FileOperate;
using Emgu.CV;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AdaWeldSystem.MonitorCam
{
    /// <summary>
    /// 监控相机运行控制（单例）
    /// 设计上解耦具体 SDK：底层通过 IMonitorCameraApi 抽象，当前实现为 HikvisionCameraApi（海康面阵相机，MvCameraControl.Net SDK）。
    /// 参考 SmartRay CameraRun 的 Live/PIL 模式与采集完成信号机制（ADR-005 回调等待）。
    /// 注意：不修改任何既有相机模块，监控相机独立运行。
    /// 相机自身的健康巡检（连接态 + 近期有效采集）归属本类，不属工作流职责（ADR-042 R1）。
    /// </summary>
    public class MonitorCameraRun : IDisposable
    {
        public delegate void MonitorCameraFrameCompletedHandler(object sender, MonitorCameraFrameCompletedEventArgs e);
        public static event MonitorCameraFrameCompletedHandler FrameCompletedEvent;

        /// <summary>健康监督状态变更事件（相机自身状态，供 UI 订阅）</summary>
        public event EventHandler<MonitorSupervisionStatusChangedEventArgs> StatusChanged;

        public bool IsLiveMode = false;

        /// <summary>
        /// 采集完成信号（Workflow 回调等待模式，参考 ADR-005 / CameraRun）
        /// </summary>
        public readonly AutoResetEvent AcquisitionCompletedSignal = new AutoResetEvent(false);

        private bool _lastAcquisitionSuccess = false;
        public bool LastAcquisitionSuccess
        {
            get { return _lastAcquisitionSuccess; }
            private set { _lastAcquisitionSuccess = value; }
        }

        private readonly IMonitorCameraApi _api;
        private static MonitorCameraRun _instance = null;
        private static readonly object _instLock = new object();
        private MonitorCameraPhase _currentPhase = MonitorCameraPhase.PreWeldAlignment;
        private Mat _currentMat;
        private readonly object _matLock = new object();
        private MonitorCameraConfig _config;
        private bool _isDisposed = false;

        // ---- 健康巡检字段（相机自身状态，ADR-042 R1 由工作流下沉至此）----

        private readonly object _stateLock = new object();
        private MonitorSupervisionState _supervisionState = MonitorSupervisionState.Idle;
        private DateTime _lastHealthyUtc = DateTime.MinValue;
        private volatile bool _supervisionRunning;
        private Thread _supervisionThread;
        private int _checkIntervalMs = 1000;
        private int _healthyValidMs = 5000;
        private readonly MonitorSupervisionStatusChangedEventArgs _statusArgs =
            new MonitorSupervisionStatusChangedEventArgs();

        public Mat CurrentMat
        {
            get { lock (_matLock) { return _currentMat; } }
        }

        public bool IsRun
        {
            get { return _api != null && _api.ConnectionState == MonitorCameraConnectionState.Connected; }
        }

        public MonitorCameraConnectionState ConnectionState
        {
            get { return _api != null ? _api.ConnectionState : MonitorCameraConnectionState.Disconnected; }
        }

        public MonitorCameraConfig Config
        {
            get { return _config; }
            set { _config = value; }
        }

        #region 健康巡检

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

        #endregion

        public static MonitorCameraRun Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instLock)
                    {
                        if (_instance == null)
                            _instance = new MonitorCameraRun();
                    }
                }
                return _instance;
            }
        }

        private MonitorCameraRun()
        {
            _config = new MonitorCameraConfig();
            _config.LoadFromIni(MonitorCameraConfig.DefaultConfigPath);
            _api = new HikvisionCameraApi();
            _api.FrameReceived += OnApiFrameReceived;
        }

        public void OpenSensor(string ip, string port)
        {
            if (ConnectionState == MonitorCameraConnectionState.Connected)
                return;
            if (_config == null) _config = new MonitorCameraConfig();
            _config.IpAddress = ip;
            _config.Port = port;
            _api.Connect(ip, port);
        }

        /// <summary>
        /// 设置当前检测阶段
        /// </summary>
        public void SetPhase(MonitorCameraPhase phase)
        {
            _currentPhase = phase;
        }

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
                _lastAcquisitionSuccess = (frame != null && !frame.IsEmpty);
                AcquisitionCompletedSignal.Set();
                RaiseFrameCompleted(_lastAcquisitionSuccess);
            }
            catch (Exception)
            {
                _lastAcquisitionSuccess = false;
                AcquisitionCompletedSignal.Set();
                RaiseFrameCompleted(false);
            }
        }

        public void SensorStop()
        {
            IsLiveMode = false;
            _api.StopAcquisition();
        }

        private void OnApiFrameReceived(object sender, MonitorFrameEventArgs e)
        {
            lock (_matLock)
            {
                _currentMat = e.Frame;
            }
            MarkHealthy();
            if (FrameCompletedEvent != null)
            {
                var args = new MonitorCameraFrameCompletedEventArgs();
                args.Frame = e.Frame;
                args.Phase = _currentPhase;
                args.Success = true;
                args.IsLive = true;
                args.Timestamp = DateTime.Now;
                FrameCompletedEvent(this, args);
            }
        }

        private void RaiseFrameCompleted(bool success)
        {
            if (success) MarkHealthy();
            if (FrameCompletedEvent != null)
            {
                var args = new MonitorCameraFrameCompletedEventArgs();
                Mat snap;
                lock (_matLock) { snap = _currentMat; }
                args.Frame = snap;
                args.Phase = _currentPhase;
                args.Success = success;
                args.IsLive = false;
                args.Timestamp = DateTime.Now;
                FrameCompletedEvent(this, args);
            }
        }

        #region 健康巡检实现

        /// <summary>记录一次有效采集时间（供健康评估使用）</summary>
        private void MarkHealthy()
        {
            lock (_stateLock) { _lastHealthyUtc = DateTime.Now; }
        }

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
            GlobalCommData.ShowLog("监控相机", string.Format(
                "健康巡检状态 {0} 原因 {1}", newState, reason));
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopSupervision();
            try { _api.FrameReceived -= OnApiFrameReceived; } catch { }
            try { _api.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// 监控相机配置（IP/端口/曝光/尺寸等硬件参数）。
    /// 算法阈值（对中容差 / 质量合格分）已迁至 MonitorAlgorithmManager（单一可配置算法，持久化到 MonitorAlgorithm.ini），
    /// 实现「算法配置管理与算法实现分离」，镜像 EmguALG 的 AlgorithmManager 范式。
    /// 持久化到 Config/INI/MonitorCamera.ini。
    /// </summary>
    public class MonitorCameraConfig
    {
        public static string DefaultConfigPath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "MonitorCamera.ini");
            }
        }

        public string IpAddress { get; set; }
        public string Port { get; set; }

        public int ExposureMs { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        public MonitorCameraConfig()
        {
            IpAddress = "192.168.1.100";
            Port = "5000";
            ExposureMs = 10;
            ImageWidth = 640;
            ImageHeight = 480;
        }

        public void SaveToIni(string filePath)
        {
            var ini = new INIFile(filePath);
            ini.WriteString("Camera", "IpAddress", IpAddress);
            ini.WriteString("Camera", "Port", Port);
            ini.WriteInt("Camera", "ExposureMs", ExposureMs);
            ini.WriteInt("Camera", "ImageWidth", ImageWidth);
            ini.WriteInt("Camera", "ImageHeight", ImageHeight);
            ini.SaveToFile();
        }

        public void LoadFromIni(string filePath)
        {
            if (!File.Exists(filePath))
            {
                SaveToIni(filePath);
                return;
            }
            var ini = new INIFile(filePath);
            IpAddress = ini.ReadString("Camera", "IpAddress", IpAddress);
            Port = ini.ReadString("Camera", "Port", Port);
            ExposureMs = ini.ReadInt("Camera", "ExposureMs", ExposureMs);
            ImageWidth = ini.ReadInt("Camera", "ImageWidth", ImageWidth);
            ImageHeight = ini.ReadInt("Camera", "ImageHeight", ImageHeight);
        }
    }

    /// <summary>
    /// 监控相机帧采集完成事件参数（供 UI 实时预览订阅）
    /// </summary>
    public class MonitorCameraFrameCompletedEventArgs : EventArgs
    {
        public Mat Frame { get; set; }
        public MonitorCameraPhase Phase { get; set; }
        public bool Success { get; set; }
        public bool IsLive { get; set; }
        public DateTime Timestamp { get; set; }
    }
}