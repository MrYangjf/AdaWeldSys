using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdaWeldSystem.FileOperate
{
    /// <summary>
    /// INI 配置文件操作类
    /// 支持默认创建、增加、修改、读取、保存配置
    /// </summary>
    public class INIFile
    {
        #region 字段与属性

        private readonly string _filePath;
        private readonly Dictionary<string, Dictionary<string, string>> _sections;
        private readonly object _lockObj = new object();

        /// <summary>
        /// INI 文件完整路径
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// 当前已加载的节(Section)数量
        /// </summary>
        public int SectionCount
        {
            get
            {
                lock (_lockObj)
                {
                    return _sections.Count;
                }
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 INIFile 实例，若文件不存在则自动创建空文件
        /// </summary>
        /// <param name="filePath">INI 文件路径（绝对路径或相对路径）</param>
        public INIFile(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_filePath))
            {
                LoadFromFile();
            }
            else
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Create(_filePath).Dispose();
            }
        }

        #endregion

        #region 公共方法：读取

        /// <summary>
        /// 读取指定节和键的字符串值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="defaultValue">默认值（键不存在时返回）</param>
        /// <returns>键值或默认值</returns>
        public string ReadString(string section, string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(section))
                throw new ArgumentException("节名称不能为空", nameof(section));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("键名称不能为空", nameof(key));

            lock (_lockObj)
            {
                if (_sections.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var value))
                {
                    return value;
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// 读取指定节和键的整数值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>整数值或默认值</returns>
        public int ReadInt(string section, string key, int defaultValue = 0)
        {
            string value = ReadString(section, key, defaultValue.ToString());
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 读取指定节和键的双精度浮点值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>双精度值或默认值</returns>
        public double ReadDouble(string section, string key, double defaultValue = 0.0)
        {
            string value = ReadString(section, key, defaultValue.ToString());
            if (double.TryParse(value, out double result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 读取指定节和键的布尔值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>布尔值或默认值</returns>
        public bool ReadBool(string section, string key, bool defaultValue = false)
        {
            string value = ReadString(section, key, defaultValue.ToString());
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            // 兼容 1/0 表示
            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;
            return defaultValue;
        }

        /// <summary>
        /// 获取指定节下的所有键值对
        /// </summary>
        /// <param name="section">节名称</param>
        /// <returns>键值对字典（只读副本）</returns>
        public Dictionary<string, string> GetSectionKeys(string section)
        {
            if (string.IsNullOrEmpty(section))
                throw new ArgumentException("节名称不能为空", nameof(section));

            lock (_lockObj)
            {
                if (_sections.TryGetValue(section, out var keys))
                {
                    return new Dictionary<string, string>(keys, StringComparer.OrdinalIgnoreCase);
                }
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 获取所有节名称列表
        /// </summary>
        /// <returns>节名称列表</returns>
        public List<string> GetAllSections()
        {
            lock (_lockObj)
            {
                return new List<string>(_sections.Keys);
            }
        }

        /// <summary>
        /// 判断指定节是否存在
        /// </summary>
        /// <param name="section">节名称</param>
        /// <returns>是否存在</returns>
        public bool SectionExists(string section)
        {
            if (string.IsNullOrEmpty(section))
                return false;

            lock (_lockObj)
            {
                return _sections.ContainsKey(section);
            }
        }

        /// <summary>
        /// 判断指定节和键是否存在
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <returns>是否存在</returns>
        public bool KeyExists(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                return false;

            lock (_lockObj)
            {
                return _sections.TryGetValue(section, out var keys) && keys.ContainsKey(key);
            }
        }

        #endregion

        #region 公共方法：写入（增加/修改）

        /// <summary>
        /// 写入字符串值（节或键不存在则自动创建）
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="value">值</param>
        public void WriteString(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(section))
                throw new ArgumentException("节名称不能为空", nameof(section));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("键名称不能为空", nameof(key));

            lock (_lockObj)
            {
                if (!_sections.TryGetValue(section, out var keys))
                {
                    keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _sections[section] = keys;
                }
                keys[key] = value ?? string.Empty;
            }
        }

        /// <summary>
        /// 写入整数值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="value">整数值</param>
        public void WriteInt(string section, string key, int value)
        {
            WriteString(section, key, value.ToString());
        }

        /// <summary>
        /// 写入双精度浮点值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="value">双精度值</param>
        public void WriteDouble(string section, string key, double value)
        {
            WriteString(section, key, value.ToString());
        }

        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="value">布尔值</param>
        public void WriteBool(string section, string key, bool value)
        {
            WriteString(section, key, value.ToString());
        }

        #endregion

        #region 公共方法：删除

        /// <summary>
        /// 删除指定键
        /// </summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <returns>是否成功删除</returns>
        public bool DeleteKey(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                return false;

            lock (_lockObj)
            {
                if (_sections.TryGetValue(section, out var keys))
                {
                    bool removed = keys.Remove(key);
                    // 若节下无键，删除该节
                    if (keys.Count == 0)
                    {
                        _sections.Remove(section);
                    }
                    return removed;
                }
                return false;
            }
        }

        /// <summary>
        /// 删除指定节（包含该节下所有键）
        /// </summary>
        /// <param name="section">节名称</param>
        /// <returns>是否成功删除</returns>
        public bool DeleteSection(string section)
        {
            if (string.IsNullOrEmpty(section))
                return false;

            lock (_lockObj)
            {
                return _sections.Remove(section);
            }
        }

        #endregion

        #region 公共方法：文件操作

        /// <summary>
        /// 将当前内存中的配置保存到 INI 文件
        /// </summary>
        public void SaveToFile()
        {
            lock (_lockObj)
            {
                var sb = new StringBuilder();

                foreach (var sectionPair in _sections)
                {
                    sb.AppendLine($"[{sectionPair.Key}]");
                    foreach (var keyPair in sectionPair.Value)
                    {
                        sb.AppendLine($"{keyPair.Key}={keyPair.Value}");
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
            }
        }

        /// <summary>
        /// 从 INI 文件重新加载配置（会覆盖内存中的当前数据）
        /// </summary>
        public void Reload()
        {
            lock (_lockObj)
            {
                _sections.Clear();
                if (File.Exists(_filePath))
                {
                    LoadFromFile();
                }
            }
        }

        /// <summary>
        /// 清空所有节和键（不自动保存到文件）
        /// </summary>
        public void Clear()
        {
            lock (_lockObj)
            {
                _sections.Clear();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 从文件加载 INI 配置到内存
        /// </summary>
        private void LoadFromFile()
        {
            if (!File.Exists(_filePath))
                return;

            string[] lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            string currentSection = string.Empty;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                // 跳过空行和注释行
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                // 节头 [SectionName]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!_sections.ContainsKey(currentSection))
                    {
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    continue;
                }

                // 键值对 Key=Value
                int equalIndex = line.IndexOf('=');
                if (equalIndex > 0 && !string.IsNullOrEmpty(currentSection))
                {
                    string key = line.Substring(0, equalIndex).Trim();
                    string value = line.Substring(equalIndex + 1).Trim();

                    // 去除值两端引号（如果存在）
                    if (value.Length >= 2 &&
                        ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                         (value.StartsWith("'") && value.EndsWith("'"))))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    _sections[currentSection][key] = value;
                }
            }
        }

        #endregion
    }
}
