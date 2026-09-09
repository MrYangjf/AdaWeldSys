using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.LineLaserCam.VirtualCam;
using AdaWeldSystem.MotionControl.IMotion;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.ProductFileManager;
using Emgu.CV;

namespace AdaWeldSystem.LineLaserCam
{
    /// <summary>轮廓帧快照（池化复用，供界面线程按版本号轮询拉取）</summary>
    /// <remarks>
    /// 双缓冲设计：业务层在回调线程填充后备快照，填充完成后与前台快照整体交换，
    /// 界面线程只读前台快照，读写永不落在同一数组上，避免撕裂。
    /// 数组一次分配后终身复用，运行期零 GC 压力。
    /// 版本号单调递增，界面线程据此判断是否有新帧，避免重复渲染。
    /// </remarks>
    public class ProfileSnapshot
    {
        #region 私有变量

        private readonly double[] _ys;
        private readonly double[] _zs;

        #endregion

        #region 公共变量

        /// <summary>单帧最大点数，与轮廓回调的取点上限一致</summary>
        public const int MaxPoints = 2048;

        /// <summary>轮廓点横向坐标（毫米），有效长度见 Count</summary>
        public double[] Ys { get { return _ys; } }

        /// <summary>轮廓点高度坐标（毫米），有效长度见 Count</summary>
        public double[] Zs { get { return _zs; } }

        /// <summary>本帧有效点数</summary>
        public int Count { get; private set; }

        /// <summary>本帧是否有有效识别结果</summary>
        public bool HasResult { get; private set; }

        /// <summary>焊缝特征点横向坐标（毫米）</summary>
        public double FeatureY { get; private set; }

        /// <summary>焊缝特征点高度坐标（毫米）</summary>
        public double FeatureZ { get; private set; }

        /// <summary>实测帧率（Hz）</summary>
        public double MeasuredFps { get; private set; }

        /// <summary>快照版本号，每次填充后递增</summary>
        public long Version { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建轮廓帧快照并分配复用数组</summary>
        public ProfileSnapshot()
        {
            _ys = new double[MaxPoints];
            _zs = new double[MaxPoints];
        }

        #endregion

        #region 公共函数

        /// <summary>填充快照内容并推进版本号</summary>
        /// <param name="ys">横向坐标源数组</param>
        /// <param name="zs">高度坐标源数组</param>
        /// <param name="count">有效点数，超过上限时截断</param>
        /// <param name="hasResult">是否有有效识别结果</param>
        /// <param name="featureY">特征点横向坐标</param>
        /// <param name="featureZ">特征点高度坐标</param>
        /// <param name="measuredFps">实测帧率</param>
        /// <param name="version">新版本号</param>
        public void Fill(double[] ys, double[] zs, int count, bool hasResult,
            double featureY, double featureZ, double measuredFps, long version)
        {
            int n = (count > MaxPoints) ? MaxPoints : count;
            if (n < 0) n = 0;

            for (int i = 0; i < n; i++)
            {
                _ys[i] = ys[i];
                _zs[i] = zs[i];
            }

            Count = n;
            HasResult = hasResult;
            FeatureY = featureY;
            FeatureZ = featureZ;
            MeasuredFps = measuredFps;
            Version = version;
        }

        /// <summary>清空为无效帧并推进版本号，用于停机与切换相机</summary>
        /// <param name="version">新版本号</param>
        public void Clear(long version)
        {
            Count = 0;
            HasResult = false;
            FeatureY = 0.0;
            FeatureZ = 0.0;
            MeasuredFps = 0.0;
            Version = version;
        }

        #endregion
    }

    /// <summary>轮廓 Mat 渲染器</summary>
    /// <remarks>
    /// 原为英莱相机实现层内部的私有绘制逻辑（ConvertContourToMatDemo），2026-09-08 上提至业务层：
    /// 实现层只负责在 SDK 回调中捕获并存储校准数据（<see cref="LineLaserCalibration"/>），
    /// 显示用的 Mat 由业务层按需生成（ADR-039 线激光职责二次分工）。
    /// 画布与 Bitmap 一次分配后终身复用，每帧只 Clear 后重绘，消除 GC 抖动。
    /// </remarks>
    public class ProfileMatRenderer : IDisposable
    {
        #region 私有变量

        private const string Tag = "线激光渲染器";
        private const int CanvasW = 834;
        private const int CanvasH = 554;
        private const float Margin = 30f;

        private readonly object _lock = new object();
        private Bitmap _canvasBmp;
        private Graphics _canvasG;
        private Mat _mat;
        private bool _disposed;

        #endregion

        #region 公共变量

        /// <summary>最近一次渲染结果，调用方须自行 Clone 后使用</summary>
        public Mat LastMat { get { lock (_lock) { return _mat; } } }

        #endregion

        #region 私有函数

        /// <summary>首次渲染时创建持久画布</summary>
        private void EnsureCanvas()
        {
            if (_canvasBmp == null)
            {
                _canvasBmp = new Bitmap(CanvasW, CanvasH);
                _canvasG = Graphics.FromImage(_canvasBmp);
            }
        }

        /// <summary>把画布内容拷入持久 Mat，首帧新建、后续原地复用</summary>
        private void UpdateMatFromCanvas()
        {
            if (_mat == null)
            {
                _mat = _canvasBmp.ToMat();
                return;
            }

            using (Mat tmp = _canvasBmp.ToMat())
            {
                tmp.CopyTo(_mat);
            }
        }

        /// <summary>求两条直线的交点，平行时返回 (-1,-1)</summary>
        /// <param name="line1P1">直线一点一</param>
        /// <param name="line1P2">直线一点二</param>
        /// <param name="line2P1">直线二点一</param>
        /// <param name="line2P2">直线二点二</param>
        /// <returns>交点坐标</returns>
        private static PointF CalcTwoLineCrossPoint(PointF line1P1, PointF line1P2, PointF line2P1, PointF line2P2)
        {
            float x1 = line1P1.X, y1 = line1P1.Y;
            float x2 = line1P2.X, y2 = line1P2.Y;
            float x3 = line2P1.X, y3 = line2P1.Y;
            float x4 = line2P2.X, y4 = line2P2.Y;

            float denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            if (denom == 0) return new PointF(-1f, -1f);

            float ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
            return new PointF(x1 + ua * (x2 - x1), y1 + ua * (y2 - y1));
        }

        #endregion

        #region 公共函数

        /// <summary>把一帧轮廓渲染为 Mat</summary>
        /// <param name="cloud">轮廓点云，为空时只清屏</param>
        /// <param name="calib">校准数据，HasData 为 false 时按轮廓范围自动缩放</param>
        /// <param name="result">焊缝识别结果，有效时绘制特征点</param>
        /// <returns>渲染结果，调用方须 Clone 后再跨线程使用</returns>
        public Mat Render(PointCloudData cloud, LineLaserCalibration calib, LineLaserSeamResult result)
        {
            lock (_lock)
            {
                if (_disposed) return null;

                EnsureCanvas();
                Graphics g = _canvasG;
                g.Clear(Color.FromArgb(30, 30, 30));
                g.SmoothingMode = SmoothingMode.AntiAlias;

                bool hasCalib = (calib != null) && calib.HasData;
                float rate = 1f;
                PointF center = new PointF(CanvasW / 2f, CanvasH / 2f);
                float fallbackScaleX = 0f, fallbackScaleY = 0f;
                float minY = 0f, minZ = 0f, maxY = 0f, maxZ = 0f;
                PointF[] corners = null;
                PointF crossH1 = PointF.Empty, crossH2 = PointF.Empty;
                PointF crossV1 = PointF.Empty, crossV2 = PointF.Empty;

                var pts = (cloud == null) ? null : cloud.Points;
                int count = (pts == null) ? 0 : pts.Count;

                if (count >= 2)
                {
                    maxY = float.MinValue; maxZ = float.MinValue;
                    minY = float.MaxValue; minZ = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        float y = pts[i].Y;
                        float z = pts[i].Z;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z;
                        if (z > maxZ) maxZ = z;
                    }

                    if (hasCalib)
                    {
                        float cornerW = Math.Abs(calib.RightBottomCorner[0] - calib.LeftBottomCorner[0]);
                        float cornerH = Math.Abs(calib.LeftTopCorner[1] - calib.LeftBottomCorner[1]);
                        if (cornerW < 0.001f) cornerW = 1f;
                        if (cornerH < 0.001f) cornerH = 1f;
                        float fRate1 = (CanvasW - 2f * Margin) / cornerW;
                        float fRate2 = (CanvasH - 2f * Margin) / cornerH;
                        rate = (fRate1 > fRate2) ? fRate2 : fRate1;

                        // 原英莱 demo 用 fPersent/dwOffset 计算中心导致十字架不居中，此处直接取图像中心
                        center = new PointF(CanvasW / 2f, CanvasH / 2f);
                        corners = new PointF[4];
                        corners[0] = new PointF(center.X - Math.Abs(calib.LeftTopCorner[0] * rate), center.Y - Math.Abs(calib.LeftTopCorner[1] * rate));
                        corners[1] = new PointF(center.X - Math.Abs(calib.LeftBottomCorner[0] * rate), center.Y + Math.Abs(calib.LeftBottomCorner[1] * rate));
                        corners[2] = new PointF(center.X + Math.Abs(calib.RightBottomCorner[0] * rate), center.Y + Math.Abs(calib.RightBottomCorner[1] * rate));
                        corners[3] = new PointF(center.X + Math.Abs(calib.RightTopCorner[0] * rate), center.Y - Math.Abs(calib.RightTopCorner[1] * rate));

                        PointF lineH1 = new PointF(center.X, center.Y), lineH2 = new PointF(center.X + 100f, center.Y);
                        PointF lineV1 = new PointF(center.X, center.Y), lineV2 = new PointF(center.X, center.Y + 100f);
                        crossH1 = CalcTwoLineCrossPoint(lineH1, lineH2, corners[0], corners[1]);
                        crossH2 = CalcTwoLineCrossPoint(lineH1, lineH2, corners[2], corners[3]);
                        crossV1 = CalcTwoLineCrossPoint(lineV1, lineV2, corners[3], corners[0]);
                        crossV2 = CalcTwoLineCrossPoint(lineV1, lineV2, corners[1], corners[2]);
                    }
                    else
                    {
                        float rangeY = maxY - minY;
                        float rangeZ = maxZ - minZ;
                        if (rangeY < 0.001f) rangeY = 1f;
                        if (rangeZ < 0.001f) rangeZ = 1f;
                        fallbackScaleX = (CanvasW - 2f * Margin) / rangeY;
                        fallbackScaleY = (CanvasH - 2f * Margin) / rangeZ;
                        rate = (fallbackScaleX > fallbackScaleY) ? fallbackScaleY : fallbackScaleX;
                        center = new PointF(Margin + (minY + maxY) / 2f * fallbackScaleX,
                            CanvasH - Margin - (minZ + maxZ) / 2f * fallbackScaleY);

                        using (var penGrid = new Pen(Color.FromArgb(60, 60, 60), 1f))
                        {
                            for (int i = 0; i <= 8; i++)
                            {
                                float gx = Margin + (CanvasW - 2f * Margin) * i / 8f;
                                g.DrawLine(penGrid, gx, Margin, gx, CanvasH - Margin);
                                float gy = Margin + (CanvasH - 2f * Margin) * i / 8f;
                                g.DrawLine(penGrid, Margin, gy, CanvasW - Margin, gy);
                            }
                        }
                    }
                }

                // 坐标变换：校准模式按 center + 物理值×rate，回退模式按轮廓范围自动缩放
                Func<float, float, PointF> trans = hasCalib
                    ? (Func<float, float, PointF>)((y, z) => new PointF(center.X + y * rate, center.Y - z * rate))
                    : (Func<float, float, PointF>)((y, z) => new PointF(Margin + (y - minY) * fallbackScaleX,
                        CanvasH - Margin - (z - minZ) * fallbackScaleY));

                if (hasCalib && corners != null)
                {
                    using (var bgBrush = new SolidBrush(Color.FromArgb(0x3B, 0x3B, 0x3B)))
                    {
                        g.FillPolygon(bgBrush, corners);
                    }
                    using (var penOutline = new Pen(Color.Yellow, 2f))
                    {
                        g.DrawPolygon(penOutline, corners);
                    }
                    using (var penCross = new Pen(Color.LightGray, 2f))
                    {
                        if (crossH1.X != -1f && crossH2.X != -1f) g.DrawLine(penCross, crossH1, crossH2);
                        if (crossV1.X != -1f && crossV2.X != -1f) g.DrawLine(penCross, crossV1, crossV2);
                    }
                    using (var brushGreen = new SolidBrush(Color.LightGreen))
                    {
                        const float dotDiameter = 6f;
                        float radius = dotDiameter / 2f;
                        g.FillEllipse(brushGreen, center.X - radius, center.Y - radius, dotDiameter, dotDiameter);
                        foreach (PointF pt in corners)
                        {
                            g.FillEllipse(brushGreen, pt.X - radius, pt.Y - radius, dotDiameter, dotDiameter);
                        }
                    }
                }

                if (count >= 2)
                {
                    int n = (count > ProfileSnapshot.MaxPoints) ? ProfileSnapshot.MaxPoints : count;
                    var points = new PointF[n];
                    for (int i = 0; i < n; i++)
                    {
                        points[i] = trans(pts[i].Y, pts[i].Z);
                    }
                    using (var pen = new Pen(Color.FromArgb(0, 255, 128), 1.5f))
                    {
                        g.DrawLines(pen, points);
                    }

                    if (result.Valid)
                    {
                        PointF fp = trans(result.FeatureY, result.FeatureZ);
                        using (var penFeature = new Pen(Color.Red, 8f))
                        {
                            g.DrawRectangle(penFeature, fp.X, fp.Y, 1f, 1f);
                        }
                    }
                }

                // 毫米虚线刻度栅格：原点对齐图像中心，仅在有校准数据时绘制
                if (hasCalib && rate > 0f)
                {
                    float mmYMin = -CanvasW / (2f * rate);
                    float mmYMax = CanvasW / (2f * rate);
                    float mmZMin = -CanvasH / (2f * rate);
                    float mmZMax = CanvasH / (2f * rate);
                    float stepMm = Math.Max(1f, (float)Math.Round(40f / rate));

                    using (var penGridDash = new Pen(Color.FromArgb(80, 80, 80), 1f))
                    using (var fontGrid = new Font("宋体", 8.5f))
                    using (var brushGrid = new SolidBrush(Color.FromArgb(170, 170, 170)))
                    {
                        penGridDash.DashStyle = DashStyle.Dash;

                        int kStartY = (int)Math.Ceiling(mmYMin / stepMm);
                        int kEndY = (int)Math.Floor(mmYMax / stepMm);
                        for (int k = kStartY; k <= kEndY; k++)
                        {
                            float mmVal = k * stepMm;
                            float sx = center.X + mmVal * rate;
                            g.DrawLine(penGridDash, sx, 0f, sx, CanvasH);
                            string txt = string.Format("{0}", (int)Math.Round(mmVal));
                            g.DrawString(txt, fontGrid, brushGrid, sx + 2f, CanvasH - 4f - fontGrid.Height);
                        }

                        int kStartZ = (int)Math.Ceiling(mmZMin / stepMm);
                        int kEndZ = (int)Math.Floor(mmZMax / stepMm);
                        for (int k = kStartZ; k <= kEndZ; k++)
                        {
                            float mmVal = k * stepMm;
                            float sy = center.Y - mmVal * rate;
                            g.DrawLine(penGridDash, 0f, sy, CanvasW, sy);
                            string txt = string.Format("{0}", (int)Math.Round(mmVal));
                            g.DrawString(txt, fontGrid, brushGrid, 2f, sy + 1f);
                        }
                    }
                }

                if (!hasCalib && count >= 2)
                {
                    using (var font = new Font("宋体", 9f))
                    using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                    {
                        string labelY = string.Format("Y:{0:F1}~{1:F1}mm", minY, maxY);
                        g.DrawString(labelY, font, brush, Margin, CanvasH - Margin + 4f);
                        var sf = new StringFormat();
                        sf.FormatFlags = StringFormatFlags.DirectionVertical;
                        string labelZ = string.Format("Z:{0:F1}~{1:F1}mm", minZ, maxZ);
                        g.DrawString(labelZ, font, brush, 4f, Margin, sf);
                    }
                }

                UpdateMatFromCanvas();
                return _mat;
            }
        }

        /// <summary>释放画布与 Mat 资源</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                if (_canvasG != null) { _canvasG.Dispose(); _canvasG = null; }
                if (_canvasBmp != null) { _canvasBmp.Dispose(); _canvasBmp = null; }
                if (_mat != null) { _mat.Dispose(); _mat = null; }
                GlobalCommData.ShowLog(Tag, "轮廓渲染器已释放");
            }
        }

        #endregion
    }

    /// <summary>焊缝跟踪 PVT 点下发器（Y 轴跟随）</summary>
    /// <remarks>
    /// 把线激光识别结果（焊缝特征点横向坐标）换算为 Y 轴 PVT 轨迹点，按台达 **B 套流式**
    /// 方式持续下载：PVT_Begin_Move 起段 → 按缓冲区水位 PVT_Continue_Move 续喂
    /// （台达无虚拟轴，ECAM2 SourceType 只有 0/1 两种取值，机器人 X→Y 跟随只能走 PVT B 套）。
    /// 依赖注入面只有 <see cref="IPVTMotion"/> 一个能力抽象，不感知控制器型号、不做脉冲换算
    /// （换算在运控 L3 完成）；未实现 PVT 的轴返回 NotSupported，不会连坐编译。
    /// 时间基准与速度前馈为待实测参数：Time 取相邻帧实测间隔（相对时间模式），
    /// Vel 取位置一阶差分，现场标定后再固化（ADR-039）。
    /// </remarks>
    public class SeamPvtFeeder
    {
        #region 私有变量

        private const string Tag = "焊缝跟踪";

        /// <summary>台达 PVT 点表容量上限，超过后必须丢弃最旧点防止内存膨胀</summary>
        private const int MaxPending = 8000;

        private readonly List<PVTPoint> _pending;
        private readonly object _lock = new object();

        private IPVTMotion _axis;
        private bool _running;
        private double _lastPos;
        private bool _hasLastPos;
        private ushort _lastCode;

        #endregion

        #region 公共变量

        /// <summary>单批下发点数，攒够即下发</summary>
        public int BatchSize { get; private set; }

        /// <summary>实测帧率不可用时的默认帧间隔（秒）</summary>
        public double DefaultFrameSeconds { get; private set; }

        /// <summary>剩余缓冲区低于此值时不续喂，避免点表溢出</summary>
        public int MinRemainBuffer { get; private set; }

        /// <summary>PVT 起段后是否处于运动中</summary>
        public bool IsRunning { get { lock (_lock) { return _running; } } }

        /// <summary>当前待下发的点数</summary>
        public int PendingCount { get { lock (_lock) { return _pending.Count; } } }

        /// <summary>最近一次下发返回码，0 为成功</summary>
        public ushort LastErrorCode { get { lock (_lock) { return _lastCode; } } }

        #endregion

        #region 构造函数

        /// <summary>创建 PVT 点下发器</summary>
        /// <param name="batchSize">单批下发点数</param>
        /// <param name="defaultFrameSeconds">实测帧率不可用时的默认帧间隔（秒）</param>
        /// <param name="minRemainBuffer">剩余缓冲区续喂下限</param>
        public SeamPvtFeeder(int batchSize, double defaultFrameSeconds, int minRemainBuffer)
        {
            _pending = new List<PVTPoint>(batchSize * 2);
            BatchSize = (batchSize < 1) ? 1 : batchSize;
            DefaultFrameSeconds = (defaultFrameSeconds > 0.0) ? defaultFrameSeconds : 1.0 / 60.0;
            MinRemainBuffer = (minRemainBuffer < 1) ? 1 : minRemainBuffer;
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>把待下发点整批下载到轴，水位不足时保留本批继续攒积</summary>
        /// <param name="isFinal">是否为最后一段</param>
        private void Flush(bool isFinal)
        {
            if (_pending.Count == 0) return;
            if (_axis == null) return;

            if (_running && !isFinal)
            {
                PVTInformation info;
                ushort query = _axis.GetInformation(out info);
                if (query == PVTResult.OK && info.RemainBufferSize < MinRemainBuffer) return;
            }

            PVTPoint[] segment = _pending.ToArray();
            _pending.Clear();

            ushort code = _running
                ? _axis.ContinueMove(segment, isFinal)
                : _axis.BeginMove(segment, PVTTimeMode.Relative, isFinal, false, 0.0);

            _lastCode = code;
            if (code == PVTResult.OK)
            {
                _running = !isFinal;
                return;
            }

            _running = false;
            Log(string.Format("PVT 点下发失败 返回码 0x{0:X} 点数 {1}", code, segment.Length), MessageLevel.Error);
        }

        #endregion

        #region 公共函数

        /// <summary>注入 Y 轴的 PVT 能力，由运控工作流初始化成功后调用</summary>
        /// <param name="axis">具备 PVT 能力的轴，传 null 表示撤除注入</param>
        public void Attach(IPVTMotion axis)
        {
            lock (_lock)
            {
                Detach();
                _axis = axis;
                if (axis != null)
                {
                    Log(string.Format("Y 轴 PVT 已注入 批次 {0} 点 帧间隔 {1:F4} 秒", BatchSize, DefaultFrameSeconds));
                }
            }
        }

        /// <summary>收尾下发并撤除注入，停止采集时必须调用</summary>
        public void Detach()
        {
            lock (_lock)
            {
                if (_axis != null) Flush(true);
                _axis = null;
                _running = false;
            }
        }

        /// <summary>清空待下发点与运动状态，不向轴发送任何指令</summary>
        public void Reset()
        {
            lock (_lock)
            {
                _pending.Clear();
                _running = false;
                _hasLastPos = false;
                _lastPos = 0.0;
                _lastCode = PVTResult.OK;
            }
        }

        /// <summary>把剩余点标记为最后一段并下发，结束本轮跟踪但保留轴注入</summary>
        public void FinishSegment()
        {
            lock (_lock)
            {
                if (_axis == null) return;
                Flush(true);
                _running = false;
                _hasLastPos = false;
            }
        }

        /// <summary>更新下发参数，配置重新加载后调用</summary>
        /// <param name="batchSize">单批下发点数</param>
        /// <param name="defaultFrameSeconds">实测帧率不可用时的默认帧间隔（秒）</param>
        /// <param name="minRemainBuffer">剩余缓冲区续喂下限</param>
        public void SetParameters(int batchSize, double defaultFrameSeconds, int minRemainBuffer)
        {
            lock (_lock)
            {
                BatchSize = (batchSize < 1) ? 1 : batchSize;
                DefaultFrameSeconds = (defaultFrameSeconds > 0.0) ? defaultFrameSeconds : 1.0 / 60.0;
                MinRemainBuffer = (minRemainBuffer < 1) ? 1 : minRemainBuffer;
            }
        }

        /// <summary>喂入一帧识别结果，换算为 PVT 点并在满足条件时下发</summary>
        /// <param name="result">本帧焊缝识别结果，无效帧直接丢弃</param>
        /// <param name="measuredFps">实测帧率，不可用传 0</param>
        public void Feed(LineLaserSeamResult result, double measuredFps)
        {
            if (!result.Valid) return;

            lock (_lock)
            {
                if (_axis == null) return;

                double dt = (measuredFps > 1.0) ? (1.0 / measuredFps) : DefaultFrameSeconds;
                double pos = result.FeatureY;
                double vel = _hasLastPos ? ((pos - _lastPos) / dt) : 0.0;
                _lastPos = pos;
                _hasLastPos = true;

                if (_pending.Count >= MaxPending)
                {
                    _pending.RemoveAt(0);
                }
                _pending.Add(new PVTPoint(pos, dt, vel));

                if (_pending.Count >= BatchSize) Flush(false);
            }
        }

        #endregion
    }

    /// <summary>线激光管理器</summary>
    /// <remarks>
    /// 面向抽象层编程，具体相机类型（英莱 / 虚拟）仅在本文件的惰性创建处出现，
    /// 全系统取相机实例一律走本管理器，禁止在业务层自行 new 或持有厂商实现类型。
    /// 2026-09-08 职责再分工（ADR-039）：
    /// ① 注册与选中、连接态派生、配置持久化；
    /// ② 承担全部业务处理——实现层只发 ResultReady / ProfileReady 两个回调，
    ///    结果侧本类换算 Y 轴 PVT 点并下载，轮廓侧维护轮廓点快照与显示 Mat；
    /// ③ 相机外设（连接 / 激光 / 传感器）由本类代理，工作流不直接接触相机对象。
    /// </remarks>
    public class LineLaserManager : IDisposable
    {
        #region 私有变量

        private const string Tag = "线激光管理器";

        private static readonly Lazy<LineLaserManager> _lazy =
            new Lazy<LineLaserManager>(() => new LineLaserManager());

        private IntelligentLaserCam.IntelligentLaserCam _intelligentInstance;
        private VirtualCam.VirtualCam _virtualInstance;
        private LineLaserCameraBase _active;
        private bool _working;
        private bool _disposed;

        // 结果处理链路：Y 轴 PVT 点计算与流式下载
        private readonly SeamPvtFeeder _pvtFeeder;
        private LineLaserSeamResult _lastResult;

        // 轮廓处理链路：点缓冲（双缓冲快照）+ 显示 Mat 渲染
        private readonly ProfileMatRenderer _renderer;
        private readonly double[] _ysBuffer = new double[ProfileSnapshot.MaxPoints];
        private readonly double[] _zsBuffer = new double[ProfileSnapshot.MaxPoints];
        private ProfileSnapshot _front;
        private ProfileSnapshot _back;
        private long _profileVersion;
        private DateTime _lastDisplayTime = DateTime.MinValue;

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static LineLaserManager Instance { get { return _lazy.Value; } }

        /// <summary>相机注册表</summary>
        public CameraRegistry Cameras { get; private set; }

        /// <summary>线激光配置，默认相机与相机 IP 的唯一入口</summary>
        public LineLaserConfig Config { get; private set; }

        /// <summary>当前选中的相机，未显式选择时按配置默认种类惰性创建</summary>
        public LineLaserCameraBase Active
        {
            get
            {
                if (_active == null)
                {
                    if (Config.DefaultKind == LineLaserKind.Virtual)
                    {
                        SelectVirtual();
                    }
                    else
                    {
                        SelectIntelligentLaser();
                    }
                }
                return _active;
            }
        }

        /// <summary>
        /// 英莱相机实例（管理器持有，首次访问时创建并登记）。
        /// 厂商实现层不对外暴露实例，界面与业务方一律经本属性取得。
        /// </summary>
        public IntelligentLaserCam.IntelligentLaserCam IntelligentLaser
        {
            get
            {
                if (_intelligentInstance == null)
                {
                    _intelligentInstance = new IntelligentLaserCam.IntelligentLaserCam();
                    Register(_intelligentInstance.CameraName, _intelligentInstance);
                    ApplyConfig();
                }
                return _intelligentInstance;
            }
        }

        /// <summary>虚拟相机实例（管理器持有，首次访问时创建并登记）</summary>
        public VirtualCam.VirtualCam VirtualCamera
        {
            get
            {
                if (_virtualInstance == null)
                {
                    _virtualInstance = new VirtualCam.VirtualCam();
                    Register(_virtualInstance.CameraName, _virtualInstance);
                    ApplyConfig();
                }
                return _virtualInstance;
            }
        }

        /// <summary>当前选中相机的键名，无相机时为空串</summary>
        public string ActiveName { get { return _active == null ? string.Empty : _active.CameraName; } }

        /// <summary>当前相机种类</summary>
        public LineLaserKind Kind { get { return _active == null ? LineLaserKind.None : _active.Kind; } }

        /// <summary>当前是否为虚拟调试相机</summary>
        public bool IsVirtual { get { return Kind == LineLaserKind.Virtual; } }

        /// <summary>工作流是否正在驱动相机出图</summary>
        public bool IsWorking { get { return _working; } }

        /// <summary>
        /// 线激光相机连接状态（与工作流程状态相互独立）。
        /// 工作中由工作流经 SetWorking 驱动，其余按选中相机自身的连接情况派生。
        /// </summary>
        public LineLaserConnectionState ConnectionState
        {
            get
            {
                if (_working) return LineLaserConnectionState.Working;
                if (_active == null) return LineLaserConnectionState.Disconnected;
                return _active.ConnectionState;
            }
        }

        /// <summary>最近一帧轮廓快照，供界面线程按版本号轮询刷新（只读约定，勿跨帧持有数组）</summary>
        public ProfileSnapshot Profile { get { return _front; } }

        /// <summary>最近一次焊缝识别结果</summary>
        public LineLaserSeamResult LastResult { get { return _lastResult; } }

        /// <summary>显示处理是否已开启，默认关闭（UI 显示非必需且手动/自动模式需求不同）</summary>
        public bool DisplayEnabled { get; private set; }

        /// <summary>显示处理目标帧率（Hz），用于轮廓转 Mat 的节流</summary>
        public double DisplayFps { get; private set; }

        /// <summary>显示处理是否生成 Mat，关闭时只更新点缓冲不做图像转换</summary>
        public bool ConvertMatEnabled { get; private set; }

        /// <summary>识别结果就绪事件，转发自已登记相机，工作流与业务方订阅此事件</summary>
        public event EventHandler<LineLaserResultEventArgs> ResultReady;

        /// <summary>轮廓 Mat 更新事件，仅在显示处理开启时触发，供手动调试页面订阅</summary>
        public event EventHandler<LineLaserMatEventArgs> MatUpdated;

        /// <summary>相机状态变化事件，转发自已登记相机</summary>
        public event EventHandler<LineLaserStateEventArgs> CameraStateChanged;

        #endregion

        #region 构造函数

        private LineLaserManager()
        {
            Cameras = new CameraRegistry();
            Config = new LineLaserConfig();
            Config.LoadConfig();

            _pvtFeeder = new SeamPvtFeeder(Config.PvtBatchSize, Config.PvtFrameSeconds, Config.PvtMinRemainBuffer);
            _renderer = new ProfileMatRenderer();
            _front = new ProfileSnapshot();
            _back = new ProfileSnapshot();
            _lastResult = LineLaserSeamResult.Invalid(0, 0);

            DisplayEnabled = Config.DisplayEnabled;
            DisplayFps = Config.DisplayFps;
            ConvertMatEnabled = Config.ConvertMatEnabled;
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>把配置套用到已创建的相机实例</summary>
        private void ApplyConfig()
        {
            _pvtFeeder.SetParameters(Config.PvtBatchSize, Config.PvtFrameSeconds, Config.PvtMinRemainBuffer);

            DisplayEnabled = Config.DisplayEnabled;
            DisplayFps = Config.DisplayFps;
            ConvertMatEnabled = Config.ConvertMatEnabled;

            if (_intelligentInstance != null)
            {
                _intelligentInstance.SensorIp = Config.SensorIp;
            }

            if (_virtualInstance != null)
            {
                _virtualInstance.SeamProfile = (SimSeamProfile)Config.SimSeamProfile;
                _virtualInstance.RandomMode = Config.SimRandomMode;
                _virtualInstance.TargetDistance = Config.SimTargetDistance;
                _virtualInstance.BaseSeamWidth = Config.SimBaseSeamWidth;
                _virtualInstance.BaseSeamDepth = Config.SimBaseSeamDepth;
            }
        }

        /// <summary>把相机切换为当前选中项，无日志（属状态切换，ADR-035）</summary>
        /// <param name="camera">相机对象</param>
        private void SetActive(LineLaserCameraBase camera)
        {
            _active = camera;
        }

        /// <summary>转发相机状态变化事件</summary>
        /// <param name="sender">事件源相机</param>
        /// <param name="e">状态事件参数</param>
        private void OnCameraStateChanged(object sender, LineLaserStateEventArgs e)
        {
            EventHandler<LineLaserStateEventArgs> handler = CameraStateChanged;
            if (handler != null) handler(this, e);
        }

        /// <summary>结果处理：缓存结果、换算 Y 轴 PVT 点并下载，随后向外转发事件</summary>
        /// <param name="sender">事件源相机</param>
        /// <param name="e">结果事件参数</param>
        private void OnCameraResultReady(object sender, LineLaserResultEventArgs e)
        {
            _lastResult = e.Result;
            _pvtFeeder.Feed(e.Result, e.MeasuredFps);

            EventHandler<LineLaserResultEventArgs> handler = ResultReady;
            if (handler != null) handler(this, e);
        }

        /// <summary>轮廓处理：产品记录、更新点缓冲、按需生成显示 Mat</summary>
        /// <param name="sender">事件源相机</param>
        /// <param name="e">轮廓事件参数</param>
        private void OnCameraProfileReady(object sender, LineLaserProfileEventArgs e)
        {
            if (e.Cloud != null)
            {
                // 数据记录：AddFrame 内部已有 ClonePointCloud + lock，未录制时直接返回零开销
                RunRecordManager.Instance.AddFrame(e.Cloud);
                UpdateProfileSnapshot(e.Cloud, e.MeasuredFps);
                if (DisplayEnabled) RenderDisplayFrame(e.Cloud);
            }
        }

        /// <summary>把轮廓点写入后备快照并整体交换为前台快照</summary>
        /// <param name="cloud">本帧轮廓点云</param>
        /// <param name="measuredFps">实测帧率</param>
        private void UpdateProfileSnapshot(PointCloudData cloud, double measuredFps)
        {
            var pts = cloud.Points;
            int count = (pts == null) ? 0 : pts.Count;
            if (count > ProfileSnapshot.MaxPoints) count = ProfileSnapshot.MaxPoints;

            for (int i = 0; i < count; i++)
            {
                _ysBuffer[i] = pts[i].Y;
                _zsBuffer[i] = pts[i].Z;
            }

            _profileVersion++;
            _back.Fill(_ysBuffer, _zsBuffer, count, _lastResult.Valid,
                _lastResult.FeatureY, _lastResult.FeatureZ, measuredFps, _profileVersion);

            ProfileSnapshot swap = _front;
            _front = _back;
            _back = swap;
        }

        /// <summary>按显示帧率节流生成轮廓 Mat 并触发更新事件</summary>
        /// <param name="cloud">本帧轮廓点云</param>
        private void RenderDisplayFrame(PointCloudData cloud)
        {
            // 节流：界面刷新不必跟随相机帧率，默认 15Hz
            if (DisplayFps > 0.0)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _lastDisplayTime).TotalMilliseconds < (1000.0 / DisplayFps)) return;
                _lastDisplayTime = now;
            }

            if (!ConvertMatEnabled) return;

            LineLaserCalibration calib = (_active == null) ? null : _active.Calibration;
            Mat frame = _renderer.Render(cloud, calib, _lastResult);
            if (frame == null) return;

            // 渲染器持久画布归渲染器所有，此处克隆一帧交由相机与订阅方共享
            Mat publish = frame.Clone();
            if (_active != null) _active.UpdateDisplayMat(publish);

            EventHandler<LineLaserMatEventArgs> handler = MatUpdated;
            if (handler != null) handler(this, new LineLaserMatEventArgs(ActiveName, publish));
        }

        #endregion

        #region 公共函数

        /// <summary>登记相机并订阅其结果与轮廓回调及状态变化事件</summary>
        /// <param name="name">相机键名</param>
        /// <param name="camera">相机对象</param>
        public void Register(string name, LineLaserCameraBase camera)
        {
            if (camera == null || string.IsNullOrEmpty(name)) return;

            LineLaserCameraBase old = Cameras.Get(name);
            if (old != null)
            {
                old.ResultReady -= OnCameraResultReady;
                old.ProfileReady -= OnCameraProfileReady;
                old.StateChanged -= OnCameraStateChanged;
            }

            camera.ResultReady += OnCameraResultReady;
            camera.ProfileReady += OnCameraProfileReady;
            camera.StateChanged += OnCameraStateChanged;
            Cameras.Register(name, camera);
        }

        /// <summary>撤销相机登记并退订事件</summary>
        /// <param name="name">相机键名</param>
        public void Unregister(string name)
        {
            LineLaserCameraBase camera = Cameras.Get(name);
            if (camera == null) return;

            camera.ResultReady -= OnCameraResultReady;
            camera.ProfileReady -= OnCameraProfileReady;
            camera.StateChanged -= OnCameraStateChanged;
            Cameras.Remove(name);

            if (ReferenceEquals(_intelligentInstance, camera)) _intelligentInstance = null;
            if (ReferenceEquals(_virtualInstance, camera)) _virtualInstance = null;
            if (ReferenceEquals(_active, camera)) _active = null;
        }

        /// <summary>取已登记的相机</summary>
        /// <param name="name">相机键名</param>
        /// <returns>相机对象，未找到返回 null</returns>
        public LineLaserCameraBase Get(string name)
        {
            return Cameras.Get(name);
        }

        /// <summary>选中英莱真实相机（默认）</summary>
        public void SelectIntelligentLaser()
        {
            ApplyConfig();
            SetActive(IntelligentLaser);
        }

        /// <summary>选中虚拟调试相机（模拟模式使用）</summary>
        public void SelectVirtual()
        {
            ApplyConfig();
            SetActive(VirtualCamera);
        }

        /// <summary>按名称选择已登记相机</summary>
        /// <param name="name">相机键名</param>
        /// <returns>选中成功返回 true</returns>
        public bool Select(string name)
        {
            LineLaserCameraBase camera = Cameras.Get(name);
            if (camera == null) return false;

            SetActive(camera);
            return true;
        }

        /// <summary>连接当前相机，已连接时直接返回 true</summary>
        /// <returns>连接成功或无需连接返回 true</returns>
        public bool Connect()
        {
            LineLaserCameraBase camera = Active;
            if (camera == null) return false;
            return camera.IsConnected || camera.ConnectManual(Config.SensorIp);
        }

        /// <summary>断开当前相机，未连接时不做任何动作</summary>
        public void Disconnect()
        {
            if (_active == null) return;
            if (_active.IsConnected) _active.DisconnectManual();
        }

        /// <summary>配置当前相机的默认回调周期，无需配置的相机返回 true</summary>
        /// <returns>无需配置或配置成功返回 true</returns>
        public bool ConfigureDefaultCommPeriod()
        {
            if (_active == null) return false;
            return _active.ConfigureDefaultCommPeriod();
        }

        /// <summary>当前相机传感器（出图）是否开启</summary>
        /// <returns>开启返回 true，无选中相机返回 false</returns>
        public bool IsCameraOn()
        {
            return (_active != null) && _active.IsCameraOn();
        }

        /// <summary>当前相机激光器是否开启</summary>
        /// <returns>开启返回 true，无选中相机返回 false</returns>
        public bool IsLaserOn()
        {
            return (_active != null) && _active.IsLaserOn();
        }

        /// <summary>开关当前相机传感器（出图）</summary>
        /// <param name="enable">true 为开启</param>
        public void SetSensor(bool enable)
        {
            if (_active != null) _active.SetSensor(enable);
        }

        /// <summary>开关当前相机激光器</summary>
        /// <param name="enable">true 为开启</param>
        public void SetLaser(bool enable)
        {
            if (_active != null) _active.SetLaser(enable);
        }

        /// <summary>
        /// 关闭外设：停采集 → 关激光 → 关传感器。
        /// 用于停机、异常收尾与流程复位，任一单体异常均被吞掉，不影响状态机推进。
        /// </summary>
        public void ShutdownPeripherals()
        {
            try
            {
                StopAcquisition();
            }
            catch
            {
                // 停止采集失败不阻塞后续关外设
            }

            if (_active == null) return;
            try { if (_active.IsLaserOn()) _active.SetLaser(false); } catch { }
            try { if (_active.IsCameraOn()) _active.SetSensor(false); } catch { }
        }

        /// <summary>由工作流在采集开始/结束时调用，驱动「工作中」连接态与跟踪段起止</summary>
        /// <param name="working">true 为正在采集</param>
        public void SetWorking(bool working)
        {
            if (_working == working) return;
            _working = working;

            if (working)
            {
                _pvtFeeder.Reset();
            }
            else
            {
                // 收尾：把剩余点标记为最后一段下发，避免轴停在半截轨迹上
                _pvtFeeder.FinishSegment();
            }
        }

        /// <summary>注入 Y 轴的 PVT 能力，由运控工作流初始化成功后调用</summary>
        /// <remarks>
        /// 依赖注入面只有一个能力抽象 <see cref="IPVTMotion"/>，不持有控制器与轴配置对象，
        /// 未实现 PVT 的轴由抽象层默认实现返回 NotSupported，不影响编译与运行。
        /// </remarks>
        /// <param name="yAxis">具备 PVT 能力的 Y 轴，传 null 表示撤除注入</param>
        public void AttachYAxis(IPVTMotion yAxis)
        {
            _pvtFeeder.Attach(yAxis);
        }

        /// <summary>设置显示处理开关，界面按需开启，默认关闭</summary>
        /// <param name="enabled">是否开启显示处理</param>
        /// <param name="fps">显示帧率（Hz），小于等于 0 表示不节流</param>
        /// <param name="convertMat">是否生成 Mat，关闭时只更新点缓冲</param>
        public void SetDisplayOptions(bool enabled, double fps, bool convertMat)
        {
            DisplayEnabled = enabled;
            DisplayFps = fps;
            ConvertMatEnabled = convertMat;
        }

        /// <summary>启动当前相机的连续采集</summary>
        public void StartAcquisition()
        {
            LineLaserCameraBase camera = Active;
            if (camera != null) camera.SensorRunContinue();
        }

        /// <summary>停止当前相机的连续采集</summary>
        public void StopAcquisition()
        {
            if (_active != null) _active.SensorStop();
        }

        /// <summary>重新加载配置并套用到已创建的相机</summary>
        public void LoadConfig()
        {
            Config.LoadConfig();
            ApplyConfig();
        }

        /// <summary>把当前配置写入 XML</summary>
        public void SaveConfig()
        {
            Config.SaveConfig();
        }

        /// <summary>释放全部相机并清空注册表</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _pvtFeeder.Detach();
            _renderer.Dispose();

            foreach (LineLaserCameraBase camera in Cameras.GetAll())
            {
                camera.ResultReady -= OnCameraResultReady;
                camera.ProfileReady -= OnCameraProfileReady;
                camera.StateChanged -= OnCameraStateChanged;
                camera.Dispose();
            }

            Cameras.Clear();
            _active = null;
            _intelligentInstance = null;
            _virtualInstance = null;
            Log("线激光管理器已释放");
        }

        #endregion
    }
}
