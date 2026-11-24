using AdaWeldSystem.Comm.Communication;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ConfigFileControl.Implementations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;

namespace AdaWeldSystem.Comm.Communication
{
    /// <summary>通讯对象类型</summary>
    public enum CommunicationType
    {
        /// <summary>通用 TCP/IP（服务端/客户端）</summary>
        GeneralTcp,

        /// <summary>西门子 S7</summary>
        SiemensS7,

        /// <summary>KUKA RSI</summary>
        KUKA_RSI,

        /// <summary>KUKA EKI 通道（枚举值保留供历史配置读取）</summary>
        KUKA_EKI,

        /// <summary>Modbus TCP（英莱相机等设备）</summary>
        ModbusTcp
    }

    /// <summary>单个通讯对象配置</summary>
    public class CommunicationConfig
    {
        /// <summary>配置名称（唯一标识）</summary>
        public string Name;

        /// <summary>通讯类型</summary>
        public CommunicationType Type;

        /// <summary>IP 地址</summary>
        public string IpAddress;

        /// <summary>端口号</summary>
        public int Port;

        /// <summary>S7 机架号（Rack）</summary>
        public int Rack;

        /// <summary>S7 插槽号（Slot）</summary>
        public int Slot;

        /// <summary>是否启用</summary>
        public bool Enabled;

        /// <summary>额外配置（JSON/XML 字符串，供特殊参数使用）</summary>
        public string ExtraConfig;

        /// <summary>构造函数</summary>
        public CommunicationConfig()
        {
            Name = "";
            Type = CommunicationType.GeneralTcp;
            IpAddress = "127.0.0.1";
            Port = 0;
            Rack = 0;
            Slot = 1;
            Enabled = false;
            ExtraConfig = "";
        }
    }

    /// <summary>通讯管理器（模块唯一对外访问口）</summary>
    public class CommunicationManager : IDisposable
    {
        private const string Tag = "CommunicationManager";

        private const string IniFileName = "CommunicationSettings.ini";

        /// <summary>通讯配置所属模块（全局配置目录下的模块层）。</summary>
        private const string ConfigModule = "Comm";
        private const string IniSection = "Display";
        private const string IniKeyShowCommLog = "ShowCommLog";
        private const string IniKeyShowDebugLog = "ShowDebugLog";
        private const string IniKeyShowRobotCommLog = "ShowRobotCommLog";
        private const string IniKeyShowPlcCommLog = "ShowPlcCommLog";
        private const string IniKeyShowModbusCommLog = "ShowModbusCommLog";
        private const string IniKeyPlcEnabled = "PlcEnabled";
        private const string IniKeyRobotEnabled = "RobotEnabled";
        private const string IniKeyGeneralEnabled = "GeneralEnabled";
        private const string IniKeyRobotCommMode = "RobotCommMode";
        private const string IniKeyNeedHeartbeat = "NeedHeartbeat";

        /// <summary>通讯层自动重连冷却（毫秒）</summary>
        private const int RobotReconnectCooldownMs = 5000;

        private readonly string _configFilePath;
        private readonly string _iniFilePath;
        private readonly List<CommunicationConfig> _configs;
        private readonly INIFile _iniFile;

        /// <summary>通讯 INI 载体（审计用）。</summary>
        private readonly IniConfigFile _iniFileFile;

        /// <summary>机器人位姿快照读写锁。</summary>
        private readonly object _robotPoseLock = new object();

        /// <summary>机器人端 RSI XML 写入锁</summary>
        private readonly object _rsiXmlLock = new object();

        /// <summary>上次机器人通道自愈时间（本地时钟毫秒）</summary>
        private long _lastRobotReconnectMs;

        /// <summary>最近一帧机器人位姿快照（RSI 坐标包上行）。</summary>
        private RobotPoseSnapshot _robotPose;

        /// <summary>是否已收到过机器人位姿。</summary>
        private bool _hasRobotPose;

        /// <summary>回复格式必备元素：8 具名元素 + IPOC</summary>
        private static readonly string[] ReplyFormatRequiredTags =
        {
            "WeldPower", "WireSpeed", "CutWire", "WeldSpeed", "WeaveAmp",
            "TxCmd", "TxPayload", "TxSeq", "IPOC"
        };

        /// <summary>服务器 XML 通讯结构（通用 TCP 服务端配置）</summary>
        public XdocumentReaderWriter ServerXdoc;

        /// <summary>客户端 XML 通讯结构（通用 TCP 客户端配置）</summary>
        public XdocumentReaderWriter ClientXdoc;

        /// <summary>发送数据 XML 通讯结构</summary>
        public XdocumentReaderWriter SendXdoc;

        /// <summary>接收数据 XML 通讯结构</summary>
        public XdocumentReaderWriter ReciveXdoc;

        /// <summary>机器人通讯服务端 XML 配置（RSI UDP：连接参数 + 收发格式节）</summary>
        public XdocumentReaderWriter RobotServerXdoc;

        /// <summary>EKI 已退役，字段留空兼容旧引用</summary>
        public XdocumentReaderWriter RobotClientXdoc;

        /// <summary>PLC 通讯 XML 配置（IP / Rack / Slot）</summary>
        public XdocumentReaderWriter PlcXdoc;

        /// <summary>Modbus 通讯 XML 配置（IP / Port / SlaveId）</summary>
        public XdocumentReaderWriter ModbusXdoc;

        /// <summary>以太网标准通讯实例（XML 文档交互）</summary>
        public EthernetXmlComm EthernetComm { get; private set; }

        /// <summary>KUKA RSI 通讯实例（UDP 实时通道，唯一机器人通道）</summary>
        public KukaRsiComm RsiComm { get; private set; }

        /// <summary>S7 PLC 通讯实例（备用降级通道）</summary>
        public SiemensS7Comm S7Comm { get; private set; }

        /// <summary>英莱 Modbus 实例，与 SDK 互斥</summary>
        public InLaserModbusComm ModbusComm { get; private set; }

        /// <summary>获取所有通讯配置（只读副本）</summary>
        public List<CommunicationConfig> Configs
        {
            get { return new List<CommunicationConfig>(_configs); }
        }

        /// <summary>是否将通讯指令日志显示到界面（INI 持久化）</summary>
        public bool ShowCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowCommLog, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>是否将 Debug 级日志显示到界面（INI 持久化，默认关闭）</summary>
        public bool ShowDebugLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowDebugLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowDebugLog, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
                GlobalCommData.ShowDebugToUi = value;
            }
        }

        /// <summary>是否将机器人通讯日志显示到界面（INI 持久化）</summary>
        public bool ShowRobotCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowRobotCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowRobotCommLog, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>是否将 PLC 通讯日志显示到界面（INI 持久化）</summary>
        public bool ShowPlcCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowPlcCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowPlcCommLog, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>是否将 Modbus 通讯日志显示到界面（INI 持久化）</summary>
        public bool ShowModbusCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowModbusCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowModbusCommLog, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>PLC 通讯是否启用（INI 持久化）</summary>
        public bool IsPlcEnabled
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyPlcEnabled, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyPlcEnabled, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>机器人通讯是否启用（INI 持久化）</summary>
        public bool IsRobotEnabled
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyRobotEnabled, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyRobotEnabled, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>是否启用分级心跳日志</summary>
        public bool NeedHeartbeat
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyNeedHeartbeat, true); }
        }

        /// <summary>通用通讯是否启用（INI 持久化）</summary>
        public bool IsGeneralEnabled
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyGeneralEnabled, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyGeneralEnabled, value);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>是否已收到机器人位姿（RSI 坐标包）</summary>
        public bool HasRobotPose
        {
            get
            {
                lock (_robotPoseLock)
                {
                    return _hasRobotPose;
                }
            }
        }

        /// <summary>机器人通道是否已连接（RSI 通道运行即视为已连接）</summary>
        public bool RobotConnected
        {
            get { return RsiComm != null && RsiComm.IsConnected; }
        }

        /// <summary>机器人是否真正在线</summary>
        public bool IsRobotOnline
        {
            get { return RsiComm != null && RsiComm.IsRobotOnline; }
        }

        /// <summary>最近机器人通讯启动失败原因</summary>
        public string LastRobotConnectError
        {
            get { return RsiComm != null ? RsiComm.LastError : string.Empty; }
        }

        /// <summary>最近一次收到机器人包的时间（未收到过为 DateTime.MinValue）。</summary>
        public DateTime LastRobotReceiveTime
        {
            get { return RsiComm != null ? RsiComm.LastReceiveTime : DateTime.MinValue; }
        }

        public int RsiCycleMs
        {
            get { return RsiComm.CycleMs; }
        }

        /// <summary>构造函数</summary>
        public CommunicationManager()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Comm", "Communication_XML", DefaultFileName);
            _iniFilePath = ConfigFileManager.ModuleAbsolutePath(ConfigModule, IniFileName);
            _configs = new List<CommunicationConfig>();
            _iniFileFile = ConfigFileManager.Instance.GetModuleIni(ConfigModule, "CommunicationSettings.ini");
            _iniFile = _iniFileFile.Raw;
            // 通讯 INI 缺键补写（只增不改；8 个开关均以代码默认 false 补齐）
            HealCommunicationIni();
            // 界面日志级别：Debug 默认不显示
            GlobalCommData.ShowDebugToUi = ShowDebugLog;

            InitializeXmlDocuments();
            Load();

            // 以太网标准通讯实例（XML 文档交互），从 Server/Client Xdoc 装载连接参数
            EthernetComm = new EthernetXmlComm();
            EthernetComm.Server.ServerIp = ServerXdoc.GetIp();
            EthernetComm.Server.ServerPort = ServerXdoc.GetPort();
            EthernetComm.Client.TargetIp = ClientXdoc.GetIp();
            EthernetComm.Client.TargetPort = ClientXdoc.GetPort();

            // KUKA 机器人 RSI 通道实例（通讯通道唯一为 RSI）
            RsiComm = new KukaRsiComm();
            RsiComm.RsiDataReceived += RsiComm_DataReceived;

            // PLC 通讯实例由 InitializePlcComm 延迟初始化
            S7Comm = null;

            // Modbus 通讯实例由 InitializeModbusComm 延迟初始化
            ModbusComm = null;
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">消息内容。</param>
        /// <param name="level">日志级别。</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }
        /// <summary>补写 CommunicationSettings.ini 的显示/启用开关缺键</summary>
        private void HealCommunicationIni()
        {
            List<ConfigSelfHeal.IniDefault> defaults = new List<ConfigSelfHeal.IniDefault>();
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyShowCommLog, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyShowDebugLog, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyShowRobotCommLog, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyShowPlcCommLog, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyShowModbusCommLog, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyPlcEnabled, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyRobotEnabled, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyGeneralEnabled, false.ToString()));
            defaults.Add(ConfigSelfHeal.Key(IniSection, IniKeyNeedHeartbeat, true.ToString()));

            string fileName = Path.GetFileName(_iniFilePath);
            ConfigSelfHeal.EnsureIni(_iniFilePath,
                ConfigFileManager.ModuleRelativeDir(ConfigModule) + "\\" + fileName, fileName, defaults, true);
        }

        /// <summary>实例化通讯所需的全部 XML 结构（TCP / 机器人 / PLC / Modbus / 主配置）</summary>
        private void InitializeXmlDocuments()
        {
            // 路径迁移已前移，此处不重复执行

            // 通用 TCP
            ServerXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "Server").Raw;
            ClientXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "Client").Raw;
            SendXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "Send").Raw;
            ReciveXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "Recive").Raw;

            // 机器人通讯：IP/HOST_PORT/PORT/CYCLE
            RobotServerXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "RobotServer").Raw;
            // 连接参数统一挂 <CONFIG>（旧根级配置自动迁移）；
            // 通道开关（RobotCommMode / RobotEnabled）不在本文件——RobotEnabled 存 Comm INI，RobotCommMode 固定 RSI
            EnsureRobotServerConfigSection();
            EnsureConfigElementExists(RobotServerXdoc, "IP", "192.168.10.100");
            EnsureConfigElementExists(RobotServerXdoc, "PORT", "49152");
            EnsureConfigElementExists(RobotServerXdoc, "HOST_IP", "192.168.10.50");
            EnsureConfigElementExists(RobotServerXdoc, "HOST_PORT", "49152");
            EnsureConfigElementExists(RobotServerXdoc, "SENTYPE", "WeldControl");
            EnsureConfigElementExists(RobotServerXdoc, "CYCLE", "12");
            // 一份文档含配置与收发格式，缺失补默认
            EnsureRsiFormatSections(RobotServerXdoc);

            // PLC 通讯
            PlcXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "PLC").Raw;
            EnsureElementExists(PlcXdoc, "IP", "192.168.0.1");
            EnsureElementExists(PlcXdoc, "PORT", "102");
            EnsureElementExists(PlcXdoc, "Rack", "0");
            EnsureElementExists(PlcXdoc, "Slot", "1");

            // Modbus 通讯
            ModbusXdoc = ConfigFileManager.Instance.GetModuleXml(ConfigModule, "Modbus").Raw;
            EnsureElementExists(ModbusXdoc, "IP", "192.168.1.100");
            EnsureElementExists(ModbusXdoc, "PORT", "502");
            EnsureElementExists(ModbusXdoc, "SlaveId", "1");
        }

        /// <summary>确保节点存在，缺失则建并赋默认</summary>
        private void EnsureElementExists(XdocumentReaderWriter xdoc, string elementName, string defaultValue)
        {
            if (xdoc.GetElement(elementName) == null)
            {
                xdoc.AddElement(elementName, defaultValue);
                xdoc.SaveXdocument();
            }
        }

        /// <summary>连接参数统一挂 CONFIG，旧结构迁移</summary>
        private void EnsureRobotServerConfigSection()
        {
            XDocument doc = RobotServerXdoc.GetXDocument();
            if (doc == null || doc.Root == null) return;

            XElement root = doc.Root;
            XElement config = root.Element("CONFIG");
            bool changed = false;
            if (config == null)
            {
                config = new XElement("CONFIG");
                root.AddFirst(config);
                changed = true;
            }

            string[] names = { "IP", "PORT", "HOST_IP", "HOST_PORT", "SENTYPE", "CYCLE" };
            for (int i = 0; i < names.Length; i++)
            {
                XElement flat = root.Element(names[i]);
                if (flat == null) continue;
                if (config.Element(names[i]) == null)
                {
                    flat.Remove();
                    config.Add(flat);
                }
                else
                {
                    // CONFIG 已有同名项：删除根级旧重复项，保留 CONFIG 现值
                    flat.Remove();
                }
                changed = true;
            }

            if (changed) RobotServerXdoc.SaveXdocument();
        }

        /// <summary>CONFIG 下确保节点存在并赋默认</summary>
        private void EnsureConfigElementExists(XdocumentReaderWriter xdoc, string elementName, string defaultValue)
        {
            if (xdoc.GetElement(elementName) != null) return;
            XElement config = xdoc.GetElementByPath("CONFIG") ?? xdoc.AddElement("CONFIG", null);
            xdoc.AddElement(config, elementName, defaultValue);
            xdoc.SaveXdocument();
        }

        private const string DefaultFileName = "CommunicationConfig.xml";

        /// <summary>机器人通讯模式（RSI 唯一通道）</summary>
        public enum RobotCommModeType
        {
            /// <summary>RSI 实时传感器接口（UDP）</summary>
            RSI = 0,
            /// <summary>EKI 已退役，仅留枚举值读旧配置</summary>
            EKI = 1
        }

        /// <summary>机器人通讯模式</summary>
        public RobotCommModeType RobotCommMode
        {
            get
            {
                // 通道恒为 RSI
                return RobotCommModeType.RSI;
            }
            set
            {
                // 写入恒为 RSI，保留 INI 键结构
                _iniFile.WriteInt(IniSection, IniKeyRobotCommMode, (int)RobotCommModeType.RSI);
                ConfigFileManager.SaveIni(_iniFileFile, "通讯设置", "配置变更");
            }
        }

        /// <summary>释放资源（依次释放以太网、RSI、PLC、Modbus 实例）</summary>
        public void Dispose()
        {
            try { EthernetComm?.Dispose(); EthernetComm = null; }
            catch (Exception ex) { Log("释放以太网通讯异常 " + ex.Message, MessageLevel.Warning); }
            try { RsiComm?.Dispose(); RsiComm = null; }
            catch (Exception ex) { Log("释放 RSI 通讯异常 " + ex.Message, MessageLevel.Warning); }
            try { S7Comm?.Dispose(); S7Comm = null; }
            catch (Exception ex) { Log("释放 PLC 通讯异常 " + ex.Message, MessageLevel.Warning); }
            try { ModbusComm?.Dispose(); ModbusComm = null; }
            catch (Exception ex) { Log("释放 Modbus 通讯异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>初始化机器人通讯（读 XML 配置并连接，默认主通道）</summary>
        public void InitializeRobotComm()
        {
            try
            {
                // RSI 通道配置（RobotServer Xdoc：IP=机器人 KLI，HOST_PORT=本机监听口，PORT=机器人发送口）
                string robotIp = RobotServerXdoc.GetElementValue("IP");
                if (!string.IsNullOrEmpty(robotIp))
                {
                    RsiComm.RobotIP = robotIp;
                }

                // 监听 IP 须属本机网口，否则失败
                string localIp = RobotServerXdoc.GetElementValue("HOST_IP");
                if (!string.IsNullOrEmpty(localIp))
                {
                    RsiComm.LocalIp = localIp;
                }

                string robotPort = RobotServerXdoc.GetElementValue("PORT");
                int parsedRobotPort = 0;
                if (!string.IsNullOrEmpty(robotPort))
                {
                    int.TryParse(robotPort, out parsedRobotPort);
                }
                if (parsedRobotPort > 0)
                {
                    RsiComm.RobotPort = parsedRobotPort;
                }

                // 本机监听端口以 HOST_PORT 为准（新版契约语义）；旧配置缺 HOST_PORT 时回落 PORT
                string hostPort = RobotServerXdoc.GetElementValue("HOST_PORT");
                int parsedHostPort = 0;
                if (!string.IsNullOrEmpty(hostPort))
                {
                    int.TryParse(hostPort, out parsedHostPort);
                }
                if (parsedHostPort > 0)
                {
                    RsiComm.LocalPort = parsedHostPort;
                }
                else if (parsedRobotPort > 0)
                {
                    RsiComm.LocalPort = parsedRobotPort;
                }

                string senType = RobotServerXdoc.GetElementValue("SENTYPE");
                if (!string.IsNullOrEmpty(senType))
                {
                    RsiComm.SenType = senType;
                }
                string cycleMs = RobotServerXdoc.GetElementValue("CYCLE");
                if (!string.IsNullOrEmpty(cycleMs) && int.TryParse(cycleMs, out int parsedCycle) && parsedCycle > 0)
                {
                    RsiComm.CycleMs = parsedCycle;
                }
                GlobalCommData.ShowCommLogForView(Tag, string.Format("RSI 通道配置 本地监听 {0}:{1} 机器人地址 {2}:{3} 传感器标识 {4} 通讯周期 {5} ms",
                    RsiComm.LocalIp, RsiComm.LocalPort, RsiComm.RobotIP, RsiComm.RobotPort, RsiComm.SenType, RsiComm.CycleMs));

                // 格式驱动收发：从 RobotServer.xml 的 SEND_FORMAT / REPLY_FORMAT 装载，
                // 组包解析以格式文档为准，先校验契约
                EnsureRsiFormatSections(RobotServerXdoc);
                RsiComm.SendPacketFormat = KUKAXMLConfig.LoadPacketFormat(RobotServerXdoc, "SEND_FORMAT");
                RsiComm.ReplyPacketFormat = KUKAXMLConfig.LoadPacketFormat(RobotServerXdoc, "REPLY_FORMAT");
                GlobalCommData.ShowCommLogForView(Tag, string.Format("RSI 收发格式已装载 发送格式元素 {0} 个 回复格式元素 {1} 个",
                    RsiComm.SendPacketFormat != null ? RsiComm.SendPacketFormat.Elements.Count : 0,
                    RsiComm.ReplyPacketFormat != null ? RsiComm.ReplyPacketFormat.Elements.Count : 0));

                // 每次启动刷新机器人端 RSI XML（SEND=DEF_RIst + Tool.X/Y/Z + RxCmd + RxPayload，
                // RECEIVE=WeldPower/WireSpeed/CutWire/WeldSpeed/WeaveAmp/TxCmd/TxPayload/TxSeq），
                // 生成后拷贝到机器人控制器 SensorInterface 目录使用
                SaveRobotRsiXml();
            }
            catch (Exception ex)
            {
                Log("初始化机器人通讯失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        #region 机器人通讯门面（RSI 唯一通道）

        /// <summary>发送格式必备元素：RIst/Tool/Rx*/IPOC</summary>
        private static readonly string[] SendFormatRequiredTags = { "RIst", "Tool", "RxCmd", "RxPayload", "IPOC" };

        /// <summary>RSI 数据接收转发</summary>
        public event EventHandler<KUKADataReceivedEventArgs> RobotDataReceived;

        /// <summary>刷新机器人位姿快照（RIst 笛卡尔实际位置）</summary>

        /// <param name="e">RSI 数据接收事件参数</param>
        private void UpdateRobotPose(RSIDataEventArgs e)
        {
            if (e == null || e.RobotPacket == null)
            {
                return;
            }

            KUKAPosition position = e.RobotPacket.RIst;
            lock (_robotPoseLock)
            {
                _robotPose = new RobotPoseSnapshot(position.X, position.Y, position.Z, e.Timestamp);
                _hasRobotPose = true;
            }
        }

        private void RsiComm_DataReceived(object sender, RSIDataEventArgs e)
        {
            UpdateRobotPose(e);
            RobotDataReceived?.Invoke(this, new KUKADataReceivedEventArgs
            {
                RobotData = e.RobotData,
                CorrectionData = e.CorrectionData,
                Timestamp = e.Timestamp,
                RxCmd = e.RobotPacket != null ? e.RobotPacket.RxCmd : 0,
                RxPayload = e.RobotPacket != null ? e.RobotPacket.RxPayload : 0.0
            });
        }

        /// <summary>回复格式节（8 个具名元素 + IPOC 回传）</summary>

        /// <returns>REPLY_FORMAT 节元素</returns>
        private static XElement BuildReplyFormatElement()
        {
            return new XElement("REPLY_FORMAT", new XAttribute("DESC", "上位机每周期回复 Sen 包（按此格式组包）"),
                new XElement("Sen", new XAttribute("Type", "WeldControl"),
                    new XElement("WeldPower", "激光功率"),
                    new XElement("WireSpeed", "送丝速度"),
                    new XElement("CutWire", "剪丝信号"),
                    new XElement("WeldSpeed", "焊接速度"),
                    new XElement("WeaveAmp", "振镜摆幅"),
                    new XElement("TxCmd", "TX指令码"),
                    new XElement("TxPayload", "TX指令载荷"),
                    new XElement("TxSeq", "TX指令序号"),
                    new XElement("IPOC", "原样回传")));
        }

        /// <summary>机器人发送格式节（RIst 位姿 + Tool 坐标 + RxCmd + RxPayload + IPOC 时间戳）</summary>
        /// <returns>SEND_FORMAT 节元素</returns>
        private static XElement BuildSendFormatElement()
        {
            return new XElement("SEND_FORMAT", new XAttribute("DESC", "机器人每周期发送 Rob 包（上位机按此格式解析）"),
                new XElement("Rob", new XAttribute("TYPE", "KUKA"),
                    new XElement("RIst",
                        new XAttribute("X", "笛卡尔X"), new XAttribute("Y", "笛卡尔Y"), new XAttribute("Z", "笛卡尔Z"),
                        new XAttribute("A", "笛卡尔A"), new XAttribute("B", "笛卡尔B"), new XAttribute("C", "笛卡尔C")),
                    new XElement("Tool",
                        new XAttribute("X", "工具X"), new XAttribute("Y", "工具Y"), new XAttribute("Z", "工具Z")),
                    new XElement("RxCmd", "上行指令码"),
                    new XElement("RxPayload", "上行指令载荷"),
                    new XElement("IPOC", "时间戳")));
        }

        /// <summary>判断格式节是否含指定包根与全部必备元素</summary>
        /// <param name="section">格式节（SEND_FORMAT / REPLY_FORMAT）</param>
        /// <param name="rootTag">包根标签（Rob / Sen）</param>
        /// <param name="requiredTags">必备子元素名</param>
        /// <returns>齐备返回 true</returns>
        private static bool HasRequiredElements(XElement section, string rootTag, string[] requiredTags)
        {
            if (section == null)
            {
                return false;
            }
            XElement root = section.Element(rootTag);
            if (root == null)
            {
                return false;
            }
            foreach (string tag in requiredTags)
            {
                if (root.Element(tag) == null)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>确保格式节契约一致，缺失则重建</summary>

        /// <param name="xdoc">RobotServer 配置文档读写器</param>
        private static void EnsureRsiFormatSections(XdocumentReaderWriter xdoc)
        {
            XDocument doc = xdoc.GetXDocument();
            if (doc == null || doc.Root == null)
            {
                return;
            }
            bool changed = false;

            XElement send = doc.Root.Element("SEND_FORMAT");
            if (!HasRequiredElements(send, "Rob", SendFormatRequiredTags))
            {
                if (send == null)
                {
                    doc.Root.Add(BuildSendFormatElement());
                }
                else
                {
                    send.ReplaceWith(BuildSendFormatElement());
                }
                changed = true;
                GlobalCommData.ShowLog(Tag, "RSI SEND_FORMAT 缺少现行协议元素(RxCmd/RxPayload)，已按数值化契约重建", MessageLevel.Warning);
            }

            XElement reply = doc.Root.Element("REPLY_FORMAT");
            if (!HasRequiredElements(reply, "Sen", ReplyFormatRequiredTags))
            {
                if (reply == null)
                {
                    doc.Root.Add(BuildReplyFormatElement());
                }
                else
                {
                    reply.ReplaceWith(BuildReplyFormatElement());
                }
                changed = true;
                GlobalCommData.ShowLog(Tag, "RSI REPLY_FORMAT 为旧版 Tech 契约，已按具名元素契约重建", MessageLevel.Warning);
            }

            if (changed)
            {
                xdoc.SaveXdocument();
            }
        }

        /// <summary>取位姿快照，锁保护</summary>

        /// <param name="pose">位姿快照，未收到过时为默认值</param>
        /// <returns>已收到过位姿返回 true</returns>
        public bool TryGetRobotPose(out RobotPoseSnapshot pose)
        {
            lock (_robotPoseLock)
            {
                pose = _robotPose;
                return _hasRobotPose;
            }
        }

        /// <summary>连接机器人 RSI 通道（通讯通道唯一为 RSI）</summary>
        /// <returns>true 连接成功 / false 失败</returns>
        public bool ConnectRobot()
        {
            bool ok = RsiComm != null && RsiComm.Start();
            if (ok)
            {
                // 首包前不消费上一轮陈旧位姿
                lock (_robotPoseLock)
                {
                    _hasRobotPose = false;
                }
            }
            return ok;
        }

        /// <summary>断开机器人 RSI 通道（返回关闭结果，供调试界面记录</summary>

        /// <returns>是否成功。</returns>
        public bool DisconnectRobot()
        {
            bool ok = true;
            if (RsiComm != null)
            {
                ok = RsiComm.Stop();
            }
            // 断链后位姿快照失效，消费方按「无位姿」处理
            lock (_robotPoseLock)
            {
                _hasRobotPose = false;
            }
            return ok;
        }

        /// <summary>通讯层自愈 通道未监听按冷却重连</summary>
        /// <returns>通道已监听返回 true</returns>
        public bool EnsureRobotChannelHealthy()
        {
            if (!IsRobotEnabled) return false;
            if (RsiComm != null && RsiComm.IsRunning) return true;

            long now = Environment.TickCount;
            if (_lastRobotReconnectMs != 0 && now - _lastRobotReconnectMs < RobotReconnectCooldownMs) return false;
            _lastRobotReconnectMs = now;

            try
            {
                GlobalCommData.ShowCommLogForView(Tag, "机器人通讯通道未监听 通讯层自动重连");
                InitializeRobotComm();
                bool ok = ConnectRobot();
                if (ok)
                {
                    Log("机器人通讯重连成功 恢复监听", MessageLevel.Debug);
                }
                else
                {
                    GlobalCommData.ShowCommLogForView(Tag, "机器人通讯重连失败 等待下次重试");
                }
                return ok;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowCommLogForView(Tag, "机器人通讯重连异常 " + ex.Message);
                return false;
            }
        }

        /// <summary>下发机器人指令（RSI 数值码通道：TxCmd + TxPayload + TxSeq）</summary>

        /// <param name="data">机器人数据载体（取 EStr 字段作为指令常量名，如 TX_GO_HOME）</param>
        public void SendRobotData(KUKARobotData data)
        {
            if (data == null || string.IsNullOrEmpty(data.EStr))
            {
                return;
            }
            SendRobotCommandByName(data.EStr);
        }

        /// <summary>按指令常量名下发机器人指令（内部转数值码）</summary>
        /// <param name="command">指令常量名（如 TX_GO_HOME）</param>
        /// <returns>命中已知下行指令返回 true</returns>
        public bool SendRobotCommandByName(string command)
        {
            int code = CommandCore.CommandToTxCodeValue(command);
            if (code == 0)
            {
                Log(string.Format("机器人指令未下发 未知下行指令 {0}", command), MessageLevel.Warning);
                return false;
            }
            SendRobotCommand(code);
            return true;
        }

        /// <summary>按数值码下发机器人指令（TxCmd + TxSeq 自增）</summary>
        /// <param name="txCmd">下行指令数值码（1~10，见 CommandCore 的 TX_* 映射）</param>
        /// <param name="txPayload">指令载荷（无载荷置 0）</param>
        public void SendRobotCommand(int txCmd, double txPayload = 0.0)
        {
            if (RsiComm == null) return;
            RsiComm.SendTxCommand(txCmd, txPayload);
            GlobalCommData.ShowCommLogForView(Tag, string.Format("机器人指令已下发 数值码 {0} 载荷 {1}", txCmd, txPayload));
        }

        /// <summary>清除已锁存的下行指令（报警解除后停发 TX_NG）</summary>
        public void ClearRobotCommand()
        {
            if (RsiComm == null) return;
            RsiComm.ClearTxCommand();
            GlobalCommData.ShowCommLogForView(Tag, "机器人下行指令已清除 报警标志停止重发");
        }

        /// <summary>写入回复机器人的焊接参数（RSI 回复线程取最新值</summary>

        /// <param name="laserPower">激光功率（W）→ WeldPower</param>
        /// <param name="wireSpeed">送丝速度（m/min）→ WireSpeed</param>
        /// <param name="cutWireSignal">剪丝信号（1=请求机器人执行剪丝程序）→ CutWire</param>
        /// <param name="weldSpeed">焊接速度（mm/s）→ WeldSpeed</param>
        /// <param name="weaveAmp">振镜摆幅（mm）→ WeaveAmp</param>
        public void UpdateRobotWeldParams(double laserPower, double wireSpeed, double cutWireSignal = 0.0,
            double weldSpeed = 0.0, double weaveAmp = 0.0)
        {
            RsiComm?.SetReplyValues(laserPower, wireSpeed, cutWireSignal, weldSpeed, weaveAmp);
        }

        /// <summary>设置 RSI 通讯周期（经门面写入并持久化到 RobotServer）</summary>
        /// <param name="cycleMs">通讯周期毫秒值（12 = IPO 默认 / 4 = IPO_FAST / 24 = IPO×2）</param>
        public void SetRsiCycleMs(int cycleMs)
        {
            if (cycleMs <= 0)
            {
                Log("设置 RSI 通讯周期失败 非法值 " + cycleMs, MessageLevel.Warning);
                return;
            }
            RsiComm.CycleMs = cycleMs;
            RobotServerXdoc.SetElementValue("CYCLE", cycleMs.ToString());
            RobotServerXdoc.SaveXdocument();
            Log(string.Format("RSI 通讯周期已设置 {0} ms", cycleMs));
        }

        /// <summary>生成机器人端 RSI XML 配置（WeldControlConfig.xml）</summary>

        /// <returns>生成的文件完整路径，失败返回空串</returns>
        public string SaveRobotRsiXml()
        {
            try
            {
                string hostIp = RobotServerXdoc.GetElementValue("HOST_IP");
                string hostPort = RobotServerXdoc.GetElementValue("HOST_PORT");
                string senType = RobotServerXdoc.GetElementValue("SENTYPE");

                // 目标端口须等于本机监听端口，否则失效
                int parsedHostPort = int.TryParse(hostPort, out int hostPortValue) ? hostPortValue : 0;
                int listenPort = RsiComm != null && RsiComm.LocalPort > 0 ? RsiComm.LocalPort : 0;
                if (listenPort <= 0)
                {
                    listenPort = parsedHostPort > 0 ? parsedHostPort : 49152;
                }
                if (parsedHostPort > 0 && parsedHostPort != listenPort)
                {
                    GlobalCommData.ShowCommLogForView(Tag, string.Format("RSI 端口配置不一致 HOST_PORT {0} 与本机监听 PORT {1} 不同，机器人端配置按监听口生成",
                        parsedHostPort, listenPort));
                }

                RSIConfig config = new RSIConfig
                {
                    IPNumber = string.IsNullOrEmpty(hostIp) ? "192.168.10.50" : hostIp,
                    Port = listenPort,
                    SenType = string.IsNullOrEmpty(senType) ? "WeldControl" : senType,
                    OnlySend = false
                };

                config.SendElements.Add(new RSIElement { Tag = "DEF_RIst", Type = RSIDataType.DOUBLE, Index = "INTERNAL" });
                // RSI 3.3 无 Tool 对象，用自由 TAG Tool.X/Y/Z 映射 ETHERNET 输入端 1/2/3（INDX 连续）
                config.SendElements.Add(new RSIElement { Tag = "Tool.X", Type = RSIDataType.DOUBLE, Index = "1" });
                config.SendElements.Add(new RSIElement { Tag = "Tool.Y", Type = RSIDataType.DOUBLE, Index = "2" });
                config.SendElements.Add(new RSIElement { Tag = "Tool.Z", Type = RSIDataType.DOUBLE, Index = "3" });
                // 上行指令数值化（SEND 段 TYPE 仅 BOOL/DOUBLE/LONG，禁 STRING）
                config.SendElements.Add(new RSIElement { Tag = "RxCmd", Type = RSIDataType.LONG, Index = "4" });
                config.SendElements.Add(new RSIElement { Tag = "RxPayload", Type = RSIDataType.DOUBLE, Index = "5" });

                // RECEIVE 段：上位机每周期回复（INDX 为 ETHERNET 输出端编号，须连续；HOLDON=1 保持上一有效值）
                config.ReceiveElements.Add(new RSIElement { Tag = "WeldPower", Type = RSIDataType.DOUBLE, Index = "1", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "WireSpeed", Type = RSIDataType.DOUBLE, Index = "2", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "CutWire", Type = RSIDataType.DOUBLE, Index = "3", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "WeldSpeed", Type = RSIDataType.DOUBLE, Index = "4", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "WeaveAmp", Type = RSIDataType.DOUBLE, Index = "5", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "TxCmd", Type = RSIDataType.LONG, Index = "6", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "TxPayload", Type = RSIDataType.DOUBLE, Index = "7", HoldOn = 1 });
                config.ReceiveElements.Add(new RSIElement { Tag = "TxSeq", Type = RSIDataType.LONG, Index = "8", HoldOn = 1 });

                // 写盘加锁避免并发占用
                // 写临时文件后原子替换，占用退避重试
                lock (_rsiXmlLock)
                {
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Comm", "KUKARobot_RSI_XML", "WeldControlConfig.xml");
                    WriteXmlWithRetry(filePath, () => XDocument.Parse(KUKAXMLConfig.GenerateRSIXML(config)));
                    GlobalCommData.ShowCommLogForView(Tag, "RSI XML 已生成");

                    // 格式文档随配置一并拷贝到机器人侧
                    string formatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Comm", "KUKARobot_RSI_XML", "WeldControlSendFormat.xml");
                    XDocument formatDoc = new XDocument(
                        new XDeclaration("1.0", "utf-8", null),
                        new XElement("ROOT",
                            new XElement("DESC", "机器人 RSI 收发格式定义（WeldControl）机器人每周期按 SEND_FORMAT 发送 上位机每周期按 REPLY_FORMAT 回复 IPOC 原样回传"),
                            BuildSendFormatElement(),
                            BuildReplyFormatElement()));
                    WriteXmlWithRetry(formatPath, () => formatDoc);
                    GlobalCommData.ShowCommLogForView(Tag, "RSI 收发格式文档已生成");
                    return filePath;
                }
            }
            catch (Exception ex)
            {
                Log("生成机器人端 RSI XML 配置失败 " + ex.Message, MessageLevel.Error);
                return string.Empty;
            }
        }

        /// <summary>写入机器人端 XML 配置</summary>
        /// <param name="filePath">目标文件完整路径</param>
        /// <param name="docFactory">每次重试新建文档工厂</param>
        private static void WriteXmlWithRetry(string filePath, Func<XDocument> docFactory)
        {
            const int maxAttempts = 5;
            const int retryDelayMs = 60;
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string tempPath = filePath + ".tmp_" + Guid.NewGuid().ToString("N");
                try
                {
                    docFactory().Save(tempPath);
                    if (File.Exists(filePath))
                    {
                        File.Replace(tempPath, filePath, null);
                    }
                    else
                    {
                        File.Move(tempPath, filePath);
                    }
                    return;
                }
                catch (IOException)
                {
                    TryDeleteTempFile(tempPath);
                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(retryDelayMs);
                    }
                }
                catch
                {
                    TryDeleteTempFile(tempPath);
                    throw;
                }
            }
            throw new IOException("写入超时 目标文件持续被占用 " + filePath);
        }

        /// <summary>静默删除临时文件</summary>
        /// <param name="path">文件路径</param>
        private static void TryDeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // 临时文件残留不阻断主流程
            }
            catch (UnauthorizedAccessException)
            {
                // 临时文件残留不阻断主流程
            }
        }

        #endregion

        /// <summary>初始化 PLC 通讯（备用降级通道，机器人通讯不可用时</summary>
        public void InitializePlcComm()
        {
            try
            {
                string plcIp = PlcXdoc.GetElementValue("IP");
                string plcRack = PlcXdoc.GetElementValue("Rack");
                string plcSlot = PlcXdoc.GetElementValue("Slot");

                if (!string.IsNullOrEmpty(plcIp) && !string.IsNullOrEmpty(plcRack) && !string.IsNullOrEmpty(plcSlot))
                {
                    S7Comm = new SiemensS7Comm(S7.Net.CpuType.S71500, plcIp, short.Parse(plcRack), short.Parse(plcSlot));
                    Log("PLC 通讯通道已就绪（备用降级通道）");
                }
            }
            catch (Exception ex)
            {
                Log("初始化 PLC 通讯失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>初始化 Modbus 通讯（从 XML 读连接参数并创建实例）</summary>
        public void InitializeModbusComm()
        {
            try
            {
                string modbusIp = ModbusXdoc.GetElementValue("IP");
                string modbusPort = ModbusXdoc.GetElementValue("PORT");
                string slaveId = ModbusXdoc.GetElementValue("SlaveId");

                if (!string.IsNullOrEmpty(modbusIp) && !string.IsNullOrEmpty(modbusPort))
                {
                    ModbusComm = new InLaserModbusComm
                    {
                        RemoteIP = modbusIp,
                        RemotePort = int.Parse(modbusPort),
                        SlaveId = byte.Parse(slaveId)
                    };
                    Log("Modbus 通讯通道已就绪");
                }
            }
            catch (Exception ex)
            {
                Log("初始化 Modbus 通讯失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>导入配置包后整体回读：通信通道 XML + INI 开关 + RSI RobotServer 文档。</summary>
        public void ReloadFromDisk()
        {
            Load();
            _iniFile.Reload();

            string robotServerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Comm", "KUKARobot_RSI_XML", "RobotServer.xml");
            if (File.Exists(robotServerFile))
            {
                RobotServerXdoc.LoadFromFilePath(robotServerFile);
            }
        }

        /// <summary>从 XML 文件加载配置</summary>
        public void Load()
        {
            _configs.Clear();

            if (!File.Exists(_configFilePath))
            {
                InitializeDefaultConfigs();
                Save();
                return;
            }

            try
            {
                XDocument doc = XDocument.Load(_configFilePath);
                XElement root = doc.Root;
                if (root == null)
                {
                    InitializeDefaultConfigs();
                    return;
                }

                foreach (XElement elem in root.Elements("Communication"))
                {
                    CommunicationConfig config = new CommunicationConfig();
                    XAttribute nameAttr = elem.Attribute("Name");
                    if (nameAttr != null)
                    {
                        config.Name = nameAttr.Value;
                    }

                    XAttribute typeAttr = elem.Attribute("Type");
                    if (typeAttr != null && Enum.TryParse(typeAttr.Value, out CommunicationType type))
                    {
                        config.Type = type;
                    }

                    config.IpAddress = GetElementValue(elem, "IpAddress", config.IpAddress);
                    config.Port = GetElementInt(elem, "Port", config.Port);
                    config.Rack = GetElementInt(elem, "Rack", config.Rack);
                    config.Slot = GetElementInt(elem, "Slot", config.Slot);
                    config.Enabled = GetElementBool(elem, "Enabled", config.Enabled);
                    config.ExtraConfig = GetElementValue(elem, "ExtraConfig", config.ExtraConfig);

                    _configs.Add(config);
                }
            }
            catch (Exception ex)
            {
                Log("加载通讯配置失败 " + ex.Message, MessageLevel.Error);
                InitializeDefaultConfigs();
            }
        }

        /// <summary>保存配置到 XML 文件</summary>
        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XDocument doc = new XDocument();
                XElement root = new XElement("CommunicationConfigs");
                foreach (CommunicationConfig config in _configs)
                {
                    XElement elem = new XElement("Communication");
                    elem.SetAttributeValue("Name", config.Name);
                    elem.SetAttributeValue("Type", config.Type.ToString());
                    elem.Add(new XElement("IpAddress", config.IpAddress));
                    elem.Add(new XElement("Port", config.Port));
                    elem.Add(new XElement("Rack", config.Rack));
                    elem.Add(new XElement("Slot", config.Slot));
                    elem.Add(new XElement("Enabled", config.Enabled));
                    elem.Add(new XElement("ExtraConfig", config.ExtraConfig));
                    root.Add(elem);
                }
                doc.Add(root);
                doc.Save(_configFilePath);
            }
            catch (Exception ex)
            {
                Log("保存通讯配置失败 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>添加或更新配置</summary>
        /// <param name="config">配置对象。</param>
        public void AddOrUpdate(CommunicationConfig config)
        {
            if (config == null)
            {
                return;
            }

            CommunicationConfig existing = _configs.FirstOrDefault(c => c.Name == config.Name);
            if (existing != null)
            {
                _configs.Remove(existing);
            }
            _configs.Add(config);
            Save();
        }

        /// <summary>根据名称移除配置</summary>
        /// <param name="name">名称。</param>
        public void Remove(string name)
        {
            CommunicationConfig existing = _configs.FirstOrDefault(c => c.Name == name);
            if (existing != null)
            {
                _configs.Remove(existing);
                Save();
            }
        }

        /// <summary>根据名称获取配置</summary>
        /// <param name="name">名称。</param>
        public CommunicationConfig GetByName(string name)
        {
            return _configs.FirstOrDefault(c => c.Name == name);
        }

        /// <summary>根据类型获取配置</summary>
        /// <returns>结果集合。</returns>
        public List<CommunicationConfig> GetByType(CommunicationType type)
        {
            return _configs.Where(c => c.Type == type).ToList();
        }

        #region 私有辅助方法

        private static bool GetElementBool(XElement parent, string name, bool defaultValue)
        {
            string rawText = GetElementValue(parent, name, defaultValue.ToString());
            return bool.TryParse(rawText, out bool result) ? result : defaultValue;
        }

        private static int GetElementInt(XElement parent, string name, int defaultValue)
        {
            string rawText = GetElementValue(parent, name, defaultValue.ToString());
            return int.TryParse(rawText, out int result) ? result : defaultValue;
        }

        private static string GetElementValue(XElement parent, string name, string defaultValue)
        {
            XElement elem = parent.Element(name);
            return elem != null ? elem.Value : defaultValue;
        }

        private void InitializeDefaultConfigs()
        {
            _configs.Add(new CommunicationConfig
            {
                Name = "GeneralTCPServer",
                Type = CommunicationType.GeneralTcp,
                IpAddress = "127.0.0.1",
                Port = 6000,
                Enabled = false
            });
            _configs.Add(new CommunicationConfig
            {
                Name = "GeneralTCPClient",
                Type = CommunicationType.GeneralTcp,
                IpAddress = "127.0.0.1",
                Port = 6000,
                Enabled = false
            });
            _configs.Add(new CommunicationConfig
            {
                Name = "SiemensS7",
                Type = CommunicationType.SiemensS7,
                IpAddress = "192.168.0.1",
                Port = 102,
                Rack = 0,
                Slot = 1,
                Enabled = false
            });
            _configs.Add(new CommunicationConfig
            {
                Name = "KUKA_RSI",
                Type = CommunicationType.KUKA_RSI,
                IpAddress = "192.168.10.100",
                Port = 49152,
                Enabled = false
            });
            _configs.Add(new CommunicationConfig
            {
                Name = "IntelligentLaserModbus",
                Type = CommunicationType.ModbusTcp,
                IpAddress = "192.168.1.100",
                Port = 502,
                Enabled = false
            });
        }

        #endregion
    }

    /// <summary>机器人位姿快照（RSI 上行坐标，基坐标系，毫米）</summary>
    public struct RobotPoseSnapshot
    {
        /// <summary>机器人 X 坐标（毫米）</summary>
        public double X { get; private set; }

        /// <summary>机器人 Y 坐标（毫米）</summary>
        public double Y { get; private set; }

        /// <summary>机器人 Z 坐标（毫米）</summary>
        public double Z { get; private set; }

        /// <summary>本机接收时刻</summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>创建机器人位姿快照</summary>
        /// <param name="x">机器人 X 坐标（毫米）</param>
        /// <param name="y">机器人 Y 坐标（毫米）</param>
        /// <param name="z">机器人 Z 坐标（毫米）</param>
        /// <param name="timestamp">本机接收时刻</param>
        public RobotPoseSnapshot(double x, double y, double z, DateTime timestamp)
        {
            X = x;
            Y = y;
            Z = z;
            Timestamp = timestamp;
        }
    }

    /// <summary>KUKA 数据接收事件参数（统一管理层）</summary>
    public class KUKADataReceivedEventArgs : EventArgs
    {
        public KUKARobotData RobotData;
        public KUKACorrectionData CorrectionData;
        public DateTime Timestamp;

        /// <summary>上行指令码（RSI 数值化协议；0=无指令，1~9 见 CommandCore 的 RX_* 映射）</summary>
        public int RxCmd;

        /// <summary>上行指令载荷（当前仅 RX_WELD_STOP_POSE 使用：停止位 X / mm）</summary>
        public double RxPayload;
    }

    /// <summary>KUKA 连接状态变更事件参数（统一管理层）</summary>
    public class KUKAConnectionEventArgs : EventArgs
    {
        public bool IsRSIConnected;

        /// <summary>恒返回 false</summary>
        public bool IsEKIConnected;
        public DateTime Timestamp;
    }
}


