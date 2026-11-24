namespace AdaWeldSystem.WeldParamControl.Abstractions
{
    /// <summary>焊接算法抽象基类：定义对外公共接口。</summary>
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
