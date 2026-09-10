using AdaWeldSystem.Comm;
using AdaWeldSystem.MonitorCam.IMonitorCam;
using Emgu.CV;
using Emgu.CV.CvEnum;
using MVSDK;
using System;
using System.Runtime.InteropServices;

// CameraHandle 在 MVSDK.cs 中以文件级别名定义（using 别名不跨文件），消费方须自行声明同一别名
using CameraHandle = System.Int32;

namespace AdaWeldSystem.MonitorCam.MecaVisionCam
{
    /// <summary>
    /// 麦格威（MecaVision）面阵相机实现：以 MVCAMSDK 原生接口（Native/MVSDK.cs，静态类 MvApi 动态加载）落地 IMonitorCamApi。
    /// 职责边界（ADR-035/039）：实现层只负责「按契约取帧并把帧抛出去」，
    /// 阶段语义、健康巡检、配置持久化等全部由 MonitorCamManager 承担，本类不做业务判断。
    /// 监控相机为 2D 面阵相机，无 LIVE/PIL 模式，仅提供单次触发取帧 CaptureSingleFrame。
    /// </summary>
    /// <remarks>
    /// 关键事实（均取自 SDK 原文 Demo/C#/MVSDK/MVSDK.cs，非推测）：
    /// ① 原生库 MVCAMSDK.dll（x86）/ MVCAMSDK_X64.dll（x64）由 MvApi 静态构造按进程位数动态加载；
    /// ② 相机输出默认自底向上，需 CameraFlipFrameBuffer 垂直镜像后再建图；
    /// ③ 成功调用 CameraGetImageBuffer 后必须 CameraReleaseImageBuffer，否则后续取帧失败；
    /// ④ 断联回调委托必须在本类持有引用（字段），否则会被 GC 回收导致回调崩溃。
    /// </remarks>
    public class MecaVisionCam : IMonitorCamApi
    {
        #region 私有变量

        private readonly object _sync = new object();
        private readonly string _tag = "麦格威相机";

        private CameraHandle _handle;
        private tSdkCameraDevInfo _devInfo;
        private IntPtr _processBuffer;
        private int _bufferBytes;

        // 回调委托必须持引用，防止被 GC 回收（SDK 只保存函数指针）
        private MVSDK.CAMERA_CONNECTION_STATUS_CALLBACK _connProc;

        private MonitorCameraConnectionState _state = MonitorCameraConnectionState.Disconnected;
        private int _frameIndex;
        private bool _isMono;
        private bool _isDisposed;

        #endregion

        #region 公共变量

        /// <summary>当前连接状态</summary>
        public MonitorCameraConnectionState ConnectionState
        {
            get { return _state; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建麦格威相机实现实例（不立即连接，连接由 Connect 触发）
        /// </summary>
        public MecaVisionCam()
        {
            _handle = 0;
            _processBuffer = IntPtr.Zero;
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 连接相机：初始化 SDK → 枚举设备 → 按 IP（GigE）或首台（USB3）匹配 → 打开设备 → 准备 ISP 输出。
        /// </summary>
        /// <param name="ip">相机 IP；为空或匹配失败时回退到枚举首台</param>
        /// <param name="port">相机端口；MecaVision 原生接口不使用，保留以兼容契约</param>
        /// <returns>是否成功建立连接</returns>
        public bool Connect(string ip, string port)
        {
            lock (_sync)
            {
                if (_state == MonitorCameraConnectionState.Connected)
                    return true;

                _state = MonitorCameraConnectionState.Connecting;
                try
                {
                    CameraSdkStatus status = MvApi.CameraSdkInit(0);
                    if (status != CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                    {
                        SetState(MonitorCameraConnectionState.Error);
                        GlobalCommData.ShowLog(_tag, string.Format("SDK 初始化失败 错误码 {0}", (int)status));
                        return false;
                    }

                    tSdkCameraDevInfo[] devices;
                    status = MvApi.CameraEnumerateDevice(out devices);
                    if (status != CameraSdkStatus.CAMERA_STATUS_SUCCESS || devices == null || devices.Length == 0)
                    {
                        SetState(MonitorCameraConnectionState.Error);
                        GlobalCommData.ShowLog(_tag, "未枚举到麦格威相机");
                        return false;
                    }

                    _devInfo = SelectDevice(devices, ip);

                    status = MvApi.CameraInit(ref _devInfo, -1, -1, ref _handle);
                    if (status != CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                    {
                        SetState(MonitorCameraConnectionState.Error);
                        GlobalCommData.ShowLog(_tag, string.Format("打开相机失败 错误码 {0}", (int)status));
                        return false;
                    }

                    tSdkCameraCapbility capability;
                    MvApi.CameraGetCapability(_handle, out capability);
                    _isMono = capability.sIspCapacity.bMonoSensor != 0;
                    if (_isMono)
                        MvApi.CameraSetIspOutFormat(_handle, (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);

                    // ISP 输出缓冲按最大分辨率申请，一次分配全程复用
                    int w = capability.sResolutionRange.iWidthMax;
                    int h = capability.sResolutionRange.iHeightMax;
                    _bufferBytes = w * h * 3 + 1024;
                    _processBuffer = Marshal.AllocHGlobal(_bufferBytes);

                    // 0 = 连续采集（非触发），CaptureSingleFrame 使用 CameraGetImageBuffer 取帧
                    MvApi.CameraSetTriggerMode(_handle, 0);

                    _connProc = OnConnectionStatusChanged;
                    MvApi.CameraSetConnectionStatusCallback(_handle, _connProc, IntPtr.Zero);

                    SetState(MonitorCameraConnectionState.Connected);
                    GlobalCommData.ShowLog(_tag, string.Format("相机已连接 黑白输出 {0}", _isMono));
                    return true;
                }
                catch (Exception ex)
                {
                    SetState(MonitorCameraConnectionState.Error);
                    // MvApi 静态构造抛出的异常会被 TypeInitializationException 包装，取内层才是真实原因
                    GlobalCommData.ShowLog(_tag, string.Format("连接异常 原因 {0}", ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    return false;
                }
            }
        }

        /// <summary>断开连接并释放 SDK 资源</summary>
        public void Disconnect()
        {
            lock (_sync)
            {
                try
                {
                    if (_handle != 0)
                    {
                        MvApi.CameraUnInit(_handle);
                        _handle = 0;
                    }
                }
                catch { }
                FreeProcessBuffer();
                SetState(MonitorCameraConnectionState.Disconnected);
                GlobalCommData.ShowLog(_tag, "相机已断开");
            }
        }

        /// <summary>
        /// 触发一次采集并同步返回帧（2D 面阵相机唯一取帧方式）：取缓冲 → ISP 处理 → 建图 → 释放缓冲。
        /// </summary>
        /// <returns>采集到的图像帧（独立副本）；失败返回 null</returns>
        public Mat CaptureSingleFrame()
        {
            lock (_sync)
            {
                if (_handle == 0) return null;

                tSdkFrameHead frameHead;
                IntPtr rawBuffer;
                CameraSdkStatus status = MvApi.CameraGetImageBuffer(_handle, out frameHead, out rawBuffer, 500);
                if (status != CameraSdkStatus.CAMERA_STATUS_SUCCESS || rawBuffer == IntPtr.Zero)
                {
                    GlobalCommData.ShowLog(_tag, string.Format("取帧失败 错误码 {0}", (int)status));
                    return null;
                }

                try
                {
                    MvApi.CameraImageProcess(_handle, rawBuffer, _processBuffer, ref frameHead);
                    MvApi.CameraFlipFrameBuffer(_processBuffer, ref frameHead, 1);
                    _frameIndex++;
                    return BuildMat(_processBuffer, frameHead);
                }
                finally
                {
                    // 取帧后必须归还缓冲，否则后续取帧将一直超时
                    try { MvApi.CameraReleaseImageBuffer(_handle, rawBuffer); } catch { }
                }
            }
        }

        /// <summary>释放 SDK 资源</summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Disconnect();
        }

        #endregion

        #region 私有函数

        /// <summary>
        /// 从枚举结果中挑选设备：GigE 相机按 IP 精确匹配（CameraGigeGetIp），
        /// 未指定 IP 或匹配不到时回退到枚举首台并记录日志。
        /// </summary>
        private tSdkCameraDevInfo SelectDevice(tSdkCameraDevInfo[] devices, string ip)
        {
            if (!string.IsNullOrEmpty(ip))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    tSdkCameraDevInfo candidate = devices[i];
                    string camIp;
                    string mask;
                    string gateway;
                    string etIp;
                    string etMask;
                    string etGateway;
                    CameraSdkStatus status = MvApi.CameraGigeGetIp(ref candidate, out camIp, out mask,
                        out gateway, out etIp, out etMask, out etGateway);
                    if (status == CameraSdkStatus.CAMERA_STATUS_SUCCESS && !string.IsNullOrEmpty(camIp)
                        && string.Equals(camIp, ip, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
                GlobalCommData.ShowLog(_tag, string.Format("未匹配到 IP 为 {0} 的相机，回退到枚举首台", ip));
            }
            return devices[0];
        }

        /// <summary>
        /// 将 SDK 输出缓冲转换为 Emgu Mat 并 Clone 出独立副本（缓冲会被 SDK 复用，必须复制）
        /// </summary>
        private Mat BuildMat(IntPtr buffer, tSdkFrameHead frameHead)
        {
            int w = frameHead.iWidth;
            int h = frameHead.iHeight;
            if (w <= 0 || h <= 0 || buffer == IntPtr.Zero) return null;

            bool mono = frameHead.uiMediaType == (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8;
            int channels = mono ? 1 : 3;

            Mat mat = new Mat(h, w, DepthType.Cv8U, channels, buffer, w * channels);
            Mat clone = mat.Clone();
            mat.Dispose();
            return clone;
        }

        /// <summary>SDK 断联回调：仅更新连接态并记一次日志，状态推进由业务层负责</summary>
        private void OnConnectionStatusChanged(CameraHandle hCamera, uint msg, uint uParam, IntPtr pContext)
        {
            SetState(MonitorCameraConnectionState.Error);
            GlobalCommData.ShowLog(_tag, string.Format("相机连接状态变更 消息 {0} 参数 {1}", msg, uParam));
        }

        /// <summary>更新连接态（状态切换本身不打日志，日志由调用点负责，遵循 ADR-031）</summary>
        private void SetState(MonitorCameraConnectionState newState)
        {
            _state = newState;
        }

        /// <summary>释放 ISP 输出缓冲</summary>
        private void FreeProcessBuffer()
        {
            if (_processBuffer == IntPtr.Zero) return;
            try { Marshal.FreeHGlobal(_processBuffer); } catch { }
            _processBuffer = IntPtr.Zero;
        }

        #endregion
    }
}
