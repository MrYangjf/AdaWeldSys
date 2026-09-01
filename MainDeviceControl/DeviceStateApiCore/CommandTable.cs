using System;
using System.Collections.Generic;

namespace AdaWeldSystem.MainDeviceControl.DeviceState
{
    /// <summary>交互指令表（原 RobotSignals）。</summary>
    /// <remarks>机器人↔设备 的全部握手信号集中定义于此： · RobotPose   机器人状态（机器人→设备 上报，int 编码） · RobotCommand 机器人主动指令（机器人→设备，非 Pose 的主动动作） · DeviceCommand 设备→机器人 下发指令（int 编码） · SignalCode / RobotPoseCode 提供 const int 别名（线网协议裸 int 用） · InteractionTable 提供 机器人Pose→设备指令 的指令表映射 所有枚举均继承 : int，保证跨进程/线网协议传输时的类型安全与编码一致。</remarks>

    #region 机器人状态（上报）

    /// <summary>机器人状态：机器人就位后上报给整机设备的 Pose。int 编码，与 RobotPoseCode 别名一致。</summary>
    public enum RobotPose : int
    {
        Unknown = -1,
        HomePose = 0,        // 归零位
        StartSafePose = 1,   // 进入安全起始位
        PreWeldPose = 2,     // 焊前位（到位后开始 Y 轴偏移跟踪）
        WeldStartPose = 3,   // 焊接起始位（到位后切换 Working）
        PreStopPose = 4,     // 预停位
        WeldStopPose = 5,    // 焊接停止位
        StopSafePose = 6     // 回到安全停止位
    }

    #endregion

    #region 机器人主动指令（非 Pose）

    /// <summary>机器人主动指令：在 Pose 上报之外，机器人向设备发出的主动动作请求。</summary>
    public enum RobotCommand : int
    {
        None = 0,
        PreWeldRequest = 1,  // 请求进入焊前（与 StartSafePose 配合触发 Preworking）
        Abort = 2            // 急停/中止
    }

    #endregion

    #region 设备→机器人 下发指令

    /// <summary>设备→机器人 下发指令：设备驱动流程后下发给机器人的动作指令。int 编码。</summary>
    public enum DeviceCommand : int
    {
        None = 0,
        CmdGoHome = 10,        // 回零
        CmdGoStartSafe = 11,   // 去安全起始位
        CmdGoPreWeld = 12,     // 去焊前位
        CmdGoWeldStart = 13,   // 去焊接起始位
        CmdGoPreStop = 14,     // 去预停位
        CmdGoWeldStop = 15,    // 去焊接停止位
        CmdGoStopSafe = 16,    // 回安全停止位
        CmdOK = 20,            // 设备就绪/跟踪 OK（反馈机器人继续）
        CmdAbort = 30          // 急停/中止指令
    }

    #endregion

    #region const int 别名（线网协议裸 int 用）

    /// <summary>交互指令表 const int 别名（机器人 Pose 与指令的裸 int 编码，供线网协议直接取用）。</summary>
    public static class SignalCode
    {
        public const int RHomePose = 0;
        public const int RStartSafePose = 1;
        public const int RPreWeldPose = 2;
        public const int RWeldStartPose = 3;
        public const int RPreStopPose = 4;
        public const int RWeldStopPose = 5;
        public const int RStopSafePose = 6;

        public const int RPreWeldRequest = 1;
        public const int RAbort = 2;

        public const int DCmdGoHome = 10;
        public const int DCmdGoStartSafe = 11;
        public const int DCmdGoPreWeld = 12;
        public const int DCmdGoWeldStart = 13;
        public const int DCmdGoPreStop = 14;
        public const int DCmdGoWeldStop = 15;
        public const int DCmdGoStopSafe = 16;
        public const int DCmdOK = 20;
        public const int DCmdAbort = 30;
    }

    /// <summary>RobotPose 的 const int 别名（与 RobotPose 枚举值严格一致）。</summary>
    public static class RobotPoseCode
    {
        public const int HomePose = 0;
        public const int StartSafePose = 1;
        public const int PreWeldPose = 2;
        public const int WeldStartPose = 3;
        public const int PreStopPose = 4;
        public const int WeldStopPose = 5;
        public const int StopSafePose = 6;
    }

    #endregion

    #region 交互指令表映射

    /// <summary>交互指令表：机器人 Pose → 设备应下发的指令 的静态映射。</summary>
    /// <remarks>主设备收到 RobotPose 后，经此表取得对应 DeviceCommand 下发给机器人； 具体级联（如 StartSafePose+PreWeldRequest 才 Preworking）由主设备流程细化。</remarks>
    public static class InteractionTable
    {
        private static readonly Dictionary<RobotPose, DeviceCommand> PoseToCommand =
            new Dictionary<RobotPose, DeviceCommand>
            {
                { RobotPose.HomePose, DeviceCommand.CmdGoHome },
                { RobotPose.StartSafePose, DeviceCommand.CmdGoStartSafe },
                { RobotPose.PreWeldPose, DeviceCommand.CmdGoPreWeld },
                { RobotPose.WeldStartPose, DeviceCommand.CmdGoWeldStart },
                { RobotPose.PreStopPose, DeviceCommand.CmdGoPreStop },
                { RobotPose.WeldStopPose, DeviceCommand.CmdGoWeldStop },
                { RobotPose.StopSafePose, DeviceCommand.CmdGoStopSafe }
            };

        /// <summary>根据机器人 Pose 取得对应下发指令。</summary>
        /// <param name="pose">机器人上报姿态</param>
        /// <returns>对应下发指令，无映射返回 None</returns>
        public static DeviceCommand GetCommand(RobotPose pose)
        {
            return PoseToCommand.ContainsKey(pose) ? PoseToCommand[pose] : DeviceCommand.None;
        }

        /// <summary>判断 pose 是否为合法 Pose。</summary>
        /// <param name="pose">待判断的姿态</param>
        /// <returns>非 Unknown 返回 true</returns>
        public static bool IsValidPose(RobotPose pose)
        {
            return pose != RobotPose.Unknown;
        }
    }

    #endregion
}
