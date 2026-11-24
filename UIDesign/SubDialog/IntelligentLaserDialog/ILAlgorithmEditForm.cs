using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.LineLaserCam;
using AntdUI;
using System;
using System.Windows.Forms;
using AdaWeldSystem.EmguALG.Core;

namespace AdaWeldSystem.UIDesign.SubDialog.IntelligentLaserDialog
{
    /// <summary>算法编辑窗体：按激活相机类型挂载对应页。</summary>
    public partial class ILAlgorithmEditForm : UserControl
    {
        // 注入父 Window（本窗暂无内部 Message/Modal，保留字段供统一调用）
        private readonly Window Window;
        private readonly PipelineConfig _config;
        // 按接口只创建并挂载其中一个页；另一个保持 null（即不显示）
        private readonly ILAlgoForm _ilPage;
        private readonly IntelligentLaserCam _ilCamera;

        public ILAlgorithmEditForm(Window _window, PipelineConfig config)
        {
            Window = _window;
            InitializeComponent();
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 按激活相机类型只挂载对应页，隐藏另一页
            _ilCamera = LineLaserManager.Instance.IntelligentLaser;
            if (_ilCamera != null)
            {
                _ilPage = new ILAlgoForm(_ilCamera)
                {
                    Dock = DockStyle.Fill
                };
                AntdUI.TabPage ilTab = new AntdUI.TabPage { Text = "英莱算法", Tag = _ilPage };
                ilTab.Controls.Add(_ilPage);
                tabsAlgo.AddTabSelect(ilTab);
            }

            this.HandleDestroyed += AlgorithmEditForm_HandleDestroyed;
        }

        private void AlgorithmEditForm_HandleDestroyed(object sender, EventArgs e)
        {
            // 退订英莱页的轮廓帧就绪事件
            _ilPage?.StopRefresh();
        }
    }
}
