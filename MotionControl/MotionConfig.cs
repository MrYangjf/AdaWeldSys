using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AdaWeldSystem.Comm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MotionControl.ZMotion;

namespace AdaWeldSystem.MotionControl
{
    // ============================================================
    // AxisConfig — 单轴配置模型
    // ============================================================

    /// <summary>
    /// 单轴配置模型
    /// 包含轴的逻辑名称、物理编号、运动参数等。
    /// 通过轴字典（名称→配置）实现按名称查找和控制。
    /// </summary>
    [Serializable]
    public class AxisConfig
    {
        /// <summary>轴逻辑名称（如 "Y轴"、"振镜轴"）</summary>
        public string Name { get; set; }

        /// <summary>轴物理编号（0-based）</summary>
        public int AxisNumber { get; set; }

        /// <summary>脉冲当量（脉冲/mm）</summary>
        public float Units { get; set; }

        /// <summary>起始速度（mm/s）</summary>
        public float LSpeed { get; set; }

        /// <summary>运行速度（mm/s）</summary>
        public float Speed { get; set; }

        /// <summary>加速度（mm/s²）</summary>
        public float Accel { get; set; }

        /// <summary>减速度（mm/s²）</summary>
        public float Decel { get; set; }

        /// <summary>S曲线时间（ms）</summary>
        public float Sramp { get; set; }

        /// <summary>正软限位（mm）</summary>
        public float SoftLimitPositive { get; set; }

        /// <summary>负软限位（mm）</summary>
        public float SoftLimitNegative { get; set; }

        /// <summary>回零模式（正运动 Home 模式编号）</summary>
        public int HomeMode { get; set; }

        /// <summary>回零速度（mm/s）</summary>
        public float HomeSpeed { get; set; }

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 构造函数（默认值）
        /// </summary>
        public AxisConfig()
        {
            Name = string.Empty;
            AxisNumber = 0;
            Units = 1000f;
            LSpeed = 10f;
            Speed = 50f;
            Accel = 500f;
            Decel = 500f;
            Sramp = 50f;
            SoftLimitPositive = 9999f;
            SoftLimitNegative = -9999f;
            HomeMode = 0;
            HomeSpeed = 20f;
            Enabled = true;
        }
    }

    // ============================================================
    // IOConfig — IO配置模型
    // ============================================================

    /// <summary>
    /// IO 配置模型
    /// 包含IO的逻辑名称、物理编号、类型等。
    /// 通过IO字典（名称→配置）实现按名称查找和控制。
    /// </summary>
    [Serializable]
    public class IOConfig
    {
        /// <summary>IO逻辑名称（如 "焊枪启动"、"压力传感器信号"）</summary>
        public string Name { get; set; }

        /// <summary>IO物理编号</summary>
        public int IONumber { get; set; }

        /// <summary>IO类型：Input 或 Output</summary>
        public string IOType { get; set; }

        /// <summary>急停时是否自动关闭（仅输出IO有效）</summary>
        public bool EmergencyStopOff { get; set; }

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; }

        /// <summary>描述/备注</summary>
        public string Description { get; set; }

        /// <summary>
        /// 构造函数（默认值）
        /// </summary>
        public IOConfig()
        {
            Name = string.Empty;
            IONumber = 0;
            IOType = "Input";
            EmergencyStopOff = true;
            Enabled = true;
            Description = string.Empty;
        }
    }

    // ============================================================
    // HeartbeatConfig — 心跳检测配置
    // ============================================================

    /// <summary>
    /// 心跳检测配置
    /// </summary>
    [Serializable]
    public class HeartbeatConfig
    {
        /// <summary>是否启用心跳检测</summary>
        public bool Enabled { get; set; }

        /// <summary>心跳间隔（ms）</summary>
        public int IntervalMs { get; set; }

        /// <summary>超时时间（ms）— 超过此时间未收到响应视为断连</summary>
        public int TimeoutMs { get; set; }

        /// <summary>是否启用BAS心跳保护程序（控制器端保护输出）</summary>
        public bool BasProtectionEnabled { get; set; }

        /// <summary>心跳保护BAS程序路径</summary>
        public string BasProgramPath { get; set; }

        /// <summary>
        /// 构造函数（默认值）
        /// </summary>
        public HeartbeatConfig()
        {
            Enabled = true;
            IntervalMs = 1000;
            TimeoutMs = 3000;
            BasProtectionEnabled = false;
            BasProgramPath = string.Empty;
        }
    }

    // ============================================================
    // MotionControllerConfig — 控制器整体配置
    // ============================================================

    /// <summary>
    /// 运动控制器整体配置
    /// 包含连接参数、轴字典、IO字典、心跳配置等。
    /// 所有配置存储在一个INI文件中，支持持久化。
    /// </summary>
    [Serializable]
    public class MotionControllerConfig
    {
        private const string Tag = "MotionControllerConfig";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>控制器名称</summary>
        public string ControllerName { get; set; }

        /// <summary>控制器IP地址（以太网连接）</summary>
        public string IpAddress { get; set; }

        /// <summary>连接超时时间（ms）</summary>
        public int ConnectTimeoutMs { get; set; }

        /// <summary>轴字典：键=轴逻辑名称，值=轴配置</summary>
        public Dictionary<string, AxisConfig> AxisDictionary { get; private set; }

        /// <summary>输入IO字典：键=IO逻辑名称，值=IO配置</summary>
        public Dictionary<string, IOConfig> InputIODictionary { get; private set; }

        /// <summary>输出IO字典：键=IO逻辑名称，值=IO配置</summary>
        public Dictionary<string, IOConfig> OutputIODictionary { get; private set; }

        /// <summary>心跳检测配置</summary>
        public HeartbeatConfig Heartbeat { get; private set; }

        /// <summary>V3 CAMBOX 跟踪配置（主轴/从轴/前置补偿/最大纠偏等）</summary>
        public CamBoxTrackingConfig CamBoxTracking { get; private set; }

        /// <summary>V3 通讯编码器轴配置（ATYPE=25）</summary>
        public EncoderAxisConfig EncoderAxis { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public MotionControllerConfig()
        {
            ControllerName = "正运动控制器";
            IpAddress = "192.168.0.11";
            ConnectTimeoutMs = 2000;
            AxisDictionary = new Dictionary<string, AxisConfig>();
            InputIODictionary = new Dictionary<string, IOConfig>();
            OutputIODictionary = new Dictionary<string, IOConfig>();
            Heartbeat = new HeartbeatConfig();
            CamBoxTracking = new CamBoxTrackingConfig();
            EncoderAxis = new EncoderAxisConfig();
        }

        /// <summary>
        /// 加载默认4轴配置（用于首次启动无配置文件时）
        /// </summary>
        public void LoadDefaultAxes()
        {
            AxisDictionary.Clear();

            // 轴0: Y轴
            AxisDictionary.Add("Y轴", new AxisConfig
            {
                Name = "Y轴",
                AxisNumber = 0,
                Units = 1000f,
                LSpeed = 10f,
                Speed = 50f,
                Accel = 500f,
                Decel = 500f,
                Sramp = 50f,
                HomeMode = 0,
                HomeSpeed = 20f,
                Enabled = true
            });

            // 轴1: 振镜轴
            AxisDictionary.Add("振镜轴", new AxisConfig
            {
                Name = "振镜轴",
                AxisNumber = 1,
                Units = 1000f,
                LSpeed = 20f,
                Speed = 200f,
                Accel = 2000f,
                Decel = 2000f,
                Sramp = 20f,
                HomeMode = 0,
                HomeSpeed = 50f,
                Enabled = true
            });

            // 轴2: 送丝伸缩臂轴
            AxisDictionary.Add("送丝伸缩臂轴", new AxisConfig
            {
                Name = "送丝伸缩臂轴",
                AxisNumber = 2,
                Units = 1000f,
                LSpeed = 5f,
                Speed = 30f,
                Accel = 300f,
                Decel = 300f,
                Sramp = 50f,
                HomeMode = 0,
                HomeSpeed = 10f,
                Enabled = true
            });

            // 轴3: Z轴自动对焦轴
            AxisDictionary.Add("Z轴自动对焦轴", new AxisConfig
            {
                Name = "Z轴自动对焦轴",
                AxisNumber = 3,
                Units = 1000f,
                LSpeed = 5f,
                Speed = 40f,
                Accel = 400f,
                Decel = 400f,
                Sramp = 50f,
                HomeMode = 0,
                HomeSpeed = 15f,
                Enabled = true
            });

            Log("已加载默认4轴配置");
        }

        /// <summary>
        /// 加载默认IO配置
        /// </summary>
        public void LoadDefaultIOs()
        {
            InputIODictionary.Clear();
            OutputIODictionary.Clear();

            // 输入IO
            InputIODictionary.Add("外部启动信号", new IOConfig
            {
                Name = "外部启动信号",
                IONumber = 0,
                IOType = "Input",
                Enabled = true,
                Description = "外部启动输入信号"
            });
            InputIODictionary.Add("外部停止信号", new IOConfig
            {
                Name = "外部停止信号",
                IONumber = 1,
                IOType = "Input",
                Enabled = true,
                Description = "外部停止输入信号"
            });
            InputIODictionary.Add("压力传感器信号", new IOConfig
            {
                Name = "压力传感器信号",
                IONumber = 2,
                IOType = "Input",
                Enabled = true,
                Description = "送丝臂压力传感器"
            });
            InputIODictionary.Add("位置传感器信号", new IOConfig
            {
                Name = "位置传感器信号",
                IONumber = 3,
                IOType = "Input",
                Enabled = true,
                Description = "送丝臂位置传感器"
            });

            // 输出IO
            OutputIODictionary.Add("焊枪启动", new IOConfig
            {
                Name = "焊枪启动",
                IONumber = 0,
                IOType = "Output",
                EmergencyStopOff = true,
                Enabled = true,
                Description = "焊枪启动输出"
            });
            OutputIODictionary.Add("送丝启动", new IOConfig
            {
                Name = "送丝启动",
                IONumber = 1,
                IOType = "Output",
                EmergencyStopOff = true,
                Enabled = true,
                Description = "送丝机启动输出"
            });
            OutputIODictionary.Add("振镜使能", new IOConfig
            {
                Name = "振镜使能",
                IONumber = 2,
                IOType = "Output",
                EmergencyStopOff = true,
                Enabled = true,
                Description = "振镜使能输出"
            });
            OutputIODictionary.Add("激光器使能", new IOConfig
            {
                Name = "激光器使能",
                IONumber = 3,
                IOType = "Output",
                EmergencyStopOff = true,
                Enabled = true,
                Description = "激光器使能输出"
            });

            Log("已加载默认IO配置");
        }

        /// <summary>
        /// 按轴号获取轴配置
        /// </summary>
        public AxisConfig GetAxisByNumber(int axisNumber)
        {
            foreach (var kvp in AxisDictionary)
            {
                if (kvp.Value.AxisNumber == axisNumber)
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>
        /// 按IO号获取输入IO配置
        /// </summary>
        public IOConfig GetInputIOByNumber(int ioNumber)
        {
            foreach (var kvp in InputIODictionary)
            {
                if (kvp.Value.IONumber == ioNumber)
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>
        /// 按IO号获取输出IO配置
        /// </summary>
        public IOConfig GetOutputIOByNumber(int ioNumber)
        {
            foreach (var kvp in OutputIODictionary)
            {
                if (kvp.Value.IONumber == ioNumber)
                    return kvp.Value;
            }
            return null;
        }
    }

    // ============================================================
    // MotionConfigManager — 配置持久化管理器（单例）
    // ============================================================

    /// <summary>
    /// 运动控制配置管理器（单例）
    /// 负责运动控制器配置的 INI 持久化：加载/保存轴字典、IO字典、连接参数、心跳配置等。
    /// 所有配置存储在单个 INI 文件中，遵循 ADR-008 ConfigLayout 规范。
    /// 
    /// INI 文件结构：
    ///   [Controller]
    ///   Name=正运动控制器
    ///   IpAddress=192.168.0.11
    ///   ConnectTimeoutMs=2000
    ///   
    ///   [AxisCount]
    ///   Count=4
    ///   
    ///   [Axis_0]
    ///   Name=Y轴
    ///   AxisNumber=0
    ///   Units=1000
    ///   Speed=50
    ///   ...
    ///   
    ///   [InputIOCount]
    ///   Count=4
    ///   
    ///   [InputIO_0]
    ///   Name=外部启动信号
    ///   IONumber=0
    ///   ...
    ///   
    ///   [OutputIOCount]
    ///   Count=4
    ///   
    ///   [OutputIO_0]
    ///   Name=焊枪启动
    ///   ...
    ///   
    ///   [Heartbeat]
    ///   Enabled=1
    ///   IntervalMs=1000
    ///   ...
    /// </summary>
    public class MotionConfigManager
    {
        private const string Tag = "MotionConfigManager";
        private const string ConfigFileName = "MotionController.ini";
        private const string ConfigSubDir = "INI";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private static readonly Lazy<MotionConfigManager> _lazy =
            new Lazy<MotionConfigManager>(() => new MotionConfigManager());

        /// <summary>单例实例</summary>
        public static MotionConfigManager Instance => _lazy.Value;

        /// <summary>当前配置</summary>
        public MotionControllerConfig Config { get; private set; }

        /// <summary>配置文件完整路径</summary>
        private string _configFilePath;

        private MotionConfigManager()
        {
            Config = new MotionControllerConfig();
            _configFilePath = string.Empty;
        }

        /// <summary>
        /// 获取配置文件路径
        /// 遵循 ADR-008：Config/INI/ 目录
        /// </summary>
        private string EnsureConfigPath()
        {
            if (!string.IsNullOrEmpty(_configFilePath) && File.Exists(_configFilePath))
                return _configFilePath;

            string configDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                ConfigSubDir);

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            _configFilePath = Path.Combine(configDir, ConfigFileName);
            return _configFilePath;
        }

        /// <summary>
        /// 从INI文件加载配置
        /// 如果配置文件不存在，加载默认配置并保存。
        /// </summary>
        /// <returns>是否加载成功</returns>
        public bool Load()
        {
            string configPath = EnsureConfigPath();

            if (!File.Exists(configPath))
            {
                Log("配置文件不存在，加载默认配置");
                Config.LoadDefaultAxes();
                Config.LoadDefaultIOs();
                Save();
                return true;
            }

            try
            {
                var ini = new INIFile(configPath);

                // 读取控制器基本配置
                Config.ControllerName = ini.ReadString("Controller", "Name", "正运动控制器");
                Config.IpAddress = ini.ReadString("Controller", "IpAddress", "192.168.0.11");
                Config.ConnectTimeoutMs = ini.ReadInt("Controller", "ConnectTimeoutMs", 2000);

                // 读取轴配置
                int axisCount = ini.ReadInt("AxisCount", "Count", 0);
                Config.AxisDictionary.Clear();
                for (int i = 0; i < axisCount; i++)
                {
                    string section = "Axis_" + i.ToString();
                    string name = ini.ReadString(section, "Name", string.Empty);
                    if (string.IsNullOrEmpty(name)) continue;

                    var axisCfg = new AxisConfig
                    {
                        Name = name,
                        AxisNumber = ini.ReadInt(section, "AxisNumber", i),
                        Units = (float)ini.ReadDouble(section, "Units", 1000.0),
                        LSpeed = (float)ini.ReadDouble(section, "LSpeed", 10.0),
                        Speed = (float)ini.ReadDouble(section, "Speed", 50.0),
                        Accel = (float)ini.ReadDouble(section, "Accel", 500.0),
                        Decel = (float)ini.ReadDouble(section, "Decel", 500.0),
                        Sramp = (float)ini.ReadDouble(section, "Sramp", 50.0),
                        SoftLimitPositive = (float)ini.ReadDouble(section, "SoftLimitPos", 9999.0),
                        SoftLimitNegative = (float)ini.ReadDouble(section, "SoftLimitNeg", -9999.0),
                        HomeMode = ini.ReadInt(section, "HomeMode", 0),
                        HomeSpeed = (float)ini.ReadDouble(section, "HomeSpeed", 20.0),
                        Enabled = ini.ReadBool(section, "Enabled", true)
                    };

                    if (!Config.AxisDictionary.ContainsKey(name))
                    {
                        Config.AxisDictionary.Add(name, axisCfg);
                    }
                }

                // 读取输入IO配置
                int inputCount = ini.ReadInt("InputIOCount", "Count", 0);
                Config.InputIODictionary.Clear();
                for (int i = 0; i < inputCount; i++)
                {
                    string section = "InputIO_" + i.ToString();
                    string name = ini.ReadString(section, "Name", string.Empty);
                    if (string.IsNullOrEmpty(name)) continue;

                    var ioCfg = new IOConfig
                    {
                        Name = name,
                        IONumber = ini.ReadInt(section, "IONumber", i),
                        IOType = "Input",
                        Enabled = ini.ReadBool(section, "Enabled", true),
                        Description = ini.ReadString(section, "Description", string.Empty)
                    };

                    if (!Config.InputIODictionary.ContainsKey(name))
                    {
                        Config.InputIODictionary.Add(name, ioCfg);
                    }
                }

                // 读取输出IO配置
                int outputCount = ini.ReadInt("OutputIOCount", "Count", 0);
                Config.OutputIODictionary.Clear();
                for (int i = 0; i < outputCount; i++)
                {
                    string section = "OutputIO_" + i.ToString();
                    string name = ini.ReadString(section, "Name", string.Empty);
                    if (string.IsNullOrEmpty(name)) continue;

                    var ioCfg = new IOConfig
                    {
                        Name = name,
                        IONumber = ini.ReadInt(section, "IONumber", i),
                        IOType = "Output",
                        EmergencyStopOff = ini.ReadBool(section, "EmergencyStopOff", true),
                        Enabled = ini.ReadBool(section, "Enabled", true),
                        Description = ini.ReadString(section, "Description", string.Empty)
                    };

                    if (!Config.OutputIODictionary.ContainsKey(name))
                    {
                        Config.OutputIODictionary.Add(name, ioCfg);
                    }
                }

                // 读取心跳配置
                Config.Heartbeat.Enabled = ini.ReadBool("Heartbeat", "Enabled", true);
                Config.Heartbeat.IntervalMs = ini.ReadInt("Heartbeat", "IntervalMs", 1000);
                Config.Heartbeat.TimeoutMs = ini.ReadInt("Heartbeat", "TimeoutMs", 3000);
                Config.Heartbeat.BasProtectionEnabled = ini.ReadBool("Heartbeat", "BasProtectionEnabled", false);
                Config.Heartbeat.BasProgramPath = ini.ReadString("Heartbeat", "BasProgramPath", string.Empty);

                Log(string.Format(
                    "配置加载完成 轴数 {0} 输入IO数 {1} 输出IO数 {2}",
                    Config.AxisDictionary.Count,
                    Config.InputIODictionary.Count,
                    Config.OutputIODictionary.Count));

                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("配置加载失败 原因是 {0}，使用默认配置", ex.Message));
                Config.LoadDefaultAxes();
                Config.LoadDefaultIOs();
                return false;
            }
        }

        /// <summary>
        /// 保存配置到INI文件
        /// </summary>
        /// <returns>是否保存成功</returns>
        public bool Save()
        {
            string configPath = EnsureConfigPath();

            try
            {
                var ini = new INIFile(configPath);

                // 保存控制器基本配置
                ini.WriteString("Controller", "Name", Config.ControllerName);
                ini.WriteString("Controller", "IpAddress", Config.IpAddress);
                ini.WriteInt("Controller", "ConnectTimeoutMs", Config.ConnectTimeoutMs);

                // 保存轴配置
                ini.WriteInt("AxisCount", "Count", Config.AxisDictionary.Count);

                int idx = 0;
                foreach (var kvp in Config.AxisDictionary)
                {
                    string section = "Axis_" + idx.ToString();
                    var a = kvp.Value;
                    ini.WriteString(section, "Name", a.Name);
                    ini.WriteInt(section, "AxisNumber", a.AxisNumber);
                    ini.WriteDouble(section, "Units", a.Units);
                    ini.WriteDouble(section, "LSpeed", a.LSpeed);
                    ini.WriteDouble(section, "Speed", a.Speed);
                    ini.WriteDouble(section, "Accel", a.Accel);
                    ini.WriteDouble(section, "Decel", a.Decel);
                    ini.WriteDouble(section, "Sramp", a.Sramp);
                    ini.WriteDouble(section, "SoftLimitPos", a.SoftLimitPositive);
                    ini.WriteDouble(section, "SoftLimitNeg", a.SoftLimitNegative);
                    ini.WriteInt(section, "HomeMode", a.HomeMode);
                    ini.WriteDouble(section, "HomeSpeed", a.HomeSpeed);
                    ini.WriteBool(section, "Enabled", a.Enabled);
                    idx++;
                }

                // 保存输入IO配置
                ini.WriteInt("InputIOCount", "Count", Config.InputIODictionary.Count);
                idx = 0;
                foreach (var kvp in Config.InputIODictionary)
                {
                    string section = "InputIO_" + idx.ToString();
                    var io = kvp.Value;
                    ini.WriteString(section, "Name", io.Name);
                    ini.WriteInt(section, "IONumber", io.IONumber);
                    ini.WriteBool(section, "Enabled", io.Enabled);
                    ini.WriteString(section, "Description", io.Description);
                    idx++;
                }

                // 保存输出IO配置
                ini.WriteInt("OutputIOCount", "Count", Config.OutputIODictionary.Count);
                idx = 0;
                foreach (var kvp in Config.OutputIODictionary)
                {
                    string section = "OutputIO_" + idx.ToString();
                    var io = kvp.Value;
                    ini.WriteString(section, "Name", io.Name);
                    ini.WriteInt(section, "IONumber", io.IONumber);
                    ini.WriteBool(section, "EmergencyStopOff", io.EmergencyStopOff);
                    ini.WriteBool(section, "Enabled", io.Enabled);
                    ini.WriteString(section, "Description", io.Description);
                    idx++;
                }

                // 保存心跳配置
                ini.WriteBool("Heartbeat", "Enabled", Config.Heartbeat.Enabled);
                ini.WriteInt("Heartbeat", "IntervalMs", Config.Heartbeat.IntervalMs);
                ini.WriteInt("Heartbeat", "TimeoutMs", Config.Heartbeat.TimeoutMs);
                ini.WriteBool("Heartbeat", "BasProtectionEnabled", Config.Heartbeat.BasProtectionEnabled);
                ini.WriteString("Heartbeat", "BasProgramPath", Config.Heartbeat.BasProgramPath);

                // 保存文件
                ini.SaveToFile();

                Log(string.Format(
                    "配置已保存到 {0}", configPath));

                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("配置保存失败 原因是 {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            Config = new MotionControllerConfig();
            Config.LoadDefaultAxes();
            Config.LoadDefaultIOs();
            Log("配置已重置为默认值");
        }
    }
}
