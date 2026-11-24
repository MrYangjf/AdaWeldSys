using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达运动缓冲原生封装</summary>
    public static class EtherCATMotionBufferNative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>设置运动缓冲坐标系参数（轴组、速度、S 曲线、原点）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Set_Crd_Parameters")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Set_Crd_Parameters(
            ushort CardNo, ushort TableNo, ushort AxisNum,
            ref ushort AxisArray, ref ushort SlotArray,
            int SycMaxVel, int SycMaxAcc, ushort S_Curve_Time,
            ushort Max_IO_Advance_Time, ushort SetOriginFlag, ref int OriginPos);

        /// <summary>向运动缓冲写入一段直线插补（9.7.3，按坐标系已注册轴顺序，不传轴数组）</summary>
        /// <remarks>手册图 9.2.22.1 标准流程 Step3 指定的接口，参数少于 MultiAxis2、不依赖速度模式设置，
        /// 兼容性最好。签名照官方 EtherCAT_DLL.cs L467。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Line_MultiAxis")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Line_MultiAxis(
            ushort CardNo, ushort TableNo, ref int TargetPosition,
            int SynTargetVel, int SynEndVel, int SynAcc, ushort AbsMode, ushort FifoNo);

        /// <summary>向运动缓冲写入一段多轴直线插补（AxesNum=1 即单轴段）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Line_MultiAxis2")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Line_MultiAxis2(
            ushort CardNo, ushort TableNo, ushort AxesNum,
            ref ushort Line_Axis, ref int TargetPosition,
            int SynTargetVel, int SynEndVel, int SynAcc, ushort AbsMode, ushort FifoNo);

        /// <summary>启动运动缓冲执行</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Start")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Start(
            ushort CardNo, ushort TableNo, ushort FifoNo);

        /// <summary>停止运动缓冲执行（Mode 停止方式，语义现场核实）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Stop")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Stop(
            ushort CardNo, ushort TableNo, ushort FifoNo, ushort Mode);

        /// <summary>读取运动缓冲余量（剩余可写/未执行段数）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Get_Remain_Buffer_Size")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Get_Remain_Buffer_Size(
            ushort CardNo, ushort TableNo, ref int RemainBufSize, ushort FifoNo);

        /// <summary>读取运动缓冲状态（Status 运行状态，BlockNum 缓冲内剩余块数）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Get_Motion_Status")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Get_Motion_Status(
            ushort CardNo, ushort TableNo, ref ushort Status, ref int BlockNum, ushort FifoNo);

        /// <summary>清空运动缓冲内未执行的段</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Clear_Buffer")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Clear_Buffer(
            ushort CardNo, ushort TableNo, ushort FifoNo);

        /// <summary>清除运动缓冲坐标系</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_MotionBuffer_Clear_Crd")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_MotionBuffer_Clear_Crd(
            ushort CardNo, ushort TableNo, ushort FifoNo);
    }
}
