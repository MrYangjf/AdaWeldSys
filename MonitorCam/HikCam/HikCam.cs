using AdaWeldSystem.MonitorCam.IMonitorCam;
using Emgu.CV;
using Emgu.CV.CvEnum;
using MvCameraControl;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.MonitorCam.HikCam
{
    /// <summary>海康威视（Hikvision MVS）面阵相机实现：以官方 MvCameraControl.Net</summary>
    public class HikCam : IMonitorCamApi
    {
        #region 私有变量

        private const string Tag = "海康相机";

        private readonly object _sync = new object();

        private IDevice _device;
        private bool _isGrabbing;

        // 取帧线程统一消费帧，避免多线程争抢
        private Thread _grabThread;
        private volatile bool _grabRunning;
        private volatile bool _isRecording;

        // SDK 进程级初始化守卫：Initialize 全局一次，Finalize 仅在 Dispose 时执行
        private static readonly object _sdkInitLock = new object();
        private static bool _sdkInitialized;

        private MonitorCameraConnectionState _state = MonitorCameraConnectionState.Disconnected;
        private bool _isDisposed;

        // 最近一次厂商失败现场，DescribeLastError 唯一数据源
        private readonly object _errorLock = new object();
        private int _lastErrorCode = int.MinValue;
        private bool _lastErrorLocal;
        private string _lastErrorOperation = string.Empty;

        // 枚举层组合：GigE + USB3 + GenTL（照官方 BasicDemo 原文）
        private static readonly DeviceTLayerType LayerTypes =
            DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice |
            DeviceTLayerType.MvGenTLGigEDevice | DeviceTLayerType.MvGenTLCXPDevice |
            DeviceTLayerType.MvGenTLCameraLinkDevice | DeviceTLayerType.MvGenTLXoFDevice;

        #endregion

        #region 公共变量

        /// <summary>当前连接状态</summary>
        public MonitorCameraConnectionState ConnectionState
        {
            get { return _state; }
        }

        /// <summary>是否正在连续取流（采集）</summary>
        public bool IsGrabbing
        {
            get { return _isGrabbing; }
        }

        /// <summary>是否正在录像（须处于连续取流中）</summary>
        public bool IsRecording
        {
            get { return _isRecording; }
        }

        /// <summary>预览帧事件：连续取流中每帧抛出独立副本 Mat</summary>
        public event Action<Mat> PreviewFrame;

        #endregion

        #region 构造函数

        /// <summary>创建海康相机实现实例（不立即连接，连接由 Connect 触发）</summary>
        public HikCam()
        {
            _device = null;
            _isGrabbing = false;
        }

        #endregion

        #region 公共函数

        /// <summary>记录一次厂商返回码（成功时清空现场）</summary>
        /// <param name="operation">操作名（如「打开相机」）</param>
        /// <param name="code">厂商返回码；0 表示成功</param>
        private void RecordVendorResult(string operation, int code)
        {
            lock (_errorLock)
            {
                _lastErrorOperation = operation;
                _lastErrorCode = code == MvError.MV_OK ? int.MinValue : code;
                _lastErrorLocal = false;
            }
        }

        /// <summary>记录本机拦截异常，不编造码值</summary>
        /// <param name="operation">操作名</param>
        private void RecordLocalFailure(string operation)
        {
            lock (_errorLock)
            {
                _lastErrorOperation = operation;
                _lastErrorCode = int.MinValue;
                _lastErrorLocal = true;
            }
        }

        /// <summary>连接相机：初始化、枚举、按 IP 匹配、开设备</summary>

        /// <param name="ip">相机 IP；为空时取枚举首台</param>
        /// <param name="port">相机端口；海康 SDK 不使用，保留以兼容契约</param>
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
                    if (!EnsureSdkInitialized())
                    {
                        RecordLocalFailure("SDK 初始化");
                        SetState(MonitorCameraConnectionState.Error);
                        return false;
                    }

                    int result = DeviceEnumerator.EnumDevices(LayerTypes, out List<IDeviceInfo> devices);
                    if (result != MvError.MV_OK || devices == null || devices.Count == 0)
                    {
                        RecordVendorResult("枚举设备", result);
                        SetState(MonitorCameraConnectionState.Error);
                        GlobalCommData.ShowLog(Tag, string.Format("未枚举到海康相机 错误码 {0:X}", result), MessageLevel.Debug);
                        return false;
                    }

                    if (!TrySelectDevice(devices, ip, out IDeviceInfo selected))
                    {
                        RecordLocalFailure("按 IP 匹配设备");
                        SetState(MonitorCameraConnectionState.Error);
                        return false;
                    }

                    IDevice device = DeviceFactory.CreateDevice(selected);
                    if (device == null)
                    {
                        RecordLocalFailure("创建设备");
                        SetState(MonitorCameraConnectionState.Error);
                        GlobalCommData.ShowLog(Tag, "创建设备失败", MessageLevel.Debug);
                        return false;
                    }

                    result = device.Open();
                    if (result != MvError.MV_OK)
                    {
                        RecordVendorResult("打开相机", result);
                        device.Dispose();
                        SetState(MonitorCameraConnectionState.Error);
                        GlobalCommData.ShowLog(Tag, string.Format("打开相机失败 错误码 {0:X}", result), MessageLevel.Debug);
                        return false;
                    }

                    if (device is IGigEDevice gigeDevice)
                    {
                        // 探测最佳包大小，失败仅告警不阻断
                        result = gigeDevice.GetOptimalPacketSize(out int packetSize);
                        if (result == MvError.MV_OK)
                        {
                            device.Parameters.SetIntValue("GevSCPSPacketSize", packetSize);
                        }
                        else
                        {
                            GlobalCommData.ShowLog(Tag, string.Format("探测最佳包大小失败 错误码 {0:X}", result), MessageLevel.Debug);
                        }
                    }

                    // 预置连续采集参数，此时不开流
                    device.Parameters.SetEnumValueByString("AcquisitionMode", "Continuous");
                    device.Parameters.SetEnumValueByString("TriggerMode", "Off");

                    _device = device;
                    RecordVendorResult("Connect", MvError.MV_OK);
                    SetState(MonitorCameraConnectionState.Connected);
                    GlobalCommData.ShowLog(Tag, "相机已连接（未开启采集）", MessageLevel.Debug);
                    return true;
                }
                catch (Exception ex)
                {
                    RecordLocalFailure("连接异常");
                    SetState(MonitorCameraConnectionState.Error);
                    // 异常被类型初始化包装，内层才是真因
                    GlobalCommData.ShowLog(Tag, string.Format("连接异常 原因 {0}", ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    return false;
                }
            }
        }

        /// <summary>失败原因，厂商码的唯一产出点</summary>
        /// <param name="context">失败语境（连接 / 采集 / 分析）</param>
        /// <returns>非空的原因描述</returns>
        public string DescribeLastError(MonitorCamErrorContext context)
        {
            if (context == MonitorCamErrorContext.Analyze)
                return "图像分析不在相机实现层执行，不产生厂商错误码";

            int code;
            bool local;
            string operation;
            lock (_errorLock)
            {
                code = _lastErrorCode;
                local = _lastErrorLocal;
                operation = _lastErrorOperation;
            }

            string head = context == MonitorCamErrorContext.Connection ? "连接失败" : "采集失败";
            if (local)
                return string.Format("未获得厂商错误码（{0} 未执行：相机未连接或 SDK 调用异常）", operation);
            if (code == int.MinValue)
                return string.Format("{0}（尚无厂商错误码记录）", head);
            return string.Format("{0}，厂商错误码 0x{1:X}（{2}）", head, code, operation);
        }

        /// <summary>扫描（枚举）当前连接的海康相机，GigE 相机解码 IP，返回含 IP</summary>

        /// <returns>发现的设备列表；无设备或扫描失败返回空列表</returns>
        public List<MonitorCamDeviceInfo> ScanDevices()
        {
            List<MonitorCamDeviceInfo> result = new List<MonitorCamDeviceInfo>();
            lock (_sync)
            {
                try
                {
                    if (!EnsureSdkInitialized())
                    {
                        GlobalCommData.ShowLog(Tag, "扫描前 SDK 初始化失败");
                        return result;
                    }

                    int nRet = DeviceEnumerator.EnumDevices(LayerTypes, out List<IDeviceInfo> devices);
                    if (nRet != MvError.MV_OK || devices == null || devices.Count == 0)
                    {
                        GlobalCommData.ShowLog(Tag, string.Format("扫描未枚举到海康相机 错误码 {0:X}", nRet));
                        return result;
                    }

                    for (int i = 0; i < devices.Count; i++)
                    {
                        MonitorCamDeviceInfo info = new MonitorCamDeviceInfo
                        {
                            Ip = QueryDeviceIp(devices[i]),
                            ProductName = string.IsNullOrEmpty(devices[i].UserDefinedName)
                                ? devices[i].ModelName
                                : devices[i].UserDefinedName,
                            SerialNumber = devices[i].SerialNumber,
                            PortType = devices[i].TLayerType.ToString(),
                            SensorType = devices[i].ManufacturerName
                        };
                        result.Add(info);
                    }
                    GlobalCommData.ShowLog(Tag, string.Format("扫描到 {0} 台相机", result.Count));
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, string.Format("扫描异常 原因 {0}", ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                }
            }
            return result;
        }

        /// <summary>断开连接并释放设备资源（先停录像/采集，SDK 全局资</summary>
        public void Disconnect()
        {
            lock (_sync)
            {
                try
                {
                    StopCapture();
                    if (_device != null)
                    {
                        try { _device.Close(); } catch { }   // 释放路径：设备即将销毁，失败不影响功能
                        try { _device.Dispose(); } catch { }   // 释放路径：设备即将销毁，失败不影响功能
                        _device = null;
                    }
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, "停止取流异常 " + ex.GetType().Name + " " + ex.Message,
                        MessageLevel.Warning);
                }
                SetState(MonitorCameraConnectionState.Disconnected);
                GlobalCommData.ShowLog(Tag, "相机已断开");
            }
        }

        /// <summary>触发一次采集并同步返回帧（2D 面阵相机唯一取帧</summary>

        /// <returns>采集到的图像帧（独立副本）；失败返回 null</returns>
        public Mat CaptureSingleFrame()
        {
            lock (_sync)
            {
                if (_device == null || !_isGrabbing)
                {
                    RecordLocalFailure("单次取帧");
                    return null;
                }

                int result = _device.StreamGrabber.GetImageBuffer(500, out IFrameOut frameOut);
                if (result != MvError.MV_OK || frameOut == null)
                {
                    RecordVendorResult("单次取帧", result);
                    GlobalCommData.ShowLog(Tag, string.Format("取帧失败 错误码 {0:X}", result));
                    return null;
                }

                try
                {
                    Mat frame = BuildMat(frameOut);
                    if (frame == null)
                        RecordLocalFailure("像素转换/建帧");
                    else
                        RecordVendorResult("单次取帧", MvError.MV_OK);
                    return frame;
                }
                finally
                {
                    // 取帧后必须归还缓冲，否则后续取帧将一直超时
                    try { _device.StreamGrabber.FreeImageBuffer(frameOut); } catch { }   // 释放缓冲：失败最多丢一帧，不影响取流
                }
            }
        }

        /// <summary>开始连续采集（对齐官方 demo 开始采集）：连续模式 + 开启</summary>

        /// <returns>是否成功开始采集</returns>
        public bool StartCapture()
        {
            lock (_sync)
            {
                if (_device == null)
                {
                    RecordLocalFailure("开始采集");
                    GlobalCommData.ShowLog(Tag, "开始采集失败 相机未连接");
                    return false;
                }
                // 已处于采集中（取流 + 取帧线程都在跑）：直接复用
                if (_isGrabbing && _grabRunning) return true;

                try
                {
                    if (!_isGrabbing)
                    {
                        // 连续模式（软触发单帧后残留 TriggerMode On，此处恢复）
                        _device.Parameters.SetEnumValueByString("TriggerMode", "Off");

                        int result = _device.StreamGrabber.StartGrabbing();
                        if (result != MvError.MV_OK)
                        {
                            RecordVendorResult("开启取流", result);
                            GlobalCommData.ShowLog(Tag, string.Format("开始采集失败 开启取流错误码 {0:X}", result));
                            return false;
                        }
                    }

                    // 取流已开启：启动取帧线程
                    StartGrabThreadLocked();
                    RecordVendorResult("开始采集", MvError.MV_OK);
                    GlobalCommData.ShowLog(Tag, "开始采集 连续模式");
                    return true;
                }
                catch (Exception ex)
                {
                    RecordLocalFailure("开始采集异常");
                    GlobalCommData.ShowLog(Tag, string.Format("开始采集异常 原因 {0}", ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    return false;
                }
            }
        }

        /// <summary>停止连续采集：录像中先停止录像，再停取帧线程</summary>
        public void StopCapture()
        {
            lock (_sync)
            {
                try
                {
                    if (_isRecording) StopRecord();

                    if (_grabRunning)
                    {
                        _grabRunning = false;
                        Thread grabThread = _grabThread;
                        if (grabThread != null && grabThread.IsAlive && grabThread != Thread.CurrentThread)
                            grabThread.Join(1500);
                        _grabThread = null;
                    }

                    if (_device != null && _isGrabbing)
                    {
                        try { _device.StreamGrabber.StopGrabbing(); } catch { }   // 停流失败不阻断后续释放（设备即将关闭）
                    }
                    _isGrabbing = false;
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, "停止采集异常 " + ex.GetType().Name + " " + ex.Message,
                        MessageLevel.Warning);
                }
            }
        }

        /// <summary>软触发单帧：切触发模式 → 开启取流 → TriggerSoftware → 取一帧 → 关闭取</summary>

        /// <returns>触发取得的图像帧（独立副本）；失败返回 null</returns>
        public Mat TriggerSingleShot()
        {
            lock (_sync)
            {
                if (_device == null || _isGrabbing)
                {
                    RecordLocalFailure("软触发");
                    return null;
                }

                Mat mat = null;
                try
                {
                    _device.Parameters.SetEnumValueByString("TriggerMode", "On");
                    _device.Parameters.SetEnumValueByString("TriggerSource", "Software");

                    int result = _device.StreamGrabber.StartGrabbing();
                    if (result != MvError.MV_OK)
                    {
                        RecordVendorResult("软触发开启取流", result);
                        GlobalCommData.ShowLog(Tag, string.Format("软触发失败 开启取流错误码 {0:X}", result));
                        return null;
                    }

                    result = _device.Parameters.SetCommandValue("TriggerSoftware");
                    if (result != MvError.MV_OK)
                    {
                        RecordVendorResult("软触发命令", result);
                        GlobalCommData.ShowLog(Tag, string.Format("软触发失败 触发命令错误码 {0:X}", result));
                        try { _device.StreamGrabber.StopGrabbing(); } catch { }   // 停流失败不阻断释放链
                        return null;
                    }

                    result = _device.StreamGrabber.GetImageBuffer(5000, out IFrameOut frameOut);
                    if (result != MvError.MV_OK || frameOut == null)
                    {
                        RecordVendorResult("软触发取帧", result);
                        GlobalCommData.ShowLog(Tag, string.Format("软触发取帧失败 错误码 {0:X}", result));
                        try { _device.StreamGrabber.StopGrabbing(); } catch { }   // 停流失败不阻断释放链
                        return null;
                    }

                    try
                    {
                        mat = BuildMat(frameOut);
                    }
                    finally
                    {
                        try { _device.StreamGrabber.FreeImageBuffer(frameOut); } catch { }   // 释放缓冲：失败不影响功能
                    }
                }
                finally
                {
                    try { _device.StreamGrabber.StopGrabbing(); } catch { }   // 停流失败不阻断释放链
                }
                GlobalCommData.ShowLog(Tag, "软触发单帧完成");
                if (mat != null) RecordVendorResult("软触发", MvError.MV_OK);
                return mat;
            }
        }

        /// <summary>开始录像（须处于连续取流中）：以当前宽高/像素格</summary>

        /// <param name="filePath">录像文件完整路径（.avi）</param>
        /// <returns>是否成功开始录像</returns>
        public bool StartRecord(string filePath)
        {
            lock (_sync)
            {
                if (_device == null || !_isGrabbing)
                {
                    RecordLocalFailure("开始录像");
                    GlobalCommData.ShowLog(Tag, "开始录像失败 未处于采集中");
                    return false;
                }
                if (_isRecording) return true;
                if (string.IsNullOrEmpty(filePath))
                {
                    RecordLocalFailure("开始录像");
                    GlobalCommData.ShowLog(Tag, "开始录像失败 路径为空");
                    return false;
                }

                try
                {
                    if (!ReadFrameFormat(out uint width, out uint height, out MvGvspPixelType pixelType))
                    {
                        RecordLocalFailure("读取帧格式");
                        return false;
                    }

                    RecordParam recordParam;
                    recordParam.Width = width;
                    recordParam.Height = height;
                    recordParam.PixelType = pixelType;
                    recordParam.FrameRate = GetRecordFrameRate();
                    recordParam.BitRate = 1000;
                    recordParam.FormatType = VideoFormatType.AVI;

                    int result = _device.VideoRecorder.StartRecord(filePath, recordParam);
                    if (result != MvError.MV_OK)
                    {
                        RecordVendorResult("开始录像", result);
                        GlobalCommData.ShowLog(Tag, string.Format("开始录像失败 错误码 {0:X}", result));
                        return false;
                    }

                    _isRecording = true;
                    GlobalCommData.ShowLog(Tag, string.Format("开始录像 {0}", filePath));
                    return true;
                }
                catch (Exception ex)
                {
                    RecordLocalFailure("开始录像异常");
                    GlobalCommData.ShowLog(Tag, string.Format("开始录像异常 原因 {0}", ex.Message));
                    return false;
                }
            }
        }

        /// <summary>停止录像</summary>
        public void StopRecord()
        {
            lock (_sync)
            {
                if (!_isRecording) return;
                _isRecording = false;
                try
                {
                    _device?.VideoRecorder.StopRecord();
                    GlobalCommData.ShowLog(Tag, "停止录像");
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, string.Format("停止录像异常 原因 {0}", ex.Message));
                }
            }
        }

        /// <summary>设置曝光时间（关闭自动曝光后写入）</summary>
        /// <param name="valueUs">曝光时间（微秒）</param>
        /// <returns>是否设置成功</returns>
        public bool SetExposureTime(float valueUs)
        {
            lock (_sync)
            {
                if (_device == null) return false;
                try
                {
                    _device.Parameters.SetEnumValue("ExposureAuto", 0);
                    int result = _device.Parameters.SetFloatValue("ExposureTime", valueUs);
                    if (result != MvError.MV_OK)
                    {
                        GlobalCommData.ShowLog(Tag, string.Format("设置曝光失败 错误码 {0:X}", result));
                        return false;
                    }
                    GlobalCommData.ShowLog(Tag, string.Format("设置曝光 {0:F1}us", valueUs));
                    return true;
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, string.Format("设置曝光异常 原因 {0}", ex.Message));
                    return false;
                }
            }
        }

        /// <summary>设置增益（关闭自动增益后写入）</summary>
        /// <param name="valueDb">增益（dB）</param>
        /// <returns>是否设置成功</returns>
        public bool SetGain(float valueDb)
        {
            lock (_sync)
            {
                if (_device == null) return false;
                try
                {
                    _device.Parameters.SetEnumValue("GainAuto", 0);
                    int result = _device.Parameters.SetFloatValue("Gain", valueDb);
                    if (result != MvError.MV_OK)
                    {
                        GlobalCommData.ShowLog(Tag, string.Format("设置增益失败 错误码 {0:X}", result));
                        return false;
                    }
                    GlobalCommData.ShowLog(Tag, string.Format("设置增益 {0:F1}dB", valueDb));
                    return true;
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, string.Format("设置增益异常 原因 {0}", ex.Message));
                    return false;
                }
            }
        }

        /// <summary>读取当前曝光时间（微秒）；失败返回 -1</summary>
        /// <returns>曝光时间（微秒）；失败返回 -1</returns>
        public float GetExposureTime()
        {
            lock (_sync)
            {
                if (_device == null) return -1f;
                try
                {
                    int retCode = _device.Parameters.GetFloatValue("ExposureTime", out IFloatValue value);
                    if (retCode != MvError.MV_OK || value == null) return -1f;
                    return (float)value.CurValue;
                }
                catch { return -1f; }
            }
        }

        /// <summary>读取当前增益（dB）；失败返回 -1</summary>
        /// <returns>增益（dB）；失败返回 -1</returns>
        public float GetGain()
        {
            lock (_sync)
            {
                if (_device == null) return -1f;
                try
                {
                    int retCode = _device.Parameters.GetFloatValue("Gain", out IFloatValue value);
                    if (retCode != MvError.MV_OK || value == null) return -1f;
                    return (float)value.CurValue;
                }
                catch { return -1f; }
            }
        }

        /// <summary>断开设备并释放 SDK 全局资源</summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Disconnect();

            lock (_sdkInitLock)
            {
                if (_sdkInitialized)
                {
                    try { SDKSystem.Finalize(); } catch { }   // 进程退出路径：SDK 收尾失败无法补救
                    _sdkInitialized = false;
                }
            }
        }

        #endregion

        #region 私有函数

        /// <summary>SDK 进程级初始化（幂等）：失败返回 false</summary>
        /// <returns>SDK 是否已初始化</returns>
        private static bool EnsureSdkInitialized()
        {
            lock (_sdkInitLock)
            {
                if (_sdkInitialized) return true;
                try
                {
                    SDKSystem.Initialize();
                    _sdkInitialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, string.Format("SDK 初始化失败 原因 {0}", ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    return false;
                }
            }
        }

        /// <summary>按 IP 精确挑选设备，匹配不到即失败</summary>

        /// <param name="devices">枚举到的设备列表</param>
        /// <param name="ip">目标相机 IP；为空表示不限定</param>
        /// <param name="selected">选中的设备信息</param>
        /// <returns>选中有效设备返回 true</returns>
        private static bool TrySelectDevice(List<IDeviceInfo> devices, string ip, out IDeviceInfo selected)
        {
            selected = devices[0];
            if (string.IsNullOrEmpty(ip)) return true;

            for (int i = 0; i < devices.Count; i++)
            {
                string camIp = QueryDeviceIp(devices[i]);
                if (!string.IsNullOrEmpty(camIp) && string.Equals(camIp, ip, StringComparison.OrdinalIgnoreCase))
                {
                    selected = devices[i];
                    return true;
                }
            }
            GlobalCommData.ShowLog(Tag, string.Format("未找到 IP 为 {0} 的相机，拒绝连接", ip));
            return false;
        }

        /// <summary>查询设备 GigE IP；非 GigE 设备返回空字符串</summary>
        /// <param name="info">设备信息</param>
        /// <returns>点分十进制 IP；非 GigE 返回空字符串</returns>
        private static string QueryDeviceIp(IDeviceInfo info)
        {
            if (!(info is IGigEDeviceInfo gigeInfo)) return string.Empty;

            uint value = gigeInfo.CurrentIp;
            return string.Format("{0}.{1}.{2}.{3}",
                (value >> 24) & 0xFF, (value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        }

        /// <summary>将取流帧转换为 Emgu Mat（独立副本）：彩色统一转 RGB8_Packed、其余统一</summary>

        /// <param name="frameOut">取流帧</param>
        /// <returns>独立 Mat；转换失败返回 null</returns>
        private Mat BuildMat(IFrameOut frameOut)
        {
            IImage source = frameOut.Image;
            if (source == null || source.Width == 0 || source.Height == 0) return null;

            IImage converted = null;
            try
            {
                MvGvspPixelType sourceType = source.PixelType;
                int channels;
                if (IsColorPixelFormat(sourceType))
                {
                    int convRet = _device.PixelTypeConverter.ConvertPixelType(source, out converted, MvGvspPixelType.PixelType_Gvsp_RGB8_Packed);
                    if (convRet != MvError.MV_OK || converted == null)
                    {
                        GlobalCommData.ShowLog(Tag, string.Format("彩色像素转换失败 错误码 {0:X}", convRet));
                        return null;
                    }
                    channels = 3;
                }
                else if (sourceType != MvGvspPixelType.PixelType_Gvsp_Mono8)
                {
                    int convRet = _device.PixelTypeConverter.ConvertPixelType(source, out converted, MvGvspPixelType.PixelType_Gvsp_Mono8);
                    if (convRet != MvError.MV_OK || converted == null)
                    {
                        GlobalCommData.ShowLog(Tag, string.Format("单色像素转换失败 错误码 {0:X}", convRet));
                        return null;
                    }
                    channels = 1;
                }
                else
                {
                    channels = 1;
                }

                // IImage.Width/Height 为 uint：显式收窄为 int；宽高乘 int 会经数值提升变 long，必须先用 int 变量相乘
                int width = (int)source.Width;
                int height = (int)source.Height;
                int expectBytes = width * height * channels;
                byte[] data = converted != null ? converted.PixelData : source.PixelData;
                if (data == null || data.Length < expectBytes)
                {
                    GlobalCommData.ShowLog(Tag, "像素数据长度不足");
                    return null;
                }

                Mat mat = new Mat(height, width, DepthType.Cv8U, channels);
                Marshal.Copy(data, 0, mat.DataPointer, expectBytes);
                return mat;
            }
            finally
            {
                if (converted != null)
                {
                    try { converted.Dispose(); } catch { }   // 临时图释放：失败不影响功能
                }
            }
        }

        /// <summary>判断像素格式是否为彩色（照官方 Image_ConvertPixelType 示例归类，拜耳</summary>

        /// <param name="type">像素格式</param>
        /// <returns>彩色返回 true</returns>
        private static bool IsColorPixelFormat(MvGvspPixelType type)
        {
            switch (type)
            {
                case MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerRBGG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>启动取帧线程（须已持 _sync 且取流已开启）：置运行标志</summary>
        private void StartGrabThreadLocked()
        {
            _grabRunning = true;
            _isGrabbing = true;
            _grabThread = new Thread(GrabLoop)
            {
                Name = "HikCamGrabLoop",
                IsBackground = true
            };
            _grabThread.Start();
        }

        /// <summary>连续采集取帧线程体：取缓冲 → （录像中）喂 VideoRecorder → 建预览 Mat 抛 PreviewFrame →</summary>
        private void GrabLoop()
        {
            while (_grabRunning)
            {
                Mat preview = null;
                int result;
                lock (_sync)
                {
                    // 锁内只做 SDK 帧操作；PreviewFrame 分发移到锁外，避免与 CaptureSingleFrame 等锁内 SDK 调用竞争
                    if (!_grabRunning || _device == null || !_isGrabbing) break;
                    result = _device.StreamGrabber.GetImageBuffer(1000, out IFrameOut frameOut);
                    if (result == MvError.MV_OK && frameOut != null)
                    {
                        try
                        {
                            if (_isRecording)
                            {
                                _device.VideoRecorder.InputOneFrame(frameOut.Image);
                            }
                            preview = BuildMat(frameOut);
                        }
                        finally
                        {
                            try { _device.StreamGrabber.FreeImageBuffer(frameOut); } catch { }   // 释放缓冲：失败最多丢一帧
                        }
                    }
                }
                if (preview != null)
                {
                    PreviewFrame?.Invoke(preview);
                }
                if (result != MvError.MV_OK) Thread.Sleep(10);
            }
        }

        /// <summary>读取当前帧格式（宽/高/像素格式），供录像 RecordParam 使用</summary>
        /// <param name="width">帧宽</param>
        /// <param name="height">帧高</param>
        /// <param name="pixelType">像素格式</param>
        /// <returns>读取成功返回 true</returns>
        private bool ReadFrameFormat(out uint width, out uint height, out MvGvspPixelType pixelType)
        {
            width = 0;
            height = 0;
            pixelType = MvGvspPixelType.PixelType_Gvsp_Mono8;

            if (_device.Parameters.GetIntValue("Width", out IIntValue intValue) != MvError.MV_OK) return false;
            width = (uint)intValue.CurValue;

            if (_device.Parameters.GetIntValue("Height", out intValue) != MvError.MV_OK) return false;
            height = (uint)intValue.CurValue;

            if (_device.Parameters.GetEnumValue("PixelFormat", out IEnumValue enumValue) != MvError.MV_OK) return false;
            pixelType = (MvGvspPixelType)enumValue.CurEnumEntry.Value;
            return true;
        }

        /// <summary>读取当前帧率（供录像 RecordParam）；读不到时回退 30</summary>
        /// <returns>帧率（fps）</returns>
        private float GetRecordFrameRate()
        {
            try
            {
                if (_device.Parameters.GetFloatValue("ResultingFrameRate", out IFloatValue value) == MvError.MV_OK && value != null)
                    return (float)value.CurValue;
            }
            catch { }   // 探测型：读不到帧率就回退默认值，不构成故障
            return 30f;
        }

        /// <summary>更新连接态（状态切换本身不打日志，日志由调用</summary>

        /// <param name="newState">新状态</param>
        private void SetState(MonitorCameraConnectionState newState)
        {
            _state = newState;
        }

        #endregion
    }
}
