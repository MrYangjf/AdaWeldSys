using System;
using System.Collections.Generic;
using System.Globalization;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.SQLiteDBControl.Helpers;
using Newtonsoft.Json;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>分段参数表变更审计：按宽度档逐项比较。</summary>
    public static class WeldParamProfileAudit
    {
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

        /// <summary>输出一条参数变更日志。</summary>
        /// <param name="scene">场景名</param>
        /// <param name="name">参数名</param>
        /// <param name="before">旧值</param>
        /// <param name="after">新值</param>
        private static void Emit(string scene, string name, string before, string after)
        {
            GlobalCommData.ShowLog("焊参配置", string.Format("参数变更 {0} {1} {2} 转 {3}",
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

        /// <summary>分段参数表差异审计：按宽度档逐项比较。</summary>
        /// <param name="scene">场景名</param>
        /// <param name="before">保存前的分段参数</param>
        /// <param name="after">保存后的分段参数</param>
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
