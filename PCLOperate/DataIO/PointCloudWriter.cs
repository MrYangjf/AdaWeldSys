using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AdaWeldSystem.PCLOperate.DataIO
{
    /// <summary>
    /// 点云文件写入器
    /// </summary>
    public static class PointCloudWriter
    {
        /// <summary>
        /// 根据文件扩展名自动选择保存方法
        /// </summary>
        public static void SaveToFile(PointCloudData data, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            switch (extension)
            {
                case ".txt":
                case ".xyz":
                    SaveToTxt(data, filePath);
                    break;
                case ".ply":
                    SaveToPly(data, filePath);
                    break;
                case ".obj":
                    SaveToObj(data, filePath);
                    break;
                default:
                    throw new NotSupportedException($"不支持的文件格式: {extension}");
            }
        }

        /// <summary>
        /// 保存为TXT/XYZ格式
        /// </summary>
        public static void SaveToTxt(PointCloudData data, string filePath, bool includeColor = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Point Cloud Data");
            sb.AppendLine($"# Points: {data.PointCount}");

            if (data.HasColor && includeColor)
            {
                foreach (var point in data.ColoredPoints)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F6} {1:F6} {2:F6} {3} {4} {5}",
                        point.X, point.Y, point.Z, point.R, point.G, point.B));
                }
            }
            else
            {
                var points = data.HasColor ? data.ColoredPoints.Select(p => new Point3D(p.X, p.Y, p.Z)).ToList() : data.Points;
                foreach (var point in points)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F6} {1:F6} {2:F6}",
                        point.X, point.Y, point.Z));
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 保存为PLY格式
        /// </summary>
        public static void SaveToPly(PointCloudData data, string filePath)
        {
            var sb = new StringBuilder();
            bool hasColor = data.HasColor;

            // 写入头部
            sb.AppendLine("ply");
            sb.AppendLine("format ascii 1.0");
            sb.AppendLine($"element vertex {data.PointCount}");
            sb.AppendLine("property float x");
            sb.AppendLine("property float y");
            sb.AppendLine("property float z");

            if (hasColor)
            {
                sb.AppendLine("property uchar red");
                sb.AppendLine("property uchar green");
                sb.AppendLine("property uchar blue");
            }

            sb.AppendLine("end_header");

            // 写入顶点数据
            if (hasColor)
            {
                foreach (var point in data.ColoredPoints)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F6} {1:F6} {2:F6} {3} {4} {5}",
                        point.X, point.Y, point.Z, point.R, point.G, point.B));
                }
            }
            else
            {
                foreach (var point in data.Points)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F6} {1:F6} {2:F6}",
                        point.X, point.Y, point.Z));
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 保存为OBJ格式
        /// </summary>
        public static void SaveToObj(PointCloudData data, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Point Cloud Data");
            sb.AppendLine($"# Points: {data.PointCount}");
            sb.AppendLine();

            if (data.HasColor)
            {
                foreach (var point in data.ColoredPoints)
                {
                    // OBJ格式颜色使用0-1范围
                    float r = point.R / 255.0f;
                    float g = point.G / 255.0f;
                    float b = point.B / 255.0f;

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "v {0:F6} {1:F6} {2:F6} {3:F6} {4:F6} {5:F6}",
                        point.X, point.Y, point.Z, r, g, b));
                }
            }
            else
            {
                foreach (var point in data.Points)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "v {0:F6} {1:F6} {2:F6}",
                        point.X, point.Y, point.Z));
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
