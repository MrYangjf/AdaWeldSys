using AdaWeldSystem.Comm;
using AdaWeldSystem.Sub2UI;
using AntdUI;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub1UI
{
    /// <summary>
    /// 登录窗体（AntdUI 迁移版）。
    /// ⚠ 与原任务简报不一致：原基类为 SunnyUI 的 UILoginForm 登录窗体基类（含内置用户名/密码框、
    /// 登录/取消按钮及 ButtonLoginClick / ButtonCancelClick / UserName / Password /
    /// IsLogin / ShowSuccessDialog / ShowAskDialog2 / SubText / Title 等专属成员），
    /// 无干净 1:1 映射。故重建为 AntdUI 迁移版；2026-07-30 经 ADR-022 由 AntdUI.Window 改为
    /// System.Windows.Forms.UserControl，以 AntdUI.Modal 承载（取消 PageHeader，标题由 Modal 提供），
    /// 交互逻辑（校验、权限切换）保持不变。
    /// </summary>
    public partial class FormLogin : System.Windows.Forms.UserControl
    {
        public bool IsLogin { get; set; }

        // 复刻 AntdUI demo SystemSet 模式：注入父 Window，内部方法用 this.Window 作 owner
        private readonly Window Window;

        public FormLogin() : this(null) { }

        public FormLogin(Window _window)
        {
            Window = _window;
            InitializeComponent();

            btnLogin.Click += FormLogin_ButtonLoginClick;
            btnLogout.Click += BtnLogout_Click;
            ShowCorButton();
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            bool re = AntdUI.Modal.open(new Modal.Config(this.Window, "确认", "是否注销权限？", TType.Info)) == DialogResult.OK;
            if (re) GlobalCommData.mOperateLevel = OperateLevel.Operator;
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        void ShowCorButton()
        {
            if (GlobalCommData.mOperateLevel == OperateLevel.Admin)
                btnUserManager.Visible = true;
            else
                btnUserManager.Visible = false;
        }

        private void FormLogin_ButtonLoginClick(object sender, EventArgs e)
        {
            try
            {
                if (GlobalCommData.mAuthManager.ValidateUser(txtUserName.Text, txtPassword.Text))
                {
                    IsLogin = true;
                    AntdUI.Message.success(this.Window, "登陆成功！");
                    GlobalCommData.mOperateLevel = GlobalCommData.mAuthManager.GetUserOperate(txtUserName.Text);
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
            using (UserEdit editPassword = new UserEdit(this.Window))
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
