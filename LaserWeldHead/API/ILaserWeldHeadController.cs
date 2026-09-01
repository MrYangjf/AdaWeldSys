using System;

namespace AdaWeldSystem.LaserWeldHead.API
{
    /// <summary>
    /// 激光焊接头控制器接口
    /// 定义激光焊接头的统一控制契约：激光器状态、温度监视、自动对焦、安全 IO。
    /// </summary>
    public interface ILaserWeldHeadController : IDisposable
    {
        /// <summary>是否已初始化</summary>
        bool IsInitialized { get; }

        /// <summary>激光器状态（是否就绪）</summary>
        bool IsLaserReady { get; }

        /// <summary>当前温度数据</summary>
        LaserHeadTemperatureData CurrentTemperature { get; }

        /// <summary>当前安全 IO 状态</summary>
        SafetyIOStatus CurrentSafetyIO { get; }

        /// <summary>当前自动对焦位置数据</summary>
        AutoFocusPositionData CurrentAutoFocusPosition { get; }

        /// <summary>温度超阈值事件</summary>
        event EventHandler<LaserHeadTemperatureData> TemperatureAlarm;

        /// <summary>安全 IO 报警事件</summary>
        event EventHandler<SafetyIOStatus> SafetyIOAlarm;

        /// <summary>自动对焦位置变更事件</summary>
        event EventHandler<AutoFocusPositionData> AutoFocusPositionChanged;

        /// <summary>初始化焊接头（激光器握手、温度模块枚举、IO 映射）</summary>
        bool Initialize();

        /// <summary>关闭焊接头</summary>
        void Shutdown();

        /// <summary>读取温度数据</summary>
        LaserHeadTemperatureData ReadTemperature();

        /// <summary>读取安全 IO 状态</summary>
        SafetyIOStatus ReadSafetyIO();

        /// <summary>
        /// 设置自动对焦电机位置
        /// </summary>
        /// <param name="position">目标位置（mm）</param>
        bool SetAutoFocusPosition(double position);

        /// <summary>
        /// 设置送丝电机位置（力矩控制）
        /// </summary>
        /// <param name="position">目标位置（mm）</param>
        /// <param name="torqueLimit">力矩限制（N·m）</param>
        bool SetWireFeedPosition(double position, double torqueLimit);

        /// <summary>关闭激光器</summary>
        bool DisableLaser();

        /// <summary>打开激光器（允许出光）</summary>
        bool EnableLaser();
    }
}