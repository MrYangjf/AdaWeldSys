using System;
using System.Collections.Generic;

namespace AdaWeldSystem.Comm.IntelligentLaserModbus
{
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