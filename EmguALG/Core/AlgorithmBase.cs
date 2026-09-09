using Emgu.CV;
using Emgu.CV.Util;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>
    /// 统一算法基类（算法架构设计 §4）。
    /// 封装预处理编排与图形算子封装，提供 mm↔px 变换、ROI/计时/异常与 Execute 模板方法，
    /// 以及 INI 契约（SaveToIni/LoadFromIni 由子类实现具体节）。
    /// 不直接包含任何业务语义（焊缝/接头/对中）；业务规则由领域算法在 ExecuteCore 中实现。
    /// </summary>
    public abstract class AlgorithmBase
    {
        #region 抽象契约

        /// <summary>算法名称（用于日志/事件）</summary>
        public abstract string AlgorithmName { get; }

        /// <summary>将本算法参数保存到 INI 对象（由子类实现具体节）</summary>
        public abstract void SaveToIni(INIFile ini);

        /// <summary>从 INI 对象加载本算法参数</summary>
        public abstract void LoadFromIni(INIFile ini);

        /// <summary>核心执行（领域相关，由子类实现）</summary>
        protected abstract AlgorithmResult ExecuteCore(Mat input);

        #endregion

        #region 预处理算子（配置驱动，转调 ImageAlgorithm）

        /// <summary>
        /// 配置驱动的预处理编排：滤波 → 形态学 → 边缘。
        /// 内部按 Operation 转调 ImageAlgorithm 的原子算子（不重复实现算子）。
        /// </summary>
        protected Mat Preprocess(Mat src, PreprocessConfig cfg)
        {
            Mat cur = src.Clone();
            if (cfg.Filter.Enabled)
            {
                switch (cfg.Filter.Operation)
                {
                    case FilterOperation.GaussianBlur:
                        cur = ImageAlgorithm.GaussianBlur(cur, cfg.Filter.Ksize, cfg.Filter.SigmaX);
                        break;
                    case FilterOperation.MedianBlur:
                        cur = ImageAlgorithm.MedianBlur(cur, cfg.Filter.Ksize);
                        break;
                    case FilterOperation.MeanBlur:
                        cur = ImageAlgorithm.MeanBlur(cur, cfg.Filter.Ksize);
                        break;
                    case FilterOperation.BilateralFilter:
                        cur = ImageAlgorithm.BilateralFilter(cur, 9, 75, 75);
                        break;
                    case FilterOperation.Sharpen:
                        cur = ImageAlgorithm.Sharpen(cur);
                        break;
                }
            }
            if (cfg.Morphology.Enabled)
            {
                switch (cfg.Morphology.Operation)
                {
                    case MorphologyOperation.Dilate:
                        cur = ImageAlgorithm.Dilate(cur, cfg.Morphology.Ksize);
                        break;
                    case MorphologyOperation.Erode:
                        cur = ImageAlgorithm.Erode(cur, cfg.Morphology.Ksize);
                        break;
                    case MorphologyOperation.Open:
                        cur = ImageAlgorithm.MorphologyOpen(cur, cfg.Morphology.Ksize);
                        break;
                    case MorphologyOperation.Close:
                        cur = ImageAlgorithm.MorphologyClose(cur, cfg.Morphology.Ksize);
                        break;
                }
            }
            if (cfg.Edge.Enabled)
            {
                switch (cfg.Edge.Operation)
                {
                    case EdgeDetectionOperation.Canny:
                        cur = ImageAlgorithm.CannyEdge(cur, cfg.Edge.Threshold1, cfg.Edge.Threshold2, 3);
                        break;
                    case EdgeDetectionOperation.Gradient:
                        cur = ImageAlgorithm.GradientEdge(cur, 3);
                        break;
                    case EdgeDetectionOperation.Laplacian:
                        cur = ImageAlgorithm.LaplacianEdge(cur, 3);
                        break;
                    case EdgeDetectionOperation.Roberts:
                        cur = ImageAlgorithm.RobertsEdge(cur);
                        break;
                }
            }
            return cur;
        }

        #endregion

        #region 图形算子封装

        protected VectorOfVectorOfPoint FindContours(Mat bin, double thr = 127)
        {
            return ImageAlgorithm.FindContours(bin, thr);
        }

        #endregion

        #region Execute 模板方法（统一计时/异常/结果壳）

        public AlgorithmResult Execute(Mat input)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            AlgorithmResult result;
            try
            {
                result = ExecuteCore(input);
                if (result == null) result = new AlgorithmResult();
            }
            catch (System.Exception ex)
            {
                result = new AlgorithmResult();
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                sw.Stop();
            }
            result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            result.Algorithm = AlgorithmName;
            return result;
        }

        #endregion
    }
}
