using AdaWeldSystem.FileOperate;
using AdaWeldSystem.Comm.Communication;
using System;
using System.Drawing;
using System.Threading;
using AdaWeldSystem.SQLiteDBControl;
using AdaWeldSystem.SQLiteDBControl.Helpers;

namespace AdaWeldSystem.Comm.SystemComm
{
    /// <summary>消息类型枚举</summary>
    public enum MessageType
    {
        Summary,
        Run,
        Flow,
        Debug
    }

    /// <summary>消息级别枚举</summary>
    public enum MessageLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }

    /// <summary>操作权限等级（数值越小权限越高）</summary>
    public enum OperateLevel
    {
        /// <summary>管理员：全部页面与写权限，含用户管理。</summary>
        Admin = 0,

        /// <summary>工程师：除用户管理外的全部工程页面与写权限。</summary>
        Engineer = 1,

        /// <summary>操作员：仅基础页，关键参数不可见。</summary>
        Operator = 2
    }

    /// <summary>消息事件参数（日志/通讯信息显示）</summary>
    public class MessageArgs : EventArgs
    {
        public string strMessage = "";
        public Color MessageShowColor = Color.Black;
    }

    /// <summary>全局通讯数据中心</summary>
    static class GlobalCommData
    {
        #region 1. 全局事件触发

        /// <summary>日志信息 Handler</summary>
        public static event EventHandler<MessageArgs> EventInfoHandler;

        /// <summary>通讯指令接收事件，Workflow 订阅以触发工作流步骤</summary>
        public static event EventHandler<string> CommunicationCommandReceived;

        /// <summary>通讯日志查看事件，仅供通讯配置页订阅显示，不</summary>
        public static event EventHandler<string> CommLogViewEvent;

        #endregion

        #region 2. 全局日志管理

        private static string _logDedupKey = string.Empty;
        private static DateTime _logDedupTime = DateTime.MinValue;
        private static readonly object LogDedupLock = new object();

        /// <summary>界面是否显示 Debug 级日志（默认关闭，文件仍全量）</summary>
        private static volatile bool _showDebugToUi;

        /// <summary>系统日志操作</summary>
        public static Log MachineLog;

        /// <summary>TCP 通讯日志操作</summary>
        public static Log TcpMessageLog;

        /// <summary>界面调试日志开关</summary>
        public static bool ShowDebugToUi
        {
            get { return _showDebugToUi; }
            set { _showDebugToUi = value; }
        }

        /// <summary>同一秒内完全相同的 tag + 级别 + 文本只输出一次</summary>

        /// <param name="tag">日志标签</param>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        /// <returns>true = 本条属重复，应丢弃</returns>
        private static bool IsDuplicateWithinSecond(string tag, string message, MessageLevel level)
        {
            string key = tag + "|" + (int)level + "|" + message;
            lock (LogDedupLock)
            {
                DateTime now = DateTime.Now;
                if (key == _logDedupKey && (now - _logDedupTime).TotalSeconds < 1)
                {
                    return true;
                }
                _logDedupKey = key;
                _logDedupTime = now;
                return false;
            }
        }

        /// <summary>显示日志</summary>
        /// <param name="TAG">标签（取类名）</param>
        /// <param name="message">消息内容</param>
        /// <param name="msgLevel">消息级别（决定界面颜色与文件级别）</param>
        /// <param name="msgType">消息类型</param>
        public static void ShowLog(string TAG, string message, MessageLevel msgLevel = MessageLevel.Info, MessageType msgType = MessageType.Debug)
        {
            if (IsDuplicateWithinSecond(TAG, message, msgLevel)) return;

            EventHandler<MessageArgs> Handler = EventInfoHandler;
            MessageArgs args = _logArgs;
            switch (msgLevel)
            {
                case MessageLevel.Info:
                    if (args != null)
                    {
                        args.strMessage = message;
                        args.MessageShowColor = Color.Black;
                        Handler?.Invoke(null, args);
                    }
                    MachineLog?.Info(TAG, message);
                    break;

                case MessageLevel.Warning:
                    if (args != null)
                    {
                        args.strMessage = message;
                        args.MessageShowColor = Color.Blue;
                        Handler?.Invoke(null, args);
                    }
                    MachineLog?.Warn(TAG, message);
                    break;

                case MessageLevel.Error:
                    if (args != null)
                    {
                        args.strMessage = message;
                        args.MessageShowColor = Color.Red;
                        Handler?.Invoke(null, args);
                    }
                    MachineLog?.Error(TAG, message);
                    break;

                case MessageLevel.Debug:
                    // 界面默认不收 Debug；文件端仍全量，便于事后排障
                    if (_showDebugToUi && args != null)
                    {
                        args.strMessage = message;
                        args.MessageShowColor = Color.Gray;
                        Handler?.Invoke(null, args);
                    }
                    MachineLog?.Debug(TAG, message);
                    break;
            }
        }

        /// <summary>记录通讯日志到界面（受 ShowCommLog 开关控制，由 Workflow 调用）</summary>
        /// <param name="message">消息内容。</param>
        public static void ShowCommunicationLog(string source, string message)
        {
            if (mCommunicationManager != null && mCommunicationManager.ShowCommLog)
            {
                MessageArgs args = _logArgs;
                if (args != null)
                {
                    args.strMessage = string.Format("[{0}] {1}", source, message);
                    args.MessageShowColor = Color.DarkGreen;
                    EventHandler<MessageArgs> handler = EventInfoHandler;
                    handler?.Invoke(null, args);
                }
            }
            TcpMessageLog?.Debug(source + Thread.GetDomainID(), message);
        }

        /// <summary>通讯过程日志</summary>
        /// <param name="source">日志来源标签</param>
        /// <param name="message">日志内容</param>
        public static void ShowCommLogForView(string source, string message)
        {
            TcpMessageLog?.Debug(source + Thread.GetDomainID(), message);
            CommLogViewEvent?.Invoke(null, string.Format("[{0}] {1}", source, message));
        }

        #endregion

        #region 3. 全局通讯配置管理

        /// <summary>通讯管理器（PLC、机器人、通用 TCP 持久化配置）</summary>
        public static CommunicationManager mCommunicationManager;

        /// <summary>界面是否已就绪（全部子页面实例化完成）。</summary>
        private static volatile bool _isUiReady;

        /// <summary>应用是否已进入正常运行态（首屏初始化结束）。</summary>
        private static volatile bool _isAppRunning;

        /// <summary>界面就绪标志，设备初始化须先满足</summary>
        public static bool IsUiReady
        {
            get { return _isUiReady; }
        }

        /// <summary>正常运行态标志，false 时禁查历史数据</summary>
        public static bool IsAppRunning
        {
            get { return _isAppRunning; }
        }

        /// <summary>标记界面已就绪（由 FormMain 在子页面全部实例化后调用，</summary>
        public static void MarkUiReady()
        {
            _isUiReady = true;
        }

        /// <summary>标记进入正常运行态，</summary>
        public static void MarkAppRunning()
        {
            _isAppRunning = true;
        }

        /// <summary>广播通讯指令到 Workflow（各通讯实现收到有效指令后调</summary>
        /// <param name="message">消息内容。</param>
        public static void BroadcastCommunicationCommand(string message)
        {
            CommunicationCommandReceived?.Invoke(null, message);
        }

        #endregion

        #region 4. 遗留支持（计划迁出）

        /// <summary>权限管理文件</summary>
        public static AuthHelper mAuthHelper;

        /// <summary>操作等级（禁直接赋值，须走 <see cref="SetOperateLevel"/> 广播变更）</summary>
        public static OperateLevel mOperateLevel;

        /// <summary>权限变更事件，供各页刷新门控</summary>
        public static event EventHandler<OperateLevel> OperateLevelChanged;

        /// <summary>设置当前权限等级并广播变更事件</summary>
        /// <param name="level">目标权限等级。</param>
        public static void SetOperateLevel(OperateLevel level)
        {
            if (mOperateLevel == level) return;
            mOperateLevel = level;
            OperateLevelChanged?.Invoke(null, level);
        }

        #endregion

        #region 5. Dash 运行时变量（单次软件开启，不落盘）

        /// <summary>软件启动时刻（静态构造内赋值，软件关闭即丢失）。</summary>
        public static DateTime SoftwareStartTime { get; private set; }

        #endregion

        #region 6. 事件参数复用（高频事件避免GC压力）

        private static readonly MessageArgs _logArgs;

        #endregion

        #region 6. 静态构造（集中初始化，避免字段分散初始化）

        static GlobalCommData()
        {
            // 事件参数须先于管理器构造就绪
            // 静态构造未到赋值处会空引用崩溃。
            _logArgs = new MessageArgs();

            // 日志先初始化，构造失败时会写日志
            MachineLog = new Log("智能填丝焊接系统日志");
            TcpMessageLog = new Log("智能填丝焊接通讯日志");

            // 自动调整参数由 WeldParamManager 单例在首次访问时统一初始化

            // CommunicationManager 内部负责实例化通用 TCP 通讯对象 TcpIpComm
            mCommunicationManager = new CommunicationManager();

            mAuthHelper = SQLiteDatabaseManager.Instance.AuthHelper;
            mOperateLevel = OperateLevel.Admin;

            // Dash 运行时变量：软件启动时刻（单次，不落盘）
            SoftwareStartTime = DateTime.Now;
        }

        #endregion

        #region 7. UI 配置（AntdUI.Config 一次性加载）

        /// <summary>UI 配置一次性加载标志</summary>
        private static bool _uiConfigLoaded;

        /// <summary>一次性加载 AntdUI 全局 UI 配置（主题 + 文本渲染质量）</summary>
        public static void UIConfigSetting()
        {
            if (_uiConfigLoaded)
            {
                return;
            }
            _uiConfigLoaded = true;

            AntdUI.Config.Theme()
               .Dark("#000000", "#ffffff")
               .Light("#f2f2f2", "#000000")
               .FormBorderColor();

            // 全局主题
            AntdUI.Config.IsLight = true;
           

            // 文本渲染质量
            AntdUI.Config.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            AntdUI.Config.TextRenderingHighQuality = true;

            AntdUI.Config.Animation= false;
            AntdUI.Config.ShadowEnabled = false;
        }

        #endregion
    }
}

