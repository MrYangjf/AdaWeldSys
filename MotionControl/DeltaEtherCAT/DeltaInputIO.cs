using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达数字输入</summary>
    /// <remarks>低电平有效，状态值 0 表示有效；读取前检查总线与使能。</remarks>
    public class DeltaInputIO : InputIO
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

        /// <summary>创建台达输入并注册到控制器</summary>
        /// <param name="mControl">所属控制器</param>
        /// <param name="ioName">IO 名称</param>
        /// <param name="nodeId">从站号</param>
        /// <param name="slotNo">槽号</param>
        public DeltaInputIO(MontionControl mControl, string ioName, ushort nodeId, ushort slotNo = 0)
            : base(mControl, ioName)
        {
            _deltaControl = (DeltaMontionControl)mControl;
            _nodeId = nodeId;
            _slotNo = slotNo;
            _tag = "台达运控输入";
        }

        #endregion

        #region 公共函数

        /// <summary>判断输入是否有效</summary>
        /// <returns>有效返回 true</returns>
        public override bool IsON()
        {
            return GetIOStatus() == 0;
        }

        /// <summary>读取输入原始状态</summary>
        /// <returns>0 为有效，1 为无效或不可用</returns>
        public override int GetIOStatus()
        {
            ushort opState = 0;
            ushort alarmStatus = 0;
            EtherCATMasterNative.CS_ECAT_Slave_Get_Connect_Status(CardNo, _nodeId, _slotNo,
                ref opState, ref alarmStatus);
            if (opState != EtherCATMasterNative.OpStateOperational || alarmStatus != 0 || !IOEnable) return 1;

            ushort value = 0;
            ushort ret = EtherCATIONative.CS_ECAT_Slave_DIO_Get_Single_Input_Value(
                CardNo, _nodeId, _slotNo, IONo, ref value);
            if (ret == DeltaErrorCode.NO_ERROR) return value;

            GlobalCommData.ShowLog(_tag, IOName + " 读取失败 错误码 " + ret + " 描述 " + DeltaErrorCode.GetMessage(ret),
                MessageLevel.Error);
            return 1;
        }

        #endregion
    }
}
