using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.PCLOperate.Models;
using Emgu.CV;

namespace AdaWeldSystem.LineLaserCam.ILineLaser
{
    /// <summary>线激光相机抽象基类</summary>
    /// <remarks>
    /// 线激光族的统一抽象（类比运控 [[modules/MotionControl]] 的 L2 `IMotion/`）：
    /// 英莱真实相机与虚拟调试相机均可互换，工作流与界面只依赖本抽象。
    /// 本层承载相机键名、连接态派生、两个回调事件（结果与轮廓）、统一日志出口；
    /// 硬件相关实现全部下沉派生类，抽象层不得出现厂商 SDK 类型。
    /// 派生类**只做回调触发**，业务处理（PVT 计算、显示生成）一律由业务层完成。
    /// </remarks>
    public abstract class LineLaserCameraBase : IDisposable
    {
        #region 私有变量

        private readonly string _cameraName;
        private readonly object _matLock = new object();
        private Mat _currentMat;

        #endregion

        #region 公共变量

        /// <summary>相机键名（注册表登记名，同时作为日志标签）</summary>
        public string CameraName { get { return _cameraName; } }

        /// <summary>相机种类</summary>
        public abstract LineLaserKind Kind { get; }

        /// <summary>是否已连接硬件</summary>
        public abstract bool IsConnected { get; }

        /// <summary>是否正在出图</summary>
        public abstract bool IsRunning { get; }

        /// <summary>最近一次采集是否成功</summary>
        public abstract bool LastAcquisitionSuccess { get; }

        /// <summary>最近一次采集到的图像，未生成时返回 null</summary>
        public Mat CurrentMat
        {
            get
            {
                lock (_matLock)
                {
                    return (_currentMat == null) ? null : _currentMat.Clone();
                }
            }
        }

        /// <summary>采集扫描率（Hz），业务层据此缩放等待超时与节流节拍</summary>
        public abstract double ScanRateHz { get; }

        /// <summary>采集完成信号（业务层等待此信号后读取结果）</summary>
        public abstract AutoResetEvent AcquisitionCompletedSignal { get; }

        /// <summary>
        /// 校准数据快照，业务层生成轮廓 Mat 时据此绘制校准框与毫米刻度。
        /// 厂商实现层在 SDK 回调中捕获并存储，未捕获到时返回空结构（HasData 为 false）。
        /// </summary>
        public virtual LineLaserCalibration Calibration { get { return new LineLaserCalibration(); } }

        /// <summary>相机自身连接态（已连接 / 未连接，「工作中」由 LineLaserManager 派生）</summary>
        public LineLaserConnectionState ConnectionState
        {
            get { return IsConnected ? LineLaserConnectionState.Connected : LineLaserConnectionState.Disconnected; }
        }

        /// <summary>识别结果就绪事件，由派生类在 SDK 回调末尾调用 RaiseResultReady 触发</summary>
        public event EventHandler<LineLaserResultEventArgs> ResultReady;

        /// <summary>轮廓就绪事件，由派生类在 SDK 回调末尾调用 RaiseProfileReady 触发</summary>
        public event EventHandler<LineLaserProfileEventArgs> ProfileReady;

        /// <summary>状态变化事件，由派生类在连接与采集状态变化后调用 RaiseStateChanged 触发</summary>
        public event EventHandler<LineLaserStateEventArgs> StateChanged;

        #endregion

        #region 构造函数

        /// <summary>创建线激光相机</summary>
        /// <param name="cameraName">相机键名</param>
        protected LineLaserCameraBase(string cameraName)
        {
            _cameraName = cameraName;
        }

        #endregion

        #region 保护函数

        /// <summary>触发识别结果就绪事件</summary>
        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="measuredFps">实测帧率，未统计传 0</param>
        protected void RaiseResultReady(LineLaserSeamResult result, double measuredFps)
        {
            EventHandler<LineLaserResultEventArgs> handler = ResultReady;
            if (handler != null)
            {
                handler(this, new LineLaserResultEventArgs(_cameraName, result, measuredFps));
            }
        }

        /// <summary>触发轮廓就绪事件</summary>
        /// <param name="cloud">本帧轮廓点云</param>
        /// <param name="measuredFps">实测帧率，未统计传 0</param>
        protected void RaiseProfileReady(PointCloudData cloud, double measuredFps)
        {
            EventHandler<LineLaserProfileEventArgs> handler = ProfileReady;
            if (handler != null)
            {
                handler(this, new LineLaserProfileEventArgs(_cameraName, cloud, measuredFps));
            }
        }

        /// <summary>触发状态变化事件</summary>
        protected void RaiseStateChanged()
        {
            EventHandler<LineLaserStateEventArgs> handler = StateChanged;
            if (handler != null)
            {
                handler(this, new LineLaserStateEventArgs(_cameraName, IsConnected, IsRunning, ConnectionState));
            }
        }

        /// <summary>输出日志，标签取相机键名</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        protected void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(_cameraName, message, level);
        }

        #endregion

        #region 公共函数

        /// <summary>回写最新显示帧，由业务层生成 Mat 后调用，实现类自身不生成图像</summary>
        /// <param name="mat">最新显示帧，传入的所有权归本对象，重复调用会释放上一帧</param>
        public void UpdateDisplayMat(Mat mat)
        {
            lock (_matLock)
            {
                if (!ReferenceEquals(_currentMat, mat) && _currentMat != null)
                {
                    _currentMat.Dispose();
                }
                _currentMat = mat;
            }
        }

        /// <summary>触发连续采集</summary>
        public abstract void SensorRunContinue();

        /// <summary>停止采集</summary>
        public abstract void SensorStop();

        /// <summary>释放相机资源</summary>
        public abstract void Dispose();

        #endregion

        #region 外设虚契约

        /// <summary>
        /// 手动连接相机。
        /// 默认实现返回 false（不支持），派生类按需重写；新增契约一律给默认实现，派生类可暂不实现。
        /// </summary>
        /// <param name="ip">相机 IP 地址，虚拟相机等无硬件语义的派生类可忽略</param>
        /// <returns>连接成功返回 true</returns>
        public virtual bool ConnectManual(string ip)
        {
            Log("本相机不支持手动连接", MessageLevel.Warning);
            return false;
        }

        /// <summary>手动断开相机，默认实现为空操作</summary>
        public virtual void DisconnectManual()
        {
        }

        /// <summary>配置默认回调周期，默认实现视为无需配置并返回 true</summary>
        /// <returns>无需配置或配置成功返回 true</returns>
        public virtual bool ConfigureDefaultCommPeriod()
        {
            return true;
        }

        /// <summary>传感器（出图）是否开启，默认以是否正在出图为准</summary>
        /// <returns>开启返回 true</returns>
        public virtual bool IsCameraOn()
        {
            return IsRunning;
        }

        /// <summary>激光器是否开启，默认恒为关</summary>
        /// <returns>开启返回 true</returns>
        public virtual bool IsLaserOn()
        {
            return false;
        }

        /// <summary>开关传感器（出图），默认实现为空操作</summary>
        /// <param name="enable">true 为开启</param>
        public virtual void SetSensor(bool enable)
        {
        }

        /// <summary>开关激光器，默认实现为空操作</summary>
        /// <param name="enable">true 为开启</param>
        public virtual void SetLaser(bool enable)
        {
        }

        #endregion
    }
}
