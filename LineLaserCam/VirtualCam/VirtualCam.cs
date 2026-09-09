using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Threading;

namespace AdaWeldSystem.LineLaserCam.VirtualCam
{
    /// <summary>
    /// 模拟焊缝轮廓类型
    /// </summary>
    public enum SimSeamProfile
    {
        /// <summary>V 型坡口</summary>
        VGroove = 0,
        /// <summary>U 型坡口</summary>
        UGroove = 1,
        /// <summary>平直焊缝</summary>
        Flat = 2,
        /// <summary>随机演化轮廓</summary>
        Random = 3
    }

    /// <summary>
    /// 虚拟调试相机：自生成轮廓点云数据（使用程序的 PointCloudLibrary.Models.PointCloudData），
    /// 通过 ImageAlgorithm.ConvertContourToMat 转换为 Mat，
    /// 完全复用真实相机的「取帧 → 算法分析」链路。
    /// 继承线激光抽象基类 LineLaserCameraBase（位于 ILineLaser，命名空间 AdaWeldSystem.LineLaserCam.ILineLaser），
    /// 可与英莱真实相机互换，由 LineLaserManager 统一注册与选中。
    /// </summary>
    public class VirtualCam : LineLaserCameraBase
    {
        #region 私有变量

        private readonly AutoResetEvent _acquisitionCompletedSignal = new AutoResetEvent(false);
        private readonly Random _rand = new Random();

        // 模拟出图节拍：真实相机由 SDK 回调连续推帧，虚拟相机用定时器模拟同一语义
        private const int SimFrameIntervalMs = 33;
        private const double SimFrameRateHz = 30.0;
        private System.Threading.Timer _simTimer;

        private bool _isRunning = false;
        private bool _lastSuccess = false;
        private int _frameIndex = 0;

        // 外设开关本地态：虚拟相机无硬件可查，由 SetSensor / SetLaser 维护
        private bool _sensorOn = false;
        private bool _laserOn = false;

        #endregion

        #region 公共变量

        /// <summary>模拟焊缝轮廓类型</summary>
        public SimSeamProfile SeamProfile { get; set; }

        /// <summary>是否启用随机模式（每帧随机演化焊缝宽度/中心偏移）</summary>
        public bool RandomMode { get; set; }

        /// <summary>模拟目标距离（mm）：工作流循环至机器人X达到此值时停止</summary>
        public double TargetDistance { get; set; }

        /// <summary>焊缝基准宽度（mm）</summary>
        public double BaseSeamWidth { get; set; }

        /// <summary>焊缝基准深度（mm）</summary>
        public double BaseSeamDepth { get; set; }

        /// <summary>相机种类，固定为虚拟</summary>
        public override LineLaserKind Kind { get { return LineLaserKind.Virtual; } }

        /// <summary>虚拟相机无硬件连接语义，恒为已连接</summary>
        public override bool IsConnected { get { return true; } }

        /// <summary>是否正在出图</summary>
        public override bool IsRunning { get { return _isRunning; } }

        /// <summary>最近一次采集是否成功</summary>
        public override bool LastAcquisitionSuccess { get { return _lastSuccess; } }

        /// <summary>采集扫描率（Hz）：虚拟相机同步产帧，可达 120Hz</summary>
        public override double ScanRateHz { get { return 120.0; } }

        /// <summary>采集完成信号</summary>
        public override AutoResetEvent AcquisitionCompletedSignal { get { return _acquisitionCompletedSignal; } }

        #endregion

        #region 构造函数

        /// <summary>创建虚拟调试相机，键名固定为「虚拟线激光」</summary>
        public VirtualCam()
            : base("虚拟线激光")
        {
            // ADR-002 兼容：不使用 auto-property initializer，值在此构造函数显式初始化
            SeamProfile = SimSeamProfile.VGroove;
            RandomMode = false;
            TargetDistance = 200.0;
            BaseSeamWidth = 8.0;
            BaseSeamDepth = 3.0;
        }

        #endregion

        #region 私有函数

        /// <summary>
        /// 生成一帧焊缝轮廓点云。
        /// 线激光原理：单帧 X=0，YZ 构成轮廓线，故令 Point3D.X=0，Y 沿焊缝横向、Z 为高度。
        /// 焊缝宽度随帧索引 / 随机模式演化，以驱动调整算法产生变化；
        /// 中心横向偏移（centerOffset）随帧变化，用于产生非零水平移动距离。
        /// </summary>
        private PointCloudData GenerateSeamContour()
        {
            PointCloudData cloud = new PointCloudData();

            double halfWidth = BaseSeamWidth / 2.0;
            double depth = BaseSeamDepth;

            if (RandomMode)
            {
                halfWidth = BaseSeamWidth / 2.0 * (0.6 + _rand.NextDouble() * 0.8);
                depth = BaseSeamDepth * (0.6 + _rand.NextDouble() * 0.8);
            }
            else
            {
                double phase = (_frameIndex % 200) / 200.0 * 2.0 * Math.PI;
                halfWidth = BaseSeamWidth / 2.0 * (1.0 + 0.3 * Math.Sin(phase));
            }

            // 中心横向偏移（mm）：使焊缝中心偏离标定中心，产生水平移动需求
            double centerOffset = RandomMode
                ? (_rand.NextDouble() - 0.5) * BaseSeamWidth
                : 0.3 * BaseSeamWidth * Math.Sin((_frameIndex % 100) / 100.0 * 2.0 * Math.PI);

            int sampleCount = 121; // 奇数，保证包含中心采样
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / (sampleCount - 1);      // 0..1
                double y = -halfWidth + 2.0 * halfWidth * t;    // 横向坐标
                double z;
                switch (SeamProfile)
                {
                    case SimSeamProfile.UGroove:
                        z = depth * (1.0 - Math.Cos((y / halfWidth) * Math.PI / 2.0));
                        break;
                    case SimSeamProfile.Flat:
                        // 平直焊缝并非绝对水平：引入微小横向坡度 + 轻微起伏，
                        // 保证 Z 方向存在有效范围（zRange>0），避免 ConvertContourToMat 判定"YZ范围过小"而抛异常。
                        z = depth * 0.5
                            + depth * 0.05 * (t - 0.5)
                            + depth * 0.02 * Math.Sin(y / halfWidth * Math.PI);
                        break;
                    case SimSeamProfile.Random:
                        z = depth * (0.5 + 0.5 * _rand.NextDouble());
                        break;
                    case SimSeamProfile.VGroove:
                    default:
                        z = depth * (Math.Abs(y) / halfWidth);
                        break;
                }
                double yOffset = y + centerOffset;
                cloud.AddPoint(0.0f, (float)yOffset, (float)z);
            }

            return cloud;
        }

        /// <summary>
        /// 由轮廓推算一个简易识别结果：最低点为焊缝特征点，横向跨度为焊缝宽度。
        /// 虚拟相机不走英莱算法，仅用于在模拟模式下打通「结果 → Y 轴 PVT」链路。
        /// </summary>
        /// <param name="cloud">本帧轮廓点云</param>
        /// <returns>焊缝识别结果</returns>
        private LineLaserSeamResult BuildResult(PointCloudData cloud)
        {
            var pts = cloud.Points;
            if (pts == null || pts.Count == 0) return LineLaserSeamResult.Invalid(0, -1);

            float minZ = float.MaxValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            int featureIndex = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].Z < minZ) { minZ = pts[i].Z; featureIndex = i; }
                if (pts[i].Y < minY) minY = pts[i].Y;
                if (pts[i].Y > maxY) maxY = pts[i].Y;
            }

            float width = maxY - minY;
            float depth = Math.Abs(minZ);
            return new LineLaserSeamResult(true, 1, 0, (ulong)_frameIndex,
                pts[featureIndex].Y, minZ, width, width, depth, depth, width * depth * 0.5f);
        }

        #endregion

        #region 公共函数

        /// <summary>启动连续采集：起模拟节拍，按固定间隔持续产帧</summary>
        public override void SensorRunContinue()
        {
            _isRunning = true;
            if (_simTimer == null)
            {
                _simTimer = new System.Threading.Timer(OnSimTick, null, 0, SimFrameIntervalMs);
            }
            RaiseStateChanged();
        }

        /// <summary>停止连续采集：停模拟节拍</summary>
        public override void SensorStop()
        {
            _isRunning = false;
            StopSimTimer();
            RaiseStateChanged();
        }

        /// <summary>释放图像资源与模拟节拍</summary>
        public override void Dispose()
        {
            _isRunning = false;
            StopSimTimer();
            UpdateDisplayMat(null);
        }

        /// <summary>模拟节拍回调：未运行时直接返回</summary>
        /// <param name="state">定时器状态，未使用</param>
        private void OnSimTick(object state)
        {
            if (!_isRunning) return;
            GenerateFrame();
        }

        /// <summary>生成一帧轮廓并触发结果与轮廓两个回调</summary>
        private void GenerateFrame()
        {
            try
            {
                PointCloudData cloud = GenerateSeamContour();
                _lastSuccess = (cloud != null && cloud.PointCount > 0);
                _frameIndex++;

                if (_lastSuccess)
                {
                    RaiseResultReady(BuildResult(cloud), SimFrameRateHz);
                    RaiseProfileReady(cloud, SimFrameRateHz);
                }
            }
            catch (Exception ex)
            {
                _lastSuccess = false;
                Log("生成轮廓失败 " + ex.Message, MessageLevel.Error);
            }
            _acquisitionCompletedSignal.Set();
        }

        /// <summary>释放模拟节拍定时器</summary>
        private void StopSimTimer()
        {
            if (_simTimer != null)
            {
                _simTimer.Dispose();
                _simTimer = null;
            }
        }

        /// <summary>虚拟相机无硬件连接语义，恒返回 true</summary>
        /// <param name="ip">相机 IP，虚拟相机忽略</param>
        /// <returns>恒为 true</returns>
        public override bool ConnectManual(string ip)
        {
            return true;
        }

        /// <summary>传感器（出图）是否开启</summary>
        /// <returns>开启返回 true</returns>
        public override bool IsCameraOn()
        {
            return _sensorOn;
        }

        /// <summary>激光器是否开启</summary>
        /// <returns>开启返回 true</returns>
        public override bool IsLaserOn()
        {
            return _laserOn;
        }

        /// <summary>开关传感器（出图）</summary>
        /// <param name="enable">true 为开启</param>
        public override void SetSensor(bool enable)
        {
            _sensorOn = enable;
        }

        /// <summary>开关激光器</summary>
        /// <param name="enable">true 为开启</param>
        public override void SetLaser(bool enable)
        {
            _laserOn = enable;
        }

        #endregion
    }
}
