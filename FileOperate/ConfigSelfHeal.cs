using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.FileOperate
{
    /// <summary>配置自愈：缺键补默认值，缺文件生成模板</summary>
    /// <remarks>统一入口，禁止各配置类自行复制补写代码。硬约束：
    /// ①只增不改：已有键不动、不删、不重排；
    /// ②幂等——无缺键时零写盘、零日志，二次启动不再写盘；
    /// ③安全——写前备份到 Config\_backup\&lt;时间戳&gt;\，临时文件 + 原子替换，只读/占用只告警不阻断；
    /// ④写后回读，缺键为 0 才算成功。失败只告警并保留默认。
    /// 默认值由调用方提供，本类不内置业务字面量。</remarks>
    public static class ConfigSelfHeal
    {
        private const string Tag = "配置自愈";

        /// <summary>INI 模板中的一项（节 + 键 + 默认值）</summary>
        public sealed class IniDefault
        {
            /// <summary>节名称</summary>
            public string Section { get; set; }

            /// <summary>键名称</summary>
            public string Key { get; set; }

            /// <summary>默认值文本</summary>
            public string Value { get; set; }
        }

        /// <summary>输出日志（日志系统未初始化时静默，不影响启动）</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level)
        {
            try
            {
                GlobalCommData.ShowLog(Tag, message, level);
            }
            catch (Exception)
            {
                // 日志系统不可用时不阻断配置加载
            }
        }

        /// <summary>INI 补缺：只增不改，缺文件时按模板生成完整配置</summary>
        /// <param name="fullPath">配置文件完整路径</param>
        /// <param name="relativePath">相对程序基目录的路径（备份用）</param>
        /// <param name="fileLabel">日志中的文件名</param>
        /// <param name="defaults">默认键值集合（来自配置类当前值，单一来源）</param>
        /// <param name="allowWrite">false = 预览读，只检测不写盘</param>
        /// <returns>是否发生了写盘</returns>
        public static bool EnsureIni(string fullPath, string relativePath, string fileLabel,
            IList<IniDefault> defaults, bool allowWrite)
        {
            if (string.IsNullOrEmpty(fullPath) || defaults == null || defaults.Count == 0)
                return false;

            if (!allowWrite)
                return false;

            try
            {
                bool fileExists = File.Exists(fullPath);
                if (!fileExists)
                {
                    if (!WriteIniTemplate(fullPath, relativePath, fileLabel, defaults))
                        return false;
                    Log(string.Format(CultureInfo.InvariantCulture, "配置缺失，已生成默认配置 {0}", fileLabel), MessageLevel.Warning);
                    return true;
                }

                ReadTextPreserveEncoding(fullPath, out string original, out Encoding encoding);
                List<IniDefault> missing = FindMissingIniKeys(original, defaults);
                if (missing.Count == 0)
                    return false;

                string patched = PatchIniText(original, missing);
                if (!WriteTextAtomically(fullPath, relativePath, patched, encoding))
                    return false;

                ReadTextPreserveEncoding(fullPath, out string verify, out Encoding verifyEncoding);
                List<IniDefault> stillMissing = FindMissingIniKeys(verify, defaults);
                if (stillMissing.Count > 0)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        "配置补缺 {0}：写入后回读校验仍有 {1} 键缺失，保留内存默认继续运行",
                        fileLabel, stillMissing.Count), MessageLevel.Warning);
                }
                else
                {
                    LogBuildSummary(fileLabel, missing, "代码默认");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format(CultureInfo.InvariantCulture, "配置补缺 {0} 失败：{1}（保留内存默认，不阻断启动）",
                    fileLabel, ex.Message), MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>XML 补缺：以类自身 Save 产物为模板，只增不改</summary>
        /// <param name="fullPath">配置文件完整路径</param>
        /// <param name="relativePath">相对程序基目录的路径（备份用）</param>
        /// <param name="fileLabel">日志中的文件名</param>
        /// <param name="template">模板文档（全部键均由配置类当前值写出）</param>
        /// <param name="allowWrite">false = 预览读，只检测不写盘</param>
        /// <returns>是否发生了写盘</returns>
        public static bool EnsureXml(string fullPath, string relativePath, string fileLabel,
            XDocument template, bool allowWrite)
        {
            if (string.IsNullOrEmpty(fullPath) || template == null || template.Root == null)
                return false;

            if (!allowWrite)
                return false;

            try
            {
                bool fileExists = File.Exists(fullPath);
                if (!fileExists)
                {
                    if (!WriteXmlAtomically(fullPath, relativePath, template))
                        return false;
                    Log(string.Format(CultureInfo.InvariantCulture, "配置缺失，已生成默认配置 {0}", fileLabel), MessageLevel.Warning);
                    VerifyXml(fullPath, template, fileLabel);
                    return true;
                }

                ReadXmlPreserve(fullPath, out XDocument document, out Encoding encoding, out bool hasDeclaration);
                if (document.Root == null)
                    return false;

                List<string> added = new List<string>();
                EnsureChildren(document.Root, template.Root, added);
                if (added.Count == 0)
                    return false;

                if (!WriteXmlAtomically(fullPath, relativePath, document, encoding, hasDeclaration))
                    return false;

                VerifyXml(fullPath, template, fileLabel);
                LogBuildSummary(fileLabel, added, "代码默认");
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format(CultureInfo.InvariantCulture, "配置补缺 {0} 失败：{1}（保留内存默认，不阻断启动）",
                    fileLabel, ex.Message), MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>由 INI 模型构造默认项集合（用于各类 INI 配置接入）</summary>
        /// <param name="section">节名称</param>
        /// <param name="key">键名称</param>
        /// <param name="value">默认值文本</param>
        /// <returns>默认项</returns>
        public static IniDefault Key(string section, string key, string value)
        {
            IniDefault item = new IniDefault
            {
                Section = section,
                Key = key,
                Value = value
            };
            return item;
        }

        #region 写盘与备份

        /// <summary>写前备份（复用既有备份助手，不新建备份体系）</summary>
        /// <param name="relativePath">相对程序基目录的路径</param>
        private static void Backup(string relativePath)
        {
            try
            {
                if (string.IsNullOrEmpty(relativePath)) return;
                string folder = ConfigBackupHelper.CreateBackupFolder();
                ConfigBackupHelper.BackupFile(relativePath, folder);
            }
            catch (Exception)
            {
                // 备份失败不阻断写入，仅在下层告警
            }
        }

        /// <summary>文本原子替换写入</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="relativePath">相对路径。</param>
        /// <param name="content">内容。</param>
        /// <returns>是否成功。</returns>
        private static bool WriteTextAtomically(string fullPath, string relativePath, string content, Encoding encoding)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            Backup(relativePath);
            string tempPath = fullPath + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(tempPath, content, encoding);
                ReplaceFile(tempPath, fullPath);
                return true;
            }
            catch (Exception ex)
            {
                TryDelete(tempPath);
                Log(string.Format(CultureInfo.InvariantCulture, "配置写盘失败 {0}：{1}（只读或被占用，保留内存默认）",
                    Path.GetFileName(fullPath), ex.Message), MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>XML 原子替换写入（保留原编码与换行风格）</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="relativePath">相对路径。</param>
        /// <param name="document">XML 文档对象。</param>
        /// <returns>是否成功。</returns>
        private static bool WriteXmlAtomically(string fullPath, string relativePath, XDocument document)
        {
            Encoding encoding = new UTF8Encoding(false);
            bool hasDeclaration = document.Declaration != null;
            return WriteXmlAtomically(fullPath, relativePath, document, encoding, hasDeclaration);
        }

        /// <summary>XML 原子替换写入</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="relativePath">相对路径。</param>
        /// <param name="document">XML 文档对象。</param>
        /// <returns>是否成功。</returns>
        private static bool WriteXmlAtomically(string fullPath, string relativePath, XDocument document,
            Encoding encoding, bool hasDeclaration)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            Backup(relativePath);
            string tempPath = fullPath + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Encoding = encoding,
                    Indent = false,
                    NewLineHandling = NewLineHandling.None,
                    OmitXmlDeclaration = !hasDeclaration
                };
                using (XmlWriter writer = XmlWriter.Create(tempPath, settings))
                {
                    document.Save(writer);
                }
                ReplaceFile(tempPath, fullPath);
                return true;
            }
            catch (Exception ex)
            {
                TryDelete(tempPath);
                Log(string.Format(CultureInfo.InvariantCulture, "配置写盘失败 {0}：{1}（只读或被占用，保留内存默认）",
                    Path.GetFileName(fullPath), ex.Message), MessageLevel.Warning);
                return false;
            }
        }

        /// <summary>临时文件原子替换目标文件</summary>
        /// <param name="fullPath">完整路径。</param>
        private static void ReplaceFile(string tempPath, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                // File.Replace 在同盘内原子替换；目标只读/被占用时抛异常，由上层捕获
                File.Replace(tempPath, fullPath, null);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }

        /// <summary>静默删除临时文件</summary>
        /// <param name="path">路径。</param>
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // 清理失败不影响主流程
            }
        }

        #endregion

        #region INI 解析与补写

        /// <summary>读文本并识别编码（保留 BOM 状态）</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="text">文本内容。</param>
        private static void ReadTextPreserveEncoding(string fullPath, out string text, out Encoding encoding)
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            encoding = new UTF8Encoding(hasBom);
            text = File.ReadAllText(fullPath, encoding);
        }

        /// <summary>扫描现有 INI 文本，列出模板中不存在的键</summary>
        /// <param name="text">文本内容。</param>
        /// <returns>结果集合。</returns>
        private static List<IniDefault> FindMissingIniKeys(string text, IList<IniDefault> defaults)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string section = string.Empty;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int equal = line.IndexOf('=');
                if (equal > 0)
                {
                    string key = line.Substring(0, equal).Trim();
                    existing.Add(section + "\u0001" + key);
                }
            }

            List<IniDefault> missing = new List<IniDefault>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < defaults.Count; i++)
            {
                IniDefault item = defaults[i];
                if (item == null || string.IsNullOrEmpty(item.Section) || string.IsNullOrEmpty(item.Key))
                    continue;
                string id = item.Section + "\u0001" + item.Key;
                if (existing.Contains(id) || !seen.Add(id))
                    continue;
                missing.Add(item);
            }
            return missing;
        }

        /// <summary>把缺失键插入原文本</summary>
        /// <remarks>节内末尾追加，节缺失则文末追加；其余内容逐字保留</remarks>
        /// <returns>结果文本。</returns>
        private static string PatchIniText(string original, List<IniDefault> missing)
        {
            string newline = original.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            List<string> lines = original.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

            Dictionary<string, int> sectionEnd = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> sectionStart = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string current = null;
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    if (current != null) sectionEnd[current] = FindSectionInsertIndex(lines, sectionStart[current], i);
                    current = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    sectionStart[current] = i;
                    sectionEnd[current] = i;
                }
            }
            if (current != null) sectionEnd[current] = FindSectionInsertIndex(lines, sectionStart[current], lines.Count);

            // 先按节分组，保持模板顺序
            List<string> sectionOrder = new List<string>();
            Dictionary<string, List<IniDefault>> bySection =
                new Dictionary<string, List<IniDefault>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < missing.Count; i++)
            {
                string section = missing[i].Section;
                if (!bySection.TryGetValue(section, out List<IniDefault> list))
                {
                    list = new List<IniDefault>();
                    bySection[section] = list;
                    sectionOrder.Add(section);
                }
                list.Add(missing[i]);
            }

            // 已存在节按插入位置倒序处理，避免行号漂移；缺失节追加到文末
            List<KeyValuePair<int, List<IniDefault>>> existingInserts =
                new List<KeyValuePair<int, List<IniDefault>>>();
            List<string> newSections = new List<string>();
            for (int s = 0; s < sectionOrder.Count; s++)
            {
                string section = sectionOrder[s];
                List<IniDefault> items = bySection[section];
                if (sectionEnd.ContainsKey(section))
                    existingInserts.Add(new KeyValuePair<int, List<IniDefault>>(sectionEnd[section], items));
                else
                    newSections.Add(section);
            }
            existingInserts.Sort(delegate(KeyValuePair<int, List<IniDefault>> left, KeyValuePair<int, List<IniDefault>> right)
            {
                return right.Key.CompareTo(left.Key);
            });
            for (int s = 0; s < existingInserts.Count; s++)
            {
                int insertAt = existingInserts[s].Key;
                List<IniDefault> items = existingInserts[s].Value;
                List<string> newLines = new List<string>();
                for (int i = 0; i < items.Count; i++)
                    newLines.Add(items[i].Key + "=" + (items[i].Value ?? string.Empty));
                lines.InsertRange(insertAt, newLines);
            }
            for (int s = 0; s < newSections.Count; s++)
            {
                string section = newSections[s];
                List<IniDefault> items = bySection[section];
                if (lines.Count > 0 && lines[lines.Count - 1].Length > 0)
                    lines.Add(string.Empty);
                lines.Add("[" + section + "]");
                for (int i = 0; i < items.Count; i++)
                    lines.Add(items[i].Key + "=" + (items[i].Value ?? string.Empty));
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) builder.Append(newline);
                builder.Append(lines[i]);
            }
            return builder.ToString();
        }

        /// <summary>求节内插入位置</summary>
        /// <param name="lines">文本行集合。</param>
        /// <remarks>最后一个非空行之后，保留尾部空行与注释块</remarks>
        /// <returns>结果码。</returns>
        private static int FindSectionInsertIndex(List<string> lines, int sectionStart, int sectionEndExclusive)
        {
            int insertAt = sectionStart + 1;
            for (int i = sectionStart + 1; i < sectionEndExclusive; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith(";") && !trimmed.StartsWith("#"))
                    insertAt = i + 1;
            }
            return insertAt;
        }

        /// <summary>缺文件时按模板生成完整 INI</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="relativePath">相对路径。</param>
        /// <param name="fileLabel">文件标签。</param>
        /// <returns>是否成功。</returns>
        private static bool WriteIniTemplate(string fullPath, string relativePath, string fileLabel,
            IList<IniDefault> defaults)
        {
            StringBuilder builder = new StringBuilder();
            List<string> sections = new List<string>();
            Dictionary<string, List<IniDefault>> bySection =
                new Dictionary<string, List<IniDefault>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < defaults.Count; i++)
            {
                string section = defaults[i].Section;
                if (!bySection.TryGetValue(section, out List<IniDefault> list))
                {
                    list = new List<IniDefault>();
                    bySection[section] = list;
                    sections.Add(section);
                }
                list.Add(defaults[i]);
            }
            for (int s = 0; s < sections.Count; s++)
            {
                if (s > 0) builder.Append("\r\n");
                builder.Append("[" + sections[s] + "]\r\n");
                List<IniDefault> items = bySection[sections[s]];
                for (int i = 0; i < items.Count; i++)
                    builder.Append(items[i].Key + "=" + (items[i].Value ?? string.Empty) + "\r\n");
            }
            return WriteTextAtomically(fullPath, relativePath, builder.ToString(), new UTF8Encoding(false));
        }

        #endregion

        #region XML 解析与补写

        /// <summary>读 XML 并保留空白、编码与声明状态</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="document">XML 文档对象。</param>
        private static void ReadXmlPreserve(string fullPath, out XDocument document, out Encoding encoding,
            out bool hasDeclaration)
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            encoding = new UTF8Encoding(hasBom);
            document = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace);
            hasDeclaration = document.Declaration != null;
        }

        /// <summary>按模板递归补子节点</summary>
        /// <remarks>仅当同名节点不存在时追加到同级末尾</remarks>
        private static void EnsureChildren(XElement realParent, XElement templateParent, List<string> added)
        {
            foreach (XElement templateChild in templateParent.Elements())
            {
                XElement matched = FindMatch(realParent, templateChild);
                if (matched != null)
                {
                    if (templateChild.HasElements)
                        EnsureChildren(matched, templateChild, added);
                    continue;
                }

                XElement clone = new XElement(templateChild);
                InsertAtTail(realParent, clone);
                added.Add(BuildDisplayName(templateChild));
            }
        }

        /// <summary>在同级中按业务名匹配模板节点，避免索引错位</summary>
        /// <param name="parent">父节点。</param>
        /// <remarks>导致重复节点</remarks>
        private static XElement FindMatch(XElement parent, XElement templateNode)
        {
            string name = templateNode.Name.LocalName;
            IEnumerable<XElement> candidates = parent.Elements(name);
            XAttribute templateAttr = templateNode.Attribute("Name");
            if (templateAttr != null)
            {
                return candidates.FirstOrDefault(e =>
                    e.Attribute("Name") != null && string.Equals(e.Attribute("Name").Value, templateAttr.Value, StringComparison.Ordinal));
            }

            string[] keyNames = new string[] { "AXIS_NAME", "IO_NAME", "CYLINDER_NAME", "CYLINDER", "KEY" };
            for (int i = 0; i < keyNames.Length; i++)
            {
                XElement templateKey = templateNode.Element(keyNames[i]);
                if (templateKey == null) continue;
                string key = templateKey.Value;
                return candidates.FirstOrDefault(e =>
                {
                    XElement realKey = e.Element(keyNames[i]);
                    return realKey != null && string.Equals(realKey.Value, key, StringComparison.Ordinal);
                });
            }

            return candidates.FirstOrDefault();
        }

        /// <summary>把新节点追加到同级末尾，并沿用同级缩进与换</summary>
        /// <param name="parent">父节点。</param>
        /// <remarks>行风格</remarks>
        private static void InsertAtTail(XElement parent, XElement child)
        {
            string newline = DetectNewline(parent.Document);
            string indent = DetectChildIndent(parent);
            XElement last = parent.Elements().LastOrDefault();
            if (last != null)
            {
                last.AddAfterSelf(new XText(newline + indent), child);
            }
            else if (parent.IsEmpty)
            {
                string parentIndent = DetectNodeIndent(parent);
                parent.Add(new XText(newline + indent), child, new XText(newline + parentIndent));
            }
            else
            {
                parent.AddFirst(new XText(newline + indent), child);
            }
        }

        /// <summary>识别文档换行风格</summary>
        /// <param name="document">XML 文档对象。</param>
        /// <returns>结果文本。</returns>
        private static string DetectNewline(XDocument document)
        {
            if (document == null) return "\r\n";
            foreach (XText text in document.DescendantNodes().OfType<XText>())
            {
                if (text.Value.IndexOf('\n') >= 0)
                    return text.Value.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            }
            return "\r\n";
        }

        /// <summary>取父节点首个子节点的缩进；无子节点时按父缩</summary>
        /// <param name="parent">父节点。</param>
        /// <remarks>进 + 4 空格</remarks>
        /// <returns>结果文本。</returns>
        private static string DetectChildIndent(XElement parent)
        {
            XText first = parent.Nodes().OfType<XText>().FirstOrDefault();
            if (first != null && first.Value.IndexOf('\n') >= 0)
            {
                string value = first.Value;
                int index = value.LastIndexOf('\n');
                if (index >= 0) return value.Substring(index + 1);
            }
            return DetectNodeIndent(parent) + "    ";
        }

        /// <summary>取节点自身行首缩进</summary>
        /// <param name="node">配置节点。</param>
        /// <returns>结果文本。</returns>
        private static string DetectNodeIndent(XElement node)
        {
            if (node.PreviousNode is XText previous && previous.Value.IndexOf('\n') >= 0)
            {
                string value = previous.Value;
                int index = value.LastIndexOf('\n');
                if (index >= 0) return value.Substring(index + 1);
            }
            return string.Empty;
        }

        /// <summary>日志展示用的节点路径</summary>
        /// <param name="node">配置节点。</param>
        /// <remarks>同层按名称即可，异常场景由回读校验兜底</remarks>
        /// <returns>结果文本。</returns>
        private static string BuildDisplayName(XElement node)
        {
            List<string> names = new List<string>();
            XElement current = node;
            while (current != null && current.Parent != null)
            {
                names.Insert(0, current.Name.LocalName);
                current = current.Parent;
            }
            return string.Join("/", names.ToArray());
        }

        /// <summary>写后回读：再次对比模板，缺键数应为 0</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <param name="fileLabel">文件标签。</param>
        private static void VerifyXml(string fullPath, XDocument template, string fileLabel)
        {
            try
            {
                XDocument verify = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace);
                if (verify.Root == null || template.Root == null) return;
                List<string> stillMissing = new List<string>();
                EnsureChildren(verify.Root, template.Root, stillMissing);
                if (stillMissing.Count > 0)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        "配置补缺 {0}：写入后回读校验仍有 {1} 节点缺失，保留内存默认继续运行",
                        fileLabel, stillMissing.Count), MessageLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log(string.Format(CultureInfo.InvariantCulture, "配置补缺 {0} 回读校验失败：{1}",
                    fileLabel, ex.Message), MessageLevel.Warning);
            }
        }

        #endregion

        /// <summary>聚合日志</summary>
        /// <param name="fileLabel">文件标签。</param>
        /// <param name="keys">键名集合。</param>
        /// <param name="source">数据来源。</param>
        /// <remarks>单文件一条，逐键带默认值与来源，不刷屏</remarks>
        private static void LogBuildSummary(string fileLabel, IList<string> keys, string source)
        {
            LogBuildSummary(fileLabel, keys.ToList(), source);
        }

        /// <summary>聚合日志（INI 版）</summary>
        /// <param name="fileLabel">文件标签。</param>
        /// <param name="keys">键名集合。</param>
        /// <param name="source">数据来源。</param>
        private static void LogBuildSummary(string fileLabel, IList<IniDefault> keys, string source)
        {
            List<string> texts = new List<string>();
            for (int i = 0; i < keys.Count; i++)
                texts.Add(keys[i].Section + "/" + keys[i].Key + "=" + (keys[i].Value ?? string.Empty) + "(" + source + ")");
            LogBuildSummary(fileLabel, texts, source);
        }

        /// <summary>聚合日志公共实现</summary>
        /// <param name="fileLabel">文件标签。</param>
        /// <param name="keys">键名集合。</param>
        /// <param name="source">数据来源。</param>
        private static void LogBuildSummary(string fileLabel, List<string> keys, string source)
        {
            const int MaxShow = 8;
            StringBuilder builder = new StringBuilder();
            builder.Append("配置补缺 ").Append(fileLabel).Append("：新增 ")
                .Append(keys.Count.ToString(CultureInfo.InvariantCulture)).Append(" 键（");
            for (int i = 0; i < keys.Count && i < MaxShow; i++)
            {
                if (i > 0) builder.Append("、");
                builder.Append(keys[i]);
            }
            if (keys.Count > MaxShow)
                builder.Append(" 等 ").Append(keys.Count.ToString(CultureInfo.InvariantCulture)).Append(" 项");
            builder.Append("；来源 ").Append(source).Append("）");
            Log(builder.ToString(), MessageLevel.Warning);
        }
    }
}
