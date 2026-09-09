using System;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MainDeviceControl.DeviceState
{
    /// <summary>子设备连接态（两态，ConnectOn/ConnectOff 联动）。</summary>
    public enum SubDeviceState
    {
        Disconnected = 0,
        Connected = 1
    }

    /// <summary>子设备焊接过程态（7 态，ADR-026 裁定 C2）。</summary>
    public enum SubDeviceWeldStatus
    {
        NoReset = 0,
        Standby = 1,
        PreWork = 2,
        Working = 3,
        Stopping = 4,
        ErrorAborted = 5,
        ManualStopped = 6
    }

    /// <summary>主设备运行态（6 态，DeviceControlWork 承载）。</summary>
    public enum MainDeviceStatus
    {
        Running = 0,
        Alarm = 1,
        EStop = 2,
        Stop = 3,
        NoReset = 4,
        Reseting = 5
    }

    /// <summary>子设备连接态变更事件参数。</summary>
    public class SubDeviceStateChangedEventArgs : EventArgs
    {
        /// <summary>切换前连接态。</summary>
        public SubDeviceState OldState { get; set; }

        /// <summary>切换后连接态。</summary>
        public SubDeviceState NewState { get; set; }

        /// <summary>切换原因。</summary>
        public string Reason { get; set; }

        /// <summary>变更时间。</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>子设备焊接过程态变更事件参数。</summary>
    public class WeldStatusChangedEventArgs : EventArgs
    {
        /// <summary>切换前焊接过程态。</summary>
        public SubDeviceWeldStatus OldStatus { get; set; }

        /// <summary>切换后焊接过程态。</summary>
        public SubDeviceWeldStatus NewStatus { get; set; }

        /// <summary>切换原因。</summary>
        public string Reason { get; set; }

        /// <summary>变更时间。</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>子设备态基类（连接态 + 焊接过程态 + 设备安全）。</summary>
    /// <remarks>主设备运行态 MainDeviceStatus 不在本类承载，由 DeviceControlWork 统一持有。</remarks>
    public abstract class DeviceStateBase
    {
        #region 私有变量

        private SubDeviceState _state = SubDeviceState.Disconnected;
        private SubDeviceWeldStatus _weldStatus = SubDeviceWeldStatus.NoReset;
        private bool _isAlarm;
        private string _lastReason;
        private bool _connectSettled;
        private string _connectResult = string.Empty;
        private readonly object _connectLock = new object();

        #endregion

        #region 公共变量

        /// <summary>当前子设备连接态。</summary>
        public SubDeviceState State
        {
            get { return _state; }
            protected set
            {
                if (_state == value) return;
                SubDeviceState old = _state;
                _state = value;
                OnStateSwitched(old, value);
            }
        }

        /// <summary>当前子设备焊接过程态（单控制源，由流程方法联动驱动）。</summary>
        public SubDeviceWeldStatus WeldStatus
        {
            get { return _weldStatus; }
            protected set
            {
                if (_weldStatus == value) return;
                SubDeviceWeldStatus old = _weldStatus;
                _weldStatus = value;
                OnWeldStatusChanged(old, value);
            }
        }

        /// <summary>设备安全：是否处于急停/告警态。</summary>
        public bool IsAlarm
        {
            get { return _isAlarm; }
            protected set
            {
                if (_isAlarm == value) return;
                _isAlarm = value;
                if (_isAlarm)
                {
                    var handler = AlarmOccurred;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>子设备连接态切换事件。</summary>
        public event EventHandler<SubDeviceStateChangedEventArgs> StateSwitched;

        /// <summary>子设备焊接过程态变更事件。</summary>
        public event EventHandler<WeldStatusChangedEventArgs> WeldStatusChanged;

        /// <summary>急停/告警发生事件。</summary>
        public event EventHandler AlarmOccurred;

        /// <summary>连接尝试是否已出结果（成功或失败都置 true）。</summary>
        /// <remarks>用于把「已确认失败」与「尚未有反馈」区分开：前者立即收敛，后者才需要等超时。</remarks>
        public bool ConnectSettled
        {
            get { lock (_connectLock) { return _connectSettled; } }
        }

        /// <summary>最近一次连接尝试的结果描述（成功为「已连接」，失败为失败原因）。</summary>
        public string ConnectResult
        {
            get { lock (_connectLock) { return _connectResult; } }
        }

        /// <summary>设备名称（日志/事件标识），子类必须实现。</summary>
        public abstract string StateName { get; }

        #endregion

        #region 公共函数

        /// <summary>切换连接态（带原因），State 唯一写入口。</summary>
        /// <param name="newState">目标连接态</param>
        /// <param name="reason">切换原因（记入事件参数与日志）</param>
        protected void SetState(SubDeviceState newState, string reason)
        {
            _lastReason = reason;
            if (newState == SubDeviceState.Connected)
                SettleConnect(string.IsNullOrEmpty(reason) ? "已连接" : reason);
            State = newState;
        }

        /// <summary>发起一次连接尝试（连接动作开始前调用，复位上次结果）。</summary>
        /// <remarks>调用后 <see cref="ConnectSettled"/> 置 false，直到成功置 Connected 或调用 <see cref="MarkConnectFailed"/>。</remarks>
        public void BeginConnectAttempt()
        {
            lock (_connectLock)
            {
                _connectSettled = false;
                _connectResult = string.Empty;
            }
        }

        /// <summary>标记连接尝试已失败并置 Disconnected。</summary>
        /// <remarks>异步连接线程拿到确定失败结果时调用，使等待方不必等超时即可收敛。</remarks>
        /// <param name="reason">失败原因（记入 <see cref="ConnectResult"/> 与状态切换原因）</param>
        public void MarkConnectFailed(string reason)
        {
            string text = string.IsNullOrEmpty(reason) ? "连接失败" : reason;
            SettleConnect(text);
            SetState(SubDeviceState.Disconnected, text);
        }

        /// <summary>切换焊接过程态（带原因），WeldStatus 唯一写入口。</summary>
        /// <remarks>变更后执行步由调用方自行推进；与旧 SetStep 语义对齐。</remarks>
        /// <param name="newStatus">目标焊接过程态</param>
        /// <param name="reason">切换原因（记入事件参数与日志）</param>
        protected void SetWeldStatus(SubDeviceWeldStatus newStatus, string reason)
        {
            _lastReason = reason;
            WeldStatus = newStatus;
        }

        /// <summary>设备安全：急停。</summary>
        /// <remarks>默认置 IsAlarm=true 并记录日志；子类（主设备侧）应重写以调用具体运动控制器/IO 的紧急停止。</remarks>
        public virtual void EmergencyStop()
        {
            IsAlarm = true;
            GlobalCommData.ShowLog(StateName, "设备急停", MessageLevel.Error);
        }

        #endregion

        #region 私有函数

        /// <summary>落定连接结果（线程安全写入）。</summary>
        /// <param name="result">结果描述</param>
        private void SettleConnect(string result)
        {
            lock (_connectLock)
            {
                _connectSettled = true;
                _connectResult = result;
            }
        }

        /// <summary>读取设备 IO 安全互锁。</summary>
        /// <returns>当前是否安全（默认实现恒安全）</returns>
        protected virtual bool CheckDeviceIOSafe()
        {
            return true;
        }

        /// <summary>连接态切换钩子：触发 StateSwitched 事件（切换不记日志）。</summary>
        /// <param name="oldState">切换前连接态</param>
        /// <param name="newState">切换后连接态</param>
        protected virtual void OnStateSwitched(SubDeviceState oldState, SubDeviceState newState)
        {
            var handler = StateSwitched;
            if (handler != null)
            {
                    handler(this, new SubDeviceStateChangedEventArgs
                    {
                        OldState = oldState,
                        NewState = newState,
                        Reason = _lastReason,
                        Timestamp = DateTime.Now
                    });
                }
                // 状态切换不记日志（ADR-028 报错分层：连接态经事件/状态栏呈现）
            }

        /// <summary>焊接过程态变更钩子：触发 WeldStatusChanged 事件并记录日志。</summary>
        /// <param name="oldStatus">切换前焊接过程态</param>
        /// <param name="newStatus">切换后焊接过程态</param>
        protected virtual void OnWeldStatusChanged(SubDeviceWeldStatus oldStatus, SubDeviceWeldStatus newStatus)
        {
            var handler = WeldStatusChanged;
            if (handler != null)
            {
                    handler(this, new WeldStatusChangedEventArgs
                    {
                        OldStatus = oldStatus,
                        NewStatus = newStatus,
                        Reason = _lastReason,
                        Timestamp = DateTime.Now
                    });
                }
                // 状态切换不记日志（ADR-028 报错分层：焊接过程态经事件/状态栏呈现）
            }

        #endregion
    }
}
