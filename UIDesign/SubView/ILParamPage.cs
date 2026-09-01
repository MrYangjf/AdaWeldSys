using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    /// <summary>英莱相机参数编辑页</summary>
    public partial class ILParamPage : UserControl
    {
        #region 私有变量
        // 复刻 AntdUI demo SystemSet 模式：注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;
        private readonly IntelligentLaserCameraRun _ilCamera;
        private AntdUI.AntList<CameraParamRow> _rows;
        #endregion

        #region 公共变量
        /// <summary>相机参数行模型</summary>
        public class CameraParamRow : AntdUI.NotifyProperty
        {
            private string _value;
            public string Name { get; set; }
            public string Value
            {
                get { return _value; }
                set { if (_value == value) return; _value = value; OnPropertyChanged(); }
            }
            public string Range { get; set; }
            public string Unit { get; set; }
            public string Desc { get; set; }
            public int Kind { get; set; }
            public IlJobParamInfo JobParam { get; set; }
        }
        #endregion

        #region 构造函数

        public ILParamPage(Window _window, IntelligentLaserCameraRun ilCamera)
        {
            Window = _window;
            _ilCamera = ilCamera;
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<CameraParamRow>();
            uiTableParam.Binding(_rows);
            // 事件订阅放代码后置（Designer.cs 手写订阅会被设计器重新序列化时丢失）
            uiTableParam.CellEndEdit += uiTableParam_CellEndEdit;
            btnFetch.Click += btnFetch_Click;
            btnSave.Click += btnSave_Click;
            btnCancel.Click += btnCancel_Click;
            UpdateConnLabel();
            LoadParams();
        }

        #endregion

        #region 私有函数

        private void InitTableColumns()
        {
            uiTableParam.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Name", "参数名") { Width = "160" },
                new AntdUI.Column("Value", "当前值") { Width = "180" }.SetEditable(true),
                new AntdUI.Column("Range", "范围") { Width = "180" },
                new AntdUI.Column("Unit", "单位") { Width = "80" },
                new AntdUI.Column("Desc", "说明") { Width = "280" }
            };
            uiTableParam.EditMode = AntdUI.TEditMode.DoubleClick;
            uiTableParam.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        private void UpdateConnLabel()
        {
            // 直接读硬件真相源，不缓存本地连接副本（避免与 IsConnected 脱节）
            bool connected = _ilCamera != null && _ilCamera.IsConnected;
            lblConn.Text = connected ? "英莱设备：已连接" : "英莱设备：未连接（只读）";
            lblConn.ForeColor = connected ? System.Drawing.Color.FromArgb(0, 128, 0) : System.Drawing.Color.FromArgb(192, 0, 0);
            btnSave.Enabled = connected;
            btnFetch.Enabled = connected;
        }

        private void LoadParams()
        {
            _rows.Clear();
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            var p = _ilCamera.FetchCameraParams();
            if (p == null) return;

            // 拉取 Job 参数（提取算法/边界 + 相机基础参数的范围来源）
            List<IlJobParamInfo> jobParams = null;
            try { jobParams = _ilCamera.FetchAllJobParams(); }
            catch (Exception) { jobParams = null; }

            var jobMap = new Dictionary<string, IlJobParamInfo>();
            if (jobParams != null)
            {
                foreach (var jp in jobParams)
                {
                    if (!jobMap.ContainsKey(jp.Name)) jobMap[jp.Name] = jp;
                }
            }

            _rows.Add(MakeCameraRow("曝光时间", p.Exposure.ToString(CultureInfo.InvariantCulture), "μs", "线激光曝光时长", 0, jobMap, "camera_0_exposure_time"));
            _rows.Add(MakeCameraRow("激光强度", p.LaserPower.ToString(CultureInfo.InvariantCulture), "-", "激光功率/强度", 1, jobMap, "laser_intensity"));
            _rows.Add(MakeCameraRow("抗弧光", p.Gamma.ToString("F2", CultureInfo.InvariantCulture), "γ", "伽马校正（抑制弧光）", 2, jobMap, "camera_0_gamma"));

            // 提取算法 + ROI 边界（Job 参数，参照官方软件相机参数窗口）
            AddJobRow("提取算法", "baseline_extraction", "提取算法（抗反光）", jobMap);
            AddJobRow("上边界", "baseline_roi_upper", "ROI 上边界", jobMap);
            AddJobRow("下边界", "baseline_roi_bottom", "ROI 下边界", jobMap);
            AddJobRow("左边界", "baseline_roi_left", "ROI 左边界", jobMap);
            AddJobRow("右边界", "baseline_roi_right", "ROI 右边界", jobMap);
        }

        private CameraParamRow MakeCameraRow(string name, string value, string unit, string desc, int kind,
            Dictionary<string, IlJobParamInfo> jobMap, string jobKey)
        {
            var row = new CameraParamRow { Name = name, Value = value, Unit = unit, Desc = desc, Kind = kind };
            if (jobMap != null && jobMap.TryGetValue(jobKey, out IlJobParamInfo jp) && jp.HasRange)
            {
                row.Range = FormatRange(jp);
                row.JobParam = jp;
            }
            else
            {
                row.Range = "-";
            }
            return row;
        }

        private void AddJobRow(string name, string jobKey, string desc, Dictionary<string, IlJobParamInfo> jobMap)
        {
            if (jobMap == null) return;
            if (!jobMap.TryGetValue(jobKey, out IlJobParamInfo jp)) return;
            string valStr = jp.ValueType == 1u ? jp.StringValue
                : (jp.ValueType == 2u ? jp.FloatValue.ToString("F3", CultureInfo.InvariantCulture)
                : jp.IntValue.ToString(CultureInfo.InvariantCulture));
            string rangeStr = FormatRange(jp);
            _rows.Add(new CameraParamRow { Name = name, Value = valStr, Range = rangeStr, Unit = "-", Desc = desc, Kind = 3, JobParam = jp });
        }

        private bool ValidateRow(CameraParamRow row, string raw)
        {
            if (row == null || string.IsNullOrWhiteSpace(raw)) return false;
            if (row.Kind == 0 || row.Kind == 1)
            {
                if (!uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v)) return false;
                return CheckRange(row.JobParam, v);
            }
            if (row.Kind == 2)
            {
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return false;
                return CheckRange(row.JobParam, (double)v);
            }
            // Kind==3：Job 参数按其类型 + 范围
            var jp = row.JobParam;
            if (jp == null) return false;
            if (jp.ValueType == 1u) return true;
            if (jp.ValueType == 2u)
            {
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return false;
                return CheckRange(jp, d);
            }
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return false;
            return CheckRange(jp, l);
        }

        private static bool CheckRange(IlJobParamInfo jp, double v)
        {
            if (jp == null || !jp.HasRange) return true;
            if (jp.ValueType == 2u) return v >= jp.FloatMin && v <= jp.FloatMax;
            return v >= jp.IntMin && v <= jp.IntMax;
        }

        private static bool CheckRange(IlJobParamInfo jp, long v)
        {
            if (jp == null || !jp.HasRange) return true;
            return v >= jp.IntMin && v <= jp.IntMax;
        }

        private static string FormatRange(IlJobParamInfo jp)
        {
            if (jp == null || !jp.HasRange) return "-";
            return jp.ValueType == 2u
                ? string.Format("{0:F3} ~ {1:F3}", jp.FloatMin, jp.FloatMax)
                : string.Format("{0} ~ {1}", jp.IntMin, jp.IntMax);
        }

        private bool uiTableParam_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            var row = e.Record as CameraParamRow;
            if (row == null || e.Value == null) return true;
            if (e.Column.Key != "Value") return true;

            string raw = e.Value.ToString();
            if (!ValidateRow(row, raw)) return false;
            row.Value = raw;
            return true;
        }

        private void btnFetch_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            LoadParams();
            GlobalCommData.ShowLog("ILParamPage", "相机参数已从设备读取", MessageLevel.Info);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected)
            {
                AntdUI.Message.warn(this.Window, "设备未连接，无法保存参数！");
                return;
            }
            if (_rows.Count == 0)
            {
                AntdUI.Message.warn(this.Window, "无可保存的参数！");
                return;
            }

            // 1) 相机基础参数（曝光/激光强度/抗弧光）
            var param = new IlCameraParam();
            var jobEdits = new List<IlJobParamInfo>();
            foreach (var row in _rows)
            {
                string raw = row.Value == null ? string.Empty : row.Value.Trim();
                if (!ValidateRow(row, raw))
                {
                    AntdUI.Message.warn(this.Window, "参数格式错误或超范围：" + row.Name);
                    return;
                }
                if (row.Kind == 0)
                    param.Exposure = uint.Parse(raw, CultureInfo.InvariantCulture);
                else if (row.Kind == 1)
                    param.LaserPower = uint.Parse(raw, CultureInfo.InvariantCulture);
                else if (row.Kind == 2)
                    param.Gamma = float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
                else if (row.Kind == 3 && row.JobParam != null)
                {
                    var jp = row.JobParam;
                    var info = new IlJobParamInfo();
                    info.Name = jp.Name;
                    info.ValueType = jp.ValueType;
                    info.HasRange = jp.HasRange;
                    info.FloatMin = jp.FloatMin; info.FloatMax = jp.FloatMax;
                    info.IntMin = jp.IntMin; info.IntMax = jp.IntMax;
                    if (jp.ValueType == 1u)
                        info.StringValue = raw;
                    else if (jp.ValueType == 2u)
                        info.FloatValue = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
                    else
                        info.IntValue = long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    jobEdits.Add(info);
                }
            }

            int ret = _ilCamera.ApplyCameraParams(param);
            if (ret != 0)
            {
                AntdUI.Message.error(this.Window, string.Format("相机参数保存失败, ret={0}", ret));
                return;
            }
            if (jobEdits.Count > 0)
            {
                int retJob = _ilCamera.ApplyJobParams(jobEdits);
                if (retJob != 0)
                {
                    AntdUI.Message.error(this.Window, string.Format("提取算法/边界参数保存失败, ret={0}", retJob));
                    return;
                }
            }
            AntdUI.Message.success(this.Window, "相机参数已保存到设备");
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
