namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WeldQuality
{
    /// <summary>
    /// 焊接质量检测配置（成形质量合格判定阈值）。
    /// </summary>
    public class WeldQualityConfig
    {
        /// <summary>合格分数阈值（0~100）</summary>
        public double PassScore { get; set; }

        /// <summary>
        /// 创建配置并填入默认值（ADR-002：构造函数内初始化）
        /// </summary>
        public WeldQualityConfig()
        {
            PassScore = 80;
        }
    }
}
