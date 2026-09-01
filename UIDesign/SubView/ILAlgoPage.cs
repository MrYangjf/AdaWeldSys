using AdaWeldSystem.Comm;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    /// <summary>英莱相机算法参数编辑页</summary>
    public partial class ILAlgoPage : UserControl
    {
        #region 私有变量
        private readonly IntelligentLaserCameraRun _ilCamera;
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

        public ILAlgoPage(IntelligentLaserCameraRun ilCamera)
        {
            InitializeComponent();
            InitTableColumns();
            // 事件订阅放代码后置（Designer 手写订阅会被设计器重新序列化时丢失）
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
                // 已连接：按 jobdesclist.json 构建 JOB 按钮流面板 + 拉取当前 JOB 实时参数（设备为唯一真相源）
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
            // 直接读硬件真相源，不缓存本地连接副本（避免与 IsConnected 脱节）
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
                btn.Click += (s, e) => JobButton_Click(jobId);
                _jobButtons[jobId] = btn;
                flowPanel1.Controls.Add(btn);
            }
            // 选中态跟随设备当前 JOB（切 JOB 回调也会再次同步）
            SyncSelectedJobFromDevice();
        }

        private void JobButton_Click(int jobId)
        {
            _selectedJobId = jobId;
            UpdateJobButtonsState();
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
                AntdUI.Message.warn(this.FindForm(), string.Format("JOB {0} 切换失败, ret={1}", _selectedJobId, ret));
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
                AntdUI.Message.warn(this.FindForm(), string.Format("JOB {0} 删除失败, ret={1}", _selectedJobId, ret));
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
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("读取英莱参数失败: {0}", ex.Message), MessageLevel.Error);
            }
            // 按当前 Job 接头类型 display 键集过滤（display 缺失/为空则回退显示全部；
            // 过滤后为空同样回退全显——display 键与 SDK 参数名格式不匹配时保底，避免参数表空白）
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
                // 3) 词级后缀：参数名是 display 核心键的尾部（词边界 _ 分界，如 display params_inspect_min_angle ↔ 参数名 min_angle）
                if (keyCore.Length > paramName.Length
                    && keyCore.EndsWith(paramName, StringComparison.OrdinalIgnoreCase)
                    && keyCore[keyCore.Length - paramName.Length - 1] == '_')
                    return true;
                // 4) 词级前缀：参数名更长且以 display 核心键开头（词边界，如参数名 params_inspect_min_angle_more ↔ display params_inspect_min_angle）
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
                // 选中态跟随设备当前 JOB + 刷新参数表 + 更新连接/接头类型标签
                SyncSelectedJobFromDevice();
                RefreshJobParams();
                UpdateConnLabel();
            }
            catch (Exception ex)
            {
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("Job 参数刷新失败: {0}", ex.Message), MessageLevel.Error);
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
            // 弹窗内容：FlowLayoutPanel 内每个接头类型一个按钮（参照 ParamConfigPage.ShowInputStringDialog 范式：
            // 内容控件必须给明确尺寸——Dock=Fill 的裸容器在 Modal 内容区高度塌缩为 0 导致空白）
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
                        AntdUI.Message.warn(this.FindForm(), string.Format("JOB 新建失败, ret={0}", ret));
                    }
                };
                flow.Controls.Add(b);
            }
            // 参照 ParamConfigPage.ShowInputStringDialog 的 Modal.Config 4 参构造范式：
            // TType.Info + 明确 Width，避免内容区高度/宽度异常导致空白
            AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "新建JOB - 选择接头类型", flow, TType.Info)
            {
                Width = 320,
                CloseIcon = true,
                BtnHeight = 0
            });
        }

        private bool dgvILParams_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            var row = e.Record as IlJobParamRow;
            if (row == null || e.Value == null || e.Column.Key != "Value") return true;
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
                AntdUI.Message.warn(this.FindForm(), string.Format("参数 {0} 下发失败, ret={1}", src.Name, ret));
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
                GlobalCommData.ShowLog("AlgorithmEditForm", string.Format("回读参数失败: {0}", ex.Message), MessageLevel.Error);
            }
            return true;
        }

        private static bool ParseAndValidate(IlJobParamInfo src, string raw)
        {
            if (src.ValueType == 1u) return true; // string 无需范围校验
            if (src.ValueType == 2u)
            {
                double d;
                if (!double.TryParse(raw, out d)) return false;
                return !src.HasRange || (d >= src.FloatMin && d <= src.FloatMax);
            }
            if (src.ValueType == 3u)
            {
                long l;
                if (!long.TryParse(raw, out l)) return false;
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
            // 由 AlgorithmEditForm.HandleDestroyed 调用（ADR-001：不在 .cs 重写 Dispose(bool)）；结果显示已迁移至 ILCamPage，本页不再订阅 ContourFrameReady。
            if (_ilCamera != null)
                _ilCamera.JobParamsChanged -= OnJobParamsChanged;
        }

        #endregion
    }
}
