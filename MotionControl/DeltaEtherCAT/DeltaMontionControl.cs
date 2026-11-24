using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达 EtherCAT 控制器</summary>
    public class DeltaMontionControl : MontionControl
    {
        #region 私有变量

        private const string Tag = "台达运控";
        // 初始化等待取自 ROOT/FLOW_TIMEOUT（运行期读取；缺省 30s / 100ms = 改造前行为）
        private static int InitialTimeoutSeconds { get { return FlowTimeoutSettings.Current.MasterInitSeconds; } }
        private static int InitialPollIntervalMs { get { return FlowTimeoutSettings.Current.MasterInitPollMs; } }

        private readonly ushort[] _cardNoList;
        private readonly DeltaSlaveScanner _slaveScanner;
        private ushort _existCardCount;

        /// <summary>主站是否已打开（决定 Close 时是否执行 Master_Close）</summary>
        private bool _masterOpened;

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

            // 主站已打开：后续初始化失败也必须走 Close 释放
            _masterOpened = true;

            if (_existCardCount == 0)
            {
                strMsg = "主站打开成功但未检测到板卡";
                Log(strMsg, MessageLevel.Error);
                return false;
            }

            for (ushort seq = 0; seq < _existCardCount; seq++)
            {
                ushort cardNo = 0;
                ret = EtherCATMasterNative.CS_ECAT_Master_Get_CardSeq(seq, ref cardNo);
                if (ret != DeltaErrorCode.NO_ERROR)
                {
                    strMsg = string.Format("读取板卡序号失败 序号 {0} 错误码 {1} 描述 {2}",
                        seq, ret, DeltaErrorCode.GetMessage(ret));
                    Log(strMsg, MessageLevel.Error);
                    return false;
                }

                ret = EtherCATMasterNative.CS_ECAT_Master_Initial(cardNo);
                if (ret != DeltaErrorCode.NO_ERROR)
                {
                    strMsg = string.Format("主站初始化失败 卡号 {0} 错误码 {1} 描述 {2}",
                        cardNo, ret, DeltaErrorCode.GetMessage(ret));
                    Log(strMsg, MessageLevel.Error);
                    // 哨兵卡号：关闭时跳过该卡的 Reset
                    _cardNoList[seq] = (ushort)99;
                    // 首卡失败即无法连接：立即失败，禁止空等超时
                    if (seq == 0) return false;
                    continue;
                }

                _cardNoList[seq] = cardNo;
            }

            _connectNo = _cardNoList[0];
            Log(string.Format("主站打开成功 检测到板卡 {0} 张 当前卡号 {1}", _existCardCount, _connectNo),
                MessageLevel.Debug);
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

            Log("主站初始化完成", MessageLevel.Debug);
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

        /// <summary>输出扫描到的从站清单</summary>
        private void LogScanList()
        {
            for (int i = 0; i < _slaveScanner.SlaveInfos.Count; i++)
            {
                SlaveInfo slave = _slaveScanner.SlaveInfos[i];
                Log(string.Format("从站 {0} 节点 {1} 设备 {2} 厂商 0x{3:X8} 产品码 0x{4:X8} 版本 0x{5:X8}",
                    slave.SeqId, slave.NodeId, slave.DeviceName,
                    slave.VendorId, slave.ProductCode, slave.RevisionNo),
                    MessageLevel.Debug);
            }
        }

        #endregion

        #region 公共函数

        /// <summary>初始化主站并扫描从站</summary>
        /// <param name="strMsg">失败时回写的错误描述</param>
        /// <returns>成功返回 true</returns>
        public override bool Initialization(ref string strMsg)
        {
            Log("台达运控开始初始化", MessageLevel.Debug);

            if (!OpenAndInitialCards(ref strMsg)) return false;
            if (!WaitInitialDone(ref strMsg)) return false;

            if (!_slaveScanner.Scan(_connectNo))
            {
                strMsg = "从站扫描失败";
                Log(strMsg, MessageLevel.Error);
                return false;
            }

            _isInitial = true;
            Log(string.Format("台达运控初始化完成 从站数量 {0}", _slaveScanner.SlaveInfos.Count),
                MessageLevel.Debug);
            LogScanList();
            return true;
        }

        /// <summary>关闭主站，先断伺服再复位各卡</summary>
        public override void Close()
        {
            if (!_masterOpened)
            {
                _isInitial = false;
                return;
            }

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
            _masterOpened = false;
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

            if (io is InputIO input)
            {
                if (!_inPuts.Contains(input)) _inPuts.Add(input);
                return;
            }

            if (io is OutputIO output && !_outPuts.Contains(output)) _outPuts.Add(output);
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

        /// <summary>检查总线是否全部就绪（逐从站读连接态与报警</summary>

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
