using System;
using System.Globalization;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.ConfigFileControl.Implementations
{
    /// <summary>XML 配置载体实现：包装 <see cref="XdocumentReaderWriter"/>，把路径与读写收口到本模块。</summary>
    public class XmlConfigFile : Abstractions.IConfigFileProvider
    {
        private XdocumentReaderWriter _reader;

        /// <summary>构造：按相对目录 + 文件名创建载体。</summary>
        /// <param name="relativeDir">相对程序基目录的目录</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <param name="createIfMissing">文件缺失时是否创建</param>
        public XmlConfigFile(string relativeDir, string fileName, bool createIfMissing = true)
        {
            RelativeDir = relativeDir;
            // 文件名可能已含扩展名，仅缺时补
            //    重复拼接会得到 "xxx.xml.xml" ⇒ 只在缺扩展名时补。
            FileName = fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".xml";
            RelativePath = relativeDir + "\\" + FileName;
            _reader = new XdocumentReaderWriter(relativeDir, fileName, createIfMissing);
        }

        /// <summary>相对程序基目录的目录。</summary>
        public string RelativeDir { get; private set; }

        /// <inheritdoc/>
        public string FileName { get; private set; }

        /// <inheritdoc/>
        public string RelativePath { get; private set; }

        /// <summary>底层 XML 读写器（供本模块内做元素级操作）。</summary>
        public XdocumentReaderWriter Raw { get { return _reader; } }

        /// <inheritdoc/>
        public string Read(string section, string key, string defaultValue)
        {
            XElement parent = _reader.GetElementByPath(section);
            if (parent == null) return defaultValue;
            if (string.IsNullOrEmpty(key)) return parent.Value;
            XElement child = parent.Element(key);
            return child == null ? defaultValue : child.Value;
        }

        /// <inheritdoc/>
        public void Write(string section, string key, string value)
        {
            XElement parent = _reader.GetElementByPath(section) ?? _reader.AddElement(section);
            if (string.IsNullOrEmpty(key))
            {
                parent.Value = value;
                return;
            }
            XElement child = parent.Element(key);
            if (child == null) _reader.AddElement(parent, key, value);
            else child.Value = value;
        }

        /// <inheritdoc/>
        public bool SectionExists(string section)
        {
            return _reader.GetElementByPath(section) != null;
        }

        /// <inheritdoc/>
        public bool KeyExists(string section, string key)
        {
            XElement parent = _reader.GetElementByPath(section);
            return parent != null && parent.Element(key) != null;
        }

        /// <inheritdoc/>
        public void Reload()
        {
            _reader.Reload();
        }

        /// <inheritdoc/>
        public void Save()
        {
            _reader.SaveXdocument();
        }

        /// <summary>写 double（invariant 文化）。</summary>
        /// <param name="section">元素路径</param>
        /// <param name="key">子元素名</param>
        /// <param name="value">数值</param>
        public void WriteDouble(string section, string key, double value)
        {
            Write(section, key, value.ToString(CultureInfo.InvariantCulture));
        }
    }
}

