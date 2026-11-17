using System.Drawing;

namespace AdaWeldSystem.EmguALG.Roi
{
    /// <summary>
    /// ROI 区域数据（纯数据，不含界面控件与事件）。
    /// </summary>
    /// <remarks>算法只在指定矩形区域内执行。
    /// 区域运算（裁剪/掩膜）见 <see cref="ImageRoiTool"/>；本类只描述区域本身与坐标映射。</remarks>
    public sealed class ImageRoi
    {
        #region 公共变量

        /// <summary>是否启用（未启用时算法按整幅图执行）</summary>
        public bool Enabled { get; set; }

        /// <summary>区域左上角 X（源图坐标，像素）</summary>
        public int X { get; set; }

        /// <summary>区域左上角 Y（源图坐标，像素）</summary>
        public int Y { get; set; }

        /// <summary>区域宽度（像素）</summary>
        public int Width { get; set; }

        /// <summary>区域高度（像素）</summary>
        public int Height { get; set; }

        /// <summary>区域矩形（宽高无效时为 <see cref="Rectangle.Empty"/>）</summary>
        public Rectangle Bounds
        {
            get
            {
                if (!IsValid) return Rectangle.Empty;
                return new Rectangle(X, Y, Width, Height);
            }
        }

        /// <summary>宽高是否为正（不校验是否落在图内）</summary>
        public bool IsValid
        {
            get { return Width > 0 && Height > 0; }
        }

        /// <summary>是否可直接参与区域运算（已启用且宽高有效）</summary>
        public bool IsActive
        {
            get { return Enabled && IsValid; }
        }

        #endregion

        #region 公共函数

        /// <summary>裁剪到图像范围内，完全越界时返回无效区域</summary>
        /// <param name="imageWidth">图像宽度</param>
        /// <param name="imageHeight">图像高度</param>
        /// <returns>裁剪后的新区域；原对象不被修改</returns>
        public ImageRoi ClampTo(int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0) return new ImageRoi { Enabled = Enabled };
            if (!IsValid) return new ImageRoi { Enabled = Enabled };

            int left = (X < 0) ? 0 : X;
            int top = (Y < 0) ? 0 : Y;
            int right = (X + Width > imageWidth) ? imageWidth : X + Width;
            int bottom = (Y + Height > imageHeight) ? imageHeight : Y + Height;

            return new ImageRoi
            {
                Enabled = Enabled,
                X = left,
                Y = top,
                Width = (right > left) ? right - left : 0,
                Height = (bottom > top) ? bottom - top : 0
            };
        }

        /// <summary>区域内坐标 → 源图坐标</summary>
        /// <param name="roiX">区域内的 X</param>
        /// <param name="roiY">区域内的 Y</param>
        /// <returns>源图坐标</returns>
        public Point MapToSource(int roiX, int roiY)
        {
            return new Point(X + roiX, Y + roiY);
        }

        /// <summary>区域内坐标 → 源图坐标</summary>
        /// <param name="roiPoint">区域内坐标</param>
        /// <returns>源图坐标</returns>
        public Point MapToSource(Point roiPoint)
        {
            return new Point(X + roiPoint.X, Y + roiPoint.Y);
        }

        /// <summary>源图坐标 → 区域坐标</summary>
        /// <param name="sourceX">源图 X</param>
        /// <param name="sourceY">源图 Y</param>
        /// <returns>区域坐标（可能落在区域外，调用方自行判定）</returns>
        public Point MapToRoi(int sourceX, int sourceY)
        {
            return new Point(sourceX - X, sourceY - Y);
        }

        /// <summary>源图坐标 → 区域坐标</summary>
        /// <param name="sourcePoint">源图坐标</param>
        /// <returns>区域坐标（可能落在区域外，调用方自行判定）</returns>
        public Point MapToRoi(Point sourcePoint)
        {
            return new Point(sourcePoint.X - X, sourcePoint.Y - Y);
        }

        #endregion
    }
}
