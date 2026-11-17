using AntdUI;
using System;
using System.Windows.Forms;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.UIDesign.SubDialog.UserManagerDialog
{
    /// <summary>登录窗体：校验账号并切换权限等级，以 AntdUI.Modal 承载。</summary>
    public partial class LoginForm : System.Windows.Forms.UserControl
    {
        private readonly Window Window;

        public bool IsLogin { get; set; }

        // 注入父 Window，内部方法用 this.Window 作 owner
        public LoginForm() : this(null) { }

        public LoginForm(Window _window)
        {
            Window = _window;
            InitializeComponent();

            btnLogin.Click += FormLogin_ButtonLoginClick;
            btnLogout.Click += BtnLogout_Click;
            ShowCorButton();
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            if (AntdUI.Modal.open(new Modal.Config(this.Window, "确认", "是否注销权限？注销后降为操作员，关键参数页将不可见。", TType.Info)) == DialogResult.OK)
            {
                GlobalCommData.SetOperateLevel(OperateLevel.Operator);
                AntdUI.Message.success(this.Window, "已注销，当前权限：操作员");
            }
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        void ShowCorButton()
        {
            // 用户管理入口仅管理员可见
            btnUserManager.Visible = PermissionLevel.IsAdmin(GlobalCommData.mOperateLevel);
        }

        private void FormLogin_ButtonLoginClick(object sender, EventArgs e)
        {
            try
            {
                if (GlobalCommData.mAuthHelper.ValidateUser(txtUserName.Text, txtPassword.Text))
                {
                    IsLogin = true;
                    GlobalCommData.SetOperateLevel(GlobalCommData.mAuthHelper.GetUserOperate(txtUserName.Text));
                    AntdUI.Message.success(this.Window, "登陆成功！当前权限：" + PermissionLevel.GetDisplayName(GlobalCommData.mOperateLevel));
                    var host = this.FindForm();
                    if (host != null) host.DialogResult = DialogResult.OK;
                }
                else
                {
                    AntdUI.Message.error(this.Window, "用户名或者密码错误。");
                }
            }
            catch
            {
                AntdUI.Message.error(this.Window, "用户名或者密码错误。");
            }
            txtPassword.Text = "";
            ShowCorButton();
        }

        private void btnUserManager_Click(object sender, EventArgs e)
        {
            using (UserEditForm editPassword = new UserEditForm(this.Window))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.Window, "用户管理", editPassword, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }
    }
}
