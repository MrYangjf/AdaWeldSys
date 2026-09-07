using AdaWeldSystem.EmguALG;
using AdaWeldSystem.LineLaserCamApi;
using AdaWeldSystem.Comm;
using Emgu.CV;
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
    /// 虚拟调试相机：自生成轮廓点云数据（使用程序的 PointCloudLibrary.Models.PointCloudData，
    /// 不使用 SmartRay Api.Point3d），通过 ImageAlgorithm.ConvertContourToMat 转换为 Mat，
    /// 完全复用真实相机的「取帧 → 算法分析」链路。
    /// 实现 ICameraRun（位于 LineLaserCam/Api，命名空间 AdaWeldSystem.LineLaserCamApi），
    /// 可无缝替换 CameraRun.Instance 注入到各工作流。
    /// 本类归属与 SmartRayCam 平级的 VirtualCam 模块。
    /// </summary>
    public class VirtualCameraRun : ICameraRun
    {
        private const string Tag = "VirtualCameraRun";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private readonly AutoResetEvent _acquisitionCompletedSignal = new AutoResetEvent(false);
        private bool _isRunning = false;
        private Mat _currentMat;
        private bool _lastSuccess = false;

        private int _frameIndex = 0;
        private readonly Random _rand = new Random();

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

        /// <summary>采集扫描率（Hz）：虚拟相机同步产帧，可达 120Hz</summary>
        public double ScanRateHz { get { return 120.0; } }

        public VirtualCameraRun()
        {
            // ADR-002 兼容：不使用 auto-property initializer，值在此构造函数显式初始化
            SeamProfile = SimSeamProfile.VGroove;
            RandomMode = false;
            TargetDistance = 200.0;
            BaseSeamWidth = 8.0;
            BaseSeamDepth = 3.0;
        }

        public AutoResetEvent AcquisitionCompletedSignal { get { return _acquisitionCompletedSignal; } }

        public bool IsRunning { get { return _isRunning; } }

        public bool LastAcquisitionSuccess { get { return _lastSuccess; } }

        public Mat CurrentMat { get { return _currentMat; } }

        public void SensorRunContinue()
        {
            _isRunning = true;
            try
            {
                PointCloudData cloud = GenerateSeamContour();
                if (_currentMat != null)
                {
                    _currentMat.Dispose();
                    _currentMat = null;
                }
                _currentMat = ImageAlgorithm.ConvertContourToMat(cloud);
                _lastSuccess = (_currentMat != null && !_currentMat.IsEmpty);
                _frameIndex++;
            }
            catch (Exception ex)
            {
                _lastSuccess = false;
                _currentMat = null;
                Log("VirtualCameraRun 生成轮廓失败 " + ex.Message, MessageLevel.Error);
            }
            _acquisitionCompletedSignal.Set();
        }

        public void SensorStop()
        {
            _isRunning = false;
        }

        public void Dispose()
        {
            _isRunning = false;
            if (_currentMat != null)
            {
                _currentMat.Dispose();
                _currentMat = null;
            }
        }

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
    }
}
