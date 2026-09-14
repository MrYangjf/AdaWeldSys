using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.PCLOperate.DataIO;
using AdaWeldSystem.PCLOperate.Models;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>
    /// 焊缝追踪记录类（实现层）
    /// 记录内容 = 焊缝结果信息 + 机器人信息（过程帧七项快照）+ 点云帧；并承载 .ply/.pDat/.tmp 会话与回溯。
    /// 承接 WeldParamManager.FrameRecordRequested / AbortRecordRequested 事件（经 Manager 透传，ADR-043）。
    /// </summary>
    public class SeamTrackRecord : ProductRecordBase
    {
        private readonly RecordFileStore _store;

        /// <summary>
        /// 记录名称
        /// </summary>
        public override string Name
        {
            get { return "SeamTrackRecord"; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="store">记录文件存储（Manager 独占持有并注入）</param>
        internal SeamTrackRecord(RecordFileStore store)
        {
            _store = store;
        }

        #region 记录（事件驱动 + 点云帧）

        /// <summary>
        /// 记录一帧焊缝追踪数据（焊缝结果 + 机器人信息；入队即返回，后台落盘）
        /// </summary>
        /// <param name="item">焊缝追踪条目</param>
        public void RecordWeldFrame(SeamTrackItem item)
        {
            Enqueue(item);
        }

        /// <summary>
        /// 添加一帧点云数据（线激光/监控相机路径）
        /// </summary>
        /// <param name="pointCloud">点云数据</param>
        public void AddPointCloud(PointCloudData pointCloud)
        {
            if (pointCloud == null)
            {
                return;
            }
            Enqueue(new SeamTrackItem { PointCloud = pointCloud });
        }

        /// <summary>
        /// 单条落盘（后台线程调用）
        /// </summary>
        /// <param name="item">记录条目</param>
        protected override void Persist(RecordItem item)
        {
            var track = item as SeamTrackItem;
            if (track == null)
            {
                return;
            }
            if (track.PointCloud != null)
            {
                _store.AddFrame(track.PointCloud);
            }
            if (track.HasWeldSnapshot)
            {
                _store.AddProcessRecord(track.RobotX, track.RobotY, track.RobotZ,
                    track.RobotSpeed, track.FeedSpeed, track.LaserPower, track.WeldWidth);
            }
        }

        #endregion

        #region 会话收尾（委托存储）

        /// <summary>
        /// 会话开始
        /// </summary>
        protected override void OnStart()
        {
            _store.StartSession();
        }

        /// <summary>
        /// 会话正常结束
        /// </summary>
        protected override void OnStop()
        {
            _store.StopSession();
        }

        /// <summary>
        /// 会话异常中止（保留临时文件）
        /// </summary>
        protected override void OnAbort()
        {
            _store.AbortSession();
        }

        #endregion

        #region 回溯（转发存储）

        /// <summary>
        /// 保存记录到正式文件（.ply + .pDat，按日期归档）
        /// </summary>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>保存的文件路径列表</returns>
        public List<string> SaveRecord(string fileName)
        {
            return _store.SaveRecord(fileName);
        }

        /// <summary>
        /// 获取所有缓存的帧
        /// </summary>
        /// <returns>帧列表</returns>
        public List<RecordFrame> GetAllFrames()
        {
            return _store.GetAllFrames();
        }

        /// <summary>
        /// 获取指定帧的点云
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>点云数据</returns>
        public PointCloudData GetFramePointCloud(int frameIndex)
        {
            return _store.GetFramePointCloud(frameIndex);
        }

        /// <summary>
        /// 从临时文件解析记录
        /// </summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <returns>解析是否成功</returns>
        public bool ParseFromTempFile(string tempFilePath)
        {
            return _store.ParseFromTempFile(tempFilePath);
        }

        /// <summary>
        /// 将临时文件转换为正常点云 PLY 和 pDat
        /// </summary>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <param name="outputName">输出文件名（不含扩展名）</param>
        /// <returns>转换后的文件路径列表</returns>
        public List<string> ConvertTempToNormal(string tempFilePath, string outputName)
        {
            return _store.ConvertTempToNormal(tempFilePath, outputName);
        }

        #endregion
    }

    /// <summary>
    /// 记录帧数据结构
    /// </summary>
    public class RecordFrame
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 点云数据
        /// </summary>
        public PointCloudData PointCloud { get; set; }
    }

    /// <summary>
    /// 记录文件存储（模块内部实现，由 Manager 独占持有）
    /// 承担过程帧 / 动作记录的内存缓存与 .ply / .pDat / .tmp 文件机制（含按日期归档与混淆）。
    /// 本类只做文件机制，不做记录业务编排（会话语义与事件订阅归记录类与 Manager）。
    /// </summary>
    internal sealed class RecordFileStore
    {
        private readonly object _lockObj = new object();
        private readonly List<RecordFrame> _frames = new List<RecordFrame>();
        private readonly List<WeldActionRecord> _actionRecords = new List<WeldActionRecord>();
        private string _tempFilePath = string.Empty;
        private string _recordDirectory;
        private string _tempDirectory;
        private bool _isSessionOpen = false;
        private bool _useObfuscation;
        private int _frameIndex = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="recordDirectory">记录目录</param>
        /// <param name="tempDirectory">临时文件目录</param>
        /// <param name="useObfuscation">是否混淆 pDat 文件</param>
        public RecordFileStore(string recordDirectory, string tempDirectory, bool useObfuscation)
        {
            _recordDirectory = recordDirectory;
            _tempDirectory = tempDirectory;
            _useObfuscation = useObfuscation;
        }

        #region 属性

        /// <summary>
        /// 是否会话进行中
        /// </summary>
        public bool IsSessionOpen
        {
            get { return _isSessionOpen; }
        }

        /// <summary>
        /// 当前帧索引
        /// </summary>
        public int CurrentFrameIndex
        {
            get { return _frameIndex; }
        }

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

        /// <summary>
        /// 记录目录
        /// </summary>
        public string RecordDirectory
        {
            get { return _recordDirectory; }
            set { _recordDirectory = value; }
        }

        /// <summary>
        /// 临时文件目录
        /// </summary>
        public string TempDirectory
        {
            get { return _tempDirectory; }
            set { _tempDirectory = value; }
        }

        /// <summary>
        /// 是否使用混淆（pDat文件用TXT打开为乱码）
        /// </summary>
        public bool UseObfuscation
        {
            get { return _useObfuscation; }
            set { _useObfuscation = value; }
        }

        #endregion

        #region 目录管理

        /// <summary>
        /// 确保目录存在
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            if (!string.IsNullOrEmpty(_recordDirectory) && !Directory.Exists(_recordDirectory))
            {
                Directory.CreateDirectory(_recordDirectory);
            }
            if (!string.IsNullOrEmpty(_tempDirectory) && !Directory.Exists(_tempDirectory))
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

        #region 会话控制

        /// <summary>
        /// 开始记录会话
        /// </summary>
        public void StartSession()
        {
            lock (_lockObj)
            {
                if (_isSessionOpen) return;

                EnsureDirectoriesExist();
                _frames.Clear();
                _actionRecords.Clear();
                _frameIndex = 0;
                _isSessionOpen = true;

                string tempFileName = string.Format("TempRecord_{0:yyyyMMdd_HHmmss}.tmp", DateTime.Now);
                _tempFilePath = Path.Combine(_tempDirectory, tempFileName);
            }
        }

        /// <summary>
        /// 正常结束会话（删除临时文件）
        /// </summary>
        public void StopSession()
        {
            lock (_lockObj)
            {
                if (!_isSessionOpen) return;

                _isSessionOpen = false;

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
        /// 异常中止会话（保留临时文件以便追溯）
        /// </summary>
        public void AbortSession()
        {
            lock (_lockObj)
            {
                if (!_isSessionOpen) return;

                _isSessionOpen = false;

                // 异常中断，保存临时文件以便追溯
                if (_frames.Count > 0)
                {
                    SaveTempFileInternal();
                }
            }
        }

        #endregion

        #region 数据添加（由记录类经后台落盘线程调用）

        /// <summary>
        /// 添加一帧点云数据
        /// </summary>
        /// <param name="pointCloud">点云数据</param>
        public void AddFrame(PointCloudData pointCloud)
        {
            if (pointCloud == null) throw new ArgumentNullException(nameof(pointCloud));
            if (!_isSessionOpen) return;

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
        /// 向动作记录列表追加一条过程帧记录（AdjustExecuted=false；帧索引由记录侧自取）
        /// </summary>
        public void AddProcessRecord(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = _frameIndex,
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
        /// 向动作记录列表追加一条调整记录（AdjustExecuted=true），adjustData 携带调整明细
        /// </summary>
        public void AddAdjustRecord(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth, string adjustData)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = _frameIndex,
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
        /// 向动作记录列表追加一条记录（线程安全）
        /// </summary>
        private void AddRecord(WeldActionRecord record)
        {
            if (record == null) return;
            if (!_isSessionOpen) return;

            lock (_lockObj)
            {
                _actionRecords.Add(record);
            }
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
