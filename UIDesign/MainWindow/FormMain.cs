using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.LineLaserCam;
using AntdUI;
using System;
using System.ComponentModel;
using System.Windows.Forms;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.UIDesign.ControlPage;
using AdaWeldSystem.UIDesign.SubDialog.FormMainDialog;
using AdaWeldSystem.UIDesign.SubDialog.UserManagerDialog;
using AdaWeldSystem.UIDesign.TabPage;

namespace AdaWeldSystem.UIDesign.MainWindow
{
    /// <summary>主窗口</summary>
    public partial class FormMain : Window
    {
        #region 私有变量
        // 子页面
        MainPage mainPage;
        DeviceMonitorPage deviceMonitorPage;
        NavPage navPageParam;
        DataReviewerPage dataPage;

        bool HidePage = false;
        // 被隐藏页签的引用，用于恢复显示
        AntdUI.SegmentedItem _hiddenItem1, _hiddenItem2, _hiddenItem3;

        // 初始化等待面板（Modal 内容，初始化结束事件自动关闭）
        private InitWaitingForm _initWaiting;
        #endregion

        #region 构造函数
        public FormMain()
        {
            InitializeComponent();

            // 设计器模式跳过初始化，避免单例/硬件依赖
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            // ── 配置加载 ──
            // 运控配置由 MontionManager 构造时加载
            DeviceControlWork.Instance.LoadSimulation();     // 模拟模式参数
            AlgorithmManager.Instance.Load();  // 监控相机算法参数

            // ── 全局 UI 配置（AntdUI 主题 + 文本渲染） ──
            GlobalCommData.UIConfigSetting();

            // ── 子页面实例化 ──
            mainPage = new MainPage();
            deviceMonitorPage = new DeviceMonitorPage();
            navPageParam = new NavPage();
            dataPage = new DataReviewerPage();

            // 加载子页配置到 NavPage（全量加载）
            navPageParam.LoadTabs(
                ("工艺设置", new ProcessControlPage(), "ControlOutlined"),
                ("系统设置", new SystemControlPage(), "ToolOutlined"),
                ("线激光设置", new LineLaserCamPage(), "FundOutlined"),
                ("监控相机设置", new MonitorCamPage(), "VideoCameraOutlined"),
                ("通讯设置", new CommSettingPage(), "ApiOutlined"),
                ("设置备份", new FilebackupPage(), "ApartmentOutlined")
            );

            LoadNavAndPages();

            // 全部子页面已实例化，才允许设备初始化 RSI
            // 防止通讯早于界面就绪导致回调空控件。
            GlobalCommData.MarkUiReady();

            // ── 事件订阅 ──
            LineLaserWorkflow.Instance.WeldStatusChanged += OnWorkflowStateChanged;
            UpdateStatusLabel(LineLaserWorkflow.Instance.WeldStatus);

            // 权限变更即时刷新导航显隐与权限标签
            GlobalCommData.OperateLevelChanged += OnOperateLevelChanged;

            // 关闭前询问
            this.FormClosing += FormMain_FormClosing;

            // ── 设备初始化：归 DeviceControlWork 管控；置于 Shown，界面先行、Modal 等待、结束自动关闭
            this.Shown += FormMain_Shown;
        }
        #endregion

        #region 私有函数

        /// <summary>首次呈现后触发整机初始化并弹出等待 Modal（初始化</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void FormMain_Shown(object sender, EventArgs e)
        {
            _initWaiting = new InitWaitingForm();
            DeviceControlWork.Instance.InitializationCompleted += OnInitCompleted;
            DeviceControlWork.Instance.InitializeMachine();

            // 阻塞式 Modal：后台线程推进，结束后关闭返回
            AntdUI.Modal.open(new AntdUI.Modal.Config(this, "设备初始化", _initWaiting, TType.None)
            {
                MaskClosable = false,
                BtnHeight = 0
            });

            // 初始化结束才进入运行态，此前拒绝历史查询。
            // 初始化完成的唯一判定来源
            GlobalCommData.MarkAppRunning();
        }

        /// <summary>初始化链结束：切回 UI 线程更新提示并关闭 Modal。</summary>
        /// <param name="success">是否成功。</param>
        private void OnInitCompleted(bool success)
        {
            if (_initWaiting == null || _initWaiting.IsDisposed) return;
            if (_initWaiting.InvokeRequired)
            {
                _initWaiting.BeginInvoke((Action)(() =>
                {
                    _initWaiting.SetResult(success);
                    _initWaiting.CloseModal();
                }));
                return;
            }
            _initWaiting.SetResult(success);
            _initWaiting.CloseModal();
        }
        /// <summary>关闭前询问确认</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">关闭事件参数</param>
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            bool isRunning = DeviceControlWork.Instance.IsWelding;
            string title = isRunning ? "设备运行中" : "确认关闭";
            string message = isRunning
                ? "设备正在运行中，确认强制关闭？"
                : "是否确认关闭程序？";
            TType type = isRunning ? TType.Error : TType.Info;

            if (AntdUI.Modal.open(new AntdUI.Modal.Config(this, title, message, type)) != DialogResult.OK)
            {
                e.Cancel = true;
            }
        }

        /// <summary>构建顶部 Segmented 导航 + Tabs 内容页，并建立双向联动（Segmented.SelectIndexChanged -> Tabs.SelectedIndexChanged）。</summary>
        void LoadNavAndPages()
        {
            WindowState = FormWindowState.Maximized;

            // 顶部 Segmented 导航项
            segmentedMain.Items.Clear();
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "主页面", IconSvg = "HomeOutlined" });
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "设备监控", IconSvg = "MonitorOutlined" });
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "系统参数设置", IconSvg = "SettingOutlined" });
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "数据追溯", IconSvg = "BarChartOutlined" });

            // 保存 SegmentedItem 引用，供权限显隐使用
            _hiddenItem1 = segmentedMain.Items[1] as AntdUI.SegmentedItem;
            _hiddenItem2 = segmentedMain.Items[2] as AntdUI.SegmentedItem;
            _hiddenItem3 = segmentedMain.Items[3] as AntdUI.SegmentedItem;

            // Tab 0: 主页面
            AntdUI.TabPage tabMain = new AntdUI.TabPage { Text = "主页面", Tag = "main" };
            mainPage.Dock = DockStyle.Fill;
            tabMain.Controls.Add(mainPage);
            tabsMain.AddTabSelect(tabMain);

            // 初始化图表（轮廓线 + 4 条实时曲线）并订阅轮廓事件
            mainPage.AddContourLineChart();

            // Tab 1: 设备监控
            AntdUI.TabPage tabMonitor = new AntdUI.TabPage { Text = "设备监控", Tag = "monitor" };
            deviceMonitorPage.Dock = DockStyle.Fill;
            tabMonitor.Controls.Add(deviceMonitorPage);
            tabsMain.AddTabSelect(tabMonitor);

            // Tab 2: 系统参数设置
            AntdUI.TabPage tabParam = new AntdUI.TabPage { Text = "系统参数设置", Tag = "param" };
            navPageParam.Dock = DockStyle.Fill;
            tabParam.Controls.Add(navPageParam);
            tabsMain.AddTabSelect(tabParam);

            // Tab 3: 数据追溯
            AntdUI.TabPage tabData = new AntdUI.TabPage { Text = "数据追溯", Tag = "data" };
            dataPage.Dock = DockStyle.Fill;
            tabData.Controls.Add(dataPage);
            tabsMain.AddTabSelect(tabData);

            // 重入守卫：防止 segmented<->tab 事件回环
            bool syncing = false;

            // Segmented 选中 -> 切换 Tab
            segmentedMain.SelectIndexChanged += (s, e) =>
            {
                if (syncing) return;
                syncing = true;
                try
                {
                    tabsMain.SelectedIndex = segmentedMain.SelectIndex;
                }
                finally { syncing = false; }
                // 显式激活追溯页：不依赖 TabPage 是否切换子控件可见性；页面自带"宿主已显示+首次"闸门
                if (segmentedMain.SelectIndex == 3) dataPage.EnsureLoaded();
            };

            // Tab 切换 -> 反写 Segmented 选中
            tabsMain.SelectedIndexChanged += (s, e) =>
            {
                if (syncing) return;
                int idx = tabsMain.SelectedIndex;
                if (idx >= 0 && idx < segmentedMain.Items.Count)
                {
                    syncing = true;
                    try { segmentedMain.SelectIndex = idx; }
                    finally { syncing = false; }
                }
                // 同上：直接点 Tab 进入追溯页时也显式激活一次
                if (idx == 3) dataPage.EnsureLoaded();
            };

            segmentedMain.SelectIndex = 0;
            tabsMain.SelectedIndex = 0;
        }

        /// <summary>线激光状态变更处理</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">状态变更参数</param>
        private void OnWorkflowStateChanged(object sender, WeldStatusChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnWorkflowStateChanged(sender, e)));
                return;
            }
            UpdateStatusLabel(e.NewStatus);
        }

        /// <summary>更新线激光状态文本</summary>
        /// <param name="state">线激光工作流状态</param>
        private void UpdateStatusLabel(SubDeviceWeldStatus state)
        {
            string statusText;
            switch (state)
            {
                case SubDeviceWeldStatus.NoReset: statusText = "未复位"; break;
                case SubDeviceWeldStatus.Standby: statusText = "待机"; break;
                // 进入流程后未停止或异常即视为流程中
                case SubDeviceWeldStatus.PreWork:
                case SubDeviceWeldStatus.Working:
                case SubDeviceWeldStatus.Stopping:
                    statusText = "工作流程中"; break;
                case SubDeviceWeldStatus.ErrorAborted: statusText = "异常终止"; break;
                case SubDeviceWeldStatus.ManualStopped: statusText = "手动停止"; break;
                default: statusText = "未知"; break;
            }
            lbl_Status.Text = statusText;
        }

        void ShowStatus()
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action(ShowStatus));
            }
            else
            {
                OperateLevelControl();
            }
        }

        void OperateLevelControl()
        {
            lbl_SystemTime.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            lbl_OperateLevel.Text = "权限：" + PermissionLevel.GetDisplayName(GlobalCommData.mOperateLevel);

            if (PermissionLevel.IsEngineerOrAbove(GlobalCommData.mOperateLevel))
            {
                ShowTreeNode();
            }
            else
            {
                HideTreeNode();
            }
        }

        /// <summary>权限=操作员：移除工程/数据导航项（保留主页面）。</summary>
        void HideTreeNode()
        {
            if (HidePage) return;
            // 导航项引用在 LoadNavAndPages 中捕获，未构建完成时不做隐藏
            if (_hiddenItem1 == null || _hiddenItem2 == null || _hiddenItem3 == null) return;
            segmentedMain.Items.Remove(_hiddenItem1);
            segmentedMain.Items.Remove(_hiddenItem2);
            segmentedMain.Items.Remove(_hiddenItem3);
            HidePage = true;
            if (segmentedMain.SelectIndex > 0) segmentedMain.SelectIndex = 0;
        }

        /// <summary>权限=工程师/管理员：恢复全部导航项可见。</summary>
        void ShowTreeNode()
        {
            if (HidePage)
            {
                segmentedMain.Items.Add(_hiddenItem1);
                segmentedMain.Items.Add(_hiddenItem2);
                segmentedMain.Items.Add(_hiddenItem3);
                HidePage = false;
            }
        }

        /// <summary>权限等级变更（登录/注销）回调：导航项显隐 + 状态栏</summary>

        /// <param name="sender">事件源（GlobalCommData）。</param>
        /// <param name="level">新的权限等级。</param>
        private void OnOperateLevelChanged(object sender, OperateLevel level)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnOperateLevelChanged(sender, level)));
                return;
            }
            OperateLevelControl();
        }

        private void uiMillisecondTimer1_Tick(object sender, EventArgs e)
        {
            ShowStatus();

            // 连接状态由快捷开关呈现，状态栏只留整机状态
            mainPage?.RefreshDeviceSwitchUi();
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 退订工作流状态事件：设计器不会覆盖静态订阅
            LineLaserWorkflow.Instance.WeldStatusChanged -= OnWorkflowStateChanged;
            GlobalCommData.OperateLevelChanged -= OnOperateLevelChanged;

            DeviceControlWork.Instance.Dispose();

            // 释放英莱相机（LineLaserWorkflow.Dispose 不会被外部调用，此处显式释放）
            try { LineLaserWorkflow.Instance.Dispose(); } catch { }   // 退出路径：释放失败不影响进程结束

            // 释放运控工作流：关闭台达主站（Master_Reset + Master_Close）
            try { MotionControlWorkflow.Instance.Dispose(); } catch { }

            // 释放监控相机工作流：停监督并释放图像缓冲
            try { MonitorCameraWorkflow.Instance.Dispose(); } catch { }

            // 释放管理器持有的 SDK 句柄
            try { MonitorCamManager.Instance.Dispose(); } catch { }
            try { LineLaserManager.Instance.Dispose(); } catch { }
            try { GlobalCommData.mCommunicationManager.Dispose(); } catch { }

        }

        private void buttonSZ_Click(object sender, EventArgs e)
        {
            using (LoginForm mLogin = new LoginForm(this))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this, "登录界面", mLogin, TType.None)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }
        #endregion
    }
}

