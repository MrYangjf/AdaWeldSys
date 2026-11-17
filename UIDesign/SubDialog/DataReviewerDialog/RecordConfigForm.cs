using AdaWeldSystem.ProductFileControl;
using AntdUI;
using System;
using System.IO;
using System.Windows.Forms;


namespace AdaWeldSystem.UIDesign.SubDialog.DataReviewerDialog
{
    /// <summary>记录配置：保存路径/pDat 混淆/保留天数</summary>
    public partial class RecordConfigForm : UserControl
    {
        AntdUI.FolderBrowserDialog folderDialog = new AntdUI.FolderBrowserDialog();

        // 注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        public RecordConfigForm() : this(null) { }

        public RecordConfigForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            LoadConfig();
        }

        /// <summary>从INI加载配置到界面</summary>
        private void LoadConfig()
        {
            txtRecordPath.Text = ProductFileRecordManager.Instance.RecordDirectory;
            chkUseObfuscation.Checked = ProductFileRecordManager.Instance.UseObfuscation;
            chkRecordEveryFrame.Checked = ProductFileRecordManager.Instance.RecordEveryFrame;
            txtRetention.Text = ProductFileRecordManager.Instance.RetentionDays.ToString();

            string fallback = ProductFileRecordManager.Instance.PathFallbackMessage;
            if (!string.IsNullOrEmpty(fallback))
            {
                AntdUI.Message.warn(this.Window, fallback);
            }
        }

        /// <summary>保存配置到INI并更新管理器</summary>
        private void SaveConfig()
        {
            if (!int.TryParse(txtRetention.Text.Trim(), out int retainDays) || retainDays < 0)
            {
                AntdUI.Message.error(this.Window, "记录保留天数须为不小于0的整数（0=永久保留）！");
                return;
            }

            ProductFileRecordManager.Instance.ConfigFile.WriteString("Paths", "RecordDirectory", txtRecordPath.Text);
            ProductFileRecordManager.Instance.ConfigFile.WriteBool("Settings", "UseObfuscation", chkUseObfuscation.Checked);
            ProductFileRecordManager.Instance.ConfigFile.WriteBool("Settings", "RecordEveryFrame", chkRecordEveryFrame.Checked);
            ProductFileRecordManager.Instance.ConfigFile.WriteInt("Settings", "RetentionDays", retainDays);

            ProductFileRecordManager.Instance.RecordDirectory = txtRecordPath.Text;
            ProductFileRecordManager.Instance.UseObfuscation = chkUseObfuscation.Checked;
            ProductFileRecordManager.Instance.RecordEveryFrame = chkRecordEveryFrame.Checked;
            ProductFileRecordManager.Instance.RetentionDays = retainDays;

            ProductFileRecordManager.Instance.ConfigFile.SaveToFile();

            // 保存后立即按新保留天数清理过期记录
            ProductFileRecordManager.Instance.CleanupExpiredRecords();

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

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtRecordPath.Text))
                {
                    AntdUI.Message.error(this.Window, "记录文件路径不能为空！");
                    return;
                }

                if (!Directory.Exists(txtRecordPath.Text))
                {
                    Directory.CreateDirectory(txtRecordPath.Text);
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
