﻿﻿﻿using AdaWeldSystem.LineLaserCam.ILineLaser;
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
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

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
        private double _scanRateHz;   // STANDERD 目标 60Hz，未读到时回退

        // 最近一次厂商失败现场，DescribeLastError 唯一数据源
        private int _lastErrorCode = int.MinValue;
        private bool _lastErrorLocal = false;
        private string _lastErrorOperation = string.Empty;
        private readonly object _errorLock = new object();

        // 英莱算法结果快照，回调填充，业务只读
        private LineLaserSeamResult _lastInspect;
        private readonly object _ilLock = new object();

        // 最近轮廓点数，回调更新，供界面显示
        private volatile int _lastContourCount;

        // 实测帧率取帧间隔倒数，首帧剔除
        private DateTime _lastFrameTime = DateTime.MinValue;
        private volatile bool _firstFrameSkipped = false;
        private double _measuredFps = 0.0;

        // 上次硬件查帧率时间，500ms 节流
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
        // 回调排空超时，缺省 200ms，避免关闭卡顿
        // 运行期取系统设置，改配置无需重启
        private static int CallbackWaitTimeoutMs { get { return DeviceWaitSettings.LineLaserCallbackMs; } }

        // 校准快照，回调捕获，业务层读取后绘制校准框
        private LineLaserCalibration _calibration;
        private readonly object _calibLock = new object();

        // Job 参数元数据字典，初始化回调填充。
        private readonly Dictionary<string, IlJobParamInfo> _jobMeta = new Dictionary<string, IlJobParamInfo>();
        // 当前 Job 参数缓存（OnJobParamsChanged 回调替换式重建，供 UI 展示与下发）
        private List<IlJobParamInfo> _jobParams;
        private readonly object _jobLock = new object();

        /// <summary>创建英莱线激光相机，键名固定为「英莱线激光」</summary>
        public IntelligentLaserCam()
            : base("英莱线激光")
        {
            // 公共属性初值在构造函数显式赋值（避免 auto-property initializer）
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

        /// <summary>校准数据快照，由 InternalRefCornerPts 回调捕获。 供业务层生成轮廓 Mat 时</summary>
        public override LineLaserCalibration Calibration
        {
            get { lock (_calibLock) { return _calibration; } }
        }

        /// <summary>记录一次厂商返回码（0=成功，清空现场）</summary>
        private void RecordVendorResult(string operation, int code)
        {
            lock (_errorLock)
            {
                _lastErrorOperation = operation;
                _lastErrorCode = code == 0 ? int.MinValue : code;
                _lastErrorLocal = false;
            }
        }

        /// <summary>记录本机拦截异常，不编造码值</summary>
        private void RecordLocalFailure(string operation)
        {
            lock (_errorLock)
            {
                _lastErrorOperation = operation;
                _lastErrorCode = int.MinValue;
                _lastErrorLocal = true;
            }
        }

        /// <summary>失败原因，厂商码的唯一产出点</summary>
        /// <param name="context">失败语境（连接 / 初始化 / 识别 / 指令返回码）</param>
        /// <returns>非空的原因描述</returns>
        public override string DescribeLastError(LineLaserErrorContext context)
        {
            if (context == LineLaserErrorContext.Recognition)
            {
                LineLaserSeamResult r = LastInspectResult;
                return string.Format("识别失败 ParseRes={0} ErrorCode={1}", r.ParseRes, r.ErrorCode);
            }

            int code;
            bool local;
            string operation;
            lock (_errorLock)
            {
                code = _lastErrorCode;
                local = _lastErrorLocal;
                operation = _lastErrorOperation;
            }

            if (local)
            {
                return string.Format("未获得厂商返回码（{0} 未执行：相机未连接、入参无效或 SDK 调用异常）", operation);
            }
            if (code == int.MinValue)
            {
                return "未提供返回码（尚无失败记录）";
            }
            if (context == LineLaserErrorContext.Connection && operation == "ConnectToSensor")
            {
                return string.Format("连接失败，厂商返回码 0x{0:X}（Core 服务未连上）", code);
            }
            return string.Format("厂商返回码 {0}（{1}）", code, operation);
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
                    RecordVendorResult("InitSDK", initRet);
                    Log(string.Format("InitSDK 失败 ret {0}", initRet), MessageLevel.Error);
                    return false;
                }

                // 必须在 ConnectToSensor 之前注册所有回调！
                // 连接完成时一次性触发角点与参数回调，
                // 注册晚了回调会丢失：校准框缺失、参数为空。
                RegisterCallbacks();
                _sdkInitialized = true;
            }

            int ret = IntelligentLaser_export.ConnectToSensor(ip);
            // 位掩码：某位 == 1 表示对应服务「未」连上；Core 服务必须连上才能出图。
            if ((ret & (int)EmIntelligentConnectStatus.EM_INTELLIGENT_CONNECT_E_CORE) != 0)
            {
                RecordVendorResult("ConnectToSensor", ret);
                Log(string.Format("ConnectToSensor 未连上 Core 服务 ret 0x{0:X} 连接失败", ret),
                    MessageLevel.Debug);
                // SDK 已初始化则保留（下次重连继续用），不做 UnInitSDK / Free 委托
                // 连接失败仍保留初始化态，避免反复初始化
                return false;
            }

            _isConnected = true;
            RecordVendorResult("ConnectToSensor", 0);
            _peripheralsOff = false; // 重连后外设状态重置，允许再次关闭
            // 连接成功即从硬件同步一次实际状态，
            // 之后由 SetLaser/SetSensor 本地更新，UI 不再频繁读硬件。
            SyncHardwareStatus();
            RaiseStateChanged();
            return true;
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
                        MessageLevel.Debug);
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
            // 显示帧由业务层回写，传 null 即释放
            UpdateDisplayMat(null);
            GC.SuppressFinalize(this);
        }

        ~IntelligentLaserCam()
        {
            ReleaseNativeResources();
        }

        #endregion

        #region 手动控制与 JOB 管理（英莱相机页面专用，分步控制，不经工作流）

        /// <summary>最近一次轮廓点数量</summary>
        public int LastContourPointCount { get { return _lastContourCount; } }

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

        /// <summary>扫描网口下的英莱相机</summary>
        /// <returns>发现的相机列表</returns>
        public List<IlSensorInfo> ScanSensors()
        {
            var result = new List<IlSensorInfo>();
            bool tempInit = false;
            try
            {
                // 扫描只需 SDK 已初始化：复用 _sdkInitialized，不重复 InitSDK，finally 不误 UnInitSDK
                if (!_sdkInitialized)
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
                    var info = new IlSensorInfo
                    {
                        Ip = ReadAsciiBytes(d.SzIP),
                        SerialNumber = ReadAsciiBytes(d.SzSN),
                        SoftwareVersion = ReadAsciiBytes(d.SzSoftWareVer),
                        HardwareVersion = ReadAsciiBytes(d.SzHardWareVer),
                        ProtocolVersion = ReadAsciiBytes(d.SzProtocolVer),
                        SensorType = ReadAsciiBytes(d.SzSensorType)
                    };
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
                    var param = new IlCameraParam
                    {
                        Exposure = p.Exptime,
                        Gamma = p.Gama,
                        LaserPower = p.Power
                    };
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
            if (!_isConnected || param == null)
            {
                RecordLocalFailure("设置相机参数");
                return -1;
            }
            try
            {
                var p = new IntelligentSensorBaseParams
                {
                    Exptime = param.Exposure,
                    Gama = param.Gamma,
                    Power = param.LaserPower
                };
                int ret = IntelligentLaser_export.SetSensorBaseParam(p);
                RecordVendorResult("设置相机参数", ret);
                return ret;
            }
            catch (Exception ex)
            {
                RecordLocalFailure("设置相机参数");
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
                    Log(string.Format("已连接 IP {0}", ip), MessageLevel.Debug);
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
                // 开启采集重置实测帧率，首帧剔除
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
                    // 该接头应显示的参数默认值（技术员确认）
                    if (item["display"] is JObject display)
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
            return jt?.NameZh;
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
            if (!_isConnected)
            {
                RecordLocalFailure("切换 JOB");
                return -1;
            }
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = (int)EmJobSettingCmd.EM_INTELLIGENT_JOB_SETTING_SWITCH;
                    ctrl.DwJobId = jobId;
                    int ret = IntelligentLaser_export.SetSensorJobInfo(ctrl);
                    RecordVendorResult("切换 JOB", ret);
                    return ret;
                }
            }
            catch (Exception ex)
            {
                RecordLocalFailure("切换 JOB");
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
            if (!_isConnected)
            {
                RecordLocalFailure("新建 JOB");
                return -1;
            }
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = (int)EmJobSettingCmd.EM_INTELLIGENT_JOB_SETTING_CREATE;
                    ctrl.DwJointType = jointType;
                    ctrl.SzJobDesc = Make64Bytes(desc);
                    int ret = IntelligentLaser_export.SetSensorJobInfo(ctrl);
                    RecordVendorResult("新建 JOB", ret);
                    return ret;
                }
            }
            catch (Exception ex)
            {
                RecordLocalFailure("新建 JOB");
                Log("CreateJob 异常 " + ex.Message, MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>删除 JOB</summary>
        /// <param name="jobId">JOB ID</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int DeleteJob(int jobId)
        {
            if (!_isConnected)
            {
                RecordLocalFailure("删除 JOB");
                return -1;
            }
            try
            {
                using (var ctrl = new IntelligentJobCtrl())
                {
                    ctrl.DwCmd = (int)EmJobSettingCmd.EM_INTELLIGENT_JOB_SETTING_DELETE;
                    ctrl.DwJobId = jobId;
                    int ret = IntelligentLaser_export.SetSensorJobInfo(ctrl);
                    RecordVendorResult("删除 JOB", ret);
                    return ret;
                }
            }
            catch (Exception ex)
            {
                RecordLocalFailure("删除 JOB");
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
            // 注册回调：签名为 (回调委托, pUser)。uint 是回调委托第三形参，非注册参数。
            IntelligentLaser_export.IntelligentRegisterCallBack_MillionOutlineInspect(
                _cbMillionOutline, GCHandle.ToIntPtr(_hostHandle));
            // 注册参数全量回调，缓存到参数表供界面
            _cbInitJobParams = new IntelligentJobParamInitCallBack(InitJobParamsCallback);
            _hCbInitJobParams = GCHandle.Alloc(_cbInitJobParams);
            IntelligentLaser_export.IntelligentRegisterCallBack_RecvInitJobParamsInfo(
                _cbInitJobParams, GCHandle.ToIntPtr(_hostHandle));
            // 注册参数变更回调，按名增量刷新缓存
            _cbJobParamsChanged = new IntelligentJobParamChangedCallBack(JobParamsChangedCallback);
            _hCbJobParamsChanged = GCHandle.Alloc(_cbJobParamsChanged);
            IntelligentLaser_export.IntelligentRegisterCallBack_OnJobParamsChanged(
                _cbJobParamsChanged, GCHandle.ToIntPtr(_hostHandle));
            // 注册角点回调，捕获校准数据供绘图
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
            // 超时仍有在途回调：记警告后继续释放
            int remaining = System.Threading.Interlocked.CompareExchange(ref _activeCallbacks, 0, 0);
            if (remaining > 0)
            {
                Log(string.Format("WaitForCallbacksToFinish 超时(上限 {0}ms)，仍有 {1} 个在途回调",
                    CallbackWaitTimeoutMs, remaining), MessageLevel.Warning);
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
                    // 只捕获与存储校准数据，绘制由 EmguALG.LineLaserContour 完成
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
            try
            {
                if (BuildCloudFromInspect(pOutlineData, out PointCloudData cloud))
                {
                    _lastSuccess = true;
                    HandleFrameReady(cloud);
                    return;
                }
                // 空帧仍发画面事件，供调试页清屏重绘，
                // 证明传感器确实已开启 —— 渲染器 Render 支持 cloud=null（清屏 + 画角点）。
                // 不发结果就绪事件，避免刷新有效标志
                // 空帧高频触发会干扰首个结果超时判定。
                _lastSuccess = false;
                HandleEmptyFrame();
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

        /// <summary>构建点云并刷新帧率与识别快照</summary>
        /// <param name="pOutlineData">原生轮廓数据指针</param>
        /// <param name="cloud">构建出的点云，无有效帧时为 null</param>
        /// <returns>取到非空帧返回 true</returns>
        private bool BuildCloudFromInspect(IntPtr pOutlineData, out PointCloudData cloud)
        {
            cloud = null;
            // 务必 Dispose，否则 100Hz 下快速泄漏；
            // 原生字段须在 using 内读取，Mat 外部生成
            using (var inspect = IntelligentMillionOutlineInspect.__GetOrCreateInstance(pOutlineData))
            {
                if (inspect == null || inspect.ContourData == null)
                {
                    SetInvalidInspect();
                    return false;
                }

                uint count = inspect.ContourData.ContourPointCount;
                if (count > 2048) count = 2048;   // 上限封顶，防止越界读相邻内存
                _lastContourCount = (int)count;
                UpdateFrameRate(DateTime.Now);

                // 采用官方 .NET Framework Demo 的 PointArray[] 托管访问（免手搓原生布局/偏移）
                cloud = new PointCloudData();
                var pts = inspect.ContourData.PointArray;
                int n = (pts == null) ? 0 : Math.Min((int)count, pts.Length);
                for (int i = 0; i < n; i++)
                {
                    cloud.AddPoint(0.0f, pts[i].YPos, pts[i].ZPos);
                }

                // 捕获英莱 SDK 自有算法解析结果（InspectResult[0]）并映射为中性结构
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
                    // 空帧写入无效快照，避免沿用上一帧
                    SetInvalidInspect();
                }
            }
            return cloud.PointCount > 0;
        }

        /// <summary>刷新实测帧率与传感器帧率</summary>
        /// <param name="now">本帧时刻</param>
        private void UpdateFrameRate(DateTime now)
        {
            // 实测帧率：首帧剔除，从第二帧开始用 1.0 / 帧间隔(秒)；
            // 重开后首次计算同样需剔除
            if (_firstFrameSkipped)
            {
                double intervalMs = (now - _lastFrameTime).TotalMilliseconds;
                if (intervalMs > 0.0) _measuredFps = 1000.0 / intervalMs;
            }
            else
            {
                _firstFrameSkipped = true;
            }
            _lastFrameTime = now;

            // 传感器帧率每 500ms 读一次硬件
            if ((now - _lastSensorFpsUpdateTime).TotalMilliseconds < SensorFpsUpdateIntervalMs) return;
            float hwFps = 0f;
            if (IntelligentLaser_export.FetchSensorFps(ref hwFps, 1u) == 0 && hwFps > 0f)
            {
                _scanRateHz = (double)hwFps;
            }
            _lastSensorFpsUpdateTime = now;
        }

        /// <summary>写入无效识别快照（空帧或无算法结果）</summary>
        private void SetInvalidInspect()
        {
            lock (_ilLock) { _lastInspect = LineLaserSeamResult.Invalid(0, -1); }
        }

        /// <summary>置采集信号并触发结果与轮廓事件</summary>
        /// <param name="cloud">本帧点云</param>
        private void HandleFrameReady(PointCloudData cloud)
        {
            // 实现层不生成 Mat、不记录、不分模式：
            // 结果处理与轮廓处理分别由 LineLaserManager 订阅 ResultReady 与 ProfileReady 后执行
            _acquisitionCompletedSignal.Set();
            LineLaserSeamResult snapResult;
            lock (_ilLock) { snapResult = _lastInspect; }
            RaiseResultReady(snapResult, _measuredFps);
            RaiseProfileReady(cloud, _measuredFps);
        }

        /// <summary>空帧处理：只触发画面事件，点云传 null 由渲染器清屏</summary>
        private void HandleEmptyFrame()
        {
            _acquisitionCompletedSignal.Set();
            RaiseProfileReady(null, _measuredFps);
        }

        #endregion

        #region 英莱 Job Parameter

        /// <summary>Job参数变更事件</summary>
        public event EventHandler JobParamsChanged;

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
                            var info = new IlJobParamInfo
                            {
                                Name = name,
                                ValueType = valueType
                            };
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
                    Log(string.Format("线激光参数装载 {0} 组", arraySize), MessageLevel.Debug);
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
                        var info = new IlJobParamInfo
                        {
                            Name = ReadAsciiBytes(p.SzParamName),
                            ValueType = p.DwValueType
                        };
                        if (p.DwValueType == 1u)
                            info.StringValue = ReadAsciiBytes(p.curValue.SzValue);
                        else if (p.DwValueType == 2u)
                            info.FloatValue = p.curValue.FValue;
                        else if (p.DwValueType == 3u)
                            info.IntValue = (long)p.curValue.DwValue;
                        // 范围从元数据字典补全（Changed 回调结构无 value_Range）
                        if (_jobMeta.TryGetValue(info.Name, out IlJobParamInfo meta))
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
                    // 初次/清空/变更超三成判为全量推送
                    bool fullSet = _jobParams == null || _jobParams.Count == 0
                        || changed.Count >= Math.Max(3, _jobParams.Count / 3);
                    if (fullSet)
                    {
                        // 全量推送：替换重建，只留当前 JOB 参数
                        _jobParams = new List<IlJobParamInfo>(changed.Values);
                    }
                    else
                    {
                        // 少量变更：按名更新，仅改已有参数，
                        // 避免分批推送累积无关参数。
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
                // 拷贝列表，元素共享引用只读
                return new List<IlJobParamInfo>(_jobParams);
            }
        }

        /// <summary>下发 Job Parameter 到设备</summary>
        /// <param name="values">参数列表</param>
        /// <returns>SDK 返回值，0=成功</returns>
        public int ApplyJobParams(IEnumerable<IlJobParamInfo> values)
        {
            if (!_isConnected)
            {
                RecordLocalFailure("下发参数");
                return -1;
            }
            var arr = new List<TIntelligentJobParam>();
            foreach (var v in values)
            {
                // ✅ 已对照 M3 CppSharp 生成源码 (IntelligentLaser.cs:3247/3476) + 官方 .NET Framework Demo (Form1.cs:429) 确认：
                // 嵌套「类型名」为 TIntelligentJobParam.CurValue（大写 C，partial struct CurValue），
                // 「属性名」为 curValue（小写 c，返回 CurValue 实例）。下方对象初始化器使用类型名 CurValue。
                var p = new TIntelligentJobParam
                {
                    SzParamName = Make64Bytes(v.Name),
                    DwValueType = v.ValueType
                };
                if (v.ValueType == 1u)
                    p.curValue = new TIntelligentJobParam.CurValue { SzValue = Make64Bytes(v.StringValue) };
                else if (v.ValueType == 2u)
                    p.curValue = new TIntelligentJobParam.CurValue { FValue = (float)v.FloatValue };
                else if (v.ValueType == 3u)
                    p.curValue = new TIntelligentJobParam.CurValue { DwValue = (uint)v.IntValue };
                arr.Add(p);
            }
            int ret = IntelligentLaser_export.SetJobParamter(arr.ToArray(), (uint)arr.Count);
            RecordVendorResult("下发参数", ret);
            return ret;
        }

        #endregion
    }
}

