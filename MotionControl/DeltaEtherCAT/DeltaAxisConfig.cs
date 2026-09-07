namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达单轴配置</summary>
    /// <remarks>
    /// 台达按 卡号 + 站点号 + 槽号 三级寻址，没有统一轴号，故本配置不用 AxisNo 而用 NodeId 与 SlotNo。
    /// 位置与速度一律工程单位，脉冲当量 Equiv 表示工程单位/脉冲，换算只在 L3 实现层完成。
    /// </remarks>
    public class DeltaAxisConfig
    {
        #region 公共变量

        /// <summary>轴名称，业务层按名称取轴</summary>
        public string AxisName { get; set; }

        /// <summary>站点号（NodeID）</summary>
        public ushort NodeId { get; set; }

        /// <summary>槽号（SlotNo）</summary>
        public ushort SlotNo { get; set; }

        /// <summary>是否启用，未启用的轴不参与初始化与扫描校验</summary>
        public bool Enabled { get; set; }

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

        /// <summary>回零方式，取值即台达回零模式号</summary>
        public ushort HomeMode { get; set; }

        /// <summary>回零方向，0 为负向，1 为正向</summary>
        public ushort HomeDir { get; set; }

        /// <summary>回零速度（工程单位/秒）</summary>
        public double HomeMaxVel { get; set; }

        /// <summary>正向软限位（工程单位），0 表示不启用</summary>
        public double PositiveLimit { get; set; }

        /// <summary>负向软限位（工程单位），0 表示不启用</summary>
        public double NegativeLimit { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建空轴配置</summary>
        public DeltaAxisConfig()
        {
            AxisName = string.Empty;
            Enabled = true;
            Equiv = 0.001;
        }

        /// <summary>创建轴配置</summary>
        /// <param name="axisName">轴名称</param>
        /// <param name="nodeId">站点号</param>
        /// <param name="slotNo">槽号</param>
        public DeltaAxisConfig(string axisName, ushort nodeId, ushort slotNo)
            : this()
        {
            AxisName = axisName;
            NodeId = nodeId;
            SlotNo = slotNo;
        }

        #endregion
    }
}
