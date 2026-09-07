using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达轴</summary>
    /// <remarks>对外一律工程单位，脉冲换算在本层完成；所有运动入口先做前置检查。</remarks>
    public class DeltaMontionAxis : MontionAxis
    {
        #region 私有变量

        private const int MoveDoneTimeoutSeconds = 60;
        private const int HomeDoneTimeoutSeconds = 120;
        private const int ServoOnTimeoutSeconds = 3;
        private const int PollIntervalMs = 20;

        private readonly DeltaMontionControl _deltaControl;
        private readonly ushort _nodeId;
        private readonly ushort _slotNo;
        private readonly string _tag;
 
        /// <summary>当前卡号，实时取自控制器</summary>
        private ushort CardNo { get { return _deltaControl.CardNo; } }

        #endregion

        #region 公共变量

        /// <summary>从站号</summary>
        public ushort NodeId { get { return _nodeId; } }

        /// <summary>槽号，单轴从站为 0</summary>
        public ushort SlotNo { get { return _slotNo; } }

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
            Log(string.Format("{0} 失败 错误码 {1} 描述 {2}", funcName, ret, DeltaErrorCode.GetMessage(ret)),
                MessageLevel.Error);
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
                MessageLevel.Warning);
            return false;
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

            if (ret == DeltaErrorCode.NO_ERROR) return true;
            LogFail("点位运动", ret);
            return false;
        }

        /// <summary>下发回零配置并启动</summary>
        /// <returns>启动成功返回 true</returns>
        private bool StartHome()
        {
            if (!PreMotionCheck("回零")) return false;

            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Home_Config(
                CardNo, _nodeId, _slotNo, HomeMode, 0,
                (uint)ToPulse(HomeMaxVel), (uint)ToPulse(HomeMaxVel / 2), (uint)(AccTime * 1000));
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                LogFail("回零配置", ret);
                return false;
            }

            ret = EtherCATMotionNative.CS_ECAT_Slave_Home_Move(CardNo, _nodeId, _slotNo);
            if (ret == DeltaErrorCode.NO_ERROR) return true;
            LogFail("回零启动", ret);
            return false;
        }

        #endregion

        #region 公共函数

        /// <summary>判断是否正在运动</summary>
        /// <returns>运动中返回 true</returns>
        public override bool IsMoving()
        {
            ushort mdone = 0;
            EtherCATMotionNative.CS_ECAT_Slave_Motion_Get_Mdone(CardNo, _nodeId, _slotNo, ref mdone);
            return mdone == 0;
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
                Log("等待到位超时", MessageLevel.Warning);
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
        }

        /// <summary>判断伺服是否已使能</summary>
        /// <returns>已使能返回 true</returns>
        public override bool IsServoOn()
        {
            ushort statusWord = GetStatusWord();
            return (statusWord & EtherCATMotionNative.StatusWordOperationEnabled) != 0;
        }

        /// <summary>伺服使能，先切周期同步位置模式再上电</summary>
        public override void ServoOn()
        {
            if (!PreMotionCheck("伺服使能")) return;

            if ((GetStatusWord() & EtherCATMotionNative.StatusWordFault) != 0)
            {
                EtherCATMotionNative.CS_ECAT_Slave_Motion_Ralm(CardNo, _nodeId, _slotNo);
                Thread.Sleep(50);
            }

            EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_MoveMode(CardNo, _nodeId, _slotNo,
                (ushort)MoveMode.CSP);
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
                Log("伺服使能超时", MessageLevel.Warning);
                return;
            }
        }

        /// <summary>伺服下电</summary>
        public override void ServoOff()
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Set_Svon(CardNo, _nodeId, _slotNo, 0);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("伺服下电", ret);
        }

        /// <summary>复位驱动器报警</summary>
        public override void ResetAlarm()
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_Motion_Ralm(CardNo, _nodeId, _slotNo);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("报警复位", ret);
            Thread.Sleep(50);
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
            DateTime start = DateTime.Now;
            ushort status = 0;

            while (true)
            {
                EtherCATMotionNative.CS_ECAT_Slave_Home_Status(CardNo, _nodeId, _slotNo, ref status);
                if (status == 0) return true;
                if (status != 1)
                {
                    Log(string.Format("回零异常 状态码 {0}", status), MessageLevel.Error);
                    return false;
                }

                Thread.Sleep(PollIntervalMs);
                if ((DateTime.Now - start).TotalSeconds > HomeDoneTimeoutSeconds)
                {
                    Log("等待回零超时", MessageLevel.Warning);
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

        /// <summary>设置软限位</summary>
        /// <param name="positiveLimit">正限位（工程单位）</param>
        /// <param name="negativeLimit">负限位（工程单位）</param>
        /// <param name="enable">是否启用</param>
        /// <returns>设置成功返回 true</returns>
        public bool SetSoftLimit(double positiveLimit, double negativeLimit, bool enable)
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_Set_Softlimit(
                CardNo, _nodeId, _slotNo, ToPulse(positiveLimit), ToPulse(negativeLimit),
                (ushort)(enable ? 1 : 0));
            if (ret == DeltaErrorCode.NO_ERROR) return true;
            LogFail("设置软限位", ret);
            return false;
        }

        /// <summary>运动中变更目标位置</summary>
        /// <param name="newPos">新目标位置（工程单位）</param>
        /// <returns>变更成功返回 true</returns>
        public bool ChangeTargetPosition(double newPos)
        {
            ushort ret = EtherCATMotionNative.CS_ECAT_Slave_CSP_TargetPos_Change(
                CardNo, _nodeId, _slotNo, ToPulse(newPos));
            if (ret == DeltaErrorCode.NO_ERROR) return true;
            LogFail("变更目标位置", ret);
            return false;
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
    }
}
