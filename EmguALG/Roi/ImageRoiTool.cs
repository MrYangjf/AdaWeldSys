using System;
using System.Drawing;
using System.IO;
using AdaWeldSystem.FileOperate;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace AdaWeldSystem.EmguALG.Roi
{
    /// <summary>
    /// 通用 ROI 工具（算法层）：ROI 的持久化与区域运算（裁剪/掩膜）。
    /// </summary>
    /// <remarks>只处理数据与图像，不引用 WinForms 控件或事件。
    /// 场景未定，当前不接任何算法；算法需要时按需调用，取用与否不影响其他算法。</remarks>
    public static class ImageRoiTool
    {
        #region 常量

        /// <summary>节名（沿用历史 ImageRoi.ini 的节名，保证旧文件可直接读取）</summary>
        private const string Section = "ImageROI";

        /// <summary>默认配置文件路径</summary>
        public static readonly string DefaultConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "EmguALG", "Algorithm_INI", "ImageRoi.ini");

        #endregion

        #region 持久化

        /// <summary>从配置文件读取 ROI，缺失时返回空区域</summary>
        /// <returns>ROI 数据，非 null</returns>
        public static ImageRoi Load()
        {
            ImageRoi roi = new ImageRoi();
            if (!File.Exists(DefaultConfigPath)) return roi;

            INIFile ini = new INIFile(DefaultConfigPath);
            roi.Enabled = ini.ReadBool(Section, "Enabled", false);
            roi.X = ini.ReadInt(Section, "X", 0);
            roi.Y = ini.ReadInt(Section, "Y", 0);
            roi.Width = ini.ReadInt(Section, "Width", 0);
            roi.Height = ini.ReadInt(Section, "Height", 0);
            return roi;
        }

        /// <summary>把 ROI 写入默认配置文件（目录不存在时自动创建）</summary>
        /// <param name="roi">ROI 数据；null 时不写</param>
        public static void Save(ImageRoi roi)
        {
            if (roi == null) return;

            string directory = Path.GetDirectoryName(DefaultConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            INIFile ini = new INIFile(DefaultConfigPath);
            ini.WriteInt(Section, "X", roi.X);
            ini.WriteInt(Section, "Y", roi.Y);
            ini.WriteInt(Section, "Width", roi.Width);
            ini.WriteInt(Section, "Height", roi.Height);
            ini.WriteBool(Section, "Enabled", roi.Enabled);
            ini.SaveToFile();
        }

        #endregion

        #region 区域运算

        /// <summary>把 ROI 裁剪到源图范围内；未启用或完全越界时返回 null（表示按整幅图执行）</summary>
        /// <param name="source">源图</param>
        /// <param name="roi">ROI</param>
        /// <returns>可直接用于区域运算的 ROI，或 null</returns>
        private static ImageRoi Resolve(Mat source, ImageRoi roi)
        {
            if (roi == null || !roi.IsActive) return null;

            ImageRoi clamped = roi.ClampTo(source.Width, source.Height);
            return clamped.IsValid ? clamped : null;
        }

        /// <summary>按 ROI 裁剪出子图（返回新 Mat，调用方负责释放）</summary>
        /// <param name="source">源图</param>
        /// <param name="roi">ROI；未启用或无效时返回整幅副本</param>
        /// <returns>裁剪结果；source 为 null 时返回 null</returns>
        public static Mat Crop(Mat source, ImageRoi roi)
        {
            if (source == null) return null;

            ImageRoi active = Resolve(source, roi);
            if (active == null) return source.Clone();

            Rectangle keep = active.Bounds;
            Mat crop = new Mat(keep.Height, keep.Width, source.Depth, source.NumberOfChannels);
            using (Mat view = new Mat(source, keep))
            {
                view.CopyTo(crop);
            }
            return crop;
        }

        /// <summary>按 ROI 生成掩膜图（新 Mat，调用方释放）；区域内保留原值</summary>
        /// <param name="source">源图</param>
        /// <param name="roi">ROI；未启用或无效时返回整幅副本</param>
        /// <returns>掩膜结果；source 为 null 时返回 null</returns>
        public static Mat ApplyMask(Mat source, ImageRoi roi)
        {
            if (source == null) return null;

            ImageRoi active = Resolve(source, roi);
            if (active == null) return source.Clone();

            Mat masked = new Mat();
            using (Mat mask = Mat.Zeros(source.Rows, source.Cols, DepthType.Cv8U, 1))
            {
                CvInvoke.Rectangle(mask, active.Bounds, new MCvScalar(255), -1);
                CvInvoke.BitwiseAnd(source, source, masked, mask);
            }
            return masked;
        }

        #endregion
    }
}
