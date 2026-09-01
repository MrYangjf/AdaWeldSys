using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AdaWeldSystem.Comm.IntelligentLaserModbus
{
    /// <summary>
    /// Modbus TCP 主站通讯类
    /// 自实现 Modbus TCP 协议（MBAP + PDU），无需第三方库。
    /// 支持：FC03 读保持寄存器、FC06 写单个寄存器、FC16 写多个寄存器。
    /// 与 S7PLCCommunication 类似，独立实现，不依赖 NetworkPortBase。
    /// </summary>
    public class ModbusTCPCommunication : IDisposable
    {
        private const string Tag = "ModbusTCP";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
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
        public bool IsConnected
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
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ModbusTCPCommunication()
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
        public void Dispose()
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
            EventHandler<ErrorEventArgs> handler = ErrorOccurred;
            if (handler != null)
            {
                handler(this, new ErrorEventArgs(message, ex));
            }
        }

        #endregion
    }

    /// <summary>
    /// 错误事件参数
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public ErrorEventArgs(string message, Exception ex)
        {
            Message = message;
            Exception = ex;
        }
    }
}