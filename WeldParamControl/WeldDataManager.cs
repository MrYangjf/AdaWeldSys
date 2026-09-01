using AdaWeldSystem.FileOperate;
using AdaWeldSystem.SqlLiteDatabase;
using Newtonsoft.Json;
using System;
using System.IO;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>
    /// 焊接数据管理器（单例）
    /// 集中管理自动调整参数：运行时 AutoParam、分段数据表 AutoParamManager、
    /// 配置映射 ConfigManager、当前配置标识 CurrentAutoParamId。
    /// CurrentAutoParamId 通过 INI 文件持久化。
    /// </summary>
    public class WeldDataManager
    {
        #region 单例

        private static readonly Lazy<WeldDataManager> _lazyInstance = new Lazy<WeldDataManager>(() => new WeldDataManager());

        /// <summary>
        /// 单例实例
        /// </summary>
        public static WeldDataManager Instance
        {
            get { return _lazyInstance.Value; }
        }

        #endregion

        #region 常量

        private const string IniSection = "WeldData";
        private const string IniKeyCurrentId = "CurrentAutoParamId";
        private const string IniFileName = "WeldData.ini";

        #endregion

        #region 字段

        private readonly INIFile _iniFile;

        /// <summary>
        /// 当前自动调整参数标识
        /// </summary>
        private string _currentAutoParamId;

        #endregion

        #region 属性

        /// <summary>
        /// 自动调整参数运行时对象
        /// </summary>
        public AutoParam mAutoParam { get; private set; }

        /// <summary>
        /// 自动调整参数值管理器（按 identityInfo 管理分段数据表）
        /// </summary>
        public AutoParamManager mAutoParamManage { get; private set; }

        /// <summary>
        /// 自动调整参数配置管理器（管理 AutoParam 基础配置与 JSON 映射）
        /// </summary>
        public ConfigManager mConfigManager { get; private set; }

        /// <summary>
        /// 当前自动调整参数标识
        /// </summary>
        public string CurrentAutoParamId
        {
            get { return _currentAutoParamId; }
            private set
            {
                _currentAutoParamId = value;
                SaveCurrentIdToIni();
            }
        }

        #endregion

        #region 构造函数

        private WeldDataManager()
        {
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", IniFileName);
            _iniFile = new INIFile(iniPath);

            mAutoParamManage = new AutoParamManager();
            mConfigManager = new ConfigManager();

            // 读取或初始化当前配置标识
            _currentAutoParamId = _iniFile.ReadString(IniSection, IniKeyCurrentId, AutoParam.DefaultIdentity);
            if (string.IsNullOrWhiteSpace(_currentAutoParamId))
            {
                _currentAutoParamId = AutoParam.DefaultIdentity;
            }

            // 确保默认配置存在（ConfigManager 与 AutoParamManager 都要有）
            EnsureDefaultConfigExists();

            // 加载当前配置
            LoadCurrentConfig();
        }

        #endregion

        #region 初始化与默认配置保护

        /// <summary>
        /// 确保默认配置在 ConfigManager 和 AutoParamManager 中都存在
        /// </summary>
        private void EnsureDefaultConfigExists()
        {
            // 1. ConfigManager 中必须有 Default 配置
            if (!mConfigManager.IsConfigExist(AutoParam.DefaultIdentity))
            {
                AutoParam defaultParam = AutoParam.CreateDefault();
                string json = JsonConvert.SerializeObject(defaultParam);
                mConfigManager.AddConfig(defaultParam, json);
            }

            // 2. AutoParamManager 中必须有 Default 分段数据表
            if (!mAutoParamManage.IsTableExist(AutoParam.DefaultIdentity))
            {
                AutoParam defaultParam = AutoParam.CreateDefault();
                mAutoParamManage.SelectTable(defaultParam);
            }
        }

        /// <summary>
        /// 加载当前配置标识对应的 AutoParam 并设置到 WeldProcess
        /// </summary>
        private void LoadCurrentConfig()
        {
            string json = mConfigManager.GetConfigValue(_currentAutoParamId);
            mAutoParam = JsonConvert.DeserializeObject<AutoParam>(json);

            if (mAutoParam == null)
            {
                // 当前配置在 ConfigManager 中不存在，回退到 Default
                _currentAutoParamId = AutoParam.DefaultIdentity;
                SaveCurrentIdToIni();
                json = mConfigManager.GetConfigValue(_currentAutoParamId);
                mAutoParam = JsonConvert.DeserializeObject<AutoParam>(json);
            }

            if (mAutoParam != null)
            {
                mAutoParamManage.SelectTable(mAutoParam);
                WeldProcess.Instance.CurrentAutoParam = mAutoParam;
            }
        }

        /// <summary>
        /// 将当前配置标识保存到 INI
        /// </summary>
        private void SaveCurrentIdToIni()
        {
            if (_iniFile != null)
            {
                _iniFile.WriteString(IniSection, IniKeyCurrentId, _currentAutoParamId ?? AutoParam.DefaultIdentity);
                _iniFile.SaveToFile();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 切换到指定配置
        /// </summary>
        /// <param name="configId">配置标识</param>
        /// <exception cref="ArgumentException">配置标识为空</exception>
        /// <exception cref="InvalidOperationException">配置不存在</exception>
        public void SwitchConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("配置标识不能为空", nameof(configId));
            }

            if (!mConfigManager.IsConfigExist(configId))
            {
                throw new InvalidOperationException(string.Format("配置不存在 {0}", configId));
            }

            CurrentAutoParamId = configId;
            LoadCurrentConfig();
        }

        /// <summary>
        /// 保存当前 AutoParam 到 ConfigManager
        /// </summary>
        public void SaveCurrentAutoParam()
        {
            if (mAutoParam == null)
            {
                return;
            }

            string json = JsonConvert.SerializeObject(mAutoParam);
            if (mConfigManager.IsConfigExist(mAutoParam.identityInfo))
            {
                mConfigManager.UpdateConfig(mAutoParam.identityInfo, mAutoParam, json);
            }
            else
            {
                mConfigManager.AddConfig(mAutoParam, json);
            }
        }

        /// <summary>
        /// 添加新配置并切换为当前配置
        /// </summary>
        /// <param name="configId">新配置标识</param>
        /// <returns>创建成功的 AutoParam</returns>
        public AutoParam AddConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("配置标识不能为空", nameof(configId));
            }

            if (mConfigManager.IsConfigExist(configId))
            {
                throw new InvalidOperationException(string.Format("配置已存在 {0}", configId));
            }

            AutoParam newParam = new AutoParam(configId, DateTime.Now);
            string json = JsonConvert.SerializeObject(newParam);
            mConfigManager.AddConfig(newParam, json);
            mAutoParamManage.SelectTable(newParam);

            CurrentAutoParamId = configId;
            mAutoParam = newParam;
            WeldProcess.Instance.CurrentAutoParam = newParam;

            return newParam;
        }

        /// <summary>
        /// 删除指定配置
        /// </summary>
        /// <param name="configId">配置标识</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                return false;
            }

            // 默认配置不允许删除
            if (configId == AutoParam.DefaultIdentity)
            {
                return false;
            }

            // 当前正在使用的配置不允许删除
            if (configId == _currentAutoParamId)
            {
                return false;
            }

            mAutoParamManage.DeleteTable(configId);
            return mConfigManager.DeleteConfig(configId);
        }

        /// <summary>
        /// 从 ConfigManager 重新加载当前配置，并重新生成分段参数值
        /// 用于在 ParamConfigPage 修改基础参数后刷新对应分段数据
        /// </summary>
        public void ReloadCurrentParamValues()
        {
            if (mAutoParam == null)
            {
                return;
            }

            string json = mConfigManager.GetConfigValue(_currentAutoParamId);
            AutoParam refreshed = JsonConvert.DeserializeObject<AutoParam>(json);
            if (refreshed == null)
            {
                return;
            }

            mAutoParam = refreshed;
            WeldProcess.Instance.GetListValue(mAutoParam);
            mAutoParamManage.DeleteTable(mAutoParam.identityInfo);
            mAutoParamManage.SelectTable(mAutoParam);
            WeldProcess.Instance.CurrentAutoParam = mAutoParam;
        }

        #endregion
    }
}
