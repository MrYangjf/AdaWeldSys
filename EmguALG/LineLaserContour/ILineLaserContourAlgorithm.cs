using Emgu.CV;
using System;

namespace AdaWeldSystem.EmguALG.LineLaserContour
{
    /// <summary>线激光轮廓 Mat 渲染算法抽象。 输入为算法层轮廓、标</summary>
    public interface ILineLaserContourAlgorithm : IDisposable
    {
        /// <summary>最近一次渲染结果，调用方须自行 Clone 后使用</summary>
        Mat LastMat { get; }

        /// <summary>把一帧轮廓渲染为 Mat。</summary>
        /// <param name="profile">轮廓点集，为空时只清屏</param>
        /// <param name="calib">标定数据，HasData 为 false 时按轮廓范围自动缩放</param>
        /// <param name="result">焊缝特征点，有效时绘制特征点</param>
        /// <returns>渲染结果，调用方须 Clone 后再跨线程使用</returns>
        Mat Render(LineLaserProfileData profile, LineLaserCalibrationData calib, LineLaserSeamFeature result);
    }
}
