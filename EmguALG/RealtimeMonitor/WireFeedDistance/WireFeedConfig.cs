namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance
{
    /// <summary>送丝距离检测配置</summary>
    public class WireFeedConfig
    {
        /// <summary>水平方向容差（像素）</summary>
        public double ToleranceX { get; set; }

        /// <summary>垂直方向容差（像素）</summary>
        public double ToleranceY { get; set; }

        /// <summary>Y 向三级判定警告阈值（mm），超过进提示档</summary>
        public double CenterWarnMm { get; set; }

        /// <summary>Y 向（横向）三级判定报警阈值（mm）：超过即错误报警</summary>
        public double CenterFailMm { get; set; }

        /// <summary>像素→mm 标定系数</summary>
        public double PixelMmScale { get; set; }

        /// <summary>光斑标定基准 X（像素），即视野内标定位置</summary>
        public double SpotRefX { get; set; }

        /// <summary>光斑标定基准 Y（像素）</summary>
        public double SpotRefY { get; set; }

        /// <summary>焊丝标定目标伸出长度（mm，标定精度 ±0.02mm）</summary>
        public double WireStickoutTargetMm { get; set; }

        /// <summary>送丝方向是否对应图像水平轴</summary>
        public bool StickoutAxisHorizontal { get; set; }

        /// <summary>送丝方向符号（+1/-1），用于换算偏差</summary>
        public int StickoutSign { get; set; }

        /// <summary>X 向送丝补偿增益（mm 偏差→补偿比例）</summary>
        public double WireFeedCompGain { get; set; }

        /// <summary>焊丝异常是否自动下发剪丝指令（键 WIRE_ANOMALY_CUTWIRE）</summary>
        public bool WireAnomalyCutWire { get; set; }

        /// <summary>构造时填入默认值</summary>
        public WireFeedConfig()
        {
            ToleranceX = 8;
            ToleranceY = 8;
            CenterWarnMm = 0.1;
            CenterFailMm = 0.3;
            PixelMmScale = 0.05;
            SpotRefX = 0.0;
            SpotRefY = 0.0;
            WireStickoutTargetMm = 0.0;
            StickoutAxisHorizontal = true;
            StickoutSign = 1;
            WireFeedCompGain = 1.0;
            WireAnomalyCutWire = true;
        }
    }
}
