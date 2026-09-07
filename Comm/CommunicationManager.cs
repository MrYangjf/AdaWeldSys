using AdaWeldSystem.Comm.NetworkPort.Ethernet;
using AdaWeldSystem.Comm.IntelligentLaserModbus;
using AdaWeldSystem.Comm.Robot.KUKARobot;
using AdaWeldSystem.Comm.PLC.Siemens;
using AdaWeldSystem.FileOperate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AdaWeldSystem.Comm
{
    /// <summary>
    /// 通讯对象类型
    /// </summary>
    public enum CommunicationType
    {
        /// <summary>
        /// 通用 TCP/IP（服务端/客户端）
        /// </summary>
        GeneralTcp,

        /// <summary>
        /// 西门子 S7
        /// </summary>
        SiemensS7,

        /// <summary>
        /// Profinet（通过专用硬件/GSDML）
        /// </summary>
        Profinet,

        /// <summary>
        /// KUKA RSI
        /// </summary>
        KUKA_RSI,

        /// <summary>
        /// KUKA EKI
        /// </summary>
        KUKA_EKI,

        /// <summary>
        /// Modbus TCP（英莱相机等 Modbus 设备通讯）
        /// </summary>
        ModbusTcp
    }

    /// <summary>
    /// 单个通讯对象配置
    /// </summary>
    public class CommunicationConfig
    {
        /// <summary>
        /// 配置名称（唯一标识）
        /// </summary>
        public string Name;

        /// <summary>
        /// 通讯类型
        /// </summary>
        public CommunicationType Type;

        /// <summary>
        /// IP 地址
        /// </summary>
        public string IpAddress;

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port;

        /// <summary>
        /// S7 机架号（Rack）
        /// </summary>
        public int Rack;

        /// <summary>
        /// S7 插槽号（Slot）
        /// </summary>
        public int Slot;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// 额外配置（JSON/XML 字符串，供特殊参数使用）
        /// </summary>
        public string ExtraConfig;

        /// <summary>
        /// 构造函数
        /// </summary>
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

    /// <summary>
    /// 通讯管理器
    /// 统一管理 PLC、机器人、通用 TCP、Modbus 等通讯对象的持久化配置，
    /// 并持有通用 TCP 通讯所需的 Server/Client/Send/Receive XML 结构。
    /// 非通讯结构类的显示开关（ShowCommLog）使用 INI 文件持久化。
    /// 直接暴露 KUKARobotManager、S7PLCCommunication 和 ModbusTCPCommunication 实例。
    /// 非 UI 类，实现标准 IDisposable.Dispose() 释放持有的通讯实例（ADR-001：非 UI 类不走 DisposeComponents）。
    /// </summary>
    public class CommunicationManager : IDisposable
    {
        private const string Tag = "CommunicationManager";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }
        private const string DefaultFileName = "CommunicationConfig.xml";
        private const string IniFileName = "CommunicationSettings.ini";
        private const string IniSection = "Display";
        private const string IniKeyShowCommLog = "ShowCommLog";
        private const string IniKeyShowRobotCommLog = "ShowRobotCommLog";
        private const string IniKeyShowPlcCommLog = "ShowPlcCommLog";
        private const string IniKeyShowModbusCommLog = "ShowModbusCommLog";
        private const string IniKeyPlcEnabled = "PlcEnabled";
        private const string IniKeyRobotEnabled = "RobotEnabled";
        private const string IniKeyGeneralEnabled = "GeneralEnabled";
        private const string IniKeyRobotCommMode = "RobotCommMode";

        private readonly string _configFilePath;
        private readonly string _iniFilePath;
        private readonly List<CommunicationConfig> _configs;
        private readonly INIFile _iniFile;

        /// <summary>
        /// 服务器 XML 通讯结构（通用 TCP 服务端配置）
        /// </summary>
        public XdocumentReaderWriter ServerXdoc;

        /// <summary>
        /// 客户端 XML 通讯结构（通用 TCP 客户端配置）
        /// </summary>
        public XdocumentReaderWriter ClientXdoc;

        /// <summary>
        /// 发送数据 XML 通讯结构（通用 TCP 发送数据格式）
        /// </summary>
        public XdocumentReaderWriter SendXdoc;

        /// <summary>
        /// 接收数据 XML 通讯结构（通用 TCP 接收数据格式）
        /// </summary>
        public XdocumentReaderWriter ReciveXdoc;

        /// <summary>
        /// 机器人通讯服务端 XML 配置（RSI UDP / EKI Server 模式）
        /// </summary>
        public XdocumentReaderWriter RobotServerXdoc;

        /// <summary>
        /// 机器人通讯客户端 XML 配置（EKI Client 模式）
        /// </summary>
        public XdocumentReaderWriter RobotClientXdoc;

        /// <summary>
        /// PLC 通讯 XML 配置（IP / Rack / Slot）
        /// </summary>
        public XdocumentReaderWriter PlcXdoc;

        /// <summary>
        /// Modbus 通讯 XML 配置（IP / Port / SlaveId）
        /// </summary>
        public XdocumentReaderWriter ModbusXdoc;

        /// <summary>
        /// 通用 TCP/IP 通讯实例
        /// </summary>
        public TcpIpComm TcpIpComm { get; private set; }

        /// <summary>
        /// KUKA 机器人通讯管理器（默认主通道）
        /// 直接暴露 KUKARobotManager 实例，管理 RSI/EKI 通讯。
        /// </summary>
        public KUKARobotManager RobotManager { get; private set; }

        /// <summary>
        /// S7 PLC 通讯实例（备用降级通道）
        /// 直接暴露 S7PLCCommunication 实例，由 InitializePlcComm 初始化。
        /// </summary>
        public S7PLCCommunication PlcManager { get; private set; }

        /// <summary>
        /// Modbus TCP 通讯实例（英莱相机等 Modbus 设备通讯）
        /// 直接暴露 ModbusTCPCommunication 实例，由 InitializeModbusComm 初始化。
        /// </summary>
        public ModbusTCPCommunication ModbusManager { get; private set; }

        /// <summary>
        /// 获取所有通讯配置（只读副本）
        /// </summary>
        public List<CommunicationConfig> Configs
        {
            get { return new List<CommunicationConfig>(_configs); }
        }

        /// <summary>
        /// 是否将通讯指令日志显示到界面（INI 持久化）
        /// </summary>
        public bool ShowCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowCommLog, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 是否将机器人通讯日志显示到界面（INI 持久化）
        /// </summary>
        public bool ShowRobotCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowRobotCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowRobotCommLog, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 是否将 PLC 通讯日志显示到界面（INI 持久化）
        /// </summary>
        public bool ShowPlcCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowPlcCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowPlcCommLog, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 是否将 Modbus 通讯日志显示到界面（INI 持久化）
        /// </summary>
        public bool ShowModbusCommLog
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyShowModbusCommLog, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyShowModbusCommLog, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// PLC 通讯是否启用（INI 持久化）
        /// </summary>
        public bool IsPlcEnabled
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyPlcEnabled, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyPlcEnabled, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 机器人通讯是否启用（INI 持久化）
        /// </summary>
        public bool IsRobotEnabled
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyRobotEnabled, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyRobotEnabled, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 通用通讯是否启用（INI 持久化）
        /// </summary>
        public bool IsGeneralEnabled
        {
            get { return _iniFile.ReadBool(IniSection, IniKeyGeneralEnabled, false); }
            set
            {
                _iniFile.WriteBool(IniSection, IniKeyGeneralEnabled, value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 机器人通讯模式（RSI 或 EKI，二选一）
        /// </summary>
        public enum RobotCommModeType
        {
            /// <summary>RSI 实时传感器接口（UDP）</summary>
            RSI = 0,
            /// <summary>EKI 以太网数据交换（TCP）</summary>
            EKI = 1
        }

        /// <summary>
        /// 机器人通讯模式（RSI/EKI 二选一，INI 持久化）
        /// </summary>
        public RobotCommModeType RobotCommMode
        {
            get
            {
                int val = _iniFile.ReadInt(IniSection, IniKeyRobotCommMode, 0);
                return (RobotCommModeType)val;
            }
            set
            {
                _iniFile.WriteInt(IniSection, IniKeyRobotCommMode, (int)value);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CommunicationManager()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "XML", DefaultFileName);
            _iniFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", IniFileName);
            _configs = new List<CommunicationConfig>();
            _iniFile = new INIFile(_iniFilePath);

            InitializeXmlDocuments();
            Load();
            TcpIpComm = new TcpIpComm(this);

            // 初始化 KUKA 机器人通讯管理器（默认主通道）
            RobotManager = new KUKARobotManager();

            // PLC 通讯管理器由 InitializePlcComm 延迟初始化
            PlcManager = null;

            // Modbus 通讯管理器由 InitializeModbusComm 延迟初始化
            ModbusManager = null;
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose）。
        /// 依次释放通用 TCP、机器人、PLC、Modbus 通讯实例。
        /// </summary>
        public void Dispose()
        {
            try { TcpIpComm?.Dispose(); TcpIpComm = null; }
            catch (Exception ex) { Log("释放通用 TCP 通讯异常 " + ex.Message, MessageLevel.Warning); }
            try { RobotManager?.Dispose(); RobotManager = null; }
            catch (Exception ex) { Log("释放机器人通讯异常 " + ex.Message, MessageLevel.Warning); }
            try { PlcManager?.Dispose(); PlcManager = null; }
            catch (Exception ex) { Log("释放 PLC 通讯异常 " + ex.Message, MessageLevel.Warning); }
            try { ModbusManager?.Dispose(); ModbusManager = null; }
            catch (Exception ex) { Log("释放 Modbus 通讯异常 " + ex.Message, MessageLevel.Warning); }
        }

        /// <summary>
        /// 初始化机器人通讯（从 XML 配置读取连接参数并连接）
        /// 机器人通讯为默认主通道，优先使用 RSI/EKI。
        /// </summary>
        public void InitializeRobotComm()
        {
            try
            {
                string robotIp = RobotServerXdoc.GetElementValue("IP");
                string robotPort = RobotServerXdoc.GetElementValue("PORT");
                if (!string.IsNullOrEmpty(robotIp) && !string.IsNullOrEmpty(robotPort))
                {
                    Log(string.Format("机器人通讯配置 IP {0} 端口 {1}", robotIp, robotPort));
                }

                Log("机器人通讯管理器已就绪（默认主通道）");
            }
            catch (Exception ex)
            {
                Log("初始化机器人通讯失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 初始化 PLC 通讯（从 XML 配置读取连接参数并创建实例）
        /// PLC 通讯为备用降级通道，机器人通讯不可用时自动切换。
        /// </summary>
        public void InitializePlcComm()
        {
            try
            {
                string plcIp = PlcXdoc.GetElementValue("IP");
                string plcRack = PlcXdoc.GetElementValue("Rack");
                string plcSlot = PlcXdoc.GetElementValue("Slot");

                if (!string.IsNullOrEmpty(plcIp) && !string.IsNullOrEmpty(plcRack) && !string.IsNullOrEmpty(plcSlot))
                {
                    PlcManager = new S7PLCCommunication(S7.Net.CpuType.S71500, plcIp, short.Parse(plcRack), short.Parse(plcSlot));
                    Log("PLC 通讯管理器已就绪（备用降级通道）");
                }
            }
            catch (Exception ex)
            {
                Log("初始化 PLC 通讯失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 初始化 Modbus 通讯（从 XML 配置读取连接参数并创建实例）
        /// </summary>
        public void InitializeModbusComm()
        {
            try
            {
                string modbusIp = ModbusXdoc.GetElementValue("IP");
                string modbusPort = ModbusXdoc.GetElementValue("PORT");
                string slaveId = ModbusXdoc.GetElementValue("SlaveId");

                if (!string.IsNullOrEmpty(modbusIp) && !string.IsNullOrEmpty(modbusPort))
                {
                    ModbusManager = new ModbusTCPCommunication();
                    ModbusManager.RemoteIP = modbusIp;
                    ModbusManager.RemotePort = int.Parse(modbusPort);
                    ModbusManager.SlaveId = byte.Parse(slaveId);
                    Log("Modbus 通讯管理器已就绪");
                }
            }
            catch (Exception ex)
            {
                Log("初始化 Modbus 通讯失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 实例化通讯所需的 XML 结构
        /// 通用 TCP：Server / Client / Send / Recive
        /// 机器人：RobotServer / RobotClient
        /// PLC：PLC
        /// Modbus：Modbus
        /// </summary>
        private void InitializeXmlDocuments()
        {
            // 通用 TCP
            ServerXdoc = new XdocumentReaderWriter("Server");
            ClientXdoc = new XdocumentReaderWriter("Client");
            SendXdoc = new XdocumentReaderWriter("Send");
            ReciveXdoc = new XdocumentReaderWriter("Recive");

            // 机器人通讯
            RobotServerXdoc = new XdocumentReaderWriter("RobotServer");
            RobotClientXdoc = new XdocumentReaderWriter("RobotClient");
            EnsureElementExists(RobotServerXdoc, "IP", "192.168.1.10");
            EnsureElementExists(RobotServerXdoc, "PORT", "49152");
            EnsureElementExists(RobotClientXdoc, "IP", "192.168.1.10");
            EnsureElementExists(RobotClientXdoc, "PORT", "60000");

            // PLC 通讯
            PlcXdoc = new XdocumentReaderWriter("PLC");
            EnsureElementExists(PlcXdoc, "IP", "192.168.0.1");
            EnsureElementExists(PlcXdoc, "PORT", "102");
            EnsureElementExists(PlcXdoc, "Rack", "0");
            EnsureElementExists(PlcXdoc, "Slot", "1");

            // Modbus 通讯
            ModbusXdoc = new XdocumentReaderWriter("Modbus");
            EnsureElementExists(ModbusXdoc, "IP", "192.168.1.100");
            EnsureElementExists(ModbusXdoc, "PORT", "502");
            EnsureElementExists(ModbusXdoc, "SlaveId", "1");
        }

        /// <summary>
        /// 确保 XML 文档中存在指定节点，不存在则创建并赋默认值
        /// </summary>
        private void EnsureElementExists(XdocumentReaderWriter xdoc, string elementName, string defaultValue)
        {
            if (xdoc.GetElement(elementName) == null)
            {
                xdoc.AddElement(elementName, defaultValue);
                xdoc.SaveXdocument();
            }
        }

        /// <summary>
        /// 从 XML 文件加载配置
        /// </summary>
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

        /// <summary>
        /// 保存配置到 XML 文件
        /// </summary>
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

        /// <summary>
        /// 添加或更新配置
        /// </summary>
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

        /// <summary>
        /// 根据名称移除配置
        /// </summary>
        public void Remove(string name)
        {
            CommunicationConfig existing = _configs.FirstOrDefault(c => c.Name == name);
            if (existing != null)
            {
                _configs.Remove(existing);
                Save();
            }
        }

        /// <summary>
        /// 根据名称获取配置
        /// </summary>
        public CommunicationConfig GetByName(string name)
        {
            return _configs.FirstOrDefault(c => c.Name == name);
        }

        /// <summary>
        /// 根据类型获取配置
        /// </summary>
        public List<CommunicationConfig> GetByType(CommunicationType type)
        {
            return _configs.Where(c => c.Type == type).ToList();
        }

        #region 私有辅助方法

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
                IpAddress = "192.168.1.10",
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

        private static string GetElementValue(XElement parent, string name, string defaultValue)
        {
            XElement elem = parent.Element(name);
            return elem != null ? elem.Value : defaultValue;
        }

        private static int GetElementInt(XElement parent, string name, int defaultValue)
        {
            string value = GetElementValue(parent, name, defaultValue.ToString());
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        private static bool GetElementBool(XElement parent, string name, bool defaultValue)
        {
            string value = GetElementValue(parent, name, defaultValue.ToString());
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        #endregion
    }
}