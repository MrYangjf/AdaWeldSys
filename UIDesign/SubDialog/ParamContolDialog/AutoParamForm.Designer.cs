namespace AdaWeldSystem.Sub3UI
{
    partial class AutoParamForm
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
            this.labelK = new AntdUI.Label();
            this.LabelJ = new AntdUI.Label();
            this.labelM = new AntdUI.Label();
            this.btnSave = new AntdUI.Button();
            this.uiDataGridView1 = new AntdUI.Table();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelK
            // 
            this.labelK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelK.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelK.Location = new System.Drawing.Point(403, 3);
            this.labelK.Name = "labelK";
            this.labelK.Size = new System.Drawing.Size(194, 29);
            this.labelK.TabIndex = 1;
            this.labelK.Text = "K:";
            // 
            // LabelJ
            // 
            this.LabelJ.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LabelJ.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.LabelJ.Location = new System.Drawing.Point(203, 3);
            this.LabelJ.Name = "LabelJ";
            this.LabelJ.Size = new System.Drawing.Size(194, 29);
            this.LabelJ.TabIndex = 2;
            this.LabelJ.Text = "J:";
            // 
            // labelM
            // 
            this.labelM.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelM.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelM.Location = new System.Drawing.Point(3, 3);
            this.labelM.Name = "labelM";
            this.labelM.Size = new System.Drawing.Size(194, 29);
            this.labelM.TabIndex = 3;
            this.labelM.Text = "M:";
            // 
            // btnSave
            // 
            this.btnSave.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSave.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSave.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSave.Ghost = true;
            this.btnSave.Location = new System.Drawing.Point(603, 3);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(194, 29);
            this.btnSave.TabIndex = 5;
            this.btnSave.Text = "保存";
            // 
            // uiDataGridView1
            // 
            this.uiDataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiDataGridView1.Font = new System.Drawing.Font("宋体", 12F);
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Location = new System.Drawing.Point(0, 0);
            this.uiDataGridView1.Name = "uiDataGridView1";
            this.uiDataGridView1.Size = new System.Drawing.Size(800, 450);
            this.uiDataGridView1.TabIndex = 10;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Controls.Add(this.btnSave, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelK, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelM, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.LabelJ, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(800, 35);
            this.tableLayoutPanel1.TabIndex = 11;
            // 
            // AutoParamForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.uiDataGridView1);
            this.Name = "AutoParamForm";
            this.Size = new System.Drawing.Size(800, 450);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Label labelK;
        private AntdUI.Label LabelJ;
        private AntdUI.Label labelM;
        private AntdUI.Button btnSave;
private AntdUI.Table uiDataGridView1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}