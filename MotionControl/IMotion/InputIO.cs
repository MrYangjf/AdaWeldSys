namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>数字输入抽象基类</summary>
    public abstract class InputIO : MontionIO
    {
        #region 构造函数

        /// <summary>创建输入 IO 并注册到所属控制器</summary>
        /// <param name="container">所属控制器</param>
        /// <param name="ioName">IO 名称</param>
        protected InputIO(MontionControl container, string ioName)
            : base(container, ioName)
        {
        }

        #endregion

        #region 公共函数

        /// <summary>获取 IO 方向</summary>
        /// <returns>恒返回输入</returns>
        public override IOType GetIOType()
        {
            return IOType.Input;
        }

        #endregion
    }
}
