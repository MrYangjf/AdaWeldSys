using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.UIDesign.MainWindow;

namespace AdaWeldSystem
{


    internal static class Program
    {
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 配置迁移须先于 Manager 构造，否则旧配置搬不进来
            ConfigPathMigrator.MigrateAll();

            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
