using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WireFeedDistance;
using AdaWeldSystem.EmguALG.RealtimeMonitor.WeldQuality;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>图像算法统一入口，均须在此文件实现。 分为预处</summary>
    public static class ImageAlgorithm
    {
        #region 2. 预处理算法区

        public static Mat GaussianBlur(Mat image, int ksize = 5, double sigmaX = 1.5)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat result = new Mat();
            CvInvoke.GaussianBlur(image, result, new Size(ksize, ksize), sigmaX);
            return result;
        }

        public static Mat MedianBlur(Mat image, int ksize = 5)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            if (ksize % 2 == 0) ksize++;
            Mat result = new Mat();
            CvInvoke.MedianBlur(image, result, ksize);
            return result;
        }

        public static Mat MeanBlur(Mat image, int ksize = 5)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            if (ksize % 2 == 0) ksize++;
            Mat result = new Mat();
            CvInvoke.BoxFilter(image, result, DepthType.Default, new Size(ksize, ksize), new Point(-1, -1), true);
            return result;
        }

        public static Mat BilateralFilter(Mat image, int d = 9, double sigmaColor = 75, double sigmaSpace = 75)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat result = new Mat();
            CvInvoke.BilateralFilter(image, result, d, sigmaColor, sigmaSpace);
            return result;
        }

        public static Mat Sharpen(Mat image)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            float[,] kernelData = new float[3, 3]
            {
                { 0, -1, 0 },
                { -1, 5, -1 },
                { 0, -1, 0 }
            };
            ConvolutionKernelF kernel = new ConvolutionKernelF(kernelData);
            Mat result = new Mat();
            CvInvoke.Filter2D(image, result, kernel, new Point(-1, -1));
            return result;
        }

        #endregion

        #region 3. 边缘检测算法区

        public static Mat CannyEdge(Mat image, double threshold1 = 50, double threshold2 = 150, int apertureSize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat gray = ConvertToGray(image);
            Mat edges = new Mat();
            CvInvoke.Canny(gray, edges, threshold1, threshold2, apertureSize);
            return edges;
        }

        public static Mat GradientEdge(Mat image, int ksize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat gray = ConvertToGray(image);
            Mat gradX = new Mat(), gradY = new Mat();
            CvInvoke.Sobel(gray, gradX, DepthType.Cv16S, 1, 0, ksize);
            CvInvoke.Sobel(gray, gradY, DepthType.Cv16S, 0, 1, ksize);
            Mat absX = new Mat(), absY = new Mat();
            CvInvoke.ConvertScaleAbs(gradX, absX, 1.0, 0);
            CvInvoke.ConvertScaleAbs(gradY, absY, 1.0, 0);
            Mat result = new Mat();
            CvInvoke.AddWeighted(absX, 0.5, absY, 0.5, 0, result);
            return result;
        }

        public static Mat LaplacianEdge(Mat image, int ksize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat gray = ConvertToGray(image);
            Mat lap = new Mat();
            CvInvoke.Laplacian(gray, lap, DepthType.Cv16S, ksize);
            Mat result = new Mat();
            CvInvoke.ConvertScaleAbs(lap, result, 1.0, 0);
            return result;
        }

        public static Mat RobertsEdge(Mat image)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat gray = ConvertToGray(image);
            float[,] kx = { { 1, 0 }, { 0, -1 } };
            float[,] ky = { { 0, 1 }, { -1, 0 } };
            ConvolutionKernelF kernelX = new ConvolutionKernelF(kx);
            ConvolutionKernelF kernelY = new ConvolutionKernelF(ky);
            Mat gradX = new Mat(), gradY = new Mat();
            CvInvoke.Filter2D(gray, gradX, kernelX, new Point(-1, -1));
            CvInvoke.Filter2D(gray, gradY, kernelY, new Point(-1, -1));
            Mat absX = new Mat(), absY = new Mat();
            CvInvoke.ConvertScaleAbs(gradX, absX, 1.0, 0.0);
            CvInvoke.ConvertScaleAbs(gradY, absY, 1.0, 0.0);
            Mat result = new Mat();
            CvInvoke.AddWeighted(absX, 0.5, absY, 0.5, 0, result);
            return result;
        }

        #endregion

        #region 4. 形态学算法区

        public static Mat Dilate(Mat image, int ksize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat kernel = CvInvoke.GetStructuringElement(MorphShapes.Rectangle, new Size(ksize, ksize), new Point(-1, -1));
            Mat result = new Mat();
            CvInvoke.Dilate(image, result, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
            return result;
        }

        public static Mat Erode(Mat image, int ksize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat kernel = CvInvoke.GetStructuringElement(MorphShapes.Rectangle, new Size(ksize, ksize), new Point(-1, -1));
            Mat result = new Mat();
            CvInvoke.Erode(image, result, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
            return result;
        }

        public static Mat MorphologyOpen(Mat image, int ksize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat kernel = CvInvoke.GetStructuringElement(MorphShapes.Rectangle, new Size(ksize, ksize), new Point(-1, -1));
            Mat result = new Mat();
            CvInvoke.MorphologyEx(image, result, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
            return result;
        }

        public static Mat MorphologyClose(Mat image, int ksize = 3)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat kernel = CvInvoke.GetStructuringElement(MorphShapes.Rectangle, new Size(ksize, ksize), new Point(-1, -1));
            Mat result = new Mat();
            CvInvoke.MorphologyEx(image, result, MorphOp.Close, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
            return result;
        }

        #endregion

        #region 5. 特征提取算法区

        /// <summary>查找图像中的所有轮廓</summary>
        public static VectorOfVectorOfPoint FindContours(Mat image, double binaryThreshold = 127, 
            ChainApproxMethod approxMethod = ChainApproxMethod.ChainApproxSimple)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat gray = ConvertToGray(image);
            Mat binary = new Mat();
            CvInvoke.Threshold(gray, binary, binaryThreshold, 255, ThresholdType.Binary);
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat hierarchy = new Mat();
            CvInvoke.FindContours(binary, contours, hierarchy, RetrType.External, approxMethod);
            return contours;
        }

        /// <summary>在图像上绘制轮廓</summary>
        public static Mat DrawContours(Mat image, VectorOfVectorOfPoint contours, int thickness = 2)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat result;
            if (image.NumberOfChannels == 1)
            {
                result = new Mat();
                CvInvoke.CvtColor(image, result, ColorConversion.Gray2Bgr);
            }
            else
                result = image.Clone();

            MCvScalar color = new MCvScalar(0, 255, 0);
            for (int i = 0; i < contours.Size; i++)
                CvInvoke.DrawContours(result, contours, i, color, thickness);
            return result;
        }

        /// <summary>提取骨架</summary>
        public static Mat ExtractSkeleton(Mat image, double binaryThreshold = 127, int maxIter = 100)
        {
            if (image == null || image.IsEmpty) throw new ArgumentException("输入图像无效");
            Mat binary = ConvertToBinary(image, binaryThreshold);
            Mat skeleton = binary.Clone();
            Mat temp = new Mat();
            Mat element = CvInvoke.GetStructuringElement(MorphShapes.Cross, new Size(3, 3), new Point(1, 1));

            int iter = 0;
            bool done = false;
            while (!done && iter < maxIter)
            {
                CvInvoke.Erode(skeleton, temp, element, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
                CvInvoke.Dilate(temp, temp, element, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
                Mat diff = new Mat();
                CvInvoke.Subtract(skeleton, temp, diff);

                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
                using (Mat hierarchy = new Mat())
                {
                    CvInvoke.FindContours(diff, contours, hierarchy, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    if (contours.Size == 0)
                        done = true;
                }

                skeleton = temp.Clone();
                iter++;
            }

            return skeleton;
        }

        /// <summary>计算焊缝宽度（轮廓水平方向最大跨度）</summary>
        /// <returns>计算结果。</returns>
        public static double CalculateSeamWidth(VectorOfPoint contour)
        {
            if (contour == null || contour.Size == 0) return 0;
            RotatedRect minRect = CvInvoke.MinAreaRect(contour);
            return Math.Min(minRect.Size.Width, minRect.Size.Height);
        }

        /// <summary>计算焊缝截面积</summary>
        /// <returns>计算结果。</returns>
        public static double CalculateSeamArea(VectorOfPoint contour)
        {
            if (contour == null || contour.Size == 0) return 0;
            return CvInvoke.ContourArea(contour);
        }

        /// <summary>计算焊缝中心点</summary>
        public static PointF CalculateWeldPoint(VectorOfPoint contour)
        {
            if (contour == null || contour.Size == 0) return PointF.Empty;
            Moments moments = CvInvoke.Moments(contour);
            if (moments.M00 == 0) return PointF.Empty;
            return new PointF((float)(moments.M10 / moments.M00), (float)(moments.M01 / moments.M00));
        }

        #endregion

        #region 6. 模板匹配算法区

        public static Mat TemplateMatch(Mat image, Mat template, TemplateMatchingType method, 
            bool showResult, out string resultInfo)
        {
            resultInfo = "";
            bool matchSuccess;
            Point matchLoc;
            double matchValue;

            Mat output;
            if (image.NumberOfChannels == 1)
            {
                output = new Mat();
                CvInvoke.CvtColor(image, output, ColorConversion.Gray2Bgr);
            }
            else
                output = image.Clone();

            if (template == null || template.IsEmpty)
            {
                resultInfo = "模板无效";
                if (showResult) DrawTextOnImage(output, resultInfo);
                return output;
            }

            Mat srcGray = ConvertToGray(image);
            Mat templGray = ConvertToGray(template);

            Mat result = new Mat();
            try
            {
                CvInvoke.MatchTemplate(srcGray, templGray, result, method);
                double minVal = 0, maxVal = 0;
                Point minLoc = new Point(), maxLoc = new Point();
                CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

                if (method == TemplateMatchingType.SqdiffNormed)
                {
                    matchLoc = minLoc;
                    matchValue = 1.0 - minVal;
                    matchSuccess = (minVal <= 0.5);
                }
                else
                {
                    matchLoc = maxLoc;
                    matchValue = maxVal;
                    matchSuccess = (maxVal >= 0.5);
                }

                resultInfo = matchSuccess
                    ? string.Format("匹配成功 位置 X {0} Y {1} 相似度 {2:F3}", matchLoc.X, matchLoc.Y, matchValue)
                    : string.Format("匹配失败 最佳相似度 {0:F3}", matchValue);

                if (showResult)
                {
                    if (matchSuccess)
                    {
                        Rectangle rect = new Rectangle(matchLoc, template.Size);
                        CvInvoke.Rectangle(output, rect, new MCvScalar(0, 255, 0), 2);
                        DrawTextOnImage(output, resultInfo, new Point(10, 30), new MCvScalar(0, 255, 0));
                    }
                    else
                    {
                        DrawTextOnImage(output, resultInfo, new Point(10, 30), new MCvScalar(0, 0, 255));
                    }
                }
            }
            catch (Exception ex)
            {
                resultInfo = $"匹配异常 {ex.Message}";
                if (showResult) DrawTextOnImage(output, resultInfo, new Point(10, 30), new MCvScalar(0, 0, 255));
            }

            return output;
        }

        #endregion

        #region 7. 辅助工具区

        private static Mat ConvertToGray(Mat image)
        {
            if (image.NumberOfChannels == 1) return image;
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);
            return gray;
        }

        private static Mat ConvertToBinary(Mat image, double threshold = 127, double maxVal = 255)
        {
            Mat gray = ConvertToGray(image);
            Mat binary = new Mat();
            CvInvoke.Threshold(gray, binary, threshold, maxVal, ThresholdType.Binary);
            return binary;
        }

         private static void DrawTextOnImage(Mat image, string text, Point origin, MCvScalar color)
        {
            FontFace font = FontFace.HersheySimplex;
            double fontScale = 0.5;
            int thickness = 1;
            int baseline = 0;
            Size textSize = CvInvoke.GetTextSize(text, font, fontScale, thickness, ref baseline);
            if (origin.X + textSize.Width > image.Width)
                origin.X = image.Width - textSize.Width - 5;
            if (origin.Y - textSize.Height < 0)
                origin.Y = textSize.Height + 5;
            CvInvoke.PutText(image, text, origin, font, fontScale, color, thickness);
        }

        private static void DrawTextOnImage(Mat image, string text)
        {
            int baseline = 0;
            Size textSize = CvInvoke.GetTextSize(text, FontFace.HersheySimplex, 0.6, 1, ref baseline);
            Point origin = new Point((image.Width - textSize.Width) / 2, 50);
            DrawTextOnImage(image, text, origin, new MCvScalar(0, 0, 255));
        }

        /// <summary>在默认范围内绘制点云数据（兼容旧版AlgorithmLibrary）</summary>
        public static Mat DrawPointsDefaultRange(List<PointF> points)
        {
            if (points == null || points.Count == 0)
                return new Mat(480, 640, DepthType.Cv8U, 3);

            Mat image = new Mat(480, 640, DepthType.Cv8U, 3);
            image.SetTo(new MCvScalar(0, 0, 0));

            // 计算点的范围
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var pt in points)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            // 添加边距
            float marginX = (maxX - minX) * 0.1f;
            float marginY = (maxY - minY) * 0.1f;
            minX -= marginX; maxX += marginX;
            minY -= marginY; maxY += marginY;

            // 防止除零
            if (Math.Abs(maxX - minX) < 0.001f) { maxX = minX + 1; }
            if (Math.Abs(maxY - minY) < 0.001f) { maxY = minY + 1; }

            // 绘制点
            foreach (var pt in points)
            {
                int x = (int)((pt.X - minX) / (maxX - minX) * (image.Width - 20) + 10);
                int y = image.Height - (int)((pt.Y - minY) / (maxY - minY) * (image.Height - 20) + 10);
                CvInvoke.Circle(image, new Point(x, y), 2, new MCvScalar(0, 255, 0), -1);
            }

            return image;
        }

        /// <summary>在图像上绘制水平边界线（兼容旧版AlgorithmLibrary）</summary>
        public static void DrawHorizontalBoundsDefaultRange(Mat image, float upperBound, float lowerBound)
        {
            if (image == null || image.IsEmpty) return;

            int yUpper = (int)(image.Height * 0.1);
            int yLower = (int)(image.Height * 0.9);

            CvInvoke.Line(image, new Point(0, yUpper), new Point(image.Width, yUpper), new MCvScalar(0, 0, 255), 2);
            CvInvoke.Line(image, new Point(0, yLower), new Point(image.Width, yLower), new MCvScalar(0, 0, 255), 2);

            // 绘制边界值文字
            CvInvoke.PutText(image, upperBound.ToString("F2"), new Point(5, yUpper - 5), FontFace.HersheySimplex, 0.5, new MCvScalar(0, 0, 255), 1);
            CvInvoke.PutText(image, lowerBound.ToString("F2"), new Point(5, yLower + 15), FontFace.HersheySimplex, 0.5, new MCvScalar(0, 0, 255), 1);
        }

        #endregion

        #region 8. 监控相机算法区（焊前对中 / 焊中质量检测）

        /// <summary>生成叠加绘制用的彩色 Mat（输入为灰度时转为 BGR，便于</summary>
        private static Mat PrepareOverlayMat(Mat image)
        {
            if (image.NumberOfChannels == 1)
            {
                Mat bgr = new Mat();
                CvInvoke.CvtColor(image, bgr, ColorConversion.Gray2Bgr);
                return bgr;
            }
            return image.Clone();
        }

        public static WireFeedResult DetectPreWeldAlignment(Mat image, WireFeedConfig config)
        {
            var result = new WireFeedResult();
            if (image == null || image.IsEmpty)
            {
                result.Aligned = false;
                return result;
            }

            Mat work = PrepareOverlayMat(image);
            Mat gray = ConvertToGray(image);

            // 激光光斑：最亮区域（>240）
            PointF laserCenter = PointF.Empty;
            using (Mat laserMask = new Mat())
            using (Mat h = new Mat())
            using (var laserContours = new VectorOfVectorOfPoint())
            {
                CvInvoke.Threshold(gray, laserMask, 240, 255, ThresholdType.Binary);
                CvInvoke.FindContours(laserMask, laserContours, h, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                if (laserContours.Size > 0)
                {
                    int idx = 0; double maxA = 0;
                    for (int i = 0; i < laserContours.Size; i++)
                    {
                        double a = CvInvoke.ContourArea(laserContours[i]);
                        if (a > maxA) { maxA = a; idx = i; }
                    }
                    var m = CvInvoke.Moments(laserContours[idx]);
                    if (m.M00 > 0)
                        laserCenter = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));
                }
            }

            // 焊丝：较亮区域（150~245，排除激光光斑），取最大轮廓
            PointF wireCenter = PointF.Empty;
            double wireAngle = 0;
            using (Mat wireMask = new Mat())
            using (Mat h2 = new Mat())
            using (var wireContours = new VectorOfVectorOfPoint())
            {
                CvInvoke.InRange(gray, new ScalarArray(new MCvScalar(150)), new ScalarArray(new MCvScalar(245)), wireMask);
                CvInvoke.FindContours(wireMask, wireContours, h2, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                if (wireContours.Size > 0)
                {
                    int idx = 0; double maxA = 0;
                    for (int i = 0; i < wireContours.Size; i++)
                    {
                        double a = CvInvoke.ContourArea(wireContours[i]);
                        if (a > maxA) { maxA = a; idx = i; }
                    }
                    var m = CvInvoke.Moments(wireContours[idx]);
                    if (m.M00 > 0)
                        wireCenter = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));
                    RotatedRect rr = CvInvoke.MinAreaRect(wireContours[idx]);
                    wireAngle = rr.Angle;
                }
            }

            // 毫米化偏差 + 三级判定 + 剪丝判据
            result.WireMatched = !wireCenter.IsEmpty;
            result.WireHeadAdhesion = laserCenter.IsEmpty;
            result.LaserCenter = laserCenter;
            result.WireCenter = wireCenter;
            result.AngleDeg = wireAngle;

            if (laserCenter.IsEmpty || wireCenter.IsEmpty)
            {
                result.Aligned = false;
                result.Grade = WireAlignGrade.Fail;
                DrawTextOnImage(work, "检测失败 未找到激光光斑或焊丝", new Point(10, 30), new MCvScalar(0, 0, 255));
                result.OverlayMat = work;
                return result;
            }

            double dx = laserCenter.X - wireCenter.X;
            double dy = laserCenter.Y - wireCenter.Y;
            result.OffsetX = dx;
            result.OffsetY = dy;
            result.DevXMm = dx * config.PixelMmScale;
            result.DevYMm = dy * config.PixelMmScale;

            // 轴向映射：送丝方向 ↔ 水平像素轴（可配换轴/换符号）；余下轴 = 横向只报警
            double stickoutPx = config.StickoutAxisHorizontal ? dx : dy;
            double lateralMm = (config.StickoutAxisHorizontal ? result.DevYMm : result.DevXMm);
            result.WireStickoutDevMm = stickoutPx * config.PixelMmScale * config.StickoutSign;
            result.LateralDevMm = lateralMm;

            double lateralAbs = Math.Abs(lateralMm);
            result.Grade = lateralAbs <= config.CenterWarnMm ? WireAlignGrade.Pass
                         : lateralAbs <= config.CenterFailMm ? WireAlignGrade.Warn
                         : WireAlignGrade.Fail;
            bool aligned = result.Grade == WireAlignGrade.Pass;
            result.Aligned = aligned;

            CvInvoke.Circle(work, new Point((int)laserCenter.X, (int)laserCenter.Y), 6, new MCvScalar(0, 0, 255), -1);
            CvInvoke.Circle(work, new Point((int)wireCenter.X, (int)wireCenter.Y), 6, new MCvScalar(0, 255, 0), -1);
            CvInvoke.Line(work, new Point((int)wireCenter.X, (int)wireCenter.Y),
                new Point((int)laserCenter.X, (int)laserCenter.Y), new MCvScalar(255, 255, 0), 2);
            string info = string.Format("丝偏 {0:F2}mm 横偏 {1:F2}mm 角度 {2:F1} 判定 {3}",
                result.WireStickoutDevMm, lateralMm, wireAngle, result.Grade);
            DrawTextOnImage(work, info, new Point(10, 30), aligned ? new MCvScalar(0, 255, 0) : new MCvScalar(0, 0, 255));
            result.OverlayMat = work;
            return result;
        }

        /// <summary>焊中焊接质量检测 输入 2D 图，识别焊道并检测缺陷（</summary>

        /// <param name="config">配置对象。</param>
        public static WeldQualityResult DetectWeldQuality(Mat image, WeldQualityConfig config)
        {
            var result = new WeldQualityResult();
            if (image == null || image.IsEmpty)
            {
                result.Pass = false;
                return result;
            }

            Mat work = PrepareOverlayMat(image);
            Mat gray = ConvertToGray(image);

            using (Mat beadMask = new Mat())
            using (Mat h = new Mat())
            using (var beadContours = new VectorOfVectorOfPoint())
            {
                CvInvoke.Threshold(gray, beadMask, 120, 255, ThresholdType.Binary);
                CvInvoke.FindContours(beadMask, beadContours, h, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                if (beadContours.Size == 0)
                {
                    result.Pass = false;
                    DrawTextOnImage(work, "检测失败 未找到焊道", new Point(10, 30), new MCvScalar(0, 0, 255));
                    result.OverlayMat = work;
                    return result;
                }

                int idx = 0; double maxA = 0;
                for (int i = 0; i < beadContours.Size; i++)
                {
                    double a = CvInvoke.ContourArea(beadContours[i]);
                    if (a > maxA) { maxA = a; idx = i; }
                }
                Rectangle beadRect = CvInvoke.BoundingRectangle(beadContours[idx]);
                CvInvoke.Rectangle(work, beadRect, new MCvScalar(0, 255, 255), 1);

                using (Mat roi = new Mat(gray, beadRect))
                using (Mat defectMask = new Mat())
                using (var defContours = new VectorOfVectorOfPoint())
                {
                    CvInvoke.Threshold(roi, defectMask, 80, 255, ThresholdType.BinaryInv);
                    CvInvoke.FindContours(defectMask, defContours, h, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    int minDefectArea = 20;
                    for (int i = 0; i < defContours.Size; i++)
                    {
                        double a = CvInvoke.ContourArea(defContours[i]);
                        if (a < minDefectArea) continue;
                        Rectangle b = CvInvoke.BoundingRectangle(defContours[i]);
                        b.X += beadRect.X;
                        b.Y += beadRect.Y;
                        double ar = (double)b.Width / Math.Max(1, b.Height);
                        WeldDefectType t = (ar > 3 || ar < 0.33) ? WeldDefectType.Crack : WeldDefectType.Porosity;
                        result.Defects.Add(new WeldDefect { Type = t, Bounds = b, Area = a });
                        CvInvoke.Rectangle(work, b, new MCvScalar(0, 0, 255), 2);
                    }
                }
            }

            result.DefectCount = result.Defects.Count;
            double score = 100 - result.Defects.Count * 15;
            if (score < 0) score = 0;
            result.QualityScore = score;
            result.Pass = score >= config.PassScore;
            string info = string.Format("缺陷数 {0} 质量分 {1:F0} 判定 {2}",
                result.DefectCount, score, result.Pass ? "OK" : "NG");
            DrawTextOnImage(work, info, new Point(10, 30), result.Pass ? new MCvScalar(0, 255, 0) : new MCvScalar(0, 0, 255));
            result.OverlayMat = work;
            return result;
        }

        #endregion
    }

    #region 监控相机数据模型（焊前对中 / 焊中质量，命名空间级别供外部模块引用）
    /// <summary>焊接缺陷类型</summary>
    public enum WeldDefectType
    {
        None,
        Porosity,   // 气孔
        Crack,      // 裂纹
        Undercut    // 咬边
    }

    /// <summary>单个焊接缺陷</summary>
    public class WeldDefect
    {
        public WeldDefectType Type { get; set; }
        public Rectangle Bounds { get; set; }
        public double Area { get; set; }
    }

    #endregion
}
