using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MotionControl;
using AntdUI;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub2UI
{
    /// <summary>
    /// 运控调整参数页（UIPage，接入 NavPage（参数界面）工艺配置节点）：
    ///   1) 标定数据参数：线激光前置标定距离 / 线激光-水平轴角度 / 标定系数 / 标定中心 / 趋近阈值
    ///   2) 模拟流程参数：模拟焊缝轮廓类型 / 随机模式 / 模拟模式(A/B/C) / 起点 / 速度 / 目标距离（由 MainDeviceWorkflow 统一管理，开启后贯穿三套流程）
    ///   3) 运动轴参数：待完成，可空留
    /// </summary>
    public partial class MotionControlPage : UserControl
    {
        public MotionControlPage()
        {
            InitializeComponent();
            PopulateComboBoxItems();
            LoadFromModel();

            // 页面销毁时退订线激光状态事件（设计器 Dispose(bool) 不覆盖单例事件订阅）
            this.HandleDestroyed += MotionControlPage_HandleDestroyed;
        }

        private void MotionControlPage_HandleDestroyed(object sender, EventArgs e)
        {
            UnsubscribeLineLaserEvents();
        }

        #region 模拟流程启动状态（挂起 + 事件驱动）

        /// <summary>线激光初始化完成(转 Standby)后是否需自动 Start 的挂起标记</summary>
        private bool _pendingSimStart;

        /// <summary>模拟流程是否处于异常终止态（按钮显示红色「清理异常」）</summary>
        private bool _simError;

        /// <summary>按钮默认样式（用于异常红态复位）</summary>
        private AntdUI.TTypeMini _btnDefaultType = AntdUI.TTypeMini.Primary;

        /// <summary>模拟流程按钮三态</summary>
        private enum SimButtonMode
        {
            /// <summary>空闲：开启模拟流程</summary>
            Start,
            /// <summary>运行中：停止</summary>
            Stop,
            /// <summary>异常终止：清理异常（红色）</summary>
            Cleanup
        }

        #endregion

        #region 下拉项填充

        private void PopulateComboBoxItems()
        {
            cmbProfile.Items.Add("V型坡口");
            cmbProfile.Items.Add("U型坡口");
            cmbProfile.Items.Add("平直焊缝");
            cmbProfile.Items.Add("随机演化");

            cmbSimMode.Items.Add("关闭(真实流程)");
            cmbSimMode.Items.Add("A:虚拟+计算+记录(不运动)");
            cmbSimMode.Items.Add("B:虚拟+计算+记录+运动");
            cmbSimMode.Items.Add("C:真实相机+计算+记录(不运动)");
        }

        #endregion

        #region 数据加载与保存

        private void LoadFromModel()
        {
            var mc = MoveControlData.Instance;
            txtFrontOffset.Text = mc.LaserFrontOffset.ToString("F3");
            txtAngle.Text = mc.LaserToHorizontalAngleDeg.ToString("F3");
            txtScaleX.Text = mc.LaserToHorizontalScaleX.ToString("F3");
            txtCenterX.Text = mc.CalibratedCenterX.ToString("F3");
            txtCenterY.Text = mc.CalibratedCenterY.ToString("F3");
            txtApproach.Text = mc.ApproachThreshold.ToString("F3");

            var sc = MainDeviceWorkflow.Instance;
            txtStartX.Text = sc.SimStartX.ToString("F3");
            txtSpeed.Text = sc.SimSpeed.ToString("F3");
            txtTargetDist.Text = sc.SimTargetDistance.ToString("F3");

            int modeIdx = (int)sc.SelectedMode;
            if (modeIdx >= 0 && modeIdx < cmbSimMode.Items.Count)
                cmbSimMode.SelectedIndex = modeIdx;

            // 轮廓/随机读取自模拟模式控制器（启动期已由 MainDeviceWorkflow.LoadSimulation 恢复）
            int profileIdx = (int)sc.SeamProfile;
            if (profileIdx >= 0 && profileIdx < cmbProfile.Items.Count)
                cmbProfile.SelectedIndex = profileIdx;
            else
                cmbProfile.SelectedIndex = 0;
            chkRandom.Checked = sc.RandomMode;
        }

        private static double ParseDouble(AntdUI.Input box, double fallback)
        {
            double v;
            if (double.TryParse(box.Text, out v)) return v;
            return fallback;
        }

        private void BtnSaveCalib_Click(object sender, EventArgs e)
        {
            var mc = MoveControlData.Instance;
            mc.LaserFrontOffset = ParseDouble(txtFrontOffset, mc.LaserFrontOffset);
            mc.LaserToHorizontalAngleDeg = ParseDouble(txtAngle, mc.LaserToHorizontalAngleDeg);
            mc.LaserToHorizontalScaleX = ParseDouble(txtScaleX, mc.LaserToHorizontalScaleX);
            mc.CalibratedCenterX = ParseDouble(txtCenterX, mc.CalibratedCenterX);
            mc.CalibratedCenterY = ParseDouble(txtCenterY, mc.CalibratedCenterY);
            mc.ApproachThreshold = ParseDouble(txtApproach, mc.ApproachThreshold);
            mc.Save();
            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "标定参数已保存");
        }

        private void BtnSaveSim_Click(object sender, EventArgs e)
        {
            SyncSimParamsToController();
            MainDeviceWorkflow.Instance.SaveSimulation();
            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "模拟参数已保存");
        }

        /// <summary>将界面模拟参数同步到 MainDeviceWorkflow 模拟模式控制器（不含保存提示和持久化；模式由 EnableSimulation 设置）</summary>
        private void SyncSimParamsToController()
        {
            var sc = MainDeviceWorkflow.Instance;
            sc.SimStartX = ParseDouble(txtStartX, sc.SimStartX);
            sc.SimSpeed = ParseDouble(txtSpeed, sc.SimSpeed);
            sc.SimTargetDistance = ParseDouble(txtTargetDist, sc.SimTargetDistance);

            if (cmbProfile.SelectedIndex >= 0)
                sc.SeamProfile = (SimSeamProfile)cmbProfile.SelectedIndex;
            sc.RandomMode = chkRandom.Checked;

            if (cmbSimMode.SelectedIndex >= 0)
                sc.SelectedMode = (SimTestMode)cmbSimMode.SelectedIndex;
        }

        private void BtnStartSim_Click(object sender, EventArgs e)
        {
            var sc = MainDeviceWorkflow.Instance;

            // 异常清理态：点击「清理异常」→ 复位工作流与界面，回到「开启模拟流程」（不自动重开）
            // 兜底：即使 _simError 标志因异步事件错位而未置位，只要工作流处于异常终止态也进入清理分支
            if (_simError || LineLaserWorkflow.Instance.Step == LineLaserWorkflowState.ErrorAborted)
            {
                CleanupSimError();
                return;
            }

            if (sc.IsSimulationEnabled)
            {
                // 关闭模拟：先退订事件避免 Stop 触发 StateChanged 异步回调二次进入停止路径
                UnsubscribeLineLaserEvents();
                ClearPendingSimStart(); // 取消可能挂起的初始化后再启动
                StopSimulationFlows();
                sc.DisableSimulation();
                ShowSimStopMessage("用户停止指令");
                LogSimStopSummary("用户停止指令");
                SetSimButtonMode(SimButtonMode.Start);
                return;
            }

            // 读取界面参数并校验模式
            SyncSimParamsToController();
            if (cmbSimMode.SelectedIndex < 0)
            {
                AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, "请先选择模拟模式（A/B/C）");
                return;
            }
            SimTestMode mode = (SimTestMode)cmbSimMode.SelectedIndex;
            if (mode == SimTestMode.None)
            {
                AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, "请先选择模拟模式（A/B/C），当前为关闭状态");
                return;
            }

            // 开启模拟模式：选相机 + 启动共享 robotX 时钟
            sc.EnableSimulation(mode);

            // 订阅线激光状态变更（贯穿整个模拟流程，用于检测异常终止 / 焊缝完成）
            SubscribeLineLaserEvents();

            // 若工作流处于终止态（完成/异常/手动停止），先复位到可启动状态
            var llState = LineLaserWorkflow.Instance.Step;
            if (llState != LineLaserWorkflowState.Uninitialized
                && llState != LineLaserWorkflowState.Standby)
            {
                try { LineLaserWorkflow.Instance.Reset(); } catch (Exception ex)
                {
                    ShowSimErrorDialog("启动前复位工作流失败: " + ex.Message);
                }
            }

            // 一键开启三套流程（线激光用虚拟相机/模拟robotX跑通流水线；运控读取共享robotX；监控判为模拟不工作）
            // 线激光 Initialize 为后台线程异步：未初始化时订阅 StateChanged，待转 Standby 后再 Start，
            // 规避同步判断 Standby 误跳过 Start 导致首次点击停在待机的问题
            // 任一步骤异常已由 ShowSimErrorDialog 将按钮转红色「清理异常」，故以 _simError 短路后续启动步骤
            if (!_simError) StartLineLaserWithPending();

            if (!_simError)
            {
                try { MotionControlWorkflow.Instance.Start(); } catch (Exception ex)
                {
                    ShowSimErrorDialog(string.Format("V3跟踪工作流启动失败: {0}", ex.Message));
                }
            }
            // 健康巡检已集成到 MonitorCameraWorkflow 工作循环中，无需单独启动

            // 启动成功才置「停止」；若发生异常（_simError 已置位，按钮已红）则不覆盖
            if (!_simError) SetSimButtonMode(SimButtonMode.Stop);
        }

        /// <summary>关闭模拟时反向停止三套流程（逐包 try/catch，忽略非运行态的异常）</summary>
        private void StopSimulationFlows()
        {
            try { LineLaserWorkflow.Instance.Stop(); } catch { }
            try { MotionControlWorkflow.Instance.Stop(); } catch { }
            // 健康巡检已集成到 MonitorCameraWorkflow 中，无需单独停止
        }

        /// <summary>
        /// 开启线激光流程（事件驱动，规避异步初始化竞争）：
        ///   - 若线激光为 Uninitialized：订阅 StateChanged，置挂起标记，调用 Initialize（后台线程转 Standby）；
        ///     待 StateChanged 上报 Standby 时回到 UI 线程调用 Start。
        ///   - 若线激光已为 Standby：直接同步 Start（二次开启场景）。
        /// </summary>
        private void StartLineLaserWithPending()
        {
            var ll = LineLaserWorkflow.Instance;
            try
            {
                if (ll.Step == LineLaserWorkflowState.Uninitialized)
                {
                    _pendingSimStart = true;
                    ll.Initialize();
                }
                else if (ll.Step == LineLaserWorkflowState.Standby)
                {
                    ll.Start();
                }
            }
            catch (Exception ex)
            {
                ShowSimErrorDialog("线激光流程启动失败: " + ex.Message);
                ClearPendingSimStart();
            }
        }

        /// <summary>
        /// 线激光状态变更回调（由后台初始化线程触发）：
        ///   - 转 Standby 且存在挂起标记：回到 UI 线程启动工作流，并清理挂起标记/订阅；
        ///   - 转 ErrorAborted：清理挂起标记/订阅，避免后台初始化完成误触发 Start。
        /// </summary>
        private void OnLineLaserStateChanged(object sender, WorkflowStepChangedEventArgs<LineLaserWorkflowState> e)
        {
            if (e.NewState == LineLaserWorkflowState.Standby)
            {
                if (_pendingSimStart)
                {
                    if (this.InvokeRequired)
                        this.BeginInvoke(new Action(StartPendingLineLaser));
                    else
                        StartPendingLineLaser();
                }
            }
            else if (e.NewState == LineLaserWorkflowState.ErrorAborted)
            {
                // 模拟流程中发生异常终止：清理挂起 + 弹窗（需确认 Modal）+ 按钮转红色「清理异常」，等待用户清理
                ClearPendingSimStart();
                string errMsg = string.Format("模拟流程异常终止：{0}", string.IsNullOrEmpty(e.Reason) ? "未知原因" : e.Reason);
                if (this.InvokeRequired)
                    this.BeginInvoke(new Action<string>(m => ShowSimErrorDialog(m)), errMsg);
                else
                    ShowSimErrorDialog(errMsg);
            }
            else if (e.NewState == LineLaserWorkflowState.Stopping
                && e.Reason != null && e.Reason.Contains("焊缝行程完成"))
            {
                // 焊缝行程完成（正常停止）：联动停止整段模拟并复位按钮
                ClearPendingSimStart();
                if (this.InvokeRequired)
                    this.BeginInvoke(new Action(StopSimOnWeldComplete));
                else
                    StopSimOnWeldComplete();
            }
        }

        /// <summary>回到 UI 线程启动线激光工作流（挂起场景），并清理挂起状态</summary>
        private void StartPendingLineLaser()
        {
            try
            {
                // 守卫：用户可能在初始化期间点击停止（DisableSimulation），此时放弃启动避免误进入流程
                if (!MainDeviceWorkflow.Instance.IsSimulationEnabled)
                    return;
                LineLaserWorkflow.Instance.Start();
            }
            catch (Exception ex)
            {
                ShowSimErrorDialog("线激光流程启动失败(挂起): " + ex.Message);
            }
            finally
            {
                ClearPendingSimStart();
            }
        }

        /// <summary>清理挂起标记（不再在此退订 StateChanged：状态监听现已贯穿整个模拟流程生命周期，
        /// 由 UnsubscribeLineLaserEvents 在停止/清理/焊缝完成时显式退订）</summary>
        private void ClearPendingSimStart()
        {
            _pendingSimStart = false;
        }

        /// <summary>模拟异常统一提示（需用户确认）：记录日志 + 在 UI 线程弹出 Modal 错误对话框 + 按钮转红色「清理异常」。
        /// 后台线程调用时自动 InvokeRequired 封送（BeginInvoke 异步，不阻塞工作流线程，见 [[lessons/WinForms-UI-Threading-Guide]]）。</summary>
        private void ShowSimErrorDialog(string msg)
        {
            GlobalCommData.ShowLog("模拟流程", msg, MessageLevel.Error);
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(m =>
                {
                    AntdUI.Modal.open(new AntdUI.Modal.Config(System.Windows.Forms.Form.ActiveForm, "模拟流程异常", m, AntdUI.TType.Error) { Draggable = true });
                    ShowSimErrorButton();
                }), msg);
            }
            else
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(System.Windows.Forms.Form.ActiveForm, "模拟流程异常", msg, AntdUI.TType.Error) { Draggable = true });
                ShowSimErrorButton();
            }
        }

        /// <summary>正常停止提示（无需确认）：记录日志 + 在 UI 线程弹出 Message 提示。区别于异常 Modal（需确认）。</summary>
        private void ShowSimStopMessage(string reason)
        {
            string tip = string.Format("模拟测试已停止：{0}", reason);
            GlobalCommData.ShowLog("模拟流程", tip, MessageLevel.Info);
            if (this.InvokeRequired)
                this.BeginInvoke(new Action<string>(m => AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, m)), tip);
            else
                AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, tip);
        }

        /// <summary>停止原因确认信息：汇总各流程/设备最终状态，给出「人话」式停止原因（区别于底层「状态迁移」调试日志）。</summary>
        private void LogSimStopSummary(string reason)
        {
            var ll = LineLaserWorkflow.Instance.Step;
            var v3 = MotionControlWorkflow.Instance.Step;
            var mon = MonitorCameraWorkflow.Instance.Step;
            var sim = MainDeviceWorkflow.Instance;
            string simState = sim.IsSimulationEnabled ? string.Format("运行中({0})", sim.CurrentMode) : "已关闭";
            string summary = string.Format(
                "模拟测试已停止 | 原因：{0} | 线激光：{1} | V3跟踪：{2} | 监控：{3} | 模拟模式：{4}",
                reason, ll, v3, mon, simState);
            GlobalCommData.ShowLog("模拟流程", summary, MessageLevel.Info);
        }

        /// <summary>订阅线激光状态变更（贯穿整个模拟流程生命周期）</summary>
        private void SubscribeLineLaserEvents()
        {
            LineLaserWorkflow.Instance.StateChanged += OnLineLaserStateChanged;
        }

        /// <summary>取消线激光状态变更订阅</summary>
        private void UnsubscribeLineLaserEvents()
        {
            LineLaserWorkflow.Instance.StateChanged -= OnLineLaserStateChanged;
        }

        /// <summary>设置模拟流程按钮三态：开启 / 停止 / 清理异常（红色背景）。
        /// 运行中：Ghost=false + Type=Error → 红色实心背景；停止/空闲：Ghost=true + Type=Primary → 恢复初始幽灵按钮样式。</summary>
        private void SetSimButtonMode(SimButtonMode mode)
        {
            switch (mode)
            {
                case SimButtonMode.Stop:
                    btnStartSim.Text = "停止";
                    btnStartSim.Ghost = false;          // 实心背景
                    btnStartSim.Type = AntdUI.TTypeMini.Error;
                    _simError = false;
                    break;
                case SimButtonMode.Cleanup:
                    btnStartSim.Text = "清理异常";
                    btnStartSim.Ghost = false;          // 实心背景
                    btnStartSim.Type = AntdUI.TTypeMini.Error;
                    _simError = true;
                    break;
                case SimButtonMode.Start:
                default:
                    btnStartSim.Text = "开启模拟流程";
                    btnStartSim.Ghost = true;           // 恢复初始幽灵按钮样式
                    btnStartSim.Type = _btnDefaultType; // 恢复初始颜色
                    _simError = false;
                    break;
            }
        }

        /// <summary>异常终止时按钮转红色「清理异常」</summary>
        private void ShowSimErrorButton()
        {
            SetSimButtonMode(SimButtonMode.Cleanup);
        }

        /// <summary>焊缝正常完成：停止整段模拟流程（含运控/监控）并复位按钮</summary>
        private void StopSimOnWeldComplete()
        {
            UnsubscribeLineLaserEvents();
            StopSimulationFlows();
            try { LineLaserWorkflow.Instance.Reset(); } catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "焊缝完成后复位工作流失败: " + ex.Message, MessageLevel.Error);
            }
            ClearPendingSimStart();
            MainDeviceWorkflow.Instance.DisableSimulation();
            ShowSimStopMessage("焊缝行程完成");
            LogSimStopSummary("焊缝行程完成");
            SetSimButtonMode(SimButtonMode.Start);
        }

        /// <summary>清理异常并复位：停止全部流程、复位线激光工作流到可启动状态、复位按钮为「开启模拟流程」。
        /// 关键修复：将按钮复位放入 finally，确保无论后端（Reset/Disable）是否抛异常，按钮必定恢复默认态，
        /// 解决「点击清理异常后无反应 / 不恢复」问题；各后端调用各自独立 try/catch 记录，互不阻断。</summary>
        private void CleanupSimError()
        {
            UnsubscribeLineLaserEvents();
            ClearPendingSimStart();
            try
            {
                StopSimulationFlows();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清理异常-停止流程失败: " + ex.Message, MessageLevel.Error);
            }
            try
            {
                LineLaserWorkflow.Instance.Reset();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清理异常-复位工作流失败: " + ex.Message, MessageLevel.Error);
            }
            try
            {
                MainDeviceWorkflow.Instance.DisableSimulation();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清理异常-关闭模拟失败: " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                // 无论上述后端调用是否抛异常，按钮都必须恢复默认态
                SetSimButtonMode(SimButtonMode.Start);
                ShowSimStopMessage("异常清理恢复");
                LogSimStopSummary("异常清理恢复");
            }
        }

        #endregion
    }
}
