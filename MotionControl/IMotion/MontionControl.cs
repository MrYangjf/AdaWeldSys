using System.Collections.Generic;

namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>运动控制器抽象基类</summary>
    /// <remarks>持有轴与 IO 集合，子类实现硬件初始化、关闭与急停。</remarks>
    public abstract class MontionControl
    {
        #region 私有变量

        private readonly string _deviceName;

        #endregion

        #region 公共变量

        /// <summary>连接号（台达映射为卡号）</summary>
        public ushort ConnectNo { get { return _connectNo; } }

        /// <summary>设备名称</summary>
        public string DeviceName { get { return _deviceName; } }

        /// <summary>是否已初始化</summary>
        public bool Initial { get { return _isInitial; } }

        /// <summary>全局急停状态，置位后所有运动入口短路</summary>
        public bool EmergencyStop { get; protected set; }

        #endregion

        #region 保护变量

        /// <summary>连接号</summary>
        protected ushort _connectNo;

        /// <summary>初始化完成标志</summary>
        protected bool _isInitial;

        /// <summary>轴集合</summary>
        protected readonly List<MontionAxis> _axes = new List<MontionAxis>();

        /// <summary>输入 IO 集合</summary>
        protected readonly List<InputIO> _inPuts = new List<InputIO>();

        /// <summary>输出 IO 集合</summary>
        protected readonly List<OutputIO> _outPuts = new List<OutputIO>();

        #endregion

        #region 构造函数

        /// <summary>创建控制器</summary>
        /// <param name="devName">设备名称</param>
        protected MontionControl(string devName)
        {
            _deviceName = devName;
        }

        #endregion

        #region 公共函数

        /// <summary>初始化控制器</summary>
        /// <param name="strMsg">失败时回写的错误描述</param>
        /// <returns>初始化成功返回 true</returns>
        public abstract bool Initialization(ref string strMsg);

        /// <summary>关闭控制器并释放总线</summary>
        public abstract void Close();

        /// <summary>注册轴</summary>
        /// <param name="axis">轴对象，重复注册将被忽略</param>
        public abstract void AddAxis(MontionAxis axis);

        /// <summary>注册 IO</summary>
        /// <param name="io">IO 对象，重复注册将被忽略</param>
        public abstract void AddIO(MontionIO io);

        /// <summary>获取全部轴</summary>
        /// <returns>轴集合</returns>
        public abstract List<MontionAxis> GetAxes();

        /// <summary>获取全部输入 IO</summary>
        /// <returns>输入 IO 集合</returns>
        public abstract List<InputIO> GetInPutIOs();

        /// <summary>获取全部输出 IO</summary>
        /// <returns>输出 IO 集合</returns>
        public abstract List<OutputIO> GetOutPutIOs();

        /// <summary>设置全局急停并广播到所有轴</summary>
        /// <param name="isEmergencyStop">true 为急停</param>
        public abstract void SetEmergencyStop(bool isEmergencyStop);

        /// <summary>获取控制器类型</summary>
        /// <returns>控制器类型</returns>
        public abstract ControlType GetControlType();

        /// <summary>获取控制器厂家</summary>
        /// <returns>厂家类型</returns>
        public abstract ManufacturerType GetManufacturerType();

        /// <summary>检查总线是否全部就绪（供状态监督轮询调用，须非阻塞）。</summary>
        /// <remarks>virtual 默认实现（同 IPVTMotion 契约范式）：未覆写的派生类视为「不支持总线检查」，返回 false 并给出描述。</remarks>
        /// <param name="faultDesc">故障描述；总线健康时为空串</param>
        /// <returns>总线健康返回 true</returns>
        public virtual bool CheckBusOK(out string faultDesc)
        {
            faultDesc = "控制器未实现总线检查";
            return false;
        }

        #endregion
    }
}
