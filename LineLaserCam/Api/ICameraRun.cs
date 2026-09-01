using System;
using Emgu.CV;
using System.Threading;

namespace AdaWeldSystem.LineLaserCamApi
{
    /// <summary>
    /// 线激光相机连接状态（与工作流程状态相互独立）。
    /// 相机自身只关心「是否已连上硬件 / 是否正在出图」，不表达工作流的运行/停止语义。
    /// </summary>
    public enum LineLaserCameraConnectionState
    {
        /// <summary>未连接（无相机或连接失败）</summary>
        Disconnected,
        /// <summary>已连接，空闲待命</summary>
        Connected,
        /// <summary>已连接且正在采集/出图中（工作流处于采集态）</summary>
        Working
    }

    /// <summary>
    /// 线激光相机种类（用于启动检测与状态显示）。
    /// 注意：当前工作流实际只接入 Virtual（虚拟）与 SmartRay（真实）两类；
    /// Hik 仅做启动连通性检测与状态显示，尚未接入工作流采集。
    /// </summary>
    public enum CameraKind
    {
        /// <summary>未配置相机</summary>
        None = 0,
        /// <summary>SmartRay 工业 3D 线激光（真实）</summary>
        SmartRay = 1,
        /// <summary>海康机器人 3D 线激光（真实）</summary>
        Hik = 2,
        /// <summary>虚拟调试相机（仅模拟测试启用，启动不检测）</summary>
        Virtual = 3,
        /// <summary>英莱(IntelligentLaser) 工业 3D 线激光（真实）</summary>
        IntelligentLaser = 4
    }

    /// <summary>
    /// 相机运行抽象接口。
    /// 使真实 SmartRay 相机（CameraRun）与虚拟调试相机（VirtualCameraRun）可互换，
    /// 工作流（LineLaserWorkflow / MotionControlWorkflow 取图）仅依赖此接口，不关心具体相机实现。
    /// </summary>
    public interface ICameraRun : IDisposable
    {
        /// <summary>采集完成信号（Workflow 等待此信号后读取 CurrentMat）</summary>
        AutoResetEvent AcquisitionCompletedSignal { get; }

        /// <summary>相机是否正在运行</summary>
        bool IsRunning { get; }

        /// <summary>最近一次采集是否成功</summary>
        bool LastAcquisitionSuccess { get; }

        /// <summary>最近一次采集到的图像（Mat）</summary>
        Mat CurrentMat { get; }

        /// <summary>触发连续采集</summary>
        void SensorRunContinue();

        /// <summary>停止采集</summary>
        void SensorStop();

        /// <summary>
        /// 当前采集扫描率（Hz）。工作流据此按帧周期缩放 WaitOne 超时：
        /// 120Hz 相机→数百 ms 紧超时（卡帧快速检测）；低速相机→较宽松超时（避免误判）。
        /// 虚拟相机为同步产帧，可达 120Hz。
        /// </summary>
        double ScanRateHz { get; }
    }
}
