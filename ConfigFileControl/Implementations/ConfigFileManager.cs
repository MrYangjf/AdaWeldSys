using System;
using System.Collections.Generic;
using System.IO;
using AdaWeldSystem.ConfigFileControl.Abstractions;

namespace AdaWeldSystem.ConfigFileControl.Implementations
{
    /// <summary>配置文档总管</summary>
    public class ConfigFileManager
    {
        private static ConfigFileManager _instance;

        /// <summary>全局唯一实例（普通单例，与 WeldParamManager / MontionManager 同形态）。</summary>
        public static ConfigFileManager Instance
        {
            get
            {
                if (_instance == null) _instance = new ConfigFileManager();
                return _instance;
            }
        }

        /// <summary>全局配置根目录</summary>
        public const string ConfigRootRelativeDir = @"Config\ConfigFileControl";

        /// <summary>INI 载体缓存（文件名 → 载体）。</summary>
        private readonly Dictionary<string, IniConfigFile> _iniFiles = new Dictionary<string, IniConfigFile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>XML 载体缓存（文件名 → 载体）。</summary>
        private readonly Dictionary<string, XmlConfigFile> _xmlFiles = new Dictionary<string, XmlConfigFile>(StringComparer.OrdinalIgnoreCase);

        private ConfigFileManager()
        {
        }

        /// <summary>配置文件绝对路径。</summary>
        /// <param name="fileName">文件名（含扩展名）</param>
        /// <returns>绝对路径</returns>
        public static string AbsolutePath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigRootRelativeDir, fileName);
        }

        /// <summary>取 INI 载体（不存在则创建并登记）。</summary>
        /// <param name="fileName">文件名（如 <c>WeldParam.ini</c>）</param>
        /// <returns>INI 载体</returns>
        public IniConfigFile GetIni(string fileName)
        {
            if (_iniFiles.TryGetValue(fileName, out IniConfigFile file)) return file;
            string abs = AbsolutePath(fileName);
            string dir = Path.GetDirectoryName(abs);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            file = new IniConfigFile(abs, fileName, ConfigRootRelativeDir + "\\" + fileName);
            _iniFiles[fileName] = file;
            return file;
        }

        /// <summary>取 XML 载体（不存在则创建并登记）。</summary>
        /// <param name="fileName">文件名（不含扩展名，如 <c>WeldParam</c>）</param>
        /// <param name="createIfMissing">文件缺失时是否创建</param>
        /// <returns>XML 载体</returns>
        public XmlConfigFile GetXml(string fileName, bool createIfMissing = true)
        {
            if (_xmlFiles.TryGetValue(fileName, out XmlConfigFile file)) return file;
            file = new XmlConfigFile(ConfigRootRelativeDir, fileName, createIfMissing);
            _xmlFiles[fileName] = file;
            return file;
        }

        /// <summary>已登记的全部载体文件名（诊断/自愈遍历用）。</summary>
        public IEnumerable<string> RegisteredFiles()
        {
            foreach (string name in _iniFiles.Keys) yield return name;
            foreach (string name in _xmlFiles.Keys) yield return name;
        }

        /// <summary>全部载体重新从磁盘载入。</summary>
        public void ReloadAll()
        {
            foreach (IniConfigFile file in _iniFiles.Values) file.Reload();
            foreach (XmlConfigFile file in _xmlFiles.Values) file.Reload();
        }

        /// <summary>模块子目录的相对路径</summary>
        /// <param name="module">模块名（如 <c>Comm</c>）</param>
        /// <returns>相对路径</returns>
        public static string ModuleRelativeDir(string module)
        {
            if (string.IsNullOrEmpty(module)) return ConfigRootRelativeDir;
            return ConfigRootRelativeDir + "\\" + module;
        }

        /// <summary>取模块下的 INI 载体（不存在则创建并登记）。</summary>
        /// <param name="module">模块名</param>
        /// <param name="fileName">文件名（含扩展名）</param>
        /// <returns>INI 载体</returns>
        public IniConfigFile GetModuleIni(string module, string fileName)
        {
            string key = module + "/" + fileName;
            if (_iniFiles.TryGetValue(key, out IniConfigFile file)) return file;
            string relDir = ModuleRelativeDir(module);
            string abs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relDir, fileName);
            string dir = Path.GetDirectoryName(abs);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            file = new IniConfigFile(abs, fileName, relDir + "\\" + fileName);
            _iniFiles[key] = file;
            return file;
        }

        /// <summary>取模块下的 XML 载体（不存在则创建并登记）。</summary>
        /// <param name="module">模块名</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <param name="createIfMissing">文件缺失时是否创建</param>
        /// <returns>XML 载体</returns>
        public XmlConfigFile GetModuleXml(string module, string fileName, bool createIfMissing = true)
        {
            string key = module + "/" + fileName;
            if (_xmlFiles.TryGetValue(key, out XmlConfigFile file)) return file;
            string relDir = ModuleRelativeDir(module);
            file = new XmlConfigFile(relDir, fileName, createIfMissing);
            _xmlFiles[key] = file;
            return file;
        }

        /// <summary>模块内文件的绝对路径。</summary>
        /// <param name="module">模块名</param>
        /// <param name="fileName">文件名（含扩展名）</param>
        /// <returns>绝对路径</returns>
        public static string ModuleAbsolutePath(string module, string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModuleRelativeDir(module), fileName);
        }

        /// <summary>保存 INI 载体并按文件键值记录变动项。</summary>
        /// <param name="file">INI 载体</param>
        /// <param name="scene">场景名</param>
        /// <param name="tag">日志标签</param>
        /// <param name="saveAction">执行保存的动作（缺省为载体落盘）</param>
        public static void SaveIniWithAudit(IniConfigFile file, string scene, string tag, Action saveAction = null)
        {
            if (file == null) return;

            Dictionary<string, string> before = ConfigSnapshot.OfIni(file);
            if (saveAction != null)
            {
                saveAction();
            }
            else
            {
                file.Save();
            }
            ConfigSnapshot.Diff(scene, before, ConfigSnapshot.OfIni(file), tag);
        }

        /// <summary>保存 INI 载体并记录变动项（就地落盘）。</summary>
        /// <param name="file">INI 载体</param>
        /// <param name="scene">场景名</param>
        /// <param name="tag">日志标签</param>
        public static void SaveIni(IniConfigFile file, string scene, string tag)
        {
            SaveIniWithAudit(file, scene, tag, null);
        }

        /// <summary>保存 XML 载体并按元素值记录变动项。</summary>
        /// <param name="file">XML 载体</param>
        /// <param name="scene">场景名</param>
        /// <param name="tag">日志标签</param>
        /// <param name="saveAction">执行保存的动作（缺省为载体落盘）</param>
        public static void SaveXmlWithAudit(XmlConfigFile file, string scene, string tag, Action saveAction = null)
        {
            if (file == null) return;

            Dictionary<string, string> before = ConfigSnapshot.OfXml(file);
            if (saveAction != null)
            {
                saveAction();
            }
            else
            {
                file.Save();
            }
            ConfigSnapshot.Diff(scene, before, ConfigSnapshot.OfXml(file), tag);
        }

    }
}
