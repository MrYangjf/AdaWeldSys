using System;
using System.Collections.Generic;
using System.Linq;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.API;
using AdaWeldSystem.MotionControl.ZMotion;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>
    /// 运动控制器管理器（单例）
    /// 统筹所有控制器、轴、IO 的创建与管理。
    /// 
    /// 使用方式：
    ///   MotionManager.Instance.Initialize();  // 初始化所有控制器
    ///   var controller = MotionManager.Instance.DefaultController;  // 获取默认控制器
    ///   var axis = MotionManager.Instance.GetAxis("Y轴");  // 按名称获取轴
    /// </summary>
    public class MotionManager
    {
        private const string Tag = "MotionManager";

        /// <summary>正运动控制器默认 IP（配置文件缺失时使用）</summary>
        private const string DefaultIpAddress = "192.168.0.11";

        private static readonly Lazy<MotionManager> _lazy =
            new Lazy<MotionManager>(() => new MotionManager());

        /// <summary>单例实例</summary>
        public static MotionManager Instance => _lazy.Value;

        /// <summary>所有控制器列表</summary>
        private readonly List<IMotionController> _controllers = new List<IMotionController>();

        /// <summary>默认控制器（第一个注册的控制器）</summary>
        public IMotionController DefaultController
        {
            get
            {
                lock (_controllers)
                {
                    return _controllers.FirstOrDefault();
                }
            }
        }

        /// <summary>控制器数量</summary>
        public int ControllerCount
        {
            get { lock (_controllers) { return _controllers.Count; } }
        }

        /// <summary>是否已初始化</summary>
        public bool IsInitialized { get; private set; }

        private MotionManager()
        {
            IsInitialized = false;
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 初始化默认控制器。
        /// 默认使用正运动（ZMotion）控制器：通过以太网连接硬件，按配置创建轴（含 Y轴）与 IO。
        /// CAMBOX 跟踪工作流的初始化依赖此控制器句柄，故默认即走正运动路径。
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            // 连接阶段：注册正运动控制器并按配置建轴（ADR-041：原状态机四步模板已由本方法内联承载）
            var mgr = MotionConfigManager.Instance;
            mgr.Load();
            string ip = mgr.Config.IpAddress;
            if (string.IsNullOrWhiteSpace(ip)) ip = DefaultIpAddress;

            Log(string.Format("开始初始化运动管理器 正运动控制器 目标地址 {0}", ip));

            var zmController = new ZMotionController("正运动控制器");
            zmController.SetIpAddress(ip);
            RegisterController(zmController);
            bool ok = zmController.Initialize();

            if (ok)
            {
                IsInitialized = true;
                Log("运动管理器初始化完成 正运动控制器");
            }
            else
            {
                Log("运动管理器初始化失败 原因是正运动控制器未连接或初始化未成功", MessageLevel.Warning);
            }
        }

        /// <summary>
        /// 使用正运动（ZMotion）控制器初始化。
        /// 通过以太网连接正运动控制器，自动注册4个轴和默认IO。
        /// 如果已初始化，会先关闭再重新初始化。
        /// </summary>
        /// <param name="ipAddress">控制器 IP 地址</param>
        /// <returns>是否初始化成功</returns>
        public bool InitializeWithZMotion(string ipAddress)
        {
            if (IsInitialized)
            {
                ShutdownAll();
            }

            Log(string.Format("开始初始化正运动控制器 目标地址 {0}", ipAddress));

            var zmController = new ZMotionController("正运动控制器");
            zmController.SetIpAddress(ipAddress);

            RegisterController(zmController);
            bool success = zmController.Initialize();

            if (success)
            {
                IsInitialized = true;
                Log("正运动控制器初始化完成");
            }
            else
            {
                Log("正运动控制器连接失败 运动管理器未就绪", MessageLevel.Error);
                lock (_controllers) { _controllers.Clear(); }
            }

            return success;
        }

        /// <summary>
        /// 注册控制器到管理器
        /// </summary>
        public void RegisterController(IMotionController controller)
        {
            if (controller == null) return;
            lock (_controllers)
            {
                if (!_controllers.Any(c => c.Name == controller.Name))
                {
                    _controllers.Add(controller);
                    Log(string.Format("已注册控制器 {0}", controller.Name));
                }
            }
        }

        /// <summary>
        /// 按名称获取控制器
        /// </summary>
        public IMotionController GetController(string name)
        {
            lock (_controllers)
            {
                return _controllers.FirstOrDefault(c => c.Name == name);
            }
        }

        /// <summary>
        /// 按名称获取轴（遍历所有控制器）
        /// </summary>
        public IAxis GetAxis(string axisName)
        {
            lock (_controllers)
            {
                foreach (var controller in _controllers)
                {
                    var axis = controller.GetAxisByName(axisName);
                    if (axis != null) return axis;
                }
            }
            return null;
        }

        /// <summary>
        /// 按名称获取输入IO（遍历所有控制器）
        /// </summary>
        public IInputIO GetInputIO(string ioName)
        {
            lock (_controllers)
            {
                foreach (var controller in _controllers)
                {
                    foreach (var input in controller.GetInputs())
                    {
                        if (input.IOName == ioName)
                            return input as IInputIO;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 按名称获取输出IO（遍历所有控制器）
        /// </summary>
        public IOutputIO GetOutputIO(string ioName)
        {
            lock (_controllers)
            {
                foreach (var controller in _controllers)
                {
                    foreach (var output in controller.GetOutputs())
                    {
                        if (output.IOName == ioName)
                            return output as IOutputIO;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取所有轴列表（遍历所有控制器）
        /// </summary>
        public List<IAxis> GetAllAxes()
        {
            var result = new List<IAxis>();
            lock (_controllers)
            {
                foreach (var controller in _controllers)
                {
                    result.AddRange(controller.GetAxes());
                }
            }
            return result;
        }

        /// <summary>
        /// 设置全局急停（传播到所有控制器）
        /// </summary>
        public void SetGlobalEmergencyStop(bool isEmergencyStop)
        {
            lock (_controllers)
            {
                foreach (var controller in _controllers)
                {
                    controller.SetEmergencyStop(isEmergencyStop);
                }
            }
            Log(isEmergencyStop ? "全局急停已触发" : "全局急停已解除");
        }

        /// <summary>
        /// 关闭所有控制器
        /// </summary>
        public void ShutdownAll()
        {
            SetGlobalEmergencyStop(true);
            lock (_controllers)
            {
                foreach (var controller in _controllers)
                {
                    controller.Close();
                }
                _controllers.Clear();
            }
            IsInitialized = false;
            Log("所有控制器已关闭");
        }
    }
}