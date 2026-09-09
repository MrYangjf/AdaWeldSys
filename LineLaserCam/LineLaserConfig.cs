using System;
using System.Globalization;
using AdaWeldSystem.Comm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.LineLaserCam.ILineLaser;

namespace AdaWeldSystem.LineLaserCam
{
    /// <summary>线激光配置</summary>
    /// <remarks>
    /// 承载默认相机种类、英莱相机 IP 与虚拟相机模拟参数，走 FileOperate/XMLfile.cs 读写，禁止 XmlSerializer。
    /// 配置文件为 Config\XML\LineLaserConfig.xml，根元素 ROOT。
    /// 由 LineLaserManager 持有，全系统只有 LineLaserManager.Instance.Config 一处入口。
    /// 非线程安全：只在初始化与停机阶段调用，运行期禁止在流程循环内读写配置。
    /// </remarks>
    public class LineLaserConfig
    {
        #region 私有变量

        private const string Tag = "线激光配置";
        private const string RootName = "ROOT";
        private const string DevicePath = "ROOT/DEVICE";
        private const string SensorPath = "ROOT/SENSOR";
        private const string SimPath = "ROOT/SIMULATION";
        private const string PvtPath = "ROOT/PVT";
        private const string DisplayPath = "ROOT/DISPLAY";

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

        /// <summary>虚拟相机模拟焊缝轮廓类型（0=V 型 1=U 型 2=平直 3=随机）</summary>
        public int SimSeamProfile { get; set; }

        /// <summary>虚拟相机是否启用随机模式</summary>
        public bool SimRandomMode { get; set; }

        /// <summary>虚拟相机模拟目标距离（mm）</summary>
        public double SimTargetDistance { get; set; }

        /// <summary>虚拟相机焊缝基准宽度（mm）</summary>
        public double SimBaseSeamWidth { get; set; }

        /// <summary>虚拟相机焊缝基准深度（mm）</summary>
        public double SimBaseSeamDepth { get; set; }

        /// <summary>焊缝跟踪 PVT 单批下发点数，攒够即下发</summary>
        public int PvtBatchSize { get; set; }

        /// <summary>焊缝跟踪轴名，运控初始化成功后按此名取轴并注入 PVT 能力</summary>
        public string SeamTrackAxisName { get; set; }

        /// <summary>实测帧率不可用时的默认帧间隔（秒），PVT 点时间轴基准</summary>
        public double PvtFrameSeconds { get; set; }

        /// <summary>PVT 剩余缓冲区续喂下限，低于此值暂缓续喂避免点表溢出</summary>
        public int PvtMinRemainBuffer { get; set; }

        /// <summary>显示处理是否开启，界面可按需开启，默认关闭</summary>
        public bool DisplayEnabled { get; set; }

        /// <summary>显示处理目标帧率（Hz），轮廓转 Mat 的节流节拍</summary>
        public double DisplayFps { get; set; }

        /// <summary>显示处理是否生成 Mat，关闭时只更新 ScottPlot 点缓冲</summary>
        public bool ConvertMatEnabled { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建配置并填入默认值</summary>
        public LineLaserConfig()
        {
            DefaultKind = LineLaserKind.IntelligentLaser;
            AutoConnectOnStart = false;
            SensorIp = DefaultSensorIpValue;
            SimSeamProfile = 0;
            SimRandomMode = false;
            SimTargetDistance = 200.0;
            SimBaseSeamWidth = 8.0;
            SimBaseSeamDepth = 3.0;

            PvtBatchSize = 16;
            SeamTrackAxisName = "Y轴";
            PvtFrameSeconds = 1.0 / 60.0;
            PvtMinRemainBuffer = 200;
            DisplayEnabled = false;
            DisplayFps = 15.0;
            ConvertMatEnabled = true;
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
            int val;
            return int.TryParse(txt, out val) ? val : def;
        }

        /// <summary>读取双精度配置</summary>
        /// <param name="reader">配置读写器</param>
        /// <param name="path">元素路径</param>
        /// <param name="def">读取失败时的默认值</param>
        /// <returns>元素值</returns>
        private static double ReadDouble(XdocumentReaderWriter reader, string path, double def)
        {
            string txt = reader.GetElementValueByPath(path, def.ToString(CultureInfo.InvariantCulture));
            double val;
            return double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out val) ? val : def;
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

        /// <summary>按不变文化写出数值文本</summary>
        /// <param name="value">待写出数值</param>
        /// <returns>文本</returns>
        private static string Text(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        #endregion

        #region 公共函数

        /// <summary>加载配置，读取失败时保留默认值</summary>
        public void LoadConfig()
        {
            XdocumentReaderWriter reader = new XdocumentReaderWriter(ConfigFileName);
            if (reader.GetXDocument() == null || reader.GetXDocument().Root == null)
            {
                Log("配置为空，保留默认参数", MessageLevel.Warning);
                return;
            }

            DefaultKind = (LineLaserKind)ReadInt(reader, DevicePath + "/DEFAULT_KIND", (int)DefaultKind);
            AutoConnectOnStart = ReadBool(reader, DevicePath + "/AUTO_CONNECT_ON_START", AutoConnectOnStart);
            SensorIp = reader.GetElementValueByPath(SensorPath + "/SENSOR_IP", SensorIp);

            SimSeamProfile = ReadInt(reader, SimPath + "/PROFILE", SimSeamProfile);
            SimRandomMode = ReadBool(reader, SimPath + "/RANDOM_MODE", SimRandomMode);
            SimTargetDistance = ReadDouble(reader, SimPath + "/TARGET_DISTANCE", SimTargetDistance);
            SimBaseSeamWidth = ReadDouble(reader, SimPath + "/BASE_SEAM_WIDTH", SimBaseSeamWidth);
            SimBaseSeamDepth = ReadDouble(reader, SimPath + "/BASE_SEAM_DEPTH", SimBaseSeamDepth);

            PvtBatchSize = ReadInt(reader, PvtPath + "/BATCH_SIZE", PvtBatchSize);
            SeamTrackAxisName = reader.GetElementValueByPath(PvtPath + "/SEAM_TRACK_AXIS", SeamTrackAxisName);
            PvtFrameSeconds = ReadDouble(reader, PvtPath + "/FRAME_SECONDS", PvtFrameSeconds);
            PvtMinRemainBuffer = ReadInt(reader, PvtPath + "/MIN_REMAIN_BUFFER", PvtMinRemainBuffer);

            DisplayEnabled = ReadBool(reader, DisplayPath + "/ENABLED", DisplayEnabled);
            DisplayFps = ReadDouble(reader, DisplayPath + "/FPS", DisplayFps);
            ConvertMatEnabled = ReadBool(reader, DisplayPath + "/CONVERT_MAT", ConvertMatEnabled);

            Log(string.Format("配置加载完成 默认相机 {0} 相机IP {1}", DefaultKind, SensorIp));
        }

        /// <summary>保存配置，末尾落盘</summary>
        public void SaveConfig()
        {
            XdocumentReaderWriter reader = new XdocumentReaderWriter(ConfigFileName);
            reader.NewXdocument(RootName, "1.0", "UTF-8");

            System.Xml.Linq.XElement device = reader.AddElement("DEVICE");
            reader.AddElement(device, "DEFAULT_KIND", ((int)DefaultKind).ToString(CultureInfo.InvariantCulture));
            reader.AddElement(device, "AUTO_CONNECT_ON_START", AutoConnectOnStart ? "TRUE" : "FALSE");

            System.Xml.Linq.XElement sensor = reader.AddElement("SENSOR");
            reader.AddElement(sensor, "SENSOR_IP", SensorIp);

            System.Xml.Linq.XElement sim = reader.AddElement("SIMULATION");
            reader.AddElement(sim, "PROFILE", SimSeamProfile.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(sim, "RANDOM_MODE", SimRandomMode ? "TRUE" : "FALSE");
            reader.AddElement(sim, "TARGET_DISTANCE", Text(SimTargetDistance));
            reader.AddElement(sim, "BASE_SEAM_WIDTH", Text(SimBaseSeamWidth));
            reader.AddElement(sim, "BASE_SEAM_DEPTH", Text(SimBaseSeamDepth));

            System.Xml.Linq.XElement pvt = reader.AddElement("PVT");
            reader.AddElement(pvt, "BATCH_SIZE", PvtBatchSize.ToString(CultureInfo.InvariantCulture));
            reader.AddElement(pvt, "SEAM_TRACK_AXIS", SeamTrackAxisName);
            reader.AddElement(pvt, "FRAME_SECONDS", Text(PvtFrameSeconds));
            reader.AddElement(pvt, "MIN_REMAIN_BUFFER", PvtMinRemainBuffer.ToString(CultureInfo.InvariantCulture));

            System.Xml.Linq.XElement display = reader.AddElement("DISPLAY");
            reader.AddElement(display, "ENABLED", DisplayEnabled ? "TRUE" : "FALSE");
            reader.AddElement(display, "FPS", Text(DisplayFps));
            reader.AddElement(display, "CONVERT_MAT", ConvertMatEnabled ? "TRUE" : "FALSE");

            reader.SaveXdocument();
            Log(string.Format("配置已保存 默认相机 {0} 相机IP {1}", DefaultKind, SensorIp));
        }

        #endregion
    }
}
