using System;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>运动管理器</summary>
    /// <remarks>
    /// 面向抽象层编程，厂商具体类型仅在初始化时出现一次。
    /// 轴、IO、气缸统一由三张注册表承载，初始化成功后由注册表把对象登记进控制器。
    /// </remarks>
    public class MontionManager
    {
        #region 私有变量

        private const string Tag = "运动管理器";

        private static readonly Lazy<MontionManager> _lazy =
            new Lazy<MontionManager>(() => new MontionManager());

        #endregion

        #region 公共变量

        /// <summary>单例实例</summary>
        public static MontionManager Instance { get { return _lazy.Value; } }

        /// <summary>当前控制器，未初始化时为 null</summary>
        public MontionControl MontionControl { get; private set; }

        /// <summary>是否已初始化</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>运控数据，标定参数与轴 IO 配置的唯一对外入口</summary>
        public MoveControlData MoveData { get; private set; }

        /// <summary>轴注册表</summary>
        public AxisRegistry Axes { get; private set; }

        /// <summary>IO 注册表</summary>
        public IORegistry IOs { get; private set; }

        /// <summary>气缸注册表</summary>
        public CylinderRegistry Cylinders { get; private set; }

        #endregion

        #region 构造函数

        private MontionManager()
        {
            Axes = new AxisRegistry();
            IOs = new IORegistry();
            Cylinders = new CylinderRegistry();

            MoveData = new MoveControlData();
            MoveData.Load();
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>按配置表创建全部轴并登记进注册表</summary>
        /// <remarks>轴对象在构造时已自注册进控制器，此处只补注册表并套用配置参数。</remarks>
        private void CreateAxesFromConfig()
        {
            DeltaMotionConfig config = MoveData.MotionConfig;
            for (int i = 0; i < config.Axes.Count; i++)
            {
                DeltaAxisConfig cfg = config.Axes[i];
                if (!cfg.Enabled) continue;

                MontionAxis axis = new DeltaMontionAxis(MontionControl, cfg.AxisName, cfg.NodeId, cfg.SlotNo);
                ApplyAxisConfig(axis, cfg);
                Axes.Register(cfg.AxisName, axis);
            }
            Log(string.Format("按配置创建轴 {0} 个", Axes.Count));
        }

        /// <summary>把轴配置参数套用到轴对象</summary>
        /// <param name="axis">轴对象</param>
        /// <param name="cfg">轴配置</param>
        private static void ApplyAxisConfig(MontionAxis axis, DeltaAxisConfig cfg)
        {
            axis.Equiv = cfg.Equiv;
            axis.MinVel = cfg.MinVel;
            axis.MaxVel = cfg.MaxVel;
            axis.AccTime = cfg.AccTime;
            axis.DecTime = cfg.DecTime;
            axis.STime = cfg.STime;
            axis.StopVel = cfg.StopVel;
            axis.HomeMode = cfg.HomeMode;
            axis.HomeDir = cfg.HomeDir;
            axis.HomeMaxVel = cfg.HomeMaxVel;
        }

        /// <summary>按配置表创建全部输入与输出并登记进注册表</summary>
        /// <remarks>IO 对象在构造时已自注册进控制器，此处只补注册表并套用位号与启用标志。</remarks>
        private void CreateIOsFromConfig()
        {
            DeltaMotionConfig config = MoveData.MotionConfig;

            for (int i = 0; i < config.Inputs.Count; i++)
            {
                DeltaIOConfig cfg = config.Inputs[i];
                if (!cfg.Enabled) continue;

                InputIO io = new DeltaInputIO(MontionControl, cfg.IOName, cfg.NodeId, cfg.SlotNo);
                io.IONo = cfg.BitNo;
                io.IOEnable = cfg.Enabled;
                IOs.RegisterInput(cfg.IOName, io);
            }

            for (int i = 0; i < config.Outputs.Count; i++)
            {
                DeltaIOConfig cfg = config.Outputs[i];
                if (!cfg.Enabled) continue;

                OutputIO io = new DeltaOutputIO(MontionControl, cfg.IOName, cfg.NodeId, cfg.SlotNo);
                io.IONo = cfg.BitNo;
                io.IOEnable = cfg.Enabled;
                IOs.RegisterOutput(cfg.IOName, io);
            }

            Log(string.Format("按配置创建输入 {0} 个 输出 {1} 个", IOs.InputCount, IOs.OutputCount));
        }

        #endregion

        #region 公共函数

        /// <summary>初始化控制器并把已登记对象注册进控制器</summary>
        /// <param name="devName">设备名称</param>
        /// <param name="msg">失败时回写的错误描述</param>
        /// <returns>初始化成功返回 true</returns>
        public bool Initialization(string devName, ref string msg)
        {
            Log("开始初始化运动管理器");

            MoveData.Load();

            MontionControl = new DeltaMontionControl(devName);
            if (!MontionControl.Initialization(ref msg))
            {
                // 根因已由 DeltaMontionControl 记录（ADR-031 D1 报错三层归口），此处不复述
                return false;
            }

            CreateAxesFromConfig();
            CreateIOsFromConfig();

            IsInitialized = true;
            AttachAll();
            Log(string.Format("运动管理器初始化完成 轴 {0} 个 输入 {1} 个 输出 {2} 个 气缸 {3} 个",
                Axes.Count, IOs.InputCount, IOs.OutputCount, Cylinders.Count));
            return true;
        }

        /// <summary>把三张注册表的全部对象注册进当前控制器</summary>
        public void AttachAll()
        {
            Axes.AttachAll(MontionControl);
            IOs.AttachAll(MontionControl);
            Cylinders.AttachAll(MontionControl);
        }

        /// <summary>关闭控制器并清空注册表</summary>
        public void Close()
        {
            if (MontionControl != null)
            {
                MontionControl.SetEmergencyStop(true);
                MontionControl.Close();
                MontionControl = null;
            }

            Axes.Clear();
            IOs.Clear();
            Cylinders.Clear();
            IsInitialized = false;
            Log("运动管理器已关闭");
        }

        /// <summary>注册轴</summary>
        /// <param name="name">轴键名</param>
        /// <param name="axis">轴对象</param>
        public void AddAxis(string name, MontionAxis axis)
        {
            Axes.Register(name, axis);
            if (MontionControl != null) MontionControl.AddAxis(axis);
        }

        /// <summary>取轴</summary>
        /// <param name="name">轴键名</param>
        /// <returns>轴对象，未找到返回 null</returns>
        public MontionAxis GetAxis(string name)
        {
            return Axes.Get(name);
        }

        /// <summary>注册输入</summary>
        /// <param name="name">输入键名</param>
        /// <param name="io">输入对象</param>
        public void AddInputIO(string name, InputIO io)
        {
            IOs.RegisterInput(name, io);
            if (MontionControl != null) MontionControl.AddIO(io);
        }

        /// <summary>取输入</summary>
        /// <param name="name">输入键名</param>
        /// <returns>输入对象，未找到返回 null</returns>
        public InputIO GetInputIO(string name)
        {
            return IOs.GetInput(name);
        }

        /// <summary>注册输出</summary>
        /// <param name="name">输出键名</param>
        /// <param name="io">输出对象</param>
        public void AddOutputIO(string name, OutputIO io)
        {
            IOs.RegisterOutput(name, io);
            if (MontionControl != null) MontionControl.AddIO(io);
        }

        /// <summary>取输出</summary>
        /// <param name="name">输出键名</param>
        /// <returns>输出对象，未找到返回 null</returns>
        public OutputIO GetOutputIO(string name)
        {
            return IOs.GetOutput(name);
        }

        /// <summary>注册气缸，同时把其占用的 IO 登记进控制器</summary>
        /// <param name="name">气缸键名</param>
        /// <param name="cylinder">气缸对象</param>
        public void AddCylinder(string name, Cylinder cylinder)
        {
            Cylinders.Register(name, cylinder);
            if (MontionControl != null) RegisterCylinderIO(cylinder, MontionControl);
        }

        /// <summary>取气缸</summary>
        /// <param name="name">气缸键名</param>
        /// <returns>气缸对象，未找到返回 null</returns>
        public Cylinder GetCylinder(string name)
        {
            return Cylinders.Get(name);
        }

        /// <summary>把单个气缸占用的全部 IO 注册进控制器</summary>
        /// <param name="cylinder">气缸对象</param>
        /// <param name="control">目标控制器</param>
        public void RegisterCylinderIO(Cylinder cylinder, MontionControl control)
        {
            if (cylinder == null || control == null) return;

            foreach (MontionIO io in cylinder.GetAllIO())
            {
                control.AddIO(io);
            }
        }

        /// <summary>全部轴伺服使能</summary>
        public void ServoOnAll()
        {
            foreach (MontionAxis axis in Axes.GetAll())
            {
                axis.ServoOn();
            }
            Log("全部轴已伺服使能");
        }

        /// <summary>全部轴伺服下电</summary>
        public void ServoOffAll()
        {
            foreach (MontionAxis axis in Axes.GetAll())
            {
                axis.ServoOff();
            }
            Log("全部轴已伺服下电");
        }

        /// <summary>全部轴回零</summary>
        /// <returns>全部成功返回 true</returns>
        public bool HomeAll()
        {
            bool allOk = true;
            foreach (MontionAxis axis in Axes.GetAll())
            {
                if (!axis.Home()) allOk = false;
            }
            Log(allOk ? "全部轴回零完成" : "部分轴回零失败", allOk ? MessageLevel.Info : MessageLevel.Error);
            return allOk;
        }

        /// <summary>设置全局急停</summary>
        /// <param name="isEmergencyStop">true 为急停</param>
        public void SetEmergencyStop(bool isEmergencyStop)
        {
            if (MontionControl != null) MontionControl.SetEmergencyStop(isEmergencyStop);
        }

        #endregion
    }
}
