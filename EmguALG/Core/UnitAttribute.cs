using System;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>单位标注特性：[Unit("mm")]。 仅用于 PropertyGrid 单位显示，不参与序列化。</summary>
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
