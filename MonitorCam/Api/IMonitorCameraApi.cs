using System;
using Emgu.CV;

namespace AdaWeldSystem.MonitorCam.Api
{
    /// <summary>
    /// 监控相机连接状态
    /// </summary>
    public enum MonitorCameraConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    /// <summary>
    /// 监控相机检测阶段
    /// </summary>
    public enum MonitorCameraPhase
    {
        Unknown,
        PreWeldAlignment,   // 焊前对中检测
        WeldQuality         // 焊中质量检测
    }

    /// <summary>
    /// 监控相机健康监督状态（相机自身产出，非流程态）
    /// </summary>
    public enum MonitorSupervisionState
    {
        Idle,       // 未启动监督
        Checking,   // 巡检中
        Healthy,    // 相机已连接且持续产出有效帧
        Degraded,   // 相机已连接但近期无成功采集
        Error       // 相机未连接或采集异常
    }

    /// <summary>
    /// 监控相机健康监督状态变更事件参数。
    /// 只描述相机自身状态，不含任何流程态字段（流程态由工作流基类 StateChanged 承载）。
    /// </summary>
    public class MonitorSupervisionStatusChangedEventArgs : EventArgs
    {
        public MonitorSupervisionState State { get; set; }
        public bool IsWorking { get; set; }
        public MonitorCameraPhase Phase { get; set; }
        public MonitorCameraConnectionState ConnectionState { get; set; }
        public DateTime LastHealthyTime { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 监控相机帧数据事件参数（Live 模式实时流）
    /// </summary>
    public class MonitorFrameEventArgs : EventArgs
    {
        /// <summary>采集到的图像帧</summary>
        public Mat Frame { get; set; }

        /// <summary>帧序号（从 0 递增）</summary>
        public int FrameIndex { get; set; }
    }

    /// <summary>
    /// 监控相机 SDK 抽象接口
    /// 设计参考 SmartRay Sensor API 风格（连接 / 采集 / Live-PIL 模式 / 帧回调）。
    /// 当前实现为 HikvisionCameraApi（海康面阵相机，MvCameraControl.Net SDK），满足本契约；
    /// 后续如需更换相机厂商，新增实现本接口的类并在 MonitorCameraRun 中替换即可，业务代码无需改动。
    /// </summary>
    public interface IMonitorCameraApi
    {
        /// <summary>当前连接状态</summary>
        MonitorCameraConnectionState ConnectionState { get; }

        /// <summary>实时流（Live 模式）帧回调</summary>
        event EventHandler<MonitorFrameEventArgs> FrameReceived;

        /// <summary>
        /// 连接相机
        /// </summary>
        /// <param name="ip">相机 IP</param>
        /// <param name="port">相机端口</param>
        /// <returns>是否成功发起连接</returns>
        bool Connect(string ip, string port);

        /// <summary>断开连接</summary>
        void Disconnect();

        /// <summary>开始持续采集（Live 模式）</summary>
        void StartAcquisition();

        /// <summary>停止持续采集</summary>
        void StopAcquisition();

        /// <summary>
        /// 触发一次采集并同步返回帧（PIL 模式）
        /// </summary>
        /// <returns>采集到的图像帧；失败返回 null</returns>
        Mat CaptureSingleFrame();

        /// <summary>释放 SDK 资源</summary>
        void Dispose();
    }
}
