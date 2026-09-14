using System;
using System.Collections.Concurrent;
using System.Threading;
using AdaWeldSystem.PCLOperate.Models;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>
    /// 记录条目基类（异步落盘队列的载荷）
    /// </summary>
    public abstract class RecordItem
    {
        /// <summary>记录时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 构造函数（默认取当前时间）
        /// </summary>
        protected RecordItem()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 焊缝追踪记录条目：焊缝结果信息 + 机器人信息（可携带点云帧）
    /// 载荷对应 WeldFrameRecordEventArgs 过程快照，不含记录侧状态（帧索引由记录侧自取，ADR-043）
    /// </summary>
    public class SeamTrackItem : RecordItem
    {
        /// <summary>机器人 X 坐标</summary>
        public double RobotX { get; set; }

        /// <summary>机器人 Y 坐标</summary>
        public double RobotY { get; set; }

        /// <summary>机器人 Z 坐标</summary>
        public double RobotZ { get; set; }

        /// <summary>机器人移动速度</summary>
        public double RobotSpeed { get; set; }

        /// <summary>送丝速度</summary>
        public double FeedSpeed { get; set; }

        /// <summary>激光功率</summary>
        public double LaserPower { get; set; }

        /// <summary>焊缝宽度</summary>
        public double WeldWidth { get; set; }

        /// <summary>点云帧（可空；纯参数快照时为 null）</summary>
        public PointCloudData PointCloud { get; set; }

        /// <summary>是否携带焊接过程快照（参数七项是否有效）</summary>
        public bool HasWeldSnapshot { get; set; }
    }

    /// <summary>
    /// 焊缝参数调整记录条目：调整信息 + 机器人信息
    /// </summary>
    public class WeldParamAdjustItem : RecordItem
    {
        /// <summary>机器人 X 坐标</summary>
        public double RobotX { get; set; }

        /// <summary>机器人 Y 坐标</summary>
        public double RobotY { get; set; }

        /// <summary>机器人 Z 坐标</summary>
        public double RobotZ { get; set; }

        /// <summary>机器人移动速度</summary>
        public double RobotSpeed { get; set; }

        /// <summary>调整前送丝速度</summary>
        public double FeedSpeed { get; set; }

        /// <summary>调整前激光功率</summary>
        public double LaserPower { get; set; }

        /// <summary>调整时焊缝宽度</summary>
        public double WeldWidth { get; set; }

        /// <summary>调整明细（调整前后参数）</summary>
        public string AdjustData { get; set; }
    }

    /// <summary>
    /// 焊接结果记录条目：异常/失败/成功 + 产品信息（DB 落库）
    /// </summary>
    public class WeldResultItem : RecordItem
    {
        /// <summary>产品 SN</summary>
        public string Sn { get; set; }

        /// <summary>焊接开始时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>检测类型</summary>
        public string InspectionType { get; set; }

        /// <summary>焊接结果（成功/失败/异常）</summary>
        public string Result { get; set; }

        /// <summary>检查信息（异常/失败原因明细）</summary>
        public string CheckInfo { get; set; }
    }

    /// <summary>
    /// 产品记录抽象基类（抽象层，零第三方依赖）
    /// 定义记录类对外的属性与接口：记录名 / 会话生命周期（Start/Stop/Abort）+ 异步落盘机制——
    /// 记录请求只做入队（O(1)），真正的落盘在后台线程执行（Persist），绝不在计算路径上同步落盘（ADR-043）。
    /// </summary>
    public abstract class ProductRecordBase : IDisposable
    {
        private const string Tag = "ProductRecordBase";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private readonly ConcurrentQueue<RecordItem> _queue = new ConcurrentQueue<RecordItem>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private Thread _worker;
        private bool _workerRunning;
        private bool _disposed;

        /// <summary>
        /// 记录名称（用于日志与诊断）
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 是否正在记录（会话进行中）
        /// </summary>
        public bool IsRecording { get; protected set; }

        /// <summary>
        /// 队列中待落盘的条目数（诊断用）
        /// </summary>
        public int PendingCount
        {
            get { return _queue.Count; }
        }

        /// <summary>
        /// 开始记录会话（幂等）
        /// </summary>
        public void Start()
        {
            if (IsRecording)
            {
                return;
            }
            IsRecording = true;
            EnsureWorker();
            OnStart();
            Log(string.Format("记录 {0} 会话开始", Name));
        }

        /// <summary>
        /// 正常停止记录会话（先排空队列再收尾）
        /// </summary>
        public void Stop()
        {
            if (!IsRecording)
            {
                return;
            }
            IsRecording = false;
            FlushPending();
            OnStop();
            Log(string.Format("记录 {0} 会话正常结束", Name));
        }

        /// <summary>
        /// 异常中止记录会话（保留现场，先排空队列再收尾）
        /// </summary>
        public void Abort()
        {
            if (!IsRecording)
            {
                return;
            }
            IsRecording = false;
            FlushPending();
            OnAbort();
            Log(string.Format("记录 {0} 会话异常中止", Name), MessageLevel.Warning);
        }

        /// <summary>
        /// 记录请求入队（O(1)，非会话期静默丢弃）
        /// </summary>
        /// <param name="item">记录条目</param>
        protected void Enqueue(RecordItem item)
        {
            if (item == null || !IsRecording)
            {
                return;
            }
            _queue.Enqueue(item);
            _signal.Set();
        }

        /// <summary>
        /// 单条落盘（后台线程调用；允许耗时，不得在计算线程上执行）
        /// </summary>
        /// <param name="item">记录条目</param>
        protected abstract void Persist(RecordItem item);

        /// <summary>
        /// 会话开始的收尾钩子
        /// </summary>
        protected virtual void OnStart()
        {
        }

        /// <summary>
        /// 会话正常结束的收尾钩子
        /// </summary>
        protected virtual void OnStop()
        {
        }

        /// <summary>
        /// 会话异常中止的收尾钩子
        /// </summary>
        protected virtual void OnAbort()
        {
        }

        /// <summary>
        /// 确保后台落盘线程已启动（懒启动）
        /// </summary>
        private void EnsureWorker()
        {
            if (_worker != null && _worker.IsAlive)
            {
                return;
            }
            _workerRunning = true;
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Name = "ProductRecord_" + Name;
            _worker.Start();
        }

        /// <summary>
        /// 后台落盘线程主循环
        /// </summary>
        private void WorkerLoop()
        {
            while (_workerRunning)
            {
                RecordItem item;
                if (_queue.TryDequeue(out item))
                {
                    try
                    {
                        Persist(item);
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("记录 {0} 落盘异常 {1}", Name, ex.Message), MessageLevel.Error);
                    }
                    continue;
                }
                _signal.WaitOne(200);
            }
        }

        /// <summary>
        /// 排空队列中剩余条目（Stop/Abort 前调用，保证不丢尾）
        /// </summary>
        private void FlushPending()
        {
            _signal.Set();
            while (!_queue.IsEmpty)
            {
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// 释放资源（标准 IDisposable.Dispose，ADR-020）
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            IsRecording = false;
            _workerRunning = false;
            _signal.Set();
            if (_worker != null && _worker.IsAlive)
            {
                _worker.Join(1000);
            }
            _worker = null;
            _disposed = true;
        }
    }
}
