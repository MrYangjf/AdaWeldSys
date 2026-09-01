using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AdaWeldSystem.Comm;


namespace AdaWeldSystem.LineLaserCam.HikRobotCam.ApiCore
{
    /// <summary>
    /// 海康3D相机设备封装类，负责单个相机的打开/关闭、采集控制、参数设置和断线重连
    /// </summary>
    public class HikCameraDevice : IDisposable
    {
        #region 常量

        /// <summary>
        /// 最大图像缓冲区大小（30MB）
        /// </summary>
        private static readonly uint MAX_IMAGE_SIZE = 1024 * 1024 * 30;

        /// <summary>
        /// 默认获取图像超时时间（毫秒）
        /// </summary>
        private const uint DEFAULT_TIMEOUT = 1000;

        /// <summary>
        /// 断线重连最大重试次数
        /// </summary>
        private const int MAX_RECONNECT_RETRY = 5;

        /// <summary>
        /// 断线重连间隔（毫秒）
        /// </summary>
        private const int RECONNECT_INTERVAL_MS = 2000;

        #endregion

        /// <summary>
        /// 类内日志辅助：统一走全局日志通道（标签固定 HikCameraDevice）。
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志等级</param>
        private void Log(string message, MessageLevel level)
        {
            GlobalCommData.ShowLog("HikCameraDevice", message, level);
        }

        #region 字段

        /// <summary>
        /// 设备句柄
        /// </summary>
        private IntPtr _deviceHandle = IntPtr.Zero;

        /// <summary>
        /// 是否正在采集
        /// </summary>
        private bool _isGrabbing = false;

        /// <summary>
        /// 接收图像线程
        /// </summary>
        private Thread _receiveThread = null;

        /// <summary>
        /// 线程同步锁对象
        /// </summary>
        private readonly object _lockObj = new object();

        /// <summary>
        /// 图像数据缓冲区
        /// </summary>
        private byte[] _dataBuffer = null;

        /// <summary>
        /// 当前图像信息
        /// </summary>
        private MV3D_LP_IMAGE_DATA _currentImageInfo = new MV3D_LP_IMAGE_DATA();

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _isDisposed = false;

        /// <summary>
        /// 显示窗口句柄（IntPtr.Zero 表示不显示）
        /// </summary>
        private IntPtr _displayWindowHandle = IntPtr.Zero;

        /// <summary>
        /// 图像数据回调对象
        /// </summary>
        private DeviceImageDataCallBack _imageDataCallBack = null;

        /// <summary>
        /// 异常回调对象
        /// </summary>
        private DeviceExceptionCallBack _exceptionCallBack = null;

        /// <summary>
        /// 是否正在执行断线重连
        /// </summary>
        private volatile bool _isReconnecting = false;

        /// <summary>
        /// 断线重连线程
        /// </summary>
        private Thread _reconnectThread = null;

        #endregion

        #region 属性

        /// <summary>
        /// 获取设备序列号
        /// </summary>
        public string SerialNumber { get; private set; }

        /// <summary>
        /// 获取设备型号名称
        /// </summary>
        public string ModelName { get; private set; }

        /// <summary>
        /// 获取设备 IP 地址
        /// </summary>
        public string IpAddress { get; private set; }

        /// <summary>
        /// 获取设备是否已打开
        /// </summary>
        public bool IsOpened
        {
            get { return _deviceHandle != IntPtr.Zero; }
        }

        /// <summary>
        /// 获取是否正在采集
        /// </summary>
        public bool IsGrabbing
        {
            get { return _isGrabbing; }
        }

        /// <summary>
        /// 获取当前图像宽度
        /// </summary>
        public int CurrentImageWidth
        {
            get { return (int)_currentImageInfo.nWidth; }
        }

        /// <summary>
        /// 获取当前图像高度
        /// </summary>
        public int CurrentImageHeight
        {
            get { return (int)_currentImageInfo.nHeight; }
        }

        /// <summary>
        /// 获取当前图像数据长度（字节）
        /// </summary>
        public uint CurrentImageDataLen
        {
            get { return _currentImageInfo.nDataLen; }
        }

        /// <summary>
        /// 获取当前图像帧号
        /// </summary>
        public uint CurrentFrameNum
        {
            get { return _currentImageInfo.nFrameNum; }
        }

        /// <summary>
        /// 获取当前图像数据的副本（线程安全）
        /// </summary>
        public byte[] CurrentImageData
        {
            get
            {
                lock (_lockObj)
                {
                    if (_dataBuffer == null || _currentImageInfo.nDataLen == 0)
                    {
                        return null;
                    }

                    byte[] copy = new byte[_currentImageInfo.nDataLen];
                    Array.Copy(_dataBuffer, 0, copy, 0, (int)_currentImageInfo.nDataLen);
                    return copy;
                }
            }
        }

        /// <summary>
        /// 获取当前图像信息副本（线程安全）
        /// </summary>
        public MV3D_LP_IMAGE_DATA CurrentImageInfo
        {
            get
            {
                lock (_lockObj)
                {
                    return _currentImageInfo;
                }
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 图像采集完成事件委托
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public delegate void ImageAcquiredEventHandler(object sender, EventArgs e);

        /// <summary>
        /// 图像采集完成事件，在每帧图像数据就绪时触发
        /// </summary>
        public event ImageAcquiredEventHandler ImageAcquired;

        /// <summary>
        /// 设备断线事件委托
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public delegate void DeviceDisconnectedEventHandler(object sender, EventArgs e);

        /// <summary>
        /// 设备断线事件，在检测到设备连接断开时触发
        /// </summary>
        public event DeviceDisconnectedEventHandler DeviceDisconnected;

        /// <summary>
        /// 设备重连成功事件委托
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public delegate void DeviceReconnectedEventHandler(object sender, EventArgs e);

        /// <summary>
        /// 设备重连成功事件
        /// </summary>
        public event DeviceReconnectedEventHandler DeviceReconnected;

        #endregion

        #region 构造与析构

        /// <summary>
        /// 初始化海康相机设备
        /// </summary>
        /// <param name="serialNumber">设备序列号</param>
        /// <param name="modelName">设备型号名称</param>
        /// <param name="ipAddress">设备 IP 地址</param>
        public HikCameraDevice(string serialNumber, string modelName, string ipAddress)
        {
            SerialNumber = serialNumber ?? throw new ArgumentNullException(nameof(serialNumber));
            ModelName = modelName ?? string.Empty;
            IpAddress = ipAddress ?? string.Empty;
            _dataBuffer = new byte[MAX_IMAGE_SIZE];
        }

        /// <summary>
        /// 析构函数，确保资源释放
        /// </summary>
        ~HikCameraDevice()
        {
            Dispose(false);
        }

        #endregion

        #region 公共方法 - 设备控制

        /// <summary>
        /// 通过 IP 地址打开设备
        /// </summary>
        /// <param name="ip">设备 IP 地址</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentNullException">IP 地址为空时抛出</exception>
        public bool OpenDeviceByIP(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                throw new ArgumentNullException(nameof(ip));
            }

            if (_deviceHandle != IntPtr.Zero)
            {
                return true;
            }

            int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceByIP(ref _deviceHandle, ip);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("通过IP打开设备失败", nRet);
                _deviceHandle = IntPtr.Zero;
                return false;
            }

            RegisterCallbacks();
            return true;
        }

        /// <summary>
        /// 通过序列号打开设备
        /// </summary>
        /// <returns>是否成功打开</returns>
        public bool OpenDeviceBySN()
        {
            return OpenDeviceBySN(SerialNumber);
        }

        /// <summary>
        /// 通过指定序列号打开设备
        /// </summary>
        /// <param name="sn">设备序列号</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentNullException">序列号为空时抛出</exception>
        public bool OpenDeviceBySN(string sn)
        {
            if (string.IsNullOrEmpty(sn))
            {
                throw new ArgumentNullException(nameof(sn));
            }

            if (_deviceHandle != IntPtr.Zero)
            {
                return true;
            }

            int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref _deviceHandle, sn);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("通过序列号打开设备失败", nRet);
                _deviceHandle = IntPtr.Zero;
                return false;
            }

            RegisterCallbacks();
            return true;
        }

        /// <summary>
        /// 关闭设备并停止采集
        /// </summary>
        public void CloseDevice()
        {
            // 先停止采集
            if (_isGrabbing)
            {
                StopGrabbing();
            }

            // 注销回调
            UnregisterCallbacks();

            if (_deviceHandle != IntPtr.Zero)
            {
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref _deviceHandle);
                _deviceHandle = IntPtr.Zero;
            }

            // 重置图像信息
            lock (_lockObj)
            {
                _currentImageInfo = new MV3D_LP_IMAGE_DATA();
            }
        }

        #endregion

        #region 公共方法 - 采集控制

        /// <summary>
        /// 开始连续采集
        /// </summary>
        /// <param name="displayWindowHandle">用于显示的窗口句柄，IntPtr.Zero 表示不显示</param>
        /// <returns>是否成功开始采集</returns>
        public bool StartGrabbing(IntPtr displayWindowHandle)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开，无法开始采集", 0);
                return false;
            }

            if (_isGrabbing)
            {
                return true;
            }

            _displayWindowHandle = displayWindowHandle;

            int nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(_deviceHandle);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("开始采集失败", nRet);
                return false;
            }

            _isGrabbing = true;

            // 启动接收线程
            _receiveThread = new Thread(ReceiveThreadProcess);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();

            return true;
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        /// <returns>是否成功停止采集</returns>
        public bool StopGrabbing()
        {
            if (!_isGrabbing)
            {
                return true;
            }

            // 设置标志位停止线程
            _isGrabbing = false;

            // 等待接收线程结束
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(1000);
                _receiveThread = null;
            }

            // 停止测量
            if (_deviceHandle != IntPtr.Zero)
            {
                int nRet = Mv3dLpSDK.MV3D_LP_StopMeasure(_deviceHandle);
                if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
                {
                    ShowErrorMsg("停止采集失败", nRet);
                    return false;
                }
            }

            lock (_lockObj)
            {
                _currentImageInfo.nFrameNum = 0;
            }

            return true;
        }

        /// <summary>
        /// 同步获取一帧图像数据（阻塞调用）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），0 表示使用默认值</param>
        /// <returns>是否成功获取图像</returns>
        public bool GetImage(uint timeoutMs = 0)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开，无法获取图像", 0);
                return false;
            }

            uint timeout = timeoutMs > 0 ? timeoutMs : DEFAULT_TIMEOUT;
            MV3D_LP_IMAGE_DATA imageData = new MV3D_LP_IMAGE_DATA();
            int nRet = Mv3dLpSDK.MV3D_LP_GetImage(_deviceHandle, imageData, timeout);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("获取图像失败", nRet);
                return false;
            }

            CopyImageData(imageData);
            return true;
        }

        /// <summary>
        /// 发送软触发指令采集一帧
        /// </summary>
        /// <returns>是否成功触发</returns>
        public bool SoftTrigger()
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开，无法软触发", 0);
                return false;
            }

            int nRet = Mv3dLpSDK.MV3D_LP_SoftTrigger(_deviceHandle);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("软触发失败", nRet);
                return false;
            }

            return true;
        }

        #endregion

        #region 公共方法 - 参数操作

        /// <summary>
        /// 获取布尔类型参数
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>参数当前值，失败返回 false</returns>
        /// <exception cref="InvalidOperationException">设备未打开时抛出</exception>
        /// <exception cref="ArgumentNullException">参数名为空时抛出</exception>
        public bool GetBoolParam(string paramName)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("设备未打开");
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            param.enParamType = Mv3dLpSDK.ParamType_Bool;

            int nRet = Mv3dLpSDK.MV3D_LP_GetParam(_deviceHandle, paramName, param);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("获取布尔参数失败 [" + paramName + "]", nRet);
                return false;
            }

            return param.get_boolparam() != 0;
        }

        /// <summary>
        /// 设置布尔类型参数
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="value">参数值</param>
        /// <returns>是否成功设置</returns>
        /// <exception cref="ArgumentNullException">参数名为空时抛出</exception>
        public bool SetBoolParam(string paramName, bool value)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开", 0);
                return false;
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            param.enParamType = Mv3dLpSDK.ParamType_Bool;
            param.set_boolparam(value ? 1 : 0);

            int nRet = Mv3dLpSDK.MV3D_LP_SetParam(_deviceHandle, paramName, param);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("设置布尔参数失败 [" + paramName + "]", nRet);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取整数类型参数
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>参数当前值，失败返回 0</returns>
        /// <exception cref="InvalidOperationException">设备未打开时抛出</exception>
        /// <exception cref="ArgumentNullException">参数名为空时抛出</exception>
        public int GetIntParam(string paramName)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("设备未打开");
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            param.enParamType = Mv3dLpSDK.ParamType_Int;

            int nRet = Mv3dLpSDK.MV3D_LP_GetParam(_deviceHandle, paramName, param);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("获取整数参数失败 [" + paramName + "]", nRet);
                return 0;
            }

            MV3D_LP_INTPARAM intParam = param.get_intparam();
            return (int)intParam.nCurValue;
        }

        /// <summary>
        /// 设置整数类型参数
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="value">参数值</param>
        /// <returns>是否成功设置</returns>
        /// <exception cref="ArgumentNullException">参数名为空时抛出</exception>
        public bool SetIntParam(string paramName, int value)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开", 0);
                return false;
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            param.enParamType = Mv3dLpSDK.ParamType_Int;

            MV3D_LP_INTPARAM intParam = new MV3D_LP_INTPARAM();
            intParam.nCurValue = (uint)value;
            param.set_intparam(intParam);

            int nRet = Mv3dLpSDK.MV3D_LP_SetParam(_deviceHandle, paramName, param);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("设置整数参数失败 [" + paramName + "]", nRet);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取浮点类型参数
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>参数当前值，失败返回 NaN</returns>
        /// <exception cref="InvalidOperationException">设备未打开时抛出</exception>
        /// <exception cref="ArgumentNullException">参数名为空时抛出</exception>
        public float GetFloatParam(string paramName)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("设备未打开");
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            param.enParamType = Mv3dLpSDK.ParamType_Float;

            int nRet = Mv3dLpSDK.MV3D_LP_GetParam(_deviceHandle, paramName, param);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("获取浮点参数失败 [" + paramName + "]", nRet);
                return float.NaN;
            }

            MV3D_LP_FLOATPARAM floatParam = param.get_floatparam();
            return floatParam.fCurValue;
        }

        /// <summary>
        /// 设置浮点类型参数
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="value">参数值</param>
        /// <returns>是否成功设置</returns>
        /// <exception cref="ArgumentNullException">参数名为空时抛出</exception>
        public bool SetFloatParam(string paramName, float value)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开", 0);
                return false;
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            param.enParamType = Mv3dLpSDK.ParamType_Float;

            MV3D_LP_FLOATPARAM floatParam = new MV3D_LP_FLOATPARAM();
            floatParam.fCurValue = value;
            param.set_floatparam(floatParam);

            int nRet = Mv3dLpSDK.MV3D_LP_SetParam(_deviceHandle, paramName, param);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("设置浮点参数失败 [" + paramName + "]", nRet);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取曝光时间
        /// </summary>
        /// <returns>曝光时间值（微秒），失败返回 NaN</returns>
        public float GetExposureTime()
        {
            return GetFloatParam(Mv3dLpSDK.MV3D_LP_FLOAT_EXPOSURETIME);
        }

        /// <summary>
        /// 设置曝光时间
        /// </summary>
        /// <param name="exposureTimeUs">曝光时间（微秒）</param>
        /// <returns>是否成功设置</returns>
        public bool SetExposureTime(float exposureTimeUs)
        {
            return SetFloatParam(Mv3dLpSDK.MV3D_LP_FLOAT_EXPOSURETIME, exposureTimeUs);
        }

        /// <summary>
        /// 执行设备命令（如 Execute 命令）
        /// </summary>
        /// <param name="commandKey">命令键名</param>
        /// <returns>是否成功执行</returns>
        /// <exception cref="ArgumentNullException">命令键名为空时抛出</exception>
        public bool ExecuteCommand(string commandKey)
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                ShowErrorMsg("设备未打开", 0);
                return false;
            }

            if (string.IsNullOrEmpty(commandKey))
            {
                throw new ArgumentNullException(nameof(commandKey));
            }

            int nRet = Mv3dLpSDK.MV3D_LP_Execute(_deviceHandle, commandKey);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("执行命令失败 [" + commandKey + "]", nRet);
                return false;
            }

            return true;
        }

        #endregion

        #region 公共方法 - 图像保存

        /// <summary>
        /// 保存当前图像到文件
        /// </summary>
        /// <param name="fileType">文件类型（参考 SDK 定义）</param>
        /// <param name="fileName">保存的文件路径</param>
        /// <returns>是否成功保存</returns>
        public bool SaveImage(uint fileType, string fileName)
        {
            if (!_isGrabbing)
            {
                ShowErrorMsg("未开始采集，无法保存图像", 0);
                return false;
            }

            lock (_lockObj)
            {
                if (_currentImageInfo.nFrameNum == 0)
                {
                    ShowErrorMsg("无图像数据可保存", 0);
                    return false;
                }

                int nRet = Mv3dLpSDK.MV3D_LP_SaveImage(_currentImageInfo, fileType, fileName);
                if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
                {
                    ShowErrorMsg("保存图像失败", nRet);
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region 公共方法 - 资源释放

        /// <summary>
        /// 释放设备持有的所有资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region 私有方法 - 资源释放

        /// <summary>
        /// 释放资源的核心实现
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // 停止重连线程
                _isReconnecting = false;
                if (_reconnectThread != null && _reconnectThread.IsAlive)
                {
                    _reconnectThread.Join(3000);
                    _reconnectThread = null;
                }

                CloseDevice();
                _dataBuffer = null;
            }

            _isDisposed = true;
        }

        #endregion

        #region 私有方法 - 回调注册

        /// <summary>
        /// 注册图像回调和异常回调
        /// </summary>
        private void RegisterCallbacks()
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // 注册图像数据回调
                _imageDataCallBack = new DeviceImageDataCallBack(this);
                _imageDataCallBack.Register(_deviceHandle);

                // 注册异常回调
                _exceptionCallBack = new DeviceExceptionCallBack(this);
                _exceptionCallBack.Register(_deviceHandle);
            }
            catch (Exception ex)
            {
                Log("注册回调失败 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>
        /// 注销图像回调和异常回调
        /// </summary>
        private void UnregisterCallbacks()
        {
            if (_deviceHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (_imageDataCallBack != null)
                {
                    _imageDataCallBack.UnRegister(_deviceHandle);
                    _imageDataCallBack = null;
                }

                if (_exceptionCallBack != null)
                {
                    _exceptionCallBack.UnRegister(_deviceHandle);
                    _exceptionCallBack = null;
                }
            }
            catch (Exception ex)
            {
                Log("注销回调失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        #endregion

        #region 私有方法 - 采集线程

        /// <summary>
        /// 接收图像线程处理函数，循环获取图像并触发事件
        /// </summary>
        private void ReceiveThreadProcess()
        {
            uint timeOut = 50;

            while (_isGrabbing)
            {
                MV3D_LP_IMAGE_DATA imageData = new MV3D_LP_IMAGE_DATA();
                int nRet = Mv3dLpSDK.MV3D_LP_GetImage(_deviceHandle, imageData, timeOut);

                if (nRet == (int)Mv3dLpSDK.MV3D_LP_OK)
                {
                    try
                    {
                        // 显示图像到指定窗口
                        if (_displayWindowHandle != IntPtr.Zero)
                        {
                            int dispRet = Mv3dLpSDK.MV3D_LP_DisplayImage(
                                imageData, _displayWindowHandle,
                                Mv3dLpSDK.DisplayType_Auto, 0, 0);
                            if (dispRet != (int)Mv3dLpSDK.MV3D_LP_OK)
                            {
                                Log("显示图像失败 " + dispRet, MessageLevel.Warning);
                            }
                        }

                        // 拷贝图像数据到内部缓冲区
                        CopyImageData(imageData);

                        // 触发图像采集完成事件
                        ImageAcquired?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Log("图像处理异常 " + ex.Message, MessageLevel.Error);
                    }
                }
                // 超时或无数据时继续循环
            }
        }

        /// <summary>
        /// 拷贝图像数据到内部缓冲区（线程安全）
        /// </summary>
        /// <param name="imageData">SDK 返回的图像数据结构</param>
        private void CopyImageData(MV3D_LP_IMAGE_DATA imageData)
        {
            lock (_lockObj)
            {
                _currentImageInfo.nWidth = imageData.nWidth;
                _currentImageInfo.nHeight = imageData.nHeight;
                _currentImageInfo.nDataLen = imageData.nDataLen;
                _currentImageInfo.enImageType = imageData.enImageType;
                _currentImageInfo.nFrameNum = imageData.nFrameNum;
                _currentImageInfo.nIntensityDataLen = imageData.nIntensityDataLen;
                _currentImageInfo.pIntensityData = imageData.pIntensityData;
                _currentImageInfo.fXScale = imageData.fXScale;
                _currentImageInfo.fYScale = imageData.fYScale;
                _currentImageInfo.fZScale = imageData.fZScale;
                _currentImageInfo.nXOffset = imageData.nXOffset;
                _currentImageInfo.nYOffset = imageData.nYOffset;
                _currentImageInfo.nZOffset = imageData.nZOffset;

                // 确保缓冲区足够大
                if (_dataBuffer == null || _dataBuffer.Length < imageData.nDataLen)
                {
                    _dataBuffer = new byte[imageData.nDataLen];
                }

                // 从非托管内存拷贝图像数据
                if (imageData.nDataLen > 0 && imageData.pData != IntPtr.Zero)
                {
                    _currentImageInfo.pData = Marshal.UnsafeAddrOfPinnedArrayElement(_dataBuffer, 0);
                    Marshal.Copy(imageData.pData, _dataBuffer, 0, (int)imageData.nDataLen);
                }
            }
        }

        #endregion

        #region 私有方法 - 断线重连

        /// <summary>
        /// 启动断线重连流程（在异常回调中调用）
        /// </summary>
        private void StartReconnect()
        {
            if (_isReconnecting)
            {
                return;
            }

            _isReconnecting = true;

            // 通知外部断线
            try
            {
                DeviceDisconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log("触发断线事件异常 " + ex.Message, MessageLevel.Warning);
            }

            // 启动重连线程
            _reconnectThread = new Thread(ReconnectThreadProcess);
            _reconnectThread.IsBackground = true;
            _reconnectThread.Start();
        }

        /// <summary>
        /// 断线重连线程处理函数
        /// </summary>
        private void ReconnectThreadProcess()
        {
            bool wasGrabbing = _isGrabbing;
            IntPtr savedDisplayHandle = _displayWindowHandle;

            // 先关闭设备
            try
            {
                _isGrabbing = false;
                if (_receiveThread != null && _receiveThread.IsAlive)
                {
                    _receiveThread.Join(1000);
                    _receiveThread = null;
                }

                UnregisterCallbacks();

                if (_deviceHandle != IntPtr.Zero)
                {
                    Mv3dLpSDK.MV3D_LP_CloseDevice(ref _deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Log("关闭设备异常 " + ex.Message, MessageLevel.Warning);
            }

            // 循环尝试重连
            for (int retry = 0; retry < MAX_RECONNECT_RETRY; retry++)
            {
                if (!_isReconnecting || _isDisposed)
                {
                    break;
                }

                Thread.Sleep(RECONNECT_INTERVAL_MS);

                try
                {
                    bool opened = false;

                    // 优先使用 IP 地址重连
                    if (!string.IsNullOrEmpty(IpAddress))
                    {
                        int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceByIP(ref _deviceHandle, IpAddress);
                        opened = (nRet == (int)Mv3dLpSDK.MV3D_LP_OK);
                    }

                    // IP 重连失败则使用序列号
                    if (!opened && !string.IsNullOrEmpty(SerialNumber))
                    {
                        int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref _deviceHandle, SerialNumber);
                        opened = (nRet == (int)Mv3dLpSDK.MV3D_LP_OK);
                    }

                    if (opened)
                    {
                        RegisterCallbacks();

                        // 如果之前在采集中，恢复采集
                        if (wasGrabbing)
                        {
                            _displayWindowHandle = savedDisplayHandle;
                            int startRet = Mv3dLpSDK.MV3D_LP_StartMeasure(_deviceHandle);
                            if (startRet == (int)Mv3dLpSDK.MV3D_LP_OK)
                            {
                                _isGrabbing = true;
                                _receiveThread = new Thread(ReceiveThreadProcess);
                                _receiveThread.IsBackground = true;
                                _receiveThread.Start();
                            }
                        }

                        _isReconnecting = false;

                        // 通知外部重连成功
                        try
                        {
                            DeviceReconnected?.Invoke(this, EventArgs.Empty);
                        }
                        catch (Exception ex)
                        {
                            Log("触发重连成功事件异常 " + ex.Message, MessageLevel.Warning);
                        }

                        Log("设备重连成功", MessageLevel.Info);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log("重连尝试 " + (retry + 1) + " 异常 " + ex.Message, MessageLevel.Warning);
                }
            }

            _isReconnecting = false;
            Log("设备重连失败 已达最大重试次数", MessageLevel.Error);
        }

        #endregion

        #region 私有方法 - 错误处理

        /// <summary>
        /// 显示错误信息（UI线程安全）
        /// </summary>
        /// <param name="message">错误描述</param>
        /// <param name="errorCode">SDK 返回的错误码</param>
        private void ShowErrorMsg(string message, int errorCode)
        {
            string errorMsg;
            if (errorCode == Mv3dLpSDK.MV3D_LP_OK)
            {
                errorMsg = message;
            }
            else
            {
                errorMsg = message + ": Error =" + String.Format("{0:X}", errorCode);
            }

            switch (errorCode)
            {
                case Mv3dLpSDK.MV3D_LP_E_HANDLE:
                    errorMsg += " 错误或无效句柄 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_SUPPORT:
                    errorMsg += " 不支持的功能 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_BUFOVER:
                    errorMsg += " 缓存已满 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_CALLORDER:
                    errorMsg += " 函数调用顺序错误 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_PARAMETER:
                    errorMsg += " 参数不正确 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_RESOURCE:
                    errorMsg += " 申请资源失败 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_NODATA:
                    errorMsg += " 无数据 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_PRECONDITION:
                    errorMsg += " 前置条件错误，或运行环境已改变 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_VERSION:
                    errorMsg += " 版本不匹配 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_NOENOUGH_BUF:
                    errorMsg += " 内存不足 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_ABNORMAL_IMAGE:
                    errorMsg += " 图像异常 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_LOAD_LIBRARY:
                    errorMsg += " 加载 DLL 错误 ";
                    break;
                case Mv3dLpSDK.MV3D_LP_E_UNKNOW:
                    errorMsg += " 未知错误 ";
                    break;
            }

            // SDK 错误可能在后台采集线程回调，改为错误日志记录，不弹模态框（避免阻塞线程导致卡死）。
            try
            {
                GlobalCommData.ShowLog("HikCameraDevice", errorMsg, MessageLevel.Error);
            }
            catch
            {
                Console.WriteLine(errorMsg);
            }
        }

        #endregion

        #region 嵌套类 - SDK 回调实现

        /// <summary>
        /// 图像数据回调实现类，接收 SDK 推送的图像数据
        /// </summary>
        private class DeviceImageDataCallBack : ImageDataCallBack
        {
            /// <summary>
            /// 所属设备实例的弱引用
            /// </summary>
            private readonly WeakReference _deviceRef;

            /// <summary>
            /// 初始化图像数据回调
            /// </summary>
            /// <param name="device">所属设备实例</param>
            public DeviceImageDataCallBack(HikCameraDevice device)
            {
                _deviceRef = new WeakReference(device);
            }

            /// <summary>
            /// 图像数据回调函数，由 SDK 在图像就绪时调用
            /// </summary>
            /// <param name="pstImageData">图像数据</param>
            public override void run(MV3D_LP_IMAGE_DATA pstImageData)
            {
                HikCameraDevice device = _deviceRef.Target as HikCameraDevice;
                if (device != null && device._isGrabbing)
                {
                    try
                    {
                        device.CopyImageData(pstImageData);

                        // 显示图像
                        if (device._displayWindowHandle != IntPtr.Zero)
                        {
                            Mv3dLpSDK.MV3D_LP_DisplayImage(
                                pstImageData, device._displayWindowHandle,
                                Mv3dLpSDK.DisplayType_Auto, 0, 0);
                        }

                        device.ImageAcquired?.Invoke(device, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        device.Log("图像回调处理异常 " + ex.Message, MessageLevel.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 异常回调实现类，接收 SDK 推送的设备异常信息并触发断线重连
        /// </summary>
        private class DeviceExceptionCallBack : ExceptionCallBack
        {
            /// <summary>
            /// 所属设备实例的弱引用
            /// </summary>
            private readonly WeakReference _deviceRef;

            /// <summary>
            /// 初始化异常回调
            /// </summary>
            /// <param name="device">所属设备实例</param>
            public DeviceExceptionCallBack(HikCameraDevice device)
            {
                _deviceRef = new WeakReference(device);
            }

            /// <summary>
            /// 异常回调函数，由 SDK 在设备异常时调用
            /// </summary>
            /// <param name="pstExceptInfo">异常信息</param>
            public override void run(MV3D_LP_EXCEPTION_INFO pstExceptInfo)
            {
                HikCameraDevice device = _deviceRef.Target as HikCameraDevice;
                if (device != null)
                {
                    try
                    {
                        device.Log("设备异常 " + pstExceptInfo.ToString(), MessageLevel.Error);
                        device.StartReconnect();
                    }
                    catch (Exception ex)
                    {
                        device.Log("异常回调处理失败 " + ex.Message, MessageLevel.Error);
                    }
                }
            }
        }

        #endregion
    }
}
