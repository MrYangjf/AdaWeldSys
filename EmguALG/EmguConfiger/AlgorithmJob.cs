using System;

namespace AdaWeldSystem.EmguALG.EmguConfiger
{
    /// <summary>
    /// 算法 Job：一套完整的算法参数配置任务
    /// 每个 Job 包含独立的 PipelineConfig，支持运行时切换
    /// </summary>
    [Serializable]
    public class AlgorithmJob
    {
        /// <summary>
        /// Job 编号（唯一标识）
        /// </summary>
        public int JobId { get; set; }

        /// <summary>
        /// Job 名称
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// 算法配置（各阶段参数）
        /// </summary>
        public PipelineConfig Config { get; set; }

        public AlgorithmJob()
        {
            JobId = 1;
            JobName = "Default";
            Config = new PipelineConfig();
        }

        /// <summary>
        /// 深拷贝当前 Job
        /// </summary>
        public AlgorithmJob Clone()
        {
            return new AlgorithmJob
            {
                JobId = this.JobId,
                JobName = this.JobName,
                Config = this.Config?.Clone()
            };
        }
    }
}