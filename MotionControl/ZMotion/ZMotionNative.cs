using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 正运动（ZMotion）控制器原生 API 封装
    /// 基于官方 Zmcaux.cs 精简，仅封装本项目所需的连接、轴运动、IO 操作函数。
    /// 依赖 zauxdll.dll（64位），源文件位于项目根 3rdDepend\ 目录，
    /// 构建后通过 CopyNative3rdDependToOutput 目标拷贝到输出目录（bin\Debug\），
    /// 运行时 DllImport 从程序输出目录加载。
    /// </summary>
    internal static class ZMotionNative
    {
        private const string DllName = "zauxdll.dll";

        // ============================================================
        // 连接管理
        // ============================================================

        /// <summary>
        /// 以太网方式连接控制器
        /// </summary>
        /// <param name="ipaddr">IP地址</param>
        /// <param name="phandle">返回的连接句柄</param>
        /// <returns>错误码，0表示成功</returns>
        [DllImport(DllName, EntryPoint = "ZAux_OpenEth",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_OpenEth(string ipaddr, out IntPtr phandle);

        /// <summary>
        /// 关闭控制器连接
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Close",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Close(IntPtr handle);

        /// <summary>
        /// Execute在线命令（执行BASIC指令）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Execute",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Execute(IntPtr handle, string pszCommand,
            StringBuilder psResponse, uint uiResponseLength);

        // ============================================================
        // 轴参数设置
        // ============================================================

        /// <summary>设置轴类型</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetAtype",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetAtype(IntPtr handle, int iaxis, int iValue);

        /// <summary>设置轴脉冲当量（units）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetUnits",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetUnits(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置起始速度</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetLspeed",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetLspeed(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置运动速度</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetSpeed",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetSpeed(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置加速度</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetAccel",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetAccel(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置减速度</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetDecel",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetDecel(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置S曲线时间</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetSramp",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetSramp(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置轴正向软限位</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetFsLimit",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetFsLimit(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置轴负向软限位</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetRsLimit",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetRsLimit(IntPtr handle, int iaxis, float fValue);

        // ============================================================
        // 轴运动控制
        // ============================================================

        /// <summary>
        /// 单轴相对运动
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="iaxis">轴号</param>
        /// <param name="fdistance">相对距离（脉冲单位或units单位）</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Move",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Single_Move(IntPtr handle, int iaxis, float fdistance);

        /// <summary>
        /// 单轴绝对运动
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_MoveAbs",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Single_MoveAbs(IntPtr handle, int iaxis, float fdistance);

        /// <summary>
        /// 单轴连续运动（Jog）
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="iaxis">轴号</param>
        /// <param name="idir">方向：1正，-1负</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Vmove",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Single_Vmove(IntPtr handle, int iaxis, int idir);

        /// <summary>
        /// 单轴停止（取消运动）
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="iaxis">轴号</param>
        /// <param name="imode">停止模式：0-减速停止, 1-急停, 2-直接切断脉冲</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Cancel",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Single_Cancel(IntPtr handle, int iaxis, int imode);

        /// <summary>
        /// 单轴回零
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Single_Home",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Single_Home(IntPtr handle, int iaxis, int imode);

        // ============================================================
        // 轴状态读取
        // ============================================================

        /// <summary>
        /// 读取轴规划位置（Dpos）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetDpos",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetDpos(IntPtr handle, int iaxis, ref float pfValue);

        /// <summary>
        /// 读取轴实际位置（Mpos）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetMpos",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetMpos(IntPtr handle, int iaxis, ref float pfValue);

        /// <summary>
        /// 设置轴规划位置（Dpos）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetDpos",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetDpos(IntPtr handle, int iaxis, float fValue);

        /// <summary>
        /// 读取轴是否空闲（停止状态）
        /// </summary>
        /// <param name="piValue">返回值：0-运动中，1-停止</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetIfIdle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetIfIdle(IntPtr handle, int iaxis, ref int piValue);

        /// <summary>
        /// 读取轴当前速度（VpSpeed，规划速度）
        /// </summary>
        /// <param name="pfValue">返回当前速度（mm/s）</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetVpSpeed",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetVpSpeed(IntPtr handle, int iaxis, ref float pfValue);

        /// <summary>
        /// 读取轴状态
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetAxisStatus",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetAxisStatus(IntPtr handle, int iaxis, ref int piValue);

        /// <summary>
        /// 读取轴使能状态
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetAxisEnable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetAxisEnable(IntPtr handle, int iaxis, ref int piValue);

        /// <summary>
        /// 设置轴使能
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetAxisEnable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetAxisEnable(IntPtr handle, int iaxis, int iValue);

        /// <summary>
        /// 读取轴停止原因
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetAxisStopReason",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetAxisStopReason(IntPtr handle, int iaxis, ref int piValue);

        // ============================================================
        // IO 操作
        // ============================================================

        /// <summary>
        /// 读取输入IO状态
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="ionum">IO号</param>
        /// <param name="piValue">返回值：0-断开，1-导通</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetIn",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetIn(IntPtr handle, int ionum, ref uint piValue);

        /// <summary>
        /// 设置输出IO状态
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="ionum">输出IO号</param>
        /// <param name="iValue">0-关闭，1-打开</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetOp",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetOp(IntPtr handle, int ionum, uint iValue);

        /// <summary>
        /// 读取输出IO状态
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetOp",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetOp(IntPtr handle, int ionum, ref uint piValue);

        // ============================================================
        // 连续插补（多段直线运动）
        // ============================================================

        /// <summary>设置连续插补模式（0=关闭，1=开启）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetMerge",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetMerge(IntPtr handle, int iaxis, int iValue);

        /// <summary>设置拐角模式</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetCornerMode",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetCornerMode(IntPtr handle, int iaxis, int iValue);

        /// <summary>设置拐角开始减速角度（弧度）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetDecelAngle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetDecelAngle(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置拐角停止角度（弧度）</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetStopAngle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetStopAngle(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置全速度圆角半径</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetFullSpRadius",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetFullSpRadius(IntPtr handle, int iaxis, float fValue);

        /// <summary>设置Z向平滑时间</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetZsmooth",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetZsmooth(IntPtr handle, int iaxis, float fValue);

        /// <summary>获取直线缓冲区剩余空间</summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetRemain_LineBuffer",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetRemain_LineBuffer(IntPtr handle, int iaxis, ref int piValue);

        /// <summary>
        /// 多段多轴绝对直线插补
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="segments">段数</param>
        /// <param name="axisCount">轴数</param>
        /// <param name="axisList">轴列表数组</param>
        /// <param name="positions">目标位置数组（段数×轴数个float）</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_MultiMoveAbs",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_MultiMoveAbs(IntPtr handle, int segments,
            int axisCount, int[] axisList, float[] positions);

        /// <summary>
        /// 多段多轴相对直线插补
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_MultiMove",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_MultiMove(IntPtr handle, int segments,
            int axisCount, int[] axisList, float[] positions);

        // ============================================================
        // PTPVT 自定义曲线运动
        // ============================================================

        /// <summary>
        /// PT模式：多点位置+时间绝对运动
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="pointCount">点数</param>
        /// <param name="axisCount">轴数</param>
        /// <param name="axisList">轴列表</param>
        /// <param name="timeArray">时间数组（ms，每点的绝对时间）</param>
        /// <param name="positionArray">位置数组（点×轴个float）</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_MultiMovePtAbs",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_MultiMovePtAbs(IntPtr handle, int pointCount,
            int axisCount, int[] axisList, uint[] timeArray, float[] positionArray);

        /// <summary>
        /// PVT模式：多点位置+速度+时间绝对运动
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="pointCount">点数</param>
        /// <param name="axisCount">轴数</param>
        /// <param name="axisList">轴列表</param>
        /// <param name="timeArray">时间数组（ms）</param>
        /// <param name="positionArray">位置数组</param>
        /// <param name="speedArray">速度数组</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_MultiMovePvtAbs",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_MultiMovePvtAbs(IntPtr handle, int pointCount,
            int axisCount, int[] axisList, uint[] timeArray, float[] positionArray, float[] speedArray);

        // ============================================================
        // 电子凸轮（CAMBOX + 追剪）
        // ============================================================

        /// <summary>
        /// 写入TABLE寄存器（凸轮曲线表）
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="tableIndex">TABLE起始索引</param>
        /// <param name="count">数据点数</param>
        /// <param name="data">数据数组</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetTable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetTable(IntPtr handle, int tableIndex,
            int count, float[] data);

        /// <summary>
        /// 读取TABLE寄存器
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetTable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetTable(IntPtr handle, int tableIndex,
            int count, float[] data);

        /// <summary>
        /// 启动电子凸轮（CAMBOX）
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="slaveAxis">从轴号</param>
        /// <param name="tableStart">TABLE表起始索引</param>
        /// <param name="tableEnd">TABLE表结束索引</param>
        /// <param name="slaveCycle">从轴周期长度（脉冲）</param>
        /// <param name="masterCycle">主轴周期长度（脉冲）</param>
        /// <param name="masterAxis">主轴号</param>
        /// <param name="direction">旋向（0=正向，1=反向）</param>
        /// <param name="mode">模式（0=绝对，1=相对，2=周期性）</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Cambox",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Cambox(IntPtr handle, int slaveAxis,
            int tableStart, int tableEnd, float slaveCycle, float masterCycle,
            int masterAxis, int direction, int mode);

        /// <summary>
        /// 停止电子凸轮
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_CamStop",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_CamStop(IntPtr handle, int slaveAxis, int imode);

        /// <summary>
        /// 向运行中的 CAMBOX 动态追加一个轨迹点（实时加点）
        /// 从轴在主轴走到 <paramref name="masterPosition"/> 时，驱动到 <paramref name="slavePosition"/>。
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="slaveAxis">从轴号</param>
        /// <param name="masterPosition">主轴位置（单位：主轴单位）</param>
        /// <param name="slavePosition">从轴位置（单位：从轴单位）</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_CamBox_AddPoint",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_CamBox_AddPoint(IntPtr handle, int slaveAxis,
            float masterPosition, float slavePosition);

        /// <summary>
        /// 触发运动（立即执行缓冲中的指令）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Trigger",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Trigger(IntPtr handle, int iaxis);

        /// <summary>
        /// 位置同步模式（追剪Movelink）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Movelink",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Movelink(IntPtr handle, int slaveAxis,
            float distance, float speed, float accel, float decel,
            int masterAxis, float masterPos, int mode);

        /// <summary>
        /// 速度同步模式（追剪Moveslink）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Moveslink",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Moveslink(IntPtr handle, int slaveAxis,
            float distance, float speed, float startRatio, float endRatio,
            int masterAxis, float masterPos, int mode);

        /// <summary>
        /// 运动中IO操作（飞剪开关）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_MoveOp",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_MoveOp(IntPtr handle, int iaxis,
            int opNumber, int opState);

        // ============================================================
        // 主动上报 / 周期上报
        // ============================================================

        /// <summary>主动上报回调委托</summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="typeCode">功能码/类型码</param>
        /// <param name="dataLength">数据长度</param>
        /// <param name="data">数据内容</param>
        public delegate void ZAuxCallBack(IntPtr handle, int typeCode, int dataLength, StringBuilder data);

        /// <summary>
        /// 设置主动上报回调函数
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_SetAutoUpCallBack",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_SetAutoUpCallBack(IntPtr handle, ZAuxCallBack callback);

        /// <summary>
        /// 下载BAS程序到控制器ROM
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="filePath">BAS文件路径</param>
        /// <param name="option">选项（1=下载到ROM）</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_BasDown",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_BasDown(IntPtr handle, string filePath, int option);

        /// <summary>
        /// 设置BAS全局变量（float型）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetUserVar",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetUserVar(IntPtr handle, string varName, float fValue);

        /// <summary>
        /// 读取BAS全局变量（float型）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetUserVar",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetUserVar(IntPtr handle, string varName, ref float pfValue);

        /// <summary>
        /// 设置BAS全局数组
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetUserArray",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetUserArray(IntPtr handle, string arrayName,
            int startIndex, int count, float[] data);

        // ============================================================
        // 周期上报
        // ============================================================

        /// <summary>
        /// 使能周期上报
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="channel">通道号</param>
        /// <param name="cycleMs">周期（ms）</param>
        /// <param name="paramString">参数字符串，如 "DPOS(0,5),MSPEED(0,3)"</param>
        /// <returns>错误码</returns>
        [DllImport(DllName, EntryPoint = "ZAux_CycleUpEnable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_CycleUpEnable(IntPtr handle, uint channel,
            uint cycleMs, string paramString);

        /// <summary>
        /// 关闭周期上报
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_CycleUpDisable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_CycleUpDisable(IntPtr handle, uint channel);

        /// <summary>
        /// 强制触发一次上报
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_CycleUpForceOnce",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_CycleUpForceOnce(IntPtr handle, uint channel);

        /// <summary>
        /// 获取已接收上报次数
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_CycleUpGetRecvTimes",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_CycleUpGetRecvTimes(IntPtr handle, uint channel, ref uint pCount);

        /// <summary>
        /// 读取上报缓冲区整型数据
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_CycleUpReadBuffInt",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_CycleUpReadBuffInt(IntPtr handle, uint channel,
            string paramName, int index, ref int pValue);

        // ============================================================
        // 心跳检测
        // ============================================================

        /// <summary>
        /// 获取控制器信息（用于心跳检测）
        /// </summary>
        /// <param name="handle">连接句柄</param>
        /// <param name="psResponse">响应缓冲区</param>
        /// <param name="uiResponseLength">响应缓冲区长度</param>
        /// <returns>错误码，0=成功，非0或响应为"no reply"表示断连</returns>
        [DllImport(DllName, EntryPoint = "ZAux_GetControllerInfo",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_GetControllerInfo(IntPtr handle,
            StringBuilder psResponse, uint uiResponseLength);

        // ============================================================
        // 批量读取轴参数
        // ============================================================

        /// <summary>
        /// 批量读取同一参数的多轴值
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetAllAxisPara",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetAllAxisPara(IntPtr handle, string paramName,
            int axisCount, int[] axisList, float[] values);

        // ============================================================
        // 位置比较输出（PSWITCH / 硬件位置比较）
        // ============================================================

        /// <summary>
        /// 软件位置比较输出（Pswitch）
        /// </summary>
        /// <param name="num">比较输出编号</param>
        /// <param name="enable">0-关闭，1-开启</param>
        /// <param name="axisnum">轴号</param>
        /// <param name="outnum">输出口编号（-1表示无输出）</param>
        /// <param name="outstate">输出状态（0-关闭，1-打开）</param>
        /// <param name="setpos">比较输出位置（正向）</param>
        /// <param name="resetpos">复位位置（负向）</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Pswitch",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Pswitch(IntPtr handle, int num, int enable,
            int axisnum, int outnum, int outstate, float setpos, float resetpos);

        /// <summary>
        /// 硬件位置比较输出（HwPswitch2）
        /// </summary>
        /// <param name="axisnum">轴号</param>
        /// <param name="mode">模式（1-开启，2-清除）</param>
        /// <param name="opnum">输出口编号</param>
        /// <param name="opstate">输出状态</param>
        /// <param name="modePara1">模式参数1（TABLE起始编号）</param>
        /// <param name="modePara2">模式参数2（TABLE结束编号）</param>
        /// <param name="modePara3">模式参数3（方向）</param>
        /// <param name="modePara4">模式参数4</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_HwPswitch2",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_HwPswitch2(IntPtr handle, int axisnum, int mode,
            int opnum, int opstate, float modePara1, float modePara2, float modePara3, float modePara4);

        // ============================================================
        // 编码器锁存（Regist）
        // ============================================================

        /// <summary>
        /// 触发轴编码器锁存
        /// </summary>
        /// <param name="iaxis">轴号（需为编码器轴）</param>
        /// <param name="imode">锁存模式（0-4为A锁存，10-14为B锁存等）</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Regist",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Regist(IntPtr handle, int iaxis, int imode);

        /// <summary>
        /// 读取锁存触发状态（Mark）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetMark",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetMark(IntPtr handle, int iaxis, ref int iValue);

        /// <summary>
        /// 读取锁存位置（RegPos）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetRegPos",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetRegPos(IntPtr handle, int iaxis, ref float fValue);

        // ============================================================
        // 螺距补偿（Pitchset）
        // ============================================================

        /// <summary>
        /// 设置轴螺距补偿（单向）
        /// </summary>
        /// <param name="iaxis">轴号（扩展轴无效）</param>
        /// <param name="iEnable">0-关闭，1-开启</param>
        /// <param name="startPos">补偿起始位置</param>
        /// <param name="maxpoint">最大补偿点数</param>
        /// <param name="disOne">补偿间隔</param>
        /// <param name="tablNum">补偿表起始引导地址</param>
        /// <param name="disances">补偿距离数组</param>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Pitchset",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Pitchset(IntPtr handle, int iaxis, int iEnable,
            float startPos, uint maxpoint, float disOne, uint tablNum, float[] disances);

        /// <summary>
        /// 设置轴螺距双向补偿
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_Pitchset2",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_Pitchset2(IntPtr handle, int iaxis, int iEnable,
            float startPos, uint maxpoint, float disOne, uint tablNum, float[] disances,
            uint revTablNum, float[] revDisances);

        /// <summary>
        /// 读取轴螺距补偿状态
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetPitchStatus",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetPitchStatus(IntPtr handle, int iaxis,
            ref int ifEnable, ref float pitchDist);

        // ============================================================
        // V3 通讯编码器轴（ATYPE=25）— ADR-030
        // ============================================================

        /// <summary>
        /// 设置轴类型（ATYPE）
        /// 通讯编码器轴 ATYPE=25：位置由 EtherCAT PDO 硬件刷新，非电机驱动。
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_SetParam",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_SetParam(IntPtr handle, string paramName,
            int iaxis, ref float pfValue);

        /// <summary>
        /// 读取轴类型（ATYPE）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZAux_Direct_GetParam",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int ZAux_Direct_GetParam(IntPtr handle, string paramName,
            int iaxis, ref float pfValue);

        // ============================================================
        // 辅助方法
        // ============================================================

        /// <summary>
        /// 检查返回值是否为成功（0 = 成功）
        /// </summary>
        public static bool IsSuccess(int ret)
        {
            return ret == 0;
        }

        /// <summary>
        /// 获取错误码描述（常见错误码）
        /// </summary>
        public static string GetErrorDescription(int errorCode)
        {
            switch (errorCode)
            {
                case 0: return "成功";
                case 1: return "指令错误";
                case 2: return "控制器忙";
                case 3: return "参数错误";
                case 4: return "轴号错误";
                case 5: return "句柄无效";
                case 6: return "存储错误";
                case 7: return "文件错误";
                case 8: return "不支持的功能";
                default: return string.Format("未知错误码 {0}", errorCode);
            }
        }
    }
}
