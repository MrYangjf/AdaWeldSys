using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.SqlLiteDatabase;

namespace AdaWeldSystem.ProductFileManager
{
    using DeviceState = AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState;

    /// <summary>故障分类</summary>
    public enum FaultCategory
    {
        /// <summary>通讯故障</summary>
        Communication,

        /// <summary>设备故障</summary>
        Device,

        /// <summary>工艺故障</summary>
        Process,

        /// <summary>安全故障</summary>
        Safety,

        /// <summary>系统故障</summary>
        System
    }

    /// <summary>故障记录数据载体</summary>
    /// <remarks>覆盖「检测 → 停机 → 记录 → 确认 → 排除 → 复位 → 复位验证」闭环；设备态采用统一四态 <see cref="AdaWeldSystem.MainDeviceControl.DeviceState.DeviceState"/>。</remarks>
    public class FaultRecord
    {
        /// <summary>故障时间</summary>
        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>设备名称</summary>
        public string Device { get; set; } = "未知设备";

        /// <summary>故障时的设备状态（统一四态）</summary>
        public DeviceState State { get; set; } = DeviceState.Disconnect;

        /// <summary>故障分类</summary>
        public FaultCategory Category { get; set; } = FaultCategory.Device;

        /// <summary>错误码</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>参数快照</summary>
        public string ParamSnapshot { get; set; } = string.Empty;

        /// <summary>是否已自动恢复</summary>
        public bool AutoRecovered { get; set; }

        /// <summary>恢复动作说明</summary>
        public string RecoveryAction { get; set; } = string.Empty;
    }

    /// <summary>故障恢复管理器（单例）</summary>
    /// <remarks>
    /// 故障恢复数据的归口使用方，职责：
    /// 1) 记录故障并归档（SQLite 实现由 <see cref="FaultRecordManager"/> 承载，表 FaultRecord）；
    /// 2) 按设备注册恢复策略（Func&lt;bool&gt;），故障时自动重试；
    /// 3) 重试次数/间隔：通讯 3 次/5s、相机 2 次、线激光 3 次、安全类 0 次。
    /// 安全类故障不自动恢复（必须人工），由调用方在策略层控制。
    /// 对外配置入口：<see cref="RegisterRecoveryStrategy"/> / <see cref="RecordFault(FaultRecord)"/>。
    /// </remarks>
    public class FaultRecoveryManager
    {
        #region 私有变量

        private static readonly Lazy<FaultRecoveryManager> _lazy =
            new Lazy<FaultRecoveryManager>(() => new FaultRecoveryManager());

        private readonly Dictionary<string, Func<bool>> _strategies = new Dictionary<string, Func<bool>>();

        #endregion

        #region 公共变量

        /// <summary>单例入口</summary>
        public static FaultRecoveryManager Instance => _lazy.Value;

        #endregion

        #region 构造函数

        private FaultRecoveryManager()
        {
        }

        #endregion

        #region 私有函数

        /// <summary>执行自动恢复重试</summary>
        /// <param name="key">设备键名</param>
        /// <param name="strategy">恢复策略</param>
        /// <returns>恢复成功返回 true</returns>
        private bool TryRecover(string key, Func<bool> strategy)
        {
            int retry = GetRetryCount(key);
            int intervalMs = 5000;
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    if (strategy())
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog("故障恢复", string.Format("{0} 第{1}次重试异常 原因 {2}", key, i + 1, ex.Message));
                }
                if (i < retry - 1)
                {
                    Thread.Sleep(intervalMs);
                }
            }
            return false;
        }

        /// <summary>
        /// 按设备键返回重试次数。
        /// </summary>
        /// <param name="key">设备键名</param>
        /// <returns>重试次数</returns>
        private int GetRetryCount(string key)
        {
            if (key == null) return 3;
            if (key.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (key.IndexOf("LineLaser", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (key.IndexOf("Monitor", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (key.IndexOf("Comm", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            return 3;
        }

        /// <summary>
        /// 按设备名称反查已注册的设备键名。
        /// </summary>
        /// <param name="device">设备名称</param>
        /// <returns>命中的设备键名，未命中返回 null</returns>
        private string ResolveKey(string device)
        {
            if (string.IsNullOrEmpty(device)) return null;
            lock (_strategies)
            {
                foreach (var k in _strategies.Keys)
                {
                    if (device.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return k;
                }
            }
            return null;
        }

        /// <summary>归档故障记录到数据库</summary>
        /// <remarks>仅做领域对象到写入值数组的适配与字段截断，SQLite 实现由 <see cref="FaultRecordManager"/> 承载。</remarks>
        /// <param name="record">故障记录</param>
        private void Persist(FaultRecord record)
        {
            try
            {
                string[] values = new string[]
                {
                    record.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    Truncate(record.Device, 32),
                    record.State.ToString(),
                    record.Category.ToString(),
                    Truncate(record.ErrorCode, 32),
                    Truncate(record.ParamSnapshot, 200),
                    record.AutoRecovered ? "1" : "0",
                    Truncate(record.RecoveryAction, 64)
                };
                if (!FaultRecordManager.Instance.InsertFaultRecord(values))
                {
                    GlobalCommData.ShowLog("故障恢复", "FaultRecord 持久化失败 数据库返回失败", MessageLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("故障恢复", "FaultRecord 持久化失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 按最大长度截断字符串。
        /// </summary>
        /// <param name="s">源字符串</param>
        /// <param name="max">最大长度</param>
        /// <returns>截断后的字符串</returns>
        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        #endregion

        #region 公共函数

        /// <summary>注册设备自动恢复策略</summary>
        /// <remarks>重复注册覆盖；动作用英文 key 注册，与记录时的 key 同源。</remarks>
        /// <param name="deviceKey">设备键名（英文，与记录时传入的 key 同源）</param>
        /// <param name="action">恢复动作，返回 true 表示已恢复</param>
        public void RegisterRecoveryStrategy(string deviceKey, Func<bool> action)
        {
            if (string.IsNullOrEmpty(deviceKey) || action == null) return;
            lock (_strategies)
            {
                _strategies[deviceKey] = action;
            }
        }

        /// <summary>
        /// 记录故障并触发自动恢复（若有注册策略）。
        /// </summary>
        /// <param name="deviceKey">设备键名（与 RegisterRecoveryStrategy 的 key 对应，用于匹配恢复策略）</param>
        /// <param name="record">故障记录</param>
        public void RecordFault(string deviceKey, FaultRecord record)
        {
            if (record == null) return;

            Persist(record);

            GlobalCommData.ShowLog("故障恢复", string.Format(
                "故障记录 时间 {0} 设备 {1} 类别 {2} 错误码 {3} 参数快照 {4}",
                record.Time, record.Device, record.Category, record.ErrorCode, record.ParamSnapshot),
                MessageLevel.Error);

            // 优先使用传入的 deviceKey 匹配策略，回退到按 Device 名称解析
            string key = null;
            Func<bool> strategy = null;
            lock (_strategies)
            {
                if (!string.IsNullOrEmpty(deviceKey) && _strategies.ContainsKey(deviceKey))
                {
                    key = deviceKey;
                    strategy = _strategies[key];
                }
                else
                {
                    key = ResolveKey(record.Device);
                    if (key != null && _strategies.ContainsKey(key))
                    {
                        strategy = _strategies[key];
                    }
                }
            }

            if (strategy != null)
            {
                // 异步执行恢复，避免阻塞状态转换调用链
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    bool recovered = TryRecover(key, strategy);
                    record.AutoRecovered = recovered;
                    record.RecoveryAction = recovered ? "自动重试恢复成功" : "自动重试失败，需人工处理";
                    GlobalCommData.ShowLog("故障恢复", string.Format(
                        "{0} 自动恢复{1}", record.Device, recovered ? "成功" : "失败"),
                        recovered ? MessageLevel.Info : MessageLevel.Error);
                });
            }
            else
            {
                record.RecoveryAction = "未注册自动恢复策略，需人工处理";
            }
        }

        /// <summary>记录故障（兼容重载）</summary>
        /// <remarks>不传 deviceKey 时按 Device 名称反查策略，推荐改用带 deviceKey 的重载。</remarks>
        /// <param name="record">故障记录</param>
        public void RecordFault(FaultRecord record)
        {
            RecordFault(null, record);
        }

        #endregion
    }
}
