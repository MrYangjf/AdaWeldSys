using System;
using System.ComponentModel;

namespace AdaWeldSystem.EmguALG.SeamRecognition
{
    /// <summary>
    /// 测量空间坐标点（mm）。所有几何参数、边缝、指引点、偏移、偏差均以此为单位。
    /// 图像空间(px) 仅在栅格化/绘制时由 ProfileTransform 转换得到，业务全程在 mm 空间计算
    /// （算法架构设计 §5.1）。
    /// </summary>
    public class ProfilePointMM
    {
        /// <summary>沿焊缝方向坐标（mm）</summary>
        public double Y;
        /// <summary>高度方向坐标（mm）</summary>
        public double Z;

        public ProfilePointMM() { Y = 0; Z = 0; }

        public ProfilePointMM(double y, double z) { Y = y; Z = z; }

        public static ProfilePointMM operator +(ProfilePointMM a, ProfilePointMM b)
        {
            return new ProfilePointMM(a.Y + b.Y, a.Z + b.Z);
        }

        public static ProfilePointMM operator -(ProfilePointMM a, ProfilePointMM b)
        {
            return new ProfilePointMM(a.Y - b.Y, a.Z - b.Z);
        }

        public double DistanceTo(ProfilePointMM other)
        {
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dy * dy + dz * dz);
        }

        public override string ToString()
        {
            return string.Format("({0:F3}, {1:F3})mm", Y, Z);
        }
    }

    /// <summary>
    /// 固定焊接接头形式（截面几何精定位，区别于旧 ImageAlgorithm.JointType 的宏观拓扑粗分类）。
    /// 两者解耦、互不替代（算法架构设计 §6.1）。
    /// </summary>
    public enum SeamJointType
    {
        Straight,   // 平直焊缝（对接/平角）
        VGroove,    // V 形焊缝（开坡口）
        RCorner,    // R 角焊缝（圆角角接）
        SingleSide  // 单边焊缝（搭接/单边角）
    }

    /// <summary>
    /// 单边缝（mm）：起点 + 终点。
    /// </summary>
    public class SeamEdgeMM
    {
        public ProfilePointMM Start;
        public ProfilePointMM End;

        public SeamEdgeMM()
        {
            Start = new ProfilePointMM();
            End = new ProfilePointMM();
        }

        public SeamEdgeMM(ProfilePointMM start, ProfilePointMM end)
        {
            Start = start ?? new ProfilePointMM();
            End = end ?? new ProfilePointMM();
        }

        /// <summary>边缝中点（mm），用于推导名义指引点。</summary>
        public ProfilePointMM Center()
        {
            return new ProfilePointMM((Start.Y + End.Y) / 2.0, (Start.Z + End.Z) / 2.0);
        }
    }

    /// <summary>
    /// 某接头形式的几何模板（mm）。纯数据 + 纯几何，不含 EMGU / Mat / 配置 / I/O
    /// （算法架构设计 §3.2.3）。指引点由 Left/Right 边推导，可在 mm 空间叠加操作员偏移。
    /// </summary>
    public class SeamModel
    {
        public SeamJointType Type;
        public SeamEdgeMM LeftEdge;
        public SeamEdgeMM RightEdge;

        /// <summary>操作员偏移（mm），叠加在 NominalGuide 上。</summary>
        public ProfilePointMM OffsetMM;

        public SeamModel()
        {
            Type = SeamJointType.Straight;
            LeftEdge = new SeamEdgeMM();
            RightEdge = new SeamEdgeMM();
            OffsetMM = new ProfilePointMM();
        }

        public SeamModel(SeamJointType type, SeamEdgeMM left, SeamEdgeMM right)
        {
            Type = type;
            LeftEdge = left ?? new SeamEdgeMM();
            RightEdge = right ?? new SeamEdgeMM();
            OffsetMM = new ProfilePointMM();
        }

        /// <summary>
        /// 名义指引点（mm）= 由 Left/Right 边推导的几何中点。
        /// 当前实现取两条边中点的中点作为通用名义点；V 形槽底、R 角切点、单边角点等
        /// 类型特化可由子类或后续匹配结果细化（仍为纯几何，可独立单测）。
        /// </summary>
        public ProfilePointMM NominalGuide
        {
            get { return ComputeNominal(Type, LeftEdge, RightEdge); }
        }

        /// <summary>最终指引点（mm）= 名义 + 操作员偏移。</summary>
        public ProfilePointMM FinalGuide
        {
            get { return NominalGuide + OffsetMM; }
        }

        public static ProfilePointMM ComputeNominal(SeamJointType type, SeamEdgeMM left, SeamEdgeMM right)
        {
            if (left == null || right == null) return new ProfilePointMM();
            ProfilePointMM lc = left.Center();
            ProfilePointMM rc = right.Center();
            return new ProfilePointMM((lc.Y + rc.Y) / 2.0, (lc.Z + rc.Z) / 2.0);
        }
    }

    /// <summary>
    /// 接头匹配结果（mm）。纯数据：匹配到的接头形式 + 匹配置信度 + 定位到的参考几何(mm)
    /// （算法架构设计 §6.5）。
    /// </summary>
    public class SeamMatchResult
    {
        /// <summary>匹配到的固定接头形式</summary>
        public SeamJointType JointType;
        /// <summary>匹配置信度 0~1</summary>
        public double Score;
        /// <summary>定位到的参考几何：左/右边缝(mm)</summary>
        public SeamEdgeMM LeftEdge;
        public SeamEdgeMM RightEdge;

        public SeamMatchResult()
        {
            JointType = SeamJointType.Straight;
            Score = 0;
            LeftEdge = new SeamEdgeMM();
            RightEdge = new SeamEdgeMM();
        }
    }
}
