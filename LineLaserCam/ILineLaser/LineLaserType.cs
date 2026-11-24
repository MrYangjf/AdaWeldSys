﻿namespace AdaWeldSystem.LineLaserCam.ILineLaser
{
    /// <summary>线激光相机种类</summary>
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
    public enum LineLaserConnectionState
    {
        /// <summary>未连接（无相机或连接失败）</summary>
        Disconnected,

        /// <summary>已连接，空闲待命</summary>
        Connected,

        /// <summary>已连接且正在采集/出图中（工作流处于采集态）</summary>
        Working
    }

    /// <summary>线激光失败语境（决定 DescribeLastError 返回的描述种类）</summary>
    public enum LineLaserErrorContext
    {
        /// <summary>连接失败（SDK 连接返回码 / 服务位掩码）</summary>
        Connection,

        /// <summary>初始化失败（SDK 初始化或状态校验）</summary>
        Initialization,

        /// <summary>识别失败（算法解析结果，读 ParseRes / ErrorCode）</summary>
        Recognition,

        /// <summary>指令返回码（切 JOB、建 JOB、删 JOB、参数下发等）</summary>
        ReturnCode
    }
}
