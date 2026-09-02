using System;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MainDeviceControl.DeviceState
{
    /// <summary>
    /// 子设备连接态：仅两态。
    ///   · Disconnected 未连接/已断开
    ///   · Connected    已连接
    /// 由 ConnectOn()/ConnectOff() 联动驱动（新主控设计，取代原设备四态 DeviceState）。
    /// </summary>
    public enum SubDeviceState
    {
        Disconnected = 0,
        Connected = 1
    }

    /// <summary>
    /// 子设备焊接过程态（7 态）：
    ///   · Standby        待机
    ///   · PreWork        焊接前准备
    ///   · Starting       启动中
    ///   · Working        工作中
    ///   · Stopping       停止中（转回 Standby）
    ///   · ErrorAborted   异常终止
    ///   · ManualStopped  手动停止
    /// 由 FlowProcess()/ResetProcess()/ClearStatus()/ResetWorkTime() 联动驱动。
    /// </summary>
    public enum SubDeviceWeldStatus
    {
        Standby = 0,
        PreWork = 1,
        Starting = 2,
        Working = 3,
        Stopping = 4,
        ErrorAborted = 5,
        ManualStopped = 6
    }

    /// <summary>
    /// 主设备运行态（6 态）：原流程态不再存在，改为主设备过程状态。
    ///   · Running  运行中
    ///   · Alarm    报警
    ///   · EStop    急停
    ///   · Stop     停止
    ///   · NoReset  未复位
    ///   · Reseting 复位中
    /// 由 DeviceControlWork 统一承载与驱动。
    /// </summary>
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

    /// <summary>
    /// 子设备态基类（继承链最底层）：承载子设备连接态 State + 焊接过程态 WeldStatus + 设备安全。
    /// </summary>
    /// <remarks>
    /// · State(SubDeviceState) 连接态，由子类经 ConnectOn/ConnectOff 联动驱动，变更经 StateSwitched 事件通知。
    /// · WeldStatus(SubDeviceWeldStatus) 焊接过程态，由子类经流程方法联动驱动，变更经 WeldStatusChanged 事件通知。
    /// · IsAlarm 设备安全标志；EmergencyStop() 急停（子类可重写以调用具体运动控制器急停）。
    /// · CheckDeviceIOSafe() 设备 IO 安全互锁读取（子类重写，返回是否安全）。
    /// 主设备运行态 MainDeviceStatus 不在本类承载，由 DeviceControlWork 统一持有。
    /// </remarks>
    public abstract class DeviceStateBase
    {
        #region 私有变量

        private SubDeviceState _state = SubDeviceState.Disconnected;
        private SubDeviceWeldStatus _weldStatus = SubDeviceWeldStatus.Standby;
        private bool _isAlarm;
        private string _lastReason;

        #endregion

        #region 公共变量

        /// <summary>当前子设备连接态（单控制源，由 ConnectOn/ConnectOff 联动驱动）。</summary>
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
            State = newState;
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
            DeviceLog.Write(StateName, "设备急停", MessageLevel.Error);
        }

        #endregion

        #region 私有函数

        /// <summary>读取设备 IO 安全互锁状态。</summary>
        /// <returns>当前是否安全（默认实现恒安全）</returns>
        protected virtual bool CheckDeviceIOSafe()
        {
            return true;
        }

        /// <summary>连接态切换钩子：触发 StateSwitched 事件并记录日志。</summary>
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
            DeviceLog.Write(StateName,
                string.Format("连接态切换 {0} -> {1}", oldState, newState));
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
            DeviceLog.Write(StateName,
                string.Format("焊接过程态切换 {0} -> {1}", oldStatus, newStatus));
        }

        #endregion
    }
}
