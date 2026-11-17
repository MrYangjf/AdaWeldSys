// IO 表解析器：只解析文件格式，不内置 PDO 清单
// 供应商每次更新 EtherCAT协议.xlsx，行数与列值均以表为准（ADR-079）。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>xlsx 只读解析器（BCL 实现）</summary>
    /// <remarks>
    /// 仅依赖 <c>System.IO.Compression</c> + <c>System.Xml.Linq</c>，不引入任何第三方库。
    /// 支持 sharedStrings、inlineStr、数值单元格与 mergeCells（合并区按左上角值回填）。
    /// 只读第一张表，足以覆盖焊接头 IO 表（单 sheet）。
    /// </remarks>
    public static class XlsxSheetReader
    {
        #region 私有变量

        private const string SharedStringsPath = "xl/sharedStrings.xml";
        private const string WorkbookPath = "xl/workbook.xml";
        private const string WorkbookRelsPath = "xl/_rels/workbook.xml.rels";
        private const string FallbackSheetPath = "xl/worksheets/sheet1.xml";

        #endregion

        #region 公共函数

        /// <summary>读取 xlsx 第一张工作表的全部非空行</summary>
        /// <param name="path">xlsx 绝对路径</param>
        /// <param name="rows">成功时回传行列表（每行按列展开）</param>
        /// <param name="error">失败时回传的错误描述</param>
        /// <returns>读取成功返回 true</returns>
        public static bool TryReadFirstSheet(string path, out List<string[]> rows, out string error)
        {
            rows = new List<string[]>();
            error = string.Empty;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "文件不存在 " + path;
                return false;
            }

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    List<string> shared = ReadSharedStrings(archive);
                    string sheetPath = ResolveFirstSheetPath(archive);

                    XDocument sheet = LoadXml(archive, sheetPath);
                    if (sheet == null || sheet.Root == null)
                    {
                        error = "缺少工作表 " + sheetPath;
                        return false;
                    }

                    rows = ReadSheetRows(sheet, shared);
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = "xlsx 解析失败 " + Path.GetFileName(path) + " " + ex.Message;
                return false;
            }
        }

        #endregion

        #region 私有函数

        /// <summary>读取共享字符串表</summary>
        /// <param name="archive">xlsx 压缩包</param>
        /// <returns>按索引排列的字符串列表（缺失时返回空表）</returns>
        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> list = new List<string>();
            XDocument doc = LoadXml(archive, SharedStringsPath);
            if (doc == null || doc.Root == null) return list;

            foreach (XElement si in doc.Root.Elements())
            {
                if (!string.Equals(si.Name.LocalName, "si", StringComparison.Ordinal)) continue;
                list.Add(ConcatText(si));
            }
            return list;
        }

        /// <summary>解析 workbook 关系，定位第一张工作表的包内路径</summary>
        /// <param name="archive">xlsx 压缩包</param>
        /// <returns>工作表路径</returns>
        private static string ResolveFirstSheetPath(ZipArchive archive)
        {
            string fallback = FallbackSheetPath;
            XDocument workbook = LoadXml(archive, WorkbookPath);
            if (workbook == null || workbook.Root == null) return fallback;

            XElement sheet = null;
            foreach (XElement element in workbook.Root.Descendants())
            {
                if (string.Equals(element.Name.LocalName, "sheet", StringComparison.Ordinal))
                {
                    sheet = element;
                    break;
                }
            }
            if (sheet == null) return fallback;

            string relationId = GetAttribute(sheet, "id");
            if (string.IsNullOrEmpty(relationId)) return fallback;

            XDocument rels = LoadXml(archive, WorkbookRelsPath);
            if (rels == null || rels.Root == null) return fallback;

            foreach (XElement rel in rels.Root.Elements())
            {
                if (!string.Equals(rel.Name.LocalName, "Relationship", StringComparison.Ordinal)) continue;
                if (!string.Equals(GetAttribute(rel, "Id"), relationId, StringComparison.Ordinal)) continue;

                string target = GetAttribute(rel, "Target");
                if (string.IsNullOrEmpty(target)) return fallback;
                target = target.Replace('\\', '/');
                if (target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return target;
                return "xl/" + target.TrimStart('/');
            }
            return fallback;
        }

        /// <summary>读取工作表行数据（含 mergeCells 回填）</summary>
        /// <param name="sheet">工作表 XML</param>
        /// <param name="shared">共享字符串表</param>
        /// <returns>行列表</returns>
        private static List<string[]> ReadSheetRows(XDocument sheet, List<string> shared)
        {
            List<string[]> result = new List<string[]>();
            XElement sheetData = null;
            foreach (XElement element in sheet.Root.Descendants())
            {
                if (string.Equals(element.Name.LocalName, "sheetData", StringComparison.Ordinal))
                {
                    sheetData = element;
                    break;
                }
            }
            if (sheetData == null) return result;

            foreach (XElement row in sheetData.Elements())
            {
                if (!string.Equals(row.Name.LocalName, "row", StringComparison.Ordinal)) continue;

                Dictionary<int, string> cells = new Dictionary<int, string>();
                int maxColumn = -1;

                foreach (XElement cell in row.Elements())
                {
                    if (!string.Equals(cell.Name.LocalName, "c", StringComparison.Ordinal)) continue;

                    int column = ColumnIndex(GetAttribute(cell, "r"));
                    if (column < 0) column = maxColumn + 1;
                    string text = CellText(cell, shared);
                    cells[column] = text;
                    if (column > maxColumn) maxColumn = column;
                }

                if (maxColumn < 0)
                {
                    result.Add(new string[0]);
                    continue;
                }

                string[] line = new string[maxColumn + 1];
                for (int i = 0; i < line.Length; i++)
                {
                    line[i] = cells.TryGetValue(i, out string value) ? value : string.Empty;
                }
                result.Add(line);
            }

            ApplyMergedCells(sheet, result);
            return TrimEmptyRows(result);
        }

        /// <summary>按 mergeCells 声明把合并区统一回填左上角值</summary>
        /// <param name="sheet">工作表 XML</param>
        /// <param name="rows">行数据（就地修改）</param>
        private static void ApplyMergedCells(XDocument sheet, List<string[]> rows)
        {
            List<string> refs = new List<string>();
            foreach (XElement element in sheet.Root.Descendants())
            {
                if (!string.Equals(element.Name.LocalName, "mergeCell", StringComparison.Ordinal)) continue;
                string reference = GetAttribute(element, "ref");
                if (!string.IsNullOrEmpty(reference)) refs.Add(reference);
            }

            for (int i = 0; i < refs.Count; i++)
            {
                string[] parts = refs[i].Split(':');
                if (parts.Length != 2) continue;

                int[] start = CellPosition(parts[0]);
                int[] end = CellPosition(parts[1]);
                if (start == null || end == null) continue;

                if (start[0] >= rows.Count) continue;
                string value = (start[1] < rows[start[0]].Length) ? rows[start[0]][start[1]] : string.Empty;
                if (string.IsNullOrEmpty(value)) continue;

                for (int r = start[0]; r <= end[0] && r < rows.Count; r++)
                {
                    if (end[1] >= rows[r].Length)
                    {
                        string[] expanded = new string[end[1] + 1];
                        for (int c = 0; c < rows[r].Length; c++) expanded[c] = rows[r][c];
                        rows[r] = expanded;
                    }
                    for (int c = start[1]; c <= end[1]; c++) rows[r][c] = value;
                }
            }
        }

        /// <summary>去掉整行为空的行，并裁掉行尾空列</summary>
        /// <param name="rows">原始行</param>
        /// <returns>精简后的行</returns>
        private static List<string[]> TrimEmptyRows(List<string[]> rows)
        {
            List<string[]> result = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = rows[i];
                int last = row.Length - 1;
                while (last >= 0 && string.IsNullOrEmpty(row[last].Trim())) last--;
                if (last < 0) continue;

                string[] trimmed = new string[last + 1];
                for (int c = 0; c <= last; c++) trimmed[c] = row[c];
                result.Add(trimmed);
            }
            return result;
        }

        /// <summary>取单元格文本</summary>
        /// <param name="cell">单元格元素</param>
        /// <param name="shared">共享字符串表</param>
        /// <returns>单元格文本</returns>
        private static string CellText(XElement cell, List<string> shared)
        {
            string type = GetAttribute(cell, "t");

            if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
            {
                foreach (XElement element in cell.Elements())
                {
                    if (string.Equals(element.Name.LocalName, "is", StringComparison.Ordinal)) return ConcatText(element);
                }
                return string.Empty;
            }

            string raw = string.Empty;
            foreach (XElement element in cell.Elements())
            {
                if (string.Equals(element.Name.LocalName, "v", StringComparison.Ordinal))
                {
                    raw = element.Value;
                    break;
                }
            }

            if (string.Equals(type, "s", StringComparison.Ordinal))
            {
                if (int.TryParse(raw, out int index) && index >= 0 && index < shared.Count) return shared[index];
                return string.Empty;
            }

            return raw;
        }

        /// <summary>拼合元素下所有 t 节点的文本</summary>
        /// <param name="element">容器元素</param>
        /// <returns>文本</returns>
        private static string ConcatText(XElement element)
        {
            StringBuilder builder = new StringBuilder();
            foreach (XElement node in element.Descendants())
            {
                if (string.Equals(node.Name.LocalName, "t", StringComparison.Ordinal)) builder.Append(node.Value);
            }
            return builder.ToString();
        }

        /// <summary>把 "A1" 解析为 [行号, 列号]，均从 0 开始</summary>
        /// <param name="reference">单元格引用</param>
        /// <returns>位置数组；非法引用返回 null</returns>
        private static int[] CellPosition(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;

            int column = ColumnIndex(reference);
            int split = 0;
            while (split < reference.Length && char.IsLetter(reference[split])) split++;
            if (column < 0 || split >= reference.Length) return null;

            if (!int.TryParse(reference.Substring(split), out int row) || row < 1) return null;
            return new int[] { row - 1, column };
        }

        /// <summary>把 "A1"/"AB12" 的字母段解析为 0 基列号</summary>
        /// <param name="reference">单元格引用</param>
        /// <returns>列号；非法返回 -1</returns>
        private static int ColumnIndex(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return -1;

            int value = 0;
            int letters = 0;
            for (int i = 0; i < reference.Length; i++)
            {
                char c = reference[i];
                if (!char.IsLetter(c)) break;
                value = value * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
                letters++;
            }
            return letters == 0 ? -1 : value - 1;
        }

        /// <summary>按本地名取属性值（兼容带命名空间的 r:id）</summary>
        /// <param name="element">元素</param>
        /// <param name="localName">属性本地名</param>
        /// <returns>属性值；缺失返回空串</returns>
        private static string GetAttribute(XElement element, string localName)
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal)) return attribute.Value;
            }
            return string.Empty;
        }

        /// <summary>从压缩包读取 XML（不存在时返回 null）</summary>
        /// <param name="archive">压缩包</param>
        /// <param name="path">包内路径</param>
        /// <returns>XML 文档或 null</returns>
        private static XDocument LoadXml(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path);
            if (entry == null) return null;

            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        #endregion
    }
}
