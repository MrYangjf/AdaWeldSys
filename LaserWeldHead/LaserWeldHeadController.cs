using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.LaserWeldHead.API;

namespace AdaWeldSystem.LaserWeldHead
{
    /// <summary>
    /// 激光焊接头控制器实现
    /// 负责激光器状态管理、温度监视、自动对焦控制、安全 IO 监控。
    /// 在焊接头子线程中运行，实时监视各信号并上报异常。
    /// </summary>
    public class LaserWeldHeadController : ILaserWeldHeadController
    {
        #region 单例

        private static readonly Lazy<LaserWeldHeadController> _lazy =
            new Lazy<LaserWeldHeadController>(() => new LaserWeldHeadController());

        public static LaserWeldHeadController Instance => _lazy.Value;

        #endregion

        private const string Tag = "LaserWeldHeadController";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private readonly LaserWeldHeadConfig _config;
        private Timer _temperaturePollTimer;
        private bool _disposed;

        /// <summary>是否已初始化</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>激光器状态</summary>
        public bool IsLaserReady { get; private set; }

        /// <summary>当前温度数据</summary>
        public LaserHeadTemperatureData CurrentTemperature { get; private set; }

        /// <summary>当前安全 IO 状态</summary>
        public SafetyIOStatus CurrentSafetyIO { get; private set; }

        /// <summary>当前自动对焦位置数据</summary>
        public AutoFocusPositionData CurrentAutoFocusPosition { get; private set; }

        /// <summary>温度超阈值事件</summary>
        public event EventHandler<LaserHeadTemperatureData> TemperatureAlarm;

        /// <summary>安全 IO 报警事件</summary>
        public event EventHandler<SafetyIOStatus> SafetyIOAlarm;

        /// <summary>自动对焦位置变更事件</summary>
        public event EventHandler<AutoFocusPositionData> AutoFocusPositionChanged;

        private LaserWeldHeadController()
        {
            _config = new LaserWeldHeadConfig();
            CurrentTemperature = new LaserHeadTemperatureData();
            CurrentSafetyIO = new SafetyIOStatus();
            CurrentAutoFocusPosition = new AutoFocusPositionData();
            IsInitialized = false;
            IsLaserReady = false;
        }

        /// <summary>
        /// 初始化焊接头（激光器握手、温度模块枚举、IO 映射）
        /// </summary>
        public bool Initialize()
        {
            if (IsInitialized)
            {
                Log("焊接头已初始化，跳过");
                return true;
            }

            // 统一初始化：连接(激光握手) + 配置(IO映射) + 复位 + 状态信号(温度/激光就绪) 全过才就绪
            bool ok = InitializeLaserCommunication();
            if (ok)
            {
                InitializeIOMapping();
                bool tempOk = InitializeTemperatureModules();
                bool notOverTemp = CurrentTemperature == null || !CurrentTemperature.IsOverThreshold;
                ok = tempOk && IsLaserReady && notOverTemp;
            }

            if (ok)
            {
                IsInitialized = true;
                StartTemperaturePolling();
                Log("焊接头初始化完成");
            }
            else
            {
                Log("焊接头初始化失败", MessageLevel.Error);
            }

            return ok;
        }

        /// <summary>
        /// 启动焊接头工作（Standby → Working）。
        /// 须在 Initialize() 完成后调用。
        /// </summary>
        public bool StartWorking()
        {
            if (!IsInitialized)
            {
                Log("焊接头未初始化，无法启动工作", MessageLevel.Warning);
                return false;
            }

            bool ok = IsInitialized;
            if (ok)
            {
                EnableLaser();
                Log("焊接头已进入工作状态");
            }
            else
            {
                Log("焊接头状态转换失败，无法工作", MessageLevel.Warning);
            }
            return ok;
        }

        /// <summary>
        /// 停止焊接头工作（Working → Standby）。
        /// 关闭激光器但保持设备初始化状态，等待下次 Go 指令。
        /// </summary>
        public void StopWorking()
        {
            DisableLaser();
            Log("焊接头已停止工作，回到待机");
        }

        /// <summary>
        /// 关闭焊接头（彻底下电）
        /// </summary>
        public void Shutdown()
        {
            try
            {
                StopTemperaturePolling();
                DisableLaser();
                IsInitialized = false;
                IsLaserReady = false;
                Log("焊接头已关闭");
            }
            catch (Exception ex)
            {
                Log(string.Format("焊接头关闭异常 原因 {0}", ex.Message), MessageLevel.Error);
            }
        }

        /// <summary>
        /// 读取温度数据
        /// </summary>
        public LaserHeadTemperatureData ReadTemperature()
        {
            // 实际实现中从硬件读取温度
            // 当前为框架占位，返回上次缓存值
            return CurrentTemperature;
        }

        /// <summary>
        /// 读取安全 IO 状态
        /// </summary>
        public SafetyIOStatus ReadSafetyIO()
        {
            // 实际实现中从硬件读取 IO 状态
            return CurrentSafetyIO;
        }

        /// <summary>
        /// 设置自动对焦电机位置
        /// </summary>
        public bool SetAutoFocusPosition(double position)
        {
            if (position < 0 || position > _config.AutoFocusMaxTravel)
            {
                Log(string.Format("自动对焦位置超限 {0}", position), MessageLevel.Warning);
                return false;
            }

            try
            {
                // 实际实现中驱动对焦电机
                CurrentAutoFocusPosition.AutoFocusPos = position;
                CurrentAutoFocusPosition.Timestamp = DateTime.Now;

                AutoFocusPositionChanged?.Invoke(this, CurrentAutoFocusPosition);
                Log(string.Format("自动对焦电机位置 {0:F2} mm", position));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("自动对焦设置失败 原因是 {0}", ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// 设置送丝电机位置（力矩控制）
        /// </summary>
        public bool SetWireFeedPosition(double position, double torqueLimit)
        {
            try
            {
                // 实际实现中驱动送丝电机（力矩控制模式）
                CurrentAutoFocusPosition.WireFeedPos = position;
                CurrentAutoFocusPosition.WireFeedTorque = torqueLimit;
                CurrentAutoFocusPosition.Timestamp = DateTime.Now;

                AutoFocusPositionChanged?.Invoke(this, CurrentAutoFocusPosition);
                Log(string.Format("送丝电机位置 {0:F2} mm 力矩限制 {1:F2} N·m", position, torqueLimit));
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("送丝电机设置失败 原因是 {0}", ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// 关闭激光器
        /// </summary>
        public bool DisableLaser()
        {
            try
            {
                // 实际实现中发送关闭激光器指令
                IsLaserReady = false;
                Log("激光器已关闭");
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("关闭激光器失败 原因是 {0}", ex.Message), MessageLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// 打开激光器（允许出光）
        /// </summary>
        public bool EnableLaser()
        {
            try
            {
                // 实际实现中发送打开激光器指令
                IsLaserReady = true;
                Log("激光器已开启");
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("开启激光器失败 原因是 {0}", ex.Message), MessageLevel.Error);
                return false;
            }
        }

        #region 私有方法

        private bool InitializeLaserCommunication()
        {
            // 实际实现：与激光器建立通讯连接
            Log("激光器通讯握手");
            return true;
        }

        private bool InitializeTemperatureModules()
        {
            // 实际实现：枚举各镜片温度模块
            Log("温度模块枚举");
            return true;
        }

        private bool InitializeIOMapping()
        {
            // 实际实现：映射安全 IO 信号
            Log("IO 映射");
            return true;
        }

        private void StartTemperaturePolling()
        {
            _temperaturePollTimer = new Timer(
                OnTemperaturePoll,
                null,
                _config.TemperaturePollIntervalMs,
                _config.TemperaturePollIntervalMs);
        }

        private void StopTemperaturePolling()
        {
            _temperaturePollTimer?.Dispose();
            _temperaturePollTimer = null;
        }

        private void OnTemperaturePoll(object state)
        {
            try
            {
                // 实际实现中从硬件读取温度
                // 当前为框架占位
                LaserHeadTemperatureData temp = ReadTemperature();

                if (temp != null && temp.IsOverThreshold)
                {
                    TemperatureAlarm?.Invoke(this, temp);
                    Log(string.Format("温度超阈值 数值 {0}", temp.OverThresholdLens), MessageLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("温度轮询异常 原因 {0}", ex.Message), MessageLevel.Error);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown();
                _disposed = true;
            }
        }

        #endregion
    }
}