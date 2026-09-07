using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达主站与从站管理原生封装</summary>
    /// <remarks>平台目标必须 x86 或 x64，禁止 AnyCPU。</remarks>
    public static class EtherCATMasterNative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>从站运行态：只有 OP 态可运动</summary>
        public const ushort OpStateOperational = 3;

        /// <summary>初始化完成</summary>
        public const ushort InitDoneOk = 0;

        /// <summary>初始化进行中</summary>
        public const ushort InitDoneBusy = 1;

        /// <summary>初始化失败</summary>
        public const ushort InitDoneFail = 99;

        /// <summary>打开主站并检测板卡数量</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Open")]
        public static extern ushort CS_ECAT_Master_Open(ref ushort existcard);

        /// <summary>初始化指定卡号主站</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Initial")]
        public static extern ushort CS_ECAT_Master_Initial(ushort CardNo);

        /// <summary>复位主站</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Reset")]
        public static extern ushort CS_ECAT_Master_Reset(ushort CardNo);

        /// <summary>关闭主站并释放所有卡</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Close")]
        public static extern ushort CS_ECAT_Master_Close();

        /// <summary>按序号取卡号</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_CardSeq")]
        public static extern ushort CS_ECAT_Master_Get_CardSeq(ushort CardNo_seq, ref ushort CardNo);

        /// <summary>取从站数量</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_SlaveNum")]
        public static extern ushort CS_ECAT_Master_Get_SlaveNum(ushort CardNo, ref ushort SlaveNum);

        /// <summary>取从站信息，用于识别设备类型</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Slave_Info")]
        public static extern ushort CS_ECAT_Master_Get_Slave_Info(ushort CardNo, ushort SeqID,
            ref ushort NodeID, ref uint VendorID, ref uint ProductCode, ref uint RevisionNo, ref uint DCTime);

        /// <summary>查询初始化是否完成，0 完成 1 进行中 99 失败</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Check_Initial_Done")]
        public static extern ushort CS_ECAT_Master_Check_Initial_Done(ushort CardNo, ref ushort InitDone);

        /// <summary>取初始化错误码</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Initial_ErrorCode")]
        public static extern ushort CS_ECAT_Master_Get_Initial_ErrorCode(ushort CardNo);

        /// <summary>取返回码文字描述</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Return_Code_Message")]
        public static extern ushort CS_ECAT_Master_Get_Return_Code_Message(ushort ReturnCode, string Message);

        /// <summary>取从站连接状态与报警状态</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Get_Connect_Status")]
        public static extern ushort CS_ECAT_Slave_Get_Connect_Status(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort OpState, ref ushort AlarmStatus);

        /// <summary>请求从站切换运行态</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Request_State")]
        public static extern ushort CS_ECAT_Slave_Request_State(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort OpState);

        /// <summary>取从站紧急报文</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Get_EMCY_Data")]
        public static extern ushort CS_ECAT_Slave_Get_EMCY_Data(ushort CardNo, ushort NodeID, ushort SlotNo,
            ref ushort ErrorCode, ref byte ErrorRegister, ref byte[] Data);

        /// <summary>SDO 写入（非阻塞）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_SDO_Send_Message")]
        public static extern ushort CS_ECAT_Slave_SDO_Send_Message(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort Index, ushort SubIndex, ushort Datasize, ref byte Data);

        /// <summary>SDO 读取（非阻塞）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_SDO_Read_Message")]
        public static extern ushort CS_ECAT_Slave_SDO_Read_Message(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort Index, ushort SubIndex, ushort Datasize, ref byte Data);

        /// <summary>SDO 快速写入（阻塞）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_SDO_Quick_Send_Message")]
        public static extern ushort CS_ECAT_Slave_SDO_Quick_Send_Message(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort Index, ushort SubIndex, ushort Datasize, ref byte Data);

        /// <summary>查询 SDO 是否完成，0 完成 1 进行中</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_SDO_Check_Done")]
        public static extern ushort CS_ECAT_Slave_SDO_Check_Done(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort Done);

        /// <summary>读 PDO 对象字典数据，模拟量输入用</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_PDO_Get_OD_Data")]
        public static extern ushort CS_ECAT_Slave_PDO_Get_OD_Data(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort IOType, ushort ODIndex, ushort ODSubIndex, ushort ByteSize, ref byte Data);

        /// <summary>写 PDO 对象字典数据，模拟量输出用</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_PDO_Set_OD_Data")]
        public static extern ushort CS_ECAT_Slave_PDO_Set_OD_Data(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort ODIndex, ushort ODSubIndex, ref byte Data);

        #region 链路健康状态

        /// <summary>取主站链路连接状态</summary>
        /// <remarks>台达无应用层心跳，链路健康只能靠本函数与工作计数器判定。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Connect_Status")]
        public static extern ushort CS_ECAT_Master_Get_Connect_Status(ushort CardNo, ref ushort Status);

        /// <summary>检查工作计数器是否异常，并取正常从站数量</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Check_Working_Counter")]
        public static extern ushort CS_ECAT_Master_Check_Working_Counter(ushort CardNo, ref ushort Abnormal_Flag,
            ref ushort Working_Slave_Cnt);

        /// <summary>取工作计数器错误累计次数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Working_Counter_ErrorCounter")]
        public static extern ushort CS_ECAT_Master_Get_Working_Counter_ErrorCounter(ushort CardNo,
            ref ushort Error_Cnt);

        /// <summary>取收发帧计数与帧错误计数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Frame_Counter")]
        public static extern ushort CS_ECAT_Master_Get_Frame_Counter(ushort CardNo, ref uint TxFrameCnt,
            ref uint RxFrameCnt, ref uint Frame_Error_Cnt);

        /// <summary>取周期通信耗时，含收发当前值与最大值</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Get_Cycle_SpendTime")]
        public static extern ushort CS_ECAT_Master_Get_Cycle_SpendTime(ushort CardNo, ref double Tx_Time,
            ref double Tx_MaxTime, ref double Rx_Time, ref double Rx_MaxTime);

        /// <summary>启用虚拟站点别名</summary>
        /// <remarks>仅用于站点编号重映射，不是虚拟轴，不可作为凸轮主轴。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Master_Virtual_NodeID_Alias_Enable")]
        public static extern ushort CS_ECAT_Master_Virtual_NodeID_Alias_Enable(ushort CardNo, ushort Enable);

        #endregion
    }
}
