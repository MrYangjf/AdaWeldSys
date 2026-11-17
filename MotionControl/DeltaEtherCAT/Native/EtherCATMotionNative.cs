using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达运动控制原生封装</summary>
    /// <remarks>位置与速度用脉冲。加减速时间用秒。S 曲线与回零加速度用毫秒。</remarks>
    public static class EtherCATMotionNative
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>取当前运动模式</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_MoveMode")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_MoveMode(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref byte Mode);

        /// <summary>设置运动模式</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Set_MoveMode")]
        public static extern ushort CS_ECAT_Slave_Motion_Set_MoveMode(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort OpMode);

        /// <summary>伺服使能，1 为上电</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Set_Svon")]
        public static extern ushort CS_ECAT_Slave_Motion_Set_Svon(ushort CardNo, ushort NodeID,
            ushort SlotNo, ushort On_Off);

        /// <summary>复位驱动器报警</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Ralm")]
        public static extern ushort CS_ECAT_Slave_Motion_Ralm(ushort CardNo, ushort NodeID, ushort SlotNo);

        /// <summary>设置指令位置</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Set_Position")]
        public static extern ushort CS_ECAT_Slave_Motion_Set_Position(ushort CardNo, ushort NodeID,
            ushort SlotNo, int Pos);

        /// <summary>急停</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Emg_Stop")]
        public static extern ushort CS_ECAT_Slave_Motion_Emg_Stop(ushort CardNo, ushort NodeID, ushort SlotNo);

        /// <summary>减速停止</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Sd_Stop")]
        public static extern ushort CS_ECAT_Slave_Motion_Sd_Stop(ushort CardNo, ushort NodeID,
            ushort SlotNo, double Tdec);

        /// <summary>取驱动器状态字</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_StatusWord")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_StatusWord(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort StatusWord);

        /// <summary>取指令位置</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_Command")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_Command(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref int Command);

        /// <summary>取反馈位置</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_Position")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_Position(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref int Position);

        /// <summary>取实际位置</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_Actual_Position")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_Actual_Position(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref int ActualPosition);

        /// <summary>取运动完成标志，1 为完成</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_Mdone")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_Mdone(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort Mdone);

        /// <summary>取当前速度</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_Current_Speed")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_Current_Speed(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref int Speed);

        /// <summary>取当前扭矩</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Motion_Get_Torque")]
        public static extern ushort CS_ECAT_Slave_Motion_Get_Torque(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref short Torque);

        /// <summary>启动周期同步位置运动</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_Move(ushort CardNo, ushort NodeID, ushort SlotNo,
            int Dist, int StrVel, int ConstVel, int EndVel, double Tacc, double Tdec, ushort SCurve, ushort IsAbs);

        /// <summary>启动点动，0 为正向 1 为负向</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_V_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_V_Move(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort Dir, int StrVel, int ConstVel, double Tacc, ushort SCurve);

        /// <summary>运动中变更速度</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Velocity_Change")]
        public static extern ushort CS_ECAT_Slave_CSP_Velocity_Change(ushort CardNo, ushort NodeID,
            ushort SlotNo, int NewSpeed, double Tsec);

        /// <summary>启动周期同步速度运动</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSV_Start_Move")]
        public static extern ushort CS_ECAT_Slave_CSV_Start_Move(ushort CardNo, ushort NodeID, ushort SlotNo,
            int Target_Velocity, double Acceleration, ushort Curve_Mode, ushort Acc_Type);

        /// <summary>启动周期同步扭矩运动</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CST_Start_Move")]
        public static extern ushort CS_ECAT_Slave_CST_Start_Move(ushort CardNo, ushort NodeID, ushort SlotNo,
            short Target_Torque, uint Slope, ushort Curve_Mode);

        /// <summary>CST 扭矩模式输出使能（关闭即撤除力矩）</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CST_Output_Enable")]
        public static extern ushort CS_ECAT_Slave_CST_Output_Enable(ushort CardNo, ushort NodeID, ushort SlotID,
            ushort Enable);

        /// <summary>配置回零参数，速度为脉冲每秒，加速度为毫秒</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Home_Config")]
        public static extern ushort CS_ECAT_Slave_Home_Config(ushort CardNo, ushort NodeID, ushort SlotNo,
            ushort Mode, int Offset, uint FirstVel, uint SecondVel, uint Acceleration);

        /// <summary>启动回零</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Home_Move")]
        public static extern ushort CS_ECAT_Slave_Home_Move(ushort CardNo, ushort NodeID, ushort SlotNo);

        /// <summary>取回零状态，0 完成 1 进行中</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_Home_Status")]
        public static extern ushort CS_ECAT_Slave_Home_Status(ushort CardNo, ushort NodeID,
            ushort SlotNo, ref ushort Status);

        /// <summary>设置电子齿轮比</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Set_Gear")]
        public static extern ushort CS_ECAT_Slave_CSP_Set_Gear(ushort CardNo, ushort NodeID, ushort SlotNo,
            double Numerator, double Denominator, short Enable);

        /// <summary>配置 CSP 软目标到达判定窗口（位置窗口 + 时间窗口）</summary>
        /// <remarks>CSP 运动族专用到位判定。位置偏差进入 <c>Position_Window</c> 并持续 <c>Window_Time</c> 即判到达。
        /// 与 <c>Motion_Get_Mdone</c>（只表示运动结束，不区分到位/被中断）互补。</remarks>
        /// <param name="Window_Time">时间窗口</param>
        /// <param name="Position_Window">位置窗口</param>
        /// <param name="Enable">使能</param>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Set_SoftTargetReach")]
        public static extern ushort CS_ECAT_Slave_CSP_Set_SoftTargetReach(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort Window_Time, uint Position_Window, ushort Enable);

        /// <summary>读取 CSP 软目标到达状态</summary>
        /// <param name="Status">到达状态（语义待核实，暂不作判据）</param>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Get_SoftTargetReach_Status")]
        public static extern ushort CS_ECAT_Slave_CSP_Get_SoftTargetReach_Status(ushort CardNo, ushort NodeID,
            ushort SlotID, ref ushort Status);

        /// <summary>状态字使能位</summary>
        public const ushort StatusWordOperationEnabled = 0x0004;

        /// <summary>状态字报警位</summary>
        public const ushort StatusWordFault = 0x0008;
    }
}
