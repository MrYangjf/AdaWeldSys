namespace AdaWeldSystem.LineLaserCam.ILineLaser
{
    /// <summary>线激光校准数据快照（厂商无关的中性结构）</summary>
    /// <remarks>厂商层 SDK 回调捕获，业务层经 <see cref="LineLaserCameraBase.Calibration"/> 读取；
    /// 角点与中心单位毫米，索引 0 为横向、1 为高度。</remarks>
    public class LineLaserCalibration
    {
        #region 公共变量

        /// <summary>是否已捕获到有效校准数据</summary>
        public bool HasData { get; private set; }

        /// <summary>视野中心点（毫米）</summary>
        public float[] Center { get; private set; }

        /// <summary>左上角点（毫米）</summary>
        public float[] LeftTopCorner { get; private set; }

        /// <summary>右上角点（毫米）</summary>
        public float[] RightTopCorner { get; private set; }

        /// <summary>左下角点（毫米）</summary>
        public float[] LeftBottomCorner { get; private set; }

        /// <summary>右下角点（毫米）</summary>
        public float[] RightBottomCorner { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建空的校准数据</summary>
        public LineLaserCalibration()
        {
            Center = new float[2];
            LeftTopCorner = new float[2];
            RightTopCorner = new float[2];
            LeftBottomCorner = new float[2];
            RightBottomCorner = new float[2];
        }

        /// <summary>创建校准数据</summary>
        /// <param name="center">视野中心点</param>
        /// <param name="leftTop">左上角点</param>
        /// <param name="rightTop">右上角点</param>
        /// <param name="leftBottom">左下角点</param>
        /// <param name="rightBottom">右下角点</param>
        public LineLaserCalibration(float[] center, float[] leftTop, float[] rightTop,
            float[] leftBottom, float[] rightBottom)
        {
            HasData = true;
            Center = center;
            LeftTopCorner = leftTop;
            RightTopCorner = rightTop;
            LeftBottomCorner = leftBottom;
            RightBottomCorner = rightBottom;
        }

        #endregion
    }
}
