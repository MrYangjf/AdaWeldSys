﻿using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AntdUI;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.EmguALG.Core;
using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.UIDesign.SubDialog.IntelligentLaserDialog;

namespace AdaWeldSystem.UIDesign.TabPage
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

            // 事件订阅放代码后置，避免设计器序列化丢失
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

            // 订阅焊接过程态，用 WeldStatus 判自动/空闲
            LineLaserWorkflow.Instance.WeldStatusChanged += OnWorkflowStateChanged;

            // 切到英莱相机：不持久化，仅本会话生效
            LineLaserManager.Instance.SelectIntelligentLaser();
            _ilCamera = LineLaserManager.Instance.IntelligentLaser;

            // 初始化 IP 输入框：显示本地配置保存的 IP
            txtIP.Text = LineLaserManager.Instance.Config.SensorIp;

            // 统一刷新入口：连接态 + 模式
            RefreshUi(RefreshUiScope.Connection | RefreshUiScope.Mode);
            // 订阅 Mat 更新，显示由管理器处理
            LineLaserManager.Instance.MatUpdated += OnMatUpdated;
            // 订阅 Job 变更，同步主界面下拉
            if (_ilCamera != null) _ilCamera.JobParamsChanged += OnIlJobParamsChanged;
            // 订阅「线激光」设备态切换 StateSwitched，统一驱动刷新状态标签
            LineLaserWorkflow.Instance.StateSwitched += OnIlStateChanged;
            // 初始化结束刷新连接按钮
            DeviceControlWork.Instance.InitializationCompleted += OnInitializationCompleted;
            // 已连接则先同步一次硬件状态到界面
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

            // 自动运行时不绘制，结果交主页面
            if (IsWorkflowRunning(LineLaserWorkflow.Instance.WeldStatus)) return;

            // 手动模式由 swDisplay 控制是否绘制
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
            old?.Dispose();
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
            // 无结果也显示状态码，用于定位原因
            rows.Add(new InspectResultRow { Field = "状态", Value = r.Valid ? "有识别结果" : "无识别结果", Unit = "-" });
            rows.Add(new InspectResultRow { Field = "ParseRes", Value = r.ParseRes.ToString(), Unit = "-" });
            rows.Add(new InspectResultRow { Field = "ErrorCode", Value = r.ErrorCode.ToString(), Unit = "-" });
            // 几何与测量值仅在有结果时显示
            if (r.Valid)
            {
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
            if (!TryPassPageAction("手动连接线激光")) return;
            // 界面按钮属手动动作，显式置手动模式
            LineLaserWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            // 连接态以工作流为准
            bool connected = LineLaserWorkflow.Instance.State == SubDeviceState.Connected;
            if (connected)
            {
                // 断开统一经工作流后台线程承载
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
                // 先落 IP 到配置，连接线程按配置地址执行
                LineLaserManager.Instance.Config.SensorIp = ip;
                LineLaserManager.Instance.SaveConfig();
                if (!LineLaserWorkflow.Instance.ConnectOn())
                {
                    AntdUI.Message.info(this.FindForm(), "线激光连接正在进行中，请稍候...");
                }
            }
            // 刷新连接态与模式
            RefreshUi(RefreshUiScope.Connection | RefreshUiScope.Mode);
        }

        /// <summary>页面动作门控：三档拒绝。</summary>
        /// <param name="action">动作描述，用于提示与日志。</param>
        /// <returns>true 表示允许执行。</returns>
        private bool TryPassPageAction(string action)
        {
            if (DeviceControlWork.Instance.IsShuttingDown)
            {
                AntdUI.Message.warn(this.FindForm(), "程序正在关闭，暂不支持" + action + "。");
                LogPageWarning("页面动作被拒绝 " + action + " 原因 正在关闭");
                return false;
            }
            if (DeviceControlWork.Instance.IsInitializing)
            {
                AntdUI.Message.warn(this.FindForm(), "设备初始化中仅可查询，" + action + " 已拒绝。");
                LogPageWarning("页面动作被拒绝 " + action + " 原因 初始化中");
                return false;
            }
            if (!PermissionLevel.IsEngineerOrAbove(GlobalCommData.mOperateLevel))
            {
                string msg = "当前权限为" + PermissionLevel.GetDisplayName(GlobalCommData.mOperateLevel)
                           + "，无权" + action + "。该操作需工程师及以上权限。";
                AntdUI.Message.warn(this.FindForm(), msg);
                LogPageWarning("页面动作被拒绝 " + action + " 原因 权限不足 当前等级 " + GlobalCommData.mOperateLevel);
                return false;
            }
            return true;
        }

        private static void LogPageWarning(string message)
        {
            GlobalCommData.ShowLog("ILCamPage", message, MessageLevel.Warning);
        }

        private void SyncFromHardwareCore()
        {
            if (_ilCamera == null) return;

            // 从硬件同步一次开关状态到本地
            _ilCamera.SyncHardwareStatus();
            UpdateLaserSensorUi();

            // 2. 从相机端读取 Job 和帧率
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
            // 启停传感器用 SetSensor，回调随之启停；SetCaptureCamera 不启动传感器，不用
            _ilCamera.SetSensor(!_ilCamera.IsCameraOn());
            UpdateLaserSensorUi();
            // 传感器开启时禁用帧率切换，关闭时恢复
            UpdateFpsDropdownState();
        }

        private void btnAlgoEdit_Click(object sender, EventArgs e)
        {
            // 英莱分支下 AlgorithmEditForm 会挂载 ILAlgoPage（JOB 参数编辑），PipelineConfig 仅作占位
            using (ILAlgorithmEditForm dlg = new ILAlgorithmEditForm(this.FindForm() as AntdUI.Window, AlgorithmManager.Instance.CreateEmptyPipelineConfig()))
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
            // 打开相机参数编辑页（可读改存 Job 参数）
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
            // 下拉选择不立即切换，点「应用」才确认；此处仅按「与设备是否一致」更新按钮可用性
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
            // 切 JOB 后同步下拉选中项与应用按钮态
            RefreshJobSelect();
            UpdateApplyButtonsState();
        }

        private void btnApplyJob_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            int idx = cmbJob.SelectedIndex;
            if (idx < 0 || idx >= cmbJob.Items.Count) return;
            if (!(cmbJob.Items[idx] is CmbJobItem item)) return;
            int currentId = _ilCamera.FetchCurrentJobId();
            if (currentId == item.JobId) return;
            int ret = _ilCamera.SwitchJob(item.JobId);
            if (ret == 0)
            {
                GlobalCommData.ShowLog("ILCamPage", string.Format("已切换到 JOB {0}", item.JobId), MessageLevel.Info);
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("JOB {0} 切换失败, {1}", item.JobId,
                    LineLaserManager.Instance.DescribeLastError(LineLaserErrorContext.ReturnCode)));
            }
            // 成功失败都刷新下拉与应用按钮态
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
            // 下拉选择不立即切换，点「应用」才确认；此处仅更新应用按钮可用性
            UpdateApplyButtonsState();
        }

        private void btnApplyFps_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            int idx = cmbFps.SelectedIndex;
            if (idx < 0 || idx >= 3) return;
            // 相机开启时禁止切换帧率（SDK 报错）
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
            // 成功失败都刷新并更新按钮状态
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

            // 帧率按钮：选中模式与设备实际不同则启用
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
            // 状态变更：刷新模式与连接态
            RefreshUi(RefreshUiScope.Mode | RefreshUiScope.Connection);
        }

        private void swDisplay_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            RefreshDisplay();
        }

        /// <summary>初始化结束回调。</summary>
        /// <param name="success">初始化是否成功。</param>
        private void OnInitializationCompleted(bool success)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnInitializationCompleted(success)));
                return;
            }
            RefreshUi(RefreshUiScope.Connection | RefreshUiScope.Mode);
        }

        private void UpdateModeUiCore()
        {
            bool running = IsWorkflowRunning(LineLaserWorkflow.Instance.WeldStatus);

            // 手动/空闲开 Mat 生成，自动运行时关闭；生成由管理器承担，本页只切开关
            LineLaserManager.Instance.SetDisplayOptions(!running, LineLaserManager.Instance.DisplayFps, true);

            // 标签由 Connection 分支刷新，本函数只管模式
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
                // 初始化/关闭中锁定连接
                bool blocked = DeviceControlWork.Instance.IsInitializing || DeviceControlWork.Instance.IsShuttingDown;
                btnConnect.Enabled = !blocked;
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
            bool blocked = DeviceControlWork.Instance.IsInitializing || DeviceControlWork.Instance.IsShuttingDown;

            if (!connected && blocked)
            {
                // 初始化期只查询
                alert_ConnStatus.Text = "初始化中，连接已锁定";
                alert_ConnStatus.TextColor = Color.FromArgb(200, 120, 0);
            }
            else
            {
                alert_ConnStatus.Text = connected ? "已连接" : "未连接";
                alert_ConnStatus.TextColor = connected ? Color.FromArgb(0, 128, 0) : Color.FromArgb(192, 0, 0);
            }
            btnConnect.Text = connected ? "断开" : "连接";
            btnConnect.IconSvg = connected ? "DisconnectOutlined" : "ApiOutlined";
            txtIP.Enabled = !connected;
            if (!connected)
            {
                // 断开后按硬件真相刷新激光/传感器按钮
                UpdateLaserSensorUi();
                // 断开连接时清除 Job 和帧率模式的选择
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

            // Disconnected→Connected 时完整同步一次硬件状态到 UI（Job 列表、帧率、开关状态）
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
            // 传感器运行时禁止切帧率（SDK 会报错）
            cmbFps.Enabled = !_ilCamera.IsCameraOn();
        }

        private void ILCamPage_HandleDestroyed(object sender, EventArgs e)
        {
            // 取消订阅（避免事件源持有已销毁页面引用）
            swDisplay.CheckedChanged -= swDisplay_CheckedChanged;
            LineLaserWorkflow.Instance.WeldStatusChanged -= OnWorkflowStateChanged;
            LineLaserWorkflow.Instance.StateSwitched -= OnIlStateChanged;
            DeviceControlWork.Instance.InitializationCompleted -= OnInitializationCompleted;
            LineLaserManager.Instance.MatUpdated -= OnMatUpdated;
            if (_ilCamera != null)
            {
                _ilCamera.JobParamsChanged -= OnIlJobParamsChanged;
            }

            _displayedClone?.Dispose();
            _displayedClone = null;

            // 页面释放不断设备
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
