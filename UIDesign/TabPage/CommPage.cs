using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.Robot.KUKARobot;
using AntdUI;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub2UI
{
    /// <summary>
    /// 合并通讯页面（三选一模式：PLC / 机器人 / 通用 TCP）。
    /// 顶部 Radio 三选一，下方 Tabs 显示对应配置面板（TabMenuVisible=false）。
    /// 布局：GridPanel 四行（Radio → Tabs → Checkbox → Input日志）。
    /// 每个通讯模式有独立的"启用"开关，启用后才能配置。
    /// 启用且正确配置后，启动时自动初始化连接。
    /// </summary>
    public partial class CommPage : UserControl
    {
        private KUKARobotManager _robotManager;
        private bool _isLogVisible = true;

        public CommPage()
        {
            InitializeComponent();
            _robotManager = new KUKARobotManager();

            // 加载配置
            LoadConfig();

            // 显示通讯日志
            _isLogVisible = chkShowCommLog.Checked;
            GlobalCommData.CommunicationCommandReceived += GlobalCommData_CommunicationCommandReceived; ;

            // 默认选中机器人通讯页
            radioRobot.Checked = true;
            tabsSettings.SelectedIndex = 0;

            // 页面销毁时释放外部资源（ADR-001：UI 类用 DisposeComponents，不在 .cs 重写 Dispose(bool)）
            this.HandleDestroyed += CommPage_HandleDestroyed;
        }

        private void CommPage_HandleDestroyed(object sender, EventArgs e)
        {
            DisposeComponents();
        }

        private void GlobalCommData_CommunicationCommandReceived(object sender, string e)
        {
            //throw new NotImplementedException();
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

        /// <summary>
        /// 加载所有通讯模式的配置
        /// </summary>
        private void LoadConfig()
        {
            // PLC 配置
            txtPlcIp.Text = GlobalCommData.mCommunicationManager.PlcXdoc.GetIp();
            txtPlcRack.Text = GlobalCommData.mCommunicationManager.PlcXdoc.GetElementValue("Rack", "0");
            txtPlcSlot.Text = GlobalCommData.mCommunicationManager.PlcXdoc.GetElementValue("Slot", "1");
            chkEnablePLC.Checked = GlobalCommData.mCommunicationManager.IsPlcEnabled;

            // 机器人配置
            txtRobotIp.Text = GlobalCommData.mCommunicationManager.RobotServerXdoc.GetIp();
            txtRobotPort.Text = GlobalCommData.mCommunicationManager.RobotServerXdoc.GetPort().ToString();
            chkEnableRobot.Checked = GlobalCommData.mCommunicationManager.IsRobotEnabled;
            // 机器人通讯模式（RSI/EKI 二选一）
            var mode = GlobalCommData.mCommunicationManager.RobotCommMode;
            if (mode == CommunicationManager.RobotCommModeType.RSI)
                radioModeRSI.Checked = true;
            else
                radioModeEKI.Checked = true;

            // 通用通讯配置
            txtServerIp.Text = GlobalCommData.mCommunicationManager.ServerXdoc.GetIp();
            txtServerPort.Text = GlobalCommData.mCommunicationManager.ServerXdoc.GetPort().ToString();
            txtClientIp.Text = GlobalCommData.mCommunicationManager.ClientXdoc.GetIp();
            txtClientPort.Text = GlobalCommData.mCommunicationManager.ClientXdoc.GetPort().ToString();
            chkEnableGeneral.Checked = GlobalCommData.mCommunicationManager.IsGeneralEnabled;
        }

        /// <summary>
        /// 保存所有通讯模式的配置
        /// </summary>
        private void SaveConfig()
        {
            // PLC 配置
            GlobalCommData.mCommunicationManager.PlcXdoc.SetElementValue("IP", txtPlcIp.Text);
            GlobalCommData.mCommunicationManager.PlcXdoc.SetElementValue("Rack", txtPlcRack.Text);
            GlobalCommData.mCommunicationManager.PlcXdoc.SetElementValue("Slot", txtPlcSlot.Text);
            GlobalCommData.mCommunicationManager.PlcXdoc.SaveXdocument();
            GlobalCommData.mCommunicationManager.IsPlcEnabled = chkEnablePLC.Checked;

            // 机器人配置
            GlobalCommData.mCommunicationManager.RobotServerXdoc.SetElementValue("IP", txtRobotIp.Text);
            int port;
            if (int.TryParse(txtRobotPort.Text, out port))
            {
                GlobalCommData.mCommunicationManager.RobotServerXdoc.SetElementValue("PORT", port.ToString());
            }
            GlobalCommData.mCommunicationManager.RobotServerXdoc.SaveXdocument();
            GlobalCommData.mCommunicationManager.IsRobotEnabled = chkEnableRobot.Checked;
            // 保存机器人通讯模式（RSI/EKI 二选一）
            GlobalCommData.mCommunicationManager.RobotCommMode = radioModeRSI.Checked
                ? CommunicationManager.RobotCommModeType.RSI
                : CommunicationManager.RobotCommModeType.EKI;

            // 通用通讯配置
            GlobalCommData.mCommunicationManager.ServerXdoc.SetElementValue("IP", txtServerIp.Text);
            GlobalCommData.mCommunicationManager.ServerXdoc.SetElementValue("PORT", txtServerPort.Text);
            GlobalCommData.mCommunicationManager.ServerXdoc.SaveXdocument();
            GlobalCommData.mCommunicationManager.ClientXdoc.SetElementValue("IP", txtClientIp.Text);
            GlobalCommData.mCommunicationManager.ClientXdoc.SetElementValue("PORT", txtClientPort.Text);
            GlobalCommData.mCommunicationManager.ClientXdoc.SaveXdocument();
            GlobalCommData.mCommunicationManager.IsGeneralEnabled = chkEnableGeneral.Checked;

            AppendLog("通讯配置已保存");
        }

        /// <summary>
        /// Radio 按钮切换：同步 Tabs 选中页
        /// </summary>
        private void OnRadioModeChanged(object sender, AntdUI.BoolEventArgs e)
        {
            AntdUI.Radio radio = sender as AntdUI.Radio;
            if (radio == null || !radio.Checked) return;

            if (radio == radioRobot)
                tabsSettings.SelectedIndex = 0;  // tabRobot
            else if (radio == radioGeneral)
                tabsSettings.SelectedIndex = 1;  // tabGeneral
            else if (radio == radioPLC)
                tabsSettings.SelectedIndex = 2;  // tabPLC
        }

        /// <summary>
        /// Tabs 选中切换：同步 Radio 按钮（防止双向回环）
        /// </summary>
        private void OnTabsSelectedChanged(object sender, AntdUI.IntEventArgs e)
        {
            int idx = tabsSettings.SelectedIndex;
            if (idx == 0 && !radioRobot.Checked)
                radioRobot.Checked = true;
            else if (idx == 1 && !radioGeneral.Checked)
                radioGeneral.Checked = true;
            else if (idx == 2 && !radioPLC.Checked)
                radioPLC.Checked = true;
        }

        /// <summary>
        /// 启用/禁用开关
        /// </summary>
        private void chkEnable_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            // 启用状态变化时，控制对应模式的配置控件可用性
            if (sender == chkEnablePLC)
            {
                SetPanelEnabled(panelPLC, chkEnablePLC.Checked, chkEnablePLC);
            }
            else if (sender == chkEnableRobot)
            {
                SetPanelEnabled(panelRobot, chkEnableRobot.Checked, chkEnableRobot);
            }
            else if (sender == chkEnableGeneral)
            {
                SetPanelEnabled(panelGeneral, chkEnableGeneral.Checked, chkEnableGeneral);
            }
        }

        /// <summary>
        /// 设置面板内控件的启用状态（保留 checkbox 本身可用）
        /// </summary>
        private void SetPanelEnabled(System.Windows.Forms.Panel panel, bool enabled, AntdUI.Checkbox excludeCheckbox)
        {
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl == excludeCheckbox) continue;
                ctrl.Enabled = enabled;
            }
        }

        // ================================================================
        // PLC 通讯操作
        // ================================================================

        private void btnPlcConnect_Click(object sender, EventArgs e)
        {
            AppendLog($"正在连接 PLC {txtPlcIp.Text} ...");
            // TODO: Phase E 接入 S7NetPlus / Profinet 驱动
        }

        private void btnPlcDisconnect_Click(object sender, EventArgs e)
        {
            AppendLog("PLC 连接已断开");
            // TODO: Phase E 断开 PLC 连接
        }

        private void btnPlcGsdGenerate_Click(object sender, EventArgs e)
        {
            AppendLog("生成 GSDML 配置文件（Profinet 专用硬件）");
            // TODO: Phase E 接入 GSDML 生成器
        }

        // ================================================================
        // 机器人通讯操作
        // ================================================================

        private void btnRsiConnect_Click(object sender, EventArgs e)
        {
            try
            {
                AppendLog("正在初始化 RSI ...");
                RSIConfig config = new RSIConfig
                {
                    IPNumber = txtRobotIp.Text,
                    Port = int.Parse(txtRobotPort.Text)
                };
                bool ok = _robotManager.InitializeRSI(config) && _robotManager.StartRSI();
                AppendLog(ok ? "RSI 启动成功" : "RSI 启动失败");
            }
            catch (Exception ex)
            {
                AppendLog("RSI 异常: " + ex.Message);
            }
        }

        private void btnEkiConnect_Click(object sender, EventArgs e)
        {
            try
            {
                AppendLog("正在连接 EKI ...");
                EKIConfig config = new EKIConfig
                {
                    ExternalIP = txtRobotIp.Text,
                    ExternalPort = int.Parse(txtRobotPort.Text)
                };
                bool ok = _robotManager.InitializeEKI(config) && _robotManager.OpenEKI();
                AppendLog(ok ? "EKI 连接成功" : "EKI 连接失败");
            }
            catch (Exception ex)
            {
                AppendLog("EKI 异常: " + ex.Message);
            }
        }

        private void btnRobotDisconnect_Click(object sender, EventArgs e)
        {
            _robotManager.StopRSI();
            _robotManager.CloseEKI();
            AppendLog("机器人通讯已断开");
        }

        // ================================================================
        // 通用通讯操作
        // ================================================================

        private void btnServerStart_Click(object sender, EventArgs e)
        {
            try
            {
                GlobalCommData.mCommunicationManager.TcpIpComm.Server.ServerIp = txtServerIp.Text;
                GlobalCommData.mCommunicationManager.TcpIpComm.Server.ServerPort = int.Parse(txtServerPort.Text);
                GlobalCommData.mCommunicationManager.TcpIpComm.StartServerCommTask();
                AppendLog("服务器启动监听");
            }
            catch (Exception ex)
            {
                AppendLog("服务器启动失败: " + ex.Message);
            }
        }

        private void btnServerStop_Click(object sender, EventArgs e)
        {
            GlobalCommData.mCommunicationManager.TcpIpComm.StopServerCommTask();
            AppendLog("服务器停止监听");
        }

        private void btnClientConnect_Click(object sender, EventArgs e)
        {
            try
            {
                GlobalCommData.mCommunicationManager.TcpIpComm.Client.TargetIp = txtClientIp.Text;
                GlobalCommData.mCommunicationManager.TcpIpComm.Client.TargetPort = int.Parse(txtClientPort.Text);
                GlobalCommData.mCommunicationManager.TcpIpComm.StartClientCommTask();
                AppendLog("客户端连接服务器");
            }
            catch (Exception ex)
            {
                AppendLog("客户端连接失败: " + ex.Message);
            }
        }

        private void btnClientDisconnect_Click(object sender, EventArgs e)
        {
            GlobalCommData.mCommunicationManager.TcpIpComm.StopClientCommTask();
            AppendLog("客户端断开连接");
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string message = txtSend.Text;
            if (!string.IsNullOrEmpty(message))
            {
                GlobalCommData.mCommunicationManager.TcpIpComm.Client.Send(message);
                AppendLog("发送: " + message);
                txtSend.Text = string.Empty;
            }
        }

        // ================================================================
        // 通用通讯日志操作
        // ================================================================

        private void btnKeepConfig_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }

        private void chkShowCommLog_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            _isLogVisible = chkShowCommLog.Checked;
        }


        /// <summary>
        /// 机器人通讯模式切换：RSI 选中时取消 EKI（二选一）
        /// </summary>
        private void radioModeRSI_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (radioModeRSI.Checked && radioModeEKI.Checked)
                radioModeEKI.Checked = false;
        }

        /// <summary>
        /// 机器人通讯模式切换：EKI 选中时取消 RSI（二选一）
        /// </summary>
        private void radioModeEKI_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (radioModeEKI.Checked && radioModeRSI.Checked)
                radioModeRSI.Checked = false;
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }
            string line = string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine);
            // 限制日志行数：超过 5000 字符时截断前半
            string newText = inputLog.Text + line;
            if (newText.Length > 5000)
                newText = newText.Substring(newText.Length - 4000);
            inputLog.Text = newText;
            inputLog.SelectionStart = inputLog.Text.Length;
            if (inputLog.CanSelect)
                inputLog.ScrollToCaret();
        }

        /// <summary>
        /// 释放自定义资源，由父页面在关闭时调用
        /// </summary>
        public void DisposeComponents()
        {
            GlobalCommData.CommunicationCommandReceived -= GlobalCommData_CommunicationCommandReceived;
            _robotManager?.Dispose();
            _robotManager = null;
        }
    }
}