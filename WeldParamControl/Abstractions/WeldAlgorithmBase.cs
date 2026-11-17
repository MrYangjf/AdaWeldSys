namespace AdaWeldSystem.WeldParamControl.Abstractions
{
    /// <summary>
    /// 焊接算法抽象基类：定义对外公共接口。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="WeldParamManager"/> 统一持有与协调，具体实现类不采用单例模式。
    /// 实现：<see cref="Algorithms.WeldParamLookupAlgorithm"/> 参数查表；<see cref="Algorithms.WeldParamAdjustmentAlgorithm"/> 宽度驱动调整；<see cref="Algorithms.SeamTrackingAlgorithm"/> 几何轨迹跟踪。
    /// </remarks>
    public abstract class WeldAlgorithmBase
    {
        /// <summary>算法名称（日志与可观测性标识）。</summary>
        public abstract string Name { get; }

        /// <summary>算法是否已就绪（配置已载入）。</summary>
        public abstract bool IsReady { get; }

        /// <summary>载入算法专属配置。</summary>
        /// <param name="config">配置对象，实现类按自身类型转换。</param>
        public abstract void LoadConfig(object config);

        /// <summary>复位算法运行期状态（保留配置）。</summary>
        public abstract void Reset();
    }
}
