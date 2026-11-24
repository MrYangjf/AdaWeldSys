using System;
using System.Collections.Generic;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.WeldParamControl.Abstractions;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>线激光焊缝几何轨迹跟踪算法实现（非单例）。</summary>
    public class SeamTrackingAlgorithm : WeldAlgorithmBase
    {
        #region 常量

        private const string Tag = "线激光焊缝跟踪";

        #endregion

        #region 私有变量

        private readonly object _lock = new object();
        private readonly List<PVTPoint> _pending;

        private WeldParamConfig _config;

        // 位置一阶差分求速度
        private double _lastPos;
        private bool _hasLastPos;

        // 机器人行程与无值跨度
        private double _lastRobotX;
        private bool _hasLastRobotX;
        private double _noValueSpanMm;

        // 超限剔除的参考基准
        private double _lastAcceptedDeviation;
        private bool _hasLastAccepted;

        // 单调窗口状态
        private double _monotonicWindowStartX;
        private bool _monotonicWindowActive;
        private double _monotonicWindowFirstDev;
        private double _monotonicOutput;
        private int _monotonicDirection;
        private bool _monotonicDirectionLocked;

        // 输出节拍与报警
        private DateTime _lastOutputTime;
        private bool _hasFault;
        private string _faultReason;

        // 可观测性
        private double _lastDeviation;
        private double _lastTargetPos;

        #endregion

        #region 公共变量

        /// <summary>算法名称。</summary>
        public override string Name { get { return "线激光焊缝几何轨迹跟踪"; } }

        /// <summary>算法是否已就绪（配置已载入）。</summary>
        public override bool IsReady { get { lock (_lock) { return _config != null; } } }

        /// <summary>当前配置（只读引用，运行期不再改动）。</summary>
        public WeldParamConfig Config { get { lock (_lock) { return _config; } } }

        /// <summary>是否已触发报警（无有效结果超限）。</summary>
        public bool HasFault { get { lock (_lock) { return _hasFault; } } }

        /// <summary>报警原因（纯文本，无符号）。</summary>
        public string FaultReason { get { lock (_lock) { return _faultReason; } } }

        /// <summary>最近一次采用的偏差（毫米），供界面观测。</summary>
        public double LastDeviation { get { lock (_lock) { return _lastDeviation; } } }

        /// <summary>最近一次运动目标位置（毫米），供界面观测。</summary>
        public double LastTargetPos { get { lock (_lock) { return _lastTargetPos; } } }

        #endregion

        #region 构造函数

        /// <summary>构造函数。</summary>
        public SeamTrackingAlgorithm()
        {
            _config = new WeldParamConfig();
            _pending = new List<PVTPoint>(64);
            _faultReason = "";
            _lastOutputTime = DateTime.Now;
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

        /// <summary>读取机器人当前 X 坐标（毫米），异常时回退 0</summary>
        /// <returns>机器人 X 坐标</returns>
        private static double ReadRobotX()
        {
            try
            {
                return WeldParamManager.Instance.CurrentRobotX;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>触发报警并记录原因（同一报警只记一次日志）。</summary>
        /// <param name="reason">报警原因</param>
        private void SetFault(string reason)
        {
            if (_hasFault) return;
            _hasFault = true;
            _faultReason = reason;
            Log(reason, MessageLevel.Error);
        }

        /// <summary>清除报警。</summary>
        private void ClearFault()
        {
            _hasFault = false;
            _faultReason = "";
        }

        /// <summary>单调约束：窗口内只朝一个方向移动，抑制反向微</summary>

        /// <param name="rawDev">原始偏差</param>
        /// <param name="robotX">机器人当前 X 坐标</param>
        /// <returns>约束后的偏差</returns>
        private double ApplyMonotonic(double rawDev, double robotX)
        {
            if (!_monotonicWindowActive
                || Math.Abs(robotX - _monotonicWindowStartX) >= _config.MonotonicWindowMm)
            {
                _monotonicWindowActive = true;
                _monotonicWindowStartX = robotX;
                _monotonicWindowFirstDev = rawDev;
                _monotonicOutput = rawDev;
                _monotonicDirectionLocked = false;
                _monotonicDirection = 0;
                return _monotonicOutput;
            }

            if (!_monotonicDirectionLocked)
            {
                double delta = rawDev - _monotonicWindowFirstDev;
                if (Math.Abs(delta) >= _config.MonotonicDeadbandMm)
                {
                    _monotonicDirection = delta > 0.0 ? 1 : -1;
                    _monotonicDirectionLocked = true;
                }
            }

            if (_monotonicDirectionLocked)
            {
                if (_monotonicDirection > 0)
                    _monotonicOutput = Math.Max(_monotonicOutput, rawDev);
                else
                    _monotonicOutput = Math.Min(_monotonicOutput, rawDev);
            }
            else
            {
                _monotonicOutput = rawDev;
            }

            return _monotonicOutput;
        }

        /// <summary>向待下发点表追加一个 PVT 点，超容量时丢弃最旧点。</summary>
        /// <param name="pos">目标位置（毫米）</param>
        /// <param name="time">到达时间（秒）</param>
        /// <param name="vel">到达速度（毫米/秒）</param>
        private void AddPoint(double pos, double time, double vel)
        {
            if (_pending.Count >= _config.MaxPendingPoints) _pending.RemoveAt(0);
            _pending.Add(new PVTPoint(pos, time, vel));
        }

        /// <summary>处理一帧引导样本：无值容差 → 超限剔除 → 单调约束 →</summary>

        /// <param name="sample">引导样本</param>
        private void ProcessSample(SeamGuideSample sample)
        {
            // 机器人行程推进量（无值跨度累计用）
            double travel = _hasLastRobotX ? (sample.RobotX - _lastRobotX) : 0.0;
            _lastRobotX = sample.RobotX;
            _hasLastRobotX = true;

            double dev;

            if (!sample.Valid)
            {
                // 无有效结果：机器人行程内容差判定
                _noValueSpanMm += Math.Abs(travel);
                if (_noValueSpanMm > _config.NoValueToleranceMm)
                {
                    SetFault(string.Format("线激光无有效结果 机器人行程 {0:F2}mm 超过容差 {1:F2}mm",
                        _noValueSpanMm, _config.NoValueToleranceMm));
                    return;
                }

                // 容差内：偏差记 0，继续移动
                dev = 0.0;
            }
            else
            {
                double rawDev = (sample.FeatureY - _config.CalibratedCenterY) * _config.AxisMmPerLaserMm * _config.GuideSign;
                if (_hasLastAccepted && Math.Abs(rawDev - _lastAcceptedDeviation) > _config.OutlierLimitMm)
                {
                    // 超限点剔除，保持上一有效偏差以维持连续。
                    dev = _lastAcceptedDeviation;
                }
                else
                {
                    _noValueSpanMm = 0.0;
                    dev = ApplyMonotonic(rawDev, sample.RobotX);
                    _lastAcceptedDeviation = rawDev;
                    _hasLastAccepted = true;
                }
            }

            // 运动目标 = 轴当前值 + 带方向偏差
            double target = sample.AxisCurrentPos + dev;

            double dt = (sample.MeasuredFps > 1.0) ? (1.0 / sample.MeasuredFps) : _config.DefaultFrameSeconds;
            if (dt <= 0.0) dt = 1.0 / 60.0;
            double vel = _hasLastPos ? ((target - _lastPos) / dt) : 0.0;
            _lastPos = target;
            _hasLastPos = true;

            _lastDeviation = dev;
            _lastTargetPos = target;

            AddPoint(target, dt, vel);
        }

        /// <summary>把当前待下发点作为一批返回并清空内部缓存。</summary>
        /// <returns>PVT 点集合；无点返回空数组</returns>
        private PVTPoint[] TakePending()
        {
            if (_pending.Count == 0) return new PVTPoint[0];
            PVTPoint[] segment = _pending.ToArray();
            _pending.Clear();
            _lastOutputTime = DateTime.Now;
            return segment;
        }

        #endregion

        #region 公共函数

        /// <summary>更新配置，配置重新加载后调用。</summary>
        /// <param name="config">线激光引导配置</param>
        public void SetConfig(WeldParamConfig config)
        {
            if (config == null) return;
            lock (_lock)
            {
                _config = config;
            }
        }

        /// <summary>载入线激光引导配置。</summary>
        /// <param name="config">配置对象，应为 <see cref="WeldParamConfig"/></param>
        public override void LoadConfig(object config)
        {
            SetConfig(config as WeldParamConfig);
        }

        /// <summary>喂入一帧线激光结果，返回当前应下发的 PVT 点集合。</summary>
        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="measuredFps">实测帧率（Hz），不可用传 0</param>
        /// <param name="axisCurrentPos">引导轴当前位置（毫米）</param>
        /// <returns>达到批次时返回 PVT 点数组，否则返回空数组</returns>
        public PVTPoint[] Feed(LineLaserSeamResult result, double measuredFps, double axisCurrentPos)
        {
            lock (_lock)
            {
                if (!_config.EnableGuide) return new PVTPoint[0];

                SeamGuideSample sample = new SeamGuideSample(DateTime.Now, ReadRobotX(), axisCurrentPos, result, measuredFps);
                ProcessSample(sample);

                if (_pending.Count >= _config.BatchSize)
                {
                    return TakePending();
                }

                return new PVTPoint[0];
            }
        }

        /// <summary>按最长输出间隔强制输出一段轨迹。</summary>
        /// <returns>超间隔且有下发点时返回点数组，否则空数组</returns>
        public PVTPoint[] Tick()
        {
            lock (_lock)
            {
                if (!_config.EnableGuide || _pending.Count == 0) return new PVTPoint[0];
                if ((DateTime.Now - _lastOutputTime).TotalMilliseconds < _config.MaxOutputIntervalMs) return new PVTPoint[0];
                return TakePending();
            }
        }

        /// <summary>排空剩余待下发点。</summary>
        /// <returns>剩余 PVT 点数组</returns>
        public PVTPoint[] Drain()
        {
            lock (_lock)
            {
                return TakePending();
            }
        }

        /// <summary>开始新一段跟踪：清空待下发点与全部修正状态。</summary>
        public override void Reset()
        {
            lock (_lock)
            {
                _pending.Clear();
                _hasLastPos = false;
                _lastPos = 0.0;

                _hasLastRobotX = false;
                _lastRobotX = 0.0;
                _noValueSpanMm = 0.0;

                _hasLastAccepted = false;
                _lastAcceptedDeviation = 0.0;

                _monotonicWindowActive = false;
                _monotonicWindowStartX = 0.0;
                _monotonicWindowFirstDev = 0.0;
                _monotonicOutput = 0.0;
                _monotonicDirection = 0;
                _monotonicDirectionLocked = false;

                _lastDeviation = 0.0;
                _lastTargetPos = 0.0;
                _lastOutputTime = DateTime.Now;

                ClearFault();
            }
        }

        #endregion
    }
}
