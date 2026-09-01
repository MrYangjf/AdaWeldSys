using System;

namespace AdaWeldSystem.Comm.Robot.KUKARobot
{
    /// <summary>
    /// KUKA 机器人通讯统一管理层
    /// 协调 RSI 和 EKI 的工作，对外提供简化接口
    /// </summary>
    public class KUKARobotManager : IDisposable
    {
        private const string Tag = "KUKARobotManager";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }
        private RSICommunication _rsi;
        private EKICommunication _eki;
        private bool _isRSIConnected;
        private bool _isEKIConnected;

        /// <summary>
        /// 获取 RSI 通讯实例
        /// </summary>
        public RSICommunication RSI
        {
            get { return _rsi; }
        }

        /// <summary>
        /// 获取 EKI 通讯实例
        /// </summary>
        public EKICommunication EKI
        {
            get { return _eki; }
        }

        /// <summary>
        /// 获取 RSI 是否已连接
        /// </summary>
        public bool IsRSIConnected
        {
            get { return _isRSIConnected; }
        }

        /// <summary>
        /// 获取 EKI 是否已连接
        /// </summary>
        public bool IsEKIConnected
        {
            get { return _isEKIConnected; }
        }

        /// <summary>
        /// KUKA 数据接收事件
        /// </summary>
        public event EventHandler<KUKADataReceivedEventArgs> DataReceived;

        /// <summary>
        /// KUKA 连接状态变更事件
        /// </summary>
        public event EventHandler<KUKAConnectionEventArgs> ConnectionStateChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public KUKARobotManager()
        {
            _rsi = new RSICommunication();
            _eki = new EKICommunication();

            _rsi.DataReceived += RSI_DataReceived;
            _rsi.ErrorOccurred += RSI_ErrorOccurred;
            _eki.DataReceived += EKI_DataReceived;
            _eki.ConnectionStateChanged += EKI_ConnectionStateChanged;
            _eki.ErrorOccurred += EKI_ErrorOccurred;
        }

        #region RSI Management

        /// <summary>
        /// 初始化 RSI 配置
        /// </summary>
        public bool InitializeRSI(string xmlConfigPath)
        {
            return _rsi.Initialize(xmlConfigPath);
        }

        /// <summary>
        /// 使用配置对象初始化 RSI
        /// </summary>
        public bool InitializeRSI(RSIConfig config)
        {
            return _rsi.Initialize(config);
        }

        /// <summary>
        /// 启动 RSI 通讯
        /// </summary>
        public bool StartRSI()
        {
            bool result = _rsi.Start();
            if (result)
            {
                _isRSIConnected = true;
                OnConnectionStateChanged();
            }
            return result;
        }

        /// <summary>
        /// 停止 RSI 通讯
        /// </summary>
        public bool StopRSI()
        {
            bool result = _rsi.Stop();
            _isRSIConnected = false;
            OnConnectionStateChanged();
            return result;
        }

        #endregion

        #region EKI Management

        /// <summary>
        /// 初始化 EKI 配置
        /// </summary>
        public bool InitializeEKI(string xmlConfigPath)
        {
            return _eki.Initialize(xmlConfigPath);
        }

        /// <summary>
        /// 使用配置对象初始化 EKI
        /// </summary>
        public bool InitializeEKI(EKIConfig config)
        {
            return _eki.Initialize(config);
        }

        /// <summary>
        /// 打开 EKI 连接
        /// </summary>
        public bool OpenEKI()
        {
            bool result = _eki.Open();
            if (result)
            {
                _isEKIConnected = true;
                OnConnectionStateChanged();
            }
            return result;
        }

        /// <summary>
        /// 关闭 EKI 连接
        /// </summary>
        public void CloseEKI()
        {
            _eki.Close();
            _isEKIConnected = false;
            OnConnectionStateChanged();
        }

        #endregion

        #region Data Exchange

        /// <summary>
        /// 获取当前机器人数据（通过 EKI）
        /// </summary>
        public KUKARobotData GetCurrentData()
        {
            KUKARobotData data = new KUKARobotData();

            double x, y, z, a, b, c;
            if (_eki.GetReal("Robot/Pos/X", out x)) data.RIst.X = x;
            if (_eki.GetReal("Robot/Pos/Y", out y)) data.RIst.Y = y;
            if (_eki.GetReal("Robot/Pos/Z", out z)) data.RIst.Z = z;
            if (_eki.GetReal("Robot/Pos/A", out a)) data.RIst.A = a;
            if (_eki.GetReal("Robot/Pos/B", out b)) data.RIst.B = b;
            if (_eki.GetReal("Robot/Pos/C", out c)) data.RIst.C = c;

            return data;
        }

        /// <summary>
        /// 发送传感器修正数据（通过 RSI）
        /// </summary>
        public void SendCorrectionData(KUKACorrectionData correction)
        {
            if (_rsi != null && _rsi.IsRunning)
            {
                _rsi.SendRobotData(new KUKARobotData { IPOC = correction.IPOC });
            }
        }

        /// <summary>
        /// 发送机器人状态数据（通过 RSI）
        /// </summary>
        public void SendRobotData(KUKARobotData data)
        {
            if (_rsi != null && _rsi.IsRunning)
            {
                _rsi.SendRobotData(data);
            }
        }

        /// <summary>
        /// 接收传感器修正数据（通过 RSI）
        /// </summary>
        public KUKACorrectionData ReceiveCorrectionData()
        {
            if (_rsi != null && _rsi.IsRunning)
            {
                return _rsi.ReceiveCorrectionData();
            }
            return null;
        }

        #endregion

        #region Event Handlers

        private void RSI_DataReceived(object sender, RSIDataEventArgs e)
        {
            EventHandler<KUKADataReceivedEventArgs> handler = DataReceived;
            if (handler != null)
            {
                handler(this, new KUKADataReceivedEventArgs
                {
                    RobotData = e.RobotData,
                    CorrectionData = e.CorrectionData,
                    Timestamp = e.Timestamp
                });
            }
        }

        private void RSI_ErrorOccurred(object sender, RSIErrorEventArgs e)
        {
            Log(string.Format("RSI 错误 {0} {1}", e.ErrorType, e.ErrorMessage), MessageLevel.Error);
        }

        private void EKI_DataReceived(object sender, EKIDataEventArgs e)
        {
            Log(string.Format("EKI 收到数据 {0}", e.XmlData));
        }

        private void EKI_ConnectionStateChanged(object sender, EKIConnectionEventArgs e)
        {
            _isEKIConnected = e.IsConnected;
            OnConnectionStateChanged();
            Log(string.Format("EKI 连接状态变更 {0}", e.IsConnected ? "已连接" : "已断开"));
        }

        private void EKI_ErrorOccurred(object sender, EKIErrorEventArgs e)
        {
            Log(string.Format("EKI 错误 {0}", e.ErrorMessage), MessageLevel.Error);
        }

        /// <summary>
        /// 触发连接状态变更事件
        /// </summary>
        private void OnConnectionStateChanged()
        {
            EventHandler<KUKAConnectionEventArgs> handler = ConnectionStateChanged;
            if (handler != null)
            {
                handler(this, new KUKAConnectionEventArgs
                {
                    IsRSIConnected = _isRSIConnected,
                    IsEKIConnected = _isEKIConnected,
                    Timestamp = DateTime.Now
                });
            }
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_rsi != null)
            {
                _rsi.DataReceived -= RSI_DataReceived;
                _rsi.ErrorOccurred -= RSI_ErrorOccurred;
                _rsi.Dispose();
                _rsi = null;
            }

            if (_eki != null)
            {
                _eki.DataReceived -= EKI_DataReceived;
                _eki.ConnectionStateChanged -= EKI_ConnectionStateChanged;
                _eki.ErrorOccurred -= EKI_ErrorOccurred;
                _eki.Dispose();
                _eki = null;
            }

            _isRSIConnected = false;
            _isEKIConnected = false;
            OnConnectionStateChanged();

            Log("KUKARobotManager 已释放");
        }
    }
}