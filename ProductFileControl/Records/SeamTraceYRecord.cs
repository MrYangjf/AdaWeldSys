using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>焊缝 Y 记录的单拍采样点（入图/出图/轴反馈）</summary>
    public class SeamTraceYPoint
    {
        /// <summary>当前机器人 X（查图中心，折线图统一横轴）</summary>
        public double RobotX { get; set; }

        /// <summary>入图键 X（= 测量时刻 RobotX + 前置量 LeadDistanceMm）</summary>
        public double MapKeyX { get; set; }

        /// <summary>入图绝对目标 Yt（CSPinYt）</summary>
        public double CspInYt { get; set; }

        /// <summary>本拍查图输出 Y</summary>
        public double CspOutY { get; set; }

        /// <summary>焊缝跟踪轴实际反馈位置</summary>
        public double AxisY { get; set; }
    }

    /// <summary>焊缝 Y 记录：三路 Y 逐拍留痕，由 <see cref="ProductFileRecordManager"/> 独占持有。</summary>
    public sealed class SeamTraceYRecord
    {
        private const string Tag = "SeamTraceYRecord";

        /// <summary>CSV 表头（列序与 <see cref="RecordPoint"/> 写入顺序、<see cref="ParseFile"/> 解析顺序三方一致）</summary>
        private const string CsvHeader = "当前RobotX,入图RobotCSPX,CSPinYt,CSPoutY,实时Y";

        private readonly object _lock = new object();
        private readonly List<string> _buffer;
        private string _recordDirectory;
        private string _sessionFile;
        private bool _sessionActive;

        /// <summary>记录根目录（Records），由 Manager 同步维护</summary>
        public string RecordDirectory
        {
            get { lock (_lock) return _recordDirectory; }
            set { lock (_lock) _recordDirectory = value; }
        }

        /// <summary>当前缓存中尚未落盘的点数。</summary>
        public int BufferedCount
        {
            get { lock (_lock) return _buffer.Count; }
        }

        /// <summary>记录会话是否已开启（供 Manager 幂等开启）。</summary>
        public bool IsSessionActive
        {
            get { lock (_lock) return _sessionActive; }
        }

        /// <summary>使用 Manager 持有的记录目录初始化。</summary>
        /// <param name="recordDirectory">记录根目录（Records）</param>
        public SeamTraceYRecord(string recordDirectory)
        {
            _recordDirectory = recordDirectory;
            _buffer = new List<string>();
            _sessionFile = string.Empty;
            _sessionActive = false;
        }

        /// <summary>开始一次记录会话：定路径并写表头</summary>

        /// <param name="sessionBaseName">会话基名（由 Manager 生成，与 .ply/.pDat 同名）</param>
        /// <returns>开启成功返回 true；基名未就绪或创建失败返回 false（不置活跃）</returns>
        public bool BeginSession(string sessionBaseName)
        {
            if (string.IsNullOrEmpty(sessionBaseName))
            {
                GlobalCommData.ShowLog(Tag, "焊缝Y记录会话开启被拒 会话基名未就绪 生产会话尚未开启", MessageLevel.Warning);
                return false;
            }

            lock (_lock)
            {
                _buffer.Clear();
                _sessionFile = string.Empty;
                bool opened = false;
                try
                {
                    string dateDir = Path.Combine(_recordDirectory, DateTime.Now.ToString("yyyyMMdd"));
                    if (!Directory.Exists(dateDir))
                    {
                        Directory.CreateDirectory(dateDir);
                    }
                    // 与 .ply/.pDat 同入以基名命名的会话子文件夹
                    string sessionDir = Path.Combine(dateDir, sessionBaseName);
                    if (!Directory.Exists(sessionDir)) Directory.CreateDirectory(sessionDir);
                    _sessionFile = Path.Combine(sessionDir, sessionBaseName + ".csv");
                    File.WriteAllText(_sessionFile, CsvHeader + "\r\n", Encoding.UTF8);
                    opened = true;
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, "焊缝Y记录会话文件创建失败 " + ex.Message, MessageLevel.Error);
                }
                // 创建失败不置活跃，否则缓存只积不落盘
                _sessionActive = opened;
                return opened;
            }
        }

        /// <summary>记录一拍焊缝 Y（5 列）；会话未开启时静默丢弃</summary>

        /// <param name="robotX">当前机器人 X（毫米，查图中心）</param>
        /// <param name="mapKeyX">入图键 X（毫米 = 测量时刻 RobotX + 前置量；尚无入图点时传 0）</param>
        /// <param name="cspInYt">最近一次入图的绝对目标 Yt（毫米；尚无入图点时传 0）</param>
        /// <param name="cspOutY">本拍查图输出 Y（毫米；查图 X 未及地图首键时 = 标定轴值原值不变）</param>
        /// <param name="axisY">焊缝跟踪轴实际反馈位置（毫米；模拟记录模式传虚拟轴位置）</param>
        public void RecordPoint(double robotX, double mapKeyX, double cspInYt, double cspOutY, double axisY)
        {
            lock (_lock)
            {
                // 会话标志读也须在锁内，避免撕裂值
                if (!_sessionActive) return;

                _buffer.Add(string.Format("{0:F3},{1:F3},{2:F4},{3:F4},{4:F4}",
                    robotX, mapKeyX, cspInYt, cspOutY, axisY));
            }
        }

        /// <summary>将缓存追加写入会话文件并清空缓存（运动终止</summary>
        public void FlushToFile()
        {
            lock (_lock)
            {
                if (_buffer.Count == 0 || string.IsNullOrEmpty(_sessionFile)) return;
                try
                {
                    var sb = new StringBuilder(_buffer.Count * 48);
                    foreach (string line in _buffer)
                    {
                        sb.AppendLine(line);
                    }
                    File.AppendAllText(_sessionFile, sb.ToString(), Encoding.UTF8);
                    _buffer.Clear();
                }
                catch (Exception ex)
                {
                    GlobalCommData.ShowLog(Tag, "焊缝Y记录落盘失败 " + ex.Message, MessageLevel.Error);
                }
            }
        }

        /// <summary>结束会话：落盘并复位（模拟关闭或焊接停止时调</summary>
        public void EndSession()
        {
            FlushToFile();
            lock (_lock)
            {
                _sessionActive = false;
                _sessionFile = string.Empty;
                _buffer.Clear();
            }
        }

        /// <summary>读取一份焊缝 Y 记录 CSV（回溯端一次性读取）</summary>

        /// <param name="filePath">CSV 路径</param>
        /// <returns>按写入顺序排列的记录点列表</returns>
        public static List<SeamTraceYPoint> ParseFile(string filePath)
        {
            var points = new List<SeamTraceYPoint>();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return points;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                foreach (string raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    string[] parts = raw.Split(',');
                    if (parts.Length < 3) continue;

                    if (!double.TryParse(parts[0], out double robotX)) continue;

                    // 旧格式：第 2 列是 RobotY、第 3 列是 CspY（出图）、第 4 列是 AxisY；入图两列无
                    bool newFormat = parts.Length >= 5;
                    if (!double.TryParse(newFormat ? parts[3] : parts[2], out double cspOutY)) continue;

                    double mapKeyX = double.NaN;
                    double cspInYt = double.NaN;
                    if (newFormat)
                    {
                        if (double.TryParse(parts[1], out double parsedKey)) mapKeyX = parsedKey;
                        if (double.TryParse(parts[2], out double parsedIn)) cspInYt = parsedIn;
                    }

                    double axisY = double.NaN;
                    int axisIndex = newFormat ? 4 : 3;
                    if (parts.Length > axisIndex)
                    {
                        if (!double.TryParse(parts[axisIndex], out double parsedAxisY)) parsedAxisY = double.NaN;
                        axisY = parsedAxisY;
                    }

                    points.Add(new SeamTraceYPoint
                    {
                        RobotX = robotX,
                        MapKeyX = mapKeyX,
                        CspInYt = cspInYt,
                        CspOutY = cspOutY,
                        AxisY = axisY
                    });
                }
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog(Tag, "焊缝Y记录文件解析失败 " + ex.Message, MessageLevel.Warning);
            }

            return points;
        }
    }
}
