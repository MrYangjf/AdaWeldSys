using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    /// <summary>初始化等待面板：作为 Modal 内容承载，初始化结束事件到达后经 DialogResult 关闭所在 Modal。</summary>
    public class InitWaitingPanel : UserControl
    {
        #region 私有变量

        private readonly Label _lblText;

        #endregion

        #region 构造函数

        /// <summary>构造初始化等待面板。</summary>
        public InitWaitingPanel()
        {
            Size = new Size(380, 120);

            _lblText = new Label
            {
                Dock = DockStyle.Fill,
                Text = "正在初始化设备，请稍候…",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            Controls.Add(_lblText);
        }

        #endregion

        #region 公共函数

        /// <summary>初始化结束后更新提示文本。</summary>
        /// <param name="success">是否全部子设备就绪</param>
        public void SetResult(bool success)
        {
            _lblText.Text = success ? "初始化完成 待复位" : "初始化失败 请查看日志";
        }

        /// <summary>关闭所在 Modal（须在 UI 线程调用）。</summary>
        public void CloseModal()
        {
            var host = FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        #endregion
    }
}
