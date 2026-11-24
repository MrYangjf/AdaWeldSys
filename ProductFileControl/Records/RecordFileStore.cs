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
    /// <summary>记录帧数据结构</summary>
    public class RecordFrame
    {
        /// <summary>帧索引</summary>
        public int Index { get; set; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>点云数据</summary>
        public PointCloudData PointCloud { get; set; }
    }

    /// <summary>记录文件存储，模块内部实现，由 Manager 独占持有。 承担记</summary>
    internal sealed class RecordFileStore
    {
        /// <summary>动作记录格式版本（当前 v4，20 列）</summary>
        private const int ActionFormatVersion = 4;

        /// <summary>动作记录 CSV 列名</summary>
        private const string ActionCsvHeader =
            "FrameIndex|Timestamp|ActionType|RobotX|RobotY|RobotZ|RobotSpeed|FeedSpeed|LaserPower|WeldWidth" +
            "|AdjustExecuted|AdjustData|JobId|SeamPointY|SeamPointZ" +
            "|TaPositionMm|LaPositionMm|GalvoAmplitudeMm|GalvoPatternId|HeadTempC";

        private readonly object _lockObj = new object();
        private readonly List<RecordFrame> _frames = new List<RecordFrame>();
        private readonly List<WeldActionRecord> _actionRecords = new List<WeldActionRecord>();
        private string _tempFilePath = string.Empty;
        private string _recordDirectory;
        private string _tempDirectory;
        private bool _isSessionOpen = false;
        private bool _useObfuscation;
        private int _frameIndex = 0;

        /// <summary>构造函数</summary>
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

        /// <summary>是否会话进行中</summary>
        public bool IsSessionOpen
        {
            get { return _isSessionOpen; }
        }

        /// <summary>当前帧索引</summary>
        public int CurrentFrameIndex
        {
            get { return _frameIndex; }
        }

        /// <summary>已缓存的帧数量</summary>
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

        /// <summary>记录目录</summary>
        public string RecordDirectory
        {
            get { return _recordDirectory; }
            set { _recordDirectory = value; }
        }

        /// <summary>临时文件目录</summary>
        public string TempDirectory
        {
            get { return _tempDirectory; }
            set { _tempDirectory = value; }
        }

        /// <summary>是否混淆（已关闭，pDat 恒明文）；仅为兼容保留</summary>
        public bool UseObfuscation
        {
            get { return _useObfuscation; }
            set { _useObfuscation = value; }
        }

        #endregion

        #region 目录管理

        /// <summary>获取指定日期的记录子目录</summary>
        /// <param name="date">记录日期</param>
        /// <returns>记录目录路径（不存在时创建）</returns>
        private string EnsureDateRecordDirectory(DateTime date)
        {
            string dir = GetDateRecordDirectoryPath(date);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        /// <summary>一次会话的归档子目录（以基名命名，内含该会话 .ply/.pDat）</summary>
        /// <returns>结果文本。</returns>
        private string EnsureSessionDirectory(string dateDir, string baseName)
        {
            string dir = Path.Combine(dateDir, baseName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>确保目录存在</summary>
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

        /// <summary>获取指定日期的记录子目录路径</summary>
        /// <param name="date">记录日期</param>
        /// <returns>按 yyyyMMdd 组织的记录目录路径（不创建目录）</returns>
        public string GetDateRecordDirectoryPath(DateTime date)
        {
            return Path.Combine(_recordDirectory, date.ToString("yyyyMMdd"));
        }

        #endregion

        #region 会话控制

        /// <summary>开始记录会话</summary>
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

        /// <summary>正常结束会话（删除临时文件）</summary>
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

        /// <summary>异常中止会话（保留临时文件以便追溯）</summary>
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

        /// <summary>向动作记录列表追加一条记录（线程安全）</summary>
        /// <param name="record">动作记录条目</param>
        private void AddRecord(WeldActionRecord record)
        {
            if (!_isSessionOpen) return;

            lock (_lockObj)
            {
                _actionRecords.Add(record);
            }
        }

        /// <summary>添加一帧点云数据</summary>
        /// <param name="robotXOffset">点云 X 统一偏移到机器人 X 的偏移量（毫米）</param>
        /// <param name="pointCloud">点云数据</param>
        public void AddFrame(PointCloudData pointCloud, double robotXOffset = 0.0)
        {
            if (pointCloud == null) throw new ArgumentNullException(nameof(pointCloud));
            if (!_isSessionOpen) return;

            lock (_lockObj)
            {
                var frame = new RecordFrame
                {
                    Index = _frameIndex++,
                    Timestamp = DateTime.Now,
                    PointCloud = ClonePointCloud(pointCloud, robotXOffset)
                };
                _frames.Add(frame);

                // 后台写入临时文件
                AppendToTempFile(frame);
            }
        }

        /// <summary>记录一帧工艺过程快照</summary>
        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        /// <param name="robotSpeed">机器人运动速度</param>
        /// <param name="feedSpeed">送丝速度（工艺查表输出值）</param>
        /// <param name="laserPower">激光功率（工艺查表输出值）</param>
        /// <param name="weldWidth">焊缝宽度（毫米）</param>
        /// <param name="jobId">线激光分析 Job 编号</param>
        /// <param name="seamPointY">线激光焊缝特征点横向坐标（毫米）</param>
        /// <param name="seamPointZ">线激光焊缝特征点高度坐标（毫米）</param>
        /// <param name="taPositionMm">TA 送丝 Z 轴位置（毫米）</param>
        /// <param name="laPositionMm">LA 对焦轴位置（毫米）</param>
        /// <param name="galvoAmplitudeMm">振镜摆幅（毫米）</param>
        /// <param name="galvoPatternId">振镜图形编号</param>
        /// <param name="headTempC">激光头温度（摄氏度）</param>
        public void AddProcessRecord(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth,
            int jobId, double seamPointY, double seamPointZ,
            double taPositionMm = 0.0, double laPositionMm = 0.0,
            double galvoAmplitudeMm = 0.0, int galvoPatternId = 0, double headTempC = 0.0)
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
                JobId = jobId,
                SeamPointY = seamPointY,
                SeamPointZ = seamPointZ,
                TaPositionMm = taPositionMm,
                LaPositionMm = laPositionMm,
                GalvoAmplitudeMm = galvoAmplitudeMm,
                GalvoPatternId = galvoPatternId,
                HeadTempC = headTempC,
                AdjustExecuted = false,
                AdjustData = string.Empty
            });
        }

        /// <summary>记录一次工艺参数调整</summary>
        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        /// <param name="robotSpeed">机器人运动速度</param>
        /// <param name="feedSpeed">送丝速度（工艺查表输出值）</param>
        /// <param name="laserPower">激光功率（工艺查表输出值）</param>
        /// <param name="weldWidth">焊缝宽度（毫米）</param>
        /// <param name="adjustData">调整明细（调整前后参数对比文本）</param>
        public void AddAdjustRecord(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth,
            int jobId, double seamPointY, double seamPointZ, string adjustData)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = _frameIndex,
                Timestamp = DateTime.Now,
                ActionType = "ProcessOutputTrigger",
                RobotX = robotX,
                RobotY = robotY,
                RobotZ = robotZ,
                RobotSpeed = robotSpeed,
                FeedSpeed = feedSpeed,
                LaserPower = laserPower,
                WeldWidth = weldWidth,
                JobId = jobId,
                SeamPointY = seamPointY,
                SeamPointZ = seamPointZ,
                AdjustExecuted = true,
                AdjustData = adjustData ?? string.Empty
            });
        }

        /// <summary>记录一次线激光分析工具切换</summary>
        /// <param name="robotX">机器人 X 坐标（毫米）</param>
        /// <param name="robotY">机器人 Y 坐标（毫米）</param>
        /// <param name="robotZ">机器人 Z 坐标（毫米）</param>
        /// <param name="robotSpeed">机器人运动速度</param>
        /// <param name="feedSpeed">送丝速度（工艺查表输出值）</param>
        /// <param name="laserPower">激光功率（工艺查表输出值）</param>
        /// <param name="weldWidth">焊缝宽度（毫米）</param>
        /// <param name="jobId">切换到的线激光分析 Job 编号</param>
        /// <param name="adjustData">切换说明文本</param>
        /// <param name="robotX">机器人 X 坐标（保证切换点按机器人 X 对齐）</param>
        /// <param name="robotY">机器人 Y 坐标</param>
        /// <param name="robotZ">机器人 Z 坐标</param>
        /// <param name="robotSpeed">机器人运动速度</param>
        /// <param name="feedSpeed">当时送丝速度（查表输出）</param>
        /// <param name="laserPower">当时激光功率（查表输出）</param>
        /// <param name="weldWidth">当时焊缝宽度</param>
        /// <param name="jobId">切换到的线激光分析 Job 编号</param>
        /// <param name="adjustData">切换明细</param>
        public void AddJobSwitchRecord(double robotX, double robotY, double robotZ,
            double robotSpeed, double feedSpeed, double laserPower, double weldWidth,
            int jobId, double seamPointY, double seamPointZ, string adjustData)
        {
            AddRecord(new WeldActionRecord
            {
                FrameIndex = _frameIndex,
                Timestamp = DateTime.Now,
                ActionType = "JobSwitch",
                RobotX = robotX,
                RobotY = robotY,
                RobotZ = robotZ,
                RobotSpeed = robotSpeed,
                FeedSpeed = feedSpeed,
                LaserPower = laserPower,
                WeldWidth = weldWidth,
                JobId = jobId,
                SeamPointY = seamPointY,
                SeamPointZ = seamPointZ,
                AdjustExecuted = true,
                AdjustData = adjustData ?? string.Empty
            });
        }

        #endregion

        #region 文件保存

        /// <summary>保存所有帧合并为一个PLY文件</summary>
        /// <param name="filePath">点云文件保存路径</param>
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

        /// <summary>保存动作记录到pDat文件</summary>
        /// <param name="filePath">动作记录文件保存路径</param>
        private void SaveActionsToPDat(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Weld Action Record");
            sb.AppendLine($"# FrameCount: {_frames.Count}");
            sb.AppendLine($"# ActionCount: {_actionRecords.Count}");
            sb.AppendLine("# Format: " + ActionCsvHeader);
            sb.AppendLine();

            foreach (var action in _actionRecords)
            {
                string timestampStr = action.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string adjustFlag = action.AdjustExecuted ? "1" : "0";
                string adjustData = (action.AdjustData ?? string.Empty).Replace("|", "/");
                sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}|{19}",
                    action.FrameIndex, timestampStr, action.ActionType,
                    action.RobotX, action.RobotY, action.RobotZ,
                    action.RobotSpeed, action.FeedSpeed, action.LaserPower, action.WeldWidth,
                    adjustFlag, adjustData, action.JobId, action.SeamPointY, action.SeamPointZ,
                    action.TaPositionMm, action.LaPositionMm, action.GalvoAmplitudeMm,
                    action.GalvoPatternId, action.HeadTempC));
            }

            // pDat 以明文落盘，可直读；读取仍兼容旧混淆
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>保存记录到正式文件</summary>
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

                // 一次会话三份文件归入以基名命名的子文件夹
                string sessionDir = EnsureSessionDirectory(dateDir, baseName);

                string plyPath = Path.Combine(sessionDir, baseName + ".ply");
                SaveFramesToPly(plyPath);
                savedFiles.Add(plyPath);

                string pDatPath = Path.Combine(sessionDir, baseName + ".pDat");
                SaveActionsToPDat(pDatPath);
                savedFiles.Add(pDatPath);

                // 已落盘：释放内存缓存，避免长时运行上涨
                _frames.Clear();
                _actionRecords.Clear();

                return savedFiles;
            }
        }

        #endregion

        #region 临时文件处理

        /// <summary>保存临时文件（内部调用）</summary>
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
                    writer.Write((int)ActionFormatVersion); // 动作记录格式版本

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

        /// <summary>追加帧到临时文件</summary>
        /// <param name="frame">待追加的记录帧</param>
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
                        writer.Write((int)ActionFormatVersion); // 版本占位
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

        /// <summary>从临时文件解析记录</summary>
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
                                _actionRecords.Add(ReadActionFromStream(reader, version));
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

        /// <summary>将临时文件转换为正常点云PLY和pDat</summary>
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

            string sessionDir = EnsureSessionDirectory(dateDir, baseName);

            string plyPath = Path.Combine(sessionDir, baseName + ".ply");
            SaveFramesToPly(plyPath);
            result.Add(plyPath);

            string pDatPath = Path.Combine(sessionDir, baseName + ".pDat");
            SaveActionsToPDat(pDatPath);
            result.Add(pDatPath);

            return result;
        }

        #endregion

        #region 数据流读写辅助

        /// <summary>将点云数据写入二进制流</summary>
        /// <param name="writer">二进制写入器</param>
        /// <param name="data">待写入的点云</param>
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

        /// <summary>从二进制流读取点云数据</summary>
        /// <param name="reader">二进制读取器</param>
        /// <returns>读取到的点云（格式异常时返回 null）</returns>
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

        /// <summary>将动作记录写入二进制流（新版格式，version >= 2）</summary>
        /// <param name="writer">二进制写入器</param>
        /// <param name="action">待写入的动作记录</param>
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
            writer.Write(action.JobId);   // v3 追加字段
            writer.Write(action.SeamPointY);   // v4 补齐：此前二进制侧漏写，仅 CSV 有
            writer.Write(action.SeamPointZ);
            writer.Write(action.TaPositionMm);     // v4 追加：TA 送丝 Z 轴位置
            writer.Write(action.LaPositionMm);     // v4 追加：LA 对焦轴位置
            writer.Write(action.GalvoAmplitudeMm);  // v4 追加：振镜摆幅
            writer.Write(action.GalvoPatternId);    // v4 追加：振镜图形
            writer.Write(action.HeadTempC);         // v4 追加：激光头温度
        }

        /// <summary>从二进制流读取动作记录（新版格式，version >= 2；>= 3 含 JobId；>= 4 含线激光</summary>

        /// <returns>读取到的动作记录（格式异常时返回 null）</returns>
        /// <param name="reader">二进制读取器</param>
        /// <param name="version">文件头登记的格式版本</param>
        private WeldActionRecord ReadActionFromStream(BinaryReader reader, int version)
        {
            var action = new WeldActionRecord
            {
                FrameIndex = reader.ReadInt32(),
                Timestamp = DateTime.FromBinary(reader.ReadInt64()),
                ActionType = reader.ReadString(),
                RobotX = reader.ReadDouble(),
                RobotY = reader.ReadDouble(),
                RobotZ = reader.ReadDouble(),
                RobotSpeed = reader.ReadDouble(),
                FeedSpeed = reader.ReadDouble(),
                LaserPower = reader.ReadDouble(),
                WeldWidth = reader.ReadDouble(),
                AdjustExecuted = reader.ReadBoolean(),
                AdjustData = reader.ReadString(),
                JobId = version >= 3 ? reader.ReadInt32() : 0
            };
            if (version >= 4)
            {
                action.SeamPointY = reader.ReadDouble();
                action.SeamPointZ = reader.ReadDouble();
                action.TaPositionMm = reader.ReadDouble();
                action.LaPositionMm = reader.ReadDouble();
                action.GalvoAmplitudeMm = reader.ReadDouble();
                action.GalvoPatternId = reader.ReadInt32();
                action.HeadTempC = reader.ReadDouble();
            }
            return action;
        }

        #endregion

        #region 数据转换

        /// <summary>克隆点云数据</summary>
        /// <param name="source">源点云</param>
        /// <param name="xOffset">克隆后统一叠加的 X 偏移（毫米）</param>
        /// <returns>克隆后的点云（源为 null 时返回 null）</returns>
        private PointCloudData ClonePointCloud(PointCloudData source, double xOffset = 0.0)
        {
            if (source == null) return null;

            var clone = new PointCloudData();
            if (source.HasColor)
            {
                foreach (var point in source.ColoredPoints)
                {
                    clone.AddPoint((float)(point.X + xOffset), point.Y, point.Z, point.R, point.G, point.B, point.A);
                }
            }
            else
            {
                foreach (var point in source.Points)
                {
                    clone.AddPoint((float)(point.X + xOffset), point.Y, point.Z);
                }
            }
            return clone;
        }

        /// <summary>获取所有缓存的帧</summary>
        /// <returns>全部记录帧的副本</returns>
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

        /// <summary>获取所有动作记录</summary>
        /// <returns>全部动作记录的副本</returns>
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
                    JobId = a.JobId,
                    SeamPointY = a.SeamPointY,
                    SeamPointZ = a.SeamPointZ,
                    AdjustExecuted = a.AdjustExecuted,
                    AdjustData = a.AdjustData
                }).ToList();
            }
        }

        /// <summary>获取指定帧的点云</summary>
        /// <param name="frameIndex">帧序号</param>
        /// <returns>该帧的点云（序号越界时返回 null）</returns>
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

        /// <summary>简单字节混淆（XOR）</summary>
        /// <param name="data">待混淆的字节数组</param>
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

        /// <summary>读取pDat文件内容（自动处理混淆）</summary>
        /// <param name="filePath">动作记录文件路径</param>
        /// <returns>文件文本内容（文件不存在时返回空串）</returns>
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
            ObfuscateBytes(bytes);
            string deobfText = Encoding.UTF8.GetString(bytes);
            if (deobfText.StartsWith("# Weld Action Record"))
            {
                return deobfText;
            }

            return string.Empty;
        }

        /// <summary>解析动作记录文件 兼容旧格式（FrameIndex|Timestamp|ActionType|Parameter）。</summary>
        /// <param name="filePath">动作记录文件路径</param>
        /// <returns>动作记录列表（文件缺失或解析失败时为空列表）</returns>
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
                        // 13 列起含 JobId（格式 v3）；14/15 列含焊缝特征点（SeamPointY/Z）；
                        // 16~20 列为 v4 追加，逐列回落兼容旧文件
                        rec.JobId = parts.Length >= 13 ? int.Parse(parts[12]) : 0;
                        rec.SeamPointY = parts.Length >= 14 ? double.Parse(parts[13]) : 0;
                        rec.SeamPointZ = parts.Length >= 15 ? double.Parse(parts[14]) : 0;
                        rec.TaPositionMm = parts.Length >= 16 ? double.Parse(parts[15]) : 0;
                        rec.LaPositionMm = parts.Length >= 17 ? double.Parse(parts[16]) : 0;
                        rec.GalvoAmplitudeMm = parts.Length >= 18 ? double.Parse(parts[17]) : 0;
                        rec.GalvoPatternId = parts.Length >= 19 ? int.Parse(parts[18]) : 0;
                        rec.HeadTempC = parts.Length >= 20 ? double.Parse(parts[19]) : 0;
                    }
                    else
                    {
                        rec.AdjustExecuted = string.Equals(rec.ActionType, "WeldParamAdjust", StringComparison.OrdinalIgnoreCase) || string.Equals(rec.ActionType, "ProcessOutputTrigger", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>获取所有临时文件列表</summary>
        /// <returns>临时记录文件路径列表</returns>
        public List<string> GetTempFiles()
        {
            EnsureDirectoriesExist();
            if (!Directory.Exists(_tempDirectory)) return new List<string>();

            return Directory.GetFiles(_tempDirectory, "TempRecord_*.tmp")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
        }

        /// <summary>获取所有记录文件列表</summary>
        /// <returns>正式记录文件路径列表</returns>
        public List<string> GetRecordFiles()
        {
            EnsureDirectoriesExist();
            if (!Directory.Exists(_recordDirectory)) return new List<string>();

            // 按日归档，递归检索兼容历史扁平文件
            return Directory.GetFiles(_recordDirectory, "*.ply", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        /// <summary>列出全部动作记录文件</summary>
        /// <returns>动作记录文件路径列表</returns>
        public List<string> GetWeldActionRecordFiles()
        {
            EnsureDirectoriesExist();
            if (!Directory.Exists(_recordDirectory)) return new List<string>();

            return Directory.GetFiles(_recordDirectory, "*.pDat", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        /// <summary>列出指定日期的动作记录文件 依据所选日期索</summary>

        /// <param name="date">记录日期</param>
        /// <returns>该日期下的动作记录文件路径列表</returns>
        public List<string> GetWeldActionRecordFiles(DateTime date)
        {
            string dateDir = GetDateRecordDirectoryPath(date);
            if (!Directory.Exists(dateDir)) return new List<string>();

            return Directory.GetFiles(dateDir, "*.pDat", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        /// <summary>按保留天数清理过期会话文件夹与残留平铺文</summary>

        /// <returns>结果码。</returns>
        public int CleanupExpiredRecords(int retainDays)
        {
            int removed = 0;
            if (retainDays <= 0 || !Directory.Exists(_recordDirectory)) return 0;
            DateTime cutoff = DateTime.Now.AddDays(-retainDays);
            try
            {
                foreach (string dateDir in Directory.GetDirectories(_recordDirectory))
                {
                    try
                    {
                        foreach (string sd in Directory.GetDirectories(dateDir))
                        {
                            try { if (Directory.GetCreationTime(sd) < cutoff) { Directory.Delete(sd, true); removed++; } }
                            catch { }   // 过期文件清理：被占用/已删除属常态，失败不影响记录写入
                        }
                        foreach (string f in Directory.GetFiles(dateDir))
                        {
                            try { if (File.GetCreationTime(f) < cutoff) { File.Delete(f); removed++; } }
                            catch { }   // 同上级目录清理：失败不影响记录写入
                        }
                        if (Directory.GetDirectories(dateDir).Length == 0 && Directory.GetFiles(dateDir).Length == 0)
                            Directory.Delete(dateDir, false);
                    }
                    catch { }   // 同上：清理是尽力而为
                }
            }
            catch { }   // 清理整体失败不影响本轮记录写入
            return removed;
        }

        #endregion
    }
}
