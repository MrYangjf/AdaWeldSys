using AdaWeldSystem.WeldParamControl;
using AntdUI;
using System;
using System.Data;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    public partial class ConfigForm : UserControl
    {
        private const string ColType = "类型";
        private const string ColWireMaterial = "焊丝材质";
        private const string ColPlateMaterial = "焊接板材";
        private const string ColTime = "时间";
        private const string ColId = "标识";
        private const int DefaultPageSize = 50;

        // 与 uiDataGridView1.Binding() 绑定的强类型列表（替代原 DataTable + AutoGenerateColumns）
        private AntdUI.AntList<ConfigRow> _rows;
        private ConfigRow _selectedRow;
        private DataTable _allConfigs;

        // 复刻 AntdUI demo SystemSet 模式：注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        public ConfigForm() : this(null) { }

        public ConfigForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<ConfigRow>();
            uiDataGridView1.Binding(_rows);
            uiDataGridView1.CellClick += uiDataGridView1_CellClick;
            // AntdUI.Pagination 分页：ValueChanged(e.Current/e.PageSize) → 重新切片绑定
            // 用 lambda 让编译器推断事件参数类型（范式见 02-Navigation.md Pagination 节）
            uiPagination1.ValueChanged += (s, e) => BindPageData(e.Current, e.PageSize);
            LoadConfigData();
        }

        /// <summary>
        /// 初始化表格列。参考 AntdUI 官方 TableDemo 范式：在代码后置中设置 Columns，
        /// 不可在 Designer.cs 的 InitializeComponent 内直接赋值 ColumnCollection。
        /// </summary>
        private void InitTableColumns()
        {
            uiDataGridView1.Columns = new AntdUI.ColumnCollection
            {
                // Column(key, title)：key 必须等于 ConfigRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("No", "No.") { Width = "50" },
                new AntdUI.Column("Type", "类型") { Width = "120" },
                new AntdUI.Column("WireMaterial", "焊丝材质") { Width = "120" },
                new AntdUI.Column("PlateMaterial", "焊接板材") { Width = "120" },
                new AntdUI.Column("Time", "时间") { Width = "160" },
                new AntdUI.Column("Id", "标识") { Width = "160" }
            };
            // 列宽填满表格区域，不留下空白
            uiDataGridView1.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        private void LoadConfigData()
        {

            try
            {
                _allConfigs = WeldDataManager.Instance.mConfigManager.GetAllConfigs();
                uiPagination1.Total = _allConfigs.Rows.Count;
                uiPagination1.PageSize = DefaultPageSize;
                BindPageData(1, DefaultPageSize);
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this.Window, $"加载配置数据失败：{ex.Message}");
            }
        }

        private void BindPageData(int pageIndex, int pageSize)
        {
            if (_allConfigs == null)
                return;

            _rows.Clear();

            int startIndex = (pageIndex - 1) * pageSize;
            int endIndex = Math.Min(pageIndex * pageSize, _allConfigs.Rows.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                DataRow sourceRow = _allConfigs.Rows[i];
                _rows.Add(new ConfigRow
                {
                    No = i + 1,
                    Type = sourceRow[ColType]?.ToString(),
                    WireMaterial = sourceRow[ColWireMaterial]?.ToString(),
                    PlateMaterial = sourceRow[ColPlateMaterial]?.ToString(),
                    Time = sourceRow[ColTime]?.ToString(),
                    Id = sourceRow[ColId]?.ToString()
                });
            }
        }


        // 通过 CellClick 捕获当前选中行（AntdUI.Table 无 SelectedItem；参见 demo TableDemo.cs 的 e.Record）
        private void uiDataGridView1_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            _selectedRow = e.Record as ConfigRow;
        }

        private string GetSelectedConfigId()
        {
            var row = _selectedRow;
            return row?.Id;
        }

        private void SelectConfig_Click(object sender, EventArgs e)
        {
            string configId = GetSelectedConfigId();
            if (string.IsNullOrWhiteSpace(configId))
                return;

            if (!WeldDataManager.Instance.mConfigManager.IsConfigExist(configId))
            {
                AntdUI.Message.error(this.Window, "当前配置为空！");
                return;
            }

            WeldDataManager.Instance.SwitchConfig(configId);
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void DeleteConfig_Click(object sender, EventArgs e)
        {
            string configId = GetSelectedConfigId();
            if (string.IsNullOrWhiteSpace(configId))
                return;

            if (configId == AutoParam.DefaultIdentity)
            {
                AntdUI.Message.error(this.Window, "默认配置不允许删除！");
                return;
            }

            if (AntdUI.Modal.open(new Modal.Config(this.Window, "确认删除", $"请确认需要删除选定配置：{configId}", TType.Info)) != DialogResult.OK)
                return;

            if (!WeldDataManager.Instance.mConfigManager.IsConfigExist(configId))
            {
                AntdUI.Message.error(this.Window, "当前配置为空！");
                return;
            }

            if (configId == WeldDataManager.Instance.CurrentAutoParamId)
            {
                AntdUI.Message.error(this.Window, "不能删除正在使用的配置！");
                return;
            }

            WeldDataManager.Instance.DeleteConfig(configId);
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }
        // AntdUI.Table 数据行模型（替代原 DataGridView 列绑定）
        public class ConfigRow
        {
            public int No { get; set; }
            public string Type { get; set; }
            public string WireMaterial { get; set; }
            public string PlateMaterial { get; set; }
            public string Time { get; set; }
            public string Id { get; set; }
        }
    }
}
