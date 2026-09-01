using System;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// RSI 位置时间戳环形缓冲（V3 架构核心组件）
    /// 缓存 KUKA RSI 通过 EtherCAT PDO 推送的「位置 + 高精度时间戳」序列。
    /// 为激光帧提供时间戳插值对齐，消除传输延迟误差。
    /// 
    /// 设计哲学：解决"激光采集时刻、机器人 PDO 时刻、网络接收时刻全部不对齐"的根本问题。
    /// 精度取决于插值算法与时间戳分辨率，匀速进给下线性插值误差小于 0.01mm。
    /// </summary>
    public class RSIPositionBuffer : IDisposable
    {
        private const string Tag = "RSIPositionBuffer";

        /// <summary>默认容量：250 个采样点（RSI 4ms 周期 = 1 秒数据）</summary>
        private const int DefaultCapacity = 250;

        /// <summary>最小容量</summary>
        private const int MinCapacity = 50;

        /// <summary>最大容量</summary>
        private const int MaxCapacity = 1000;

        /// <summary>时间戳回绕检测倍数（差值大于最大周期x100 视为回绕）</summary>
        private const long WrapThresholdFactor = 100L;

        /// <summary>环形缓冲数据数组</summary>
        private readonly PositionSample[] _samples;

        /// <summary>缓冲容量</summary>
        private readonly int _capacity;

        /// <summary>当前数据数量</summary>
        private int _count;

        /// <summary>写入位置（下一个写入槽）</summary>
        private int _writeIndex;

        /// <summary>数据访问锁</summary>
        private readonly object _lock = new object();

        // ---- 统计 ----
        private long _totalSamples;
        private int _interpolationHitCount;
        private int _interpolationMissCount;
        private long _droppedCount;

        /// <summary>缓冲容量</summary>
        public int Capacity { get { return _capacity; } }

        /// <summary>当前数据数量</summary>
        public int Count { get { lock (_lock) { return _count; } } }

        /// <summary>是否已满</summary>
        public bool IsFull { get { lock (_lock) { return _count >= _capacity; } } }

        /// <summary>最新位置（mm）</summary>
        public double LatestPosition
        {
            get
            {
                lock (_lock)
                {
                    if (_count == 0) return 0.0;
                    int idx = (_writeIndex + _capacity - 1) % _capacity;
                    return _samples[idx].Position;
                }
            }
        }

        /// <summary>最新时间戳（微秒）</summary>
        public long LatestTimestamp
        {
            get
            {
                lock (_lock)
                {
                    if (_count == 0) return 0L;
                    int idx = (_writeIndex + _capacity - 1) % _capacity;
                    return _samples[idx].Timestamp;
                }
            }
        }

        /// <summary>总采样数</summary>
        public long TotalSamples { get { lock (_lock) { return _totalSamples; } } }

        /// <summary>插值命中计数</summary>
        public int InterpolationHitCount { get { lock (_lock) { return _interpolationHitCount; } } }

        /// <summary>插值未命中计数</summary>
        public int InterpolationMissCount { get { lock (_lock) { return _interpolationMissCount; } } }

        /// <summary>
        /// 默认容量构造
        /// </summary>
        public RSIPositionBuffer() : this(DefaultCapacity)
        {
        }

        /// <summary>
        /// 指定容量构造
        /// </summary>
        /// <param name="capacity">缓冲容量（50~1000）</param>
        public RSIPositionBuffer(int capacity)
        {
            _capacity = Math.Max(MinCapacity, Math.Min(MaxCapacity, capacity));
            _samples = new PositionSample[_capacity];
            _count = 0;
            _writeIndex = 0;
            _totalSamples = 0;
            _interpolationHitCount = 0;
            _interpolationMissCount = 0;
            _droppedCount = 0;
        }

        /// <summary>
        /// 添加一个位置采样点
        /// </summary>
        /// <param name="timestamp">时间戳（微秒）</param>
        /// <param name="position">主轴位置（mm）</param>
        /// <param name="velocity">瞬时速度（mm/s，可选，用于速度补偿插值）</param>
        public void AddSample(long timestamp, double position, double velocity = 0)
        {
            lock (_lock)
            {
                // 时间戳严格递增校验（乱序丢弃）
                if (_count > 0)
                {
                    int lastIdx = (_writeIndex + _capacity - 1) % _capacity;
                    long lastTs = _samples[lastIdx].Timestamp;
                    if (timestamp < 0) { return; }
                    long diff = timestamp - lastTs;
                    // 回绕处理：新时间戳远小于上次视为 UINT32 回绕，允许继续
                    if (diff < 0 && (-diff) > WrapThresholdFactor * 1000000L)
                    {
                        // 视为回绕，允许继续（时间线连续）
                    }
                    else if (diff <= 0)
                    {
                        _droppedCount++;
                        return;
                    }
                }

                _samples[_writeIndex] = new PositionSample
                {
                    Timestamp = timestamp,
                    Position = position,
                    Velocity = velocity
                };

                _writeIndex = (_writeIndex + 1) % _capacity;
                if (_count < _capacity) _count++;
                _totalSamples++;
            }
        }

        /// <summary>
        /// 插值获取指定时刻的主轴位置（线性插值）
        /// </summary>
        /// <param name="targetTimestamp">目标时间戳（微秒，即激光采集时刻）</param>
        /// <param name="position">输出：插值得到的位置（mm）</param>
        /// <returns>插值成功返回 true</returns>
        public bool InterpolatePosition(long targetTimestamp, out double position)
        {
            position = 0.0;
            lock (_lock)
            {
                if (_count < 2)
                {
                    _interpolationMissCount++;
                    return false;
                }

                // 在环形缓冲中定位 targetTimestamp 的包围区间（从最新向旧扫描）
                int lo = -1;
                for (int i = 0; i < _count; i++)
                {
                    int realIdx = (_writeIndex - 1 - i + _capacity * 2) % _capacity;
                    if (_samples[realIdx].Timestamp <= targetTimestamp)
                    {
                        lo = realIdx;
                        break;
                    }
                }
                if (lo == -1)
                {
                    _interpolationMissCount++;
                    return false; // 目标太旧
                }

                // 找 lo 的下一个点（时间上更新）
                int nextIdx = (lo + 1) % _capacity;
                if (_samples[nextIdx].Timestamp < _samples[lo].Timestamp)
                {
                    _interpolationMissCount++;
                    return false; // 目标超出最新数据
                }

                long t1 = _samples[lo].Timestamp;
                long t2 = _samples[nextIdx].Timestamp;
                if (t2 == t1)
                {
                    position = _samples[lo].Position;
                }
                else
                {
                    double ratio = (double)(targetTimestamp - t1) / (double)(t2 - t1);
                    position = _samples[lo].Position +
                        (_samples[nextIdx].Position - _samples[lo].Position) * ratio;
                }

                _interpolationHitCount++;
                return true;
            }
        }

        /// <summary>
        /// 带速度补偿的插值（Hermite 插值，考虑加减速）
        /// 匀速进给场景下与线性插值等价；速度变化大时更精确。
        /// </summary>
        /// <param name="targetTimestamp">目标时间戳（微秒）</param>
        /// <param name="position">输出：插值得到的位置（mm）</param>
        /// <returns>插值成功返回 true</returns>
        public bool InterpolateWithVelocity(long targetTimestamp, out double position)
        {
            position = 0.0;
            lock (_lock)
            {
                if (_count < 3)
                {
                    _interpolationMissCount++;
                    return false;
                }

                int lo = -1;
                for (int i = 0; i < _count; i++)
                {
                    int realIdx = (_writeIndex - 1 - i + _capacity * 2) % _capacity;
                    if (_samples[realIdx].Timestamp <= targetTimestamp)
                    {
                        lo = realIdx;
                        break;
                    }
                }
                if (lo == -1)
                {
                    _interpolationMissCount++;
                    return false;
                }

                int nextIdx = (lo + 1) % _capacity;
                if (_samples[nextIdx].Timestamp < _samples[lo].Timestamp)
                {
                    _interpolationMissCount++;
                    return false;
                }

                long t1 = _samples[lo].Timestamp;
                long t2 = _samples[nextIdx].Timestamp;
                double p1 = _samples[lo].Position;
                double p2 = _samples[nextIdx].Position;
                double v1 = _samples[lo].Velocity;
                double v2 = _samples[nextIdx].Velocity;

                double dt = (double)(t2 - t1);
                if (dt <= 0) { position = p1; }
                else
                {
                    double s = (double)(targetTimestamp - t1) / dt;
                    // 三次 Hermite 插值
                    double h00 = 2 * s * s * s - 3 * s * s + 1;
                    double h10 = s * s * s - 2 * s * s + s;
                    double h01 = -2 * s * s * s + 3 * s * s;
                    double h11 = s * s * s - s * s;
                    position = h00 * p1 + h10 * dt * v1 + h01 * p2 + h11 * dt * v2;
                }

                _interpolationHitCount++;
                return true;
            }
        }

        /// <summary>
        /// 清空缓冲区，重置统计计数
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _count = 0;
                _writeIndex = 0;
                _totalSamples = 0;
                _interpolationHitCount = 0;
                _interpolationMissCount = 0;
                _droppedCount = 0;
            }
        }

        /// <summary>
        /// 获取最新采样点
        /// </summary>
        /// <returns>最新采样点；缓冲区为空返回默认值</returns>
        public PositionSample GetLatestSample()
        {
            lock (_lock)
            {
                if (_count == 0) return default(PositionSample);
                int idx = (_writeIndex + _capacity - 1) % _capacity;
                return _samples[idx];
            }
        }

        /// <summary>
        /// 统计指定时间范围内的采样点数量（诊断数据密度）
        /// </summary>
        /// <param name="startTime">起始时间戳（微秒）</param>
        /// <param name="endTime">结束时间戳（微秒）</param>
        /// <returns>范围内采样点数量</returns>
        public int GetSampleCountInRange(long startTime, long endTime)
        {
            lock (_lock)
            {
                int count = 0;
                for (int i = 0; i < _count; i++)
                {
                    int realIdx = (_writeIndex - 1 - i + _capacity * 2) % _capacity;
                    long ts = _samples[realIdx].Timestamp;
                    if (ts >= startTime && ts <= endTime) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，禁止覆盖 Dispose(bool)；资源均为托管数组）
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                Array.Clear(_samples, 0, _samples.Length);
                _count = 0;
                _writeIndex = 0;
            }
        }
    }

    /// <summary>
    /// 位置采样点结构
    /// </summary>
    public struct PositionSample
    {
        /// <summary>高精度时间戳（微秒）</summary>
        public long Timestamp;

        /// <summary>主轴位置（mm）</summary>
        public double Position;

        /// <summary>瞬时速度（mm/s，可选，用于速度补偿插值）</summary>
        public double Velocity;
    }
}