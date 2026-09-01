using System;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>
    /// 滑杆 / 输入范围限制特性：[Range(min, max)]（可选）。
    /// UI 可用于 Min/Max 约束与越界拦截；不影响算法逻辑。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class RangeAttribute : Attribute
    {
        public double Min { get; private set; }
        public double Max { get; private set; }

        public RangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }
    }
}
