using Emgu.CV;
using System;

namespace AdaWeldSystem.MonitorCam.IMonitorCam
{
    /// <summary>
    /// 监控相机健康监督状态变更事件参数。
    /// 只描述相机自身状态，不含任何流程态字段（流程态由工作流基类 StateChanged 承载）。
    /// </summary>
    public class MonitorSupervisionStatusChangedEventArgs : EventArgs
    {
        /// <summary>健康监督状态</summary>
        public MonitorSupervisionState State { get; set; }

        /// <summary>相机是否正在正常工作（已连接且近期有有效采集）</summary>
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
    /// 监控相机帧数据事件参数（实现层 → 业务层，Live 模式实时流）
    /// </summary>
    public class MonitorFrameEventArgs : EventArgs
    {
        /// <summary>采集到的图像帧（已 Clone，调用方持有独立副本）</summary>
        public Mat Frame { get; set; }

        /// <summary>帧序号（从 0 递增）</summary>
        public int FrameIndex { get; set; }
    }

    /// <summary>
    /// 监控相机帧采集完成事件参数（业务层 → 工作流/UI 订阅）
    /// </summary>
    public class MonitorCameraFrameCompletedEventArgs : EventArgs
    {
        /// <summary>采集到的图像帧</summary>
        public Mat Frame { get; set; }

        /// <summary>本帧所属检测阶段</summary>
        public MonitorCameraPhase Phase { get; set; }

        /// <summary>采集是否成功</summary>
        public bool Success { get; set; }

        /// <summary>是否 Live 模式产出（false 为 PIL 单次触发产出）</summary>
        public bool IsLive { get; set; }

        /// <summary>产出时间戳</summary>
        public DateTime Timestamp { get; set; }
    }
}
