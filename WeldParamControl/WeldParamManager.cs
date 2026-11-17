using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.WeldParamControl.Algorithms;
using AdaWeldSystem.WeldParamControl.Curves;
using Newtonsoft.Json;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.SQLiteDBControl;
using AdaWeldSystem.SQLiteDBControl.Helpers;
using AdaWeldSystem.Comm.Communication;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>
    /// 焊接参数管理器（单例），模块对外唯一入口。
    /// </summary>
    /// <remarks>
    /// 集中持有配置与算法实现，管持久化并分派算法；
    /// 订阅通讯层位姿快照喂算法（经预测器外推）；只计算不落盘，流程编排归 WeldParamControlWorkflow。
    /// </remarks>
    public class WeldParamManager
    {
        #region 单例

        private static readonly Lazy<WeldParamManager> _lazyInstance =
            new Lazy<WeldParamManager>(() => new WeldParamManager());

        /// <summary>单例实例（线程安全）。</summary>
        public static WeldParamManager Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 常量

        private const string IniSection = "WeldData";
        private const string IniKeyCurrentId = "CurrentAutoParamId";
        private const string IniFileName = "WeldData.ini";

        /// <summary>日志标签（无符号纯文本）。</summary>
        private const string Tag = "WeldParamManager";

        /// <summary>算法配置文件名（落在 Algorithm_XML 目录）。</summary>
        private const string ModuleConfigFileName = "WeldParamControl";

        /// <summary>算法配置 XML 子目录（相对程序基目录）。</summary>
        private const string AlgorithmXmlRelativeDir = @"Config\WeldParamControl\Algorithm_XML";

        /// <summary>算法配置 INI 子目录（相对程序基目录）。</summary>
        private const string AlgorithmIniRelativeDir = @"Config\WeldParamControl\Algorithm_INI";

        /// <summary>算法流程阈值 INI 文件名（同名前缀区分流程类）。</summary>
        private const string AlgorithmIniFileName = "WeldParamControlAlgorithm.ini";

        /// <summary>位姿预测失效间隔（秒），超时清零重立锚。</summary>
        private const double PosePredictStaleGapSeconds = 1.0;

        /// <summary>最大外推时长（毫秒），超时冻结防漂移。</summary>
        private const double PosePredictMaxExtrapolateMs = 200.0;

        /// <summary>速度幅值上限（毫米/秒），钳位异常尖峰。</summary>
        private const double PosePredictMaxSpeedMmPerS = 100.0;

        /// <summary>RSI 标称周期（毫秒）：位姿停滞判据取 2 倍周期。</summary>
        private const double NoValueRsiCycleMs = 12.0;

        /// <summary>位姿停滞判据倍数（RSI 周期倍数）。</summary>
        private const double NoValuePoseStaleFactor = 2.0;

        /// <summary>缺值推定尺速度上限（毫米/秒），防尖峰放大。</summary>
        private const double NoValueEstimateVelCapMmPerS = 100.0;

        /// <summary>缺值观测日志限频（毫秒）。</summary>
        private const int NoValueLogIntervalMs = 2000;

        #endregion

        #region 私有变量

        /// <summary>查表算法实现（非单例，由本管理器持有）。</summary>
        private readonly WeldParamLookupAlgorithm _lookupAlgorithm;

        /// <summary>参数调整算法实现（非单例，由本管理器持有）。</summary>
        private readonly WeldParamAdjustmentAlgorithm _paramAdjustmentAlgorithm;

        /// <summary>焊缝几何跟踪算法（PVT 流式段模式）。</summary>
        private readonly SeamTrackingAlgorithm _seamTrackingAlgorithm;

        /// <summary>焊缝地图跟踪算法（CSP 实时目标模式）。</summary>
        private readonly SeamMapTrackingAlgorithm _seamMapTrackingAlgorithm;

        /// <summary>机器人 X 坐标读写锁。</summary>
        private readonly object _robotXLock = new object();

        /// <summary>位姿预测器激活态：首个喂入点置位。</summary>
        private bool _posePredictActive;

        /// <summary>位姿预测锚点是否已建立。</summary>
        private bool _posePredictHasAnchor;

        /// <summary>位姿预测锚点 X（最近一次上报值，毫米）。</summary>
        private double _posePredictAnchorX;

        /// <summary>位姿预测锚点 Y（最近一次上报值，毫米）。</summary>
        private double _posePredictAnchorY;

        /// <summary>位姿预测锚点时刻（Stopwatch 时间戳）。</summary>
        private long _posePredictAnchorTime;

        /// <summary>位姿预测 X 向速度（毫米/秒，相邻上报差分，带符号）。</summary>
        private double _posePredictVelX;

        /// <summary>位姿预测 Y 向速度（毫米/秒，相邻上报差分，带符号）。</summary>
        private double _posePredictVelY;

        /// <summary>通讯管理器（机器人位姿来源，装配时缓存）。</summary>
        private CommunicationManager _communication;

        /// <summary>INI 文件读写器。</summary>
        private readonly INIFile _iniFile;

        /// <summary>算法配置 XML 读写器（本管理器独占）。</summary>
        private readonly XdocumentReaderWriter _configFile;

        /// <summary>算法流程阈值 INI 读写器（本管理器独占）。</summary>
        private readonly INIFile _algorithmIniFile;

        /// <summary>算法配置实例（两节共用，仅供只读消费）。</summary>
        private WeldAlgorithmsConfig _algorithmsConfig;

        /// <summary>当前自动调整参数标识。</summary>
        private string _currentAutoParamId;

        /// <summary>TA/LA 标定曲线（采样点表；本管理器独占，外部只读消费）。</summary>
        private readonly TaLaCurveConfig _taLaCurve = new TaLaCurveConfig();

        /// <summary>缺值累计距离读写锁（激光回调与位姿回调双写）。</summary>
        private readonly object _noValueLock = new object();

        /// <summary>缺值累计行进距离（毫米）。</summary>
        private double _noValueDistanceMm;

        /// <summary>缺值距离最近一段是否取自推定尺。</summary>
        private bool _noValueEstimated;

        /// <summary>缺值累计上一帧位姿 X（毫米）。</summary>
        private double _noValueLastPoseX;

        /// <summary>缺值累计上一帧位姿时刻（Stopwatch 时间戳）。</summary>
        private long _noValueLastPoseTime;

        /// <summary>缺值累计是否已建立位姿锚点。</summary>
        private bool _noValueHasPoseAnchor;

        /// <summary>缺值观测日志上次输出时刻（Stopwatch 时间戳）。</summary>
        private long _noValueLastLogTime;

        /// <summary>缺值距离是否处于量测窗口（起焊沿到停焊沿）。</summary>
        private bool _noValueMeasuring;

        #endregion

        #region 公共变量

        /// <summary>机器人焊接数据。</summary>
        public RobotWeldData RobotWeldData { get; private set; }

        /// <summary>当前自动参数配置。</summary>
        public AutoParam CurrentAutoParam { get; private set; }

        /// <summary>自动调整参数值 Helper（按 identityInfo 管理分段数据表）。</summary>
        public AutoParamHelper AutoParamHelper { get; private set; }

        /// <summary>自动调整参数配置 Helper（管理 AutoParam 基础配置与 JSON 映射）。</summary>
        public ConfigHelper ConfigHelper { get; private set; }

        /// <summary>TA/LA 标定曲线（曲线A 光斑↔LA 位置 / 曲线B LA 偏移↔TA 位置）。</summary>
        public TaLaCurveConfig TaLaCurve { get { return _taLaCurve; } }

        /// <summary>曲线A 锚点光斑（毫米），随标定数据变化。</summary>
        /// <remarks>外部唯一读取口，曲线实例归本管理器独占持有。
        /// 外部不得直读 <see cref="TaLaCurveConfig"/> 的锚点常量。
        /// 配置访问唯一入口。</remarks>
        public double AnchorSpotMm { get { return _taLaCurve.AnchorSpotMm; } }

        /// <summary>当前自动调整参数标识。</summary>
        public string CurrentAutoParamId
        {
            get { return _currentAutoParamId; }
            private set
            {
                _currentAutoParamId = value;
                SaveCurrentIdToIni();
            }
        }

        /// <summary>每段距离 mm（透传运行配置）。</summary>
        public double SegmentDistanceMm
        {
            get { return _paramAdjustmentAlgorithm.Config.SegmentDistanceMm; }
            set { _paramAdjustmentAlgorithm.Config.SegmentDistanceMm = value; }
        }

        /// <summary>工艺输出·手动固定缝宽 mm（透传参数调整算法；配置键兼容）。</summary>
        public double ManualSeamWidthMm
        {
            get { return _paramAdjustmentAlgorithm.ManualSeamWidthMm; }
            set { _paramAdjustmentAlgorithm.ManualSeamWidthMm = value; }
        }

        /// <summary>工艺输出·光斑宽度系数（透传参数调整算法；配置键兼容）。</summary>
        public double SpotWidthGain
        {
            get { return _paramAdjustmentAlgorithm.SpotWidthGain; }
            set { _paramAdjustmentAlgorithm.SpotWidthGain = value; }
        }

        /// <summary>工艺输出·参数输出频率 Hz（透传参数调整算法；配置键兼容）。</summary>
        public int ParamOutputHz
        {
            get { return _paramAdjustmentAlgorithm.ParamOutputHz; }
            set { _paramAdjustmentAlgorithm.ParamOutputHz = value; }
        }

        /// <summary>工艺输出·前馈安全系数（透传参数调整算法；配置键兼容）。</summary>
        public double FeedforwardSafetyFactor
        {
            get { return _paramAdjustmentAlgorithm.FeedforwardSafetyFactor; }
            set { _paramAdjustmentAlgorithm.FeedforwardSafetyFactor = value; }
        }

        /// <summary>工艺输出·单调方向锁开关（透传参数调整算法；唯一 UI 入口在工艺设置页自适应区）。</summary>
        public bool EnableMonotonicConstraint
        {
            get { return _paramAdjustmentAlgorithm.EnableMonotonicConstraint; }
            set { _paramAdjustmentAlgorithm.EnableMonotonicConstraint = value; }
        }

        /// <summary>当前机器人 X 坐标（真实坐标，来自 RobotWeldData.PosX）。</summary>
        /// <remarks>位姿预测器激活时返回外推值，否则返回原值。</remarks>
        public double CurrentRobotX
        {
            get
            {
                lock (_robotXLock)
                {
                    if (_posePredictActive)
                    {
                        return _posePredictAnchorX + _posePredictVelX * PosePredictElapsedSeconds();
                    }
                    return RobotWeldData.PosX;
                }
            }
            set
            {
                lock (_robotXLock)
                {
                    RobotWeldData.PosX = value;
                }
            }
        }

        /// <summary>当前机器人 Y 坐标（真实坐标，来自 RobotWeldData.PosY；模拟态由模拟时钟喂入）。</summary>
        /// <remarks>位姿预测器激活时返回外推值，否则返回原值。</remarks>
        public double CurrentRobotY
        {
            get
            {
                lock (_robotXLock)
                {
                    if (_posePredictActive)
                    {
                        return _posePredictAnchorY + _posePredictVelY * PosePredictElapsedSeconds();
                    }
                    return RobotWeldData.PosY;
                }
            }
            set
            {
                lock (_robotXLock)
                {
                    RobotWeldData.PosY = value;
                }
            }
        }

        /// <summary>最大连续失败次数（透传运行配置）。</summary>
        public int MaxConsecutiveFailures
        {
            get { return _paramAdjustmentAlgorithm.MaxConsecutiveFailures; }
            set { _paramAdjustmentAlgorithm.MaxConsecutiveFailures = value; }
        }

        /// <summary>连续失败次数。</summary>
        public int ConsecutiveFailureCount { get { return _paramAdjustmentAlgorithm.ConsecutiveFailureCount; } }

        /// <summary>算法执行总次数。</summary>
        public int TotalExecuteCount { get { return _paramAdjustmentAlgorithm.TotalExecuteCount; } }

        /// <summary>送丝速度输出（最近一次调整结果）。</summary>
        public double FeedSpeedOutput { get { return _paramAdjustmentAlgorithm.FeedSpeedOutput; } }

        /// <summary>焊接速度输出（最近一次调整结果）。</summary>
        public double RobotSpeedOutput { get { return _paramAdjustmentAlgorithm.RobotSpeedOutput; } }

        /// <summary>激光功率输出（最近一次调整结果）。</summary>
        public double LaserPowerOutput { get { return _paramAdjustmentAlgorithm.LaserPowerOutput; } }

        /// <summary>光斑宽度输出（最近一次调整结果；= 采用焊缝宽度 × 系数 W）。</summary>
        public double SpotWidthOutput { get { return _paramAdjustmentAlgorithm.SpotWidthOutput; } }

        /// <summary>本轮采用的焊缝宽度。</summary>
        public double CurrentSeamWidth { get { return _paramAdjustmentAlgorithm.CurrentSeamWidth; } }

        /// <summary>当前机器人 Z 坐标（来自 RobotWeldData.PosZ）</summary>
        public double CurrentRobotZ { get { return RobotWeldData.PosZ; } }

        /// <summary>当前机器人运动速度（来自 RobotWeldData.Speed）</summary>
        public double CurrentRobotSpeed { get { return RobotWeldData.Speed; } }

        /// <summary>当前缺值累计行进距离（毫米，只读观测值）。</summary>
        /// <remarks>跟踪未激活不累计；有效激光帧到达清零。</remarks>
        public double NoValueDistanceMm
        {
            get { lock (_noValueLock) { return _noValueDistanceMm; } }
        }

        /// <summary>缺值累计距离来源（实测 / 推定）。</summary>
        /// <remarks>日志标注尺源用；未累计时按实测。</remarks>
        public string NoValueSource
        {
            get { lock (_noValueLock) { return _noValueEstimated ? "推定" : "实测"; } }
        }

        /// <summary>最近一帧机器人位姿距今时长（毫秒，只读观测值）。</summary>
        /// <remarks>未收到位姿时返回 <see cref="double.MaxValue"/>。</remarks>
        public double PoseAgeMs
        {
            get
            {
                lock (_robotXLock)
                {
                    if (!_posePredictHasAnchor) return double.MaxValue;
                    return (Stopwatch.GetTimestamp() - _posePredictAnchorTime) * 1000.0 / Stopwatch.Frequency;
                }
            }
        }

        /// <summary>最近一帧线激光焊缝特征点横向坐标（毫米）；无有效结果时为 0</summary>
        public double LastSeamPointY { get { try { return LineLaserManager.Instance.LastResult.FeatureY; } catch { return 0; } } }

        /// <summary>最近一帧线激光焊缝特征点高度坐标（毫米）；无有效结果时为 0</summary>
        public double LastSeamPointZ { get { try { return LineLaserManager.Instance.LastResult.FeatureZ; } catch { return 0; } } }

        /// <summary>当前线激光分析 Job 编号（会话基线）。</summary>
        public int CurrentLaserJobId { get { try { return LineLaserManager.Instance.FetchCurrentJobId(); } catch { return 0; } } }

        /// <summary>焊缝目标宽度列表（参数查找表的键）。</summary>
        public List<double> SeamWidthList
        {
            get { return _lookupAlgorithm.SeamWidthList; }
            set { _lookupAlgorithm.SeamWidthList = value; }
        }

        /// <summary>激光功率查找表（焊缝宽度 -> 激光功率）。</summary>
        public Dictionary<double, double> LaserPowerDic
        {
            get { return _lookupAlgorithm.LaserPowerDic; }
            set { _lookupAlgorithm.LaserPowerDic = value; }
        }

        /// <summary>送丝速度查找表（焊缝宽度 -> 送丝速度）。</summary>
        public Dictionary<double, double> FeedSpeedDic
        {
            get { return _lookupAlgorithm.FeedSpeedDic; }
            set { _lookupAlgorithm.FeedSpeedDic = value; }
        }

        /// <summary>焊接速度查找表（焊缝宽度 -> 焊接速度）。</summary>
        public Dictionary<double, double> RobotSpeedDic
        {
            get { return _lookupAlgorithm.RobotSpeedDic; }
            set { _lookupAlgorithm.RobotSpeedDic = value; }
        }

        /// <summary>光斑宽度查找表（焊缝宽度 -> 光斑宽度）。</summary>
        public Dictionary<double, double> SpotWidthDic
        {
            get { return _lookupAlgorithm.SpotWidthDic; }
            set { _lookupAlgorithm.SpotWidthDic = value; }
        }

        /// <summary>摆幅宽度查找表（焊缝宽度 -> 摆幅宽度；公式初值 + 手动修正，输出用查表值）。</summary>
        public Dictionary<double, double> SwingWidthDic
        {
            get { return _lookupAlgorithm.SwingWidthDic; }
            set { _lookupAlgorithm.SwingWidthDic = value; }
        }

        /// <summary>摆幅频率查找表（焊缝宽度 -> 摆幅频率 Hz；公式初值 + 手动核对，输出用查表值）。</summary>
        public Dictionary<double, int> SwingFrequencyDic
        {
            get { return _lookupAlgorithm.SwingFrequencyDic; }
            set { _lookupAlgorithm.SwingFrequencyDic = value; }
        }

        /// <summary>轮廓数据（基于 RobotX 的轮廓坐标）。</summary>
        public ChartVar ContourChart { get { return _paramAdjustmentAlgorithm.ContourChart; } }

        /// <summary>激光功率折线数据（基于 RobotX）。</summary>
        public ChartVar LaserPowerChart { get { return _paramAdjustmentAlgorithm.LaserPowerChart; } }

        /// <summary>送丝速度折线数据（基于 RobotX）。</summary>
        public ChartVar FeedSpeedChart { get { return _paramAdjustmentAlgorithm.FeedSpeedChart; } }

        /// <summary>焊缝宽度折线数据（基于 RobotX）。</summary>
        public ChartVar SeamWidthChart { get { return _paramAdjustmentAlgorithm.SeamWidthChart; } }

        /// <summary>机器人移动速度折线数据（基于 RobotX）。</summary>
        public ChartVar RobotSpeedChart { get { return _paramAdjustmentAlgorithm.RobotSpeedChart; } }

        /// <summary>输出缓存版本号（界面轮询判重用）。</summary>
        public long AdjustmentOutputVersion { get { return _paramAdjustmentAlgorithm.OutputVersion; } }

        /// <summary>焊接算法配置（运行期只读引用；线激光引导 + 参数调整两节合一，本管理器独占）。</summary>
        public WeldAlgorithmsConfig AlgorithmsConfig { get { return _algorithmsConfig; } }

        /// <summary>焊缝跟踪模式（读自算法配置）。</summary>
        public SeamTrackingMode TrackingMode
        {
            get
            {
                WeldAlgorithmsConfig config = _algorithmsConfig;
                return config != null ? config.TrackingMode : SeamTrackingMode.CspRealtime;
            }
        }

        /// <summary>是否处于 CSP 实时目标模式。</summary>
        public bool IsCspTrackingMode { get { return TrackingMode == SeamTrackingMode.CspRealtime; } }

        /// <summary>机器人位姿是否已上行（RSI 坐标包已到达）。</summary>
        /// <remarks>位姿数据归通讯层，本管理器只读快照。</remarks>
        public bool HasRobotPose
        {
            get
            {
                CommunicationManager communication = _communication;
                return communication != null && communication.HasRobotPose;
            }
        }

        /// <summary>跟踪报警请求事件（运控工作流停机）。</summary>
        public event EventHandler<TrackingAlarmEventArgs> SeamTrackingAlarmRequested
        {
            add { _seamMapTrackingAlgorithm.TrackingAlarmRequested += value; }
            remove { _seamMapTrackingAlgorithm.TrackingAlarmRequested -= value; }
        }

        /// <summary>焊缝地图跟踪 Y 轴目标位置（毫米，CSP 周期线程读取）。</summary>
        public double SeamTrackingCorrectionMm { get { return _seamMapTrackingAlgorithm.CorrectionMm; } }

        /// <summary>最近一帧特征偏差修正量（毫米，诊断用）。</summary>
        public double SeamTrackingLastCorrectionMm { get { return _seamMapTrackingAlgorithm.LastCorrectionMm; } }

        /// <summary>最近一帧线激光特征点 Y（毫米，线激光坐标，诊断用）。</summary>
        public double SeamTrackingLastFeatureY { get { return _seamMapTrackingAlgorithm.LastFeatureY; } }

        /// <summary>机器人 Y 漂移补偿项（诊断用）。</summary>
        public double SeamTrackingPathYOffset { get { return _seamMapTrackingAlgorithm.PathYOffset; } }

        /// <summary>最近入图查图键（毫米），诊断用</summary>
        public double SeamTrackingLastMapKeyX { get { return _seamMapTrackingAlgorithm.LastMapEntryKeyX; } }

        /// <summary>最近一次入图的绝对目标 Y（毫米，供 CSPinY 记录列）。</summary>
        public double SeamTrackingLastMapEntryY { get { return _seamMapTrackingAlgorithm.LastAcceptedTargetY; } }

        /// <summary>是否已有入图点（供 CSPinY 记录列）。</summary>
        public bool SeamTrackingHasMapEntry { get { return _seamMapTrackingAlgorithm.HasLastMapEntry; } }

        /// <summary>焊缝跟踪是否已触发折返报警。</summary>
        public bool SeamTrackingHasAlarm { get { return _seamMapTrackingAlgorithm.HasAlarm; } }

        /// <summary>焊缝跟踪折返报警原因。</summary>
        public string SeamTrackingAlarmReason { get { return _seamMapTrackingAlgorithm.AlarmReason; } }

        /// <summary>焊接工艺调整完成事件（透传参数调整算法）。</summary>
        public event EventHandler<WeldProcessCompletedEventArgs> WeldProcessCompleted
        {
            add { _paramAdjustmentAlgorithm.WeldProcessCompleted += value; }
            remove { _paramAdjustmentAlgorithm.WeldProcessCompleted -= value; }
        }

        /// <summary>过程帧记录请求事件（异步落盘）。</summary>
        public event EventHandler<WeldFrameRecordEventArgs> FrameRecordRequested
        {
            add { _paramAdjustmentAlgorithm.FrameRecordRequested += value; }
            remove { _paramAdjustmentAlgorithm.FrameRecordRequested -= value; }
        }

        /// <summary>调整动作记录请求事件（异步落盘）。</summary>
        public event EventHandler<WeldAdjustmentRecordEventArgs> AdjustmentRecordRequested
        {
            add { _paramAdjustmentAlgorithm.AdjustmentRecordRequested += value; }
            remove { _paramAdjustmentAlgorithm.AdjustmentRecordRequested -= value; }
        }

        /// <summary>记录中断请求事件（记录模块处置）。</summary>
        public event EventHandler AbortRecordRequested
        {
            add { _paramAdjustmentAlgorithm.AbortRecordRequested += value; }
            remove { _paramAdjustmentAlgorithm.AbortRecordRequested -= value; }
        }

        /// <summary>取最近一帧机器人位姿（机器人基坐标系，毫米）。</summary>
        /// <param name="x">机器人 X 坐标（毫米）</param>
        /// <param name="y">机器人 Y 坐标（毫米）</param>
        /// <param name="z">机器人 Z 坐标（毫米）</param>
        /// <returns>位姿可用返回 true</returns>
        public bool TryGetRobotPose(out double x, out double y, out double z)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;

            CommunicationManager communication = _communication;
            if (communication == null) return false;

            if (!communication.TryGetRobotPose(out RobotPoseSnapshot pose)) return false;

            x = pose.X;
            y = pose.Y;
            z = pose.Z;
            return true;
        }

        #endregion

        #region 构造函数

        private WeldParamManager()
        {
            RobotWeldData = new RobotWeldData();
            _lookupAlgorithm = new WeldParamLookupAlgorithm();
            _paramAdjustmentAlgorithm = new WeldParamAdjustmentAlgorithm(_lookupAlgorithm, RobotWeldData);
            _seamTrackingAlgorithm = new SeamTrackingAlgorithm();
            _seamMapTrackingAlgorithm = new SeamMapTrackingAlgorithm();
            AttachRobotPoseChannel();

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "WeldParamControl", "WeldData_INI", IniFileName);
            _iniFile = new INIFile(iniPath);
            _configFile = new XdocumentReaderWriter(AlgorithmXmlRelativeDir, ModuleConfigFileName);
            string algorithmIniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AlgorithmIniRelativeDir, AlgorithmIniFileName);
            _algorithmIniFile = new INIFile(algorithmIniPath);

            AutoParamHelper = SQLiteDatabaseManager.Instance.AutoParamHelper;
            ConfigHelper = SQLiteDatabaseManager.Instance.ConfigHelper;
            // 曲线为采样点表，缺失时用内置默认组。
            _taLaCurve.LoadFromFile();

            _currentAutoParamId = _iniFile.ReadString(IniSection, IniKeyCurrentId, AutoParam.DefaultIdentity);
            if (string.IsNullOrWhiteSpace(_currentAutoParamId))
            {
                _currentAutoParamId = AutoParam.DefaultIdentity;
            }

            EnsureDefaultConfigExists();
            LoadCurrentConfig();
            LoadAlgorithmsConfig();
        }

        #endregion

        #region 私有函数

        /// <summary>确保默认配置在 ConfigHelper 和 AutoParamHelper 中都存在。</summary>
        private void EnsureDefaultConfigExists()
        {
            if (!ConfigHelper.IsConfigExist(AutoParam.DefaultIdentity))
            {
                AutoParam defaultParam = AutoParam.CreateDefault();
                string json = JsonConvert.SerializeObject(defaultParam);
                ConfigHelper.AddConfig(
                    defaultParam.identityInfo,
                    defaultParam.WeldType.ToString(),
                    string.Empty,   // WireType 无用，恒传空
                    string.Empty,   // PlateType 无用，恒传空
                    defaultParam.CreatTime.ToString("yyyyMMddHHmmss"),
                    json);
            }

            if (!AutoParamHelper.IsTableExist(AutoParam.DefaultIdentity))
            {
                AutoParamHelper.SelectTable(AutoParam.DefaultIdentity);
            }
        }

        /// <summary>将当前配置标识保存到 INI。</summary>
        private void SaveCurrentIdToIni()
        {
            _iniFile.WriteString(IniSection, IniKeyCurrentId, _currentAutoParamId ?? AutoParam.DefaultIdentity);
            _iniFile.SaveToFile();
        }

        /// <summary>加载当前配置标识对应的 AutoParam 并重建查找表。</summary>
        private void LoadCurrentConfig()
        {
            string json = ConfigHelper.GetConfigValue(_currentAutoParamId);
            CurrentAutoParam = JsonConvert.DeserializeObject<AutoParam>(json);

            if (CurrentAutoParam == null)
            {
                _currentAutoParamId = AutoParam.DefaultIdentity;
                SaveCurrentIdToIni();
                json = ConfigHelper.GetConfigValue(_currentAutoParamId);
                CurrentAutoParam = JsonConvert.DeserializeObject<AutoParam>(json);
            }

            if (CurrentAutoParam != null)
            {
                // 先基准兜底，再用 DB 分段表覆盖。
                _lookupAlgorithm.LoadConfig(CurrentAutoParam);
                LoadProfileIntoLookupTable(CurrentAutoParam.identityInfo);
                _paramAdjustmentAlgorithm.Reset();
            }
        }

        /// <summary>从分段参数构建持久化数据对象。</summary>
        /// <returns>分段参数数据对象</returns>
        private WeldAutoParamProfile BuildProfileFromLookupTable()
        {
            return new WeldAutoParamProfile
            {
                SeamWidthJson = JsonConvert.SerializeObject(SeamWidthList),
                FeedSpeedJson = JsonConvert.SerializeObject(FeedSpeedDic),
                LaserPowerJson = JsonConvert.SerializeObject(LaserPowerDic),
                RobotSpeedJson = JsonConvert.SerializeObject(RobotSpeedDic),
                SpotWidthJson = JsonConvert.SerializeObject(SpotWidthDic),
                SwingWidthJson = JsonConvert.SerializeObject(SwingWidthDic),
                SwingFrequencyJson = JsonConvert.SerializeObject(SwingFrequencyDic)
            };
        }

        /// <summary>装配机器人位姿通道：订阅通讯层机器人数据，位</summary>
        /// <remarks>收包监听与解析仍归通讯层（KukaRsiComm），本类只消费。 姿快照与算法喂入都在本管理器收口。</remarks>
        private void AttachRobotPoseChannel()
        {
            CommunicationManager communication = GlobalCommData.mCommunicationManager;
            if (communication == null)
            {
                GlobalCommData.ShowLog(Tag, "通讯管理器不可用 机器人位姿通道未装配", MessageLevel.Warning);
                return;
            }

            _communication = communication;
            communication.RobotDataReceived += OnRobotDataReceived;
        }

        /// <summary>机器人数据回调（RSI 接收线程）：刷新位姿快照、喂入位</summary>
        /// <remarks>只在 RSI 线程写快照并喂算法，不落盘。</remarks>
        /// <param name="sender">事件源</param>
        /// <param name="e">KUKA 数据接收事件参数</param>
        private void OnRobotDataReceived(object sender, KUKADataReceivedEventArgs e)
        {
            if (e == null || e.RobotData == null) return;

            KUKAPosition position = e.RobotData.RIst;

            // 机器人 X/Y 落地数据载体，供观测
            CurrentRobotX = position.X;
            CurrentRobotY = position.Y;

            UpdatePosePredictor(position.X, position.Y);
            AccumulateNoValueDistanceOnPose(position.X);

            _seamMapTrackingAlgorithm.OnRsiPose(position.X, position.Y, position.Z);
        }

        /// <summary>位姿预测器喂入（真实 RSI 接收线程 / 模拟节拍线程）：以</summary>
        /// <remarks>每个喂入点重设锚点，速度取相邻差分。
        /// 超过失效间隔则清零重立锚。
        /// 漂移由下一点重锚纠正。</remarks>
        /// <param name="x">最新喂入机器人 X（毫米）</param>
        /// <param name="y">最新喂入机器人 Y（毫米）</param>
        private void UpdatePosePredictor(double x, double y)
        {
            bool activated = false;
            lock (_robotXLock)
            {
                long now = Stopwatch.GetTimestamp();
                if (_posePredictHasAnchor)
                {
                    double dt = (now - _posePredictAnchorTime) / (double)Stopwatch.Frequency;
                    if (dt > PosePredictStaleGapSeconds)
                    {
                        _posePredictVelX = 0.0;
                        _posePredictVelY = 0.0;
                    }
                    else if (dt > 0.0)
                    {
                        _posePredictVelX = ClampPredictVelocity((x - _posePredictAnchorX) / dt);
                        _posePredictVelY = ClampPredictVelocity((y - _posePredictAnchorY) / dt);
                    }
                }
                _posePredictAnchorX = x;
                _posePredictAnchorY = y;
                _posePredictAnchorTime = now;
                _posePredictHasAnchor = true;
                if (!_posePredictActive)
                {
                    _posePredictActive = true;
                    activated = true;
                }
            }
            if (activated)
            {
                GlobalCommData.ShowLog(Tag, "位姿预测器激活 位姿喂入锚点已建立", MessageLevel.Info);
            }
        }

        /// <summary>位姿回调喂入缺值距离累计器（RSI / 模拟线程）。</summary>
        /// <remarks>只在起焊沿到停焊沿窗口累计。
        /// 位姿间隔超 2 周期按推定尺，其余按相邻 X 差。
        /// 有效激光帧由激光回调单独清零。</remarks>
        /// <param name="x">本帧机器人 X 坐标（毫米）</param>
        private void AccumulateNoValueDistanceOnPose(double x)
        {
            double distance;
            bool estimated;
            bool measuring;
            long now = Stopwatch.GetTimestamp();
            lock (_noValueLock)
            {
                if (!_noValueHasPoseAnchor)
                {
                    _noValueLastPoseX = x;
                    _noValueLastPoseTime = now;
                    _noValueHasPoseAnchor = true;
                    return;
                }

                measuring = _noValueMeasuring;
                double dt = (now - _noValueLastPoseTime) / (double)Stopwatch.Frequency;
                if (measuring)
                {
                    double staleSeconds = NoValueRsiCycleMs * NoValuePoseStaleFactor / 1000.0;
                    if (dt > staleSeconds)
                    {
                        // 位姿停滞：按推定尺补计，防 RSI 断流漏计
                        _noValueDistanceMm += EstimateNoValueTravelMm(dt);
                        _noValueEstimated = true;
                    }
                    else
                    {
                        _noValueDistanceMm += Math.Abs(x - _noValueLastPoseX);
                        _noValueEstimated = false;
                    }
                }
                distance = _noValueDistanceMm;
                estimated = _noValueEstimated;
                _noValueLastPoseX = x;
                _noValueLastPoseTime = now;
            }

            if (measuring) LogNoValueDistance(distance, estimated);
        }

        /// <summary>按推定尺估算行进距离（毫米）。</summary>
        /// <param name="seconds">停滞时长（秒）</param>
        /// <returns>估算距离（毫米）</returns>
        private double EstimateNoValueTravelMm(double seconds)
        {
            double velocity = Math.Abs(CurrentRobotSpeed);
            if (velocity > NoValueEstimateVelCapMmPerS) velocity = NoValueEstimateVelCapMmPerS;
            return velocity * seconds;
        }

        /// <summary>有效激光帧到达：缺值累计距离清零。</summary>
        private void ResetNoValueDistance()
        {
            lock (_noValueLock)
            {
                _noValueDistanceMm = 0.0;
                _noValueEstimated = false;
            }
        }

        /// <summary>开关缺值量测窗口（起焊沿开、停焊沿关）。</summary>
        /// <param name="on">是否处于量测窗口</param>
        private void SetNoValueMeasuring(bool on)
        {
            lock (_noValueLock)
            {
                _noValueMeasuring = on;
            }
        }

        /// <summary>缺值观测日志（限频 Debug，只观测不触发动作）。</summary>
        /// <param name="distanceMm">累计距离（毫米）</param>
        /// <param name="estimated">是否取自推定尺</param>
        private void LogNoValueDistance(double distanceMm, bool estimated)
        {
            long now = Stopwatch.GetTimestamp();
            lock (_noValueLock)
            {
                if (_noValueLastLogTime != 0)
                {
                    double sinceMs = (now - _noValueLastLogTime) * 1000.0 / Stopwatch.Frequency;
                    if (sinceMs < NoValueLogIntervalMs) return;
                }
                _noValueLastLogTime = now;
            }

            double threshold = _algorithmsConfig == null ? 0.0 : _algorithmsConfig.NoValueToleranceMm;
            GlobalCommData.ShowLog(Tag, string.Format(
                "缺值累计距离 {0:F2}mm 尺源 {1} 阈值 {2:F1}mm",
                distanceMm, estimated ? "推定" : "实测", threshold), MessageLevel.Debug);
        }

        /// <summary>位姿外推时长（秒）：自锚点起算，超出最大外推时长</summary>
        /// <remarks>即冻结（须在 _robotXLock 内调用）。</remarks>
        /// <returns>外推时长（秒，非负且不超过上限）</returns>
        private double PosePredictElapsedSeconds()
        {
            double ms = (Stopwatch.GetTimestamp() - _posePredictAnchorTime) * 1000.0 / Stopwatch.Frequency;
            if (ms < 0.0) ms = 0.0;
            if (ms > PosePredictMaxExtrapolateMs) ms = PosePredictMaxExtrapolateMs;
            return ms / 1000.0;
        }

        /// <summary>预测速度幅值钳位（毫米/秒，保号）。</summary>
        /// <param name="velocity">差分求得的轴向速度（毫米/秒）</param>
        /// <returns>幅值不超过上限的速度（毫米/秒）</returns>
        private static double ClampPredictVelocity(double velocity)
        {
            if (velocity > PosePredictMaxSpeedMmPerS) return PosePredictMaxSpeedMmPerS;
            if (velocity < -PosePredictMaxSpeedMmPerS) return -PosePredictMaxSpeedMmPerS;
            return velocity;
        }

        /// <summary>取（或创建）指定接头类型的默认基准工艺配置。</summary>
        /// <remarks>标识为「接头默认_」加两位编号（接头类型读 jointtypelist.json 共 20 类）。
        /// 新建时基准参数取 <see cref="AutoParam.CreateDefault"/>。
        /// 并写入 <see cref="AutoParam.JointType"/>。
        /// 参数为出厂占位，标定后经保存覆盖。</remarks>
        /// <param name="jointType">接头类型编号（英莱 20 类）</param>
        /// <returns>该接头的默认基准工艺配置</returns>
        public AutoParam GetOrCreateJointDefault(int jointType)
        {
            string id = "接头默认_" + jointType.ToString("00");
            if (!ConfigHelper.IsConfigExist(id))
            {
                AutoParam param = AutoParam.CreateDefault();
                param.identityInfo = id;
                param.JointType = jointType;
                string json = JsonConvert.SerializeObject(param);
                ConfigHelper.AddConfig(
                    id,
                    param.WeldType.ToString(),
                    string.Empty,
                    string.Empty,
                    DateTime.Now.ToString("yyyyMMddHHmmss"),
                    json);
            }
            string configText = ConfigHelper.GetConfigValue(id);
            AutoParam loaded = null;
            try
            {
                loaded = JsonConvert.DeserializeObject<AutoParam>(configText);
            }
            catch
            {
                // JSON 损坏时回落默认，不影响主流程
            }
            return loaded ?? AutoParam.CreateDefault();
        }

        /// <summary>模拟态喂入机器人位姿（等效 RSI 回包）：刷新位姿快照、</summary>
        /// <remarks>模拟线程按拍喂入（Y/Z 恒 0），单入口杜绝多源。
        /// 与真实同经位姿预测器外推。</remarks>
        /// <param name="x">机器人 X 坐标（毫米）</param>
        /// <param name="y">机器人 Y 坐标（毫米），模拟态恒 0</param>
        /// <param name="z">机器人 Z 坐标（毫米），模拟态恒 0</param>
        public void UpdateRobotPose(double x, double y, double z)
        {
            UpdatePosePredictor(x, y);
            AccumulateNoValueDistanceOnPose(x);
            RobotWeldData.PosX = x;
            RobotWeldData.PosY = y;
            RobotWeldData.PosZ = z;
            _seamMapTrackingAlgorithm.OnRsiPose(x, y, z);
        }

        #endregion

        #region 配置管理公共方法

        /// <summary>一次性迁移：TA 下压力从已弃用的轴配置 TorqueTarget 迁入工艺</summary>
        /// <param name="config">配置对象。</param>
        /// <remarks>
        /// 仅首次运行且轴配置力值非零时执行；
        /// 迁移后不落盘，由统一保存落盘。
        /// ⛔ 轴查询统一走 MontionManager 单例。
        ///  配置 TaForceN。</remarks>
        private void MigrateLegacyTorqueTarget(WeldAlgorithmsConfig config)
        {
            try
            {
                if (config == null) return;

                MontionManager manager = MontionManager.Instance;
                if (manager == null || manager.MoveData == null || manager.MoveData.MotionConfig == null) return;
                DeltaTaHomeConfig taHome = manager.TaHomeConfig;
                // 方向推断与补缺只用已加载配置。
                // 须在 IsInitialized 门禁前执行，否则无法补键。
                NormalizeTaForceDirection(config, manager, taHome);
                // 方向键与缺键合并为一次写盘。
                manager.EnsureMotionConfigKeys(true, taHome == null ? 1 : taHome.TorqueDirection);

                if (_algorithmIniFile.KeyExists(WeldAlgorithmsConfig.ProcessControlSection, "TA_FORCE_N")) return;

                // 旧 TorqueTarget 换算需要轴实例，硬件未就绪时不做
                if (!manager.IsInitialized) return;
                DeltaAxisConfig axis = manager.GetAxisConfigByKeys(MontionManager.TaAxisKeys);
                if (axis == null || axis.TorqueTarget == 0) return;
                // 旧 TorqueTarget 是 CST 千分比，TaForceN 是牛顿：按 100% 力量基准换算，⛔ 不再 1:1 直抄
                double force100 = taHome == null ? 0.0 : taHome.Force100PctN;
                if (!(force100 > 0.0))
                {
                    GlobalCommData.ShowLog("工艺配置",
                        "一次性迁移跳过：100% 力量基准 TA_FORCE_100PCT_N 未配置或 ≤0，旧 TorqueTarget 未换算",
                        MessageLevel.Warning);
                    return;
                }

                config.TaForceN = Math.Round(axis.TorqueTarget * force100 / 1000.0, 3);
                GlobalCommData.ShowLog("工艺配置",
                    string.Format("一次性迁移：TA 下压力 TorqueTarget {0}‰ × 100%基准 {1:F1}N -> TaForceN {2:F3}N",
                        axis.TorqueTarget, force100, config.TaForceN),
                    MessageLevel.Info);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("工艺配置",
                    "TorqueTarget 迁移异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>TA 工作零位坐标一次性迁移：ROOT/TA_HOME/TA_HOME_ZERO_POSE_MM → 工艺 INI 的 TA_ZERO_POSE_MM</summary>
        /// <param name="config">当前工艺配置（迁移目标）</param>
        /// <remarks>幂等：仅未写过时执行一次，写过以工艺页为准。
        /// ⛔ 须先于 <see cref="HealAlgorithmsConfigKeys"/> 调用。
        /// 补键会写默认值。之后无法判断未写过，旧值会丢。
        /// 迁移只改内存，随保存链路落盘。</remarks>
        private void MigrateLegacyTaZeroPoseMm(WeldAlgorithmsConfig config)
        {
            try
            {
                if (config == null) return;
                if (_algorithmIniFile == null) return;
                if (_algorithmIniFile.KeyExists(WeldAlgorithmsConfig.ProcessControlSection, "TA_ZERO_POSE_MM")) return;

                MontionManager manager = MontionManager.Instance;
                DeltaTaHomeConfig taHome = manager?.TaHomeConfig;
                if (taHome == null || taHome.WorkOriginMm == 0.0) return;   // 无旧值可迁，交给补键写默认 0

                config.TaZeroPoseMm = taHome.WorkOriginMm;
                GlobalCommData.ShowLog("工艺配置",
                    string.Format("一次性迁移：TA 工作零位坐标 TA_HOME_ZERO_POSE_MM {0:F3}mm -> 工艺设置页 TA_ZERO_POSE_MM（旧键保留不再读取）",
                        taHome.WorkOriginMm),
                    MessageLevel.Info);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("工艺配置",
                    "TA 工作零位坐标迁移异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>旧负力值迁移：负号代表反向出力，改存 TA_TORQUE_DIRECTION</summary>
        /// <param name="config">当前工艺配置（TaForceN 可能为负）</param>
        /// <param name="manager">运控管理器（用于落盘 TA_HOME）</param>
        /// <param name="taHome">TA 轴专有参数组</param>
        /// <remarks>方向键已存在则不覆盖，只取力值绝对值。提示双负号翻转风险。
        /// ⛔ 本方法只改内存，不落盘。
        /// 由调用方合并为一次写盘。</remarks>
        private void NormalizeTaForceDirection(WeldAlgorithmsConfig config, MontionManager manager, DeltaTaHomeConfig taHome)
        {
            if (config == null || taHome == null) return;
            if (config.TaForceN < 0.0)
            {
                double oldForce = config.TaForceN;
                config.TaForceN = Math.Abs(oldForce);
                if (taHome.TorqueDirectionKeyPresent)
                {
                    GlobalCommData.ShowLog("工艺配置", string.Format(
                        "旧力值 {0:F1}N 方向键已存在（{1}）：以键值为准不改方向，力值取绝对值 {2:F1}N；"
                        + "⚠ 若键为 -1，原反向出力在「大小×-1」下方向一致，但旧负号已消除，请核对现场出力方向",
                        oldForce, taHome.TorqueDirection, config.TaForceN), MessageLevel.Warning);
                    return;
                }
                taHome.TorqueDirection = -1;
                GlobalCommData.ShowLog("工艺配置", string.Format(
                    "旧力值 {0:F1}N 方向推断为 -（待写入 TA_TORQUE_DIRECTION=-1），力值取绝对值 {1:F1}N",
                    oldForce, config.TaForceN), MessageLevel.Info);
                return;
            }

            // 力值为非负且方向键缺省：按默认 +1 补齐；这是「补出来的 +1」唯一的合法产生点
            if (!taHome.TorqueDirectionKeyPresent)
            {
                taHome.TorqueDirection = 1;
                GlobalCommData.ShowLog("工艺配置", string.Format(
                    "旧力值 {0:F1}N 非负且方向键缺省：按默认 + 补齐（TA_TORQUE_DIRECTION=+1）", config.TaForceN),
                    MessageLevel.Info);
            }
        }

        /// <summary>补写焊接算法 XML / 工艺 INI 的缺失键</summary>
        /// <param name="config">已加载的配置对象（缺键回填默认值）</param>
        /// <remarks>期望键集由 <see cref="WeldAlgorithmsConfig.SaveConfig"/> 与 <see cref="WeldAlgorithmsConfig.SaveProcessIni"/> 自身写出。
        /// 模板镜像即键集，默认值不在此处另抄字面量。
        /// 整文件缺失时生成完整模板。</remarks>
        private void HealAlgorithmsConfigKeys(WeldAlgorithmsConfig config)
        {
            if (config == null) return;

            try
            {
                // XML：先写内存镜像，再对比真实文件
                XdocumentReaderWriter mirror = new XdocumentReaderWriter();
                mirror.NewXdocument("ROOT", "1.0", "UTF-8");
                config.SaveConfig(mirror);
                XDocument template = mirror.GetXDocument();
                string xmlFileName = ModuleConfigFileName + ".xml";
                string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AlgorithmXmlRelativeDir, xmlFileName);
                ConfigSelfHeal.EnsureXml(xmlPath, AlgorithmXmlRelativeDir + "\\" + xmlFileName, xmlFileName, template, true);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("焊接算法配置", "XML 缺键补写失败：" + ex.Message, MessageLevel.Warning);
            }

            string tempPath = null;
            try
            {
                // INI：借临时实例取键全集，再补写。
                tempPath = Path.Combine(Path.GetTempPath(), "adaweld_heal_" + Guid.NewGuid().ToString("N") + ".ini");
                INIFile templateIni = new INIFile(tempPath);
                config.SaveProcessIni(templateIni);

                List<ConfigSelfHeal.IniDefault> defaults = new List<ConfigSelfHeal.IniDefault>();
                List<string> sections = templateIni.GetAllSections();
                for (int i = 0; i < sections.Count; i++)
                {
                    Dictionary<string, string> keys = templateIni.GetSectionKeys(sections[i]);
                    foreach (KeyValuePair<string, string> pair in keys)
                    {
                        defaults.Add(ConfigSelfHeal.Key(sections[i], pair.Key, pair.Value));
                    }
                }

                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AlgorithmIniRelativeDir, AlgorithmIniFileName);
                ConfigSelfHeal.EnsureIni(iniPath, AlgorithmIniRelativeDir + "\\" + AlgorithmIniFileName,
                    AlgorithmIniFileName, defaults, true);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("焊接算法配置", "工艺 INI 缺键补写失败：" + ex.Message, MessageLevel.Warning);
            }
            finally
            {
                try
                {
                    if (tempPath != null && File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch (Exception)
                {
                    // 临时文件清理失败不影响主流程
                }
            }
        }

        /// <summary>切换到指定配置。</summary>
        /// <param name="configId">配置标识</param>
        /// <exception cref="ArgumentException">配置标识为空</exception>
        /// <exception cref="InvalidOperationException">配置不存在</exception>
        public void SwitchConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("配置标识不能为空", nameof(configId));
            }

            if (!ConfigHelper.IsConfigExist(configId))
            {
                throw new InvalidOperationException(string.Format("配置不存在 {0}", configId));
            }

            CurrentAutoParamId = configId;
            LoadCurrentConfig();
        }

        /// <summary>保存当前 AutoParam 到 ConfigHelper。</summary>
        /// <remarks>落盘前读旧值做差异审计。</remarks>
        public void SaveCurrentAutoParam()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            AutoParam previous = DeserializeOrNull<AutoParam>(
                ConfigHelper.GetConfigValue(CurrentAutoParam.identityInfo));

            string json = JsonConvert.SerializeObject(CurrentAutoParam);
            if (ConfigHelper.IsConfigExist(CurrentAutoParam.identityInfo))
            {
                ConfigHelper.UpdateConfig(
                    CurrentAutoParam.identityInfo,
                    CurrentAutoParam.WeldType.ToString(),
                    string.Empty,   // WireType 无用，恒传空
                    string.Empty,   // PlateType 无用，恒传空
                    json);
            }
            else
            {
                ConfigHelper.AddConfig(
                    CurrentAutoParam.identityInfo,
                    CurrentAutoParam.WeldType.ToString(),
                    string.Empty,   // WireType 无用，恒传空
                    string.Empty,   // PlateType 无用，恒传空
                    CurrentAutoParam.CreatTime.ToString("yyyyMMddHHmmss"),
                    json);
            }

            WeldParamChangeLogger.LogObjectDiff("焊参调整", previous, CurrentAutoParam);
        }

        /// <summary>保存当前分段参数值到数据库。</summary>
        /// <remarks>落盘前读旧分段表，按宽度档审计。</remarks>
        public void SaveCurrentProfile()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            WeldAutoParamProfile previous = AutoParamHelper.LoadProfile(CurrentAutoParam.identityInfo);
            WeldAutoParamProfile profile = BuildProfileFromLookupTable();
            if (AutoParamHelper.IsTableExist(CurrentAutoParam.identityInfo))
            {
                AutoParamHelper.UpdateProfile(CurrentAutoParam.identityInfo, profile);
            }
            else
            {
                AutoParamHelper.SaveProfile(CurrentAutoParam.identityInfo, profile);
            }

            WeldParamChangeLogger.LogProfileDiff("分段参数表", previous, profile);
        }

        /// <summary>添加新配置并切换为当前配置。</summary>
        /// <param name="configId">新配置标识</param>
        /// <returns>创建成功的 AutoParam</returns>
        public AutoParam AddConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("配置标识不能为空", nameof(configId));
            }

            if (ConfigHelper.IsConfigExist(configId))
            {
                throw new InvalidOperationException(string.Format("配置已存在 {0}", configId));
            }

            AutoParam newParam = new AutoParam(configId, DateTime.Now);
            string json = JsonConvert.SerializeObject(newParam);
            ConfigHelper.AddConfig(
                newParam.identityInfo,
                newParam.WeldType.ToString(),
                string.Empty,   // WireType 无用，恒传空
                string.Empty,   // PlateType 无用，恒传空
                newParam.CreatTime.ToString("yyyyMMddHHmmss"),
                json);
            AutoParamHelper.SelectTable(newParam.identityInfo);

            CurrentAutoParamId = configId;
            CurrentAutoParam = newParam;
            _lookupAlgorithm.LoadConfig(newParam);
            _paramAdjustmentAlgorithm.Reset();

            return newParam;
        }

        /// <summary>删除指定配置。</summary>
        /// <param name="configId">配置标识</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                return false;
            }

            if (configId == AutoParam.DefaultIdentity)
            {
                return false;
            }

            if (configId == _currentAutoParamId)
            {
                return false;
            }

            AutoParamHelper.DeleteTable(configId);
            return ConfigHelper.DeleteConfig(configId);
        }

        /// <summary>从 ConfigHelper 重新加载当前配置，并重新生成分段参数值。</summary>
        public void ReloadCurrentParamValues()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            string json = ConfigHelper.GetConfigValue(_currentAutoParamId);
            AutoParam refreshed = JsonConvert.DeserializeObject<AutoParam>(json);
            if (refreshed == null)
            {
                return;
            }

            CurrentAutoParam = refreshed;
            _lookupAlgorithm.SetSpotWidthGain(_algorithmsConfig != null ? _algorithmsConfig.SpotWidthGain : 1.0);
            _lookupAlgorithm.LoadConfig(CurrentAutoParam);
            AutoParamHelper.DeleteTable(CurrentAutoParam.identityInfo);
            SaveCurrentProfile();
            _paramAdjustmentAlgorithm.Reset();
        }

        /// <summary>反序列化分段参数（失败返回 null）。</summary>
        /// <param name="json">JSON 串</param>
        /// <returns>反序列化结果；失败返回 null</returns>
        private static T DeserializeOrNull<T>(string json) where T : class
        {
            if (!WeldAutoParamProfile.IsMeaningfulJson(json))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>从数据库加载分段参数并写入查找表。</summary>
        /// <remarks>空表不覆盖已有查找表。
        /// 否则查表失败，功率与送丝输出恒 0。</remarks>
        /// <param name="identityInfo">自动参数标识</param>
        public void LoadProfileIntoLookupTable(string identityInfo)
        {
            WeldAutoParamProfile profile = AutoParamHelper.LoadProfile(identityInfo);
            if (!profile.HasAnyValue)
            {
                GlobalCommData.ShowLog(Tag, string.Format(
                    "分段参数表无实质数据 配置 {0} 保持内存查找表不变", identityInfo), MessageLevel.Warning);
                return;
            }

            List<double> widths = DeserializeOrNull<List<double>>(profile.SeamWidthJson);
            if (widths != null && widths.Count > 0)
            {
                SeamWidthList = widths;
            }

            Dictionary<double, double> feedSpeeds = DeserializeOrNull<Dictionary<double, double>>(profile.FeedSpeedJson);
            if (feedSpeeds != null && feedSpeeds.Count > 0)
            {
                FeedSpeedDic = feedSpeeds;
            }

            Dictionary<double, double> laserPowers = DeserializeOrNull<Dictionary<double, double>>(profile.LaserPowerJson);
            if (laserPowers != null && laserPowers.Count > 0)
            {
                LaserPowerDic = laserPowers;
            }

            Dictionary<double, double> robotSpeeds = DeserializeOrNull<Dictionary<double, double>>(profile.RobotSpeedJson);
            if (robotSpeeds != null && robotSpeeds.Count > 0)
            {
                RobotSpeedDic = robotSpeeds;
            }

            Dictionary<double, double> spotWidths = DeserializeOrNull<Dictionary<double, double>>(profile.SpotWidthJson);
            if (spotWidths != null && spotWidths.Count > 0)
            {
                SpotWidthDic = spotWidths;
            }

            Dictionary<double, double> swingWidths = DeserializeOrNull<Dictionary<double, double>>(profile.SwingWidthJson);
            if (swingWidths != null && swingWidths.Count > 0)
            {
                SwingWidthDic = swingWidths;
            }

            Dictionary<double, int> swingFrequencies = DeserializeOrNull<Dictionary<double, int>>(profile.SwingFrequencyJson);
            if (swingFrequencies != null && swingFrequencies.Count > 0)
            {
                SwingFrequencyDic = swingFrequencies;
            }
        }

        /// <summary>从自有配置文件载入焊接算法配置（SEAM_GUIDE + WELD_PARAM_ADJUST 两节）并分发</summary>
        /// <remarks>配置实例由本管理器独占，外部只读，禁止回灌。</remarks>
        public void LoadAlgorithmsConfig()
        {
            // ⛔ 先刷磁盘再读，否则读到内存快照。
            bool xmlReloaded = _configFile.Reload();
            _algorithmIniFile.Reload();
            if (!xmlReloaded)
            {
                GlobalCommData.ShowLog("焊接算法配置", "XML 配置文件未能从磁盘重载，沿用内存快照", MessageLevel.Warning);
            }

            WeldAlgorithmsConfig config = new WeldAlgorithmsConfig();
            config.LoadConfig(_configFile);
            config.LoadProcessIni(_algorithmIniFile);
            _algorithmsConfig = config;
            _lookupAlgorithm.SetSpotWidthGain(config.SpotWidthGain);
            _paramAdjustmentAlgorithm.LoadConfig(config);
            GlobalCommData.ShowLog("焊接算法配置",
                string.Format("已从磁盘重载 手斑增益 {0} 焊宽 {1}mm 输出频率 {2}Hz 单调锁 {3}",
                    config.SpotWidthGain, config.ManualSeamWidthMm, config.ParamOutputHz,
                    config.EnableMonotonicConstraint ? "开" : "关"),
                MessageLevel.Info);
            _seamTrackingAlgorithm.LoadConfig(config);
            _seamMapTrackingAlgorithm.LoadConfig(config);

            // 内存无表才按基准重建，生产只读表。
            if (CurrentAutoParam != null)
            {
                LoadProfileIntoLookupTable(CurrentAutoParam.identityInfo);
                if (!_lookupAlgorithm.IsReady)
                {
                    _lookupAlgorithm.BuildLookupTable(CurrentAutoParam);
                }
            }

            // TA 下压力一次性迁移，仅首次运行生效
            MigrateLegacyTorqueTarget(_algorithmsConfig);
            // TA 工作零位坐标一次性迁移（TA_HOME_ZERO_POSE_MM → TA_ZERO_POSE_MM），⛔ 必须早于下面的补键（ADR-086 D8）
            MigrateLegacyTaZeroPoseMm(_algorithmsConfig);
            // 工艺 XML / INI 缺键补写（只增不改；无缺键时零写盘、零日志）
            HealAlgorithmsConfigKeys(_algorithmsConfig);
        }

        /// <summary>把当前焊接算法配置写回自有配置文件并落盘。</summary>
        /// <remarks>阈值改动后由界面或流程调用；写入后即时生效于两个算法。
        /// 落盘前读旧配置做差异审计。</remarks>
        public void SaveAlgorithmsConfig()
        {
            WeldAlgorithmsConfig config = _algorithmsConfig;
            if (config == null) return;

            WeldAlgorithmsConfig previous = new WeldAlgorithmsConfig();
            previous.LoadConfig(_configFile);
            previous.LoadProcessIni(_algorithmIniFile);

            config.SaveConfig(_configFile);
            config.SaveProcessIni(_algorithmIniFile);
            _configFile.SaveXdocument();

            WeldParamChangeLogger.LogObjectDiff("焊接算法配置", previous, config);
        }

        /// <summary>按 UI 分区从自有配置文件载入焊接算法配置（XML + INI 双后</summary>
        /// <remarks>
        /// 配置实例引用不变，无需重分发。
        /// 加载后同步查表算法的增益缓存。</remarks>
        /// <param name="zone">UI 分区（<see cref="WeldAlgorithmsConfig.AlgorithmsZone"/>）</param>
        public void LoadAlgorithmsZone(WeldAlgorithmsConfig.AlgorithmsZone zone)
        {
            WeldAlgorithmsConfig config = _algorithmsConfig;
            if (config == null) return;

            config.LoadZoneXml(_configFile, zone);
            config.LoadZoneIni(_algorithmIniFile, zone);

            if (zone == WeldAlgorithmsConfig.AlgorithmsZone.Adapt)
            {
                _lookupAlgorithm.SetSpotWidthGain(config.SpotWidthGain);
            }
        }

        /// <summary>按 UI 分区把当前焊接算法配置写回自有配置文件</summary>
        /// <remarks>
        /// 只写该分区键，未写键保留原值。
        /// 落盘前读旧配置做差异审计。</remarks>
        /// <param name="zone">UI 分区（<see cref="WeldAlgorithmsConfig.AlgorithmsZone"/>）</param>
        public void SaveAlgorithmsZone(WeldAlgorithmsConfig.AlgorithmsZone zone)
        {
            WeldAlgorithmsConfig config = _algorithmsConfig;
            if (config == null) return;

            WeldAlgorithmsConfig previous = new WeldAlgorithmsConfig();
            previous.LoadConfig(_configFile);
            previous.LoadProcessIni(_algorithmIniFile);

            config.SaveZoneXml(_configFile, zone);
            config.SaveZoneIni(_algorithmIniFile, zone);
            _configFile.SaveXdocument();
            _algorithmIniFile.SaveToFile();

            WeldParamChangeLogger.LogObjectDiff("焊接算法配置", previous, config);
        }

        #endregion

        #region 算法公共方法

        /// <summary>工艺参数 XL 缓存点数（观测用）。</summary>
        public int ParamCacheCount { get { return _paramAdjustmentAlgorithm.XlCacheCount; } }

        /// <summary>焊缝地图点数（界面观测）。</summary>
        public int SeamTrackingMapPointCount { get { return _seamMapTrackingAlgorithm.MapPointCount; } }

        /// <summary>累计剔除的野值点数（界面观测）。</summary>
        public int SeamTrackingOutlierCount { get { return _seamMapTrackingAlgorithm.OutlierCount; } }

        /// <summary>累计无有效识别结果的帧数（界面观测）。</summary>
        public int SeamTrackingInvalidFrameCount { get { return _seamMapTrackingAlgorithm.InvalidFrameCount; } }

        /// <summary>累计因地图点不足跳过反算的次数（界面观测）。</summary>
        public int SeamTrackingSmoothSkipCount { get { return _seamMapTrackingAlgorithm.SmoothSkipCount; } }

        /// <summary>累计吞掉的抖动级 X 回退帧数（界面观测）。</summary>
        public int SeamTrackingJitterRejectCount { get { return _seamMapTrackingAlgorithm.JitterRejectCount; } }

        /// <summary>累计因最终目标突变被拒的帧数（界面观测）。</summary>
        public int SeamTrackingTargetStepRejectCount { get { return _seamMapTrackingAlgorithm.TargetStepRejectCount; } }

        /// <summary>最近一帧对应的跟踪轴实际位置（毫米，界面观测）。</summary>
        public double SeamTrackingAxisAtMeasureMm { get { return _seamMapTrackingAlgorithm.AxisAtMeasureMm; } }

        /// <summary>焊缝地图跟踪最近机器人 X（观测用）</summary>
        public double SeamTrackingRobotX { get { return _seamMapTrackingAlgorithm.RobotX; } }

        /// <summary>焊缝跟踪算法是否已触发报警。</summary>
        public bool SeamTrackingHasFault { get { return _seamTrackingAlgorithm.HasFault; } }

        /// <summary>焊缝跟踪算法报警原因。</summary>
        public string SeamTrackingFaultReason { get { return _seamTrackingAlgorithm.FaultReason; } }

        /// <summary>最近一次焊缝跟踪偏差（毫米）。</summary>
        public double SeamTrackingLastDeviation { get { return _seamTrackingAlgorithm.LastDeviation; } }

        /// <summary>最近一次焊缝跟踪目标位置（毫米）。</summary>
        public double SeamTrackingLastTargetPos { get { return _seamTrackingAlgorithm.LastTargetPos; } }

        /// <summary>把线激光最近一帧焊缝宽度喂入调整算法的滑</summary>
        /// <remarks>动平均序列（无效帧跳过；手动固定宽度来源时不喂）。</remarks>
        private void FeedLatestSeamWidth()
        {
            if (_algorithmsConfig != null && _algorithmsConfig.SeamWidthSource == SeamWidthSource.Manual)
            {
                return;
            }
            try
            {
                LineLaserSeamResult last = LineLaserManager.Instance.LastResult;
                if (!last.Valid)
                {
                    return;
                }
                _paramAdjustmentAlgorithm.AccumulateSeamWidth((last.Width0 + last.Width1) / 2.0);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "喂入焊缝宽度异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>无参触发一次自动调整（使用最近一帧的焊缝特</summary>
        /// <remarks>对外保持无参，配置与机器人坐标内部取用。 征）。</remarks>
        public void TriggerAdjustment()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            FeedLatestSeamWidth();
            _paramAdjustmentAlgorithm.ExecuteAdjustment(CurrentAutoParam, CurrentRobotX);
        }

        /// <summary>生成参数查找表（按焊缝宽度范围与灵敏度生成</summary>
        /// <remarks>三张映射表；重建前同步光斑宽度系数 W）。</remarks>
        /// <param name="autoParam">自动参数配置</param>
        public void BuildLookupTable(AutoParam autoParam)
        {
            _lookupAlgorithm.SetSpotWidthGain(_algorithmsConfig != null ? _algorithmsConfig.SpotWidthGain : 1.0);
            _lookupAlgorithm.BuildLookupTable(autoParam);
        }

        /// <summary>按当前光斑宽度系数 W 重算光斑宽度列（宽度档与</summary>
        /// <remarks>宽度来源切换只影响光斑宽度，勿重建整表。
        /// 基准表仅首建用，不得覆盖 DB 修正。</remarks>
        public void RefreshSpotWidthByGain()
        {
            _lookupAlgorithm.SetSpotWidthGain(_algorithmsConfig != null ? _algorithmsConfig.SpotWidthGain : 1.0);
            _lookupAlgorithm.RefreshSpotWidthDic();
        }

        /// <summary>按焊缝宽度在查找表中匹配最近一档工艺参数。</summary>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="laserPower">匹配到的激光功率</param>
        /// <param name="feedSpeed">匹配到的送丝速度</param>
        /// <param name="robotSpeed">匹配到的焊接速度</param>
        /// <returns>匹配成功返回 true</returns>
        public bool TryLookup(double seamWidth, out double laserPower, out double feedSpeed, out double robotSpeed)
        {
            return _lookupAlgorithm.TryLookup(seamWidth, out laserPower, out feedSpeed, out robotSpeed);
        }

        /// <summary>按焊缝宽度查摆幅宽度（查表值=公式初值+手动修</summary>
        /// <remarks>正；无记录返回 false，调用方按公式兜底）。</remarks>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="swingWidth">命中的摆幅宽度（毫米）</param>
        /// <returns>命中返回 true</returns>
        public bool TryLookupSwingWidth(double seamWidth, out double swingWidth)
        {
            return _lookupAlgorithm.TryLookupSwingWidth(seamWidth, out swingWidth);
        }

        /// <summary>按焊缝宽度查摆幅频率（查表值=公式初值+手动核</summary>
        /// <remarks>对；无记录返回 false，调用方按基准公式兜底）。</remarks>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="frequencyHz">命中的摆幅频率（Hz）</param>
        /// <returns>命中返回 true</returns>
        public bool TryLookupSwingFrequency(double seamWidth, out int frequencyHz)
        {
            return _lookupAlgorithm.TryLookupSwingFrequency(seamWidth, out frequencyHz);
        }

        /// <summary>按当拍机器人 X 取「已到达的最邻近 XL」的工艺参数（XL 输</summary>
        /// <remarks>出支路每拍调用）。</remarks>
        /// <param name="robotX">当拍机器人 X（毫米）</param>
        /// <param name="laserPower">激光功率</param>
        /// <param name="feedSpeed">送丝速度</param>
        /// <param name="robotSpeed">焊接速度</param>
        /// <param name="spotWidth">光斑宽度</param>
        /// <param name="seamWidth">采用的焊缝宽度</param>
        /// <returns>命中返回 true；前置量段内尚无缓存点返回 false</returns>
        public bool TryGetParamOutputAt(double robotX, out double laserPower, out double feedSpeed,
                                        out double robotSpeed, out double spotWidth, out double seamWidth)
        {
            laserPower = 0.0;
            feedSpeed = 0.0;
            robotSpeed = 0.0;
            spotWidth = 0.0;
            seamWidth = 0.0;

            return _paramAdjustmentAlgorithm.TryGetOutputAt(robotX, out laserPower, out feedSpeed,
                                                            out robotSpeed, out spotWidth, out seamWidth);
        }

        /// <summary>复位全部算法运行期状态（查找表与运行数据全</summary>
        /// <remarks>部清空）。</remarks>
        public void Reset()
        {
            _paramAdjustmentAlgorithm.Reset();
            _lookupAlgorithm.Reset();
            _seamTrackingAlgorithm.Reset();
            _seamMapTrackingAlgorithm.Reset();
        }

        /// <summary>喂入一帧线激光结果，返回当前应下发的 PVT 点集合。</summary>
        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="measuredFps">实测帧率（Hz），不可用传 0</param>
        /// <param name="axisCurrentPos">引导轴当前位置（毫米）</param>
        /// <returns>PVT 点数组</returns>
        public PVTPoint[] FeedSeamTracking(LineLaserSeamResult result, double measuredFps, double axisCurrentPos)
        {
            return _seamTrackingAlgorithm.Feed(result, measuredFps, axisCurrentPos);
        }

        /// <summary>按最长输出间隔强制输出轨迹点。</summary>
        /// <returns>PVT 点数组</returns>
        public PVTPoint[] TickSeamTracking()
        {
            return _seamTrackingAlgorithm.Tick();
        }

        /// <summary>排空焊缝跟踪算法剩余轨迹点。</summary>
        /// <returns>PVT 点数组</returns>
        public PVTPoint[] DrainSeamTracking()
        {
            return _seamTrackingAlgorithm.Drain();
        }

        /// <summary>复位焊缝跟踪算法状态，两种模式各自复位。
        /// 并级联复位工艺调整运行期状态。</summary>
        /// <remarks>起焊沿与模拟开始都会调用本方法。
        /// 工艺调整四曲线须在会话边界清空。
        /// 否则跨会话无限累加。
        /// Reset 推进版本号，界面据此清空序列。</remarks>
        public void ResetSeamTracking(string reason = "")
        {
            GlobalCommData.ShowLog(Tag, string.Format("焊缝跟踪与工艺调整复位 原因 {0}",
                string.IsNullOrEmpty(reason) ? "未注明" : reason), MessageLevel.Info);
            _seamTrackingAlgorithm.Reset();
            _seamMapTrackingAlgorithm.Reset();
            _paramAdjustmentAlgorithm.Reset();
            SetNoValueMeasuring(false);
        }

        /// <summary>起焊沿复位：只复位焊缝跟踪算法（PVT + 焊缝地图），不清</summary>
        /// <remarks>工艺调整清空在焊前检查，避免闪断。
        /// 跟踪算法跨会话残留须在起焊沿清除。</remarks>
        public void ResetTrackingForWeldStart(string reason = "")
        {
            GlobalCommData.ShowLog(Tag, string.Format("起焊沿跟踪复位（不清工艺调整） 原因 {0}",
                string.IsNullOrEmpty(reason) ? "未注明" : reason), MessageLevel.Info);
            _seamTrackingAlgorithm.Reset();
            _seamMapTrackingAlgorithm.Reset();
            SetNoValueMeasuring(false);
        }

        /// <summary>清除焊缝跟踪折返报警态（会话切换 / 异常清理时</summary>
        /// <remarks>只清报警态，不清地图与位姿基准；整轮复位请用 <see cref="ResetSeamTracking"/>。 调用）。</remarks>
        public void ClearSeamTrackingAlarm()
        {
            _seamMapTrackingAlgorithm.ClearAlarmState();
        }

        /// <summary>喂入一帧线激光结果到焊缝地图跟踪算法（CSP 闭环</summary>
        /// <remarks>只入焊缝地图存绝对目标，不产出轨迹点。 模式）。</remarks>
        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="axisPosMm">本帧跟踪轴实际位置（毫米），还原绝对目标用</param>
        public void OnSeamLaserResult(LineLaserSeamResult result, double axisPosMm)
        {
            // 有效值到达即清零；缺值距离由位姿回调累计
            if (result.Valid) ResetNoValueDistance();

            // 入图键取位姿预测值，与查图侧同源，
            // 键阶梯会造成周期性微顿，故用外推值。
            _seamMapTrackingAlgorithm.UpdatePredictedRobotX(CurrentRobotX);
            _seamMapTrackingAlgorithm.PauseIngest = IsIngestPaused();
            _seamMapTrackingAlgorithm.OnLaserResult(result, axisPosMm);
        }

        /// <summary>判当前是否暂停入图（缺值累计中或位姿超龄）。</summary>
        /// <returns>暂停入图返回 true</returns>
        private bool IsIngestPaused()
        {
            if (NoValueDistanceMm > 0.0) return true;
            return PoseAgeMs > NoValueRsiCycleMs * 2.0;
        }

        /// <summary>起焊沿激活焊缝地图跟踪的修正量输出（基准由</summary>
        /// <remarks>起焊前沿调用：先 <see cref="ResetSeamTracking"/> 清图，再激活修正量输出；
        /// 修正量初值 0 表示保持轴当前位置不动。 运控侧取起焊锚点位置）。</remarks>
        public void BeginSeamTrackingCorrection()
        {
            // 起焊沿从零起量，防上一会话缺值距离残留
            ResetNoValueDistance();
            SetNoValueMeasuring(true);
            double threshold = _algorithmsConfig == null ? 0.0 : _algorithmsConfig.NoValueToleranceMm;
            GlobalCommData.ShowLog(Tag, string.Format("缺值阈值生效 {0:F1}mm 缺值距离量测已启用",
                threshold), MessageLevel.Info);
            _seamMapTrackingAlgorithm.BeginCorrection();
        }

        /// <summary>取焊缝地图跟踪的修正量（运控周期线程读取，叠</summary>
        /// <remarks>加起焊锚点位置后即为 Y 轴目标）。</remarks>
        /// <param name="correctionMm">修正量（毫米）</param>
        /// <returns>修正量已就绪返回 true</returns>
        public bool TryGetSeamTrackingCorrectionMm(out double correctionMm)
        {
            correctionMm = _seamMapTrackingAlgorithm.CorrectionMm;
            return _seamMapTrackingAlgorithm.HasCorrection;
        }

        /// <summary>按当拍机器人位姿取绝对 Y 轴目标（运控写入时刻</summary>
        /// <remarks>基准为标定轴值，Y 差分由算法补偿；
        /// 返回 false 时保持标定轴值。 调用）。</remarks>
        /// <param name="robotX">当拍机器人 X 坐标（毫米）</param>
        /// <param name="robotY">当拍机器人 Y 坐标（毫米）</param>
        /// <param name="targetY">绝对 Y 轴目标（毫米）</param>
        /// <returns>取到有效地图目标返回 true</returns>
        public bool TryGetSeamTrackingTargetY(double robotX, double robotY, out double targetY)
        {
            return _seamMapTrackingAlgorithm.TryGetTargetY(robotX, robotY, out targetY);
        }

        /// <summary>按机器人 X 向速度取 Y 轴跟踪速度需求（速度前馈算</summary>
        /// <remarks>法口）。</remarks>
        /// <param name="robotVxMmPerS">机器人 X 向速度幅值（毫米/秒，运控由 X 差分推算）</param>
        /// <param name="requiredVelMmPerS">Y 轴速度需求幅值（毫米/秒）</param>
        /// <returns>地图可估计局部斜率且速度为正返回 true</returns>
        public bool TryGetSeamTrackingRequiredVelocityMmPerS(double robotVxMmPerS, out double requiredVelMmPerS)
        {
            return _seamMapTrackingAlgorithm.TryGetRequiredAxisVelocityMmPerS(robotVxMmPerS, out requiredVelMmPerS);
        }

        #endregion
    }
}
