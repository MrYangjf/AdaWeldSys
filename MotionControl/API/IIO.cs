namespace AdaWeldSystem.MotionControl.API
{
    /// <summary>
    /// IO类型枚举
    /// </summary>
    public enum IOType
    {
        /// <summary>输入IO</summary>
        Input,

        /// <summary>输出IO</summary>
        Output
    }

    /// <summary>
    /// IO抽象接口
    /// 定义输入/输出IO的基本操作。
    /// </summary>
    public interface IIO
    {
        /// <summary>IO名称</summary>
        string IOName { get; }

        /// <summary>IO号（在控制器中的索引）</summary>
        ushort IONumber { get; set; }

        /// <summary>IO是否导通</summary>
        bool IsOn { get; }

        /// <summary>获取IO类型</summary>
        IOType GetIOType();

        /// <summary>获取IO状态值</summary>
        int GetIOStatus();
    }

    /// <summary>
    /// 输入IO接口
    /// </summary>
    public interface IInputIO : IIO
    {
        /// <summary>是否启用检测</summary>
        bool IsEnabled { get; set; }
    }

    /// <summary>
    /// 输出IO接口
    /// </summary>
    public interface IOutputIO : IIO
    {
        /// <summary>设置输出为ON</summary>
        void SetON();

        /// <summary>设置输出为OFF</summary>
        void SetOFF();

        /// <summary>急停时是否自动关闭</summary>
        bool EmergencyStopOff { get; set; }
    }
}