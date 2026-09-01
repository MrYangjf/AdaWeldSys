using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Linq;

namespace AdaWeldSystem.ProductFileManager
{
    /// <summary>
    /// SmartRay点云数据转换器
    /// 将SmartRay相机原始数据转换为标准PointCloudData
    /// </summary>
    public static class SmartRayConverter
    {
        /// <summary>
        /// 将SmartRay原始点云数据转换为标准PointCloudData
        /// </summary>
        /// <param name="numPoints">点数</param>
        /// <param name="point3d">SmartRay点数组</param>
        /// <param name="intensity">强度数组（可选）</param>
        /// <param name="transportResolution">传输分辨率，用于计算X坐标</param>
        /// <returns>标准点云数据</returns>
        public static PointCloudData ToPointCloudData(uint numPoints, Smartray.Api.Point3d[] point3d,
            ushort[] intensity, float transportResolution)
        {
            if (point3d == null)
                throw new ArgumentNullException(nameof(point3d));

            var data = new PointCloudData();
            int count = Math.Min((int)numPoints, point3d.Length);

            for (int i = 0; i < count; i++)
            {
                // SmartRay原始数据是2维的（y和z坐标）
                // x坐标使用传输分辨率计算：x = profileIdx * transportResolution
                float x = point3d[i].X;
                float y = point3d[i].Y;
                float z = point3d[i].Z;

                // 如果X对齐结构
                x = i * transportResolution;


                // 跳过无效点
                if (y < -999900.0f || z < -999900.0f)
                {
                    continue;
                }

                // 如果有强度信息，使用强度作为灰度颜色
                if (intensity != null && i < intensity.Length)
                {
                    byte gray = IntensityToGray(intensity[i]);
                    data.AddPoint(x, y, z, gray, gray, gray);
                }
                else
                {
                    data.AddPoint(x, y, z);
                }
            }

            return data;
        }

        /// <summary>
        /// 将强度值转换为灰度颜色
        /// </summary>
        private static byte IntensityToGray(ushort intensity)
        {
            // 假设强度范围是0-65535，映射到0-255
            int gray = intensity / 257;
            if (gray > 255) gray = 255;
            if (gray < 0) gray = 0;
            return (byte)gray;
        }

        /// <summary>
        /// 将标准PointCloudData转换为SmartRay格式（简化版）
        /// </summary>
        /// <param name="data">标准点云数据</param>
        /// <param name="transportResolution">传输分辨率</param>
        /// <returns>SmartRay点数组</returns>
        public static Smartray.Api.Point3d[] ToSmartRayPoints(PointCloudData data, float transportResolution)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            int count = data.PointCount;
            var points = new Smartray.Api.Point3d[count];

            var pointList = data.Points.ToArray();
            for (int i = 0; i < count; i++)
            {
                points[i] = new Smartray.Api.Point3d
                {
                    X = pointList[i].X,
                    Y = pointList[i].Y,
                    Z = pointList[i].Z
                };
            }

            return points;
        }
    }
}
