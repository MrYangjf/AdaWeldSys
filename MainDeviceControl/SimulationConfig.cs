using System;
using System.IO;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ConfigFileControl.Implementations;

namespace AdaWeldSystem.MainDeviceControl
{
    /// <summary>模拟测试参数配置（SimulationController.ini 的唯一持有者）</summary>
    public sealed class SimulationConfig
    {
        #region 常量

        /// <summary>仿真配置所属模块（全局配置目录下的模块层）。</summary>
        private const string ConfigModule = "MainDeviceControl";

        private const string Section = "Simulation";

        /// <summary>线激光数据频率键名（30/60/80/120Hz）</summary>
        private const string KeyLaserFrequencyHz = "LineLaserDataFrequencyHz";

        /// <summary>焊接前寻位起点 X 键名（毫米）：模拟机器人 PreWork 段移动到该点后停</summary>
        private const string KeyPreWorkStartXmm = "PreWorkStartXmm";

        /// <summary>焊接前寻位起点 X 缺省值（毫米）</summary>
        public const double DefaultPreWorkStartXmm = 100.0;

        #endregion

        /// <summary>默认配置文件绝对路径（与 ConfigFileManager 载体同路径）</summary>
        public static string DefaultFilePath
        {
            get { return ConfigFileManager.ModuleAbsolutePath(ConfigModule, "SimulationController.ini"); }
        }

        #region 私有变量

        private readonly object _sync = new object();
        private readonly string _filePath;

        #endregion

        #region 构造函数

        /// <summary>创建模拟参数配置对象</summary>
        /// <param name="filePath">INI 绝对路径</param>
        public SimulationConfig(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            _filePath = filePath;
        }

        #endregion

        #region 公共方法

        /// <summary>读取线激光数据频率</summary>
        /// <param name="fallback">文件或键缺失时的缺省值（保留调用方当前值）</param>

        /// <returns>频率（Hz）</returns>
        public int LoadLaserFrequencyHz(int fallback)
        {
            lock (_sync)
            {
                try
                {
                    if (!File.Exists(_filePath)) return fallback;
                    // 每次新建实例读取，避免缓存与外部改动不一致
                    INIFile ini = ConfigFileManager.Instance.GetModuleIni(ConfigModule, "SimulationController.ini").Raw;
                    return ini.ReadInt(Section, KeyLaserFrequencyHz, fallback);
                }
                catch (Exception)
                {
                    return fallback;   // 配置损坏时按缺省值继续，不影响启动
                }
            }
        }

        /// <summary>保存线激光数据频率</summary>
        /// <param name="value">频率（Hz）</param>

        /// <returns>保存成功返回 true</returns>
        public bool SaveLaserFrequencyHz(int value)
        {
            lock (_sync)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    IniConfigFile file = ConfigFileManager.Instance.GetModuleIni(ConfigModule, "SimulationController.ini");
                    INIFile ini = file.Raw;
                    ini.WriteInt(Section, KeyLaserFrequencyHz, value);
                    ConfigFileManager.SaveIni(file, "仿真参数", "配置变更");
                    return true;
                }
                catch (Exception)
                {
                    return false;   // 磁盘/权限异常时不阻断界面操作
                }
            }
        }

        /// <summary>读取焊接前寻位起点 X（毫米）</summary>
        /// <param name="fallback">文件或键缺失时的缺省值</param>

        /// <returns>起点 X（毫米）</returns>
        public double LoadPreWorkStartXmm(double fallback)
        {
            lock (_sync)
            {
                try
                {
                    if (!File.Exists(_filePath)) return fallback;
                    INIFile ini = ConfigFileManager.Instance.GetModuleIni(ConfigModule, "SimulationController.ini").Raw;
                    double value = ini.ReadDouble(Section, KeyPreWorkStartXmm, fallback);
                    return value < 0.0 ? fallback : value;
                }
                catch (Exception)
                {
                    return fallback;   // 配置损坏时按缺省值继续，不影响启动
                }
            }
        }

        /// <summary>保存焊接前寻位起点 X（毫米）</summary>
        /// <param name="value">起点 X（毫米），负值按缺省值处理</param>

        /// <returns>保存成功返回 true</returns>
        public bool SavePreWorkStartXmm(double value)
        {
            lock (_sync)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    IniConfigFile file = ConfigFileManager.Instance.GetModuleIni(ConfigModule, "SimulationController.ini");
                    INIFile ini = file.Raw;
                    ini.WriteDouble(Section, KeyPreWorkStartXmm, value < 0.0 ? DefaultPreWorkStartXmm : value);
                    ConfigFileManager.SaveIni(file, "仿真参数", "配置变更");
                    return true;
                }
                catch (Exception)
                {
                    return false;   // 磁盘/权限异常时不阻断界面操作
                }
            }
        }

        #endregion
    }
}
