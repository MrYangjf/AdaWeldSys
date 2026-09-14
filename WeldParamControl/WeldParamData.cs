using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// 过程帧记录请求事件参数（算法只发请求，落盘由记录模块执行，ADR-008 ProcessFrame）。
    /// </summary>
    /// <remarks>帧索引属记录侧状态，由记录模块自行取用，算法不感知。</remarks>
    public class WeldFrameRecordEventArgs : EventArgs
    {
        /// <summary>机器人 X 坐标</summary>
        public double RobotX { get; }

        /// <summary>机器人 Y 坐标</summary>
        public double RobotY { get; }

        /// <summary>机器人 Z 坐标</summary>
        public double RobotZ { get; }

        /// <summary>机器人移动速度</summary>
        public double RobotSpeed { get; }

        /// <summary>送丝速度</summary>
        public double FeedSpeed { get; }

        /// <summary>激光功率</summary>
        public double LaserPower { get; }

        /// <summary>焊缝宽度</summary>
        public double WeldWidth { get; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; }

        public WeldFrameRecordEventArgs(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth)
        {
            RobotX = robotX;
            RobotY = robotY;
            RobotZ = robotZ;
            RobotSpeed = robotSpeed;
            FeedSpeed = feedSpeed;
            LaserPower = laserPower;
            WeldWidth = weldWidth;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 调整动作记录请求事件参数（算法只发请求，落盘由记录模块执行，ADR-008 WeldParamAdjust）。
    /// </summary>
    /// <remarks>与过程帧共用同一组过程快照字段，独立成类型以便两类记录契约各自演进。</remarks>
    public class WeldAdjustmentRecordEventArgs : WeldFrameRecordEventArgs
    {
        public WeldAdjustmentRecordEventArgs(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth)
            : base(robotX, robotY, robotZ, robotSpeed, feedSpeed, laserPower, weldWidth)
        {
        }
    }
    #endregion
}