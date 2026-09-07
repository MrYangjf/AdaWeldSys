namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>数字输出抽象基类</summary>
    public abstract class OutputIO : MontionIO
    {
        #region 构造函数

        /// <summary>创建输出 IO 并注册到所属控制器</summary>
        /// <param name="container">所属控制器</param>
        /// <param name="ioName">IO 名称</param>
        protected OutputIO(MontionControl container, string ioName)
            : base(container, ioName)
        {
        }

        #endregion

        #region 公共函数

        /// <summary>置位输出</summary>
        /// <returns>0 为成功，其他为错误码</returns>
        public abstract int SetON();

        /// <summary>复位输出</summary>
        /// <returns>0 为成功，其他为错误码</returns>
        public abstract int SetOFF();

        /// <summary>获取 IO 方向</summary>
        /// <returns>恒返回输出</returns>
        public override IOType GetIOType()
        {
            return IOType.Output;
        }

        #endregion
    }
}
