using AdaWeldSystem.Comm;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace AdaWeldSystem.SqlLiteDatabase
{
    /// <summary>
    /// 权限管理器
    /// 负责用户认证、权限级别查询和账户管理
    /// </summary>
    public class AuthManager : DatabaseManagerBase
    {
        private readonly string[] _userColumns = { "UserName", "PassWord", "UserLevel" };
        private readonly string _tableName = "UserTable";
        private  Dictionary<string, OperateLevel> Userlevel = new Dictionary<string, OperateLevel>();

        /// <summary>
        /// 构造函数，初始化权限管理数据库
        /// </summary>
        public AuthManager() : base("Auth.db")
        {
            InitializeUserTable();
        }

        /// <summary>
        /// 初始化用户表（如果不存在）
        /// </summary>
        private void InitializeUserTable()
        {
            if (!Database.IsExistTable(_tableName))
            {
                Database.CreateTable(_tableName, _userColumns);
                // 插入默认管理员账户
                AddUser("Admin", "MINO123456", "0");
                AddUser("Engineer", "123", "1");
            }
            Userlevel.Add("0", OperateLevel.Admin);
            Userlevel.Add("1", OperateLevel.Engineer);
            Userlevel.Add("2", OperateLevel.Operator);
        }

        /// <summary>
        /// 添加新用户
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="userLevel">用户级别</param>
        /// <returns>添加成功返回true，失败返回false</returns>
        public bool AddUser(string userName, string password, string userLevel)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new ArgumentException("用户名不能为空", nameof(userName));
            }

            var values = BuildValues(userName, password ?? string.Empty, userLevel ?? "操作员");
            return Database.Insert(_tableName, _userColumns, values);
        }

        /// <summary>
        /// 验证用户登录
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>验证成功返回true，失败返回false</returns>
        public bool ValidateUser(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            string sqlStr = "SELECT [UserName] FROM [UserTable] WHERE [UserName] = @p0 AND [PassWord] = @p1";
            var parameters = new SQLiteParameter[]
            {
                new SQLiteParameter("@p0", userName),
                new SQLiteParameter("@p1", password)
            };

            DataSet dataset = Database.GetDataSet(sqlStr, parameters);
            return dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0;
        }

        /// <summary>
        /// 获取用户权限级别
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <returns>用户级别字符串，用户不存在返回空字符串</returns>
        public string GetUserLevel(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return string.Empty;
            }

            return Database.GetTableCellValue(_tableName, "UserLevel", "UserName", userName);
        }

        public OperateLevel GetUserOperate(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return OperateLevel.Operator;
            }

            var userLevel = GetUserLevel(userName);
            if (Userlevel.ContainsKey(userLevel))
            {
                return Userlevel[userLevel];
            }

            return OperateLevel.Operator;
        }

        /// <summary>
        /// 检查用户是否存在
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsUserExist(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return false;
            }

            return Database.HasTableValue(_tableName, "UserName", userName);
        }

        /// <summary>
        /// 更新用户密码
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>更新成功返回true，失败返回false</returns>
        public bool UpdatePassword(string userName, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new ArgumentException("用户名不能为空", nameof(userName));
            }

            var columns = new[] { "PassWord" };
            var values = new[] { newPassword ?? string.Empty };
            return Database.Update(_tableName, columns, values, "UserName", userName);
        }

        /// <summary>
        /// 获取所有用户信息（不含密码）
        /// </summary>
        /// <summary>
        /// 获取所有用户信息（不含密码）
        /// </summary>
        public DataTable GetAllUsers()
        {
            try
            {
                return Database.Select(_tableName);
            }
            catch (Exception ex)
            {
                throw new Exception($"获取用户列表失败 原因是 {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定用户的等级（从数据库直接查询）
        /// </summary>
        public string GetUserLevelFromDb(string name)
        {
            if (!IsUserExist(name))
            {
                return null;
            }
            try
            {
                return Database.GetTableCellValue(_tableName, "UserLevel", "UserName", name);
            }
            catch (Exception ex)
            {
                throw new Exception($"获取用户等级失败 原因是 {ex.Message}");
            }
        }

        /// <summary>
        /// 更新用户等级
        /// </summary>
        public bool UpdateUserLevel(string name, string level)
        {
            if (!IsUserExist(name))
            {
                return false;
            }
            try
            {
                var columns = new[] { "UserLevel" };
                var values = new[] { level };
                return Database.Update(_tableName, columns, values, "UserName", name);
            }
            catch (Exception ex)
            {
                throw new Exception($"更新用户等级失败 原因是 {ex.Message}");
            }
        }
    }
}
