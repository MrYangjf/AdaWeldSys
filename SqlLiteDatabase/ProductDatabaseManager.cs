using System;
using System.Data;

namespace AdaWeldSystem.SqlLiteDatabase
{
    /// <summary>
    /// 产品数据库管理器
    /// 负责产品检测数据的按日期分表存储和查询
    /// </summary>
    public class ProductDatabaseManager : DatabaseManagerBase
    {
        private readonly string[] _productColumns = { "SN", "Time", "InspectionType", "Result", "Info" };
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
        public ProductDatabaseManager() : base("Product.db")
        {
            InitializeTodayTable();
        }

        /// <summary>
        /// 初始化当天数据表（如果不存在）
        /// </summary>
        private void InitializeTodayTable()
        {
            _currentTableName = GetTableNameByDate(DateTime.Now);
            if (!Database.IsExistTable(_currentTableName))
            {
                Database.CreateTable(_currentTableName, _productColumns);
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

        /// <summary>
        /// 切换到指定日期的数据表
        /// </summary>
        /// <param name="date">目标日期</param>
        public void SelectTable(DateTime date)
        {
            _currentTableName = GetTableNameByDate(date);
            _currentTable = Database.Select(_currentTableName);
        }

        /// <summary>
        /// 添加产品检测信息到当天表
        /// </summary>
        /// <param name="sn">产品序列号</param>
        /// <param name="startTime">检测开始时间</param>
        /// <param name="inspectionType">检测类型</param>
        /// <param name="result">检测结果</param>
        /// <param name="checkInfo">检测详细信息</param>
        public void AddProductInfo(string sn, DateTime startTime, string inspectionType, string result, string checkInfo)
        {
            EnsureTodayTableExists();

            var values = BuildValues(
                sn ?? string.Empty,
                startTime.ToString("yyyyMMddHHmmss"),
                inspectionType ?? string.Empty,
                result ?? string.Empty,
                checkInfo ?? string.Empty
            );

            Database.Insert(GetTableNameByDate(DateTime.Now), _productColumns, values);
        }

        /// <summary>
        /// 确保当天数据表存在（跨天时自动创建新表）
        /// </summary>
        public void EnsureTodayTableExists()
        {
            string todayTableName = GetTableNameByDate(DateTime.Now);
            if (!Database.IsExistTable(todayTableName))
            {
                Database.CreateTable(todayTableName, _productColumns);
            }
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
