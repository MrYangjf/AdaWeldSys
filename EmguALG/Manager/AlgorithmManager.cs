using AdaWeldSystem.EmguALG.Abstractions;
using AdaWeldSystem.EmguALG.Core;
using AdaWeldSystem.EmguALG.LineLaserContour;
using AdaWeldSystem.EmguALG.RealtimeMonitor.Playback;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WeldQuality;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance;
using AdaWeldSystem.FileOperate;
using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Drawing;
using System.IO;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.EmguALG.Manager
{
    /// <summary>
    /// 算法业务中枢（单例），统一调度两个检测算法。
    /// 持有强类型配置并持久化。
    /// 提供同步检测、结果轮询与完成事件三种方式。
    /// 输入源唯一：监控相机图像（Mat）不再接收点云输入。
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

        // 轮廓渲染算法：惰性创建，生命周期随单例
        private readonly Lazy<ILineLaserContourAlgorithm> _lineLaserContour =
            new Lazy<ILineLaserContourAlgorithm>(() => new LineLaserContourAlgorithm());

        // 回放算法：惰性创建，全页面共用，自行复位
        private readonly Lazy<PlaybackAlgorithm> _playback =
            new Lazy<PlaybackAlgorithm>(() => new PlaybackAlgorithm());

        private static readonly string _configFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "EmguALG", "Algorithm_INI", "MonitorAlgorithm.ini");

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

        /// <summary>线激光轮廓渲染算法（首次取用时创建；无独立可配置状态）</summary>
        public ILineLaserContourAlgorithm LineLaserContour
        {
            get { return _lineLaserContour.Value; }
        }

        /// <summary>监控相机回放算法（首次取用时创建；所有页面共用同一实例）</summary>
        public PlaybackAlgorithm Playback
        {
            get { return _playback.Value; }
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
        /// 在帧上画光斑点十字标记（绿色，半长 20px，线宽 1；坐标非正或越界时不画）
        /// </summary>
        /// <param name="frame">待标注帧（就地标注）</param>
        /// <param name="x">光斑点 X 坐标（像素）</param>
        /// <param name="y">光斑点 Y 坐标（像素）</param>
        public void DrawSpotMark(Mat frame, int x, int y)
        {
            if (frame == null) return;
            if (x <= 0 || y <= 0) return;
            if (x >= frame.Width || y >= frame.Height) return;

            const int half = 20;
            MCvScalar color = new MCvScalar(0, 255, 0);
            CvInvoke.Line(frame, new Point(x - half, y), new Point(x + half, y), color, 1);
            CvInvoke.Line(frame, new Point(x, y - half), new Point(x, y + half), color, 1);
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

        /// <summary>将当前配置下发给两个算法（配置被外部修改后</summary>
        /// <remarks>调用）</remarks>
        public void ApplyConfig()
        {
            var configurableWireFeed = _wireFeedAlgorithm as IConfigurableAlgorithm<WireFeedConfig>;
            configurableWireFeed?.SetConfig(_wireFeedConfig);

            var configurableQuality = _qualityAlgorithm as IConfigurableAlgorithm<WeldQualityConfig>;
            configurableQuality?.SetConfig(_qualityConfig);
        }

        /// <summary>创建空白算法管线配置</summary>
        /// <returns>未接场景的占位配置</returns>
        public PipelineConfig CreateEmptyPipelineConfig()
        {
            return new PipelineConfig();
        }

        /// <summary>从配置文件加载算法参数（须在应用启动、创建读</summary>
        /// <remarks>取它的 UI 页面之前调用）</remarks>
        public void Load()
        {
            try
            {
                if (!File.Exists(_configFile)) return;
                INIFile ini = new INIFile(_configFile);
                _wireFeedConfig.ToleranceX = ini.ReadDouble("Alignment", "ToleranceX", _wireFeedConfig.ToleranceX);
                _wireFeedConfig.ToleranceY = ini.ReadDouble("Alignment", "ToleranceY", _wireFeedConfig.ToleranceY);
                // 毫米化与三级判定对中配置
                _wireFeedConfig.CenterWarnMm = ini.ReadDouble("Alignment", "WIRE_ALIGN_WARN_MM", _wireFeedConfig.CenterWarnMm);
                _wireFeedConfig.CenterFailMm = ini.ReadDouble("Alignment", "WIRE_ALIGN_FAIL_MM", _wireFeedConfig.CenterFailMm);
                _wireFeedConfig.PixelMmScale = ini.ReadDouble("Alignment", "PIXEL_MM_SCALE", _wireFeedConfig.PixelMmScale);
                _wireFeedConfig.SpotRefX = ini.ReadDouble("Alignment", "SPOT_REF_X", _wireFeedConfig.SpotRefX);
                _wireFeedConfig.SpotRefY = ini.ReadDouble("Alignment", "SPOT_REF_Y", _wireFeedConfig.SpotRefY);
                _wireFeedConfig.WireStickoutTargetMm = ini.ReadDouble("Alignment", "WIRE_STICKOUT_TARGET_MM", _wireFeedConfig.WireStickoutTargetMm);
                _wireFeedConfig.StickoutAxisHorizontal = ini.ReadBool("Alignment", "STICKOUT_AXIS_HORIZONTAL", _wireFeedConfig.StickoutAxisHorizontal);
                _wireFeedConfig.StickoutSign = (int)ini.ReadDouble("Alignment", "STICKOUT_SIGN", _wireFeedConfig.StickoutSign);
                _wireFeedConfig.WireFeedCompGain = ini.ReadDouble("Alignment", "WIRE_FEED_COMP_GAIN", _wireFeedConfig.WireFeedCompGain);
                _wireFeedConfig.WireAnomalyCutWire = ini.ReadBool("Alignment", "WIRE_ANOMALY_CUTWIRE", _wireFeedConfig.WireAnomalyCutWire);
                if (_wireFeedConfig.StickoutSign >= 0) _wireFeedConfig.StickoutSign = 1; else _wireFeedConfig.StickoutSign = -1;
                if (_wireFeedConfig.PixelMmScale <= 0.0) _wireFeedConfig.PixelMmScale = 0.05;
                if (_wireFeedConfig.CenterWarnMm <= 0.0) _wireFeedConfig.CenterWarnMm = 0.1;
                if (_wireFeedConfig.CenterFailMm <= _wireFeedConfig.CenterWarnMm) _wireFeedConfig.CenterFailMm = 0.3;
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
                // 毫米化与三级判定对中配置
                ini.WriteDouble("Alignment", "WIRE_ALIGN_WARN_MM", _wireFeedConfig.CenterWarnMm);
                ini.WriteDouble("Alignment", "WIRE_ALIGN_FAIL_MM", _wireFeedConfig.CenterFailMm);
                ini.WriteDouble("Alignment", "PIXEL_MM_SCALE", _wireFeedConfig.PixelMmScale);
                ini.WriteDouble("Alignment", "SPOT_REF_X", _wireFeedConfig.SpotRefX);
                ini.WriteDouble("Alignment", "SPOT_REF_Y", _wireFeedConfig.SpotRefY);
                ini.WriteDouble("Alignment", "WIRE_STICKOUT_TARGET_MM", _wireFeedConfig.WireStickoutTargetMm);
                ini.WriteBool("Alignment", "STICKOUT_AXIS_HORIZONTAL", _wireFeedConfig.StickoutAxisHorizontal);
                ini.WriteDouble("Alignment", "STICKOUT_SIGN", _wireFeedConfig.StickoutSign);
                ini.WriteDouble("Alignment", "WIRE_FEED_COMP_GAIN", _wireFeedConfig.WireFeedCompGain);
                ini.WriteBool("Alignment", "WIRE_ANOMALY_CUTWIRE", _wireFeedConfig.WireAnomalyCutWire);
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

        /// <summary>抛出完成事件（仅成功/失败/耗时）</summary>
        private void RaiseCompleted(string algorithmName, bool success, double elapsedMs, string error)
        {
            EventHandler<AlgorithmCompletedEventArgs> handler = AlgorithmCompleted;
            if (handler == null) return;

            AlgorithmCompletedEventArgs args = new AlgorithmCompletedEventArgs
            {
                AlgorithmName = algorithmName,
                Success = success,
                ElapsedMilliseconds = elapsedMs,
                ErrorMessage = error
            };
            handler(this, args);

            if (!success)
                GlobalCommData.ShowLog(_tag, string.Format("算法 {0} 执行失败 原因 {1}", algorithmName, error));
        }

        #endregion
    }
}
