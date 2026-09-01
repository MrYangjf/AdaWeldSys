using System;

namespace AdaWeldSystem.MotionControl.API
{
    /// <summary>
    /// 运动轴抽象接口
    /// 定义单轴运动控制的基本操作，由具体硬件实现（伺服/步进/直线电机）。
    /// </summary>
    public interface IAxis
    {
        /// <summary>轴名称</summary>
        string AxisName { get; }

        /// <summary>轴号（在控制器中的索引）</summary>
        ushort AxisNumber { get; set; }

        /// <summary>当前轴位置（mm）</summary>
        double CurrentPosition { get; }

        /// <summary>轴是否正在运动</summary>
        bool IsMoving { get; }

        /// <summary>伺服是否已使能</summary>
        bool IsServoOn { get; }

        /// <summary>轴是否已连接</summary>
        bool IsConnected { get; }

        /// <summary>轴停止事件</summary>
        event EventHandler Stopped;

        /// <summary>绝对运动（阻塞式，等待运动完成）</summary>
        /// <param name="positionMm">目标位置（mm）</param>
        /// <param name="speed">运动速度（mm/s）</param>
        /// <returns>是否成功到达目标位置</returns>
        bool MoveTo(double positionMm, double speed = 10.0);

        /// <summary>绝对运动（非阻塞式，立即返回）</summary>
        /// <param name="positionMm">目标位置（mm）</param>
        /// <param name="speed">运动速度（mm/s）</param>
        void MoveToNoBlock(double positionMm, double speed = 10.0);

        /// <summary>相对运动（阻塞式，等待运动完成）</summary>
        /// <param name="deltaMm">增量距离（mm，正数正向，负数负向）</param>
        /// <param name="speed">运动速度（mm/s）</param>
        /// <returns>是否成功到达目标位置</returns>
        bool MoveRelative(double deltaMm, double speed = 10.0);

        /// <summary>停止轴运动</summary>
        /// <param name="immediate">true=立即停止，false=减速停止</param>
        void Stop(bool immediate = false);

        /// <summary>伺服使能</summary>
        bool ServoOn();

        /// <summary>伺服关闭</summary>
        bool ServoOff();

        /// <summary>回零运动</summary>
        bool Home();

        /// <summary>点动运动</summary>
        /// <param name="direction">true=正向，false=负向</param>
        /// <param name="speed">点动速度（mm/s）</param>
        void JogMove(bool direction, double speed = 10.0);

        /// <summary>设置当前位置为原点</summary>
        void SetHomePosition();

        /// <summary>等待运动完成（超时）</summary>
        /// <param name="timeoutMs">超时时间（ms），0=无限等待</param>
        /// <returns>是否在超时前完成</returns>
        bool WaitMoveDone(int timeoutMs = 0);

        // ===== PTPVT 自定义曲线运动 =====

        /// <summary>
        /// PT模式运动：多点位置+时间的绝对运动
        /// </summary>
        /// <param name="timeArray">时间点数组（ms，绝对时间）</param>
        /// <param name="positionArray">位置点数组（mm）</param>
        /// <returns>是否成功启动</returns>
        bool MovePt(uint[] timeArray, double[] positionArray);

        /// <summary>
        /// PVT模式运动：多点位置+速度+时间的绝对运动
        /// </summary>
        /// <param name="timeArray">时间点数组（ms）</param>
        /// <param name="positionArray">位置点数组（mm）</param>
        /// <param name="speedArray">速度点数组（mm/s）</param>
        /// <returns>是否成功启动</returns>
        bool MovePvt(uint[] timeArray, double[] positionArray, double[] speedArray);
    }
}