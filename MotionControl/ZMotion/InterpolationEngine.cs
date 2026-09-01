using System;
using System.Collections.Generic;
using System.Threading;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 连续插补拐角模式
    /// </summary>
    public enum CornerMode
    {
        /// <summary>拐角减速模式（默认）</summary>
        DecelMode = 0,

        /// <summary>全速度圆角模式</summary>
        FullSpeedRadius = 1,

        /// <summary>停止模式（精确停在每段终点）</summary>
        StopMode = 2
    }

    /// <summary>
    /// 多轴插补段数据
    /// </summary>
    public struct InterpolationSegment
    {
        /// <summary>各轴目标位置数组（顺序与轴列表一致）</summary>
        public float[] Positions { get; set; }
    }

    /// <summary>
    /// 连续插补引擎
    /// 封装正运动控制器的小线段连续插补功能，负责：
    /// - 配置拐角参数（拐角模式、减速角度、停止角度、圆角半径等）
    /// - 批量发送插补段（MultiMoveAbs）
    /// - 缓冲区管理（剩余空间检查、自动流控）
    /// - 后台线程持续发送轨迹段
    /// 
    /// 使用方式：
    ///   var engine = new InterpolationEngine(handle, mainAxis, axisList);
    ///   engine.SetCornerParams(CornerMode.DecelMode, 60, 120, 5);
    ///   engine.Start(segmentsList);  // 启动插补
    ///   engine.Stop();               // 停止插补
    /// </summary>
    public class InterpolationEngine : IDisposable
    {
        private const string Tag = "InterpolationEngine";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private readonly IntPtr _handle;
        private readonly int _mainAxis;
        private readonly int[] _axisList;
        private readonly int _axisCount;

        // 插补参数
        private float _speed;
        private float _accel;
        private float _decel;
        private CornerMode _cornerMode;
        private float _decelAngleRad;   // 开始减速角度（弧度）
        private float _stopAngleRad;    // 停止角度（弧度）
        private float _fullSpRadius;    // 全速度圆角半径
        private float _zsmooth;         // Z平滑时间（ms）

        // 发送控制
        private int _batchSize;         // 每批发送段数
        private int _minBufferSpace;    // 最小缓冲区剩余空间

        // 运行状态
        private volatile bool _isRunning;
        private Thread _sendThread;
        private Queue<InterpolationSegment> _segmentQueue;
        private readonly object _queueLock = new object();

        // 进度
        private volatile int _totalSegments;
        private volatile int _sentSegments;

        /// <summary>是否正在插补运行中</summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        /// <summary>总段数</summary>
        public int TotalSegments
        {
            get { return _totalSegments; }
        }

        /// <summary>已发送段数</summary>
        public int SentSegments
        {
            get { return _sentSegments; }
        }

        /// <summary>主轴号</summary>
        public int MainAxis
        {
            get { return _mainAxis; }
        }

        /// <summary>轴列表</summary>
        public int[] AxisList
        {
            get { return _axisList; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="handle">控制器连接句柄</param>
        /// <param name="mainAxis">主轴号（插补速度等参数以主轴为准）</param>
        /// <param name="axisList">参与插补的轴列表（第一个为主轴）</param>
        public InterpolationEngine(IntPtr handle, int mainAxis, int[] axisList)
        {
            _handle = handle;
            _mainAxis = mainAxis;
            _axisList = axisList ?? new int[] { mainAxis };
            _axisCount = _axisList.Length;

            _speed = 50f;
            _accel = 500f;
            _decel = 500f;
            _cornerMode = CornerMode.DecelMode;
            _decelAngleRad = (float)(60.0 * Math.PI / 180.0); // 60度
            _stopAngleRad = (float)(120.0 * Math.PI / 180.0);  // 120度
            _fullSpRadius = 5f;
            _zsmooth = 5f;

            _batchSize = 10;
            _minBufferSpace = 20;

            _isRunning = false;
            _sendThread = null;
            _segmentQueue = new Queue<InterpolationSegment>();
            _totalSegments = 0;
            _sentSegments = 0;
        }

        /// <summary>
        /// 设置插补速度参数
        /// </summary>
        public void SetSpeedParams(float speed, float accel, float decel)
        {
            _speed = speed;
            _accel = accel;
            _decel = decel;

            if (_handle == IntPtr.Zero) return;

            ZMotionNative.ZAux_Direct_SetSpeed(_handle, _mainAxis, _speed);
            ZMotionNative.ZAux_Direct_SetAccel(_handle, _mainAxis, _accel);
            ZMotionNative.ZAux_Direct_SetDecel(_handle, _mainAxis, _decel);
        }

        /// <summary>
        /// 设置拐角参数
        /// </summary>
        /// <param name="mode">拐角模式</param>
        /// <param name="decelAngleDeg">开始减速角度（度）</param>
        /// <param name="stopAngleDeg">停止角度（度）</param>
        /// <param name="fullSpeedRadius">全速度圆角半径</param>
        /// <param name="zsmoothMs">Z向平滑时间（ms）</param>
        public void SetCornerParams(CornerMode mode, float decelAngleDeg, float stopAngleDeg,
            float fullSpeedRadius, float zsmoothMs = 5f)
        {
            _cornerMode = mode;
            _decelAngleRad = (float)(decelAngleDeg * Math.PI / 180.0);
            _stopAngleRad = (float)(stopAngleDeg * Math.PI / 180.0);
            _fullSpRadius = fullSpeedRadius;
            _zsmooth = zsmoothMs;
        }

        /// <summary>
        /// 配置连续插补（应用参数到控制器）
        /// </summary>
        /// <returns>是否成功</returns>
        public bool Configure()
        {
            if (_handle == IntPtr.Zero)
            {
                Log("配置插补失败 原因是未连接");
                return false;
            }

            int ret;

            // 设置速度参数
            ret = ZMotionNative.ZAux_Direct_SetSpeed(_handle, _mainAxis, _speed);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            ret = ZMotionNative.ZAux_Direct_SetAccel(_handle, _mainAxis, _accel);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            ret = ZMotionNative.ZAux_Direct_SetDecel(_handle, _mainAxis, _decel);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            // 开启连续插补
            ret = ZMotionNative.ZAux_Direct_SetMerge(_handle, _mainAxis, 1);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            // 设置起始速度为0（插补模式下建议）
            ret = ZMotionNative.ZAux_Direct_SetLspeed(_handle, _mainAxis, 0);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            // 设置拐角模式
            ret = ZMotionNative.ZAux_Direct_SetCornerMode(_handle, _mainAxis, (int)_cornerMode);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            // 设置拐角角度参数
            ret = ZMotionNative.ZAux_Direct_SetDecelAngle(_handle, _mainAxis, _decelAngleRad);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            ret = ZMotionNative.ZAux_Direct_SetStopAngle(_handle, _mainAxis, _stopAngleRad);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            // 设置全速度圆角半径
            ret = ZMotionNative.ZAux_Direct_SetFullSpRadius(_handle, _mainAxis, _fullSpRadius);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            // 设置Z平滑时间
            ret = ZMotionNative.ZAux_Direct_SetZsmooth(_handle, _mainAxis, _zsmooth);
            if (!ZMotionNative.IsSuccess(ret)) goto error;

            Log(string.Format(
                "连续插补配置完成 模式 {0} 速度 {1:F1} 轴数 {2}",
                _cornerMode, _speed, _axisCount));

            return true;

            error:
            Log(string.Format(
                "配置插补失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// 获取直线缓冲区剩余空间
        /// </summary>
        public int GetBufferRemain()
        {
            if (_handle == IntPtr.Zero) return 0;
            int remain = 0;
            int ret = ZMotionNative.ZAux_Direct_GetRemain_LineBuffer(_handle, _mainAxis, ref remain);
            if (!ZMotionNative.IsSuccess(ret)) return 0;
            return remain;
        }

        /// <summary>
        /// 同步发送一批插补段
        /// </summary>
        /// <param name="segments">插补段数组</param>
        /// <returns>是否成功</returns>
        public bool SendSegments(InterpolationSegment[] segments)
        {
            if (_handle == IntPtr.Zero || segments == null || segments.Length == 0)
                return false;

            int segCount = segments.Length;

            // 构造位置数组：段数 × 轴数
            float[] positions = new float[segCount * _axisCount];
            for (int i = 0; i < segCount; i++)
            {
                for (int j = 0; j < _axisCount && j < segments[i].Positions.Length; j++)
                {
                    positions[i * _axisCount + j] = segments[i].Positions[j];
                }
            }

            int ret = ZMotionNative.ZAux_Direct_MultiMoveAbs(
                _handle, segCount, _axisCount, _axisList, positions);

            if (ZMotionNative.IsSuccess(ret))
            {
                _sentSegments += segCount;
                return true;
            }

            Log(string.Format(
                "发送插补段失败 原因是 {0}", ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// 启动插补（使用传入的段列表，后台线程发送）
        /// </summary>
        /// <param name="segments">插补段列表</param>
        /// <param name="batchSize">每批发送段数（默认10）</param>
        /// <returns>是否启动成功</returns>
        public bool Start(List<InterpolationSegment> segments, int batchSize = 10)
        {
            if (_handle == IntPtr.Zero || segments == null || segments.Count == 0)
            {
                Log("启动插补失败 原因是参数无效");
                return false;
            }

            if (_isRunning)
            {
                Log("插补正在运行中，先停止再启动");
                return false;
            }

            // 先配置插补参数
            if (!Configure()) return false;

            // 清空队列，装入所有段
            lock (_queueLock)
            {
                _segmentQueue.Clear();
                foreach (var seg in segments)
                {
                    _segmentQueue.Enqueue(seg);
                }
                _totalSegments = segments.Count;
                _sentSegments = 0;
            }

            _batchSize = batchSize;
            _isRunning = true;

            // 启动后台发送线程
            _sendThread = new Thread(SendThreadProc);
            _sendThread.IsBackground = true;
            _sendThread.Name = "InterpolationSendThread";
            _sendThread.Start();

            Log(string.Format(
                "连续插补已启动 总段数 {0} 批大小 {1}", _totalSegments, _batchSize));

            return true;
        }

        /// <summary>
        /// 停止插补
        /// </summary>
        public void Stop()
        {
            _isRunning = false;

            // 等待发送线程结束
            if (_sendThread != null && _sendThread.IsAlive)
            {
                if (!_sendThread.Join(2000))
                {
                    _sendThread.Abort();
                }
                _sendThread = null;
            }

            // 停止主轴运动
            if (_handle != IntPtr.Zero)
            {
                ZMotionNative.ZAux_Direct_Single_Cancel(_handle, _mainAxis, 0);
            }

            // 清空队列
            lock (_queueLock)
            {
                _segmentQueue.Clear();
            }

            // 关闭连续插补
            if (_handle != IntPtr.Zero)
            {
                ZMotionNative.ZAux_Direct_SetMerge(_handle, _mainAxis, 0);
            }

            Log("连续插补已停止");
        }

        /// <summary>
        /// 后台发送线程
        /// </summary>
        private void SendThreadProc()
        {
            try
            {
                while (_isRunning)
                {
                    int queueCount = 0;
                    lock (_queueLock)
                    {
                        queueCount = _segmentQueue.Count;
                    }

                    if (queueCount == 0)
                    {
                        // 所有段发送完毕，等待运动完成
                        Thread.Sleep(100);
                        int idle = 0;
                        ZMotionNative.ZAux_Direct_GetIfIdle(_handle, _mainAxis, ref idle);
                        if (idle == 1) // 已停止
                        {
                            _isRunning = false;
                            Log("插补全部完成");
                            break;
                        }
                        continue;
                    }

                    // 检查缓冲区剩余空间
                    int remain = GetBufferRemain();
                    if (remain < _minBufferSpace)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    // 计算本次发送数量
                    int sendCount = Math.Min(_batchSize, Math.Min(queueCount, remain / 2));
                    if (sendCount <= 0)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    // 取出段数据
                    InterpolationSegment[] batchSegs;
                    lock (_queueLock)
                    {
                        batchSegs = new InterpolationSegment[sendCount];
                        for (int i = 0; i < sendCount; i++)
                        {
                            batchSegs[i] = _segmentQueue.Dequeue();
                        }
                    }

                    // 发送
                    if (!SendSegments(batchSegs))
                    {
                        // 发送失败，停止
                        _isRunning = false;
                        break;
                    }

                    // 短暂让步，避免占满总线
                    Thread.Sleep(1);
                }
            }
            catch (ThreadAbortException)
            {
                // 正常中止
            }
            catch (Exception ex)
            {
                Log(string.Format(
                    "插补发送线程异常 原因 {0}", ex.Message));
                _isRunning = false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isRunning)
            {
                Stop();
            }
        }
    }
}