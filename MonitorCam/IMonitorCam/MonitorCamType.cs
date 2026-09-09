using System;

namespace AdaWeldSystem.MonitorCam.IMonitorCam
{
    /// <summary>
    /// 监控相机连接状态（相机自身状态，与设备流程态无关）
    /// </summary>
    public enum MonitorCameraConnectionState
    {
        /// <summary>未连接</summary>
        Disconnected,

        /// <summary>正在连接</summary>
        Connecting,

        /// <summary>已连接</summary>
        Connected,

        /// <summary>连接异常</summary>
        Error
    }

    /// <summary>
    /// 监控相机检测阶段（决定帧回调携带的阶段语义）
    /// </summary>
    public enum MonitorCameraPhase
    {
        /// <summary>未知阶段</summary>
        Unknown,

        /// <summary>焊前对中检测</summary>
        PreWeldAlignment,

        /// <summary>焊中质量检测</summary>
        WeldQuality
    }

    /// <summary>
    /// 监控相机健康监督状态（相机自身产出，非流程态）
    /// </summary>
    public enum MonitorSupervisionState
    {
        /// <summary>未启动监督</summary>
        Idle,

        /// <summary>巡检中</summary>
        Checking,

        /// <summary>相机已连接且持续产出有效帧</summary>
        Healthy,

        /// <summary>相机已连接但近期无成功采集</summary>
        Degraded,

        /// <summary>相机未连接或采集异常</summary>
        Error
    }
}
