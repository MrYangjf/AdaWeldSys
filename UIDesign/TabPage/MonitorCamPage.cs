using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.Api;
using Emgu.CV;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub2UI
{
    /// <summary>
    /// 监控相机页面 - 监控画面显示与算法调试
    /// 提供：实时预览、焊前对中检测、焊中质量检测、算法参数编辑。
    /// 订阅 MonitorCameraRun 实时帧（图像归相机）与 MonitorCameraWorkflow 基类 StateChanged（流程态归流程）。
    /// 检测结果在流程态转 Completed 后由本页向流程只读属性拉取（权责边界）。
    /// 所有事件处理均检查 InvokeRequired 以跨线程安全刷新（ADR / Lessons）。
    /// 算法参数内联于右侧面板，由 MonitorAlgorithmManager 单例持久化（ADR-014）。
    /// </summary>
    public partial class MonitorCamPage : UserControl
    {
        private Timer _statusTimer;
        private bool _isMonitoring = false;
        private bool _userStopPending = false;

        public MonitorCamPage()
        {
            InitializeComponent();
            InitializePhaseSelect();
            InitializeAlgorithmControls();
            SubscribeEvents();
            StartStatusTimer();
            this.HandleDestroyed += MonitorCamPage_HandleDestroyed;
        }

        private void MonitorCamPage_HandleDestroyed(object sender, EventArgs e)
        {
            DisposeComponents();
        }

        /// <summary>
        /// 资源释放（ADR-001：使用 DisposeComponents 公共方法，不在 .cs 中重写 Dispose(bool)）
        /// </summary>
        public void DisposeComponents()
        {
            UnsubscribeEvents();
            StopStatusTimer();
            try { MonitorCameraRun.Instance.SensorStop(); } catch { }
            try { MonitorCameraWorkflow.Instance.Stop(); } catch { }
        }

        #region 初始化

        private void InitializePhaseSelect()
        {
            cmbPhase.Items.Clear();
            cmbPhase.Items.Add("焊前对中检测");
            cmbPhase.Items.Add("焊中质量检测");
            cmbPhase.SelectedIndex = 0;
        }

        /// <summary>
        /// 从 MonitorAlgorithmManager 单例填充右侧算法参数框。
        /// 单例 Load() 已在 FormMain 启动期调用，此处仅读取（参见 Lessons/StartupSingletonInitialization）。
        /// </summary>
        private void InitializeAlgorithmControls()
        {
            var mgr = MonitorAlgorithmManager.Instance;
            txtTolX.Text = mgr.AlignmentConfig.ToleranceX.ToString("F1");
            txtTolY.Text = mgr.AlignmentConfig.ToleranceY.ToString("F1");
            txtTolAngle.Text = mgr.AlignmentConfig.ToleranceAngle.ToString("F1");
            txtPassScore.Text = mgr.QualityConfig.PassScore.ToString("F1");
        }

        private void SubscribeEvents()
        {
            // 图像由监控相机自身输出（权责边界 R1），流程态由基类统一通知（R3）
            MonitorCameraRun.FrameCompletedEvent += MonitorCameraRun_FrameCompletedEvent;
            MonitorCameraRun.Instance.StatusChanged += MonitorCameraRun_StatusChanged;
            MonitorCameraWorkflow.Instance.WeldStatusChanged += Workflow_StateChanged;
        }

        private void UnsubscribeEvents()
        {
            MonitorCameraRun.FrameCompletedEvent -= MonitorCameraRun_FrameCompletedEvent;
            MonitorCameraRun.Instance.StatusChanged -= MonitorCameraRun_StatusChanged;
            MonitorCameraWorkflow.Instance.WeldStatusChanged -= Workflow_StateChanged;
        }

        private void StartStatusTimer()
        {
            _statusTimer = new Timer();
            _statusTimer.Interval = 500;
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
        }

        private void StopStatusTimer()
        {
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                _statusTimer = null;
            }
        }

        #endregion

        #region 状态显示

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            string camState = "未连接";
            if (MonitorCameraRun.Instance.ConnectionState == MonitorCameraConnectionState.Connected)
                camState = MonitorCameraRun.Instance.IsLiveMode ? "监控实时中" : "已连接";

            MonitorSupervisionState health = MonitorCameraRun.Instance.SupervisionState;
            if (health != MonitorSupervisionState.Idle && health != MonitorSupervisionState.Healthy)
                camState = string.Format("{0}（{1}）", camState, GetHealthText(health));

            lblStatus.Text = string.Format("相机: {0}", camState);

            SubDeviceWeldStatus ws = MonitorCameraWorkflow.Instance.WeldStatus;
            if (ws == SubDeviceWeldStatus.Standby)
                uiLightStatus.Value = 1;            // 绿灯亮 (On)
            else if (ws == SubDeviceWeldStatus.ErrorAborted)
                uiLightStatus.Value = 5;            // 灭 (Off)
            else
            {
                uiLightStatus.Value = 1;
                uiLightStatus.Loading = true;       // 闪烁 (Blink)
            }

            // 连续监测（btnStartMonitor 以 continuous=true 启动）焊接过程态恒为 Working，
            // 完成信号不再经 WeldStatusChanged 透出，故此处按 500ms 节拍拉取最新结果刷新界面
            if (_isMonitoring)
                ShowResult();
        }

        #endregion

        #region 交互

        private bool EnsureReady()
        {
            if (MonitorCameraWorkflow.Instance.State == SubDeviceState.Disconnected)
                MonitorCameraWorkflow.Instance.ConnectOn();
            return MonitorCameraWorkflow.Instance.WeldStatus == SubDeviceWeldStatus.Standby;
        }

        private void btnSingleCheck_Click(object sender, EventArgs e)
        {
            if (!EnsureReady())
            {
                MessageBox.Show("监控相机初始化失败，请检查连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MonitorCameraWorkflow.Instance.Start(GetSelectedPhase(), false);
        }

        private void btnStartMonitor_Click(object sender, EventArgs e)
        {
            if (!EnsureReady())
            {
                MessageBox.Show("监控相机初始化失败，请检查连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _isMonitoring = true;
            MonitorCameraWorkflow.Instance.Start(GetSelectedPhase(), true);
        }

        private void btnStopMonitor_Click(object sender, EventArgs e)
        {
            MonitorCameraWorkflow.Instance.Stop();
            _userStopPending = true;
            _isMonitoring = false;
        }

        private void chkLive_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            try
            {
                if (chkLive.Checked)
                {
                    if (MonitorCameraRun.Instance.ConnectionState != MonitorCameraConnectionState.Connected)
                    {
                        string ip = MonitorCameraRun.Instance.Config != null ? MonitorCameraRun.Instance.Config.IpAddress : "192.168.1.100";
                        string port = MonitorCameraRun.Instance.Config != null ? MonitorCameraRun.Instance.Config.Port : "5000";
                        MonitorCameraRun.Instance.OpenSensor(ip, port);
                    }
                    MonitorCameraRun.Instance.ChangeMode(true);
                }
                else
                {
                    MonitorCameraRun.Instance.ChangeMode(false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("实时预览切换失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 保存右侧算法参数：写回 MonitorAlgorithmManager 单例并持久化到 INI。
        /// 解析失败时回退到原值，避免误清空配置。
        /// </summary>
        private void btnSaveAlgorithm_Click(object sender, EventArgs e)
        {
            try
            {
                var mgr = MonitorAlgorithmManager.Instance;
                mgr.AlignmentConfig.ToleranceX = ParseDouble(txtTolX.Text, mgr.AlignmentConfig.ToleranceX);
                mgr.AlignmentConfig.ToleranceY = ParseDouble(txtTolY.Text, mgr.AlignmentConfig.ToleranceY);
                mgr.AlignmentConfig.ToleranceAngle = ParseDouble(txtTolAngle.Text, mgr.AlignmentConfig.ToleranceAngle);
                mgr.QualityConfig.PassScore = ParseDouble(txtPassScore.Text, mgr.QualityConfig.PassScore);
                mgr.Save();
                MessageBox.Show("算法参数已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private double ParseDouble(string text, double fallback)
        {
            double v;
            if (double.TryParse(text, out v))
                return v;
            return fallback;
        }

        private MonitorCameraPhase GetSelectedPhase()
        {
            return cmbPhase.SelectedIndex == 1 ? MonitorCameraPhase.WeldQuality : MonitorCameraPhase.PreWeldAlignment;
        }

        #endregion

        #region 事件处理（跨线程）

        private void MonitorCameraRun_FrameCompletedEvent(object sender, MonitorCameraFrameCompletedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, MonitorCameraFrameCompletedEventArgs>(MonitorCameraRun_FrameCompletedEvent), sender, e);
                return;
            }
            if (e.Frame != null && !e.Frame.IsEmpty)
                imageBox1.Image = e.Frame;
        }

        /// <summary>相机健康状态变更：收敛到统一刷新入口 UpdateStatusDisplay（[[lessons/UiRefreshUnifiedEntry]]）</summary>
        private void MonitorCameraRun_StatusChanged(object sender, MonitorSupervisionStatusChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, MonitorSupervisionStatusChangedEventArgs>(MonitorCameraRun_StatusChanged), sender, e);
                return;
            }
            UpdateStatusDisplay();
        }

        /// <summary>
        /// 流程态变更：统一入口（权责边界 R3/R4）。
        /// 状态文本直接取流程态；结果数据由本页在 Completed 后向流程拉取只读属性。
        /// </summary>
        private void Workflow_StateChanged(object sender, WeldStatusChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, WeldStatusChangedEventArgs>(Workflow_StateChanged), sender, e);
                return;
            }

            lblStatus.Text = GetStateText(e.NewStatus);

            switch (e.NewStatus)
            {
                case SubDeviceWeldStatus.Standby:
                    // 单轮检测完成（Working → Standby）显示结果；手动停止由 _userStopPending 区分
                    if (_userStopPending)
                    {
                        txtResult.Text = "已停止";
                        _userStopPending = false;
                    }
                    else
                    {
                        ShowResult();
                    }
                    break;

                case SubDeviceWeldStatus.ErrorAborted:
                    txtResult.Text = string.Format("异常: {0}", e.Reason);
                    _isMonitoring = false;
                    break;
            }
        }

        /// <summary>从流程读取最近一轮检测结果并刷新界面（图像由流程持有的算法叠加图提供）</summary>
        private void ShowResult()
        {
            var wf = MonitorCameraWorkflow.Instance;
            Mat overlay = wf.LastOverlay;
            if (overlay != null && !overlay.IsEmpty)
                imageBox1.Image = overlay;

            MonitorResult r = wf.LastResult;
            txtResult.Text = string.Format("判定: {0}\r\n{1}", r.Pass ? "合格" : "不合格", r.Message);
        }

        private static string GetHealthText(MonitorSupervisionState health)
        {
            switch (health)
            {
                case MonitorSupervisionState.Checking: return "巡检中";
                case MonitorSupervisionState.Degraded: return "近期无采集";
                case MonitorSupervisionState.Error: return "采集异常";
                default: return "正常";
            }
        }

        private string GetStateText(SubDeviceWeldStatus state)
        {
            switch (state)
            {
                case SubDeviceWeldStatus.NoReset: return "未复位";
                case SubDeviceWeldStatus.Standby: return "待机";
                case SubDeviceWeldStatus.PreWork: return "焊接前准备";
                case SubDeviceWeldStatus.Working: return "工作中";
                case SubDeviceWeldStatus.Stopping: return "停止中";
                case SubDeviceWeldStatus.ErrorAborted: return "异常终止";
                case SubDeviceWeldStatus.ManualStopped: return "手动停止";
                default: return "未知";
            }
        }

        #endregion
    }
}
