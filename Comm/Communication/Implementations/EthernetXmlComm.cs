using STTech.BytesIO.Tcp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.Comm.Communication.Abstractions;

namespace AdaWeldSystem.Comm.Communication.Implementations
{
    /// <summary>以太网标准通讯实现（服务端 + 客户端双模式）</summary>
    public class EthernetXmlComm : CommChannelBase
    {
        private readonly SstTcpServer _server;
        private readonly SstTcpClient _client;

        /// <summary>通道名称</summary>
        public override string Name
        {
            get { return "Ethernet_XML"; }
        }

        /// <summary>是否已连接（客户端已连接或服务端正在监听）</summary>
        public override bool IsConnected
        {
            get { return _client.TcpClient != null || _server.ServerSocket != null; }
        }

        /// <summary>服务端封装（监听、接收、广播）</summary>
        public SstTcpServer Server
        {
            get { return _server; }
        }

        /// <summary>客户端封装（连接、发送）</summary>
        public SstTcpClient Client
        {
            get { return _client; }
        }

        /// <summary>构造函数</summary>
        public EthernetXmlComm()
            : base("EthernetXmlComm")
        {
            _server = new SstTcpServer();
            _client = new SstTcpClient();
        }

        #region 生命周期（CommChannelBase）

        /// <summary>建立连接（默认启动客户端连接）</summary>
        public override void Connect()
        {
            ConnectClient();
        }

        /// <summary>断开连接（停止客户端与服务端）</summary>
        public override void Disconnect()
        {
            DisconnectClient();
            StopServer();
        }

        /// <summary>释放资源（标准 IDisposable.Dispose）</summary>
        public override void Dispose()
        {
            Disconnect();
            try { _server?.Dispose(); }
            catch (Exception ex) { Log(string.Format("释放服务端异常 {0}", ex.Message), MessageLevel.Warning); }
            try { _client?.Dispose(); }
            catch (Exception ex) { Log(string.Format("释放客户端异常 {0}", ex.Message), MessageLevel.Warning); }
        }

        #endregion

        #region 服务端

        /// <summary>启动服务端监听</summary>
        public void StartServer()
        {
            _server.StartListening();
        }

        /// <summary>停止服务端监听</summary>
        public void StopServer()
        {
            _server.StopListening();
        }

        #endregion

        #region 客户端

        /// <summary>启动客户端连接</summary>
        public void ConnectClient()
        {
            _client.StartConnect();
        }

        /// <summary>断开客户端连接</summary>
        public void DisconnectClient()
        {
            _client.Disconnect();
        }

        /// <summary>发送原始字节（走客户端）</summary>
        /// <param name="data">待发送字节</param>
        public override void Send(byte[] data)
        {
            _client.Send(data);
        }

        /// <summary>发送文本（走客户端）</summary>
        /// <param name="text">待发送文本</param>
        public override void Send(string text)
        {
            _client.Send(text);
        }

        /// <summary>按预定义 XML 文档格式发送（KUKA 风格：根节点 + 具名元素）</summary>
        /// <param name="rootName">根节点名</param>
        /// <param name="elements">元素名与值的键值对</param>
        /// <returns>true 发送成功 / false 失败</returns>
        public bool SendXmlDocument(string rootName, IEnumerable<KeyValuePair<string, string>> elements)
        {
            string xml = BuildXmlDocument(rootName, elements);
            if (string.IsNullOrEmpty(xml))
            {
                return false;
            }
            Send(xml);
            Log(string.Format("发送 XML 文档 根节点 {0}", rootName));
            return true;
        }

        #endregion

        #region XML 文档格式（向 KUKA 交互格式靠齐）

        /// <summary>构建预定义 XML 文档：根节点 + 具名元素文本（KUKA 风格）</summary>
        /// <param name="rootName">根节点名</param>
        /// <param name="elements">元素名与值的键值对</param>
        /// <returns>XML 文本；构建失败返回 null</returns>
        public static string BuildXmlDocument(string rootName, IEnumerable<KeyValuePair<string, string>> elements)
        {
            if (string.IsNullOrEmpty(rootName) || elements == null)
            {
                return null;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                XmlElement root = doc.CreateElement(rootName);
                doc.AppendChild(root);
                foreach (KeyValuePair<string, string> kv in elements)
                {
                    XmlElement elem = doc.CreateElement(kv.Key);
                    elem.InnerText = kv.Value ?? string.Empty;
                    root.AppendChild(elem);
                }
                return doc.OuterXml;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>解析收到的 XML 文档为元素字典（根节点下的一级具</summary>

        /// <param name="xml">XML 文本</param>
        /// <returns>元素名与值的字典；解析失败返回 null</returns>
        public static Dictionary<string, string> ParseXmlDocument(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return null;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                Dictionary<string, string> result = new Dictionary<string, string>();
                XmlNode root = doc.DocumentElement;
                if (root == null)
                {
                    return null;
                }
                foreach (XmlNode node in root.ChildNodes)
                {
                    result[node.Name] = node.InnerText;
                }
                return result;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        #endregion
    }

    /// <summary>TCP 服务端封装（监听、连接管理、XML 文本接收）</summary>
    public class SstTcpServer : IDisposable
    {
        private const string Tag = "EthernetServer";

        private int _serverPort = 6000;

        private string _serverIp = "127.0.0.1";

        private uint _clientLimited = 5;

        /// <summary>获取或设置服务端 IP 地址</summary>
        public string ServerIp
        {
            get { return _serverIp; }
            set { _serverIp = value; }
        }

        /// <summary>获取或设置服务端端口号</summary>
        public int ServerPort
        {
            get { return _serverPort; }
            set { _serverPort = value; }
        }

        /// <summary>获取或设置最大客户端连接数限制</summary>
        public uint ClientLimited
        {
            get { return _clientLimited; }
            set { _clientLimited = value; }
        }

        /// <summary>TCP 服务端底层套接字</summary>
        public TcpServer ServerSocket { get; private set; }

        /// <summary>初始化服务端封装</summary>
        public SstTcpServer()
        {
            ServerSocket = new TcpServer();
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">消息内容。</param>
        /// <param name="level">日志级别。</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>客户端断开连接事件处理</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void ServerSocket_ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            Log("客户端已断开");
        }

        /// <summary>服务端关闭事件处理</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void ServerSocket_Closed(object sender, EventArgs e)
        {
            Log("服务器停止监听");
            ServerSocket.ClientConnected -= ServerSocket_ClientConnected;
            ServerSocket.ClientDisconnected -= ServerSocket_ClientDisconnected;
        }

        /// <summary>服务端启动事件处理</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void ServerSocket_Started(object sender, EventArgs e)
        {
            Log("服务器开启监听");
            ServerSocket.ClientConnected += ServerSocket_ClientConnected;
            ServerSocket.ClientDisconnected += ServerSocket_ClientDisconnected;
        }

        /// <summary>客户端连接事件处理</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void ServerSocket_ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            e.Client.OnDataReceived += Client_OnDataReceived;
            Log("客户端已连接");
        }

        /// <summary>数据接收事件处理</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void Client_OnDataReceived(object sender, STTech.BytesIO.Core.DataReceivedEventArgs e)
        {
            string message = e.Data.EncodeToString("GBK");
            Log(string.Format("服务器接收数据 {0}", message));

            // 广播通讯指令到 Workflow 模块以触发工作流
            GlobalCommData.BroadcastCommunicationCommand(message);
        }
        /// <summary>释放资源（标准 IDisposable.Dispose）</summary>
        public void Dispose()
        {
            if (ServerSocket != null)
            {
                try
                {
                    ServerSocket.StopAsync();
                }
                catch (Exception ex)
                {
                    Log(string.Format("停止服务端异常 {0}", ex.Message), MessageLevel.Warning);
                }
                finally
                {
                    ServerSocket.Dispose();
                    ServerSocket = null;
                }
            }
        }

        /// <summary>启动服务端监听</summary>
        public void StartListening()
        {
            ServerSocket.Host = _serverIp;
            ServerSocket.Port = _serverPort;
            ServerSocket.MaxConnections = _clientLimited;
            ServerSocket.StartAsync();

            ServerSocket.Started += ServerSocket_Started;
            ServerSocket.Closed += ServerSocket_Closed;
        }

        /// <summary>停止服务端监听</summary>
        public void StopListening()
        {
            try
            {
                ServerSocket.StopAsync();
            }
            catch (Exception ex)
            {
                Log(string.Format("停止监听异常 {0}", ex.Message), MessageLevel.Warning);
            }
        }

    }

    /// <summary>TCP 客户端封装，提供连接、断开与文本/字节发送。</summary>
    public class SstTcpClient : IDisposable
    {
        private const string Tag = "EthernetClient";

        private int _targetPort = 6000;

        private string _targetIp = "127.0.0.1";

        /// <summary>获取或设置目标 IP 地址</summary>
        public string TargetIp
        {
            get { return _targetIp; }
            set { _targetIp = value; }
        }

        /// <summary>获取或设置目标端口号</summary>
        public int TargetPort
        {
            get { return _targetPort; }
            set { _targetPort = value; }
        }

        /// <summary>TCP 客户端底层套接字</summary>
        public STTech.BytesIO.Tcp.TcpClient TcpClient { get; set; }

        /// <summary>初始化客户端封装</summary>
        public SstTcpClient()
        {
            TcpClient = new STTech.BytesIO.Tcp.TcpClient();
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">消息内容。</param>
        /// <param name="level">日志级别。</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>向服务端发送文本（UTF-8）</summary>
        /// <param name="message">待发送文本</param>
        public void Send(string message)
        {
            TcpClient.Send(message.GetBytes("utf-8"));
        }

        /// <summary>向服务端发送原始字节</summary>
        /// <param name="data">待发送字节</param>
        public void Send(byte[] data)
        {
            TcpClient.Send(data);
        }

        /// <summary>启动客户端连接</summary>
        public void StartConnect()
        {
            TcpClient.Host = _targetIp;
            TcpClient.Port = _targetPort;
            TcpClient.Connect();
            Log("客户端连接远端服务器");
        }

        /// <summary>断开客户端连接</summary>
        public void Disconnect()
        {
            try
            {
                TcpClient.Disconnect();
                Log("客户端断开连接");
            }
            catch (Exception ex)
            {
                Log(string.Format("断开连接异常 {0}", ex.Message), MessageLevel.Warning);
            }
        }

        /// <summary>释放资源（标准 IDisposable.Dispose）</summary>
        public void Dispose()
        {
            if (TcpClient != null)
            {
                try
                {
                    TcpClient.Disconnect();
                }
                catch (Exception ex)
                {
                    Log(string.Format("释放客户端异常 {0}", ex.Message), MessageLevel.Warning);
                }
                finally
                {
                    TcpClient.Dispose();
                    TcpClient = null;
                }
            }
        }
    }
}
