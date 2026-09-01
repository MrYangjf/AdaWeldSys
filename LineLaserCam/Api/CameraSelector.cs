using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam; // 英莱 IntelligentLaser 线激光相机
using System;

namespace AdaWeldSystem.LineLaserCamApi
{
    /// <summary>
    /// 相机选择器：维护当前激活的 ICameraRun（英莱真实相机或虚拟调试相机）。
    /// 工作流与界面通过 CameraSelector.Active 获取当前相机，无需关心具体类型。
    /// 默认直接使用英莱(IntelligentLaser)线激光相机，一步到达，无需启动加载/连接检测。
    /// </summary>
    public static class CameraSelector
    {
        private static ICameraRun _active;
        private static VirtualCameraRun _virtualInstance;
        private static IntelligentLaserCameraRun _intelligentInstance;

        /// <summary>当前是否使用虚拟调试相机</summary>
        public static bool IsVirtual { get; private set; }

        /// <summary>当前配置的相机种类（默认英莱）</summary>
        private static CameraKind _kind = CameraKind.IntelligentLaser;

        /// <summary>工作流是否正在驱动相机出图（驱动「工作中」连接态）</summary>
        private static bool _working = false;

        /// <summary>
        /// 当前激活的相机（默认英莱线激光相机，首次访问时自动创建实例）。
        /// 一步到达：无需 Load/DetectConnection，直接返回可用的相机实例。
        /// </summary>
        public static ICameraRun Active
        {
            get
            {
                if (_active == null)
                {
                    SelectIntelligentLaser();
                }
                return _active;
            }
        }

        /// <summary>当前相机种类（虚拟 / 英莱 / 无）</summary>
        public static CameraKind Kind
        {
            get { return _kind; }
        }

        /// <summary>
        /// 线激光相机连接状态（与工作流程状态相互独立）。
        /// 工作中：工作流正在驱动相机出图；否则按种类与硬件连接情况派生 已连接/未连接。
        /// </summary>
        public static LineLaserCameraConnectionState ConnectionState
        {
            get
            {
                if (_working)
                    return LineLaserCameraConnectionState.Working;

                switch (_kind)
                {
                    case CameraKind.None:
                        return LineLaserCameraConnectionState.Disconnected;
                    case CameraKind.Virtual:
                        // 虚拟相机始终可用：空闲时显示为「虚拟相机（待启用）」，由 UI 映射文本
                        return LineLaserCameraConnectionState.Connected;
                    case CameraKind.IntelligentLaser:
                        return (_intelligentInstance != null && _intelligentInstance.IsConnected)
                            ? LineLaserCameraConnectionState.Connected
                            : LineLaserCameraConnectionState.Disconnected;
                    default:
                        return LineLaserCameraConnectionState.Disconnected;
                }
            }
        }

        /// <summary>
        /// 由工作流在采集开始/结束时调用，驱动相机连接态在「工作中」与「已连接」间切换。
        /// </summary>
        public static void SetWorking(bool working)
        {
            _working = working;
        }

        /// <summary>选择虚拟调试相机（模拟模式使用）</summary>
        public static void SelectVirtual()
        {
            if (_virtualInstance == null)
            {
                _virtualInstance = new VirtualCameraRun();
            }
            _active = _virtualInstance;
            IsVirtual = true;
            _kind = CameraKind.Virtual;
        }

        /// <summary>选择英莱(IntelligentLaser) 真实线激光相机（默认）</summary>
        public static void SelectIntelligentLaser()
        {
            if (_intelligentInstance == null)
            {
                _intelligentInstance = new IntelligentLaserCameraRun();
            }
            _active = _intelligentInstance;
            IsVirtual = false;
            _kind = CameraKind.IntelligentLaser;
        }
    }
}
