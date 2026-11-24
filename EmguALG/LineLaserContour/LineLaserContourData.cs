namespace AdaWeldSystem.EmguALG.LineLaserContour
{
    /// <summary>一帧线激光轮廓点集（算法层 DTO，单位毫米）。</summary>
    public sealed class LineLaserProfileData
    {
        #region 常量

        /// <summary>单帧最大点数（与轮廓回调取点上限一致）</summary>
        public const int MaxPoints = 2048;

        #endregion

        #region 私有变量

        private readonly double[] _ys;
        private readonly double[] _zs;

        #endregion

        #region 公共变量

        /// <summary>轮廓点横向坐标（毫米），有效长度见 <see cref="Count"/></summary>
        public double[] Ys
        {
            get { return _ys; }
        }

        /// <summary>轮廓点高度坐标（毫米），有效长度见 <see cref="Count"/></summary>
        public double[] Zs
        {
            get { return _zs; }
        }

        /// <summary>本帧有效点数</summary>
        public int Count { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建自带缓冲的轮廓点集（容量为 <see cref="MaxPoints"/>）</summary>
        public LineLaserProfileData()
            : this(new double[MaxPoints], new double[MaxPoints])
        {
        }

        /// <summary>在调用方缓冲上建视图，不复制数据</summary>
        /// <param name="ys">横向坐标缓冲</param>
        /// <param name="zs">高度坐标缓冲</param>
        public LineLaserProfileData(double[] ys, double[] zs)
        {
            _ys = ys ?? new double[MaxPoints];
            _zs = zs ?? new double[MaxPoints];
            Count = 0;
        }

        #endregion

        #region 公共函数

        /// <summary>设置本帧有效点数（超过容量或为负时自动截断）</summary>
        /// <param name="count">有效点数</param>
        public void SetCount(int count)
        {
            int capacity = (_ys.Length < _zs.Length) ? _ys.Length : _zs.Length;
            if (count > capacity) count = capacity;
            if (count < 0) count = 0;
            Count = count;
        }

        /// <summary>清空为无轮廓帧</summary>
        public void Clear()
        {
            Count = 0;
        }

        #endregion
    }

    /// <summary>线激光标定四角与中心（算法层 DTO，单位毫米）。</summary>
    public sealed class LineLaserCalibrationData
    {
        #region 公共变量

        /// <summary>是否存在有效标定数据</summary>
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

        /// <summary>创建无标定数据的实例</summary>
        public LineLaserCalibrationData()
        {
            Center = new float[2];
            LeftTopCorner = new float[2];
            RightTopCorner = new float[2];
            LeftBottomCorner = new float[2];
            RightBottomCorner = new float[2];
        }

        /// <summary>用角点创建标定数据（内部拷贝）</summary>
        /// <param name="center">视野中心点</param>
        /// <param name="leftTop">左上角点</param>
        /// <param name="rightTop">右上角点</param>
        /// <param name="leftBottom">左下角点</param>
        /// <param name="rightBottom">右下角点</param>
        public LineLaserCalibrationData(float[] center, float[] leftTop, float[] rightTop,
            float[] leftBottom, float[] rightBottom)
        {
            HasData = true;
            Center = Copy2(center);
            LeftTopCorner = Copy2(leftTop);
            RightTopCorner = Copy2(rightTop);
            LeftBottomCorner = Copy2(leftBottom);
            RightBottomCorner = Copy2(rightBottom);
        }

        #endregion

        #region 私有函数

        /// <summary>拷贝二维点（源为空或长度不足时补 0）</summary>
        /// <param name="source">源数组</param>
        /// <returns>长度固定为 2 的新数组</returns>
        private static float[] Copy2(float[] source)
        {
            float[] target = new float[2];
            if (source == null) return target;
            if (source.Length > 0) target[0] = source[0];
            if (source.Length > 1) target[1] = source[1];
            return target;
        }

        #endregion
    }

    /// <summary>焊缝特征点（单位毫米，含有效标志与 Y/Z）</summary>
    public struct LineLaserSeamFeature
    {
        #region 公共变量

        /// <summary>本帧是否存在有效识别结果</summary>
        public bool Valid { get; set; }

        /// <summary>焊缝特征点横向坐标（毫米）</summary>
        public float FeatureY { get; set; }

        /// <summary>焊缝特征点高度坐标（毫米）</summary>
        public float FeatureZ { get; set; }

        #endregion

        #region 构造函数

        /// <summary>创建焊缝特征点</summary>
        /// <param name="valid">是否存在有效识别结果</param>
        /// <param name="featureY">特征点横向坐标</param>
        /// <param name="featureZ">特征点高度坐标</param>
        public LineLaserSeamFeature(bool valid, float featureY, float featureZ)
        {
            Valid = valid;
            FeatureY = featureY;
            FeatureZ = featureZ;
        }

        #endregion

        #region 公共函数

        /// <summary>创建无效特征点（无识别的帧使用）</summary>
        /// <returns>无效特征点</returns>
        public static LineLaserSeamFeature Invalid()
        {
            return new LineLaserSeamFeature(false, 0f, 0f);
        }

        #endregion
    }
}
