using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.Comm.Communication.Implementations
{
    /// <summary>
    /// Modbus TCP 主站通讯类
    /// 自实现 Modbus TCP 协议（MBAP + PDU），无需第三方库。
    /// 支持：FC03 读保持寄存器、FC06 写单个寄存器、FC16 写多个寄存器。
    /// 与 SiemensS7Comm 类似，独立实现，不依赖 NetworkPortBase。
    /// </summary>
    public class InLaserModbusComm : CommChannelBase
    {
        private const string Tag = "InLaserModbusComm";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 通道名称
        /// </summary>
        public override string Name
        {
            get { return "InLaser_Modbus"; }
        }

        /// <summary>
        /// 建立连接（打开 Modbus TCP 连接）
        /// </summary>
        public override void Connect()
        {
            Open();
        }

        /// <summary>
        /// 断开连接（关闭 Modbus TCP 连接）
        /// </summary>
        public override void Disconnect()
        {
            Close();
        }

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private readonly object _lock = new object();
        private ushort _transactionId = 0;

        /// <summary>远程 IP 地址</summary>
        public string RemoteIP { get; set; }

        /// <summary>远程端口号（默认 502）</summary>
        public int RemotePort { get; set; }

        /// <summary>从站 ID（默认 1）</summary>
        public byte SlaveId { get; set; }

        /// <summary>连接超时（毫秒，默认 3000）</summary>
        public int ConnectTimeoutMs { get; set; }

        /// <summary>读写超时（毫秒，默认 5000）</summary>
        public int ReadWriteTimeoutMs { get; set; }

        /// <summary>是否已连接</summary>
        public override bool IsConnected
        {
            get
            {
                try
                {
                    return _tcpClient != null && _tcpClient.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>连接成功事件</summary>
        public event EventHandler Connected;

        /// <summary>断开事件</summary>
        public event EventHandler Disconnected;

        /// <summary>错误事件</summary>
        public event EventHandler<ModbusErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 构造函数
        /// </summary>
        public InLaserModbusComm()
        {
            RemoteIP = "192.168.1.100";
            RemotePort = 502;
            SlaveId = 1;
            ConnectTimeoutMs = 3000;
            ReadWriteTimeoutMs = 5000;
        }

        /// <summary>
        /// 打开连接
        /// </summary>
        public bool Open()
        {
            try
            {
                Close();

                _tcpClient = new TcpClient();
                IAsyncResult ar = _tcpClient.BeginConnect(RemoteIP, RemotePort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs, false))
                {
                    _tcpClient.Close();
                    Log(string.Format("连接超时 目标地址 {0} 端口 {1}", RemoteIP, RemotePort), MessageLevel.Error);
                    return false;
                }
                _tcpClient.EndConnect(ar);

                _tcpClient.ReceiveTimeout = ReadWriteTimeoutMs;
                _tcpClient.SendTimeout = ReadWriteTimeoutMs;
                _stream = _tcpClient.GetStream();

                Log(string.Format("连接成功 目标地址 {0} 端口 {1} 从站 {2}", RemoteIP, RemotePort, SlaveId));
                OnConnected();
                return true;
            }
            catch (Exception ex)
            {
                Log("连接失败 " + ex.Message, MessageLevel.Error);
                OnErrorOccurred("连接失败 " + ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            try
            {
                if (_stream != null)
                {
                    _stream.Close();
                    _stream = null;
                }
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
                OnDisconnected();
            }
            catch (Exception ex)
            {
                Log("关闭异常 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public override void Dispose()
        {
            Close();
        }

        #region Modbus 功能码

        /// <summary>
        /// 读保持寄存器（FC03）
        /// </summary>
        /// <param name="startAddress">起始地址（0-based）</param>
        /// <param name="count">寄存器数量</param>
        /// <returns>读取到的寄存器值数组，失败返回 null</returns>
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
        {
            if (count < 1 || count > 125)
            {
                Log("ReadHoldingRegisters count 必须介于 1~125 之间", MessageLevel.Error);
                return null;
            }

            byte[] pdu = new byte[5];
            pdu[0] = 0x03;  // Function Code
            pdu[1] = (byte)(startAddress >> 8);
            pdu[2] = (byte)(startAddress & 0xFF);
            pdu[3] = (byte)(count >> 8);
            pdu[4] = (byte)(count & 0xFF);

            byte[] response = Execute(pdu);
            if (response == null)
            {
                return null;
            }

            // 检查响应
            if (response.Length < 3 || response[0] != 0x03)
            {
                HandleErrorResponse(response);
                return null;
            }

            byte byteCount = response[1];
            ushort[] result = new ushort[byteCount / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (ushort)((response[2 + i * 2] << 8) | response[3 + i * 2]);
            }
            return result;
        }

        /// <summary>
        /// 写单个寄存器（FC06）
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">值</param>
        /// <returns>成功返回 true</returns>
        public bool WriteSingleRegister(ushort address, ushort value)
        {
            byte[] pdu = new byte[5];
            pdu[0] = 0x06;  // Function Code
            pdu[1] = (byte)(address >> 8);
            pdu[2] = (byte)(address & 0xFF);
            pdu[3] = (byte)(value >> 8);
            pdu[4] = (byte)(value & 0xFF);

            byte[] response = Execute(pdu);
            if (response == null)
            {
                return false;
            }

            if (response.Length < 5 || response[0] != 0x06)
            {
                HandleErrorResponse(response);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 写多个寄存器（FC16）
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="values">值数组</param>
        /// <returns>成功返回 true</returns>
        public bool WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            if (values == null || values.Length < 1 || values.Length > 123)
            {
                Log("WriteMultipleRegisters values 长度必须介于 1~123 之间", MessageLevel.Error);
                return false;
            }

            int pduLen = 6 + values.Length * 2;
            byte[] pdu = new byte[pduLen];
            pdu[0] = 0x10;  // Function Code
            pdu[1] = (byte)(startAddress >> 8);
            pdu[2] = (byte)(startAddress & 0xFF);
            pdu[3] = (byte)(values.Length >> 8);
            pdu[4] = (byte)(values.Length & 0xFF);
            pdu[5] = (byte)(values.Length * 2);  // Byte count

            for (int i = 0; i < values.Length; i++)
            {
                pdu[6 + i * 2] = (byte)(values[i] >> 8);
                pdu[7 + i * 2] = (byte)(values[i] & 0xFF);
            }

            byte[] response = Execute(pdu);
            if (response == null)
            {
                return false;
            }

            if (response.Length < 5 || response[0] != 0x10)
            {
                HandleErrorResponse(response);
                return false;
            }

            return true;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行 Modbus 请求（发送 PDU + 接收响应）
        /// 线程安全：同一时间只允许一个请求
        /// </summary>
        private byte[] Execute(byte[] pdu)
        {
            lock (_lock)
            {
                if (!IsConnected)
                {
                    Log("未连接，无法发送请求", MessageLevel.Error);
                    return null;
                }

                try
                {
                    // 构建 MBAP 头
                    _transactionId++;
                    byte[] mbap = new byte[7];
                    mbap[0] = (byte)(_transactionId >> 8);
                    mbap[1] = (byte)(_transactionId & 0xFF);
                    mbap[2] = 0x00;  // Protocol ID (Modbus)
                    mbap[3] = 0x00;
                    mbap[4] = (byte)((pdu.Length + 1) >> 8);  // Length = PDU + Unit ID
                    mbap[5] = (byte)((pdu.Length + 1) & 0xFF);
                    mbap[6] = SlaveId;  // Unit ID

                    // 发送
                    byte[] request = new byte[mbap.Length + pdu.Length];
                    Buffer.BlockCopy(mbap, 0, request, 0, mbap.Length);
                    Buffer.BlockCopy(pdu, 0, request, mbap.Length, pdu.Length);

                    _stream.Write(request, 0, request.Length);

                    // 接收响应头（7 字节 MBAP）
                    byte[] header = new byte[7];
                    int read = ReadExact(_stream, header, 0, 7);
                    if (read < 7)
                    {
                        Log("响应头不完整", MessageLevel.Error);
                        return null;
                    }

                    // 解析响应长度
                    int respLen = (header[4] << 8) | header[5];
                    if (respLen < 1)
                    {
                        Log("响应长度无效", MessageLevel.Error);
                        return null;
                    }

                    // 接收 PDU（respLen - 1 是 PDU 长度，减去 Unit ID 占位）
                    byte[] pduResp = new byte[respLen - 1];
                    read = ReadExact(_stream, pduResp, 0, respLen - 1);
                    if (read < respLen - 1)
                    {
                        Log("响应数据不完整", MessageLevel.Error);
                        return null;
                    }

                    return pduResp;
                }
                catch (Exception ex)
                {
                    Log("通讯异常 " + ex.Message, MessageLevel.Error);
                    OnErrorOccurred("通讯异常 " + ex.Message, ex);
                    Close();
                    return null;
                }
            }
        }

        /// <summary>
        /// 从流中精确读取指定字节数
        /// </summary>
        private static int ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }
            return totalRead;
        }

        /// <summary>
        /// 处理异常响应
        /// </summary>
        private void HandleErrorResponse(byte[] response)
        {
            if (response == null || response.Length < 2)
            {
                return;
            }

            if ((response[0] & 0x80) != 0)
            {
                byte exceptionCode = response[1];
                string msg = string.Format("Modbus 异常响应 功能码 0x{0:X2} 异常码 {1}", response[0] & 0x7F, exceptionCode);
                Log(msg, MessageLevel.Error);
            }
        }

        private void OnConnected()
        {
            EventHandler handler = Connected;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnDisconnected()
        {
            EventHandler handler = Disconnected;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnErrorOccurred(string message, Exception ex)
        {
            EventHandler<ModbusErrorEventArgs> handler = ErrorOccurred;
            if (handler != null)
            {
                handler(this, new ModbusErrorEventArgs(message, ex));
            }
        }

        #endregion
    }

    /// <summary>
    /// Modbus 错误事件参数
    /// </summary>
    public class ModbusErrorEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public ModbusErrorEventArgs(string message, Exception ex)
        {
            Message = message;
            Exception = ex;
        }
    }

    /// <summary>
    /// 英莱(IntelligentLaser) 相机 Modbus 寄存器映射与数据转换
    /// 基于英莱 Modbus TCP 通讯协议文档（2026-04-09）
    /// 寄存器地址范围：0x00 ~ 0x28
    /// 单位：0.01mm（部分寄存器）
    /// </summary>
    public static class ILModbusData
    {
        private const string Tag = "ILModbusData";

        #region 寄存器地址定义

        /// <summary>
        /// 控制寄存器地址
        /// </summary>
        public static class ControlRegisters
        {
            /// <summary>控制字（0x00）：1=激光开，2=激光关，3=启动检测，4=停止检测</summary>
            public const ushort ControlWord = 0x00;

            /// <summary>状态字（0x01）：1=就绪，2=检测中，3=错误</summary>
            public const ushort StatusWord = 0x01;

            /// <summary>触发模式（0x02）：0=内触发，1=外触发</summary>
            public const ushort TriggerMode = 0x02;

            /// <summary>触发源（0x03）：0=软触发，1=IO触发，2=编码器触发</summary>
            public const ushort TriggerSource = 0x03;
        }

        /// <summary>
        /// 检测结果寄存器地址
        /// </summary>
        public static class ResultRegisters
        {
            /// <summary>检测结果（0x08）：0=未检测，1=OK，2=NG</summary>
            public const ushort InspectResult = 0x08;

            /// <summary>焊缝宽度（0x09），单位：0.01mm</summary>
            public const ushort SeamWidth = 0x09;

            /// <summary>焊缝高度（0x0A），单位：0.01mm</summary>
            public const ushort SeamHeight = 0x0A;

            /// <summary>焊缝偏移X（0x0B），单位：0.01mm</summary>
            public const ushort SeamOffsetX = 0x0B;

            /// <summary>焊缝偏移Y（0x0C），单位：0.01mm</summary>
            public const ushort SeamOffsetY = 0x0C;

            /// <summary>焊缝面积（0x0D），单位：0.01mm²</summary>
            public const ushort SeamArea = 0x0D;

            /// <summary>焊缝角度（0x0E），单位：0.01°</summary>
            public const ushort SeamAngle = 0x0E;

            /// <summary>轮廓点数（0x0F）</summary>
            public const ushort ContourPointCount = 0x0F;

            /// <summary>检测帧率（0x10），单位：Hz</summary>
            public const ushort FrameRate = 0x10;

            /// <summary>激光功率（0x11），单位：0.1%</summary>
            public const ushort LaserPower = 0x11;

            /// <summary>曝光时间（0x12），单位：μs</summary>
            public const ushort ExposureTime = 0x12;

            /// <summary>设备温度（0x13），单位：0.1°C</summary>
            public const ushort DeviceTemp = 0x13;

            /// <summary>运行时长（0x14），单位：秒</summary>
            public const ushort RunTime = 0x14;

            /// <summary>总检测数（0x15）</summary>
            public const ushort TotalCount = 0x15;

            /// <summary>OK 数（0x16）</summary>
            public const ushort OkCount = 0x16;

            /// <summary>NG 数（0x17）</summary>
            public const ushort NgCount = 0x17;

            /// <summary>预留地址 0x18~0x27</summary>
            public const ushort ReservedStart = 0x18;
            public const ushort ReservedEnd = 0x27;

            /// <summary>固件版本（0x28），高字节=主版本，低字节=次版本</summary>
            public const ushort FirmwareVersion = 0x28;
        }

        #endregion

        #region 数据转换

        /// <summary>
        /// 将寄存器值转换为毫米（0.01mm → mm）
        /// </summary>
        public static float ToMillimeter(ushort registerValue)
        {
            return registerValue * 0.01f;
        }

        /// <summary>
        /// 将毫米值转换为寄存器值（mm → 0.01mm）
        /// </summary>
        public static ushort FromMillimeter(float mmValue)
        {
            return (ushort)Math.Round(mmValue * 100.0f);
        }

        /// <summary>
        /// 将寄存器值转换为角度（0.01° → °）
        /// </summary>
        public static float ToDegree(ushort registerValue)
        {
            return registerValue * 0.01f;
        }

        /// <summary>
        /// 将角度值转换为寄存器值（° → 0.01°）
        /// </summary>
        public static ushort FromDegree(float degreeValue)
        {
            return (ushort)Math.Round(degreeValue * 100.0f);
        }

        /// <summary>
        /// 将寄存器值转换为百分比（0.1% → %）
        /// </summary>
        public static float ToPercent(ushort registerValue)
        {
            return registerValue * 0.1f;
        }

        /// <summary>
        /// 将寄存器值转换为温度（0.1°C → °C）
        /// </summary>
        public static float ToTemperature(ushort registerValue)
        {
            return registerValue * 0.1f;
        }

        /// <summary>
        /// 将两个寄存器值解析为固件版本字符串
        /// </summary>
        public static string ToFirmwareVersion(ushort regValue)
        {
            byte major = (byte)(regValue >> 8);
            byte minor = (byte)(regValue & 0xFF);
            return string.Format("{0}.{1:D2}", major, minor);
        }

        #endregion

        #region 控制命令

        /// <summary>
        /// 控制字命令值
        /// </summary>
        public static class ControlCommands
        {
            /// <summary>激光开启</summary>
            public const ushort LaserOn = 1;

            /// <summary>激光关闭</summary>
            public const ushort LaserOff = 2;

            /// <summary>启动检测</summary>
            public const ushort StartInspect = 3;

            /// <summary>停止检测</summary>
            public const ushort StopInspect = 4;
        }

        /// <summary>
        /// 状态字值
        /// </summary>
        public static class StatusValues
        {
            /// <summary>就绪</summary>
            public const ushort Ready = 1;

            /// <summary>检测中</summary>
            public const ushort Inspecting = 2;

            /// <summary>错误</summary>
            public const ushort Error = 3;
        }

        /// <summary>
        /// 检测结果值
        /// </summary>
        public static class InspectResultValues
        {
            /// <summary>未检测</summary>
            public const ushort None = 0;

            /// <summary>OK</summary>
            public const ushort OK = 1;

            /// <summary>NG</summary>
            public const ushort NG = 2;
        }

        #endregion

        #region 批量读取方法

        /// <summary>
        /// 获取所有需要读取的状态寄存器地址列表
        /// </summary>
        public static ushort[] GetStatusRegisterAddresses()
        {
            return new ushort[]
            {
                ResultRegisters.InspectResult,      // 0x08
                ResultRegisters.SeamWidth,          // 0x09
                ResultRegisters.SeamHeight,         // 0x0A
                ResultRegisters.SeamOffsetX,        // 0x0B
                ResultRegisters.SeamOffsetY,        // 0x0C
                ResultRegisters.SeamArea,           // 0x0D
                ResultRegisters.SeamAngle,          // 0x0E
                ResultRegisters.ContourPointCount,  // 0x0F
                ResultRegisters.FrameRate,          // 0x10
                ResultRegisters.LaserPower,         // 0x11
                ResultRegisters.ExposureTime,       // 0x12
                ResultRegisters.DeviceTemp,         // 0x13
                ResultRegisters.RunTime,            // 0x14
                ResultRegisters.TotalCount,         // 0x15
                ResultRegisters.OkCount,            // 0x16
                ResultRegisters.NgCount,            // 0x17
                ResultRegisters.FirmwareVersion,    // 0x28
            };
        }

        /// <summary>
        /// 批量读取状态时的起始地址（0x08）
        /// </summary>
        public const ushort StatusBatchStartAddress = 0x08;

        /// <summary>
        /// 批量读取状态时的寄存器数量（0x08~0x17 共 16 个 + 0x28 单独读取）
        /// </summary>
        public const ushort StatusBatchCount = 16;

        #endregion

        #region 检测结果模型

        /// <summary>
        /// 英莱 Modbus 检测结果快照
        /// </summary>
        public class ILModbusInspectResult
        {
            /// <summary>检测结果（0=None, 1=OK, 2=NG）</summary>
            public ushort InspectResult { get; set; }

            /// <summary>焊缝宽度（mm）</summary>
            public float SeamWidthMm { get; set; }

            /// <summary>焊缝高度（mm）</summary>
            public float SeamHeightMm { get; set; }

            /// <summary>焊缝偏移X（mm）</summary>
            public float SeamOffsetXmm { get; set; }

            /// <summary>焊缝偏移Y（mm）</summary>
            public float SeamOffsetYmm { get; set; }

            /// <summary>焊缝面积（mm²）</summary>
            public float SeamAreaMm2 { get; set; }

            /// <summary>焊缝角度（°）</summary>
            public float SeamAngleDeg { get; set; }

            /// <summary>轮廓点数</summary>
            public ushort ContourPointCount { get; set; }

            /// <summary>检测帧率（Hz）</summary>
            public ushort FrameRate { get; set; }

            /// <summary>激光功率（%）</summary>
            public float LaserPowerPercent { get; set; }

            /// <summary>曝光时间（μs）</summary>
            public ushort ExposureTime { get; set; }

            /// <summary>设备温度（°C）</summary>
            public float DeviceTempC { get; set; }

            /// <summary>运行时长（秒）</summary>
            public uint RunTimeSeconds { get; set; }

            /// <summary>总检测数</summary>
            public uint TotalCount { get; set; }

            /// <summary>OK 数</summary>
            public uint OkCount { get; set; }

            /// <summary>NG 数</summary>
            public uint NgCount { get; set; }

            /// <summary>固件版本字符串</summary>
            public string FirmwareVersion { get; set; }

            /// <summary>
            /// 从 Modbus 寄存器数组解析检测结果
            /// </summary>
            /// <param name="registers">从 0x08 开始的连续寄存器值数组</param>
            /// <param name="firmwareReg">0x28 固件版本寄存器值</param>
            /// <returns>解析后的检测结果</returns>
            public static ILModbusInspectResult FromRegisters(ushort[] registers, ushort firmwareReg)
            {
                if (registers == null || registers.Length < 16)
                {
                    return null;
                }

                ILModbusInspectResult result = new ILModbusInspectResult();

                // 0x08: 检测结果
                result.InspectResult = registers[0];
                // 0x09: 焊缝宽度
                result.SeamWidthMm = ToMillimeter(registers[1]);
                // 0x0A: 焊缝高度
                result.SeamHeightMm = ToMillimeter(registers[2]);
                // 0x0B: 焊缝偏移X
                result.SeamOffsetXmm = ToMillimeter(registers[3]);
                // 0x0C: 焊缝偏移Y
                result.SeamOffsetYmm = ToMillimeter(registers[4]);
                // 0x0D: 焊缝面积
                result.SeamAreaMm2 = ToMillimeter(registers[5]);
                // 0x0E: 焊缝角度
                result.SeamAngleDeg = ToDegree(registers[6]);
                // 0x0F: 轮廓点数
                result.ContourPointCount = registers[7];
                // 0x10: 帧率
                result.FrameRate = registers[8];
                // 0x11: 激光功率
                result.LaserPowerPercent = ToPercent(registers[9]);
                // 0x12: 曝光时间
                result.ExposureTime = registers[10];
                // 0x13: 设备温度
                result.DeviceTempC = ToTemperature(registers[11]);
                // 0x14: 运行时长
                result.RunTimeSeconds = registers[12];
                // 0x15: 总检测数
                result.TotalCount = registers[13];
                // 0x16: OK 数
                result.OkCount = registers[14];
                // 0x17: NG 数
                result.NgCount = registers[15];
                // 0x28: 固件版本
                result.FirmwareVersion = ToFirmwareVersion(firmwareReg);

                return result;
            }

            public override string ToString()
            {
                return string.Format("InspectResult={0}, Width={1:F2}mm, Height={2:F2}mm, OffsetX={3:F2}mm, OffsetY={4:F2}mm, Area={5:F2}mm², Angle={6:F1}°, Fps={7}, Power={8:F1}%, Temp={9:F1}°C, OK={10}, NG={11}, FW={12}",
                    InspectResult, SeamWidthMm, SeamHeightMm, SeamOffsetXmm, SeamOffsetYmm, SeamAreaMm2, SeamAngleDeg,
                    FrameRate, LaserPowerPercent, DeviceTempC, OkCount, NgCount, FirmwareVersion);
            }
        }

        #endregion
    }
}
