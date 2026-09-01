using System;
using System.Collections.Generic;
using System.Linq;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.ZMotion;

namespace AdaWeldSystem.MotionControl.API
{
    /// <summary>
    /// 运动控制器抽象基类
    /// 管理轴列表和IO列表，提供统一的控制器生命周期管理。
    /// 参考 PcomDeviceInterface 的 MontionControl 设计模式。
    /// 
    /// 子类需实现：
    /// - Initialize() / Close() — 硬件连接/断开
    /// - SetEmergencyStop() — 急停传播（遍历轴和IO）
    /// </summary>
    public abstract class MotionControllerBase : IMotionController
    {
        private const string Tag = "MotionController";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>控制器名称</summary>
        public string Name { get; protected set; }

        /// <summary>控制器是否已连接</summary>
        public bool IsConnected { get; protected set; }

        /// <summary>急停标志</summary>
        public bool IsEmergencyStop { get; protected set; }

        /// <summary>轴列表</summary>
        protected readonly List<IAxis> _axes = new List<IAxis>();

        /// <summary>输入IO列表</summary>
        protected readonly List<IIO> _inputIOs = new List<IIO>();

        /// <summary>输出IO列表</summary>
        protected readonly List<IIO> _outputIOs = new List<IIO>();

        /// <summary>轴数量</summary>
        public int AxisCount
        {
            get { lock (_axes) { return _axes.Count; } }
        }

        /// <summary>输入IO数量</summary>
        public int InputCount
        {
            get { lock (_inputIOs) { return _inputIOs.Count; } }
        }

        /// <summary>输出IO数量</summary>
        public int OutputCount
        {
            get { lock (_outputIOs) { return _outputIOs.Count; } }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">控制器名称</param>
        protected MotionControllerBase(string name)
        {
            Name = name ?? "MotionController";
            IsConnected = false;
            IsEmergencyStop = false;
        }

        // ===== 抽象方法（子类必须实现） =====

        /// <summary>初始化控制器连接（硬件握手）</summary>
        public abstract bool Initialize();

        /// <summary>关闭控制器连接</summary>
        public abstract void Close();

        /// <summary>设置急停，传播到所有轴和输出IO</summary>
        public abstract void SetEmergencyStop(bool isEmergencyStop);

        // ===== 轴管理 =====

        /// <summary>添加轴到控制器</summary>
        public void AddAxis(IAxis axis)
        {
            if (axis == null) return;
            lock (_axes)
            {
                if (!_axes.Any(a => a.AxisName == axis.AxisName))
                {
                    _axes.Add(axis);
                    Log(string.Format("添加轴 {0} 编号 {1}", axis.AxisName, axis.AxisNumber));
                }
            }
        }

        /// <summary>从控制器移除轴</summary>
        public void RemoveAxis(IAxis axis)
        {
            if (axis == null) return;
            lock (_axes)
            {
                _axes.RemoveAll(a => a.AxisName == axis.AxisName);
            }
        }

        /// <summary>获取所有轴列表</summary>
        public IReadOnlyList<IAxis> GetAxes()
        {
            lock (_axes)
            {
                return _axes.ToList();
            }
        }

        /// <summary>按轴号获取轴</summary>
        public IAxis GetAxis(ushort axisNumber)
        {
            lock (_axes)
            {
                return _axes.FirstOrDefault(a => a.AxisNumber == axisNumber);
            }
        }

        /// <summary>按名称获取轴</summary>
        public IAxis GetAxisByName(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) return null;
            lock (_axes)
            {
                return _axes.FirstOrDefault(a => a.AxisName == axisName);
            }
        }

        // ===== IO管理 =====

        /// <summary>添加IO到控制器（自动区分输入/输出）</summary>
        public void AddIO(IIO io)
        {
            if (io == null) return;
            if (io.GetIOType() == IOType.Input)
            {
                lock (_inputIOs)
                {
                    if (!_inputIOs.Any(i => i.IOName == io.IOName))
                    {
                        _inputIOs.Add(io);
                    }
                }
            }
            else
            {
                lock (_outputIOs)
                {
                    if (!_outputIOs.Any(o => o.IOName == io.IOName))
                    {
                        _outputIOs.Add(io);
                    }
                }
            }
        }

        /// <summary>从控制器移除IO</summary>
        public void RemoveIO(IIO io)
        {
            if (io == null) return;
            if (io.GetIOType() == IOType.Input)
            {
                lock (_inputIOs) { _inputIOs.RemoveAll(i => i.IOName == io.IOName); }
            }
            else
            {
                lock (_outputIOs) { _outputIOs.RemoveAll(o => o.IOName == io.IOName); }
            }
        }

        /// <summary>清除所有IO</summary>
        public void ClearIO()
        {
            lock (_inputIOs) { _inputIOs.Clear(); }
            lock (_outputIOs) { _outputIOs.Clear(); }
        }

        /// <summary>获取所有输入IO列表</summary>
        public IReadOnlyList<IIO> GetInputs()
        {
            lock (_inputIOs) { return _inputIOs.ToList(); }
        }

        /// <summary>获取所有输出IO列表</summary>
        public IReadOnlyList<IIO> GetOutputs()
        {
            lock (_outputIOs) { return _outputIOs.ToList(); }
        }

        /// <summary>按IO号获取输入IO</summary>
        public IInputIO GetInput(ushort ioNumber)
        {
            lock (_inputIOs)
            {
                return _inputIOs.FirstOrDefault(i => i.IONumber == ioNumber) as IInputIO;
            }
        }

        /// <summary>按IO号获取输出IO</summary>
        public IOutputIO GetOutput(ushort ioNumber)
        {
            lock (_outputIOs)
            {
                return _outputIOs.FirstOrDefault(o => o.IONumber == ioNumber) as IOutputIO;
            }
        }

        // ===== 心跳检测（默认空实现，子类可重写） =====

        /// <summary>心跳状态变化事件</summary>
        public virtual event HeartbeatEventHandler HeartbeatStateChanged;

        /// <summary>启动心跳检测（默认空实现）</summary>
        public virtual bool StartHeartbeat(int intervalMs, int timeoutMs)
        {
            return false;
        }

        /// <summary>停止心跳检测（默认空实现）</summary>
        public virtual void StopHeartbeat()
        {
        }

        /// <summary>手动执行一次心跳检测（默认空实现）</summary>
        public virtual bool CheckHeartbeat()
        {
            return IsConnected;
        }

        // ===== 连续插补（默认空实现，子类可重写） =====

        /// <summary>创建连续插补引擎（默认空实现）</summary>
        public virtual InterpolationEngine CreateInterpolationEngine(int mainAxis, int[] axisList)
        {
            return null;
        }

        // ===== 电子凸轮（默认空实现，子类可重写） =====

        /// <summary>写入凸轮曲线到TABLE寄存器（默认空实现）</summary>
        public virtual bool WriteCamTable(int tableStart, float[] camPoints)
        {
            return false;
        }

        /// <summary>启动电子凸轮（CAMBOX）（默认空实现）</summary>
        public virtual bool StartCambox(int slaveAxis, int masterAxis, int tableStart, int tableEnd,
            float slaveCycle, float masterCycle, int direction, int mode)
        {
            return false;
        }

        /// <summary>停止电子凸轮（默认空实现）</summary>
        public virtual bool StopCambox(int slaveAxis, bool immediate)
        {
            return false;
        }

        /// <summary>追剪位置同步（Movelink）（默认空实现）</summary>
        public virtual bool Movelink(int slaveAxis, int masterAxis, float distance,
            float speed, float accel, float decel, float masterPos, int mode)
        {
            return false;
        }

        /// <summary>追剪速度同步（Moveslink）（默认空实现）</summary>
        public virtual bool Moveslink(int slaveAxis, int masterAxis, float distance,
            float speed, float startRatio, float endRatio, float masterPos, int mode)
        {
            return false;
        }

        // ===== 主动上报（默认空实现，子类可重写） =====

        /// <summary>主动上报数据接收事件</summary>
        public virtual event AutoReportEventHandler AutoReportReceived;

        /// <summary>启用主动上报（默认空实现）</summary>
        public virtual bool EnableAutoReport(string reportItems, int intervalMs)
        {
            return false;
        }

        /// <summary>禁用主动上报（默认空实现）</summary>
        public virtual bool DisableAutoReport()
        {
            return false;
        }

        // ===== 周期上报（默认空实现，子类可重写） =====

        /// <summary>周期上报数据接收事件</summary>
        public virtual event CycleReportEventHandler CycleReportReceived;

        /// <summary>启用周期上报（默认空实现）</summary>
        public virtual bool EnableCycleReport(uint channel, uint cycleMs, string paramString)
        {
            return false;
        }

        /// <summary>禁用周期上报（默认空实现）</summary>
        public virtual bool DisableCycleReport(uint channel)
        {
            return false;
        }

        /// <summary>强制触发一次周期上报（默认空实现）</summary>
        public virtual bool ForceCycleReportOnce(uint channel)
        {
            return false;
        }

        // ===== 配置管理（默认空实现，子类可重写） =====

        /// <summary>从配置加载轴和IO（默认空实现）</summary>
        public virtual bool LoadFromConfig()
        {
            return false;
        }

        /// <summary>保存当前轴和IO配置（默认空实现）</summary>
        public virtual bool SaveToConfig()
        {
            return false;
        }
    }
}