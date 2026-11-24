namespace AdaWeldSystem.MonitorCam.IMonitorCam
{
    /// <summary>扫描（枚举）发现的监控相机设备信息</summary>
    public class MonitorCamDeviceInfo
    {
        /// <summary>相机 IP（GigE 相机有效；USB3 相机为空字符串）</summary>
        public string Ip { get; set; }

        /// <summary>产品名称</summary>
        public string ProductName { get; set; }

        /// <summary>产品唯一序列号</summary>
        public string SerialNumber { get; set; }

        /// <summary>接口类型（GigE / USB3 等）</summary>
        public string PortType { get; set; }

        /// <summary>传感器类型</summary>
        public string SensorType { get; set; }

        /// <summary>创建空的设备信息</summary>
        public MonitorCamDeviceInfo()
        {
            Ip = string.Empty;
            ProductName = string.Empty;
            SerialNumber = string.Empty;
            PortType = string.Empty;
            SensorType = string.Empty;
        }
    }
}
