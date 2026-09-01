using AdaWeldSystem.LineLaserCam.HikRobotCam.ApiCore;
using AdaWeldSystem.Comm;
using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace AdaWeldSystem.LineLaserCam.HikRobotCam
{
    /// <summary>
    /// 海康相机运行控制类（单例模式），提供相机打开、连续采集、Mat 输出和曝光控制
    /// </summary>
    public class HikCameraRun : IDisposable
    {
        private const string Tag = "HikCameraRun";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region 常量

        /// <summary>
        /// 采集循环轮询间隔（毫秒）
        /// </summary>
        private const int ACQUISITION_POLL_INTERVAL_MS = 10;

        /// <summary>
        /// 采集任务等待结束超时（毫秒）
        /// </summary>
        private const int ACQUISITION_WAIT_TIMEOUT_MS = 2000;

        #endregion

        #region 字段

        /// <summary>
        /// 单例实例
        /// </summary>
        private static HikCameraRun _instance = null;

        /// <summary>
        /// 单例双重检查锁对象
        /// </summary>
        private static readonly object _instanceLock = new object();

        /// <summary>
        /// 相机管理器
        /// </summary>
        private HikCameraManager _cameraManager = null;

        /// <summary>
        /// 当前相机设备
        /// </summary>
        private HikCameraDevice _cameraDevice = null;

        /// <summary>
        /// 当前图像 Mat
        /// </summary>
        private Mat _currentMat = null;

        /// <summary>
        /// Mat 访问同步锁
        /// </summary>
        private readonly object _matLock = new object();

        /// <summary>
        /// 是否已初始化相机
        /// </summary>
        private bool _isCameraInitialized = false;

        /// <summary>
        /// 是否正在运行采集
        /// </summary>
        private bool _isRun = false;

        /// <summary>
        /// 采集任务
        /// </summary>
        private Task _acquisitionTask = null;

        /// <summary>
        /// 采集取消令牌源
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = null;

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _isDisposed = false;

        /// <summary>
        /// 是否实时显示模式
        /// </summary>
        private bool _isLiveMode = false;

        #endregion

        #region 属性

        /// <summary>
        /// 获取单例实例（双重检查锁，线程安全）
        /// </summary>
        public static HikCameraRun Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HikCameraRun();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取当前图像 Mat（线程安全，返回克隆副本）
        /// </summary>
        public Mat CurrentMat
        {
            get
            {
                lock (_matLock)
                {
                    if (_currentMat == null)
                    {
                        return null;
                    }
                    // 返回克隆副本以避免外部修改影响内部数据
                    return _currentMat.Clone();
                }
            }
        }

        /// <summary>
        /// 获取是否正在运行采集
        /// </summary>
        public bool IsRun
        {
            get { return _isRun; }
        }

        /// <summary>
        /// 获取相机是否已初始化成功
        /// </summary>
        public bool IsCameraInitialized
        {
            get { return _isCameraInitialized; }
        }

        /// <summary>
        /// 获取或设置是否实时显示模式
        /// </summary>
        public bool IsLiveMode
        {
            get { return _isLiveMode; }
            set { _isLiveMode = value; }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 图像采集完成事件委托（Mat 类型输出）
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public delegate void AcquisitionMatCompletedEventHandler(object sender, EventArgs e);

        /// <summary>
        /// 图像采集完成静态事件，每帧 Mat 就绪时触发，供外部订阅使用
        /// </summary>
        public static event AcquisitionMatCompletedEventHandler AcqusitionMatCompletedEvent;

        #endregion

        #region 构造与析构

        /// <summary>
        /// 私有构造函数（单例模式）
        /// </summary>
        private HikCameraRun()
        {
        }

        /// <summary>
        /// 析构函数，确保资源释放
        /// </summary>
        ~HikCameraRun()
        {
            Dispose(false);
        }

        #endregion

        #region 公共方法 - 相机打开

        /// <summary>通过 IP 地址打开相机传感器</summary>
        public void OpenSensorByIP(string ip)
        {
            OpenSensorCore(ip,
                createDevice: () => _cameraManager.CreateDevice(0),
                createFailMsg: "打开相机失败: 创建相机设备失败",
                openDevice: (val) => _cameraDevice.OpenDeviceByIP(val),
                openFailMsg: (val) => "打开相机失败: 通过IP打开相机设备失败: " + val);
        }

        /// <summary>通过序列号打开相机传感器</summary>
        public void OpenSensorBySN(string serialNumber)
        {
            OpenSensorCore(serialNumber,
                createDevice: () => _cameraManager.CreateDeviceBySerialNumber(serialNumber),
                createFailMsg: "打开相机失败: 未找到指定序列号的相机: " + serialNumber,
                openDevice: (val) => _cameraDevice.OpenDeviceBySN(val),
                openFailMsg: (val) => "打开相机失败: 打开相机设备失败");
        }

        /// <summary>打开相机核心逻辑，提取 OpenSensorByIP/OpenSensorBySN 的公共代码</summary>
        private void OpenSensorCore(string param,
            Func<HikCameraDevice> createDevice, string createFailMsg,
            Func<string, bool> openDevice, Func<string, string> openFailMsg)
        {
            if (string.IsNullOrEmpty(param))
                throw new ArgumentNullException(nameof(param));

            try
            {
                CloseSensor();

                _cameraManager = new HikCameraManager();
                uint devCount = _cameraManager.EnumerateDevices();
                if (devCount == 0)
                {
                    Log("打开相机失败 未找到海康相机设备", MessageLevel.Error);
                    _isCameraInitialized = false;
                    return;
                }

                _cameraDevice = createDevice();
                if (_cameraDevice == null)
                {
                    Log(createFailMsg, MessageLevel.Error);
                    _isCameraInitialized = false;
                    return;
                }

                if (!openDevice(param))
                {
                    Log(openFailMsg(param), MessageLevel.Error);
                    _isCameraInitialized = false;
                    return;
                }

                _cameraDevice.ImageAcquired += OnImageAcquired;
                _isCameraInitialized = true;
            }
            catch (Exception ex)
            {
                Log("打开相机异常 " + ex.Message, MessageLevel.Error);
                _isCameraInitialized = false;
            }
        }

        #endregion

        #region 公共方法 - 采集控制

        /// <summary>
        /// 开始连续采集
        /// </summary>
        public void SensorRunContinue()
        {
            if (!_isCameraInitialized)
            {
                return;
            }

            if (_isRun)
            {
                return;
            }

            try
            {
                _isRun = true;
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;

                // 启动采集任务
                _acquisitionTask = new Task(() =>
                {
                    AcquisitionLoop(token);
                }, token);
                _acquisitionTask.Start();
            }
            catch (Exception ex)
            {
                Log("开始采集异常 " + ex.Message, MessageLevel.Error);
                _isRun = false;
            }
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        public void SensorStop()
        {
            if (!_isCameraInitialized)
            {
                return;
            }

            if (!_isRun)
            {
                return;
            }

            try
            {
                _isRun = false;

                // 取消采集任务
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource = null;
                }

                // 等待任务结束
                if (_acquisitionTask != null)
                {
                    _acquisitionTask.Wait(ACQUISITION_WAIT_TIMEOUT_MS);
                    _acquisitionTask = null;
                }

                // 停止设备采集
                if (_cameraDevice != null)
                {
                    _cameraDevice.StopGrabbing();
                }
            }
            catch (Exception ex)
            {
                Log("停止采集异常 " + ex.Message, MessageLevel.Error);
            }
        }

        #endregion

        #region 公共方法 - 参数控制

        /// <summary>
        /// 设置曝光时间
        /// </summary>
        /// <param name="exposureTimeUs">曝光时间（微秒）</param>
        /// <returns>是否成功设置</returns>
        public bool SetExposureTime(float exposureTimeUs)
        {
            if (_cameraDevice == null || !_cameraDevice.IsOpened)
            {
                return false;
            }

            return _cameraDevice.SetExposureTime(exposureTimeUs);
        }

        /// <summary>
        /// 获取曝光时间
        /// </summary>
        /// <returns>曝光时间（微秒），失败返回 NaN</returns>
        public float GetExposureTime()
        {
            if (_cameraDevice == null || !_cameraDevice.IsOpened)
            {
                return float.NaN;
            }

            return _cameraDevice.GetExposureTime();
        }

        #endregion

        #region 公共方法 - 资源释放

        /// <summary>
        /// 释放所有资源
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
                SensorStop();
                CloseSensor();

                lock (_matLock)
                {
                    if (_currentMat != null)
                    {
                        _currentMat.Dispose();
                        _currentMat = null;
                    }
                }

                if (_cameraManager != null)
                {
                    _cameraManager.Dispose();
                    _cameraManager = null;
                }
            }

            _isDisposed = true;
        }

        /// <summary>
        /// 关闭当前相机并释放相关资源
        /// </summary>
        private void CloseSensor()
        {
            if (_cameraDevice != null)
            {
                _cameraDevice.ImageAcquired -= OnImageAcquired;
                _cameraDevice.CloseDevice();
                _cameraDevice.Dispose();
                _cameraDevice = null;
            }

            if (_cameraManager != null)
            {
                _cameraManager.Dispose();
                _cameraManager = null;
            }

            _isCameraInitialized = false;
        }

        #endregion

        #region 私有方法 - 采集循环

        /// <summary>
        /// 采集循环（在 Task 中运行），启动设备采集并维持运行状态
        /// </summary>
        /// <param name="token">取消令牌</param>
        private void AcquisitionLoop(CancellationToken token)
        {
            try
            {
                // 开始设备采集
                bool started = _cameraDevice.StartGrabbing(IntPtr.Zero);
                if (!started)
                {
                    _isRun = false;
                    return;
                }

                // 循环等待取消
                while (_isRun && !token.IsCancellationRequested)
                {
                    // 图像数据通过 ImageAcquired 事件回调处理
                    // 此处仅做循环等待和状态检查
                    Thread.Sleep(ACQUISITION_POLL_INTERVAL_MS);
                }
            }
            catch (Exception ex)
            {
                Log("采集循环异常 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                if (_cameraDevice != null)
                {
                    _cameraDevice.StopGrabbing();
                }
                _isRun = false;
            }
        }

        #endregion

        #region 私有方法 - 事件处理

        /// <summary>
        /// 图像采集完成事件处理，将 SDK 图像数据转换为 EmguCV Mat
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnImageAcquired(object sender, EventArgs e)
        {
            try
            {
                if (_cameraDevice == null)
                {
                    return;
                }

                // 获取图像数据和图像信息
                MV3D_LP_IMAGE_DATA imageInfo = _cameraDevice.CurrentImageInfo;
                byte[] imageData = _cameraDevice.CurrentImageData;

                if (imageData == null || imageData.Length == 0)
                {
                    return;
                }

                // 转换为 Mat
                Mat newMat = ConvertToMat(imageInfo, imageData);

                if (newMat != null)
                {
                    lock (_matLock)
                    {
                        if (_currentMat != null)
                        {
                            _currentMat.Dispose();
                        }
                        _currentMat = newMat;
                    }

                    // 触发静态采集完成事件
                    AcqusitionMatCompletedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Log("图像处理异常 " + ex.Message, MessageLevel.Error);
            }
        }

        #endregion

        #region 私有方法 - 图像转换

        /// <summary>
        /// 将海康 SDK 图像数据转换为 EmguCV Mat 对象
        /// </summary>
        /// <param name="imageInfo">SDK 图像信息结构体</param>
        /// <param name="imageData">图像字节数据</param>
        /// <returns>转换后的 Mat 对象，失败返回 null</returns>
        private Mat ConvertToMat(MV3D_LP_IMAGE_DATA imageInfo, byte[] imageData)
        {
            if (imageInfo.nWidth == 0 || imageInfo.nHeight == 0 || imageData == null)
            {
                return null;
            }

            try
            {
                int width = (int)imageInfo.nWidth;
                int height = (int)imageInfo.nHeight;
                Mat mat = null;

                // 根据图像类型判断数据格式并创建对应 Mat
                switch (imageInfo.enImageType)
                {
                    case 0: // Mono8 / 灰度图
                        mat = new Mat(height, width, DepthType.Cv8U, 1);
                        break;

                    case 1: // Mono16
                        mat = new Mat(height, width, DepthType.Cv16U, 1);
                        break;

                    case 2: // RGB24
                        mat = new Mat(height, width, DepthType.Cv8U, 3);
                        break;

                    case 3: // RGB48
                        mat = new Mat(height, width, DepthType.Cv16U, 3);
                        break;

                    case 4: // BGR24
                        mat = new Mat(height, width, DepthType.Cv8U, 3);
                        break;

                    case 5: // BGR48
                        mat = new Mat(height, width, DepthType.Cv16U, 3);
                        break;

                    default:
                        // 默认按单通道 8 位灰度处理
                        mat = new Mat(height, width, DepthType.Cv8U, 1);
                        break;
                }

                // 拷贝数据到 Mat
                if (mat != null)
                {
                    int dataSize = (int)Math.Min(imageData.Length, mat.Step * height);
                    if (dataSize > 0)
                    {
                        mat.SetTo(imageData);
                    }
                }

                return mat;
            }
            catch (Exception ex)
            {
                Log("转换 Mat 异常 " + ex.Message, MessageLevel.Error);
                return null;
            }
        }

        #endregion
    }
}
