﻿using System;
using System.Threading;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.ConfigFileControl.Implementations;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.MainDeviceControl.DeviceStateApiCore
{
    /// <summary>流程运行模式：动作来源分类。</summary>
    public enum WorkflowRunMode
    {
        /// <summary>自动：初始化、编排、后台流程触发。</summary>
        Auto = 0,

        /// <summary>手动：界面按钮触发的单次动作。</summary>
        Manual = 1
    }

    /// <summary>就绪分层：从断连到工作零位。</summary>
    public enum ReadinessStage
    {
        /// <summary>断连：L0~L4 全部失效。</summary>
        Disconnected = 0,

        /// <summary>L0 已连接。</summary>
        Connected = 1,

        /// <summary>L1 已使能。</summary>
        Enabled = 2,

        /// <summary>L2 已使能但未回零（需回零轴）。</summary>
        EnabledNotHomed = 3,

        /// <summary>L3 已回零（免回零轴为位置可信）。</summary>
        Homed = 4,

        /// <summary>L4 已到工作零位。</summary>
        AtWorkZero = 5
    }

    /// <summary>模组流程基类。</summary>
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

        /// <summary>流程运行模式</summary>
        private volatile WorkflowRunMode _runMode = WorkflowRunMode.Auto;

        #endregion

        #region 公共变量

        /// <summary>通用执行步号：待机（子类不得重复定义）。</summary>
        protected const int StepIdle = 0;

        /// <summary>通用执行步号：成功收尾（子类不得重复定义）。</summary>
        protected const int StepFinishOk = 800;

        /// <summary>通用执行步号：失败收尾（子类不得重复定义）。</summary>
        protected const int StepFinishFail = 900;

        /// <summary>日志标签（流程名）：子类构造内赋值，禁自造 _tag。</summary>
        public string Tag { get; protected set; }

        /// <summary>是否启用：未启用的流程不参与编排。</summary>
        public bool IsEnable
        {
            get { return _isEnable; }
            set { _isEnable = value; }
        }

        /// <summary>流程运行模式：自动（初始化/编排）或手动（界面按钮）。</summary>
        public WorkflowRunMode RunMode
        {
            get { return _runMode; }
        }

        /// <summary>流程步控：驱动 FlowProcess() / ResetProcess() 的执行位置。</summary>
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

        /// <summary>超时计时（秒）</summary>
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

        /// <summary>可观测性摘要（供 UI 状态标签与日志读取）。</summary>
        public string StepStatusText
        {
            get
            {
                return string.Format("step:{0} / {1:F1}s {2}",
                    _runStep, StepWorkTime.TotalSeconds, GetStepName(_runStep));
            }
        }

        /// <summary>模组配置文件名（不含扩展名）；默认取子类类型名。</summary>
        protected virtual string ConfigFileName { get { return GetType().Name; } }

        /// <summary>模组配置所属模块</summary>
        protected virtual string ConfigModule { get { return "MainDeviceControl"; } }

        /// <summary>模组配置读写器，首次访问时按 ConfigModule + ConfigFileName 惰性创建。</summary>
        protected XdocumentReaderWriter Config
        {
            get
            {
                if (_config == null)
                {
                    _config = ConfigFileManager.Instance.GetModuleXml(ConfigModule, ConfigFileName).Raw;
                }
                return _config;
            }
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

        /// <summary>释放钩子：子类在此实现资源释放。</summary>
        protected virtual void DisposeManaged() { }

        #endregion

        #region 公共函数

        /// <summary>最近一条流程日志（供 UI 失败时写 Tooltip）。</summary>
        public string LastActionMessage
        {
            get { return _lastActionMessage; }
        }

        /// <summary>释放资源并调用子类释放钩子。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeManaged();
            GC.SuppressFinalize(this);
        }

        /// <summary>整机下发模组焊接态迁移请求。</summary>

        /// <param name="target">目标焊接过程态</param>
        /// <param name="reason">迁移原因</param>
        public void RequestWeldStatus(SubDeviceWeldStatus target, string reason)
        {
            SetWeldStatus(target, reason);
        }

        /// <summary>置位流程运行模式：动作入口在调用 ConnectOn/ConnectOff 前显式设置。</summary>
        /// <param name="mode">自动 / 手动</param>
        public void SetRunMode(WorkflowRunMode mode)
        {
            _runMode = mode;
        }

        /// <summary>模式中文名：自动 / 手动。</summary>
        /// <param name="mode">运行模式</param>
        /// <returns>模式中文名</returns>
        public static string ModeName(WorkflowRunMode mode)
        {
            return mode == WorkflowRunMode.Manual ? "手动" : "自动";
        }

        /// <summary>当前模式中文名</summary>
        protected string ModeText
        {
            get { return ModeName(_runMode); }
        }

        /// <summary>按模式输出动作日志：自动 / 手动前缀由模式甄别。</summary>
        /// <param name="mode">动作发起时捕获的模式</param>
        /// <param name="action">动作名（如「连接开始」）</param>
        /// <param name="level">日志级别</param>
        protected void LogAction(WorkflowRunMode mode, string action, MessageLevel level = MessageLevel.Info)
        {
            Log(ModeName(mode) + action, level);
        }

        /// <summary>推进执行步并同步刷新步骤计时。</summary>

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
            _lastActionMessage = message;
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private volatile string _lastActionMessage = string.Empty;

        /// <summary>刷新本流程工作起点（累计工作计时用）。</summary>
        protected void SetWorkTimeStart()
        {
            Interlocked.Exchange(ref _workElapsedTicks, DateTime.Now.Ticks);
        }

        /// <summary>读取整型配置项（解析失败回退默认值，避免首次</summary>

        /// <param name="elementName">配置元素名</param>
        /// <param name="defaultValue">解析失败时的回退值</param>
        /// <returns>解析得到的整数</returns>
        protected int ReadInt(string elementName, int defaultValue)
        {
            return int.TryParse(Config.GetElementValue(elementName, defaultValue.ToString()), out int value)
                ? value : defaultValue;
        }

        /// <summary>读取双精度配置项（解析失败回退默认值）。</summary>
        /// <param name="elementName">配置元素名</param>
        /// <param name="defaultValue">解析失败时的回退值</param>
        /// <returns>解析得到的双精度值</returns>
        protected double ReadDouble(string elementName, double defaultValue)
        {
            return double.TryParse(Config.GetElementValue(elementName, defaultValue.ToString()), out double value)
                ? value : defaultValue;
        }

        #endregion

        #region 就绪分层（L0~L4）

        /// <summary>本流程是否已连接（L0 前提）。</summary>
        public virtual bool IsConnected
        {
            get { return State == SubDeviceState.Connected; }
        }

        /// <summary>本流程依赖轴是否已使能（L1 前提）。</summary>
        public virtual bool IsEnabled { get { return false; } }

        /// <summary>本流程依赖轴是否已回零（L3 前提）。</summary>
        public virtual bool IsHomed { get { return false; } }

        /// <summary>本流程是否已到工作零位（L4 前提）。</summary>
        public virtual bool IsAtWorkZero { get { return false; } }

        /// <summary>依赖轴是否需要回零（轴级能力）。</summary>
        protected virtual bool NeedHoming { get { return false; } }

        /// <summary>依赖轴是否具备绝对编码器（免回零）。</summary>
        protected virtual bool HasAbsoluteEncoder { get { return false; } }

        /// <summary>工作零位来源说明（日志与拒绝原因用）。</summary>
        public virtual string WorkZeroSource { get { return "无"; } }

        /// <summary>本流程最低门槛（子类按用途声明）。</summary>
        public virtual ReadinessStage MinReadinessStage { get { return ReadinessStage.Connected; } }

        /// <summary>未达门槛的可读原因（空串表示无额外原因）。</summary>
        public virtual string ReadinessReason { get { return string.Empty; } }

        /// <summary>分层就绪等级：只按本流程依赖轴求值。</summary>
        public virtual ReadinessStage ReadinessStage
        {
            get
            {
                if (!IsConnected) return ReadinessStage.Disconnected;
                if (!IsEnabled) return ReadinessStage.Connected;
                if (NeedHoming && !IsHomed) return ReadinessStage.EnabledNotHomed;
                if (!IsHomed) return ReadinessStage.Enabled;
                if (!IsAtWorkZero) return ReadinessStage.Homed;
                return ReadinessStage.AtWorkZero;
            }
        }

        /// <summary>就绪等级中文名（含 L 号）。</summary>
        /// <param name="stage">就绪等级</param>
        /// <returns>中文名</returns>
        public static string ReadinessName(ReadinessStage stage)
        {
            switch (stage)
            {
                case ReadinessStage.Disconnected: return "未连接";
                case ReadinessStage.Connected: return "L0 已连接";
                case ReadinessStage.Enabled: return "L1 已使能";
                case ReadinessStage.EnabledNotHomed: return "L2 未回零";
                case ReadinessStage.Homed: return "L3 已回零";
                default: return "L4 已到工作零位";
            }
        }

        /// <summary>当前等级是否达到要求。</summary>
        /// <param name="required">要求等级</param>
        /// <returns>达到返回 true</returns>
        public bool IsReadinessAtLeast(ReadinessStage required)
        {
            return (int)ReadinessStage >= (int)required;
        }

        /// <summary>门槛判定：未达即给「当前 L / 需 L」原因。</summary>
        /// <param name="required">要求等级</param>
        /// <param name="action">动作名</param>
        /// <param name="reason">拒绝原因；达到为空串</param>
        /// <returns>达到返回 true</returns>
        public bool EnsureReadiness(ReadinessStage required, string action, out string reason)
        {
            ReadinessStage current = ReadinessStage;
            if ((int)current >= (int)required)
            {
                reason = string.Empty;
                return true;
            }

            string detail = ReadinessReason;
            reason = string.Format("就绪不足 当前 {0} 需 {1} 动作 {2} 原因 {3}",
                ReadinessName(current), ReadinessName(required), action,
                string.IsNullOrEmpty(detail) ? "未说明" : detail);
            Log("拒绝 " + reason, MessageLevel.Warning);
            return false;
        }

        #endregion

        #region 抽象契约：常驻流程（设备主流程，10 项）
        /// <summary>流程处理：以 RunStep 驱动，每步由 bool 判定推进。</summary>
        public abstract void FlowProcess();

        /// <summary>复位流程：线性阻塞链（if (!动作()) return false;），非步控。</summary>
        /// <returns>复位成功返回 true，任一环节失败返回 false</returns>
        public abstract bool ResetProcess();

        /// <summary>状态清理：复位前清理标志位并归零 RunStep。</summary>
        public abstract void ClearStatus();

        /// <summary>清除步骤用时，防止暂停/停止期计入超时。</summary>
        public abstract void ResetWorkTime();

        /// <summary>设备连接（阻塞）：同步等待至连接完成或超时。</summary>
        /// <returns>连接成功返回 true</returns>
        public abstract bool InitializeOn();

        /// <summary>设备断开（阻塞）：调用方阻塞至释放完成。</summary>
        /// <returns>断开成功返回 true</returns>
        public abstract bool InitializeOff();

        /// <summary>设备日志：子流程各自重写，输出差异化日志。</summary>
        public abstract void SubDeviceLog();

        /// <summary>加载配置：无参，内部走 XdocumentReaderWriter。</summary>
        public abstract void LoadConfig();

        /// <summary>保存配置：无参，末尾必须调用 Config.SaveXdocument() 落盘。</summary>
        public abstract void SaveConfig();

        /// <summary>焊接工作状态切换管控。</summary>
        public abstract void WeldStatusTrans();

        #endregion

        #region 抽象契约：非阻塞动作指令（2 项）

        /// <summary>开启连接（非阻塞）：结果由 SubDeviceState 异步反映。</summary>
        /// <returns>受理返回 true，拒绝返回 false</returns>
        public abstract bool ConnectOn();

        /// <summary>关闭连接（非阻塞）：结果由 SubDeviceState 异步反映。</summary>
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
