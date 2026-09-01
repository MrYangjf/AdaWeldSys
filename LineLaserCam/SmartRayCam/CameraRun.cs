using AdaWeldSystem.EmguALG;
using AdaWeldSystem.ProductFileManager;
using AdaWeldSystem.Comm;
using Emgu.CV;
using Emgu.CV.UI;
using Smartray;
using AdaWeldSystem.LineLaserCam.SmartRayCam.ApiCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AdaWeldSystem.LineLaserCamApi;

namespace AdaWeldSystem.LineLaserCam.SmartRayCam
{
    public class CameraRun : ICameraRun
    {
        private const string Tag = "CameraRun";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        public delegate void delegateMatCompleted(Object Sender, EventArgs e);
        public static event delegateMatCompleted AcqusitionMatCompletedEvent;
        public bool IsLiveMode = false;

        /// <summary>
        /// 采集完成信号（用于 Workflow 等待回调模式）
        /// Workflow 调用 SensorRunContinue() 后，通过此 AutoResetEvent 等待相机回调
        /// </summary>
        private readonly System.Threading.AutoResetEvent _acquisitionCompletedSignal = new System.Threading.AutoResetEvent(false);

        /// <summary>
        /// 采集完成信号（ICameraRun 接口要求，包装内部字段）
        /// </summary>
        public System.Threading.AutoResetEvent AcquisitionCompletedSignal
        {
            get { return _acquisitionCompletedSignal; }
        }

        /// <summary>
        /// 相机是否正在运行（ICameraRun 接口要求）
        /// </summary>
        public bool IsRunning
        {
            get { return isRun; }
        }

        /// <summary>
        /// 最近一次 PIL 模式采集是否成功
        /// </summary>
        private bool _lastAcquisitionSuccess = false;
        public bool LastAcquisitionSuccess { get { return _lastAcquisitionSuccess; } private set { _lastAcquisitionSuccess = value; } }

        /// <summary>
        /// 当前有效采集扫描率（Hz）：Internal 触发模式取内部触发频率，否则取最大扫描率；供工作流缩放取图超时
        /// </summary>
        public double ScanRateHz { get { return _currentScanRateHz; } }

        private readonly SensorHelper sensorHelper = new SensorHelper();
        private uint numberofProfile = 10;
        private static CameraRun instance = null;
        private static bool IniCameraSucceed = false;
        private int OrginX, Width = 1920, OriginY, Height = 1200;
        private ImageRoiTools roiOverlay;
        private bool isDrawRoi = false;
        private string liveParamFile = "C:\\Users\\yangjingfeng\\Desktop\\ECCO95_Liveimage.par";
        private string pilParamFile = "C:\\Users\\yangjingfeng\\Desktop\\111.par";
        private int reta1 = 0;

        /// <summary>当前有效采集扫描率（Hz），由扫描率诊断更新，供工作流按帧周期缩放 WaitOne 超时（默认 10，与 SDK 默认内部触发频率对齐）</summary>
        private double _currentScanRateHz = 10.0;

        public static CameraRun Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CameraRun();
                }
                return instance;
            }
        }

        SensorManager _sensorManager = new SensorManager();

        Sensor _sensor;
        Mat _currentMat;
        public Mat CurrentMat { get { return _currentMat; } }
        public bool isRun = false;

        /// <summary>
        /// 相机硬件是否已成功连接（OpenSensor 成功后置 true）。
        /// 与 isRun（单次采集进行中）区分：isRun 在 Snapshot 每帧交付后复位，
        /// 而 _isConnected 在连接成功后保持，用于表达「相机连接状态」。
        /// </summary>
        private bool _isConnected = false;
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        public void ChangeMode(bool EnableLive)
        {
            IsLiveMode = EnableLive;
            ChangeConfig(IsLiveMode);
        }

        public void OpenSensor(string ip, string port)
        {
            if (IniCameraSucceed) return;

            int numofsensor = SensorManager._sensors.Count;
            string sensorname = "Sensor" + numofsensor.ToString();
            string sensorIp = ip;
            ushort portnum = Convert.ToUInt16(port);
            numberofProfile = 10;


            // OpenSensor 由工作流后台线程（LineLaserWorkflow.InitializeWorker）调用，
            // 任何失败均以错误日志记录并返回（IniCameraSucceed 保持 false），
            // 严禁弹出模态 MessageBox —— 否则会阻塞后台线程，无人监控时程序卡死。
            _sensor = _sensorManager.CreateSensor(sensorname, numofsensor, sensorIp, portnum);
            reta1 = _sensor.Connect();
            if (reta1 != 0) { Log("相机连接失败 Connect Failed 错误码 " + reta1, MessageLevel.Error); return; }
            reta1 = _sensor.LoadCalibrationDataFromSensor();
            if (reta1 != 0) { Log("加载标定数据失败 Load Calibration Data Failed 错误码 " + reta1, MessageLevel.Error); return; }

            reta1 = Api.LoadParameterSetFromFile(_sensor._sensorObject, pilParamFile);
            if (reta1 != 0) { Log("加载参数集失败 Load Parameter Set Failed 错误码 " + reta1, MessageLevel.Error); return; }
            reta1 = Api.SetImageAcquisitionType(_sensor._sensorObject, Api.ImageAcquisitionType.ProfileIntensityLaserLineThickness);
            if (reta1 != 0) { Log("设置图像采集类型失败 SetImageAcquisitionType Failed 错误码 " + reta1, MessageLevel.Error); return; }
            // 工作流（PIL）以单次快照（Snapshot）模式运行：每次 SensorRunContinue 采集一帧，
            // 由工作流在 AcquireImage 每周期主动触发，避免连续产帧与工作流分析/资源销毁相互竞争。
            // （连续模式 RepeatSnapshot 仅用于 Live 实时预览，见 ChangeConfig）
            reta1 = Api.SetAcquisitionMode(_sensor._sensorObject, Api.AcquisitionMode.Snapshot);
            if (reta1 != 0) { Log("设置采集模式失败 SetAcquisitionMode Failed 错误码 " + reta1, MessageLevel.Error); return; }
            reta1 = Api.SetNumberOfProfilesToCapture(_sensor._sensorObject, numberofProfile);
            if (reta1 != 0) { Log("设置采集帧数失败 SetNumberOfProfilesToCapture Failed 错误码 " + reta1, MessageLevel.Error); return; }

            _sensor.SendParameterSet();

            _sensor.AcqusitionCompletedEvent += new Sensor.delegateAcqusitionCompleted(OnCompletedAcqEvent);

            // 初始化ROI配置
            InitializeRoi();

            if (reta1 == 0) IniCameraSucceed = true;
            _isConnected = IniCameraSucceed;

            // 诊断：打印当前 ROI+曝光下传感器最大扫描率（硬件天花板），确认能否达到目标分析频率（如100Hz）
            LogScanRateDiagnostics();
        }

        /// <summary>
        /// 读取并打印传感器在当前 ROI/曝光下的扫描率能力（诊断可达分析频率上限，如 100Hz）。
        /// 仅在相机已成功初始化后调用；信息经 GlobalCommData.ShowLog 输出到 app 日志（可见）。
        /// </summary>
        private void LogScanRateDiagnostics()
        {
            if (!IniCameraSucceed || _sensor == null) return;
            // 缓存当前有效扫描率，供工作流按帧周期缩放 WaitOne 超时（支持 120Hz 紧超时）
            _currentScanRateHz = _sensor.GetEffectiveScanRateHz();
            Log(_sensor.GetScanRateDiagnostics(), MessageLevel.Info);
        }

        /// <summary>
        /// 初始化ROI - 相机打开后调用，应用已保存的ROI配置
        /// </summary>
        public void InitializeRoi()
        {
            try
            {
                // 获取传感器最大尺寸
                int ret = Api.GetSensorMaxDimensions(SensorManager._sensors[0]._sensorObject, out int maxWidth, out int maxHeight);
                if (ret != 0)
                {
                    Log("GetSensorMaxDimensions 失败，使用默认值", MessageLevel.Info);
                    maxWidth = 1920;
                    maxHeight = 1200;
                }

                // 尝试从INI读取已保存的ROI
                string iniPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "ImageRoi.ini");
                if (System.IO.File.Exists(iniPath))
                {
                    var ini = new AdaWeldSystem.FileOperate.INIFile(iniPath);
                    if (ini.KeyExists("ImageROI", "Enabled") && ini.ReadBool("ImageROI", "Enabled", false))
                    {
                        int x = ini.ReadInt("ImageROI", "X", 0);
                        int y = ini.ReadInt("ImageROI", "Y", 0);
                        int w = ini.ReadInt("ImageROI", "Width", maxWidth);
                        int h = ini.ReadInt("ImageROI", "Height", maxHeight);

                        // 确保ROI在有效范围内
                        x = Math.Max(0, Math.Min(x, maxWidth - 1));
                        y = Math.Max(0, Math.Min(y, maxHeight - 1));
                        w = Math.Max(1, Math.Min(w, maxWidth - x));
                        h = Math.Max(1, Math.Min(h, maxHeight - y));

                        // 应用ROI到传感器
                        ret = Api.SetROI(SensorManager._sensors[0]._sensorObject, x, w, y, h);
                        if (ret == 0)
                        {
                            OrginX = x;
                            Width = w;
                            OriginY = y;
                            Height = h;
                            Log("ROI 从 INI 初始化 起点 " + x + " " + y + " 宽 " + w + " 高 " + h, MessageLevel.Info);
                        }
                        else
                        {
                            Log("SetROI 初始化失败，使用全尺寸", MessageLevel.Info);
                            Api.SetROI(SensorManager._sensors[0]._sensorObject, 0, maxWidth, 0, maxHeight);
                            OrginX = 0;
                            Width = maxWidth;
                            OriginY = 0;
                            Height = maxHeight;
                        }
                        return;
                    }
                }

                // 没有保存的ROI，使用全尺寸
                Api.SetROI(SensorManager._sensors[0]._sensorObject, 0, maxWidth, 0, maxHeight);
                OrginX = 0;
                Width = maxWidth;
                OriginY = 0;
                Height = maxHeight;
                Log("ROI 初始化为全尺寸 宽 " + maxWidth + " 高 " + maxHeight, MessageLevel.Info);
            }
            catch (Exception ex)
            {
                Log("InitializeRoi 失败 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>
        /// 设置ROI绘制模式 - 在Live模式下启用ROI绘制
        /// </summary>
        public void SetRoi(ImageBox imageBox)
        {
            if (!IsLiveMode)
            {
                MessageBox.Show("请先切换到实时模式再绘制ROI");
                return;
            }

            isDrawRoi = true;
            SensorStop();

            // 重置为全尺寸以便用户绘制ROI
            Api.GetSensorMaxDimensions(SensorManager._sensors[0]._sensorObject, out int maxWidth, out int maxHeight);
            Api.SetROI(SensorManager._sensors[0]._sensorObject, 0, maxWidth, 0, maxHeight);

            SensorRunContinue();

            // 创建ROI工具（PictureBox基类兼容ImageBox）
            roiOverlay = new ImageRoiTools(imageBox);
            roiOverlay.RoiColor = Color.Lime;
            roiOverlay.RoiChanged += (s, e) =>
            {
                RectangleF rectangle = roiOverlay.GetRoi();
                if (rectangle.Width > 0 && rectangle.Height > 0)
                {
                    // 应用新的ROI到传感器
                    int x = (int)rectangle.X;
                    int y = (int)rectangle.Y;
                    int w = (int)rectangle.Width;
                    int h = (int)rectangle.Height;

                    // 确保ROI有效
                    Api.GetSensorMaxDimensions(SensorManager._sensors[0]._sensorObject, out int sw, out int sh);
                    x = Math.Max(0, Math.Min(x, sw - 1));
                    y = Math.Max(0, Math.Min(y, sh - 1));
                    w = Math.Max(1, Math.Min(w, sw - x));
                    h = Math.Max(1, Math.Min(h, sh - y));

                    SensorStop();
                    int ret = Api.SetROI(SensorManager._sensors[0]._sensorObject, x, w, y, h);
                    if (ret == 0)
                    {
                        OrginX = x;
                        Width = w;
                        OriginY = y;
                        Height = h;
                    }
                    // 诊断：ROI 变更后最大扫描率随之变化，重新打印天花板
                    LogScanRateDiagnostics();
                    SensorRunContinue();
                }
            };
        }

        private Task AcqusitionFromSensor(Sensor sensor)
        {
            isRun = true;
            Task mtask = new Task(() =>
            {
                try
                {
                    //Api.SetNumberOfProfilesToCapture(sensor._sensorObject, numberofProfile);
                    Api.SetPacketSize(sensor._sensorObject, 0);
                    Api.SetPacketTimeOut(sensor._sensorObject, 0);
                    sensor.ClearImageData();
                    sensor.StartAcquisition();
                    sensor.WaitForImage(1);
                }
                catch (Exception ce)
                {
                    Log("SensorRunContinue 异常 " + ce.Message, MessageLevel.Error);
                }
            });
            mtask.Start();
            return mtask;
        }

        void OnCompletedAcqEvent(object sender, EventArgs e)
        {

            if (!IsLiveMode)
            {
                //PIL模式下获取数据并处理
                try
                {
                    ushort[] profiledata = SensorManager._sensors[0].GetLastImageData().ProfileImage.GetImage();
                    var PILimageWidth = profiledata.GetLength(0) / numberofProfile;
                    var pointcloud = new Api.Point3d[(int)PILimageWidth];
                    Api.GetROI(SensorManager._sensors[0]._sensorObject, out int originX, out int width, out int originY, out int height);
                    Api.CreatePointCloudSingleProfile(SensorManager._sensors[0]._sensorObject, profiledata, originX, (int)PILimageWidth, pointcloud);
                    _currentMat = ImageAlgorithm.DrawPointsDefaultRange(GetPointList(pointcloud));
                    RunRecordManager.Instance.AddSmartRayFrame((uint)PILimageWidth, pointcloud, null, 0);
                    LastAcquisitionSuccess = true;
                }
                catch (Exception ex)
                {
                    LastAcquisitionSuccess = false;
                    Log("OnCompletedAcqEvent PIL处理异常 " + ex.Message, MessageLevel.Error);
                }
                //Mat processedMat = PipelineManager.Instance.Execute(_currentMat);
                //CurrentImage = _currentMat.ToImage<Gray, byte>();
                if (isDrawRoi) { isDrawRoi = false; SensorStop(); }
            }
            else
            {
                //Live模式下获取数据并处理（不通知 Workflow）
                var imagedata = SensorManager._sensors[0].GetLastImageData().LiveImage.GetImage();
                int orgx, width, orgy, heigt;
                Api.GetROI(SensorManager._sensors[0]._sensorObject, out orgx, out width, out orgy, out heigt);
                Api.GetMeasurementRange(SensorManager._sensors[0]._sensorObject, out int minZ, out int maxZ);
                //是否需要根据测量范围对图像进行处理

                _currentMat = new Mat(heigt, width, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
                sensorHelper.byte2MatImage(imagedata, _currentMat);
                //CurrentImage = _currentMat.ToImage<Gray, byte>();
            }
            SensorManager._sensors[0].ClearImageData();

            // 仅 PIL 模式通知 Workflow 采集完成（Live 模式不参与 Workflow 流程）
            if (!IsLiveMode)
            {
                _acquisitionCompletedSignal.Set();
                // Snapshot（单次）模式下，单帧已交付即复位 isRun，
                // 使下一轮 AcquireImage 可立即重新触发 SensorRunContinue 采集下一帧；
                // RepeatSnapshot（Live）模式保持 isRun=true 以持续产帧，不在此复位。
                isRun = false;
            }

            AcqusitionMatCompletedEvent(sender, e);
        }

        private List<PointF> GetPointList(Api.Point3d[] pointcloud)
        {
            List<PointF> points = new List<PointF>();
            for (int i = 0; i < pointcloud.Length; i++)
            {
                points.Add(new PointF((float)pointcloud[i].Y, (float)pointcloud[i].Z));
            }
            return points;
        }

        public async void SensorRunContinue()
        {
            if (!IniCameraSucceed) return;
            if (!isRun)
            {
                isRun = true;
                //for (int i = 0; i < SensorManager._sensors.Count; i++)
                //{
                //    await AcqusitionFromSensor(SensorManager._sensors[i]);
                //}
                await AcqusitionFromSensor(_sensor);
            }
        }

        public void SensorStop()
        {
            if (!IniCameraSucceed) return;
            if (isRun)
            {
                isRun = false;
                //for (int i = 0; i < SensorManager._sensors.Count; i++)
                //{
                //    SensorManager._sensors[i].StopAcquisition();
                //}
                _sensor.StopAcquisition();
            }

        }

        public void Dispose()
        {
            try
            {
                _sensorManager.Dispose();
            }
            catch
            {

            }
        }

        bool ChangeConfig(bool isLiveMode)
        {
            if (isLiveMode)
            {
                numberofProfile = 1200;
                reta1 = Api.LoadParameterSetFromFile(_sensor._sensorObject, liveParamFile);
                if (reta1 != 0) return false;
                reta1 = Api.SetImageAcquisitionType(_sensor._sensorObject, Api.ImageAcquisitionType.LiveImage);
                if (reta1 != 0) return false;
                // Live 实时预览使用连续快照模式，持续产帧
                reta1 = Api.SetAcquisitionMode(_sensor._sensorObject, Api.AcquisitionMode.RepeatSnapshot);
                if (reta1 != 0) return false;
                reta1 = Api.SetNumberOfProfilesToCapture(_sensor._sensorObject, numberofProfile);
                if (reta1 != 0) return false;
                _sensor.SendParameterSet();
            }
            else
            {
                numberofProfile = 10;
                reta1 = Api.LoadParameterSetFromFile(_sensor._sensorObject, pilParamFile);
                if (reta1 != 0) return false;
                reta1 = Api.SetImageAcquisitionType(_sensor._sensorObject, Api.ImageAcquisitionType.ProfileIntensityLaserLineThickness);
                if (reta1 != 0) return false;
                // PIL/工作流使用单次快照模式，由工作流每周期触发一帧（runonce 语义）
                reta1 = Api.SetAcquisitionMode(_sensor._sensorObject, Api.AcquisitionMode.Snapshot);
                if (reta1 != 0) return false;
                reta1 = Api.SetNumberOfProfilesToCapture(_sensor._sensorObject, numberofProfile);
                _sensor.SendParameterSet();
            }
            return true;
        }


    }
}
