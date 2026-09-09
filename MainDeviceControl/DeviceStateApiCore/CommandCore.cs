using System;

namespace AdaWeldSystem.MainDeviceControl.DeviceState
{
    /// <summary>机器人指令协议常量（ADR-026 裁定 C3）。</summary>
    /// <remarks>指令一律字符串常量、禁调用方自定义赋值；不监控机器人 pose；报文格式待协议细化，消费方以 // todo 占位。</remarks>
    public static class CommandCore
    {
        #region 协议方向标识（原档 R002/R003）

        /// <summary>接受指令方向标识（机器人 → 设备）。原档拼写 ReciveCommand，按正确英文落地。</summary>
        public const string ReceiveCommand = "RECEIVE";

        /// <summary>发送指令方向标识（设备 → 机器人）。</summary>
        public const string SendCommand = "SEND";

        #endregion

        #region 接收指令（机器人 → 设备）

        /// <summary>机器人上报：归零位。</summary>
        public const string RxHomePose = "RX_HOME_POSE";

        /// <summary>机器人上报：进入安全起始位。</summary>
        public const string RxStartSafePose = "RX_START_SAFE_POSE";

        /// <summary>机器人上报：焊前位。</summary>
        public const string RxPreWeldPose = "RX_PRE_WELD_POSE";

        /// <summary>机器人上报：焊接起始位。</summary>
        public const string RxWeldStartPose = "RX_WELD_START_POSE";

        /// <summary>机器人上报：预停位。</summary>
        public const string RxPreStopPose = "RX_PRE_STOP_POSE";

        /// <summary>机器人上报：焊接停止位。</summary>
        public const string RxWeldStopPose = "RX_WELD_STOP_POSE";

        /// <summary>机器人上报：回到安全停止位。</summary>
        public const string RxStopSafePose = "RX_STOP_SAFE_POSE";

        /// <summary>机器人请求：进入焊前。</summary>
        public const string RxPreWeldRequest = "RX_PRE_WELD_REQUEST";

        /// <summary>机器人请求：急停 / 中止。</summary>
        public const string RxAbort = "RX_ABORT";

        #endregion

        #region 下发指令（设备 → 机器人）

        /// <summary>下发：回零。</summary>
        public const string TxGoHome = "TX_GO_HOME";

        /// <summary>下发：去安全起始位。</summary>
        public const string TxGoStartSafe = "TX_GO_START_SAFE";

        /// <summary>下发：去焊前位。</summary>
        public const string TxGoPreWeld = "TX_GO_PRE_WELD";

        /// <summary>下发：去焊接起始位。</summary>
        public const string TxGoWeldStart = "TX_GO_WELD_START";

        /// <summary>下发：去预停位。</summary>
        public const string TxGoPreStop = "TX_GO_PRE_STOP";

        /// <summary>下发：去焊接停止位。</summary>
        public const string TxGoWeldStop = "TX_GO_WELD_STOP";

        /// <summary>下发：回安全停止位。</summary>
        public const string TxGoStopSafe = "TX_GO_STOP_SAFE";

        /// <summary>下发：设备就绪，反馈机器人继续。</summary>
        public const string TxOK = "TX_OK";

        /// <summary>下发：急停 / 中止。</summary>
        public const string TxAbort = "TX_ABORT";

        #endregion

        #region 公共函数

        /// <summary>把原始报文规整为协议常量（去空白 + 转大写）。</summary>
        /// <param name="raw">通讯层收到的原始指令字符串</param>
        /// <returns>规整后的指令；原始报文为 null 或空白时返回空串</returns>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Trim().ToUpperInvariant();
        }

        /// <summary>判断原始报文是否为已知指令。</summary>
        /// <param name="raw">通讯层收到的原始指令字符串</param>
        /// <returns>命中任一协议常量返回 true</returns>
        public static bool IsKnownCommand(string raw)
        {
            string cmd = Normalize(raw);
            if (cmd.Length == 0) return false;

            return cmd == RxHomePose
                || cmd == RxStartSafePose
                || cmd == RxPreWeldPose
                || cmd == RxWeldStartPose
                || cmd == RxPreStopPose
                || cmd == RxWeldStopPose
                || cmd == RxStopSafePose
                || cmd == RxPreWeldRequest
                || cmd == RxAbort
                || cmd == TxGoHome
                || cmd == TxGoStartSafe
                || cmd == TxGoPreWeld
                || cmd == TxGoWeldStart
                || cmd == TxGoPreStop
                || cmd == TxGoWeldStop
                || cmd == TxGoStopSafe
                || cmd == TxOK
                || cmd == TxAbort;
        }

        #endregion
    }
}
