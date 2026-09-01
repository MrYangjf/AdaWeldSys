using System;
using System.Collections.Generic;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.Comm.Robot.KUKARobot
{
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
            IPNumber = "172.1.10.5";
            Port = 49152;
            SenType = "ImFree";
            OnlySend = false;
        }
    }

    public class EKIElement
    {
        public string Tag;
        public EKIDataType Type;
        public string Mode;
        public int SetOut;
        public int SetFlag;
    }

    public class EKIConfig
    {
        public string ExternalIP;
        public int ExternalPort;
        public EKIMode ExternalType;
        public EKIEnvironment Environment;
        public string BufferingMode;
        public int BufferingLimit;
        public int BuffSizeLimit;
        public int TimeoutConnect;
        public int AliveSetOut;
        public int AliveSetFlag;
        public int AlivePing;
        public List<EKIElement> ReceiveElements;
        public List<EKIElement> SendElements;

        public EKIConfig()
        {
            ExternalIP = "172.1.10.5";
            ExternalPort = 54600;
            ExternalType = EKIMode.Client;
            Environment = EKIEnvironment.Program;
            BufferingMode = "FIFO";
            BufferingLimit = 10;
            BuffSizeLimit = 16384;
            TimeoutConnect = 60000;
            AliveSetOut = 0;
            AliveSetFlag = 0;
            AlivePing = 0;
            ReceiveElements = new List<EKIElement>();
            SendElements = new List<EKIElement>();
        }
    }

    public static class KUKAXMLConfig
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

            return writer.ToXmlString();
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
                RSIElement elem = new RSIElement();
                elem.Tag = writer.GetAttributeValue(node, "TAG");
                string typeStr = writer.GetAttributeValue(node, "TYPE");
                if (!string.IsNullOrEmpty(typeStr))
                    elem.Type = (RSIDataType)Enum.Parse(typeof(RSIDataType), typeStr);
                elem.Index = writer.GetAttributeValue(node, "INDX");
                config.SendElements.Add(elem);
            }

            foreach (var node in writer.GetChildElements("RECEIVE/ELEMENTS", "ELEMENT"))
            {
                RSIElement elem = new RSIElement();
                elem.Tag = writer.GetAttributeValue(node, "TAG");
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

        public static void SaveRSIConfig(string filePath, RSIConfig config)
        {
            string xml = GenerateRSIXML(config);
            var writer = new XdocumentReaderWriter();
            writer.LoadFromXmlString(xml);
            writer.SaveToFilePath(filePath);
        }

        public static RSIConfig LoadRSIConfig(string filePath)
        {
            var writer = new XdocumentReaderWriter();
            writer.LoadFromFilePath(filePath);
            string xml = writer.ToXmlString();
            return ParseRSIXML(xml);
        }

        public static string GenerateEKIXML(EKIConfig config)
        {
            var writer = new XdocumentReaderWriter();
            writer.NewXdocument("ETHERNETKRL", "1.0", "UTF-8");

            var configuration = writer.AddElement("CONFIGURATION");

            var external = writer.AddElement(configuration, "EXTERNAL");
            if (!string.IsNullOrEmpty(config.ExternalIP) && config.ExternalType == EKIMode.Client)
                writer.AddElement(external, "IP", config.ExternalIP);
            if (config.ExternalPort > 0 && config.ExternalType == EKIMode.Client)
                writer.AddElement(external, "PORT", config.ExternalPort.ToString());
            writer.AddElement(external, "TYPE", config.ExternalType == EKIMode.Server ? "Server" : "Client");

            var internalElem = writer.AddElement(configuration, "INTERNAL");
            writer.AddElement(internalElem, "ENVIRONMENT", config.Environment.ToString());

            if (!string.IsNullOrEmpty(config.BufferingMode))
            {
                var buffering = writer.AddElement(internalElem, "BUFFERING");
                writer.SetAttributeValue(buffering, "Mode", config.BufferingMode);
                if (config.BufferingLimit > 0)
                    writer.SetAttributeValue(buffering, "Limit", config.BufferingLimit.ToString());
            }

            if (config.BuffSizeLimit > 0)
            {
                var buffSize = writer.AddElement(internalElem, "BUFFSIZE");
                writer.SetAttributeValue(buffSize, "Limit", config.BuffSizeLimit.ToString());
            }

            if (config.TimeoutConnect > 0)
            {
                var timeout = writer.AddElement(internalElem, "TIMEOUT");
                writer.SetAttributeValue(timeout, "Connect", config.TimeoutConnect.ToString());
            }

            if (config.AliveSetOut > 0 || config.AliveSetFlag > 0 || config.AlivePing > 0)
            {
                var alive = writer.AddElement(internalElem, "ALIVE");
                if (config.AliveSetOut > 0) writer.SetAttributeValue(alive, "Set_Out", config.AliveSetOut.ToString());
                if (config.AliveSetFlag > 0) writer.SetAttributeValue(alive, "Set_Flag", config.AliveSetFlag.ToString());
                if (config.AlivePing > 0) writer.SetAttributeValue(alive, "Ping", config.AlivePing.ToString());
            }

            if (config.ReceiveElements != null && config.ReceiveElements.Count > 0)
            {
                var receive = writer.AddElement("RECEIVE");
                var xmlElem = writer.AddElement(receive, "XML");
                foreach (EKIElement elem in config.ReceiveElements)
                {
                    var attrs = new Dictionary<string, string>();
                    attrs.Add("Tag", elem.Tag);
                    if (elem.Type != EKIDataType.STREAM)
                        attrs.Add("Type", elem.Type.ToString());
                    if (!string.IsNullOrEmpty(elem.Mode))
                        attrs.Add("Mode", elem.Mode);
                    if (elem.SetOut > 0)
                        attrs.Add("Set_Out", elem.SetOut.ToString());
                    if (elem.SetFlag > 0)
                        attrs.Add("Set_Flag", elem.SetFlag.ToString());
                    writer.AddElementWithAttributes(xmlElem, "ELEMENT", attrs);
                }
            }

            if (config.SendElements != null && config.SendElements.Count > 0)
            {
                var send = writer.AddElement("SEND");
                var xmlElem = writer.AddElement(send, "XML");
                foreach (EKIElement elem in config.SendElements)
                {
                    var attrs = new Dictionary<string, string>();
                    attrs.Add("Tag", elem.Tag);
                    writer.AddElementWithAttributes(xmlElem, "ELEMENT", attrs);
                }
            }

            return writer.ToXmlString();
        }

        public static EKIConfig ParseEKIXML(string xmlContent)
        {
            EKIConfig config = new EKIConfig();
            var writer = new XdocumentReaderWriter();
            writer.LoadFromXmlString(xmlContent);

            var external = writer.GetElementByPath("CONFIGURATION/EXTERNAL");
            if (external != null)
            {
                config.ExternalIP = writer.GetElementValueByPath("CONFIGURATION/EXTERNAL/IP", config.ExternalIP);
                string portStr = writer.GetElementValueByPath("CONFIGURATION/EXTERNAL/PORT");
                if (int.TryParse(portStr, out int port))
                    config.ExternalPort = port;
                string type = writer.GetElementValueByPath("CONFIGURATION/EXTERNAL/TYPE");
                if (!string.IsNullOrEmpty(type))
                    config.ExternalType = type == "Server" ? EKIMode.Server : EKIMode.Client;
            }

            var internalElem = writer.GetElementByPath("CONFIGURATION/INTERNAL");
            if (internalElem != null)
            {
                string env = writer.GetElementValueByPath("CONFIGURATION/INTERNAL/ENVIRONMENT");
                if (!string.IsNullOrEmpty(env))
                    config.Environment = (EKIEnvironment)Enum.Parse(typeof(EKIEnvironment), env);

                var buffering = writer.GetElementByPath("CONFIGURATION/INTERNAL/BUFFERING");
                if (buffering != null)
                {
                    config.BufferingMode = writer.GetAttributeValue(buffering, "Mode", config.BufferingMode);
                    string limit = writer.GetAttributeValue(buffering, "Limit");
                    if (!string.IsNullOrEmpty(limit)) config.BufferingLimit = int.Parse(limit);
                }

                var buffSize = writer.GetElementByPath("CONFIGURATION/INTERNAL/BUFFSIZE");
                if (buffSize != null)
                {
                    string limit = writer.GetAttributeValue(buffSize, "Limit");
                    if (!string.IsNullOrEmpty(limit)) config.BuffSizeLimit = int.Parse(limit);
                }

                var timeout = writer.GetElementByPath("CONFIGURATION/INTERNAL/TIMEOUT");
                if (timeout != null)
                {
                    string connect = writer.GetAttributeValue(timeout, "Connect");
                    if (!string.IsNullOrEmpty(connect)) config.TimeoutConnect = int.Parse(connect);
                }

                var alive = writer.GetElementByPath("CONFIGURATION/INTERNAL/ALIVE");
                if (alive != null)
                {
                    string setOut = writer.GetAttributeValue(alive, "Set_Out");
                    if (!string.IsNullOrEmpty(setOut)) config.AliveSetOut = int.Parse(setOut);
                    string setFlag = writer.GetAttributeValue(alive, "Set_Flag");
                    if (!string.IsNullOrEmpty(setFlag)) config.AliveSetFlag = int.Parse(setFlag);
                    string ping = writer.GetAttributeValue(alive, "Ping");
                    if (!string.IsNullOrEmpty(ping)) config.AlivePing = int.Parse(ping);
                }
            }

            foreach (var node in writer.GetChildElements("RECEIVE/XML", "ELEMENT"))
            {
                EKIElement elem = new EKIElement();
                elem.Tag = writer.GetAttributeValue(node, "Tag");
                string typeStr = writer.GetAttributeValue(node, "Type");
                if (!string.IsNullOrEmpty(typeStr))
                    elem.Type = (EKIDataType)Enum.Parse(typeof(EKIDataType), typeStr);
                elem.Mode = writer.GetAttributeValue(node, "Mode");
                string setOut = writer.GetAttributeValue(node, "Set_Out");
                if (!string.IsNullOrEmpty(setOut)) elem.SetOut = int.Parse(setOut);
                string setFlag = writer.GetAttributeValue(node, "Set_Flag");
                if (!string.IsNullOrEmpty(setFlag)) elem.SetFlag = int.Parse(setFlag);
                config.ReceiveElements.Add(elem);
            }

            foreach (var node in writer.GetChildElements("SEND/XML", "ELEMENT"))
            {
                EKIElement elem = new EKIElement();
                elem.Tag = writer.GetAttributeValue(node, "Tag");
                config.SendElements.Add(elem);
            }

            return config;
        }

        public static void SaveEKIConfig(string filePath, EKIConfig config)
        {
            string xml = GenerateEKIXML(config);
            var writer = new XdocumentReaderWriter();
            writer.LoadFromXmlString(xml);
            writer.SaveToFilePath(filePath);
        }

        public static EKIConfig LoadEKIConfig(string filePath)
        {
            var writer = new XdocumentReaderWriter();
            writer.LoadFromFilePath(filePath);
            string xml = writer.ToXmlString();
            return ParseEKIXML(xml);
        }
    }
}