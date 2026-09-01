using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Smartray;

namespace AdaWeldSystem.LineLaserCam.SmartRayCam.ApiCore
{
    class Sensor : IDisposable
    {

        #region ParameterSet

        //
        // available parameter sets to preconfigure the sensor
        //
        public enum ParameterSet
        {
            Undefined = 0,
            LiveImage = 1,
            Snapshot3d = 2,
            Snapshot3dRepeat = 3,
        };

        #endregion


        #region members

        // sensor description object
        internal Api.Sensor _sensorObject;

        // reflects the reported state of the sensor (updated by API callback)
        internal SensorState _sensorState;

        internal bool m_isCapturing = false;

        // the series of the sensor, ECCO55, ECCO75...
        string _sensorSeries = string.Empty;


        // number of profiles to capture
        UInt32 _numberOfProfilesToCapture = 0;

        // packet size
        UInt32 _packetSize = 0;

        // true when live image acquisition is configured
        bool _liveAcquisition = false;

        //true when MSR mode is enabled 
        bool _msrMode = false;

        // !!! use _currentImageData & _completedImageDatas instead 
        // received sensor image data
        List<SensorImageData> _imageDatas = new List<SensorImageData>();

        //recieved point cloud data
        List<PointCloud> _cbPointCloud = new List<PointCloud>();       // Point Clouds received from sensor via the callback function.

        //recieved point cloud data
        List<PointCloudMSR> _cbPointCloudMSR = new List<PointCloudMSR>();      // Point Clouds received from sensor via the callback function.

        uint _numberOfCapturedProfiles;                     // Total number of profiles captured in the currect aquisition.

        string _pointCloudFilePrefix;                                 // Filename-prefix, to store Point CLoud data.

        float _transportResolution;                                 // x-axis resolution (transport resolution) for point cloud data.

        bool _saveAllPoints;                                           // true: all points of the Point Cloud are stored in the file
                                                                       // false: only the valid points of the Point Cloud are stored in the file.

        public int SensorIndex = 0;

        // will be triggered if a complete has been received
        AutoResetEvent _imageCompleted = new AutoResetEvent(false);

        protected List<MetaData> _meta_data = new List<MetaData>();
        protected bool _metaDataExport;
        protected UInt32 _packetTimeout;

        //define a delegate and event
        public delegate void delegateAcqusitionCompleted(Object Sender, EventArgs e);

        public event delegateAcqusitionCompleted AcqusitionCompletedEvent;

        //define a delegate and event for PointCloud

        public delegate void delegatePointCloudCompleted(Object Sender, EventArgs e);

        public event delegatePointCloudCompleted PointCloudCompletedEvent;

        //define a delegate and event for dualhead sensor
        public delegate void delegatePointCloudMSRCompleted(Object Sender, EventArgs e);
        public event delegatePointCloudMSRCompleted PointCloudMSRCompleteEvent;

        #endregion


        #region helper functions

        /// <summary>
        /// trace messeage to the console output
        /// </summary>
        static void Trace(string text)
        {
            SensorManager.Trace(text);
        }

        //
        // callback for data with not registered command numbers
        //
        static int UnknownSensorCommandCallback(Api.Sensor sensorObject)
        {
            Trace("WARN: unknown command received for sensor: " + sensorObject.name);
            return 0;
        }

        //
        // get image data object, so image packages can be added
        //
        internal SensorImageData AddImageData(int width, int height, Api.ImageDataType imageType, int originX = 0, float originYMillimeters = 0)
        {
            lock (_imageDatas)
            {
                // !!! add sync to c++ as well
                if (_msrMode)
                {
                    _imageDatas.Add(new SensorImageData(width, height, imageType, originX, originYMillimeters));
                    _imageDatas.Last().CurrentHeight += height;
                    Trace("New MSR sensor image created, #images: " + _imageDatas.Count().ToString() + " ### ");
                }
                else if (_imageDatas.Any() && _imageDatas.Last().CurrentHeight < _numberOfProfilesToCapture)
                {
                    _imageDatas.Last().CurrentHeight += height;
                    Trace("Sensor image updated, height: " + _imageDatas.Last().CurrentHeight.ToString() + " ### ");
                }
                else
                {
                    _imageDatas.Add(new SensorImageData(width, (int)_numberOfProfilesToCapture, imageType, originX, originYMillimeters));
                    _imageDatas.Last().CurrentHeight += height;
                    Trace("New sensor image created, #images: " + _imageDatas.Count().ToString() + " ### ");
                }
                return _imageDatas.Last();
            }
        }

        //
        // triggers the 
        //
        internal void SignalImageDataCompleted(SensorImageData imageData)
        {
            if (imageData.ContainsFullImages())
            {
                _imageCompleted.Set();
                this.AcqusitionCompletedEvent(this, new EventArgs());
            }

        }

        /// <summary>
        /// clear the buffer
        /// </summary>
        public void ClearImageData()
        {
            lock (_imageDatas)
            {
                _imageDatas.Clear();
            }
        }

        #endregion

        #region data types

        public class MetaData
        {
            public Api.MetaData[] meta_data;
            public UInt32 height;
        };

        #endregion

        //
        // Ctor
        //
        internal Sensor(string name, int multiSensorIndex, string ipAddress, ushort port)
        {
            _sensorObject = new Api.Sensor();
            _sensorState = new SensorState();

            // sensor index (increment by one for each new sensor if you want to implement >1 sensors)
            _sensorObject.cam_index = multiSensorIndex;

            // sensor name
            _sensorObject.name = name;

            // sensor IP address (can be changed ATTENTION!!!! Don't forget new IPAddress!)
            _sensorObject.IPAdr = ipAddress;

            // sensor IP port number (can be changed ATTENTION!!!! Don't forget new IPAddress!) 
            _sensorObject.portnum = port;

            // install data callback for "unknown" commands (advanced mode only)
            _sensorObject.usercbf = UnknownSensorCommandCallback;

            // init image & packet size information
            _numberOfProfilesToCapture = 0;
            _packetSize = 0;

            // true when MSR mode is enabled
            _msrMode = false;

            SetTransportResolution(0.100f);

            _saveAllPoints = true;

            _pointCloudFilePrefix = "Point_Cloud_data";
        }

        /// <summary>
        /// cleanup
        /// </summary>
        public void Dispose()
        {
            if (_sensorObject != null)
            {
                _sensorObject.Dispose();
                _sensorObject = null;
            }
        }

        //
        // connect to the sensor
        //
        public int Connect()
        {
            //=======================================
            // Connect to the sensor
            // reconnect automatically if disconnected
            // disconnect if alive signal timeout
            //======================================= 
            int timeoutS = 10;
            int ret = Api.ConnectSensor(_sensorObject, timeoutS);
            if (SensorManager.HandleReturnCode(ret))
            {
                Trace("Sensor: " + _sensorObject.name + " configured to " + _sensorObject.IPAdr + ":" + _sensorObject.portnum);
                Trace("\n=== sensor connected ===");

                try
                {
                    GetSensorSeries();
                }
                catch (Exception e)
                {
                    Trace("WARN: requesting the sensor series failed!:" + e.Message);
                }
            }
            return ret;
        }

        //
        // extract sensor series from the sensor model name  which will be used to set parameter set path
        //
        public void GetSensorSeries()
        {

            //=======================================
            // get sensor type & model name
            //=======================================
            string modelName;
            string partNumber;
            Trace("requesting sensor type");
            int ret = Api.GetSensorModelName(_sensorObject, out modelName, out partNumber);

            SensorManager.HandleReturnCode(ret);
            Trace("sensor model name: " + modelName + " part number: " + partNumber);

            // extract sensor series from the sensor model name
            _sensorSeries = string.Empty;
            for (int i = 0; i < modelName.Length; i++)
            {
                if (modelName[i] == '.')
                    break;

                if (modelName[i] == ' ')
                    continue;

                if (_sensorSeries.Length == 4 && _sensorSeries.Substring(0, 2) == "SR")
                {
                    _sensorSeries += "00";
                    break;
                }
                _sensorSeries += modelName[i];
            }
        }

        //
        // disconnect from the sensor
        //
        public void Disconnect()
        {
            if (_sensorState.SensorConnection != SensorState.ConnectionState.Connected)
                return;

            //=======================================
            // disconnect from sensor and stop trying to connect
            //=======================================
            Trace("stop sensor connection");
            int ret = Api.DisconnectSensor(_sensorObject);
            SensorManager.HandleReturnCode(ret);
        }

        public void AddPointCloudData(int numPoints,
            int numProfile,
            Api.Point3d[] point_cloud,
            ushort[] intensity,
            ushort[] laserlinethickness,
            uint[] profileIdx,
            uint[] columnIdx)
        {
            SensorManager.Trace("*** Point Cloud callback(): " + _sensorObject.name);
            lock (_cbPointCloud)
            {
                AddPointCloud(numPoints, numProfile, point_cloud, intensity, laserlinethickness, profileIdx, columnIdx);
                _numberOfCapturedProfiles += (uint)numProfile;
                Api.GetNumberOfProfilesToCapture(_sensorObject, out uint fullNumberOfProfile);

                if (_numberOfCapturedProfiles >= fullNumberOfProfile)
                {
                    this.PointCloudCompletedEvent(this, new EventArgs());
                }
            }
            SensorManager.Trace("*** callback() finished.");
        }

        public void AddPointCloudMSRData(
            int numPoints,
            Api.Point3d[] point_cloud,
            ushort[] intensity,
            ushort[] laserlinethickness,
            uint[] sensorIdx,
            uint[] profileIdx,
            uint[] pointIdx)
        {
            SensorManager.Trace("*** Point Cloud MSR callback(): " + _sensorObject.name);
            lock (_cbPointCloudMSR)
            {
                AddPointCloudMSR(numPoints, point_cloud, intensity, laserlinethickness,
                    sensorIdx, profileIdx, pointIdx);
                this.PointCloudMSRCompleteEvent(this, new EventArgs());
            }
            SensorManager.Trace("*** callback() finished.");
        }

        public void AddPointCloud(int numPoints,
            int numProfile,
            Api.Point3d[] point_cloud,
            ushort[] intensity,
            ushort[] laserlinethickness,
            uint[] profileIdx,
            uint[] columnIdx)
        {
            PointCloud pointcloud = new PointCloud(
                (uint)numPoints, point_cloud, intensity, laserlinethickness,
                profileIdx, columnIdx);
            _cbPointCloud.Add(pointcloud);
        }

        public void AddPointCloudMSR(
            int numPoints,
            Api.Point3d[] point_cloud,
            ushort[] intensity,
            ushort[] laserlinethickness,
            uint[] sensorIdx,
            uint[] profileIdx,
            uint[] pointIdx)
        {
            PointCloudMSR pointcloud = new PointCloudMSR(
                (uint)numPoints, point_cloud, intensity, laserlinethickness,
                sensorIdx, profileIdx, pointIdx);
            _cbPointCloudMSR.Add(pointcloud);
        }

        public void AddMetaData(UInt32 height, Api.MetaData[] meta_data)
        {
            Api.MetaData[] new_meta_data = new Api.MetaData[height];
            Array.Copy(meta_data, new_meta_data, height);

            //memcpy(new_meta_data, meta_data, height * sizeof(SR_MetaData));

            MetaData data = new MetaData();
            data.meta_data = new_meta_data;
            data.height = height;

            _meta_data.Add(data);
        }

        public void ExportMetaData(string file_name)
        {
            StreamWriter printable = new StreamWriter(file_name);

            printable.WriteLine("Meta data export: ");
            printable.WriteLine();
            printable.WriteLine();

            for (int i = 0; i < _meta_data.Count(); ++i)
            {
                printable.WriteLine(" -------------------------- ");
                for (UInt32 j = 0; j < _meta_data[i].height; ++j)
                {
                    printable.WriteLine("Start trigger number       : " + _meta_data[i].meta_data[j].StartTriggerNb);
                    printable.WriteLine("Data trigger number        : " + _meta_data[i].meta_data[j].DataTriggerNb);
                    printable.WriteLine("Profile number             : " + _meta_data[i].meta_data[j].ProfileNb);
                    printable.WriteLine("Timestamp                  : " + _meta_data[i].meta_data[j].TimeStamp);
                    printable.WriteLine("Timestamp sequence         : " + _meta_data[i].meta_data[j].TimeStampSequence);
                    printable.WriteLine("Rising edge duration       : " + _meta_data[i].meta_data[j].Input_0_State);
                    printable.WriteLine("Falling edge duration      : " + _meta_data[i].meta_data[j].Input_1_State);
                    printable.WriteLine("Filtered Quadstep count    : " + _meta_data[i].meta_data[j].QuadStepCountFiltered);
                    printable.WriteLine("Raw Quadstep count         : " + _meta_data[i].meta_data[j].QuadStepCountRaw);
                    printable.WriteLine("Trigger Overflow           : " + _meta_data[i].meta_data[j].TriggerOverflow);
                    printable.WriteLine("Output Status              : " + _meta_data[i].meta_data[j].OutputStatus);
                    printable.WriteLine("Data trigger overflow count: " + _meta_data[i].meta_data[j].DataTriggerOverflowCnt);

                    printable.WriteLine();
                }

                printable.WriteLine();

                _meta_data[i].meta_data = null;
            }

            printable.Close();

            _meta_data.Clear();
        }

        public bool GetMetaDataExportEnable()
        {
            return _metaDataExport;
        }

        public void SetMetaDataExportEnable(bool enable)
        {
            Trace("enable / disable meta data export.");
            int ret = Api.SetMetaDataExportEnabled(_sensorObject, true);

            _metaDataExport = enable;

            // only ECCO 95
            //SensorManager::HandleReturnCode(ret);
        }

        public void ClearBuffer()
        {
            lock (_imageDatas)
                _imageDatas.Clear();
            lock (_cbPointCloud)
                _cbPointCloud.Clear();
            lock (_cbPointCloudMSR)
                _cbPointCloudMSR.Clear();
            lock (_meta_data)
                _meta_data.Clear();
            // _numberOfProfilesToCapture = 0;
        }

        private int safeErase<T>(ref List<T> container, int count)
        {
            lock (container)
            {
                if (container.Count() < count)
                {
                    count = container.Count();
                }
                container.RemoveRange(0, count);
            }
            return count;
        }

        public void ClearReceivedImages(int count)
        {
            safeErase(ref _imageDatas, count);
            safeErase(ref _meta_data, count);
        }

        //
        // start data acquisition
        //
        public void StartAcquisition()
        {
            int ret;

            //==============================================================================
            // get configured number of profiles to capture and packet size
            //==============================================================================
            if (!_liveAcquisition)
            {
                ret = Api.GetNumberOfProfilesToCapture(_sensorObject, out _numberOfProfilesToCapture);
                SensorManager.HandleReturnCode(ret);
                ret = Api.GetPacketSize(_sensorObject, out _packetSize);
                // ECCO95 only, 500ms default
                ret = Api.GetPacketTimeOut(_sensorObject, out _packetTimeout);
            }
            else
            {
                int originX, width;
                int originY, height;

                // live image data always contains full images
                Api.GetROI(_sensorObject, out originX, out width, out originY, out height);

                _numberOfProfilesToCapture = (UInt32)height;
                _packetSize = (UInt32)height;
                _packetTimeout = 0;
            }

            //=======================================
            // start the sensor
            //=======================================
            Trace("start sensor data acquisition.");
            ret = Api.StartAcquisition(_sensorObject);
            SensorManager.HandleReturnCode(ret);

            m_isCapturing = true;

            Trace("Number of profiles to capture: " + _numberOfProfilesToCapture);
            Trace("Profile packet size: " + _packetSize);
            Trace(string.Empty);
        }

        //
        // stop data acquisition
        //
        public void StopAcquisition()
        {
            if (!m_isCapturing)
            {
                return;
            }
            //=======================================
            // stop the sensor
            //=======================================
            Trace("stop sensor data acquisition");
            int ret = Api.StopAcquisition(_sensorObject);
            SensorManager.HandleReturnCode(ret);
        }


        //
        // returns a copy of the current sensor state
        //
        public SensorState GetSensorState()
        {
            return (SensorState)_sensorState.Clone();
        }

        //
        // returns the most recent image data if available or an empty representation
        //
        public SensorImageData GetLastImageData()
        {
            lock (_imageDatas)
            {
                // get last valid image
                for (int i = _imageDatas.Count() - 1; i >= 0; i--)
                {
                    var imageData = _imageDatas[i];
                    if (!imageData.ContainsFullImages())
                        continue;
                    return imageData;
                }
                return new SensorImageData();
            }
        }

        private bool ExpectingPointClouds()
        {
            lock (_cbPointCloud)
            {
                return _numberOfCapturedProfiles < _numberOfProfilesToCapture;
            }
        }

        public void AcquirePointCloud()
        {
            int acquisitionTimeoutMs = 15000;
            int acquisitionTimeoutStepMs = 100;
            int timeOut = acquisitionTimeoutMs;
            while (0 < timeOut && ExpectingPointClouds())
            {
                Console.Write(".");
                System.Threading.Thread.Sleep(acquisitionTimeoutStepMs);
                timeOut -= acquisitionTimeoutStepMs;
            }
            Console.WriteLine(".");
            if (timeOut < 0 && ExpectingPointClouds())
                SensorManager.HandleError("ERROR: Acquisition timeout occurred!");
        }

        private bool ExpectingImages(int totalExpectedImages)
        {
            lock (_imageDatas)
            {
                if (_imageDatas.Count() < totalExpectedImages)
                    return true;

                // wait until we got a full image
                return !_imageDatas[totalExpectedImages - 1].ContainsFullImages();
            }
        }

        //
        // wait until the number of expected images has been received
        //
        public void WaitForImage(int totalExpectedImages)
        {
            //=======================================
            // wait until requested number of images have been received
            //=======================================
            int acquisitionTimeoutMs = 15000;
            int acquisitionTimeoutStepMs = 100;
            while (0 < acquisitionTimeoutMs && ExpectingImages(totalExpectedImages))
            {
                if (!_imageCompleted.WaitOne(acquisitionTimeoutStepMs))
                    acquisitionTimeoutMs -= acquisitionTimeoutStepMs;
            }
            Trace("number of expected images: " + totalExpectedImages + " number of received images: " + _imageDatas.Count());
            if (acquisitionTimeoutMs == 0 && ExpectingImages(totalExpectedImages))
                SensorManager.HandleError("image acquisition timeout reached!");
        }

        private bool ExpectingPointCloudsMSR(int expected)
        {
            lock (_cbPointCloudMSR)
            {
                return _cbPointCloudMSR.Count() < expected;
            }
        }

        public void WaitForPointCloudMSR(int expected)
        {
            //=======================================
            // wait until requested number of images have been received
            //=======================================
            int acquisitionTimeoutMs = 15000;
            int acquisitionTimeoutStepMs = 100;
            while (0 < acquisitionTimeoutMs && ExpectingPointCloudsMSR(expected))
            {
                System.Threading.Thread.Sleep(acquisitionTimeoutStepMs);
                acquisitionTimeoutMs -= acquisitionTimeoutStepMs;
            }
            Trace("number of expected point clouds: " + expected
                + " number of received images: " + _cbPointCloudMSR.Count());
            if (acquisitionTimeoutMs == 0 && ExpectingPointCloudsMSR(expected))
                SensorManager.HandleError("image acquisition timeout reached!");
        }

        //
        // load a certain parameter set to reconfigure the sensor
        //
        public void LoadParameterSet(ParameterSet parameterSet)
        {
            string smartrayInstallationDirectory = Environment.GetEnvironmentVariable("Smartray");
            //=======================================
            // load and use default parameter set of the respective sensor series
            //=======================================
            string parameterSetBaseDirectory = smartrayInstallationDirectory;
            parameterSetBaseDirectory += "\\SR_API\\sr_parameter_sets\\";
            string parameterSetPath = parameterSetBaseDirectory;
            switch (parameterSet)
            {
                // predefined live image parameter set for the ecco75 series as part of the installation
                case ParameterSet.LiveImage:
                    parameterSetPath += "Pars_" + _sensorSeries + "\\" + _sensorSeries + "_Liveimage.par";
                    _liveAcquisition = true;
                    break;
                case ParameterSet.Snapshot3d:
                    parameterSetPath += "Pars_" + _sensorSeries + "\\" + _sensorSeries + "_3D_Snapshot.par";
                    _liveAcquisition = false;
                    break;
                case ParameterSet.Snapshot3dRepeat:
                    parameterSetPath += "Pars_" + _sensorSeries + "\\" + _sensorSeries + "_3D_Repeat_Snapshot.par";
                    _liveAcquisition = false;
                    break;

                default:
                    SensorManager.HandleError("unsupported parameter set specified: " + parameterSet);
                    break;
            }

            // try to read the parameter set from the file
            Trace("try to read parameter set from file: " + parameterSetPath);

            int ret = Api.LoadParameterSetFromFile(_sensorObject, parameterSetPath);
            SensorManager.HandleReturnCode(ret);
        }


        //
        // send a parameter set to the sensor
        //
        public void SendParameterSet()
        {
            Trace("send the parameter set to the sensor.");
            int ret = Api.SendParameterSetToSensor(_sensorObject);
            SensorManager.HandleReturnCode(ret);
            // ECCO95 only, 500ms default
            ret = Api.SetPacketTimeOut(_sensorObject, 500);
        }

        //
        // set multi exposure merge mode
        //
        public void SetMultiExposureMode(Api.MultipleExposureMergeModeType merge_mode)
        {
            Trace("set multi exposure merge mode.");
            int ret = Api.SetMultiExposureMode(_sensorObject, merge_mode);
        }

        //
        // loading a calibration file is necessary for creating point cloud and ZIL
        //
        public int LoadCalibrationDataFromSensor()
        {
            Trace("loading calibration from the sensor");
            int ret = Api.LoadCalibrationDataFromSensor(_sensorObject);
            SensorManager.HandleReturnCode(ret);
            return ret;
        }

        //
        // configures the region of interest to a quarter of the full scanner image size
        //
        public void ConfigureRegionOfInterestDivider(int divider = 2)
        {
            //=======================================
            // get sensor resolution
            //=======================================
            int sensorWidth = 0;
            int sensorHeight = 0;
            Trace("requesting sensor resolution");
            int ret = Api.GetSensorMaxDimensions(_sensorObject, out sensorWidth, out sensorHeight);
            SensorManager.HandleReturnCode(ret);
            Trace("sensor resolution: " + sensorWidth + " x " + sensorHeight);

            //=======================================
            // setup region of interest
            //=======================================
            int regionOfInterestX = 0;
            int regionOfInterestY = 0;
            int regionOfInterestWidth = sensorWidth / divider;
            int regionOfInterestHeight = sensorHeight / divider;
            Trace("setting region of interest to " + regionOfInterestWidth + " x " + regionOfInterestHeight);
            ret = Api.SetROI(_sensorObject, regionOfInterestX, regionOfInterestWidth, regionOfInterestY, regionOfInterestHeight);
            SensorManager.HandleReturnCode(ret);
        }

        public void EnableSmartXpress()
        {
            //=======================================
            // enable SmartXpress on the sensor
            //=======================================
            int ret = Api.SetSmartXpress(_sensorObject, true);
            SensorManager.HandleReturnCode(ret);

            //=======================================
            // set a SmartXpress configuration file (*.sxp)
            //=======================================
            string executableFilePath = System.IO.Directory.GetCurrentDirectory();
            string configurationFilePath = executableFilePath + "\\SmartXpressSample.sxp";
            ret = Api.SetSmartXpressConfiguration(_sensorObject, configurationFilePath);
            SensorManager.HandleReturnCode(ret);

            //=======================================
            // get the current SmartXpress configuration
            //=======================================
            string configuration;
            ret = Api.GetSmartXpressConfiguration(_sensorObject, out configuration, 260);
            SensorManager.HandleReturnCode(ret);
            Trace("SmartXpress Configuration: " + configuration);
        }

        public void DisableSmartXpress()
        {
            //=======================================
            // disable SmartXpress on the sensor
            //=======================================
            int ret = Api.SetSmartXpress(_sensorObject, false);
            SensorManager.HandleReturnCode(ret);
        }

        public void EnableSmartXtract()
        {
            //=======================================
            // set a SmartXtract configuration file (*.sxt)
            //=======================================
            string reflection_filter = Environment.GetEnvironmentVariable("Smartray")
                + "\\SR_API\\smartxtract\\archive.sxt";
            int ret = Api.SetSmartXtractPreset(_sensorObject, reflection_filter);
            SensorManager.HandleReturnCode(ret);

            //=======================================
            // enable SmartXtract on the sensor
            //=======================================
            ret = Api.EnableSmartXtract(_sensorObject, true);
            SensorManager.HandleReturnCode(ret);
        }

        public void DisableSmartXtract()
        {
            //=======================================
            // disable SmartXpress on the sensor
            //=======================================
            int ret = Api.EnableSmartXtract(_sensorObject, false);
            SensorManager.HandleReturnCode(ret);
        }

        public void EnableSmartXtractArchive()
        {
            //=======================================
            // enable SmartXpress on the sensor
            //=======================================
            int ret = Api.ArchiveSmartXtractData(_sensorObject, ".\\SmartXtractArchiveSample_archive.dat");
            SensorManager.HandleReturnCode(ret);
        }

        public void DisableSmartXtractArchive()
        {
            //=======================================
            // disable SmartXpress on the sensor
            //=======================================
            int ret = Api.DisableArchiveSmartXtractData(_sensorObject);
            SensorManager.HandleReturnCode(ret);
        }

        public void SetSmartXtractAlgorithm(Api.SmartXtractAlgorithmType mode)
        {
            SensorManager.HandleReturnCode(
                Api.SetSmartXtractAlgorithm(_sensorObject, mode));
        }

        public void ShowsScanRate()
        {
            //=======================================
            // shows the scan rate
            //=======================================
            int scanRateMax = 0;
            int ret = Api.GetMaximumScanRate(_sensorObject, out scanRateMax);
            Trace("Scan rate: " + scanRateMax);
            SensorManager.HandleReturnCode(ret);
        }

        /// <summary>
        /// 诊断：读取传感器在当前 ROI+曝光配置下的最大扫描率（硬件天花板），
        /// 并输出当前数据触发模式与内部触发频率。用于确认能否达到目标分析频率（如 100Hz）。
        /// 注意 SDK 约束：内部触发频率不可超过 GetMaximumScanRate 返回的最大扫描率
        /// （受当前 ROI 与曝光时间限制）。
        /// </summary>
        public string GetScanRateDiagnostics()
        {
            int scanRateMax = 0;
            int ret = Api.GetMaximumScanRate(_sensorObject, out scanRateMax);
            SensorManager.HandleReturnCode(ret);

            var mode = Api.DataTriggerMode.FreeRunning;
            ret = Api.GetDataTriggerMode(_sensorObject, out mode);
            SensorManager.HandleReturnCode(ret);

            int internalFreq = 0;
            string modeText;
            if (mode == Api.DataTriggerMode.Internal)
            {
                ret = Api.GetDataTriggerInternalFrequency(_sensorObject, out internalFreq);
                SensorManager.HandleReturnCode(ret);
                modeText = "Internal 内部触发(当前频率=" + internalFreq + "Hz)";
            }
            else if (mode == Api.DataTriggerMode.External)
            {
                modeText = "External 外部触发";
            }
            else
            {
                modeText = "FreeRunning 自由运行";
            }

            string verdict = (scanRateMax >= 100)
                ? "可达(需将内部触发频率显式配置为100Hz)"
                : "不可达(需缩小ROI/降低曝光以提升上限)";
            string msg = string.Format(
                "[扫描率诊断] 最大扫描率={0}Hz | 触发模式={1} | 目标100Hz={2}",
                scanRateMax, modeText, verdict);
            Trace(msg);
            return msg;
        }

        /// <summary>
        /// 读取当前有效扫描率（Hz），供工作流按帧周期缩放 WaitOne 超时：
        /// Internal 触发模式返回内部触发频率；FreeRunning/External 返回最大扫描率；
        /// 任一 SDK 调用失败则回退默认 10Hz。全程 try/catch，不向外抛异常。
        /// </summary>
        public double GetEffectiveScanRateHz()
        {
            int scanRateMax = 0;
            try
            {
                int ret = Api.GetMaximumScanRate(_sensorObject, out scanRateMax);
                SensorManager.HandleReturnCode(ret);
            }
            catch
            {
                scanRateMax = 0;
            }

            var mode = Api.DataTriggerMode.FreeRunning;
            try
            {
                int ret = Api.GetDataTriggerMode(_sensorObject, out mode);
                SensorManager.HandleReturnCode(ret);
            }
            catch
            {
                // 读取模式失败，沿用默认 FreeRunning
            }

            if (mode == Api.DataTriggerMode.Internal)
            {
                int internalFreq = 0;
                try
                {
                    int ret = Api.GetDataTriggerInternalFrequency(_sensorObject, out internalFreq);
                    SensorManager.HandleReturnCode(ret);
                }
                catch
                {
                    internalFreq = 0;
                }
                if (internalFreq > 0) return (double)internalFreq;
            }

            if (scanRateMax > 0) return (double)scanRateMax;
            return 10.0;
        }

        //
        // configures the exposure times
        //
        public void ConfigureExposureTimesMicroS(int exposureTime1MicroS = 1000, int exposureTime2MicroS = 2000)
        {
            //=======================================
            // set the exposure time
            //=======================================
            Trace("enabling multiple exposure mode, exposure time 1: "
                + exposureTime1MicroS + " Microseconds "
                + " time 2: "
                + exposureTime2MicroS + " Microseconds ");

            // set number of exposure times
            int ret = Api.SetNumberOfExposureTimes(_sensorObject, 2);
            SensorManager.HandleReturnCode(ret);

            // set first exposure time
            ret = Api.SetExposureTime(_sensorObject, 0, exposureTime1MicroS);
            SensorManager.HandleReturnCode(ret);

            // set 2nd exposure time (multiple exposure feature)
            ret = Api.SetExposureTime(_sensorObject, 1, exposureTime2MicroS);
            SensorManager.HandleReturnCode(ret);
        }

        //
        // configure the laser parameters
        //
        public void ConfigureLaserBrightnessPercent(int laserPowerPercent = 100)
        {
            Trace("laser brightness configured to " + laserPowerPercent + " percent");
            int ret = Api.SetLaserBrightness(_sensorObject, laserPowerPercent);
            SensorManager.HandleReturnCode(ret);
        }

        //
        // configure an trigger mode to internal data trigger
        // 
        public void ConfigureDataTriggerToFrequencyHz(int triggerFrequencyHz = 10)
        {
            //=======================================
            // configure data trigger (triggers that capture PIL')
            // Data Trigger Modes: 
            //	 1. Free Run
            //	 2. Internal
            //	 3. External
            //=======================================
            Trace("set data trigger mode to internal");
            int ret = Api.SetDataTriggerMode(_sensorObject, Api.DataTriggerMode.Internal);
            SensorManager.HandleReturnCode(ret);

            // configure internal data trigger frequency
            // Note: The internal data trigger frequency cannot be greater than the maximum scan rate achievable by the sensor 
            //	   in "Data Trigger Mode: Free Run", for the configured ROI and Exposure Time)
            Trace("set data trigger internal frequency: " + triggerFrequencyHz + " Hz");
            ret = Api.SetDataTriggerInternalFrequency(_sensorObject, triggerFrequencyHz);
            SensorManager.HandleReturnCode(ret);
        }

        //
        // configure the start trigger for image acquisition cycle
        // 
        public void ConfigureStartTriggerOnHardwareInput(Api.StartTriggerSource source, bool enable = true)
        {
            //=======================================
            // configure the start trigger 
            // (triggers the start for a frame capture)
            //=======================================
            Trace("configure the acquisition start trigger");
            int ret = Api.SetStartTrigger(_sensorObject, source, enable, Api.TriggerEdgeMode.RisingEdge);
            SensorManager.HandleReturnCode(ret);
        }

        //
        // configure 3D image acquisition type and number of profiles to capture
        //
        public void ConfigureImageAquisition(Api.ImageAcquisitionType imageType, uint numberOfProfiles = 100)
        {
            //=======================================
            // configure acquiring parameters manually
            //=======================================

            // set image type for provided sensor data
            Trace("configuring the image type to: " + imageType);
            int ret = Api.SetImageAcquisitionType(_sensorObject, imageType);
            SensorManager.HandleReturnCode(ret);

            _liveAcquisition = imageType == Api.ImageAcquisitionType.LiveImage;

            Trace("configuring the number of profiles to be acquired: " + numberOfProfiles);
            ret = Api.SetNumberOfProfilesToCapture(_sensorObject, numberOfProfiles);
            SensorManager.HandleReturnCode(ret);
            ret = Api.SetPacketSize(_sensorObject, 0 /* autopacketsize */);
            SensorManager.HandleReturnCode(ret);
        }

        //
        // create a point cloud from profile image data
        //
        public Api.Point3d[,] CreatePointCloud(SensorImageData profileImageData)
        {
            Trace("creating a 3D point cloud from the profile image");

            //============================================================
            // create the point cloud from the provided profile image data
            //============================================================
            var pointCloud = new Api.Point3d[profileImageData.CurrentHeight, profileImageData.Width];
            int ret = Api.CreatePointCloudMultipleProfile(_sensorObject,
                profileImageData.ProfileImage.GetImage(),
                profileImageData.OriginX,
                profileImageData.Width,
                profileImageData.CurrentHeight,
                pointCloud);
            SensorManager.HandleReturnCode(ret);

            return pointCloud;
        }

        //
        // configure msr mode
        //
        public void ConfigureMSRMode()
        {
            Trace("enable MSR mode");
            int ret = Api.MSREnableRegistration(true);
            SensorManager.HandleReturnCode(ret);

            string default_MSR_registration_path = Environment.GetEnvironmentVariable("Smartray");
            default_MSR_registration_path += "\\SR_Studio_4\\msr\\multi-sensor-registration\\transformation.xml";

            Trace("load registration file created during MSR registration mode:\n\t" + default_MSR_registration_path + "\n");
            /*Load the correct registration file path*/
            ret = Api.MSRLoadRegistrationFile(default_MSR_registration_path);
            SensorManager.HandleReturnCode(ret);
            _msrMode = true;
        }

        //
        // call getter & display the sensor resolution
        //
        public void GetSensorResolution()
        {
            //=======================================
            // getter sensor resolution
            //=======================================
            int sensorWidth = 0;
            int sensorHeight = 0;
            Trace("requesting sensor resolution");
            int ret = Api.GetSensorMaxDimensions(_sensorObject, out sensorWidth, out sensorHeight);
            SensorManager.HandleReturnCode(ret);
            Trace("sensor resolution: " + sensorWidth + " x " + sensorHeight);
        }

        //
        // call getter & display the region of interest parameters
        //
        public void GetRegionOfInterestParameters()
        {
            //=======================================
            // getter region of interest
            //=======================================
            int regionOfInterestX = 0;
            int regionOfInterestY = 0;
            int regionOfInterestWidth = 0;
            int regionOfInterestHeight = 0;
            Trace("requesting region of interest ");
            int ret = Api.GetROI(_sensorObject, out regionOfInterestX, out regionOfInterestWidth, out regionOfInterestY, out regionOfInterestHeight);
            SensorManager.HandleReturnCode(ret);
            Trace("region of interest: X :" + regionOfInterestX
                        + " width : " + regionOfInterestWidth
                        + " Y : " + regionOfInterestY
                        + " height : " + regionOfInterestHeight);
        }

        //
        // call getter & display the exposure times parameters
        //
        public void GetExposureParameters()
        {
            //=======================================
            // getter gain
            //=======================================
            bool gainEnable = false;
            int gainValue = 0;
            Trace("requesting gain settings... ");
            int ret = Api.GetGain(_sensorObject, out gainEnable, out gainValue);
            if (SensorManager.HandleReturnCode(ret))
                Trace("gain settings : gainEnable :" + gainEnable
                    + " gainValue : " + gainValue);


            //=======================================
            // getter exposure
            //=======================================
            int exposureValue = 0;
            Trace("requesting exposure 1 value... ");
            ret = Api.GetExposureTime(_sensorObject, 0, out exposureValue);
            SensorManager.HandleReturnCode(ret);
            Trace("exposure 1 value : " + exposureValue);
            Trace("requesting exposure 2 value... ");
            ret = Api.GetExposureTime(_sensorObject, 1, out exposureValue);
            if (!SensorManager.HandleReturnCode(ret, false))
                Trace("exposure 2 is not enabled ");
            else
                Trace("exposure 2 value : " + exposureValue);

        }

        //
        // call getter & display the laser parameters
        //
        public void GetLaserParameters()
        {
            //=======================================
            // getter laser parameters
            //=======================================
            bool enable = false;
            int laserBrightness = 0;
            Smartray.Api.LaserMode mode = 0;
            Trace("requesting laser power state... ");
            int ret = Api.GetLaserPower(_sensorObject, out enable);
            SensorManager.HandleReturnCode(ret);
            Trace("laser power state : " + enable);
            Trace("requesting laser mode... ");
            ret = Api.GetLaserMode(_sensorObject, out mode);
            SensorManager.HandleReturnCode(ret);
            Trace("laser mode : " + mode);
            Trace("requesting laser brightness... ");
            ret = Api.GetLaserBrightness(_sensorObject, out laserBrightness);
            SensorManager.HandleReturnCode(ret);
            Trace("laser brightness : " + laserBrightness);

        }

        //
        // call getter & display start trigger parameters
        //
        public void GetStartTriggerParameters()
        {
            //=======================================
            // getter Start Trigger parameters
            //=======================================
            var source = Api.StartTriggerSource.None;
            bool enable = false;
            var edge = Api.TriggerEdgeMode.FallingEdge;
            Trace("requesting start trigger settings... ");
            int ret = Api.GetStartTrigger(_sensorObject, out source, out enable, out edge);
            SensorManager.HandleReturnCode(ret);
            Trace("start Trigger settings : ");
            Trace("source : " + source + " enable : " + enable + " edge : " + edge);
        }

        //
        // call getter & display the data trigger parameters
        //
        public void GetDataTriggerParameters()
        {
            //=======================================
            // getter data trigger parameters
            //=======================================
            var mode = Api.DataTriggerMode.FreeRunning;
            Trace("requesting data trigger mode");
            int ret = Api.GetDataTriggerMode(_sensorObject, out mode);
            SensorManager.HandleReturnCode(ret);
            Trace("data trigger mode : " + mode);
            if (mode == Api.DataTriggerMode.Internal)
            {
                int internalFrequency = 0;
                Trace(" requesting data trigger internal frequency... ");
                ret = Api.GetDataTriggerInternalFrequency(_sensorObject, out internalFrequency);
                SensorManager.HandleReturnCode(ret);
                Trace("internal frequency : " + internalFrequency);
            }
            if (mode == Api.DataTriggerMode.External)
            {
                var dataTriggerSource = Api.DataTriggerSource.Input1;
                Trace("requesting external data trigger source");
                ret = Api.GetDataTriggerExternalTriggerSource(_sensorObject, out dataTriggerSource);
                SensorManager.HandleReturnCode(ret);
                Trace("external data trigger source : " + dataTriggerSource);
                if (dataTriggerSource == Api.DataTriggerSource.Input1 ||
                    dataTriggerSource == Api.DataTriggerSource.Input2 ||
                    dataTriggerSource == Api.DataTriggerSource.Combined ||
                    dataTriggerSource == Api.DataTriggerSource.QuadEncoder)
                {
                    int triggerdelay = 0;
                    int triggerdivider = 0;
                    var triggercondition = Api.TriggerEdgeMode.Both;
                    Trace("requesting external data trigger parameters");
                    ret = Api.GetDataTriggerExternalTriggerParameters(_sensorObject, out triggerdivider, out triggerdelay, out triggercondition);
                    SensorManager.HandleReturnCode(ret);
                    Trace("external trigger parameters : ");
                    Trace("trigger divider : " + triggerdivider + " trigger delay : " + triggerdelay + " trigger direction " + triggercondition);
                }
            }
        }

        //
        // call getter & display the reflection filter parameters
        //
        public void GetReflectionFilterParameters()
        {
            //=======================================
            // getter reflection filter parameters
            //=======================================
            bool enableReflectionFilter = false;
            int algorithm = 0;
            int presets = 0;
            Trace("requesting reflection filter parameters... ");
            int ret = Api.GetReflectionFilter(_sensorObject, out enableReflectionFilter, out algorithm, out presets);
            Trace("reflection filter parameters : ");
            Trace("enable : " + enableReflectionFilter + " algorithm : " + algorithm + "Preset : " + presets);
        }

        //
        // call getter & display the sensor acquisition parameters
        //
        public void GetAcquisitionParameters()
        {
            //=======================================
            // getter acquisition parameters
            //=======================================
            var acquisitionType = Api.ImageAcquisitionType.Profile;
            Trace("requesting image acquisition type... ");
            int ret = Api.GetImageAcquisitionType(_sensorObject, out acquisitionType);
            SensorManager.HandleReturnCode(ret);
            Trace(" image acquisition type : " + acquisitionType);
            var acquisitionMode = Api.AcquisitionMode.Snapshot;
            Trace("requesting image acquisition mode... ");
            ret = Api.GetAcquisitionMode(_sensorObject, out acquisitionMode);
            SensorManager.HandleReturnCode(ret);
            Trace(" image acquisition mode : " + acquisitionMode);
            UInt32 numberofProfile = 0;
            UInt32 packetSize = 0;
            UInt32 packetTimeout = 0;
            Trace("requesting number of profiles to capture and packet size... ");
            ret = Api.GetNumberOfProfilesToCapture(_sensorObject, out numberofProfile);
            SensorManager.HandleReturnCode(ret);
            ret = Api.GetPacketSize(_sensorObject, out packetSize);
            SensorManager.HandleReturnCode(ret);
            // ECCO95 only, 500ms default timeout
            ret = Api.GetPacketTimeOut(_sensorObject, out packetTimeout);
            Trace(" Number of profiles to capture : " + numberofProfile + " Packet size : " + packetSize + " Packet timeout : " + packetTimeout);
            int laserlineThreshold = 0;
            Trace("requesting laser line threshold... ");
            ret = Api.Get3DLaserLineBrightnessThreshold(_sensorObject, 0, out laserlineThreshold);
            SensorManager.HandleReturnCode(ret);
            Trace("laser line threshold for exposure index 0 : " + laserlineThreshold);
            ret = Api.Get3DLaserLineBrightnessThreshold(_sensorObject, 1, out laserlineThreshold);
            SensorManager.HandleReturnCode(ret);
            Trace("laser line threshold for exposure index 1 : " + laserlineThreshold);
        }

        //
        // call getter & display the sensor information
        //
        public void GetSensorInformation()
        {
            //=======================================
            // getter sensor parameters
            //=======================================
            string apiVersion;
            Trace("requesting API version...  ");
            int ret = Api.GetApiVersion(out apiVersion);
            SensorManager.HandleReturnCode(ret);
            Trace(" API version : " + apiVersion);

            string macAddress;
            Trace("requesting sensor mac address...  ");
            ret = Api.GetSensorMacAddress(_sensorObject, out macAddress);
            SensorManager.HandleReturnCode(ret);
            Trace(" sensor mac address : " + macAddress);

            string partNumber;
            string modelName;
            Trace("requesting sensor model name and part number...  ");
            ret = Api.GetSensorModelName(_sensorObject, out modelName, out partNumber);
            SensorManager.HandleReturnCode(ret);
            Trace("sensor model name : " + modelName + " part number : " + partNumber);

            string firmwareVersion;
            Trace("requesting sensor firmware version...  ");
            ret = Api.GetSensorFirmwareVersion(_sensorObject, out firmwareVersion);
            SensorManager.HandleReturnCode(ret);
            Trace("sensor firmware version : " + firmwareVersion);

            string serialNumber;
            Trace("requesting sensor serial number...  ");
            ret = Api.GetSensorSerialNumber(_sensorObject, out serialNumber);
            SensorManager.HandleReturnCode(ret);
            Trace("sensor serial number : " + serialNumber);

            int originX = 0;
            int originY = 0;
            Trace("requesting sensor origin...  ");
            ret = Api.GetSensorOrigin(_sensorObject, out originX, out originY);
            SensorManager.HandleReturnCode(ret);
            Trace(" Sensor origin ");
            Trace("origin X : " + originX + " origin Y : " + originY);

            int granualarityX = 0;
            int granualarityY = 0;
            Trace("requesting sensor granularity...  ");
            ret = Api.GetSensorGranularity(_sensorObject, out granualarityX, out granualarityY);
            SensorManager.HandleReturnCode(ret);
            Trace("sensor granularity");
            Trace("granularity X : " + granualarityX + " granularity Y : " + granualarityY);

            int minMeasurementRange = 0;
            int maxMeasurementRange = 0;
            Trace("requesting sensor measurement range...  ");
            ret = Api.GetMeasurementRange(_sensorObject, out minMeasurementRange, out maxMeasurementRange);
            SensorManager.HandleReturnCode(ret);
            Trace(" sensor measurement range");
            Trace("minimum measurement range : " + minMeasurementRange + " maximum measurement Range : " + maxMeasurementRange);

            float lateralResolution = 0;
            float verticalResolution = 0;
            Trace("requesting ZMap resolution...  ");
            ret = Api.GetZmapResolution(_sensorObject, out lateralResolution, out verticalResolution);
            SensorManager.HandleReturnCode(ret);
            Trace("ZMap resolution");
            Trace("horizontal resolution : " + lateralResolution + " vertical resolution : " + verticalResolution);
        }

        public void SavePointCloudToFile(string prefix)
        {
            lock (_cbPointCloud)
            {
                _pointCloudFilePrefix = prefix;
                string fileName = _pointCloudFilePrefix + ".txt";
                PointCloud.PrintHeader(fileName, _transportResolution);
                uint lastPointIdx = 0;
                foreach (var pointCloudX in _cbPointCloud)
                {
                    lastPointIdx = pointCloudX.SavePointCloud(fileName, _transportResolution, lastPointIdx, _saveAllPoints);
                }
                _numberOfCapturedProfiles = 0;
            }
        }

        public void SavePointCloudMSRToFile(string prefix)
        {
            lock (_cbPointCloudMSR)
            {
                _pointCloudFilePrefix = prefix;
                string fileName = _pointCloudFilePrefix + ".txt";
                PointCloudMSR.PrintHeader(fileName, _transportResolution);
                uint lastPointIdx = 0;
                foreach (var pointCloudX in _cbPointCloudMSR)
                {
                    lastPointIdx = pointCloudX.SavePointCloud(fileName, _transportResolution, lastPointIdx, _saveAllPoints);
                }
                _numberOfCapturedProfiles = 0;
            }
        }

        // Set pitch angle of sensor.
        public void SetTiltCorrectionPitch(float degree)
        {
            int ret = Api.SetTiltCorrectionPitch(_sensorObject, degree);
            SensorManager.HandleReturnCode(ret);
        }

        public float GetTiltCorrectionPitch()
        {
            float degree = 0.0f;
            int ret = Api.GetTiltCorrectionPitch(_sensorObject, out degree);
            SensorManager.HandleReturnCode(ret);
            return degree;
        }

        // Set transport resolution of sensor.
        public void SetTransportResolution(float transportResolution)
        {
            _transportResolution = transportResolution;
            int ret = Api.SetTransportResolution(_sensorObject, transportResolution);
            SensorManager.HandleReturnCode(ret);
        }

        // Get transport resolution of sensor.
        public float GetTransportResolution()
        {
            float transportResolution = 0.0f;
            int ret = Api.GetTransportResolution(_sensorObject, out transportResolution);
            SensorManager.HandleReturnCode(ret);
            return transportResolution;
        }

        // Set transport resolution of sensor.
        public void SetZmapResolution(float lateralResolution, float verticalResolution)
        {
            int ret = Api.SetZmapResolution(_sensorObject, lateralResolution, verticalResolution);
            SensorManager.HandleReturnCode(ret);
        }

        // Get transport resolution of sensor.
        public void GetZmapResolution(out float lateralResolution, out float verticalResolution)
        {
            int ret = Api.GetZmapResolution(_sensorObject, out lateralResolution, out verticalResolution);
            SensorManager.HandleReturnCode(ret);
        }

        // Set transport resolution of sensor.
        public void SetSmartXactMode(int mode)
        {
            int ret = Api.SetSmartXactMode(_sensorObject, mode);
            SensorManager.HandleReturnCode(ret);
        }

        // Get transport resolution of sensor.
        public int GetSmartXactMode()
        {
            int mode = 0;
            int ret = Api.GetSmartXactMode(_sensorObject, out mode);
            SensorManager.HandleReturnCode(ret);
            return mode;
        }

    }
}
