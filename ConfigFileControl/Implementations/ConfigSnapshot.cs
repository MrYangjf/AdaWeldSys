using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.ConfigFileControl.Implementations
{
    /// <summary>配置快照：把一份配置文档摊平成「路径 → 值」字典。</summary>
    public static class ConfigSnapshot
    {
        /// <summary>取 INI 载体的磁盘快照。</summary>
        /// <param name="file">INI 载体</param>
        /// <returns>节.键 → 值 的映射</returns>
        public static Dictionary<string, string> OfIni(IniConfigFile file)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (file == null) return map;

            string path = file.Raw.FilePath;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return map;

            INIFile reader = new INIFile(path);
            List<string> sections = reader.GetAllSections();
            for (int i = 0; i < sections.Count; i++)
            {
                Dictionary<string, string> keys = reader.GetSectionKeys(sections[i]);
                foreach (KeyValuePair<string, string> pair in keys)
                {
                    map[sections[i] + "." + pair.Key] = pair.Value;
                }
            }
            return map;
        }

        /// <summary>取 XML 载体的磁盘快照。</summary>
        /// <param name="file">XML 载体</param>
        /// <returns>元素路径 → 值 的映射</returns>
        public static Dictionary<string, string> OfXml(XmlConfigFile file)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (file == null) return map;

            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                file.RelativeDir, file.FileName);
            if (!System.IO.File.Exists(path)) return map;

            XDocument doc;
            try
            {
                doc = XDocument.Load(path);
            }
            catch
            {
                return map;
            }
            if (doc == null || doc.Root == null) return map;

            Walk(doc.Root, "", map);
            return map;
        }

        /// <summary>递归摊平元素树。</summary>
        /// <param name="element">当前元素</param>
        /// <param name="prefix">父路径</param>
        /// <param name="map">结果容器</param>
        private static void Walk(XElement element, string prefix, Dictionary<string, string> map)
        {
            string path = string.IsNullOrEmpty(prefix) ? element.Name.LocalName : prefix + "/" + element.Name.LocalName;
            bool hasChild = false;
            foreach (XElement child in element.Elements())
            {
                hasChild = true;
                Walk(child, path, map);
            }
            if (!hasChild)
            {
                map[path] = element.Value;
            }
        }

        /// <summary>比较两份快照并记录变动项。</summary>
        /// <param name="scene">场景名</param>
        /// <param name="before">保存前快照</param>
        /// <param name="after">保存后快照</param>
        /// <param name="tag">日志标签</param>
        public static void Diff(string scene, Dictionary<string, string> before,
                                Dictionary<string, string> after, string tag)
        {
            if (after == null) return;

            Dictionary<string, string> oldValues = before ?? new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in after)
            {
                if (!oldValues.TryGetValue(pair.Key, out string oldValue))
                {
                    oldValue = string.Empty;
                }
                if (!Same(oldValue, pair.Value))
                {
                    ConfigChangeLogger.LogField(scene, pair.Key, oldValue, pair.Value, tag);
                }
            }

            foreach (KeyValuePair<string, string> pair in oldValues)
            {
                if (!after.ContainsKey(pair.Key))
                {
                    ConfigChangeLogger.LogField(scene, pair.Key, pair.Value, string.Empty, tag);
                }
            }
        }

        /// <summary>数值等价判定（避免 1 与 1.0 的格式性误报）。</summary>
        /// <param name="before">旧值</param>
        /// <param name="after">新值</param>
        /// <returns>视为相同返回 true</returns>
        private static bool Same(string before, string after)
        {
            if (string.Equals(before, after, StringComparison.Ordinal)) return true;

            if (double.TryParse(before, NumberStyles.Any, CultureInfo.InvariantCulture, out double oldNumber)
                && double.TryParse(after, NumberStyles.Any, CultureInfo.InvariantCulture, out double newNumber))
            {
                return oldNumber == newNumber;
            }
            return false;
        }
    }
}
