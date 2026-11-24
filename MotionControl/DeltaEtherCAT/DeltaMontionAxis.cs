﻿using System;
using System.Threading;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达轴</summary>
    public class DeltaMontionAxis : MontionAxis
    {
        #region 私有变量

        // 超时取自 ROOT/FLOW_TIMEOUT，改配置即时生效。
        // 超时缺省值由配置类提供
        private static int MoveDoneTimeoutSeconds { get { return FlowTimeoutSettings.Current.MoveDoneSeconds; } }
        private static int HomeDoneTimeoutSeconds { get { return FlowTimeoutSettings.Current.HomeDoneSeconds; } }
        private static int ServoOnTimeoutSeconds { get { return FlowTimeoutSettings.Current.ServoOnSeconds; } }
        private static int PollIntervalMs { get { return FlowTimeoutSettings.Current.AxisPollMs; } }
        private static int StopSettleMs { get { return FlowTimeoutSettings.Current.StopSettleMs; } }

        /// <summary>CST 曲线模式：1=直线加速</summary>
        private const ushort TorqueCurveModeLinear = 1;

        /// <summary>额定力矩千分比满量程</summary>
        private const short TorqueRatedPermille = 1000;

        /// <summary>输出使能开启（开力矩输出）</summary>
        private const ushort DeltaEnableOn = 1;

        /// <summary>输出使能关闭（撤力矩 / 撤使能）</summary>
        private const ushort DeltaEnableOff = 0;

        private readonly DeltaMontionControl _deltaControl;
        private readonly ushort _nodeId;
        private readonly ushort _slotNo;
        private readonly string _tag;
        /// <summary>最近一次指令反馈（函数名+返回码）</summary>
        private string _lastCommand = string.Empty;

        /// <summary>CST 扭矩输出是否已开启（运行期态；StopTorque / 回滚 CSP 后清零）</summary>
        private bool _torqueOutputEnabled;

        /// <summary>最近成功下发的扭矩目标，0 表示无；用于识别反号</summary>
        private short _lastTorqueTarget;

        /// <summary>撤力是否失败；true 表示力矩可能仍在输出</summary>
        private volatile bool _torqueRetractFailed;

        /// <summary>最近一次撤力失败的返回码（0 = 无失败记录）</summary>
        private ushort _torqueRetractFailCode;

        /// <summary>已写入驱动器 16#6072 的上限值</summary>
        private int _appliedDriveTorqueLimit = -1;

        /// <summary>本轮使能窗口写失败后不再重试</summary>
        private bool _driveTorqueLimitUnavailable;

        /// <summary>上限写入轮次，每次使能加一</summary>
        private int _torqueLimitAttemptNo;

        /// <summary>正负扭矩限值对象是否已探测（每进程一次）</summary>
        private bool _torqueLimitProbeDone;

        /// <summary>本次回零是否经驱动器状态确认完成</summary>
        private volatile bool _homeVerified;

        /// <summary>本次回零证据（成败均写，供上层引用）</summary>
        private volatile string _homeEvidence = string.Empty;

        /// <summary>对象字典通道（共用 <see cref="DeltaSlaveDevice"/> 握手重试）</summary>
        private readonly DeltaSlaveDevice _sdo;

        /// <summary>冷启动切 CST 前等待位置运动停稳的上限（秒，来自 FLOW_TIMEOUT）</summary>
        private static double TorqueEntryStabilizeTimeoutSeconds { get { return FlowTimeoutSettings.Current.TorqueEntryStabilizeSeconds; } }
 
        /// <summary>当前卡号，实时取自控制器</summary>
        private ushort CardNo { get { return _deltaControl.CardNo; } }

        #endregion

        #region 公共变量

        /// <summary>从站号</summary>
        public ushort NodeId { get { return _nodeId; } }

        /// <summary>槽号，单轴从站为 0</summary>
        public ushort SlotNo { get { return _slotNo; } }

        /// <summary>原点偏移（工程单位），由配置套用，对应台达 Home_Config Offset</summary>
        public double HomeOffset { get; set; }

        /// <summary>寻找速度（rpm，台达伺服口径），由配置套用，对应 Home_Config FirstVel</summary>
        public double HomeFirstVel { get; set; }

        /// <summary>爬行速度（rpm，台达伺服口径），由配置套用，对应 Home_Config SecondVel</summary>
        public double HomeSecondVel { get; set; }

        /// <summary>回零加减速时间（毫秒），由配置套用，对应台达 Home_Config Acceleration</summary>
        public double HomeAcc { get; set; }

        /// <summary>最近一次指令反馈，供 UI 定位问题</summary>
        public override string LastCommand { get { return _lastCommand; } }

        /// <summary>当前是否 CST 输出态；不作门禁</summary>
        public bool TorqueOutputEnabled { get { return _torqueOutputEnabled; } }

        /// <summary>最近一次撤力（CST_Output_Enable(0)）是否失败：true 表示力矩可能仍在输出</summary>
        public bool TorqueRetractFailed { get { return _torqueRetractFailed; } }

        /// <summary>最近撤力失败的返回码，供定位</summary>
        public ushort TorqueRetractFailCode { get { return _torqueRetractFailCode; } }

        /// <summary>扭矩上限（千分比 1~1000），CSP 与 CST 共用</summary>
        public int TorqueLimitPerMille { get; set; }

        /// <summary>使能时按满扭矩写 16#6072（TA 回零推动不被阻力停车）</summary>
        public bool FullTorqueOnEnable { get; set; }

        /// <summary>本次回零是否经驱动器确认完成</summary>
        public bool HomeVerified
        {
            get { return _homeVerified; }
        }

        /// <summary>本次回零证据（未启动/超时/异常/成功各一句）</summary>
        public string HomeEvidence
        {
            get { return _homeEvidence; }
        }

        /// <summary>读取该从站的连接状态与报警状态</summary>

        /// <param name="opState">EtherCAT 状态机值（8 = Operational）</param>
        /// <param name="alarmStatus">从站报警状态字</param>
        public void ReadConnectStatus(out ushort opState, out ushort alarmStatus)
        {
            opState = 0;
            alarmStatus = 0;
            EtherCATMasterNative.CS_ECAT_Slave_Get_Connect_Status(CardNo, _nodeId, _slotNo,
                ref opState, ref alarmStatus);
        }

        #endregion

        #region 构造函数

        /// <summary>创建台达轴并注册到控制器</summary>
        /// <param name="mControl">所属控制器</param>
        /// <param name="aName">轴名称</param>
        /// <param name="nodeId">从站号</param>
        /// <param name="slotNo">槽号，单轴从站为 0</param>
        public DeltaMontionAxis(MontionControl mControl, string aName, ushort nodeId, ushort slotNo = 0)
            : base(mControl, aName)
        {
            _deltaControl = (DeltaMontionControl)mControl;
            _nodeId = nodeId;
            _slotNo = slotNo;
            _tag = "台达运控轴";
            _sdo = new DeltaSlaveDevice(_deltaControl.CardNo, nodeId, slotNo);
            TorqueLimitPerMille = TorqueRatedPermille;
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(_tag, AxisName + " " + message, level);
        }

        /// <summary>输出失败日志</summary>
        /// <param name="funcName">接口名</param>
        /// <param name="ret">返回码</param>
        private void LogFail(string funcName, ushort ret)
        {
            _lastCommand = string.Format("{0} 失败 错误码 {1} 描述 {2}", funcName, ret, DeltaErrorCode.GetMessage(ret));
            Log(_lastCommand, MessageLevel.Error);
        }

        /// <summary>记录运动指令成功反馈（返回码 0）</summary>
        /// <param name="funcName">接口名</param>
        private void RecordCmdOk(string funcName)
        {
            _lastCommand = funcName + " 成功 错误码 0";
        }

        /// <summary>运动前统一检查急停、总线状态与报警</summary>
        /// <param name="funcName">调用方接口名</param>
        /// <returns>允许运动返回 true</returns>
        private bool PreMotionCheck(string funcName)
        {
            if (_deltaControl.EmergencyStop)
            {
                Log(funcName + " 已拦截 原因是全局急停", MessageLevel.Warning);
                return false;
            }

            ushort opState = 0;
            ushort alarmStatus = 0;
            EtherCATMasterNative.CS_ECAT_Slave_Get_Connect_Status(CardNo, _nodeId, _slotNo,
                ref opState, ref alarmStatus);
            if (opState == EtherCATMasterNative.OpStateOperational && alarmStatus == 0) return true;

            Log(string.Format("{0} 已拦截 总线状态 {1} 报警状态 {2}", funcName, opState, alarmStatus),
                MessageLevel.Debug);
            return false;
        }

        /// <summary>写扭矩上限到驱动器（CSP/CST 共用）</summary>
        private void EnsureDriveTorqueLimit()
        {
            // TA 回零用满扭矩：驱动侧放到额定上限
            int limit = FullTorqueOnEnable ? TorqueRatedPermille : TorqueLimitPerMille;
            if (limit < 1) limit = 1;
            else if (limit > TorqueRatedPermille) limit = TorqueRatedPermille;
            if (limit == _appliedDriveTorqueLimit) return;
            if (_driveTorqueLimitUnavailable) return;

            _torqueLimitAttemptNo++;
            ProbeTorqueLimitObjectsOnce();
            try
            {
                Log(string.Format("扭矩上限写入尝试 第 {0} 次 目标 {1}‰",
                    _torqueLimitAttemptNo, limit), MessageLevel.Debug);

                // 该对象只在伺服 OFF 窗口保证可写。
                // 已是 Operation Enabled 时可能被拒，日志须显示时机没对准。
                if (AxisStatus.Read(this).OperationEnabled)
                    Log("扭矩上限写入窗口为伺服 ON（非 OFF），驱动器可能拒绝写入 16#6072", MessageLevel.Warning);

                if (TryWriteTorqueLimitSdo(limit, out string reason))
                {
                    _appliedDriveTorqueLimit = limit;
                    Log(string.Format("扭矩上限已写入 16#6072 = {0}‰", limit), MessageLevel.Debug);
                    return;
                }

                _driveTorqueLimitUnavailable = true;
                Log(string.Format("驱动器侧扭矩上限本轮不可用：{0}。位置模式推动阻抗保持驱动器自身参数，"
                    + "CST 目标仍按软件上限 {1}‰ 钳制；下次伺服使能会再重试一次", reason, limit),
                    MessageLevel.Warning);
            }
            catch (Exception ex)
            {
                Log("扭矩上限写驱动器异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>只读探测正负扭矩限值对象（每进程一次）</summary>
        private void ProbeTorqueLimitObjectsOnce()
        {
            if (_torqueLimitProbeDone) return;
            _torqueLimitProbeDone = true;
            byte[] posData = new byte[2];
            byte[] negData = new byte[2];
            DeltaSlaveDevice.SdoResult pos = _sdo.ReadSdo(0x60E0, 0, 2, posData);
            DeltaSlaveDevice.SdoResult neg = _sdo.ReadSdo(0x60E1, 0, 2, negData);
            Log(string.Format("16#60E0 只读探测 → {0}；16#60E1 → {1}",
                DeltaSlaveDevice.Describe(pos), DeltaSlaveDevice.Describe(neg)), MessageLevel.Debug);
        }

        /// <summary>写标准对象 16#6072（UINT16，额定转矩 0.1%）</summary>

        /// <param name="limit">上限（额定千分比，1~1000）</param>
        /// <param name="reason">失败原因（含 API 返回码 / SDO 中止码，供汇总日志引用）</param>
        /// <returns>写入且回读一致返回 true</returns>
        private bool TryWriteTorqueLimitSdo(int limit, out string reason)
        {
            reason = string.Empty;

            // 基线 16#6041 必定存在；读失败时不得对 16#6072 下结论。
            byte[] baselineData = new byte[2];
            DeltaSlaveDevice.SdoResult baseline = _sdo.ReadSdo(0x6041, 0, 2, baselineData);
            if (!baseline.Ok)
            {
                reason = "基线读 16#6041 失败（" + DeltaSlaveDevice.Describe(baseline)
                    + "），无法区分通道问题与对象不存在";
                Log("SDO 基线读 16#6041 失败 " + DeltaSlaveDevice.Describe(baseline)
                    + "；本轮不对 16#6072 下「未实现」结论", MessageLevel.Warning);
                return false;
            }
            ushort baselineValue = (ushort)(baselineData[0] | (baselineData[1] << 8));
            Log(string.Format("SDO 基线读 16#6041 = 0x{0:X4}", baselineValue), MessageLevel.Debug);

            // SubIndex 0/1 各只读探测一次，两结果均入日志。
            byte[] probe0 = new byte[2];
            byte[] probe1 = new byte[2];
            DeltaSlaveDevice.SdoResult sub0 = _sdo.ReadSdo(0x6072, 0, 2, probe0);
            DeltaSlaveDevice.SdoResult sub1 = _sdo.ReadSdo(0x6072, 1, 2, probe1);
            Log(string.Format("16#6072 只读探测 SubIndex 0 → {0}；SubIndex 1 → {1}",
                DeltaSlaveDevice.Describe(sub0), DeltaSlaveDevice.Describe(sub1)), MessageLevel.Debug);

            ushort subIndex;
            ushort current;
            if (sub0.Ok)
            {
                subIndex = 0;
                current = (ushort)(probe0[0] | (probe0[1] << 8));
            }
            else if (sub1.Ok)
            {
                subIndex = 1;
                current = (ushort)(probe1[0] | (probe1[1] << 8));
                Log("16#6072 仅 SubIndex 1 读通 → 改用 SubIndex 1", MessageLevel.Warning);
            }
            else
            {
                reason = string.Format("SubIndex 0 {0}；SubIndex 1 {1}",
                    DeltaSlaveDevice.Describe(sub0), DeltaSlaveDevice.Describe(sub1));
                Log("16#6072 两个子索引均未读通 → " + reason, MessageLevel.Warning);
                return false;
            }

            if (current == (ushort)limit)
            {
                Log(string.Format("16#6072 SubIndex {0} 已是目标值 {1}‰，无需写入", subIndex, limit));
                return true;
            }

            byte[] data = new byte[2];
            data[0] = (byte)(limit & 0xFF);
            data[1] = (byte)((limit >> 8) & 0xFF);
            DeltaSlaveDevice.SdoResult write = _sdo.WriteSdo(0x6072, subIndex, 2, data);
            if (!write.Ok)
            {
                reason = "写失败 " + DeltaSlaveDevice.Describe(write);
                Log(string.Format("扭矩上限写 16#6072 SubIndex {0} 失败 {1}",
                    subIndex, DeltaSlaveDevice.Describe(write)), MessageLevel.Warning);
                return false;
            }

            byte[] readBackData = new byte[2];
            DeltaSlaveDevice.SdoResult readBack = _sdo.ReadSdo(0x6072, subIndex, 2, readBackData);
            if (!readBack.Ok)
            {
                reason = "写后回读失败（" + DeltaSlaveDevice.Describe(readBack) + "）";
                Log("扭矩上限写 16#6072 后回读失败 " + DeltaSlaveDevice.Describe(readBack),
                    MessageLevel.Warning);
                return false;
            }
            ushort readBackValue = (ushort)(readBackData[0] | (readBackData[1] << 8));
            if (readBackValue != (ushort)limit)
            {
                reason = string.Format("回读 {0}‰ ≠ 下发 {1}‰", readBackValue, limit);
                Log(string.Format("扭矩上限写 16#6072 后回读 {0}‰ ≠ 下发 {1}‰（读成功但值不符，写入未生效）",
                    readBackValue, limit), MessageLevel.Warning);
                return false;
            }
            return true;
        }

        /// <summary>工程单位转脉冲</summary>
        /// <param name="value">工程单位值</param>
        /// <returns>脉冲值</returns>
        private int ToPulse(double value)
        {
            double equiv = Equiv > 0 ? Equiv : 1;
            return (int)(value / equiv);
        }

        /// <summary>脉冲转工程单位</summary>
        /// <param name="pulse">脉冲值</param>
        /// <returns>工程单位值</returns>
        private double ToUnit(int pulse)
        {
            return pulse * (Equiv > 0 ? Equiv : 1);
        }

        /// <summary>下发周期同步位置运动</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>下发成功返回 true</returns>
        private bool StartMove(double pos, double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort posMode)
        {
            if (!PreMotionCheck("点位运动") || !IsServoOn())
            {
                Log("点位运动 已拦截 原因是伺服未使能", MessageLevel.Warning);
                return false;
            }

            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Start_Move(
                CardNo, _nodeId, _slotNo,
                ToPulse(pos), ToPulse(minVel), ToPulse(maxVel), ToPulse(stopVel),
                accTime, decTime, (ushort)(sTime * 1000), posMode);

            if (ret == DeltaErrorCode.NO_ERROR) { RecordCmdOk("CSP_Start_Move"); return true; }
            LogFail("点位运动", ret);
            return false;
        }

        /// <summary>下发回零配置并启动</summary>
        /// <returns>启动成功返回 true</returns>
        private bool StartHome()
        {
            if (!PreMotionCheck("回零")) return false;

            // 新一轮回零，完成须由驱动器状态确认
            _homeVerified = false;
            _homeEvidence = "回零已下发 尚未收到状态";

            if (!IsServoOn())
            {
                _homeEvidence = "回零未下发 伺服未使能";
                Log("回零被拒 伺服未使能（Home_Move 需先伺服上电），请先使能伺服", MessageLevel.Warning);
                return false;
            }

            ushort homeStatus = GetHomeStatus();
            if (homeStatus == 1)
            {
                _homeEvidence = "回零未下发 上一次回零仍在进行 Home_Status=1";
                Log("回零被拒 上一次回零仍在进行中（Home_Status=1）", MessageLevel.Warning);
                return false;
            }
            if (homeStatus == 3)
            {
                _homeEvidence = "回零未下发 上一次回零错误未清 Home_Status=3";
                Log("回零被拒 上一次回零发生错误未清（Home_Status=3，6041h Bit13），请先排除报警/复位", MessageLevel.Warning);
                return false;
            }

            if (HomeFirstVel <= 0 || HomeSecondVel <= 0 || HomeAcc <= 0)
            {
                _homeEvidence = string.Format("回零未下发 参数非法 寻找速度 {0} 爬行速度 {1} 加速度 {2}",
                    HomeFirstVel, HomeSecondVel, HomeAcc);
                Log(string.Format("回零参数非法 寻找速度 {0} 爬行速度 {1} 加速度 {2} 毫秒（三者均须大于 0）",
                    HomeFirstVel, HomeSecondVel, HomeAcc), MessageLevel.Warning);
                return false;
            }

            // 模式换算：逻辑值转 16 位补码下发
            ushort modeWord = ToHomeModeWord(HomeMode);
            Log(string.Format("回零模式换算 逻辑 {0} → 下发值 {1}",
                HomeMode, modeWord), MessageLevel.Debug);

            Log(string.Format("回零参数 模式 {0} 偏移 {1} 脉冲 寻找速度 {2} rpm 爬行速度 {3} rpm 加速时间 {4} 毫秒",
                HomeMode, ToPulse(HomeOffset), HomeFirstVel, HomeSecondVel, HomeAcc), MessageLevel.Debug);

            // 负值与正值统一走 Home_Config（Mode 为 U16，负值按 16 位补码强转）；
            // 单位按《EtherCAT 程式開發手冊》§12.1（P636）：台達伺服驅動器 Offset=脈波數、
            // FirstVel/SecondVel=rpm、Acceleration=毫秒（0→3000rpm 的時間）；故速度不再經 ToPulse 換算
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Home_Config(
                CardNo, _nodeId, _slotNo, modeWord,
                (int)ToPulse(HomeOffset),
                (uint)Math.Max(1.0, Math.Round(HomeFirstVel)),
                (uint)Math.Max(1.0, Math.Round(HomeSecondVel)), (uint)HomeAcc);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("回零配置", ret);
                return false;
            }

            // 写后回读 16#6098 反解，供现场核对。
            VerifyHomeModeReadback(modeWord);

            ret = EtherCATMotionNative.CS_ECAT_Slave_Home_Move(CardNo, _nodeId, _slotNo);
            if (ret == DeltaErrorCode.NO_ERROR) { RecordCmdOk("Home_Config+Home_Move"); return true; }
            LogFail("回零启动", ret);
            return false;
        }

        /// <summary>回读 16#6098 并按同一补码规则反解，打印下发值与逻辑</summary>
        /// <param name="modeWord">已下发的 16 位模式值</param>
        private void VerifyHomeModeReadback(ushort modeWord)
        {
            byte[] data = new byte[1];
            try
            {
                DeltaSlaveDevice.SdoResult result = _sdo.ReadSdo(0x6098, 0, 1, data);
                if (!result.Ok)
                {
                    Log(string.Format("回零模式回读不可用 {0}（下发值 {1}，逻辑 {2}）",
                        DeltaSlaveDevice.Describe(result), modeWord, HomeMode), MessageLevel.Warning);
                    return;
                }
                short logical = ToHomeModeLogical(data[0]);
                Log(string.Format("回零模式回读 下发值 {0} → 反解逻辑 {1}", modeWord, logical), MessageLevel.Debug);
            }
            catch (Exception ex)
            {
                Log("回零模式回读异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>回零模式单字节补码反解为逻辑值（0xFD → -3）</summary>
        /// <param name="raw">驱动器回读的单字节原始值</param>
        /// <returns>有符号逻辑模式号</returns>
        private static short ToHomeModeLogical(byte raw)
        {
            return raw >= 0x80 ? (short)(raw - 0x100) : (short)raw;
        }

        /// <summary>回零模式逻辑值转 16 位补码字</summary>
        /// <param name="mode">逻辑模式号（16#6098，有符号）</param>
        /// <returns>16 位补码值，如 -3 → 65533</returns>
        private static ushort ToHomeModeWord(short mode)
        {
            return (ushort)(mode & 0xFFFF);
        }

        /// <summary>反号切换：撤输出经位置停止再反向建立</summary>

        /// <param name="targetTorque">反向目标力矩（额定千分比）</param>
        /// <param name="slope">扭矩爬升速率（0.1%/s）</param>
        /// <returns>下发成功返回 true</returns>
        private bool ReverseTorqueOutput(short targetTorque, uint slope)
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(
                CardNo, _nodeId, _slotNo, DeltaEnableOff);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                // 撤输出失败不改硬件真值标志
                LogFail("反向切换撤除扭矩输出", ret);
                _torqueRetractFailed = true;
                _torqueRetractFailCode = ret;
                return false;
            }
            Log("反号切换 撤除扭矩输出 CST_Output_Enable(0)", MessageLevel.Info);

            // 切 CSP 前锁当前位置防回退
            double lockedPos = GetPosition();
            SetPosition(lockedPos);
            Log(string.Format("反号切换 位置目标锁定当前位 {0:F3}", lockedPos), MessageLevel.Info);

            ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(
                CardNo, _nodeId, _slotNo, (ushort)MoveMode.CSP);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("反号切换切回位置模式", ret);
                RestoreCspMode();
                return false;
            }
            ushort mode = GetMoveMode();
            if (mode != (ushort)MoveMode.CSP)
            {
                Log(string.Format("反号切换被拒 位置模式未生效 模式字 {0}（期望 {1}）",
                    mode, (ushort)MoveMode.CSP), MessageLevel.Warning);
                RestoreCspMode();
                return false;
            }
            Log("反号切换 已切回位置模式 CSP", MessageLevel.Info);

            // 复读指令位置，偏离即复写一次锁住
            double cmdPos = GetCommandPosition();
            if (Math.Abs(cmdPos - lockedPos) > 0.001)
            {
                SetPosition(lockedPos);
                Log(string.Format("反号切换 指令位置偏离锁定值 {0:F3} 已复写 {1:F3}", cmdPos, lockedPos),
                    MessageLevel.Warning);
            }

            // 位置环 Stop：电流快速回无方向保持位
            AxisStop((ushort)StopMode.Deceleration);
            if (!WaitReverseStopDone())
            {
                RestoreCspMode();
                return false;
            }
            Log("反号切换 位置模式停止完成", MessageLevel.Info);

            ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(
                CardNo, _nodeId, _slotNo, (ushort)MoveMode.CST);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("反号切换切到 CST", ret);
                RestoreCspMode();
                return false;
            }
            mode = GetMoveMode();
            if (mode != (ushort)MoveMode.CST)
            {
                Log(string.Format("反号切换被拒 CST 未生效 模式字 {0}（期望 {1}）",
                    mode, (ushort)MoveMode.CST), MessageLevel.Warning);
                RestoreCspMode();
                return false;
            }
            Log("反号切换 已切到 CST", MessageLevel.Info);

            ret = EtherCATMotionNative.CS_ECAT_Slave_CST_Start_Move(
                CardNo, _nodeId, _slotNo, targetTorque, slope, TorqueCurveModeLinear);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("反号切换下发反向目标", ret);
                Log(string.Format("反号切换目标明细 目标 {0}‰ 速率 {1}（0.1%/s） 模式字 {2}",
                    targetTorque, slope, mode), MessageLevel.Error);
                RestoreCspMode();
                return false;
            }
            Log(string.Format("反号切换 已写入反向目标 {0}‰ 速率 {1}（0.1%/s）", targetTorque, slope),
                MessageLevel.Info);

            ret = EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(
                CardNo, _nodeId, _slotNo, DeltaEnableOn);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("反号切换开启扭矩输出", ret);
                EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(CardNo, _nodeId, _slotNo, DeltaEnableOff);
                RestoreCspMode();
                return false;
            }
            Log("反号切换 已开启扭矩输出 CST_Output_Enable(1)", MessageLevel.Info);

            _torqueOutputEnabled = true;
            _lastTorqueTarget = targetTorque;
            _torqueRetractFailed = false;
            _torqueRetractFailCode = 0;
            RecordCmdOk("CST 反号：撤输出 + 位置停止 + 反向目标 + 重新使能");
            Log(string.Format("CST 扭矩反号切换 目标 {0}‰ 扭矩爬升速率 {1}（0.1%/s） 反号走位置停止",
                targetTorque, slope), MessageLevel.Info);
            return true;
        }

        /// <summary>反号切换：等待位置停止完成</summary>
        /// <returns>已停稳返回 true，超时返回 false</returns>
        private bool WaitReverseStopDone()
        {
            DateTime start = DateTime.Now;
            while (IsMoving())
            {
                if ((DateTime.Now - start).TotalSeconds > TorqueEntryStabilizeTimeoutSeconds)
                {
                    Log(string.Format("反号切换被拒 位置停止超时（{0:F1}s）", TorqueEntryStabilizeTimeoutSeconds),
                        MessageLevel.Error);
                    return false;
                }
                Thread.Sleep(PollIntervalMs);
            }
            return true;
        }

        #endregion

        #region 公共函数

        /// <summary>打印「目标力矩 vs 上限」关系，区分被钳制 / 已贴边 / 余量</summary>
        /// <param name="requestedTorque">调用方请求的目标（额定千分比，含符号）</param>
        /// <param name="appliedTorque">实际下发目标（钳制后）</param>
        /// <param name="limit">上限（额定千分比，正值）</param>
        private void LogTorqueLimitRelation(short requestedTorque, short appliedTorque, int limit)
        {
            int magnitude = Math.Abs((int)requestedTorque);
            if (appliedTorque != requestedTorque)
            {
                Log(string.Format("CST 扭矩上限钳制 目标 {0}‰ 超出上限 {1}‰，已按 ±{1}‰ 下发"
                    + "（被钳制 ≠ 已出力：驱动器仍按斜率从当前扭矩爬升，实际出力可能低于此值）",
                    requestedTorque, limit), MessageLevel.Warning);
                return;
            }

            int margin = limit - magnitude;
            if (margin <= 0)
            {
                Log(string.Format("CST 扭矩上限关系 目标 {0}‰ / 上限 {1}‰ → 已贴边（无余量：位置保持与力矩出力同一上限）",
                    requestedTorque, limit), MessageLevel.Warning);
                return;
            }
            if (margin * 10 <= limit)
            {
                Log(string.Format("CST 扭矩上限关系 目标 {0}‰ / 上限 {1}‰ → 余量仅 {2}‰（{3:F1}%），"
                    + "位置保持已接近上限，建议上调上限",
                    requestedTorque, limit, margin, margin * 100.0 / limit), MessageLevel.Warning);
                return;
            }
            Log(string.Format("CST 扭矩上限关系 目标 {0}‰ / 上限 {1}‰ → 有余量 {2}‰（{3:F1}%）",
                requestedTorque, limit, margin, margin * 100.0 / limit), MessageLevel.Info);
        }

        /// <summary>切 CST 前确认 CSP 位置运动已停稳</summary>

        /// <returns>已停稳返回 true</returns>
        private bool EnsureCspMotionStoppedForTorque()
        {
            if (!IsMoving()) return true;

            Log("CST 进入前 位置运动未停 先减速停止并等待停稳", MessageLevel.Warning);
            AxisStop((ushort)StopMode.Deceleration);

            DateTime start = DateTime.Now;
            while (IsMoving())
            {
                if ((DateTime.Now - start).TotalSeconds > TorqueEntryStabilizeTimeoutSeconds)
                {
                    Log(string.Format("CST 扭矩下发被拒 位置运动停稳超时（{0:F1}s）", TorqueEntryStabilizeTimeoutSeconds),
                        MessageLevel.Error);
                    return false;
                }
                Thread.Sleep(PollIntervalMs);
            }
            Log("CST 进入前 位置运动已停稳");
            return true;
        }

        /// <summary>失败回滚：把轴切回 CSP 位置模式（不下发运动指令）</summary>
        private void RestoreCspMode()
        {
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(
                CardNo, _nodeId, _slotNo, (ushort)MoveMode.CSP);
            // 回滚后不再是输出态，避免误判热切换
            _torqueOutputEnabled = false;
            _lastTorqueTarget = 0;
        }

        /// <summary>判断是否正在运动</summary>
        /// <returns>运动中返回 true</returns>
        public override bool IsMoving()
        {
            ushort mdone = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_Mdone(CardNo, _nodeId, _slotNo, ref mdone);
            // 手册 8.12：CSP 系 Mdone 是多值状态机（0 静止/1 加速/2 常速/3 减速/5 Mailbox），运动中 = Mdone != 0
            return mdone != 0;
        }

        /// <summary>等待运动到位</summary>
        /// <returns>到位返回 true，超时返回 false</returns>
        public override bool WaitMoveDone()
        {
            DateTime start = DateTime.Now;
            while (IsMoving())
            {
                Thread.Sleep(PollIntervalMs);
                if ((DateTime.Now - start).TotalSeconds <= MoveDoneTimeoutSeconds) continue;
                Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "等待到位超时 {0}s（FLOW_TIMEOUT/MOVE_DONE_SECONDS）", MoveDoneTimeoutSeconds), MessageLevel.Warning);
                return false;
            }
            return true;
        }

        /// <summary>停止轴运动</summary>
        /// <param name="stopMode">0 为减速停止，1 为急停</param>
        public override void AxisStop(ushort stopMode)
        {
            ushort ret = stopMode == (ushort)StopMode.Emergency
                ? EtherCATMotionNative.CS_ECAT_Slave_Motion_Emg_Stop(CardNo, _nodeId, _slotNo)
                : EtherCATMotionNative.CS_ECAT_Slave_Motion_Sd_Stop(CardNo, _nodeId, _slotNo, DecTime);

            if (ret != DeltaErrorCode.NO_ERROR) LogFail("停止轴", ret);
            else
            {
                RecordCmdOk(stopMode == (ushort)StopMode.Emergency ? "Emg_Stop" : "Sd_Stop");
                // 急停不撤 CST 输出，输出态时补撤力（只告警）
                if (stopMode == (ushort)StopMode.Emergency && _torqueOutputEnabled) StopTorque();
            }
        }

        /// <summary>判断伺服是否已使能</summary>
        /// <returns>已使能返回 true</returns>
        public override bool IsServoOn()
        {
            return AxisStatus.Read(this).OperationEnabled;
        }

        /// <summary>伺服使能，先切周期同步位置模式再上电</summary>
        public override void ServoOn()
        {
            if (!PreMotionCheck("伺服使能")) return;

            if (AxisStatus.Read(this).Fault)
            {
                EtherCATMotionNative.CS_ECAT_Slave_Motion_Ralm(CardNo, _nodeId, _slotNo);
                Thread.Sleep(StopSettleMs);   // 复位后沉降，缺省 50ms（FLOW_TIMEOUT/STOP_SETTLE_MS）
            }

            // OFF 窗口写 16#6072 上限，CSP 与 CST 共用。
            // 须在切换模式之前执行
            // 0x1305（邮箱未就绪）的典型诱因，勿加延时硬凑。
            // 每次使能重试一次，轮次计数可在日志分辨。
            _driveTorqueLimitUnavailable = false;
            EnsureDriveTorqueLimit();

            ushort modeRet = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(CardNo, _nodeId, _slotNo,
                (ushort)MoveMode.CSP);
            if (modeRet != DeltaErrorCode.NO_ERROR && GetMoveMode() != (ushort)MoveMode.CSP)
            {
                // 模式未切成功则后续缓冲写段必被拒（4610），使能也不应继续
                LogFail("伺服使能前切 CSP 运动模式", modeRet);
                return;
            }
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_Svon(CardNo, _nodeId, _slotNo, 1);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("伺服使能", ret);
                return;
            }

            DateTime start = DateTime.Now;
            while (!IsServoOn())
            {
                Thread.Sleep(PollIntervalMs);
                if ((DateTime.Now - start).TotalSeconds <= ServoOnTimeoutSeconds) continue;
                Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "伺服使能超时 {0}s（FLOW_TIMEOUT/SERVO_ON_SECONDS）", ServoOnTimeoutSeconds), MessageLevel.Warning);
                _lastCommand = "Set_Svon(1) 超时 错误码 4080";
                return;
            }
            RecordCmdOk("Set_Svon(1)");
        }

        /// <summary>伺服下电</summary>
        public override void ServoOff()
        {
            // 先撤力：下电不清输出位，再上电会带力矩启动。
            // 撤力失败仍继续下电，两标志保持真实结果。
            if (_torqueOutputEnabled) StopTorque();

            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_Svon(CardNo, _nodeId, _slotNo, 0);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("伺服下电", ret);
            else RecordCmdOk("Set_Svon(0)");
            // 下电后上限置未确认，下次使能重写 16#6072。
            _appliedDriveTorqueLimit = -1;
        }

        /// <summary>回零后回落扭矩上限（伺服 ON 窗口）</summary>

        /// <returns>已回落或无需回落返回 true</returns>
        public bool RestoreTorqueLimitAfterHome()
        {
            if (!FullTorqueOnEnable) return true;
            int limit = TorqueLimitPerMille;
            if (limit < 1) limit = 1;
            else if (limit > TorqueRatedPermille) limit = TorqueRatedPermille;
            if (limit == _appliedDriveTorqueLimit) return true;

            string actual = _appliedDriveTorqueLimit > 0
                ? _appliedDriveTorqueLimit.ToString() + "‰" : "未知";
            if (!AxisStatus.Read(this).OperationEnabled)
            {
                Log("回零后扭矩上限回落跳过：伺服未使能，驱动侧当前 " + actual, MessageLevel.Warning);
                return false;
            }

            // 回落不占使能窗口标志，失败留给下次使能重写
            try
            {
                if (TryWriteTorqueLimitSdo(limit, out string reason))
                {
                    _appliedDriveTorqueLimit = limit;
                    Log(string.Format("回零后扭矩上限已回落 16#6072 = {0}‰", limit));
                    return true;
                }
                Log(string.Format("回零后扭矩上限回落失败 {0}；驱动侧仍为 {1}，"
                    + "位置模式推动阻抗偏大，须伺服重新使能后恢复", reason, actual),
                    MessageLevel.Warning);
            }
            catch (Exception ex)
            {
                Log("回零后扭矩上限回落异常 " + ex.Message, MessageLevel.Warning);
            }
            return false;
        }

        /// <summary>复位驱动器报警</summary>
        public override void ResetAlarm()
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Ralm(CardNo, _nodeId, _slotNo);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("报警复位", ret);
            Thread.Sleep(StopSettleMs);   // 复位后沉降，缺省 50ms（FLOW_TIMEOUT/STOP_SETTLE_MS）
        }

        /// <summary>回零并等待完成</summary>
        /// <returns>回零成功返回 true</returns>
        public override bool Home()
        {
            return StartHome() && WaitHomeDone();
        }

        /// <summary>启动回零后不等待</summary>
        /// <returns>启动成功返回 true</returns>
        public override bool HomeNoBlock()
        {
            return StartHome();
        }

        /// <summary>等待回零完成</summary>
        /// <returns>完成返回 true，超时返回 false</returns>
        public override bool WaitHomeDone()
        {
            const int StartGraceMs = 2000;   // 下发后等状态字置 1 的宽限
            DateTime start = DateTime.Now;
            double startPos = GetPosition();
            ushort status = 0;
            bool sawRunning = false;

            while (true)
            {
                ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Home_Status(
                    CardNo, _nodeId, _slotNo, ref status);
                if (ret != DeltaErrorCode.NO_ERROR) status = 0xFFFF;

                if (status == 1)
                {
                    sawRunning = true;
                }
                else if (status == 0)
                {
                    if (sawRunning)
                    {
                        double endPos = GetPosition();
                        _homeVerified = true;
                        _homeEvidence = string.Format("用时 {0:F1}s 起 {1:F3}mm 终 {2:F3}mm",
                            (DateTime.Now - start).TotalSeconds, startPos, endPos);
                        Log("回零完成 " + _homeEvidence, MessageLevel.Debug);
                        return true;
                    }
                    if ((DateTime.Now - start).TotalMilliseconds > StartGraceMs)
                    {
                        _homeEvidence = string.Format("回零未启动 Home_Status 始终为 0 起 {0:F3}mm 当前 {1:F3}mm",
                            startPos, GetPosition());
                        Log(_homeEvidence, MessageLevel.Warning);
                        return false;
                    }
                }
                else
                {
                    _homeEvidence = string.Format("回零异常 状态码 {0} 起 {1:F3}mm 当前 {2:F3}mm",
                        status, startPos, GetPosition());
                    Log(_homeEvidence, MessageLevel.Error);
                    return false;
                }

                Thread.Sleep(PollIntervalMs);
                if ((DateTime.Now - start).TotalSeconds > HomeDoneTimeoutSeconds)
                {
                    _homeEvidence = string.Format("等待回零超时 {0}s 起 {1:F3}mm 当前 {2:F3}mm",
                        HomeDoneTimeoutSeconds, startPos, GetPosition());
                    Log(_homeEvidence, MessageLevel.Warning);
                    return false;
                }
            }
        }

        /// <summary>按轴参数做点位运动并等待到位</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>到位返回 true</returns>
        public override bool AbsMove(double pos, ushort posMode = 1)
        {
            return AbsMove(pos, MinVel, MaxVel, StopVel, AccTime, DecTime, STime, posMode);
        }

        /// <summary>按临时参数做点位运动并等待到位</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>到位返回 true</returns>
        public override bool AbsMove(double pos, double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort posMode = 1)
        {
            return StartMove(pos, minVel, maxVel, stopVel, accTime, decTime, sTime, posMode) && WaitMoveDone();
        }

        /// <summary>按轴参数启动点位运动后不等待</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>启动成功返回 true</returns>
        public override bool AbsMoveNoBlock(double pos, ushort posMode = 1)
        {
            return AbsMoveNoBlock(pos, MinVel, MaxVel, StopVel, AccTime, DecTime, STime, posMode);
        }

        /// <summary>按临时参数启动点位运动后不等待</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>启动成功返回 true</returns>
        public override bool AbsMoveNoBlock(double pos, double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort posMode = 1)
        {
            return StartMove(pos, minVel, maxVel, stopVel, accTime, decTime, sTime, posMode);
        }

        /// <summary>按轴参数点动</summary>
        /// <param name="dir">0 为正向，1 为负向</param>
        public override void JogMove(ushort dir)
        {
            JogMove(MinVel, MaxVel, StopVel, AccTime, DecTime, STime, dir);
        }

        /// <summary>按临时参数点动</summary>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="dir">0 为正向，1 为负向</param>
        public override void JogMove(double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort dir)
        {
            if (!PreMotionCheck("点动")) return;

            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Start_V_Move(
                CardNo, _nodeId, _slotNo, dir, ToPulse(minVel), ToPulse(maxVel),
                accTime, (ushort)(sTime * 1000));
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("点动", ret);
            else RecordCmdOk("CSP_Start_V_Move");
        }

        /// <summary>读取反馈位置</summary>
        /// <returns>位置（工程单位）</returns>
        public override double GetPosition()
        {
            int position = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_Position(CardNo, _nodeId, _slotNo, ref position);
            return ToUnit(position);
        }

        /// <summary>读取指令位置</summary>
        /// <returns>位置（工程单位）</returns>
        public override double GetCommandPosition()
        {
            int command = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_Command(CardNo, _nodeId, _slotNo, ref command);
            return ToUnit(command);
        }

        /// <summary>写入当前位置</summary>
        /// <param name="pos">位置（工程单位）</param>
        public override void SetPosition(double pos)
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_Position(
                CardNo, _nodeId, _slotNo, ToPulse(pos));
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("写入位置", ret);
        }

        /// <summary>将当前位置设为零点</summary>
        public override void SetHomePosition()
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_Position(CardNo, _nodeId, _slotNo, 0);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("设置零点", ret);
        }

        /// <summary>读取当前速度</summary>
        /// <returns>速度（工程单位/秒）</returns>
        public override double GetCurrentSpeed()
        {
            int speed = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_Current_Speed(CardNo, _nodeId, _slotNo, ref speed);
            return ToUnit(speed);
        }

        /// <summary>读取当前扭矩</summary>
        /// <returns>扭矩（额定千分比）</returns>
        public override short GetCurrentTorque()
        {
            short torque = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_Torque(CardNo, _nodeId, _slotNo, ref torque);
            return torque;
        }

        /// <summary>读取驱动器状态字</summary>
        /// <returns>状态字原始值</returns>
        public override ushort GetStatusWord()
        {
            ushort statusWord = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_StatusWord(CardNo, _nodeId, _slotNo, ref statusWord);
            return statusWord;
        }

        /// <summary>读取驱动器当前运动模式字</summary>

        /// <returns>模式字原始值（1=PP 3=PV 6=回零 8=CSP 9=CSV 10=CST）</returns>
        public ushort GetMoveMode()
        {
            byte mode = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_MoveMode(CardNo, _nodeId, _slotNo, ref mode);
            return mode;
        }

        /// <summary>确保轴处于 CSP 周期同步位置模式</summary>
        /// <remarks>缓冲插补 API 只在 CSP 模式下有效（台达手册 P9-11）。
        /// 点位运动（MoveTo）会把轴切离 CSP，且 ServoOn 的模式切换返回值曾被忽略，
        /// 导致建系后写段被拒（4610 ERR_ECAT_SLAVE_TYPE）。</remarks>
        /// <returns>已是或已切到 CSP 返回 true</returns>
        public bool EnsureCspMode()
        {
            ushort mode = GetMoveMode();
            if (mode == (ushort)MoveMode.CSP) return true;
            ushort prevMode = mode;
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(
                CardNo, _nodeId, _slotNo, (ushort)MoveMode.CSP);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                // 返回非 0 但读回已是 CSP 时按成功处理（重复设置可能带警告码）
                if (GetMoveMode() != (ushort)MoveMode.CSP)
                {
                    LogFail("切换 CSP 运动模式", ret);
                    return false;
                }
            }
            mode = GetMoveMode();
            if (mode != (ushort)MoveMode.CSP)
            {
                Log(string.Format("切换 CSP 运动模式后读回不一致 当前模式 {0} 目标 {1}",
                    mode, (ushort)MoveMode.CSP), MessageLevel.Warning);
                return false;
            }
            Log(string.Format("已切换到 CSP 运动模式 原模式 {0}", prevMode));
            return true;
        }

        /// <summary>读取回零状态</summary>
        /// <returns>0=未运动/已完成(6041h Bit12) 1=运动中(6041h Bit10) 2=中途被中止(6040h Bit8 Halt) 3=回零错误(6041h Bit13)；依据《EtherCAT 程式開發手冊》§12.3(P653)</returns>
        public ushort GetHomeStatus()
        {
            ushort status = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Home_Status(CardNo, _nodeId, _slotNo, ref status);
            return status;
        }

        /// <summary>以 CST 扭矩模式建立送丝下压力</summary>

        /// <param name="targetTorque">目标力矩（额定千分比，±1~1000）</param>
        /// <param name="slope">扭矩爬升速率（0.1%/s，0 非法）</param>
        /// <returns>下发成功返回 true</returns>
        public bool SetTorque(short targetTorque, uint slope)
        {
            if (targetTorque == 0 || targetTorque > TorqueRatedPermille || targetTorque < -TorqueRatedPermille)
            {
                Log(string.Format("CST 扭矩下发被拒 目标扭矩 {0} 非法（额定千分比 -{1} ~ {1}，0 视为非法参数）",
                    targetTorque, TorqueRatedPermille), MessageLevel.Warning);
                return false;
            }

            if (slope == 0)
            {
                Log("CST 扭矩下发被拒 扭矩爬升速率为 0（轴配置 TORQUE_SLOPE 未设置），台达按非法参数拒绝", MessageLevel.Warning);
                return false;
            }

            // 共用扭矩上限，被钳制仍按斜率爬升
            int limit = TorqueLimitPerMille < 1 ? 1 : TorqueLimitPerMille;
            if (limit > TorqueRatedPermille) limit = TorqueRatedPermille;
            short requestedTorque = targetTorque;
            if (targetTorque > limit) targetTorque = (short)limit;
            else if (targetTorque < -limit) targetTorque = (short)-limit;
            LogTorqueLimitRelation(requestedTorque, targetTorque, limit);

            if (!PreMotionCheck("CST 扭矩下发")) return false;

            AxisStatus status = AxisStatus.Read(this);
            if (!status.OperationEnabled)
            {
                Log(string.Format("CST 扭矩下发被拒 伺服未就绪 状态字 0x{0:X4}", status.Raw), MessageLevel.Warning);
                return false;
            }
            if (status.Fault)
            {
                Log(string.Format("CST 扭矩下发被拒 驱动器报警 状态字 0x{0:X4}", status.Raw), MessageLevel.Warning);
                return false;
            }

            ushort homeStatus = GetHomeStatus();
            if (homeStatus == 1)
            {
                Log("CST 扭矩下发被拒 轴仍在回零流程中（Home_Status=1），等回零完成后再下发力矩", MessageLevel.Warning);
                return false;
            }
            if (homeStatus != 0)
            {
                Log(string.Format("CST 扭矩下发提示 回零状态 {0} 非完成态，按当前状态继续下发", homeStatus),
                    MessageLevel.Warning);
            }

            ushort currentMode = GetMoveMode();
            // 不设参数相同跳过，热切换须重发目标
            bool warmEntry = _torqueOutputEnabled && currentMode == (ushort)MoveMode.CST;
            if (warmEntry)
            {
                // 反号：撤输出后经位置停止再反向建立，见 ReverseTorqueOutput。
                bool reversed = (_lastTorqueTarget > 0 && targetTorque < 0)
                    || (_lastTorqueTarget < 0 && targetTorque > 0);
                if (reversed) return ReverseTorqueOutput(targetTorque, slope);

                // 已开输出：直接改目标，驱动器按斜率爬升。
                ushort warmRet = EtherCATMotionNative.CS_ECAT_Slave_CST_Start_Move(
                    CardNo, _nodeId, _slotNo, targetTorque, slope, TorqueCurveModeLinear);
                if (warmRet != DeltaErrorCode.NO_ERROR)
                {
                    LogFail("扭矩参数下发（热切换）", warmRet);
                    EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(CardNo, _nodeId, _slotNo, DeltaEnableOff);
                    RestoreCspMode();
                    return false;
                }
                _lastTorqueTarget = targetTorque;
                RecordCmdOk("CST_Start_Move 热切换");
                _torqueRetractFailed = false;
                _torqueRetractFailCode = 0;
                Log(string.Format("CST 扭矩热切换 目标扭矩 {0}‰ 扭矩爬升速率 {1}（0.1%/s） 输出保持开启 由驱动器从当前扭矩按斜率爬升",
                    targetTorque, slope), MessageLevel.Info);
                return true;
            }

            // 冷启动（输出关或非 CST）：先停稳再切。
            // 位置环未释放就切，现场表现为先反跑再回来。
            if (!EnsureCspMotionStoppedForTorque()) return false;

            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(
                CardNo, _nodeId, _slotNo, (ushort)MoveMode.CST);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("切换 CST 模式", ret);
                RestoreCspMode();
                return false;
            }

            ushort mode = GetMoveMode();
            if (mode != (ushort)MoveMode.CST)
            {
                Log(string.Format("CST 扭矩下发被拒 模式切换未生效 当前模式字 {0}（期望 {1}）",
                    mode, (ushort)MoveMode.CST), MessageLevel.Warning);
                RestoreCspMode();
                return false;
            }

            // 先参数后使能：切 CST 后无从继承旧方向，
            // 首条力矩指令即目标，无需种子覆盖。
            ret = EtherCATMotionNative.CS_ECAT_Slave_CST_Start_Move(
                CardNo, _nodeId, _slotNo, targetTorque, slope, TorqueCurveModeLinear);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("扭矩目标下发（开输出前）", ret);
                Log(string.Format("扭矩目标下发失败明细 目标扭矩 {0} 扭矩爬升速率 {1} 曲线模式 {2} 模式字 {3}",
                    targetTorque, slope, TorqueCurveModeLinear, mode), MessageLevel.Error);
                RestoreCspMode();
                return false;
            }

            ret = EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(
                CardNo, _nodeId, _slotNo, DeltaEnableOn);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("开启扭矩输出", ret);
                EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(CardNo, _nodeId, _slotNo, DeltaEnableOff);
                RestoreCspMode();
                return false;
            }
            _torqueOutputEnabled = true;
            _lastTorqueTarget = targetTorque;

            RecordCmdOk("CST 冷启动：停稳 + 切模式 + 目标 + 开输出");
            // 输出已重开并下发目标，残留出力风险已被取代。
            _torqueRetractFailed = false;
            _torqueRetractFailCode = 0;
            Log(string.Format("CST 扭矩冷启动 目标 {0}‰ 扭矩爬升速率 {1}（0.1%/s） 位置运动已停稳",
                targetTorque, slope), MessageLevel.Info);
            return true;
        }

        /// <summary>撤除 CST 扭矩输出并切回 CSP 位置模式</summary>

        /// <returns>设置成功返回 true</returns>
        public bool StopTorque()
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CST_Output_Enable(
                CardNo, _nodeId, _slotNo, DeltaEnableOff);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                // 先回读力矩与模式字，判断是否仍在出力。
                short torque = GetCurrentTorque();
                ushort mode = GetMoveMode();
                _torqueRetractFailed = true;
                _torqueRetractFailCode = ret;
                LogFail("撤除扭矩输出", ret);
                Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "撤力失败残留出力 轴 {0} 返回码 {1}（{2}） 回读力矩 {3}‰ 模式字 {4} 输出态保持 {5} "
                    + "→ 力矩可能仍在输出；可再次 StopTorque / 执行 ServoOff / 急停 重试撤力",
                    AxisName, ret, DeltaErrorCode.GetMessage(ret), torque, mode, _torqueOutputEnabled),
                    MessageLevel.Error);
                return false;
            }

            RestoreCspMode();
            _torqueOutputEnabled = false;
            _torqueRetractFailed = false;
            _torqueRetractFailCode = 0;
            RecordCmdOk("CST_Output_Enable(0) + CSP");
            return true;
        }

        /// <summary>配置 CSP 软目标到达判定窗口（仅 CSP 运动族）</summary>

        /// <param name="positionWindow">位置窗口（脉冲，与 ToPulse 同口径）</param>
        /// <param name="windowTime">时间窗口</param>
        /// <param name="enable">是否启用</param>
        /// <returns>设置成功返回 true</returns>
        public bool SetSoftTargetReach(uint positionWindow, ushort windowTime, bool enable)
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Set_SoftTargetReach(
                CardNo, _nodeId, _slotNo, windowTime, positionWindow, (ushort)(enable ? 1 : 0));
            if (ret == DeltaErrorCode.NO_ERROR) return true;
            LogFail("软目标到达配置", ret);
            return false;
        }

        /// <summary>读取 CSP 软目标到达状态（驱动器给出的到位判定）。</summary>

        /// <param name="status">到达状态原值</param>
        /// <returns>读取成功返回 true</returns>
        public bool TryGetSoftTargetReachStatus(out ushort status)
        {
            status = 0;
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Get_SoftTargetReach_Status(
                CardNo, _nodeId, _slotNo, ref status);
            return ret == DeltaErrorCode.NO_ERROR;
        }

        /// <summary>读取 CSP 软目标到达状态并在失败时记日志。</summary>

        /// <returns>到达状态原值；读取失败返回 0</returns>
        public ushort GetSoftTargetReachStatus()
        {
            ushort status = 0;
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Get_SoftTargetReach_Status(
                CardNo, _nodeId, _slotNo, ref status);
            if (ret == DeltaErrorCode.NO_ERROR) return status;
            LogFail("软目标到达状态读取", ret);
            return 0;
        }

        /// <summary>运动中变更速度</summary>
        /// <param name="newSpeed">新速度（工程单位/秒）</param>
        /// <param name="changeTime">变更用时（秒）</param>
        /// <returns>变更成功返回 true</returns>
        public bool ChangeSpeed(double newSpeed, double changeTime)
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Velocity_Change(
                CardNo, _nodeId, _slotNo, ToPulse(newSpeed), changeTime);
            if (ret == DeltaErrorCode.NO_ERROR) return true;
            LogFail("变更速度", ret);
            return false;
        }

        #endregion

        #region 运动缓冲

        // 焊缝跟踪单轴流式，坐标系表与 FIFO 固定 0 号
        private const ushort CrdTableNo = 0;
        private const ushort CrdFifoNo = 0;
        private const ushort CrdAbsModeAbsolute = 1;
        // S 曲线时间缺省值：台达建议 10ms，现场原型亦取 10；⛔ 传 0 时驱动器侧滤波关闭
        private const ushort CrdDefaultSCurveTimeMs = 10;
        // 工作坐标偏移标志：现场原型取 1（0=不需要偏移 / 1=需要偏移）。
        // 传 0 时坐标系未按该驱动器要求建立，写段会被判「此模块不支持此 API」
        private const ushort CrdSetOriginFlag = 1;
        // 坐标系速度/加速度上限相对标称值的倍数（段速会被积压提速短时放大）
        private const int CrdMaxLimitScale = 4;

        /// <summary>建立单轴运动缓冲坐标系（不设原点，段目标即轴</summary>

        /// <param name="maxVelMmPerS">同步最大速度（毫米/秒）</param>
        /// <param name="maxAccMmPerS2">同步最大加速度（毫米/秒²）</param>
        /// <returns>建立成功返回 true</returns>
        public bool StartCrdBuffer(double maxVelMmPerS, double maxAccMmPerS2)
        {
            if (!PreMotionCheck("缓冲坐标系") || !IsServoOn()) return false;

            ushort axis = _nodeId;
            ushort slot = _slotNo;
            int originPos = 0;
            // S 曲线：轴配置优先；未配置时取 10ms（台达建议值，亦为现场原型取值），⛔ 不用 0
            ushort sCurveTimeMs = (ushort)(STime * 1000);
            if (sCurveTimeMs == 0) sCurveTimeMs = CrdDefaultSCurveTimeMs;
            // 工作坐标偏移标志取 1（现场原型取值：该驱动器需工作坐标偏移；
            // 传 0 时坐标系未按其要求建立，写段会被判为「此模块不支持此 API」）
            ushort setOriginFlag = CrdSetOriginFlag;
            ushort ret = EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Set_Crd_Parameters(
                CardNo, CrdTableNo, 1, ref axis, ref slot,
                // 坐标系上限须大于实际段速：手册 9.7.1 明确「运动参数超越此坐标系的最大值则回传错误」。
                // 段速会因积压反馈提速而短时超过标称值，故留余量（现场原型也取远大于段速的上限）
                ToPulse(maxVelMmPerS) * CrdMaxLimitScale, ToPulse(maxAccMmPerS2) * CrdMaxLimitScale, sCurveTimeMs,
                0, setOriginFlag, ref originPos);
            if (ret == DeltaErrorCode.NO_ERROR)
            {
                RecordCmdOk("缓冲坐标系建立");
                Log(string.Format("缓冲坐标系建立 同步速度 {0:F1}mm/s 加速度 {1:F0}mm/s2 S 曲线 {2}ms 工作坐标偏移 {3} 轴数 1 节点 {4} 槽 {5} 表 {6}",
                    maxVelMmPerS, maxAccMmPerS2, sCurveTimeMs, setOriginFlag, _nodeId, _slotNo, CrdTableNo));
                return true;
            }
            LogFail("缓冲坐标系建立", ret);
            return false;
        }

        /// <summary>向运动缓冲写入一段单轴直线插补（本坐标系绝</summary>

        /// <param name="targetPosMm">段终点（毫米，坐标系绝对值）</param>
        /// <param name="velMmPerS">段速度（毫米/秒）</param>
        /// <param name="endVelMmPerS">段末速度（毫米/秒，与段速度相等为连续路径）</param>
        /// <param name="accMmPerS2">段加速度（毫米/秒²）</param>
        /// <returns>台达返回码，0 为成功</returns>
        public ushort BufferLineTo(double targetPosMm, double velMmPerS, double endVelMmPerS, double accMmPerS2)
        {
            int target = ToPulse(targetPosMm);
            int vel = ToPulse(velMmPerS);
            int endVel = ToPulse(endVelMmPerS);
            int acc = ToPulse(accMmPerS2);

            // 优先 9.7.3 Line_MultiAxis：手册图 9.2.22.1 标准流程 Step3 指定的接口，
            // 不传轴数组、不依赖速度模式设置，兼容性最好
            ushort ret = EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Line_MultiAxis(
                CardNo, CrdTableNo, ref target, vel, endVel, acc, CrdAbsModeAbsolute, CrdFifoNo);
            if (ret != CspResult.EcatSlaveType) return ret;

            // 4610 = ERR_ECAT_SLAVE_TYPE「此模块不支持此 API」：回落 MultiAxis2 再试一次，
            // 两者结果一并记日志，便于现场判定驱动器是否支持缓冲插补
            ushort axis = _nodeId;
            ushort ret2 = EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Line_MultiAxis2(
                CardNo, CrdTableNo, 1, ref axis, ref target, vel, endVel, acc, CrdAbsModeAbsolute, CrdFifoNo);
            if (ret2 != DeltaErrorCode.NO_ERROR)
            {
                Log(string.Format("缓冲写段两种接口均被拒 Line_MultiAxis {0} MultiAxis2 {1} 节点 {2} 槽 {3} 表 {4} FIFO {5} 目标脉冲 {6}",
                    ret, ret2, _nodeId, _slotNo, CrdTableNo, CrdFifoNo, target), MessageLevel.Warning);
            }
            return ret2;
        }

        /// <summary>启动运动缓冲执行（缓冲内须已有段）</summary>
        /// <returns>启动成功返回 true</returns>
        public bool BufferStart()
        {
            ushort ret = EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Start(
                CardNo, CrdTableNo, CrdFifoNo);
            if (ret == DeltaErrorCode.NO_ERROR) { RecordCmdOk("缓冲启动"); return true; }
            LogFail("缓冲启动", ret);
            return false;
        }

        /// <summary>停止运动缓冲执行（MotionBuffer 收尾）</summary>

        /// <param name="mode">停止模式（见上：1 = 平滑停止 + 清 FIFO + 解坐标系）</param>
        /// <returns>台达返回码，0 为成功</returns>
        public ushort BufferStop(ushort mode)
        {
            return EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Stop(
                CardNo, CrdTableNo, CrdFifoNo, mode);
        }

        /// <summary>读取运动缓冲余量</summary>
        /// <param name="remainSize">余量（剩余可写/未执行段数，语义现场核实）</param>
        /// <returns>台达返回码，0 为成功</returns>
        public ushort BufferRemainSize(out int remainSize)
        {
            remainSize = 0;
            return EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Get_Remain_Buffer_Size(
                CardNo, CrdTableNo, ref remainSize, CrdFifoNo);
        }

        /// <summary>读取运动缓冲状态</summary>
        /// <param name="status">运行状态原值（语义现场核实）</param>
        /// <param name="blockNum">缓冲内剩余块数</param>
        /// <returns>台达返回码，0 为成功</returns>
        public ushort BufferMotionStatus(out ushort status, out int blockNum)
        {
            status = 0;
            blockNum = 0;
            return EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Get_Motion_Status(
                CardNo, CrdTableNo, ref status, ref blockNum, CrdFifoNo);
        }

        /// <summary>读取从站总线状态与报警状态</summary>

        /// <param name="opState">总线状态原值</param>
        /// <param name="alarmStatus">报警状态原值</param>
        /// <returns>台达返回码，0 为成功</returns>
        public ushort ReadSlaveStatus(out ushort opState, out ushort alarmStatus)
        {
            opState = 0;
            alarmStatus = 0;
            return EtherCATMasterNative.CS_ECAT_Slave_Get_Connect_Status(CardNo, _nodeId, _slotNo,
                ref opState, ref alarmStatus);
        }

        /// <summary>清空缓冲内未执行段并清除坐标系（停焊沿收尾）</summary>
        /// <returns>首个非零返回码（两项均成功返回 0）；供收尾侧失败统计与告警</returns>
        public ushort BufferClear()
        {
            ushort ret = EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Clear_Buffer(
                CardNo, CrdTableNo, CrdFifoNo);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("缓冲清空", ret);
                return ret;
            }

            ret = EtherCATMotionBufferNative.CS_ECAT_Slave_CSP_Start_MotionBuffer_Clear_Crd(
                CardNo, CrdTableNo, CrdFifoNo);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("缓冲坐标系清除", ret);
            return ret;
        }

        #endregion
    }
}

