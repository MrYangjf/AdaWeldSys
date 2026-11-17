namespace AdaWeldSystem.UIDesign.SubDialog.ParamContolDialog
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
            this.gridPanel1 = new AntdUI.GridPanel();
            this.flowPanel1 = new AntdUI.FlowPanel();
            this.gridPanel1.SuspendLayout();
            this.flowPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelK
            // 
            this.labelK.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelK.Location = new System.Drawing.Point(403, 3);
            this.labelK.Name = "labelK";
            this.labelK.Size = new System.Drawing.Size(194, 44);
            this.labelK.TabIndex = 1;
            this.labelK.Text = "K:";
            // 
            // LabelJ
            // 
            this.LabelJ.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.LabelJ.Location = new System.Drawing.Point(3, 3);
            this.LabelJ.Name = "LabelJ";
            this.LabelJ.Size = new System.Drawing.Size(194, 44);
            this.LabelJ.TabIndex = 2;
            this.LabelJ.Text = "J:";
            // 
            // labelM
            // 
            this.labelM.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelM.Location = new System.Drawing.Point(3, 53);
            this.labelM.Name = "labelM";
            this.labelM.Size = new System.Drawing.Size(794, 344);
            this.labelM.TabIndex = 3;
            this.labelM.Text = "M:";
            // 
            // btnSave
            // 
            this.btnSave.BackHover = System.Drawing.Color.FromArgb(((int)(((byte)(3)))), ((int)(((byte)(169)))), ((int)(((byte)(244)))));
            this.btnSave.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSave.Ghost = true;
            this.btnSave.Location = new System.Drawing.Point(597, 3);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(194, 29);
            this.btnSave.TabIndex = 5;
            this.btnSave.Text = "保存";
            // 
            // uiDataGridView1
            // 
            this.uiDataGridView1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.uiDataGridView1.Gap = 12;
            this.uiDataGridView1.Location = new System.Drawing.Point(203, 3);
            this.uiDataGridView1.Name = "uiDataGridView1";
            this.uiDataGridView1.Size = new System.Drawing.Size(194, 44);
            this.uiDataGridView1.TabIndex = 10;
            // 
            // gridPanel1
            // 
            this.gridPanel1.Controls.Add(this.flowPanel1);
            this.gridPanel1.Controls.Add(this.uiDataGridView1);
            this.gridPanel1.Controls.Add(this.labelM);
            this.gridPanel1.Controls.Add(this.LabelJ);
            this.gridPanel1.Controls.Add(this.labelK);
            this.gridPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPanel1.Location = new System.Drawing.Point(0, 0);
            this.gridPanel1.Name = "gridPanel1";
            this.gridPanel1.Size = new System.Drawing.Size(800, 450);
            this.gridPanel1.Span = "40:160 160 160 ;100%:fill;40:fill";
            this.gridPanel1.TabIndex = 12;
            this.gridPanel1.Text = "gridPanel1";
            // 
            // flowPanel1
            // 
            this.flowPanel1.Align = AntdUI.TAlignFlow.RightCenter;
            this.flowPanel1.Controls.Add(this.btnSave);
            this.flowPanel1.Location = new System.Drawing.Point(3, 403);
            this.flowPanel1.Name = "flowPanel1";
            this.flowPanel1.Size = new System.Drawing.Size(794, 44);
            this.flowPanel1.TabIndex = 11;
            this.flowPanel1.Text = "flowPanel1";
            // 
            // AutoParamForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.gridPanel1);
            this.Name = "AutoParamForm";
            this.Size = new System.Drawing.Size(800, 450);
            this.gridPanel1.ResumeLayout(false);
            this.flowPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Label labelK;
        private AntdUI.Label LabelJ;
        private AntdUI.Label labelM;
        private AntdUI.Button btnSave;
        private AntdUI.Table uiDataGridView1;
        private AntdUI.GridPanel gridPanel1;
        private AntdUI.FlowPanel flowPanel1;
    }
}