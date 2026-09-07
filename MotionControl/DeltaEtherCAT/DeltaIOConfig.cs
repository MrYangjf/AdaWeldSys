namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达单路 IO 配置</summary>
    /// <remarks>
    /// 台达 IO 按 站点号 + 槽号 + 位号 三级寻址，位号取值 0 到 15，对应 <see cref="IMotion.MontionIO.IONo"/>。
    /// 输入与输出分两张表存放，与 IORegistry 的输入输出分表结构对称，允许同名输入与输出共存。
    /// </remarks>
    public class DeltaIOConfig
    {
        #region 公共变量

        /// <summary>IO 名称，业务层按名称取 IO</summary>
        public string IOName { get; set; }

        /// <summary>站点号（NodeID）</summary>
        public ushort NodeId { get; set; }

        /// <summary>槽号（SlotNo）</summary>
        public ushort SlotNo { get; set; }

        /// <summary>位号（BitNo），取值 0 到 15</summary>
        public ushort BitNo { get; set; }

        /// <summary>是否启用，未启用的 IO 不参与初始化</summary>
        public bool Enabled { get; set; }

        /// <summary>备注，记录物理接线用途</summary>
        public string Remark { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建空 IO 配置</summary>
        public DeltaIOConfig()
        {
            IOName = string.Empty;
            Enabled = true;
            Remark = string.Empty;
        }

        /// <summary>创建 IO 配置</summary>
        /// <param name="ioName">IO 名称</param>
        /// <param name="nodeId">站点号</param>
        /// <param name="slotNo">槽号</param>
        /// <param name="bitNo">位号</param>
        public DeltaIOConfig(string ioName, ushort nodeId, ushort slotNo, ushort bitNo)
            : this()
        {
            IOName = ioName;
            NodeId = nodeId;
            SlotNo = slotNo;
            BitNo = bitNo;
        }

        #endregion
    }
}
