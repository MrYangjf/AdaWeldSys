using System;
using AdaWeldSystem.LineLaserCam.ILineLaser;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>线激光引导样本：结果、坐标与时间戳整帧绑定。</summary>
    /// <remarks>
    /// 线激光前置在焊枪前约 50mm。
    /// 同帧结果与坐标经本结构绑定。
    /// 由 <see cref="SeamTrackingAlgorithm"/> 按时间戳做轨迹计算。
    /// 滞后对齐同样由该算法完成。
    /// 轴当前位置只用于偏差叠加。
    /// 运动目标 = 轴当前值 + 带方向偏差。
    /// </remarks>
    public struct SeamGuideSample
    {
        #region 公共变量

        /// <summary>样本产生时间（本机时间）</summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>机器人当前 X 坐标（真实坐标，毫米）</summary>
        public double RobotX { get; private set; }

        /// <summary>引导轴当前位置（工程单位，毫米）</summary>
        public double AxisCurrentPos { get; private set; }

        /// <summary>线激光焊缝识别结果（厂商无关的中性结构）</summary>
        public LineLaserSeamResult Result { get; private set; }

        /// <summary>相机实测帧率（Hz），不可用时为 0</summary>
        public double MeasuredFps { get; private set; }

        /// <summary>本帧是否存在有效识别结果</summary>
        public bool Valid { get { return Result.Valid; } }

        /// <summary>焊缝特征点横向坐标（毫米，线激光坐标）</summary>
        public double FeatureY { get { return Result.FeatureY; } }

        /// <summary>焊缝特征点高度坐标（毫米，线激光坐标）</summary>
        public double FeatureZ { get { return Result.FeatureZ; } }

        #endregion

        #region 构造函数

        /// <summary>构造一个线激光引导样本</summary>
        /// <param name="timestamp">样本产生时间</param>
        /// <param name="robotX">机器人当前 X 坐标（毫米）</param>
        /// <param name="axisCurrentPos">引导轴当前位置（毫米）</param>
        /// <param name="result">线激光焊缝识别结果</param>
        /// <param name="measuredFps">实测帧率（Hz）</param>
        public SeamGuideSample(DateTime timestamp, double robotX, double axisCurrentPos,
            LineLaserSeamResult result, double measuredFps)
        {
            Timestamp = timestamp;
            RobotX = robotX;
            AxisCurrentPos = axisCurrentPos;
            Result = result;
            MeasuredFps = measuredFps;
        }

        #endregion
    }
}
