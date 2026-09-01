using System;
using System.IO;

namespace AdaWeldSystem.LineLaserCam.SmartRayCam
{
    /// <summary>
    /// 相机参数预设管理器（单例）
    /// 管理 10 组相机参数预设的持久化
    /// </summary>
    public class CameraPresetManager
    {
        #region 单例
        private static readonly Lazy<CameraPresetManager> _lazy = new Lazy<CameraPresetManager>(() => new CameraPresetManager());
        public static CameraPresetManager Instance => _lazy.Value;
        #endregion

        private readonly string _iniPath;
        private readonly AdaWeldSystem.FileOperate.INIFile _ini;

        private CameraPresetManager()
        {
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            _iniPath = Path.Combine(configDir, "CameraPresets.ini");
            _ini = new AdaWeldSystem.FileOperate.INIFile(_iniPath);
        }

        /// <summary>
        /// 保存指定预设组
        /// </summary>
        /// <param name="presetIndex">预设组索引（1-10）</param>
        /// <param name="data">预设数据</param>
        public void SavePreset(int presetIndex, CameraPresetData data)
        {
            if (presetIndex < 1 || presetIndex > 10)
                throw new ArgumentOutOfRangeException(nameof(presetIndex), "预设组索引必须在 1-10 范围内");
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string section = string.Format("Preset_{0}", presetIndex);
            _ini.WriteString(section, "CameraType", data.CameraType);
            _ini.WriteString(section, "IPAddress", data.IPAddress);
            _ini.WriteInt(section, "Port", data.Port);
            _ini.WriteInt(section, "ExposureTime", data.ExposureTime);
            _ini.WriteInt(section, "LaserIntensity", data.LaserIntensity);
            _ini.WriteString(section, "LaserMode", data.LaserMode);
            _ini.WriteInt(section, "ROIX", data.ROIX);
            _ini.WriteInt(section, "ROIY", data.ROIY);
            _ini.WriteInt(section, "ROIWidth", data.ROIWidth);
            _ini.WriteInt(section, "ROIHeight", data.ROIHeight);
            _ini.SaveToFile();
        }

        /// <summary>
        /// 加载指定预设组
        /// </summary>
        /// <param name="presetIndex">预设组索引（1-10）</param>
        /// <returns>预设数据</returns>
        public CameraPresetData LoadPreset(int presetIndex)
        {
            if (presetIndex < 1 || presetIndex > 10)
                throw new ArgumentOutOfRangeException(nameof(presetIndex), "预设组索引必须在 1-10 范围内");

            string section = string.Format("Preset_{0}", presetIndex);
            CameraPresetData data = new CameraPresetData();

            if (_ini.SectionExists(section))
            {
                data.CameraType = _ini.ReadString(section, "CameraType", data.CameraType);
                data.IPAddress = _ini.ReadString(section, "IPAddress", data.IPAddress);
                data.Port = _ini.ReadInt(section, "Port", data.Port);
                data.ExposureTime = _ini.ReadInt(section, "ExposureTime", data.ExposureTime);
                data.LaserIntensity = _ini.ReadInt(section, "LaserIntensity", data.LaserIntensity);
                data.LaserMode = _ini.ReadString(section, "LaserMode", data.LaserMode);
                data.ROIX = _ini.ReadInt(section, "ROIX", data.ROIX);
                data.ROIY = _ini.ReadInt(section, "ROIY", data.ROIY);
                data.ROIWidth = _ini.ReadInt(section, "ROIWidth", data.ROIWidth);
                data.ROIHeight = _ini.ReadInt(section, "ROIHeight", data.ROIHeight);
            }

            return data;
        }

        /// <summary>
        /// 获取所有预设名称列表
        /// </summary>
        /// <returns>包含 10 个预设名称的数组</returns>
        public string[] GetPresetNames()
        {
            string[] names = new string[10];
            for (int i = 1; i <= 10; i++)
            {
                names[i - 1] = string.Format("预设组 {0}", i);
            }
            return names;
        }
    }

    /// <summary>
    /// 相机参数预设数据结构
    /// </summary>
    public class CameraPresetData
    {
        public CameraPresetData()
        {
            CameraType = "SmartRay";
            IPAddress = "192.168.178.200";
            Port = 40;
            ExposureTime = 5000;
            LaserIntensity = 80;
            LaserMode = "Standard";
            ROIX = 0;
            ROIY = 0;
            ROIWidth = 1920;
            ROIHeight = 1200;
        }

        public string CameraType { get; set; }
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public int ExposureTime { get; set; }
        public int LaserIntensity { get; set; }
        public string LaserMode { get; set; }
        public int ROIX { get; set; }
        public int ROIY { get; set; }
        public int ROIWidth { get; set; }
        public int ROIHeight { get; set; }
    }
}
