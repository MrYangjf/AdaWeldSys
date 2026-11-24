using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.ProductFileControl;
using AdaWeldSystem.Comm.SystemComm;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using AdaWeldSystem.UIDesign.SubDialog.DataReviewerDialog;


namespace AdaWeldSystem.UIDesign.ControlPage
{
    /// <summary>生产数据界面</summary>
    public partial class DataReviewerPage : UserControl
    {
        // 数据行 = 一条生产记录（产品库按日期分表的一行）
        // 继承 NotifyProperty：Selected 变更时通知表格刷新 checkbox 单元格（AntdUI Table 的行模型契约）
        public class DataReviewRow : AntdUI.NotifyProperty
        {
            private bool _selected;

            /// <summary>多选勾选态（对应列表多选列 ColumnCheck("Selected")）</summary>
            public bool Selected
            {
                get { return _selected; }
                set { if (_selected == value) return; _selected = value; OnPropertyChanged(); }
            }

            public int No { get; set; }
            public string Sn { get; set; }
            public string Time { get; set; }
            public string Status { get; set; }
            public string Result { get; set; }
            public string Info { get; set; }

            /// <summary>起焊时间原值（仅用于推算会话基名）</summary>
            public DateTime StartTime { get; set; }
        }

        AntdUI.AntList<DataReviewRow> _rows;
        private DataReviewRow _selectedRow;

        /// <summary>当前区间整段记录：一次读取，分页只切片</summary>
        private List<DataReviewRow> _allRows;

        /// <summary>首次可见已加载，避免反复切页重读</summary>
        private bool _firstShownLoaded;

        /// <summary>节流：不足该毫秒数直接返回，防拖慢界面</summary>
        private const int HoverThrottleMs = 60;

        /// <summary>悬停命中容差</summary>
        private const int HoverPixelRadius = 18;

        private DateTime _lastHoverCalc = DateTime.MinValue;

        /// <summary>图表悬停提示组件（AntdUI TooltipComponent，范式见 MainPage.InitDeviceSwitchButtons）</summary>
        private AntdUI.TooltipComponent _tooltip;

        /// <summary>当前悬停读数文本；未命中为 null</summary>
        private string _hoverText;

        /// <summary>最近汇总文本：悬停清除后回填，避免丢失</summary>
        private string _lastSummaryText = string.Empty;

        /// <summary>会话曲线集合</summary>
        private sealed class SessionCharts
        {
            private readonly Dictionary<string, ChartVar> _charts = new Dictionary<string, ChartVar>();

            /// <summary>取某系列的曲线；该会话没有此系列时返回 null。</summary>
            public ChartVar this[string seriesName]
            {
                get
                {
                    return _charts.TryGetValue(seriesName, out ChartVar cv) ? cv : null;
                }
            }

            /// <summary>本会话累计点数（0 表示无可绘数据）。</summary>
            public int PointCount
            {
                get
                {
                    int sum = 0;
                    foreach (KeyValuePair<string, ChartVar> entry in _charts) sum += entry.Value.Count();
                    return sum;
                }
            }
            /// <summary>向某系列追加一个点（系列不存在时自动创建）。</summary>
            public void Add(string seriesName, double x, double y)
            {
                if (!_charts.TryGetValue(seriesName, out ChartVar cv))
                {
                    cv = new ChartVar();
                    _charts[seriesName] = cv;
                }
                cv.Add(x, y);
            }

        }

        /// <summary>装载的会话（已剔除无文件者），索引即序号</summary>
        private List<SessionCharts> _sessions = new List<SessionCharts>();

        // 系列名称常量（与各模组的 Checkbox 一一对应）
        private const string NameSeamWidth = "焊缝宽度";
        private const string NameRobotSpeed = "机器人运动速度";
        private const string NameFeedSpeed = "送丝速度";
        private const string NameLaserPower = "激光功率";
        private const string NameCspOutY = "CSPoutY(出图)";
        private const string NameCspInY = "CSPinY(入图)";
        private const string NameAxisY = "AxisY(轴反馈)";
        private const string NameLaPosition = "LA对焦位置";
        private const string NameGalvoAmp = "振镜摆幅";
        private const string NameHeadTemp = "头部温度";
        private const string NameRobotY = "机器人Y坐标";
        private const string NameRobotZ = "机器人Z坐标";
        private const string NameTaPosition = "TA送丝Z位置";
        private const string NameAdjust = "调整点";

        // 系列线条颜色（Ant Design 标准色板，与 AntdUI 主题同源；调整点固定红色）
        private static readonly Color ColorSeamWidth = Color.FromArgb(24, 144, 255);
        private static readonly Color ColorRobotSpeed = Color.FromArgb(114, 46, 209);
        private static readonly Color ColorFeedSpeed = Color.FromArgb(82, 196, 26);
        private static readonly Color ColorLaserPower = Color.FromArgb(250, 173, 20);
        private static readonly Color ColorCspY = Color.FromArgb(19, 194, 194);
        private static readonly Color ColorRobotY = Color.FromArgb(140, 100, 60);
        private static readonly Color ColorAxisY = Color.FromArgb(235, 47, 150);
        private static readonly Color ColorLaPosition = Color.FromArgb(0, 114, 192);
        private static readonly Color ColorGalvoAmp = Color.FromArgb(214, 137, 16);
        private static readonly Color ColorHeadTemp = Color.FromArgb(207, 62, 108);
        private static readonly Color ColorRobotPos = Color.FromArgb(83, 63, 155);
        private static readonly Color ColorTaPosition = Color.FromArgb(56, 118, 29);

        /// <summary>系列项：Checkbox + 线色 + 系列名</summary>
        private sealed class SeriesItem
        {
            public AntdUI.Checkbox Box { get; set; }

            /// <summary>勾选框文本（图例名可带单位后缀）</summary>
            public string Text { get; set; }

            public string Name { get; set; }
            public Color Color { get; set; }

            /// <summary>取该系列在指定会话中的曲线</summary>
            public Func<SessionCharts, ChartVar> GetChart
            {
                get { return s => s[Name]; }
            }
        }

        /// <summary>模组：每个选项一组，组内 Checkbox 多选。</summary>
        private sealed class SeriesGroup
        {
            public string Title { get; set; }

            public List<SeriesItem> Items { get; set; }
        }

        // 五大类模组：顺序即下拉顺序，勿调整
        private static readonly string[] GroupTitles = { "接头", "激光头", "机器人", "送丝机", "激光器功率" };

        private readonly List<SeriesGroup> _groups = new List<SeriesGroup>();

        /// <summary>模组切换时抑制各 Checkbox 的 CheckedChanged（回填勾选态会连带触发）</summary>
        private bool _suppressSeriesEvent;

        /// <summary>诊断计数：每进程最多 5 条，避免污染日志</summary>
        private static int _diagCount;

        public double MinValueRobotX, MaxValueRobotX;

        public DataReviewerPage()
        {
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<DataReviewRow>();
            _allRows = new List<DataReviewRow>();
            uiDataGridView1.Binding(_rows);
            uiDataGridView1.CellClick += uiDataGridView1_CellClick;
            InitSeriesGroups();
            // 初始筛选区间：今天 ~ 今天（DatePickerRange.Value 为两元素数组）
            DateTime today = DateTime.Today;
            uiDateRange.Value = new DateTime[] { today, today };
            // 区间变化才加载（订阅放在初始赋值之后）
            uiDateRange.ValueChanged += uiDateRange_ValueChanged;
            // 未加载态：中性提示，不判断有无记录
            uiSymbolLabel1.Text = "尚未加载生产记录（选择日期区间后点刷新）";
            // 悬停读数：MouseMove 高频事件，处理器内做节流（见 HoverThrottleMs）
            formsPlotData.MouseMove += formsPlotData_MouseMove;
            // Pagination 可选页大小
            pagination1.PageSizeOptions = new int[] { 20, 50, 100, 200 };
            pagination1.ValueChanged += pagination1_ValueChanged;
            // 未挂载 Checkbox 不在控件树内，须显式释放
            this.Disposed += DataReviewerPage_Disposed;
        }

        /// <summary>页面销毁时释放各模组勾选框。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void DataReviewerPage_Disposed(object sender, EventArgs e)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                List<SeriesItem> items = _groups[i].Items;
                for (int k = 0; k < items.Count; k++)
                {
                    AntdUI.Checkbox box = items[k].Box;
                    if (box == null) continue;
                    box.Dispose();
                    items[k].Box = null;
                }
            }
        }

        /// <summary>构建五大类曲线模组：填 Select 选项 + 逐模组创建 Checkbox。</summary>
        private void InitSeriesGroups()
        {
            _groups.Clear();
            _groups.Add(BuildGroup(0, new[]
            {
                MakeItem("焊缝宽度", NameSeamWidth, ColorSeamWidth)
            }));
            _groups.Add(BuildGroup(1, new[]
            {
                MakeItem("CSPoutY(出图)", NameCspOutY, ColorCspY),
                MakeItem("CSPinY(入图)", NameCspInY, ColorRobotY),
                MakeItem("AxisY(轴反馈)", NameAxisY, ColorAxisY),
                MakeItem("LA对焦位置", NameLaPosition, ColorLaPosition),
                MakeItem("振镜摆幅", NameGalvoAmp, ColorGalvoAmp),
                MakeItem("头部温度", NameHeadTemp, ColorHeadTemp)
            }));
            _groups.Add(BuildGroup(2, new[]
            {
                MakeItem("机器人Y坐标", NameRobotY, ColorRobotPos),
                MakeItem("机器人Z坐标", NameRobotZ, ColorGalvoAmp),
                MakeItem("机器人运动速度", NameRobotSpeed, ColorRobotSpeed)
            }));
            _groups.Add(BuildGroup(3, new[]
            {
                MakeItem("送丝速度", NameFeedSpeed, ColorFeedSpeed),
                MakeItem("TA送丝Z位置", NameTaPosition, ColorTaPosition)
            }));
            _groups.Add(BuildGroup(4, new[]
            {
                MakeItem("激光功率", NameLaserPower, ColorLaserPower)
            }));

            selSeriesGroup.Items.Clear();
            for (int i = 0; i < GroupTitles.Length; i++)
            {
                selSeriesGroup.Items.Add(GroupTitles[i]);
            }
            // 先赋值再订阅，初始化不触发切换回调
            selSeriesGroup.SelectedIndex = 0;
            selSeriesGroup.SelectedIndexChanged += selSeriesGroup_SelectedIndexChanged;
            ShowGroupOnly(0);
        }

        /// <summary>创建系列项描述（Checkbox 由 <see cref="BuildGroup"/> 建）。</summary>
        /// <param name="text">文本内容。</param>
        /// <param name="name">名称。</param>
        private static SeriesItem MakeItem(string text, string name, Color color)
        {
            return new SeriesItem { Name = name, Color = color, Text = text };
        }

        /// <summary>构建模组：逐项建 Checkbox，暂不挂容器。</summary>
        /// <param name="index">索引。</param>
        private SeriesGroup BuildGroup(int index, SeriesItem[] items)
        {
            var group = new SeriesGroup { Title = GroupTitles[index], Items = new List<SeriesItem>() };
            for (int i = 0; i < items.Length; i++)
            {
                SeriesItem item = items[i];
                var box = new AntdUI.Checkbox
                {
                    Name = "chkSeries_" + index + "_" + i,
                    Text = item.Text,
                    Checked = true,
                    CheckState = CheckState.Checked,
                    Cursor = Cursors.Hand,
                    Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular, GraphicsUnit.Point, (byte)134),
                    ForeColor = Color.FromArgb(48, 48, 48),
                    Size = new Size(EstimateBoxWidth(item.Text), 28),
                };
                box.CheckedChanged += ChkSeries_CheckedChanged;
                item.Box = box;
                group.Items.Add(item);
            }
            return group;
        }

        /// <summary>按显示文本估算 Checkbox 宽度。</summary>
        /// <param name="text">文本内容。</param>

        /// <returns>结果码。</returns>
        private static int EstimateBoxWidth(string text)
        {
            int width = 34;
            if (string.IsNullOrEmpty(text)) return width;
            foreach (char c in text)
            {
                width += c > 127 ? 16 : 9;
            }
            return width;
        }

        /// <summary>模组切换：只显示选中模组的 Checkbox 面板，并按该模组内</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void selSeriesGroup_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            int index = selSeriesGroup.SelectedIndex;
            if (index < 0) return;
            ShowGroupOnly(index);
            RebuildChartDatasets();
        }

        /// <summary>切换模组：移除旧组 Checkbox、把新组 Checkbox 加入 <see cref="SelectFlowPanel"/>。</summary>
        /// <param name="index">索引。</param>
        private void ShowGroupOnly(int index)
        {
            if (index < 0 || index >= _groups.Count) return;
            _suppressSeriesEvent = true;
            try
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    List<SeriesItem> items = _groups[i].Items;
                    for (int k = 0; k < items.Count; k++)
                    {
                        AntdUI.Checkbox box = items[k].Box;
                        if (box == null) continue;
                        if (i == index)
                        {
                            SelectFlowPanel.Controls.Add(box);
                        }
                        else
                        {
                            SelectFlowPanel.Controls.Remove(box);
                        }
                    }
                }
                // 逆序入流：须位于末位才保持最左
                SelectFlowPanel.Controls.Remove(selSeriesGroup);
                SelectFlowPanel.Controls.Add(selSeriesGroup);
            }
            finally
            {
                _suppressSeriesEvent = false;
            }
        }

        /// <summary>初始化表格列：须代码后置设置 Columns。</summary>
        private void InitTableColumns()
        {
            uiDataGridView1.Columns = new AntdUI.ColumnCollection
            {
                // 双参 ColumnCheck = 表头渲染纯文字 title，无全选 checkbox（AntdUI-Reference §4.2 源码确证）；
                // 全选/全取消/反选由筛选栏 panelSelectBar 三按钮承担；key 须等于 DataReviewRow.Selected，Fixed 冻结首列
                new AntdUI.ColumnCheck("Selected", "选取") { Width = "60", Fixed = true },
                // Column(key, title)：key 必须等于 DataReviewRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("No", "No.") { Width = "40" },
                new AntdUI.Column("Sn", "产品码") { Width = "90" },
                new AntdUI.Column("Time", "起焊时间") { Width = "110" },
                new AntdUI.Column("Status", "状态") { Width = "70" },
                new AntdUI.Column("Result", "结果") { Width = "60" },
                new AntdUI.Column("Info", "异常原因") { Width = "140" }
            };
        }

        /// <summary>区间选择变化：按所选区间一次性读取生产记录</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void uiDateRange_ValueChanged(object sender, EventArgs e)
        {
            // 不可见时不查：仅用户可见时生效
            if (!IsReadyForQuery()) return;
            DateTime[] range = uiDateRange.Value;
            if (range == null || range.Length < 2) return;
            InitPCLDir(range[0], range[1], true);
        }

        /// <summary>是否允许查询（唯一判据：已进入运行态）</summary>
        /// <returns>true = 允许查询；false = 启动/初始化链路拒绝</returns>
        private bool IsReadyForQuery()
        {
            if (GlobalCommData.IsAppRunning) return true;
            DiagLog("门拒绝", "应用尚未进入正常运行态，拒绝历史数据查询");
            return false;
        }

        /// <summary>诊断出口：记录时间/来源/可见性/堆栈（限流 5 条）</summary>
        /// <param name="scene">场景标记（门拒绝）</param>
        /// <param name="detail">补充信息</param>
        private void DiagLog(string scene, string detail)
        {
            if (System.Threading.Interlocked.Increment(ref _diagCount) > 5) return;
            Form host = FindForm();
            GlobalCommData.ShowLog("DataReviewDiag",
                string.Format("【{0}】{1} 页面Visible={2} 宿主存在={3} 宿主Visible={4} 运行态={5} 线程={6}{7}堆栈={8}",
                    scene, detail, Visible, host != null, host != null && host.Visible,
                    GlobalCommData.IsAppRunning,
                    System.Threading.Thread.CurrentThread.ManagedThreadId,
                    Environment.NewLine,
                    new System.Diagnostics.StackTrace(true).ToString()),
                MessageLevel.Warning);
        }

        /// <summary>加载时间区间的生产记录到表格。</summary>
        /// <param name="start">起始位置。</param>
        /// <param name="end">结束位置。</param>
        /// <param name="userInitiated">true=用户主动查询；false=进入页面自动加载</param>
        private void InitPCLDir(DateTime start, DateTime end, bool userInitiated)
        {
            // 硬闸：启动链路不查库，判据=运行态标志
            if (!GlobalCommData.IsAppRunning) return;

            _rows.Clear();
            _selectedRow = null;
            _allRows = LoadProductRecords(start, end);

            if (_allRows.Count == 0)
            {
                pagination1.Total = 0;
                pagination1.Current = 1;
                // "已查询但为空"：仅在用户主动查询后出现
                uiSymbolLabel1.Text = "所选区间无生产记录";
                // 首进自动加载不打扰：弹窗只由主动查询触发
                if (userInitiated)
                {
                    AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm,
                        string.Format("{0} 至 {1} 没有生产记录。",
                            start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd")));
                }
                return;
            }

            uiSymbolLabel1.Text = string.Format("已加载 {0} 条生产记录，点击某行回溯曲线/3D", _allRows.Count);
            // 重置分页并填第一页，分页只做切片
            pagination1.Total = _allRows.Count;
            pagination1.Current = 1;
            FillPage();
        }

        /// <summary>一次性读取区间生产记录并转为表格行。</summary>
        /// <param name="start">区间起始日（含）</param>
        /// <param name="end">区间结束日（含）</param>
        /// <returns>生产记录行列表；无数据返回空列表</returns>
        private List<DataReviewRow> LoadProductRecords(DateTime start, DateTime end)
        {
            var list = new List<DataReviewRow>();
            System.Data.DataTable table;
            try
            {
                table = ProductFileRecordManager.Instance.SelectProductRecords(start, end);
            }
            catch (Exception ex)
            {
                // 提示只做结果反馈，BUG 记 LOG 便于定位；
                // 读取失败按零行返回，统一提示无记录
                GlobalCommData.ShowLog("DataReviewerPage",
                    string.Format("读取生产记录失败 {0} 至 {1} {2}",
                        start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), ex.Message),
                    MessageLevel.Error);
                return list;
            }
            if (table == null) return list;

            int no = 0;
            foreach (System.Data.DataRow row in table.Rows)
            {
                no++;
                string timeText = GetCellText(row, "Time");
                DateTime startTime = ParseRecordTime(timeText, start);
                // 合并区间后可能有非首日行，基准取该行日
                if (timeText.Length >= 8 &&
                    DateTime.TryParseExact(timeText.Substring(0, 8), "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime dayBase))
                {
                    startTime = ParseRecordTime(timeText, dayBase);
                }
                list.Add(new DataReviewRow
                {
                    No = no,
                    Sn = GetCellText(row, "SN"),
                    Time = timeText,
                    Status = GetCellText(row, "Status"),
                    Result = GetCellText(row, "Result"),
                    Info = GetCellText(row, "Info"),
                    StartTime = startTime
                });
            }
            return list;
        }

        /// <summary>安全读取单元格（缺失或空值返回空串）。</summary>
        /// <param name="row">行数据。</param>
        /// <returns>结果文本。</returns>
        private static string GetCellText(System.Data.DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column)) return string.Empty;
            object value = row[column];
            return value == null || value == DBNull.Value ? string.Empty : value.ToString();
        }

        /// <summary>解析时间列（yyyyMMddHHmmss），失败回退到当天零点。</summary>
        private static DateTime ParseRecordTime(string timeText, DateTime fallbackDate)
        {
            if (!string.IsNullOrEmpty(timeText) &&
                DateTime.TryParseExact(timeText, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }
            return fallbackDate.Date;
        }

        /// <summary>构建单折线图：标题 + 坐标轴标签（ScottPlot.FormsPlot 4.1.74）。</summary>
        private void AfterInitializeUI()
        {
            // 高 DPI：DpiStretch=true 会模糊，置 false
            formsPlotData.Configuration.DpiStretch = false;
            formsPlotData.Plot.Title("焊接过程参数（按机器人 X 轴）");
            formsPlotData.Plot.XLabel("机器人 X 轴");
            formsPlotData.Plot.YLabel("参数值");
            formsPlotData.Refresh();
        }

        /// <summary>勾选状态控制数据线显隐，调整点始终显示。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void ChkSeries_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (_suppressSeriesEvent) return;
            RebuildChartDatasets();
        }

        /// <summary>按当前模组勾选状态重建图表数据集。</summary>
        private void RebuildChartDatasets()
        {
            formsPlotData.Plot.Clear();
            // 标题/轴随 Clear 被清，重建后须重设
            formsPlotData.Plot.Title("焊接过程参数（按机器人 X 轴）");
            formsPlotData.Plot.XLabel("机器人 X 轴");
            formsPlotData.Plot.YLabel("参数值");
            int index = selSeriesGroup.SelectedIndex;
            if (index >= 0 && index < _groups.Count)
            {
                List<SeriesItem> items = _groups[index].Items;
                for (int i = 0; i < items.Count; i++)
                {
                    SeriesItem item = items[i];
                    if (item.Box == null || !item.Box.Checked) continue;
                    for (int s = 0; s < _sessions.Count; s++)
                    {
                        AddSeries(SessionLabel(item.Name, s), item.GetChart(_sessions[s]), item.Color);
                    }
                }
            }
            for (int s = 0; s < _sessions.Count; s++)
            {
                AddSeries(SessionLabel(NameAdjust, s), _sessions[s][NameAdjust], Color.Red);
            }
            formsPlotData.Plot.AxisAuto();
            formsPlotData.Plot.Legend();
            formsPlotData.Refresh();
        }

        /// <summary>图例文本：多会话带 #序号，单会话不加。</summary>
        /// <returns>结果文本。</returns>
        private string SessionLabel(string seriesName, int sessionIndex)
        {
            return _sessions.Count > 1
                ? string.Format("{0}#{1}", seriesName, sessionIndex + 1)
                : seriesName;
        }

        /// <summary>以散点折线加入坐标序列（调整点为红色）。</summary>
        /// <param name="name">名称。</param>
        private void AddSeries(string name, ChartVar cv, Color color)
        {
            if (cv == null) return;
            int count = cv.Count();
            if (count < 1) return;
            double[] xs = new double[count];
            double[] ys = new double[count];
            for (int i = 0; i < count; i++)
            {
                xs[i] = cv.X[i];
                ys[i] = cv.Y[i];
            }
            formsPlotData.Plot.AddScatter(xs, ys, color, 2, markerSize: 0, label: name);
        }

        private void uiDataGridView1_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            _selectedRow = e.Record as DataReviewRow;
            if (e.Button == MouseButtons.Right)
            {
                AntdUI.ContextMenuStrip.open(uiDataGridView1, it => Datashow(), new AntdUI.IContextMenuStripItem[]
                {
                    new AntdUI.ContextMenuStripItem("显示数据")
                });
            }
        }

        // CellClick 捕获当前选中行（AntdUI.Table 无 SelectedItem），右键行时弹出上下文菜单
private void btnRenerView_Click(object sender, EventArgs e)
        {
            var parentForm = this.FindForm() as AntdUI.Window;
            using (var helixForm = new HelixRenderForm(parentForm))
            {
                helixForm.Size = new System.Drawing.Size(900, 600);
                AntdUI.Modal.open(new AntdUI.Modal.Config(parentForm, "3D数据回溯", helixForm, TType.None)
                {
                    BtnHeight = 0,
                    MaskClosable = true,
                    CloseIcon = true,
                });
            }
        }

        private void BtnRecordSetting_Click(object sender, EventArgs e)
        {
            var parentForm = this.FindForm() as AntdUI.Window;
            using (var configForm = new RecordConfigForm(parentForm))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(parentForm, "记录配置", configForm, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }

        /// <summary>刷新按钮：按当前区间重载生产记录。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnRefreshDate_Click(object sender, EventArgs e)
        {
            DateTime[] range = uiDateRange.Value;
            if (range == null || range.Length < 2)
            {
                InitPCLDir(DateTime.Today, DateTime.Today, true);
                return;
            }
            InitPCLDir(range[0], range[1], true);
        }

        /// <summary>鼠标悬停读点值，移到数据点附近才显示。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void formsPlotData_MouseMove(object sender, MouseEventArgs e)
        {
            DateTime now = DateTime.Now;
            if ((now - _lastHoverCalc).TotalMilliseconds < HoverThrottleMs) return;
            _lastHoverCalc = now;

            HoverHit hit = FindNearestPoint(e.X, e.Y);
            if (hit.SeriesName == null)
            {
                ClearHoverTip();
                return;
            }
            ShowHoverTip(hit);
        }

        /// <summary>在当前勾选显示的系列里找离鼠标最近的点。</summary>
        /// <param name="mouseX">鼠标在 <c>formsPlotData</c> 内的 X 像素</param>
        /// <param name="mouseY">鼠标在 <c>formsPlotData</c> 内的 Y 像素</param>
        /// <returns>命中返回 HoverHit；无可见数据或超容差即未命中。</returns>
        private HoverHit FindNearestPoint(int mouseX, int mouseY)
        {
            var none = new HoverHit();
            if (MinValueRobotX > MaxValueRobotX) return none;

            GetPlotWindow(out double xMin, out double xMax, out double yMin, out double yMax);
            double xSpan = xMax - xMin;
            double ySpan = yMax - yMin;
            if (xSpan <= 0 || ySpan <= 0) return none;

            int plotWidth = Math.Max(1, formsPlotData.Width);
            int plotHeight = Math.Max(1, formsPlotData.Height);
            double bestDist = double.MaxValue;
            HoverHit best = none;

            foreach (KeyValuePair<string, ChartVar> entry in VisibleSeries())
            {
                ChartVar cv = entry.Value;
                if (cv == null || cv.X == null || cv.Y == null) continue;
                int limit = Math.Min(cv.X.Count, cv.Y.Count);
                for (int i = 0; i < limit; i++)
                {
                    double dx = cv.X[i] - mouseX;
                    double dy = cv.Y[i] - mouseY;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist >= bestDist) continue;
                    bestDist = dist;
                    best = new HoverHit
                    {
                        SeriesName = entry.Key,
                        DataX = cv.X[i],
                        DataY = cv.Y[i],
                        PixelX = xMin + (cv.X[i] - xMin) / xSpan * plotWidth,
                        PixelY = yMax - (cv.Y[i] - yMin) / ySpan * plotHeight
                    };
                }
            }
            return bestDist <= HoverPixelRadius ? best : none;
        }

        /// <summary>取得当前可见系列，与图上实际画线严格一致。</summary>
        /// <returns>结果集合。</returns>
        private IEnumerable<KeyValuePair<string, ChartVar>> VisibleSeries()
        {
            int index = selSeriesGroup.SelectedIndex;
            if (index < 0 || index >= _groups.Count) yield break;
            List<SeriesItem> items = _groups[index].Items;
            for (int i = 0; i < items.Count; i++)
            {
                SeriesItem item = items[i];
                if (item.Box == null || !item.Box.Checked) continue;
                for (int s = 0; s < _sessions.Count; s++)
                {
                    ChartVar cv = item.GetChart(_sessions[s]);
                    if (cv == null) continue;
                    yield return new KeyValuePair<string, ChartVar>(SessionLabel(item.Name, s), cv);
                }
            }
        }

        /// <summary>取当前绘图区在数据坐标下的显示窗口（X 与 Y 两轴）。</summary>
        private void GetPlotWindow(out double xMin, out double xMax, out double yMin, out double yMax)
        {
            xMin = MinValueRobotX;
            xMax = MaxValueRobotX;
            yMin = double.MaxValue;
            yMax = double.MinValue;
            foreach (KeyValuePair<string, ChartVar> entry in VisibleSeries())
            {
                ChartVar cv = entry.Value;
                if (cv == null || cv.X == null || cv.Y == null) continue;
                for (int i = 0; i < Math.Min(cv.X.Count, cv.Y.Count); i++)
                {
                    double v = cv.Y[i];
                    if (v < yMin) yMin = v;
                    if (v > yMax) yMax = v;
                }
            }
            if (yMin > yMax)
            {
                yMin = 0;
                yMax = 1;
            }
            double pad = (yMax - yMin) * 0.1;
            if (pad <= 0) pad = 1;
            yMin -= pad;
            yMax += pad;
        }

        /// <summary>显示命中读数：Tooltip + 底部标签同步。</summary>
        private void ShowHoverTip(HoverHit hit)
        {
            _hoverText = string.Format("{0}　X={1:F1}　Y={2:F3}",
                hit.SeriesName, hit.DataX, hit.DataY);
            if (_tooltip == null)
            {
                _tooltip = new AntdUI.TooltipComponent
                {
                    Font = new System.Drawing.Font("Microsoft YaHei UI", 9.75F),
                    ArrowAlign = AntdUI.TAlign.Top
                };
            }
            // SetTip 支持原地更新：同一控件重复挂即刷新文本
            _tooltip.SetTip(formsPlotData, _hoverText);
            uiSymbolLabel1.Text = _hoverText;
        }

        /// <summary>清除悬停读数（鼠标移出所有数据点时调用）。</summary>
        private void ClearHoverTip()
        {
            _hoverText = null;
            _tooltip?.SetTip(formsPlotData, null);
            uiSymbolLabel1.Text = _lastSummaryText;
        }

        /// <summary>回溯生产记录：一次读取三份记录文件并绘图。</summary>
        private void Datashow()
        {
            List<DataReviewRow> targets = CollectSelectedRows();
            if (targets.Count == 0) return;

            _sessions = new List<SessionCharts>();

            MinValueRobotX = double.MaxValue;
            MaxValueRobotX = double.MinValue;
            int dataLines = 0;
            int missingFiles = 0;
            var adjustSummary = new StringBuilder();

            for (int i = 0; i < targets.Count; i++)
            {
                DataReviewRow row = targets[i];
                if (row == null) continue;

                // 推算三份文件路径：0 轮廓 / 1 工艺 / 2 轨迹
                List<string> files = ProductFileRecordManager.Instance.GetSessionRecordFiles(row.StartTime, row.Sn, row.Status);
                string pdatPath = files != null && files.Count > 1 ? files[1] : string.Empty;
                string csvPath = files != null && files.Count > 2 ? files[2] : string.Empty;

                List<WeldActionRecord> actions;
                try
                {
                    actions = ProductFileRecordManager.Instance.ParsePDatRecords(pdatPath);
                }
                catch
                {
                    actions = new List<WeldActionRecord>();
                }
                List<SeamTraceYPoint> points = ProductFileRecordManager.Instance.ReadSeamTraceYPoints(csvPath);

                if ((actions == null || actions.Count == 0) && (points == null || points.Count == 0))
                {
                    missingFiles++;
                    continue;
                }

                // 会话曲线容器：与索引一一对应，各成一条线
                SessionCharts session = new SessionCharts();

                // 工艺控制记录：过程帧 + 调整动作（按 X 对齐）
                if (actions != null)
                {
                    foreach (var rec in actions)
                    {
                        if (rec.AdjustExecuted)
                        {
                            // 调整点取调整时刻的焊缝宽度，便于定位
                            session.Add(NameAdjust, rec.RobotX, rec.WeldWidth);
                            if (adjustSummary.Length > 0) adjustSummary.Append("；");
                            adjustSummary.AppendFormat("{0} X={1:F1} {2}", row.Sn, rec.RobotX,
                                string.Equals(rec.ActionType, WeldParamControlRecord.ActionJobSwitch, StringComparison.Ordinal)
                                    ? string.Format("切换Job{0}", rec.JobId)
                                    : "参数调整");
                        }
                        else
                        {
                            session.Add(NameSeamWidth, rec.RobotX, rec.WeldWidth);
                            session.Add(NameFeedSpeed, rec.RobotX, rec.FeedSpeed);
                            session.Add(NameLaserPower, rec.RobotX, rec.LaserPower);
                            session.Add(NameRobotSpeed, rec.RobotX, rec.RobotSpeed);
                            // .pDat v4 已含的 6 路数据
                            session.Add(NameRobotY, rec.RobotX, rec.RobotY);
                            session.Add(NameRobotZ, rec.RobotX, rec.RobotZ);
                            session.Add(NameTaPosition, rec.RobotX, rec.TaPositionMm);
                            session.Add(NameLaPosition, rec.RobotX, rec.LaPositionMm);
                            session.Add(NameGalvoAmp, rec.RobotX, rec.GalvoAmplitudeMm);
                            session.Add(NameHeadTemp, rec.RobotX, rec.HeadTempC);
                            dataLines++;
                            if (rec.RobotX < MinValueRobotX) MinValueRobotX = rec.RobotX;
                            if (rec.RobotX > MaxValueRobotX) MaxValueRobotX = rec.RobotX;
                        }
                    }
                }

                // 三路并列：入图与出图差值即跟随误差
                if (points != null)
                {
                    foreach (var pt in points)
                    {
                        session.Add(NameCspOutY, pt.RobotX, pt.CspOutY);
                        if (!double.IsNaN(pt.CspInYt) && !double.IsNaN(pt.MapKeyX))
                        {
                            session.Add(NameCspInY, pt.MapKeyX, pt.CspInYt);
                        }
                        if (!double.IsNaN(pt.AxisY)) session.Add(NameAxisY, pt.RobotX, pt.AxisY);
                        if (pt.RobotX < MinValueRobotX) MinValueRobotX = pt.RobotX;
                        if (pt.RobotX > MaxValueRobotX) MaxValueRobotX = pt.RobotX;
                    }
                }

                // 仅纳入有数据的会话，避免空会话占序号
                if (session.PointCount > 0) _sessions.Add(session);
            }

            if (dataLines < 1 && missingFiles > 0)
            {
                AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm,
                    string.Format("所选 {0} 条记录均未找到可回溯的过程记录文件。", missingFiles));
                return;
            }

            if (MinValueRobotX > MaxValueRobotX)
            {
                MinValueRobotX = 0;
                MaxValueRobotX = 1;
            }

            RebuildChartDatasets();

            // 记下汇总：悬停清除后恢复底部标签
            _lastSummaryText = BuildSummaryText(targets, missingFiles, adjustSummary);
            uiSymbolLabel1.Text = _lastSummaryText;
        }

        /// <summary>悬停命中信息。字段全为可空/默认值，未命中时 <see cref="SeriesName"/> 为 null。</summary>
        private class HoverHit
        {
            public string SeriesName { get; set; }
            public double DataX { get; set; }
            public double DataY { get; set; }
            public double PixelX { get; set; }
            public double PixelY { get; set; }
        }

        /// <summary>收集回溯目标行：优先勾选行，否则取选中行。</summary>
        /// <returns>结果集合。</returns>
        private List<DataReviewRow> CollectSelectedRows()
        {
            var picked = new List<DataReviewRow>();
            if (_allRows != null)
            {
                for (int i = 0; i < _allRows.Count; i++)
                {
                    if (_allRows[i] != null && _allRows[i].Selected) picked.Add(_allRows[i]);
                }
            }
            if (picked.Count > 0) return picked;
            if (_selectedRow != null) picked.Add(_selectedRow);
            return picked;
        }

        /// <summary>汇总底部说明文本（会话数/起焊/调整/缺失）。</summary>
        /// <returns>结果文本。</returns>
        private string BuildSummaryText(List<DataReviewRow> targets, int missingFiles, StringBuilder adjustSummary)
        {
            DataReviewRow first = targets[0];
            string scope = targets.Count > 1
                ? string.Format("已选 {0} 条会话", targets.Count)
                : string.Format("产品码 {0}｜起焊 {1}｜结果 {2}",
                    first.Sn, first.Time,
                    string.IsNullOrEmpty(first.Result) ? "未知" : first.Result);

            string text = string.Format("{0}｜调整 {1} 次：{2}",
                scope, TotalAdjustCount(),
                adjustSummary != null && adjustSummary.Length > 0 ? adjustSummary.ToString() : "无");

            if (missingFiles > 0)
            {
                text = string.Format("{0}｜{1} 条无记录文件", text, missingFiles);
            }
            return text;
        }

        /// <summary>调整点总数（各会话合计）。</summary>
        /// <returns>结果码。</returns>
        private int TotalAdjustCount()
        {
            int sum = 0;
            for (int s = 0; s < _sessions.Count; s++)
            {
                ChartVar cv = _sessions[s][NameAdjust];
                if (cv != null) sum += cv.Count();
            }
            return sum;
        }

        /// <summary>全选：勾选区间全部记录（跨分页）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            ApplySelection(null);
        }

        /// <summary>全取消：清空当前区间的全部勾选。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnSelectNone_Click(object sender, EventArgs e)
        {
            ApplySelection(false);
        }

        /// <summary>反选：逐行取反当前勾选态。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnSelectInvert_Click(object sender, EventArgs e)
        {
            ApplySelection(null, true);
        }

        /// <summary>批量设置勾选态并刷新表格。</summary>
        /// <param name="value">目标勾选态；传 null 且 <paramref name="invert"/> 为 false 时按「全选」处理。</param>
        /// <param name="invert">true = 取反（忽略 <paramref name="value"/>）；false = 置为 <paramref name="value"/> 或 true。</param>
        private void ApplySelection(bool? value, bool invert = false)
        {
            if (_allRows == null) return;
            for (int i = 0; i < _allRows.Count; i++)
            {
                DataReviewRow row = _allRows[i];
                if (row == null) continue;
                row.Selected = invert ? !row.Selected : (value ?? true);
            }
            // 行模型已继承 NotifyProperty，逐行 setter 已通知；此处兜底整体重绘一次（表格按可视区惰性渲染）
            uiDataGridView1.Refresh();
        }

        /// <summary>按 Pagination 状态把已读入内存的记录切片显示。</summary>
        void FillPage()
        {
            _rows.Clear();
            if (_allRows == null || _allRows.Count == 0) return;

            int pageIndex = pagination1.Current;
            int pageSize = pagination1.PageSize <= 0 ? _allRows.Count : pagination1.PageSize;
            int start = (pageIndex - 1) * pageSize;
            if (start < 0) start = 0;
            int end = Math.Min(start + pageSize, _allRows.Count);
            for (int i = start; i < end; i++)
                _rows.Add(_allRows[i]);
        }

        /// <summary>Pagination 页码或页大小变化时重新填充表格</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void pagination1_ValueChanged(object sender, AntdUI.PagePageEventArgs e)
        {
            FillPage();
        }
        /// <summary>进入追溯页时确保首屏已加载（唯一入口）</summary>
        public void EnsureLoaded()
        {
            if (!IsReadyForQuery()) return;
            AfterInitializeUI();
            // 懒加载：仅首次进入才读生产记录；切页不重复读盘，区间变化仍重扫
            if (_firstShownLoaded) return;
            _firstShownLoaded = true;

            // 打开即加载当日记录，区间未就绪回退今天。
            DateTime start, end;
            DateTime[] range = uiDateRange.Value;
            if (range == null || range.Length < 2)
            {
                start = DateTime.Today;
                end = DateTime.Today;
            }
            else
            {
                start = range[0];
                end = range[1];
            }
            InitPCLDir(start, end, false);
        }

    }
}
