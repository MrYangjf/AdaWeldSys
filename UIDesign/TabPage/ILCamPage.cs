using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.EmguALG;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.Sub3UI;
using AntdUI;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub2UI
{
    /// <summary>英莱线激光相机页面</summary>
    public partial class ILCamPage : UserControl
    {
        private IntelligentLaserCam _ilCamera;

        private Mat _displayedClone;

        public ILCamPage()
        {
            InitializeComponent();
            InitResultTable();

            // 事件订阅放代码后置（Designer 手写事件订阅会被设计器重新序列化时丢失）
            btnConnect.Click += btnConnect_Click;
            btnScan.Click += btnScan_Click;
            btnLaser.Click += btnLaser_Click;
            btnCaptureCamera.Click += btnCaptureCamera_Click;
            cmbJob.SelectedIndexChanged += cmbJob_SelectedIndexChanged;
            cmbFps.SelectedIndexChanged += cmbFps_SelectedIndexChanged;
            btnApplyJob.Click += btnApplyJob_Click;
            btnApplyFps.Click += btnApplyFps_Click;
            btnAlgoEdit.Click += btnAlgoEdit_Click;
            btnParamEdit.Click += btnParamEdit_Click;
            swDisplay.CheckedChanged += swDisplay_CheckedChanged;

            // 订阅线激光工作流焊接过程态（WeldStatusChanged），用 WeldStatus 判断自动/空闲，不新增 bool
            LineLaserWorkflow.Instance.WeldStatusChanged += OnWorkflowStateChanged;

            // 切换到英莱相机并获取实例（不持久化，仅当前会话生效）
            LineLaserManager.Instance.SelectIntelligentLaser();
            _ilCamera = LineLaserManager.Instance.IntelligentLaser;

            // 初始化 IP 输入框：显示本地配置保存的 IP（需求：初始化使用保存路径上的 IP）
            txtIP.Text = LineLaserManager.Instance.Config.SensorIp;

            // 统一刷新入口：连接态 + 模式（覆盖原 UpdateConnectionUi/UpdateModeUi 两处直调）
            RefreshUi(RefreshUiScope.Connection | RefreshUiScope.Mode);
            // 订阅线激光管理器的 Mat 更新事件（显示处理由管理器承担，ADR-039）
            LineLaserManager.Instance.MatUpdated += OnMatUpdated;
            // 订阅 Job 参数变更（切 JOB/改参数，可能来自 ILAlgoPage 算法编辑页或工作流）→ 同步主 UI 下拉
            if (_ilCamera != null) _ilCamera.JobParamsChanged += OnIlJobParamsChanged;
            // ADR-022：订阅「线激光」设备态切换 StateSwitched（设备状态外发），统一驱动刷新状态标签
            LineLaserWorkflow.Instance.StateSwitched += OnIlStateChanged;
            // 若相机已连接（如启动检测/子设备初始化已连接），直接同步一次硬件状态到 UI
            // （"第一次正确"：连接后本地状态与硬件一致，之后按钮点击本地生效）
            if (_ilCamera != null && _ilCamera.IsConnected)
            {
                RefreshUi(RefreshUiScope.Hardware);
            }
            this.HandleDestroyed += ILCamPage_HandleDestroyed;
        }

        [Flags]
        private enum RefreshUiScope
        {
            Connection = 1,
            Mode = 2,
            Hardware = 4
        }

        private void RefreshUi(RefreshUiScope scope)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => RefreshUi(scope)));
                return;
            }
            if ((scope & RefreshUiScope.Connection) != 0) UpdateConnectionUiCore();
            if ((scope & RefreshUiScope.Mode) != 0) UpdateModeUiCore();
            if ((scope & RefreshUiScope.Hardware) != 0) SyncFromHardwareCore();
        }
        // 结果表格行模型（与 ILAlgoPage 的 InspectResultRow 一致，统一展示格式）
        private class InspectResultRow
        {
            public string Field { get; set; }
            public string Value { get; set; }
            public string Unit { get; set; }
        }

        #region 显示刷新（回调驱动）

        private void OnMatUpdated(object sender, LineLaserMatEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMatUpdated(sender, e)));
                return;
            }
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (_ilCamera == null) return;

            // 状态数据（轮廓点 + 帧率）始终更新，不受模式限制
            lblContourPoint.Text = "轮廓点：" + _ilCamera.LastContourPointCount.ToString();
            // 传感器自身帧率（采集分析频率，来自 ScanRateHz）
            lblSenorfps.Text = string.Format("传感器帧率：{0:F1} Hz", _ilCamera.ScanRateHz);
            // 实测回调帧率（首帧已剔除，保留 1 位小数）
            lblMeasuredFps.Text = string.Format("回调帧率：{0:F1} Hz", _ilCamera.MeasuredFps);

            // 自动运行期间不在本页绘制，轮廓与结果由管理器分发给主页面 ScottPlot
            if (IsWorkflowRunning(LineLaserWorkflow.Instance.WeldStatus)) return;

            // 手动模式下 swDisplay 控制是否绘制 Mat（允许只看结果表不看画面）
            if (swDisplay.Checked)
            {
                ShowMat(_ilCamera.CurrentMat);
            }

            RefreshInspectResult();
        }

        private void ShowMat(Mat src)
        {
            if (src == null || src.IsEmpty) return;
            var clone = src.Clone();
            var old = _displayedClone;
            _displayedClone = clone;
            imageBox.Image = clone;
            if (old != null) old.Dispose();
        }

        private void InitResultTable()
        {
            dgvResult.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Field", "字段") { Width = "120" },
                new AntdUI.Column("Value", "值") { Width = "160" },
                new AntdUI.Column("Unit", "单位") { Width = "100" }
            };
            dgvResult.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            // 初始填充占位行
            var rows = new List<InspectResultRow>();
            rows.Add(new InspectResultRow { Field = "状态", Value = "-", Unit = "未连接" });
            dgvResult.Binding(new AntdUI.AntList<InspectResultRow>(rows));
        }

        private void RefreshInspectResult()
        {
            var rows = new List<InspectResultRow>();
            LineLaserSeamResult r = _ilCamera.LastInspectResult;
            if (!r.Valid)
            {
                rows.Add(new InspectResultRow { Field = "状态", Value = "-", Unit = "无识别结果" });
            }
            else
            {
                rows.Add(new InspectResultRow { Field = "ParseRes", Value = r.ParseRes.ToString(), Unit = "-" });
                rows.Add(new InspectResultRow { Field = "ErrorCode", Value = r.ErrorCode.ToString(), Unit = "-" });
                rows.Add(new InspectResultRow { Field = "FeatureY", Value = r.FeatureY.ToString("F3"), Unit = "mm" });
                rows.Add(new InspectResultRow { Field = "FeatureZ", Value = r.FeatureZ.ToString("F3"), Unit = "mm" });
                rows.Add(new InspectResultRow { Field = "Width0", Value = r.Width0.ToString("F3"), Unit = "mm" });
                rows.Add(new InspectResultRow { Field = "Width1", Value = r.Width1.ToString("F3"), Unit = "mm" });
                rows.Add(new InspectResultRow { Field = "Height0", Value = r.Height0.ToString("F3"), Unit = "mm" });
                rows.Add(new InspectResultRow { Field = "Height1", Value = r.Height1.ToString("F3"), Unit = "mm" });
                rows.Add(new InspectResultRow { Field = "Area", Value = r.Area.ToString("F3"), Unit = "mm2" });
            }
            dgvResult.Binding(new AntdUI.AntList<InspectResultRow>(rows));
        }

        #endregion

        #region 按钮事件

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null) return;
            // ADR-022：手动连接/断开驱动「线激光」工作流（英莱是线激光的厂商实例），
            // 不在此维护连接布尔；连接态由 LineLaserWorkflow 的 ConnectOn/ConnectOff 承载（内部自起后台线程，非阻塞）。
            if (_ilCamera.IsConnected)
            {
                _ilCamera.DisconnectManual();
                // 线激光设备态 → Disconnect（手动断开）
                LineLaserWorkflow.Instance.ConnectOff();
            }
            else
            {
                string ip = txtIP.Text == null ? string.Empty : txtIP.Text.Trim();
                if (string.IsNullOrEmpty(ip))
                {
                    AntdUI.Message.warn(this.FindForm(), "请输入相机IP！");
                    return;
                }
                bool ok = _ilCamera.ConnectManual(ip);
                // 连接结果 → Connect(连接就绪) / Disconnect(连接失败)
                if (ok)
                    LineLaserWorkflow.Instance.ConnectOn();
                else
                    LineLaserWorkflow.Instance.ConnectOff();

                if (ok)
                {
                    // 从相机端读取实际状态并刷新 UI（硬件状态检测 + Job + 帧率 + 传感器使能）
                    RefreshUi(RefreshUiScope.Hardware);
                }
            }
            // 统一刷新入口：连接态标签/按钮 + 模式可用性基于线激光状态机识别
            RefreshUi(RefreshUiScope.Connection | RefreshUiScope.Mode);
        }

        private void SyncFromHardwareCore()
        {
            if (_ilCamera == null) return;

            // 1. 从硬件同步一次激光/相机开关状态到本地（"第一次正确"），统一刷新激光/传感器按钮 text
            //    （之后 SetLaser/SetSensor 本地更新，UI 点击后立即生效，不再反复读硬件）
            _ilCamera.SyncHardwareStatus();
            UpdateLaserSensorUi();

            // 2. 需求2：从相机端读取 Job 和帧率
            RefreshJobSelect();
            RefreshFpsSelect();

            // 3. 同步按钮使能状态
            UpdateFpsDropdownState();
            UpdateApplyButtonsState();
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null) return;
            // 扫描网口下的英莱相机（SDK UDP 广播发现，阻塞执行）
            var sensors = _ilCamera.ScanSensors();
            if (sensors == null || sensors.Count == 0)
            {
                AntdUI.Message.warn(this.FindForm(), "未扫描到英莱相机，请检查网口连接！");
                return;
            }
            using (var dlg = new ILScanForm(this.FindForm() as AntdUI.Window, sensors))
            {
                if (AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "扫描相机", dlg, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                }) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedIp))
                {
                    // 选中后 IP 自动填充并保存到本地配置
                    txtIP.Text = dlg.SelectedIp;
                    LineLaserManager.Instance.Config.SensorIp = dlg.SelectedIp;
                    LineLaserManager.Instance.SaveConfig();
                }
            }
        }

        private void btnLaser_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            _ilCamera.SetLaser(!_ilCamera.IsLaserOn());
            UpdateLaserSensorUi();
        }

        private void btnCaptureCamera_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            // 线激光工作方式：SetSensor 才是启停传感器（回调由此开始/停止）；
            // SetCaptureCamera 仅上电采集相机、不启动传感器，非线激光工作方式，已舍弃。
            _ilCamera.SetSensor(!_ilCamera.IsCameraOn());
            UpdateLaserSensorUi();
            // 需求2：传感器开启时禁用帧率切换，关闭时恢复
            UpdateFpsDropdownState();
        }

        private void btnAlgoEdit_Click(object sender, EventArgs e)
        {
            // 英莱分支下 AlgorithmEditForm 会挂载 ILAlgoPage（JOB 参数编辑），PipelineConfig 仅作占位
            using (ILAlgorithmEditForm dlg = new ILAlgorithmEditForm(this.FindForm() as AntdUI.Window, new PipelineConfig()))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "算法编辑", dlg, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }

        private void btnParamEdit_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null) return;
            // 打开独立「相机参数编辑」页（AntdUI.Table 展示 Job Parameter，支持读取/修改/保存）
            using (var dlg = new ILParamForm(this.FindForm() as AntdUI.Window, _ilCamera))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "相机参数编辑", dlg, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }

        #endregion

        #region JOB 选择

        private void RefreshJobSelect()
        {
            cmbJob.Items.Clear();
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            List<IlJobDescInfo> jobs = _ilCamera.FetchJobList();
            if (jobs.Count == 0) return;
            int currentJobId = _ilCamera.FetchCurrentJobId();
            int selectIdx = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                IlJobDescInfo job = jobs[i];
                string typeName = _ilCamera.GetJointTypeName(job.JointType);
                string text = string.IsNullOrEmpty(typeName)
                    ? string.Format("JOB {0}", job.Id)
                    : string.Format("JOB {0} [{1}]", job.Id, typeName);
                cmbJob.Items.Add(new CmbJobItem { JobId = job.Id, Text = text });
                if (job.Id == currentJobId) selectIdx = i;
            }
            cmbJob.SelectedIndex = selectIdx;
        }

        private void cmbJob_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            // 需求2：下拉选择后不立即切换，点击"应用"按钮才确认切换
            // 此处仅更新应用按钮的可用状态：选中项与当前一致则禁用，不一致则启用
            UpdateApplyButtonsState();
        }

        private void OnIlJobParamsChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnIlJobParamsChanged(sender, e)));
                return;
            }
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            // 切 JOB 后同步 cmbJob 下拉选中项 + 应用按钮态（"job 切换同步到主 UI"）
            RefreshJobSelect();
            UpdateApplyButtonsState();
        }

        private void btnApplyJob_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            int idx = cmbJob.SelectedIndex;
            if (idx < 0 || idx >= cmbJob.Items.Count) return;
            var item = cmbJob.Items[idx] as CmbJobItem;
            if (item == null) return;
            int currentId = _ilCamera.FetchCurrentJobId();
            if (currentId == item.JobId) return;
            int ret = _ilCamera.SwitchJob(item.JobId);
            if (ret == 0)
            {
                GlobalCommData.ShowLog("ILCamPage", string.Format("已切换到 JOB {0}", item.JobId), MessageLevel.Info);
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("JOB {0} 切换失败, ret={1}", item.JobId, ret));
            }
            // 无论成功失败都刷新下拉 + 应用按钮态（失败时回滚显示）
            RefreshJobSelect();
            UpdateApplyButtonsState();
        }

        #endregion

        #region 帧率设置

        private void RefreshFpsSelect()
        {
            cmbFps.Items.Clear();
            cmbFps.Items.Add("标准 (STANDERD)");
            cmbFps.Items.Add("快速 (FAST_SPEED)");
            cmbFps.Items.Add("高速 (HIGH_SPEED)");

            // 从硬件读取当前帧率模式，精确匹配选中项
            if (_ilCamera != null && _ilCamera.IsConnected)
            {
                IlFpsModel model = _ilCamera.FetchFpsModel();
                cmbFps.SelectedIndex = (int)model - 1;
            }
            else
            {
                cmbFps.SelectedIndex = 0;
            }
        }

        private void cmbFps_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            // 需求2：下拉选择后不立即切换，点击"应用"按钮才确认切换
            // 此处仅更新应用按钮的可用状态
            UpdateApplyButtonsState();
        }

        private void btnApplyFps_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            int idx = cmbFps.SelectedIndex;
            if (idx < 0 || idx >= 3) return;
            // 相机开启时禁止切换帧率（SDK 报错），呼应 [[lessons/IntelligentLaser-Pitfalls]]
            if (_ilCamera.IsCameraOn())
            {
                AntdUI.Message.warn(this.FindForm(), "传感器运行时无法切换帧率，请先关闭传感器！");
                // 回滚下拉选中项
                RefreshFpsSelect();
                UpdateApplyButtonsState();
                return;
            }
            IlFpsModel model = (IlFpsModel)(idx + 1);
            int ret = _ilCamera.ApplyFrameRateModel(model);
            if (ret == 0)
            {
                GlobalCommData.ShowLog("ILCamPage",
                    string.Format("帧率模式已切换为 {0}", cmbFps.Items[idx]),
                    MessageLevel.Info);
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), "帧率模式切换失败！");
            }
            // 无论成功失败都刷新 + 更新按钮状态（失败时回滚显示）
            RefreshFpsSelect();
            UpdateApplyButtonsState();
        }

        private void UpdateApplyButtonsState()
        {
            if (_ilCamera == null || !_ilCamera.IsConnected)
            {
                btnApplyJob.Enabled = false;
                btnApplyFps.Enabled = false;
                return;
            }

            // JOB 应用按钮：当前选中的 JOB ID 与设备实际 JOB ID 不同时启用
            int jobIdx = cmbJob.SelectedIndex;
            bool jobChanged = false;
            if (jobIdx >= 0 && jobIdx < cmbJob.Items.Count && cmbJob.Items[jobIdx] is CmbJobItem item)
            {
                int currentJobId = _ilCamera.FetchCurrentJobId();
                jobChanged = (currentJobId >= 0 && item.JobId != currentJobId);
            }
            btnApplyJob.Enabled = jobChanged;

            // 帧率应用按钮：当前选中的模式与设备实际模式不同时启用
            int fpsIdx = cmbFps.SelectedIndex;
            bool fpsChanged = false;
            if (fpsIdx >= 0 && fpsIdx < 3)
            {
                IlFpsModel selectedModel = (IlFpsModel)(fpsIdx + 1);
                IlFpsModel currentModel = _ilCamera.FetchFpsModel();
                fpsChanged = (selectedModel != currentModel);
            }
            btnApplyFps.Enabled = fpsChanged;
        }

        #endregion

        #region UI 状态

        private static bool IsWorkflowRunning(SubDeviceWeldStatus state)
        {
            switch (state)
            {
                case SubDeviceWeldStatus.PreWork:
                case SubDeviceWeldStatus.Working:
                case SubDeviceWeldStatus.Stopping:
                    return true;
                default:
                    return false;
            }
        }

        private void OnWorkflowStateChanged(object sender, WeldStatusChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnWorkflowStateChanged(sender, e)));
                return;
            }
            // 工作流状态变更：模式可用性 + 连接态标签统一刷新
            RefreshUi(RefreshUiScope.Mode | RefreshUiScope.Connection);
        }

        private void swDisplay_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            RefreshDisplay();
        }

        private void UpdateModeUiCore()
        {
            bool running = IsWorkflowRunning(LineLaserWorkflow.Instance.WeldStatus);

            // 设置显示处理：手动/空闲模式下按需开启轮廓 Mat 生成，自动运行时关闭以节省 CPU/GPU。
            // 生成动作由 LineLaserManager 承担（ADR-039），本页只切换开关。
            LineLaserManager.Instance.SetDisplayOptions(!running, LineLaserManager.Instance.DisplayFps, true);

            // ADR-022：状态标签统一由 Connection 分支刷新（基于状态机识别），本函数只管模式可用性。
            if (running)
            {
                btnConnect.Enabled = false;
                btnScan.Enabled = false;
                btnLaser.Enabled = false;
                btnCaptureCamera.Enabled = false;
                cmbJob.Enabled = false;
                cmbFps.Enabled = false;
                btnApplyJob.Enabled = false;
                btnApplyFps.Enabled = false;
                btnAlgoEdit.Enabled = false;
                btnParamEdit.Enabled = false;
                txtIP.Enabled = false;
                swDisplay.Enabled = false;
            }
            else
            {
                // 空闲时恢复手动按钮（连接态相关按钮由 Connection 分支覆盖）
                btnConnect.Enabled = true;
                btnScan.Enabled = true;
                cmbJob.Enabled = true;
                // cmbFps 使能由 UpdateFpsDropdownState 管理（需考虑传感器是否运行）
                UpdateFpsDropdownState();
                btnAlgoEdit.Enabled = true;
                btnParamEdit.Enabled = true;
                swDisplay.Enabled = true;
            }
        }

        private void UpdateConnectionUiCore()
        {
            // 设备真相源：相机实例是否已连接
            bool connected = _ilCamera != null && _ilCamera.IsConnected;

            alert_ConnStatus.Text = connected ? "已连接" : "未连接";
            alert_ConnStatus.TextColor = connected ? Color.FromArgb(0, 128, 0) : Color.FromArgb(192, 0, 0);
            btnConnect.Text = connected ? "断开" : "连接";
            btnConnect.IconSvg = connected ? "DisconnectOutlined" : "ApiOutlined";
            txtIP.Enabled = !connected;
            if (!connected)
            {
                // 断开后统一刷新激光/传感器按钮 text（基于硬件真相源，此时均关闭）
                UpdateLaserSensorUi();
                // 需求2：断开连接时清除 Job 和帧率模式的选择
                cmbJob.Items.Clear();
                cmbJob.SelectedIndex = -1;
                cmbFps.SelectedIndex = -1;
            }
            btnLaser.Enabled = connected;
            btnCaptureCamera.Enabled = connected;
            cmbJob.Enabled = connected;
            UpdateFpsDropdownState();
            UpdateApplyButtonsState();
        }

        private void UpdateLaserSensorUi()
        {
            if (_ilCamera == null) return;
            bool laserOn = _ilCamera.IsLaserOn();
            bool sensorOn = _ilCamera.IsCameraOn();
            btnLaser.Text = laserOn ? "关闭激光" : "开启激光";
            btnLaser.ExtraMouseDown = laserOn;
            btnCaptureCamera.Text = sensorOn ? "关闭传感器" : "开启传感器";
            btnCaptureCamera.ExtraMouseDown = sensorOn;
        }

        private void OnIlStateChanged(object sender, SubDeviceStateChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnIlStateChanged(sender, e)));
                return;
            }
            // 设备态变更：连接态标签/按钮统一刷新
            RefreshUi(RefreshUiScope.Connection);

            // 初始化完成（从 Disconnect 进入 Connect）时，完整同步硬件状态到 UI
            // （Job 列表、帧率选择、激光/相机开关状态等），解决「初始化完 UI 仍不同步」问题
            if (e.OldState == SubDeviceState.Disconnected
                && e.NewState == SubDeviceState.Connected)
            {
                RefreshUi(RefreshUiScope.Hardware);
            }
        }

        private void UpdateFpsDropdownState()
        {
            if (_ilCamera == null || !_ilCamera.IsConnected)
            {
                cmbFps.Enabled = false;
                return;
            }
            // 传感器运行时禁止切换帧率（SDK 限制：采集运行时切帧率会报错）
            cmbFps.Enabled = !_ilCamera.IsCameraOn();
        }

        private void ILCamPage_HandleDestroyed(object sender, EventArgs e)
        {
            // 取消订阅（避免事件源持有已销毁页面引用）
            swDisplay.CheckedChanged -= swDisplay_CheckedChanged;
            LineLaserWorkflow.Instance.WeldStatusChanged -= OnWorkflowStateChanged;
            LineLaserWorkflow.Instance.StateSwitched -= OnIlStateChanged;
            LineLaserManager.Instance.MatUpdated -= OnMatUpdated;
            if (_ilCamera != null)
            {
                _ilCamera.JobParamsChanged -= OnIlJobParamsChanged;
            }

            if (_displayedClone != null)
            {
                _displayedClone.Dispose();
                _displayedClone = null;
            }

            // 页面销毁时断开相机（ADR-001：资源释放走此回调，不重写 Dispose(bool)）；
            // 同步线激光设备态 → Disconnect（仅当处于已连接/工作态，避免重复断开）
            if (_ilCamera != null && _ilCamera.IsConnected)
            {
                _ilCamera.DisconnectManual();
                SubDeviceState st = LineLaserWorkflow.Instance.State;
                if (st == SubDeviceState.Connected)
                {
                    LineLaserWorkflow.Instance.ConnectOff();
                }
            }
        }

        #endregion

        #region 嵌套类型

        private class CmbJobItem
        {
            public int JobId;
            public string Text;
            public override string ToString() { return Text; }
        }

        #endregion

    }
}
