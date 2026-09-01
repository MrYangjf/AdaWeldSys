using System;
using System.IO;
using System.Xml.Linq;

namespace AdaWeldSystem.Comm.NetworkPort.Ethernet.Profinet
{
    /// <summary>
    /// Profinet GSDML 文件生成器
    /// 说明：PC 端通常无法直接作为 Profinet IO 控制器运行，需要专用硬件或 RTOS。
    /// 本生成器负责输出标准 GSDML 文件，供 TIA Portal 等工程工具导入后配置现场设备。
    /// </summary>
    public static class GSDMLGenerator
    {
        private const string Tag = "GSDMLGenerator";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 生成 GSDML 文件
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <param name="deviceId">设备 ID（十六进制字符串）</param>
        /// <param name="vendorId">厂商 ID（十六进制字符串）</param>
        /// <param name="outputPath">输出路径</param>
        public static void Generate(string deviceName, string deviceId, string vendorId, string outputPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XNamespace pn = "http://www.profibus.com/GSDML/2009/11/DeviceProfile";
                XDocument doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(pn + "ISO15745ProfileContainer",
                        new XElement(pn + "ISO15745Profile",
                            new XElement(pn + "ProfileHeader",
                                new XElement(pn + "ProfileIdentification", "GSDML Device Profile"),
                                new XElement(pn + "ProfileRevision", "1"),
                                new XElement(pn + "ProfileName", deviceName),
                                new XElement(pn + "ProfileSource", "AdaWeldSystem")
                            ),
                            new XElement(pn + "ProfileBody",
                                new XElement(pn + "DeviceIdentity",
                                    new XAttribute("VendorID", vendorId),
                                    new XAttribute("DeviceID", deviceId),
                                    new XElement(pn + "InfoText", string.Format("GSDML for {0}", deviceName)),
                                    new XElement(pn + "VendorName", "AdaWeld"),
                                    new XElement(pn + "OrderNumber", "AdaWeld-IO-01")
                                ),
                                new XElement(pn + "DeviceFunction",
                                    new XElement(pn + "Directions", new XElement(pn + "Input"), new XElement(pn + "Output"))
                                ),
                                new XElement(pn + "ApplicationProcess",
                                    new XElement(pn + "ModuleList",
                                        new XElement(pn + "Module",
                                            new XAttribute("ID", "M1"),
                                            new XElement(pn + "Name", "Default IO Module"),
                                            new XElement(pn + "InfoText", "Default 8-byte input / 8-byte output module"),
                                            new XElement(pn + "IOData",
                                                new XElement(pn + "Input",
                                                    new XAttribute("Length", "8")
                                                ),
                                                new XElement(pn + "Output",
                                                    new XAttribute("Length", "8")
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

                doc.Save(outputPath);
                Log(string.Format("GSDML 文件已生成 {0}", outputPath));
            }
            catch (Exception ex)
            {
                Log(string.Format("GSDML 生成失败 原因是 {0}", ex.Message), MessageLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// 生成默认 GSDML 文件到 Config 目录
        /// </summary>
        public static string GenerateDefault(string deviceName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "GSDML", string.Format("{0}.GSDML.xml", deviceName));
            Generate(deviceName, "0x0001", "0x002A", path);
            return path;
        }
    }
}