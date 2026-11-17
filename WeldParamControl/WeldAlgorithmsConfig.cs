using System;
using System.Globalization;
using System.Xml.Linq;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>焊缝跟踪模式。</summary>
    /// <remarks>
    /// CspRealtime：地图反算 Y 轴目标，运控周期写 CSP。
    /// PvtStream：轨迹点攒批下发（保留作回退）。
    /// </remarks>
    public enum SeamTrackingMode
    {
        CspRealtime = 0,

        PvtStream = 1
    }

    /// <summary>焊接工艺参数的焊缝宽度来源。</summary>
    /// <remarks>
    /// Manual：手动固定宽度（<see cref="WeldAlgorithmsConfig.ManualSeamWidthMm"/>），查表输出全程恒定；
    /// Laser：实测宽度驱动，按前置量缓存后输出。
    /// 来源不再是独立配置项（只读派生）；
    /// 派生源改由 <see cref="WeldAlgorithmsConfig.EnableGalvoWeave"/> 唯一决定
    /// 即光斑宽度跟随；光斑宽度不可调，只随摆幅变化。
    /// </remarks>
    public enum SeamWidthSource
    {
        Manual = 0,

        Laser = 1
    }

    /// <summary>焊接算法配置（按 UI 分区组织、按标准分流落盘）。</summary>
    /// <remarks>
    /// 承载 WeldParamControl 模块全部算法的运行参数。
    /// 配置属性按 UI 界面分为四区。
    /// 四区为系统设置、工艺快捷、自适应、工艺设定值。
    /// 分区枚举见 <see cref="AlgorithmsZone"/>，另有 PVT 组。
    /// 支撑分区域修改、读取与保存。
    /// 分区读写入口为 <see cref="LoadZoneXml"/> 与 <see cref="SaveZoneXml"/>。
    /// 分区侧另有 <see cref="LoadZoneIni"/> 与 <see cref="SaveZoneIni"/>。
    /// 总入口为 <see cref="LoadConfig"/> 与 <see cref="SaveConfig"/>。
    /// 流程侧总入口为 <see cref="LoadProcessIni"/> 与 <see cref="SaveProcessIni"/>。
    /// 总入口内部依次调分区方法，净行为不变。
    /// 落盘分流：设备参数用 XML，流程阈值用 INI。
    ///   - <b>XML</b>（Config\WeldParamControl\Algorithm_XML\WeldParamControl.xml）：几何/安装/模式/容量/标定。
    ///   - <b>INI</b>（Config\WeldParamControl\Algorithm_INI\WeldParamControlAlgorithm.ini）：阈值/开关/节拍/报警/计数。
    /// 两后端由 <see cref="WeldParamManager"/> 独占，外部只读。
    /// </remarks>
    public class WeldAlgorithmsConfig
    {
        #region 分区枚举

        /// <summary>配置 UI 分区（支撑分区修改/读取/保存）。</summary>
        /// <remarks>
        /// 分区与落盘后端：①系统设置 跨 XML+INI；②工艺快捷 / ③自适应 仅 INI；④工艺设定值 跨 XML+INI（Tracking 几何在 XML，TA/LA/振镜预设 在 INI）。
        /// 分区归属：光斑宽度来源三项（ManualSeamWidthMm/SpotWidthGain/ParamOutputHz）归 ③自适应区；
        /// EnableGuide 只被 ②工艺快捷区持有；EnableMonotonicConstraint 只归 ①系统设置区。
        /// </remarks>
        public enum AlgorithmsZone
        {
            /// <summary>① 系统设置区：轨迹点设定区 + 线激光标定区</summary>
            System = 0,

            /// <summary>② 工艺快捷区：工艺设置页·快捷启用区 + 快捷指令区</summary>
            Quick = 1,

            /// <summary>③ 自适应区：工艺设置页·焊参调整面板</summary>
            Adapt = 2,

            /// <summary>④ 工艺设定值区：Tracking 区 + TA 区 + 动作预设</summary>
            Preset = 3
        }

        #endregion

        #region 常量

        /// <summary>线激光引导配置节名（XML：几何/安装段；INI：阈值段）</summary>
        public const string SeamGuideSection = "SEAM_GUIDE";

        private const string PathAxisPerLaser = SeamGuideSection + "/AXIS_MM_PER_LASER_MM";
        private const string PathTeachLockY = SeamGuideSection + "/TEACH_LOCK_Y_MM";
        private const string PathGuideSign = SeamGuideSection + "/GUIDE_SIGN";
        private const string PathLeadDistance = SeamGuideSection + "/LEAD_DISTANCE_MM";
        private const string PathTrackingMode = SeamGuideSection + "/TRACKING_MODE";
        private const string PathYAxisMin = SeamGuideSection + "/Y_AXIS_MIN_MM";
        private const string PathYAxisMax = SeamGuideSection + "/Y_AXIS_MAX_MM";
        private const string PathMaxMapPoints = SeamGuideSection + "/MAX_MAP_POINTS";
        private const string PathGaussianSigma = SeamGuideSection + "/GAUSSIAN_SIGMA_MM";
        private const string PathGaussianHalfWindow = SeamGuideSection + "/GAUSSIAN_HALF_WINDOW_MM";
        private const string PathRobotYFilterAlpha = SeamGuideSection + "/ROBOT_Y_FILTER_ALPHA";
        private const string PathRobotYGain = SeamGuideSection + "/ROBOT_Y_GAIN";

        // —— XML 路径常量：LASER_CALIBRATION 段（设备几何标定） ——
        private const string PathCalCenterY = CalibrationSection + "/CALIBRATED_CENTER_Y";
        private const string PathCalCenterZ = CalibrationSection + "/CALIBRATED_CENTER_Z";
        private const string PathCalAngleDeg = CalibrationSection + "/LASER_ANGLE_DEG";

        // —— INI 键常量：SEAM_GUIDE 阈值段（①系统设置区） ——
        private const string IniEnable = "ENABLE_GUIDE";
        private const string IniNoValueTolerance = "NO_VALUE_TOLERANCE_MM";
        private const string IniOutlierLimit = "OUTLIER_LIMIT_MM";
        private const string IniMonotonicWindow = "MONOTONIC_WINDOW_MM";
        private const string IniMonotonicDeadband = "MONOTONIC_DEADBAND_MM";
        private const string IniMonotonicRelease = "MONOTONIC_RELEASE_MM";
        private const string IniConsecutiveReject = "CONSECUTIVE_REJECT_FRAMES";
        private const string IniCspMinSeg = "CSP_MIN_SEG_MM";
        private const string IniMaxTargetStep = "MAX_TARGET_STEP_MM";
        private const string IniOutlierSigmaK = "OUTLIER_SIGMA_K";
        private const string IniOutlierSlopeLimit = "OUTLIER_SLOPE_LIMIT_MM_PER_MM";
        private const string IniNearbyWindow = "NEARBY_WINDOW_MM";
        private const string IniMaxStepChange = "MAX_STEP_CHANGE_MM";
        private const string IniEnableMonotonic = "ENABLE_MONOTONIC_CONSTRAINT";
        private const string IniOutputDeadband = "OUTPUT_DEADBAND_MM";
        private const string IniMaxVel = "MAX_VEL_MM_PER_S";
        private const string IniFeedforwardSafetyFactor = "FEEDFORWARD_SAFETY_FACTOR";
        private const string IniMaxAcc = "MAX_ACC_MM_PER_S2";
        private const string IniReverseAlarm = "REVERSE_ALARM_MM";
        private const string IniReverseDeadband = "REVERSE_DEADBAND_MM";

        // —— INI 键常量：WELD_PARAM_ADJUST 段（③自适应区） ——
        private const string IniMaxConsecutiveFailures = "MAX_CONSECUTIVE_FAILURES";
        private const string IniSegmentDistance = "SEGMENT_DISTANCE_MM";

        // 旧键仅保留读取，迁移期忽略并留一条 Info（回退一版）
        private const string IniIsCountType = "IS_COUNT_TYPE";
        private const string IniCountNum = "COUNT_NUM";
        private const string IniManualSeamWidth = "MANUAL_SEAM_WIDTH_MM";
        private const string IniSpotWidthGain = "SPOT_WIDTH_GAIN";
        private const string IniParamOutputHz = "PARAM_OUTPUT_HZ";

        // —— INI 键常量：PROCESS_CONTROL 段（②工艺快捷区：启用开关 + 快捷调试目标位；④工艺设定值区：TA/LA/振镜预设） ——
        private const string IniEnableSeamTrace = "ENABLE_SEAM_TRACE";
        private const string IniEnableWireAlign = "ENABLE_WIRE_ALIGN";
        private const string IniEnableTempCtrl = "ENABLE_TEMP_CTRL";
        private const string IniSpotCalibPos = "SPOT_CALIB_POS_MM";
        private const string IniWireLockPos = "WIRE_LOCK_POS_MM";
        private const string IniLockYCalib = "LOCK_Y_CALIB";
        private const string IniLockSpotCalib = "LOCK_SPOT_CALIB";
        private const string IniLockWirePos = "LOCK_WIRE_POS";
        private const string IniEnableGalvoWeave = "ENABLE_GALVO_WEAVE";
        private const string IniLaSpotTarget = "LA_SPOT_TARGET_MM";
        private const string IniLaSpot1 = "LA_SPOT1_MM";
        private const string IniLaSpot2 = "LA_SPOT2_MM";
        private const string IniLaPose1 = "LA_POSE1_MM";
        private const string IniLaPose2 = "LA_POSE2_MM";
        private const string IniTaForce = "TA_FORCE_N";
        private const string IniTaPose1 = "TA_POSE1_MM";
        private const string IniTaPose2 = "TA_POSE2_MM";

        /// <summary>TA 工作零位坐标（工艺设置页 TA 区，唯一可编辑源）</summary>
        private const string IniTaZeroPose = "TA_ZERO_POSE_MM";
        private const string IniGalvoAutoCenterComp = "GALVO_AUTO_CENTER_COMP";
        private const string IniGalvoCenterCompGain = "GALVO_CENTER_COMP_GAIN";
        private const string IniGalvoPattern = "GALVO_PATTERN";

        #endregion

        #region 公共变量

        #region ① 系统设置区（SystemZone：XML 几何/标定段 + INI SEAM_GUIDE 阈值段）

        /// <summary>参数调整配置节名（INI：阈值/计数段）</summary>
        public const string AdjustmentSection = "WELD_PARAM_ADJUST";

        /// <summary>线激光标定配置节名（XML：设备几何段）</summary>
        public const string CalibrationSection = "LASER_CALIBRATION";

        /// <summary>工艺调试快捷配置节名（INI：开关+目标位+预设）</summary>
        public const string ProcessControlSection = "PROCESS_CONTROL";

        // —— XML 路径常量：SEAM_GUIDE 几何 / 安装段 ——
        /// <summary>焊缝跟踪模式（CSP 实时目标 / PVT 流式段）</summary>
        /// <remarks>UI落点：无控件，PVT 未验证，强制 CSP。</remarks>
        public SeamTrackingMode TrackingMode { get; set; }

        /// <summary>机器人 Y 一阶低通滤波系数，新值权重（0~1）</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtGeoRobotYFilterAlpha（BtnSaveAlgoThresh 保存）。</remarks>
        public double RobotYFilterAlpha { get; set; }

        /// <summary>高斯平滑标准差（毫米），越大越平滑</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtGeoGaussianSigma（BtnSaveAlgoThresh 保存）。</remarks>
        public double GaussianSigmaMm { get; set; }

        /// <summary>高斯窗口半宽（毫米），须小于超前量</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtGeoGaussianHalfWindow（BtnSaveAlgoThresh 保存）。</remarks>
        public double GaussianHalfWindowMm { get; set; }

        /// <summary>示教 Y 轴标定点（毫米），标定对的轴侧</summary>
        /// <remarks>起焊锚点：StartCspOutput 以此为闭环基准。
        /// 同时用于到位比对防呆。
        /// 与 LASER_CALIBRATION 段的标定中心点 Y 构成标定对（特征侧）。
        /// UI落点：系统设置页·线激光标定区 txtTeachLockY（BtnSaveLaserCalib 保存）。</remarks>
        public double TeachLockYMm { get; set; }

        /// <summary>标定的焊缝中心点 Y（线激光图像坐标，mm）</summary>
        /// <remarks>UI落点：系统设置页·线激光标定区 txtCalibCenterY（BtnSaveLaserCalib 保存）。</remarks>
        public double CalibratedCenterY { get; set; }

        /// <summary>标定中心点 Z（毫米，激光坐标系）：现场标定记录项</summary>
        /// <remarks>UI落点：系统设置页·线激光标定区 txtCalibCenterZ（BtnSaveLaserCalib 保存）。</remarks>
        public double CalibratedCenterZ { get; set; }

        /// <summary>线激光-水平轴角度（度）：现场标定记录项</summary>
        /// <remarks>UI落点：系统设置页·线激光标定区 txtCalibAngle（BtnSaveLaserCalib 保存）。</remarks>
        public double LaserToHorizontalAngleDeg { get; set; }

        /// <summary>焊缝地图点数上限，超过后丢弃最旧点</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区（线激光点过滤组）txtGeoMaxMapPoints。
        /// 分区归属：①System 区（UI 落点为系统设置页，XML 键路径 SEAM_GUIDE/MAX_MAP_POINTS 不变）。</remarks>
        public int MaxMapPoints { get; set; }

        /// <summary>机器人行程内无有效结果容差（毫米），超出即报警</summary>
        /// <remarks>读入钳位 1~50mm，缺省 5mm；
        /// UI落点：系统设置页·轨迹点设定区（BtnSaveAlgoThresh 保存）。</remarks>
        public double NoValueToleranceMm { get; set; }

        /// <summary>超限点剔除阈值（毫米），跳变超限即剔</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区（BtnSaveAlgoThresh 保存）。</remarks>
        public double OutlierLimitMm { get; set; }

        /// <summary>野值统计邻域窗口（毫米，按机器人 X 计）</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowNearbyWindow（BtnSaveAlgoThresh 保存）。</remarks>
        public double NearbyWindowMm { get; set; }

        /// <summary>野值 3σ 统计判定倍数</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowOutlierSigmaK（BtnSaveAlgoThresh 保存）。</remarks>
        public double OutlierSigmaK { get; set; }

        /// <summary>野值切向偏差系数：超出切线外推即判炸点</summary>
        /// <remarks>切线连续性系数：连续曲线不剔，孤立飞点剔除。
        /// UI落点：系统设置页·轨迹点设定区 txtFlowOutlierSlope（BtnSaveAlgoThresh 保存）。</remarks>
        public double OutlierSlopeLimitMmPerMm { get; set; }

        /// <summary>单帧最大变化量（毫米/帧），超出即剔除</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowMaxStepChange（BtnSaveAlgoThresh 保存）。</remarks>
        public double MaxStepChangeMm { get; set; }

        /// <summary>目标突变上限（毫米），超出即拒帧</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowMaxTargetStep（BtnSaveAlgoThresh 保存）。</remarks>
        public double MaxTargetStepMm { get; set; }

        /// <summary>连续拒绝达到此帧数即重新种子</summary>
        /// <remarks>区分毛刺与真实台阶：持续落差放行。
        /// 避免地图永久停止更新导致修正量冻结。
        /// UI落点：系统设置页·轨迹点设定区 txtFlowConsecReject（BtnSaveAlgoThresh 保存）。</remarks>
        public int ConsecutiveRejectFrames { get; set; }

        /// <summary>方向锁最大持距（毫米），仅作兜底上限</summary>
        /// <remarks>改由迟滞释放后，本值仅作兜底重种子。
        /// 防止一次错误的锁把整条焊缝绑架。
        /// UI落点：系统设置页·轨迹点设定区 txtFlowMonoWindow（BtnSaveAlgoThresh 保存）。</remarks>
        public double MonotonicWindowMm { get; set; }

        /// <summary>方向判定死区（毫米），超出才锁向</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowMonoDeadband（BtnSaveAlgoThresh 保存）。</remarks>
        public double MonotonicDeadbandMm { get; set; }

        /// <summary>方向锁释放阈值（毫米），超出才解锁</summary>
        /// <remarks>须大于噪声带、小于真实反弯量。
        /// UI落点：系统设置页·轨迹点设定区 txtFlowMonoRelease（BtnSaveAlgoThresh 保存）。</remarks>
        public double MonotonicReleaseMm { get; set; }

        /// <summary>是否启用单调约束（抑制来回摆动）</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 selParamMono（BtnSaveAlgoThresh 保存）。
        /// 只归①系统设置区，不设第二开关。</remarks>
        public bool EnableMonotonicConstraint { get; set; }

        /// <summary>输出死区（毫米）：小于此值保持上一输出</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowOutDeadband（BtnSaveAlgoThresh 保存）。</remarks>
        public double OutputDeadbandMm { get; set; }

        /// <summary>CSP 输出最大速度（毫米/秒）</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowMaxVel（BtnSaveAlgoThresh 保存）。</remarks>
        public double MaxVelMmPerS { get; set; }

        /// <summary>CSP 输出最大加速度（毫米/秒平方）</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowMaxAcc（BtnSaveAlgoThresh 保存）。</remarks>
        public double MaxAccMmPerS2 { get; set; }

        /// <summary>速度前馈安全系数：按斜率抬升速度/加速度上限</summary>
        /// <remarks>前馈只抬上限、不产生位移，失真即回退。
        /// UI落点：系统设置页·轨迹点设定区 txtParamFfSafety（BtnSaveAlgoThresh 保存）。</remarks>
        public double FeedforwardSafetyFactor { get; set; }

        /// <summary>CSP 最小段距（毫米），仅留痕与回显</summary>
        /// <remarks>不参与写段判定，仅为兼容旧键保留。
        /// ⚠️ 勿与每拍步长同量级，否则轴停走。
        /// UI落点：系统设置页·轨迹点设定区 txtFlowCspMinSeg（BtnSaveAlgoThresh 保存）。</remarks>
        public double CspMinSegMm { get; set; }

        /// <summary>X 折返报警里程（毫米），超限即报警停止</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowReverseAlarm（BtnSaveAlgoThresh 保存）。</remarks>
        public double ReverseAlarmMm { get; set; }

        /// <summary>X 回退死区（毫米）：不计方向，抑制抖动</summary>
        /// <remarks>UI落点：系统设置页·轨迹点设定区 txtFlowReverseDeadband（BtnSaveAlgoThresh 保存）。</remarks>
        public double ReverseDeadbandMm { get; set; }

        #endregion

        #region ② 工艺快捷区（QuickZone：INI SEAM_GUIDE ENABLE_GUIDE 键 + PROCESS_CONTROL 段启用/指令组）

        /// <summary>是否启用线激光追踪工艺（跟踪系统总开关）</summary>
        /// <remarks>UI落点：系统设置页·工艺启用设置区 swEnableSeamTrace（即改即存）。</remarks>
        public bool EnableSeamTrace { get; set; }

        /// <summary>是否启用光丝对中工艺（送丝压力 / 光斑对焦对中）</summary>
        /// <remarks>UI落点：系统设置页·工艺启用设置区 swEnableWireAlign（即改即存）。</remarks>
        public bool EnableWireAlign { get; set; }

        /// <summary>是否启用温控工艺（温度采集与上下限监控）</summary>
        /// <remarks>UI落点：系统设置页·工艺启用设置区 swEnableTempCtrl（即改即存）。</remarks>
        public bool EnableTempCtrl { get; set; }

        /// <summary>是否启用振镜摆动（关闭时摆幅恒 0，振镜不动；同时是焊缝宽度来源开关：开=实测宽度驱动摆幅）</summary>
        /// <remarks>UI落点：系统设置页·工艺启用设置区 swEnableGalvoWeaveQuick。
        /// 本开关同时决定 <see cref="SeamWidthSource"/>（开=Laser，关=Manual）。
        /// 即光斑宽度跟随，不可调，只随摆幅变化。</remarks>
        public bool EnableGalvoWeave { get; set; }

        /// <summary>是否启用线激光引导 Y 轴（关=只转发）</summary>
        /// <remarks>UI落点：系统设置页·工艺启用设置区 swEnableAdapt（即改即存）。
        /// 只归②工艺快捷区，不设第二入口。</remarks>
        public bool EnableGuide { get; set; }

        /// <summary>Y 轴标定锁：勾选后移到 TeachLockYMm 保持</summary>
        /// <remarks>UI落点：Tracking 指令区两只单选。</remarks>
        public bool LockYCalib { get; set; }

        /// <summary>光斑标定锁：勾选后移到 SpotCalibPosMm 保持</summary>
        /// <remarks>UI落点：工艺设置页·快捷指令区（即改即存）。</remarks>
        public bool LockSpotCalib { get; set; }

        /// <summary>送丝锁定：勾选后移到 WireLockPosMm 保持</summary>
        /// <remarks>UI落点：工艺设置页·快捷指令区（即改即存）。</remarks>
        public bool LockWirePos { get; set; }

        /// <summary>光斑回标定位（毫米），快捷项锁定目标</summary>
        /// <remarks>UI落点：工艺设置页·快捷指令区（即改即存）。</remarks>
        public double SpotCalibPosMm { get; set; }

        /// <summary>送丝锁定位置（毫米），快捷项目标位</summary>
        /// <remarks>UI落点：工艺设置页·快捷指令区（即改即存）。</remarks>
        public double WireLockPosMm { get; set; }

        #endregion

        #region ③ 自适应区（AdaptZone：INI WELD_PARAM_ADJUST 段）

        /// <summary>连续失败上限，达到即中断并保持</summary>
        /// <remarks>UI落点：工艺设置页·焊参调整面板 txtFlowMaxConsecFail（BtnSaveAutoParam 保存）。</remarks>
        public int MaxConsecutiveFailures { get; set; }

        /// <summary>每段距离 mm 达值取段内均值查表</summary>
        /// <remarks>UI落点：工艺设置页·焊参调整面板 txtFlowCountNum（文案「每段距离(mm)」）。
        /// 与缺值累计同一把距离尺。</remarks>
        public double SegmentDistanceMm { get; set; }

        /// <summary>宽度来源（只读）：振镜摆动开=Laser，关=Manual</summary>
        /// <remarks>Manual：按手动固定宽度（<see cref="ManualSeamWidthMm"/>）查表，输出全程恒定。
        /// Laser：线激光实测宽度动态驱动振镜摆幅。
        /// 工艺参数按 XL（测量 X + 前置量）缓存。
        /// 机器人到达后输出。
        /// SEAM_WIDTH_SOURCE 独立 INI 键停用（停读写，存量残留无害）。
        /// 来源改由 <see cref="EnableGalvoWeave"/> 唯一决定。
        /// 光斑跟随即振镜摆动，本属性只读。
        /// UI落点：无独立控件（振镜摆动开关即来源开关）。</remarks>
        public SeamWidthSource SeamWidthSource
        {
            get { return EnableGalvoWeave ? SeamWidthSource.Laser : SeamWidthSource.Manual; }
        }

        /// <summary>手动固定宽度（毫米），Manual 时唯一查表输入</summary>
        /// <remarks>UI落点：轨迹点设定区 txtParamManualWidth。</remarks>
        public double ManualSeamWidthMm { get; set; }

        /// <summary>光斑宽度系数 W，随查找表逐档生成</summary>
        /// <remarks>UI落点：轨迹点设定区 txtParamSpotGain。</remarks>
        public double SpotWidthGain { get; set; }

        /// <summary>工艺参数输出频率（Hz，默认 60）：XL 输出支路的节流上限</summary>
        /// <remarks>UI落点：轨迹点设定区 selParamOutputHz。</remarks>
        public int ParamOutputHz { get; set; }

        #endregion

        #region ④ 工艺设定值区（PresetZone：XML SEAM_GUIDE 几何段 Tracking 6 项 + INI PROCESS_CONTROL 段 TA/LA/振镜预设）

        /// <summary>轴/线激光比例系数（轴 mm 每激光 mm）</summary>
        /// <remarks>UI落点：工艺设置页·Tracking 设置区 txtGeoAxisPerLaser（BtnSaveLaserCalib 保存）。</remarks>
        public double AxisMmPerLaserMm { get; set; }

        /// <summary>偏差方向（+1 或 -1），与轴正方向对齐</summary>
        /// <remarks>UI落点：工艺设置页·Tracking 设置区 selGeoGuideSign（BtnSaveLaserCalib 保存）。</remarks>
        public int GuideSign { get; set; }

        /// <summary>线激光超前量（毫米），即安装位置到焊头距离</summary>
        /// <remarks>入图键 = 测量时机器人 X + 超前量。
        /// 使查表目标与焊枪位置相位对齐。
        /// UI落点：工艺设置页·Tracking 设置区 txtGeoLeadDistance（BtnSaveLaserCalib 保存）。</remarks>
        public double LeadDistanceMm { get; set; }

        /// <summary>机器人 Y 补偿系数：写入时反向抵消其增量</summary>
        /// <remarks>机器人 Y 平移由跟踪轴全额反向抵消。
        /// 只补测量到写入间的前置量行程。
        /// 修正量约为应补偿两倍时，置 0 关闭。
        /// UI落点：工艺设置页·Tracking 设置区 txtGeoRobotYGain（BtnSaveLaserCalib 保存）。</remarks>
        public double RobotYGain { get; set; }

        /// <summary>Y 轴行程下限（毫米），反算目标的下限幅值</summary>
        /// <remarks>UI落点：工艺设置页·Tracking 设置区 txtGeoYAxisMin（BtnSaveLaserCalib 保存）。</remarks>
        public double YAxisMinMm { get; set; }

        /// <summary>Y 轴行程上限（毫米），反算目标的上限幅值</summary>
        /// <remarks>UI落点：工艺设置页·Tracking 设置区 txtGeoYAxisMax（BtnSaveLaserCalib 保存）。</remarks>
        public double YAxisMaxMm { get; set; }

        /// <summary>TA 下压力目标值（N）：TA auto 指令下发给力矩电机的目标力。</summary>
        /// <remarks>归工艺配置管理，轴配置仅留模板字段。
        /// 轴配置侧仅留作迁移，运行时以本值为准。
        /// ⛔ 单位牛顿，下发前换算成 CST 千分比。
        /// UI落点：工艺设置页·TA 参数区 txtTaForce（BtnSaveTaParam 保存）。</remarks>
        public double TaForceN { get; set; }

        /// <summary>TA Pose1 预设调试位（毫米）：快捷动作「TA Pose1」移至此位。</summary>
        /// <remarks>UI落点：工艺设置页·TA 参数区 txtTaPose1（BtnSaveTaParam 保存）。</remarks>
        public double TaPose1Mm { get; set; }

        /// <summary>TA Pose2 预设调试位（毫米）：快捷动作「TA Pose2」移至此位。</summary>
        /// <remarks>UI落点：工艺设置页·TA 参数区 txtTaPose2（BtnSaveTaParam 保存）。</remarks>
        public double TaPose2Mm { get; set; }

        /// <summary>TA 工作零位坐标（毫米）：`TA_ZeroPose` 指令的目标坐标（键 TA_ZERO_POSE_MM）。</summary>
        /// <remarks>UI落点：工艺设置页·TA 参数区 txtTaZeroPose（BtnSaveTaParam 保存），与 TA_Pose1/2 同构。
        /// ⛔ 参数弹窗不再提供该项；旧键 ROOT/TA_HOME/TA_HOME_ZERO_POSE_MM 只作一次性迁移来源，不再被读取。</remarks>
        public double TaZeroPoseMm { get; set; }

        /// <summary>目标光斑（毫米），经曲线A 反查 LA 基准位</summary>
        /// <remarks>UI落点：工艺设置页·LA 参数区 txtLaSpotTarget（BtnSaveLaParam 保存）。</remarks>
        public double LaSpotTargetMm { get; set; }

        /// <summary>LA Spot1 预设光斑大小（毫米）：快捷动作「LA Spot1」的设定值</summary>
        /// <remarks>UI落点：工艺设置页·LA 参数区 txtLaSpot1（BtnSaveLaParam 保存）。</remarks>
        public double LaSpot1Mm { get; set; }

        /// <summary>LA Spot2 预设光斑大小（毫米）：快捷动作「LA Spot2」的设定值</summary>
        /// <remarks>UI落点：工艺设置页·LA 参数区 txtLaSpot2（BtnSaveLaParam 保存）。</remarks>
        public double LaSpot2Mm { get; set; }

        /// <summary>LA Pose1 预设位（毫米），用于观察光斑</summary>
        /// <remarks>UI落点：工艺设置页·LA 参数区 txtLaPose1（BtnSaveLaParam 保存）。</remarks>
        public double LaPose1Mm { get; set; }

        /// <summary>LA Pose2 预设调试位（毫米）</summary>
        /// <remarks>UI落点：工艺设置页·LA 参数区 txtLaPose2（BtnSaveLaParam 保存）。</remarks>
        public double LaPose2Mm { get; set; }

        /// <summary>振镜摆动图形（1 圆 2 八字 3 直线 4 圆弧；人工编辑项）</summary>
        /// <remarks>UI落点：快捷区振镜图形按钮组。</remarks>
        public int GalvoPattern { get; set; }

        /// <summary>光丝对中偏差是否自动微调振镜中心</summary>
        /// <remarks>UI落点：无界面控件（人工编辑 INI）。</remarks>
        public bool GalvoAutoCenterComp { get; set; }

        /// <summary>振镜中心补偿增益（偏差 mm → 中心 mm 的比例，可负；现场标定）</summary>
        /// <remarks>UI落点：无界面控件（人工编辑 INI）。</remarks>
        public double GalvoCenterCompGain { get; set; }

        #endregion

        #region PVT 回退路径专用（不持久化，不经界面调配）

        /// <summary>单批下发点数（PVT 回退专用，不持久化）</summary>
        public int BatchSize { get; set; }

        /// <summary>续喂下限（PVT 回退专用，不持久化）</summary>
        public int MinRemainBuffer { get; set; }

        /// <summary>默认帧间隔（PVT 回退专用，不持久化）</summary>
        public double DefaultFrameSeconds { get; set; }

        /// <summary>最长输出间隔（PVT 回退专用，不持久化）</summary>
        public int MaxOutputIntervalMs { get; set; }

        /// <summary>点表容量上限（PVT 回退专用，不持久化）</summary>
        public int MaxPendingPoints { get; set; }

        #endregion

        #endregion

        #region 构造函数

        /// <summary>创建配置并填入默认值</summary>
        public WeldAlgorithmsConfig()
        {
            // ① 系统设置区
            TrackingMode = SeamTrackingMode.CspRealtime;
            RobotYFilterAlpha = 0.2;
            GaussianSigmaMm = 12.0;
            GaussianHalfWindowMm = 30.0;
            TeachLockYMm = 0.0;
            CalibratedCenterY = 5.0;
            CalibratedCenterZ = 5.0;
            LaserToHorizontalAngleDeg = 0.0;
            NoValueToleranceMm = 5.0;
            OutlierLimitMm = 2.0;
            NearbyWindowMm = 20.0;
            OutlierSigmaK = 3.0;
            OutlierSlopeLimitMmPerMm = 0.15;
            MaxStepChangeMm = 0.5;
            MaxTargetStepMm = 5.0;
            ConsecutiveRejectFrames = 20;
            MonotonicWindowMm = 200.0;
            MonotonicDeadbandMm = 0.05;
            MonotonicReleaseMm = 0.30;
            EnableMonotonicConstraint = true;
            // 输出死区只管是否更新输出。
            // 写出链路无突变护栏，勿改用整形判据。
            OutputDeadbandMm = 0.01;
            MaxVelMmPerS = 50.0;
            MaxAccMmPerS2 = 500.0;
            FeedforwardSafetyFactor = 3.0;
            // 段距下限 = 单脉冲当量（0.001mm）。
            // 低于 1 脉冲会写入零位移段塞满缓冲。
            CspMinSegMm = 0.001;
            ReverseAlarmMm = 1.0;
            ReverseDeadbandMm = 0.1;

            // ② 工艺快捷区
            EnableSeamTrace = true;
            EnableWireAlign = false;
            EnableTempCtrl = false;
            // 振镜摆动：默认关闭，现场确认后再开。
            EnableGalvoWeave = false;
            EnableGuide = true;
            LockYCalib = false;
            LockSpotCalib = false;
            LockWirePos = false;
            SpotCalibPosMm = 0.0;
            WireLockPosMm = 0.0;

            // ③ 自适应区（SeamWidthSource 为派生只读，无构造赋值）
            MaxConsecutiveFailures = 5;
            SegmentDistanceMm = 5.0;
            ManualSeamWidthMm = 1.2;
            SpotWidthGain = 1.0;
            ParamOutputHz = 60;

            // ④ 工艺设定值区
            AxisMmPerLaserMm = 1.0;
            GuideSign = 1;
            LeadDistanceMm = 60.0;
            RobotYGain = 1.0;
            YAxisMinMm = -15.0;
            YAxisMaxMm = 15.0;
            MaxMapPoints = 2000;
            // TA 工艺调试参数（下压力/两个预设位初值 0）
            TaForceN = 0.0;
            TaPose1Mm = 0.0;
            TaPose2Mm = 0.0;
            TaZeroPoseMm = 0.0;
            // TA/LA 动作预设：光斑默认取最聚焦锚点 0.6mm；Pose 位默认 0（现场示教后经 UI 保存）
            LaSpotTargetMm = 0.6;
            LaSpot1Mm = 0.6;
            LaSpot2Mm = 1.0;
            LaPose1Mm = 0.0;
            LaPose2Mm = 0.0;
            GalvoPattern = 3;   // 默认直线
            // 光丝对中微调默认关闭，仅监控状态。
            GalvoAutoCenterComp = false;
            GalvoCenterCompGain = 1.0;

            // PVT 回退路径专用
            BatchSize = 4;
            MinRemainBuffer = 200;
            DefaultFrameSeconds = 1.0 / 60.0;
            MaxOutputIntervalMs = 250;
            MaxPendingPoints = 8000;
        }

        #endregion

        #region 私有函数（通用 XML 读写辅助）

        private static int ReadInt(XdocumentReaderWriter reader, string path, int def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(txt, out int val) ? val : def;
        }

        private static double ReadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : def;
        }

        /// <summary>读取跟踪模式枚举，值非法时回退默认</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">默认值</param>
        /// <returns>跟踪模式</returns>
        private static SeamTrackingMode ReadTrackingMode(XdocumentReaderWriter reader, string path, SeamTrackingMode def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString());
            if (string.IsNullOrWhiteSpace(txt)) return def;
            try
            {
                return (SeamTrackingMode)Enum.Parse(typeof(SeamTrackingMode), txt.Trim(), true);
            }
            catch (ArgumentException)
            {
                return def;
            }
        }

        private static string Text(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>写入子元素，不存在则创建，避免重复保存产生重</summary>
        /// <remarks>复节点</remarks>
        /// <param name="reader">配置读写器</param>
        /// <param name="parent">父元素</param>
        /// <param name="name">子元素名</param>
        /// <param name="value">子元素值</param>
        private static void SetChild(XdocumentReaderWriter reader, XElement parent, string name, string value)
        {
            XElement child = parent.Element(name);
            if (child == null)
            {
                reader.AddElement(parent, name, value);
            }
            else
            {
                child.Value = value;
            }
        }

        #endregion

        #region 私有函数（通用 INI 读写辅助）

        /// <summary>从 INI 读取 double（invariant 文化，与 XML 端一致）</summary>
        /// <param name="key">键名。</param>
        /// <returns>计算结果。</returns>
        private static double ReadDoubleIni(INIFile ini, string section, string key, double def)
        {
            string txt = ini.ReadString(section, key, def.ToString(CultureInfo.InvariantCulture));
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : def;
        }

        /// <summary>从 INI 读取 int（invariant 文化）</summary>
        /// <param name="key">键名。</param>
        /// <returns>结果码。</returns>
        private static int ReadIntIni(INIFile ini, string section, string key, int def)
        {
            string txt = ini.ReadString(section, key, def.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val) ? val : def;
        }

        #endregion

        #region 公共函数（总入口）

        /// <summary>从 XML 读写器加载设备几何 / 安装常量 / 标定（①系统设置</summary>
        /// <remarks>区 + ④工艺设定值区的 Tracking 几何），缺失项保留默认值。</remarks>
        /// <param name="reader">XML 配置读写器</param>
        public void LoadConfig(XdocumentReaderWriter reader)
        {
            LoadZoneXml(reader, AlgorithmsZone.System);
            LoadZoneXml(reader, AlgorithmsZone.Preset);
        }

        /// <summary>把设备几何 / 安装常量 / 标定写入 XML 读写器（仅更新对</summary>
        /// <remarks>应段，不重建整篇）。</remarks>
        /// <param name="reader">XML 配置读写器</param>
        public void SaveConfig(XdocumentReaderWriter reader)
        {
            SaveZoneXml(reader, AlgorithmsZone.System);
            SaveZoneXml(reader, AlgorithmsZone.Preset);
        }

        /// <summary>从 INI 读写器加载流程类阈值 / 开关 / 节拍 / 报警 / 计数（①阈</summary>
        /// <remarks>值段 + ②快捷 + ③自适应 + ④预设），缺失项保留默认值。</remarks>
        /// <param name="ini">INI 配置读写器</param>
        public void LoadProcessIni(INIFile ini)
        {
            if (ini == null) return;

            LoadZoneIni(ini, AlgorithmsZone.System);
            LoadZoneIni(ini, AlgorithmsZone.Adapt);
            LoadZoneIni(ini, AlgorithmsZone.Preset);
            LoadZoneIni(ini, AlgorithmsZone.Quick);

            // PVT 回退路径参数的默认值保护（不经 INI）
            if (BatchSize < 1) BatchSize = 1;
            if (MaxOutputIntervalMs < 1) MaxOutputIntervalMs = 1;
            if (MaxPendingPoints < 1) MaxPendingPoints = 1;
            if (DefaultFrameSeconds <= 0.0) DefaultFrameSeconds = 1.0 / 60.0;
        }

        /// <summary>把流程类阈值 / 开关 / 节拍 / 报警 / 计数写入 INI 读写器（invariant 文</summary>
        /// <remarks>化，与 XML 端一致）。</remarks>
        /// <param name="ini">INI 配置读写器</param>
        public void SaveProcessIni(INIFile ini)
        {
            if (ini == null) return;

            SaveZoneIni(ini, AlgorithmsZone.System);
            SaveZoneIni(ini, AlgorithmsZone.Adapt);
            SaveZoneIni(ini, AlgorithmsZone.Preset);
            SaveZoneIni(ini, AlgorithmsZone.Quick);

            ini.SaveToFile();
        }

        #endregion

        #region 公共函数（分区读写：AlgorithmsZone）

        /// <summary>按分区从 XML 加载配置（仅①系统设置/④工艺设定值两区）。</summary>
        /// <remarks>缺失项保留默认值。</remarks>
        /// <param name="reader">XML 配置读写器</param>
        /// <param name="zone">UI 分区</param>
        public void LoadZoneXml(XdocumentReaderWriter reader, AlgorithmsZone zone)
        {
            if (reader == null || reader.GetXDocument() == null || reader.GetXDocument().Root == null) return;

            switch (zone)
            {
                case AlgorithmsZone.System: LoadZoneXmlSystem(reader); break;
                case AlgorithmsZone.Preset: LoadZoneXmlPreset(reader); break;
            }
        }

        /// <summary>按分区把配置写入 XML 读写器（仅 ①系统设置 / ④工艺设定</summary>
        /// <remarks>值 两区有 XML 内容，其余区为空操作；仅更新对应段，不重建整篇）。</remarks>
        /// <param name="reader">XML 配置读写器</param>
        /// <param name="zone">UI 分区</param>
        public void SaveZoneXml(XdocumentReaderWriter reader, AlgorithmsZone zone)
        {
            if (reader == null || reader.GetXDocument() == null || reader.GetXDocument().Root == null) return;

            switch (zone)
            {
                case AlgorithmsZone.System: SaveZoneXmlSystem(reader); break;
                case AlgorithmsZone.Preset: SaveZoneXmlPreset(reader); break;
            }
        }

        /// <summary>按分区从 INI 读写器加载配置，缺失项保留默认值（含</summary>
        /// <remarks>各分区钳位保护）。</remarks>
        /// <param name="ini">INI 配置读写器</param>
        /// <param name="zone">UI 分区</param>
        public void LoadZoneIni(INIFile ini, AlgorithmsZone zone)
        {
            if (ini == null) return;

            switch (zone)
            {
                case AlgorithmsZone.System: LoadZoneIniSystem(ini); break;
                case AlgorithmsZone.Quick: LoadZoneIniQuick(ini); break;
                case AlgorithmsZone.Adapt: LoadZoneIniAdapt(ini); break;
                case AlgorithmsZone.Preset: LoadZoneIniPreset(ini); break;
            }
        }

        /// <summary>按分区把配置写入 INI 读写器（invariant 文化，与 XML 端一致；不落盘，</summary>
        /// <remarks>由调用方决定 SaveToFile 时机）。</remarks>
        /// <param name="ini">INI 配置读写器</param>
        /// <param name="zone">UI 分区</param>
        public void SaveZoneIni(INIFile ini, AlgorithmsZone zone)
        {
            if (ini == null) return;

            switch (zone)
            {
                case AlgorithmsZone.System: SaveZoneIniSystem(ini); break;
                case AlgorithmsZone.Quick: SaveZoneIniQuick(ini); break;
                case AlgorithmsZone.Adapt: SaveZoneIniAdapt(ini); break;
                case AlgorithmsZone.Preset: SaveZoneIniPreset(ini); break;
            }
        }

        #endregion

        #region 私有函数（分区实现：XML）

        /// <summary>①系统设置区 XML 加载：示教锁定 Y / Y 滤波 / 高斯平滑 / 跟踪模</summary>
        /// <remarks>式 / 焊缝地图点数上限（SEAM_GUIDE 几何段）+ 线激光标定（LASER_CALIBRATION 段）</remarks>
        private void LoadZoneXmlSystem(XdocumentReaderWriter reader)
        {
            TeachLockYMm = ReadDouble(reader, PathTeachLockY, TeachLockYMm);
            RobotYFilterAlpha = ReadDouble(reader, PathRobotYFilterAlpha, RobotYFilterAlpha);
            GaussianSigmaMm = ReadDouble(reader, PathGaussianSigma, GaussianSigmaMm);
            GaussianHalfWindowMm = ReadDouble(reader, PathGaussianHalfWindow, GaussianHalfWindowMm);
            TrackingMode = ReadTrackingMode(reader, PathTrackingMode, TrackingMode);
            MaxMapPoints = ReadInt(reader, PathMaxMapPoints, MaxMapPoints);

            if (GaussianSigmaMm <= 0.0) GaussianSigmaMm = 12.0;
            if (GaussianHalfWindowMm <= 0.0) GaussianHalfWindowMm = 30.0;
            if (RobotYFilterAlpha < 0.0) RobotYFilterAlpha = 0.0;
            if (RobotYFilterAlpha > 1.0) RobotYFilterAlpha = 1.0;
            if (MaxMapPoints < 1) MaxMapPoints = 1;

            CalibratedCenterY = ReadDouble(reader, PathCalCenterY, CalibratedCenterY);
            CalibratedCenterZ = ReadDouble(reader, PathCalCenterZ, CalibratedCenterZ);
            LaserToHorizontalAngleDeg = ReadDouble(reader, PathCalAngleDeg, LaserToHorizontalAngleDeg);
        }

        /// <summary>①系统设置区 XML 保存：Y 锁/滤波/平滑/模式/点数/标定</summary>
        /// <remarks>点数上限改归 ①System 区，键路径不变。</remarks>
        private void SaveZoneXmlSystem(XdocumentReaderWriter reader)
        {
            XElement guideSection = reader.GetElementByPath(SeamGuideSection) ?? reader.AddElement(SeamGuideSection);
            SetChild(reader, guideSection, "TEACH_LOCK_Y_MM", Text(TeachLockYMm));
            SetChild(reader, guideSection, "ROBOT_Y_FILTER_ALPHA", Text(RobotYFilterAlpha));
            SetChild(reader, guideSection, "GAUSSIAN_SIGMA_MM", Text(GaussianSigmaMm));
            SetChild(reader, guideSection, "GAUSSIAN_HALF_WINDOW_MM", Text(GaussianHalfWindowMm));
            SetChild(reader, guideSection, "TRACKING_MODE", TrackingMode.ToString());
            SetChild(reader, guideSection, "MAX_MAP_POINTS", MaxMapPoints.ToString(CultureInfo.InvariantCulture));

            XElement calibSection = reader.GetElementByPath(CalibrationSection) ?? reader.AddElement(CalibrationSection);
            SetChild(reader, calibSection, "CALIBRATED_CENTER_Y", Text(CalibratedCenterY));
            SetChild(reader, calibSection, "CALIBRATED_CENTER_Z", Text(CalibratedCenterZ));
            SetChild(reader, calibSection, "LASER_ANGLE_DEG", Text(LaserToHorizontalAngleDeg));
        }

        /// <summary>④工艺设定值区 XML 加载：Tracking 几何 6 项（SEAM_GUIDE 几何段）</summary>
        /// <remarks>点数上限已迁 ①System 区，本区不读。</remarks>
        private void LoadZoneXmlPreset(XdocumentReaderWriter reader)
        {
            AxisMmPerLaserMm = ReadDouble(reader, PathAxisPerLaser, AxisMmPerLaserMm);
            GuideSign = ReadInt(reader, PathGuideSign, GuideSign);
            LeadDistanceMm = ReadDouble(reader, PathLeadDistance, LeadDistanceMm);
            RobotYGain = ReadDouble(reader, PathRobotYGain, RobotYGain);
            YAxisMinMm = ReadDouble(reader, PathYAxisMin, YAxisMinMm);
            YAxisMaxMm = ReadDouble(reader, PathYAxisMax, YAxisMaxMm);

            if (AxisMmPerLaserMm <= 0.0) AxisMmPerLaserMm = 1.0;
            if (RobotYGain < 0.0) RobotYGain = 0.0;
            else if (RobotYGain > 2.0) RobotYGain = 2.0;
            if (GuideSign >= 0) GuideSign = 1; else GuideSign = -1;
            if (LeadDistanceMm < 0.0) LeadDistanceMm = 0.0;
            if (YAxisMinMm > YAxisMaxMm)
            {
                double swap = YAxisMinMm;
                YAxisMinMm = YAxisMaxMm;
                YAxisMaxMm = swap;
            }
        }

        /// <summary>④工艺设定值区 XML 保存：Tracking 几何 6 项（SEAM_GUIDE 几何段）</summary>
        /// <remarks>点数上限已迁 ①System 区，本区不写。</remarks>
        private void SaveZoneXmlPreset(XdocumentReaderWriter reader)
        {
            XElement section = reader.GetElementByPath(SeamGuideSection) ?? reader.AddElement(SeamGuideSection);
            SetChild(reader, section, "AXIS_MM_PER_LASER_MM", Text(AxisMmPerLaserMm));
            SetChild(reader, section, "GUIDE_SIGN", GuideSign.ToString(CultureInfo.InvariantCulture));
            SetChild(reader, section, "LEAD_DISTANCE_MM", Text(LeadDistanceMm));
            SetChild(reader, section, "ROBOT_Y_GAIN", Text(RobotYGain));
            SetChild(reader, section, "Y_AXIS_MIN_MM", Text(YAxisMinMm));
            SetChild(reader, section, "Y_AXIS_MAX_MM", Text(YAxisMaxMm));
        }

        #endregion

        #region 私有函数（分区实现：INI）

        /// <summary>①系统设置区 INI 加载：SEAM_GUIDE 阈值段（含单调约束开关）+ 钳位保</summary>
        /// <remarks>护 + 跟踪模式强制 CSP 实时</remarks>
        private void LoadZoneIniSystem(INIFile ini)
        {
            // 缺值容差：读入钳位 1~50mm，越界记一条 Warning
            double noValueTolerance = ReadDoubleIni(ini, SeamGuideSection, IniNoValueTolerance, NoValueToleranceMm);
            if (noValueTolerance < 1.0 || noValueTolerance > 50.0)
            {
                GlobalCommData.ShowLog("焊参调整", string.Format(CultureInfo.InvariantCulture,
                    "NO_VALUE_TOLERANCE_MM {0:F2} 越界 已钳位 1~50mm", noValueTolerance), MessageLevel.Warning);
                noValueTolerance = noValueTolerance < 1.0 ? 1.0 : 50.0;
            }
            NoValueToleranceMm = noValueTolerance;
            OutlierLimitMm = ReadDoubleIni(ini, SeamGuideSection, IniOutlierLimit, OutlierLimitMm);
            MonotonicWindowMm = ReadDoubleIni(ini, SeamGuideSection, IniMonotonicWindow, MonotonicWindowMm);
            MonotonicDeadbandMm = ReadDoubleIni(ini, SeamGuideSection, IniMonotonicDeadband, MonotonicDeadbandMm);
            MonotonicReleaseMm = ReadDoubleIni(ini, SeamGuideSection, IniMonotonicRelease, MonotonicReleaseMm);
            ConsecutiveRejectFrames = ReadIntIni(ini, SeamGuideSection, IniConsecutiveReject, ConsecutiveRejectFrames);
            OutlierSigmaK = ReadDoubleIni(ini, SeamGuideSection, IniOutlierSigmaK, OutlierSigmaK);
            OutlierSlopeLimitMmPerMm = ReadDoubleIni(ini, SeamGuideSection, IniOutlierSlopeLimit, OutlierSlopeLimitMmPerMm);
            NearbyWindowMm = ReadDoubleIni(ini, SeamGuideSection, IniNearbyWindow, NearbyWindowMm);
            MaxStepChangeMm = ReadDoubleIni(ini, SeamGuideSection, IniMaxStepChange, MaxStepChangeMm);
            MaxTargetStepMm = ReadDoubleIni(ini, SeamGuideSection, IniMaxTargetStep, MaxTargetStepMm);
            EnableMonotonicConstraint = ini.ReadBool(SeamGuideSection, IniEnableMonotonic, EnableMonotonicConstraint);
            OutputDeadbandMm = ReadDoubleIni(ini, SeamGuideSection, IniOutputDeadband, OutputDeadbandMm);
            MaxVelMmPerS = ReadDoubleIni(ini, SeamGuideSection, IniMaxVel, MaxVelMmPerS);
            MaxAccMmPerS2 = ReadDoubleIni(ini, SeamGuideSection, IniMaxAcc, MaxAccMmPerS2);
            FeedforwardSafetyFactor = ReadDoubleIni(ini, SeamGuideSection, IniFeedforwardSafetyFactor, FeedforwardSafetyFactor);
            CspMinSegMm = ReadDoubleIni(ini, SeamGuideSection, IniCspMinSeg, CspMinSegMm);
            ReverseAlarmMm = ReadDoubleIni(ini, SeamGuideSection, IniReverseAlarm, ReverseAlarmMm);
            ReverseDeadbandMm = ReadDoubleIni(ini, SeamGuideSection, IniReverseDeadband, ReverseDeadbandMm);

            if (MonotonicReleaseMm < 0.0) MonotonicReleaseMm = 0.0;
            if (ConsecutiveRejectFrames < 1) ConsecutiveRejectFrames = 1;
            if (OutlierSigmaK <= 0.0) OutlierSigmaK = 3.0;
            if (OutlierSlopeLimitMmPerMm <= 0.0) OutlierSlopeLimitMmPerMm = 0.15;
            if (NearbyWindowMm <= 0.0) NearbyWindowMm = 20.0;
            if (MaxVelMmPerS <= 0.0) MaxVelMmPerS = 50.0;
            if (MaxAccMmPerS2 <= 0.0) MaxAccMmPerS2 = 500.0;
            if (FeedforwardSafetyFactor < 1.0) FeedforwardSafetyFactor = 1.0;
            else if (FeedforwardSafetyFactor > 10.0) FeedforwardSafetyFactor = 10.0;
            // PVT 未验证，存量配置一律强制 CSP 实时。
            TrackingMode = SeamTrackingMode.CspRealtime;
            if (MaxTargetStepMm <= 0.0) MaxTargetStepMm = 5.0;
            // 段距下限 = 单脉冲当量（0.001mm）。
            // 低于 1 脉冲会写入零位移段塞满缓冲。
            // ⚠️ 勿与每拍步长同量级，否则轴停走。
            if (CspMinSegMm < 0.001) CspMinSegMm = 0.001;
            // 输出死区上界 0.02mm，须在跟随器时间常数内铺完。
            // 间隔过大则退化为阶跃+平台，平台超阈值即停走。
            // ⚠️ INI 存旧值，须在加载期设上界。
            // 仅改默认值不会生效。
            if (OutputDeadbandMm > 0.02) OutputDeadbandMm = 0.02;
            if (ReverseAlarmMm <= 0.0) ReverseAlarmMm = 1.0;
            if (ReverseDeadbandMm < 0.0) ReverseDeadbandMm = 0.0;
        }

        /// <summary>①系统设置区 INI 保存：SEAM_GUIDE 阈值段（含单调约束开关）</summary>
        private void SaveZoneIniSystem(INIFile ini)
        {
            ini.WriteString(SeamGuideSection, IniNoValueTolerance, Text(NoValueToleranceMm));
            ini.WriteString(SeamGuideSection, IniOutlierLimit, Text(OutlierLimitMm));
            ini.WriteString(SeamGuideSection, IniMonotonicWindow, Text(MonotonicWindowMm));
            ini.WriteString(SeamGuideSection, IniMonotonicDeadband, Text(MonotonicDeadbandMm));
            ini.WriteString(SeamGuideSection, IniMonotonicRelease, Text(MonotonicReleaseMm));
            ini.WriteInt(SeamGuideSection, IniConsecutiveReject, ConsecutiveRejectFrames);
            ini.WriteString(SeamGuideSection, IniOutlierSigmaK, Text(OutlierSigmaK));
            ini.WriteString(SeamGuideSection, IniOutlierSlopeLimit, Text(OutlierSlopeLimitMmPerMm));
            ini.WriteString(SeamGuideSection, IniNearbyWindow, Text(NearbyWindowMm));
            ini.WriteString(SeamGuideSection, IniMaxStepChange, Text(MaxStepChangeMm));
            ini.WriteString(SeamGuideSection, IniMaxTargetStep, Text(MaxTargetStepMm));
            ini.WriteBool(SeamGuideSection, IniEnableMonotonic, EnableMonotonicConstraint);
            ini.WriteString(SeamGuideSection, IniOutputDeadband, Text(OutputDeadbandMm));
            ini.WriteString(SeamGuideSection, IniMaxVel, Text(MaxVelMmPerS));
            ini.WriteString(SeamGuideSection, IniFeedforwardSafetyFactor, Text(FeedforwardSafetyFactor));
            ini.WriteString(SeamGuideSection, IniMaxAcc, Text(MaxAccMmPerS2));
            ini.WriteString(SeamGuideSection, IniCspMinSeg, Text(CspMinSegMm));
            ini.WriteString(SeamGuideSection, IniReverseAlarm, Text(ReverseAlarmMm));
            ini.WriteString(SeamGuideSection, IniReverseDeadband, Text(ReverseDeadbandMm));
        }

        /// <summary>②工艺快捷区 INI 加载：SEAM_GUIDE 段 ENABLE_GUIDE 键 + PROCESS_CONTROL 段启用组/指令组</summary>
        private void LoadZoneIniQuick(INIFile ini)
        {
            EnableGuide = ini.ReadBool(SeamGuideSection, IniEnable, EnableGuide);

            EnableSeamTrace = ini.ReadBool(ProcessControlSection, IniEnableSeamTrace, EnableSeamTrace);
            EnableWireAlign = ini.ReadBool(ProcessControlSection, IniEnableWireAlign, EnableWireAlign);
            EnableTempCtrl = ini.ReadBool(ProcessControlSection, IniEnableTempCtrl, EnableTempCtrl);
            EnableGalvoWeave = ini.ReadBool(ProcessControlSection, IniEnableGalvoWeave, EnableGalvoWeave);
            SpotCalibPosMm = ReadDoubleIni(ini, ProcessControlSection, IniSpotCalibPos, SpotCalibPosMm);
            WireLockPosMm = ReadDoubleIni(ini, ProcessControlSection, IniWireLockPos, WireLockPosMm);
            LockYCalib = ini.ReadBool(ProcessControlSection, IniLockYCalib, LockYCalib);
            LockSpotCalib = ini.ReadBool(ProcessControlSection, IniLockSpotCalib, LockSpotCalib);
            LockWirePos = ini.ReadBool(ProcessControlSection, IniLockWirePos, LockWirePos);
        }

        /// <summary>②工艺快捷区 INI 保存：SEAM_GUIDE 段 ENABLE_GUIDE 键 + PROCESS_CONTROL 段启用组/指令组</summary>
        private void SaveZoneIniQuick(INIFile ini)
        {
            ini.WriteBool(SeamGuideSection, IniEnable, EnableGuide);

            ini.WriteBool(ProcessControlSection, IniEnableSeamTrace, EnableSeamTrace);
            ini.WriteBool(ProcessControlSection, IniEnableWireAlign, EnableWireAlign);
            ini.WriteBool(ProcessControlSection, IniEnableTempCtrl, EnableTempCtrl);
            ini.WriteBool(ProcessControlSection, IniEnableGalvoWeave, EnableGalvoWeave);
            ini.WriteString(ProcessControlSection, IniSpotCalibPos, Text(SpotCalibPosMm));
            ini.WriteString(ProcessControlSection, IniWireLockPos, Text(WireLockPosMm));
            ini.WriteBool(ProcessControlSection, IniLockYCalib, LockYCalib);
            ini.WriteBool(ProcessControlSection, IniLockSpotCalib, LockSpotCalib);
            ini.WriteBool(ProcessControlSection, IniLockWirePos, LockWirePos);
        }

        /// <summary>③自适应区 INI 加载：WELD_PARAM_ADJUST 段（焊参调整 + 宽度来源三项）+ 钳位保</summary>
        /// <remarks>SeamWidthSource 为派生只读（由 ②区 EnableGalvoWeave 决定），不再从 INI 读取；
        /// 存量 INI 中残留的 SEAM_WIDTH_SOURCE 键停读写，无害。 护</remarks>
        private void LoadZoneIniAdapt(INIFile ini)
        {
            MaxConsecutiveFailures = ReadIntIni(ini, AdjustmentSection, IniMaxConsecutiveFailures, MaxConsecutiveFailures);
            SegmentDistanceMm = ReadDoubleIni(ini, AdjustmentSection, IniSegmentDistance, SegmentDistanceMm);
            // 旧键只读忽略 留一条 Info
            bool legacyIsCount = ini.ReadBool(AdjustmentSection, IniIsCountType, true);
            int legacyCountNum = ReadIntIni(ini, AdjustmentSection, IniCountNum, 5);
            GlobalCommData.ShowLog("焊参调整", string.Format(
                "分段已改为按距离 每段 {0:F1}mm 旧键 IS_COUNT_TYPE={1} COUNT_NUM={2} 已忽略",
                SegmentDistanceMm, legacyIsCount, legacyCountNum), MessageLevel.Info);
            ManualSeamWidthMm = ReadDoubleIni(ini, AdjustmentSection, IniManualSeamWidth, ManualSeamWidthMm);
            SpotWidthGain = ReadDoubleIni(ini, AdjustmentSection, IniSpotWidthGain, SpotWidthGain);
            ParamOutputHz = ReadIntIni(ini, AdjustmentSection, IniParamOutputHz, ParamOutputHz);

            if (MaxConsecutiveFailures < 1) MaxConsecutiveFailures = 1;
            if (SegmentDistanceMm < 1.0 || SegmentDistanceMm > 50.0)
            {
                double clamped = SegmentDistanceMm < 1.0 ? 1.0 : 50.0;
                GlobalCommData.ShowLog("焊参调整", string.Format(
                    "每段距离 {0:F1}mm 越界 已钳位到 {1:F1}mm", SegmentDistanceMm, clamped), MessageLevel.Warning);
                SegmentDistanceMm = clamped;
            }
            if (ManualSeamWidthMm <= 0.0) ManualSeamWidthMm = 1.2;
            if (SpotWidthGain <= 0.0) SpotWidthGain = 1.0;
            if (ParamOutputHz < 1) ParamOutputHz = 60;
            else if (ParamOutputHz > 500) ParamOutputHz = 500;
        }

        /// <summary>③自适应区 INI 保存：WELD_PARAM_ADJUST 段（按距离分段 + 宽度来源三项；SEAM_WIDTH_SOURCE 停写）</summary>
        private void SaveZoneIniAdapt(INIFile ini)
        {
            ini.WriteInt(AdjustmentSection, IniMaxConsecutiveFailures, MaxConsecutiveFailures);
            ini.WriteString(AdjustmentSection, IniSegmentDistance, Text(SegmentDistanceMm));
            ini.WriteString(AdjustmentSection, IniManualSeamWidth, Text(ManualSeamWidthMm));
            ini.WriteString(AdjustmentSection, IniSpotWidthGain, Text(SpotWidthGain));
            ini.WriteInt(AdjustmentSection, IniParamOutputHz, ParamOutputHz);
        }

        /// <summary>④工艺设定值区 INI 加载：PROCESS_CONTROL 段 TA/LA/振镜预设 + 钳位保护</summary>
        private void LoadZoneIniPreset(INIFile ini)
        {
            LaSpotTargetMm = ReadDoubleIni(ini, ProcessControlSection, IniLaSpotTarget, LaSpotTargetMm);
            LaSpot1Mm = ReadDoubleIni(ini, ProcessControlSection, IniLaSpot1, LaSpot1Mm);
            LaSpot2Mm = ReadDoubleIni(ini, ProcessControlSection, IniLaSpot2, LaSpot2Mm);
            LaPose1Mm = ReadDoubleIni(ini, ProcessControlSection, IniLaPose1, LaPose1Mm);
            LaPose2Mm = ReadDoubleIni(ini, ProcessControlSection, IniLaPose2, LaPose2Mm);
            // TA 工艺调试参数
            TaForceN = ReadDoubleIni(ini, ProcessControlSection, IniTaForce, TaForceN);
            TaPose1Mm = ReadDoubleIni(ini, ProcessControlSection, IniTaPose1, TaPose1Mm);
            TaPose2Mm = ReadDoubleIni(ini, ProcessControlSection, IniTaPose2, TaPose2Mm);
            TaZeroPoseMm = ReadDoubleIni(ini, ProcessControlSection, IniTaZeroPose, TaZeroPoseMm);
            GalvoAutoCenterComp = ini.ReadBool(ProcessControlSection, IniGalvoAutoCenterComp, GalvoAutoCenterComp);
            GalvoCenterCompGain = ReadDoubleIni(ini, ProcessControlSection, IniGalvoCenterCompGain, GalvoCenterCompGain);
            GalvoPattern = ReadIntIni(ini, ProcessControlSection, IniGalvoPattern, GalvoPattern);

            if (GalvoPattern < 1 || GalvoPattern > 4) GalvoPattern = 3;
            if (LaSpotTargetMm <= 0.0) LaSpotTargetMm = 0.6;
            if (LaSpot1Mm <= 0.0) LaSpot1Mm = 0.6;
            if (LaSpot2Mm <= 0.0) LaSpot2Mm = 1.0;
        }

        /// <summary>④工艺设定值区 INI 保存：PROCESS_CONTROL 段 TA/LA/振镜预设</summary>
        private void SaveZoneIniPreset(INIFile ini)
        {
            ini.WriteString(ProcessControlSection, IniLaSpotTarget, Text(LaSpotTargetMm));
            ini.WriteString(ProcessControlSection, IniLaSpot1, Text(LaSpot1Mm));
            ini.WriteString(ProcessControlSection, IniLaSpot2, Text(LaSpot2Mm));
            ini.WriteString(ProcessControlSection, IniLaPose1, Text(LaPose1Mm));
            ini.WriteString(ProcessControlSection, IniLaPose2, Text(LaPose2Mm));
            // TA 工艺调试参数
            ini.WriteString(ProcessControlSection, IniTaForce, Text(TaForceN));
            ini.WriteString(ProcessControlSection, IniTaPose1, Text(TaPose1Mm));
            ini.WriteString(ProcessControlSection, IniTaPose2, Text(TaPose2Mm));
            ini.WriteString(ProcessControlSection, IniTaZeroPose, Text(TaZeroPoseMm));
            ini.WriteBool(ProcessControlSection, IniGalvoAutoCenterComp, GalvoAutoCenterComp);
            ini.WriteString(ProcessControlSection, IniGalvoCenterCompGain, Text(GalvoCenterCompGain));
            ini.WriteInt(ProcessControlSection, IniGalvoPattern, GalvoPattern);
        }

        #endregion
    }
}
