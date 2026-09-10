using AdaWeldSystem.LineLaserCam.Data;
using Emgu.CV;
using System;

namespace AdaWeldSystem.EmguALG.LineLaserContour
{
    /// <summary>
    /// 线激光轮廓 Mat 渲染算法抽象。
    /// 输入为线激光相机输出的轮廓点云与校准/识别结果，输出为供 UI 显示的 Emgu Mat。
    /// 实现须保证输出 Mat 可被调用方安全 Clone 后跨线程使用。
    /// </summary>
    public interface ILineLaserContourAlgorithm : IDisposable
    {
        /// <summary>最近一次渲染结果，调用方须自行 Clone 后使用</summary>
        Mat LastMat { get; }

        /// <summary>
        /// 把一帧轮廓渲染为 Mat。
        /// </summary>
        /// <param name="cloud">轮廓点云，为空时只清屏</param>
        /// <param name="calib">校准数据，HasData 为 false 时按轮廓范围自动缩放</param>
        /// <param name="result">焊缝识别结果，有效时绘制特征点</param>
        /// <returns>渲染结果，调用方须 Clone 后再跨线程使用</returns>
        Mat Render(PointCloudData cloud, LineLaserCalibration calib, LineLaserSeamResult result);
    }
}
