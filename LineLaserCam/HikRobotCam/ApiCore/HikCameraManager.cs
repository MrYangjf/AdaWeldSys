using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AdaWeldSystem.Comm;



namespace AdaWeldSystem.LineLaserCam.HikRobotCam.ApiCore
{
    /// <summary>
    /// 海康机器人3D相机管理器，负责设备枚举、创建和生命周期管理
    /// </summary>
    public class HikCameraManager : IDisposable
    {
        #region 常量

        /// <summary>
        /// 默认获取图像超时时间（毫秒）
        /// </summary>
        private const uint DEFAULT_TIMEOUT = 1000;

        #endregion

        #region 字段

        /// <summary>
        /// 管理的相机设备列表
        /// </summary>
        private readonly List<HikCameraDevice> _cameraDevices = new List<HikCameraDevice>();

        /// <summary>
        /// 设备信息列表（SDK枚举结果）
        /// </summary>
        private MV3D_LP_DEVICE_INFO_VECTOR _deviceInfoVector;

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _isDisposed = false;

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前管理的相机设备列表（只读副本）
        /// </summary>
        public IReadOnlyList<HikCameraDevice> CameraDevices
        {
            get { return _cameraDevices.AsReadOnly(); }
        }

        /// <summary>
        /// 获取已枚举的设备数量
        /// </summary>
        public uint DeviceCount { get; private set; } = 0;

        #endregion

        #region 构造与析构

        /// <summary>
        /// 初始化海康相机管理器
        /// </summary>
        public HikCameraManager()
        {
            _deviceInfoVector = new MV3D_LP_DEVICE_INFO_VECTOR(0);
        }

        /// <summary>
        /// 析构函数，确保资源释放
        /// </summary>
        ~HikCameraManager()
        {
            Dispose(false);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 枚举网络中的海康3D相机设备
        /// </summary>
        /// <returns>枚举到的设备数量，失败返回 0</returns>
        public uint EnumerateDevices()
        {
            // 先清理已有设备列表
            ClearDevices();

            uint devNum = 0;
            int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref devNum);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("枚举设备数量失败", nRet);
                return 0;
            }

            DeviceCount = devNum;
            if (devNum == 0)
            {
                return 0;
            }

            // 创建设备信息向量并填充
            _deviceInfoVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)devNum);
            for (uint i = 0; i < devNum; i++)
            {
                _deviceInfoVector.Add(new MV3D_LP_DEVICE_INFO());
            }

            // 获取设备列表
            uint actualNum = 0;
            nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(_deviceInfoVector[0], devNum, ref actualNum);
            if (nRet != (int)Mv3dLpSDK.MV3D_LP_OK)
            {
                ShowErrorMsg("获取设备列表失败", nRet);
                DeviceCount = 0;
                return 0;
            }

            DeviceCount = actualNum;
            return actualNum;
        }

        /// <summary>
        /// 根据索引创建设备封装对象（不打开设备）
        /// </summary>
        /// <param name="index">设备索引（从0开始）</param>
        /// <returns>相机设备对象</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出范围时抛出</exception>
        public HikCameraDevice CreateDevice(int index)
        {
            if (index < 0 || index >= DeviceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "设备索引超出范围");
            }

            MV3D_LP_DEVICE_INFO deviceInfo = _deviceInfoVector[index];
            string serialNumber = deviceInfo.chSerialNumber.TrimEnd('\0');
            string modelName = deviceInfo.chModelName.TrimEnd('\0');
            string ipAddress = deviceInfo.chCurrentIp.TrimEnd('\0');

            HikCameraDevice camera = new HikCameraDevice(serialNumber, modelName, ipAddress);
            _cameraDevices.Add(camera);

            return camera;
        }

        /// <summary>
        /// 根据序列号创建设备封装对象
        /// </summary>
        /// <param name="serialNumber">设备序列号</param>
        /// <returns>相机设备对象，若未找到则返回 null</returns>
        /// <exception cref="ArgumentNullException">序列号为空时抛出</exception>
        public HikCameraDevice CreateDeviceBySerialNumber(string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber))
            {
                throw new ArgumentNullException(nameof(serialNumber));
            }

            // 若尚未枚举设备则先执行枚举
            if (DeviceCount == 0)
            {
                EnumerateDevices();
            }

            for (int i = 0; i < DeviceCount; i++)
            {
                string sn = _deviceInfoVector[i].chSerialNumber.TrimEnd('\0');
                if (sn == serialNumber)
                {
                    return CreateDevice(i);
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定索引的设备信息结构体
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns>设备信息结构体</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出范围时抛出</exception>
        public MV3D_LP_DEVICE_INFO GetDeviceInfo(int index)
        {
            if (index < 0 || index >= DeviceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "设备索引超出范围");
            }

            return _deviceInfoVector[index];
        }

        /// <summary>
        /// 获取指定索引设备的序列号
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns>序列号字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出范围时抛出</exception>
        public string GetDeviceSerialNumber(int index)
        {
            if (index < 0 || index >= DeviceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "设备索引超出范围");
            }

            return _deviceInfoVector[index].chSerialNumber.TrimEnd('\0');
        }

        /// <summary>
        /// 获取指定索引设备的型号名称
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns>型号名称字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出范围时抛出</exception>
        public string GetDeviceModelName(int index)
        {
            if (index < 0 || index >= DeviceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "设备索引超出范围");
            }

            return _deviceInfoVector[index].chModelName.TrimEnd('\0');
        }

        /// <summary>
        /// 获取指定索引设备的 IP 地址
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <returns>IP 地址字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出范围时抛出</exception>
        public string GetDeviceIpAddress(int index)
        {
            if (index < 0 || index >= DeviceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "设备索引超出范围");
            }

            return _deviceInfoVector[index].chCurrentIp.TrimEnd('\0');
        }

        /// <summary>
        /// 清理所有已创建的相机设备
        /// </summary>
        public void ClearDevices()
        {
            foreach (var camera in _cameraDevices)
            {
                camera?.Dispose();
            }
            _cameraDevices.Clear();
        }

        /// <summary>
        /// 释放管理器持有的所有资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region 私有方法

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
                ClearDevices();
                _deviceInfoVector?.Clear();
            }

            _isDisposed = true;
        }

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
                GlobalCommData.ShowLog("HikCameraManager", errorMsg, MessageLevel.Error);
            }
            catch
            {
                Console.WriteLine(errorMsg);
            }
        }

        #endregion
    }
}
