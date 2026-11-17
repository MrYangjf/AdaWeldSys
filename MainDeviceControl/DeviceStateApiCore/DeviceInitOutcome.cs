using System;
using System.Collections.Generic;

namespace AdaWeldSystem.MainDeviceControl.DeviceStateApiCore
{
    /// <summary>设备初始化结果三态。</summary>
    /// <remarks>与 <see cref="SubDeviceState"/> 正交，只表达初始化处置。
    /// 不是设备运行态；因父级未就绪而没尝试的连接记 <see cref="Skipped"/>，不计入失败。</remarks>
    public enum DeviceInitOutcome
    {
        /// <summary>成功（已连接或已就绪）。</summary>
        Success = 0,

        /// <summary>失败（真发起过连接且失败 / 等待超时）。</summary>
        Failed = 1,

        /// <summary>跳过（上级未就绪，未发起连接）。</summary>
        Skipped = 2
    }

    /// <summary>单个设备的初始化结果。</summary>
    public sealed class DeviceInitOutcomeItem
    {
        /// <summary>节点键（与初始化层级表中的 Key 一致）。</summary>
        public string Key { get; set; }

        /// <summary>设备中文名（日志与界面展示用）。</summary>
        public string DisplayName { get; set; }

        /// <summary>本次初始化结果。</summary>
        public DeviceInitOutcome Outcome { get; set; }

        /// <summary>跳过时被谁挡住（父级中文名）；非跳过为空串。</summary>
        public string BlockedBy { get; set; }

        /// <summary>原因说明（成功说明 / 失败原因 / 跳过说明）。</summary>
        public string Reason { get; set; }
    }

    /// <summary>初始化汇总事件参数（成功 / 失败 / 跳过分开计数）。</summary>
    public sealed class DeviceInitSummaryEventArgs : EventArgs
    {
        /// <summary>成功数。</summary>
        public int SuccessCount { get; set; }

        /// <summary>失败数（不含跳过）。</summary>
        public int FailedCount { get; set; }

        /// <summary>跳过数（上级未就绪，未发起连接）。</summary>
        public int SkippedCount { get; set; }

        /// <summary>逐设备结果（顺序与初始化层级表一致）。</summary>
        public IList<DeviceInitOutcomeItem> Items { get; set; }
    }
}
