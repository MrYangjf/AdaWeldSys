using AdaWeldSystem.MonitorCam.IMonitorCam;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.SubDialog.MonitorCamDialog
{
    /// <summary>扫描结果对话框：单选相机，确定后回填 IP。</summary>
    public partial class MonitorCamScanForm : UserControl
    {
        #region 私有变量
        // 注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        // 与 uiTableSensor.Binding() 绑定的强类型列表（单选：一次仅一项 Selected=true）
        private AntdUI.AntList<DeviceRow> _rows;
        #endregion

        #region 公共变量
        /// <summary>用户选中的相机 IP（点「确定」后有效）</summary>
        public string SelectedIp { get; private set; }
        #endregion

        #region 嵌套类型
        /// <summary>表格行模型：No=序号，Selected=选取列（单选），Ip/ProductName/SerialNumber/PortType/SensorType=相机信息。</summary>
        private class DeviceRow : AntdUI.NotifyProperty
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
            public string ProductName { get; set; }
            public string SerialNumber { get; set; }
            public string PortType { get; set; }
            public string SensorType { get; set; }
        }
        private DeviceRow _selected;
        #endregion

        #region 构造函数
        public MonitorCamScanForm(Window _window, List<MonitorCamDeviceInfo> devices)
        {
            Window = _window;
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<DeviceRow>();
            uiTableDevice.Binding(_rows);
            // 事件订阅放代码后置，避免设计器序列化丢失
            uiTableDevice.CellClick += uiTableDevice_CellClick;
            btnOK.Click += btnOK_Click;
            btnCancel.Click += btnCancel_Click;
            LoadDevices(devices);
        }
        #endregion

        #region 私有函数

        /// <summary>初始化表格列：Columns 代码后置；ColumnCheck+SetAutoCheck(false) 单选互斥，表头无全选。</summary>
        private void InitTableColumns()
        {
            uiTableDevice.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("No", "No.") { Width = "60" },
                new AntdUI.ColumnCheck("Selected", "选取") { Width = "70" }.SetAutoCheck(false),
                new AntdUI.Column("Ip", "IP地址") { Width = "170" },
                new AntdUI.Column("ProductName", "产品名称") { Width = "170" },
                new AntdUI.Column("SerialNumber", "序列号") { Width = "150" },
                new AntdUI.Column("PortType", "接口类型") { Width = "110" },
                new AntdUI.Column("SensorType", "传感器类型") { Width = "120" }
            };
            uiTableDevice.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        /// <summary>填充扫描结果列表，默认选中第一台相机。</summary>
        private void LoadDevices(List<MonitorCamDeviceInfo> devices)
        {
            _rows.Clear();
            _selected = null;
            if (devices == null) return;
            int no = 1;
            foreach (var d in devices)
            {
                _rows.Add(new DeviceRow
                {
                    No = no++,
                    Ip = d.Ip,
                    ProductName = d.ProductName,
                    SerialNumber = d.SerialNumber,
                    PortType = d.PortType,
                    SensorType = d.SensorType
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
        private void uiTableDevice_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            if (!(e.Record is DeviceRow row)) return;
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
