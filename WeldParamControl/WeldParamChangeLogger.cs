using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.SQLiteDBControl.Helpers;
using Newtonsoft.Json;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>
    /// 参数变更审计：逐条记录改动，供现场回溯。
    /// </summary>
    /// <remarks>
    /// 仅在保存且确有差异时输出，无差异不输出。
    /// 比较基准须取落盘源（配置文件或数据库）。
    /// 界面改动作用于同一实例，内存快照取不到旧值。
    /// 比较与输出本类不落盘、不持有状态，纯函数式。
    /// </remarks>
    public static class WeldParamChangeLogger
    {
        /// <summary>日志标签（纯文本，无符号）。</summary>
        private const string Tag = "焊参配置";

        /// <summary>不参与审计的成员名（派生值或标识）。</summary>
        private static readonly string[] IgnoredMembers = { "CreatTime" };

        /// <summary>逐档比较两个字典（缺失档判新增、多余档判删除）。</summary>
        /// <param name="scene">场景名</param>
        /// <param name="label">参数中文名</param>
        /// <param name="before">旧字典（null 视为空）</param>
        /// <param name="after">新字典（null 视为空）</param>
        private static void DiffDict(string scene, string label,
                                     Dictionary<double, double> before, Dictionary<double, double> after)
        {
            if (before == null)
            {
                before = new Dictionary<double, double>();
            }
            if (after == null)
            {
                after = new Dictionary<double, double>();
            }

            foreach (KeyValuePair<double, double> pair in after)
            {
                if (!before.TryGetValue(pair.Key, out double oldValue))
                {
                    Emit(scene, label + " 新增档位", "-",
                        FormatWidth(pair.Key) + "=" + Num(pair.Value));
                    continue;
                }

                if (oldValue != pair.Value)
                {
                    Emit(scene, label + "[" + FormatWidth(pair.Key) + "]", Num(oldValue), Num(pair.Value));
                }
            }

            foreach (KeyValuePair<double, double> pair in before)
            {
                if (!after.ContainsKey(pair.Key))
                {
                    Emit(scene, label + " 删除档位", FormatWidth(pair.Key) + "=" + Num(pair.Value), "-");
                }
            }
        }

        /// <summary>反射收集标量成员（基元/字符串/枚举/十进制），复杂</summary>
        /// <remarks>对象默认 ToString 无意义故排除。</remarks>
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

        /// <summary>差异判定：字符串不同且数值语义也不同（避免 1 与 1.0 </summary>
        /// <remarks>的格式性误报）。</remarks>
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
        /// <param name="scene">场景名</param>
        /// <param name="name">参数名</param>
        /// <param name="before">旧值</param>
        /// <param name="after">新值</param>
        private static void Emit(string scene, string name, string before, string after)
        {
            GlobalCommData.ShowLog(Tag, string.Format("参数变更 {0} {1} {2} 转 {3}",
                scene, name, Display(before), Display(after)), MessageLevel.Info);
        }

        /// <summary>空值显示占位，避免日志出现空段。</summary>
        /// <param name="value">原始值</param>
        /// <returns>可读文本</returns>
        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "(空)" : value;
        }

        /// <summary>数值文本（不变文化，避免小数点被本地化）。</summary>
        /// <param name="value">数值</param>
        /// <returns>文本</returns>
        private static string Num(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>档位文本（最多三位小数，去掉尾随零）。</summary>
        /// <param name="value">档位值</param>
        /// <returns>文本</returns>
        private static string FormatWidth(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>解析档位 JSON（空表或解析失败返回空列表）。</summary>
        /// <param name="json">JSON 串</param>
        /// <returns>档位列表</returns>
        private static List<double> ParseList(string json)
        {
            if (!WeldAutoParamProfile.IsMeaningfulJson(json))
            {
                return new List<double>();
            }

            try
            {
                List<double> parsed = JsonConvert.DeserializeObject<List<double>>(json);
                return parsed ?? new List<double>();
            }
            catch
            {
                return new List<double>();
            }
        }

        /// <summary>解析档位字典 JSON（空表或解析失败返回空字典）。</summary>
        /// <param name="json">JSON 串</param>
        /// <returns>档位字典</returns>
        private static Dictionary<double, double> ParseDict(string json)
        {
            if (!WeldAutoParamProfile.IsMeaningfulJson(json))
            {
                return new Dictionary<double, double>();
            }

            try
            {
                Dictionary<double, double> parsed = JsonConvert.DeserializeObject<Dictionary<double, double>>(json);
                return parsed ?? new Dictionary<double, double>();
            }
            catch
            {
                return new Dictionary<double, double>();
            }
        }

        /// <summary>对象级差异审计：逐标量字段与属性比较，仅输出</summary>
        /// <remarks>有变动的项。</remarks>
        /// <param name="scene">场景名（面板名或方法语义）</param>
        /// <param name="before">保存前的落盘对象（可为 null，视为全新增）</param>
        /// <param name="after">保存后的当前对象</param>
        public static void LogObjectDiff(string scene, object before, object after)
        {
            if (after == null)
            {
                return;
            }

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
                    Emit(scene, pair.Key, oldValue, pair.Value);
                }
            }
        }

        /// <summary>单项差异审计：值相同则不输出。</summary>
        /// <param name="scene">场景名</param>
        /// <param name="name">参数名</param>
        /// <param name="before">旧值（字符串形式）</param>
        /// <param name="after">新值（字符串形式）</param>
        public static void LogField(string scene, string name, string before, string after)
        {
            if (!IsChanged(before, after))
            {
                return;
            }

            Emit(scene, name, before, after);
        }

        /// <summary>分段参数表差异审计：以宽度档位为粒度，逐档比</summary>
        /// <remarks>较送丝速度/激光功率/焊接速度/光斑宽度。</remarks>
        /// <param name="scene">场景名</param>
        /// <param name="before">保存前的数据库分段参数</param>
        /// <param name="after">保存后的当前分段参数</param>
        public static void LogProfileDiff(string scene, WeldAutoParamProfile before, WeldAutoParamProfile after)
        {
            if (after == null)
            {
                return;
            }

            WeldAutoParamProfile old = before ?? new WeldAutoParamProfile();

            DiffDict(scene, "送丝速度", ParseDict(old.FeedSpeedJson), ParseDict(after.FeedSpeedJson));
            DiffDict(scene, "激光功率", ParseDict(old.LaserPowerJson), ParseDict(after.LaserPowerJson));
            DiffDict(scene, "焊接速度", ParseDict(old.RobotSpeedJson), ParseDict(after.RobotSpeedJson));
            DiffDict(scene, "光斑宽度", ParseDict(old.SpotWidthJson), ParseDict(after.SpotWidthJson));
            DiffDict(scene, "摆幅宽度", ParseDict(old.SwingWidthJson), ParseDict(after.SwingWidthJson));
            DiffDict(scene, "摆幅频率", ParseDict(old.SwingFrequencyJson), ParseDict(after.SwingFrequencyJson));

            List<double> oldWidths = ParseList(old.SeamWidthJson);
            List<double> newWidths = ParseList(after.SeamWidthJson);
            if (oldWidths.Count != newWidths.Count)
            {
                Emit(scene, "焊缝宽度档位数",
                    oldWidths.Count.ToString(CultureInfo.InvariantCulture),
                    newWidths.Count.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
