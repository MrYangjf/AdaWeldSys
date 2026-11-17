using System;
using System.IO;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.MainDeviceControl
{
    /// <summary>模拟测试参数配置（SimulationController.ini 的唯一持有者）</summary>
    /// <remarks>业务类（<see cref="DeviceControlWork"/>）只调用本对象，⛔ 不再自行 <c>new INIFile</c>。
    /// 生命周期：由 <see cref="DeviceControlWork"/> 单例以只读字段持有，全进程一份。
    /// 线程安全：读写同一把锁，避免交叉写文件。</remarks>
    public sealed class SimulationConfig
    {
        #region 常量

        /// <summary>节名</summary>
        private const string Section = "Simulation";

        /// <summary>线激光数据频率键名（30/60/80/120Hz）</summary>
        private const string KeyLaserFrequencyHz = "LineLaserDataFrequencyHz";

        #endregion

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
        /// <remarks>读取失败不抛异常，缺失不阻断启动。</remarks>
        /// <returns>频率（Hz）</returns>
        public int LoadLaserFrequencyHz(int fallback)
        {
            lock (_sync)
            {
                try
                {
                    if (!File.Exists(_filePath)) return fallback;
                    // 每次新建实例读取，避免缓存与外部改动不一致
                    INIFile ini = new INIFile(_filePath);
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
        /// <remarks>写失败不抛异常，保存失败不影响运行时。</remarks>
        /// <returns>保存成功返回 true</returns>
        public bool SaveLaserFrequencyHz(int value)
        {
            lock (_sync)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    INIFile ini = new INIFile(_filePath);
                    ini.WriteInt(Section, KeyLaserFrequencyHz, value);
                    ini.SaveToFile();
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
