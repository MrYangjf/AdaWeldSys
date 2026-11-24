using AdaWeldSystem.Comm.Communication;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.UIDesign.SubDialog.CommConfigDialog;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.TabPage
{
    /// <summary>通讯设置页：RSI 通讯配置与调试。</summary>
    public partial class CommSettingPage : UserControl
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

        #endregion

        #region 页面生命周期

        /// <summary>界面可安全访问：句柄已创建且未销毁。</summary>
        private bool IsUiAccessible
        {
            get { return IsHandleCreated && !IsDisposed && !Disposing; }
        }

        public CommSettingPage()
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
            this.HandleCreated += CommSettingPage_HandleCreated;

            // 加载配置
            LoadConfig();

            // 显示通讯日志
            GlobalCommData.CommunicationCommandReceived += GlobalCommData_CommunicationCommandReceived;
            // 通讯日志仅本页显示，文件常记
            GlobalCommData.CommLogViewEvent += GlobalCommData_CommLogViewEvent;

            // 页面销毁时释放外部资源（UI 类用 DisposeComponents）
            this.HandleDestroyed += CommSettingPage_HandleDestroyed;
        }

        /// <summary>句柄创建后再启动 RSI 调试刷新计时器。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void CommSettingPage_HandleCreated(object sender, EventArgs e)
        {
            if (!_rsiDebugTimer.Enabled)
            {
                _rsiDebugTimer.Start();
            }
        }

        private void CommSettingPage_HandleDestroyed(object sender, EventArgs e)
        {
            this.HandleCreated -= CommSettingPage_HandleCreated;
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

        /// <summary>通讯过程日志查看</summary>
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
        private void SaveConfig()
        {
            // 保存机器人通讯启用开关（通道唯一为 RSI，无模式项）
            GlobalCommData.mCommunicationManager.IsRobotEnabled = chkEnableRobot.Checked;

            AppendLog("通讯配置已保存");
        }

        /// <summary>启用/禁用开关：写 Manager 启用配置并联动 panel 内控件可用性；</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
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
        }

        #endregion
    }
}
