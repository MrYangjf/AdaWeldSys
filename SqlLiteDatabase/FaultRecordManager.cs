using System;
using System.Data;

namespace AdaWeldSystem.SqlLiteDatabase
{
    /// <summary>故障记录数据库管理器</summary>
    /// <remarks>
    /// FaultRecord 表的建表、插入与查询。单例，数据库文件位于 DataBase\FaultRecord.db。
    /// 只负责持久化，故障恢复闭环逻辑见 <see cref="AdaWeldSystem.ProductFileManager.FaultRecoveryManager"/>；
    /// 领域载体 FaultRecord/FaultCategory 定义于 ProductFileManager，本类不依赖领域类型，
    /// 写入以字符串值数组为契约（按 Columns 顺序）。
    /// </remarks>
    public class FaultRecordManager : DatabaseManagerBase
    {
        #region 私有变量

        private static readonly Lazy<FaultRecordManager> _lazy =
            new Lazy<FaultRecordManager>(() => new FaultRecordManager());

        private const string TableName = "FaultRecord";

        private static readonly string[] Columns = new string[]
        {
            "Time", "Device", "State", "Category", "ErrorCode", "ParamSnapshot", "AutoRecovered", "RecoveryAction"
        };

        private static readonly int[] ColumnLengths = new int[] { 24, 32, 16, 16, 32, 200, 8, 64 };

        private readonly object _tableLock = new object();
        private bool _tableReady;

        #endregion

        #region 公共变量

        /// <summary>单例入口</summary>
        public static FaultRecordManager Instance => _lazy.Value;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数，初始化故障记录数据库
        /// </summary>
        private FaultRecordManager() : base("FaultRecord.db")
        {
        }

        #endregion

        #region 私有函数

        /// <summary>
        /// 确认 FaultRecord 表存在（首次写入前建表）
        /// </summary>
        private void EnsureTable()
        {
            if (_tableReady) return;
            lock (_tableLock)
            {
                if (_tableReady) return;
                Database.CreateTable(TableName, Columns, ColumnLengths);
                _tableReady = true;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>
        /// 写入一条故障记录
        /// </summary>
        /// <param name="values">按 Columns 顺序的值数组：Time/Device/State/Category/ErrorCode/ParamSnapshot/AutoRecovered/RecoveryAction</param>
        /// <returns>写入成功返回 true</returns>
        public bool InsertFaultRecord(string[] values)
        {
            EnsureTable();
            lock (_tableLock)
            {
                return Database.Insert(TableName, Columns, values);
            }
        }

        /// <summary>
        /// 查询全部故障记录
        /// </summary>
        /// <returns>故障记录表（Time/Device/State/Category/ErrorCode/ParamSnapshot/AutoRecovered/RecoveryAction 列）</returns>
        public DataTable SelectFaultRecords()
        {
            EnsureTable();
            lock (_tableLock)
            {
                return Database.Select(TableName);
            }
        }

        #endregion
    }
}
