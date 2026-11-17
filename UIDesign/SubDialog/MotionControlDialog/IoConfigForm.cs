using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;

namespace AdaWeldSystem.UIDesign.SubDialog.MotionControlDialog
{
    /// <summary>IO 完整参数编辑（弹窗内使用，仅 Commit 落回配置实例）</summary>
    /// <remarks>名称只读，避免破坏输入/输出分表按名取 IO 的引用。</remarks>
    public partial class IoConfigForm : UserControl
    {
        private readonly DeltaIOConfig _config;

        private readonly TextBox _txtName;
        private readonly TextBox _txtNodeId;
        private readonly TextBox _txtSlotNo;
        private readonly TextBox _txtBitNo;
        private readonly CheckBox _chkEnabled;
        private readonly TextBox _txtRemark;

        public IoConfigForm(DeltaIOConfig config)
        {
            _config = config ?? throw new ArgumentNullException("config");

            _txtName = new TextBox { ReadOnly = true };
            _txtNodeId = new TextBox();
            _txtSlotNo = new TextBox();
            _txtBitNo = new TextBox();
            _chkEnabled = new CheckBox { Text = "启用", Dock = DockStyle.Fill };
            _txtRemark = new TextBox();

            this.AutoScroll = true;
            this.Size = new Size(420, 240);
            this.Padding = new Padding(8);

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < grid.RowCount; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            }

            AddText(grid, 0, "IO名称", _txtName);
            AddText(grid, 1, "站点号 NodeId", _txtNodeId);
            AddText(grid, 2, "槽号 SlotNo", _txtSlotNo);
            AddText(grid, 3, "位号 BitNo", _txtBitNo);
            AddCheck(grid, 4, "启用", _chkEnabled);
            AddText(grid, 5, "备注 Remark", _txtRemark);

            // 底部确定/取消：AntdUI 弹窗需内部按钮回写宿主 DialogResult
            Panel bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8, 6, 8, 6) };
            Button btnOk = new Button { Text = "确定", Width = 90, Dock = DockStyle.Right, BackColor = Color.FromArgb(22, 119, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnCancel = new Button { Text = "取消", Width = 90, Dock = DockStyle.Right };
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            bottomPanel.Controls.Add(btnOk);
            bottomPanel.Controls.Add(btnCancel);

            this.Controls.Add(bottomPanel);
            this.Controls.Add(grid);
            LoadValues();
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        private static void AddText(TableLayoutPanel grid, int row, string label, TextBox box)
        {
            box.Dock = DockStyle.Fill;
            Label lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(lbl, 0, row);
            grid.Controls.Add(box, 1, row);
        }

        private static void AddCheck(TableLayoutPanel grid, int row, string label, CheckBox box)
        {
            Label lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(lbl, 0, row);
            grid.Controls.Add(box, 1, row);
        }

        private void LoadValues()
        {
            _txtName.Text = _config.IOName;
            _txtNodeId.Text = _config.NodeId.ToString(CultureInfo.InvariantCulture);
            _txtSlotNo.Text = _config.SlotNo.ToString(CultureInfo.InvariantCulture);
            _txtBitNo.Text = _config.BitNo.ToString(CultureInfo.InvariantCulture);
            _chkEnabled.Checked = _config.Enabled;
            _txtRemark.Text = _config.Remark == null ? string.Empty : _config.Remark;
        }
        private static ushort ParseUShort(string s, ushort def)
        {
            return ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort v) ? v : def;
        }
        /// <summary>把界面数值写回配置实例；解析失败保留原值</summary>
        public void Commit()
        {
            _config.NodeId = ParseUShort(_txtNodeId.Text, _config.NodeId);
            _config.SlotNo = ParseUShort(_txtSlotNo.Text, _config.SlotNo);
            _config.BitNo = ParseUShort(_txtBitNo.Text, _config.BitNo);
            _config.Enabled = _chkEnabled.Checked;
            _config.Remark = _txtRemark.Text == null ? string.Empty : _txtRemark.Text.Trim();
        }


    }
}
