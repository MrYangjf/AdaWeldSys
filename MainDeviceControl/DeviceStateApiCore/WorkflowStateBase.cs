using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MainDeviceControl.DeviceState
{
    /// <summary>流程态变更事件参数（通用契约：TStep 为各工作流自有流程态枚举）。</summary>
    /// <remarks>· OldState / NewState  前后流程态 · Reason               转换原因 · Timestamp            变更时间</remarks>
    /// <typeparam name="TStep">工作流自有流程态枚举类型</typeparam>
    public class WorkflowStepChangedEventArgs<TStep> : EventArgs where TStep : struct
    {
        /// <summary>切换前流程态。</summary>
        public TStep OldState { get; set; }

        /// <summary>切换后流程态。</summary>
        public TStep NewState { get; set; }

        /// <summary>切换原因。</summary>
        public string Reason { get; set; }

        /// <summary>变更时间。</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>流程态基类（继承 DeviceStateBase）：统一承载流程态 Step 与状态转换机制。</summary>
    /// <remarks>· Step(TStep) 流程态属性，各工作流定义自有枚举，读取与变更通知由本类实现。 · SetStep(newStep, reason) 流程态统一转换入口： 合法性校验 → 更新 Step → 设备四态映射 → 触发 StateChanged 事件。 · MapToDeviceState(step) 流程态 → 设备四态映射，抽象方法由子类承载（ADR-041 映射规则）。 · ValidateTransition(from, to) 转换合法性校验钩子，默认放行，子类按需重写。 机器人侧整机流程态（MainDeviceFlowState）与机器人 Pose 安全门控不属本基类， 由 MainDeviceWorkflow 独有承载（子设备无此概念）。 子类禁止手工给 _step / State 赋值，流程态变更一律经 SetStep（ADR-041）。</remarks>
    /// <typeparam name="TStep">工作流自有流程态枚举类型</typeparam>
    public abstract class WorkflowStateBase<TStep> : DeviceStateBase where TStep : struct
    {
        #region 私有变量

        /// <summary>类级复用事件参数（高频事件避免 GC 压力）。</summary>
        private readonly WorkflowStepChangedEventArgs<TStep> _stepArgs =
            new WorkflowStepChangedEventArgs<TStep>();

        #endregion

        #region 公共变量

        /// <summary>流程态字段（子类只读，写入统一经 SetStep 单点）。</summary>
        protected TStep _step;

        /// <summary>当前流程态（各工作流自有枚举，供 UI/主设备读取）。</summary>
        public TStep Step { get { return _step; } }

        /// <summary>流程态变更事件（统一契约，替代各工作流自有 StateChanged 参数类）。</summary>
        public event EventHandler<WorkflowStepChangedEventArgs<TStep>> StateChanged;

        #endregion

        #region 构造函数

        /// <summary>指定初始流程态构造（各工作流构造函数传入自有枚举初始值）。</summary>
        /// <param name="initialStep">初始流程态</param>
        protected WorkflowStateBase(TStep initialStep)
        {
            _step = initialStep;
        }

        #endregion

        #region 流程态转换

        /// <summary>流程态统一转换入口。</summary>
        /// <remarks>合法性校验 → 更新 Step → 设备四态映射（MapToDeviceState）→ 触发 StateChanged 事件 → 记录流程态切换日志。</remarks>
        /// <param name="newStep">目标流程态</param>
        /// <param name="reason">转换原因（写入事件与日志）</param>
        /// <returns>是否完成转换（同值或校验不通过返回 false）</returns>
        protected bool SetStep(TStep newStep, string reason = "")
        {
            if (EqualityComparer<TStep>.Default.Equals(_step, newStep)) return false;
            if (!ValidateTransition(_step, newStep))
            {
                GlobalCommData.ShowLog(StateName, string.Format(
                    "非法状态转换 {0} 到 {1} 已忽略", _step, newStep), MessageLevel.Warning);
                return false;
            }
            TStep old = _step;
            _step = newStep;
            State = MapToDeviceState(newStep);
            var handler = StateChanged;
            if (handler != null)
            {
                _stepArgs.OldState = old;
                _stepArgs.NewState = newStep;
                _stepArgs.Reason = reason;
                _stepArgs.Timestamp = DateTime.Now;
                handler(this, _stepArgs);
            }
            OnStepChanged(_stepArgs);
            GlobalCommData.ShowLog(StateName, string.Format(
                "流程态切换 {0} 到 {1} 原因 {2} 设备态 {3}", old, newStep, reason, State));
            return true;
        }

        #endregion

        #region 映射与校验钩子（子类承载）

        /// <summary>流程态 → 设备四态映射（ADR-041）。</summary>
        /// <remarks>未初始化 → Disconnect / 初始化完成待机 → Connect / 进程中 → Work / 报警 → Alarm。</remarks>
        /// <param name="step">流程态</param>
        /// <returns>对应设备四态</returns>
        protected abstract DeviceState MapToDeviceState(TStep step);

        /// <summary>流程态转换合法性校验钩子。</summary>
        /// <remarks>默认放行；有严格时序约束的工作流重写（如监控相机）。</remarks>
        /// <param name="from">当前流程态</param>
        /// <param name="to">目标流程态</param>
        /// <returns>是否允许转换</returns>
        protected virtual bool ValidateTransition(TStep from, TStep to)
        {
            return true;
        }

        /// <summary>流程态变更后钩子。</summary>
        /// <remarks>在 StateChanged 触发后调用，子类可重写追加处理；禁止在此转发为工作流专属事件（如历史上的 FlowStateSwitched），订阅方统一订阅本类 StateChanged（ADR-042 R3）。</remarks>
        /// <param name="e">流程态变更参数</param>
        protected virtual void OnStepChanged(WorkflowStepChangedEventArgs<TStep> e) { }

        #endregion
    }
}
