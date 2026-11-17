﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.WeldParamControl;

namespace AdaWeldSystem.UIDesign.SubDialog.MotionControlDialog
{
    /// <summary>轴完整参数编辑（弹窗内使用，仅 Commit 落回配置实例）</summary>
    /// <remarks>名称只读，避免破坏按名称取轴的关键引用。字段按台达原生参数结构组织。
    /// 两段式：通用区（落 ROOT/AXES/AXIS）为两轴共用；轴特有区仅在构造时传入
    /// <see cref="DeltaTaHomeConfig"/> 才出现（TA 轴，落 ROOT/TA_HOME）。
    /// ⛔ Y 轴仍走单参构造：行序与容错语义不变。
    /// 布局按字体实测：行高下限 30，标签列宽取实测；输入列等宽拉伸，输入框垂直居中。
    /// 内容超高时整表竖向滚动（Panel.AutoScroll），各缩放不裁切。
    /// </remarks>
    public partial class AxisConfigForm : UserControl
    {
        private sealed class RowSpec
        {
            public string Caption;
            public Control Editor;   // null 表示分区标题
            public bool IsSection;
        }

        private readonly DeltaAxisConfig _config;
        private readonly DeltaTaHomeConfig _taHome;

        private readonly TextBox _txtName;
        private readonly TextBox _txtNodeId;
        private readonly TextBox _txtSlotNo;
        private readonly CheckBox _chkEnabled;
        private readonly TextBox _txtEquiv;
        private readonly TextBox _txtStrVel;
        private readonly TextBox _txtConstVel;
        private readonly TextBox _txtEndVel;
        private readonly TextBox _txtAccTime;
        private readonly TextBox _txtDecTime;
        private readonly TextBox _txtSTime;
        private readonly TextBox _txtHomeMode;
        private readonly TextBox _txtHomeOffset;
        private readonly TextBox _txtHomeFirstVel;
        private readonly TextBox _txtHomeSecondVel;
        private readonly TextBox _txtHomeAcc;
        private readonly TextBox _txtPositiveLimit;
        private readonly TextBox _txtNegativeLimit;
        private readonly TextBox _txtForce100Pct;
        private readonly TextBox _txtTorqueSlope;
        private readonly TextBox _txtTorqueLimit;
        private readonly ComboBox _cboTorqueDirection;

        /// <summary>「确定」时是否已把界面值写回配置实例</summary>
        private bool _committed;

        /// <summary>是否携带轴特有区（TA 轴）</summary>
        public bool HasAxisSpecificSection
        {
            get { return _taHome != null; }
        }

        /// <summary>用户在弹窗内点「确定」且校验通过、界面值已写回配置实例</summary>
        /// <remarks>宿主据此决定是否落盘。⛔ 不要用 <c>Modal.open</c> 的返回值判定：
        /// 弹窗关闭后内容控件已被销毁，界面值取不到。</remarks>
        public bool Committed
        {
            get { return _committed; }
        }

        /// <summary>Y 轴等通用轴编辑（行为与改动前一致）</summary>
        /// <param name="config">配置对象。</param>
        public AxisConfigForm(DeltaAxisConfig config)
            : this(config, null)
        {
        }

        /// <summary>TA 轴编辑：通用区 + 回零/力矩特有区（特有区落 ROOT/TA_HOME）</summary>
        /// <param name="config">该轴的 AXIS 配置（通用区落点）</param>
        /// <param name="taHome">TA 回零参数组（特有区落点）；传 null 即退化为通用轴编辑</param>
        public AxisConfigForm(DeltaAxisConfig config, DeltaTaHomeConfig taHome)
        {
            _config = config ?? throw new ArgumentNullException("config");
            _taHome = taHome;

            // 统一字体口径：按字体实测布局，不跟随宿主 AutoScale
            this.AutoScaleMode = AutoScaleMode.None;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = Color.White;

            _txtName = new TextBox { ReadOnly = true };
            _txtNodeId = new TextBox();
            _txtSlotNo = new TextBox();
            _chkEnabled = new CheckBox { Text = "启用", AutoSize = true };
            _txtEquiv = new TextBox();
            _txtStrVel = new TextBox();
            _txtConstVel = new TextBox();
            _txtEndVel = new TextBox();
            _txtAccTime = new TextBox();
            _txtDecTime = new TextBox();
            _txtSTime = new TextBox();
            _txtHomeMode = new TextBox();
            _txtHomeOffset = new TextBox();
            _txtHomeFirstVel = new TextBox();
            _txtHomeSecondVel = new TextBox();
            _txtHomeAcc = new TextBox();
            _txtPositiveLimit = new TextBox();
            _txtNegativeLimit = new TextBox();
            _txtForce100Pct = _taHome == null ? null : new TextBox();
            _txtTorqueSlope = _taHome == null ? null : new TextBox();
            _txtTorqueLimit = _taHome == null ? null : new TextBox();
            _cboTorqueDirection = _taHome == null ? null : new ComboBox();
            if (_cboTorqueDirection != null)
            {
                _cboTorqueDirection.DropDownStyle = ComboBoxStyle.DropDownList;
                _cboTorqueDirection.Items.Add("+（正向出力）");
                _cboTorqueDirection.Items.Add("-（反向出力）");
            }

            List<RowSpec> rows = BuildRowSpecs();
            BuildLayout(rows);
            LoadValues();
        }

        /// <summary>按轴类型生成行清单</summary>
        /// <remarks>Y 轴字段顺序与本轮改动前完全一致</remarks>
        /// <returns>结果集合。</returns>
        private List<RowSpec> BuildRowSpecs()
        {
            List<RowSpec> rows = new List<RowSpec>();
            rows.Add(FieldRow("轴名称", _txtName));
            rows.Add(FieldRow("站点号 NodeId", _txtNodeId));
            rows.Add(FieldRow("槽号 SlotNo", _txtSlotNo));
            rows.Add(EditorRow("启用", _chkEnabled));
            rows.Add(FieldRow("脉冲当量 Equiv", _txtEquiv));
            rows.Add(FieldRow("起始速度 StrVel", _txtStrVel));
            rows.Add(FieldRow("匀速 ConstVel", _txtConstVel));
            rows.Add(FieldRow("末速度 EndVel", _txtEndVel));
            rows.Add(FieldRow("加速时间 AccTime(s)", _txtAccTime));
            rows.Add(FieldRow("减速时间 DecTime(s)", _txtDecTime));
            rows.Add(FieldRow("S曲线时间 STime(s)", _txtSTime));

            if (_taHome == null)
            {
                rows.Add(FieldRow("回零模式 HomeMode", _txtHomeMode));
                rows.Add(FieldRow("原点偏移 HomeOffset", _txtHomeOffset));
                rows.Add(FieldRow("寻找速度 HomeFirstVel", _txtHomeFirstVel));
                rows.Add(FieldRow("爬行速度 HomeSecondVel", _txtHomeSecondVel));
                rows.Add(FieldRow("回零加减速 HomeAcc(ms)", _txtHomeAcc));
            }
            else
            {
                rows.Add(Section("回零参数（ROOT/TA_HOME，下次回零生效）"));
                rows.Add(FieldRow("回零模式 16#6098（-4~35）", _txtHomeMode));
                rows.Add(FieldRow("原点偏移（AXIS 落点）", _txtHomeOffset));
                rows.Add(FieldRow("寻找速度 6099:01（rpm）", _txtHomeFirstVel));
                rows.Add(FieldRow("爬行速度 6099:02（rpm）", _txtHomeSecondVel));
                rows.Add(FieldRow("回零加减速 609A（ms）", _txtHomeAcc));
                rows.Add(Section("力—力矩换算（TA_Auto；落 ROOT/TA_HOME）"));
                rows.Add(FieldRow("100% 力量基准 TA_FORCE_100PCT_N（N）", _txtForce100Pct));
                rows.Add(Section("力矩输出（CST 恒力下压；16#6072 与位置模式推动阻抗共用同一上限）"));
                rows.Add(FieldRow("扭矩爬升速率 TORQUE_SLOPE（0.1%/s）", _txtTorqueSlope));
                rows.Add(FieldRow("扭矩上限 TA_TORQUE_LIMIT_N（N）", _txtTorqueLimit));
                rows.Add(EditorRow("扭矩方向 TA_TORQUE_DIRECTION（+ / -）", _cboTorqueDirection));
            }

            rows.Add(FieldRow("正向软限位 PositiveLimit", _txtPositiveLimit));
            rows.Add(FieldRow("负向软限位 NegativeLimit", _txtNegativeLimit));
            return rows;
        }

        private static RowSpec FieldRow(string caption, TextBox box)
        {
            return new RowSpec { Caption = caption, Editor = box, IsSection = false };
        }

        private static RowSpec EditorRow(string caption, Control editor)
        {
            return new RowSpec { Caption = caption, Editor = editor, IsSection = false };
        }

        private static RowSpec Section(string caption)
        {
            return new RowSpec { Caption = caption, Editor = null, IsSection = true };
        }

        /// <summary>按内容实测布局</summary>
        /// <remarks>行高/列宽/总高都由字体度量推导，不写死尺寸</remarks>
        private void BuildLayout(List<RowSpec> rows)
        {
            int rowHeight = Math.Max(this.Font.Height + 14, 30);
            int sectionHeight = rowHeight + 6;
            int pad = Math.Max(10, this.Font.Height / 2 + 6);
            int inputWidth = Math.Max(220, this.Font.Height * 14);

            // 标签列宽取「字段行」标签的实测最大宽度；分区标题跨两列，不参与列宽计算
            Font boldFont = new Font(this.Font, FontStyle.Bold);
            int labelWidth = 150;
            foreach (RowSpec r in rows)
            {
                if (r.IsSection) continue;
                int w = TextRenderer.MeasureText(r.Caption, this.Font).Width;
                if (w > labelWidth) labelWidth = w;
            }
            labelWidth = Math.Min(labelWidth + 8, 420);

            int gridWidth = labelWidth + 10 + inputWidth;
            int contentHeight = 0;
            foreach (RowSpec r in rows) contentHeight += r.IsSection ? sectionHeight : rowHeight;

            int bottomHeight = Math.Max(this.Font.Height + 24, 44);
            int scrollbarAllowance = SystemInformation.VerticalScrollBarWidth + 6;
            // AntdUI Modal 弹窗尺寸为控件尺寸加 48 宽 62 高，横向另留余量
            int modalSideInset = 24;

            // 高度预留 96px 防超出屏幕工作区，超出由容器滚动
            int wantedHeight = contentHeight + pad * 2 + bottomHeight + 8;
            int maxHeight = Math.Max(400, Screen.PrimaryScreen.WorkingArea.Height - 96);
            int finalHeight = Math.Min(wantedHeight, maxHeight);

            int finalWidth = gridWidth + pad * 2 + scrollbarAllowance + modalSideInset;
            this.Size = new Size(finalWidth, finalHeight);
            this.MinimumSize = new Size(finalWidth, Math.Min(420, finalHeight));

            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = bottomHeight, Padding = new Padding(pad, 4, pad, 4) };
            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            int btnWidth = Math.Max(88, this.Font.Height * 7);
            Button btnOk = new Button
            {
                Text = "确定",
                Width = btnWidth,
                Height = Math.Max(this.Font.Height + 16, 30),
                BackColor = Color.FromArgb(22, 119, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            Button btnCancel = new Button
            {
                Text = "取消",
                Width = btnWidth,
                Height = Math.Max(this.Font.Height + 16, 30),
                Margin = new Padding(0)
            };
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);
            bottom.Controls.Add(buttons);

            // 滚动宿主：内容超高时竖滚动，全部行可达。
            // 实测行数：通用轴 19 行 / TA 31 行
            //   通用 11 + TA 特有（4 分区 + 13 字段 = 17）+ 软限位 3 = 31；通用轴 11 + 回零 5 + 软限位 3 = 19。
            // 像素实测（Microsoft YaHei UI 9pt → Font.Height=16）：rowHeight=30、sectionHeight=36、pad=14、bottom=44；
            //   TA contentHeight = 27*30 + 4*36 = 954，wantedHeight = 954+28+44+8 = 1034。
            //   wantedHeight > maxHeight 时靠 AutoScroll 整表滚动（不裁切）；工作区 ≥ 1100px 时全显不滚动。
            Panel host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(pad)
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = rows.Count,
                Height = contentHeight,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int index = 0;
            foreach (RowSpec r in rows)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, r.IsSection ? sectionHeight : rowHeight));
                if (r.IsSection)
                {
                    Label title = new Label
                    {
                        Text = r.Caption,
                        Dock = DockStyle.Fill,
                        AutoSize = false,
                        AutoEllipsis = true,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Font = boldFont,
                        Margin = new Padding(0, 4, 0, 0)
                    };
                    grid.Controls.Add(title, 0, index);
                    grid.SetColumnSpan(title, 2);
                }
                else
                {
                    Label lbl = new Label
                    {
                        Text = r.Caption,
                        Dock = DockStyle.Fill,
                        AutoSize = false,
                        AutoEllipsis = true,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Margin = new Padding(0, 0, 6, 0)
                    };
                    // 输入框放入单元格并垂直居中，否则基线错开
                    Control editor = r.Editor;
                    Panel cell = new Panel
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(0, 2, 0, 2),
                        BackColor = Color.Transparent
                    };
                    editor.Anchor = editor is CheckBox
                        ? AnchorStyles.Left
                        : (AnchorStyles.Left | AnchorStyles.Right);
                    editor.Margin = new Padding(0);
                    cell.Controls.Add(editor);
                    cell.Resize += delegate
                    {
                        if (!(editor is CheckBox)) editor.Width = Math.Max(1, cell.ClientSize.Width);
                        editor.Top = Math.Max(0, (cell.ClientSize.Height - editor.Height) / 2);
                    };
                    grid.Controls.Add(lbl, 0, index);
                    grid.Controls.Add(cell, 1, index);
                }
                index++;
            }

            host.Controls.Add(grid);
            this.Controls.Add(host);
            this.Controls.Add(bottom);
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (!Validate(out string error))
            {
                AntdUI.Message.warn(this.FindForm(), error);
                return;
            }
            // 必须在控件存活期内写回配置：弹窗关闭后内容控件会被 AntdUI 销毁
            Commit();
            Form host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            Form host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        private void LoadValues()
        {
            _txtName.Text = _config.AxisName;
            _txtNodeId.Text = _config.NodeId.ToString(CultureInfo.InvariantCulture);
            _txtSlotNo.Text = _config.SlotNo.ToString(CultureInfo.InvariantCulture);
            _chkEnabled.Checked = _config.Enabled;
            _txtEquiv.Text = _config.Equiv.ToString(CultureInfo.InvariantCulture);
            _txtStrVel.Text = _config.StrVel.ToString(CultureInfo.InvariantCulture);
            _txtConstVel.Text = _config.ConstVel.ToString(CultureInfo.InvariantCulture);
            _txtEndVel.Text = _config.EndVel.ToString(CultureInfo.InvariantCulture);
            _txtAccTime.Text = _config.AccTime.ToString(CultureInfo.InvariantCulture);
            _txtDecTime.Text = _config.DecTime.ToString(CultureInfo.InvariantCulture);
            _txtSTime.Text = _config.STime.ToString(CultureInfo.InvariantCulture);
            _txtHomeOffset.Text = _config.HomeOffset.ToString(CultureInfo.InvariantCulture);
            _txtPositiveLimit.Text = _config.PositiveLimit.ToString(CultureInfo.InvariantCulture);
            _txtNegativeLimit.Text = _config.NegativeLimit.ToString(CultureInfo.InvariantCulture);

            if (_taHome == null)
            {
                _txtHomeMode.Text = _config.HomeMode.ToString(CultureInfo.InvariantCulture);
                _txtHomeFirstVel.Text = _config.HomeFirstVel.ToString(CultureInfo.InvariantCulture);
                _txtHomeSecondVel.Text = _config.HomeSecondVel.ToString(CultureInfo.InvariantCulture);
                _txtHomeAcc.Text = _config.HomeAcc.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                // 特有区以 ROOT/TA_HOME 为唯一真相源（AXIS 的 HOME_* 仅为派生镜像）
                _txtHomeMode.Text = _taHome.HomeMode.ToString(CultureInfo.InvariantCulture);
                _txtHomeFirstVel.Text = _taHome.HomeFirstVel.ToString(CultureInfo.InvariantCulture);
                _txtHomeSecondVel.Text = _taHome.HomeSecondVel.ToString(CultureInfo.InvariantCulture);
                _txtHomeAcc.Text = _taHome.HomeAcc.ToString(CultureInfo.InvariantCulture);
                _txtForce100Pct.Text = _taHome.Force100PctN.ToString(CultureInfo.InvariantCulture);
                _txtTorqueSlope.Text = _config.TorqueSlope.ToString(CultureInfo.InvariantCulture);
                _txtTorqueLimit.Text = _taHome.TorqueLimitN.ToString(CultureInfo.InvariantCulture);
                _cboTorqueDirection.SelectedIndex = _taHome.TorqueDirection < 0 ? 1 : 0;
            }
        }

        private static double ParseDouble(string s, double def)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : def;
        }

        private static ushort ParseUShort(string s, ushort def)
        {
            return ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort v) ? v : def;
        }

        /// <summary>解析有符号回零模式（负值为台达扭矩回零模式）</summary>
        /// <param name="def">默认值。</param>
        /// <returns>结果码。</returns>
        private static short ParseShort(string s, short def)
        {
            return short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out short v) ? v : def;
        }

        private static int ParseInt(string s, int def)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;
        }

        private static bool TryNumber(TextBox box, out double value)
        {
            return double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>校验当前输入</summary>
        /// <remarks>仅轴特有区启用；通用轴路径恒通过，保持 Y 轴原行为</remarks>
        /// <param name="error">首条中文错误提示</param>
        /// <returns>true = 可保存；false = 拦截且未写入任何配置</returns>
        public bool Validate(out string error)
        {
            error = null;
            if (_taHome == null) return true;

            if (!ushort.TryParse(_txtNodeId.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort _))
                { error = "站点号 NodeId 必须为 0~65535 的整数"; return false; }
            if (!ushort.TryParse(_txtSlotNo.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort _))
                { error = "槽号 SlotNo 必须为 0~65535 的整数"; return false; }
            if (!TryNumber(_txtEquiv, out double equiv) || equiv <= 0.0)
                { error = "脉冲当量 Equiv 必须大于 0"; return false; }
            if (!TryNumber(_txtStrVel, out double strVel) || strVel <= 0.0)
                { error = "起始速度 StrVel 必须大于 0"; return false; }
            if (!TryNumber(_txtConstVel, out double constVel) || constVel <= 0.0)
                { error = "匀速 ConstVel 必须大于 0"; return false; }
            if (!TryNumber(_txtEndVel, out double endVel) || endVel < 0.0)
                { error = "末速度 EndVel 必须 ≥ 0"; return false; }
            if (!TryNumber(_txtAccTime, out double accTime) || accTime <= 0.0)
                { error = "加速时间 AccTime 必须大于 0 秒"; return false; }
            if (!TryNumber(_txtDecTime, out double decTime) || decTime <= 0.0)
                { error = "减速时间 DecTime 必须大于 0 秒"; return false; }
            if (!TryNumber(_txtSTime, out double sTime) || sTime < 0.0)
                { error = "S 曲线时间 STime 必须 ≥ 0 秒"; return false; }
            if (!short.TryParse(_txtHomeMode.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short mode)
                || mode < -4 || mode > 35)
                { error = "回零模式须在 -4~35（-3 = 扭矩回零）"; return false; }
            if (!TryNumber(_txtHomeOffset, out double _))
                { error = "原点偏移必须为数字"; return false; }
            if (!TryNumber(_txtHomeFirstVel, out double firstVel) || firstVel <= 0.0)
                { error = "寻找速度必须大于 0 rpm"; return false; }
            if (!TryNumber(_txtHomeSecondVel, out double secondVel) || secondVel <= 0.0)
                { error = "爬行速度必须大于 0 rpm"; return false; }
            if (!TryNumber(_txtHomeAcc, out double homeAcc) || homeAcc <= 0.0)
                { error = "回零加减速必须大于 0 毫秒"; return false; }
            if (!TryNumber(_txtForce100Pct, out double force100) || !(force100 > 0.0))
                { error = "100% 力量基准 TA_FORCE_100PCT_N 必须大于 0 N（不可留空或填 0）"; return false; }
            if (!int.TryParse(_txtTorqueSlope.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int torqueSlope)
                || torqueSlope < 1 || torqueSlope > 65535)
                { error = "扭矩爬升速率 TORQUE_SLOPE 须为 1~65535 的整数（0.1%/s，0 会被驱动器判参数非法）"; return false; }
            // 扭矩上限：位置模式推动阻抗与 CST 最大出力共用；上限需 ≥ 下压力目标，否则保压被钳住打滑
            if (!TryNumber(_txtTorqueLimit, out double torqueLimit) || !(torqueLimit > 0.0))
                { error = "扭矩上限 TA_TORQUE_LIMIT_N 必须大于 0 N（缺省 = 100% 力量基准，即不降额）"; return false; }
            if (torqueLimit > force100)
                { error = string.Format("扭矩上限 {0:F2}N 不得超过 100% 力量基准 {1:F2}N（驱动器上限 1000‰）", torqueLimit, force100); return false; }
            WeldAlgorithmsConfig taCfg = WeldParamManager.Instance.AlgorithmsConfig;
            double taForceN = taCfg == null ? 0.0 : taCfg.TaForceN;
            if (taForceN > 0.0 && torqueLimit < taForceN)
                { error = string.Format("扭矩上限 {0:F2}N 不得低于当前下压力目标 TA_FORCE_N {1:F2}N（否则目标全程被钳制且位置保持/Break 保压会打滑）", torqueLimit, taForceN); return false; }
            if (_cboTorqueDirection.SelectedIndex < 0)
                { error = "请选择扭矩方向（+ 正向出力 / - 反向出力）"; return false; }
            if (!TryNumber(_txtPositiveLimit, out double positiveLimit) || positiveLimit < 0.0)
                { error = "正向软限位必须 ≥ 0（0 = 不启用）"; return false; }
            if (!TryNumber(_txtNegativeLimit, out double negativeLimit) || negativeLimit > 0.0)
                { error = "负向软限位必须 ≤ 0（0 = 不启用）"; return false; }

            return true;
        }

        /// <summary>把界面数值写回配置实例，解析失败保留原值</summary>
        /// <remarks>TA 轴通用区写 AXIS，回零四参加 P1 两参写 TA_HOME。
        /// TA_HOME 是唯一真相源。
        /// ⛔ 不回写 AXIS 的 HOME_*，避免字段两处各存。
        /// 镜像由 SaveConfig 派生。
        /// ⛔ 只能在界面控件存活期内调用（由「确定」按钮触发），重复调用幂等。</remarks>
        public void Commit()
        {
            if (_committed) return;
            _committed = true;

            _config.NodeId = ParseUShort(_txtNodeId.Text, _config.NodeId);
            _config.SlotNo = ParseUShort(_txtSlotNo.Text, _config.SlotNo);
            _config.Enabled = _chkEnabled.Checked;
            _config.Equiv = ParseDouble(_txtEquiv.Text, _config.Equiv);
            _config.StrVel = ParseDouble(_txtStrVel.Text, _config.StrVel);
            _config.ConstVel = ParseDouble(_txtConstVel.Text, _config.ConstVel);
            _config.EndVel = ParseDouble(_txtEndVel.Text, _config.EndVel);
            _config.AccTime = ParseDouble(_txtAccTime.Text, _config.AccTime);
            _config.DecTime = ParseDouble(_txtDecTime.Text, _config.DecTime);
            _config.STime = ParseDouble(_txtSTime.Text, _config.STime);
            _config.HomeOffset = ParseDouble(_txtHomeOffset.Text, _config.HomeOffset);
            _config.PositiveLimit = ParseDouble(_txtPositiveLimit.Text, _config.PositiveLimit);
            _config.NegativeLimit = ParseDouble(_txtNegativeLimit.Text, _config.NegativeLimit);

            if (_taHome == null)
            {
                _config.HomeMode = ParseShort(_txtHomeMode.Text, _config.HomeMode);
                _config.HomeFirstVel = ParseDouble(_txtHomeFirstVel.Text, _config.HomeFirstVel);
                _config.HomeSecondVel = ParseDouble(_txtHomeSecondVel.Text, _config.HomeSecondVel);
                _config.HomeAcc = ParseDouble(_txtHomeAcc.Text, _config.HomeAcc);
            }
            else
            {
                _taHome.HomeMode = ParseShort(_txtHomeMode.Text, _taHome.HomeMode);
                _taHome.HomeFirstVel = ParseDouble(_txtHomeFirstVel.Text, _taHome.HomeFirstVel);
                _taHome.HomeSecondVel = ParseDouble(_txtHomeSecondVel.Text, _taHome.HomeSecondVel);
                _taHome.HomeAcc = ParseDouble(_txtHomeAcc.Text, _taHome.HomeAcc);
                _taHome.Force100PctN = ParseDouble(_txtForce100Pct.Text, _taHome.Force100PctN);
                _config.TorqueSlope = (uint)ParseInt(_txtTorqueSlope.Text, (int)_config.TorqueSlope);
                _taHome.TorqueLimitN = ParseDouble(_txtTorqueLimit.Text, _taHome.TorqueLimitN);
                _taHome.TorqueDirection = _cboTorqueDirection.SelectedIndex == 1 ? -1 : 1;
            }
        }
    }
}
