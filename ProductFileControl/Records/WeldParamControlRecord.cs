using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>工艺控制记录类：焊接工艺数据留痕（.pDat）。</summary>
    /// <remarks>三类动作：过程帧、参数调整、Job 切换。
    /// 均携带 RobotX，按机器人 X 对齐。
    /// 轮廓数据与 Job 切换不写入本记录，由 <see cref="LineLaserContourRecord"/> 承接；承接记录请求事件（经 Manager 透传）。</remarks>
    public class WeldParamControlRecord : ProductRecordBase
    {
        private readonly RecordFileStore _store;

        /// <summary>过程帧动作类型标识（落盘标签，与 <see cref="WeldActionRecord.ActionType"/> 对齐）</summary>
        public const string ActionProcessFrame = "ProcessFrame";

        /// <summary>工艺输出触发动作类型标识（控制工艺输出触发）</summary>
        public const string ActionProcessOutputTrigger = "ProcessOutputTrigger";

        /// <summary>分析工具（线激光 Job）切换动作类型标识</summary>
        public const string ActionJobSwitch = "JobSwitch";

        /// <summary>
        /// 记录名称
        /// </summary>
        public override string Name
        {
            get { return "WeldParamControlRecord"; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="store">记录文件存储（Manager 独占持有并注入）</param>
        internal WeldParamControlRecord(RecordFileStore store)
        {
            _store = store;
        }

        #region 记录（过程帧 + 调整动作）

        /// <summary>
        /// 记录一帧工艺过程快照（入队即返回，后台落盘）
        /// </summary>
        /// <param name="item">工艺快照条目（AdjustExecuted 应为 false）</param>
        public void RecordProcessFrame(WeldParamAdjustItem item)
        {
            if (item == null)
            {
                return;
            }
            item.ActionType = ActionProcessFrame;
            item.AdjustExecuted = false;
            Enqueue(item);
        }

        /// <summary>
        /// 记录一次焊缝参数调整（入队即返回，后台落盘）
        /// </summary>
        /// <param name="item">焊缝参数调整条目（AdjustExecuted 应为 true）</param>
        public void RecordAdjustment(WeldParamAdjustItem item)
        {
            if (item == null)
            {
                return;
            }
            item.ActionType = ActionProcessOutputTrigger;
            item.AdjustExecuted = true;
            Enqueue(item);
        }

        /// <summary>记录一次线激光工具（Job）切换（入队即返回）</summary>
        /// <remarks>切换记入本记录，不写入轮廓记录。</remarks>
        /// <param name="item">工具切换条目（JobId 为切换到的目标 Job；AdjustExecuted 应为 true）</param>
        public void RecordJobSwitch(WeldParamAdjustItem item)
        {
            if (item == null)
            {
                return;
            }
            item.ActionType = ActionJobSwitch;
            item.AdjustExecuted = true;
            Enqueue(item);
        }

        /// <summary>单条落盘（后台线程调用；按 ActionType 分派过程帧 / 参数调整 / </summary>
        /// <remarks>工具切换三类记录）</remarks>
        /// <param name="item">记录条目</param>
        protected override void Persist(RecordItem item)
        {
            if (!(item is WeldParamAdjustItem adjust))
            {
                return;
            }

            if (string.Equals(adjust.ActionType, ActionJobSwitch, StringComparison.Ordinal))
            {
                _store.AddJobSwitchRecord(adjust.RobotX, adjust.RobotY, adjust.RobotZ,
                    adjust.RobotSpeed, adjust.FeedSpeed, adjust.LaserPower, adjust.WeldWidth,
                    adjust.JobId, adjust.SeamPointY, adjust.SeamPointZ, adjust.AdjustData);
            }
            else if (adjust.AdjustExecuted)
            {
                _store.AddAdjustRecord(adjust.RobotX, adjust.RobotY, adjust.RobotZ,
                    adjust.RobotSpeed, adjust.FeedSpeed, adjust.LaserPower, adjust.WeldWidth,
                    adjust.JobId, adjust.SeamPointY, adjust.SeamPointZ, adjust.AdjustData);
            }
            else
            {
                _store.AddProcessRecord(adjust.RobotX, adjust.RobotY, adjust.RobotZ,
                    adjust.RobotSpeed, adjust.FeedSpeed, adjust.LaserPower, adjust.WeldWidth,
                    adjust.JobId, adjust.SeamPointY, adjust.SeamPointZ,
                    adjust.TaPositionMm, adjust.LaPositionMm, adjust.GalvoAmplitudeMm,
                    adjust.GalvoPatternId, adjust.HeadTempC);
            }
        }

        /// <summary>
        /// 获取所有动作记录（过程帧 + 调整记录，供回溯）
        /// </summary>
        /// <returns>动作记录列表</returns>
        public List<WeldActionRecord> GetAllActions()
        {
            return _store.GetAllActions();
        }
    }

    /// <summary>
    /// 焊接动作与过程记录，属全流程生产数据。
    /// 回溯焊接中算法与智能调整数据。
    /// 每条含该时刻过程量与调整动作明细。
    /// </summary>
    /// <remarks>
    /// 记录入口共三类，详见 ProductFileRecordManager。
    /// 1) 过程帧记录：ActionType 为 ProcessFrame。
    /// 每帧记录一次，由 WeldParamAdjustmentAlgorithm 联动。
    /// 2) 参数调整：自适应触发时记录调整明细。
    /// 3) 工具切换：焊接中切换分析 Job 时记录。
    /// </remarks>
    public class WeldActionRecord
    {
        /// <summary>
        /// 对应帧索引（与点云 X 对齐，便于联合回溯）
        /// </summary>
        public int FrameIndex { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 记录类型：过程帧/参数调整/工具切换
        /// </summary>
        public string ActionType { get; set; }

        /// <summary>
        /// 机器人 X 坐标（折线图统一 X 轴）
        /// </summary>
        public double RobotX { get; set; }

        /// <summary>
        /// 机器人 Y 坐标
        /// </summary>
        public double RobotY { get; set; }

        /// <summary>
        /// 机器人 Z 坐标
        /// </summary>
        public double RobotZ { get; set; }

        /// <summary>
        /// 机器人运动速度
        /// </summary>
        public double RobotSpeed { get; set; }

        /// <summary>
        /// 送丝速度（查表输出的调整值）
        /// </summary>
        public double FeedSpeed { get; set; }

        /// <summary>
        /// 激光功率（查表输出的调整值）
        /// </summary>
        public double LaserPower { get; set; }

        /// <summary>
        /// 焊缝宽度（来自图像算法焊缝特征）
        /// </summary>
        public double WeldWidth { get; set; }

        /// <summary>
        /// 切换到的线激光分析 Job 编号（ActionType=JobSwitch 时有效，其余为 0）
        /// </summary>
        public int JobId { get; set; }

        /// <summary>线激光焊缝特征点横向坐标（毫米）</summary>
        public double SeamPointY { get; set; }



        /// <summary>线激光焊缝特征点高度坐标（毫米）</summary>
        public double SeamPointZ { get; set; }

        /// <summary>TA 送丝 Z 轴位置（毫米，台达轴反馈）。与 <see cref="FeedSpeed"/> 正交：一个是位置一个是速率。</summary>
        public double TaPositionMm { get; set; }

        /// <summary>LA 对焦轴位置（毫米，WSX 0x6020 Sub3 × 量纲）。与 <see cref="RobotZ"/> 正交：不同设备的 Z。</summary>
        public double LaPositionMm { get; set; }

        /// <summary>振镜摆幅（毫米，来自 WeldParamOutput.GalvoAmplitude）。与 <see cref="WeldWidth"/> 正交：一个是摆动量一个是识别宽度。</summary>
        public double GalvoAmplitudeMm { get; set; }

        /// <summary>振镜图形编号（WsgGalvoPattern 枚举值）</summary>
        public int GalvoPatternId { get; set; }

        /// <summary>激光头温度（摄氏度，WSX 0x6000 Sub1 保护镜温度）</summary>
        public double HeadTempC { get; set; }

        /// <summary>
        /// 是否调整动作（调整或切换为 true）
        /// </summary>
        public bool AdjustExecuted { get; set; }

        /// <summary>
        /// 调整数据（AdjustExecuted 为 true 时有效，记录调整前后的参数明细）
        /// </summary>
        public string AdjustData { get; set; }
    }
    #endregion
}
