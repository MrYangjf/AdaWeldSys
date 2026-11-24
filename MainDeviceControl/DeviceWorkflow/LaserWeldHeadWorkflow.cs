using System;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.WeldParamControl;
using System.Threading;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{

    /// <summary>焊接头工作流（骨架）。</summary>
    public class LaserWeldHeadWorkflow : DeviceWorkflowBase
    {
        #region 常量


        /// <summary>执行步段位（10 振镜参数下发）</summary>
        private const int StepGalvo = 10;

        /// <summary>执行步段位（20 LA 随动下发）</summary>
        private const int StepLaFollow = 20;

        #endregion

        #region 私有变量

        /// <summary>上次已下发的振镜摆幅</summary>
        private double _lastGalvoAmplitudeMm = double.NaN;

        /// <summary>最近温度上报（保护镜/环境/温升/气压），供 UI 读取。</summary>
        private LaserHeadTemperatureData _lastTemperature;

        /// <summary>最近从站报警状态位，供判据与 UI 读取。</summary>
        private HeadAlarmStatus _lastAlarmStatus;

        /// <summary>LA 是否处于 auto 随动态（按 TA 位置补偿保持光斑恒定）</summary>
        private bool _laFollowActive;

        /// <summary>目标光斑对应的 LA 基准位置（毫米，曲线A 反查 = TA=0 时的 LA 位）</summary>
        private double _laBaseMm;

        /// <summary>当前目标光斑大小（毫米）；初值取曲线A 锚点</summary>
        private double _targetSpotMm;

        /// <summary>上次已下发的 LA 目标位置</summary>
        private double _lastLaTargetMm = double.NaN;

        /// <summary>上次已下发的振镜频率</summary>
        private int _lastGalvoFreqHz = -1;

        /// <summary>光丝对中补偿后的振镜 X 中心（mm，单写口 ApplyGalvoCenterOffset）</summary>
        private double _galvoCenterXmm;

        /// <summary>光丝对中补偿后的振镜 Y 中心（mm）</summary>
        private double _galvoCenterYmm;

        /// <summary>上次已下发的振镜 X 中心（mm）</summary>
        private double _lastGalvoCenterXmm = double.NaN;

        /// <summary>上次已下发的振镜 Y 中心（mm）</summary>
        private double _lastGalvoCenterYmm = double.NaN;

        #endregion

        #region 单例

        private static readonly Lazy<LaserWeldHeadWorkflow> _lazyInstance =
            new Lazy<LaserWeldHeadWorkflow>(() => new LaserWeldHeadWorkflow());

        /// <summary>焊接头工作流单例。</summary>
        public static LaserWeldHeadWorkflow Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 公共变量

        private Action _axisReadyHandler;
        private bool _axisReadyWired;

        /// <summary>设备名称（日志/事件标识）。</summary>
        public override string StateName { get { return Tag; } }

        /// <summary>模组配置文件名（不含扩展名），在本流程类别目录。</summary>
        protected override string ConfigFileName { get { return "LaserWeldHeadWorkflow"; } }

        /// <summary>模组配置所属模块（全局配置目录下的模块层）。</summary>
        protected override string ConfigModule { get { return "LaserWeldHead"; } }

        /// <summary>最近一次温度上报（只读；未上报过为 null）。</summary>
        public LaserHeadTemperatureData LastTemperature { get { return _lastTemperature; } }

        /// <summary>最近一次从站报警状态（只读；未读过为 null）。</summary>
        public HeadAlarmStatus LastAlarmStatus { get { return _lastAlarmStatus; } }

        /// <summary>当前温度是否处于报警态（保护镜温度 ≥ 报警阈值）。</summary>
        public bool IsTemperatureAlarm
        {
            get
            {
                double limit = MontionManager.Instance.WeldHeadConfig.TempAlarmThreshold;
                return _lastTemperature != null && limit > 0.0 && _lastTemperature.ProtectiveLensTemp >= limit;
            }
        }

        /// <summary>LA 轴就绪变化（按轴过滤），UI 订阅后刷新。</summary>
        public event Action AxisReadyChanged
        {
            add
            {
                if (!_axisReadyWired)
                {
                    _axisReadyWired = true;
                    MontionManager.Instance.AxisReadinessChanged += OnAxisReadinessChanged;
                }
                _axisReadyHandler += value;
            }
            remove
            {
                _axisReadyHandler -= value;
            }
        }

        /// <summary>LA（焊接头从站：对焦电机 / 振镜）流程层就绪。</summary>
        public bool LaReady { get { return MontionManager.Instance.LaReady; } }

        /// <summary>LA 不可用原因（就绪返回空串）。</summary>
        public string LaGateReason { get { return MontionManager.Instance.LaGateReason; } }

        /// <summary>TA 轴已使能（L1 前提）。</summary>
        public override bool IsEnabled { get { return MontionManager.Instance.TaMotionReady; } }

        /// <summary>TA 轴已回零（L3 前提）。</summary>
        public override bool IsHomed { get { return MontionManager.Instance.TaHomed; } }

        /// <summary>TA 已在工作零位（L4）。</summary>
        public override bool IsAtWorkZero { get { return MontionManager.Instance.TaAtWorkZero; } }

        /// <summary>TA 轴需回零后位置才可信。</summary>
        protected override bool NeedHoming { get { return true; } }

        /// <summary>工作零位来源：工艺设置页 TA_ZERO_POSE_MM。</summary>
        public override string WorkZeroSource { get { return "TA_ZERO_POSE_MM"; } }

        /// <summary>TA 不可用原因（就绪返回空串）。</summary>
        public override string ReadinessReason { get { return MontionManager.Instance.TaDegradedReason; } }

        /// <summary>LA 是否处于 auto 随动态（只读，供 UI 显示动作位）。</summary>
        public bool LaFollowActive { get { return _laFollowActive; } }

        /// <summary>当前目标光斑大小（毫米，只读）。</summary>
        public double TargetSpotMm { get { return _targetSpotMm; } }

        private void OnAxisReadinessChanged(string axisName)
        {
            if (!(axisName == "LA" || axisName == "ALL")) return;
            _axisReadyHandler?.Invoke();
        }

        #endregion

        #region 构造函数

        private LaserWeldHeadWorkflow()
        {
            Tag = "焊接头工作流";
            // 目标光斑初值 = 曲线A 的 LA=0 锚点光斑
            _targetSpotMm = WeldParamManager.Instance.AnchorSpotMm;
            SubscribeWeldHeadEvents();
        }

        /// <summary>订阅焊接头温度与报警事件（断开时退订）。</summary>
        private void SubscribeWeldHeadEvents()
        {
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null) return;
            weldHead.TemperatureAlarm += OnWeldHeadTemperatureAlarm;
            weldHead.AlarmTriggered += OnWeldHeadAlarmTriggered;
        }

        /// <summary>退订焊接头从站事件（Dispose 时调用）。</summary>
        private void UnsubscribeWeldHeadEvents()
        {
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null) return;
            weldHead.TemperatureAlarm -= OnWeldHeadTemperatureAlarm;
            weldHead.AlarmTriggered -= OnWeldHeadAlarmTriggered;
        }

        /// <summary>温度越阈上报：记保护镜温度与温升，超阈报警。</summary>

        /// <param name="sender">事件源（WeldHeadSlaveDevice）。</param>
        /// <param name="data">本次上报的温度气压数据。</param>
        private void OnWeldHeadTemperatureAlarm(object sender, LaserHeadTemperatureData data)
        {
            if (data == null) return;
            _lastTemperature = data;
            Log(string.Format("焊接头温度 保护镜 {0:F1} 环境 {1:F1} 温升 {2:F1} 切割气压 {3:F2}",
                data.ProtectiveLensTemp, data.ProtectiveLensAmbientTemp,
                data.ProtectiveLensRiseTemp, data.CuttingAirPressure));

            // 本侧二次判阈：从站独立报警之外再兜底
            double warnLimit = MontionManager.Instance.WeldHeadConfig.TempWarnThreshold;
            double alarmLimit = MontionManager.Instance.WeldHeadConfig.TempAlarmThreshold;
            if (alarmLimit > 0.0 && data.ProtectiveLensTemp >= alarmLimit)
            {
                Log(WeldHeadSlaveDevice.DescribeTemperatureThreshold(data.ProtectiveLensTemp, alarmLimit, true),
                    MessageLevel.Error);
                SetState(SubDeviceState.Alarm, "保护镜温度超报警阈值");
            }
            else if (warnLimit > 0.0 && data.ProtectiveLensTemp >= warnLimit)
            {
                Log(WeldHeadSlaveDevice.DescribeTemperatureThreshold(data.ProtectiveLensTemp, warnLimit, false),
                    MessageLevel.Warning);
            }
        }

        /// <summary>从站报警位升起：按位输出报警原因。</summary>
        /// <param name="sender">事件源（WeldHeadSlaveDevice）。</param>
        /// <param name="status">当前报警状态位。</param>
        private void OnWeldHeadAlarmTriggered(object sender, HeadAlarmStatus status)
        {
            if (status == null) return;
            _lastAlarmStatus = status;
            Log(status.Describe(), MessageLevel.Error);
            SetState(SubDeviceState.Alarm, status.Describe());
        }

        #endregion

        /// <summary>释放托管资源：退订焊接头从站事件（基类 Dispose 调此钩</summary>
        protected override void DisposeManaged()
        {
            UnsubscribeWeldHeadEvents();
            base.DisposeManaged();
        }

        #region 私有函数

        /// <summary>按当前摆幅下发振镜参数（值未变则跳过，避免逐</summary>
        private void ApplyGalvoAmplitude()
        {
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null)
            {
                return;
            }

            // 非焊接态不沿用旧摆幅 直接归零
            WeldParamAdaptControl output = WeldStatus == SubDeviceWeldStatus.Working
                ? WeldParamControlWorkflow.Instance.LastOutput
                : null;
            double amplitude = (IsGalvoWeaveEnabled() && output != null) ? output.GalvoAmplitude : 0.0;
            if (amplitude < 0.0)
            {
                amplitude = 0.0;
            }

            // 频率随摆幅自适应：按焊缝宽查「摆幅频率」表
            int frequency = GalvoParams.CreateLogicalHome().Frequency;
            if (output != null && output.GalvoFrequency > 0)
            {
                frequency = output.GalvoFrequency;
            }

            // 摆幅/频率/中心三量全未变才跳过重发
            if (!double.IsNaN(_lastGalvoAmplitudeMm) && Math.Abs(amplitude - _lastGalvoAmplitudeMm) < 0.01
                && frequency == _lastGalvoFreqHz
                && Math.Abs(_galvoCenterXmm - _lastGalvoCenterXmm) < 0.01
                && Math.Abs(_galvoCenterYmm - _lastGalvoCenterYmm) < 0.01)
            {
                return;
            }

            // 变量名避开 C# 关键字
            GalvoParams galvo = GalvoParams.CreateLogicalHome();
            galvo.Width = amplitude;
            galvo.Length = amplitude;
            galvo.GalvoEnable = amplitude > 0.0;
            // 未设图形按直线兜底
            WeldParamConfig algoCfg = WeldParamManager.Instance.AlgorithmsConfig;
            galvo.Pattern = ResolveGalvoPattern(algoCfg != null ? algoCfg.GalvoPattern : 0);
            galvo.Frequency = frequency;
            galvo.XCenter = _galvoCenterXmm;
            galvo.YCenter = _galvoCenterYmm;

            if (weldHead.SetGalvoParams(galvo))
            {
                _lastGalvoAmplitudeMm = amplitude;
                _lastGalvoFreqHz = frequency;
                _lastGalvoCenterXmm = _galvoCenterXmm;
                _lastGalvoCenterYmm = _galvoCenterYmm;
            }
        }

        /// <summary>配置图形值 → 设备协议图形：0</summary>

        /// <param name="configValue">配置中的图形编号</param>
        /// <returns>可下发的协议图形</returns>
        public static WsgGalvoPattern ResolveGalvoPattern(int configValue)
        {
            if (configValue < (int)WsgGalvoPattern.Circle || configValue > (int)WsgGalvoPattern.Arc)
            {
                return WsgGalvoPattern.Line;
            }
            return (WsgGalvoPattern)configValue;
        }

        /// <summary>振镜摆动是否启用（实时读配置真相源）。</summary>

        /// <returns>true = 已启用振镜跟随摆动工艺</returns>
        public static bool IsGalvoWeaveEnabled()
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            return cfg != null && cfg.EnableGalvoWeave;
        }

        /// <summary>LA 目标位置下发（毫米，随动与 Spot/Pose 共用；0.01mm 死区防刷总线）。</summary>
        /// <param name="posMm">目标位置（毫米）</param>
        /// <param name="actionName">动作名（日志用）</param>
        /// <param name="verbose">是否逐次记日志（随动步为 false 防刷屏）</param>
        /// <returns>下发成功返回 true</returns>
        private bool LaMoveTo(double posMm, string actionName, bool verbose)
        {
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null)
            {
                if (verbose) Log(actionName + " 失败 焊接头从站未就绪", MessageLevel.Error);
                return false;
            }
            if (!double.IsNaN(_lastLaTargetMm) && Math.Abs(posMm - _lastLaTargetMm) < 0.01) return true;
            bool ok = weldHead.SetMotorPosEnable(posMm, true);
            if (ok)
            {
                _lastLaTargetMm = posMm;
                if (verbose) Log(string.Format("{0} LA 目标 {1:F3}mm", actionName, posMm));
            }
            else if (verbose)
            {
                Log(actionName + " LA 下发失败", MessageLevel.Error);
            }
            return ok;
        }

        /// <summary>LA auto 随动一步：LA 目标 = 光斑基准位 + 曲线B 按 TA 位置的偏移补</summary>
        private void ApplyLaFollow()
        {
            double taMm = MotionControlWorkflow.Instance.TaPositionMm;
            double offset = WeldParamManager.Instance.TaLaCurve.QueryLaOffsetMm(taMm);
            if (double.IsNaN(offset))
            {
                Log("LA 随动跳过 TA 偏移超出曲线B 定义域（外推策略 Reject）", MessageLevel.Warning);
                return;
            }
            LaMoveTo(_laBaseMm + offset, "LA 随动", false);
        }

        /// <summary>设目标光斑大小：曲线A 反查基准位，叠加当前 TA 偏移</summary>

        /// <param name="spotMm">目标光斑大小（毫米）</param>
        /// <param name="actionName">动作名（日志用）</param>
        /// <returns>受理并下发成功返回 true</returns>
        private bool LaApplySpot(double spotMm, string actionName)
        {
            double baseMm = WeldParamManager.Instance.TaLaCurve.QueryLaPositionMm(spotMm);
            if (double.IsNaN(baseMm))
            {
                Log(actionName + " 失败 目标光斑超出曲线A 定义域（外推策略 Reject）", MessageLevel.Warning);
                return false;
            }

            MontionManager mgr = MontionManager.Instance;
            if (!mgr.TryBeginAxisInstruction("LA", actionName, out CancellationToken token, out string rejectReason))
            {
                Log(actionName + " 被拒 " + rejectReason, MessageLevel.Warning);
                return false;
            }

            try
            {
                _laFollowActive = false;
                _targetSpotMm = spotMm;
                _laBaseMm = baseMm;
                if (!LaMoveTo(baseMm, actionName, true))
                {
                    Log(actionName + " LA 下发失败，未进入状态检测", MessageLevel.Error);
                    return false;
                }
                ApplyLaFollow();
                // _lastLaTargetMm 只在真正下发时更新；若因死区未下发（可能仍为 NaN），回落到基准位校验
                double detectTargetMm = double.IsNaN(_lastLaTargetMm) ? baseMm : _lastLaTargetMm;
                Log(string.Format("{0} 目标光斑 {1:F3}mm 基准位 {2:F3}mm 检测目标 {3:F3}mm 已下发，进入状态检测",
                    actionName, spotMm, baseMm, detectTargetMm));

                bool ok = mgr.WaitLaMotorAtTarget(detectTargetMm, token, out string evidence);
                Log(actionName + " 状态检测 " + (ok ? "达成 " : "失败 ") + evidence,
                    ok ? MessageLevel.Info : MessageLevel.Error);
                return ok;
            }
            finally
            {
                mgr.EndAxisInstruction("LA");
            }
        }

        /// <summary>LA 调试位移动（Pose 共用）：退出随动后直接下发目标位，不</summary>

        /// <param name="posMm">目标位置（毫米）</param>
        /// <param name="actionName">动作名（日志用）</param>
        /// <returns>下发成功返回 true</returns>
        private bool LaMoveToPose(double posMm, string actionName)
        {
            MontionManager mgr = MontionManager.Instance;
            if (!mgr.TryBeginAxisInstruction("LA", actionName, out CancellationToken token, out string rejectReason))
            {
                Log(actionName + " 被拒 " + rejectReason, MessageLevel.Warning);
                return false;
            }

            try
            {
                _laFollowActive = false;
                if (!LaMoveTo(posMm, actionName, true))
                {
                    Log(actionName + " LA 下发失败，未进入状态检测", MessageLevel.Error);
                    return false;
                }
                Log(string.Format("{0} LA 目标 {1:F3}mm 已下发，进入状态检测", actionName, posMm));

                bool ok = mgr.WaitLaMotorAtTarget(posMm, token, out string evidence);
                Log(actionName + " 状态检测 " + (ok ? "达成 " : "失败 ") + evidence,
                    ok ? MessageLevel.Info : MessageLevel.Error);
                return ok;
            }
            finally
            {
                mgr.EndAxisInstruction("LA");
            }
        }

        #endregion

        #region 常驻流程

        /// <summary>流程处理：按步驱动振镜参数运行期下发（摆幅随</summary>
        public override void FlowProcess()
        {
            if (!IsEnable) return;

            switch (RunStep)
            {
                case StepIdle:
                    AdvanceStep(StepGalvo);
                    break;

                case StepGalvo:
                    ApplyGalvoAmplitude();
                    AdvanceStep(StepLaFollow);
                    break;

                case StepLaFollow:
                    if (_laFollowActive) ApplyLaFollow();
                    AdvanceStep(StepIdle);
                    break;
            }
        }

        /// <summary>复位流程：线性阻塞链，非步控。</summary>
        /// <returns>复位成功返回 true</returns>
        public override bool ResetProcess()
        {
            ClearStatus();
            SetWeldStatus(SubDeviceWeldStatus.Standby, "流程复位");
            // 焊接头回零位（LA / 振镜）尚未实现：机械回零动作待现场方案确定后接入，
            // 此处仅把振镜退回逻辑零位并记录待办，不臆造 LA 机械回零。
            GalvoHomeForReset();
            return true;
        }

        /// <summary>复位时振镜退回逻辑零位（LA 机械回零待实现）。</summary>
        private void GalvoHomeForReset()
        {
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null || !weldHead.IsInitialized)
            {
                Log("焊接头复位 振镜回逻辑零位跳过 焊接头从站未就绪", MessageLevel.Warning);
                return;
            }
            _galvoCenterXmm = 0.0;
            _galvoCenterYmm = 0.0;
            _lastGalvoAmplitudeMm = double.NaN;
            _lastGalvoFreqHz = -1;
            _lastGalvoCenterXmm = double.NaN;
            _lastGalvoCenterYmm = double.NaN;
            bool ok = weldHead.SetGalvoParams(GalvoParams.CreateLogicalHome());
            Log(ok
                ? "焊接头复位 振镜已回逻辑零位 LA 机械回零待实现"
                : "焊接头复位 振镜回逻辑零位失败 LA 机械回零待实现",
                ok ? MessageLevel.Info : MessageLevel.Warning);
        }

        /// <summary>状态清理：复位标志与执行步。</summary>
        public override void ClearStatus()
        {
            AdvanceStep(StepIdle);
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入</summary>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：无实体设备，仅切换状态机</summary>

        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            SetState(SubDeviceState.Connected, "焊接头初始化连接");
            SetWeldStatus(SubDeviceWeldStatus.Standby, "初始化连接完成");
            return true;
        }

        /// <summary>设备断开（阻塞）：无实体设备，仅切换状态机</summary>

        /// <returns>断开成功返回 true</returns>
        public override bool InitializeOff()
        {
            SetState(SubDeviceState.Disconnected, "焊接头初始化断开");
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "初始化断开完成");
            return true;
        }

        /// <summary>设备日志：输出焊接头差异化日志（温度阈值）。</summary>
        public override void SubDeviceLog()
        {
            // 阈值由 MontionManager 初始化下发；本工作流订阅 TemperatureAlarm/AlarmTriggered 后汇总输出温度与报警态
            Log(string.Format("焊接头 相位 {0} 连接态 {1} 焊接过程态 {2} 温度报警 {3}",
                WorkStepName, State, WeldStatus, IsTemperatureAlarm));

            if (_lastTemperature != null)
            {
                Log(string.Format("保护镜温度 {0:F1} 环境 {1:F1} 温升 {2:F1} 切割气压 {3:F2}",
                    _lastTemperature.ProtectiveLensTemp,
                    _lastTemperature.ProtectiveLensAmbientTemp,
                    _lastTemperature.ProtectiveLensRiseTemp,
                    _lastTemperature.CuttingAirPressure));
            }
        }

        /// <summary>加载本模组的配置。</summary>
        public override void LoadConfig()
        {
            TimeOutSeconds = ReadInt("TimeOutSeconds", 30);

            // 目标光斑取焊参配置
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg != null)
            {
                if (cfg.LaSpotTargetMm > 0.0) _targetSpotMm = cfg.LaSpotTargetMm;
            }
            _laBaseMm = WeldParamManager.Instance.TaLaCurve.QueryLaPositionMm(_targetSpotMm);
            if (double.IsNaN(_laBaseMm)) _laBaseMm = 0.0;
        }

        /// <summary>保存本模组的配置。</summary>
        public override void SaveConfig()
        {
            Config.SetElementValue("TimeOutSeconds", TimeOutSeconds.ToString());
            Config.SaveXdocument();
        }

        /// <summary>焊接工作状态切换管控。</summary>
        public override void WeldStatusTrans()
        {
            if (WeldStatus == SubDeviceWeldStatus.NoReset) return;
            SetWeldStatus(WeldStatus, "焊接状态切换管控");
        }

        #endregion

        #region 非阻塞动作指令

        /// <summary>开启连接（非阻塞）：动作即退出释放。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOn()
        {
            SetState(SubDeviceState.Connected, "焊接头运行期连接");
            return true;
        }

        /// <summary>关闭连接（非阻塞）：动作即退出释放。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOff()
        {
            SetState(SubDeviceState.Disconnected, "焊接头运行期断开");
            return true;
        }

        /// <summary>焊接头手动动作统一门禁</summary>

        /// <param name="actionName">动作名（日志与提示用）</param>
        /// <param name="reason">拒绝原因（放行时为空串）</param>
        /// <returns>允许执行返回 true</returns>
        public bool EnsureManualWeldHeadActionAllowed(string actionName, out string reason)
        {
            reason = string.Empty;
            if (!DeviceControlWork.Instance.IsAutoWeldingRunning) return true;
            reason = "自动焊接流程进行中，手动动作与生产流程互斥";
            LogAction(RunMode, actionName + " 被拒 " + reason, MessageLevel.Warning);
            return false;
        }

        /// <summary>LA auto：开启随动（TA 上下移动时 LA 跟随补偿保持光斑恒定）。</summary>
        /// <returns>受理返回 true</returns>
        public bool LaAuto()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA auto", out _)) return false;
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg != null && cfg.LaSpotTargetMm > 0.0)
            {
                if (!LaApplySpot(cfg.LaSpotTargetMm, "LA auto 开启"))
                {
                    return false;
                }
            }
            _laFollowActive = true;
            _lastLaTargetMm = double.NaN;   // 重入强制首下发
            Log(string.Format("LA auto 随动开启 目标光斑 {0:F3}mm 基准位 {1:F3}mm", _targetSpotMm, _laBaseMm));
            return true;
        }

        /// <summary>LA 自动模式入口（流程内调用，焊前阶段进工作模式）。</summary>
        /// <remarks>与手动入口 <see cref="LaAuto"/> 的区别：⛔ 不做「自动焊接中禁手动动作」门禁检查——
        /// 焊前阶段本就属自动流程，主控派发的动作是流程组成而非手动干预。</remarks>
        /// <returns>受理返回 true</returns>
        public bool LaAutoAuto()
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg != null && cfg.LaSpotTargetMm > 0.0)
            {
                if (!LaApplySpot(cfg.LaSpotTargetMm, "LA 自动模式开启"))
                {
                    return false;
                }
            }
            _laFollowActive = true;
            _lastLaTargetMm = double.NaN;   // 重入强制首下发
            Log(string.Format("LA 自动模式随动开启 目标光斑 {0:F3}mm 基准位 {1:F3}mm", _targetSpotMm, _laBaseMm));
            return true;
        }

        /// <summary>LA Spot1：设目标光斑并按当前 TA 位置推算 LA 位。</summary>
        /// <returns>下发成功返回 true</returns>
        public bool LaSpot1()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA Spot1", out _)) return false;
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA Spot1 失败 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return LaApplySpot(cfg.LaSpot1Mm, "LA Spot1");
        }

        /// <summary>LA Spot2：设目标光斑并按当前 TA 位置推算 LA 位。</summary>
        /// <returns>下发成功返回 true</returns>
        public bool LaSpot2()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA Spot2", out _)) return false;
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA Spot2 失败 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return LaApplySpot(cfg.LaSpot2Mm, "LA Spot2");
        }

        /// <summary>LA Pose1：直接移到预设调试位观察光斑。</summary>
        /// <returns>下发成功返回 true</returns>
        public bool LaPose1()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA Pose1", out _)) return false;
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA Pose1 失败 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return LaMoveToPose(cfg.LaPose1Mm, "LA Pose1");
        }

        /// <summary>LA 默认焦平面：直接移到 LA 的默认工作位</summary>
        /// <returns>下发成功返回 true</returns>
        public bool LaDefaultFocus()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA 默认焦平面", out _)) return false;
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA 默认焦平面 失败 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return LaMoveToPose(cfg.LaDefaultFocusMm, "LA 默认焦平面");
        }

        /// <summary>LA 就位到默认焦平面</summary>
        /// <returns>下发成功返回 true</returns>
        public bool MoveToDefaultFocus()
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA 默认焦平面 跳过 工艺配置不可用", MessageLevel.Warning);
                return false;
            }
            double posMm = cfg.LaDefaultFocusMm;
            if (posMm <= 0.0)
            {
                Log("LA 默认焦平面 跳过 未配置位置", MessageLevel.Info);
                return false;
            }
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null || !weldHead.IsInitialized)
            {
                Log("LA 默认焦平面 跳过 焊接头从站未就绪", MessageLevel.Warning);
                return false;
            }
            bool ok = LaMoveTo(posMm, "LA 默认焦平面", true);
            Log(ok ? "LA 已就位默认焦平面" : "LA 默认焦平面下发失败",
                ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>振镜回到逻辑零位：直线+最小摆幅+不使能，中心补</summary>

        /// <returns>下发成功返回 true</returns>
        public bool GalvoHome()
        {
            if (!EnsureManualWeldHeadActionAllowed("振镜回逻辑零位", out _)) return false;
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null)
            {
                LogAction(RunMode, "振镜回零失败 焊接头从站未就绪", MessageLevel.Error);
                return false;
            }
            _galvoCenterXmm = 0.0;
            _galvoCenterYmm = 0.0;
            _lastGalvoAmplitudeMm = double.NaN;
            _lastGalvoFreqHz = -1;
            _lastGalvoCenterXmm = double.NaN;
            _lastGalvoCenterYmm = double.NaN;
            bool ok = weldHead.SetGalvoParams(GalvoParams.CreateLogicalHome());
            LogAction(RunMode, ok ? "振镜已回逻辑零位" : "振镜回零指令下发失败",
                ok ? MessageLevel.Info : MessageLevel.Error);
            return ok;
        }

        /// <summary>焊接头从站是否已装载并就绪</summary>
        /// <remarks>从站未装载（如节点号撞号）属配置缺硬件，调用方应先判此值再决定是否下发，
        /// 避免把「已知缺硬件」记成流程 Error。</remarks>
        public bool IsSlaveReady
        {
            get { return MontionManager.Instance.WeldHead != null; }
        }

        /// <summary>振镜摆动启动入口（流程内调用，焊前阶段按自适应基准值开启摆动）。</summary>
        /// <remarks>与手动入口 <see cref="GalvoManualStart"/> 的区别：⛔ 不做「自动焊接中禁手动动作」门禁检查，
        /// 且摆幅取自适应基准值（<c>AutoParam.SwingWidth</c>）与基准频率，
        /// ⛔ 不依赖 Working 态的 <c>LastOutput</c>（焊前尚无当拍工艺输出）。</remarks>
        /// <returns>下发成功返回 true</returns>
        public bool GalvoWeaveStartAuto()
        {
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null)
            {
                Log("振镜摆动启动失败 焊接头从站未就绪", MessageLevel.Error);
                return false;
            }
            if (!IsGalvoWeaveEnabled())
            {
                Log("振镜摆动启动跳过 未启用振镜跟随摆动工艺", MessageLevel.Info);
                return false;
            }

            AutoParam autoParam = WeldParamManager.Instance.CurrentAutoParam;
            if (autoParam == null)
            {
                Log("振镜摆动启动失败 自适应参数未载入", MessageLevel.Error);
                return false;
            }

            double amplitude = autoParam.SwingWidth;
            if (amplitude <= 0.0)
            {
                Log("振镜摆动启动失败 自适应基准摆幅未配置", MessageLevel.Warning);
                return false;
            }
            int frequency = (int)Math.Round(autoParam.SwingFreqHz1 > 0.0
                ? autoParam.SwingFreqHz1
                : AutoParam.DefaultSwingFreqHz1);

            GalvoParams galvo = GalvoParams.CreateLogicalHome();
            galvo.Pattern = ResolveGalvoPattern(WeldParamManager.Instance.AlgorithmsConfig != null
                ? WeldParamManager.Instance.AlgorithmsConfig.GalvoPattern : 0);
            galvo.Width = amplitude;
            galvo.Length = amplitude;
            galvo.Frequency = frequency;
            galvo.GalvoEnable = true;
            galvo.XCenter = _galvoCenterXmm;
            galvo.YCenter = _galvoCenterYmm;
            bool ok = weldHead.SetGalvoParams(galvo);
            if (ok)
            {
                _lastGalvoAmplitudeMm = amplitude;
                _lastGalvoFreqHz = frequency;
                _lastGalvoCenterXmm = _galvoCenterXmm;
                _lastGalvoCenterYmm = _galvoCenterYmm;
                Log(string.Format("振镜摆动已启动 图形 {0} 基准摆幅 {1:F3}mm 频率 {2}Hz",
                    galvo.Pattern, amplitude, frequency));
            }
            else
            {
                Log("振镜摆动启动失败 参数写入失败", MessageLevel.Error);
            }
            return ok;
        }

        /// <summary>振镜手动下发：按 GA 设定档</summary>
        /// <param name="pattern">图形</param>
        /// <param name="shape">该图形的分档参数（宽/长/频）</param>
        /// <returns>下发成功返回 true</returns>
        public bool GalvoManualStart(WsgGalvoPattern pattern, GalvoShapeParams shape)
        {
            if (!EnsureManualWeldHeadActionAllowed("振镜手动下发", out _)) return false;
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null)
            {
                LogAction(RunMode, "振镜手动下发失败 焊接头从站未就绪", MessageLevel.Error);
                return false;
            }
            if (shape == null)
            {
                LogAction(RunMode, "振镜手动下发失败 分档参数不可用", MessageLevel.Error);
                return false;
            }
            GalvoParams galvo = GalvoParams.CreateLogicalHome();
            galvo.Pattern = pattern;
            galvo.Width = shape.Width;
            galvo.Length = shape.Length;
            galvo.Frequency = shape.Frequency;
            galvo.GalvoEnable = true;
            galvo.XCenter = _galvoCenterXmm;
            galvo.YCenter = _galvoCenterYmm;
            bool ok = weldHead.SetGalvoParams(galvo);
            if (ok)
            {
                _lastGalvoAmplitudeMm = shape.Width;
                _lastGalvoFreqHz = galvo.Frequency;
                _lastGalvoCenterXmm = _galvoCenterXmm;
                _lastGalvoCenterYmm = _galvoCenterYmm;
                LogAction(RunMode, string.Format("振镜手动下发 图形 {0} 宽 {1:F3}mm 长 {2:F3}mm 频率 {3}Hz",
                    pattern, shape.Width, shape.Length, galvo.Frequency));
            }
            else
            {
                LogAction(RunMode, "振镜手动下发失败 参数写入失败", MessageLevel.Error);
            }
            return ok;
        }

        /// <summary>振镜手动停止：撤销使能并回逻辑零位</summary>
        /// <returns>下发成功返回 true</returns>
        public bool GalvoManualStop()
        {
            if (!EnsureManualWeldHeadActionAllowed("振镜手动停止", out _)) return false;
            return GalvoHome();
        }

        /// <summary>光丝对中偏差→振镜中心微调的单写口：只改补偿</summary>

        /// <param name="xMm">X 中心补偿后值（mm）</param>
        /// <param name="yMm">Y 中心补偿后值（mm）</param>
        public void ApplyGalvoCenterOffset(double xMm, double yMm)
        {
            _galvoCenterXmm = xMm;
            _galvoCenterYmm = yMm;
        }

        #endregion

        #region 可观测性

        /// <summary>执行步号转中文名，骨架期按相位展示。</summary>
        /// <param name="step">执行步号</param>
        /// <returns>步号对应的中文名</returns>
        protected override string GetStepName(int step)
        {
            // 步名按执行步号表达
            switch (step)
            {
                case StepGalvo: return "振镜参数下发";
                case StepLaFollow: return "LA随动下发";
                case StepIdle: return "待机";
                default: return "步 " + step;
            }
        }

        #endregion
    }
}


