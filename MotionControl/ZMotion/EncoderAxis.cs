using System;
using System.Threading;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 编码器来源类型
    /// </summary>
    public enum EncoderSourceType
    {
        /// <summary>EtherCAT PDO 通讯编码器（V3 默认，ATYPE=25）</summary>
        EtherCATPDO = 0,

        /// <summary>脉冲输入编码器（物理光栅尺）</summary>
        PulseInput = 1,

        /// <summary>SSI 绝对编码器</summary>
        SSI = 2,

        /// <summary>EnDat 绝对编码器</summary>
        EnDat = 3
    }

    /// <summary>
    /// 通讯编码器轴主配置
    /// </summary>
    [Serializable]
    public class EncoderAxisConfig
    {
        /// <summary>轴号（建议使用 8 及以上，避开物理伺服轴 0~3）</summary>
        public int AxisIndex { get; set; }

        /// <summary>轴名称</summary>
        public string Name { get; set; }

        /// <summary>编码器来源类型</summary>
        public EncoderSourceType SourceType { get; set; }

        /// <summary>脉冲当量（units/mm），与机器人单位对齐，建议 1.0</summary>
        public double Units { get; set; }

        /// <summary>EtherCAT 从站索引（机器人 = 从站 1）</summary>
        public int EtherCATSlaveIndex { get; set; }

        /// <summary>PDO 数据项索引（ToolRelX 在 PDO 中的位置）</summary>
        public int PDOEntryIndex { get; set; }

        /// <summary>数据位数（32 = REAL32 / float）</summary>
        public int DataSize { get; set; }

        /// <summary>计数方向（1 正向 / -1 反向）</summary>
        public int Direction { get; set; }

        /// <summary>
        /// 构造函数（默认值）
        /// </summary>
        public EncoderAxisConfig()
        {
            AxisIndex = 8;
            Name = "通讯编码器主轴";
            SourceType = EncoderSourceType.EtherCATPDO;
            Units = 1.0;
            EtherCATSlaveIndex = 1;
            PDOEntryIndex = 0;
            DataSize = 32;
            Direction = 1;
        }
    }

    /// <summary>
    /// 位置更新事件参数
    /// </summary>
    public class PositionUpdatedEventArgs : EventArgs
    {
        /// <summary>当前位置（mm）</summary>
        public double Position { get; set; }

        /// <summary>位置变化量（mm）</summary>
        public double Delta { get; set; }

        /// <summary>更新时间戳</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 通讯编码器轴（ATYPE=25）— V3 架构核心组件
    /// 作为 CAMBOX 电子凸轮的主轴，接收 KUKA 机器人 EtherCAT PDO 推送的 ToolRelX 位置。
    /// 
    /// 本质：不跑电机、不输出脉冲，完全由 EtherCAT 总线硬件周期刷新 MPOS 的虚拟编码器轴。
    /// 实时跟随由 CAMBOX 硬件直接访问 MPOS，不走软件路径。
    /// </summary>
    public class EncoderAxis : IDisposable
    {
        private const string Tag = "EncoderAxis";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }
        private const string ATYPE_PARAM = "ATYPE";
        private const int ATYPE_ETHERNET_ENCODER = 25;

        /// <summary>控制器连接句柄</summary>
        private IntPtr _handle;

        /// <summary>轴配置</summary>
        private EncoderAxisConfig _config;

        /// <summary>当前缓存位置（mm）</summary>
        private double _currentPosition;

        /// <summary>上次读取位置（用于计算 Delta）</summary>
        private double _lastPosition;

        /// <summary>是否已初始化</summary>
        private bool _initialized;

        /// <summary>EtherCAT 连接状态</summary>
        private bool _isConnected;

        /// <summary>位置访问锁</summary>
        private readonly object _lock = new object();

        /// <summary>轴号</summary>
        public int AxisIndex { get { return _config != null ? _config.AxisIndex : -1; } }

        /// <summary>轴名称</summary>
        public string Name { get { return _config != null ? _config.Name : string.Empty; } }

        /// <summary>当前主轴位置（mm）</summary>
        public double CurrentPosition { get { lock (_lock) { return _currentPosition; } } }

        /// <summary>脉冲当量（units/mm）</summary>
        public double Units { get { return _config != null ? _config.Units : 1.0; } }

        /// <summary>是否已连接（EtherCAT 在线）</summary>
        public bool IsConnected { get { return _initialized && _isConnected; } }

        /// <summary>编码器来源类型</summary>
        public EncoderSourceType SourceType { get { return _config != null ? _config.SourceType : EncoderSourceType.EtherCATPDO; } }

        /// <summary>位置更新事件（用于 UI 显示和数据记录，不用于实时控制）</summary>
        public event EventHandler<PositionUpdatedEventArgs> PositionUpdated;

        /// <summary>
        /// 初始化通讯编码器轴
        /// 设置 ATYPE=25、配置 UNITS、清零当前位置。
        /// </summary>
        /// <param name="config">编码器轴配置</param>
        /// <param name="controllerHandle">正运动控制器句柄</param>
        /// <returns>初始化成功返回 true</returns>
        public bool Initialize(EncoderAxisConfig config, IntPtr controllerHandle)
        {
            if (config == null)
            {
                Log("初始化失败 原因是配置为空", MessageLevel.Warning);
                return false;
            }
            if (controllerHandle == IntPtr.Zero)
            {
                Log("初始化失败 原因是控制器未连接", MessageLevel.Warning);
                return false;
            }

            _config = config;
            _handle = controllerHandle;
            _initialized = false;
            _currentPosition = 0.0;
            _lastPosition = 0.0;

            // 1. 设置轴类型 ATYPE = 25（EtherCAT 通讯编码器）
            float atype = ATYPE_ETHERNET_ENCODER;
            int ret = ZMotionNative.ZAux_Direct_SetParam(
                _handle, ATYPE_PARAM, _config.AxisIndex, ref atype);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "ATYPE 设置失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)), MessageLevel.Error);
                return false;
            }

            // 2. 设置脉冲当量 UNITS
            float units = (float)_config.Units;
            ret = ZMotionNative.ZAux_Direct_SetUnits(_handle, _config.AxisIndex, units);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "UNITS 设置失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)), MessageLevel.Error);
                return false;
            }

            // 3. 清零当前位置
            ret = ZMotionNative.ZAux_Direct_SetDpos(_handle, _config.AxisIndex, 0f);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "位置清零失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)), MessageLevel.Warning);
            }

            _initialized = true;
            _isConnected = true;

            Log(string.Format(
                "通讯编码器轴初始化完成 轴号 {0} 名称 {1} ATYPE {2} 单位 {3:F2}",
                _config.AxisIndex, _config.Name, ATYPE_ETHERNET_ENCODER, _config.Units));

            return true;
        }

        /// <summary>
        /// 读取当前位置（MPOS）
        /// 软件读取，用于状态监控和调试。实时跟随由 CAMBOX 硬件直接访问 MPOS。
        /// </summary>
        /// <returns>当前主轴位置（mm）</returns>
        public double GetCurrentPosition()
        {
            if (!_initialized || _handle == IntPtr.Zero)
            {
                return 0.0;
            }

            float value = 0f;
            int ret = ZMotionNative.ZAux_Direct_GetMpos(_handle, _config.AxisIndex, ref value);
            if (ZMotionNative.IsSuccess(ret))
            {
                lock (_lock)
                {
                    _currentPosition = value;
                }
            }

            return _currentPosition;
        }

        /// <summary>
        /// 设置当前位置（清零或设置初始值）
        /// 仅在 CAMBOX 未启动时调用；跟踪运行中调用会导致从轴突变。
        /// </summary>
        /// <param name="position">要设置的位置值（mm）</param>
        public void SetPosition(double position)
        {
            if (!_initialized || _handle == IntPtr.Zero) return;

            float value = (float)position;
            int ret = ZMotionNative.ZAux_Direct_SetDpos(_handle, _config.AxisIndex, value);
            if (ZMotionNative.IsSuccess(ret))
            {
                lock (_lock)
                {
                    _currentPosition = position;
                    _lastPosition = position;
                }
                Log(string.Format(
                    "编码器轴位置已设置 毫米 {0:F2}", position));
            }
            else
            {
                Log(string.Format(
                    "编码器轴位置设置失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)), MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 复位轴：清零当前位置、复位轴状态
        /// </summary>
        public void Reset()
        {
            SetPosition(0.0);
            lock (_lock)
            {
                _lastPosition = 0.0;
            }
        }

        /// <summary>
        /// 更新状态（周期调用，建议 10~50Hz）
        /// 读取当前位置到缓存，触发 PositionUpdated 事件（位置有变化时）。
        /// </summary>
        public void UpdateStatus()
        {
            if (!_initialized || _handle == IntPtr.Zero) return;

            GetCurrentPosition();

            double current;
            lock (_lock)
            {
                current = _currentPosition;
            }

            double delta = current - _lastPosition;
            if (Math.Abs(delta) > 0.0001)
            {
                _lastPosition = current;
                PositionUpdated?.Invoke(this, new PositionUpdatedEventArgs
                {
                    Position = current,
                    Delta = delta,
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，禁止覆盖 Dispose(bool)）
        /// </summary>
        public void Dispose()
        {
            _initialized = false;
            _isConnected = false;
            _handle = IntPtr.Zero;
            _config = null;
            PositionUpdated = null;
        }
    }
}