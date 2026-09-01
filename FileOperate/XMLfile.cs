using AdaWeldSystem.Comm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AdaWeldSystem.FileOperate
{
    /// <summary>
    /// XML文档读写器，提供对指定XML配置文件的加载、保存及常用节点值的读取与写入功能。
    /// </summary>
    public class XdocumentReaderWriter
    {
        /// <summary>
        /// 根元素名称常量。
        /// </summary>
        private const string RootElementName = "Root";

        /// <summary>
        /// IP节点名称常量。
        /// </summary>
        private const string IpElementName = "IP";

        /// <summary>
        /// 端口节点名称常量。
        /// </summary>
        private const string PortElementName = "PORT";

        /// <summary>
        /// 配置名称节点常量。
        /// </summary>
        private const string ConfigNameElement = "ConfigName";

        /// <summary>
        /// XML文件默认扩展名常量。
        /// </summary>
        private const string XmlExtension = ".xml";

        /// <summary>
        /// 当前操作的XML文档对象。
        /// </summary>
        public XDocument _xDocument;

        /// <summary>
        /// 字符串读取器，用于从XML字符串加载文档。
        /// </summary>
        private TextReader _reader;

        /// <summary>
        /// XML 配置文件所在目录路径（Config/XML/ 子文件夹）。
        /// </summary>
        public string _configDirectory = AppDomain.CurrentDomain.BaseDirectory + @"\Config\XML\";

        /// <summary>
        /// XML配置文件名。
        /// </summary>
        private string _configFileName = "Config.xml";

        /// <summary>
        /// 初始化 <see cref="XdocumentReaderWriter"/> 类的新实例，并加载或创建指定的XML文件。
        /// </summary>
        /// <param name="xmlName">XML文件名称（不含扩展名时会自动追加.xml）。</param>
        public XdocumentReaderWriter(string xmlName)
        {
            if (!string.IsNullOrEmpty(xmlName))
            {
                if (!xmlName.Contains(XmlExtension))
                {
                    xmlName += XmlExtension;
                }
                _configFileName = xmlName;
            }

            string configXmlPath = _configDirectory + _configFileName;
            if (CheckFile(configXmlPath))
            {
                _xDocument = XDocument.Load(configXmlPath);
            }
            else
            {
                NewXdocument();
                SaveXdocument();
            }
        }

        /// <summary>
        /// 检查配置文件目录及文件是否存在，目录不存在时自动创建。
        /// </summary>
        /// <param name="configXmlPath">配置文件完整路径。</param>
        /// <returns>文件是否存在。</returns>
        private bool CheckFile(string configXmlPath)
        {
            bool checkResult = true;
            if (!Directory.Exists(_configDirectory))
            {
                checkResult = false;
                Directory.CreateDirectory(_configDirectory);
            }
            if (!File.Exists(configXmlPath))
            {
                checkResult = false;
            }
            return checkResult;
        }

        /// <summary>
        /// 创建一个新的XML文档，仅包含根元素。
        /// </summary>
        public void NewXdocument()
        {
            _xDocument = new XDocument(new XElement(RootElementName));
        }

        /// <summary>
        /// 创建带XML声明和指定根元素的新文档。
        /// </summary>
        /// <param name="rootName">根元素名称。</param>
        /// <param name="version">XML版本，默认为1.0。</param>
        /// <param name="encoding">编码格式，默认为UTF-8。</param>
        public void NewXdocument(string rootName, string version = "1.0", string encoding = "UTF-8")
        {
            _xDocument = new XDocument(
                new XDeclaration(version, encoding, null),
                new XElement(rootName)
            );
        }

        /// <summary>
        /// 将当前XML文档保存到配置文件路径。
        /// </summary>
        public void SaveXdocument()
        {
            string configXmlPath = _configDirectory + _configFileName;
            if (_xDocument != null)
            {
                _xDocument.Save(configXmlPath);
            }
            else
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(System.Windows.Forms.Form.ActiveForm, "错误", "未打开任何文件，无法保存", AntdUI.TType.Error) { Draggable = true });
            }
        }

        /// <summary>
        /// 从XML文档中读取IP地址。
        /// </summary>
        /// <returns>IP地址字符串；读取失败时返回空字符串。</returns>
        public string GetIp()
        {
            try
            {
                string ip = _xDocument?.Descendants(IpElementName).FirstOrDefault()?.Value ?? string.Empty;
                return ip;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("XMLfile.GetIp", $"读取IP失败 异常信息 {ex.Message}", MessageLevel.Error);
                return string.Empty;
            }
        }

        /// <summary>
        /// 从XML文档中读取端口号。
        /// </summary>
        /// <returns>端口号；读取或解析失败时返回 -1。</returns>
        public int GetPort()
        {
            try
            {
                int port = int.Parse(_xDocument.Descendants(PortElementName).FirstOrDefault()?.Value ?? "-1");
                return port;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("XMLfile.GetPort", $"读取端口失败 异常信息 {ex.Message}", MessageLevel.Error);
                return -1;
            }
        }

        /// <summary>
        /// 从XML文档中读取配置名称。
        /// </summary>
        /// <returns>配置名称字符串；读取失败时返回空字符串。</returns>
        public string GetConfig()
        {
            try
            {
                string configName = _xDocument.Descendants(ConfigNameElement).FirstOrDefault()?.Value ?? string.Empty;
                return configName;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("XMLfile.GetConfig", $"读取配置名称失败 异常信息 {ex.Message}", MessageLevel.Error);
                return string.Empty;
            }
        }

        /// <summary>
        /// 设置XML文档中的配置名称并保存。
        /// </summary>
        /// <param name="name">要设置的配置名称。</param>
        public void SetConfig(string name)
        {
            try
            {
                var element = _xDocument.Descendants(ConfigNameElement).FirstOrDefault();
                if (element != null)
                {
                    element.Value = name;
                    SaveXdocument();
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("XMLfile.SetConfig", $"设置配置名称失败 异常信息 {ex.Message}", MessageLevel.Error);
            }
        }

        /// <summary>
        /// 获取当前XML文档的无格式化字符串表示，用于网络发送。
        /// </summary>
        /// <returns>XML字符串。</returns>
        public string GetXmlSendStr()
        {
            string xmlConvertString = _xDocument.ToString(SaveOptions.DisableFormatting);
            return xmlConvertString;
        }

        /// <summary>
        /// 从XML字符串中读取指定元素和属性的值。
        /// </summary>
        /// <param name="xmlStr">XML字符串。</param>
        /// <param name="element">元素名称。</param>
        /// <param name="attribute">属性名称。</param>
        /// <returns>属性值字符串。</returns>
        public string GetXmlStrValue(string xmlStr, string element, string attribute)
        {
            string valueStr;
            _reader = new StringReader(xmlStr);
            _xDocument = XDocument.Load(_reader);
            valueStr = _xDocument.Descendants(element).FirstOrDefault()?.Attribute(attribute)?.Value;
            return valueStr;
        }

        /// <summary>
        /// 从XML字符串中读取指定元素的值。
        /// </summary>
        /// <param name="xmlStr">XML字符串。</param>
        /// <param name="element">元素名称。</param>
        /// <returns>元素值字符串。</returns>
        public string GetXmlStrValue(string xmlStr, string element)
        {
            string valueStr;
            _reader = new StringReader(xmlStr);
            _xDocument = XDocument.Load(_reader);
            valueStr = _xDocument.Descendants(element).FirstOrDefault()?.Value;
            return valueStr;
        }

        /// <summary>
        /// 无参构造函数，创建空的XML文档。
        /// </summary>
        public XdocumentReaderWriter()
        {
            NewXdocument();
        }

        /// <summary>
        /// 从XML字符串加载文档。
        /// </summary>
        public void LoadFromXmlString(string xmlContent)
        {
            _reader = new StringReader(xmlContent);
            _xDocument = XDocument.Load(_reader);
        }

        /// <summary>
        /// 从指定文件路径加载文档。
        /// </summary>
        public void LoadFromFilePath(string filePath)
        {
            _xDocument = XDocument.Load(filePath);
        }

        /// <summary>
        /// 保存到指定文件路径（自动创建目录）。
        /// </summary>
        public void SaveToFilePath(string filePath)
        {
            if (_xDocument != null)
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                _xDocument.Save(filePath);
            }
            else
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(System.Windows.Forms.Form.ActiveForm, "错误", "未打开任何文件，无法保存", AntdUI.TType.Error) { Draggable = true });
            }
        }

        /// <summary>
        /// 获取格式化XML字符串。
        /// </summary>
        public string ToXmlString()
        {
            return _xDocument?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 获取当前XDocument对象（供外部解析使用）。
        /// </summary>
        public XDocument GetXDocument()
        {
            return _xDocument;
        }

        /// <summary>
        /// 设置当前XDocument对象。
        /// </summary>
        public void SetXDocument(XDocument doc)
        {
            _xDocument = doc;
        }

        #region 通用XML操作方法

        /// <summary>
        /// 在根元素下添加子元素。
        /// </summary>
        /// <param name="elementName">子元素名称。</param>
        /// <param name="value">元素值，可为null。</param>
        /// <returns>新创建的元素。</returns>
        public XElement AddElement(string elementName, string value = null)
        {
            var elem = new XElement(elementName);
            if (value != null)
                elem.Value = value;
            _xDocument.Root?.Add(elem);
            return elem;
        }

        /// <summary>
        /// 在指定父元素下添加子元素。
        /// </summary>
        /// <param name="parent">父元素。</param>
        /// <param name="elementName">子元素名称。</param>
        /// <param name="value">元素值，可为null。</param>
        /// <returns>新创建的元素。</returns>
        public XElement AddElement(XElement parent, string elementName, string value = null)
        {
            var elem = new XElement(elementName);
            if (value != null)
                elem.Value = value;
            parent?.Add(elem);
            return elem;
        }

        /// <summary>
        /// 在指定父元素下添加带属性的子元素。
        /// </summary>
        /// <param name="parent">父元素。</param>
        /// <param name="elementName">子元素名称。</param>
        /// <param name="attributes">属性字典。</param>
        /// <param name="value">元素值，可为null。</param>
        /// <returns>新创建的元素。</returns>
        public XElement AddElementWithAttributes(XElement parent, string elementName, Dictionary<string, string> attributes, string value = null)
        {
            var elem = new XElement(elementName);
            if (value != null)
                elem.Value = value;
            if (attributes != null)
            {
                foreach (var attr in attributes)
                {
                    elem.SetAttributeValue(attr.Key, attr.Value);
                }
            }
            parent?.Add(elem);
            return elem;
        }

        /// <summary>
        /// 获取指定名称的第一个元素（全文搜索）。
        /// </summary>
        /// <param name="elementName">元素名称。</param>
        /// <returns>匹配的元素，未找到返回null。</returns>
        public XElement GetElement(string elementName)
        {
            return _xDocument?.Descendants(elementName).FirstOrDefault();
        }

        /// <summary>
        /// 按路径获取元素，路径用/分隔。支持从根子元素开始的路径。
        /// </summary>
        /// <param name="path">元素路径，如"CONFIG/IP_NUMBER"或"ROOT/CONFIG/IP_NUMBER"。</param>
        /// <returns>匹配的元素，未找到返回null。</returns>
        public XElement GetElementByPath(string path)
        {
            if (_xDocument?.Root == null || string.IsNullOrEmpty(path))
                return null;
            string[] parts = path.Split('/');
            XElement current = _xDocument.Root;
            int startIndex = 0;
            if (parts[0] == current.Name.LocalName)
                startIndex = 1;
            for (int i = startIndex; i < parts.Length; i++)
            {
                current = current.Element(parts[i]);
                if (current == null)
                    return null;
            }
            return current;
        }

        /// <summary>
        /// 获取指定名称的所有元素（全文搜索）。
        /// </summary>
        /// <param name="elementName">元素名称。</param>
        /// <returns>元素集合。</returns>
        public IEnumerable<XElement> GetElements(string elementName)
        {
            return _xDocument?.Descendants(elementName) ?? Enumerable.Empty<XElement>();
        }

        /// <summary>
        /// 获取指定父路径下的所有指定名称的子元素。
        /// </summary>
        /// <param name="parentPath">父元素路径。</param>
        /// <param name="childName">子元素名称。</param>
        /// <returns>子元素集合。</returns>
        public IEnumerable<XElement> GetChildElements(string parentPath, string childName)
        {
            var parent = GetElementByPath(parentPath);
            return parent?.Elements(childName) ?? Enumerable.Empty<XElement>();
        }

        /// <summary>
        /// 获取指定名称的第一个元素的值。
        /// </summary>
        /// <param name="elementName">元素名称。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>元素值，未找到返回默认值。</returns>
        public string GetElementValue(string elementName, string defaultValue = "")
        {
            return GetElement(elementName)?.Value ?? defaultValue;
        }

        /// <summary>
        /// 按路径获取元素的值。
        /// </summary>
        /// <param name="path">元素路径。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>元素值，未找到返回默认值。</returns>
        public string GetElementValueByPath(string path, string defaultValue = "")
        {
            return GetElementByPath(path)?.Value ?? defaultValue;
        }

        /// <summary>
        /// 设置指定名称的第一个元素的值。
        /// </summary>
        /// <param name="elementName">元素名称。</param>
        /// <param name="value">要设置的值。</param>
        public void SetElementValue(string elementName, string value)
        {
            var elem = GetElement(elementName);
            if (elem != null)
                elem.Value = value;
        }

        /// <summary>
        /// 获取元素的属性值。
        /// </summary>
        /// <param name="element">元素对象。</param>
        /// <param name="attributeName">属性名称。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>属性值，未找到返回默认值。</returns>
        public string GetAttributeValue(XElement element, string attributeName, string defaultValue = "")
        {
            return element?.Attribute(attributeName)?.Value ?? defaultValue;
        }

        /// <summary>
        /// 设置元素的属性值。
        /// </summary>
        /// <param name="element">元素对象。</param>
        /// <param name="attributeName">属性名称。</param>
        /// <param name="value">属性值。</param>
        public void SetAttributeValue(XElement element, string attributeName, string value)
        {
            element?.SetAttributeValue(attributeName, value);
        }

        #endregion
    }
}
