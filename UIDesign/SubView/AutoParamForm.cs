using AdaWeldSystem.WeldParamControl;
using AntdUI;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    public partial class AutoParamForm : UserControl
    {

        // 与 uiDataGridView1.Binding() 绑定的强类型列表（替代原 DataTable + AutoGenerateColumns）
        private AntdUI.AntList<AutoParamRow> _rows;

        // 复刻 AntdUI demo SystemSet 模式：注入父 Window，内部方法用 this.Window 作 owner
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

        /// <summary>
        /// 单元格编辑结束事件：将编辑后的值写回行对象，供保存逻辑读取。
        /// </summary>
        private bool uiDataGridView1_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            AutoParamRow row = e.Record as AutoParamRow;
            if (row == null || e.Value == null)
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
                default:
                    break;
            }
            return true;
        }

        /// <summary>
        /// 初始化表格列。参考 AntdUI 官方 TableDemo 范式：在代码后置中设置 Columns，
        /// 不可在 Designer.cs 的 InitializeComponent 内直接赋值 ColumnCollection，
        /// 否则 WinForms 设计器无法序列化而报错。
        /// </summary>
        private void InitTableColumns()
        {
            uiDataGridView1.Columns = new AntdUI.ColumnCollection
            {
                // Column(key, title)：key 必须等于 AutoParamRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("SeamWidth", "焊缝宽度") { Width = "150" },
                new AntdUI.Column("RobotSpeed", "焊接速度") { Width = "150" }.SetEditable(true),
                new AntdUI.Column("FeedSpeed", "送丝速度") { Width = "150" }.SetEditable(true),
                new AntdUI.Column("LaserPower", "焊接功率") { Width = "150" }.SetEditable(true)
            };
            // 启用表格单元格编辑（双击进入编辑）+ 列宽填满表格区域，不留下空白
            uiDataGridView1.EditMode = AntdUI.TEditMode.DoubleClick;
            uiDataGridView1.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        public void LoadConfigData(AutoParam autoParam)
        {
            if (autoParam == null)
                return;

            WeldDataManager.Instance.mAutoParamManage.SelectTable(autoParam);

            labelK.Text = $"K:{autoParam.KVar}";
            LabelJ.Text = $"J:{autoParam.JVar}";
            labelM.Text = $"M:{autoParam.MVar}";


            try
            {
                var seamWidths = WeldProcess.Instance.SeamWidthList;
                var robotSpeeds = WeldProcess.Instance.RobotSpeedDic;
                var feedSpeeds = WeldProcess.Instance.FeedSpeedDic;
                var laserPowers = WeldProcess.Instance.LaserPowerDic;

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

                    _rows.Add(new AutoParamRow
                    {
                        SeamWidth = width.ToString(CultureInfo.InvariantCulture),
                        RobotSpeed = robotSpeed.ToString(CultureInfo.InvariantCulture),
                        FeedSpeed = feedSpeed.ToString(CultureInfo.InvariantCulture),
                        LaserPower = laserPower.ToString(CultureInfo.InvariantCulture)
                    });
                }
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this.Window, $"加载自动参数值失败：{ex.Message}");
            }
        }

        private void UpdateParamValues()
        {
            var seamWidths = WeldProcess.Instance.SeamWidthList;
            var robotSpeeds = WeldProcess.Instance.RobotSpeedDic;
            var feedSpeeds = WeldProcess.Instance.FeedSpeedDic;
            var laserPowers = WeldProcess.Instance.LaserPowerDic;

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
            }

            WeldDataManager.Instance.mAutoParamManage.UpdateParamValues();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            UpdateParamValues();
        }
        // AntdUI.Table 数据行模型（替代原 DataGridView 列绑定）
        public class AutoParamRow
        {
            public string SeamWidth { get; set; }
            public string RobotSpeed { get; set; }
            public string FeedSpeed { get; set; }
            public string LaserPower { get; set; }
        }
    }
}
