using System;
using System.Data;
using System.Data.SQLite;
using System.Text;

namespace AdaWeldSystem.SqlLiteDatabase
{
    /// <summary>
    /// SQLite数据库基础操作类
    /// 提供连接管理、SQL执行、数据查询等底层数据库操作
    /// </summary>
    public class SQLiteDataBase : IDisposable
    {
        private SQLiteConnection _connection;
        private string _connectionString;
        private bool _disposed;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SQLiteDataBase()
        {
        }

        /// <summary>
        /// 构造函数，指定数据库文件路径
        /// </summary>
        /// <param name="dataBasePath">SQLite数据库文件完整路径</param>
        public SQLiteDataBase(string dataBasePath)
        {
            _connectionString = string.Format("Data Source={0}", dataBasePath);
            _connection = new SQLiteConnection(_connectionString);
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~SQLiteDataBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的核心实现
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                }

                // 释放非托管资源
                if (_connection != null)
                {
                    try
                    {
                        if (_connection.State == ConnectionState.Open)
                        {
                            _connection.Close();
                        }
                        _connection.Dispose();
                    }
                    catch
                    {
                        // 静默处理释放异常
                    }
                    _connection = null;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public void OnInitConnect()
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("数据库连接未初始化，请先调用构造函数或ConnectDBFile方法");
            }
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void ExitConnect()
        {
            if (_connection != null && _connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// 连接到指定数据库文件
        /// </summary>
        /// <param name="dataBasePath">数据库文件路径</param>
        public void ConnectDBFile(string dataBasePath)
        {
            _connectionString = string.Format("Data Source={0}", dataBasePath);
            _connection = new SQLiteConnection(_connectionString);
        }

        /// <summary>
        /// 执行SQL语句（Insert/Update/Delete）
        /// </summary>
        /// <param name="strSQL">要执行的SQL语句</param>
        /// <returns>执行成功返回true，失败返回false</returns>
        public bool ExecuteSQL(string strSQL)
        {
            OnInitConnect();
            try
            {
                using (var cmd = new SQLiteCommand(strSQL, _connection))
                {
                    int count = cmd.ExecuteNonQuery();
                    return count >= 0;
                }
            }
            catch (Exception e)
            {
                throw new SQLiteException(string.Format("执行SQL失败 SqlString {0} 异常信息 {1}", strSQL, e.Message), e);
            }
            finally
            {
                ExitConnect();
            }
        }

        /// <summary>
        /// 执行参数化SQL语句
        /// </summary>
        /// <param name="strSQL">SQL语句（含参数占位符）</param>
        /// <param name="parameters">参数数组</param>
        /// <returns>执行成功返回true，失败返回false</returns>
        public bool ExecuteSQL(string strSQL, SQLiteParameter[] parameters)
        {
            OnInitConnect();
            try
            {
                using (var cmd = new SQLiteCommand(strSQL, _connection))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    int count = cmd.ExecuteNonQuery();
                    return count >= 0;
                }
            }
            catch (Exception e)
            {
                throw new SQLiteException(string.Format("执行SQL失败 SqlString {0} 异常信息 {1}", strSQL, e.Message), e);
            }
            finally
            {
                ExitConnect();
            }
        }

        /// <summary>
        /// 执行查询，返回DataSet
        /// </summary>
        /// <param name="strSQL">SQL查询语句</param>
        /// <returns>查询结果DataSet</returns>
        public DataSet GetDataSet(string strSQL)
        {
            var dataset = new DataSet();
            OnInitConnect();
            try
            {
                using (var adapter = new SQLiteDataAdapter(strSQL, _connection))
                {
                    adapter.Fill(dataset);
                }
            }
            catch (Exception e)
            {
                throw new SQLiteException(string.Format("GetDataSet失败 SqlString {0} 异常信息 {1}", strSQL, e.Message), e);
            }
            finally
            {
                ExitConnect();
            }
            return dataset;
        }

        /// <summary>
        /// 执行参数化查询，返回DataSet
        /// </summary>
        /// <param name="strSQL">SQL查询语句（含参数占位符）</param>
        /// <param name="parameters">参数数组</param>
        /// <returns>查询结果DataSet</returns>
        public DataSet GetDataSet(string strSQL, SQLiteParameter[] parameters)
        {
            var dataset = new DataSet();
            OnInitConnect();
            try
            {
                using (var adapter = new SQLiteDataAdapter(strSQL, _connection))
                {
                    if (parameters != null)
                    {
                        adapter.SelectCommand.Parameters.AddRange(parameters);
                    }
                    adapter.Fill(dataset);
                }
            }
            catch (Exception e)
            {
                throw new SQLiteException(string.Format("GetDataSet失败 SqlString {0} 异常信息 {1}", strSQL, e.Message), e);
            }
            finally
            {
                ExitConnect();
            }
            return dataset;
        }

        /// <summary>
        /// 检查指定表是否存在
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsExistTable(string tableName)
        {
            if (_connection == null)
            {
                return false;
            }

            try
            {
                _connection.Open();
                DataTable schemaTable = _connection.GetSchema("Tables");
                string filter = string.Format("TABLE_TYPE='table' AND TABLE_NAME='{0}'", tableName);
                DataRow[] rows = schemaTable.Select(filter);
                return rows.Length > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }
        }

        /// <summary>
        /// 删除N天前的表
        /// </summary>
        /// <param name="n">保留天数，之前的表全部删除</param>
        public void DropDateTable(double n)
        {
            if (_connection == null)
            {
                return;
            }

            try
            {
                _connection.Open();
                DataTable schemaTable = _connection.GetSchema("Tables", new string[] { null, null, null, "TABLE" });
                for (int i = 0; i < schemaTable.Rows.Count; i++)
                {
                    try
                    {
                        string tableName = schemaTable.Rows[i][2].ToString();
                        if (DateTime.TryParseExact(tableName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime tableDate))
                        {
                            DateTime cutoffDate = DateTime.Now.AddDays(-n);
                            if (tableDate <= cutoffDate)
                            {
                                string sqlStr = string.Format("DROP TABLE IF EXISTS '{0}'", tableName);
                                using (var cmd = new SQLiteCommand(sqlStr, _connection))
                                {
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 静默处理单个表删除失败
                    }
                }
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }
        }

        /// <summary>
        /// 判断文件是否存在
        /// </summary>
        /// <param name="strPathName">文件路径</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsExistFile(string strPathName)
        {
            return System.IO.File.Exists(strPathName);
        }

        /// <summary>
        /// 创建表（默认列类型为char(10)）
        /// </summary>
        /// <param name="strTableName">表名</param>
        /// <param name="tableColumn">列名数组</param>
        /// <returns>创建成功返回true，失败返回false</returns>
        public bool CreateTable(string strTableName, string[] tableColumn)
        {
            if (tableColumn == null || tableColumn.Length == 0)
            {
                return false;
            }

            try
            {
                if (!IsExistTable(strTableName))
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < tableColumn.Length; i++)
                    {
                        sb.Append(",[").Append(tableColumn[i]).Append("] char(10) not null");
                    }
                    OnInitConnect();
                    string strSQL = string.Format("CREATE TABLE {0}([AUTOID] INTEGER PRIMARY KEY AUTOINCREMENT{1})", strTableName, sb.ToString());
                    using (var cmd = new SQLiteCommand(strSQL, _connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                throw new SQLiteException(string.Format("创建表失败 {0}", e.Message), e);
            }
            finally
            {
                ExitConnect();
            }
        }

        /// <summary>
        /// 创建表（指定列长度）
        /// </summary>
        /// <param name="strTableName">表名</param>
        /// <param name="tableColumn">列名数组</param>
        /// <param name="columnLength">对应列的数据长度数组</param>
        /// <returns>创建成功返回true，失败返回false</returns>
        public bool CreateTable(string strTableName, string[] tableColumn, int[] columnLength)
        {
            if (tableColumn == null || tableColumn.Length == 0 || columnLength == null || columnLength.Length != tableColumn.Length)
            {
                return false;
            }

            try
            {
                var sb = new StringBuilder();
                for (int i = 0; i < tableColumn.Length; i++)
                {
                    sb.Append(",[").Append(tableColumn[i]).Append("] char(").Append(columnLength[i]).Append(") not null");
                }
                OnInitConnect();
                string strSQL = string.Format("CREATE TABLE {0}([AUTOID] INTEGER PRIMARY KEY AUTOINCREMENT{1})", strTableName, sb.ToString());
                using (var cmd = new SQLiteCommand(strSQL, _connection))
                {
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception e)
            {
                throw new SQLiteException(string.Format("创建表失败 {0}", e.Message), e);
            }
            finally
            {
                ExitConnect();
            }
        }

        /// <summary>
        /// 插入数据（参数化查询）
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="tableColumn">列名数组</param>
        /// <param name="values">值数组</param>
        /// <returns>插入成功返回true，失败返回false</returns>
        public bool Insert(string tableName, string[] tableColumn, string[] values)
        {
            if (tableColumn == null || values == null || tableColumn.Length != values.Length)
            {
                return false;
            }

            var sbColumns = new StringBuilder();
            var sbParameters = new StringBuilder();
            var parameters = new SQLiteParameter[tableColumn.Length];

            for (int i = 0; i < tableColumn.Length; i++)
            {
                if (i > 0)
                {
                    sbColumns.Append(",");
                    sbParameters.Append(",");
                }
                sbColumns.Append("[").Append(tableColumn[i]).Append("]");
                sbParameters.Append("@p").Append(i);
                parameters[i] = new SQLiteParameter("@p" + i, values[i] ?? string.Empty);
            }

            string sqlString = string.Format("INSERT INTO {0}({1}) VALUES({2})", tableName, sbColumns.ToString(), sbParameters.ToString());
            return ExecuteSQL(sqlString, parameters);
        }

        /// <summary>
        /// 更新数据（参数化查询，带WHERE条件）
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="tableColumn">列名数组</param>
        /// <param name="values">值数组</param>
        /// <param name="addressName">WHERE条件列名</param>
        /// <param name="value">WHERE条件值</param>
        /// <returns>更新成功返回true，失败返回false</returns>
        public bool Update(string tableName, string[] tableColumn, string[] values, string addressName, string value)
        {
            if (tableColumn == null || values == null || tableColumn.Length != values.Length)
            {
                return false;
            }

            var sb = new StringBuilder();
            var parameters = new System.Collections.Generic.List<SQLiteParameter>();

            for (int i = 0; i < tableColumn.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                sb.Append("[").Append(tableColumn[i]).Append("]=@p").Append(i);
                parameters.Add(new SQLiteParameter("@p" + i, values[i] ?? string.Empty));
            }

            sb.Append(" WHERE [").Append(addressName).Append("]=@w0");
            parameters.Add(new SQLiteParameter("@w0", value ?? string.Empty));

            string sqlString = string.Format("UPDATE {0} SET {1}", tableName, sb.ToString());
            return ExecuteSQL(sqlString, parameters.ToArray());
        }

        /// <summary>
        /// 更新数据（参数化查询，全表更新）
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="tableColumn">列名数组</param>
        /// <param name="values">值数组</param>
        /// <returns>更新成功返回true，失败返回false</returns>
        public bool Update(string tableName, string[] tableColumn, string[] values)
        {
            if (tableColumn == null || values == null || tableColumn.Length != values.Length)
            {
                return false;
            }

            var sb = new StringBuilder();
            var parameters = new SQLiteParameter[tableColumn.Length];

            for (int i = 0; i < tableColumn.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                sb.Append("[").Append(tableColumn[i]).Append("]=@p").Append(i);
                parameters[i] = new SQLiteParameter("@p" + i, values[i] ?? string.Empty);
            }

            string sqlString = string.Format("UPDATE {0} SET {1}", tableName, sb.ToString());
            return ExecuteSQL(sqlString, parameters);
        }

        /// <summary>
        /// 删除表
        /// </summary>
        /// <param name="tableName">表名</param>
        public void DropTable(string tableName)
        {
            string sqlStr = string.Format("DROP TABLE IF EXISTS '{0}'", tableName);
            ExecuteSQL(sqlStr);
        }

        /// <summary>
        /// 执行查询，返回DataTable
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <returns>查询结果DataTable</returns>
        public DataTable GetDataTable(string sqlString)
        {
            DataSet dataset = GetDataSet(sqlString);
            dataset.CaseSensitive = false;
            if (dataset.Tables.Count > 0)
            {
                return dataset.Tables[0];
            }
            return null;
        }

        /// <summary>
        /// 执行查询，返回DataRow
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <returns>查询结果DataRow</returns>
        public DataRow GetDataRow(string sqlString)
        {
            DataSet dataset = GetDataSet(sqlString);
            dataset.CaseSensitive = false;
            if (dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0)
            {
                return dataset.Tables[0].Rows[0];
            }
            return null;
        }

        /// <summary>
        /// 执行查询，返回单个字符串值
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <returns>查询结果字符串</returns>
        public string GetDataString(string sqlString)
        {
            DataSet dataset = GetDataSet(sqlString);
            dataset.CaseSensitive = false;
            if (dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0)
            {
                object value = dataset.Tables[0].Rows[0].ItemArray.GetValue(0);
                return value != null ? value.ToString() : string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取指定表中满足条件的单元格值
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="columnName">目标列名</param>
        /// <param name="conditionColumn">条件列名</param>
        /// <param name="conditionValue">条件值</param>
        /// <returns>查询结果字符串</returns>
        public string GetTableCellValue(string tableName, string columnName, string conditionColumn, string conditionValue)
        {
            string sqlStr = string.Format("SELECT [{0}] FROM [{1}] WHERE [{2}] = @p0", columnName, tableName, conditionColumn);
            var parameters = new SQLiteParameter[]
            {
                new SQLiteParameter("@p0", conditionValue ?? string.Empty)
            };
            return GetDataString(sqlStr, parameters);
        }

        /// <summary>
        /// 获取指定表中指定列的值
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="columnName">列名</param>
        /// <returns>查询结果字符串</returns>
        public string GetTableColumnValue(string tableName, string columnName)
        {
            string sqlStr = string.Format("SELECT [{0}] FROM [{1}]", columnName, tableName);
            return GetDataString(sqlStr);
        }

        /// <summary>
        /// 检查表中是否存在满足条件的记录
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="conditionColumn">条件列名</param>
        /// <param name="conditionValue">条件值</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool HasTableValue(string tableName, string conditionColumn, string conditionValue)
        {
            string sqlStr = string.Format("SELECT [{0}] FROM [{1}] WHERE [{2}] = @p0", conditionColumn, tableName, conditionColumn);
            var parameters = new SQLiteParameter[]
            {
                new SQLiteParameter("@p0", conditionValue ?? string.Empty)
            };
            DataSet dataset = GetDataSet(sqlStr, parameters);
            dataset.CaseSensitive = false;
            return dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0;
        }

        /// <summary>
        /// 删除表中满足条件的记录
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="conditionColumn">条件列名</param>
        /// <param name="conditionValue">条件值</param>
        /// <returns>删除成功返回true，失败返回false</returns>
        public bool DeleteTableByCondition(string tableName, string conditionColumn, string conditionValue)
        {
            string sqlStr = string.Format("DELETE FROM [{0}] WHERE [{1}] = @p0", tableName, conditionColumn);
            var parameters = new SQLiteParameter[]
            {
                new SQLiteParameter("@p0", conditionValue ?? string.Empty)
            };
            return ExecuteSQL(sqlStr, parameters);
        }

        /// <summary>
        /// 查询整张表
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>查询结果DataTable</returns>
        public DataTable Select(string tableName)
        {
            string sqlStr = string.Format("SELECT * FROM [{0}]", tableName);
            return GetDataTable(sqlStr);
        }

        /// <summary>
        /// 执行查询，返回单个字符串值（参数化）
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <param name="parameters">参数数组</param>
        /// <returns>查询结果字符串</returns>
        public string GetDataString(string sqlString, SQLiteParameter[] parameters)
        {
            DataSet dataset = GetDataSet(sqlString, parameters);
            dataset.CaseSensitive = false;
            if (dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0)
            {
                object value = dataset.Tables[0].Rows[0].ItemArray.GetValue(0);
                return value != null ? value.ToString() : string.Empty;
            }
            return string.Empty;
        }
    }
}
