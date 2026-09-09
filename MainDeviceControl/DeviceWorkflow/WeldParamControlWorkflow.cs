using System;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.WeldParamControl;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>焊接工艺控制流程状态（业务工作态）：随焊接工艺业务变化。</summary>
    /// <remarks>· Uninitialized 未初始化（尚未载入工艺参数） · Initializing 初始化中（载入工艺参数/查找表） · Standby 待机（工艺参数就绪，等待焊接开始） · Computing 计算中（根据机器人信息 + 线激光结果计算工艺参数） · Outputting 下发中（计算结果下发至机器人/激光器） · Holding 保持中（连续失败超阈值，暂停调整） · ErrorAborted 异常终止</remarks>
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

    /// <summary>焊接工艺控制工作流（单例，无实体设备）。</summary>
    /// <remarks>消费机器人 RobotX 与线激光焊缝特征，计算功率/送丝/振镜/速度经 WeldParamOutput 下发；Connect 仅表示参数就绪（原档 R012）。</remarks>
    public class WeldParamControlWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepCompute = 10;
        private const int StepOutput = 20;

        #endregion

        #region 私有变量

        /// <summary>私有相位：仅驱动 FlowProcess 与可观测性，对外以 SubDeviceWeldStatus 暴露。</summary>
        private WeldParamControlWorkflowState _phase = WeldParamControlWorkflowState.Uninitialized;

        /// <summary>无实体设备，以标志位表达「连接」状态（工艺参数就绪）。</summary>
        private volatile bool _paramReady;

        /// <summary>命令变量：外部触发一次工艺计算与下发（只置位，业务由编排线程消费）。</summary>
        private volatile bool _pendingCompute;

        /// <summary>最近一次计算结果输出（类级复用，避免高频分配）</summary>
        private readonly WeldParamOutput _output = new WeldParamOutput();

        // 失败收尾记录（原基类 FailReason / FailStep 已删除，下沉为子类私有字段）
        private string _failReason = "";
        private int _failStep;

        #endregion

        #region 单例

        private static readonly Lazy<WeldParamControlWorkflow> _lazyInstance =
            new Lazy<WeldParamControlWorkflow>(() => new WeldParamControlWorkflow());

        /// <summary>焊接工艺控制工作流单例。</summary>
        public static WeldParamControlWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        /// <summary>设备名称（日志/事件标识）。</summary>
        public override string StateName { get { return Tag; } }

        /// <summary>工艺参数是否已就绪（等价于设备态 Connect）。</summary>
        public bool IsParamReady { get { return _paramReady; } }

        /// <summary>连续失败次数（直通 WeldProcess）。</summary>
        public int ConsecutiveFailureCount { get { return WeldProcess.Instance.ConsecutiveFailureCount; } }

        /// <summary>最近一次计算结果（类级复用实例，供 UI 折线图读取）。</summary>
        public WeldParamOutput LastOutput { get { return _output; } }

        /// <summary>子设备配置文件名（不含扩展名），落在 Config/XML/ 下。</summary>
        protected override string ConfigFileName { get { return "WeldParamControlWorkflow"; } }

        #endregion

        #region 构造函数

        private WeldParamControlWorkflow()
        {
            Tag = "焊接工艺控制工作流";
        }

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
                Log("工艺参数下发异常 " + ex.Message, MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>相位 → 焊接过程态映射（取代旧基类 MapToDeviceState）。</summary>
        /// <param name="step">相位</param>
        /// <returns>对应焊接过程态</returns>
        private static SubDeviceWeldStatus MapWeldStatus(WeldParamControlWorkflowState phase)
        {
            switch (phase)
            {
                case WeldParamControlWorkflowState.Standby:
                    return SubDeviceWeldStatus.Standby;
                case WeldParamControlWorkflowState.Uninitialized:
                    return SubDeviceWeldStatus.NoReset;
                case WeldParamControlWorkflowState.ErrorAborted:
                    return SubDeviceWeldStatus.ErrorAborted;
                default:
                    // Initializing / Computing / Outputting / Holding 均为业务进程中
                    return SubDeviceWeldStatus.Working;
            }
        }

        /// <summary>刷新相位并映射为对外焊接过程态，同时把执行步复位到该阶段入口。</summary>
        /// <param name="phase">目标相位</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetPhase(WeldParamControlWorkflowState phase, string reason)
        {
            _phase = phase;
            SetWeldStatus(MapWeldStatus(phase), reason);
            switch (phase)
            {
                case WeldParamControlWorkflowState.Computing:
                    AdvanceStep(StepCompute);
                    break;
                case WeldParamControlWorkflowState.Outputting:
                    AdvanceStep(StepOutput);
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

        /// <summary>执行步 10：工艺参数计算。</summary>
        /// <remarks>输入机器人 RobotX 与线激光焊缝特征，输出功率/送丝速度/焊接速度/摆幅。</remarks>
        /// <returns>进入下发阶段返回 true，进入保持态或失败返回 false</returns>
        private bool DoCompute()
        {
            SetPhase(WeldParamControlWorkflowState.Computing, "焊接工艺参数计算中");
            var wp = WeldProcess.Instance;

            if (wp.CurrentAutoParam == null)
            {
                Fail("未载入工艺参数，无法计算");
                return false;
            }

            // 连续失败超阈值：进入保持态，不再计算
            if (wp.ConsecutiveFailureCount >= wp.MaxConsecutiveFailures)
            {
                SetPhase(WeldParamControlWorkflowState.Holding, string.Format(
                    "连续失败 {0} 次达到阈值 {1}，暂停工艺调整",
                    wp.ConsecutiveFailureCount, wp.MaxConsecutiveFailures));
                return false;
            }

            wp.TriggerAdjustment();
            FillOutput(wp);
            return true;
        }

        /// <summary>执行步 20：下发工艺参数。</summary>
        /// <remarks>下发至机器人，失败即转失败收尾。</remarks>
        /// <returns>下发成功返回 true</returns>
        private bool DoOutput()
        {
            bool sent = SendToRobot(_output);
            _output.Success = sent;
            if (!sent)
            {
                Fail("焊接工艺参数下发失败");
                return false;
            }
            return true;
        }

        /// <summary>执行步 800：成功收尾（结果已写入 _output，订阅方读 LastOutput，权责边界 R4）。</summary>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishOk()
        {
            SetPhase(WeldParamControlWorkflowState.Standby, "本轮工艺调整完成");
            return true;
        }

        /// <summary>执行步 900：失败收尾三步走。</summary>
        /// <remarks>置 Alarm 须经 SetWeldStatus 映射（禁手工赋值 State），再记日志与故障记录后回待机。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetPhase(WeldParamControlWorkflowState.ErrorAborted, _failReason);
            IsAlarm = true;
            Log(string.Format("焊接工艺控制异常终止 原因 {0}", _failReason), MessageLevel.Error);
            return true;
        }

        #endregion

        #region 公共方法

        /// <summary>载入工艺参数并进入待机（对应焊接过程态 Standby）。</summary>
        /// <remarks>由主设备上电初始化或工艺参数切换时调用。</remarks>
        /// <returns>载入成功返回 true</returns>
        public bool LoadParameters()
        {
            SetPhase(WeldParamControlWorkflowState.Initializing, "焊接工艺参数载入开始");
            try
            {
                WeldDataManager.Instance.ReloadCurrentParamValues();

                var wp = WeldProcess.Instance;
                if (wp.CurrentAutoParam == null)
                {
                    SetPhase(WeldParamControlWorkflowState.Uninitialized, "未载入有效工艺参数");
                    return false;
                }

                SetPhase(WeldParamControlWorkflowState.Standby, "焊接工艺参数就绪");
                return true;
            }
            catch (Exception ex)
            {
                SetPhase(WeldParamControlWorkflowState.ErrorAborted, "工艺参数载入异常 " + ex.Message);
                return false;
            }
        }

        /// <summary>触发工艺计算与下发。</summary>
        /// <returns>上一轮计算与下发是否成功（本轮结果请读 LastOutput.Success）</returns>
        public bool ComputeAndOutput()
        {
            _pendingCompute = true;
            return _output.Success;
        }

        /// <summary>本轮焊接结束，回到待机（Standby）。</summary>
        public void Complete()
        {
            SetPhase(WeldParamControlWorkflowState.Standby, "本轮焊接完成，工艺控制回到待机");
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
                SetPhase(WeldParamControlWorkflowState.ErrorAborted, "切换工艺配置异常 " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 全阻塞流程

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进。</summary>
        /// <remarks>StepIdle 内消费 _pendingCompute 触发变量；全部 case 无 Thread.Sleep / while 轮询。</remarks>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            switch (RunStep)
            {
                case StepCompute:
                    if (DoCompute()) SetPhase(WeldParamControlWorkflowState.Outputting, "焊接工艺参数下发中");
                    break;
                case StepOutput:
                    if (DoOutput()) AdvanceStep(StepFinishOk);
                    break;
                case StepFinishOk:
                    if (DoFinishOk()) AdvanceStep(StepIdle);
                    break;
                case StepFinishFail:
                    if (DoFinishFail()) AdvanceStep(StepIdle);
                    break;
                default:
                    if (_pendingCompute)
                    {
                        _pendingCompute = false;
                        AdvanceStep(StepCompute);
                    }
                    break;
            }
        }

        /// <summary>流程复位：清触发标记与失败计数，回到待机（线性阻塞链）。</summary>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            _pendingCompute = false;
            IsAlarm = false;
            if (!LoadParameters()) return false;
            SetWeldStatus(SubDeviceWeldStatus.Standby, "工艺控制复位完成");
            return true;
        }

        /// <summary>状态清理：标志位全复位 + 执行步归零 + 强制未复位态。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _pendingCompute = false;
            _failReason = "";
            _failStep = 0;
            SetPhase(WeldParamControlWorkflowState.Uninitialized, "状态清除");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入步骤时长导致异常超时。</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：无实体设备，载入工艺参数后置位即返回。</summary>
        /// <returns>工艺参数就绪返回 true</returns>
        public override bool InitializeOn()
        {
            _paramReady = LoadParameters();
            if (_paramReady)
                SetState(SubDeviceState.Connected, "工艺参数就绪");
            else
                MarkConnectFailed("工艺参数载入失败");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化连接完成，等待复位");
            return _paramReady;
        }

        /// <summary>设备断开（阻塞）：无实体设备，清标志位即返回。</summary>
        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            _paramReady = false;
            SetPhase(WeldParamControlWorkflowState.Uninitialized, "初始化断开");
            SetState(SubDeviceState.Disconnected, "焊接工艺控制初始化断开");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开完成");
            return true;
        }

        /// <summary>设备日志：输出工艺参数差异化日志（功率 / 送丝 / 速度 / 摆幅）</summary>
        public override void SubDeviceLog()
        {
            Log(string.Format("工艺控制 连接态 {0} 焊接过程态 {1} 相位 {2} 功率 {3:F1} 送丝 {4:F1} 速度 {5:F1} 摆幅 {6:F1} 缝宽 {7:F2}",
                State, WeldStatus, _phase, _output.LaserPower, _output.FeedSpeed,
                _output.RobotSpeed, _output.GalvoAmplitude, _output.SeamWidth));
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
            if (WeldStatus == SubDeviceWeldStatus.ErrorAborted
                && _phase != WeldParamControlWorkflowState.ErrorAborted)
            {
                SetPhase(WeldParamControlWorkflowState.ErrorAborted, "焊接状态切换管控");
            }
        }

        #endregion

        #region 非阻塞流程

        /// <summary>开启连接（非阻塞）：无实体设备，置 bool 变量即返回（原档 R012）。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOn()
        {
            _paramReady = true;
            SetState(SubDeviceState.Connected, "焊接工艺控制已连接");
            return true;
        }

        /// <summary>关闭连接（非阻塞）：无实体设备，清 bool 变量即返回（原档 R012）。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOff()
        {
            _paramReady = false;
            SetPhase(WeldParamControlWorkflowState.Standby, "焊接工艺控制断开");
            SetState(SubDeviceState.Disconnected, "焊接工艺控制断开");
            return true;
        }

        #endregion

        #region 可观测性

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
    }
}
