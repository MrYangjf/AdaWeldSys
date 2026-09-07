using System.Collections.Generic;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>运动轴抽象基类</summary>
    /// <remarks>
    /// 运动参数挂在轴上，一次配置多次调用；位置与速度一律工程单位。
    /// 默认实现 IPVTMotion 但四个入口均返回不支持码，由具备该能力的厂商轴重写。
    /// </remarks>
    public abstract class MontionAxis : IPVTMotion
    {
        #region 私有变量

        private readonly string _aName;

        #endregion

        #region 公共变量

        /// <summary>所属控制器</summary>
        public MontionControl Container { get; protected set; }

        /// <summary>轴名称</summary>
        public string AxisName { get { return _aName; } }

        /// <summary>轴号</summary>
        public ushort AxisNo { get; set; }

        /// <summary>脉冲当量（工程单位/脉冲）</summary>
        public double Equiv { get; set; }

        /// <summary>起始速度（工程单位/秒）</summary>
        public double MinVel { get; set; }

        /// <summary>运行速度（工程单位/秒）</summary>
        public double MaxVel { get; set; }

        /// <summary>加速时间（秒）</summary>
        public double AccTime { get; set; }

        /// <summary>减速时间（秒）</summary>
        public double DecTime { get; set; }

        /// <summary>S 曲线时间（秒）</summary>
        public double STime { get; set; }

        /// <summary>停止速度（工程单位/秒）</summary>
        public double StopVel { get; set; }

        /// <summary>回零方式</summary>
        public ushort HomeMode { get; set; }

        /// <summary>回零方向，0 为负向，1 为正向</summary>
        public ushort HomeDir { get; set; }

        /// <summary>回零速度（工程单位/秒）</summary>
        public double HomeMaxVel { get; set; }

        #endregion

        #region 保护变量

        /// <summary>连接号</summary>
        protected ushort _connectNo;

        #endregion

        #region 构造函数

        /// <summary>创建轴并注册到所属控制器</summary>
        /// <param name="container">所属控制器</param>
        /// <param name="aName">轴名称</param>
        protected MontionAxis(MontionControl container, string aName)
        {
            Container = container;
            _aName = aName;
            _connectNo = container.ConnectNo;
            Equiv = 1;
            AccTime = 0.1;
            DecTime = 0.1;
            STime = 0.05;
            container.AddAxis(this);
        }

        #endregion

        #region 公共函数

        /// <summary>判断是否正在运动</summary>
        /// <returns>运动中返回 true</returns>
        public abstract bool IsMoving();

        /// <summary>等待运动到位</summary>
        /// <returns>到位返回 true，超时返回 false</returns>
        public abstract bool WaitMoveDone();

        /// <summary>停止轴运动</summary>
        /// <param name="stopMode">0 为减速停止，1 为急停</param>
        public abstract void AxisStop(ushort stopMode);

        /// <summary>判断伺服是否已使能</summary>
        /// <returns>已使能返回 true</returns>
        public abstract bool IsServoOn();

        /// <summary>伺服使能</summary>
        public abstract void ServoOn();

        /// <summary>伺服下电</summary>
        public abstract void ServoOff();

        /// <summary>复位驱动器报警</summary>
        public abstract void ResetAlarm();

        /// <summary>回零并等待完成</summary>
        /// <returns>回零成功返回 true</returns>
        public abstract bool Home();

        /// <summary>启动回零后不等待</summary>
        /// <returns>启动成功返回 true</returns>
        public abstract bool HomeNoBlock();

        /// <summary>等待回零完成</summary>
        /// <returns>完成返回 true，超时返回 false</returns>
        public abstract bool WaitHomeDone();

        /// <summary>按轴参数做点位运动并等待到位</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>到位返回 true</returns>
        public abstract bool AbsMove(double pos, ushort posMode = 1);

        /// <summary>按临时参数做点位运动并等待到位</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>到位返回 true</returns>
        public abstract bool AbsMove(double pos, double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort posMode = 1);

        /// <summary>按轴参数启动点位运动后不等待</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>启动成功返回 true</returns>
        public abstract bool AbsMoveNoBlock(double pos, ushort posMode = 1);

        /// <summary>按临时参数启动点位运动后不等待</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="posMode">0 为相对，1 为绝对</param>
        /// <returns>启动成功返回 true</returns>
        public abstract bool AbsMoveNoBlock(double pos, double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort posMode = 1);

        /// <summary>按轴参数点动</summary>
        /// <param name="dir">0 为正向，1 为负向</param>
        public abstract void JogMove(ushort dir);

        /// <summary>按临时参数点动</summary>
        /// <param name="minVel">起始速度（工程单位/秒）</param>
        /// <param name="maxVel">运行速度（工程单位/秒）</param>
        /// <param name="stopVel">停止速度（工程单位/秒）</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        /// <param name="sTime">S 曲线时间（秒）</param>
        /// <param name="dir">0 为正向，1 为负向</param>
        public abstract void JogMove(double minVel, double maxVel, double stopVel,
            double accTime, double decTime, double sTime, ushort dir);

        /// <summary>读取反馈位置</summary>
        /// <returns>位置（工程单位）</returns>
        public abstract double GetPosition();

        /// <summary>读取指令位置</summary>
        /// <returns>位置（工程单位）</returns>
        public abstract double GetCommandPosition();

        /// <summary>写入当前位置</summary>
        /// <param name="pos">位置（工程单位）</param>
        public abstract void SetPosition(double pos);

        /// <summary>将当前位置设为零点</summary>
        public abstract void SetHomePosition();

        /// <summary>读取当前速度</summary>
        /// <returns>速度（工程单位/秒）</returns>
        public abstract double GetCurrentSpeed();

        /// <summary>读取当前扭矩</summary>
        /// <returns>扭矩（额定千分比）</returns>
        public abstract short GetCurrentTorque();

        /// <summary>读取驱动器状态字</summary>
        /// <returns>状态字原始值</returns>
        public abstract ushort GetStatusWord();

        #endregion

        #region PVT 多段插值

        /// <summary>下发起始段并开始运动</summary>
        /// <param name="points">起始段点集合，位置与速度为工程单位，时间为秒</param>
        /// <param name="timeMode">时间模式，决定 Time 按相对还是绝对解释</param>
        /// <param name="isFinal">是否为最后一段</param>
        /// <param name="retain">保持标志，语义待现场实测确认</param>
        /// <param name="sdStopTime">平滑停止时间（秒）</param>
        /// <returns>未重写时恒返回不支持码</returns>
        public virtual ushort BeginMove(IList<PVTPoint> points, PVTTimeMode timeMode, bool isFinal,
            bool retain, double sdStopTime)
        {
            GlobalCommData.ShowLog(_aName, "当前轴未实现 PVT 起始段下发", MessageLevel.Warning);
            return PVTResult.NotSupported;
        }

        /// <summary>运动中追加点段，保持轨迹不断流</summary>
        /// <param name="points">追加段点集合，单位同 BeginMove</param>
        /// <param name="isFinal">是否为最后一段</param>
        /// <returns>未重写时恒返回不支持码</returns>
        public virtual ushort ContinueMove(IList<PVTPoint> points, bool isFinal)
        {
            GlobalCommData.ShowLog(_aName, "当前轴未实现 PVT 续喂", MessageLevel.Warning);
            return PVTResult.NotSupported;
        }

        /// <summary>查询执行进度与剩余缓冲区</summary>
        /// <param name="info">未重写时恒返回空信息</param>
        /// <returns>未重写时恒返回不支持码</returns>
        public virtual ushort GetInformation(out PVTInformation info)
        {
            info = new PVTInformation();
            GlobalCommData.ShowLog(_aName, "当前轴未实现 PVT 水位查询", MessageLevel.Warning);
            return PVTResult.NotSupported;
        }

        /// <summary>启动循环模式，点表执行完后自动回到起点重放</summary>
        /// <returns>未重写时恒返回不支持码</returns>
        public virtual ushort CycleStart()
        {
            GlobalCommData.ShowLog(_aName, "当前轴未实现 PVT 循环启动", MessageLevel.Warning);
            return PVTResult.NotSupported;
        }

        #endregion
    }
}
