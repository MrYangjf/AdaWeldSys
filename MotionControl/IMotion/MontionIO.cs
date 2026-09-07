namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>IO 抽象基类</summary>
    /// <remarks>台达 IO 为 NPN 低电平有效，状态值 0 表示有效。</remarks>
    public abstract class MontionIO
    {
        #region 私有变量

        private readonly string _ioName;

        #endregion

        #region 公共变量

        /// <summary>所属控制器</summary>
        public MontionControl Container { get; protected set; }

        /// <summary>IO 名称</summary>
        public string IOName { get { return _ioName; } }

        /// <summary>位号，取值 0 到 15</summary>
        public ushort IONo { get; set; }

        /// <summary>是否启用，未启用读取恒为无效电平</summary>
        public bool IOEnable { get; set; }

        #endregion

        #region 保护变量

        /// <summary>连接号</summary>
        protected ushort _connectNo;

        #endregion

        #region 构造函数

        /// <summary>创建 IO 并注册到所属控制器</summary>
        /// <param name="container">所属控制器</param>
        /// <param name="ioName">IO 名称</param>
        protected MontionIO(MontionControl container, string ioName)
        {
            Container = container;
            _ioName = ioName;
            _connectNo = container.ConnectNo;
            IOEnable = true;
            container.AddIO(this);
        }

        #endregion

        #region 公共函数

        /// <summary>判断 IO 是否有效</summary>
        /// <returns>有效返回 true</returns>
        public abstract bool IsON();

        /// <summary>读取 IO 原始状态</summary>
        /// <returns>0 为有效，1 为无效，其他为异常</returns>
        public abstract int GetIOStatus();

        /// <summary>获取 IO 方向</summary>
        /// <returns>输入或输出</returns>
        public abstract IOType GetIOType();

        #endregion
    }
}
