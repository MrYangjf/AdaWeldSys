using System;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>
    /// 单位标注特性：[Unit("mm")]。
    /// 仅用于 UI(PropertyGrid) 显示单位与文档提示，不参与 INI 序列化（单位信息不落盘）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class UnitAttribute : Attribute
    {
        public string Unit { get; private set; }

        public UnitAttribute(string unit)
        {
            Unit = unit;
        }
    }
}
