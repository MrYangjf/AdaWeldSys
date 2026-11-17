using AdaWeldSystem.MonitorCam.IMonitorCam;
using AdaWeldSystem.MonitorCam.HikCam;
using AdaWeldSystem.EmguALG.Manager;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.MonitorCam
{
    /// <summary>
    /// 监控相机业务中枢（单例，对外唯一入口）。
    /// 实现层只按契约取帧。
    /// 本类负责编排与帧分发。
    /// 含单次触发采集与健康巡检。
    /// 抽象层契约见 IMonitorCamApi。
    /// 2D 面阵相机，无模式，采集均单次触发。
    /// 健康巡检归本类，不属工作流职责。
    /// </summary>
    public class MonitorCamManager
    {
        #region 常量

        /// <summary>巡检线程退出等待上限（毫秒，巡检节拍 1000ms 留 500ms 余量；超时只记警告）。</summary>
        private const int SupervisionJoinTimeoutMs = 1500;

        #endregion

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

        // 录像目录，创建失败用内存态回退，不覆盖配置
        private string _recordDirFallback;

        // ---- 健康巡检字段（相机自身状态）----
        private MonitorSupervisionState _supervisionState = MonitorSupervisionState.Idle;
        private DateTime _lastHealthyUtc = DateTime.MinValue;
        private volatile bool _supervisionRunning;
        private Thread _supervisionThread;
        private int _checkIntervalMs = 1000;
        private int _healthyValidMs = 5000;

        /// <summary>上次巡检日志的原因（同因不重复打日志）。</summary>
        private string _supervisionLogReason;
        private readonly MonitorSupervisionStatusChangedEventArgs _statusArgs =
            new MonitorSupervisionStatusChangedEventArgs();

        #endregion

        #region 公共变量

        /// <summary>采集完成信号（Workflow 回调等待模式）</summary>
        public readonly AutoResetEvent AcquisitionCompletedSignal = new AutoResetEvent(false);

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
            get { return _api.ConnectionState == MonitorCameraConnectionState.Connected; }
        }

        /// <summary>当前连接状态</summary>
        public MonitorCameraConnectionState ConnectionState
        {
            get { return _api.ConnectionState; }
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

        /// <summary>
        /// 预览帧事件，连续采集中每帧抛出。
        /// 供调试页显示，预览帧不触发分析。
        /// 与工作流分析链路相互独立。
        /// </summary>
        public event Action<Mat> PreviewFrameEvent;

        #endregion

        #region 构造函数

        private MonitorCamManager()
        {
            _config = new MonitorCamConfig();
            _config.LoadFromIni(MonitorCamConfig.DefaultConfigPath);
            EnsureRecordDirectory();
            _api = new HikCam.HikCam();
            _api.PreviewFrame += RelayPreviewFrame;
        }

        /// <summary>确保录像目录可用</summary>
        /// <remarks>启动期调用，失败只留痕不抛出。
        /// 不存在则创建；失败回退 exe 目录 Record。</remarks>
        private void EnsureRecordDirectory()
        {
            string desired = _config != null && !string.IsNullOrWhiteSpace(_config.RecordDir)
                ? _config.RecordDir
                : MonitorCamConfig.DefaultRecordDir;
            try
            {
                Directory.CreateDirectory(desired);
                _recordDirFallback = null;
                return;
            }
            catch (Exception ex)
            {
                string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Record");
                string message;
                try
                {
                    Directory.CreateDirectory(fallback);
                    _recordDirFallback = fallback;
                    message = string.Format("录像目录 {0} 不可用（{1}），已回退到 {2}", desired, ex.Message, fallback);
                }
                catch (Exception ex2)
                {
                    _recordDirFallback = AppDomain.CurrentDomain.BaseDirectory;
                    message = string.Format(
                        "录像目录 {0} 不可用（{1}），回退目录 {2} 也无法创建（{3}），改用程序目录 {4}",
                        desired, ex.Message, fallback, ex2.Message, _recordDirFallback);
                }
                // 只在日志留痕，不弹窗骚扰；回退目录仅内存生效，不写 INI
                GlobalCommData.ShowLog(_tag, message, MessageLevel.Warning);
            }
        }

        /// <summary>把预览帧透传给各订阅方，每人独立副本</summary>
        /// <remarks>订阅方拥有并释放副本，分发后释放原始帧；
        /// 禁止多方共享同一 Mat，跨线程释放会踩内存。</remarks>
        /// <param name="frame">预览帧（实现层建出的独立 Mat，本方法分发后即释放）</param>
        private void RelayPreviewFrame(Mat frame)
        {
            try
            {
                Action<Mat> handler = PreviewFrameEvent;
                if (handler == null) return;
                // 标定十字就地标注，订阅方拿成品帧
                try { AnnotateSpot(frame); } catch { }   // 标注失败不阻断预览
                foreach (Delegate singleDelegate in handler.GetInvocationList())
                {
                    // 显式强转以满足 IDE0220，类型固定必成立
                    Action<Mat> single = (Action<Mat>)singleDelegate;
                    Mat copy;
                    try
                    {
                        copy = frame.Clone();
                    }
                    catch { continue; }
                    try
                    {
                        single(copy);
                    }
                    catch
                    {
                        // 订阅方同步异常时收回副本；正常返回即所有权已移交订阅方
                        try { copy.Dispose(); } catch { }   // 释放路径：失败不影响功能
                    }
                }
            }
            finally
            {
                try { frame.Dispose(); } catch { }   // 释放路径：失败不影响功能
            }
        }

        #endregion

        #region 公共函数

        /// <summary>录像保存目录（取配置 RecordDir；创建失败时返回内存回退目录；配置缺失回退 D:\WeldRecord）</summary>
        public string RecordDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_recordDirFallback))
                    return _recordDirFallback;
                if (_config != null && !string.IsNullOrWhiteSpace(_config.RecordDir))
                    return _config.RecordDir;
                return MonitorCamConfig.DefaultRecordDir;
            }
        }

        /// <summary>是否正在连续采集</summary>
        public bool IsCapturing
        {
            get { return _api.IsGrabbing; }
        }

        /// <summary>是否正在录像</summary>
        public bool IsRecording
        {
            get { return _api.IsRecording; }
        }

        /// <summary>
        /// 打开相机连接
        /// </summary>
        /// <param name="ip">相机 IP</param>
        /// <param name="port">相机端口</param>
        /// <returns>已连接或本次连接成功返回 true</returns>
        public bool OpenSensor(string ip, string port)
        {
            if (ConnectionState == MonitorCameraConnectionState.Connected)
                return true;
            if (_config == null) _config = new MonitorCamConfig();
            _config.IpAddress = ip;
            _config.Port = port;
            return _api.Connect(ip, port);
        }

        /// <summary>关闭相机连接并释放 SDK 资源（断开前先停健康巡检）</summary>
        public void CloseSensor()
        {
            StopSupervision();
            _api.Disconnect();
        }

        /// <summary>扫描（枚举）当前可用相机设备，返回含 IP 的设备列表</summary>
        /// <remarks>供 UI 选择。</remarks>
        /// <returns>发现的设备列表；无设备或扫描失败返回空列表</returns>
        public List<MonitorCamDeviceInfo> ScanDevices()
        {
            return _api.ScanDevices();
        }

        /// <summary>把当前硬件配置（含连接 IP）落盘到 INI</summary>
        public void SaveConfig()
        {
            if (_config == null) return;
            _config.SaveConfig();
        }

        /// <summary>开始连续采集（对齐官方 demo 开始采集，连续模式；帧经 PreviewFrameEvent </summary>
        /// <remarks>逐帧抛出）</remarks>
        /// <returns>是否成功开始采集</returns>
        public bool StartCapture()
        {
            if (ConnectionState != MonitorCameraConnectionState.Connected) return false;
            return _api.StartCapture();
        }

        /// <summary>停止连续采集（录像中先停止录像）</summary>
        public void StopCapture()
        {
            _api.StopCapture();
        }

        /// <summary>开始录像：文件落入配置的录像目录下按日期</summary>
        /// <remarks>用方给定（建议时间戳命名）。 须处于连续采集中，否则失败。 yyyyMMdd）分的子目录，文件名由调</remarks>
        /// <param name="fileName">录像文件名（如 MonitorCam_20260929_160000.avi）</param>
        /// <returns>是否成功开始录像</returns>
        public bool StartRecord(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            // 录像按 yyyyMMdd 子目录分日期存放
            string dir = Path.Combine(RecordDirectory, DateTime.Now.ToString("yyyyMMdd"));
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, string.Format("创建录像目录失败 {0} 原因 {1}", dir, ex.Message));
                return false;
            }
            return _api.StartRecord(Path.Combine(dir, fileName));
        }

        /// <summary>停止录像</summary>
        public void StopRecord()
        {
            _api.StopRecord();
        }

        /// <summary>软触发单帧，供送丝距离测试等标定入口</summary>
        /// <remarks>先停采集，再切触发模式软触发取帧；
        /// 图像由调用方显示。</remarks>
        /// <returns>触发取得的图像帧（独立副本）；失败返回 null</returns>
        public Mat TriggerSingleShot()
        {
            if (ConnectionState != MonitorCameraConnectionState.Connected) return null;
            if (_api.IsGrabbing) StopCapture();
            return _api.TriggerSingleShot();
        }

        /// <summary>送丝测试取帧，返回已画十字的成品帧</summary>
        /// <param name="frame">成品帧（独立副本，调用方负责释放）；失败返回 null</param>
        /// <param name="failureMessage">失败提示（成功时为空串）</param>
        /// <returns>取帧成功返回 true</returns>
        public bool TryAcquireCalibFrame(out Mat frame, out string failureMessage)
        {
            frame = null;
            failureMessage = string.Empty;
            if (ConnectionState != MonitorCameraConnectionState.Connected)
            {
                failureMessage = "请先连接相机！";
                return false;
            }
            try
            {
                Mat raw = TriggerSingleShot();
                if (raw == null || raw.IsEmpty)
                {
                    failureMessage = "软触发取帧失败，请查看日志！";
                    return false;
                }
                try { AnnotateSpot(raw); } catch { }   // 标注失败不阻断出图
                frame = raw;
                return true;
            }
            catch (Exception ex)
            {
                failureMessage = "软触发取帧失败，请查看日志！";
                GlobalCommData.ShowLog(_tag, string.Format("软触发取帧异常 原因 {0}", ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>失败原因成品文本，流程与界面只读</summary>
        /// <param name="context">失败语境（连接 / 采集 / 分析）</param>
        /// <returns>非空的原因描述</returns>
        public string DescribeLastError(MonitorCamErrorContext context)
        {
            if (context == MonitorCamErrorContext.Analyze)
                return "分析未完成（算法未返回有效结果）";
            if (context == MonitorCamErrorContext.Capture
                && ConnectionState != MonitorCameraConnectionState.Connected)
                return "相机未连接（采集未发生）";
            return _api.DescribeLastError(context);
        }

        /// <summary>设置曝光时间（微秒）</summary>
        /// <param name="valueUs">曝光时间（微秒）</param>
        /// <returns>是否设置成功</returns>
        public bool SetExposureTime(float valueUs)
        {
            return _api.SetExposureTime(valueUs);
        }

        /// <summary>设置增益（dB）</summary>
        /// <param name="valueDb">增益（dB）</param>
        /// <returns>是否设置成功</returns>
        public bool SetGain(float valueDb)
        {
            return _api.SetGain(valueDb);
        }

        /// <summary>读取当前曝光时间（微秒）；失败返回 -1</summary>
        /// <returns>曝光时间（微秒）；失败返回 -1</returns>
        public float GetExposureTime()
        {
            return _api.GetExposureTime();
        }

        /// <summary>读取当前增益（dB）；失败返回 -1</summary>
        /// <returns>增益（dB）；失败返回 -1</returns>
        public float GetGain()
        {
            return _api.GetGain();
        }

        /// <summary>
        /// 设置当前检测阶段（影响帧事件携带的阶段语义）
        /// </summary>
        /// <param name="phase">目标阶段</param>
        public void SetPhase(MonitorCameraPhase phase)
        {
            _currentPhase = phase;
        }

        /// <summary>触发一次采集（2D 面阵相机唯一采集方式）。 监控相机</summary>
        /// <remarks>无 LIVE/PIL 模式，本方法仅启动单次异步取帧任务。</remarks>
        public void SensorRunContinue()
        {
            if (ConnectionState != MonitorCameraConnectionState.Connected)
                return;
            new Task(AcquireOnce).Start();
        }

        /// <summary>停止采集</summary>
        public void SensorStop()
        {
            // 任务自行结束，此处仅占位兼容调用方
        }

        /// <summary>启动健康巡检</summary>
        public void StartSupervision()
        {
            if (_supervisionRunning) return;
            _supervisionRunning = true;
            _lastHealthyUtc = DateTime.MinValue;
            SupervisionTransitionTo(MonitorSupervisionState.Checking, "监控相机健康巡检启动");
            _supervisionThread = new Thread(SupervisionLoop)
            {
                Name = "MonitorCamSupervision",
                IsBackground = true
            };
            _supervisionThread.Start();
        }

        /// <summary>停止健康巡检，并等待旧巡检线程退出。</summary>
        /// <remarks>先存旧句柄再 Join。超时记警告并留句柄。</remarks>
        public void StopSupervision()
        {
            _supervisionRunning = false;

            Thread old = _supervisionThread;
            _supervisionThread = null;
            if (old != null && old.IsAlive)
            {
                if (old.Join(SupervisionJoinTimeoutMs))
                {
                    old = null;
                }
                else
                {
                    GlobalCommData.ShowLog(_tag,
                        "监控相机健康巡检线程未在 " + SupervisionJoinTimeoutMs + "ms 内退出，保留句柄待复查",
                        MessageLevel.Warning);
                }
            }
            else
            {
                old = null;
            }

            // 未退净时保留句柄，供下次启动前复查；已退净不回填
            if (old != null) _supervisionThread = old;

            SupervisionTransitionTo(MonitorSupervisionState.Idle, "监控相机健康巡检停止");
        }

        /// <summary>释放相机资源</summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopSupervision();
            try { _api.Dispose(); } catch { }   // 释放路径：失败不影响功能
        }

        #endregion

        #region 私有函数

        /// <summary>按标定配置在帧上画光斑十字</summary>
        /// <param name="frame">待标注帧（就地标注）</param>
        private void AnnotateSpot(Mat frame)
        {
            MonitorCamConfig cfg = _config;
            if (cfg == null || cfg.SpotX <= 0 || cfg.SpotY <= 0) return;
            AlgorithmManager.Instance.DrawSpotMark(frame, cfg.SpotX, cfg.SpotY);
        }

        /// <summary>单次采集（异步任务体）</summary>
        private void AcquireOnce()
        {
            try
            {
                Mat frame = _api.CaptureSingleFrame();
                lock (_matLock)
                {
                    _currentMat = frame;
                }
                LastAcquisitionSuccess = (frame != null && !frame.IsEmpty);
                AcquisitionCompletedSignal.Set();
                RaiseFrameCompleted(frame, LastAcquisitionSuccess);
            }
            catch (Exception)
            {
                LastAcquisitionSuccess = false;
                AcquisitionCompletedSignal.Set();
                RaiseFrameCompleted(null, false);
            }
        }

        /// <summary>统一抛帧出口</summary>
        /// <param name="success">是否成功。</param>
        private void RaiseFrameCompleted(Mat frame, bool success)
        {
            // 记录一次有效采集时间（供健康评估使用）
            if (success)
            {
                lock (_stateLock) { _lastHealthyUtc = DateTime.Now; }
            }
            MonitorCameraFrameCompletedHandler handler = FrameCompletedEvent;
            if (handler != null)
            {
                MonitorCameraFrameCompletedEventArgs args = new MonitorCameraFrameCompletedEventArgs
                {
                    Frame = frame,
                    Phase = _currentPhase,
                    Success = success,
                    Timestamp = DateTime.Now
                };
                handler(this, args);
            }
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

        /// <summary>评估相机健康度：连接态 + 近期有效采集。 状态或工</summary>
        /// <remarks>作标志变化时经 StatusChanged 通知订阅方。</remarks>
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
        /// <remarks>状态与原因没变化时不重复打日志。
        /// 退出时 ConnectOff 与 Dispose 各停一次。</remarks>
        private void SupervisionTransitionTo(MonitorSupervisionState newState, string reason)
        {
            bool sameAsLastLog = newState == SupervisionState && reason == _supervisionLogReason;
            _supervisionLogReason = reason;
            SupervisionState = newState;
            _statusArgs.State = newState;
            _statusArgs.IsWorking = IsMonitorCameraWorking;
            _statusArgs.Phase = _currentPhase;
            _statusArgs.ConnectionState = ConnectionState;
            _statusArgs.LastHealthyTime = LastHealthyTime;
            _statusArgs.Message = reason;
            _statusArgs.Timestamp = DateTime.Now;
            StatusChanged?.Invoke(this, _statusArgs);
            if (sameAsLastLog) return;
            GlobalCommData.ShowLog(_tag, string.Format(
                "健康巡检状态 {0} 原因 {1}", newState, reason));
        }

        #endregion
    }
}
