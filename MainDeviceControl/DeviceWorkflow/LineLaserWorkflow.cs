﻿using System;
using System.Threading;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>线激光工作流</summary>
    /// <remarks>两层状态：State 与 WeldStatus；焊缝特征由 Manager 产出，不经算法层中转。</remarks>
    public class LineLaserWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 / 900 由基类提供）
        private const int StepPreWork = 10;
        private const int StepPreWorkOn = 20;
        private const int StepPreWorkStable = 30;
        private const int StepPreWorkWaitWork = 31;
        private const int StepWorking = 100;
        private const int StepStopping = 700;

        // 单步超时阈值取自线激光配置 ROOT/TIMEOUT
        private const int DataStableFrameCount = 5;

        /// <summary>PreWork 等待轮廓数据稳定的超时秒数（配置项 DATA_STABLE_SECONDS）</summary>
        private int DataStableTimeoutSeconds { get { return LineLaserManager.Instance.Config.DataStableTimeoutSeconds; } }

        /// <summary>等待主设备进入 Working 的超时秒数（配置项 WAIT_WORKING_SECONDS）</summary>
        private int WaitWorkingTimeoutSeconds { get { return LineLaserManager.Instance.Config.WaitWorkingTimeoutSeconds; } }

        /// <summary>等待首个有效结果的超时秒数（FIRST_RESULT_SECONDS）。</summary>
        private int FirstResultTimeoutSeconds { get { return LineLaserManager.Instance.Config.FirstResultTimeoutSeconds; } }

        #endregion

        #region 私有变量

        /// <summary>公共方法互斥锁（用私有锁对象）。</summary>
        private readonly object _syncRoot = new object();

        private int _consecutiveFailureCount;
        private int _maxConsecutiveFailures = 5;
        // 行程目标（MaxWeldLength）：真实模式默认目标
        private double _maxWeldLength = 1000.0;

        /// <summary>本会话测量是否已收工（主控命中后置位）。</summary>
        /// <remarks>收工＝关外设、入图结束，但流程态仍留 Working；
        /// 见本标志即跳过工艺调整与失效判定。进入新沿时复位。</remarks>
        private volatile bool _measureFinished;
        private bool _simReady = false;

        /// <summary>连接/断开线程是否进行中（防重入）。</summary>
        private volatile bool _isConnecting = false;

        /// <summary>连接线程句柄（关闭时 Join）</summary>
        private Thread _connectThread;

        /// <summary>断开线程句柄（关闭时 Join）</summary>
        private Thread _disconnectThread;

        // 分步协同字段（主设备驱动模组分步启动）
        private volatile bool _preWorkDone;
        private volatile bool _dataStable;
        private int _startingValidFrameCount;

        // Working 态字段
        private volatile bool _adjustmentPending;
        private volatile bool _lastResultValid;
        private double _measuredFps;
        private int _consecutiveSuccessCount;
        private int _adjustTriggerSuccessCount = 1;
        private bool _lastAdjustSuccess;

        // 失败收尾记录（子类私有字段）
        private string _failReason = "";
        private int _failStep;

        /// <summary>是否已启用模拟模式。</summary>
        private bool IsSimulation { get { return DeviceControlWork.Instance.IsSimulationEnabled; } }

        /// <summary>是否使用虚拟相机（模拟下不接真机的开关）。</summary>
        private bool IsVirtualCamera { get { return DeviceControlWork.Instance.ShouldUseVirtualCamera; } }

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

        /// <summary>数据是否稳定（主设备轮询，稳定后可进 Working）。</summary>
        public bool IsDataStable { get { return _dataStable; } }

        /// <summary>模组配置文件名（不含扩展名），在本流程类别目录。</summary>
        protected override string ConfigFileName { get { return "LineLaserWorkflow"; } }

        /// <summary>配置落 LineLaser\Workflow_XML 子目录。</summary>
        protected override string ConfigRelativeDir { get { return @"Config\LineLaser\Workflow_XML"; } }

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
                if (IsSimulation
                    && !IsVirtualCamera)
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
                // 相机经 Manager 代理取用，不持有厂商类型
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

        /// <summary>初始化三步模板（连接 → 读配置 → 校验硬件状态）。</summary>
        /// <param name="connect">第 1 步：连接设备</param>
        /// <param name="readConfiguration">第 2 步：读取配置</param>
        /// <param name="verifyHardwareStatus">第 3 步：校验硬件状态</param>
        /// <returns>三步全部成功返回 true；任一步失败即中断并返回 false</returns>
        private bool RunInitializeSteps(
            Func<bool> connect, Func<bool> readConfiguration, Func<bool> verifyHardwareStatus)
        {
            Log("初始化步骤 1/3 连接设备", MessageLevel.Debug);
            if (!RunStepAction(connect, "连接设备", "连接失败", LineLaserErrorContext.Connection)) return false;
            Log("初始化步骤 2/3 读取配置", MessageLevel.Debug);
            if (!RunStepAction(readConfiguration, "读取配置", "配置读取失败", LineLaserErrorContext.Initialization)) return false;
            Log("初始化步骤 3/3 状态信号校验", MessageLevel.Debug);
            if (!RunStepAction(verifyHardwareStatus, "状态信号校验", "状态信号异常", LineLaserErrorContext.Initialization)) return false;
            return true;
        }

        /// <summary>执行单个初始化步骤并统一处理异常。</summary>
        /// <remarks>方法名避让基类 RunStep 属性（CS0102）。失败描述由硬件管理器产出，本方法只负责追加。</remarks>
        /// <param name="step">步骤执行体（调用方保证非 null）</param>
        /// <param name="stepName">步骤名（日志用）</param>
        /// <param name="failureReason">失败原因（日志用）</param>
        /// <param name="context">失败语境（取硬件管理器的成品描述）</param>
        /// <returns>步骤成功返回 true</returns>
        private bool RunStepAction(Func<bool> step, string stepName, string failureReason,
            LineLaserErrorContext context)
        {
            try
            {
                if (step()) return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("[{0}] 异常 原因 {1}", stepName, ex.Message), MessageLevel.Debug);
            }
            Log(string.Format("[{0}] 失败 原因是 {1}；{2}", stepName, failureReason,
                LineLaserManager.Instance.DescribeLastError(context)), MessageLevel.Debug);
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
        /// <remarks>取激活相机并订阅轮廓事件，Start 时调一次。</remarks>
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
            // 必须开连续采集，否则不出图、等稳定必超时
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
            HandleFailure(string.Format("PreWork 阶段等待数据稳定超时 {0}秒", DataStableTimeoutSeconds));
            ResetWorkTime();
            return false;
        }

        /// <summary>执行步 31：等待主设备通知进入 Working。</summary>
        /// <returns>收到通知（或模拟自驱动）返回 true</returns>
        private bool DoPreWorkWaitWork()
        {
            if (!IsTimeout(WaitWorkingTimeoutSeconds)) return false;
            Fail(string.Format("数据稳定后等待 Working 超时 {0}秒 主设备未调用 BeginWorking", WaitWorkingTimeoutSeconds));
            return false;
        }

        /// <summary>模拟相机准备（每拍幂等）：选相机 + 绑定 + 开采集。</summary>
        /// <remarks>不连硬件、不置流程态、不切步，迁移由命令链驱动。
        /// 模拟器下发等效指令，与真实时序对齐。</remarks>
        private void SimPrepareCamera()
        {
            if (_simReady) return;

            if (IsVirtualCamera)
                LineLaserManager.Instance.SelectVirtual();
            else
                LineLaserManager.Instance.SelectIntelligentLaser();
            LineLaserManager.Instance.SetWorking(true);
            BindCamera();
            // 模拟态先开采集兜底，确保轮廓帧产出
            LineLaserManager.Instance.StartAcquisition();
            _preWorkDone = true;
            _simReady = true;
            Log("模拟模式 相机准备完成 等待命令链驱动步骤", MessageLevel.Info);
        }

        /// <summary>执行步 100：焊接工作（轮廓采集与自适应调整）。</summary>
        /// <remarks>行程完成判断归设备主控，本流程不自行判停。
        /// Stopping 只由命令链经 WeldStatusTrans 进入。</remarks>
        private void DoWorking()
        {
            // 测量已收工：入图结束，不再调整与失效判定
            // 流程态保持 Working，由主控按行程统一收口
            if (_measureFinished) return;

            if (_adjustmentPending)
            {
                _adjustmentPending = false;
                _consecutiveSuccessCount = 0;
                RunAutoAdjust();
                return;
            }

            if (LineLaserManager.Instance.IsVirtual)
            {
                _lastResultValid = true;
                // 虚拟相机无结果事件，仍须驱动工艺计算下发
                // 使 .pDat 工艺控制记录按拍填充；实际计算在 WeldParamControlWorkflow 自身流程线程消费 _pendingCompute 时执行
                try { WeldParamControlWorkflow.Instance.ComputeAndOutput(); }
                catch (Exception ex) { Log("模拟工艺计算触发异常 " + ex.Message, MessageLevel.Error); }
                return;
            }

            if (_lastResultValid)
            {
                _consecutiveFailureCount = 0;
                return;
            }

            // 缺失满一个超时才计一次失败，避免打满计数
            if (!IsTimeout(FirstResultTimeoutSeconds)) return;
            HandleFailure("线激光识别失败 ParseRes 非 1；"
                + LineLaserManager.Instance.DescribeLastError(LineLaserErrorContext.Recognition));
            ResetWorkTime();
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
            // 业务处理已上提 LineLaserManager，工作流订阅其转发的结果事件
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
                    // 焊缝特征由 Manager 直接产出，不经算法中转
                    _adjustmentPending = true;
                }
            }
            else
            {
                _consecutiveSuccessCount = 0;
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
                // 特征由 Manager 产出，本处不传参，由工艺流计算下发
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

        /// <summary>转入失败收尾。</summary>
        /// <param name="reason">失败原因（纯文本，无符号）</param>
        private void Fail(string reason)
        {
            _failStep = RunStep;
            _failReason = reason;
            AdvanceStep(StepFinishFail);
        }

        /// <summary>连接后台线程体。</summary>
        private void ConnectWorker(WorkflowRunMode mode)
        {
            try
            {
                LogAction(mode, "连接开始", MessageLevel.Debug);
                bool ok = InitializeInternal();
                if (ok)
                {
                    SetState(SubDeviceState.Connected, "线激光连接完成");
                    LogAction(mode, "连接完成", MessageLevel.Debug);
                }
                else
                {
                    string connectDiagnosis =
                        LineLaserManager.Instance.DescribeLastError(LineLaserErrorContext.Connection);
                    MarkConnectFailed("线激光连接失败；" + connectDiagnosis);
                    SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "线激光连接失败；" + connectDiagnosis);
                    LogAction(mode, "连接失败", MessageLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                MarkConnectFailed("线激光连接异常 " + ex.Message);
                SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "连接异常 " + ex.Message);
                LogAction(mode, "连接异常 " + ex.Message, MessageLevel.Info);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>断开后台线程体：断开相机外设并置 Disconnected。</summary>
        private void DisconnectWorker(WorkflowRunMode mode)
        {
            try
            {
                LogAction(mode, "断开开始", MessageLevel.Info);
                LineLaserManager.Instance.Disconnect();
                SafeShutdownPeripherals();
                ReleaseCameraBinding();
                SetState(SubDeviceState.Disconnected, "线激光断开");
                LogAction(mode, "断开完成", MessageLevel.Info);
            }
            catch (Exception ex)
            {
                LogAction(mode, "断开异常 " + ex.Message, MessageLevel.Info);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>模拟结束复位：反订阅相机事件、关采集、焊接态与</summary>
        /// <remarks>步骤归位，避免污染后续真实启动。</remarks>
        public void SimReset()
        {
            if (!_simReady) return;
            _simReady = false;
            UnsubscribeCameraEvents();
            LineLaserManager.Instance.SetWorking(false);
            SetWeldStatus(SubDeviceWeldStatus.Standby, "模拟结束复位");
            AdvanceStep(StepIdle);
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

            if (IsSimulation && IsVirtualCamera)
                LineLaserManager.Instance.SelectVirtual();
            else
                LineLaserManager.Instance.SelectIntelligentLaser();

            LineLaserManager.Instance.SetWorking(true);
            _measuredFps = 0.0;

            BindCamera();
        }

        /// <summary>停止工作流（模式由调用入口置位）。</summary>
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
                SetWeldStatus(SubDeviceWeldStatus.ManualStopped, ModeText + "停止");
            }
        }

        /// <summary>线激光测量行程完成收工（主控命中行程时调用）。</summary>
        /// <remarks>前置量：入图键 = 测量X + 前置量 = 焊缝终止 X。
        /// 扫描至此已达焊缝末端，再往前无焊缝可测。
        /// 故收工＝关闭外设，入图不再产生。
        /// 焊接未结束，故不改 WeldStatus（保持 Working）。
        /// 行程判据唯一归主控，本方法幂等。</remarks>
        public void FinishMeasureByStroke()
        {
            lock (_syncRoot)
            {
                if (_measureFinished) return;
                if (WeldStatus != SubDeviceWeldStatus.Working && WeldStatus != SubDeviceWeldStatus.PreWork) return;
                _measureFinished = true;
            }
            Log("焊缝测量行程完成 已扫过焊缝末端 关闭激光与传感器（入图结束，焊接继续走到焊缝结束位置）");
            SafeShutdownPeripherals();
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

        #region 常驻流程

        /// <summary>行程目标上限（mm，MaxWeldLength）。</summary>
        public double MaxWeldLengthMm
        {
            get { return _maxWeldLength; }
        }

        /// <summary>PreWork 完成反馈：数据稳定且已进入等待 Working 指令的步骤。</summary>
        /// <remarks>主控轮询本反馈后才下发 Working 指令。</remarks>
        public bool IsPreWorkFeedbackDone
        {
            get { return WeldStatus == SubDeviceWeldStatus.PreWork && _dataStable; }
        }

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询，等待靠「外部线程重入 + IsTimeout 判超时」。</remarks>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            // 模拟只做相机准备，步迁移由命令链驱动
            // 子流程不自切步，按指令调度范式推进
            if (IsSimulation)
            {
                SimPrepareCamera();
            }

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
                    // 行程完成判断归设备主控；Stopping 只由命令链（WeldStatusTrans）进入，本步永不自行推进
                    DoWorking();
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
                    // 空转等待，靠流程态切换把执行步复位
                    break;
            }
        }

        /// <summary>流程复位：关外设回待机（线性阻塞链）。</summary>
        /// <returns>未连接返回 false，复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            lock (_syncRoot)
            {
                if (IsSimulation)
                {
                    // 模拟态不连接硬件，不得以未连接为由拒绝复位——
                    // 否则 ErrorAborted 残留，清理与启动互相锁死
                    UnsubscribeCameraEvents();
                    LineLaserManager.Instance.SetWorking(false);
                    _simReady = false;
                    IsAlarm = false;
                    AdvanceStep(StepIdle);
                    SetWeldStatus(SubDeviceWeldStatus.Standby, "模拟复位");
                    return true;
                }
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

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入</summary>
        /// <remarks>步骤时长导致异常超时。</remarks>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：同步等待至连接完成。上电初始化</summary>
        /// <remarks>阶段使用。</remarks>
        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            if (State == SubDeviceState.Connected) return true;
            bool ok;
            try
            {
                Log("初始化连接开始", MessageLevel.Debug);
                ok = InitializeInternal();
            }
            catch (Exception ex)
            {
                Log("初始化连接异常 " + ex.Message, MessageLevel.Error);
                ok = false;
            }
            if (!ok)
            {
                SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "线激光初始化连接失败；"
                    + LineLaserManager.Instance.DescribeLastError(LineLaserErrorContext.Connection));
                return false;
            }
            SetState(SubDeviceState.Connected, "线激光初始化连接完成");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化连接完成，等待复位");
            return true;
        }

        /// <summary>设备断开（阻塞）：内部执行释放动作，调用方阻塞至</summary>
        /// <remarks>完成。</remarks>
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

        /// <summary>设备日志：输出线激光差异化日志（结果 / 帧率 / 稳定</summary>
        /// <remarks>帧计数）。</remarks>
        public override void SubDeviceLog()
        {
            Log(string.Format("线激光 连接态 {0} 焊接过程态 {1} 步骤 {2} 有效结果 {3} 实测帧率 {4:F1} 稳定帧 {5}",
                State, WeldStatus, WorkStepName, _lastResultValid, _measuredFps, _startingValidFrameCount));
        }

        /// <summary>加载本模组的配置。</summary>
        public override void LoadConfig()
        {
            TimeOutSeconds = ReadInt("TimeOutSeconds", 30);
            _maxConsecutiveFailures = ReadInt("MaxConsecutiveFailures", 5);
            _maxWeldLength = ReadDouble("MaxWeldLength", 1000.0);
            _adjustTriggerSuccessCount = ReadInt("AdjustTriggerSuccessCount", 1);
        }

        /// <summary>保存本模组的配置。</summary>
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

        #region 非阻塞动作指令

        /// <summary>开启连接（非阻塞）：自起后台线程执行连接，动作即</summary>
        /// <remarks>退出释放。</remarks>
        /// <returns>受理返回 true，连接进行中被拒绝返回 false</returns>
        public override bool ConnectOn()
        {
            if (State == SubDeviceState.Connected) return true;
            if (_isConnecting) return false;
            _isConnecting = true;
            BeginConnectAttempt();
            // 模式在入口线程捕获，后台线程只读快照
            WorkflowRunMode mode = RunMode;
            var t = new Thread(() => ConnectWorker(mode))
            {
                Name = "LineLaserConnect",
                IsBackground = true
            };
            _connectThread = t;
            DeviceControlWork.Instance.RegisterWorkThread(t, 5000);
            t.Start();
            return true;
        }

        /// <summary>关闭连接（非阻塞）：自起后台线程执行断开，动作即</summary>
        /// <remarks>退出释放。</remarks>
        /// <returns>受理返回 true，断开进行中被拒绝返回 false</returns>
        public override bool ConnectOff()
        {
            if (State == SubDeviceState.Disconnected) return true;
            if (_isConnecting) return false;
            _isConnecting = true;
            // 模式在入口线程捕获：后台线程不读可变字段
            WorkflowRunMode mode = RunMode;
            var t = new Thread(() => DisconnectWorker(mode))
            {
                Name = "LineLaserDisconnect",
                IsBackground = true
            };
            _disconnectThread = t;
            DeviceControlWork.Instance.RegisterWorkThread(t, 5000);
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
                    _measureFinished = false;   // 新一沿：测量收工标志复位
                    AdvanceStep(StepPreWork);
                    break;
                case SubDeviceWeldStatus.Working:
                    _measureFinished = false;   // 进入焊接工作态同样复位
                    AdvanceStep(StepWorking);
                    break;
                case SubDeviceWeldStatus.Stopping:
                    AdvanceStep(StepStopping);
                    break;
                case SubDeviceWeldStatus.ErrorAborted:
                    // 异常终止也须关线激光再回待机
                    // 否则外设仍出光采集，与恢复初始态矛盾
                    Log("线激光异常态收尾 关闭外设(停采集 关激光 关传感器)", MessageLevel.Warning);
                    SafeShutdownPeripherals();
                    AdvanceStep(StepIdle);
                    ReleaseCameraBinding();
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
            StopPendingConnectThreads();
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
        }

        /// <summary>等待连接/断开线程退出。</summary>
        /// <remarks>超时记 Warning 并保留句柄，供现场复查。</remarks>
        private void StopPendingConnectThreads()
        {
            JoinPendingThread(ref _connectThread, "LineLaserConnect");
            JoinPendingThread(ref _disconnectThread, "LineLaserDisconnect");
        }

        /// <summary>等待单条连接类线程退出。</summary>
        /// <param name="thread">线程句柄引用</param>
        /// <param name="name">线程名（日志用）</param>
        private void JoinPendingThread(ref Thread thread, string name)
        {
            Thread target = thread;
            if (target == null) return;
            if (!target.IsAlive || target.Join(5000))
            {
                thread = null;
                return;
            }
            Log(name + " 未在 5000ms 内退出，保留句柄待复查", MessageLevel.Warning);
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
