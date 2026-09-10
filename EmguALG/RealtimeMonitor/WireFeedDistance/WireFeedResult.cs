using AdaWeldSystem.EmguALG.Core;
using Emgu.CV;
using System.Drawing;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance
{
    /// <summary>
    /// 送丝距离检测结果：焊丝尖端相对参考点的像素偏差与是否对中。
    /// 派生自 AlgorithmResult 以统一成功标志与耗时（满足 IAlgorithm 泛型约束）。
    /// </summary>
    public class WireFeedResult : AlgorithmResult
    {
        /// <summary>水平方向偏差（像素）</summary>
        public double OffsetX { get; set; }

        /// <summary>垂直方向偏差（像素）</summary>
        public double OffsetY { get; set; }

        /// <summary>角度偏差（度）</summary>
        public double AngleDeg { get; set; }

        /// <summary>是否在容差范围内（对中合格）</summary>
        public bool Aligned { get; set; }

        /// <summary>参考点（激光光斑）中心，像素坐标</summary>
        public PointF LaserCenter { get; set; }

        /// <summary>焊丝中心/尖端，像素坐标</summary>
        public PointF WireCenter { get; set; }

        /// <summary>叠加了检测标注的图像（供 UI 显示）</summary>
        public Mat OverlayMat { get; set; }
    }
}
