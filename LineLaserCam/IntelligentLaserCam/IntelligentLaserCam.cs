using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using IntelligentLaser_CSharp;
using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdaWeldSystem.LineLaserCam.IntelligentLaserCam
{
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

    /// <summary>英莱帧率模式</summary>
    public enum IlFpsModel
    {
        STANDERD = 1,
        FAST_SPEED = 2,
        HIGH_SPEED = 3
    }

    /// <summary>英莱线激光相机运行控制</summary>
    public class IntelligentLaserCam : LineLaserCameraBase
    {
        // 默认相机 IP（首次无配置时的回退值；实际值由 LineLaserManager 套用配置下发）
        private const string DefaultSensorIp = "192.168.178.210";

        private string _sensorIp = DefaultSensorIp;

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
        private bool _lastSuccess = false;
        private double _scanRateHz = 60.0;   // STANDERD 目标 60Hz，未读到时回退

        // 英莱 SDK 算法结果快照（线程安全）：回调内映射为中性结构，业务层读取判定走英莱算法路线
        private LineLaserSeamResult _lastInspect;
        private readonly object _ilLock = new object();

        // 最近一次轮廓点数量（MillionOutline 回调内更新，供英莱相机页面显示）
        private volatile int _lastContourCount;

        // 实测帧率：从第二次帧开始用 1.0 / 帧间隔(秒) 计算，首帧剔除避免初始值错误
        private DateTime _lastFrameTime = DateTime.MinValue;
        private volatile bool _firstFrameSkipped = false;
        private double _measuredFps = 0.0;

        // 上次传感器帧率硬件查询时间（节流：500ms，避免每帧调用 SDK 硬件查询）
        private DateTime _lastSensorFpsUpdateTime = DateTime.MinValue;
        private const int SensorFpsUpdateIntervalMs = 500;

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

        // 校准数据快照（线程安全）：InternalRefCornerPts 回调内捕获中心点 + 4 角点（mm），
        // 由业务层经 Calibration 属性读取后绘制校准框，本类只存储不做显示处理。
        private LineLaserCalibration _calibration;
        private readonly object _calibLock = new object();

        // 英莱 Job Parameter 元数据字典（RecvInitJobParamsInfo 初始化回调填充：参数名 → 类型/范围）。
        private readonly Dictionary<string, IlJobParamInfo> _jobMeta = new Dictionary<string, IlJobParamInfo>();
        // 当前 Job 参数缓存（OnJobParamsChanged 回调替换式重建，供 UI 展示与下发）
        private List<IlJobParamInfo> _jobParams;
        private readonly object _jobLock = new object();

        /// <summary>创建英莱线激光相机，键名固定为「英莱线激光」</summary>
        public IntelligentLaserCam()
            : base("英莱线激光")
        {
            // ADR-002 兼容：公共属性初值在构造函数显式赋值（避免 auto-property initializer）
            _scanRateHz = 60.0;
        }

        #region 相机 IP

        /// <summary>相机 IP 地址，由 LineLaserManager 套用配置下发，未配置时回退默认</summary>
        public string SensorIp
        {
            get { return _sensorIp; }
            set { if (!string.IsNullOrEmpty(value)) _sensorIp = value; }
        }

        #endregion

        #region 抽象层实现

        /// <summary>相机种类，固定为英莱</summary>
        public override LineLaserKind Kind { get { return LineLaserKind.IntelligentLaser; } }

        /// <summary>采集完成信号（工作流等待此信号后读取 CurrentMat）</summary>
        public override AutoResetEvent AcquisitionCompletedSignal { get { return _acquisitionCompletedSignal; } }

        /// <summary>连续采集是否运行中</summary>
        public override bool IsRunning { get { return _isCameraOn; } }

        /// <summary>最近一次采集是否成功</summary>
        public override bool LastAcquisitionSuccess { get { return _lastSuccess; } }

        /// <summary>是否已连接硬件</summary>
        public override bool IsConnected { get { return _isConnected; } }

        /// <summary>当前采集扫描率(Hz)</summary>
        public override double ScanRateHz { get { return _scanRateHz; } }

        /// <summary>实测帧率(Hz)</summary>
        public double MeasuredFps { get { return _measuredFps; } }

        /// <summary>
        /// 校准数据快照，由 InternalRefCornerPts 回调捕获并存储，
        /// 供业务层生成轮廓 Mat 时绘制校准框与毫米刻度；本类只存储不做显示处理。
        /// </summary>
        public override LineLaserCalibration Calibration
        {
            get { lock (_calibLock) { return _calibration; } }
        }

        #endregion

        #region 英莱算法结果 (InspectResult)

        /// <summary>最近一次算法结果快照，中性结构（厂商无关）</summary>
        public LineLaserSeamResult LastInspectResult
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
            RaiseStateChanged();
            return true;
        }

        /// <summary>配置默认回调周期</summary>
        /// <returns>true=成功</returns>
        public override bool ConfigureDefaultCommPeriod()
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

        /// <summary>启动连续采集（未连接时先按配置 IP 打开相机）</summary>
        public override void SensorRunContinue()
        {
            if (!_isConnected)
            {
                OpenSensor(_sensorIp);
            }
            if (!_isConnected) return;
            if (!IsRunning)
            {
                SetSensor(true);
            }
        }

        /// <summary>停止连续采集</summary>
        public override void SensorStop()
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
        public override void Dispose()
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
            // 显示帧由业务层生成后经 UpdateDisplayMat 回写，此处传 null 即释放基类持有的 Mat
            UpdateDisplayMat(null);
            GC.SuppressFinalize(this);
        }

        ~IntelligentLaserCam()
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
        public override bool ConnectManual(string ip)
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
        public override void DisconnectManual()
        {
            ShutdownPeripherals();
            _isConnected = false;
            RaiseStateChanged();
        }

        /// <summary>开启/关闭传感器采集</summary>
        /// <param name="enable">true=开启</param>
        public override void SetSensor(bool enable)
        {
            if (!_isConnected) return;
            IntelligentLaser_export.SetSensorEnable(enable);
            _isCameraOn = enable;
            if (enable)
            {
                // 开启采集时重置实测 FPS 计数器，确保首帧被剔除，避免初始值错误
                ResetMeasuredFps();
            }
            RaiseStateChanged();
        }

        /// <summary>开启/关闭激光</summary>
        /// <param name="enable">true=开</param>
        public override void SetLaser(bool enable)
        {
            if (!_isConnected) return;
            IntelligentLaser_export.SetLaserEnable(enable);
            _isLaserOn = enable;
        }

        /// <summary>获取激光开关状态</summary>
        /// <returns>true=开</returns>
        public override bool IsLaserOn()
        {
            return _isConnected && _isLaserOn;
        }

        /// <summary>获取相机（传感器）状态</summary>
        /// <returns>true=开</returns>
        public override bool IsCameraOn()
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
                    // 只捕获与存储校准数据，绘制由业务层 ProfileMatRenderer 完成
                    var snap = new LineLaserCalibration(
                        calib.Center,
                        calib.LeftTopCorner,
                        calib.RightTopCorner,
                        calib.LeftBottomCorner,
                        calib.RightBottomCorner);
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
                    if (inspect == null)
                    {
                        lock (_ilLock) { _lastInspect = LineLaserSeamResult.Invalid(0, -1); }
                        return;
                    }
                    if (inspect.ContourData == null)
                    {
                        lock (_ilLock) { _lastInspect = LineLaserSeamResult.Invalid(0, -1); }
                        return;
                    }

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

                    // 捕获英莱 SDK 自有算法解析结果（InspectResult[0]）并映射为中性结构。
                    // 必须在 using 块内读取（块结束即 Dispose 原生内存）。字段含义见 [[api/IntelligentLaserSDK_CSharp_DevManual]] §5。
                    var results = inspect.InspectResult;
                    if (results != null && results.Length > 0)
                    {
                        var r0 = results[0];
                        LineLaserSeamResult snap = new LineLaserSeamResult(
                            r0.ParseRes == 1,
                            r0.ParseRes,
                            r0.ErrorCode,
                            r0.Timestamp,
                            r0.CurrentFeaturePoint.YPos,
                            r0.CurrentFeaturePoint.ZPos,
                            r0.Width[0],
                            r0.Width[1],
                            r0.HeightOrDepth[0],
                            r0.HeightOrDepth[1],
                            r0.Area);
                        lock (_ilLock)
                        {
                            _lastInspect = snap;
                        }
                    }
                    else
                    {
                        // 空帧/无算法结果：写入无效快照，避免上一帧快照被误判为有效
                        lock (_ilLock) { _lastInspect = LineLaserSeamResult.Invalid(0, -1); }
                    }
                }

                // 空帧：标记失败，不再触发任何事件
                if (cloud == null || cloud.PointCount == 0)
                {
                    _lastSuccess = false;
                    return;
                }
                _lastSuccess = true;

                // ===== 第二步：只做回调触发，业务处理全部交给 LineLaserManager =====
                // 实现层不生成 Mat、不做数据记录、不区分手动/自动模式——
                // 结果处理（Y 轴 PVT 点计算与下载）与轮廓处理（显示 Mat、ScottPlot 点缓冲）
                // 分别由 LineLaserManager 订阅 ResultReady 与 ProfileReady 后执行。
                _acquisitionCompletedSignal.Set();

                LineLaserSeamResult snapResult;
                lock (_ilLock) { snapResult = _lastInspect; }
                RaiseResultReady(snapResult, _measuredFps);
                RaiseProfileReady(cloud, _measuredFps);
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

        #endregion
    }
}
