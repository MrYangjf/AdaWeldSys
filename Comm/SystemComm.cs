using AdaWeldSystem.FileOperate;
using AdaWeldSystem.SqlLiteDatabase;
using System;
using System.Drawing;
using System.Threading;
using Emgu.CV;

namespace AdaWeldSystem.Comm
{
    /// <summary>
    /// 消息类型枚举
    /// </summary>
    public enum MessageType
    {
        Summary,
        Run,
        Flow,
        Debug
    }

    /// <summary>
    /// 消息级别枚举
    /// </summary>
    public enum MessageLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 操作权限等级枚举
    /// </summary>
    public enum OperateLevel
    {
        Operator = 2,
        Engineer = 1,
        Admin = 0
    }

    /// <summary>
    /// 消息事件参数（用于日志/通讯信息显示）
    /// </summary>
    public class MessageArgs : EventArgs
    {
        public string strMessage = "";
        public Color MessageShowColor = Color.Black;
    }

    /// <summary>
    /// 全局通讯数据中心
    /// 由原 GlobalCommData.cs 与 SystemStatus.cs 合并而成（SystemComm.cs）。
    /// 职责（按优先级排序）：
    /// 1. 全局事件触发（日志、通讯指令广播）
    /// 2. 全局日志管理
    /// 3. 全局通讯配置管理（由 CommunicationManager 实例化具体通讯结构，并持有通用 TCP 实例）
    /// 自动调整参数管理已迁出到 [[WeldDataManager]]
    /// 遗留支持：权限/操作等级（计划逐步迁出）
    /// </summary>
    static class GlobalCommData
    {
        #region 1. 全局事件触发

        /// <summary>
        /// 日志信息Handler
        /// </summary>
        public static event EventHandler<MessageArgs> EventInfoHandler;

        /// <summary>
        /// 通讯指令接收事件（Server/Client/S7/Profinet/Robot 收到指令后广播）
        /// Workflow 订阅此事件以触发工作流步骤
        /// </summary>
        public static event EventHandler<string> CommunicationCommandReceived;

        #endregion

        #region 2. 全局日志管理

        /// <summary>
        /// 系统日志操作
        /// </summary>
        public static Log MachineLog;

        /// <summary>
        /// TCP通讯日志操作
        /// </summary>
        public static Log TcpMessageLog;

        /// <summary>
        /// 显示日志
        /// </summary>
        public static void ShowLog(string TAG, string message, MessageLevel msgLevel = MessageLevel.Info, MessageType msgType = MessageType.Debug)
        {
            EventHandler<MessageArgs> Handler = EventInfoHandler;
            switch (msgLevel)
            {
                case MessageLevel.Info:
                    _logArgs.strMessage = message;
                    _logArgs.MessageShowColor = Color.Black;
                    Handler?.Invoke(null, _logArgs);
                    MachineLog.Info(TAG, message);
                    break;

                case MessageLevel.Warning:
                    _logArgs.strMessage = message;
                    _logArgs.MessageShowColor = Color.Blue;
                    Handler?.Invoke(null, _logArgs);
                    MachineLog.Warn(TAG, message);
                    break;

                case MessageLevel.Error:
                    _logArgs.strMessage = message;
                    _logArgs.MessageShowColor = Color.Red;
                    Handler?.Invoke(null, _logArgs);
                    MachineLog.Error(TAG, message);
                    break;
            }
        }

        /// <summary>
        /// 记录通讯日志到界面（受 CommunicationManager.ShowCommLog 开关控制）
        /// 由 Workflow 在接收到通讯指令后调用，避免在通讯层直接打印
        /// </summary>
        public static void ShowCommunicationLog(string source, string message)
        {
            if (mCommunicationManager != null && mCommunicationManager.ShowCommLog)
            {
                _logArgs.strMessage = string.Format("[{0}] {1}", source, message);
                _logArgs.MessageShowColor = Color.DarkGreen;
                EventHandler<MessageArgs> handler = EventInfoHandler;
                handler?.Invoke(null, _logArgs);
            }
            TcpMessageLog.Debug(source + Thread.GetDomainID(), message);
        }

        #endregion

        #region 3. 全局通讯配置管理

        /// <summary>
        /// 通讯管理器（PLC、机器人、通用 TCP 持久化配置）
        /// 内部实例化 Server/Client/Send/Receive XML 通讯结构，并持有通用 TCP 通讯实例 TcpIpComm
        /// </summary>
        public static CommunicationManager mCommunicationManager;

        /// <summary>
        /// 广播通讯指令到 Workflow 模块
        /// 由各通讯实现（TCP/S7/Profinet/Robot）在收到有效指令后调用
        /// </summary>
        public static void BroadcastCommunicationCommand(string message)
        {
            EventHandler<string> handler = CommunicationCommandReceived;
            if (handler != null)
            {
                handler(null, message);
            }
        }

        #endregion

        #region 4. 遗留支持（计划迁出）

        /// <summary>
        /// 权限管理文件
        /// </summary>
        /// <remarks>待迁出到独立的认证服务模块。</remarks>
        public static AuthManager mAuthManager;

        /// <summary>
        /// 操作等级
        /// </summary>
        public static OperateLevel mOperateLevel;

        #endregion

        #region 5. 事件参数复用（高频事件避免GC压力）

        private static readonly MessageArgs _logArgs;

        #endregion

        #region 6. 静态构造（集中初始化，避免字段分散初始化）

        static GlobalCommData()
        {
            // 日志必须先初始化，后续管理器构造失败时会调用 ShowLog
            MachineLog = new Log("智能填丝焊接系统日志");
            TcpMessageLog = new Log("智能填丝焊接通讯日志");

            // 自动调整参数管理已迁出到 WeldDataManager（WeldParamControl 模块）
            // 由 WeldDataManager 单例在首次访问时完成初始化，包括：
            // - CurrentAutoParamId 的 INI 持久化
            // - Default 配置的初始构建与保护

            // CommunicationManager 内部负责实例化通用 TCP 通讯对象 TcpIpComm
            mCommunicationManager = new CommunicationManager();

            mAuthManager = new AuthManager();
            mOperateLevel = OperateLevel.Operator;

            _logArgs = new MessageArgs();
        }

        #endregion

        #region 7. UI 配置（AntdUI.Config 一次性加载）

        /// <summary>
        /// UI 配置一次性加载标志（防止重复初始化 AntdUI 全局配置）
        /// </summary>
        private static bool _uiConfigLoaded;

        /// <summary>
        /// 一次性加载 AntdUI 全局 UI 配置（主题 + 文本渲染质量）。
        /// 集中管理 AntdUI.Config 的所有设置，替代原先分散在 Program.cs / FormMain 构造函数中的配置。
        /// 须在 UI 线程调用（FormMain 构造时）；内部以 _uiConfigLoaded 守卫保证只执行一次。
        /// </summary>
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

            // 全局主题：AntdUI 用 Config 替代 UIStyles（原 FormMain 构造函数）
            AntdUI.Config.IsLight = true;
           

            // 文本渲染质量（原 Program.Main）
            AntdUI.Config.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            AntdUI.Config.TextRenderingHighQuality = true;

            AntdUI.Config.Animation= true;
            AntdUI.Config.ShadowEnabled = true;
        }

        #endregion
    }
}
