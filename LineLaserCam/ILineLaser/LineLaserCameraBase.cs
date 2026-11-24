using System;
using System.Threading;
using AdaWeldSystem.PCLOperate.Models;
using Emgu.CV;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.LineLaserCam.ILineLaser
{
    /// <summary>线激光相机抽象基类</summary>
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
                    return _currentMat?.Clone();
                }
            }
        }

        /// <summary>采集扫描率，业务层据此缩放超时</summary>
        public abstract double ScanRateHz { get; }

        /// <summary>采集完成信号（业务层等待此信号后读取结果）</summary>
        public abstract AutoResetEvent AcquisitionCompletedSignal { get; }

        /// <summary>校准快照，业务层据此绘制校准框。 回调捕获存储，</summary>
        public virtual LineLaserCalibration Calibration { get { return new LineLaserCalibration(); } }

        /// <summary>相机自身连接态（已连接 / 未连接，「工作中」由 LineLaserManager 派生）</summary>
        public LineLaserConnectionState ConnectionState
        {
            get { return IsConnected ? LineLaserConnectionState.Connected : LineLaserConnectionState.Disconnected; }
        }

        /// <summary>结果就绪事件，派生类在回调末尾触发</summary>
        public event EventHandler<LineLaserResultEventArgs> ResultReady;

        /// <summary>轮廓就绪事件，由派生类在 SDK 回调末尾调用 RaiseProfileReady 触发</summary>
        public event EventHandler<LineLaserProfileEventArgs> ProfileReady;

        /// <summary>状态变化事件，派生类在状态变化后触发</summary>
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
            ResultReady?.Invoke(this, new LineLaserResultEventArgs(_cameraName, result, measuredFps));
        }

        /// <summary>触发轮廓就绪事件</summary>
        /// <param name="cloud">本帧轮廓点云</param>
        /// <param name="measuredFps">实测帧率，未统计传 0</param>
        protected void RaiseProfileReady(PointCloudData cloud, double measuredFps)
        {
            ProfileReady?.Invoke(this, new LineLaserProfileEventArgs(_cameraName, cloud, measuredFps));
        }

        /// <summary>触发状态变化事件</summary>
        protected void RaiseStateChanged()
        {
            StateChanged?.Invoke(this, new LineLaserStateEventArgs(_cameraName, IsConnected, IsRunning, ConnectionState));
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

        /// <summary>回写最新显示帧，由业务层生成 Mat 后调用</summary>

        /// <param name="mat">显示帧，所有权归本对象，重复调用释放旧帧</param>
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

        /// <summary>手动连接相机。 默认实现返回 false（不支持），派生类按需</summary>

        /// <param name="ip">相机 IP，无硬件的派生类可忽略</param>
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

        /// <summary>配置默认回调周期，默认实现视为无需配置并返</summary>

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

        #region 诊断契约

        /// <summary>失败原因成品文本，日志与界面只读</summary>

        /// <param name="context">失败语境（连接 / 初始化 / 识别 / 指令返回码）</param>
        /// <returns>非空的原因描述</returns>
        public virtual string DescribeLastError(LineLaserErrorContext context)
        {
            return string.Format("未提供返回码（{0} 实现不上报厂商返回码）", Kind);
        }

        #endregion
    }
}
