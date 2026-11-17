using S7.Net;
using System;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.Comm.Communication.Abstractions;

namespace AdaWeldSystem.Comm.Communication.Implementations
{
    /// <summary>西门子 S7 PLC 通讯实现</summary>
    /// <remarks>基于 S7NetPlus（S7.Net.dll），继承 CommChannelBase。</remarks>
    public class SiemensS7Comm : CommChannelBase
    {
        private Plc _plc;
        private readonly CpuType _cpuType;
        private readonly string _ipAddress;
        private readonly short _rack;
        private readonly short _slot;

        private bool _isConnected;

        /// <summary>通道名称</summary>
        public override string Name
        {
            get { return "SiemensS7"; }
        }

        /// <summary>是否已连接</summary>
        public override bool IsConnected
        {
            get { return _isConnected; }
        }

        /// <summary>PLC CPU 类型</summary>
        public CpuType CpuType
        {
            get { return _cpuType; }
        }

        /// <summary>IP 地址</summary>
        public string IpAddress
        {
            get { return _ipAddress; }
        }

        /// <summary>构造函数</summary>
        public SiemensS7Comm(CpuType cpuType, string ipAddress, short rack, short slot)
            : base("SiemensS7Comm")
        {
            _cpuType = cpuType;
            _ipAddress = ipAddress;
            _rack = rack;
            _slot = slot;
            _isConnected = false;
        }

        private void EnsureConnected()
        {
            if (!IsConnected || _plc == null)
            {
                throw new InvalidOperationException("S7 PLC 未连接");
            }
        }

        /// <summary>连接到 S7 PLC</summary>
        public override void Connect()
        {
            Disconnect();
            try
            {
                _plc = new Plc(_cpuType, _ipAddress, _rack, _slot);
                _plc.Open();
                _isConnected = true;
                Log(string.Format("S7 PLC 已连接 {0}", _ipAddress));
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log(string.Format("S7 PLC 连接失败 原因是 {0}", ex.Message), MessageLevel.Error);
                throw;
            }
        }

        /// <summary>断开 S7 PLC 连接</summary>
        public override void Disconnect()
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
                    _isConnected = false;
                }
            }
        }

        /// <summary>读取字节数组</summary>
        /// <param name="dataType">PLC 存储区类型</param>
        /// <param name="db">数据块号（非 DB 区传 0）</param>
        /// <param name="startByteAdr">起始字节地址</param>
        /// <param name="count">读取字节数</param>
        /// <returns>读取到的字节数组</returns>
        public byte[] ReadBytes(DataType dataType, int db, int startByteAdr, int count)
        {
            EnsureConnected();
            return _plc.ReadBytes(dataType, db, startByteAdr, count);
        }

        /// <summary>写入字节数组</summary>
        /// <param name="dataType">PLC 存储区类型</param>
        /// <param name="db">数据块号（非 DB 区传 0）</param>
        /// <param name="startByteAdr">起始字节地址</param>
        /// <param name="value">待写入字节</param>
        public void WriteBytes(DataType dataType, int db, int startByteAdr, byte[] value)
        {
            EnsureConnected();
            _plc.WriteBytes(dataType, db, startByteAdr, value);
        }

        /// <summary>读取单个变量（使用 Read/Write 泛型方法）</summary>
        public T Read<T>(string variable)
        {
            EnsureConnected();
            return (T)_plc.Read(variable);
        }

        /// <summary>写入单个变量</summary>
        public void Write<T>(string variable, T value)
        {
            EnsureConnected();
            _plc.Write(variable, value);
        }

        /// <summary>读取 PLC 位（Bool）</summary>
        /// <param name="dataType">PLC 存储区类型</param>
        /// <param name="db">数据块号（非 DB 区传 0）</param>
        /// <param name="startByteAdr">起始字节地址</param>
        /// <param name="bitIndex">位序号（0 起）</param>
        /// <returns>该位为 true 时返回 true</returns>
        public bool ReadBit(DataType dataType, int db, int startByteAdr, int bitIndex)
        {
            EnsureConnected();
            byte[] buffer = _plc.ReadBytes(dataType, db, startByteAdr, 1);
            return (buffer[0] & (1 << bitIndex)) != 0;
        }

        /// <summary>写入 PLC 位（Bool）</summary>
        /// <param name="dataType">PLC 存储区类型</param>
        /// <param name="db">数据块号（非 DB 区传 0）</param>
        /// <param name="startByteAdr">起始字节地址</param>
        /// <param name="bitIndex">位序号（0 起）</param>
        /// <param name="value">待写入的位值</param>
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

        /// <summary>读取 PLC 字符串</summary>
        /// <param name="dataType">PLC 存储区类型</param>
        /// <param name="db">数据块号（非 DB 区传 0）</param>
        /// <param name="startByteAdr">起始字节地址</param>
        /// <param name="length">字符串最大长度</param>
        /// <returns>读取到的字符串（按首个空字符截断）</returns>
        public string ReadString(DataType dataType, int db, int startByteAdr, int length)
        {
            EnsureConnected();
            byte[] buffer = _plc.ReadBytes(dataType, db, startByteAdr, length);
            string result = System.Text.Encoding.ASCII.GetString(buffer);
            int nullTerminator = result.IndexOf('\0');
            return nullTerminator >= 0 ? result.Substring(0, nullTerminator) : result.TrimEnd();
        }

        /// <summary>释放资源</summary>
        public override void Dispose()
        {
            Disconnect();
        }
    }
}