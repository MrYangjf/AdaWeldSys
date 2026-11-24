using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.UIDesign.SubDialog.MotionControlDialog;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.WeldParamControl.Curves;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.TabPage
{
    /// <summary>系统设置页：跟踪/光丝/温控/模拟测试四区。</summary>
    public partial class SystemControlPage : UserControl
    {
        #region 常量

        private const string LogTag = "系统设置";
        private const double Epsilon = 0.001;

        /// <summary>跟踪系统只渲染 Y 向轴：轴名关键字命中即纳入</summary>
        private static readonly string[] TraceAxisKeys = { "Y", "横梁", "跟踪" };

        /// <summary>表内参数列占位符号（控制按钮固定在页内）</summary>
        private const string SymParam = "⚙";

        #endregion

        #region 行模型

        /// <summary>轴行模型：配置列存于 Config，运行状态由 CellBadge 呈现</summary>
        public class AxisRow
        {
            public string AxisName { get; set; }
            public string Position { get; set; }
            public CellBadge PositiveLight { get; set; }
            public CellBadge NegativeLight { get; set; }
            public CellBadge OriginLight { get; set; }
            public CellBadge AlarmLight { get; set; }
            public CellBadge ServoLight { get; set; }
            public string LayoutValue { get; set; }
            public string TargetPos { get; set; }
            public CellLink[] ParamBtn { get; set; }
            public DeltaAxisConfig Config { get; set; }
            /// <summary>最近一次运动指令反馈（函数名/错误码）。</summary>
            public string Feedback { get; set; }
        }

        #endregion

        #region 私有变量

        private readonly AntdUI.AntList<AxisRow> _axisRows;

        /// <summary>TA 表行集合（单行；与 Y 轴表共用同列定义、同刷新入口 FillAxisRuntime）</summary>
        private readonly AntdUI.AntList<AxisRow> _taRows;

        private bool _autoRefresh;
        private bool _jogging;

        /// <summary>回填期间抑制开关勾选副作用，避免误落盘</summary>
        private bool _loadingProcessEnable;

        private AntdUI.TooltipComponent _tooltip;

        #endregion

        #region 模拟流程启动状态（事件驱动）

        /// <summary>模拟流程是否异常终止（按钮显示「清理异常」）</summary>
        private bool _simError;

        /// <summary>模拟是否正在停止中（防止重复点击停止按钮）</summary>
        private bool _simStopping;

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

        #region 构造函数

        public SystemControlPage()
        {
            InitializeComponent();
            _axisRows = new AntdUI.AntList<AxisRow>();
            _taRows = new AntdUI.AntList<AxisRow>();

            InitTables();
            BindEvents();
            LoadAxisRows();
            LoadTaAxisRow();
            LoadAlgoThreshOnForm();
            LoadTimeoutOnForm();
            LoadProcessEnableOnForm();
            PopulateComboBoxItems();
            LoadFromModel();
            LoadOpticsFromConfig();
            LoadTempFromConfig();
            ApplyPermissionGate();
            GlobalCommData.OperateLevelChanged += OnOperateLevelChanged;
            // 开关退回/恢复由连接态事件统一判定。
            MotionControlWorkflow.Instance.StateSwitched += OnMotionStateSwitched;
            DeviceControlWork.Instance.InitializationCompleted += OnInitializationCompleted;
            DeviceControlWork.Instance.InitializationSummaryReady += OnInitializationSummaryReady;
            MotionControlWorkflow.Instance.AxisReadyChanged += OnAxisReadyChanged;
            LaserWeldHeadWorkflow.Instance.AxisReadyChanged += OnAxisReadyChanged;
            this.HandleDestroyed += SystemControlPage_HandleDestroyed;
            // 温度/气压与轴表共用计时器（常驻 500ms；轴表刷新仍受自动刷新开关控制）
            timerRefresh.Start();
            // 按钮初始态由统一管控给出
            RefreshActionButtonStates();
        }

        /// <summary>权限门控：激光头参数页仅管理员可见。</summary>
        private void ApplyPermissionGate()
        {
            bool admin = PermissionLevel.IsAdmin(GlobalCommData.mOperateLevel);
            tabPageLaserHeadParam.Visible = admin;
            panelOpticsSys.Enabled = admin;
            panelTempSys.Enabled = admin;

            // 门控后若正停留在被隐藏页，回退到首个可见页
            if (!admin && tabsSystem.SelectedIndex == LaserHeadTabIndex)
            {
                tabsSystem.SelectedIndex = 0;
            }
        }

        /// <summary>切页二次校验：非管理员直接选中管理员专属页</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void tabsSystem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PermissionLevel.IsAdmin(GlobalCommData.mOperateLevel)) return;
            if (tabsSystem.SelectedIndex != LaserHeadTabIndex) return;
            if (!EnsureAdminForWrite("进入激光头系统参数设置页"))
            {
                tabsSystem.SelectedIndex = 0;
            }
        }

        /// <summary>写权限二次校验：管理员专属参数写入前必须过</summary>

        /// <param name="action">写入动作描述，用于提示。</param>
        /// <returns>true 表示允许写入。</returns>
        private bool EnsureAdminForWrite(string action)
        {
            if (PermissionLevel.IsAdmin(GlobalCommData.mOperateLevel)) return true;
            string msg = "当前权限为" + PermissionLevel.GetDisplayName(GlobalCommData.mOperateLevel)
                       + "，无权" + action + "。该操作仅管理员可执行。";
            AntdUI.Message.warn(this.FindForm(), msg);
            Log("权限拒绝 " + action + "（当前等级 " + GlobalCommData.mOperateLevel + "）", MessageLevel.Warning);
            return false;
        }

        /// <summary>权限等级变更（登录/注销）回调：重算本页门控。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="level">日志级别。</param>
        private void OnOperateLevelChanged(object sender, OperateLevel level)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnOperateLevelChanged(sender, level)));
                return;
            }
            ApplyPermissionGate();
        }

        #endregion

        #region 日志 / 配置源

        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(LogTag, message, level);
        }

        private static DeltaMotionConfig GetConfig()
        {
            MontionManager manager = MontionManager.Instance;
            if (manager == null || manager.MoveData == null) return null;
            return manager.MoveData.MotionConfig;
        }

        private static bool IsMotionReady()
        {
            MontionManager manager = MontionManager.Instance;
            return manager != null && manager.IsInitialized;
        }

        #endregion

        #region 单元格渲染辅助

        /// <summary>有信号/无信号两态徽标：on = Success(绿)，off = Error(红)；alarmSemantics 翻转语义（报警灯 on=红）</summary>
        private static CellBadge MakeLight(bool on, bool alarmSemantics)
        {
            AntdUI.TState state = on
                ? (alarmSemantics ? AntdUI.TState.Error : AntdUI.TState.Success)
                : (alarmSemantics ? AntdUI.TState.Success : AntdUI.TState.Error);
            return new CellBadge(state, string.Empty);
        }

        /// <summary>未连接 / 未装配 / 未配置的统一灰色徽标（TState.Default）</summary>
        private static CellBadge MakeLightGray()
        {
            return new CellBadge(AntdUI.TState.Default, string.Empty);
        }

        #endregion

        #region 表格初始化

        private void InitTables()
        {
            // Y 轴表与 TA 表共用同一份列定义，禁止各写一套
            tableAxis.Columns = CreateAxisColumns();
            tableAxis.EditMode = TEditMode.DoubleClick;
            tableAxis.AutoSizeColumnsMode = ColumnsMode.Fill;
            tableAxis.Binding(_axisRows);
            tableAxis.EmptyHeader = true;
            tableAxis.VisibleHeader = true;

            tableTa.Columns = CreateAxisColumns();
            tableTa.EditMode = TEditMode.DoubleClick;
            tableTa.AutoSizeColumnsMode = ColumnsMode.Fill;
            tableTa.Binding(_taRows);
            tableTa.EmptyHeader = true;
            tableTa.VisibleHeader = true;
        }

        /// <summary>构建轴表列定义</summary>

        /// <returns>轴表列集合（每次调用新建，两个表各持一份实例）</returns>
        private static ColumnCollection CreateAxisColumns()
        {
            return new ColumnCollection
            {
                new Column("AxisName", "轴名") { Width = "120" },
                new Column("Position", "轴位置") { Width = "100" },
                new Column("PositiveLight", "正限") { Width = "50" },
                new Column("NegativeLight", "负限") { Width = "50" },
                new Column("OriginLight", "原点") { Width = "50" },
                new Column("AlarmLight", "报警") { Width = "50" },
                new Column("ServoLight", "使能") { Width = "50" },
                new Column("LayoutValue", "步距") { Width = "80" }.SetEditable(true),
                new Column("TargetPos", "移动目标") { Width = "100" }.SetEditable(true),
                new Column("ParamBtn", "参数") { Width = "50" },
                new Column("Feedback", "运动指令反馈") { Width = "220" }
            };
        }

        private void BindEvents()
        {
            tableAxis.CellButtonClick += TableAxis_CellButtonClick;
            tableAxis.CellEndEdit += TableAxis_CellEndEdit;
            // TA 表与 Y 轴表同构：同一派发、同一回写
            tableTa.CellButtonClick += TableTa_CellButtonClick;
            tableTa.CellEndEdit += TableAxis_CellEndEdit;

            // 轴控制固定按钮（页内固定按钮：
            // 统一走 MontionManager 单轴函数，本页固定跟踪轴）
            ServoBtn.Click += FixedServo_Click;
            HomeBtn.Click += FixedHome_Click;
            StopBtn.Click += FixedStop_Click;
            MoveToBtn.Click += FixedMoveTo_Click;
            StepBackBtn.Click += FixedStepBack_Click;
            StepFwdBtn.Click += FixedStepFwd_Click;
            NegBackBtn.MouseDown += FixedNegBack_MouseDown;
            NegFwdBtn.MouseDown += FixedNegFwd_MouseDown;
            NegBackBtn.MouseUp += FixedJog_MouseUp;
            NegFwdBtn.MouseUp += FixedJog_MouseUp;
            NegBackBtn.MouseLeave += FixedJog_MouseLeave;
            NegFwdBtn.MouseLeave += FixedJog_MouseLeave;

            // TA 送丝力矩电机固定按钮（与跟踪轴控制完全同构：
            // 同 8 键、同 MontionManager 六函数、同点动安全兜底）
            TaServoBtn.Click += TaServo_Click;
            TaHomeBtn.Click += TaHome_Click;
            TaStopBtn.Click += TaStop_Click;
            TaMoveToBtn.Click += TaMoveTo_Click;
            TaStepBackBtn.Click += TaStepBack_Click;
            TaStepFwdBtn.Click += TaStepFwd_Click;
            TaNegBackBtn.MouseDown += TaNegBack_MouseDown;
            TaNegFwdBtn.MouseDown += TaNegFwd_MouseDown;
            TaNegBackBtn.MouseUp += TaNegBack_MouseUp;
            TaNegFwdBtn.MouseUp += TaNegFwd_MouseUp;
            TaNegBackBtn.MouseLeave += TaNegBack_MouseLeave;
            TaNegFwdBtn.MouseLeave += TaNegFwd_MouseLeave;

            // 切页二次校验（管理员专属页）
            tabsSystem.SelectedIndexChanged += tabsSystem_SelectedIndexChanged;

            // 轴表自动刷新开关，共用激光头前置条件
            // 配置编辑后的重载走 OpenAxisEditor，运行态由本开关按拍刷新
            swAutoRefresh.CheckedChanged += SwAutoRefresh_CheckedChanged;
            _autoRefresh = swAutoRefresh.Checked;

            // 跟踪设置统一读存三按钮（收口）
            button1.Click += BtnReadLocal_Click;
            button2.Click += BtnApplyParams_Click;
            button3.Click += BtnSaveLocal_Click;

            // 固定按钮 ToolTip（AntdUI.TooltipComponent 单实例；只有图标无文字需补语义）
            _tooltip = new AntdUI.TooltipComponent();
            _tooltip.SetTip(ServoBtn, "伺服使能/关闭切换");
            _tooltip.SetTip(HomeBtn, "回零（非阻塞，立即返回）");
            _tooltip.SetTip(MoveToBtn, "绝对移动到目标位置（取轴表首行「移动目标」列）");
            _tooltip.SetTip(NegFwdBtn, "正向连续点动（按住持续移动，松开停止）");
            _tooltip.SetTip(StepFwdBtn, "正向步进（步距取轴表首行「步距」列）");
            _tooltip.SetTip(StopBtn, "减速停止（可中断点动）");
            _tooltip.SetTip(StepBackBtn, "负向步进（步距取轴表首行「步距」列）");
            _tooltip.SetTip(NegBackBtn, "负向连续点动（按住持续移动，松开停止）");

            // TA 固定按钮 ToolTip（与跟踪轴同语义，目标/步距取 TA 表列）
            _tooltip.SetTip(TaServoBtn, "TA 伺服使能/关闭切换");
            _tooltip.SetTip(TaHomeBtn, "TA 回零（非阻塞，立即返回）");
            _tooltip.SetTip(TaMoveToBtn, "TA 绝对移动到 TA 表「移动目标」列");
            _tooltip.SetTip(TaNegFwdBtn, "TA 正向连续点动（按住持续移动，松开停止）");
            _tooltip.SetTip(TaStepFwdBtn, "TA 正向步进（步距取 TA 表「步距」列）");
            _tooltip.SetTip(TaStopBtn, "TA 减速停止（可中断点动）");
            _tooltip.SetTip(TaStepBackBtn, "TA 负向步进（步距取 TA 表「步距」列）");
            _tooltip.SetTip(TaNegBackBtn, "TA 负向连续点动（按住持续移动，松开停止）");

            // LA 新增按钮（后退/前进由 Designer 订阅，此处不重复绑定）
            btnOptMotorMoveTo.Click += BtnOptMotorMoveTo_Click;
            btnOptMotorEnable.Click += BtnOptMotorEnable_Click;
            btnOptGalvoEnable.Click += BtnOptGalvoEnable_Click;
            _tooltip.SetTip(btnOptMotorEnable, "LA 对焦电机使能/撤销使能切换");
            _tooltip.SetTip(btnOptMotorMoveTo, "LA 绝对移动到「移动目标」输入值");
            _tooltip.SetTip(btnOptGalvoEnable, "振镜使能/撤销使能切换（焊接中由摆幅自动控制，不可手动切换）");

            // 自动刷新开关：TA / LA / 振镜三区共用 timerRefresh，按区分控；初值以设计器为准
            swTARefresh.CheckedChanged += SwTARefresh_CheckedChanged;
            swLARefresh.CheckedChanged += SwLARefresh_CheckedChanged;
            swGalvoRefresh.CheckedChanged += SwGalvoRefresh_CheckedChanged;
            _taAutoRefresh = swTARefresh.Checked;
            _laAutoRefresh = swLARefresh.Checked;
            _galvoAutoRefresh = swGalvoRefresh.Checked;

            timerRefresh.Tick += TimerRefresh_Tick;
            timerRefresh.Interval = FlowTimeoutSettings.UiRefreshMs;   // 30Hz 刷屏默认 33ms
        }

        #endregion

        #region 数据加载

        /// <summary>跟踪页轴表只渲染 Y 向电机轴。</summary>
        private void LoadAxisRows()
        {
            // 先快照旧行的用户输入，重建行时回填
            Dictionary<string, AxisRow> previous = new Dictionary<string, AxisRow>();
            foreach (AxisRow old in _axisRows)
            {
                if (old == null || string.IsNullOrEmpty(old.AxisName)) continue;
                previous[old.AxisName] = old;
            }

            _axisRows.Clear();
            DeltaMotionConfig config = GetConfig();
            if (config == null) return;
            foreach (DeltaAxisConfig axis in config.Axes)
            {
                if (!IsTraceAxis(axis)) continue;
                AxisRow row = new AxisRow
                {
                    AxisName = axis.AxisName,
                    Position = "--",
                    Config = axis,
                    LayoutValue = "1",
                    TargetPos = "0",
                    ParamBtn = new CellLink[] { new CellButton(System.Guid.NewGuid().ToString(), SymParam, AntdUI.TTypeMini.Default) }
                };
                if (previous.TryGetValue(axis.AxisName ?? string.Empty, out AxisRow old))
                {
                    row.LayoutValue = old.LayoutValue;
                    row.TargetPos = old.TargetPos;
                }
                FillAxisRuntime(row);
                _axisRows.Add(row);
            }
        }

        /// <summary>判断轴是否属于跟踪系统（Y 向电机轴）：轴名命中任</summary>
        /// <param name="axis">轴对象。</param>

        /// <returns>是否成功。</returns>
        private static bool IsTraceAxis(DeltaAxisConfig axis)
        {
            if (axis == null || string.IsNullOrEmpty(axis.AxisName)) return false;
            string name = axis.AxisName.ToUpperInvariant();
            foreach (string key in TraceAxisKeys)
            {
                if (name.Contains(key.ToUpperInvariant())) return true;
            }
            return false;
        }

        /// <summary>重建 TA（送丝力矩电机）单行；保留用户已填的「步距 / 移</summary>
        private void LoadTaAxisRow()
        {
            AxisRow previous = _taRows.Count > 0 ? _taRows[0] : null;
            _taRows.Clear();
            DeltaMotionConfig config = GetConfig();
            if (config == null) return;
            DeltaAxisConfig taCfg = config.GetAxis(config.TaAxisName);
            if (taCfg == null) return;

            AxisRow row = new AxisRow
            {
                AxisName = taCfg.AxisName,
                Position = "--",
                Config = taCfg,
                LayoutValue = "1",
                TargetPos = "0",
                ParamBtn = new CellLink[] { new CellButton(System.Guid.NewGuid().ToString(), SymParam, AntdUI.TTypeMini.Default) }
            };
            if (previous != null)
            {
                row.LayoutValue = previous.LayoutValue;
                row.TargetPos = previous.TargetPos;
            }
            FillAxisRuntime(row);
            _taRows.Add(row);
        }

        /// <summary>取 TA 表行（TA 单行表；未装配时返回 null）</summary>
        /// <returns>TA 轴行或 null</returns>
        private AxisRow GetTaRow()
        {
            return _taRows.Count > 0 ? _taRows[0] : null;
        }

        #endregion

        #region 运行状态填充

        private void FillAxisRuntime(AxisRow row)
        {
            if (row == null) return;
            if (!IsMotionReady() || row.Config == null)
            {
                row.Position = "--";
                row.ServoLight = MakeLightGray();
                row.AlarmLight = MakeLightGray();
                row.PositiveLight = MakeLightGray();
                row.NegativeLight = MakeLightGray();
                row.OriginLight = MakeLightGray();
                row.Feedback = string.Empty;
                return;
            }
            ApplyAxisReadout(row, CollectAxisReadout(row.Config));
        }

        /// <summary>采集单轴只读快照</summary>
        private static AxisReadoutSnapshot CollectAxisReadout(DeltaAxisConfig config)
        {
            AxisReadoutSnapshot snapshot = new AxisReadoutSnapshot();
            if (config == null) return snapshot;
            snapshot.AxisName = config.AxisName;
            try
            {
                // 只读摘要统一走 Manager（A-2）：UI 不持轴对象、不解析状态字位
                if (!MontionManager.Instance.TryGetAxisReadout(config.AxisName,
                        out double pos, out string lastCommand, out bool servo, out bool alarm))
                {
                    return snapshot;
                }
                snapshot.Available = true;
                snapshot.PositionMm = pos;
                snapshot.LastCommand = lastCommand;
                snapshot.Servo = servo;
                snapshot.Alarm = alarm;
            }
            catch (Exception)
            {
                snapshot.Failure = "读取失败";
            }
            return snapshot;
        }

        /// <summary>采集单轴快照（单配置入口）。</summary>
        private static List<AxisReadoutSnapshot> CollectAxisReadouts(DeltaAxisConfig config)
        {
            List<AxisReadoutSnapshot> list = new List<AxisReadoutSnapshot>();
            if (config != null) list.Add(CollectAxisReadout(config));
            return list;
        }

        /// <summary>采集多轴快照（配置列表入口，可在后台线程执行）。</summary>
        private static List<AxisReadoutSnapshot> CollectAxisReadouts(List<DeltaAxisConfig> configs)
        {
            List<AxisReadoutSnapshot> list = new List<AxisReadoutSnapshot>();
            if (configs == null) return list;
            foreach (DeltaAxisConfig config in configs)
            {
                if (config != null) list.Add(CollectAxisReadout(config));
            }
            return list;
        }

        /// <summary>把单轴快照贴到行模型。</summary>
        private void ApplyAxisReadout(AxisRow row, AxisReadoutSnapshot snapshot)
        {
            if (row == null || snapshot == null) return;
            if (!string.IsNullOrEmpty(snapshot.Failure))
            {
                row.Position = snapshot.Failure;
                return;
            }
            if (!snapshot.Available)
            {
                row.Position = "未装配";
                row.ServoLight = MakeLightGray();
                row.AlarmLight = MakeLightGray();
                row.PositiveLight = MakeLightGray();
                row.NegativeLight = MakeLightGray();
                row.OriginLight = MakeLightGray();
                row.Feedback = string.Empty;
                return;
            }
            row.Feedback = snapshot.LastCommand;
            row.Position = snapshot.PositionMm.ToString("F3", CultureInfo.InvariantCulture);
            DeltaAxisConfig config = row.Config;
            bool posLim = config != null && snapshot.PositionMm >= config.PositiveLimit - Epsilon;
            bool negLim = config != null && snapshot.PositionMm <= config.NegativeLimit + Epsilon;
            bool origin = Math.Abs(snapshot.PositionMm) <= Epsilon;
            row.ServoLight = MakeLight(snapshot.Servo, false);
            row.AlarmLight = MakeLight(snapshot.Alarm, true);
            row.PositiveLight = MakeLight(posLim, false);
            row.NegativeLight = MakeLight(negLim, false);
            row.OriginLight = MakeLight(origin, false);
        }

        #endregion

        #region 单元格编辑回写

        private bool TableAxis_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            if (!(e.Record is AxisRow row) || e.Value == null) return true;
            string key = e.Column == null ? string.Empty : e.Column.Key;
            string val = e.Value.ToString().Trim();
            if (key == "LayoutValue")
            {
                if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    AntdUI.Message.warn(this.FindForm(), "步距必须为数值");
                    return false;
                }
                row.LayoutValue = val;
                return true;
            }
            if (key == "TargetPos")
            {
                if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    AntdUI.Message.warn(this.FindForm(), "指定坐标必须为数值");
                    return false;
                }
                row.TargetPos = val;
                return true;
            }
            return true;
        }

        #endregion

        #region 单元格按钮派发

        /// <summary>Y 轴表参数按钮：打开通用轴参数编辑器</summary>
        /// <param name="sender">发送方</param>
        /// <param name="e">表格按钮事件（e.Record 为轴行）</param>
        private void TableAxis_CellButtonClick(object sender, AntdUI.TableButtonEventArgs e)
        {
            if (!(e.Record is AxisRow row) || row.Config == null) return;
            if (e.Btn.Text == SymParam)
            {
                OpenAxisEditor(row);
            }
            RefreshRuntime();
        }

        /// <summary>TA 表参数按钮：打开扭矩回零参数弹窗</summary>

        /// <param name="sender">发送方</param>
        /// <param name="e">表格按钮事件（e.Record 为 TA 行）</param>
        private void TableTa_CellButtonClick(object sender, AntdUI.TableButtonEventArgs e)
        {
            if (e.Btn == null || e.Btn.Text != SymParam) return;
            OpenTaAxisEditor();
        }

        /// <summary>TA 轴参数弹窗（通用区 + TA 特有区两段）。</summary>
        private void OpenTaAxisEditor()
        {
            if (!EnsureAdminForWrite("修改 TA 轴参数")) return;
            DeltaAxisConfig taCfg = MontionManager.Instance.TaAxisConfig;
            DeltaTaHomeConfig taHome = MontionManager.Instance.TaHomeConfig;
            if (taCfg == null || taHome == null)
            {
                Warn("TA 轴参数不可用（运控配置未加载）");
                return;
            }

            using (AxisConfigForm editor = new AxisConfigForm(taCfg, taHome))
            {
                // 配置已写回，此处负责落盘推回
                // 不用弹窗返回值判定
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "TA 轴参数 - " + taCfg.AxisName, editor, AntdUI.TType.Info) { CloseIcon = true, BtnHeight = 0 });
                if (!editor.Committed) return;
                if (!MontionManager.Instance.SaveTaHomeConfig(taHome))
                {
                    AntdUI.Message.error(this.FindForm(), "TA 轴参数保存失败，详见日志");
                    return;
                }
                Log(string.Format("TA 轴参数已保存（通用区落 AXIS，回零六参落 ROOT/TA_HOME） 逻辑模式 {0} → 下发值 {1}（16 位补码）",
                    taHome.HomeMode, (ushort)(taHome.HomeMode & 0xFFFF)));
                RefreshTaMotorStatus();
            }
        }

        #endregion

        #region 固定按钮（2026-10-19 自表格迁出；ADR-057 控制统一走 MontionManager · 本页固定轴=跟踪轴）

        /// <summary>跟踪轴手动操作下发</summary>

        /// <param name="operation">操作种类</param>
        /// <param name="value">MoveTo 目标 / StepMove 位移（其余传 0）</param>
        /// <param name="dir">JogStart 方向（1=负向 0=正向）</param>
        /// <param name="axisName">命中轴名（日志用）</param>
        /// <param name="servoOn">ToggleServo 后的使能状态</param>
        /// <param name="commandOk">指令下发结果</param>
        /// <param name="rejectReason">前置拒绝原因（空串表示前置通过）</param>
        /// <returns>前置是否通过</returns>
        private static bool TryTraceOperation(AxisManualOperation operation, double value, ushort dir,
            out string axisName, out bool servoOn, out bool commandOk, out string rejectReason)
        {
            rejectReason = string.Empty;
            return MotionControlWorkflow.Instance.TryManualAxisOperation(TraceAxisKeys, operation, value, dir,
                out axisName, out servoOn, out commandOk, out rejectReason);
        }

        /// <summary>由手动操作下发结果构造执行结果</summary>
        /// <param name="gatePassed">前置是否通过</param>
        /// <param name="rejectReason">前置拒绝原因</param>
        /// <param name="commandOk">指令下发结果</param>
        /// <param name="axisName">命中轴名</param>
        /// <param name="failReason">下发失败时的原因文案</param>
        /// <returns>执行结果</returns>
        private static ManualOpOutcome BuildOutcome(bool gatePassed, string rejectReason, bool commandOk,
            string axisName, string failReason)
        {
            if (!gatePassed) return new ManualOpOutcome { Ok = false, Reason = rejectReason };
            if (!commandOk) return new ManualOpOutcome { Ok = false, Reason = failReason, AxisName = axisName };
            return new ManualOpOutcome { Ok = true, AxisName = axisName };
        }

        /// <summary>取角色轴当前坐标（mm；未装配或读取失败返回 false）。</summary>
        /// <param name="roleKeys">角色关键字</param>
        /// <param name="positionMm">当前坐标</param>
        /// <param name="axisName">命中轴名</param>
        /// <returns>读取成功返回 true</returns>
        private static bool TryGetAxisPositionMm(string[] roleKeys, out double positionMm, out string axisName)
        {
            positionMm = 0.0;
            axisName = string.Empty;
            MontionManager manager = MontionManager.Instance;
            if (manager == null) return false;
            if (!manager.TryGetAxisNameByKeys(roleKeys, out axisName)) return false;
            return manager.TryGetAxisReadout(axisName, out positionMm, out _, out _, out _);
        }

        /// <summary>固定按钮取值行（本页轴表唯一跟踪轴行：步距/移</summary>
        private AxisRow GetFixedRow()
        {
            return _axisRows.Count > 0 ? _axisRows[0] : null;
        }

        private void FixedServo_Click(object sender, EventArgs e)
        {
            StartManualOperation(ManualOpGroup.Trace, "跟踪轴伺服使能切换", () =>
            {
                bool gate = TryTraceOperation(AxisManualOperation.ToggleServo, 0, 0,
                    out string axisName, out bool on, out bool commandOk, out string rejectReason);
                return BuildOutcome(gate, rejectReason, commandOk, axisName, "伺服使能未生效");
            });
        }

        private void FixedHome_Click(object sender, EventArgs e)
        {
            // 手动回零走统一入口
            StartManualOperation(ManualOpGroup.Trace, "跟踪轴回零", () =>
            {
                bool ok = MotionControlWorkflow.Instance.TryHomeAxisWithWait(TraceAxisKeys, "跟踪轴回零",
                    out string axisName, out string rejectCode, out string rejectDetail, out string evidence);
                string reason = string.IsNullOrEmpty(rejectDetail) ? rejectCode : rejectDetail;
                return new ManualOpOutcome { Ok = ok, Reason = ok ? string.Empty : reason, AxisName = axisName };
            });
        }

        private void FixedStop_Click(object sender, EventArgs e)
        {
            // 停止同步下发一次
            ResetJogFlag();
            if (!TryTraceOperation(AxisManualOperation.Stop, 0, 0,
                    out string axisName, out _, out _, out string rejectReason))
            {
                Log("跟踪轴 停止被拒 " + rejectReason, MessageLevel.Warning);
                return;
            }
            Log(axisName + " 已下发减速停止");
        }

        private void FixedMoveTo_Click(object sender, EventArgs e)
        {
            AxisRow row = GetFixedRow();
            if (row == null) return;
            double target = ParseTarget(row);
            if (double.IsNaN(target)) return;
            StartManualOperation(ManualOpGroup.Trace, "跟踪轴移动到 " + target.ToString("F3", CultureInfo.InvariantCulture), () =>
            {
                bool ok = MotionControlWorkflow.Instance.TryMoveAxisToTarget(TraceAxisKeys, target, "跟踪轴绝对移动",
                    out string axisName, out string rejectCode, out string rejectDetail, out string evidence);
                string reason = string.IsNullOrEmpty(rejectDetail) ? rejectCode : rejectDetail;
                return new ManualOpOutcome { Ok = ok, Reason = ok ? string.Empty : reason, AxisName = axisName };
            });
        }

        private void FixedStepBack_Click(object sender, EventArgs e)
        {
            FixedStep(-1);
        }

        private void FixedStepFwd_Click(object sender, EventArgs e)
        {
            FixedStep(1);
        }

        /// <summary>步进方向 sign：-1 后退 / +1 前进，步距取轴表首行「步距」列。</summary>
        private void FixedStep(int sign)
        {
            AxisRow row = GetFixedRow();
            if (row == null) return;
            double delta = sign * ParseStep(row, 1.0);
            StartManualOperation(ManualOpGroup.Trace, "跟踪轴相对移动 " + delta.ToString("F3", CultureInfo.InvariantCulture), () =>
            {
                // 相对步进换算为绝对移动
                if (!TryGetAxisPositionMm(TraceAxisKeys, out double currentMm, out string axisName))
                {
                    return new ManualOpOutcome { Ok = false, Reason = "轴未装配或位置读取失败" };
                }
                bool ok = MotionControlWorkflow.Instance.TryMoveAxisToTarget(TraceAxisKeys, currentMm + delta,
                    "跟踪轴相对移动", out axisName, out string rejectCode, out string rejectDetail, out string evidence);
                string reason = string.IsNullOrEmpty(rejectDetail) ? rejectCode : rejectDetail;
                return new ManualOpOutcome { Ok = ok, Reason = ok ? string.Empty : reason, AxisName = axisName };
            });
        }

        private void FixedNegBack_MouseDown(object sender, MouseEventArgs e)
        {
            FixedJog(1);
        }

        private void FixedNegFwd_MouseDown(object sender, MouseEventArgs e)
        {
            FixedJog(0);
        }

        /// <summary>按下固定负向/正向按钮：以轴配置速度剖面启动</summary>
        /// <param name="dir">目录路径。</param>
        private void FixedJog(ushort dir)
        {
            // 点动按钮须保持接收松开事件
            if (IsGroupBusy(ManualOpGroup.Trace))
            {
                Warn("上一条操作尚未完成，请等待完成后再点动");
                return;
            }
            // 界面按钮属手动动作，置手动模式
            MotionControlWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            if (!TryTraceOperation(AxisManualOperation.JogStart, 0, dir,
                    out string axisName, out _, out _, out string rejectReason))
            {
                Log("跟踪轴 点动被拒 " + rejectReason, MessageLevel.Warning);
                return;
            }
            _jogging = true;
            Log(axisName + (dir == 1 ? " 负向" : " 正向") + "点动已下发（按住中）");
        }

        private void FixedJog_MouseUp(object sender, MouseEventArgs e)
        {
            StopJog();
        }

        /// <summary>鼠标移出按钮的安全兜底：若仍在点动则停止（应</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void FixedJog_MouseLeave(object sender, EventArgs e)
        {
            if (_jogging) StopJog();
        }

        /// <summary>停止当前连续点动并复位状态。</summary>
        private void StopJog()
        {
            if (!_jogging) return;
            try
            {
                if (TryTraceOperation(AxisManualOperation.JogStop, 0, 0,
                        out string axisName, out _, out _, out string rejectReason))
                    Log(axisName + " 点动已停止");
            }
            catch (Exception ex)
            {
                Log("跟踪轴 点动停止下发异常 " + ex.GetType().Name + " " + ex.Message, MessageLevel.Warning);
            }
            _jogging = false;
        }

        /// <summary>只复位点动标志</summary>
        private void ResetJogFlag()
        {
            _jogging = false;
        }

        #endregion

        #region 手动操作执行器（后台执行 + 执行消息 + 防呆）

        /// <summary>手动操作分组（每台电机一组，互不相干）</summary>
        private enum ManualOpGroup
        {
            /// <summary>Y 跟踪轴</summary>
            Trace,
            /// <summary>TA 送丝力矩轴</summary>
            Ta,
            /// <summary>LA 对焦电机</summary>
            La,
            /// <summary>GA 振镜电机</summary>
            Galvo
        }

        /// <summary>各电机的手动操作在途标志</summary>
        private volatile bool _traceOpBusy;
        private volatile bool _taOpBusy;
        private volatile bool _laOpBusy;
        private volatile bool _galvoOpBusy;

        /// <summary>取某电机组的手动操作在途标志</summary>
        /// <param name="group">电机组</param>
        /// <returns>该组有操作在途返回 true</returns>
        private bool IsGroupBusy(ManualOpGroup group)
        {
            switch (group)
            {
                case ManualOpGroup.Trace: return _traceOpBusy;
                case ManualOpGroup.Ta: return _taOpBusy;
                case ManualOpGroup.La: return _laOpBusy;
                default: return _galvoOpBusy;
            }
        }

        /// <summary>置某电机组的手动操作在途标志</summary>
        /// <param name="group">电机组</param>
        /// <param name="busy">true=有操作在途</param>
        private void SetGroupBusy(ManualOpGroup group, bool busy)
        {
            switch (group)
            {
                case ManualOpGroup.Trace: _traceOpBusy = busy; break;
                case ManualOpGroup.Ta: _taOpBusy = busy; break;
                case ManualOpGroup.La: _laOpBusy = busy; break;
                default: _galvoOpBusy = busy; break;
            }
        }

        /// <summary>振镜从站就绪且无报警</summary>
        private bool _galvoStationReady = true;

        /// <summary>手动动作是否允许（自动焊接流程在跑 ⇒ 禁）。</summary>
        private bool _manualActionAllowed = true;

        /// <summary>手动操作执行结果（后台线程产出，UI 线程消费）</summary>
        private sealed class ManualOpOutcome
        {
            /// <summary>是否成功</summary>
            public bool Ok;
            /// <summary>失败原因（可读文本）</summary>
            public string Reason;
            /// <summary>命中轴名（日志用）</summary>
            public string AxisName;
            /// <summary>UI 回填动作（可选；在 UI 线程执行，参数为是否成功）</summary>
            public Action<bool> UiCallback;
        }

        /// <summary>启动一次手动操作：控件立即置不可用</summary>
        /// <param name="group">电机组（防呆与按钮禁用只在本组内生效）</param>
        /// <param name="actionLabel">动作名（日志与提示用）</param>
        /// <param name="exec">后台执行体，返回执行结果</param>
        private void StartManualOperation(ManualOpGroup group, string actionLabel, Func<ManualOpOutcome> exec)
        {
            if (IsGroupBusy(group))
            {
                Warn("上一条操作尚未完成，请等待完成后再操作");
                return;
            }
            if (_jogging || _taJogging)
            {
                Warn("点动进行中，请先松开点动按钮");
                return;
            }
            SetGroupBusy(group, true);
            RefreshActionButtonStates();
            // 界面按钮切换手动模式
            MotionControlWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            Thread worker = new Thread(() =>
            {
                ManualOpOutcome outcome;
                try
                {
                    outcome = exec() ?? new ManualOpOutcome { Ok = false, Reason = "执行未返回结果" };
                }
                catch (Exception ex)
                {
                    outcome = new ManualOpOutcome { Ok = false, Reason = ex.GetType().Name + " " + ex.Message };
                }
                try
                {
                    BeginInvoke(new Action(() => FinishManualOperation(group, actionLabel, outcome)));
                }
                catch (Exception)
                {
                    SetGroupBusy(group, false);   // 页面已销毁：放开本组标记
                }
            })
            {
                IsBackground = true,
                Name = "ManualOp_" + actionLabel
            };
            worker.Start();
        }

        /// <summary>手动操作完成（UI 线程）</summary>
        /// <param name="group">电机组</param>
        /// <param name="actionLabel">动作名</param>
        /// <param name="outcome">执行结果</param>
        private void FinishManualOperation(ManualOpGroup group, string actionLabel, ManualOpOutcome outcome)
        {
            if (IsDisposed || Disposing) return;
            SetGroupBusy(group, false);
            RefreshActionButtonStates();
            if (outcome.UiCallback != null)
            {
                try { outcome.UiCallback(outcome.Ok); }
                catch (Exception) { /* 回填失败不影响主流程 */ }
            }
            if (outcome.Ok)
            {
                Log((string.IsNullOrEmpty(outcome.AxisName) ? string.Empty : outcome.AxisName + " ")
                    + actionLabel + " 完成", MessageLevel.Info);
            }
            else
            {
                string reason = string.IsNullOrEmpty(outcome.Reason) ? "未知原因" : outcome.Reason;
                Log(actionLabel + " 失败：" + reason, MessageLevel.Warning);
                AntdUI.Message.warn(this.FindForm(), actionLabel + " 失败：" + reason);
            }
            RefreshRuntime();
            RefreshLaserHeadRuntime();
        }

        /// <summary>手动操作按钮统一管控（单一控制函数）</summary>
        private void RefreshActionButtonStates()
        {
            // 焊接中禁用全部手动动作
            bool manualAllowed = _manualActionAllowed;
            bool traceIdle = manualAllowed && !IsGroupBusy(ManualOpGroup.Trace);
            bool taIdle = manualAllowed && !IsGroupBusy(ManualOpGroup.Ta);
            bool laIdle = manualAllowed && !IsGroupBusy(ManualOpGroup.La);
            bool galvoIdle = manualAllowed && !IsGroupBusy(ManualOpGroup.Galvo);

            SetControlEnabled(ServoBtn, traceIdle);
            SetControlEnabled(HomeBtn, traceIdle);
            SetControlEnabled(MoveToBtn, traceIdle);
            SetControlEnabled(StepBackBtn, traceIdle);
            SetControlEnabled(StepFwdBtn, traceIdle);
            SetControlEnabled(NegBackBtn, traceIdle);
            SetControlEnabled(NegFwdBtn, traceIdle);
            SetControlEnabled(TaServoBtn, taIdle);
            SetControlEnabled(TaHomeBtn, taIdle);
            SetControlEnabled(TaMoveToBtn, taIdle);
            SetControlEnabled(TaStepBackBtn, taIdle);
            SetControlEnabled(TaStepFwdBtn, taIdle);
            SetControlEnabled(TaNegBackBtn, taIdle);
            SetControlEnabled(TaNegFwdBtn, taIdle);
            SetControlEnabled(btnOptMotorEnable, laIdle);
            SetControlEnabled(btnOptMotorBack, laIdle);
            SetControlEnabled(btnOptMotorFwd, laIdle);
            SetControlEnabled(btnOptMotorMoveTo, laIdle);
            SetControlEnabled(btnOptGalvoEnable, galvoIdle && _galvoStationReady);
            SetControlEnabled(btnOptGalvoHome, galvoIdle);
            // 停止键始终可用
            SetControlEnabled(StopBtn, true);
            SetControlEnabled(TaStopBtn, true);
        }

        /// <summary>差分设可用性：值没变不碰控件。</summary>
        /// <param name="control">目标控件</param>
        /// <param name="value">目标可用性</param>
        private static void SetControlEnabled(Control control, bool value)
        {
            if (control == null) return;
            if (control.Enabled == value) return;
            control.Enabled = value;
        }

        /// <summary>刷新激光头区显示信息（振镜 / 对焦电机）。</summary>
        private void RefreshLaserHeadRuntime()
        {
            if (IsDisposed || Disposing) return;
            RefreshGalvoStatus();
            RefreshLaMotorStatus();
        }

        #endregion

        #region 轴动作辅助

        private double ParseStep(AxisRow row, double fallback)
        {
            if (double.TryParse(row.LayoutValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return fallback;
        }

        private double ParseTarget(AxisRow row)
        {
            if (double.TryParse(row.TargetPos, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            AntdUI.Message.warn(this.FindForm(), "请先在「指定坐标输入」填写有效数值");
            return double.NaN;
        }

        #endregion

        #region 轴参数编辑器

        private void OpenAxisEditor(AxisRow row)
        {
            if (row.Config == null) return;
            using (AxisConfigForm editor = new AxisConfigForm(row.Config))
            {
                // 配置已写回，此处只负责落盘
                // 不用弹窗返回值判定
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "轴参数 - " + row.Config.AxisName, editor, AntdUI.TType.Info) { CloseIcon = true, BtnHeight = 0 });
                if (!editor.Committed) return;
                try
                {
                    MontionManager.Instance.MoveData.MotionConfig.SaveConfig();
                    Log("轴参数已更新并保存 " + row.Config.AxisName);
                }
                catch (Exception ex)
                {
                    Log("轴参数保存失败 " + row.Config.AxisName + " " + ex.Message, MessageLevel.Error);
                    AntdUI.Message.error(this.FindForm(), "轴参数保存失败：" + ex.Message);
                }
                LoadAxisRows();
            }
        }

        #endregion

        #region 跟踪系统参数：轨迹点设定

        /// <summary>轨迹点设定区：按点过滤 / 方向平滑 / CSP 输出 / 折返监测</summary>
        private void LoadAlgoThreshOnForm()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            txtGeoRobotYFilterAlpha.Text = cfg.RobotYFilterAlpha.ToString("F3");
            txtGeoMaxMapPoints.Text = cfg.MaxMapPoints.ToString();
            txtGeoGaussianSigma.Text = cfg.GaussianSigmaMm.ToString("F3");
            txtGeoGaussianHalfWindow.Text = cfg.GaussianHalfWindowMm.ToString("F3");
            txtFlowNearbyWindow.Text = cfg.NearbyWindowMm.ToString("F3");
            txtFlowOutlierSigmaK.Text = cfg.OutlierSigmaK.ToString("F3");
            txtFlowOutlierSlope.Text = cfg.OutlierSlopeLimitMmPerMm.ToString("F4");
            txtFlowMaxStepChange.Text = cfg.MaxStepChangeMm.ToString("F3");
            txtFlowMaxTargetStep.Text = cfg.MaxTargetStepMm.ToString("F3");
            txtFlowConsecReject.Text = cfg.ConsecutiveRejectFrames.ToString();

            txtFlowMonoDeadband.Text = cfg.MonotonicDeadbandMm.ToString("F3");
            txtFlowMonoRelease.Text = cfg.MonotonicReleaseMm.ToString("F3");
            txtFlowMonoWindow.Text = cfg.MonotonicWindowMm.ToString("F3");

            txtFlowOutDeadband.Text = cfg.OutputDeadbandMm.ToString("F3");
            txtFlowMaxVel.Text = cfg.MaxVelMmPerS.ToString("F3");
            txtFlowMaxAcc.Text = cfg.MaxAccMmPerS2.ToString("F3");
            txtFlowCspMinSeg.Text = cfg.CspMinSegMm.ToString("F3");

            txtFlowReverseAlarm.Text = cfg.ReverseAlarmMm.ToString("F3");
            txtFlowReverseDeadband.Text = cfg.ReverseDeadbandMm.ToString("F4");

            // 前馈安全系数 + 单调方向锁（轨迹点过滤区）
            txtParamFfSafety.Text = cfg.FeedforwardSafetyFactor.ToString("F3");
            selParamMono.SelectedIndex = cfg.EnableMonotonicConstraint ? 0 : 1;
        }

        /// <summary>轨迹跟踪参数设置页：标定角度 + 跟踪几何 4 项。</summary>
        private void LoadLaserCalibOnForm()
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            txtCalibAngle.Text = cfg.LaserToHorizontalAngleDeg.ToString("F3");
            // 标定项存于几何段
            txtGeoAxisPerLaser.Text = cfg.AxisMmPerLaserMm.ToString("F3");
            selGeoGuideSign.SelectedIndex = cfg.GuideSign >= 0 ? 0 : 1;
            txtGeoLeadDistance.Text = cfg.LeadDistanceMm.ToString("F3");
            txtGeoRobotYGain.Text = cfg.RobotYGain.ToString("F3");
        }

        /// <summary>回填「流程等待超时」区</summary>
        private void LoadTimeoutOnForm()
        {
            var cfg = LineLaserManager.Instance.Config;
            if (cfg != null)
            {
                txtDataStable.Text = cfg.DataStableTimeoutSeconds.ToString();
                txtWaitWorking.Text = cfg.WaitWorkingTimeoutSeconds.ToString();
                txtFirstResult.Text = cfg.FirstResultTimeoutSeconds.ToString();
            }

            var flow = MontionManager.Instance.FlowTimeouts;
            if (flow != null)
            {
                txtInstrDetect.Text = flow.InstructionDetectSeconds.ToString("F1", CultureInfo.InvariantCulture);
                txtInstrSample.Text = flow.InstructionSampleMs.ToString();
                txtMoveDone.Text = flow.MoveDoneSeconds.ToString();
                txtHomeDone.Text = flow.HomeDoneSeconds.ToString();
                txtServoOn.Text = flow.ServoOnSeconds.ToString();
                txtZeroPoseWait.Text = flow.PositionReachResultWaitSeconds.ToString("F0", CultureInfo.InvariantCulture);
                txtMasterInit.Text = flow.MasterInitSeconds.ToString();
                // 设备侧等待：秒与毫秒两值独立，不得混用
                txtCameraAcquire.Text = flow.CameraAcquireSeconds.ToString("F1", CultureInfo.InvariantCulture);
                txtRecordFlush.Text = flow.RecordFlushSeconds.ToString("F1", CultureInfo.InvariantCulture);
                txtLineLaserCallback.Text = flow.LineLaserCallbackMs.ToString();
                txtModbusConnect.Text = flow.ModbusConnectMs.ToString();
                txtModbusRW.Text = flow.ModbusReadWriteMs.ToString();
            }
        }

        #region 跟踪设置统一读存（2026-10-19 底部三按钮收口，替代四子区 6 个读取/保存按钮）

        /// <summary>读取本地参数：配置文件→UI 全量回填（轨迹点设定 + 线</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnReadLocal_Click(object sender, EventArgs e)
        {
            WeldParamManager.Instance.LoadAlgorithmsZone(WeldParamConfig.AlgorithmsZone.System);
            LineLaserManager.Instance.LoadConfig();
            LoadAlgoThreshOnForm();
            LoadLaserCalibOnForm();
            LoadTimeoutOnForm();
            AntdUI.Message.info(this.FindForm(), "已读取本地参数");
        }

        /// <summary>修改应用参数：UI→内存配置（不落盘；「保存参数」按钮再</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnApplyParams_Click(object sender, EventArgs e)
        {
            WeldParamConfig cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            // 轨迹点设定 4 组（按点过滤 / 方向平滑 / CSP 输出 / 折返监测）
            cfg.RobotYFilterAlpha = ParseCalibDouble(txtGeoRobotYFilterAlpha, cfg.RobotYFilterAlpha);
            cfg.MaxMapPoints = ParseInt(txtGeoMaxMapPoints, cfg.MaxMapPoints);
            cfg.GaussianSigmaMm = ParseCalibDouble(txtGeoGaussianSigma, cfg.GaussianSigmaMm);
            cfg.GaussianHalfWindowMm = ParseCalibDouble(txtGeoGaussianHalfWindow, cfg.GaussianHalfWindowMm);
            cfg.NearbyWindowMm = ParseCalibDouble(txtFlowNearbyWindow, cfg.NearbyWindowMm);
            cfg.OutlierSigmaK = ParseCalibDouble(txtFlowOutlierSigmaK, cfg.OutlierSigmaK);
            cfg.OutlierSlopeLimitMmPerMm = ParseCalibDouble(txtFlowOutlierSlope, cfg.OutlierSlopeLimitMmPerMm);
            cfg.MaxStepChangeMm = ParseCalibDouble(txtFlowMaxStepChange, cfg.MaxStepChangeMm);
            cfg.MaxTargetStepMm = ParseCalibDouble(txtFlowMaxTargetStep, cfg.MaxTargetStepMm);
            cfg.ConsecutiveRejectFrames = ParseInt(txtFlowConsecReject, cfg.ConsecutiveRejectFrames);

            cfg.MonotonicDeadbandMm = ParseCalibDouble(txtFlowMonoDeadband, cfg.MonotonicDeadbandMm);
            cfg.MonotonicReleaseMm = ParseCalibDouble(txtFlowMonoRelease, cfg.MonotonicReleaseMm);
            cfg.MonotonicWindowMm = ParseCalibDouble(txtFlowMonoWindow, cfg.MonotonicWindowMm);

            cfg.OutputDeadbandMm = ParseCalibDouble(txtFlowOutDeadband, cfg.OutputDeadbandMm);
            cfg.MaxVelMmPerS = ParseCalibDouble(txtFlowMaxVel, cfg.MaxVelMmPerS);
            cfg.MaxAccMmPerS2 = ParseCalibDouble(txtFlowMaxAcc, cfg.MaxAccMmPerS2);
            cfg.CspMinSegMm = ParseCalibDouble(txtFlowCspMinSeg, cfg.CspMinSegMm);

            cfg.ReverseAlarmMm = ParseCalibDouble(txtFlowReverseAlarm, cfg.ReverseAlarmMm);
            cfg.ReverseDeadbandMm = ParseCalibDouble(txtFlowReverseDeadband, cfg.ReverseDeadbandMm);

            // 前馈安全系数 + 单调方向锁（轨迹点过滤区）
            cfg.FeedforwardSafetyFactor = ParseCalibDouble(txtParamFfSafety, cfg.FeedforwardSafetyFactor);
            WeldParamManager.Instance.EnableMonotonicConstraint = selParamMono.SelectedIndex == 0;

            // 线激光标定角度
            cfg.LaserToHorizontalAngleDeg = ParseCalibDouble(txtCalibAngle, cfg.LaserToHorizontalAngleDeg);

            // 标定项存于几何段
            cfg.AxisMmPerLaserMm = ParseCalibDouble(txtGeoAxisPerLaser, cfg.AxisMmPerLaserMm);
            cfg.GuideSign = selGeoGuideSign.SelectedIndex == 0 ? 1 : -1;
            cfg.LeadDistanceMm = ParseCalibDouble(txtGeoLeadDistance, cfg.LeadDistanceMm);
            cfg.RobotYGain = ParseCalibDouble(txtGeoRobotYGain, cfg.RobotYGain);

            // 等待超时三项（LineLaserConfig 内存对象，随「保存参数」一并落盘）
            var ll = LineLaserManager.Instance.Config;
            if (ll != null)
            {
                ll.DataStableTimeoutSeconds = ParseInt(txtDataStable, ll.DataStableTimeoutSeconds);
                ll.WaitWorkingTimeoutSeconds = ParseInt(txtWaitWorking, ll.WaitWorkingTimeoutSeconds);
                ll.FirstResultTimeoutSeconds = ParseInt(txtFirstResult, ll.FirstResultTimeoutSeconds);
            }

            // 流程等待六项+设备侧五项，随保存参数落盘
            var flow = MontionManager.Instance.FlowTimeouts;
            if (flow != null)
            {
                flow.InstructionDetectSeconds = ParseCalibDouble(txtInstrDetect, flow.InstructionDetectSeconds);
                flow.InstructionSampleMs = ParseInt(txtInstrSample, flow.InstructionSampleMs);
                flow.MoveDoneSeconds = ParseInt(txtMoveDone, flow.MoveDoneSeconds);
                flow.HomeDoneSeconds = ParseInt(txtHomeDone, flow.HomeDoneSeconds);
                flow.ServoOnSeconds = ParseInt(txtServoOn, flow.ServoOnSeconds);
                flow.PositionReachResultWaitSeconds = ParseCalibDouble(txtZeroPoseWait, flow.PositionReachResultWaitSeconds);
                flow.MasterInitSeconds = ParseInt(txtMasterInit, flow.MasterInitSeconds);
                // 设备侧等待五项（只做可见可配，不改变时序行为；非法输入回退原值）
                flow.CameraAcquireSeconds = ParseCalibDouble(txtCameraAcquire, flow.CameraAcquireSeconds);
                flow.RecordFlushSeconds = ParseCalibDouble(txtRecordFlush, flow.RecordFlushSeconds);
                flow.LineLaserCallbackMs = ParseInt(txtLineLaserCallback, flow.LineLaserCallbackMs);
                flow.ModbusConnectMs = ParseInt(txtModbusConnect, flow.ModbusConnectMs);
                flow.ModbusReadWriteMs = ParseInt(txtModbusRW, flow.ModbusReadWriteMs);
            }

            AntdUI.Message.success(this.FindForm(), "参数已应用到内存（尚未落盘，点「保存参数」写入本地）");
        }

        /// <summary>保存参数至本地：内存→文件（①System 区）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnSaveLocal_Click(object sender, EventArgs e)
        {
            WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.System);
            MontionManager.Instance.ApplyFlowTimeouts();   // 流程超时：校验 → 发布运行期快照 → 落盘
            LineLaserManager.Instance.SaveConfig();
            AntdUI.Message.success(this.FindForm(), "参数已保存至本地");
        }

        #endregion

        /// <summary>解析标定输入控件的 double 值：校验失败回退原值（复用</summary>

        /// <returns>计算结果。</returns>
        private static double ParseCalibDouble(AntdUI.Input box, double fallback)
        {
            return TryReadDouble(box, out double value) ? value : fallback;
        }

        /// <summary>解析整型输入控件的数值：校验失败回退原值（复</summary>

        /// <returns>结果码。</returns>
        private static int ParseInt(AntdUI.Input box, int fallback)
        {
            return TryReadInt(box, out int value) ? value : fallback;
        }

        #endregion

        #region 工艺启用开关（自工艺设置页迁入）

        /// <summary>回填 5 个工艺启用开关</summary>
        private void LoadProcessEnableOnForm()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            _loadingProcessEnable = true;
            try
            {
                swEnableSeamTrace.Checked = cfg.EnableSeamTrace;
                swEnableGalvoWeaveQuick.Checked = cfg.EnableGalvoWeave;
                swEnableAdapt.Checked = cfg.EnableGuide;
                swEnableWireAlign.Checked = cfg.EnableWireAlign;
                swEnableTempCtrl.Checked = cfg.EnableTempCtrl;
            }
            finally
            {
                _loadingProcessEnable = false;
            }
        }

        /// <summary>写回 5 个工艺启用开关并落盘（②′工艺启用管理区）。</summary>
        private void SaveProcessEnable()
        {
            var cfg = WeldParamManager.Instance.AlgorithmsConfig;
            if (cfg == null) return;

            cfg.EnableSeamTrace = swEnableSeamTrace.Checked;
            cfg.EnableGalvoWeave = swEnableGalvoWeaveQuick.Checked;
            cfg.EnableGuide = swEnableAdapt.Checked;
            cfg.EnableWireAlign = swEnableWireAlign.Checked;
            cfg.EnableTempCtrl = swEnableTempCtrl.Checked;

            WeldParamManager.Instance.SaveAlgorithmsZone(WeldParamConfig.AlgorithmsZone.Flow);
            GlobalCommData.ShowLog("工艺设置", "工艺启用管理已保存", MessageLevel.Info);
            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "工艺启用管理已保存");
        }

        /// <summary>振镜摆动开关：工艺摆动的唯一 UI 入口。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void swEnableGalvoWeaveQuick_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingProcessEnable) return;
            SaveProcessEnable();
        }

        /// <summary>焊缝跟踪启用开关：勾选即生效并落盘。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void swEnableSeamTrace_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingProcessEnable) return;
            SaveProcessEnable();
        }

        /// <summary>自适应引导启用开关：勾选即生效并落盘。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void swEnableAdapt_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingProcessEnable) return;
            SaveProcessEnable();
        }

        /// <summary>光丝对中启用开关：勾选即生效并落盘。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void swEnableWireAlign_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingProcessEnable) return;
            SaveProcessEnable();
        }

        /// <summary>温度控制启用开关：勾选即生效并落盘。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void swEnableTempCtrl_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingProcessEnable) return;
            SaveProcessEnable();
        }

        #endregion

        #region 模拟测试

        private void PopulateComboBoxItems()
        {
            cmbFreq.Items.Add("30 Hz");
            cmbFreq.Items.Add("60 Hz");
            cmbFreq.Items.Add("80 Hz");
            cmbFreq.Items.Add("120 Hz");
        }

        private void LoadFromModel()
        {
            var sc = DeviceControlWork.Instance;
            int idx = FrequencyToIndex(sc.SimLaserFrequencyHz);
            if (idx >= 0 && idx < cmbFreq.Items.Count)
                cmbFreq.SelectedIndex = idx;
            else
                cmbFreq.SelectedIndex = 0;
            numPreWorkStartX.Value = (decimal)sc.SimPreWorkStartXmm;
        }

        /// <summary>线激光数据频率 → 下拉索引（默认 30Hz）。</summary>
        /// <returns>结果码。</returns>
        private static int FrequencyToIndex(int hz)
        {
            switch (hz)
            {
                case 60: return 1;
                case 80: return 2;
                case 120: return 3;
                default: return 0;
            }
        }

        /// <summary>下拉索引 → 线激光数据频率。</summary>
        /// <param name="index">索引。</param>
        /// <returns>结果码。</returns>
        private static int IndexToFrequency(int index)
        {
            switch (index)
            {
                case 1: return 60;
                case 2: return 80;
                case 3: return 120;
                default: return 30;
            }
        }

        private void BtnSaveSim_Click(object sender, EventArgs e)
        {
            SyncSimParamsToController();
            DeviceControlWork.Instance.SaveSimulation();
            AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "模拟参数已保存");
        }

        /// <summary>将界面模拟参数同步到 DeviceControlWork（不含保存提示和</summary>
        private void SyncSimParamsToController()
        {
            int idx = cmbFreq.SelectedIndex;
            if (idx >= 0)
                DeviceControlWork.Instance.SimLaserFrequencyHz = IndexToFrequency(idx);
            DeviceControlWork.Instance.SimPreWorkStartXmm = (double)numPreWorkStartX.Value;
        }

        private void BtnStartSim_Click(object sender, EventArgs e)
        {
            var sc = DeviceControlWork.Instance;

            if (_simStopping)
            {
                AntdUI.Message.info(this.FindForm(), "模拟正在停止中，请稍候...");
                return;
            }

            if (_simError
                || (sc.IsSimulationEnabled && LineLaserWorkflow.Instance.WeldStatus == SubDeviceWeldStatus.ErrorAborted)
                || (sc.IsSimulationEnabled && sc.Status == MainDeviceStatus.Alarm))
            {
                CleanupSimError();
                return;
            }

            if (sc.IsSimulationEnabled)
            {
                _simStopping = true;
                UnsubscribeLineLaserEvents();
                StopSimulationFlows();
                sc.DisableSimulation("用户手动停止", hasError: false, isAuto: false);
                return;
            }

            // 模拟启动仅管理员
            if (!EnsureAdminForWrite("开启模拟测试")) return;

            SyncSimParamsToController();

            sc.StartSimulation();

            sc.SimulationStopped -= OnSimulationStopped;
            sc.SimulationStopped += OnSimulationStopped;

            sc.StatusChanged -= OnMainDeviceStatusChanged;
            sc.StatusChanged += OnMainDeviceStatusChanged;

            SubscribeLineLaserEvents();

            // 模拟启动属界面动作，置手动模式
            LineLaserWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            MotionControlWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);

            // 线激光/运控的启动改由 DeviceControlWork 复位完成后驱动：
            // 模拟须先自复位（TA 回零 + Y 工作零位）才进 PreWork，
            // 若此处立即 Start 会与复位链抢轴，导致 Y 未复位即回归标定位越界
            if (!_simError) SetSimButtonMode(SimButtonMode.Stop);
        }

        /// <summary>关闭模拟时反向停止三套流程（逐包 try/catch，忽略非运行</summary>
        private void StopSimulationFlows()
        {
            // 模拟停止属界面动作，置手动模式
            LineLaserWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            MotionControlWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            try { DeviceControlWork.Instance.StopSimulationLineLaser(); } catch { }   // 退出页 best-effort 停机，失败不阻断界面销毁
            try { MotionControlWorkflow.Instance.Stop(); } catch { }   // 同上，失败不阻断界面销毁
        }

        /// <summary>模拟停止事件回调（统一处理按钮复位）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnSimulationStopped(object sender, SimulationStoppedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnSimulationStopped(this, e)));
                return;
            }

            _simStopping = false;
            DeviceControlWork.Instance.SimulationStopped -= OnSimulationStopped;
            DeviceControlWork.Instance.StatusChanged -= OnMainDeviceStatusChanged;
            UnsubscribeLineLaserEvents();

            if (e.HasError)
            {
                ShowSimErrorDialog(string.Format("模拟异常停止：{0}", e.Reason));
                SetSimButtonMode(SimButtonMode.Cleanup);
            }
            else
            {
                ShowSimStopMessage(e.Reason);
                SetSimButtonMode(SimButtonMode.Start);
            }

            LogSimStopSummary(e.Reason);
        }

        /// <summary>线激光状态变更回调（事件驱动，仅消费结果）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnLineLaserStateChanged(object sender, WeldStatusChangedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnLineLaserStateChanged(this, e)));
                return;
            }

            // 焊接态变化重算按钮可用性
            // 构造期不读设备单例
            _manualActionAllowed = !DeviceControlWork.Instance.IsAutoWeldingRunning;
            RefreshActionButtonStates();

            if (e.NewStatus == SubDeviceWeldStatus.ErrorAborted)
            {
                string errMsg = string.Format("模拟流程异常终止：{0}", string.IsNullOrEmpty(e.Reason) ? "未知原因" : e.Reason);
                ShowSimErrorDialog(errMsg);
            }
            else if (e.NewStatus == SubDeviceWeldStatus.Stopping
                && e.Reason != null && e.Reason.Contains("焊缝行程完成"))
            {
                StopSimOnWeldComplete();
            }
        }

        /// <summary>主设备运行态变更回调（事件驱动，仅消费结果）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnMainDeviceStatusChanged(object sender, MainDeviceStatusChangedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnMainDeviceStatusChanged(this, e)));
                return;
            }

            // 运行态变化重算按钮可用性
            RefreshActionButtonStates();

            if (e.NewStatus != MainDeviceStatus.Alarm) return;
            if (!DeviceControlWork.Instance.IsSimulationEnabled) return;

            string alarmMsg = string.Format("模拟测试报警（{0}）：整机进入报警态，请点击「清理异常」清除报警",
                string.IsNullOrEmpty(e.Reason) ? "未知原因" : e.Reason);
            ShowSimErrorDialog(alarmMsg);
        }

        /// <summary>模拟异常统一提示（需用户确认）：记录日志 + 在 UI 线程</summary>
        /// <param name="msg">消息内容。</param>
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

        /// <summary>正常停止提示（无需确认）：记录日志 + 在 UI 线程弹出 Message 提</summary>
        private void ShowSimStopMessage(string reason)
        {
            string tip = string.Format("模拟测试已停止：{0}", reason);
            GlobalCommData.ShowLog("模拟流程", tip, MessageLevel.Info);
            if (this.InvokeRequired)
                this.BeginInvoke(new Action<string>(m => AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, m)), tip);
            else
                AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, tip);
        }

        /// <summary>停止原因确认信息：汇总各流程/设备最终状态，给</summary>
        private void LogSimStopSummary(string reason)
        {
            var ll = LineLaserWorkflow.Instance.WeldStatus;
            var v3 = MotionControlWorkflow.Instance.WeldStatus;
            var mon = MonitorCameraWorkflow.Instance.WeldStatus;
            var sim = DeviceControlWork.Instance;
            string simState = sim.IsSimulationEnabled ? "运行中" : "已关闭";
            string summary = string.Format(
                "模拟测试已停止 | 原因：{0} | 线激光：{1} | V3跟踪：{2} | 监控：{3} | 模拟模式：{4}",
                reason, ll, v3, mon, simState);
            GlobalCommData.ShowLog("模拟流程", summary, MessageLevel.Info);
        }

        /// <summary>订阅线激光状态变更（贯穿整个模拟流程生命周</summary>
        private void SubscribeLineLaserEvents()
        {
            LineLaserWorkflow.Instance.WeldStatusChanged -= OnLineLaserStateChanged;
            LineLaserWorkflow.Instance.WeldStatusChanged += OnLineLaserStateChanged;
        }

        /// <summary>取消线激光状态变更订阅</summary>
        private void UnsubscribeLineLaserEvents()
        {
            LineLaserWorkflow.Instance.WeldStatusChanged -= OnLineLaserStateChanged;
        }

        /// <summary>设置模拟流程按钮三态：开启 / 停止 / 清理异常（红色</summary>
        private void SetSimButtonMode(SimButtonMode mode)
        {
            switch (mode)
            {
                case SimButtonMode.Stop:
                    btnStartSim.Text = "停止";
                    btnStartSim.Ghost = false;
                    btnStartSim.Type = AntdUI.TTypeMini.Error;
                    _simError = false;
                    break;
                case SimButtonMode.Cleanup:
                    btnStartSim.Text = "清理异常";
                    btnStartSim.Ghost = false;
                    btnStartSim.Type = AntdUI.TTypeMini.Error;
                    _simError = true;
                    break;
                case SimButtonMode.Start:
                default:
                    btnStartSim.Text = "开启模拟流程";
                    btnStartSim.Ghost = true;
                    btnStartSim.Type = AntdUI.TTypeMini.Default;
                    btnStartSim.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
                    _simError = false;
                    break;
            }
        }

        /// <summary>异常终止时按钮转红色「清理异常」</summary>
        private void ShowSimErrorButton()
        {
            SetSimButtonMode(SimButtonMode.Cleanup);
        }

        /// <summary>焊缝正常完成：停止整段模拟流程（含运控/监控）。</summary>
        private void StopSimOnWeldComplete()
        {
            UnsubscribeLineLaserEvents();
            StopSimulationFlows();
            try { DeviceControlWork.Instance.ResetSimulationLineLaser(); }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "焊缝完成后复位工作流失败 " + ex.Message, MessageLevel.Error);
            }
            DeviceControlWork.Instance.DisableSimulation("焊缝行程完成", hasError: false, isAuto: true);
        }

        /// <summary>清理异常并复位：停止全部流程、复位线激光工作</summary>
        private void CleanupSimError()
        {
            UnsubscribeLineLaserEvents();
            DeviceControlWork.Instance.SimulationStopped -= OnSimulationStopped;
            DeviceControlWork.Instance.StatusChanged -= OnMainDeviceStatusChanged;

            try
            {
                StopSimulationFlows();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清理异常-停止流程失败 " + ex.Message, MessageLevel.Error);
            }
            try
            {
                DeviceControlWork.Instance.ResetSimulationLineLaser();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清理异常-复位工作流失败 " + ex.Message, MessageLevel.Error);
            }

            try
            {
                if (DeviceControlWork.Instance.Status == MainDeviceStatus.Alarm)
                {
                    DeviceControlWork.Instance.ClearAlarm();
                    GlobalCommData.ShowLog("模拟流程", "已清除主设备报警", MessageLevel.Info);
                }
                // 清警后显式复位：回工作零位再允许重焊（N01/N11）
                if (DeviceControlWork.Instance.ResetMachine())
                {
                    GlobalCommData.ShowLog("模拟流程", "已下发设备复位 回工作零位", MessageLevel.Info);
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清除报警失败 " + ex.Message, MessageLevel.Error);
            }

            try
            {
                DeviceControlWork.Instance.DisableSimulation("清理异常复位", hasError: false, isAuto: false);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("模拟流程", "清理异常-关闭模拟失败 " + ex.Message, MessageLevel.Error);
            }
            finally
            {
                _simStopping = false;
                SetSimButtonMode(SimButtonMode.Start);
                ShowSimStopMessage("异常清理恢复");
                LogSimStopSummary("异常清理恢复");
            }
        }

        #endregion

        #region 激光头系统参数（四区：温度 / 振镜 / TA-LA / 光丝对中标定曲线）

        /// <summary>振镜自动刷新开关（与轴表共用 timerRefresh，不新增计时器）。</summary>
        private bool _galvoAutoRefresh;

        /// <summary>LA 对焦电机自动刷新开关（与轴表共用计时器）。</summary>
        private bool _laAutoRefresh;

        /// <summary>TA 送丝力矩电机自动刷新开关（与轴表共用计时器）。</summary>
        private bool _taAutoRefresh;

        /// <summary>程序回写刷新开关时抑制勾选事件。</summary>
        private bool _syncingRefreshSwitch;

        /// <summary>轴读数签名（轴名 → 位置/使能/报警/最近指令）</summary>
        private readonly Dictionary<string, string> _axisReadoutSignatures = new Dictionary<string, string>();

        private bool _taJogging;

        /// <summary>装载期静默标记：控件回填不触发生效/落盘。</summary>
        private bool _loadingLaserHead;

        /// <summary>激光头页索引（tabsSystem 第 2 页，四区刷新分发依据）。</summary>
        private const int LaserHeadTabIndex = 1;

        /// <summary>轨迹页索引（tabsSystem 第 1 页，轴表刷新分发依据）。</summary>
        private const int TraceTabIndex = 0;

        /// <summary>自动刷新开关前置校验：未连接时拒绝并回退。</summary>

        /// <returns>true = 允许开启；false = 已拒绝并回退</returns>
        private bool AcceptRefreshSwitchOn(AntdUI.Switch toggle, string zoneName, Func<bool> zoneReady)
        {
            if (zoneReady == null || zoneReady()) return true;
            _syncingRefreshSwitch = true;
            try
            {
                toggle.Checked = false;
                toggle.Enabled = false;
            }
            finally
            {
                _syncingRefreshSwitch = false;
            }
            Warn(zoneName + "：对应硬件未初始化成功，刷新不可用");
            return false;
        }

        /// <summary>重算五个刷新开关的可用性。</summary>
        private void ApplyRefreshSwitchGate()
        {
            if (IsDisposed || Disposing) return;
            bool motionReady = IsMotionZoneReady();
            bool laReady = IsLaZoneReady();
            bool taReady = motionReady && IsTaAxisLoaded();
            GateRefreshSwitch(swAutoRefresh, ref _autoRefresh, motionReady);
            GateRefreshSwitch(swTARefresh, ref _taAutoRefresh, taReady);
            GateRefreshSwitch(swLARefresh, ref _laAutoRefresh, laReady);
            GateRefreshSwitch(swGalvoRefresh, ref _galvoAutoRefresh, laReady);
        }

        /// <summary>单开关门禁：就绪解冻，否则置灰。</summary>
        /// <param name="toggle">开关</param>
        /// <param name="flag">开关对应的刷新标记</param>
        /// <param name="ready">该区硬件是否就绪</param>
        private void GateRefreshSwitch(AntdUI.Switch toggle, ref bool flag, bool ready)
        {
            if (toggle == null) return;
            if (toggle.Enabled != ready) toggle.Enabled = ready;
            if (ready) return;
            if (!toggle.Checked && !flag) return;
            _syncingRefreshSwitch = true;
            try
            {
                toggle.Checked = false;
            }
            finally
            {
                _syncingRefreshSwitch = false;
            }
            flag = false;
        }

        /// <summary>运控初始化完成判定（轴表 / TA 区共用）。</summary>
        private static bool IsMotionZoneReady()
        {
            MontionManager motion = MontionManager.Instance;
            return motion != null && motion.IsInitCompleted && motion.IsInitialized;
        }

        /// <summary>焊接头从站就绪判定（LA / 振镜 / 温度区共用）。</summary>
        private static bool IsLaZoneReady()
        {
            MontionManager motion = MontionManager.Instance;
            return motion != null && motion.LaReady;
        }

        /// <summary>TA 轴是否已装载（未装配时 TA 区刷新置灰）。</summary>
        private static bool IsTaAxisLoaded()
        {
            MontionManager motion = MontionManager.Instance;
            return motion != null && motion.GetDeltaAxisByKeys(MontionManager.TaAxisKeys) != null;
        }

        /// <summary>TA 区刷新就绪判定（运控初始化完成且 TA 轴已装载）。</summary>
        private static bool IsTaRefreshReady()
        {
            return IsMotionZoneReady() && IsTaAxisLoaded();
        }

        /// <summary>振镜自动刷新开关：只切换刷新节拍并立即刷新</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void SwGalvoRefresh_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingLaserHead || _syncingRefreshSwitch) return;
            if (swGalvoRefresh.Checked && !AcceptRefreshSwitchOn(swGalvoRefresh, "振镜", IsLaZoneReady))
            {
                _galvoAutoRefresh = false;
                return;
            }
            _galvoAutoRefresh = swGalvoRefresh.Checked;
            if (_galvoAutoRefresh)
            {
                RefreshGalvoStatus();
            }
            Log(_galvoAutoRefresh ? "振镜自动刷新已开启" : "振镜自动刷新已关闭");
        }

        /// <summary>LA 对焦电机自动刷新开关：只切换刷新节拍并立即</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void SwLARefresh_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingLaserHead || _syncingRefreshSwitch) return;
            if (swLARefresh.Checked && !AcceptRefreshSwitchOn(swLARefresh, "LA 对焦电机", IsLaZoneReady))
            {
                _laAutoRefresh = false;
                return;
            }
            _laAutoRefresh = swLARefresh.Checked;
            if (_laAutoRefresh)
            {
                RefreshLaMotorStatus();
            }
            Log(_laAutoRefresh ? "LA 对焦电机自动刷新已开启" : "LA 对焦电机自动刷新已关闭");
        }

        /// <summary>TA 送丝力矩电机自动刷新开关：只切换刷新节拍并</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void SwTARefresh_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingLaserHead || _syncingRefreshSwitch) return;
            if (swTARefresh.Checked && !AcceptRefreshSwitchOn(swTARefresh, "TA 送丝力矩电机", IsTaRefreshReady))
            {
                _taAutoRefresh = false;
                return;
            }
            _taAutoRefresh = swTARefresh.Checked;
            if (_taAutoRefresh)
            {
                RefreshTaMotorStatus();
            }
            Log(_taAutoRefresh ? "TA 送丝力矩电机自动刷新已开启" : "TA 送丝力矩电机自动刷新已关闭");
        }

        /// <summary>读取参数：配置→界面，并读一次温压与电机状态。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnLhRead_Click(object sender, EventArgs e)
        {
            MontionManager motion = MontionManager.Instance;
            motion?.LoadWeldHeadConfig();

            WeldHeadSlaveDevice head = motion?.WeldHead;
            if (head != null && head.IsInitialized)
            {
                head.ReadTemperature();
                head.ReadAlarmStatus();
            }
            LoadOpticsFromConfig();
            LoadTempFromConfig();
            // 刷新振镜/LA/TA 电机状态（只读，每拍一次）
            RefreshGalvoStatus();
            RefreshLaMotorStatus();
            RefreshTaMotorStatus();
            Log("激光头参数已读取");
        }

        /// <summary>修改参数：界面→内存，只写内存不落盘。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnLhApply_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("修改激光头系统参数")) return;
            if (!ApplyLaserHeadOnForm()) return;
            // 回填一次：界面只显示已写入内存的真实值
            LoadOpticsFromConfig();
            string applied = "激光头参数已应用到内存，未落盘，请点保存参数";
            AntdUI.Message.success(this.FindForm(), applied);
            Log(applied);
        }

        /// <summary>保存参数：内存→文件并下发设备。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnLhSave_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("保存激光头系统参数")) return;
            SaveLaserHead();
        }

        /// <summary>从配置回填界面（读取按钮与构造期共用）。</summary>
        private void LoadOpticsFromConfig()
        {
            if (_loadingLaserHead) return;
            _loadingLaserHead = true;
            try
            {
                MontionManager motion = MontionManager.Instance;
                WeldHeadConfig headCfg = motion?.WeldHeadConfig;
                if (headCfg != null)
                {
                    GalvoGlobalParams galvo = headCfg.DefaultGalvo;
                    txtOptGalvoAngle.Text = galvo.Angle.ToString();
                    txtOptGalvoStartAngle.Text = galvo.StartAngle.ToString("0.0", CultureInfo.InvariantCulture);
                    txtOptGalvoEndAngle.Text = galvo.EndAngle.ToString("0.0", CultureInfo.InvariantCulture);
                    txtOptGalvoXCenter.Text = galvo.XCenter.ToString("0.0", CultureInfo.InvariantCulture);
                    txtOptGalvoYCenter.Text = galvo.YCenter.ToString("0.0", CultureInfo.InvariantCulture);
                    txtOptGalvoXComp.Text = galvo.XCompression.ToString();
                    txtOptGalvoYComp.Text = galvo.YCompression.ToString();
                    txtOptGalvoTargetSize.Text = galvo.TargetSize.ToString("0.0", CultureInfo.InvariantCulture);
                    txtOptGalvoOblique.Text = galvo.ObliqueEllipseCorrection.ToString();
                    txtOptMotorPulse.Text = headCfg.MotorPulsePerMm.ToString("0.###", CultureInfo.InvariantCulture);
                }

                // TORQUE_SLOPE 已迁 TA 轴参数弹窗
                // TA 回零参数已迁 ROOT/TA_HOME

                // 对中阈值已迁监控相机页

                UpdateMotorStateLabel();
                UpdateCurveInfo();
            }
            finally
            {
                _loadingLaserHead = false;
            }
        }

        /// <summary>从配置回填温度阈值与启用开关。</summary>
        private void LoadTempFromConfig()
        {
            DeltaMotionConfig cfg = GetConfig();
            if (cfg != null && cfg.WeldHeadSlave != null)
            {
                swWeldHeadEnable.Checked = cfg.WeldHeadSlave.Enabled;
                numWeldHeadNode.Value = cfg.WeldHeadSlave.NodeId;
            }

            MontionManager motion = MontionManager.Instance;
            WeldHeadConfig headCfg = motion?.WeldHeadConfig;
            if (headCfg == null) return;

            txtTempWarn.Text = headCfg.TempWarnThreshold.ToString("0.0", CultureInfo.InvariantCulture);
            txtTempAlarm.Text = headCfg.TempAlarmThreshold.ToString("0.0", CultureInfo.InvariantCulture);
            txtRiseWarn.Text = headCfg.RiseWarnThreshold.ToString("0.0", CultureInfo.InvariantCulture);
            txtRiseAlarm.Text = headCfg.RiseAlarmThreshold.ToString("0.0", CultureInfo.InvariantCulture);
            txtPressureAlarm.Text = headCfg.PressureAlarmThreshold.ToString("0.0", CultureInfo.InvariantCulture);
            txtPressureDelay.Text = headCfg.PressureDelayMs.ToString();
        }

        private void UpdateMotorStateLabel()
        {
            MontionManager motion = MontionManager.Instance;
            WeldHeadSlaveDevice head = motion?.WeldHead;
            MotorState? state = head != null && head.IsInitialized ? head.ReadMotorStatus() : null;
            lblOptMotorState.Text = state.HasValue && state.Value.IsReady ? "已使能" : "未使能";
        }

        private void UpdateCurveInfo()
        {
            TaLaCurveConfig curve = WeldParamManager.Instance.TaLaCurve;
            lblOptCurveInfo.Text = curve == null
                ? "曲线：—"
                : string.Format("曲线A {0} 点 / 曲线B {1} 点", curve.PointsA.Count, curve.PointsB.Count);
        }

        /// <summary>刷新振镜使能与驱动报警。</summary>
        private void RefreshGalvoStatus()
        {
            ApplyGalvo(CollectGalvo());
        }

        /// <summary>采集振镜快照（焊接头从站读，可在后台线程执行）。</summary>
        private static GalvoSnapshot CollectGalvo()
        {
            GalvoSnapshot snapshot = new GalvoSnapshot();
            MontionManager motion = MontionManager.Instance;
            WeldHeadSlaveDevice head = motion?.WeldHead;
            if (head == null || !head.IsInitialized)
            {
                snapshot.Enable = "—";
                snapshot.Driver = "—";
                snapshot.StationReady = false;
                return snapshot;
            }

            HeadAlarmStatus alarm = head.ReadAlarmStatus() ?? LaserWeldHeadWorkflow.Instance.LastAlarmStatus;
            if (alarm == null)
            {
                snapshot.Enable = "读取失败";
                snapshot.Driver = "读取失败";
                snapshot.StationReady = false;
                return snapshot;
            }

            snapshot.Enable = alarm.GalvoOn ? "已使能" : "未使能";
            snapshot.Driver = alarm.DriverAlarm ? "报警" : "正常";
            // 从站就绪且无报警时允许切换
            snapshot.StationReady = !alarm.DriverAlarm;
            return snapshot;
        }

        /// <summary>把振镜快照贴到标签与按钮（UI 线程）。</summary>
        private void ApplyGalvo(GalvoSnapshot snapshot)
        {
            if (snapshot == null) return;
            lblOptGalvoEnableValue.Text = snapshot.Enable;
            lblOptGalvoDriverValue.Text = snapshot.Driver;
            // 按钮管控由统一函数负责
            _galvoStationReady = snapshot.StationReady;
        }

        /// <summary>振镜使能切换：焊接中禁止手动切换。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptGalvoEnable_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("切换振镜使能")) return;
            if (DeviceControlWork.Instance.IsWelding)
            {
                Warn("焊接中振镜使能由摆幅自动控制，禁止手动切换");
                return;
            }

            StartManualOperation(ManualOpGroup.Galvo, "振镜使能切换", () =>
            {
                if (!LaserWeldHeadWorkflow.Instance.EnsureManualWeldHeadActionAllowed("振镜使能切换", out string galvoReject1))
                    return new ManualOpOutcome { Ok = false, Reason = galvoReject1 };
                MontionManager motion = MontionManager.Instance;
                WeldHeadSlaveDevice head = motion?.WeldHead;
                if (head == null || !head.IsInitialized)
                    return new ManualOpOutcome { Ok = false, Reason = "焊接头从站未就绪，无法操作振镜使能" };

                GalvoParams galvo = head.ReadGalvoParams();
                if (galvo == null)
                    return new ManualOpOutcome { Ok = false, Reason = "振镜参数读取失败，无法切换使能" };
                bool enable = !galvo.GalvoEnable;
                galvo.GalvoEnable = enable;
                if (!head.SetGalvoParams(galvo))
                    return new ManualOpOutcome { Ok = false, Reason = "振镜使能写入失败" };
                return new ManualOpOutcome { Ok = true };
            });
        }

        /// <summary>刷新 LA 对焦电机状态（源 = 焊接头 0x6020 回读，只读）。</summary>
        private void RefreshLaMotorStatus()
        {
            ApplyLaMotor(CollectLaMotor());
        }

        /// <summary>采集 LA 对焦电机快照。</summary>
        private static LaMotorSnapshot CollectLaMotor()
        {
            LaMotorSnapshot snapshot = new LaMotorSnapshot();
            MontionManager motion = MontionManager.Instance;
            WeldHeadSlaveDevice head = motion?.WeldHead;
            if (head == null || !head.IsInitialized)
            {
                snapshot.Position = "—";
                snapshot.State = "未就绪";
                return snapshot;
            }

            MotorState? state = head.ReadMotorStatus();
            if (!state.HasValue)
            {
                snapshot.Position = "—";
                snapshot.State = "读取失败";
                return snapshot;
            }

            snapshot.Position = state.Value.CurrentPositionMm.ToString("0.000", CultureInfo.InvariantCulture);
            snapshot.State = state.Value.IsReady ? "已使能" : "未使能";
            return snapshot;
        }

        /// <summary>把 LA 快照贴到标签（UI 线程）。</summary>
        private void ApplyLaMotor(LaMotorSnapshot snapshot)
        {
            if (snapshot == null) return;
            lblMotorPosValue.Text = snapshot.Position;
            lblOptMotorState.Text = snapshot.State;
        }

        /// <summary>刷新 TA 表（与 Y 轴表同一刷新入口 FillAxisRuntime；不新增计时器，沿用 timerRefresh）</summary>
        private void RefreshTaMotorStatus()
        {
            if (_taRows.Count == 0) LoadTaAxisRow();
            foreach (AxisRow row in _taRows) FillAxisRuntime(row);
            tableTa.Binding(_taRows);
        }

        /// <summary>TA 轴手动操作统一入口（A-1）</summary>

        /// <param name="operation">操作种类</param>
        /// <param name="value">MoveTo 目标 / StepMove 位移（其余传 0）</param>
        /// <param name="dir">JogStart 方向（1=负向 0=正向）</param>
        /// <param name="axisName">命中轴名（日志用）</param>
        /// <param name="servoOn">ToggleServo 后的使能状态</param>
        /// <param name="commandOk">指令下发结果</param>
        /// <returns>前置是否通过；被拒时已弹提示</returns>
        private static bool TryTaOperation(AxisManualOperation operation, double value, ushort dir,
            out string axisName, out bool servoOn, out bool commandOk, out string rejectReason)
        {
            rejectReason = string.Empty;
            return MotionControlWorkflow.Instance.TryManualAxisOperation(MontionManager.TaAxisKeys, operation, value, dir,
                out axisName, out servoOn, out commandOk, out rejectReason);
        }

        /// <summary>界面→内存：校验后写入，不落盘不下发。</summary>
        /// <returns>全部合法并写入返回 true</returns>
        private bool ApplyLaserHeadOnForm()
        {

            // TORQUE_SLOPE 已迁 TA 参数弹窗
            if (!TryReadDouble(txtOptMotorPulse, out double pulse) || pulse <= 0.0)
            {
                Warn("LA 量纲必须为大于 0 的数值"); return false;
            }
            if (!TryReadInt(txtOptGalvoAngle, out int angle) || angle < 0 || angle > 360)
            {
                Warn("振镜旋转角范围 0~360°"); return false;
            }
            if (!TryReadDouble(txtOptGalvoStartAngle, out double startAngle) || startAngle < 0.0 || startAngle > 360.0
                || !TryReadDouble(txtOptGalvoEndAngle, out double endAngle) || endAngle < 0.0 || endAngle > 360.0)
            {
                Warn("振镜起止角范围 0~360°"); return false;
            }
            if (!TryReadDouble(txtOptGalvoXCenter, out double xCenter) || xCenter < -10.0 || xCenter > 10.0
                || !TryReadDouble(txtOptGalvoYCenter, out double yCenter) || yCenter < -10.0 || yCenter > 10.0)
            {
                Warn("振镜中心范围 -10~10mm"); return false;
            }
            if (!TryReadInt(txtOptGalvoXComp, out int xComp) || xComp < -100 || xComp > 100
                || !TryReadInt(txtOptGalvoYComp, out int yComp) || yComp < -100 || yComp > 100)
            {
                Warn("振镜压缩范围 -100~100%"); return false;
            }
            if (!TryReadDouble(txtOptGalvoTargetSize, out double targetSize) || targetSize < 0.0 || targetSize > 5.0)
            {
                Warn("振镜目标尺寸范围 0~5mm"); return false;
            }
            if (!TryReadInt(txtOptGalvoOblique, out int oblique) || oblique < -180 || oblique > 180)
            {
                Warn("斜椭校正范围 -180~180"); return false;
            }
            if (!TryReadDouble(txtTempWarn, out double tempWarn) || !TryReadDouble(txtTempAlarm, out double tempAlarm)
                || tempWarn <= 0.0 || tempAlarm <= tempWarn)
            {
                Warn("温度阈值须满足 0 < 警告 < 报警"); return false;
            }
            if (!TryReadDouble(txtRiseWarn, out double riseWarn) || !TryReadDouble(txtRiseAlarm, out double riseAlarm)
                || riseWarn <= 0.0 || riseAlarm <= riseWarn)
            {
                Warn("温升阈值须满足 0 < 警告 < 报警"); return false;
            }
            if (!TryReadDouble(txtPressureAlarm, out double pressureAlarm) || pressureAlarm <= 0.0)
            {
                Warn("气压报警阈值必须大于 0"); return false;
            }
            if (!TryReadInt(txtPressureDelay, out int pressureDelay) || pressureDelay < 0)
            {
                Warn("气压延时必须为非负整数(ms)"); return false;
            }

            MontionManager motion = MontionManager.Instance;
            WeldHeadConfig headCfg = motion?.WeldHeadConfig;
            if (headCfg == null)
            {
                Warn("焊接头配置不可用"); return false;
            }

            GalvoGlobalParams galvo = headCfg.DefaultGalvo;
            galvo.Angle = angle;
            galvo.StartAngle = startAngle;
            galvo.EndAngle = endAngle;
            galvo.XCenter = xCenter;
            galvo.YCenter = yCenter;
            galvo.XCompression = xComp;
            galvo.YCompression = yComp;
            galvo.TargetSize = targetSize;
            galvo.ObliqueEllipseCorrection = oblique;
            headCfg.MotorPulsePerMm = pulse;
            headCfg.TempWarnThreshold = tempWarn;
            headCfg.TempAlarmThreshold = tempAlarm;
            headCfg.RiseWarnThreshold = riseWarn;
            headCfg.RiseAlarmThreshold = riseAlarm;
            headCfg.PressureAlarmThreshold = pressureAlarm;
            headCfg.PressureDelayMs = pressureDelay;

            return true;
        }

        /// <summary>内存→配置落盘 + 设备下发（振镜 / 温压阈值）。</summary>
        private void SaveLaserHead()
        {
            MontionManager motion = MontionManager.Instance;
            WeldHeadConfig headCfg = motion?.WeldHeadConfig;
            if (headCfg == null)
            {
                Warn("焊接头配置不可用"); return;
            }

            motion.SaveWeldHeadConfig();
            // 只存焊接头配置
            DeltaMotionConfig cfg = GetConfig();
            if (cfg != null && cfg.WeldHeadSlave != null)
            {
                // 启用开关随保存落盘，不即时下发
                cfg.WeldHeadSlave.Enabled = swWeldHeadEnable.Checked;
                // 节点撞号由装配层跳过
                int nodeId = (int)numWeldHeadNode.Value;
                if (nodeId < 0 || nodeId > ushort.MaxValue)
                {
                    Warn("焊接头节点号超出范围");
                    return;
                }
                cfg.WeldHeadSlave.NodeId = (ushort)nodeId;
            }
            cfg?.SaveConfig();
            // 保存后推回运行期轴，下次回零生效
            if (motion.ApplyTaAxisRuntimeConfig())
            {
                Log("TA 回零参数已应用到运行期轴，下次回零生效（无需重新使能）");
            }
            // 回零模式逻辑值转 16 位补码下发，统一走 Home_Config
            DeltaTaHomeConfig taHome = motion.TaHomeConfig;
            if (taHome != null)
            {
                Log(string.Format("TA 回零模式 逻辑 {0} → 下发值 {1}（16 位补码，统一走 Home_Config）",
                    taHome.HomeMode, (ushort)(taHome.HomeMode & 0xFFFF)));
            }
            if (AlgorithmManager.Instance.WireFeed != null) AlgorithmManager.Instance.Save();

            // 当前振镜图形档
            WeldParamConfig algoCfg = WeldParamManager.Instance.AlgorithmsConfig;
            WsgGalvoPattern galvoPattern = LaserWeldHeadWorkflow.ResolveGalvoPattern(algoCfg != null ? algoCfg.GalvoPattern : 0);

            WeldHeadSlaveDevice head = motion.WeldHead;
            if (head != null && head.IsInitialized)
            {
                head.MotorPulsePerMm = headCfg.MotorPulsePerMm;
                head.SetGalvoParams(headCfg.BuildGalvoParamsFor(galvoPattern));
                head.TemperatureAlarmThreshold = headCfg.TempAlarmThreshold;   // 同步本机越阈判定阈值，否则改完要重连才生效
                head.SetAlarmThresholds(headCfg.TempWarnThreshold, headCfg.TempAlarmThreshold,
                    headCfg.RiseWarnThreshold, headCfg.RiseAlarmThreshold,
                    headCfg.PressureAlarmThreshold, headCfg.PressureDelayMs);
            }
            Log(string.Format("激光头参数已保存 振镜图形 {0} 频率 {1}Hz",
                galvoPattern, headCfg.GetGalvoShape(galvoPattern).Frequency));
        }

        /// <summary>LA 对焦电机使能切换（Button，仅 Admin；当前状态由 lblOptMotorState 呈现）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptMotorEnable_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("切换对焦电机使能")) return;
            StartManualOperation(ManualOpGroup.La, "对焦电机使能切换", () =>
            {
                if (!LaserWeldHeadWorkflow.Instance.EnsureManualWeldHeadActionAllowed("对焦电机使能切换", out string laReject1))
                    return new ManualOpOutcome { Ok = false, Reason = laReject1 };
                MontionManager motion = MontionManager.Instance;
                WeldHeadSlaveDevice head = motion?.WeldHead;
                if (head == null || !head.IsInitialized)
                    return new ManualOpOutcome { Ok = false, Reason = "焊接头从站未就绪，无法操作对焦电机" };

                MotorState? state = head.ReadMotorStatus();
                if (!state.HasValue)
                    return new ManualOpOutcome { Ok = false, Reason = "对焦电机状态读取失败" };
                bool enable = !state.Value.IsReady;
                // PDO 只有目标位置+使能位，故均写当前位置
                if (!head.SetMotorPosEnable(state.Value.CurrentPositionMm, enable))
                    return new ManualOpOutcome { Ok = false, Reason = "对焦电机使能写入失败" };
                return new ManualOpOutcome { Ok = true };
            });
        }

        /// <summary>LA 后退一步（仅 Admin）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptMotorBack_Click(object sender, EventArgs e)
        {
            StepLaMotor(-1);
        }

        /// <summary>LA 前进一步（仅 Admin）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptMotorFwd_Click(object sender, EventArgs e)
        {
            StepLaMotor(1);
        }

        /// <summary>LA 绝对移动到「移动目标」（仅 Admin，目标取 txtOptMotorTarget）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptMotorMoveTo_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("对焦电机移动到目标")) return;
            if (!TryReadDouble(txtOptMotorTarget, out double target))
            {
                Warn("请填写有效的 LA 移动目标"); return;
            }

            StartManualOperation(ManualOpGroup.La, "对焦电机移动到 " + target.ToString("F3", CultureInfo.InvariantCulture), () =>
            {
                if (!LaserWeldHeadWorkflow.Instance.EnsureManualWeldHeadActionAllowed("对焦电机移动到目标", out string laReject2))
                    return new ManualOpOutcome { Ok = false, Reason = laReject2 };
                MontionManager motion = MontionManager.Instance;
                WeldHeadSlaveDevice head = motion?.WeldHead;
                if (head == null || !head.IsInitialized)
                    return new ManualOpOutcome { Ok = false, Reason = "焊接头从站未就绪，无法操作对焦电机" };

                MotorState? state = head.ReadMotorStatus();
                if (!state.HasValue)
                    return new ManualOpOutcome { Ok = false, Reason = "对焦电机状态读取失败" };
                if (!state.Value.IsReady)
                    return new ManualOpOutcome { Ok = false, Reason = "请先使能对焦电机" };

                if (!head.SetMotorPosEnable(target, true))
                    return new ManualOpOutcome { Ok = false, Reason = "对焦电机位置下发失败" };
                return new ManualOpOutcome { Ok = true };
            });
        }

        /// <summary>LA 步距位置移动（PDO 仅目标位置+使能，无连续点动接口）。</summary>
        private void StepLaMotor(int sign)
        {
            if (!EnsureAdminForWrite("对焦电机步进")) return;
            if (!TryReadDouble(txtOptMotorStep, out double step) || step <= 0.0)
            {
                Warn("LA 步距必须大于 0"); return;
            }

            StartManualOperation(ManualOpGroup.La, "对焦电机" + (sign < 0 ? "后退" : "前进") + "一步", () =>
            {
                if (!LaserWeldHeadWorkflow.Instance.EnsureManualWeldHeadActionAllowed("对焦电机步进", out string laReject3))
                    return new ManualOpOutcome { Ok = false, Reason = laReject3 };
                MontionManager motion = MontionManager.Instance;
                WeldHeadSlaveDevice head = motion?.WeldHead;
                if (head == null || !head.IsInitialized)
                    return new ManualOpOutcome { Ok = false, Reason = "焊接头从站未就绪，无法操作对焦电机" };

                MotorState? state = head.ReadMotorStatus();
                if (!state.HasValue)
                    return new ManualOpOutcome { Ok = false, Reason = "对焦电机状态读取失败" };
                if (!state.Value.IsReady)
                    return new ManualOpOutcome { Ok = false, Reason = "请先使能对焦电机" };

                double target = state.Value.CurrentPositionMm + sign * step;
                if (!head.SetMotorPosEnable(target, true))
                    return new ManualOpOutcome { Ok = false, Reason = "对焦电机位置下发失败" };
                return new ManualOpOutcome { Ok = true };
            });
        }

        /// <summary>TA 伺服使能切换（与 Y 轴 FixedServo 同实现，仅 Admin）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void TaServo_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("切换 TA 伺服")) return;
            StartManualOperation(ManualOpGroup.Ta, "TA 伺服使能切换", () =>
            {
                bool gate = TryTaOperation(AxisManualOperation.ToggleServo, 0, 0,
                    out string axisName, out bool on, out bool commandOk, out string rejectReason);
                return BuildOutcome(gate, rejectReason, commandOk, axisName, "伺服使能未生效");
            });
        }

        /// <summary>TA 减速停止（与 Y 轴 FixedStop 同实现，安全操作不设权限门控）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void TaStop_Click(object sender, EventArgs e)
        {
            // 停止同步下发一次
            ResetTaJogFlag();
            if (!TryTaOperation(AxisManualOperation.Stop, 0, 0,
                    out string axisName, out _, out _, out string rejectReason))
            {
                Log("TA 轴 停止被拒 " + rejectReason, MessageLevel.Warning);
                return;
            }
            Log(axisName + " 已下发减速停止");
        }

        private void TaHome_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("TA 回零")) return;
            // 手动回零走统一入口
            StartManualOperation(ManualOpGroup.Ta, "TA 回零", () =>
            {
                bool ok = MotionControlWorkflow.Instance.TryHomeAxisWithWait(MontionManager.TaAxisKeys, "TA 回零",
                    out string axisName, out string rejectCode, out string rejectDetail, out string evidence);
                string reason = string.IsNullOrEmpty(rejectDetail) ? rejectCode : rejectDetail;
                return new ManualOpOutcome { Ok = ok, Reason = ok ? string.Empty : reason, AxisName = axisName };
            });
        }

        /// <summary>TA 绝对移动到「移动目标」</summary>

        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void TaMoveTo_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("TA 移动到")) return;
            AxisRow row = GetTaRow();
            if (row == null || !double.TryParse(row.TargetPos, NumberStyles.Float, CultureInfo.InvariantCulture, out double target))
            {
                Warn("请填写有效的 TA 移动目标（TA 表「移动目标」列）"); return;
            }
            StartManualOperation(ManualOpGroup.Ta, "TA 移动到 " + target.ToString("F3", CultureInfo.InvariantCulture), () =>
            {
                bool ok = MotionControlWorkflow.Instance.TryMoveAxisToTarget(MontionManager.TaAxisKeys, target, "TA 绝对移动",
                    out string axisName, out string rejectCode, out string rejectDetail, out string evidence);
                string reason = string.IsNullOrEmpty(rejectDetail) ? rejectCode : rejectDetail;
                return new ManualOpOutcome { Ok = ok, Reason = ok ? string.Empty : reason, AxisName = axisName };
            });
        }

        /// <summary>TA 负向步进（与 Y 轴 FixedStepBack 同实现，步距取 TA 表「步距」列）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void TaStepBack_Click(object sender, EventArgs e)
        {
            TaStep(-1);
        }

        /// <summary>TA 正向步进（与 Y 轴 FixedStepFwd 同实现）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void TaStepFwd_Click(object sender, EventArgs e)
        {
            TaStep(1);
        }

        private void TaStep(int sign)
        {
            if (!EnsureAdminForWrite("TA 步进")) return;
            AxisRow row = GetTaRow();
            if (row == null
                || !double.TryParse(row.LayoutValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double step)
                || step <= 0.0)
            {
                Warn("TA 步距必须大于 0（TA 表「步距」列）"); return;
            }
            double delta = sign * step;
            StartManualOperation(ManualOpGroup.Ta, "TA 相对移动 " + delta.ToString("F3", CultureInfo.InvariantCulture), () =>
            {
                // 相对步进换算为绝对移动
                if (!TryGetAxisPositionMm(MontionManager.TaAxisKeys, out double currentMm, out string axisName))
                {
                    return new ManualOpOutcome { Ok = false, Reason = "轴未装配或位置读取失败" };
                }
                bool ok = MotionControlWorkflow.Instance.TryMoveAxisToTarget(MontionManager.TaAxisKeys, currentMm + delta,
                    "TA 相对移动", out axisName, out string rejectCode, out string rejectDetail, out string evidence);
                string reason = string.IsNullOrEmpty(rejectDetail) ? rejectCode : rejectDetail;
                return new ManualOpOutcome { Ok = ok, Reason = ok ? string.Empty : reason, AxisName = axisName };
            });
        }

        private void TaNegBack_MouseDown(object sender, MouseEventArgs e) { StartTaJog(1); }
        private void TaNegFwd_MouseDown(object sender, MouseEventArgs e) { StartTaJog(0); }
        private void TaNegBack_MouseUp(object sender, MouseEventArgs e) { StopTaJog(); }
        private void TaNegFwd_MouseUp(object sender, MouseEventArgs e) { StopTaJog(); }
        private void TaNegBack_MouseLeave(object sender, EventArgs e) { if (_taJogging) StopTaJog(); }
        private void TaNegFwd_MouseLeave(object sender, EventArgs e) { if (_taJogging) StopTaJog(); }

        /// <summary>按住 TA 点动（dir 1=负向, 0=正向），松开/移出即停。</summary>
        /// <param name="dir">目录路径。</param>
        private void StartTaJog(ushort dir)
        {
            if (!EnsureAdminForWrite("TA 点动")) return;
            // 点动按钮须保持接收松开事件
            if (IsGroupBusy(ManualOpGroup.Ta))
            {
                Warn("上一条操作尚未完成，请等待完成后再点动");
                return;
            }
            // 界面按钮属手动动作，置手动模式
            MotionControlWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            if (!TryTaOperation(AxisManualOperation.JogStart, 0, dir,
                    out string axisName, out _, out _, out string rejectReason))
            {
                Log("TA 轴 点动被拒 " + rejectReason, MessageLevel.Warning);
                return;
            }
            _taJogging = true;
            Log(axisName + (dir == 1 ? " 负向" : " 正向") + "点动已下发（按住中）");
        }

        private void StopTaJog()
        {
            if (!_taJogging) return;
            try
            {
                if (TryTaOperation(AxisManualOperation.JogStop, 0, 0,
                        out string axisName, out _, out _, out string rejectReason))
                    Log(axisName + " 点动已停止");
            }
            catch (Exception ex)
            {
                Log("TA 轴 点动停止下发异常 " + ex.GetType().Name + " " + ex.Message, MessageLevel.Warning);
            }
            _taJogging = false;
        }

        /// <summary>只复位 TA 点动标志</summary>
        private void ResetTaJogFlag()
        {
            _taJogging = false;
        }

        /// <summary>振镜回到逻辑零位（仅 Admin）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptGalvoHome_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("振镜回零位")) return;
            StartManualOperation(ManualOpGroup.Galvo, "振镜回逻辑零位", () =>
            {
                if (!LaserWeldHeadWorkflow.Instance.EnsureManualWeldHeadActionAllowed("振镜回逻辑零位", out string galvoReject2))
                    return new ManualOpOutcome { Ok = false, Reason = galvoReject2 };
                // 界面按钮属手动动作，置手动模式（激光头流程）
                LaserWeldHeadWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
                bool ok = LaserWeldHeadWorkflow.Instance.GalvoHome();
                return new ManualOpOutcome
                {
                    Ok = ok,
                    Reason = ok ? string.Empty : "振镜回零失败",
                    UiCallback = success =>
                    {
                        lblOptGalvoHomeState.Text = success ? "已回逻辑零位" : "回零失败";
                    }
                };
            });
        }

        /// <summary>TA/LA 曲线标定入口：选择 CSV/TSV 导入（曲线只读，导入即替换并</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnOptImportCurve_Click(object sender, EventArgs e)
        {
            if (!EnsureAdminForWrite("导入 TA/LA 曲线")) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "曲线表格|*.csv;*.tsv;*.txt|所有文件|*.*";
                dialog.Title = "导入 TA/LA 曲线标定表格";
                if (dialog.ShowDialog(this.FindForm()) != DialogResult.OK) return;

                bool ok = WeldParamManager.Instance.TaLaCurve.ImportFromCsv(dialog.FileName, out string error);
                UpdateCurveInfo();
                if (!ok) lblOptCurveInfo.Text = "导入失败";
                Log(ok ? "曲线表格导入成功 " + dialog.FileName : "曲线表格导入失败 " + error,
                    ok ? MessageLevel.Info : MessageLevel.Warning);
                if (!ok) Warn(error);
            }
        }

        private void Warn(string message)
        {
            AntdUI.Message.warn(this.FindForm(), message);
            Log(message, MessageLevel.Warning);
        }

        private static bool TryReadDouble(AntdUI.Input input, out double value)
        {
            value = 0.0;
            if (input == null || string.IsNullOrEmpty(input.Text)) return false;
            string text = input.Text.Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            return double.TryParse(text, out value);
        }

        private static bool TryReadInt(AntdUI.Input input, out int value)
        {
            value = 0;
            if (!TryReadDouble(input, out double parsed)) return false;
            value = (int)Math.Round(parsed);
            return Math.Abs(parsed - value) < 0.0001;
        }

        #endregion

        #region 运行态刷新

        /// <summary>轨迹页轴表自动刷新开关：只切换刷新节拍并立</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void SwAutoRefresh_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_syncingRefreshSwitch) return;
            if (swAutoRefresh.Checked && !AcceptRefreshSwitchOn(swAutoRefresh, "轴表", IsMotionZoneReady))
            {
                _autoRefresh = false;
                return;
            }
            _autoRefresh = swAutoRefresh.Checked;
            if (_autoRefresh)
            {
                RefreshRuntime();
            }
            Log(_autoRefresh ? "轴表自动刷新已开启" : "轴表自动刷新已关闭");
        }

        /// <summary>前置条件②：未切到本页只跳过刷新。</summary>

        /// <returns>是否成功。</returns>
        private bool IsRefreshPageVisible()
        {
            if (IsDisposed || Disposing) return false;
            return Visible;
        }

        /// <summary>运控连接态切换</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">状态切换参数。</param>
        private void OnMotionStateSwitched(object sender, SubDeviceStateChangedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMotionStateSwitched(sender, e)));
                return;
            }
            ApplyRefreshSwitchGate();
        }

        /// <summary>整机初始化链结束（成功/失败均触发）</summary>
        /// <param name="allReady">是否全部模组就绪（门禁按各区状态重算）。</param>
        private void OnInitializationCompleted(bool allReady)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnInitializationCompleted), allReady);
                return;
            }
            ApplyRefreshSwitchGate();
        }

        /// <summary>初始化汇总（成功 / 失败 / 跳过三态分开）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">汇总事件参数。</param>
        private void OnInitializationSummaryReady(object sender, DeviceInitSummaryEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, DeviceInitSummaryEventArgs>(OnInitializationSummaryReady), sender, e);
                return;
            }
            ApplyRefreshSwitchGate();
            if (e == null || e.SkippedCount <= 0) return;
            Log("模组初始化 跳过 " + e.SkippedCount + " 个 原因见上方各模组跳过行", MessageLevel.Debug);
        }

        /// <summary>轴/从站就绪状态变化（Y / TA / LA）：重算刷新开关门禁。</summary>
        private void OnAxisReadyChanged()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnAxisReadyChanged));
                return;
            }
            ApplyRefreshSwitchGate();
        }

        #region 刷新快照（后台采集，纯数据，不持控件引用）

        /// <summary>刷新请求：在 UI 线程登记要刷哪个区、要采哪些数据。</summary>
        private sealed class RefreshSnapshot
        {
            public int TabIndex;
            public bool Axis;
            public bool Galvo;
            public bool La;
            public bool Ta;
            public List<DeltaAxisConfig> AxisConfigs;
            public DeltaAxisConfig TaConfig;
            public GalvoSnapshot GalvoState;
            public LaMotorSnapshot LaMotor;
            public List<AxisReadoutSnapshot> AxisReadouts;
            public List<AxisReadoutSnapshot> TaReadouts;
        }

        /// <summary>振镜区快照。</summary>
        private sealed class GalvoSnapshot
        {
            public string Enable;
            public string Driver;
            /// <summary>从站就绪且无报警</summary>
            public bool StationReady;
        }

        /// <summary>LA 对焦电机区快照。</summary>
        private sealed class LaMotorSnapshot
        {
            public string Position;
            public string State;
        }

        /// <summary>单轴只读快照（位置/使能/报警/最近指令）。</summary>
        private sealed class AxisReadoutSnapshot
        {
            public string AxisName;
            public bool Available;
            public double PositionMm;
            public string LastCommand;
            public bool Servo;
            public bool Alarm;
            public string Failure;
        }

        #endregion

        /// <summary>刷新节拍：只做显示信息刷新。</summary>
        private void TimerRefresh_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;

            int interval = FlowTimeoutSettings.UiRefreshMs;
            if (timerRefresh.Interval != interval) timerRefresh.Interval = interval;   // 配置晚到即生效

            // 前置条件①：未连接只跳过刷新。
            // 初始化期不在此判定连接
            if (!IsMotionReady()) return;

            // 前置条件②：未切页只跳过刷新，开关保留。
            if (!IsRefreshPageVisible()) return;

            RefreshSnapshot request = new RefreshSnapshot { TabIndex = tabsSystem.SelectedIndex };
            if (request.TabIndex == LaserHeadTabIndex)
            {
                request.Galvo = _galvoAutoRefresh;
                request.La = _laAutoRefresh;
                request.Ta = _taAutoRefresh;
                if (request.Ta)
                {
                    if (_taRows.Count == 0) LoadTaAxisRow();
                    AxisRow taRow = GetTaRow();
                    request.TaConfig = taRow?.Config;
                }
            }
            else if (request.TabIndex == TraceTabIndex)
            {
                request.Axis = _autoRefresh;
                if (!request.Axis) return;
                request.AxisConfigs = new List<DeltaAxisConfig>(_axisRows.Count);
                foreach (AxisRow row in _axisRows)
                {
                    if (row != null && row.Config != null) request.AxisConfigs.Add(row.Config);
                }
                if (_taRows.Count == 0) LoadTaAxisRow();
                AxisRow traceTaRow = GetTaRow();
                request.TaConfig = traceTaRow?.Config;
            }
            else
            {
                return; // 模拟测试页不属于任何刷新区
            }

            try
            {
                CollectRefreshSnapshot(request);
            }
            catch (Exception ex)
            {
                Log("刷新采集失败：" + ex.Message, MessageLevel.Warning);
                return;
            }
            ApplyRefreshSnapshot(request);
        }

        /// <summary>采集刷新快照</summary>
        private static void CollectRefreshSnapshot(RefreshSnapshot request)
        {
            if (request.TabIndex == LaserHeadTabIndex)
            {
                if (request.Galvo) request.GalvoState = CollectGalvo();
                if (request.La) request.LaMotor = CollectLaMotor();
                if (request.Ta) request.TaReadouts = CollectAxisReadouts(request.TaConfig);
            }
            else if (request.TabIndex == TraceTabIndex)
            {
                request.AxisReadouts = CollectAxisReadouts(request.AxisConfigs);
                request.TaReadouts = CollectAxisReadouts(request.TaConfig);
            }
        }

        /// <summary>把刷新快照贴到界面。</summary>
        private void ApplyRefreshSnapshot(RefreshSnapshot request)
        {
            if (IsDisposed || Disposing || request == null) return;

            if (request.TabIndex == LaserHeadTabIndex)
            {
                if (request.Galvo && request.GalvoState != null) ApplyGalvo(request.GalvoState);
                if (request.La && request.LaMotor != null) ApplyLaMotor(request.LaMotor);
                if (request.Ta && request.TaReadouts != null)
                {
                    if (_taRows.Count == 0) LoadTaAxisRow();
                    if (ApplyAxisReadouts(_taRows, request.TaReadouts))
                    {
                        tableTa.Binding(_taRows);
                    }
                }
                return;
            }

            if (request.TabIndex != TraceTabIndex) return;
            if (request.AxisReadouts != null)
            {
                if (ApplyAxisReadouts(_axisRows, request.AxisReadouts))
                {
                    tableAxis.Binding(_axisRows);
                    tableAxis.EmptyHeader = true;
                    tableAxis.VisibleHeader = true;
                }
            }
            if (request.TaReadouts != null)
            {
                if (_taRows.Count == 0) LoadTaAxisRow();
                if (ApplyAxisReadouts(_taRows, request.TaReadouts))
                {
                    tableTa.Binding(_taRows);
                }
            }
        }

        /// <summary>刷新轴表运行状态与信号灯。</summary>
        private void RefreshRuntime()
        {
            if (IsDisposed || Disposing) return;
            foreach (AxisRow row in _axisRows) FillAxisRuntime(row);
            tableAxis.Binding(_axisRows);
            tableAxis.EmptyHeader = true;
            tableAxis.VisibleHeader = true;

            // TA 表与 Y 轴表同源刷新，重建行保留步距。
            LoadTaAxisRow();
            foreach (AxisRow row in _taRows) FillAxisRuntime(row);
            tableTa.Binding(_taRows);
        }

        /// <summary>把采集到的轴快照贴回行集合</summary>
        private bool ApplyAxisReadouts(IList<AxisRow> rows, IList<AxisReadoutSnapshot> readouts)
        {
            bool changed = false;
            foreach (AxisReadoutSnapshot snapshot in readouts)
            {
                if (snapshot == null) continue;
                foreach (AxisRow row in rows)
                {
                    if (row == null || row.Config == null) continue;
                    if (!string.Equals(row.Config.AxisName, snapshot.AxisName, StringComparison.OrdinalIgnoreCase)) continue;
                    ApplyAxisReadout(row, snapshot);
                    if (AxisReadoutSignatureChanged(snapshot)) changed = true;
                    break;
                }
            }
            return changed;
        }

        /// <summary>轴读数签名（位置/使能/报警/最近指令）</summary>
        private string AxisReadoutSignature(AxisReadoutSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;
            return snapshot.Available + "|" + snapshot.PositionMm.ToString("F3", CultureInfo.InvariantCulture)
                + "|" + snapshot.Servo + "|" + snapshot.Alarm + "|" + snapshot.LastCommand + "|" + snapshot.Failure;
        }

        /// <summary>轴读数签名是否变化（变化则登记新签名）。</summary>
        private bool AxisReadoutSignatureChanged(AxisReadoutSnapshot snapshot)
        {
            string signature = AxisReadoutSignature(snapshot);
            string key = snapshot == null ? string.Empty : snapshot.AxisName;
            bool changed = !_axisReadoutSignatures.TryGetValue(key, out string previous) || previous != signature;
            if (changed) _axisReadoutSignatures[key] = signature;
            return changed;
        }

        #endregion

        #region 资源释放

        private void SystemControlPage_HandleDestroyed(object sender, EventArgs e)
        {
            DisposeComponents();
        }

        /// <summary>释放自定义资源：停定时器 + 退订模拟相关事件 + 释</summary>
        public void DisposeComponents()
        {
            timerRefresh?.Stop();
            _tooltip?.Dispose();
            _tooltip = null;
            UnsubscribeLineLaserEvents();
            GlobalCommData.OperateLevelChanged -= OnOperateLevelChanged;
            MotionControlWorkflow.Instance.StateSwitched -= OnMotionStateSwitched;
            DeviceControlWork.Instance.InitializationCompleted -= OnInitializationCompleted;
            DeviceControlWork.Instance.InitializationSummaryReady -= OnInitializationSummaryReady;
            MotionControlWorkflow.Instance.AxisReadyChanged -= OnAxisReadyChanged;
            LaserWeldHeadWorkflow.Instance.AxisReadyChanged -= OnAxisReadyChanged;
            DeviceControlWork.Instance.SimulationStopped -= OnSimulationStopped;
            DeviceControlWork.Instance.StatusChanged -= OnMainDeviceStatusChanged;
        }

        #endregion
    }
}

