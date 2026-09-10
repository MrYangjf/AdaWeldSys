namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance
{
    /// <summary>
    /// 送丝距离检测配置（焊前对中/焊丝尖端相对参考点判定）。
    /// 持久化由算法 Manager 统一负责，本类只承载强类型字段。
    /// </summary>
    public class WireFeedConfig
    {
        /// <summary>水平方向容差（像素）</summary>
        public double ToleranceX { get; set; }

        /// <summary>垂直方向容差（像素）</summary>
        public double ToleranceY { get; set; }

        /// <summary>角度容差（度）</summary>
        public double ToleranceAngle { get; set; }

        /// <summary>
        /// 创建配置并填入默认值（ADR-002：构造函数内初始化）
        /// </summary>
        public WireFeedConfig()
        {
            ToleranceX = 8;
            ToleranceY = 8;
            ToleranceAngle = 2.0;
        }
    }
}
