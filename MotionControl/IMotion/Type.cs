namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>控制器类型</summary>
    public enum ControlType
    {
        /// <summary>总线控制器</summary>
        NmcController,
        /// <summary>脉冲控制器</summary>
        Controller,
        /// <summary>脉冲控制卡</summary>
        ControlCard,
        /// <summary>IO 控制卡</summary>
        IOCard,
        /// <summary>EtherCAT 主站卡</summary>
        EtherCATMaster
    }

    /// <summary>控制器厂家</summary>
    public enum ManufacturerType
    {
        /// <summary>雷赛</summary>
        Leadshine,
        /// <summary>凌华</summary>
        Adlink,
        /// <summary>固高</summary>
        Googoltech,
        /// <summary>台达</summary>
        Delta
    }

    /// <summary>IO 方向</summary>
    public enum IOType
    {
        /// <summary>输入</summary>
        Input,
        /// <summary>输出</summary>
        Output
    }

    /// <summary>伺服运动模式（取值即台达模式号）</summary>
    public enum MoveMode
    {
        /// <summary>轮廓位置模式</summary>
        PP = 1,
        /// <summary>轮廓速度模式</summary>
        PV = 3,
        /// <summary>回零模式</summary>
        Homing = 6,
        /// <summary>周期同步位置模式</summary>
        CSP = 8,
        /// <summary>周期同步速度模式</summary>
        CSV = 9,
        /// <summary>周期同步扭矩模式</summary>
        CST = 10
    }

    /// <summary>回零方式（取值即台达回零模式号）</summary>
    public enum HomingMode
    {
        /// <summary>一次回零</summary>
        Once = 0,
        /// <summary>一次回零加回找</summary>
        OnceAndBack = 1,
        /// <summary>二次回零</summary>
        Twice = 2,
        /// <summary>一次回零后记 EZ 信号</summary>
        OnceAndEz = 3,
        /// <summary>单 EZ 信号回零</summary>
        EzOnly = 4,
        /// <summary>一次回零后反向记 EZ 信号</summary>
        OnceAndReverseEz = 5
    }

    /// <summary>停止方式（对应 AxisStop 入参）</summary>
    public enum StopMode
    {
        /// <summary>减速停止</summary>
        Deceleration = 0,
        /// <summary>急停</summary>
        Emergency = 1
    }

    /// <summary>轴的离散状态</summary>
    public enum AxisState
    {
        /// <summary>未使能</summary>
        NotReady,
        /// <summary>使能就绪</summary>
        OperationEnabled,
        /// <summary>报警</summary>
        Fault,
        /// <summary>运动中</summary>
        Moving,
        /// <summary>回零完成</summary>
        HomeDone
    }

    /// <summary>插补类型</summary>
    public enum InterpolationType
    {
        /// <summary>直线插补</summary>
        Line,
        /// <summary>圆弧插补</summary>
        Arc
    }

    /// <summary>PVT 时间模式</summary>
    public enum PVTTimeMode
    {
        /// <summary>相对时间，每点时间为与前一点的间隔</summary>
        Relative = 0,
        /// <summary>绝对时间，每点时间为相对起点的累计值</summary>
        Absolute = 1
    }

    /// <summary>凸轮曲线段插值方式</summary>
    /// <remarks>取值语义 SDK 与手册均未明确记载，须现场实测后确认。</remarks>
    public enum EcamCurveType
    {
        /// <summary>直线段</summary>
        Linear = 0,
        /// <summary>曲线段</summary>
        Curve = 1
    }

    /// <summary>凸轮啮合启动方式</summary>
    public enum EcamStartMode
    {
        /// <summary>主轴正向跳变时啮合</summary>
        PositiveJump = 0,
        /// <summary>最短距离啮合</summary>
        Shortest = 1,
        /// <summary>正向啮合</summary>
        Positive = 2,
        /// <summary>反向啮合</summary>
        Negative = 3
    }

    /// <summary>凸轮缓冲模式</summary>
    public enum EcamBufferMode
    {
        /// <summary>打断当前运动立即啮合</summary>
        Aborting = 0,
        /// <summary>缓冲，当前运动结束后啮合</summary>
        Buffered = 1
    }
}
