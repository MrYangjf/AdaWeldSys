using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MotionControl;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>配置文档目录识别级别</summary>
    public enum ConfigDirectoryLevel
    {
        /// <summary>无任何可识别文档</summary>
        Invalid,
        /// <summary>部分文档缺失，只能只读预览</summary>
        Partial,
        /// <summary>轴/IO/气缸三表 + 焊接头 ESI + 焊接头 IO 表齐备，可生成 XML</summary>
        Ready
    }

    /// <summary>配置文档目录扫描结果</summary>
    public sealed class ConfigDirectoryScan
    {
        /// <summary>扫描目录</summary>
        public string DirectoryPath = string.Empty;

        /// <summary>轴配置表.csv 是否存在</summary>
        public bool HasAxisCsv;

        /// <summary>IO配置表.csv 是否存在</summary>
        public bool HasIoCsv;

        /// <summary>气缸配置表.csv 是否存在</summary>
        public bool HasCylinderCsv;

        /// <summary>焊接头从站 ESI（Wsx_ScanCut.xml）是否存在；存在即台达从站文档就绪</summary>
        public bool HasWeldHeadEsi;

        /// <summary>焊接头从站 IO 表（EtherCAT协议.xlsx）是否存在</summary>
        public bool HasWeldHeadProtocol;

        /// <summary>ESI 路径</summary>
        public string EsiPath = string.Empty;

        /// <summary>焊接头 IO 表路径（唯一权威源）</summary>
        public string ProtocolPath = string.Empty;

        /// <summary>ESI 厂商号（如 0xE0000002）</summary>
        public string VendorIdText = string.Empty;

        /// <summary>ESI 产品码（如 0x26483052）</summary>
        public string ProductCodeText = string.Empty;

        /// <summary>ESI 修订号（如 0x00000006）</summary>
        public string RevisionNoText = string.Empty;

        /// <summary>ESI 从站名称（Device/Name）</summary>
        public string SlaveName = string.Empty;

        /// <summary>既有配置中的从站节点号文本</summary>
        public string NodeIdText = string.Empty;

        /// <summary>既有配置中是否取到有效节点号（0 视为待填）</summary>
        public bool NodeIdKnown;

        /// <summary>既有配置中的从站启用状态文本（TRUE/FALSE；空表示待填）</summary>
        public string EnabledText = string.Empty;

        /// <summary>焊接头 PDO 映射（全部来自 IO 表，不硬编码）</summary>
        public List<WeldHeadPdoEntry> PdoEntries = new List<WeldHeadPdoEntry>();

        /// <summary>IO 表数据行数（不含表头）</summary>
        public int ProtocolRowCount;

        /// <summary>IO 表中仅 SDO、无 PDO 的行数</summary>
        public int SdoOnlyCount;

        /// <summary>是否有轴配置表.csv（区分缺文件与内容不符）</summary>
        public bool AxisCsvExists;

        /// <summary>IO配置表.csv 文件是否存在</summary>
        public bool IoCsvExists;

        /// <summary>气缸配置表.csv 文件是否存在</summary>
        public bool CylinderCsvExists;

        /// <summary>目录内是否有 XML；存在但非 ESI 即拦下写盘</summary>
        public bool WeldHeadEsiExists;

        /// <summary>目录内是否有 xlsx；存在但无 PDO 即拦下写盘</summary>
        public bool WeldHeadProtocolExists;

        /// <summary>IO 表文件修改时间（yyyy-MM-dd HH:mm:ss）</summary>
        public string ProtocolLastWriteTime = string.Empty;

        /// <summary>ESI 中的 PDO 点位总数（仅用于交叉校验）</summary>
        public int EsiPointCount;

        /// <summary>IO 表与 ESI 的差异说明（仅告警，一律以 IO 表为准）</summary>
        public List<string> EsiDifferences = new List<string>();

        /// <summary>识别级别</summary>
        public ConfigDirectoryLevel Level = ConfigDirectoryLevel.Invalid;

        /// <summary>识别结论（面向业主的中文说明）</summary>
        public string Message = string.Empty;
    }

    /// <summary>台达控制器配置文档 → 项目 XML 生成器</summary>
    public static class DeltaControllerConfigImporter
    {
        #region 私有变量

        private const string Tag = "台达配置生成";

        private const string MotionXmlRelativePath = @"Config\MotionControl\Motion_XML\DeltaMotionConfig.xml";
        private const string WeldHeadXmlRelativePath = @"Config\MotionControl\WeldHead_XML\WeldHeadSlave.xml";

        /// <summary>轴配置表文件名</summary>
        public const string AxisFileName = "轴配置表.csv";

        /// <summary>IO 配置表文件名</summary>
        public const string IOFileName = "IO配置表.csv";

        /// <summary>气缸配置表文件名</summary>
        public const string CylinderFileName = "气缸配置表.csv";

        #endregion

        #region 公共函数

        /// <summary>跳过节清单（缺失文件不导入，保留原配置）</summary>
        /// <param name="scan">识别结果</param>
        /// <returns>中文清单；全部齐备返回「无」</returns>
        private static string BuildSkipList(ConfigDirectoryScan scan)
        {
            List<string> skipped = new List<string>();
            if (!scan.HasAxisCsv) skipped.Add(AxisFileName);
            if (!scan.HasIoCsv) skipped.Add(IOFileName);
            if (!scan.HasCylinderCsv) skipped.Add(CylinderFileName);
            if (!scan.HasWeldHeadEsi) skipped.Add("焊接头 ESI");
            if (!scan.HasWeldHeadProtocol) skipped.Add("焊接头 IO 表");
            return skipped.Count == 0 ? "无" : string.Join("、", skipped.ToArray());
        }

        /// <summary>对象索引规范化</summary>

        /// <param name="text">原始十六进制文本</param>
        /// <returns>规范化文本；空值返回空串</returns>
        private static string NormalizeObjectKeyHex(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length == 0) return string.Empty;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value.Substring(2);
            return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed)
                ? parsed.ToString("X4", CultureInfo.InvariantCulture)
                : value.ToUpperInvariant();
        }

        /// <summary>拼装 Partial 级说明</summary>

        /// <param name="scan">扫描结果</param>
        /// <param name="missingFiles">缺失文件清单</param>
        /// <param name="badContent">内容不符清单</param>
        /// <param name="prefix">前缀说明（可为 null）</param>
        /// <returns>说明文本</returns>
        private static string BuildPartialMessage(ConfigDirectoryScan scan, List<string> missingFiles,
            List<string> badContent, string prefix)
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(prefix)) sb.Append(prefix).Append("；");
            sb.Append("已加载，可保存");
            if (missingFiles.Count > 0)
            {
                sb.Append("；缺失节将跳过并保留原配置：").Append(string.Join("、", missingFiles.ToArray()));
            }
            if (badContent.Count > 0)
            {
                sb.Append("；以下文档内容不符，保存将被拦截：").Append(string.Join("、", badContent.ToArray()));
            }
            return sb.ToString();
        }

        /// <summary>按扫描结果生成项目 XML（写前备份，失败零写入）</summary>
        /// <param name="scan">识别结果（表格源时焊接头字段已覆盖）</param>
        /// <param name="directoryPath">CSV 源目录</param>
        /// <param name="pdoEntries">焊接头 PDO 行（null = 无表格覆盖）</param>
        /// <param name="tableSource">是否来自界面表格（用于日志口径）</param>
        /// <param name="message">成功/失败说明</param>
        /// <returns>生成成功返回 true</returns>
        private static bool GenerateCore(ConfigDirectoryScan scan, string directoryPath,
            IList<WeldHeadPdoEntry> pdoEntries, bool tableSource, out string message)
        {
            GlobalCommData.ShowLog(Tag, "开始重载保存，目录 " + directoryPath + "，识别结论：" + scan.Message
                + "；跳过清单：" + BuildSkipList(scan));
            if (scan.Level == ConfigDirectoryLevel.Invalid)
            {
                message = scan.Message;
                GlobalCommData.ShowLog(Tag, "重载保存中止：" + message, MessageLevel.Warning);
                return false;
            }

            // 存在但解析失败 → 整次拦下，绝不为「部分成功」写盘
            List<string> failures = GetParseFailures(scan);
            if (failures.Count > 0)
            {
                message = "以下文档解析失败，已拦下写盘（未写入任何配置）：" + string.Join("；", failures.ToArray());
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                return false;
            }

            bool anyCsv = scan.HasAxisCsv || scan.HasIoCsv || scan.HasCylinderCsv;
            bool anyWeld = scan.HasWeldHeadEsi || scan.HasWeldHeadProtocol;
            if (!anyCsv && !anyWeld)
            {
                message = "目录内无可保存的配置文档，未写入任何配置";
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Warning);
                return false;
            }

            string backupFolder = ConfigBackupHelper.CreateBackupFolder();
            if (string.IsNullOrEmpty(backupFolder))
            {
                message = "创建备份目录失败，已中止（未写入任何配置）";
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                return false;
            }

            bool backedUp = ConfigBackupHelper.BackupFile(MotionXmlRelativePath, backupFolder)
                && ConfigBackupHelper.BackupFile(WeldHeadXmlRelativePath, backupFolder);
            if (!backedUp)
            {
                message = "配置备份失败，已中止（未写入任何配置）";
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                return false;
            }

            List<string> written = new List<string>();
            if (anyCsv)
            {
                string importMessage = string.Empty;
                if (!MotionConfigImporter.Import(directoryPath, scan.HasAxisCsv, scan.HasIoCsv, scan.HasCylinderCsv,
                    backupFolder, ref importMessage))
                {
                    message = "轴/IO/气缸导入失败：" + importMessage;
                    GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                    return false;
                }
                written.Add("轴/IO/气缸（" + importMessage + "）");
                // 摘要并入末尾「重载保存完成」结论，不重复
            }

            MontionManager manager = MontionManager.Instance;
            if (manager == null || manager.MoveData == null || manager.MoveData.MotionConfig == null)
            {
                message = "运控配置实例不可用，已中止（未写入焊接头配置）";
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                return false;
            }

            if (scan.HasWeldHeadEsi)
            {
                WeldHeadSlaveConfig slave = manager.MoveData.MotionConfig.WeldHeadSlave;
                uint vendorId = ParseHexUInt(scan.VendorIdText, slave.VendorId);
                uint productCode = ParseHexUInt(scan.ProductCodeText, slave.ProductCode);
                uint revisionNo = ParseHexUInt(scan.RevisionNoText, slave.RevisionNo);
                bool identityChanged = slave.VendorId != vendorId || slave.ProductCode != productCode || slave.RevisionNo != revisionNo;
                slave.VendorId = vendorId;
                slave.ProductCode = productCode;
                slave.RevisionNo = revisionNo;

                manager.MoveData.MotionConfig.SaveConfig(backupFolder);
                // 明细并入末尾「重载保存完成」，本节不再刷一条
                written.Add(string.Format(CultureInfo.InvariantCulture,
                    "焊接头从站身份 VENDOR_ID {0} PRODUCT_CODE {1} REVISION_NO {2}{3}；节点号沿用既有值 {4}，启用 {5}",
                    scan.VendorIdText, scan.ProductCodeText, scan.RevisionNoText,
                    identityChanged ? "（有变化）" : "（无变化）",
                    scan.NodeIdKnown ? scan.NodeIdText : "未配置（待填）",
                    scan.EnabledText.Length == 0 ? "待填" : scan.EnabledText));
            }

            if (scan.HasWeldHeadProtocol)
            {
                manager.WeldHeadConfig.PdoMap.Clear();
                for (int i = 0; i < scan.PdoEntries.Count; i++) manager.WeldHeadConfig.PdoMap.Add(scan.PdoEntries[i]);
                manager.WeldHeadConfig.IoMapSourceFile = Path.GetFileName(scan.ProtocolPath);
                manager.WeldHeadConfig.IoMapSourceTime = scan.ProtocolLastWriteTime;
                manager.WeldHeadConfig.IoMapSourceRows = scan.ProtocolRowCount;
                manager.SaveWeldHeadConfig();

                written.Add(string.Format(CultureInfo.InvariantCulture,
                    "焊接头 PDO 映射 {0} 条（数据行 {1}，仅 SDO {2}；来源 {3}）",
                    scan.PdoEntries.Count, scan.ProtocolRowCount, scan.SdoOnlyCount,
                    tableSource ? "界面表格（唯一提交源）" : Path.GetFileName(scan.ProtocolPath)));
            }

            List<string> skipped = new List<string>();
            if (!scan.HasAxisCsv) skipped.Add(AxisFileName);
            if (!scan.HasIoCsv) skipped.Add(IOFileName);
            if (!scan.HasCylinderCsv) skipped.Add(CylinderFileName);
            if (!scan.HasWeldHeadEsi) skipped.Add("焊接头 ESI");
            if (!scan.HasWeldHeadProtocol) skipped.Add("焊接头 IO 表");

            message = "重载保存完成：仅重载台达配置（" + string.Join("、", written.ToArray()) + "）"
                + (skipped.Count > 0 ? "；跳过（保留原配置）：" + string.Join("、", skipped.ToArray()) : string.Empty)
                + "；备份位于 " + backupFolder;
            GlobalCommData.ShowLog(Tag, message);
            return true;
        }

        /// <summary>识别目录并按内容特征扫描配置文档</summary>
        /// <param name="directoryPath">待识别目录</param>
        /// <returns>扫描结果（含级别与说明，不抛异常）</returns>
        public static ConfigDirectoryScan Scan(string directoryPath)
        {
            ConfigDirectoryScan scan = new ConfigDirectoryScan
            {
                DirectoryPath = directoryPath ?? string.Empty
            };

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                scan.Message = "目录不存在：" + directoryPath;
                return scan;
            }

            scan.HasAxisCsv = HasCsvSignature(Path.Combine(directoryPath, AxisFileName), "AXIS_NAME", out bool axisExists);
            scan.HasIoCsv = HasCsvSignature(Path.Combine(directoryPath, IOFileName), "IO_TYPE", out bool ioExists);
            scan.HasCylinderCsv = HasCsvSignature(Path.Combine(directoryPath, CylinderFileName), "CYLINDER_NAME", out bool cylinderExists);
            scan.AxisCsvExists = axisExists;
            scan.IoCsvExists = ioExists;
            scan.CylinderCsvExists = cylinderExists;

            ScanWeldHeadFiles(scan);
            ReadSlaveIdentity(scan);

            List<string> missingFiles = new List<string>();
            List<string> badContent = new List<string>();
            if (!scan.HasAxisCsv) (axisExists ? badContent : missingFiles).Add(axisExists ? AxisFileName + "（表头缺少 AXIS_NAME）" : AxisFileName);
            if (!scan.HasIoCsv) (ioExists ? badContent : missingFiles).Add(ioExists ? IOFileName + "（表头缺少 IO_TYPE）" : IOFileName);
            if (!scan.HasCylinderCsv) (cylinderExists ? badContent : missingFiles).Add(cylinderExists ? CylinderFileName + "（表头缺少 CYLINDER_NAME）" : CylinderFileName);

            if (missingFiles.Count == 3 && badContent.Count == 0 && !scan.HasWeldHeadEsi && !scan.HasWeldHeadProtocol
                && !scan.WeldHeadEsiExists && !scan.WeldHeadProtocolExists)
            {
                scan.Level = ConfigDirectoryLevel.Invalid;
                scan.Message = "该目录不是配置文档目录：未找到轴/IO/气缸三表，也未找到焊接头 ESI 或 IO 表";
                return scan;
            }

            if (missingFiles.Count > 0 || badContent.Count > 0)
            {
                scan.Level = ConfigDirectoryLevel.Partial;
                scan.Message = BuildPartialMessage(scan, missingFiles, badContent, null);
                return scan;
            }

            if (!scan.HasWeldHeadEsi || !scan.HasWeldHeadProtocol)
            {
                scan.Level = ConfigDirectoryLevel.Partial;
                List<string> weldMissing = new List<string>();
                List<string> weldBad = new List<string>();
                if (!scan.HasWeldHeadEsi)
                {
                    (scan.WeldHeadEsiExists ? weldBad : weldMissing).Add(scan.WeldHeadEsiExists
                        ? "台达从站文档/焊接头 ESI（Wsx_ScanCut.xml 存在但未识别）"
                        : "台达从站文档/焊接头 ESI（Wsx_ScanCut.xml）");
                }
                if (!scan.HasWeldHeadProtocol)
                {
                    (scan.WeldHeadProtocolExists ? weldBad : weldMissing).Add(scan.WeldHeadProtocolExists
                        ? "焊接头从站 IO 表（EtherCAT协议.xlsx 存在但未解析出 PDO）"
                        : "焊接头从站 IO 表（EtherCAT协议.xlsx）");
                }
                scan.Message = BuildPartialMessage(scan, weldMissing, weldBad, "轴/IO/气缸三表已就绪");
                return scan;
            }

            scan.Level = ConfigDirectoryLevel.Ready;
            scan.Message = string.Format(CultureInfo.InvariantCulture,
                "识别成功：轴/IO/气缸三表 + 台达从站（ESI：{0}）+ 焊接头 IO 表 {1} 行（PDO {2} 条 / 仅 SDO {3} 条）；IO 表为唯一权威源，ESI 交叉校验差异 {4} 处（仅告警不覆盖）",
                string.IsNullOrEmpty(scan.SlaveName) ? "未命名" : scan.SlaveName,
                scan.ProtocolRowCount, scan.PdoEntries.Count - scan.SdoOnlyCount, scan.SdoOnlyCount,
                scan.EsiDifferences.Count);
            return scan;
        }

        /// <summary>焊接头 PDO 表对象键</summary>
        /// <param name="entry">PDO 条目</param>
        /// <returns>规范化对象键，如 <c>PDO 1601/01</c> 或 <c>SDO 7010/01</c></returns>
        public static string BuildPdoObjectKey(WeldHeadPdoEntry entry)
        {
            if (entry == null) return string.Empty;

            string pdoIndex = NormalizeObjectKeyHex(entry.PdoIndex);
            string pdoSubIndex = NormalizeObjectKeyHex(entry.PdoSubIndex);
            if (pdoIndex.Length > 0 || pdoSubIndex.Length > 0) return "PDO " + pdoIndex + "/" + pdoSubIndex;

            string sdoIndex = NormalizeObjectKeyHex(entry.SdoIndex);
            string sdoSubIndex = NormalizeObjectKeyHex(entry.SdoSubIndex);
            if (sdoIndex.Length > 0 || sdoSubIndex.Length > 0) return "SDO " + sdoIndex + "/" + sdoSubIndex;

            return string.Empty;
        }

        /// <summary>存在但解析失败的文档清单</summary>

        /// <param name="scan">扫描结果</param>
        /// <returns>失败项描述列表；无失败返回空列表</returns>
        public static List<string> GetParseFailures(ConfigDirectoryScan scan)
        {
            List<string> failures = new List<string>();
            if (scan == null) return failures;

            if (scan.AxisCsvExists && !scan.HasAxisCsv) failures.Add(AxisFileName + "（表头缺少 AXIS_NAME）");
            if (scan.IoCsvExists && !scan.HasIoCsv) failures.Add(IOFileName + "（表头缺少 IO_TYPE）");
            if (scan.CylinderCsvExists && !scan.HasCylinderCsv) failures.Add(CylinderFileName + "（表头缺少 CYLINDER_NAME）");
            if (scan.WeldHeadEsiExists && !scan.HasWeldHeadEsi) failures.Add("目录内 XML 未识别为焊接头 ESI（需含 EtherCATInfo/Vendor/Id，且 Type 节点带 ProductCode 属性或子元素）");
            if (scan.WeldHeadProtocolExists && !scan.HasWeldHeadProtocol) failures.Add("EtherCAT协议.xlsx 存在但未解析出 PDO 映射（表头需含 变量/数据类型/SDO Index/PDO Index）");
            return failures;
        }

        /// <summary>按扫描结果生成项目 XML（写前备份，失败零写入）</summary>

        /// <param name="directoryPath">配置文档目录</param>
        /// <param name="message">成功/失败说明</param>
        /// <returns>生成成功返回 true</returns>
        public static bool Generate(string directoryPath, out string message)
        {
            return GenerateCore(Scan(directoryPath), directoryPath, null, false, out message);
        }

        /// <summary>按界面表格生成项目 XML</summary>
        /// <param name="directoryPath">轴/IO/气缸 CSV 源目录（界面按当前表格导出的临时目录）</param>
        /// <param name="pdoEntries">焊接头 PDO 行（表格唯一提交源；null 表示沿用目录 xlsx 解析结果）</param>
        /// <param name="message">成功/失败说明</param>
        /// <returns>生成成功返回 true</returns>
        public static bool Generate(string directoryPath, IList<WeldHeadPdoEntry> pdoEntries, out string message)
        {
            ConfigDirectoryScan scan = Scan(directoryPath);
            if (pdoEntries != null)
            {
                List<WeldHeadPdoEntry> entries = new List<WeldHeadPdoEntry>();
                int sdoOnly = 0;
                for (int i = 0; i < pdoEntries.Count; i++)
                {
                    WeldHeadPdoEntry entry = pdoEntries[i];
                    if (entry == null) continue;
                    entries.Add(entry);
                    if (string.IsNullOrEmpty(entry.PdoIndex)) sdoOnly++;
                }
                scan.HasWeldHeadProtocol = entries.Count > 0;
                scan.WeldHeadProtocolExists = false;
                scan.PdoEntries = entries;
                scan.ProtocolRowCount = entries.Count;
                scan.SdoOnlyCount = sdoOnly;
                scan.ProtocolPath = Path.Combine(directoryPath, "EtherCAT协议.xlsx");
                scan.ProtocolLastWriteTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                if (entries.Count > 0 && scan.Level == ConfigDirectoryLevel.Invalid)
                {
                    scan.Level = ConfigDirectoryLevel.Partial;
                    scan.Message = "表格提交源：轴/IO/气缸 + 焊接头 PDO 取自界面表格";
                }
            }
            return GenerateCore(scan, directoryPath, pdoEntries, true, out message);
        }

        /// <summary>读取 CSV 表头与数据行（用于界面只读预览）</summary>
        /// <param name="filePath">CSV 路径</param>
        /// <param name="headers">成功时回传表头</param>
        /// <param name="rows">成功时回传数据行</param>
        /// <param name="message">失败说明</param>
        /// <returns>读取成功返回 true</returns>
        public static bool TryReadCsvTable(string filePath, out string[] headers, out List<string[]> rows, out string message)
        {
            headers = new string[0];
            rows = new List<string[]>();
            message = string.Empty;

            if (!File.Exists(filePath))
            {
                message = "文件不存在 " + filePath;
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrEmpty(lines[i]) || lines[i].Trim().Length == 0) continue;
                    string[] fields = SplitCsvLine(lines[i]);
                    if (headers.Length == 0)
                    {
                        headers = fields;
                        continue;
                    }
                    rows.Add(fields);
                }
                return headers.Length > 0;
            }
            catch (Exception ex)
            {
                message = "CSV 读取失败 " + Path.GetFileName(filePath) + " " + ex.Message;
                return false;
            }
        }

        #endregion

        #region 私有函数

        /// <summary>按表头签名校验 CSV 内容</summary>

        /// <param name="filePath">CSV 路径</param>
        /// <param name="signature">必须出现的表头列名</param>
        /// <param name="exists">回传文件是否存在（用于区分缺文件与内容不符）</param>
        /// <returns>表头命中签名返回 true</returns>
        private static bool HasCsvSignature(string filePath, string signature, out bool exists)
        {
            exists = File.Exists(filePath);
            if (!exists) return false;

            try
            {
                using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return false;

                    string[] columns = SplitCsvLine(header.TrimStart('\uFEFF'));
                    for (int i = 0; i < columns.Length; i++)
                    {
                        if (string.Equals(columns[i].Trim(), signature, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "CSV 表头校验失败 " + Path.GetFileName(filePath) + " " + ex.Message, MessageLevel.Warning);
            }
            return false;
        }

        /// <summary>扫描焊接头 ESI 与 IO 表</summary>
        /// <param name="scan">扫描结果（就地写入）</param>
        private static void ScanWeldHeadFiles(ConfigDirectoryScan scan)
        {
            string[] xmlFiles = Directory.GetFiles(scan.DirectoryPath, "*.xml");
            scan.WeldHeadEsiExists = xmlFiles.Length > 0;
            for (int i = 0; i < xmlFiles.Length; i++)
            {
                if (TryParseEsi(xmlFiles[i], scan))
                {
                    scan.HasWeldHeadEsi = true;
                    scan.EsiPath = xmlFiles[i];
                    break;
                }
            }

            string[] xlsxFiles = Directory.GetFiles(scan.DirectoryPath, "*.xlsx");
            scan.WeldHeadProtocolExists = xlsxFiles.Length > 0;
            for (int i = 0; i < xlsxFiles.Length; i++)
            {
                List<WeldHeadPdoEntry> entries = TryParseProtocol(xlsxFiles[i]);
                if (entries == null || entries.Count == 0) continue;
                scan.HasWeldHeadProtocol = true;
                scan.ProtocolPath = xlsxFiles[i];
                scan.PdoEntries = entries;
                scan.ProtocolRowCount = entries.Count;
                for (int j = 0; j < entries.Count; j++)
                {
                    if (string.IsNullOrEmpty(entries[j].PdoIndex)) scan.SdoOnlyCount++;
                }
                scan.ProtocolLastWriteTime = File.GetLastWriteTime(xlsxFiles[i]).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                if (scan.HasWeldHeadEsi) CrossCheckProtocol(scan);
                break;
            }
        }

        /// <summary>解析 ESI 中的从站身份（VendorId/ProductCode/RevisionNo）</summary>
        /// <param name="path">XML 路径</param>
        /// <param name="scan">扫描结果（就地写入身份字段）</param>
        /// <returns>是焊接头从站 ESI 返回 true</returns>
        private static bool TryParseEsi(string path, ConfigDirectoryScan scan)
        {
            try
            {
                XDocument doc = XDocument.Load(path);
                if (doc.Root == null) return false;

                string vendor = ReadVendorId(doc);
                if (string.IsNullOrEmpty(vendor)) return false;

                XElement device = null;
                XElement type = null;
                string product = string.Empty;
                foreach (XElement element in doc.Root.Descendants())
                {
                    if (!string.Equals(element.Name.LocalName, "Type", StringComparison.Ordinal)) continue;

                    string candidate = ReadAttribute(element, "ProductCode");
                    if (string.IsNullOrEmpty(candidate)) candidate = ReadChildText(element, "ProductCode");
                    if (string.IsNullOrEmpty(candidate)) continue;

                    type = element;
                    product = candidate;
                    XElement parent = element.Parent;
                    if (parent != null && string.Equals(parent.Name.LocalName, "Device", StringComparison.Ordinal)) device = parent;
                    break;
                }
                if (type == null) return false;

                string revision = ReadAttribute(type, "RevisionNo");
                if (string.IsNullOrEmpty(revision)) revision = ReadChildText(type, "RevisionNo");

                scan.VendorIdText = NormalizeHex(vendor);
                scan.ProductCodeText = NormalizeHex(product);
                scan.RevisionNoText = NormalizeHex(revision);
                scan.SlaveName = ReadChildText(device, "Name");
                scan.EsiPointCount = ReadEsiPdoContainers(doc, null);
                return true;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "ESI 解析跳过 " + Path.GetFileName(path) + " " + ex.Message, MessageLevel.Warning);
            }
            return false;
        }

        /// <summary>读取 ESI 厂商号（EtherCATInfo/Vendor/Id）</summary>
        /// <param name="doc">ESI 文档</param>
        /// <returns>厂商号文本；缺失返回空串</returns>
        private static string ReadVendorId(XDocument doc)
        {
            foreach (XElement element in doc.Root.Descendants())
            {
                if (!string.Equals(element.Name.LocalName, "Vendor", StringComparison.Ordinal)) continue;
                foreach (XElement child in element.Elements())
                {
                    if (!string.Equals(child.Name.LocalName, "Id", StringComparison.Ordinal)) continue;
                    string value = child.Value == null ? string.Empty : child.Value.Trim();
                    if (value.Length > 0) return value;
                }
            }
            return string.Empty;
        }

        /// <summary>读取既有配置中的从站节点号与启用状态</summary>
        /// <param name="scan">扫描结果（就地写入节点号/启用状态）</param>
        private static void ReadSlaveIdentity(ConfigDirectoryScan scan)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MotionXmlRelativePath);
            if (!File.Exists(path)) return;

            try
            {
                XDocument doc = XDocument.Load(path);
                if (doc.Root == null) return;

                XElement slave = null;
                foreach (XElement element in doc.Root.Descendants())
                {
                    if (string.Equals(element.Name.LocalName, "WELD_HEAD_SLAVE", StringComparison.Ordinal))
                    {
                        slave = element;
                        break;
                    }
                }
                if (slave == null) return;

                string nodeId = ReadChildText(slave, "NODE_ID");
                uint parsedNodeId = ParseIndexNumber(nodeId);
                scan.NodeIdKnown = nodeId.Length > 0 && parsedNodeId != 0;
                scan.NodeIdText = scan.NodeIdKnown ? parsedNodeId.ToString(CultureInfo.InvariantCulture) : string.Empty;
                scan.EnabledText = ReadChildText(slave, "ENABLED");
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "既有从站配置读取失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>IO 表与 ESI 交叉校验：差异只记告警，一律不覆盖 IO 表值</summary>
        /// <param name="scan">扫描结果（就地写入差异清单）</param>
        private static void CrossCheckProtocol(ConfigDirectoryScan scan)
        {
            try
            {
                Dictionary<string, List<EsiPdoPoint>> containers = new Dictionary<string, List<EsiPdoPoint>>();
                int esiPointCount = ReadEsiPdoContainers(XDocument.Load(scan.EsiPath), containers);
                scan.EsiPointCount = esiPointCount;

                int covered = 0;
                int positionMismatch = 0;
                for (int i = 0; i < scan.PdoEntries.Count; i++)
                {
                    WeldHeadPdoEntry entry = scan.PdoEntries[i];
                    if (string.IsNullOrEmpty(entry.PdoIndex)) continue;

                    string containerKey = NormalizeContainerKey(entry.PdoIndex);
                    if (!containers.TryGetValue(containerKey, out List<EsiPdoPoint> points))
                    {
                        scan.EsiDifferences.Add(string.Format(CultureInfo.InvariantCulture,
                            "表项 {0} 的 PDO {1} 在 ESI 中不存在", entry.VarName, entry.PdoIndex));
                        continue;
                    }

                    string tableObjectKey = BuildPdoKey(entry.SdoIndex, entry.SdoSubIndex);
                    int actualIndex = points.FindIndex(delegate(EsiPdoPoint item)
                    {
                        return string.Equals(item.ObjectKey, tableObjectKey, StringComparison.OrdinalIgnoreCase);
                    });
                    if (actualIndex < 0)
                    {
                        scan.EsiDifferences.Add(string.Format(CultureInfo.InvariantCulture,
                            "表项 {0}（SDO {1}/{2}）不在 ESI 的 PDO {3} 中",
                            entry.VarName, entry.SdoIndex, entry.SdoSubIndex, entry.PdoIndex));
                        continue;
                    }

                    covered++;
                    EsiPdoPoint point = points[actualIndex];
                    int tablePosition = (int)ParseIndexNumber(entry.PdoSubIndex);
                    if (tablePosition != actualIndex + 1)
                    {
                        positionMismatch++;
                        if (positionMismatch <= 5)
                        {
                            scan.EsiDifferences.Add(string.Format(CultureInfo.InvariantCulture,
                                "表项 {0} 表内位置 {1}，ESI 实际位置 {2}（以表为准）",
                                entry.VarName, tablePosition, actualIndex + 1));
                        }
                    }
                    if (!string.Equals(point.VarName, entry.VarName, StringComparison.OrdinalIgnoreCase))
                    {
                        scan.EsiDifferences.Add(string.Format(CultureInfo.InvariantCulture,
                            "表项 {0} 与 ESI 变量 {1} 名称不一致（以表为准）", entry.VarName, point.VarName));
                    }
                }

                if (positionMismatch > 0)
                {
                    scan.EsiDifferences.Add(string.Format(CultureInfo.InvariantCulture,
                        "PDO 位置与 ESI 顺序不一致共 {0} 处（仅列前 5 条，以表为准）", positionMismatch));
                }
                scan.EsiDifferences.Add(string.Format(CultureInfo.InvariantCulture,
                    "ESI 共 {0} 个 PDO 点位，IO 表按对象地址覆盖 {1} 个，其余 {2} 个未映射（以表为准）",
                    esiPointCount, covered, esiPointCount - covered));

                for (int i = 0; i < scan.EsiDifferences.Count; i++)
                {
                    GlobalCommData.ShowLog(Tag, "交叉校验差异 " + scan.EsiDifferences[i], MessageLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "ESI 交叉校验跳过 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>ESI 的单个 PDO 点位（仅用于交叉校验，不作为写入源）</summary>
        private sealed class EsiPdoPoint
        {
            /// <summary>对象地址键（对象索引 + 子索引）</summary>
            public string ObjectKey = string.Empty;

            /// <summary>ESI 变量名</summary>
            public string VarName = string.Empty;
        }

        /// <summary>读取 ESI 的 PDO 容器与点位</summary>

        /// <param name="doc">ESI 文档</param>
        /// <param name="containers">回传容器集合（键为 PDO 索引）；为 null 时只统计数量</param>
        /// <returns>有效点位数量（不含 padding）</returns>
        private static int ReadEsiPdoContainers(XDocument doc, Dictionary<string, List<EsiPdoPoint>> containers)
        {
            if (doc == null || doc.Root == null) return 0;

            int count = 0;
            foreach (XElement pdo in doc.Root.Descendants())
            {
                string localName = pdo.Name.LocalName;
                if (!string.Equals(localName, "RxPdo", StringComparison.Ordinal)
                    && !string.Equals(localName, "TxPdo", StringComparison.Ordinal)) continue;

                string containerKey = NormalizeContainerKey(ReadChildText(pdo, "Index"));
                List<EsiPdoPoint> points = new List<EsiPdoPoint>();
                foreach (XElement entry in pdo.Elements())
                {
                    if (!string.Equals(entry.Name.LocalName, "Entry", StringComparison.Ordinal)) continue;

                    string index = ReadChildText(entry, "Index");
                    string subIndex = ReadChildText(entry, "SubIndex");
                    if (index.Length == 0 || subIndex.Length == 0) continue;
                    if (ParseIndexNumber(index) == 0) continue; // padding 占位条目不参与点位统计

                    EsiPdoPoint point = new EsiPdoPoint
                    {
                        ObjectKey = BuildPdoKey(index, subIndex),
                        VarName = ReadChildText(entry, "Name")
                    };
                    points.Add(point);
                    count++;
                }

                if (containers != null && containerKey.Length > 0) containers[containerKey] = points;
            }
            return count;
        }

        /// <summary>PDO 容器索引归一化（如 0x1A00）</summary>
        /// <param name="index">索引文本</param>
        /// <returns>归一化文本；无法解析返回空串</returns>
        private static string NormalizeContainerKey(string index)
        {
            uint parsed = ParseIndexNumber(index);
            if (parsed == 0) return string.Empty;
            return "0x" + parsed.ToString("X4", CultureInfo.InvariantCulture);
        }

        /// <summary>构造 PDO 点位键</summary>

        /// <param name="index">PDO 索引文本（#x / 0x / 十进制）</param>
        /// <param name="subIndex">PDO 子索引文本</param>
        /// <returns>键文本</returns>
        private static string BuildPdoKey(string index, string subIndex)
        {
            uint parsedIndex = ParseIndexNumber(index);
            uint parsedSub = ParseIndexNumber(subIndex);
            return "0x" + parsedIndex.ToString("X4", CultureInfo.InvariantCulture) + "/" + parsedSub.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>解析索引文本（支持 #x / 0x 十六进制与十进制）</summary>
        /// <param name="text">原始文本</param>
        /// <returns>数值；无法解析返回 0</returns>
        private static uint ParseIndexNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string value = text.Trim();
            if (value.StartsWith("#x", StringComparison.OrdinalIgnoreCase) || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex)) return hex;
                return 0;
            }
            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint dec)) return dec;
            return 0;
        }

        /// <summary>读取指定元素的子元素文本</summary>
        /// <param name="parent">父元素</param>
        /// <param name="localName">子元素本地名</param>
        /// <returns>文本；缺失返回空串</returns>
        private static string ReadChildText(XElement parent, string localName)
        {
            if (parent == null) return string.Empty;
            foreach (XElement child in parent.Elements())
            {
                if (string.Equals(child.Name.LocalName, localName, StringComparison.Ordinal)) return child.Value.Trim();
            }
            return string.Empty;
        }

        /// <summary>解析焊接头 IO 表（xlsx）的 PDO 映射</summary>
        /// <param name="path">xlsx 路径</param>
        /// <returns>PDO 条目；非焊接头 IO 表返回 null</returns>
        private static List<WeldHeadPdoEntry> TryParseProtocol(string path)
        {
            if (!XlsxSheetReader.TryReadFirstSheet(path, out List<string[]> rows, out string error))
            {
                GlobalCommData.ShowLog(Tag, "xlsx 读取跳过 " + Path.GetFileName(path) + " " + error, MessageLevel.Warning);
                return null;
            }

            int headerRow = -1;
            Dictionary<string, int> map = null;
            for (int i = 0; i < rows.Count && i < 6; i++)
            {
                Dictionary<string, int> candidate = BuildHeaderMap(rows[i]);
                if (candidate.ContainsKey("变量") && candidate.ContainsKey("SDOINDEX") && candidate.ContainsKey("PDOINDEX"))
                {
                    headerRow = i;
                    map = candidate;
                    break;
                }
            }

            if (headerRow < 0 || map == null) return null;

            List<WeldHeadPdoEntry> entries = new List<WeldHeadPdoEntry>();
            for (int i = headerRow + 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string variable = GetCell(row, map, "变量");
                if (string.IsNullOrEmpty(variable)) continue;

                WeldHeadPdoEntry entry = new WeldHeadPdoEntry
                {
                    Seq = GetCell(row, map, "序号"),
                    VarName = variable,
                    DisplayName = GetCell(row, map, "名称"),
                    DataType = GetCell(row, map, "数据类型"),
                    SdoIndex = GetCell(row, map, "SDOINDEX"),
                    SdoSubIndex = GetCell(row, map, "SDOSUBINDEX"),
                    Access = GetCell(row, map, "ACCESS"),
                    PdoIndex = GetCell(row, map, "PDOINDEX"),
                    PdoSubIndex = GetCell(row, map, "PDOSUBINDEX"),
                    Description = GetCell(row, map, "说明")
                };
                entries.Add(entry);
            }
            return entries;
        }

        /// <summary>构造表头本地名到列下标的映射</summary>
        /// <param name="header">表头行</param>
        /// <returns>映射</returns>
        private static Dictionary<string, int> BuildHeaderMap(string[] header)
        {
            Dictionary<string, int> map = new Dictionary<string, int>();
            if (header == null) return map;
            for (int i = 0; i < header.Length; i++)
            {
                string key = NormalizeHeader(header[i]);
                if (string.IsNullOrEmpty(key) || map.ContainsKey(key)) continue;
                map[key] = i;
            }
            return map;
        }

        /// <summary>取指定列的值</summary>
        /// <param name="row">数据行</param>
        /// <param name="map">表头映射</param>
        /// <param name="key">规范化列名</param>
        /// <returns>值；缺列返回空串</returns>
        private static string GetCell(string[] row, Dictionary<string, int> map, string key)
        {
            if (row == null || map == null || !map.TryGetValue(key, out int index)) return string.Empty;
            if (index < 0 || index >= row.Length) return string.Empty;
            return row[index].Trim();
        }

        /// <summary>规范化表头（去空白、转大写）</summary>
        /// <param name="text">表头文本</param>
        /// <returns>规范化结果</returns>
        private static string NormalizeHeader(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace(" ", string.Empty).Replace("　", string.Empty).Trim().ToUpperInvariant();
        }

        /// <summary>规范化十六进制文本为 0xXXXXXXXX</summary>
        /// <param name="text">原始文本（#x / 0x / 纯数字）</param>
        /// <returns>规范化文本；空输入返回空串</returns>
        private static string NormalizeHex(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string value = text.Trim();
            if (value.StartsWith("#x", StringComparison.OrdinalIgnoreCase) || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }
            uint parsed = ParseHexUInt(value, 0);
            return "0x" + parsed.ToString("X8", CultureInfo.InvariantCulture);
        }

        /// <summary>解析十六进制文本为无符号整数</summary>
        /// <param name="text">文本</param>
        /// <param name="defaultValue">失败时的默认值</param>
        /// <returns>解析结果</returns>
        private static uint ParseHexUInt(string text, uint defaultValue)
        {
            if (string.IsNullOrEmpty(text)) return defaultValue;
            string value = text.Trim();
            if (value.StartsWith("#x", StringComparison.OrdinalIgnoreCase) || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }
            if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint result)) return result;
            return defaultValue;
        }

        /// <summary>按本地名读取元素属性</summary>
        /// <param name="element">元素</param>
        /// <param name="localName">属性本地名</param>
        /// <returns>属性值；缺失返回空串</returns>
        private static string ReadAttribute(XElement element, string localName)
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal)) return attribute.Value;
            }
            return string.Empty;
        }

        /// <summary>按 CSV 规则拆行（支持双引号包裹）</summary>
        /// <param name="line">原始行</param>
        /// <returns>字段数组</returns>
        private static string[] SplitCsvLine(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString().Trim());
            return fields.ToArray();
        }

        #endregion
    }
}
