using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace AdaWeldSystem.EmguALG.RealtimeMonitor.Playback
{
    /// <summary>
    /// 回放算法：目录扫描、缩略图解码与定时推进。
    /// 时间基准、进度、暂停、倍速与跳转均在此。
    /// 调用方只消费事件。
    /// </summary>
    /// <remarks>
    /// 线程模型：扫描与缩略图各起后台 Thread（与迁移前一致）；播放帧解码在
    /// System.Threading.Timer 回调线程执行，不占 UI 线程。事件由后台线程抛出，
    /// 订阅方自行封送回到 UI 线程。解码/缩略图绝不阻塞 UI。
    /// 由 AlgorithmManager 单例所有，跨页面复用。
    /// 事件可多次 +=/-=，退订后不再触发。
    /// </remarks>
    public sealed class PlaybackAlgorithm : IDisposable
    {
        #region 常量

        /// <summary>慢放档位（点一次切一档，末档回到 1.0x）</summary>
        private static readonly double[] SlowSpeeds = new double[] { 0.8, 0.5, 0.3, 0.1, 1.0 };

        /// <summary>快放档位（点一次切一档，末档回到 1.0x）</summary>
        private static readonly double[] FastSpeeds = new double[] { 1.5, 2.0, 2.5, 3.0, 1.0 };

        /// <summary>1.0x 在档位数组中的下标</summary>
        private const int SpeedNeutralIndex = 4;

        /// <summary>播放推进周期（毫秒），与迁移前的 WinForms 定时器一致</summary>
        private const int PlayTickMs = 33;

        #endregion

        #region 私有变量

        private readonly object _sync = new object();
        private readonly object _scanLock = new object();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Timer _playTimer;

        private VideoCapture _capture;
        private double _speed = 1.0;
        private int _slowSpeedIndex = SpeedNeutralIndex;
        private int _fastSpeedIndex = SpeedNeutralIndex;
        private int _frameIndex;
        private int _totalFrames;
        private double _fps = 30;
        private double _anchorFrames;
        private double _anchorSpeed = 1.0;
        private bool _isPlaying;
        private bool _isDisposed;

        // ---- 区间扫描 / 缩略图 ----
        private CancellationTokenSource _scanCts;
        private volatile int _scanGeneration;
        private volatile int _thumbGeneration;

        #endregion

        #region 事件

        /// <summary>扫描开始（同步在调用方线程抛出；UI 清空列表并显示忙碌文案）</summary>
        public event Action<string> ScanStarted;

        /// <summary>扫描完成（后台线程抛出，UI 自行封送）</summary>
        public event Action<PlaybackScanResult> ScanCompleted;

        /// <summary>扫描线程异常（后台线程抛出，参数为异常原文）</summary>
        public event Action<string> ScanCrashed;

        /// <summary>缩略图就绪（后台线程抛出，UI 自行封送）</summary>
        public event Action<PlaybackFileInfo> ThumbnailReady;

        /// <summary>播放帧就绪（后台线程抛出，UI 自行封送并上屏）</summary>
        public event Action<Mat> FrameReady;

        /// <summary>播放进度（帧序号 / 总帧数 / 帧率）</summary>
        public event Action<int, int, double> ProgressChanged;

        /// <summary>播放状态（是否播放中 / 当前倍速）</summary>
        public event Action<bool, double> PlayStateChanged;

        #endregion

        #region 公共变量

        /// <summary>是否已打开录像文件</summary>
        public bool HasSource
        {
            get { lock (_sync) { return _capture != null; } }
        }

        /// <summary>是否正在播放</summary>
        public bool IsPlaying
        {
            get { lock (_sync) { return _isPlaying; } }
        }

        /// <summary>当前倍速</summary>
        public double Speed
        {
            get { lock (_sync) { return _speed; } }
        }

        #endregion

        #region 构造函数

        /// <summary>构造回放算法（定时器创建后处于停止态）</summary>
        public PlaybackAlgorithm()
        {
            _playTimer = new Timer(PlayTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region 区间扫描 / 缩略图

        /// <summary>采集区间内录像文件行（日期倒序，按文件名序）</summary>
        /// <param name="root">录像根目录</param>
        /// <param name="start">起始日期（含）</param>
        /// <param name="end">结束日期（含）</param>
        /// <param name="rangeText">区间展示文本</param>
        /// <param name="token">取消令牌</param>
        /// <returns>扫描结果（非 null）</returns>
        private static PlaybackScanResult CollectRangeFileRows(string root, DateTime start, DateTime end, string rangeText, CancellationToken token)
        {
            PlaybackScanResult outcome = new PlaybackScanResult { Files = new List<PlaybackFileInfo>() };
            if (string.IsNullOrEmpty(root))
            {
                outcome.Error = "焊接相机参数设置中未配置录像保存地址";
                return outcome;
            }
            if (!Directory.Exists(root))
            {
                outcome.Error = "录像目录不存在：" + root;
                return outcome;
            }

            token.ThrowIfCancellationRequested();
            string[] dirs = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly);
            List<string> dayPaths = new List<string>();
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (name == null || name.Length != 8) continue;
                if (!DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) continue;
                if (date.Date < start || date.Date > end) continue;
                dayPaths.Add(dirs[i]);
            }
            dayPaths.Sort(delegate (string a, string b) { return string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)); });

            int folderCount = dayPaths.Count;
            for (int i = 0; i < dayPaths.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string dayName = Path.GetFileName(dayPaths[i]);
                // 只取该日期文件夹本层，不进入其子目录
                string[] paths = Directory.GetFiles(dayPaths[i], "*.avi", SearchOption.TopDirectoryOnly);
                Array.Sort(paths, delegate (string a, string b) { return string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase); });
                for (int j = 0; j < paths.Length; j++)
                {
                    token.ThrowIfCancellationRequested();
                    FileInfo file = new FileInfo(paths[j]);
                    outcome.Files.Add(new PlaybackFileInfo
                    {
                        FileName = file.Name,
                        FullPath = file.FullName,
                        // 日期取文件夹名，时间取文件时分
                        Modified = dayName.Substring(0, 4) + "-" + dayName.Substring(4, 2) + "-" + dayName.Substring(6, 2)
                            + " " + file.LastWriteTime.ToString("HH:mm"),
                        Size = (file.Length / 1024.0 / 1024.0).ToString("F1") + "MB"
                    });
                }
            }

            if (folderCount == 0)
            {
                outcome.Caption = string.Format("日期区间内无日期文件夹 · {0}（{1}）", root, rangeText);
            }
            else if (outcome.Files.Count == 0)
            {
                outcome.Caption = string.Format("该区间内无录像文件（{0} 个日期文件夹均为空）· {1}", folderCount, root);
            }
            else
            {
                outcome.Caption = string.Format("共 {0} 个录像文件（{1} 个日期文件夹，{2}）· 右键行播放 · 根路径：{3}",
                    outcome.Files.Count, folderCount, rangeText, root);
            }
            return outcome;
        }

        /// <summary>启动缩略图线程，逐帧解码后抛事件</summary>
        /// <param name="files">扫描产出的文件行</param>
        private void StartThumbnailLoader(List<PlaybackFileInfo> files)
        {
            int generation = ++_thumbGeneration;
            Thread thread = new Thread(delegate ()
            {
                for (int i = 0; i < files.Count; i++)
                {
                    if (generation != _thumbGeneration) return;
                    Bitmap thumb = LoadThumbnail(files[i].FullPath);
                    if (generation != _thumbGeneration)
                    {
                        thumb?.Dispose();
                        return;
                    }
                    if (thumb == null) continue;
                    files[i].Thumbnail = thumb;
                    ThumbnailReady?.Invoke(files[i]);
                }
            })
            {
                IsBackground = true,
                Name = "MonitorCamThumbnailLoader"
            };
            thread.Start();
        }

        /// <summary>解码录像首帧并缩放到 96×54 缩略图；失败返回 null（不阻断列表显示）</summary>
        /// <param name="path">录像文件完整路径</param>
        /// <returns>缩略图；失败返回 null</returns>
        private static Bitmap LoadThumbnail(string path)
        {
            try
            {
                using (VideoCapture capture = new VideoCapture(path))
                {
                    if (!capture.IsOpened) return null;
                    using (Mat frame = capture.QueryFrame())
                    {
                        if (frame == null || frame.IsEmpty) return null;
                        using (Bitmap source = frame.ToBitmap())
                        {
                            if (source == null) return null;
                            Bitmap thumb = new Bitmap(96, 54);
                            using (Graphics graphics = Graphics.FromImage(thumb))
                            {
                                graphics.DrawImage(source, 0, 0, 96, 54);
                            }
                            return thumb;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>取消扫描与缩略图解码，不 Dispose 令牌源</summary>
        public void CancelScan()
        {
            lock (_scanLock)
            {
                _scanGeneration++;
                _thumbGeneration++;
                CancellationTokenSource cts = _scanCts;
                _scanCts = null;
                if (cts == null) return;
                try { cts.Cancel(); } catch { }   // 取消令牌：失败无影响
            }
        }

        /// <summary>按日期区间汇总录像文件（后台线程扫描，事件回 UI）</summary>
        /// <param name="root">录像根目录（由调用方从硬件配置取出后传入）</param>
        /// <param name="start">起始日期（含）</param>
        /// <param name="end">结束日期（含）</param>
        public void LoadRange(string root, DateTime start, DateTime end)
        {
            string rangeText = string.Format("{0} ~ {1}", start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

            CancelScan();
            int generation = _scanGeneration;
            CancellationTokenSource cts = new CancellationTokenSource();
            lock (_scanLock) { _scanCts = cts; }
            CancellationToken token = cts.Token;

            ScanStarted?.Invoke("正在读取录像文件… " + root);

            Thread thread = new Thread(delegate ()
            {
                PlaybackScanResult result = null;
                Exception error = null;
                try
                {
                    result = CollectRangeFileRows(root, start, end, rangeText, token);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                if (token.IsCancellationRequested || generation != _scanGeneration) return;

                if (error != null)
                {
                    ScanCrashed?.Invoke(error.Message);
                    return;
                }

                ScanCompleted?.Invoke(result);
                if (result != null && result.Error == null && result.Files.Count > 0)
                    StartThumbnailLoader(result.Files);
            })
            {
                IsBackground = true,
                Name = "MonitorCamPlayScan"
            };
            thread.Start();
        }

        #endregion

        #region 播放控制

        /// <summary>打开录像并播放（重置进度与倍速）</summary>
        /// <param name="path">录像文件完整路径</param>
        /// <returns>空串表示成功，否则为可展示的失败提示</returns>
        public string PlayFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "录像文件不存在：" + path;

            lock (_sync)
            {
                if (_isDisposed) return "回放组件已释放";
                StopLocked();

                VideoCapture capture = new VideoCapture(path);
                if (!capture.IsOpened)
                {
                    capture.Dispose();
                    return "无法打开录像文件（需要解码支持）：" + path;
                }
                _capture = capture;

                _totalFrames = (int)capture.Get(CapProp.FrameCount);
                if (_totalFrames < 0) _totalFrames = 0;
                // 帧率取自文件（cv Get(CapProp.Fps)）；异常值才回退 30，避免「固定 30fps 假设」与真实录像错位
                _fps = capture.Get(CapProp.Fps);
                if (_fps <= 0 || _fps > 240) _fps = 30;
                _frameIndex = 0;
                _anchorFrames = 0;
                _anchorSpeed = 1.0;
                _speed = 1.0;
                _slowSpeedIndex = SpeedNeutralIndex;
                _fastSpeedIndex = SpeedNeutralIndex;
                _isPlaying = true;
                _clock.Restart();
                _playTimer.Change(PlayTickMs, PlayTickMs);
            }
            RaiseStateChanged();
            RaiseProgress();
            return string.Empty;
        }

        /// <summary>播放/暂停切换</summary>
        /// <returns>空串表示成功，否则为可展示的提示文案</returns>
        public string TogglePlayPause()
        {
            lock (_sync)
            {
                if (_capture == null) return "请先在右侧列表右键选择要回放的录像文件！";
                if (_isPlaying)
                {
                    _isPlaying = false;
                    _playTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _clock.Stop();
                }
                else
                {
                    if (_totalFrames > 0 && _frameIndex >= _totalFrames) SeekToFrameLocked(0);
                    _anchorFrames = _frameIndex;
                    _anchorSpeed = _speed;
                    _isPlaying = true;
                    _clock.Restart();
                    _playTimer.Change(PlayTickMs, PlayTickMs);
                }
            }
            RaiseStateChanged();
            return string.Empty;
        }

        /// <summary>慢放：按 0.8→0.5→0.3→0.1→1.0 循环切换（以当前位置为新锚点）</summary>
        public void CycleSlow()
        {
            lock (_sync)
            {
                _slowSpeedIndex = (_slowSpeedIndex + 1) % SlowSpeeds.Length;
                _fastSpeedIndex = SpeedNeutralIndex;
                _speed = SlowSpeeds[_slowSpeedIndex];
                RebasePlayClockLocked();
            }
            RaiseStateChanged();
        }

        /// <summary>快放：按 1.5→2.0→2.5→3.0→1.0 循环切换（以当前位置为新锚点）</summary>
        public void CycleFast()
        {
            lock (_sync)
            {
                _fastSpeedIndex = (_fastSpeedIndex + 1) % FastSpeeds.Length;
                _slowSpeedIndex = SpeedNeutralIndex;
                _speed = FastSpeeds[_fastSpeedIndex];
                RebasePlayClockLocked();
            }
            RaiseStateChanged();
        }

        /// <summary>定位到指定帧（暂停态立即抛帧）</summary>
        /// <param name="frameIndex">目标帧序号</param>
        public void SeekToFrame(int frameIndex)
        {
            lock (_sync) { SeekToFrameLocked(frameIndex); }
            RaiseProgress();
        }

        /// <summary>相对跳转（±5 秒按钮）</summary>
        /// <param name="seconds">相对秒数（负为后退）</param>
        public void SeekBySeconds(double seconds)
        {
            lock (_sync)
            {
                if (_capture == null) return;
                int delta = (int)Math.Round(seconds * _fps);
                SeekToFrameLocked(_frameIndex + delta);
            }
            RaiseProgress();
        }

        /// <summary>停止回放并释放解码资源（进度回零、倍速复位；幂等，可重复调用）</summary>
        public void Stop()
        {
            lock (_sync) { StopLocked(); }
            RaiseProgress();
            RaiseStateChanged();
        }

        /// <summary>复位到干净会话，并取消扫描与解码</summary>
        /// <remarks>页面打开时调用，避免沿用旧回放态</remarks>
        public void Reset()
        {
            lock (_sync) { StopLocked(); }
            CancelScan();
            RaiseProgress();
            RaiseStateChanged();
        }

        #endregion

        #region 私有函数

        /// <summary>定时回调：按时间基准推算目标帧并补齐</summary>
        /// <param name="state">定时器状态（未使用）</param>
        private void PlayTimerTick(object state)
        {
            if (_isDisposed) return;
            if (!Monitor.TryEnter(_sync)) return;   // 上一拍未完成时丢弃本拍，避免解码重叠
            try
            {
                if (_capture == null || !_isPlaying) return;

                int targetFrame = (int)TargetPlayFrameLocked();
                if (targetFrame > _frameIndex)
                {
                    int limit = _totalFrames > 0 ? _totalFrames : int.MaxValue;
                    if (targetFrame > limit) targetFrame = limit;
                    int advance = targetFrame - _frameIndex;
                    for (int i = 0; i < advance; i++)
                    {
                        Mat frame = _capture.QueryFrame();
                        if (frame == null || frame.IsEmpty)
                        {
                            FinishPlaybackLocked();
                            return;
                        }
                        if (i == advance - 1)
                        {
                            FrameReady?.Invoke(frame);   // 交付 UI 上屏，所有权随显示控件（与迁移前一致）
                        }
                        else
                        {
                            frame.Dispose();
                        }
                    }
                    _frameIndex = targetFrame;
                    RaiseProgress();
                }

                if (_totalFrames > 0 && _frameIndex >= _totalFrames) FinishPlaybackLocked();
            }
            finally
            {
                Monitor.Exit(_sync);
            }
        }

        /// <summary>时间基准驱动：锚点帧 + 已播放时长 × 帧率 × 倍速</summary>
        /// <returns>应到的帧位置（可能为小数）</returns>
        private double TargetPlayFrameLocked()
        {
            return _anchorFrames + _clock.Elapsed.TotalSeconds * _fps * _anchorSpeed;
        }

        /// <summary>以当前位置为新锚点重启播放时钟</summary>
        private void RebasePlayClockLocked()
        {
            _anchorFrames = TargetPlayFrameLocked();
            _anchorSpeed = _speed;
            _clock.Restart();
        }

        /// <summary>定位到指定帧（调用方已持有 _sync）</summary>
        /// <param name="frameIndex">目标帧序号</param>
        private void SeekToFrameLocked(int frameIndex)
        {
            if (_capture == null) return;

            int maxFrame = _totalFrames > 0 ? _totalFrames - 1 : int.MaxValue;
            if (frameIndex < 0) frameIndex = 0;
            if (frameIndex > maxFrame) frameIndex = maxFrame;
            if (!_capture.Set(CapProp.PosFrames, frameIndex)) return;

            _frameIndex = frameIndex;
            _anchorFrames = frameIndex;
            _anchorSpeed = _speed;
            _clock.Restart();
            if (!_isPlaying)
            {
                Mat frame = _capture.QueryFrame();
                if (frame != null && !frame.IsEmpty)
                {
                    FrameReady?.Invoke(frame);
                }
            }
        }

        /// <summary>播到结尾：停在末帧，进度置满（须持锁）</summary>
        private void FinishPlaybackLocked()
        {
            _isPlaying = false;
            _playTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _clock.Stop();
            if (_totalFrames > 0) _frameIndex = _totalFrames;
            RaiseProgress();
            RaiseStateChanged();
        }

        /// <summary>停止并复位（调用方已持有 _sync）</summary>
        private void StopLocked()
        {
            _playTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isPlaying = false;
            if (_capture != null)
            {
                try { _capture.Dispose(); } catch { }   // 释放路径：失败不影响功能
                _capture = null;
            }
            _totalFrames = 0;
            _frameIndex = 0;
            _anchorFrames = 0;
            _anchorSpeed = 1.0;
            _clock.Reset();
            _speed = 1.0;
            _slowSpeedIndex = SpeedNeutralIndex;
            _fastSpeedIndex = SpeedNeutralIndex;
        }

        /// <summary>抛出进度（帧序号 / 总帧数 / 帧率）</summary>
        private void RaiseProgress()
        {
            int index;
            int total;
            double fps;
            lock (_sync)
            {
                index = _frameIndex;
                total = _totalFrames;
                fps = _fps;
            }

            ProgressChanged?.Invoke(index, total, fps);
        }

        /// <summary>抛出播放状态（是否播放中 / 当前倍速）</summary>
        private void RaiseStateChanged()
        {
            bool playing;
            double speed;
            lock (_sync)
            {
                playing = _isPlaying;
                speed = _speed;
            }

            PlayStateChanged?.Invoke(playing, speed);
        }

        #endregion

        #region 释放

        /// <summary>停止回放、取消扫描并释放解码器与定时器</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_isDisposed) return;
                _isDisposed = true;
                StopLocked();
            }
            CancelScan();
            _playTimer.Dispose();
        }

        #endregion
    }
}
