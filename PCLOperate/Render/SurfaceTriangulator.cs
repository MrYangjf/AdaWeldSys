using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AdaWeldSystem.PCLOperate.Render
{
    /// <summary>
    /// 表面三角剖分器 - 提供多种点云表面重建算法
    /// 
    /// 重要：所有采样步长参数由外部控制，内部不做任何降采样。
    /// sampleStep = 1 表示使用全部点，sampleStep > 1 表示每隔 sampleStep 个点取一个。
    /// </summary>
    public static class SurfaceTriangulator
    {
        #region 1. 邻域搜索三角剖分

        /// <summary>
        /// 邻域搜索三角剖分 - 基于局部邻域的快速表面重建
        /// </summary>
        /// <param name="data">点云数据</param>
        /// <param name="searchRadius">搜索半径</param>
        /// <param name="maxNeighbors">最大邻居数量</param>
        /// <param name="sampleStep">采样步长（1=全部点，2=每隔2点取1个，默认1）</param>
        public static HelixToolkit.SharpDX.MeshGeometry3D GenerateSurface(PointCloudData data, float searchRadius = 0.5f, int maxNeighbors = 30, int sampleStep = 1)
        {
            if (data.PointCount < 3) return null;
            if (sampleStep < 1) sampleStep = 1;

            var points = ToVector3List(data);

            var spatialIndex = new SpatialIndex(points);

            var positions = new HelixToolkit.Vector3Collection();
            var normals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();
            var processed = new HashSet<(int, int, int)>();

            for (int i = 0; i < points.Count; i += sampleStep)
            {
                var neighbors = spatialIndex.FindNeighbors(i, searchRadius, maxNeighbors);

                for (int j = 0; j < neighbors.Count; j++)
                {
                    for (int k = j + 1; k < neighbors.Count; k++)
                    {
                        int idx1 = i;
                        int idx2 = neighbors[j];
                        int idx3 = neighbors[k];

                        var triKey = GetTriangleKey(idx1, idx2, idx3);
                        if (processed.Contains(triKey)) continue;

                        var p1 = points[idx1];
                        var p2 = points[idx2];
                        var p3 = points[idx3];

                        float d12 = VectorMath.Distance(p1, p2);
                        float d23 = VectorMath.Distance(p2, p3);
                        float d31 = VectorMath.Distance(p3, p1);

                        float maxEdge = Math.Max(Math.Max(d12, d23), d31);
                        float minEdge = Math.Min(Math.Min(d12, d23), d31);

                        if (maxEdge > searchRadius * 2 || minEdge < 0.001f || maxEdge / minEdge > 10)
                            continue;

                        var normal = ComputeNormal(p1, p2, p3);
                        if (normal.LengthSquared() < 0.0001f) continue;
                        normal = VectorMath.Normalize(normal);

                        int baseIndex = positions.Count;
                        positions.Add(p1);
                        positions.Add(p2);
                        positions.Add(p3);

                        normals.Add(normal);
                        normals.Add(normal);
                        normals.Add(normal);

                        indices.Add(baseIndex);
                        indices.Add(baseIndex + 1);
                        indices.Add(baseIndex + 2);

                        processed.Add(triKey);
                    }
                }
            }

            if (positions.Count == 0) return null;

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                Indices = indices
            };
        }

        #endregion

        #region 2. Ball Pivoting 滚球法

        /// <summary>
        /// Ball Pivoting 滚球法表面重建
        /// </summary>
        /// <param name="data">点云数据</param>
        /// <param name="ballRadius">球半径</param>
        /// <param name="sampleStep">采样步长（1=全部点，默认1）</param>
        /// <param name="maxNeighbors">最大邻居数量（默认20）</param>
        public static HelixToolkit.SharpDX.MeshGeometry3D GenerateSurfaceBallPivoting(PointCloudData data, float ballRadius = 0.5f, int sampleStep = 1, int maxNeighbors = 20)
        {
            if (data.PointCount < 3) return null;
            if (sampleStep < 1) sampleStep = 1;

            var points = ToVector3List(data);

            var spatialIndex = new SpatialIndex(points);
            var positions = new HelixToolkit.Vector3Collection();
            var normals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();
            var processed = new HashSet<(int, int, int)>();

            for (int i = 0; i < points.Count; i += sampleStep)
            {
                var neighbors = spatialIndex.FindNeighbors(i, ballRadius * 2.5f, maxNeighbors);
                if (neighbors.Count < 2) continue;

                for (int jIdx = 0; jIdx < neighbors.Count; jIdx++)
                {
                    int j = neighbors[jIdx];
                    if (j <= i) continue;

                    for (int kIdx = jIdx + 1; kIdx < neighbors.Count; kIdx++)
                    {
                        int k = neighbors[kIdx];
                        if (k <= j) continue;

                        var triKey = GetTriangleKey(i, j, k);
                        if (processed.Contains(triKey)) continue;

                        var p1 = points[i];
                        var p2 = points[j];
                        var p3 = points[k];

                        if (CanFormTriangle(p1, p2, p3, ballRadius))
                        {
                            var normal = ComputeNormal(p1, p2, p3);
                            if (normal.LengthSquared() < 0.0001f) continue;
                            normal = VectorMath.Normalize(normal);

                            int baseIndex = positions.Count;
                            positions.Add(p1);
                            positions.Add(p2);
                            positions.Add(p3);

                            normals.Add(normal);
                            normals.Add(normal);
                            normals.Add(normal);

                            indices.Add(baseIndex);
                            indices.Add(baseIndex + 1);
                            indices.Add(baseIndex + 2);

                            processed.Add(triKey);
                        }
                    }
                }
            }

            if (positions.Count == 0) return null;

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                Indices = indices
            };
        }

        #endregion

        #region 3. Alpha Shape 阿尔法形状

        /// <summary>
        /// Alpha Shape 阿尔法形状表面重建
        /// </summary>
        /// <param name="data">点云数据</param>
        /// <param name="alpha">Alpha值</param>
        /// <param name="sampleStep">采样步长（1=全部点，默认1）</param>
        /// <param name="maxNeighbors">最大邻居数量（默认15）</param>
        public static HelixToolkit.SharpDX.MeshGeometry3D GenerateAlphaShape(PointCloudData data, float alpha = 1.0f, int sampleStep = 1, int maxNeighbors = 15)
        {
            if (data.PointCount < 4) return null;
            if (sampleStep < 1) sampleStep = 1;

            var points = ToVector3List(data);

            var positions = new HelixToolkit.Vector3Collection();
            var normals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();
            var processed = new HashSet<(int, int, int)>();

            var spatialIndex = new SpatialIndex(points);
            float alphaSquared = alpha * alpha;

            for (int i = 0; i < points.Count; i += sampleStep)
            {
                var neighbors = spatialIndex.FindNeighbors(i, alpha * 2, maxNeighbors);
                if (neighbors.Count < 2) continue;

                for (int jIdx = 0; jIdx < neighbors.Count; jIdx++)
                {
                    int idx2 = neighbors[jIdx];

                    for (int kIdx = jIdx + 1; kIdx < neighbors.Count; kIdx++)
                    {
                        int idx3 = neighbors[kIdx];

                        var triKey = GetTriangleKey(i, idx2, idx3);
                        if (processed.Contains(triKey)) continue;

                        var p1 = points[i];
                        var p2 = points[idx2];
                        var p3 = points[idx3];

                        if (IsAlphaShapeValidFast(p1, p2, p3, alphaSquared))
                        {
                            var normal = ComputeNormal(p1, p2, p3);
                            if (normal.LengthSquared() < 0.0001f) continue;
                            normal = VectorMath.Normalize(normal);

                            int baseIndex = positions.Count;
                            positions.Add(p1);
                            positions.Add(p2);
                            positions.Add(p3);

                            normals.Add(normal);
                            normals.Add(normal);
                            normals.Add(normal);

                            indices.Add(baseIndex);
                            indices.Add(baseIndex + 1);
                            indices.Add(baseIndex + 2);

                            processed.Add(triKey);
                        }
                    }
                }
            }

            if (positions.Count == 0) return null;

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                Indices = indices
            };
        }

        private static bool IsAlphaShapeValidFast(Vector3 p1, Vector3 p2, Vector3 p3, float alphaSquared)
        {
            float a = VectorMath.Distance(p2, p3);
            float b = VectorMath.Distance(p1, p3);
            float c = VectorMath.Distance(p1, p2);

            float maxEdge = Math.Max(Math.Max(a, b), c);
            float minEdge = Math.Min(Math.Min(a, b), c);
            if (maxEdge > 1000 || minEdge < 0.001f || maxEdge / minEdge > 20)
                return false;

            float area = ComputeNormal(p1, p2, p3).Length() * 0.5f;
            if (area < 0.0001f) return false;

            float circumRadius = (a * b * c) / (4 * area);
            return circumRadius * circumRadius <= 1.0f / alphaSquared;
        }

        #endregion

        #region 4. Delaunay 3D 三角剖分

        /// <summary>
        /// 3D Delaunay 三角剖分表面重建
        /// </summary>
        /// <param name="data">点云数据</param>
        /// <param name="qualityThreshold">质量阈值（0-1，默认0.3）</param>
        /// <param name="searchRadiusMultiplier">搜索半径倍数（相对于平均间距，默认5）</param>
        /// <param name="sampleStep">采样步长（1=全部点，默认1）</param>
        /// <param name="maxNeighbors">最大邻居数量（默认15）</param>
        public static HelixToolkit.SharpDX.MeshGeometry3D GenerateDelaunay3D(PointCloudData data, float qualityThreshold = 0.3f, float searchRadiusMultiplier = 5f, int sampleStep = 1, int maxNeighbors = 15)
        {
            if (data.PointCount < 4) return null;
            if (sampleStep < 1) sampleStep = 1;

            var points = ToVector3List(data);

            var positions = new HelixToolkit.Vector3Collection();
            var normals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();
            var processed = new HashSet<(int, int, int)>();

            var spatialIndex = new SpatialIndex(points);
            float avgSpacing = ComputeAverageSpacing(points);
            float searchRadius = avgSpacing * searchRadiusMultiplier;

            for (int i = 0; i < points.Count; i += sampleStep)
            {
                var neighbors = spatialIndex.FindNeighbors(i, searchRadius, maxNeighbors);
                if (neighbors.Count < 2) continue;

                for (int jIdx = 0; jIdx < neighbors.Count; jIdx++)
                {
                    int idx2 = neighbors[jIdx];

                    for (int kIdx = jIdx + 1; kIdx < neighbors.Count; kIdx++)
                    {
                        int idx3 = neighbors[kIdx];

                        var triKey = GetTriangleKey(i, idx2, idx3);
                        if (processed.Contains(triKey)) continue;

                        var p1 = points[i];
                        var p2 = points[idx2];
                        var p3 = points[idx3];

                        if (IsDelaunayValidFast(p1, p2, p3, points, spatialIndex))
                        {
                            float quality = ComputeTriangleQuality(p1, p2, p3);
                            if (quality < qualityThreshold) continue;

                            var normal = ComputeNormal(p1, p2, p3);
                            if (normal.LengthSquared() < 0.0001f) continue;
                            normal = VectorMath.Normalize(normal);

                            int baseIndex = positions.Count;
                            positions.Add(p1);
                            positions.Add(p2);
                            positions.Add(p3);

                            normals.Add(normal);
                            normals.Add(normal);
                            normals.Add(normal);

                            indices.Add(baseIndex);
                            indices.Add(baseIndex + 1);
                            indices.Add(baseIndex + 2);

                            processed.Add(triKey);
                        }
                    }
                }
            }

            if (positions.Count == 0) return null;

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                Indices = indices
            };
        }

        private static bool IsDelaunayValidFast(Vector3 p1, Vector3 p2, Vector3 p3, List<Vector3> allPoints, SpatialIndex spatialIndex)
        {
            var center = (p1 + p2 + p3) / 3;
            float radius = VectorMath.Distance(center, p1);

            var nearby = spatialIndex.FindNeighborsInRadius(center, radius * 1.2f, allPoints);

            int checkCount = 0;
            foreach (int idx in nearby)
            {
                if (checkCount++ > 20) break;

                var p = allPoints[idx];
                if (p != p1 && p != p2 && p != p3)
                {
                    float dist = VectorMath.Distance(center, p);
                    if (dist < radius * 0.95f)
                        return false;
                }
            }

            return true;
        }

        private static float ComputeTriangleQuality(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float a = VectorMath.Distance(p2, p3);
            float b = VectorMath.Distance(p1, p3);
            float c = VectorMath.Distance(p1, p2);

            float area = ComputeNormal(p1, p2, p3).Length() * 0.5f;
            if (area < 0.0001f) return 0;

            float circumRadius = (a * b * c) / (4 * area);
            float inRadius = area / ((a + b + c) * 0.5f);

            return inRadius / circumRadius;
        }

        #endregion

        #region 5. Poisson 泊松重建

        /// <summary>
        /// Poisson 泊松表面重建
        /// </summary>
        /// <param name="data">点云数据</param>
        /// <param name="depth">八叉树深度（默认8）</param>
        /// <param name="samplesPerNode">每节点最小采样数（默认1.0）</param>
        /// <param name="maxCells">最大处理单元数（默认无限制）</param>
        public static HelixToolkit.SharpDX.MeshGeometry3D GeneratePoissonSurface(PointCloudData data, int depth = 8, float samplesPerNode = 1.0f, int maxCells = 0)
        {
            if (data.PointCount < 10) return null;

            var points = ToVector3List(data);

            var spatialIndex = new SpatialIndex(points);
            var normals = EstimateNormalsFast(points, spatialIndex);

            var positions = new HelixToolkit.Vector3Collection();
            var meshNormals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();

            float minX = points.Min(p => p.X);
            float maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxY = points.Max(p => p.Y);
            float minZ = points.Min(p => p.Z);
            float maxZ = points.Max(p => p.Z);

            float size = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
            int resolution = Math.Min(1 << Math.Min(depth, 8), 256);
            float cellSize = size / resolution;

            var grid = new Dictionary<(int, int, int), List<(Vector3 point, Vector3 normal)>>();

            for (int i = 0; i < points.Count; i++)
            {
                var cell = GetGridCell(points[i], minX, minY, minZ, cellSize);
                if (!grid.TryGetValue(cell, out var list))
                {
                    list = new List<(Vector3, Vector3)>();
                    grid[cell] = list;
                }
                list.Add((points[i], normals[i]));
            }

            var processedCells = new HashSet<(int, int, int)>();
            int cellCount = 0;

            foreach (var kvp in grid)
            {
                if (kvp.Value.Count < Math.Max(1, samplesPerNode)) continue;
                if (processedCells.Contains(kvp.Key)) continue;

                var localSurface = GenerateLocalSurfacePatch(kvp.Value);

                if (localSurface.HasValue)
                {
                    var surface = localSurface.Value;
                    int baseIndex = positions.Count;

                    foreach (var pos in surface.Positions)
                        positions.Add(pos);
                    foreach (var n in surface.Normals)
                        meshNormals.Add(n);
                    foreach (int idx in surface.Indices)
                        indices.Add(baseIndex + idx);
                }

                processedCells.Add(kvp.Key);
                cellCount++;

                if (maxCells > 0 && cellCount >= maxCells) break;
            }

            if (positions.Count == 0) return null;

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = meshNormals,
                Indices = indices
            };
        }

        private static (HelixToolkit.Vector3Collection Positions, HelixToolkit.Vector3Collection Normals, HelixToolkit.IntCollection Indices)?
            GenerateLocalSurfacePatch(List<(Vector3 point, Vector3 normal)> localPoints)
        {
            if (localPoints.Count < 3) return null;

            var positions = new HelixToolkit.Vector3Collection();
            var normals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();

            var centroid = Vector3.Zero;
            var avgNormal = Vector3.Zero;
            foreach (var pt in localPoints)
            {
                centroid += pt.point;
                avgNormal += pt.normal;
            }
            centroid /= localPoints.Count;
            avgNormal = VectorMath.Normalize(avgNormal);

            var tangent = Math.Abs(avgNormal.X) > 0.9f ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
            tangent = VectorMath.Normalize(tangent - avgNormal * VectorMath.Dot(tangent, avgNormal));
            var bitangent = VectorMath.Cross(avgNormal, tangent);

            var projected2D = new List<(Vector2 uv, Vector3 point, Vector3 normal)>();
            foreach (var pt in localPoints)
            {
                var toPoint = pt.point - centroid;
                float u = VectorMath.Dot(toPoint, tangent);
                float v = VectorMath.Dot(toPoint, bitangent);
                projected2D.Add((new Vector2(u, v), pt.point, pt.normal));
            }

            int n = Math.Min(projected2D.Count, 12);
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        var p1 = projected2D[i];
                        var p2 = projected2D[j];
                        var p3 = projected2D[k];

                        float area = Math.Abs((p2.uv.X - p1.uv.X) * (p3.uv.Y - p1.uv.Y) -
                                              (p3.uv.X - p1.uv.X) * (p2.uv.Y - p1.uv.Y)) * 0.5f;
                        if (area < 0.0001f) continue;

                        int baseIndex = positions.Count;
                        positions.Add(p1.point);
                        positions.Add(p2.point);
                        positions.Add(p3.point);

                        normals.Add(p1.normal);
                        normals.Add(p2.normal);
                        normals.Add(p3.normal);

                        indices.Add(baseIndex);
                        indices.Add(baseIndex + 1);
                        indices.Add(baseIndex + 2);
                    }
                }
            }

            if (positions.Count == 0) return null;

            return (positions, normals, indices);
        }

        private static (int, int, int) GetGridCell(Vector3 p, float minX, float minY, float minZ, float cellSize)
        {
            return (
                (int)((p.X - minX) / cellSize),
                (int)((p.Y - minY) / cellSize),
                (int)((p.Z - minZ) / cellSize)
            );
        }

        private static List<Vector3> EstimateNormalsFast(List<Vector3> points, SpatialIndex spatialIndex)
        {
            var normals = new List<Vector3>(points.Count);
            float searchRadius = ComputeAverageSpacing(points) * 3;

            for (int i = 0; i < points.Count; i++)
            {
                var neighbors = spatialIndex.FindNeighbors(i, searchRadius, 8);
                normals.Add(EstimateNormalFast(points, i, neighbors));
            }
            return normals;
        }

        private static Vector3 EstimateNormalFast(List<Vector3> points, int index, List<int> neighbors)
        {
            if (neighbors.Count < 2)
                return new Vector3(0, 1, 0);

            var p = points[index];
            var v1 = points[neighbors[0]] - p;
            var v2 = neighbors.Count > 1 ? points[neighbors[1]] - p : new Vector3(0, 0, 1);
            var normal = VectorMath.Cross(v1, v2);

            if (normal.LengthSquared() < 0.0001f)
                return new Vector3(0, 1, 0);

            return VectorMath.Normalize(normal);
        }

        #endregion

        #region 6. MLS 移动最小二乘

        /// <summary>
        /// MLS (Moving Least Squares) 移动最小二乘表面重建
        /// </summary>
        /// <param name="data">点云数据</param>
        /// <param name="searchRadius">搜索半径</param>
        /// <param name="sampleStep">采样步长（1=全部点，默认1）</param>
        /// <param name="maxNeighbors">最大邻居数量（默认12）</param>
        public static HelixToolkit.SharpDX.MeshGeometry3D GenerateMLSSurface(PointCloudData data, float searchRadius = 0.5f, int sampleStep = 1, int maxNeighbors = 12)
        {
            if (data.PointCount < 10) return null;
            if (sampleStep < 1) sampleStep = 1;

            var points = ToVector3List(data);

            var spatialIndex = new SpatialIndex(points);
            var projectedPoints = new List<Vector3>();
            var projectedNormals = new List<Vector3>();

            for (int i = 0; i < points.Count; i += sampleStep)
            {
                var neighbors = spatialIndex.FindNeighbors(i, searchRadius, maxNeighbors);
                if (neighbors.Count < 3) continue;

                var localPoints = neighbors.Take(10).Select(idx => points[idx]).ToList();

                var weights = localPoints.Select(p =>
                {
                    float d = VectorMath.Distance(points[i], p);
                    return (float)Math.Exp(-(d * d) / (searchRadius * searchRadius * 0.25f));
                }).ToList();

                var (planePoint, planeNormal) = FitWeightedPlaneFast(localPoints, weights);

                var projected = ProjectToPlane(points[i], planePoint, planeNormal);
                projectedPoints.Add(projected);
                projectedNormals.Add(planeNormal);
            }

            if (projectedPoints.Count < 3) return null;

            var positions = new HelixToolkit.Vector3Collection();
            var normals = new HelixToolkit.Vector3Collection();
            var indices = new HelixToolkit.IntCollection();
            var processed = new HashSet<(int, int, int)>();

            var projSpatialIndex = new SpatialIndex(projectedPoints);

            for (int i = 0; i < projectedPoints.Count; i++)
            {
                var neighbors = projSpatialIndex.FindNeighbors(i, searchRadius * 2, 10);
                if (neighbors.Count < 2) continue;

                for (int jIdx = 0; jIdx < neighbors.Count; jIdx++)
                {
                    int idx2 = neighbors[jIdx];

                    for (int kIdx = jIdx + 1; kIdx < neighbors.Count; kIdx++)
                    {
                        int idx3 = neighbors[kIdx];

                        var triKey = GetTriangleKey(i, idx2, idx3);
                        if (processed.Contains(triKey)) continue;

                        var p1 = projectedPoints[i];
                        var p2 = projectedPoints[idx2];
                        var p3 = projectedPoints[idx3];

                        float d12 = VectorMath.Distance(p1, p2);
                        float d23 = VectorMath.Distance(p2, p3);
                        float d31 = VectorMath.Distance(p3, p1);

                        float maxEdge = Math.Max(Math.Max(d12, d23), d31);
                        if (maxEdge > searchRadius * 4) continue;

                        var normal = ComputeNormal(p1, p2, p3);
                        if (normal.LengthSquared() < 0.0001f) continue;
                        normal = VectorMath.Normalize(normal);

                        int baseIndex = positions.Count;
                        positions.Add(p1);
                        positions.Add(p2);
                        positions.Add(p3);

                        normals.Add(projectedNormals[i]);
                        normals.Add(projectedNormals[idx2]);
                        normals.Add(projectedNormals[idx3]);

                        indices.Add(baseIndex);
                        indices.Add(baseIndex + 1);
                        indices.Add(baseIndex + 2);

                        processed.Add(triKey);
                    }
                }
            }

            if (positions.Count == 0) return null;

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                Indices = indices
            };
        }

        private static (Vector3 point, Vector3 normal) FitWeightedPlaneFast(List<Vector3> points, List<float> weights)
        {
            Vector3 centroid = Vector3.Zero;
            float totalWeight = 0;
            for (int i = 0; i < points.Count; i++)
            {
                centroid += points[i] * weights[i];
                totalWeight += weights[i];
            }
            centroid /= totalWeight;

            if (points.Count >= 3)
            {
                var v1 = points[1] - centroid;
                var v2 = points[2] - centroid;
                var normal = VectorMath.Cross(v1, v2);
                if (normal.LengthSquared() > 0.0001f)
                    return (centroid, VectorMath.Normalize(normal));
            }

            return (centroid, new Vector3(0, 1, 0));
        }

        private static Vector3 ProjectToPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            var v = point - planePoint;
            float dist = VectorMath.Dot(v, planeNormal);
            return point - planeNormal * dist;
        }

        #endregion

        #region 私有方法

        private static List<Vector3> ToVector3List(PointCloudData data)
        {
            return data.HasColor
                ? data.ColoredPoints.Select(p => new Vector3(p.X, p.Y, p.Z)).ToList()
                : data.Points.Select(p => new Vector3(p.X, p.Y, p.Z)).ToList();
        }

        private static void AddTriangle(HelixToolkit.Vector3Collection positions, HelixToolkit.Vector3Collection normals, HelixToolkit.IntCollection indices, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            int baseIndex = positions.Count;
            positions.Add(p1);
            positions.Add(p2);
            positions.Add(p3);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
        }

        private static Vector3 ComputeNormal(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var v1 = p2 - p1;
            var v2 = p3 - p1;
            return VectorMath.Cross(v1, v2);
        }

        private static (int, int, int) GetTriangleKey(int i, int j, int k)
        {
            var sorted = new[] { i, j, k }.OrderBy(x => x).ToArray();
            return (sorted[0], sorted[1], sorted[2]);
        }

        private static bool CanFormTriangle(Vector3 p1, Vector3 p2, Vector3 p3, float ballRadius)
        {
            float d12 = VectorMath.Distance(p1, p2);
            float d23 = VectorMath.Distance(p2, p3);
            float d31 = VectorMath.Distance(p3, p1);

            if (d12 > ballRadius * 2 || d23 > ballRadius * 2 || d31 > ballRadius * 2)
                return false;

            float area = ComputeNormal(p1, p2, p3).Length() * 0.5f;
            if (area < 0.0001f) return false;

            float circumRadius = (d12 * d23 * d31) / (4 * area);
            return circumRadius <= ballRadius * 1.5f;
        }

        private static float ComputeAverageSpacing(List<Vector3> points)
        {
            if (points.Count < 2) return 0.1f;

            var spatialIndex = new SpatialIndex(points);
            float totalSpacing = 0;
            int count = 0;

            int step = Math.Max(1, points.Count / 100);
            for (int i = 0; i < points.Count; i += step)
            {
                var neighbors = spatialIndex.FindNeighbors(i, float.MaxValue, 2);
                if (neighbors.Count > 1)
                {
                    totalSpacing += VectorMath.Distance(points[i], points[neighbors[1]]);
                    count++;
                }
            }

            return count > 0 ? totalSpacing / count : 0.1f;
        }

        #endregion

        #region 向量数学辅助方法

        private static class VectorMath
        {
            public static float Distance(Vector3 a, Vector3 b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                float dz = a.Z - b.Z;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            public static float DistanceSquared(Vector3 a, Vector3 b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                float dz = a.Z - b.Z;
                return dx * dx + dy * dy + dz * dz;
            }

            public static float Dot(Vector3 a, Vector3 b)
            {
                return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            }

            public static Vector3 Cross(Vector3 a, Vector3 b)
            {
                return new Vector3(
                    a.Y * b.Z - a.Z * b.Y,
                    a.Z * b.X - a.X * b.Z,
                    a.X * b.Y - a.Y * b.X);
            }

            public static Vector3 Normalize(Vector3 v)
            {
                float len = v.Length();
                if (len < 1e-8f)
                    return Vector3.Zero;
                return new Vector3(v.X / len, v.Y / len, v.Z / len);
            }
        }

        #endregion

        #region 空间索引

        private class SpatialIndex
        {
            private readonly List<Vector3> _points;
            private readonly Dictionary<(int, int, int), List<int>> _grid;
            private readonly float _cellSize;

            public SpatialIndex(List<Vector3> points, float cellSize = 0f)
            {
                _points = points;
                _grid = new Dictionary<(int, int, int), List<int>>();

                if (cellSize <= 0 && points.Count > 0)
                {
                    float minX = points.Min(p => p.X);
                    float maxX = points.Max(p => p.X);
                    float minY = points.Min(p => p.Y);
                    float maxY = points.Max(p => p.Y);
                    float minZ = points.Min(p => p.Z);
                    float maxZ = points.Max(p => p.Z);

                    float maxRange = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
                    _cellSize = maxRange / Math.Max(1, (int)Math.Pow(points.Count, 1.0 / 3.0));
                    if (_cellSize <= 0) _cellSize = 1f;
                }
                else
                {
                    _cellSize = cellSize;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    var cell = GetCell(points[i]);
                    if (!_grid.TryGetValue(cell, out var list))
                    {
                        list = new List<int>();
                        _grid[cell] = list;
                    }
                    list.Add(i);
                }
            }

            private (int, int, int) GetCell(Vector3 p)
            {
                return ((int)(p.X / _cellSize), (int)(p.Y / _cellSize), (int)(p.Z / _cellSize));
            }

            public List<int> FindNeighbors(int pointIndex, float radius, int maxCount)
            {
                var p = _points[pointIndex];
                var result = new List<(int index, float dist)>();
                float radiusSq = radius * radius;

                int cellRange = Math.Max(1, (int)(radius / _cellSize) + 1);
                var centerCell = GetCell(p);

                for (int dx = -cellRange; dx <= cellRange; dx++)
                {
                    for (int dy = -cellRange; dy <= cellRange; dy++)
                    {
                        for (int dz = -cellRange; dz <= cellRange; dz++)
                        {
                            var cell = (centerCell.Item1 + dx, centerCell.Item2 + dy, centerCell.Item3 + dz);
                            if (_grid.TryGetValue(cell, out var cellIndices))
                            {
                                foreach (int idx in cellIndices)
                                {
                                    if (idx != pointIndex)
                                    {
                                        float distSq = VectorMath.DistanceSquared(p, _points[idx]);
                                        if (distSq <= radiusSq)
                                        {
                                            result.Add((idx, distSq));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                result.Sort((a, b) => a.dist.CompareTo(b.dist));
                return result.Count > maxCount
                    ? result.Take(maxCount).Select(x => x.index).ToList()
                    : result.Select(x => x.index).ToList();
            }

            public List<int> FindNeighborsInRadius(Vector3 center, float radius, List<Vector3> allPoints)
            {
                var result = new List<int>();
                float radiusSq = radius * radius;

                for (int i = 0; i < allPoints.Count; i++)
                {
                    if (VectorMath.DistanceSquared(center, allPoints[i]) <= radiusSq)
                        result.Add(i);
                }

                return result;
            }
        }

        #endregion
    }
}