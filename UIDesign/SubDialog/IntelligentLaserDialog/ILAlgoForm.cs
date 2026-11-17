using AdaWeldSystem.LineLaserCam.ILineLaser;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.UIDesign.SubDialog.IntelligentLaserDialog
{
    /// <summary>英莱相机算法参数编辑页</summary>
    public partial class ILAlgoForm : UserControl
    {
        #region 私有变量
        private readonly IntelligentLaserCam _ilCamera;
        private List<IlJobParamInfo> _ilParams;
        private AntdUI.AntList<IlJobParamRow> _ilParamRows;
        private readonly Dictionary<int, AntdUI.Button> _jobButtons = new Dictionary<int, AntdUI.Button>();
        private List<IlJobDescInfo> _jobList = new List<IlJobDescInfo>();
        private int _selectedJobId = -1;
        #endregion

        #region 公共变量
        /// <summary>参数行数据模型</summary>
        public class IlJobParamRow
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Type { get; set; }
            public string Range { get; set; }
            public string Group { get; set; }
        }
        #endregion

        #region 构造函数

        public ILAlgoForm(IntelligentLaserCam ilCamera)
        {
            InitializeComponent();
            InitTableColumns();
            // 事件订阅放代码后置，避免设计器序列化丢失
            btnCreate.Click += btnCreate_Click;
            btnchangeJob.Click += btnchangeJob_Click;
            btnDeleteJob.Click += btnDeleteJob_Click;
            dgvILParams.CellEndEdit += dgvILParams_CellEndEdit;
            _ilCamera = ilCamera;
            this.Text = "英莱相机算法 (IntelligentLaser)";
            if (_ilCamera != null)
                _ilCamera.JobParamsChanged += OnJobParamsChanged;
            InitIlModule();
        }

        #endregion

        #region 私有函数

        private void InitTableColumns()
        {
            dgvILParams.Columns = new AntdUI.ColumnCollection
            {
                // Column(key, title)：key 必须等于 IlJobParamRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("Group", "分组") { Width = "100" },
                new AntdUI.Column("Name", "参数名") { Width = "260" },
                new AntdUI.Column("Value", "当前值(可编辑)") { Width = "240" },
                new AntdUI.Column("Type", "类型") { Width = "100" },
                new AntdUI.Column("Range", "范围") { Width = "240" }
            };
        }

        private void InitIlModule()
        {
            if (_ilCamera == null)
            {
                lblConn.Text = "当前相机非英莱，参数不可用";
                lblConn.ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);
                SetIlControlsEnabled(false);
                return;
            }
            UpdateConnLabel();
            bool connected = _ilCamera.IsConnected;
            SetIlControlsEnabled(connected);
            if (connected)
            {
                // 已连接：按 jobdesclist.json 建 JOB 按钮流并拉参数
                RefreshJobFlow();
                RefreshJobParams();
            }
            else
            {
                // 未连接设备即无 Job：清空按钮流与参数表，控件禁用
                ClearJobFlow();
                LoadIlParamsToGrid(null);
            }
        }

        private void ClearJobFlow()
        {
            flowPanel1.Controls.Clear();
            _jobButtons.Clear();
            _jobList.Clear();
            _selectedJobId = -1;
            lblSelectJointType.Text = "请选择JOB";
        }

        private void UpdateConnLabel()
        {
            if (_ilCamera == null) return;
            // 直读硬件真相源，不缓存连接副本
            if (!_ilCamera.IsConnected)
            {
                lblConn.Text = "英莱设备：未连接";
                lblConn.ForeColor = System.Drawing.Color.FromArgb(192, 0, 0);
                return;
            }
            string text = "英莱设备：已连接";
            // 追加当前 JOB 与接头类型语义（jobdesclist.json + jointtypelist.json）
            IlJobDescInfo job = _ilCamera.GetCurrentJobInfo();
            if (job != null)
            {
                string typeName = _ilCamera.GetJointTypeName(job.JointType);
                text += string.IsNullOrEmpty(typeName)
                    ? string.Format(" | JOB {0}", job.Id)
                    : string.Format(" | JOB {0} [{1}]", job.Id, typeName);
            }
            lblConn.Text = text;
            lblConn.ForeColor = System.Drawing.Color.FromArgb(0, 128, 0);
        }

        private void SetIlControlsEnabled(bool enabled)
        {
            btnchangeJob.Enabled = enabled;
            btnDeleteJob.Enabled = enabled;
            btnCreate.Enabled = enabled;
            foreach (var btn in _jobButtons.Values)
                btn.Enabled = enabled;
        }

        private void RefreshJobFlow()
        {
            flowPanel1.Controls.Clear();
            _jobButtons.Clear();
            _jobList.Clear();
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            _jobList = _ilCamera.FetchJobList();
            foreach (IlJobDescInfo job in _jobList)
            {
                string typeName = _ilCamera.GetJointTypeName(job.JointType);
                string text = string.IsNullOrEmpty(typeName)
                    ? string.Format("JOB {0}", job.Id)
                    : string.Format("JOB {0}\n{1}", job.Id, typeName);
                var btn = new AntdUI.Button
                {
                    Text = text,
                    Size = new System.Drawing.Size(120, 48),
                    Margin = new System.Windows.Forms.Padding(4)
                };
                int jobId = job.Id;
                btn.Click += (s, e) =>
                {
                    _selectedJobId = jobId;
                    UpdateJobButtonsState();
                };
                _jobButtons[jobId] = btn;
                flowPanel1.Controls.Add(btn);
            }
            // 选中态跟随设备当前 JOB（切 JOB 回调也会再次同步）
            SyncSelectedJobFromDevice();
        }

        private void SyncSelectedJobFromDevice()
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            _selectedJobId = _ilCamera.FetchCurrentJobId();
            UpdateJobButtonsState();
        }

        private void UpdateJobButtonsState()
        {
            foreach (var kv in _jobButtons)
                kv.Value.Type = (kv.Key == _selectedJobId) ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            if (_selectedJobId < 0)
            {
                lblSelectJointType.Text = "请选择JOB";
                return;
            }
            var job = _jobList.Find(j => j.Id == _selectedJobId);
            if (job == null)
            {
                lblSelectJointType.Text = "请选择JOB";
                return;
            }
            string typeName = _ilCamera.GetJointTypeName(job.JointType);
            lblSelectJointType.Text = string.IsNullOrEmpty(typeName)
                ? string.Format("JOB {0}", job.Id)
                : typeName;
        }

        private void btnchangeJob_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            if (_selectedJobId < 0)
            {
                AntdUI.Message.warn(this.FindForm(), "请先选择一个 JOB");
                return;
            }
            if (_ilCamera.FetchCurrentJobId() == _selectedJobId) return; // 已是当前 JOB
            int ret = _ilCamera.SwitchJob(_selectedJobId);
            if (ret != 0)
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("JOB {0} 切换失败, {1}", _selectedJobId,
                    _ilCamera.DescribeLastError(LineLaserErrorContext.ReturnCode)));
            }
        }

        private void btnDeleteJob_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            if (_selectedJobId < 0)
            {
                AntdUI.Message.warn(this.FindForm(), "请先选择要删除的 JOB");
                return;
            }
            if (_selectedJobId == _ilCamera.FetchCurrentJobId())
            {
                AntdUI.Message.warn(this.FindForm(), "正在使用的 JOB 不可删除");
                return;
            }
            if (AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(),
                "确认删除", string.Format("确定删除 JOB {0}？", _selectedJobId), TType.Warn)) != DialogResult.OK)
                return;
            int ret = _ilCamera.DeleteJob(_selectedJobId);
            if (ret == 0)
            {
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("JOB {0} 已删除", _selectedJobId), MessageLevel.Info);
                RefreshJobFlow();
            }
            else
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("JOB {0} 删除失败, {1}", _selectedJobId,
                    _ilCamera.DescribeLastError(LineLaserErrorContext.ReturnCode)));
            }
        }

        private void RefreshJobParams()
        {
            if (_ilCamera == null || !_ilCamera.IsConnected)
            {
                LoadIlParamsToGrid(null);
                return;
            }
            List<IlJobParamInfo> list = null;
            try { list = _ilCamera.FetchAllJobParams(); }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("读取英莱参数失败 {0}", ex.Message), MessageLevel.Error);
            }
            // 按当前 Job 接头类型 display 键集过滤；键缺失或为空时回退显示全部（键名不匹配兜底）
            if (list != null && list.Count > 0)
            {
                List<string> displayKeys = GetCurrentJobDisplayKeys();
                if (displayKeys != null && displayKeys.Count > 0)
                {
                    var filtered = list.FindAll(p => IsDisplayParam(displayKeys, p.Name));
                    if (filtered.Count > 0)
                        list = filtered;
                }
            }
            LoadIlParamsToGrid(list);
            UpdateJointTypeLabel();
        }

        private List<string> GetCurrentJobDisplayKeys()
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return null;
            IlJobDescInfo job = _ilCamera.GetCurrentJobInfo();
            if (job == null) return null;
            return _ilCamera.GetJointTypeDisplayKeys(job.JointType);
        }

        private static bool IsDisplayParam(List<string> displayKeys, string paramName)
        {
            if (displayKeys == null || displayKeys.Count == 0 || string.IsNullOrEmpty(paramName)) return true;
            foreach (string key in displayKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                // 1) 完全相等（display 键即 SDK 参数名）
                if (key.Equals(paramName, StringComparison.OrdinalIgnoreCase)) return true;
                // 2) 去 params_ 前缀后相等（SDK 参数名不带前缀）
                string keyCore = key.StartsWith("params_", StringComparison.OrdinalIgnoreCase)
                    ? key.Substring(7) : key;
                if (keyCore.Equals(paramName, StringComparison.OrdinalIgnoreCase)) return true;
                // 3) 词级后缀：参数名是核心键尾部（以 _ 分界）
                if (keyCore.Length > paramName.Length
                    && keyCore.EndsWith(paramName, StringComparison.OrdinalIgnoreCase)
                    && keyCore[keyCore.Length - paramName.Length - 1] == '_')
                    return true;
                // 4) 词级前缀：参数名以核心键开头且更长（_ 分界）
                if (paramName.Length > keyCore.Length
                    && paramName.StartsWith(keyCore, StringComparison.OrdinalIgnoreCase)
                    && paramName[keyCore.Length] == '_')
                    return true;
            }
            return false;
        }

        private void UpdateJointTypeLabel()
        {
            if (_ilCamera == null || !_ilCamera.IsConnected)
            {
                lblJointType.Text = "未连接";
                return;
            }
            IlJobDescInfo job = _ilCamera.GetCurrentJobInfo();
            if (job == null)
            {
                lblJointType.Text = "当前JOB未知";
                return;
            }
            string typeName = _ilCamera.GetJointTypeName(job.JointType);
            lblJointType.Text = string.IsNullOrEmpty(typeName)
                ? string.Format("JOB {0}", job.Id)
                : string.Format("JOB {0} - {1}", job.Id, typeName);
        }

        private void LoadIlParamsToGrid(List<IlJobParamInfo> list)
        {
            if (_ilParamRows == null)
                _ilParamRows = new AntdUI.AntList<IlJobParamRow>();
            else
                _ilParamRows.Clear();

            if (list == null) { dgvILParams.Binding(_ilParamRows); return; }
            _ilParams = list;
            foreach (var p in list)
            {
                string typeStr = p.ValueType == 1u ? "string" : (p.ValueType == 2u ? "float" : "int");
                string valStr = FormatCellValue(p);
                string rangeStr = p.HasRange
                    ? (p.ValueType == 2u ? string.Format("{0:F3} ~ {1:F3}", p.FloatMin, p.FloatMax)
                        : string.Format("{0} ~ {1}", p.IntMin, p.IntMax))
                    : "-";
                _ilParamRows.Add(new IlJobParamRow { Name = p.Name, Value = valStr, Type = typeStr, Range = rangeStr, Group = GetParamGroup(p.Name) });
            }
            dgvILParams.Binding(_ilParamRows);
        }

        private static string GetParamGroup(string name)
        {
            if (string.IsNullOrEmpty(name)) return "其他";
            if (name.StartsWith("camera_", StringComparison.OrdinalIgnoreCase)) return "相机";
            if (name.StartsWith("after_inspect_", StringComparison.OrdinalIgnoreCase)) return "结果处理";
            if (name.StartsWith("inspect_", StringComparison.OrdinalIgnoreCase)) return "检测";
            if (name.StartsWith("segment_", StringComparison.OrdinalIgnoreCase)) return "分段";
            if (name.StartsWith("baseline_", StringComparison.OrdinalIgnoreCase)) return "基线提取";
            return "其他";
        }

        private void OnJobParamsChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                Invoke(new Action<object, EventArgs>(OnJobParamsChanged), sender, e);
                return;
            }
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            try
            {
                // 跟随设备当前 JOB，刷新参数与连接标签
                SyncSelectedJobFromDevice();
                RefreshJobParams();
                UpdateConnLabel();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("Job 参数刷新失败 {0}", ex.Message), MessageLevel.Error);
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (_ilCamera == null || !_ilCamera.IsConnected) return;
            List<IlJointTypeInfo> jointTypes = _ilCamera.FetchJointTypes();
            if (jointTypes.Count == 0)
            {
                AntdUI.Message.warn(this.FindForm(), "无法读取接头类型列表（jointtypelist.json 缺失或为空）");
                return;
            }
            // 弹窗内容：每接头类型一个按钮；内容控件须给明确尺寸（Dock=Fill 裸容器在 Modal 内高度塌缩为 0）
            var flow = new System.Windows.Forms.FlowLayoutPanel
            {
                Width = 300,
                Height = 360,
                AutoScroll = true,
                Padding = new System.Windows.Forms.Padding(8)
            };
            foreach (IlJointTypeInfo jt in jointTypes)
            {
                int type = jt.Int;
                string name = jt.NameZh;
                var b = new AntdUI.Button
                {
                    Text = string.Format("{0} ({1})", name, type),
                    Size = new System.Drawing.Size(150, 40),
                    Margin = new System.Windows.Forms.Padding(4)
                };
                b.Click += (s2, e2) =>
                {
                    int ret = _ilCamera.CreateJob(type, "");
                    var host = flow.FindForm();
                    if (ret == 0)
                    {
                        GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("JOB 新建成功（{0}）", name), MessageLevel.Info);
                        if (host != null) host.DialogResult = DialogResult.OK;
                        RefreshJobFlow();
                        RefreshJobParams();
                    }
                    else
                    {
                        AntdUI.Message.warn(this.FindForm(), string.Format("JOB 新建失败, {0}",
                            _ilCamera.DescribeLastError(LineLaserErrorContext.ReturnCode)));
                    }
                };
                flow.Controls.Add(b);
            }
            // Modal.Config 4 参构造：TType.Info + 明确 Width，避免内容区尺寸异常导致空白
            AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "新建JOB - 选择接头类型", flow, TType.Info)
            {
                Width = 320,
                CloseIcon = true,
                BtnHeight = 0
            });
        }

        private bool dgvILParams_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            if (!(e.Record is IlJobParamRow row) || e.Value == null || e.Column.Key != "Value") return true;
            if (_ilCamera == null || !_ilCamera.IsConnected) return false;
            if (_ilParams == null || _ilParamRows == null) return false;

            int idx = _ilParamRows.IndexOf(row);
            if (idx < 0 || idx >= _ilParams.Count) return false;
            var src = _ilParams[idx];

            string raw = e.Value.ToString().Trim();
            if (!ParseAndValidate(src, raw))
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("参数格式错误或超出范围：{0}", src.Name));
                return false; // 恢复单元格原值
            }

            // 单条下发：仅当前编辑的参数
            var edited = new List<IlJobParamInfo> { BuildParamInfo(src, raw) };
            int ret = _ilCamera.ApplyJobParams(edited);
            if (ret != 0)
            {
                AntdUI.Message.warn(this.FindForm(), string.Format("参数 {0} 下发失败, {1}", src.Name,
                    _ilCamera.DescribeLastError(LineLaserErrorContext.ReturnCode)));
                return false; // 恢复单元格原值
            }

            // 成功：从设备回读该参数当前值，刷新单元格显示
            try
            {
                var list = _ilCamera.FetchAllJobParams();
                var updated = list.Find(x => x.Name == src.Name);
                if (updated != null)
                {
                    _ilParams[idx] = updated;
                    row.Value = FormatCellValue(updated);
                }
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("英莱参数已下发 {0}", src.Name), MessageLevel.Info);
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("回读参数失败 {0}", ex.Message), MessageLevel.Error);
            }
            return true;
        }

        private static bool ParseAndValidate(IlJobParamInfo src, string raw)
        {
            if (src.ValueType == 1u) return true; // string 无需范围校验
            if (src.ValueType == 2u)
            {
                if (!double.TryParse(raw, out double d)) return false;
                return !src.HasRange || (d >= src.FloatMin && d <= src.FloatMax);
            }
            if (src.ValueType == 3u)
            {
                if (!long.TryParse(raw, out long l)) return false;
                return !src.HasRange || (l >= src.IntMin && l <= src.IntMax);
            }
            return false;
        }

        private static IlJobParamInfo BuildParamInfo(IlJobParamInfo src, string raw)
        {
            var info = new IlJobParamInfo
            {
                Name = src.Name,
                ValueType = src.ValueType,
                HasRange = src.HasRange,
                FloatMin = src.FloatMin, FloatMax = src.FloatMax,
                IntMin = src.IntMin, IntMax = src.IntMax
            };
            if (src.ValueType == 1u)
                info.StringValue = raw;
            else if (src.ValueType == 2u)
                info.FloatValue = double.Parse(raw);
            else if (src.ValueType == 3u)
                info.IntValue = long.Parse(raw);
            return info;
        }

        private static string FormatCellValue(IlJobParamInfo p)
        {
            if (p.ValueType == 1u) return p.StringValue;
            if (p.ValueType == 2u) return p.FloatValue.ToString("F3");
            return p.IntValue.ToString();
        }

        #endregion

        #region 公共函数

        /// <summary>退订 Job 参数变更事件</summary>
        public void StopRefresh()
        {
            // 由 AlgorithmEditForm.HandleDestroyed 调用；结果显示在 ILCamPage，本页不订阅轮廓帧就绪
            if (_ilCamera != null)
                _ilCamera.JobParamsChanged -= OnJobParamsChanged;
        }

        #endregion
    }
}
