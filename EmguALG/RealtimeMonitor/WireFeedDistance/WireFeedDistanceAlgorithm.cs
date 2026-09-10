using AdaWeldSystem.EmguALG.Abstractions;
using Emgu.CV;
using System;
using System.Diagnostics;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance
{
    /// <summary>
    /// 送丝距离检测（焊前对中）：检测激光光斑与焊丝尖端，输出偏差与是否对中。
    /// 纯算子实现仍在 ImageAlgorithm（ADR-038：算子库唯一文件），本类只做契约封装与计时。
    /// </summary>
    public sealed class WireFeedDistanceAlgorithm : IAlgorithm<WireFeedResult>, IConfigurableAlgorithm<WireFeedConfig>
    {
        #region 私有变量

        private readonly string _algorithmName = "WireFeedDistance";
        private WireFeedConfig _config;

        #endregion

        #region 公共变量

        /// <summary>算法名称</summary>
        public string AlgorithmName
        {
            get { return _algorithmName; }
        }

        /// <summary>是否正在执行（防重入）</summary>
        public bool IsBusy { get; private set; }

        /// <summary>当前配置</summary>
        public WireFeedConfig Config
        {
            get { return _config; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建送丝距离检测算法并填入默认配置
        /// </summary>
        public WireFeedDistanceAlgorithm()
        {
            _config = new WireFeedConfig();
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 注入配置
        /// </summary>
        /// <param name="config">新配置；为 null 时回落默认配置</param>
        public void SetConfig(WireFeedConfig config)
        {
            _config = config ?? new WireFeedConfig();
        }

        /// <summary>
        /// 执行送丝距离检测
        /// </summary>
        /// <param name="input">监控相机图像</param>
        /// <returns>检测结果（含成功标志与耗时）</returns>
        public WireFeedResult Execute(Mat input)
        {
            if (IsBusy)
            {
                WireFeedResult busy = new WireFeedResult();
                busy.ErrorMessage = "算法正在执行，已拒绝重入";
                return busy;
            }

            IsBusy = true;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                WireFeedResult result = ImageAlgorithm.DetectPreWeldAlignment(input, _config);
                if (result == null)
                {
                    result = new WireFeedResult();
                    result.ErrorMessage = "送丝距离检测无结果";
                    return result;
                }
                result.Success = true;
                result.Algorithm = _algorithmName;
                result.ElapsedMs = watch.Elapsed.TotalMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                WireFeedResult failed = new WireFeedResult();
                failed.ErrorMessage = ex.Message;
                failed.Algorithm = _algorithmName;
                failed.ElapsedMs = watch.Elapsed.TotalMilliseconds;
                return failed;
            }
            finally
            {
                watch.Stop();
                IsBusy = false;
            }
        }

        #endregion
    }
}
