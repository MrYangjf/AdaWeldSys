using AdaWeldSystem.Sub3UI;
using AdaWeldSystem.WeldParamControl;
using AntdUI;
using System;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub2UI
{
    public partial class ParamConfigPage : UserControl
    {
        private string _paramIdName;

        public ParamConfigPage()
        {
            InitializeComponent();
            LoadAutoParam();
        }

        private void ParamAutoPage_Shown(object sender, EventArgs e)
        {
            if (WeldDataManager.Instance.mAutoParam != null)
                LoadParamOnForm();
        }

        private void btnShowConfigList_Click(object sender, EventArgs e)
        {
            using (ConfigForm configForm = new ConfigForm(this.FindForm() as AntdUI.Window))
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "系统配置", configForm, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }

            string currentConfig = WeldDataManager.Instance.CurrentAutoParamId;
            if (_paramIdName != currentConfig)
            {
                _paramIdName = currentConfig;
                AntdUI.Message.info(System.Windows.Forms.Form.ActiveForm, $"配置切换至->{_paramIdName}");
            }

            LoadParamOnForm();
        }

        private void btnAddNewConfig_Click(object sender, EventArgs e)
        {
            string configName = string.Empty;
            if (!this.ShowInputStringDialog(ref configName, "请输入配置名称"))
                return;

            if (WeldDataManager.Instance.mConfigManager.IsConfigExist(configName))
            {
                AntdUI.Message.error(System.Windows.Forms.Form.ActiveForm, "已存在当前命名配置");
                return;
            }

            WeldDataManager.Instance.AddConfig(configName);
            _paramIdName = configName;

            LoadAutoParam();
            if (WeldDataManager.Instance.mAutoParam != null)
                LoadParamOnForm();
        }

        private void LoadParamOnForm()
        {
            if (WeldDataManager.Instance.mAutoParam == null)
                return;

            AutoParam param = WeldDataManager.Instance.mAutoParam;

            uiComboBox1.Text = param.WeldType.ToString();
            mAutoParamName.Text = param.identityInfo;
            WireType.Text = param.WireType;
            PlateType.Text = param.PlateType;
            WireDiameter.Value = (decimal)param.WireDiameter;
            Platethickness.Value = (decimal)param.Platethickness;
            LaserDiameter.Value = (decimal)param.LaserDiameter;
            LaserPower.Value = (decimal)param.LaserPower;
            FeedSpeed.Value = (decimal)param.FeedSpeed;
            RobotSpeed.Value = (decimal)param.RobotSpeed;
            SeamWidth.Value = (decimal)param.SeamWidth;
            SeamLength.Value = (decimal)param.SeamLength;
            MaxSeamWidth.Value = (decimal)param.SeamWidthMax;
            MinSeamWidth.Value = (decimal)param.SeamWidthMin;
            Sensitivity.Value = (decimal)param.Sensitivity;
        }

        private void CollectParamFromForm()
        {
            if (WeldDataManager.Instance.mAutoParam == null)
                return;

            AutoParam param = WeldDataManager.Instance.mAutoParam;
            param.WeldType = (SeamType)uiComboBox1.SelectedIndex;
            param.WireType = WireType.Text;
            param.PlateType = PlateType.Text;
            param.WireDiameter = (double)WireDiameter.Value;
            param.Platethickness = (double)Platethickness.Value;
            param.LaserDiameter = (double)LaserDiameter.Value;
            param.LaserPower = (double)LaserPower.Value;
            param.FeedSpeed = (double)FeedSpeed.Value;
            param.RobotSpeed = (double)RobotSpeed.Value;
            param.SeamWidth = (double)SeamWidth.Value;
            param.SeamLength = (double)SeamLength.Value;
            param.SeamWidthMax = (double)MaxSeamWidth.Value;
            param.SeamWidthMin = (double)MinSeamWidth.Value;
            param.Sensitivity = (double)Sensitivity.Value;
        }

        private void RefreshParam()
        {
            WeldDataManager.Instance.ReloadCurrentParamValues();
            LoadParamOnForm();
        }

        private void SaveParamToDatabase()
        {
            WeldDataManager.Instance.SaveCurrentAutoParam();
        }

        private void LoadAutoParam()
        {
            _paramIdName = WeldDataManager.Instance.CurrentAutoParamId;
            // WeldDataManager 单例已在构造时完成当前配置的加载
        }

        private void btnCorAutoParam_Click(object sender, EventArgs e)
        {
            CollectParamFromForm();
            SaveParamToDatabase();
            RefreshParam();
        }

        private void btnShowAutoParam_Click(object sender, EventArgs e)
        {
            using (AutoParamForm autoValueForm = new AutoParamForm(this.FindForm() as AntdUI.Window))
            {
                autoValueForm.LoadConfigData(WeldDataManager.Instance.mAutoParam);
                AntdUI.Modal.open(new AntdUI.Modal.Config(this.FindForm(), "自动参数配置", autoValueForm, TType.Info)
                {
                    CloseIcon = true,
                    BtnHeight = 0
                });
            }
        }

        /// <summary>
        /// 替代 SunnyUI.UIPage.ShowInputStringDialog / Microsoft.VisualBasic.Interaction.InputBox 的输入对话框。
        /// 用 AntdUI Modal + Input 实现：返回 true 表示用户点击确认且输入非空，value 写回输入结果。
        /// 官方 Demo（ModalDemo.cs）确认 Modal.Config 支持 4 参构造 (owner, title, Control 内容体, TType)，可嵌入自定义控件。
        /// 裸 AntdUI.Input 默认尺寸为 0（Size={0,0}/LineHeight=0/AutoSize=false），作为内容体时高度为 0 不可见；
        /// 须用 Panel 包裹（Dock=Fill + 明确高度），令 Input 在内容区获得尺寸。
        /// </summary>
        private bool ShowInputStringDialog(ref string value, string prompt)
        {

            AntdUI.Input inputBox = new AntdUI.Input
            {
                Text = value ?? "",
                Height = 40,
                Width = 300,
                Location = new System.Drawing.Point(0, 0),
            };

            string result = value ?? "";
            var config = new AntdUI.Modal.Config(
                this.FindForm() ?? System.Windows.Forms.Form.ActiveForm,
                prompt, inputBox, AntdUI.TType.Info)
            {
                OkText = "确认",
                CancelText = "取消",
                Width = 320,
                OnOk = cfg =>
                {
                    if (string.IsNullOrWhiteSpace(inputBox.Text))
                        return false; // 输入为空则不关闭，强制用户填写或取消
                    result = inputBox.Text;
                    return true;
                }
            };
            if (AntdUI.Modal.open(config) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(result))
            {
                value = result;
                return true;
            }
            return false;
        }
    }
}
