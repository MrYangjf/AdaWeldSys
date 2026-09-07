using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.ProductFileManager;
using AdaWeldSystem.Sub3UI;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;


namespace AdaWeldSystem.Sub1UI
{
    /// <summary>
    /// 生产数据界面
    /// </summary>
    public partial class DataReviewerPage : UserControl
    {
        // 数据行（替代原 DataTable + DataGridView 列绑定）
        public class DataReviewRow
        {
            public int No { get; set; }
            public string FileName { get; set; }
            public string Datatype { get; set; }
        }

        AntdUI.AntList<DataReviewRow> _rows;
        private DataReviewRow _selectedRow;
        public string[] FilestrArray = new string[0];
        public List<string> FilestrList;
        public string DataFileName = "";
        public string DataFilePath = "";
        public double MinValueRobotX, MaxValueRobotX, TrackbarRate;

        // 单折线图：四条数据线 + 一条红色调整点线
        ChartVar SeamWidthChartVar, RobotSpeedChartVar, FeedSpeedChartVar, LaserPowerChartVar, AdjustChartVar;

        // 系列名称常量（与复选框一一对应）
        private const string NameSeamWidth = "焊缝宽度";
        private const string NameRobotSpeed = "机器人运动速度";
        private const string NameFeedSpeed = "送丝速度";
        private const string NameLaserPower = "激光功率";
        private const string NameAdjust = "调整点";

        public DataReviewerPage()
        {
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<DataReviewRow>();
            uiDataGridView1.Binding(_rows);
            uiDataGridView1.CellClick += uiDataGridView1_CellClick;
            PageBangdingIni();
            // 日历日期切换才加载对应日期目录（订阅在初始日期赋值之后，故初始化不触发、不显示）
            uiCalendar1.ValueChanged += uiCalendar1_OnDateTimeChanged;
            // Pagination 可选页大小
            pagination1.PageSizeOptions = new int[] { 20, 50, 100, 200 };
            pagination1.ValueChanged += pagination1_ValueChanged;
        }

        /// <summary>
        /// 初始化表格列。参考 AntdUI 官方 TableDemo 范式：在代码后置中设置 Columns，
        /// 不可在 Designer.cs 的 InitializeComponent 内直接赋值 ColumnCollection。
        /// </summary>
        private void InitTableColumns()
        {
            uiDataGridView1.Columns = new AntdUI.ColumnCollection
            {
                // Column(key, title)：key 必须等于 DataReviewRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("No", "No.") { Width = "30" },
                new AntdUI.Column("FileName", "文件名") { Width = "120" },
                new AntdUI.Column("Datatype", "数据类型") { Width = "100" }
            };
        }

        /// <summary>
        /// 日历日期切换：按所选日期索引 yyyyMMdd 生产数据文件夹并刷新文件列表
        /// </summary>
        private void uiCalendar1_OnDateTimeChanged(object sender, EventArgs e)
        {
            DateTime dt = uiCalendar1.Value ?? DateTime.Today;
            InitPCLDir(dt);
        }

        private void DataReview_Shown(object sender, EventArgs e)
        {
            AfterInitializeUI();
        }

        void PageBangdingIni()
        {
            uiCalendar1.Value = DateTime.Today;
        }

        /// <summary>
        /// 根据日历日期，从 RunRecordManager 的记录目录加载当天的 pDat 生产文件列表
        /// </summary>
        private void InitPCLDir(DateTime InputDateTime)
        {
            _rows.Clear();
            this.DataFilePath = "";
            FilestrArray = new string[0];

            // 依据所选日期索引 yyyyMMdd 生产数据文件夹
            string dateDir = RunRecordManager.Instance.GetDateRecordDirectoryPath(InputDateTime);
            if (!Directory.Exists(dateDir))
            {
                AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, string.Format("未找到 {0} 的生产数据文件夹：\r\n{1}", InputDateTime.ToString("yyyy-MM-dd"), dateDir));
                return;
            }

            List<string> files;
            try
            {
                files = RunRecordManager.Instance.GetWeldActionRecordFiles(InputDateTime);
            }
            catch
            {
                files = new List<string>();
            }
            FilestrArray = files.ToArray();

            if (FilestrArray.Length == 0)
            {
                AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, string.Format("{0} 的生产数据文件夹内没有记录文件。", InputDateTime.ToString("yyyy-MM-dd")));
                return;
            }

            // 重置 Pagination 并填充第一页
            pagination1.Total = FilestrArray.Length;
            pagination1.Current = 1;
            FillPage();
        }

        /// <summary>
        /// 构建单折线图：标题 + 坐标轴标签 + 图例（ScottPlot.FormsPlot 4.1.74）。
        /// 用法见 [[lessons/ScottPlot-FormsPlot-Usage]]。
        /// </summary>
        private void AfterInitializeUI()
        {
            // [lessons/ScottPlot-FormsPlot-Usage#dpi] 修复高 DPI 缩放下字体锯齿（同 MainPage.SetupChart）：
            // Program.cs 为 System DPI aware，显示缩放 >100% 时默认 DpiStretch=true 拉伸位图致字体模糊；
            // 置 false 让 ScottPlot 按真实 DPI 渲染高分辨率位图（官方 4.1 cookbook misc_dpiscale）。
            formsPlotData.Configuration.DpiStretch = false;
            formsPlotData.Plot.Title("焊接过程参数（按机器人 X 轴）");
            formsPlotData.Plot.XLabel("机器人 X 轴");
            formsPlotData.Plot.YLabel("参数值");
            formsPlotData.Plot.Legend();
            formsPlotData.Refresh();
        }

        /// <summary>
        /// 复选框勾选状态 -> 对应数据线显隐（调整点红色标记始终显示）
        /// </summary>
        private void ChkSeries_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            SyncCheckBoxToSeries();
        }

        private void SyncCheckBoxToSeries()
        {
            RebuildChartDatasets();
        }

        /// <summary>
        /// 按勾选状态重建图表数据集（ScottPlot 无逐系列 Visible，故清空后重建 plottable）。
        /// 用法见 [[lessons/ScottPlot-FormsPlot-Usage]]。
        /// </summary>
        private void RebuildChartDatasets()
        {
            formsPlotData.Plot.Clear();
            if (chkSeamWidth.Checked) AddSeries(NameSeamWidth, SeamWidthChartVar);
            if (chkRobotSpeed.Checked) AddSeries(NameRobotSpeed, RobotSpeedChartVar);
            if (chkFeedSpeed.Checked) AddSeries(NameFeedSpeed, FeedSpeedChartVar);
            if (chkLaserPower.Checked) AddSeries(NameLaserPower, LaserPowerChartVar);
            AddSeries(NameAdjust, AdjustChartVar, Color.Red);
            formsPlotData.Plot.AxisAuto();
            formsPlotData.Plot.Legend();
            formsPlotData.Refresh();
        }

        /// <summary>
        /// 将 ChartVar 坐标序列以散点折线方式加入 FormsPlot（主色 Purple 128,0,128，调整点为红色）。
        /// </summary>
        private void AddSeries(string name, ChartVar cv, Color? color = null)
        {
            if (cv == null || cv.Count() < 1) return;
            double[] xs = new double[cv.Count()];
            double[] ys = new double[cv.Count()];
            for (int i = 0; i < cv.Count(); i++)
            {
                xs[i] = cv.X[i];
                ys[i] = cv.Y[i];
            }
            System.Drawing.Color lineColor = color.HasValue ? color.Value : System.Drawing.Color.FromArgb(128, 0, 128);
            formsPlotData.Plot.AddScatter(xs, ys, lineColor, 2, markerSize: 0, label: name);
        }

        private void uiTrackBar1_ValueChanged(object sender, IntEventArgs e)
        {
            SeamWidthShowRange();
        }

        // 通过 CellClick 捕获当前选中行（AntdUI.Table 无 SelectedItem；参见 demo TableDemo.cs 的 e.Record）
        private void uiDataGridView1_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            _selectedRow = e.Record as DataReviewRow;
        }

        /// <summary>
        /// 依据滑块位置（对应机器人 X 轴进度）设置折线图 X 轴显示窗口
        /// </summary>
        void SeamWidthShowRange()
        {
            if (SeamWidthChartVar == null || SeamWidthChartVar.Count() < 5) return;
            if (uiTrackBar1.MaxValue <= 0) return;

            double Rate = 100.0 * ((double)uiTrackBar1.Value / (double)uiTrackBar1.MaxValue);
            double step = (MaxValueRobotX - MinValueRobotX) / (double)uiTrackBar1.MaxValue;
            double min = MinValueRobotX + (uiTrackBar1.Value - 1) * step;
            double max = MinValueRobotX + (uiTrackBar1.Value + 1) * step;
            string message = string.Format("进度：{0}%；坐标段：{1}~{2}", Rate.ToString("0.00"), min.ToString("0.00"), max.ToString("0.00"));
            uiSymbolLabel1.Text = message;

            // ScottPlot.FormsPlot：运行时 X 轴显示窗口（原 AntdUI SetRange TODO 已实现）
            formsPlotData.Plot.SetAxisLimits(min, max);
            formsPlotData.Refresh();
        }

        private void ShowData_Click(object sender, EventArgs e)
        {
            Datashow();
        }

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

        private void SetRange_Click(object sender, EventArgs e)
        {
        }

        private void btn_LoadFile_Click(object sender, EventArgs e)
        {
            DateTime dt = uiCalendar1.Value ?? DateTime.Today;
            InitPCLDir(dt);
        }

        /// <summary>
        /// 解析选中的 pDat 生产记录，绘制单折线图（4 条数据线 + 红色调整点）
        /// </summary>
        private void Datashow()
        {
            var sel = _selectedRow;
            if (sel == null) return;
            string path = sel.FileName;

            List<WeldActionRecord> records = RunRecordManager.Instance.ParsePDatRecords(path);
            if (records == null || records.Count == 0) return;

            SeamWidthChartVar = new ChartVar();
            RobotSpeedChartVar = new ChartVar();
            FeedSpeedChartVar = new ChartVar();
            LaserPowerChartVar = new ChartVar();
            AdjustChartVar = new ChartVar();

            MinValueRobotX = double.MaxValue;
            MaxValueRobotX = double.MinValue;
            int dataLines = 0;

            foreach (var rec in records)
            {
                if (rec.AdjustExecuted)
                {
                    AdjustChartVar.Add(rec.RobotX, rec.WeldWidth);
                }
                else
                {
                    SeamWidthChartVar.Add(rec.RobotX, rec.WeldWidth);
                    RobotSpeedChartVar.Add(rec.RobotX, rec.RobotSpeed);
                    FeedSpeedChartVar.Add(rec.RobotX, rec.FeedSpeed);
                    LaserPowerChartVar.Add(rec.RobotX, rec.LaserPower);
                    dataLines++;
                    if (rec.RobotX < MinValueRobotX) MinValueRobotX = rec.RobotX;
                    if (rec.RobotX > MaxValueRobotX) MaxValueRobotX = rec.RobotX;
                }
            }

            if (dataLines < 1) return;

            // 滑块按机器人 X 轴分段定位
            uiTrackBar1.MaxValue = Math.Max(1, dataLines - 2);
            TrackbarRate = (MaxValueRobotX - MinValueRobotX) / (double)uiTrackBar1.MaxValue;
            uiTrackBar1.Value = 0;

            RebuildChartDatasets();
        }

        /// <summary>
        /// 按当前 Pagination 状态填充表格（替代自建分页逻辑）
        /// </summary>
        void FillPage()
        {
            _rows.Clear();
            int pageIndex = pagination1.Current;
            int pageSize = pagination1.PageSize;
            int total = pagination1.Total;
            int start = (pageIndex - 1) * pageSize;
            int end = Math.Min(start + pageSize, total);
            for (int i = start; i < end; i++)
                _rows.Add(new DataReviewRow { No = i + 1, FileName = FilestrArray[i], Datatype = "WeldRecord" });
        }

        /// <summary>
        /// Pagination 页码或页大小变化时重新填充表格
        /// </summary>
        private void pagination1_ValueChanged(object sender, AntdUI.PagePageEventArgs e)
        {
            FillPage();
        }
    }
}
