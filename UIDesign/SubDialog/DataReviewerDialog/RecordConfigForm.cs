using AdaWeldSystem.ProductFileManager;
using AntdUI;
using System;
using System.IO;
using System.Windows.Forms;


namespace AdaWeldSystem.Sub3UI
{
    /// <summary>
    /// 记录配置对话框
    /// 用于设置临时文件和保存文件的保存路径，以及混淆选项
    /// </summary>
    public partial class RecordConfigForm : UserControl
    {
        AntdUI.FolderBrowserDialog folderDialog = new AntdUI.FolderBrowserDialog();

        // 复刻 AntdUI demo SystemSet 模式：注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        public RecordConfigForm() : this(null) { }

        public RecordConfigForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            LoadConfig();
        }

        /// <summary>
        /// 从INI加载配置到界面
        /// </summary>
        private void LoadConfig()
        {
            // 读规范化后的路径（RunRecordManager 已在构造时对空路径回退默认值）
            string recordDir = RunRecordManager.Instance.RecordDirectory;
            string tempDir = RunRecordManager.Instance.TempDirectory;
            bool useObfuscation = RunRecordManager.Instance.UseObfuscation;

            txtRecordPath.Text = recordDir;
            txtTempPath.Text = tempDir;
            chkUseObfuscation.Checked = useObfuscation;

            // 提示路径回退信息（配置路径为空时已回退默认）
            string fallback = RunRecordManager.Instance.PathFallbackMessage;
            if (!string.IsNullOrEmpty(fallback))
            {
                AntdUI.Message.warn(this.Window, fallback);
            }
        }

        /// <summary>
        /// 保存配置到INI并更新管理器
        /// </summary>
        private void SaveConfig()
        {

            RunRecordManager.Instance.ConfigFile.WriteString("Paths", "RecordDirectory", txtRecordPath.Text);
            RunRecordManager.Instance.ConfigFile.WriteString("Paths", "TempDirectory", txtTempPath.Text);
            RunRecordManager.Instance.ConfigFile.WriteBool("Settings", "UseObfuscation", chkUseObfuscation.Checked);

            // 同步更新RunRecordManager
            RunRecordManager.Instance.RecordDirectory = txtRecordPath.Text;
            RunRecordManager.Instance.TempDirectory = txtTempPath.Text;
            RunRecordManager.Instance.UseObfuscation = chkUseObfuscation.Checked;

            RunRecordManager.Instance.ConfigFile.SaveToFile();

            AntdUI.Message.success(this.Window, "配置已保存！");
        }

        private void btnBrowseRecord_Click(object sender, EventArgs e)
        {
            folderDialog.DirectoryPath = txtRecordPath.Text;

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                txtRecordPath.Text = folderDialog.DirectoryPath;
            }

        }

        private void btnBrowseTemp_Click(object sender, EventArgs e)
        {
            folderDialog.DirectoryPath = txtTempPath.Text;

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                txtTempPath.Text = folderDialog.DirectoryPath;
            }

        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // 验证路径有效性
                if (string.IsNullOrWhiteSpace(txtRecordPath.Text))
                {
                    AntdUI.Message.error(this.Window, "记录文件路径不能为空！");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtTempPath.Text))
                {
                    AntdUI.Message.error(this.Window, "临时文件路径不能为空！");
                    return;
                }

                // 确保目录存在
                if (!Directory.Exists(txtRecordPath.Text))
                {
                    Directory.CreateDirectory(txtRecordPath.Text);
                }

                if (!Directory.Exists(txtTempPath.Text))
                {
                    Directory.CreateDirectory(txtTempPath.Text);
                }

                SaveConfig();
                var host = this.FindForm();
                if (host != null) host.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this.Window, $"保存配置失败：{ex.Message}");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

    }
}
