using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model.Scene;
using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Globalization;
using System.IO;

namespace AdaWeldSystem.PCLOperate.DataIO
{
    /// <summary>
    /// 点云文件读取器
    /// </summary>
    public static class PointCloudReader
    {
        /// <summary>
        /// 根据文件扩展名自动选择读取方法
        /// </summary>
        public static PointCloudData LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件未找到: {filePath}");

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            // 定义返回值变量
            PointCloudData result;

            // 传统 switch 语句（C# 7.3 支持）
            switch (extension)
            {
                case ".txt":
                case ".xyz":
                    result = LoadFromTxt(filePath);
                    break;
                case ".ply":
                    result = LoadFromPly(filePath);
                    break;
                case ".obj":
                    result = LoadFromObj(filePath);
                    break;
                default:
                    throw new NotSupportedException($"不支持的文件格式: {extension}");
            }

            return result;
        }

        /// <summary>
        /// 从TXT/XYZ文件加载点云
        /// </summary>
        public static PointCloudData LoadFromTxt(string filePath, bool hasColor = false)
        {
            var data = new PointCloudData();
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (hasColor && parts.Length >= 6)
                {
                    if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) &&
                        byte.TryParse(parts[3], out byte r) &&
                        byte.TryParse(parts[4], out byte g) &&
                        byte.TryParse(parts[5], out byte b))
                    {
                        data.AddPoint(x, y, z, r, g, b);
                    }
                }
                else if (parts.Length >= 3)
                {
                    if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        data.AddPoint(x, y, z);
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// 从PLY文件加载点云
        /// </summary>
        public static PointCloudData LoadFromPly(string filePath)
        {
            var data = new PointCloudData();
            var lines = File.ReadAllLines(filePath);

            int vertexCount = 0;
            int headerEndIndex = 0;
            bool hasColor = false;
            int xIndex = -1, yIndex = -1, zIndex = -1;
            int rIndex = -1, gIndex = -1, bIndex = -1;

            // 解析头部
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("element vertex"))
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 3)
                        int.TryParse(parts[2], out vertexCount);
                }
                else if (line.StartsWith("property"))
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 3)
                    {
                        var propName = parts[parts.Length - 1].ToLower();
                        switch (propName)
                        {
                            case "x":
                                xIndex = GetPropertyIndex(xIndex, yIndex, zIndex, rIndex, gIndex, bIndex);
                                break;
                            case "y":
                                yIndex = GetPropertyIndex(xIndex, yIndex, zIndex, rIndex, gIndex, bIndex);
                                break;
                            case "z":
                                zIndex = GetPropertyIndex(xIndex, yIndex, zIndex, rIndex, gIndex, bIndex);
                                break;

                            case "red":
                            case "r":
                                rIndex = GetPropertyIndex(xIndex, yIndex, zIndex, rIndex, gIndex, bIndex);
                                hasColor = true;
                                break;

                            case "green":
                            case "g":
                                gIndex = GetPropertyIndex(xIndex, yIndex, zIndex, rIndex, gIndex, bIndex);
                                hasColor = true;
                                break;

                            case "blue":
                            case "b":
                                bIndex = GetPropertyIndex(xIndex, yIndex, zIndex, rIndex, gIndex, bIndex);
                                hasColor = true;
                                break;
                        }
                    }
                }
                else if (line == "end_header")
                {
                    headerEndIndex = i + 1;
                    break;
                }
            }

            // 设置默认索引
            if (xIndex == -1) xIndex = 0;
            if (yIndex == -1) yIndex = 1;
            if (zIndex == -1) zIndex = 2;

            // 读取顶点数据
            int maxIndex = Math.Max(Math.Max(xIndex, yIndex), zIndex);
            if (hasColor)
                maxIndex = Math.Max(Math.Max(Math.Max(rIndex, gIndex), bIndex), maxIndex);

            for (int i = headerEndIndex; i < Math.Min(headerEndIndex + vertexCount, lines.Length); i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length <= maxIndex) continue;

                if (float.TryParse(parts[xIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[yIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[zIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    if (hasColor && rIndex >= 0 && gIndex >= 0 && bIndex >= 0 &&
                        parts.Length > Math.Max(Math.Max(rIndex, gIndex), bIndex))
                    {
                        // PLY颜色通常是0-255
                        float rVal = float.Parse(parts[rIndex], CultureInfo.InvariantCulture);
                        float gVal = float.Parse(parts[gIndex], CultureInfo.InvariantCulture);
                        float bVal = float.Parse(parts[bIndex], CultureInfo.InvariantCulture);

                        byte r = rVal <= 1.0f ? (byte)(rVal * 255) : (byte)rVal;
                        byte g = gVal <= 1.0f ? (byte)(gVal * 255) : (byte)gVal;
                        byte b = bVal <= 1.0f ? (byte)(bVal * 255) : (byte)bVal;

                        data.AddPoint(x, y, z, r, g, b);
                    }
                    else
                    {
                        data.AddPoint(x, y, z);
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// 从OBJ文件加载点云
        /// </summary>
        public static PointCloudData LoadFromObj(string filePath)
        {
            var data = new PointCloudData();
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts[0] == "v" && parts.Length >= 4)
                {
                    if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        // OBJ格式可能包含颜色信息
                        if (parts.Length >= 7 &&
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
                            float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
                            float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                        {
                            byte rb = r <= 1.0f ? (byte)(r * 255) : (byte)Math.Min(r, 255);
                            byte gb = g <= 1.0f ? (byte)(g * 255) : (byte)Math.Min(g, 255);
                            byte bb = b <= 1.0f ? (byte)(b * 255) : (byte)Math.Min(b, 255);
                            data.AddPoint(x, y, z, rb, gb, bb);
                        }
                        else
                        {
                            data.AddPoint(x, y, z);
                        }
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// 使用 HelixToolkit.SharpDX.Assimp 直接加载 PLY/OBJ 等文件为 HelixToolkitScene
        /// 返回的 Scene 可通过 PointCloudViewer.LoadScene() 直接显示到 Viewport3DX
        /// </summary>
        /// <param name="filePath">文件路径（支持 PLY/OBJ/STL/FBX 等 Assimp 支持的格式）</param>
        /// <returns>HelixToolkitScene 对象（调用方负责使用后 Dispose）</returns>
        public static HelixToolkitScene LoadSceneFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件未找到: {filePath}");

            using (var importer = new Importer())
            {
                var scene = importer.Load(filePath);

                if (scene == null || importer.ErrorCode != ErrorCode.Succeed)
                {
                    throw new InvalidOperationException($"Assimp 导入失败 原因是 {importer.ErrorCode}");
                }

                return scene;
            }
        }

        /// <summary>
        /// 使用 HelixToolkit.SharpDX.Assimp 从 PLY 文件加载点云
        /// </summary>
        public static PointCloudData LoadFromPlyWithAssimp(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件未找到: {filePath}");

            var data = new PointCloudData();

            using (var importer = new Importer())
            {
                var scene = importer.Load(filePath);

                if (scene == null || importer.ErrorCode != ErrorCode.Succeed)
                {
                    throw new InvalidOperationException($"Assimp 导入失败 原因是 {importer.ErrorCode}");
                }

                // 递归遍历场景节点，提取 MeshNode 中的顶点数据
                if (scene.Root != null)
                {
                    TraverseSceneNode(scene.Root, data);
                }
            }

            return data;
        }

        /// <summary>
        /// 递归遍历场景节点，提取顶点数据
        /// </summary>
        private static void TraverseSceneNode(SceneNode node, PointCloudData data)
        {
            if (node is MeshNode meshNode)
            {
                var geometry = meshNode.Geometry;
                if (geometry != null && geometry.Positions != null)
                {
                    var positions = geometry.Positions;
                    var colors = geometry.Colors;
                    bool hasColors = colors != null && colors.Count > 0;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        var pos = positions[i];
                        float x = pos.X;
                        float y = pos.Y;
                        float z = pos.Z;

                        if (hasColors && i < colors.Count)
                        {
                            var color = colors[i];
                            byte r = (byte)Math.Max(0, Math.Min(255, color.Red * 255));
                            byte g = (byte)Math.Max(0, Math.Min(255, color.Green * 255));
                            byte b = (byte)Math.Max(0, Math.Min(255, color.Blue * 255));
                            data.AddPoint(x, y, z, r, g, b);
                        }
                        else
                        {
                            data.AddPoint(x, y, z);
                        }
                    }
                }
            }

            // 递归遍历子节点
            if (node is GroupNode groupNode)
            {
                foreach (var child in groupNode.Items)
                {
                    TraverseSceneNode(child, data);
                }
            }
        }

        private static int GetPropertyIndex(int x, int y, int z, int r, int g, int b)
        {
            int count = 0;
            if (x >= 0) count++;
            if (y >= 0) count++;
            if (z >= 0) count++;
            if (r >= 0) count++;
            if (g >= 0) count++;
            if (b >= 0) count++;
            return count;
        }
    }
}
