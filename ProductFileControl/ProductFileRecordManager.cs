using AdaWeldSystem.FileOperate;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.WeldParamControl;
using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>
    /// 产品文件记录管理器（单例，模块唯一对外访问口）
    /// 四职责（ADR-042）：① 管理全部记录类实例（焊缝追踪 / 焊接参数调整 / 焊接结果）
    /// ② 管理记录数据（记录文件存储独占持有）③ 持久化配置（Record.ini：目录 + 混淆开关）
    /// ④ 对外唯一访问口 + 订阅 WeldParamManager 记录请求事件透传给记录类（ADR-043 订阅方闭环）。
    /// </summary>
    public sealed class ProductFileRecordManager : IDisposable
    {
        private const string Tag = "ProductFileRecordManager";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region 单例实现

        private static readonly Lazy<ProductFileRecordManager> _instance =
            new Lazy<ProductFileRecordManager>(() => new ProductFileRecordManager());

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ProductFileRecordManager Instance
        {
            get { return _instance.Value; }
        }

        #endregion

        #region 字段

        private readonly string _iniFilePath = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\INI\\Record.ini";
        private INIFile _inifile;
        private RecordFileStore _store;
        private SeamTrackRecord _trackRecord;
        private WeldParamAdjustRecord _adjustRecord;
        private WeldResultRecord _resultRecord;
        private string _recordDirectory = string.Empty;
        private string _tempDirectory = string.Empty;
        private bool _useObfuscation = false;
        private bool _disposed = false;

        /// <summary>记录请求事件订阅句柄（Dispose 退订用）</summary>
        private EventHandler<WeldFrameRecordEventArgs> _frameHandler;
        private EventHandler<WeldAdjustmentRecordEventArgs> _adjustHandler;
        private EventHandler _abortHandler;

        #endregion

        #region 属性

        /// <summary>
        /// 是否正在记录（焊缝追踪会话进行中）
        /// </summary>
        public bool IsRecording
        {
            get { return _trackRecord != null && _trackRecord.IsRecording; }
        }

        /// <summary>
        /// 当前帧索引
        /// </summary>
        public int CurrentFrameIndex
        {
            get { return _store != null ? _store.CurrentFrameIndex : 0; }
        }

        /// <summary>
        /// 已缓存的帧数量
        /// </summary>
        public int CachedFrameCount
        {
            get { return _store != null ? _store.CachedFrameCount : 0; }
        }

        /// <summary>
        /// 记录目录
        /// </summary>
        public string RecordDirectory
        {
            get { return _recordDirectory; }
            set
            {
                _recordDirectory = value;
                if (_store != null)
                {
                    _store.RecordDirectory = value;
                    _store.EnsureDirectoriesExist();
                }
            }
        }

        /// <summary>
        /// 临时文件目录
        /// </summary>
        public string TempDirectory
        {
            get { return _tempDirectory; }
            set
            {
                _tempDirectory = value;
                if (_store != null)
                {
                    _store.TempDirectory = value;
                    _store.EnsureDirectoriesExist();
                }
            }
        }

        /// <summary>
        /// 路径回退提示信息（配置路径为空时回退默认路径的说明，供 UI 层提示；无回退则为空）
        /// </summary>
        public string PathFallbackMessage { get; private set; }

        /// <summary>
        /// 是否使用混淆（pDat文件用TXT打开为乱码）
        /// </summary>
        public bool UseObfuscation
        {
            get { return _useObfuscation; }
            set
            {
                _useObfuscation = value;
                if (_store != null)
                {
                    _store.UseObfuscation = value;
                }
            }
        }

        /// <summary>
        /// 记录配置文件（INI），供配置页直接写入键值
        /// </summary>
        public INIFile ConfigFile
        {
            get
            {
                if (_inifile == null)
                {
                    _inifile = new INIFile(_iniFilePath);
                }
                return _inifile;
            }
        }

        #endregion

        #region 构造函数

        private ProductFileRecordManager()
        {
            LoadConfigFromIni();

            _store = new RecordFileStore(_recordDirectory, _tempDirectory, _useObfuscation);

            // 记录类由 Manager 构造创建并持有（实现类非单例，ADR-042）
            _trackRecord = new SeamTrackRecord(_store);
            _adjustRecord = new WeldParamAdjustRecord(_store);
            _resultRecord = new WeldResultRecord();

            // 订阅 WeldParamManager 记录请求事件（ADR-043：订阅方只入队，落盘在后台）
            _frameHandler = OnFrameRecordRequested;
            _adjustHandler = OnAdjustmentRecordRequested;
            _abortHandler = OnAbortRecordRequested;
            WeldParamManager.Instance.FrameRecordRequested += _frameHandler;
            WeldParamManager.Instance.AdjustmentRecordRequested += _adjustHandler;
            WeldParamManager.Instance.AbortRecordRequested += _abortHandler;

            Log("产品文件记录管理器已就绪");
        }

        #endregion

        #region 记录请求事件处理（ADR-043 订阅方）

        /// <summary>
        /// 每帧过程快照记录请求 → 焊缝追踪记录类
        /// </summary>
        private void OnFrameRecordRequested(object sender, WeldFrameRecordEventArgs e)
        {
            _trackRecord.RecordWeldFrame(new SeamTrackItem
            {
                RobotX = e.RobotX,
                RobotY = e.RobotY,
                RobotZ = e.RobotZ,
                RobotSpeed = e.RobotSpeed,
                FeedSpeed = e.FeedSpeed,
                LaserPower = e.LaserPower,
                WeldWidth = e.WeldWidth,
                HasWeldSnapshot = true
            });
        }

        /// <summary>
        /// 调整动作记录请求 → 焊接参数调整记录类
        /// </summary>
        private void OnAdjustmentRecordRequested(object sender, WeldAdjustmentRecordEventArgs e)
        {
            _adjustRecord.RecordAdjustment(new WeldParamAdjustItem
            {
                RobotX = e.RobotX,
                RobotY = e.RobotY,
                RobotZ = e.RobotZ,
                RobotSpeed = e.RobotSpeed,
                FeedSpeed = e.FeedSpeed,
                LaserPower = e.LaserPower,
                WeldWidth = e.WeldWidth,
                AdjustData = string.Format("FeedSpeed={0};LaserPower={1};WeldWidth={2}",
                    e.FeedSpeed, e.LaserPower, e.WeldWidth)
            });
        }

        /// <summary>
        /// 记录中止请求（连续失败超限）→ 异常中止焊缝追踪会话
        /// </summary>
        private void OnAbortRecordRequested(object sender, EventArgs e)
        {
            AbortRecording();
        }

        #endregion

        #region 记录控制

        /// <summary>
        /// 开始记录（焊缝追踪会话）
        /// </summary>
        public void StartRecording()
        {
            _trackRecord.Start();
        }

        /// <summary>
        /// 停止记录（正常结束，删除临时文件）
        /// </summary>
        public void StopRecording()
        {
            _trackRecord.Stop();
        }

        /// <summary>
        /// 异常中断记录（保留临时文件以便追溯）
        /// </summary>
        public void AbortRecording()
        {
            _trackRecord.Abort();
        }

        /// <summary>
        /// 添加一帧点云数据（线激光/监控相机路径）
        /// </summary>
        /// <param name="pointCloud">点云数据</param>
        public void AddFrame(PointCloudData pointCloud)
        {
            _trackRecord.AddPointCloud(pointCloud);
        }


        /// <summary>
        /// 记录一条焊接结果（异常/失败/成功 + 产品信息，DB 落库；入队即返回）
        /// </summary>
        /// <param name="sn">产品 SN</param>
        /// <param name="startTime">焊接开始时间</param>
        /// <param name="inspectionType">检测类型</param>
        /// <param name="result">焊接结果（成功/失败/异常）</param>
        /// <param name="checkInfo">检查信息（异常/失败原因明细）</param>
        public void RecordWeldResult(string sn, DateTime startTime, string inspectionType,
            string result, string checkInfo)
        {
            _resultRecord.RecordResult(new WeldResultItem
            {
                Sn = sn,
                StartTime = startTime,
                InspectionType = inspectionType,
                Result = result,
                CheckInfo = checkInfo
            });
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 从 INI 加载配置（空路径回退默认值并记录提示）
        /// </summary>
        public void LoadConfigFromIni()
        {
            _inifile = new INIFile(_iniFilePath);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string recordDir = _inifile.ReadString("Paths", "RecordDirectory", System.IO.Path.Combine(baseDir, "Records"));
            string tempDir = _inifile.ReadString("Paths", "TempDirectory", System.IO.Path.Combine(baseDir, "Temp"));
            _useObfuscation = _inifile.ReadBool("Settings", "UseObfuscation", false);

            // 空路径判断：配置路径为空时回退默认路径并记录提示
            PathFallbackMessage = string.Empty;
            _recordDirectory = NormalizeDirectoryPath(recordDir, System.IO.Path.Combine(baseDir, "Records"), "记录文件");
            _tempDirectory = NormalizeDirectoryPath(tempDir, System.IO.Path.Combine(baseDir, "Temp"), "临时文件");
        }

        /// <summary>
        /// 规范化目录路径：配置路径为空时回退默认路径，并追加回退提示信息。
        /// </summary>
        /// <param name="configuredPath">配置的路径（可能为空）</param>
        /// <param name="defaultPath">默认路径</param>
        /// <param name="displayName">路径用途名称（用于提示文案）</param>
        /// <returns>规范化后的路径</returns>
        private string NormalizeDirectoryPath(string configuredPath, string defaultPath, string displayName)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                string msg = string.Format("{0}路径为空 已转到默认路径 {1}", displayName, defaultPath);
                if (!string.IsNullOrEmpty(PathFallbackMessage)) PathFallbackMessage += "；";
                PathFallbackMessage += msg;
                return defaultPath;
            }
            return configuredPath;
        }

        /// <summary>
        /// 保存配置到 INI
        /// </summary>
        public void SaveConfigToIni()
        {
            ConfigFile.WriteString("Paths", "RecordDirectory", _recordDirectory);
            ConfigFile.WriteString("Paths", "TempDirectory", _tempDirectory);
            ConfigFile.WriteBool("Settings", "UseObfuscation", _useObfuscation);
            ConfigFile.SaveToFile();
        }

        /// <summary>
        /// 获取指定日期对应的记录子目录路径（格式 yyyyMMdd），供回看端按日期索引
        /// </summary>
        /// <param name="date">查询日期</param>
        /// <returns>子目录路径</returns>
        public string GetDateRecordDirectoryPath(DateTime date)
        {
            return _store.GetDateRecordDirectoryPath(date);
        }

        #endregion

        #region 回溯（转发焊缝追踪记录类 / 存储）

        /// <summary>
        /// 保存记录到正式文件（.ply + .pDat，按日期归档）
        /// </summary>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>保存的文件路径列表</returns>
        public List<string> SaveRecord(string fileName)
        {
            return _trackRecord.SaveRecord(fileName);
        }

        /// <summary>
        /// 获取所有缓存的帧
        /// </summary>
        /// <returns>帧列表</returns>
        public List<RecordFrame> GetAllFrames()
        {
            return _trackRecord.GetAllFrames();
        }

        /// <summary>
        /// 获取所有动作记录（过程帧 + 调整记录，双记录设计 ADR-008）
        /// </summary>
        /// <returns>动作记录列表</returns>
        public List<WeldActionRecord> GetAllActions()
        {
            return _store.GetAllActions();
        }

        /// <summary>
        /// 获取指定帧的点云
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>点云数据</returns>
        public PointCloudData GetFramePointCloud(int frameIndex)
        {
            return _trackRecord.GetFramePointCloud(frameIndex);
        }

        /// <summary>
        /// 从临时文件解析记录
        /// </summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <returns>解析是否成功</returns>
        public bool ParseFromTempFile(string tempFilePath)
        {
            return _trackRecord.ParseFromTempFile(tempFilePath);
        }

        /// <summary>
        /// 将临时文件转换为正常点云 PLY 和 pDat
        /// </summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <param name="outputName">输出文件名（不含扩展名）</param>
        /// <returns>转换后的文件路径列表</returns>
        public List<string> ConvertTempToNormal(string tempFilePath, string outputName)
        {
            return _trackRecord.ConvertTempToNormal(tempFilePath, outputName);
        }

        /// <summary>
        /// 解析 pDat 文件为结构化记录列表（自动处理混淆）
        /// </summary>
        /// <param name="filePath">pDat 文件路径</param>
        /// <returns>动作记录列表</returns>
        public List<WeldActionRecord> ParsePDatRecords(string filePath)
        {
            return _store.ParsePDatRecords(filePath);
        }

        /// <summary>
        /// 读取 pDat 文件内容（自动处理混淆）
        /// </summary>
        /// <param name="filePath">pDat 文件路径</param>
        /// <returns>文件文本内容</returns>
        public string ReadPDatFile(string filePath)
        {
            return _store.ReadPDatFile(filePath);
        }

        /// <summary>
        /// 获取所有临时文件列表
        /// </summary>
        /// <returns>临时文件路径列表</returns>
        public List<string> GetTempFiles()
        {
            return _store.GetTempFiles();
        }

        /// <summary>
        /// 获取所有记录文件列表（.ply，按创建时间倒序）
        /// </summary>
        /// <returns>记录文件路径列表</returns>
        public List<string> GetRecordFiles()
        {
            return _store.GetRecordFiles();
        }

        /// <summary>
        /// 获取所有焊接动作记录(pDat)文件（按创建时间倒序，递归所有日期子目录）
        /// </summary>
        /// <returns>pDat 文件路径列表</returns>
        public List<string> GetWeldActionRecordFiles()
        {
            return _store.GetWeldActionRecordFiles();
        }

        /// <summary>
        /// 按生产日期获取焊接动作记录(pDat)文件
        /// </summary>
        /// <param name="date">生产日期</param>
        /// <returns>pDat 文件路径列表</returns>
        public List<string> GetWeldActionRecordFiles(DateTime date)
        {
            return _store.GetWeldActionRecordFiles(date);
        }

        #endregion

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，ADR-020）：退订事件 + 释放记录类
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            WeldParamManager.Instance.FrameRecordRequested -= _frameHandler;
            WeldParamManager.Instance.AdjustmentRecordRequested -= _adjustHandler;
            WeldParamManager.Instance.AbortRecordRequested -= _abortHandler;

            _trackRecord?.Dispose();
            _adjustRecord?.Dispose();
            _resultRecord?.Dispose();
            _trackRecord = null;
            _adjustRecord = null;
            _resultRecord = null;
            _store = null;

            Log("产品文件记录管理器已释放");
            _disposed = true;
        }
    }
}
