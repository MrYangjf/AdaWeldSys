using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.SQLiteDBControl.Helpers;
using AdaWeldSystem.UIDesign.SubDialog.ParamContolDialog;
using AdaWeldSystem.WeldParamControl;
using AntdUI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using MotionControlWorkflow = AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow;
using LaserWeldHeadWorkflow = AdaWeldSystem.MainDeviceControl.DeviceWorkflow.LaserWeldHeadWorkflow;

namespace AdaWeldSystem.UIDesign.TabPage
{
    /// <summary>工艺设置页：调试/自适应/追踪/对中四区。</summary>
    public partial class ProcessControlPage : UserControl
    {
        #region 私有变量

        private string _paramIdName;

        /// <summary>回填期间抑制勾选副作用，避免误下发指令</summary>
        private bool _loadingProcessEnable;

        /// <summary>指令区 Tooltip 组件（单实例；HandleDestroyed 时释放）</summary>
        private AntdUI.TooltipComponent _tooltip;

        private void ProcessControlPage_HandleDestroyed(object sender, EventArgs e)
        {
            AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.Instance.StateSwitched -=
                OnMotionStateSwitched;
            AdaWeldSystem.MainDeviceControl.DeviceControlWork.Instance.InitializationCompleted -=
                OnMachineInitializationCompleted;
            MotionControlWorkflowInstance().AxisReadyChanged -= OnAxisReadyChanged;
            LaserWeldHeadWorkflowInstance().AxisReadyChanged -= OnAxisReadyChanged;
            MotionControlWorkflowInstance().TaZeroPoseStageChanged -= OnTaZeroPoseStageChanged;
            MontionManager.Instance.CancelAllAxisInstructions("工艺设置页销毁/切页");
            timerRefresh?.Stop();
            _tooltip?.Dispose();
            _tooltip = null;
        }

        /// <summary>启动本页刷新节拍（Designer 托管的 timerRefresh；页内唯一实例）。</summary>
        private void StartActionRefresh()
        {
            timerRefresh.Start();
        }

        /// <summary>刷新节拍：只刷新本页显示信息（TA Break 停止位）。</summary>
        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (!Visible) return;
            MotionControlWorkflow flow = MotionControlWorkflowInstance();
            if (flow == null) return;
            SetText(txtTaBreakPos, flow.TaBreakHoldValid
                ? flow.TaBreakHoldMm.ToString("F3", CultureInfo.InvariantCulture)
                : "--");
        }

        /// <summary>运控连机/断开时实时同步三个指令区可用性</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnMotionStateSwitched(object sender,
            AdaWeldSystem.MainDeviceControl.DeviceStateApiCore.SubDeviceStateChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMotionStateSwitched(sender, e)));
                return;
            }
            RefreshActionStates();
        }

        /// <summary>轴就绪事件（后台线程）</summary>
        private void OnAxisReadyChanged()
        {
            PostToUi(RefreshActionStates);
        }

        /// <summary>TA_ZeroPose 阶段变化</summary>
        private void OnTaZeroPoseStageChanged()
        {
            PostToUi(ApplyTaZeroPoseStage);
        }

        /// <summary>后台事件封送到 UI 线程执行</summary>

        /// <param name="action">UI 线程动作</param>
        private void PostToUi(Action action)
        {
            if (action == null) return;
            if (IsDisposed || Disposing) return;
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing) return;
                    action();
                }));
            }
            catch (Exception)
            {
                // 句柄已释放：忽略
            }
        }

        /// <summary>同步 TA_ZeroPose 阶段到选中态与底色。</summary>
        private void ApplyTaZeroPoseStage()
        {
            MotionControlWorkflow flow = MotionControlWorkflowInstance();
            bool running = flow.IsTaZeroPoseRunning;
            bool done = flow.IsTaZeroPoseDone;
            bool attempted = flow.IsTaZeroPoseAttempted;
            // 跑过没有与结果如何分两维：失败必须呈红
            if (!running && !done && !attempted) return;

            // 自动回零不抢选中态与底色
            // 避免选中态与手动指令矛盾
            // 仅用户选中时才落结果色
            if (radioQuickTaZeroPose.Checked)
            {
                if (running) ApplyActionRunning(radioQuickTaZeroPose);
                else if (done) ApplyActionBack(radioQuickTaZeroPose, true);
                else ApplyActionBack(radioQuickTaZeroPose, false);
            }
            RefreshActionStates();
        }

        /// <summary>页面首次可见兜底</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void ProcessControlPage_VisibleChanged(object sender, EventArgs e)
        {
            if (!Visible) return;
            if (IsDisposed || Disposing) return;
            RefreshActionStates();
        }

        /// <summary>整机初始化完成后主动回填一次三区可用性</summary>
        /// <param name="success">是否成功。</param>
        private void OnMachineInitializationCompleted(bool success)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnMachineInitializationCompleted), success);
                return;
            }
            EnsureTaParamFilled();   // 晚到时序：页面先构造、配置后就绪时在此补一次 TA 初值
            RefreshActionStates();
            ApplyTaZeroPoseStage();  // 初始化内部已自动跑过 ZeroPose：补一次显示同步（⛔ 不抢用户选中态）
        }

        /// <summary>三个指令区统一 Tooltip：只讲按钮作用</summary>
        private void InitCommandTooltips()
        {
            _tooltip = new AntdUI.TooltipComponent();
            _tooltip.SetTip(radioQuickYAuto, "Y_AutoTracking：跟随模式，Y 轴响应线激光目标值（默认）");
            _tooltip.SetTip(radioQuickYForceCalibPose, "Y_ForceCalibPose：Y 轴移到示教 Y 锁定坐标并保持");
            _tooltip.SetTip(radioQuickYForceHome, "Y_ForceHome：Y 轴强制回零");
            _tooltip.SetTip(radioQuickLaAuto, "LA_Auto：按目标光斑自动跟随");
            _tooltip.SetTip(radioQuickLaSpot1, "LA_Spot1：移动到光斑位 1");
            _tooltip.SetTip(radioQuickLaSpot2, "LA_Spot2：移动到光斑位 2");
            _tooltip.SetTip(radioQuickLaPose1, "LA_Pose1：移动到姿态位 1");
            _tooltip.SetTip(radioQuickLaDefaultFocus, "LA_DefaultFocus：移动到 LA 默认焦平面（LA 默认工作位）");
            _tooltip.SetTip(radioQuickLaGalvoCircle, "GA_Circle：振镜图形=圆");
            _tooltip.SetTip(radioQuickLaGalvoEight, "GA_Eight：振镜图形=八字");
            _tooltip.SetTip(radioQuickLaGalvoLine, "GA_Line：振镜图形=直线");
            _tooltip.SetTip(radioQuickLaGalvoArc, "GA_Arc：振镜图形=圆弧");
            _tooltip.SetTip(radioQuickTaAuto, "TA_Auto：按设定下压力自动压紧");
            _tooltip.SetTip(radioQuickTaBreak, "TA_Break：锁止在 Break 停止位");
            _tooltip.SetTip(radioQuickTaRetract, "TA_Retract：力矩反向出力（与 TA_Auto 方向相反），保持出力不定位");
            _tooltip.SetTip(radioQuickTaPose1, "TA_Pose1：移动到姿态位 1");
            _tooltip.SetTip(radioQuickTaPose2, "TA_Pose2：移动到姿态位 2");
            _tooltip.SetTip(radioQuickTaZeroPose, "TA_ZeroPose：走 TA 工作零位（绝对定位，非回零）");
        }

        #endregion

        #region 构造函数

        public ProcessControlPage()
        {
            InitializeComponent();
            InitConfigListColumns();   // Columns 必须后置（Designer 铁律），先于任何行绑定
            InitCommandTooltips();     // 指令 Radio 只用 XXX_XXX 文本，语义全部放入 Tooltip
            WireTaInstructionClicks(); // TA 指令派发挂 Click：已选中再点不触发 CheckedChanged
            PrepareActionRadioBackColor();   // 关闭 AllPaintingInWmPaint，让 BackColor 真正可见（⛔ 不用 Fill）
            LoadConfigList();
            LoadAutoParam();
            LoadAlgoConfigOnForm();
            EnsureTaParamFilled();       // 配置未就绪时置空，由 InitializationCompleted 补一次
            LoadProcessEnableOnForm();
            ApplyDefaultCommandModes();   // 三个指令区默认回到各自的 Auto 项（加载期不下发指令）
            StartActionRefresh();      // 本页定时器只做显示信息刷新；按钮管控与状态判定走事件驱动
            AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.Instance.StateSwitched +=
                OnMotionStateSwitched;   // 运控连机/断开实时同步指令区可用性
            AdaWeldSystem.MainDeviceControl.DeviceControlWork.Instance.InitializationCompleted +=
                OnMachineInitializationCompleted;   // 初始化完成后主动首填，避免页面先于 Init 构造导致陈旧置灰
            MotionControlWorkflowInstance().AxisReadyChanged += OnAxisReadyChanged;   // Y/TA 就绪即时刷新
            LaserWeldHeadWorkflowInstance().AxisReadyChanged += OnAxisReadyChanged;   // LA 就绪即时刷新
            MotionControlWorkflowInstance().TaZeroPoseStageChanged += OnTaZeroPoseStageChanged;   // 自动 ZeroPose 选中态
            this.VisibleChanged += ProcessControlPage_VisibleChanged;   // 首次可见兜底刷新
            this.HandleDestroyed += ProcessControlPage_HandleDestroyed;
        }

        #endregion

        #region 自适应调整工艺设置

        // ── 左侧参数列表（左列表 + 右设置）──
        // 行模型：单列配置名（key=Id，与 ConfigHelper「标识」列对应）
        private class ConfigListRow
        {
            public int No { get; set; }
            public string Id { get; set; }
        }

        private AntdUI.AntList<ConfigListRow> _configRows;
        private ConfigListRow _selectedConfigRow;

        /// <summary>初始化参数列表表格列：Columns 须在代码后置设置，不可</summary>
        private void InitConfigListColumns()
        {
            tableConfigList.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("No", "No.") { Width = "50" },
                new AntdUI.Column("Id", "配置名")
            };
            tableConfigList.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            _configRows = new AntdUI.AntList<ConfigListRow>();
            tableConfigList.Binding(_configRows);
            // CellClick 后置订阅（5 个先例均为代码后置，AntdUI.Table 无 SelectedItem）
            tableConfigList.CellClick += tableConfigList_CellClick;
        }

        /// <summary>列表点击：记录选中行；右键弹出「选择/切换」菜单</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void tableConfigList_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            _selectedConfigRow = e.Record as ConfigListRow;
            if (e.Button == MouseButtons.Right)
            {
                AntdUI.ContextMenuStrip.open(tableConfigList, it => ConfigSelect(), new AntdUI.IContextMenuStripItem[]
                {
                    new AntdUI.ContextMenuStripItem("选择/切换")
                });
            }
        }

        /// <summary>刷新参数列表：GetAllConfigs → 行绑定（高亮当前使用配置）。</summary>
        private void LoadConfigList()
        {
            if (_configRows == null) return;
            try
            {
                System.Data.DataTable table = WeldParamManager.Instance.ConfigHelper.GetAllConfigs();
                _configRows.Clear();
                int no = 1;
                foreach (System.Data.DataRow row in table.Rows)
                {
                    string id = row["标识"] == System.DBNull.Value ? string.Empty : row["标识"].ToString();
                    if (string.IsNullOrEmpty(id)) continue;
                    _configRows.Add(new ConfigListRow { No = no++, Id = id });
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("工艺设置", "加载参数列表失败 " + ex.Message, MessageLevel.Error);
            }
        }

        private void btnConfigAddNew_Click(object sender, EventArgs e)
        {
            btnAddNewConfig_Click(sender, e);   // 复用既有新建链（查重 + AddConfig + LoadParamOnForm）
            LoadConfigList();
        }

        private void btnConfigDelete_Click(object sender, EventArgs e)
        {
            string configId = _selectedConfigRow == null ? string.Empty : _selectedConfigRow.Id;
            if (string.IsNullOrWhiteSpace(configId)) return;

            if (configId == AutoParam.DefaultIdentity)
            {
                AntdUI.Message.error(this.FindForm(), "默认配置不允许删除！");
                return;
            }
            if (AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "确认删除",
                    "请确认需要删除选定配置：" + configId, AntdUI.TType.Info)) != DialogResult.OK)
                return;
            if (configId == WeldParamManager.Instance.CurrentAutoParamId)
            {
                AntdUI.Message.error(this.FindForm(), "不能删除正在使用的配置！");
                return;
            }
            WeldParamManager.Instance.DeleteConfig(configId);
            LoadConfigList();
            GlobalCommData.ShowLog("工艺设置", "已删除配置 " + configId, MessageLevel.Info);
        }

        /// <summary>右键「选择/切换」</summary>
        private void ConfigSelect()
        {
            string configId = _selectedConfigRow == null ? string.Empty : _selectedConfigRow.Id;
            if (string.IsNullOrWhiteSpace(configId)) return;
            if (!WeldParamManager.Instance.ConfigHelper.IsConfigExist(configId))
            {
                AntdUI.Message.error(this.FindForm(), "配置不存在！");
                return;
            }
            WeldParamManager.Instance.SwitchConfig(configId);
            _paramIdName = configId;
            LoadParamOnForm();
            LoadConfigList();
            AntdUI.Message.info(this.FindForm(), "已切换至配置：" + configId);
        }

        private void ParamAutoPage_Shown(object sender, EventArgs e)
        {
            LoadConfigList();   // 页面显示时刷新左侧参数列表（配置可能经旧弹窗变更）
            LoadParamOnForm();
            LoadAlgoConfigOnForm();
            LoadProcessEnableOnForm();
            RefreshActionStates();   // 页面重新显示时按运控/力矩模式同步指令区可用性
        }

        private void btnAddNewConfig_Click(object sender, EventArgs e)
        {
            string configName = string.Empty;
            if (!this.ShowInputStringDialog(ref configName, "请输入配置名称"))
                return;

            if (WeldParamManager.Instance.ConfigHelper.IsConfigExist(configName))
            {
                AntdUI.Message.error(System.Windows.Forms.Form.ActiveForm, "已存在当前命名配置");
                return;
            }

            WeldParamManager.Instance.AddConfig(configName);
            _paramIdName = configName;

            LoadAutoParam();
            LoadParamOnForm();
        }

        private void LoadParamOnForm()
        {
            AutoParam param = WeldParamManager.Instance.CurrentAutoParam;
            if (param == null)
                return;

            uiComboBox1.Text = param.WeldType.ToString();
            WireDiameter.Text = param.WireDiameter.ToString("F3");
            Platethickness.Text = param.Platethickness.ToString("F3");
            LaserDiameter.Text = param.LaserDiameter.ToString("F3");
            LaserPower.Text = param.LaserPower.ToString("F3");
            FeedSpeed.Text = param.FeedSpeed.ToString("F3");
            RobotSpeed.Text = param.RobotSpeed.ToString("F3");
            SeamWidth.Text = param.SeamWidth.ToString("F3");
            MaxSeamWidth.Text = param.SeamWidthMax.ToString("F3");
            MinSeamWidth.Text = param.SeamWidthMin.ToString("F3");
            Sensitivity.Text = param.Sensitivity.ToString("F3");
        }

        private void CollectParamFromForm()
        {
            AutoParam param = WeldParamManager.Instance.CurrentAutoParam;
            if (param == null)
                return;

            param.WeldType = (SeamType)uiComboBox1.SelectedIndex;
            param.WireDiameter = ParseCalibDouble(WireDiameter, param.WireDiameter);
            param.Platethickness = ParseCalibDouble(Platethickness, param.Platethickness);
            param.LaserDiameter = ParseCalibDouble(LaserDiameter, param.LaserDiameter);
            param.LaserPower = ParseCalibDouble(LaserPower, param.LaserPower);
            param.FeedSpeed = ParseCalibDouble(FeedSpeed, param.FeedSpeed);
            param.RobotSpeed = ParseCalibDouble(RobotSpeed, param.RobotSpeed);
            param.SeamWidth = ParseCalibDouble(SeamWidth, param.SeamWidth);
            param.SeamWidthMax = ParseCalibDouble(MaxSeamWidth, param.SeamWidthMax);
            param.SeamWidthMin = ParseCalibDouble(MinSeamWidth, param.SeamWidthMin);
            param.Sensitivity = ParseCalibDouble(Sensitivity, param.Sensitivity);
        }

        private void LoadAutoParam()
        {
            _paramIdName = WeldParamManager.Instance.CurrentAutoParamId;
        }

        /// <summary>焊参调整面板：仅持久化标量与 4 个流程开关。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnSaveAutoParam_Click(object sender, EventArgs e)
        {
            if (WeldParamManager.Instance.CurrentAutoParam == null)
                return;

            CollectParamFromForm();
            WeldParamManager.Instance.SaveCurrentAutoParam();

            // UI→内存须先于落盘，否则三项不写文件
            if (!ApplyAdaptUiToMemory()) return;

            // ③自适应区落盘（EnableGuide 归 ②快捷启用区）
            // 前馈与单调锁不落本区
            //    顺带落 System 会连系统设置页未保存项一并写盘。
            WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Adapt);

            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "焊参调整已保存");
        }

        /// <summary>自适应区「UI→内存」统一底座：六项，只写内存。</summary>

        /// <returns>false 表示配置对象不可用，调用方应中止。</returns>
        private bool ApplyAdaptUiToMemory()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return false;

            // 焊参调整（③自适应区）
            cfg.MaxConsecutiveFailures = ParseInt(txtFlowMaxConsecFail, cfg.MaxConsecutiveFailures);
            // 按距离分段：1~50mm，越界钳位
            double segmentMm = ParseCalibDouble(txtFlowCountNum, cfg.SegmentDistanceMm);
            if (segmentMm < 1.0) segmentMm = 1.0;
            else if (segmentMm > 50.0) segmentMm = 50.0;
            cfg.SegmentDistanceMm = segmentMm;

            // 工艺输出项已移除
            // 按段落长度触发，宽度取振镜档
            // ⚠️ 前馈系数/单调锁归①系统区，本面板不写。
            return true;
        }

        /// <summary>读取焊参调整：重载配置并回填表单。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnReadAutoParam_Click(object sender, EventArgs e)
        {
            WeldParamManager.Instance.LoadAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Adapt);
            LoadFlowSwitchOnForm();
            LoadParamOnForm();
            AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, "已读取焊参调整");
        }

        /// <summary>修改焊参调整：仅同步底部 6 项到内存，不落盘。</summary>

        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnCorAutoParam_Click(object sender, EventArgs e)
        {
            if (!ApplyAdaptUiToMemory()) return;

            AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, "已修改（未保存，点保存焊参调整落盘）");
        }

        /// <summary>修正基准：采集基准参数→落 DB→重建查找表。</summary>

        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnCorBaseline_Click(object sender, EventArgs e)
        {
            CollectParamFromForm();
            WeldParamManager.Instance.SaveCurrentAutoParam();
            WeldParamManager.Instance.ReloadCurrentParamValues();
            LoadParamOnForm();
        }

        private void btnShowAutoParam_Click(object sender, EventArgs e)
        {
            using (AutoParamForm autoValueForm = new AutoParamForm(this.FindForm() as AntdUI.Window))
            {
                autoValueForm.LoadConfigData(WeldParamManager.Instance.CurrentAutoParam);
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "自动参数配置", autoValueForm, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }

        #endregion

        #region 线激光追踪工艺设置

        /// <summary>振镜工艺参数：UI→分图形档（宽度/长度/频率），不落盘。</summary>
        /// <returns>是否成功。</returns>
        private bool ApplyGalvoParamsToHead()
        {
            MontionManager motion = MontionManager.Instance;
            WeldHeadConfig headCfg = motion?.WeldHeadConfig;
            if (headCfg == null)
            {
                AntdUI.Message.warn(this.FindForm(), "焊接头配置不可用");
                return false;
            }
            if (!int.TryParse(txtGalvoFreq.Text.Trim(), out int freq) || freq < 10 || freq > 500)
            {
                AntdUI.Message.warn(this.FindForm(), "振镜频率范围 10~500Hz");
                return false;
            }
            if (!double.TryParse(txtGalvoWidth.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
                || width < 0.0 || width > 5.0)
            {
                AntdUI.Message.warn(this.FindForm(), "振镜宽度范围 0~5mm");
                return false;
            }
            if (!double.TryParse(txtGalvoLength.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double length)
                || length < 0.0 || length > 5.0)
            {
                AntdUI.Message.warn(this.FindForm(), "振镜长度范围 0~5mm");
                return false;
            }
            headCfg.GalvoShapePresets[CurrentGalvoPattern()] = new GalvoShapeParams(width, length, freq);
            return true;
        }

        /// <summary>读取激光工艺参数：文件→UI（Tracking / LA / TA / 振镜 / YT 设置）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnReadLaserZone_Click(object sender, EventArgs e)
        {
            WeldParamManager.Instance.LoadAlgorithmsZone(
                WeldParamConfig.AlgorithmsZone.Preset,
                WeldParamConfig.AlgorithmsZone.Quick);

            // 焊接头配置从文件重载（振镜宽/长/频/阈值/量纲）
            MontionManager motion = MontionManager.Instance;
            motion?.LoadWeldHeadConfig();

            LoadGalvoActionUi();
            LoadLaParamUi();
            LoadTaParamUi();
            LoadYtCalibUi();
            LoadProcessEnableOnForm();
            AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, "已读取激光工艺参数");
        }

        /// <summary>修改激光工艺参数：UI→内存（不落盘，含 LA/TA 校验）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnModifyLaserZone_Click(object sender, EventArgs e)
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;
            if (!ApplyTaToConfig(cfg)) return;
            if (!ApplyLaToConfig(cfg)) return;
            if (!ApplyGalvoParamsToHead()) return;
            ApplyYtCalibToConfig(cfg);
            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "激光工艺参数已修改（未保存，点保存参数落盘）");
        }

        /// <summary>保存参数：UI→内存→文件，落 ④Preset 区（含 YT 设置三项）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnSaveLaserZone_Click(object sender, EventArgs e)
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;
            if (!ApplyTaToConfig(cfg)) return;
            if (!ApplyLaToConfig(cfg)) return;
            if (!ApplyGalvoParamsToHead()) return;
            ApplyYtCalibToConfig(cfg);
            // ④Preset 区落盘（工艺设定值 + YT 设置三项）。
            WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Preset);
            // 几何与标定参数不落本区
            MontionManager motion = MontionManager.Instance;
            if (motion != null)
            {
                motion.SaveWeldHeadConfig();
                WeldHeadSlaveDevice head = motion.WeldHead;
                if (head != null && head.IsInitialized && motion.WeldHeadConfig != null)
                {
                    // 下发全局项与当前图形档
                    head.SetGalvoParams(motion.WeldHeadConfig.BuildGalvoParamsFor(CurrentGalvoPattern()));
                }
            }
            LoadLaParamUi();
            LoadTaParamUi();
            LoadYtCalibUi();
            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "激光工艺参数已保存");
        }

        #endregion

        #region 工艺调试快捷设定

        /// <summary>回填 Tracking 指令默认项（工艺启用开关已迁系统设置页）</summary>
        private void LoadProcessEnableOnForm()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            _loadingProcessEnable = true;
            try
            {
                // Tracking 指令：LockYCalib=true 回填 Y_ForceCalibPose，否则默认 Y_AutoTracking
                radioQuickYForceCalibPose.Checked = cfg.LockYCalib;
                radioQuickYAuto.Checked = !cfg.LockYCalib;
            }
            finally
            {
                _loadingProcessEnable = false;
            }
        }

        /// <summary>三个指令区加载默认态：一律选中各自的 Auto 项（Y_AutoTracking / LA_Auto / TA_Auto）。</summary>
        private void ApplyDefaultCommandModes()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg != null) cfg.LockYCalib = false;

            bool prev = _loadingProcessEnable;
            _loadingProcessEnable = true;
            try
            {
                radioQuickYAuto.Checked = true;
                // TA 区不预选：预选会造成「再点不触发」的假禁用；
                // 自动回零不抢用户选中态
                // 操作员点哪条执行哪条。
                // 两组已在独立容器内各自单选，可同时选中；
                // 振镜组默认项由 LoadGalvoActionUi 落定，此处只复位对焦组默认项。
                radioQuickLaAuto.Checked = true;

                // 记录上一已选指令，取消确认时回退（不执行）
                // TA 无预选项：取消确认时直接取消选中
                _lastYRadio = radioQuickYAuto;
                _lastTaRadio = null;
                _lastLaRadio = radioQuickLaAuto;
            }
            finally
            {
                _loadingProcessEnable = prev;
            }
        }

        /// <summary>Tracking 指令（Y_AutoTracking / Y_ForceCalibPose / Y_ForceHome）：仅选中沿触发。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void radioQuickY_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (!e.Value || _loadingProcessEnable || _suppressActionRadio) return;
            if (!(sender is AntdUI.Radio radio)) return;
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            ResetOtherRadioBacks(radio);   // 互斥单色：取消上一个指令的状态
            RunInstruction(radio, "Y 跟踪轴", BuildYActionDetail(radio, cfg),
                delegate { return ExecuteYAction(radio, cfg); }, ref _lastYRadio);
        }

        /// <summary>Y 指令确认弹窗的关键参数文案。</summary>
        /// <param name="cfg">配置对象。</param>
        /// <returns>结果文本。</returns>
        private static string BuildYActionDetail(AntdUI.Radio radio, WeldParamConfig cfg)
        {
            switch (radio.Name)
            {
                case "radioQuickYForceCalibPose":
                    return "目标位置 " + cfg.TeachLockYMm.ToString("F3", CultureInfo.InvariantCulture) + " mm（锁定示教 Y 坐标）";
                case "radioQuickYForceHome":
                    return "锁定解除后下发回零指令";
                default:
                    return "Y 轴跟随线激光目标值（LockYCalib=false）";
            }
        }

        /// <summary>Y 指令执行体</summary>
        /// <param name="cfg">配置对象。</param>

        /// <returns>是否成功。</returns>
        private bool ExecuteYAction(AntdUI.Radio radio, WeldParamConfig cfg)
        {
            switch (radio.Name)
            {
                case "radioQuickYAuto":
                    cfg.LockYCalib = false;
                    // LockYCalib 归 ②Quick 区（INI PROCESS_CONTROL/LOCK_Y_CALIB）：只落该分区，禁全量保存
                    WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Quick);
                    GlobalCommData.ShowLog("工艺设置", "Y_AutoTracking Y 轴跟随线激光目标值", MessageLevel.Info);
                    return true;
                case "radioQuickYForceCalibPose":
                    cfg.LockYCalib = true;
                    if (!MoveAxisToTarget(MontionManager.SeamTrackYKeys, cfg.TeachLockYMm, "Y_ForceCalibPose")) return false;
                    WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Quick);
                    return true;
                case "radioQuickYForceHome":
                    cfg.LockYCalib = false;
                    WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Quick);
                    return MoveAxisHome(MontionManager.SeamTrackYKeys, "Y_ForceHome");
                default:
                    return false;
            }
        }
        /// <summary>按轴名关键字定位跟踪轴并下发非阻塞回零</summary>

        /// <param name="keys">轴名角色关键字。</param>
        /// <param name="actionName">动作名（日志与单飞隔离键）。</param>
        /// <returns>是否成功。</returns>
        private bool MoveAxisHome(string[] keys, string actionName)
        {
            bool ok = MotionControlWorkflowInstance().TryHomeAxisWithWait(keys, actionName,
                out string axisName, out string rejectCode, out string rejectDetail, out string evidence);

            if (HandleAxisReject(rejectCode, rejectDetail, keys, actionName)) return false;

            GlobalCommData.ShowLog("工艺设置", actionName + " " + axisName + " 回零指令已下发，进入状态检测",
                MessageLevel.Info);
            ReportAxisResult(actionName, ok, evidence, rejectDetail);
            return ok;
        }
        /// <summary>按轴名关键字在运控配置中定位轴并绝对移动</summary>

        /// <param name="keys">轴名角色关键字。</param>
        /// <param name="targetMm">目标位置（mm）。</param>
        /// <param name="actionName">动作名（日志与单飞隔离键）。</param>
        /// <returns>是否成功。</returns>
        private bool MoveAxisToTarget(string[] keys, double targetMm, string actionName)
        {
            bool ok = MotionControlWorkflowInstance().TryMoveAxisToTarget(keys, targetMm, actionName,
                out string axisName, out string rejectCode, out string rejectDetail, out string evidence);

            if (HandleAxisReject(rejectCode, rejectDetail, keys, actionName)) return false;
            if (rejectCode == MotionControlWorkflow.AxisRejectCommandFailed)
            {
                GlobalCommData.ShowLog("工艺设置", actionName + " 指令下发失败，未进入状态检测", MessageLevel.Error);
                UiMessage(true, actionName + " 指令下发失败，请检查伺服状态");
                return false;
            }

            GlobalCommData.ShowLog("工艺设置", string.Format("{0}：{1} 目标 {2} mm 已下发，进入状态检测",
                actionName, axisName, targetMm.ToString("F3", CultureInfo.InvariantCulture)), MessageLevel.Info);
            ReportAxisResult(actionName, ok, evidence, rejectDetail);
            return ok;
        }

        /// <summary>应用动作入口拒绝码 → 界面提示与日志</summary>

        /// <param name="rejectCode">拒绝码</param>
        /// <param name="rejectDetail">拒绝详情</param>
        /// <param name="keys">轴名角色关键字（未装配提示用）</param>
        /// <param name="actionName">动作名</param>
        /// <returns>已拒绝返回 true，调用方直接返回 false</returns>
        private bool HandleAxisReject(string rejectCode, string rejectDetail, string[] keys, string actionName)
        {
            if (rejectCode == MotionControlWorkflow.AxisRejectNotInitialized)
            {
                UiMessage(false, "运控未初始化，无法执行：" + actionName);
                GlobalCommData.ShowLog("工艺设置", actionName + " 执行失败：运控未初始化", MessageLevel.Error);
                return true;
            }
            if (rejectCode == MotionControlWorkflow.AxisRejectAxisNotAssigned)
            {
                UiMessage(false,
                    "未在运控配置中找到该工艺对应的轴（关键字：" + string.Join("/", keys) + "），无法执行：" + actionName);
                GlobalCommData.ShowLog("工艺设置", actionName + " 执行失败：未找到对应轴", MessageLevel.Error);
                return true;
            }
            if (rejectCode == MotionControlWorkflow.AxisRejectAxisNotReady)
            {
                UiMessage(false, actionName + " 不可执行：" + rejectDetail);
                GlobalCommData.ShowLog("工艺设置", actionName + " 被拒 " + rejectDetail, MessageLevel.Warning);
                return true;
            }
            if (rejectCode == MotionControlWorkflow.AxisRejectBusy)
            {
                GlobalCommData.ShowLog("工艺设置", actionName + " 被拒 " + rejectDetail, MessageLevel.Warning);
                return true;
            }
            return false;
        }

        /// <summary>状态检测结果 → 日志与失败提示</summary>

        /// <param name="actionName">动作名</param>
        /// <param name="ok">是否达成</param>
        /// <param name="evidence">判据实测证据</param>
        /// <param name="rejectDetail">收尾异常详情</param>
        private void ReportAxisResult(string actionName, bool ok, string evidence, string rejectDetail)
        {
            GlobalCommData.ShowLog("工艺设置", actionName + " 状态检测 " + (ok ? "达成 " : "失败 ") + evidence,
                ok ? MessageLevel.Info : MessageLevel.Error);
            if (ok) return;
            if (rejectDetail.Length > 0)
            {
                GlobalCommData.ShowLog("工艺设置",
                    actionName + " 减速停止下发异常 " + rejectDetail,
                    MessageLevel.Warning);
            }
            UiMessage(true, actionName + " 状态检测失败：" + evidence);
        }

        #endregion

        #region 激光头工艺动作绑定

        /// <summary>回填期静默标记：程序回填 Radio 时抑制 CheckedChanged</summary>
        private bool _loadingGalvoUi;

        /// <summary>TA_Auto 门状态（-2 未判定 / -1 等待 / 0 不可用 / 1 可用）</summary>
        private int _lastTaAutoGate = -2;

        /// <summary>执行成功底色（不透明绿；业主明确要求用 BackColor，不用 Fill）</summary>
        private static readonly System.Drawing.Color ActionBackOk = System.Drawing.Color.FromArgb(76, 175, 80);
        /// <summary>执行失败底色（不透明红）</summary>
        private static readonly System.Drawing.Color ActionBackFail = System.Drawing.Color.FromArgb(244, 67, 54);
        /// <summary>执行中底色（中性灰；不得提前上绿）</summary>
        private static readonly System.Drawing.Color ActionBackRunning = System.Drawing.Color.FromArgb(189, 189, 189);
        /// <summary>未执行/未选中底色（与 panelQuick 容器白一致）</summary>
        private static readonly System.Drawing.Color ActionBackIdle = System.Drawing.Color.White;

        /// <summary>Control.SetStyle 为 protected，反射调用一次（构造期统一打开 BackColor 绘制）</summary>
        private static readonly System.Reflection.MethodInfo SetStyleMethod =
            typeof(System.Windows.Forms.Control).GetMethod("SetStyle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // —— 指令区「确认 → 执行 → 上色 → 复位」公共链路 ——
        /// <summary>程序化回填/回退选中态期间抑制 CheckedChanged（防递归）</summary>
        private bool _suppressActionRadio;
        /// <summary>各指令组上次成功的 Radio，取消时回退</summary>
        private AntdUI.Radio _lastYRadio;
        private AntdUI.Radio _lastLaRadio;
        private AntdUI.Radio _lastTaRadio;

        /// <summary>振镜图形转中文名。</summary>
        /// <param name="pattern">图形</param>
        /// <returns>中文名</returns>
        private static string GalvoPatternText(WsgGalvoPattern pattern)
        {
            switch (pattern)
            {
                case WsgGalvoPattern.Circle: return "圆";
                case WsgGalvoPattern.Eight: return "八字";
                case WsgGalvoPattern.Arc: return "圆弧";
                default: return "直线";
            }
        }

        /// <summary>指令区按钮可用性同步（事件驱动单一入口，UI 线程）</summary>
        private void RefreshActionStates()
        {
            if (IsDisposed || Disposing) return;

            MontionManager manager = MontionManager.Instance;
            bool initDone = manager != null && manager.IsInitCompleted;
            MotionControlWorkflow motionFlow = MotionControlWorkflowInstance();
            LaserWeldHeadWorkflow laserFlow = LaserWeldHeadWorkflowInstance();

            bool yReady = initDone && motionFlow != null && motionFlow.YReady;
            bool laReady = initDone && laserFlow != null && laserFlow.LaReady;
            bool taMotionReady = motionFlow != null && motionFlow.TaMotionReady;
            bool taReady = motionFlow != null && motionFlow.TaAvailable;
            string taReason = motionFlow == null ? "运控流程未就绪" : motionFlow.TaDegradedReason;

            // Y 区：轴未使能即不可点，不进确认框
            SetControlEnabled(radioQuickYAuto, yReady);
            SetControlEnabled(radioQuickYForceCalibPose, yReady);
            SetControlEnabled(radioQuickYForceHome, yReady);

            // TA 区不按就绪禁用：初始化结果会过期
            // 就绪判定统一在执行段；被拒原因由执行段给日志与失败底色
            UpdateTaAutoGateHint(initDone, taMotionReady, taReady, taReason);

            // LA 指令区：焊接头从站就绪才可点
            SetControlEnabled(radioQuickLaAuto, laReady);
            SetControlEnabled(radioQuickLaSpot1, laReady);
            SetControlEnabled(radioQuickLaSpot2, laReady);
            SetControlEnabled(radioQuickLaPose1, laReady);
            SetControlEnabled(radioQuickLaDefaultFocus, laReady);
            // 振镜未启用则图形组禁用
            bool galvoWeaveOn = LaserWeldHeadWorkflow.IsGalvoWeaveEnabled();
            SetControlEnabled(radioQuickLaGalvoCircle, laReady && galvoWeaveOn);
            SetControlEnabled(radioQuickLaGalvoEight, laReady && galvoWeaveOn);
            SetControlEnabled(radioQuickLaGalvoLine, laReady && galvoWeaveOn);
            SetControlEnabled(radioQuickLaGalvoArc, laReady && galvoWeaveOn);
            SetControlEnabled(txtGalvoWidth, laReady);
            SetControlEnabled(txtGalvoLength, laReady);
            SetControlEnabled(txtGalvoFreq, laReady);
            // 手动与自动模式互斥
            bool autoWelding = AdaWeldSystem.MainDeviceControl.DeviceControlWork.Instance.IsAutoWeldingRunning;
            SetControlEnabled(btnGalvoManualStart, laReady && !autoWelding);
            SetControlEnabled(btnGalvoManualStop, laReady && !autoWelding);
        }

        /// <summary>差分设可用性：值没变不碰控件</summary>
        /// <param name="control">目标控件</param>
        /// <param name="value">目标可用性</param>
        private static void SetControlEnabled(Control control, bool value)
        {
            if (control == null || control.Enabled == value) return;
            control.Enabled = value;
        }

        /// <summary>差分设文本：与现值相同直接返回</summary>
        /// <param name="control">目标控件</param>
        /// <param name="text">目标文本</param>
        private static void SetText(Control control, string text)
        {
            if (control == null) return;
            if (text == null) text = string.Empty;
            if (control.Text == text) return;
            control.Text = text;
        }

        /// <summary>TA_Auto 门状态变化日志</summary>

        /// <param name="initDone">初始化闸门是否已打开</param>
        /// <param name="taMotionReady">TA 轴可动（ZeroPose 可重试）</param>
        /// <param name="taReady">TA 执行就绪（TaAvailable：已使能 + 已回零；不要求已到工作零位）</param>
        /// <param name="degradedReason">TA 降级原因（空串表示就绪）</param>
        private void UpdateTaAutoGateHint(bool initDone, bool taMotionReady, bool taReady, string degradedReason)
        {
            int gate = !initDone ? -1 : (taReady ? 1 : 0);
            // 等待初始化阶段不打印可用结论，避免提前宣告
            if (gate != -1 && _lastTaAutoGate != -2 && gate != _lastTaAutoGate)
            {
                if (gate == 0)
                {
                    // 状态刷新副产物：只进文件，不刷界面
                    GlobalCommData.ShowLog("工艺设置",
                        "TA 未就绪：" + (degradedReason.Length > 0 ? degradedReason : "TA 轴未准备好"),
                        MessageLevel.Debug);
                }
                else
                {
                    GlobalCommData.ShowLog("工艺设置", "TA_Auto 就绪", MessageLevel.Info);
                }
            }
            _lastTaAutoGate = gate;
        }

        /// <summary>加载振镜工艺区</summary>
        private void LoadGalvoActionUi()
        {
            _loadingGalvoUi = true;
            try
            {
                ApplyGalvoPatternSelection();
                LoadGalvoShapeToInputs();
            }
            finally
            {
                _loadingGalvoUi = false;
            }
            RefreshActionStates();
        }

        /// <summary>把「当前图形」的分档参数（宽/长/频）载入输入框。</summary>
        private void LoadGalvoShapeToInputs()
        {
            WeldHeadConfig headCfg = MontionManager.Instance?.WeldHeadConfig;
            if (headCfg == null) return;
            GalvoShapeParams shape = headCfg.GetGalvoShape(CurrentGalvoPattern());
            txtGalvoWidth.Text = shape.Width.ToString("0.000", CultureInfo.InvariantCulture);
            txtGalvoLength.Text = shape.Length.ToString("0.000", CultureInfo.InvariantCulture);
            txtGalvoFreq.Text = shape.Frequency.ToString();
        }

        /// <summary>当前图形：GA 指令区选中的那个（未选 ⇒ 配置里的图形）。</summary>
        /// <returns>图形枚举</returns>
        private WsgGalvoPattern CurrentGalvoPattern()
        {
            if (radioQuickLaGalvoCircle.Checked) return WsgGalvoPattern.Circle;
            if (radioQuickLaGalvoEight.Checked) return WsgGalvoPattern.Eight;
            if (radioQuickLaGalvoArc.Checked) return WsgGalvoPattern.Arc;
            if (radioQuickLaGalvoLine.Checked) return WsgGalvoPattern.Line;
            // 未选中时用配置图形取档
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            return cfg != null ? LaserWeldHeadWorkflow.ResolveGalvoPattern(cfg.GalvoPattern) : WsgGalvoPattern.Line;
        }

        /// <summary>按振镜启用态决定图形选态</summary>
        private void ApplyGalvoPatternSelection()
        {
            if (!LaserWeldHeadWorkflow.IsGalvoWeaveEnabled())
            {
                // 未启用振镜则清空图形选中
                ClearGalvoPatternSelection();
                return;
            }
            WsgGalvoPattern? configured = ReadConfiguredGalvoPattern();
            if (configured == null)
            {
                // 未设图形则回退默认直线
                SaveGalvoPattern(WsgGalvoPattern.Line);
                GlobalCommData.ShowLog("工艺设置", "振镜已启用但未设置图形，已落回默认直线", MessageLevel.Info);
                configured = WsgGalvoPattern.Line;
            }
            AntdUI.Radio patternRadio = GalvoPatternToRadio(configured);
            if (patternRadio != null) patternRadio.Checked = true;
        }

        /// <summary>清空 GA 图形组选中态（振镜未启用 = 没有图形）。</summary>
        private void ClearGalvoPatternSelection()
        {
            radioQuickLaGalvoCircle.Checked = false;
            radioQuickLaGalvoEight.Checked = false;
            radioQuickLaGalvoLine.Checked = false;
            radioQuickLaGalvoArc.Checked = false;
        }

        /// <summary>GA「手动下发」</summary>
        private void btnGalvoManualStart_Click(object sender, EventArgs e)
        {
            WeldHeadConfig headCfg = MontionManager.Instance?.WeldHeadConfig;
            if (headCfg == null)
            {
                AntdUI.Message.warn(this.FindForm(), "焊接头配置不可用");
                return;
            }
            if (!ApplyGalvoParamsToHead()) return;   // 先校验输入并写入当前图形档
            WsgGalvoPattern pattern = CurrentGalvoPattern();
            GalvoShapeParams shape = headCfg.GetGalvoShape(pattern);
            LaserWeldHeadWorkflow head = LaserWeldHeadWorkflow.Instance;
            head.SetRunMode(WorkflowRunMode.Manual);
            if (!head.GalvoManualStart(pattern, shape))
            {
                AntdUI.Message.warn(this.FindForm(), "振镜手动下发失败（自动焊接进行中或从站未就绪）");
                return;
            }
            GlobalCommData.ShowLog("工艺设置", "振镜手动下发 图形 " + GalvoPatternText(pattern), MessageLevel.Info);
            RefreshActionStates();
        }

        /// <summary>GA「关闭退出」：撤销振镜使能并回逻辑零位。</summary>
        private void btnGalvoManualStop_Click(object sender, EventArgs e)
        {
            LaserWeldHeadWorkflow head = LaserWeldHeadWorkflow.Instance;
            head.SetRunMode(WorkflowRunMode.Manual);
            if (!head.GalvoManualStop())
            {
                AntdUI.Message.warn(this.FindForm(), "振镜关闭退出失败（自动焊接进行中或从站未就绪）");
                return;
            }
            GlobalCommData.ShowLog("工艺设置", "振镜已关闭退出", MessageLevel.Info);
            RefreshActionStates();
        }

        /// <summary>读取配置的振镜图形</summary>
        /// <returns>有效图形，无则返回空</returns>
        private static WsgGalvoPattern? ReadConfiguredGalvoPattern()
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return null;
            int value = cfg.GalvoPattern;
            if (value < (int)WsgGalvoPattern.Circle || value > (int)WsgGalvoPattern.Arc) return null;
            return (WsgGalvoPattern)value;
        }

        /// <summary>让 Radio 背景色真正可见</summary>
        private static void EnableBackColorPaint(AntdUI.Radio radio)
        {
            if (radio == null || SetStyleMethod == null) return;
            try
            {
                SetStyleMethod.Invoke(radio,
                    new object[] { System.Windows.Forms.ControlStyles.AllPaintingInWmPaint, false });
                //radio.Fill = System.Drawing.Color.Transparent;
                radio.BackColor = ActionBackIdle;
                radio.Invalidate();
            }
            catch (Exception)
            {
                // 反射失败保持原样：不上色，但不抛异常
            }
        }

        /// <summary>构造期一次性打开全部指令 Radio 的 BackColor 绘制</summary>
        private void PrepareActionRadioBackColor()
        {
            AntdUI.Radio[] radios =
            {
                radioQuickYAuto, radioQuickYForceCalibPose, radioQuickYForceHome,
                radioQuickTaAuto, radioQuickTaBreak, radioQuickTaRetract,
                radioQuickTaPose1, radioQuickTaPose2, radioQuickTaZeroPose,
                radioQuickLaAuto, radioQuickLaSpot1, radioQuickLaSpot2,
                radioQuickLaPose1, radioQuickLaDefaultFocus,
                radioQuickLaGalvoCircle, radioQuickLaGalvoEight, radioQuickLaGalvoLine, radioQuickLaGalvoArc
            };
            for (int i = 0; i < radios.Length; i++) EnableBackColorPaint(radios[i]);
        }
        /// <summary>指令确认弹窗：指令名 + 所属轴 + 关键参数；取消返回 false</summary>
        /// <param name="radio">待执行的指令按钮，取 Text 作指令名</param>
        /// <param name="axisName">轴名</param>
        /// <param name="detail">关键参数行，空则不显示</param>
        /// <returns>是否确认执行</returns>
        private bool ConfirmInstruction(AntdUI.Radio radio, string axisName, string detail)
        {
            string message = "指令：" + radio.Text + "\r\n所属轴：" + axisName
                + (string.IsNullOrEmpty(detail) ? string.Empty : "\r\n" + detail)
                + "\r\n\r\n确认执行？";
            return AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "指令确认", message, AntdUI.TType.Info))
                == DialogResult.OK;
        }

        /// <summary>UI 提示统一经 BeginInvoke</summary>

        /// <param name="error">true=错误提示，false=警告提示</param>
        /// <param name="text">提示文本</param>
        private void UiMessage(bool error, string text)
        {
            if (IsDisposed || Disposing) return;
            Action show = () =>
            {
                if (IsDisposed || Disposing) return;
                if (error) AntdUI.Message.error(this.FindForm(), text);
                else AntdUI.Message.warn(this.FindForm(), text);
            };
            try
            {
                if (InvokeRequired) BeginInvoke(show);
                else show();
            }
            catch (Exception)
            {
                // 页面已销毁：忽略
            }
        }

        /// <summary>上色：成功=绿 / 失败=红</summary>
        private void ApplyActionBack(AntdUI.Radio radio, bool ok)
        {
            if (radio == null) return;
            System.Drawing.Color target = ok ? ActionBackOk : ActionBackFail;
            if (radio.BackColor != target)   // 差分：同色不重绘
            {
                radio.BackColor = target;
                radio.Invalidate();
            }
        }

        /// <summary>执行中：中性灰底，不得提前上绿。</summary>
        private void ApplyActionRunning(AntdUI.Radio radio)
        {
            if (radio == null) return;
            if (radio.BackColor != ActionBackRunning)   // 差分：同色不重绘
            {
                radio.BackColor = ActionBackRunning;
                radio.Invalidate();
            }
        }

        /// <summary>复位旧指令底色</summary>
        private void ResetActionBack(AntdUI.Radio radio)
        {
            if (radio == null) return;
            if (radio.BackColor == ActionBackIdle) return;   // 差分：同色不重绘
            radio.BackColor = ActionBackIdle;
            radio.Invalidate();
        }

        /// <summary>指令区互斥单色：复位同组其它指令 Radio 的底色。</summary>

        /// <param name="current">当前 Radio（自身底色不动）</param>
        private void ResetOtherRadioBacks(AntdUI.Radio current)
        {
            AntdUI.Radio[] group = RadioGroupOf(current);
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] == null) continue;
                if (ReferenceEquals(group[i], current)) continue;
                ResetActionBack(group[i]);
            }
        }

        /// <summary>取指令 Radio 所属组（按容器作用域）。</summary>
        /// <param name="radio">指令 Radio</param>
        /// <returns>同组 Radio 集合；不属于任何组时返回空集</returns>
        private AntdUI.Radio[] RadioGroupOf(AntdUI.Radio radio)
        {
            if (radio == null) return new AntdUI.Radio[0];
            if (radio.Name.StartsWith("radioQuickTa", StringComparison.Ordinal))
            {
                return new AntdUI.Radio[]
                {
                    radioQuickTaAuto, radioQuickTaBreak, radioQuickTaRetract,
                    radioQuickTaPose1, radioQuickTaPose2, radioQuickTaZeroPose
                };
            }
            if (radio.Name.StartsWith("radioQuickY", StringComparison.Ordinal))
            {
                return new AntdUI.Radio[] { radioQuickYAuto, radioQuickYForceCalibPose, radioQuickYForceHome };
            }
            if (radio.Name.StartsWith("radioQuickLaGalvo", StringComparison.Ordinal))
            {
                return new AntdUI.Radio[]
                {
                    radioQuickLaGalvoCircle, radioQuickLaGalvoEight,
                    radioQuickLaGalvoLine, radioQuickLaGalvoArc
                };
            }
            if (radio.Name.StartsWith("radioQuickLa", StringComparison.Ordinal))
            {
                return new AntdUI.Radio[]
                {
                    radioQuickLaAuto, radioQuickLaSpot1, radioQuickLaSpot2,
                    radioQuickLaPose1, radioQuickLaDefaultFocus
                };
            }
            return new AntdUI.Radio[0];
        }

        /// <summary>统一链路：确认→门禁→单飞→后台执行→上色</summary>
        /// <param name="radio">指令按钮</param>
        /// <param name="axisName">轴名</param>
        /// <param name="detail">确认弹窗里的关键参数行</param>
        /// <param name="exec">实际下发指令的委托</param>
        /// <param name="last">上次成功按钮，被拒或取消时回退</param>

        /// <returns>是否已成功下发到后台线程</returns>
        private bool RunInstruction(AntdUI.Radio radio, string axisName, string detail, Func<bool> exec, ref AntdUI.Radio last)
        {
            Stopwatch watch = Stopwatch.StartNew();   // 诊断：点击入口到线程启动
            MontionManager manager = MontionManager.Instance;

            // ① 确认：执行动作必须等用户点确定才生效
            if (!ConfirmInstruction(radio, axisName, detail))
            {
                // 取消时回退选中态与底色
                ResetActionBack(radio);
                RevertInstructionSelection(radio, last);
                return false;
            }

            // ② 门禁预检（确认之后才判：被拒也要让用户看得见）
            string gateReason = ResolveAxisGateReason(radio, manager);
            if (gateReason.Length > 0)
            {
                MarkInstructionRejected(radio, radio.Text + " 被拒 " + gateReason);
                return false;
            }

            // ③ 单飞预检
            string axisKey = ResolveInstructionAxisKey(radio);
            if (axisKey != null && manager.IsAxisInstructionBusy(axisKey))
            {
                string busy = manager.GetAxisInstructionBusyText(axisKey);
                MarkInstructionRejected(radio, radio.Text + " 被拒 " + busy + "，暂不接受新指令");
                return false;
            }

            // ④ 执行
            if (last != null && !ReferenceEquals(last, radio)) ResetActionBack(last);
            ApplyActionRunning(radio);
            last = radio;   // 本次选中：后续取消确认时回退到它

            Thread worker = new Thread(() => InstructionWorker(radio, exec))
            {
                IsBackground = true,
                Name = "InstructionDetect_" + radio.Name
            };
            try
            {
                worker.Start();
            }
            catch (Exception ex)
            {
                ApplyActionBack(radio, false);
                GlobalCommData.ShowLog("工艺设置", radio.Text + " 后台检测线程启动失败 " + ex.Message,
                    MessageLevel.Error);
                return false;
            }
            watch.Stop();
            GlobalCommData.ShowLog("工艺设置", "点击诊断 " + radio.Text + " 入口到线程启动 "
                + watch.ElapsedMilliseconds + "ms", MessageLevel.Debug);
            return true;
        }

        /// <summary>指令被拒：保持当前选中态 + 标红 + 清同组旧底色。</summary>

        /// <param name="radio">被拒的指令 Radio</param>
        /// <param name="logText">日志文本</param>
        private void MarkInstructionRejected(AntdUI.Radio radio, string logText)
        {
            ResetOtherRadioBacks(radio);
            ApplyActionBack(radio, false);
            GlobalCommData.ShowLog("工艺设置", logText, MessageLevel.Warning);
        }

        /// <summary>后台执行指令并阻塞等待状态检测结果；UI 更新一</summary>
        private void InstructionWorker(AntdUI.Radio radio, Func<bool> exec)
        {
            bool ok = false;
            try
            {
                ok = exec();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("工艺设置", radio.Text + " 执行异常：" + ex.Message,
                    MessageLevel.Error);
            }

            if (IsDisposed || Disposing) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing) return;
                    ApplyActionBack(radio, ok);
                    RefreshActionStates();
                }));
            }
            catch (Exception)
            {
                // 页面已销毁：忽略 UI 更新
            }
        }

        /// <summary>回退选中态（取消确认/被拒时调用，不下发指令）。</summary>
        private void RevertInstructionSelection(AntdUI.Radio radio, AntdUI.Radio last)
        {
            _suppressActionRadio = true;
            try
            {
                if (last != null && !ReferenceEquals(last, radio)) last.Checked = true;
                else radio.Checked = false;
            }
            finally
            {
                _suppressActionRadio = false;
            }
        }

        /// <summary>按轴取门禁原因（就绪返空串）</summary>

        /// <param name="radio">指令 Radio</param>
        /// <param name="manager">运控管理器（仅判空）</param>
        /// <returns>不可执行原因；空串表示放行或交执行段判定</returns>
        private string ResolveAxisGateReason(AntdUI.Radio radio, MontionManager manager)
        {
            if (manager == null) return "运动管理器未初始化";
            if (radio == null) return "指令未绑定轴";
            MotionControlWorkflow motionFlow = MotionControlWorkflowInstance();
            LaserWeldHeadWorkflow laserFlow = LaserWeldHeadWorkflowInstance();
            if (radio.Name.StartsWith("radioQuickTa", StringComparison.Ordinal))
            {
                // TA_Zero 须已使能且回零
                bool ok = motionFlow != null && motionFlow.TaAvailable;
                if (ok) return string.Empty;
                return motionFlow == null ? "运控流程未就绪" : motionFlow.TaDegradedReason;
            }
            if (radio.Name.StartsWith("radioQuickY", StringComparison.Ordinal))
            {
                if (motionFlow != null && motionFlow.YReady) return string.Empty;
                return motionFlow == null ? "运控流程未就绪" : motionFlow.YGateReason;
            }
            if (radio.Name.StartsWith("radioQuickLa", StringComparison.Ordinal))
            {
                if (laserFlow != null && laserFlow.LaReady) return string.Empty;
                return laserFlow == null ? "激光头流程未就绪" : laserFlow.LaGateReason;
            }
            return string.Empty;
        }

        /// <summary>指令所属轴名</summary>
        private static string ResolveInstructionAxisKey(AntdUI.Radio radio)
        {
            MontionManager manager = MontionManager.Instance;
            if (manager == null || radio == null) return null;
            string axisName;
            if (radio.Name.StartsWith("radioQuickTa", StringComparison.Ordinal))
            {
                return manager.TryGetAxisNameByKeys(MontionManager.TaAxisKeys, out axisName) ? axisName : null;
            }
            if (radio.Name.StartsWith("radioQuickY", StringComparison.Ordinal))
            {
                return manager.TryGetAxisNameByKeys(MontionManager.SeamTrackYKeys, out axisName) ? axisName : null;
            }
            if (radio.Name.StartsWith("radioQuickLa", StringComparison.Ordinal)) return "LA";
            return null;
        }

        /// <summary>LaserWeldHeadWorkflow 单例快捷访问（LA 对焦/振镜流程归属）。</summary>
        private static AdaWeldSystem.MainDeviceControl.DeviceWorkflow.LaserWeldHeadWorkflow LaserWeldHeadWorkflowInstance()
        {
            return AdaWeldSystem.MainDeviceControl.DeviceWorkflow.LaserWeldHeadWorkflow.Instance;
        }

        /// <summary>MotionControlWorkflow 单例快捷访问（Y / TA 流程归属）。</summary>
        private static AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow MotionControlWorkflowInstance()
        {
            return AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.Instance;
        }

        // —— TA 动作（流程归属：MotionControlWorkflow；快捷区与完整区共用） ——
        private bool DoTaAuto()
        {
            // 可用性由 MotionControlWorkflow 的前置判定打印可读原因；
            // TaExecute 完成 CSP→CST→使能，失败回滚可重试
            return MotionControlWorkflowInstance().TaExecute(
                AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.TaCommand.Auto);
        }

        private bool DoTaBreak()
        {
            return MotionControlWorkflowInstance().TaExecute(
                AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.TaCommand.Break);
        }

        private bool DoTaPose1()
        {
            return MotionControlWorkflowInstance().TaExecute(
                AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.TaCommand.Pose1);
        }

        private bool DoTaPose2()
        {
            return MotionControlWorkflowInstance().TaExecute(
                AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.TaCommand.Pose2);
        }

        private bool DoTaRetract()
        {
            return MotionControlWorkflowInstance().TaExecute(
                AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.TaCommand.Retract);
        }

        /// <summary>TA 工作零位：阻塞到到位或失败</summary>
        /// <returns>是否成功。</returns>
        private bool DoTaZeroPose()
        {
            return MotionControlWorkflowInstance().TaExecute(
                AdaWeldSystem.MainDeviceControl.DeviceWorkflow.MotionControlWorkflow.TaCommand.ZeroPose);
        }

        private bool DoLaAuto()
        {
            return LaserWeldHeadWorkflowInstance().LaAuto();
        }

        private bool DoLaSpot1()
        {
            return LaserWeldHeadWorkflowInstance().LaSpot1();
        }

        private bool DoLaSpot2()
        {
            return LaserWeldHeadWorkflowInstance().LaSpot2();
        }

        private bool DoLaPose1()
        {
            return LaserWeldHeadWorkflowInstance().LaPose1();
        }

        private bool DoLaDefaultFocus()
        {
            return LaserWeldHeadWorkflowInstance().LaDefaultFocus();
        }
        /// <summary>振镜图形落盘唯一入口：GalvoPattern 归 ④Preset 区（INI PROCESS_CONTROL/GALVO_PATTERN）。</summary>
        private void SaveGalvoPattern(WsgGalvoPattern pattern)
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            cfg.GalvoPattern = (int)pattern;
            WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Preset);
        }

        /// <summary>振镜图形设定唯一入口：由 GA_* 图形组 Radio 触发，落盘走 <see cref="SaveGalvoPattern"/>。</summary>
        private void SetGalvoPattern(WsgGalvoPattern pattern)
        {
            SaveGalvoPattern(pattern);
            GlobalCommData.ShowLog("工艺设置", "振镜图形已设为 " + GalvoPatternText(pattern), MessageLevel.Info);
            RefreshActionStates();
        }

        // —— 快捷区（TA指令/LA指令）——
        // Radio 同父容器自动互斥，统一按 Name 分发。
        private void radioQuickTa_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            // 单选同组互斥，选中新指令取消旧态
            // 清底色统一在动作启动时执行
            if (!e.Value || _loadingProcessEnable || _suppressActionRadio) return;
            if (sender is AntdUI.Radio radio) ResetOtherRadioBacks(radio);
        }

        /// <summary>TA 指令派发：挂在 Click，选中态仅作显示</summary>

        /// <param name="sender">被点击的 Radio</param>
        /// <param name="e">事件参数</param>
        private void radioQuickTa_Click(object sender, EventArgs e)
        {
            if (_loadingProcessEnable) return;
            if (!(sender is AntdUI.Radio radio)) return;
            if (!radio.Checked) radio.Checked = true;   // 选中态由本处补齐，仅作显示
            // 点已选中的 radio 不触发 CheckedChanged ⇒ 此处补一次同组清色，
            // 保证「带底色者 = 当前选中者」在任何点法下都成立。
            ResetOtherRadioBacks(radio);
            RunInstruction(radio, "TA 送丝力矩轴", BuildTaActionDetail(radio),
                delegate { return ExecuteTaAction(radio); }, ref _lastTaRadio);
        }

        /// <summary>把 TA 六个指令按钮的派发统一挂到 Click</summary>
        private void WireTaInstructionClicks()
        {
            AntdUI.Radio[] radios = new AntdUI.Radio[]
            {
                radioQuickTaAuto, radioQuickTaBreak, radioQuickTaRetract,
                radioQuickTaPose1, radioQuickTaPose2, radioQuickTaZeroPose
            };
            for (int i = 0; i < radios.Length; i++)
            {
                if (radios[i] != null) radios[i].Click += radioQuickTa_Click;
            }
        }

        /// <summary>确认弹窗文案：力值/方向/基准/目标</summary>
        private static string BuildTaActionDetail(AntdUI.Radio radio)
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            MontionManager manager = MontionManager.Instance;
            DeltaTaHomeConfig home = manager?.TaHomeConfig;
            double force = cfg == null ? 0 : Math.Abs(cfg.TaForceN);
            int dir = home == null ? 1 : home.TorqueDirection;
            switch (radio.Name)
            {
                case "radioQuickTaAuto":
                    return string.Format("力值 {0} N / 方向 {1} / 100% 力量基准 {2} N（进入时自动切 CST）",
                        force.ToString("F1", CultureInfo.InvariantCulture), dir > 0 ? "+" : "-",
                        home == null ? 0 : home.Force100PctN);
                case "radioQuickTaPose1":
                    return "目标位置 " + (cfg == null ? 0 : cfg.TaPose1Mm).ToString("F3", CultureInfo.InvariantCulture) + " mm";
                case "radioQuickTaPose2":
                    return "目标位置 " + (cfg == null ? 0 : cfg.TaPose2Mm).ToString("F3", CultureInfo.InvariantCulture) + " mm";
                case "radioQuickTaZeroPose":
                    return "工作零位 " + (cfg == null ? 0 : cfg.TaZeroPoseMm).ToString("F3", CultureInfo.InvariantCulture) + " mm（工艺设置页坐标，CSP 绝对定位，非回零）";
                case "radioQuickTaBreak":
                    return "停止在 Break 停止位";
                case "radioQuickTaRetract":
                    return "力矩反向出力（与 TA_Auto 方向相反，保持出力不定位）";
                default:
                    return string.Empty;
            }
        }

        /// <summary>TA 指令执行体：先处理 ZeroPose 互斥停止，再按 Name 分发</summary>

        /// <returns>是否成功。</returns>
        private bool ExecuteTaAction(AntdUI.Radio radio)
        {
            // 同轴单飞：ZeroPose 未返回前预检即拒，不抢停
            switch (radio.Name)
            {
                case "radioQuickTaAuto": return DoTaAuto();
                case "radioQuickTaBreak": return DoTaBreak();
                case "radioQuickTaRetract": return DoTaRetract();
                case "radioQuickTaPose1": return DoTaPose1();
                case "radioQuickTaPose2": return DoTaPose2();
                case "radioQuickTaZeroPose": return DoTaZeroPose();
                default: return false;
            }
        }
        // LA 指令区含两套独立电机：对焦动作组（LA_Auto~LA_Pose2）与振镜组（GA_*）
        // 同处 gridQuickLa 一个区域（已拆平，不再分区容器）；组内天然互斥、跨组互不影响（可同时各选一个）。
        private void radioQuickLa_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (!e.Value || _loadingGalvoUi || _loadingProcessEnable || _suppressActionRadio) return;
            if (!(sender is AntdUI.Radio radio)) return;

            if (radio.Name.StartsWith("radioQuickLaGalvo"))
            {
                // 图形选择属配置项（非动作指令）：不弹确认、不上色
                WsgGalvoPattern? pattern = RadioToGalvoPattern(radio.Name);
                if (pattern == null)
                {
                    GlobalCommData.ShowLog("工艺设置", "振镜图形 Radio 未在映射表中 " + radio.Name, MessageLevel.Warning);
                    return;
                }
                SetGalvoPattern(pattern.Value);
                // 切换图形 ⇒ 载入该图形的分档参数（宽/长/频各不同）
                LoadGalvoShapeToInputs();
                return;
            }

            ResetOtherRadioBacks(radio);   // 互斥单色：取消上一个指令的状态
            RunInstruction(radio, "LA 焊接头轴", BuildLaActionDetail(radio),
                delegate { return ExecuteLaAction(radio); }, ref _lastLaRadio);
        }

        /// <summary>LA 指令确认弹窗的关键参数文案。</summary>
        /// <returns>结果文本。</returns>
        private static string BuildLaActionDetail(AntdUI.Radio radio)
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            switch (radio.Name)
            {
                case "radioQuickLaAuto":
                    return "按目标光斑自动跟随";
                case "radioQuickLaSpot1":
                    return "目标光斑位 1：" + (cfg == null ? 0 : cfg.LaSpot1Mm).ToString("F3", CultureInfo.InvariantCulture) + " mm";
                case "radioQuickLaSpot2":
                    return "目标光斑位 2：" + (cfg == null ? 0 : cfg.LaSpot2Mm).ToString("F3", CultureInfo.InvariantCulture) + " mm";
                case "radioQuickLaPose1":
                    return "目标姿态位 1：" + (cfg == null ? 0 : cfg.LaPose1Mm).ToString("F3", CultureInfo.InvariantCulture) + " mm";
                case "radioQuickLaDefaultFocus":
                    return "LA 默认焦平面：" + (cfg == null ? 0 : cfg.LaDefaultFocusMm).ToString("F3", CultureInfo.InvariantCulture) + " mm";
                default:
                    return string.Empty;
            }
        }

        /// <summary>LA 指令执行体（返回 false 表示未下发/下发失败）。</summary>
        /// <returns>是否成功。</returns>
        private bool ExecuteLaAction(AntdUI.Radio radio)
        {
            switch (radio.Name)
            {
                case "radioQuickLaAuto": return DoLaAuto();
                case "radioQuickLaSpot1": return DoLaSpot1();
                case "radioQuickLaSpot2": return DoLaSpot2();
                case "radioQuickLaPose1": return DoLaPose1();
                case "radioQuickLaDefaultFocus": return DoLaDefaultFocus();
                default: return false;
            }
        }
        /// <summary>Radio 名 → 振镜图形枚举；名称不匹配返回 null。</summary>
        /// <param name="name">名称。</param>
        /// <returns>对应图形，无匹配返回空</returns>
        private static WsgGalvoPattern? RadioToGalvoPattern(string name)
        {
            switch (name)
            {
                case "radioQuickLaGalvoCircle": return WsgGalvoPattern.Circle;
                case "radioQuickLaGalvoEight": return WsgGalvoPattern.Eight;
                case "radioQuickLaGalvoArc": return WsgGalvoPattern.Arc;
                case "radioQuickLaGalvoLine": return WsgGalvoPattern.Line;
                default: return null;
            }
        }

        /// <summary>振镜图形枚举 → 图形组 Radio；无对应图形返回 null。</summary>
        /// <param name="pattern">图形。</param>
        /// <returns>对应单选钮，无匹配返回空</returns>
        private AntdUI.Radio GalvoPatternToRadio(WsgGalvoPattern? pattern)
        {
            if (pattern == null) return null;
            switch (pattern.Value)
            {
                case WsgGalvoPattern.Circle: return radioQuickLaGalvoCircle;
                case WsgGalvoPattern.Eight: return radioQuickLaGalvoEight;
                case WsgGalvoPattern.Arc: return radioQuickLaGalvoArc;
                case WsgGalvoPattern.Line: return radioQuickLaGalvoLine;
                default: return null;
            }
        }

        // —— 激光工艺设置区（TA 参数区，无指令按钮）——
        // TA 工艺设置区为参数区（TA_Force / TA_Break停止位 / TA_Pose1 / TA_Pose2），
        // 指令统一走快捷区 Radio，本区不设按钮。

        /// <summary>从 WeldParamConfig 回填 TA 参数区（下压力 / Break停止位 / Pose1 / Pose2）。</summary>
        private void LoadTaParamUi()
        {
            var ta = WeldParamManager.Instance.AlgorithmsConfig;
            if (ta == null) return;
            // 工艺页只显示力的绝对值，不显示负号
            txtTaForce.Text = Math.Abs(ta.TaForceN).ToString("F1", CultureInfo.InvariantCulture);
            txtTaPose1.Text = ta.TaPose1Mm.ToString("F3", CultureInfo.InvariantCulture);
            txtTaPose2.Text = ta.TaPose2Mm.ToString("F3", CultureInfo.InvariantCulture);
            txtTaZeroPose.Text = ta.TaZeroPoseMm.ToString("F3", CultureInfo.InvariantCulture);
            var flow = MotionControlWorkflowInstance();   // TA 运行时状态归流程层 MotionControlWorkflow
            txtTaBreakPos.Text = flow.TaBreakHoldValid
                ? flow.TaBreakHoldMm.ToString("F3", CultureInfo.InvariantCulture)
                : "--";
        }

        /// <summary>激光工艺设置区·TA 参数：UI→内存（含 force 校验；返回 false 表示校验</summary>

        /// <returns>是否成功。</returns>
        private bool ApplyTaToConfig(WeldParamConfig ta)
        {
            if (!double.TryParse(txtTaForce.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double force))
            {
                AntdUI.Message.warn(this.FindForm(), "下压力大小请输入数字（单位 N）");
                return false;
            }
            // 工艺页只填大小：负号必须显式拒绝
            if (force < 0.0)
            {
                AntdUI.Message.warn(this.FindForm(), string.Format(
                    "此处只填力的大小（当前 {0:F2}N 为负），方向在『系统设置 → TA 电机 → 参数 → 扭矩方向』设置", force));
                return false;
            }
            // 100% 力量基准（ROOT/TA_HOME/TA_FORCE_100PCT_N）：力值按此换算成 CST 千分比
            DeltaTaHomeConfig taHome = MontionManager.Instance.TaHomeConfig;
            double force100 = taHome == null ? 0.0 : taHome.Force100PctN;
            if (!(force100 > 0.0))
            {
                AntdUI.Message.warn(this.FindForm(),
                    "100% 力量基准未配置或 ≤0（TA_FORCE_100PCT_N），请先在系统设置 → TA 电机 → 参数 填写大于 0 的基准值");
                return false;
            }
            if (force == 0.0)
            {
                AntdUI.Message.warn(this.FindForm(), "下压力大小不能为 0：0 无法出力，底层会判参数非法；方向请在 TA 参数弹窗单独设置");
                return false;
            }
            if (force > force100)
            {
                AntdUI.Message.warn(this.FindForm(), string.Format(
                    "下压力大小超出 100% 力量基准：当前 {0:F2}N，上限 {1:F2}N（超过额定能力不给静默限幅）", force, force100));
                return false;
            }
            short torque = (short)Math.Round(force / force100 * 1000.0, MidpointRounding.AwayFromZero);
            if (torque == 0)
            {
                AntdUI.Message.warn(this.FindForm(), string.Format(
                    "下压力大小 {0:F4}N 低于 1‰ 分辨率 {1:F4}N（基准 {2:F1}N），换算后为 0 不出力，请加大力值或核对基准",
                    force, force100 / 1000.0, force100));
                return false;
            }
            ta.TaForceN = force;   // 仅存大小（≥0），方向另存 ROOT/TA_HOME/TA_TORQUE_DIRECTION
            if (double.TryParse(txtTaPose1.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p1)) ta.TaPose1Mm = p1;
            if (double.TryParse(txtTaPose2.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p2)) ta.TaPose2Mm = p2;
            // TA 工作零位坐标：非法输入静默保留旧值
            if (double.TryParse(txtTaZeroPose.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var zp)) ta.TaZeroPoseMm = zp;
            return true;
        }

        // —— 激光工艺设置区（LA 参数区，无指令按钮）——
        // LA 设置与 TA 设置同形态，是「设置」不是指令动作

        /// <summary>从 WeldParamConfig 回填 LA 参数区（Spot1/Spot2 光斑 + Pose1/Pose2 调试位 + 目标光斑）。</summary>
        private void LoadLaParamUi()
        {
            var la = WeldParamManager.Instance.AlgorithmsConfig;
            if (la == null) return;
            txtLaSpot1.Text = la.LaSpot1Mm.ToString("F2", CultureInfo.InvariantCulture);
            txtLaSpot2.Text = la.LaSpot2Mm.ToString("F2", CultureInfo.InvariantCulture);
            txtLaPose1.Text = la.LaPose1Mm.ToString("F3", CultureInfo.InvariantCulture);
            txtLaDefaultFocus.Text = la.LaDefaultFocusMm.ToString("F3", CultureInfo.InvariantCulture);
            txtLaSpotTarget.Text = la.LaSpotTargetMm.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>激光工艺设置区·LA 参数：UI→内存（含 Spot1/Spot2/Target 校验；返回 false 表示校验</summary>

        /// <returns>是否成功。</returns>
        private bool ApplyLaToConfig(WeldParamConfig la)
        {
            if (!double.TryParse(txtLaSpot1.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double spot1) || spot1 <= 0.0)
            {
                AntdUI.Message.warn(this.FindForm(), "请输入有效的 LA Spot1 光斑(mm)");
                return false;
            }
            if (!double.TryParse(txtLaSpot2.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double spot2) || spot2 <= 0.0)
            {
                AntdUI.Message.warn(this.FindForm(), "请输入有效的 LA Spot2 光斑(mm)");
                return false;
            }
            if (!double.TryParse(txtLaSpotTarget.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double target) || target <= 0.0)
            {
                AntdUI.Message.warn(this.FindForm(), "请输入有效的目标光斑大小(mm)");
                return false;
            }
            la.LaSpot1Mm = spot1;
            la.LaSpot2Mm = spot2;
            la.LaSpotTargetMm = target;
            if (double.TryParse(txtLaPose1.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p1)) la.LaPose1Mm = p1;
            if (double.TryParse(txtLaDefaultFocus.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p2)) la.LaDefaultFocusMm = p2;
            return true;
        }

        /// <summary>回填 YT 设置区（标定中心 Y/Z + 示教 Y 轴锁定坐标）。</summary>
        private void LoadYtCalibUi()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            txtCalibCenterY.Text = cfg.CalibratedCenterY.ToString("F3", CultureInfo.InvariantCulture);
            txtCalibCenterZ.Text = cfg.CalibratedCenterZ.ToString("F3", CultureInfo.InvariantCulture);
            txtTeachLockY.Text = cfg.TeachLockYMm.ToString("F3", CultureInfo.InvariantCulture);
        }

        /// <summary>YT 设置区 UI→内存（解析失败保留原值）。</summary>
        /// <param name="cfg">目标配置对象。</param>
        private void ApplyYtCalibToConfig(WeldParamConfig cfg)
        {
            if (cfg == null) return;

            cfg.CalibratedCenterY = ParseCalibDouble(txtCalibCenterY, cfg.CalibratedCenterY);
            cfg.CalibratedCenterZ = ParseCalibDouble(txtCalibCenterZ, cfg.CalibratedCenterZ);
            cfg.TeachLockYMm = ParseCalibDouble(txtTeachLockY, cfg.TeachLockYMm);
        }

        #endregion

        #region 公共加载与解析

        /// <summary>TA 参数区是否已首次回填（两条路径幂等）</summary>
        private bool _taParamFilled;

        /// <summary>将本页参数读入输入控件（构造/页显示调用）。</summary>
        private void LoadAlgoConfigOnForm()
        {
            LoadFlowSwitchOnForm();
            LoadGalvoActionUi();
            LoadLaParamUi();
            LoadTaParamUi();   
            LoadYtCalibUi();
        }

        /// <summary>TA 参数区首次补载</summary>
        private void EnsureTaParamFilled()
        {
            if (_taParamFilled) return;
            if (WeldParamManager.Instance.AlgorithmsConfig == null) return;
            LoadTaParamUi();
            _taParamFilled = true;
        }

        /// <summary>焊参调整面板内的流程开关</summary>
        private void LoadFlowSwitchOnForm()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            txtFlowMaxConsecFail.Text = cfg.MaxConsecutiveFailures.ToString();
            txtFlowCountNum.Text = cfg.SegmentDistanceMm.ToString("F1");
        }

        /// <summary>解析标定输入的 double：解析失败回退原值。</summary>

        /// <returns>计算结果。</returns>
        private static double ParseCalibDouble(AntdUI.Input box, double fallback)
        {
            if (box == null) return fallback;
            string text = box.Text == null ? string.Empty : box.Text.Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return value;
            if (double.TryParse(text, out value)) return value;
            return fallback;
        }

        /// <summary>解析整型输入控件的数值：InvariantCulture 优先、当前文化回落，解</summary>

        /// <returns>结果码。</returns>
        private static int ParseInt(AntdUI.Input box, int fallback)
        {
            if (box == null) return fallback;
            string text = box.Text == null ? string.Empty : box.Text.Trim();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) return value;
            if (int.TryParse(text, out value)) return value;
            return fallback;
        }

        #endregion

        /// <summary>字符串输入对话框（AntdUI Modal + Input）：确认且输入非空返回 true。</summary>
        /// <param name="value">值。</param>
        /// <returns>是否成功。</returns>
        private bool ShowInputStringDialog(ref string value, string prompt)
        {
            AntdUI.Input inputBox = new AntdUI.Input
            {
                Text = value ?? "",
                Height = 40,
                Width = 300,
                Location = new System.Drawing.Point(0, 0),
            };

            string result = value ?? "";
            var config = new AntdUI.Modal.Config(
                this.FindForm() ?? System.Windows.Forms.Form.ActiveForm,
                prompt, inputBox, AntdUI.TType.Info)
            {
                OkText = "确认",
                CancelText = "取消",
                Width = 320,
                OnOk = cfg =>
                {
                    if (string.IsNullOrWhiteSpace(inputBox.Text))
                        return false;
                    result = inputBox.Text;
                    return true;
                }
            };
            if (AntdUI.Modal.open(config) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(result))
            {
                value = result;
                return true;
            }
            return false;
        }
    }
}


