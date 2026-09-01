using STTech.BytesIO.Tcp;
using System;
using System.Collections.Generic;

namespace AdaWeldSystem.Comm.NetworkPort.Ethernet
{
    /// <summary>
    /// TCP/IP 通信管理类，负责封装 TCP 服务端与客户端的初始化、配置加载及生命周期管理。
    /// 位于 Comm/Ethernet 命名空间，属于通用以太网通讯层。
    /// 非 UI 类，实现标准 IDisposable.Dispose() 释放内部资源。
    /// </summary>
    public class TcpIpComm : IDisposable
    {
        /// <summary>
        /// TCP 服务端实例。
        /// </summary>
        private readonly SstTcpServer _server;

        /// <summary>
        /// TCP 客户端实例。
        /// </summary>
        private readonly SstTcpClient _client;

        /// <summary>
        /// 所属通讯管理器，用于读取 XML 通讯结构配置。
        /// </summary>
        private readonly CommunicationManager _manager;

        /// <summary>
        /// 获取 TCP 服务端实例。
        /// </summary>
        public SstTcpServer Server => _server;

        /// <summary>
        /// 获取 TCP 客户端实例。
        /// </summary>
        public SstTcpClient Client => _client;

        /// <summary>
        /// 初始化 <see cref="TcpIpComm"/> 类的新实例，并加载配置。
        /// </summary>
        public TcpIpComm(CommunicationManager manager)
        {
            _manager = manager;
            _server = new SstTcpServer();
            _client = new SstTcpClient();
            LoadConfig();
        }

        /// <summary>
        /// 从通讯管理器的 XML 配置中加载服务端与客户端的 IP 及端口信息。
        /// </summary>
        public void LoadConfig()
        {
            _server.ServerIp = _manager.ServerXdoc.GetIp();
            _server.ServerPort = _manager.ServerXdoc.GetPort();
            _client.TargetIp = _manager.ClientXdoc.GetIp();
            _client.TargetPort = _manager.ClientXdoc.GetPort();
        }

        /// <summary>
        /// 启动服务端监听任务。
        /// </summary>
        public void StartServerCommTask()
        {
            _server.StartListening();
        }

        /// <summary>
        /// 停止服务端监听任务。
        /// </summary>
        public void StopServerCommTask()
        {
            _server.StopListening();
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose）。
        /// 依次停止并释放服务端与客户端底层连接。
        /// </summary>
        public void Dispose()
        {
            try { _server?.Dispose(); }
            catch (Exception ex) { GlobalCommData.ShowLog("TcpIpComm", $"释放服务端异常 {ex.Message}", MessageLevel.Warning); }
            try { _client?.Dispose(); }
            catch (Exception ex) { GlobalCommData.ShowLog("TcpIpComm", $"释放客户端异常 {ex.Message}", MessageLevel.Warning); }
        }

        /// <summary>
        /// 启动客户端连接任务。
        /// </summary>
        public void StartClientCommTask()
        {
            _client.StartConnect();
        }

        /// <summary>
        /// 停止客户端连接任务。
        /// </summary>
        public void StopClientCommTask()
        {
            _client.Disconnect();
        }
    }

    /// <summary>
    /// TCP 服务端封装类，提供监听、连接管理及数据接收功能。
    /// 非 UI 类，实现标准 IDisposable.Dispose() 释放底层 TcpServer。
    /// </summary>
    public class SstTcpServer : IDisposable
    {
        /// <summary>
        /// 日志标签。
        /// </summary>
        private const string Tag = "TCPServer";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 默认服务端 IP 地址。
        /// </summary>
        private string _serverIp = "127.0.0.1";

        /// <summary>
        /// 获取或设置服务端 IP 地址。
        /// </summary>
        public string ServerIp
        {
            get => _serverIp;
            set => _serverIp = value;
        }

        /// <summary>
        /// 默认服务端端口号。
        /// </summary>
        private int _serverPort = 6000;

        /// <summary>
        /// 获取或设置服务端端口号。
        /// </summary>
        public int ServerPort
        {
            get => _serverPort;
            set => _serverPort = value;
        }

        /// <summary>
        /// 最大客户端连接数限制。
        /// </summary>
        private uint _clientLimited = 5;

        /// <summary>
        /// 获取或设置最大客户端连接数限制。
        /// </summary>
        public uint ClientLimited
        {
            get => _clientLimited;
            set => _clientLimited = value;
        }

        /// <summary>
        /// TCP 服务端底层套接字。
        /// </summary>
        public TcpServer ServerSocket { get; private set; }

        /// <summary>
        /// 接收到的消息列表。
        /// </summary>
        public List<string> MessageList { get; private set; }

        /// <summary>
        /// 初始化 <see cref="SstTcpServer"/> 类的新实例。
        /// </summary>
        public SstTcpServer()
        {
            ServerSocket = new TcpServer();
            MessageList = new List<string>();
        }

        /// <summary>
        /// 析构函数，释放服务端资源（兜底）。
        /// </summary>
        ~SstTcpServer()
        {
            Dispose();
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose）。
        /// 停止监听并释放底层 TcpServer。
        /// </summary>
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
                    Log($"停止服务端异常 {ex.Message}");
                }
                finally
                {
                    ServerSocket.Dispose();
                    ServerSocket = null;
                }
            }
        }

        /// <summary>
        /// 启动服务端监听。
        /// </summary>
        public void StartListening()
        {
            ServerSocket.Host = _serverIp;
            ServerSocket.Port = _serverPort;
            ServerSocket.MaxConnections = _clientLimited;
            ServerSocket.StartAsync();

            ServerSocket.Started += ServerSocket_Started;
            ServerSocket.Closed += ServerSocket_Closed;
        }

        /// <summary>
        /// 停止服务端监听。
        /// </summary>
        public void StopListening()
        {
            try
            {
                ServerSocket.StopAsync();
            }
            catch (Exception ex)
            {
                Log($"停止监听异常 {ex.Message}");
            }
        }

        /// <summary>
        /// 客户端断开连接事件处理。
        /// </summary>
        private void ServerSocket_ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            Log("客户端已断开！");
        }

        /// <summary>
        /// 服务端关闭事件处理。
        /// </summary>
        private void ServerSocket_Closed(object sender, EventArgs e)
        {
            Log("服务器停止监听！");
            ServerSocket.ClientConnected -= ServerSocket_ClientConnected;
            ServerSocket.ClientDisconnected -= ServerSocket_ClientDisconnected;
        }

        /// <summary>
        /// 服务端启动事件处理。
        /// </summary>
        private void ServerSocket_Started(object sender, EventArgs e)
        {
            Log("服务器开启监听！");
            ServerSocket.ClientConnected += ServerSocket_ClientConnected;
            ServerSocket.ClientDisconnected += ServerSocket_ClientDisconnected;
        }

        /// <summary>
        /// 客户端连接事件处理。
        /// </summary>
        private void ServerSocket_ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            e.Client.OnDataReceived += Client_OnDataReceived;
            Log("客户端已连接！");
        }

        /// <summary>
        /// 数据接收事件处理。
        /// </summary>
        private void Client_OnDataReceived(object sender, STTech.BytesIO.Core.DataReceivedEventArgs e)
        {
            TcpClient tcpClient = (TcpClient)sender;
            string message = e.Data.EncodeToString("GBK");
            MessageList.Add(message);

            // 广播通讯指令到 Workflow 模块以触发工作流
            Log($"服务器接收数据 {message}");
            GlobalCommData.BroadcastCommunicationCommand(message);
        }
    }

    /// <summary>
    /// TCP 客户端封装类，提供连接、断开及数据发送功能。
    /// 非 UI 类，实现标准 IDisposable.Dispose() 释放底层 TcpClient。
    /// </summary>
    public class SstTcpClient : IDisposable
    {
        /// <summary>
        /// 日志标签。
        /// </summary>
        private const string Tag = "TCPClient";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 默认目标 IP 地址。
        /// </summary>
        private string _targetIp = "127.0.0.1";

        /// <summary>
        /// 获取或设置目标 IP 地址。
        /// </summary>
        public string TargetIp
        {
            get => _targetIp;
            set => _targetIp = value;
        }

        /// <summary>
        /// 默认目标端口号。
        /// </summary>
        private int _targetPort = 6000;

        /// <summary>
        /// 获取或设置目标端口号。
        /// </summary>
        public int TargetPort
        {
            get => _targetPort;
            set => _targetPort = value;
        }

        /// <summary>
        /// TCP 客户端底层套接字。
        /// </summary>
        public TcpClient TcpClient { get; set; }

        /// <summary>
        /// 向服务端发送字符串消息。
        /// </summary>
        public void Send(string message)
        {
            TcpClient.Send(message.GetBytes("utf-8"));
        }

        /// <summary>
        /// 启动客户端连接。
        /// </summary>
        public void StartConnect()
        {
            TcpClient.Host = _targetIp;
            TcpClient.Port = _targetPort;
            TcpClient.Connect();
            Log("客户端连接远端服务器！");
        }

        /// <summary>
        /// 断开客户端连接。
        /// </summary>
        public void Disconnect()
        {
            try
            {
                TcpClient.Disconnect();
                Log("客户端断开连接！");
            }
            catch (Exception ex)
            {
                Log($"断开连接异常 {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose）。
        /// 断开并释放底层 TcpClient。
        /// </summary>
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
                    Log($"释放客户端异常 {ex.Message}");
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
