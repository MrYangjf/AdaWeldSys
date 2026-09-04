using AdaWeldSystem.Comm;
using AdaWeldSystem.EmguALG;
using AdaWeldSystem.LineLaserCamApi;
using IntelligentLaser_CSharp;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.ProductFileManager;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdaWeldSystem.LineLaserCam.IntelligentLaserCam
{
    /// <summary>英莱SDK算法解析结果快照</summary>
    public struct IlInspectResult
    {
        public int ParseRes;
        public int ErrorCode;
        public ulong Timestamp;
        public float FeatureY, FeatureZ;
        public float Width0, Width1;
        public float Height0, Height1;
        public float Area;
        public float Angle0, Angle1, Angle2;
        public float Radius0, Radius1;
        public float Vec0, Vec1;
    }

    /// <summary>轮廓数据就绪事件参数</summary>
    public class ContourDataEventArgs : EventArgs
    {
        /// <summary>轮廓点云数据</summary>
        public PointCloudData Cloud { get; private set; }
        /// <summary>算法解析结果</summary>
        public IlInspectResult Inspect { get; private set; }
        /// <summary>是否有有效识别</summary>
        public bool HasValidResult { get; private set; }
        /// <summary>实测帧率(Hz)</summary>
        public double MeasuredFps { get; private set; }

        public ContourDataEventArgs(PointCloudData cloud, IlInspectResult inspect, bool hasValidResult, double measuredFps)
        {
            Cloud = cloud;
            Inspect = inspect;
            HasValidResult = hasValidResult;
            MeasuredFps = measuredFps;
        }
    }

    /// <summary>英莱Job参数元数据与当前值</summary>
    public class IlJobParamInfo
    {
        public string Name = string.Empty;
        public uint ValueType;
        public string StringValue = string.Empty;
        public double FloatValue;
        public long IntValue;
        public bool HasRange;
        public double FloatMin, FloatMax;
        public long IntMin, IntMax;
    }

    /// <summary>英莱相机信息快照</summary>
    public class IlSensorInfo
    {
        public string Ip = string.Empty;
        public string SerialNumber = string.Empty;
        public string SoftwareVersion = string.Empty;
        public string HardwareVersion = string.Empty;
        public string ProtocolVersion = string.Empty;
        public string SensorType = string.Empty;
    }

    /// <summary>英莱Job描述信息</summary>
    public class IlJobDescInfo
    {
        public string JobDescribe = string.Empty;
        public int JointType;
        public int Id;
    }

    /// <summary>英莱接头类型语义信息</summary>
    public class IlJointTypeInfo
    {
        public int Int;
        public string NameZh = string.Empty;
        public List<string> DisplayKeys = new List<string>();
    }

    /// <summary>英莱相机基础参数</summary>
    public class IlCameraParam
    {
        public uint Exposure;
        public float Gamma;
        public uint LaserPower;
    }

    /// <summary>英莱相机校准数据快照</summary>
    public class IlCalibrationInfo
    {
        public bool HasData;
        public float[] Center = new float[2];
        public float[] LeftTopCorner = new float[2];
        public float[] RightTopCorner = new float[2];
        public float[] LeftBottomCorner = new float[2];
        public float[] RightBottomCorner = new float[2];
    }

    /// <summary>英莱帧率模式</summary>
    public enum IlFpsModel
    {
        STANDERD = 1,
        FAST_SPEED = 2,
        HIGH_SPEED = 3
    }

    /// <summary>英莱线激光相机运行控制</summary>
    public class IntelligentLaserCameraRun : ICameraRun
    {
        private const string Tag = "IntelligentLaserCameraRun";

        // 默认相机 IP（首次无配置时的回退值；实际以 Config/INI/IntelligentLaser.ini 保存值为准）
        private const string DefaultSensorIp = "192.168.178.210";

        // 英莱相机 IP 持久化文件（INI，归 Config/INI，符合 ADR-007 配置分层）
        private static readonly string _iniFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "IntelligentLaser.ini");

        private readonly AutoResetEvent _acquisitionCompletedSignal = new AutoResetEvent(false);
        private volatile bool _isConnected = false;
        // 线激光三态：连接 / 激光 / 相机（传感器）。_isLaserOn/_isCameraOn 由连接时硬件同步
        private volatile bool _isLaserOn = false;
        private volatile bool _isCameraOn = false;
        private volatile bool _disposed = false;
        // 完整释放已执行（ReleaseNativeResources 幂等防护：Dispose/析构只完整执行一次）
        private volatile bool _released = false;
        // 外设已全部关闭（ShutdownPeripherals 置位，重连时 ConnectCore 重置）：
        private volatile bool _peripheralsOff = false;
        private Emgu.CV.Mat _currentMat;
        // 轮廓显示复用画布（Issue #2 优化）：持久 Bitmap/Graphics，每帧 Clear 后重绘，
        // 取代原先每帧 new Bitmap + new Graphics + new Mat 的分配，消除 GC 抖动、降低显示延迟。
        private System.Drawing.Bitmap _canvasBmp;
        private System.Drawing.Graphics _canvasG;
        private const int CanvasW = 834;
        private const int CanvasH = 554;
        private bool _lastSuccess = false;
        private double _scanRateHz = 60.0;   // STANDERD 目标 60Hz，未读到时回退

        private readonly object _matLock = new object();

        // 英莱 SDK 算法结果快照（线程安全）：回调内捕获 InspectResult[0]，工作流读取判定走英莱算法路线
        private IlInspectResult _lastInspect;
        private bool _hasInspect = false;
        private readonly object _ilLock = new object();

        // 最近一次轮廓点数量（MillionOutline 回调内更新，供英莱相机页面显示）
        private volatile int _lastContourCount;

        // 实测帧率：从第二次帧开始用 1.0 / 帧间隔(秒) 计算，首帧剔除避免初始值错误
        private DateTime _lastFrameTime = DateTime.MinValue;
        private volatile bool _firstFrameSkipped = false;
        private double _measuredFps = 0.0;

        // 上次轮廓帧通知时间（节流：16ms → 约 60fps，与 60FPS 帧率匹配）
        private DateTime _lastFrameNotifyTime = DateTime.MinValue;

        // 上次传感器帧率硬件查询时间（节流：500ms，避免每帧调用 SDK 硬件查询）
        private DateTime _lastSensorFpsUpdateTime = DateTime.MinValue;
        private const int SensorFpsUpdateIntervalMs = 500;

        // 手动模式标志：true=手动调试模式
        private bool _isManualMode = true;

        // SDK 初始化标志：true=已 InitSDK 且回调已注册；false=未初始化。
        private bool _sdkInitialized = false;

        // CommPeriod 配置标志：true=已配置过 SetSensorCommPeriod，防止重复配置。
        private bool _commPeriodConfigured = false;

        // 回调常驻
        private IntelligentMillionOutlineInspectCallBack _cbMillionOutline;
        private GCHandle _hCbMillionOutline;
        private IntelligentJobParamInitCallBack _cbInitJobParams;
        private GCHandle _hCbInitJobParams;
        private IntelligentJobParamChangedCallBack _cbJobParamsChanged;
        private GCHandle _hCbJobParamsChanged;
        private IntelligentInternalRefCornerPtsCallBack _cbCalibration;
        private GCHandle _hCbCalibration;
        private GCHandle _hostHandle;

        // 回调在途计数器（Interlocked 原子操作）：释放 GCHandle 前等待归零，
        private int _activeCallbacks;
        // 回调等待超时：UnInitSDK 后在途回调应快速归零，200ms 足够，避免关闭程序长时间卡顿
        private const int CallbackWaitTimeoutMs = 200;

        // 英莱校准数据快照（线程安全）：InternalRefCornerPts 回调内捕获中心点 + 4 角点（mm），供绘制校准框/十字架
        private IlCalibrationInfo _calibration;
        private readonly object _calibLock = new object();

        // 英莱 Job Parameter 元数据字典（RecvInitJobParamsInfo 初始化回调填充：参数名 → 类型/范围）。
        private readonly Dictionary<string, IlJobParamInfo> _jobMeta = new Dictionary<string, IlJobParamInfo>();
        // 当前 Job 参数缓存（OnJobParamsChanged 回调替换式重建，供 UI 展示与下发）
        private List<IlJobParamInfo> _jobParams;
        private readonly object _jobLock = new object();

        /// <summary>轮廓帧就绪事件</summary>
        public event EventHandler ContourFrameReady;

        /// <summary>轮廓数据就绪事件</summary>
        public event EventHandler<ContourDataEventArgs> ContourDataReady;

        public IntelligentLaserCameraRun()
        {
            // ADR-002 兼容：公共属性初值在构造函数显式赋值（避免 auto-property initializer）
            _scanRateHz = 60.0;
        }

        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region IP 持久化（Config/INI/IntelligentLaser.ini）

        /// <summary>读取已保存的英莱相机 IP</summary>
        /// <returns>已保存 IP，未配置回退默认</returns>
        public static string LoadSavedIp()
        {
            try
            {
                if (!File.Exists(_iniFile))
                    return DefaultSensorIp;
                var ini = new AdaWeldSystem.FileOperate.INIFile(_iniFile);
                string ip = ini.ReadString("Sensor", "IpAddress", string.Empty);
                if (string.IsNullOrEmpty(ip))
                    return DefaultSensorIp;
                return ip;
            }
            catch
            {
                return DefaultSensorIp;
            }
        }

        /// <summary>保存英莱相机 IP 到本地</summary>
        /// <param name="ip">相机 IP 地址</param>
        public static void SaveSensorIp(string ip)
        {
            try
            {
                if (string.IsNullOrEmpty(ip)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(_iniFile));
                var ini = new AdaWeldSystem.FileOperate.INIFile(_iniFile);
                ini.WriteString("Sensor", "IpAddress", ip);
                ini.SaveToFile();
            }
            catch
            {
                // 保存失败不影响运行时选择
            }
        }

        #endregion

        #region ICameraRun 实现

        public AutoResetEvent AcquisitionCompletedSignal { get { return _acquisitionCompletedSignal; } }

        /// <summary>连续采集是否运行中</summary>
        public bool IsRunning { get { return _isCameraOn; } }

        public bool LastAcquisitionSuccess { get { return _lastSuccess; } }

        /// <summary>是否已连接硬件</summary>
        public bool IsConnected { get { return _isConnected; } }

        /// <summary>当前采集扫描率(Hz)</summary>
        public double ScanRateHz { get { return _scanRateHz; } }

        /// <summary>实测帧率(Hz)</summary>
        public double MeasuredFps { get { return _measuredFps; } }

        /// <summary>手动模式标志</summary>
        public bool IsManualMode
        {
            get { return _isManualMode; }
            set { _isManualMode = value; }
        }

        /// <summary>最近一次轮廓Mat</summary>
        public Emgu.CV.Mat CurrentMat
        {
            get
            {
                lock (_matLock)
                {
                    return (_currentMat == null) ? null : _currentMat.Clone();
                }
            }
        }

        #endregion

        #region 英莱算法结果 (InspectResult)

        /// <summary>是否有有效算法结果</summary>
        public bool HasInspectResult
        {
            get { lock (_ilLock) { return _hasInspect; } }
        }

        /// <summary>最近一次算法结果快照</summary>
        public IlInspectResult LastInspectResult
        {
            get { lock (_ilLock) { return _lastInspect; } }
        }

        #endregion

        #region 打开 / 启动 / 停止

        private bool ConnectCore(string ip)
        {
            // 首次连接：InitSDK + 注册回调（只做一次）
            if (!_sdkInitialized)
            {
                int initRet = IntelligentLaser_export.InitSDK();
                if (initRet != 0)
                {
                    Log(string.Format("InitSDK 失败 ret {0}", initRet), MessageLevel.Error);
                    return false;
                }

                // 必须在 ConnectToSensor 之前注册所有回调！
                // SDK 在连接完成时会一次性触发 InternalRefCornerPts（校准框角点）和 RecvInitJobParamsInfo（Job 参数元数据）回调，
                // 如果注册晚了，这两个一次性回调会丢失 → 校准框不显示、参数列表为空。
                RegisterCallbacks();
                _sdkInitialized = true;
            }

            int ret = IntelligentLaser_export.ConnectToSensor(ip);
            // 位掩码：某位 == 1 表示对应服务「未」连上；Core 服务必须连上才能出图。
            if ((ret & (int)EmIntelligentConnectStatus.EM_INTELLIGENT_CONNECT_E_CORE) != 0)
            {
                Log(string.Format("ConnectToSensor 未连上 Core 服务 ret 0x{0:X} 连接失败", ret),
                    MessageLevel.Error);
                // SDK 已初始化则保留（下次重连继续用），不做 UnInitSDK / Free 委托
                // 首次连接失败也保留 SDK 初始化状态，避免反复 Init/UnInit 引发 GC 问题
                return false;
            }

            _isConnected = true;
            _peripheralsOff = false; // 重连后外设状态重置，允许再次关闭
            // 连接成功即从硬件同步一次激光/相机实际状态（"第一次正确"），
            // 之后由 SetLaser/SetSensor 本地更新，UI 不再频繁读硬件。
            SyncHardwareStatus();
            return true;
        }

        /// <summary>配置默认回调周期</summary>
        /// <returns>true=成功</returns>
        public bool ConfigureDefaultCommPeriod()
        {
            if (_commPeriodConfigured) return false;
            if (!_isConnected) return false;

            try
            {
                using (var commCtrl = new IntelligentSdkCommCtrl())
                {
                    commCtrl.DwSendCycle = 10u;
                    commCtrl.DwBufferDuration = 1000u;
                    int commRet = IntelligentLaser_export.SetSensorCommPeriod(commCtrl, 1u);
                    if (commRet != 0)
                    {
                        Log(string.Format("SetSensorCommPeriod 失败 ret 0x{0:X} 不影响连接 回调周期使用默认值", commRet),
                            MessageLevel.Warning);
                        return false;
                    }
                    _commPeriodConfigured = true;
                    Log("SetSensorCommPeriod 已配置 SendCycle 10ms BufferDuration 1000ms",
                        MessageLevel.Info);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log("SetSensorCommPeriod 异常 " + ex.Message + " 不影响连接",
                    MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>打开相机并启动连续采集</summary>
        /// <param name="ip">相机 IP 地址</param>
        public void OpenSensor(string ip)
        {
            if (_isConnected) return;
            try
            {
                if (!ConnectCore(ip)) return;

                SetSensor(true);
                SetLaser(true);
                //SetCaptureCamera(true);
                IntelligentLaser_export.SetSensorFpsModel(EmIntelligentFpsModel.EM_INTELLIGENT_FPS_MODEL_STANDERD);

                float fps = 0f;
                if (IntelligentLaser_export.FetchSensorFps(ref fps) == 0 && fps > 0f)
                {
                    _scanRateHz = (double)fps;
                }

                Log(string.Format("相机已打开 IP {0} 帧率 {1:F1} Hz", ip, _scanRateHz),
                    MessageLevel.Info);
            }
            catch (Exception ex)
            {
                Log("OpenSensor 异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>启动连续采集（未运行触发一次）</summary>
        public void SensorRunContinue()
        {
            if (!_isConnected)
            {
                OpenSensor(DefaultSensorIp);
            }
            if (!_isConnected) return;
            if (!IsRunning)
            {
                SetSensor(true);
            }
        }

        /// <summary>停止连续采集</summary>
        public void SensorStop()
        {
            if (!_isConnected) return;
            try
            {
                SetSensor(false);
            }
            catch
            {
                // 停止失败不影响状态机
            }
        }

        /// <summary>释放相机并反初始化 SDK</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                ReleaseNativeResources();
            }
            catch
            {
                // 释放异常吞掉，避免影响退出
            }
            lock (_matLock)
            {
                if (_currentMat != null)
                {
                    _currentMat.Dispose();
                    _currentMat = null;
                }
                // 释放持久画布（GDI 资源），避免句柄泄漏
                if (_canvasG != null) { _canvasG.Dispose(); _canvasG = null; }
                if (_canvasBmp != null) { _canvasBmp.Dispose(); _canvasBmp = null; }
            }
            GC.SuppressFinalize(this);
        }

        ~IntelligentLaserCameraRun()
        {
            ReleaseNativeResources();
        }

        private void ShutdownPeripherals()
        {
            if (_peripheralsOff || !_isConnected) return;
            _peripheralsOff = true;
            try
            {
                // 先停传感器（MillionOutline 回调源）→ 再关激光/相机 → 断连
                IntelligentLaser_export.SetSensorEnable(false);
                IntelligentLaser_export.SetLaserEnable(false);
                IntelligentLaser_export.SetSensorCaptureCameraEnable(false);
                IntelligentLaser_export.SetSensorMonitorCameraEnable(false);
                IntelligentLaser_export.DisConnSensor();
            }
            catch (Exception ex)
            {
                Log("关闭外设异常 " + ex.Message, MessageLevel.Error);
            }
            _isLaserOn = false;
            _isCameraOn = false;
        }

        private void ReleaseNativeResources()
        {
            if (_released) return;
            _released = true;
            ShutdownPeripherals();
            try
            {
                if (_isConnected)
                {
                    IntelligentLaser_export.UnInitSDK();
                }
                // 等待在途回调执行完毕，再释放委托 GCHandle，防止 CallbackOnCollectedDelegate
                // 仅在此处统一等待一次（ShutdownPeripherals 中去掉了 WaitForCallbacksToFinish）
                WaitForCallbacksToFinish();
            }
            catch
            {
                // 释放异常吞掉
            }
            // 先释放委托 GCHandle（必须在非托管 SDK 已停止回调后再释放）
            FreeCallbackHandles();
            if (_hostHandle.IsAllocated)
            {
                _hostHandle.Free();
            }
            _isConnected = false;
            _isLaserOn = false;
            _isCameraOn = false;
        }

        #endregion

        #region 手动控制与 JOB 管理（英莱相机页面专用，分步控制，不经工作流）

        /// <summary>最近一次轮廓点数量</summary>
        public int LastContourPointCount { get { return _lastContourCount; } }

        /// <summary>最近一次校准数据快照</summary>
        public IlCalibrationInfo CalibrationInfo
        {
            get { lock (_calibLock) { return _calibration; } }
        }

        /// <summary>扫描网口下的英莱相机</summary>
        /// <returns>发现的相机列表</returns>
        public List<IlSensorInfo> ScanSensors()
        {
            var result = new List<IlSensorInfo>();
            bool tempInit = false;
            try
            {
                if (!_isConnected)
                {
                    int initRet = IntelligentLaser_export.InitSDK();
                    if (initRet != 0)
                    {
                        Log(string.Format("扫描前 InitSDK 失败 ret {0}", initRet), MessageLevel.Error);
                        return result;
                    }
                    tempInit = true;
                }

                IntelligentLaser_export.DetectAllSensor();
                var devices = new IntelligentSensorMsg[32];
                var netCard = new StringBuilder(32);
                int count = devices.Length;
                IntelligentLaser_export.FetchAllSensorInfo2(netCard, devices, ref count);

                for (int i = 0; i < count && i < devices.Length; i++)
                {
                    var d = devices[i];
                    if (d == null) continue;
                    var info = new IlSensorInfo();
                    info.Ip = ReadAsciiBytes(d.SzIP);
                    info.SerialNumber = ReadAsciiBytes(d.SzSN);
                    info.SoftwareVersion = ReadAsciiBytes(d.SzSoftWareVer);
                    info.HardwareVersion = ReadAsciiBytes(d.SzHardWareVer);
                    info.ProtocolVersion = ReadAsciiBytes(d.SzProtocolVer);
                    info.SensorType = ReadAsciiBytes(d.SzSensorType);
                    if (string.IsNullOrEmpty(info.Ip)) continue;
                    result.Add(info);
                }
                Log(string.Format("扫描到 {0} 台相机", result.Count), MessageLevel.Info);
            }
            catch (Exception ex)
            {
                Log("ScanSensors 异常 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                if (tempInit)
                {
                    try { IntelligentLaser_export.UnInitSDK(); }
                    catch { /* 临时反初始化失败不影响扫描结果 */ }
                }
            }
            return result;
        }

        /// <summary>读取相机基础参数</summary>
        /// <returns>参数，未连接返回 null</returns>
        public IlCameraParam FetchCameraParams()
        {
            if (!_isConnected) return null;
            try
            {
                var p = new IntelligentSensorBaseParams();
                if (IntelligentLaser_export.FetchSensorBaseParam(p) == 0)
                {
                    var param = new IlCameraParam();
                    param.Exposure = p.Exptime;
                    param.Gamma = p.Gama;
                    param.LaserPower = p.Power;
                    return param;
                }
                return null;
            }
            catch (Exception ex)
            {
                Log("FetchCameraParams 异常 " + ex.Message, MessageLevel.Error);
                return null;
            }
        }

        /// <summary>设置相机基础参数</summary>
        /// <param name="param">相机参数</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int ApplyCameraParams(IlCameraParam param)
        {
            if (!_isConnected || param == null) return -1;
            try
            {
                var p = new IntelligentSensorBaseParams();
                p.Exptime = param.Exposure;
                p.Gama = param.Gamma;
                p.Power = param.LaserPower;
                return IntelligentLaser_export.SetSensorBaseParam(p);
            }
            catch (Exception ex)
            {
                Log("ApplyCameraParams 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>手动连接相机（只连不采）</summary>
        /// <param name="ip">相机 IP 地址</param>
        /// <returns>true=成功</returns>
        public bool ConnectManual(string ip)
        {
            if (_isConnected) return true;
            try
            {
                bool ok = ConnectCore(ip);
                if (ok)
                {
                    Log(string.Format("已连接 IP {0}", ip), MessageLevel.Info);
                }
                return ok;
            }
            catch (Exception ex)
            {
                Log("ConnectManual 异常 " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>手动断开并关闭外设</summary>
        public void DisconnectManual()
        {
            ShutdownPeripherals();
            _isConnected = false;
        }

        /// <summary>开启/关闭传感器采集</summary>
        /// <param name="enable">true=开启</param>
        public void SetSensor(bool enable)
        {
            if (!_isConnected) return;
            IntelligentLaser_export.SetSensorEnable(enable);
            _isCameraOn = enable;
            if (enable)
            {
                // 开启采集时重置实测 FPS 计数器，确保首帧被剔除，避免初始值错误
                ResetMeasuredFps();
            }
        }

        /// <summary>开启/关闭激光</summary>
        /// <param name="enable">true=开</param>
        public void SetLaser(bool enable)
        {
            if (!_isConnected) return;
            IntelligentLaser_export.SetLaserEnable(enable);
            _isLaserOn = enable;
        }

        /// <summary>获取激光开关状态</summary>
        /// <returns>true=开</returns>
        public bool IsLaserOn()
        {
            return _isConnected && _isLaserOn;
        }

        /// <summary>获取相机（传感器）状态</summary>
        /// <returns>true=开</returns>
        public bool IsCameraOn()
        {
            return _isConnected && _isCameraOn;
        }

        /// <summary>读取当前帧率模式</summary>
        /// <returns>帧率模式，失败回退 STANDERD</returns>
        public IlFpsModel FetchFpsModel()
        {
            if (!_isConnected) return IlFpsModel.STANDERD;
            try
            {
                EmIntelligentFpsModel model = EmIntelligentFpsModel.EM_INTELLIGENT_FPS_MODEL_STANDERD;
                int ret = IntelligentLaser_export.FetchSensorFpsModel(ref model, 1u);
                if (ret == 0)
                    return (IlFpsModel)model;
                Log(string.Format("FetchSensorFpsModel 失败 ret={0} 回退 STANDERD", ret),
                    MessageLevel.Warning);
            }
            catch (Exception ex)
            {
                Log("FetchFpsModel 异常 " + ex.Message, MessageLevel.Warning);
            }
            return IlFpsModel.STANDERD;
        }

        /// <summary>从硬件同步激光/相机状态</summary>
        public void SyncHardwareStatus()
        {
            if (!_isConnected) return;
            try
            {
                uint laserStatus = 0;
                if (IntelligentLaser_export.FetchLaserStatus(ref laserStatus) == 0)
                    _isLaserOn = laserStatus != 0;

                using (var info = new IntelligentSensorInfo())
                {
                    if (IntelligentLaser_export.FetchSensorInfo(info, 1u) == 0)
                        _isCameraOn = info.StCtrl.DwCameraEnable != 0;
                }
            }
            catch (Exception ex)
            {
                Log("SyncHardwareStatus 异常 " + ex.Message, MessageLevel.Warning);
            }

            float fps = 0f;
            if (IntelligentLaser_export.FetchSensorFps(ref fps, 1u) == 0 && fps > 0f)
                _scanRateHz = (double)fps;
        }

        /// <summary>重置实测帧率计数器</summary>
        public void ResetMeasuredFps()
        {
            _lastFrameTime = DateTime.MinValue;
            _firstFrameSkipped = false;
            _measuredFps = 0.0;
        }

        /// <summary>设置帧率模式并刷新扫描率</summary>
        /// <param name="model">帧率模式</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int ApplyFrameRateModel(IlFpsModel model)
        {
            return ApplyFrameRateModel((EmIntelligentFpsModel)model);
        }

        private int ApplyFrameRateModel(EmIntelligentFpsModel model)
        {
            if (!_isConnected) return -1;
            try
            {
                int ret = IntelligentLaser_export.SetSensorFpsModel(model);
                if (ret == 0)
                {
                    float fps = 0f;
                    if (IntelligentLaser_export.FetchSensorFps(ref fps) == 0 && fps > 0f)
                    {
                        _scanRateHz = (double)fps;
                    }
                    Log(string.Format("帧率模式已切换 model {0} 帧率 {1:F1} Hz", model, _scanRateHz),
                        MessageLevel.Info);
                }
                else
                {
                    Log(string.Format("帧率模式切换失败 model={0} ret={1}", model, ret),
                        MessageLevel.Error);
                }
                return ret;
            }
            catch (Exception ex)
            {
                Log("ApplyFrameRateModel 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>读取全部 JOB 描述</summary>
        /// <returns>JOB 描述列表</returns>
        public List<IlJobDescInfo> FetchJobList()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IntelligentLaser_jobdesclist.json");
                if (!File.Exists(path)) return new List<IlJobDescInfo>();
                return JsonConvert.DeserializeObject<List<IlJobDescInfo>>(File.ReadAllText(path))
                    ?? new List<IlJobDescInfo>();
            }
            catch (Exception ex)
            {
                Log("FetchJobList 异常 " + ex.Message, MessageLevel.Error);
                return new List<IlJobDescInfo>();
            }
        }

        /// <summary>读取全部接头类型</summary>
        /// <returns>接头类型列表</returns>
        public List<IlJointTypeInfo> FetchJointTypes()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IntelligentLaser_jointtypelist.json");
                if (!File.Exists(path)) return new List<IlJointTypeInfo>();
                var root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path));
                var arr = root == null ? null : root["joint_types"] as JArray;
                if (arr == null) return new List<IlJointTypeInfo>();
                var list = new List<IlJointTypeInfo>();
                foreach (var item in arr)
                {
                    if (item["int"] == null || item["name_zh"] == null) continue;
                    var info = new IlJointTypeInfo
                    {
                        Int = item["int"].Value<int>(),
                        NameZh = item["name_zh"].Value<string>()
                    };
                    // display：该接头类型应显示的参数 key → 默认值（技术员确认的 Job 实际使用参数）
                    var display = item["display"] as JObject;
                    if (display != null)
                    {
                        foreach (var kv in display)
                            info.DisplayKeys.Add(kv.Key);
                    }
                    list.Add(info);
                }
                return list;
            }
            catch (Exception ex)
            {
                Log("FetchJointTypes 异常 " + ex.Message, MessageLevel.Error);
                return new List<IlJointTypeInfo>();
            }
        }

        /// <summary>查询接头类型应显示参数键</summary>
        /// <param name="jointType">接头类型编号</param>
        /// <returns>参数键列表</returns>
        public List<string> GetJointTypeDisplayKeys(int jointType)
        {
            var jt = FetchJointTypes().Find(j => j.Int == jointType);
            return jt == null ? new List<string>() : jt.DisplayKeys;
        }

        /// <summary>查询接头类型中文名</summary>
        /// <param name="jointType">接头类型编号</param>
        /// <returns>中文名，未命中返回 null</returns>
        public string GetJointTypeName(int jointType)
        {
            var jt = FetchJointTypes().Find(j => j.Int == jointType);
            return jt == null ? null : jt.NameZh;
        }

        /// <summary>获取当前 JOB 描述</summary>
        /// <returns>JOB 描述，未命中返回 null</returns>
        public IlJobDescInfo GetCurrentJobInfo()
        {
            int id = FetchCurrentJobId();
            if (id < 0) return null;
            return FetchJobList().Find(j => j.Id == id);
        }

        /// <summary>获取当前 JOB ID</summary>
        /// <returns>JOB ID，-1=失败</returns>
        public int FetchCurrentJobId()
        {
            if (!_isConnected) return -1;
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = 0;
                    if (IntelligentLaser_export.FetchJobInfo(ctrl) == 0)
                        return ctrl.DwJobId;
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Log("FetchCurrentJobId 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>切换 JOB</summary>
        /// <param name="jobId">JOB ID</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int SwitchJob(int jobId)
        {
            if (!_isConnected) return -1;
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = (int)EmJobSettingCmd.EM_INTELLIGENT_JOB_SETTING_SWITCH;
                    ctrl.DwJobId = jobId;
                    return IntelligentLaser_export.SetSensorJobInfo(ctrl);
                }
            }
            catch (Exception ex)
            {
                Log("SwitchJob 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>新建 JOB</summary>
        /// <param name="jointType">焊缝类型</param>
        /// <param name="desc">备注</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int CreateJob(int jointType, string desc)
        {
            if (!_isConnected) return -1;
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = (int)EmJobSettingCmd.EM_INTELLIGENT_JOB_SETTING_CREATE;
                    ctrl.DwJointType = jointType;
                    ctrl.SzJobDesc = Make64Bytes(desc);
                    return IntelligentLaser_export.SetSensorJobInfo(ctrl);
                }
            }
            catch (Exception ex)
            {
                Log("CreateJob 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>删除 JOB</summary>
        /// <param name="jobId">JOB ID</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int DeleteJob(int jobId)
        {
            if (!_isConnected) return -1;
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = (int)EmJobSettingCmd.EM_INTELLIGENT_JOB_SETTING_DELETE;
                    ctrl.DwJobId = jobId;
                    return IntelligentLaser_export.SetSensorJobInfo(ctrl);
                }
            }
            catch (Exception ex)
            {
                Log("DeleteJob 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        #endregion

        #region 回调

        private void RegisterCallbacks()
        {
            _hostHandle = GCHandle.Alloc(this);
            _cbMillionOutline = new IntelligentMillionOutlineInspectCallBack(MillionOutlineCallback);
            _hCbMillionOutline = GCHandle.Alloc(_cbMillionOutline);
            // 注册回调：签名为 (回调委托, pUser)。uint 是回调委托自身的第三形参（由 SDK 调用时传入），并非注册方法的参数。
            IntelligentLaser_export.IntelligentRegisterCallBack_MillionOutlineInspect(
                _cbMillionOutline, GCHandle.ToIntPtr(_hostHandle));
            // 注册参数初始化全量回调：拉取全部 Job Parameter 的 名称/类型/范围/初值，缓存到 _jobParams 供 UI 使用
            _cbInitJobParams = new IntelligentJobParamInitCallBack(InitJobParamsCallback);
            _hCbInitJobParams = GCHandle.Alloc(_cbInitJobParams);
            IntelligentLaser_export.IntelligentRegisterCallBack_RecvInitJobParamsInfo(
                _cbInitJobParams, GCHandle.ToIntPtr(_hostHandle));
            // 注册参数变更回调：切 JOB 或改参数时 SDK 推送变更项（TIntelligentJobParam[]），按名增量刷新 _jobParams 缓存
            _cbJobParamsChanged = new IntelligentJobParamChangedCallBack(JobParamsChangedCallback);
            _hCbJobParamsChanged = GCHandle.Alloc(_cbJobParamsChanged);
            IntelligentLaser_export.IntelligentRegisterCallBack_OnJobParamsChanged(
                _cbJobParamsChanged, GCHandle.ToIntPtr(_hostHandle));
            // 注册内参角点回调：捕获校准数据（中心点 + 4 角点，mm），供绘制校准框（矩形区域）+ 大十字架（参照官方 demo）
            _cbCalibration = new IntelligentInternalRefCornerPtsCallBack(CalibrationCallback);
            _hCbCalibration = GCHandle.Alloc(_cbCalibration);
            IntelligentLaser_export.IntelligentRegisterCallBack_InternalRefCornerPts(
                _cbCalibration, GCHandle.ToIntPtr(_hostHandle));
        }

        private void FreeCallbackHandles()
        {
            if (_hCbMillionOutline.IsAllocated) _hCbMillionOutline.Free();
            if (_hCbInitJobParams.IsAllocated) _hCbInitJobParams.Free();
            if (_hCbJobParamsChanged.IsAllocated) _hCbJobParamsChanged.Free();
            if (_hCbCalibration.IsAllocated) _hCbCalibration.Free();
        }

        private void WaitForCallbacksToFinish()
        {
            int elapsed = 0;
            int sleepMs = 10;
            while (System.Threading.Interlocked.CompareExchange(ref _activeCallbacks, 0, 0) > 0
                   && elapsed < CallbackWaitTimeoutMs)
            {
                System.Threading.Thread.Sleep(sleepMs);
                elapsed += sleepMs;
            }
            // 超时后仍有在途回调：记录警告，仍继续释放（避免死锁）
            int remaining = System.Threading.Interlocked.CompareExchange(ref _activeCallbacks, 0, 0);
            if (remaining > 0)
            {
                Log(string.Format("WaitForCallbacksToFinish 超时，仍有 {0} 个在途回调", remaining),
                    MessageLevel.Warning);
            }
        }

        private void CalibrationCallback(IntPtr fRegioninfo, IntPtr pUser, uint dwInstanceId)
        {
            if (_disposed || fRegioninfo == IntPtr.Zero) return;
            System.Threading.Interlocked.Increment(ref _activeCallbacks);
            try
            {
                using (var calib = IntelligentCalibrationInfo.__GetOrCreateInstance(fRegioninfo))
                {
                    if (calib == null) return;
                    var snap = new IlCalibrationInfo();
                    snap.Center = calib.Center;
                    snap.LeftTopCorner = calib.LeftTopCorner;
                    snap.RightTopCorner = calib.RightTopCorner;
                    snap.LeftBottomCorner = calib.LeftBottomCorner;
                    snap.RightBottomCorner = calib.RightBottomCorner;
                    snap.HasData = true;
                    lock (_calibLock) { _calibration = snap; }
                }
            }
            catch (Exception ex)
            {
                Log("校准回调异常 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _activeCallbacks);
            }
            GC.KeepAlive(this);
        }

        private void MillionOutlineCallback(IntPtr pOutlineData, IntPtr pUser, uint dwInstanceId)
        {
            // 不在 lock 内做重活；进入换帧临界区前再次确认未释放，避免 Dispose 后 use-after-free
            if (_disposed) return;
            System.Threading.Interlocked.Increment(ref _activeCallbacks);
            PointCloudData cloud = null;
            try
            {
                // ===== 第一步：拿数据（所有模式共用，必须执行）=====
                // 务必 Dispose：__GetOrCreateInstance 包装的对象拥有原生内存，
                // 不释放会在 100Hz 下快速泄漏（呼应 [[api/IntelligentLaserSDK_CSharp_DevManual]] 第 4 条约定）。
                // 所有原生字段读取须在 using 块内完成；Mat 在块外由托管 PointCloudData 生成。
                using (var inspect = IntelligentMillionOutlineInspect.__GetOrCreateInstance(pOutlineData))
                {
                    if (inspect == null) { lock (_ilLock) { _hasInspect = false; } return; }
                    if (inspect.ContourData == null) { lock (_ilLock) { _hasInspect = false; } return; }

                    uint count = inspect.ContourData.ContourPointCount;
                    if (count > 2048) count = 2048;   // 上限封顶，防止越界读相邻内存
                    _lastContourCount = (int)count;

                    // 实测帧率计算：首帧剔除，从第二帧开始用 1.0 / 帧间隔(秒) 得到实时 FPS
                    // 相机关闭重开后首次计算需剔除，否则第一次会错误
                    DateTime now = DateTime.Now;
                    if (_firstFrameSkipped)
                    {
                        double intervalMs = (now - _lastFrameTime).TotalMilliseconds;
                        if (intervalMs > 0.0)
                            _measuredFps = 1000.0 / intervalMs;
                    }
                    else
                    {
                        _firstFrameSkipped = true;
                    }
                    _lastFrameTime = now;

                    // 传感器帧率实时刷新：每 500ms 从硬件读一次（节流，避免每帧调用 SDK 硬件查询）
                    // ScanRateHz 反映的是传感器自身采集分析频率（硬件输出值），区别于 MeasuredFps（回调实测值）
                    if ((now - _lastSensorFpsUpdateTime).TotalMilliseconds >= SensorFpsUpdateIntervalMs)
                    {
                        float hwFps = 0f;
                        if (IntelligentLaser_export.FetchSensorFps(ref hwFps, 1u) == 0 && hwFps > 0f)
                        {
                            _scanRateHz = (double)hwFps;
                        }
                        _lastSensorFpsUpdateTime = now;
                    }

                    // 构建 PointCloudData：采用官方 .NET Framework Demo 的 PointArray[] 托管访问
                    // （IntelligentLaser_CSharp_Demo/Form1.cs:513）：
                    // CppSharp 已封装 FPoint[]，免手搓原生布局/偏移，消除 __Instance + Marshal.Copy + BitConverter 的脆弱路径。
                    cloud = new PointCloudData();
                    var pts = inspect.ContourData.PointArray;
                    int n = (pts == null) ? 0 : Math.Min((int)count, pts.Length);
                    for (int i = 0; i < n; i++)
                    {
                        cloud.AddPoint(0.0f, pts[i].YPos, pts[i].ZPos);
                    }

                    // 捕获英莱 SDK 自有算法解析结果（InspectResult[0]），供工作流走英莱算法路线。
                    // 必须在 using 块内读取（块结束即 Dispose 原生内存）。字段含义见 [[api/IntelligentLaserSDK_CSharp_DevManual]] §5。
                    var results = inspect.InspectResult;
                    if (results != null && results.Length > 0)
                    {
                        var r0 = results[0];
                        IlInspectResult snap;
                        snap.ParseRes = r0.ParseRes;
                        snap.ErrorCode = r0.ErrorCode;
                        snap.Timestamp = r0.Timestamp;
                        snap.FeatureY = r0.CurrentFeaturePoint.YPos;
                        snap.FeatureZ = r0.CurrentFeaturePoint.ZPos;
                        snap.Width0 = r0.Width[0];
                        snap.Width1 = r0.Width[1];
                        snap.Height0 = r0.HeightOrDepth[0];
                        snap.Height1 = r0.HeightOrDepth[1];
                        snap.Area = r0.Area;
                        snap.Angle0 = r0.Angles[0];
                        snap.Angle1 = r0.Angles[1];
                        snap.Angle2 = r0.Angles[2];
                        snap.Radius0 = r0.Radii[0];
                        snap.Radius1 = r0.Radii[1];
                        snap.Vec0 = r0.Vec[0];
                        snap.Vec1 = r0.Vec[1];
                        lock (_ilLock)
                        {
                            _lastInspect = snap;
                            _hasInspect = true;
                        }
                    }
                    else
                    {
                        // 空帧/无算法结果：置 false，避免上一帧快照被误判为有效
                        lock (_ilLock) { _hasInspect = false; }
                    }
                }

                // 空帧：标记失败，不进入模式分支
                if (cloud == null || cloud.PointCount == 0)
                {
                    _lastSuccess = false;
                    return;
                }

                // ===== 第二步：模式分支 =====
                // 手动模式：生成 Mat + 换帧 + UI 事件（供手动调试页面显示）
                // 自动/其他模式：仅外传数据（节省 CPU/GPU，不触发 UI 刷新）
                if (_isManualMode)
                {
                    // --- 手动模式分支 ---
                    // 复用持久画布 + 持久 _currentMat（见 ConvertContourToMatDemo）：不再每帧 new Bitmap/Graphics/Mat。
                    // 方法内部持有 _matLock 完成绘制与 _currentMat 原地更新，天然替换原「换帧临界区」双缓冲逻辑。
                    ConvertContourToMatDemo(cloud);
                    _lastSuccess = true;

                    // 回调驱动显示：成功出帧后通知订阅方刷新轮廓画面（替代 UI 层轮询 Timer）
                    NotifyFrameReady();
                }
                else
                {
                    // --- 自动/其他模式分支（与手动模式互逆）---
                    // 不生成 Mat，不触发 UI 事件，仅将结果传递出去供工作流/ScottPlot 调用
                    _lastSuccess = true;
                }

                // ===== 以下为两种模式共有 =====
                _acquisitionCompletedSignal.Set();

                // 数据记录：AddFrame 内部已有 ClonePointCloud + lock，_isRecording=false 时直接返回零开销
                RunRecordManager.Instance.AddFrame(cloud);

                // 轮廓数据就绪事件：携带 PointCloudData + 解析结果 + 帧率
                EventHandler<ContourDataEventArgs> dataHandler = ContourDataReady;
                if (dataHandler != null)
                {
                    IlInspectResult inspectSnap;
                    bool hasValid;
                    lock (_ilLock)
                    {
                        inspectSnap = _lastInspect;
                        hasValid = _hasInspect && _lastInspect.ParseRes == 1;
                    }
                    dataHandler(this, new ContourDataEventArgs(cloud, inspectSnap, hasValid, _measuredFps));
                }
            }
            catch (Exception ex)
            {
                _lastSuccess = false;
                Log("MillionOutline 回调异常 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _activeCallbacks);
            }
            // 防止 JIT 在方法执行中提前回收 this（CallbackOnCollectedDelegate 标准防御）
            GC.KeepAlive(this);
        }

        private void NotifyFrameReady()
        {
            EventHandler handler = ContourFrameReady;
            if (handler == null) return;

            // 节流：16ms → 约 60fps，与 STANDERD 60Hz 帧率匹配
            DateTime now = DateTime.UtcNow;
            if ((now - _lastFrameNotifyTime).TotalMilliseconds < 16.0) return;
            _lastFrameNotifyTime = now;

            handler(this, EventArgs.Empty);
        }

        #endregion

        #region 英莱 Job Parameter

        private void InitJobParamsCallback(IntPtr pInstance, uint arraySize, IntPtr pUser, uint dwInstanceId)
        {
            if (_disposed || pInstance == IntPtr.Zero) return;
            System.Threading.Interlocked.Increment(ref _activeCallbacks);
            try
            {
               
                int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(TIntelligentJobParamsGroup.__Internal));
                var groups = new TIntelligentJobParamsGroup[arraySize];
                try
                {
                    for (uint i = 0; i < arraySize; i++)
                    {
                        IntPtr currentElementPtr = IntPtr.Add(pInstance, (int)(i * stride));
                        TIntelligentJobParamsGroup instancePtr = TIntelligentJobParamsGroup.__GetOrCreateInstance(currentElementPtr);
                        groups[i] = new TIntelligentJobParamsGroup(instancePtr);
                        instancePtr.Dispose();
                    }

                    // ══════════════════════════════════════════════════════════
                    // 步骤 2：从复制后的 Group 原生内存读取参数数据
                    // ══════════════════════════════════════════════════════════
                    // 使用 Marshal.OffsetOf 获取所有偏移量，避免硬编码。
                    // 仍然使用直接内存读取（绕过 StParamArray getter 缓存机制）。
                    // ══════════════════════════════════════════════════════════
                    int stParamArrayOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(
                        typeof(TIntelligentJobParamsGroup.__Internal), "stParamArray");
                    int paramFullInfoSize = System.Runtime.InteropServices.Marshal.SizeOf(
                        typeof(TIntelligentJobParamFullInfo.__Internal));
                    int dwParamNumOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(
                        typeof(TIntelligentJobParamsGroup.__Internal), "dwParamNum");
                    int dwValueTypeOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(
                        typeof(TIntelligentJobParam.__Internal), "dwValueType");
                    int valueRangeOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(
                        typeof(TIntelligentJobParamFullInfo.__Internal), "value_Range");

                    var meta = new Dictionary<string, IlJobParamInfo>();
                    byte[] nameBuf = new byte[64];
                    byte[] rangeBuf = new byte[8];

                    foreach (var group in groups)
                    {
                        IntPtr gp = group.__Instance;
                        uint num = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(gp, dwParamNumOffset);
                        int count = Math.Min((int)num, 32);
                        for (int j = 0; j < count; j++)
                        {
                            IntPtr elemPtr = IntPtr.Add(gp, stParamArrayOffset + j * paramFullInfoSize);
                            // 读参数名：szParamName 在 stParam 起始处（offset 0）
                            System.Runtime.InteropServices.Marshal.Copy(elemPtr, nameBuf, 0, 64);
                            string name = ReadAsciiBytes(nameBuf);
                            if (string.IsNullOrEmpty(name)) continue;
                            // 读值类型
                            uint valueType = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(elemPtr, dwValueTypeOffset);
                            var info = new IlJobParamInfo();
                            info.Name = name;
                            info.ValueType = valueType;
                            // 读范围：value_Range（union 8 字节）
                            if (valueType == 2u || valueType == 3u)
                            {
                                IntPtr rangePtr = IntPtr.Add(elemPtr, valueRangeOffset);
                                System.Runtime.InteropServices.Marshal.Copy(rangePtr, rangeBuf, 0, 8);
                                info.HasRange = true;
                                if (valueType == 2u)
                                {
                                    info.FloatMin = System.BitConverter.ToSingle(rangeBuf, 0);
                                    info.FloatMax = System.BitConverter.ToSingle(rangeBuf, 4);
                                }
                                else
                                {
                                    info.IntMin = (long)System.BitConverter.ToInt32(rangeBuf, 0);
                                    info.IntMax = (long)System.BitConverter.ToInt32(rangeBuf, 4);
                                }
                            }
                            if (!meta.ContainsKey(info.Name))
                                meta[info.Name] = info;
                        }
                    }
                    lock (_jobLock)
                    {
                        _jobMeta.Clear();
                        foreach (var kv in meta)
                            _jobMeta[kv.Key] = kv.Value;
                    }
                    Log(string.Format(
                        "InitJobParamsCallback 收到 {0} 个参数组，{1} 个参数元数据 [偏移量: dwParamNum@{2}, stParamArray@{3}, dwValueType@{4}, valueRange@{5}]",
                        arraySize, meta.Count, dwParamNumOffset, stParamArrayOffset, dwValueTypeOffset, valueRangeOffset),
                        MessageLevel.Info);
                }
                finally
                {
                    // 释放复制后的 Group（释放独立分配的原生内存）
                    foreach (var g in groups)
                        g?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log("InitJobParams 回调异常 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _activeCallbacks);
            }
            GC.KeepAlive(this);
        }

        private void JobParamsChangedCallback(IntPtr pInstance, uint arraySize, IntPtr pUser, uint dwInstanceId)
        {
            if (_disposed || pInstance == IntPtr.Zero) return;
            System.Threading.Interlocked.Increment(ref _activeCallbacks);
            try
            {
                int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(TIntelligentJobParam.__Internal));
                var changed = new Dictionary<string, IlJobParamInfo>();
                for (uint i = 0; i < arraySize; i++)
                {
                    IntPtr gp = IntPtr.Add(pInstance, (int)(i * stride));
                    using (var p = TIntelligentJobParam.__GetOrCreateInstance(gp))
                    {
                        if (p == null) continue;
                        var info = new IlJobParamInfo();
                        info.Name = ReadAsciiBytes(p.SzParamName);
                        info.ValueType = p.DwValueType;
                        if (p.DwValueType == 1u)
                            info.StringValue = ReadAsciiBytes(p.curValue.SzValue);
                        else if (p.DwValueType == 2u)
                            info.FloatValue = p.curValue.FValue;
                        else if (p.DwValueType == 3u)
                            info.IntValue = (long)p.curValue.DwValue;
                        // 范围从元数据字典补全（Changed 回调结构无 value_Range）
                        IlJobParamInfo meta;
                        if (_jobMeta.TryGetValue(info.Name, out meta))
                        {
                            info.HasRange = meta.HasRange;
                            info.FloatMin = meta.FloatMin; info.FloatMax = meta.FloatMax;
                            info.IntMin = meta.IntMin; info.IntMax = meta.IntMax;
                        }
                        changed[info.Name] = info;
                    }
                }
                if (changed.Count == 0) return;
                lock (_jobLock)
                {
                    // 全量替换判断：初次 / 清空 / 变更数超过当前缓存 1/3（至少 3 个）视为全量推送
                    bool fullSet = _jobParams == null || _jobParams.Count == 0
                        || changed.Count >= Math.Max(3, _jobParams.Count / 3);
                    if (fullSet)
                    {
                        // 全量推送（连接/切 JOB）：替换式重建，只保留当前 Job 参数（与 demo 一致）
                        _jobParams = new List<IlJobParamInfo>(changed.Values);
                    }
                    else
                    {
                        // 少量变更（单参数修改）：按名更新，**仅更新已有参数**，不添加新参数，
                        // 避免 SDK 分批推送时 _jobParams 累积无关参数导致「21组变几百组」异常。
                        if (_jobParams == null) _jobParams = new List<IlJobParamInfo>();
                        foreach (var kv in changed)
                        {
                            var existing = _jobParams.Find(x => x.Name == kv.Key);
                            if (existing != null)
                            {
                                existing.ValueType = kv.Value.ValueType;
                                existing.StringValue = kv.Value.StringValue;
                                existing.FloatValue = kv.Value.FloatValue;
                                existing.IntValue = kv.Value.IntValue;
                            }
                            // ⚠️ 不再 else _jobParams.Add(kv.Value) —— 不累积未知参数，
                            // 防止 SDK 推送非当前 Job 参数时 _jobParams 无限增长。
                        }
                    }
                }
                JobParamsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log("JobParamsChanged 回调异常 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _activeCallbacks);
            }
            GC.KeepAlive(this);
        }

        /// <summary>Job参数变更事件</summary>
        public event EventHandler JobParamsChanged;

        /// <summary>读取当前 JOB 全部参数</summary>
        /// <returns>参数列表</returns>
        public List<IlJobParamInfo> FetchAllJobParams()
        {
            if (!_isConnected)
                throw new InvalidOperationException("英莱设备未连接，无法读取参数");

            lock (_jobLock)
            {
                if (_jobParams == null || _jobParams.Count == 0)
                    return new List<IlJobParamInfo>();
                // 拷贝列表（元素为共享引用，业务层只读不修改字段；下发时构造新对象）
                return new List<IlJobParamInfo>(_jobParams);
            }
        }

        /// <summary>下发 Job Parameter 到设备</summary>
        /// <param name="values">参数列表</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int ApplyJobParams(IEnumerable<IlJobParamInfo> values)
        {
            if (!_isConnected)
                return -1;
            var arr = new List<TIntelligentJobParam>();
            foreach (var v in values)
            {
                // ✅ 已对照 M3 CppSharp 生成源码 (IntelligentLaser.cs:3247/3476) + 官方 .NET Framework Demo (Form1.cs:429) 确认：
                // 嵌套「类型名」为 TIntelligentJobParam.CurValue（大写 C，partial struct CurValue），
                // 「属性名」为 curValue（小写 c，返回 CurValue 实例）。下方对象初始化器使用类型名 CurValue。
                var p = new TIntelligentJobParam();
                p.SzParamName = Make64Bytes(v.Name);
                p.DwValueType = v.ValueType;
                if (v.ValueType == 1u)
                    p.curValue = new TIntelligentJobParam.CurValue { SzValue = Make64Bytes(v.StringValue) };
                else if (v.ValueType == 2u)
                    p.curValue = new TIntelligentJobParam.CurValue { FValue = (float)v.FloatValue };
                else if (v.ValueType == 3u)
                    p.curValue = new TIntelligentJobParam.CurValue { DwValue = (uint)v.IntValue };
                arr.Add(p);
            }
            int ret = IntelligentLaser_export.SetJobParamter(arr.ToArray(), (uint)arr.Count);
            return ret;
        }

        private static byte[] Make64Bytes(string name)
        {
            byte[] buf = new byte[64];
            byte[] src = Encoding.ASCII.GetBytes(name ?? string.Empty);
            int len = Math.Min(src.Length, 64);
            Array.Copy(src, 0, buf, 0, len);
            return buf;
        }

        private static string ReadAsciiBytes(byte[] buf)
        {
            if (buf == null) return string.Empty;
            int len = 0;
            while (len < buf.Length && buf[len] != 0) len++;
            return Encoding.ASCII.GetString(buf, 0, len);
        }

        private void EnsureCanvas()
        {
            if (_canvasBmp == null)
            {
                _canvasBmp = new System.Drawing.Bitmap(CanvasW, CanvasH);
                _canvasG = System.Drawing.Graphics.FromImage(_canvasBmp);
            }
        }

        private void UpdateCurrentMatFromCanvas()
        {
            // 调用方需持有 _matLock。首帧新建 _currentMat；后续 CopyTo 原地复用（同尺寸/类型时零新分配）。
            if (_currentMat == null)
            {
                _currentMat = _canvasBmp.ToMat();
            }
            else
            {
                using (var tmp = _canvasBmp.ToMat())
                {
                    tmp.CopyTo(_currentMat);
                }
            }
        }

        private void ConvertContourToMatDemo(PointCloudData cloud)
        {
            // 复用持久画布（首次创建 Bitmap/Graphics，后续每帧 Clear 后重绘），消除每帧 new 的 GC 抖动。
            // 整段在 _matLock 内执行，与 CurrentMat getter（克隆 _currentMat）互斥，并避免回调并发重入。
            lock (_matLock)
            {
                if (_disposed) return;

                int w = CanvasW;
                int h = CanvasH;

                EnsureCanvas();
                var bmp = _canvasBmp;   // 持久画布别名，下面沿用原 g/bmp 绘制逻辑
                var g = _canvasG;
                g.Clear(Color.FromArgb(30, 30, 30));
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var pts = cloud.Points;
                int count = pts.Count;
                if (count < 2)
                {
                    UpdateCurrentMatFromCanvas();
                    return;
                }

                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    float y = pts[i].Y;
                    float z = pts[i].Z;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                }

                float margin = 30f;

                // 校准数据（矩形区域 + 大十字架）：参照 demo resetInnerData（Form1.cs:597）
                IlCalibrationInfo calib;
                lock (_calibLock) { calib = _calibration; }
                bool hasCalib = (calib != null) && calib.HasData;

                float rate;
                PointF center;
                float fallbackScaleX = 0f, fallbackScaleY = 0f;
                PointF[] corners = null;
                PointF crossH1 = PointF.Empty, crossH2 = PointF.Empty;
                PointF crossV1 = PointF.Empty, crossV2 = PointF.Empty;

                if (hasCalib)
                {
                    float cornerW = Math.Abs(calib.RightBottomCorner[0] - calib.LeftBottomCorner[0]);
                    float cornerH = Math.Abs(calib.LeftTopCorner[1] - calib.LeftBottomCorner[1]);
                    if (cornerW < 0.001f) cornerW = 1f;
                    if (cornerH < 0.001f) cornerH = 1f;
                    float fRate1 = (w - 2f * margin) / cornerW;
                    float fRate2 = (h - 2f * margin) / cornerH;
                    rate = (fRate1 > fRate2) ? fRate2 : fRate1;

                    // 原Demo问题：center 通过 fPersent/dwOffset 计算，未对齐图像中心
                    // 修复：直接设为图像中心，使十字交叉居中
                    center = new PointF(w / 2f, h / 2f);

                    corners = new PointF[4];
                    corners[0] = new PointF(center.X - Math.Abs(calib.LeftTopCorner[0] * rate), center.Y - Math.Abs(calib.LeftTopCorner[1] * rate));
                    corners[1] = new PointF(center.X - Math.Abs(calib.LeftBottomCorner[0] * rate), center.Y + Math.Abs(calib.LeftBottomCorner[1] * rate));
                    corners[2] = new PointF(center.X + Math.Abs(calib.RightBottomCorner[0] * rate), center.Y + Math.Abs(calib.RightBottomCorner[1] * rate));
                    corners[3] = new PointF(center.X + Math.Abs(calib.RightTopCorner[0] * rate), center.Y - Math.Abs(calib.RightTopCorner[1] * rate));

                    // 十字线：水平线（左→右边交点）+ 垂直线（上→下边交点），参照 demo（Form1.cs:636-652）
                    PointF lineH1 = new PointF(center.X, center.Y), lineH2 = new PointF(center.X + 100, center.Y);
                    PointF lineV1 = new PointF(center.X, center.Y), lineV2 = new PointF(center.X, center.Y + 100);
                    crossH1 = CalcTwoLineCrossPoint(lineH1, lineH2, corners[0], corners[1]);
                    crossH2 = CalcTwoLineCrossPoint(lineH1, lineH2, corners[2], corners[3]);
                    crossV1 = CalcTwoLineCrossPoint(lineV1, lineV2, corners[3], corners[0]);
                    crossV2 = CalcTwoLineCrossPoint(lineV1, lineV2, corners[1], corners[2]);
                }
                else
                {
                    // 回退：自动缩放（原逻辑）
                    float rangeY = maxY - minY;
                    float rangeZ = maxZ - minZ;
                    if (rangeY < 0.001f) rangeY = 1f;
                    if (rangeZ < 0.001f) rangeZ = 1f;
                    fallbackScaleX = (w - 2f * margin) / rangeY;
                    fallbackScaleY = (h - 2f * margin) / rangeZ;
                    rate = (fallbackScaleX > fallbackScaleY) ? fallbackScaleY : fallbackScaleX;
                    center = new PointF(margin + (minY + maxY) / 2f * fallbackScaleX, h - margin - (minZ + maxZ) / 2f * fallbackScaleY);

                    // 网格
                    using (var penGrid = new Pen(Color.FromArgb(60, 60, 60), 1f))
                    {
                        for (int i = 0; i <= 8; i++)
                        {
                            float gx = margin + (w - 2f * margin) * i / 8f;
                            g.DrawLine(penGrid, gx, margin, gx, h - margin);
                            float gy = margin + (h - 2f * margin) * i / 8f;
                            g.DrawLine(penGrid, margin, gy, w - margin, gy);
                        }
                    }
                }

                // 坐标转换：校准模式用 TransPoint（center + y*rate, center - z*rate）；回退模式用自动缩放
                Func<float, float, PointF> trans;
                if (hasCalib)
                {
                    trans = (y, z) => new PointF(center.X + y * rate, center.Y - z * rate);
                }
                else
                {
                    trans = (y, z) => new PointF(margin + (y - minY) * fallbackScaleX, h - margin - (z - minZ) * fallbackScaleY);
                }

                // 校准框：填充深色背景 + 黄色边框 + 大十字架 + 角点/中心点（参照 demo Paint，Form1.cs:685）
                if (hasCalib && corners != null)
                {
                    using (var bgBrush = new SolidBrush(Color.FromArgb(0x3B, 0x3B, 0x3B)))
                    {
                        g.FillPolygon(bgBrush, corners);
                    }
                    using (var penOutline = new Pen(Color.Yellow, 2f))
                    {
                        g.DrawPolygon(penOutline, corners);
                    }
                    using (var penCross = new Pen(Color.LightGray, 2f))
                    {
                        if (crossH1.X != -1 && crossH2.X != -1) g.DrawLine(penCross, crossH1, crossH2);
                        if (crossV1.X != -1 && crossV2.X != -1) g.DrawLine(penCross, crossV1, crossV2);
                    }
                    using (var brushGreen = new SolidBrush(Color.LightGreen))
                    {
                        float dotDiameter = 6f;
                        float radius = dotDiameter / 2f;
                        g.FillEllipse(brushGreen, center.X - radius, center.Y - radius, dotDiameter, dotDiameter);
                        foreach (var pt in corners)
                        {
                            g.FillEllipse(brushGreen, pt.X - radius, pt.Y - radius, dotDiameter, dotDiameter);
                        }
                    }
                }

                // 轮廓折线（绿色）
                var points = new PointF[count];
                for (int i = 0; i < count; i++)
                {
                    points[i] = trans(pts[i].Y, pts[i].Z);
                }

                using (var pen = new Pen(Color.FromArgb(0, 255, 128), 1.5f))
                {
                    g.DrawLines(pen, points);
                }

                // 特征点（红色 8px 方块）：ParseRes==1 时绘制，参照 demo（Form1.cs:752）
                bool hasInspect;
                IlInspectResult lastInspect;
                lock (_ilLock) { hasInspect = _hasInspect; lastInspect = _lastInspect; }
                if (hasInspect && lastInspect.ParseRes == 1)
                {
                    PointF fp = trans(lastInspect.FeatureY, lastInspect.FeatureZ);
                    using (var penFeature = new Pen(Color.Red, 8f))
                    {
                        g.DrawRectangle(penFeature, fp.X, fp.Y, 1, 1);
                    }
                }

                // mm 虚线刻度栅格：对齐轮廓真实 mm 值，原点(0,0)在图像中心
                if (hasCalib && rate > 0f)
                {
                    // 画面可见的 mm 范围（原点在图像中心）
                    float mmYMin = -w / (2f * rate);
                    float mmYMax = w / (2f * rate);
                    float mmZMin = -h / (2f * rate);
                    float mmZMax = h / (2f * rate);

                    float stepMm = Math.Max(1f, (float)Math.Round(40f / rate));
                    using (var penGridDash = new Pen(Color.FromArgb(80, 80, 80), 1f))
                    using (var fontGrid = new Font("宋体", 8.5f))
                    using (var brushGrid = new SolidBrush(Color.FromArgb(170, 170, 170)))
                    {
                        penGridDash.DashStyle = DashStyle.Dash;

                        // 垂直网格线：Y(横向) mm 刻度  screen_x = center.X + mm*rate
                        int kStartY = (int)Math.Ceiling(mmYMin / stepMm);
                        int kEndY = (int)Math.Floor(mmYMax / stepMm);
                        for (int k = kStartY; k <= kEndY; k++)
                        {
                            float mmVal = k * stepMm;
                            float sx = center.X + mmVal * rate;
                            g.DrawLine(penGridDash, sx, 0f, sx, h);
                            string txt = string.Format("{0}", (int)Math.Round(mmVal));
                            g.DrawString(txt, fontGrid, brushGrid, sx + 2f, h - 4f - fontGrid.Height);
                        }

                        // 水平网格线：Z(高度) mm 刻度  screen_y = center.Y - mm*rate
                        int kStartZ = (int)Math.Ceiling(mmZMin / stepMm);
                        int kEndZ = (int)Math.Floor(mmZMax / stepMm);
                        for (int k = kStartZ; k <= kEndZ; k++)
                        {
                            float mmVal = k * stepMm;
                            float sy = center.Y - mmVal * rate;
                            g.DrawLine(penGridDash, 0f, sy, w, sy);
                            string txt = string.Format("{0}", (int)Math.Round(mmVal));
                            g.DrawString(txt, fontGrid, brushGrid, 2f, sy + 1f);
                        }
                    }
                }

                // 坐标轴标签（仅回退模式显示）
                if (!hasCalib)
                {
                    using (var font = new Font("宋体", 9f))
                    using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                    {
                        string labelY = string.Format("Y:{0:F1}~{1:F1}mm", minY, maxY);
                        g.DrawString(labelY, font, brush, margin, h - margin + 4f);
                        var sf = new StringFormat();
                        sf.FormatFlags = StringFormatFlags.DirectionVertical;
                        string labelZ = string.Format("Z:{0:F1}~{1:F1}mm", minZ, maxZ);
                        g.DrawString(labelZ, font, brush, 4f, margin, sf);
                    }
                }

                UpdateCurrentMatFromCanvas();
                return;
            }
        }

        private static PointF CalcTwoLineCrossPoint(PointF line1P1, PointF line1P2, PointF line2P1, PointF line2P2)
        {
            float x1 = line1P1.X, y1 = line1P1.Y;
            float x2 = line1P2.X, y2 = line1P2.Y;
            float x3 = line2P1.X, y3 = line2P1.Y;
            float x4 = line2P2.X, y4 = line2P2.Y;

            float denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            if (denom == 0)
            {
                return new PointF(-1, -1);
            }

            float ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
            float ix = x1 + ua * (x2 - x1);
            float iy = y1 + ua * (y2 - y1);
            return new PointF(ix, iy);
        }

        #endregion
    }
}
