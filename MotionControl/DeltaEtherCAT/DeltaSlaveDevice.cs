using System;
using System.Threading;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>台达 EtherCAT 从站设备通道（SDO / PDO 封装）</summary>
    /// <remarks>封装对象字典读写并做定标换算（整型 ↔ 工程值）；
    /// 只做通道与换算，不含业务语义。</remarks>
    public class DeltaSlaveDevice
    {
        #region 私有变量

        private const string Tag = "DeltaSlaveDevice";
        // 等待节奏取自 ROOT/FLOW_TIMEOUT（运行期读取，改配置不需重启）；缺省值 = 改造前行为
        private static int SdoPollIntervalMs { get { return FlowTimeoutSettings.Current.SdoPollMs; } }
        private static int SdoPollMaxCount { get { return FlowTimeoutSettings.Current.SdoPollMaxCount; } }

        /// <summary>发起 SDO 前先等待：Done 完成后保持 0，立刻轮询读陈旧值</summary>
        private static int SdoPreDelayMs { get { return FlowTimeoutSettings.Current.SdoPreDelayMs; } }

        /// <summary>邮箱忙（0x1304 / 0x1305）后的退避重试等待（毫秒）</summary>
        private static int SdoMailboxRetryDelayMs { get { return FlowTimeoutSettings.Current.SdoMailboxRetryMs; } }

        /// <summary>SDO 尝试次数（仅对邮箱忙等未发出返回码重试）</summary>
        private const int SdoAttemptCount = 3;

        private readonly ushort _cardNo;
        private readonly ushort _nodeId;
        private readonly ushort _slotNo;

        #endregion

        #region 公共变量

        /// <summary>主站卡号</summary>
        public ushort CardNo { get { return _cardNo; } }

        /// <summary>从站号</summary>
        public ushort NodeId { get { return _nodeId; } }

        /// <summary>从站槽号</summary>
        public ushort SlotNo { get { return _slotNo; } }

        #endregion

        #region 构造函数

        /// <summary>创建从站设备通道</summary>
        /// <param name="cardNo">主站卡号</param>
        /// <param name="nodeId">从站号</param>
        /// <param name="slotNo">从站槽号，默认 0</param>
        public DeltaSlaveDevice(ushort cardNo, ushort nodeId, ushort slotNo = 0)
        {
            _cardNo = cardNo;
            _nodeId = nodeId;
            _slotNo = slotNo;
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

        /// <summary>等待 SDO 传输完成</summary>
        /// <param name="slotNo">槽号</param>
        /// <returns>完成返回 true，超时返回 false</returns>
        private bool WaitSdoDone(ushort slotNo)
        {
            for (int i = 0; i < SdoPollMaxCount; i++)
            {
                ushort done = 1;
                ushort ret = EtherCATMasterNative.CS_ECAT_Slave_SDO_Check_Done(_cardNo, _nodeId, slotNo, ref done);
                if (ret == DeltaErrorCode.NO_ERROR && done == 0)
                {
                    return true;
                }
                Thread.Sleep(SdoPollIntervalMs);
            }
            Log(string.Format("SDO 等待完成超时 卡号 {0} 从站 {1} 槽 {2}", _cardNo, _nodeId, slotNo), MessageLevel.Warning);
            return false;
        }

        #endregion

        #region 公共函数

        /// <summary>SDO 结果：区分本层 API 返回码与驱动器中止码。</summary>
        public struct SdoResult
        {
            /// <summary>传输成功</summary>
            public bool Ok;

            /// <summary>本层 API 函数返回码（非 0 = 请求可能根本没发出去）</summary>
            public ushort ApiReturn;

            /// <summary>驱动器 SDO 中止码（标准段 0x0500~0x06FF；0 = 无中止）</summary>
            public uint AbortCode;

            /// <summary>请求已发出但未在轮询上限内完成</summary>
            public bool TimedOut;
        }

        /// <summary>描述 SDO 传输结果</summary>
        /// <param name="result">传输结果</param>
        /// <returns>可读原因</returns>
        /// <remarks>⛔ 0x1305（=4869）属台达 DLL 自身返回码段，不是 SDO 中止码。
        /// 不能据此判定对象不存在。三态互斥：API 返回码 / 驱动器中止码 / 轮询超时</remarks>
        public static string Describe(SdoResult result)
        {
            if (result.Ok) return "成功";
            if (result.ApiReturn != 0)
            {
                string msg = DeltaErrorCode.GetMessage(result.ApiReturn);
                string known = string.Empty;
                if (result.ApiReturn == 0x1304)
                    known = "（台达 DLL 邮箱错误：MAILBOX / SDO_BUFFER_FULL / ESC_FAILED，请求可能未发出）";
                else if (result.ApiReturn == 0x1305)
                    known = "（台达 DLL 返回码表未收录；与 P1.087/P1.088 失败同码，属邮箱/上一笔事务未结束一类，请求很可能未发出）";
                return msg.Length == 0
                    ? string.Format("API 返回码 0x{0:X4}（{1}）{2}", result.ApiReturn, result.ApiReturn, known)
                    : string.Format("API 返回码 0x{0:X4}（{1}）{2}{3}", result.ApiReturn, result.ApiReturn, msg, known);
            }
            if (result.TimedOut) return "SDO 未完成（轮询超时，驱动器无响应）";
            if (result.AbortCode != 0) return string.Format("驱动器 SDO 中止码 0x{0:X8}", result.AbortCode);
            return "未知失败";
        }

        /// <summary>SDO 读取原始字节</summary>
        /// <remarks>阻塞式：发起后轮询完成，再取中止码</remarks>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">对象字典子索引（**对象子索引**，不是轴号）</param>
        /// <param name="size">读取字节数</param>
        /// <param name="buffer">接收缓冲区（至少 size 字节）</param>
        /// <returns>三态结果；⛔ Ok = false 时 buffer 内容不可信，调用方不得当成 0</returns>
        public SdoResult ReadSdo(ushort index, ushort subIndex, ushort size, byte[] buffer)
        {
            SdoResult result = new SdoResult();
            if (buffer == null || buffer.Length < size)
            {
                result.ApiReturn = 0xFFFF;
                return result;
            }

            for (int attempt = 0; attempt < SdoAttemptCount; attempt++)
            {
                Thread.Sleep(attempt == 0 ? SdoPreDelayMs : SdoMailboxRetryDelayMs);
                result = new SdoResult();
                ushort ret = EtherCATMasterNative.CS_ECAT_Slave_SDO_Read_Message(
                    _cardNo, _nodeId, _slotNo, index, subIndex, size, ref buffer[0]);
                if (ret != DeltaErrorCode.NO_ERROR)
                {
                    result.ApiReturn = ret;
                    // 邮箱忙：请求多半没发出 → 退避重试，⛔ 非对象不存在
                    if (ret == 0x1304 || ret == 0x1305) continue;
                    return result;
                }

                if (!WaitSdoDone(_slotNo))
                {
                    result.TimedOut = true;
                    return result;
                }

                uint abortCode = 0;
                EtherCATMasterNative.CS_ECAT_Slave_SDO_Get_ErrorCode(_cardNo, _nodeId, _slotNo, ref abortCode);
                if (abortCode != 0)
                {
                    result.AbortCode = abortCode;
                    return result;
                }

                result.Ok = true;
                return result;
            }
            return result;
        }

        /// <summary>SDO 读取原始字节（读后等待传输完成）。</summary>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">子索引</param>
        /// <param name="size">读取字节数</param>
        /// <param name="buffer">接收缓冲区（至少 size 字节）</param>
        /// <returns>成功返回 true</returns>
        public bool SdoRead(ushort index, ushort subIndex, ushort size, byte[] buffer)
        {
            SdoResult result = ReadSdo(index, subIndex, size, buffer);
            if (result.Ok) return true;
            Log(string.Format("SDO 读取失败 索引 0x{0:X4} 子索引 {1} {2}", index, subIndex, Describe(result)),
                MessageLevel.Warning);
            return false;
        }

        /// <summary>SDO 快速写入原始字节</summary>
        /// <remarks>阻塞直至完成，邮箱忙退避重试</remarks>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">对象字典子索引（**对象子索引**，不是轴号）</param>
        /// <param name="size">写入字节数</param>
        /// <param name="data">待写数据</param>
        /// <returns>三态结果</returns>
        public SdoResult WriteSdo(ushort index, ushort subIndex, ushort size, byte[] data)
        {
            SdoResult result = new SdoResult();
            if (data == null || data.Length < size)
            {
                result.ApiReturn = 0xFFFF;
                return result;
            }

            byte[] buffer = new byte[size];
            Array.Copy(data, buffer, size);
            for (int attempt = 0; attempt < SdoAttemptCount; attempt++)
            {
                Thread.Sleep(attempt == 0 ? SdoPreDelayMs : SdoMailboxRetryDelayMs);
                ushort ret = EtherCATMasterNative.CS_ECAT_Slave_SDO_Quick_Send_Message(
                    _cardNo, _nodeId, _slotNo, index, subIndex, size, ref buffer[0]);
                if (ret == DeltaErrorCode.NO_ERROR)
                {
                    result.Ok = true;
                    return result;
                }
                result.ApiReturn = ret;
                if (ret != 0x1304 && ret != 0x1305) return result;
            }
            return result;
        }

        /// <summary>SDO 快速写入原始字节（阻塞直至完成）。</summary>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">子索引</param>
        /// <param name="size">写入字节数</param>
        /// <param name="data">待写数据</param>
        /// <returns>成功返回 true</returns>
        public bool SdoWrite(ushort index, ushort subIndex, ushort size, byte[] data)
        {
            SdoResult result = WriteSdo(index, subIndex, size, data);
            if (result.Ok) return true;
            Log(string.Format("SDO 写入失败 索引 0x{0:X4} 子索引 {1} {2}", index, subIndex, Describe(result)),
                MessageLevel.Warning);
            return false;
        }

        /// <summary>PDO 读对象字典数据（周期数据镜像读取）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="size">读取字节数</param>
        /// <param name="buffer">接收缓冲区（至少 size 字节）</param>
        /// <param name="ioType">厂商 IO 类型（透传，默认 0）</param>
        /// <returns>成功返回 true</returns>
        public bool PdoGet(ushort odIndex, ushort odSubIndex, ushort size, byte[] buffer, ushort ioType = 0)
        {
            if (buffer == null || buffer.Length < size) return false;
            try
            {
                ushort ret = EtherCATMasterNative.CS_ECAT_Slave_PDO_Get_OD_Data(
                    _cardNo, _nodeId, _slotNo, ioType, odIndex, odSubIndex, size, ref buffer[0]);
                if (ret != DeltaErrorCode.NO_ERROR)
                {
                    Log(string.Format("PDO 读取失败 索引 0x{0:X4} 子索引 {1} 错误码 0x{2:X4}", odIndex, odSubIndex, ret), MessageLevel.Warning);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("PDO 读取异常 索引 0x{0:X4} 原因 {1}", odIndex, ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>PDO 写对象字典数据（随总线周期下发）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="data">待写数据（首元素起为有效写入区）</param>
        /// <returns>成功返回 true</returns>
        public bool PdoSet(ushort odIndex, ushort odSubIndex, byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            try
            {
                ushort ret = EtherCATMasterNative.CS_ECAT_Slave_PDO_Set_OD_Data(
                    _cardNo, _nodeId, _slotNo, odIndex, odSubIndex, ref data[0]);
                if (ret != DeltaErrorCode.NO_ERROR)
                {
                    Log(string.Format("PDO 写入失败 索引 0x{0:X4} 子索引 {1} 错误码 0x{2:X4}", odIndex, odSubIndex, ret), MessageLevel.Warning);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("PDO 写入异常 索引 0x{0:X4} 原因 {1}", odIndex, ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>SDO 读取无符号 16 位</summary>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">子索引</param>
        /// <param name="value">读取结果</param>
        /// <returns>成功返回 true</returns>
        public bool ReadUInt16(ushort index, ushort subIndex, out ushort value)
        {
            value = 0;
            byte[] buffer = new byte[2];
            if (!SdoRead(index, subIndex, 2, buffer)) return false;
            value = BitConverter.ToUInt16(buffer, 0);
            return true;
        }

        /// <summary>SDO 读取有符号 16 位</summary>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">子索引</param>
        /// <param name="value">读取结果</param>
        /// <returns>成功返回 true</returns>
        public bool ReadInt16(ushort index, ushort subIndex, out short value)
        {
            value = 0;
            byte[] buffer = new byte[2];
            if (!SdoRead(index, subIndex, 2, buffer)) return false;
            value = BitConverter.ToInt16(buffer, 0);
            return true;
        }

        /// <summary>SDO 读取无符号 32 位</summary>
        /// <param name="index">对象字典索引</param>
        /// <param name="subIndex">子索引</param>
        /// <param name="value">读取结果</param>
        /// <returns>成功返回 true</returns>
        public bool ReadUInt32(ushort index, ushort subIndex, out uint value)
        {
            value = 0;
            byte[] buffer = new byte[4];
            if (!SdoRead(index, subIndex, 4, buffer)) return false;
            value = BitConverter.ToUInt32(buffer, 0);
            return true;
        }

        /// <summary>PDO 读取无符号 16 位并按定标还原工程值</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="scale">定标系数（寄存器精度，如 0.1）</param>
        /// <param name="value">工程值（原始值 × 定标）</param>
        /// <returns>成功返回 true</returns>
        public bool PdoGetScaledUInt16(ushort odIndex, ushort odSubIndex, double scale, out double value)
        {
            value = 0;
            byte[] buffer = new byte[2];
            if (!PdoGet(odIndex, odSubIndex, 2, buffer)) return false;
            value = BitConverter.ToUInt16(buffer, 0) * scale;
            return true;
        }

        /// <summary>PDO 读取有符号 16 位并按定标还原工程值</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="scale">定标系数（寄存器精度，如 0.1）</param>
        /// <param name="value">工程值（原始值 × 定标）</param>
        /// <returns>成功返回 true</returns>
        public bool PdoGetScaledInt16(ushort odIndex, ushort odSubIndex, double scale, out double value)
        {
            value = 0;
            byte[] buffer = new byte[2];
            if (!PdoGet(odIndex, odSubIndex, 2, buffer)) return false;
            value = BitConverter.ToInt16(buffer, 0) * scale;
            return true;
        }

        /// <summary>PDO 写无符号 16 位（按定标取整写入寄存器值）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="scale">定标系数（寄存器精度，如 0.1）</param>
        /// <param name="value">工程值（写入时按定标取整）</param>
        /// <returns>成功返回 true</returns>
        public bool PdoSetScaledUInt16(ushort odIndex, ushort odSubIndex, double scale, double value)
        {
            ushort raw = (ushort)Math.Round(value / scale);
            byte[] rawBytes = BitConverter.GetBytes(raw);
            return PdoSet(odIndex, odSubIndex, rawBytes);
        }

        /// <summary>PDO 写有符号 16 位（按定标取整写入寄存器值）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="scale">定标系数（寄存器精度，如 0.1）</param>
        /// <param name="value">工程值（写入时按定标取整）</param>
        /// <returns>成功返回 true</returns>
        public bool PdoSetScaledInt16(ushort odIndex, ushort odSubIndex, double scale, double value)
        {
            short raw = (short)Math.Round(value / scale);
            byte[] rawBytes = BitConverter.GetBytes(raw);
            return PdoSet(odIndex, odSubIndex, rawBytes);
        }

        /// <summary>PDO 写布尔量（单字节 0/1）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="value">真写 1 假写 0</param>
        /// <returns>成功返回 true</returns>
        public bool PdoSetBool(ushort odIndex, ushort odSubIndex, bool value)
        {
            return PdoSet(odIndex, odSubIndex, new byte[] { value ? (byte)1 : (byte)0 });
        }

        /// <summary>PDO 读取布尔量（单字节 0/1）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="value">读取结果</param>
        /// <returns>成功返回 true</returns>
        public bool PdoGetBool(ushort odIndex, ushort odSubIndex, out bool value)
        {
            value = false;
            byte[] buffer = new byte[1];
            if (!PdoGet(odIndex, odSubIndex, 1, buffer)) return false;
            value = buffer[0] != 0;
            return true;
        }

        /// <summary>PDO 读取单字节原始值（状态位等）</summary>
        /// <param name="odIndex">对象字典索引</param>
        /// <param name="odSubIndex">对象字典子索引</param>
        /// <param name="value">读取结果</param>
        /// <returns>成功返回 true</returns>
        public bool PdoGetByte(ushort odIndex, ushort odSubIndex, out byte value)
        {
            value = 0;
            byte[] buffer = new byte[1];
            if (!PdoGet(odIndex, odSubIndex, 1, buffer)) return false;
            value = buffer[0];
            return true;
        }

        #endregion
    }
}
