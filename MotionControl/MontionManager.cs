using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ConfigFileControl.Implementations;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>运动管理器</summary>
    public class MontionManager
    {
        #region 私有变量

        private const string Tag = "运动管理器";

        /// <summary>TA 降级原因：初始化第 2 步尚未执行</summary>
        private const string TaReasonConfigNotLoaded = "TA 轴配置尚未加载（初始化第 2 步未执行）";

        /// <summary>TA 降级原因：工作零位定位尚未完成</summary>
        private const string TaReasonZeroPoseNotDone = "TA_ZeroPose 未到位（初始化第 3 步工作零位定位未完成）";

        /// <summary>初始化第 2 步 TA 轴配置加载（读-比对-写）是否成功</summary>
        private bool _taConfigLoadOk;

        /// <summary>第 3 步 TA_ZeroPose 零位门禁（Workflow 写入，本类只判定）</summary>
        private volatile bool _taZeroPoseGate;

        /// <summary>TA 配置加载失败时的可读原因</summary>
        private string _taDegradedReason = TaReasonConfigNotLoaded;

        /// <summary>TA 工作零位门禁未通过时的可读原因</summary>
        private volatile string _taZeroPoseGateReason = TaReasonZeroPoseNotDone;

        /// <summary>停稳判据：速度绝对值上限，低于即算停稳</summary>
        private const double InstructionStableSpeedMmPerSec = 0.05;

        /// <summary>单飞登记逾期自动释放，防该轴永久 BUSY</summary>
        private const double AxisInstructionLeaseSeconds = 600.0;

        private static double EffectiveDetectTimeoutSeconds { get { return FlowTimeoutSettings.Current.InstructionDetectSeconds; } }
        private static int EffectiveSampleIntervalMs { get { return FlowTimeoutSettings.Current.InstructionSampleMs; } }
        private static int EffectiveStableSampleCount { get { return FlowTimeoutSettings.Current.InstructionStableSamples; } }
        private static int EffectiveTorqueConfirmSamples { get { return FlowTimeoutSettings.Current.InstructionTorqueSamples; } }

        /// <summary>台达 CSP 位置模式模式字（CIA402 8）</summary>
        private const ushort ModeCsp = 8;

        /// <summary>台达 CST 扭矩模式模式字（CIA402 10）</summary>
        private const ushort ModeCst = 10;

        /// <summary>初始化链是否走完（唯一闸门）</summary>
        private volatile bool _initCompleted;

        /// <summary>整机初始化是否成功；<b>仅展示</b>，不作门禁</summary>
        private volatile bool _initSucceeded;

        // —— 运行期生效值取自 ROOT/FLOW_TIMEOUT，缺省=上方常量 ——
        // 一律用 Effective*：改流程超时后重载配置即生效。
        /// <summary>轴名单飞登记表（同轴同一时刻只一条指令）</summary>
        private readonly Dictionary<string, AxisInstructionEntry> _axisInstructions =
            new Dictionary<string, AxisInstructionEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>焊接头配置所属模块（全局配置目录下的模块层）。</summary>
        internal const string ConfigModule = "MotionControl";

        /// <summary>轴角色关键字表（轴名含任一即命中）</summary>
        public static readonly string[] SeamTrackYKeys = { "Y", "横梁", "跟踪" };

        /// <summary>对焦/光斑轴关键字（轴名包含任一即命中）</summary>
        public static readonly string[] FocusSpotKeys = { "对焦", "焦点", "光斑", "FOCUS", "SPOT" };

        /// <summary>TA 轴关键字</summary>
        public static readonly string[] TaAxisKeys = { "TA", "力矩", "TORQUE" };

        // 状态字位定义已收敛到 IMotion.AxisStatusBits / AxisStatus（全项目唯一解析入口）。
        // 位运算交由底层封装处理

        /// <summary>指令状态检测统一超时（秒）：5s，集中常量便于调整</summary>
        public const double InstructionDetectTimeoutSeconds = 5.0;

        /// <summary>指令状态检测采样周期（毫秒）</summary>
        public const int InstructionSampleIntervalMs = 20;

        /// <summary>位置类「连续稳定」采样次数（3 次 × 20ms = 60ms 确认窗）</summary>
        public const int InstructionStableSampleCount = 3;

        /// <summary>同轴指令单飞登记项</summary>
        private sealed class AxisInstructionEntry
        {
            public string InstructionName;
            public DateTime StartedUtc;
            public CancellationTokenSource Cancel;
        }

        private readonly object _axisInstructionLock = new object();

        /// <summary>位置类默认容差（mm）；Pose/Y/LA 未单独配置容差时统一用该值</summary>
        public const double InstructionDefaultTolMm = 0.1;

        private static readonly Lazy<MontionManager> _lazy =
            new Lazy<MontionManager>(() => new MontionManager());

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static MontionManager Instance { get { return _lazy.Value; } }

        /// <summary>当前控制器，未初始化时为 null</summary>
        public MontionControl MontionControl { get; private set; }

        /// <summary>是否已初始化</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>运控数据，标定参数与轴 IO 配置的唯一对外入口</summary>
        public MoveControlData MoveData { get; private set; }

        /// <summary>轴注册表</summary>
        public AxisRegistry Axes { get; private set; }

        /// <summary>IO 注册表</summary>
        public IORegistry IOs { get; private set; }

        /// <summary>气缸注册表</summary>
        public CylinderRegistry Cylinders { get; private set; }

        /// <summary>焊接头从站设备</summary>
        public WeldHeadSlaveDevice WeldHead { get; private set; }

        /// <summary>焊接头配置（实例归本类独占，Config\MotionControl\WeldHead_XML\WeldHeadSlave.xml，外部只读）</summary>
        public WeldHeadConfig WeldHeadConfig { get; private set; }

        /// <summary>TA 指令是否可执行</summary>
        public bool TaAvailable
        {
            get { return TaMotionReady && TaHomed; }
        }

        /// <summary>TA 工作零位是否到位（L4 只读观测位；不参与 TA 指令门禁）</summary>
        public bool TaAtWorkZero { get { return _taZeroPoseGate; } }

        /// <summary>TA 工作零位未到位原因（观测用；不代表 TA 指令被拒）</summary>
        public string TaZeroPoseReason { get { return _taZeroPoseGateReason; } }

        /// <summary>TA 轴本次上电是否已回零</summary>
        public bool TaHomed { get; private set; }

        /// <summary>TA 轴配置已加载且已使能</summary>
        public bool TaMotionReady
        {
            get
            {
                if (!_initCompleted) return false;
                if (!IsInitialized) return false;
                DeltaMontionAxis axis = GetDeltaAxisByKeys(TaAxisKeys);
                if (axis == null || !_taConfigLoadOk) return false;
                return axis.IsServoOn();
            }
        }

        /// <summary>TA 工作零位坐标（mm）：唯一可编辑源 = 工艺设置页 TA 区 `TA_ZERO_POSE_MM`。</summary>
        public double TaZeroPoseTargetMm
        {
            get
            {
                WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
                if (cfg != null) return cfg.TaZeroPoseMm;
                DeltaTaHomeConfig taHome = MoveData == null || MoveData.MotionConfig == null ? null : MoveData.MotionConfig.TaHome;
                return taHome == null ? 0.0 : taHome.WorkOriginMm;
            }
        }

        /// <summary>TA 降级原因（<see cref="TaAvailable"/> 为 false 时给出可读说明）</summary>
        public string TaDegradedReason
        {
            get
            {
                if (!_initCompleted) return "初始化未完成（使能阶段尚未结束）";
                if (!IsInitialized) return "运动管理器未初始化";
                DeltaMontionAxis axis = GetDeltaAxisByKeys(TaAxisKeys);
                if (axis == null) return "TA 轴未装配（配置未启用或轴名未匹配 TA 关键字）";
                if (IsAxisSlaveMissing(axis)) return "从站未连接 节点 " + axis.NodeId;
                if (!_taConfigLoadOk) return _taDegradedReason;
                if (!axis.IsServoOn()) return "TA 轴未使能（初始化第 3 步使能失败或已下电）";
                if (!TaHomed) return "TA 未回零（伺服下电后位置不可信，须先完成回零：手动回零或复位回零均可）";
                return string.Empty;
            }
        }

        /// <summary>Y 轴就绪：链路走完 ∧ 已装载 ∧ 已使能</summary>
        public bool YReady
        {
            get
            {
                if (!_initCompleted) return false;
                if (!IsInitialized) return false;
                MontionAxis axis = GetAxisByKeys(SeamTrackYKeys);
                return axis != null && axis.IsServoOn();
            }
        }

        /// <summary>Y 轴降级原因（就绪返回空串）</summary>
        public string YGateReason
        {
            get
            {
                if (!_initCompleted) return "初始化未完成（使能阶段进行中）";
                if (!IsInitialized) return "运动管理器未初始化";
                MontionAxis axis = GetAxisByKeys(SeamTrackYKeys);
                if (axis == null) return "Y 轴未装配（配置未启用或轴名未匹配 Y 关键字）";
                if (IsAxisSlaveMissing(axis)) return "从站未连接 节点 " + ((DeltaMontionAxis)axis).NodeId;
                if (!axis.IsServoOn()) return "Y 轴使能失败";
                return string.Empty;
            }
        }

        /// <summary>LA（焊接头从站：对焦电机 / 振镜）是否就绪</summary>
        public bool LaReady
        {
            get
            {
                if (!_initCompleted) return false;
                if (!IsInitialized) return false;
                return WeldHead != null && WeldHead.IsInitialized;
            }
        }

        /// <summary>LA 降级原因（就绪返回空串）</summary>
        public string LaGateReason
        {
            get
            {
                if (!_initCompleted) return "初始化未完成（使能阶段进行中）";
                if (!IsInitialized) return "运动管理器未初始化";
                if (WeldHead == null) return "焊接头从站未装载";
                if (!WeldHead.IsInitialized) return "焊接头从站握手未完成（LA 对焦/振镜不可用）";
                return string.Empty;
            }
        }

        /// <summary>该节点是否出现在从站扫描结果中</summary>

        /// <param name="nodeId">站点号</param>
        /// <returns>已发现返回 true</returns>
        private bool IsNodeDetected(ushort nodeId)
        {
            if (!(MontionControl is DeltaMontionControl control) || control.SlaveScanner == null || control.SlaveScanner.SlaveInfos == null)
                return false;
            for (int i = 0; i < control.SlaveScanner.SlaveInfos.Count; i++)
            {
                if (control.SlaveScanner.SlaveInfos[i].NodeId == nodeId) return true;
            }
            return false;
        }

        /// <summary>该轴从站是否未出现在主站扫描结果中</summary>
        /// <param name="axis">运动轴</param>
        /// <returns>未发现返回 true；非台达轴返回 false</returns>
        private bool IsAxisSlaveMissing(MontionAxis axis)
        {
            if (!(axis is DeltaMontionAxis delta)) return false;
            return !IsNodeDetected(delta.NodeId);
        }

        /// <summary>指定节点是否出现在主站扫描结果中</summary>
        /// <param name="nodeId">站点号</param>
        /// <returns>已发现返回 true</returns>
        public bool IsSlaveDetected(ushort nodeId)
        {
            return IsNodeDetected(nodeId);
        }

        /// <summary>按轴取门禁原因（就绪返回空串）</summary>

        /// <param name="axis">运动轴对象</param>
        /// <returns>不可执行原因；空串表示可执行</returns>
        public string GetAxisGateReason(MontionAxis axis)
        {
            if (axis == null) return "轴未装载";
            if (!_initCompleted) return "初始化未完成（使能阶段进行中）";
            DeltaMontionAxis ta = GetDeltaAxisByKeys(TaAxisKeys);
            if (ta != null && ReferenceEquals(axis, ta)) return TaDegradedReason;
            MontionAxis y = GetAxisByKeys(SeamTrackYKeys);
            if (y != null && ReferenceEquals(axis, y)) return YGateReason;
            if (IsAxisSlaveMissing(axis)) return axis.AxisName + " 从站未连接 节点 " + ((DeltaMontionAxis)axis).NodeId;
            if (!axis.IsServoOn()) return axis.AxisName + " 未就绪：" + DescribeAxisDiagnosis(axis, "伺服使能", false);
            return string.Empty;
        }

        /// <summary>按轴取回零门禁原因（就绪返回空串）</summary>

        /// <param name="axis">运动轴对象</param>
        /// <returns>不可执行原因；空串表示可执行</returns>
        public string GetAxisHomingGateReason(MontionAxis axis)
        {
            if (axis == null) return "轴未装载";
            if (!_initCompleted) return "初始化未完成（使能阶段进行中）";
            if (!IsInitialized) return "运动管理器未初始化";
            DeltaMontionAxis ta = GetDeltaAxisByKeys(TaAxisKeys);
            if (ta != null && ReferenceEquals(axis, ta) && !_taConfigLoadOk) return _taDegradedReason;
            if (IsAxisSlaveMissing(axis)) return axis.AxisName + " 从站未连接 节点 " + ((DeltaMontionAxis)axis).NodeId;
            if (!axis.IsServoOn()) return axis.AxisName + " 未就绪：" + DescribeAxisDiagnosis(axis, "伺服使能", false);
            if (GetAxisStatus(axis).Fault) return axis.AxisName + " 报警未复位，须先复位再回零";
            return string.Empty;
        }

        /// <summary>读取单轴状态摘要（UI 展示与上层判定的统一入口）</summary>
        /// <param name="axis">运动轴；为空时返回 0 快照</param>

        /// <returns>状态字摘要</returns>
        public AxisStatus GetAxisStatus(MontionAxis axis)
        {
            return AxisStatus.Read(axis);
        }

        /// <summary>按轴名读取只读运行摘要</summary>

        /// <param name="axisName">轴名（与注册表一致）</param>
        /// <param name="positionMm">当前位置（mm，未装配为 0）</param>
        /// <param name="lastCommand">最近指令描述（未装配为空串）</param>
        /// <param name="servoOn">伺服使能（状态字 OperationEnabled）</param>
        /// <param name="fault">报警（状态字 Fault）</param>
        /// <returns>轴是否已装配</returns>
        public bool TryGetAxisReadout(string axisName, out double positionMm, out string lastCommand,
            out bool servoOn, out bool fault)
        {
            positionMm = 0;
            lastCommand = string.Empty;
            servoOn = false;
            fault = false;

            MontionAxis axis = Axes.Get(axisName);
            if (axis == null) return false;

            lastCommand = axis.LastCommand;
            positionMm = axis.GetPosition();
            AxisStatus status = GetAxisStatus(axis);
            servoOn = status.OperationEnabled;
            fault = status.Fault;
            return true;
        }

        /// <summary>轴状态字文本（硬件故障树的唯一产出点）</summary>
        /// <param name="axis">运动轴</param>
        /// <returns>可读文本；取不到时写明「未取到」</returns>
        public string DescribeAxisStatusWord(MontionAxis axis)
        {
            if (axis == null) return "状态字 未取到";
            try
            {
                return string.Format("状态字 0x{0:X4}", axis.GetStatusWord());
            }
            // 诊断探测：读失败即降级为「未取到」，不抛给调用方
            catch (Exception) { return "状态字 未取到"; }
        }

        /// <summary>轴 CSP 缓冲运行证据文本（硬件故障树的唯一产出点）</summary>
        /// <param name="axis">运动轴</param>
        /// <returns>总线/报警/缓冲状态原值文本；非台达轴或读失败时写明「未取到」</returns>
        public string DescribeAxisCspRuntime(MontionAxis axis)
        {
            if (!(axis is DeltaMontionAxis delta)) return "总线状态 未取到 报警状态 未取到 缓冲状态 未取到 块数 未取到";
            try
            {
                delta.ReadSlaveStatus(out ushort opState, out ushort alarmStatus);
                delta.BufferMotionStatus(out ushort bufferStatus, out int blocks);
                return string.Format("总线状态 {0} 报警状态 {1} 缓冲状态 {2} 块数 {3}",
                    opState, alarmStatus, bufferStatus, blocks);
            }
            // 诊断探测：读失败即降级为「未取到」，不抛给调用方
            catch (Exception) { return "总线状态 未取到 报警状态 未取到 缓冲状态 未取到 块数 未取到"; }
        }

        /// <summary>轴指令返回码文本（硬件故障树的唯一产出点）</summary>
        /// <param name="code">台达返回码</param>
        /// <returns>可读文本</returns>
        public string DescribeAxisCommandReturnCode(int code)
        {
            return string.Format("返回码 0x{0:X}", code);
        }

        /// <summary>轴撤力失败文本（硬件故障树的唯一产出点）</summary>
        /// <param name="code">撤力返回码</param>
        /// <returns>可读文本</returns>
        public string DescribeAxisRetractFailure(ushort code)
        {
            return string.Format("撤力失败（错误码 {0}）", code);
        }

        /// <summary>拼接单轴失败诊断串</summary>
        /// <param name="axis">运动轴</param>
        /// <param name="phase">失败环节（如「伺服使能」）</param>
        /// <param name="withBus">是否叠加总线健康检查（高频刷新传 false）</param>

        /// <returns>可读诊断串</returns>
        public string DescribeAxisDiagnosis(MontionAxis axis, string phase, bool withBus)
        {
            if (axis == null) return phase + " 失败：轴对象为空";

            ushort statusWord = 0;
            bool statusGot = false;
            try
            {
                statusWord = axis.GetStatusWord();
                statusGot = true;
            }
            // 诊断读失败不打日志，原因已在返回值
            catch (Exception) { }
            string statusText = statusGot ? "0x" + statusWord.ToString("X4") : "未取到";

            string lastCmd = string.IsNullOrEmpty(axis.LastCommand)
                ? "无（指令未下发或被前置拦截）" : axis.LastCommand;

            string nodeInfo = "节点信息未取到";
            if (axis is DeltaMontionAxis delta)
            {
                ushort opState = 0;
                ushort alarmStatus = 0;
                try { delta.ReadConnectStatus(out opState, out alarmStatus); }
                // 诊断探测：同上
                catch (Exception) { }
                bool detected = false;
                try { detected = IsNodeDetected(delta.NodeId); }
                // 诊断探测：同上
                catch (Exception) { }
                nodeInfo = string.Format("节点 {0} 槽 {1} 从站扫描 {2} 连接状态 {3}（{4}） 报警状态 {5}",
                    delta.NodeId, delta.SlotNo, detected ? "已发现" : "未发现", opState,
                    opState == AdaWeldSystem.MotionControl.DeltaEtherCAT.Native.EtherCATMasterNative.OpStateOperational
                        ? "Operational" : "非 Operational", alarmStatus);
            }

            string busInfo = string.Empty;
            if (withBus && MontionControl != null)
            {
                try
                {
                    string faultDesc = string.Empty;
                    bool busOk = MontionControl.CheckBusOK(out faultDesc);
                    busInfo = busOk ? " 总线全部从站在运行态且无报警" : (" 总线异常：" + faultDesc);
                }
                // 诊断探测：同上
                catch (Exception) { }
            }

            string conclusion;
            if (!statusGot)
            {
                conclusion = "无法读取状态字，请检查该从站通讯";
            }
            else if (statusWord == 0)
            {
                conclusion = "状态字 0x0000 表示控制器未取到该轴有效状态（0x0000 的官方定义未确证）；"
                    + "常见于驱动器未上电、EtherCAT 网线/节点号不匹配或从站未进入 OP，请检查该轴供电与接线/节点配置";
            }
            else if (AxisStatus.Parse(statusWord).Fault)
            {
                conclusion = "状态字 fault 位为 1，需先复位驱动器报警";
            }
            else if (!AxisStatus.Parse(statusWord).OperationEnabled)
            {
                conclusion = "状态字未置 Operation Enabled 位，使能未生效";
            }
            else
            {
                conclusion = "状态字已置 Operation Enabled 位";
            }

            return string.Format("{0} 失败 轴 {1} 状态字 {2} 下发 {3} {4}{5}；{6}",
                phase, axis.AxisName, statusText, lastCmd, nodeInfo, busInfo, conclusion);
        }

        #region 初始化闸门与指令状态检测（2026-11-05）

        /// <summary>位置「有变化」判定阈值（mm）</summary>
        private const double PositionChangeEpsilonMm = 0.001;

        /// <summary>初始化链是否已走完</summary>
        public bool IsInitCompleted { get { return _initCompleted; } }

        /// <summary>整机初始化是否成功；<b>仅展示</b>，不作门禁</summary>
        public bool IsInitSucceeded { get { return _initSucceeded; } }

        /// <summary>轴就绪变化：UI 收到即重算三区可用性</summary>
        public event Action<string> AxisReadinessChanged;

        /// <summary>到位等待内核</summary>
        /// <param name="axis">轴对象。</param>
        /// <param name="targetMm">目标位置 毫米。</param>
        /// <param name="tolMm">到位容差 毫米。</param>
        /// <param name="timeoutSeconds">超时秒数。</param>
        /// <param name="noChangeSeconds">位置无变化早停秒数。</param>
        /// <param name="requireStableSpeed">是否要求速度稳定。</param>
        /// <param name="cancelRequested">取消请求回调。</param>
        /// <param name="evidence">失败证据收集开关。</param>

        /// <returns>是否成功。</returns>
        private bool WaitAxisAtTargetCore(MontionAxis axis, double targetMm, double tolMm,
            double timeoutSeconds, double noChangeSeconds, bool requireStableSpeed,
            Func<bool> cancelRequested, out string evidence)
        {
            evidence = string.Empty;
            if (axis == null) { evidence = "轴对象为空"; return false; }
            if (tolMm <= 0.0) tolMm = InstructionDefaultTolMm;
            if (timeoutSeconds <= 0.0) timeoutSeconds = EffectiveDetectTimeoutSeconds;
            DateTime startUtc = DateTime.UtcNow;
            DateTime deadline = startUtc.AddSeconds(timeoutSeconds);
            DateTime lastChangeUtc = startUtc;
            double lastPos = axis.GetPosition();
            int stable = 0;
            while (true)
            {
                if (cancelRequested != null && cancelRequested())
                {
                    evidence = string.Format("检测被取消 当前 {0:F3}mm 目标 {1:F3}mm",
                        axis.GetPosition(), targetMm);
                    return false;
                }

                double pos = axis.GetPosition();
                double speed = Math.Abs(axis.GetCurrentSpeed());
                AxisStatus status = AxisStatus.Read(axis);
                // 等价改造：原「axis.IsAlarm() || (status & 0x0008) != 0」——IsAlarm 即同一 6041h 的 Fault 位，
                // Fault 为锁存位，去掉重复读取不改变判定
                if (status.Fault)
                {
                    evidence = string.Format("驱动器报警 状态字 0x{0:X4} 当前 {1:F3}mm 目标 {2:F3}mm",
                        status.Raw, pos, targetMm);
                    return false;
                }

                if (Math.Abs(pos - lastPos) >= PositionChangeEpsilonMm)
                {
                    lastPos = pos;
                    lastChangeUtc = DateTime.UtcNow;
                }

                bool inTolerance = Math.Abs(pos - targetMm) <= tolMm;
                if (inTolerance && (!requireStableSpeed || speed <= InstructionStableSpeedMmPerSec))
                {
                    stable++;
                    if (!requireStableSpeed || stable >= EffectiveStableSampleCount)
                    {
                        evidence = string.Format("到位 当前 {0:F3}mm 目标 {1:F3}mm 容差 {2:F3}mm 速度 {3:F3}mm/s 状态字 0x{4:X4}",
                            pos, targetMm, tolMm, speed, status.Raw);
                        return true;
                    }
                }
                else stable = 0;

                if (DateTime.UtcNow >= deadline)
                {
                    evidence = string.Format("超时 {0:F1}s 未到位 当前 {1:F3}mm 目标 {2:F3}mm 容差 {3:F3}mm 速度 {4:F3}mm/s 状态字 0x{5:X4}",
                        timeoutSeconds, pos, targetMm, tolMm, speed, status.Raw);
                    return false;
                }

                if (noChangeSeconds > 0.0 && (DateTime.UtcNow - lastChangeUtc).TotalSeconds > noChangeSeconds)
                {
                    evidence = string.Format("位置长时间无变化（{0:F0}s 内位移小于 {1:F3}mm，当前 {2:F3}mm 目标 {3:F3}mm）",
                        noChangeSeconds, PositionChangeEpsilonMm, pos, targetMm);
                    return false;
                }

                try { Thread.Sleep(EffectiveSampleIntervalMs); }
                // 指令被取消（Thread.Interrupt）的正常出口，非异常
                catch (ThreadInterruptedException) { }
            }
        }

        /// <summary>标记初始化链结束（由 DeviceControlWork 在 InitStepEvaluate 收尾调用一次）</summary>
        /// <param name="allAxesReady">全部轴是否就绪（<b>仅展示</b>；不参与任何单轴门禁）</param>
        public void MarkInitCompleted(bool allAxesReady)
        {
            _initSucceeded = allAxesReady;
            _initCompleted = true;
            if (!allAxesReady) TaHomed = false;   // 初始化失败重来：回零结论作废
            Log("初始化闸门 打开 按轴判定 就绪放行 未就绪拒绝", MessageLevel.Info);
            NotifyAxisReadinessChanged("ALL");
        }

        /// <summary>广播某轴就绪状态变化（"Y" / "TA" / "LA" / "ALL"）。</summary>
        /// <param name="axisName">轴名或 ALL</param>
        public void NotifyAxisReadinessChanged(string axisName)
        {
            Action<string> handler = AxisReadinessChanged;
            if (handler == null) return;
            try { handler(axisName); }
            catch (Exception ex) { Log("轴就绪事件处理异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>登记 TA 工作零位门禁状态</summary>
        /// <param name="done">是否到位</param>
        /// <param name="reason">未到位原因（到位时传空串）</param>
        public void SetTaZeroPoseGate(bool done, string reason)
        {
            _taZeroPoseGate = done;
            _taZeroPoseGateReason = done || string.IsNullOrEmpty(reason) ? TaReasonZeroPoseNotDone : reason;
        }

        /// <summary>登记某轴指令开始执行</summary>

        /// <param name="axisName">轴名（隔离键）</param>
        /// <param name="instructionName">指令名（提示与日志用）</param>
        /// <param name="token">本次指令的取消标记</param>
        /// <param name="reason">被拒原因</param>
        /// <returns>允许执行返回 true</returns>
        public bool TryBeginAxisInstruction(string axisName, string instructionName,
            out CancellationToken token, out string reason)
        {
            token = CancellationToken.None;
            reason = string.Empty;
            if (string.IsNullOrEmpty(axisName)) axisName = "未知轴";
            lock (_axisInstructionLock)
            {
                if (_axisInstructions.TryGetValue(axisName, out AxisInstructionEntry busy) && busy != null)
                {
                    double elapsed = (DateTime.UtcNow - busy.StartedUtc).TotalSeconds;
                    if (elapsed >= AxisInstructionLeaseSeconds)
                    {
                        // 逾期释放：漏调 EndAxisInstruction 的异常路径不得让该轴永久 BUSY
                        _axisInstructions.Remove(axisName);
                        try { busy.Cancel.Dispose(); }
                        // CTS 释放：失败无影响
                        catch (Exception) { }
                        Log(string.Format("指令登记逾期释放 轴 {0} {1} 已占用 {2:F0}s（超过 {3:F0}s 视为残留登记）",
                            axisName, busy.InstructionName, elapsed, AxisInstructionLeaseSeconds),
                            MessageLevel.Warning);
                    }
                    else
                    {
                        reason = string.Format("正在执行 {0}（已 {1:F1}s），暂不接受新指令",
                            busy.InstructionName, elapsed);
                        Log(string.Format("指令被拒 轴 {0} {1}", axisName, reason), MessageLevel.Warning);
                        return false;
                    }
                }
                AxisInstructionEntry entry = new AxisInstructionEntry
                {
                    InstructionName = instructionName,
                    StartedUtc = DateTime.UtcNow,
                    Cancel = new CancellationTokenSource()
                };
                _axisInstructions[axisName] = entry;
                token = entry.Cancel.Token;
            }
            Log(string.Format("指令开始 轴 {0} {1}",
                axisName, instructionName), MessageLevel.Debug);
            return true;
        }

        /// <summary>登记某轴指令结束（成功/失败/异常/取消均经 finally 调用）</summary>
        /// <param name="axisName">轴名</param>
        public void EndAxisInstruction(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) axisName = "未知轴";
            AxisInstructionEntry entry;
            lock (_axisInstructionLock)
            {
                if (!_axisInstructions.TryGetValue(axisName, out entry)) return;
                _axisInstructions.Remove(axisName);
            }
            try { entry.Cancel.Dispose(); }
            // CTS 释放：失败无影响
            catch (Exception) { }
        }

        /// <summary>该轴当前是否有指令在执行</summary>
        /// <param name="axisName">轴名</param>
        /// <returns>占用返回 true</returns>
        public bool IsAxisInstructionBusy(string axisName)
        {
            lock (_axisInstructionLock)
            {
                return axisName != null && _axisInstructions.ContainsKey(axisName);
            }
        }

        /// <summary>该轴当前执行中的指令描述（空闲返回空串）</summary>
        /// <param name="axisName">轴名</param>
        /// <returns>可读描述</returns>
        public string GetAxisInstructionBusyText(string axisName)
        {
            lock (_axisInstructionLock)
            {
                AxisInstructionEntry busy = axisName != null && _axisInstructions.ContainsKey(axisName)
                    ? _axisInstructions[axisName] : null;
                if (busy == null) return string.Empty;
                return string.Format("正在执行 {0}（已 {1:F1}s）", busy.InstructionName,
                    (DateTime.UtcNow - busy.StartedUtc).TotalSeconds);
            }
        }

        /// <summary>请求取消全部轴的指令状态检测</summary>

        /// <param name="reason">取消原因（日志用）</param>
        public void CancelAllAxisInstructions(string reason)
        {
            List<string> names = new List<string>();
            lock (_axisInstructionLock)
            {
                foreach (string name in _axisInstructions.Keys) names.Add(name);
            }
            if (names.Count == 0) return;
            Log(string.Format("请求取消指令状态检测 {0} 条 原因 {1}", names.Count, reason),
                MessageLevel.Warning);
            for (int i = 0; i < names.Count; i++) CancelAxisInstruction(names[i]);
        }

        /// <summary>等待运动轴到位</summary>

        /// <param name="axis">运动轴</param>
        /// <param name="targetMm">目标位置（mm）</param>
        /// <param name="tolMm">到位容差（mm）</param>
        /// <param name="token">取消标记</param>
        /// <param name="evidence">实测证据（成功为到位摘要，失败含当前与目标）</param>
        /// <returns>达成返回 true</returns>
        public bool WaitAxisAtTarget(MontionAxis axis, double targetMm, double tolMm,
            CancellationToken token, out string evidence)
        {
            return WaitAxisAtTargetCore(axis, targetMm, tolMm, EffectiveDetectTimeoutSeconds, 0.0, true,
                () => token.IsCancellationRequested, out evidence);
        }

        /// <summary>等待运动轴到位</summary>

        /// <param name="axis">运动轴</param>
        /// <param name="targetMm">目标位置（mm）</param>
        /// <param name="tolMm">到位容差（mm）</param>
        /// <param name="timeoutSeconds">超时秒数（≤0 回退到 FLOW_TIMEOUT 指令检测超时）</param>
        /// <param name="noChangeSeconds">位置无变化早停秒数（≤0 表示不启用）</param>
        /// <param name="cancelRequested">外部取消判定（可传 null）</param>
        /// <param name="evidence">实测证据</param>
        /// <returns>达成返回 true</returns>
        public bool WaitAxisAtTarget(MontionAxis axis, double targetMm, double tolMm,
            double timeoutSeconds, double noChangeSeconds, Func<bool> cancelRequested, out string evidence)
        {
            return WaitAxisAtTargetCore(axis, targetMm, tolMm, timeoutSeconds, noChangeSeconds, false,
                cancelRequested, out evidence);
        }

        /// <summary>按绝对坐标下发点位运动（不等待）</summary>
        /// <param name="axis">运动轴</param>
        /// <param name="targetMm">目标位置（mm）</param>
        /// <param name="velMmPerSec">移动速度（mm/s）</param>
        /// <param name="tolMm">已在位判定容差（mm）</param>
        /// <param name="alreadyInPosition">已在容差内（未发起运动）</param>
        /// <param name="rejectReason">被拒/下发失败的可读原因</param>
        /// <returns>指令已下发或已在位返回 true</returns>
        public bool TryMoveAbsoluteNoBlock(MontionAxis axis, double targetMm, double velMmPerSec, double tolMm,
            out bool alreadyInPosition, out string rejectReason)
        {
            alreadyInPosition = false;
            rejectReason = string.Empty;
            if (axis == null) { rejectReason = "轴对象为空"; return false; }
            if (velMmPerSec <= 0.0)
            {
                rejectReason = string.Format("速度 {0:F3} mm/s 非法（须大于 0）", velMmPerSec);
                return false;
            }
            if (tolMm < 0.0)
            {
                rejectReason = string.Format("到位容差 {0:F3} mm 非法（须大于等于 0）", tolMm);
                return false;
            }

            double current = axis.GetPosition();
            if (Math.Abs(current - targetMm) <= tolMm)
            {
                alreadyInPosition = true;
                Log(string.Format("已在目标位 当前 {0:F3}mm 目标 {1:F3}mm 容差 {2:F3}mm，跳过下发",
                    current, targetMm, tolMm));
                return true;
            }

            if (!axis.AbsMoveNoBlock(targetMm, velMmPerSec, velMmPerSec, velMmPerSec,
                    axis.AccTime, axis.DecTime, axis.STime, 1))
            {
                rejectReason = "点位指令下发失败（详见前一条日志）";
                return false;
            }
            return true;
        }

        /// <summary>等待运动轴回零完成</summary>

        /// <param name="axis">运动轴</param>
        /// <param name="token">取消标记</param>
        /// <param name="evidence">实测证据</param>
        /// <returns>达成返回 true</returns>
        public bool WaitAxisHomed(MontionAxis axis, CancellationToken token, out string evidence)
        {
            evidence = string.Empty;
            if (axis == null) { evidence = "轴对象为空"; return false; }
            DateTime deadline = DateTime.UtcNow.AddSeconds(EffectiveDetectTimeoutSeconds);
            int stable = 0;
            while (true)
            {
                if (token != null && token.IsCancellationRequested)
                {
                    evidence = "检测被取消"; return false;
                }

                double pos = axis.GetPosition();
                double speed = Math.Abs(axis.GetCurrentSpeed());
                AxisStatus status = AxisStatus.Read(axis);
                ushort homeStatus = 0;
                bool homeVerified = true;
                if (axis is DeltaMontionAxis delta)
                {
                    homeStatus = delta.GetHomeStatus();
                    homeVerified = delta.HomeVerified;
                }

                // 等价：原「axis.IsAlarm() || (status & 0x0008) != 0」，理由同 WaitAxisAtTarget
                if (status.Fault)
                {
                    evidence = string.Format("驱动器报警 状态字 0x{0:X4} Home_Status {1} 当前 {2:F3}mm",
                        status.Raw, homeStatus, pos);
                    return false;
                }
                if (homeStatus == 3)
                {
                    evidence = string.Format("回零异常 Home_Status 3 当前 {0:F3}mm", pos);
                    return false;
                }

                // 期望回零坐标只用于位置偏差可观测，不再作完成硬门：
                // 台达扭矩回零模式（如 -3）下驱动器把 offset 反向应用（填 +1.0mm → 终位 -1.0mm），
                // 拿 expectHome 作硬判据会把「已由 Home_Status 确认完成」误判为 NOT_REACHED。
                double expectHome = 0.0;
                if (axis is DeltaMontionAxis homeAxis) expectHome = homeAxis.HomeOffset;

                // 权威信号：驱动器回零完成（Home_Status=0）+ 本轮已确认启动（HomeVerified）。
                // 位置与速度仅用于稳定采样与告警，不阻断达成。
                if (homeVerified && homeStatus == 0 && speed <= InstructionStableSpeedMmPerSec)
                {
                    stable++;
                    if (stable >= EffectiveStableSampleCount)
                    {
                        double after = axis.GetPosition();
                        // 位置偏差只记 Warning：现场 offset 符号约定可能与配置值相反，须可见但不拦完成
                        if (Math.Abs(after - expectHome) > InstructionDefaultTolMm)
                        {
                            Log(string.Format("回零位置与期望不符 期望 {0:F3}mm 实测 {1:F3}mm 偏差 {2:F3}mm（已完成，仅告警）",
                                expectHome, after, after - expectHome), MessageLevel.Warning);
                        }
                        if (Math.Abs(after - pos) > InstructionDefaultTolMm)
                        {
                            Log(string.Format("回零后位置漂移 偏差 {0:F3}mm 前 {1:F3}mm 后 {2:F3}mm",
                                after - pos, pos, after));
                        }
                        if (ReferenceEquals(axis, GetDeltaAxisByKeys(TaAxisKeys)))
                        {
                            TaHomed = true;
                            Log(string.Format("TA 回零完成 期望 {0:F3}mm 实测 {1:F3}mm", expectHome, after),
                                MessageLevel.Debug);
                            // 满扭矩只服务回零，成功即回落配置上限
                            if (axis is DeltaMontionAxis taAxis) taAxis.RestoreTorqueLimitAfterHome();
                        }
                        evidence = string.Format("回零达成 期望 {0:F3}mm 当前 {1:F3}mm 速度 {2:F3}mm/s 状态字 0x{3:X4}",
                            expectHome, after, speed, status.Raw);
                        return true;
                    }
                }
                else stable = 0;

                if (DateTime.UtcNow >= deadline)
                {
                    evidence = string.Format("超时 {0:F1}s 未回零完成 Home_Status {1} 已确认 {2} 当前 {3:F3}mm 速度 {4:F3}mm/s 状态字 0x{5:X4}",
                        EffectiveDetectTimeoutSeconds, homeStatus, homeVerified ? "是" : "否", pos, speed, status.Raw);
                    return false;
                }

                try { Thread.Sleep(EffectiveSampleIntervalMs); }
                // 指令被取消的正常出口，非异常
                catch (ThreadInterruptedException) { }
            }
        }

        /// <summary>TA 出力达成：TorqueOutputEnabled ∧ 模式字 CST ∧ 无报警 ∧ 状态字 Operation Enabled；超时 5s</summary>
        /// <param name="axis">TA 轴</param>
        /// <param name="token">取消标记</param>
        /// <param name="evidence">实测证据</param>
        /// <returns>达成返回 true</returns>
        public bool WaitTaTorqueOn(DeltaMontionAxis axis, CancellationToken token, out string evidence)
        {
            evidence = string.Empty;
            if (axis == null) { evidence = "TA 轴对象为空"; return false; }
            DateTime deadline = DateTime.UtcNow.AddSeconds(EffectiveDetectTimeoutSeconds);
            int stable = 0;
            while (true)
            {
                if (token != null && token.IsCancellationRequested)
                {
                    evidence = "检测被取消"; return false;
                }

                ushort mode = axis.GetMoveMode();
                AxisStatus status = AxisStatus.Read(axis);
                // 等价：原「axis.IsAlarm() || (status & 0x0008) != 0」
                bool alarm = status.Fault;
                if (alarm)
                {
                    evidence = string.Format("驱动器报警 状态字 0x{0:X4} 模式 {1}", status.Raw, mode);
                    return false;
                }

                bool met = axis.TorqueOutputEnabled && mode == ModeCst && status.OperationEnabled;
                if (met)
                {
                    stable++;
                    if (stable >= EffectiveTorqueConfirmSamples)
                    {
                        evidence = string.Format("出力已生效 模式 CST(10) 输出位 1 状态字 0x{0:X4}", status.Raw);
                        return true;
                    }
                }
                else stable = 0;

                if (DateTime.UtcNow >= deadline)
                {
                    evidence = string.Format("超时 {0:F1}s 未进入出力态 输出位 {1} 模式 {2} 状态字 0x{3:X4}",
                        EffectiveDetectTimeoutSeconds, axis.TorqueOutputEnabled ? "1" : "0", mode, status.Raw);
                    return false;
                }

                try { Thread.Sleep(EffectiveSampleIntervalMs); }
                // 指令被取消的正常出口，非异常
                catch (ThreadInterruptedException) { }
            }
        }

        /// <summary>TA 撤力达成</summary>

        /// <param name="axis">TA 轴</param>
        /// <param name="holdMm">锁存位置（mm）</param>
        /// <param name="tolMm">位置容差（mm）</param>
        /// <param name="token">取消标记</param>
        /// <param name="evidence">实测证据</param>
        /// <returns>达成返回 true</returns>
        public bool WaitTaTorqueOff(DeltaMontionAxis axis, double holdMm, double tolMm,
            CancellationToken token, out string evidence)
        {
            evidence = string.Empty;
            if (axis == null) { evidence = "TA 轴对象为空"; return false; }
            if (tolMm <= 0.0) tolMm = InstructionDefaultTolMm;
            DateTime deadline = DateTime.UtcNow.AddSeconds(EffectiveDetectTimeoutSeconds);
            int stable = 0;
            while (true)
            {
                if (token != null && token.IsCancellationRequested)
                {
                    evidence = "检测被取消"; return false;
                }

                ushort mode = axis.GetMoveMode();
                AxisStatus status = AxisStatus.Read(axis);
                double pos = axis.GetPosition();
                // 等价：原「axis.IsAlarm() || (status & 0x0008) != 0」
                if (status.Fault)
                {
                    evidence = string.Format("驱动器报警 状态字 0x{0:X4} 当前 {1:F3}mm", status.Raw, pos);
                    return false;
                }

                bool met = !axis.TorqueOutputEnabled && mode == ModeCsp
                    && Math.Abs(pos - holdMm) <= tolMm;
                if (met)
                {
                    stable++;
                    if (stable >= EffectiveTorqueConfirmSamples)
                    {
                        evidence = string.Format("撤力达成 输出位 0 模式 CSP(8) 当前 {0:F3}mm 锁存 {1:F3}mm 容差 {2:F3}mm 状态字 0x{3:X4}",
                            pos, holdMm, tolMm, status.Raw);
                        return true;
                    }
                }
                else stable = 0;

                if (DateTime.UtcNow >= deadline)
                {
                    evidence = string.Format("超时 {0:F1}s 未完成撤力 输出位 {1} 模式 {2} 当前 {3:F3}mm 锁存 {4:F3}mm 状态字 0x{5:X4}",
                        EffectiveDetectTimeoutSeconds, axis.TorqueOutputEnabled ? "1" : "0",
                        mode, pos, holdMm, status.Raw);
                    return false;
                }

                try { Thread.Sleep(EffectiveSampleIntervalMs); }
                // 指令被取消的正常出口，非异常
                catch (ThreadInterruptedException) { }
            }
        }

        /// <summary>等待 LA 对焦轴到位</summary>

        /// <param name="targetMm">目标位置（mm）</param>
        /// <param name="token">取消标记</param>
        /// <param name="evidence">实测证据</param>
        /// <returns>达成返回 true</returns>
        public bool WaitLaMotorAtTarget(double targetMm, CancellationToken token, out string evidence)
        {
            evidence = string.Empty;
            WeldHeadSlaveDevice head = WeldHead;
            if (head == null) { evidence = "焊接头从站未就绪"; return false; }
            DateTime deadline = DateTime.UtcNow.AddSeconds(EffectiveDetectTimeoutSeconds);
            int stable = 0;
            double lastPos = double.NaN;
            while (true)
            {
                if (token != null && token.IsCancellationRequested)
                {
                    evidence = "检测被取消"; return false;
                }

                MotorState? state = head.ReadMotorStatus();
                if (!state.HasValue)
                {
                    evidence = "LA 电机状态读取失败";
                    return false;
                }
                double pos = state.Value.CurrentPositionMm;
                if (!double.IsNaN(lastPos) && Math.Abs(pos - lastPos) <= 0.001) stable++;
                else stable = 0;
                lastPos = pos;

                bool inBand = Math.Abs(pos - targetMm) <= InstructionDefaultTolMm;
                if (inBand && stable >= EffectiveStableSampleCount - 1)
                {
                    evidence = string.Format("到位 当前 {0:F3}mm 目标 {1:F3}mm 容差 {2:F3}mm 电机状态 {3}",
                        pos, targetMm, InstructionDefaultTolMm, state.Value.State);
                    return true;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    evidence = string.Format("超时 {0:F1}s 未到位 当前 {1:F3}mm 目标 {2:F3}mm 容差 {3:F3}mm 电机状态 {4}",
                        EffectiveDetectTimeoutSeconds, pos, targetMm, InstructionDefaultTolMm, state.Value.State);
                    return false;
                }

                try { Thread.Sleep(EffectiveSampleIntervalMs); }
                // 指令被取消的正常出口，非异常
                catch (ThreadInterruptedException) { }
            }
        }

        #endregion

        #endregion

        #region 构造函数

        private MontionManager()
        {
            // 配置迁移在 Program.Main 首步执行，先于本构造

            Axes = new AxisRegistry();
            IOs = new IORegistry();
            Cylinders = new CylinderRegistry();

            // 运控配置唯一加载点：构造时读一次
            // 其它入口不得重复加载
            MoveData = new MoveControlData();
            MoveData.Load();

            WeldHeadConfig = new WeldHeadConfig();
            LoadWeldHeadConfig();
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>取消某轴在途指令的状态检测（停止与急停共用）</summary>
        /// <param name="axisName">轴名（单飞隔离键）</param>
        private void CancelAxisInstruction(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) return;
            CancellationTokenSource cancel;
            lock (_axisInstructionLock)
            {
                if (!_axisInstructions.TryGetValue(axisName, out AxisInstructionEntry entry)) return;
                cancel = entry.Cancel;
            }
            try { cancel.Cancel(); }
            // CTS 取消：失败无影响
            catch (Exception) { }
        }

        /// <summary>按配置表创建全部轴并登记进注册表</summary>
        private void CreateAxesFromConfig()
        {
            DeltaMotionConfig config = MoveData.MotionConfig;
            for (int i = 0; i < config.Axes.Count; i++)
            {
                DeltaAxisConfig cfg = config.Axes[i];
                if (!cfg.Enabled) continue;
                if (config.ConflictingDeviceKeys.Contains(DeltaMotionConfig.AxisOwnerKey(cfg.AxisName)))
                {
                    Log("轴 " + cfg.AxisName + " 节点号撞号，跳过装载", MessageLevel.Error);
                    continue;
                }

                DeltaMontionAxis axis = new DeltaMontionAxis(MontionControl, cfg.AxisName, cfg.NodeId, cfg.SlotNo);
                ApplyAxisConfig(axis, cfg);
                ApplyTaHomeOnLoad(cfg, axis);
                Axes.Register(cfg.AxisName, axis);
            }
            // 重建后 TA 先降级，等配置加载通过再打开
            _taConfigLoadOk = false;
            _taDegradedReason = TaReasonConfigNotLoaded;
            SetTaZeroPoseGate(false, null);
            Log(string.Format("按配置创建轴 {0} 个", Axes.Count), MessageLevel.Debug);
        }

        /// <summary>装载 TA 轴时按其特有参数组（ROOT/TA_HOME）重新套用回零六参</summary>
        /// <param name="cfg">配置对象。</param>
        /// <param name="axis">轴对象。</param>
        private void ApplyTaHomeOnLoad(DeltaAxisConfig cfg, MontionAxis axis)
        {
            DeltaMotionConfig config = MoveData?.MotionConfig;
            DeltaTaHomeConfig taHome = config?.TaHome;
            if (cfg == null || axis == null || taHome == null || config.TaAxisName == null) return;
            if (!string.Equals(cfg.AxisName, config.TaAxisName, StringComparison.OrdinalIgnoreCase)) return;

            if (!(axis is DeltaMontionAxis taAxis)) return;
            taAxis.HomeMode = taHome.HomeMode;
            taAxis.HomeFirstVel = taHome.HomeFirstVel;
            taAxis.HomeSecondVel = taHome.HomeSecondVel;
            taAxis.HomeAcc = taHome.HomeAcc;
            // 扭矩上限：CSP 与 CST 共用，下次使能写 16#6072
            taAxis.TorqueLimitPerMille = taHome.GetTorqueLimitPerMille();
            // 回零满扭矩：驱动侧放满，软件侧仍按配置钳制
            taAxis.FullTorqueOnEnable = true;
            Log(string.Format("TA 轴已按 ROOT/TA_HOME 装载特有参数 回零模式 {0} 寻找速度 {1} rpm 爬行速度 {2} rpm 加速 {3} 毫秒 扭矩上限 {4:F2}N（{5}‰）",
                taHome.HomeMode, taHome.HomeFirstVel, taHome.HomeSecondVel, taHome.HomeAcc,
                taHome.TorqueLimitN, taAxis.TorqueLimitPerMille), MessageLevel.Debug);
            Log("TA 回零扭矩：驱动侧取满扭矩，软件钳制仍按 TA_TORQUE_LIMIT_N", MessageLevel.Debug);
        }

        /// <summary>把轴配置参数套用到轴对象</summary>
        /// <param name="axis">轴对象</param>
        /// <param name="cfg">轴配置</param>
        private static void ApplyAxisConfig(MontionAxis axis, DeltaAxisConfig cfg)
        {
            axis.Equiv = cfg.Equiv;
            axis.MinVel = cfg.StrVel;
            axis.MaxVel = cfg.ConstVel;
            axis.AccTime = cfg.AccTime;
            axis.DecTime = cfg.DecTime;
            axis.STime = cfg.STime;
            axis.StopVel = cfg.EndVel;
            axis.HomeMode = cfg.HomeMode;
            // 回零五参无抽象属性，向下转型写入台达轴
            if (axis is DeltaMontionAxis dAxis)
            {
                dAxis.HomeOffset = cfg.HomeOffset;
                dAxis.HomeFirstVel = cfg.HomeFirstVel;
                dAxis.HomeSecondVel = cfg.HomeSecondVel;
                dAxis.HomeAcc = cfg.HomeAcc;
            }
        }

        /// <summary>按配置表创建全部输入与输出并登记进注册表</summary>
        private void CreateIOsFromConfig()
        {
            DeltaMotionConfig config = MoveData.MotionConfig;

            for (int i = 0; i < config.Inputs.Count; i++)
            {
                DeltaIOConfig cfg = config.Inputs[i];
                if (!cfg.Enabled) continue;

                InputIO io = new DeltaInputIO(MontionControl, cfg.IOName, cfg.NodeId, cfg.SlotNo)
                {
                    IONo = cfg.BitNo,
                    IOEnable = cfg.Enabled
                };
                IOs.RegisterInput(cfg.IOName, io);
            }

            for (int i = 0; i < config.Outputs.Count; i++)
            {
                DeltaIOConfig cfg = config.Outputs[i];
                if (!cfg.Enabled) continue;

                OutputIO io = new DeltaOutputIO(MontionControl, cfg.IOName, cfg.NodeId, cfg.SlotNo)
                {
                    IONo = cfg.BitNo,
                    IOEnable = cfg.Enabled
                };
                IOs.RegisterOutput(cfg.IOName, io);
            }

            Log(string.Format("按配置创建输入 {0} 个 输出 {1} 个", IOs.InputCount, IOs.OutputCount), MessageLevel.Debug);
        }

        /// <summary>按配置表装配全部气缸并登记进注册表</summary>
        private void CreateCylindersFromConfig()
        {
            DeltaMotionConfig config = MoveData.MotionConfig;
            for (int i = 0; i < config.Cylinders.Count; i++)
            {
                DeltaCylinderConfig cfg = config.Cylinders[i];

                Cylinder cylinder = Cylinder.Builder(cfg.CylinderName);
                if (cfg.StretchSensorName.Length > 0)
                {
                    InputIO io = IOs.GetInput(cfg.StretchSensorName);
                    if (io == null)
                    {
                        Log(string.Format("气缸 {0} 伸出传感器 {1} 未注册，跳过装配",
                            cfg.CylinderName, cfg.StretchSensorName), MessageLevel.Warning);
                        continue;
                    }
                    cylinder.SetInStretchSensor(io);
                }
                if (cfg.RetractSensorName.Length > 0)
                {
                    InputIO io = IOs.GetInput(cfg.RetractSensorName);
                    if (io == null)
                    {
                        Log(string.Format("气缸 {0} 缩回传感器 {1} 未注册，跳过装配",
                            cfg.CylinderName, cfg.RetractSensorName), MessageLevel.Warning);
                        continue;
                    }
                    cylinder.SetInRetractSensor(io);
                }
                if (cfg.CenterSensorName.Length > 0)
                {
                    InputIO io = IOs.GetInput(cfg.CenterSensorName);
                    if (io == null)
                    {
                        Log(string.Format("气缸 {0} 中间传感器 {1} 未注册，跳过装配",
                            cfg.CylinderName, cfg.CenterSensorName), MessageLevel.Warning);
                        continue;
                    }
                    cylinder.SetInCenterSensor(io);
                }
                if (cfg.StretchOutputName.Length > 0)
                {
                    OutputIO io = IOs.GetOutput(cfg.StretchOutputName);
                    if (io == null)
                    {
                        Log(string.Format("气缸 {0} 伸出电磁阀 {1} 未注册，跳过装配",
                            cfg.CylinderName, cfg.StretchOutputName), MessageLevel.Warning);
                        continue;
                    }
                    cylinder.SetOutCylinderStretch(io, cfg.StretchActionName.Length > 0 ? cfg.StretchActionName : null);
                }
                if (cfg.RetractOutputName.Length > 0)
                {
                    OutputIO io = IOs.GetOutput(cfg.RetractOutputName);
                    if (io == null)
                    {
                        Log(string.Format("气缸 {0} 缩回电磁阀 {1} 未注册，跳过装配",
                            cfg.CylinderName, cfg.RetractOutputName), MessageLevel.Warning);
                        continue;
                    }
                    cylinder.SetOutCylinderRetract(io, cfg.RetractActionName.Length > 0 ? cfg.RetractActionName : null);
                }

                Cylinders.Register(cfg.CylinderName, cylinder);
            }
            Log(string.Format("按配置装配气缸 {0} 个", Cylinders.Count), MessageLevel.Debug);
        }

        /// <summary>按配置装载焊接头从站设备（握手 + 下发阈值与振</summary>
        private void CreateWeldHeadFromConfig()
        {
            DeltaMotionConfig config = MoveData.MotionConfig;
            if (config.WeldHeadSlave == null)
            {
                Log("焊接头从站未配置，跳过装载", MessageLevel.Debug);
                return;
            }
            if (!config.WeldHeadSlave.Enabled)
            {
                Log("焊接头从站未启用，跳过装载", MessageLevel.Debug);
                return;
            }
            if (!IsNodeDetected(config.WeldHeadSlave.NodeId))
            {
                Log("焊接头从站未连接，跳过装载", MessageLevel.Debug);
                return;
            }
            if (config.ConflictingDeviceKeys.Contains(DeltaMotionConfig.WeldHeadOwnerKey))
            {
                Log("焊接头从站节点号撞号，跳过装载", MessageLevel.Error);
                return;
            }

            try
            {
                WeldHead = new WeldHeadSlaveDevice(config.CardNo, config.WeldHeadSlave.NodeId)
                {
                    TemperatureAlarmThreshold = WeldHeadConfig == null ? 0.0 : WeldHeadConfig.TempAlarmThreshold
                };
                if (!WeldHead.Initialize())
                {
                    Log("焊接头从站初始化失败", MessageLevel.Error);
                    return;
                }

                WeldHead.SetAlarmThresholds(WeldHeadConfig.TempWarnThreshold, WeldHeadConfig.TempAlarmThreshold,
                    WeldHeadConfig.RiseWarnThreshold, WeldHeadConfig.RiseAlarmThreshold,
                    WeldHeadConfig.PressureAlarmThreshold, WeldHeadConfig.PressureDelayMs);

                // 电机脉冲当量（量纲可配置，默认 10 计数/mm）
                WeldHead.MotorPulsePerMm = WeldHeadConfig.MotorPulsePerMm;

                // 初始下发振镜逻辑零位，非协议默认参数
                WeldHead.SetGalvoParams(GalvoParams.CreateLogicalHome());
                Log(string.Format("按配置装载焊接头从站 卡号 {0} 从站 {1} 电机量纲 {2:F0}计数每毫米",
                    config.CardNo, config.WeldHeadSlave.NodeId, WeldHeadConfig.MotorPulsePerMm),
                    MessageLevel.Debug);
            }
            catch (Exception ex)
            {
                Log("焊接头从站装载异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>读取整数配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">默认值</param>
        /// <returns>读取到的整数</returns>
        private static int ReadWeldHeadInt(XdocumentReaderWriter reader, string path, int def)
        {
            return int.TryParse(reader.GetElementValueByPath(path, def.ToString()), out int val) ? val : def;
        }

        /// <summary>读取双精度配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">默认值</param>
        /// <returns>读取到的浮点数</returns>
        private static double ReadWeldHeadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            return double.TryParse(reader.GetElementValueByPath(path, def.ToString()),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double val) ? val : def;
        }

        /// <summary>读取元素属性值（缺失返回空串）</summary>
        /// <param name="element">元素</param>
        /// <param name="name">属性名</param>
        /// <returns>属性值</returns>
        private static string ReadAttribute(XElement element, string name)
        {
            if (element == null) return string.Empty;
            XAttribute attribute = element.Attribute(name);
            return attribute == null ? string.Empty : attribute.Value;
        }

        /// <summary>按不变文化写出文本</summary>
        /// <param name="value">待格式化数值</param>
        /// <returns>格式化后的文本</returns>
        private static string WeldHeadText(double value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>振镜图形 → XML 节点名（GALVO 下的分档子节点）。</summary>
        /// <param name="pattern">图形</param>
        /// <returns>节点名（CIRCLE / EIGHT / LINE / ARC）</returns>
        private static string GalvoShapeNodeName(WsgGalvoPattern pattern)
        {
            switch (pattern)
            {
                case WsgGalvoPattern.Circle: return "CIRCLE";
                case WsgGalvoPattern.Eight: return "EIGHT";
                case WsgGalvoPattern.Arc: return "ARC";
                default: return "LINE";
            }
        }

        /// <summary>初始化第 2 步：装载 TA 轴配置</summary>

        /// <returns>TA 轴与 TA_HOME 均存在返回 true</returns>
        public bool LoadTaAxisConfig()
        {
            _taConfigLoadOk = false;
            SetTaZeroPoseGate(false, null);
            DeltaTaHomeConfig taHome = MoveData?.MotionConfig?.TaHome;
            DeltaMontionAxis axis = GetDeltaAxisByKeys(TaAxisKeys);
            if (taHome == null || axis == null)
            {
                _taDegradedReason = "TA 轴未装配（配置未启用或轴名未匹配 TA 关键字，或 TA_HOME 缺失）";
                Log("TA 轴配置加载失败 " + _taDegradedReason, MessageLevel.Error);
                return false;
            }

            Log(string.Format("TA 轴配置加载开始 轴 {0} 回零模式 {1} 寻找速度 {2}rpm 爬行速度 {3}rpm 加速 {4}ms 偏移 {5}",
                axis.AxisName, taHome.HomeMode, taHome.HomeFirstVel, taHome.HomeSecondVel,
                taHome.HomeAcc, axis.HomeOffset), MessageLevel.Debug);

            _taConfigLoadOk = true;
            _taDegradedReason = string.Empty;
            Log("TA 轴配置加载成功", MessageLevel.Debug);
            return true;
        }
        /// <summary>保存焊接头配置，末尾落盘</summary>
        public void SaveWeldHeadConfig()
        {
            XmlConfigFile file = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "WeldHeadSlave");
            XdocumentReaderWriter reader = file.Raw;
            ConfigFileManager.SaveXmlWithAudit(file, "焊接头配置", "配置变更", () =>
            {
            reader.NewXdocument("ROOT", "1.0", "UTF-8");

            var threshold = reader.AddElement("THRESHOLD");
            reader.AddElement(threshold, "TEMP_WARN", WeldHeadText(WeldHeadConfig.TempWarnThreshold));
            reader.AddElement(threshold, "TEMP_ALARM", WeldHeadText(WeldHeadConfig.TempAlarmThreshold));
            reader.AddElement(threshold, "RISE_WARN", WeldHeadText(WeldHeadConfig.RiseWarnThreshold));
            reader.AddElement(threshold, "RISE_ALARM", WeldHeadText(WeldHeadConfig.RiseAlarmThreshold));
            reader.AddElement(threshold, "PRESSURE_ALARM", WeldHeadText(WeldHeadConfig.PressureAlarmThreshold));
            reader.AddElement(threshold, "PRESSURE_DELAY_MS", WeldHeadConfig.PressureDelayMs.ToString());

            var galvo = reader.AddElement("GALVO");
            // 顶层不再写默认长宽频
            //    宽/长/频一律落在 GALVO/<图形>/ 子节点；顶层仅留全局项。
            reader.AddElement(galvo, "ANGLE", WeldHeadConfig.DefaultGalvo.Angle.ToString());
            reader.AddElement(galvo, "START_ANGLE", WeldHeadText(WeldHeadConfig.DefaultGalvo.StartAngle));
            reader.AddElement(galvo, "END_ANGLE", WeldHeadText(WeldHeadConfig.DefaultGalvo.EndAngle));
            reader.AddElement(galvo, "X_CENTER", WeldHeadText(WeldHeadConfig.DefaultGalvo.XCenter));
            reader.AddElement(galvo, "Y_CENTER", WeldHeadText(WeldHeadConfig.DefaultGalvo.YCenter));
            reader.AddElement(galvo, "X_COMPRESSION", WeldHeadConfig.DefaultGalvo.XCompression.ToString());
            reader.AddElement(galvo, "Y_COMPRESSION", WeldHeadConfig.DefaultGalvo.YCompression.ToString());
            reader.AddElement(galvo, "TARGET_SIZE", WeldHeadText(WeldHeadConfig.DefaultGalvo.TargetSize));
            reader.AddElement(galvo, "OBLIQUE_CORRECTION", WeldHeadConfig.DefaultGalvo.ObliqueEllipseCorrection.ToString());

            // 按图形分别存宽长频
            foreach (WsgGalvoPattern pattern in WeldHeadConfig.GalvoPatternOrder)
            {
                GalvoShapeParams shape = WeldHeadConfig.GetGalvoShape(pattern);
                var node = reader.AddElement(galvo, GalvoShapeNodeName(pattern));
                reader.AddElement(node, "WIDTH", WeldHeadText(shape.Width));
                reader.AddElement(node, "LENGTH", WeldHeadText(shape.Length));
                reader.AddElement(node, "FREQUENCY", shape.Frequency.ToString());
            }

            var motor = reader.AddElement("MOTOR");
            reader.AddElement(motor, "PULSE_PER_MM", WeldHeadText(WeldHeadConfig.MotorPulsePerMm));

            var ioMap = reader.AddElement("IO_MAP");
            ioMap.SetAttributeValue("SOURCE_FILE", WeldHeadConfig.IoMapSourceFile ?? string.Empty);
            ioMap.SetAttributeValue("SOURCE_TIME", WeldHeadConfig.IoMapSourceTime ?? string.Empty);
            ioMap.SetAttributeValue("ROW_COUNT", WeldHeadConfig.IoMapSourceRows.ToString());
            for (int i = 0; i < WeldHeadConfig.PdoMap.Count; i++)
            {
                WeldHeadPdoEntry entry = WeldHeadConfig.PdoMap[i];
                Dictionary<string, string> attributes = new Dictionary<string, string>();
                attributes["SEQ"] = entry.Seq ?? string.Empty;
                attributes["VAR"] = entry.VarName ?? string.Empty;
                attributes["NAME"] = entry.DisplayName ?? string.Empty;
                attributes["TYPE"] = entry.DataType ?? string.Empty;
                attributes["SDO_INDEX"] = entry.SdoIndex ?? string.Empty;
                attributes["SDO_SUB"] = entry.SdoSubIndex ?? string.Empty;
                attributes["ACCESS"] = entry.Access ?? string.Empty;
                attributes["PDO_INDEX"] = entry.PdoIndex ?? string.Empty;
                attributes["PDO_SUB"] = entry.PdoSubIndex ?? string.Empty;
                attributes["DESC"] = entry.Description ?? string.Empty;
                reader.AddElementWithAttributes(ioMap, "IO", attributes);
            }

            reader.SaveXdocument();
            });
            Log("焊接头配置保存完成");
        }

        /// <summary>加载焊接头配置（阈值与振镜默认参数），读取失败</summary>
        public void LoadWeldHeadConfig()
        {
            XdocumentReaderWriter reader = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "WeldHeadSlave").Raw;
            if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
            {
                Log("焊接头配置为空，保留默认参数", MessageLevel.Warning);
                return;
            }

            WeldHeadConfig.TempWarnThreshold = ReadWeldHeadDouble(reader, "THRESHOLD/TEMP_WARN", WeldHeadConfig.TempWarnThreshold);
            WeldHeadConfig.TempAlarmThreshold = ReadWeldHeadDouble(reader, "THRESHOLD/TEMP_ALARM", WeldHeadConfig.TempAlarmThreshold);
            WeldHeadConfig.RiseWarnThreshold = ReadWeldHeadDouble(reader, "THRESHOLD/RISE_WARN", WeldHeadConfig.RiseWarnThreshold);
            WeldHeadConfig.RiseAlarmThreshold = ReadWeldHeadDouble(reader, "THRESHOLD/RISE_ALARM", WeldHeadConfig.RiseAlarmThreshold);
            WeldHeadConfig.PressureAlarmThreshold = ReadWeldHeadDouble(reader, "THRESHOLD/PRESSURE_ALARM", WeldHeadConfig.PressureAlarmThreshold);
            WeldHeadConfig.PressureDelayMs = ReadWeldHeadInt(reader, "THRESHOLD/PRESSURE_DELAY_MS", WeldHeadConfig.PressureDelayMs);

            WeldHeadConfig.DefaultGalvo.Angle = ReadWeldHeadInt(reader, "GALVO/ANGLE", WeldHeadConfig.DefaultGalvo.Angle);
            WeldHeadConfig.DefaultGalvo.StartAngle = ReadWeldHeadDouble(reader, "GALVO/START_ANGLE", WeldHeadConfig.DefaultGalvo.StartAngle);
            WeldHeadConfig.DefaultGalvo.EndAngle = ReadWeldHeadDouble(reader, "GALVO/END_ANGLE", WeldHeadConfig.DefaultGalvo.EndAngle);
            WeldHeadConfig.DefaultGalvo.XCenter = ReadWeldHeadDouble(reader, "GALVO/X_CENTER", WeldHeadConfig.DefaultGalvo.XCenter);
            WeldHeadConfig.DefaultGalvo.YCenter = ReadWeldHeadDouble(reader, "GALVO/Y_CENTER", WeldHeadConfig.DefaultGalvo.YCenter);
            WeldHeadConfig.DefaultGalvo.XCompression = ReadWeldHeadInt(reader, "GALVO/X_COMPRESSION", WeldHeadConfig.DefaultGalvo.XCompression);
            WeldHeadConfig.DefaultGalvo.YCompression = ReadWeldHeadInt(reader, "GALVO/Y_COMPRESSION", WeldHeadConfig.DefaultGalvo.YCompression);
            WeldHeadConfig.DefaultGalvo.TargetSize = ReadWeldHeadDouble(reader, "GALVO/TARGET_SIZE", WeldHeadConfig.DefaultGalvo.TargetSize);
            WeldHeadConfig.DefaultGalvo.ObliqueEllipseCorrection =
                ReadWeldHeadInt(reader, "GALVO/OBLIQUE_CORRECTION", WeldHeadConfig.DefaultGalvo.ObliqueEllipseCorrection);

            // 按图形分档读取，旧单档作缺省
            GalvoShapeParams shapeFallback = new GalvoShapeParams();
            double legacyWidth = ReadWeldHeadDouble(reader, "GALVO/WIDTH", shapeFallback.Width);
            double legacyLength = ReadWeldHeadDouble(reader, "GALVO/LENGTH", shapeFallback.Length);
            int legacyFrequency = ReadWeldHeadInt(reader, "GALVO/FREQUENCY", shapeFallback.Frequency);
            foreach (WsgGalvoPattern pattern in WeldHeadConfig.GalvoPatternOrder)
            {
                GalvoShapeParams shape = WeldHeadConfig.GetGalvoShape(pattern);
                string node = GalvoShapeNodeName(pattern);
                shape.Width = ReadWeldHeadDouble(reader, "GALVO/" + node + "/WIDTH", legacyWidth);
                shape.Length = ReadWeldHeadDouble(reader, "GALVO/" + node + "/LENGTH", legacyLength);
                shape.Frequency = ReadWeldHeadInt(reader, "GALVO/" + node + "/FREQUENCY", legacyFrequency);
            }

            WeldHeadConfig.MotorPulsePerMm = ReadWeldHeadDouble(reader, "MOTOR/PULSE_PER_MM", WeldHeadConfig.MotorPulsePerMm);

            WeldHeadConfig.PdoMap.Clear();
            WeldHeadConfig.IoMapSourceFile = string.Empty;
            WeldHeadConfig.IoMapSourceTime = string.Empty;
            WeldHeadConfig.IoMapSourceRows = 0;
            XElement ioMapNode = reader.GetXDocument().Root.Element("IO_MAP");
            if (ioMapNode != null)
            {
                XAttribute sourceFile = ioMapNode.Attribute("SOURCE_FILE");
                XAttribute sourceTime = ioMapNode.Attribute("SOURCE_TIME");
                XAttribute rowCount = ioMapNode.Attribute("ROW_COUNT");
                WeldHeadConfig.IoMapSourceFile = sourceFile == null ? string.Empty : sourceFile.Value;
                WeldHeadConfig.IoMapSourceTime = sourceTime == null ? string.Empty : sourceTime.Value;
                WeldHeadConfig.IoMapSourceRows = rowCount != null && int.TryParse(rowCount.Value, out int parsedRows) ? parsedRows : 0;
            }
            foreach (XElement node in reader.GetChildElements("IO_MAP", "IO"))
            {
                WeldHeadPdoEntry entry = new WeldHeadPdoEntry
                {
                    Seq = ReadAttribute(node, "SEQ"),
                    VarName = ReadAttribute(node, "VAR"),
                    DisplayName = ReadAttribute(node, "NAME"),
                    DataType = ReadAttribute(node, "TYPE"),
                    SdoIndex = ReadAttribute(node, "SDO_INDEX"),
                    SdoSubIndex = ReadAttribute(node, "SDO_SUB"),
                    Access = ReadAttribute(node, "ACCESS"),
                    PdoIndex = ReadAttribute(node, "PDO_INDEX"),
                    PdoSubIndex = ReadAttribute(node, "PDO_SUB"),
                    Description = ReadAttribute(node, "DESC")
                };
                WeldHeadConfig.PdoMap.Add(entry);
            }

            Log("焊接头配置加载完成");
        }

        #endregion

        #region 公共函数

        /// <summary>按关键字定位轴名（内部实现；对外请用 GetAxisByKeys / GetDeltaAxisByKeys / GetAxisConfigByKeys）。</summary>
        /// <param name="keys">轴名关键字（不区分大小写，任一命中即返回）</param>
        /// <returns>命中的轴名；未命中返回 null</returns>
        private string FindAxisNameByKeys(string[] keys)
        {
            if (keys == null || keys.Length == 0) return null;
            DeltaMotionConfig cfg = MoveData?.MotionConfig;
            if (cfg == null) return null;
            foreach (DeltaAxisConfig axis in cfg.Axes)
            {
                if (axis == null || string.IsNullOrEmpty(axis.AxisName)) continue;
                string name = axis.AxisName.ToUpperInvariant();
                foreach (string key in keys)
                {
                    if (!string.IsNullOrEmpty(key) && name.Contains(key.ToUpperInvariant())) return axis.AxisName;
                }
            }
            return null;
        }

        /// <summary>判断是否为 TA（送丝下压力矩）轴。</summary>
        /// <param name="axis">待判断轴</param>
        /// <returns>命中 TA 角色关键字返回 true</returns>
        private static bool IsTaAxis(MontionAxis axis)
        {
            if (axis == null) return false;
            string name = axis.AxisName ?? string.Empty;
            for (int i = 0; i < TaAxisKeys.Length; i++)
            {
                if (name.IndexOf(TaAxisKeys[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>TA 轴手动操作统一退出力矩输出</summary>

        /// <param name="axis">目标轴</param>
        /// <param name="action">动作名（日志用）</param>
        private void ExitTaTorqueMode(MontionAxis axis, string action)
        {
            if (!IsTaAxis(axis)) return;
            if (!(axis is DeltaMontionAxis ta) || !ta.TorqueOutputEnabled) return;
            ta.StopTorque();
            Log("手动操作 " + action + " → 先停 TA 力矩输出并回 CSP", MessageLevel.Info);
        }

        /// <summary>初始化控制器并把已登记对象注册进控制器</summary>
        /// <param name="devName">设备名称</param>
        /// <param name="msg">失败时回写的错误描述</param>
        /// <returns>初始化成功返回 true</returns>
        public bool Initialization(string devName, ref string msg)
        {
            Log("开始初始化运动管理器", MessageLevel.Debug);

            // 配置已在构造时载入，此处不重复读盘
            MontionControl = new DeltaMontionControl(devName);
            if (!MontionControl.Initialization(ref msg))
            {
                return false;
            }

            CreateAxesFromConfig();
            CreateIOsFromConfig();
            CreateCylindersFromConfig();
            CreateWeldHeadFromConfig();

            IsInitialized = true;
            AttachAll();
            Log(string.Format("运动管理器初始化完成 轴 {0} 输入 {1} 输出 {2} 气缸 {3} 焊接头 {4}",
                Axes.Count, IOs.InputCount, IOs.OutputCount, Cylinders.Count,
                WeldHead != null && WeldHead.IsInitialized ? "已装载" : "未装载"), MessageLevel.Debug);
            return true;
        }

        /// <summary>把三张注册表的全部对象注册进当前控制器</summary>
        public void AttachAll()
        {
            Axes.AttachAll(MontionControl);
            IOs.AttachAll(MontionControl);
            Cylinders.AttachAll(MontionControl);
        }

        /// <summary>不重启总线，按当前内存配置重建运行期轴/IO/气缸/</summary>

        /// <returns>是否成功。</returns>
        public bool ReloadRuntimeFromConfig()
        {
            if (MontionControl == null || !IsInitialized)
            {
                Log("控制器未初始化，跳过运行期重载", MessageLevel.Warning);
                return false;
            }

            if (WeldHead != null)
            {
                try { WeldHead.Dispose(); } catch (Exception ex) { Log("重建前关闭焊接头异常 " + ex.Message, MessageLevel.Warning); }
                WeldHead = null;
            }

            Axes.Clear();
            IOs.Clear();
            Cylinders.Clear();

            CreateAxesFromConfig();
            CreateIOsFromConfig();
            CreateCylindersFromConfig();
            CreateWeldHeadFromConfig();

            AttachAll();
            Log(string.Format("运行期按配置重载完成 轴 {0} 输入 {1} 输出 {2} 气缸 {3} 焊接头 {4}",
                Axes.Count, IOs.InputCount, IOs.OutputCount, Cylinders.Count,
                WeldHead != null && WeldHead.IsInitialized ? "已装载" : "未装载"));
            return true;
        }

        /// <summary>关闭控制器并清空注册表</summary>
        public void Close()
        {
            // 幂等：已关闭不再重复释放
            if (!IsInitialized && MontionControl == null && WeldHead == null)
            {
                IsInitialized = false;
                return;
            }

            // 正常退出：逐轴停机不触发全局急停
            MontionControl?.Close();
            MontionControl = null;

            Axes.Clear();
            IOs.Clear();
            Cylinders.Clear();

            if (WeldHead != null)
            {
                try { WeldHead.Dispose(); } catch (Exception ex) { Log("关闭焊接头从站异常 " + ex.Message, MessageLevel.Warning); }
                WeldHead = null;
            }

            IsInitialized = false;
            Log("运动管理器已关闭");
        }

        /// <summary>注册轴</summary>
        /// <param name="name">轴键名</param>
        /// <param name="axis">轴对象</param>
        public void AddAxis(string name, DeltaMontionAxis axis)
        {
            Axes.Register(name, axis);
            MontionControl?.AddAxis(axis);
        }

        /// <summary>取轴</summary>
        /// <param name="name">轴键名</param>
        /// <returns>轴对象，未找到返回 null</returns>
        public MontionAxis GetAxis(string name)
        {
            return Axes.Get(name);
        }

        /// <summary>取台达具体轴（台达专属能力入口）</summary>
        /// <param name="name">轴键名</param>
        /// <returns>台达轴，未注册或非台达实现返回 null</returns>
        public DeltaMontionAxis GetDeltaAxis(string name)
        {
            return Axes.Get(name);
        }

        /// <summary>按轴名关键字在运控配置中定位轴并取注册实</summary>
        /// <param name="keys">轴名关键字（不区分大小写，任一命中即返回）</param>
        /// <returns>轴对象；未在配置中找到或未注册返回 null</returns>
        public MontionAxis GetAxisByKeys(params string[] keys)
        {
            string name = FindAxisNameByKeys(keys);
            return name == null ? null : GetAxis(name);
        }

        /// <summary>TA 回零参数组（只读；修改必须走 <see cref="SaveTaHomeConfig"/>）</summary>
        public DeltaTaHomeConfig TaHomeConfig => MoveData?.MotionConfig?.TaHome;

        /// <summary>流程等待超时配置（ROOT/FLOW_TIMEOUT 的唯一持有者的读写入口）</summary>
        public FlowTimeoutConfig FlowTimeouts => MoveData?.MotionConfig?.FlowTimeouts;

        /// <summary>按轴名关键字取轴名</summary>

        /// <param name="keys">轴名关键字（不区分大小写，任一命中即返回）</param>
        /// <param name="axisName">命中轴名；未命中为空串</param>
        /// <returns>命中返回 true</returns>
        public bool TryGetAxisNameByKeys(string[] keys, out string axisName)
        {
            axisName = string.Empty;
            string name = FindAxisNameByKeys(keys);
            if (name == null) return false;
            axisName = name;
            return true;
        }

        /// <summary>按轴名关键字取台达具体轴（角色定位 + 台达专属</summary>

        /// <param name="keys">轴名关键字（不区分大小写，任一命中即返回）</param>
        /// <returns>台达轴；未找到或非台达实现返回 null</returns>
        public DeltaMontionAxis GetDeltaAxisByKeys(params string[] keys)
        {
            string name = FindAxisNameByKeys(keys);
            return name == null ? null : GetDeltaAxis(name);
        }

        /// <summary>TA 轴的通用 AXIS 配置（只读入口；弹窗预填用，保存仍走 <see cref="SaveTaHomeConfig"/>）</summary>
        public DeltaAxisConfig TaAxisConfig => GetAxisConfigByKeys(TaAxisKeys);

        /// <summary>按关键字取轴的运行期配置（不下发运动，只读配</summary>
        /// <param name="keys">轴名关键字（不区分大小写，任一命中即返回）</param>
        /// <returns>轴配置；未找到返回 null</returns>
        public DeltaAxisConfig GetAxisConfigByKeys(params string[] keys)
        {
            string name = FindAxisNameByKeys(keys);
            if (name == null) return null;
            DeltaMotionConfig cfg = MoveData?.MotionConfig;
            return cfg?.GetAxis(name);
        }

        /// <summary>按轴名**精确**取轴的运行期配置（只读，不下发运动）</summary>
        /// <param name="axisName">轴名（如 <c>SeamTrackAxisName</c> 的取值）</param>
        /// <returns>轴配置；轴名未配置或未找到返回 null</returns>
        public DeltaAxisConfig GetAxisConfigByName(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) return null;
            DeltaMotionConfig cfg = MoveData?.MotionConfig;
            return cfg?.GetAxis(axisName);
        }

        /// <summary>创建与运行期隔离的运控配置预览副本</summary>
        /// <returns>已静默读盘的独立配置</returns>
        public DeltaMotionConfig CreatePreviewMotionConfig()
        {
            DeltaMotionConfig config = new DeltaMotionConfig();
            config.LoadConfig(false);
            return config;
        }

        /// <summary>应用界面改动的流程超时</summary>
        public void ApplyFlowTimeouts()
        {
            DeltaMotionConfig config = MoveData?.MotionConfig;
            if (config == null || config.FlowTimeouts == null) return;

            config.FlowTimeouts.Normalize(delegate (string message)
            {
                GlobalCommData.ShowLog("运动控制", "流程超时配置 " + message, MessageLevel.Warning);
            });
            FlowTimeoutSettings.Publish(config.FlowTimeouts);
            DeviceWaitSettings.Publish(config.FlowTimeouts.CameraAcquireSeconds, config.FlowTimeouts.RecordFlushSeconds,
                config.FlowTimeouts.LineLaserCallbackMs, config.FlowTimeouts.ModbusConnectMs, config.FlowTimeouts.ModbusReadWriteMs);
            config.SaveConfig();
            GlobalCommData.ShowLog("运动控制", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "流程超时已生效 指令检测 {0:F1}s 采样 {1}ms 运动到位 {2}s 回零 {3}s 伺服 {4}s TA零位等待 {5:F0}s",
                config.FlowTimeouts.InstructionDetectSeconds, config.FlowTimeouts.InstructionSampleMs,
                config.FlowTimeouts.MoveDoneSeconds, config.FlowTimeouts.HomeDoneSeconds,
                config.FlowTimeouts.ServoOnSeconds, config.FlowTimeouts.PositionReachResultWaitSeconds),
                MessageLevel.Debug);
            GlobalCommData.ShowLog("运动控制", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "设备侧等待已生效 相机 {0:F1}s 落盘 {1:F1}s 线激光回调 {2}ms Modbus 连接 {3}ms 读写 {4}ms",
                config.FlowTimeouts.CameraAcquireSeconds, config.FlowTimeouts.RecordFlushSeconds,
                config.FlowTimeouts.LineLaserCallbackMs, config.FlowTimeouts.ModbusConnectMs,
                config.FlowTimeouts.ModbusReadWriteMs));
        }

        /// <summary>补写 DeltaMotionConfig.xml 缺失键（含 TA_TORQUE_DIRECTION）</summary>
        /// <param name="allowWrite">是否写盘；预览/审计读传 false</param>
        /// <param name="torqueDirection">方向键的最终取值（由旧负值迁移判定，缺省 +1）</param>
        public void EnsureMotionConfigKeys(bool allowWrite, int torqueDirection)
        {
            DeltaMotionConfig config = MoveData?.MotionConfig;
            if (config == null || config.TaHome == null) return;
            config.TaHome.TorqueDirection = torqueDirection;
            config.SelfHealMissingKeys(allowWrite, true);
        }

        /// <summary>把 TA 轴配置重新套用到运行期轴对象</summary>

        /// <returns>false 表示运控未就绪或未找到 TA 轴（本次未应用）</returns>
        public bool ApplyTaAxisRuntimeConfig()
        {
            DeltaAxisConfig cfg = GetAxisConfigByKeys(TaAxisKeys);
            DeltaMontionAxis axis = GetDeltaAxisByKeys(TaAxisKeys);
            DeltaTaHomeConfig taHome = MoveData?.MotionConfig?.TaHome;
            if (cfg == null || axis == null || taHome == null)
            {
                Log("TA 轴运行期参数未应用：未找到 TA 轴配置、轴对象或回零参数组（运控未初始化）", MessageLevel.Warning);
                return false;
            }
            ApplyAxisConfig(axis, cfg);
            axis.HomeMode = taHome.HomeMode;
            axis.HomeFirstVel = taHome.HomeFirstVel;
            axis.HomeSecondVel = taHome.HomeSecondVel;
            axis.HomeAcc = taHome.HomeAcc;
            // 软件钳制即时生效；16#6072 仅 OFF 窗口写，改后需重使能
            axis.TorqueLimitPerMille = taHome.GetTorqueLimitPerMille();
            axis.FullTorqueOnEnable = true;   // 回零满扭矩随下次使能生效
            Log(string.Format("TA 轴运行期参数已更新 回零模式 {0}（下发值 {1}）寻找速度 {2} rpm 爬行速度 {3} rpm 加速度 {4} 毫秒（下次回零生效）"
                + " 扭矩上限 {5:F2}N（{6}‰，软件钳制即时生效，驱动侧 16#6072 下次使能生效）",
                taHome.HomeMode, (ushort)(taHome.HomeMode & 0xFFFF), taHome.HomeFirstVel, taHome.HomeSecondVel,
                taHome.HomeAcc, taHome.TorqueLimitN, axis.TorqueLimitPerMille));
            return true;
        }

        /// <summary>保存 TA 扭矩回零参数组并推回运行期</summary>
        /// <param name="value">新的回零参数组（模式 -4~35、两档速度 rpm、加速 ms）</param>
        /// <returns>false 表示运控配置不可用或未找到 TA 轴</returns>
        public bool SaveTaHomeConfig(DeltaTaHomeConfig value)
        {
            DeltaMotionConfig config = MoveData?.MotionConfig;
            if (config == null || value == null || config.TaHome == null)
            {
                Log("TA 回零参数保存失败：运控配置或参数组不可用", MessageLevel.Warning);
                return false;
            }
            DeltaAxisConfig taCfg = config.GetAxis(config.TaAxisName);
            if (taCfg == null)
            {
                Log("TA 回零参数保存失败：未找到 TA 轴 " + config.TaAxisName, MessageLevel.Warning);
                return false;
            }

            config.TaHome.HomeMode = value.HomeMode;
            config.TaHome.HomeFirstVel = value.HomeFirstVel;
            config.TaHome.HomeSecondVel = value.HomeSecondVel;
            config.TaHome.HomeAcc = value.HomeAcc;
            config.SaveConfig();

            bool applied = ApplyTaAxisRuntimeConfig();
            Log(string.Format("TA 回零参数已保存 模式 {0}（下发值 {1}）寻找速度 {2} rpm 爬行速度 {3} rpm 加速度 {4} 毫秒"
                + " 100%力量基准 {5} N 扭矩爬升速率 {6}（0.1%/s）；{7}"
                + "（工作零位坐标在工艺设置页 TA_ZeroPose；速度/容差复用 TA_Pose1/2，不在本弹窗配置）",
                config.TaHome.HomeMode, (ushort)(config.TaHome.HomeMode & 0xFFFF),
                config.TaHome.HomeFirstVel, config.TaHome.HomeSecondVel, config.TaHome.HomeAcc,
                config.TaHome.Force100PctN,
                taCfg.TorqueSlope,
                applied ? "已推回运行期轴，下次回零生效" : "运行期轴未装配，仅落盘"));
            return true;
        }

        /// <summary>绝对定位（自动使能伺服；轴控制统一入口）。</summary>
        /// <param name="axis">目标轴（null 时返回 false，不下发）</param>
        /// <param name="target">目标坐标</param>
        /// <returns>指令是否下发成功</returns>
        public bool MoveTo(MontionAxis axis, double target)
        {
            if (axis == null) return false;
            ExitTaTorqueMode(axis, "绝对移动");
            if (!axis.IsServoOn()) axis.ServoOn();
            return axis.AbsMove(target, 1);
        }

        /// <summary>相对步进（posMode=0；轴控制统一入口）。</summary>
        /// <param name="axis">目标轴（null 时返回 false，不下发）</param>
        /// <param name="delta">相对位移（可为负）</param>
        /// <returns>指令是否下发成功</returns>
        public bool StepMove(MontionAxis axis, double delta)
        {
            if (axis == null) return false;
            ExitTaTorqueMode(axis, "相对步进");
            return axis.AbsMove(delta, 0);
        }

        /// <summary>绝对定位非阻塞版</summary>
        /// <param name="axis">目标轴（null 时返回 false，不下发）</param>
        /// <param name="target">目标坐标</param>
        /// <returns>指令是否下发成功</returns>
        public bool MoveToNoBlock(MontionAxis axis, double target)
        {
            if (axis == null) return false;
            ExitTaTorqueMode(axis, "绝对移动");
            if (!axis.IsServoOn()) axis.ServoOn();
            return axis.AbsMoveNoBlock(target, 1);
        }

        /// <summary>相对步进非阻塞版</summary>
        /// <param name="axis">目标轴（null 时返回 false，不下发）</param>
        /// <param name="delta">相对位移（可为负）</param>
        /// <returns>指令是否下发成功</returns>
        public bool StepMoveNoBlock(MontionAxis axis, double delta)
        {
            if (axis == null) return false;
            ExitTaTorqueMode(axis, "相对步进");
            return axis.AbsMoveNoBlock(delta, 0);
        }

        /// <summary>伺服使能翻转（轴控制统一入口）。</summary>
        /// <param name="axis">目标轴（null 时返回 false）</param>
        /// <returns>翻转后的使能状态</returns>
        public bool ToggleServo(MontionAxis axis)
        {
            if (axis == null) return false;
            ExitTaTorqueMode(axis, "伺服使能");
            if (axis.IsServoOn()) axis.ServoOff();
            else axis.ServoOn();
            return axis.IsServoOn();
        }

        /// <summary>启动连续点动（dir 1=负向, 0=正向；轴控制统一入口）。</summary>
        /// <param name="axis">目标轴（null 时不下发）</param>
        /// <param name="dir">方向 1=负向 0=正向</param>
        public void JogStart(MontionAxis axis, ushort dir)
        {
            if (axis == null) return;
            ExitTaTorqueMode(axis, "点动");
            axis.JogMove(dir);
        }

        /// <summary>减速停止（点动与停止按钮共用）。</summary>

        /// <param name="axis">目标轴（null 时不下发）</param>
        public void StopAxis(MontionAxis axis)
        {
            if (axis == null) return;
            CancelAxisInstruction(axis.AxisName);
            ExitTaTorqueMode(axis, "停止");
            axis.AxisStop((ushort)StopMode.Deceleration);
        }

        /// <summary>回零并等待驱动器确认完成</summary>
        /// <param name="axis">目标轴（null 直接失败）</param>
        /// <returns>完成且经状态字确认返回 true</returns>
        public bool HomeAxisAndWait(MontionAxis axis)
        {
            if (axis == null) return false;
            ExitTaTorqueMode(axis, "回零");
            return axis.Home();
        }

        // 按轴角色的手动操作分发（应用动作封装）已迁至 MainDeviceControl/DeviceWorkflow/MotionControlWorkflow。
        // 本类只管取轴、门禁、状态摘要与单轴指令

        /// <summary>注册输入</summary>
        /// <param name="name">输入键名</param>
        /// <param name="io">输入对象</param>
        public void AddInputIO(string name, InputIO io)
        {
            IOs.RegisterInput(name, io);
            MontionControl?.AddIO(io);
        }

        /// <summary>取输入</summary>
        /// <param name="name">输入键名</param>
        /// <returns>输入对象，未找到返回 null</returns>
        public InputIO GetInputIO(string name)
        {
            return IOs.GetInput(name);
        }

        /// <summary>注册输出</summary>
        /// <param name="name">输出键名</param>
        /// <param name="io">输出对象</param>
        public void AddOutputIO(string name, OutputIO io)
        {
            IOs.RegisterOutput(name, io);
            MontionControl?.AddIO(io);
        }

        /// <summary>取输出</summary>
        /// <param name="name">输出键名</param>
        /// <returns>输出对象，未找到返回 null</returns>
        public OutputIO GetOutputIO(string name)
        {
            return IOs.GetOutput(name);
        }

        /// <summary>注册气缸，同时把其占用的 IO 登记进控制器</summary>
        /// <param name="name">气缸键名</param>
        /// <param name="cylinder">气缸对象</param>
        public void AddCylinder(string name, Cylinder cylinder)
        {
            Cylinders.Register(name, cylinder);
            if (MontionControl != null) RegisterCylinderIO(cylinder, MontionControl);
        }

        /// <summary>取气缸</summary>
        /// <param name="name">气缸键名</param>
        /// <returns>气缸对象，未找到返回 null</returns>
        public Cylinder GetCylinder(string name)
        {
            return Cylinders.Get(name);
        }

        /// <summary>把单个气缸占用的全部 IO 注册进控制器</summary>
        /// <param name="cylinder">气缸对象</param>
        /// <param name="control">目标控制器</param>
        public void RegisterCylinderIO(Cylinder cylinder, MontionControl control)
        {
            if (cylinder == null || control == null) return;

            foreach (MontionIO io in cylinder.GetAllIO())
            {
                control.AddIO(io);
            }
        }

        /// <summary>全部轴伺服使能</summary>
        public void ServoOnAll()
        {
            foreach (MontionAxis axis in Axes.GetAll())
            {
                axis.ServoOn();
            }
            Log("全部轴已伺服使能");
        }

        /// <summary>初始化完成后按配置统一使能已装载轴</summary>

        /// <returns>全部轴使能成功返回 true；未初始化或存在失败轴返回 false</returns>
        public bool EnableAxesAfterInit()
        {
            if (!IsInitialized || MontionControl == null)
            {
                Log("阶段二 使能 跳过 原因 运动管理器未初始化", MessageLevel.Warning);
                return false;
            }

            int okCount = 0;
            int failCount = 0;
            DeltaMontionAxis taAxis = GetDeltaAxisByKeys(TaAxisKeys);
            foreach (MontionAxis axis in Axes.GetAll())
            {
                if (axis == null) continue;
                if (IsAxisSlaveMissing(axis))
                {
                    failCount++;
                    Log(string.Format("阶段二 使能 {0} 失败 从站未连接 节点 {1}",
                        axis.AxisName, ((DeltaMontionAxis)axis).NodeId), MessageLevel.Error);
                    continue;
                }
                if (taAxis != null && ReferenceEquals(axis, taAxis) && !_taConfigLoadOk)
                {
                    // TA 配置加载失败则不使能，功能降级不静默
                    Log(string.Format("阶段二 使能 {0} 跳过 配置加载失败", axis.AxisName), MessageLevel.Error);
                    Log(TaDegradedReason, MessageLevel.Debug);
                    continue;
                }
                try
                {
                    if (axis.IsServoOn())
                    {
                        okCount++;
                        Log(string.Format("阶段二 使能 {0} 已处于使能状态，跳过", axis.AxisName), MessageLevel.Debug);
                        continue;
                    }

                    axis.ServoOn();
                    if (axis.IsServoOn())
                    {
                        okCount++;
                        Log(string.Format("阶段二 使能 {0} 成功", axis.AxisName));
                    }
                    else
                    {
                        failCount++;
                        Log(string.Format("阶段二 使能 {0} 失败 状态字 0x{1:X4}",
                            axis.AxisName, GetAxisStatus(axis).Raw), MessageLevel.Error);
                        Log(DescribeAxisDiagnosis(axis, "使能", true), MessageLevel.Debug);
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    Log(string.Format("阶段二 使能 {0} 异常 {1}", axis.AxisName, ex.GetType().Name), MessageLevel.Error);
                }
            }

            Log(string.Format("阶段二 使能 完成 成功 {0} 失败 {1}", okCount, failCount),
                failCount == 0 ? MessageLevel.Info : MessageLevel.Error);
            NotifyAxisReadinessChanged("ALL");   // 每轴使能结果即时通知 UI，不等下一次点击

            // TA_ZeroPose 不在本方法内自动执行：由 DeviceControlWork 在闸门打开后调用 MotionControlWorkflow.RunTaZeroPoseAfterInit
            return failCount == 0;
        }

        /// <summary>全部轴伺服下电</summary>
        public void ServoOffAll()
        {
            foreach (MontionAxis axis in Axes.GetAll())
            {
                axis.ServoOff();
            }
            Log("全部轴已伺服下电");
        }

        /// <summary>全部轴复位驱动器报警</summary>
        public void ResetAlarmAll()
        {
            foreach (MontionAxis axis in Axes.GetAll())
            {
                axis.ResetAlarm();
            }
            Log("全部轴报警已复位");
        }

        /// <summary>全部轴回零</summary>
        /// <returns>全部成功返回 true</returns>
        public bool HomeAll()
        {
            bool allOk = true;
            foreach (MontionAxis axis in Axes.GetAll())
            {
                if (!axis.Home()) allOk = false;
            }
            Log(allOk ? "全部轴回零完成" : "部分轴回零失败", allOk ? MessageLevel.Info : MessageLevel.Error);
            return allOk;
        }

        /// <summary>设置全局急停</summary>
        /// <param name="isEmergencyStop">true 为急停</param>
        public void SetEmergencyStop(bool isEmergencyStop)
        {
            MontionControl?.SetEmergencyStop(isEmergencyStop);
            if (isEmergencyStop) CancelAllAxisInstructions("急停");
        }

        #endregion
    }

    /// <summary>UI 手动轴操作种类（硬件控制归 <c>MontionManager</c>，UI 只表达意图）</summary>
    public enum AxisManualOperation
    {
        /// <summary>伺服使能/下电翻转</summary>
        ToggleServo,
        /// <summary>减速停止</summary>
        Stop,
        /// <summary>绝对定位</summary>
        MoveTo,
        /// <summary>相对步进</summary>
        StepMove,
        /// <summary>开始连续点动</summary>
        JogStart,
        /// <summary>结束连续点动（与 Stop 分开命名，便于调用方表达意图）</summary>
        JogStop
    }
}


