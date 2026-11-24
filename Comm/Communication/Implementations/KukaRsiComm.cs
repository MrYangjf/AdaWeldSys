using System;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.Comm.Communication.Abstractions;

namespace AdaWeldSystem.Comm.Communication.Implementations
{
    /// <summary>RSI（Robot Sensor Interface）UDP 实时通讯实现</summary>
    public class KukaRsiComm : CommChannelBase
    {
        /// <summary>连续 N 周期无包触发告警</summary>
        private const int StallWarnCycles = 5;

        private UdpClient _udpClient;
        private IPEndPoint _robotEndPoint;
        private Thread _receiveThread;
        private Thread _sendThread;
        private bool _isRunning;
        private readonly object _replyLock = new object();

        /// <summary>收包信号，回复线程一包一回</summary>
        private readonly AutoResetEvent _replySignal = new AutoResetEvent(false);
        private RsiRobotPacket _latestPacket;
        private double _laserPower;
        private double _wireSpeed;

        /// <summary>剪丝请求信号，对应 CutWire</summary>
        private double _cutWireSignal;

        /// <summary>焊接速度（mm/s），对应具名元素 WeldSpeed。</summary>
        private double _weldSpeed;

        /// <summary>振镜摆幅（mm），对应具名元素 WeaveAmp。</summary>
        private double _weaveAmp;

        /// <summary>下行指令码（TX_* 数值码，0=无指令），对应具名元素 TxCmd。</summary>
        private int _txCmd;

        /// <summary>下行指令载荷（预留，如坐标值），对应具名元素 TxPayload。</summary>
        private double _txPayload;

        /// <summary>下行指令序号，对应 TxSeq</summary>
        private int _txSeq;

        /// <summary>已发出的下行指令序号（自增计数，用于生成 TxSeq）。</summary>
        private int _txSeqCounter;

        /// <summary>收到的机器人包计数（调试面板展示）。</summary>
        private long _rxPacketCount;

        /// <summary>发出的回复包计数（调试面板展示）。</summary>
        private long _txPacketCount;

        /// <summary>收到的有效包原文（调试面板展示最近一帧）。</summary>
        private string _lastRxRaw = string.Empty;

        /// <summary>发出的回复包原文（调试面板展示最近一帧）。</summary>
        private string _lastTxRaw = string.Empty;

        private RSIConfig _config;
        private DateTime _lastReceiveUtc;
        private bool _stallWarned;

        /// <summary>通道名称</summary>
        public override string Name
        {
            get { return "KUKA_RSI"; }
        }

        /// <summary>是否已连接（映射 RSI 运行态）</summary>
        public override bool IsConnected
        {
            get { return _isRunning; }
        }

        /// <summary>获取 RSI 是否正在运行</summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        /// <summary>机器人 IP（回复发往收包远端）</summary>
        public string RobotIP { get; set; }

        /// <summary>获取或设置机器人端口号（参考信息）</summary>
        public int RobotPort { get; set; }

        /// <summary>本机 UDP 监听端口，须与机器人一致</summary>
        public int LocalPort { get; set; }

        /// <summary>本机监听 IP（对应 RobotServer.xml HOST_IP；必须已分配到本机已启用网口）</summary>
        public string LocalIp { get; set; }

        /// <summary>最近一次启动/停止失败原因</summary>
        public string LastError { get; private set; }

        /// <summary>传感器系统标识（须与机器人端 XML 配置的 SENTYPE 一致）</summary>
        public string SenType { get; set; }

        /// <summary>RSI 通讯周期（毫秒）：IPO 12 / IPO_FAST 4，用于接收超时判定</summary>
        public int CycleMs { get; set; }

        /// <summary>机器人发送格式（解析依据，null 用内置默认）</summary>
        public RsiPacketFormat SendPacketFormat { get; set; }

        /// <summary>上位机回复格式（组包依据，null 用内置默认）</summary>
        public RsiPacketFormat ReplyPacketFormat { get; set; }

        /// <summary>最近机器人数据包，无数据为 null</summary>
        public RsiRobotPacket LatestPacket
        {
            get
            {
                lock (_replyLock)
                {
                    return _latestPacket;
                }
            }
        }

        /// <summary>RSI 数据接收事件（机器人坐标包到达时触发）</summary>
        public event EventHandler<RSIDataEventArgs> RsiDataReceived;

        /// <summary>RSI 错误事件</summary>
        public event EventHandler<RSIErrorEventArgs> ErrorOccurred;

        /// <summary>收到的机器人包计数</summary>
        public long RxPacketCount
        {
            get { lock (_replyLock) { return _rxPacketCount; } }
        }

        /// <summary>发出的回复包计数（调试面板展示；线程安全读取）</summary>
        public long TxPacketCount
        {
            get { lock (_replyLock) { return _txPacketCount; } }
        }

        /// <summary>最近一帧收到的机器人包原文</summary>
        public string LastRxRaw
        {
            get { lock (_replyLock) { return _lastRxRaw; } }
        }

        /// <summary>最近一帧发出的回复包原文</summary>
        public string LastTxRaw
        {
            get { lock (_replyLock) { return _lastTxRaw; } }
        }

        /// <summary>离线判定超时（毫秒）：5 个通讯周期且不小于 500ms。</summary>
        public int OfflineTimeoutMs
        {
            get { return Math.Max(CycleMs * 5, 500); }
        }

        /// <summary>最近一次收到有效机器人包的时间</summary>
        public DateTime LastReceiveTime
        {
            get
            {
                DateTime utc = _lastReceiveUtc;
                return utc == DateTime.MinValue ? DateTime.MinValue : utc.ToLocalTime();
            }
        }

        /// <summary>在线判据：监听中且有新包</summary>
        public bool IsRobotOnline
        {
            get
            {
                if (!_isRunning) return false;
                long count;
                lock (_replyLock) { count = _rxPacketCount; }
                if (count <= 0) return false;
                DateTime utc = _lastReceiveUtc;
                if (utc == DateTime.MinValue) return false;
                return (DateTime.UtcNow - utc).TotalMilliseconds <= OfflineTimeoutMs;
            }
        }

        /// <summary>最近一帧收到的上行指令码（RxCmd；0=无指令）</summary>
        public int LastRxCmd
        {
            get { lock (_replyLock) { return _latestPacket != null ? _latestPacket.RxCmd : 0; } }
        }

        /// <summary>最近一帧收到的上行指令载荷（RxPayload）</summary>
        public double LastRxPayload
        {
            get { lock (_replyLock) { return _latestPacket != null ? _latestPacket.RxPayload : 0.0; } }
        }

        /// <summary>最近一帧的 IPOC 时间戳</summary>
        public string LastIpoc
        {
            get { lock (_replyLock) { return _latestPacket != null ? _latestPacket.Ipoc : string.Empty; } }
        }

        /// <summary>默认构造函数</summary>
        public KukaRsiComm()
            : this("192.168.10.100", 49152, 49152)
        {
        }

        /// <summary>带参数的构造函数</summary>
        public KukaRsiComm(string robotIP, int robotPort, int localPort)
            : base("KukaRsiComm")
        {
            RobotIP = robotIP;
            RobotPort = robotPort;
            LocalPort = localPort;
            LocalIp = "192.168.10.50";
            SenType = "WeldControl";
            CycleMs = 12;
        }

        /// <summary>校验本机是否存在承载指定 IPv4 的已启用物理/虚拟</summary>

        /// <param name="address">待绑定本机 IP</param>
        /// <param name="nicName">命中网口名称</param>
        /// <param name="reason">失败原因（本机未找到该 IP / 网口未就绪）</param>
        /// <returns>存在且网口 Up 返回 true</returns>
        private bool IsLocalAddressReady(IPAddress address, out string nicName, out string reason)
        {
            nicName = string.Empty;
            bool foundButDown = false;
            string downReason = string.Empty;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                IPInterfaceProperties props;
                try { props = ni.GetIPProperties(); }
                catch { continue; }

                foreach (UnicastIPAddressInformation ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (!ua.Address.Equals(address)) continue;

                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        nicName = ni.Name;
                        reason = string.Empty;
                        return true;
                    }
                    foundButDown = true;
                    downReason = string.Format("通讯网口 {0} 未打开", ni.Name);
                }
            }

            reason = foundButDown ? downReason : "本机未找到该通讯 IP";
            return false;
        }

        /// <summary>占位值解析：按占位名取实时值</summary>

        /// <param name="placeholder">占位名（详见 wiki 占位符表）</param>
        /// <param name="laserPower">激光功率（W）</param>
        /// <param name="wireSpeed">送丝速度（m/min）</param>
        /// <param name="ipoc">IPOC 时间戳</param>
        /// <param name="cutWireSignal">剪丝信号</param>
        /// <param name="weldSpeed">焊接速度（mm/s）</param>
        /// <param name="weaveAmp">振镜摆幅（mm）</param>
        /// <param name="txCmd">下行指令码</param>
        /// <param name="txPayload">下行指令载荷</param>
        /// <param name="txSeq">下行指令序号</param>
        /// <returns>占位值替换结果</returns>
        private static string ResolvePlaceholder(string placeholder, double laserPower, double wireSpeed,
            string ipoc, double cutWireSignal = 0.0, double weldSpeed = 0.0, double weaveAmp = 0.0,
            int txCmd = 0, double txPayload = 0.0, int txSeq = 0)
        {
            if (placeholder == "激光功率")
            {
                return laserPower.ToString("0.0", CultureInfo.InvariantCulture);
            }
            if (placeholder == "送丝速度")
            {
                return wireSpeed.ToString("0.0", CultureInfo.InvariantCulture);
            }
            if (placeholder == "剪丝信号")
            {
                return cutWireSignal != 0.0 ? "1" : "0";
            }
            if (placeholder == "焊接速度")
            {
                return weldSpeed.ToString("0.0", CultureInfo.InvariantCulture);
            }
            if (placeholder == "振镜摆幅")
            {
                return weaveAmp.ToString("0.0", CultureInfo.InvariantCulture);
            }
            if (placeholder == "TX指令码")
            {
                return txCmd.ToString(CultureInfo.InvariantCulture);
            }
            if (placeholder == "TX指令载荷")
            {
                return txPayload.ToString("0.0", CultureInfo.InvariantCulture);
            }
            if (placeholder == "TX指令序号")
            {
                return txSeq.ToString(CultureInfo.InvariantCulture);
            }
            if (placeholder == "原样回传" || placeholder == "时间戳")
            {
                return ipoc;
            }
            return placeholder ?? string.Empty;
        }

        /// <summary>按格式属性序号取元素属性文本，越界返回空串</summary>
        /// <param name="index">索引。</param>
        /// <returns>结果文本。</returns>
        private static string GetAttrByIndex(XmlNode node, List<RsiFormatAttribute> attributes, int index)
        {
            if (attributes == null || index < 0 || index >= attributes.Count)
            {
                return string.Empty;
            }
            XmlAttribute attr = node.Attributes?[attributes[index].Name];
            return attr != null ? attr.Value : string.Empty;
        }

        /// <summary>接收循环线程：收包 解析 记录远端端点 触发事件</summary>
        private void ReceiveLoop()
        {
            GlobalCommData.ShowCommLogForView(Tag, "RSI 接收线程已启动");

            while (_isRunning)
            {
                try
                {
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _udpClient.Receive(ref remoteEndPoint);
                    string xml = Encoding.UTF8.GetString(bytes);

                    // 按发送格式文档解析，缺失回退默认
                    RsiRobotPacket packet = ParseRobotPacket(xml, SendPacketFormat);
                    if (packet == null)
                    {
                        continue;
                    }

                    // 回复发往收包远端端点
                    _robotEndPoint = remoteEndPoint;

                    // 收到有效包，复位丢失判定状态
                    _lastReceiveUtc = DateTime.UtcNow;
                    if (_stallWarned)
                    {
                        _stallWarned = false;
                        GlobalCommData.ShowCommLogForView(Tag, "RSI 数据流已恢复");
                    }

                    lock (_replyLock)
                    {
                        _latestPacket = packet;
                        _rxPacketCount++;
                        _lastRxRaw = xml;
                    }

                    // 置位收包信号，回复线程取快照组包
                    _replySignal.Set();

                    RSIDataEventArgs args = new RSIDataEventArgs
                    {
                        RobotPacket = packet,
                        RobotData = new KUKARobotData { RIst = packet.RIst },
                        Timestamp = packet.ReceiveTime
                    };
                    RsiDataReceived?.Invoke(this, args);
                }
                catch (SocketException ex)
                {
                    if (_isRunning)
                    {
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            // 2 周期无包：转周期丢失判定
                            CheckReceiveStall();
                        }
                        else
                        {
                            Log(string.Format("RSI 接收线程 Socket 异常 原因 {0}", ex.Message), MessageLevel.Error);
                        }
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

            GlobalCommData.ShowCommLogForView(Tag, "RSI 接收线程已退出");
        }

        /// <summary>接收超时判定：连续 N 周期未收包则告警一次，恢复</summary>
        private void CheckReceiveStall()
        {
            if (_latestPacket == null)
            {
                return;
            }
            if ((DateTime.UtcNow - _lastReceiveUtc).TotalMilliseconds < CycleMs * StallWarnCycles)
            {
                return;
            }
            if (_stallWarned)
            {
                return;
            }
            _stallWarned = true;
            GlobalCommData.ShowCommLogForView(Tag, string.Format("RSI 接收超时 连续 {0} 个周期未收到机器人数据包 通讯周期 {1} ms", StallWarnCycles, CycleMs));
        }

        /// <summary>回复循环线程：与接收线程解耦，超时用上一帧有</summary>
        private void SendLoop()
        {
            GlobalCommData.ShowCommLogForView(Tag, "RSI 回复线程已启动");

            while (_isRunning)
            {
                try
                {
                    // 等下一帧包，回复须与收包同周期
                    int waitMs = CycleMs > 0 ? CycleMs * 5 : 100;
                    if (!_replySignal.WaitOne(waitMs))
                    {
                        continue;
                    }
                    if (!_isRunning)
                    {
                        break;
                    }

                    RsiRobotPacket packet;
                    double laserPower;
                    double wireSpeed;
                    double cutWireSignal;
                    double weldSpeed;
                    double weaveAmp;
                    int txCmd;
                    double txPayload;
                    int txSeq;
                    lock (_replyLock)
                    {
                        packet = _latestPacket;
                        laserPower = _laserPower;
                        wireSpeed = _wireSpeed;
                        cutWireSignal = _cutWireSignal;
                        weldSpeed = _weldSpeed;
                        weaveAmp = _weaveAmp;
                        txCmd = _txCmd;
                        txPayload = _txPayload;
                        txSeq = _txSeq;
                    }

                    if (packet == null || _udpClient == null || _robotEndPoint == null)
                    {
                        continue;
                    }

                    byte[] reply = BuildReplyXml(ReplyPacketFormat, laserPower, wireSpeed, packet.Ipoc, SenType,
                        cutWireSignal, weldSpeed, weaveAmp, txCmd, txPayload, txSeq);
                    _udpClient.Send(reply, reply.Length, _robotEndPoint);

                    lock (_replyLock)
                    {
                        _txPacketCount++;
                        _lastTxRaw = Encoding.UTF8.GetString(reply);
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log(string.Format("RSI 回复线程异常 原因 {0}", ex.Message), MessageLevel.Error);
                        OnErrorOccurred("回复发送失败", ex, RSIErrorType.NetworkTimeout);
                    }
                }
            }

            GlobalCommData.ShowCommLogForView(Tag, "RSI 回复线程已退出");
        }

        /// <summary>不变文化解析双精度，失败返回 0</summary>
        /// <param name="value">值。</param>
        /// <returns>计算结果。</returns>
        private static double ParseDouble(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : 0.0;
        }

        /// <summary>触发错误事件</summary>
        /// <param name="message">消息内容。</param>
        private void OnErrorOccurred(string message, Exception ex, RSIErrorType errorType)
        {
            ErrorOccurred?.Invoke(this, new RSIErrorEventArgs
            {
                ErrorMessage = message,
                Exception = ex,
                ErrorType = errorType
            });
        }

        /// <summary>初始化 RSI 配置（从机器人端 XML 配置文件装载）</summary>
        /// <returns>是否成功。</returns>
        public bool Initialize(string xmlConfigPath)
        {
            try
            {
                return Initialize(KUKAXMLConfig.LoadRSIConfig(xmlConfigPath));
            }
            catch (Exception ex)
            {
                Log(string.Format("RSI 配置加载失败 原因是 {0}", ex.Message), MessageLevel.Error);
                OnErrorOccurred("配置加载失败", ex, RSIErrorType.ConnectionFailed);
                return false;
            }
        }

        /// <summary>使用配置对象初始化</summary>
        /// <param name="config">配置对象。</param>
        /// <returns>是否成功。</returns>
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

        /// <summary>建立连接（启动 RSI 通讯）</summary>
        public override void Connect()
        {
            Start();
        }

        /// <summary>断开连接（停止 RSI 通讯）</summary>
        public override void Disconnect()
        {
            Stop();
        }

        /// <summary>启动 RSI 通讯（接收线程 + 回复线程）</summary>
        /// <returns>是否成功。</returns>
        public bool Start()
        {
            try
            {
                if (_isRunning)
                {
                    GlobalCommData.ShowCommLogForView(Tag, "RSI 已在运行中");
                    return true;
                }

                LastError = string.Empty;
                // 绑定具体网口，无网口即失败
                if (string.IsNullOrEmpty(LocalIp) || !IPAddress.TryParse(LocalIp, out IPAddress localAddress))
                {
                    LastError = "HOST_IP 未配置或格式非法 当前值 " + LocalIp;
                    Log("RSI 监听启动失败 " + LastError, MessageLevel.Debug);
                    OnErrorOccurred("监听 IP 非法", null, RSIErrorType.ConnectionFailed);
                    return false;
                }

                if (!IsLocalAddressReady(localAddress, out string nicName, out string nicReason))
                {
                    LastError = nicReason;
                    Log("RSI 监听启动失败 " + LastError, MessageLevel.Debug);
                    OnErrorOccurred("监听 IP 无对应网口", null, RSIErrorType.ConnectionFailed);
                    return false;
                }

                try
                {
                    IPEndPoint localEndPoint = new IPEndPoint(localAddress, LocalPort);
                    _udpClient = new UdpClient(localEndPoint);
                }
                catch (SocketException ex)
                {
                    LastError = string.Format("监听绑定失败 地址 {0}:{1} 错误码 {2} 原因 {3}",
                        LocalIp, LocalPort, ex.SocketErrorCode, ex.Message);
                    Log("RSI " + LastError, MessageLevel.Debug);
                    OnErrorOccurred("监听绑定失败", ex, RSIErrorType.ConnectionFailed);
                    return false;
                }
                _udpClient.Client.ReceiveBufferSize = 65536;
                _udpClient.Client.SendBufferSize = 65536;
                // 超时取 2 周期，转入丢失判定
                _udpClient.Client.ReceiveTimeout = CycleMs * 2;
                _robotEndPoint = null;
                _lastReceiveUtc = DateTime.UtcNow;
                _stallWarned = false;
                _replySignal.Reset();
                lock (_replyLock)
                {
                    // 启动清零统计与最近包原文
                    _latestPacket = null;
                    _rxPacketCount = 0;
                    _txPacketCount = 0;
                    _lastRxRaw = string.Empty;
                    _lastTxRaw = string.Empty;
                }

                _isRunning = true;

                _receiveThread = new Thread(ReceiveLoop)
                {
                    Name = "RSI-Receive",
                    IsBackground = true,
                    Priority = ThreadPriority.Highest
                };
                _receiveThread.Start();

                _sendThread = new Thread(SendLoop)
                {
                    Name = "RSI-Send",
                    IsBackground = true,
                    Priority = ThreadPriority.Highest
                };
                _sendThread.Start();

                GlobalCommData.ShowCommLogForView(Tag, string.Format("RSI 监听已绑定 {0}:{1} 网口 {2} 机器人 {3}:{4} 传感器标识 {5}",
                    LocalIp, LocalPort, nicName, RobotIP, RobotPort, SenType));
                return true;
            }
            catch (Exception ex)
            {
                LastError = "启动失败 原因 " + ex.Message;
                Log("RSI " + LastError, MessageLevel.Debug);
                OnErrorOccurred("启动失败", ex, RSIErrorType.ConnectionFailed);
                return false;
            }
        }

        /// <summary>停止 RSI 通讯</summary>
        /// <returns>是否成功。</returns>
        public bool Stop()
        {
            try
            {
                LastError = string.Empty;
                _isRunning = false;
                // 唤醒回复线程，使其尽快退出
                _replySignal.Set();

                if (_receiveThread != null && _receiveThread.IsAlive)
                {
                    _receiveThread.Join(1000);
                    _receiveThread = null;
                }
                if (_sendThread != null && _sendThread.IsAlive)
                {
                    _sendThread.Join(1000);
                    _sendThread = null;
                }

                _udpClient?.Close();
                _udpClient = null;

                lock (_replyLock)
                {
                    _latestPacket = null;
                }

                GlobalCommData.ShowCommLogForView(Tag, "RSI 已停止");
                return true;
            }
            catch (Exception ex)
            {
                LastError = "停止异常 原因 " + ex.Message;
                Log("RSI " + LastError, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>写入回复给机器人的焊接参数（5 个具名元素通道）</summary>
        /// <param name="laserPower">激光功率（W）→ WeldPower</param>
        /// <param name="wireSpeed">送丝速度（m/min）→ WireSpeed</param>
        /// <param name="cutWireSignal">剪丝信号（1=请求机器人执行剪丝程序）→ CutWire</param>
        /// <param name="weldSpeed">焊接速度（mm/s）→ WeldSpeed</param>
        /// <param name="weaveAmp">振镜摆幅（mm）→ WeaveAmp</param>
        public void SetReplyValues(double laserPower, double wireSpeed, double cutWireSignal = 0.0,
            double weldSpeed = 0.0, double weaveAmp = 0.0)
        {
            lock (_replyLock)
            {
                _laserPower = laserPower;
                _wireSpeed = wireSpeed;
                _cutWireSignal = cutWireSignal;
                _weldSpeed = weldSpeed;
                _weaveAmp = weaveAmp;
            }
        }

        /// <summary>下发下行指令（TxCmd + TxPayload + TxSeq 三件套）</summary>

        /// <param name="txCmd">下行指令码（CommandCore.Tx* 对应的数值码）</param>
        /// <param name="txPayload">下行指令载荷（无载荷置 0）</param>
        public void SendTxCommand(int txCmd, double txPayload = 0.0)
        {
            lock (_replyLock)
            {
                _txCmd = txCmd;
                _txPayload = txPayload;
                _txSeqCounter++;
                _txSeq = _txSeqCounter;
            }
        }

        /// <summary>清零下行指令码（机器人已执行，或上位机主动撤</summary>
        public void ClearTxCommand()
        {
            lock (_replyLock)
            {
                _txCmd = 0;
                _txPayload = 0.0;
            }
        }

        /// <summary>解析机器人发送的 XML 数据包（内置默认格式：Rob 包 RIst + Tool + IPOC）</summary>
        /// <param name="xml">XML 文本</param>
        /// <returns>解析结果，格式错误返回 null</returns>
        public static RsiRobotPacket ParseRobotPacket(string xml)
        {
            return ParseRobotPacket(xml, null);
        }

        /// <summary>按发送格式文档解析机器人 XML 数据包（格式驱动：读</summary>

        /// <param name="xml">XML 文本</param>
        /// <param name="sendFormat">机器人发送格式（null 时用内置默认）</param>
        /// <returns>解析结果，RIst 主定位元素缺失返回 null</returns>
        public static RsiRobotPacket ParseRobotPacket(string xml, RsiPacketFormat sendFormat)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return null;
            }
            if (sendFormat == null)
            {
                sendFormat = RsiPacketFormat.CreateDefaultSendFormat();
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                RsiRobotPacket packet = new RsiRobotPacket
                {
                    ReceiveTime = DateTime.Now
                };

                bool ristFound = false;
                foreach (RsiFormatElement elem in sendFormat.Elements)
                {
                    XmlNode node = doc.SelectSingleNode("//" + elem.Tag);
                    if (node == null)
                    {
                        continue;
                    }

                    if (elem.IsTextNode)
                    {
                        // 文本节点按元素名分派语义
                        // （否则 RxCmd/RxPayload 会被误写成 Ipoc，且数值解析分支不可达）
                        if (string.Equals(elem.Tag, "IPOC", StringComparison.OrdinalIgnoreCase))
                        {
                            packet.Ipoc = node.InnerText;
                        }
                        else if (string.Equals(elem.Tag, "RxCmd", StringComparison.OrdinalIgnoreCase))
                        {
                            packet.RxCmd = (int)ParseDouble(node.InnerText);
                        }
                        else if (string.Equals(elem.Tag, "RxPayload", StringComparison.OrdinalIgnoreCase))
                        {
                            packet.RxPayload = ParseDouble(node.InnerText);
                        }
                        continue;
                    }

                    if (string.Equals(elem.Tag, "RIst", StringComparison.OrdinalIgnoreCase))
                    {
                        // 笛卡尔实际位置：按格式属性顺序绑定 X/Y/Z/A/B/C
                        packet.RIst = new KUKAPosition(
                            ParseDouble(GetAttrByIndex(node, elem.Attributes, 0)),
                            ParseDouble(GetAttrByIndex(node, elem.Attributes, 1)),
                            ParseDouble(GetAttrByIndex(node, elem.Attributes, 2)),
                            ParseDouble(GetAttrByIndex(node, elem.Attributes, 3)),
                            ParseDouble(GetAttrByIndex(node, elem.Attributes, 4)),
                            ParseDouble(GetAttrByIndex(node, elem.Attributes, 5)));
                        ristFound = true;
                    }
                    else if (string.Equals(elem.Tag, "Tool", StringComparison.OrdinalIgnoreCase))
                    {
                        // 工具坐标：按格式属性顺序取前三项
                        packet.ToolX = ParseDouble(GetAttrByIndex(node, elem.Attributes, 0));
                        packet.ToolY = ParseDouble(GetAttrByIndex(node, elem.Attributes, 1));
                        packet.ToolZ = ParseDouble(GetAttrByIndex(node, elem.Attributes, 2));
                    }
                    else if (string.Equals(elem.Tag, "RxCmd", StringComparison.OrdinalIgnoreCase))
                    {
                        // 上行指令码（具名元素形式 <RxCmd>4</RxCmd>，LONG）
                        packet.RxCmd = (int)ParseDouble(node.InnerText);
                    }
                    else if (string.Equals(elem.Tag, "RxPayload", StringComparison.OrdinalIgnoreCase))
                    {
                        // 上行指令载荷（具名元素形式 <RxPayload>0</RxPayload>，DOUBLE）
                        packet.RxPayload = ParseDouble(node.InnerText);
                    }
                    // 其余元素忽略，扩展时在此绑定语义
                }

                if (!ristFound)
                {
                    return null;
                }
                return packet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>组装回复给机器人的 XML 数据包（内置默认格式：Sen 包具</summary>

        /// <param name="laserPower">激光功率（W）</param>
        /// <param name="wireSpeed">送丝速度（m/min）</param>
        /// <param name="ipoc">IPOC 时间戳（必须与收到包一致）</param>
        /// <param name="sensorType">传感器标识（须与机器人端 SENTYPE 一致）</param>
        /// <param name="cutWireSignal">剪丝信号（1=请求剪丝）</param>
        /// <param name="weldSpeed">焊接速度（mm/s）</param>
        /// <param name="weaveAmp">振镜摆幅（mm）</param>
        /// <param name="txCmd">下行指令码</param>
        /// <param name="txPayload">下行指令载荷</param>
        /// <param name="txSeq">下行指令序号</param>
        /// <returns>UTF8 编码的回复包字节</returns>
        public static byte[] BuildReplyXml(double laserPower, double wireSpeed, string ipoc, string sensorType,
            double cutWireSignal = 0.0, double weldSpeed = 0.0, double weaveAmp = 0.0,
            int txCmd = 0, double txPayload = 0.0, int txSeq = 0)
        {
            return BuildReplyXml(null, laserPower, wireSpeed, ipoc, sensorType, cutWireSignal,
                weldSpeed, weaveAmp, txCmd, txPayload, txSeq);
        }

        /// <summary>按回复格式文档组包（格式驱动，StringBuilder 拼接保实时）</summary>
        /// <param name="replyFormat">上位机回复格式（null 时用内置默认）</param>
        /// <param name="laserPower">激光功率（W）</param>
        /// <param name="wireSpeed">送丝速度（m/min）</param>
        /// <param name="ipoc">IPOC 时间戳（必须与收到包一致）</param>
        /// <param name="sensorType">传感器标识（格式根未带 Type 属性值时使用）</param>
        /// <param name="cutWireSignal">剪丝信号（1=请求剪丝，写入 T13）</param>
        /// <returns>UTF8 编码的回复包字节</returns>
        public static byte[] BuildReplyXml(RsiPacketFormat replyFormat, double laserPower, double wireSpeed,
            string ipoc, string sensorType, double cutWireSignal = 0.0, double weldSpeed = 0.0,
            double weaveAmp = 0.0, int txCmd = 0, double txPayload = 0.0, int txSeq = 0)
        {
            if (replyFormat == null)
            {
                replyFormat = RsiPacketFormat.CreateDefaultReplyFormat();
            }

            StringBuilder sb = new StringBuilder(256);
            sb.Append("<").Append(replyFormat.RootTag);
            string typeValue = string.IsNullOrEmpty(replyFormat.RootTypeValue) ? sensorType : replyFormat.RootTypeValue;
            if (!string.IsNullOrEmpty(typeValue))
            {
                sb.Append(" Type=\"").Append(typeValue).Append("\"");
            }
            sb.Append(">");

            foreach (RsiFormatElement elem in replyFormat.Elements)
            {
                if (elem.IsTextNode)
                {
                    string textPlaceholder = elem.Attributes.Count > 0 ? elem.Attributes[0].Placeholder : string.Empty;
                    sb.Append("<").Append(elem.Tag).Append(">");
                    sb.Append(ResolvePlaceholder(textPlaceholder, laserPower, wireSpeed, ipoc, cutWireSignal,
                        weldSpeed, weaveAmp, txCmd, txPayload, txSeq));
                    sb.Append("</").Append(elem.Tag).Append(">");
                    continue;
                }

                sb.Append("<").Append(elem.Tag);
                foreach (RsiFormatAttribute attr in elem.Attributes)
                {
                    sb.Append(" ").Append(attr.Name).Append("=\"")
                      .Append(ResolvePlaceholder(attr.Placeholder, laserPower, wireSpeed, ipoc, cutWireSignal,
                          weldSpeed, weaveAmp, txCmd, txPayload, txSeq)).Append("\"");
                }
                sb.Append(" />");
            }

            sb.Append("</").Append(replyFormat.RootTag).Append(">");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>释放资源</summary>
        public override void Dispose()
        {
            Stop();
            // 线程已 Join 退出，收包信号可安全释放
            _replySignal.Dispose();
        }
    }

    /// <summary>RSI 数据类型枚举</summary>
    public enum RSIDataType
    {
        BOOL,
        DOUBLE,
        LONG,
        STRING
    }

    /// <summary>RSI 错误类型枚举</summary>
    public enum RSIErrorType
    {
        ConnectionFailed,
        IPOMismatch,
        XMLSerializationError,
        XMLDeserializationError,
        NetworkTimeout,
        Unknown
    }

    /// <summary>笛卡尔位姿结构</summary>
    public struct KUKAPosition
    {
        public double X;
        public double Y;
        public double Z;
        public double A;
        public double B;
        public double C;

        public KUKAPosition(double x, double y, double z, double a, double b, double c)
        {
            X = x;
            Y = y;
            Z = z;
            A = a;
            B = b;
            C = c;
        }

        public override string ToString()
        {
            return string.Format("X={0:F3}, Y={1:F3}, Z={2:F3}, A={3:F3}, B={4:F3}, C={5:F3}", X, Y, Z, A, B, C);
        }
    }

    /// <summary>机器人发送的 RSI 数据包（解析后）</summary>
    public class RsiRobotPacket
    {
        /// <summary>笛卡尔实际位置</summary>
        public KUKAPosition RIst;

        /// <summary>工具 X，未发送 Tool 时为 0</summary>
        public double ToolX;

        /// <summary>工具 Y（语义同 ToolX）</summary>
        public double ToolY;

        /// <summary>工具 Z（语义同 ToolX）</summary>
        public double ToolZ;

        /// <summary>IPOC 时间戳（回复时必须原样回传）</summary>
        public string Ipoc;

        /// <summary>上行指令码（0=无指令；1~9 见 CommandCore 的 RX_* 数值码映射）</summary>
        public int RxCmd;

        /// <summary>上行指令载荷（当前仅 RX_WELD_STOP_POSE 使用：停止位 X / mm）</summary>
        public double RxPayload;

        /// <summary>本机接收时间戳（延迟统计用）</summary>
        public DateTime ReceiveTime;
    }

    /// <summary>机器人状态数据（EKI 指令通道与事件参数载体）</summary>
    public class KUKARobotData
    {
        public KUKAPosition RIst;
        public KUKAPosition RSol;
        public double[] AIPos;
        public double[] ASPos;
        public double[] EIPos;
        public double[] ESPos;
        public double[] MACur;
        public double[] MECur;
        public double[] TechC;
        public double[] TechT;
        public long Delay;
        public string EStr;
        public ulong IPOC;

        public KUKARobotData()
        {
            AIPos = new double[6];
            ASPos = new double[6];
            EIPos = new double[6];
            ESPos = new double[6];
            MACur = new double[6];
            MECur = new double[6];
            TechC = new double[6];
            TechT = new double[6];
            EStr = string.Empty;
        }
    }

    /// <summary>传感器修正数据（现行链路不填充，兼容保留）</summary>
    public class KUKACorrectionData
    {
        public KUKAPosition RKorr;
        public double[] AK;
        public double[] EK;
        public long DiO;
        public ulong IPOC;

        public KUKACorrectionData()
        {
            AK = new double[6];
            EK = new double[6];
        }
    }

    /// <summary>RSI 数据接收事件参数</summary>
    public class RSIDataEventArgs : EventArgs
    {
        /// <summary>机器人坐标包（现行数据链路）</summary>
        public RsiRobotPacket RobotPacket;

        /// <summary>机器人状态数据（RIst 已填充，供既有事件消费方兼容）</summary>
        public KUKARobotData RobotData;

        /// <summary>修正数据（历史语义，现行链路恒为 null）</summary>
        public KUKACorrectionData CorrectionData;

        public ulong IPOC;
        public DateTime Timestamp;
    }

    /// <summary>RSI 错误事件参数</summary>
    public class RSIErrorEventArgs : EventArgs
    {
        public string ErrorMessage;
        public Exception Exception;
        public RSIErrorType ErrorType;
    }

    /// <summary>RSI 格式属性（名称 + 占位值），由格式文档节解析而来</summary>
    public class RsiFormatAttribute
    {
        /// <summary>属性名（如 X / T11）</summary>
        public string Name;

        /// <summary>占位值：功率/送丝/回传/字面量</summary>
        public string Placeholder;

        /// <summary>构造格式属性</summary>
        /// <param name="name">属性名</param>
        /// <param name="placeholder">占位值</param>
        public RsiFormatAttribute(string name, string placeholder)
        {
            Name = name;
            Placeholder = placeholder;
        }
    }

    /// <summary>RSI 格式元素（数据包内一个节点的定义）</summary>
    public class RsiFormatElement
    {
        /// <summary>元素标签名（RIst / Tool / Tech / IPOC）</summary>
        public string Tag;

        /// <summary>属性序列（按格式文档顺序）</summary>
        public List<RsiFormatAttribute> Attributes;

        /// <summary>是否文本节点形式（无属性无子元素，如 IPOC）</summary>
        public bool IsTextNode;

        /// <summary>构造格式元素</summary>
        /// <param name="tag">元素标签名</param>
        public RsiFormatElement(string tag)
        {
            Tag = tag;
            Attributes = new List<RsiFormatAttribute>();
        }
    }

    /// <summary>RSI 数据包格式（收发组包与解析的唯一依据）</summary>
    public class RsiPacketFormat
    {
        /// <summary>包根标签（Rob / Sen）</summary>
        public string RootTag;

        /// <summary>包根类型属性值（KUKA / WeldControl；空串时组包用 SENTYPE）</summary>
        public string RootTypeValue;

        /// <summary>包内元素序列</summary>
        public List<RsiFormatElement> Elements;

        /// <summary>构造空格式</summary>
        public RsiPacketFormat()
        {
            Elements = new List<RsiFormatElement>();
        }

        /// <summary>构造文本节点形式的具名元素（形如 &lt;WeldPower&gt;0&lt;/WeldPower&gt;）</summary>
        /// <param name="tag">元素名</param>
        /// <param name="placeholder">占位名</param>
        /// <returns>格式元素</returns>
        private static RsiFormatElement CreateTextElement(string tag, string placeholder)
        {
            RsiFormatElement elem = new RsiFormatElement(tag)
            {
                IsTextNode = true
            };
            elem.Attributes.Add(new RsiFormatAttribute(string.Empty, placeholder));
            return elem;
        }

        /// <summary>内置默认机器人发送格式（Rob 包：RIst 六属性 + Tool 三属性 + RxCmd + RxPayload + IPOC 文本）</summary>

        /// <returns>默认发送格式</returns>
        public static RsiPacketFormat CreateDefaultSendFormat()
        {
            RsiPacketFormat format = new RsiPacketFormat
            {
                RootTag = "Rob",
                RootTypeValue = "KUKA"
            };

            RsiFormatElement rist = new RsiFormatElement("RIst");
            rist.Attributes.Add(new RsiFormatAttribute("X", ""));
            rist.Attributes.Add(new RsiFormatAttribute("Y", ""));
            rist.Attributes.Add(new RsiFormatAttribute("Z", ""));
            rist.Attributes.Add(new RsiFormatAttribute("A", ""));
            rist.Attributes.Add(new RsiFormatAttribute("B", ""));
            rist.Attributes.Add(new RsiFormatAttribute("C", ""));
            format.Elements.Add(rist);

            RsiFormatElement tool = new RsiFormatElement("Tool");
            tool.Attributes.Add(new RsiFormatAttribute("X", "工具X"));
            tool.Attributes.Add(new RsiFormatAttribute("Y", "工具Y"));
            tool.Attributes.Add(new RsiFormatAttribute("Z", "工具Z"));
            format.Elements.Add(tool);

            RsiFormatElement rxCmd = new RsiFormatElement("RxCmd")
            {
                IsTextNode = true
            };
            rxCmd.Attributes.Add(new RsiFormatAttribute("", "上行指令码"));
            format.Elements.Add(rxCmd);

            RsiFormatElement rxPayload = new RsiFormatElement("RxPayload")
            {
                IsTextNode = true
            };
            rxPayload.Attributes.Add(new RsiFormatAttribute("", "上行指令载荷"));
            format.Elements.Add(rxPayload);

            RsiFormatElement ipoc = new RsiFormatElement("IPOC")
            {
                IsTextNode = true
            };
            ipoc.Attributes.Add(new RsiFormatAttribute("", "时间戳"));
            format.Elements.Add(ipoc);
            return format;
        }

        /// <summary>内置默认回复格式（Sen 包：8 个具名元素 + IPOC 回传）</summary>

        /// <returns>默认回复格式</returns>
        public static RsiPacketFormat CreateDefaultReplyFormat()
        {
            RsiPacketFormat format = new RsiPacketFormat
            {
                RootTag = "Sen",
                RootTypeValue = ""
            };

            format.Elements.Add(CreateTextElement("WeldPower", "激光功率"));
            format.Elements.Add(CreateTextElement("WireSpeed", "送丝速度"));
            format.Elements.Add(CreateTextElement("CutWire", "剪丝信号"));
            format.Elements.Add(CreateTextElement("WeldSpeed", "焊接速度"));
            format.Elements.Add(CreateTextElement("WeaveAmp", "振镜摆幅"));
            format.Elements.Add(CreateTextElement("TxCmd", "TX指令码"));
            format.Elements.Add(CreateTextElement("TxPayload", "TX指令载荷"));
            format.Elements.Add(CreateTextElement("TxSeq", "TX指令序号"));

            RsiFormatElement ipoc = new RsiFormatElement("IPOC")
            {
                IsTextNode = true
            };
            ipoc.Attributes.Add(new RsiFormatAttribute("", "原样回传"));
            format.Elements.Add(ipoc);
            return format;
        }
    }


    public class RSIElement
    {
        public string Tag;
        public RSIDataType Type;
        public string Index;
        public int HoldOn;
    }

    public class RSIConfig
    {
        public string IPNumber;
        public int Port;
        public string SenType;
        public bool OnlySend;
        public List<RSIElement> SendElements;
        public List<RSIElement> ReceiveElements;

        public RSIConfig()
        {
            SendElements = new List<RSIElement>();
            ReceiveElements = new List<RSIElement>();
            IPNumber = "192.168.10.50";
            Port = 49152;
            SenType = "WeldControl";
            OnlySend = false;
        }
    }

    /// <summary>RSI XML 配置生成与解析（partial：RSI 半边）</summary>
    public static partial class KUKAXMLConfig
    {

        private const string Tag = "KUKAXMLConfig";

        public static string GenerateRSIXML(RSIConfig config)
        {
            var writer = new XdocumentReaderWriter();
            writer.NewXdocument("ROOT", "1.0", "UTF-8");

            var configElem = writer.AddElement("CONFIG");
            writer.AddElement(configElem, "IP_NUMBER", config.IPNumber);
            writer.AddElement(configElem, "PORT", config.Port.ToString());
            writer.AddElement(configElem, "SENTYPE", config.SenType);
            writer.AddElement(configElem, "ONLYSEND", config.OnlySend ? "TRUE" : "FALSE");

            if (config.SendElements != null && config.SendElements.Count > 0)
            {
                var send = writer.AddElement("SEND");
                var elements = writer.AddElement(send, "ELEMENTS");
                foreach (RSIElement elem in config.SendElements)
                {
                    var attrs = new Dictionary<string, string>
                    {
                        {"TAG", elem.Tag},
                        {"TYPE", elem.Type.ToString()},
                        {"INDX", elem.Index}
                    };
                    writer.AddElementWithAttributes(elements, "ELEMENT", attrs);
                }
            }

            if (config.ReceiveElements != null && config.ReceiveElements.Count > 0)
            {
                var receive = writer.AddElement("RECEIVE");
                var elements = writer.AddElement(receive, "ELEMENTS");
                foreach (RSIElement elem in config.ReceiveElements)
                {
                    var attrs = new Dictionary<string, string>
                    {
                        {"TAG", elem.Tag},
                        {"TYPE", elem.Type.ToString()},
                        {"INDX", elem.Index}
                    };
                    if (elem.HoldOn > 0)
                        attrs.Add("HOLDON", elem.HoldOn.ToString());
                    writer.AddElementWithAttributes(elements, "ELEMENT", attrs);
                }
            }

            // 注入格式注释，机器人端忽略
            string xml = writer.ToXmlString();
            xml = xml.Replace("<CONFIG>",
                "<CONFIG>\r\n    <!-- IP_NUMBER = 上位机本机 IP（机器人据此发包，须与机器人同一网段，仅末位不同）；PORT = 上位机监听端口 -->");
            xml = xml.Replace("<SEND>",
                "<SEND>\r\n    <!-- 机器人每周期发送（RSI 根据上方元素自动生成）：<Rob TYPE=\"KUKA\"><RIst X=\"\" Y=\"\" Z=\"\" A=\"\" B=\"\" C=\"\" /><Tool X=\"\" Y=\"\" Z=\"\" /><RxCmd>0</RxCmd><RxPayload>0</RxPayload><IPOC>时间戳</IPOC></Rob> -->");
            xml = xml.Replace("<RECEIVE>",
                "<RECEIVE>\r\n    <!-- 上位机每周期回复：<Sen Type=\"WeldControl\"><WeldPower>0</WeldPower><WireSpeed>0</WireSpeed><CutWire>0</CutWire><WeldSpeed>0</WeldSpeed><WeaveAmp>0</WeaveAmp><TxCmd>0</TxCmd><TxPayload>0</TxPayload><TxSeq>0</TxSeq><IPOC>原样回传</IPOC></Sen> -->");
            return xml;
        }

        public static RSIConfig ParseRSIXML(string xmlContent)
        {
            RSIConfig config = new RSIConfig();
            var writer = new XdocumentReaderWriter();
            writer.LoadFromXmlString(xmlContent);

            config.IPNumber = writer.GetElementValueByPath("CONFIG/IP_NUMBER", config.IPNumber);
            string portStr = writer.GetElementValueByPath("CONFIG/PORT");
            if (int.TryParse(portStr, out int port))
                config.Port = port;
            config.SenType = writer.GetElementValueByPath("CONFIG/SENTYPE", config.SenType);
            string onlySend = writer.GetElementValueByPath("CONFIG/ONLYSEND");
            if (!string.IsNullOrEmpty(onlySend))
                config.OnlySend = onlySend.ToUpper() == "TRUE";

            foreach (var node in writer.GetChildElements("SEND/ELEMENTS", "ELEMENT"))
            {
                RSIElement elem = new RSIElement
                {
                    Tag = writer.GetAttributeValue(node, "TAG")
                };
                string typeStr = writer.GetAttributeValue(node, "TYPE");
                if (!string.IsNullOrEmpty(typeStr))
                    elem.Type = (RSIDataType)Enum.Parse(typeof(RSIDataType), typeStr);
                elem.Index = writer.GetAttributeValue(node, "INDX");
                config.SendElements.Add(elem);
            }

            foreach (var node in writer.GetChildElements("RECEIVE/ELEMENTS", "ELEMENT"))
            {
                RSIElement elem = new RSIElement
                {
                    Tag = writer.GetAttributeValue(node, "TAG")
                };
                string typeStr = writer.GetAttributeValue(node, "TYPE");
                if (!string.IsNullOrEmpty(typeStr))
                    elem.Type = (RSIDataType)Enum.Parse(typeof(RSIDataType), typeStr);
                elem.Index = writer.GetAttributeValue(node, "INDX");
                string holdOn = writer.GetAttributeValue(node, "HOLDON");
                if (!string.IsNullOrEmpty(holdOn))
                    elem.HoldOn = int.Parse(holdOn);
                config.ReceiveElements.Add(elem);
            }

            return config;
        }

        /// <summary>解析格式文档节为数据包格式（首子元素为包根，</summary>

        /// <param name="section">格式节元素（缺失返回 null，运行时回退内置默认）</param>
        /// <returns>数据包格式</returns>
        public static RsiPacketFormat ParseFormatSection(XElement section)
        {
            if (section == null)
            {
                return null;
            }
            XElement packetRoot = section.Elements().FirstOrDefault();
            if (packetRoot == null)
            {
                return null;
            }

            RsiPacketFormat format = new RsiPacketFormat
            {
                RootTag = packetRoot.Name.LocalName
            };
            XAttribute typeAttr = packetRoot.Attribute("TYPE") ?? packetRoot.Attribute("Type");
            format.RootTypeValue = typeAttr != null ? typeAttr.Value : string.Empty;

            foreach (XElement child in packetRoot.Elements())
            {
                RsiFormatElement elem = new RsiFormatElement(child.Name.LocalName);
                if (!child.HasElements && !child.HasAttributes)
                {
                    // 文本节点存占位值（时间戳/回传）
                    elem.IsTextNode = true;
                    elem.Attributes.Add(new RsiFormatAttribute(string.Empty, child.Value));
                }
                foreach (XAttribute attr in child.Attributes())
                {
                    elem.Attributes.Add(new RsiFormatAttribute(attr.Name.LocalName, attr.Value));
                }
                format.Elements.Add(elem);
            }
            return format;
        }

        /// <summary>从 XdocumentReaderWriter 文档加载格式节（sectionName：SEND_FORMAT / REPLY_FORMAT）</summary>
        /// <param name="writer">配置文档读写器</param>
        /// <param name="sectionName">格式节名</param>
        /// <returns>数据包格式，节缺失返回 null（运行时回退内置默认）</returns>
        public static RsiPacketFormat LoadPacketFormat(XdocumentReaderWriter writer, string sectionName)
        {
            XDocument doc = writer?.GetXDocument();
            if (doc == null || doc.Root == null)
            {
                return null;
            }
            return ParseFormatSection(doc.Root.Element(sectionName));
        }

        public static void SaveRSIConfig(string filePath, RSIConfig config)
        {
            var writer = new XdocumentReaderWriter();
            writer.LoadFromXmlString(GenerateRSIXML(config));
            writer.SaveToFilePath(filePath);
        }

        public static RSIConfig LoadRSIConfig(string filePath)
        {
            var writer = new XdocumentReaderWriter();
            writer.LoadFromFilePath(filePath);
            return ParseRSIXML(writer.ToXmlString());
        }
    }
}
