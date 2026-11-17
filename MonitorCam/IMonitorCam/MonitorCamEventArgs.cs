using Emgu.CV;
using System;

namespace AdaWeldSystem.MonitorCam.IMonitorCam
{
    /// <summary>
    /// 监控相机健康监督状态变更事件参数。
    /// 只描述相机状态，流程态不在此。
    /// </summary>
    public class MonitorSupervisionStatusChangedEventArgs : EventArgs
    {
        /// <summary>健康监督状态</summary>
        public MonitorSupervisionState State { get; set; }

        /// <summary>相机是否已连接且近期有有效采集</summary>
        public bool IsWorking { get; set; }

        /// <summary>当前检测阶段</summary>
        public MonitorCameraPhase Phase { get; set; }

        /// <summary>当前连接状态</summary>
        public MonitorCameraConnectionState ConnectionState { get; set; }

        /// <summary>最近一次有效采集时间</summary>
        public DateTime LastHealthyTime { get; set; }

        /// <summary>状态变更说明</summary>
        public string Message { get; set; }

        /// <summary>状态变更时间戳</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 帧采集完成事件参数，供工作流订阅。
    /// 2D 面阵相机，无模式，帧均单次触发。
    /// </summary>
    public class MonitorCameraFrameCompletedEventArgs : EventArgs
    {
        /// <summary>采集到的图像帧</summary>
        public Mat Frame { get; set; }

        /// <summary>本帧所属检测阶段</summary>
        public MonitorCameraPhase Phase { get; set; }

        /// <summary>采集是否成功</summary>
        public bool Success { get; set; }

        /// <summary>产出时间戳</summary>
        public DateTime Timestamp { get; set; }
    }
}
