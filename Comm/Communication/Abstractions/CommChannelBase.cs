using System;
using System.Text;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.Comm.Communication.Abstractions
{
    /// <summary>
    /// 通讯数据接收事件参数（原始字节 + 文本视图）
    /// </summary>
    public class CommDataEventArgs : EventArgs
    {
        /// <summary>原始接收字节</summary>
        public byte[] Data { get; private set; }

        /// <summary>文本视图（UTF-8 解码；不可解码时为空串）</summary>
        public string Text { get; private set; }

        /// <summary>接收时间</summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="data">原始接收字节</param>
        /// <param name="text">文本视图</param>
        public CommDataEventArgs(byte[] data, string text)
        {
            Data = data;
            Text = text ?? string.Empty;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 对外通讯通道抽象基类（抽象层，零第三方依赖）
    /// 定义通讯类对外的属性与接口：通道名 / 连接状态 / 生命周期 / 流式收发 / 数据接收事件。
    /// 寄存器型读写（S7 DB 块、Modbus 寄存器）属各实现类专属能力，由 Manager 以具体类型属性暴露。
    /// 实现类：SiemensS7Comm / KukaRsiComm / KukaEkiComm / EthernetXmlComm / InLaserModbusComm。
    /// </summary>
    public abstract class CommChannelBase
    {
        private const string Tag = "CommChannelBase";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 通道名称（用于日志与配置路由）
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// 最近一次错误描述（空串表示无错误）
        /// </summary>
        public string LastErrorMessage { get; protected set; }

        /// <summary>
        /// 建立连接（实现类映射到各自的 Open / Start 语义）
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// 断开连接（实现类映射到各自的 Close / Stop 语义）
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// 数据接收事件（流式通道收到对端数据后触发）
        /// </summary>
        public event EventHandler<CommDataEventArgs> DataReceived;

        /// <summary>
        /// 发送原始字节（默认不支持，寄存器型通道无需实现）
        /// </summary>
        /// <param name="data">待发送字节</param>
        public virtual void Send(byte[] data)
        {
            Log(string.Format("通道 {0} 不支持流式字节发送", Name), MessageLevel.Warning);
        }

        /// <summary>
        /// 发送文本（默认按 UTF-8 编码后走 Send(byte[])）
        /// </summary>
        /// <param name="text">待发送文本</param>
        public virtual void Send(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Log("发送内容为空，已忽略", MessageLevel.Warning);
                return;
            }
            Send(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，ADR-020）
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// 触发数据接收事件（由实现类在收到对端数据后调用）
        /// </summary>
        /// <param name="data">原始字节</param>
        /// <param name="text">文本视图（可为 null，自动转空串）</param>
        protected virtual void OnDataReceived(byte[] data, string text)
        {
            EventHandler<CommDataEventArgs> handler = DataReceived;
            if (handler != null)
            {
                handler(this, new CommDataEventArgs(data, text));
            }
        }
    }
}
