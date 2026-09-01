using System;
using System.Collections.Generic;
using System.Data;
using AdaWeldSystem.WeldParamControl;
using Newtonsoft.Json;

namespace AdaWeldSystem.SqlLiteDatabase
{
    /// <summary>
    /// 自动参数值管理器
    /// 负责焊接自动参数（焊缝宽度、送丝速度、激光功率、焊接速度）的JSON序列化存储和读取
    /// 每个AutoParam配置对应一张独立的数据表，以identityInfo作为表名
    /// </summary>
    public class AutoParamManager : DatabaseManagerBase
    {
        private readonly string[] _paramColumns = { "焊缝宽度", "送丝速度", "激光功率", "焊接速度" };
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
        /// 构造函数，初始化自动参数值数据库
        /// </summary>
        public AutoParamManager() : base("AutoParamValue.db")
        {
            _currentTableName = AutoParam.DefaultIdentity;
        }

        /// <summary>
        /// 创建参数值表（如果不存在）
        /// </summary>
        /// <param name="tableName">表名（通常为AutoParam的identityInfo）</param>
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
        /// 删除指定AutoParam对应的参数值表
        /// </summary>
        /// <param name="autoParam">自动参数对象</param>
        public void DeleteTable(AutoParam autoParam)
        {
            if (autoParam == null)
            {
                return;
            }

            DeleteTable(autoParam.identityInfo);
        }

        /// <summary>
        /// 选择并加载指定AutoParam的参数值
        /// 如果对应表不存在，则创建新表并从WeldProcess获取初始值写入
        /// </summary>
        /// <param name="autoParam">自动参数对象</param>
        public void SelectTable(AutoParam autoParam)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            _currentTableName = autoParam.identityInfo;

            if (Database.IsExistTable(_currentTableName))
            {
                // 从数据库加载参数值到 WeldProcess
                LoadParamValuesFromDatabase();
            }
            else
            {
                // 表不存在，创建新表并从 WeldProcess 获取初始值
                CreateTableIfNotExist(_currentTableName);
                SaveParamValuesFromWeldProcess(autoParam);
            }
        }

        /// <summary>
        /// 从数据库加载参数值到 WeldProcess 单例
        /// </summary>
        private void LoadParamValuesFromDatabase()
        {
            string jsonStr;

            jsonStr = Database.GetTableColumnValue(_currentTableName, "焊缝宽度");
            WeldProcess.Instance.SeamWidthList = JsonConvert.DeserializeObject<List<double>>(jsonStr);

            jsonStr = Database.GetTableColumnValue(_currentTableName, "送丝速度");
            WeldProcess.Instance.FeedSpeedDic = JsonConvert.DeserializeObject<Dictionary<double, double>>(jsonStr);

            jsonStr = Database.GetTableColumnValue(_currentTableName, "激光功率");
            WeldProcess.Instance.LaserPowerDic = JsonConvert.DeserializeObject<Dictionary<double, double>>(jsonStr);

            jsonStr = Database.GetTableColumnValue(_currentTableName, "焊接速度");
            WeldProcess.Instance.RobotSpeedDic = JsonConvert.DeserializeObject<Dictionary<double, double>>(jsonStr);
        }

        /// <summary>
        /// 从 WeldProcess 单例获取参数值并保存到数据库
        /// </summary>
        /// <param name="autoParam">自动参数对象（用于初始化WeldProcess参数）</param>
        private void SaveParamValuesFromWeldProcess(AutoParam autoParam)
        {
            WeldProcess.Instance.GetListValue(autoParam);

            string width = JsonConvert.SerializeObject(WeldProcess.Instance.SeamWidthList);
            string feedSpeed = JsonConvert.SerializeObject(WeldProcess.Instance.FeedSpeedDic);
            string laserPower = JsonConvert.SerializeObject(WeldProcess.Instance.LaserPowerDic);
            string robotSpeed = JsonConvert.SerializeObject(WeldProcess.Instance.RobotSpeedDic);

            AddParamValues(width, feedSpeed, laserPower, robotSpeed);
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
        /// 从 WeldProcess 单例更新当前表的参数值
        /// </summary>
        public void UpdateParamValues()
        {
            string width = JsonConvert.SerializeObject(WeldProcess.Instance.SeamWidthList);
            string feedSpeed = JsonConvert.SerializeObject(WeldProcess.Instance.FeedSpeedDic);
            string laserPower = JsonConvert.SerializeObject(WeldProcess.Instance.LaserPowerDic);
            string robotSpeed = JsonConvert.SerializeObject(WeldProcess.Instance.RobotSpeedDic);

            var columns = new[] { "焊缝宽度", "送丝速度", "激光功率", "焊接速度" };
            var values = new[] { width, feedSpeed, laserPower, robotSpeed };

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
}
