using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AdaWeldSystem.Comm;
using AdaWeldSystem.ProductFileManager;
using AdaWeldSystem.MotionControl;

namespace AdaWeldSystem.WeldParamControl
{
    #region 焊接工艺调整相关数据类
    /// <summary>
    /// 机器人焊接数据类，包含焊接点坐标、焊缝宽度、焊缝高度等信息，以及设置和计算相关参数的方法。
    /// </summary>
    public class RobotWeldData
    {
        public double PosX;
        public double PosY;
        public double PosZ;
        public double Speed;
        public double StartX;
        public double EndX;
        public double EndDelayDistance;

        /// <summary>
        /// 构造函数，初始化所有值为0
        /// </summary>
        public RobotWeldData()
        {
            PosX = 0;
            PosY = 0;
            PosZ = 0;
            Speed = 0;
            StartX = 0;
            EndX = 0;
            EndDelayDistance = 0;
        }

        /// <summary>
        /// 设置起始X坐标
        /// </summary>
        /// <param name="startX">起始X坐标值</param>
        public void SetStartX(double startX)
        {
            StartX = startX;
        }

        /// <summary>
        /// 设置结束X坐标
        /// </summary>
        /// <param name="endX">结束X坐标值</param>
        public void SetEndX(double endX)
        {
            EndX = endX;
        }

        /// <summary>
        /// 设置结束延迟距离
        /// </summary>
        /// <param name="delayDistance">结束延迟距离（mm）</param>
        public void SetEndDelayDistance(double delayDistance)
        {
            EndDelayDistance = delayDistance;
        }

        /// <summary>
        /// 设置焊缝起始和结束位置（由机器人示教给定）
        /// </summary>
        /// <param name="startX">起始X坐标</param>
        /// <param name="endX">结束X坐标</param>
        /// <param name="delayDistance">结束延迟距离（mm）</param>
        public void SetWeldRange(double startX, double endX, double delayDistance)
        {
            StartX = startX;
            EndX = endX;
            EndDelayDistance = delayDistance;
        }

        /// <summary>
        /// 获取有效结束位置（结束位置 + 延迟距离）
        /// </summary>
        public double EffectiveEndX
        {
            get { return EndX + EndDelayDistance; }
        }

        /// <summary>
        /// 判断当前机器人X坐标是否已进入焊接区域
        /// </summary>
        public bool IsInWeldArea(double robotX)
        {
            return robotX >= StartX && robotX <= EffectiveEndX;
        }

        /// <summary>
        /// 判断当前机器人X坐标是否已超过有效结束位置
        /// </summary>
        public bool IsBeyondEnd(double robotX)
        {
            return robotX > EffectiveEndX;
        }

        /// <summary>
        /// 设置机器人焊接坐标和速度
        /// </summary>
        /// <param name="posX">X坐标</param>
        /// <param name="posY">Y坐标</param>
        /// <param name="posZ">Z坐标</param>
        /// <param name="speed">焊接速度</param>
        /// <returns>解析成功返回true，失败返回false</returns>
        public bool SetRobot(string posX, string posY, string posZ, string speed)
        {
            if (!double.TryParse(posX, out PosX)) return false;
            if (!double.TryParse(posY, out PosY)) return false;
            if (!double.TryParse(posZ, out PosZ)) return false;
            if (!double.TryParse(speed, out Speed)) return false;
            return true;
        }
    }

    /// <summary>
    /// 激光焊接数据类，包含激光功率、送丝速度等信息，以及设置相关参数的方法。
    /// </summary>
    public class LaserWeldData
    {
        public double LaserPower;
        public double FeedSpeed;

        /// <summary>
        /// 构造函数，初始化所有值为0
        /// </summary>
        public LaserWeldData()
        {
            LaserPower = 0;
            FeedSpeed = 0;
        }

        /// <summary>
        /// 设置激光焊接数据
        /// </summary>
        /// <param name="feedVer">送丝速度字符串</param>
        /// <param name="power">激光功率字符串</param>
        /// <returns>解析成功返回true，失败返回false</returns>
        public bool SetData(string feedVer, string power)
        {
            if (!double.TryParse(feedVer, out FeedSpeed)) return false;
            if (!double.TryParse(power, out LaserPower)) return false;
            return true;
        }
    }

    /// <summary>
    /// 焊接点标定类，包含相机标定位置、机器人标定位置等信息，以及设置和计算相关参数的方法。
    /// </summary>
    public class MotionWeldPoint
    {
        public double CameraCalibPos;
        public double MotionCalibPos;
        private double _detaCamera;
        private double _targetPos;

        /// <summary>
        /// 构造函数，初始化标定位置为0
        /// </summary>
        public MotionWeldPoint()
        {
            CameraCalibPos = 0;
            MotionCalibPos = 0;
        }

        /// <summary>
        /// 设置标定位置
        /// </summary>
        /// <param name="cameraCalibPos">相机标定位置</param>
        /// <param name="motionCalibPos">机器人标定位置</param>
        public void SetCalibPos(double cameraCalibPos, double motionCalibPos)
        {
            CameraCalibPos = cameraCalibPos;
            MotionCalibPos = motionCalibPos;
        }

        /// <summary>
        /// 计算目标位置（相机坐标系到机器人坐标系的偏移转换）
        /// </summary>
        /// <param name="cameraOnlinePos">相机在线检测位置</param>
        /// <returns>转换后的机器人目标位置</returns>
        public double TargetPosCal(double cameraOnlinePos)
        {
            _detaCamera = cameraOnlinePos - CameraCalibPos;
            _targetPos = MotionCalibPos + _detaCamera;
            return _targetPos;
        }
    }

    /// <summary>
    /// 焊缝宽度计算类，维护最近N个焊缝宽度的滑动平均值
    /// </summary>
    public class SeamCalData
    {
        public List<double> SeamWidthList;
        public int SeamDataCount = 10;
        public double SeamWidth;

        /// <summary>
        /// 构造函数，初始化滑动窗口
        /// </summary>
        public SeamCalData()
        {
            SeamWidthList = new List<double>();
            SeamWidth = 0;
        }

        /// <summary>
        /// 计算当前焊缝宽度的滑动平均值
        /// </summary>
        /// <returns>平均焊缝宽度</returns>
        public double SeamWidthCal()
        {
            if (SeamWidthList.Count > 0)
            {
                SeamWidth = SeamWidthList.Average();
            }
            else
            {
                SeamWidth = 0;
            }
            return SeamWidth;
        }

        /// <summary>
        /// 添加一个新的焊缝宽度值到滑动窗口
        /// </summary>
        /// <param name="seamWidth">焊缝宽度值</param>
        public void AddSeamWidth(double seamWidth)
        {
            SeamWidthList.Add(seamWidth);
            if (SeamWidthList.Count > SeamDataCount)
            {
                SeamWidthList.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 焊缝种类枚举
    /// </summary>
    public enum SeamType
    {
        None = 0,
        VType = 1,
        TType = 2,
    }

    /// <summary>
    /// 自动参数配置类，包含焊接工艺的所有参数及计算公式
    /// </summary>
    public class AutoParam
    {
        /// <summary>
        /// 默认配置标识
        /// </summary>
        public const string DefaultIdentity = "DefaultParam";

        public SeamType WeldType;

        public string identityInfo;
        public DateTime CreatTime;
        public string WireType;
        public double WireDiameter;
        public double LaserDiameter;
        public double LaserPower;
        public double SeamLength;
        public double SeamWidth;
        public string PlateType;
        public double Platethickness;
        public double FeedSpeed;
        public double RobotSpeed;
        public double SeamWidthMax;
        public double SeamWidthMin;
        public double Sensitivity;

        public double KVar;
        public double JVar;
        public double MVar;

        /// <summary>
        /// 是否为默认配置
        /// </summary>
        public bool IsDefault
        {
            get { return identityInfo == DefaultIdentity; }
        }

        /// <summary>
        /// 创建默认自动参数配置
        /// </summary>
        /// <returns>默认 AutoParam 实例</returns>
        public static AutoParam CreateDefault()
        {
            return new AutoParam(DefaultIdentity, DateTime.Now);
        }

        /// <summary>
        /// 自动参数构造
        /// </summary>
        /// <param name="identity">标识</param>
        /// <param name="creatTime">创建时间</param>
        public AutoParam(string identity, DateTime creatTime)
        {
            identityInfo = identity;
            CreatTime = creatTime;
            WireType = "Default";
            PlateType = "Default";
            Platethickness = 10;
            WireDiameter = 3;
            LaserDiameter = 3;
            LaserPower = 3000;
            SeamLength = 150;
            SeamWidth = 5;
            FeedSpeed = 4000;
            RobotSpeed = 50;
            MVar = 0.6;
            WeldType = 0;
        }

        /// <summary>
        /// 计算KVar和JVar的值，KVar用于计算送丝速度，JVar用于计算激光功率。
        /// </summary>
        public void CalVar()
        {
            KVar = ((3.14 / 4) * WireDiameter * WireDiameter * (FeedSpeed / 60)) / (Platethickness * SeamWidth * RobotSpeed);
            JVar = (MVar * LaserPower) / ((3.14 / 4) * WireDiameter * WireDiameter * (FeedSpeed / 60));
        }
    }

    /// <summary>
    /// 折线坐标数据，用于主界面表格折线图勾画
    /// </summary>
    public class ChartVar
    {
        public List<double> X;
        public List<double> Y;

        /// <summary>
        /// 构造函数，初始化坐标列表
        /// </summary>
        public ChartVar()
        {
            X = new List<double>();
            Y = new List<double>();
        }

        /// <summary>
        /// 添加坐标点
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        public void Add(double x, double y)
        {
            X.Add(x);
            Y.Add(y);
        }

        /// <summary>
        /// 获取坐标点数量
        /// </summary>
        /// <returns>坐标点数量</returns>
        public int Count()
        {
            return X.Count;
        }

        /// <summary>
        /// 清空所有坐标数据
        /// </summary>
        public void Clear()
        {
            X.Clear();
            Y.Clear();
        }
    }

    /// <summary>
    /// 焊接工艺调整完成事件参数
    /// </summary>
    public class WeldProcessCompletedEventArgs : EventArgs
    {
        /// <summary>当前机器人X坐标</summary>
        public double RobotX { get; }

        /// <summary>轮廓数据（基于RobotX的轮廓坐标）</summary>
        public ChartVar ContourData { get; }

        /// <summary>激光功率输出</summary>
        public double LaserPower { get; }

        /// <summary>送丝速度输出</summary>
        public double FeedSpeed { get; }

        /// <summary>焊缝宽度</summary>
        public double SeamWidth { get; }

        /// <summary>机器人移动速度</summary>
        public double RobotSpeed { get; }

        /// <summary>是否成功</summary>
        public bool Success { get; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; }

        public WeldProcessCompletedEventArgs(double robotX, ChartVar contourData, double laserPower, double feedSpeed, double seamWidth, double robotSpeed, bool success)
        {
            RobotX = robotX;
            ContourData = contourData ?? new ChartVar();
            LaserPower = laserPower;
            FeedSpeed = feedSpeed;
            SeamWidth = seamWidth;
            RobotSpeed = robotSpeed;
            Success = success;
            Timestamp = DateTime.Now;
        }
    }
    #endregion

    /// <summary>
    /// 焊接工艺核心逻辑类：参数自动调整、送丝速度/激光功率计算、在线数据记录
    /// </summary>
    public class WeldProcess
    {
        private const string Tag = "焊接计算流程";

        /// <summary>统一日志出口</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        public RobotWeldData RobotWeldData;
        public LaserWeldData LaserWeldData;

        #region 机器人X（真实坐标）

        private readonly object _robotXLock = new object();

        /// <summary>线程安全地读取当前机器人X（真实坐标，来自 RobotWeldData.PosX）</summary>
        public double CurrentRobotX
        {
            get
            {
                lock (_robotXLock)
                {
                    return RobotWeldData != null ? RobotWeldData.PosX : 0.0;
                }
            }
        }

        #endregion

        /// <summary>
        /// 是否调整参数触发，触发后需要等待焊缝宽度稳定才进行下一次调整
        /// </summary>
        public bool Trigger;

        public double StartRobotX;
        public double CurrentSeamWidth;

        public double FeedSpeedOutput;
        public double RobotSpeedOutput;
        public double LaserPowerOutput;

        #region 轮廓数据ChartVar（用于主界面折线图显示）

        /// <summary>
        /// 轮廓数据（基于RobotX的轮廓坐标）
        /// </summary>
        public ChartVar ContourChart { get; private set; }

        /// <summary>
        /// 激光功率折线数据（基于RobotX）
        /// </summary>
        public ChartVar LaserPowerChart { get; private set; }

        /// <summary>
        /// 送丝速度折线数据（基于RobotX）
        /// </summary>
        public ChartVar FeedSpeedChart { get; private set; }

        /// <summary>
        /// 焊缝宽度折线数据（基于RobotX）
        /// </summary>
        public ChartVar SeamWidthChart { get; private set; }

        /// <summary>
        /// 机器人移动速度折线数据（基于RobotX）
        /// </summary>
        public ChartVar RobotSpeedChart { get; private set; }

        #endregion

        /// <summary>
        /// 焊接工艺调整完成事件（通知主界面更新折线图）
        /// </summary>
        public event EventHandler<WeldProcessCompletedEventArgs> WeldProcessCompleted;

        #region 失败计数与平滑控制

        /// <summary>
        /// 连续失败计数（由AutoAction维护）
        /// </summary>
        public int ConsecutiveFailureCount { get; private set; }

        /// <summary>
        /// 最大允许连续失败次数
        /// </summary>
        public int MaxConsecutiveFailures { get; set; }

        /// <summary>
        /// 算法执行总次数
        /// </summary>
        public int TotalExecuteCount { get; private set; }

        /// <summary>
        /// 焊缝宽度滑动平均计算器（在AutoAction内部进行平滑）
        /// </summary>
        public SeamCalData SeamCalData { get; private set; }

        /// <summary>
        /// 当前自动参数配置（由外部设置，用于事件回调）
        /// </summary>
        public AutoParam CurrentAutoParam { get; set; }

        /// <summary>
        /// 最近一次算法完成事件携带的机器人X坐标
        /// </summary>
        private double _lastRobotX;

        /// <summary>
        /// 是否按点数分段计算
        /// </summary>
        public bool IsCountType { get; set; }

        /// <summary>
        /// 每段点数
        /// </summary>
        public int CountNum { get; set; }

        #endregion

        private static readonly Lazy<WeldProcess> _lazyInstance = new Lazy<WeldProcess>(() => new WeldProcess());

        /// <summary>
        /// 单例实例（线程安全）
        /// </summary>
        public static WeldProcess Instance => _lazyInstance.Value;

        private WeldProcess()
        {
            RobotWeldData = new RobotWeldData();
            LaserWeldData = new LaserWeldData();
            ContourChart = new ChartVar();
            LaserPowerChart = new ChartVar();
            FeedSpeedChart = new ChartVar();
            SeamWidthChart = new ChartVar();
            RobotSpeedChart = new ChartVar();
            SeamCalData = new SeamCalData();

            // ADR-002 兼容：auto-property initializer 在 MSBuild v4.0 报 CS1519，改在构造函数初始化
            MaxConsecutiveFailures = 5;
            IsCountType = true;
            CountNum = 5;

        }


        #region 自动调整参数，需要从数据库读取的参数列表，焊缝宽度为基准值，其他参数为对应关系

        /// <summary>
        /// 焊缝宽度列表（参数查找表的键）
        /// </summary>
        public List<double> SeamWidthList = new List<double>();

        /// <summary>
        /// 激光功率查找表（焊缝宽度 -> 激光功率）
        /// </summary>
        public Dictionary<double, double> LaserPowerDic = new Dictionary<double, double>();

        /// <summary>
        /// 送丝速度查找表（焊缝宽度 -> 送丝速度）
        /// </summary>
        public Dictionary<double, double> FeedSpeedDic = new Dictionary<double, double>();

        /// <summary>
        /// 焊接速度查找表（焊缝宽度 -> 焊接速度）
        /// </summary>
        public Dictionary<double, double> RobotSpeedDic = new Dictionary<double, double>();

        #endregion

        /// <summary>
        /// 自动调整参数主流程
        /// 每一帧数据都执行，内部判断失败次数和平滑
        /// 根据焊缝宽度变化实时匹配参数查找表，输出送丝速度、激光功率、焊接速度
        /// 并将轮廓数据存入ChartVar变量，触发完成事件
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="isCountType">是否按点数分段计算</param>
        /// <param name="countNum">每段点数</param>
        /// <param name="robotX">当前机器人X坐标</param>
        /// <param name="contourPoints">轮廓点列表（可选）</param>
        public void AutoAction(AutoParam autoParam, bool isCountType, int countNum, double robotX = 0, List<System.Drawing.Point> contourPoints = null)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            // 首先判断失败次数是否超出阈值
            if (ConsecutiveFailureCount >= MaxConsecutiveFailures)
            {
                // 连续失败超限，中断记录并通知
                string abortReason = string.Format(
                    "连续失败 {0} 次 超过最大允许次数 {1} 终止焊接过程",
                    ConsecutiveFailureCount, MaxConsecutiveFailures);
                Log(abortReason, MessageLevel.Error);

                try
                {
                    RunRecordManager.Instance.AbortRecording();
                }
                catch (Exception ex)
                {
                    Log(string.Format(
                        "调用AbortRecording失败 原因是 {0}", ex.Message), MessageLevel.Error);
                }

                // 触发完成事件（标记为失败）
                var failedArgs = new WeldProcessCompletedEventArgs(
                    robotX, ContourChart, LaserPowerOutput, FeedSpeedOutput,
                    CurrentSeamWidth, RobotSpeedOutput, false);
                WeldProcessCompleted?.Invoke(this, failedArgs);
                return;
            }

            bool actionSuccess = false;

            // 设定执行宽度值，第一次输入为焊缝当前基准
            // 输入连续的宽度值，计算最后整体长度（以某个值定义为一段）：比如5个点或距离5mm生成一段焊缝宽
            // 比较整体变化（避免某一个点变化影响）：A段宽 B段宽 C段宽
            // 若A段大于B段宽某个值（自动调整参数的灵敏值），判定为焊缝产生了突变
            // 产生突变的X（机器人坐标）去对应自动调整参数

            if (isCountType)
            {
                // 判断宽度数据个数是否足够
                if (SeamCalData.SeamWidthList.Count < countNum)
                {
                    CurrentSeamWidth = autoParam.SeamWidth;
                }
                else
                {
                    // 获取滑动平均后的焊缝宽度（在AutoAction内部进行平滑）
                    double smoothedSeamWidth = SeamCalData.SeamWidthCal();

                    // 使用滑动平均后的最新值进行参数匹配
                    double temp = smoothedSeamWidth;
                    if (Math.Abs(temp - CurrentSeamWidth) > autoParam.Sensitivity && !Trigger)
                    {
                        Trigger = true;
                        CurrentSeamWidth = temp;

                        double findSeamWidth = -1;
                        double diffTemp = 100;
                        for (int i = 0; i < SeamWidthList.Count; i++)
                        {
                            double diffAbs = Math.Abs(CurrentSeamWidth - SeamWidthList[i]);
                            if (diffAbs <= diffTemp)
                            {
                                diffTemp = diffAbs;
                                findSeamWidth = SeamWidthList[i];
                            }
                        }

                        if (findSeamWidth != -1)
                        {
                            FeedSpeedOutput = FeedSpeedDic[findSeamWidth];
                            RobotSpeedOutput = RobotSpeedDic[findSeamWidth];
                            LaserPowerOutput = LaserPowerDic[findSeamWidth];
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

            // 如果计算成功，记录到RunFileRecord
            if (actionSuccess)
            {
                RecordActionToRunFileRecord(robotX);
            }

            // 触发完成事件，通知主界面更新折线图
            var completedArgs = new WeldProcessCompletedEventArgs(
                robotX, ContourChart, LaserPowerOutput, FeedSpeedOutput,
                CurrentSeamWidth, RobotSpeedOutput, actionSuccess);
            WeldProcessCompleted?.Invoke(this, completedArgs);
        }

        /// <summary>
        /// 由 LineLaserWorkflow 在 Adjust 步骤调用，使用最近一帧的焊缝特征执行自动调整，
        /// 并通过 RunRecordManager 落盘记录（每一帧记录 + 调整时记录）。
        /// </summary>
        public void TriggerAdjustment()
        {
            if (CurrentAutoParam != null)
            {
                AutoAction(CurrentAutoParam, IsCountType, CountNum, _lastRobotX);
            }
        }

        /// <summary>
        /// 将轮廓和参数数据存入ChartVar（基于RobotX坐标）
        /// </summary>
        /// <param name="robotX">当前机器人X坐标</param>
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

            // 存储激光功率
            LaserPowerChart.Add(robotX, LaserPowerOutput);

            // 存储送丝速度
            FeedSpeedChart.Add(robotX, FeedSpeedOutput);

            // 存储焊缝宽度
            SeamWidthChart.Add(robotX, CurrentSeamWidth);

            // 存储机器人移动速度
            RobotSpeedChart.Add(robotX, RobotSpeedOutput);

            // 每帧记录过程数据到 RunFileRecord（与图像算法焊缝宽度、智能焊接输出联动）
            try
            {
                var rwd = RobotWeldData;
                RunRecordManager.Instance.RecordProcessFrame(
                    RunRecordManager.Instance.CurrentFrameIndex,
                    robotX,
                    rwd != null ? rwd.PosY : 0,
                    rwd != null ? rwd.PosZ : 0,
                    rwd != null ? rwd.Speed : 0,
                    FeedSpeedOutput,
                    LaserPowerOutput,
                    CurrentSeamWidth);
            }
            catch (Exception ex)
            {
                Log(string.Format(
                    "记录过程帧到RunFileRecord失败 原因是 {0}", ex.Message), MessageLevel.Error);
            }
        }

        /// <summary>
        /// 将焊接动作记录到RunFileRecord
        /// </summary>
        /// <param name="robotX">当前机器人X坐标</param>
        private void RecordActionToRunFileRecord(double robotX)
        {
            try
            {
                var recordManager = RunRecordManager.Instance;
                if (recordManager == null)
                {
                    return;
                }

                // 构建动作参数字符串
                var paramBuilder = new StringBuilder();
                paramBuilder.AppendFormat("SeamWidth={0};", CurrentSeamWidth);
                paramBuilder.AppendFormat("FeedSpeed={0};", FeedSpeedOutput);
                paramBuilder.AppendFormat("LaserPower={0};", LaserPowerOutput);
                paramBuilder.AppendFormat("RobotSpeed={0};", RobotSpeedOutput);
                paramBuilder.AppendFormat("RobotX={0}", robotX);

                // 使用当前帧索引记录调整动作（含机器人坐标与过程快照）
                int frameIndex = recordManager.CurrentFrameIndex;
                var rwd = RobotWeldData;
                recordManager.RecordAdjustment(
                    frameIndex, robotX,
                    rwd != null ? rwd.PosY : 0,
                    rwd != null ? rwd.PosZ : 0,
                    rwd != null ? rwd.Speed : 0,
                    FeedSpeedOutput,
                    LaserPowerOutput,
                    CurrentSeamWidth,
                    paramBuilder.ToString());

                Log(string.Format(
                    "记录焊接动作到RunFileRecord 帧索引 {0} 参数 {1}",
                    frameIndex, paramBuilder.ToString()), MessageLevel.Info);
            }
            catch (Exception ex)
            {
                Log(string.Format(
                    "记录焊接动作到RunFileRecord失败 原因是 {0}", ex.Message), MessageLevel.Error);
            }
        }

        /// <summary>
        /// 送丝速度计算公式
        /// FeedSpeed = 60 * KVar * SeamWidth * PlateThickness * RobotSpeed / (pi/4 * WireDiameter^2)
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="robotSpeed">机器人焊接速度</param>
        /// <param name="seamWidth">焊缝宽度</param>
        /// <returns>计算得到的送丝速度</returns>
        public double FeedSpeedCal(AutoParam autoParam, double robotSpeed, double seamWidth)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            double re = 60 * autoParam.KVar * seamWidth * autoParam.Platethickness * robotSpeed / ((3.14 / 4) * autoParam.WireDiameter * autoParam.WireDiameter);
            return re;
        }

        /// <summary>
        /// 激光功率计算公式
        /// LaserPower = JVar * (pi/4 * WireDiameter^2 * FeedSpeed/60) / MVar
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="feedSpeed">送丝速度</param>
        /// <returns>计算得到的激光功率</returns>
        public double LaserPowerCal(AutoParam autoParam, double feedSpeed)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            double re = (autoParam.JVar * (3.14 / 4) * autoParam.WireDiameter * autoParam.WireDiameter * (feedSpeed / 60)) / autoParam.MVar;
            return re;
        }

        /// <summary>
        /// 生成参数查找表
        /// 根据自动参数配置的焊缝宽度范围、灵敏度，生成焊缝宽度与送丝速度/激光功率/焊接速度的映射字典
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        public void GetListValue(AutoParam autoParam)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            autoParam.CalVar();
            SeamWidthList = new List<double>();
            LaserPowerDic = new Dictionary<double, double>();
            FeedSpeedDic = new Dictionary<double, double>();
            RobotSpeedDic = new Dictionary<double, double>();

            int arrayAddCount = (int)(Math.Truncate((autoParam.SeamWidthMax - autoParam.SeamWidth) / autoParam.Sensitivity) + 2);
            int arrayInclineCount = (int)(Math.Truncate((autoParam.SeamWidth - autoParam.SeamWidthMin) / autoParam.Sensitivity) + 2);

            for (int i = 0; i < arrayAddCount; i++)
            {
                double seamWidthValue = autoParam.SeamWidth + i * autoParam.Sensitivity;
                double robotSpeed = autoParam.RobotSpeed;
                double feedSpeed = FeedSpeedCal(autoParam, robotSpeed, seamWidthValue);
                double laserPower = LaserPowerCal(autoParam, feedSpeed);
                SeamWidthList.Add(seamWidthValue);
                RobotSpeedDic.Add(seamWidthValue, robotSpeed);
                FeedSpeedDic.Add(seamWidthValue, feedSpeed);
                LaserPowerDic.Add(seamWidthValue, laserPower);
            }

            for (int i = 0; i < arrayInclineCount; i++)
            {
                if (i != 0)
                {
                    double seamWidthValue = autoParam.SeamWidth - i * autoParam.Sensitivity;
                    double robotSpeed = autoParam.RobotSpeed;
                    double feedSpeed = FeedSpeedCal(autoParam, robotSpeed, seamWidthValue);
                    double laserPower = LaserPowerCal(autoParam, feedSpeed);
                    SeamWidthList.Add(seamWidthValue);
                    RobotSpeedDic.Add(seamWidthValue, robotSpeed);
                    FeedSpeedDic.Add(seamWidthValue, feedSpeed);
                    LaserPowerDic.Add(seamWidthValue, laserPower);
                }
            }

            SeamWidthList.Sort();
        }

       
    }
}
