namespace AdaWeldSystem.EmguALG.Core
{
    // 预处理算子域枚举（统一在此定义；命名空间使 Stages 与 Core/* 均直接可见）
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
