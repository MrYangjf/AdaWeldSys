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
    /// 线激光轮廓记录条目，仅携带点云帧。
    /// 工艺快照由 <see cref="WeldParamAdjustItem"/> 承载。
    /// </summary>
    public class ContourFrameItem : RecordItem
    {
        /// <summary>点云帧</summary>
        public PointCloudData PointCloud { get; set; }

        /// <summary>捕获该帧时的机器人 X（毫米）。落盘时偏移到点云 X，使轮廓沿行走轴对齐。</summary>
        public double RobotX { get; set; }
    }

    /// <summary>
    /// 工艺控制记录条目：机器人信息 + 工艺快照
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

        /// <summary>查表输出的送丝速度（调整值；与 RobotX 同帧落盘，保证按机器人 X 对齐）</summary>
        public double FeedSpeed { get; set; }

        /// <summary>查表输出的激光功率（调整值；与 RobotX 同帧落盘，保证按机器人 X 对齐）</summary>
        public double LaserPower { get; set; }

        /// <summary>调整时焊缝宽度</summary>
        public double WeldWidth { get; set; }

        /// <summary>动作类型：过程帧/输出触发/工具切换；由入口方法设定</summary>
        public string ActionType { get; set; }

        /// <summary>切换到的线激光分析 Job 编号（JobSwitch 动作时有效，其余为 0）</summary>
        public int JobId { get; set; }

        /// <summary>是否调整动作：true=参数调整/工具切换，false=过程帧。</summary>
        public bool AdjustExecuted { get; set; }

        /// <summary>调整明细（调整动作时有效）</summary>
        public string AdjustData { get; set; }

        /// <summary>焊缝特征点横向坐标（毫米，取最近一帧）</summary>
        public double SeamPointY { get; set; }

        /// <summary>焊缝特征点高度坐标（毫米，取最近一帧）</summary>
        public double SeamPointZ { get; set; }

        /// <summary>TA 送丝 Z 轴位置（毫米，台达轴反馈）</summary>
        public double TaPositionMm { get; set; }

        /// <summary>LA 对焦轴位置（毫米，WSX 0x6020 Sub3 × 量纲）</summary>
        public double LaPositionMm { get; set; }

        /// <summary>振镜摆幅（毫米，工艺计算输出）</summary>
        public double GalvoAmplitudeMm { get; set; }

        /// <summary>振镜图形编号（WsgGalvoPattern）</summary>
        public int GalvoPatternId { get; set; }

        /// <summary>激光头温度（摄氏度，WSX 0x6000 Sub1）</summary>
        public double HeadTempC { get; set; }
    }

    /// <summary>
    /// 焊接结果记录条目（数据库落库）。
    /// 焊毕一次写入，起焊信息在起焊时缓存。
    /// </summary>
    public class WeldResultItem : RecordItem
    {
        /// <summary>产品码 SN（空缺时由调用方以 TEST + 时间后缀填充）</summary>
        public string Sn { get; set; }

        /// <summary>焊接开始时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>记录状态（模拟 / 正常生产）</summary>
        public string Status { get; set; }

        /// <summary>焊接结果（正常焊接完成为 pass，异常焊接为 fail）</summary>
        public string Result { get; set; }

        /// <summary>异常焊接原因（Result=fail 时有效）</summary>
        public string Info { get; set; }
    }

    /// <summary>
    /// 产品记录抽象基类，抽象层零第三方依赖。
    /// 对外接口含记录名、会话生命周期与异步落盘。
    /// 请求只入队，落盘在后台线程。
    /// 后台落盘不阻塞计算路径。
    /// </summary>
    public abstract class ProductRecordBase : IDisposable
    {
        private const string Tag = "ProductRecordBase";

        private readonly ConcurrentQueue<RecordItem> _queue = new ConcurrentQueue<RecordItem>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private Thread _worker;
        private volatile bool _workerRunning;
        private volatile bool _disposed;
        private volatile bool _isRecording;

        /// <summary>排空等待上限（毫秒），超时告警退出，避免阻塞。</summary>
        /// <remarks>取自设置页「记录落盘排空(秒)」，运行期读取。改配置无需重启。</remarks>
        private static int FlushTimeoutMs { get { return (int)(DeviceWaitSettings.RecordFlushSeconds * 1000); } }

        /// <summary>
        /// 记录名称（用于日志与诊断）
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 是否正在记录（会话进行中）
        /// </summary>
        /// <remarks>起停线程与热路径共读写，故用 volatile 保证可见性。</remarks>
        public bool IsRecording
        {
            get { return _isRecording; }
            protected set { _isRecording = value; }
        }

        /// <summary>
        /// 队列中待落盘的条目数（诊断用）
        /// </summary>
        public int PendingCount
        {
            get { return _queue.Count; }
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">消息内容。</param>
        /// <param name="level">日志级别。</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
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
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "ProductRecord_" + Name
            };
            _worker.Start();
        }

        /// <summary>
        /// 后台落盘线程主循环
        /// </summary>
        private void WorkerLoop()
        {
            while (_workerRunning)
            {
                if (_queue.TryDequeue(out RecordItem item))
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
            Thread worker = _worker;
            if (worker == null || !worker.IsAlive)
            {
                // 线程未存活则直接返回，剩余条目丢弃
                if (!_queue.IsEmpty) Log(string.Format("记录 {0} 落盘线程未存活 剩余 {1} 条未落盘", Name, _queue.Count), MessageLevel.Warning);
                return;
            }

            int waitedMs = 0;
            while (!_queue.IsEmpty && waitedMs < FlushTimeoutMs)
            {
                _signal.Set();
                Thread.Sleep(10);
                waitedMs += 10;
            }

            if (!_queue.IsEmpty)
            {
                Log(string.Format("记录 {0} 排空超时(上限 {1}ms) 剩余 {2} 条未落盘", Name, FlushTimeoutMs, _queue.Count), MessageLevel.Warning);
            }
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
        /// 释放资源（标准 IDisposable.Dispose）
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
