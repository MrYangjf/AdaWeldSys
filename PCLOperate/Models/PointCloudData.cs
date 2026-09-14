using System;
using System.Collections.Generic;


namespace AdaWeldSystem.PCLOperate.Models
{
    /// <summary>
    /// 点云数据容器
    /// </summary>
    public class PointCloudData
    {
        private readonly List<Point3D> _points = new List<Point3D>();
        private readonly List<Point3DColor> _coloredPoints = new List<Point3DColor>();

        public PointCloudData()
        {
        }

        ~PointCloudData()
        {
            Clear();
        }

        /// <summary>
        /// 点列表
        /// </summary>
        public IReadOnlyList<Point3D> Points => _points.AsReadOnly();

        /// <summary>
        /// 彩色点列表
        /// </summary>
        public IReadOnlyList<Point3DColor> ColoredPoints => _coloredPoints.AsReadOnly();

        /// <summary>
        /// 是否包含颜色信息
        /// </summary>
        public bool HasColor => _coloredPoints.Count > 0;

        /// <summary>
        /// 点的数量
        /// </summary>
        public int PointCount => HasColor ? _coloredPoints.Count : _points.Count;

        /// <summary>
        /// 添加点（无颜色）
        /// </summary>
        public void AddPoint(float x, float y, float z)
        {
            _points.Add(new Point3D(x, y, z));
        }

        /// <summary>
        /// 添加彩色点
        /// </summary>
        public void AddPoint(float x, float y, float z, byte r, byte g, byte b, byte a = 255)
        {
            _coloredPoints.Add(new Point3DColor(x, y, z, r, g, b, a));
        }

        /// <summary>
        /// 添加点（从Point3D）
        /// </summary>
        public void AddPoint(Point3D point)
        {
            _points.Add(point);
        }

        /// <summary>
        /// 拷贝构造：深拷贝另一个 PointCloudData 的所有点
        /// </summary>
        public PointCloudData(PointCloudData other)
        {
            if (other == null) return;
            foreach (var p in other.Points)
                _points.Add(new Point3D(p.X, p.Y, p.Z));
            foreach (var cp in other.ColoredPoints)
                _coloredPoints.Add(new Point3DColor(cp.X, cp.Y, cp.Z, cp.R, cp.G, cp.B, cp.A));
        }

        /// <summary>
        /// 添加彩色点（从Point3DColor）
        /// </summary>
        public void AddPoint(Point3DColor point)
        {
            _coloredPoints.Add(point);
        }

        /// <summary>
        /// 清除所有点
        /// </summary>
        public void Clear()
        {
            _points.Clear();
            _coloredPoints.Clear();
        }

        /// <summary>
        /// 计算边界框
        /// </summary>
        public void GetBounds(out float minX, out float minY, out float minZ,
                              out float maxX, out float maxY, out float maxZ)
        {
            if (PointCount == 0)
            {
                minX = minY = minZ = maxX = maxY = maxZ = 0;
                return;
            }

            minX = minY = minZ = float.MaxValue;
            maxX = maxY = maxZ = float.MinValue;

            if (HasColor)
            {
                foreach (var point in _coloredPoints)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    minZ = Math.Min(minZ, point.Z);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                    maxZ = Math.Max(maxZ, point.Z);
                }
            }
            else
            {
                foreach (var point in _points)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    minZ = Math.Min(minZ, point.Z);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                    maxZ = Math.Max(maxZ, point.Z);
                }
            }
        }

        /// <summary>
        /// 获取中心点
        /// </summary>
        public (float x, float y, float z) GetCenter()
        {
            GetBounds(out float minX, out float minY, out float minZ,
                      out float maxX, out float maxY, out float maxZ);
            return ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        }

        /// <summary>
        /// 转换为彩色点列表（如果没有颜色，使用默认白色）
        /// </summary>
        public List<Point3DColor> ToColoredPoints(byte defaultR = 255, byte defaultG = 255, byte defaultB = 255)
        {
            if (HasColor)
            {
                return new List<Point3DColor>(_coloredPoints);
            }

            var result = new List<Point3DColor>(_points.Count);
            foreach (var point in _points)
            {
                result.Add(new Point3DColor(point.X, point.Y, point.Z, defaultR, defaultG, defaultB));
            }
            return result;
        }
    }
}
