using System;
using AdaWeldSystem.PCLOperate.Models;
using Emgu.CV;

namespace AdaWeldSystem.LineLaserCam.ILineLaser
{
    /// <summary>线激光焊缝识别结果快照（厂商无关的中性结构）</summary>
    /// <remarks>
    /// 由厂商实现层（英莱）在 SDK 回调中把自身算法结果映射为本结构后外传，
    /// 业务层与工作流只认本结构，不感知厂商 SDK 类型（ADR-038 D1 铁律）。
    /// 反例：此前业务层直接消费英莱 <c>IlInspectResult</c>，导致线激光业务被单一厂商绑死。
    /// </remarks>
    public struct LineLaserSeamResult
    {
        #region 公共变量

        /// <summary>本帧是否存在有效识别结果</summary>
        public bool Valid { get; private set; }

        /// <summary>算法解析结果码，1 为解析成功</summary>
        public int ParseRes { get; private set; }

        /// <summary>算法错误码，0 为无错误</summary>
        public int ErrorCode { get; private set; }

        /// <summary>帧时间戳（厂商自定义单位）</summary>
        public ulong Timestamp { get; private set; }

        /// <summary>焊缝特征点横向坐标（毫米）</summary>
        public float FeatureY { get; private set; }

        /// <summary>焊缝特征点高度坐标（毫米）</summary>
        public float FeatureZ { get; private set; }

        /// <summary>焊缝宽度测量值一（毫米）</summary>
        public float Width0 { get; private set; }

        /// <summary>焊缝宽度测量值二（毫米）</summary>
        public float Width1 { get; private set; }

        /// <summary>焊缝高度或深度测量值一（毫米）</summary>
        public float Height0 { get; private set; }

        /// <summary>焊缝高度或深度测量值二（毫米）</summary>
        public float Height1 { get; private set; }

        /// <summary>焊缝截面积（平方毫米）</summary>
        public float Area { get; private set; }

        /// <summary>焊缝平均宽度（毫米）</summary>
        public double AverageWidth { get { return (Width0 + Width1) * 0.5; } }

        #endregion

        #region 公共函数

        /// <summary>创建焊缝识别结果快照</summary>
        /// <param name="valid">是否存在有效识别结果</param>
        /// <param name="parseRes">解析结果码</param>
        /// <param name="errorCode">错误码</param>
        /// <param name="timestamp">帧时间戳</param>
        /// <param name="featureY">特征点横向坐标</param>
        /// <param name="featureZ">特征点高度坐标</param>
        /// <param name="width0">宽度测量值一</param>
        /// <param name="width1">宽度测量值二</param>
        /// <param name="height0">高度测量值一</param>
        /// <param name="height1">高度测量值二</param>
        /// <param name="area">截面积</param>
        public LineLaserSeamResult(bool valid, int parseRes, int errorCode, ulong timestamp,
            float featureY, float featureZ, float width0, float width1,
            float height0, float height1, float area)
        {
            Valid = valid;
            ParseRes = parseRes;
            ErrorCode = errorCode;
            Timestamp = timestamp;
            FeatureY = featureY;
            FeatureZ = featureZ;
            Width0 = width0;
            Width1 = width1;
            Height0 = height0;
            Height1 = height1;
            Area = area;
        }

        /// <summary>创建无效结果快照，用于无识别的帧</summary>
        /// <param name="parseRes">解析结果码</param>
        /// <param name="errorCode">错误码</param>
        /// <returns>无效结果快照</returns>
        public static LineLaserSeamResult Invalid(int parseRes, int errorCode)
        {
            return new LineLaserSeamResult(false, parseRes, errorCode, 0UL, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        }

        #endregion
    }

    /// <summary>线激光识别结果就绪事件参数（结果处理链路）</summary>
    public class LineLaserResultEventArgs : EventArgs
    {
        #region 公共变量

        /// <summary>相机键名</summary>
        public string CameraName { get; private set; }

        /// <summary>本帧焊缝识别结果</summary>
        public LineLaserSeamResult Result { get; private set; }

        /// <summary>实测帧率（Hz），相机未统计时为 0</summary>
        public double MeasuredFps { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建识别结果就绪事件参数</summary>
        /// <param name="cameraName">相机键名</param>
        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="measuredFps">实测帧率</param>
        public LineLaserResultEventArgs(string cameraName, LineLaserSeamResult result, double measuredFps)
        {
            CameraName = cameraName;
            Result = result;
            MeasuredFps = measuredFps;
        }

        #endregion
    }

    /// <summary>线激光轮廓就绪事件参数（轮廓处理链路）</summary>
    public class LineLaserProfileEventArgs : EventArgs
    {
        #region 公共变量

        /// <summary>相机键名</summary>
        public string CameraName { get; private set; }

        /// <summary>本帧轮廓点云，单帧 X 恒为 0，Y 为横向、Z 为高度</summary>
        public PointCloudData Cloud { get; private set; }

        /// <summary>实测帧率（Hz），相机未统计时为 0</summary>
        public double MeasuredFps { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建轮廓就绪事件参数</summary>
        /// <param name="cameraName">相机键名</param>
        /// <param name="cloud">本帧轮廓点云</param>
        /// <param name="measuredFps">实测帧率</param>
        public LineLaserProfileEventArgs(string cameraName, PointCloudData cloud, double measuredFps)
        {
            CameraName = cameraName;
            Cloud = cloud;
            MeasuredFps = measuredFps;
        }

        #endregion
    }

    /// <summary>线激光轮廓 Mat 更新事件参数（显示链路）</summary>
    /// <remarks>帧实例与相机 <c>CurrentMat</c> 共享同一对象，订阅方须自行 Clone 后再跨帧持有。</remarks>
    public class LineLaserMatEventArgs : EventArgs
    {
        #region 公共变量

        /// <summary>相机键名</summary>
        public string CameraName { get; private set; }

        /// <summary>本帧轮廓图像</summary>
        public Mat Frame { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建轮廓 Mat 更新事件参数</summary>
        /// <param name="cameraName">相机键名</param>
        /// <param name="frame">本帧轮廓图像</param>
        public LineLaserMatEventArgs(string cameraName, Mat frame)
        {
            CameraName = cameraName;
            Frame = frame;
        }

        #endregion
    }

    /// <summary>线激光相机状态变化事件参数</summary>
    public class LineLaserStateEventArgs : EventArgs
    {
        #region 公共变量

        /// <summary>相机键名</summary>
        public string CameraName { get; private set; }

        /// <summary>是否已连接硬件</summary>
        public bool IsConnected { get; private set; }

        /// <summary>是否正在出图</summary>
        public bool IsRunning { get; private set; }

        /// <summary>相机自身连接态（不含工作流驱动的「工作中」）</summary>
        public LineLaserConnectionState ConnectionState { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建状态变化事件参数</summary>
        /// <param name="cameraName">相机键名</param>
        /// <param name="isConnected">是否已连接硬件</param>
        /// <param name="isRunning">是否正在出图</param>
        /// <param name="connectionState">相机自身连接态</param>
        public LineLaserStateEventArgs(string cameraName, bool isConnected, bool isRunning,
            LineLaserConnectionState connectionState)
        {
            CameraName = cameraName;
            IsConnected = isConnected;
            IsRunning = isRunning;
            ConnectionState = connectionState;
        }

        #endregion
    }
}
