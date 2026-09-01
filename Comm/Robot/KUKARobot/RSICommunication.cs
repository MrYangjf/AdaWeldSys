using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace AdaWeldSystem.Comm.Robot.KUKARobot
{
    /// <summary>
    /// RSI (Robot Sensor Interface) UDP 实时通讯管理类
    /// 基于 UDP/IP 协议与 KUKA 机器人控制系统进行毫秒级 XML 数据交换
    /// </summary>
    public class RSICommunication : IDisposable
    {
        private const string Tag = "RSICommunication";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }
        private UdpClient _udpClient;
        private IPEndPoint _robotEndPoint;
        private IPEndPoint _localEndPoint;
        private Thread _receiveThread;
        private bool _isRunning;
        private ulong _currentIPOC;
        private readonly object _ipocLock = new object();
        private RSIConfig _config;

        /// <summary>
        /// 获取 RSI 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        /// <summary>
        /// 获取或设置机器人 IP 地址
        /// </summary>
        public string RobotIP { get; set; }

        /// <summary>
        /// 获取或设置机器人端口号
        /// </summary>
        public int RobotPort { get; set; }

        /// <summary>
        /// 获取或设置本地端口号
        /// </summary>
        public int LocalPort { get; set; }

        /// <summary>
        /// 获取当前 IPOC 时间戳
        /// </summary>
        public ulong CurrentIPOC
        {
            get
            {
                lock (_ipocLock)
                {
                    return _currentIPOC;
                }
            }
        }

        /// <summary>
        /// RSI 数据接收事件
        /// </summary>
        public event EventHandler<RSIDataEventArgs> DataReceived;

        /// <summary>
        /// RSI 错误事件
        /// </summary>
        public event EventHandler<RSIErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public RSICommunication()
        {
            RobotIP = "192.168.1.10";
            RobotPort = 49152;
            LocalPort = 49153;
            _currentIPOC = 0;
        }

        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        public RSICommunication(string robotIP, int robotPort, int localPort)
        {
            RobotIP = robotIP;
            RobotPort = robotPort;
            LocalPort = localPort;
            _currentIPOC = 0;
        }

        /// <summary>
        /// 初始化 RSI 配置
        /// </summary>
        public bool Initialize(string xmlConfigPath)
        {
            try
            {
                _config = KUKAXMLConfig.LoadRSIConfig(xmlConfigPath);
                RobotIP = _config.IPNumber;
                RobotPort = _config.Port;
                Log(string.Format("RSI 配置已加载 IP {0} 端口 {1}", RobotIP, RobotPort));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 配置加载失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("配置加载失败", ex, RSIErrorType.ConnectionFailed);
                return false;
            }
        }

        /// <summary>
        /// 使用配置对象初始化
        /// </summary>
        public bool Initialize(RSIConfig config)
        {
            try
            {
                _config = config;
                RobotIP = config.IPNumber;
                RobotPort = config.Port;
                Log(string.Format("RSI 配置已加载 IP {0} 端口 {1}", RobotIP, RobotPort));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 配置加载失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("配置加载失败", ex, RSIErrorType.ConnectionFailed);
                return false;
            }
        }

        /// <summary>
        /// 启动 RSI 通讯
        /// </summary>
        public bool Start()
        {
            try
            {
                if (_isRunning)
                {
                    Log("RSI 已在运行中");
                    return true;
                }

                _localEndPoint = new IPEndPoint(IPAddress.Any, LocalPort);
                _udpClient = new UdpClient(_localEndPoint);
                _robotEndPoint = new IPEndPoint(IPAddress.Parse(RobotIP), RobotPort);

                _isRunning = true;
                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                Log(string.Format("RSI 已启动 本地端口 {0} 机器人 {1} 端口 {2}", LocalPort, RobotIP, RobotPort));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 启动失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("启动失败", ex, RSIErrorType.ConnectionFailed);
                return false;
            }
        }

        /// <summary>
        /// 停止 RSI 通讯
        /// </summary>
        public bool Stop()
        {
            try
            {
                _isRunning = false;

                if (_receiveThread != null && _receiveThread.IsAlive)
                {
                    _receiveThread.Join(1000);
                    if (_receiveThread.IsAlive)
                    {
                        _receiveThread.Abort();
                    }
                    _receiveThread = null;
                }

                if (_udpClient != null)
                {
                    _udpClient.Close();
                    _udpClient = null;
                }

                Log("RSI 已停止");
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 停止异常 原因 {0}", ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// 发送机器人状态数据
        /// </summary>
        public void SendRobotData(KUKARobotData data)
        {
            if (!_isRunning || _udpClient == null)
            {
                return;
            }

            try
            {
                lock (_ipocLock)
                {
                    _currentIPOC++;
                    data.IPOC = _currentIPOC;
                }

                string xml = SerializeRobotData(data);
                byte[] bytes = Encoding.UTF8.GetBytes(xml);
                _udpClient.Send(bytes, bytes.Length, _robotEndPoint);
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 发送失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("发送失败", ex, RSIErrorType.XMLSerializationError);
            }
        }

        /// <summary>
        /// 接收修正数据（同步阻塞）
        /// </summary>
        public KUKACorrectionData ReceiveCorrectionData()
        {
            if (!_isRunning || _udpClient == null)
            {
                return null;
            }

            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = _udpClient.Receive(ref remoteEndPoint);
                string xml = Encoding.UTF8.GetString(bytes);
                return DeserializeCorrectionData(xml);
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 接收失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("接收失败", ex, RSIErrorType.XMLDeserializationError);
                return null;
            }
        }

        /// <summary>
        /// 尝试接收修正数据（带超时）
        /// </summary>
        public bool TryReceiveCorrectionData(out KUKACorrectionData correction, int timeoutMs)
        {
            correction = null;
            if (!_isRunning || _udpClient == null)
            {
                return false;
            }

            try
            {
                _udpClient.Client.ReceiveTimeout = timeoutMs;
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = _udpClient.Receive(ref remoteEndPoint);
                _udpClient.Client.ReceiveTimeout = 0;
                string xml = Encoding.UTF8.GetString(bytes);
                correction = DeserializeCorrectionData(xml);
                return correction != null;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    return false;
                }
                Log(string.Format("RSI 接收异常 原因 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("接收超时", ex, RSIErrorType.NetworkTimeout);
                return false;
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 接收异常 原因 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("接收异常", ex, RSIErrorType.XMLDeserializationError);
                return false;
            }
        }

        /// <summary>
        /// 序列化机器人数据为 RSI XML
        /// </summary>
        public string SerializeRobotData(KUKARobotData data)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("Rob");
            root.SetAttribute("TYPE", "KUKA");
            doc.AppendChild(root);

            // 笛卡尔实际位置
            XmlElement rIstElem = doc.CreateElement("RIst");
            rIstElem.SetAttribute("X", data.RIst.X.ToString("F4"));
            rIstElem.SetAttribute("Y", data.RIst.Y.ToString("F4"));
            rIstElem.SetAttribute("Z", data.RIst.Z.ToString("F4"));
            rIstElem.SetAttribute("A", data.RIst.A.ToString("F4"));
            rIstElem.SetAttribute("B", data.RIst.B.ToString("F4"));
            rIstElem.SetAttribute("C", data.RIst.C.ToString("F4"));
            root.AppendChild(rIstElem);

            // IPOC 时间戳
            XmlElement ipocElem = doc.CreateElement("IPOC");
            ipocElem.InnerText = data.IPOC.ToString();
            root.AppendChild(ipocElem);

            return doc.OuterXml;
        }

        /// <summary>
        /// 反序列化传感器修正数据 XML
        /// </summary>
        public KUKACorrectionData DeserializeCorrectionData(string xml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                KUKACorrectionData correction = new KUKACorrectionData();

                // 解析 IPOC
                XmlNode ipocNode = doc.SelectSingleNode("//IPOC");
                if (ipocNode != null)
                {
                    correction.IPOC = ulong.Parse(ipocNode.InnerText);
                }

                // 校验 IPOC
                lock (_ipocLock)
                {
                    if (correction.IPOC != _currentIPOC)
                    {
                        Log(string.Format("RSI IPOC 不匹配 接收 {0} 期望 {1}", correction.IPOC, _currentIPOC), MessageLevel.Warning);
                        OnErrorOccurred("IPOC 时间戳不匹配", null, RSIErrorType.IPOMismatch);
                    }
                }

                // 解析笛卡尔修正
                XmlNode rKorrNode = doc.SelectSingleNode("//RKorr");
                if (rKorrNode != null)
                {
                    if (rKorrNode.Attributes["X"] != null) correction.RKorr.X = double.Parse(rKorrNode.Attributes["X"].Value);
                    if (rKorrNode.Attributes["Y"] != null) correction.RKorr.Y = double.Parse(rKorrNode.Attributes["Y"].Value);
                    if (rKorrNode.Attributes["Z"] != null) correction.RKorr.Z = double.Parse(rKorrNode.Attributes["Z"].Value);
                    if (rKorrNode.Attributes["A"] != null) correction.RKorr.A = double.Parse(rKorrNode.Attributes["A"].Value);
                    if (rKorrNode.Attributes["B"] != null) correction.RKorr.B = double.Parse(rKorrNode.Attributes["B"].Value);
                    if (rKorrNode.Attributes["C"] != null) correction.RKorr.C = double.Parse(rKorrNode.Attributes["C"].Value);
                }

                return correction;
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI XML 反序列化失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("XML 反序列化失败", ex, RSIErrorType.XMLDeserializationError);
                return null;
            }
        }

        /// <summary>
        /// 接收循环线程
        /// </summary>
        private void ReceiveLoop()
        {
            Log("RSI 接收线程已启动");

            while (_isRunning)
            {
                try
                {
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _udpClient.Receive(ref remoteEndPoint);
                    string xml = Encoding.UTF8.GetString(bytes);

                    KUKACorrectionData correction = DeserializeCorrectionData(xml);
                    if (correction != null)
                    {
                        RSIDataEventArgs args = new RSIDataEventArgs
                        {
                            CorrectionData = correction,
                            IPOC = correction.IPOC,
                            Timestamp = DateTime.Now
                        };

                        EventHandler<RSIDataEventArgs> handler = DataReceived;
                        if (handler != null)
                        {
                            handler(this, args);
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (_isRunning)
                    {
                        Log(string.Format("RSI 接收线程 Socket 异常 原因 {0}", ex.Message), MessageLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log(string.Format("RSI 接收线程异常 原因 {0}", ex.Message), MessageLevel.Error);
                        OnErrorOccurred("接收线程异常", ex, RSIErrorType.Unknown);
                    }
                }
            }

            Log("RSI 接收线程已退出");
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        private void OnErrorOccurred(string message, Exception ex, RSIErrorType errorType)
        {
            EventHandler<RSIErrorEventArgs> handler = ErrorOccurred;
            if (handler != null)
            {
                handler(this, new RSIErrorEventArgs
                {
                    ErrorMessage = message,
                    Exception = ex,
                    ErrorType = errorType
                });
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}