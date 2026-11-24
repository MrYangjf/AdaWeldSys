using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using Newtonsoft.Json;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>配置包清单文件</summary>
    public sealed class ConfigPackageManifest
    {
        /// <summary>包格式版本</summary>
        public string SchemaVersion { get; set; }

        /// <summary>包范围（固定 system-settings，防止后续误扩大）</summary>
        public string Scope { get; set; }

        /// <summary>机型标记</summary>
        public string MachineModel { get; set; }

        /// <summary>导出程序版本</summary>
        public string AppVersion { get; set; }

        /// <summary>导出时间（本机真实执行时间）</summary>
        public string ExportedAt { get; set; }

        /// <summary>文件清单</summary>
        public List<ConfigPackageFile> Files { get; set; }

        /// <summary>排除清单（每项含理由，便于导入前核对）</summary>
        public List<string> Excluded { get; set; }
    }

    /// <summary>配置包内单个文件</summary>
    public sealed class ConfigPackageFile
    {
        /// <summary>相对程序基目录的路径（反斜杠）</summary>
        public string Path { get; set; }

        /// <summary>SHA256（大写十六进制）</summary>
        public string Sha256 { get; set; }

        /// <summary>字节数</summary>
        public long Length { get; set; }
    }

    /// <summary>系统设置整站配置打包器</summary>
    public static class SystemConfigPackager
    {
        #region 私有变量

        private const string Tag = "配置打包";

        private const string SchemaVersion = "1.1";
        private const string ScopeValue = "full-settings";
        private const string MachineModel = "AdaWeldSystem-ScanCut";
        private const string ManifestEntryName = "manifest.json";
        private const string ConfigEntryPrefix = "config/";

        /// <summary>整站导出根目录（递归全部文件）</summary>
        private static readonly string[] IncludedRootDirectories = { "Config", "DataBase" };

        /// <summary>排除项（文件名，不含伴生后缀）：生产记录库 + 用户库</summary>
        private static readonly string[] ExcludedDatabaseNames = { "product.db", "auth.db" };

        /// <summary>SQLite 伴生文件后缀（与排除库同前缀一并排除）</summary>
        private static readonly string[] SqliteSidecarSuffixes = { "-wal", "-shm", "-journal" };

        /// <summary>导入 staging 目录（自身不得进包）</summary>
        private const string ImportStagingDirectory = @"Config\_import";

        /// <summary>包扩展名</summary>
        public const string Extension = ".awcfg";

        #endregion

        #region 公共函数

        /// <summary>导出系统设置整站配置包</summary>
        /// <param name="targetFile">目标 .awcfg 文件路径</param>
        /// <param name="message">成功/失败说明</param>
        /// <returns>导出成功返回 true</returns>
        public static bool Export(string targetFile, out string message)
        {
            message = string.Empty;
            try
            {
                List<ConfigPackageFile> files = CollectFiles();
                if (files.Count == 0)
                {
                    message = "没有可导出的系统配置文件（检查 Config 目录是否完整）";
                    GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                    return false;
                }

                ConfigPackageManifest manifest = new ConfigPackageManifest
                {
                    SchemaVersion = SchemaVersion,
                    Scope = ScopeValue,
                    MachineModel = MachineModel,
                    AppVersion = typeof(SystemConfigPackager).Assembly.GetName().Version.ToString(),
                    ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Files = files,
                    Excluded = BuildExcludedList()
                };

                string targetDirectory = Path.GetDirectoryName(Path.GetFullPath(targetFile));
                if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                if (File.Exists(targetFile)) File.Delete(targetFile);

                using (FileStream stream = new FileStream(targetFile, FileMode.CreateNew, FileAccess.Write))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    WriteTextEntry(archive, ManifestEntryName, JsonConvert.SerializeObject(manifest, Formatting.Indented));

                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    for (int i = 0; i < files.Count; i++)
                    {
                        string relative = files[i].Path;
                        string source = Path.Combine(baseDirectory, relative);
                        if (!File.Exists(source)) continue;

                        string entryName = ConfigEntryPrefix + relative.Replace('\\', '/');
                        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using (Stream entryStream = entry.Open())
                        using (FileStream sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            sourceStream.CopyTo(entryStream);
                        }
                    }
                }

                GlobalCommData.ShowLog(Tag, "导出完成 " + targetFile + "，共 " + files.Count + " 个文件");
                message = "导出完成：" + targetFile + "（" + files.Count + " 个文件）";
                return true;
            }
            catch (Exception ex)
            {
                message = "导出失败 " + ex.Message;
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>导入系统设置整站配置包</summary>
        /// <param name="packageFile">.awcfg 文件路径</param>
        /// <param name="message">成功/失败说明</param>
        /// <returns>导入成功返回 true</returns>
        public static bool Import(string packageFile, out string message)
        {
            message = string.Empty;
            if (!File.Exists(packageFile))
            {
                message = "文件不存在 " + packageFile;
                return false;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string stagingRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Config\_import", stamp);
            string backupFolder = ConfigBackupHelper.CreateBackupFolder();
            if (string.IsNullOrEmpty(backupFolder))
            {
                message = "创建备份目录失败，已中止导入";
                GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                return false;
            }

            try
            {
                if (!ReadPackage(packageFile, out ConfigPackageManifest manifest, out List<KeyValuePair<string, byte[]>> payload, out message)) return false;

                List<string> targets = new List<string>();
                for (int i = 0; i < payload.Count; i++)
                {
                    string relative = payload[i].Key;
                    string fullTarget = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative));
                    if (!IsUnderBaseDirectory(fullTarget))
                    {
                        message = "包内路径越界，已中止：" + relative;
                        GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                        return false;
                    }
                    if (!IsAllowedTarget(relative))
                    {
                        message = "包内文件不在整站导出范围（Config/DataBase 且非生产/用户库），已中止：" + relative;
                        GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                        return false;
                    }
                    targets.Add(fullTarget);
                }

                for (int i = 0; i < payload.Count; i++)
                {
                    string relative = payload[i].Key;
                    if (!ConfigBackupHelper.BackupFile(relative, backupFolder))
                    {
                        message = "备份失败，已中止导入：" + relative;
                        GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                        return false;
                    }
                }

                WriteStaging(stagingRoot, payload);

                List<string> written = new List<string>();
                try
                {
                    for (int i = 0; i < payload.Count; i++)
                    {
                        string target = targets[i];
                        string directory = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                        File.WriteAllBytes(target, payload[i].Value);
                        written.Add(payload[i].Key);
                    }
                }
                catch (Exception ex)
                {
                    Rollback(written, backupFolder);
                    message = "写入失败，已回滚：" + ex.Message;
                    GlobalCommData.ShowLog(Tag, message, MessageLevel.Error);
                    return false;
                }

                GlobalCommData.ShowLog(Tag, "导入完成，共 " + payload.Count + " 个文件；备份位于 " + backupFolder);
                message = "导入完成：写入 " + payload.Count + " 个文件，备份位于 " + backupFolder;
                return true;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
                }
                catch
                {
                    // staging 清理失败不影响导入结果
                }
            }
        }

        #endregion

        #region 私有函数

        /// <summary>递归收集 Config 与 DataBase 全部文件</summary>

        /// <returns>按路径排序的文件清单</returns>
        private static List<ConfigPackageFile> CollectFiles()
        {
            List<ConfigPackageFile> files = new List<ConfigPackageFile>();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < IncludedRootDirectories.Length; i++)
            {
                string root = Path.Combine(baseDirectory, IncludedRootDirectories[i]);
                if (!Directory.Exists(root)) continue;

                string[] all;
                try
                {
                    all = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, "枚举目录失败 " + IncludedRootDirectories[i] + " " + ex.Message, MessageLevel.Warning);
                    continue;
                }

                for (int j = 0; j < all.Length; j++)
                {
                    string relative = all[j].Substring(baseDirectory.Length).TrimStart('\\');
                    if (IsExcluded(relative) || IsStagingPath(relative)) continue;
                    AddFile(files, baseDirectory, relative);
                }
            }

            files.Sort(delegate(ConfigPackageFile a, ConfigPackageFile b)
            {
                return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
            return files;
        }

        /// <summary>排除清单（写入 manifest，说明为何不导出）</summary>
        /// <returns>排除项说明</returns>
        private static List<string> BuildExcludedList()
        {
            List<string> excluded = new List<string>();
            excluded.Add(@"DataBase\Product.db（生产记录数据库，按业主口径排除）");
            excluded.Add(@"DataBase\Auth.db（用户数据库，按业主口径排除）");
            excluded.Add(@"Config\_import\*（导入 staging 暂存，不进包）");
            return excluded;
        }

        /// <summary>是否属于排除项</summary>

        /// <param name="relative">相对路径</param>
        /// <returns>排除返回 true</returns>
        private static bool IsExcluded(string relative)
        {
            string fileName = Path.GetFileName(relative.Replace('/', '\\')).ToLowerInvariant();
            for (int i = 0; i < ExcludedDatabaseNames.Length; i++)
            {
                if (fileName == ExcludedDatabaseNames[i]) return true;
                for (int j = 0; j < SqliteSidecarSuffixes.Length; j++)
                {
                    if (fileName == ExcludedDatabaseNames[i] + SqliteSidecarSuffixes[j]) return true;
                }
            }
            return false;
        }

        /// <summary>是否为导入 staging 路径</summary>
        /// <param name="relative">相对路径</param>
        /// <returns>是返回 true</returns>
        private static bool IsStagingPath(string relative)
        {
            string normalized = relative.Replace('/', '\\');
            return normalized.StartsWith(ImportStagingDirectory + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>登记单个文件</summary>
        /// <param name="files">目标清单</param>
        /// <param name="baseDirectory">程序基目录</param>
        /// <param name="relative">相对路径</param>
        private static void AddFile(List<ConfigPackageFile> files, string baseDirectory, string relative)
        {
            string fullPath = Path.Combine(baseDirectory, relative);
            if (!File.Exists(fullPath))
            {
                GlobalCommData.ShowLog(Tag, "跳过缺失文件 " + relative, MessageLevel.Warning);
                return;
            }

            ConfigPackageFile file = new ConfigPackageFile
            {
                Path = relative,
                Length = new FileInfo(fullPath).Length,
                Sha256 = Sha256(fullPath)
            };
            files.Add(file);
        }

        /// <summary>读取并校验配置包</summary>
        /// <param name="packageFile">包路径</param>
        /// <param name="manifest">成功时回传清单</param>
        /// <param name="payload">成功时回传「相对路径 → 文件字节」</param>
        /// <param name="message">失败说明</param>
        /// <returns>校验通过返回 true</returns>
        private static bool ReadPackage(string packageFile, out ConfigPackageManifest manifest,
            out List<KeyValuePair<string, byte[]>> payload, out string message)
        {
            manifest = null;
            payload = new List<KeyValuePair<string, byte[]>>();
            message = string.Empty;

            using (FileStream stream = new FileStream(packageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntryName);
                if (manifestEntry == null)
                {
                    message = "包内缺少 " + ManifestEntryName;
                    return false;
                }

                string json;
                using (Stream entryStream = manifestEntry.Open())
                using (StreamReader reader = new StreamReader(entryStream, Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }

                manifest = JsonConvert.DeserializeObject<ConfigPackageManifest>(json);
                if (manifest == null || manifest.Files == null)
                {
                    message = "manifest.json 解析失败";
                    return false;
                }
                if (!string.Equals(manifest.Scope, ScopeValue, StringComparison.OrdinalIgnoreCase))
                {
                    message = "包范围不是 full-settings，拒绝导入：" + manifest.Scope;
                    return false;
                }
                if (!string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.OrdinalIgnoreCase))
                {
                    message = "包版本不兼容（期望 " + SchemaVersion + "，实际 " + manifest.SchemaVersion + "）";
                    return false;
                }

                for (int i = 0; i < manifest.Files.Count; i++)
                {
                    ConfigPackageFile file = manifest.Files[i];
                    if (file == null || string.IsNullOrEmpty(file.Path)) continue;
                    if (!IsSafeRelativePath(file.Path))
                    {
                        message = "包内路径非法，拒绝导入：" + file.Path;
                        return false;
                    }

                    string entryName = ConfigEntryPrefix + file.Path.Replace('\\', '/');
                    ZipArchiveEntry entry = archive.GetEntry(entryName);
                    if (entry == null)
                    {
                        message = "包内缺少文件：" + file.Path;
                        return false;
                    }

                    byte[] bytes = ReadEntryBytes(entry);
                    if (!string.Equals(Sha256(bytes), file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        message = "校验失败（SHA256 不一致）：" + file.Path;
                        return false;
                    }
                    payload.Add(new KeyValuePair<string, byte[]>(file.Path, bytes));
                }
            }
            return payload.Count > 0;
        }

        /// <summary>把校验通过的字节落到 staging 目录</summary>
        /// <param name="stagingRoot">staging 根</param>
        /// <param name="payload">相对路径 → 字节</param>
        private static void WriteStaging(string stagingRoot, List<KeyValuePair<string, byte[]>> payload)
        {
            Directory.CreateDirectory(stagingRoot);
            for (int i = 0; i < payload.Count; i++)
            {
                string target = Path.Combine(stagingRoot, payload[i].Key);
                string directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(target, payload[i].Value);
            }
        }

        /// <summary>按备份回滚已写入的文件</summary>
        /// <param name="written">已写入的相对路径</param>
        /// <param name="backupFolder">备份根</param>
        private static void Rollback(List<string> written, string backupFolder)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < written.Count; i++)
            {
                try
                {
                    string target = Path.Combine(baseDirectory, written[i]);
                    string backup = Path.Combine(backupFolder, written[i]);
                    if (File.Exists(backup)) File.Copy(backup, target, true);
                    else if (File.Exists(target)) File.Delete(target);
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, "回滚失败 " + written[i] + " " + ex.Message, MessageLevel.Error);
                }
            }
        }

        /// <summary>判断路径是否位于程序基目录内</summary>
        /// <param name="fullPath">绝对路径</param>
        /// <returns>位于基目录内返回 true</returns>
        private static bool IsUnderBaseDirectory(string fullPath)
        {
            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            if (!baseDirectory.EndsWith("\\", StringComparison.Ordinal)) baseDirectory += "\\";
            return fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>拒绝 ..、绝对路径与盘符</summary>
        /// <param name="relative">包内相对路径</param>
        /// <returns>安全返回 true</returns>
        private static bool IsSafeRelativePath(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return false;
            if (relative.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            if (relative.StartsWith("\\", StringComparison.Ordinal) || relative.StartsWith("/", StringComparison.Ordinal)) return false;
            if (relative.IndexOf(':') >= 0) return false;
            return true;
        }

        /// <summary>判断导入目标是否在整站范围内（且非排除项/非 staging）</summary>
        /// <param name="relative">相对路径</param>
        /// <returns>允许返回 true</returns>
        private static bool IsAllowedTarget(string relative)
        {
            if (!IsSafeRelativePath(relative)) return false;

            string normalized = relative.Replace('/', '\\');
            if (IsStagingPath(normalized) || IsExcluded(normalized)) return false;

            for (int i = 0; i < IncludedRootDirectories.Length; i++)
            {
                string prefix = IncludedRootDirectories[i] + "\\";
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>读取 zip 条目全部字节</summary>
        /// <param name="entry">条目</param>
        /// <returns>字节数组</returns>
        private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            using (MemoryStream memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        /// <summary>写文本条目</summary>
        /// <param name="archive">压缩包</param>
        /// <param name="name">条目名</param>
        /// <param name="content">文本内容</param>
        private static void WriteTextEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        /// <summary>计算文件 SHA256</summary>
        /// <param name="path">文件路径</param>
        /// <returns>大写十六进制</returns>
        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(stream));
            }
        }

        /// <summary>计算字节数组 SHA256</summary>
        /// <param name="bytes">字节</param>
        /// <returns>大写十六进制</returns>
        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        /// <summary>哈希转大写十六进制</summary>
        /// <param name="hash">哈希字节</param>
        /// <returns>十六进制文本</returns>
        private static string ToHex(byte[] hash)
        {
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        #endregion
    }
}
