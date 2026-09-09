using Emgu.CV;
using System;

namespace AdaWeldSystem.MonitorCam.IMonitorCam
{
    /// <summary>
    /// 监控相机 SDK 抽象接口（抽象层，实现层与业务层之间的唯一契约）。
    /// 实现层只负责「按契约取帧并把帧抛出去」，不承载阶段、健康巡检、配置等业务语义，
    /// 后者全部由 MonitorCamManager 承担（ADR-039 三层职责分工）。
    /// 当前实现：MecaVisionCam（麦格威 MecaVision，MVCAMSDK 原生动态加载）。
    /// </summary>
    public interface IMonitorCamApi
    {
        /// <summary>当前连接状态</summary>
        MonitorCameraConnectionState ConnectionState { get; }

        /// <summary>实时流（Live 模式）帧回调，由实现层在采集线程抛出</summary>
        event EventHandler<MonitorFrameEventArgs> FrameReceived;

        /// <summary>
        /// 连接相机
        /// </summary>
        /// <param name="ip">相机 IP（GigE 按 IP 匹配；USB3 相机可留空，取枚举首台）</param>
        /// <param name="port">相机端口（MecaVision 原生接口不使用，保留以兼容契约）</param>
        /// <returns>是否成功建立连接</returns>
        bool Connect(string ip, string port);

        /// <summary>断开连接并释放 SDK 资源</summary>
        void Disconnect();

        /// <summary>开始持续采集（Live 模式）</summary>
        void StartAcquisition();

        /// <summary>停止持续采集</summary>
        void StopAcquisition();

        /// <summary>
        /// 触发一次采集并同步返回帧（PIL 模式）
        /// </summary>
        /// <returns>采集到的图像帧（独立副本）；失败返回 null</returns>
        Mat CaptureSingleFrame();

        /// <summary>释放 SDK 资源</summary>
        void Dispose();
    }
}
