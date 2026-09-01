using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace AdaWeldSystem.Comm.Robot.KUKARobot
{
    /// <summary>
    /// EKI TCP 数据交换管理类
    /// </summary>
    public class EKICommunication : IDisposable
    {
        #region 私有变量
        private const string Tag = "EKICommunication";
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private Thread _receiveThread;
        private bool _isConnected;
        private EKIConfig _config;
        private readonly object _connectLock = new object();
        #endregion

        #region 公共变量
        /// <summary>获取 EKI 是否已连接</summary>
        public bool IsConnected
        {
            get { return _isConnected; }
        }
        /// <summary>服务器 IP 地址</summary>
        public string ServerIP { get; set; }
        /// <summary>服务器端口号</summary>
        public int ServerPort { get; set; }
        /// <summary>运行模式（客户端/服务器）</summary>
        public EKIMode Mode { get; set; }
        /// <summary>环境配置</summary>
        public EKIEnvironment Environment { get; set; }
        /// <summary>连接名称（XML 文件名）</summary>
        public string ConnectionName { get; set; }
        public event EventHandler<EKIDataEventArgs> DataReceived;
        public event EventHandler<EKIConnectionEventArgs> ConnectionStateChanged;
        public event EventHandler<EKIErrorEventArgs> ErrorOccurred;
        #endregion

        #region 构造函数
        public EKICommunication()
        {
            ServerIP = "192.168.1.10";
            ServerPort = 54600;
            Mode = EKIMode.Client;
            Environment = EKIEnvironment.Program;
            ConnectionName = "EKI_Connection";
        }

        public EKICommunication(string serverIP, int serverPort, EKIMode mode)
        {
            ServerIP = serverIP;
            ServerPort = serverPort;
            Mode = mode;
            Environment = EKIEnvironment.Program;
            ConnectionName = "EKI_Connection";
        }
        #endregion

        #region 私有函数
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private bool TryReadXml(out string xml)
        {
            xml = string.Empty;
            if (!_isConnected || _networkStream == null)
            {
                return false;
            }

            try
            {
                if (_networkStream.DataAvailable)
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        xml = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void ReceiveLoop()
        {
            Log("EKI 接收线程已启动");

            while (_isConnected)
            {
                try
                {
                    if (_networkStream != null && _networkStream.DataAvailable)
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string xml = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                            EventHandler<EKIDataEventArgs> handler = DataReceived;
                            if (handler != null)
                            {
                                handler(this, new EKIDataEventArgs
                                {
                                    XmlData = xml,
                                    Timestamp = DateTime.Now
                                });
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (IOException)
                {
                    if (_isConnected)
                    {
                        Log("EKI 连接已断开", MessageLevel.Warning);
                        _isConnected = false;
                        OnConnectionStateChanged(false);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    if (_isConnected)
                    {
                        Log(string.Format("EKI 接收线程异常 原因 {0}", ex.Message), MessageLevel.Error);
                        OnErrorOccurred("接收线程异常", ex);
                    }
                }
            }

            Log("EKI 接收线程已退出");
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            EventHandler<EKIConnectionEventArgs> handler = ConnectionStateChanged;
            if (handler != null)
            {
                handler(this, new EKIConnectionEventArgs
                {
                    IsConnected = isConnected,
                    ConnectionName = ConnectionName,
                    Timestamp = DateTime.Now
                });
            }
        }

        private void OnErrorOccurred(string message, Exception ex)
        {
            EventHandler<EKIErrorEventArgs> handler = ErrorOccurred;
            if (handler != null)
            {
                handler(this, new EKIErrorEventArgs
                {
                    ErrorMessage = message,
                    Exception = ex,
                    ConnectionName = ConnectionName
                });
            }
        }
        #endregion

        #region 公共函数
        /// <summary>从 XML 文件初始化 EKI 配置</summary>
        /// <param name="xmlConfigPath">EKI XML 配置文件路径</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool Initialize(string xmlConfigPath)
        {
            try
            {
                _config = KUKAXMLConfig.LoadEKIConfig(xmlConfigPath);
                ServerIP = _config.ExternalIP;
                ServerPort = _config.ExternalPort;
                Mode = _config.ExternalType;
                Environment = _config.Environment;
                Log(string.Format("EKI 配置已加载 IP {0} 端口 {1} 模式 {2}", ServerIP, ServerPort, Mode));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("EKI 配置加载失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("配置加载失败", ex);
                return false;
            }
        }

        /// <summary>从配置对象初始化 EKI</summary>
        /// <param name="config">EKI 配置对象</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool Initialize(EKIConfig config)
        {
            try
            {
                _config = config;
                ServerIP = config.ExternalIP;
                ServerPort = config.ExternalPort;
                Mode = config.ExternalType;
                Environment = config.Environment;
                Log(string.Format("EKI 配置已加载 IP {0} 端口 {1} 模式 {2}", ServerIP, ServerPort, Mode));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("EKI 配置加载失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("配置加载失败", ex);
                return false;
            }
        }

        /// <summary>打开 TCP 连接</summary>
        /// <returns>true 成功 / false 失败</returns>
        public bool Open()
        {
            lock (_connectLock)
            {
                try
                {
                    if (_isConnected)
                    {
                        Log("EKI 已连接");
                        return true;
                    }

                    _tcpClient = new TcpClient();
                    _tcpClient.Connect(ServerIP, ServerPort);
                    _networkStream = _tcpClient.GetStream();
                    _isConnected = true;

                    _receiveThread = new Thread(ReceiveLoop);
                    _receiveThread.IsBackground = true;
                    _receiveThread.Start();

                    Log(string.Format("EKI 已连接 {0} 端口 {1}", ServerIP, ServerPort));
                    OnConnectionStateChanged(true);
                    return true;
                }
                catch (Exception ex)
                {
                    Log(string.Format("EKI 连接失败 原因是 {0}", ex.Message), MessageLevel.Error);
                    OnErrorOccurred("连接失败", ex);
                    return false;
                }
            }
        }

        /// <summary>关闭 TCP 连接</summary>
        public void Close()
        {
            lock (_connectLock)
            {
                try
                {
                    _isConnected = false;

                    if (_receiveThread != null && _receiveThread.IsAlive)
                    {
                        _receiveThread.Join(1000);
                        if (_receiveThread.IsAlive)
                        {
                            _receiveThread.Abort();
                        }
                        _receiveThread = null;
                    }

                    if (_networkStream != null)
                    {
                        _networkStream.Close();
                        _networkStream = null;
                    }

                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                        _tcpClient = null;
                    }

                    Log("EKI 已关闭");
                    OnConnectionStateChanged(false);
                }
                catch (Exception ex)
                {
                    Log(string.Format("EKI 关闭异常 原因 {0}", ex.Message), MessageLevel.Error);
                }
            }
        }

        /// <summary>清除连接与配置</summary>
        public void Clear()
        {
            Close();
            _config = null;
            Log("EKI 连接已清除");
        }

        /// <summary>发送 XML 元素</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool Send(string elementPath)
        {
            if (!_isConnected || _networkStream == null)
            {
                Log("EKI 未连接，无法发送", MessageLevel.Warning);
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                string[] parts = elementPath.Split('/');
                XmlElement current = doc.CreateElement(parts[0]);
                doc.AppendChild(current);

                for (int i = 1; i < parts.Length; i++)
                {
                    XmlElement child = doc.CreateElement(parts[i]);
                    current.AppendChild(child);
                    current = child;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(doc.OuterXml);
                _networkStream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("EKI 发送失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("发送失败", ex);
                return false;
            }
        }

        /// <summary>发送原始字节</summary>
        /// <param name="data">待发送原始字节</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SendRaw(byte[] data)
        {
            if (!_isConnected || _networkStream == null)
            {
                Log("EKI 未连接，无法发送", MessageLevel.Warning);
                return false;
            }

            try
            {
                _networkStream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("EKI 发送原始数据失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("发送原始数据失败", ex);
                return false;
            }
        }

        /// <summary>发送 XML 字符串</summary>
        /// <param name="xmlString">待发送 XML 字符串</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SendString(string xmlString)
        {
            if (!_isConnected || _networkStream == null)
            {
                Log("EKI 未连接，无法发送", MessageLevel.Warning);
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(xmlString);
                _networkStream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("EKI 发送字符串失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("发送字符串失败", ex);
                return false;
            }
        }

        /// <summary>写入 REAL 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">REAL 数值</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SetReal(string elementPath, double value)
        {
            return SendString(string.Format("<{0}>{1}</{0}>", elementPath.Replace("/", "><"), value.ToString("F6")));
        }

        /// <summary>写入 INT 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">整数值</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SetInt(string elementPath, int value)
        {
            return SendString(string.Format("<{0}>{1}</{0}>", elementPath.Replace("/", "><"), value));
        }

        /// <summary>写入 BOOL 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">布尔值</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SetBool(string elementPath, bool value)
        {
            return SendString(string.Format("<{0}>{1}</{0}>", elementPath.Replace("/", "><"), value ? "true" : "false"));
        }

        /// <summary>写入 FRAME 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="frame">KUKA 位姿帧</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SetFrame(string elementPath, KUKAFrame frame)
        {
            string xml = string.Format(
                "<{0} X=\"{1:F4}\" Y=\"{2:F4}\" Z=\"{3:F4}\" A=\"{4:F4}\" B=\"{5:F4}\" C=\"{6:F4}\"/>",
                elementPath.Replace("/", "><"),
                frame.X, frame.Y, frame.Z, frame.A, frame.B, frame.C);
            return SendString(xml);
        }

        /// <summary>写入 STRING 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">字符串值</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool SetString(string elementPath, string value)
        {
            return SendString(string.Format("<{0}>{1}</{0}>", elementPath.Replace("/", "><"), value));
        }

        /// <summary>读取 BOOL 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">输出解析结果</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool GetBool(string elementPath, out bool value)
        {
            value = false;
            string xml;
            if (!TryReadXml(out xml))
            {
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNode node = doc.SelectSingleNode("//" + elementPath.Replace('/', '/'));
                if (node != null)
                {
                    value = bool.Parse(node.InnerText);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>读取 INT 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">输出解析结果</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool GetInt(string elementPath, out int value)
        {
            value = 0;
            string xml;
            if (!TryReadXml(out xml))
            {
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNode node = doc.SelectSingleNode("//" + elementPath.Replace('/', '/'));
                if (node != null)
                {
                    value = int.Parse(node.InnerText);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>读取 REAL 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">输出解析结果</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool GetReal(string elementPath, out double value)
        {
            value = 0;
            string xml;
            if (!TryReadXml(out xml))
            {
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNode node = doc.SelectSingleNode("//" + elementPath.Replace('/', '/'));
                if (node != null)
                {
                    value = double.Parse(node.InnerText);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>读取 STRING 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="value">输出解析结果</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool GetString(string elementPath, out string value)
        {
            value = string.Empty;
            string xml;
            if (!TryReadXml(out xml))
            {
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNode node = doc.SelectSingleNode("//" + elementPath.Replace('/', '/'));
                if (node != null)
                {
                    value = node.InnerText;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>读取 FRAME 值</summary>
        /// <param name="elementPath">XML 元素路径</param>
        /// <param name="frame">输出解析位姿帧</param>
        /// <returns>true 成功 / false 失败</returns>
        public bool GetFrame(string elementPath, out KUKAFrame frame)
        {
            frame = new KUKAFrame();
            string xml;
            if (!TryReadXml(out xml))
            {
                return false;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNode node = doc.SelectSingleNode("//" + elementPath.Replace('/', '/'));
                if (node != null)
                {
                    if (node.Attributes["X"] != null) frame.X = double.Parse(node.Attributes["X"].Value);
                    if (node.Attributes["Y"] != null) frame.Y = double.Parse(node.Attributes["Y"].Value);
                    if (node.Attributes["Z"] != null) frame.Z = double.Parse(node.Attributes["Z"].Value);
                    if (node.Attributes["A"] != null) frame.A = double.Parse(node.Attributes["A"].Value);
                    if (node.Attributes["B"] != null) frame.B = double.Parse(node.Attributes["B"].Value);
                    if (node.Attributes["C"] != null) frame.C = double.Parse(node.Attributes["C"].Value);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>释放资源</summary>
        public void Dispose()
        {
            Clear();
        }
        #endregion
    }
}
