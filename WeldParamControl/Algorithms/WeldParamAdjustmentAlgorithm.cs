using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.WeldParamControl.Abstractions;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>宽度驱动的参数调整算法（工艺控制唯一）。</summary>
    /// <remarks>
    /// 消费宽度序列：平滑→判变→查表→维护输出。
    /// 并承载 XL 缓存，供输出支路按拍取点。
    /// 只计算与触发记录，落盘交给记录模块。</remarks>
    public class WeldParamAdjustmentAlgorithm : WeldAlgorithmBase
    {
        #region 常量

        private const string Tag = "焊接计算流程";

        /// <summary>XL 缓存点数上限，超限丢最旧点。</summary>
        private const int MaxXlCachePoints = 8000;

        /// <summary>宽度变化判定阈值（毫米），超限即留痕。</summary>
        private const double WidthChangeLogEpsilonMm = 1e-6;

        /// <summary>宽度变化日志限频间隔（毫秒）。</summary>
        private const int WidthChangeLogIntervalMs = 1000;

        /// <summary>XL 超限清理的批量大小，避免逐点删除。</summary>
        private const int XlTrimBatch = 1000;

        /// <summary>XL 同键合并容差（毫米），小于即覆盖。</summary>
        private const double XlSameKeyEpsilonMm = 0.001;

        #endregion

        #region 私有变量

        /// <summary>查表算法（参数匹配委托方）</summary>
        private readonly WeldParamLookupAlgorithm _lookup;

        /// <summary>机器人焊接数据（过程快照来源）</summary>
        private readonly RobotWeldData _robotWeldData;

        /// <summary>算法运行配置（由 Manager 载入，本类只读消费）</summary>
        private WeldAlgorithmsConfig _config;

        /// <summary>输出缓存版本号，调整或复位时自增。</summary>
        private long _outputVersion;

        /// <summary>工艺参数 XL 缓存点列表（升序，支持二分）。</summary>
        private readonly List<XLPoint> _xlPoints = new List<XLPoint>();

        /// <summary>手动源是否已落过一次调整留痕（每会话仅一次）。</summary>
        private bool _manualAdjustRaised;

        /// <summary>宽度变化留痕：上次已记录的采用宽度（毫米）。</summary>
        private double _lastLoggedSeamWidth;

        /// <summary>宽度变化累计次数（含复位归零；用于判定焊接中宽度是否反复跳变）。</summary>
        private int _seamWidthChangeCount;

        /// <summary>宽度变化日志上次输出时刻（限频）。</summary>
        private DateTime _lastWidthLogTime;

        /// <summary>本段已累计行进距离（毫米）。</summary>
        private double _segmentTravelMm;

        /// <summary>本段有效宽度累加和（毫米）。</summary>
        private double _segmentWidthSum;

        /// <summary>本段有效宽度点数。</summary>
        private int _segmentWidthCount;

        /// <summary>上一拍机器人 X（分段累计锚点）。</summary>
        private double _lastSegmentRobotX;

        /// <summary>分段累计锚点是否已建立。</summary>
        private bool _hasSegmentAnchor;

        #endregion

        #region 公共变量

        /// <summary>算法名称。</summary>
        public override string Name { get { return "焊缝宽度驱动的参数调整"; } }

        /// <summary>算法是否已就绪（存在可用配置）。</summary>
        public override bool IsReady { get { return _lookup != null && _lookup.IsReady && _config != null; } }

        /// <summary>算法运行配置（只读引用，改值后须由 Manager 落盘）。</summary>
        public WeldAlgorithmsConfig Config { get { return _config; } }

        /// <summary>是否已触发调整，需宽度稳定后再调整。</summary>
        public bool Trigger;

        /// <summary>焊接起始机器人 X 坐标。</summary>
        public double StartRobotX;

        /// <summary>本轮采用的焊缝宽度。</summary>
        public double CurrentSeamWidth;

        /// <summary>送丝速度输出。</summary>
        public double FeedSpeedOutput;

        /// <summary>焊接速度输出。</summary>
        public double RobotSpeedOutput;

        /// <summary>激光功率输出。</summary>
        public double LaserPowerOutput;

        /// <summary>光斑宽度输出（采用宽度 × 系数 W）。</summary>
        public double SpotWidthOutput;

        /// <summary>连续失败计数。</summary>
        public int ConsecutiveFailureCount { get; private set; }

        /// <summary>最大允许连续失败次数（取自运行配置）。</summary>
        public int MaxConsecutiveFailures
        {
            get { return _config.MaxConsecutiveFailures; }
            set { _config.MaxConsecutiveFailures = value; }
        }

        /// <summary>算法执行总次数。</summary>
        public int TotalExecuteCount { get; private set; }

        /// <summary>焊缝宽度滑动平均计算器。</summary>
        public SeamCalData SeamCalData { get; private set; }

        // 工艺输出参数：本算法统一管理，配置仍由 WeldAlgorithmsConfig 落盘。
        // 分区不变（③Adapt / ①System），INI/XML 键零改动。
        // 只把管理归属迁至算法层。

        /// <summary>工艺输出·手动固定缝宽 mm（落盘走 WeldAlgorithmsConfig ③Adapt）。</summary>
        public double ManualSeamWidthMm
        {
            get { return _config.ManualSeamWidthMm; }
            set { _config.ManualSeamWidthMm = value; }
        }

        /// <summary>工艺输出·光斑宽度系数（③Adapt 落盘兼容）。</summary>
        public double SpotWidthGain
        {
            get { return _config.SpotWidthGain; }
            set { _config.SpotWidthGain = value; }
        }

        /// <summary>工艺输出·参数输出频率 Hz（③Adapt 落盘兼容）。</summary>
        public int ParamOutputHz
        {
            get { return _config.ParamOutputHz; }
            set { _config.ParamOutputHz = value; }
        }

        /// <summary>工艺输出·前馈安全系数（①System 落盘兼容）。</summary>
        public double FeedforwardSafetyFactor
        {
            get { return _config.FeedforwardSafetyFactor; }
            set { _config.FeedforwardSafetyFactor = value; }
        }

        /// <summary>工艺输出·单调方向锁开关（①System 落盘兼容；唯一 UI 入口在工艺设置页自适应区）。</summary>
        public bool EnableMonotonicConstraint
        {
            get { return _config.EnableMonotonicConstraint; }
            set { _config.EnableMonotonicConstraint = value; }
        }

        /// <summary>轮廓数据（基于 RobotX 的轮廓坐标）。</summary>
        public ChartVar ContourChart { get; private set; }

        /// <summary>激光功率折线数据（基于 RobotX）。</summary>
        public ChartVar LaserPowerChart { get; private set; }

        /// <summary>送丝速度折线数据（基于 RobotX）。</summary>
        public ChartVar FeedSpeedChart { get; private set; }

        /// <summary>焊缝宽度折线数据（基于 RobotX）。</summary>
        public ChartVar SeamWidthChart { get; private set; }

        /// <summary>机器人移动速度折线数据（基于 RobotX）。</summary>
        public ChartVar RobotSpeedChart { get; private set; }

        /// <summary>输出缓存版本号，界面轮询此号决定消费。</summary>
        public long OutputVersion { get { return Interlocked.Read(ref _outputVersion); } }

        /// <summary>工艺参数 XL 缓存点数（诊断）。</summary>
        public int XlCacheCount { get { return _xlPoints.Count; } }

        /// <summary>XL 缓存抖动回退被吞掉的帧数（诊断）。</summary>
        public int XlJitterRejectCount { get; private set; }

        /// <summary>XL 缓存超限丢弃的最旧点累计数（诊断）。</summary>
        public int XlDroppedCount { get; private set; }

        /// <summary>焊接工艺调整完成事件（通知主界面更新折线图）。</summary>
        public event EventHandler<WeldProcessCompletedEventArgs> WeldProcessCompleted;

        /// <summary>过程帧记录请求事件（每帧触发，记录模块落盘）。</summary>
        public event EventHandler<WeldFrameRecordEventArgs> FrameRecordRequested;

        /// <summary>调整动作记录请求事件（成功时触发）。</summary>
        public event EventHandler<WeldAdjustmentRecordEventArgs> AdjustmentRecordRequested;

        /// <summary>记录中断请求事件（连续失败超限时触发）。</summary>
        public event EventHandler AbortRecordRequested;

        #endregion

        #region 构造函数

        /// <summary>构造函数。</summary>
        /// <param name="lookup">查表算法实例</param>
        /// <param name="robotWeldData">机器人焊接数据（过程快照来源，可为 null）</param>
        public WeldParamAdjustmentAlgorithm(WeldParamLookupAlgorithm lookup, RobotWeldData robotWeldData)
        {
            if (lookup == null)
            {
                throw new ArgumentNullException(nameof(lookup));
            }

            _lookup = lookup;
            _robotWeldData = robotWeldData ?? new RobotWeldData();
            _config = new WeldAlgorithmsConfig();

            ContourChart = new ChartVar();
            LaserPowerChart = new ChartVar();
            FeedSpeedChart = new ChartVar();
            SeamWidthChart = new ChartVar();
            RobotSpeedChart = new ChartVar();
            SeamCalData = new SeamCalData();
            _lastLoggedSeamWidth = 0.0;
            _seamWidthChangeCount = 0;
            _lastWidthLogTime = DateTime.Now;
        }

        #endregion

        #region 私有函数

        /// <summary>统一日志出口。</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>触发过程帧记录请求（本类不落盘）。</summary>
        /// <param name="robotX">当前机器人 X 坐标</param>
        private void RaiseFrameRecord(double robotX)
        {
            EventHandler<WeldFrameRecordEventArgs> handler = FrameRecordRequested;
            if (handler == null) return;
            handler(this, new WeldFrameRecordEventArgs(
                robotX, _robotWeldData.PosY, _robotWeldData.PosZ, _robotWeldData.Speed,
                FeedSpeedOutput, LaserPowerOutput, CurrentSeamWidth));
        }

        /// <summary>触发调整动作记录请求（本类不落盘）。</summary>
        /// <param name="robotX">当前机器人 X 坐标</param>
        private void RaiseAdjustmentRecord(double robotX)
        {
            EventHandler<WeldAdjustmentRecordEventArgs> handler = AdjustmentRecordRequested;
            if (handler == null) return;
            var args = new WeldAdjustmentRecordEventArgs(
                robotX, _robotWeldData.PosY, _robotWeldData.PosZ, _robotWeldData.Speed,
                FeedSpeedOutput, LaserPowerOutput, CurrentSeamWidth)
            {
                // 查表输出的机器人速度单独留痕，与 RobotX 同帧
                RobotSpeedOutput = RobotSpeedOutput
            };
            handler(this, args);
        }

        /// <summary>触发记录中断请求（本类不落盘）。</summary>
        private void RaiseAbortRecord()
        {
            AbortRecordRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>将轮廓和参数数据存入 ChartVar（基于 RobotX 坐标），并触发过程帧</summary>
        /// <remarks>记录请求。</remarks>
        /// <param name="robotX">当前机器人 X 坐标</param>
        /// <param name="contourPoints">轮廓点列表</param>
        private void StoreChartData(double robotX, List<System.Drawing.Point> contourPoints)
        {
            // 存储轮廓数据（如果有轮廓点）
            if (contourPoints != null && contourPoints.Count > 0)
            {
                foreach (var pt in contourPoints)
                {
                    // 轮廓X坐标相对于RobotX的偏移
                    ContourChart.Add(robotX, pt.Y);
                }
            }

            LaserPowerChart.Add(robotX, LaserPowerOutput);
            FeedSpeedChart.Add(robotX, FeedSpeedOutput);
            SeamWidthChart.Add(robotX, CurrentSeamWidth);
            RobotSpeedChart.Add(robotX, RobotSpeedOutput);
        }

        #endregion

        #region 公共函数

        /// <summary>载入算法运行配置（查找表由 Manager 单独维护，此处只接</summary>
        /// <remarks>管运行参数）。</remarks>
        /// <param name="config">配置对象，应为 <see cref="WeldAlgorithmsConfig"/></param>
        public override void LoadConfig(object config)
        {
            SetConfig(config as WeldAlgorithmsConfig);
        }

        /// <summary>更新运行配置，配置重新加载后调用。</summary>
        /// <param name="config">参数调整配置</param>
        public void SetConfig(WeldAlgorithmsConfig config)
        {
            if (config == null) return;
            _config = config;
        }

        /// <summary>复位运行期状态（保留查找表配置）。</summary>
        /// <remarks>清零只在会话边界，管控尚未开始。
        /// 起焊沿只清跟踪，避免宽度闪断。</remarks>
        public override void Reset()
        {
            // 复位是唯一降宽入口，须留痕可归因。
            Log(string.Format("工艺调整算法复位 复位前采用宽度 {0:F3}mm 宽度变化累计 {1} 次 复位后宽度 0.000mm",
                CurrentSeamWidth, _seamWidthChangeCount));
            Trigger = false;
            CurrentSeamWidth = 0.0;
            _lastLoggedSeamWidth = 0.0;
            FeedSpeedOutput = 0.0;
            RobotSpeedOutput = 0.0;
            LaserPowerOutput = 0.0;
            SpotWidthOutput = 0.0;
            ConsecutiveFailureCount = 0;
            TotalExecuteCount = 0;
            SeamCalData.SeamWidthList.Clear();
            _segmentTravelMm = 0.0;
            _segmentWidthSum = 0.0;
            _segmentWidthCount = 0;
            _hasSegmentAnchor = false;
            ContourChart.Clear();
            LaserPowerChart.Clear();
            FeedSpeedChart.Clear();
            SeamWidthChart.Clear();
            RobotSpeedChart.Clear();
            _xlPoints.Clear();
            _manualAdjustRaised = false;
            XlJitterRejectCount = 0;
            XlDroppedCount = 0;
            // 复位也推进版本号，轮询端据此清空显示序列。
            Interlocked.Increment(ref _outputVersion);
        }

        /// <summary>喂入有效宽度 同步滑动窗与段累计</summary>
        /// <param name="seamWidth">焊缝宽度（毫米）</param>
        public void AccumulateSeamWidth(double seamWidth)
        {
            SeamCalData.AddSeamWidth(seamWidth);
            _segmentWidthSum += seamWidth;
            _segmentWidthCount++;
        }

        /// <summary>自动调整参数主流程：每一帧数据都执行，内部判</summary>
        /// <remarks>分段与阈值取自 <see cref="Config"/>，本方法不落盘。按宽度查表输出参数，并存图与触发记录。</remarks>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="robotX">当前机器人 X 坐标</param>
        /// <param name="contourPoints">轮廓点列表（可选）</param>
        public void ExecuteAdjustment(AutoParam autoParam,
                                      double robotX = 0, List<System.Drawing.Point> contourPoints = null)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            TotalExecuteCount++;

            // 首先判断失败次数是否超出阈值
            if (ConsecutiveFailureCount >= MaxConsecutiveFailures)
            {
                // 连续失败超限，请求记录模块保留临时记录。
                Log(string.Format(
                    "连续失败 {0} 次 超过最大允许次数 {1} 终止焊接过程",
                    ConsecutiveFailureCount, MaxConsecutiveFailures), MessageLevel.Error);

                RaiseAbortRecord();

                // 触发完成事件（标记为失败）
                var failedArgs = new WeldProcessCompletedEventArgs(
                    robotX, ContourChart, LaserPowerOutput, FeedSpeedOutput,
                    CurrentSeamWidth, RobotSpeedOutput, false);
                WeldProcessCompleted?.Invoke(this, failedArgs);
                return;
            }

            bool actionSuccess = false;

            if (_config.SeamWidthSource == SeamWidthSource.Manual)
            {
                // 手动固定宽度：直接查表，全程无变化。
                CurrentSeamWidth = _config.ManualSeamWidthMm;

                if (_lookup.TryLookupWithSpot(CurrentSeamWidth, out double laserPower, out double feedSpeed,
                                              out double robotSpeed, out double spotWidth))
                {
                    FeedSpeedOutput = feedSpeed;
                    RobotSpeedOutput = robotSpeed;
                    LaserPowerOutput = laserPower;
                    SpotWidthOutput = spotWidth;
                    actionSuccess = true;
                }
            }
            else
            {
                // 按距离分段 达段距取段内均值查表
                if (_hasSegmentAnchor)
                {
                    _segmentTravelMm += Math.Abs(robotX - _lastSegmentRobotX);
                }
                _lastSegmentRobotX = robotX;
                _hasSegmentAnchor = true;

                if (_segmentTravelMm >= _config.SegmentDistanceMm)
                {
                    bool hasPoints = _segmentWidthCount > 0;
                    double segmentWidth = hasPoints ? _segmentWidthSum / _segmentWidthCount : 0.0;
                    _segmentTravelMm = 0.0;
                    _segmentWidthSum = 0.0;
                    _segmentWidthCount = 0;
                    Trigger = hasPoints;
                    // 段内无有效点不查表，交缺值报警统一处置
                    if (hasPoints && _lookup.TryLookupWithSpot(segmentWidth, out double laserPower, out double feedSpeed,
                                                               out double robotSpeed, out double spotWidth))
                    {
                        CurrentSeamWidth = segmentWidth;
                        FeedSpeedOutput = feedSpeed;
                        RobotSpeedOutput = robotSpeed;
                        LaserPowerOutput = laserPower;
                        SpotWidthOutput = spotWidth;
                        actionSuccess = true;
                    }
                }
            }

            StoreChartData(robotX, contourPoints);

            // 宽度变化留痕：手动源下宽度应全程恒定。
            // 任何变化都必须可归因（赋值/复位/源为 Laser）。
            // 限频 1s 防刷屏，变化次数单独累计。
            if (Math.Abs(CurrentSeamWidth - _lastLoggedSeamWidth) > WidthChangeLogEpsilonMm)
            {
                _seamWidthChangeCount++;
                DateTime now = DateTime.Now;
                if ((now - _lastWidthLogTime).TotalMilliseconds >= WidthChangeLogIntervalMs)
                {
                    _lastWidthLogTime = now;
                    Log(string.Format("焊缝宽度变化 X {0:F3}mm 宽度源 {1} 由 {2:F3} 变为 {3:F3} 本次会话第 {4} 次变化",
                        robotX, _config.SeamWidthSource, _lastLoggedSeamWidth, CurrentSeamWidth, _seamWidthChangeCount));
                }
                _lastLoggedSeamWidth = CurrentSeamWidth;
            }

            // 推进版本号，界面按轮询节拍取数。
            Interlocked.Increment(ref _outputVersion);

            // 请求记录模块落盘过程帧（本类不落盘）
            RaiseFrameRecord(robotX);

            // 计算成功时请求记录模块落盘本次调整动作。
            // 手动源输出恒定，逐拍留痕无意义，
            // 但会话首个成功输出必须留一次痕。
            // 起焊初值是「已下发」的唯一凭据。
            if (actionSuccess)
            {
                if (_config.SeamWidthSource != SeamWidthSource.Manual)
                {
                    RaiseAdjustmentRecord(robotX);
                }
                else if (!_manualAdjustRaised)
                {
                    _manualAdjustRaised = true;
                    RaiseAdjustmentRecord(robotX);
                }
            }

            // 触发完成事件，通知主界面更新折线图
            var completedArgs = new WeldProcessCompletedEventArgs(
                robotX, ContourChart, LaserPowerOutput, FeedSpeedOutput,
                CurrentSeamWidth, RobotSpeedOutput, actionSuccess);
            WeldProcessCompleted?.Invoke(this, completedArgs);

            // XL 缓存喂入（仅线激光源）：键 = 测量 X + 前置量。
            // Manual 源每拍直查表，无需缓存。
            if (_config.SeamWidthSource == SeamWidthSource.Laser)
            {
                OnParamFrame(robotX, LaserPowerOutput, FeedSpeedOutput, RobotSpeedOutput, SpotWidthOutput, CurrentSeamWidth);
            }
        }

        /// <summary>喂入一帧测量时刻工艺参数进 XL 缓存（由 <see cref="ExecuteAdjustment"/> 在线激光</summary>
        /// <remarks>源下自动调用，外部一般无需直调）。</remarks>
        /// <param name="measureRobotX">测量时刻机器人 X（毫米）</param>
        /// <param name="laserPower">激光功率</param>
        /// <param name="feedSpeed">送丝速度</param>
        /// <param name="robotSpeed">焊接速度</param>
        /// <param name="spotWidth">光斑宽度</param>
        /// <param name="seamWidth">采用的焊缝宽度</param>
        public void OnParamFrame(double measureRobotX, double laserPower, double feedSpeed,
                                 double robotSpeed, double spotWidth, double seamWidth)
        {
            double xl = measureRobotX + _config.LeadDistanceMm;

            if (_xlPoints.Count > 0)
            {
                XLPoint last = _xlPoints[_xlPoints.Count - 1];
                if (xl < last.RobotXL - XlSameKeyEpsilonMm)
                {
                    // X 抖动回退：吞掉不入缓存（同单调化口径）。
                    XlJitterRejectCount++;
                    return;
                }
                if (xl <= last.RobotXL + XlSameKeyEpsilonMm)
                {
                    // 同键覆盖：同一 XL 的新参数以最新为准
                    last.LaserPower = laserPower;
                    last.FeedSpeed = feedSpeed;
                    last.RobotSpeed = robotSpeed;
                    last.SpotWidth = spotWidth;
                    last.SeamWidth = seamWidth;
                    return;
                }
            }

            _xlPoints.Add(new XLPoint(xl, laserPower, feedSpeed, robotSpeed, spotWidth, seamWidth));

            if (_xlPoints.Count > MaxXlCachePoints)
            {
                int trim = Math.Min(XlTrimBatch, _xlPoints.Count);
                _xlPoints.RemoveRange(0, trim);
                XlDroppedCount += trim;
            }
        }

        /// <summary>按当拍机器人 X 取「已到达的最邻近 XL」的工艺参数（XL 输</summary>
        /// <remarks>出支路每拍调用）。</remarks>
        /// <param name="robotXNow">当拍机器人 X（毫米）</param>
        /// <param name="laserPower">激光功率</param>
        /// <param name="feedSpeed">送丝速度</param>
        /// <param name="robotSpeed">焊接速度</param>
        /// <param name="spotWidth">光斑宽度</param>
        /// <param name="seamWidth">采用的焊缝宽度</param>
        /// <returns>命中返回 true；尚无已到达的缓存点返回 false</returns>
        public bool TryGetOutputAt(double robotXNow, out double laserPower, out double feedSpeed,
                                   out double robotSpeed, out double spotWidth, out double seamWidth)
        {
            laserPower = 0.0;
            feedSpeed = 0.0;
            robotSpeed = 0.0;
            spotWidth = 0.0;
            seamWidth = 0.0;
            if (_xlPoints.Count == 0) return false;

            // 二分查找最后一个 RobotXL <= robotXNow 的点
            int lo = 0;
            int hi = _xlPoints.Count - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_xlPoints[mid].RobotXL <= robotXNow)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (found < 0) return false;
            XLPoint point = _xlPoints[found];
            laserPower = point.LaserPower;
            feedSpeed = point.FeedSpeed;
            robotSpeed = point.RobotSpeed;
            spotWidth = point.SpotWidth;
            seamWidth = point.SeamWidth;
            return true;
        }

        #endregion

        #region XL 缓存点

        /// <summary>工艺参数 XL 缓存点（键 = 测量 X + 前置量）。</summary>
        private class XLPoint
        {
            /// <summary>缓存键：焊枪到达该 X 时输出本组参数（毫米）。</summary>
            public double RobotXL;

            /// <summary>激光功率</summary>
            public double LaserPower;

            /// <summary>送丝速度</summary>
            public double FeedSpeed;

            /// <summary>焊接速度</summary>
            public double RobotSpeed;

            /// <summary>光斑宽度（毫米，= 焊缝宽度 × 系数 W）</summary>
            public double SpotWidth;

            /// <summary>采用的焊缝宽度（毫米）</summary>
            public double SeamWidth;

            /// <summary>构造函数。</summary>
            /// <param name="robotXL">缓存键（毫米）</param>
            /// <param name="laserPower">激光功率</param>
            /// <param name="feedSpeed">送丝速度</param>
            /// <param name="robotSpeed">焊接速度</param>
            /// <param name="spotWidth">光斑宽度</param>
            /// <param name="seamWidth">焊缝宽度</param>
            public XLPoint(double robotXL, double laserPower, double feedSpeed,
                           double robotSpeed, double spotWidth, double seamWidth)
            {
                RobotXL = robotXL;
                LaserPower = laserPower;
                FeedSpeed = feedSpeed;
                RobotSpeed = robotSpeed;
                SpotWidth = spotWidth;
                SeamWidth = seamWidth;
            }
        }

        #endregion
    }
}
