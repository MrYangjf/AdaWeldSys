using System;
using System.Collections.Generic;
using System.IO;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.WeldParamControl.Algorithms;
using Newtonsoft.Json;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.SQLiteDBControl;
using AdaWeldSystem.SQLiteDBControl.Helpers;

namespace AdaWeldSystem.WeldParamControl
{
    /// <summary>
    /// 焊接参数管理器（单例）：WeldParamControl 模块对外唯一访问入口。
    /// </summary>
    /// <remarks>
    /// 集中持有焊接参数配置（<see cref="AutoParam"/>）、模块运行配置
    /// （<see cref="WeldAlgorithmsConfig"/> 焊接算法配置：线激光引导 + 参数调整，两节共用一个文件）
    /// 与全部算法实现（<see cref="WeldParamLookupAlgorithm"/> 参数查表、
    ///  <see cref="WeldParamAdjustmentAlgorithm"/> 焊缝宽度驱动的参数调整、
    ///  <see cref="SeamTrackingAlgorithm"/> 线激光焊缝几何轨迹跟踪），
    /// 同时管理配置持久化（INI、ConfigHelper、AutoParamHelper、Config/XML/WeldParamControl.xml）。
    /// 流程编排在 WeldParamControlWorkflow 中进行，本类不承载流程。
    /// 算法只计算不落盘，记录请求事件在此透传，由记录模块订阅后异步落盘。
    /// </remarks>
    public class WeldParamManager
    {
        #region 单例

        private static readonly Lazy<WeldParamManager> _lazyInstance =
            new Lazy<WeldParamManager>(() => new WeldParamManager());

        /// <summary>单例实例（线程安全）。</summary>
        public static WeldParamManager Instance { get { return _lazyInstance.Value; } }

        #endregion

        #region 常量

        private const string IniSection = "WeldData";
        private const string IniKeyCurrentId = "CurrentAutoParamId";
        private const string IniFileName = "WeldData.ini";

        /// <summary>模块配置文件文件名（不含扩展名），落在 Config/XML/ 下；线激光引导与参数调整分节共存。</summary>
        private const string ModuleConfigFileName = "WeldParamControl";

        #endregion

        #region 私有变量

        /// <summary>查表算法实现（非单例，由本管理器持有）。</summary>
        private readonly WeldParamLookupAlgorithm _lookupAlgorithm;

        /// <summary>参数调整算法实现（非单例，由本管理器持有）。</summary>
        private readonly WeldParamAdjustmentAlgorithm _paramAdjustmentAlgorithm;

        /// <summary>焊缝几何轨迹跟踪算法实现（非单例，由本管理器持有）。</summary>
        private readonly SeamTrackingAlgorithm _seamTrackingAlgorithm;

        /// <summary>机器人 X 坐标读写锁。</summary>
        private readonly object _robotXLock = new object();

        /// <summary>INI 文件读写器。</summary>
        private readonly INIFile _iniFile;

        /// <summary>模块配置读写器（Config/XML/WeldParamControl.xml，含 SEAM_GUIDE / WELD_PARAM_ADJUST 两节），本管理器独占。</summary>
        private readonly XdocumentReaderWriter _configFile;

        /// <summary>焊接算法配置实例（两节共用一份，本管理器独占，同时供参数调整与轨迹跟踪算法只读消费）。</summary>
        private WeldAlgorithmsConfig _algorithmsConfig;

        /// <summary>当前自动调整参数标识。</summary>
        private string _currentAutoParamId;

        #endregion

        #region 公共变量

        /// <summary>机器人焊接数据。</summary>
        public RobotWeldData RobotWeldData { get; private set; }

        /// <summary>当前自动参数配置。</summary>
        public AutoParam CurrentAutoParam { get; private set; }

        /// <summary>自动调整参数值 Helper（按 identityInfo 管理分段数据表）。</summary>
        public AutoParamHelper AutoParamHelper { get; private set; }

        /// <summary>自动调整参数配置 Helper（管理 AutoParam 基础配置与 JSON 映射）。</summary>
        public ConfigHelper ConfigHelper { get; private set; }

        /// <summary>当前自动调整参数标识。</summary>
        public string CurrentAutoParamId
        {
            get { return _currentAutoParamId; }
            private set
            {
                _currentAutoParamId = value;
                SaveCurrentIdToIni();
            }
        }

        /// <summary>是否按点数分段计算（透传参数调整算法的运行配置）。</summary>
        public bool IsCountType
        {
            get { return _paramAdjustmentAlgorithm.IsCountType; }
            set { _paramAdjustmentAlgorithm.IsCountType = value; }
        }

        /// <summary>每段点数（透传参数调整算法的运行配置）。</summary>
        public int CountNum
        {
            get { return _paramAdjustmentAlgorithm.CountNum; }
            set { _paramAdjustmentAlgorithm.CountNum = value; }
        }

        /// <summary>当前机器人 X 坐标（真实坐标，来自 RobotWeldData.PosX）。</summary>
        public double CurrentRobotX
        {
            get
            {
                lock (_robotXLock)
                {
                    return RobotWeldData != null ? RobotWeldData.PosX : 0.0;
                }
            }
            set
            {
                lock (_robotXLock)
                {
                    if (RobotWeldData != null)
                    {
                        RobotWeldData.PosX = value;
                    }
                }
            }
        }

        /// <summary>最大允许连续失败次数（透传参数调整算法的运行配置）。</summary>
        public int MaxConsecutiveFailures
        {
            get { return _paramAdjustmentAlgorithm.MaxConsecutiveFailures; }
            set { _paramAdjustmentAlgorithm.MaxConsecutiveFailures = value; }
        }

        /// <summary>连续失败次数。</summary>
        public int ConsecutiveFailureCount { get { return _paramAdjustmentAlgorithm.ConsecutiveFailureCount; } }

        /// <summary>算法执行总次数。</summary>
        public int TotalExecuteCount { get { return _paramAdjustmentAlgorithm.TotalExecuteCount; } }

        /// <summary>送丝速度输出（最近一次调整结果）。</summary>
        public double FeedSpeedOutput { get { return _paramAdjustmentAlgorithm.FeedSpeedOutput; } }

        /// <summary>焊接速度输出（最近一次调整结果）。</summary>
        public double RobotSpeedOutput { get { return _paramAdjustmentAlgorithm.RobotSpeedOutput; } }

        /// <summary>激光功率输出（最近一次调整结果）。</summary>
        public double LaserPowerOutput { get { return _paramAdjustmentAlgorithm.LaserPowerOutput; } }

        /// <summary>本轮采用的焊缝宽度。</summary>
        public double CurrentSeamWidth { get { return _paramAdjustmentAlgorithm.CurrentSeamWidth; } }

        /// <summary>焊缝目标宽度列表（参数查找表的键）。</summary>
        public List<double> SeamWidthList
        {
            get { return _lookupAlgorithm.SeamWidthList; }
            set { _lookupAlgorithm.SeamWidthList = value; }
        }

        /// <summary>激光功率查找表（焊缝宽度 -> 激光功率）。</summary>
        public Dictionary<double, double> LaserPowerDic
        {
            get { return _lookupAlgorithm.LaserPowerDic; }
            set { _lookupAlgorithm.LaserPowerDic = value; }
        }

        /// <summary>送丝速度查找表（焊缝宽度 -> 送丝速度）。</summary>
        public Dictionary<double, double> FeedSpeedDic
        {
            get { return _lookupAlgorithm.FeedSpeedDic; }
            set { _lookupAlgorithm.FeedSpeedDic = value; }
        }

        /// <summary>焊接速度查找表（焊缝宽度 -> 焊接速度）。</summary>
        public Dictionary<double, double> RobotSpeedDic
        {
            get { return _lookupAlgorithm.RobotSpeedDic; }
            set { _lookupAlgorithm.RobotSpeedDic = value; }
        }

        /// <summary>轮廓数据（基于 RobotX 的轮廓坐标）。</summary>
        public ChartVar ContourChart { get { return _paramAdjustmentAlgorithm.ContourChart; } }

        /// <summary>激光功率折线数据（基于 RobotX）。</summary>
        public ChartVar LaserPowerChart { get { return _paramAdjustmentAlgorithm.LaserPowerChart; } }

        /// <summary>送丝速度折线数据（基于 RobotX）。</summary>
        public ChartVar FeedSpeedChart { get { return _paramAdjustmentAlgorithm.FeedSpeedChart; } }

        /// <summary>焊缝宽度折线数据（基于 RobotX）。</summary>
        public ChartVar SeamWidthChart { get { return _paramAdjustmentAlgorithm.SeamWidthChart; } }

        /// <summary>机器人移动速度折线数据（基于 RobotX）。</summary>
        public ChartVar RobotSpeedChart { get { return _paramAdjustmentAlgorithm.RobotSpeedChart; } }

        /// <summary>焊接算法配置（运行期只读引用；线激光引导 + 参数调整两节合一，本管理器独占）。</summary>
        public WeldAlgorithmsConfig AlgorithmsConfig { get { return _algorithmsConfig; } }

        /// <summary>焊接工艺调整完成事件（透传参数调整算法）。</summary>
        public event EventHandler<WeldProcessCompletedEventArgs> WeldProcessCompleted
        {
            add { _paramAdjustmentAlgorithm.WeldProcessCompleted += value; }
            remove { _paramAdjustmentAlgorithm.WeldProcessCompleted -= value; }
        }

        /// <summary>过程帧记录请求事件（透传参数调整算法，落盘由记录模块异步执行）。</summary>
        public event EventHandler<WeldFrameRecordEventArgs> FrameRecordRequested
        {
            add { _paramAdjustmentAlgorithm.FrameRecordRequested += value; }
            remove { _paramAdjustmentAlgorithm.FrameRecordRequested -= value; }
        }

        /// <summary>调整动作记录请求事件（透传参数调整算法，落盘由记录模块异步执行）。</summary>
        public event EventHandler<WeldAdjustmentRecordEventArgs> AdjustmentRecordRequested
        {
            add { _paramAdjustmentAlgorithm.AdjustmentRecordRequested += value; }
            remove { _paramAdjustmentAlgorithm.AdjustmentRecordRequested -= value; }
        }

        /// <summary>记录中断请求事件（透传参数调整算法，由记录模块处置临时记录）。</summary>
        public event EventHandler AbortRecordRequested
        {
            add { _paramAdjustmentAlgorithm.AbortRecordRequested += value; }
            remove { _paramAdjustmentAlgorithm.AbortRecordRequested -= value; }
        }

        #endregion

        #region 构造函数

        private WeldParamManager()
        {
            RobotWeldData = new RobotWeldData();
            _lookupAlgorithm = new WeldParamLookupAlgorithm();
            _paramAdjustmentAlgorithm = new WeldParamAdjustmentAlgorithm(_lookupAlgorithm, RobotWeldData);
            _seamTrackingAlgorithm = new SeamTrackingAlgorithm();

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", IniFileName);
            _iniFile = new INIFile(iniPath);
            _configFile = new XdocumentReaderWriter(ModuleConfigFileName);

            AutoParamHelper = SQLiteDatabaseManager.Instance.AutoParamHelper;
            ConfigHelper = SQLiteDatabaseManager.Instance.ConfigHelper;

            _currentAutoParamId = _iniFile.ReadString(IniSection, IniKeyCurrentId, AutoParam.DefaultIdentity);
            if (string.IsNullOrWhiteSpace(_currentAutoParamId))
            {
                _currentAutoParamId = AutoParam.DefaultIdentity;
            }

            EnsureDefaultConfigExists();
            LoadCurrentConfig();
            LoadAlgorithmsConfig();
        }

        #endregion

        #region 私有函数

        /// <summary>确保默认配置在 ConfigHelper 和 AutoParamHelper 中都存在。</summary>
        private void EnsureDefaultConfigExists()
        {
            if (!ConfigHelper.IsConfigExist(AutoParam.DefaultIdentity))
            {
                AutoParam defaultParam = AutoParam.CreateDefault();
                string json = JsonConvert.SerializeObject(defaultParam);
                ConfigHelper.AddConfig(
                    defaultParam.identityInfo,
                    defaultParam.WeldType.ToString(),
                    defaultParam.WireType,
                    defaultParam.PlateType,
                    defaultParam.CreatTime.ToString("yyyyMMddHHmmss"),
                    json);
            }

            if (!AutoParamHelper.IsTableExist(AutoParam.DefaultIdentity))
            {
                AutoParamHelper.SelectTable(AutoParam.DefaultIdentity);
            }
        }

        /// <summary>将当前配置标识保存到 INI。</summary>
        private void SaveCurrentIdToIni()
        {
            if (_iniFile != null)
            {
                _iniFile.WriteString(IniSection, IniKeyCurrentId, _currentAutoParamId ?? AutoParam.DefaultIdentity);
                _iniFile.SaveToFile();
            }
        }

        /// <summary>加载当前配置标识对应的 AutoParam 并重建查找表。</summary>
        private void LoadCurrentConfig()
        {
            string json = ConfigHelper.GetConfigValue(_currentAutoParamId);
            CurrentAutoParam = JsonConvert.DeserializeObject<AutoParam>(json);

            if (CurrentAutoParam == null)
            {
                _currentAutoParamId = AutoParam.DefaultIdentity;
                SaveCurrentIdToIni();
                json = ConfigHelper.GetConfigValue(_currentAutoParamId);
                CurrentAutoParam = JsonConvert.DeserializeObject<AutoParam>(json);
            }

            if (CurrentAutoParam != null)
            {
                LoadProfileIntoLookupTable(CurrentAutoParam.identityInfo);
                _lookupAlgorithm.LoadConfig(CurrentAutoParam);
                _paramAdjustmentAlgorithm.Reset();
            }
        }

        /// <summary>从分段参数构建持久化数据对象。</summary>
        /// <returns>分段参数数据对象</returns>
        private WeldAutoParamProfile BuildProfileFromLookupTable()
        {
            return new WeldAutoParamProfile
            {
                SeamWidthJson = JsonConvert.SerializeObject(SeamWidthList),
                FeedSpeedJson = JsonConvert.SerializeObject(FeedSpeedDic),
                LaserPowerJson = JsonConvert.SerializeObject(LaserPowerDic),
                RobotSpeedJson = JsonConvert.SerializeObject(RobotSpeedDic)
            };
        }

        #endregion

        #region 配置管理公共方法

        /// <summary>切换到指定配置。</summary>
        /// <param name="configId">配置标识</param>
        /// <exception cref="ArgumentException">配置标识为空</exception>
        /// <exception cref="InvalidOperationException">配置不存在</exception>
        public void SwitchConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("配置标识不能为空", nameof(configId));
            }

            if (!ConfigHelper.IsConfigExist(configId))
            {
                throw new InvalidOperationException(string.Format("配置不存在 {0}", configId));
            }

            CurrentAutoParamId = configId;
            LoadCurrentConfig();
        }

        /// <summary>保存当前 AutoParam 到 ConfigHelper。</summary>
        public void SaveCurrentAutoParam()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            string json = JsonConvert.SerializeObject(CurrentAutoParam);
            if (ConfigHelper.IsConfigExist(CurrentAutoParam.identityInfo))
            {
                ConfigHelper.UpdateConfig(
                    CurrentAutoParam.identityInfo,
                    CurrentAutoParam.WeldType.ToString(),
                    CurrentAutoParam.WireType,
                    CurrentAutoParam.PlateType,
                    json);
            }
            else
            {
                ConfigHelper.AddConfig(
                    CurrentAutoParam.identityInfo,
                    CurrentAutoParam.WeldType.ToString(),
                    CurrentAutoParam.WireType,
                    CurrentAutoParam.PlateType,
                    CurrentAutoParam.CreatTime.ToString("yyyyMMddHHmmss"),
                    json);
            }
        }

        /// <summary>保存当前分段参数值到数据库。</summary>
        public void SaveCurrentProfile()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            WeldAutoParamProfile profile = BuildProfileFromLookupTable();
            if (AutoParamHelper.IsTableExist(CurrentAutoParam.identityInfo))
            {
                AutoParamHelper.UpdateProfile(CurrentAutoParam.identityInfo, profile);
            }
            else
            {
                AutoParamHelper.SaveProfile(CurrentAutoParam.identityInfo, profile);
            }
        }

        /// <summary>添加新配置并切换为当前配置。</summary>
        /// <param name="configId">新配置标识</param>
        /// <returns>创建成功的 AutoParam</returns>
        public AutoParam AddConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("配置标识不能为空", nameof(configId));
            }

            if (ConfigHelper.IsConfigExist(configId))
            {
                throw new InvalidOperationException(string.Format("配置已存在 {0}", configId));
            }

            AutoParam newParam = new AutoParam(configId, DateTime.Now);
            string json = JsonConvert.SerializeObject(newParam);
            ConfigHelper.AddConfig(
                newParam.identityInfo,
                newParam.WeldType.ToString(),
                newParam.WireType,
                newParam.PlateType,
                newParam.CreatTime.ToString("yyyyMMddHHmmss"),
                json);
            AutoParamHelper.SelectTable(newParam.identityInfo);

            CurrentAutoParamId = configId;
            CurrentAutoParam = newParam;
            _lookupAlgorithm.LoadConfig(newParam);
            _paramAdjustmentAlgorithm.Reset();

            return newParam;
        }

        /// <summary>删除指定配置。</summary>
        /// <param name="configId">配置标识</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteConfig(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                return false;
            }

            if (configId == AutoParam.DefaultIdentity)
            {
                return false;
            }

            if (configId == _currentAutoParamId)
            {
                return false;
            }

            AutoParamHelper.DeleteTable(configId);
            return ConfigHelper.DeleteConfig(configId);
        }

        /// <summary>从 ConfigHelper 重新加载当前配置，并重新生成分段参数值。</summary>
        public void ReloadCurrentParamValues()
        {
            if (CurrentAutoParam == null)
            {
                return;
            }

            string json = ConfigHelper.GetConfigValue(_currentAutoParamId);
            AutoParam refreshed = JsonConvert.DeserializeObject<AutoParam>(json);
            if (refreshed == null)
            {
                return;
            }

            CurrentAutoParam = refreshed;
            _lookupAlgorithm.LoadConfig(CurrentAutoParam);
            AutoParamHelper.DeleteTable(CurrentAutoParam.identityInfo);
            SaveCurrentProfile();
            _paramAdjustmentAlgorithm.Reset();
        }

        /// <summary>从数据库加载分段参数并写入查找表。</summary>
        /// <param name="identityInfo">自动参数标识</param>
        public void LoadProfileIntoLookupTable(string identityInfo)
        {
            WeldAutoParamProfile profile = AutoParamHelper.LoadProfile(identityInfo);
            if (!profile.HasAnyValue)
            {
                return;
            }

            if (!string.IsNullOrEmpty(profile.SeamWidthJson))
            {
                SeamWidthList = JsonConvert.DeserializeObject<List<double>>(profile.SeamWidthJson);
            }
            if (!string.IsNullOrEmpty(profile.FeedSpeedJson))
            {
                FeedSpeedDic = JsonConvert.DeserializeObject<Dictionary<double, double>>(profile.FeedSpeedJson);
            }
            if (!string.IsNullOrEmpty(profile.LaserPowerJson))
            {
                LaserPowerDic = JsonConvert.DeserializeObject<Dictionary<double, double>>(profile.LaserPowerJson);
            }
            if (!string.IsNullOrEmpty(profile.RobotSpeedJson))
            {
                RobotSpeedDic = JsonConvert.DeserializeObject<Dictionary<double, double>>(profile.RobotSpeedJson);
            }
        }

        /// <summary>从自有配置文件载入焊接算法配置（SEAM_GUIDE + WELD_PARAM_ADJUST 两节）并分发给两个算法。</summary>
        /// <remarks>配置实例由本管理器独占持有，同时供参数调整与轨迹跟踪算法只读消费；外部只读，禁止在外部 new 实例回灌。</remarks>
        public void LoadAlgorithmsConfig()
        {
            WeldAlgorithmsConfig config = new WeldAlgorithmsConfig();
            config.LoadConfig(_configFile);
            _algorithmsConfig = config;
            _paramAdjustmentAlgorithm.LoadConfig(config);
            _seamTrackingAlgorithm.LoadConfig(config);
        }

        /// <summary>把当前焊接算法配置写回自有配置文件并落盘。</summary>
        /// <remarks>线激光标定或参数调整阈值改动后由界面或流程调用；写入后即时生效于两个算法。</remarks>
        public void SaveAlgorithmsConfig()
        {
            WeldAlgorithmsConfig config = _algorithmsConfig;
            if (config == null) return;
            config.SaveConfig(_configFile);
            _configFile.SaveXdocument();
        }

        #endregion

        #region 算法公共方法

        /// <summary>无参触发一次自动调整（使用最近一帧的焊缝特征）。</summary>
        /// <remarks>对外保持无参契约（ADR-009），配置与机器人坐标由本管理器内部取用；
        /// 分段方式与失败阈值取自 <see cref="AlgorithmsConfig"/>，算法只计算不落盘。</remarks>
        public void TriggerAdjustment()
        {
            if (CurrentAutoParam != null)
            {
                _paramAdjustmentAlgorithm.ExecuteAdjustment(CurrentAutoParam, CurrentRobotX);
            }
        }

        /// <summary>生成参数查找表（按焊缝宽度范围与灵敏度生成三张映射表）。</summary>
        /// <param name="autoParam">自动参数配置</param>
        public void BuildLookupTable(AutoParam autoParam)
        {
            _lookupAlgorithm.BuildLookupTable(autoParam);
        }

        /// <summary>按焊缝宽度在查找表中匹配最近一档工艺参数。</summary>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="laserPower">匹配到的激光功率</param>
        /// <param name="feedSpeed">匹配到的送丝速度</param>
        /// <param name="robotSpeed">匹配到的焊接速度</param>
        /// <returns>匹配成功返回 true</returns>
        public bool TryLookup(double seamWidth, out double laserPower, out double feedSpeed, out double robotSpeed)
        {
            return _lookupAlgorithm.TryLookup(seamWidth, out laserPower, out feedSpeed, out robotSpeed);
        }

        /// <summary>复位全部算法运行期状态（查找表与运行数据全部清空）。</summary>
        public void Reset()
        {
            _paramAdjustmentAlgorithm.Reset();
            _lookupAlgorithm.Reset();
            _seamTrackingAlgorithm.Reset();
        }

        /// <summary>喂入一帧线激光结果，返回当前应下发的 PVT 点集合。</summary>
        /// <param name="result">本帧焊缝识别结果</param>
        /// <param name="measuredFps">实测帧率（Hz），不可用传 0</param>
        /// <param name="axisCurrentPos">引导轴当前位置（毫米）</param>
        /// <returns>PVT 点数组</returns>
        public PVTPoint[] FeedSeamTracking(LineLaserCam.ILineLaser.LineLaserSeamResult result, double measuredFps, double axisCurrentPos)
        {
            return _seamTrackingAlgorithm.Feed(result, measuredFps, axisCurrentPos);
        }

        /// <summary>按最长输出间隔强制输出轨迹点。</summary>
        /// <returns>PVT 点数组</returns>
        public PVTPoint[] TickSeamTracking()
        {
            return _seamTrackingAlgorithm.Tick();
        }

        /// <summary>排空焊缝跟踪算法剩余轨迹点。</summary>
        /// <returns>PVT 点数组</returns>
        public PVTPoint[] DrainSeamTracking()
        {
            return _seamTrackingAlgorithm.Drain();
        }

        /// <summary>复位焊缝跟踪算法状态。</summary>
        public void ResetSeamTracking()
        {
            _seamTrackingAlgorithm.Reset();
        }

        /// <summary>焊缝跟踪算法是否已触发报警。</summary>
        public bool SeamTrackingHasFault { get { return _seamTrackingAlgorithm.HasFault; } }

        /// <summary>焊缝跟踪算法报警原因。</summary>
        public string SeamTrackingFaultReason { get { return _seamTrackingAlgorithm.FaultReason; } }

        /// <summary>最近一次焊缝跟踪偏差（毫米）。</summary>
        public double SeamTrackingLastDeviation { get { return _seamTrackingAlgorithm.LastDeviation; } }

        /// <summary>最近一次焊缝跟踪目标位置（毫米）。</summary>
        public double SeamTrackingLastTargetPos { get { return _seamTrackingAlgorithm.LastTargetPos; } }

        #endregion
    }
}
