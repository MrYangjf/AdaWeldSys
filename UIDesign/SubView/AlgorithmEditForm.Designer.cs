namespace AdaWeldSystem.Sub3UI
{
    partial class AlgorithmEditForm
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
            AntdUI.Tabs.StyleLine styleLine1 = new AntdUI.Tabs.StyleLine();
            this.tabsAlgo = new AntdUI.Tabs();
            this.SuspendLayout();
            // 
            // tabsAlgo
            // 
            this.tabsAlgo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsAlgo.Location = new System.Drawing.Point(0, 0);
            this.tabsAlgo.Name = "tabsAlgo";
            this.tabsAlgo.Size = new System.Drawing.Size(930, 553);
            this.tabsAlgo.Style = styleLine1;
            this.tabsAlgo.TabIndex = 0;
            // 
            // AlgorithmEditForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.tabsAlgo);
            this.Name = "AlgorithmEditForm";
            this.Size = new System.Drawing.Size(930, 553);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.Tabs tabsAlgo;
    }
}
