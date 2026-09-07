using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达 ECAM2 电子凸轮原生封装</summary>
    /// <remarks>
    /// 主轴来源 SourceType 只有 0（远端从站）与 1（轴卡脉冲输入口）两个合法值，不存在虚拟轴取值。
    /// 无位置主轴时只能改用时间模式凸轮 Profile_CamIn_TimeMode，该模式从轴按时间推进，无法跟随外部位置。
    /// 一代 ECAM 接口不再使用，新功能一律走 ECAM2。
    /// </remarks>
    public static class EtherCATEcam2Native
    {
#if X86
        private const string DllPath = "EtherCAT_DLL.dll";
#else
        private const string DllPath = "EtherCAT_DLL_x64.dll";
#endif

        /// <summary>凸轮表最大编号</summary>
        public const ushort MaxCamNo = 31;

        /// <summary>单张凸轮表最大点数</summary>
        public const ushort MaxProfilePoint = 8000;

        /// <summary>主轴来源：远端脉波模块或伺服驱动器</summary>
        public const ushort SourceTypeRemoteSlave = 0;

        /// <summary>主轴来源：轴卡板载脉冲输入口</summary>
        public const ushort SourceTypePulseInput = 1;

        /// <summary>主轴来源单位：位置</summary>
        public const ushort SourceUnitPosition = 0;

        /// <summary>主轴来源单位：时间</summary>
        public const ushort SourceUnitTime = 1;

        #region 建表与啮合

        /// <summary>写入凸轮曲线点表</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_Data")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_Data(ushort CardNo, ushort CamNo,
            ushort DataNum, ref double MasterPos_mm, ref double SlavePos_mm, ref ushort Curve_Type,
            ref double Velocity_Scale);

        /// <summary>设置造表模式，须在建表前调用</summary>
        /// <remarks>Mode 0 为按用户参数造表，1 为自动衔接五次曲线。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Set_Profile_Mode")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Set_Profile_Mode(ushort CardNo, ushort Mode);

        /// <summary>对凸轮曲线做自动平滑</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_Auto_Smooth")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_Auto_Smooth(ushort CardNo, ushort CamNo,
            ushort DataNum, ref double SlavePosUserUnit);

        /// <summary>删除指定凸轮表</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Delete_Profile_Data")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Delete_Profile_Data(ushort CardNo, ushort CamNo);

        /// <summary>从轴接入凸轮</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_CamIn")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_CamIn(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort MasterNodeID, ushort MasterSlotID, ushort SourceType, ushort SourceNo,
            ushort CamNo, ushort Periodic, ushort MasterAbsolute, ushort SlaveAbsolute, double MasterOffset,
            double SlaveOffset, double MasterScaling, double SlaveScaling, ushort StartMode,
            int MasterAxis_mm_Pulse, int SlaveAxis_mm_Pulse, double Slave_MaxVel_mm, double Slave_Acc_mm,
            double Slave_Dec_mm, short MasterAxis_Dir, short SlaveAxis_Dir, ushort BufferMode);

        /// <summary>时间模式啮合凸轮，无需主轴站点</summary>
        /// <remarks>须先调用 Master_Axis_Source_Unit_Type 把主轴单位切为时间。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_CamIn_TimeMode")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_CamIn_TimeMode(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort CamNo, ushort Periodic, ushort SlaveAbsolute, double SlaveOffset,
            double SlaveScaling, ushort StartMode, int SlaveAxis_mm_Pulse, double Slave_MaxVel_mm,
            double Slave_Acc_mm, double Slave_Dec_mm, short SlaveAxis_Dir, ushort BufferMode);

        /// <summary>设置主轴来源单位，须在啮合前调用</summary>
        /// <remarks>SourceUnitType 取 SourceUnitPosition 或 SourceUnitTime，PerodicTime 单位为微秒。</remarks>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Master_Axis_Source_Unit_Type")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Master_Axis_Source_Unit_Type(ushort CardNo,
            ushort NodeID, ushort SlotID, ushort SourceUnitType, int PerodicTime);

        /// <summary>从轴退出凸轮</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_CamOut")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_CamOut(ushort CardNo, ushort NodeID,
            ushort SlotID);

        /// <summary>停用凸轮</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Disable")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Disable(ushort CardNo, ushort NodeID,
            ushort SlotID);

        /// <summary>尝试啮合，等待主轴进入啮合条件</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_CamSet")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_CamSet(ushort CardNo, ushort NodeID,
            ushort SlotID);

        #endregion

        #region 运行中调整

        /// <summary>运行中修改凸轮表指定点</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Profile_Modify")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Profile_Modify(ushort CardNo, ushort CamNo,
            ushort PointNo, ref double Parameters);

        /// <summary>运行中相位偏移</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Phasing")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Phasing(ushort CardNo, ushort NodeID, ushort SlotID,
            double Dist, double MaxVel, double Acceleration, double Deceleration);

        /// <summary>配置凸轮切换点</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_CamSwitch")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_CamSwitch(ushort CardNo, ushort CamNo,
            ushort SwitchNum, ref ushort Percent, ushort Cam_Mode);

        /// <summary>查询凸轮切换状态</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_CamSwitch_Status")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_CamSwitch_Status(ushort CardNo, ushort NodeID,
            ushort SlotID, ref ushort Status);

        /// <summary>设置啮合条件参数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Set_Engage_Parameters")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Set_Engage_Parameters(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort SourceType, ushort EngageNodeID, ushort EngageSlotID, ushort SourceNo,
            ushort Enable);

        /// <summary>设置脱离条件参数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Set_DisEngage_Parameters")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Set_DisEngage_Parameters(ushort CardNo,
            ushort NodeID, ushort SlotID, ushort SourceType, ushort DisEngageNodeID, ushort DisEngageSlotID,
            ushort SourceNo, ushort Enable);

        /// <summary>设置啮合补偿参数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Set_Compensate_Parameters")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Set_Compensate_Parameters(ushort CardNo,
            ushort NodeID, ushort SlotID, ushort SourceType, ushort CompensateNodeID, ushort CompensateSlotID,
            ushort SourceNo, double TargetPos, double ActiveRangeRatio, double MaxVel, double Acceleration,
            double Deceleration, ushort Enable);

        #endregion

        #region 飞剪与旋切

        /// <summary>初始化飞剪参数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_FlyingShear_Initial")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_FlyingShear_Initial(ushort CardNo,
            ushort FlyingShearNo, double MasterRadius, double SlaveRadius, double CutLength,
            double MasterStartPosition, double MasterSyncPosition, double SlaveSyncPosition,
            double SlaveEndPosition, double SlaveWaitPosition, double SlaveVelocity, double SlaveAcceleration,
            double SlaveDeceleration);

        /// <summary>启动飞剪</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_FlyingShear_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_FlyingShear_Move(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort FlyingShearNo, ushort MasterNodeID, ushort MasterSlotID, ushort SourceType,
            ushort SourceNo, int MasterAxis_PulseRev, int SlaveAxis_PulseRev, short MasterAxis_Dir,
            short SlaveAxis_Dir);

        /// <summary>初始化旋切参数</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_RotaryCut_Initial")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_RotaryCut_Initial(ushort CardNo, ushort RotCutNo,
            double RotaryAxisRadius, ushort RotaryAxisKnifeNum, double FeedAxisRadius, double CutLenth,
            double SyncStartPos, double SyncStopPos);

        /// <summary>启动旋切</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_RotaryCut_Move")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_RotaryCut_Move(ushort CardNo, ushort NodeID,
            ushort SlotID, ushort RotCutNo, ushort MasterNodeID, ushort MasterSlotID, ushort SourceType,
            ushort SourceNo, int RotaryAxis_PulseRev, int FeedAxis_PulseRev, short RotaryAxis_Dir,
            short FeedAxis_Dir);

        #endregion

        #region 状态与试算

        /// <summary>查询凸轮运行状态</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Get_Status")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Get_Status(ushort CardNo, ushort NodeID,
            ushort SlotID, ref ushort Status);

        /// <summary>查询从轴当前凸轮编号与表位置</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Get_Slave_Info")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Get_Slave_Info(ushort CardNo, ushort NodeID,
            ushort SlotID, ref short pi16_CurrentCamNo, ref int pi32_CurrentTablePos);

        /// <summary>不启动即试算主轴位置对应的从轴位置</summary>
        [DllImport(DllPath, EntryPoint = "_ECAT_Slave_CSP_Start_ECAM2_Get_PreviewTable")]
        public static extern ushort CS_ECAT_Slave_CSP_Start_ECAM2_Get_PreviewTable(ushort CardNo, ushort CamNo,
            double MasterPosMm, double MasterOffset, double SlaveOffset, double MasterScaling,
            double SlaveScaling, ref double SlavePosMm);

        #endregion
    }
}
