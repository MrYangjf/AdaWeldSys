namespace AdaWeldSystem.Sub2UI
{
    partial class UserEdit
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
            this.uiDataGridView1 = new AntdUI.Table();
            this.uiGroupBox1 = new System.Windows.Forms.GroupBox();
            this.btnRefresh = new AntdUI.Button();
            this.btnSave = new AntdUI.Button();
            this.uiComboBoxLevel = new AntdUI.Select();
            this.uiSymbolLabel4 = new AntdUI.Label();
            this.uiTextBoxConfirmPwd = new AntdUI.Input();
            this.uiSymbolLabel3 = new AntdUI.Label();
            this.uiTextBoxPassword = new AntdUI.Input();
            this.uiSymbolLabel2 = new AntdUI.Label();
            this.uiTextBoxUserName = new AntdUI.Input();
            this.uiSymbolLabel1 = new AntdUI.Label();
            this.uiGroupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiDataGridView1
            // 
            this.uiDataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uiDataGridView1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Location = new System.Drawing.Point(13, 3);
            this.uiDataGridView1.Name = "uiDataGridView1";
            this.uiDataGridView1.Size = new System.Drawing.Size(373, 313);
            this.uiDataGridView1.TabIndex = 0;
            // 
            // uiGroupBox1
            // 
            this.uiGroupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uiGroupBox1.Controls.Add(this.btnRefresh);
            this.uiGroupBox1.Controls.Add(this.btnSave);
            this.uiGroupBox1.Controls.Add(this.uiComboBoxLevel);
            this.uiGroupBox1.Controls.Add(this.uiSymbolLabel4);
            this.uiGroupBox1.Controls.Add(this.uiTextBoxConfirmPwd);
            this.uiGroupBox1.Controls.Add(this.uiSymbolLabel3);
            this.uiGroupBox1.Controls.Add(this.uiTextBoxPassword);
            this.uiGroupBox1.Controls.Add(this.uiSymbolLabel2);
            this.uiGroupBox1.Controls.Add(this.uiTextBoxUserName);
            this.uiGroupBox1.Controls.Add(this.uiSymbolLabel1);
            this.uiGroupBox1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiGroupBox1.Location = new System.Drawing.Point(392, 5);
            this.uiGroupBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiGroupBox1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiGroupBox1.Name = "uiGroupBox1";
            this.uiGroupBox1.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.uiGroupBox1.Size = new System.Drawing.Size(394, 309);
            this.uiGroupBox1.TabIndex = 1;
            this.uiGroupBox1.TabStop = false;
            this.uiGroupBox1.Text = "用户信息编辑";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRefresh.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRefresh.Location = new System.Drawing.Point(291, 226);
            this.btnRefresh.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(100, 35);
            this.btnRefresh.TabIndex = 9;
            this.btnRefresh.Text = "刷新";
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnSave
            // 
            this.btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSave.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSave.Location = new System.Drawing.Point(13, 226);
            this.btnSave.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 35);
            this.btnSave.TabIndex = 8;
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // uiComboBoxLevel
            // 
            this.uiComboBoxLevel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiComboBoxLevel.Items.AddRange(new object[] {
            "管理员",
            "工程师",
            "操作员"});
            this.uiComboBoxLevel.Location = new System.Drawing.Point(139, 185);
            this.uiComboBoxLevel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiComboBoxLevel.MinimumSize = new System.Drawing.Size(63, 0);
            this.uiComboBoxLevel.Name = "uiComboBoxLevel";
            this.uiComboBoxLevel.Size = new System.Drawing.Size(230, 29);
            this.uiComboBoxLevel.TabIndex = 6;
            // 
            // uiSymbolLabel4
            // 
            this.uiSymbolLabel4.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel4.Location = new System.Drawing.Point(13, 185);
            this.uiSymbolLabel4.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel4.Name = "uiSymbolLabel4";
            this.uiSymbolLabel4.Size = new System.Drawing.Size(120, 35);
            this.uiSymbolLabel4.TabIndex = 7;
            this.uiSymbolLabel4.Text = "用户等级";
            // 
            // uiTextBoxConfirmPwd
            // 
            this.uiTextBoxConfirmPwd.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.uiTextBoxConfirmPwd.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiTextBoxConfirmPwd.Location = new System.Drawing.Point(139, 140);
            this.uiTextBoxConfirmPwd.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiTextBoxConfirmPwd.MinimumSize = new System.Drawing.Size(1, 16);
            this.uiTextBoxConfirmPwd.Name = "uiTextBoxConfirmPwd";
            this.uiTextBoxConfirmPwd.PasswordChar = '*';
            this.uiTextBoxConfirmPwd.PlaceholderText = "请再次输入新密码";
            this.uiTextBoxConfirmPwd.Size = new System.Drawing.Size(230, 29);
            this.uiTextBoxConfirmPwd.TabIndex = 4;
            // 
            // uiSymbolLabel3
            // 
            this.uiSymbolLabel3.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel3.Location = new System.Drawing.Point(13, 140);
            this.uiSymbolLabel3.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel3.Name = "uiSymbolLabel3";
            this.uiSymbolLabel3.Size = new System.Drawing.Size(120, 35);
            this.uiSymbolLabel3.TabIndex = 5;
            this.uiSymbolLabel3.Text = "确认密码";
            // 
            // uiTextBoxPassword
            // 
            this.uiTextBoxPassword.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.uiTextBoxPassword.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiTextBoxPassword.Location = new System.Drawing.Point(139, 95);
            this.uiTextBoxPassword.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiTextBoxPassword.MinimumSize = new System.Drawing.Size(1, 16);
            this.uiTextBoxPassword.Name = "uiTextBoxPassword";
            this.uiTextBoxPassword.PasswordChar = '*';
            this.uiTextBoxPassword.PlaceholderText = "请输入新密码";
            this.uiTextBoxPassword.Size = new System.Drawing.Size(230, 29);
            this.uiTextBoxPassword.TabIndex = 2;
            // 
            // uiSymbolLabel2
            // 
            this.uiSymbolLabel2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel2.Location = new System.Drawing.Point(13, 95);
            this.uiSymbolLabel2.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel2.Name = "uiSymbolLabel2";
            this.uiSymbolLabel2.Size = new System.Drawing.Size(120, 35);
            this.uiSymbolLabel2.TabIndex = 3;
            this.uiSymbolLabel2.Text = "新密码";
            // 
            // uiTextBoxUserName
            // 
            this.uiTextBoxUserName.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.uiTextBoxUserName.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiTextBoxUserName.Location = new System.Drawing.Point(139, 50);
            this.uiTextBoxUserName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiTextBoxUserName.MinimumSize = new System.Drawing.Size(1, 16);
            this.uiTextBoxUserName.Name = "uiTextBoxUserName";
            this.uiTextBoxUserName.ReadOnly = true;
            this.uiTextBoxUserName.Size = new System.Drawing.Size(230, 29);
            this.uiTextBoxUserName.TabIndex = 0;
            // 
            // uiSymbolLabel1
            // 
            this.uiSymbolLabel1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel1.Location = new System.Drawing.Point(13, 50);
            this.uiSymbolLabel1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel1.Name = "uiSymbolLabel1";
            this.uiSymbolLabel1.Size = new System.Drawing.Size(120, 35);
            this.uiSymbolLabel1.TabIndex = 1;
            this.uiSymbolLabel1.Text = "用户名";
            // 
            // UserEdit
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiGroupBox1);
            this.Controls.Add(this.uiDataGridView1);
            this.Name = "UserEdit";
            this.Size = new System.Drawing.Size(799, 319);
            this.uiGroupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Table uiDataGridView1;
        private System.Windows.Forms.GroupBox uiGroupBox1;
        private AntdUI.Label uiSymbolLabel1;
        private AntdUI.Input uiTextBoxUserName;
        private AntdUI.Label uiSymbolLabel2;
        private AntdUI.Input uiTextBoxPassword;
        private AntdUI.Label uiSymbolLabel3;
        private AntdUI.Input uiTextBoxConfirmPwd;
        private AntdUI.Label uiSymbolLabel4;
        private AntdUI.Select uiComboBoxLevel;
        private AntdUI.Button btnSave;
        private AntdUI.Button btnRefresh;
    }
}
