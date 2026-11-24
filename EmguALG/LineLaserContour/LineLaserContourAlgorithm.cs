using Emgu.CV;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.EmguALG.LineLaserContour
{
    /// <summary>线激光轮廓 Mat 渲染算法实现</summary>
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

        /// <summary>毫米坐标转像素坐标</summary>
        /// <param name="origin">毫米原点所在像素位置</param>
        /// <param name="rate">每毫米像素数</param>
        /// <param name="mm">毫米坐标</param>
        /// <returns>像素坐标</returns>
        private static PointF ToPixel(PointF origin, float rate, float[] mm)
        {
            return new PointF(origin.X + mm[0] * rate, origin.Y - mm[1] * rate);
        }

        /// <summary>计算本帧视图几何</summary>
        /// <param name="calib">校准数据</param>
        /// <param name="hasCalib">校准数据是否可用</param>
        /// <param name="pts">轮廓点集</param>
        /// <param name="count">点数</param>
        /// <returns>本帧视图几何</returns>
        private static ViewGeometry BuildViewGeometry(LineLaserCalibrationData calib, bool hasCalib,
            LineLaserProfileData pts, int count)
        {
            ViewGeometry view = new ViewGeometry
            {
                HasCalib = hasCalib,
                Center = new PointF(CanvasW / 2f, CanvasH / 2f),
                Origin = new PointF(CanvasW / 2f, CanvasH / 2f)
            };

            // 校准几何只依赖 calib（角点与 mm 跨度），与轮廓点数无关：
            // 须排在 count 判空之前，否则空帧会提前返回
            // 则 Corners 与 Rate 均为零，校准框与刻度不画
            if (hasCalib)
            {
                // 标定四角为绝对毫米坐标，与轮廓点同一坐标系；
                // 比例取四角包围盒跨度，原点落在包围盒中心，校准框即完整可见且与轮廓严格对齐
                float minX = Math.Min(Math.Min(calib.LeftTopCorner[0], calib.RightTopCorner[0]),
                    Math.Min(calib.LeftBottomCorner[0], calib.RightBottomCorner[0]));
                float maxX = Math.Max(Math.Max(calib.LeftTopCorner[0], calib.RightTopCorner[0]),
                    Math.Max(calib.LeftBottomCorner[0], calib.RightBottomCorner[0]));
                float minZ = Math.Min(Math.Min(calib.LeftTopCorner[1], calib.RightTopCorner[1]),
                    Math.Min(calib.LeftBottomCorner[1], calib.RightBottomCorner[1]));
                float maxZ = Math.Max(Math.Max(calib.LeftTopCorner[1], calib.RightTopCorner[1]),
                    Math.Max(calib.LeftBottomCorner[1], calib.RightBottomCorner[1]));

                float spanX = maxX - minX;
                float spanZ = maxZ - minZ;
                if (spanX < 0.001f) spanX = 1f;
                if (spanZ < 0.001f) spanZ = 1f;
                float fRate1 = (CanvasW - 2f * Margin) / spanX;
                float fRate2 = (CanvasH - 2f * Margin) / spanZ;
                view.Rate = (fRate1 > fRate2) ? fRate2 : fRate1;

                // 毫米原点落在画布上的位置：令标定区域包围盒居中
                view.Origin = new PointF(
                    CanvasW / 2f - (minX + maxX) / 2f * view.Rate,
                    CanvasH / 2f + (minZ + maxZ) / 2f * view.Rate);

                PointF[] corners = new PointF[4];
                corners[0] = ToPixel(view.Origin, view.Rate, calib.LeftTopCorner);
                corners[1] = ToPixel(view.Origin, view.Rate, calib.LeftBottomCorner);
                corners[2] = ToPixel(view.Origin, view.Rate, calib.RightBottomCorner);
                corners[3] = ToPixel(view.Origin, view.Rate, calib.RightTopCorner);
                view.Corners = corners;

                // 视图锚点取标定中心；落在标定区域之外时回退包围盒中心
                PointF center = ToPixel(view.Origin, view.Rate, calib.Center);
                float boxMinX = Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
                float boxMaxX = Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
                float boxMinY = Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
                float boxMaxY = Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));
                if (center.X < boxMinX || center.X > boxMaxX || center.Y < boxMinY || center.Y > boxMaxY)
                {
                    center = new PointF((boxMinX + boxMaxX) / 2f, (boxMinY + boxMaxY) / 2f);
                }
                view.Center = center;

                PointF lineH1 = new PointF(center.X, center.Y), lineH2 = new PointF(center.X + 100f, center.Y);
                PointF lineV1 = new PointF(center.X, center.Y), lineV2 = new PointF(center.X, center.Y + 100f);
                view.CrossH1 = CalcTwoLineCrossPoint(lineH1, lineH2, corners[0], corners[1]);
                view.CrossH2 = CalcTwoLineCrossPoint(lineH1, lineH2, corners[2], corners[3]);
                view.CrossV1 = CalcTwoLineCrossPoint(lineV1, lineV2, corners[3], corners[0]);
                view.CrossV2 = CalcTwoLineCrossPoint(lineV1, lineV2, corners[1], corners[2]);
            }

            if (count < 2) return view;

            view.MaxY = float.MinValue; view.MaxZ = float.MinValue;
            view.MinY = float.MaxValue; view.MinZ = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float y = (float)pts.Ys[i];
                float z = (float)pts.Zs[i];
                if (y < view.MinY) view.MinY = y;
                if (y > view.MaxY) view.MaxY = y;
                if (z < view.MinZ) view.MinZ = z;
                if (z > view.MaxZ) view.MaxZ = z;
            }

            if (hasCalib) return view;

            float rangeY = view.MaxY - view.MinY;
            float rangeZ = view.MaxZ - view.MinZ;
            if (rangeY < 0.001f) rangeY = 1f;
            if (rangeZ < 0.001f) rangeZ = 1f;
            view.FallbackScaleX = (CanvasW - 2f * Margin) / rangeY;
            view.FallbackScaleY = (CanvasH - 2f * Margin) / rangeZ;
            view.Rate = (view.FallbackScaleX > view.FallbackScaleY) ? view.FallbackScaleY : view.FallbackScaleX;
            view.Center = new PointF(Margin + (view.MinY + view.MaxY) / 2f * view.FallbackScaleX,
                CanvasH - Margin - (view.MinZ + view.MaxZ) / 2f * view.FallbackScaleY);
            return view;
        }

        /// <summary>回退模式绘制参考网格</summary>
        /// <param name="g">绘图表面</param>
        /// <param name="view">本帧视图几何</param>
        private static void DrawFallbackGrid(Graphics g, ViewGeometry view)
        {
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

        /// <summary>校准模式绘制标定区域、十字线与中心点</summary>
        /// <param name="g">绘图表面</param>
        /// <param name="view">本帧视图几何</param>
        private static void DrawCalibrationBackdrop(Graphics g, ViewGeometry view)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(0x3B, 0x3B, 0x3B)))
            {
                g.FillPolygon(bgBrush, view.Corners);
            }
            using (var penOutline = new Pen(Color.Yellow, 2f))
            {
                g.DrawPolygon(penOutline, view.Corners);
            }
            using (var penCross = new Pen(Color.LightGray, 2f))
            {
                if (view.CrossH1.X != -1f && view.CrossH2.X != -1f) g.DrawLine(penCross, view.CrossH1, view.CrossH2);
                if (view.CrossV1.X != -1f && view.CrossV2.X != -1f) g.DrawLine(penCross, view.CrossV1, view.CrossV2);
            }
            using (var brushGreen = new SolidBrush(Color.LightGreen))
            {
                const float dotDiameter = 6f;
                float radius = dotDiameter / 2f;
                g.FillEllipse(brushGreen, view.Center.X - radius, view.Center.Y - radius, dotDiameter, dotDiameter);
                foreach (PointF pt in view.Corners)
                {
                    g.FillEllipse(brushGreen, pt.X - radius, pt.Y - radius, dotDiameter, dotDiameter);
                }
            }
        }

        /// <summary>绘制轮廓折线与特征点</summary>
        /// <param name="g">绘图表面</param>
        /// <param name="pts">轮廓点集</param>
        /// <param name="count">点数</param>
        /// <param name="view">本帧视图几何</param>
        /// <param name="hasCalib">校准数据是否可用</param>
        /// <param name="result">焊缝特征点</param>
        private static void DrawProfile(Graphics g, LineLaserProfileData pts, int count,
            ViewGeometry view, bool hasCalib, LineLaserSeamFeature result)
        {
            // 校准模式按原点 + 物理值×rate，与校准框共用同一变换；回退按范围缩放
            Func<float, float, PointF> trans = hasCalib
                ? (Func<float, float, PointF>)((y, z) => new PointF(view.Origin.X + y * view.Rate, view.Origin.Y - z * view.Rate))
                : (Func<float, float, PointF>)((y, z) => new PointF(Margin + (y - view.MinY) * view.FallbackScaleX,
                    CanvasH - Margin - (z - view.MinZ) * view.FallbackScaleY));

            int n = (count > LineLaserProfileData.MaxPoints) ? LineLaserProfileData.MaxPoints : count;
            var points = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                points[i] = trans((float)pts.Ys[i], (float)pts.Zs[i]);
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

        /// <summary>绘制毫米刻度栅格</summary>
        /// <param name="g">绘图表面</param>
        /// <param name="view">本帧视图几何</param>
        private static void DrawMillimeterGrid(Graphics g, ViewGeometry view)
        {
            // 可见区域对应的毫米范围：以毫米原点落点为基准反算
            float mmYMin = -view.Origin.X / view.Rate;
            float mmYMax = (CanvasW - view.Origin.X) / view.Rate;
            float mmZMin = (view.Origin.Y - CanvasH) / view.Rate;
            float mmZMax = view.Origin.Y / view.Rate;
            float stepMm = Math.Max(1f, (float)Math.Round(40f / view.Rate));

            using (var penGridDash = new Pen(Color.FromArgb(80, 80, 80), 1f))
            using (var fontGrid = new Font("Microsoft YaHei UI", 8.5f))
            using (var brushGrid = new SolidBrush(Color.FromArgb(170, 170, 170)))
            {
                penGridDash.DashStyle = DashStyle.Dash;

                int kStartY = (int)Math.Ceiling(mmYMin / stepMm);
                int kEndY = (int)Math.Floor(mmYMax / stepMm);
                for (int k = kStartY; k <= kEndY; k++)
                {
                    float mmVal = k * stepMm;
                    float sx = view.Origin.X + mmVal * view.Rate;
                    g.DrawLine(penGridDash, sx, 0f, sx, CanvasH);
                    string txt = string.Format("{0}", (int)Math.Round(mmVal));
                    g.DrawString(txt, fontGrid, brushGrid, sx + 2f, CanvasH - 4f - fontGrid.Height);
                }

                int kStartZ = (int)Math.Ceiling(mmZMin / stepMm);
                int kEndZ = (int)Math.Floor(mmZMax / stepMm);
                for (int k = kStartZ; k <= kEndZ; k++)
                {
                    float mmVal = k * stepMm;
                    float sy = view.Origin.Y - mmVal * view.Rate;
                    g.DrawLine(penGridDash, 0f, sy, CanvasW, sy);
                    string txt = string.Format("{0}", (int)Math.Round(mmVal));
                    g.DrawString(txt, fontGrid, brushGrid, 2f, sy + 1f);
                }
            }
        }

        /// <summary>回退模式标注轮廓 Y/Z 范围</summary>
        /// <param name="g">绘图表面</param>
        /// <param name="view">本帧视图几何</param>
        private static void DrawRangeLabels(Graphics g, ViewGeometry view)
        {
            using (var font = new Font("Microsoft YaHei UI", 9f))
            using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
            {
                string labelY = string.Format("Y:{0:F1}~{1:F1}mm", view.MinY, view.MaxY);
                g.DrawString(labelY, font, brush, Margin, CanvasH - Margin + 4f);
                var sf = new StringFormat
                {
                    FormatFlags = StringFormatFlags.DirectionVertical
                };
                string labelZ = string.Format("Z:{0:F1}~{1:F1}mm", view.MinZ, view.MaxZ);
                g.DrawString(labelZ, font, brush, 4f, Margin, sf);
            }
        }

        #endregion

        #region 公共函数

        /// <summary>把一帧轮廓渲染为 Mat</summary>
        /// <param name="profile">轮廓点集，为空时只清屏</param>
        /// <param name="calib">标定数据，HasData 为 false 时按轮廓范围自动缩放</param>
        /// <param name="result">焊缝特征点，有效时绘制特征点</param>
        /// <returns>渲染结果，调用方须 Clone 后再跨线程使用</returns>
        public Mat Render(LineLaserProfileData profile, LineLaserCalibrationData calib, LineLaserSeamFeature result)
        {
            lock (_lock)
            {
                if (_disposed) return null;

                EnsureCanvas();
                Graphics g = _canvasG;
                g.Clear(Color.FromArgb(30, 30, 30));
                g.SmoothingMode = SmoothingMode.AntiAlias;

                bool hasCalib = (calib != null) && calib.HasData;
                int count = (profile == null) ? 0 : profile.Count;

                // 视图几何一次算全，绘制段只读结果
                ViewGeometry view = BuildViewGeometry(calib, hasCalib, profile, count);
                if (!hasCalib && count >= 2) DrawFallbackGrid(g, view);
                if (hasCalib && view.Corners != null) DrawCalibrationBackdrop(g, view);
                if (count >= 2) DrawProfile(g, profile, count, view, hasCalib, result);
                if (hasCalib && view.Rate > 0f) DrawMillimeterGrid(g, view);
                if (!hasCalib && count >= 2) DrawRangeLabels(g, view);

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
                _canvasG?.Dispose();
                _canvasG = null;
                _canvasBmp?.Dispose();
                _canvasBmp = null;
                _mat?.Dispose();
                _mat = null;
                GlobalCommData.ShowLog(Tag, "轮廓渲染器已释放");
            }
        }

        #endregion

        #region 嵌套类型

        /// <summary>一帧渲染的视图几何</summary>
        private class ViewGeometry
        {
            public bool HasCalib;
            public float Rate = 1f;
            public PointF Center = PointF.Empty;
            public PointF Origin = PointF.Empty;
            public PointF[] Corners;
            public PointF CrossH1 = PointF.Empty;
            public PointF CrossH2 = PointF.Empty;
            public PointF CrossV1 = PointF.Empty;
            public PointF CrossV2 = PointF.Empty;
            public float FallbackScaleX;
            public float FallbackScaleY;
            public float MinY, MaxY, MinZ, MaxZ;
        }

        #endregion
    }
}
