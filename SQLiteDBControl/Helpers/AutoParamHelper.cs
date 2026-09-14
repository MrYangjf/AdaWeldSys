using System;
using System.Data;

namespace AdaWeldSystem.SQLiteDBControl.Helpers
{
    /// <summary>
    /// 自动参数值 Helper
    /// 负责焊接自动参数（焊缝宽度、送丝速度、激光功率、焊接速度）的JSON序列化存储和读取
    /// 每个AutoParam配置对应一张独立的数据表，以identityInfo作为表名
    /// </summary>
    public class AutoParamHelper : DatabaseHelperBase
    {
        private readonly string[] _paramColumns = { "焊缝宽度", "送丝速度", "激光功率", "焊接速度" };
        private string _currentTableName;
        private DataTable _currentTable;

        /// <summary>
        /// 默认自动参数标识（与 WeldParamControl.AutoParam.DefaultIdentity 保持一致，避免反向引用业务类型）
        /// </summary>
        public const string DefaultIdentity = "DefaultParam";

        /// <summary>
        /// 当前选中的表名
        /// </summary>
        public string CurrentTableName => _currentTableName;

        /// <summary>
        /// 当前选中的数据表
        /// </summary>
        public DataTable CurrentTable => _currentTable;

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
                RobotSpeedJson = Database.GetTableColumnValue(_currentTableName, "焊接速度")
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
            AddParamValues(
                profile.SeamWidthJson,
                profile.FeedSpeedJson,
                profile.LaserPowerJson,
                profile.RobotSpeedJson);
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
            UpdateParamValues(profile);
        }

        /// <summary>
        /// 添加参数值记录
        /// </summary>
        /// <param name="width">焊缝宽度JSON</param>
        /// <param name="feedSpeed">送丝速度JSON</param>
        /// <param name="laserPower">激光功率JSON</param>
        /// <param name="robotSpeed">焊接速度JSON</param>
        public void AddParamValues(string width, string feedSpeed, string laserPower, string robotSpeed)
        {
            var values = BuildValues(
                width ?? string.Empty,
                feedSpeed ?? string.Empty,
                laserPower ?? string.Empty,
                robotSpeed ?? string.Empty
            );

            Database.Insert(_currentTableName, _paramColumns, values);
        }

        /// <summary>
        /// 更新当前表中的参数值
        /// </summary>
        /// <param name="profile">分段参数数据</param>
        private void UpdateParamValues(WeldAutoParamProfile profile)
        {
            var columns = new[] { "焊缝宽度", "送丝速度", "激光功率", "焊接速度" };
            var values = new[]
            {
                profile.SeamWidthJson ?? string.Empty,
                profile.FeedSpeedJson ?? string.Empty,
                profile.LaserPowerJson ?? string.Empty,
                profile.RobotSpeedJson ?? string.Empty
            };

            Database.Update(_currentTableName, columns, values);
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
    }

    /// <summary>
    /// 焊接自动参数分段数据中性传输对象
    /// 用于在 SQLiteDatabase 层与 WeldParamControl 层之间传递焊缝宽度/送丝速度/激光功率/焊接速度的 JSON 序列化值，
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
        /// 是否有任意一项非空
        /// </summary>
        public bool HasAnyValue
        {
            get
            {
                return !string.IsNullOrEmpty(SeamWidthJson)
                    || !string.IsNullOrEmpty(FeedSpeedJson)
                    || !string.IsNullOrEmpty(LaserPowerJson)
                    || !string.IsNullOrEmpty(RobotSpeedJson);
            }
        }
    }
}
