using AdaWeldSystem.Comm;
using AdaWeldSystem.EmguALG;
using AdaWeldSystem.FileOperate;
using System;
using System.IO;
using System.Windows.Forms;

namespace AdaWeldSystem.MonitorCam
{
    /// <summary>
    /// 监控相机算法管理器（单例）：持有焊缝检查所需的唯一一套可配置算法参数。
    /// 针对的仅是焊缝检查、无多种类，因此不引入 JOB 切换，仅有单一可配置算法 Profile。
    /// 算法参数（对中阈值 / 质量合格分）与相机硬件参数（IP/端口/曝光）在此分离，
    /// 纯算法实现仍集中在 EmguALG/ImageAlgorithm.cs 第 8 区（遵循 [[decisions/ADR-003-SingleAlgorithmFile]]）。
    /// 持久化到 Config/INI/MonitorAlgorithm.ini（遵循 [[decisions/ADR-008-ConfigLayout]]）。
    /// 须在应用启动、创建读取它的 UI 页面之前调用 Load()（参见 [[lessons/StartupSingletonInitialization]]）。
    /// </summary>
    public class MonitorAlgorithmManager
    {
        #region 单例

        private static readonly Lazy<MonitorAlgorithmManager> _lazy =
            new Lazy<MonitorAlgorithmManager>(() => new MonitorAlgorithmManager());
        public static MonitorAlgorithmManager Instance => _lazy.Value;

        #endregion

        #region 字段

        private readonly string _tag = "监控算法管理器";

        // 唯一一套可配置算法参数（针对焊缝检查，无多种类 → 无 JOB 切换）
        public PreWeldAlignmentConfig AlignmentConfig { get; set; }
        public WeldQualityConfig QualityConfig { get; set; }

        private static readonly string _configFile =
            Path.Combine(Application.StartupPath, "Config", "INI", "MonitorAlgorithm.ini");

        #endregion

        #region 构造函数（ADR-002：构造函数内初始化，禁用 auto-property initializer）

        private MonitorAlgorithmManager()
        {
            AlignmentConfig = new PreWeldAlignmentConfig();
            QualityConfig = new WeldQualityConfig();
            SetDefaults();
        }

        private void SetDefaults()
        {
            AlignmentConfig.ToleranceX = 8;
            AlignmentConfig.ToleranceY = 8;
            AlignmentConfig.ToleranceAngle = 2.0;
            QualityConfig.PassScore = 80;
        }

        #endregion

        #region 配置持久化（INI，归 Config/INI，符合 ADR-008）

        /// <summary>
        /// 从配置文件加载算法参数。须在应用启动、创建子页面之前调用；
        /// 文件不存在或读取失败时保留默认/当前值。
        /// </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_configFile))
                    return;

                var ini = new INIFile(_configFile);
                AlignmentConfig.ToleranceX = ini.ReadDouble("Alignment", "ToleranceX", AlignmentConfig.ToleranceX);
                AlignmentConfig.ToleranceY = ini.ReadDouble("Alignment", "ToleranceY", AlignmentConfig.ToleranceY);
                AlignmentConfig.ToleranceAngle = ini.ReadDouble("Alignment", "ToleranceAngle", AlignmentConfig.ToleranceAngle);
                QualityConfig.PassScore = ini.ReadDouble("Quality", "PassScore", QualityConfig.PassScore);
            }
            catch
            {
                // 加载异常时保留默认/当前值
            }
        }

        /// <summary>
        /// 将当前算法参数持久化到配置文件。用户通过界面「保存算法参数」时调用。
        /// </summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configFile));
                var ini = new INIFile(_configFile);
                ini.WriteDouble("Alignment", "ToleranceX", AlignmentConfig.ToleranceX);
                ini.WriteDouble("Alignment", "ToleranceY", AlignmentConfig.ToleranceY);
                ini.WriteDouble("Alignment", "ToleranceAngle", AlignmentConfig.ToleranceAngle);
                ini.WriteDouble("Quality", "PassScore", QualityConfig.PassScore);
                ini.SaveToFile();
            }
            catch
            {
                // 保存失败不影响运行时
            }
        }

        #endregion
    }
}
