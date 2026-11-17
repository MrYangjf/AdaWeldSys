using System;
using System.Collections.Generic;
using System.Linq;

namespace AdaWeldSystem.WeldParamControl
{
    #region 焊接工艺调整相关数据类
    /// <summary>焊接流程步骤（与机器人指令/整机状态对齐）。</summary>
    public enum WeldFlowStep
    {
        SafePose = 0,        // 安全位（焊接准备）
        PreWeldCheck = 1,    // 焊前检查（PreWork）
        ArcStart = 2,        // 起焊（Working 启动）
        Welding = 3,         // 焊接中
        WeldEnd = 4          // 焊接结束
    }

    /// <summary>焊接流程步骤信息（标题/副标题/说明；数据层唯一来源）。</summary>
    public class WeldFlowStepInfo
    {
        /// <summary>步骤标识。</summary>
        public WeldFlowStep Step;
        /// <summary>步骤标题（步骤条主文案）。</summary>
        public string Title;
        /// <summary>步骤副标题（步骤条次文案）。</summary>
        public string SubTitle;
        /// <summary>步骤说明（日志/提示用完整描述）。</summary>
        public string Description;

        /// <summary>构造一条步骤信息。</summary>
        /// <param name="step">步长。</param>
        public WeldFlowStepInfo(WeldFlowStep step, string title, string subTitle, string description)
        {
            Step = step;
            Title = title;
            SubTitle = subTitle;
            Description = description;
        }
    }

    /// <summary>焊接流程步骤表：安全位 → 焊前检查 → 起焊 → 焊接 → 结束。</summary>
    /// <remarks>与项目现行链路对齐：RX_START_SAFE_POSE → RX_PRE_WELD_REQUEST/IO_SUB_PREWORK →
    /// RX_WELD_START_POSE/IO_SUB_WORK → RX_WELD_STOP_POSE/IO_SUB_STOP → RX_STOP_SAFE_POSE。</remarks>
    public static class WeldFlowSteps
    {
        /// <summary>全流程步骤（数组顺序即步骤条索引）。</summary>
        public static readonly WeldFlowStepInfo[] All =
        {
            new WeldFlowStepInfo(WeldFlowStep.SafePose, "安全位", "焊接准备", "机器人回安全起始位，设备待机就绪"),
            new WeldFlowStepInfo(WeldFlowStep.PreWeldCheck, "焊前检查", "PreWork", "整机启动，模组焊前检查与工艺参数预置"),
            new WeldFlowStepInfo(WeldFlowStep.ArcStart, "起焊", "焊接启动", "机器人到焊接起始位，进入 Working 并开启生产会话"),
            new WeldFlowStepInfo(WeldFlowStep.Welding, "焊接", "自适应跟踪", "沿焊缝焊接，工艺参数实时调整与焊缝监视"),
            new WeldFlowStepInfo(WeldFlowStep.WeldEnd, "结束", "停止流程", "到焊接停止位，收尾并回安全停止位")
        };

        /// <summary>步骤总数。</summary>
        public static int Count { get { return All.Length; } }

        /// <summary>取步骤索引（枚举值即索引）。</summary>
        /// <param name="step">步长。</param>
        /// <returns>结果码。</returns>
        public static int IndexOf(WeldFlowStep step)
        {
            int index = (int)step;
            return index >= 0 && index < All.Length ? index : 0;
        }

        /// <summary>取步骤信息（越界回落首步，防 UI 空引用）。</summary>
        /// <param name="step">步长。</param>
        public static WeldFlowStepInfo Get(WeldFlowStep step)
        {
            return All[IndexOf(step)];
        }
    }

    /// <summary>
    /// 机器人焊接数据：坐标/焊缝宽度/高度及设置方法。
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

        public double EffectiveEndX
        {
            get { return EndX + EndDelayDistance; }
        }

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
        /// 判断当前机器人X坐标是否已进入焊接区域
        /// </summary>
        /// <returns>是否成功。</returns>
        public bool IsInWeldArea(double robotX)
        {
            return robotX >= StartX && robotX <= EffectiveEndX;
        }

        /// <summary>
        /// 判断当前机器人X坐标是否已超过有效结束位置
        /// </summary>
        /// <returns>是否成功。</returns>
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
    /// 焊缝宽度滑动平均：维护最近 N 个宽度值。
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
    /// 自适应控制输出：算法算出的工艺参数，供下发。
    /// </summary>
    /// <remarks>
    /// 边界：只装自适应调整算法算出来的量。
    /// 轴位置与速度由上位机或运控主动下发。
    /// 这类参数一律归 <see cref="WeldParamMontionControl"/>。
    /// 不得混入本类。
    /// </remarks>
    public class WeldParamAdaptControl
    {
        /// <summary>焊接功率（激光功率），自适应算出。</summary>
        public double LaserPower;

        /// <summary>送丝速度，自适应算出。</summary>
        public double FeedSpeed;

        /// <summary>焊接速度（机器人移动速度），自适应算出。</summary>
        public double RobotSpeed;

        /// <summary>振镜摆幅（毫米），自适应算出。</summary>
        public double GalvoAmplitude;

        /// <summary>振镜摆幅频率（Hz），摆幅越大频率越低。</summary>
        public int GalvoFrequency;

        /// <summary>光斑宽度（毫米，= 采用焊缝宽度 × 系数 W；暂不下发机器人，后续供振镜摆幅控制使用）。</summary>
        public double SpotWidth;

        /// <summary>本轮采用的焊缝宽度（毫米，平滑后）。</summary>
        public double SeamWidth;

        /// <summary>计算是否成功。</summary>
        public bool Success;

        /// <summary>本次计算的时刻。</summary>
        public DateTime Timestamp;
    }

    /// <summary>
    /// 运动主动控制参数：上位机/运控直给的轴位置量。
    /// </summary>
    /// <remarks>
    /// 主动控制输入与自适应输出是两回事，不得混类。
    /// 本类是上位机与运控直给的位置量。
    /// 不经自适应算法。
    /// 混在一类会让「谁决定这个值」不可追溯。
    /// 消费方：MainPage 的 Y 轴与送丝 Z 轴曲线。
    /// </remarks>
    public class WeldParamMontionControl
    {
        /// <summary>Y 轴位置（毫米，主动控制，不由自适应算法决定）。</summary>
        public double AxisYPositionMm;

        /// <summary>送丝 Z 轴位置（毫米，TA 力矩电机位置，主动控制）。</summary>
        public double WireZPositionMm;

        /// <summary>本次下发的时刻。</summary>
        public DateTime Timestamp;

        /// <summary>清零（复位或尚无下发时调用）。</summary>
        /// <remarks>避免读到上一次的陈值。</remarks>
        public void Reset()
        {
            AxisYPositionMm = 0.0;
            WireZPositionMm = 0.0;
            Timestamp = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 自动参数配置：焊接工艺参数及计算公式。
    /// </summary>
    public class AutoParam
    {
        /// <summary>
        /// 默认配置标识
        /// </summary>
        public const string DefaultIdentity = "DefaultParam";

        /// <summary>
        /// 光斑摆幅系数默认值（1.2 保持既有「焊缝宽 ×(1+0.2)」行为）
        /// </summary>
        public const double DefaultSpotSwingGain = 1.2;

        /// <summary>
        /// 摆幅频率基准点①的摆幅宽度默认值（毫米）
        /// </summary>
        public const double DefaultSwingFreqRefWidth1 = 2.0;

        /// <summary>
        /// 摆幅频率基准点①的频率默认值（Hz）
        /// </summary>
        /// <remarks>协议侧频率为整型（振镜 10~500Hz 精度 1），与 <see cref="GalvoFrequencyMinHz"/> 一致取 int。</remarks>
        public const int DefaultSwingFreqHz1 = 500;

        /// <summary>
        /// 摆幅频率基准点②的摆幅宽度默认值（毫米）
        /// </summary>
        public const double DefaultSwingFreqRefWidth2 = 10.0;

        /// <summary>
        /// 摆幅频率基准点②的频率默认值（Hz）
        /// </summary>
        /// <remarks>协议侧频率为整型（振镜 10~500Hz 精度 1），与 <see cref="GalvoFrequencyMinHz"/> 一致取 int。</remarks>
        public const int DefaultSwingFreqHz2 = 200;

        /// <summary>
        /// 振镜频率协议下限（Hz）
        /// </summary>
        public const int GalvoFrequencyMinHz = 10;

        /// <summary>
        /// 振镜频率协议上限（Hz）
        /// </summary>
        public const int GalvoFrequencyMaxHz = 500;

        public SeamType WeldType;

        /// <summary>接头类型（英莱 jointtypelist.json 的 20 类编号；0 = 未指定）</summary>
        public int JointType;

        public string identityInfo;
        public DateTime CreatTime;
        public double WireDiameter;
        public double LaserDiameter;
        public double LaserPower;
        public double SeamWidth;
        public double Platethickness;
        public double FeedSpeed;
        public double RobotSpeed;
        public double SeamWidthMax;
        public double SeamWidthMin;
        public double Sensitivity;

        public double KVar;
        public double JVar;
        public double MVar;

        /// <summary>光斑摆幅系数：摆幅宽度 = 系数 × 焊缝宽。
        /// 旧 JSON 缺字段时为 0，CalVar 内自愈。</summary>
        public double SpotSwingGain;

        /// <summary>摆幅宽度（毫米，= 光斑摆幅系数 × 焊缝宽，CalVar 计算；即振镜摆动形成的光斑覆盖宽度）。</summary>
        public double SwingWidth;

        /// <summary>摆幅频率基准点①：摆幅宽度（毫米）。默认 2mm。</summary>
        public double SwingFreqRefWidth1;

        /// <summary>摆幅频率基准点①：对应频率（Hz）。默认 500Hz。</summary>
        public double SwingFreqHz1;

        /// <summary>摆幅频率基准点②：摆幅宽度（毫米）。默认 10mm。</summary>
        public double SwingFreqRefWidth2;

        /// <summary>摆幅频率基准点②：对应频率（Hz）。默认 200Hz（摆幅越大频率越低）。</summary>
        public double SwingFreqHz2;

        /// <summary>
        /// 是否为默认配置
        /// </summary>
        public bool IsDefault
        {
            get { return identityInfo == DefaultIdentity; }
        }

        public AutoParam(string identity, DateTime creatTime)
        {
            identityInfo = identity;
            CreatTime = creatTime;
            JointType = 0;
            Platethickness = 10;
            WireDiameter = 3;
            LaserDiameter = 3;
            LaserPower = 3000;
            SeamWidth = 5;
            FeedSpeed = 4000;
            RobotSpeed = 50;
            MVar = 0.6;
            SpotSwingGain = DefaultSpotSwingGain;
            SwingFreqRefWidth1 = DefaultSwingFreqRefWidth1;
            SwingFreqHz1 = DefaultSwingFreqHz1;
            SwingFreqRefWidth2 = DefaultSwingFreqRefWidth2;
            SwingFreqHz2 = DefaultSwingFreqHz2;
            WeldType = 0;
        }

        /// <summary>
        /// 创建默认自动参数配置
        /// </summary>
        /// <returns>默认 AutoParam 实例</returns>
        public static AutoParam CreateDefault()
        {
            return new AutoParam(DefaultIdentity, DateTime.Now);
        }

        /// <summary>计算 KVar、JVar 与摆幅宽度。</summary>
        /// <remarks>KVar 用于送丝速度，JVar 用于激光功率。
        /// 摆幅宽度 = 光斑摆幅系数 × 焊缝宽。</remarks>
        public void CalVar()
        {
            // 旧 JSON 缺字段自愈：光斑摆幅系数非正时回默认值
            if (SpotSwingGain <= 0.0)
            {
                SpotSwingGain = DefaultSpotSwingGain;
            }
            // 摆幅频率基准点自愈：缺失或非正回默认值；两基准点宽度相同则退化为点②频率
            if (SwingFreqRefWidth1 <= 0.0)
            {
                SwingFreqRefWidth1 = DefaultSwingFreqRefWidth1;
            }
            if (SwingFreqHz1 <= 0.0)
            {
                SwingFreqHz1 = DefaultSwingFreqHz1;
            }
            if (SwingFreqRefWidth2 <= 0.0)
            {
                SwingFreqRefWidth2 = DefaultSwingFreqRefWidth2;
            }
            if (SwingFreqHz2 <= 0.0)
            {
                SwingFreqHz2 = DefaultSwingFreqHz2;
            }
            KVar = ((3.14 / 4) * WireDiameter * WireDiameter * (FeedSpeed / 60)) / (Platethickness * SeamWidth * RobotSpeed);
            JVar = (MVar * LaserPower) / ((3.14 / 4) * WireDiameter * WireDiameter * (FeedSpeed / 60));
            SwingWidth = SpotSwingGain * SeamWidth;
        }

        /// <summary>按摆幅宽度反算摆幅频率（越大频率越低）。</summary>
        /// <remarks>
        /// 基准点①：2mm → 500Hz；基准点②：10mm → 200Hz。
        /// 两点间线性插值，外段按端点钳位。
        /// 结果再钳到振镜协议频率范围 10~500Hz。
        /// </remarks>
        /// <param name="swingWidthMm">摆幅宽度（毫米）</param>
        /// <returns>摆幅频率（Hz，取整）</returns>
        public int CalSwingFrequency(double swingWidthMm)
        {
            double w1 = SwingFreqRefWidth1 > 0.0 ? SwingFreqRefWidth1 : DefaultSwingFreqRefWidth1;
            double h1 = SwingFreqHz1 > 0.0 ? SwingFreqHz1 : DefaultSwingFreqHz1;
            double w2 = SwingFreqRefWidth2 > 0.0 ? SwingFreqRefWidth2 : DefaultSwingFreqRefWidth2;
            double h2 = SwingFreqHz2 > 0.0 ? SwingFreqHz2 : DefaultSwingFreqHz2;

            double hz;
            if (Math.Abs(w2 - w1) < 1e-9)
            {
                // 两基准点宽度重合：退化为点②频率（不产生除零）
                hz = h2;
            }
            else
            {
                hz = h1 + (h2 - h1) * (swingWidthMm - w1) / (w2 - w1);
                // 外段钳位到基准区间（取两基准频率的上下界）。
                double hMin = Math.Min(h1, h2);
                double hMax = Math.Max(h1, h2);
                if (hz < hMin) hz = hMin;
                if (hz > hMax) hz = hMax;
            }

            int result = (int)Math.Round(hz);
            if (result < GalvoFrequencyMinHz) result = GalvoFrequencyMinHz;
            if (result > GalvoFrequencyMaxHz) result = GalvoFrequencyMaxHz;
            return result;
        }
    }

    /// <summary>
    /// 折线坐标数据，用于主界面表格折线图勾画
    /// </summary>
    public class ChartVar
    {
        /// <summary>读写互斥锁：生产与消费线程并发访问。</summary>
        private readonly object _sync = new object();

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
        /// <remarks>X/Y 必须在同一把锁内成对写入。
        /// 否则消费端可能读到 X 比 Y 多一条的中间态。
        /// 会触发 ScottPlot AddScatter 长度不等异常。</remarks>
        public void Add(double x, double y)
        {
            lock (_sync)
            {
                X.Add(x);
                Y.Add(y);
            }
        }

        /// <summary>
        /// 获取坐标点数量
        /// </summary>
        /// <returns>坐标点数量</returns>
        public int Count()
        {
            lock (_sync)
            {
                return X.Count;
            }
        }

        /// <summary>
        /// 清空所有坐标数据
        /// </summary>
        public void Clear()
        {
            lock (_sync)
            {
                X.Clear();
                Y.Clear();
            }
        }

        /// <summary>配对快照：锁内一次性取出 X/Y 副本。</summary>
        /// <remarks>两数组长度必然相等。</remarks>
        /// <param name="xs">X 坐标副本</param>
        /// <param name="ys">Y 坐标副本</param>
        /// <returns>有有效数据返回 true；空数据或长度不一致返回 false</returns>
        public bool TrySnapshot(out double[] xs, out double[] ys)
        {
            lock (_sync)
            {
                int count = X.Count;
                if (count <= 0 || count != Y.Count)
                {
                    xs = new double[0];
                    ys = new double[0];
                    return false;
                }

                xs = X.ToArray();
                ys = Y.ToArray();
                return true;
            }
        }

        /// <summary>增量配对快照：取 startIndex 起的新增点。</summary>
        /// <remarks>供界面增量拷贝，避免每拍全量复制。</remarks>
        /// <param name="startIndex">起始下标（已消费点数）</param>
        /// <param name="count">当前总点数</param>
        /// <param name="xs">新增 X 坐标副本</param>
        /// <param name="ys">新增 Y 坐标副本</param>
        /// <returns>存在新增点返回 true</returns>
        public bool TryCopyRange(int startIndex, out int count, out double[] xs, out double[] ys)
        {
            lock (_sync)
            {
                count = X.Count;
                int take = count - startIndex;
                if (startIndex < 0 || take <= 0 || count != Y.Count)
                {
                    xs = new double[0];
                    ys = new double[0];
                    return false;
                }

                xs = new double[take];
                ys = new double[take];
                X.CopyTo(startIndex, xs, 0, take);
                Y.CopyTo(startIndex, ys, 0, take);
                return true;
            }
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
    /// 过程帧记录参数：算法只发请求，记录侧落盘。
    /// </summary>
    /// <remarks>帧索引由记录模块自行取用，算法不感知。</remarks>
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
    /// 调整动作记录参数：算法只发请求，记录侧落盘。
    /// </summary>
    /// <remarks>
    /// 与过程帧共用过程快照字段；FeedSpeed 与 LaserPower 为调整后输出值。
    /// RobotSpeedOutput 单独承载查表速度，供记录侧按调整值留痕。
    /// 与 RobotX 同帧落盘，保证对齐。
    /// </remarks>
    public class WeldAdjustmentRecordEventArgs : WeldFrameRecordEventArgs
    {
        /// <summary>查表输出的机器人速度（调整值；未显式赋值时等于当前机器人速度）</summary>
        public double RobotSpeedOutput { get; set; }

        public WeldAdjustmentRecordEventArgs(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth)
            : base(robotX, robotY, robotZ, robotSpeed, feedSpeed, laserPower, weldWidth)
        {
            RobotSpeedOutput = robotSpeed;
        }

    }
    #endregion
}