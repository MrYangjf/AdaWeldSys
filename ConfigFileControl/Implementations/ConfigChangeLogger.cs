using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.ConfigFileControl.Implementations
{
    /// <summary>配置变更审计：逐条记录改动，供现场回溯。</summary>
    public static class ConfigChangeLogger
    {
        /// <summary>默认日志标签（纯文本，无符号）。</summary>
        private const string DefaultTag = "配置变更";

        /// <summary>不参与审计的成员名（派生值或标识）。</summary>
        private static readonly string[] IgnoredMembers = { "CreatTime" };

        /// <summary>反射收集标量成员（基元/字符串/枚举/十进制）。</summary>
        /// <param name="source">源对象（null 返回空表）</param>
        /// <returns>成员名到值字符串的映射</returns>
        private static Dictionary<string, string> CollectScalars(object source)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            if (source == null)
            {
                return map;
            }

            Type type = source.GetType();

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }
                if (!IsScalar(property.PropertyType) || IsIgnored(property.Name))
                {
                    continue;
                }

                try
                {
                    object value = property.GetValue(source, null);
                    map[property.Name] = value == null ? string.Empty
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // 个别成员取值异常不影响其余参数审计
                }
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (!IsScalar(field.FieldType) || IsIgnored(field.Name))
                {
                    continue;
                }

                try
                {
                    object value = field.GetValue(source);
                    map[field.Name] = value == null ? string.Empty
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // 同上
                }
            }

            return map;
        }

        /// <summary>判断类型是否可直接比较的标量。</summary>
        /// <param name="type">待判定类型</param>
        /// <returns>是标量返回 true</returns>
        private static bool IsScalar(Type type)
        {
            return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        /// <summary>判断成员是否在忽略名单内。</summary>
        /// <param name="name">成员名</param>
        /// <returns>需忽略返回 true</returns>
        private static bool IsIgnored(string name)
        {
            for (int i = 0; i < IgnoredMembers.Length; i++)
            {
                if (IgnoredMembers[i] == name)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>差异判定：字符串不同且数值语义也不同。</summary>
        /// <param name="before">旧值</param>
        /// <param name="after">新值</param>
        /// <returns>确有差异返回 true</returns>
        private static bool IsChanged(string before, string after)
        {
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                return false;
            }

            if (double.TryParse(before, NumberStyles.Any, CultureInfo.InvariantCulture, out double oldNumber)
                && double.TryParse(after, NumberStyles.Any, CultureInfo.InvariantCulture, out double newNumber))
            {
                return oldNumber != newNumber;
            }

            return true;
        }

        /// <summary>输出一条参数变更日志。</summary>
        /// <param name="tag">日志标签</param>
        /// <param name="scene">场景名</param>
        /// <param name="name">参数名</param>
        /// <param name="before">旧值</param>
        /// <param name="after">新值</param>
        private static void Emit(string tag, string scene, string name, string before, string after)
        {
            GlobalCommData.ShowLog(tag, string.Format("参数变更 {0} {1} {2} 转 {3}",
                scene, name, Display(before), Display(after)), MessageLevel.Info);
        }

        /// <summary>空值显示占位，避免日志出现空段。</summary>
        /// <param name="value">原始值</param>
        /// <returns>可读文本</returns>
        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "(空)" : value;
        }

        /// <summary>对象级差异审计：逐项比较，仅输出有变动的项。</summary>
        /// <param name="scene">场景名（面板名或方法语义）</param>
        /// <param name="before">保存前的落盘对象（可为 null，视为全新增）</param>
        /// <param name="after">保存后的当前对象</param>
        /// <param name="tag">日志标签（null 用默认标签）</param>
        public static void LogObjectDiff(string scene, object before, object after, string tag = null)
        {
            if (after == null)
            {
                return;
            }

            string logTag = string.IsNullOrEmpty(tag) ? DefaultTag : tag;
            Dictionary<string, string> oldValues = CollectScalars(before);
            Dictionary<string, string> newValues = CollectScalars(after);

            foreach (KeyValuePair<string, string> pair in newValues)
            {
                if (!oldValues.TryGetValue(pair.Key, out string oldValue))
                {
                    oldValue = string.Empty;
                }

                if (IsChanged(oldValue, pair.Value))
                {
                    Emit(logTag, scene, pair.Key, oldValue, pair.Value);
                }
            }
        }

        /// <summary>单项差异审计：值相同则不输出。</summary>
        /// <param name="scene">场景名</param>
        /// <param name="name">参数名</param>
        /// <param name="before">旧值（字符串形式）</param>
        /// <param name="after">新值（字符串形式）</param>
        /// <param name="tag">日志标签（null 用默认标签）</param>
        public static void LogField(string scene, string name, string before, string after, string tag = null)
        {
            if (!IsChanged(before, after))
            {
                return;
            }

            Emit(string.IsNullOrEmpty(tag) ? DefaultTag : tag, scene, name, before, after);
        }
    }
}
