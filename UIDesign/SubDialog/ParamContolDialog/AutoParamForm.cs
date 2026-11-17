using AdaWeldSystem.WeldParamControl;
using AntdUI;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.SubDialog.ParamContolDialog
{
    public partial class AutoParamForm : UserControl
    {

        // 与 uiDataGridView1.Binding() 绑定的强类型列表
        private AntdUI.AntList<AutoParamRow> _rows;

        // 注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        public AutoParamForm() : this(null) { }

        public AutoParamForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<AutoParamRow>();
            uiDataGridView1.Binding(_rows);
            uiDataGridView1.CellEndEdit += uiDataGridView1_CellEndEdit;
        }

        /// <summary>单元格编辑结束事件：将编辑后的值写回行对象，</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>供保存逻辑读取。</remarks>
        /// <returns>是否成功。</returns>
        private bool uiDataGridView1_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            if (!(e.Record is AutoParamRow row) || e.Value == null)
            {
                return true;
            }

            string newValue = e.Value;
            switch (e.Column.Key)
            {
                case "RobotSpeed":
                    row.RobotSpeed = newValue;
                    break;
                case "FeedSpeed":
                    row.FeedSpeed = newValue;
                    break;
                case "LaserPower":
                    row.LaserPower = newValue;
                    break;
                case "SwingWidth":
                    row.SwingWidth = newValue;
                    break;
                case "SwingFrequency":
                    row.SwingFrequency = newValue;
                    break;
                default:
                    break;
            }
            return true;
        }

        /// <summary>初始化表格列：Columns 须在代码后置设置，不可在 Designer 内赋值。</summary>
        private void InitTableColumns()
        {
            uiDataGridView1.Columns = new AntdUI.ColumnCollection
            {
                // Column(key, title)：key 必须等于 AutoParamRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("SeamWidth", "焊缝宽度") { Width = "150" },
                new AntdUI.Column("RobotSpeed", "焊接速度") { Width = "150" }.SetEditable(true),
                new AntdUI.Column("FeedSpeed", "送丝速度") { Width = "150" }.SetEditable(true),
                new AntdUI.Column("LaserPower", "焊接功率") { Width = "150" }.SetEditable(true),
                // 摆幅宽度：公式初值入表可改，输出用查表值
                new AntdUI.Column("SwingWidth", "摆幅宽度") { Width = "150" }.SetEditable(true),
                // 摆幅频率：公式初值入表可核对，摆幅越大越低
                new AntdUI.Column("SwingFrequency", "摆幅频率") { Width = "150" }.SetEditable(true)
            };
            // 启用双击编辑，列宽填满表格区域
            uiDataGridView1.EditMode = AntdUI.TEditMode.DoubleClick;
            uiDataGridView1.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        private void UpdateParamValues()
        {
            var seamWidths = WeldParamManager.Instance.SeamWidthList;
            var robotSpeeds = WeldParamManager.Instance.RobotSpeedDic;
            var feedSpeeds = WeldParamManager.Instance.FeedSpeedDic;
            var laserPowers = WeldParamManager.Instance.LaserPowerDic;
            var swingWidths = WeldParamManager.Instance.SwingWidthDic;
            var swingFrequencies = WeldParamManager.Instance.SwingFrequencyDic;

            for (int i = 0; i < seamWidths.Count && i < _rows.Count; i++)
            {
                double width = seamWidths[i];
                var row = _rows[i];

                if (double.TryParse(row.RobotSpeed, NumberStyles.Any, CultureInfo.InvariantCulture, out double robotSpeed))
                    robotSpeeds[width] = robotSpeed;

                if (double.TryParse(row.FeedSpeed, NumberStyles.Any, CultureInfo.InvariantCulture, out double feedSpeed))
                    feedSpeeds[width] = feedSpeed;

                if (double.TryParse(row.LaserPower, NumberStyles.Any, CultureInfo.InvariantCulture, out double laserPower))
                    laserPowers[width] = laserPower;

                // 摆幅宽度手动修正写回查表（最终输出用查表值）
                if (double.TryParse(row.SwingWidth, NumberStyles.Any, CultureInfo.InvariantCulture, out double swingWidth))
                    swingWidths[width] = swingWidth;

                // 摆幅频率手动核对写回查表（最终输出用查表值）
                if (int.TryParse(row.SwingFrequency, NumberStyles.Any, CultureInfo.InvariantCulture, out int swingFrequency))
                    swingFrequencies[width] = swingFrequency;
            }

            WeldParamManager.Instance.SaveCurrentProfile();
        }

        /// <summary>按摆幅宽度取摆幅频率（走 AutoParam 基准公式，基准点缺失</summary>
        /// <remarks>时回默认值）。</remarks>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="swingWidthMm">摆幅宽度（毫米）</param>
        /// <returns>摆幅频率（Hz）</returns>
        private static int EffectiveSwingFrequency(AutoParam autoParam, double swingWidthMm)
        {
            if (autoParam == null)
            {
                return AutoParam.DefaultSwingFreqHz1;
            }

            return autoParam.CalSwingFrequency(swingWidthMm);
        }

        // AntdUI.Table 数据行模型
        public class AutoParamRow
        {
            public string SeamWidth { get; set; }
            public string RobotSpeed { get; set; }
            public string FeedSpeed { get; set; }
            public string LaserPower { get; set; }

            /// <summary>摆幅宽度（初值入表可改，输出用查表值）。</summary>
            public string SwingWidth { get; set; }

            /// <summary>摆幅频率 Hz（初值入表可核对，输出用查表值）。</summary>
            public string SwingFrequency { get; set; }
        }

        public void LoadConfigData(AutoParam autoParam)
        {
            if (autoParam == null)
                return;

            WeldParamManager.Instance.LoadProfileIntoLookupTable(autoParam.identityInfo);

            labelK.Text = $"K:{autoParam.KVar}";
            LabelJ.Text = $"J:{autoParam.JVar}";
            labelM.Text = $"M:{autoParam.MVar}";


            try
            {
                var seamWidths = WeldParamManager.Instance.SeamWidthList;
                var robotSpeeds = WeldParamManager.Instance.RobotSpeedDic;
                var feedSpeeds = WeldParamManager.Instance.FeedSpeedDic;
                var laserPowers = WeldParamManager.Instance.LaserPowerDic;
                var swingWidths = WeldParamManager.Instance.SwingWidthDic;
                var swingFrequencies = WeldParamManager.Instance.SwingFrequencyDic;

                // 重复调用时先清空，避免行累加（AntList 已 Binding，增删即刷新）
                _rows.Clear();

                foreach (double width in seamWidths)
                {
                    if (!robotSpeeds.TryGetValue(width, out double robotSpeed))
                        continue;
                    if (!feedSpeeds.TryGetValue(width, out double feedSpeed))
                        continue;
                    if (!laserPowers.TryGetValue(width, out double laserPower))
                        continue;

                    // 摆幅宽度缺失档按公式兜底（系数 × 焊缝宽）
                    if (!swingWidths.TryGetValue(width, out double swingWidth))
                        swingWidth = width * (autoParam != null && autoParam.SpotSwingGain > 0.0
                            ? autoParam.SpotSwingGain
                            : AutoParam.DefaultSpotSwingGain);

                    // 摆幅频率缺失档按公式兜底（由该档宽度反算）
                    if (!swingFrequencies.TryGetValue(width, out int swingFrequency))
                        swingFrequency = EffectiveSwingFrequency(autoParam, swingWidth);

                    _rows.Add(new AutoParamRow
                    {
                        SeamWidth = width.ToString(CultureInfo.InvariantCulture),
                        RobotSpeed = robotSpeed.ToString(CultureInfo.InvariantCulture),
                        FeedSpeed = feedSpeed.ToString(CultureInfo.InvariantCulture),
                        LaserPower = laserPower.ToString(CultureInfo.InvariantCulture),
                        SwingWidth = swingWidth.ToString(CultureInfo.InvariantCulture),
                        SwingFrequency = swingFrequency.ToString(CultureInfo.InvariantCulture)
                    });
                }
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this.Window, $"加载自动参数值失败：{ex.Message}");
            }
        }
    }
}
