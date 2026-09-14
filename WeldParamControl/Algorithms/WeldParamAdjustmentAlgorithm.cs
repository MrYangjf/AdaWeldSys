using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.WeldParamControl;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>
    /// 焊缝宽度驱动的焊接参数调整算法实现（非单例）。
    /// </summary>
    /// <remarks>
    /// 消费线激光焊缝宽度序列：滑动平均平滑 -> 突变判定 -> 经查表算法匹配工艺参数，
    /// 维护输出值、折线图数据、连续失败计数与完成事件。由 <see cref="WeldParamManager"/> 持有。
    /// 本类**只做计算**：不落盘、不写记录，需要留痕时只触发记录请求事件（<see cref="FrameRecordRequested"/> /
    /// <see cref="AdjustmentRecordRequested"/> / <see cref="AbortRecordRequested"/>），
    /// 由记录模块订阅后异步落盘，控制与记录相互隔离。
    /// 运行参数来自 <see cref="WeldAlgorithmsConfig"/>，配置实例与持久化均由 Manager 独占。
    /// </remarks>
    public class WeldParamAdjustmentAlgorithm : Abstractions.WeldAlgorithmBase
    {
        #region 常量

        private const string Tag = "焊接计算流程";

        #endregion

        #region 私有变量

        /// <summary>查表算法（参数匹配委托方）</summary>
        private readonly WeldParamLookupAlgorithm _lookup;

        /// <summary>机器人焊接数据（过程快照来源）</summary>
        private readonly RobotWeldData _robotWeldData;

        /// <summary>算法运行配置（由 Manager 载入，本类只读消费）</summary>
        private WeldAlgorithmsConfig _config;

        #endregion

        #region 公共变量

        /// <summary>算法名称。</summary>
        public override string Name { get { return "焊缝宽度驱动的参数调整"; } }

        /// <summary>算法是否已就绪（存在可用配置）。</summary>
        public override bool IsReady { get { return _lookup != null && _lookup.IsReady && _config != null; } }

        /// <summary>算法运行配置（只读引用，改值后须由 Manager 落盘）。</summary>
        public WeldAlgorithmsConfig Config { get { return _config; } }

        /// <summary>是否已触发调整，触发后需等待焊缝宽度稳定才进行下一次调整。</summary>
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

        /// <summary>是否按点数分段计算（取自运行配置）。</summary>
        public bool IsCountType
        {
            get { return _config.IsCountType; }
            set { _config.IsCountType = value; }
        }

        /// <summary>每段点数（取自运行配置）。</summary>
        public int CountNum
        {
            get { return _config.CountNum; }
            set { _config.CountNum = value; }
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

        /// <summary>焊接工艺调整完成事件（通知主界面更新折线图）。</summary>
        public event EventHandler<WeldProcessCompletedEventArgs> WeldProcessCompleted;

        /// <summary>过程帧记录请求事件（每帧触发，落盘由记录模块异步执行）。</summary>
        public event EventHandler<WeldFrameRecordEventArgs> FrameRecordRequested;

        /// <summary>调整动作记录请求事件（调整成功时触发，落盘由记录模块异步执行）。</summary>
        public event EventHandler<WeldAdjustmentRecordEventArgs> AdjustmentRecordRequested;

        /// <summary>记录中断请求事件（连续失败超限时触发，保留临时记录由记录模块处置）。</summary>
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
            handler(this, new WeldAdjustmentRecordEventArgs(
                robotX, _robotWeldData.PosY, _robotWeldData.PosZ, _robotWeldData.Speed,
                FeedSpeedOutput, LaserPowerOutput, CurrentSeamWidth));
        }

        /// <summary>触发记录中断请求（本类不落盘）。</summary>
        private void RaiseAbortRecord()
        {
            EventHandler handler = AbortRecordRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// 将轮廓和参数数据存入 ChartVar（基于 RobotX 坐标），并触发过程帧记录请求。
        /// </summary>
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

        /// <summary>载入算法运行配置（查找表由 Manager 单独维护，此处只接管运行参数）。</summary>
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
        public override void Reset()
        {
            Trigger = false;
            CurrentSeamWidth = 0.0;
            FeedSpeedOutput = 0.0;
            RobotSpeedOutput = 0.0;
            LaserPowerOutput = 0.0;
            ConsecutiveFailureCount = 0;
            TotalExecuteCount = 0;
            SeamCalData.SeamWidthList.Clear();
            ContourChart.Clear();
            LaserPowerChart.Clear();
            FeedSpeedChart.Clear();
            SeamWidthChart.Clear();
            RobotSpeedChart.Clear();
        }

        /// <summary>
        /// 自动调整参数主流程：每一帧数据都执行，内部判断失败次数和平滑。
        /// 根据焊缝宽度变化实时匹配参数查找表，输出送丝速度、激光功率、焊接速度，
        /// 将轮廓数据存入 ChartVar 变量，并触发记录请求与完成事件。
        /// </summary>
        /// <remarks>分段方式与失败阈值取自 <see cref="Config"/>（由 Manager 载入）；本方法不落盘。</remarks>
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
                // 连续失败超限，请求记录模块保留临时记录并通知
                string abortReason = string.Format(
                    "连续失败 {0} 次 超过最大允许次数 {1} 终止焊接过程",
                    ConsecutiveFailureCount, MaxConsecutiveFailures);
                Log(abortReason, MessageLevel.Error);

                RaiseAbortRecord();

                // 触发完成事件（标记为失败）
                var failedArgs = new WeldProcessCompletedEventArgs(
                    robotX, ContourChart, LaserPowerOutput, FeedSpeedOutput,
                    CurrentSeamWidth, RobotSpeedOutput, false);
                var failedHandler = WeldProcessCompleted;
                if (failedHandler != null)
                {
                    failedHandler(this, failedArgs);
                }
                return;
            }

            bool actionSuccess = false;

            if (IsCountType)
            {
                // 判断宽度数据个数是否足够
                if (SeamCalData.SeamWidthList.Count < CountNum)
                {
                    CurrentSeamWidth = autoParam.SeamWidth;
                }
                else
                {
                    // 获取滑动平均后的焊缝宽度（在内部进行平滑）
                    double smoothedSeamWidth = SeamCalData.SeamWidthCal();

                    // 使用滑动平均后的最新值进行参数匹配
                    double temp = smoothedSeamWidth;
                    if (Math.Abs(temp - CurrentSeamWidth) > autoParam.Sensitivity && !Trigger)
                    {
                        Trigger = true;
                        CurrentSeamWidth = temp;

                        double laserPower;
                        double feedSpeed;
                        double robotSpeed;
                        if (_lookup.TryLookup(CurrentSeamWidth, out laserPower, out feedSpeed, out robotSpeed))
                        {
                            FeedSpeedOutput = feedSpeed;
                            RobotSpeedOutput = robotSpeed;
                            LaserPowerOutput = laserPower;
                            actionSuccess = true;
                        }
                        else
                        {
                            Trigger = false;
                        }
                    }
                }
            }
            else
            {
                // TODO: 按距离分段计算模式待实现
            }

            // 将数据存入ChartVar（基于RobotX）
            StoreChartData(robotX, contourPoints);

            // 请求记录模块落盘过程帧（本类不落盘）
            RaiseFrameRecord(robotX);

            // 计算成功时请求记录模块落盘本次调整动作
            if (actionSuccess)
            {
                RaiseAdjustmentRecord(robotX);
            }

            // 触发完成事件，通知主界面更新折线图
            var completedArgs = new WeldProcessCompletedEventArgs(
                robotX, ContourChart, LaserPowerOutput, FeedSpeedOutput,
                CurrentSeamWidth, RobotSpeedOutput, actionSuccess);
            var handler = WeldProcessCompleted;
            if (handler != null)
            {
                handler(this, completedArgs);
            }
        }

        #endregion
    }
}
