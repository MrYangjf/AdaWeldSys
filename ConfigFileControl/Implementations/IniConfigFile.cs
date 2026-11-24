using System.Collections.Generic;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.ConfigFileControl.Implementations
{
    /// <summary>INI 配置载体实现：包装 <see cref="INIFile"/>，把路径与读写收口到本模块。</summary>
    public class IniConfigFile : Abstractions.IConfigFileProvider
    {
        private readonly INIFile _ini;

        /// <summary>构造：按绝对路径创建载体。</summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <param name="fileName">文件名（对外标识）</param>
        /// <param name="relativePath">相对程序基目录的路径（备份用）</param>
        public IniConfigFile(string filePath, string fileName, string relativePath)
        {
            _ini = new INIFile(filePath);
            FileName = fileName;
            RelativePath = relativePath;
        }

        /// <summary>底层 INI 读写器（供本模块内做节级操作）。</summary>
        public INIFile Raw { get { return _ini; } }

        /// <inheritdoc/>
        public string FileName { get; private set; }

        /// <inheritdoc/>
        public string RelativePath { get; private set; }

        /// <inheritdoc/>
        public string Read(string section, string key, string defaultValue)
        {
            return _ini.ReadString(section, key, defaultValue);
        }

        /// <inheritdoc/>
        public void Write(string section, string key, string value)
        {
            _ini.WriteString(section, key, value);
        }

        /// <inheritdoc/>
        public bool SectionExists(string section)
        {
            return _ini.SectionExists(section);
        }

        /// <inheritdoc/>
        public bool KeyExists(string section, string key)
        {
            return _ini.KeyExists(section, key);
        }

        /// <inheritdoc/>
        public void Reload()
        {
            _ini.Reload();
        }

        /// <inheritdoc/>
        public void Save()
        {
            _ini.SaveToFile();
        }

        /// <summary>取节的全部键值（模板生成用）。</summary>
        /// <param name="section">节名</param>
        /// <returns>键值字典（无此节返回空字典）</returns>
        public Dictionary<string, string> SectionKeys(string section)
        {
            return _ini.GetSectionKeys(section);
        }
    }
}
