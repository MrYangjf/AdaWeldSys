using System.Collections.Generic;

namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>PVT 多段插值能力</summary>
    /// <remarks>
    /// 面向连续跟随场景（焊缝跟踪）的流式接口：起始段下发后可不断追加点段，
    /// 由调用方根据水位自行决定续喂节拍。本接口不感知控制器型号与脉冲换算。
    /// 轴在调用前必须已切到 CSP 模式并伺服使能，接口不负责模式切换。
    /// 配套数据结构 PVTPoint 与 PVTInformation 一并定义在本文件，避免能力契约碎片化。
    /// </remarks>
    public interface IPVTMotion
    {
        /// <summary>下发起始段并开始运动</summary>
        /// <param name="points">起始段点集合，位置与速度为工程单位，时间为秒</param>
        /// <param name="timeMode">时间模式，决定 Time 按相对还是绝对解释</param>
        /// <param name="isFinal">是否为最后一段，置位后不再接受续喂</param>
        /// <param name="retain">保持标志，语义待现场实测确认</param>
        /// <param name="sdStopTime">平滑停止时间（秒）</param>
        /// <returns>台达返回码，0 为成功</returns>
        ushort BeginMove(IList<PVTPoint> points, PVTTimeMode timeMode, bool isFinal, bool retain,
            double sdStopTime);

        /// <summary>运动中追加点段，保持轨迹不断流</summary>
        /// <param name="points">追加段点集合，单位同 BeginMove</param>
        /// <param name="isFinal">是否为最后一段</param>
        /// <returns>台达返回码，0 为成功</returns>
        ushort ContinueMove(IList<PVTPoint> points, bool isFinal);

        /// <summary>查询执行进度与剩余缓冲区</summary>
        /// <param name="info">进度与水位信息，失败时内容未定义</param>
        /// <returns>台达返回码，0 为成功</returns>
        ushort GetInformation(out PVTInformation info);

        /// <summary>启动循环模式，点表执行完后自动回到起点重放</summary>
        /// <returns>台达返回码，0 为成功</returns>
        ushort CycleStart();
    }

    /// <summary>PVT 轨迹点</summary>
    /// <remarks>
    /// 位置与速度一律工程单位（毫米、毫米每秒），时间为秒，与 MontionAxis 的单位语义一致。
    /// 脉冲换算只在 L3 台达实现层完成，本结构不感知控制器型号。
    /// </remarks>
    public struct PVTPoint
    {
        #region 公共变量

        /// <summary>目标位置（工程单位）</summary>
        public double Pos { get; private set; }

        /// <summary>到达该点的时间（秒）</summary>
        public double Time { get; private set; }

        /// <summary>到达该点时的速度（工程单位/秒）</summary>
        public double Vel { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>构造一个 PVT 轨迹点</summary>
        /// <param name="pos">目标位置（工程单位）</param>
        /// <param name="time">到达该点的时间（秒）</param>
        /// <param name="vel">到达该点时的速度（工程单位/秒）</param>
        public PVTPoint(double pos, double time, double vel)
        {
            Pos = pos;
            Time = time;
            Vel = vel;
        }

        #endregion
    }

    /// <summary>PVT 执行进度与缓冲区水位</summary>
    /// <remarks>剩余缓冲区是流式续喂的节拍判据，水位低于阈值时应追加下一段。</remarks>
    public struct PVTInformation
    {
        #region 公共变量

        /// <summary>本段总点数</summary>
        public int TotalCount { get; private set; }

        /// <summary>已完成点数</summary>
        public int DoneCount { get; private set; }

        /// <summary>剩余缓冲区容量，单位与 TotalCount 一致</summary>
        public int RemainBufferSize { get; private set; }

        /// <summary>运行状态字</summary>
        public ushort Status { get; private set; }

        /// <summary>剩余待执行点数</summary>
        public int RemainCount { get { return TotalCount - DoneCount; } }

        #endregion

        #region 构造函数

        /// <summary>构造一组 PVT 运行信息</summary>
        /// <param name="totalCount">本段总点数</param>
        /// <param name="doneCount">已完成点数</param>
        /// <param name="remainBufferSize">剩余缓冲区容量</param>
        /// <param name="status">运行状态字</param>
        public PVTInformation(int totalCount, int doneCount, int remainBufferSize, ushort status)
        {
            TotalCount = totalCount;
            DoneCount = doneCount;
            RemainBufferSize = remainBufferSize;
            Status = status;
        }

        #endregion
    }

    /// <summary>PVT 返回码</summary>
    /// <remarks>成功码与台达 SDK 一致；不支持码为抽象层自定义，用于未实现 PVT 的轴。</remarks>
    public static class PVTResult
    {
        #region 公共变量

        /// <summary>执行成功</summary>
        public const ushort OK = 0;

        /// <summary>当前轴未实现 PVT 能力</summary>
        public const ushort NotSupported = 0xFFFF;

        #endregion
    }
}
