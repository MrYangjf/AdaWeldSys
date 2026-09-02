using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;
using AdaWeldSystem.EmguALG.EmguConfiger;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.ProductFileManager;

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

    /// <summary>
    /// 焊接工艺控制工作流（单例）：继承统一非泛型基类 DeviceWorkflowBase（新主控设计）。
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
    ///
    /// 新主控设计：原设备四态映射取消，对外焊接过程态统一经 SetWeldStatus(SubDeviceWeldStatus) 承载；
    /// 连接态经 ConnectOn/ConnectOff 联动 State（本流程无物理连接，Connect 表示工艺参数就绪）。
    ///
    /// 步骤驱动（ADR-047）：本流程是「被外部逐帧触发的一次性计算 + 下发」，不是自推进流程，
    /// 故采用命令变量模式 —— ComputeAndOutput 只置 _pendingCompute，业务由监听线程按
    /// 执行步 10 计算 → 20 下发 → 800 成功收尾 / 900 失败收尾 完成。
    /// 上一轮未完成时新触发会被丢弃，天然实现高频调用下的节流。
    /// </summary>
    public class WeldParamControlWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 成功收尾 / 900 失败收尾 由基类提供，子类不重复定义）
        private const int StepCompute = 10;
        private const int StepOutput = 20;

        /// <summary>自动流程单轮计算与下发的有界等待超时（毫秒，ADR-048）。</summary>
        private const int AutoFinishTimeoutMs = 10000;

        #endregion

        #region 私有变量

        /// <summary>私有相位（取代旧基类 _step，仅驱动 FlowProcess 与可观测性）。</summary>
        private WeldParamControlWorkflowState _phase = WeldParamControlWorkflowState.Uninitialized;

        /// <summary>手动连接/断开线程是否进行中（防重入）。</summary>
        private volatile bool _manualBusy;

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

        public override string StateName => Tag;

        /// <summary>工艺参数是否已就绪（等价于设备态 Connect）。</summary>
        public bool IsParamReady => _phase == WeldParamControlWorkflowState.Standby;

        /// <summary>连续失败次数（直通 WeldProcess）。</summary>
        public int ConsecutiveFailureCount => WeldProcess.Instance.ConsecutiveFailureCount;

        /// <summary>最近一次计算结果（类级复用实例，供 UI 折线图读取）。</summary>
        public WeldParamOutput LastOutput => _output;

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
        private static SubDeviceWeldStatus MapWeldStatus(WeldParamControlWorkflowState step)
        {
            switch (step)
            {
                case WeldParamControlWorkflowState.Standby:
                case WeldParamControlWorkflowState.Uninitialized:
                    return SubDeviceWeldStatus.Standby;
                case WeldParamControlWorkflowState.ErrorAborted:
                    return SubDeviceWeldStatus.ErrorAborted;
                default:
                    // Initializing / Computing / Outputting / Holding 均为业务进程中
                    return SubDeviceWeldStatus.Working;
            }
        }

        /// <summary>刷新相位并映射为对外焊接过程态。</summary>
        /// <remarks>等价旧基类 SetStep + MapToDeviceState + OnStepChanged：Computing→StepCompute，Outputting→StepOutput，其余→StepIdle。</remarks>
        /// <param name="step">目标相位</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetStep(WeldParamControlWorkflowState step, string reason)
        {
            _phase = step;
            SetWeldStatus(MapWeldStatus(step), reason);
            switch (step)
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
        /// <remarks>置 Alarm 须经 SetWeldStatus 映射（禁手工赋值 State），再记日志与故障记录后回待机。</remarks>
        private void DoFinishFail()
        {
            SetStep(WeldParamControlWorkflowState.ErrorAborted, FailReason);
            IsAlarm = true;
            FaultRecoveryManager.Instance.RecordFault(Tag, new FaultRecord
            {
                Time = DateTime.Now,
                Device = Tag,
                State = MapWeldStatus(_phase),
                Category = FaultCategory.Process,
                ErrorCode = "WeldParamStepFail",
                ParamSnapshot = string.Format("WorkStep={0} Reason={1}", WorkStep, FailReason),
                AutoRecovered = false,
                RecoveryAction = "重新载入工艺参数后复位"
            });
            Log(string.Format("焊接工艺控制异常终止 原因 {0}", FailReason), MessageLevel.Error);
            GoStep(StepIdle);
        }

        #endregion

        #region 公共方法

        /// <summary>载入工艺参数并进入待机（对应焊接过程态 Standby）。</summary>
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
        /// 本轮焊接结束，回到待机（对应焊接过程态 Standby）。
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

        #region 执行步分派

        /// <summary>单步分派，由监听线程节拍驱动。</summary>
        /// <remarks>全部 case 无 Thread.Sleep / while 轮询。</remarks>
        public  override void FlowProcess()
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

        #region 抽象生命周期方法（新主控设计 6 契约）

        /// <summary>开启连接并载入工艺参数。</summary>
        /// <returns>受理返回 true，忙线返回 false</returns>
        public override bool ConnectOn()
        {
            if (_manualBusy) return false;
            _manualBusy = true;
            var t = new Thread(() =>
            {
                try
                {
                    Log("手动连接开始", MessageLevel.Info);
                    bool ok = LoadParameters();
                    SetState(ok ? SubDeviceState.Connected : SubDeviceState.Disconnected,
                        ok ? "工艺参数就绪" : "工艺参数载入失败");
                    Log("手动连接完成", MessageLevel.Info);
                }
                catch (Exception ex)
                {
                    Log("手动连接异常 " + ex.Message, MessageLevel.Info);
                }
                finally
                {
                    _manualBusy = false;
                }
            })
            {
                Name = "WeldParamManualConnect",
                IsBackground = true
            };
            t.Start();
            return true;
        }

        /// <summary>关闭连接：停止工艺计算并回待机（非阻塞）。</summary>
        public override void ConnectOff()
        {
            if (_manualBusy) return;
            _manualBusy = true;
            var t = new Thread(() =>
            {
                try
                {
                    Log("手动断开开始", MessageLevel.Info);
                    SetStep(WeldParamControlWorkflowState.Standby, "手动停止焊接工艺控制");
                    SetState(SubDeviceState.Disconnected, "焊接工艺控制断开");
                    Log("手动断开完成", MessageLevel.Info);
                }
                catch (Exception ex)
                {
                    Log("手动断开异常 " + ex.Message, MessageLevel.Info);
                }
                finally
                {
                    _manualBusy = false;
                }
            })
            {
                Name = "WeldParamManualDisconnect",
                IsBackground = true
            };
            t.Start();
        }

        /// <summary>流程复位：清触发标记与失败计数，回到待机。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ResetProcess()
        {
            _pendingCompute = false;
            IsAlarm = false;
            ResetWorkStep();
            SetStep(WeldParamControlWorkflowState.Standby, "流程复位");
            return true;
        }

        /// <summary>清除报警态并回就绪。</summary>
        public override void ClearStatus()
        {
            IsAlarm = false;
            ResetWorkStep();
            SetStep(WeldParamControlWorkflowState.Standby, "状态清除");
        }

        /// <summary>复位时间：重置本流程工作时间计时。</summary>
        public override void ResetWorkTime()
        {
            SetWorkTimeStart();
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
