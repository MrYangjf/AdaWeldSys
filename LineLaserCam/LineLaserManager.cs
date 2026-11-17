﻿using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.EmguALG.LineLaserContour;
using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.ProductFileControl;
using Emgu.CV;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.WeldParamControl;

namespace AdaWeldSystem.LineLaserCam
{
    /// <summary>轮廓帧快照，池化复用，按版本号轮询</summary>
    /// <remarks>双缓冲整体交换，读写不落同一数组；数组复用零 GC，版本号判新帧。</remarks>
    public class ProfileSnapshot
    {
        #region 私有变量

        private readonly double[] _ys;
        private readonly double[] _zs;

        #endregion

        #region 公共变量

        /// <summary>单帧最大点数，唯一来源为算法层</summary>
        public const int MaxPoints = LineLaserProfileData.MaxPoints;

        /// <summary>轮廓点横向坐标（毫米），有效长度见 Count</summary>
        public double[] Ys { get { return _ys; } }

        /// <summary>轮廓点高度坐标（毫米），有效长度见 Count</summary>
        public double[] Zs { get { return _zs; } }

        /// <summary>本帧有效点数</summary>
        public int Count { get; private set; }

        /// <summary>本帧是否有有效识别结果</summary>
        public bool HasResult { get; private set; }

        /// <summary>焊缝特征点横向坐标（毫米）</summary>
        public double FeatureY { get; private set; }

        /// <summary>焊缝特征点高度坐标（毫米）</summary>
        public double FeatureZ { get; private set; }

        /// <summary>实测帧率（Hz）</summary>
        public double MeasuredFps { get; private set; }

        /// <summary>快照版本号，每次填充后递增</summary>
        public long Version { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建轮廓帧快照并分配复用数组</summary>
        public ProfileSnapshot()
        {
            _ys = new double[MaxPoints];
            _zs = new double[MaxPoints];
        }

        #endregion

        #region 公共函数

        /// <summary>填充快照内容并推进版本号</summary>
        /// <param name="ys">横向坐标源数组</param>
        /// <param name="zs">高度坐标源数组</param>
        /// <param name="count">有效点数，超过上限时截断</param>
        /// <param name="hasResult">是否有有效识别结果</param>
        /// <param name="featureY">特征点横向坐标</param>
        /// <param name="featureZ">特征点高度坐标</param>
        /// <param name="measuredFps">实测帧率</param>
        /// <param name="version">新版本号</param>
        public void Fill(double[] ys, double[] zs, int count, bool hasResult,
            double featureY, double featureZ, double measuredFps, long version)
        {
            int n = (count > MaxPoints) ? MaxPoints : count;
            if (n < 0) n = 0;

            for (int i = 0; i < n; i++)
            {
                _ys[i] = ys[i];
                _zs[i] = zs[i];
            }

            Count = n;
            HasResult = hasResult;
            FeatureY = featureY;
            FeatureZ = featureZ;
            MeasuredFps = measuredFps;
            Version = version;
        }

        /// <summary>清空为无效帧并推进版本号，用于停机与切换相</summary>
        /// <remarks>机</remarks>
        /// <param name="version">新版本号</param>
        public void Clear(long version)
        {
            Count = 0;
            HasResult = false;
            FeatureY = 0.0;
            FeatureZ = 0.0;
            MeasuredFps = 0.0;
            Version = version;
        }

        #endregion
    }

    /// <summary>线激光管理器</summary>
    /// <remarks>全系统取相机实例一律走本管理器；
    /// 本类管注册/连接/配置与事件，PVT 归 SeamTrackingAlgorithm。</remarks>
    public class LineLaserManager : IDisposable
    {
        #region 私有变量

        private const string Tag = "线激光管理器";

        private static readonly Lazy<LineLaserManager> _lazy =
            new Lazy<LineLaserManager>(() => new LineLaserManager());

        private IntelligentLaserCam.IntelligentLaserCam _intelligentInstance;
        private VirtualCam.VirtualCam _virtualInstance;
        private LineLaserCameraBase _active;
        private bool _working;
        private bool _disposed;

        private LineLaserSeamResult _lastResult;

        // 轮廓处理链路：点缓冲（双缓冲快照）+ 显示 Mat 渲染
        private readonly double[] _ysBuffer = new double[ProfileSnapshot.MaxPoints];
        private readonly double[] _zsBuffer = new double[ProfileSnapshot.MaxPoints];
        // 算法层轮廓 DTO：与上面两条缓冲共用，零拷贝；只负责把点数同步给渲染算法
        private readonly LineLaserProfileData _profileData;
        // 标定缓存：快照引用变化才重建 DTO
        private LineLaserCalibration _calibSource;
        private LineLaserCalibrationData _calibData;
        private ProfileSnapshot _front;
        private ProfileSnapshot _back;
        private long _profileVersion;
        private DateTime _lastDisplayTime = DateTime.MinValue;

        /// <summary>单活约束：非活动事件累计丢弃数</summary>
        private long _inactiveDropCount;
        /// <summary>非活动事件日志的最近输出时刻</summary>
        private int _inactiveLogTick;
        /// <summary>日志限流间隔，防止异常刷屏</summary>
        private const int InactiveLogIntervalMs = 5000;

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static LineLaserManager Instance { get { return _lazy.Value; } }

        /// <summary>相机注册表</summary>
        public CameraRegistry Cameras { get; private set; }

        /// <summary>线激光配置，默认相机与相机 IP 的唯一入口</summary>
        public LineLaserConfig Config { get; private set; }

        /// <summary>当前选中相机，未选则按默认惰性创建</summary>
        public LineLaserCameraBase Active
        {
            get
            {
                if (_active == null)
                {
                    if (Config.DefaultKind == LineLaserKind.Virtual)
                    {
                        SelectVirtual();
                    }
                    else
                    {
                        SelectIntelligentLaser();
                    }
                }
                return _active;
            }
        }

        /// <summary>
        /// 英莱相机实例，首次访问时创建登记。
        /// 厂商实例不暴露，界面经本属性取得。
        /// </summary>
        public IntelligentLaserCam.IntelligentLaserCam IntelligentLaser
        {
            get
            {
                if (_intelligentInstance == null)
                {
                    _intelligentInstance = new IntelligentLaserCam.IntelligentLaserCam();
                    Register(_intelligentInstance.CameraName, _intelligentInstance);
                    ApplyConfig();
                }
                return _intelligentInstance;
            }
        }

        /// <summary>虚拟相机实例，首次访问时创建</summary>
        public VirtualCam.VirtualCam VirtualCamera
        {
            get
            {
                if (_virtualInstance == null)
                {
                    _virtualInstance = new VirtualCam.VirtualCam();
                    Register(_virtualInstance.CameraName, _virtualInstance);
                    ApplyConfig();
                }
                return _virtualInstance;
            }
        }

        /// <summary>当前选中相机的键名，无相机时为空串</summary>
        public string ActiveName { get { return _active == null ? string.Empty : _active.CameraName; } }

        /// <summary>当前相机种类</summary>
        public LineLaserKind Kind { get { return _active == null ? LineLaserKind.None : _active.Kind; } }

        /// <summary>当前是否为虚拟调试相机</summary>
        public bool IsVirtual { get { return Kind == LineLaserKind.Virtual; } }

        /// <summary>工作流是否正在驱动相机出图</summary>
        public bool IsWorking { get { return _working; } }

        /// <summary>
        /// 线激光相机连接状态（与工作流程状态相互独立）。
        /// 工作时由工作流驱动，其余按连接态派生。
        /// </summary>
        public LineLaserConnectionState ConnectionState
        {
            get
            {
                if (_working) return LineLaserConnectionState.Working;
                if (_active == null) return LineLaserConnectionState.Disconnected;
                return _active.ConnectionState;
            }
        }

        /// <summary>最近轮廓快照，界面按版本号轮询</summary>
        public ProfileSnapshot Profile { get { return _front; } }

        /// <summary>最近一次焊缝识别结果</summary>
        public LineLaserSeamResult LastResult { get { return _lastResult; } }

        /// <summary>是否生成显示，默认关闭</summary>
        public bool DisplayEnabled { get; private set; }

        /// <summary>显示处理目标帧率（Hz），用于轮廓转 Mat 的节流</summary>
        public double DisplayFps { get; private set; }

        /// <summary>是否生成 Mat，否则只更新点缓冲</summary>
        public bool ConvertMatEnabled { get; private set; }

        /// <summary>结果就绪事件，转发自已登记相机</summary>
        public event EventHandler<LineLaserResultEventArgs> ResultReady;

        /// <summary>轮廓事件，仅显示开启时触发</summary>
        public event EventHandler<LineLaserMatEventArgs> MatUpdated;

        /// <summary>相机状态变化事件，转发自已登记相机</summary>
        public event EventHandler<LineLaserStateEventArgs> CameraStateChanged;

        /// <summary>Job 切换成功事件，供记录留痕</summary>
        public event EventHandler<int> JobChanged;

        /// <summary>当前相机失败原因，只读成品文本</summary>
        /// <param name="context">失败语境（连接 / 初始化 / 识别 / 指令返回码）</param>
        /// <returns>非空的原因描述；未选相机时返回明确的未提供说明</returns>
        public string DescribeLastError(LineLaserErrorContext context)
        {
            LineLaserCameraBase camera = Active;
            return camera == null
                ? "未提供返回码（未选择线激光相机）"
                : camera.DescribeLastError(context);
        }

        #endregion

        #region 构造函数

        private LineLaserManager()
        {
            Cameras = new CameraRegistry();
            Config = new LineLaserConfig();
            Config.LoadConfig();

            _front = new ProfileSnapshot();
            _back = new ProfileSnapshot();
            _profileData = new LineLaserProfileData(_ysBuffer, _zsBuffer);
            _lastResult = LineLaserSeamResult.Invalid(0, 0);

            DisplayEnabled = Config.DisplayEnabled;
            DisplayFps = Config.DisplayFps;
            ConvertMatEnabled = Config.ConvertMatEnabled;
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>把配置套用到已创建的相机实例</summary>
        private void ApplyConfig()
        {
            DisplayEnabled = Config.DisplayEnabled;
            DisplayFps = Config.DisplayFps;
            ConvertMatEnabled = Config.ConvertMatEnabled;

            if (_intelligentInstance != null)
            {
                _intelligentInstance.SensorIp = Config.SensorIp;
            }

            if (_virtualInstance != null)
            {
                // 模拟统一平直搭接焊缝，仅套数据频率
                _virtualInstance.FrameRateHz = Config.LineLaserDataFrequencyHz;
            }
        }

        /// <summary>切换相机并释放上一台，单活约束</summary>
        /// <param name="camera">相机对象</param>
        private void SetActive(LineLaserCameraBase camera)
        {
            LineLaserCameraBase previous = _active;
            if (ReferenceEquals(previous, camera))
            {
                _active = camera;
                return;
            }

            // 先切引用再停旧机，在途帧被丢弃
            _active = camera;
            if (previous != null) ReleaseCamera(previous, "切换活动相机");
        }

        /// <summary>停止并断开相机，物理只留一台</summary>
        /// <param name="camera">待释放的相机</param>
        /// <param name="reason">释放原因，写入日志</param>
        private void ReleaseCamera(LineLaserCameraBase camera, string reason)
        {
            if (camera == null) return;
            bool running = camera.IsRunning;
            bool connected = camera.IsConnected;
            if (!running && !connected) return;

            if (running)
            {
                try { camera.SensorStop(); }
                catch (Exception ex) { Log("停止 " + camera.CameraName + " 异常：" + ex.Message, MessageLevel.Warning); }
            }

            if (camera.IsConnected)
            {
                try { camera.DisconnectManual(); }
                catch (Exception ex) { Log("断开 " + camera.CameraName + " 异常：" + ex.Message, MessageLevel.Warning); }
            }

            Log(reason + "：已停止并断开 " + camera.CameraName + "（单活约束：一次只允许连接一种线激光或虚拟相机）");
        }

        /// <summary>查找其它仍在出图的已登记相机</summary>
        /// <param name="except">允许的当前相机</param>
        /// <returns>另一台正在出图的相机；无则返回 null</returns>
        private LineLaserCameraBase FindOtherRunningCamera(LineLaserCameraBase except)
        {
            foreach (LineLaserCameraBase camera in Cameras.GetAll())
            {
                if (camera == null || ReferenceEquals(camera, except)) continue;
                if (camera.IsRunning) return camera;
            }
            return null;
        }

        /// <summary>校验发送方是否为活动相机，非活动丢弃</summary>
        /// <param name="sender">事件发送方相机</param>
        /// <param name="eventName">事件名，写入日志</param>
        /// <returns>true 表示发送方就是当前活动相机，可继续处理</returns>
        private bool IsActiveSender(object sender, string eventName)
        {
            if (ReferenceEquals(sender, _active)) return true;

            Interlocked.Increment(ref _inactiveDropCount);
            int now = Environment.TickCount;
            int last = Volatile.Read(ref _inactiveLogTick);
            if (unchecked(now - last) >= InactiveLogIntervalMs
                && Interlocked.CompareExchange(ref _inactiveLogTick, now, last) == last)
            {
                Log("已拦截非活动相机的" + eventName + "：发送方 "
                    + (!(sender is LineLaserCameraBase camera) ? "未知" : camera.CameraName)
                    + "，当前活动 " + (_active == null ? "无" : _active.CameraName)
                    + "，累计丢弃 " + Interlocked.Read(ref _inactiveDropCount) + " 帧",
                    MessageLevel.Warning);
            }
            return false;
        }

        /// <summary>转发相机状态变化事件</summary>
        /// <param name="sender">事件源相机</param>
        /// <param name="e">状态事件参数</param>
        private void OnCameraStateChanged(object sender, LineLaserStateEventArgs e)
        {
            CameraStateChanged?.Invoke(this, e);
        }

        /// <summary>结果处理：缓存结果并向外转发事件（Y 轴 PVT 由运控工</summary>
        /// <remarks>作流经 WeldParamControl 轨迹构造器写入）</remarks>
        /// <param name="sender">事件源相机</param>
        /// <param name="e">结果事件参数</param>
        private void OnCameraResultReady(object sender, LineLaserResultEventArgs e)
        {
            if (!IsActiveSender(sender, "结果事件")) return;
            _lastResult = e.Result;
            ResultReady?.Invoke(this, e);
        }

        /// <summary>轮廓处理：产品记录、更新点缓冲、按需生成显示 Mat</summary>
        /// <param name="sender">事件源相机</param>
        /// <param name="e">轮廓事件参数；<c>e.Cloud</c> 为 null 表示空帧（传感器已开但无识别结果）</param>
        private void OnCameraProfileReady(object sender, LineLaserProfileEventArgs e)
        {
            // 只接受活动相机帧，其余丢弃
            if (!IsActiveSender(sender, "轮廓帧")) return;

            if (e.Cloud == null)
            {
                // 空帧：只重绘画面（渲染器 Render 支持 null ⇒ 清屏 + 画校准四边框）。
                // ⛔ 不走 AddFrame / UpdateProfileSnapshot —— 两者都要访问 cloud.Points，空帧会 NRE；
                //    也不写点缓冲，避免前台曲线闪断。
                if (DisplayEnabled) RenderDisplayFrame(null);
                return;
            }

            // 数据记录：AddFrame 内部已有 ClonePointCloud + lock，未录制时直接返回零开销；
            // 传捕获时刻机器人 X，使轮廓沿行走轴对齐
            ProductFileRecordManager.Instance.AddFrame(e.Cloud, WeldParamManager.Instance.CurrentRobotX);
            UpdateProfileSnapshot(e.Cloud, e.MeasuredFps);
            if (DisplayEnabled) RenderDisplayFrame(e.Cloud);
        }

        /// <summary>把轮廓点写入后备快照并整体交换为前台快照</summary>
        /// <param name="cloud">本帧轮廓点云</param>
        /// <param name="measuredFps">实测帧率</param>
        private void UpdateProfileSnapshot(PointCloudData cloud, double measuredFps)
        {
            var pts = cloud.Points;
            int count = (pts == null) ? 0 : pts.Count;
            if (count > ProfileSnapshot.MaxPoints) count = ProfileSnapshot.MaxPoints;

            for (int i = 0; i < count; i++)
            {
                _ysBuffer[i] = pts[i].Y;
                _zsBuffer[i] = pts[i].Z;
            }
            _profileData.SetCount(count);

            _profileVersion++;
            _back.Fill(_ysBuffer, _zsBuffer, count, _lastResult.Valid,
                _lastResult.FeatureY, _lastResult.FeatureZ, measuredFps, _profileVersion);

            ProfileSnapshot swap = _front;
            _front = _back;
            _back = swap;
        }

        /// <summary>标定快照转算法 DTO，同引用只转一次</summary>
        /// <param name="calib">相机标定快照；null 表示无标定</param>
        /// <returns>算法层标定数据；calib 为 null 时返回 null</returns>
        private LineLaserCalibrationData ConvertCalibration(LineLaserCalibration calib)
        {
            if (calib == null) return null;
            if (ReferenceEquals(calib, _calibSource) && _calibData != null) return _calibData;

            _calibData = calib.HasData
                ? new LineLaserCalibrationData(calib.Center, calib.LeftTopCorner, calib.RightTopCorner,
                    calib.LeftBottomCorner, calib.RightBottomCorner)
                : new LineLaserCalibrationData();
            _calibSource = calib;
            return _calibData;
        }

        /// <summary>按显示帧率节流生成轮廓 Mat 并触发更新事件</summary>
        /// <param name="cloud">本帧轮廓点云；null = 空帧（清屏 + 画校准四边框）</param>
        private void RenderDisplayFrame(PointCloudData cloud)
        {
            // 节流：界面刷新不必跟随相机帧率，默认 15Hz
            if (DisplayFps > 0.0)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _lastDisplayTime).TotalMilliseconds < (1000.0 / DisplayFps)) return;
                _lastDisplayTime = now;
            }

            if (!ConvertMatEnabled) return;

            // 硬件数据在此转算法 DTO，单向依赖
            LineLaserCalibrationData calib = ConvertCalibration(_active?.Calibration);
            // 空帧传无效特征点，避免沿用上一帧
            LineLaserSeamFeature shown = (cloud == null)
                ? LineLaserSeamFeature.Invalid()
                : new LineLaserSeamFeature(_lastResult.Valid, _lastResult.FeatureY, _lastResult.FeatureZ);
            Mat frame = AlgorithmManager.Instance.LineLaserContour.Render(
                (cloud == null) ? null : _profileData, calib, shown);
            if (frame == null) return;

            // 克隆一帧，交由相机与订阅方共享
            Mat publish = frame.Clone();
            _active?.UpdateDisplayMat(publish);
            MatUpdated?.Invoke(this, new LineLaserMatEventArgs(ActiveName, publish));
        }

        #endregion

        #region 公共函数

        /// <summary>登记相机并订阅其结果与轮廓回调及状态变化</summary>
        /// <remarks>事件</remarks>
        /// <param name="name">相机键名</param>
        /// <param name="camera">相机对象</param>
        public void Register(string name, LineLaserCameraBase camera)
        {
            if (camera == null || string.IsNullOrEmpty(name)) return;

            LineLaserCameraBase old = Cameras.Get(name);
            if (old != null)
            {
                old.ResultReady -= OnCameraResultReady;
                old.ProfileReady -= OnCameraProfileReady;
                old.StateChanged -= OnCameraStateChanged;
            }

            camera.ResultReady += OnCameraResultReady;
            camera.ProfileReady += OnCameraProfileReady;
            camera.StateChanged += OnCameraStateChanged;
            Cameras.Register(name, camera);
        }

        /// <summary>撤销相机登记并退订事件</summary>
        /// <param name="name">相机键名</param>
        public void Unregister(string name)
        {
            LineLaserCameraBase camera = Cameras.Get(name);
            if (camera == null) return;

            camera.ResultReady -= OnCameraResultReady;
            camera.ProfileReady -= OnCameraProfileReady;
            camera.StateChanged -= OnCameraStateChanged;
            Cameras.Remove(name);

            if (ReferenceEquals(_intelligentInstance, camera)) _intelligentInstance = null;
            if (ReferenceEquals(_virtualInstance, camera)) _virtualInstance = null;
            if (ReferenceEquals(_active, camera)) _active = null;
        }

        /// <summary>取已登记的相机</summary>
        /// <param name="name">相机键名</param>
        /// <returns>相机对象，未找到返回 null</returns>
        public LineLaserCameraBase Get(string name)
        {
            return Cameras.Get(name);
        }

        /// <summary>选中英莱真实相机（默认）</summary>
        public void SelectIntelligentLaser()
        {
            ApplyConfig();
            SetActive(IntelligentLaser);
        }

        /// <summary>选中虚拟调试相机（模拟模式使用）</summary>
        public void SelectVirtual()
        {
            ApplyConfig();
            SetActive(VirtualCamera);
        }

        #region 线激光分析 Job（切换属调整的一种，唯一入口转发）

        /// <summary>读取当前线激光分析 Job 编号（未连接或读取失败返</summary>
        /// <remarks>回 -1）。</remarks>
        /// <returns>当前 Job 编号</returns>
        public int FetchCurrentJobId()
        {
            return IntelligentLaser.FetchCurrentJobId();
        }

        /// <summary>切换到指定线激光分析 Job，唯一入口</summary>
        /// <remarks>触发 <see cref="JobChanged"/> 供上层留痕。
        /// 界面与流程一律经本方法。</remarks>
        /// <param name="jobId">目标 Job 编号</param>
        /// <returns>厂商返回码（0 表示成功）</returns>
        public int SwitchJob(int jobId)
        {
            int ret = IntelligentLaser.SwitchJob(jobId);
            if (ret == 0)
            {
                JobChanged?.Invoke(this, jobId);
            }
            return ret;
        }

        #endregion

        /// <summary>按名称选择已登记相机</summary>
        /// <param name="name">相机键名</param>
        /// <returns>选中成功返回 true</returns>
        public bool Select(string name)
        {
            LineLaserCameraBase camera = Cameras.Get(name);
            if (camera == null) return false;

            SetActive(camera);
            return true;
        }

        /// <summary>连接当前相机，已连接时直接返回 true；另一台仍在采集时拒绝连接（单活约束）</summary>
        /// <returns>连接成功或无需连接返回 true</returns>
        public bool Connect()
        {
            LineLaserCameraBase camera = Active;
            if (camera == null) return false;

            LineLaserCameraBase busy = FindOtherRunningCamera(camera);
            if (busy != null)
            {
                Log("拒绝连接 " + camera.CameraName + "：检测到 " + busy.CameraName
                    + " 仍在采集（单活约束：一次只允许连接一种线激光或虚拟相机）", MessageLevel.Error);
                return false;
            }

            return camera.IsConnected || camera.ConnectManual(Config.SensorIp);
        }

        /// <summary>断开当前相机，未连接时不做任何动作</summary>
        public void Disconnect()
        {
            if (_active == null) return;
            if (_active.IsConnected) _active.DisconnectManual();
        }

        /// <summary>配置当前相机的默认回调周期，无需配置的相机</summary>
        /// <remarks>返回 true</remarks>
        /// <returns>无需配置或配置成功返回 true</returns>
        public bool ConfigureDefaultCommPeriod()
        {
            if (_active == null) return false;
            return _active.ConfigureDefaultCommPeriod();
        }

        /// <summary>当前相机传感器（出图）是否开启</summary>
        /// <returns>开启返回 true，无选中相机返回 false</returns>
        public bool IsCameraOn()
        {
            return (_active != null) && _active.IsCameraOn();
        }

        /// <summary>当前相机激光器是否开启</summary>
        /// <returns>开启返回 true，无选中相机返回 false</returns>
        public bool IsLaserOn()
        {
            return (_active != null) && _active.IsLaserOn();
        }

        /// <summary>开关当前相机传感器（出图）</summary>
        /// <param name="enable">true 为开启</param>
        public void SetSensor(bool enable)
        {
            _active?.SetSensor(enable);
        }

        /// <summary>开关当前相机激光器</summary>
        /// <param name="enable">true 为开启</param>
        public void SetLaser(bool enable)
        {
            _active?.SetLaser(enable);
        }

        /// <summary>关闭外设：停采集 → 关激光 → 关传感器。 用于停机、异常</summary>
        /// <remarks>收尾复位，异常吞掉，不影响状态机。</remarks>
        public void ShutdownPeripherals()
        {
            try
            {
                StopAcquisition();
            }
            catch
            {
                // 停止采集失败不阻塞后续关外设
            }

            if (_active == null) return;
            try { if (_active.IsLaserOn()) _active.SetLaser(false); } catch { }   // best-effort 关光：设备可能已离线，失败不阻断停机
            try { if (_active.IsCameraOn()) _active.SetSensor(false); } catch { }   // best-effort 关相机：同上
        }

        /// <summary>由工作流在采集开始/结束时调用，驱动「工作中」连</summary>
        /// <remarks>接态与跟踪段起止</remarks>
        /// <param name="working">true 为正在采集</param>
        public void SetWorking(bool working)
        {
            if (_working == working) return;
            _working = working;
        }

        /// <summary>设置显示处理开关，界面按需开启，默认关闭</summary>
        /// <param name="enabled">是否开启显示处理</param>
        /// <param name="fps">显示帧率（Hz），小于等于 0 表示不节流</param>
        /// <param name="convertMat">是否生成 Mat，关闭时只更新点缓冲</param>
        public void SetDisplayOptions(bool enabled, double fps, bool convertMat)
        {
            DisplayEnabled = enabled;
            DisplayFps = fps;
            ConvertMatEnabled = convertMat;
        }

        /// <summary>启动当前相机的连续采集</summary>
        public void StartAcquisition()
        {
            LineLaserCameraBase camera = Active;
            camera?.SensorRunContinue();
        }

        /// <summary>停止当前相机的连续采集</summary>
        public void StopAcquisition()
        {
            _active?.SensorStop();
        }

        /// <summary>重新加载配置并套用到已创建的相机</summary>
        public void LoadConfig()
        {
            Config.LoadConfig();
            ApplyConfig();
        }

        /// <summary>把当前配置写入 XML</summary>
        public void SaveConfig()
        {
            Config.SaveConfig();
        }

        /// <summary>释放全部相机并清空注册表</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (LineLaserCameraBase camera in Cameras.GetAll())
            {
                camera.ResultReady -= OnCameraResultReady;
                camera.ProfileReady -= OnCameraProfileReady;
                camera.StateChanged -= OnCameraStateChanged;
                camera.Dispose();
            }

            Cameras.Clear();
            _active = null;
            _intelligentInstance = null;
            _virtualInstance = null;
            Log("线激光管理器已释放");
        }

        #endregion
    }
}
