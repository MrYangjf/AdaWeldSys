using AdaWeldSystem.PCLOperate.Models;

namespace AdaWeldSystem.EmguALG.Core
{
    /// <summary>
    /// mm ↔ px 唯一换算点（算法架构设计 §5）。
    /// 由当前帧点云范围 + 目标图像尺寸计算；scale = px/mm，offset 为像素平移。
    /// 同一帧内所有 mm↔px 换算必须复用同一变换，保证一致性。
    /// 测量空间(mm) 与 图像空间(px) 的边界换算只发生在此处，业务全程在 mm 空间计算。
    /// </summary>
    public sealed class ProfileTransform
    {
        public double Scale { get; private set; }   // px per mm
        public double OffsetX { get; private set; } // px
        public double OffsetY { get; private set; } // px
        public int ImgW { get; private set; }
        public int ImgH { get; private set; }

        private double _minY; // 点云 Y 最小值（mm），ToPixel/ToMM 的基准
        private double _minZ; // 点云 Z 最小值（mm）

        public ProfileTransform(double scale, double offX, double offY, int imgW, int imgH, double minY, double minZ)
        {
            Scale = scale;
            OffsetX = offX;
            OffsetY = offY;
            ImgW = imgW;
            ImgH = imgH;
            _minY = minY;
            _minZ = minZ;
        }

        /// <summary>
        /// 由点云构建 mm↔px 变换。margin 为图像四周留白（像素）。
        /// </summary>
        public static ProfileTransform FromPointCloud(PointCloudData pc, int imgW, int imgH, int margin)
        {
            float minX, minY, minZ, maxX, maxY, maxZ;
            pc.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);

            float yRange = maxY - minY;
            float zRange = maxZ - minZ;
            if (yRange < 0.001f) yRange = 0.001f;
            if (zRange < 0.001f) zRange = 0.001f;

            double scale = System.Math.Min(
                (imgW - 2.0 * margin) / (double)yRange,
                (imgH - 2.0 * margin) / (double)zRange);
            double offX = (imgW - yRange * scale) / 2.0;
            double offY = (imgH - zRange * scale) / 2.0;

            return new ProfileTransform(scale, offX, offY, imgW, imgH, minY, minZ);
        }

        /// <summary>mm(Y,Z) → 像素(列,行)。行坐标自底向上（图像原点在左上）。</summary>
        public System.Drawing.Point ToPixel(double yMm, double zMm)
        {
            int col = (int)((yMm - _minY) * Scale + OffsetX);
            int row = ImgH - (int)((zMm - _minZ) * Scale + OffsetY);
            return new System.Drawing.Point(col, row);
        }

        /// <summary>像素(列,行) → mm(Y,Z)。</summary>
        public void ToMM(int col, int row, out double yMm, out double zMm)
        {
            yMm = (col - OffsetX) / Scale + _minY;
            zMm = (ImgH - row - OffsetY) / Scale + _minZ;
        }
    }
}
