using S7.Net;
using System;

namespace AdaWeldSystem.Comm.PLC.Siemens
{
    /// <summary>
    /// 西门子 S7 PLC 通讯封装
    /// 基于 S7NetPlus 库（需添加 S7.Net.dll 引用）
    /// </summary>
    public class S7PLCCommunication : IDisposable
    {
        private const string Tag = "S7PLCCommunication";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private Plc _plc;
        private readonly CpuType _cpuType;
        private readonly string _ipAddress;
        private readonly short _rack;
        private readonly short _slot;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// PLC CPU 类型
        /// </summary>
        public CpuType CpuType
        {
            get { return _cpuType; }
        }

        /// <summary>
        /// IP 地址
        /// </summary>
        public string IpAddress
        {
            get { return _ipAddress; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public S7PLCCommunication(CpuType cpuType, string ipAddress, short rack, short slot)
        {
            _cpuType = cpuType;
            _ipAddress = ipAddress;
            _rack = rack;
            _slot = slot;
            IsConnected = false;
        }

        /// <summary>
        /// 连接到 S7 PLC
        /// </summary>
        public void Connect()
        {
            Disconnect();
            try
            {
                _plc = new Plc(_cpuType, _ipAddress, _rack, _slot);
                _plc.Open();
                IsConnected = true;
                Log(string.Format("S7 PLC 已连接 {0}", _ipAddress));
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Log(string.Format("S7 PLC 连接失败 原因是 {0}", ex.Message), MessageLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// 断开 S7 PLC 连接
        /// </summary>
        public void Disconnect()
        {
            if (_plc != null)
            {
                try
                {
                    _plc.Close();
                }
                catch (Exception ex)
                {
                    Log(string.Format("S7 PLC 断开异常 原因 {0}", ex.Message), MessageLevel.Error);
                }
                finally
                {
                    _plc = null;
                    IsConnected = false;
                }
            }
        }

        /// <summary>
        /// 读取字节数组
        /// </summary>
        public byte[] ReadBytes(DataType dataType, int db, int startByteAdr, int count)
        {
            EnsureConnected();
            return _plc.ReadBytes(dataType, db, startByteAdr, count);
        }

        /// <summary>
        /// 写入字节数组
        /// </summary>
        public void WriteBytes(DataType dataType, int db, int startByteAdr, byte[] value)
        {
            EnsureConnected();
            _plc.WriteBytes(dataType, db, startByteAdr, value);
        }

        /// <summary>
        /// 读取单个变量（使用 Read/Write 泛型方法）
        /// </summary>
        public T Read<T>(string variable)
        {
            EnsureConnected();
            return (T)_plc.Read(variable);
        }

        /// <summary>
        /// 写入单个变量
        /// </summary>
        public void Write<T>(string variable, T value)
        {
            EnsureConnected();
            _plc.Write(variable, value);
        }

        /// <summary>
        /// 读取 PLC 位（Bool）
        /// </summary>
        public bool ReadBit(DataType dataType, int db, int startByteAdr, int bitIndex)
        {
            EnsureConnected();
            byte[] buffer = _plc.ReadBytes(dataType, db, startByteAdr, 1);
            return (buffer[0] & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// 写入 PLC 位（Bool）
        /// </summary>
        public void WriteBit(DataType dataType, int db, int startByteAdr, int bitIndex, bool value)
        {
            EnsureConnected();
            byte[] buffer = _plc.ReadBytes(dataType, db, startByteAdr, 1);
            if (value)
                buffer[0] |= (byte)(1 << bitIndex);
            else
                buffer[0] &= (byte)~(1 << bitIndex);
            _plc.WriteBytes(dataType, db, startByteAdr, buffer);
        }

        /// <summary>
        /// 读取 PLC 字符串
        /// </summary>
        public string ReadString(DataType dataType, int db, int startByteAdr, int length)
        {
            EnsureConnected();
            byte[] buffer = _plc.ReadBytes(dataType, db, startByteAdr, length);
            string result = System.Text.Encoding.ASCII.GetString(buffer);
            int nullTerminator = result.IndexOf('\0');
            return nullTerminator >= 0 ? result.Substring(0, nullTerminator) : result.TrimEnd();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }

        private void EnsureConnected()
        {
            if (!IsConnected || _plc == null)
            {
                throw new InvalidOperationException("S7 PLC 未连接");
            }
        }
    }
}