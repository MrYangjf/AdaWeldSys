using AdaWeldSystem.EmguALG.Core;
using Emgu.CV;

namespace AdaWeldSystem.EmguALG.Abstractions
{
    /// <summary>
    /// 算法抽象：输入统一为监控相机图像 Mat，输出强类型结果。
    /// 与 IMonitorCamApi 同构的「抽象先行」契约，使换算法后端时 UI 与业务层零改动。
    /// </summary>
    /// <typeparam name="TResult">算法结果类型，须派生自 AlgorithmResult</typeparam>
    public interface IAlgorithm<TResult> where TResult : AlgorithmResult
    {
        /// <summary>算法名称（用于日志与结果标识）</summary>
        string AlgorithmName { get; }

        /// <summary>是否正在执行（防重入：同一算法实例不允许并发执行）</summary>
        bool IsBusy { get; }

        /// <summary>
        /// 执行算法
        /// </summary>
        /// <param name="input">输入图像（监控相机帧）</param>
        /// <returns>算法结果</returns>
        TResult Execute(Mat input);
    }

    /// <summary>
    /// 可配置算法抽象：由 Job 或业务层注入强类型配置
    /// </summary>
    /// <typeparam name="TConfig">强类型配置</typeparam>
    public interface IConfigurableAlgorithm<TConfig> where TConfig : class
    {
        /// <summary>当前配置</summary>
        TConfig Config { get; }

        /// <summary>
        /// 注入配置（配置变更时由 AlgorithmManager 分发）
        /// </summary>
        /// <param name="config">新配置；为 null 时由实现内部回落到默认配置</param>
        void SetConfig(TConfig config);
    }
}
