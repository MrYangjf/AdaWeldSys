using System;
using System.Collections.Generic;

namespace AdaWeldSystem.MainDeviceControl.DeviceStateApiCore
{
    /// <summary>机器人指令协议常量。</summary>
    public static class CommandCore
    {
        #region 协议方向标识

        /// <summary>接受指令方向标识（机器人 → 设备）。</summary>
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

        /// <summary>下发：焊接中异常，通知机器人停止动作。</summary>
        public const string TxNG = "TX_NG";

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

        /// <summary>尝试把带坐标载荷的报文拆成「指令名 + 载荷」。</summary>

        /// <param name="normalized">已规整（Trim + 大写）的报文</param>
        /// <param name="command">输出：指令名（已知指令常量）</param>
        /// <param name="payload">输出：指令名后的载荷原文（无载荷为空串）</param>
        /// <returns>头部命中已知指令返回 true</returns>
        public static bool TrySplitPayload(string normalized, out string command, out string payload)
        {
            command = normalized;
            payload = string.Empty;
            if (string.IsNullOrEmpty(normalized)) return false;
            if (IsKnownCommand(normalized)) return true;

            int cut = -1;
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (c == ' ' || c == ',' || c == ';' || c == '\t') { cut = i; break; }
                // 数值连写也可：遇数字/小数点/负号即截断
                if (i > 0 && (char.IsDigit(c) || c == '.' || c == '-')) { cut = i; break; }
            }
            if (cut <= 0) return false;

            string head = normalized.Substring(0, cut).TrimEnd(' ', ',', ';', '\t');
            if (!IsKnownCommand(head)) return false;
            command = head;
            payload = normalized.Substring(cut).TrimStart(' ', ',', ';', '\t');
            return true;
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
                || cmd == TxNG
                || cmd == TxAbort;
        }

        #endregion

        #region 指令释意字典（供 MainPage Dash 机器人指令显示）

        /// <summary>指令 → 中文释意（供 MainPage Dash 和日志显示）。</summary>
        public static readonly Dictionary<string, string> CommandDescriptions = new Dictionary<string, string>
        {
            // Rx（机器人 → 设备）
            { RxHomePose, "机器人到归零位" },
            { RxStartSafePose, "机器人到安全起始位" },
            { RxPreWeldPose, "机器人到焊前位" },
            { RxWeldStartPose, "机器人到焊接起始位" },
            { RxPreStopPose, "机器人到预停位" },
            { RxWeldStopPose, "机器人到焊接停止位" },
            { RxStopSafePose, "机器人到安全停止位" },
            { RxPreWeldRequest, "机器人请求进入焊前" },
            { RxAbort, "机器人请求急停" },
            // Tx（设备 → 机器人）
            { TxGoHome, "下发回零指令" },
            { TxGoStartSafe, "下发去安全起始位" },
            { TxGoPreWeld, "下发去焊前位" },
            { TxGoWeldStart, "下发去焊接起始位" },
            { TxGoPreStop, "下发去预停位" },
            { TxGoWeldStop, "下发去焊接停止位" },
            { TxGoStopSafe, "下发回安全停止位" },
            { TxNG, "焊接异常 通知机器人停止" },
            { TxAbort, "下发急停指令" },
        };

        /// <summary>把指令常量翻译成中文释意（未命中返回原始指</summary>

        /// <returns>结果文本。</returns>
        public static string TranslateCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return "——";
            string key = Normalize(command);
            if (string.IsNullOrEmpty(key)) return "——";
            // 带载荷报文：拆出指令头再查字典
            if (TrySplitPayload(key, out string head, out _))
            {
                key = head;
            }
            return CommandDescriptions.TryGetValue(key, out var desc) ? desc : key;
        }

        #endregion

        #region 数值码映射（RSI 数值化协议：上行 RxCmd / 下行 TxCmd）

        /// <summary>上行指令码（RxCmd）→ 指令常量名。</summary>
        private static readonly Dictionary<int, string> RxCodeToCommand = new Dictionary<int, string>
        {
            { 1, RxHomePose },
            { 2, RxStartSafePose },
            { 3, RxPreWeldPose },
            { 4, RxWeldStartPose },
            { 5, RxPreStopPose },
            { 6, RxWeldStopPose },
            { 7, RxStopSafePose },
            { 8, RxPreWeldRequest },
            { 9, RxAbort }
        };

        /// <summary>下行指令码（TxCmd）→ 指令常量名。</summary>
        private static readonly Dictionary<int, string> TxCodeToCommand = new Dictionary<int, string>
        {
            { 1, TxGoHome },
            { 2, TxGoStartSafe },
            { 3, TxGoPreWeld },
            { 4, TxGoWeldStart },
            { 5, TxGoPreStop },
            { 6, TxGoWeldStop },
            { 7, TxGoStopSafe },
            { 8, TxNG },
            { 9, TxAbort }
        };

        /// <summary>指令常量名 → 下行指令码（TxCmd 数值码）。</summary>
        private static readonly Dictionary<string, int> CommandToTxCode = BuildTxCodeReverse();

        /// <summary>构造下行指令码反查表（由 TxCodeToCommand 反向生成，避免两处手</summary>

        /// <returns>指令常量名 → 数值码</returns>
        private static Dictionary<string, int> BuildTxCodeReverse()
        {
            Dictionary<string, int> map = new Dictionary<string, int>();
            foreach (KeyValuePair<int, string> kv in TxCodeToCommand)
            {
                map[kv.Value] = kv.Key;
            }
            return map;
        }

        /// <summary>上行指令码 → 指令常量名。</summary>
        /// <param name="rxCode">上行数值码（0=无指令）</param>
        /// <returns>命中返回指令常量名；未命中返回空串</returns>
        public static string RxCodeToCommandName(int rxCode)
        {
            return RxCodeToCommand.TryGetValue(rxCode, out string name) ? name : string.Empty;
        }

        /// <summary>下行指令码 → 指令常量名。</summary>
        /// <param name="txCode">下行数值码（0=无指令）</param>
        /// <returns>命中返回指令常量名；未命中返回空串</returns>
        public static string TxCodeToCommandName(int txCode)
        {
            return TxCodeToCommand.TryGetValue(txCode, out string name) ? name : string.Empty;
        }

        /// <summary>下行指令常量名 → 数值码。</summary>
        /// <param name="command">指令常量名（如 TxGoHome）</param>
        /// <returns>命中返回数值码；未命中返回 0</returns>
        public static int CommandToTxCodeValue(string command)
        {
            if (string.IsNullOrEmpty(command)) return 0;
            return CommandToTxCode.TryGetValue(Normalize(command), out int code) ? code : 0;
        }

        /// <summary>上行指令码 → 中文释意（调试面板用；未命中返回码</summary>

        /// <param name="rxCode">上行数值码</param>
        /// <returns>释意文本</returns>
        public static string DescribeRxCode(int rxCode)
        {
            string name = RxCodeToCommandName(rxCode);
            if (string.IsNullOrEmpty(name)) return rxCode == 0 ? "无指令" : string.Format("未知上行码 {0}", rxCode);
            return TranslateCommand(name);
        }

        /// <summary>下行指令码 → 中文释意（调试面板用；未命中返回码</summary>

        /// <param name="txCode">下行数值码</param>
        /// <returns>释意文本</returns>
        public static string DescribeTxCode(int txCode)
        {
            string name = TxCodeToCommandName(txCode);
            if (string.IsNullOrEmpty(name)) return txCode == 0 ? "无指令" : string.Format("未知下行码 {0}", txCode);
            return TranslateCommand(name);
        }

        #endregion
    }
}
