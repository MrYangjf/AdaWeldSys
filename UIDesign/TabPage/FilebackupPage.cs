using AdaWeldSystem.Comm.Communication;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MotionControl;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.UIDesign.SubDialog.CommConfigDialog;
using AdaWeldSystem.WeldParamControl;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.TabPage
{
    /// <summary>文档备份页：通讯配置 / 系统配置两页签。</summary>
    public partial class FilebackupPage : UserControl
    {
        #region 私有变量

        private bool _isLogVisible = true;

        /// <summary>装载中标志：屏蔽赋值触发的回写与日志。</summary>
        private bool _loadingConfig;

        /// <summary>RSI 通讯周期下拉选项（毫秒，与显示文本一一对应）</summary>
        private static readonly int[] RsiCycleOptions = { 4, 12, 24 };

        /// <summary>RSI 通讯周期下拉显示文本（索引与 RsiCycleOptions 对应）</summary>
        private static readonly string[] RsiCycleTexts =
        {
            "4 ms（IPO_FAST）",
            "12 ms（IPO 默认）",
            "24 ms（IPO×2）"
        };

        /// <summary>导出文件名时间戳格式</summary>
        private const string StampFormat = "yyyyMMdd_HHmmss";

        /// <summary>系统配置页日志标签</summary>
        private const string LogTag = "系统配置页";

        /// <summary>台达视图状态栏前缀，标明数据来源</summary>
        private string _deltaStatusBase = string.Empty;

        /// <summary>最近识别结果；null 表示数据源为项目</summary>
        private ConfigDirectoryScan _lastScan;

        /// <summary>表格含未保存手工改动的标记</summary>
        private bool _tableEdits;

        /// <summary>分视图脏标记：仅编辑过的视图强校验</summary>
        private readonly bool[] _viewDirty = new bool[4];

        /// <summary>表格右键菜单：增删行/导出/重识别；不新增控件。</summary>
        private System.Windows.Forms.ContextMenuStrip _tableMenu;

        /// <summary>右键点击的行号（-1 = 未命中数据行）</summary>
        private int _menuRowIndex = -1;

        /// <summary>表格可编辑提示（附在状态栏末尾）</summary>
        private const string EditableHint = "；表格可编辑（双击单元格修改，右键可增删行 / 导出 CSV）";

        /// <summary>台达控制器配置视图序号（与 Segmented 顺序一致）</summary>
        private const int DeltaViewAxis = 0;
        private const int DeltaViewIo = 1;
        private const int DeltaViewCylinder = 2;
        private const int DeltaViewWeldHead = 3;

        /// <summary>轴配置表预览行</summary>
        private readonly AntdUI.AntList<AxisConfigPreviewRow> _axisRows = new AntdUI.AntList<AxisConfigPreviewRow>();

        /// <summary>IO 配置表预览行</summary>
        private readonly AntdUI.AntList<IoConfigPreviewRow> _ioRows = new AntdUI.AntList<IoConfigPreviewRow>();

        /// <summary>气缸配置表预览行</summary>
        private readonly AntdUI.AntList<CylinderConfigPreviewRow> _cylinderRows = new AntdUI.AntList<CylinderConfigPreviewRow>();

        /// <summary>焊接头从站 PDO 预览行</summary>
        private readonly AntdUI.AntList<WeldHeadPdoEntry> _weldHeadRows = new AntdUI.AntList<WeldHeadPdoEntry>();

        #endregion

        #region 页面生命周期

        /// <summary>界面可安全访问：句柄已创建且未销毁。</summary>
        private bool IsUiAccessible
        {
            get { return IsHandleCreated && !IsDisposed && !Disposing; }
        }

        public FilebackupPage()
        {
            InitializeComponent();

            // RSI 周期下拉：装载选项并后置订阅
            for (int i = 0; i < RsiCycleTexts.Length; i++)
            {
                selectRsiCycle.Items.Add(RsiCycleTexts[i]);
            }
            selectRsiCycle.SelectedIndexChanged += selectRsiCycle_SelectedIndexChanged;

            // RSI 调试面板：装载指令下拉，计时器后置
            InitRsiDebugPanel();
            this.HandleCreated += FilebackupPage_HandleCreated;

            // 台达配置：滑移视图+只读表格，列后置
            InitDeltaConfigPanel();

            // 加载配置
            LoadConfig();

            // 显示通讯日志
            GlobalCommData.CommunicationCommandReceived += GlobalCommData_CommunicationCommandReceived;
            // 通讯过程日志（配置/启停/收发）：仅本页打开时显示，文件端始终记录
            GlobalCommData.CommLogViewEvent += GlobalCommData_CommLogViewEvent;

            // 页面销毁时释放外部资源（UI 类用 DisposeComponents）
            this.HandleDestroyed += FilebackupPage_HandleDestroyed;
        }

        /// <summary>句柄创建后再启动 RSI 调试刷新计时器。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void FilebackupPage_HandleCreated(object sender, EventArgs e)
        {
            if (!_rsiDebugTimer.Enabled)
            {
                _rsiDebugTimer.Start();
            }
        }

        private void FilebackupPage_HandleDestroyed(object sender, EventArgs e)
        {
            this.HandleCreated -= FilebackupPage_HandleCreated;
            _rsiDebugTimer.Stop();
            _rsiDebugTimer.Tick -= RsiDebugTimer_Tick;
            _rsiDebugTimer.Dispose();
            DisposeComponents();
        }

        private void GlobalCommData_CommunicationCommandReceived(object sender, string e)
        {
            if (!IsUiAccessible)
            {
                return;
            }
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<object, string>(GlobalCommData_CommunicationCommandReceived), sender, e);
                    return;
                }
                if (_isLogVisible)
                {
                    AppendLog(e);
                }
            }
            catch (ObjectDisposedException)
            {
                // 页面销毁与回调并发：丢弃本条日志
            }
            catch (InvalidOperationException)
            {
                // 句柄销毁瞬间无法封送：丢弃本条日志
            }
        }

        /// <summary>通讯过程日志查看：配置/启停/收发放本页日志框，页面未打开时由全局日志写文件</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">已带来源标签的日志文本。</param>
        private void GlobalCommData_CommLogViewEvent(object sender, string e)
        {
            if (!IsUiAccessible)
            {
                return;
            }
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<object, string>(GlobalCommData_CommLogViewEvent), sender, e);
                    return;
                }
                AppendLog(e);
            }
            catch (ObjectDisposedException)
            {
                // 页面销毁与回调并发：丢弃本条
            }
            catch (InvalidOperationException)
            {
                // 句柄销毁瞬间无法封送：丢弃本条
            }
        }

        #endregion

        #region 通讯配置

        /// <summary>加载通讯配置（启用开关 + 机器人通讯模式）。</summary>
        private void LoadConfig()
        {
            // 管理器未就绪时不访问成员，保持默认态
            var comm = GlobalCommData.mCommunicationManager;
            if (comm == null)
            {
                chkEnableRobot.Checked = false;
                ApplyEnableState();
                return;
            }

            // 装载期屏蔽控件事件回写
            _loadingConfig = true;
            try
            {
                // 机器人配置（连接参数归 RSI 专属 XML，经"打开配置"编辑）
                chkEnableRobot.Checked = comm.IsRobotEnabled;
                // 通讯通道唯一为 RSI，无模式二选一
                radioModeRSI.Checked = true;

                // RSI 周期下拉按配置选中，只读不回写
                int cycleMs = comm.RsiCycleMs;
                int selectIdx = Array.IndexOf(RsiCycleOptions, cycleMs);
                selectRsiCycle.SelectedIndex = selectIdx >= 0 ? selectIdx : 1;
            }
            finally
            {
                _loadingConfig = false;
            }

            ApplyEnableState();
        }

        /// <summary>RSI 通讯周期下拉选中：经 Manager 门面写入 RsiComm.CycleMs 并持久化到 RobotServer Xdoc。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void selectRsiCycle_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            if (_loadingConfig) return;
            int idx = selectRsiCycle.SelectedIndex;
            if (idx < 0 || idx >= RsiCycleOptions.Length) return;

            GlobalCommData.mCommunicationManager.SetRsiCycleMs(RsiCycleOptions[idx]);
            AppendLog(string.Format("RSI 通讯周期已切换为 {0}", RsiCycleTexts[idx]));
        }

        /// <summary>保存通讯配置（机器人通讯模式 + 启用开关已即时</summary>
        /// <remarks>生效，此处兜底统一保存）。</remarks>
        private void SaveConfig()
        {
            // 保存机器人通讯启用开关（通道唯一为 RSI，无模式项）
            GlobalCommData.mCommunicationManager.IsRobotEnabled = chkEnableRobot.Checked;

            AppendLog("通讯配置已保存");
        }

        /// <summary>启用/禁用开关：写 Manager 启用配置并联动 panel 内控件可用性；</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>停用时同时关闭 RSI 监听。</remarks>
        private void chkEnable_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_loadingConfig) return;
            GlobalCommData.mCommunicationManager.IsRobotEnabled = chkEnableRobot.Checked;
            // 取消勾选必须停 RSI 通道，避免继续收包
            if (!chkEnableRobot.Checked)
            {
                var comm = GlobalCommData.mCommunicationManager;
                bool ok = comm.DisconnectRobot();
                AppendLog(ok
                    ? "机器人通讯已停用 监听关闭成功"
                    : "机器人通讯已停用 监听关闭失败 " + (comm.RsiComm != null ? comm.RsiComm.LastError : "管理器未就绪"));
            }
            ApplyEnableState();
            if (chkEnableRobot.Checked) AppendLog("机器人通讯已启用");
        }

        /// <summary>按启用开关刷新面板控件与按钮可用性。</summary>
        private void ApplyEnableState()
        {
            // 设置控件不禁用，仅连接按钮跟随开关
            bool enabled = chkEnableRobot.Checked;
            btnConnect.Enabled = enabled;
            btnRobotDisconnect.Enabled = enabled;
        }

        /// <summary>启动按钮：装载配置并连接机器人通道。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnRobotStart_Click(object sender, EventArgs e)
        {
            if (!TryPassCommAction("开启机器人通讯监听")) return;
            var comm = GlobalCommData.mCommunicationManager;
            if (!comm.IsRobotEnabled)
            {
                AppendLog("机器人通讯未启用，请先勾选启用");
                return;
            }
            try
            {
                AppendLog("正在开启机器人通讯监听（RSI）...");
                comm.InitializeRobotComm();
                bool ok = comm.ConnectRobot();
                if (ok)
                {
                    AppendLog(string.Format("机器人通讯监听 开启成功 本地 {0}:{1}",
                        comm.RsiComm.LocalIp, comm.RsiComm.LocalPort));
                }
                else
                {
                    AppendLog("机器人通讯监听 开启失败 " +
                        (comm.RsiComm != null ? comm.RsiComm.LastError : "管理器未就绪"));
                }
            }
            catch (Exception ex)
            {
                AppendLog("机器人通讯启动异常: " + ex.Message);
            }
        }

        /// <summary>关闭按钮：按当前模式断开机器人通道（走 Manager 门面）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnRobotStop_Click(object sender, EventArgs e)
        {
            if (!TryPassCommAction("关闭机器人通讯监听")) return;
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                bool ok = comm.DisconnectRobot();
                AppendLog(ok
                    ? "机器人通讯监听 关闭成功"
                    : "机器人通讯监听 关闭失败 " + (comm.RsiComm != null ? comm.RsiComm.LastError : "管理器未就绪"));
            }
            catch (Exception ex)
            {
                AppendLog("机器人通讯关闭异常: " + ex.Message);
            }
        }

        /// <summary>通讯动作门控：初始化中与关闭中拒绝设备动作</summary>
        /// <param name="action">动作描述，用于提示与日志。</param>
        /// <returns>true 表示允许执行。</returns>
        /// <remarks>初始化期间禁止手动开启/关闭机器人通道：初始化链正在建立同一通道，
        /// 并发操作会与初始化链争用机器人端 RSI XML（文件占用）且违反
        /// 「初始化动作结束拿到结果后才允许重连或设备动作」的强制要求。</remarks>
        private bool TryPassCommAction(string action)
        {
            if (DeviceControlWork.Instance.IsShuttingDown)
            {
                AntdUI.Message.warn(this.FindForm(), "程序正在关闭，暂不支持" + action + "。");
                AppendLog("通讯动作被拒绝 " + action + " 原因 正在关闭");
                return false;
            }
            if (DeviceControlWork.Instance.IsInitializing)
            {
                AntdUI.Message.warn(this.FindForm(), "设备初始化中仅可查询，" + action + " 已拒绝。");
                AppendLog("通讯动作被拒绝 " + action + " 原因 初始化中");
                return false;
            }
            return true;
        }

        /// <summary>打开配置按钮：弹出 FileEditorForm 编辑 Manager 自持的 RobotServer.xml（RSI 连接 + 收发格式契</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>约，连接参数单一真相源）。</remarks>
        private void btnConfig_Click(object sender, EventArgs e)
        {
            var comm = GlobalCommData.mCommunicationManager;
            const string docType = "机器人RSI配置";
            try
            {
                using (FileEditorForm editor = new FileEditorForm())
                {
                    editor.OpenDocument(comm.RobotServerXdoc, docType);
                    editor.Size = new System.Drawing.Size(960, 640);
                    AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), docType, editor, TType.Info)
                    {
                        CloseIcon = true,
                        BtnHeight = 0
                    });
                }
                AppendLog(docType + " 编辑器已关闭（保存需在编辑器内点保存；连接参数变更需重连或重启生效）");
            }
            catch (Exception ex)
            {
                AppendLog("打开配置失败: " + ex.Message);
            }
        }

        /// <summary>保存配置按钮。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnKeepConfig_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }

        /// <summary>RSI 模式标识（通讯通道唯一为 RSI，无互斥逻辑，仅显示用</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>途）。</remarks>
        private void radioModeRSI_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
        }

        #endregion

        #region RSI 通讯调试（面板：下行下发 / 焊参注入 / 收发包监视）

        /// <summary>下行指令下拉项（数值码 + 释义）。</summary>
        private sealed class TxCmdOption
        {
            /// <summary>下行数值码</summary>
            public int Code;

            /// <summary>显示文本</summary>
            public string Text;
        }

        /// <summary>下行指令下拉选项集合（装载于构造函数）。</summary>
        private readonly List<TxCmdOption> _txCmdOptions = new List<TxCmdOption>();

        /// <summary>RSI 调试刷新计时器，周期刷新统计。</summary>
        private readonly System.Windows.Forms.Timer _rsiDebugTimer = new System.Windows.Forms.Timer();

        /// <summary>装载 RSI 调试下拉选项并启动刷新计时器。</summary>
        private void InitRsiDebugPanel()
        {
            // 下行选项：数值码 1~9 对应 CommandCore 的 Tx* 常量（10=备用，不映射）
            for (int code = 1; code <= 9; code++)
            {
                string name = CommandCore.TxCodeToCommandName(code);
                if (string.IsNullOrEmpty(name)) continue;
                TxCmdOption opt = new TxCmdOption { Code = code, Text = string.Format("{0} {1}", code, CommandCore.TranslateCommand(name)) };
                _txCmdOptions.Add(opt);
                selectTxCmd.Items.Add(opt.Text);
            }
            if (_txCmdOptions.Count > 0) selectTxCmd.SelectedIndex = 0;

            // 刷新计时器每 500ms 刷新统计与最近包
            // 启动时机在 HandleCreated（见构造函数），不在构造期启动
            _rsiDebugTimer.Interval = 500;
            _rsiDebugTimer.Tick += RsiDebugTimer_Tick;
        }

        /// <summary>RSI 调试定时刷新：状态、计数与最近包。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void RsiDebugTimer_Tick(object sender, EventArgs e)
        {
            if (!IsUiAccessible)
            {
                return;
            }
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null || comm.RsiComm == null)
                {
                    lblRsiStat.Text = "RSI 状态：管理器未就绪";
                    return;
                }

                KukaRsiComm rsi = comm.RsiComm;
                // 监听开启≠在线，在线以最近收包超时为准
                string state;
                if (!rsi.IsConnected)
                {
                    state = string.IsNullOrEmpty(rsi.LastError)
                        ? "监听未开启"
                        : "监听未开启：" + rsi.LastError;
                }
                else if (rsi.RxPacketCount <= 0)
                {
                    state = "监听已开启·等待机器人首包";
                }
                else if (rsi.IsRobotOnline)
                {
                    state = "机器人在线";
                }
                else
                {
                    DateTime last = rsi.LastReceiveTime;
                    double idleSec = last == DateTime.MinValue ? 0.0 : (DateTime.Now - last).TotalSeconds;
                    state = string.Format("机器人离线（{0:F1}s 无包）", idleSec);
                }

                lblRsiStat.Text = string.Format("RSI 状态：{0}  收包 {1}  发包 {2}  上行码 {3}  IPOC {4}",
                    state,
                    rsi.RxPacketCount,
                    rsi.TxPacketCount,
                    string.IsNullOrEmpty(rsi.LastRxRaw) ? "无" : CommandCore.DescribeRxCode(rsi.LastRxCmd),
                    string.IsNullOrEmpty(rsi.LastIpoc) ? "无" : rsi.LastIpoc);

                txtRsiRxPacket.Text = rsi.LastRxRaw;
                txtRsiTxPacket.Text = rsi.LastTxRaw;
            }
            catch (Exception ex)
            {
                lblRsiStat.Text = "RSI 状态刷新异常 " + ex.Message;
            }
        }

        /// <summary>下发下行指令（经 RSI 数值码通道，下一帧生效）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>RSI 为被动回复模型，本指令只写回复缓存。
        /// 实际发出在下一个回复周期。</remarks>
        private void btnSendTx_Click(object sender, EventArgs e)
        {
            try
            {
                int idx = selectTxCmd.SelectedIndex;
                if (idx < 0 || idx >= _txCmdOptions.Count)
                {
                    AppendLog("下行下发失败 未选择指令");
                    return;
                }
                int code = _txCmdOptions[idx].Code;
                double payload = ParseDebugDouble(txtTxPayload.Text);

                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null || !comm.IsRobotEnabled)
                {
                    AppendLog("下行下发失败 机器人通讯未启用");
                    return;
                }
                comm.SendRobotCommand(code, payload);
                GlobalCommData.BroadcastCommunicationCommand(CommandCore.TxCodeToCommandName(code));
                AppendLog(string.Format("已下发下行指令 码 {0} 载荷 {1} 释意 {2}（下一帧生效）",
                    code, payload, CommandCore.DescribeTxCode(code)));
            }
            catch (Exception ex)
            {
                AppendLog("下行下发异常 " + ex.Message);
            }
        }

        /// <summary>写入焊参通道五个具名元素。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnInjectWeldParams_Click(object sender, EventArgs e)
        {
            try
            {
                var comm = GlobalCommData.mCommunicationManager;
                if (comm == null || !comm.IsRobotEnabled)
                {
                    AppendLog("焊参注入失败 机器人通讯未启用");
                    return;
                }

                double power = ParseDebugDouble(txtTestPower.Text);
                double wire = ParseDebugDouble(txtTestWire.Text);
                double weldSpeed = ParseDebugDouble(txtTestWeldSpeed.Text);
                double weave = ParseDebugDouble(txtTestWeave.Text);
                double cutWire = ParseDebugDouble(txtTestCutWire.Text);

                comm.UpdateRobotWeldParams(power, wire, cutWire, weldSpeed, weave);
                AppendLog(string.Format("已写入焊参通道 功率 {0} 送丝 {1} 速度 {2} 摆幅 {3} 剪丝 {4}",
                    power, wire, weldSpeed, weave, cutWire));
                // 焊参通道与生产链路共用回复缓存
                // 写入仅下一周期可见，只用于验证连通。
                AppendLog("提示：生产链路每拍覆盖同一通道，本写入仅下一帧有效（焊接中立即被真实工艺值覆盖）");
            }
            catch (Exception ex)
            {
                AppendLog("焊参注入异常 " + ex.Message);
            }
        }

        /// <summary>解析调试输入框数值（非法值按 0 处理）。</summary>
        /// <param name="text">输入文本</param>
        /// <returns>解析出的数值</returns>
        private static double ParseDebugDouble(string text)
        {
            if (string.IsNullOrEmpty(text) || !double.TryParse(text.Trim(), out double value))
            {
                return 0.0;
            }
            return value;
        }

        #endregion

        #region 系统配置文件

        /// <summary>选择配置文档目录：识别源与反向导出目标。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnBrowseDir_Click(object sender, EventArgs e)
        {
            AntdUI.FolderBrowserDialog dialog = new AntdUI.FolderBrowserDialog
            {
                DirectoryPath = txtCsvDir.Text
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtCsvDir.Text = dialog.DirectoryPath;
                // 导入/识别日志走系统配置页日志，不进通讯日志
                GlobalCommData.ShowLog(LogTag, "配置文档目录已设为 " + dialog.DirectoryPath);
                // 选中目录即识别，结论写状态栏与表格
                ScanDeltaDirectory(dialog.DirectoryPath);
            }
        }

        #endregion

        #region 台达控制器配置

        /// <summary>初始化滑移视图与表格，列一律后置</summary>
        private void InitDeltaConfigPanel()
        {
            if (segDeltaView == null || tableDeltaView == null) return;

            segDeltaView.Items.Clear();
            segDeltaView.Items.Add(new AntdUI.SegmentedItem { Text = "轴" });
            segDeltaView.Items.Add(new AntdUI.SegmentedItem { Text = "IO" });
            segDeltaView.Items.Add(new AntdUI.SegmentedItem { Text = "气缸" });
            segDeltaView.Items.Add(new AntdUI.SegmentedItem { Text = "焊接头从站" });

            tableDeltaView.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            tableDeltaView.EmptyHeader = true;
            // 表格为唯一提交源：双击编辑并写回行
            tableDeltaView.EditMode = AntdUI.TEditMode.DoubleClick;
            tableDeltaView.CellEndEdit += TableDeltaView_CellEndEdit;
            tableDeltaView.CellClick += TableDeltaView_CellClick;
            InitDeltaTableMenu();

            // 默认展示项目 XML，不要求先选目录
            // 切视图时逐次读盘，不缓存内存快照
            _deltaStatusBase = "数据来源：项目 XML（Config\\MotionControl\\Motion_XML\\DeltaMotionConfig.xml / WeldHead_XML\\WeldHeadSlave.xml）";
            lblDeltaStatus.Text = _deltaStatusBase;
            lblDeltaStatus.ForeColor = System.Drawing.Color.DimGray;

            segDeltaView.SelectIndex = DeltaViewAxis;
            ApplyDeltaView(DeltaViewAxis);
            segDeltaView.SelectIndexChanged += SegDeltaView_SelectIndexChanged;

            btnDeltaGen.Enabled = false;
        }

        /// <summary>滑移视图切换：按视图重建列并绑定对应数据源</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void SegDeltaView_SelectIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            ApplyDeltaView(segDeltaView.SelectIndex);
        }

        /// <summary>按视图序号装载数据并重绑表格</summary>
        /// <remarks>数据源为项目 XML 时每次切视图重新读盘</remarks>
        /// <param name="index">视图序号</param>
        private void ApplyDeltaView(int index)
        {
            if (tableDeltaView == null) return;

            // 有未保存改动则不重读盘
            if (!IsDocumentSource() && !_tableEdits)
            {
                LoadProjectXmlRows();
            }

            BindDeltaView(index);
        }

        /// <summary>按视图序号重建列（可编辑）并绑定当前行集合</summary>
        /// <remarks>不读盘</remarks>
        /// <param name="index">视图序号</param>
        private void BindDeltaView(int index)
        {
            if (tableDeltaView == null) return;

            if (index == DeltaViewIo)
            {
                tableDeltaView.Columns = new AntdUI.ColumnCollection
                {
                    new AntdUI.Column("IoType", "类型") { Width = "100", Editable = true },
                    new AntdUI.Column("IoName", "名称") { Width = "300", Editable = true },
                    new AntdUI.Column("NodeId", "节点号") { Width = "90", Editable = true },
                    new AntdUI.Column("SlotNo", "槽位") { Width = "90", Editable = true },
                    new AntdUI.Column("BitNo", "位号") { Width = "90", Editable = true },
                    new AntdUI.Column("Enabled", "启用") { Width = "90", Editable = true },
                    new AntdUI.Column("Remark", "说明") { Width = "520", Editable = true }
                };
                tableDeltaView.Binding(_ioRows);
                UpdateDeltaStatusHint(index);
                return;
            }

            if (index == DeltaViewCylinder)
            {
                tableDeltaView.Columns = new AntdUI.ColumnCollection
                {
                    new AntdUI.Column("CylinderName", "气缸名称") { Width = "160", Editable = true },
                    new AntdUI.Column("StretchSensor", "伸出传感器") { Width = "150", Editable = true },
                    new AntdUI.Column("RetractSensor", "缩回传感器") { Width = "150", Editable = true },
                    new AntdUI.Column("CenterSensor", "中间传感器") { Width = "150", Editable = true },
                    new AntdUI.Column("StretchOutput", "伸出输出") { Width = "110", Editable = true },
                    new AntdUI.Column("RetractOutput", "缩回输出") { Width = "110", Editable = true },
                    new AntdUI.Column("StretchActionName", "伸出动作") { Width = "200", Editable = true },
                    new AntdUI.Column("RetractActionName", "缩回动作") { Width = "200", Editable = true }
                };
                tableDeltaView.Binding(_cylinderRows);
                UpdateDeltaStatusHint(index);
                return;
            }

            if (index == DeltaViewWeldHead)
            {
                tableDeltaView.Columns = new AntdUI.ColumnCollection
                {
                    new AntdUI.Column("Seq", "序号") { Width = "60" },
                    new AntdUI.Column("VarName", "变量") { Width = "220", Editable = true },
                    new AntdUI.Column("DisplayName", "名称") { Width = "150", Editable = true },
                    new AntdUI.Column("DataType", "数据类型") { Width = "95", Editable = true },
                    new AntdUI.Column("SdoIndex", "SDO Index") { Width = "100", Editable = true },
                    new AntdUI.Column("SdoSubIndex", "SDO SubIndex") { Width = "110", Editable = true },
                    new AntdUI.Column("Access", "Access") { Width = "80", Editable = true },
                    new AntdUI.Column("PdoIndex", "PDO Index") { Width = "100", Editable = true },
                    new AntdUI.Column("PdoSubIndex", "PDO SubIndex") { Width = "110", Editable = true },
                    new AntdUI.Column("Description", "说明") { Width = "255", Editable = true }
                };
                tableDeltaView.Binding(_weldHeadRows);
                UpdateDeltaStatusHint(index);
                return;
            }

            tableDeltaView.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("AxisName", "轴名称") { Width = "80", Editable = true },
                new AntdUI.Column("NodeId", "节点号") { Width = "56", Editable = true },
                new AntdUI.Column("SlotNo", "槽位") { Width = "50", Editable = true },
                new AntdUI.Column("Enabled", "启用") { Width = "50", Editable = true },
                new AntdUI.Column("Equiv", "当量") { Width = "58", Editable = true },
                new AntdUI.Column("StrVel", "启动速度") { Width = "72", Editable = true },
                new AntdUI.Column("ConstVel", "恒速") { Width = "62", Editable = true },
                new AntdUI.Column("AccTime", "加速时间") { Width = "72", Editable = true },
                new AntdUI.Column("DecTime", "减速时间") { Width = "72", Editable = true },
                new AntdUI.Column("STime", "S 时间") { Width = "56", Editable = true },
                new AntdUI.Column("EndVel", "终点速度") { Width = "72", Editable = true },
                new AntdUI.Column("HomeMode", "回零模式") { Width = "74", Editable = true },
                new AntdUI.Column("HomeOffset", "回零偏移") { Width = "74", Editable = true },
                new AntdUI.Column("HomeFirstVel", "一次回零速度") { Width = "82", Editable = true },
                new AntdUI.Column("HomeSecondVel", "二次回零速度") { Width = "82", Editable = true },
                new AntdUI.Column("HomeAcc", "回零加速度") { Width = "72", Editable = true },
                new AntdUI.Column("PositiveLimit", "正限位") { Width = "62", Editable = true },
                new AntdUI.Column("NegativeLimit", "负限位") { Width = "62", Editable = true },
                new AntdUI.Column("SoftLimitMode", "软限位模式") { Width = "72", Editable = true }
            };
            tableDeltaView.Binding(_axisRows);
            UpdateDeltaStatusHint(index);
        }

        /// <summary>扫描配置文档目录并刷新表格与状态栏。</summary>
        /// <param name="directoryPath">配置文档目录</param>
        private void ScanDeltaDirectory(string directoryPath)
        {
            // 有未保存改动先询问，识别会覆盖表格
            if (_tableEdits)
            {
                DialogResult confirm = AntdUI.Modal.open(new AntdUI.Modal.Config(FindForm(), "重新识别目录",
                    "当前表格含未保存的手工修改，重新识别将用目录文档覆盖表格内容（改动不会写盘）。是否继续？", TType.Warn));
                if (confirm != DialogResult.OK) return;
            }

            ConfigDirectoryScan scan = DeltaControllerConfigImporter.Scan(directoryPath);
            _lastScan = scan;
            ClearDirty();

            // 系统配置页日志只走 ShowLog（不进通讯日志）
            GlobalCommData.ShowLog(LogTag, "目录识别 " + directoryPath + " → " + scan.Message,
                scan.Level == ConfigDirectoryLevel.Invalid ? MessageLevel.Warning : MessageLevel.Info);

            RefreshDeltaPreview(scan);
            UpdateDeltaStatusText(scan);
            ApplyDeltaView(segDeltaView.SelectIndex);
        }

        /// <summary>当前表格数据源是否为配置文档（false = 项目 XML）。</summary>
        /// <returns>识别到任一配置文档返回 true</returns>
        private bool IsDocumentSource()
        {
            ConfigDirectoryScan scan = _lastScan;
            return scan != null && !string.IsNullOrEmpty(scan.DirectoryPath)
                && (scan.HasAxisCsv || scan.HasIoCsv || scan.HasCylinderCsv || scan.HasWeldHeadEsi || scan.HasWeldHeadProtocol);
        }

        /// <summary>按识别结果刷新四类预览数据。</summary>
        /// <remarks>未识别时回落到项目 XML，只读。</remarks>
        /// <param name="scan">识别结果</param>
        private void RefreshDeltaPreview(ConfigDirectoryScan scan)
        {
            _axisRows.Clear();
            _ioRows.Clear();
            _cylinderRows.Clear();
            _weldHeadRows.Clear();

            if (!IsDocumentSource())
            {
                // 未识别到文档则保持项目 XML 视图
                LoadProjectXmlRows();
                return;
            }

            LoadAxisPreview(Path.Combine(scan.DirectoryPath, DeltaControllerConfigImporter.AxisFileName));
            LoadIoPreview(Path.Combine(scan.DirectoryPath, DeltaControllerConfigImporter.IOFileName));
            LoadCylinderPreview(Path.Combine(scan.DirectoryPath, DeltaControllerConfigImporter.CylinderFileName));

            for (int i = 0; i < scan.PdoEntries.Count; i++) _weldHeadRows.Add(scan.PdoEntries[i]);
        }

        /// <summary>装载项目现有 XML：轴/IO/气缸 ← DeltaMotionConfig.xml；焊接头 PDO ← WeldHeadSlave.xml</summary>
        /// <remarks>每次调用都重读盘，保证看到最新配置。</remarks>
        private void LoadProjectXmlRows()
        {
            _axisRows.Clear();
            _ioRows.Clear();
            _cylinderRows.Clear();
            _weldHeadRows.Clear();

            try
            {
                // 独立实例由 Manager 提供，不碰运行期配置，不写日志
                DeltaMotionConfig config = MontionManager.Instance.CreatePreviewMotionConfig();

                for (int i = 0; i < config.Axes.Count; i++)
                {
                    DeltaAxisConfig cfg = config.Axes[i];
                    AxisConfigPreviewRow row = new AxisConfigPreviewRow
                    {
                        AxisName = cfg.AxisName,
                        NodeId = cfg.NodeId.ToString(),
                        SlotNo = cfg.SlotNo.ToString(),
                        Enabled = cfg.Enabled ? "TRUE" : "FALSE",
                        Equiv = cfg.Equiv.ToString("0.####"),
                        StrVel = cfg.StrVel.ToString("0.####"),
                        ConstVel = cfg.ConstVel.ToString("0.####"),
                        AccTime = cfg.AccTime.ToString("0.####"),
                        DecTime = cfg.DecTime.ToString("0.####"),
                        STime = cfg.STime.ToString("0.####"),
                        EndVel = cfg.EndVel.ToString("0.####"),
                        HomeMode = cfg.HomeMode.ToString(),
                        HomeOffset = cfg.HomeOffset.ToString("0.####"),
                        HomeFirstVel = cfg.HomeFirstVel.ToString("0.####"),
                        HomeSecondVel = cfg.HomeSecondVel.ToString("0.####"),
                        HomeAcc = cfg.HomeAcc.ToString("0.####"),
                        PositiveLimit = cfg.PositiveLimit.ToString("0.####"),
                        NegativeLimit = cfg.NegativeLimit.ToString("0.####"),
                        SoftLimitMode = cfg.SoftLimitMode.ToString()
                    };
                    _axisRows.Add(row);
                }

                for (int i = 0; i < config.Inputs.Count; i++)
                {
                    _ioRows.Add(BuildIoPreviewRow("输入", config.Inputs[i]));
                }
                for (int i = 0; i < config.Outputs.Count; i++)
                {
                    _ioRows.Add(BuildIoPreviewRow("输出", config.Outputs[i]));
                }

                for (int i = 0; i < config.Cylinders.Count; i++)
                {
                    DeltaCylinderConfig cfg = config.Cylinders[i];
                    CylinderConfigPreviewRow row = new CylinderConfigPreviewRow
                    {
                        CylinderName = cfg.CylinderName,
                        StretchSensor = cfg.StretchSensorName,
                        RetractSensor = cfg.RetractSensorName,
                        CenterSensor = cfg.CenterSensorName,
                        StretchOutput = cfg.StretchOutputName,
                        RetractOutput = cfg.RetractOutputName,
                        StretchActionName = cfg.StretchActionName,
                        RetractActionName = cfg.RetractActionName
                    };
                    _cylinderRows.Add(row);
                }

                // 焊接头 PDO 取运行期已装载配置，不再重复加载
                MontionManager manager = MontionManager.Instance;
                if (manager != null)
                {
                    WeldHeadConfig weldHead = manager.WeldHeadConfig;
                    if (weldHead != null)
                    {
                        for (int i = 0; i < weldHead.PdoMap.Count; i++) _weldHeadRows.Add(weldHead.PdoMap[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(LogTag, "项目 XML 预览装载失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>由运行期 IO 配置构造预览行</summary>
        /// <param name="ioType">输入 / 输出</param>
        /// <param name="cfg">IO 配置</param>
        /// <returns>预览行</returns>
        private static IoConfigPreviewRow BuildIoPreviewRow(string ioType, DeltaIOConfig cfg)
        {
            IoConfigPreviewRow row = new IoConfigPreviewRow
            {
                IoType = ioType,
                IoName = cfg.IOName,
                NodeId = cfg.NodeId.ToString(),
                SlotNo = cfg.SlotNo.ToString(),
                BitNo = cfg.BitNo.ToString(),
                Enabled = cfg.Enabled ? "TRUE" : "FALSE",
                Remark = cfg.Remark
            };
            return row;
        }

        /// <summary>状态栏追加当前视图行数与空态说明</summary>
        /// <remarks>明确区分「暂无数据」与「未接上」</remarks>
        /// <param name="index">视图序号</param>
        private void UpdateDeltaStatusHint(int index)
        {
            if (lblDeltaStatus == null) return;

            string viewName;
            int count;
            string emptyHint;
            if (index == DeltaViewIo)
            {
                viewName = "IO";
                count = _ioRows.Count;
                emptyHint = "项目 XML 的 ROOT/INPUTS 与 ROOT/OUTPUTS 为空节点";
            }
            else if (index == DeltaViewCylinder)
            {
                viewName = "气缸";
                count = _cylinderRows.Count;
                emptyHint = "项目 XML 的 ROOT/CYLINDERS 为空节点";
            }
            else if (index == DeltaViewWeldHead)
            {
                viewName = "焊接头从站";
                count = _weldHeadRows.Count;
                emptyHint = "项目 XML 的 ROOT/IO_MAP 为空（尚未导入焊接头 IO 表）";
            }
            else
            {
                viewName = "轴";
                count = _axisRows.Count;
                emptyHint = "项目 XML 的 ROOT/AXES 为空节点";
            }

            if (count == 0)
            {
                lblDeltaStatus.Text = _deltaStatusBase + "；当前视图【" + viewName + "】暂无数据 —— " + emptyHint + EditableHint;
            }
            else
            {
                lblDeltaStatus.Text = _deltaStatusBase + "；当前视图【" + viewName + "】共 " + count + " 行" + EditableHint;
            }

            // 四视图合计有行即可保存
            if (btnDeltaGen != null)
            {
                btnDeltaGen.Enabled = _axisRows.Count + _ioRows.Count + _cylinderRows.Count + _weldHeadRows.Count > 0;
            }
        }

        /// <summary>更新状态栏文本与配色（分级提示，禁止静默）</summary>
        /// <param name="scan">识别结果</param>
        private void UpdateDeltaStatusText(ConfigDirectoryScan scan)
        {
            if (lblDeltaStatus == null) return;
            if (scan == null || scan.DirectoryPath.Length == 0 || scan.Level == ConfigDirectoryLevel.Invalid)
            {
                _deltaStatusBase = "数据来源：项目 XML（DeltaMotionConfig.xml / WeldHeadSlave.xml）；请选择配置文档目录（轴/IO/气缸三表与焊接头 ESI/IO 表可只含其中一部分，缺文件节跳过）";
                lblDeltaStatus.Text = _deltaStatusBase;
                lblDeltaStatus.ForeColor = System.Drawing.Color.DimGray;
                return;
            }

            if (scan.Level == ConfigDirectoryLevel.Ready)
            {
                _deltaStatusBase = "数据来源：配置文档（" + scan.DirectoryPath + "）；【已加载，可保存】" + scan.Message;
                lblDeltaStatus.ForeColor = System.Drawing.Color.SeaGreen;
            }
            else if (scan.Level == ConfigDirectoryLevel.Partial)
            {
                bool blocked = DeltaControllerConfigImporter.GetParseFailures(scan).Count > 0;
                _deltaStatusBase = "数据来源：配置文档（" + scan.DirectoryPath + "）；"
                    + (blocked ? "【已加载，保存被拦截】" : "【已加载，可保存（缺文件节跳过）】")
                    + scan.Message;
                lblDeltaStatus.ForeColor = blocked ? System.Drawing.Color.DarkOrange : System.Drawing.Color.SeaGreen;
            }
            else
            {
                _deltaStatusBase = "数据来源：项目 XML（DeltaMotionConfig.xml / WeldHeadSlave.xml）；【不可用】" + scan.Message;
                lblDeltaStatus.ForeColor = System.Drawing.Color.Firebrick;
            }
            lblDeltaStatus.Text = _deltaStatusBase;
        }

        /// <summary>装载轴配置表预览（表头 AXIS_NAME 等，按列名取值）</summary>
        /// <param name="filePath">CSV 路径</param>
        private void LoadAxisPreview(string filePath)
        {
            if (!DeltaControllerConfigImporter.TryReadCsvTable(filePath, out string[] headers, out List<string[]> rows, out string message))
            {
                if (message.Length > 0) GlobalCommData.ShowLog(LogTag, "轴配置表预览跳过 " + message, MessageLevel.Warning);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                AxisConfigPreviewRow row = new AxisConfigPreviewRow
                {
                    AxisName = CsvCell(headers, rows[i], "AXIS_NAME"),
                    NodeId = CsvCell(headers, rows[i], "NODE_ID"),
                    SlotNo = CsvCell(headers, rows[i], "SLOT_NO"),
                    Enabled = CsvCell(headers, rows[i], "ENABLED"),
                    Equiv = CsvCell(headers, rows[i], "EQUIV"),
                    StrVel = CsvCell(headers, rows[i], "STR_VEL"),
                    ConstVel = CsvCell(headers, rows[i], "CONST_VEL"),
                    AccTime = CsvCell(headers, rows[i], "ACC_TIME"),
                    DecTime = CsvCell(headers, rows[i], "DEC_TIME"),
                    STime = CsvCell(headers, rows[i], "S_TIME"),
                    EndVel = CsvCell(headers, rows[i], "END_VEL"),
                    HomeMode = CsvCell(headers, rows[i], "HOME_MODE"),
                    HomeOffset = CsvCell(headers, rows[i], "HOME_OFFSET"),
                    HomeFirstVel = CsvCell(headers, rows[i], "HOME_FIRST_VEL"),
                    HomeSecondVel = CsvCell(headers, rows[i], "HOME_SECOND_VEL"),
                    HomeAcc = CsvCell(headers, rows[i], "HOME_ACC"),
                    PositiveLimit = CsvCell(headers, rows[i], "POSITIVE_LIMIT"),
                    NegativeLimit = CsvCell(headers, rows[i], "NEGATIVE_LIMIT"),
                    SoftLimitMode = CsvCell(headers, rows[i], "SOFT_LIMIT_MODE")
                };
                _axisRows.Add(row);
            }
        }

        /// <summary>装载 IO 配置表预览</summary>
        /// <param name="filePath">CSV 路径</param>
        private void LoadIoPreview(string filePath)
        {
            if (!DeltaControllerConfigImporter.TryReadCsvTable(filePath, out string[] headers, out List<string[]> rows, out string message))
            {
                if (message.Length > 0) GlobalCommData.ShowLog(LogTag, "IO 配置表预览跳过 " + message, MessageLevel.Warning);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                IoConfigPreviewRow row = new IoConfigPreviewRow
                {
                    IoType = CsvCell(headers, rows[i], "IO_TYPE"),
                    IoName = CsvCell(headers, rows[i], "IO_NAME"),
                    NodeId = CsvCell(headers, rows[i], "NODE_ID"),
                    SlotNo = CsvCell(headers, rows[i], "SLOT_NO"),
                    BitNo = CsvCell(headers, rows[i], "BIT_NO"),
                    Enabled = CsvCell(headers, rows[i], "ENABLED"),
                    Remark = CsvCell(headers, rows[i], "REMARK")
                };
                _ioRows.Add(row);
            }
        }

        /// <summary>装载气缸配置表预览</summary>
        /// <param name="filePath">CSV 路径</param>
        private void LoadCylinderPreview(string filePath)
        {
            if (!DeltaControllerConfigImporter.TryReadCsvTable(filePath, out string[] headers, out List<string[]> rows, out string message))
            {
                if (message.Length > 0) GlobalCommData.ShowLog(LogTag, "气缸配置表预览跳过 " + message, MessageLevel.Warning);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                CylinderConfigPreviewRow row = new CylinderConfigPreviewRow
                {
                    CylinderName = CsvCell(headers, rows[i], "CYLINDER_NAME"),
                    StretchSensor = CsvCell(headers, rows[i], "STRETCH_SENSOR"),
                    RetractSensor = CsvCell(headers, rows[i], "RETRACT_SENSOR"),
                    CenterSensor = CsvCell(headers, rows[i], "CENTER_SENSOR"),
                    StretchOutput = CsvCell(headers, rows[i], "STRETCH_OUTPUT"),
                    RetractOutput = CsvCell(headers, rows[i], "RETRACT_OUTPUT"),
                    StretchActionName = CsvCell(headers, rows[i], "STRETCH_ACTION_NAME"),
                    RetractActionName = CsvCell(headers, rows[i], "RETRACT_ACTION_NAME")
                };
                _cylinderRows.Add(row);
            }
        }

        /// <summary>按表头名取单元格值（大小写不敏感）</summary>
        /// <param name="headers">表头</param>
        /// <param name="row">数据行</param>
        /// <param name="name">列名</param>
        /// <returns>单元格文本；缺列或越界返回空串</returns>
        private static string CsvCell(string[] headers, string[] row, string name)
        {
            if (headers == null || row == null) return string.Empty;
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] == null) continue;
                if (!string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) continue;
                return i < row.Length ? row[i] : string.Empty;
            }
            return string.Empty;
        }

        /// <summary>生成 XML：唯一写入口，写前自动备份，校验失败零写入</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnDeltaGen_Click(object sender, EventArgs e)
        {
            // 先做页面级校验，失败零写入
            List<string> errors = ValidateCurrentTables(out List<string> hints);
            if (errors.Count > 0)
            {
                string errorText = "重载保存被拦下（未写入任何配置）：" + string.Join("；", errors.ToArray());
                lblDeltaStatus.Text = errorText;
                lblDeltaStatus.ForeColor = System.Drawing.Color.Firebrick;
                GlobalCommData.ShowLog(LogTag, errorText, MessageLevel.Error);
                return;
            }

            // 按项目表头导出表格到临时目录
            string sourceDir = string.Empty;
            string writeError;
            try
            {
                sourceDir = WriteTableSourceDirectory(out writeError);
            }
            catch (Exception ex)
            {
                writeError = ex.Message;
            }
            if (sourceDir.Length == 0)
            {
                string errorText = "重载保存被拦下（未写入任何配置）：导出表格失败 " + writeError;
                lblDeltaStatus.Text = errorText;
                lblDeltaStatus.ForeColor = System.Drawing.Color.Firebrick;
                GlobalCommData.ShowLog(LogTag, errorText, MessageLevel.Error);
                return;
            }

            bool ok;

            string message;
            try
            {
                List<WeldHeadPdoEntry> pdoEntries = new List<WeldHeadPdoEntry>(_weldHeadRows);
                ok = DeltaControllerConfigImporter.Generate(sourceDir, pdoEntries, out message);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(LogTag, "重载保存异常 " + ex.Message, MessageLevel.Error);
                message = "重载保存异常 " + ex.Message;
                ok = false;
            }

            // 结论只打一条，由 Generate 统一输出
            if (!ok && message.IndexOf("重载保存异常", StringComparison.Ordinal) >= 0)
            {
                GlobalCommData.ShowLog(LogTag, "重载保存失败 " + message, MessageLevel.Error);
            }
            lblDeltaStatus.Text = ok ? message + BuildHintSuffix(hints) : "重载保存失败：" + message;
            lblDeltaStatus.ForeColor = ok ? System.Drawing.Color.SeaGreen : System.Drawing.Color.Firebrick;
            if (!ok) return;

            ClearDirty();
            // 写盘后只回读台达配置，不动工艺/通讯
            // 焊接头已由 Generate 落盘，此处不重读
            ReloadDeltaConfigOnly();
            LoadProjectXmlRows();
            BindDeltaView(segDeltaView.SelectIndex);
        }

        /// <summary>重载保存后回读：只重载台达配置</summary>
        /// <remarks>含轴/IO/气缸与焊接头，不触碰工艺/通讯</remarks>
        private void ReloadDeltaConfigOnly()
        {
            try
            {
                // 台达运控 XML 从磁盘重读（静默：结论由下方一条输出）
                MontionManager.Instance.MoveData.MotionConfig.LoadConfig(false);
                // 焊接头配置（WeldHeadSlave.xml / IO_MAP）已由 Generate 在内存同步并落盘，不重复读
                GlobalCommData.ShowLog(LogTag, "已重载台达配置（轴/IO/气缸）与焊接头从站配置");
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(LogTag, "台达配置重载失败 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>表格编辑写回行对象</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">编辑事件（含行对象、行号、列号、新值）</param>
        /// <returns>是否成功。</returns>
        private bool TableDeltaView_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            if (e == null || e.Record == null) return true;
            string cellText = e.Value == null ? string.Empty : e.Value.Trim();
            if (!SetRowCellValue(e.Record, e.ColumnIndex, cellText)) return true;
            MarkCurrentViewDirty(segDeltaView.SelectIndex);
            UpdateDeltaStatusHint(segDeltaView.SelectIndex);
            lblDeltaStatus.Text = lblDeltaStatus.Text + "；表格已修改（未保存），点「重载保存」生效";
            return true;
        }

        /// <summary>记录右键命中行，供删除/新增定位</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">点击事件</param>
        private void TableDeltaView_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            _menuRowIndex = e == null ? -1 : e.RowIndex;
            if (_menuRowIndex >= 0 && tableDeltaView != null) tableDeltaView.SelectedIndex = _menuRowIndex;
        }

        /// <summary>初始化表格右键菜单（不新增控件、不改 Designer 版式）</summary>
        private void InitDeltaTableMenu()
        {
            _tableMenu = new System.Windows.Forms.ContextMenuStrip();
            _tableMenu.Items.Add("新增行", null, delegate { AddDeltaRow(); });
            _tableMenu.Items.Add("删除当前行", null, delegate { RemoveDeltaRow(); });
            _tableMenu.Items.Add("导出当前视图 CSV（反向）", null, delegate { ExportCurrentViewCsv(); });
            _tableMenu.Items.Add("从目录重新识别", null, delegate { RescanFromDirectory(); });
            tableDeltaView.ContextMenuStrip = _tableMenu;
        }

        /// <summary>新增空行（四视图均支持）</summary>
        private void AddDeltaRow()
        {
            int index = segDeltaView.SelectIndex;
            if (index == DeltaViewIo)
            {
                IoConfigPreviewRow row = new IoConfigPreviewRow
                {
                    IoType = "输入",
                    IoName = string.Empty,
                    NodeId = "0",
                    SlotNo = "0",
                    BitNo = "0",
                    Enabled = "FALSE",
                    Remark = string.Empty
                };
                _ioRows.Add(row);
            }
            else if (index == DeltaViewCylinder)
            {
                CylinderConfigPreviewRow row = new CylinderConfigPreviewRow
                {
                    CylinderName = string.Empty,
                    StretchSensor = string.Empty,
                    RetractSensor = string.Empty,
                    CenterSensor = string.Empty,
                    StretchOutput = string.Empty,
                    RetractOutput = string.Empty,
                    StretchActionName = string.Empty,
                    RetractActionName = string.Empty
                };
                _cylinderRows.Add(row);
            }
            else if (index == DeltaViewWeldHead)
            {
                WeldHeadPdoEntry row = new WeldHeadPdoEntry
                {
                    Seq = (_weldHeadRows.Count + 1).ToString(),
                    VarName = string.Empty,
                    DisplayName = string.Empty,
                    DataType = string.Empty,
                    SdoIndex = string.Empty,
                    SdoSubIndex = string.Empty,
                    Access = string.Empty,
                    PdoIndex = string.Empty,
                    PdoSubIndex = string.Empty,
                    Description = string.Empty
                };
                _weldHeadRows.Add(row);
            }
            else
            {
                AxisConfigPreviewRow row = new AxisConfigPreviewRow
                {
                    AxisName = string.Empty,
                    NodeId = "0",
                    SlotNo = "0",
                    Enabled = "FALSE",
                    Equiv = "0.001",
                    StrVel = "0",
                    ConstVel = "0",
                    AccTime = "0",
                    DecTime = "0",
                    STime = "0",
                    EndVel = "0",
                    HomeMode = "0",
                    HomeOffset = "0",
                    HomeFirstVel = "0",
                    HomeSecondVel = "0",
                    HomeAcc = "0",
                    PositiveLimit = "0",
                    NegativeLimit = "0",
                    SoftLimitMode = "0"
                };
                _axisRows.Add(row);
            }

            MarkCurrentViewDirty(segDeltaView.SelectIndex);
            BindDeltaView(segDeltaView.SelectIndex);
            lblDeltaStatus.Text = lblDeltaStatus.Text + "；已新增 1 行（未保存）";
        }

        /// <summary>删除右键命中的行（四视图均支持）</summary>
        private void RemoveDeltaRow()
        {
            int index = segDeltaView.SelectIndex;
            int rowIndex = _menuRowIndex;
            if (rowIndex < 0 && tableDeltaView != null) rowIndex = tableDeltaView.SelectedIndex;
            if (rowIndex < 0) return;

            if (index == DeltaViewIo)
            {
                if (rowIndex >= _ioRows.Count) return;
                _ioRows.RemoveAt(rowIndex);
            }
            else if (index == DeltaViewCylinder)
            {
                if (rowIndex >= _cylinderRows.Count) return;
                _cylinderRows.RemoveAt(rowIndex);
            }
            else if (index == DeltaViewWeldHead)
            {
                if (rowIndex >= _weldHeadRows.Count) return;
                _weldHeadRows.RemoveAt(rowIndex);
            }
            else
            {
                if (rowIndex >= _axisRows.Count) return;
                _axisRows.RemoveAt(rowIndex);
            }

            _menuRowIndex = -1;
            MarkCurrentViewDirty(index);
            BindDeltaView(index);
            lblDeltaStatus.Text = lblDeltaStatus.Text + "；已删除 1 行（未保存）";
        }

        /// <summary>从当前目录重新识别（含覆盖确认）</summary>
        private void RescanFromDirectory()
        {
            string dir = ReadDirInput(txtCsvDir);
            if (dir.Length == 0)
            {
                GlobalCommData.ShowLog(LogTag, "请先在目录栏选择配置文档目录", MessageLevel.Warning);
                return;
            }
            ScanDeltaDirectory(dir);
        }

        /// <summary>反向导出</summary>
        /// <remarks>把当前视图表格按项目 CSV 表头写到所选目录</remarks>
        private void ExportCurrentViewCsv()
        {
            int index = segDeltaView.SelectIndex;
            string fileName = index == DeltaViewIo ? DeltaControllerConfigImporter.IOFileName
                : index == DeltaViewCylinder ? DeltaControllerConfigImporter.CylinderFileName
                : index == DeltaViewWeldHead ? "焊接头PDO映射.csv"
                : DeltaControllerConfigImporter.AxisFileName;

            AntdUI.FolderBrowserDialog dialog = new AntdUI.FolderBrowserDialog
            {
                DirectoryPath = ReadDirInput(txtCsvDir)
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                string path = Path.Combine(dialog.DirectoryPath, fileName);
                List<string[]> rows = BuildCurrentViewRows(index);
                WriteCsvTable(path, ColumnHeaders(index), rows);
                GlobalCommData.ShowLog(LogTag, "已导出当前表格 CSV：" + path);
                lblDeltaStatus.Text = "已导出当前表格 CSV：" + path;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(LogTag, "导出 CSV 失败 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>页面级校验</summary>
        /// <remarks>脏视图强校验；未编辑视图只给提示，不得拦保存</remarks>
        /// <param name="hints">非阻断提示清单</param>
        /// <returns>阻断错误清单；空表示可写盘</returns>
        private List<string> ValidateCurrentTables(out List<string> hints)
        {
            hints = new List<string>();
            List<string> errors = new List<string>();

            List<string> axisErrors = new List<string>();
            ValidateAxisRows(axisErrors);
            MergeView(axisErrors, DeltaViewAxis, errors, hints);

            List<string> ioErrors = new List<string>();
            ValidateIoRows(ioErrors);
            MergeView(ioErrors, DeltaViewIo, errors, hints);

            List<string> cylinderErrors = new List<string>();
            ValidateCylinderRows(cylinderErrors);
            MergeView(cylinderErrors, DeltaViewCylinder, errors, hints);

            List<string> weldErrors = new List<string>();
            List<string> weldHints = new List<string>();
            ValidateWeldHeadRows(weldErrors, weldHints);
            MergeView(weldErrors, DeltaViewWeldHead, errors, hints);
            // 变量名重复与未填索引只提示，不拦截
            hints.AddRange(weldHints);
            return errors;
        }

        /// <summary>按视图脏标记分流</summary>
        /// <remarks>脏视图的问题阻断；未编辑视图同一批问题降为提示</remarks>
        /// <param name="viewErrors">该视图校验出的问题</param>
        /// <param name="viewIndex">视图序号</param>
        /// <param name="errors">阻断错误清单</param>
        /// <param name="hints">非阻断提示清单</param>
        private void MergeView(List<string> viewErrors, int viewIndex, List<string> errors, List<string> hints)
        {
            if (_viewDirty[viewIndex]) errors.AddRange(viewErrors);
            else hints.AddRange(viewErrors);
        }

        /// <summary>校验提示后缀（最多列 3 条，避免状态栏过长）</summary>
        /// <param name="hints">非阻断提示清单</param>
        /// <returns>「；提示：…」后缀；无提示返回空串</returns>
        private static string BuildHintSuffix(List<string> hints)
        {
            if (hints == null || hints.Count == 0) return string.Empty;
            int show = hints.Count > 3 ? 3 : hints.Count;
            string text = string.Join("；", hints.GetRange(0, show).ToArray());
            return "；提示：" + text + (hints.Count > show ? "…共 " + hints.Count + " 条" : string.Empty);
        }

        /// <summary>标记当前视图有手工改动</summary>
        /// <remarks>总脏标记 + 分视图脏标记</remarks>
        /// <param name="viewIndex">视图序号（0=轴 1=IO 2=气缸 3=焊接头）</param>
        private void MarkCurrentViewDirty(int viewIndex)
        {
            _tableEdits = true;
            if (viewIndex >= 0 && viewIndex < _viewDirty.Length) _viewDirty[viewIndex] = true;
        }

        /// <summary>清除全部脏标记（写盘完成 / 重新识别 / 包导入后）</summary>
        private void ClearDirty()
        {
            _tableEdits = false;
            for (int i = 0; i < _viewDirty.Length; i++) _viewDirty[i] = false;
        }

        /// <summary>校验轴表</summary>
        /// <param name="errors">错误清单</param>
        private void ValidateAxisRows(List<string> errors)
        {
            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < _axisRows.Count; i++)
            {
                AxisConfigPreviewRow row = _axisRows[i];
                string name = (row.AxisName ?? string.Empty).Trim();
                if (name.Length == 0) { errors.Add(Where(i, 0, "轴名称") + "不能为空"); continue; }
                if (!names.Add(name)) errors.Add(Where(i, 0, "轴名称") + "名称重复 " + name);
                CheckUShort(row.NodeId, i, 1, "节点号", 0, 255, errors);
                CheckUShort(row.SlotNo, i, 2, "槽位", 0, 255, errors);
                CheckBool(row.Enabled, i, 3, "启用", errors);
                CheckDouble(row.Equiv, i, 4, "当量", errors);
                CheckDouble(row.StrVel, i, 5, "启动速度", errors);
                CheckDouble(row.ConstVel, i, 6, "恒速", errors);
                CheckDouble(row.AccTime, i, 7, "加速时间", errors);
                CheckDouble(row.DecTime, i, 8, "减速时间", errors);
                CheckDouble(row.STime, i, 9, "S 时间", errors);
                CheckDouble(row.EndVel, i, 10, "终点速度", errors);
                CheckUShort(row.HomeMode, i, 11, "回零模式", -4, 35, errors);
                CheckDouble(row.HomeOffset, i, 12, "回零偏移", errors);
                CheckDouble(row.HomeFirstVel, i, 13, "一次回零速度", errors);
                CheckDouble(row.HomeSecondVel, i, 14, "二次回零速度", errors);
                CheckDouble(row.HomeAcc, i, 15, "回零加速度", errors);
                CheckDouble(row.PositiveLimit, i, 16, "正限位", errors);
                CheckDouble(row.NegativeLimit, i, 17, "负限位", errors);
                CheckUShort(row.SoftLimitMode, i, 18, "软限位模式", 0, 255, errors);

                double constVel = ParseDouble(row.ConstVel, 0);
                double strVel = ParseDouble(row.StrVel, 0);
                if (constVel > 0 && strVel > constVel) errors.Add(Where(i, 5, "启动速度") + "启动速度大于恒速");
            }
        }

        /// <summary>校验 IO 表（IO_NAME 唯一、BIT_NO 范围、类型取值）</summary>
        /// <param name="errors">错误清单</param>
        private void ValidateIoRows(List<string> errors)
        {
            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < _ioRows.Count; i++)
            {
                IoConfigPreviewRow row = _ioRows[i];
                string name = (row.IoName ?? string.Empty).Trim();
                if (name.Length == 0) { errors.Add(Where(i, 1, "名称") + "不能为空"); continue; }
                if (!names.Add(name)) errors.Add(Where(i, 1, "名称") + "名称重复 " + name);

                string type = (row.IoType ?? string.Empty).Trim();
                if (!IsInputType(type) && !IsOutputType(type))
                    errors.Add(Where(i, 0, "类型") + "必须为 输入/输出（INPUT/OUTPUT），当前 " + type);

                CheckUShort(row.NodeId, i, 2, "节点号", 0, 255, errors);
                CheckUShort(row.SlotNo, i, 3, "槽位", 0, 255, errors);
                CheckUShort(row.BitNo, i, 4, "位号", 0, 15, errors);
                CheckBool(row.Enabled, i, 5, "启用", errors);
            }
        }

        /// <summary>校验气缸表（IO_NAME 引用完整性）</summary>
        /// <param name="errors">错误清单</param>
        private void ValidateCylinderRows(List<string> errors)
        {
            HashSet<string> inputNames = new HashSet<string>();
            HashSet<string> outputNames = new HashSet<string>();
            for (int i = 0; i < _ioRows.Count; i++)
            {
                IoConfigPreviewRow io = _ioRows[i];
                string name = (io.IoName ?? string.Empty).Trim();
                if (name.Length == 0) continue;
                if (IsInputType(io.IoType)) inputNames.Add(name); else outputNames.Add(name);
            }

            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < _cylinderRows.Count; i++)
            {
                CylinderConfigPreviewRow row = _cylinderRows[i];
                string name = (row.CylinderName ?? string.Empty).Trim();
                if (name.Length == 0) { errors.Add(Where(i, 0, "气缸名称") + "不能为空"); continue; }
                if (!names.Add(name)) errors.Add(Where(i, 0, "气缸名称") + "名称重复 " + name);

                CheckIoReference(row.StretchSensor, inputNames, i, 1, "伸出传感器", errors);
                CheckIoReference(row.RetractSensor, inputNames, i, 2, "缩回传感器", errors);
                CheckIoReference(row.CenterSensor, inputNames, i, 3, "中间传感器", errors);
                CheckIoReference(row.StretchOutput, outputNames, i, 4, "伸出输出", errors);
                CheckIoReference(row.RetractOutput, outputNames, i, 5, "缩回输出", errors);
            }
        }

        /// <summary>校验焊接头 PDO 表</summary>
        /// <param name="errors">阻断错误清单</param>
        /// <param name="hints">非阻断提示清单</param>
        /// <remarks>同一变量在 Rx 与 Tx 成对出现是 ESI/IO 表正常结构。
        /// 唯一性按 <c>BuildPdoObjectKey</c> 判，PDO 索引子索引优先。
        /// 仅 SDO 行用 SDO 复合键。
        /// ⛔ 不得仅按变量名判，防历史误报。
        /// 复合键唯一，变量名重复只提示。</remarks>
        private void ValidateWeldHeadRows(List<string> errors, List<string> hints)
        {
            HashSet<string> keys = new HashSet<string>();
            Dictionary<string, int> nameCounts = new Dictionary<string, int>();
            for (int i = 0; i < _weldHeadRows.Count; i++)
            {
                WeldHeadPdoEntry row = _weldHeadRows[i];
                string name = (row.VarName ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    errors.Add(Where(i, 1, "变量") + "不能为空");
                }
                else
                {
                    nameCounts[name] = nameCounts.ContainsKey(name) ? nameCounts[name] + 1 : 1;
                }

                CheckHex(row.SdoIndex, i, 4, "SDO Index", errors);
                CheckHex(row.SdoSubIndex, i, 5, "SDO SubIndex", errors);
                CheckHex(row.PdoIndex, i, 7, "PDO Index", errors);
                CheckHex(row.PdoSubIndex, i, 8, "PDO SubIndex", errors);

                string key = DeltaControllerConfigImporter.BuildPdoObjectKey(row);
                if (key.Length == 0)
                {
                    hints.Add(Where(i, 1, "变量") + "未填 PDO/SDO 对象索引（按行号视为唯一）");
                    key = "ROW " + i;
                }
                if (!keys.Add(key)) errors.Add(Where(i, 1, "变量") + "对象索引重复 " + key + "（变量 " + name + "）");
            }

            int repeatedNameGroups = 0;
            foreach (KeyValuePair<string, int> pair in nameCounts)
            {
                if (pair.Value > 1) repeatedNameGroups++;
            }
            if (repeatedNameGroups > 0)
            {
                hints.Add("变量名重复 " + repeatedNameGroups + " 组（Rx/Tx 成对出现属正常结构，非错误）");
            }
        }

        /// <summary>构造“第 N 行第 M 列（列名）”定位文本</summary>
        /// <param name="rowIndex">零基行号</param>
        /// <param name="columnIndex">零基列号</param>
        /// <param name="columnName">列名</param>
        /// <returns>定位文本</returns>
        private static string Where(int rowIndex, int columnIndex, string columnName)
        {
            return "第 " + (rowIndex + 1) + " 行第 " + (columnIndex + 1) + " 列（" + columnName + "）：";
        }

        /// <summary>校验无符号整数范围</summary>
        /// <param name="text">文本内容。</param>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        private static void CheckUShort(string text, int rowIndex, int columnIndex, string columnName,
            int min, int max, List<string> errors)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!int.TryParse(value, out int parsed) || parsed < min || parsed > max)
                errors.Add(Where(rowIndex, columnIndex, columnName) + "必须为 " + min + "~" + max + " 的整数，当前 " + value);
        }

        /// <summary>校验布尔文本（TRUE/FALSE）</summary>
        /// <param name="text">文本内容。</param>
        private static void CheckBool(string text, int rowIndex, int columnIndex, string columnName, List<string> errors)
        {
            string value = (text ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length == 0) return;
            if (value != "TRUE" && value != "FALSE")
                errors.Add(Where(rowIndex, columnIndex, columnName) + "必须为 TRUE/FALSE，当前 " + text);
        }

        /// <summary>校验双精度</summary>
        /// <param name="text">文本内容。</param>
        private static void CheckDouble(string text, int rowIndex, int columnIndex, string columnName, List<string> errors)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                errors.Add(Where(rowIndex, columnIndex, columnName) + "数值非法 " + value);
        }

        /// <summary>校验十六进制对象索引</summary>
        /// <param name="text">文本内容。</param>
        /// <remarks>0x 前缀或纯十六进制，空值跳过</remarks>
        private static void CheckHex(string text, int rowIndex, int columnIndex, string columnName, List<string> errors)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!TryParseHex(value, out _))
                errors.Add(Where(rowIndex, columnIndex, columnName) + "必须为十六进制地址，当前 " + value);
        }

        /// <summary>校验 IO 引用是否存在于 IO 表（空值按未接按通过）</summary>
        private static void CheckIoReference(string reference, HashSet<string> candidates, int rowIndex,
            int columnIndex, string columnName, List<string> errors)
        {
            string value = (reference ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!candidates.Contains(value))
                errors.Add(Where(rowIndex, columnIndex, columnName) + "引用的 IO 不存在 " + value);
        }

        /// <summary>解析双精度（失败取默认值）</summary>
        /// <param name="text">文本内容。</param>
        /// <returns>计算结果。</returns>
        private static double ParseDouble(string text, double defaultValue)
        {
            if (double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return defaultValue;
        }

        /// <summary>解析十六进制（支持 0x 前缀）</summary>
        /// <param name="text">文本内容。</param>
        /// <param name="value">值。</param>
        /// <returns>是否成功。</returns>
        private static bool TryParseHex(string text, out uint value)
        {
            value = 0;
            string body = (text ?? string.Empty).Trim();
            if (body.Length == 0) return false;
            if (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || body.StartsWith("#x", StringComparison.OrdinalIgnoreCase))
                body = body.Substring(2);
            return uint.TryParse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>判断是否输入类型（兼容中文/英文写法）</summary>
        /// <param name="text">文本内容。</param>
        /// <returns>是否成功。</returns>
        private static bool IsInputType(string text)
        {
            string value = (text ?? string.Empty).Trim().ToUpperInvariant();
            return value == "INPUT" || value == "输入";
        }

        /// <summary>判断是否输出类型（兼容中文/英文写法）</summary>
        /// <param name="text">文本内容。</param>
        /// <returns>是否成功。</returns>
        private static bool IsOutputType(string text)
        {
            string value = (text ?? string.Empty).Trim().ToUpperInvariant();
            return value == "OUTPUT" || value == "输出";
        }

        /// <summary>导出当前表格到临时目录（表格 = 唯一提交源）</summary>
        /// <param name="error">失败说明</param>
        /// <returns>成功返回临时目录；失败返回空串</returns>
        private string WriteTableSourceDirectory(out string error)
        {
            error = string.Empty;
            // 提交源不能落在 Config_import，会被清空
            // 改写到产物根 staging，跨整站导入可复现
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConfigTableStaging",
                "edit_" + DateTime.Now.ToString(StampFormat));
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                WriteOrClearCsv(dir, DeltaControllerConfigImporter.AxisFileName, AxisHeaders(),
                    _axisRows.Count > 0 ? BuildAxisCsvRows() : null);
                WriteOrClearCsv(dir, DeltaControllerConfigImporter.IOFileName, IoHeaders(),
                    _ioRows.Count > 0 ? BuildIoCsvRows() : null);
                WriteOrClearCsv(dir, DeltaControllerConfigImporter.CylinderFileName, CylinderHeaders(),
                    _cylinderRows.Count > 0 ? BuildCylinderCsvRows() : null);

                // ESI 仅承载从站身份；PDO 映射来自表格，xlsx 不复制（避免被旧表覆盖）
                ConfigDirectoryScan scan = _lastScan;
                if (scan != null && scan.HasWeldHeadEsi && File.Exists(scan.EsiPath))
                {
                    File.Copy(scan.EsiPath, Path.Combine(dir, Path.GetFileName(scan.EsiPath)), true);
                }
                return dir;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return string.Empty;
            }
        }

        /// <summary>有行则写 CSV，无行则删除同名残留</summary>
        /// <remarks>防止空视图被旧文件当作有效节导入</remarks>
        private static void WriteOrClearCsv(string dir, string fileName, string[] headers, List<string[]> rows)
        {
            string path = Path.Combine(dir, fileName);
            if (rows != null && rows.Count > 0)
            {
                WriteCsvTable(path, headers, rows);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>轴 CSV 表头（TORQUE_* 保留既有值，避免表格提交时被清零）</summary>
        private static string[] AxisHeaders()
        {
            return new string[] { "AXIS_NAME", "NODE_ID", "SLOT_NO", "ENABLED", "EQUIV", "STR_VEL", "CONST_VEL",
                "ACC_TIME", "DEC_TIME", "S_TIME", "END_VEL", "HOME_MODE", "HOME_OFFSET", "HOME_FIRST_VEL",
                "HOME_SECOND_VEL", "HOME_ACC", "POSITIVE_LIMIT", "NEGATIVE_LIMIT", "SOFT_LIMIT_MODE",
                "TORQUE_MODE", "TORQUE_TARGET", "TORQUE_SLOPE" };
        }

        /// <summary>轴 CSV 数据行（界面上未展示的力矩列按既有 XML 回填）</summary>
        /// <returns>结果集合。</returns>
        private List<string[]> BuildAxisCsvRows()
        {
            // 力矩列不进预览表，按既有 XML 回填
            DeltaMotionConfig existing = MontionManager.Instance.CreatePreviewMotionConfig();
            Dictionary<string, DeltaAxisConfig> torque = new Dictionary<string, DeltaAxisConfig>();
            for (int i = 0; i < existing.Axes.Count; i++)
            {
                torque[existing.Axes[i].AxisName] = existing.Axes[i];
            }

            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < _axisRows.Count; i++)
            {
                AxisConfigPreviewRow row = _axisRows[i];
                string name = (row.AxisName ?? string.Empty).Trim();
                DeltaAxisConfig old = torque.ContainsKey(name) ? torque[name] : null;
                rows.Add(new string[]
                {
                    name, row.NodeId, row.SlotNo, row.Enabled, row.Equiv, row.StrVel, row.ConstVel,
                    row.AccTime, row.DecTime, row.STime, row.EndVel, row.HomeMode, row.HomeOffset,
                    row.HomeFirstVel, row.HomeSecondVel, row.HomeAcc, row.PositiveLimit, row.NegativeLimit,
                    row.SoftLimitMode,
                    old == null ? "FALSE" : (old.TorqueModeEnabled ? "TRUE" : "FALSE"),
                    old == null ? "0" : old.TorqueTarget.ToString(),
                    old == null ? "0" : old.TorqueSlope.ToString()
                });
            }
            return rows;
        }

        /// <summary>IO CSV 表头</summary>
        private static string[] IoHeaders()
        {
            return new string[] { "IO_TYPE", "IO_NAME", "NODE_ID", "SLOT_NO", "BIT_NO", "ENABLED", "REMARK" };
        }

        /// <summary>IO CSV 数据行（中文类型转换为 INPUT/OUTPUT）</summary>
        /// <returns>结果集合。</returns>
        private List<string[]> BuildIoCsvRows()
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < _ioRows.Count; i++)
            {
                IoConfigPreviewRow row = _ioRows[i];
                string type = IsInputType(row.IoType) ? "INPUT" : "OUTPUT";
                rows.Add(new string[] { type, row.IoName, row.NodeId, row.SlotNo, row.BitNo, row.Enabled, row.Remark });
            }
            return rows;
        }

        /// <summary>气缸 CSV 表头</summary>
        private static string[] CylinderHeaders()
        {
            return new string[] { "CYLINDER_NAME", "STRETCH_SENSOR", "RETRACT_SENSOR", "CENTER_SENSOR",
                "STRETCH_OUTPUT", "RETRACT_OUTPUT", "STRETCH_ACTION_NAME", "RETRACT_ACTION_NAME" };
        }

        /// <summary>气缸 CSV 数据行</summary>
        /// <returns>结果集合。</returns>
        private List<string[]> BuildCylinderCsvRows()
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < _cylinderRows.Count; i++)
            {
                CylinderConfigPreviewRow row = _cylinderRows[i];
                rows.Add(new string[] { row.CylinderName, row.StretchSensor, row.RetractSensor, row.CenterSensor,
                    row.StretchOutput, row.RetractOutput, row.StretchActionName, row.RetractActionName });
            }
            return rows;
        }

        /// <summary>按视图给出导出 CSV 表头</summary>
        /// <param name="index">索引。</param>
        private static string[] ColumnHeaders(int index)
        {
            if (index == DeltaViewIo) return IoHeaders();
            if (index == DeltaViewCylinder) return CylinderHeaders();
            if (index == DeltaViewWeldHead)
            {
                return new string[] { "Seq", "VarName", "DisplayName", "DataType", "SdoIndex", "SdoSubIndex",
                    "Access", "PdoIndex", "PdoSubIndex", "Description" };
            }
            return AxisHeaders();
        }

        /// <summary>按视图给出当前表格数据行（供反向导出）</summary>
        /// <param name="index">索引。</param>
        /// <returns>结果集合。</returns>
        private List<string[]> BuildCurrentViewRows(int index)
        {
            if (index == DeltaViewIo) return BuildIoCsvRows();
            if (index == DeltaViewCylinder) return BuildCylinderCsvRows();
            if (index == DeltaViewWeldHead)
            {
                List<string[]> rows = new List<string[]>();
                for (int i = 0; i < _weldHeadRows.Count; i++)
                {
                    WeldHeadPdoEntry row = _weldHeadRows[i];
                    rows.Add(new string[] { row.Seq, row.VarName, row.DisplayName, row.DataType, row.SdoIndex,
                        row.SdoSubIndex, row.Access, row.PdoIndex, row.PdoSubIndex, row.Description });
                }
                return rows;
            }
            return BuildAxisCsvRows();
        }

        /// <summary>写 CSV（UTF-8 带 BOM，逗号/引号/换行按 RFC4180 转义）</summary>
        /// <param name="path">路径。</param>
        private static void WriteCsvTable(string path, string[] headers, List<string[]> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string[] cells = new string[row.Length];
                for (int c = 0; c < row.Length; c++) cells[c] = CsvEscape(row[c]);
                sb.AppendLine(string.Join(",", cells));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>CSV 单元格转义</summary>
        /// <param name="value">值。</param>
        /// <returns>结果文本。</returns>
        private static string CsvEscape(string value)
        {
            string text = value == null ? string.Empty : value;
            if (text.IndexOf(',') >= 0 || text.IndexOf('"') >= 0 || text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }

        /// <summary>按视图与列号把编辑值写回行对象</summary>
        /// <param name="record">行对象</param>
        /// <param name="columnIndex">列号</param>
        /// <param name="value">新值</param>
        /// <returns>写回成功返回 true</returns>
        private bool SetRowCellValue(object record, int columnIndex, string value)
        {
            int index = segDeltaView.SelectIndex;
            if (index == DeltaViewIo)
            {
                if (!(record is IoConfigPreviewRow row)) return false;
                if (columnIndex == 0) row.IoType = value;
                else if (columnIndex == 1) row.IoName = value;
                else if (columnIndex == 2) row.NodeId = value;
                else if (columnIndex == 3) row.SlotNo = value;
                else if (columnIndex == 4) row.BitNo = value;
                else if (columnIndex == 5) row.Enabled = value;
                else if (columnIndex == 6) row.Remark = value;
                else return false;
                return true;
            }
            if (index == DeltaViewCylinder)
            {
                if (!(record is CylinderConfigPreviewRow row)) return false;
                if (columnIndex == 0) row.CylinderName = value;
                else if (columnIndex == 1) row.StretchSensor = value;
                else if (columnIndex == 2) row.RetractSensor = value;
                else if (columnIndex == 3) row.CenterSensor = value;
                else if (columnIndex == 4) row.StretchOutput = value;
                else if (columnIndex == 5) row.RetractOutput = value;
                else if (columnIndex == 6) row.StretchActionName = value;
                else if (columnIndex == 7) row.RetractActionName = value;
                else return false;
                return true;
            }
            if (index == DeltaViewWeldHead)
            {
                if (!(record is WeldHeadPdoEntry row)) return false;
                if (columnIndex == 1) row.VarName = value;
                else if (columnIndex == 2) row.DisplayName = value;
                else if (columnIndex == 3) row.DataType = value;
                else if (columnIndex == 4) row.SdoIndex = value;
                else if (columnIndex == 5) row.SdoSubIndex = value;
                else if (columnIndex == 6) row.Access = value;
                else if (columnIndex == 7) row.PdoIndex = value;
                else if (columnIndex == 8) row.PdoSubIndex = value;
                else if (columnIndex == 9) row.Description = value;
                else return false;
                return true;
            }

            if (!(record is AxisConfigPreviewRow axis)) return false;
            if (columnIndex == 0) axis.AxisName = value;
            else if (columnIndex == 1) axis.NodeId = value;
            else if (columnIndex == 2) axis.SlotNo = value;
            else if (columnIndex == 3) axis.Enabled = value;
            else if (columnIndex == 4) axis.Equiv = value;
            else if (columnIndex == 5) axis.StrVel = value;
            else if (columnIndex == 6) axis.ConstVel = value;
            else if (columnIndex == 7) axis.AccTime = value;
            else if (columnIndex == 8) axis.DecTime = value;
            else if (columnIndex == 9) axis.STime = value;
            else if (columnIndex == 10) axis.EndVel = value;
            else if (columnIndex == 11) axis.HomeMode = value;
            else if (columnIndex == 12) axis.HomeOffset = value;
            else if (columnIndex == 13) axis.HomeFirstVel = value;
            else if (columnIndex == 14) axis.HomeSecondVel = value;
            else if (columnIndex == 15) axis.HomeAcc = value;
            else if (columnIndex == 16) axis.PositiveLimit = value;
            else if (columnIndex == 17) axis.NegativeLimit = value;
            else if (columnIndex == 18) axis.SoftLimitMode = value;
            else return false;
            return true;
        }

        /// <summary>写盘后回读校验</summary>
        /// <remarks>运控配置 + 焊接头配置 + 工艺算法 XML/INI 双后端 + 通讯配置</remarks>
        private void ReloadSystemConfig()
        {
            ReloadSystemConfig(true);
        }

        /// <summary>全量回读</summary>
        /// <remarks>仅系统设置包导入用，含工艺算法与通讯</remarks>
        /// <param name="reloadWeldHead">是否从磁盘重读焊接头配置（包导入传 true）</param>
        private void ReloadSystemConfig(bool reloadWeldHead)
        {
            try
            {
                // 静默读盘，结论由末尾一条统一输出
                MontionManager.Instance.MoveData.MotionConfig.LoadConfig(false);
                if (reloadWeldHead) MontionManager.Instance.LoadWeldHeadConfig();
                // LoadAlgorithmsConfig 同时加载 Algorithm_XML 与 Algorithm_INI，双后端一致
                WeldParamManager.Instance.LoadAlgorithmsConfig();
                CommunicationManager comm = GlobalCommData.mCommunicationManager;
                comm?.ReloadFromDisk();


                GlobalCommData.ShowLog(LogTag, "回读校验完成：运控配置 / "
                    + (reloadWeldHead ? "焊接头配置 / " : "焊接头配置（已由保存同步） / ")
                    + "工艺算法 XML+INI / 通讯配置");
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(LogTag, "回读校验失败 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>一键导出系统设置（.awcfg，仅系统设置范围，不加密）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnExportPackage_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "导出系统设置";
                dialog.Filter = "系统设置配置包 (*.awcfg)|*.awcfg";
                dialog.FileName = "SystemSettings_" + DateTime.Now.ToString(StampFormat) + SystemConfigPackager.Extension;
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                string message = string.Empty;
                bool ok;
                try
                {
                    ok = SystemConfigPackager.Export(dialog.FileName, out message);
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(LogTag, "导出异常 " + ex.Message, MessageLevel.Error);
                    return;
                }
                GlobalCommData.ShowLog(LogTag, (ok ? "导出成功 " : "导出失败 ") + message,
                    ok ? MessageLevel.Info : MessageLevel.Warning);
            }
        }

        /// <summary>一键导入系统设置（staging → 校验 → 备份 → 原子替换 → 失败回滚）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnImportPackage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "导入系统设置";
                dialog.Filter = "系统设置配置包 (*.awcfg)|*.awcfg|所有文件|*.*";
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                DialogResult confirm = AntdUI.Modal.open(new AntdUI.Modal.Config(FindForm(), "导入系统设置",
                    "将用配置包覆盖本机系统设置，当前表格中的手工修改也会被覆盖，写前自动备份到 Config\\_backup。是否继续？", TType.Warn));
                if (confirm != DialogResult.OK) return;

                string message = string.Empty;
                bool ok;
                try
                {
                    ok = SystemConfigPackager.Import(dialog.FileName, out message);
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(LogTag, "导入异常 " + ex.Message, MessageLevel.Error);
                    return;
                }

                GlobalCommData.ShowLog(LogTag, (ok ? "导入成功 " : "导入失败 ") + message,
                    ok ? MessageLevel.Info : MessageLevel.Warning);
                if (ok)
                {
                    ReloadSystemConfig();
                    // 导入后只回读一次表格（不再扫描目录）
                    _lastScan = null;
                    ClearDirty();
                    LoadProjectXmlRows();
                    BindDeltaView(segDeltaView.SelectIndex);
                }
            }
        }

        #endregion

        #region 目录输入

        /// <summary>读取目录输入框文本，空引用按空串处理</summary>
        /// <param name="input">目录输入框</param>
        /// <returns>去空白后的目录文本</returns>
        private static string ReadDirInput(AntdUI.Input input)
        {
            if (input == null || input.Text == null) return string.Empty;
            return input.Text.Trim();
        }

        #endregion

        #region 通用操作

        private void AppendLog(string message)
        {
            if (inputLog == null || !IsUiAccessible)
            {
                return;
            }
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                    return;
                }
                string line = string.Format("[{0}] {1}{2}",
                    AdaWeldSystem.FileOperate.Log.FormatTimestamp(DateTime.Now), message, Environment.NewLine);
                // 限制日志行数：超过 5000 字符时截断前半
                string newText = inputLog.Text + line;
                if (newText.Length > 5000)
                    newText = newText.Substring(newText.Length - 4000);
                inputLog.Text = newText;
                inputLog.SelectionStart = inputLog.Text.Length;
                if (inputLog.CanSelect)
                    inputLog.ScrollToCaret();
            }
            catch (ObjectDisposedException)
            {
                // 页面销毁与日志写入并发：丢弃本条
            }
            catch (InvalidOperationException)
            {
                // 句柄销毁瞬间无法封送：丢弃本条
            }
        }

        /// <summary>释放自定义资源，由父页面在关闭时调用。</summary>
        public void DisposeComponents()
        {
            GlobalCommData.CommunicationCommandReceived -= GlobalCommData_CommunicationCommandReceived;
            GlobalCommData.CommLogViewEvent -= GlobalCommData_CommLogViewEvent;
            _tableMenu?.Dispose();
            _tableMenu = null;
        }

        #endregion

        #region 配置预览行（只读表格数据源）

        /// <summary>轴配置表预览行（表头 AXIS_NAME 等逐列对应）</summary>
        public class AxisConfigPreviewRow
        {
            /// <summary>轴名称（AXIS_NAME）</summary>
            public string AxisName { get; set; }

            /// <summary>节点号（NODE_ID）</summary>
            public string NodeId { get; set; }

            /// <summary>槽位（SLOT_NO）</summary>
            public string SlotNo { get; set; }

            /// <summary>启用（ENABLED）</summary>
            public string Enabled { get; set; }

            /// <summary>当量（EQUIV）</summary>
            public string Equiv { get; set; }

            /// <summary>启动速度（STR_VEL）</summary>
            public string StrVel { get; set; }

            /// <summary>恒速（CONST_VEL）</summary>
            public string ConstVel { get; set; }

            /// <summary>加速时间（ACC_TIME）</summary>
            public string AccTime { get; set; }

            /// <summary>减速时间（DEC_TIME）</summary>
            public string DecTime { get; set; }

            /// <summary>S 时间（S_TIME）</summary>
            public string STime { get; set; }

            /// <summary>终点速度（END_VEL）</summary>
            public string EndVel { get; set; }

            /// <summary>回零模式（HOME_MODE）</summary>
            public string HomeMode { get; set; }

            /// <summary>回零偏移（HOME_OFFSET）</summary>
            public string HomeOffset { get; set; }

            /// <summary>一次回零速度（HOME_FIRST_VEL）</summary>
            public string HomeFirstVel { get; set; }

            /// <summary>二次回零速度（HOME_SECOND_VEL）</summary>
            public string HomeSecondVel { get; set; }

            /// <summary>回零加速度（HOME_ACC）</summary>
            public string HomeAcc { get; set; }

            /// <summary>正限位（POSITIVE_LIMIT）</summary>
            public string PositiveLimit { get; set; }

            /// <summary>负限位（NEGATIVE_LIMIT）</summary>
            public string NegativeLimit { get; set; }

            /// <summary>软限位模式（SOFT_LIMIT_MODE）</summary>
            public string SoftLimitMode { get; set; }
        }

        /// <summary>IO 配置表预览行（表头 IO_TYPE 等逐列对应）</summary>
        public class IoConfigPreviewRow
        {
            /// <summary>IO 类型（IO_TYPE）</summary>
            public string IoType { get; set; }

            /// <summary>IO 名称（IO_NAME）</summary>
            public string IoName { get; set; }

            /// <summary>节点号（NODE_ID）</summary>
            public string NodeId { get; set; }

            /// <summary>槽位（SLOT_NO）</summary>
            public string SlotNo { get; set; }

            /// <summary>位号（BIT_NO）</summary>
            public string BitNo { get; set; }

            /// <summary>启用（ENABLED）</summary>
            public string Enabled { get; set; }

            /// <summary>说明（REMARK）</summary>
            public string Remark { get; set; }
        }

        /// <summary>气缸配置表预览行（表头 CYLINDER_NAME 等逐列对应）</summary>
        public class CylinderConfigPreviewRow
        {
            /// <summary>气缸名称（CYLINDER_NAME）</summary>
            public string CylinderName { get; set; }

            /// <summary>伸出传感器（STRETCH_SENSOR）</summary>
            public string StretchSensor { get; set; }

            /// <summary>缩回传感器（RETRACT_SENSOR）</summary>
            public string RetractSensor { get; set; }

            /// <summary>中间传感器（CENTER_SENSOR）</summary>
            public string CenterSensor { get; set; }

            /// <summary>伸出输出（STRETCH_OUTPUT）</summary>
            public string StretchOutput { get; set; }

            /// <summary>缩回输出（RETRACT_OUTPUT）</summary>
            public string RetractOutput { get; set; }

            /// <summary>伸出动作名（STRETCH_ACTION_NAME）</summary>
            public string StretchActionName { get; set; }

            /// <summary>缩回动作名（RETRACT_ACTION_NAME）</summary>
            public string RetractActionName { get; set; }
        }

        #endregion
    }
}
