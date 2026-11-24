using AdaWeldSystem.EmguALG.Core;
using Emgu.CV;
using System.Drawing;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance
{
    /// <summary>光丝对中三级判定（安全 / 提示 / 报警）</summary>
    public enum WireAlignGrade
    {
        /// <summary>安全（偏差 ≤ 警告阈值）</summary>
        Pass = 0,

        /// <summary>提示（警告阈值 &lt; 偏差 ≤ 报警阈值，黄灯）</summary>
        Warn = 1,

        /// <summary>错误（偏差 &gt; 报警阈值或检测失败，红灯/报警）</summary>
        Fail = 2
    }

    /// <summary>送丝距离结果</summary>
    public class WireFeedResult : AlgorithmResult
    {
        /// <summary>水平方向偏差（像素）</summary>
        public double OffsetX { get; set; }

        /// <summary>垂直方向偏差（像素）</summary>
        public double OffsetY { get; set; }

        /// <summary>角度偏差（度，仅展示，不参与判定）</summary>
        public double AngleDeg { get; set; }

        /// <summary>是否在容差范围内（对中合格）</summary>
        public bool Aligned { get; set; }

        /// <summary>水平方向偏差（mm，像素×标定系数）</summary>
        public double DevXMm { get; set; }

        /// <summary>垂直方向偏差（mm）</summary>
        public double DevYMm { get; set; }

        /// <summary>送丝方向偏差（mm；正值=焊丝偏长）：供送丝指令补偿</summary>
        public double WireStickoutDevMm { get; set; }

        /// <summary>横向偏差（mm）：只做三级报警，不自动调整</summary>
        public double LateralDevMm { get; set; }

        /// <summary>三级判定（按横向偏差分级；检测失败判 Fail）</summary>
        public WireAlignGrade Grade { get; set; }

        /// <summary>焊丝是否识别（false 为断丝/送丝异常）</summary>
        public bool WireMatched { get; set; }

        /// <summary>焊丝头部是否粘连（光斑未识别即粘连）</summary>
        public bool WireHeadAdhesion { get; set; }

        /// <summary>参考点（激光光斑）中心，像素坐标</summary>
        public PointF LaserCenter { get; set; }

        /// <summary>焊丝中心/尖端，像素坐标</summary>
        public PointF WireCenter { get; set; }

        /// <summary>叠加了检测标注的图像（供 UI 显示）</summary>
        public Mat OverlayMat { get; set; }
    }
}
