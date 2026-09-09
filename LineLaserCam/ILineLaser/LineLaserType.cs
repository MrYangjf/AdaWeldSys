namespace AdaWeldSystem.LineLaserCam.ILineLaser
{
    /// <summary>线激光相机种类</summary>
    /// <remarks>
    /// 2026-09-08 重构：SmartRay 与海康机器人 3D 两套实现已从代码库移除，
    /// 线激光族只保留英莱（真实）与虚拟（调试）两类。
    /// </remarks>
    public enum LineLaserKind
    {
        /// <summary>未配置相机</summary>
        None = 0,

        /// <summary>英莱(IntelligentLaser) 工业 3D 线激光，真实相机</summary>
        IntelligentLaser = 1,

        /// <summary>虚拟调试相机，仅模拟测试启用，启动不检测</summary>
        Virtual = 2
    }

    /// <summary>线激光相机连接状态</summary>
    /// <remarks>
    /// 相机自身只关心「是否已连上硬件 / 是否正在出图」，
    /// 与工作流程状态相互独立（[[decisions/ADR-013-CameraConnectionStateDecoupled]]）。
    /// </remarks>
    public enum LineLaserConnectionState
    {
        /// <summary>未连接（无相机或连接失败）</summary>
        Disconnected,

        /// <summary>已连接，空闲待命</summary>
        Connected,

        /// <summary>已连接且正在采集/出图中（工作流处于采集态）</summary>
        Working
    }
}
