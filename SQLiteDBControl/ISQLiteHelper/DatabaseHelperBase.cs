using System;
using System.IO;
using System.Windows.Forms;
using AdaWeldSystem.SQLiteDBControl.SQLiteCore;

namespace AdaWeldSystem.SQLiteDBControl.ISQLiteHelper
{
    /// <summary>
    /// 数据库 Helper 抽象基类
    /// 提供数据库文件路径管理、连接初始化和通用辅助方法
    /// </summary>
    public abstract class DatabaseHelperBase : IDisposable
    {
        private string _dbPath;
        private string _dbFileName;
        private string _dbFilePath;
        private bool _disposed;

        /// <summary>
        /// SQLite数据库操作实例
        /// </summary>
        protected SQLiteDataBase Database { get; private set; }

        /// <summary>
        /// 数据库文件完整路径
        /// </summary>
        protected string DatabaseFilePath => _dbFilePath;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbFileName">数据库文件名（含扩展名）</param>
        protected DatabaseHelperBase(string dbFileName)
        {
            _dbFileName = dbFileName ?? throw new ArgumentNullException(nameof(dbFileName));
            InitializeDatabasePath();
            InitializeDatabase();
        }

        /// <summary>
        /// 初始化数据库文件路径
        /// </summary>
        private void InitializeDatabasePath()
        {
            _dbPath = Path.Combine(Application.StartupPath, "DataBase");
            if (!Directory.Exists(_dbPath))
            {
                Directory.CreateDirectory(_dbPath);
            }
            _dbFilePath = Path.Combine(_dbPath, _dbFileName);
        }

        /// <summary>
        /// 初始化数据库连接
        /// </summary>
        private void InitializeDatabase()
        {
            Database = new SQLiteDataBase(_dbFilePath);
        }

        /// <summary>
        /// 释放资源（按 ADR-020 使用标准 IDisposable.Dispose，不重写 Dispose(bool)）
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Database?.Dispose();
            Database = null;
            _disposed = true;
        }

        /// <summary>
        /// 检查数据库文件是否存在
        /// </summary>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsDatabaseFileExist()
        {
            return File.Exists(_dbFilePath);
        }

        /// <summary>
        /// 构建值数组（辅助方法）
        /// </summary>
        /// <param name="values">值数组</param>
        /// <returns>值数组</returns>
        protected static string[] BuildValues(params string[] values)
        {
            return values;
        }
    }
}
