using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达高级运动原生封装</summary>
    /// <remarks>覆盖多轴插补、同步、龙门、位置锁存与虚拟坐标偏移。电子凸轮见 EtherCATEcam2Native，PVT 见 EtherCATPVTNative。</remarks>
    public static class EtherCATAdvancedNative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>启动多轴直线插补</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_Multiaxes_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_Multiaxes_Move(ushort CardNo, ushort AxisNum,
            ref ushort AxisArray, ref ushort SlotArray, ref int Dist, int StrVel, int ConstVel, int EndVel,
            double Tacc, double Tdec, ushort SCurve, ushort IsAbs);

        /// <summary>启动圆弧插补</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_Arc_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_Arc_Move(ushort CardNo, ref ushort NodeID,
            ref ushort SlotNo, ref int CenterPoint, double Angle, int StrVel, int ConstVel, int EndVel,
            double Tacc, double Tdec, ushort SCurve, ushort IsAbs);

        /// <summary>启动螺旋插补</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_Heli_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_Heli_Move(ushort CardNo, ref ushort NodeID,
            ref ushort SlotNo, ref int CenterPoint, int Depth, int Pitch, ushort Dir, int StrVel, int ConstVel,
            int EndVel, double Tacc, double Tdec, ushort SCurve, ushort IsAbs);

        /// <summary>配置多轴同步组</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Sync_Config")]
        public static extern ushort CS_ECAT_Slave_CSP_Sync_Config(ushort CardNo, ushort AxisNum,
            ref ushort AxisArray, ref ushort SlotArray, ushort Enable);

        /// <summary>启动已配置的多轴同步组</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Sync_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Sync_Move(ushort CardNo);

        /// <summary>启用龙门同步</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Gantry_Enable")]
        public static extern ushort CS_ECAT_Slave_CSP_Gantry_Enable(ushort CardNo, ushort GantryNo,
            ushort MasterNodeID, ushort MasterSlotID, ushort SlaveNodeID, ushort SlaveSlotID,
            int Motor_Pulse_Per_Turn, double Motor_MaxTorque, double K_I, double K_VI,
            double Filter_Frequency, ushort Enable);

        /// <summary>配置位置锁存</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Set_TouchProbe_Config")]
        public static extern ushort CS_ECAT_Slave_Motion_Set_TouchProbe_Config(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort TriggerMode, ushort Signal_Source);

        /// <summary>取位置锁存状态</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_TouchProbe_Status")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_TouchProbe_Status(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort Status);

        /// <summary>取位置锁存值</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_TouchProbe_Position")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_TouchProbe_Position(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref int LatchPosition);

        /// <summary>启用虚拟坐标偏移</summary>
        /// <remarks>仅作坐标系平移与零点偏移，不是虚拟主轴，不可用于凸轮主轴。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Virtual_Set_Enable")]
        public static extern ushort CS_ECAT_Slave_CSP_Virtual_Set_Enable(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort Enable);

        /// <summary>写入虚拟坐标偏移量</summary>
        /// <remarks>单位为脉冲，Enable 为 1 时生效。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Virtual_Set_Command")]
        public static extern ushort CS_ECAT_Slave_CSP_Virtual_Set_Command(ushort CardNo, ushort NodeID,
            ushort SlotID, int Command);
    }
}
