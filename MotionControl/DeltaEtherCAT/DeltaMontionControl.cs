using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达 EtherCAT 控制器</summary>
    /// <remarks>初始化为异步过程，须轮询完成后才能扫描从站。</remarks>
    public class DeltaMontionControl : MontionControl
    {
        #region 私有变量

        private const string Tag = "台达运控";
        private const int InitialTimeoutSeconds = 30;
        private const int InitialPollIntervalMs = 100;

        private readonly ushort[] _cardNoList;
        private readonly DeltaSlaveScanner _slaveScanner;
        private ushort _existCardCount;

        #endregion

        #region 公共变量

        /// <summary>当前卡号</summary>
        public ushort CardNo { get { return _connectNo; } }

        /// <summary>从站扫描器</summary>
        public DeltaSlaveScanner SlaveScanner { get { return _slaveScanner; } }

        /// <summary>检测到的板卡数量</summary>
        public ushort ExistCardCount { get { return _existCardCount; } }

        #endregion

        #region 构造函数

        /// <summary>创建台达控制器</summary>
        /// <param name="devName">设备名称</param>
        public DeltaMontionControl(string devName)
            : base(devName)
        {
            _cardNoList = new ushort[32];
            _slaveScanner = new DeltaSlaveScanner();
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

        /// <summary>打开主站并初始化每张卡</summary>
        /// <param name="strMsg">失败时回写的错误描述</param>
        /// <returns>成功返回 true</returns>
        private bool OpenAndInitialCards(ref string strMsg)
        {
            ushort ret = EtherCATMasterNative.CS_ECAT_Master_Open(ref _existCardCount);
            if (ret != DeltaErrorCode.NO_ERROR)
            {
                strMsg = string.Format("主站打开失败 错误码 {0} 描述 {1}", ret, DeltaErrorCode.GetMessage(ret));
                Log(strMsg, MessageLevel.Error);
                return false;
            }

            if (_existCardCount == 0)
            {
                strMsg = "主站打开成功但未检测到板卡";
                Log(strMsg, MessageLevel.Error);
                return false;
            }

            for (ushort seq = 0; seq < _existCardCount; seq++)
            {
                ushort cardNo = 0;
                EtherCATMasterNative.CS_ECAT_Master_Get_CardSeq(seq, ref cardNo);
                ret = EtherCATMasterNative.CS_ECAT_Master_Initial(cardNo);
                _cardNoList[seq] = ret == DeltaErrorCode.NO_ERROR ? cardNo : (ushort)99;
            }

            _connectNo = _cardNoList[0];
            Log(string.Format("主站打开成功 检测到板卡 {0} 张 当前卡号 {1}", _existCardCount, _connectNo));
            return true;
        }

        /// <summary>轮询等待主站初始化完成</summary>
        /// <param name="strMsg">失败时回写的错误描述</param>
        /// <returns>成功返回 true</returns>
        private bool WaitInitialDone(ref string strMsg)
        {
            DateTime startTime = DateTime.Now;
            ushort initDone = EtherCATMasterNative.InitDoneBusy;

            while (initDone == EtherCATMasterNative.InitDoneBusy)
            {
                Thread.Sleep(InitialPollIntervalMs);
                EtherCATMasterNative.CS_ECAT_Master_Check_Initial_Done(_connectNo, ref initDone);

                if (initDone == EtherCATMasterNative.InitDoneFail)
                {
                    ushort errCode = EtherCATMasterNative.CS_ECAT_Master_Get_Initial_ErrorCode(_connectNo);
                    strMsg = string.Format("主站初始化失败 错误码 {0} 描述 {1}", errCode, DeltaErrorCode.GetMessage(errCode));
                    Log(strMsg, MessageLevel.Error);
                    return false;
                }

                if ((DateTime.Now - startTime).TotalSeconds > InitialTimeoutSeconds)
                {
                    strMsg = string.Format("主站初始化超时 超过 {0} 秒", InitialTimeoutSeconds);
                    Log(strMsg, MessageLevel.Error);
                    return false;
                }
            }

            Log("主站初始化完成");
            return true;
        }

        /// <summary>判断从站是否处于可运动的运行态</summary>
        /// <param name="nodeId">从站号</param>
        /// <param name="slotNo">槽号</param>
        /// <returns>运行态且无报警返回 true</returns>
        private bool IsSlaveOperational(ushort nodeId, ushort slotNo)
        {
            ushort opState = 0;
            ushort alarmStatus = 0;
            EtherCATMasterNative.CS_ECAT_Slave_Get_Connect_Status(_connectNo, nodeId, slotNo,
                ref opState, ref alarmStatus);
            return opState == EtherCATMasterNative.OpStateOperational && alarmStatus == 0;
        }

        #endregion

        #region 公共函数

        /// <summary>初始化主站并扫描从站</summary>
        /// <param name="strMsg">失败时回写的错误描述</param>
        /// <returns>成功返回 true</returns>
        public override bool Initialization(ref string strMsg)
        {
            Log("台达运控开始初始化");

            if (!OpenAndInitialCards(ref strMsg)) return false;
            if (!WaitInitialDone(ref strMsg)) return false;

            if (!_slaveScanner.Scan(_connectNo))
            {
                strMsg = "从站扫描失败";
                Log(strMsg, MessageLevel.Error);
                return false;
            }

            _isInitial = true;
            Log(string.Format("台达运控初始化完成 从站数量 {0}", _slaveScanner.SlaveInfos.Count));
            return true;
        }

        /// <summary>关闭主站，先断伺服再复位各卡</summary>
        public override void Close()
        {
            foreach (MontionAxis axis in _axes)
            {
                axis.ServoOff();
            }

            for (ushort seq = 0; seq < _existCardCount; seq++)
            {
                if (_cardNoList[seq] != 99)
                {
                    EtherCATMasterNative.CS_ECAT_Master_Reset(_cardNoList[seq]);
                }
            }

            EtherCATMasterNative.CS_ECAT_Master_Close();
            _isInitial = false;
            Log("台达运控已关闭");
        }

        /// <summary>注册轴，重复注册将被忽略</summary>
        /// <param name="axis">轴对象</param>
        public override void AddAxis(MontionAxis axis)
        {
            if (axis == null || _axes.Contains(axis)) return;
            _axes.Add(axis);
        }

        /// <summary>注册 IO，重复注册将被忽略</summary>
        /// <param name="io">IO 对象</param>
        public override void AddIO(MontionIO io)
        {
            if (io == null) return;

            InputIO input = io as InputIO;
            if (input != null)
            {
                if (!_inPuts.Contains(input)) _inPuts.Add(input);
                return;
            }

            OutputIO output = io as OutputIO;
            if (output != null && !_outPuts.Contains(output)) _outPuts.Add(output);
        }

        /// <summary>获取全部轴</summary>
        /// <returns>轴集合</returns>
        public override List<MontionAxis> GetAxes()
        {
            return _axes;
        }

        /// <summary>获取全部输入 IO</summary>
        /// <returns>输入 IO 集合</returns>
        public override List<InputIO> GetInPutIOs()
        {
            return _inPuts;
        }

        /// <summary>获取全部输出 IO</summary>
        /// <returns>输出 IO 集合</returns>
        public override List<OutputIO> GetOutPutIOs()
        {
            return _outPuts;
        }

        /// <summary>设置全局急停，置位后所有轴立即急停</summary>
        /// <param name="isEmergencyStop">true 为急停</param>
        public override void SetEmergencyStop(bool isEmergencyStop)
        {
            EmergencyStop = isEmergencyStop;
            if (!isEmergencyStop) return;

            foreach (MontionAxis axis in _axes)
            {
                axis.AxisStop((ushort)StopMode.Emergency);
            }
            Log("台达运控全局急停已触发", MessageLevel.Warning);
        }

        /// <summary>获取控制器类型</summary>
        /// <returns>恒返回 EtherCAT 主站卡</returns>
        public override ControlType GetControlType()
        {
            return ControlType.EtherCATMaster;
        }

        /// <summary>获取控制器厂家</summary>
        /// <returns>恒返回台达</returns>
        public override ManufacturerType GetManufacturerType()
        {
            return ManufacturerType.Delta;
        }

        /// <summary>检查总线是否全部就绪（逐从站读连接态与报警位，读共享内存非阻塞，供状态监督轮询）。</summary>
        /// <param name="faultDesc">故障描述：首个非就绪从站的站号与现象；总线健康时为空串</param>
        /// <returns>所有从站均在运行态且无报警返回 true</returns>
        public override bool CheckBusOK(out string faultDesc)
        {
            faultDesc = string.Empty;
            if (!_isInitial)
            {
                faultDesc = "控制器未初始化";
                return false;
            }

            foreach (SlaveInfo slave in _slaveScanner.SlaveInfos)
            {
                if (!IsSlaveOperational(slave.NodeId, 0))
                {
                    faultDesc = "从站 " + slave.NodeId + " 非运行态或存在报警";
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
