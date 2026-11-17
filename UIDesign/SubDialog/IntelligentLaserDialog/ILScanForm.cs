using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.SubDialog.IntelligentLaserDialog
{
    /// <summary>扫描结果对话框：单选相机，确定后回填 IP。</summary>
    public partial class ILScanForm : UserControl
    {
        #region 私有变量
        // 注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        // 与 uiTableSensor.Binding() 绑定的强类型列表（单选：一次仅一项 Selected=true）
        private AntdUI.AntList<SensorRow> _rows;
        #endregion

        #region 公共变量
        /// <summary>用户选中的相机 IP（点「确定」后有效）</summary>
        public string SelectedIp { get; private set; }
        #endregion

        #region 嵌套类型
        /// <summary>表格行模型：No=序号，Selected=选取列（单选），Ip/SerialNumber/SoftwareVersion/SensorType=相机信息。</summary>
        private class SensorRow : AntdUI.NotifyProperty
        {
            public int No { get; set; }

            private bool _selected;
            public bool Selected
            {
                get { return _selected; }
                set
                {
                    if (_selected == value) return;
                    _selected = value;
                    OnPropertyChanged();
                }
            }

            public string Ip { get; set; }
            public string SerialNumber { get; set; }
            public string SoftwareVersion { get; set; }
            public string SensorType { get; set; }
        }
        private SensorRow _selected;
        #endregion

        #region 构造函数
        public ILScanForm(Window _window, List<IlSensorInfo> sensors)
        {
            Window = _window;
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<SensorRow>();
            uiTableSensor.Binding(_rows);
            // 事件订阅放代码后置，避免设计器序列化丢失
            uiTableSensor.CellClick += uiTableSensor_CellClick;
            btnOK.Click += btnOK_Click;
            btnCancel.Click += btnCancel_Click;
            LoadSensors(sensors);
        }
        #endregion

        #region 私有函数

        /// <summary>初始化表格列。参考 AntdUI 官方 TableDemo 范式：Columns 在代码后置设置，不</summary>
        /// <remarks>可写入 Designer.cs。 选取列用 ColumnCheck（双参，表头无全选）+ SetAutoCheck(false)，配合 CellClick 实现单选互斥。</remarks>
        private void InitTableColumns()
        {
            uiTableSensor.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("No", "No.") { Width = "60" },
                new AntdUI.ColumnCheck("Selected", "选取") { Width = "70" }.SetAutoCheck(false),
                new AntdUI.Column("Ip", "IP地址") { Width = "170" },
                new AntdUI.Column("SerialNumber", "序列号") { Width = "170" },
                new AntdUI.Column("SoftwareVersion", "软件版本") { Width = "130" },
                new AntdUI.Column("SensorType", "传感器类型") { Width = "120" }
            };
            uiTableSensor.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        /// <summary>填充扫描结果列表，默认选中第一台相机。</summary>
        private void LoadSensors(List<IlSensorInfo> sensors)
        {
            _rows.Clear();
            _selected = null;
            if (sensors == null) return;
            int no = 1;
            foreach (var s in sensors)
            {
                _rows.Add(new SensorRow
                {
                    No = no++,
                    Ip = s.Ip,
                    SerialNumber = s.SerialNumber,
                    SoftwareVersion = s.SoftwareVersion,
                    SensorType = s.SensorType
                });
            }
            if (_rows.Count > 0)
            {
                _selected = _rows[0];
                _rows[0].Selected = true;
            }
        }

        /// <summary>点击任意单元格选中该行（单选互斥）。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void uiTableSensor_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            if (!(e.Record is SensorRow row)) return;
            _selected = row;
            foreach (var r in _rows)
            {
                r.Selected = (r == row);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                AntdUI.Message.warn(this.Window, "请先选择一台相机！");
                return;
            }
            SelectedIp = _selected.Ip;
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        #endregion
    }
}
