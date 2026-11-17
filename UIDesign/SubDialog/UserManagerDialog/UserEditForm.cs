using AntdUI;
using System;
using System.Data;
using System.Windows.Forms;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.SQLiteDBControl.Helpers;

namespace AdaWeldSystem.UIDesign.SubDialog.UserManagerDialog
{
    /// <summary>用户管理：改各级用户密码与等级</summary>
    public partial class UserEditForm : UserControl
    {
        private readonly AuthHelper _authHelper;
        // 与 uiDataGridView1.Binding() 绑定的强类型列表
        private AntdUI.AntList<UserRow> _rows;

        // 注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        public UserEditForm() : this(null) { }

        public UserEditForm(Window _window)
        {
            Window = _window;
            InitializeComponent();
            InitTableColumns();
            _rows = new AntdUI.AntList<UserRow>();
            uiDataGridView1.Binding(_rows);
            _authHelper = GlobalCommData.mAuthHelper;
            LoadUserList();
        }

        /// <summary>初始化表格列：Columns 须在代码后置设置，不可在 Designer 内赋值。</summary>
        private void InitTableColumns()
        {
            uiDataGridView1.Columns = new AntdUI.ColumnCollection
            {
                // Column(key, title)：key 必须等于 UserRow 的属性名，title 才是表头显示文字
                new AntdUI.Column("UserName", "用户名") { Width = "150" },
                new AntdUI.Column("UserLevel", "用户等级") { Width = "100" }
            };
        }

        /// <summary>加载用户列表到 DataGridView</summary>
        private void LoadUserList()
        {
            try
            {
                DataTable userTable = _authHelper.GetAllUsers();
                _rows.Clear();
                foreach (DataRow r in userTable.Rows)
                {
                    _rows.Add(new UserRow
                    {
                        UserName = r["UserName"]?.ToString(),
                        UserLevel = r["UserLevel"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this.Window, $"加载用户列表失败：{ex.Message}");
            }
        }

        /// <summary>保存修改（密码和等级）</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            string userName = uiTextBoxUserName.Text.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                AntdUI.Message.warn(this.Window, "请先选择需要修改的用户");
                return;
            }

            if (!_authHelper.IsUserExist(userName))
            {
                AntdUI.Message.error(this.Window, "用户不存在");
                return;
            }

            // 修改密码
            string password = uiTextBoxPassword.Text;
            string confirmPwd = uiTextBoxConfirmPwd.Text;

            if (!string.IsNullOrEmpty(password))
            {
                if (password != confirmPwd)
                {
                    AntdUI.Message.error(this.Window, "两次输入的密码不一致");
                    return;
                }

                if (!_authHelper.UpdatePassword(userName, password))
                {
                    AntdUI.Message.error(this.Window, "密码修改失败");
                    return;
                }
            }

            // 修改用户等级
            if (!_authHelper.UpdateUserLevel(userName, uiComboBoxLevel.SelectedIndex.ToString()))
            {
                AntdUI.Message.error(this.Window, "用户等级修改失败");
                return;
            }

            AntdUI.Message.success(this.Window, "修改成功");
            LoadUserList();
        }

        /// <summary>刷新用户列表</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadUserList();
        }

        // AntdUI.Table 数据行模型
        public class UserRow
        {
            public string UserName { get; set; }
            public string UserLevel { get; set; }
        }
    }
}
