using System;
using AdaWeldSystem.SQLiteDBControl.Helpers;

namespace AdaWeldSystem.SQLiteDBControl
{
    /// <summary>
    /// SQLite 数据库管理器。
    /// 统一持有各功能 Helper，是对外唯一访问口径。
    /// </summary>
    public class SQLiteDatabaseManager : IDisposable
    {
        private static readonly Lazy<SQLiteDatabaseManager> _lazyInstance =
            new Lazy<SQLiteDatabaseManager>(() => new SQLiteDatabaseManager());

        private bool _disposed;

        /// <summary>
        /// 全局唯一实例
        /// </summary>
        public static SQLiteDatabaseManager Instance => _lazyInstance.Value;

        /// <summary>
        /// 权限 Helper
        /// </summary>
        public AuthHelper AuthHelper { get; private set; }

        /// <summary>
        /// 自动参数 Helper
        /// </summary>
        public AutoParamHelper AutoParamHelper { get; private set; }

        /// <summary>
        /// 配置 Helper
        /// </summary>
        public ConfigHelper ConfigHelper { get; private set; }

        /// <summary>
        /// 产品数据库 Helper
        /// </summary>
        public ProductDatabaseHelper ProductDatabaseHelper { get; private set; }

        /// <summary>
        /// 构造函数，初始化所有 Helper
        /// </summary>
        private SQLiteDatabaseManager()
        {
            AuthHelper = new AuthHelper();
            AutoParamHelper = new AutoParamHelper();
            ConfigHelper = new ConfigHelper();
            ProductDatabaseHelper = new ProductDatabaseHelper();
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，不重写 Dispose(bool)）
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            AuthHelper?.Dispose();
            AutoParamHelper?.Dispose();
            ConfigHelper?.Dispose();
            ProductDatabaseHelper?.Dispose();

            _disposed = true;
        }
    }
}
