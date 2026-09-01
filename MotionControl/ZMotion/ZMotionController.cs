using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.API;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 正运动（ZMotion）运动控制器实现
    /// 继承 MotionControllerBase，封装正运动控制器的连接、轴管理、IO管理。
    /// 
    /// 支持的高级运动功能：
    ///   - 板卡连接、单轴运动、回零运动（基础功能）
    ///   - 连续插补（小线段批量运动）
    ///   - 主动上报（回调方式）
    ///   - 周期上报（轮询方式）
    ///   - 心跳检测（连接保活）
    ///   - PTPVT 自定义曲线运动
    ///   - 电子凸轮（CAMBOX + 追剪同步）
    ///   - 轴字典/IO字典 + 配置持久化
    /// 
    /// 4轴定义（默认配置，可通过配置文件修改）：
    ///   轴0 - Y轴（线激光焊缝焊接点引导）
    ///   轴1 - 振镜轴（线激光焊缝宽度设置摆幅和位置）
    ///   轴2 - 送丝伸缩臂轴（外部压力信号控制）
    ///   轴3 - Z轴自动对焦轴（送丝臂位置传感器控制）
    /// 非 UI 类，实现标准 IDisposable.Dispose() 释放线程与原生句柄。
    /// </summary>
    public class ZMotionController : MotionControllerBase, IDisposable
    {
        private const string Tag = "ZMotionController";

        /// <summary>控制器连接句柄</summary>
        private IntPtr _handle;

        /// <summary>控制器IP地址</summary>
        private string _ipAddress;

        // ===== 心跳检测 =====
        private Thread _heartbeatThread;
        private volatile bool _heartbeatRunning;
        private int _heartbeatIntervalMs;
        private int _heartbeatTimeoutMs;
        private DateTime _lastHeartbeatTime;

        // ===== 主动上报 =====
        private ZMotionNative.ZAuxCallBack _autoReportCallback;
        private GCHandle _autoReportHandle;
        private volatile bool _autoReportEnabled;

        // ===== 周期上报 =====
        private Thread _cycleReportThread;
        private volatile bool _cycleReportRunning;
        private uint _cycleReportChannel;
        private string _cycleReportParams;

        // ===== 振镜控制 =====
        private ZMotionGalvo _galvo;

        // ===== 事件 =====
        public event HeartbeatEventHandler HeartbeatStateChanged;
        public event AutoReportEventHandler AutoReportReceived;
        public event CycleReportEventHandler CycleReportReceived;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">控制器名称</param>
        public ZMotionController(string name)
            : base(name)
        {
            _handle = IntPtr.Zero;
            _ipAddress = "192.168.0.11"; // 默认IP
            _galvo = new ZMotionGalvo(4, 5); // 默认振镜轴 4/5
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 初始化控制器连接
        /// 通过以太网连接正运动控制器，并初始化4个轴和默认IO。
        /// </summary>
        public override bool Initialize()
        {
            if (IsConnected)
            {
                Log("控制器已连接，无需重复初始化");
                return true;
            }

            Log(string.Format("正在连接正运动控制器 {0}", _ipAddress));

            int ret = ZMotionNative.ZAux_OpenEth(_ipAddress, out _handle);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "连接失败 {0} 错误码 {1}",
                    ZMotionNative.GetErrorDescription(ret), ret));
                _handle = IntPtr.Zero;
                IsConnected = false;
                return false;
            }

            IsConnected = true;
            Log("控制器连接成功");

            InitializeAxes();
            InitializeIOs();
            InitializeGalvo();

            Log(string.Format(
                "初始化完成 {0}个轴 {1}个输入IO {2}个输出IO",
                AxisCount, InputCount, OutputCount));

            return true;
        }

        /// <summary>
        /// 关闭控制器连接
        /// </summary>
        public override void Close()
        {
            if (!IsConnected || _handle == IntPtr.Zero) return;

            lock (_axes)
            {
                foreach (var axis in _axes)
                {
                    try
                    {
                        axis.Stop(true);
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format(
                            "停止轴 {0} 时异常 {1}", axis.AxisName, ex.Message));
                    }
                }
            }

            lock (_outputIOs)
            {
                foreach (var io in _outputIOs)
                {
                    try
                    {
                        if (io is IOutputIO outputIO)
                        {
                            outputIO.SetOFF();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format(
                            "关闭IO {0} 时异常 {1}", io.IOName, ex.Message));
                    }
                }
            }

            int ret = ZMotionNative.ZAux_Close(_handle);
            if (ZMotionNative.IsSuccess(ret))
            {
                Log("控制器连接已关闭");
            }
            else
            {
                Log(string.Format(
                    "关闭连接时出错 {0}", ZMotionNative.GetErrorDescription(ret)));
            }

            _handle = IntPtr.Zero;
            IsConnected = false;

            lock (_axes) { _axes.Clear(); }
            ClearIO();
        }

        /// <summary>
        /// 设置急停
        /// 触发所有轴停止，并关闭标记为急停自动关闭的输出IO。
        /// </summary>
        public override void SetEmergencyStop(bool isEmergencyStop)
        {
            IsEmergencyStop = isEmergencyStop;

            string action = isEmergencyStop ? "触发急停" : "解除急停";
            Log(action);

            // 传播到所有轴
            lock (_axes)
            {
                foreach (var axis in _axes)
                {
                    try
                    {
                        axis.Stop(isEmergencyStop); // 急停时立即停止
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format(
                            "轴 {0} 急停操作异常 原因 {1}", axis.AxisName, ex.Message));
                    }
                }
            }

            // 传播到输出IO
            lock (_outputIOs)
            {
                foreach (var io in _outputIOs)
                {
                    try
                    {
                        if (io is IOutputIO outputIO && outputIO.EmergencyStopOff)
                        {
                            if (isEmergencyStop)
                            {
                                outputIO.SetOFF();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format(
                            "IO {0} 急停操作异常 原因 {1}", io.IOName, ex.Message));
                    }
                }
            }
        }

        // ============================================================
        // 内部方法
        // ============================================================

        /// <summary>
        /// 设置控制器IP地址（需在 Initialize() 前调用）
        /// </summary>
        public void SetIpAddress(string ip)
        {
            _ipAddress = ip;
        }

        /// <summary>
        /// 获取当前连接句柄（内部使用）
        /// </summary>
        internal IntPtr GetHandle()
        {
            return _handle;
        }

        /// <summary>
        /// 停止后台线程：等待其结束，超时则中止，并将引用置空（两处上报线程共用）
        /// </summary>
        private static void StopReportThread(ref Thread thread)
        {
            if (thread != null && thread.IsAlive)
            {
                if (!thread.Join(2000))
                {
                    thread.Abort();
                }
                thread = null;
            }
        }

        /// <summary>
        /// 执行BASIC命令（高级功能，用于发送自定义指令）
        /// </summary>
        /// <param name="command">BASIC命令字符串</param>
        /// <param name="response">返回的响应字符串</param>
        /// <returns>是否成功</returns>
        public bool ExecuteCommand(string command, out string response)
        {
            response = string.Empty;
            if (_handle == IntPtr.Zero) return false;

            var sb = new StringBuilder(1024);
            int ret = ZMotionNative.ZAux_Execute(_handle, command, sb, 1024);
            if (ZMotionNative.IsSuccess(ret))
            {
                response = sb.ToString();
                return true;
            }
            return false;
        }

        // ============================================================
        // 心跳检测
        // ============================================================

        /// <summary>
        /// 启动心跳检测
        /// </summary>
        public bool StartHeartbeat(int intervalMs, int timeoutMs)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("启动心跳失败 原因是未连接");
                return false;
            }
            if (_heartbeatRunning)
            {
                Log("心跳检测已在运行");
                return true;
            }

            _heartbeatIntervalMs = intervalMs;
            _heartbeatTimeoutMs = timeoutMs;
            _heartbeatRunning = true;
            _lastHeartbeatTime = DateTime.Now;

            _heartbeatThread = new Thread(HeartbeatThreadProc);
            _heartbeatThread.IsBackground = true;
            _heartbeatThread.Name = "ZMotionHeartbeat";
            _heartbeatThread.Start();

            Log(string.Format(
                "心跳检测已启动 间隔毫秒 {0} 超时毫秒 {1}", intervalMs, timeoutMs));

            return true;
        }

        /// <summary>
        /// 停止心跳检测
        /// </summary>
        public void StopHeartbeat()
        {
            _heartbeatRunning = false;

            StopReportThread(ref _heartbeatThread);

            Log("心跳检测已停止");
        }

        /// <summary>
        /// 手动执行一次心跳检测
        /// </summary>
        public bool CheckHeartbeat()
        {
            if (_handle == IntPtr.Zero) return false;

            var sb = new StringBuilder(256);
            int ret = ZMotionNative.ZAux_GetControllerInfo(_handle, sb, 256);
            bool connected = ZMotionNative.IsSuccess(ret) && sb.ToString() != "no reply";

            if (connected)
            {
                _lastHeartbeatTime = DateTime.Now;
            }

            return connected;
        }

        /// <summary>
        /// 心跳检测线程
        /// </summary>
        private void HeartbeatThreadProc()
        {
            try
            {
                while (_heartbeatRunning)
                {
                    Thread.Sleep(_heartbeatIntervalMs);

                    if (_handle == IntPtr.Zero) continue;

                    var sb = new StringBuilder(256);
                    int ret = ZMotionNative.ZAux_GetControllerInfo(_handle, sb, 256);
                    bool connected = ZMotionNative.IsSuccess(ret) && sb.ToString() != "no reply";

                    if (connected)
                    {
                        _lastHeartbeatTime = DateTime.Now;
                    }
                    else
                    {
                        // 检查是否超时
                        TimeSpan elapsed = DateTime.Now - _lastHeartbeatTime;
                        if (elapsed.TotalMilliseconds >= _heartbeatTimeoutMs)
                        {
                            // 触发断连事件
                            IsConnected = false;
                            var handler = HeartbeatStateChanged;
                            if (handler != null)
                            {
                                var args = new HeartbeatEventArgs
                                {
                                    IsConnected = false,
                                    ErrorMessage = "心跳超时，控制器连接断开"
                                };
                                handler(this, args);
                            }
                            Log("心跳超时，控制器连接断开");
                            _heartbeatRunning = false;
                            break;
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // 正常中止
            }
            catch (Exception ex)
            {
                Log(string.Format("心跳线程异常 原因 {0}", ex.Message));
            }
        }

        // ============================================================
        // 连续插补
        // ============================================================

        /// <summary>
        /// 创建连续插补引擎
        /// </summary>
        public InterpolationEngine CreateInterpolationEngine(int mainAxis, int[] axisList)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("创建插补引擎失败 原因是未连接");
                return null;
            }

            var engine = new InterpolationEngine(_handle, mainAxis, axisList);
            Log(string.Format(
                "创建插补引擎 主轴 {0} 轴数 {1}", mainAxis, axisList != null ? axisList.Length : 0));

            return engine;
        }

        // ============================================================
        // 电子凸轮（CAMBOX + 追剪）
        // ============================================================

        /// <summary>
        /// 写入凸轮曲线到TABLE寄存器
        /// </summary>
        public bool WriteCamTable(int tableStart, float[] camPoints)
        {
            if (_handle == IntPtr.Zero || camPoints == null || camPoints.Length == 0)
            {
                Log("写入凸轮表失败 原因是参数无效");
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_SetTable(
                _handle, tableStart, camPoints.Length, camPoints);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "凸轮表写入成功 起始位置 {0} 点数 {1}", tableStart, camPoints.Length));
                return true;
            }

            Log(string.Format(
                "凸轮表写入失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// 启动电子凸轮（CAMBOX）
        /// </summary>
        public bool StartCambox(int slaveAxis, int masterAxis, int tableStart, int tableEnd,
            float slaveCycle, float masterCycle, int direction, int mode)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("启动CAMBOX失败 原因是未连接");
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_Cambox(
                _handle, slaveAxis, tableStart, tableEnd,
                slaveCycle, masterCycle, masterAxis, direction, mode);

            if (ZMotionNative.IsSuccess(ret))
            {
                // 触发运动
                ZMotionNative.ZAux_Trigger(_handle, slaveAxis);

                Log(string.Format(
                    "电子凸轮启动 从轴 {0} 主轴 {1} 表范围起点 {2} 终点 {3} 模式 {4}",
                    slaveAxis, masterAxis, tableStart, tableEnd, mode));
                return true;
            }

            Log(string.Format(
                "电子凸轮启动失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// 停止电子凸轮
        /// </summary>
        public bool StopCambox(int slaveAxis, bool immediate)
        {
            if (_handle == IntPtr.Zero) return false;

            int mode = immediate ? 1 : 0;
            int ret = ZMotionNative.ZAux_Direct_CamStop(_handle, slaveAxis, mode);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "电子凸轮停止 从轴 {0} 模式 {1}", slaveAxis, immediate ? "急停" : "减速停止"));
                return true;
            }

            Log(string.Format(
                "电子凸轮停止失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// 追剪位置同步（Movelink）
        /// </summary>
        public bool Movelink(int slaveAxis, int masterAxis, float distance,
            float speed, float accel, float decel, float masterPos, int mode)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("Movelink失败 原因是未连接");
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_Movelink(
                _handle, slaveAxis, distance, speed, accel, decel,
                masterAxis, masterPos, mode);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "Movelink位置同步 从轴 {0} 主轴 {1} 距离毫米 {2:F1}",
                    slaveAxis, masterAxis, distance));
                return true;
            }

            Log(string.Format(
                "Movelink失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// 追剪速度同步（Moveslink）
        /// </summary>
        public bool Moveslink(int slaveAxis, int masterAxis, float distance,
            float speed, float startRatio, float endRatio, float masterPos, int mode)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("Moveslink失败 原因是未连接");
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_Moveslink(
                _handle, slaveAxis, distance, speed, startRatio, endRatio,
                masterAxis, masterPos, mode);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "Moveslink速度同步 从轴 {0} 主轴 {1} 距离毫米 {2:F1}",
                    slaveAxis, masterAxis, distance));
                return true;
            }

            Log(string.Format(
                "Moveslink失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        // ============================================================
        // 主动上报
        // ============================================================

        /// <summary>
        /// 启用主动上报
        /// 注意：需要控制器端有对应的BAS主动上报程序。
        /// </summary>
        public bool EnableAutoReport(string reportItems, int intervalMs)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("启用主动上报失败 原因是未连接");
                return false;
            }
            if (_autoReportEnabled)
            {
                Log("主动上报已启用");
                return true;
            }

            try
            {
                // 注册回调函数（需保持引用，防止GC回收）
                _autoReportCallback = new ZMotionNative.ZAuxCallBack(AutoReportCallback);
                _autoReportHandle = GCHandle.Alloc(_autoReportCallback);

                int ret = ZMotionNative.ZAux_SetAutoUpCallBack(_handle, _autoReportCallback);
                if (!ZMotionNative.IsSuccess(ret))
                {
                    Log(string.Format(
                        "设置主动上报回调失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
                    _autoReportHandle.Free();
                    return false;
                }

                // 设置上报内容和间隔（通过BASIC变量）
                if (!string.IsNullOrEmpty(reportItems))
                {
                    ZMotionNative.ZAux_Execute(_handle,
                        "AutoCmdString = \"" + reportItems + "\"",
                        new StringBuilder(64), 64);
                }

                ZMotionNative.ZAux_Direct_SetUserVar(_handle, "AutoUpTime", intervalMs);
                ZMotionNative.ZAux_Direct_SetUserVar(_handle, "If_EnableAutoUp", 1);

                _autoReportEnabled = true;
                Log(string.Format(
                    "主动上报已启用 间隔毫秒 {0}", intervalMs));

                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("启用主动上报异常 原因 {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 禁用主动上报
        /// </summary>
        public bool DisableAutoReport()
        {
            if (_handle == IntPtr.Zero) return false;
            if (!_autoReportEnabled) return true;

            try
            {
                // 禁用上报
                ZMotionNative.ZAux_Direct_SetUserVar(_handle, "If_EnableAutoUp", 0);

                // 清除回调
                ZMotionNative.ZAux_SetAutoUpCallBack(_handle, null);

                if (_autoReportHandle.IsAllocated)
                {
                    _autoReportHandle.Free();
                }

                _autoReportEnabled = false;
                Log("主动上报已禁用");
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("禁用主动上报异常 原因 {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 主动上报回调函数
        /// </summary>
        private void AutoReportCallback(IntPtr handle, int typeCode, int dataLength, StringBuilder data)
        {
            var handler = AutoReportReceived;
            if (handler != null)
            {
                var args = new AutoReportEventArgs
                {
                    TypeCode = typeCode,
                    DataLength = dataLength,
                    Data = data != null ? data.ToString() : string.Empty
                };
                handler(this, args);
            }
        }

        // ============================================================
        // 周期上报
        // ============================================================

        /// <summary>
        /// 启用周期上报
        /// </summary>
        public bool EnableCycleReport(uint channel, uint cycleMs, string paramString)
        {
            if (_handle == IntPtr.Zero)
            {
                Log("启用周期上报失败 原因是未连接");
                return false;
            }
            if (_cycleReportRunning)
            {
                Log("周期上报已在运行");
                return true;
            }

            int ret = ZMotionNative.ZAux_CycleUpEnable(_handle, channel, cycleMs, paramString);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "启用周期上报失败 原因 {0}", ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            _cycleReportChannel = channel;
            _cycleReportParams = paramString;
            _cycleReportRunning = true;

            // 启动轮询线程
            _cycleReportThread = new Thread(CycleReportThreadProc);
            _cycleReportThread.IsBackground = true;
            _cycleReportThread.Name = "ZMotionCycleReport";
            _cycleReportThread.Start();

            Log(string.Format(
                "周期上报已启用 通道号 {0} 周期毫秒 {1} 参数 {2}",
                channel, cycleMs, paramString));

            return true;
        }

        /// <summary>
        /// 禁用周期上报
        /// </summary>
        public bool DisableCycleReport(uint channel)
        {
            _cycleReportRunning = false;

            StopReportThread(ref _cycleReportThread);

            if (_handle != IntPtr.Zero)
            {
                int ret = ZMotionNative.ZAux_CycleUpDisable(_handle, channel);
                if (!ZMotionNative.IsSuccess(ret))
                {
                    Log(string.Format(
                        "禁用周期上报失败 原因 {0}", ZMotionNative.GetErrorDescription(ret)));
                    return false;
                }
            }

            Log("周期上报已禁用");
            return true;
        }

        /// <summary>
        /// 强制触发一次周期上报
        /// </summary>
        public bool ForceCycleReportOnce(uint channel)
        {
            if (_handle == IntPtr.Zero) return false;

            int ret = ZMotionNative.ZAux_CycleUpForceOnce(_handle, channel);
            return ZMotionNative.IsSuccess(ret);
        }

        /// <summary>
        /// 周期上报轮询线程
        /// </summary>
        private void CycleReportThreadProc()
        {
            try
            {
                uint lastCount = 0;

                while (_cycleReportRunning)
                {
                    Thread.Sleep(50);

                    if (_handle == IntPtr.Zero) continue;

                    uint count = 0;
                    int ret = ZMotionNative.ZAux_CycleUpGetRecvTimes(
                        _handle, _cycleReportChannel, ref count);

                    if (ZMotionNative.IsSuccess(ret) && count > lastCount)
                    {
                        lastCount = count;

                        // 触发事件
                        var handler = CycleReportReceived;
                        if (handler != null)
                        {
                            var args = new CycleReportEventArgs
                            {
                                Channel = _cycleReportChannel,
                                ReportCount = count
                            };
                            handler(this, args);
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // 正常中止
            }
            catch (Exception ex)
            {
                Log(string.Format("周期上报线程异常 原因 {0}", ex.Message));
            }
        }

        // ============================================================
        // 配置管理（轴字典/IO字典 + 持久化）
        // ============================================================

        /// <summary>
        /// 从配置加载轴和IO（使用轴字典/IO字典）
        /// 需在 Initialize() 之后调用，或修改 Initialize 流程先加载配置。
        /// </summary>
        public bool LoadFromConfig()
        {
            if (_handle == IntPtr.Zero)
            {
                Log("从配置加载失败 原因是未连接");
                return false;
            }

            var mgr = MotionConfigManager.Instance;
            mgr.Load();
            var cfg = mgr.Config;

            Log(string.Format(
                "从配置加载 轴数量 {0} 输入IO数量 {1} 输出IO数量 {2}",
                cfg.AxisDictionary.Count,
                cfg.InputIODictionary.Count,
                cfg.OutputIODictionary.Count));

            // 清空现有轴和IO
            lock (_axes) { _axes.Clear(); }
            ClearIO();

            // 按配置创建轴
            foreach (var kvp in cfg.AxisDictionary)
            {
                var axisCfg = kvp.Value;
                if (!axisCfg.Enabled) continue;

                var axis = new ZMotionAxis(_handle, axisCfg.AxisNumber, axisCfg.Name);
                axis.SetDefaultParams(
                    axisCfg.Units,
                    axisCfg.LSpeed,
                    axisCfg.Speed,
                    axisCfg.Accel,
                    axisCfg.Decel,
                    axisCfg.Sramp);
                AddAxis(axis);
            }

            // 按配置创建输入IO
            foreach (var kvp in cfg.InputIODictionary)
            {
                var ioCfg = kvp.Value;
                if (!ioCfg.Enabled) continue;

                var io = new ZMotionInputIO(_handle, ioCfg.IONumber, ioCfg.Name);
                AddIO(io);
            }

            // 按配置创建输出IO
            foreach (var kvp in cfg.OutputIODictionary)
            {
                var ioCfg = kvp.Value;
                if (!ioCfg.Enabled) continue;

                var io = new ZMotionOutputIO(_handle, ioCfg.IONumber, ioCfg.Name);
                io.EmergencyStopOff = ioCfg.EmergencyStopOff;
                AddIO(io);
            }

            Log("配置加载应用完成");
            return true;
        }

        /// <summary>
        /// 保存当前轴和IO配置到INI文件
        /// </summary>
        public bool SaveToConfig()
        {
            var mgr = MotionConfigManager.Instance;
            var cfg = mgr.Config;

            // 同步IP
            cfg.IpAddress = _ipAddress;
            cfg.ControllerName = Name;

            // 注意：这里保存的是配置文件中的字典数据，
            // 如果需要从当前运行时轴/IO反推配置，需额外实现。
            // 当前设计：配置是唯一来源，运行时从配置创建。

            return mgr.Save();
        }

        // ============================================================
        // 内部方法 - 初始化（改为从配置加载）
        // ============================================================

        /// <summary>
        /// 初始化轴和IO（优先从配置加载，配置不存在则使用默认值）
        /// </summary>
        private void InitializeAxes()
        {
            // 尝试从配置加载
            var mgr = MotionConfigManager.Instance;
            bool configLoaded = mgr.Load();

            if (configLoaded && mgr.Config.AxisDictionary.Count > 0)
            {
                // 从配置创建轴
                foreach (var kvp in mgr.Config.AxisDictionary)
                {
                    var axisCfg = kvp.Value;
                    if (!axisCfg.Enabled) continue;

                    var axis = new ZMotionAxis(_handle, axisCfg.AxisNumber, axisCfg.Name);
                    axis.SetDefaultParams(
                        axisCfg.Units,
                        axisCfg.LSpeed,
                        axisCfg.Speed,
                        axisCfg.Accel,
                        axisCfg.Decel,
                        axisCfg.Sramp);
                    // 应用软限位配置（有效值才下发）
                    if (axisCfg.SoftLimitPositive < 9999f || axisCfg.SoftLimitNegative > -9999f)
                    {
                        axis.SetSoftLimit(axisCfg.SoftLimitPositive, axisCfg.SoftLimitNegative);
                    }
                    AddAxis(axis);
                }
            }
            else
            {
                // 默认4轴
                var yAxis = new ZMotionAxis(_handle, 0, "Y轴");
                yAxis.SetDefaultParams(1000f, 10f, 50f, 500f, 500f, 50f);
                AddAxis(yAxis);

                var galvoAxis = new ZMotionAxis(_handle, 1, "振镜轴");
                galvoAxis.SetDefaultParams(1000f, 20f, 200f, 2000f, 2000f, 20f);
                AddAxis(galvoAxis);

                var wireFeedAxis = new ZMotionAxis(_handle, 2, "送丝伸缩臂轴");
                wireFeedAxis.SetDefaultParams(1000f, 5f, 30f, 300f, 300f, 50f);
                AddAxis(wireFeedAxis);

                var zFocusAxis = new ZMotionAxis(_handle, 3, "Z轴自动对焦轴");
                zFocusAxis.SetDefaultParams(1000f, 5f, 40f, 400f, 400f, 50f);
                AddAxis(zFocusAxis);
            }

            Log(string.Format("运动轴初始化完成 轴数量 {0}", AxisCount));
        }

        /// <summary>
        /// 初始化IO（优先从配置加载）
        /// </summary>
        private void InitializeIOs()
        {
            var mgr = MotionConfigManager.Instance;
            var cfg = mgr.Config;

            if (cfg.InputIODictionary.Count > 0 || cfg.OutputIODictionary.Count > 0)
            {
                // 从配置创建输入IO
                foreach (var kvp in cfg.InputIODictionary)
                {
                    var ioCfg = kvp.Value;
                    if (!ioCfg.Enabled) continue;
                    var io = new ZMotionInputIO(_handle, ioCfg.IONumber, ioCfg.Name);
                    AddIO(io);
                }

                // 从配置创建输出IO
                foreach (var kvp in cfg.OutputIODictionary)
                {
                    var ioCfg = kvp.Value;
                    if (!ioCfg.Enabled) continue;
                    var io = new ZMotionOutputIO(_handle, ioCfg.IONumber, ioCfg.Name);
                    io.EmergencyStopOff = ioCfg.EmergencyStopOff;
                    AddIO(io);
                }
            }
            else
            {
                // 默认IO
                AddIO(new ZMotionInputIO(_handle, 0, "外部启动信号"));
                AddIO(new ZMotionInputIO(_handle, 1, "外部停止信号"));
                AddIO(new ZMotionInputIO(_handle, 2, "压力传感器信号"));
                AddIO(new ZMotionInputIO(_handle, 3, "位置传感器信号"));
                AddIO(new ZMotionOutputIO(_handle, 0, "焊枪启动"));
                AddIO(new ZMotionOutputIO(_handle, 1, "送丝启动"));
                AddIO(new ZMotionOutputIO(_handle, 2, "振镜使能"));
                AddIO(new ZMotionOutputIO(_handle, 3, "激光器使能"));
            }

            Log(string.Format(
                "IO初始化完成 输入数量 {0} 输出数量 {1}", InputCount, OutputCount));
        }

        /// <summary>
        /// 初始化振镜控制
        /// 配置振镜X/Y轴、激光输出口、功率DAC，供上层振镜打标调用。
        /// 振镜DLL缺失时不影响运动控制主流程（记录日志后继续）。
        /// </summary>
        private void InitializeGalvo()
        {
            try
            {
                bool ok = _galvo.Initialize(_handle);
                if (ok)
                {
                    Log("振镜控制初始化完成");
                }
                else
                {
                    Log("振镜控制初始化失败（可能缺少 ZmotionScanLaser.dll）");
                }
            }
            catch (Exception ex)
            {
                Log(string.Format(
                    "振镜控制初始化异常 原因 {0}", ex.Message));
            }
        }

        /// <summary>
        /// 获取振镜控制实例
        /// 用于振镜打标、激光工艺参数配置等高级操作。
        /// </summary>
        /// <returns>振镜控制实例（未初始化时为 null）</returns>
        public ZMotionGalvo GetGalvo()
        {
            return _galvo != null && _galvo.IsInitialized ? _galvo : null;
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose）。
        /// 停止心跳/周期上报线程、释放主动上报 GCHandle、关闭控制器连接。
        /// </summary>
        public void Dispose()
        {
            try { StopHeartbeat(); } catch (Exception ex) { Log(string.Format("释放心跳线程异常 原因 {0}", ex.Message)); }
            try { DisableCycleReport(_cycleReportChannel); } catch (Exception ex) { Log(string.Format("释放周期上报线程异常 原因 {0}", ex.Message)); }
            try { DisableAutoReport(); } catch (Exception ex) { Log(string.Format("释放主动上报异常 原因 {0}", ex.Message)); }
            try
            {
                if (_autoReportHandle.IsAllocated) _autoReportHandle.Free();
            }
            catch { }
            try { Close(); } catch (Exception ex) { Log(string.Format("关闭控制器异常 原因 {0}", ex.Message)); }

            HeartbeatStateChanged = null;
            AutoReportReceived = null;
            CycleReportReceived = null;
        }
    }
}
