using System;
using System.Data;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.SQLiteDBControl.ISQLiteHelper;

namespace AdaWeldSystem.SQLiteDBControl.Helpers
{
    /// <summary>
    /// 产品数据库 Helper。
    /// 负责产品检测数据按日期分表存储和查询。
    /// </summary>
    public class ProductDatabaseHelper : DatabaseHelperBase
    {
        /// <summary>产品表列契约：SN 产品码、Time 起焊时间、Status 状态、Result 结果、Info 原因。</summary>
        private readonly string[] _productColumns = { "SN", "Time", "Status", "Result", "Info" };

        private string _currentTableName;
        private DataTable _currentTable;

        /// <summary>
        /// 当前选中的表名
        /// </summary>
        public string CurrentTableName => _currentTableName;

        /// <summary>
        /// 当前选中的数据表
        /// </summary>
        public DataTable CurrentTable => _currentTable;

        /// <summary>
        /// 构造函数，初始化产品数据库
        /// </summary>
        public ProductDatabaseHelper() : base("Product.db")
        {
            // 初始化当天数据表（如果不存在）
            _currentTableName = GetTableNameByDate(DateTime.Now);
            EnsureTableSchema(_currentTableName);
        }

        /// <summary>校验并修正数据表结构：表不存在则建表；已存在</summary>
        /// <remarks>仅作用于当天表（跨天自动新建）；历史日期表原样保留，不做破坏性重建。 但列集与现行契约不一致（历史 InspectionType 列）则重建。</remarks>
        /// <param name="tableName">目标表名</param>
        private void EnsureTableSchema(string tableName)
        {
            if (!Database.IsExistTable(tableName))
            {
                Database.CreateTable(tableName, _productColumns);
                return;
            }

            DataTable existing = Database.Select(tableName);
            // 建表恒带 AUTOID 列，按业务列是否齐全判定
            // 不能用 Columns.Count==业务列数，否则 6!=5 每次写都误 DROP 重建、清掉历史记录
            if (existing == null || existing.Columns.Count < _productColumns.Length)
            {
                Database.DropTable(tableName);
                Database.CreateTable(tableName, _productColumns);
                return;
            }

            foreach (string column in _productColumns)
            {
                if (!existing.Columns.Contains(column))
                {
                    Database.DropTable(tableName);
                    Database.CreateTable(tableName, _productColumns);
                    return;
                }
            }
        }

        /// <summary>
        /// 根据日期生成表名
        /// </summary>
        /// <param name="date">日期</param>
        /// <returns>表名（格式：TyyyyMMdd）</returns>
        private static string GetTableNameByDate(DateTime date)
        {
            return "T" + date.ToString("yyyyMMdd");
        }

        /// <summary>建一张与产品记录列契约一致、零行的表（区间合</summary>
        /// <remarks>并的累加目标）。</remarks>
        private DataTable CreateEmptyProductTable()
        {
            DataTable table = new DataTable();
            foreach (string column in _productColumns)
            {
                table.Columns.Add(column, typeof(string));
            }
            return table;
        }

        /// <summary>把单日表的数据行按业务列名克隆进合并表（按</summary>
        /// <remarks>名取列，容忍单日表多出的 AUTOID 列）。</remarks>
        /// <param name="target">目标位置(mm)。</param>
        private static void AppendRows(DataTable target, DataTable day)
        {
            foreach (DataRow row in day.Rows)
            {
                DataRow added = target.NewRow();
                foreach (DataColumn column in target.Columns)
                {
                    if (!day.Columns.Contains(column.ColumnName)) continue;
                    object value = row[column.ColumnName];
                    added[column] = value == null || value == DBNull.Value
                        ? string.Empty : value.ToString();
                }
                target.Rows.Add(added);
            }
        }

        /// <summary>
        /// 切换到指定日期的数据表
        /// </summary>
        /// <param name="date">目标日期</param>
        public void SelectTable(DateTime date)
        {
            _currentTableName = GetTableNameByDate(date);
            _currentTable = Database.Select(_currentTableName);
        }

        /// <summary>按时间区间读取生产记录（逐日读表后合并）</summary>
        /// <param name="start">区间起始日（含）</param>
        /// <param name="end">区间结束日（含）</param>
        /// <returns>
        /// 合并后记录表，列同 <see cref="SelectTable"/> 单日表。
        /// 无记录时返回空表，不返回 null。
        /// </returns>
        /// <remarks>按日分表，逐日读取后按列序合并。
        /// 缺表日期跳过；单日查表异常只记日志并跳过。
        /// 不向界面抛错。区间为闭区间，起止颠倒时自动交换。</remarks>
        public DataTable SelectTables(DateTime start, DateTime end)
        {
            if (start.Date > end.Date)
            {
                DateTime swap = start;
                start = end;
                end = swap;
            }

            DataTable merged = CreateEmptyProductTable();
            DateTime cursor = start.Date;
            // 上限保护：最多查 366 天，避免上千次查询
            for (int guard = 0; guard <= 366 && cursor <= end.Date; guard++)
            {
                string tableName = GetTableNameByDate(cursor);
                // 缺表直接跳过：Database.Select 对不存在的表会抛 SQLiteException（GetDataSet 上抛），不能直连
                if (Database.IsExistTable(tableName))
                {
                    try
                    {
                        DataTable day = Database.Select(tableName);
                        if (day != null && day.Rows.Count > 0)
                        {
                            AppendRows(merged, day);
                        }
                    }
                    catch (Exception ex)
                    {
                        GlobalCommData.ShowLog("ProductDatabaseHelper",
                            string.Format("区间查询 {0} 查表失败 {1}", cursor.ToString("yyyyMMdd"), ex.Message),
                            MessageLevel.Warning);
                    }
                }
                cursor = cursor.AddDays(1);
            }
            return merged;
        }

        /// <summary>添加一条生产记录（焊毕一次性写入：产品码 + 起焊</summary>
        /// <remarks>时间 + 记录状态 + 结果 + 异常原因）</remarks>
        /// <param name="sn">产品码（空缺时由调用方以 TEST + 时间后缀填充）</param>
        /// <param name="startTime">焊接开始时间</param>
        /// <param name="status">记录状态（模拟 / 正常生产）</param>
        /// <param name="result">焊接结果（pass / fail）</param>
        /// <param name="info">异常焊接原因（result=fail 时有效）</param>
        public void AddProductInfo(string sn, DateTime startTime, string status, string result, string info)
        {
            EnsureTodayTableExists();

            var values = BuildValues(
                sn ?? string.Empty,
                startTime.ToString("yyyyMMddHHmmss"),
                status ?? string.Empty,
                result ?? string.Empty,
                info ?? string.Empty
            );

            Database.Insert(GetTableNameByDate(DateTime.Now), _productColumns, values);
        }

        /// <summary>确保当天数据表存在（跨天时自动创建新表；列契</summary>
        /// <remarks>约不符时重建当天表）</remarks>
        public void EnsureTodayTableExists()
        {
            EnsureTableSchema(GetTableNameByDate(DateTime.Now));
        }

        /// <summary>
        /// 获取指定日期的产品数据
        /// </summary>
        /// <param name="date">目标日期</param>
        /// <returns>产品数据表</returns>
        public DataTable GetProductDataByDate(DateTime date)
        {
            string tableName = GetTableNameByDate(date);
            return Database.Select(tableName);
        }

        /// <summary>
        /// 获取当天产品数据
        /// </summary>
        /// <returns>产品数据表</returns>
        public DataTable GetTodayProductData()
        {
            EnsureTodayTableExists();
            return Database.Select(GetTableNameByDate(DateTime.Now));
        }

        /// <summary>
        /// 删除N天前的历史数据表
        /// </summary>
        /// <param name="keepDays">保留天数</param>
        public void CleanOldData(int keepDays)
        {
            if (keepDays < 1)
            {
                throw new ArgumentException("保留天数必须大于0", nameof(keepDays));
            }

            Database.DropDateTable(keepDays);
        }

        /// <summary>
        /// 检查指定日期的表是否存在
        /// </summary>
        /// <param name="date">目标日期</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsTableExist(DateTime date)
        {
            return Database.IsExistTable(GetTableNameByDate(date));
        }

        /// <summary>
        /// 获取产品数据表中的记录数量
        /// </summary>
        /// <param name="date">目标日期</param>
        /// <returns>记录数量</returns>
        public int GetRecordCount(DateTime date)
        {
            DataTable table = GetProductDataByDate(date);
            return table?.Rows.Count ?? 0;
        }
    }
}
