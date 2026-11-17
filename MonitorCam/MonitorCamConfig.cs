﻿﻿﻿using AdaWeldSystem.FileOperate;
using System;
using System.Collections.Generic;
using System.IO;

namespace AdaWeldSystem.MonitorCam
{
    /// <summary>
    /// 监控相机硬件配置（IP/曝光/尺寸/目录）。
    /// 算法阈值归算法层配置，不在此处。
    /// 持久化到 MonitorCamera.ini（Camera 节 + Calibration 节）。
    /// </summary>
    public class MonitorCamConfig
    {
        /// <summary>录像目录默认值，仅此一处定义</summary>
        public const string DefaultRecordDir = @"D:\WeldRecord";

        /// <summary>默认配置文件路径</summary>
        public static string DefaultConfigPath
        {
            get
            {
                return Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory, "Config", "MonitorCam", "Monitor_INI", "MonitorCamera.ini");
            }
        }

        /// <summary>相机 IP 地址（GigE 用于设备匹配）</summary>
        public string IpAddress { get; set; }

        /// <summary>相机端口（海康 SDK 不使用，仅兼容既有配置）</summary>
        public string Port { get; set; }

        /// <summary>曝光时间（毫秒）</summary>
        public int ExposureMs { get; set; }

        /// <summary>图像宽度</summary>
        public int ImageWidth { get; set; }

        /// <summary>图像高度</summary>
        public int ImageHeight { get; set; }

        /// <summary>录像保存目录（默认 D:\WeldRecord；回放页列表读取同一目录）</summary>
        public string RecordDir { get; set; }

        /// <summary>标定尺度（mm/pixel）</summary>
        public double MmPerPixel { get; set; }

        /// <summary>光斑点坐标 X（像素，画面按此坐标画十字）</summary>
        public int SpotX { get; set; }

        /// <summary>光斑点坐标 Y（像素，画面按此坐标画十字）</summary>
        public int SpotY { get; set; }

        /// <summary>
        /// 创建配置并填入默认值
        /// </summary>
        public MonitorCamConfig()
        {
            IpAddress = "192.168.1.100";
            Port = "5000";
            ExposureMs = 10;
            ImageWidth = 640;
            ImageHeight = 480;
            RecordDir = DefaultRecordDir;
            MmPerPixel = 1.0;
            SpotX = 0;
            SpotY = 0;
        }

        /// <summary>按当前值构造 INI 期望键集</summary>
        /// <remarks>默认值单一来源：字段当前值</remarks>
        /// <returns>结果集合。</returns>
        private List<ConfigSelfHeal.IniDefault> BuildIniDefaults()
        {
            List<ConfigSelfHeal.IniDefault> defaults = new List<ConfigSelfHeal.IniDefault>();
            defaults.Add(ConfigSelfHeal.Key("Camera", "IpAddress", IpAddress));
            defaults.Add(ConfigSelfHeal.Key("Camera", "Port", Port));
            defaults.Add(ConfigSelfHeal.Key("Camera", "ExposureMs", ExposureMs.ToString()));
            defaults.Add(ConfigSelfHeal.Key("Camera", "ImageWidth", ImageWidth.ToString()));
            defaults.Add(ConfigSelfHeal.Key("Camera", "ImageHeight", ImageHeight.ToString()));
            defaults.Add(ConfigSelfHeal.Key("Camera", "RecordDir", RecordDir));
            defaults.Add(ConfigSelfHeal.Key("Calibration", "MmPerPixel", MmPerPixel.ToString()));
            defaults.Add(ConfigSelfHeal.Key("Calibration", "SpotX", SpotX.ToString()));
            defaults.Add(ConfigSelfHeal.Key("Calibration", "SpotY", SpotY.ToString()));
            return defaults;
        }

        /// <summary>把完整路径换算为相对程序基目录的路径</summary>
        /// <param name="fullPath">完整路径。</param>
        /// <remarks>备份助手使用</remarks>
        /// <returns>结果文本。</returns>
        private static string RelativeToBase(string fullPath)
        {
            string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            string full = Path.GetFullPath(fullPath);
            if (full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                return full.Substring(baseDir.Length);
            return full;
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
            ini.WriteString("Camera", "RecordDir", RecordDir);
            ini.WriteDouble("Calibration", "MmPerPixel", MmPerPixel);
            ini.WriteInt("Calibration", "SpotX", SpotX);
            ini.WriteInt("Calibration", "SpotY", SpotY);
            ini.SaveToFile();
        }

        /// <summary>把当前配置落盘到默认 INI 路径（连接 IP 等修改后调用）</summary>
        public void SaveConfig()
        {
            SaveToIni(DefaultConfigPath);
        }

        /// <summary>从 INI 文件读取配置</summary>
        /// <param name="filePath">源文件路径</param>
        public void LoadFromIni(string filePath)
        {
            LoadFromIni(filePath, true);
        }

        /// <summary>从 INI 文件读取配置（可控制缺键补写）</summary>
        /// <param name="filePath">源文件路径</param>
        /// <param name="allowHeal">true = 缺键补写/缺文件生成模板；false = 预览读，零写盘</param>
        public void LoadFromIni(string filePath, bool allowHeal)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (!File.Exists(filePath))
            {
                if (!allowHeal) return;
                // 整文件缺失则按默认值生成完整模板
                ConfigSelfHeal.EnsureIni(filePath, RelativeToBase(filePath), Path.GetFileName(filePath),
                    BuildIniDefaults(), true);
            }

            var ini = new INIFile(filePath);
            IpAddress = ini.ReadString("Camera", "IpAddress", IpAddress);
            Port = ini.ReadString("Camera", "Port", Port);
            ExposureMs = ini.ReadInt("Camera", "ExposureMs", ExposureMs);
            ImageWidth = ini.ReadInt("Camera", "ImageWidth", ImageWidth);
            ImageHeight = ini.ReadInt("Camera", "ImageHeight", ImageHeight);
            RecordDir = ini.ReadString("Camera", "RecordDir", RecordDir);
            // 缺失回落默认值，仅内存纠正不写盘
            if (string.IsNullOrWhiteSpace(RecordDir)) RecordDir = DefaultRecordDir;
            MmPerPixel = ini.ReadDouble("Calibration", "MmPerPixel", MmPerPixel);
            SpotX = ini.ReadInt("Calibration", "SpotX", SpotX);
            SpotY = ini.ReadInt("Calibration", "SpotY", SpotY);

            if (allowHeal)
            {
                // 只补缺失键，已有键值逐字保留
                ConfigSelfHeal.EnsureIni(filePath, RelativeToBase(filePath), Path.GetFileName(filePath),
                    BuildIniDefaults(), true);
            }
        }

    }
}
