using AdaWeldSystem.Comm;
using AdaWeldSystem.EmguALG.Abstractions;
using AdaWeldSystem.EmguALG.WeldQuality;
using AdaWeldSystem.EmguALG.WireFeedDistance;
using AdaWeldSystem.FileOperate;
using Emgu.CV;
using System;
using System.IO;

namespace AdaWeldSystem.EmguALG.Manager
{
    /// <summary>
    /// 算法业务中枢（单例，对外唯一入口）：统一调度两个领域检测算法，
    /// 持有强类型配置并持久化，对外提供「同步检测 + 结果缓存轮询 + 低频完成事件」三种消费方式。
    /// 输入源唯一：监控相机图像（Mat），不再接收点云输入。
    /// </summary>
    public sealed class AlgorithmManager
    {
        #region 私有变量

        private static readonly Lazy<AlgorithmManager> _lazy =
            new Lazy<AlgorithmManager>(() => new AlgorithmManager());

        private readonly string _tag = "算法管理器";
        private readonly IAlgorithm<WireFeedResult> _wireFeedAlgorithm;
        private readonly IAlgorithm<WeldQualityResult> _qualityAlgorithm;
        private readonly IResultStore<WireFeedResult> _wireFeedStore;
        private readonly IResultStore<WeldQualityResult> _qualityStore;
        private readonly WireFeedConfig _wireFeedConfig;
        private readonly WeldQualityConfig _qualityConfig;

        private static readonly string _configFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "MonitorAlgorithm.ini");

        #endregion

        #region 公共变量

        /// <summary>全局唯一实例</summary>
        public static AlgorithmManager Instance
        {
            get { return _lazy.Value; }
        }

        /// <summary>送丝距离检测配置（对外可改，改后需调用 ApplyConfig 下发）</summary>
        public WireFeedConfig WireFeed
        {
            get { return _wireFeedConfig; }
        }

        /// <summary>焊接质量检测配置（对外可改，改后需调用 ApplyConfig 下发）</summary>
        public WeldQualityConfig Quality
        {
            get { return _qualityConfig; }
        }

        /// <summary>送丝距离结果缓存版本号</summary>
        public int WireFeedVersion
        {
            get { return _wireFeedStore.Version; }
        }

        /// <summary>焊接质量结果缓存版本号</summary>
        public int QualityVersion
        {
            get { return _qualityStore.Version; }
        }

        #endregion

        #region 事件

        /// <summary>算法完成事件（低频：日志/状态灯；结果本体走缓存）</summary>
        public event EventHandler<AlgorithmCompletedEventArgs> AlgorithmCompleted;

        #endregion

        #region 构造函数

        private AlgorithmManager()
        {
            _wireFeedConfig = new WireFeedConfig();
            _qualityConfig = new WeldQualityConfig();

            var wireFeed = new WireFeedDistanceAlgorithm();
            var quality = new WeldQualityAlgorithm();
            wireFeed.SetConfig(_wireFeedConfig);
            quality.SetConfig(_qualityConfig);

            _wireFeedAlgorithm = wireFeed;
            _qualityAlgorithm = quality;
            _wireFeedStore = new AlgorithmResultStore<WireFeedResult>();
            _qualityStore = new AlgorithmResultStore<WeldQualityResult>();
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 送丝距离检测（焊前对中）
        /// </summary>
        /// <param name="image">监控相机图像</param>
        /// <returns>检测结果</returns>
        public WireFeedResult DetectWireFeed(Mat image)
        {
            WireFeedResult result = _wireFeedAlgorithm.Execute(image);
            _wireFeedStore.Publish(result);
            RaiseCompleted(result.Algorithm, result.Success, result.ElapsedMs, result.ErrorMessage);
            return result;
        }

        /// <summary>
        /// 焊接质量检测（焊中成形）
        /// </summary>
        /// <param name="image">监控相机图像</param>
        /// <returns>检测结果</returns>
        public WeldQualityResult DetectWeldQuality(Mat image)
        {
            WeldQualityResult result = _qualityAlgorithm.Execute(image);
            _qualityStore.Publish(result);
            RaiseCompleted(result.Algorithm, result.Success, result.ElapsedMs, result.ErrorMessage);
            return result;
        }

        /// <summary>
        /// 取最新送丝距离结果（UI 轮询入口）
        /// </summary>
        /// <param name="version">取出时对应的版本号</param>
        /// <returns>最新结果；尚无结果时返回 null</returns>
        public WireFeedResult GetLatestWireFeed(out int version)
        {
            return _wireFeedStore.GetLatest(out version);
        }

        /// <summary>
        /// 取最新焊接质量结果（UI 轮询入口）
        /// </summary>
        /// <param name="version">取出时对应的版本号</param>
        /// <returns>最新结果；尚无结果时返回 null</returns>
        public WeldQualityResult GetLatestQuality(out int version)
        {
            return _qualityStore.GetLatest(out version);
        }

        /// <summary>
        /// 将当前配置下发给两个算法（配置被外部修改后调用）
        /// </summary>
        public void ApplyConfig()
        {
            var configurableWireFeed = _wireFeedAlgorithm as IConfigurableAlgorithm<WireFeedConfig>;
            if (configurableWireFeed != null) configurableWireFeed.SetConfig(_wireFeedConfig);

            var configurableQuality = _qualityAlgorithm as IConfigurableAlgorithm<WeldQualityConfig>;
            if (configurableQuality != null) configurableQuality.SetConfig(_qualityConfig);
        }

        /// <summary>
        /// 从配置文件加载算法参数（须在应用启动、创建读取它的 UI 页面之前调用）
        /// </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_configFile)) return;
                INIFile ini = new INIFile(_configFile);
                _wireFeedConfig.ToleranceX = ini.ReadDouble("Alignment", "ToleranceX", _wireFeedConfig.ToleranceX);
                _wireFeedConfig.ToleranceY = ini.ReadDouble("Alignment", "ToleranceY", _wireFeedConfig.ToleranceY);
                _wireFeedConfig.ToleranceAngle = ini.ReadDouble("Alignment", "ToleranceAngle", _wireFeedConfig.ToleranceAngle);
                _qualityConfig.PassScore = ini.ReadDouble("Quality", "PassScore", _qualityConfig.PassScore);
                ApplyConfig();
            }
            catch
            {
                // 加载异常时保留默认/当前值
            }
        }

        /// <summary>
        /// 将当前算法参数持久化到配置文件
        /// </summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configFile));
                INIFile ini = new INIFile(_configFile);
                ini.WriteDouble("Alignment", "ToleranceX", _wireFeedConfig.ToleranceX);
                ini.WriteDouble("Alignment", "ToleranceY", _wireFeedConfig.ToleranceY);
                ini.WriteDouble("Alignment", "ToleranceAngle", _wireFeedConfig.ToleranceAngle);
                ini.WriteDouble("Quality", "PassScore", _qualityConfig.PassScore);
                ini.SaveToFile();
            }
            catch
            {
                // 保存失败不影响运行时
            }
        }

        #endregion

        #region 私有函数

        /// <summary>抛出低频完成事件（仅成功/失败/耗时，不含结果本体）</summary>
        private void RaiseCompleted(string algorithmName, bool success, double elapsedMs, string error)
        {
            EventHandler<AlgorithmCompletedEventArgs> handler = AlgorithmCompleted;
            if (handler == null) return;

            AlgorithmCompletedEventArgs args = new AlgorithmCompletedEventArgs();
            args.AlgorithmName = algorithmName;
            args.Success = success;
            args.ElapsedMilliseconds = elapsedMs;
            args.ErrorMessage = error;
            handler(this, args);

            if (!success)
                GlobalCommData.ShowLog(_tag, string.Format("算法 {0} 执行失败 原因 {1}", algorithmName, error));
        }

        #endregion
    }
}
