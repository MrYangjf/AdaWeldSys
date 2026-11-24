using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达 PVT 多段插值原生封装</summary>
    public static class EtherCATPVTNative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>A 套单批最大点数</summary>
        public const int MaxPointCount = 8000;

        /// <summary>时间模式 0：各段时间为相邻点间隔</summary>
        public const ushort TimeModeRelative = 0;

        /// <summary>时间模式 1：各段时间为相对起点的累计值</summary>
        public const ushort TimeModeAbsolute = 1;

        #region A 套 int 脉冲

        /// <summary>下载并启动单轴 PVT 运动</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Move(ushort CardNo, ushort NodeID, ushort SlotID,
            int DataCnt, ref int TargetPos, ref int TargetTime, ref int TargetVel, ushort Abs);

        /// <summary>下载并启动单轴 PVT 运动，并约束起止速度</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVTComplete_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVTComplete_Move(ushort CardNo, ushort NodeID,
            ushort SlotID, int DataCnt, ref int TargetPos, ref int TargetTime, int StrVel, int EndVel, ushort Abs);

        /// <summary>只下载不启动 PVT 数据，供多轴同动使用</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_Config")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Config(ushort CardNo, ushort NodeID, ushort SlotID,
            int DataCnt, ref int TargetPos, ref int TargetTime, ref int TargetVel, ushort Abs);

        /// <summary>只下载不启动并约束起止速度，供多轴同动</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVTComplete_Config")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVTComplete_Config(ushort CardNo, ushort NodeID,
            ushort SlotID, int DataCnt, ref int TargetPos, ref int TargetTime, int StrVel, int EndVel, ushort Abs);

        /// <summary>统一启动已下载 PVT 数据的多个轴</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_Sync_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Sync_Move(ushort CardNo, ushort AxisNum,
            ref ushort AxisArray, ref ushort SlotArray);

        /// <summary>查询当前执行到第几个 PVT 点</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_PVT_Get_NowMotCount")]
        public static extern ushort CS_ECAT_Slave_CSP_PVT_Get_NowMotCount(ushort CardNo, ushort NodeID,
            ushort SlotID, ref ushort NowMotCount);

        #endregion

        #region B 套 double 工程单位

        /// <summary>下发 PVT 起始段并开始运动，后续可继续追加</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_Begin_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Begin_Move(ushort CardNo, ushort NodeID, ushort SlotID,
            int DataCount, ref double Pos, ref double Time, ref double Vel, ushort TimeAbsMode, ushort Finial,
            ushort Retain, double SdStopTime);

        /// <summary>运动中追加 PVT 点段，保持轨迹不断流</summary>
        [DllImport(DllPath, EntryPoint = "CS_ECAT_Slave_CSP_Start_PVT_Continue_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Continue_Move(ushort CardNo, ushort NodeID,
            ushort SlotID, int DataCount, ref double Pos, ref double Time, ref double Vel, ushort Finial);

        /// <summary>查询 PVT 执行进度与剩余缓冲区</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_Get_Information")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Get_Information(ushort CardNo, ushort NodeID,
            ushort SlotID, ref int TotalMcCount, ref int MdoneMcCount, ref int RemainBufSize, ref ushort Status);

        /// <summary>启动 PVT 循环模式</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_CycleStart")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_CycleStart(ushort CardNo, ushort NodeID,
            ushort SlotID);

        #endregion
    }
}
