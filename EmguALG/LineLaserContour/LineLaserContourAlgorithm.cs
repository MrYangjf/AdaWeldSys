using Emgu.CV;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.EmguALG.LineLaserContour
{
    /// <summary>
    /// 线激光轮廓 Mat 渲染算法实现（继承 Abstractions，输出 Mat 供 UI 显示）。
    /// 线激光只做采集识别，本类负责渲染输出。
    /// 画布一次性分配复用，每帧只清空重绘。
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
                Center = new PointF(CanvasW / 2f, CanvasH / 2f)
            };

            // 校准几何只依赖 calib（角点与 mm 跨度），与轮廓点数无关：
            // 须排在 count 判空之前，否则空帧会提前返回
            // 则 Corners 与 Rate 均为零，校准框与刻度不画
            if (hasCalib)
            {
                float cornerW = Math.Abs(calib.RightBottomCorner[0] - calib.LeftBottomCorner[0]);
                float cornerH = Math.Abs(calib.LeftTopCorner[1] - calib.LeftBottomCorner[1]);
                if (cornerW < 0.001f) cornerW = 1f;
                if (cornerH < 0.001f) cornerH = 1f;
                float fRate1 = (CanvasW - 2f * Margin) / cornerW;
                float fRate2 = (CanvasH - 2f * Margin) / cornerH;
                view.Rate = (fRate1 > fRate2) ? fRate2 : fRate1;

                // 中心直接取图像中心（英莱 demo 的 fPersent/dwOffset 公式会偏离中心）
                PointF center = new PointF(CanvasW / 2f, CanvasH / 2f);
                view.Center = center;
                PointF[] corners = new PointF[4];
                corners[0] = new PointF(center.X - Math.Abs(calib.LeftTopCorner[0] * view.Rate), center.Y - Math.Abs(calib.LeftTopCorner[1] * view.Rate));
                corners[1] = new PointF(center.X - Math.Abs(calib.LeftBottomCorner[0] * view.Rate), center.Y + Math.Abs(calib.LeftBottomCorner[1] * view.Rate));
                corners[2] = new PointF(center.X + Math.Abs(calib.RightBottomCorner[0] * view.Rate), center.Y + Math.Abs(calib.RightBottomCorner[1] * view.Rate));
                corners[3] = new PointF(center.X + Math.Abs(calib.RightTopCorner[0] * view.Rate), center.Y - Math.Abs(calib.RightTopCorner[1] * view.Rate));
                view.Corners = corners;

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
            // 校准模式按 center + 物理值×rate，回退按范围缩放
            Func<float, float, PointF> trans = hasCalib
                ? (Func<float, float, PointF>)((y, z) => new PointF(view.Center.X + y * view.Rate, view.Center.Y - z * view.Rate))
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
            float mmYMin = -CanvasW / (2f * view.Rate);
            float mmYMax = CanvasW / (2f * view.Rate);
            float mmZMin = -CanvasH / (2f * view.Rate);
            float mmZMax = CanvasH / (2f * view.Rate);
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
                    float sx = view.Center.X + mmVal * view.Rate;
                    g.DrawLine(penGridDash, sx, 0f, sx, CanvasH);
                    string txt = string.Format("{0}", (int)Math.Round(mmVal));
                    g.DrawString(txt, fontGrid, brushGrid, sx + 2f, CanvasH - 4f - fontGrid.Height);
                }

                int kStartZ = (int)Math.Ceiling(mmZMin / stepMm);
                int kEndZ = (int)Math.Floor(mmZMax / stepMm);
                for (int k = kStartZ; k <= kEndZ; k++)
                {
                    float mmVal = k * stepMm;
                    float sy = view.Center.Y - mmVal * view.Rate;
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

        /// <summary>
        /// 把一帧轮廓渲染为 Mat
        /// </summary>
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
