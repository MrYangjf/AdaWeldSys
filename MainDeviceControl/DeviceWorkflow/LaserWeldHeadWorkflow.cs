using System;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;
using AdaWeldSystem.MainDeviceControl.FlowState;

namespace AdaWeldSystem.MainDeviceControl.DeviceWorkflow
{
    /// <summary>焊接头工作流状态（骨架期最小集，随私有逻辑落地再扩展）。</summary>
    /// <remarks>· Uninitialized 未初始化 · Standby 待机（已连接就绪） · Working 工作中 · ErrorAborted 异常终止</remarks>
    public enum LaserWeldHeadWorkflowState
    {
        Uninitialized = 0,
        Standby = 1,
        Working = 2,
        ErrorAborted = 3
    }

    /// <summary>
    /// 焊接头工作流（骨架）
    ///
    /// 步骤驱动（ADR-047）：骨架期私有逻辑未落地，暂无执行步，
    /// 故 FlowProcess 为空转、IsRunLoopActive 返回 false（不启动常驻监听线程，避免空转）。
    /// 私有逻辑落地时按段位规范新增执行步，并同步登记到 GetStepName 映射表。
    ///
    /// 新主控设计：基类改为非泛型 DeviceWorkflowBase，原流程态枚举降为私有 _phase 仅驱动 FlowProcess；
    /// 对外相位统一经 SetWeldStatus(SubDeviceWeldStatus) 承载，连接态经 ConnectOn/ConnectOff 联动 State。
    /// </summary>
    public class LaserWeldHeadWorkflow : DeviceWorkflowBase
    {
        #region 私有变量

        private readonly string _tag = "焊接头工作流";

        /// <summary>私有相位（取代旧基类 _step，仅驱动 FlowProcess 与可观测性）。</summary>
        private LaserWeldHeadWorkflowState _phase = LaserWeldHeadWorkflowState.Uninitialized;

        #endregion

        #region 单例

        private static readonly Lazy<LaserWeldHeadWorkflow> _lazyInstance =
            new Lazy<LaserWeldHeadWorkflow>(() => new LaserWeldHeadWorkflow());

        public static LaserWeldHeadWorkflow Instance => _lazyInstance.Value;

        #endregion

        #region 公共变量

        public override string StateName => _tag;

        /// <summary>骨架期无业务，不启动常驻监听线程（避免空转线程）。</summary>
        protected override bool IsRunLoopActive { get { return false; } }

        #endregion

        #region 构造函数

        private LaserWeldHeadWorkflow() { }

        #endregion

        #region 相位 → 焊接过程态映射

        /// <summary>私有 SetStep：刷新 _phase 并映射为对外焊接过程态（取代旧基类 SetStep + MapToDeviceState）。</summary>
        /// <param name="step">目标相位</param>
        /// <param name="reason">切换原因（纯文本，无符号）</param>
        private void SetStep(LaserWeldHeadWorkflowState step, string reason)
        {
            _phase = step;
            SetWeldStatus(MapWeldStatus(step), reason);
        }

        /// <summary>相位 → 焊接过程态（SubDeviceWeldStatus）映射。</summary>
        /// <param name="step">相位</param>
        /// <returns>对应焊接过程态</returns>
        private static SubDeviceWeldStatus MapWeldStatus(LaserWeldHeadWorkflowState step)
        {
            switch (step)
            {
                case LaserWeldHeadWorkflowState.Standby: return SubDeviceWeldStatus.Standby;
                case LaserWeldHeadWorkflowState.Working: return SubDeviceWeldStatus.Working;
                case LaserWeldHeadWorkflowState.ErrorAborted: return SubDeviceWeldStatus.ErrorAborted;
                default: return SubDeviceWeldStatus.Standby;
            }
        }

        #endregion

        #region 执行步分派

        /// <summary>单步分派（骨架期无执行步，空转）。</summary>
        /// <remarks>私有逻辑落地后在此 switch(_phase) 分派，case 内禁止 Thread.Sleep / while 轮询。</remarks>
        protected override void FlowProcess()
        {
        }

        /// <summary>执行步号 → 中文名映射（取私有相位，保证可观测性）。</summary>
        /// <param name="step">执行步号（基类内部游标，本骨架未推进，统一按相位展示）</param>
        /// <returns>步号对应的中文名</returns>
        protected override string GetStepName(int step)
        {
            switch (_phase)
            {
                case LaserWeldHeadWorkflowState.Standby: return "待机";
                case LaserWeldHeadWorkflowState.Working: return "工作中";
                case LaserWeldHeadWorkflowState.ErrorAborted: return "异常终止";
                default: return "未初始化";
            }
        }

        #endregion

        #region 抽象生命周期方法（新主控设计 6 契约）

        /// <summary>开启连接（骨架待实现）。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ConnectOn()
        {
            // TODO 私有逻辑待实现：自起后台线程连接焊接头并置 Connected
            return false;
        }

        /// <summary>关闭连接：断开焊接头（骨架待实现）。非阻塞。</summary>
        public override void ConnectOff()
        {
            // TODO 私有逻辑待实现：自起后台线程断开焊接头并置 Disconnected
        }

        /// <summary>流程复位：清执行步与相位并回到待机。</summary>
        /// <returns>受理返回 true</returns>
        public override bool ResetProcess()
        {
            ResetWorkStep();
            _phase = LaserWeldHeadWorkflowState.Standby;
            SetWeldStatus(SubDeviceWeldStatus.Standby, "流程复位");
            return true;
        }

        /// <summary>清除报警态并回就绪。</summary>
        public override void ClearStatus()
        {
            ResetWorkStep();
            _phase = LaserWeldHeadWorkflowState.Standby;
            SetWeldStatus(SubDeviceWeldStatus.Standby, "状态清除");
        }

        /// <summary>复位时间：重置本流程工作时间计时。</summary>
        public override void ResetWorkTime()
        {
            SetWorkTimeStart();
        }

        #endregion
    }
}
