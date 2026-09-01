using System;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.EmguALG.EmguConfiger;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.ProductFileManager;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>焊接工艺控制流程状态（业务工作态）：随焊接工艺业务变化。</summary>
    /// <remarks>· Uninitialized 未初始化（尚未载入工艺参数） · Initializing  初始化中（载入工艺参数/查找表） · Standby       待机（工艺参数就绪，等待焊接开始） · Computing     计算中（根据机器人信息 + 线激光结果计算工艺参数） · Outputting    下发中（计算结果下发至机器人/激光器） · Holding       保持中（连续失败超阈值，暂停调整） · ErrorAborted  异常终止</remarks>
    public enum WeldParamControlWorkflowState
    {
        Uninitialized = 0,
        Initializing = 1,
        Standby = 2,
        Computing = 3,
        Outputting = 4,
        Holding = 5,
        ErrorAborted = 6
    }

    /// <summary>焊接工艺参数输出（计算结果）。</summary>
    /// <remarks>由本工作流计算得出，下发给机器人与激光器：焊接功率、送丝速度、振镜摆幅、焊接速度。</remarks>
    public class WeldParamOutput
    {
        /// <summary>焊接功率（激光功率）</summary>
        public double LaserPower;

        /// <summary>送丝速度</summary>
        public double FeedSpeed;

        /// <summary>焊接速度（机器人移动速度）</summary>
        public double RobotSpeed;

        /// <summary>振镜摆幅</summary>
        public double GalvoAmplitude;

        /// <summary>本轮采用的焊缝宽度（平滑后）</summary>
        public double SeamWidth;

        /// <summary>当前机器人 X 坐标（真实坐标）</summary>
        public double RobotX;

        /// <summary>计算是否成功</summary>
        public bool Success;

        public DateTime Timestamp;
    }

    /// <summary>
    /// 焊接工艺控制工作流（单例）：继承统一泛型基类 DeviceWorkflowBase&lt;WeldParamControlWorkflowState&gt;。
    ///
    /// 定位：本工作流**不是有连接动作的设备**，而是设备内部的一个「控制焊接工艺的流程」。
    /// 核心作用：接收机器人信息（RobotX 坐标等）+ 线激光结果信息（焊缝宽度/焊缝特征），
    /// 经分析计算得出焊接功率、送丝速度、振镜摆幅、焊接速度，并下发给机器人。
    ///
    /// 数据流向（焊缝信息即英莱线激光的结果）：
    ///   1. 英莱线激光相机取像，回调 IlInspectResult 直接携带特征点 FeatureY/FeatureZ 与宽度 Width0/Width1
    ///   2. LineLaserWorkflow.OnContourFrameReady 据此构造 SeamFeatureResult，
    ///      经 PipelineManager.ReportExternalResult(sf, robotX) 上报
    ///   3. WeldProcess.OnAlgorithmCompleted 暂存最新帧特征 _lastSeamFeature 与 _lastRobotX
    ///   4. 本工作流调用 WeldProcess.TriggerAdjustment()（无参，用上一步暂存值）
    ///      完成滑动平滑 + 查找表匹配 + 灵敏度判定
    ///   5. 输出 WeldParamOutput（功率/送丝/速度/摆幅）→ 下发机器人
    /// 注：英莱回调本就含特征点与宽度，本工作流补的是「计算后下发到机器人」这一环。
    ///
    /// 设备态映射（四态）——本流程无连接动作，按业务进度映射：
    ///   Uninitialized → Disconnect（工艺参数未就绪）
    ///   Standby       → Connect（工艺参数就绪）
    ///   Initializing/Computing/Outputting/Holding → Work（业务进程中）
    ///   ErrorAborted  → Alarm
    ///
    /// 步骤驱动（ADR-047）：本流程是「被外部逐帧触发的一次性计算 + 下发」，不是自推进流程，
    /// 故采用命令变量模式 —— ComputeAndOutput 只置 _pendingCompute，业务由监听线程按
    /// 执行步 10 计算 → 20 下发 → 800 成功收尾 / 900 失败收尾 完成。
    /// 上一轮未完成时新触发会被丢弃，天然实现高频调用下的节流。
    /// </summary>
    public class WeldParamControlWorkflow : DeviceWorkflowBase<WeldParamControlWorkflowState>
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepCompute = 10;
        private const int StepOutput = 20;

        /// <summary>自动流程单轮计算与下发的有界等待超时（毫秒，ADR-048）。</summary>
        private const int AutoFinishTimeoutMs = 10000;

        #endregion

        #region 私有变量

        private readonly string _tag = "焊接工艺控制工作流";

        /// <summary>命令变量：外部触发一次工艺计算与下发（只置位，业务由监听线程消费）。</summary>
        private volatile bool _pendingCompute;

        /// <summary>最近一次计算结果输出（类级复用，避免高频分配）</summary>
        private readonly WeldParamOutput _output = new WeldParamOutput();

        #endregion

        #region 单例

        private static readonly Lazy<WeldParamControlWorkflow> _lazyInstance =
            new Lazy<WeldParamControlWorkflow>(() => new WeldParamControlWorkflow());

        public static WeldParamControlWorkflow Instance => _lazyInstance.Value;

        #endregion

        #region 公共变量

        public override string StateName => _tag;

        /// <summary>工艺参数是否已就绪（等价于设备态 Connect）。</summary>
        public bool IsParamReady => _step == WeldParamControlWorkflowState.Standby;

        /// <summary>连续失败次数（直通 WeldProcess）。</summary>
        public int ConsecutiveFailureCount => WeldProcess.Instance.ConsecutiveFailureCount;

        /// <summary>最近一次计算结果（类级复用实例，供 UI 折线图读取）。</summary>
        public WeldParamOutput LastOutput => _output;

        #endregion

        #region 构造函数

        private WeldParamControlWorkflow() : base(WeldParamControlWorkflowState.Uninitialized) { }

        #endregion

        #region 私有函数

        /// <summary>组装工艺参数输出（功率 / 送丝 / 速度 / 振镜摆幅）。</summary>
        /// <param name="wp">焊接工艺管理器（读取最近一次调整结果）</param>
        private void FillOutput(WeldProcess wp)
        {
            _output.LaserPower = wp.LaserPowerOutput;
            _output.FeedSpeed = wp.FeedSpeedOutput;
            _output.RobotSpeed = wp.RobotSpeedOutput;
            _output.SeamWidth = wp.CurrentSeamWidth;
            _output.RobotX = wp.CurrentRobotX;
            _output.GalvoAmplitude = CalcGalvoAmplitude(wp);
            _output.Timestamp = DateTime.Now;
        }

        /// <summary>计算振镜摆幅。</summary>
        /// <remarks>TODO 待实现：规划在 AutoParamManager 新增 WeaveWidth 分段表， 落地后改为按焊缝宽度查表取值，与送丝速度/焊接速度/激光功率三张表同样的匹配方式。 当前为占位实现，返回 0（不摆动），不影响其余三项工艺参数下发。</remarks>
        /// <param name="wp">焊接工艺参数对象</param>
        /// <returns>摆幅值；当前为占位实现，恒返回 0 表示不摆动</returns>
        private static double CalcGalvoAmplitude(WeldProcess wp)
        {
            // TODO 待 WeaveWidth 查找表落地后改为查表匹配
            return 0.0;
        }

        /// <summary>下发工艺参数至机器人（经顶层 Comm 的机器人通讯）。</summary>
        /// <remarks>注：连接本身由顶层 Comm（CommunicationManager）管控，此处只做数据下发。</remarks>
        /// <param name="output">待下发的工艺参数</param>
        /// <returns>下发成功返回 true</returns>
        private bool SendToRobot(WeldParamOutput output)
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null || !comm.IsRobotEnabled || comm.RobotManager == null)
                {
                    // 机器人未启用时视为无需下发，不判定为失败
                    return true;
                }

                // 协议格式：P功率 S送丝 V速度 G摆幅
                string payload = string.Format("P{0} S{1} V{2} G{3}",
                    output.LaserPower, output.FeedSpeed, output.RobotSpeed, output.GalvoAmplitude);

                comm.RobotManager.SendRobotData(new Comm.Robot.KUKARobot.KUKARobotData { EStr = payload });
                return true;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(_tag, "工艺参数下发异常 " + ex.Message, MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>执行步 10：工艺参数计算。</summary>
        /// <remarks>输入机器人 RobotX 与线激光焊缝特征，输出功率/送丝速度/焊接速度/摆幅。</remarks>
        private void DoCompute()
        {
            SetStep(WeldParamControlWorkflowState.Computing, "焊接工艺参数计算中");
            var wp = WeldProcess.Instance;

            if (wp.CurrentAutoParam == null)
            {
                FailFlow("未载入工艺参数，无法计算");
                return;
            }

            // 连续失败超阈值：进入保持态，不再计算
            if (wp.ConsecutiveFailureCount >= wp.MaxConsecutiveFailures)
            {
                SetStep(WeldParamControlWorkflowState.Holding, string.Format(
                    "连续失败 {0} 次达到阈值 {1}，暂停工艺调整",
                    wp.ConsecutiveFailureCount, wp.MaxConsecutiveFailures));
                GoStep(StepIdle);
                return;
            }

            wp.TriggerAdjustment();
            FillOutput(wp);
            SetStep(WeldParamControlWorkflowState.Outputting, "焊接工艺参数下发中");
        }

        /// <summary>执行步 20：下发工艺参数。</summary>
        /// <remarks>下发至机器人，失败即转失败收尾。</remarks>
        private void DoOutput()
        {
            bool sent = SendToRobot(_output);
            _output.Success = sent;
            if (sent)
                GoStep(StepFinishOk);
            else
                FailFlow("焊接工艺参数下发失败");
        }

        /// <summary>执行步 800：成功收尾（结果已写入 _output，订阅方读 LastOutput，ADR-042 R4）。</summary>
        private void DoFinishOk()
        {
            GoStep(StepIdle);
        }

        /// <summary>执行步 900：失败收尾三步走。</summary>
        /// <remarks>置 Alarm 须经 SetStep 映射（禁手工赋值 State），再记日志与故障记录后回待机。</remarks>
        private void DoFinishFail()
        {
            SetStep(WeldParamControlWorkflowState.ErrorAborted, FailReason);
            IsAlarm = true;
            FaultRecoveryManager.Instance.RecordFault(_tag, new FaultRecord
            {
                Time = DateTime.Now,
                Device = _tag,
                State = MapToDeviceState(_step),
                Category = FaultCategory.Process,
                ErrorCode = "WeldParamStepFail",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", WorkStep, FailReason),
                AutoRecovered = false,
                RecoveryAction = "重新载入工艺参数后复位"
            });
            GlobalCommData.ShowLog(_tag, string.Format("焊接工艺控制异常终止 原因 {0}", FailReason), MessageLevel.Error);
            GoStep(StepIdle);
        }

        #endregion

        #region 公共方法

        /// <summary>载入工艺参数并进入待机（对应设备态 Connect）。</summary>
        /// <remarks>由主设备上电初始化或工艺参数切换时调用。</remarks>
        /// <returns>载入成功返回 true</returns>
        public bool LoadParameters()
        {
            SetStep(WeldParamControlWorkflowState.Initializing, "焊接工艺参数载入开始");
            try
            {
                WeldDataManager.Instance.ReloadCurrentParamValues();

                var wp = WeldProcess.Instance;
                if (wp.CurrentAutoParam == null)
                {
                    SetStep(WeldParamControlWorkflowState.Uninitialized, "未载入有效工艺参数");
                    return false;
                }

                SetStep(WeldParamControlWorkflowState.Standby, "焊接工艺参数就绪");
                return true;
            }
            catch (Exception ex)
            {
                SetStep(WeldParamControlWorkflowState.ErrorAborted, "工艺参数载入异常 " + ex.Message);
                return false;
            }
        }

        /// <summary>触发工艺计算与下发。</summary>
        /// <remarks>根据机器人信息与线激光结果计算并下发焊接工艺参数；返回上一轮是否成功。</remarks>
        /// <returns>上一轮计算与下发是否成功（本轮结果请读 LastOutput.Success）</returns>
        public bool ComputeAndOutput()
        {
            _pendingCompute = true;
            StartRunLoop();
            return _output.Success;
        }

        /// <summary>
        /// 本轮焊接结束，回到待机（对应设备态 Connect）。
        /// </summary>
        public void Complete()
        {
            SetStep(WeldParamControlWorkflowState.Standby, "本轮焊接完成，工艺控制回到待机");
        }

        /// <summary>切换工艺配置并重新载入参数。</summary>
        /// <param name="configId">工艺配置标识</param>
        /// <returns>切换并载入成功返回 true</returns>
        public bool SwitchParameter(string configId)
        {
            try
            {
                WeldDataManager.Instance.SwitchConfig(configId);
                return LoadParameters();
            }
            catch (Exception ex)
            {
                SetStep(WeldParamControlWorkflowState.ErrorAborted, "切换工艺配置异常 " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 流程态 → 设备四态映射

        /// <summary>阶段态转设备四态。</summary>
        /// <remarks>本流程无连接动作，按业务进度映射。</remarks>
        /// <param name="step">阶段态</param>
        /// <returns>该阶段态对应的设备四态</returns>
        protected override DeviceState MapToDeviceState(WeldParamControlWorkflowState step)
        {
            switch (step)
            {
                case WeldParamControlWorkflowState.Uninitialized:
                    return DeviceState.Disconnect;
                case WeldParamControlWorkflowState.Standby:
                    return DeviceState.Connect;
                case WeldParamControlWorkflowState.ErrorAborted:
                    return DeviceState.Alarm;
                default:
                    // Initializing / Computing / Outputting / Holding 均为业务进程态
                    return DeviceState.Work;
            }
        }

        #endregion

        #region 阶段态变更钩子（阶段态切换时复位执行步到该阶段入口）

        /// <summary>阶段态切换时把执行步复位到该阶段入口步。</summary>
        /// <remarks>注意：本方法在 SetStep 同步路径内执行，调用处 SetStep 后须立即 return。</remarks>
        /// <param name="e">流程态变更参数</param>
        protected override void OnStepChanged(WorkflowStepChangedEventArgs<WeldParamControlWorkflowState> e)
        {
            base.OnStepChanged(e);
            switch (e.NewState)
            {
                case WeldParamControlWorkflowState.Computing:
                    GoStep(StepCompute);
                    break;
                case WeldParamControlWorkflowState.Outputting:
                    GoStep(StepOutput);
                    break;
                default:
                    GoStep(StepIdle);
                    break;
            }
        }

        #endregion

        #region 执行步分派

        /// <summary>单步分派（switch(WorkStep)），由监听线程按 10ms 节拍反复调用。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询。</remarks>
        protected override void FlowProcess()
        {
            switch (WorkStep)
            {
                case StepCompute: DoCompute(); break;
                case StepOutput: DoOutput(); break;
                case StepFinishOk: DoFinishOk(); break;
                case StepFinishFail: DoFinishFail(); break;
                default:
                    // StepIdle 与未登记步号：空转等待触发
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
                case StepIdle: return "待触发";
                case StepCompute: return "工艺参数计算";
                case StepOutput: return "工艺参数下发";
                case StepFinishOk: return "成功收尾";
                case StepFinishFail: return "失败收尾";
                default: return string.Format("未登记步({0})", step);
            }
        }

        #endregion

        #region 抽象生命周期方法

        /// <summary>初始化流程 = 载入工艺参数（对应设备态 Connect）。</summary>
        /// <returns>初始化成功返回 true</returns>
        protected override bool InitializeFlow()
        {
            return LoadParameters();
        }

        /// <summary>手动开 = 重新载入工艺参数。</summary>
        protected override void ManualOn()
        {
            LoadParameters();
        }

        /// <summary>手动关 = 停止工艺计算，回到待机就绪。</summary>
        protected override void ManualOff()
        {
            SetStep(WeldParamControlWorkflowState.Standby, "手动停止焊接工艺控制");
        }

        /// <summary>自动运行流程 = 阻塞式触发一轮计算与下发并等待完成（ADR-048）。</summary>
        /// <remarks>计算与下发仍由监听线程按执行步 10→20→800/900 推进，本序列只做排程与有界等待。</remarks>
        protected override void AutoRunFlow()
        {
            ComputeAndOutput();
            AutoWait(() => WorkStep == StepIdle, AutoFinishTimeoutMs);
        }

        /// <summary>流程复位：清触发标记与失败计数，回到待机（由基类公共入口 Reset 调用，ADR-048）。</summary>
        protected override void ResetFlow()
        {
            _pendingCompute = false;
            IsAlarm = false;
            GoStep(StepIdle);
            SetStep(WeldParamControlWorkflowState.Standby, "流程复位");
        }

        #endregion

        #region 监听线程钩子

        /// <summary>命令变量消费：把外部触发转为一轮执行步推进。</summary>
        /// <remarks>上一轮未跑完（WorkStep 非 StepIdle）时丢弃本次触发，实现高频调用下的自然节流。</remarks>
        protected override void ConsumeCommand()
        {
            if (!_pendingCompute) return;
            _pendingCompute = false;
            if (WorkStep == StepIdle)
                GoStep(StepCompute);
        }

        #endregion
    }
}
