using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam;
using AdaWeldSystem.LineLaserCam.Data;
using Emgu.CV;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AdaWeldSystem.EmguALG.LineLaserContour
{
    /// <summary>
    /// 线激光轮廓 Mat 渲染算法实现（继承 Abstractions，输出 Mat 供 UI 显示）。
    /// 2026-09-09 从 LineLaserManager.ProfileMatRenderer 上提为 EmguALG 独立算法，
    /// 线激光模块只负责采集/识别，显示渲染归 EmguALG.LineLaserContour（ADR-038/039）。
    /// 画布与 Bitmap 一次分配后终身复用，每帧只 Clear 后重绘，消除 GC 抖动。
    /// </summary>
    public class LineLaserContourAlgorithm : ILineLaserContourAlgorithm
    {
        #region 私有变量

        private const string Tag = "线激光轮廓渲染";
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

        /// <summary>
        /// 把一帧轮廓渲染为 Mat
        /// </summary>
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

                    if (result != null && result.Valid)
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
}
