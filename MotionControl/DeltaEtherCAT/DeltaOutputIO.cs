using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达数字输出</summary>
    /// <remarks>低电平有效，置位写 0、复位写 1，不可按直觉反转。</remarks>
    public class DeltaOutputIO : OutputIO
    {
        #region 私有变量

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

        /// <summary>槽号</summary>
        public ushort SlotNo { get { return _slotNo; } }

        #endregion

        #region 构造函数

        /// <summary>创建台达输出并注册到控制器</summary>
        /// <param name="mControl">所属控制器</param>
        /// <param name="ioName">IO 名称</param>
        /// <param name="nodeId">从站号</param>
        /// <param name="slotNo">槽号</param>
        public DeltaOutputIO(MontionControl mControl, string ioName, ushort nodeId, ushort slotNo = 0)
            : base(mControl, ioName)
        {
            _deltaControl = (DeltaMontionControl)mControl;
            _nodeId = nodeId;
            _slotNo = slotNo;
            _tag = "台达运控输出";
        }

        #endregion

        #region 私有函数

        /// <summary>检查总线是否可操作</summary>
        /// <returns>可操作返回 true</returns>
        private bool IsBusReady()
        {
            ushort opState = 0;
            ushort alarmStatus = 0;
            EtherCATMasterNative.CS_ECAT_Slave_Get_Connect_Status(CardNo, _nodeId, _slotNo,
                ref opState, ref alarmStatus);
            return opState == EtherCATMasterNative.OpStateOperational && alarmStatus == 0;
        }

        /// <summary>输出失败日志</summary>
        /// <param name="funcName">接口名</param>
        /// <param name="ret">返回码</param>
        private void LogFail(string funcName, ushort ret)
        {
            GlobalCommData.ShowLog(_tag, IOName + " " + funcName + " 失败 错误码 " + ret
                + " 描述 " + DeltaErrorCode.GetMessage(ret), MessageLevel.Error);
        }

        #endregion

        #region 公共函数

        /// <summary>判断输出是否有效</summary>
        /// <returns>有效返回 true</returns>
        public override bool IsON()
        {
            return GetIOStatus() == 0;
        }

        /// <summary>读取输出原始状态</summary>
        /// <returns>0 为有效，1 为无效或不可用</returns>
        public override int GetIOStatus()
        {
            if (!IsBusReady()) return 1;

            ushort value = 0;
            ushort ret = EtherCATIONative.CS_ECAT_Slave_DIO_Get_Single_Output_Value(
                CardNo, _nodeId, _slotNo, IONo, ref value);
            if (ret == DeltaErrorCode.NO_ERROR) return value;

            LogFail("读取", ret);
            return 1;
        }

        /// <summary>置位输出，低电平有效故写 0</summary>
        /// <returns>0 为成功，其他为错误码</returns>
        public override int SetON()
        {
            if (!IsBusReady()) return 1;

            ushort ret = EtherCATIONative.CS_ECAT_Slave_DIO_Set_Single_Output_Value(
                CardNo, _nodeId, _slotNo, IONo, 0);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("置位", ret);
            return ret;
        }

        /// <summary>复位输出，低电平有效故写 1</summary>
        /// <returns>0 为成功，其他为错误码</returns>
        public override int SetOFF()
        {
            if (!IsBusReady()) return 1;

            ushort ret = EtherCATIONative.CS_ECAT_Slave_DIO_Set_Single_Output_Value(
                CardNo, _nodeId, _slotNo, IONo, 1);
            if (ret != DeltaErrorCode.NO_ERROR) LogFail("复位", ret);
            return ret;
        }

        #endregion
    }
}
