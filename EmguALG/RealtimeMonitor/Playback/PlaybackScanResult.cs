using System.Collections.Generic;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.Playback
{
    /// <summary>一次区间扫描的结果（行数据 + 结果文案）</summary>
    public class PlaybackScanResult
    {
        /// <summary>本层行数据（非 null，可为空集合）</summary>
        public List<PlaybackFileInfo> Files { get; set; }

        /// <summary>结果摘要 / 空态文案（回填到列表标题标签）</summary>
        public string Caption { get; set; }

        /// <summary>快速失败提示（非 null 时不回填行，直接提示）</summary>
        public string Error { get; set; }
    }
}
