namespace AdaWeldSystem.MotionControl.DeltaEtherCAT.Native
{
    /// <summary>台达返回码常量</summary>
    /// <remarks>取值取自 SDK 原档 EtherCAT_DLL_Err.cs；其余返回码可经 GetMessage 取原文描述。</remarks>
    public static class DeltaErrorCode
    {
        #region 通用

        /// <summary>成功</summary>
        public const ushort NO_ERROR = 0x0000;

        #endregion

        #region 硬件

        /// <summary>硬件未初始化</summary>
        public const ushort HW_NO_INITIALIZE = 0x0001;

        /// <summary>未找到主站</summary>
        public const ushort NO_MASTER = 0x0019;

        /// <summary>未找到从站</summary>
        public const ushort NO_SLAVE = 0x001A;

        #endregion

        #region 初始化

        /// <summary>扫描不到从站</summary>
        public const ushort NO_SLAVE_FOUND = 0x1000;

        /// <summary>初始化超时</summary>
        public const ushort INITIAL_TIMEOUT = 0x1001;

        /// <summary>从站模式切换失败</summary>
        public const ushort MODE_CHANGE_FAILED = 0x1002;

        /// <summary>从站号非法</summary>
        public const ushort SLAVE_ID = 0x1003;

        #endregion

        #region 状态检查

        /// <summary>需先初始化</summary>
        public const ushort NEED_INITIAL = 0x1100;

        /// <summary>需先复位报警</summary>
        public const ushort NEED_RALM = 0x1104;

        /// <summary>需先伺服使能</summary>
        public const ushort NEED_SVON = 0x1105;

        /// <summary>需先配置回零</summary>
        public const ushort NEED_HOMECONFIG = 0x1106;

        #endregion

        #region 参数与模式

        /// <summary>环形缓冲区满</summary>
        public const ushort RING_BUFFER_FULL = 0x1200;

        /// <summary>接口参数错误</summary>
        public const ushort API_PARAMETER = 0x1201;

        /// <summary>当前模式不支持该指令</summary>
        public const ushort MODE_NOT_SUPPORT = 0x1204;

        #endregion

        #region IO 与运动

        /// <summary>数字量通道非法</summary>
        public const ushort DIO_CHANNEL_INVALID = 0x0200;

        /// <summary>模拟量通道非法</summary>
        public const ushort ADDA_CHANNEL_INVALID = 0x0201;

        /// <summary>上一次运动尚未完成</summary>
        public const ushort MOTION_NOT_FINISHED = 0x0202;

        #endregion

        #region SDO 与 PDO

        /// <summary>PDO 发送失败</summary>
        public const ushort PDO_TX_FAILED = 0x1300;

        /// <summary>SDO 通讯超时</summary>
        public const ushort SDO_TIMEOUT = 0x1301;

        #endregion

        #region DLL

        /// <summary>动态库已被占用</summary>
        public const ushort DLL_IS_USED = 0xF000;

        /// <summary>未找到动态库</summary>
        public const ushort NO_DLL_FOUND = 0xF001;

        /// <summary>功能不支持</summary>
        public const ushort NOT_SUPPORT = 0xF009;

        #endregion

        #region 公共函数

        /// <summary>取返回码的原文描述</summary>
        /// <param name="code">返回码</param>
        /// <returns>描述文本，取不到时返回空串</returns>
        public static string GetMessage(ushort code)
        {
            string msg = new string('\0', 256);
            ushort ret = EtherCATMasterNative.CS_ECAT_Master_Get_Return_Code_Message(code, msg);
            if (ret != NO_ERROR) return string.Empty;
            return msg.TrimEnd('\0').Trim();
        }

        #endregion
    }
}
