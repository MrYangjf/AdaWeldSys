using System;
using System.Collections.Generic;
using AdaWeldSystem.PCLOperate.Models;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>线激光轮廓记录类，只记录点云帧</summary>
    public class LineLaserContourRecord : ProductRecordBase
    {
        private readonly RecordFileStore _store;

        /// <summary>记录名称</summary>
        public override string Name
        {
            get { return "LineLaserContourRecord"; }
        }

        /// <summary>构造函数</summary>
        /// <param name="store">记录文件存储（Manager 独占持有并注入）</param>
        internal LineLaserContourRecord(RecordFileStore store)
        {
            _store = store;
        }

        #region 记录（点云帧）

        /// <summary>添加一帧点云数据（线激光/监控相机路径；入队即</summary>

        /// <param name="pointCloud">点云数据</param>
        public void AddPointCloud(PointCloudData pointCloud, double robotX = 0.0)
        {
            if (pointCloud == null)
            {
                return;
            }
            Enqueue(new ContourFrameItem { PointCloud = pointCloud, RobotX = robotX });
        }

        /// <summary>单条落盘（后台线程调用）</summary>
        /// <param name="item">记录条目</param>
        protected override void Persist(RecordItem item)
        {
            if (!(item is ContourFrameItem frame) || frame.PointCloud == null)
            {
                return;
            }
            _store.AddFrame(frame.PointCloud, frame.RobotX);
        }

        #endregion

        #region 会话收尾（委托存储）

        /// <summary>会话开始</summary>
        protected override void OnStart()
        {
            _store.StartSession();
        }

        /// <summary>会话正常结束</summary>
        protected override void OnStop()
        {
            _store.StopSession();
        }

        /// <summary>会话异常中止（保留临时文件）</summary>
        protected override void OnAbort()
        {
            _store.AbortSession();
        }

        #endregion
    }
}
