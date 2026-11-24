using System;

namespace AdaWeldSystem.Comm.SystemComm
{
    /// <summary>设备侧等待快照，只读镜像</summary>
    public static class DeviceWaitSettings
    {
        private sealed class Snapshot
        {
            public double CameraAcquireSeconds;
            public double RecordFlushSeconds;
            public int LineLaserCallbackMs;
            public int ModbusConnectMs;
            public int ModbusReadWriteMs;
        }

        private static Snapshot _current = new Snapshot
        {
            CameraAcquireSeconds = 5.0,
            RecordFlushSeconds = 5.0,
            LineLaserCallbackMs = 200,
            ModbusConnectMs = 3000,
            ModbusReadWriteMs = 5000
        };

        /// <summary>相机采集等待信号超时（秒）</summary>
        public static double CameraAcquireSeconds { get { return _current.CameraAcquireSeconds; } }

        /// <summary>记录落盘排空等待上限（秒）</summary>
        public static double RecordFlushSeconds { get { return _current.RecordFlushSeconds; } }

        /// <summary>线激光回调排空等待上限（毫秒）</summary>
        public static int LineLaserCallbackMs { get { return _current.LineLaserCallbackMs; } }

        /// <summary>Modbus 建立连接超时（毫秒）</summary>
        public static int ModbusConnectMs { get { return _current.ModbusConnectMs; } }

        /// <summary>Modbus 单次读写超时（毫秒）</summary>
        public static int ModbusReadWriteMs { get { return _current.ModbusReadWriteMs; } }

        /// <summary>发布新快照（只由配置加载/保存调用）</summary>
        internal static void Publish(double cameraAcquireSeconds, double recordFlushSeconds,
            int lineLaserCallbackMs, int modbusConnectMs, int modbusReadWriteMs)
        {
            Snapshot next = new Snapshot
            {
                CameraAcquireSeconds = cameraAcquireSeconds,
                RecordFlushSeconds = recordFlushSeconds,
                LineLaserCallbackMs = lineLaserCallbackMs,
                ModbusConnectMs = modbusConnectMs,
                ModbusReadWriteMs = modbusReadWriteMs
            };
            _current = next;
        }
    }
}
