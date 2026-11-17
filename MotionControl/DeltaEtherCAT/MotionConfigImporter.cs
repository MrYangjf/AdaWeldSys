using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>运控配置 CSV 导入器</summary>
    /// <remarks>电气填写的三张 CSV 校验后全量替换进 DeltaMotionConfig。
    /// 落盘后下次初始化生效。校验失败不写入，全部错误经 msg 回传。</remarks>
    public static class MotionConfigImporter
    {
        #region 私有变量

        private const string Tag = "配置导入";

        private const string AxisFileName = "轴配置表.csv";
        private const string IOFileName = "IO配置表.csv";
        private const string CylinderFileName = "气缸配置表.csv";

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>读取 CSV 全部行，自动剥离 UTF-8 BOM</summary>
        /// <param name="path">CSV 文件路径</param>
        /// <param name="rows">成功时回传的行列表（每行为字段数组）</param>
        /// <param name="error">失败时回传的错误描述</param>
        /// <returns>读取成功返回 true</returns>
        private static bool ReadCsvRows(string path, out List<string[]> rows, out string error)
        {
            rows = new List<string[]>();
            error = string.Empty;

            if (!File.Exists(path))
            {
                error = "文件不存在 " + path;
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                error = "文件读取失败 " + Path.GetFileName(path) + " " + ex.Message;
                return false;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line) || line.Trim().Length == 0) continue;
                rows.Add(SplitCsvLine(line));
            }
            return true;
        }

        /// <summary>按 CSV 规则拆分单行，支持双引号包裹（内部双引号写</summary>
        /// <remarks>成两个）</remarks>
        /// <param name="line">原始行文本</param>
        /// <returns>字段数组</returns>
        private static string[] SplitCsvLine(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString().Trim());
            return fields.ToArray();
        }

        /// <summary>构造列名到下标的映射</summary>
        /// <param name="header">表头字段数组</param>
        /// <returns>列名字典，未出现的列取不到</returns>
        private static Dictionary<string, int> BuildColumnMap(string[] header)
        {
            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
            {
                map[header[i].Trim().ToUpper()] = i;
            }
            return map;
        }

        /// <summary>取单元格文本，越界或缺失返回空串</summary>
        /// <param name="row">行字段数组</param>
        /// <param name="map">列映射</param>
        /// <param name="column">列名</param>
        /// <returns>单元格文本</returns>
        private static string Cell(string[] row, Dictionary<string, int> map, string column)
        {
            if (!map.TryGetValue(column, out int index)) return string.Empty;
            return index < row.Length ? row[index] : string.Empty;
        }

        /// <summary>解析布尔启用标志，仅 TRUE 视为真</summary>
        /// <param name="txt">单元格文本</param>
        /// <returns>TRUE 返回 true</returns>
        private static bool ParseEnabled(string txt)
        {
            return txt.ToUpper() == "TRUE";
        }

        /// <summary>解析整数，空串取默认值，非法文本记入错误列表</summary>
        /// <param name="txt">单元格文本</param>
        /// <param name="def">空串默认值</param>
        /// <param name="where">出错时定位描述（表名 行号 列名）</param>
        /// <param name="errors">错误列表</param>
        /// <returns>解析结果</returns>
        private static int ToInt(string txt, int def, string where, List<string> errors)
        {
            if (txt.Length == 0) return def;
            if (int.TryParse(txt, out int val)) return val;
            errors.Add(where + " 数值非法 " + txt);
            return def;
        }

        /// <summary>解析带范围约束的整数</summary>
        /// <remarks>越界记入错误列表并取默认值</remarks>
        /// <param name="txt">单元格文本</param>
        /// <param name="def">空串/越界默认值</param>
        /// <param name="where">出错时定位描述</param>
        /// <param name="min">允许最小值</param>
        /// <param name="max">允许最大值</param>
        /// <param name="errors">错误列表</param>
        /// <returns>解析结果</returns>
        private static int ToIntRange(string txt, int def, string where, int min, int max, List<string> errors)
        {
            int val = ToInt(txt, def, where, errors);
            if (val < min || val > max)
            {
                errors.Add(where + " 必须为 " + min + "~" + max + "，当前 " + val);
                return def;
            }
            return val;
        }

        /// <summary>解析双精度，空串取默认值，非法文本记入错误列</summary>
        /// <remarks>表</remarks>
        /// <param name="txt">单元格文本</param>
        /// <param name="def">空串默认值</param>
        /// <param name="where">出错时定位描述</param>
        /// <param name="errors">错误列表</param>
        /// <returns>解析结果</returns>
        private static double ToDouble(string txt, double def, string where, List<string> errors)
        {
            if (txt.Length == 0) return def;
            if (double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double val)) return val;
            errors.Add(where + " 数值非法 " + txt);
            return def;
        }

        /// <summary>断言名称唯一，重复时记入错误列表</summary>
        /// <param name="seen">已出现名称集合</param>
        /// <param name="name">当前名称</param>
        /// <param name="where">出错时定位描述</param>
        /// <param name="errors">错误列表</param>
        private static void AssertUnique(HashSet<string> seen, string name, string where, List<string> errors)
        {
            if (!seen.Add(name))
            {
                errors.Add(where + " 名称重复 " + name);
            }
        }

        /// <summary>导入轴配置表</summary>
        /// <param name="dir">模板目录</param>
        /// <param name="errors">错误列表</param>
        /// <returns>解析出的轴配置列表</returns>
        private static List<DeltaAxisConfig> ImportAxes(string dir, List<string> errors)
        {
            List<DeltaAxisConfig> axes = new List<DeltaAxisConfig>();
            if (!ReadCsvRows(Path.Combine(dir, AxisFileName), out List<string[]> rows, out string error))
            {
                errors.Add(error);
                return axes;
            }
            if (rows.Count == 0)
            {
                errors.Add(AxisFileName + " 无数据行");
                return axes;
            }

            Dictionary<string, int> map = BuildColumnMap(rows[0]);
            HashSet<string> names = new HashSet<string>();

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string where = AxisFileName + " 第" + (r + 1) + "行";
                string name = Cell(row, map, "AXIS_NAME");
                if (name.Length == 0)
                {
                    errors.Add(where + " AXIS_NAME 为空");
                    continue;
                }
                string whereName = where + " 轴 " + name;
                AssertUnique(names, name, whereName, errors);

                DeltaAxisConfig cfg = new DeltaAxisConfig
                {
                    AxisName = name,
                    NodeId = (ushort)ToInt(Cell(row, map, "NODE_ID"), 0, whereName + " NODE_ID", errors),
                    SlotNo = (ushort)ToInt(Cell(row, map, "SLOT_NO"), 0, whereName + " SLOT_NO", errors),
                    Enabled = ParseEnabled(Cell(row, map, "ENABLED")),
                    Equiv = ToDouble(Cell(row, map, "EQUIV"), 0.001, whereName + " EQUIV", errors),
                    StrVel = ToDouble(Cell(row, map, "STR_VEL"), 0, whereName + " STR_VEL", errors),
                    ConstVel = ToDouble(Cell(row, map, "CONST_VEL"), 0, whereName + " CONST_VEL", errors),
                    AccTime = ToDouble(Cell(row, map, "ACC_TIME"), 0, whereName + " ACC_TIME", errors),
                    DecTime = ToDouble(Cell(row, map, "DEC_TIME"), 0, whereName + " DEC_TIME", errors),
                    STime = ToDouble(Cell(row, map, "S_TIME"), 0, whereName + " S_TIME", errors),
                    EndVel = ToDouble(Cell(row, map, "END_VEL"), 0, whereName + " END_VEL", errors),
                    HomeMode = (short)ToIntRange(Cell(row, map, "HOME_MODE"), 0, whereName + " HOME_MODE", -4, 35, errors),
                    HomeOffset = ToDouble(Cell(row, map, "HOME_OFFSET"), 0, whereName + " HOME_OFFSET", errors),
                    HomeFirstVel = ToDouble(Cell(row, map, "HOME_FIRST_VEL"), 0, whereName + " HOME_FIRST_VEL", errors),
                    HomeSecondVel = ToDouble(Cell(row, map, "HOME_SECOND_VEL"), 0, whereName + " HOME_SECOND_VEL", errors),
                    HomeAcc = ToDouble(Cell(row, map, "HOME_ACC"), 0, whereName + " HOME_ACC", errors),
                    PositiveLimit = ToDouble(Cell(row, map, "POSITIVE_LIMIT"), 0, whereName + " POSITIVE_LIMIT", errors),
                    NegativeLimit = ToDouble(Cell(row, map, "NEGATIVE_LIMIT"), 0, whereName + " NEGATIVE_LIMIT", errors),
                    SoftLimitMode = (ushort)ToInt(Cell(row, map, "SOFT_LIMIT_MODE"), 0, whereName + " SOFT_LIMIT_MODE", errors),
                    TorqueModeEnabled = ParseEnabled(Cell(row, map, "TORQUE_MODE")),
                    TorqueTarget = (short)ToInt(Cell(row, map, "TORQUE_TARGET"), 0, whereName + " TORQUE_TARGET", errors),
                    TorqueSlope = (uint)ToInt(Cell(row, map, "TORQUE_SLOPE"), 0, whereName + " TORQUE_SLOPE", errors)
                };
                if (cfg.ConstVel > 0 && cfg.StrVel > cfg.ConstVel)
                {
                    errors.Add(whereName + " STR_VEL 大于 CONST_VEL");
                }
                axes.Add(cfg);
            }
            return axes;
        }

        /// <summary>导入 IO 配置表</summary>
        /// <param name="dir">模板目录</param>
        /// <param name="inputs">回传输入配置列表</param>
        /// <param name="outputs">回传输出配置列表</param>
        /// <param name="inputNames">回传输入名集合（供气缸表校验引用）</param>
        /// <param name="outputNames">回传输出名集合（供气缸表校验引用）</param>
        /// <param name="errors">错误列表</param>
        private static void ImportIOs(string dir, List<DeltaIOConfig> inputs, List<DeltaIOConfig> outputs,
            HashSet<string> inputNames, HashSet<string> outputNames, List<string> errors)
        {
            if (!ReadCsvRows(Path.Combine(dir, IOFileName), out List<string[]> rows, out string error))
            {
                errors.Add(error);
                return;
            }
            if (rows.Count == 0)
            {
                errors.Add(IOFileName + " 无数据行");
                return;
            }

            Dictionary<string, int> map = BuildColumnMap(rows[0]);
            HashSet<string> names = new HashSet<string>();

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string where = IOFileName + " 第" + (r + 1) + "行";
                string type = Cell(row, map, "IO_TYPE").ToUpper();
                string name = Cell(row, map, "IO_NAME");
                if (name.Length == 0)
                {
                    errors.Add(where + " IO_NAME 为空");
                    continue;
                }
                string whereName = where + " IO " + name;
                AssertUnique(names, name, whereName, errors);

                if (type != "INPUT" && type != "OUTPUT")
                {
                    errors.Add(whereName + " IO_TYPE 必须为 INPUT 或 OUTPUT 当前 " + type);
                    continue;
                }

                DeltaIOConfig cfg = new DeltaIOConfig
                {
                    IOName = name,
                    NodeId = (ushort)ToInt(Cell(row, map, "NODE_ID"), 0, whereName + " NODE_ID", errors),
                    SlotNo = (ushort)ToInt(Cell(row, map, "SLOT_NO"), 0, whereName + " SLOT_NO", errors),
                    BitNo = (ushort)ToInt(Cell(row, map, "BIT_NO"), 0, whereName + " BIT_NO", errors),
                    Enabled = ParseEnabled(Cell(row, map, "ENABLED")),
                    Remark = Cell(row, map, "REMARK")
                };

                if (type == "INPUT")
                {
                    inputs.Add(cfg);
                    inputNames.Add(name);
                }
                else
                {
                    outputs.Add(cfg);
                    outputNames.Add(name);
                }
            }
        }

        /// <summary>导入气缸配置表</summary>
        /// <param name="dir">模板目录</param>
        /// <param name="inputNames">输入名集合（校验传感器引用）</param>
        /// <param name="outputNames">输出名集合（校验电磁阀引用）</param>
        /// <param name="errors">错误列表</param>
        /// <returns>解析出的气缸配置列表</returns>
        private static List<DeltaCylinderConfig> ImportCylinders(string dir, HashSet<string> inputNames,
            HashSet<string> outputNames, List<string> errors)
        {
            List<DeltaCylinderConfig> cylinders = new List<DeltaCylinderConfig>();
            if (!ReadCsvRows(Path.Combine(dir, CylinderFileName), out List<string[]> rows, out string error))
            {
                errors.Add(error);
                return cylinders;
            }
            if (rows.Count == 0)
            {
                return cylinders;
            }

            Dictionary<string, int> map = BuildColumnMap(rows[0]);
            HashSet<string> names = new HashSet<string>();

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string where = CylinderFileName + " 第" + (r + 1) + "行";
                string name = Cell(row, map, "CYLINDER_NAME");
                if (name.Length == 0)
                {
                    errors.Add(where + " CYLINDER_NAME 为空");
                    continue;
                }
                string whereName = where + " 气缸 " + name;
                AssertUnique(names, name, whereName, errors);

                DeltaCylinderConfig cyl = new DeltaCylinderConfig
                {
                    CylinderName = name,
                    StretchSensorName = Cell(row, map, "STRETCH_SENSOR"),
                    RetractSensorName = Cell(row, map, "RETRACT_SENSOR"),
                    CenterSensorName = Cell(row, map, "CENTER_SENSOR"),
                    StretchOutputName = Cell(row, map, "STRETCH_OUTPUT"),
                    RetractOutputName = Cell(row, map, "RETRACT_OUTPUT"),
                    StretchActionName = Cell(row, map, "STRETCH_ACTION_NAME"),
                    RetractActionName = Cell(row, map, "RETRACT_ACTION_NAME")
                };
                CheckIORef(cyl.StretchSensorName, inputNames, "STRETCH_SENSOR", whereName, errors);
                CheckIORef(cyl.RetractSensorName, inputNames, "RETRACT_SENSOR", whereName, errors);
                CheckIORef(cyl.CenterSensorName, inputNames, "CENTER_SENSOR", whereName, errors);
                CheckIORef(cyl.StretchOutputName, outputNames, "STRETCH_OUTPUT", whereName, errors);
                CheckIORef(cyl.RetractOutputName, outputNames, "RETRACT_OUTPUT", whereName, errors);
                cylinders.Add(cyl);
            }
            return cylinders;
        }

        /// <summary>校验气缸引用的 IO 名存在，非空且未命中时记入错</summary>
        /// <remarks>误列表</remarks>
        /// <param name="ioName">引用的 IO 名</param>
        /// <param name="names">名称集合</param>
        /// <param name="column">列名（出错定位用）</param>
        /// <param name="where">出错时定位描述</param>
        /// <param name="errors">错误列表</param>
        private static void CheckIORef(string ioName, HashSet<string> names, string column, string where,
            List<string> errors)
        {
            if (ioName.Length > 0 && !names.Contains(ioName))
            {
                errors.Add(where + " " + column + " 引用的 IO 不存在 " + ioName);
            }
        }

        /// <summary>单元格文本转 CSV 字段，含逗号或引号时用双引号包</summary>
        /// <remarks>裹并转义</remarks>
        /// <param name="text">原始文本</param>
        /// <returns>可写入 CSV 的字段</returns>
        private static string CsvField(string text)
        {
            string val = text == null ? string.Empty : text;
            if (val.IndexOf(',') >= 0 || val.IndexOf('"') >= 0 || val.IndexOf('\r') >= 0 || val.IndexOf('\n') >= 0)
            {
                return "\"" + val.Replace("\"", "\"\"") + "\"";
            }
            return val;
        }

        /// <summary>布尔转 CSV 文本，与导入侧 ParseEnabled 对称</summary>
        /// <param name="value">布尔值</param>
        /// <returns>TRUE 或 FALSE</returns>
        private static string CsvBool(bool value)
        {
            return value ? "TRUE" : "FALSE";
        }

        /// <summary>双精度转 CSV 文本，固定不变文化，避免小数点随系统</summary>
        /// <remarks>区域变化</remarks>
        /// <param name="value">数值</param>
        /// <returns>数值文本</returns>
        private static string CsvDouble(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>按 UTF-8 带 BOM 写出 CSV，Excel 双击直开中文不乱码</summary>
        /// <param name="path">目标文件路径</param>
        /// <param name="rows">行数据，第一行为表头</param>
        private static void WriteCsv(string path, List<string[]> rows)
        {
            List<string> lines = new List<string>();
            foreach (string[] row in rows)
            {
                lines.Add(string.Join(",", row));
            }
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(true));
        }

        /// <summary>按轴配置表表头写出一行</summary>
        /// <param name="rows">目标行集合</param>
        /// <param name="axes">轴配置集合</param>
        private static void AppendAxisRows(List<string[]> rows, IList<DeltaAxisConfig> axes)
        {
            foreach (DeltaAxisConfig cfg in axes)
            {
                rows.Add(new string[]
                {
                    CsvField(cfg.AxisName),
                    cfg.NodeId.ToString(CultureInfo.InvariantCulture),
                    cfg.SlotNo.ToString(CultureInfo.InvariantCulture),
                    CsvBool(cfg.Enabled),
                    CsvDouble(cfg.Equiv),
                    CsvDouble(cfg.StrVel),
                    CsvDouble(cfg.ConstVel),
                    CsvDouble(cfg.AccTime),
                    CsvDouble(cfg.DecTime),
                    CsvDouble(cfg.STime),
                    CsvDouble(cfg.EndVel),
                    cfg.HomeMode.ToString(CultureInfo.InvariantCulture),
                    CsvDouble(cfg.HomeOffset),
                    CsvDouble(cfg.HomeFirstVel),
                    CsvDouble(cfg.HomeSecondVel),
                    CsvDouble(cfg.HomeAcc),
                    CsvDouble(cfg.PositiveLimit),
                    CsvDouble(cfg.NegativeLimit),
                    cfg.SoftLimitMode.ToString(CultureInfo.InvariantCulture),
                    CsvBool(cfg.TorqueModeEnabled),
                    cfg.TorqueTarget.ToString(CultureInfo.InvariantCulture),
                    cfg.TorqueSlope.ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        /// <summary>按 IO 配置表表头追加一行，IO_TYPE 由调用方给定</summary>
        /// <param name="rows">目标行集合</param>
        /// <param name="list">IO 配置集合</param>
        /// <param name="ioType">INPUT 或 OUTPUT</param>
        private static void AppendIORows(List<string[]> rows, IList<DeltaIOConfig> list, string ioType)
        {
            foreach (DeltaIOConfig cfg in list)
            {
                rows.Add(new string[]
                {
                    ioType,
                    CsvField(cfg.IOName),
                    cfg.NodeId.ToString(CultureInfo.InvariantCulture),
                    cfg.SlotNo.ToString(CultureInfo.InvariantCulture),
                    cfg.BitNo.ToString(CultureInfo.InvariantCulture),
                    CsvBool(cfg.Enabled),
                    CsvField(cfg.Remark)
                });
            }
        }

        /// <summary>按气缸配置表表头追加一行</summary>
        /// <param name="rows">目标行集合</param>
        /// <param name="cylinders">气缸配置集合</param>
        private static void AppendCylinderRows(List<string[]> rows, IList<DeltaCylinderConfig> cylinders)
        {
            foreach (DeltaCylinderConfig cfg in cylinders)
            {
                rows.Add(new string[]
                {
                    CsvField(cfg.CylinderName),
                    CsvField(cfg.StretchSensorName),
                    CsvField(cfg.RetractSensorName),
                    CsvField(cfg.CenterSensorName),
                    CsvField(cfg.StretchOutputName),
                    CsvField(cfg.RetractOutputName),
                    CsvField(cfg.StretchActionName),
                    CsvField(cfg.RetractActionName)
                });
            }
        }

        /// <summary>写出三张 CSV 配置表，表头与导入侧逐字一致，可原样</summary>
        /// <remarks>回填</remarks>
        /// <param name="dir">目标目录</param>
        /// <param name="config">运控配置实例</param>
        private static void WriteAllCsv(string dir, DeltaMotionConfig config)
        {
            List<string[]> axes = new List<string[]>();
            axes.Add(new string[]
            {
                "AXIS_NAME", "NODE_ID", "SLOT_NO", "ENABLED", "EQUIV", "STR_VEL", "CONST_VEL",
                "ACC_TIME", "DEC_TIME", "S_TIME", "END_VEL", "HOME_MODE", "HOME_OFFSET",
                "HOME_FIRST_VEL", "HOME_SECOND_VEL", "HOME_ACC", "POSITIVE_LIMIT",
                "NEGATIVE_LIMIT", "SOFT_LIMIT_MODE",
                "TORQUE_MODE", "TORQUE_TARGET", "TORQUE_SLOPE"
            });
            AppendAxisRows(axes, config.Axes);
            WriteCsv(Path.Combine(dir, AxisFileName), axes);

            List<string[]> ios = new List<string[]>();
            ios.Add(new string[] { "IO_TYPE", "IO_NAME", "NODE_ID", "SLOT_NO", "BIT_NO", "ENABLED", "REMARK" });
            AppendIORows(ios, config.Inputs, "INPUT");
            AppendIORows(ios, config.Outputs, "OUTPUT");
            WriteCsv(Path.Combine(dir, IOFileName), ios);

            List<string[]> cylinders = new List<string[]>();
            cylinders.Add(new string[]
            {
                "CYLINDER_NAME", "STRETCH_SENSOR", "RETRACT_SENSOR", "CENTER_SENSOR",
                "STRETCH_OUTPUT", "RETRACT_OUTPUT", "STRETCH_ACTION_NAME", "RETRACT_ACTION_NAME"
            });
            AppendCylinderRows(cylinders, config.Cylinders);
            WriteCsv(Path.Combine(dir, CylinderFileName), cylinders);
        }

        #endregion

        #region 公共函数

        /// <summary>从指定目录导入三张 CSV 配置表并落盘</summary>
        /// <param name="dir">CSV 所在目录</param>
        /// <param name="msg">成功回传导入摘要，失败回传全部校验错误</param>
        /// <returns>导入成功返回 true</returns>
        public static bool Import(string dir, ref string msg)
        {
            return Import(dir, true, true, true, ref msg);
        }

        /// <summary>分节导入：只替换 flag 为 true 的节，其余节保留原配置</summary>
        /// <remarks>缺文件时按分节跳过</remarks>
        /// <param name="dir">CSV 所在目录</param>
        /// <param name="importAxes">是否导入并替换轴节</param>
        /// <param name="importIos">是否导入并替换 IO 节</param>
        /// <param name="importCylinders">是否导入并替换气缸节</param>
        /// <param name="msg">成功回传导入摘要，失败回传全部校验错误</param>
        /// <returns>导入成功返回 true</returns>
        public static bool Import(string dir, bool importAxes, bool importIos, bool importCylinders, ref string msg)
        {
            return Import(dir, importAxes, importIos, importCylinders, string.Empty, ref msg);
        }

        /// <summary>分节导入</summary>
        /// <remarks>复用上层备份目录：一次操作只备份一次</remarks>
        /// <param name="dir">CSV 所在目录</param>
        /// <param name="importAxes">是否导入轴</param>
        /// <param name="importIos">是否导入 IO</param>
        /// <param name="importCylinders">是否导入气缸</param>
        /// <param name="backupFolder">上层已创建的备份目录；空 = 由 SaveConfig 自行创建</param>
        /// <param name="msg">成功回传导入摘要，失败回传全部校验错误</param>
        /// <returns>导入成功返回 true</returns>
        public static bool Import(string dir, bool importAxes, bool importIos, bool importCylinders,
            string backupFolder, ref string msg)
        {
            List<string> errors = new List<string>();

            List<DeltaAxisConfig> axes = importAxes ? ImportAxes(dir, errors) : null;

            List<DeltaIOConfig> inputs = new List<DeltaIOConfig>();
            List<DeltaIOConfig> outputs = new List<DeltaIOConfig>();
            HashSet<string> inputNames = new HashSet<string>();
            HashSet<string> outputNames = new HashSet<string>();

            DeltaMotionConfig config = MontionManager.Instance.MoveData.MotionConfig;
            if (importIos)
            {
                ImportIOs(dir, inputs, outputs, inputNames, outputNames, errors);
            }
            else
            {
                // IO 节跳过时，气缸校验仍以既有 IO 名为准
                foreach (DeltaIOConfig cfg in config.Inputs) inputNames.Add(cfg.IOName);
                foreach (DeltaIOConfig cfg in config.Outputs) outputNames.Add(cfg.IOName);
            }

            List<DeltaCylinderConfig> cylinders = importCylinders
                ? ImportCylinders(dir, inputNames, outputNames, errors)
                : null;

            if (errors.Count > 0)
            {
                msg = string.Join("；", errors.ToArray());
                Log("导入校验失败 " + errors.Count + " 项", MessageLevel.Error);
                return false;
            }

            if (importAxes)
            {
                config.Axes.Clear();
                foreach (DeltaAxisConfig cfg in axes) config.Axes.Add(cfg);

                // TA 回零参数唯一真相源为 ROOT/TA_HOME，CSV 导入即同步
                DeltaAxisConfig taAxis = config.GetAxis(config.TaAxisName);
                if (taAxis != null && config.TaHome != null)
                {
                    config.TaHome.CopyFrom(taAxis);
                    Log("TA 回零参数已随 CSV 导入同步至 ROOT/TA_HOME（TA 轴 " + config.TaAxisName + "）");
                }
            }

            if (importIos)
            {
                config.Inputs.Clear();
                foreach (DeltaIOConfig cfg in inputs) config.Inputs.Add(cfg);

                config.Outputs.Clear();
                foreach (DeltaIOConfig cfg in outputs) config.Outputs.Add(cfg);
            }

            if (importCylinders)
            {
                config.Cylinders.Clear();
                foreach (DeltaCylinderConfig cfg in cylinders) config.Cylinders.Add(cfg);
            }

            config.SaveConfig(backupFolder);
            msg = string.Format("导入成功 轴 {0} 输入 {1} 输出 {2} 气缸 {3}",
                importAxes ? axes.Count : config.Axes.Count,
                importIos ? inputs.Count : config.Inputs.Count,
                importIos ? outputs.Count : config.Outputs.Count,
                importCylinders ? cylinders.Count : config.Cylinders.Count);
            // 摘要只随返回值上抛，由「重载保存完成」一次输出；此处不再单独打印（Debug 也会进界面日志）
            return true;
        }

        /// <summary>把当前运控配置导出为三张 CSV 配置表</summary>
        /// <param name="dir">导出目录，不存在时自动创建</param>
        /// <param name="msg">成功回传导出摘要，失败回传错误描述</param>
        /// <returns>导出成功返回 true</returns>
        /// <remarks>表头与导入侧逐字一致可原样回填；只读实例不改内存状态，导出失败不影响运行期。</remarks>
        public static bool Export(string dir, ref string msg)
        {
            if (string.IsNullOrEmpty(dir))
            {
                msg = "导出目录为空";
                Log(msg, MessageLevel.Error);
                return false;
            }

            DeltaMotionConfig config = MontionManager.Instance.MoveData.MotionConfig;

            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                WriteAllCsv(dir, config);
            }
            catch (Exception ex)
            {
                msg = "导出失败 " + ex.Message;
                Log(msg, MessageLevel.Error);
                return false;
            }

            msg = string.Format("导出成功 轴 {0} 输入 {1} 输出 {2} 气缸 {3} 目录 {4}",
                config.Axes.Count, config.Inputs.Count, config.Outputs.Count, config.Cylinders.Count, dir);
            Log(msg);
            return true;
        }

        #endregion
    }
}
