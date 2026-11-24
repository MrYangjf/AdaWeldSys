using System;
using System.Collections.Generic;
using System.IO;

namespace AdaWeldSystem.FileOperate
{
    /// <summary>旧配置目录单向搬迁到新结构。</summary>
    public static class ConfigPathMigrator
    {
        /// <summary>程序运行目录（配置树根）。</summary>
        private static readonly string ConfigBase = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>迁移后需要按「空目录才删」规则清理的旧根目录。</summary>
        private static readonly string[] LegacyRoots =
        {
            @"Config\XML",
            @"Config\INI",
            @"Config\Comm\KUKARobot",
            @"Config\Comm\Tcp_XML",
            @"Config\Comm\PLC_XML",
            @"Config\Comm\Modbus_XML",
            @"Config\LineLaser",
            @"Config\MonitorCam",
            @"Config\MotionControl",
            @"Config\EmguALG",
            @"Config\PCLOperate",
            @"Config\MainDeviceControl",
            @"Config\WeldParamControl"
        };

        /// <summary>历史目录→现行目录（仅目标不存在时搬迁）。</summary>
        private static readonly string[][] LegacyDirectoryMoves =
        {
            new[] { @"Config\Csv", @"Config\MotionControl\Csv" }
        };

        /// <summary>整体搬迁历史数据目录；目标已存在时保留目标</summary>

        /// <param name="legacyRelativePath">历史目录相对路径。</param>
        /// <param name="currentRelativePath">现行功能类别目录相对路径。</param>
        private static void MoveDirectoryIfNeeded(string legacyRelativePath, string currentRelativePath)
        {
            string legacyDirectory = Path.Combine(ConfigBase, legacyRelativePath);
            if (!Directory.Exists(legacyDirectory))
            {
                return;
            }

            string currentDirectory = Path.Combine(ConfigBase, currentRelativePath);
            if (Directory.Exists(currentDirectory))
            {
                if (Directory.GetFileSystemEntries(legacyDirectory).Length == 0)
                {
                    Directory.Delete(legacyDirectory);
                }
                return;
            }

            string parentDirectory = Path.GetDirectoryName(currentDirectory);
            if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }
            Directory.Move(legacyDirectory, currentDirectory);
        }

        /// <summary>构建迁移表：现行相对路径 → 历史/过渡布局的候选</summary>

        /// <returns>迁移表。</returns>
        private static Dictionary<string, string[]> BuildMigrationMap()
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // 全局配置统一存放目录
                { @"Config\ConfigFileControl\MotionControl\DeltaMotionConfig.xml", new[] { @"Config\MotionControl\Motion_XML\DeltaMotionConfig.xml", @"Config\XML\MotionControl\Motion_XML\DeltaMotionConfig.xml", @"Config\XML\MotionControl\DeltaMotionConfig.xml", @"Config\XML\DeltaMotionConfig.xml" } },
                { @"Config\ConfigFileControl\MotionControl\WeldHeadSlave.xml", new[] { @"Config\MotionControl\WeldHead_XML\WeldHeadSlave.xml", @"Config\XML\MotionControl\WeldHead_XML\WeldHeadSlave.xml", @"Config\XML\MotionControl\WeldHeadSlave.xml", @"Config\XML\WeldHeadSlave.xml" } },
                { @"Config\ConfigFileControl\LineLaser\LineLaserConfig.xml", new[] { @"Config\LineLaser\LineLaser_XML\LineLaserConfig.xml", @"Config\XML\LineLaser\LineLaser_XML\LineLaserConfig.xml", @"Config\XML\LineLaser\LineLaserConfig.xml", @"Config\XML\LineLaserConfig.xml" } },
                { @"Config\ConfigFileControl\Comm\CommunicationSettings.ini", new[] { @"Config\Comm\Communication_INI\CommunicationSettings.ini", @"Config\INI\Comm\Communication_INI\CommunicationSettings.ini", @"Config\INI\Comm\CommunicationSettings.ini", @"Config\INI\CommunicationSettings.ini" } },
                { @"Config\ConfigFileControl\Comm\Server.xml", new[] { @"Config\Comm\Tcp_XML\Server.xml" } },
                { @"Config\ConfigFileControl\Comm\Client.xml", new[] { @"Config\Comm\Tcp_XML\Client.xml" } },
                { @"Config\ConfigFileControl\Comm\Send.xml", new[] { @"Config\Comm\Tcp_XML\Send.xml" } },
                { @"Config\ConfigFileControl\Comm\Recive.xml", new[] { @"Config\Comm\Tcp_XML\Recive.xml" } },
                { @"Config\ConfigFileControl\Comm\RobotServer.xml", new[] { @"Config\Comm\KUKARobot_RSI_XML\RobotServer.xml" } },
                { @"Config\ConfigFileControl\Comm\RobotClient.xml", new[] { @"Config\Comm\KUKARobot_EKI_XML\RobotClient.xml" } },
                { @"Config\ConfigFileControl\Comm\PLC.xml", new[] { @"Config\Comm\PLC_XML\PLC.xml" } },
                { @"Config\ConfigFileControl\Comm\Modbus.xml", new[] { @"Config\Comm\Modbus_XML\Modbus.xml" } },
                { @"Config\ConfigFileControl\MonitorCam\MonitorCamera.ini", new[] { @"Config\MonitorCam\Monitor_INI\MonitorCamera.ini", @"Config\INI\MonitorCam\Monitor_INI\MonitorCamera.ini", @"Config\INI\MonitorCamera.ini" } },
                { @"Config\ConfigFileControl\PCLOperate\RenderConfig.ini", new[] { @"Config\PCLOperate\Render_INI\RenderConfig.ini", @"Config\INI\PCLOperate\RenderConfig.ini", @"Config\INI\RenderConfig.ini" } },
                { @"Config\ConfigFileControl\MainDeviceControl\SimulationController.ini", new[] { @"Config\MainDeviceControl\Simulation_INI\SimulationController.ini", @"Config\INI\MainDeviceControl\SimulationController.ini", @"Config\INI\SimulationController.ini" } },
                { @"Config\ConfigFileControl\EmguALG\MonitorAlgorithm.ini", new[] { @"Config\EmguALG\Algorithm_INI\MonitorAlgorithm.ini", @"Config\INI\EmguALG\MonitorAlgorithm.ini", @"Config\INI\MonitorAlgorithm.ini" } },
                { @"Config\ConfigFileControl\EmguALG\ImageRoi.ini", new[] { @"Config\EmguALG\Algorithm_INI\ImageRoi.ini", @"Config\INI\EmguALG\ImageRoi.ini", @"Config\INI\ImageRoi.ini" } },
                { @"Config\ConfigFileControl\WeldParam\WeldParam.ini", new[] { @"Config\WeldParamControl\Algorithm_INI\WeldParamControlAlgorithm.ini", @"Config\INI\WeldParam\Algorithms\WeldParamControlAlgorithm.ini", @"Config\INI\WeldParam\WeldParamControlAlgorithm.ini", @"Config\INI\WeldParamControlAlgorithm.ini" } },
                { @"Config\ConfigFileControl\WeldParam\WeldParam.xml", new[] { @"Config\WeldParamControl\Algorithm_XML\WeldParamControl.xml", @"Config\XML\WeldParam\Algorithms\WeldParamControl.xml", @"Config\XML\WeldParam\WeldParamControl.xml", @"Config\XML\WeldParamControl.xml" } },
                { @"Config\ConfigFileControl\WeldParam\TaLaCurve.xml", new[] { @"Config\WeldParamControl\Algorithm_XML\TaLaCurve.xml" } },
                { @"Config\ConfigFileControl\WeldParam\WeldParamControlWorkflow.xml", new[] { @"Config\WeldParamControl\Workflow_XML\WeldParamControlWorkflow.xml", @"Config\XML\WeldParam\Workflow_XML\WeldParamControlWorkflow.xml", @"Config\XML\WeldParam\WeldParamControlWorkflow.xml", @"Config\XML\WeldParamControlWorkflow.xml" } },
                { @"Config\ConfigFileControl\LaserWeldHead\LaserWeldHeadWorkflow.xml", new[] { @"Config\LaserWeldHead\Workflow_XML\LaserWeldHeadWorkflow.xml", @"Config\XML\LaserWeldHead\Workflow_XML\LaserWeldHeadWorkflow.xml", @"Config\XML\LaserWeldHead\LaserWeldHeadWorkflow.xml" } },
                { @"Config\ConfigFileControl\LineLaser\LineLaserWorkflow.xml", new[] { @"Config\LineLaser\Workflow_XML\LineLaserWorkflow.xml", @"Config\XML\LineLaser\Workflow_XML\LineLaserWorkflow.xml", @"Config\XML\LineLaser\LineLaserWorkflow.xml" } },
                { @"Config\ConfigFileControl\MonitorCam\MonitorCameraWorkflow.xml", new[] { @"Config\MonitorCam\Workflow_XML\MonitorCameraWorkflow.xml", @"Config\XML\MonitorCam\Workflow_XML\MonitorCameraWorkflow.xml", @"Config\XML\MonitorCam\MonitorCameraWorkflow.xml" } },
                { @"Config\ConfigFileControl\MotionControl\MotionControlWorkflow.xml", new[] { @"Config\MotionControl\Workflow_XML\MotionControlWorkflow.xml", @"Config\XML\MotionControl\Workflow_XML\MotionControlWorkflow.xml", @"Config\XML\MotionControl\MotionControlWorkflow.xml" } },
                { @"Config\ConfigFileControl\WeldParam\WeldData.ini", new[] { @"Config\WeldParamControl\WeldData_INI\WeldData.ini", @"Config\INI\WeldParam\WeldData.ini", @"Config\INI\WeldData.ini" } },
            };
        }

        /// <summary>把候选旧路径中存在的文件搬到现行路径；目标</summary>

        /// <param name="legacyCandidates">历史/过渡布局的候选相对路径，按优先级排列。</param>
        /// <param name="currentRelativePath">现行范式相对路径（迁移目标）。</param>
        private static void MoveToCurrentPath(string[] legacyCandidates, string currentRelativePath)
        {
            string currentPath = Path.Combine(ConfigBase, currentRelativePath);
            string currentDir = Path.GetDirectoryName(currentPath);

            foreach (string candidate in legacyCandidates)
            {
                string legacyPath = Path.Combine(ConfigBase, candidate);
                if (!File.Exists(legacyPath))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(currentDir) && !Directory.Exists(currentDir))
                {
                    Directory.CreateDirectory(currentDir);
                }

                if (File.Exists(currentPath))
                {
                    File.Delete(legacyPath);
                }
                else
                {
                    File.Move(legacyPath, currentPath);
                }
            }
        }

        /// <summary>自底向上清理空目录；目录内仍有文件或非空子</summary>

        /// <param name="directory">待清理目录绝对路径。</param>
        private static void PruneEmptyDirectories(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string subDirectory in Directory.GetDirectories(directory))
            {
                PruneEmptyDirectories(subDirectory);
            }

            if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }

        /// <summary>执行全部配置路径迁移与旧目录清理。</summary>
        public static void MigrateAll()
        {
            Dictionary<string, string[]> migrationMap = BuildMigrationMap();
            foreach (KeyValuePair<string, string[]> pair in migrationMap)
            {
                try
                {
                    MoveToCurrentPath(pair.Value, pair.Key);
                }
                catch
                {
                    // 单条失败不影响其余，旧文件留待下次重试
                }
            }

            foreach (string[] directoryMove in LegacyDirectoryMoves)
            {
                try
                {
                    MoveDirectoryIfNeeded(directoryMove[0], directoryMove[1]);
                }
                catch
                {
                    // 清理失败无副作用：旧目录留待下次启动
                }
            }

            foreach (string legacyRoot in LegacyRoots)
            {
                try
                {
                    PruneEmptyDirectories(Path.Combine(ConfigBase, legacyRoot));
                }
                catch
                {
                    // 清理失败无副作用：旧目录留待下次启动
                }
            }
        }
    }
}

