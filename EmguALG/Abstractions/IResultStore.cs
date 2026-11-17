using AdaWeldSystem.EmguALG.Core;

namespace AdaWeldSystem.EmguALG.Abstractions
{
    /// <summary>
    /// 结果缓存抽象：池化快照 + 版本号，避免事件风暴。
    /// 算法线程写入，UI 线程按版本号拉取。
    /// </summary>
    /// <typeparam name="TResult">缓存的结果类型</typeparam>
    public interface IResultStore<TResult> where TResult : AlgorithmResult
    {
        /// <summary>当前版本号，每次发布结果后递增</summary>
        int Version { get; }

        /// <summary>
        /// 取最新结果快照
        /// </summary>
        /// <param name="version">取出时的版本号，供调用方判重</param>
        /// <returns>最新结果；尚无结果时返回 null</returns>
        TResult GetLatest(out int version);

        /// <summary>
        /// 发布一次结果（推进版本号）
        /// </summary>
        /// <param name="result">待发布的结果</param>
        void Publish(TResult result);
    }
}
