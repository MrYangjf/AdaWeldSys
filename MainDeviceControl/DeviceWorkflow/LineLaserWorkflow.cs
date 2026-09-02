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
    /// <summary>线激光工作流（单例，步骤驱动 ADR-047，新主控设计三态模型）。</summary>
    /// <remarks>
    /// 两层状态：连接态 State(SubDeviceState)，由 ConnectOn/ConnectOff 联动；焊接过程态 WeldStatus(SubDeviceWeldStatus)，由流程方法联动；执行步 WorkStep（内部游标，经 GoStep 推进）。
    /// 执行步段位：10 PreWork清理 → 11 等主设备通知Starting → 20 开传感器激光 → 30 等数据稳定 → 31 等主设备通知Working → 100 焊接工作 → 700 停止清料 → 800 成功收尾 / 900 失败收尾。
    /// </remarks>
    public class LineLaserWorkflow : DeviceWorkflowBase
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

        /// <summary>公共方法互斥锁（ADR-038 ④-3：禁止 lock(this)，用私有锁对象）。</summary>
        private readonly object _syncRoot = new object();

        private int _consecutiveFailureCount;
        private int _maxConsecutiveFailures = 5;
        private double _startRobotX;
        private double _maxWeldLength = 1000.0;
        private volatile bool _isWorkflowActive = false;

        /// <summary>连接/断开线程是否进行中（防重入）。</summary>
        private volatile bool _isConnecting = false;

        // 分步协同字段（主设备驱动子设备分步启动）
        private volatile bool _preWorkDone;
        private volatile bool _dataStable;
        private int _startingValidFrameCount;

        // Working 态字段
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

        public static LineLaserWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        /// <summary>对外名片名（日志与监听线程命名用）。</summary>
        public override string StateName { get { return Tag; } }

        /// <summary>PreWork 是否完成（主设备轮询判断，完成后可进入 Starting）。</summary>
        public bool IsPreWorkDone { get { return _preWorkDone; } }

        /// <summary>仅进程中焊接过程态才跑业务；待机/报警/手动停止时空转并持续刷新步时间戳，</summary>
        /// <remarks>避免长时间停机后恢复瞬间误判超时。</remarks>
        protected override bool IsRunLoopActive
        {
            get
            {
                return WeldStatus == SubDeviceWeldStatus.PreWork
                    || WeldStatus == SubDeviceWeldStatus.Starting
                    || WeldStatus == SubDeviceWeldStatus.Working
                    || WeldStatus == SubDeviceWeldStatus.Stopping;
            }
        }

        /// <summary>数据是否稳定有效（主设备轮询判断，稳定后可进入 Working）。</summary>
        public bool IsDataStable { get { return _dataStable; } }

        #endregion

        #region 构造函数

        private LineLaserWorkflow()
        {
            Tag = "线激光工作流";

            GlobalCommData.CommunicationCommandReceived += OnCommunicationCommandReceived;
        }

        #endregion

        #region 私有函数

        /// <summary>连接初始化三步（connect→readConfiguration→verifyHardwareStatus），连接后台线程体内执行。</summary>
        /// <remarks>阻塞同步执行；只做连接与校验，成功后由调用方置 Connected。复位职责归 ResetProcess。</remarks>
        /// <returns>初始化成功返回 true</returns>
        private bool InitializeInternal()
        {
            try
            {
                if (DeviceControlWork.Instance.IsSimulationEnabled
                    && !DeviceControlWork.Instance.ShouldUseVirtualCamera)
                {
                    CameraSelector.SelectIntelligentLaser();
                }
                var camera = CameraSelector.Active;
                if (camera is IntelligentLaserCameraRun)
                {
                    var ilCamera = (IntelligentLaserCameraRun)camera;
                    // 统一初始化三步：connect → readConfiguration → verifyHardwareStatus（复位职责归 ResetProcess）
                    return RunInitializeSteps(
                        connect: () => ilCamera.IsConnected || ilCamera.ConnectManual(IntelligentLaserCameraRun.LoadSavedIp()),
                        readConfiguration: () => true,
                        verifyHardwareStatus: () =>
                        {
                            if (!ilCamera.IsConnected) return false;
                            ilCamera.ConfigureDefaultCommPeriod();
                            return true;
                        });
                }

                if (CameraSelector.IsVirtual)
                {
                    return RunInitializeSteps(
                        connect: () => true, readConfiguration: () => true,
                        verifyHardwareStatus: () => true);
                }

                Log("线激光相机未正确选择/未连接，初始化失败", MessageLevel.Error);
                return false;
            }
            catch (Exception ex)
            {
                Log("相机初始化失败 原因是 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>初始化三步模板（吸收原 DeviceInitializationStateMachineBase 的固化流转）。</summary>
        /// <param name="connect">第 1 步：连接设备</param>
        /// <param name="readConfiguration">第 2 步：读取配置</param>
        /// <param name="verifyHardwareStatus">第 3 步：校验硬件状态</param>
        /// <returns>三步全部成功返回 true；任一步失败即中断并返回 false</returns>
        private bool RunInitializeSteps(
            Func<bool> connect, Func<bool> readConfiguration, Func<bool> verifyHardwareStatus)
        {
            Log("初始化步骤 1/3 连接设备", MessageLevel.Info);
            if (!RunStep(connect, "连接设备", "连接失败")) return false;
            Log("初始化步骤 2/3 读取配置", MessageLevel.Info);
            if (!RunStep(readConfiguration, "读取配置", "配置读取失败")) return false;
            Log("初始化步骤 3/3 状态信号校验", MessageLevel.Info);
            if (!RunStep(verifyHardwareStatus, "状态信号校验", "状态信号异常")) return false;
            return true;
        }

        private bool RunStep(Func<bool> step, string stepName, string failureReason)
        {
            if (step == null)
            {
                Log(string.Format("[{0}] 跳过（未提供）", stepName), MessageLevel.Info);
                return true;
            }
            try
            {
                if (step()) return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("[{0}] 异常 原因 {1}", stepName, ex.Message), MessageLevel.Error);
            }
            Log(string.Format("[{0}] 失败 原因是 {1}", stepName, failureReason), MessageLevel.Error);
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
            Log("PreWork 完成，等待主设备通知进入 Starting", MessageLevel.Info);
            GoStep(StepPreWorkWait);
        }

        /// <summary>执行步 11：等待主设备 BeginStarting 通知（外部驱动，非本流程自决）。</summary>
        /// <remarks>模拟模式自驱动；超时走失败收尾，不留在原步死等。</remarks>
        private void DoPreWorkWait()
        {
            if (DeviceControlWork.Instance.IsSimulationEnabled)
            {
                SetWeldStatus(SubDeviceWeldStatus.Starting, "模拟模式自驱动进入 Starting");
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
                Log("虚拟相机 Starting 数据稳定默认通过", MessageLevel.Info);
                GoStep(StepStartingWaitWork);
                return;
            }
            if (!_ilCamera.IsCameraOn()) _ilCamera.SetSensor(true);
            if (!_ilCamera.IsLaserOn()) _ilCamera.SetLaser(true);
            _startingValidFrameCount = 0;
            Log("Starting 激光与传感器已开启，等待数据稳定", MessageLevel.Info);
            GoStep(StepStartingStable);
        }

        /// <summary>执行步 30：等待轮廓数据稳定（_dataStable 由相机回调置位）。</summary>
        /// <remarks>超时记一次失败并刷新时间戳重试，达到连续失败上限才转失败收尾（失败重试零代码）。</remarks>
        private void DoStartingStable()
        {
            if (_dataStable)
            {
                Log("Starting 数据已稳定，等待主设备通知进入 Working", MessageLevel.Info);
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
            if (DeviceControlWork.Instance.IsSimulationEnabled)
            {
                SetWeldStatus(SubDeviceWeldStatus.Working, "模拟模式自驱动进入 Working");
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
                RunAutoAdjust();
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
            Log("Stopping 关闭激光与传感器", MessageLevel.Info);
            SafeShutdownPeripherals();
            GoStep(StepFinishOk);
        }

        /// <summary>执行步 800：成功收尾回待机。</summary>
        private void DoFinishOk()
        {
            if (WeldStatus == SubDeviceWeldStatus.Stopping)
                SetWeldStatus(SubDeviceWeldStatus.Standby, "Stopping 完成，回到待机");
            else
                GoStep(StepIdle);
        }

        /// <summary>执行步 900：失败收尾。</summary>
        private void DoFinishFail()
        {
            SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, FailReason);
            IsAlarm = true;
            FaultRecoveryManager.Instance.RecordFault(Tag, new FaultRecord
            {
                Time = DateTime.Now,
                Device = Tag,
                State = WeldStatus,
                Category = FaultCategory.Process,
                ErrorCode = "LineLaserStepFail",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", FailStep, FailReason),
                AutoRecovered = false,
                RecoveryAction = "回到待机，等待人工复位"
            });
            Log(string.Format("工作流异常终止 原因 {0}", FailReason), MessageLevel.Error);
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

            if (WeldStatus == SubDeviceWeldStatus.Starting)
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
                // 焊接工艺计算与下发收敛到 WeldParamControlWorkflow。
                // 焊缝特征与机器人 X 由 WeldProcess 在 PipelineManager.AlgorithmCompleted 中暂存，
                // 故此处无需传参，由工艺工作流统一执行计算与下发
                _lastAdjustSuccess = WeldParamControlWorkflow.Instance.ComputeAndOutput();
            }
            catch (Exception ex)
            {
                Log(string.Format("调用智能焊接调整算法失败 原因是 {0}", ex.Message), MessageLevel.Error);
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
            GlobalCommData.ShowCommunicationLog(Tag, message);
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
            if (WeldStatus != SubDeviceWeldStatus.Working) return;
            double traveled = Math.Abs(GetCurrentRobotX() - _startRobotX);
            SetWeldStatus(SubDeviceWeldStatus.Stopping, string.Format(
                "焊缝行程完成 X行程 {0}mm 最大 {1}mm 进入 Stopping",
                traveled.ToString("F2"), _maxWeldLength.ToString("F2")));
        }

        private double GetCurrentRobotX()
        {
            if (DeviceControlWork.Instance.IsSimulationEnabled)
                return DeviceControlWork.Instance.GetEffectiveRobotX();
            try
            {
                return WeldProcess.Instance.CurrentRobotX;
            }
            catch (Exception ex)
            {
                Log("读取机器人X坐标异常 " + ex.Message, MessageLevel.Warning);
                return 0.0;
            }
        }

        /// <summary>连接后台线程体。</summary>
        private void ConnectWorker()
        {
            try
            {
                Log("连接开始", MessageLevel.Info);
                bool ok = InitializeInternal();
                if (ok)
                {
                    SetState(SubDeviceState.Connected, "线激光连接完成");
                    StartRunLoop();
                    Log("连接完成", MessageLevel.Info);
                }
                else
                {
                    SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "线激光连接失败");
                    Log("连接失败", MessageLevel.Info);
                }
            }
            catch (Exception ex)
            {
                SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "连接异常 " + ex.Message);
                Log("连接异常 " + ex.Message, MessageLevel.Info);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>断开后台线程体：断开相机外设并置 Disconnected。</summary>
        private void DisconnectWorker()
        {
            try
            {
                Log("断开开始", MessageLevel.Info);
                var camera = CameraSelector.Active;
                if (camera is IntelligentLaserCameraRun)
                {
                    var ilCamera = (IntelligentLaserCameraRun)camera;
                    if (ilCamera.IsConnected)
                        ilCamera.DisconnectManual();
                }
                SafeShutdownPeripherals();
                ReleaseCameraBinding();
                SetState(SubDeviceState.Disconnected, "线激光断开");
                Log("断开完成", MessageLevel.Info);
            }
            catch (Exception ex)
            {
                Log("断开异常 " + ex.Message, MessageLevel.Info);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>启动工作流：Standby → PreWork。PreWork 完成后置 _preWorkDone=true，</summary>
        /// <remarks>主设备轮询到后调用 BeginStarting() 进入 Starting。</remarks>
        public void Start()
        {
            lock (_syncRoot)
            {
                if (WeldStatus != SubDeviceWeldStatus.Standby)
                    throw new InvalidOperationException(string.Format(
                        "Start 要求当前焊接过程态为 Standby，实际状态为 {0}", WeldStatus));

                ResetFailureCounters();
                _preWorkDone = false;
                _dataStable = false;
                _startingValidFrameCount = 0;
                SetWeldStatus(SubDeviceWeldStatus.PreWork, "工作流启动，进入 PreWork");
            }

            if (DeviceControlWork.Instance.IsSimulationEnabled && DeviceControlWork.Instance.ShouldUseVirtualCamera)
                CameraSelector.SelectVirtual();
            else
                CameraSelector.SelectIntelligentLaser();

            _isWorkflowActive = true;
            CameraSelector.SetWorking(true);
            _measuredFps = 0.0;

            BindCamera();
            StartRunLoop();
        }

        /// <summary>手动停止工作流。</summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                if (WeldStatus != SubDeviceWeldStatus.PreWork && WeldStatus != SubDeviceWeldStatus.Starting
                    && WeldStatus != SubDeviceWeldStatus.Working && WeldStatus != SubDeviceWeldStatus.Stopping)
                {
                    Log(string.Format("Stop 忽略 当前状态 {0} 不在进程中", WeldStatus), MessageLevel.Warning);
                    return;
                }
                SetWeldStatus(SubDeviceWeldStatus.ManualStopped, "用户手动停止");
            }
        }

        /// <summary>进入 Starting 阶段。</summary>
        /// <remarks>主设备调用；开激光、开传感器、等待数据稳定。</remarks>
        public void BeginStarting()
        {
            lock (_syncRoot)
            {
                if (WeldStatus != SubDeviceWeldStatus.PreWork)
                {
                    Log(string.Format("BeginStarting 忽略 当前状态 {0} 不是 PreWork", WeldStatus), MessageLevel.Warning);
                    return;
                }
                if (!_preWorkDone)
                {
                    Log("BeginStarting 忽略 PreWork 尚未完成", MessageLevel.Warning);
                    return;
                }
                SetWeldStatus(SubDeviceWeldStatus.Starting, "主设备通知进入 Starting");
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
                if (WeldStatus != SubDeviceWeldStatus.Starting)
                {
                    Log(string.Format("BeginWorking 忽略 当前状态 {0} 不是 Starting", WeldStatus), MessageLevel.Warning);
                    return;
                }
                if (!_dataStable)
                {
                    Log("BeginWorking 忽略 数据尚未稳定", MessageLevel.Warning);
                    return;
                }
                SetWeldStatus(SubDeviceWeldStatus.Working, "主设备通知进入 Working（焊接开始）");
            }
        }

        #endregion

        #region 连接契约（ConnectOn/ConnectOff 联动 State）

        /// <summary>开启连接（受理式）。</summary>
        /// <remarks>已连接直接受理返回；连接失败记 Info 反馈并置 ErrorAborted，不置 Alarm（IsAlarm 仅初始化失败时置位）。</remarks>
        /// <returns>受理返回 true，连接进行中被拒绝返回 false</returns>
        public override bool ConnectOn()
        {
            if (State == SubDeviceState.Connected) return true;
            if (_isConnecting) return false;
            _isConnecting = true;
            var t = new Thread(ConnectWorker)
            {
                Name = "LineLaserConnect",
                IsBackground = true
            };
            t.Start();
            return true;
        }

        /// <summary>关闭连接：非阻塞；内部自起后台线程断开并置 Disconnected。</summary>
        public override void ConnectOff()
        {
            if (State == SubDeviceState.Disconnected) return;
            if (_isConnecting) return;
            _isConnecting = true;
            var t = new Thread(DisconnectWorker)
            {
                Name = "LineLaserDisconnect",
                IsBackground = true
            };
            t.Start();
        }

        #endregion

        #region 复位/清除契约（联动 WeldStatus）

        /// <summary>流程复位：关外设回待机。</summary>
        /// <remarks>未连接不可复位返回 false；复位职责（关外设）由本流程单独管控。</remarks>
        /// <returns>受理返回 true</returns>
        public override bool ResetProcess()
        {
            lock (_syncRoot)
            {
                if (State == SubDeviceState.Disconnected)
                {
                    Log("复位拒绝 当前未连接", MessageLevel.Warning);
                    return false;
                }
                SafeShutdownPeripherals();
                ResetFailureCounters();
            }
            SetWeldStatus(SubDeviceWeldStatus.Standby, "流程复位");
            return true;
        }

        /// <summary>清除报警态并回待机。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            ResetWorkStep();
            SetWeldStatus(SubDeviceWeldStatus.Standby, "状态清除");
        }

        /// <summary>复位时间：重置本流程工作时间计时。</summary>
        public override void ResetWorkTime()
        {
            SetWorkTimeStart();
        }

        #endregion

        #region 焊接过程态变更钩子（状态切换时复位执行步到该阶段入口）

        /// <summary>焊接过程态切换钩子。</summary>
        /// <remarks>把执行步（内部游标）复位到该阶段入口步；离开运行态时解绑相机。
        /// 注意：本方法在 SetWeldStatus 同步路径内执行，调用处 SetWeldStatus 后须立即 return。</remarks>
        /// <param name="oldStatus">切换前焊接过程态</param>
        /// <param name="newStatus">切换后焊接过程态</param>
        protected override void OnWeldStatusChanged(SubDeviceWeldStatus oldStatus, SubDeviceWeldStatus newStatus)
        {
            base.OnWeldStatusChanged(oldStatus, newStatus);
            switch (newStatus)
            {
                case SubDeviceWeldStatus.PreWork:
                    GoStep(StepPreWork);
                    break;
                case SubDeviceWeldStatus.Starting:
                    GoStep(StepStartingOn);
                    break;
                case SubDeviceWeldStatus.Working:
                    GoStep(StepWorking);
                    break;
                case SubDeviceWeldStatus.Stopping:
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
        public override void FlowProcess()
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
                    // StepIdle 与未登记步号：空转等待，等待靠焊接过程态切换的 GoStep 复位
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
                Log("释放相机异常 " + ex.Message, MessageLevel.Warning);
            }
            _isWorkflowActive = false;
        }

        #endregion
    }
}
