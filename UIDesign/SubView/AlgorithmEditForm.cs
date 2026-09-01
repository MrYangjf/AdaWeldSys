using AdaWeldSystem.EmguALG;
using AdaWeldSystem.LineLaserCam.IntelligentLaserCam;
using AdaWeldSystem.LineLaserCamApi;
using AntdUI;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub3UI
{
    /// <summary>
    /// 算法编辑窗体（AntdUI 迁移版）。
    /// 按当前激活相机的接口（CameraSelector.Active 类型）只挂载对应的算法编辑页，
    /// 另一页根本不 AddTabSelect，从而「只显示并编辑对应算法、隐藏另一套」——满足接口分隔要求。
    /// 迁移要点（ADR-021 / AntdUI-DesignParadigm）：
    /// 子页（EmguAlgoPage / ILAlgoPage）为 UserControl，需包进 AntdUI.TabPage 再 AddTabSelect（Dock=Fill 加入 Controls）。
    /// 底部 确定/取消 按钮始终可见（位于 Tabs 之外）。
    /// </summary>
    public partial class AlgorithmEditForm : UserControl
    {
        // 复刻 AntdUI demo SystemSet 模式：注入父 Window（本窗暂无内部 Message/Modal，保留字段供统一调用范式）
        private readonly Window Window;
        private readonly PipelineConfig _config;
        // 按接口只创建并挂载其中一个页；另一个保持 null（即不显示）
        private readonly ILAlgoPage _ilPage;
        private readonly IntelligentLaserCameraRun _ilCamera;

        public AlgorithmEditForm(Window _window, PipelineConfig config)
        {
            Window = _window;
            InitializeComponent();
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 按接口判断当前激活相机类型，仅挂载对应算法编辑页（隐藏另一页）
            _ilCamera = CameraSelector.Active as IntelligentLaserCameraRun;
            if (_ilCamera != null)
            {
                _ilPage = new ILAlgoPage(_ilCamera);
                _ilPage.Dock = DockStyle.Fill;
                AntdUI.TabPage ilTab = new AntdUI.TabPage { Text = "英莱算法", Tag = _ilPage };
                ilTab.Controls.Add(_ilPage);
                tabsAlgo.AddTabSelect(ilTab);
            }

            this.HandleDestroyed += AlgorithmEditForm_HandleDestroyed;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // 仅当项目算法页存在时（非英莱相机）把网格编辑同步回配置
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            var host = this.FindForm();
            if (host != null) host.DialogResult = DialogResult.Cancel;
        }

        private void AlgorithmEditForm_HandleDestroyed(object sender, EventArgs e)
        {
            // 退订英莱页的轮廓帧就绪事件（ADR-001：不在 .cs 重写 Dispose(bool)）
            if (_ilPage != null)
                _ilPage.StopRefresh();
        }
    }
}
