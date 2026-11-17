using AdaWeldSystem.ProductFileControl;
using AntdUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.SubDialog.DataReviewerDialog
{
    /// <summary>选择正常/临时文件，临时文件可解析为点云</summary>
    public partial class FileTraceForm : UserControl
    {
        private readonly Window Window;

        // 与两个 Table.Binding() 绑定的强类型列表
        private AntdUI.AntList<FileTraceRow> _recordRows;
        private AntdUI.AntList<FileTraceRow> _tempRows;

        // 当前选中行（单选：同一列表一次仅一项 Selected=true）
        private FileTraceRow _selectedRecord;
        private FileTraceRow _selectedTemp;

        /// <summary>用户选择的文件路径</summary>
        public string SelectedFilePath { get; private set; }

        // 注入父 Window，内部方法用 this.Window 作 owner
        public FileTraceForm() : this(null) { }

        public FileTraceForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            InitTableColumns();
            _recordRows = new AntdUI.AntList<FileTraceRow>();
            uiTableRecord.Binding(_recordRows);
            _tempRows = new AntdUI.AntList<FileTraceRow>();
            uiTableTemp.Binding(_tempRows);
            // 事件订阅放代码后置，避免设计器序列化丢失
            uiTableRecord.CellClick += uiTableRecord_CellClick;
            uiTableTemp.CellClick += uiTableTemp_CellClick;
            LoadFileList();
        }

        /// <summary>初始化表格列：Columns 须代码后置；选取列用 ColumnCheck+SetAutoCheck(false) 单选互斥。</summary>
        private void InitTableColumns()
        {
            uiTableRecord.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("No", "No.") { Width = "60" },
                new AntdUI.ColumnCheck("Selected", "选取") { Width = "80" }.SetAutoCheck(false),
                new AntdUI.Column("FileName", "文件名") { Width = "400" }
            };
            uiTableRecord.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;

            uiTableTemp.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("No", "No.") { Width = "60" },
                new AntdUI.ColumnCheck("Selected", "选取") { Width = "80" }.SetAutoCheck(false),
                new AntdUI.Column("FileName", "文件名") { Width = "400" }
            };
            uiTableTemp.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        }

        /// <summary>加载文件列表</summary>
        private void LoadFileList()
        {
            _recordRows.Clear();
            _tempRows.Clear();
            _selectedRecord = null;
            _selectedTemp = null;

            // 加载正常记录文件
            List<string> recordFiles = ProductFileRecordManager.Instance.GetRecordFiles();
            int no = 1;
            foreach (var file in recordFiles)
            {
                string fileName = Path.GetFileName(file);
                string timeStr = File.GetCreationTime(file).ToString("yyyy-MM-dd HH:mm:ss");
                string display = $"[{timeStr}] {fileName}";
                _recordRows.Add(new FileTraceRow { No = no++, FileName = display, FilePath = file });
            }

            // 加载临时文件
            List<string> tempFiles = ProductFileRecordManager.Instance.GetTempFiles();
            no = 1;
            foreach (var file in tempFiles)
            {
                string fileName = Path.GetFileName(file);
                string timeStr = File.GetCreationTime(file).ToString("yyyy-MM-dd HH:mm:ss");
                string display = $"[{timeStr}] {fileName}";
                _tempRows.Add(new FileTraceRow { No = no++, FileName = display, FilePath = file });
            }

            // 默认选中第一个文件
            if (_recordRows.Count > 0)
            {
                _selectedRecord = _recordRows[0];
                _recordRows[0].Selected = true;
            }
            if (_tempRows.Count > 0)
            {
                _selectedTemp = _tempRows[0];
                _tempRows[0].Selected = true;
            }

            uiLabelRecordCount.Text = $"共 {_recordRows.Count} 个文件";
            uiLabelTempCount.Text = $"共 {_tempRows.Count} 个文件";
        }

        /// <summary>正常记录表格：点击任意单元格选中该行（单选互</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>斥）</remarks>
        private void uiTableRecord_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            if (!(e.Record is FileTraceRow row)) return;
            _selectedRecord = row;
            foreach (var r in _recordRows)
            {
                r.Selected = (r == row);
            }
        }

        /// <summary>临时文件表格：点击任意单元格选中该行（单选互</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        /// <remarks>斥）</remarks>
        private void uiTableTemp_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            if (!(e.Record is FileTraceRow row)) return;
            _selectedTemp = row;
            foreach (var r in _tempRows)
            {
                r.Selected = (r == row);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadFileList();
        }

        private void btnSelectRecord_Click(object sender, EventArgs e)
        {
            if (_selectedRecord == null)
            {
                AntdUI.Message.warn(this.Window, "请先从正常文件列表中选择一个文件！");
                return;
            }

            SelectedFilePath = _selectedRecord.FilePath;
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void btnSelectTemp_Click(object sender, EventArgs e)
        {
            if (_selectedTemp == null)
            {
                AntdUI.Message.warn(this.Window, "请先从临时文件列表中选择一个文件！");
                return;
            }

            SelectedFilePath = _selectedTemp.FilePath;
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void btnConvertTemp_Click(object sender, EventArgs e)
        {
            if (_selectedTemp == null)
            {
                AntdUI.Message.warn(this.Window, "请先从临时文件列表中选择一个要转换的文件！");
                return;
            }

            try
            {
                string outputName = string.Format("Recovered_{0:yyyyMMdd_HHmmss}", DateTime.Now);
                var resultFiles = ProductFileRecordManager.Instance.ConvertTempToNormal(_selectedTemp.FilePath, outputName);

                if (resultFiles.Count > 0)
                {
                    AntdUI.Message.success(this.Window, $"转换成功！\n生成文件：\n{string.Join("\n", resultFiles)}");
                    LoadFileList();
                }
                else
                {
                    AntdUI.Message.error(this.Window, "临时文件转换失败，请检查文件是否损坏！");
                }
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this.Window, $"转换失败：{ex.Message}");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        /// <summary>表格行模型：序号/选取/文件名/路径（隐藏）</summary>
        private class FileTraceRow : AntdUI.NotifyProperty
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

            public string FileName { get; set; }
            public string FilePath { get; set; }
        }
    }
}
