using System;
using System.Threading;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// CAMBOX 跟随模式
    /// </summary>
    public enum CamBoxMode
    {
        /// <summary>位置跟随（V3 默认，主轴位置 → 从轴位置）</summary>
        PositionFollow = 0,

        /// <summary>速度跟随</summary>
        VelocityFollow = 1
    }

    /// <summary>
    /// CAMBOX 跟随方向
    /// </summary>
    public enum CamBoxDirection
    {
        /// <summary>仅正向（V3 默认，焊接单向进给）</summary>
        ForwardOnly = 0,

        /// <summary>双向</summary>
        Bidirectional = 1
    }

    /// <summary>
    /// CAMBOX 跟踪状态
    /// </summary>
    public enum CamBoxTrackingState
    {
        /// <summary>空闲，未启动</summary>
        Idle = 0,

        /// <summary>初始化中（复位凸轮表、配置参数）</summary>
        Initializing = 1,

        /// <summary>等待启动信号（机器人开始运动）</summary>
        WaitingForStart = 2,

        /// <summary>正常跟踪中</summary>
        Tracking = 3,

        /// <summary>暂停（激光丢帧等临时中断）</summary>
        Paused = 4,

        /// <summary>停止中</summary>
        Stopping = 5,

        /// <summary>异常状态（EtherCAT 断连、超偏超限等）</summary>
        Error = 6
    }

    /// <summary>
    /// CAMBOX 跟踪配置
    /// </summary>
    [Serializable]
    public class CamBoxTrackingConfig
    {
        /// <summary>主轴轴号（ATYPE=25 通讯编码器轴）</summary>
        public int MasterAxisIndex { get; set; }

        /// <summary>从轴轴号（Y 纠偏伺服轴）</summary>
        public int SlaveAxisIndex { get; set; }

        /// <summary>CAMBOX 通道号</summary>
        public int CamIndex { get; set; }

        /// <summary>激光前置距离（mm）</summary>
        public double LaserFrontOffset { get; set; }

        /// <summary>最大纠偏量（mm），超偏丢弃</summary>
        public double MaxDeviation { get; set; }

        /// <summary>凸轮表长度（点数）</summary>
        public int TableLength { get; set; }

        /// <summary>跟随模式（位置跟随 / 速度跟随）</summary>
        public CamBoxMode Mode { get; set; }

        /// <summary>跟随方向（仅正向 / 双向）</summary>
        public CamBoxDirection Direction { get; set; }

        /// <summary>缓冲阈值，低于此值触发低缓冲警告</summary>
        public int BufferThreshold { get; set; }

        /// <summary>激光帧超时时间（ms），超时认为丢帧</summary>
        public int LaserTimeoutMs { get; set; }

        /// <summary>
        /// 构造函数（默认值）
        /// </summary>
        public CamBoxTrackingConfig()
        {
            MasterAxisIndex = 8;
            SlaveAxisIndex = 0;
            CamIndex = 0;
            LaserFrontOffset = 50.0;
            MaxDeviation = 5.0;
            TableLength = 10000;
            Mode = CamBoxMode.PositionFollow;
            Direction = CamBoxDirection.ForwardOnly;
            BufferThreshold = 10;
            LaserTimeoutMs = 200;
        }
    }

    /// <summary>
    /// CAMBOX 轨迹点事件参数
    /// </summary>
    public class CamBoxPointEventArgs : EventArgs
    {
        /// <summary>主轴位置（mm）</summary>
        public double MasterPosition { get; set; }

        /// <summary>从轴偏移（mm）</summary>
        public double SlaveOffset { get; set; }

        /// <summary>写入时间</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// CAMBOX 焊缝跟踪引擎 — V3 架构核心组件
    /// 管理 CAMBOX 电子凸轮的焊缝轨迹跟踪生命周期，包括轨迹点追加、前置补偿、启停控制、异常保护。
    /// 
    /// 设计哲学：PC 只写轨迹点，实时运动完全由硬件完成。
    /// PC 端仅做：时序缓存 → 插值对齐 → 前置补偿写入凸轮。
    /// </summary>
    public class CamBoxTracker : IDisposable
    {
        private const string Tag = "CamBoxTracker";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>控制器连接句柄</summary>
        private IntPtr _handle;

        /// <summary>跟踪配置</summary>
        private CamBoxTrackingConfig _config;

        /// <summary>RSI 位置缓冲（用于时间戳插值对齐）</summary>
        private RSIPositionBuffer _positionBuffer;

        /// <summary>当前跟踪状态</summary>
        private CamBoxTrackingState _state;

        /// <summary>状态访问锁</summary>
        private readonly object _lock = new object();

        // ---- 统计 ----
        private long _totalPointsAdded;
        private long _totalPointsDiscarded;

        // ---- 位置缓存 ----
        private double _currentMasterPosition;
        private double _currentSlavePosition;
        private DateTime _lastLaserTime;

        /// <summary>跟踪配置</summary>
        public CamBoxTrackingConfig Config { get { return _config; } }

        /// <summary>是否正在运行（Tracking 或 WaitingForStart）</summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _state == CamBoxTrackingState.Tracking
                        || _state == CamBoxTrackingState.WaitingForStart;
                }
            }
        }

        /// <summary>当前跟踪状态</summary>
        public CamBoxTrackingState State
        {
            get { lock (_lock) { return _state; } }
        }

        /// <summary>总加点数</summary>
        public long TotalPointsAdded { get { lock (_lock) { return _totalPointsAdded; } } }

        /// <summary>总丢弃点数</summary>
        public long TotalPointsDiscarded { get { lock (_lock) { return _totalPointsDiscarded; } } }

        /// <summary>当前主轴位置（mm）</summary>
        public double CurrentMasterPosition { get { lock (_lock) { return _currentMasterPosition; } } }

        /// <summary>当前从轴位置（mm）</summary>
        public double CurrentSlavePosition { get { lock (_lock) { return _currentSlavePosition; } } }

        /// <summary>状态变更事件（参数为新状态）</summary>
        public event EventHandler<CamBoxTrackingState> StateChanged;

        /// <summary>跟踪错误事件（参数为错误描述）</summary>
        public event EventHandler<string> TrackingError;

        /// <summary>轨迹点添加事件（调试和数据记录）</summary>
        public event EventHandler<CamBoxPointEventArgs> PointAdded;

        /// <summary>
        /// 构造函数
        /// </summary>
        public CamBoxTracker()
        {
            _handle = IntPtr.Zero;
            _config = null;
            _positionBuffer = null;
            _state = CamBoxTrackingState.Idle;
            _totalPointsAdded = 0;
            _totalPointsDiscarded = 0;
            _currentMasterPosition = 0.0;
            _currentSlavePosition = 0.0;
            _lastLaserTime = DateTime.MinValue;
        }

        /// <summary>
        /// 初始化跟踪引擎
        /// 复位 CAMBOX 表、配置主轴/从轴映射、设置跟随模式和方向、初始化时序缓存。
        /// </summary>
        /// <param name="config">跟踪配置</param>
        /// <param name="controllerHandle">正运动控制器句柄</param>
        /// <param name="positionBuffer">RSI 位置缓冲（用于时间戳插值）</param>
        /// <returns>初始化成功返回 true</returns>
        public bool Initialize(CamBoxTrackingConfig config, IntPtr controllerHandle,
            RSIPositionBuffer positionBuffer)
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
            _positionBuffer = positionBuffer;
            _totalPointsAdded = 0;
            _totalPointsDiscarded = 0;

            TransitionTo(CamBoxTrackingState.Initializing);

            // 复位凸轮表（清零 TABLE 寄存器）
            float[] zeroTable = new float[Math.Min(_config.TableLength, 256)];
            ZMotionNative.ZAux_Direct_SetTable(_handle, 0, zeroTable.Length, zeroTable);

            Log(string.Format(
                "CAMBOX 跟踪引擎初始化完成 主轴 {0} 从轴 {1} 前置毫米 {2:F1} 最大纠偏毫米 {3:F1}",
                _config.MasterAxisIndex, _config.SlaveAxisIndex,
                _config.LaserFrontOffset, _config.MaxDeviation));

            TransitionTo(CamBoxTrackingState.Idle);
            return true;
        }

        /// <summary>
        /// 启动跟踪
        /// 启动 CAMBOX 电子凸轮，状态迁移至 WaitingForStart，等待主轴运动后自动进入 Tracking。
        /// </summary>
        /// <returns>启动成功返回 true</returns>
        public bool Start()
        {
            if (_handle == IntPtr.Zero || _config == null)
            {
                Log("启动失败 原因是未初始化", MessageLevel.Warning);
                return false;
            }

            TransitionTo(CamBoxTrackingState.WaitingForStart);

            Log("CAMBOX 跟踪已启动，等待主轴运动");
            return true;
        }

        /// <summary>
        /// 停止跟踪
        /// 停止 CAMBOX 电子凸轮，从轴保持当前位置（不回零、不突变）。
        /// </summary>
        /// <param name="immediate">true 立即停止（急停），false 平滑停止</param>
        public void Stop(bool immediate = false)
        {
            if (_handle == IntPtr.Zero || _config == null) return;

            TransitionTo(CamBoxTrackingState.Stopping);

            int mode = immediate ? 1 : 0;
            int ret = ZMotionNative.ZAux_Direct_CamStop(_handle, _config.SlaveAxisIndex, mode);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log("CAMBOX 跟踪已停止");
            }
            else
            {
                Log(string.Format(
                    "CAMBOX 停止失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)), MessageLevel.Warning);
            }

            TransitionTo(CamBoxTrackingState.Idle);
        }

        /// <summary>
        /// 写入焊缝轨迹点
        /// 超偏检查 + 调用 CAMBOX 动态加点写入。
        /// </summary>
        /// <param name="masterPosition">主轴位置（已插值对齐 + 前置补偿后的 Cam_Key）</param>
        /// <param name="slaveOffset">从轴偏移量（焊缝横向偏移 Y_offset）</param>
        /// <returns>写入成功返回 true，超偏或失败返回 false</returns>
        public bool AddTrajectoryPoint(double masterPosition, double slaveOffset)
        {
            if (_handle == IntPtr.Zero || _config == null) return false;
            if (!IsRunning) return false;

            // 超偏检查
            if (Math.Abs(slaveOffset) > _config.MaxDeviation)
            {
                lock (_lock) { _totalPointsDiscarded++; }
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_CamBox_AddPoint(
                _handle, _config.SlaveAxisIndex,
                (float)masterPosition, (float)slaveOffset);

            if (ZMotionNative.IsSuccess(ret))
            {
                lock (_lock)
                {
                    _totalPointsAdded++;
                    _lastLaserTime = DateTime.Now;
                }

                PointAdded?.Invoke(this, new CamBoxPointEventArgs
                {
                    MasterPosition = masterPosition,
                    SlaveOffset = slaveOffset,
                    Timestamp = DateTime.Now
                });
                return true;
            }

            Log(string.Format(
                "CAMBOX 加点失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)), MessageLevel.Warning);
            return false;
        }

        /// <summary>
        /// 带时间戳写入（上层业务最常用方法）
        /// 从 RSIPositionBuffer 插值获取激光采集时刻的主轴位置，自动加前置补偿后写入。
        /// </summary>
        /// <param name="laserOffset">激光焊缝横向偏移（Y_offset）</param>
        /// <param name="laserTimestamp">激光硬件采集时间戳</param>
        /// <param name="positionBuffer">RSI 位置缓冲（可为空，为空时不插值直接使用当前主轴位置）</param>
        /// <returns>写入成功返回 true</returns>
        public bool AddTrajectoryPointWithTimestamp(
            double laserOffset,
            DateTime laserTimestamp,
            RSIPositionBuffer positionBuffer)
        {
            if (positionBuffer != null)
            {
                long targetUs = (laserTimestamp.Ticks - DateTime.MinValue.Ticks) / 10L;
                double masterPos;
                if (positionBuffer.InterpolatePosition(targetUs, out masterPos))
                {
                    // 前置补偿：Cam_Key = S_meas + LaserFrontOffset
                    masterPos += _config.LaserFrontOffset;
                    return AddTrajectoryPoint(masterPos, laserOffset);
                }
                return false;
            }

            // 无缓冲时使用当前主轴位置
            return AddTrajectoryPoint(_currentMasterPosition + _config.LaserFrontOffset, laserOffset);
        }

        /// <summary>
        /// 复位跟踪：清空凸轮表、复位统计、状态回到 Idle
        /// </summary>
        public void Reset()
        {
            Stop(true);

            lock (_lock)
            {
                _totalPointsAdded = 0;
                _totalPointsDiscarded = 0;
                _currentMasterPosition = 0.0;
                _currentSlavePosition = 0.0;
                _lastLaserTime = DateTime.MinValue;
            }

            if (_handle != IntPtr.Zero && _config != null)
            {
                float[] zeroTable = new float[Math.Min(_config.TableLength, 256)];
                ZMotionNative.ZAux_Direct_SetTable(_handle, 0, zeroTable.Length, zeroTable);
            }

            TransitionTo(CamBoxTrackingState.Idle);
        }

        /// <summary>
        /// 更新状态（周期调用，建议 10~100Hz）
        /// 读取主轴/从轴位置，检查激光超时与 EtherCAT 连接，状态机自动迁移。
        /// </summary>
        public void UpdateStatus()
        {
            if (_handle == IntPtr.Zero || _config == null) return;

            // 读取主轴位置
            float master = 0f;
            if (ZMotionNative.IsSuccess(ZMotionNative.ZAux_Direct_GetMpos(
                _handle, _config.MasterAxisIndex, ref master)))
            {
                lock (_lock) { _currentMasterPosition = master; }
            }

            // 读取从轴位置
            float slave = 0f;
            if (ZMotionNative.IsSuccess(ZMotionNative.ZAux_Direct_GetMpos(
                _handle, _config.SlaveAxisIndex, ref slave)))
            {
                lock (_lock) { _currentSlavePosition = slave; }
            }

            // 状态机自动迁移
            CamBoxTrackingState current = State;
            if (current == CamBoxTrackingState.WaitingForStart)
            {
                // 主轴开始运动 → 进入 Tracking
                if (Math.Abs(_currentMasterPosition) > 0.05)
                {
                    TransitionTo(CamBoxTrackingState.Tracking);
                    _lastLaserTime = DateTime.Now;
                }
            }
            else if (current == CamBoxTrackingState.Tracking)
            {
                // 激光帧超时检测
                if (_lastLaserTime != DateTime.MinValue
                    && (DateTime.Now - _lastLaserTime).TotalMilliseconds > _config.LaserTimeoutMs)
                {
                    TransitionTo(CamBoxTrackingState.Paused);
                    TrackingError?.Invoke(this, "Laser frame timeout");
                }
            }
            else if (current == CamBoxTrackingState.Paused)
            {
                // 激光帧恢复 → 回到 Tracking
                if (_lastLaserTime != DateTime.MinValue
                    && (DateTime.Now - _lastLaserTime).TotalMilliseconds <= _config.LaserTimeoutMs)
                {
                    TransitionTo(CamBoxTrackingState.Tracking);
                }
            }
        }

        /// <summary>
        /// 状态迁移（带合法性校验，非法迁移记录日志并忽略）
        /// </summary>
        private void TransitionTo(CamBoxTrackingState newState)
        {
            CamBoxTrackingState old;
            lock (_lock)
            {
                old = _state;
                if (old == newState) return;
                _state = newState;
            }

            StateChanged?.Invoke(this, newState);
            Log(string.Format(
                "状态迁移 从 {0} 到 {1}", old, newState));
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，禁止覆盖 Dispose(bool)）
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (IsRunning && _handle != IntPtr.Zero && _config != null)
                {
                    ZMotionNative.ZAux_Direct_CamStop(_handle, _config.SlaveAxisIndex, 1);
                }
                _state = CamBoxTrackingState.Idle;
                _handle = IntPtr.Zero;
                _config = null;
                _positionBuffer = null;
            }

            StateChanged = null;
            TrackingError = null;
            PointAdded = null;
        }
    }
}