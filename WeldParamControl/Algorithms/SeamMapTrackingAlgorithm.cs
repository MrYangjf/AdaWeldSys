﻿using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.WeldParamControl.Abstractions;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>焊缝地图测量点。</summary>
    public struct SeamMapPoint
    {
        #region 公共变量

        /// <summary>焊枪到达该处的 X 绝对坐标（毫米）</summary>
        public double RobotX { get; private set; }

        /// <summary>该 X 处的绝对 Y 轴目标（毫米）：标定轴值 + 特征偏差换算</summary>
        public double TargetY { get; private set; }

        /// <summary>测量时机器人 Y（毫米，滤波后）</summary>
        public double RobotY { get; private set; }

        /// <summary>测量时刻的路径累积距离（毫米，诊断字段）</summary>
        public double PathDistance { get; private set; }

        /// <summary>测量时刻（毫秒计时）</summary>
        public long Timestamp { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>构造一个焊缝地图测量点</summary>
        /// <param name="robotX">焊枪到达该处时的机器人 X 绝对坐标（毫米）</param>
        /// <param name="targetY">该 X 处的绝对 Y 轴目标（毫米）</param>
        /// <param name="robotY">测量时刻的机器人 Y 坐标（毫米）</param>
        /// <param name="pathDistance">路径累积距离（毫米）</param>
        /// <param name="timestamp">测量时刻（毫秒计时）</param>
        public SeamMapPoint(double robotX, double targetY, double robotY, double pathDistance, long timestamp)
        {
            RobotX = robotX;
            TargetY = targetY;
            RobotY = robotY;
            PathDistance = pathDistance;
            Timestamp = timestamp;
        }

        #endregion
    }

    /// <summary>焊缝跟踪报警请求事件参数。</summary>
    public class TrackingAlarmEventArgs : EventArgs
    {
        #region 公共变量

        /// <summary>报警原因（纯文本，无符号）</summary>
        public string Reason { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建报警请求事件参数</summary>
        /// <param name="reason">报警原因</param>
        public TrackingAlarmEventArgs(string reason)
        {
            Reason = reason;
        }

        #endregion
    }

    /// <summary>线激光前置引导焊缝地图跟踪算法实现（非单例）。</summary>
    public class SeamMapTrackingAlgorithm : WeldAlgorithmBase
    {
        #region 常量

        private const string Tag = "焊缝地图跟踪";

        /// <summary>平滑最少点数，不足则保持上一目标</summary>
        private const int MinSmoothPoints = 3;

        /// <summary>统计判定最少参考点数，不足则不判</summary>
        private const int MinOutlierPoints = 3;

        /// <summary>统计判定最小标准差（毫米），低于不判</summary>
        private const double MinOutlierStd = 0.05;

        // 切向判据噪声地板（毫米），偏离小则永不剔
        private const double TangentFloorMm = 0.2;

        // 速度前馈基线（毫米）、EMA 与钳位，防坏点估高
        private const double FeedforwardSlopeRefGapMm = 2.0;
        private const double FeedforwardSlopeAlpha = 0.2;
        private const double FeedforwardSlopeCapMmPerMm = 5.0;

        /// <summary>速度前馈用的局部斜率 EMA 平滑值（毫米/毫米）。</summary>
        private double _ffSlopeFiltered;

        /// <summary>首帧种子偏差上限（毫米），超出即丢</summary>
        private const double MaxSeedDeviationMm = 10.0;

        #endregion

        #region 私有变量

        private readonly object _lock = new object();

        /// <summary>焊缝绝对 Y 地图，按机器人 X 升序排列</summary>
        private readonly List<SeamMapPoint> _seamMap = new List<SeamMapPoint>();

        private WeldParamConfig _config;

        // 线激光侧状态，地图值不再滤波
        private double _lastFeatureY;
        private double _lastDy;
        private double _lastCorrectionMm;
        private double _lastAcceptedRawDy;
        private bool _hasLastAcceptedRawDy;
        private int _consecutiveRejectCount;

        // 单调约束状态（方向锁 + 迟滞释放）
        private double _monoWindowStartX;
        private bool _monoWindowActive;
        private double _monoWindowFirstVal;
        private double _monoOutput;
        private int _monoDirection;
        private bool _monoDirectionLocked;

        // 机器人位姿状态
        private double _robotX;
        private double _robotY;
        private double _robotZ;
        private double _filteredRobotY;
        private bool _hasRobotY;
        private double _lastRobotX;
        private double _lastRobotY;
        private double _lastRobotZ;
        private bool _hasLastPose;

        // X 单调化：地图键只增不减
        private double _monotonicRobotX;
        private bool _hasMonotonicRobotX;
        private double _lastPathMonotonicX;
        private int _jitterRejectCount;

        // 机器人 Y 差分：写入与测量时刻之差
        private double _robotYWriteDiff;

        // 测量时刻轴值，用于还原绝对目标
        private double _axisAtMeasureMm;

        // 目标突变护栏：相邻帧超限即拒
        private double _lastAcceptedTargetY;
        private bool _hasLastAcceptedTargetY;
        private double _lastAcceptedKeyX;
        private bool _hasLastAcceptedKeyX;

        // 路径累积距离（诊断量）
        private double _pathDistance;

        // 焊接方向锁定与折返监测
        private int _weldDirection;
        private double _reverseTravelMm;
        private bool _reverseWarned;

        // 跟踪激活与共享目标
        private bool _isTracking;
        private double _correctionMm;
        private bool _hasCorrection;

        // 死区只管本拍是否更新输出。
        private double _lastOutputCorrection;
        private bool _hasLastOutput;

        // 折返报警
        private bool _hasAlarm;
        private string _alarmReason;

        // 拒绝按判据分源，便于定位原因。
        private int _outlierCount;
        private int _timeRejectCount;
        private int _spaceRejectCount;
        private int _spaceConsecutiveRejectCount;
        private int _targetStepRejectCount;
        private int _invalidFrameCount;
        private int _smoothSkipCount;

        /// <summary>暂停入图 缺值或位姿超龄时置位。</summary>
        private volatile bool _pauseIngest;

        #endregion

        #region 公共变量

        /// <summary>算法名称。</summary>
        public override string Name { get { return "线激光焊缝地图跟踪"; } }

        /// <summary>算法是否已就绪（配置已载入）。</summary>
        public override bool IsReady { get { lock (_lock) { return _config != null; } } }

        /// <summary>当前配置（只读引用，运行期不再改动）。</summary>
        public WeldParamConfig Config { get { lock (_lock) { return _config; } } }

        /// <summary>是否正在跟踪（起焊置位，复位清除）。</summary>
        public bool IsTracking { get { lock (_lock) { return _isTracking; } } }

        /// <summary>暂停入图开关（置位期间有效帧丢弃，不计入地图）。</summary>
        public bool PauseIngest
        {
            get { return _pauseIngest; }
            set { _pauseIngest = value; }
        }

        /// <summary>最近查询修正量 = 绝对目标 − 标定轴值。</summary>
        public double CorrectionMm { get { lock (_lock) { return _correctionMm; } } }

        /// <summary>修正量是否已就绪。</summary>
        public bool HasCorrection { get { lock (_lock) { return _hasCorrection; } } }

        /// <summary>是否已触发折返报警。</summary>
        public bool HasAlarm { get { lock (_lock) { return _hasAlarm; } } }

        /// <summary>折返报警原因（纯文本，无符号）。</summary>
        public string AlarmReason { get { lock (_lock) { return _alarmReason; } } }

        /// <summary>机器人 X 坐标快照（毫米）。</summary>
        public double RobotX { get { lock (_lock) { return _robotX; } } }

        /// <summary>机器人 Y 坐标快照（滤波后，毫米）。</summary>
        public double RobotY { get { lock (_lock) { return _filteredRobotY; } } }

        /// <summary>路径累积距离（毫米，诊断量）。</summary>
        public double PathDistance { get { lock (_lock) { return _pathDistance; } } }

        /// <summary>焊缝地图点数。</summary>
        public int MapPointCount { get { lock (_lock) { return _seamMap.Count; } } }

        /// <summary>累计剔除的野值点数。</summary>
        public int OutlierCount { get { lock (_lock) { return _outlierCount; } } }

        /// <summary>累计突变剔除帧数（含首帧闸门）</summary>
        public int TimeRejectCount { get { lock (_lock) { return _timeRejectCount; } } }

        /// <summary>累计因空间域野值判据剔除的帧数。</summary>
        public int SpaceRejectCount { get { lock (_lock) { return _spaceRejectCount; } } }

        /// <summary>相邻帧超限拒绝计数。</summary>
        public int TargetStepRejectCount { get { lock (_lock) { return _targetStepRejectCount; } } }

        /// <summary>最近一帧对应的跟踪轴实际位置（毫米，诊断量）。</summary>
        public double AxisAtMeasureMm { get { lock (_lock) { return _axisAtMeasureMm; } } }

        /// <summary>最近一次被接受的绝对目标（毫米，诊断量）。</summary>
        public double LastAcceptedTargetY { get { lock (_lock) { return _lastAcceptedTargetY; } } }

        /// <summary>最近入图查图键（毫米），诊断用</summary>
        public double LastMapEntryKeyX { get { lock (_lock) { return _lastAcceptedKeyX; } } }

        /// <summary>是否已有入图点（诊断量，供 CSPinY 记录列）。</summary>
        public bool HasLastMapEntry { get { lock (_lock) { return _hasLastAcceptedKeyX; } } }

        /// <summary>累计无有效识别结果的帧数。</summary>
        public int InvalidFrameCount { get { lock (_lock) { return _invalidFrameCount; } } }

        /// <summary>累计因地图点不足而跳过反算的次数。</summary>
        public int SmoothSkipCount { get { lock (_lock) { return _smoothSkipCount; } } }

        /// <summary>入图等效偏差 = 绝对目标 − 测量轴值</summary>
        public double LastDy { get { lock (_lock) { return _lastDy; } } }

        /// <summary>特征偏差修正量（毫米），诊断用</summary>
        public double LastCorrectionMm { get { lock (_lock) { return _lastCorrectionMm; } } }

        /// <summary>最近一帧线激光特征点 Y（毫米，线激光坐标，诊断用）。</summary>
        public double LastFeatureY { get { lock (_lock) { return _lastFeatureY; } } }

        /// <summary>机器人 Y 差分 = 写入 − 测量时值</summary>
        public double PathYOffset { get { lock (_lock) { return _robotYWriteDiff; } } }

        /// <summary>累计吞掉的抖动级 X 回退帧数</summary>
        public int JitterRejectCount { get { lock (_lock) { return _jitterRejectCount; } } }

        /// <summary>折返报警请求事件。</summary>
        public event EventHandler<TrackingAlarmEventArgs> TrackingAlarmRequested;

        #endregion

        #region 构造函数

        /// <summary>构造函数。</summary>
        public SeamMapTrackingAlgorithm()
        {
            _config = new WeldParamConfig();
            _alarmReason = "";
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

        /// <summary>清除折返报警。</summary>
        private void ClearAlarm()
        {
            _hasAlarm = false;
            _alarmReason = "";
        }

        /// <summary>触发折返报警请求事件（在锁外调用，避免订阅方</summary>

        /// <param name="reason">报警原因</param>
        private void RaiseAlarmRequest(string reason)
        {
            EventHandler<TrackingAlarmEventArgs> handler = TrackingAlarmRequested;
            if (handler == null) return;
            try
            {
                handler(this, new TrackingAlarmEventArgs(reason));
            }
            catch (Exception ex)
            {
                Log("折返报警通知异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>处理一帧线激光结果：换算→剔除→合成入图</summary>

        /// <param name="featureY">焊缝特征点横向坐标（毫米，线激光坐标）</param>
        /// <param name="axisPosMm">本帧对应的跟踪轴实际位置（毫米）</param>
        private void ProcessLaserResult(double featureY, double axisPosMm)
        {
            // ① 特征偏差换算为带方向轴毫米
            double rawDy = (featureY - _config.CalibratedCenterY) * _config.AxisMmPerLaserMm * _config.GuideSign;
            _lastFeatureY = featureY;

            // ② 首帧种子闸门：超出物理范围即失败
            if (!_hasLastAcceptedRawDy && Math.Abs(rawDy) > MaxSeedDeviationMm)
            {
                _outlierCount++;
                _timeRejectCount++;
                return;
            }

            // ③ 时间域突变剔除：超单帧最大变化即丢
            // （线激光边缘是连续的，单帧不可能跳变几十丝；突变多为识别失败或通信毛刺）
            if (_hasLastAcceptedRawDy && _config.MaxStepChangeMm > 0.0
                && Math.Abs(rawDy - _lastAcceptedRawDy) > _config.MaxStepChangeMm)
            {
                _outlierCount++;
                _timeRejectCount++;
                _consecutiveRejectCount++;

                // 连续拒绝护栏：真实台阶须放行。
                // 真实台阶会持续超限。达上限即放行并重置种子。
                if (_config.ConsecutiveRejectFrames <= 0 || _consecutiveRejectCount < _config.ConsecutiveRejectFrames)
                {
                    return;
                }

                Log(string.Format("线激光偏差连续 {0} 帧被拒 判定为真实台阶 接受新水平 {1:F3}mm",
                    _consecutiveRejectCount, rawDy), MessageLevel.Warning);
                _consecutiveRejectCount = 0;
            }

            // ④ 该 X 处的绝对 Y 轴目标 = 测量时刻轴实时值 Ya1 + 特征偏差 rawDy；
            //    全式：Yt=(特征Y−标定Y)×方向+轴Y+机器人Y，Ycsp=Yt−机器人Y。
            //    特征偏差含 −ΔYa，加回 Ya1 相消；
            //    写入侧差分恰好实现 ±机器人Y 项。
            // 特征读数须随轴反向变化
            //    退化为「目标 = 轴延迟副本 + 常量」自指环，真机不复制。
            // 偏差与合成结果不做低通滤波
            //    · 滤 rawDy 会引入轴速反馈，拐点必冲过头。
            //    · 滤合成结果 ⇒ 入图值 = 真值 − τ·v_缝，斜段恒滞后。
            //    抑噪由查图侧高斯与三道闸门承担。
            _axisAtMeasureMm = axisPosMm;
            double targetY = axisPosMm + rawDy;
            // _lastDy 维持等效偏差诊断语义
            _lastDy = targetY - axisPosMm;

            // ⑤b 目标突变护栏：相邻帧超限即拒
            // 与 ③ 的分工：③ 判特征偏差帧间跳变（毫秒级、单分量）；本条判合成后的绝对目标（汇总全部来源）。
            if (_hasLastAcceptedTargetY && _config.MaxTargetStepMm > 0.0
                && Math.Abs(targetY - _lastAcceptedTargetY) > _config.MaxTargetStepMm)
            {
                _outlierCount++;
                _timeRejectCount++;
                _targetStepRejectCount++;
                _consecutiveRejectCount++;

                if (_config.ConsecutiveRejectFrames <= 0 || _consecutiveRejectCount < _config.ConsecutiveRejectFrames)
                {
                    return;
                }

                Log(string.Format("最终目标连续 {0} 帧被拒 判定为真实台阶 接受新水平 {1:F3}mm",
                    _consecutiveRejectCount, targetY), MessageLevel.Warning);
                _consecutiveRejectCount = 0;
            }

            // ⑥ 查图键 = 测量 X（单调化）+ 前置量
            double arriveX = _monotonicRobotX + _config.LeadDistanceMm;

            // ⑦ 空间域野值检测：炸点不污染地图
            // 切向判据只拦孤立飞点，连续曲线不剔。
            if (IsOutlier(targetY, arriveX))
            {
                _outlierCount++;
                _spaceRejectCount++;
                _spaceConsecutiveRejectCount++;
                // 真实台阶持续偏离，达上限即放行
                if (_config.ConsecutiveRejectFrames <= 0 || _spaceConsecutiveRejectCount < _config.ConsecutiveRejectFrames)
                {
                    return;
                }
                Log(string.Format("空间域连续 {0} 帧被拒 判定为真实台阶 接受新水平 {1:F3}mm",
                    _spaceConsecutiveRejectCount, targetY), MessageLevel.Warning);
                _spaceConsecutiveRejectCount = 0;
            }
            else
            {
                _spaceConsecutiveRejectCount = 0;
            }

            // ⑧ 存图并记测量时机器人 Y。
            InsertMapPoint(arriveX, targetY, _filteredRobotY, _pathDistance, Environment.TickCount);
            _lastAcceptedRawDy = rawDy;
            _hasLastAcceptedRawDy = true;
            _lastAcceptedTargetY = targetY;
            _hasLastAcceptedTargetY = true;
            _lastAcceptedKeyX = arriveX;
            _hasLastAcceptedKeyX = true;
            _consecutiveRejectCount = 0;
        }

        /// <summary>野值检测：切线连续性违约或 3σ 统计异常即判为炸</summary>

        /// <param name="newTargetY">待判定的绝对 Y 轴目标（毫米）</param>
        /// <param name="arriveX">待判定点的查图键（毫米）</param>
        /// <returns>是炸点返回 true</returns>
        private bool IsOutlier(double newTargetY, double arriveX)
        {
            // ① 切线连续性：取尾部两点作切线
            if (_seamMap.Count >= 2)
            {
                SeamMapPoint p1 = _seamMap[_seamMap.Count - 1];
                SeamMapPoint p2 = _seamMap[_seamMap.Count - 2];
                double gap = arriveX - p1.RobotX;
                double refGap = p1.RobotX - p2.RobotX;
                if (gap > 0.0 && refGap > 0.0)
                {
                    double slope = (p1.TargetY - p2.TargetY) / refGap;
                    double predicted = p1.TargetY + slope * gap;
                    double limit = TangentFloorMm + _config.OutlierSlopeLimitMmPerMm * gap;
                    if (Math.Abs(newTargetY - predicted) > limit)
                    {
                        return true;
                    }
                }
            }

            // ② 3σ 统计异常：针对噪声簇漂移
            double window = _config.NearbyWindowMm;
            if (window <= 0.0) return false;

            int count = 0;
            double sum = 0.0;
            for (int i = 0; i < _seamMap.Count; i++)
            {
                if (Math.Abs(_seamMap[i].RobotX - arriveX) >= window) continue;
                sum += _seamMap[i].TargetY;
                count++;
            }

            if (count < MinOutlierPoints) return false;

            double mean = sum / count;
            double variance = 0.0;
            for (int i = 0; i < _seamMap.Count; i++)
            {
                double distance = Math.Abs(_seamMap[i].RobotX - arriveX);
                if (distance >= window) continue;

                double diff = _seamMap[i].TargetY - mean;
                variance += diff * diff;
            }

            double std = Math.Sqrt(variance / count);
            return std > MinOutlierStd
                && Math.Abs(newTargetY - mean) > _config.OutlierSigmaK * std;
        }

        /// <summary>把测量点按机器人 X 升序插入焊缝地图，超容量时</summary>

        /// <param name="robotX">焊枪到达该处时的机器人 X 绝对坐标（毫米）</param>
        /// <param name="targetY">该 X 处的绝对 Y 轴目标（毫米）</param>
        /// <param name="robotY">测量时刻的机器人 Y 坐标（毫米）</param>
        /// <param name="pathDistance">路径累积距离（毫米）</param>
        /// <param name="timestamp">测量时刻（毫秒计时）</param>
        private void InsertMapPoint(double robotX, double targetY, double robotY, double pathDistance, long timestamp)
        {
            // 由尾部向前定位插入位，正常前进时命中尾部即 O(1)
            int insertAt = 0;
            for (int i = _seamMap.Count - 1; i >= 0; i--)
            {
                if (_seamMap[i].RobotX <= robotX)
                {
                    insertAt = i + 1;
                    break;
                }
            }

            _seamMap.Insert(insertAt, new SeamMapPoint(robotX, targetY, robotY, pathDistance, timestamp));

            if (_config.MaxMapPoints > 0 && _seamMap.Count > _config.MaxMapPoints)
            {
                _seamMap.RemoveRange(0, _seamMap.Count - _config.MaxMapPoints);
            }
        }

        /// <summary>按焊枪 X 取该处的绝对 Y 轴目标与测量时刻机器人 Y（</summary>

        /// <param name="robotX">当前机器人 X 坐标（毫米）</param>
        /// <param name="targetY">该 X 处的绝对 Y 轴目标（毫米）</param>
        /// <param name="pointRobotY">窗口内加权平均的测量时刻机器人 Y（毫米）</param>
        /// <returns>点足够并完成平滑返回 true，点不足返回 false</returns>
        private bool QueryTargetY(double robotX, out double targetY, out double pointRobotY)
        {
            targetY = 0.0;
            pointRobotY = 0.0;

            double sigma = _config.GaussianSigmaMm;
            double halfWindow = _config.GaussianHalfWindowMm;
            if (sigma <= 0.0 || halfWindow <= 0.0) return false;

            double denominator = 2.0 * sigma * sigma;
            double sumWeight = 0.0;
            double sumTarget = 0.0;
            double sumRobotY = 0.0;
            int count = 0;

            for (int i = 0; i < _seamMap.Count; i++)
            {
                double delta = _seamMap[i].RobotX - robotX;
                if (delta > halfWindow || delta < -halfWindow) continue;

                double weight = Math.Exp(-(delta * delta) / denominator);
                sumWeight += weight;
                sumTarget += weight * _seamMap[i].TargetY;
                sumRobotY += weight * _seamMap[i].RobotY;
                count++;
            }

            if (count < MinSmoothPoints || sumWeight <= 0.0)
            {
                _smoothSkipCount++;
                return false;
            }

            targetY = sumTarget / sumWeight;
            pointRobotY = sumRobotY / sumWeight;
            return true;
        }

        /// <summary>单调约束：锁向后单向移动，反向超阈值解锁</summary>

        /// <param name="rawValue">高斯平滑后的修正量（毫米）</param>
        /// <param name="robotX">当前机器人 X 坐标（毫米）</param>
        /// <returns>单调约束后的修正量（毫米）</returns>
        private double ApplyMonotonic(double rawValue, double robotX)
        {
            // 持距兜底：超过上限强制重新种子
            if (!_monoWindowActive
                || (_config.MonotonicWindowMm > 0.0
                    && Math.Abs(robotX - _monoWindowStartX) >= _config.MonotonicWindowMm))
            {
                ReseedMonotonic(rawValue, robotX);
                return _monoOutput;
            }

            if (!_monoDirectionLocked)
            {
                // 未锁态：同向偏离超死区即锁向
                double delta = rawValue - _monoWindowFirstVal;
                if (Math.Abs(delta) < _config.MonotonicDeadbandMm)
                {
                    _monoOutput = rawValue;
                    return _monoOutput;
                }

                _monoDirection = delta > 0.0 ? 1 : -1;
                _monoDirectionLocked = true;
            }

            // 已锁态：反向偏离超阈值即解锁
            double reverse = _monoDirection > 0 ? (_monoOutput - rawValue) : (rawValue - _monoOutput);
            if (_config.MonotonicReleaseMm > 0.0 && reverse > _config.MonotonicReleaseMm)
            {
                ReseedMonotonic(rawValue, robotX);
                return _monoOutput;
            }

            if (_monoDirection > 0)
                _monoOutput = Math.Max(_monoOutput, rawValue);
            else
                _monoOutput = Math.Min(_monoOutput, rawValue);

            return _monoOutput;
        }

        /// <summary>重新种子方向锁：以当前值为新参考并清除锁定</summary>

        /// <param name="rawValue">当前修正量（毫米）</param>
        /// <param name="robotX">当前机器人 X 坐标（毫米）</param>
        private void ReseedMonotonic(double rawValue, double robotX)
        {
            _monoWindowActive = true;
            _monoWindowStartX = robotX;
            _monoWindowFirstVal = rawValue;
            _monoOutput = rawValue;
            _monoDirectionLocked = false;
            _monoDirection = 0;
        }

        /// <summary>焊接方向锁定与 X 折返监测：偶发折返仅警告，连续</summary>

        /// <param name="dx">本包机器人 X 位移（毫米）</param>
        /// <returns>本次调用新触发报警返回 true</returns>
        private bool UpdateDirection(double dx)
        {
            if (Math.Abs(dx) < _config.ReverseDeadbandMm) return false;

            int sign = dx > 0.0 ? 1 : -1;

            if (_weldDirection == 0)
            {
                // 首段有效位移锁定焊接前进方向
                _weldDirection = sign;
                return false;
            }

            if (sign == _weldDirection)
            {
                if (_reverseTravelMm > 0.0)
                {
                    Log(string.Format("机器人 X 折返恢复正向 本次累计逆向 {0:F2}mm", _reverseTravelMm));
                }
                _reverseTravelMm = 0.0;
                _reverseWarned = false;
                return false;
            }

            _reverseTravelMm += Math.Abs(dx);

            if (_reverseTravelMm > _config.ReverseAlarmMm)
            {
                if (_hasAlarm) return false;
                _hasAlarm = true;
                _alarmReason = string.Format("机器人 X 折返 累计 {0:F2}mm 超过阈值 {1:F2}mm 疑似复焊",
                    _reverseTravelMm, _config.ReverseAlarmMm);
                Log(_alarmReason, MessageLevel.Error);
                return true;
            }

            if (!_reverseWarned)
            {
                _reverseWarned = true;
                Log(string.Format("机器人 X 偶发折返 累计 {0:F2}mm 未超阈值 {1:F2}mm 仅警告继续跟踪",
                    _reverseTravelMm, _config.ReverseAlarmMm), MessageLevel.Warning);
            }

            return false;
        }

        /// <summary>处理一帧机器人位姿：Y 滤波 + X 单调化 + 路径距离 + 折返</summary>

        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        /// <returns>本次调用新触发报警返回 true</returns>
        private bool ProcessRsiPose(double robotX, double robotY, double robotZ)
        {
            FilterRobotY(robotY);

            // 首帧位姿只建立基准，不累积距离也不参与查图
            if (CachePose(robotX, robotY, robotZ)) return false;

            return TrackPathDistance(robotX, robotY, robotZ);
        }

        /// <summary>机器人 Y 低通并建立起焊基准</summary>
        /// <param name="robotY">本帧机器人 Y 坐标（毫米）</param>
        private void FilterRobotY(double robotY)
        {
            double beta = Math.Max(0.0, Math.Min(1.0, _config.RobotYFilterAlpha));
            if (!_hasRobotY)
            {
                _filteredRobotY = robotY;
                _hasRobotY = true;
            }
            else
            {
                _filteredRobotY = _filteredRobotY * (1.0 - beta) + robotY * beta;
            }
        }

        /// <summary>缓存本帧位姿并把 X 单调化（地图键只增不减）</summary>
        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        /// <returns>首帧（只建基准、不回算目标）返回 true</returns>
        private bool CachePose(double robotX, double robotY, double robotZ)
        {
            _robotX = robotX;
            _robotY = robotY;
            _robotZ = robotZ;

            // X 单调化：抖动回退不推进键；
            // 真实回退也不倒退键，仅累计报警
            if (!_hasMonotonicRobotX)
            {
                _monotonicRobotX = robotX;
                _hasMonotonicRobotX = true;
            }
            else if (robotX > _monotonicRobotX)
            {
                _monotonicRobotX = robotX;
            }
            else if (_monotonicRobotX - robotX <= _config.ReverseDeadbandMm)
            {
                _jitterRejectCount++;
            }

            if (_hasLastPose) return false;

            _hasLastPose = true;
            _lastRobotX = robotX;
            _lastRobotY = robotY;
            _lastRobotZ = robotZ;
            _lastPathMonotonicX = _monotonicRobotX;
            return true;
        }

        /// <summary>累积路径距离并更新方向与折返监测</summary>
        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        /// <returns>本次触发折返报警返回 true</returns>
        private bool TrackPathDistance(double robotX, double robotY, double robotZ)
        {
            double dx = robotX - _lastRobotX;
            double dy = robotY - _lastRobotY;
            double dz = robotZ - _lastRobotZ;

            // 三维路径距离累积，适应空间曲线焊缝，仅作诊断；X 项取单调化增量，回退不重复计入
            double pathDx = _monotonicRobotX - _lastPathMonotonicX;
            _pathDistance += Math.Sqrt(pathDx * pathDx + dy * dy + dz * dz);
            _lastPathMonotonicX = _monotonicRobotX;
            bool alarmRaised = UpdateDirection(dx);

            _lastRobotX = robotX;
            _lastRobotY = robotY;
            _lastRobotZ = robotZ;
            return alarmRaised;
        }

        /// <summary>以位姿预测器的外推 X 刷新地图键基准。</summary>

        /// <param name="robotX">位姿预测器给出的外推机器人 X（毫米）</param>
        public void UpdatePredictedRobotX(double robotX)
        {
            if (!_hasMonotonicRobotX) return;   // 未建基准时不抢建，仍由喂入首帧建立
            if (robotX > _monotonicRobotX) _monotonicRobotX = robotX;
        }

        /// <summary>清除折返报警态（对外：会话切换 / 异常清理时调用）。</summary>
        public void ClearAlarmState()
        {
            lock (_lock)
            {
                ClearAlarm();
            }
        }

        #endregion

        #region 公共函数

        /// <summary>更新配置，配置重新加载后调用。</summary>
        /// <param name="config">焊接算法配置</param>
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

        /// <summary>复位跟踪状态：清图、清位姿基准、清方向与报警，保</summary>
        public override void Reset()
        {
            lock (_lock)
            {
                _seamMap.Clear();

                _lastFeatureY = 0.0;
                _lastDy = 0.0;
                _lastCorrectionMm = 0.0;
                _lastAcceptedRawDy = 0.0;
                _hasLastAcceptedRawDy = false;
                _consecutiveRejectCount = 0;

                _monoWindowStartX = 0.0;
                _monoWindowActive = false;
                _monoWindowFirstVal = 0.0;
                _monoOutput = 0.0;
                _monoDirection = 0;
                _monoDirectionLocked = false;

                _robotX = 0.0;
                _robotY = 0.0;
                _robotZ = 0.0;
                _filteredRobotY = 0.0;
                _hasRobotY = false;
                _lastRobotX = 0.0;
                _lastRobotY = 0.0;
                _lastRobotZ = 0.0;
                _hasLastPose = false;

                _pathDistance = 0.0;

                _weldDirection = 0;
                _reverseTravelMm = 0.0;
                _reverseWarned = false;

                _monotonicRobotX = 0.0;
                _hasMonotonicRobotX = false;
                _lastPathMonotonicX = 0.0;
                _jitterRejectCount = 0;
                _robotYWriteDiff = 0.0;
                _axisAtMeasureMm = 0.0;
                _lastAcceptedTargetY = 0.0;
                _hasLastAcceptedTargetY = false;
                _lastAcceptedKeyX = 0.0;
                _hasLastAcceptedKeyX = false;

                _isTracking = false;
                _correctionMm = 0.0;
                _hasCorrection = false;
                _lastOutputCorrection = 0.0;
                _hasLastOutput = false;

                // 复位须一并清折返报警。
                // 上层「模组报警」判定长期为真
                ClearAlarm();

                _outlierCount = 0;
                _timeRejectCount = 0;
                _spaceRejectCount = 0;
                _spaceConsecutiveRejectCount = 0;
                _ffSlopeFiltered = 0.0;
                _targetStepRejectCount = 0;
                _invalidFrameCount = 0;
                _smoothSkipCount = 0;
                _pauseIngest = false;
            }
        }

        /// <summary>起焊沿激活目标查询（给绝对 Y 目标）</summary>
        public void BeginCorrection()
        {
            lock (_lock)
            {
                _correctionMm = 0.0;
                _hasCorrection = true;
                _isTracking = true;
            }
        }

        /// <summary>按机器人 X 向速度取 Y 轴跟踪速度需求</summary>

        /// <param name="robotVxMmPerS">机器人 X 向速度幅值（毫米/秒，调用方由 X 差分推算）</param>
        /// <param name="requiredVelMmPerS">Y 轴速度需求幅值（毫米/秒）</param>
        /// <returns>可估计斜率且速度为正返回 true</returns>
        public bool TryGetRequiredAxisVelocityMmPerS(double robotVxMmPerS, out double requiredVelMmPerS)
        {
            requiredVelMmPerS = 0.0;
            if (robotVxMmPerS <= 0.0) return false;
            lock (_lock)
            {
                if (_seamMap.Count < 2) return false;
                SeamMapPoint p1 = _seamMap[_seamMap.Count - 1];
                // SeamMapPoint 是 struct（值类型），禁 = null / == null（CS0019）——命中与否用布尔旗标承载
                bool refFound = false;
                SeamMapPoint p2 = default(SeamMapPoint);
                int scan = Math.Min(_seamMap.Count - 1, 256);
                for (int i = _seamMap.Count - 2; i >= _seamMap.Count - 1 - scan; i--)
                {
                    if (p1.RobotX - _seamMap[i].RobotX >= FeedforwardSlopeRefGapMm)
                    {
                        p2 = _seamMap[i];
                        refFound = true;
                        break;
                    }
                }
                if (!refFound) return false;
                double slope = (p1.TargetY - p2.TargetY) / (p1.RobotX - p2.RobotX);
                if (slope > FeedforwardSlopeCapMmPerMm) slope = FeedforwardSlopeCapMmPerMm;
                else if (slope < -FeedforwardSlopeCapMmPerMm) slope = -FeedforwardSlopeCapMmPerMm;
                _ffSlopeFiltered += (slope - _ffSlopeFiltered) * FeedforwardSlopeAlpha;
                requiredVelMmPerS = Math.Abs(_ffSlopeFiltered) * robotVxMmPerS;
                return true;
            }
        }

        /// <summary>按当拍位姿取绝对 Y 轴目标（写入时刻调用）</summary>

        /// <param name="robotX">当拍机器人 X 坐标（毫米）</param>
        /// <param name="robotY">当拍机器人 Y 坐标（毫米）</param>
        /// <param name="targetY">绝对 Y 轴目标（毫米）；无可用地图点时取标定轴值</param>
        /// <returns>返回 false 时保持标定轴值</returns>
        public bool TryGetTargetY(double robotX, double robotY, out double targetY)
        {
            lock (_lock)
            {
                targetY = _config.TeachLockYMm;
                if (!_isTracking) return false;

                if (!QueryTargetY(robotX, out double smoothedTarget, out double pointRobotY)) return false;

                // 单调约束：方向锁 + 迟滞释放。
                if (_config.EnableMonotonicConstraint)
                {
                    smoothedTarget = ApplyMonotonic(smoothedTarget, robotX);
                }

                // 机器人 Y 差分补偿，跟踪轴反向抵消。
                _robotYWriteDiff = robotY - pointRobotY;
                smoothedTarget -= _config.RobotYGain * _robotYWriteDiff;

                // 死区：小于即保持输出（0.01mm）。
                // 偏差按固定基准判定
                // 跳幅可达整段量级，写出链路无护栏。
                // 无迟滞，边界抖动会来回跳。
                if (_hasLastOutput && _config.OutputDeadbandMm > 0.0
                    && Math.Abs(smoothedTarget - _lastOutputCorrection) < _config.OutputDeadbandMm)
                {
                    smoothedTarget = _lastOutputCorrection;
                }

                targetY = smoothedTarget;
                _lastOutputCorrection = smoothedTarget;
                _hasLastOutput = true;
                _lastCorrectionMm = smoothedTarget - _config.TeachLockYMm;
                _correctionMm = _lastCorrectionMm;
                _hasCorrection = true;
                return true;
            }
        }

        /// <summary>喂入一帧线激光结果（相机 SDK 回调线程，约 60Hz）。</summary>

        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="axisPosMm">本帧对应的跟踪轴实际位置（毫米）</param>
        public void OnLaserResult(LineLaserSeamResult result, double axisPosMm)
        {
            lock (_lock)
            {
                if (!_isTracking) return;
                if (_pauseIngest)
                {
                    // 缺值或位姿超龄：本帧不入图，防错键
                    _invalidFrameCount++;
                    return;
                }
                if (!result.Valid)
                {
                    _invalidFrameCount++;
                    return;
                }
                if (!_hasRobotY) return;

                ProcessLaserResult(result.FeatureY, axisPosMm);
            }
        }

        /// <summary>喂入一帧机器人 RSI 位姿（RSI 接收线程，约 83Hz）。</summary>
        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        public void OnRsiPose(double robotX, double robotY, double robotZ)
        {
            bool alarmRaised;
            string reason;
            lock (_lock)
            {
                if (!_isTracking) return;
                alarmRaised = ProcessRsiPose(robotX, robotY, robotZ);
                reason = _alarmReason;
            }

            if (alarmRaised) RaiseAlarmRequest(reason);
        }

        #endregion
    }
}

