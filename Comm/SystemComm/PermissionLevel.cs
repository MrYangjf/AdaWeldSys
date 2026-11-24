using System;

namespace AdaWeldSystem.Comm.SystemComm
{
    /// <summary>权限等级职责与门控判定</summary>
    public static class PermissionLevel
    {
        /// <summary>权限等级显示名。</summary>
        /// <param name="level">操作权限等级。</param>
        /// <returns>中文显示名。</returns>
        public static string GetDisplayName(OperateLevel level)
        {
            switch (level)
            {
                case OperateLevel.Admin:
                    return "管理员";
                case OperateLevel.Engineer:
                    return "工程师";
                case OperateLevel.Operator:
                    return "操作员";
                default:
                    return "未知";
            }
        }

        /// <summary>权限等级职责说明。</summary>
        /// <param name="level">操作权限等级。</param>
        /// <returns>职责描述文本。</returns>
        public static string GetDutyText(OperateLevel level)
        {
            switch (level)
            {
                case OperateLevel.Admin:
                    return "全部页面可见可写，含用户管理";
                case OperateLevel.Engineer:
                    return "工程参数页面可见可写，不含用户管理";
                case OperateLevel.Operator:
                    return "仅基础操作页面，关键参数页不可见不可写";
                default:
                    return "权限未知";
            }
        }

        /// <summary>是否具备管理员权限。</summary>
        /// <param name="level">操作权限等级。</param>
        /// <returns>true 表示管理员。</returns>
        public static bool IsAdmin(OperateLevel level)
        {
            return level == OperateLevel.Admin;
        }

        /// <summary>是否具备工程师及以上权限。</summary>
        /// <param name="level">操作权限等级。</param>
        /// <returns>true 表示工程师或管理员。</returns>
        public static bool IsEngineerOrAbove(OperateLevel level)
        {
            return level <= OperateLevel.Engineer;
        }

        /// <summary>设置当前权限等级并广播变更事件。</summary>
        /// <param name="level">目标权限等级。</param>
        public static void SetLevel(OperateLevel level)
        {
            GlobalCommData.SetOperateLevel(level);
        }
    }
}