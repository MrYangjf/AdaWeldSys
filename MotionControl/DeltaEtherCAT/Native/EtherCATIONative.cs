using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达数字量 IO 原生封装</summary>
    /// <remarks>IO 为 NPN 低电平有效，状态值 0 表示有效。</remarks>
    public static class EtherCATIONative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>读取全部输入位图</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_DIO_Get_Input_Value")]
        public static extern ushort CS_ECAT_Slave_DIO_Get_Input_Value(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort value);

        /// <summary>读取全部输出位图</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_DIO_Get_Output_Value")]
        public static extern ushort CS_ECAT_Slave_DIO_Get_Output_Value(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort value);

        /// <summary>设置全部输出位图</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_DIO_Set_Output_Value")]
        public static extern ushort CS_ECAT_Slave_DIO_Set_Output_Value(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort value);

        /// <summary>读取单路输入</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_DIO_Get_Single_Input_Value")]
        public static extern ushort CS_ECAT_Slave_DIO_Get_Single_Input_Value(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort BitNum, ref ushort value);

        /// <summary>读取单路输出</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_DIO_Get_Single_Output_Value")]
        public static extern ushort CS_ECAT_Slave_DIO_Get_Single_Output_Value(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort BitNum, ref ushort value);

        /// <summary>设置单路输出</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_DIO_Set_Single_Output_Value")]
        public static extern ushort CS_ECAT_Slave_DIO_Set_Single_Output_Value(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort BitNum, ushort value);
    }
}
