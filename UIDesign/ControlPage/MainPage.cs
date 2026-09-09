using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.PCLOperate.Models;
using AntdUI;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
//using ScottPlot;

namespace AdaWeldSystem.Sub1UI
{
    /// <summary>
    /// 主页面
    /// </summary>
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

        // 英莱线激光轮廓 ScottPlot（AddSignalXY 模式，固定数组 1920 点，目标 60Hz）
        // 用 AddSignalXY(xs, ys) 而非 AddSignal：X=沿激光线物理坐标 Y 非等距；SignalXY 支持显式 X 且保留 Signal 高性能渲染（优于 AddScatter）
        private const int ContourSignalMaxPoints = 1920;
        private double[] _contourSignalData;   // Y 值数组（高度 Z，mm），SignalXY 直接引用此数组
        private double[] _contourSignalXs;     // X 值数组（沿激光线物理坐标 Y，mm，非等距），SignalXY 直接引用
        private ScottPlot.Plottable.SignalPlotXY _contourSignalPlot;  // AddSignalXY 返回 SignalPlotXY，字段类型须与之匹配（此 ScottPlot 版本中二者非继承关系）
        private ScottPlot.Plottable.ScatterPlot _featurePointPlot;  // 特征点散点（红色实心圆）
        private double[] _featureXs = new double[1];  // 特征点 X 数组（复用，避免每帧分配）
        private double[] _featureYs = new double[1];  // 特征点 Y 数组（复用，避免每帧分配）

        // 轮廓轮询：主页面用独立线程按固定节拍从 LineLaserManager 拉取最新快照（ADR-039），
        // 取代原先「回调逐帧驱动 UI」的方式——界面刷新不必跟随相机帧率，且手动/自动模式可各自取舍。
        private const int ProfilePollIntervalMs = 33;   // 约 30Hz
        private Thread _profilePollThread;
        private volatile bool _profilePolling;
        private long _lastProfileVersion;

        // 轮廓图轴范围（mm）：优先用校准框动态确定，无校准框时用默认回退值
        private double _contourYMin = -30.0;
        private double _contourYMax = 30.0;
        private double _contourZMin = -10.0;
        private double _contourZMax = 50.0;
        // 轴范围是否已锁定：相机连接后校准数据到达时锁定一次；重新订阅相机（切换/重连）时复位。
        // 锁定后未重连恒定不变，每帧仅更新轮廓与特征点，不再重设轴范围（呼应需求）。
        private bool _contourAxisLocked = false;
        // 高频事件合并派发：保证 BeginInvoke 队列中最多 1 个待处理，根治逐事件派发导致的队列堆积（内存↑/卡死）
        private volatile bool _uiUpdatePending = false;
        // 生产者线程快照：在相机回调线程立即拷贝，避免 BeginInvoke 延迟读取被相机回收的云点缓冲（Bug1 候选 b：读到陈旧/清零数据→平直线）
        private int _snapPointCount = 0;
        private bool _snapHasResult = false;
        private double _snapFeatureY = 0.0;
        private double _snapFeatureZ = 0.0;
        private bool _pendingAxisLock = false;

        // 硬件快捷开关（AntdUI ButtonShadow 阴影按钮，位于 panelfloatbutton 面板，随页面显隐自动生效）
        // 形态对齐 AntdUI Demo buttonShadow1：50×50 方形阴影按钮，仅显示 SVG 图标（无文本），
        // 设备名经 TooltipComponent 顶部提示，连接态由原生徽标 Badge 呈现（红叉=断开/绿勾=连接）
        private AntdUI.ButtonShadow[] _deviceButtons;            // 与 _deviceSwitchNames 一一对应
        private AntdUI.TooltipComponent _tooltip;                // 设备名顶部提示（方位 Top）
        private string[] _deviceIconSvgs;                        // 各设备按钮图标 SVG（设备指向不同，可经 SetDeviceIconSvg 逐个替换）
        private bool _lastLlConnected = false;                   // 缓存线激光连接态，避免每帧重设配色
        // 设备名称（Tooltip 提示文本）
        private readonly string[] _deviceSwitchNames = { "焊接头", "线激光", "监控相机", "运动控制器", "机器人通讯", "PLC通讯" };
        // 线激光为数组中唯一当前可测的真实开关（其余硬件暂未接入）
        private const int LineLaserSwitchIndex = 1;
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

        // ── 整机状态标签（LabelStatus，承接原 FormMain 左下角整机状态）──
        private System.Windows.Forms.Timer _statusBlinkTimer;   // EStop 态 0.5s 红闪定时器
        private bool _blinkOn;                                  // 闪烁相位（true=亮红）
        private MainDeviceStatus _currentStatus;                // 当前整机态（闪烁 Tick 防越界改色）
        #endregion

        #region 构造函数
        public MainPage()
        {
            InitializeComponent();
            // ADR-002：字段禁止 auto-property/声明处初始化，统一在构造函数初始化（同时避免 Clear() 空引用）
            SeamWidth = new ChartVar();
            robotSpeed = new ChartVar();
            LaserPower = new ChartVar();
            FeedSpeed = new ChartVar();
            // 轮廓 SignalXY 数组初始化（固定长度 1920，ScottPlot SignalXY 直接引用此数组原地修改）
            _contourSignalData = new double[ContourSignalMaxPoints];
            _contourSignalXs = new double[ContourSignalMaxPoints];
            // 递增占位：AddSignalXY 构造时要求 Xs 单调递增，否则抛 ArgumentException；
            // 填 i 作为严格递增占位保证 AddSignalXY 调用不崩，首帧数据到达后由生产者线程用真实 Y 覆盖（数据严格递增，无需额外校验）。
            for (int i = 0; i < ContourSignalMaxPoints; i++)
                _contourSignalXs[i] = i;
            GlobalCommData.EventInfoHandler += CommenData_EventInfoHandler;

            // 硬件快捷开关按钮（常驻 panelfloatbutton 面板，页面切换时随宿主自动显隐，无需事件同步）
            InitDeviceSwitchButtons();

            // 页面销毁时释放外部资源（ADR-001：UI 类用 DisposeComponents，不在 .cs 重写 Dispose(bool)）
            this.HandleDestroyed += MainPage_HandleDestroyed;

            // 整机状态标签（承接原 FormMain 左下角整机状态，2026-09-08 迁移）：
            // 闪烁定时器 0.5s 翻转一次相位（ADR-002：字段统一在构造函数初始化）
            _statusBlinkTimer = new System.Windows.Forms.Timer();
            _statusBlinkTimer.Interval = 500;
            _statusBlinkTimer.Tick += StatusBlinkTimer_Tick;
            DeviceControlWork.Instance.StatusChanged += OnMainDeviceStatusChanged;
            ApplyDeviceStatus(DeviceControlWork.Instance.Status);
        }
        #endregion

        #region 私有函数
        private void MainPage_HandleDestroyed(object sender, EventArgs e)
        {
            DisposeComponents();
        }

        /// <summary>
        /// 启动轮廓轮询线程（取代原先订阅相机回调的方式）
        /// </summary>
        private void StartProfilePolling()
        {
            // 轴范围锁定复位：重启轮询（相机切换/重连）时，下一帧用新校准框重新锁定
            _contourAxisLocked = false;
            // 轮廓线复位为不可见：重连/切换相机期间尚未有有效数据，避免残留上一会话的轮廓线
            if (_contourSignalPlot != null) _contourSignalPlot.IsVisible = false;

            if (_profilePolling) return;
            _profilePolling = true;
            _lastProfileVersion = 0;

            _profilePollThread = new Thread(ProfilePollLoop);
            _profilePollThread.IsBackground = true;
            _profilePollThread.Name = "轮廓轮询";
            _profilePollThread.Start();
        }

        /// <summary>
        /// 停止轮廓轮询线程
        /// </summary>
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

        /// <summary>
        /// 轮询主循环：按固定节拍从线激光管理器取最新快照，版本号未变则跳过
        /// </summary>
        private void ProfilePollLoop()
        {
            while (_profilePolling)
            {
                try
                {
                    ProfileSnapshot snap = LineLaserManager.Instance.Profile;
                    if (snap != null && snap.Version != _lastProfileVersion)
                    {
                        _lastProfileVersion = snap.Version;
                        ApplyProfileSnapshot(snap);
                    }
                }
                catch
                {
                    // 轮询异常不影响页面其它功能
                }
                Thread.Sleep(ProfilePollIntervalMs);
            }
        }

        /// <summary>
        /// 把快照拷入 ScottPlot 复用数组并派发一次界面刷新
        /// </summary>
        /// <param name="snap">轮廓帧快照</param>
        private void ApplyProfileSnapshot(ProfileSnapshot snap)
        {
            int pointCount = Math.Min(snap.Count, ContourSignalMaxPoints);
            if (pointCount <= 0) return;

            for (int i = 0; i < pointCount; i++)
            {
                _contourSignalData[i] = snap.Zs[i];
                _contourSignalXs[i] = snap.Ys[i];   // X = 沿激光线物理坐标 Y（非等距，AddSignalXY 显式传入）
            }
            _snapPointCount = pointCount;

            _snapHasResult = snap.HasResult;
            if (snap.HasResult)
            {
                _snapFeatureY = snap.FeatureY;
                _snapFeatureZ = snap.FeatureZ;
            }

            LockContourAxisOnce();

            // 合并派发：保证 BeginInvoke 队列最多 1 个待处理，根治逐事件派发导致的队列堆积（内存↑/卡死，Bug2/3）
            if (_uiUpdatePending) return;
            _uiUpdatePending = true;
            if (formsPlot1.InvokeRequired)
                formsPlot1.BeginInvoke(new Action(UpdateChartUI));
            else
                UpdateChartUI();
        }

        /// <summary>
        /// 首帧轴范围锁定（仅一次，满足"固定视图"约束）：
        /// 由相机校准框四角点确定轴限度——此为相机视野的最大/最小值，
        /// 角点可带负值（Math.Min/Max 已兼容），故不假设符号、直接取极值。锁定后恒定不变，每帧仅更新轮廓与特征点。
        /// SetAxisLimits 属 UI 操作，置 _pendingAxisLock 由 UI 线程应用。
        /// </summary>
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

            // 范围有效性检查 + 5% 边距，避免轮廓贴边或零范围报错
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

        /// <summary>
        /// UI 线程执行：应用轴范围锁定 + 更新可见点数/映射/特征点 + Refresh（轻量，不重算数据）
        /// </summary>
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

                // 本帧有效点范围：MinRenderIndex 固定 0，MaxRenderIndex 限制只渲染前 pointCount 个（替代清除多余点数据）
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

        /// <summary>
        /// 绑定 ChartVar 到图表
        /// </summary>
        /// <param name="chart">目标图表</param>
        /// <param name="seriesName">系列名称</param>
        /// <param name="data">坐标序列</param>
        private void BindChartVar(ScottPlot.FormsPlot chart, string seriesName, ChartVar data)
        {
            chart.Plot.Clear();
            int count = data.Count();
            if (count > 0)
            {
                double[] xs = data.X.ToArray();
                double[] ys = data.Y.ToArray();
                chart.Plot.AddScatter(xs, ys, System.Drawing.Color.FromArgb(128, 0, 128), 2, markerSize: 0, label: seriesName);
                chart.Plot.AxisAuto();
            }
            chart.Refresh();
        }

        void AfterInitializeUI()
        {
            // 线激光数据输入（轮廓）
            SetupChart(formsPlot1, "焊缝模型数据", "轮廓");
            // 焊缝宽度
            SetupChart(formsPlot2, "焊缝宽度", "焊缝宽度");
            // 机器人运动速度
            SetupChart(formsPlot3, "机器人运动速度", "运动速度");
            // 机器人送丝速度
            SetupChart(formsPlot4, "送丝机送丝速度", "送丝速度");
            // 机器人激光功率
            SetupChart(formsPlot5, "激光器激光功率", "激光功率");
        }

        /// <summary>
        /// 初始化折线图
        /// </summary>
        /// <param name="chart">目标图表</param>
        /// <param name="title">图表标题</param>
        /// <param name="seriesName">系列名称</param>
        private void SetupChart(ScottPlot.FormsPlot chart, string title, string seriesName)
        {
            // [lessons/ScottPlot-FormsPlot-Usage#dpi] 修复高 DPI 缩放下字体锯齿：
            // Program.cs 用 SetProcessDPIAware()（System DPI aware），显示缩放 >100% 时
            // ScottPlot 默认 DpiStretch=true 会拉伸低分辨率位图导致字体模糊/锯齿；
            // 置 false 让 ScottPlot 按真实 DPI 渲染高分辨率位图（官方 4.1 cookbook misc_dpiscale）。
            chart.Configuration.DpiStretch = false;
            chart.Plot.Title(title);
            chart.Plot.XLabel("机器人 X 轴");
            chart.Plot.YLabel(seriesName);
            chart.Plot.Legend();
            chart.Refresh();
        }

        private void CommenData_EventInfoHandler(object sender, MessageArgs e)
        {
            // [lessons/UI-Thread-Safety] 事件可能来自后台线程：先判 InvokeRequired，
            // 并用 BeginInvoke 异步封送避免阻塞发布线程（原为无守卫的阻塞 Invoke，存在死锁隐患）
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
        /// <summary>
        /// 主设备运行态变更回调（承接原 FormMain 左下角整机状态标签）
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">运行态变更参数</param>
        private void OnMainDeviceStatusChanged(object sender, MainDeviceStatusChangedEventArgs e)
        {
            // 事件可能来自后台线程：封送到 UI 线程（[[lessons/UI-Thread-Safety]]）
            if (LabelStatus.InvokeRequired)
            {
                LabelStatus.BeginInvoke(new Action(() => ApplyDeviceStatus(e.NewStatus)));
                return;
            }
            ApplyDeviceStatus(e.NewStatus);
        }

        /// <summary>
        /// 应用整机状态到 LabelStatus：文本「设备状态：{状态值}」，背景色按设备颜色映射
        /// </summary>
        /// <remarks>颜色映射（2026-09-08 用户二次定义）：Running=绿 / Reseting=黄 / Alarm=橙 /
        /// EStop(急停)=红闪(0.5s 红↔白) / Stop=蓝(不闪) / NoReset=灰。
        /// EStop 闪烁由 _statusBlinkTimer 每 0.5s 翻转相位实现。</remarks>
        /// <param name="state">整机运行态</param>
        private void ApplyDeviceStatus(MainDeviceStatus state)
        {
            _currentStatus = state;
            string text;
            System.Drawing.Color back;
            System.Drawing.Color fore;
            switch (state)
            {
                case MainDeviceStatus.Running:
                    text = "设备状态：运行中";
                    back = System.Drawing.Color.Green;
                    fore = System.Drawing.Color.White;
                    break;
                case MainDeviceStatus.Reseting:
                    text = "设备状态：复位中";
                    back = System.Drawing.Color.Yellow;
                    fore = System.Drawing.Color.Black;
                    break;
                case MainDeviceStatus.Stop:
                    text = "设备状态：已停止";
                    back = System.Drawing.Color.Blue;
                    fore = System.Drawing.Color.White;
                    break;
                case MainDeviceStatus.Alarm:
                    text = "设备状态：报警";
                    back = System.Drawing.Color.Orange;
                    fore = System.Drawing.Color.Black;
                    break;
                case MainDeviceStatus.EStop:
                    // 亮相位固定红底；暗相位（白）由闪烁定时器翻转
                    text = "设备状态：急停";
                    back = System.Drawing.Color.Red;
                    fore = System.Drawing.Color.White;
                    break;
                case MainDeviceStatus.NoReset:
                    text = "设备状态：未复位";
                    back = System.Drawing.Color.Gray;
                    fore = System.Drawing.Color.White;
                    break;
                default:
                    text = "设备状态：" + state.ToString();
                    back = System.Drawing.Color.Gray;
                    fore = System.Drawing.Color.White;
                    break;
            }
            LabelStatus.Text = text;
            LabelStatus.BackColor = back;
            LabelStatus.ForeColor = fore;

            // 仅 EStop 态跑闪烁表，其余态停表并保持固定底色
            if (state == MainDeviceStatus.EStop)
            {
                _blinkOn = true;
                _statusBlinkTimer.Start();
            }
            else
            {
                _statusBlinkTimer.Stop();
            }
        }

        /// <summary>
        /// EStop 态红闪定时器：每 0.5s 翻转一次背景相位（红 ↔ 白）
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void StatusBlinkTimer_Tick(object sender, EventArgs e)
        {
            // 防御式守卫：非 EStop 态不启表，此处理论上不会命中
            if (_currentStatus != MainDeviceStatus.EStop) return;
            _blinkOn = !_blinkOn;
            LabelStatus.BackColor = _blinkOn ? System.Drawing.Color.Red : System.Drawing.Color.White;
        }
        #endregion

        #region 硬件快捷开关（AntdUI ButtonShadow）
        /// <summary>
        /// 初始化硬件快捷开关按钮（常驻 panelfloatbutton 面板），徽标红=断开/绿=连接
        /// </summary>
        private void InitDeviceSwitchButtons()
        {
            // Tooltip 组件（TooltipDemo 范式）：单实例对多控件 SetTip，方位 Top
            _tooltip = new AntdUI.TooltipComponent
            {
                Font = new System.Drawing.Font("Microsoft YaHei UI", 10F),
                ArrowAlign = AntdUI.TAlign.Top,
            };
            int count = _deviceSwitchNames.Length;
            _deviceButtons = new AntdUI.ButtonShadow[count];
            _deviceIconSvgs = new string[count];
            // 面板已竖直摆放：顶部标题 Label「子设备状态」，其下按钮 52×52 自上而下纵向堆叠
            const int btnWidth = 60, btnHeight = 60, gap = 8, left = 20, top = 2;
            // 标题：面板最上方「子设备状态」（宽度自适应文本，高度 24 定值）
            const int labelHeight = 24;
           
            // 按钮起始 Y 让位标题（标题下方再留一个 gap）
            int btnTop = top ;
            for (int i = 0; i < count; i++)
            {
                _deviceIconSvgs[i] = DefaultDeviceIconSvg;   // 各设备图标默认同源，可经 SetDeviceIconSvg 逐个替换
                var btn = new AntdUI.ButtonShadow
                {
                    Name = "btnDev_" + i,
                    BorderWidth = 1F,
                    IconRatio = 1.2F,
                    IconSvg = _deviceIconSvgs[i],
                    ShadowColor = System.Drawing.Color.Black,
                    // 断开默认灰底（158,158,158），连接态由 RefreshDeviceSwitchUi 切回主题默认白
                    DefaultBack = DeviceBackDisconnected,
                    // 原生徽标 SVG：断开=CloseCircleFilled 叉号（红），填色由 BadgeBack 驱动
                    BadgeSvg = DisconnectedBadgeSvg,
                    BadgeAlign = AntdUI.TAlign.TR,
                    BadgeBack = DeviceBadgeRed,
                    BadgeSize=1.0F,
                    Size = new System.Drawing.Size(btnWidth, btnHeight),
                    Location = new System.Drawing.Point(left, btnTop + i * (btnHeight + gap)),
                };
                int idx = i;   // 闭包须拷贝循环变量
                btn.Click += (s, e) => OnDeviceSwitchClick(idx);
                // Tooltip：设备名 + 连接状态（初始全部未连接；线激光随态刷新）
                _tooltip.SetTip(btn, BuildDeviceTooltipText(i, false));
                _deviceButtons[i] = btn;
                panelfloatbutton.Controls.Add(btn);
            }
        }

        /// <summary>
        /// 构建设备按钮 Tooltip 文本（设备名 + 连接状态）
        /// </summary>
        /// <param name="idx">设备索引（对应 _deviceSwitchNames 下标）</param>
        /// <param name="connected">是否已连接</param>
        /// <returns>形如「线激光：已连接」的文本</returns>
        private string BuildDeviceTooltipText(int idx, bool connected)
        {
            return _deviceSwitchNames[idx] + (connected ? "：已连接" : "：未连接");
        }

        /// <summary>
        /// 快捷开关点击分发
        /// </summary>
        /// <param name="idx">设备索引（对应 _deviceSwitchNames 下标）</param>
        private void OnDeviceSwitchClick(int idx)
        {
            if (idx == LineLaserSwitchIndex)
                ToggleLineLaserConnection();
            else
                AntdUI.Message.info(this.FindForm() ?? System.Windows.Forms.Form.ActiveForm,
                    _deviceSwitchNames[idx] + "：暂未接入，无法操作");
            // 注：按钮配色刷新由定时器据工作流实例状态驱动，点击后不立即变色（避免状态错乱）
        }

        /// <summary>
        /// 线激光是否已连接（用于快捷开关徽标绿勾/红叉）
        /// </summary>
        /// <remarks>基于设备连接态 SubDeviceState：Connected 视为已连接（绿勾白底），Disconnected 视为未连接（红叉灰底）。
        /// 与开关按钮的连接语义一致，不再依据焊接过程态 PreWork/Working 判断。</remarks>
        /// <returns>已连接返回 true</returns>
        private bool IsLineLaserRunning()
        {
            var st = LineLaserWorkflow.Instance.State;
            return st == SubDeviceState.Connected;
        }

        /// <summary>
        /// 切换线激光连接态（设备开关语义：只关心连接/断开，与流程状态无关）
        /// </summary>
        /// <remarks>基于设备连接态 SubDeviceState 判断：Disconnected → ConnectOn 连接；Connected → ConnectOff 断开；报警态 → 提示复位（IsAlarm）。
        /// 走 LineLaserWorkflow 的 public ConnectOn/ConnectOff 契约入口（内部自起后台线程，非阻塞），不越权直驱子类 Start/Stop/Reset。</remarks>
        private void ToggleLineLaserConnection()
        {
            var wf = LineLaserWorkflow.Instance;
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
                GlobalCommData.ShowLog("MainPage", "线激光快捷开关切换失败：" + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 按连接态刷新快捷开关徽标与背景（绿勾+白底=已连接/红叉+灰底=断开）
        /// </summary>
        /// <remarks>ButtonShadow 为常驻控件，仅改 BadgeSvg/BadgeBack/DefaultBack 属性即可，无需重建；按钮 Type 保持默认中性态。</remarks>
        public void RefreshDeviceSwitchUi()
        {
            if (!this.IsHandleCreated) return;
            bool ll = IsLineLaserRunning();
            if (ll == _lastLlConnected) return;   // 状态未变，跳过
            _lastLlConnected = ll;
            if (_deviceButtons == null) return;
            for (int i = 0; i < _deviceButtons.Length; i++)
            {
                // 其余 5 个硬件暂未接入，固定断开叉号灰底；仅线激光随运行态切换 对勾/叉号 + 绿/红 + 白/灰底
                if (i == LineLaserSwitchIndex)
                {
                    _deviceButtons[i].BadgeSvg = ll ? ConnectedBadgeSvg : DisconnectedBadgeSvg;
                    _deviceButtons[i].BadgeBack = ll ? DeviceBadgeGreen : DeviceBadgeRed;
                    // 连接=主题默认白底；断开=灰底
                    _deviceButtons[i].DefaultBack = ll ? (System.Drawing.Color?)null : DeviceBackDisconnected;
                    // Tooltip 同步状态文本（SetTip 支持原地更新）
                    _tooltip.SetTip(_deviceButtons[i], BuildDeviceTooltipText(i, ll));
                }
            }
        }

        /// <summary>
        /// 设置设备按钮图标 SVG（预留接口）
        /// </summary>
        /// <param name="idx">设备索引（对应 _deviceSwitchNames 下标）</param>
        /// <param name="iconSvg">按钮图标 SVG 字符串（各按钮设备指向不同，逐个设置）</param>
        public void SetDeviceIconSvg(int idx, string iconSvg)
        {
            if (_deviceButtons == null || idx < 0 || idx >= _deviceButtons.Length) return;
            _deviceIconSvgs[idx] = iconSvg;
            _deviceButtons[idx].IconSvg = iconSvg;
        }

        /// <summary>
        /// 设置设备徽标 SVG（预留接口）
        /// </summary>
        /// <param name="idx">设备索引（对应 _deviceSwitchNames 下标）</param>
        /// <param name="badgeSvg">徽标 SVG 字符串；设置后徽标以 SVG 呈现，颜色仍随 BadgeBack（红/绿）</param>
        public void SetDeviceBadgeSvg(int idx, string badgeSvg)
        {
            if (_deviceButtons == null || idx < 0 || idx >= _deviceButtons.Length) return;
            _deviceButtons[idx].BadgeSvg = badgeSvg;
        }
        #endregion

        #region 公共函数
        /// <summary>
        /// 释放资源并退订事件
        /// </summary>
        public void DisposeComponents()
        {
            GlobalCommData.EventInfoHandler -= CommenData_EventInfoHandler;
            // 退订整机运行态（防内存泄漏）+ 释放闪烁定时器
            DeviceControlWork.Instance.StatusChanged -= OnMainDeviceStatusChanged;
            if (_statusBlinkTimer != null)
            {
                _statusBlinkTimer.Stop();
                _statusBlinkTimer.Tick -= StatusBlinkTimer_Tick;
                _statusBlinkTimer.Dispose();
                _statusBlinkTimer = null;
            }
            // 停止轮廓轮询线程（防止后台线程继续引用已销毁页面）
            StopProfilePolling();
            // 释放 Tooltip 组件
            if (_tooltip != null)
            {
                _tooltip.Dispose();
                _tooltip = null;
            }
        }

        /// <summary>
        /// 初始化轮廓线图表
        /// </summary>
        public void AddContourLineChart()
        {
            // 英莱线激光轮廓 ScottPlot（AddSignalXY 模式）：
            // 固定长度 double[1920] 数组（xs/ys 显式坐标）+ AddSignalXY + Refresh()，目标 60Hz 全帧率刷新。
            // 用 AddSignalXY 而非 AddSignal：轮廓点沿激光线的 X（物理坐标 Y）非等距，AddSignalXY 支持显式 X 且仍保留 Signal 高性能渲染（优于 AddScatter）。
            // 事件触发 + 跨线程 InvokeRequired 封送（项目一贯做法，呼应 [[lessons/UI-Thread-Safety]]）。
            // X 轴 = 沿激光线物理坐标 Y（mm，非等距，逐点显式传入）；Y 轴 = 高度 Z（mm）。
            // 轴范围首帧锁定（Z 用数据实际范围、Y 用校准框或数据 X 范围），同时叠加特征点散点（红色大圆点）。

            // 高 DPI 修复（与其他 ScottPlot 一致）
            formsPlot1.Configuration.DpiStretch = false;

            // 锁定轴缩放与平移（固定视场，防止用户误操作）
            formsPlot1.Configuration.ScrollWheelZoom = false;
            formsPlot1.Configuration.LeftClickDragPan = false;
            formsPlot1.Configuration.RightClickDragZoom = false;
            formsPlot1.Configuration.MiddleClickDragZoom = false;

            formsPlot1.Plot.Clear();
            formsPlot1.Plot.Title("焊缝轮廓（YZ）");
            formsPlot1.Plot.XLabel("Y 轴（沿激光线，mm）");
            formsPlot1.Plot.YLabel("Z 轴（高度 mm）");

            // ① 轮廓线 SignalXY 图：直接引用 _contourSignalXs（X=Y 物理坐标，非等距）+ _contourSignalData（Z 高度）两个显式数组
            _contourSignalPlot = formsPlot1.Plot.AddSignalXY(_contourSignalXs, _contourSignalData,
                System.Drawing.Color.FromArgb(128, 0, 128), "轮廓");
            _contourSignalPlot.LineWidth = 2;
            // 初始不可见：未连接相机/无有效数据时，_contourSignalData 全为 0，若直接渲染会画出一条 Z=0 的
            // 假轮廓线。故初始 IsVisible=false，首帧有效数据到达（ApplyProfileSnapshot）后才置 true。
            _contourSignalPlot.IsVisible = false;

            // ② 特征点散点（红色实心圆，markerSize=8，初始 IsVisible=false 隐藏）
            // 注意：ScatterPlot.Xs/Ys 是只读属性，只能修改数组元素，不能重新赋值数组
            //       markerSize 仅能在 AddScatter 构造时传入，运行时用 IsVisible 控制显隐
            _featurePointPlot = formsPlot1.Plot.AddScatter(
                _featureXs, _featureYs,
                System.Drawing.Color.Red, 0, markerSize: 8,
                ScottPlot.MarkerShape.filledCircle, label: "特征点");
            _featurePointPlot.IsVisible = false;

            // 初始轴范围（默认回退值，校准框数据到达后更新为真实范围）
            formsPlot1.Plot.SetAxisLimits(_contourYMin, _contourYMax, _contourZMin, _contourZMax);
            formsPlot1.Refresh();

            // 启动轮廓轮询线程：从线激光管理器按节拍取点刷新（ADR-039）
            StartProfilePolling();
        }

        /// <summary>
        /// 绑定实时数据曲线
        /// </summary>
        public void AddNewDataLineChart()
        {
            // 各曲线实时数据重绑：以 ChartVar(X/Y) 全量重建后刷新
            BindChartVar(formsPlot2, "焊缝宽度", SeamWidth);
            BindChartVar(formsPlot3, "运动速度", robotSpeed);
            BindChartVar(formsPlot4, "送丝速度", FeedSpeed);
            BindChartVar(formsPlot5, "激光功率", LaserPower);
        }

        /// <summary>
        /// 追加日志文本
        /// </summary>
        /// <param name="color">文本颜色</param>
        /// <param name="strMsg">日志内容</param>
        public void LogAppend(Color color, string strMsg)
        {
            try
            {
                // AntdUI.Input 基于 TextBox，不支持 SelectionColor（彩色日志），仅追加文本
                inputLog.AppendText(string.Format("{0}-{1}\n", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), strMsg));
            }
            catch
            { }
        }
        #endregion
    }
}
