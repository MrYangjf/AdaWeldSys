using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

namespace AdaWeldSystem.Sub3UI
{
    partial class HelixRenderForm
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.uiTableLayoutPanel1 = new AntdUI.GridPanel();
            this.btnAssimpImport = new AntdUI.Button();
            this.btnRenderParam = new AntdUI.Button();
            this.btnClearData = new AntdUI.Button();
            this.btnSurface = new AntdUI.Button();
            this.btnZColor = new AntdUI.Button();
            this.btnTraceFile = new AntdUI.Button();
            this.uiPanel2 = new System.Windows.Forms.Panel();
            this.btnRound = new AntdUI.Button();
            this.btnMove = new AntdUI.Button();
            this.btnRotate = new AntdUI.Button();
            this.elementHost1 = new System.Windows.Forms.Integration.ElementHost();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.uiToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.uiTableLayoutPanel1.SuspendLayout();
            this.uiPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // uiTableLayoutPanel1
            // 
            this.uiTableLayoutPanel1.Controls.Add(this.tableLayoutPanel1);
            this.uiTableLayoutPanel1.Controls.Add(this.uiPanel2);
            this.uiTableLayoutPanel1.Controls.Add(this.elementHost1);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(1054, 570);
            this.uiTableLayoutPanel1.Span = "100% 100;40:100%";
            this.uiTableLayoutPanel1.TabIndex = 1;
            // 
            // btnAssimpImport
            // 
            this.btnAssimpImport.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAssimpImport.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAssimpImport.Ghost = true;
            this.btnAssimpImport.Location = new System.Drawing.Point(363, 3);
            this.btnAssimpImport.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnAssimpImport.Name = "btnAssimpImport";
            this.btnAssimpImport.Size = new System.Drawing.Size(114, 35);
            this.btnAssimpImport.TabIndex = 6;
            this.btnAssimpImport.Text = "Assimp导入";
            this.btnAssimpImport.Click += new System.EventHandler(this.btnAssimpImport_Click);
            // 
            // btnRenderParam
            // 
            this.btnRenderParam.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRenderParam.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRenderParam.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRenderParam.Ghost = true;
            this.btnRenderParam.Location = new System.Drawing.Point(603, 3);
            this.btnRenderParam.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnRenderParam.Name = "btnRenderParam";
            this.btnRenderParam.Size = new System.Drawing.Size(114, 35);
            this.btnRenderParam.TabIndex = 4;
            this.btnRenderParam.Text = "渲染参数设置";
            this.btnRenderParam.Click += new System.EventHandler(this.btnRenderParam_Click);
            // 
            // btnClearData
            // 
            this.btnClearData.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClearData.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClearData.Ghost = true;
            this.btnClearData.Location = new System.Drawing.Point(243, 3);
            this.btnClearData.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnClearData.Name = "btnClearData";
            this.btnClearData.Size = new System.Drawing.Size(114, 35);
            this.btnClearData.TabIndex = 3;
            this.btnClearData.Text = "清除数据";
            this.btnClearData.Click += new System.EventHandler(this.btnClearData_Click);
            // 
            // btnSurface
            // 
            this.btnSurface.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSurface.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSurface.Ghost = true;
            this.btnSurface.Location = new System.Drawing.Point(483, 3);
            this.btnSurface.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSurface.Name = "btnSurface";
            this.btnSurface.Size = new System.Drawing.Size(114, 35);
            this.btnSurface.TabIndex = 2;
            this.btnSurface.Text = "表面建模";
            this.btnSurface.Click += new System.EventHandler(this.btnSurface_Click);
            // 
            // btnZColor
            // 
            this.btnZColor.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnZColor.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnZColor.Ghost = true;
            this.btnZColor.Location = new System.Drawing.Point(123, 3);
            this.btnZColor.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnZColor.Name = "btnZColor";
            this.btnZColor.Size = new System.Drawing.Size(114, 35);
            this.btnZColor.TabIndex = 1;
            this.btnZColor.Text = "Z向着色(开)";
            this.btnZColor.Click += new System.EventHandler(this.btnZColor_Click);
            // 
            // btnTraceFile
            // 
            this.btnTraceFile.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnTraceFile.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnTraceFile.Ghost = true;
            this.btnTraceFile.Location = new System.Drawing.Point(3, 3);
            this.btnTraceFile.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnTraceFile.Name = "btnTraceFile";
            this.btnTraceFile.Size = new System.Drawing.Size(114, 35);
            this.btnTraceFile.TabIndex = 0;
            this.btnTraceFile.Text = "点云文件导入";
            this.btnTraceFile.Click += new System.EventHandler(this.btnTraceFile_Click);
            // 
            // uiPanel2
            // 
            this.uiPanel2.Controls.Add(this.tableLayoutPanel2);
            this.uiPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel2.Location = new System.Drawing.Point(933, 5);
            this.uiPanel2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.uiPanel2.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel2.Name = "uiPanel2";
            this.uiPanel2.Size = new System.Drawing.Size(117, 510);
            this.uiPanel2.TabIndex = 3;
            // 
            // btnRound
            // 
            this.btnRound.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRound.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRound.Location = new System.Drawing.Point(3, 83);
            this.btnRound.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnRound.Name = "btnRound";
            this.btnRound.Size = new System.Drawing.Size(71, 34);
            this.btnRound.TabIndex = 2;
            this.uiToolTip1.SetToolTip(this.btnRound, "环绕");
            this.btnRound.Click += new System.EventHandler(this.btnRound_Click);
            // 
            // btnMove
            // 
            this.btnMove.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnMove.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnMove.Location = new System.Drawing.Point(3, 43);
            this.btnMove.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnMove.Name = "btnMove";
            this.btnMove.Size = new System.Drawing.Size(71, 34);
            this.btnMove.TabIndex = 1;
            this.uiToolTip1.SetToolTip(this.btnMove, "横移");
            this.btnMove.Click += new System.EventHandler(this.btnMove_Click);
            // 
            // btnRotate
            // 
            this.btnRotate.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRotate.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRotate.Location = new System.Drawing.Point(3, 3);
            this.btnRotate.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnRotate.Name = "btnRotate";
            this.btnRotate.Size = new System.Drawing.Size(71, 34);
            this.btnRotate.TabIndex = 0;
            this.uiToolTip1.SetToolTip(this.btnRotate, "旋转");
            this.btnRotate.Click += new System.EventHandler(this.btnRotate_Click);
            // 
            // elementHost1
            // 
            this.elementHost1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.elementHost1.Location = new System.Drawing.Point(3, 3);
            this.elementHost1.Name = "elementHost1";
            this.elementHost1.Size = new System.Drawing.Size(923, 514);
            this.elementHost1.TabIndex = 0;
            this.elementHost1.Text = "elementHost1";
            this.elementHost1.Child = null;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 7;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.btnTraceFile, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnRenderParam, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnAssimpImport, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnSurface, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnZColor, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnClearData, 2, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 523);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1048, 44);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.btnRotate, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.btnRound, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.btnMove, 0, 1);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(117, 510);
            this.tableLayoutPanel2.TabIndex = 3;
            // 
            // HelixRenderForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "HelixRenderForm";
            this.Size = new System.Drawing.Size(1054, 570);
            this.uiToolTip1.SetToolTip(this, "旋转");
            this.uiTableLayoutPanel1.ResumeLayout(false);
            this.uiPanel2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntdUI.GridPanel uiTableLayoutPanel1;
        private System.Windows.Forms.Integration.ElementHost elementHost1;
        private AntdUI.Button btnSurface;
        private AntdUI.Button btnZColor;
        private AntdUI.Button btnTraceFile;
        private AntdUI.Button btnAssimpImport;
        private AntdUI.Button btnClearData;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Panel uiPanel2;
        private AntdUI.Button btnRenderParam;
        private AntdUI.Button btnRound;
        private AntdUI.Button btnMove;
        private AntdUI.Button btnRotate;
        private System.Windows.Forms.ToolTip uiToolTip1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
    }
}
