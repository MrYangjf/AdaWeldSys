using System;
using System.IO;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.FileOperate
{
    /// <summary>配置写前备份工具</summary>
    /// <remarks>覆盖配置文件的动作，写前统一调用本类，
    /// 备份到 <c>Config\_backup\&lt;yyyyMMdd_HHmmss&gt;\</c>，备份失败即中止写入，避免半截写坏。</remarks>
    public static class ConfigBackupHelper
    {
        private const string Tag = "配置备份";

        /// <summary>备份根目录（相对程序基目录）</summary>
        public const string BackupRootRelative = @"Config\_backup";

        /// <summary>递归复制目录</summary>
        /// <param name="source">源目录</param>
        /// <param name="target">目标目录</param>
        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(files[i], Path.Combine(target, Path.GetFileName(files[i])), true);
            }

            string[] dirs = Directory.GetDirectories(source);
            for (int i = 0; i < dirs.Length; i++)
            {
                CopyDirectory(dirs[i], Path.Combine(target, Path.GetFileName(dirs[i]))); 
            }
        }
        /// <summary>按真实执行时间创建本次备份目录</summary>
        /// <returns>备份目录绝对路径；创建失败返回空串</returns>
        public static string CreateBackupFolder()
        {
            try
            {
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BackupRootRelative);
                string folder = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "创建备份目录失败 " + ex.Message, MessageLevel.Error);
                return string.Empty;
            }
        }

        /// <summary>备份单个配置文件（保持相对目录结构）</summary>
        /// <param name="relativePath">相对程序基目录的文件路径</param>
        /// <param name="backupFolder">备份根目录（由 <see cref="CreateBackupFolder"/> 创建）</param>
        /// <returns>备份成功返回 true；源文件不存在视为成功（无可备份内容）</returns>
        public static bool BackupFile(string relativePath, string backupFolder)
        {
            if (string.IsNullOrEmpty(backupFolder)) return false;

            try
            {
                string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                if (!File.Exists(source)) return true;

                string target = Path.Combine(backupFolder, relativePath);
                string targetDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                File.Copy(source, target, true);
                GlobalCommData.ShowLog(Tag, "已备份 " + relativePath + " -> " + target);
                return true;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "备份失败 " + relativePath + " " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

        /// <summary>备份整个配置子目录（保持相对目录结构）</summary>
        /// <param name="relativeDirectory">相对程序基目录的目录</param>
        /// <param name="backupFolder">备份根目录</param>
        /// <returns>备份成功返回 true；源目录不存在视为成功</returns>
        public static bool BackupDirectory(string relativeDirectory, string backupFolder)
        {
            if (string.IsNullOrEmpty(backupFolder)) return false;

            try
            {
                string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeDirectory);
                if (!Directory.Exists(source)) return true;

                string target = Path.Combine(backupFolder, relativeDirectory);
                CopyDirectory(source, target);
                GlobalCommData.ShowLog(Tag, "已备份目录 " + relativeDirectory + " -> " + target);
                return true;
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "备份目录失败 " + relativeDirectory + " " + ex.Message, MessageLevel.Error);
                return false;
            }
        }

    }
}
