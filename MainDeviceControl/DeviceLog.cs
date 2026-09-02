using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MainDeviceControl
{
    /// <summary>
    /// 设备日志统一出口：封装 GlobalCommData.ShowLog，收敛「日志标签 + 级别」写法。
    /// </summary>
    /// <remarks>
    /// 全局约定（2026-09-02 确立）：
    ///   1. 每个流程与管控类持有一个日志标签——子工作流用基类 <c>Tag</c>，
    ///      独立管控类（DeviceControlWork / DeviceStatusWork）用私有 <c>_tag</c>；
    ///      日志一律经本类写出，禁止再直接调用 GlobalCommData.ShowLog，避免标签写法发散。
    ///   2. 设备日志（运行/状态/异常）走本类；通讯收发内容走 GlobalCommData.ShowCommunicationLog。
    ///      二者不得混用：连接 open/close 归设备日志，报文 content/stop 归通信日志。
    ///   3. 日志内容为纯文本无符号；函数块起止与结果处各记一条。
    /// </remarks>
    public static class DeviceLog
    {
        /// <summary>写设备日志。</summary>
        /// <param name="tag">日志标签（流程名或管控类名）</param>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        public static void Write(string tag, string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(tag, message, level);
        }

        /// <summary>写 Information 级设备日志。</summary>
        /// <param name="tag">日志标签</param>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        public static void Info(string tag, string message)
        {
            Write(tag, message, MessageLevel.Info);
        }

        /// <summary>写 Warning 级设备日志。</summary>
        /// <param name="tag">日志标签</param>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        public static void Warn(string tag, string message)
        {
            Write(tag, message, MessageLevel.Warning);
        }

        /// <summary>写 Error 级设备日志。</summary>
        /// <param name="tag">日志标签</param>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        public static void Error(string tag, string message)
        {
            Write(tag, message, MessageLevel.Error);
        }
    }
}
