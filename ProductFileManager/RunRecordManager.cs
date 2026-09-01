using AdaWeldSystem.FileOperate;
using AdaWeldSystem.PCLOperate.DataIO;
using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AdaWeldSystem.ProductFileManager
{
    /// <summary>
    /// 运行记录管理器 - 单例模式
    /// 负责程序运行过程中3D点云数据的缓存保存和实时记录
    /// </summary>
    public sealed class RunRecordManager
    {
        #region 单例实现

        private static readonly Lazy<RunRecordManager> _instance = new Lazy<RunRecordManager>(() => new RunRecordManager());
        /// <summary>
        /// 单例实例
        /// </summary>
        public static RunRecordManager Instance => _instance.Value;

        #endregion

        #region 字段

        private readonly object _lockObj = new object();
        private readonly List<RecordFrame> _frames = new List<RecordFrame>();
        private readonly List<WeldActionRecord> _actionRecords = new List<WeldActionRecord>();
        private string _tempFilePath = string.Empty;
        private string _recordDirectory = string.Empty;
        private string _tempDirectory = string.Empty;
        private bool _isRecording = false;
        private bool _useObfuscation = true;
        private int _frameIndex = 0;
        private string _iniFilePath = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\INI\\Record.ini";
        private INIFile _inifile;

        #endregion

        #region 属性

        /// <summary>
        /// 是否正在记录
        /// </summary>
        public bool IsRecording => _isRecording;

        /// <summary>
        /// 当前帧索引
        /// </summary>
        public int CurrentFrameIndex => _frameIndex;

        /// <summary>
        /// 已缓存的帧数量
        /// </summary>
        public int CachedFrameCount
        {
            get
            {
                lock (_lockObj)
                {
                    return _frames.Count;
                }
            }
        }

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
        /// <summary>
        /// 记录目录
        /// </summary>
        public string RecordDirectory
        {
            get => _recordDirectory;
            set => _recordDirectory = value;
        }

        /// <summary>
        /// 临时文件目录
        /// </summary>
        public string TempDirectory
        {
            get => _tempDirectory;
            set => _tempDirectory = value;
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
            get => _useObfuscation;
            set => _useObfuscation = value;
        }

        #endregion

        #region 构造函数

        private RunRecordManager()
        {
            LoadConfigFromIni();
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 从INI加载配置
        /// </summary>
        public void LoadConfigFromIni()
        {
            _inifile = new INIFile(_iniFilePath);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string recordDir = _inifile.ReadString("Paths", "RecordDirectory", Path.Combine(baseDir, "Records"));
            string tempDir = _inifile.ReadString("Paths", "TempDirectory", Path.Combine(baseDir, "Temp"));
            _useObfuscation = _inifile.ReadBool("Settings", "UseObfuscation", false);

            // 空路径判断：配置路径为空时回退默认路径并记录提示
            PathFallbackMessage = string.Empty;
            _recordDirectory = NormalizeDirectoryPath(recordDir, Path.Combine(baseDir, "Records"), "记录文件");
            _tempDirectory = NormalizeDirectoryPath(tempDir, Path.Combine(baseDir, "Temp"), "临时文件");

            EnsureDirectoriesExist();
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
        /// 保存配置到INI
        /// </summary>
        public void SaveConfigToIni()
        {
            _inifile.WriteString("Paths", "RecordDirectory", _recordDirectory);
            _inifile.WriteString("Paths", "TempDirectory", _tempDirectory);
            _inifile.WriteBool("Settings", "UseObfuscation", _useObfuscation);
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(_recordDirectory))
            {
                Directory.CreateDirectory(_recordDirectory);
            }
            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        /// <summary>
        /// 获取指定日期对应的记录子目录路径（格式 yyyyMMdd），不创建目录，供回看端按日期索引
        /// </summary>
        public string GetDateRecordDirectoryPath(DateTime date)
        {
            return Path.Combine(_recordDirectory, date.ToString("yyyyMMdd"));
        }

        /// <summary>
        /// 获取指定日期对应的记录子目录（格式 yyyyMMdd），不存在则创建，供保存端写入
        /// </summary>
        private string EnsureDateRecordDirectory(DateTime date)
        {
            string dir = GetDateRecordDirectoryPath(date);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        #endregion

        #region 记录控制

        /// <summary>
        /// 开始记录
        /// </summary>
        public void StartRecording()
        {
            lock (_lockObj)
            {
                if (_isRecording) return;

                EnsureDirectoriesExist();
                _frames.Clear();
                _actionRecords.Clear();
                _frameIndex = 0;
                _isRecording = true;
                
                string tempFileName = string.Format("TempRecord_{0:yyyyMMdd_HHmmss}.tmp", DateTime.Now);
                _tempFilePath = Path.Combine(_tempDirectory, tempFileName);
            }
        }

        /// <summary>
        /// 停止记录（正常结束，删除临时文件）
        /// </summary>
        public void StopRecording()
        {
            lock (_lockObj)
            {
                if (!_isRecording) return;

                _isRecording = false;

                // 正常结束，删除临时文件
                if (File.Exists(_tempFilePath))
                {
                    try
                    {
                        File.Delete(_tempFilePath);
                    }
                    catch
                    {
                        // 静默处理删除失败
                    }
                }

                _tempFilePath = string.Empty;
            }
        }

        /// <summary>
        /// 异常中断记录（保留临时文件）
        /// </summary>
        public void AbortRecording()
        {
            lock (_lockObj)
            {
                if (!_isRecording) return;

                _isRecording = false;

                // 异常中断，保存临时文件以便追溯
                if (_frames.Count > 0)
                {
                    SaveTempFileInternal();
                }
            }
        }

        #endregion

        #region 数据添加

        /// <summary>
        /// 添加一帧点云数据
        /// </summary>
        /// <param name="pointCloud">点云数据</param>
        public void AddFrame(PointCloudData pointCloud)
        {
            if (pointCloud == null) throw new ArgumentNullException(nameof(pointCloud));
            if (!_isRecording) return;

            lock (_lockObj)
            {
                var frame = new RecordFrame
                {
                    Index = _frameIndex++,
                    Timestamp = DateTime.Now,
                    PointCloud = ClonePointCloud(pointCloud)
                };
                _frames.Add(frame);

                // 后台写入临时文件
                AppendToTempFile(frame);
            }
        }

        /// <summary>
        /// 添加SmartRay原始点云数据
        /// </summary>
        /// <param name="numPoints">点数</param>
        /// <param name="point3d">点数组</param>
        /// <param name="intensity">强度数组</param>
        /// <param name="transportResolution">传输分辨率</param>
        public void AddSmartRayFrame(uint numPoints, Smartray.Api.Point3d[] point3d,
            ushort[] intensity, float transportResolution)
        {
            if (point3d == null) throw new ArgumentNullException(nameof(point3d));
            if (!_isRecording) return;

            PointCloudData data = SmartRayConverter.ToPointCloudData(numPoints, point3d, intensity, transportResolution);
            AddFrame(data);
        }

        /// <summary>
        /// 向动作记录列表追加一条记录（线程安全）
        /// </summary>
        private void AddRecord(WeldActionRecord record)
        {
            if (record == null) return;
            if (!_isRecording) return;

            lock (_lockObj)
            {
                _actionRecords.Add(record);
            }
        }

        /// <summary>
        /// 记录一帧过程数据（全流程连续记录，AdjustExecuted=false）。
        /// 由 WeldProcess.StoreChartData 每帧调用，联动图像算法焊缝宽度与智能焊接输出。
        /// </summary>
        public void RecordProcessFrame(int frameIndex, double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = frameIndex,
                Timestamp = DateTime.Now,
                ActionType = "ProcessFrame",
                RobotX = robotX,
                RobotY = robotY,
                RobotZ = robotZ,
                RobotSpeed = robotSpeed,
                FeedSpeed = feedSpeed,
                LaserPower = laserPower,
                WeldWidth = weldWidth,
                AdjustExecuted = false,
                AdjustData = string.Empty
            });
        }

        /// <summary>
        /// 记录一次自适应调整动作（AdjustExecuted=true），adjustData 携带调整前后参数明细。
        /// 由 WeldProcess.RecordActionToRunFileRecord 在调整触发时调用。
        /// </summary>
        public void RecordAdjustment(int frameIndex, double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth, string adjustData)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = frameIndex,
                Timestamp = DateTime.Now,
                ActionType = "WeldParamAdjust",
                RobotX = robotX,
                RobotY = robotY,
                RobotZ = robotZ,
                RobotSpeed = robotSpeed,
                FeedSpeed = feedSpeed,
                LaserPower = laserPower,
                WeldWidth = weldWidth,
                AdjustExecuted = true,
                AdjustData = adjustData ?? string.Empty
            });
        }

        /// <summary>
        /// 兼容旧调用：以字符串参数形式写入一条记录。
        /// </summary>
        public void AddActionRecord(int frameIndex, string actionType, string parameter)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = frameIndex,
                Timestamp = DateTime.Now,
                ActionType = actionType ?? string.Empty,
                RobotX = 0,
                RobotY = 0,
                RobotZ = 0,
                RobotSpeed = 0,
                FeedSpeed = 0,
                LaserPower = 0,
                WeldWidth = 0,
                AdjustExecuted = string.Equals(actionType, "WeldParamAdjust", StringComparison.OrdinalIgnoreCase),
                AdjustData = parameter ?? string.Empty
            });
        }

        #endregion

        #region 文件保存

        /// <summary>
        /// 保存记录到正式文件
        /// </summary>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>保存的文件路径列表</returns>
        public List<string> SaveRecord(string fileName)
        {
            lock (_lockObj)
            {
                var savedFiles = new List<string>();
                if (_frames.Count == 0) return savedFiles;

                EnsureDirectoriesExist();
                // 按生产日期归档到 yyyyMMdd 子目录，供回看端按日期索引
                string dateDir = EnsureDateRecordDirectory(DateTime.Now);
                string baseName = string.IsNullOrEmpty(fileName)
                    ? string.Format("Record_{0:yyyyMMdd_HHmmss}", DateTime.Now)
                    : fileName;

                // 保存点云PLY文件
                string plyPath = Path.Combine(dateDir, baseName + ".ply");
                SaveFramesToPly(plyPath);
                savedFiles.Add(plyPath);

                // 保存动作记录pDat文件
                string pDatPath = Path.Combine(dateDir, baseName + ".pDat");
                SaveActionsToPDat(pDatPath);
                savedFiles.Add(pDatPath);

                return savedFiles;
            }
        }

        /// <summary>
        /// 保存所有帧合并为一个PLY文件
        /// </summary>
        private void SaveFramesToPly(string filePath)
        {
            var mergedData = new PointCloudData();
            foreach (var frame in _frames)
            {
                if (frame.PointCloud.HasColor)
                {
                    foreach (var point in frame.PointCloud.ColoredPoints)
                    {
                        mergedData.AddPoint(point);
                    }
                }
                else
                {
                    foreach (var point in frame.PointCloud.Points)
                    {
                        mergedData.AddPoint(point);
                    }
                }
            }
            PointCloudWriter.SaveToPly(mergedData, filePath);
        }

        /// <summary>
        /// 保存动作记录到pDat文件
        /// </summary>
        private void SaveActionsToPDat(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Weld Action Record");
            sb.AppendLine($"# FrameCount: {_frames.Count}");
            sb.AppendLine($"# ActionCount: {_actionRecords.Count}");
            sb.AppendLine("# Format: FrameIndex|Timestamp|ActionType|RobotX|RobotY|RobotZ|RobotSpeed|FeedSpeed|LaserPower|WeldWidth|AdjustExecuted|AdjustData");
            sb.AppendLine();

            foreach (var action in _actionRecords)
            {
                string timestampStr = action.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string adjustFlag = action.AdjustExecuted ? "1" : "0";
                string adjustData = (action.AdjustData ?? string.Empty).Replace("|", "/");
                sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}",
                    action.FrameIndex, timestampStr, action.ActionType,
                    action.RobotX, action.RobotY, action.RobotZ,
                    action.RobotSpeed, action.FeedSpeed, action.LaserPower, action.WeldWidth,
                    adjustFlag, adjustData));
            }

            string content = sb.ToString();

            if (_useObfuscation)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                ObfuscateBytes(bytes);
                File.WriteAllBytes(filePath, bytes);
            }
            else
            {
                File.WriteAllText(filePath, content, Encoding.UTF8);
            }
        }

        #endregion

        #region 临时文件处理

        /// <summary>
        /// 保存临时文件（内部调用）
        /// </summary>
        private void SaveTempFileInternal()
        {
            if (string.IsNullOrEmpty(_tempFilePath) || _frames.Count == 0) return;

            try
            {
                using (var fs = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs, Encoding.UTF8))
                {
                    // 写入头部信息
                    writer.Write(_frames.Count);
                    writer.Write(_actionRecords.Count);
                    writer.Write(_useObfuscation);
                    writer.Write((int)2); // 动作记录格式版本

                    // 写入帧数据
                    foreach (var frame in _frames)
                    {
                        writer.Write(frame.Index);
                        writer.Write(frame.Timestamp.ToBinary());
                        WritePointCloudToStream(writer, frame.PointCloud);
                    }

                    // 写入动作记录
                    foreach (var action in _actionRecords)
                    {
                        WriteActionToStream(writer, action);
                    }
                }
            }
            catch
            {
                // 静默处理保存失败
            }
        }

        /// <summary>
        /// 追加帧到临时文件
        /// </summary>
        private void AppendToTempFile(RecordFrame frame)
        {
            if (string.IsNullOrEmpty(_tempFilePath)) return;

            try
            {
                bool fileExists = File.Exists(_tempFilePath);
                using (var fs = new FileStream(_tempFilePath, FileMode.Append, FileAccess.Write))
                using (var writer = new BinaryWriter(fs, Encoding.UTF8))
                {
                    if (!fileExists)
                    {
                        // 首次写入头部占位
                        writer.Write(0); // 帧数占位
                        writer.Write(0); // 动作数占位
                        writer.Write(_useObfuscation);
                        writer.Write((int)2); // 版本占位
                    }

                    writer.Write(frame.Index);
                    writer.Write(frame.Timestamp.ToBinary());
                    WritePointCloudToStream(writer, frame.PointCloud);
                }
            }
            catch
            {
                // 静默处理追加失败
            }
        }

        /// <summary>
        /// 从临时文件解析记录
        /// </summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <returns>解析是否成功</returns>
        public bool ParseFromTempFile(string tempFilePath)
        {
            if (!File.Exists(tempFilePath)) return false;

            lock (_lockObj)
            {
                _frames.Clear();
                _actionRecords.Clear();

                try
                {
                    using (var fs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                    using (var reader = new BinaryReader(fs, Encoding.UTF8))
                    {
                        int frameCount = reader.ReadInt32();
                        int actionCount = reader.ReadInt32();
                        bool useObf = reader.ReadBoolean();
                        int version = reader.ReadInt32();

                        for (int i = 0; i < frameCount; i++)
                        {
                            var frame = new RecordFrame
                            {
                                Index = reader.ReadInt32(),
                                Timestamp = DateTime.FromBinary(reader.ReadInt64()),
                                PointCloud = ReadPointCloudFromStream(reader)
                            };
                            _frames.Add(frame);
                        }

                        for (int i = 0; i < actionCount; i++)
                        {
                            if (version >= 2)
                            {
                                _actionRecords.Add(ReadActionFromStream(reader));
                            }
                            else
                            {
                                // 兼容旧版动作记录格式（无动作时间戳字段）
                                var action = new WeldActionRecord
                                {
                                    FrameIndex = reader.ReadInt32(),
                                    Timestamp = DateTime.FromBinary(reader.ReadInt64()),
                                    ActionType = reader.ReadString(),
                                    AdjustData = reader.ReadString()
                                };
                                _actionRecords.Add(action);
                            }
                        }
                    }

                    _frameIndex = _frames.Count > 0 ? _frames.Max(f => f.Index) + 1 : 0;
                    return true;
                }
                catch
                {
                    _frames.Clear();
                    _actionRecords.Clear();
                    return false;
                }
            }
        }

        /// <summary>
        /// 将临时文件转换为正常点云PLY和pDat
        /// </summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <param name="outputName">输出文件名（不含扩展名）</param>
        /// <returns>转换后的文件路径列表</returns>
        public List<string> ConvertTempToNormal(string tempFilePath, string outputName)
        {
            var result = new List<string>();
            if (!ParseFromTempFile(tempFilePath)) return result;

            EnsureDirectoriesExist();
            // 恢复文件同样按当前日期归档到 yyyyMMdd 子目录
            string dateDir = EnsureDateRecordDirectory(DateTime.Now);
            string baseName = string.IsNullOrEmpty(outputName)
                ? string.Format("Recovered_{0:yyyyMMdd_HHmmss}", DateTime.Now)
                : outputName;

            string plyPath = Path.Combine(dateDir, baseName + ".ply");
            SaveFramesToPly(plyPath);
            result.Add(plyPath);

            string pDatPath = Path.Combine(dateDir, baseName + ".pDat");
            SaveActionsToPDat(pDatPath);
            result.Add(pDatPath);

            return result;
        }

        #endregion

        #region 数据流读写辅助

        /// <summary>
        /// 将点云数据写入二进制流
        /// </summary>
        private void WritePointCloudToStream(BinaryWriter writer, PointCloudData data)
        {
            bool hasColor = data.HasColor;
            writer.Write(hasColor);
            writer.Write(data.PointCount);

            if (hasColor)
            {
                foreach (var point in data.ColoredPoints)
                {
                    writer.Write(point.X);
                    writer.Write(point.Y);
                    writer.Write(point.Z);
                    writer.Write(point.R);
                    writer.Write(point.G);
                    writer.Write(point.B);
                    writer.Write(point.A);
                }
            }
            else
            {
                foreach (var point in data.Points)
                {
                    writer.Write(point.X);
                    writer.Write(point.Y);
                    writer.Write(point.Z);
                }
            }
        }

        /// <summary>
        /// 从二进制流读取点云数据
        /// </summary>
        private PointCloudData ReadPointCloudFromStream(BinaryReader reader)
        {
            var data = new PointCloudData();
            bool hasColor = reader.ReadBoolean();
            int count = reader.ReadInt32();

            if (hasColor)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    byte r = reader.ReadByte();
                    byte g = reader.ReadByte();
                    byte b = reader.ReadByte();
                    byte a = reader.ReadByte();
                    data.AddPoint(x, y, z, r, g, b, a);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    data.AddPoint(x, y, z);
                }
            }

            return data;
        }

        /// <summary>
        /// 将动作记录写入二进制流（新版格式，version=2）
        /// </summary>
        private void WriteActionToStream(BinaryWriter writer, WeldActionRecord action)
        {
            writer.Write(action.FrameIndex);
            writer.Write(action.Timestamp.ToBinary());
            writer.Write(action.ActionType ?? string.Empty);
            writer.Write(action.RobotX);
            writer.Write(action.RobotY);
            writer.Write(action.RobotZ);
            writer.Write(action.RobotSpeed);
            writer.Write(action.FeedSpeed);
            writer.Write(action.LaserPower);
            writer.Write(action.WeldWidth);
            writer.Write(action.AdjustExecuted);
            writer.Write(action.AdjustData ?? string.Empty);
        }

        /// <summary>
        /// 从二进制流读取动作记录（新版格式，version=2）
        /// </summary>
        private WeldActionRecord ReadActionFromStream(BinaryReader reader)
        {
            var action = new WeldActionRecord();
            action.FrameIndex = reader.ReadInt32();
            action.Timestamp = DateTime.FromBinary(reader.ReadInt64());
            action.ActionType = reader.ReadString();
            action.RobotX = reader.ReadDouble();
            action.RobotY = reader.ReadDouble();
            action.RobotZ = reader.ReadDouble();
            action.RobotSpeed = reader.ReadDouble();
            action.FeedSpeed = reader.ReadDouble();
            action.LaserPower = reader.ReadDouble();
            action.WeldWidth = reader.ReadDouble();
            action.AdjustExecuted = reader.ReadBoolean();
            action.AdjustData = reader.ReadString();
            return action;
        }

        #endregion

        #region 数据转换

        /// <summary>
        /// 克隆点云数据
        /// </summary>
        private PointCloudData ClonePointCloud(PointCloudData source)
        {
            if (source == null) return null;

            var clone = new PointCloudData();
            if (source.HasColor)
            {
                foreach (var point in source.ColoredPoints)
                {
                    clone.AddPoint(point);
                }
            }
            else
            {
                foreach (var point in source.Points)
                {
                    clone.AddPoint(point);
                }
            }
            return clone;
        }

        /// <summary>
        /// 获取所有缓存的帧
        /// </summary>
        public List<RecordFrame> GetAllFrames()
        {
            lock (_lockObj)
            {
                return _frames.Select(f => new RecordFrame
                {
                    Index = f.Index,
                    Timestamp = f.Timestamp,
                    PointCloud = ClonePointCloud(f.PointCloud)
                }).ToList();
            }
        }

        /// <summary>
        /// 获取所有动作记录
        /// </summary>
        public List<WeldActionRecord> GetAllActions()
        {
            lock (_lockObj)
            {
                return _actionRecords.Select(a => new WeldActionRecord
                {
                    FrameIndex = a.FrameIndex,
                    Timestamp = a.Timestamp,
                    ActionType = a.ActionType,
                    RobotX = a.RobotX,
                    RobotY = a.RobotY,
                    RobotZ = a.RobotZ,
                    RobotSpeed = a.RobotSpeed,
                    FeedSpeed = a.FeedSpeed,
                    LaserPower = a.LaserPower,
                    WeldWidth = a.WeldWidth,
                    AdjustExecuted = a.AdjustExecuted,
                    AdjustData = a.AdjustData
                }).ToList();
            }
        }

        /// <summary>
        /// 获取指定帧的点云
        /// </summary>
        public PointCloudData GetFramePointCloud(int frameIndex)
        {
            lock (_lockObj)
            {
                var frame = _frames.FirstOrDefault(f => f.Index == frameIndex);
                return frame != null ? ClonePointCloud(frame.PointCloud) : null;
            }
        }

        #endregion

        #region 混淆/反混淆

        /// <summary>
        /// 简单字节混淆（XOR）
        /// </summary>
        private void ObfuscateBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            byte key = 0xA7;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key;
                key = (byte)((key + 0x3D) & 0xFF);
            }
        }

        /// <summary>
        /// 反混淆字节（XOR是对合运算，同一函数即可）
        /// </summary>
        private void DeobfuscateBytes(byte[] data)
        {
            ObfuscateBytes(data);
        }

        /// <summary>
        /// 读取pDat文件内容（自动处理混淆）
        /// </summary>
        public string ReadPDatFile(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            byte[] bytes = File.ReadAllBytes(filePath);

            // 尝试直接作为UTF8读取
            string directText = Encoding.UTF8.GetString(bytes);
            if (directText.StartsWith("# Weld Action Record"))
            {
                return directText;
            }

            // 尝试反混淆
            DeobfuscateBytes(bytes);
            string deobfText = Encoding.UTF8.GetString(bytes);
            if (deobfText.StartsWith("# Weld Action Record"))
            {
                return deobfText;
            }

            return string.Empty;
        }

        /// <summary>
        /// 解析pDat文件为结构化记录列表（自动处理混淆），供 DataReview 回溯显示。
        /// 兼容旧格式（FrameIndex|Timestamp|ActionType|Parameter）。
        /// </summary>
        public List<WeldActionRecord> ParsePDatRecords(string filePath)
        {
            var result = new List<WeldActionRecord>();
            string content = ReadPDatFile(filePath);
            if (string.IsNullOrEmpty(content)) return result;

            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                if (raw.StartsWith("#")) continue;
                string[] parts = raw.Split('|');
                if (parts.Length < 4) continue;

                var rec = new WeldActionRecord();
                try
                {
                    rec.FrameIndex = int.Parse(parts[0]);
                    rec.Timestamp = DateTime.Parse(parts[1]);
                    rec.ActionType = parts[2];

                    if (parts.Length >= 12)
                    {
                        rec.RobotX = double.Parse(parts[3]);
                        rec.RobotY = double.Parse(parts[4]);
                        rec.RobotZ = double.Parse(parts[5]);
                        rec.RobotSpeed = double.Parse(parts[6]);
                        rec.FeedSpeed = double.Parse(parts[7]);
                        rec.LaserPower = double.Parse(parts[8]);
                        rec.WeldWidth = double.Parse(parts[9]);
                        rec.AdjustExecuted = parts[10] == "1";
                        rec.AdjustData = parts[11].Replace("/", "|");
                    }
                    else
                    {
                        rec.AdjustExecuted = string.Equals(rec.ActionType, "WeldParamAdjust", StringComparison.OrdinalIgnoreCase);
                        rec.AdjustData = parts.Length > 3 ? parts[3].Replace("/", "|") : string.Empty;
                    }

                    result.Add(rec);
                }
                catch
                {
                    // 跳过无法解析的行
                }
            }

            return result;
        }

        #endregion

        #region 临时文件列表

        /// <summary>
        /// 获取所有临时文件列表
        /// </summary>
        public List<string> GetTempFiles()
        {
            EnsureDirectoriesExist();
            if (!Directory.Exists(_tempDirectory)) return new List<string>();

            return Directory.GetFiles(_tempDirectory, "TempRecord_*.tmp")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
        }

        /// <summary>
        /// 获取所有记录文件列表
        /// </summary>
        public List<string> GetRecordFiles()
        {
            EnsureDirectoriesExist();
            if (!Directory.Exists(_recordDirectory)) return new List<string>();

            // 记录已按 yyyyMMdd 子目录归档，递归检索兼容历史扁平文件与新子目录
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(_recordDirectory, "*.ply", SearchOption.AllDirectories));
            return files.OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        /// <summary>
        /// 获取所有焊接动作记录(pDat)文件（按创建时间倒序，递归所有日期子目录）
        /// </summary>
        public List<string> GetWeldActionRecordFiles()
        {
            EnsureDirectoriesExist();
            if (!Directory.Exists(_recordDirectory)) return new List<string>();

            return Directory.GetFiles(_recordDirectory, "*.pDat", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        /// <summary>
        /// 按生产日期获取焊接动作记录(pDat)文件（用于 DataReview 日期回溯）
        /// 依据所选日期索引 yyyyMMdd 子目录，目录不存在时返回空列表
        /// </summary>
        public List<string> GetWeldActionRecordFiles(DateTime date)
        {
            string dateDir = GetDateRecordDirectoryPath(date);
            if (!Directory.Exists(dateDir)) return new List<string>();

            return Directory.GetFiles(dateDir, "*.pDat")
                .OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        #endregion
    }
}
