namespace AdaWeldSystem.EmguALG
{
    // 预处理算子域枚举（2026-09-09 自已删除的 EmguConfiger/AlgorithmManager.cs 迁入，ADR-038 收尾；ADR-017 单一定义点）。
    // 命名空间取 AdaWeldSystem.EmguALG：Stages（EmguALG）与 Core/*（EmguALG.Core）均可直接可见，零 using 变更。
    #region 算子枚举

    /// <summary>滤波算子枚举</summary>
    public enum FilterOperation
    {
        GaussianBlur,
        MedianBlur,
        MeanBlur,
        BilateralFilter,
        Sharpen
    }

    /// <summary>形态学算子枚举</summary>
    public enum MorphologyOperation
    {
        Dilate,
        Erode,
        Open,
        Close
    }

    /// <summary>边缘检测算子枚举</summary>
    public enum EdgeDetectionOperation
    {
        Canny,
        Gradient,
        Laplacian,
        Roberts
    }

    /// <summary>特征提取算子枚举</summary>
    public enum FeatureExtractionOperation
    {
        ContourExtraction,
        SkeletonExtraction
    }

    #endregion
}
