﻿namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达单轴配置（按台达原生参数结构组织）</summary>
    /// <remarks>台达按卡号+站点+槽号三级寻址，故用 NodeId 与 SlotNo；
    /// 速度/回零对齐 CSP_Start_Move、Home_Config 原生结构，
    /// 与雷赛那套 MinVel/HomeDir 结构不同。Equiv 为工程单位/脉冲，换算只在实现层完成。</remarks>
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

        /// <summary>脉冲当量（工程单位/脉冲），换算只在 L3 完成</summary>
        public double Equiv { get; set; }

        /// <summary>起始速度（工程单位/秒），对应台达 CSP StrVel</summary>
        public double StrVel { get; set; }

        /// <summary>匀速（工程单位/秒），对应台达 CSP ConstVel</summary>
        public double ConstVel { get; set; }

        /// <summary>末速度（工程单位/秒），对应台达 CSP EndVel</summary>
        public double EndVel { get; set; }

        /// <summary>加速时间（秒）</summary>
        public double AccTime { get; set; }

        /// <summary>减速时间（秒）</summary>
        public double DecTime { get; set; }

        /// <summary>S 曲线时间（毫秒）</summary>
        public double STime { get; set; }

        /// <summary>回零模式 16#6098；负值(-4~-1)为扭矩回零，正值走 Home_Config</summary>
        public short HomeMode { get; set; }

        /// <summary>原点偏移（工程单位），对应台达 Home_Config Offset</summary>
        public double HomeOffset { get; set; }

        /// <summary>寻找速度（rpm，台达伺服口径），对应 Home_Config FirstVel</summary>
        public double HomeFirstVel { get; set; }

        /// <summary>爬行速度（rpm，台达伺服口径），对应台达 Home_Config SecondVel</summary>
        public double HomeSecondVel { get; set; }

        /// <summary>回零加减速时间（毫秒），对应台达 Home_Config Acceleration</summary>
        public double HomeAcc { get; set; }

        /// <summary>正向软限位（工程单位），0 表示不启用</summary>
        public double PositiveLimit { get; set; }

        /// <summary>负向软限位（工程单位），0 表示不启用</summary>
        public double NegativeLimit { get; set; }

        /// <summary>软限位使能模式，对应台达 CSP_Set_Softlimit Mode</summary>
        /// <remarks>⛔ 无下发路径：仅随 XML/CSV 存储，不送驱动器。</remarks>
        public ushort SoftLimitMode { get; set; }

        /// <summary>是否启用 CST 力矩模式（送丝下压力控制；false 走常规 CSP 位置模式）</summary>
        public bool TorqueModeEnabled { get; set; }

        /// <summary>
        /// 目标力矩/下压力（已弃用，勿在新代码中消费）。
        /// </summary>
        /// <remarks>
        /// 下压力归 WeldAlgorithmsConfig.TaForceN。本字段仅用于迁移读取与 CSV 兼容。
        /// TA_Auto 不再读。未用 Obsolete 标注：本文件无 using System。
        /// </remarks>
        public short TorqueTarget { get; set; }

        /// <summary>扭矩爬升速率（0.1%/s），对应台达 CST_Start_Move Slope</summary>
        /// <remarks>TA 唯一斜率：冷/热切换与正反向调力都用本值。
        /// ⚠️ 斜率只管力建立快慢，⛔ 不限速、治不了跑飞（见 ADR-088）。</remarks>
        public uint TorqueSlope { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建空轴配置</summary>
        public DeltaAxisConfig()
        {
            AxisName = string.Empty;
            Enabled = true;
            Equiv = 0.001;
            ConstVel = 100.0;
            HomeFirstVel = 20.0;
            HomeSecondVel = 5.0;
            HomeAcc = 100.0;
            TorqueModeEnabled = false;
            TorqueTarget = 0;
            TorqueSlope = 0;
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
