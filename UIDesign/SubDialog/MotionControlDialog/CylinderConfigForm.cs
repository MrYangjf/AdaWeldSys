using System;
using System.Drawing;
using System.Windows.Forms;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;

namespace AdaWeldSystem.UIDesign.SubDialog.MotionControlDialog
{
    /// <summary>气缸完整参数编辑（弹窗内使用，仅 Commit 落回配置实例）</summary>
    /// <remarks>名称只读，避免破坏按名取气缸的引用；各字段存 IO 名称，空串表示未配置该点位。</remarks>
    public partial class CylinderConfigForm : UserControl
    {
        private readonly DeltaCylinderConfig _config;

        private readonly TextBox _txtName;
        private readonly TextBox _txtStretchSensor;
        private readonly TextBox _txtRetractSensor;
        private readonly TextBox _txtCenterSensor;
        private readonly TextBox _txtStretchOutput;
        private readonly TextBox _txtRetractOutput;
        private readonly TextBox _txtStretchAction;
        private readonly TextBox _txtRetractAction;

        public CylinderConfigForm(DeltaCylinderConfig config)
        {
            _config = config ?? throw new ArgumentNullException("config");

            _txtName = new TextBox { ReadOnly = true };
            _txtStretchSensor = new TextBox();
            _txtRetractSensor = new TextBox();
            _txtCenterSensor = new TextBox();
            _txtStretchOutput = new TextBox();
            _txtRetractOutput = new TextBox();
            _txtStretchAction = new TextBox();
            _txtRetractAction = new TextBox();

            this.AutoScroll = true;
            this.Size = new Size(440, 320);
            this.Padding = new Padding(8);

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < grid.RowCount; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            }

            AddText(grid, 0, "气缸名称", _txtName);
            AddText(grid, 1, "伸出传感器名", _txtStretchSensor);
            AddText(grid, 2, "缩回传感器名", _txtRetractSensor);
            AddText(grid, 3, "中间位传感器名", _txtCenterSensor);
            AddText(grid, 4, "伸出电磁阀名", _txtStretchOutput);
            AddText(grid, 5, "缩回电磁阀名", _txtRetractOutput);
            AddText(grid, 6, "伸出动作显示名", _txtStretchAction);
            AddText(grid, 7, "缩回动作显示名", _txtRetractAction);

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

        private void LoadValues()
        {
            _txtName.Text = _config.CylinderName;
            _txtStretchSensor.Text = _config.StretchSensorName;
            _txtRetractSensor.Text = _config.RetractSensorName;
            _txtCenterSensor.Text = _config.CenterSensorName;
            _txtStretchOutput.Text = _config.StretchOutputName;
            _txtRetractOutput.Text = _config.RetractOutputName;
            _txtStretchAction.Text = _config.StretchActionName;
            _txtRetractAction.Text = _config.RetractActionName;
        }
        private static string TrimOrEmpty(string s)
        {
            return s == null ? string.Empty : s.Trim();
        }
        /// <summary>把界面文本写回配置实例（空串表示未配置点位）</summary>
        public void Commit()
        {
            _config.StretchSensorName = TrimOrEmpty(_txtStretchSensor.Text);
            _config.RetractSensorName = TrimOrEmpty(_txtRetractSensor.Text);
            _config.CenterSensorName = TrimOrEmpty(_txtCenterSensor.Text);
            _config.StretchOutputName = TrimOrEmpty(_txtStretchOutput.Text);
            _config.RetractOutputName = TrimOrEmpty(_txtRetractOutput.Text);
            _config.StretchActionName = TrimOrEmpty(_txtStretchAction.Text);
            _config.RetractActionName = TrimOrEmpty(_txtRetractAction.Text);
        }


    }
}
