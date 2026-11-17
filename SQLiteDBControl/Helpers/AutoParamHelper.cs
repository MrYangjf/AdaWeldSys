using System;
using System.Data;
using AdaWeldSystem.SQLiteDBControl.ISQLiteHelper;

namespace AdaWeldSystem.SQLiteDBControl.Helpers
{
    /// <summary>
    /// 自动参数值 Helper，负责自动参数 JSON 序列化读写。
    /// 每个 AutoParam 配置对应一张独立数据表。
    /// 表名取 identityInfo 字段。
    /// </summary>
    public class AutoParamHelper : DatabaseHelperBase
    {
        private readonly string[] _paramColumns = { "焊缝宽度", "送丝速度", "激光功率", "焊接速度", "光斑宽度", "振镜摆幅", "摆幅频率" };
        private string _currentTableName;

        /// <summary>
        /// 默认参数标识，与业务侧常量保持一致
        /// </summary>
        public const string DefaultIdentity = "DefaultParam";

        /// <summary>
        /// 当前选中的表名
        /// </summary>
        public string CurrentTableName => _currentTableName;

        /// <summary>
        /// 构造函数，初始化自动参数值数据库
        /// </summary>
        public AutoParamHelper() : base("AutoParamValue.db")
        {
            _currentTableName = DefaultIdentity;
        }

        /// <summary>
        /// 创建参数值表（如果不存在）
        /// </summary>
        /// <param name="tableName">表名</param>
        private void CreateTableIfNotExist(string tableName)
        {
            if (!Database.IsExistTable(tableName))
            {
                Database.CreateTable(tableName, _paramColumns);
            }
            else
            {
                EnsureSpotWidthColumn(tableName);
                EnsureGalvoAmplitudeColumn(tableName);
                EnsureSwingFrequencyColumn(tableName);
            }
        }

        /// <summary>补「振镜摆幅」列（第 6 列，历史表为 5 列，缺列时补列）：有实</summary>
        /// <remarks>测值优先查表、无记录走「焊缝宽度 ×(1+k)」公式；旧行该列为空即未标定。</remarks>
        /// <param name="tableName">表名</param>
        private void EnsureGalvoAmplitudeColumn(string tableName)
        {
            try
            {
                DataTable schema = Database.GetDataTable(string.Format(
                    "SELECT * FROM [{0}] LIMIT 1", tableName));
                if (schema != null && !schema.Columns.Contains("振镜摆幅"))
                {
                    Database.ExecuteSQL(string.Format(
                        "ALTER TABLE [{0}] ADD COLUMN [振镜摆幅] TEXT", tableName));
                }
            }
            catch
            {
                // 列已存在或探测失败则静默跳过
            }
        }


        /// <summary>补「摆幅频率」列（第 7 列，随宽度自适应）</summary>
        /// <remarks>查表优先，公式初值入表后可手动核对。旧「振镜频率」列保留不删。</remarks>
        /// <param name="tableName">表名</param>
        private void EnsureSwingFrequencyColumn(string tableName)
        {
            try
            {
                DataTable schema = Database.GetDataTable(string.Format(
                    "SELECT * FROM [{0}] LIMIT 1", tableName));
                if (schema != null && !schema.Columns.Contains("摆幅频率"))
                {
                    Database.ExecuteSQL(string.Format(
                        "ALTER TABLE [{0}] ADD COLUMN [摆幅频率] TEXT", tableName));
                }
            }
            catch
            {
                // 列已存在或探测失败则静默跳过
            }
        }
        /// <summary>补「光斑宽度」列（历史表为 4 列，缺列时补列）；旧行该列</summary>
        /// <remarks>为空，读取侧按「焊缝宽度 × W」现算兜底。</remarks>
        /// <param name="tableName">表名</param>
        private void EnsureSpotWidthColumn(string tableName)
        {
            try
            {
                DataTable schema = Database.GetDataTable(string.Format(
                    "SELECT * FROM [{0}] LIMIT 1", tableName));
                if (schema != null && !schema.Columns.Contains("光斑宽度"))
                {
                    Database.ExecuteSQL(string.Format(
                        "ALTER TABLE [{0}] ADD COLUMN [光斑宽度] TEXT", tableName));
                }
            }
            catch
            {
                // 列已存在或探测失败则静默跳过
            }
        }

        /// <summary>读取摆幅频率列（焊缝宽度→摆幅频率 Hz）。</summary>
        /// <remarks>缺列或异常时返回空串，由读取侧兜底。</remarks>
        /// <returns>摆幅频率 JSON（可能为空）</returns>
        private string GetSwingFrequencyColumnSafe()
        {
            try
            {
                return Database.GetTableColumnValue(_currentTableName, "摆幅频率");
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>读取摆幅宽度列（焊缝宽度→摆幅宽度）。</summary>
        /// <remarks>缺列或异常时返回空串，由读取侧兜底。</remarks>
        /// <returns>摆幅宽度 JSON（可能为空）</returns>
        private string GetSwingWidthColumnSafe()
        {
            try
            {
                return Database.GetTableColumnValue(_currentTableName, "振镜摆幅");
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>读取光斑宽度列（旧表缺列或读取异常时返回空</summary>
        /// <remarks>串，由读取侧按公式兜底）。</remarks>
        /// <returns>光斑宽度 JSON（可能为空）</returns>
        private string GetSpotWidthColumnSafe()
        {
            try
            {
                return Database.GetTableColumnValue(_currentTableName, "光斑宽度");
            }
            catch            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 更新当前表中的参数值
        /// </summary>
        /// <param name="profile">分段参数数据</param>
        private void UpdateParamValues(WeldAutoParamProfile profile)
        {
            var columns = new[] { "焊缝宽度", "送丝速度", "激光功率", "焊接速度", "光斑宽度", "振镜摆幅", "摆幅频率" };
            var values = new[]
            {
                profile.SeamWidthJson ?? string.Empty,
                profile.FeedSpeedJson ?? string.Empty,
                profile.LaserPowerJson ?? string.Empty,
                profile.RobotSpeedJson ?? string.Empty,
                profile.SpotWidthJson ?? string.Empty,
                profile.SwingWidthJson ?? string.Empty,
                profile.SwingFrequencyJson ?? string.Empty
            };

            Database.Update(_currentTableName, columns, values);
        }

        /// <summary>
        /// 检查指定名称的参数值表是否存在
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsTableExist(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return false;
            }

            return Database.IsExistTable(tableName);
        }

        /// <summary>
        /// 删除指定名称的参数值表
        /// </summary>
        /// <param name="tableName">表名</param>
        public void DeleteTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return;
            }

            Database.DropTable(tableName);
        }

        /// <summary>
        /// 选择并加载指定标识的参数值表
        /// </summary>
        /// <param name="identityInfo">自动参数标识</param>
        public void SelectTable(string identityInfo)
        {
            if (string.IsNullOrWhiteSpace(identityInfo))
            {
                throw new ArgumentNullException(nameof(identityInfo));
            }

            _currentTableName = identityInfo;

            if (!Database.IsExistTable(_currentTableName))
            {
                CreateTableIfNotExist(_currentTableName);
            }
            else
            {
                EnsureSpotWidthColumn(_currentTableName);
                EnsureGalvoAmplitudeColumn(_currentTableName);
                EnsureSwingFrequencyColumn(_currentTableName);
            }
        }

        /// <summary>
        /// 加载指定标识的分段参数数据
        /// </summary>
        /// <param name="identityInfo">自动参数标识</param>
        /// <returns>分段参数数据对象</returns>
        public WeldAutoParamProfile LoadProfile(string identityInfo)
        {
            SelectTable(identityInfo);

            return new WeldAutoParamProfile
            {
                SeamWidthJson = Database.GetTableColumnValue(_currentTableName, "焊缝宽度"),
                FeedSpeedJson = Database.GetTableColumnValue(_currentTableName, "送丝速度"),
                LaserPowerJson = Database.GetTableColumnValue(_currentTableName, "激光功率"),
                RobotSpeedJson = Database.GetTableColumnValue(_currentTableName, "焊接速度"),
                SpotWidthJson = GetSpotWidthColumnSafe(),
                SwingWidthJson = GetSwingWidthColumnSafe(),
                SwingFrequencyJson = GetSwingFrequencyColumnSafe()
            };
        }

        /// <summary>
        /// 保存分段参数数据到指定标识的表
        /// </summary>
        /// <param name="identityInfo">自动参数标识</param>
        /// <param name="profile">分段参数数据</param>
        public void SaveProfile(string identityInfo, WeldAutoParamProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            SelectTable(identityInfo);
            // 一配置一表一行：写入前清空旧行。INSERT 追加会让读取侧（只认第 0 行）读到旧快照
            Database.ExecuteSQL(string.Format("DELETE FROM [{0}]", _currentTableName));
            AddParamValues(
                profile.SeamWidthJson,
                profile.FeedSpeedJson,
                profile.LaserPowerJson,
                profile.RobotSpeedJson,
                profile.SpotWidthJson,
                profile.SwingWidthJson,
                profile.SwingFrequencyJson);
        }

        /// <summary>
        /// 更新指定标识的表中的分段参数数据
        /// </summary>
        /// <param name="identityInfo">自动参数标识</param>
        /// <param name="profile">分段参数数据</param>
        public void UpdateProfile(string identityInfo, WeldAutoParamProfile profile)
        {
            if (string.IsNullOrWhiteSpace(identityInfo))
            {
                throw new ArgumentNullException(nameof(identityInfo));
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            SelectTable(identityInfo);
            if (GetRowCount(_currentTableName) == 0)
            {
                // 空表 UPDATE 命中 0 行会丢数据，改为插入
                AddParamValues(
                    profile.SeamWidthJson,
                    profile.FeedSpeedJson,
                    profile.LaserPowerJson,
                    profile.RobotSpeedJson,
                    profile.SpotWidthJson,
                    profile.SwingWidthJson,
                    profile.SwingFrequencyJson);
                return;
            }
            UpdateParamValues(profile);
        }

        /// <summary>
        /// 查询指定表的数据行数
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>数据行数；查询失败返回 0</returns>
        public int GetRowCount(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return 0;
            }

            string value = Database.GetDataString(string.Format(
                "SELECT COUNT(*) FROM [{0}]", tableName));
            return int.TryParse(value, out int count) ? count : 0;
        }

        /// <summary>
        /// 添加参数值记录
        /// </summary>
        /// <param name="width">焊缝宽度JSON</param>
        /// <param name="feedSpeed">送丝速度JSON</param>
        /// <param name="laserPower">激光功率JSON</param>
        /// <param name="robotSpeed">焊接速度JSON</param>
        /// <param name="spotWidth">光斑宽度JSON</param>
        /// <param name="swingWidth">摆幅宽度JSON（焊缝宽度→摆幅宽度，公式初值+手动修正）</param>
        /// <param name="swingFrequency">摆幅频率JSON（焊缝宽度→摆幅频率 Hz，公式初值+手动核对）</param>
        public void AddParamValues(string width, string feedSpeed, string laserPower, string robotSpeed,
            string spotWidth = null, string swingWidth = null, string swingFrequency = null)
        {
            var values = BuildValues(
                width ?? string.Empty,
                feedSpeed ?? string.Empty,
                laserPower ?? string.Empty,
                robotSpeed ?? string.Empty,
                spotWidth ?? string.Empty,
                swingWidth ?? string.Empty,
                swingFrequency ?? string.Empty
            );

            Database.Insert(_currentTableName, _paramColumns, values);
        }

        /// <summary>
        /// 获取指定列的参数值（JSON字符串）
        /// </summary>
        /// <param name="columnName">列名</param>
        /// <returns>JSON字符串</returns>
        public string GetParamValue(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return string.Empty;
            }

            return Database.GetTableColumnValue(_currentTableName, columnName);
        }

        /// <summary>安全读取指定列（缺列或异常时返回空串）</summary>
        /// <remarks>补列前调用会抛 SQL 异常，统一在此吞掉。</remarks>
        /// <param name="columnName">列名</param>
        /// <returns>列值 JSON（缺列或异常时为空串）</returns>
        public string GetParamValueSafe(string columnName)
        {
            try
            {
                return GetParamValue(columnName);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// 焊接自动参数分段数据中性传输对象。
    /// 在 DB 层与业务层之间传递自动参数 JSON。
    /// 避免 DB 层反向依赖业务类型。
    /// </summary>
    public class WeldAutoParamProfile
    {
        /// <summary>
        /// 焊缝宽度列表 JSON
        /// </summary>
        public string SeamWidthJson { get; set; }

        /// <summary>
        /// 送丝速度字典 JSON
        /// </summary>
        public string FeedSpeedJson { get; set; }

        /// <summary>
        /// 激光功率字典 JSON
        /// </summary>
        public string LaserPowerJson { get; set; }

        /// <summary>
        /// 焊接速度字典 JSON
        /// </summary>
        public string RobotSpeedJson { get; set; }

        /// <summary>
        /// 光斑宽度字典 JSON（= 焊缝宽度 × 系数 W）
        /// </summary>
        public string SpotWidthJson { get; set; }

        /// <summary>
        /// 摆幅宽度字典 JSON（公式初值，可手动修正）
        /// </summary>
        public string SwingWidthJson { get; set; }

        /// <summary>
        /// 摆幅频率字典 JSON（公式初值由摆幅宽度反算）
        /// </summary>
        public string SwingFrequencyJson { get; set; }

        /// <summary>
        /// 是否有任意一项承载实质数据
        /// </summary>
        /// <remarks>判据应为「承载实质数据」，不是字符串非空。
        /// 空表 `[]`/`{}` 亦属非空，会覆盖现有查找表。</remarks>
        public bool HasAnyValue
        {
            get
            {
                return IsMeaningfulJson(SeamWidthJson)
                    || IsMeaningfulJson(FeedSpeedJson)
                    || IsMeaningfulJson(LaserPowerJson)
                    || IsMeaningfulJson(RobotSpeedJson)
                    || IsMeaningfulJson(SpotWidthJson)
                    || IsMeaningfulJson(SwingWidthJson)
                    || IsMeaningfulJson(SwingFrequencyJson);
            }
        }

        /// <summary>
        /// 判断 JSON 串是否承载实质数据
        /// </summary>
        /// <param name="json">待判定的 JSON 串</param>
        /// <returns>承载实质数据返回 true；空白串、空数组 [] 、空对象 {} 返回 false</returns>
        public static bool IsMeaningfulJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            string trimmed = json.Trim();
            return trimmed != "[]" && trimmed != "{}";
        }
    }
}
