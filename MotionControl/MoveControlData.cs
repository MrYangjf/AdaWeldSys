using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm;
using AdaWeldSystem.EmguALG.EmguConfiger;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>运控数据：线激光标定参数 + 台达运控配置 + 水平移动距离缓存</summary>
    /// <remarks>
    /// 本类为普通实例类，由 <see cref="MontionManager"/> 在构造时创建并持有，
    /// 全系统唯一入口为 MontionManager.Instance.MoveData。
    /// 标定参数不在本类另存副本，一律转发到 MotionConfig 的 CALIBRATION 节，避免双份数据源不同步。
    /// 持久化统一走 Config\XML\DeltaMotionConfig.xml，原 MoveControlData.ini 已弃用。
    /// </remarks>
    public class MoveControlData
    {
        #region 私有变量

        private const string Tag = "运控调整";

        private readonly object _cacheLock = new object();
        private readonly Dictionary<double, double> _horizontalMoveCache = new Dictionary<double, double>();

        #endregion

        #region 公共变量

        /// <summary>台达运控配置，标定与运控参数的唯一持久化载体</summary>
        public DeltaMotionConfig MotionConfig { get; private set; }

        /// <summary>线激光前置标定距离（mm）：线激光相对焊接头的轴向前置安装距离</summary>
        public double LaserFrontOffset
        {
            get { return MotionConfig.LaserFrontOffset; }
            set { MotionConfig.LaserFrontOffset = value; }
        }

        /// <summary>线激光与水平轴的夹角（弧度），由角度换算得到</summary>
        public double LaserToHorizontalAngleRad
        {
            get { return MotionConfig.LaserToHorizontalAngleDeg * Math.PI / 180.0; }
            set { MotionConfig.LaserToHorizontalAngleDeg = value * 180.0 / Math.PI; }
        }

        /// <summary>线激光与水平轴的角度（度），供 UI 显示与设置，写入时同步换算为弧度</summary>
        public double LaserToHorizontalAngleDeg
        {
            get { return MotionConfig.LaserToHorizontalAngleDeg; }
            set { MotionConfig.LaserToHorizontalAngleDeg = value; }
        }

        /// <summary>线激光 X 1mm 对应水平轴距离（标定系数，例如 0.98 表示线激光 1mm = 水平轴 0.98mm）</summary>
        public double LaserToHorizontalScaleX
        {
            get { return MotionConfig.LaserToHorizontalScaleX; }
            set { MotionConfig.LaserToHorizontalScaleX = value; }
        }

        /// <summary>标定的焊缝中心点 X（线激光图像坐标，mm）</summary>
        public double CalibratedCenterX
        {
            get { return MotionConfig.CalibratedCenterX; }
            set { MotionConfig.CalibratedCenterX = value; }
        }

        /// <summary>标定的焊缝中心点 Y（线激光图像坐标，mm）</summary>
        public double CalibratedCenterY
        {
            get { return MotionConfig.CalibratedCenterY; }
            set { MotionConfig.CalibratedCenterY = value; }
        }

        /// <summary>趋近匹配阈值（mm）：实际机器人 X 与缓存键差值小于该值时，触发水平轴移动</summary>
        public double ApproachThreshold
        {
            get { return MotionConfig.ApproachThreshold; }
            set { MotionConfig.ApproachThreshold = value; }
        }

        #endregion

        #region 构造函数

        /// <summary>创建运控数据并载入配置</summary>
        public MoveControlData()
        {
            MotionConfig = new DeltaMotionConfig();
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #endregion

        #region 水平移动距离算法

        /// <summary>计算水平移动距离（简化投影模型：delta × cos(angle) × scaleX）。</summary>
        /// <param name="seamCenterX">焊缝中心 X（线激光坐标，mm）</param>
        /// <param name="seamCenterY">焊缝中心 Y（线激光坐标，mm）</param>
        /// <returns>水平轴移动距离（mm）</returns>
        public double ComputeHorizontalMove(double seamCenterX, double seamCenterY)
        {
            double delta = seamCenterX - CalibratedCenterX;
            double horizontalMove = delta * Math.Cos(LaserToHorizontalAngleRad) * LaserToHorizontalScaleX;
            return horizontalMove;
        }

        /// <summary>缓存水平移动距离，键为 robotX + 前置标定距离。</summary>
        /// <param name="robotX">机器人 X 坐标</param>
        /// <param name="moveDistance">水平轴移动距离</param>
        public void CacheHorizontalMove(double robotX, double moveDistance)
        {
            double key = robotX + LaserFrontOffset;
            lock (_cacheLock)
            {
                _horizontalMoveCache[key] = moveDistance;
            }
        }

        /// <summary>计算并缓存水平移动距离，供真实/模拟流程统一调用。</summary>
        /// <param name="seamFeature">焊缝特征</param>
        /// <param name="robotX">机器人 X 坐标</param>
        public void ComputeAndCacheHorizontalMove(SeamFeatureResult seamFeature, double robotX)
        {
            if (seamFeature == null) return;
            double move = ComputeHorizontalMove(seamFeature.CenterX, seamFeature.CenterY);
            CacheHorizontalMove(robotX, move);
        }

        /// <summary>查询最接近的水平移动缓存项，用于趋近触发。</summary>
        /// <param name="robotX">当前机器人 X 坐标</param>
        /// <param name="moveDistance">输出匹配的移动距离</param>
        /// <param name="matchedKey">输出匹配的缓存键</param>
        /// <returns>是否找到匹配项</returns>
        public bool TryGetNearestHorizontalMove(double robotX, out double moveDistance, out double matchedKey)
        {
            moveDistance = 0.0;
            matchedKey = 0.0;
            lock (_cacheLock)
            {
                double bestDiff = double.MaxValue;
                double bestKey = 0.0;
                bool found = false;
                foreach (var kv in _horizontalMoveCache)
                {
                    double diff = Math.Abs(kv.Key - robotX);
                    if (diff <= ApproachThreshold && diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestKey = kv.Key;
                        found = true;
                    }
                }
                if (found)
                {
                    moveDistance = _horizontalMoveCache[bestKey];
                    matchedKey = bestKey;
                    return true;
                }
            }
            return false;
        }

        /// <summary>移除一条缓存（水平轴移动已执行后调用，避免重复触发）</summary>
        /// <param name="key">缓存键</param>
        public void RemoveHorizontalMove(double key)
        {
            lock (_cacheLock)
            {
                if (_horizontalMoveCache.ContainsKey(key))
                {
                    _horizontalMoveCache.Remove(key);
                }
            }
        }

        /// <summary>清空全部水平移动缓存</summary>
        public void ClearHorizontalMoveCache()
        {
            lock (_cacheLock)
            {
                _horizontalMoveCache.Clear();
            }
        }

        #endregion

        #region 配置持久化

        /// <summary>从 XML 加载标定与运控配置，文件缺失时保留默认值</summary>
        public void Load()
        {
            try
            {
                MotionConfig.LoadConfig();
                Log(string.Format("运控配置已加载 前置距离 {0:F3} 角度 {1:F3} 度",
                    LaserFrontOffset, LaserToHorizontalAngleDeg));
            }
            catch (Exception ex)
            {
                Log("运控配置加载异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>保存标定与运控配置到 XML 文件</summary>
        public void Save()
        {
            try
            {
                MotionConfig.SaveConfig();
                Log("运控配置已保存");
            }
            catch (Exception ex)
            {
                Log("运控配置保存异常 " + ex.Message, MessageLevel.Error);
            }
        }

        #endregion
    }
}
