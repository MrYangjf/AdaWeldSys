using AdaWeldSystem.PCLOperate.Models;
using System;

namespace AdaWeldSystem.ProductFileManager
{
    /// <summary>
    /// 记录帧数据结构
    /// </summary>
    public class RecordFrame
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 点云数据
        /// </summary>
        public PointCloudData PointCloud { get; set; }
    }
}
