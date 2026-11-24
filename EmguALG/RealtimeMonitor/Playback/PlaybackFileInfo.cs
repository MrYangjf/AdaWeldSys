using System.Drawing;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.Playback
{
    /// <summary>回放列表行数据（算法层产出，UI 只负责绑定显示）</summary>
    public class PlaybackFileInfo
    {
        /// <summary>录像文件名（列表显示）</summary>
        public string FileName { get; set; }

        /// <summary>录像文件完整路径（回放与缩略图解码用）</summary>
        public string FullPath { get; set; }

        /// <summary>所属日期文件夹日期 + 文件修改时间（yyyy-MM-dd HH:mm）</summary>
        public string Modified { get; set; }

        /// <summary>文件大小（MB，一位小数）</summary>
        public string Size { get; set; }

        /// <summary>预览缩略图</summary>
        public Bitmap Thumbnail { get; set; }
    }
}
