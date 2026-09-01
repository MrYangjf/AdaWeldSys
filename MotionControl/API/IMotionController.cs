using System;
using System.Collections.Generic;
using AdaWeldSystem.MotionControl.ZMotion;

namespace AdaWeldSystem.MotionControl.API
{
    /// <summary>
    /// 运动控制器接口
    /// 管理轴和IO的控制器抽象，所有具体硬件控制器实现此接口。
    /// 参考 PcomDeviceInterface 的 MontionControl 设计模式。
    /// </summary>
    public interface IMotionController
    {
        /// <summary>控制器名称</summary>
        string Name { get; }

        /// <summary>控制器是否已连接</summary>
        bool IsConnected { get; }

        /// <summary>轴数量</summary>
        int AxisCount { get; }

        /// <summary>输入IO数量</summary>
        int InputCount { get; }

        /// <summary>输出IO数量</summary>
        int OutputCount { get; }

        /// <summary>急停标志</summary>
        bool IsEmergencyStop { get; }

        // ===== 控制器生命周期 =====

        /// <summary>初始化控制器（建立连接）</summary>
        /// <returns>是否成功</returns>
        bool Initialize();

        /// <summary>关闭控制器（断开连接）</summary>
        void Close();

        // ===== 轴管理 =====

        /// <summary>添加轴到控制器</summary>
        void AddAxis(IAxis axis);

        /// <summary>从控制器移除轴</summary>
        void RemoveAxis(IAxis axis);

        /// <summary>获取所有轴列表</summary>
        IReadOnlyList<IAxis> GetAxes();

        /// <summary>按轴号获取轴</summary>
        IAxis GetAxis(ushort axisNumber);

        /// <summary>按名称获取轴</summary>
        IAxis GetAxisByName(string axisName);

        // ===== IO管理 =====

        /// <summary>添加IO到控制器（自动区分输入/输出）</summary>
        void AddIO(IIO io);

        /// <summary>从控制器移除IO</summary>
        void RemoveIO(IIO io);

        /// <summary>清除所有IO</summary>
        void ClearIO();

        /// <summary>获取所有输入IO列表</summary>
        IReadOnlyList<IIO> GetInputs();

        /// <summary>获取所有输出IO列表</summary>
        IReadOnlyList<IIO> GetOutputs();

        /// <summary>按IO号获取输入IO</summary>
        IInputIO GetInput(ushort ioNumber);

        /// <summary>按IO号获取输出IO</summary>
        IOutputIO GetOutput(ushort ioNumber);

        // ===== 急停控制 =====

        /// <summary>设置急停（传播到所有轴和输出IO）</summary>
        void SetEmergencyStop(bool isEmergencyStop);

        // ===== 心跳检测 =====

        /// <summary>心跳状态变化事件</summary>
        event HeartbeatEventHandler HeartbeatStateChanged;

        /// <summary>启动心跳检测</summary>
        bool StartHeartbeat(int intervalMs, int timeoutMs);

        /// <summary>停止心跳检测</summary>
        void StopHeartbeat();

        /// <summary>手动执行一次心跳检测</summary>
        bool CheckHeartbeat();

        // ===== 连续插补 =====

        /// <summary>创建连续插补引擎</summary>
        /// <param name="mainAxis">主轴号</param>
        /// <param name="axisList">参与插补的轴列表</param>
        /// <returns>插补引擎实例</returns>
        InterpolationEngine CreateInterpolationEngine(int mainAxis, int[] axisList);

        // ===== 电子凸轮 =====

        /// <summary>
        /// 写入凸轮曲线到TABLE寄存器
        /// </summary>
        /// <param name="tableStart">TABLE起始索引</param>
        /// <param name="camPoints">凸轮曲线点数组（从轴位置，脉冲单位）</param>
        /// <returns>是否成功</returns>
        bool WriteCamTable(int tableStart, float[] camPoints);

        /// <summary>
        /// 启动电子凸轮（CAMBOX）
        /// </summary>
        /// <param name="slaveAxis">从轴号</param>
        /// <param name="masterAxis">主轴号</param>
        /// <param name="tableStart">TABLE表起始索引</param>
        /// <param name="tableEnd">TABLE表结束索引</param>
        /// <param name="slaveCycle">从轴周期长度（脉冲）</param>
        /// <param name="masterCycle">主轴周期长度（脉冲）</param>
        /// <param name="direction">旋向（0=正向，1=反向）</param>
        /// <param name="mode">模式（0=绝对，1=相对，2=周期性）</param>
        /// <returns>是否成功</returns>
        bool StartCambox(int slaveAxis, int masterAxis, int tableStart, int tableEnd,
            float slaveCycle, float masterCycle, int direction, int mode);

        /// <summary>
        /// 停止电子凸轮
        /// </summary>
        /// <param name="slaveAxis">从轴号</param>
        /// <param name="immediate">是否立即停止（true=急停，false=减速停止）</param>
        /// <returns>是否成功</returns>
        bool StopCambox(int slaveAxis, bool immediate);

        /// <summary>
        /// 追剪位置同步（Movelink）
        /// </summary>
        /// <param name="slaveAxis">从轴号</param>
        /// <param name="masterAxis">主轴号</param>
        /// <param name="distance">同步距离（从轴）</param>
        /// <param name="speed">同步速度</param>
        /// <param name="accel">加速度</param>
        /// <param name="decel">减速度</param>
        /// <param name="masterPos">主轴起始位置</param>
        /// <param name="mode">模式（0=相对，1=绝对）</param>
        /// <returns>是否成功</returns>
        bool Movelink(int slaveAxis, int masterAxis, float distance,
            float speed, float accel, float decel, float masterPos, int mode);

        /// <summary>
        /// 追剪速度同步（Moveslink）
        /// </summary>
        /// <param name="slaveAxis">从轴号</param>
        /// <param name="masterAxis">主轴号</param>
        /// <param name="distance">同步距离（从轴）</param>
        /// <param name="speed">同步速度</param>
        /// <param name="startRatio">起始速度比</param>
        /// <param name="endRatio">结束速度比</param>
        /// <param name="masterPos">主轴起始位置</param>
        /// <param name="mode">模式（0=相对，1=绝对）</param>
        /// <returns>是否成功</returns>
        bool Moveslink(int slaveAxis, int masterAxis, float distance,
            float speed, float startRatio, float endRatio, float masterPos, int mode);

        // ===== 主动上报 =====

        /// <summary>主动上报数据接收事件</summary>
        event AutoReportEventHandler AutoReportReceived;

        /// <summary>
        /// 启用主动上报
        /// </summary>
        /// <param name="reportItems">上报项配置字符串</param>
        /// <param name="intervalMs">上报间隔（ms）</param>
        /// <returns>是否成功</returns>
        bool EnableAutoReport(string reportItems, int intervalMs);

        /// <summary>
        /// 禁用主动上报
        /// </summary>
        /// <returns>是否成功</returns>
        bool DisableAutoReport();

        // ===== 周期上报 =====

        /// <summary>周期上报数据接收事件</summary>
        event CycleReportEventHandler CycleReportReceived;

        /// <summary>
        /// 启用周期上报
        /// </summary>
        /// <param name="channel">通道号</param>
        /// <param name="cycleMs">周期（ms）</param>
        /// <param name="paramString">参数字符串，如 "DPOS(0,5),MSPEED(0,3)"</param>
        /// <returns>是否成功</returns>
        bool EnableCycleReport(uint channel, uint cycleMs, string paramString);

        /// <summary>
        /// 禁用周期上报
        /// </summary>
        /// <param name="channel">通道号</param>
        /// <returns>是否成功</returns>
        bool DisableCycleReport(uint channel);

        /// <summary>
        /// 强制触发一次周期上报
        /// </summary>
        /// <param name="channel">通道号</param>
        /// <returns>是否成功</returns>
        bool ForceCycleReportOnce(uint channel);

        // ===== 配置管理 =====

        /// <summary>
        /// 从配置加载轴和IO（使用轴字典/IO字典）
        /// </summary>
        /// <returns>是否成功</returns>
        bool LoadFromConfig();

        /// <summary>
        /// 保存当前轴和IO配置
        /// </summary>
        /// <returns>是否成功</returns>
        bool SaveToConfig();
    }

    // ===== 委托定义 =====

    /// <summary>心跳状态变化委托</summary>
    public delegate void HeartbeatEventHandler(object sender, HeartbeatEventArgs e);

    /// <summary>心跳事件参数</summary>
    public class HeartbeatEventArgs : EventArgs
    {
        /// <summary>连接是否正常</summary>
        public bool IsConnected { get; set; }

        /// <summary>错误信息</summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>主动上报数据接收委托</summary>
    public delegate void AutoReportEventHandler(object sender, AutoReportEventArgs e);

    /// <summary>主动上报事件参数</summary>
    public class AutoReportEventArgs : EventArgs
    {
        /// <summary>功能码</summary>
        public int TypeCode { get; set; }

        /// <summary>数据长度</summary>
        public int DataLength { get; set; }

        /// <summary>数据内容</summary>
        public string Data { get; set; }
    }

    /// <summary>周期上报数据接收委托</summary>
    public delegate void CycleReportEventHandler(object sender, CycleReportEventArgs e);

    /// <summary>周期上报事件参数</summary>
    public class CycleReportEventArgs : EventArgs
    {
        /// <summary>通道号</summary>
        public uint Channel { get; set; }

        /// <summary>上报次数</summary>
        public uint ReportCount { get; set; }
    }
}