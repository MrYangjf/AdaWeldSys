using AdaWeldSystem.FileOperate;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>产品文件记录管理器，单例，模块唯一对外访问口。 ①</summary>
    public sealed class ProductFileRecordManager : IDisposable
    {
        private const string Tag = "ProductFileRecordManager";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region 单例实现

        private static readonly Lazy<ProductFileRecordManager> _instance =
            new Lazy<ProductFileRecordManager>(() => new ProductFileRecordManager());

        /// <summary>单例实例</summary>
        public static ProductFileRecordManager Instance
        {
            get { return _instance.Value; }
        }

        #endregion

        #region 字段

        private readonly string _iniFilePath = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\ProductFileControl\\Record_INI\\Record.ini";
        private INIFile _inifile;
        private RecordFileStore _store;
        private LineLaserContourRecord _contourRecord;
        private WeldParamControlRecord _adjustRecord;
        private WeldResultRecord _resultRecord;
        private SeamTraceYRecord _seamTraceYRecord;
        private string _recordDirectory = string.Empty;
        private string _tempDirectory = string.Empty;
        private bool _useObfuscation = false;
        private int _retentionDays = 30;        private string _recordNamePrefix = string.Empty;
        private bool _disposed = false;

        /// <summary>生产会话四项状态，三份文件共用基名</summary>
        private string _productSn = string.Empty;
        private DateTime _sessionStartTime = DateTime.MinValue;
        private string _sessionStatus = string.Empty;
        private string _sessionBaseName = string.Empty;
        private string _sessionAbortReason = string.Empty;

        /// <summary>收口门禁锁：起焊与收口跨线程，须串行。</summary>
        private readonly object _sessionLock = new object();

        /// <summary>是否已收口：并发只首次生效，避免重复落库。</summary>
        private bool _sessionEnded;

        /// <summary>记录请求事件订阅句柄（Dispose 退订用）</summary>
        private EventHandler<WeldFrameRecordEventArgs> _frameHandler;
        private EventHandler<WeldAdjustmentRecordEventArgs> _adjustHandler;
        private EventHandler _abortHandler;

        /// <summary>当前线激光分析 Job 基线（开会话取一次，JobSwitch 后随之更新）</summary>
        private int _currentJobId;

        /// <summary>逐帧全帧记录开关</summary>
        private bool _recordEveryFrame = true;
        private EventHandler<LineLaserResultEventArgs> _laserFrameHandler;

        #endregion

        #region 属性

        /// <summary>是否正在记录（以轮廓记录类会话为准）</summary>
        public bool IsRecording
        {
            get { return _contourRecord != null && _contourRecord.IsRecording; }
        }

        /// <summary>当前帧索引</summary>
        public int CurrentFrameIndex
        {
            get { return _store != null ? _store.CurrentFrameIndex : 0; }
        }

        /// <summary>已缓存的帧数量</summary>
        public int CachedFrameCount
        {
            get { return _store != null ? _store.CachedFrameCount : 0; }
        }

        /// <summary>记录目录</summary>
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
                if (_seamTraceYRecord != null)
                {
                    _seamTraceYRecord.RecordDirectory = value;
                }
            }
        }

        /// <summary>临时文件目录</summary>
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

        /// <summary>路径回退提示</summary>
        public string PathFallbackMessage { get; private set; }

        /// <summary>是否使用混淆（pDat文件用TXT打开为乱码）</summary>
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

        /// <summary>记录保留天数</summary>
        public int RetentionDays
        {
            get { return _retentionDays; }
            set { _retentionDays = value; }
        }

        /// <summary>是否记录线激光逐帧全帧</summary>
        public bool RecordEveryFrame
        {
            get { return _recordEveryFrame; }
            set { _recordEveryFrame = value; }
        }
        /// <summary>记录文件名前缀</summary>
        public string RecordNamePrefix
        {
            get { return _recordNamePrefix; }
            set { _recordNamePrefix = value ?? string.Empty; }
        }

        /// <summary>记录配置文件（INI），供配置页直接写入键值</summary>
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
            // 启动即按保留天数清理过期记录
            _store.CleanupExpiredRecords(_retentionDays);

            // 记录类由 Manager 构造创建并持有（实现类非单例）：
            // 轮廓与工艺记录职责分离，会话由 Manager 启停
            _contourRecord = new LineLaserContourRecord(_store);
            _adjustRecord = new WeldParamControlRecord(_store);
            _resultRecord = new WeldResultRecord();
            _seamTraceYRecord = new SeamTraceYRecord(_recordDirectory);

            // 订阅 WeldParamManager 记录请求事件（订阅方只入队，落盘在后台）
            _frameHandler = OnFrameRecordRequested;
            _adjustHandler = OnAdjustmentRecordRequested;
            _abortHandler = OnAbortRecordRequested;
            WeldParamManager.Instance.FrameRecordRequested += _frameHandler;
            WeldParamManager.Instance.AdjustmentRecordRequested += _adjustHandler;
            WeldParamManager.Instance.AbortRecordRequested += _abortHandler;

            // 逐帧全帧记录：挂线激光结果事件，每帧一行
            _laserFrameHandler = OnLineLaserFrameReady;
            LineLaserManager.Instance.ResultReady += _laserFrameHandler;

            Log("产品文件记录管理器已就绪");
        }

        #endregion

        #region 记录请求事件处理

        /// <summary>每帧过程快照记录请求 → 工艺控制记录类（过程帧，AdjustExecuted=false）</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">过程帧事件参数</param>
        private void OnFrameRecordRequested(object sender, WeldFrameRecordEventArgs e)
        {
            // 逐帧模式下过程帧随线激光回调落盘，不重复
            if (_recordEveryFrame) return;
            WeldParamManager mgr = WeldParamManager.Instance;
            _adjustRecord.RecordProcessFrame(new WeldParamAdjustItem
            {
                RobotX = e.RobotX,
                RobotY = e.RobotY,
                RobotZ = e.RobotZ,
                RobotSpeed = e.RobotSpeed,
                FeedSpeed = e.FeedSpeed,
                LaserPower = e.LaserPower,
                WeldWidth = e.WeldWidth,
                JobId = _currentJobId,
                SeamPointY = mgr.LastSeamPointY,
                SeamPointZ = mgr.LastSeamPointZ
            });
        }

        /// <summary>调整动作记录请求 → 工艺控制记录类（调整动作，AdjustExecuted=true）</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">调整记录事件参数</param>
        private void OnAdjustmentRecordRequested(object sender, WeldAdjustmentRecordEventArgs e)
        {
            WeldParamManager mgr = WeldParamManager.Instance;
            _adjustRecord.RecordAdjustment(new WeldParamAdjustItem
            {
                RobotX = e.RobotX,
                RobotY = e.RobotY,
                RobotZ = e.RobotZ,
                RobotSpeed = e.RobotSpeed,
                FeedSpeed = e.FeedSpeed,
                LaserPower = e.LaserPower,
                WeldWidth = e.WeldWidth,
                JobId = _currentJobId,
                SeamPointY = mgr.LastSeamPointY,
                SeamPointZ = mgr.LastSeamPointZ,
                AdjustData = string.Format("FeedSpeed={0};LaserPower={1};WeldWidth={2}",
                    e.FeedSpeed, e.LaserPower, e.WeldWidth)
            });
        }

        /// <summary>受理异常中止记录请求</summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">中止事件参数</param>
        private void OnAbortRecordRequested(object sender, EventArgs e)
        {
            _sessionAbortReason = "连续失败超限 记录会话异常中止";
            AbortRecording();
        }

        /// <summary>线激光每帧结果：逐帧全帧记录（机器人位姿/特征</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void OnLineLaserFrameReady(object sender, LineLaserResultEventArgs e)
        {
            if (!_recordEveryFrame || !IsRecording) return;
            WeldParamManager mgr = WeldParamManager.Instance;
            LineLaserSeamResult r = e.Result;
            _adjustRecord.RecordProcessFrame(new WeldParamAdjustItem
            {
                RobotX = mgr.CurrentRobotX,
                RobotY = mgr.CurrentRobotY,
                RobotZ = mgr.CurrentRobotZ,
                RobotSpeed = mgr.CurrentRobotSpeed,
                FeedSpeed = mgr.FeedSpeedOutput,
                LaserPower = mgr.LaserPowerOutput,
                WeldWidth = mgr.CurrentSeamWidth,
                JobId = _currentJobId,
                SeamPointY = r.FeatureY,
                SeamPointZ = r.FeatureZ
            });
        }

        #endregion

        #region 生产会话（产品生产生命周期）

        /// <summary>会话基名（三份记录文件共用；未开启为空）</summary>
        public string SessionBaseName
        {
            get { return _sessionBaseName; }
        }

        /// <summary>本次生产会话的产品码（空缺时由 BeginProductSession 以 TEST 填充）</summary>
        public string ProductSn
        {
            get { return _productSn; }
        }

        /// <summary>本次生产会话的回溯标识（模拟 / 正常生产）</summary>
        public string SessionStatus
        {
            get { return _sessionStatus; }
        }

        /// <summary>定位单份记录文件：优先新会话子文件夹；不存在</summary>

        /// <param name="sessionDir">会话目录</param>
        /// <param name="baseName">文件基名</param>
        /// <param name="dateDir">日期目录</param>
        /// <param name="ext">扩展名</param>
        /// <returns>记录文件路径</returns>
        private static string PickExisting(string sessionDir, string baseName, string dateDir, string ext)
        {
            string candidate = System.IO.Path.Combine(sessionDir, baseName + ext);
            if (System.IO.File.Exists(candidate)) return candidate;
            return System.IO.Path.Combine(dateDir, baseName + ext);
        }

        /// <summary>开启一次生产会话</summary>

        /// <param name="sn">产品码（空缺时以 TEST 占位；产品码将由机器人传输指令下发）</param>
        /// <param name="isSimulation">是否模拟测试会话（为真时文件名加「模拟」前缀）</param>
        public void BeginProductSession(string sn, bool isSimulation)
        {
            _productSn = string.IsNullOrWhiteSpace(sn) ? "TEST" : sn.Trim();
            _sessionStatus = isSimulation ? "模拟" : "正常生产";
            _recordNamePrefix = isSimulation ? "模拟" : string.Empty;
            _sessionStartTime = DateTime.Now;
            _sessionBaseName = string.Format("{0}{1}_{2}",
                _recordNamePrefix, _productSn, _sessionStartTime.ToString("yyyyMMdd_HHmmss"));
            _sessionAbortReason = string.Empty;
            lock (_sessionLock) { _sessionEnded = false; }

            StartRecording();
            // SeamTraceY 不从生产会话开启：起点为 Ready 检查通过后
            // 由运控调 BeginSeamTraceYSession 开启；否则会记全 0 空行，干扰判读
            // 结果记录随生产会话开启，否则入队会被丢弃
            // 产品库按日期查询恒为空 → 回溯表无模拟记录
            _resultRecord.Start();
            Log(string.Format("生产会话开始 产品码 {0} 状态 {1} 会话基名 {2}",
                _productSn, _sessionStatus, _sessionBaseName));
        }

        /// <summary>结束生产会话并落库</summary>

        /// <param name="result">焊接结果（正常焊接完成为 pass，异常焊接为 fail）</param>
        /// <param name="info">异常焊接原因（result=fail 时填写）</param>
        public void EndProductSession(string result, string info)
        {
            // 焊缝行程到程时 CheckWeldProgress 的 DoSubStop 与随后的 DisableSimulation 会在两个线程上
            // 两线程先后进入：门禁须在锁内原子判定
            // 非原子则两次都通过，会重复落库与乱序
            lock (_sessionLock)
            {
                if (_sessionEnded) return;
                if (_sessionStartTime == DateTime.MinValue)
                {
                    return;   // 未开生产会话（如未走起焊指令直接停机）
                }
                _sessionEnded = true;
            }

            string finalResult = result;
            string finalInfo = info;
            if (!string.IsNullOrEmpty(_sessionAbortReason))
            {
                finalResult = "fail";
                if (string.IsNullOrEmpty(finalInfo)) finalInfo = _sessionAbortReason;
            }

            StopRecording();
            FlushSeamTraceY();
            EndSeamTraceYSession();

            RecordWeldResult(_productSn, _sessionStartTime, _sessionStatus, finalResult, finalInfo);
            _resultRecord.Stop();
            Log(string.Format("生产会话结束 产品码 {0} 结果 {1} 原因 {2}",
                _productSn, finalResult, string.IsNullOrEmpty(finalInfo) ? "无" : finalInfo));

            _sessionStartTime = DateTime.MinValue;
            _sessionBaseName = string.Empty;
            _sessionAbortReason = string.Empty;
        }

        /// <summary>推算三份记录文件路径</summary>

        /// <param name="startTime">生产记录的起焊时间</param>
        /// <param name="sn">生产记录的产品码</param>
        /// <param name="status">生产记录的状态（模拟 / 正常生产）</param>
        /// <returns>三份记录文件路径列表</returns>
        public List<string> GetSessionRecordFiles(DateTime startTime, string sn, string status)
        {
            string prefix = string.Equals(status, "模拟", StringComparison.Ordinal) ? "模拟" : string.Empty;
            string code = string.IsNullOrWhiteSpace(sn) ? "TEST" : sn.Trim();
            string baseName = string.Format("{0}{1}_{2}", prefix, code, startTime.ToString("yyyyMMdd_HHmmss"));
            string dateDir = GetDateRecordDirectoryPath(startTime);
            string sessionDir = System.IO.Path.Combine(dateDir, baseName);

            var files = new List<string>();
            files.Add(PickExisting(sessionDir, baseName, dateDir, ".ply"));
            files.Add(PickExisting(sessionDir, baseName, dateDir, ".pDat"));
            files.Add(PickExisting(sessionDir, baseName, dateDir, ".csv"));
            return files;
        }

        #endregion

        #region 记录控制

        /// <summary>开始记录（轮廓会话 + 工艺控制会话同步开启；幂等）</summary>
        public void StartRecording()
        {
            // 会话开始取一次当前 Job 作为基线；后续 Job 切换由 RecordJobSwitch 更新，逐帧不再查询
            _currentJobId = WeldParamManager.Instance.CurrentLaserJobId;
            _contourRecord.Start();
            _adjustRecord.Start();
        }

        /// <summary>停止记录并归档</summary>
        public void StopRecording()
        {
            if (!IsRecording)
            {
                return;
            }
            // 先排空工艺控制记录队列，再关共享会话
            // 其落盘依赖轮廓会话；先关会丢弃全部动作记录
            _adjustRecord.Stop();
            _contourRecord.Stop();

            // 排空后归档；文件名优先取会话基名（与轨迹同源）
            try
            {
                string baseName;
                if (!string.IsNullOrEmpty(_sessionBaseName))
                {
                    baseName = _sessionBaseName;
                }
                else if (string.IsNullOrEmpty(_recordNamePrefix))
                {
                    baseName = string.Format("Record_{0:yyyyMMdd_HHmmss}", DateTime.Now);
                }
                else
                {
                    baseName = string.Format("{0}Record_{1:yyyyMMdd_HHmmss}", _recordNamePrefix, DateTime.Now);
                }
                List<string> saved = _store.SaveRecord(baseName);
                Log(string.Format("会话归档完成 文件数 {0}", saved != null ? saved.Count : 0));
            }
            catch (Exception ex)
            {
                Log("会话归档失败 " + ex.Message, MessageLevel.Warning);
            }
        }

        /// <summary>异常中断记录（保留临时文件以便追溯）</summary>
        public void AbortRecording()
        {
            _adjustRecord.Abort();
            _contourRecord.Abort();
        }

        /// <summary>开启焊缝 Y 记录会话（Ready 通过后由运控调用）</summary>

        /// <returns>会话开启成功（或已开启）返回 true；基名未就绪返回 false</returns>
        public bool BeginSeamTraceYSession()
        {
            if (_seamTraceYRecord.IsSessionActive) return true;
            if (string.IsNullOrEmpty(_sessionBaseName))
            {
                return false;
            }
            return _seamTraceYRecord.BeginSession(_sessionBaseName);
        }

        /// <summary>记录一拍焊缝 Y（热路径只入内存，不碰磁盘）。工艺调</summary>

        /// <param name="robotX">当前机器人 X（毫米，查图中心）</param>
        /// <param name="mapKeyX">入图键 X（毫米 = 测量时刻 RobotX + 前置量；尚无入图点时传 0）</param>
        /// <param name="cspInYt">最近一次入图的绝对目标 Yt（毫米；尚无入图点时传 0）</param>
        /// <param name="cspOutY">本拍查图输出 Y（毫米）</param>
        /// <param name="axisY">焊缝跟踪轴实际反馈位置（毫米；模拟记录模式传虚拟轴位置）</param>
        public void RecordSeamTraceYPoint(double robotX, double mapKeyX, double cspInYt, double cspOutY, double axisY)
        {
            _seamTraceYRecord.RecordPoint(robotX, mapKeyX, cspInYt, cspOutY, axisY);
        }

        /// <summary>读取一份焊缝 Y 记录 CSV（数据回溯端一次性读取，不做</summary>

        /// <param name="filePath">CSV 路径</param>
        /// <returns>记录点列表（文件缺失或解析失败时为空列表）</returns>
        public List<SeamTraceYPoint> ReadSeamTraceYPoints(string filePath)
        {
            return SeamTraceYRecord.ParseFile(filePath);
        }

        /// <summary>把缓存批量落盘（焊接停止沿调用），缓存清空。</summary>
        public void FlushSeamTraceY()
        {
            _seamTraceYRecord.FlushToFile();
        }

        /// <summary>结束记录会话：落盘并复位（模拟关闭时兜底调用）。</summary>
        public void EndSeamTraceYSession()
        {
            _seamTraceYRecord.EndSession();
        }

        /// <summary>记录一帧线激光轮廓</summary>
        /// <param name="pointCloud">点云数据</param>
        public void AddFrame(PointCloudData pointCloud)
        {
            _contourRecord.AddPointCloud(pointCloud);
        }

        /// <summary>添加一帧点云并携带捕获时刻的机器人 X</summary>

        /// <param name="pointCloud">点云数据</param>
        /// <param name="robotX">捕获该帧时的机器人 X 坐标（毫米）</param>
        public void AddFrame(PointCloudData pointCloud, double robotX)
        {
            _contourRecord.AddPointCloud(pointCloud, robotX);
        }


        /// <summary>写入一条生产结果记录</summary>
        /// <param name="sn">产品码</param>
        /// <param name="startTime">焊接开始时间</param>
        /// <param name="status">记录状态（模拟 / 正常生产）</param>
        /// <param name="result">焊接结果（pass / fail）</param>
        /// <param name="info">异常焊接原因（result=fail 时填写）</param>
        public void RecordWeldResult(string sn, DateTime startTime, string status,
            string result, string info)
        {
            _resultRecord.RecordResult(new WeldResultItem
            {
                Sn = sn,
                StartTime = startTime,
                Status = status,
                Result = result,
                Info = info
            });
        }

        /// <summary>查询当日生产记录</summary>
        /// <param name="date">查询日期</param>
        /// <returns>当日生产记录表</returns>
        /// <param name="date">生产日期</param>
        /// <returns>该日期生产记录数据表；无数据返回 null</returns>
        public System.Data.DataTable SelectProductRecords(DateTime date)
        {
            return _resultRecord.SelectResultTable(date);
        }

        /// <summary>按时间区间查询生产记录（数据追溯页）</summary>
        /// <param name="start">区间起始日（含）</param>
        /// <param name="end">区间结束日（含）</param>
        /// <returns>合并后的记录表；数据库不可用返回 null，区间内无记录返回零行的表</returns>
        public System.Data.DataTable SelectProductRecords(DateTime start, DateTime end)
        {
            return _resultRecord.SelectResultTable(start, end);
        }

        /// <summary>记录一次分析工具切换</summary>

        /// <param name="jobId">切换到的线激光分析 Job 编号</param>
        /// <param name="robotX">切换时刻机器人 X 坐标</param>
        /// <param name="robotY">切换时刻机器人 Y 坐标</param>
        public void RecordJobSwitch(int jobId, double robotX, double robotY)
        {
            // 切换后更新 Job 基线，后续过程帧跟随新 Job
            _currentJobId = jobId;
            WeldParamManager mgr = WeldParamManager.Instance;
            _adjustRecord.RecordJobSwitch(new WeldParamAdjustItem
            {
                RobotX = robotX,
                RobotY = robotY,
                RobotZ = 0,
                RobotSpeed = mgr.RobotSpeedOutput,
                FeedSpeed = mgr.FeedSpeedOutput,
                LaserPower = mgr.LaserPowerOutput,
                WeldWidth = mgr.CurrentSeamWidth,
                JobId = jobId,
                SeamPointY = mgr.LastSeamPointY,
                SeamPointZ = mgr.LastSeamPointZ,
                AdjustData = string.Format("JobId={0}", jobId)
            });
        }

        #endregion

        #region 配置管理

        /// <summary>规范化目录路径</summary>
        /// <param name="configuredPath">配置读取到的路径</param>
        /// <param name="defaultPath">缺省路径</param>
        /// <param name="displayName">目录用途名（日志用）</param>
        /// <returns>可用的目录路径</returns>
        /// <param name="configuredPath">配置的路径（可能为空）</param>
        /// <param name="defaultPath">默认路径</param>
        /// <param name="displayName">路径用途名称（用于提示文案）</param>
        /// <returns>规范化后的路径</returns>
        private string NormalizeDirectoryPath(string configuredPath, string defaultPath, string displayName)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                if (!string.IsNullOrEmpty(PathFallbackMessage)) PathFallbackMessage += "；";
                PathFallbackMessage += string.Format("{0}路径为空 已转到默认路径 {1}", displayName, defaultPath);
                return defaultPath;
            }
            return configuredPath;
        }

        /// <summary>从 INI 加载配置（空路径回退默认值并记录提示）</summary>
        public void LoadConfigFromIni()
        {
            _inifile = new INIFile(_iniFilePath);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string recordDir = _inifile.ReadString("Paths", "RecordDirectory", System.IO.Path.Combine(baseDir, "Records"));
            string tempDir = _inifile.ReadString("Paths", "TempDirectory", System.IO.Path.Combine(baseDir, "Temp"));
            _useObfuscation = _inifile.ReadBool("Settings", "UseObfuscation", false);
            _retentionDays = _inifile.ReadInt("Settings", "RetentionDays", 30);
            _recordEveryFrame = _inifile.ReadBool("Settings", "RecordEveryFrame", true);

            // 配置路径为空则回退默认路径并记录提示
            PathFallbackMessage = string.Empty;
            _recordDirectory = NormalizeDirectoryPath(recordDir, System.IO.Path.Combine(baseDir, "Records"), "记录文件");
            _tempDirectory = NormalizeDirectoryPath(tempDir, System.IO.Path.Combine(baseDir, "Temp"), "临时文件");
        }

        /// <summary>保存配置到 INI</summary>
        public void SaveConfigToIni()
        {
            ConfigFile.WriteString("Paths", "RecordDirectory", _recordDirectory);
            ConfigFile.WriteString("Paths", "TempDirectory", _tempDirectory);
            ConfigFile.WriteBool("Settings", "UseObfuscation", _useObfuscation);
            ConfigFile.WriteInt("Settings", "RetentionDays", _retentionDays);
            ConfigFile.WriteBool("Settings", "RecordEveryFrame", _recordEveryFrame);
            ConfigFile.SaveToFile();
        }

        /// <summary>获取指定日期的记录目录</summary>
        /// <param name="date">查询日期</param>
        /// <returns>子目录路径</returns>
        public string GetDateRecordDirectoryPath(DateTime date)
        {
            return _store.GetDateRecordDirectoryPath(date);
        }

        #endregion

        #region 回溯（转发记录文件存储）

        /// <summary>保存记录到正式文件（.ply + .pDat，按日期归档）</summary>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>保存的文件路径列表</returns>
        public List<string> SaveRecord(string fileName)
        {
            return _store.SaveRecord(fileName);
        }

        /// <summary>获取所有缓存的帧</summary>
        /// <returns>帧列表</returns>
        public List<RecordFrame> GetAllFrames()
        {
            return _store.GetAllFrames();
        }

        /// <summary>获取所有动作记录（过程帧 + 调整记录）</summary>
        /// <returns>动作记录列表</returns>
        public List<WeldActionRecord> GetAllActions()
        {
            return _store.GetAllActions();
        }

        /// <summary>获取指定帧的点云</summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>点云数据</returns>
        public PointCloudData GetFramePointCloud(int frameIndex)
        {
            return _store.GetFramePointCloud(frameIndex);
        }

        /// <summary>从临时文件解析记录</summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <returns>解析是否成功</returns>
        public bool ParseFromTempFile(string tempFilePath)
        {
            return _store.ParseFromTempFile(tempFilePath);
        }

        /// <summary>将临时文件转换为正常点云 PLY 和 pDat</summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <param name="outputName">输出文件名（不含扩展名）</param>
        /// <returns>转换后的文件路径列表</returns>
        public List<string> ConvertTempToNormal(string tempFilePath, string outputName)
        {
            return _store.ConvertTempToNormal(tempFilePath, outputName);
        }

        /// <summary>解析 pDat 文件为结构化记录列表（自动处理混淆）</summary>
        /// <param name="filePath">pDat 文件路径</param>
        /// <returns>动作记录列表</returns>
        public List<WeldActionRecord> ParsePDatRecords(string filePath)
        {
            return _store.ParsePDatRecords(filePath);
        }

        /// <summary>读取 pDat 文件内容（自动处理混淆）</summary>
        /// <param name="filePath">pDat 文件路径</param>
        /// <returns>文件文本内容</returns>
        public string ReadPDatFile(string filePath)
        {
            return _store.ReadPDatFile(filePath);
        }

        /// <summary>获取所有临时文件列表</summary>
        /// <returns>临时文件路径列表</returns>
        public List<string> GetTempFiles()
        {
            return _store.GetTempFiles();
        }

        /// <summary>按当前保留天数清理过期记录（对话框保存后调</summary>
        public void CleanupExpiredRecords()
        {
            try { _store.CleanupExpiredRecords(_retentionDays); }
            catch { }   // 清理失败不影响主流程：过期旧文件最多多留一天
        }

        /// <summary>获取所有记录文件列表（.ply，按创建时间倒序）</summary>
        /// <returns>记录文件路径列表</returns>
        public List<string> GetRecordFiles()
        {
            return _store.GetRecordFiles();
        }

        /// <summary>列出动作记录文件</summary>
        /// <returns>pDat 文件路径列表</returns>
        public List<string> GetWeldActionRecordFiles()
        {
            return _store.GetWeldActionRecordFiles();
        }

        /// <summary>按生产日期获取焊接动作记录(pDat)文件</summary>
        /// <param name="date">生产日期</param>
        /// <returns>pDat 文件路径列表</returns>
        public List<string> GetWeldActionRecordFiles(DateTime date)
        {
            return _store.GetWeldActionRecordFiles(date);
        }

        #endregion

        /// <summary>释放资源（标准 IDisposable.Dispose）：退订事件 + 释放记录类</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            WeldParamManager.Instance.FrameRecordRequested -= _frameHandler;
            WeldParamManager.Instance.AdjustmentRecordRequested -= _adjustHandler;
            WeldParamManager.Instance.AbortRecordRequested -= _abortHandler;
            LineLaserManager.Instance.ResultReady -= _laserFrameHandler;

            _contourRecord?.Dispose();
            _adjustRecord?.Dispose();
            _resultRecord?.Dispose();
            _seamTraceYRecord?.EndSession();
            _contourRecord = null;
            _adjustRecord = null;
            _resultRecord = null;
            _seamTraceYRecord = null;
            _store = null;

            Log("产品文件记录管理器已释放");
            _disposed = true;
        }
    }
}
