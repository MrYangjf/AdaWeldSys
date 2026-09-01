using System;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.API;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 正运动输入 IO 实现
    /// </summary>
    public class ZMotionInputIO : IInputIO
    {
        private const string Tag = "ZMotionInputIO";

        private readonly IntPtr _handle;
        private readonly int _ioNumber;
        private string _ioName;

        public string IOName
        {
            get { return _ioName; }
            private set { _ioName = value; }
        }

        public ushort IONumber
        {
            get { return (ushort)_ioNumber; }
            set { /* IO号由构造函数确定 */ }
        }

        /// <summary>IO是否导通</summary>
        public bool IsOn
        {
            get
            {
                if (_handle == IntPtr.Zero) return false;
                uint value = 0;
                int ret = ZMotionNative.ZAux_Direct_GetIn(_handle, _ioNumber, ref value);
                if (!ZMotionNative.IsSuccess(ret)) return false;
                return value != 0;
            }
        }

        public bool IsEnabled { get; set; }

        public ZMotionInputIO(IntPtr handle, int ioNumber, string ioName)
        {
            _handle = handle;
            _ioNumber = ioNumber;
            _ioName = ioName ?? string.Format("输入IO{0}", ioNumber);
            IsEnabled = true;
        }

        public IOType GetIOType()
        {
            return IOType.Input;
        }

        public int GetIOStatus()
        {
            return IsOn ? 1 : 0;
        }
    }

    /// <summary>
    /// 正运动输出 IO 实现
    /// </summary>
    public class ZMotionOutputIO : IOutputIO
    {
        private const string Tag = "ZMotionOutputIO";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private readonly IntPtr _handle;
        private readonly int _ioNumber;
        private string _ioName;
        private bool _emergencyStopOff;

        public string IOName
        {
            get { return _ioName; }
            private set { _ioName = value; }
        }

        public ushort IONumber
        {
            get { return (ushort)_ioNumber; }
            set { /* IO号由构造函数确定 */ }
        }

        /// <summary>输出是否导通</summary>
        public bool IsOn
        {
            get
            {
                if (_handle == IntPtr.Zero) return false;
                uint value = 0;
                int ret = ZMotionNative.ZAux_Direct_GetOp(_handle, _ioNumber, ref value);
                if (!ZMotionNative.IsSuccess(ret)) return false;
                return value != 0;
            }
        }

        /// <summary>急停时是否自动关闭</summary>
        public bool EmergencyStopOff
        {
            get { return _emergencyStopOff; }
            set { _emergencyStopOff = value; }
        }

        public ZMotionOutputIO(IntPtr handle, int ioNumber, string ioName)
        {
            _handle = handle;
            _ioNumber = ioNumber;
            _ioName = ioName ?? string.Format("输出IO{0}", ioNumber);
            _emergencyStopOff = true;
        }

        public IOType GetIOType()
        {
            return IOType.Output;
        }

        public int GetIOStatus()
        {
            return IsOn ? 1 : 0;
        }

        /// <summary>设置输出为ON</summary>
        public void SetON()
        {
            if (_handle == IntPtr.Zero) return;
            int ret = ZMotionNative.ZAux_Direct_SetOp(_handle, _ioNumber, 1);
            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format("{0} 输出 ON", _ioName));
            }
            else
            {
                Log(string.Format(
                    "{0} 输出 ON 失败 原因是 {1}", _ioName,
                    ZMotionNative.GetErrorDescription(ret)));
            }
        }

        /// <summary>设置输出为OFF</summary>
        public void SetOFF()
        {
            if (_handle == IntPtr.Zero) return;
            int ret = ZMotionNative.ZAux_Direct_SetOp(_handle, _ioNumber, 0);
            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format("{0} 输出 OFF", _ioName));
            }
            else
            {
                Log(string.Format(
                    "{0} 输出 OFF 失败 原因是 {1}", _ioName,
                    ZMotionNative.GetErrorDescription(ret)));
            }
        }
    }
}
