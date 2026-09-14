namespace AdaWeldSystem.PCLOperate.Models
{

    /// <summary>
    /// 3D点数据结构
    /// </summary>
    public struct Point3D
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Point3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }
    }

    /// <summary>
    /// 带颜色的3D点数据结构
    /// </summary>
    public struct Point3DColor
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public Point3DColor(float x, float y, float z, byte r, byte g, byte b, byte a = 255)
        {
            X = x;
            Y = y;
            Z = z;
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z}) [R:{R}, G:{G}, B:{B}]";
        }
    }
}
