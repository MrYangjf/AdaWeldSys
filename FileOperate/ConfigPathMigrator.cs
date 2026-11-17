using System;
using System.Collections.Generic;
using System.IO;

namespace AdaWeldSystem.FileOperate
{
    /// <summary>旧配置目录单向搬迁到新结构。</summary>
    /// <remarks>单向搬迁到现行路径，不做回退读。失败不阻塞启动。
    /// 须在任何 Manager 构造前调用（Xdoc 构造即建空文件）；启动早期日志未就绪，禁用 ShowLog。</remarks>
    public static class ConfigPathMigrator
    {
        /// <summary>程序运行目录（配置树根）。</summary>
        private static readonly string ConfigBase = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>迁移后需要按「空目录才删」规则清理的旧根目录。</summary>
        private static readonly string[] LegacyRoots =
        {
            @"Config\XML",
            @"Config\INI",
            @"Config\Comm\KUKARobot"
        };

        /// <summary>历史目录→现行目录（仅目标不存在时搬迁）。</summary>
        private static readonly string[][] LegacyDirectoryMoves =
        {
            new[] { @"Config\Csv", @"Config\MotionControl\Csv" }
        };

        /// <summary>整体搬迁历史数据目录；目标已存在时保留目标</summary>
        /// <remarks>现状，仅在旧目录为空时删除旧目录。</remarks>
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
        /// <remarks>相对路径（按优先级排列）。</remarks>
        /// <returns>迁移表。</returns>
        private static Dictionary<string, string[]> BuildMigrationMap()
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // 运控
                { @"Config\MotionControl\Motion_XML\DeltaMotionConfig.xml", new[] { @"Config\XML\MotionControl\Motion_XML\DeltaMotionConfig.xml", @"Config\XML\MotionControl\DeltaMotionConfig.xml", @"Config\XML\DeltaMotionConfig.xml" } },
                { @"Config\MotionControl\WeldHead_XML\WeldHeadSlave.xml", new[] { @"Config\XML\MotionControl\WeldHead_XML\WeldHeadSlave.xml", @"Config\XML\MotionControl\WeldHeadSlave.xml", @"Config\XML\WeldHeadSlave.xml" } },
                { @"Config\MotionControl\Workflow_XML\MotionControlWorkflow.xml", new[] { @"Config\XML\MotionControl\Workflow_XML\MotionControlWorkflow.xml", @"Config\XML\MotionControl\MotionControlWorkflow.xml", @"Config\XML\MotionControlWorkflow.xml" } },

                // 线激光
                { @"Config\LineLaser\LineLaser_XML\LineLaserConfig.xml", new[] { @"Config\XML\LineLaser\LineLaser_XML\LineLaserConfig.xml", @"Config\XML\LineLaser\LineLaserConfig.xml", @"Config\XML\LineLaserConfig.xml" } },
                { @"Config\LineLaser\Workflow_XML\LineLaserWorkflow.xml", new[] { @"Config\XML\LineLaser\Workflow_XML\LineLaserWorkflow.xml", @"Config\XML\LineLaser\LineLaserWorkflow.xml", @"Config\XML\LineLaserWorkflow.xml" } },
                { @"Config\LineLaser\Camera_INI\CameraPresets.ini", new[] { @"Config\INI\LineLaser\Camera_INI\CameraPresets.ini", @"Config\INI\LineLaser\CameraPresets.ini", @"Config\INI\CameraPresets.ini" } },
                { @"Config\LineLaser\Camera_INI\CameraSelector.ini", new[] { @"Config\INI\LineLaser\Camera_INI\CameraSelector.ini", @"Config\INI\LineLaser\CameraSelector.ini", @"Config\INI\CameraSelector.ini" } },

                // 焊参
                { @"Config\WeldParamControl\Algorithm_XML\WeldParamControl.xml", new[] { @"Config\XML\WeldParam\Algorithms\WeldParamControl.xml", @"Config\XML\WeldParam\Algorithm_XML\WeldParamControl.xml", @"Config\XML\WeldParam\WeldParamControl.xml", @"Config\XML\WeldParamControl.xml" } },
                { @"Config\WeldParamControl\Algorithm_INI\WeldParamControlAlgorithm.ini", new[] { @"Config\INI\WeldParam\Algorithms\WeldParamControlAlgorithm.ini", @"Config\INI\WeldParam\Algorithm_INI\WeldParamControlAlgorithm.ini", @"Config\INI\WeldParam\WeldParamControlAlgorithm.ini", @"Config\INI\WeldParamControlAlgorithm.ini" } },
                { @"Config\WeldParamControl\Algorithm_INI\AlgorithmParams.ini", new[] { @"Config\INI\WeldParam\Algorithms\AlgorithmParams.ini", @"Config\INI\WeldParam\Algorithm_INI\AlgorithmParams.ini", @"Config\INI\WeldParam\AlgorithmParams.ini", @"Config\INI\AlgorithmParams.ini" } },
                { @"Config\WeldParamControl\WeldData_INI\WeldData.ini", new[] { @"Config\INI\WeldParam\WeldData_INI\WeldData.ini", @"Config\INI\WeldParam\WeldData.ini", @"Config\INI\WeldData.ini" } },
                { @"Config\WeldParamControl\Workflow_XML\WeldParamControlWorkflow.xml", new[] { @"Config\XML\WeldParam\Workflow_XML\WeldParamControlWorkflow.xml", @"Config\XML\WeldParam\WeldParamControlWorkflow.xml", @"Config\XML\WeldParamControlWorkflow.xml" } },

                // 监控相机
                { @"Config\MonitorCam\Monitor_INI\MonitorCamera.ini", new[] { @"Config\INI\Monitor\Monitor_INI\MonitorCamera.ini", @"Config\INI\Monitor\MonitorCamera.ini", @"Config\INI\MonitorCamera.ini" } },
                { @"Config\MonitorCam\Workflow_XML\MonitorCameraWorkflow.xml", new[] { @"Config\XML\Monitor\Workflow_XML\MonitorCameraWorkflow.xml", @"Config\XML\Monitor\MonitorCameraWorkflow.xml", @"Config\XML\MonitorCameraWorkflow.xml" } },

                // 焊接头
                { @"Config\LaserWeldHead\Workflow_XML\LaserWeldHeadWorkflow.xml", new[] { @"Config\XML\LaserWeldHead\Workflow_XML\LaserWeldHeadWorkflow.xml", @"Config\XML\LaserWeldHead\LaserWeldHeadWorkflow.xml", @"Config\XML\LaserWeldHeadWorkflow.xml" } },

                // 主控
                { @"Config\MainDeviceControl\Simulation_INI\SimulationController.ini", new[] { @"Config\INI\MainDevice\Simulation_INI\SimulationController.ini", @"Config\INI\MainDevice\SimulationController.ini", @"Config\INI\SimulationController.ini" } },

                // 产品记录
                { @"Config\ProductFileControl\Record_INI\Record.ini", new[] { @"Config\INI\ProductFile\Record_INI\Record.ini", @"Config\INI\ProductFile\Record.ini", @"Config\INI\Record.ini" } },

                // 三维渲染
                { @"Config\PCLOperate\Render_INI\RenderConfig.ini", new[] { @"Config\INI\Render\Render_INI\RenderConfig.ini", @"Config\INI\Render\RenderConfig.ini", @"Config\INI\RenderConfig.ini" } },

                // 图像算法
                { @"Config\EmguALG\Algorithm_INI\MonitorAlgorithm.ini", new[] { @"Config\INI\EmguALG\Algorithm_INI\MonitorAlgorithm.ini", @"Config\INI\EmguALG\MonitorAlgorithm.ini", @"Config\INI\MonitorAlgorithm.ini" } },
                { @"Config\EmguALG\Algorithm_INI\ImageRoi.ini", new[] { @"Config\INI\EmguALG\Algorithm_INI\ImageRoi.ini", @"Config\INI\EmguALG\ImageRoi.ini", @"Config\INI\ImageRoi.ini" } },

                // 英莱相机
                { @"Config\IntelligentLaserCam\Camera_INI\IntelligentLaser.ini", new[] { @"Config\INI\IntelligentLaserCam\Camera_INI\IntelligentLaser.ini", @"Config\INI\IntelligentLaser\IntelligentLaser.ini", @"Config\INI\IntelligentLaser.ini" } },

                // 通讯：INI 开关
                { @"Config\Comm\Communication_INI\CommunicationSettings.ini", new[] { @"Config\INI\Comm\Communication_INI\CommunicationSettings.ini", @"Config\INI\Comm\CommunicationSettings.ini", @"Config\INI\CommunicationSettings.ini" } },

                // 通讯：各通道 XML
                { @"Config\Comm\Tcp_XML\Server.xml", new[] { @"Config\XML\Server.xml" } },
                { @"Config\Comm\Tcp_XML\Client.xml", new[] { @"Config\XML\Client.xml" } },
                { @"Config\Comm\Tcp_XML\Send.xml", new[] { @"Config\XML\Send.xml" } },
                { @"Config\Comm\Tcp_XML\Recive.xml", new[] { @"Config\XML\Recive.xml" } },
                { @"Config\Comm\PLC_XML\PLC.xml", new[] { @"Config\XML\PLC.xml" } },
                { @"Config\Comm\Modbus_XML\Modbus.xml", new[] { @"Config\XML\Modbus.xml" } },
                { @"Config\Comm\Communication_XML\CommunicationConfig.xml", new[] { @"Config\XML\CommunicationConfig.xml" } },
                { @"Config\Comm\KUKARobot_RSI_XML\RobotServer.xml", new[] { @"Config\XML\RobotServer.xml" } },
                { @"Config\Comm\KUKARobot_RSI_XML\WeldControlConfig.xml", new[] { @"Config\XML\WeldControlConfig.xml" } },
                { @"Config\Comm\KUKARobot_RSI_XML\WeldControlSendFormat.xml", new[] { @"Config\XML\WeldControlSendFormat.xml" } },
                { @"Config\Comm\KUKARobot_RSI_XML\RSI_Ethernet.xml", new[] { @"Comm\KUKARobot\RSI_Ethernet.xml", @"Config\Comm\KUKARobot\RSI_Ethernet.xml", @"Config\XML\RSI_Ethernet.xml" } },
                { @"Config\Comm\KUKARobot_EKI_XML\RobotClient.xml", new[] { @"Config\XML\RobotClient.xml" } },
                { @"Config\Comm\KUKARobot_EKI_XML\EKI_Connection.xml", new[] { @"Comm\KUKARobot\EKI_Connection.xml", @"Config\Comm\KUKARobot\EKI_Connection.xml", @"Config\XML\EKI_Connection.xml" } },

                // 作业数据
                { @"Config\Jobs\Job_INI\Job_001.ini", new[] { @"Config\Jobs\Job_001.ini" } },
                { @"Config\Jobs\Job_INI\JobIndex.ini", new[] { @"Config\Jobs\JobIndex.ini" } },
                { @"Config\Jobs\Job_INI\IntelligentLaserJobParams.ilini", new[] { @"Config\Jobs\IntelligentLaserJobParams.ilini" } }
            };
        }

        /// <summary>把候选旧路径中存在的文件搬到现行路径；目标</summary>
        /// <remarks>已存在时只删除旧文件。</remarks>
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
        /// <remarks>目录时保留。</remarks>
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
