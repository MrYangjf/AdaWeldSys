﻿﻿﻿using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.EmguALG.Manager;
using AdaWeldSystem.EmguALG.RealtimeMonitor.Playback;
using AdaWeldSystem.MainDeviceControl.DeviceStateApiCore;
using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MonitorCam;
using AdaWeldSystem.MonitorCam.IMonitorCam;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AdaWeldSystem.UIDesign.SubDialog.MonitorCamDialog;

namespace AdaWeldSystem.UIDesign.TabPage
{
    /// <summary>监控相机页：焊接相机设置与焊接回放两个页签。</summary>
    /// <remarks>事件处理均检查 InvokeRequired，跨线程安全刷新。</remarks>
    public partial class MonitorCamPage : UserControl
    {
        /// <summary>回放行 = 一个录像文件</summary>
        /// <remarks>层级只有根目录 → 日期文件夹 → 文件；表格只渲染文件行，日期由「修改时间」列体现。</remarks>
        public class PlaybackRow
        {
            /// <summary>录像文件名（列表显示）</summary>
            public string FileName { get; set; }

            /// <summary>录像文件完整路径（回放与缩略图解码用）</summary>
            public string FullPath { get; set; }

            /// <summary>所属日期 + 修改时间，用于区分日期</summary>
            public string Modified { get; set; }

            /// <summary>文件大小（MB，一位小数）</summary>
            public string Size { get; set; }

            /// <summary>预览缩略图（后台线程解码首帧后回填；null 表示仍在加载）</summary>
            public Bitmap Thumbnail { get; set; }

            /// <summary>预览列：缩略图未就绪显示加载占位</summary>
            public object Preview
            {
                get
                {
                    Bitmap thumb = Thumbnail;
                    if (thumb == null) return new AntdUI.CellText("加载中…");
                    return new AntdUI.CellImage(thumb, 6) { Size = new Size(96, 54), ImageFit = AntdUI.TFit.Contain };
                }
            }
        }

        /// <summary>鼠标进入底部热区显示播放控件栏</summary>
        private const int PlayBarHotZoneHeight = 96;

        private AntdUI.AntList<PlaybackRow> _playRows;
        private PlaybackRow _selectedPlayRow;
        private AntdUI.TooltipComponent _playTooltip;
        // 回放能力归算法层（AlgorithmManager 单例），流程层门面转发；本页只调用与展示
        private readonly MonitorCameraWorkflow _playback = MonitorCameraWorkflow.Instance;

        // ---- 回放状态（仅显示态）----
        private bool _syncPlaySlider;
        private bool _playListLoaded;

        public MonitorCamPage()
        {
            InitializeComponent();
            InitializeConnectionBar();
            txtRecordDir.Text = MonitorCamManager.Instance.RecordDirectory;
            InitCalibUi();
            LoadAlignThresholds();
            InitPlaybackTable();
            InitPlaybackUi();
            WireButtons();
            // 连续采集预览帧（图像归相机）；工作流分析链路使用 FrameCompletedEvent，与本页预览互不相干
            MonitorCamManager.Instance.PreviewFrameEvent += Manager_PreviewFrame;
            timerStatus.Start();
            ApplyAlignPermission();
            GlobalCommData.OperateLevelChanged += OnOperateLevelChanged;
            this.HandleDestroyed += MonitorCamPage_HandleDestroyed;
        }

        private void MonitorCamPage_HandleDestroyed(object sender, EventArgs e)
        {
            DisposeComponents();
        }

        /// <summary>释放资源并退订事件。</summary>
        public void DisposeComponents()
        {
            MonitorCamManager.Instance.PreviewFrameEvent -= Manager_PreviewFrame;
            GlobalCommData.OperateLevelChanged -= OnOperateLevelChanged;
            timerStatus.Stop();
            _playback.StopPlayback();
            _playback.CancelScan();
            UnwirePlaybackEvents();
            ClearPlaybackRows();
            _playTooltip?.Dispose();
            _playTooltip = null;
            try { MonitorCamManager.Instance.StopCapture(); } catch { }   // 停采集失败不阻断释放
        }

        #region 初始化

        private void WireButtons()
        {
            btnStartCapture.Click += btnStartCapture_Click;
            btnStopCapture.Click += btnStopCapture_Click;
            btnBrowseRecord.Click += btnBrowseRecord_Click;
            btnStartRecord.Click += btnStartRecord_Click;
            btnStopRecord.Click += btnStopRecord_Click;
            btnApplyParam.Click += btnApplyParam_Click;
            btnSaveAlign.Click += btnSaveAlign_Click;
            btnCalibSave.Click += btnCalibSave_Click;
            btnFeedTest.Click += btnFeedTest_Click;
            btnRefreshPlay.Click += btnRefreshPlay_Click;
        }

        /// <summary>初始化标定区：从配置加载尺度与光斑点坐标</summary>
        private void InitCalibUi()
        {
            var cfg = MonitorCamManager.Instance.Config;
            if (cfg == null) return;
            txtMmPerPixel.Text = cfg.MmPerPixel.ToString("F4");
            txtSpotX.Text = cfg.SpotX.ToString();
            txtSpotY.Text = cfg.SpotY.ToString();
        }

        /// <summary>对中阈值仅管理员可写</summary>
        private void ApplyAlignPermission()
        {
            bool admin = PermissionLevel.IsAdmin(GlobalCommData.mOperateLevel);
            txtAlignWarn.Enabled = admin;
            txtAlignFail.Enabled = admin;
            btnSaveAlign.Enabled = admin;
        }

        private void OnOperateLevelChanged(object sender, OperateLevel level)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnOperateLevelChanged(sender, level)));
                return;
            }
            ApplyAlignPermission();
        }

        /// <summary>回填监控对中判定阈值（算法层 WireFeed 配置，键 WIRE_ALIGN_WARN_MM / WIRE_ALIGN_FAIL_MM）</summary>
        private void LoadAlignThresholds()
        {
            var wire = AlgorithmManager.Instance.WireFeed;
            if (wire == null) return;
            txtAlignWarn.Text = wire.CenterWarnMm.ToString("0.00", CultureInfo.InvariantCulture);
            txtAlignFail.Text = wire.CenterFailMm.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>保存对中阈值：写入内存后落盘</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnSaveAlign_Click(object sender, EventArgs e)
        {
            if (!PermissionLevel.IsAdmin(GlobalCommData.mOperateLevel))
            {
                AntdUI.Message.warn(this.FindForm(), "当前权限无权修改对中阈值。该操作仅管理员可执行。");
                return;
            }
            if (!TryReadDouble(txtAlignWarn, out double alignWarn) || alignWarn <= 0.0
                || !TryReadDouble(txtAlignFail, out double alignFail) || alignFail <= alignWarn)
            {
                AntdUI.Message.warn(this.FindForm(), "光丝对中阈值须满足 0 < 警告 < 报警");
                return;
            }

            var wire = AlgorithmManager.Instance.WireFeed;
            if (wire == null)
            {
                AntdUI.Message.warn(this.FindForm(), "对中算法配置不可用！");
                return;
            }

            wire.CenterWarnMm = alignWarn;
            wire.CenterFailMm = alignFail;
            AlgorithmManager.Instance.Save();
            AntdUI.Message.info(this.FindForm(), "对中阈值已保存");
        }

        /// <summary>解析输入框为 double：空值或格式错误返回 false</summary>
        /// <param name="input">输入框</param>
        /// <param name="value">解析结果</param>
        /// <returns>true 表示解析成功</returns>
        private static bool TryReadDouble(AntdUI.Input input, out double value)
        {
            value = 0.0;
            if (input == null || string.IsNullOrEmpty(input.Text)) return false;
            string text = input.Text.Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            return double.TryParse(text, out value);
        }

        /// <summary>初始化回放列表列：Columns 须在代码后置设置，不可在 Designer 内</summary>
        /// <remarks>赋值。</remarks>
        private void InitPlaybackTable()
        {
            _playRows = new AntdUI.AntList<PlaybackRow>();
            playTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Preview", "预览图") { Width = "100" },
                new AntdUI.Column("FileName", "录像文件") { Width = "170" },
                new AntdUI.Column("Modified", "修改时间") { Width = "100" },
                new AntdUI.Column("Size", "大小") { Width = "55" }
            };
            playTable.Binding(_playRows);
            playTable.CellClick += playTable_CellClick;
        }

        #endregion

        #region 相机连接（IP 扫描 / 保存 / 连接）

        /// <summary>初始化顶栏连接区：显示本地配置保存的 IP，订阅按</summary>
        /// <remarks>钮并同步一次连接态</remarks>
        private void InitializeConnectionBar()
        {
            var cfg = MonitorCamManager.Instance.Config;
            if (cfg != null) txtIP.Text = cfg.IpAddress;

            btnScan.Click += btnScan_Click;
            btnConnect.Click += btnConnect_Click;

            UpdateConnectionUi();
        }

        /// <summary>统一刷新顶栏连接态（状态标签 / 连接按钮 / IP 与扫描</summary>
        /// <remarks>可用性）</remarks>
        private void UpdateConnectionUi()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateConnectionUi));
                return;
            }

            bool connected = MonitorCamManager.Instance.ConnectionState == MonitorCameraConnectionState.Connected;
            alert_ConnStatus.Text = connected ? "已连接" : "未连接";
            alert_ConnStatus.TextColor = connected ? Color.FromArgb(0, 128, 0) : Color.FromArgb(192, 0, 0);
            btnConnect.Text = connected ? "断开" : "连接";
            btnConnect.IconSvg = connected ? "DisconnectOutlined" : "ApiOutlined";
            txtIP.Enabled = !connected;
            btnScan.Enabled = !connected;
        }

        /// <summary>扫描网口下的监控相机，选中后回填并保存连接 IP</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnScan_Click(object sender, EventArgs e)
        {
            List<MonitorCamDeviceInfo> devices = MonitorCamManager.Instance.ScanDevices();
            if (devices == null || devices.Count == 0)
            {
                AntdUI.Message.warn(this.FindForm(), "未扫描到监控相机，请检查网口连接！");
                return;
            }
            using (var dlg = new MonitorCamScanForm(this.FindForm() as AntdUI.Window, devices))
            {
                if (AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "扫描相机", dlg, AntdUI.TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                }) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedIp))
                {
                    // 选中后 IP 自动填充并保存到本地配置
                    txtIP.Text = dlg.SelectedIp;
                    SetConnectIp(dlg.SelectedIp);
                }
            }
        }

        /// <summary>连接/断开监控相机（连接 IP 先落盘，再连接；连接成功</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>后读取当前曝光/增益回填）</remarks>
        private void btnConnect_Click(object sender, EventArgs e)
        {
            var mgr = MonitorCamManager.Instance;
            if (mgr.ConnectionState == MonitorCameraConnectionState.Connected)
            {
                mgr.StopCapture();
                mgr.CloseSensor();
                UpdateConnectionUi();
                return;
            }

            string ip = txtIP.Text == null ? string.Empty : txtIP.Text.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                AntdUI.Message.warn(this.FindForm(), "请输入相机IP！");
                return;
            }

            // 保存连接 IP 后再连接：工作流 EnsureConnected 读取配置 IP
            SetConnectIp(ip);
            bool ok = mgr.OpenSensor(ip, mgr.Config != null ? mgr.Config.Port : "5000");
            if (ok)
            {
                LoadCameraParams();
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("连接失败，请检查IP {0} 与设备是否匹配！", ip));
            }
            UpdateConnectionUi();
        }

        /// <summary>写入连接 IP 并落盘到 INI</summary>
        /// <param name="ip">目标相机 IP</param>
        private void SetConnectIp(string ip)
        {
            var mgr = MonitorCamManager.Instance;
            if (mgr.Config == null) return;
            mgr.Config.IpAddress = ip;
            mgr.SaveConfig();
        }

        /// <summary>连接成功后读取相机当前曝光/增益回填输入框（</summary>
        /// <remarks>读取失败保持原值）</remarks>
        private void LoadCameraParams()
        {
            float exposure = MonitorCamManager.Instance.GetExposureTime();
            if (exposure >= 0) txtExposure.Text = exposure.ToString("F1");
            float gain = MonitorCamManager.Instance.GetGain();
            if (gain >= 0) txtGain.Text = gain.ToString("F1");
        }

        #endregion

        #region 采集（连续模式）

        /// <summary>开始采集：连续模式开启取流，帧经预览事件刷新</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>画面</remarks>
        private void btnStartCapture_Click(object sender, EventArgs e)
        {
            var mgr = MonitorCamManager.Instance;
            if (mgr.ConnectionState != MonitorCameraConnectionState.Connected)
            {
                AntdUI.Message.warn(this.FindForm(), "请先连接相机！");
                return;
            }
            if (mgr.StartCapture())
            {
                LoadCameraParams();
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), "开始采集失败，请检查相机连接！");
            }
        }

        /// <summary>停止采集：录像中会先自动停止录像。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnStopCapture_Click(object sender, EventArgs e)
        {
            MonitorCamManager.Instance.StopCapture();
        }

        /// <summary>预览帧：直接上屏上层给的成品帧</summary>
        /// <param name="frame">预览帧（独立副本，成品帧）</param>
        private void Manager_PreviewFrame(Mat frame)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<Mat>(Manager_PreviewFrame), frame);
                return;
            }
            if (frame == null || frame.IsEmpty) return;
            imageBox1.Image = frame;
        }

        #endregion

        #region 录像（须先开始采集）

        /// <summary>选择录像目录并落盘（FolderBrowserDialog 未实现 IDisposable，不可用 using）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnBrowseRecord_Click(object sender, EventArgs e)
        {
            AntdUI.FolderBrowserDialog dlg = new AntdUI.FolderBrowserDialog();
            if (dlg.ShowDialog() != DialogResult.OK) return;
            string path = dlg.DirectoryPath;
            if (string.IsNullOrEmpty(path)) return;

            var mgr = MonitorCamManager.Instance;
            if (mgr.Config != null)
            {
                mgr.Config.RecordDir = path;
                mgr.SaveConfig();
            }
            txtRecordDir.Text = path;
        }

        /// <summary>开始录像：策略在流程层，本页只发起</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnStartRecord_Click(object sender, EventArgs e)
        {
            // 界面按钮属手动动作，置手动模式
            MonitorCameraWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            if (!MonitorCameraWorkflow.Instance.StartRecording(MonitorCameraWorkflow.DebugRecordPrefix, out string fileName, out string failure))
            {
                AntdUI.Message.warn(this.FindForm(), failure);
                return;
            }
            if (string.IsNullOrEmpty(fileName)) return;   // 已在录像：与原行为一致，静默
            AntdUI.Message.info(this.FindForm(), "录像已开始：" + fileName);
        }

        /// <summary>停止录像</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnStopRecord_Click(object sender, EventArgs e)
        {
            // 界面按钮属手动动作，置手动模式
            MonitorCameraWorkflow.Instance.SetRunMode(WorkflowRunMode.Manual);
            MonitorCameraWorkflow.Instance.StopRecording();
        }

        #endregion

        #region 参数设置（曝光 / 增益）

        /// <summary>应用曝光/增益：解析失败回退原值；写入结果按按</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>钮外提示</remarks>
        private void btnApplyParam_Click(object sender, EventArgs e)
        {
            var mgr = MonitorCamManager.Instance;
            if (mgr.ConnectionState != MonitorCameraConnectionState.Connected)
            {
                AntdUI.Message.warn(this.FindForm(), "请先连接相机！");
                return;
            }

            if (!float.TryParse(txtExposure.Text, out float exposure) || !float.TryParse(txtGain.Text, out float gain))
            {
                AntdUI.Message.warn(this.FindForm(), "曝光/增益格式不正确！");
                return;
            }

            bool okExposure = mgr.SetExposureTime(exposure);
            bool okGain = mgr.SetGain(gain);
            if (okExposure && okGain)
            {
                AntdUI.Message.info(this.FindForm(), "参数已应用");
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), "参数应用失败，请查看日志！");
            }
        }

        #endregion

        #region 标定设置（尺度 / 光斑点 / 送丝距离测试）

        /// <summary>保存标定设置（mm/pixel 与光斑点坐标）到配置 INI</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnCalibSave_Click(object sender, EventArgs e)
        {
            var mgr = MonitorCamManager.Instance;
            if (mgr.Config == null) return;

            if (!double.TryParse(txtMmPerPixel.Text, out double mmPerPixel) ||
                !int.TryParse(txtSpotX.Text, out int spotX) ||
                !int.TryParse(txtSpotY.Text, out int spotY))
            {
                AntdUI.Message.warn(this.FindForm(), "标定参数格式不正确！");
                return;
            }

            mgr.Config.MmPerPixel = mmPerPixel;
            mgr.Config.SpotX = spotX;
            mgr.Config.SpotY = spotY;
            mgr.SaveConfig();
            AntdUI.Message.info(this.FindForm(), "标定设置已保存");
        }

        /// <summary>送丝距离测试：向 Manager 要一张软触发成品帧并显示；</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>失败文案由 Manager 给出</remarks>
        private void btnFeedTest_Click(object sender, EventArgs e)
        {
            if (!MonitorCamManager.Instance.TryAcquireCalibFrame(out Mat frame, out string failure))
            {
                AntdUI.Message.warn(this.FindForm(), failure);
                return;
            }
            imageBox1.Image = frame;
            AntdUI.Message.info(this.FindForm(), "已获取软触发图像，送丝距离分析待算法对接");
        }

        #endregion

        #region 焊接回放（日期区间文件汇总 + 缩略图后台加载 + 双层播放控件栏）

        /// <summary>初始化回放控件：提示、事件接线、默认日期区间</summary>
        /// <remarks>今天~今天</remarks>
        private void InitPlaybackUi()
        {
            _playTooltip = new AntdUI.TooltipComponent();
            _playTooltip.SetTip(btnSlowPlay, "慢放：0.8x / 0.5x / 0.3x / 0.1x / 1.0x 循环切换（当前倍速见上方速度标签）");
            _playTooltip.SetTip(btnBack5Play, "后退 5 秒");
            _playTooltip.SetTip(btnPlayPause, "播放 / 暂停切换");
            _playTooltip.SetTip(btnForward5Play, "前进 5 秒");
            _playTooltip.SetTip(btnFastPlay, "快放：1.5x / 2.0x / 2.5x / 3.0x / 1.0x 循环切换（当前倍速见上方速度标签）");
            _playTooltip.SetTip(sliderPlay, "播放进度：点击或拖动定位播放位置");

            btnSlowPlay.Click += btnSlowPlay_Click;
            btnBack5Play.Click += btnBack5Play_Click;
            btnPlayPause.Click += btnPlayPause_Click;
            btnForward5Play.Click += btnForward5Play_Click;
            btnFastPlay.Click += btnFastPlay_Click;
            sliderPlay.ValueChanged += sliderPlay_ValueChanged;
            sliderPlay.MouseEnter += sliderPlay_MouseEnter;
            sliderPlay.MouseDown += sliderPlay_MouseDown;
            sliderPlay.MouseUp += sliderPlay_MouseUp;

            imageBoxPlay.MouseMove += imageBoxPlay_MouseMove;
            imageBoxPlay.MouseLeave += imageBoxPlay_MouseLeave;
            panelPlayBar.MouseEnter += panelPlayBar_MouseEnter;
            panelPlayBar.MouseLeave += panelPlayBar_MouseLeave;

            // 首次切到回放页签才读列表
            tabsMonitor.SelectedIndexChanged += tabsMonitor_SelectedIndexChanged;

            // 默认今天到今天；订阅放在赋值之后
            DateTime today = DateTime.Today;
            uiPlayDateRange.Value = new DateTime[] { today, today };
            uiPlayDateRange.ValueChanged += uiPlayDateRange_ValueChanged;
            // 回放组件共用：打开页面先复位会话
            _playback.StopPlayback();
            WirePlaybackEvents();
            UpdatePlayPauseUi();
        }

        /// <summary>首次切到焊接回放页签时按当前日期区间汇总</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>该区间内全部录像文件</remarks>
        private void tabsMonitor_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            if (e.Value != 1)
            {
                // 离开回放页取消扫描，避免旧结果回填
                _playback.CancelScan();
                return;
            }
            // 扫描被切页取消则回来重扫
            if (_playListLoaded && _playRows.Count > 0) return;
            _playListLoaded = true;
            GetPlayRange(out DateTime start, out DateTime end);
            LoadPlaybackRange(start, end);
        }

        /// <summary>刷新回放列表</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>按当前区间重新汇总录像文件</remarks>
        private void btnRefreshPlay_Click(object sender, EventArgs e)
        {
            GetPlayRange(out DateTime start, out DateTime end);
            LoadPlaybackRange(start, end);
        }

        /// <summary>日期区间变化</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>按新区间重新汇总录像文件</remarks>
        private void uiPlayDateRange_ValueChanged(object sender, EventArgs e)
        {
            GetPlayRange(out DateTime start, out DateTime end);
            LoadPlaybackRange(start, end);
        }

        /// <summary>取当前日期区间</summary>
        /// <param name="start">起始值。</param>
        /// <param name="end">结束值。</param>
        /// <remarks>闭区间；空值回退今天，起止倒置自动交换</remarks>
        private void GetPlayRange(out DateTime start, out DateTime end)
        {
            DateTime[] range = uiPlayDateRange.Value;
            if (range == null || range.Length < 2)
            {
                start = DateTime.Today;
                end = DateTime.Today;
                return;
            }
            start = range[0].Date;
            end = range[1].Date;
            if (start > end)
            {
                DateTime temp = start;
                start = end;
                end = temp;
            }
        }

        /// <summary>接线回放事件，统一封送 UI 线程</summary>
        private void WirePlaybackEvents()
        {
            _playback.ScanStarted += OnPlayScanStarted;
            _playback.ScanCompleted += OnPlayScanCompleted;
            _playback.ScanCrashed += OnPlayScanCrashed;
            _playback.ThumbnailReady += OnPlayThumbnailReady;
            _playback.FrameReady += OnPlayFrameReady;
            _playback.ProgressChanged += OnPlayProgress;
            _playback.PlayStateChanged += OnPlayStateChanged;
        }

        /// <summary>退订回放事件，组件归算法层不释放</summary>
        private void UnwirePlaybackEvents()
        {
            _playback.ScanStarted -= OnPlayScanStarted;
            _playback.ScanCompleted -= OnPlayScanCompleted;
            _playback.ScanCrashed -= OnPlayScanCrashed;
            _playback.ThumbnailReady -= OnPlayThumbnailReady;
            _playback.FrameReady -= OnPlayFrameReady;
            _playback.ProgressChanged -= OnPlayProgress;
            _playback.PlayStateChanged -= OnPlayStateChanged;
        }

        /// <summary>扫描开始：清空旧结果并显示忙碌文案</summary>
        /// <param name="caption">忙碌态文案</param>
        private void OnPlayScanStarted(string caption)
        {
            ClearPlaybackRows();
            _selectedPlayRow = null;
            SetPlayCaption(caption);
        }

        /// <summary>扫描完成：回填行与标题文案（后台线程 → UI）</summary>
        /// <param name="result">扫描结果</param>
        private void OnPlayScanCompleted(PlaybackScanResult result)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<PlaybackScanResult>(OnPlayScanCompleted), result); } catch { }
                return;
            }
            if (result == null) return;
            if (result.Error != null)
            {
                SetPlayCaption("读取失败：" + result.Error);
                GlobalCommData.ShowLog("监控相机回放", result.Error, MessageLevel.Warning);
                AntdUI.Message.warn(this.FindForm(), result.Error);
                return;
            }

            for (int i = 0; i < result.Files.Count; i++) _playRows.Add(ToPlaybackRow(result.Files[i]));
            SetPlayCaption(result.Caption);
        }

        /// <summary>扫描线程异常：提示并留痕（后台线程 → UI）</summary>
        /// <param name="message">异常原文</param>
        private void OnPlayScanCrashed(string message)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(OnPlayScanCrashed), message); } catch { }
                return;
            }
            GlobalCommData.ShowLog("监控相机回放", "读取录像目录失败 " + message, MessageLevel.Warning);
            SetPlayCaption("读取失败：" + message);
            AntdUI.Message.warn(this.FindForm(), "读取录像目录失败：" + message);
        }

        /// <summary>缩略图就绪：按路径回填对应行并刷新（后台线程 → UI）</summary>
        /// <param name="info">已回填 Thumbnail 的文件行</param>
        private void OnPlayThumbnailReady(PlaybackFileInfo info)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<PlaybackFileInfo>(OnPlayThumbnailReady), info); } catch { }
                return;
            }
            for (int i = 0; i < _playRows.Count; i++)
            {
                if (_playRows[i].FullPath != info.FullPath) continue;
                _playRows[i].Thumbnail = info.Thumbnail;
                playTable.Refresh();
                return;
            }
            // 行已不在（切页 / 换区间），收回缩略图
            info.Thumbnail?.Dispose();
        }

        /// <summary>播放帧就绪：上屏（后台解码线程 → UI）</summary>
        /// <param name="frame">成品帧</param>
        private void OnPlayFrameReady(Mat frame)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<Mat>(OnPlayFrameReady), frame); } catch { }
                return;
            }
            if (frame == null || frame.IsEmpty) return;
            imageBoxPlay.Image = frame;
        }

        /// <summary>同步进度条与时间标签，回写不触发定位</summary>
        /// <param name="frameIndex">当前帧序号</param>
        /// <param name="totalFrames">总帧数（0 表示未知）</param>
        /// <param name="fps">帧率</param>
        private void OnPlayProgress(int frameIndex, int totalFrames, double fps)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int, int, double>(OnPlayProgress), frameIndex, totalFrames, fps); } catch { }
                return;
            }
            _syncPlaySlider = true;
            sliderPlay.MaxValue = totalFrames > 0 ? totalFrames : 1;
            if (sliderPlay.Value != frameIndex) sliderPlay.Value = frameIndex;
            _syncPlaySlider = false;
            UpdatePlayTimeLabel(frameIndex, totalFrames, fps);
        }

        /// <summary>播放状态：同步播放/暂停图标与速度标签</summary>
        /// <param name="isPlaying">是否播放中</param>
        /// <param name="speed">当前倍速</param>
        private void OnPlayStateChanged(bool isPlaying, double speed)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<bool, double>(OnPlayStateChanged), isPlaying, speed); } catch { }
                return;
            }
            btnPlayPause.IconSvg = isPlaying ? "PauseCircleOutlined" : "PlayCircleOutlined";
            lblPlaySpeed.Text = "速度 " + FormatSpeed(speed);
        }

        /// <summary>流程层文件行 → 本页表格行（仅视图映射，不解码）</summary>
        /// <param name="info">流程层产出的文件行</param>
        /// <returns>表格行</returns>
        private static PlaybackRow ToPlaybackRow(PlaybackFileInfo info)
        {
            return new PlaybackRow
            {
                FileName = info.FileName,
                FullPath = info.FullPath,
                Modified = info.Modified,
                Size = info.Size
            };
        }

        /// <summary>按区间汇总录像文件，本页只传区间</summary>
        /// <param name="start">起始值。</param>
        /// <param name="end">结束值。</param>
        /// <remarks>区间即文件夹选择器，选完直接出文件</remarks>
        private void LoadPlaybackRange(DateTime start, DateTime end)
        {
            _playback.LoadRange(start, end);
        }

        /// <summary>写入列表标题标签（仅 UI 线程；页面销毁后忽略）</summary>
        /// <param name="text">文本内容。</param>
        private void SetPlayCaption(string text)
        {
            if (IsDisposed || Disposing) return;
            label1.Text = text ?? string.Empty;
        }

        /// <summary>清空列表并释放缩略图；在跑的后台解码线程由流程层代数自行退出</summary>
        private void ClearPlaybackRows()
        {
            for (int i = 0; i < _playRows.Count; i++)
            {
                Bitmap thumb = _playRows[i].Thumbnail;
                thumb?.Dispose();
            }
            _playRows.Clear();
        }

        /// <summary>列表点击：录像文件右键弹出「回放」上下文菜单</summary>
        /// <remarks>区间汇总后表格只含文件行，无下钻</remarks>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void playTable_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            _selectedPlayRow = e.Record as PlaybackRow;
            if (_selectedPlayRow == null) return;
            if (e.Button != MouseButtons.Right) return;

            AntdUI.ContextMenuStrip.open(playTable, it => PlaySelected(), new AntdUI.IContextMenuStripItem[]
            {
                new AntdUI.ContextMenuStripItem("回放")
            });
        }

        /// <summary>回放选中文件：解码在流程层</summary>
        private void PlaySelected()
        {
            if (_selectedPlayRow == null) return;
            string error = _playback.PlayFile(_selectedPlayRow.FullPath);
            if (!string.IsNullOrEmpty(error)) AntdUI.Message.warn(this.FindForm(), error);
        }

        /// <summary>拖动/点击进度条后定位到指定帧</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void sliderPlay_ValueChanged(object sender, AntdUI.IntEventArgs e)
        {
            if (_syncPlaySlider || !_playback.HasSource) return;
            _playback.SeekToFrame(e.Value);
        }

        /// <summary>后退 5 秒</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnBack5Play_Click(object sender, EventArgs e)
        {
            _playback.SeekBySeconds(-5);
        }

        /// <summary>前进 5 秒</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnForward5Play_Click(object sender, EventArgs e)
        {
            _playback.SeekBySeconds(5);
        }

        /// <summary>慢放：按 0.8→0.5→0.3→0.1→1.0 循环切换</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>以当前位置为新锚点，播放位置不重置</remarks>
        private void btnSlowPlay_Click(object sender, EventArgs e)
        {
            _playback.CycleSlow();
        }

        /// <summary>快放：按 1.5→2.0→2.5→3.0→1.0 循环切换</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>以当前位置为新锚点，播放位置不重置</remarks>
        private void btnFastPlay_Click(object sender, EventArgs e)
        {
            _playback.CycleFast();
        }

        /// <summary>播放/暂停切换：无打开文件时提示先选录像</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnPlayPause_Click(object sender, EventArgs e)
        {
            string error = _playback.TogglePlayPause();
            if (!string.IsNullOrEmpty(error)) AntdUI.Message.info(this.FindForm(), error);
        }

        /// <summary>同步播放/暂停按钮图标（初始态）</summary>
        private void UpdatePlayPauseUi()
        {
            btnPlayPause.IconSvg = _playback.IsPlaying ? "PauseCircleOutlined" : "PlayCircleOutlined";
        }

        /// <summary>倍速文本（一位小数 + x）</summary>
        /// <returns>结果文本。</returns>
        private static string FormatSpeed(double speed)
        {
            return string.Format("{0:F1}x", speed);
        }

        /// <summary>刷新「当前时间 / 总时长」显示</summary>
        /// <param name="frameIndex">当前帧序号</param>
        /// <param name="totalFrames">总帧数（0 表示未知）</param>
        /// <param name="fps">帧率</param>
        private void UpdatePlayTimeLabel(int frameIndex, int totalFrames, double fps)
        {
            double current = fps > 0 ? frameIndex / fps : 0;
            double total = fps > 0 ? totalFrames / fps : 0;
            lblPlayTime.Text = FormatPlayTime(current) + " / " + FormatPlayTime(total);
        }

        /// <summary>秒数格式化为 mm:ss（超一小时为 HH:mm:ss）</summary>
        /// <returns>结果文本。</returns>
        private static string FormatPlayTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int totalSeconds = (int)Math.Round(seconds);
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int secs = totalSeconds % 60;
            if (hours > 0)
                return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, secs);
            return string.Format("{0:00}:{1:00}", minutes, secs);
        }

        /// <summary>鼠标在画面底部热区显示播放控件栏</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>其余区域隐藏</remarks>
        private void imageBoxPlay_MouseMove(object sender, MouseEventArgs e)
        {
            SetPlayBarVisible(e.Y >= imageBoxPlay.ClientSize.Height - PlayBarHotZoneHeight);
        }

        /// <summary>鼠标离开画面即隐藏播放控件栏</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>光标已落在控件栏内则保持显示</remarks>
        private void imageBoxPlay_MouseLeave(object sender, EventArgs e)
        {
            if (panelPlayBar.Visible &&
                panelPlayBar.RectangleToScreen(panelPlayBar.ClientRectangle).Contains(Cursor.Position)) return;
            SetPlayBarVisible(false);
        }

        /// <summary>鼠标进入控件栏保持显示</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void panelPlayBar_MouseEnter(object sender, EventArgs e)
        {
            SetPlayBarVisible(true);
        }

        /// <summary>鼠标离开控件栏：确实移出栏区域才隐藏</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>移入子按钮不算离开</remarks>
        private void panelPlayBar_MouseLeave(object sender, EventArgs e)
        {
            if (panelPlayBar.RectangleToScreen(panelPlayBar.ClientRectangle).Contains(Cursor.Position)) return;
            SetPlayBarVisible(false);
        }

        /// <summary>显隐播放控件栏（半透明由 Panel 的 50% Alpha 背景承载）</summary>
        private void SetPlayBarVisible(bool visible)
        {
            if (panelPlayBar.Visible == visible) return;
            panelPlayBar.Visible = visible;
        }

        /// <summary>鼠标进入进度条：光标为手型抓取样式</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void sliderPlay_MouseEnter(object sender, EventArgs e)
        {
            sliderPlay.Cursor = Cursors.Hand;
        }

        /// <summary>按下进度条：光标为横向拖动样式</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void sliderPlay_MouseDown(object sender, MouseEventArgs e)
        {
            sliderPlay.Cursor = Cursors.SizeWE;
        }

        /// <summary>松开进度条：光标恢复手型</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void sliderPlay_MouseUp(object sender, MouseEventArgs e)
        {
            sliderPlay.Cursor = Cursors.Hand;
        }

        #endregion

        #region 状态显示

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            // 顶栏连接态随节拍同步
            UpdateConnectionUi();
        }

        #endregion
    }
}
