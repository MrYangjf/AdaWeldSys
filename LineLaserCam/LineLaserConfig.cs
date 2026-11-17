using System;
using System.Globalization;
using System.Xml.Linq;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.LineLaserCam.VirtualCam;

namespace AdaWeldSystem.LineLaserCam
{
    /// <summary>线激光配置</summary>
    /// <remarks>走 XdocumentReaderWriter 读写，禁止 XmlSerializer；文件 Config\LineLaser\LineLaser_XML\LineLaserConfig.xml，
    /// 全系统仅 LineLaserManager.Instance.Config 一处入口；非线程安全，运行期禁止在流程循环内读写。模拟焊缝固定平直搭接。</remarks>
    public class LineLaserConfig
    {
        #region 私有变量

        private const string Tag = "线激光配置";
        private const string RootName = "ROOT";
        private const string DevicePath = "ROOT/DEVICE";
        private const string SensorPath = "ROOT/SENSOR";
        private const string SimPath = "ROOT/SIMULATION";
        private const string DisplayPath = "ROOT/DISPLAY";
        private const string TimeoutPath = "ROOT/TIMEOUT";

        /// <summary>线激光配置 XML 子目录（相对程序基目录）</summary>
        private const string LineLaserXmlRelativeDir = @"Config\LineLaser\LineLaser_XML";

        private const string DefaultSensorIpValue = "192.168.178.210";

        #endregion

        #region 公共变量

        /// <summary>配置文件名（不含扩展名）</summary>
        public string ConfigFileName { get { return "LineLaserConfig"; } }

        /// <summary>默认选中的相机种类</summary>
        public LineLaserKind DefaultKind { get; set; }

        /// <summary>上电时是否自动连接相机</summary>
        public bool AutoConnectOnStart { get; set; }

        /// <summary>英莱相机 IP 地址</summary>
        public string SensorIp { get; set; }

        /// <summary>线激光数据频率（Hz）：30/60/80/120，模拟测试与真实相机共用</summary>
        public int LineLaserDataFrequencyHz { get; set; }

        /// <summary>显示处理是否开启，界面可按需开启，默认关闭</summary>
        public bool DisplayEnabled { get; set; }

        /// <summary>显示处理目标帧率（Hz），轮廓转 Mat 的节流节拍</summary>
        public double DisplayFps { get; set; }

        /// <summary>显示处理是否生成 Mat，关闭时只更新 ScottPlot 点缓冲</summary>
        public bool ConvertMatEnabled { get; set; }

        /// <summary>PreWork 等待轮廓数据稳定的超时阈值（秒）</summary>
        public int DataStableTimeoutSeconds { get; set; }

        /// <summary>数据稳定后等待主设备进入 Working 的超时阈值（秒）</summary>
        public int WaitWorkingTimeoutSeconds { get; set; }

        /// <summary>首个有效结果超时阈值，秒</summary>
        public int FirstResultTimeoutSeconds { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建配置并填入默认值</summary>
        public LineLaserConfig()
        {
            DefaultKind = LineLaserKind.IntelligentLaser;
            AutoConnectOnStart = false;
            SensorIp = DefaultSensorIpValue;
            LineLaserDataFrequencyHz = 30;

            DisplayEnabled = false;
            DisplayFps = 15.0;
            ConvertMatEnabled = true;

            DataStableTimeoutSeconds = 10;
            WaitWorkingTimeoutSeconds = 30;
            FirstResultTimeoutSeconds = 5;
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>读取整数配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static int ReadInt(XdocumentReaderWriter reader, string path, int def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(txt, out int val) ? val : def;
        }

        /// <summary>读取双精度配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static double ReadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : def;
        }

        /// <summary>读取布尔配置，文本 TRUE 视为真</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static bool ReadBool(XdocumentReaderWriter reader, string path, bool def)
        {
            string txt = reader.GetElementValueByPath(path, def ? "TRUE" : "FALSE");
            return txt.ToUpper() == "TRUE";
        }

        /// <summary>把超时阈值收敛到 1~3600 秒的合法区间，防止配置写 0 或</summary>
        /// <remarks>负数导致「立即超时」或永不超时。</remarks>
        /// <param name="seconds">配置读出的原始秒数</param>
        /// <returns>合法秒数</returns>
        private static int SanitizeSeconds(int seconds)
        {
            if (seconds < 1) return 1;
            if (seconds > 3600) return 3600;
            return seconds;
        }

        #endregion

        #region 公共函数

        /// <summary>把当前内存配置写成完整节点树</summary>
        /// <remarks>保存与补缺模板共用</remarks>
        /// <param name="reader">目标读写器（已 NewXdocument）</param>
        private void WriteConfigNodes(XdocumentReaderWriter reader)
        {
            System.Xml.Linq.XElement device = reader.AddElement("DEVICE");
            reader.AddElement(device, "DEFAULT_KIND", ((int)DefaultKind).ToString(CultureInfo.InvariantCulture));
            reader.AddElement(device, "AUTO_CONNECT_ON_START", AutoConnectOnStart ? "TRUE" : "FALSE");

            System.Xml.Linq.XElement sensor = reader.AddElement("SENSOR");
            reader.AddElement(sensor, "SENSOR_IP", SensorIp);

            System.Xml.Linq.XElement sim = reader.AddElement("SIMULATION");
            reader.AddElement(sim, "DATA_FREQUENCY_HZ", LineLaserDataFrequencyHz.ToString(CultureInfo.InvariantCulture));

            System.Xml.Linq.XElement display = reader.AddElement("DISPLAY");
            reader.AddElement(display, "ENABLED", DisplayEnabled ? "TRUE" : "FALSE");
            reader.AddElement(display, "FPS", DisplayFps.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(display, "CONVERT_MAT", ConvertMatEnabled ? "TRUE" : "FALSE");

            System.Xml.Linq.XElement timeout = reader.AddElement("TIMEOUT");
            reader.AddElement(timeout, "DATA_STABLE_SECONDS", DataStableTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(timeout, "WAIT_WORKING_SECONDS", WaitWorkingTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(timeout, "FIRST_RESULT_SECONDS", FirstResultTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>加载配置，读取失败时保留默认值</summary>
        public void LoadConfig()
        {
            LoadConfig(true);
        }

        /// <summary>加载配置（可控制缺键补写）</summary>
        /// <param name="allowHeal">true = 权威加载，缺键补写并可生成缺失模板；false = 预览读，零写盘</param>
        public void LoadConfig(bool allowHeal)
        {
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                LineLaserXmlRelativeDir, ConfigFileName + ".xml");
            bool fileMissing = !System.IO.File.Exists(fullPath);
            if (fileMissing && allowHeal)
            {
                SaveConfig();
                Log("配置缺失，已生成默认配置 " + ConfigFileName + ".xml", MessageLevel.Warning);
            }

            XdocumentReaderWriter reader = new XdocumentReaderWriter(LineLaserXmlRelativeDir, ConfigFileName, false);
            if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
            {
                Log("配置为空，保留默认参数", MessageLevel.Warning);
                return;
            }

            DefaultKind = (LineLaserKind)ReadInt(reader, DevicePath + "/DEFAULT_KIND", (int)DefaultKind);
            AutoConnectOnStart = ReadBool(reader, DevicePath + "/AUTO_CONNECT_ON_START", AutoConnectOnStart);
            SensorIp = reader.GetElementValueByPath(SensorPath + "/SENSOR_IP", SensorIp);

            LineLaserDataFrequencyHz = ReadInt(reader, SimPath + "/DATA_FREQUENCY_HZ", LineLaserDataFrequencyHz);

            DisplayEnabled = ReadBool(reader, DisplayPath + "/ENABLED", DisplayEnabled);
            DisplayFps = ReadDouble(reader, DisplayPath + "/FPS", DisplayFps);
            ConvertMatEnabled = ReadBool(reader, DisplayPath + "/CONVERT_MAT", ConvertMatEnabled);

            DataStableTimeoutSeconds = SanitizeSeconds(
                ReadInt(reader, TimeoutPath + "/DATA_STABLE_SECONDS", DataStableTimeoutSeconds));
            WaitWorkingTimeoutSeconds = SanitizeSeconds(
                ReadInt(reader, TimeoutPath + "/WAIT_WORKING_SECONDS", WaitWorkingTimeoutSeconds));
            FirstResultTimeoutSeconds = SanitizeSeconds(
                ReadInt(reader, TimeoutPath + "/FIRST_RESULT_SECONDS", FirstResultTimeoutSeconds));

            if (allowHeal)
            {
                // 模板按当前值生成，只补缺失节点
                XdocumentReaderWriter mirror = new XdocumentReaderWriter();
                mirror.NewXdocument(RootName, "1.0", "UTF-8");
                WriteConfigNodes(mirror);
                XDocument template = mirror.GetXDocument();
                ConfigSelfHeal.EnsureXml(fullPath, LineLaserXmlRelativeDir + "\\" + ConfigFileName + ".xml",
                    ConfigFileName + ".xml", template, true);
            }

            Log(string.Format("配置加载完成 默认相机 {0} 相机IP {1} 模拟数据频率 {2}Hz", DefaultKind, SensorIp, LineLaserDataFrequencyHz));
        }

        /// <summary>保存配置，末尾落盘</summary>
        public void SaveConfig()
        {
            XdocumentReaderWriter reader = new XdocumentReaderWriter(LineLaserXmlRelativeDir, ConfigFileName);
            reader.NewXdocument(RootName, "1.0", "UTF-8");

            WriteConfigNodes(reader);

            reader.SaveXdocument();
            Log(string.Format("配置已保存 默认相机 {0} 相机IP {1} 模拟数据频率 {2}Hz", DefaultKind, SensorIp, LineLaserDataFrequencyHz));
        }

        #endregion
    }
}
