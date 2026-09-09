using AdaWeldSystem.EmguALG.Core;
using Emgu.CV;
using System.Collections.Generic;

namespace AdaWeldSystem.EmguALG.WeldQuality
{
    /// <summary>
    /// 焊接质量检测结果：缺陷列表 + 质量分 + 合格判定。
    /// 派生自 AlgorithmResult 以统一成功标志与耗时（满足 IAlgorithm 泛型约束）。
    /// </summary>
    public class WeldQualityResult : AlgorithmResult
    {
        /// <summary>缺陷数量</summary>
        public int DefectCount { get; set; }

        /// <summary>缺陷明细</summary>
        public List<WeldDefect> Defects { get; set; }

        /// <summary>质量分（0~100）</summary>
        public double QualityScore { get; set; }

        /// <summary>是否合格</summary>
        public bool Pass { get; set; }

        /// <summary>叠加了缺陷标注的图像（供 UI 显示）</summary>
        public Mat OverlayMat { get; set; }

        /// <summary>
        /// 创建结果并初始化缺陷列表（ADR-002：构造函数内初始化）
        /// </summary>
        public WeldQualityResult()
        {
            Defects = new List<WeldDefect>();
        }
    }
}
