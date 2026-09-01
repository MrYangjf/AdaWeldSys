using System;
using System.Runtime.InteropServices;
using System.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using MvCamCtrl.NET;
// MvCameraControl.Net 将所有结构体/枚举/委托作为 MyCamera 的嵌套类型，
// using static 将其与静态方法一并引入当前作用域，避免逐处写 MyCamera. 前缀。
using static MvCamCtrl.NET.MyCamera;

namespace AdaWeldSystem.MonitorCam.Api
{
    /// <summary>
    /// 海康威视面阵工业相机（MvCameraControl.Net SDK）监控相机真实实现。
    /// 参考 MVS C# BasicDemo 用法：枚举 → CreateDevice → OpenDevice → 采集。
    /// 设计上满足 IMonitorCameraApi 契约，是 MonitorCameraRun 当前使用的底层相机实现。
    ///
    /// 依赖：
    ///   1) MvCameraControl.Net.dll（托管封装，已通过 Libs/MvCameraControl 引用，CopyLocal=true）；
    ///   2) MvCameraControl.dll（原生核心）。本 Dev 包未随附，由完整 MVS 运行时提供。
    ///      运行时需保证该原生 DLL 在 PATH 或可执行文件目录中（详见 LLMWiki lessons/MvCameraControl-NativeRuntime）。
    ///
    /// 线程安全说明：SDK 回调在内部采集线程触发，ConvertToMat 内部 Clone 出独立 Mat，
    /// 故上行文 FrameReceived 事件的消费者拿到的 Mat 是线程安全副本。
    /// </summary>
    public class HikvisionCameraApi : IMonitorCameraApi
    {
        private readonly object _lock = new object();
        private MonitorCameraConnectionState _state = MonitorCameraConnectionState.Disconnected;
        private MyCamera _camera = null;
        private MV_CC_DEVICE_INFO_LIST _devList;
        private bool _grabbing = false;
        private cbOutputExdelegate _imageCallback;
        private int _frameIndex = 0;

        public HikvisionCameraApi()
        {
            _imageCallback = new cbOutputExdelegate(OnImageCallback);
        }

        public MonitorCameraConnectionState ConnectionState
        {
            get { lock (_lock) { return _state; } }
        }

        public event EventHandler<MonitorFrameEventArgs> FrameReceived;

        public bool Connect(string ip, string port)
        {
            lock (_lock) { _state = MonitorCameraConnectionState.Connecting; }
            try
            {
                _devList = new MV_CC_DEVICE_INFO_LIST();
                int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref _devList);
                if (nRet != MyCamera.MV_OK || _devList.nDeviceNum == 0)
                {
                    lock (_lock) { _state = MonitorCameraConnectionState.Error; }
                    return false;
                }

                MV_CC_DEVICE_INFO target = FindDeviceByIp(_devList, ip);
                if (target.nTLayerType == 0)
                {
                    // FindDeviceByIp 返回空设备（nTLayerType==0 表示未找到）
                    lock (_lock) { _state = MonitorCameraConnectionState.Error; }
                    return false;
                }

                _camera = new MyCamera();
                nRet = _camera.MV_CC_CreateDevice_NET(ref target);
                if (nRet != MyCamera.MV_OK)
                {
                    _camera = null;
                    lock (_lock) { _state = MonitorCameraConnectionState.Error; }
                    return false;
                }

                nRet = _camera.MV_CC_OpenDevice_NET();
                if (nRet != MyCamera.MV_OK)
                {
                    try { _camera.MV_CC_DestroyDevice_NET(); } catch { }
                    _camera = null;
                    lock (_lock) { _state = MonitorCameraConnectionState.Error; }
                    return false;
                }

                // GigE 探测网络最优包大小（仅 GigE 有效）
                if (target.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    int pkt = _camera.MV_CC_GetOptimalPacketSize_NET();
                    if (pkt > 0)
                    {
                        try { _camera.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", pkt); } catch { }
                    }
                }

                // 连续采集 + 关闭触发（监控为自由采集）
                _camera.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                _camera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

                lock (_lock) { _state = MonitorCameraConnectionState.Connected; }
                return true;
            }
            catch (Exception)
            {
                lock (_lock) { _state = MonitorCameraConnectionState.Error; }
                return false;
            }
        }

        /// <summary>
        /// 在枚举列表中按 IP 查找 GigE 设备；ip 为空或无匹配时回退到第一台可用设备。
        /// 返回值 nTLayerType==0 表示未找到任何设备。
        /// </summary>
        private MV_CC_DEVICE_INFO FindDeviceByIp(MV_CC_DEVICE_INFO_LIST list, string ip)
        {
            MV_CC_DEVICE_INFO empty = new MV_CC_DEVICE_INFO(0);
            MV_CC_DEVICE_INFO fallback = empty;
            bool hasFallback = false;
            for (int i = 0; i < list.nDeviceNum; i++)
            {
                IntPtr ptr = list.pDeviceInfo[i];
                if (ptr == IntPtr.Zero) continue;
                MV_CC_DEVICE_INFO dev = (MV_CC_DEVICE_INFO)Marshal.PtrToStructure(ptr, typeof(MV_CC_DEVICE_INFO));
                if (!hasFallback)
                {
                    fallback = dev;
                    hasFallback = true;
                }
                if (!string.IsNullOrEmpty(ip))
                {
                    string devIp = GetGigEIp(dev);
                    if (devIp != null && devIp == ip.Trim())
                    {
                        return dev;
                    }
                }
            }
            return hasFallback ? fallback : empty;
        }

        /// <summary>
        /// 从 GigE 设备信息中提取当前 IP。nCurrentIp 为大端存储（最高字节为第一段）。
        /// </summary>
        private string GetGigEIp(MV_CC_DEVICE_INFO dev)
        {
            if (dev.nTLayerType != MyCamera.MV_GIGE_DEVICE) return null;
            MV_GIGE_DEVICE_INFO_EX gige =
                (MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(dev.SpecialInfo.stGigEInfo, typeof(MV_GIGE_DEVICE_INFO_EX));
            uint n = gige.nCurrentIp;
            byte[] b = BitConverter.GetBytes(n);
            // 大端：b[3] 为第一段
            return string.Format("{0}.{1}.{2}.{3}", b[3], b[2], b[1], b[0]);
        }

        public void Disconnect()
        {
            StopAcquisition();
            if (_camera != null)
            {
                try { _camera.MV_CC_CloseDevice_NET(); } catch { }
                try { _camera.MV_CC_DestroyDevice_NET(); } catch { }
                _camera = null;
            }
            lock (_lock) { _state = MonitorCameraConnectionState.Disconnected; }
        }

        public void StartAcquisition()
        {
            if (ConnectionState != MonitorCameraConnectionState.Connected || _camera == null) return;
            try { _camera.MV_CC_RegisterImageCallBackEx_NET(_imageCallback, IntPtr.Zero); } catch { }
            int nRet = _camera.MV_CC_StartGrabbing_NET();
            if (nRet == MyCamera.MV_OK) _grabbing = true;
        }

        public void StopAcquisition()
        {
            if (_camera != null)
            {
                try { _camera.MV_CC_StopGrabbing_NET(); } catch { }
                try { _camera.MV_CC_RegisterImageCallBackEx_NET(null, IntPtr.Zero); } catch { }
            }
            _grabbing = false;
        }

        public Mat CaptureSingleFrame()
        {
            if (_camera == null) return null;
            bool wasGrabbing = _grabbing;
            if (!wasGrabbing)
            {
                int r = _camera.MV_CC_StartGrabbing_NET();
                if (r != MyCamera.MV_OK) return null;
                _grabbing = true;
            }
            try
            {
                MV_FRAME_OUT frameOut = new MV_FRAME_OUT();
                int nRet = _camera.MV_CC_GetImageBuffer_NET(ref frameOut, 1000);
                if (nRet != MyCamera.MV_OK || frameOut.pBufAddr == IntPtr.Zero)
                {
                    return null;
                }
                Mat mat = ConvertToMat(frameOut.pBufAddr, frameOut.stFrameInfo);
                try { _camera.MV_CC_FreeImageBuffer_NET(ref frameOut); } catch { }
                return mat;
            }
            finally
            {
                if (!wasGrabbing)
                {
                    try { _camera.MV_CC_StopGrabbing_NET(); } catch { }
                    _grabbing = false;
                }
            }
        }

        private void OnImageCallback(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
        {
            if (pData == IntPtr.Zero) return;
            Mat frame = ConvertToMat(pData, pFrameInfo);
            if (frame == null) return;
            if (FrameReceived != null)
            {
                var args = new MonitorFrameEventArgs();
                args.Frame = frame;
                args.FrameIndex = Interlocked.Increment(ref _frameIndex);
                FrameReceived(this, args);
            }
        }

        /// <summary>
        /// 将 SDK 原始像素缓冲转换为 Emgu Mat（灰度/彩色），并 Clone 出独立副本。
        /// 支持 Mono8（单通道）与 RGB8/BGR8（三通道）；其余格式按 Mono8 尽力解析。
        /// Bayer 等需转码的格式建议通过 MV_CC_ConvertPixelTypeEx_NET 预处理（见 LLMWiki）。
        /// </summary>
        private Mat ConvertToMat(IntPtr pData, MV_FRAME_OUT_INFO_EX info)
        {
            int w = info.nWidth;
            int h = info.nHeight;
            if (w <= 0 || h <= 0 || pData == IntPtr.Zero) return null;

            Mat mat;
            MvGvspPixelType pt = info.enPixelType;
            if (pt == MvGvspPixelType.PixelType_Gvsp_Mono8)
            {
                mat = new Mat(h, w, DepthType.Cv8U, 1, pData, w);
            }
            else if (pt == MvGvspPixelType.PixelType_Gvsp_BGR8_Packed)
            {
                mat = new Mat(h, w, DepthType.Cv8U, 3, pData, w * 3);
            }
            else if (pt == MvGvspPixelType.PixelType_Gvsp_RGB8_Packed)
            {
                // Emgu 默认按 BGR 解释 3 通道；RGB 此处直接以 3 通道承载（可能存在通道顺序差异）
                mat = new Mat(h, w, DepthType.Cv8U, 3, pData, w * 3);
            }
            else
            {
                mat = new Mat(h, w, DepthType.Cv8U, 1, pData, w);
            }

            Mat clone = mat.Clone();
            mat.Dispose();
            return clone;
        }

        public void Dispose()
        {
            try { StopAcquisition(); } catch { }
            if (_camera != null)
            {
                try { _camera.MV_CC_CloseDevice_NET(); } catch { }
                try { _camera.MV_CC_DestroyDevice_NET(); } catch { }
                _camera = null;
            }
            _imageCallback = null;
        }
    }
}
