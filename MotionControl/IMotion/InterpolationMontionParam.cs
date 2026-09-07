namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>插补运动参数</summary>
    public class InterpolationMontionParam
    {
        /// <summary>创建插补参数</summary>
        /// <param name="montionType">插补类型</param>
        /// <param name="axisCount">参与插补的轴数</param>
        public InterpolationMontionParam(InterpolationType montionType, ushort axisCount)
        {
            MontionType = montionType;
            _axisCount = axisCount;
            lstAxis = new ushort[axisCount];
            lstTargetPos = new double[axisCount];
            lstCenterPos = new double[axisCount];
        }

        /// <summary>插补类型</summary>
        public InterpolationType MontionType { get; set; }

        /// <summary>参与插补的轴号列表</summary>
        public ushort[] lstAxis;

        /// <summary>目标位置列表（工程单位）</summary>
        public double[] lstTargetPos;

        /// <summary>圆弧圆心坐标列表（工程单位）</summary>
        public double[] lstCenterPos;

        /// <summary>位置模式，0 为相对，1 为绝对</summary>
        public ushort _posMode;

        /// <summary>圆弧方向，0 为顺时针，1 为逆时针</summary>
        public ushort _arcDir;

        /// <summary>圆弧圈数</summary>
        public ushort _arcCircle;

        /// <summary>起始速度（工程单位/秒）</summary>
        public double _minVel;

        /// <summary>运行速度（工程单位/秒）</summary>
        public double _maxVel;

        /// <summary>加速时间（秒）</summary>
        public double _accTime;

        /// <summary>减速时间（秒）</summary>
        public double _decTime;

        /// <summary>S 曲线时间（秒）</summary>
        public double _sTime;

        /// <summary>停止速度（工程单位/秒）</summary>
        public double _stopVel;

        private readonly ushort _axisCount;

        /// <summary>参与插补的轴数</summary>
        public ushort AxisCount { get { return _axisCount; } }
    }
}
