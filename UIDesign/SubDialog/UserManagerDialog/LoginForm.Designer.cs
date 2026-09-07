namespace AdaWeldSystem.Sub1UI
{
    partial class LoginForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblUserName = new AntdUI.Label();
            this.lblPassword = new AntdUI.Label();
            this.txtPassword = new AntdUI.Input();
            this.btnLogin = new AntdUI.Button();
            this.txtUserName = new AntdUI.Select();
            this.btnLogout = new AntdUI.Button();
            this.btnUserManager = new AntdUI.Button();
            this.SuspendLayout();
            // 
            // lblUserName
            // 
            this.lblUserName.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblUserName.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblUserName.Location = new System.Drawing.Point(10, 68);
            this.lblUserName.Name = "lblUserName";
            this.lblUserName.Size = new System.Drawing.Size(67, 22);
            this.lblUserName.TabIndex = 1;
            this.lblUserName.Text = "用户名：";
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.lblPassword.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblPassword.Location = new System.Drawing.Point(10, 128);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(50, 22);
            this.lblPassword.TabIndex = 3;
            this.lblPassword.Text = "密码：";
            // 
            // txtPassword
            // 
            this.txtPassword.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtPassword.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtPassword.Location = new System.Drawing.Point(83, 118);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.PasswordCopy = true;
            this.txtPassword.PlaceholderText = "密码";
            this.txtPassword.Size = new System.Drawing.Size(249, 43);
            this.txtPassword.TabIndex = 4;
            // 
            // btnLogin
            // 
            this.btnLogin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLogin.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.btnLogin.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnLogin.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnLogin.Ghost = true;
            this.btnLogin.IconSvg = "LoginOutlined";
            this.btnLogin.Location = new System.Drawing.Point(260, 189);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(86, 49);
            this.btnLogin.TabIndex = 5;
            this.btnLogin.Text = "登陆";
            // 
            // txtUserName
            // 
            this.txtUserName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUserName.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtUserName.Items.AddRange(new object[] {
            "Engineer",
            "Admin"});
            this.txtUserName.Location = new System.Drawing.Point(83, 58);
            this.txtUserName.Name = "txtUserName";
            this.txtUserName.Size = new System.Drawing.Size(249, 41);
            this.txtUserName.TabIndex = 12;
            // 
            // btnLogout
            // 
            this.btnLogout.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLogout.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.btnLogout.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnLogout.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnLogout.Ghost = true;
            this.btnLogout.IconSvg = "LogoutOutlined";
            this.btnLogout.Location = new System.Drawing.Point(260, 0);
            this.btnLogout.Name = "btnLogout";
            this.btnLogout.Size = new System.Drawing.Size(86, 49);
            this.btnLogout.TabIndex = 13;
            this.btnLogout.Text = "注销";
            // 
            // btnUserManager
            // 
            this.btnUserManager.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUserManager.AutoSizeMode = AntdUI.TAutoSize.Auto;
            this.btnUserManager.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnUserManager.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnUserManager.Ghost = true;
            this.btnUserManager.IconSvg = "UserAddOutlined";
            this.btnUserManager.Location = new System.Drawing.Point(3, 189);
            this.btnUserManager.Name = "btnUserManager";
            this.btnUserManager.Size = new System.Drawing.Size(119, 49);
            this.btnUserManager.TabIndex = 14;
            this.btnUserManager.Text = "权限管理";
            this.btnUserManager.Click += new System.EventHandler(this.btnUserManager_Click);
            // 
            // FormLogin
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.btnUserManager);
            this.Controls.Add(this.btnLogout);
            this.Controls.Add(this.txtUserName);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.lblUserName);
            this.Name = "FormLogin";
            this.Size = new System.Drawing.Size(349, 241);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private AntdUI.Label lblUserName;
        private AntdUI.Label lblPassword;
        private AntdUI.Input txtPassword;
        private AntdUI.Button btnLogin;
        private AntdUI.Select txtUserName;
        private AntdUI.Button btnLogout;
        private AntdUI.Button btnUserManager;
    }
}
