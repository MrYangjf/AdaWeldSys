using System;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{

    /// <summary>焊接工艺控制工作流（单例，无实体设备）。</summary>
    /// <remarks>消费 RobotX 与焊缝特征，算功率/送丝/振镜/速度下发。</remarks>
    public class WeldParamControlWorkflow : DeviceWorkflowBase
    {
        #region 常量

        // 执行步段位（0 待机 / 800 / 900 由基类提供）
        private const int StepCompute = 10;
        private const int StepOutput = 20;

        #endregion

        #region 私有变量

        /// <summary>无实体设备，以标志位表达连接（参数就绪）。</summary>
        private volatile bool _paramReady;

        /// <summary>外部触发一次计算与下发，由编排线程消费。</summary>
        private volatile bool _pendingCompute;

        /// <summary>最近一次计算结果输出（类级复用，避免高频分配）</summary>
        private readonly WeldParamAdaptControl _output = new WeldParamAdaptControl();

        // 失败收尾记录（子类私有字段）
        private string _failReason = "";
        private int _failStep;

        /// <summary>XL 输出支路上次输出时刻（Environment.TickCount 毫秒；差值减法对回绕免疫）</summary>
        private int _lastXlOutputTick;

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

        /// <summary>连续失败次数（直通 WeldParamManager）。</summary>
        public int ConsecutiveFailureCount { get { return WeldParamManager.Instance.ConsecutiveFailureCount; } }

        /// <summary>最近一次计算结果（类级复用实例，供 UI 折线图读取）。</summary>
        public WeldParamAdaptControl LastOutput { get { return _output; } }

        /// <summary>模组配置文件名（不含扩展名），在本流程类别目录。</summary>
        protected override string ConfigFileName { get { return "WeldParamControlWorkflow"; } }

        /// <summary>配置落 Workflow_XML 子目录。</summary>
        protected override string ConfigRelativeDir { get { return @"Config\WeldParamControl\Workflow_XML"; } }

        #endregion

        #region 构造函数

        private WeldParamControlWorkflow()
        {
            Tag = "焊接工艺控制工作流";
            // 构造即载入本流程配置；线激光引导配置由 WeldParamManager 自持，随其单例构造即就绪
            LoadConfig();
        }

        #endregion

        #region 私有函数

        /// <summary>组装工艺参数输出（功率 / 送丝 / 速度 / 振镜摆幅）。</summary>
        /// <param name="wp">焊接工艺管理器（读取最近一次调整结果）</param>
        private void FillOutput(WeldParamManager wp)
        {
            _output.LaserPower = wp.LaserPowerOutput;
            _output.FeedSpeed = wp.FeedSpeedOutput;
            _output.RobotSpeed = wp.RobotSpeedOutput;
            _output.SeamWidth = wp.CurrentSeamWidth;
            _output.SpotWidth = wp.SpotWidthOutput;
            _output.GalvoAmplitude = CalcGalvoAmplitude(wp.CurrentSeamWidth);
            _output.GalvoFrequency = CalcGalvoFrequency(wp.CurrentSeamWidth);
            _output.Timestamp = DateTime.Now;
        }

        /// <summary>组装 XL 缓存命中的工艺参数输出（线激光宽度来源，</summary>
        /// <remarks>机器人已到达的 XL 处参数）。</remarks>
        /// <param name="robotX">当拍机器人 X</param>
        /// <param name="laserPower">激光功率</param>
        /// <param name="feedSpeed">送丝速度</param>
        /// <param name="robotSpeed">焊接速度</param>
        /// <param name="spotWidth">光斑宽度</param>
        /// <param name="seamWidth">采用的焊缝宽度</param>
        private void FillXlOutput(double robotX, double laserPower, double feedSpeed,
                                  double robotSpeed, double spotWidth, double seamWidth)
        {
            _output.LaserPower = laserPower;
            _output.FeedSpeed = feedSpeed;
            _output.RobotSpeed = robotSpeed;
            _output.SpotWidth = spotWidth;
            _output.SeamWidth = seamWidth;
            // 与主支路同源，否则两条路径摆幅不一致
            _output.GalvoAmplitude = CalcGalvoAmplitude(seamWidth);
            _output.GalvoFrequency = CalcGalvoFrequency(seamWidth);
            _output.Timestamp = DateTime.Now;
        }

        /// <summary>XL 输出支路：按配置频率（默认 60Hz）节流输出工艺参数。</summary>
        /// <remarks>手动宽度源恒定输出；线激光源按已到达 XL 跟随。
        /// 下发经 SendToRobot，模拟态只更新 LastOutput。</remarks>
        private void TickXlOutput()
        {
            var wp = WeldParamManager.Instance;
            var cfg = wp.AlgorithmsConfig;
            if (cfg == null) return;

            int now = Environment.TickCount;
            int intervalMs = (int)Math.Max(1.0, 1000.0 / cfg.ParamOutputHz);
            if (now - _lastXlOutputTick < intervalMs) return;
            _lastXlOutputTick = now;

            if (cfg.SeamWidthSource == SeamWidthSource.Manual)
            {
                // 手动固定宽度：直查表输出，结果恒定
                wp.TriggerAdjustment();
                FillOutput(wp);
            }
            else
            {
                // 线激光宽度：按机器人 X 取已到达 XL 的缓存
                if (!wp.TryGetParamOutputAt(wp.CurrentRobotX, out double laserPower, out double feedSpeed,
                                            out double robotSpeed, out double spotWidth, out double seamWidth))
                {
                    return;
                }
                FillXlOutput(wp.CurrentRobotX, laserPower, feedSpeed, robotSpeed, spotWidth, seamWidth);
            }

            // 模拟态或通讯断开不发送，仅更新 LastOutput
            if (DeviceControlWork.Instance.IsSimulationEnabled)
            {
                _output.Success = true;
            }
            else
            {
                _output.Success = SendToRobot(_output);
            }
        }

        /// <summary>计算振镜摆幅宽度（查表优先、公式兜底）。</summary>
        /// <remarks>固定光斑模式：振镜不摆动，光斑恒定，返回 0。
        /// 非固定模式：按焊缝宽度查表输出摆幅。
        /// ①摆幅宽度表：公式初值入表，可用查表值。
        /// ②无档位记录按公式兜底「系数 × 焊缝宽」。
        /// 摆幅不参与 Y 向引导闭环，禁引入时间域低通。</remarks>
        /// <param name="seamWidthMm">采用的焊缝识别宽度（毫米）</param>
        /// <returns>摆幅宽度（毫米）</returns>
        private double CalcGalvoAmplitude(double seamWidthMm)
        {
            WeldParamManager manager = WeldParamManager.Instance;
            WeldAlgorithmsConfig cfg = manager.AlgorithmsConfig;
            if (cfg == null)
            {
                return 0.0;
            }

            // 固定输出光斑模式：不驱动振镜摆动
            if (!cfg.EnableGalvoWeave)
            {
                return 0.0;
            }

            // 非固定模式：最终输出用查表值
            if (manager.TryLookupSwingWidth(seamWidthMm, out double swingWidth))
            {
                return swingWidth;
            }

            // 公式兜底：摆幅宽度 = 光斑摆幅系数 × 焊缝宽
            AutoParam autoParam = manager.CurrentAutoParam;
            double gain = autoParam != null && autoParam.SpotSwingGain > 0.0
                ? autoParam.SpotSwingGain
                : AutoParam.DefaultSpotSwingGain;
            return seamWidthMm * gain;
        }

        /// <summary>计算振镜摆幅频率（查表优先、公式兜底）。</summary>
        /// <remarks>摆幅越大频率越低（2mm→500Hz、10mm→200Hz）。
        /// ①摆幅频率表按焊缝宽度查，初值可核对。
        /// ②无档位记录按基准公式反算。
        /// 固定模式返回协议默认频率，由下发侧不使能。</remarks>
        /// <param name="seamWidthMm">采用的焊缝识别宽度（毫米）</param>
        /// <returns>摆幅频率（Hz）</returns>
        private int CalcGalvoFrequency(double seamWidthMm)
        {
            WeldParamManager manager = WeldParamManager.Instance;

            // 查表（公式初值 + 手动核对）
            if (manager.TryLookupSwingFrequency(seamWidthMm, out int tableHz) && tableHz > 0)
            {
                return tableHz;
            }

            // 公式兜底：按该档摆幅宽度基准反算
            AutoParam autoParam = manager.CurrentAutoParam;
            if (autoParam == null)
            {
                return AutoParam.DefaultSwingFreqHz1;
            }

            double gain = autoParam.SpotSwingGain > 0.0
                ? autoParam.SpotSwingGain
                : AutoParam.DefaultSpotSwingGain;
            return autoParam.CalSwingFrequency(seamWidthMm * gain);
        }

        /// <summary>下发工艺参数至机器人（RSI 具名元素通道：5 个数值通</summary>
        /// <remarks>注：连接本身由顶层 Comm（CommunicationManager）管控，此处只做数据下发。
        /// RSI 为唯一通道，指令与参数全部数值化。文本协议已废止。
        /// 通道对应：WeldPower(1) / WireSpeed(2) / CutWire(3) / WeldSpeed(4) / WeaveAmp(5)。 道）。</remarks>
        /// <param name="output">待下发的工艺参数</param>
        /// <returns>下发成功返回 true</returns>
        private bool SendToRobot(WeldParamAdaptControl output)
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null || !comm.IsRobotEnabled)
                {
                    // 机器人未启用时视为无需下发，不判定为失败
                    return true;
                }

                // 送丝速度经单一组装点并入 X 向送丝补偿，防冲突
                double feedSpeed = DeviceControlWork.Instance.ComposeWireFeedSpeed(output.FeedSpeed);

                comm.UpdateRobotWeldParams(
                    output.LaserPower,      // WeldPower（激光功率 W）
                    feedSpeed,             // WireSpeed（送丝速度 m/min）
                    0.0,                   // CutWire（剪丝信号由剪丝指令通道单独触发）
                    output.RobotSpeed,     // WeldSpeed（焊接速度 mm/s）
                    output.GalvoAmplitude  // WeaveAmp（振镜摆幅 mm）
                );
                return true;
            }
            catch (Exception ex)
            {
                Log("工艺参数下发异常 " + ex.Message, MessageLevel.Warning);
                return false;
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

        /// <summary>执行步 10：工艺参数计算。</summary>
        /// <remarks>输入 RobotX 与焊缝特征，输出功率/送丝/速度/摆幅。</remarks>
        /// <returns>进入下发阶段返回 true，进入保持态或失败返回 false</returns>
        private bool DoCompute()
        {
            SetWeldStatus(SubDeviceWeldStatus.Working, "焊接工艺参数计算中");
            var wp = WeldParamManager.Instance;

            if (wp.CurrentAutoParam == null)
            {
                Fail("未载入工艺参数，无法计算");
                return false;
            }

            // 连续失败超阈值：进入保持态，不再计算
            if (wp.ConsecutiveFailureCount >= wp.MaxConsecutiveFailures)
            {
                SetWeldStatus(SubDeviceWeldStatus.Standby, string.Format(
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
            // 模拟态只记录不下发，避免误发真机器人
            if (DeviceControlWork.Instance.IsSimulationEnabled)
            {
                _output.Success = true;
                return true;
            }

            bool sent = SendToRobot(_output);
            _output.Success = sent;
            if (!sent)
            {
                Fail("焊接工艺参数下发失败");
                return false;
            }
            return true;
        }

        /// <summary>执行步 800：成功收尾（结果写 _output，订阅方读 LastOutput）。</summary>
        /// <remarks>焊接中不得回落 Standby，否则击穿消费门禁。
        /// 仅当不在焊接过程态才回落待机。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishOk()
        {
            AdvanceStep(StepIdle);
            if (WeldStatus != SubDeviceWeldStatus.Working)
            {
                SetWeldStatus(SubDeviceWeldStatus.Standby, "本轮工艺调整完成");
            }
            return true;
        }

        /// <summary>执行步 900：失败收尾三步走。</summary>
        /// <remarks>置 Alarm 须经 SetWeldStatus，禁手工赋值 State。</remarks>
        /// <returns>收尾完成返回 true</returns>
        private bool DoFinishFail()
        {
            SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, _failReason);
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
            SetWeldStatus(SubDeviceWeldStatus.PreWork, "焊接工艺参数载入开始");
            try
            {
                WeldParamManager.Instance.ReloadCurrentParamValues();

                var wp = WeldParamManager.Instance;
                if (wp.CurrentAutoParam == null)
                {
                    SetWeldStatus(SubDeviceWeldStatus.NoReset, "未载入有效工艺参数");
                    return false;
                }

                SetWeldStatus(SubDeviceWeldStatus.Standby, "焊接工艺参数就绪");
                return true;
            }
            catch (Exception ex)
            {
                SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "工艺参数载入异常 " + ex.Message);
                return false;
            }
        }

        /// <summary>焊前预置：起焊前先输出一次工艺参数。</summary>
        /// <remarks>归属主控焊前检查步骤，真实与模拟同入口。
        /// 两种宽度源都须输出，线激光源用基准兜底。
        /// 避免起焊瞬间参数为空引发事故。
        /// 同步只跑一拍，不引入新线程。</remarks>
        /// <returns>预置成功返回 true</returns>
        public bool PreWork()
        {
            if (!IsEnable)
            {
                return false;
            }

            // 报警停焊期间拒绝焊前预置
            if (DeviceControlWork.Instance.IsAlarmstop)
            {
                Log("焊前预置被拒绝 当前为报警停焊状态", MessageLevel.Warning);
                return false;
            }

            var wp = WeldParamManager.Instance;
            if (wp.CurrentAutoParam == null)
            {
                SetWeldStatus(SubDeviceWeldStatus.NoReset, "焊前预置失败 未载入工艺参数");
                Log("焊前预置失败 未载入工艺参数", MessageLevel.Error);
                return false;
            }

            if (wp.ConsecutiveFailureCount >= wp.MaxConsecutiveFailures)
            {
                SetWeldStatus(SubDeviceWeldStatus.Standby, string.Format(
                    "连续失败 {0} 次达到阈值 {1} 焊前预置暂停",
                    wp.ConsecutiveFailureCount, wp.MaxConsecutiveFailures));
                return false;
            }

            wp.TriggerAdjustment();
            FillOutput(wp);

            if (DeviceControlWork.Instance.IsSimulationEnabled)
            {
                _output.Success = true;
            }
            else
            {
                _output.Success = SendToRobot(_output);
            }

            Log(string.Format(
                "焊前预置完成 宽度 {0:F3}mm 功率 {1:F0} 送丝 {2:F0} 焊接速度 {3:F0} 光斑 {4:F3}",
                _output.SeamWidth, _output.LaserPower, _output.FeedSpeed,
                _output.RobotSpeed, _output.SpotWidth),
                _output.Success ? MessageLevel.Info : MessageLevel.Warning);
            return _output.Success;
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
            SetWeldStatus(SubDeviceWeldStatus.Standby, "本轮焊接完成，工艺控制回到待机");
        }

        /// <summary>切换工艺配置并重新载入参数。</summary>
        /// <param name="configId">工艺配置标识</param>
        /// <returns>切换并载入成功返回 true</returns>
        public bool SwitchParameter(string configId)
        {
            try
            {
                WeldParamManager.Instance.SwitchConfig(configId);
                return LoadParameters();
            }
            catch (Exception ex)
            {
                SetWeldStatus(SubDeviceWeldStatus.ErrorAborted, "切换工艺配置异常 " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 常驻流程

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进。</summary>
        /// <remarks>StepIdle 内消费 _pendingCompute 触发变量；全部 case 无 Thread.Sleep / while 轮询。</remarks>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            // 报警停焊 不再推进计算与下发
            if (WeldStatus == SubDeviceWeldStatus.ErrorAborted || DeviceControlWork.Instance.IsAlarmstop) return;

            switch (RunStep)
            {
                case StepCompute:
                    if (DoCompute()) SetWeldStatus(SubDeviceWeldStatus.Working, "焊接工艺参数下发中");
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
                    // 触发门禁：仅 Working 才消费，起止由主控控制
                    if (WeldStatus != SubDeviceWeldStatus.Working)
                    {
                        break;
                    }

                    if (_pendingCompute)
                    {
                        _pendingCompute = false;
                        AdvanceStep(StepCompute);
                        break;
                    }

                    // XL 输出与调整解耦：按配置频率输出，不受滞留阻塞
                    // 不得并入触发分支，否则输出支路不可达
                    TickXlOutput();
                    break;
            }
        }

        /// <summary>流程复位：清触发标记与失败计数，回到待机（线性</summary>
        /// <remarks>阻塞链）。</remarks>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            _pendingCompute = false;
            IsAlarm = false;
            if (!LoadParameters()) return false;
            SetWeldStatus(SubDeviceWeldStatus.Standby, "工艺控制复位完成");
            return true;
        }

        /// <summary>状态清理：标志位全复位 + 执行步归零 + 强制未复位</summary>
        /// <remarks>态。</remarks>
        public override void ClearStatus()
        {
            IsAlarm = false;
            _pendingCompute = false;
            _failReason = "";
            _failStep = 0;
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入</summary>
        /// <remarks>步骤时长导致异常超时。</remarks>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：无实体设备，载入工艺参数后置位</summary>
        /// <remarks>即返回。</remarks>
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
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开");
            SetState(SubDeviceState.Disconnected, "焊接工艺控制初始化断开");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开完成");
            return true;
        }

        /// <summary>设备日志：输出工艺参数差异化日志（功率 / 送丝 / 速</summary>
        /// <remarks>度 / 摆幅）</remarks>
        public override void SubDeviceLog()
        {
            Log(string.Format("工艺控制 连接态 {0} 焊接过程态 {1} 相位 {2} 功率 {3:F1} 送丝 {4:F1} 速度 {5:F1} 摆幅 {6:F1} 缝宽 {7:F2} 光斑 {8:F2}",
                State, WeldStatus, WorkStepName, _output.LaserPower, _output.FeedSpeed,
                _output.RobotSpeed, _output.GalvoAmplitude, _output.SeamWidth, _output.SpotWidth));
        }

        /// <summary>加载本模组的配置。</summary>
        public override void LoadConfig()
        {
            TimeOutSeconds = ReadInt("TimeOutSeconds", 30);
        }

        /// <summary>保存本模组的配置，末尾落盘。</summary>
        public override void SaveConfig()
        {
            Config.SetElementValue("TimeOutSeconds", TimeOutSeconds.ToString());
            Config.SaveXdocument();
        }

        /// <summary>焊接工作状态切换管控。</summary>
        /// <remarks>整机级联迁移后由编排入口调用。
        /// 历史双源判据已消解，WeldStatus 为唯一状态源。级联写入即已到位。
        /// ErrorAborted 下输出停走由消费门禁承担（WeldStatus != Working），本流程无步号入口。</remarks>
        public override void WeldStatusTrans()
        {
            if (WeldStatus == SubDeviceWeldStatus.NoReset) return;
        }

        #endregion

        #region 非阻塞动作指令

        /// <summary>开启连接（非阻塞）：无实体设备，置 bool 变量即返回。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOn()
        {
            _paramReady = true;
            SetState(SubDeviceState.Connected, "焊接工艺控制已连接");
            return true;
        }

        /// <summary>关闭连接（非阻塞）：无实体设备，清 bool 变量即返回。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOff()
        {
            _paramReady = false;
            SetWeldStatus(SubDeviceWeldStatus.Standby, "焊接工艺控制断开");
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
