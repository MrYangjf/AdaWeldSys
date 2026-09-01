using AdaWeldSystem.WeldParamControl;
using System;
using System.Data;
using System.Numerics;

namespace AdaWeldSystem.SqlLiteDatabase
{
    /// <summary>
    /// 配置管理器
    /// 负责系统配置参数的持久化存储和读取
    /// </summary>
    public class ConfigManager : DatabaseManagerBase
    {
        private readonly string[] _configColumns = { "类型", "焊丝材质", "焊接板材", "时间", "标识", "对象" };
        private readonly string _tableName = "ParamConfigTable";

        /// <summary>
        /// 构造函数，初始化配置数据库
        /// </summary>
        public ConfigManager() : base("Config.db")
        {
            InitializeConfigTable();
        }

        /// <summary>
        /// 初始化配置表（如果不存在）
        /// </summary>
        private void InitializeConfigTable()
        {
            if (!Database.IsExistTable(_tableName))
            {
                Database.CreateTable(_tableName, _configColumns);
            }
        }

        /// <summary>
        /// 添加配置项
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <param name="configValue">配置值</param>
        /// <returns>添加成功返回true，失败返回false</returns>
        public bool AddConfig(string configName, string configValue)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                throw new ArgumentException("配置名称不能为空", nameof(configName));
            }

            var values = BuildValues(configName, configValue ?? string.Empty);
            return Database.Insert(_tableName, _configColumns, values);
        }

        /// <summary>
        /// 添加配置项（重载，接受AutoParam对象和对应的Json字符串）
        /// </summary>
        /// <param name="autoParam"></param>
        /// <param name="JsonString"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool AddConfig(AutoParam autoParam, string JsonString)
        {
            if (string.IsNullOrWhiteSpace(JsonString))
            {
                throw new ArgumentException("配置名称不能为空", nameof(JsonString));
            }

            var values = ConvertJsonToValues(autoParam, JsonString);
            return Database.Insert(_tableName, _configColumns, values);
        }

        private string[] ConvertJsonToValues(AutoParam autoParam, string JsonString)
        {
            return new string[] { autoParam.WeldType.ToString(), autoParam.WireType, autoParam.PlateType, autoParam.CreatTime.ToString("yyyyMMddHHmmss"), autoParam.identityInfo, JsonString };
        }

        /// <summary>
        /// 获取配置值
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <returns>配置值字符串，不存在返回空字符串</returns>
        public string GetConfigValue(string configName)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                return string.Empty;
            }

            return Database.GetTableCellValue(_tableName, "对象", "标识", configName);
        }

        /// <summary>
        /// 更新配置值
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <param name="configValue">新配置值</param>
        /// <returns>更新成功返回true，失败返回false</returns>
        public bool UpdateConfig(string configName, string configValue)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                throw new ArgumentException("配置名称不能为空", nameof(configName));
            }

            var columns = new[] { "ConfigValue" };
            var values = new[] { configValue ?? string.Empty };
            return Database.Update(_tableName, columns, values, "ConfigName", configName);
        }

        public bool UpdateConfig(string info, AutoParam mAutoParam, string json)
        {
            //info = string.Format("'{0}'", info);
            //json = string.Format("'{0}'", json);
            //string wiretypr = string.Format("'{0}'", mAutoParam.WireType);
            //string platetypr = string.Format("'{0}'", mAutoParam.PlateType);
            //string seamtype = string.Format("'{0}'", mAutoParam.WeldType);

            string[] columns = { "类型", "焊丝材质", "焊接板材", "对象" };
            string[] values = { mAutoParam.WireType, mAutoParam.PlateType, mAutoParam.WeldType.ToString(), json };
            return Database.Update(_tableName, columns, values, "标识", info);
        }

        /// <summary>
        /// 检查配置项是否存在
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public bool IsConfigExist(string configName)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                return false;
            }

            return Database.HasTableValue(_tableName, "标识", configName);
        }

        /// <summary>
        /// 删除配置项
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <returns>删除成功返回true，失败返回false</returns>
        public bool DeleteConfig(string configName)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                return false;
            }

            return Database.DeleteTableByCondition(_tableName, "标识", configName);
        }

        /// <summary>
        /// 获取所有配置项
        /// </summary>
        /// <returns>配置数据表</returns>
        public DataTable GetAllConfigs()
        {
            return Database.Select(_tableName);
        }

        /// <summary>
        /// 保存或更新配置（存在则更新，不存在则添加）
        /// </summary>
        /// <param name="configName">配置名称</param>
        /// <param name="configValue">配置值</param>
        /// <returns>操作成功返回true，失败返回false</returns>
        public bool SaveConfig(string configName, string configValue)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                throw new ArgumentException("配置名称不能为空", nameof(configName));
            }

            if (IsConfigExist(configName))
            {
                return UpdateConfig(configName, configValue);
            }
            else
            {
                return AddConfig(configName, configValue);
            }
        }
    }
}
