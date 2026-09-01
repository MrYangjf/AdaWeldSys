using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.LineLaserCamApi;
using AdaWeldSystem.EmguALG.EmguConfiger;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.ProductFileManager;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>线激光工作流状态枚举</summary>
    public enum LineLaserWorkflowState
    {
        Uninitialized,   // 未初始化
        Initializing,    // 初始化中
        Standby,         // 待机（传感器/激光均关闭，等待启动指令）
        PreWork,         // 启动前准备（清理状态/计数/残留数据）
        Starting,        // 启动中（开激光+开传感器+等待数据稳定）
        Working,         // 工作中（回传ScottPlot轮廓+分析结果+数据记录）
        Stopping,        // 停止中（关激光+关传感器+清理，转回Standby）
        ErrorAborted,    // 异常终止
        ManualStopped    // 手动停止
    }

    /// <summary>线激光工作流（单例，步骤驱动 ADR-047）。</summary>
    /// <remarks>三层状态：设备四态 State（映射派生） / 阶段态 Step（对外名片，主设备经 BeginStarting、BeginWorking 驱动） / 执行步 WorkStep（内部游标，经 GoStep 推进）。 执行步段位：10 PreWork清理 → 11 等主设备通知Starting → 20 开传感器激光 → 30 等数据稳定 → 31 等主设备通知Working → 100 焊接工作 → 700 停止清料 → 800 成功收尾 / 900 失败收尾。</remarks>
    public class LineLaserWorkflow : DeviceWorkflowBase<LineLaserWorkflowState>
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepPreWork = 10;
        private const int StepPreWorkWait = 11;
        private const int StepStartingOn = 20;
        private const int StepStartingStable = 30;
        private const int StepStartingWaitWork = 31;
        private const int StepWorking = 100;
        private const int StepStopping = 700;

        // 超时阈值（毫秒）
        private const int PreWorkWaitTimeoutMs = 30000;
        private const int DataStableTimeoutMs = 10000;
        private const int WaitWorkingTimeoutMs = 30000;
        private const int FirstResultTimeoutMs = 5000;

        private const int DataStableFrameCount = 5;

        #endregion

        #region 私有变量

        private readonly string _tag = "线激光工作流";

        /// <summary>公共方法互斥锁（ADR-038 ④-3：禁止 lock(this)，用私有锁对象）。</summary>
        private readonly object _syncRoot = new object();

        private volatile bool _isManualMode;
        private Thread _initThread;
        private int _consecutiveFailureCount;
        private int _maxConsecutiveFailures = 5;
        private double _startRobotX;
        private double _maxWeldLength = 1000.0;
        private volatile bool _isWorkflowActive = false;

        // 分步协同字段（主设备驱动子设备分步启动）
        private volatile bool _preWorkDone;
        private volatile bool _dataStable;
        private int _startingValidFrameCount;

        // Running 态字段
        private IntelligentLaserCameraRun _ilCamera;
        private volatile bool _adjustmentPending;
        private volatile bool _lastResultValid;
        private double _measuredFps;
        private int _consecutiveSuccessCount;
        private int _adjustTriggerSuccessCount = 1;
        private bool _lastAnalysisSuccess;
        private bool _lastAdjustSuccess;

        #endregion

        #region 单例

        private static readonly Lazy<LineLaserWorkflow> _lazyInstance =
            new Lazy<LineLaserWorkflow>(() => new LineLaserWorkflow());

        public static LineLaserWorkflow Instance => _lazyInstance.Value;

        #endregion

        #region 公共变量

        public override string StateName => _tag;

        public bool IsManualMode => _isManualMode;
        public bool IsInWorkflow => _isWorkflowActive;
        public bool CanAcceptExternalCommand => !_isManualMode;

        public int MaxConsecutiveFailures
        {
            get { return _maxConsecutiveFailures; }
            set { _maxConsecutiveFailures = value > 0 ? value : 1; }
        }

        public double MaxWeldLength
        {
            get { return _maxWeldLength; }
            set { _maxWeldLength = value > 0 ? value : 1.0; }
        }

        public double RealTimeAnalysisFps => _measuredFps;

        /// <summary>PreWork 是否完成（主设备轮询判断，完成后可进入 Starting）。</summary>
        public bool IsPreWorkDone => _preWorkDone;

        /// <summary>仅进程中阶段态才跑业务；待机/报警/手动停止时空转并持续刷新步时间戳，</summary>
        /// <remarks>避免长时间停机后恢复瞬间误判超时。</remarks>
        protected override bool IsRunLoopActive
        {
            get
            {
                return _step == LineLaserWorkflowState.PreWork
                    || _step == LineLaserWorkflowState.Starting
                    || _step == LineLaserWorkflowState.Working
                    || _step == LineLaserWorkflowState.Stopping;
            }
        }

        /// <summary>数据是否稳定有效（主设备轮询判断，稳定后可进入 Working）。</summary>
        public bool IsDataStable => _dataStable;

        #endregion

        #region 构造函数

        private LineLaserWorkflow() : base(LineLaserWorkflowState.Uninitialized)
        {
            GlobalCommData.CommunicationCommandReceived += OnCommunicationCommandReceived;
        }

        #endregion

        #region 私有函数

        /// <summary>后台初始化任务。</summary>
        /// <remarks>一次性线程，非流程监听线程；只在初始化期间存在。</remarks>
        private void InitializeWorker()
        {
            try
            {
                if (MainDeviceWorkflow.Instance.IsSimulationEnabled
                    && !MainDeviceWorkflow.Instance.ShouldUseVirtualCamera)
                {
                    CameraSelector.SelectIntelligentLaser();
                }
                var camera = CameraSelector.Active;
                if (camera is IntelligentLaserCameraRun)
                {
                    var ilCamera = (IntelligentLaserCameraRun)camera;
                    // 统一初始化四步：connect → readConfiguration → resetDevice → verifyHardwareStatus
                    bool ok = RunInitializeSteps(
                        connect: () => ilCamera.IsConnected || ilCamera.ConnectManual(IntelligentLaserCameraRun.LoadSavedIp()),
                        readConfiguration: () => true,
                        resetDevice: () =>
                        {
                            if (ilCamera.IsLaserOn()) ilCamera.SetLaser(false);
                            if (ilCamera.IsCameraOn()) ilCamera.SetSensor(false);
                            return true;
                        },
                        verifyHardwareStatus: () =>
                        {
                            if (!ilCamera.IsConnected) return false;
                            ilCamera.ConfigureDefaultCommPeriod();
                            return true;
                        });
                    SetStep(ok ? LineLaserWorkflowState.Standby : LineLaserWorkflowState.ErrorAborted,
                        ok ? "英莱相机初始化完成" : "英莱相机初始化失败");
                    if (!ok) IsAlarm = true;
                    else StartRunLoop();
                    return;
                }

                if (CameraSelector.IsVirtual)
                {
                    bool ok = RunInitializeSteps(
                        connect: () => true, readConfiguration: () => true,
                        resetDevice: () => true, verifyHardwareStatus: () => true);
                    SetStep(ok ? LineLaserWorkflowState.Standby : LineLaserWorkflowState.ErrorAborted,
                        ok ? "虚拟相机初始化完成" : "虚拟相机初始化失败");
                    if (!ok) IsAlarm = true;
                    else StartRunLoop();
                    return;
                }

                SetStep(LineLaserWorkflowState.ErrorAborted, "线激光相机未正确选择/未连接，初始化失败");
                IsAlarm = true;
            }
            catch (Exception ex)
            {
                SetStep(LineLaserWorkflowState.ErrorAborted, "相机初始化失败 原因是 " + ex.Message);
                IsAlarm = true;
            }
        }

        /// <summary>初始化四步模板（吸收原 DeviceInitializationStateMachineBase 的固化流转）。</summary>
        /// <param name="connect">第 1 步：连接设备</param>
        /// <param name="readConfiguration">第 2 步：读取配置</param>
        /// <param name="resetDevice">第 3 步：复位设备</param>
        /// <param name="verifyHardwareStatus">第 4 步：校验硬件状态</param>
        /// <returns>四步全部成功返回 true；任一步失败即中断并返回 false</returns>
        private bool RunInitializeSteps(
            Func<bool> connect, Func<bool> readConfiguration, Func<bool> resetDevice, Func<bool> verifyHardwareStatus)
        {
            GlobalCommData.ShowLog(_tag, "初始化步骤 1/4 连接设备", MessageLevel.Info);
            if (!RunStep(connect, "连接设备", "连接失败")) return false;
            GlobalCommData.ShowLog(_tag, "初始化步骤 2/4 读取配置", MessageLevel.Info);
            if (!RunStep(readConfiguration, "读取配置", "配置读取失败")) return false;
            GlobalCommData.ShowLog(_tag, "初始化步骤 3/4 复位设备", MessageLevel.Info);
            if (!RunStep(resetDevice, "复位设备", "复位失败")) return false;
            GlobalCommData.ShowLog(_tag, "初始化步骤 4/4 状态信号校验", MessageLevel.Info);
            if (!RunStep(verifyHardwareStatus, "状态信号校验", "状态信号异常")) return false;
            return true;
        }

        private bool RunStep(Func<bool> step, string stepName, string failureReason)
        {
            if (step == null)
            {
                GlobalCommData.ShowLog(_tag, string.Format("[{0}] 跳过（未提供）", stepName), MessageLevel.Info);
                return true;
            }
            try
            {
                if (step()) return true;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, string.Format("[{0}] 异常 原因 {1}", stepName, ex.Message), MessageLevel.Error);
            }
            GlobalCommData.ShowLog(_tag, string.Format("[{0}] 失败 原因是 {1}", stepName, failureReason), MessageLevel.Error);
            return false;
        }

        /// <summary>绑定相机。</summary>
        /// <remarks>取当前激活相机并订阅轮廓数据事件，Start 时调用一次。</remarks>
        private void BindCamera()
        {
            _ilCamera = CameraSelector.Active as IntelligentLaserCameraRun;
            SubscribeCameraEvents();
        }

        /// <summary>解绑相机。</summary>
        /// <remarks>退订事件、安全关外设、复位运行标志；离开运行态时调用。</remarks>
        private void ReleaseCameraBinding()
        {
            UnsubscribeCameraEvents();
            SafeShutdownPeripherals();
            _ilCamera = null;
            _isWorkflowActive = false;
            CameraSelector.SetWorking(false);
        }

        /// <summary>执行步 10：PreWork。</summary>
        /// <remarks>清理状态、计数与外设；完成后置 _preWorkDone 交主设备轮询。</remarks>
        private void DoPreWork()
        {
            ResetFailureCounters();
            _consecutiveSuccessCount = 0;
            _adjustmentPending = false;
            _lastResultValid = false;
            _lastAnalysisSuccess = false;
            _lastAdjustSuccess = false;
            _measuredFps = 0.0;
            _dataStable = false;
            _startingValidFrameCount = 0;

            SafeShutdownPeripherals();

            _preWorkDone = true;
            GlobalCommData.ShowLog(_tag, "PreWork 完成，等待主设备通知进入 Starting", MessageLevel.Info);
            GoStep(StepPreWorkWait);
        }

        /// <summary>执行步 11：等待主设备 BeginStarting 通知（外部驱动，非本流程自决）。</summary>
        /// <remarks>模拟模式自驱动；超时走失败收尾，不留在原步死等。</remarks>
        private void DoPreWorkWait()
        {
            if (MainDeviceWorkflow.Instance.IsSimulationEnabled)
            {
                SetStep(LineLaserWorkflowState.Starting, "模拟模式自驱动进入 Starting");
                return;
            }
            if (!IsStepTimeout(PreWorkWaitTimeoutMs)) return;
            FailFlow("PreWork 完成后等待 Starting 超时 30秒 主设备未调用 BeginStarting");
        }

        /// <summary>执行步 20：开传感器与激光。</summary>
        /// <remarks>动作失败即返回，步号不变，下拍重入重试。</remarks>
        private void DoStartingOn()
        {
            if (_ilCamera == null)
            {
                _dataStable = true;
                GlobalCommData.ShowLog(_tag, "虚拟相机 Starting 数据稳定默认通过", MessageLevel.Info);
                GoStep(StepStartingWaitWork);
                return;
            }
            if (!_ilCamera.IsCameraOn()) _ilCamera.SetSensor(true);
            if (!_ilCamera.IsLaserOn()) _ilCamera.SetLaser(true);
            _startingValidFrameCount = 0;
            GlobalCommData.ShowLog(_tag, "Starting 激光与传感器已开启，等待数据稳定", MessageLevel.Info);
            GoStep(StepStartingStable);
        }

        /// <summary>执行步 30：等待轮廓数据稳定（_dataStable 由相机回调置位）。</summary>
        /// <remarks>超时记一次失败并刷新时间戳重试，达到连续失败上限才转失败收尾（失败重试零代码）。</remarks>
        private void DoStartingStable()
        {
            if (_dataStable)
            {
                GlobalCommData.ShowLog(_tag, "Starting 数据已稳定，等待主设备通知进入 Working", MessageLevel.Info);
                GoStep(StepStartingWaitWork);
                return;
            }
            if (!IsStepTimeout(DataStableTimeoutMs)) return;
            HandleFailure("Starting 阶段等待数据稳定超时 10秒");
            ResetStepTime();
        }

        /// <summary>执行步 31：等待 BeginWorking。</summary>
        /// <remarks>由主设备外部驱动；模拟模式下自驱动。</remarks>
        private void DoStartingWaitWork()
        {
            if (MainDeviceWorkflow.Instance.IsSimulationEnabled)
            {
                SetStep(LineLaserWorkflowState.Working, "模拟模式自驱动进入 Working");
                _dataStable = false;
                return;
            }
            if (!IsStepTimeout(WaitWorkingTimeoutMs)) return;
            FailFlow("数据稳定后等待 Working 超时 30秒 主设备未调用 BeginWorking");
        }

        /// <summary>执行步 100：焊接工作。</summary>
        /// <remarks>行程完成转 700；有待调整则触发自适应调整；长时间无有效识别结果记一次失败并重置计时重试。</remarks>
        private void DoWorking()
        {
            if (IsWeldLengthReached())
            {
                StopByWeldLengthReached();
                return;
            }

            if (_adjustmentPending)
            {
                _adjustmentPending = false;
                _consecutiveSuccessCount = 0;
                if (!_isManualMode) RunAutoAdjust();
                return;
            }

            if (_ilCamera == null)
            {
                _lastResultValid = true;
                return;
            }

            if (_lastResultValid)
            {
                _consecutiveFailureCount = 0;
                return;
            }

            // 有效结果缺失需持续满一个超时窗口才计一次失败，避免每拍累加瞬间打满计数
            if (!IsStepTimeout(FirstResultTimeoutMs)) return;
            HandleFailure("线激光识别失败 ParseRes 非 1");
            ResetStepTime();
        }

        /// <summary>执行步 700：停止清料，关闭激光与传感器。</summary>
        private void DoStopping()
        {
            GlobalCommData.ShowLog(_tag, "Stopping 关闭激光与传感器", MessageLevel.Info);
            SafeShutdownPeripherals();
            GoStep(StepFinishOk);
        }

        /// <summary>执行步 800：成功收尾，回到 Standby（阶段态切换由 OnStepChanged 复位执行步）。</summary>
        private void DoFinishOk()
        {
            if (_step == LineLaserWorkflowState.Stopping)
                SetStep(LineLaserWorkflowState.Standby, "Stopping 完成，回到待机");
            else
                GoStep(StepIdle);
        }

        /// <summary>执行步 900：失败收尾三步走 —— 置 Alarm（经 SetStep 映射，禁手工赋值 State）</summary>
        /// <remarks>→ 记日志与故障记录 → 回待机执行步。</remarks>
        private void DoFinishFail()
        {
            SetStep(LineLaserWorkflowState.ErrorAborted, FailReason);
            IsAlarm = true;
            FaultRecoveryManager.Instance.RecordFault(_tag, new FaultRecord
            {
                Time = DateTime.Now,
                Device = _tag,
                State = MapToDeviceState(_step),
                Category = FaultCategory.Process,
                ErrorCode = "LineLaserStepFail",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", FailStep, FailReason),
                AutoRecovered = false,
                RecoveryAction = "回到待机，等待人工复位"
            });
            GlobalCommData.ShowLog(_tag, string.Format("工作流异常终止 原因 {0}", FailReason), MessageLevel.Error);
            GoStep(StepIdle);
        }

        private void SafeShutdownPeripherals()
        {
            if (_ilCamera == null) return;
            try { if (_ilCamera.IsLaserOn()) _ilCamera.SetLaser(false); } catch { }
            try { if (_ilCamera.IsCameraOn()) _ilCamera.SetSensor(false); } catch { }
        }

        private void SubscribeCameraEvents()
        {
            if (_ilCamera == null) return;
            _ilCamera.ContourDataReady += IlCamera_ContourDataReady;
            _adjustmentPending = false;
            _lastResultValid = false;
            _consecutiveSuccessCount = 0;
        }

        private void UnsubscribeCameraEvents()
        {
            if (_ilCamera != null)
                _ilCamera.ContourDataReady -= IlCamera_ContourDataReady;
        }

        private void IlCamera_ContourDataReady(object sender, ContourDataEventArgs e)
        {
            _lastResultValid = e.HasValidResult;
            _measuredFps = e.MeasuredFps;

            if (_step == LineLaserWorkflowState.Starting)
            {
                if (e.HasValidResult)
                {
                    _startingValidFrameCount++;
                    if (_startingValidFrameCount >= DataStableFrameCount)
                        _dataStable = true;
                }
                else
                    _startingValidFrameCount = 0;
            }

            if (e.HasValidResult)
            {
                _consecutiveSuccessCount++;
                if (_consecutiveSuccessCount >= _adjustTriggerSuccessCount && !_adjustmentPending)
                {
                    try
                    {
                        var ir = e.Inspect;
                        var sf = new SeamFeatureResult();
                        sf.CenterY = ir.FeatureY;
                        sf.CenterX = ir.FeatureZ;
                        sf.SeamWidth = (ir.Width0 + ir.Width1) * 0.5;
                        sf.SeamArea = ir.Area;
                        double robotX = GetCurrentRobotX();
                        PipelineManager.Instance.ReportExternalResult(sf, robotX, null);
                        _lastAnalysisSuccess = true;
                    }
                    catch
                    {
                        _lastAnalysisSuccess = false;
                    }
                    _adjustmentPending = true;
                }
            }
            else
            {
                _consecutiveSuccessCount = 0;
                _lastAnalysisSuccess = false;
            }
        }

        private void RunAutoAdjust()
        {
            // 硬件 CAMBOX 跟踪运行期间，跳过软件离散调整
            if (MotionControlWorkflow.Instance.IsRunning)
            {
                _lastAdjustSuccess = true;
                return;
            }

            try
            {
                // ADR-041：焊接工艺计算与下发收敛到 WeldParamControlWorkflow。
                // 焊缝特征与机器人 X 由 WeldProcess 在 PipelineManager.AlgorithmCompleted 中暂存，
                // 故此处无需传参，由工艺工作流统一执行计算与下发
                _lastAdjustSuccess = WeldParamControlWorkflow.Instance.ComputeAndOutput();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, string.Format("调用智能焊接调整算法失败 原因是 {0}", ex.Message), MessageLevel.Error);
                _lastAdjustSuccess = false;
            }
        }

        private void ResetFailureCounters()
        {
            _consecutiveFailureCount = 0;
            _startRobotX = GetCurrentRobotX();
        }

        private void OnCommunicationCommandReceived(object sender, string message)
        {
            if (!CanAcceptExternalCommand) return;
            GlobalCommData.ShowCommunicationLog(_tag, message);
        }

        /// <summary>记录一次失败。</summary>
        /// <remarks>连续失败达到上限才转失败收尾；未达上限留在原步重试。</remarks>
        /// <param name="failureSource">失败来源描述（供日志与故障快照定位）</param>
        private void HandleFailure(string failureSource)
        {
            _consecutiveFailureCount++;
            if (_consecutiveFailureCount >= _maxConsecutiveFailures)
            {
                FailFlow(string.Format("{0} 连续失败{1}次，超过最大允许次数{2}次",
                    failureSource, _consecutiveFailureCount, _maxConsecutiveFailures));
            }
        }

        private bool IsWeldLengthReached()
        {
            double traveled = Math.Abs(GetCurrentRobotX() - _startRobotX);
            return traveled >= _maxWeldLength;
        }

        private void StopByWeldLengthReached()
        {
            if (_step != LineLaserWorkflowState.Working) return;
            double traveled = Math.Abs(GetCurrentRobotX() - _startRobotX);
            SetStep(LineLaserWorkflowState.Stopping, string.Format(
                "焊缝行程完成 X行程 {0}mm 最大 {1}mm 进入 Stopping",
                traveled.ToString("F2"), _maxWeldLength.ToString("F2")));
        }

        private double GetCurrentRobotX()
        {
            if (MainDeviceWorkflow.Instance.IsSimulationEnabled)
                return MainDeviceWorkflow.Instance.GetEffectiveRobotX();
            try
            {
                return WeldProcess.Instance.CurrentRobotX;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, "读取机器人X坐标异常 " + ex.Message, MessageLevel.Warning);
                return 0.0;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>同步初始化（阻塞等待初始化完成后返回）。</summary>
        /// <returns>初始化后处于 Connect 返回 true</returns>
        public bool InitializeSync()
        {
            if (_step != LineLaserWorkflowState.Uninitialized)
                return State == DeviceState.Connect;

            SetStep(LineLaserWorkflowState.Initializing, "线激光初始化开始");
            InitializeWorker();
            return State == DeviceState.Connect;
        }

        /// <summary>启动工作流：Standby → PreWork。PreWork 完成后置 _preWorkDone=true，</summary>
        /// <remarks>主设备轮询到后调用 BeginStarting() 进入 Starting。</remarks>
        public void Start()
        {
            lock (_syncRoot)
            {
                if (_step != LineLaserWorkflowState.Standby)
                    throw new InvalidOperationException(string.Format(
                        "Start 要求当前状态为 Standby，实际状态为 {0}", _step));

                ResetFailureCounters();
                _preWorkDone = false;
                _dataStable = false;
                _startingValidFrameCount = 0;
                SetStep(LineLaserWorkflowState.PreWork, "工作流启动，进入 PreWork");
            }

            if (MainDeviceWorkflow.Instance.IsSimulationEnabled && MainDeviceWorkflow.Instance.ShouldUseVirtualCamera)
                CameraSelector.SelectVirtual();
            else
                CameraSelector.SelectIntelligentLaser();

            _isWorkflowActive = true;
            CameraSelector.SetWorking(true);
            _measuredFps = 0.0;
            // 设备态由 SetStep 的 MapToDeviceState 统一映射（PreWork → Work），此处不手工赋值

            BindCamera();
            StartRunLoop();
        }

        /// <summary>手动停止工作流。</summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                if (_step != LineLaserWorkflowState.PreWork && _step != LineLaserWorkflowState.Starting
                    && _step != LineLaserWorkflowState.Working && _step != LineLaserWorkflowState.Stopping)
                    throw new InvalidOperationException(string.Format(
                        "Stop 无法从状态 {0} 执行手动停止", _step));

                SetStep(LineLaserWorkflowState.ManualStopped, "用户手动停止");
            }
        }

        /// <summary>进入 Starting 阶段。</summary>
        /// <remarks>主设备调用；开激光、开传感器、等待数据稳定。</remarks>
        public void BeginStarting()
        {
            lock (_syncRoot)
            {
                if (_step != LineLaserWorkflowState.PreWork)
                {
                    GlobalCommData.ShowLog(_tag,
                        string.Format("BeginStarting 忽略 当前状态 {0} 不是 PreWork", _step), MessageLevel.Warning);
                    return;
                }
                if (!_preWorkDone)
                {
                    GlobalCommData.ShowLog(_tag, "BeginStarting 忽略 PreWork 尚未完成", MessageLevel.Warning);
                    return;
                }
                SetStep(LineLaserWorkflowState.Starting, "主设备通知进入 Starting");
                _dataStable = false;
                _startingValidFrameCount = 0;
            }
        }

        /// <summary>进入 Working 阶段。</summary>
        /// <remarks>主设备调用；数据稳定后开始焊接工作。</remarks>
        public void BeginWorking()
        {
            lock (_syncRoot)
            {
                if (_step != LineLaserWorkflowState.Starting)
                {
                    GlobalCommData.ShowLog(_tag,
                        string.Format("BeginWorking 忽略 当前状态 {0} 不是 Starting", _step), MessageLevel.Warning);
                    return;
                }
                if (!_dataStable)
                {
                    GlobalCommData.ShowLog(_tag, "BeginWorking 忽略 数据尚未稳定", MessageLevel.Warning);
                    return;
                }
                SetStep(LineLaserWorkflowState.Working, "主设备通知进入 Working（焊接开始）");
            }
        }

        /// <summary>流程复位：回到未初始化并重新同步初始化。</summary>
        /// <remarks>由基类公共入口 Reset 调用（ADR-048）；未初始化/初始化中不允许复位。</remarks>
        protected override void ResetFlow()
        {
            lock (_syncRoot)
            {
                if (_step == LineLaserWorkflowState.Uninitialized || _step == LineLaserWorkflowState.Initializing)
                    throw new InvalidOperationException(string.Format("Reset 无法从状态 {0} 执行", _step));
                SetStep(LineLaserWorkflowState.Uninitialized);
                ResetFailureCounters();
            }
            InitializeSync();
        }

        /// <summary>进入手动调试模式（要求当前为 Standby）。</summary>
        public void StartManualMode()
        {
            lock (_syncRoot)
            {
                if (_step != LineLaserWorkflowState.Standby)
                    throw new InvalidOperationException(string.Format(
                        "StartManualMode 要求当前状态为 Standby，实际状态为 {0}", _step));
                _isManualMode = true;
            }
        }

        /// <summary>退出手动调试模式。</summary>
        public void StopManualMode()
        {
            lock (_syncRoot)
            {
                if (!_isManualMode) return;
                _isManualMode = false;
            }
        }

        #endregion

        #region 流程态 → 设备四态映射

        /// <summary>流程态 → 设备四态映射。</summary>
        /// <remarks>未初始化 Uninitialized        → Disconnect 初始化中 Initializing         → Work（初始化进行中，属进程态） 待机 Standby                  → Connect（初始化完成） 进程中 PreWork/Starting/Working/Stopping → Work 报警 ErrorAborted             → Alarm 手动停止 ManualStopped        → Connect（已连接但流程已停，回到待机就绪）</remarks>
        /// <param name="step">阶段态</param>
        /// <returns>该阶段态对应的设备四态</returns>
        protected override DeviceState MapToDeviceState(LineLaserWorkflowState step)
        {
            switch (step)
            {
                case LineLaserWorkflowState.Uninitialized:
                    return DeviceState.Disconnect;
                case LineLaserWorkflowState.Standby:
                case LineLaserWorkflowState.ManualStopped:
                    return DeviceState.Connect;
                case LineLaserWorkflowState.ErrorAborted:
                    return DeviceState.Alarm;
                default:
                    // Initializing / PreWork / Starting / Working / Stopping 均为进程态
                    return DeviceState.Work;
            }
        }

        #endregion

        #region 阶段态变更钩子（阶段态切换时复位执行步到该阶段入口）

        /// <summary>阶段态切换钩子。</summary>
        /// <remarks>把执行步（内部游标）复位到该阶段入口步。</remarks>
        /// <param name="e">阶段态变更事件参数</param>
        protected override void OnStepChanged(WorkflowStepChangedEventArgs<LineLaserWorkflowState> e)
        {
            base.OnStepChanged(e);
            switch (e.NewState)
            {
                case LineLaserWorkflowState.PreWork:
                    GoStep(StepPreWork);
                    break;
                case LineLaserWorkflowState.Starting:
                    GoStep(StepStartingOn);
                    break;
                case LineLaserWorkflowState.Working:
                    GoStep(StepWorking);
                    break;
                case LineLaserWorkflowState.Stopping:
                    GoStep(StepStopping);
                    break;
                default:
                    GoStep(StepIdle);
                    ReleaseCameraBinding();
                    break;
            }
        }

        #endregion

        #region 执行步分派

        /// <summary>单步分派（switch(WorkStep)），由监听线程按 10ms 节拍反复调用。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询，等待靠「节拍重入 + IsStepTimeout 判超时」。</remarks>
        protected override void FlowProcess()
        {
            switch (WorkStep)
            {
                case StepPreWork: DoPreWork(); break;
                case StepPreWorkWait: DoPreWorkWait(); break;
                case StepStartingOn: DoStartingOn(); break;
                case StepStartingStable: DoStartingStable(); break;
                case StepStartingWaitWork: DoStartingWaitWork(); break;
                case StepWorking: DoWorking(); break;
                case StepStopping: DoStopping(); break;
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
                case StepPreWork: return "启动前准备-清理状态与外设";
                case StepPreWorkWait: return "启动前准备-等待进入Starting";
                case StepStartingOn: return "启动中-开传感器与激光";
                case StepStartingStable: return "启动中-等待数据稳定";
                case StepStartingWaitWork: return "启动中-等待进入Working";
                case StepWorking: return "工作中-焊接与自适应调整";
                case StepStopping: return "停止中-关闭外设";
                case StepFinishOk: return "成功收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        #endregion

        #region 抽象生命周期方法实现（设备态映射）

        /// <summary>初始化流程 = connect 线激光（一次性后台线程）。成功转 Connect(Standby)。</summary>
        /// <returns>初始化成功返回 true</returns>
        protected override bool InitializeFlow()
        {
            if (_step != LineLaserWorkflowState.Uninitialized)
                return State == DeviceState.Connect;

            SetStep(LineLaserWorkflowState.Initializing, "线激光初始化开始");
            _initThread = new Thread(InitializeWorker)
            {
                Name = "LineLaserInit",
                IsBackground = true
            };
            _initThread.Start();
            // 异步初始化：返回当前是否已 connect（同步无法确定，交由线程完成时置态）
            return State == DeviceState.Connect;
        }

        /// <summary>手动开 = 连接线激光。</summary>
        protected override void ManualOn()
        {
            if (_step == LineLaserWorkflowState.Uninitialized)
            {
                InitializeFlow();
            }
        }

        /// <summary>手动关 = 断开线激光。</summary>
        protected override void ManualOff()
        {
            SafeShutdownPeripherals();
            SetStep(LineLaserWorkflowState.Uninitialized, "手动断开");
        }

        /// <summary>自动运行 = 阻塞式执行完整工作序列（ADR-048）。</summary>
        /// <remarks>
        /// 序列：就绪复位 → Start → 等 PreWork 完成 → BeginStarting → 等数据稳定 → BeginWorking
        /// → 阻塞等待行程完成回到 Standby 或异常收尾。全部等待经 AutoWait 有界/可中止；
        /// 中止时按当前阶段安全停机。FlowProcess（监听线程）仍是执行器，本序列只做排程。
        /// </remarks>
        protected override void AutoRunFlow()
        {
            // 前置就绪：未初始化先同步初始化；异常/手动停止先复位
            if (_step == LineLaserWorkflowState.Uninitialized)
            {
                InitializeSync();
            }
            if (_step == LineLaserWorkflowState.ErrorAborted
                || _step == LineLaserWorkflowState.ManualStopped)
            {
                ResetFlow();
            }
            if (_step != LineLaserWorkflowState.Standby)
            {
                // 监听线程在 Standby 不活跃（900 收尾步不会被消费），前置失败只记日志
                GlobalCommData.ShowLog(_tag, "自动运行前置状态不满足 当前 " + _step, MessageLevel.Error);
                return;
            }

            Start();
            if (!AutoWait(() => IsPreWorkDone, PreWorkWaitTimeoutMs))
            {
                if (!IsAutoAbortRequested) FailFlow("自动运行等待 PreWork 完成超时");
                SafeAbortStop();
                return;
            }

            BeginStarting();
            if (!AutoWait(() => IsDataStable, DataStableTimeoutMs))
            {
                if (!IsAutoAbortRequested) FailFlow("自动运行等待数据稳定超时");
                SafeAbortStop();
                return;
            }

            BeginWorking();
            // 阻塞等待流程自然收尾（行程完成→Stopping→800→Standby；或失败→ErrorAborted）
            AutoWait(() =>
                _step == LineLaserWorkflowState.Standby
                || _step == LineLaserWorkflowState.ErrorAborted
                || _step == LineLaserWorkflowState.ManualStopped, 0);
            if (IsAutoAbortRequested) SafeAbortStop();
        }

        /// <summary>自动序列中止/失败兜底：按当前阶段安全停机（不在可停阶段则忽略）。</summary>
        private void SafeAbortStop()
        {
            var st = _step;
            if (st == LineLaserWorkflowState.PreWork || st == LineLaserWorkflowState.Starting
                || st == LineLaserWorkflowState.Working || st == LineLaserWorkflowState.Stopping)
            {
                try { Stop(); }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(_tag, "自动流程安全停机异常 " + ex.Message, MessageLevel.Warning);
                }
            }
        }

        #endregion

        #region 监听线程钩子与释放

        /// <summary>释放钩子（基类 Dispose 调用，ADR-034）：退订全局事件并释放相机。</summary>
        protected override void DisposeManaged()
        {
            GlobalCommData.CommunicationCommandReceived -= OnCommunicationCommandReceived;
            try
            {
                UnsubscribeCameraEvents();
                if (_ilCamera != null)
                {
                    _ilCamera.Dispose();
                    _ilCamera = null;
                }
                CameraSelector.SetWorking(false);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, "释放相机异常 " + ex.Message, MessageLevel.Warning);
            }
            _isWorkflowActive = false;
        }

        #endregion
    }
}
