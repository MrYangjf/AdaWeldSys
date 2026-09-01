using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;

namespace AdaWeldSystem.MainDeviceControl.FlowState
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>工作流运行模式：None 未运行 / Auto 自动（阻塞式序列线程） / Manual 手动（非阻塞执行步，外部驱动）。</summary>
    /// <remarks>自动与手动共用同一套执行步引擎（FlowProcess），差异仅在推进驱动者：自动由 AutoRunFlow 阻塞脚本排程，手动由外部（主设备/UI）逐步驱动（ADR-048）。</remarks>
    public enum WorkflowRunMode
    {
        /// <summary>未运行</summary>
        None = 0,
        /// <summary>自动运行：后台阻塞式序列（设备自动运行入口）</summary>
        Auto = 1,
        /// <summary>手动运行：后台非阻塞执行步，由外部驱动（界面 UI 入口）</summary>
        Manual = 2
    }

    /// <summary>
    /// 统一工作流基类（泛型 TStep = 各工作流自有流程态枚举）。
    /// 继承链：DeviceStateBase(设备态+设备安全) → WorkflowStateBase&lt;TStep&gt;(流程态+转换机制) → 本类(生命周期+执行步+监听线程)。
    ///
    /// 三层状态模型（ADR-047，**三层并存不是替换**）：
    ///   1. 设备四态 State        —— 经 MapToDeviceState 映射派生，禁止手工赋值（ADR-041）
    ///   2. 阶段态   Step(TStep)  —— 对外名片，经基类 SetStep 变更（ADR-042/043）
    ///   3. 执行步   WorkStep(int) + WorkStartTime —— 内部游标，经 GoStep 成对刷新，承载推进/超时/排故
    /// 一个阶段态可横跨多个执行步；执行步推进到收尾段（800 成功 / 900 失败）时才触发 SetStep 换阶段态。
    ///
    /// 步骤驱动三件套（ADR-047）：
    ///   1. 命令变量：通讯回调只写变量不做业务，子类重写 ConsumeCommand 在监听线程内消费
    ///   2. 常驻监听线程：StartRunLoop / StopRunLoop，固定节拍调用 FlowProcess
    ///   3. 执行步：GoStep(n) 唯一改步入口（步号与时间成对刷新）；非活跃态每拍 ResetStepTime 防误判超时
    ///
    /// 自动/手动双流程（ADR-048）：
    ///   1. 自动 = AutoRun 拉起后台阻塞线程执行 AutoRunFlow 完整序列，等待统一经 AutoWait（有界/可中止），
    ///      中止统一经 IsAutoAbortRequested 探测；设备自动运行调用本入口
    ///   2. 手动 = ManualStart 置 Manual 模式，流程态推进由外部（主设备级联/UI）逐调用驱动，不阻塞调用方；
    ///      界面 UI 调用本入口
    ///   3. 两种模式下 FlowProcess 都由监听线程按节拍驱动（FlowProcess 是执行器，自动线程是排程器，职责正交）
    ///
    /// 红线：FlowProcess 单步内禁止无界阻塞等待（Thread.Sleep / while 轮询），等待靠「节拍重入 + 时间戳判超时」；
    /// 自动序列线程内等待必须经 AutoWait（响应中止 + 可选超时），不得裸写 while(true) 轮询；
    /// 新增执行步必须登记到子类 GetStepName 映射表，否则该步不可观测。
    /// </summary>
    /// <typeparam name="TStep">工作流自有流程态枚举类型</typeparam>
    public abstract class DeviceWorkflowBase<TStep> : WorkflowStateBase<TStep>, IDisposable where TStep : struct
    {
        #region 私有变量

        /// <summary>监听线程节拍（毫秒），默认 10ms（ADR-047 建议 10~20ms）。</summary>
        private int _beatMs = 10;

        /// <summary>执行步游标（volatile 保证跨线程读到最新步号）。</summary>
        private volatile int _workStep;

        /// <summary>当前步进入时间戳（Ticks，Interlocked 读写保证 64 位原子，避免 DateTime 撕裂）。</summary>
        private long _workStartTicks;

        private Thread _runLoopThread;
        private volatile bool _runLoopClosing;
        private volatile bool _disposed;
        private ThreadPriority _runLoopPriority = ThreadPriority.Normal;

        /// <summary>自动流程线程（AutoRun 拉起，一次性；结束自动置空）。</summary>
        private Thread _autoThread;

        /// <summary>自动流程中止标记（volatile，跨线程探测；ManualStop/EmergencyStop/Dispose 置位）。</summary>
        private volatile bool _autoAbortRequested;

        /// <summary>当前运行模式（AutoRun 置 Auto，ManualStart 置 Manual，流程结束回 None）。</summary>
        private volatile WorkflowRunMode _runMode = WorkflowRunMode.None;

        private string _failReason = "";
        private int _failStep;

        #endregion

        #region 公共变量

        /// <summary>通用执行步号：待机（全流程一致，子类不得重复定义）。</summary>
        protected const int StepIdle = 0;

        /// <summary>通用执行步号：成功收尾（全流程一致，子类不得重复定义）。</summary>
        protected const int StepFinishOk = 800;

        /// <summary>通用执行步号：失败收尾（全流程一致，子类不得重复定义）。</summary>
        protected const int StepFinishFail = 900;

        /// <summary>当前执行步号（内部游标，int 编号；段位规范见步骤驱动标准第五章）。</summary>
        public int WorkStep { get { return _workStep; } }

        /// <summary>当前步进入时间（由 GoStep 统一刷新，用于超时判定与可观测性）。</summary>
        public DateTime WorkStartTime { get { return new DateTime(Interlocked.Read(ref _workStartTicks)); } }

        /// <summary>当前步已停留时长（毫秒）。</summary>
        public double StepElapsedMs { get { return (DateTime.Now - WorkStartTime).TotalMilliseconds; } }

        /// <summary>当前执行步的中文名（取自子类 GetStepName 映射表）。</summary>
        public string WorkStepName { get { return GetStepName(_workStep); } }

        /// <summary>可观测性摘要（形如 "step:21 / 3.4s 等待数据稳定"），供 UI 状态标签与日志一次性读取。</summary>
        public string StepStatusText
        {
            get
            {
                return string.Format("step:{0} / {1:F1}s {2}",
                    _workStep, StepElapsedMs / 1000.0, GetStepName(_workStep));
            }
        }

        /// <summary>监听线程是否正在运行。</summary>
        public bool IsRunLoopRunning
        {
            get { return _runLoopThread != null && _runLoopThread.IsAlive; }
        }

        /// <summary>失败原因（由 FailFlow 记录，供失败收尾步写日志与故障快照）。</summary>
        protected string FailReason { get { return _failReason; } }

        /// <summary>监听线程节拍（毫秒），默认 10ms。</summary>
        protected int BeatMs
        {
            get { return _beatMs; }
            set { _beatMs = value > 0 ? value : 10; }
        }

        /// <summary>监听线程是否执行业务（默认 true；无业务流程重写返回 false 以避免空转线程）。</summary>
        protected virtual bool IsRunLoopActive { get { return true; } }

        /// <summary>监听线程优先级，默认 Normal（安全监控等对实时性要求高的流程可提升）。</summary>
        protected ThreadPriority RunLoopPriority
        {
            get { return _runLoopPriority; }
            set { _runLoopPriority = value; }
        }

        /// <summary>触发失败的执行步号（由 FailFlow 记录，用于排故定位卡点）。</summary>
        protected int FailStep { get { return _failStep; } }

        /// <summary>当前运行模式（None 未运行 / Auto 自动 / Manual 手动）。</summary>
        public WorkflowRunMode RunMode { get { return _runMode; } }

        /// <summary>自动流程线程是否正在运行。</summary>
        public bool IsAutoRunning
        {
            get { return _runMode == WorkflowRunMode.Auto && _autoThread != null && _autoThread.IsAlive; }
        }

        /// <summary>自动流程中止标记（自动序列内经 AutoWait / 手动探测，置位后应尽快安全退出）。</summary>
        protected bool IsAutoAbortRequested { get { return _autoAbortRequested; } }

        #endregion

        #region 构造函数

        /// <summary>指定初始流程态构造（透传流程态基类，并初始化执行步时间戳）。</summary>
        /// <param name="initialStep">初始流程态</param>
        protected DeviceWorkflowBase(TStep initialStep) : base(initialStep)
        {
            _workStartTicks = DateTime.Now.Ticks;
        }

        #endregion

        #region 私有函数

        /// <summary>监听线程主体：按节拍消费命令变量并单步分派。</summary>
        /// <remarks>非活跃态持续刷新时间戳；单拍异常只记日志不退出，避免整条流程永久停摆。</remarks>
        private void RunLoop()
        {
            while (!_runLoopClosing)
            {
                try
                {
                    if (IsRunLoopActive)
                    {
                        ConsumeCommand();
                        FlowProcess();
                    }
                    else
                    {
                        // 关键细节：非运行/暂停态必须持续刷新，
                        // 否则暂停 10 分钟后恢复，第一步就会误判超时
                        ResetStepTime();
                    }
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(StateName, "监听线程异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(_beatMs);
            }
        }

        /// <summary>自动流程线程主体：执行子类阻塞式 AutoRunFlow，异常记日志并转失败收尾，结束后回 None 模式。</summary>
        private void AutoThreadBody()
        {
            try
            {
                GlobalCommData.ShowLog(StateName, "自动流程开始", MessageLevel.Info);
                AutoRunFlow();
                GlobalCommData.ShowLog(StateName, "自动流程结束", MessageLevel.Info);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(StateName, "自动流程异常 " + ex.Message, MessageLevel.Error);
                FailFlow("自动流程异常 " + ex.Message);
            }
            finally
            {
                _runMode = WorkflowRunMode.None;
                _autoThread = null;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>启动常驻监听线程（幂等，已运行则直接返回）。</summary>
        public void StartRunLoop()
        {
            if (IsRunLoopRunning) return;
            _runLoopClosing = false;
            _runLoopThread = new Thread(RunLoop)
            {
                Name = StateName + "RunLoop",
                IsBackground = true,
                Priority = _runLoopPriority
            };
            _runLoopThread.Start();
            GlobalCommData.ShowLog(StateName, "监听线程已启动", MessageLevel.Info);
        }

        /// <summary>停止常驻监听线程（Join 上限 1000ms，超时放弃等待）。</summary>
        public void StopRunLoop()
        {
            _runLoopClosing = true;
            if (_runLoopThread != null && _runLoopThread.IsAlive)
                _runLoopThread.Join(1000);
            _runLoopThread = null;
        }

        /// <summary>刷新当前步进入时间。</summary>
        /// <remarks>非运行态每拍调用，防止恢复瞬间误判超时。</remarks>
        public void ResetStepTime()
        {
            Interlocked.Exchange(ref _workStartTicks, DateTime.Now.Ticks);
        }

        /// <summary>释放资源（标准 IDisposable.Dispose，ADR-034）：中止自动流程 + 停监听线程 + 调用子类释放钩子。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _autoAbortRequested = true;
            if (_autoThread != null && _autoThread.IsAlive) _autoThread.Join(1000);
            StopRunLoop();
            DisposeManaged();
            GC.SuppressFinalize(this);
        }

        /// <summary>初始化设备并同步设备四态。</summary>
        /// <returns>初始化成功返回 true，失败置 Disconnect 并返回 false</returns>
        public bool Initialize()
        {
            if (InitializeFlow())
            {
                State = DeviceState.Connect;
                return true;
            }
            State = DeviceState.Disconnect;
            return false;
        }

        /// <summary>手动开：置 Manual 模式，调用 ManualOn 并置 Connect（非阻塞，界面 UI 入口）。</summary>
        public void ManualStart()
        {
            _runMode = WorkflowRunMode.Manual;
            ManualOn();
            State = DeviceState.Connect;
        }

        /// <summary>手动关：自动流程若在跑先请求中止并限时等待，再调用 ManualOff 并置 Disconnect。</summary>
        public void ManualStop()
        {
            if (IsAutoRunning)
            {
                _autoAbortRequested = true;
                if (_autoThread != null) _autoThread.Join(2000);
            }
            _runMode = WorkflowRunMode.None;
            ManualOff();
            State = DeviceState.Disconnect;
        }

        /// <summary>自动运行：拉起后台阻塞线程执行 AutoRunFlow 完整序列并置 Work（设备自动运行入口）。</summary>
        /// <remarks>幂等：自动流程运行中重复调用直接返回。中止经 RequestAutoAbort，等待须用 AutoWait。</remarks>
        public void AutoRun()
        {
            if (IsAutoRunning) return;
            _autoAbortRequested = false;
            _runMode = WorkflowRunMode.Auto;
            State = DeviceState.Work;
            StartRunLoop();
            _autoThread = new Thread(AutoThreadBody)
            {
                Name = StateName + "AutoFlow",
                IsBackground = true
            };
            _autoThread.Start();
        }

        /// <summary>请求中止自动流程（幂等）。自动序列内经 IsAutoAbortRequested / AutoWait 探测后安全退出。</summary>
        public void RequestAutoAbort()
        {
            _autoAbortRequested = true;
        }

        /// <summary>自动流程有界等待：轮询条件直至满足、超时或中止。</summary>
        /// <remarks>仅限自动序列线程（AutoRunFlow 内）调用；FlowProcess 单步内禁用（红线）。
        /// 条件异常按不满足处理并记警告，不中断等待。</remarks>
        /// <param name="condition">等待条件</param>
        /// <param name="timeoutMs">超时阈值（毫秒），须来自常量；传 0 表示不设超时（仍响应中止）</param>
        /// <returns>条件满足返回 true；超时或中止返回 false</returns>
        protected bool AutoWait(Func<bool> condition, double timeoutMs)
        {
            DateTime start = DateTime.Now;
            while (!_autoAbortRequested)
            {
                bool ok = false;
                try
                {
                    ok = condition != null && condition();
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(StateName, "自动流程等待条件异常 " + ex.Message, MessageLevel.Warning);
                }
                if (ok) return true;
                if (timeoutMs > 0 && (DateTime.Now - start).TotalMilliseconds >= timeoutMs) return false;
                Thread.Sleep(_beatMs);
            }
            return false;
        }

        /// <summary>流程复位（公共入口）：记日志后调用子类 ResetFlow。</summary>
        public void Reset()
        {
            GlobalCommData.ShowLog(StateName, "流程复位开始", MessageLevel.Info);
            ResetFlow();
            GlobalCommData.ShowLog(StateName, "流程复位完成", MessageLevel.Info);
        }

        /// <summary>急停：先请求中止自动流程（自动序列在下一拍探测到后安全退出），再走设备级急停。</summary>
        public override void EmergencyStop()
        {
            _autoAbortRequested = true;
            base.EmergencyStop();
        }

        #endregion

        #region 抽象生命周期方法（子类必须实现）

        /// <summary>初始化流程（= 设备 Connect）。返回是否成功连接。</summary>
        protected abstract bool InitializeFlow();

        /// <summary>手动开（= 设备 Connect）。</summary>
        protected abstract void ManualOn();

        /// <summary>手动关（= 设备 Disconnect）。</summary>
        protected abstract void ManualOff();

        /// <summary>自动运行流程（= 设备 Work）。</summary>
        /// <remarks>在自动流程线程内阻塞执行完整序列（ADR-048）；等待统一经 AutoWait，禁止裸 while 轮询。</remarks>
        protected abstract void AutoRunFlow();

        /// <summary>流程复位：清命令变量/计数/报警并回到可启动状态，由公共入口 Reset 调用。</summary>
        protected abstract void ResetFlow();

        /// <summary>单步分派（switch(WorkStep)），由监听线程按节拍反复调用。</summary>
        /// <remarks>红线：case 体内禁止 Thread.Sleep / while 轮询 / 无界同步等待，等待靠「节拍重入 + 时间戳判超时」。</remarks>
        protected abstract void FlowProcess();

        /// <summary>执行步号 → 中文名映射（新增步必须登记，否则该步不可观测）。</summary>
        protected abstract string GetStepName(int step);

        #endregion

        #region 执行步（ADR-047）

        /// <summary>推进执行步（唯一合法的改步入口）。</summary>
        /// <remarks>直接写 WorkStep / WorkStartTime 会破坏「步号与时间成对刷新」约定。</remarks>
        /// <param name="step">目标执行步号（段位规范见步骤驱动标准第五章）</param>
        protected void GoStep(int step)
        {
            _workStep = step;
            Interlocked.Exchange(ref _workStartTicks, DateTime.Now.Ticks);
        }

        /// <summary>状态与执行步清零（含时间戳刷新）。</summary>
        protected virtual void ClearStatus()
        {
            _workStep = 0;
            ResetStepTime();
        }

        /// <summary>判断当前步停留是否超时。</summary>
        /// <remarks>统一超时判据入口，避免超时判断写法发散。</remarks>
        /// <param name="timeoutMs">超时阈值（毫秒），须来自常量</param>
        /// <returns>停留时长超过阈值返回 true</returns>
        protected bool IsStepTimeout(double timeoutMs)
        {
            return StepElapsedMs > timeoutMs;
        }

        /// <summary>转入失败收尾并跳 900 段。</summary>
        /// <remarks>记录卡住的步号与原因；失败原因的日志由子类失败收尾步统一输出，此处不重复记。</remarks>
        /// <param name="reason">失败原因（纯文本，无符号；供失败收尾步写日志与故障快照）</param>
        protected void FailFlow(string reason)
        {
            _failStep = _workStep;
            _failReason = reason;
            GoStep(StepFinishFail);
        }

        #endregion

        #region 监听线程钩子（子类按需重写）

        /// <summary>消费命令变量（子类重写以执行业务）。</summary>
        /// <remarks>通讯回调只写命令变量，业务一律在此执行。</remarks>
        protected virtual void ConsumeCommand() { }

        /// <summary>释放钩子：子类把原 Dispose 内容搬到这里（避免重写 Dispose 破坏 ADR-001）。</summary>
        protected virtual void DisposeManaged() { }

        #endregion

        #region 机器人 Pose 入口（主设备级联，子设备空实现）

        /// <summary>机器人 Pose 到达入口（子设备可空实现）。</summary>
        /// <remarks>主设备据此切换流程态并下发 DeviceCommand、级联子设备 DeviceState。</remarks>
        /// <param name="pose">机器人上报的姿态</param>
        protected virtual void OnRobotPoseReceived(RobotPose pose) { }

        #endregion
    }
}
