using System;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MainDeviceControl.DeviceState
{
    /// <summary>
    /// 设备态枚举：所有设备（主设备 + 子设备）共有且仅有四态。
    ///   · Disconnect 未连接/已断开（对应流程态「未初始化」）
    ///   · Connect    已连接（初始化完成 = 完成 connect，对应流程态「待机」）
    ///   · Work       工作中（对应流程态「进程中的全部状态」：准备/启动/焊接/停止中……）
    ///   · Alarm      报警（对应流程态「报警」）
    ///
    /// 核心约定：设备流程态随业务变化（各设备各不相同，可丰富）；
    /// 设备态固定为这四态，由流程态按下列规则派生：
    ///   流程「未初始化」        → Disconnect
    ///   流程「初始化完成/待机」 → Connect
    ///   流程「进程中的状态」    → Work
    ///   流程「报警」            → Alarm
    /// </summary>
    public enum DeviceState
    {
        Disconnect = 0,
        Connect = 1,
        Work = 2,
        Alarm = 3
    }

    /// <summary>设备状态变更事件参数（单控制源）。</summary>
    public class DeviceStateChangedEventArgs : EventArgs
    {
        /// <summary>切换前设备态。</summary>
        public DeviceState OldState { get; set; }

        /// <summary>切换后设备态。</summary>
        public DeviceState NewState { get; set; }

        /// <summary>切换原因。</summary>
        public string Reason { get; set; }

        /// <summary>变更时间。</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>设备态基类（继承链最底层）：承载设备态 + 设备安全。</summary>
    /// <remarks>· State(DeviceState) 设备态，单控制源，变更经 OnStateSwitched 通知并触发 StateSwitched 事件。 · IsAlarm 设备安全标志；EmergencyStop() 急停（子类可重写以调用具体运动控制器急停）。 · CheckDeviceIOSafe() 设备 IO 安全互锁读取（子类重写，返回是否安全）。 设备安全需要的能力统一写进本类（ADR：设备安全归属 DeviceState）。</remarks>
    public abstract class DeviceStateBase
    {
        #region 私有变量

        private DeviceState _state = DeviceState.Disconnect;
        private bool _isAlarm;

        #endregion

        #region 公共变量

        /// <summary>当前设备态（单控制源）。</summary>
        public DeviceState State
        {
            get { return _state; }
            protected set
            {
                if (_state == value) return;
                DeviceState old = _state;
                _state = value;
                OnStateSwitched(old, value);
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

        /// <summary>设备状态切换事件（设备态）。</summary>
        public event EventHandler<DeviceStateChangedEventArgs> StateSwitched;

        /// <summary>急停/告警发生事件。</summary>
        public event EventHandler AlarmOccurred;

        /// <summary>设备名称（日志/事件标识），子类必须实现。</summary>
        public abstract string StateName { get; }

        #endregion

        #region 公共函数

        /// <summary>设备安全：急停。</summary>
        /// <remarks>默认置 IsAlarm=true 并记录日志；子类（主设备）应重写以调用具体运动控制器/IO 的紧急停止。</remarks>
        public virtual void EmergencyStop()
        {
            IsAlarm = true;
            GlobalCommData.ShowLog(StateName, "设备急停", MessageLevel.Error);
        }

        /// <summary>设备安全：读取 IO 安全互锁。</summary>
        /// <remarks>子类重写以检查具体设备的急停/光栅/气压等 IO 信号。</remarks>
        /// <returns>当前是否安全（默认实现恒安全）</returns>
        protected virtual bool CheckDeviceIOSafe()
        {
            return true;
        }

        /// <summary>设备态切换钩子。</summary>
        /// <remarks>更新 State 时调用，触发 StateSwitched 事件并记录日志；子类可重写以追加处理（勿忘记 base）。</remarks>
        /// <param name="oldState">切换前设备态</param>
        /// <param name="newState">切换后设备态</param>
        protected virtual void OnStateSwitched(DeviceState oldState, DeviceState newState)
        {
            var handler = StateSwitched;
            if (handler != null)
            {
                handler(this, new DeviceStateChangedEventArgs
                {
                    OldState = oldState,
                    NewState = newState,
                    Timestamp = DateTime.Now
                });
            }
            GlobalCommData.ShowLog(StateName,
                string.Format("设备态切换 {0} -> {1}", oldState, newState));
        }

        #endregion
    }
}
