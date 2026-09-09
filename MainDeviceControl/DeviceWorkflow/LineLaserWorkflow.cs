using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.WeldParamControl;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>线激光工作流（单例）。</summary>
    /// <remarks>两层状态：连接态 State 与焊接过程态 WeldStatus；焊缝特征由线激光 Manager 直接产出，不再经算法层中转上报。</remarks>
    public class LineLaserWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepPreWork = 10;
        private const int StepPreWorkOn = 20;
        private const int StepPreWorkStable = 30;
        private const int StepPreWorkWaitWork = 31;
        private const int StepWorking = 100;
        private const int StepStopping = 700;

        // 单步超时阈值（秒）
        private const int DataStableTimeoutSeconds = 10;
        private const int WaitWorkingTimeoutSeconds = 30;
        private const int FirstResultTimeoutSeconds = 5;

        private const int DataStableFrameCount = 5;

        #endregion

        #region 私有变量

        /// <summary>公共方法互斥锁（ADR-023 ④-3：禁止 lock(this)，用私有锁对象）。</summary>
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
        private volatile bool _adjustmentPending;
        private volatile bool _lastResultValid;
        private double _measuredFps;
        private int _consecutiveSuccessCount;
        private int _adjustTriggerSuccessCount = 1;
        private bool _lastAnalysisSuccess;
        private bool _lastAdjustSuccess;

        // 失败收尾记录（原基类 FailReason / FailStep 已删除，下沉为子类私有字段）
        private string _failReason = "";
        private int _failStep;

        #endregion

        #region 单例

        private static readonly Lazy<LineLaserWorkflow> _lazyInstance =
            new Lazy<LineLaserWorkflow>(() => new LineLaserWorkflow());

        /// <summary>线激光工作流单例。</summary>
        public static LineLaserWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        /// <summary>对外名片名（日志与事件标识）。</summary>
        public override string StateName { get { return Tag; } }

        /// <summary>PreWork 是否完成（主设备轮询判断，完成后可进入 Working）。</summary>
        public bool IsPreWorkDone { get { return _preWorkDone; } }

        /// <summary>数据是否稳定有效（主设备轮询判断，稳定后可进入 Working）。</summary>
        public bool IsDataStable { get { return _dataStable; } }

        /// <summary>子设备配置文件名（不含扩展名），落在 Config/XML/ 下。</summary>
        protected override string ConfigFileName { get { return "LineLaserWorkflow"; } }

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
                    LineLaserManager.Instance.SelectIntelligentLaser();
                }
                LineLaserCameraBase camera = LineLaserManager.Instance.Active;
                if (camera == null)
                {
                    Log("线激光相机未选择，初始化失败", MessageLevel.Error);
                    return false;
                }

                // 统一初始化三步：connect → readConfiguration → verifyHardwareStatus（复位职责归 ResetProcess）
                // 相机实例一律经 LineLaserManager 代理取用，工作流不持有厂商实现类型
                return RunInitializeSteps(
                    connect: () => LineLaserManager.Instance.Connect(),
                    readConfiguration: () => true,
                    verifyHardwareStatus: () =>
                    {
                        if (!camera.IsConnected) return false;
                        LineLaserManager.Instance.ConfigureDefaultCommPeriod();
                        return true;
                    });
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
            if (!RunStepAction(connect, "连接设备", "连接失败")) return false;
            Log("初始化步骤 2/3 读取配置", MessageLevel.Info);
            if (!RunStepAction(readConfiguration, "读取配置", "配置读取失败")) return false;
            Log("初始化步骤 3/3 状态信号校验", MessageLevel.Info);
            if (!RunStepAction(verifyHardwareStatus, "状态信号校验", "状态信号异常")) return false;
            return true;
        }

        /// <summary>执行单个初始化步骤并统一处理异常与失败日志。</summary>
        /// <remarks>方法名避让基类 RunStep 属性（CS0102，见 lessons/StepMemberNameCollision）。</remarks>
        /// <param name="step">步骤执行体，null 视为跳过</param>
        /// <param name="stepName">步骤名（日志用）</param>
        /// <param name="failureReason">失败原因（日志用）</param>
        /// <returns>步骤成功或跳过返回 true</returns>
        private bool RunStepAction(Func<bool> step, string stepName, string failureReason)
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

        /// <summary>判断当前步停留是否超过指定秒数。</summary>
        /// <param name="seconds">超时阈值（秒）</param>
        /// <returns>超过阈值返回 true</returns>
        private bool IsTimeout(int seconds)
        {
            return StepWorkTime.TotalSeconds > seconds;
        }

        /// <summary>绑定相机。</summary>
        /// <remarks>取当前激活相机并订阅轮廓数据事件，Start 时调用一次。</remarks>
        private void BindCamera()
        {
            SubscribeCameraEvents();
        }

        /// <summary>解绑相机。</summary>
        /// <remarks>退订事件、安全关外设、复位运行标志；离开运行态时调用。</remarks>
        private void ReleaseCameraBinding()
        {
            UnsubscribeCameraEvents();
            SafeShutdownPeripherals();
            _isWorkflowActive = false;
            LineLaserManager.Instance.SetWorking(false);
        }

        /// <summary>执行步 10：PreWork 清理。</summary>
        /// <returns>清理完成返回 true</returns>
        private bool DoPreWork()
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
            Log("PreWork 清理完成，进入开传感器与激光", MessageLevel.Info);
            return true;
        }

        /// <summary>执行步 20：开传感器与激光。</summary>
        /// <returns>动作完成返回 true</returns>
        private bool DoPreWorkOn()
        {
            if (LineLaserManager.Instance.IsVirtual)
            {
                LineLaserManager.Instance.StartAcquisition();
                _dataStable = true;
                Log("虚拟相机 PreWork 数据稳定默认通过", MessageLevel.Info);
                return true;
            }
            if (!LineLaserManager.Instance.IsCameraOn()) LineLaserManager.Instance.SetSensor(true);
            if (!LineLaserManager.Instance.IsLaserOn()) LineLaserManager.Instance.SetLaser(true);
            // 补启动连续采集：此前工作流从未调用，相机连上也不出图，PreWork 必然等待数据稳定超时
            LineLaserManager.Instance.StartAcquisition();
            _startingValidFrameCount = 0;
            Log("PreWork 激光与传感器已开启并启动采集，等待数据稳定", MessageLevel.Info);
            return true;
        }

        /// <summary>执行步 30：等待轮廓数据稳定（_dataStable 由相机回调置位）。</summary>
        /// <returns>数据稳定返回 true</returns>
        private bool DoPreWorkStable()
        {
            if (_dataStable)
            {
                Log("PreWork 数据已稳定，等待主设备通知进入 Working", MessageLevel.Info);
                return true;
            }
            if (!IsTimeout(DataStableTimeoutSeconds)) return false;
            HandleFailure("PreWork 阶段等待数据稳定超时 10秒");
            ResetWorkTime();
            return false;
        }

        /// <summary>执行步 31：等待主设备通知进入 Working。</summary>
        /// <returns>收到通知（或模拟自驱动）返回 true</returns>
        private bool DoPreWorkWaitWork()
        {
            if (DeviceControlWork.Instance.IsSimulationEnabled)
            {
                SetWeldStatus(SubDeviceWeldStatus.Working, "模拟模式自驱动进入 Working");
                _dataStable = false;
                return false;
            }
            if (!IsTimeout(WaitWorkingTimeoutSeconds)) return false;
            Fail("数据稳定后等待 Working 超时 30秒 主设备未调用 BeginWorking");
            return false;
        }

        /// <summary>执行步 100：焊接工作。</summary>
        /// <returns>行程完成转 700 返回 true，否则停留在 Working 继续</returns>
        private bool DoWorking()
        {
            if (IsWeldLengthReached())
            {
                StopByWeldLengthReached();
                return WeldStatus != SubDeviceWeldStatus.Working;
            }

            if (_adjustmentPending)
            {
                _adjustmentPending = false;
                _consecutiveSuccessCount = 0;
                RunAutoAdjust();
                return false;
            }

            if (LineLaserManager.Instance.IsVirtual)
            {
                _lastResultValid = true;
                return false;
            }

            if (_lastResultValid)
            {
                _consecutiveFailureCount = 0;
                return false;
            }

            // 有效结果缺失需持续满一个超时窗口才计一次失败，避免每拍累加瞬间打满计数
            if (!IsTimeout(FirstResultTimeoutSeconds)) return false;
            HandleFailure("线激光识别失败 ParseRes 非 1");
            ResetWorkTime();
            return false;
        }

        /// <summary>执行步 700：停止清料，关闭激光与传感器。</summary>
        /// <returns>清理完成返回 true</returns>
        private bool DoStopping()
        {
            Log("Stopping 关闭激光与传感器", MessageLevel.Info);
            SafeShutdownPeripherals();
            return true;
        }

        /// <summary>执行步 800：成功收尾回待机。</summary>
        /// <returns>回到待机返回 true</returns>
        private bool DoFinishOk()
        {
            if (WeldStatus == SubDeviceWeldStatus.Stopping)
            {
                SetWeldStatus(SubDeviceWeldStatus.Standby, "Stopping 完成，回到待机");
                return true;
            }
            return true;
        }

        /// <summary>执行步 900：失败收尾（记录故障快照并回待机）。</summary>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, _failReason);
            IsAlarm = true;
            Log(string.Format("工作流异常终止 原因 {0}", _failReason), MessageLevel.Error);
            return true;
        }

        private void SafeShutdownPeripherals()
        {
            // 停采集 → 关激光 → 关传感器，由 LineLaserManager 统一代理
            LineLaserManager.Instance.ShutdownPeripherals();
        }

        private void SubscribeCameraEvents()
        {
            // 业务处理已上提 LineLaserManager，工作流订阅其转发的结果事件（ADR-039）
            LineLaserManager.Instance.ResultReady += ManagerResultReady;
            _adjustmentPending = false;
            _lastResultValid = false;
            _consecutiveSuccessCount = 0;
        }

        private void UnsubscribeCameraEvents()
        {
            LineLaserManager.Instance.ResultReady -= ManagerResultReady;
        }

        private void ManagerResultReady(object sender, LineLaserResultEventArgs e)
        {
            _lastResultValid = e.Result.Valid;
            _measuredFps = e.MeasuredFps;

            if (WeldStatus == SubDeviceWeldStatus.PreWork)
            {
                if (e.Result.Valid)
                {
                    _startingValidFrameCount++;
                    if (_startingValidFrameCount >= DataStableFrameCount)
                        _dataStable = true;
                }
                else
                    _startingValidFrameCount = 0;
            }

            if (e.Result.Valid)
            {
                _consecutiveSuccessCount++;
                if (_consecutiveSuccessCount >= _adjustTriggerSuccessCount && !_adjustmentPending)
                {
                    // 焊缝特征由线激光 Manager 直接产出（ADR-039），不再经算法 Pipeline 中转上报
                    _lastAnalysisSuccess = true;
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
                // 焊缝特征由线激光 Manager 直接产出，故此处无需传参，由工艺工作流统一执行计算与下发
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
                Fail(string.Format("{0} 连续失败{1}次，超过最大允许次数{2}次",
                    failureSource, _consecutiveFailureCount, _maxConsecutiveFailures));
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
                    Log("连接完成", MessageLevel.Info);
                }
                else
                {
                    MarkConnectFailed("线激光连接失败");
                    SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "线激光连接失败");
                    Log("连接失败", MessageLevel.Info);
                }
            }
            catch (Exception ex)
            {
                MarkConnectFailed("线激光连接异常 " + ex.Message);
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
                LineLaserManager.Instance.Disconnect();
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
        /// <remarks>主设备轮询到后调用 BeginWorking() 进入 Working。</remarks>
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
                LineLaserManager.Instance.SelectVirtual();
            else
                LineLaserManager.Instance.SelectIntelligentLaser();

            _isWorkflowActive = true;
            LineLaserManager.Instance.SetWorking(true);
            _measuredFps = 0.0;

            BindCamera();
        }

        /// <summary>手动停止工作流。</summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                if (WeldStatus != SubDeviceWeldStatus.PreWork && WeldStatus != SubDeviceWeldStatus.Working
                    && WeldStatus != SubDeviceWeldStatus.Stopping)
                {
                    Log(string.Format("Stop 忽略 当前状态 {0} 不在进程中", WeldStatus), MessageLevel.Warning);
                    return;
                }
                SetWeldStatus(SubDeviceWeldStatus.ManualStopped, "用户手动停止");
            }
        }

        /// <summary>进入 Working 阶段。</summary>
        /// <remarks>主设备调用；数据稳定后开始焊接工作。</remarks>
        public void BeginWorking()
        {
            lock (_syncRoot)
            {
                if (WeldStatus != SubDeviceWeldStatus.PreWork)
                {
                    Log(string.Format("BeginWorking 忽略 当前状态 {0} 不是 PreWork", WeldStatus), MessageLevel.Warning);
                    return;
                }
                if (!_preWorkDone)
                {
                    Log("BeginWorking 忽略 PreWork 尚未完成", MessageLevel.Warning);
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

        #region 全阻塞流程

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询，等待靠「外部线程重入 + IsTimeout 判超时」。</remarks>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            switch (RunStep)
            {
                case StepPreWork:
                    if (DoPreWork()) AdvanceStep(StepPreWorkOn);
                    break;
                case StepPreWorkOn:
                    if (DoPreWorkOn()) AdvanceStep(StepPreWorkStable);
                    break;
                case StepPreWorkStable:
                    if (DoPreWorkStable()) AdvanceStep(StepPreWorkWaitWork);
                    break;
                case StepPreWorkWaitWork:
                    if (DoPreWorkWaitWork()) AdvanceStep(StepWorking);
                    break;
                case StepWorking:
                    if (DoWorking()) AdvanceStep(StepStopping);
                    break;
                case StepStopping:
                    if (DoStopping()) AdvanceStep(StepFinishOk);
                    break;
                case StepFinishOk:
                    if (DoFinishOk()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                default:
                    // StepIdle 与未登记步号：空转等待，靠焊接过程态切换把执行步复位到阶段入口
                    break;
            }
        }

        /// <summary>流程复位：关外设回待机（线性阻塞链）。</summary>
        /// <returns>未连接返回 false，复位成功返回 true</returns>
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

        /// <summary>状态清理：回未复位态并归零标志。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _consecutiveFailureCount = 0;
            _dataStable = false;
            _preWorkDone = false;
            AdvanceStep(StepIdle);
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入步骤时长导致异常超时。</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：同步等待至连接完成。上电初始化阶段使用。</summary>
        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            if (State == SubDeviceState.Connected) return true;
            bool ok;
            try
            {
                Log("初始化连接开始", MessageLevel.Info);
                ok = InitializeInternal();
            }
            catch (Exception ex)
            {
                Log("初始化连接异常 " + ex.Message, MessageLevel.Error);
                ok = false;
            }
            if (!ok)
            {
                SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "线激光初始化连接失败");
                return false;
            }
            SetState(SubDeviceState.Connected, "线激光初始化连接完成");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化连接完成，等待复位");
            return true;
        }

        /// <summary>设备断开（阻塞）：内部执行释放动作，调用方阻塞至完成。</summary>
        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            try
            {
                Log("初始化断开开始", MessageLevel.Info);
                LineLaserManager.Instance.Disconnect();
                SafeShutdownPeripherals();
                ReleaseCameraBinding();
                SetState(SubDeviceState.Disconnected, "线激光初始化断开");
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开完成");
                return true;
            }
            catch (Exception ex)
            {
                Log("初始化断开异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>设备日志：输出线激光差异化日志（结果 / 帧率 / 稳定帧计数）。</summary>
        public override void SubDeviceLog()
        {
            Log(string.Format("线激光 连接态 {0} 焊接过程态 {1} 步骤 {2} 有效结果 {3} 实测帧率 {4:F1} 稳定帧 {5}",
                State, WeldStatus, WorkStepName, _lastResultValid, _measuredFps, _startingValidFrameCount));
        }

        /// <summary>加载本子设备的配置。</summary>
        public override void LoadConfig()
        {
            TimeOutSeconds = ReadInt("TimeOutSeconds", 30);
            _maxConsecutiveFailures = ReadInt("MaxConsecutiveFailures", 5);
            _maxWeldLength = ReadDouble("MaxWeldLength", 1000.0);
            _adjustTriggerSuccessCount = ReadInt("AdjustTriggerSuccessCount", 1);
        }

        /// <summary>保存本子设备的配置。</summary>
        public override void SaveConfig()
        {
            Config.SetElementValue("TimeOutSeconds", TimeOutSeconds.ToString());
            Config.SetElementValue("MaxConsecutiveFailures", _maxConsecutiveFailures.ToString());
            Config.SetElementValue("MaxWeldLength", _maxWeldLength.ToString());
            Config.SetElementValue("AdjustTriggerSuccessCount", _adjustTriggerSuccessCount.ToString());
            Config.SaveXdocument();
        }

        /// <summary>焊接工作状态切换管控。</summary>
        public override void WeldStatusTrans()
        {
            if (WeldStatus == SubDeviceWeldStatus.NoReset) return;
            if (WeldStatus == SubDeviceWeldStatus.Stopping)
            {
                AdvanceStep(StepStopping);
                return;
            }
            if (WeldStatus == SubDeviceWeldStatus.ManualStopped
                || WeldStatus == SubDeviceWeldStatus.ErrorAborted)
            {
                AdvanceStep(StepIdle);
            }
        }

        #endregion

        #region 非阻塞流程

        /// <summary>开启连接（非阻塞）：自起后台线程执行连接，动作即退出释放。</summary>
        /// <returns>受理返回 true，连接进行中被拒绝返回 false</returns>
        public override bool ConnectOn()
        {
            if (State == SubDeviceState.Connected) return true;
            if (_isConnecting) return false;
            _isConnecting = true;
            BeginConnectAttempt();
            var t = new Thread(ConnectWorker)
            {
                Name = "LineLaserConnect",
                IsBackground = true
            };
            t.Start();
            return true;
        }

        /// <summary>关闭连接（非阻塞）：自起后台线程执行断开，动作即退出释放。</summary>
        /// <returns>受理返回 true，断开进行中被拒绝返回 false</returns>
        public override bool ConnectOff()
        {
            if (State == SubDeviceState.Disconnected) return true;
            if (_isConnecting) return false;
            _isConnecting = true;
            var t = new Thread(DisconnectWorker)
            {
                Name = "LineLaserDisconnect",
                IsBackground = true
            };
            t.Start();
            return true;
        }

        #endregion

        #region 焊接过程态变更钩子（状态切换时把执行步复位到该阶段入口）

        /// <summary>焊接过程态切换钩子。</summary>
        /// <remarks>把执行步（内部游标）复位到该阶段入口步；离开运行态时解绑相机。</remarks>
        /// <param name="oldStatus">切换前焊接过程态</param>
        /// <param name="newStatus">切换后焊接过程态</param>
        protected override void OnWeldStatusChanged(SubDeviceWeldStatus oldStatus, SubDeviceWeldStatus newStatus)
        {
            base.OnWeldStatusChanged(oldStatus, newStatus);
            switch (newStatus)
            {
                case SubDeviceWeldStatus.PreWork:
                    AdvanceStep(StepPreWork);
                    break;
                case SubDeviceWeldStatus.Working:
                    AdvanceStep(StepWorking);
                    break;
                case SubDeviceWeldStatus.Stopping:
                    AdvanceStep(StepStopping);
                    break;
                default:
                    AdvanceStep(StepIdle);
                    ReleaseCameraBinding();
                    break;
            }
        }

        /// <summary>释放资源：退订全局事件并释放相机。</summary>
        protected override void DisposeManaged()
        {
            GlobalCommData.CommunicationCommandReceived -= OnCommunicationCommandReceived;
            try
            {
                UnsubscribeCameraEvents();
                // 相机实例由 LineLaserManager 持有并统一释放，工作流不再自行 Dispose
                LineLaserManager.Instance.SetWorking(false);
            }
            catch (Exception ex)
            {
                Log("释放相机异常 " + ex.Message, MessageLevel.Warning);
            }
            _isWorkflowActive = false;
        }

        #endregion

        #region 可观测性

        /// <summary>执行步号转中文名。</summary>
        /// <remarks>新增步必须登记，否则该步不可观测。</remarks>
        /// <param name="step">执行步号</param>
        /// <returns>步号对应中文名；未登记的步返回「未登记步(N)」</returns>
        protected override string GetStepName(int step)
        {
            switch (step)
            {
                case StepIdle: return "待机";
                case StepPreWork: return "焊接前-清理状态与外设";
                case StepPreWorkOn: return "焊接前-开传感器与激光";
                case StepPreWorkStable: return "焊接前-等待数据稳定";
                case StepPreWorkWaitWork: return "焊接前-等待进入Working";
                case StepWorking: return "工作中-焊接与自适应调整";
                case StepStopping: return "停止中-关闭外设";
                case StepFinishOk: return "成功收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        #endregion
    }
}
