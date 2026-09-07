using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达 PVT 多段插值原生封装</summary>
    /// <remarks>
    /// 分 A/B 两套：A 套为 int 脉冲、一次性下发；B 套为 double 工程单位、支持流式续喂。
    /// 焊缝跟踪等连续跟随场景用 B 套，靠 Get_Information 的剩余缓冲区实现水位闭环。
    /// 全部 API 基于 CSP 模式，调用前轴必须已 Set_Move_Mode(8) 且伺服使能。
    /// </remarks>
    public static class EtherCATPVTNative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>A 套单批最大点数</summary>
        public const int MaxPointCount = 8000;

        /// <summary>时间模式：相对时间，每一段的时间为相邻两点的间隔</summary>
        public const ushort TimeModeRelative = 0;

        /// <summary>时间模式：绝对时间，每一段的时间为相对于起点的累计值</summary>
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

        /// <summary>只下载不启动 PVT 数据并约束起止速度，供多轴同动使用</summary>
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
        /// <remarks>TimeAbsMode 取 TimeModeRelative 或 TimeModeAbsolute，语义待现场实测确认。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_PVT_Begin_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Begin_Move(ushort CardNo, ushort NodeID, ushort SlotID,
            int DataCount, ref double Pos, ref double Time, ref double Vel, ushort TimeAbsMode, ushort Finial,
            ushort Retain, double SdStopTime);

        /// <summary>运动中追加 PVT 点段，保持轨迹不断流</summary>
        /// <remarks>
        /// EntryPoint 在 SDK 头文件中缺少下划线前缀，与同族其它函数不一致，疑为厂商笔误，此处照抄。
        /// 运行时若抛 EntryPointNotFoundException，改为 _ECAT_Slave_CSP_Start_PVT_Continue_Move 再验。
        /// </remarks>
        [DllImport(DllPath, EntryPoint = "CS_ECAT_Slave_CSP_Start_PVT_Continue_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_PVT_Continue_Move(ushort CardNo, ushort NodeID,
            ushort SlotID, int DataCount, ref double Pos, ref double Time, ref double Vel, ushort Finial);

        /// <summary>查询 PVT 执行进度与剩余缓冲区</summary>
        /// <remarks>RemainBufSize 为续喂节拍的判据，水位低于阈值时追加下一段。</remarks>
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
