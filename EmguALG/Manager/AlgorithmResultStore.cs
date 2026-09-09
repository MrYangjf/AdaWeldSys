using AdaWeldSystem.EmguALG.Abstractions;
using AdaWeldSystem.EmguALG.Core;

namespace AdaWeldSystem.EmguALG.Manager
{
    /// <summary>
    /// 结果缓存（池化快照 + 版本号）：算法线程写入，UI 线程按版本号拉取。
    /// 与监控相机帧缓存、线激光轮廓缓存同构，避免高频结果走事件造成事件风暴。
    /// </summary>
    /// <typeparam name="TResult">缓存的结果类型</typeparam>
    public sealed class AlgorithmResultStore<TResult> : IResultStore<TResult> where TResult : AlgorithmResult
    {
        #region 私有变量

        private readonly object _sync = new object();
        private TResult _latest;
        private int _version;

        #endregion

        #region 公共变量

        /// <summary>当前版本号，每次发布后递增</summary>
        public int Version
        {
            get { lock (_sync) { return _version; } }
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 取最新结果
        /// </summary>
        /// <param name="version">取出时对应的版本号</param>
        /// <returns>最新结果；尚无结果时返回 null</returns>
        public TResult GetLatest(out int version)
        {
            lock (_sync)
            {
                version = _version;
                return _latest;
            }
        }

        /// <summary>
        /// 发布一次结果并推进版本号
        /// </summary>
        /// <param name="result">待发布的结果</param>
        public void Publish(TResult result)
        {
            if (result == null) return;
            lock (_sync)
            {
                _latest = result;
                _version++;
            }
        }

        #endregion
    }
}
