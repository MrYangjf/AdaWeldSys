using System;

namespace AdaWeldSystem.ProductFileManager
{
    /// <summary>
    /// 焊接动作/过程记录（全流程生产数据）
    /// 位于 ProductFileManager/（与 RunRecordManager 同模块）：本记录用于回溯焊接流程中由图像算法与智能焊接调整
    /// 联动产生的生成数据。每条记录既包含该时刻的过程量（机器人坐标、速度、功率、
    /// 焊缝宽度），也包含是否执行了自适应调整动作及其调整数据。
    /// </summary>
    /// <remarks>
    /// 两类记录入口（详见 RunRecordManager）：
    /// 1) 过程帧记录（AdjustExecuted = false）：每一帧记录一次，来自 WeldProcess.StoreChartData 联动。
    /// 2) 调整动作记录（AdjustExecuted = true）：仅当自适应调整触发时记录，AdjustData 携带调整明细。
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
        /// 记录类型标签：ProcessFrame（过程帧）/ WeldParamAdjust（调整动作）
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
        /// 送丝速度
        /// </summary>
        public double FeedSpeed { get; set; }

        /// <summary>
        /// 激光功率
        /// </summary>
        public double LaserPower { get; set; }

        /// <summary>
        /// 焊缝宽度（来自图像算法焊缝特征）
        /// </summary>
        public double WeldWidth { get; set; }

        /// <summary>
        /// 是否执行了调整动作
        /// </summary>
        public bool AdjustExecuted { get; set; }

        /// <summary>
        /// 调整数据（AdjustExecuted 为 true 时有效，记录调整前后的参数明细）
        /// </summary>
        public string AdjustData { get; set; }
    }
}
