using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.PCLOperate.Models;
using System;
using System.Threading;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.LineLaserCam.VirtualCam
{
    /// <summary>虚拟调试相机，自生成平直搭接轮廓点云。 推算中</summary>
    public class VirtualCam : LineLaserCameraBase
    {
        #region 私有变量

        private readonly AutoResetEvent _acquisitionCompletedSignal = new AutoResetEvent(false);

        // 模拟节拍，定时器代替 SDK 推帧
        private System.Threading.Timer _simTimer;

        private bool _isRunning = false;
        private bool _lastSuccess = false;
        private int _frameIndex = 0;

        // 外设开关本地态：虚拟相机无硬件可查，由 SetSensor / SetLaser 维护
        private bool _sensorOn = false;
        private bool _laserOn = false;

        #endregion

        #region 公共变量

        /// <summary>焊缝基准宽度（mm），平直搭接轮廓全跨</summary>
        public const double BaseSeamWidth = 8.0;

        /// <summary>焊缝基准深度（mm），中心搭接缝槽深度</summary>
        public const double BaseSeamDepth = 3.0;

        /// <summary>线激光数据频率（Hz）：30/60/80/120，决定模拟出图节拍与 ScanRateHz</summary>
        public int FrameRateHz { get; set; }

        /// <summary>相机种类，固定为虚拟</summary>
        public override LineLaserKind Kind { get { return LineLaserKind.Virtual; } }

        /// <summary>虚拟相机无硬件连接语义，恒为已连接</summary>
        public override bool IsConnected { get { return true; } }

        /// <summary>是否正在出图</summary>
        public override bool IsRunning { get { return _isRunning; } }

        /// <summary>最近一次采集是否成功</summary>
        public override bool LastAcquisitionSuccess { get { return _lastSuccess; } }

        /// <summary>采集扫描率（Hz）：跟随 FrameRateHz</summary>
        public override double ScanRateHz { get { return (double)FrameRateHz; } }

        /// <summary>采集完成信号</summary>
        public override AutoResetEvent AcquisitionCompletedSignal { get { return _acquisitionCompletedSignal; } }

        /// <summary>无厂商 SDK，无返回码可描述</summary>
        /// <param name="context">失败语境</param>
        /// <returns>固定的未提供说明</returns>
        public override string DescribeLastError(LineLaserErrorContext context)
        {
            return "未提供返回码（虚拟相机不走厂商 SDK，无返回码可描述）";
        }

        #endregion

        #region 构造函数

        /// <summary>创建虚拟调试相机，键名固定为「虚拟线激光」</summary>
        public VirtualCam()
            : base("虚拟线激光")
        {
            // 不在属性上初始化，值在此显式赋值
            FrameRateHz = 30;
        }

        #endregion

        #region 私有函数

        /// <summary>生成一帧平直搭接轮廓点云，X 恒为 0</summary>
        private PointCloudData GenerateSeamContour()
        {
            PointCloudData cloud = new PointCloudData();

            double halfWidth = BaseSeamWidth / 2.0;
            double depth = BaseSeamDepth;
            double seamHalf = halfWidth * 0.25;   // 中心搭接缝槽半宽

            int sampleCount = 1000; // 轮廓基准点数
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / (sampleCount - 1);      // 0..1
                double y = -halfWidth + 2.0 * halfWidth * t;    // 横向坐标
                double ay = Math.Abs(y);
                double z;
                if (ay < seamHalf)
                    z = -depth * (1.0 - ay / seamHalf);        // 中心搭接缝槽（最低点）
                else
                    z = 0.0;                                    // 平直搭接面
                // 极轻微起伏，保证 YZ 范围有效、避免绝对水平
                z += depth * 0.02 * Math.Sin(y / halfWidth * Math.PI);
                cloud.AddPoint(0.0f, (float)y, (float)z);
            }

            return cloud;
        }

        /// <summary>由轮廓推算简易结果：最近 y=0 点为中心</summary>

        /// <param name="cloud">本帧轮廓点云</param>
        /// <returns>焊缝识别结果</returns>
        private LineLaserSeamResult BuildResult(PointCloudData cloud)
        {
            var pts = cloud.Points;
            if (pts == null || pts.Count == 0) return LineLaserSeamResult.Invalid(0, -1);

            int centerIndex = 0;
            double minAbsY = double.MaxValue;
            float minZ = float.MaxValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                if (Math.Abs(pts[i].Y) < minAbsY) { minAbsY = Math.Abs(pts[i].Y); centerIndex = i; }
                if (pts[i].Z < minZ) minZ = pts[i].Z;
                if (pts[i].Y < minY) minY = pts[i].Y;
                if (pts[i].Y > maxY) maxY = pts[i].Y;
            }

            float width = maxY - minY;
            float depth = Math.Abs(minZ);
            float centerY = pts[centerIndex].Y;
            float centerZ = pts[centerIndex].Z;
            return new LineLaserSeamResult(true, 1, 0, (ulong)_frameIndex,
                centerY, centerZ, width, width, depth, depth, width * depth * 0.5f);
        }

        #endregion

        #region 公共函数

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
                _lastSuccess = cloud.PointCount > 0;
                _frameIndex++;

                if (_lastSuccess)
                {
                    RaiseResultReady(BuildResult(cloud), ScanRateHz);
                    RaiseProfileReady(cloud, ScanRateHz);
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
            _simTimer?.Dispose();
            _simTimer = null;
        }

        /// <summary>启动连续采集：起模拟节拍，按固定间隔持续产帧</summary>
        public override void SensorRunContinue()
        {
            _isRunning = true;
            if (_simTimer == null)
            {
                int intervalMs = FrameRateHz > 0 ? (int)(1000.0 / FrameRateHz) : 33;
                _simTimer = new System.Threading.Timer(OnSimTick, null, 0, intervalMs);
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
