using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using AdaWeldSystem.Comm;
using AdaWeldSystem.EmguALG.EmguConfiger;


namespace AdaWeldSystem.MotionControl
{
    /// <summary>
    /// 运控调整数据单例：线激光前置标定参数 + 水平移动距离缓存
    /// </summary>
    public class MoveControlData
    {
        private const string Tag = "运控调整";

        /// <summary>统一日志出口</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region 标定参数

        /// <summary>线激光前置标定距离（mm）：线激光相对焊接头的轴向前置安装距离</summary>
        public double LaserFrontOffset { get; set; }

        /// <summary>线激光与水平轴的夹角（弧度，由角度换算得到）</summary>
        public double LaserToHorizontalAngleRad { get; set; }

        /// <summary>线激光与水平轴的角度（度），供 UI 显示/设置，写入时同步换算为弧度</summary>
        public double LaserToHorizontalAngleDeg
        {
            get { return LaserToHorizontalAngleRad * 180.0 / Math.PI; }
            set { LaserToHorizontalAngleRad = value * Math.PI / 180.0; }
        }

        /// <summary>线激光 X 1mm 对应水平轴距离（标定系数，例如 0.98 表示线激光 1mm = 水平轴 0.98mm）</summary>
        public double LaserToHorizontalScaleX { get; set; }

        /// <summary>标定的焊缝中心点 X（线激光图像坐标，mm）</summary>
        public double CalibratedCenterX { get; set; }

        /// <summary>标定的焊缝中心点 Y（线激光图像坐标，mm）</summary>
        public double CalibratedCenterY { get; set; }

        #endregion

        #region 水平移动距离缓存

        private readonly object _cacheLock = new object();
        private readonly Dictionary<double, double> _horizontalMoveCache = new Dictionary<double, double>();

        /// <summary>趋近匹配阈值（mm）：实际机器人X 与缓存键差值小于该值时，触发水平轴移动</summary>
        public double ApproachThreshold { get; set; }

        #endregion

        private static readonly Lazy<MoveControlData> _lazy =
            new Lazy<MoveControlData>(() => new MoveControlData());

        /// <summary>单例实例</summary>
        public static MoveControlData Instance => _lazy.Value;

        private MoveControlData()
        {
            // ADR-002 兼容：不使用 auto-property initializer，值在此构造函数显式初始化
            LaserFrontOffset = 0.0;
            LaserToHorizontalAngleRad = 0.0;
            LaserToHorizontalScaleX = 1.0;
            CalibratedCenterX = 0.0;
            CalibratedCenterY = 0.0;
            ApproachThreshold = 5.0;
        }

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
        public void CacheHorizontalMove(double robotX, double moveDistance)
        {
            double key = robotX + LaserFrontOffset;
            lock (_cacheLock)
            {
                _horizontalMoveCache[key] = moveDistance;
            }
        }

        /// <summary>计算并缓存水平移动距离，供真实/模拟流程统一调用。</summary>
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

        #region 配置持久化（INI）

        private static readonly string _configFile =
            Path.Combine(Application.StartupPath, "Config", "INI", "MoveControlData.ini");

        /// <summary>从 INI 加载标定参数，文件不存在或失败时保留当前值。</summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_configFile))
                    return;

                var ini = new AdaWeldSystem.FileOperate.INIFile(_configFile);
                LaserFrontOffset = ini.ReadDouble("Calibration", "LaserFrontOffset", LaserFrontOffset);
                LaserToHorizontalAngleDeg = ini.ReadDouble("Calibration", "LaserToHorizontalAngleDeg", LaserToHorizontalAngleDeg);
                LaserToHorizontalScaleX = ini.ReadDouble("Calibration", "LaserToHorizontalScaleX", LaserToHorizontalScaleX);
                CalibratedCenterX = ini.ReadDouble("Calibration", "CalibratedCenterX", CalibratedCenterX);
                CalibratedCenterY = ini.ReadDouble("Calibration", "CalibratedCenterY", CalibratedCenterY);
                ApproachThreshold = ini.ReadDouble("Calibration", "ApproachThreshold", ApproachThreshold);
            }
            catch
            {
            }
        }

        /// <summary>保存当前标定参数到 INI 文件。</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configFile));
                var ini = new AdaWeldSystem.FileOperate.INIFile(_configFile);
                ini.WriteDouble("Calibration", "LaserFrontOffset", LaserFrontOffset);
                ini.WriteDouble("Calibration", "LaserToHorizontalAngleDeg", LaserToHorizontalAngleDeg);
                ini.WriteDouble("Calibration", "LaserToHorizontalScaleX", LaserToHorizontalScaleX);
                ini.WriteDouble("Calibration", "CalibratedCenterX", CalibratedCenterX);
                ini.WriteDouble("Calibration", "CalibratedCenterY", CalibratedCenterY);
                ini.WriteDouble("Calibration", "ApproachThreshold", ApproachThreshold);
                ini.SaveToFile();
            }
            catch
            {
            }
        }

        #endregion
    }
}
