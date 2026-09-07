using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.MainDeviceControl.DeviceState;

namespace AdaWeldSystem.MainDeviceControl.FlowState
{
    /// <summary>子设备流程基类（非泛型，17 项契约）。</summary>
    /// <remarks>不自带线程（线程归 DeviceControlWork）；FlowProcess 各 case 内由 bool 判定推进，推进前必须调用 ResetWorkTime()。</remarks>
    public abstract class DeviceWorkflowBase : DeviceStateBase, IDisposable
    {
        #region 私有变量

        /// <summary>执行步游标（volatile 保证跨线程读到最新步号）。</summary>
        private volatile int _runStep;

        /// <summary>当前步进入时间戳（Ticks，Interlocked 读写保证 64 位原子，避免 DateTime 撕裂）。</summary>
        private long _stepStartTicks;

        /// <summary>本流程工作起点时间戳（Ticks，累计工作计时用）。</summary>
        private long _workElapsedTicks;

        /// <summary>超时阈值（秒）。</summary>
        private int _timeOutSeconds = 30;

        /// <summary>是否启用：禁用后编排层不再驱动本流程。</summary>
        private bool _isEnable = true;

        /// <summary>配置读写器（惰性创建，非线程安全）。</summary>
        private XdocumentReaderWriter _config;

        /// <summary>是否已释放。</summary>
        private bool _disposed;

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

        /// <summary>是否启用：未启用的流程不参与编排。</summary>
        public bool IsEnable
        {
            get { return _isEnable; }
            set { _isEnable = value; }
        }

        /// <summary>流程步控：驱动 FlowProcess() / ResetProcess() 的执行位置。</summary>
        /// <remarks>推进步号前必须先调用 ResetWorkTime()，否则步骤计时会把停留时长带入下一步。</remarks>
        public int RunStep
        {
            get { return _runStep; }
            protected set { _runStep = value; }
        }

        /// <summary>步骤用时：自进入当前步以来的时长（ResetWorkTime 归零）。</summary>
        public TimeSpan StepWorkTime
        {
            get { return DateTime.Now - new DateTime(Interlocked.Read(ref _stepStartTicks)); }
        }

        /// <summary>超时计时（秒）：单步停留超过该值即判超时；0 表示不启用超时。</summary>
        public int TimeOutSeconds
        {
            get { return _timeOutSeconds; }
            set { _timeOutSeconds = value > 0 ? value : 0; }
        }

        /// <summary>当前步已停留时长（毫秒）。</summary>
        public double StepElapsedMs { get { return StepWorkTime.TotalMilliseconds; } }

        /// <summary>本流程工作起点（由 ResetWorkTime 之外的 StartWorkTime 刷新，累计工作计时用）。</summary>
        public DateTime WorkTime { get { return new DateTime(Interlocked.Read(ref _workElapsedTicks)); } }

        /// <summary>本流程已工作时长（毫秒）。</summary>
        public double WorkElapsedMs { get { return (DateTime.Now - WorkTime).TotalMilliseconds; } }

        /// <summary>当前执行步的中文名（取自子类 GetStepName 映射表）。</summary>
        public string WorkStepName { get { return GetStepName(_runStep); } }

        /// <summary>可观测性摘要（形如 "step:21 / 3.4s 等待数据稳定"），供 UI 状态标签与日志一次性读取。</summary>
        public string StepStatusText
        {
            get
            {
                return string.Format("step:{0} / {1:F1}s {2}",
                    _runStep, StepWorkTime.TotalSeconds, GetStepName(_runStep));
            }
        }

        /// <summary>子设备配置文件名（不含扩展名），落在 Config/XML/ 下；默认取子类类型名。</summary>
        protected virtual string ConfigFileName { get { return GetType().Name; } }

        /// <summary>子设备配置读写器，首次访问时按 ConfigFileName 惰性创建。</summary>
        /// <remarks>非线程安全：只在初始化 / 停机等非并发阶段调用，运行期禁止在 FlowProcess() 内读写配置。</remarks>
        protected XdocumentReaderWriter Config
        {
            get { return _config ?? (_config = new XdocumentReaderWriter(ConfigFileName)); }
        }

        #endregion

        #region 构造函数

        /// <summary>构造：初始化步骤计时与工作计时时间戳。</summary>
        protected DeviceWorkflowBase()
        {
            long now = DateTime.Now.Ticks;
            _stepStartTicks = now;
            _workElapsedTicks = now;
        }

        #endregion

        #region 私有函数

        /// <summary>释放钩子：子类把原 Dispose 内容搬到这里（避免重写 Dispose 破坏 ADR-001）。</summary>
        protected virtual void DisposeManaged() { }

        #endregion

        #region 公共函数

        /// <summary>释放资源并调用子类释放钩子。</summary>
        /// <remarks>按 ADR-023 使用标准 IDisposable.Dispose()，禁止重写 Dispose(bool)。</remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeManaged();
            GC.SuppressFinalize(this);
        }

        /// <summary>整机下发子设备焊接态迁移请求。</summary>
        /// <remarks>SetWeldStatus 为 protected，本方法供编排层统一下发目标焊接态。</remarks>
        /// <param name="target">目标焊接过程态</param>
        /// <param name="reason">迁移原因</param>
        public void RequestWeldStatus(SubDeviceWeldStatus target, string reason)
        {
            SetWeldStatus(target, reason);
        }

        /// <summary>推进执行步并同步刷新步骤计时。</summary>
        /// <remarks>改步与刷新计时必须成对出现，漏刷新会导致下一步立即误判超时。</remarks>
        /// <param name="step">目标执行步号</param>
        protected void AdvanceStep(int step)
        {
            _runStep = step;
            ResetStepTime();
        }

        /// <summary>刷新步骤计时起点（基类机制，子类 ResetWorkTime() 内必须调用）。</summary>
        protected void ResetStepTime()
        {
            Interlocked.Exchange(ref _stepStartTicks, DateTime.Now.Ticks);
        }

        /// <summary>判断当前步停留是否超时。</summary>
        /// <returns>TimeOutSeconds 大于 0 且停留秒数超过阈值返回 true</returns>
        protected bool IsStepTimeout()
        {
            return _timeOutSeconds > 0 && StepWorkTime.TotalSeconds > _timeOutSeconds;
        }

        /// <summary>统一日志出口：固定使用本流程 Tag 作为标签。</summary>
        /// <param name="message">日志内容（纯文本，无符号）</param>
        /// <param name="level">日志级别</param>
        protected void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>刷新本流程工作起点（累计工作计时用）。</summary>
        protected void SetWorkTimeStart()
        {
            Interlocked.Exchange(ref _workElapsedTicks, DateTime.Now.Ticks);
        }

        /// <summary>读取整型配置项（解析失败回退默认值，避免首次运行文件为空时抛异常）。</summary>
        /// <param name="elementName">配置元素名</param>
        /// <param name="defaultValue">解析失败时的回退值</param>
        /// <returns>解析得到的整数</returns>
        protected int ReadInt(string elementName, int defaultValue)
        {
            int value;
            return int.TryParse(Config.GetElementValue(elementName, defaultValue.ToString()), out value)
                ? value : defaultValue;
        }

        /// <summary>读取双精度配置项（解析失败回退默认值）。</summary>
        /// <param name="elementName">配置元素名</param>
        /// <param name="defaultValue">解析失败时的回退值</param>
        /// <returns>解析得到的双精度值</returns>
        protected double ReadDouble(string elementName, double defaultValue)
        {
            double value;
            return double.TryParse(Config.GetElementValue(elementName, defaultValue.ToString()), out value)
                ? value : defaultValue;
        }

        #endregion

        #region 抽象契约：全阻塞流程（10 项）

        /// <summary>流程处理：以 RunStep 驱动，每个 case 由 bool 判定推进，由 DeviceControlWork 的子设备线程反复调用。</summary>
        /// <remarks>红线：case 体内禁止 Thread.Sleep / while 轮询 / 无界同步等待；未完成时直接结束本次调用，下周期重试。</remarks>
        public abstract void FlowProcess();

        /// <summary>复位流程：线性阻塞链（if (!动作()) return false;），非步控。</summary>
        /// <returns>复位成功返回 true，任一环节失败返回 false</returns>
        public abstract bool ResetProcess();

        /// <summary>状态清理：做设备复位前的状态清理（标志位全复位 + RunStep 归零）。</summary>
        public abstract void ClearStatus();

        /// <summary>清除步骤用时：防止暂停 / 停止期间的时间被计入步骤时长导致异常超时。</summary>
        public abstract void ResetWorkTime();

        /// <summary>设备连接（阻塞）：同步等待至连接完成或超时。</summary>
        /// <returns>连接成功返回 true</returns>
        public abstract bool InitializeOn();

        /// <summary>设备断开（阻塞）：内部异步执行释放，调用方阻塞至完成。</summary>
        /// <returns>断开成功返回 true</returns>
        public abstract bool InitializeOff();

        /// <summary>设备日志：由每个子流程各自重写，输出本设备的差异化日志。</summary>
        public abstract void SubDeviceLog();

        /// <summary>加载配置：无参，内部走本项目的 XdocumentReaderWriter（裁定 D1）。</summary>
        public abstract void LoadConfig();

        /// <summary>保存配置：无参，末尾必须调用 Config.SaveXdocument() 落盘。</summary>
        public abstract void SaveConfig();

        /// <summary>焊接工作状态切换管控。</summary>
        public abstract void WeldStatusTrans();

        #endregion

        #region 抽象契约：非阻塞流程（2 项）

        /// <summary>开启连接（非阻塞）：动作即退出释放，结果由 SubDeviceState 异步反映。</summary>
        /// <returns>受理返回 true，拒绝返回 false</returns>
        public abstract bool ConnectOn();

        /// <summary>关闭连接（非阻塞）：动作即退出释放，结果由 SubDeviceState 异步反映。</summary>
        /// <returns>受理返回 true，拒绝返回 false</returns>
        public abstract bool ConnectOff();

        #endregion

        #region 抽象契约：可观测性

        /// <summary>执行步号映射中文名，新增步必须登记。</summary>
        /// <param name="step">执行步号</param>
        /// <returns>该步的中文名</returns>
        protected abstract string GetStepName(int step);

        #endregion
    }
}
