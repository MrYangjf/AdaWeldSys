using AdaWeldSystem.EmguALG.Abstractions;
using Emgu.CV;
using System;
using System.Diagnostics;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.WeldQuality
{
    /// <summary>
    /// 焊接质量检测（焊中）：识别焊道成形与缺陷，输出质量分与合格判定。
    /// 纯算子实现仍在 ImageAlgorithm（ADR-038：算子库唯一文件），本类只做契约封装与计时。
    /// </summary>
    public sealed class WeldQualityAlgorithm : IAlgorithm<WeldQualityResult>, IConfigurableAlgorithm<WeldQualityConfig>
    {
        #region 私有变量

        private readonly string _algorithmName = "WeldQuality";
        private WeldQualityConfig _config;

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
        public WeldQualityConfig Config
        {
            get { return _config; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建焊接质量检测算法并填入默认配置
        /// </summary>
        public WeldQualityAlgorithm()
        {
            _config = new WeldQualityConfig();
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 注入配置
        /// </summary>
        /// <param name="config">新配置；为 null 时回落默认配置</param>
        public void SetConfig(WeldQualityConfig config)
        {
            _config = config ?? new WeldQualityConfig();
        }

        /// <summary>
        /// 执行焊接质量检测
        /// </summary>
        /// <param name="input">监控相机图像</param>
        /// <returns>检测结果（含成功标志与耗时）</returns>
        public WeldQualityResult Execute(Mat input)
        {
            if (IsBusy)
            {
                WeldQualityResult busy = new WeldQualityResult();
                busy.ErrorMessage = "算法正在执行，已拒绝重入";
                return busy;
            }

            IsBusy = true;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                WeldQualityResult result = ImageAlgorithm.DetectWeldQuality(input, _config);
                if (result == null)
                {
                    result = new WeldQualityResult();
                    result.ErrorMessage = "焊接质量检测无结果";
                    return result;
                }
                result.Success = true;
                result.Algorithm = _algorithmName;
                result.ElapsedMs = watch.Elapsed.TotalMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                WeldQualityResult failed = new WeldQualityResult();
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
