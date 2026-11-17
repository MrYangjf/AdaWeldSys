﻿using System;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.WeldParamControl;
using System.Threading;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{

    /// <summary>焊接头工作流（骨架）。</summary>
    /// <remarks>差异化职责：监控温度阈值（点位待硬件明确）。</remarks>
    public class LaserWeldHeadWorkflow : DeviceWorkflowBase
    {
        #region 常量


        /// <summary>执行步段位（10 振镜参数下发）</summary>
        private const int StepGalvo = 10;

        /// <summary>执行步段位（20 LA 随动下发）</summary>
        private const int StepLaFollow = 20;

        #endregion

        #region 私有变量

        /// <summary>上次已下发的振镜摆幅（毫米）；与目标不一致才下发，避免逐帧刷总线</summary>
        private double _lastGalvoAmplitudeMm = double.NaN;

        /// <summary>本次运行的振镜摆动是否启用（LoadConfig 时取自焊参配置）</summary>
        private bool _galvoWeaveEnabled;

        /// <summary>最近温度上报（保护镜/环境/温升/气压），供 UI 读取。</summary>
        private LaserHeadTemperatureData _lastTemperature;

        /// <summary>最近从站报警状态位，供判据与 UI 读取。</summary>
        private HeadAlarmStatus _lastAlarmStatus;

        /// <summary>LA 是否处于 auto 随动态（按 TA 位置补偿保持光斑恒定）</summary>
        private bool _laFollowActive;

        /// <summary>目标光斑对应的 LA 基准位置（毫米，曲线A 反查 = TA=0 时的 LA 位）</summary>
        private double _laBaseMm;

        /// <summary>当前目标光斑大小（毫米）；初值取曲线A 锚点（经 WeldParamManager 读取，构造期赋值）。</summary>
        private double _targetSpotMm;

        /// <summary>上次已下发的 LA 目标位置（毫米）；变化超死区才下发，避免逐拍刷从站总线</summary>
        private double _lastLaTargetMm = double.NaN;

        /// <summary>上次已下发的振镜频率（Hz，-1=未下发）；与摆幅/中心共同决定是否重发</summary>
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

        /// <summary>配置落LaserWeldHead\Workflow_XML 子目录。</summary>
        protected override string ConfigRelativeDir { get { return @"Config\LaserWeldHead\Workflow_XML"; } }

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
        /// <remarks>LA 就绪判定归本流程（硬件状态来自从站）。</remarks>
        public bool LaReady { get { return MontionManager.Instance.LaReady; } }

        /// <summary>LA 不可用原因（就绪返回空串）。</summary>
        public string LaGateReason { get { return MontionManager.Instance.LaGateReason; } }

        /// <summary>TA 轴已使能（L1 前提）。</summary>
        public override bool IsEnabled { get { return MontionManager.Instance.TaMotionReady; } }

        /// <summary>TA 轴已回零（L3 前提）。</summary>
        public override bool IsHomed { get { return MontionManager.Instance.TaHomed; } }

        /// <summary>TA 已在工作零位（L4）。</summary>
        /// <remarks>2026-11-17 改版：读工作零位到位观测位 <see cref="MontionManager.TaAtWorkZero"/>，
        /// 不再读「TA 指令是否可执行」——两者语义不同（L4 ≠ L1∧L3）。</remarks>
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
        /// <remarks>阈值不在此重复下发（SetAlarmThresholds 归初始化，避免双写）。</remarks>
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
        /// <remarks>越阈触发在从站侧，阈值与级别判定归本方法。</remarks>
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
        /// <remarks>本类不得写 <c>Dispose(bool)</c>；基类提供 <c>DisposeManaged()</c> 钩子。 子）。</remarks>
        protected override void DisposeManaged()
        {
            UnsubscribeWeldHeadEvents();
            base.DisposeManaged();
        }

        #region 私有函数

        /// <summary>按当前摆幅下发振镜参数（值未变则跳过，避免逐</summary>
        /// <remarks>摆幅取 <c>WeldParamControlWorkflow.LastOutput.GalvoAmplitude</c>；
        /// 关闭摆动时下发 0，振镜回到不摆动状态。
        /// ⚠️ 中心偏移由 <c>ApplyGalvoCenterOffset</c> 单写。</remarks>
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
            double amplitude = (_galvoWeaveEnabled && output != null) ? output.GalvoAmplitude : 0.0;
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

            // ⛔ 变量名不可用 `params`（C# 关键字）
            GalvoParams galvo = GalvoParams.CreateLogicalHome();
            galvo.Width = amplitude;
            galvo.Length = amplitude;
            galvo.GalvoEnable = amplitude > 0.0;
            // 图形种类 = 人工编辑项，不参与宽度自适应
            WeldAlgorithmsConfig algoCfg = WeldParamManager.Instance.AlgorithmsConfig;
            galvo.Pattern = (WsgGalvoPattern)(algoCfg != null ? algoCfg.GalvoPattern : (int)WsgGalvoPattern.Line);
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
        /// <remarks>偿。</remarks>
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
        /// <remarks>本方法与 LA_Pose 同退随动，再做一次定位下发。
        /// 目标 = 曲线A 基准 + 曲线B 当前 TA 偏移；持续随动仅由 <see cref="LaAuto"/> 重新进入。</remarks>
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
        /// <remarks>经曲线。</remarks>
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
        /// <remarks>本工作流按拍消费 <c>WeldParamAdaptControl.GalvoAmplitude</c>，保证界面改参数运行期生效；
        /// case 内禁 Thread.Sleep / while 轮询。 焊缝宽度自适应）。</remarks>
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
            return true;
        }

        /// <summary>状态清理：复位标志与执行步。</summary>
        public override void ClearStatus()
        {
            AdvanceStep(StepIdle);
            SetWeldStatus(SubDeviceWeldStatus.NoReset, "状态清除");
        }

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入</summary>
        /// <remarks>步骤时长。</remarks>
        public override void ResetWorkTime()
        {
            ResetStepTime();
        }

        /// <summary>设备连接（阻塞）：无实体设备，仅切换状态机</summary>
        /// <remarks>回。</remarks>
        /// <returns>连接成功返回 true</returns>
        public override bool InitializeOn()
        {
            SetState(SubDeviceState.Connected, "焊接头初始化连接");
            SetWeldStatus(SubDeviceWeldStatus.Standby, "初始化连接完成");
            return true;
        }

        /// <summary>设备断开（阻塞）：无实体设备，仅切换状态机</summary>
        /// <remarks>回。</remarks>
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

            // 振镜摆动开关与系数归焊参配置，此处只取开关
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg != null)
            {
                _galvoWeaveEnabled = cfg.EnableGalvoWeave;
                // 目标光斑取配置（键 LA_SPOT_TARGET_MM）
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

        /// <summary>焊接头手动动作统一门禁：自动焊接流程进行中 ⇒ 拒绝</summary>
        /// <remarks>2026-11-17 新增（ADR-100 演进）：通过界面 UI 发起的动作一律是手动模式，
        /// 自动焊接流程一旦启动到结束必须拒绝——手动与生产流程同时驱动同一台电机会互相打架。
        /// 判据唯一来源 = <see cref="DeviceControlWork.IsAutoWeldingRunning"/>（主设备级权威）。
        /// ⛔ 不复用 <c>IsWelding</c>：后者覆盖整段开机期，过宽会把正常调试也一并禁掉。</remarks>
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
        /// <remarks>开启时先按配置目标光斑下发一次，再进随动。
        /// 配置未设置（≤0）时沿用最近一次目标光斑。</remarks>
        public bool LaAuto()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA auto", out string laReject)) return false;
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
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

        /// <summary>LA Spot1：设目标光斑并按当前 TA 位置推算 LA 位。</summary>
        /// <returns>下发成功返回 true</returns>
        public bool LaSpot1()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA Spot1", out string laReject)) return false;
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
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
            if (!EnsureManualWeldHeadActionAllowed("LA Spot2", out string laReject)) return false;
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
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
            if (!EnsureManualWeldHeadActionAllowed("LA Pose1", out string laReject)) return false;
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA Pose1 失败 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return LaMoveToPose(cfg.LaPose1Mm, "LA Pose1");
        }

        /// <summary>LA Pose2：直接移到预设调试位观察光斑。</summary>
        /// <returns>下发成功返回 true</returns>
        public bool LaPose2()
        {
            if (!EnsureManualWeldHeadActionAllowed("LA Pose2", out string laReject)) return false;
            WeldAlgorithmsConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null)
            {
                Log("LA Pose2 失败 工艺配置不可用", MessageLevel.Error);
                return false;
            }
            return LaMoveToPose(cfg.LaPose2Mm, "LA Pose2");
        }

        /// <summary>振镜回到逻辑零位：直线+最小摆幅+不使能，中心补</summary>
        /// <remarks>偿状态一并清零。</remarks>
        /// <returns>下发成功返回 true</returns>
        public bool GalvoHome()
        {
            if (!EnsureManualWeldHeadActionAllowed("振镜回逻辑零位", out string galvoReject)) return false;
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

        /// <summary>振镜手动演示：按图形一次性下发演示参数（圆/八</summary>
        /// <param name="pattern">演示图形</param>
        /// <returns>下发成功返回 true</returns>
        /// <remarks>一次性动作：不进入随动循环；演示摆幅 1.0mm、频率取协议默认；
        /// 焊接中拒绝执行；停止演示 = <see cref="GalvoHome"/> 回零位。 字/直线/圆弧），与生产流程互斥。</remarks>
        public bool GalvoDemo(WsgGalvoPattern pattern)
        {
            if (!EnsureManualWeldHeadActionAllowed("振镜演示", out string demoReject)) return false;
            WeldHeadSlaveDevice weldHead = MontionManager.Instance.WeldHead;
            if (weldHead == null)
            {
                LogAction(RunMode, "振镜演示失败 焊接头从站未就绪", MessageLevel.Error);
                return false;
            }
            GalvoParams galvo = GalvoParams.CreateLogicalHome();
            galvo.Pattern = pattern;
            galvo.Width = 1.0;
            galvo.Length = 1.0;
            galvo.GalvoEnable = true;
            galvo.XCenter = _galvoCenterXmm;
            galvo.YCenter = _galvoCenterYmm;
            bool ok = weldHead.SetGalvoParams(galvo);
            if (ok)
            {
                _lastGalvoAmplitudeMm = 1.0;
                _lastGalvoFreqHz = galvo.Frequency;
                _lastGalvoCenterXmm = _galvoCenterXmm;
                _lastGalvoCenterYmm = _galvoCenterYmm;
                LogAction(RunMode, string.Format("振镜演示 图形 {0} 摆幅 1.0mm 频率 {1}Hz", pattern, galvo.Frequency));
            }
            return ok;
        }

        /// <summary>光丝对中偏差→振镜中心微调的单写口：只改补偿</summary>
        /// <remarks>状态，实际下发由随动步合并执行。</remarks>
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
