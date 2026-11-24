using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.WeldParamControl;
using AntdUI;
using Emgu.CV;
using ScottPlot;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
//using ScottPlot;

namespace AdaWeldSystem.UIDesign.ControlPage
{
    /// <summary>主页面</summary>
    public partial class MainPage : UserControl
    {
        #region 公共变量
        public delegate void LogAppendDelegate(Color messageColor, string text);
        #endregion

        #region 私有变量
        //实时获取的数据
        ChartVar SeamWidth;
        ChartVar robotSpeed;
        ChartVar LaserPower;
        ChartVar FeedSpeed;

        /// <summary>送丝Z轴位置曲线序列（主动控制量，归 WeldParamMontionControl）。</summary>
        ChartVar WireZ;

        /// <summary>单选互销抑制标志：为真时不响应勾选变更。</summary>
        private bool _suppressSeriesEvent;

        // 线激光轮廓：固定 1920 点，用 AddSignalXY 提速
        private const int ContourSignalMaxPoints = 1920;
        private double[] _contourSignalData;   // Y 值数组（高度 Z，mm），SignalXY 直接引用此数组
        private double[] _contourSignalXs;     // X 值数组（沿激光线物理坐标 Y，mm，非等距），SignalXY 直接引用
        private ScottPlot.Plottable.SignalPlotXY _contourSignalPlot;  // AddSignalXY 返回 SignalPlotXY，字段类型须与之匹配（此 ScottPlot 版本中二者非继承关系）
        private ScottPlot.Plottable.ScatterPlot _featurePointPlot;  // 特征点散点（红色实心圆）
        private double[] _featureXs = new double[1];  // 特征点 X 数组（复用，避免每帧分配）
        private double[] _featureYs = new double[1];  // 特征点 Y 数组（复用，避免每帧分配）

        // 轮廓轮询：独立线程约 30Hz 定节拍，与帧率解耦
            private const int ProfilePollIntervalMs = 33;
        private Thread _profilePollThread;
        private volatile bool _profilePolling;
        private long _lastProfileVersion;
        private volatile bool _contourHiddenByManual;

        // 工艺曲线轮询：同节拍，版本变化时增量拷点
        private long _lastParamVersion;
        private int _lastSeamWidthCount;
        private int _lastRobotSpeedCount;
        private int _lastFeedSpeedCount;
        private int _lastLaserPowerCount;
        private volatile bool _paramUiUpdatePending;
        private string _seamLimitLogState;

// ── 工艺参数实时曲线：五项互斥单选，无源项不画 ──
private const string NameSeamWidth = "接头宽";
        private const string NameRobotSpeed = "运动速度";
        private const string NameFeedSpeed = "送丝速度";
        private const string NameLaserPower = "激光功率";
        private const string NameWireZ = "送丝Z轴位置";
        private const string NameSeamWidthMax = "焊缝宽度上限";
        private const string NameSeamWidthMin = "焊缝宽度下限";

        private static readonly System.Drawing.Color ColorSeamWidth = System.Drawing.Color.FromArgb(24, 144, 255);
        private static readonly System.Drawing.Color ColorRobotSpeed = System.Drawing.Color.FromArgb(114, 46, 209);
        private static readonly System.Drawing.Color ColorFeedSpeed = System.Drawing.Color.FromArgb(82, 196, 26);
        private static readonly System.Drawing.Color ColorLaserPower = System.Drawing.Color.FromArgb(250, 173, 20);
        private static readonly System.Drawing.Color ColorWireZ = System.Drawing.Color.FromArgb(24, 160, 88);
        private static readonly System.Drawing.Color ColorSeamWidthLimit = System.Drawing.Color.FromArgb(255, 77, 79);

        private Mat _weldPreviewFrame;   // 焊接预览当前帧，本页拥有并释放

        // 轮廓图轴范围：优先用校准框，否则用默认值
        private double _contourYMin = -30.0;
        private double _contourYMax = 30.0;
        private double _contourZMin = -10.0;
        private double _contourZMax = 50.0;
        // 轴范围锁定：校准到达锁一次，重订阅复位；锁定后每帧仅更新轮廓与特征点
        private bool _contourAxisLocked = false;
        // 合并派发：队列最多 1 个待处理，防堆积卡死
        private volatile bool _uiUpdatePending = false;
        // 生产者快照：回调线程立即拷贝，避免缓冲回收
        private int _snapPointCount = 0;
        private bool _snapHasResult = false;
        private double _snapFeatureY = 0.0;
        private double _snapFeatureZ = 0.0;
        private bool _pendingAxisLock = false;

        // ── 焊接流程五步步骤条，当前步骤只前进可回退 ──
        private WeldFlowStep _weldFlowStep;
        private bool _weldFlowActive;                             // 本轮焊接流程是否已启动（整机 Running 后置位）
        private string[] _weldFlowStepTips;                       // 各步悬停提示（副标题+描述，与 steps1.Items 同序）
        private System.Windows.Forms.ToolTip _weldFlowToolTip;    // 步骤悬停提示（AntdUI Steps 无逐项 Tooltip 承载位）
        private int _weldFlowToolTipIndex;                        // 当前提示对应步骤（-1=无）

        // 硬件快捷开关 2×2 四区域，按钮与名称表同序
        private AntdUI.ButtonShadow[] _deviceButtons;
        private AntdUI.Alert[] _deviceAlerts;                    // 每区域设备名 Alert（等高，Success/Error 表示连接态）
        private AntdUI.TooltipComponent _tooltip;                // 设备名顶部提示（方位 Top）
        private string[] _deviceIconSvgs;                        // 各设备按钮图标 SVG（设备指向不同，可经 SetDeviceIconSvg 逐个替换）
        private bool[] _lastDeviceConnected;                      // 各设备连接态快照（与 _deviceSwitchNames 同长，惰性初始化），仅状态变化才重设配色
        /// <summary>模组开关名称表（索引与开关常量同步）。</summary>
private readonly string[] _deviceSwitchNames = { "线激光", "监控相机", "控制器", "机器人" };
        /// <summary>线激光开关索引（0）。</summary>
        private const int LineLaserSwitchIndex = 0;
        // 徽标配色：红=断开 / 绿=连接（Ant Design 语义色，BadgeBack 驱动 SVG 填色）
        private static readonly System.Drawing.Color DeviceBadgeRed = System.Drawing.Color.FromArgb(255, 77, 79);
        private static readonly System.Drawing.Color DeviceBadgeGreen = System.Drawing.Color.FromArgb(82, 196, 26);
        // 徽标 SVG（AntdUI 内置图标名）：连接=对勾 / 断开=叉号
        private const string ConnectedBadgeSvg = "CheckCircleFilled";
        private const string DisconnectedBadgeSvg = "CloseCircleFilled";
        // 按钮背景：断开=灰 / 连接=null（主题默认白）
        private static readonly System.Drawing.Color DeviceBackDisconnected = System.Drawing.Color.FromArgb(200, 200, 200);
        // 默认按钮图标（Ant Design poweroff 电源 SVG，与 buttonShadow1 demo 同源；可经 SetDeviceIconSvg 替换）
        private const string DefaultDeviceIconSvg = "ApiOutlined";

        // ── 整机状态标签与 500ms 常驻 UI 定时器 ──
        private System.Windows.Forms.Timer _uiTimer;
        private bool _blinkOn;                                  // 闪烁相位（true=亮红）
        private MainDeviceStatus _currentStatus;                // 当前整机态（闪烁 Tick 防越界改色）
        #endregion

        #region 构造函数
        public MainPage()
        {
            InitializeComponent();

            // 字段在构造函数初始化，禁止声明处赋值
            SeamWidth = new ChartVar();
            robotSpeed = new ChartVar();
            LaserPower = new ChartVar();
            FeedSpeed = new ChartVar();
            WireZ = new ChartVar();
            // 轮廓 SignalXY 数组初始化（原地引用，避免拷贝）
            _contourSignalData = new double[ContourSignalMaxPoints];
            _contourSignalXs = new double[ContourSignalMaxPoints];
            // 递增占位：Xs 须单调递增，首帧后由生产者覆盖
            for (int i = 0; i < ContourSignalMaxPoints; i++)
                _contourSignalXs[i] = i;
            GlobalCommData.EventInfoHandler += CommenData_EventInfoHandler;
            GlobalCommData.CommunicationCommandReceived += OnRobotCommandReceived;

            // 焊接过程预览：帧由工作流派发（调试页直连相机）
            MonitorCameraWorkflow.Instance.WeldPreviewFrame += OnWeldPreviewFrame;

            // 硬件快捷开关：常驻悬浮面板，随宿主自动显隐
            InitDeviceSwitchButtons();

            // 五项曲线互斥单选：订阅放在 InitializeComponent 之后
            chkSeamWidth.CheckedChanged += ChkSeries_CheckedChanged;
            chkRobotSpeed.CheckedChanged += ChkSeries_CheckedChanged;
            chkFeedSpeed.CheckedChanged += ChkSeries_CheckedChanged;
            chkLaserPower.CheckedChanged += ChkSeries_CheckedChanged;
            chkWireZ.CheckedChanged += ChkSeries_CheckedChanged;
            // 单选互斥：仅保留第一个默认勾选，其余取消
            ApplySeriesSingleSelection(chkSeamWidth);

            // 焊接流程状态机：联动机器人指令与整机状态
            InitWeldFlowSteps();
            steps1.MouseMove += OnWeldFlowStepsMouseMove;
            steps1.MouseLeave += OnWeldFlowStepsMouseLeave;
            WeldParamControlWorkflow.Instance.WeldStatusChanged += OnWeldParamWeldStatusChanged;

            // 页面销毁时释放外部资源（UI 类用 DisposeComponents）
            this.HandleDestroyed += MainPage_HandleDestroyed;

            // UI 统一定时器：500ms 常驻（EStop 红闪），替代原分离的闪烁表与 Dash 表
            _uiTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
            DeviceControlWork.Instance.StatusChanged += OnMainDeviceStatusChanged;
            ApplyDeviceStatus(DeviceControlWork.Instance.Status);

            // 工艺曲线由轮询线程取缓存，不订阅事件
        }
        #endregion

        #region 日志封装

        /// <summary>日志来源标识（本页统一出口，便于按来源筛选）。</summary>
        private const string LogTag = "MainPage";

        /// <summary>本页日志统一出口，转发 GlobalCommData.ShowLog。</summary>
        /// <param name="message">日志正文（纯文本，无符号）</param>
        /// <param name="level">日志级别，默认 Info</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(LogTag, message, level);
        }

        #endregion

        #region 私有函数
        /// <summary>焊接模式是否生效：整机 Running，含模拟测试。</summary>
        private bool IsWeldModeActive
        {
            get { return _currentStatus == MainDeviceStatus.Running; }
        }

        private void MainPage_HandleDestroyed(object sender, EventArgs e)
        {
            _weldFlowToolTip?.Dispose();
            _weldFlowToolTip = null;
            DisposeComponents();
        }

        /// <summary>手动模式隐藏主页轮廓，切到线激光调试页显示。</summary>
        private void HideContourForManual()
        {
            if (_contourHiddenByManual) return;
            _contourHiddenByManual = true;
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (formsPlot1.InvokeRequired)
            {
                try { formsPlot1.BeginInvoke(new Action(HideContourOnUiThread)); }
                catch { }
                return;
            }
            HideContourOnUiThread();
        }

        /// <summary>UI 线程隐藏轮廓线与特征点。</summary>
        private void HideContourOnUiThread()
        {
            if (IsDisposed || Disposing) return;
            if (_contourSignalPlot != null) _contourSignalPlot.IsVisible = false;
            if (_featurePointPlot != null) _featurePointPlot.IsVisible = false;
            _lastProfileVersion = 0;
            try { formsPlot1.Refresh(); } catch { }
        }

        /// <summary>启动轮廓轮询线程。</summary>
        private void StartProfilePolling()
        {
            // 锁定复位：重启轮询后下一帧用新校准框
            _contourAxisLocked = false;
            // 轮廓线置不可见：无有效数据时不留旧轮廓
            if (_contourSignalPlot != null) _contourSignalPlot.IsVisible = false;

            if (_profilePolling) return;
            _profilePolling = true;
            _lastProfileVersion = 0;

            _profilePollThread = new Thread(ProfilePollLoop)
            {
                IsBackground = true,
                Name = "轮廓轮询"
            };
            _profilePollThread.Start();
        }

        /// <summary>停止轮廓轮询线程。</summary>
        private void StopProfilePolling()
        {
            _profilePolling = false;
            Thread thread = _profilePollThread;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(300);
            }
            _profilePollThread = null;
        }

        /// <summary>按固定节拍取线激光最新快照，版本未变则跳过。</summary>
        private void ProfilePollLoop()
        {
            while (_profilePolling)
            {
                try
                {
                    // 轮廓只在焊接自动流程或模拟流程显示；手动调试由线激光调试页承载
                    // 模拟启动时线激光流程态被置 Manual，故不能用 RunMode 判据（会误挡模拟）
                    DeviceControlWork dc = DeviceControlWork.Instance;
                    bool contourAllowed = dc.IsAutoWeldingRunning || dc.IsSimulationEnabled;
                    if (contourAllowed)
                    {
                        _contourHiddenByManual = false;
                        ProfileSnapshot snap = LineLaserManager.Instance.Profile;
                        if (snap != null && snap.Version != _lastProfileVersion)
                        {
                            _lastProfileVersion = snap.Version;
                            ApplyProfileSnapshot(snap);
                        }
                    }
                    else if (!_contourHiddenByManual)
                    {
                        HideContourForManual();
                    }
                }
                catch
                {
                    // 轮询异常不影响页面其它功能
                }

                // 工艺曲线轮询：版本变化时增量拷点并刷新
                try
                {
                    WeldParamManager mgr = WeldParamManager.Instance;
                    long version = mgr.AdjustmentOutputVersion;
                    if (version != _lastParamVersion)
                    {
                        _lastParamVersion = version;
                        AppendNewPoints(mgr.SeamWidthChart, SeamWidth, ref _lastSeamWidthCount);
                        AppendNewPoints(mgr.RobotSpeedChart, robotSpeed, ref _lastRobotSpeedCount);
                        AppendNewPoints(mgr.FeedSpeedChart, FeedSpeed, ref _lastFeedSpeedCount);
                        AppendNewPoints(mgr.LaserPowerChart, LaserPower, ref _lastLaserPowerCount);

                        // 合并派发：队列最多 1 个待处理，防堆积
                        if (!_paramUiUpdatePending)
                        {
                            _paramUiUpdatePending = true;
                            if (formsPlot2.InvokeRequired)
                                formsPlot2.BeginInvoke(new Action(RefreshParamChartsUi));
                            else
                                RefreshParamChartsUi();
                        }
                    }
                }
                catch
                {
                    // 并发追加可能读到越界索引：下轮轮询自愈
                }

                Thread.Sleep(ProfilePollIntervalMs);
            }
        }

        /// <summary>增量拷贝新点到显示序列（只显示不加工）。</summary>

        /// <param name="source">算法侧原始点缓存（只读消费）</param>
        /// <param name="target">显示序列</param>
        /// <param name="lastIndex">上次已消费的点数（调用方持有，用于增量拷贝）</param>
        private void AppendNewPoints(ChartVar source, ChartVar target, ref int lastIndex)
        {
            if (source == null) return;

            bool hasNew = source.TryCopyRange(lastIndex, out int count, out double[] xs, out double[] ys);

            if (count < lastIndex)
            {
                // 算法侧已复位：清空显示序列并重新跟随
                lastIndex = 0;
                target.Clear();
                return;
            }

            if (!hasNew) return;

            for (int i = 0; i < xs.Length; i++)
            {
                target.Add(xs[i], ys[i]);
            }
            lastIndex = count;
        }

        /// <summary>把快照拷入 ScottPlot 复用数组并派发一次界面刷新。</summary>
        /// <param name="snap">轮廓帧快照</param>
        private void ApplyProfileSnapshot(ProfileSnapshot snap)
        {
            int pointCount = Math.Min(snap.Count, ContourSignalMaxPoints);
            if (pointCount <= 0) return;

            // SignalXY 要求 X 严格升序；新相机局部 Y 回退（如 14.51→14.47）会令 Refresh 抛异常。
            // 剔除回退点：只在 X 递增时写入对应 Z，保住 X↔Z 一一对应；显示点数取实际写入数。
            int written = 0;
            double lastX = double.NegativeInfinity;
            for (int i = 0; i < pointCount; i++)
            {
                double x = snap.Ys[i];
                if (x <= lastX) continue;
                _contourSignalXs[written] = x;      // X = 沿激光线物理坐标 Y（非等距，AddSignalXY 显式传入）
                _contourSignalData[written] = snap.Zs[i];
                lastX = x;
                written++;
            }
            if (written <= 0) return;
            _snapPointCount = written;

            _snapHasResult = snap.HasResult;
            if (snap.HasResult)
            {
                _snapFeatureY = snap.FeatureY;
                _snapFeatureZ = snap.FeatureZ;
            }

            LockContourAxisOnce();

            // 合并派发：队列最多 1 个待处理，防堆积卡死
            if (_uiUpdatePending) return;
            _uiUpdatePending = true;
            if (formsPlot1.InvokeRequired)
                formsPlot1.BeginInvoke(new Action(UpdateChartUI));
            else
                UpdateChartUI();
        }

        /// <summary>首帧按校准框四角点锁定轴范围（仅一次）。</summary>
        private void LockContourAxisOnce()
        {
            if (_contourAxisLocked) return;

            LineLaserCameraBase camera = LineLaserManager.Instance.Active;
            if (camera == null) return;

            LineLaserCalibration calib = camera.Calibration;
            if (calib == null || !calib.HasData) return;

            double yMin = Math.Min(
                Math.Min(calib.LeftTopCorner[0], calib.RightTopCorner[0]),
                Math.Min(calib.LeftBottomCorner[0], calib.RightBottomCorner[0]));
            double yMax = Math.Max(
                Math.Max(calib.LeftTopCorner[0], calib.RightTopCorner[0]),
                Math.Max(calib.LeftBottomCorner[0], calib.RightBottomCorner[0]));
            double zMin = Math.Min(
                Math.Min(calib.LeftTopCorner[1], calib.RightTopCorner[1]),
                Math.Min(calib.LeftBottomCorner[1], calib.RightBottomCorner[1]));
            double zMax = Math.Max(
                Math.Max(calib.LeftTopCorner[1], calib.RightTopCorner[1]),
                Math.Max(calib.LeftBottomCorner[1], calib.RightBottomCorner[1]));

            // 有效性检查 + 5% 边距，避免贴边或零范围
            double yRange = yMax - yMin;
            double zRange = zMax - zMin;
            if (yRange <= 0.001 || zRange <= 0.001) return;

            double yPad = yRange * 0.05;
            double zPad = zRange * 0.05;
            _contourYMin = yMin - yPad;
            _contourYMax = yMax + yPad;
            _contourZMin = zMin - zPad;
            _contourZMax = zMax + zPad;
            _contourAxisLocked = true;
            _pendingAxisLock = true;
        }

        /// <summary>UI 线程应用轴范围锁定并刷新图表（不重算数据）。</summary>
        private void UpdateChartUI()
        {
            try
            {
                if (_contourSignalPlot == null) return;
                // 首帧有效数据到达：轮廓线置为可见
                _contourSignalPlot.IsVisible = true;

                if (_pendingAxisLock)
                {
                    formsPlot1.Plot.SetAxisLimits(_contourYMin, _contourYMax, _contourZMin, _contourZMax);
                    _pendingAxisLock = false;
                }

                // 有效点范围：只渲染前 pointCount 个点
                _contourSignalPlot.MinRenderIndex = 0;
                _contourSignalPlot.MaxRenderIndex = _snapPointCount - 1;

                if (_snapHasResult)
                {
                    _featureXs[0] = _snapFeatureY;
                    _featureYs[0] = _snapFeatureZ;
                    _featurePointPlot.IsVisible = true;
                }
                else
                {
                    _featurePointPlot.IsVisible = false;
                }

                formsPlot1.Refresh();
            }
            finally
            {
                _uiUpdatePending = false;
            }
        }

        /// <summary>UI 线程重绑四条工艺曲线并刷新。</summary>
        private void RefreshParamChartsUi()
        {
            try
            {
                RefreshParamCharts();
            }
            finally
            {
                _paramUiUpdatePending = false;
            }
        }

        /// <summary>重绑工艺参数实时曲线（同图只画勾选项）。</summary>
        private void RefreshParamCharts()
        {
            // 页面销毁后不再重建（排队回调可能仍在）
            if (IsDisposed) return;

            formsPlot2.Plot.Clear();
            // 标题与轴标签随 Clear 被清，须重建后重设
            formsPlot2.Configuration.DpiStretch = true;
            formsPlot2.Plot.Title("");
            formsPlot2.Plot.XLabel("机器人 X 轴");
            formsPlot2.Plot.YLabel("参数值");
            if (chkSeamWidth.Checked)
            {
                AddSeries(formsPlot2, NameSeamWidth, SeamWidth, ColorSeamWidth);
                // 焊缝宽度上下限参考线（随「接头宽」显示）
                AddSeamWidthLimitLines(formsPlot2);
            }
            if (chkRobotSpeed.Checked) AddSeries(formsPlot2, NameRobotSpeed, robotSpeed, ColorRobotSpeed);
            if (chkFeedSpeed.Checked) AddSeries(formsPlot2, NameFeedSpeed, FeedSpeed, ColorFeedSpeed);
            if (chkLaserPower.Checked) AddSeries(formsPlot2, NameLaserPower, LaserPower, ColorLaserPower);
            // 送丝Z轴：后端就绪前无数据源，空序列跳过
            if (chkWireZ.Checked) AddSeries(formsPlot2, NameWireZ, WireZ, ColorWireZ);
            formsPlot2.Plot.AxisAuto();
            formsPlot2.Plot.Legend();
            formsPlot2.Refresh();

        }


        /// <summary>把坐标序列以折线加入图表（线色按系列区分）。</summary>
        /// <param name="chart">目标图表</param>
        /// <param name="seriesName">系列名称（图例文本）</param>
        /// <param name="data">坐标序列</param>
        /// <param name="color">折线颜色</param>
        private void AddSeries(ScottPlot.FormsPlot chart, string seriesName, ChartVar data, System.Drawing.Color color)
        {
            if (data == null) return;

            // 配对快照：同锁取 X/Y 保证等长，避免抛异常
            if (data.TrySnapshot(out double[] xs, out double[] ys))
            {
                chart.Plot.AddScatter(xs, ys, color, 2, markerSize: 0, label: seriesName);
            }
        }

        /// <summary>把焊缝宽度上下限画成水平参考线（红色虚线）。</summary>

        /// <param name="chart">目标图表</param>
        private void AddSeamWidthLimitLines(ScottPlot.FormsPlot chart)
        {
            AutoParam param = WeldParamManager.Instance.CurrentAutoParam;

            string state;
            string limitsText;
            if (!IsWeldModeActive)
            {
                // 焊接未开启：不画，避免图例重复条目
                state = "skip-not-welding";
                limitsText = string.Empty;
            }
            else if (param == null)
            {
                state = "skip-null";
                limitsText = string.Empty;
            }
            else
            {
                limitsText = string.Format("下限 {0:F3} 上限 {1:F3}mm", param.SeamWidthMin, param.SeamWidthMax);
                if (param.SeamWidthMax <= param.SeamWidthMin)
                {
                    state = "skip-invalid";
                }
                else
                {
                    state = "draw:" + limitsText;
                    chart.Plot.AddHorizontalLine(param.SeamWidthMax, ColorSeamWidthLimit, 1, ScottPlot.LineStyle.Dash, NameSeamWidthMax);
                    chart.Plot.AddHorizontalLine(param.SeamWidthMin, ColorSeamWidthLimit, 1, ScottPlot.LineStyle.Dash, NameSeamWidthMin);
                }
            }

            // 变更留痕：状态变化才记一条（画值/原因）
            if (state != _seamLimitLogState)
            {
                _seamLimitLogState = state;
                if (state.StartsWith("draw:"))
                {
                    Log("接头宽参考线 已画焊缝宽度上下限 " + limitsText);
                }
                else if (state == "skip-null")
                {
                    Log("接头宽参考线 未画 自适应参数为空", MessageLevel.Warning);
                }
                else
                {
                    Log("接头宽参考线 未画 焊缝宽度上下限未配置或无效 " + limitsText, MessageLevel.Warning);
                }
            }
        }

        void AfterInitializeUI()
        {
            SetupMergedChart(formsPlot2);
            // 显示后按最终尺寸核算 2×2 设备区域
            LayoutDeviceSwitchAreas();
        }

        /// <summary>初始化四合一工艺参数图（多系列共用坐标轴）。</summary>

        /// <param name="chart">目标图表</param>
        private void SetupMergedChart(ScottPlot.FormsPlot chart)
        {
            // 仅兜底设一次标题/轴（空白期显示用）；图例不建（由刷新路径统一创建）
            chart.Configuration.DpiStretch = false;
            chart.Plot.XLabel("机器人 X 轴");
            chart.Plot.YLabel("参数值");
            chart.Refresh();
        }

        private void CommenData_EventInfoHandler(object sender, MessageArgs e)
        {
            // 未生成或已销毁时不访问控件（句柄可能未到）
            if (IsDisposed || Disposing || !IsHandleCreated || inputLog == null)
            {
                return;
            }
            // 后台线程事件：判 InvokeRequired 后异步封送
            if (inputLog.InvokeRequired)
            {
                LogAppendDelegate la = new LogAppendDelegate(LogAppend);
                inputLog.BeginInvoke(la, e.MessageShowColor, e.strMessage);
            }
            else
            {
                LogAppend(e.MessageShowColor, e.strMessage);
            }
        }

        private void inputLog_TextChanged(object sender, EventArgs e)
        {
            inputLog.ScrollToCaret();
        }

        private void MainPage_Shown(object sender, EventArgs e)
        {
            AfterInitializeUI();
        }
        #endregion

        #region 整机状态标签（LabelStatus）
        /// <summary>构造报警/急停文本：前缀加首个具体原因。</summary>

        /// <param name="prefix">状态前缀（报警 / 急停）。</param>
        /// <returns>可直接显示的报警文本。</returns>
        private string BuildAlarmText(string prefix)
        {
            // 温控报警（焊接头从站）
            try
            {
                if (LaserWeldHeadWorkflow.Instance.IsTemperatureAlarm)
                {
                    return prefix + "：保护镜温度超限";
                }

                // 焊接头从站报警位（驱动/气压/温升）
                AdaWeldSystem.MotionControl.HeadAlarmStatus headAlarm =
                    LaserWeldHeadWorkflow.Instance.LastAlarmStatus;
                if (headAlarm != null && headAlarm.HasAlarm)
                {
                    // 文本由硬件层唯一产出；此处仅按 Label 宽度截取首个原因
                    string reason = headAlarm.Describe();
                    int sep = reason.IndexOf(' ');
                    return prefix + "：" + (sep > 0 ? reason.Substring(0, sep) : reason);
                }
            }
            catch (Exception ex)
            {
                // 报警异常降级兜底并记日志
                Log("读取报警信息失败 " + ex.Message, MessageLevel.Warning);
            }

            return prefix + "：请检查设备";
        }

        /// <summary>主设备运行态变更回调，封送到 UI 线程。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnMainDeviceStatusChanged(object sender, MainDeviceStatusChangedEventArgs e)
        {
            // 事件可能来自后台线程：封送到 UI 线程
            if (LabelStatus.InvokeRequired)
            {
                LabelStatus.BeginInvoke(new Action(() => ApplyDeviceStatus(e.NewStatus)));
                return;
            }
            ApplyDeviceStatus(e.NewStatus);
        }

        /// <summary>应用整机状态：报警文本 + 背景色映射。</summary>

        /// <param name="state">整机运行态</param>
        private void ApplyDeviceStatus(MainDeviceStatus state)
        {
            MainDeviceStatus previous = _currentStatus;
            _currentStatus = state;
            string text;
            System.Drawing.Color back;
            System.Drawing.Color fore;
            switch (state)
            {
                case MainDeviceStatus.Running:
                    text = "运行中";
                    back = System.Drawing.Color.Green;
                    fore = System.Drawing.Color.White;
                    break;
                case MainDeviceStatus.Reseting:
                    text = "复位中";
                    back = System.Drawing.Color.Yellow;
                    fore = System.Drawing.Color.Black;
                    break;
                case MainDeviceStatus.Stop:
                    text = "已停止";
                    back = System.Drawing.Color.Blue;
                    fore = System.Drawing.Color.White;
                    break;
                case MainDeviceStatus.Alarm:
                    //报警档：拼接模组上报的具体报警原因；无上报时给可读兜底
                    text = BuildAlarmText("报警");
                    back = System.Drawing.Color.Orange;
                    fore = System.Drawing.Color.Black;
                    break;
                case MainDeviceStatus.EStop:
                    // 亮相位固定红底；暗相位（白）由闪烁定时器翻转
                    text = BuildAlarmText("急停");
                    back = System.Drawing.Color.Red;
                    fore = System.Drawing.Color.White;
                    break;
                case MainDeviceStatus.NoReset:
                    text = "未复位";
                    back = System.Drawing.Color.Gray;
                    fore = System.Drawing.Color.White;
                    break;
                default:
                    // 未知态不直出枚举名：给可读文案并记日志
                    Log(string.Format("整机状态落入未知分支当前值 {0}", state));
                    text = "状态未知";
                    back = System.Drawing.Color.Gray;
                    fore = System.Drawing.Color.White;
                    break;
            }
            LabelStatus.Text = text;
            LabelStatus.BackColor = back;
            LabelStatus.ForeColor = fore;

            // _uiTimer 常驻不启停；EStop 相位复位（闪烁在 UiTimer_Tick 内按状态判断）
            if (state == MainDeviceStatus.EStop)
            {
                _blinkOn = true;
            }

            // 步骤条随整机状态联动：推进/收口/回退
            ApplyWeldFlowDeviceStatus(previous, state);
        }

        /// <summary>UI 统一定时器（500ms）：EStop 态翻转红闪相位。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void UiTimer_Tick(object sender, EventArgs e)
        {
            // EStop 红闪（非 EStop 态不改色，保持 ApplyDeviceStatus 设置的固定底色）
            if (_currentStatus == MainDeviceStatus.EStop)
            {
                _blinkOn = !_blinkOn;
                LabelStatus.BackColor = _blinkOn ? System.Drawing.Color.Red : System.Drawing.Color.White;
            }
        }

        /// <summary>机器人指令接收处理。</summary>

        /// <param name="sender">发送方。</param>
        /// <param name="message">消息内容。</param>
        private void OnRobotCommandReceived(object sender, string message)
        {
            if (!this.IsHandleCreated) return;
            this.BeginInvoke(new Action(() => AdvanceWeldFlowByCommand(message)));
        }
        #endregion

        #region 焊接流程步骤条（steps1，安全位→焊前检查→起焊→焊接→结束）
        /// <summary>初始化步骤条并停在安全位。</summary>
        private void InitWeldFlowSteps()
        {
            steps1.Items.Clear();
            _weldFlowStepTips = new string[WeldFlowSteps.Count];
            for (int i = 0; i < WeldFlowSteps.Count; i++)
            {
                WeldFlowStepInfo info = WeldFlowSteps.All[i];
                // 横排只放标题，副标题/描述移入悬停提示
                steps1.Items.Add(new AntdUI.StepsItem
                {
                    ID = info.Step.ToString(),
                    Title = info.Title,
                });
                _weldFlowStepTips[i] = info.SubTitle + "\n" + info.Description;
            }
            _weldFlowToolTipIndex = -1;
            if (_weldFlowToolTip == null)
            {
                _weldFlowToolTip = new System.Windows.Forms.ToolTip
                {
                    InitialDelay = 0,
                    ReshowDelay = 0,
                    AutoPopDelay = 20000,
                    ShowAlways = true,
                };
            }
            _weldFlowActive = false;
            _weldFlowStep = WeldFlowStep.SafePose;
            steps1.Current = WeldFlowSteps.IndexOf(WeldFlowStep.SafePose);
            steps1.Status = AntdUI.TStepState.Wait;
        }

        /// <summary>读取 AntdUI 为每个步骤项算出的矩形（StepsItem.rect，内部属性）。</summary>
        private static readonly System.Reflection.PropertyInfo WeldFlowStepRectProperty =
            typeof(AntdUI.StepsItem).GetProperty(
                "rect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>步骤条悬停：切换到鼠标所在步骤的提示。</summary>
        private void OnWeldFlowStepsMouseMove(object sender, MouseEventArgs e)
        {
            int index = -1;
            Rectangle rect = Rectangle.Empty;
            if (WeldFlowStepRectProperty != null && _weldFlowStepTips != null)
            {
                for (int i = 0; i < steps1.Items.Count && i < _weldFlowStepTips.Length; i++)
                {
                    Rectangle itemRect = (Rectangle)WeldFlowStepRectProperty.GetValue(steps1.Items[i], null);
                    if (!itemRect.IsEmpty && itemRect.Contains(e.Location))
                    {
                        index = i;
                        rect = itemRect;
                        break;
                    }
                }
            }
            if (index == _weldFlowToolTipIndex) return;
            _weldFlowToolTipIndex = index;
            if (_weldFlowToolTip == null) return;
            _weldFlowToolTip.Hide(steps1);
            if (index < 0) return;
            _weldFlowToolTip.Show(_weldFlowStepTips[index], steps1, rect.Left, rect.Bottom + 2, 20000);
        }

        /// <summary>移出步骤条：隐藏悬停提示。</summary>
        private void OnWeldFlowStepsMouseLeave(object sender, EventArgs e)
        {
            _weldFlowToolTipIndex = -1;
            _weldFlowToolTip?.Hide(steps1);
        }

        /// <summary>设置当前步骤与状态（默认只前进）。</summary>

        /// <param name="step">目标步骤</param>
        /// <param name="state">步骤状态（Process/Finish/Error）</param>
        /// <param name="allowBack">是否允许回退（复位/重新开始用）</param>
        private void SetWeldFlowStep(WeldFlowStep step, AntdUI.TStepState state, bool allowBack)
        {
            int next = WeldFlowSteps.IndexOf(step);
            int current = WeldFlowSteps.IndexOf(_weldFlowStep);
            if (!allowBack && next < current) return;
            bool changed = next != current || steps1.Status != state;
            _weldFlowStep = step;
            steps1.Current = next;
            steps1.Status = state;
            if (changed)
            {
                WeldFlowStepInfo info = WeldFlowSteps.Get(step);
                Log("焊接流程步骤 " + (next + 1) + "/" + WeldFlowSteps.Count + " " + info.Title + "（" + info.SubTitle + "）" + info.Description);
            }
        }

        /// <summary>整机状态联动步骤条。</summary>

        /// <param name="previous">切换前整机态</param>
        /// <param name="state">切换后整机态</param>
        private void ApplyWeldFlowDeviceStatus(MainDeviceStatus previous, MainDeviceStatus state)
        {
            if (steps1 == null) return;
            switch (state)
            {
                case MainDeviceStatus.Running:
                    _weldFlowActive = true;
                    if (_weldFlowStep == WeldFlowStep.SafePose)
                        SetWeldFlowStep(WeldFlowStep.PreWeldCheck, AntdUI.TStepState.Process, false);
                    break;
                case MainDeviceStatus.Alarm:
                case MainDeviceStatus.EStop:
                    // 当前步骤标红，保留进度便于判断中断步
                    steps1.Status = AntdUI.TStepState.Error;
                    break;
                case MainDeviceStatus.Reseting:
                case MainDeviceStatus.NoReset:
                    _weldFlowActive = false;
                    // 步骤条复位到安全位（新一轮开始前）
                    _weldFlowStep = WeldFlowStep.SafePose;
                    steps1.Current = WeldFlowSteps.IndexOf(WeldFlowStep.SafePose);
                    steps1.Status = AntdUI.TStepState.Wait;
                    break;
                case MainDeviceStatus.Stop:
                    if (previous == MainDeviceStatus.Running && _weldFlowActive)
                    {
                        SetWeldFlowStep(WeldFlowStep.WeldEnd, AntdUI.TStepState.Finish, false);
                        _weldFlowActive = false;
                    }
                    break;
            }
        }

        /// <summary>焊接过程态变更回调，封送到 UI 线程。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnWeldParamWeldStatusChanged(object sender, WeldStatusChangedEventArgs e)
        {
            if (steps1.InvokeRequired)
            {
                steps1.BeginInvoke(new Action<SubDeviceWeldStatus>(ApplyWeldFlowWeldStatus), e.NewStatus);
                return;
            }
            ApplyWeldFlowWeldStatus(e.NewStatus);
        }

        /// <summary>模组焊接过程态 → 步骤条联动（机器人指令为主，</summary>

        /// <param name="status">焊接过程态</param>
        private void ApplyWeldFlowWeldStatus(SubDeviceWeldStatus status)
        {
            switch (status)
            {
                case SubDeviceWeldStatus.PreWork:
                    if (_weldFlowStep == WeldFlowStep.SafePose)
                        SetWeldFlowStep(WeldFlowStep.PreWeldCheck, AntdUI.TStepState.Process, false);
                    break;
                case SubDeviceWeldStatus.Working:
                    SetWeldFlowStep(WeldFlowStep.Welding, AntdUI.TStepState.Process, false);
                    break;
                case SubDeviceWeldStatus.Stopping:
                case SubDeviceWeldStatus.ManualStopped:
                    SetWeldFlowStep(WeldFlowStep.WeldEnd, AntdUI.TStepState.Process, false);
                    break;
                case SubDeviceWeldStatus.ErrorAborted:
                    SetWeldFlowStep(WeldFlowStep.WeldEnd, AntdUI.TStepState.Error, false);
                    break;
            }
        }

        /// <summary>机器人指令 → 步骤条推进（与 CommandCore 指令常量对齐）。</summary>
        /// <param name="message">通讯层收到的原始报文</param>
        private void AdvanceWeldFlowByCommand(string message)
        {
            string normalized = CommandCore.Normalize(message);
            if (!CommandCore.TrySplitPayload(normalized, out string cmd, out _)) return;

            switch (cmd)
            {
                case CommandCore.RxHomePose:
                case CommandCore.RxStartSafePose:
                    SetWeldFlowStep(WeldFlowStep.SafePose, AntdUI.TStepState.Process, true);
                    break;
                case CommandCore.RxPreWeldRequest:
                case CommandCore.RxPreWeldPose:
                    SetWeldFlowStep(WeldFlowStep.PreWeldCheck, AntdUI.TStepState.Process, false);
                    break;
                case CommandCore.RxWeldStartPose:
                    SetWeldFlowStep(WeldFlowStep.ArcStart, AntdUI.TStepState.Process, false);
                    break;
                case CommandCore.RxPreStopPose:
                case CommandCore.RxWeldStopPose:
                    SetWeldFlowStep(WeldFlowStep.WeldEnd, AntdUI.TStepState.Process, false);
                    break;
                case CommandCore.RxStopSafePose:
                    SetWeldFlowStep(WeldFlowStep.WeldEnd, AntdUI.TStepState.Finish, false);
                    break;
            }
        }
        #endregion

        #region 硬件快捷开关（AntdUI ButtonShadow）
        /// <summary>初始化硬件快捷开关（常驻悬浮面板）：2×2 区域。</summary>
        private void InitDeviceSwitchButtons()
        {
            // Tooltip 保留：用户取消的是 ShowTips 按钮
            _tooltip = new AntdUI.TooltipComponent
            {
                Font = new System.Drawing.Font("Microsoft YaHei UI", 10F),
                ArrowAlign = AntdUI.TAlign.Top,
            };
            int count = _deviceSwitchNames.Length;
            _deviceButtons = new AntdUI.ButtonShadow[count];
            _deviceAlerts = new AntdUI.Alert[count];
            _deviceIconSvgs = new string[count];

            for (int i = 0; i < count; i++)
            {
                _deviceIconSvgs[i] = DefaultDeviceIconSvg;
                var btn = new AntdUI.ButtonShadow
                {
                    Name = "btnDev_" + i,
                    BorderWidth = 1F,
                    IconRatio = 1.2F,
                    IconSvg = _deviceIconSvgs[i],
                    ShadowColor = System.Drawing.Color.Black,
                    DefaultBack = DeviceBackDisconnected,
                    BadgeSvg = DisconnectedBadgeSvg,
                    BadgeAlign = AntdUI.TAlign.TR,
                    BadgeBack = DeviceBadgeRed,
                    BadgeSize = 1.0F,
                };
                int idx = i;
                btn.Click += (s, e) => OnDeviceSwitchClick(idx);
                _tooltip.SetTip(btn, BuildDeviceTooltipText(i, false));
                _deviceButtons[i] = btn;

                // 每区域标签：Alert 显示设备名与连接态
                var alert = new AntdUI.Alert
                {
                    Name = "alertDev_" + i,
                    Text = _deviceSwitchNames[i],
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
                    Icon = AntdUI.TType.Error,
                    TextColor = System.Drawing.Color.FromArgb(192, 0, 0),
                };
                _deviceAlerts[i] = alert;

                panelfloatbutton.Controls.Add(btn);
                panelfloatbutton.Controls.Add(alert);
            }

            // 2×2 布局按面板实际显示尺寸核算；面板尺寸变化（宿主重排）时重排
            panelfloatbutton.SizeChanged += OnPanelFloatButtonSizeChanged;
            LayoutDeviceSwitchAreas();
        }

        /// <summary>panelfloatbutton 尺寸变化时重排 2×2 设备区域。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnPanelFloatButtonSizeChanged(object sender, EventArgs e)
        {
            LayoutDeviceSwitchAreas();
        }

        /// <summary>按 panelfloatbutton 实际客户区核算 2×2：按钮与 Alert 等高、区内间隔 1px、区间间</summary>
        private void LayoutDeviceSwitchAreas()
        {
            if (_deviceButtons == null || _deviceAlerts == null) return;
            int width = panelfloatbutton.ClientSize.Width;
            int height = panelfloatbutton.ClientSize.Height;
            if (width <= 0 || height <= 0) return;

            const int outer = 4;      // 面板外边距
            const int areaGap = 8;    // 区域间隔（明显大于区内 1px）
            const int innerGap = 1;   // 按钮与 Alert 间隔
            int cellWidth = (width - outer * 2 - areaGap) / 2;
            int cellHeight = (height - outer * 2 - areaGap) / 2;
            int btnSide = cellHeight;                       // 按钮高度 = Alert 高度
            int alertWidth = cellWidth - btnSide - innerGap;
            if (btnSide < 16 || alertWidth < 10) return;    // 面板过小不排，等下一次尺寸变化

            for (int i = 0; i < _deviceButtons.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int x = outer + col * (cellWidth + areaGap);
                int y = outer + row * (cellHeight + areaGap);
                _deviceButtons[i].Location = new System.Drawing.Point(x, y);
                _deviceButtons[i].Size = new System.Drawing.Size(btnSide, btnSide);
                _deviceAlerts[i].Location = new System.Drawing.Point(x + btnSide + innerGap, y);
                _deviceAlerts[i].Size = new System.Drawing.Size(alertWidth, btnSide);
            }
        }

        /// <summary>构建设备按钮 Tooltip 文本（设备名 + 连接状态）。</summary>
        /// <returns>形如「线激光：已连接」的文本</returns>
        private string BuildDeviceTooltipText(int idx, bool connected)
        {
            return _deviceSwitchNames[idx] + (connected ? "：已连接" : "：未连接");
        }

        /// <summary>快捷开关点击分发。</summary>
        private void OnDeviceSwitchClick(int idx)
        {
            if (idx == LineLaserSwitchIndex)
                ToggleLineLaserConnection();
            else
                AntdUI.Message.info(this.FindForm() ?? System.Windows.Forms.Form.ActiveForm,
                    _deviceSwitchNames[idx] + "：暂未接入，无法操作");
            // 配色由定时器驱动，点击后不急变色
        }

        /// <summary>线激光是否已连接（徽标绿勾/红叉）。</summary>

        /// <returns>已连接返回 true</returns>
        private bool IsLineLaserRunning()
        {
            return LineLaserWorkflow.Instance.State == SubDeviceState.Connected;
        }

        /// <summary>切换线激光连接态（只关心连接/断开）。</summary>
        private void ToggleLineLaserConnection()
        {
            var wf = LineLaserWorkflow.Instance;
            // 界面按钮属手动动作，显式置手动模式
            wf.SetRunMode(WorkflowRunMode.Manual);
            try
            {
                if (wf.IsAlarm)
                {
                    // 报警态 → 提示复位（报警清除/复位由 ClearStatus 契约落地）
                    AntdUI.Message.info(this.FindForm() ?? System.Windows.Forms.Form.ActiveForm,
                        "线激光处于报警态，请先复位");
                    return;
                }

                if (wf.State == SubDeviceState.Connected)
                    // 已连接 → 断开（后台线程执行，回 Standby → State=Disconnected），不阻塞 UI
                    wf.ConnectOff();
                else
                    // 未连接 → 连接（后台线程执行，置 Standby → State=Connected），不阻塞 UI
                    wf.ConnectOn();
            }
            catch (Exception ex)
            {
                Log("线激光快捷开关切换失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>按连接态刷新快捷开关徽标与背景。</summary>
        public void RefreshDeviceSwitchUi()
        {
            if (!this.IsHandleCreated) return;
            if (_deviceButtons == null || _tooltip == null) return;
            int count = _deviceButtons.Length;
            if (_lastDeviceConnected == null || _lastDeviceConnected.Length != count)
            {
                _lastDeviceConnected = new bool[count];
            }

            // 实际设备状态（索引同名称表顺序）
            bool[] connected =
            {
                IsLineLaserRunning(),
                MonitorCameraWorkflow.Instance.State == SubDeviceState.Connected,
                MotionControlWorkflow.Instance.State == SubDeviceState.Connected,
                DeviceControlWork.Instance.IsRobotOnline
            };

            for (int i = 0; i < count; i++)
            {
                if (connected[i] == _lastDeviceConnected[i]) continue;   // 状态未变，跳过
                _lastDeviceConnected[i] = connected[i];
                _deviceButtons[i].BadgeSvg = connected[i] ? ConnectedBadgeSvg : DisconnectedBadgeSvg;
                _deviceButtons[i].BadgeBack = connected[i] ? DeviceBadgeGreen : DeviceBadgeRed;
                // 连接=主题默认白底；断开=灰底
                _deviceButtons[i].DefaultBack = connected[i] ? (System.Drawing.Color?)null : DeviceBackDisconnected;
                // Tooltip 同步状态文本（SetTip 支持原地更新）
                _tooltip.SetTip(_deviceButtons[i], BuildDeviceTooltipText(i, connected[i]));
                // 每区域 Alert：Success/Error 图标 + 绿/红文字色表示该设备连接态
                AntdUI.Alert alert = _deviceAlerts[i];
                if (alert != null)
                {
                    alert.Icon = connected[i] ? AntdUI.TType.Success : AntdUI.TType.Error;
                    alert.TextColor = connected[i]
                        ? System.Drawing.Color.FromArgb(0, 128, 0)
                        : System.Drawing.Color.FromArgb(192, 0, 0);
                }
            }
        }

        /// <summary>设置设备按钮图标 SVG。</summary>
        /// <param name="idx">设备索引（对应 _deviceSwitchNames 下标）</param>
        /// <param name="iconSvg">按钮图标 SVG 字符串（各按钮设备指向不同，逐个设置）</param>
        public void SetDeviceIconSvg(int idx, string iconSvg)
        {
            if (_deviceButtons == null || idx < 0 || idx >= _deviceButtons.Length) return;
            _deviceIconSvgs[idx] = iconSvg;
            _deviceButtons[idx].IconSvg = iconSvg;
        }

        /// <summary>设置设备徽标 SVG。</summary>
        /// <param name="idx">设备索引（对应 _deviceSwitchNames 下标）</param>
        /// <param name="badgeSvg">徽标 SVG 字符串；设置后徽标以 SVG 呈现，颜色仍随 BadgeBack（红/绿）</param>
        public void SetDeviceBadgeSvg(int idx, string badgeSvg)
        {
            if (_deviceButtons == null || idx < 0 || idx >= _deviceButtons.Length) return;
            _deviceButtons[idx].BadgeSvg = badgeSvg;
        }
        #endregion

        #region 工艺参数实时曲线（单选互斥）
        /// <summary>五项曲线互斥单选：先互销其余项再重建。</summary>

        /// <param name="sender">事件源（复选框）</param>
        /// <param name="e">勾选事件参数</param>
        private void ChkSeries_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            // 标识符不得用 C# 关键字（如 checked），统一加前缀规避

            if (_suppressSeriesEvent) return;

            // 取消勾选时不互销，否则无法取消到最后一项
            if (sender is AntdUI.Checkbox chkSelf && chkSelf.Checked)
            {
                _suppressSeriesEvent = true;
                try
                {
                    foreach (AntdUI.Checkbox box in SeriesCheckboxes())
                    {
                        if (!ReferenceEquals(box, chkSelf)) box.Checked = false;
                    }
                }
                finally
                {
                    _suppressSeriesEvent = false;
                }
            }

            RefreshParamCharts();
        }

        /// <summary>取得五个工艺参数复选框（顺序即展示顺序）。</summary>
        private AntdUI.Checkbox[] SeriesCheckboxes()
        {
            return new AntdUI.Checkbox[] { chkSeamWidth, chkRobotSpeed, chkFeedSpeed, chkLaserPower, chkWireZ };
        }

        /// <summary>构造后统一收敛为单选：仅保留 <paramref name="keep"/> 勾选。</summary>
        /// <param name="keep">要保留勾选的复选框。</param>
        private void ApplySeriesSingleSelection(AntdUI.Checkbox keep)
        {
            _suppressSeriesEvent = true;
            try
            {
                foreach (AntdUI.Checkbox box in SeriesCheckboxes())
                {
                    box.Checked = ReferenceEquals(box, keep);
                }
            }
            finally
            {
                _suppressSeriesEvent = false;
            }
        }
        #endregion

        #region 公共函数
        /// <summary>把指定字符区间按颜色上色。</summary>
        /// <param name="start">起始位置。</param>
        private void ApplyLineColor(int start, int length, Color color)
        {
            try
            {
                if (length <= 0) return;
                inputLog.SetStyle(start, length, null, color, null);
            }
            catch
            {
                // 上色属外观容错，打日志会递归回自身
            }
        }

        /// <summary>焊接预览帧上屏（工作流派发，已 30Hz 节流）。</summary>
        /// <param name="frame">预览帧（本页接管所有权）</param>
        private void OnWeldPreviewFrame(Mat frame)
        {
            if (frame == null) return;
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                DisposePreviewFrame(frame);
                return;
            }
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<Mat>(OnWeldPreviewFrame), frame);
                }
                catch
                {
                    DisposePreviewFrame(frame);
                }
                return;
            }
            ShowWeldPreviewFrame(frame);
        }

        /// <summary>上屏并释放上一帧（显式释放，不依赖控件语义）。</summary>
        /// <param name="frame">预览帧（所有权移交本控件）</param>
        private void ShowWeldPreviewFrame(Mat frame)
        {
            Mat previous = _weldPreviewFrame;
            _weldPreviewFrame = frame;
            try
            {
                imb_WeldViewer.Image = frame;
            }
            catch
            {
                // 赋值失败即释放本帧
                _weldPreviewFrame = previous;
                DisposePreviewFrame(frame);
                return;
            }
            if (previous != null && !ReferenceEquals(previous, frame))
                DisposePreviewFrame(previous);
        }

        /// <summary>释放预览帧副本</summary>
        /// <param name="frame">待释放帧</param>
        private static void DisposePreviewFrame(Mat frame)
        {
            try { frame.Dispose(); } catch { }   // 释放路径：失败不影响上屏
        }

        /// <summary>释放资源并退订事件。</summary>
        public void DisposeComponents()
        {
            GlobalCommData.EventInfoHandler -= CommenData_EventInfoHandler;
            GlobalCommData.CommunicationCommandReceived -= OnRobotCommandReceived;
            // 退订整机运行态（防内存泄漏）
            DeviceControlWork.Instance.StatusChanged -= OnMainDeviceStatusChanged;
            // 退订焊接过程态，防销毁后残留回调
            WeldParamControlWorkflow.Instance.WeldStatusChanged -= OnWeldParamWeldStatusChanged;
            // 退订焊接预览并释放末帧
            MonitorCameraWorkflow.Instance.WeldPreviewFrame -= OnWeldPreviewFrame;
            if (_weldPreviewFrame != null)
            {
                try { imb_WeldViewer.Image = null; } catch { }   // 先解除引用，再释放
                DisposePreviewFrame(_weldPreviewFrame);
                _weldPreviewFrame = null;
            }
            // 释放 UI 统一定时器（EStop 闪烁 + Dash 分频刷新共用）
            if (_uiTimer != null)
            {
                _uiTimer.Stop();
                _uiTimer.Tick -= UiTimer_Tick;
                _uiTimer.Dispose();
                _uiTimer = null;
            }
            // 停止轮廓轮询线程，防引用已销毁页面
            StopProfilePolling();
            // 释放 Tooltip 组件
            _tooltip?.Dispose();
            _tooltip = null;
            // 退订面板尺寸变化，事件订阅在此收口
            panelfloatbutton.SizeChanged -= OnPanelFloatButtonSizeChanged;

            // 四合一图复选框退订，控件随树释放
            chkSeamWidth.CheckedChanged -= ChkSeries_CheckedChanged;
            chkRobotSpeed.CheckedChanged -= ChkSeries_CheckedChanged;
            chkFeedSpeed.CheckedChanged -= ChkSeries_CheckedChanged;
            chkLaserPower.CheckedChanged -= ChkSeries_CheckedChanged;
            chkWireZ.CheckedChanged -= ChkSeries_CheckedChanged;
        }

        /// <summary>追加日志并按级别着色。</summary>
        /// <param name="color">文本颜色（ShowLog 按 MessageLevel 映射，Error=红）</param>
        /// <param name="strMsg">日志内容</param>
        public void LogAppend(Color color, string strMsg)
        {
            string line = null;
            try
            {
                int start = inputLog.Text.Length;
                line = string.Format("{0}-{1}\n",
                    AdaWeldSystem.FileOperate.Log.FormatTimestamp(DateTime.Now), strMsg);
                inputLog.AppendText(line);

                // 按整行区间着色（区间开闭未定，宁多不少）
                ApplyLineColor(start, line.Length, color);
            }
            catch
            {
                // 着色失败不得影响日志落地：退化为纯文本追加
                if (line != null)
                {
                    try
                    {
                        int retryStart = Math.Max(0, inputLog.Text.Length - line.Length);
                        inputLog.AppendText(line);
                        ApplyLineColor(retryStart, line.Length, color);
                    }
                    catch
                    {
                        // 追加失败属外观容错，打日志会递归
                    }
                }
            }
        }

        /// <summary>初始化轮廓线图表。</summary>
        public void AddContourLineChart()
        {
            // 轮廓图：AddSignalXY 显式坐标，轴范围首帧锁定，叠加特征点

            // 高 DPI 修复（与其他 ScottPlot 一致）
            formsPlot1.Configuration.DpiStretch = false;

            // 锁定轴缩放与平移（固定视场，防止用户误操作）
            formsPlot1.Configuration.ScrollWheelZoom = false;
            formsPlot1.Configuration.LeftClickDragPan = false;
            formsPlot1.Configuration.RightClickDragZoom = false;
            formsPlot1.Configuration.MiddleClickDragZoom = false;

            formsPlot1.Plot.Clear();
            formsPlot1.Plot.XLabel("Y 轴（沿激光线，mm）");
            formsPlot1.Plot.YLabel("Z 轴（高度 mm）");

            // ① 轮廓线：直接引用两个显式数组（X 非等距）
            _contourSignalPlot = formsPlot1.Plot.AddSignalXY(_contourSignalXs, _contourSignalData,
                System.Drawing.Color.FromArgb(128, 0, 128), "轮廓");
            _contourSignalPlot.LineWidth = 2;
            // 初始不可见：全 0 会画出假轮廓，首帧后再显示
            _contourSignalPlot.IsVisible = false;

            // ② 特征点散点（红色实心圆，初始隐藏）
            _featurePointPlot = formsPlot1.Plot.AddScatter(
                _featureXs, _featureYs,
                System.Drawing.Color.Red, 0, markerSize: 8,
                ScottPlot.MarkerShape.filledCircle, label: "特征点");
            _featurePointPlot.IsVisible = false;

            // 初始轴范围（默认值，校准后更新）
            formsPlot1.Plot.SetAxisLimits(_contourYMin, _contourYMax, _contourZMin, _contourZMax);
            // 图例在此创建：唯一构建路径，自动收集
            // ⚠️ 不可在 SetupChart 再建，否则图例重复渲染
            formsPlot1.Plot.Legend();
            formsPlot1.Refresh();

            // 启动轮廓轮询：按节拍从线激光取点刷新
            StartProfilePolling();
        }

        #endregion
    }
}

