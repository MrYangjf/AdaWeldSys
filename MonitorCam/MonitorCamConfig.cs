using AdaWeldSystem.FileOperate;
using System.IO;

namespace AdaWeldSystem.MonitorCam
{
    /// <summary>
    /// 监控相机硬件配置（IP/端口/曝光/图像尺寸）。
    /// 算法阈值（对中容差 / 质量合格分）不在此处，由算法层配置承载（见 EmguALG 各检测配置）。
    /// 持久化到 Config/INI/MonitorCamera.ini（遵循 ADR-007）。
    /// </summary>
    public class MonitorCamConfig
    {
        /// <summary>默认配置文件路径</summary>
        public static string DefaultConfigPath
        {
            get
            {
                return Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "MonitorCamera.ini");
            }
        }

        /// <summary>相机 IP 地址（GigE 用于设备匹配）</summary>
        public string IpAddress { get; set; }

        /// <summary>相机端口（麦格威原生接口不使用，保留以兼容历史配置）</summary>
        public string Port { get; set; }

        /// <summary>曝光时间（毫秒）</summary>
        public int ExposureMs { get; set; }

        /// <summary>图像宽度</summary>
        public int ImageWidth { get; set; }

        /// <summary>图像高度</summary>
        public int ImageHeight { get; set; }

        /// <summary>
        /// 创建配置并填入默认值（ADR-002：构造函数内初始化）
        /// </summary>
        public MonitorCamConfig()
        {
            IpAddress = "192.168.1.100";
            Port = "5000";
            ExposureMs = 10;
            ImageWidth = 640;
            ImageHeight = 480;
        }

        /// <summary>
        /// 将配置写入 INI 文件
        /// </summary>
        /// <param name="filePath">目标文件路径</param>
        public void SaveToIni(string filePath)
        {
            var ini = new INIFile(filePath);
            ini.WriteString("Camera", "IpAddress", IpAddress);
            ini.WriteString("Camera", "Port", Port);
            ini.WriteInt("Camera", "ExposureMs", ExposureMs);
            ini.WriteInt("Camera", "ImageWidth", ImageWidth);
            ini.WriteInt("Camera", "ImageHeight", ImageHeight);
            ini.SaveToFile();
        }

        /// <summary>
        /// 从 INI 文件读取配置；文件不存在时以默认值落地一份
        /// </summary>
        /// <param name="filePath">源文件路径</param>
        public void LoadFromIni(string filePath)
        {
            if (!File.Exists(filePath))
            {
                SaveToIni(filePath);
                return;
            }
            var ini = new INIFile(filePath);
            IpAddress = ini.ReadString("Camera", "IpAddress", IpAddress);
            Port = ini.ReadString("Camera", "Port", Port);
            ExposureMs = ini.ReadInt("Camera", "ExposureMs", ExposureMs);
            ImageWidth = ini.ReadInt("Camera", "ImageWidth", ImageWidth);
            ImageHeight = ini.ReadInt("Camera", "ImageHeight", ImageHeight);
        }
    }
}
