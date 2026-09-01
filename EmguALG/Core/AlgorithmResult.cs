using System;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>
    /// 算法执行结果基类：所有领域算法结果均派生自此。
    /// 携带统一的成功标志、耗时与错误信息；具体领域结果追加业务字段。
    /// </summary>
    public class AlgorithmResult
    {
        /// <summary>是否成功完成</summary>
        public bool Success { get; set; }

        /// <summary>执行耗时（毫秒）</summary>
        public double ElapsedMs { get; set; }

        /// <summary>失败时的错误信息（成功时为 null）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>算法名称（由 AlgorithmBase.Execute 模板方法填充）</summary>
        public string Algorithm { get; set; }

        public AlgorithmResult()
        {
            Success = false;
            ElapsedMs = 0;
            ErrorMessage = null;
            Algorithm = null;
        }
    }
}
