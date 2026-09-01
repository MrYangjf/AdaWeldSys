using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.Api;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.LineLaserCamApi;
using AdaWeldSystem.Sub1UI;
using AdaWeldSystem.Sub2UI;
using AntdUI;
using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace AdaWeldSystem
{
    /// <summary>
    /// 主窗口
    /// </summary>
    public partial class FormMain : Window
    {
        #region 私有变量
        // 子页面
        MainPage mainPage;
        NavPage navPageParam;
        NavPage navPageImage;
        DataReviewPage dataPage;

        bool HidePage = false;
        // 存储被隐藏的 SegmentedItem 引用，用于恢复显示
        AntdUI.SegmentedItem _hiddenItem1, _hiddenItem2, _hiddenItem3;

        // V3：整机状态标签（动态添加到状态栏，显示整设备聚合状态）
        private System.Windows.Forms.ToolStripLabel lbl_SystemStatus;
        private System.Windows.Forms.ToolStripSeparator sep_SystemStatus;
        #endregion

        #region 构造函数
        public FormMain()
        {
            InitializeComponent();

            // 设计器模式下不执行运行时初始化，避免触发单例和硬件/配置文件依赖
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            // ── 配置加载 ──
            MoveControlData.Instance.Load();          // 运控标定参数
            MainDeviceWorkflow.Instance.LoadSimulation();     // 模拟模式参数
            MonitorAlgorithmManager.Instance.Load();  // 监控相机算法参数

            // ── 全局 UI 配置（AntdUI 主题 + 文本渲染） ──
            GlobalCommData.UIConfigSetting();

            // ── 子页面实例化 ──
            mainPage = new MainPage();
            navPageParam = new NavPage();
            navPageImage = new NavPage();
            dataPage = new DataReviewPage();

            // 加载子页配置到 NavPage（全量加载）
            navPageParam.LoadTabs(
                ("工艺设置", new ParamConfigPage()),
                ("运控设置", new MotionControlPage()),
                ("通讯设置", new CommPage()),
                ("文件设置", new FileEditorPage())
            );

            navPageImage.LoadTabs(
                ("英莱相机设置", new ILCamPage(), "FundOutlined"),
                ("监控相机设置", new MonitorCamPage(), "VideoCameraOutlined")
            );

            LoadNavAndPages();

            // ── 事件订阅 ──
            LineLaserWorkflow.Instance.StateChanged += OnWorkflowStateChanged;
            UpdateStatusLabel(LineLaserWorkflow.Instance.Step);

            // V3：动态添加整机状态标签到状态栏
            InitializeSystemStatusLabel();

            // 关闭前询问
            this.FormClosing += FormMain_FormClosing;

            // ── 设备初始化 ──
            // 子设备初始化
            MainDeviceWorkflow.Instance.PowerOnInitialize();
            // 主线程启动，待机监听
            MainDeviceWorkflow.Instance.Start();

            GlobalCommData.ShowLog("FormMain", "初始化完成");
        }
        #endregion

        #region 私有函数
        /// <summary>
        /// 关闭前询问确认
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">关闭事件参数</param>
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            bool isRunning = MainDeviceWorkflow.Instance.IsWelding;
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

        /// <summary>
        /// 构建顶部 Segmented 导航 + Tabs 内容页，并建立双向联动（Segmented.SelectIndexChanged -> Tabs.SelectedIndexChanged）。
        /// </summary>
        void LoadNavAndPages()
        {
            WindowState = FormWindowState.Maximized;

            // 顶部 Segmented 导航项
            segmentedMain.Items.Clear();
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "主页面", IconSvg = "HomeOutlined" });
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "参数设置", IconSvg = "SettingOutlined" });
            segmentedMain.Items.Add(new AntdUI.SegmentedItem { Text = "相机设置", IconSvg = "PictureOutlined" });
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

            // 初始化主页面图表（轮廓线 + 4条实时数据曲线）+ 订阅英莱相机轮廓事件
            mainPage.AddContourLineChart();
            mainPage.AddNewDataLineChart();

            // Tab 1: 参数设置
            AntdUI.TabPage tabParam = new AntdUI.TabPage { Text = "参数设置", Tag = "param" };
            navPageParam.Dock = DockStyle.Fill;
            tabParam.Controls.Add(navPageParam);
            tabsMain.AddTabSelect(tabParam);

            // Tab 2: 相机设置
            AntdUI.TabPage tabImage = new AntdUI.TabPage { Text = "相机设置", Tag = "image" };
            navPageImage.Dock = DockStyle.Fill;
            tabImage.Controls.Add(navPageImage);
            tabsMain.AddTabSelect(tabImage);

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
            };

            segmentedMain.SelectIndex = 0;
            tabsMain.SelectedIndex = 0;
        }

        /// <summary>
        /// 初始化整机状态标签
        /// </summary>
        private void InitializeSystemStatusLabel()
        {
            lbl_SystemStatus = new System.Windows.Forms.ToolStripLabel();
            lbl_SystemStatus.Name = "lbl_SystemStatus";
            lbl_SystemStatus.Text = "整机：初始化中";
            lbl_SystemStatus.ForeColor = System.Drawing.Color.Orange;
            lbl_SystemStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold);

            sep_SystemStatus = new System.Windows.Forms.ToolStripSeparator();
            sep_SystemStatus.Name = "sep_SystemStatus";

            // 整机状态置顶（最左），其后为流程状态、系统时间
            toolStrip1.Items.Insert(0, sep_SystemStatus);
            toolStrip1.Items.Insert(0, lbl_SystemStatus);

            // 订阅整机流程态切换（ADR-042 R3：统一走基类 StateChanged）
            MainDeviceWorkflow.Instance.StateChanged += OnMainDeviceStepChanged;
            UpdateSystemStatusLabel(MainDeviceWorkflow.Instance.FlowState);
        }

        /// <summary>
        /// 整机流程态变更回调
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">流程态变更参数</param>
        private void OnMainDeviceStepChanged(object sender, WorkflowStepChangedEventArgs<MainDeviceFlowState> e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMainDeviceStepChanged(sender, e)));
                return;
            }
            UpdateSystemStatusLabel(e.NewState);
        }

        /// <summary>
        /// 更新整机状态标签（展示整机流程态 MainDeviceFlowState）
        /// </summary>
        /// <param name="state">整机流程态</param>
        private void UpdateSystemStatusLabel(MainDeviceFlowState state)
        {
            string text;
            System.Drawing.Color color;
            switch (state)
            {
                case MainDeviceFlowState.Idle:
                    text = "整机：空闲";
                    color = System.Drawing.Color.Gray;
                    break;
                case MainDeviceFlowState.SafePose:
                    text = "整机：安全起始位";
                    color = System.Drawing.Color.Green;
                    break;
                case MainDeviceFlowState.Preworking:
                    text = "整机：焊前准备";
                    color = System.Drawing.Color.Orange;
                    break;
                case MainDeviceFlowState.PreWeldPose:
                    text = "整机：焊前位";
                    color = System.Drawing.Color.Orange;
                    break;
                case MainDeviceFlowState.Working:
                    text = "整机：焊接中";
                    color = System.Drawing.Color.Green;
                    break;
                case MainDeviceFlowState.Stopping:
                    text = "整机：停止中";
                    color = System.Drawing.Color.Orange;
                    break;
                case MainDeviceFlowState.Stopped:
                    text = "整机：已停止";
                    color = System.Drawing.Color.Gray;
                    break;
                case MainDeviceFlowState.Alarm:
                    text = "整机：报警";
                    color = System.Drawing.Color.Red;
                    break;
                default:
                    text = "整机：" + state.ToString();
                    color = System.Drawing.Color.Gray;
                    break;
            }
            lbl_SystemStatus.Text = text;
            lbl_SystemStatus.ForeColor = color;
        }

        /// <summary>
        /// 线激光状态变更处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">状态变更参数</param>
        private void OnWorkflowStateChanged(object sender, WorkflowStepChangedEventArgs<LineLaserWorkflowState> e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnWorkflowStateChanged(sender, e)));
                return;
            }
            UpdateStatusLabel(e.NewState);
        }

        /// <summary>
        /// 更新线激光状态文本
        /// </summary>
        /// <param name="state">线激光工作流状态</param>
        private void UpdateStatusLabel(LineLaserWorkflowState state)
        {
            string statusText;
            switch (state)
            {
                case LineLaserWorkflowState.Uninitialized: statusText = "未初始化"; break;
                case LineLaserWorkflowState.Initializing: statusText = "初始化中"; break;
                case LineLaserWorkflowState.Standby: statusText = "待机"; break;
                // 进入工作流程后，只要未手动停止/异常终止，均视为「工作流程中」
                case LineLaserWorkflowState.PreWork:
                case LineLaserWorkflowState.Starting:
                case LineLaserWorkflowState.Working:
                case LineLaserWorkflowState.Stopping:
                    statusText = "工作流程中"; break;
                case LineLaserWorkflowState.ErrorAborted: statusText = "异常终止"; break;
                case LineLaserWorkflowState.ManualStopped: statusText = "手动停止"; break;
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
            // 操作级别标签已从状态栏移除，仅保留系统时间刷新（权限驱动导航显隐逻辑保持不变）
            lbl_SystemTime.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

            switch (GlobalCommData.mOperateLevel)
            {
                case OperateLevel.Admin:
                case OperateLevel.Engineer:
                    ShowTreeNode();
                    break;
                case OperateLevel.Operator:
                    break;
            }
        }

        /// <summary>
        /// 权限=工程师/管理员：恢复全部导航项可见。
        /// </summary>
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

        private void uiMillisecondTimer1_Tick(object sender, EventArgs e)
        {
            ShowStatus();

            // 设备硬件连接状态由 MainPage 面板内快捷开关按钮（ButtonShadow 常驻控件）呈现，
            // 状态栏仅保留整机状态/流程状态/系统时间
            if (mainPage != null)
                mainPage.RefreshDeviceSwitchUi();
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 退订工作流状态事件（设计器 Dispose(bool) 不覆盖静态/单例事件订阅，须显式退订）
            LineLaserWorkflow.Instance.StateChanged -= OnWorkflowStateChanged;

            MainDeviceWorkflow.Instance.StateChanged -= OnMainDeviceStepChanged;

            MainDeviceWorkflow.Instance.Dispose();

            // 释放英莱相机（LineLaserWorkflow.Dispose 不会被外部调用，此处显式释放）
            try { LineLaserWorkflow.Instance.Dispose(); } catch { }

        }

        private void buttonSZ_Click(object sender, EventArgs e)
        {
            using (FormLogin mLogin = new FormLogin(this))
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
