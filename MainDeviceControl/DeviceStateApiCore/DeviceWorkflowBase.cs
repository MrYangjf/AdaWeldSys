using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MainDeviceControl.DeviceState;

namespace AdaWeldSystem.MainDeviceControl.FlowState
{
    /// <summary>
    /// 运行基类（非泛型，新主控设计）：承载执行步 + 常驻监听线程 + 连接/焊接过程态联动。
    /// 继承链：DeviceStateBase(连接态 State + 焊接过程态 WeldStatus + 设备安全) → 本类(生命周期 + 执行步 + 监听线程)。
    /// 不再实现流程态枚举与状态转换表（原 WorkflowStateBase 已删除）；对外状态即 SubDeviceState / SubDeviceWeldStatus。
    ///
    /// 抽象契约（6 项，新主控设计）：
    ///   1. FlowProcess()     流程处理：switch(WorkStep) 单步分派，由监听线程按节拍反复调用
    ///   2. ResetProcess()    复位流程：返回是否受理（受理式非阻塞，完成以 WeldStatus 信号化）
    ///   3. ClearStatus()     状态清除：清命令变量/计数/报警标志（实现内必须调用 ResetWorkStep()）
    ///   4. ResetWorkTime()   复位时间：重置工作时间（实现内必须调用 SetWorkTimeStart()）
    ///   5. ConnectOn()       开启连接：受理式非阻塞，子类内部自起线程，完成以 State=Connected 信号化
    ///   6. ConnectOff()      关闭连接：非阻塞，完成以 State=Disconnected 信号化
    /// 状态联动约定：ConnectOn/ConnectOff 联动 State(SubDeviceState)；
    /// FlowProcess/ResetProcess/ClearStatus/ResetWorkTime 联动 WeldStatus(SubDeviceWeldStatus)。
    ///
    /// 步骤驱动三件套（ADR-047 机制延续）：
    ///   1. 命令变量：通讯回调只写变量不做业务，子类重写 ConsumeCommand 在监听线程内消费
    ///   2. 常驻监听线程：StartRunLoop / StopRunLoop，固定节拍调用 FlowProcess
    ///   3. 执行步：GoStep(n) 唯一改步入口（步号与时间成对刷新）；非活跃态每拍 ResetStepTime 防误判超时
    ///
    /// 红线：FlowProcess 单步内禁止无界阻塞等待（Thread.Sleep / while 轮询），等待靠「节拍重入 + 时间戳判超时」；
    /// ConnectOn/ConnectOff 内部自起线程，不得阻塞 UI 线程；
    /// 新增执行步必须登记到子类 GetStepName 映射表，否则该步不可观测。
    /// </summary>
    public abstract class DeviceWorkflowBase : DeviceStateBase, IDisposable
    {
        #region 私有变量

        /// <summary>监听线程节拍（毫秒），默认 10ms（ADR-047 建议 10~20ms）。</summary>
        private int _beatMs = 10;

        /// <summary>执行步游标（volatile 保证跨线程读到最新步号）。</summary>
        private volatile int _workStep;

        /// <summary>当前步进入时间戳（Ticks，Interlocked 读写保证 64 位原子，避免 DateTime 撕裂）。</summary>
        private long _workStartTicks;

        /// <summary>工作时间起点时间戳（Ticks，本流程累计工作计时用）。</summary>
        private long _workElapsedTicks;

        private Thread _runLoopThread;
        private volatile bool _runLoopClosing;
        private volatile bool _disposed;
        private ThreadPriority _runLoopPriority = ThreadPriority.Normal;

        private string _failReason = "";
        private int _failStep;

        private bool _isEnable = true;
        private int _workStepCount;
        private double _timeoutMs;

        #endregion

        #region 公共变量

        /// <summary>通用执行步号：待机（全流程一致，子类不得重复定义）。</summary>
        protected const int StepIdle = 0;

        /// <summary>通用执行步号：成功收尾（全流程一致，子类不得重复定义）。</summary>
        protected const int StepFinishOk = 800;

        /// <summary>通用执行步号：失败收尾（全流程一致，子类不得重复定义）。</summary>
        protected const int StepFinishFail = 900;

        /// <summary>日志标签（流程名）：子类构造函数内赋值，禁止子类再自造 _tag 常量遮蔽本属性。</summary>
        public string Tag { get; protected set; }

        /// <summary>是否启用（禁用后监听线程不执行业务，仅刷新时间戳）。</summary>
        public bool IsEnable
        {
            get { return _isEnable; }
            set { _isEnable = value; }
        }

        /// <summary>工作步数：本流程总步骤数（子类构造时设定，供进度显示）。</summary>
        public int WorkStepCount
        {
            get { return _workStepCount; }
            set { _workStepCount = value; }
        }

        /// <summary>超时时间（毫秒）：单步默认超时阈值，0 表示不启用默认超时。</summary>
        public double TimeoutMs
        {
            get { return _timeoutMs; }
            set { _timeoutMs = value; }
        }

        /// <summary>当前执行步号（内部游标，int 编号；段位规范见步骤驱动标准第五章）。</summary>
        public int WorkStep { get { return _workStep; } }

        /// <summary>当前步进入时间（由 GoStep 统一刷新，用于超时判定与可观测性）。</summary>
        public DateTime WorkStartTime { get { return new DateTime(Interlocked.Read(ref _workStartTicks)); } }

        /// <summary>当前步已停留时长（毫秒）。</summary>
        public double StepElapsedMs { get { return (DateTime.Now - WorkStartTime).TotalMilliseconds; } }

        /// <summary>工作时间起点（由 SetWorkTimeStart 刷新，本流程累计工作计时用）。</summary>
        public DateTime WorkTime { get { return new DateTime(Interlocked.Read(ref _workElapsedTicks)); } }

        /// <summary>本流程已工作时长（毫秒）。</summary>
        public double WorkElapsedMs { get { return (DateTime.Now - WorkTime).TotalMilliseconds; } }

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

        #endregion

        #region 构造函数

        /// <summary>构造：初始化执行步与工作时间时间戳。</summary>
        protected DeviceWorkflowBase()
        {
            long now = DateTime.Now.Ticks;
            _workStartTicks = now;
            _workElapsedTicks = now;
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
                    if (IsRunLoopActive && _isEnable)
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
                    Log("监听线程异常 " + ex.Message, MessageLevel.Error);
                }
                Thread.Sleep(_beatMs);
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
            Log("监听线程已启动", MessageLevel.Info);
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

        /// <summary>释放监听线程并调用子类释放钩子。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopRunLoop();
            DisposeManaged();
            GC.SuppressFinalize(this);
        }

        /// <summary>统一日志出口：固定使用本流程 Tag 作为标签。</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        protected void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            DeviceLog.Write(Tag, message, level);
        }

        #endregion

        #region 抽象契约（新主控设计 6 项，子类必须实现）

        /// <summary>流程处理：switch(WorkStep) 单步分派，由监听线程按节拍反复调用，自动运行唯一执行器。</summary>
        /// <remarks>红线：case 体内禁止 Thread.Sleep / while 轮询 / 无界同步等待，等待靠「节拍重入 + 时间戳判超时」；
        /// 联动 WeldStatus（进入焊接过程段置 Working，收尾回 Standby）。</remarks>
        public abstract void FlowProcess();

        /// <summary>复位流程：清执行步与命令变量并回到可启动状态。</summary>
        /// <remarks>受理式非阻塞，返回是否受理；复位完成以 WeldStatus 信号化（回 Standby）。</remarks>
        /// <returns>受理返回 true，当前不可复位（如流程运行中）返回 false</returns>
        public abstract bool ResetProcess();

        /// <summary>状态清除：报警后停止的处理流程（仅清报警标志与状态残留，与复位正交）。</summary>
        /// <remarks>实现内必须调用 ResetWorkStep() 清执行步；联动 WeldStatus（回 Standby）。</remarks>
        public abstract void ClearStatus();

        /// <summary>复位时间：重置本流程工作时间计时。</summary>
        /// <remarks>实现内必须调用 SetWorkTimeStart()。</remarks>
        public abstract void ResetWorkTime();

        /// <summary>开启连接：受理式非阻塞，立即返回。</summary>
        /// <remarks>子类内部须自行拉起后台线程执行连接并置 State=Connected（信号化完成），本方法不得阻塞 UI 线程；
        /// 连接失败/超时由子类自行反馈（Info），不置 Alarm；联动 State(SubDeviceState)。</remarks>
        /// <returns>受理返回 true（已在连接中或已连接也视为受理），拒绝返回 false</returns>
        public abstract bool ConnectOn();

        /// <summary>关闭连接：非阻塞，立即返回。</summary>
        /// <remarks>子类内部须自行拉起后台线程执行断开并置 State=Disconnected，本方法不得阻塞 UI 线程；联动 State(SubDeviceState)。</remarks>
        public abstract void ConnectOff();

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

        /// <summary>执行步清零并刷新时间戳。</summary>
        protected void ResetWorkStep()
        {
            _workStep = 0;
            ResetStepTime();
        }

        /// <summary>刷新工作时间起点。</summary>
        protected void SetWorkTimeStart()
        {
            Interlocked.Exchange(ref _workElapsedTicks, DateTime.Now.Ticks);
        }

        /// <summary>判断当前步停留是否超时（按显式阈值）。</summary>
        /// <remarks>统一超时判据入口，避免超时判断写法发散。</remarks>
        /// <param name="timeoutMs">超时阈值（毫秒），须来自常量</param>
        /// <returns>停留时长超过阈值返回 true</returns>
        protected bool IsStepTimeout(double timeoutMs)
        {
            return StepElapsedMs > timeoutMs;
        }

        /// <summary>判断当前步是否超时。</summary>
        /// <returns>TimeoutMs 大于 0 且停留时长超过阈值返回 true</returns>
        protected bool IsStepTimeout()
        {
            return _timeoutMs > 0 && StepElapsedMs > _timeoutMs;
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
    }
}
