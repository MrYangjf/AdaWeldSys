using System;

namespace AdaWeldSystem.EmguALG.Manager
{
    /// <summary>
    /// 算法完成事件参数（低频：状态灯/日志；高频结果走 IResultStore 轮询）。
    /// </summary>
    public class AlgorithmCompletedEventArgs : EventArgs
    {
        /// <summary>算法名称</summary>
        public string AlgorithmName { get; set; }

        /// <summary>是否成功</summary>
        public bool Success { get; set; }

        /// <summary>执行耗时（毫秒）</summary>
        public double ElapsedMilliseconds { get; set; }

        /// <summary>失败信息（成功时为 null）</summary>
        public string ErrorMessage { get; set; }
    }
}
