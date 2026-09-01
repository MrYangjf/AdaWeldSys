using System;
using System.Runtime.InteropServices;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 正运动（ZMotion）激光振镜控制 DLL 原生 API 封装
    /// 基于官方振镜控制 Demo5（ZmotionScanLaser.dll）精简封装。
    /// 依赖 ZmotionScanLaser.dll（64位），与 zauxdll.dll 配套使用。
    /// 构建后通过 CopyNative3rdDependToOutput 目标拷贝到输出目录。
    /// </summary>
    internal static class ZMotionNativeLaser
    {
        private const string DllName = "ZmotionScanLaser.dll";

        // ============================================================
        // 基础配置
        // ============================================================

        /// <summary>
        /// 设置激光的输出口
        /// </summary>
        /// <param name="nAp">激光开关输出口</param>
        /// <param name="nMo">激光使能输出口</param>
        /// <param name="nRed">红光输出口</param>
        /// <param name="dMoDealy">使能延时</param>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetOutput",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetOutput(int nAp, int nMo, int nRed, double dMoDealy);

        /// <summary>
        /// 激光的额外输出口控制处理
        /// </summary>
        /// <param name="nStop">急停信号输出口（&lt;0 表示无）</param>
        /// <param name="nPowerLatch">功率修改锁存输出口（&lt;0 表示无）</param>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetOtherOutDeal",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetOtherOutDeal(int nStop, int nPowerLatch);

        /// <summary>
        /// 激光功率 DAC 输出初始化
        /// </summary>
        /// <param name="nAout">功率 DAC 输出口</param>
        /// <param name="nLimits">功率修改范围 0-nLimits</param>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_AoutInit",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_AoutInit(int nAout, int nLimits);

        /// <summary>
        /// 设置振镜轴号
        /// </summary>
        /// <param name="nScanX">振镜 X 轴轴号</param>
        /// <param name="nScanY">振镜 Y 轴轴号</param>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetScanAxis",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetScanAxis(int nScanX, int nScanY);

        // ============================================================
        // 工艺参数
        // ============================================================

        /// <summary>
        /// 矢量图形的打标参数设置
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetVectPar",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetVectPar(int nPower, double dFreq, double dMarkSp,
            double dJumpSp, double dOpenDelay, double dCloseDelay, double dJumpDelay,
            double dCorAngle, double dCorDelay, double dEndDelay, int nTechnology);

        /// <summary>
        /// 位图的打标参数设置
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetBmpPar",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetBmpPar(int nPower, double dFreq, double dMarkSp,
            double dJumpSp, double dOpenDelay, double dCloseDelay, double dJumpDelay, double dEndDelay);

        /// <summary>
        /// 文本状态设置
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetTextStyle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetTextStyle(string pStrFont, bool bThickness,
            bool bItalics, bool bUnderline, bool bDelete, double dHeight, double dWidthR,
            double dAngle, double dTilt);

        /// <summary>
        /// 位图状态设置
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetBmpStyle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetBmpStyle(bool bReversal, bool bGridPoint,
            double dBrightness, double dContrastRatio);

        /// <summary>
        /// 位图加工设置
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetBmpWork",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetBmpWork(double dDotTime, double dTwoWayDis,
            bool bYScan, bool bPtSpaceDis, int nBmpScanAdd, int nNoScanLowGray);

        /// <summary>
        /// 位图调整点功率
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetBmpPower",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetBmpPower(bool bAdjustPower, int nMinPower,
            int nMaxPower, int nMinGray, int nMaxGray);

        /// <summary>
        /// 设置填充参数
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetFillParam",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetFillParam(bool bEnable, bool bOutline,
            bool bOutlineFirst, bool bWhole, bool bEdge, bool bAverage, int nType, int nAngle,
            int nNum, double dLineSpace, double dMargin, double dSOffset, double dEOffset,
            double dLineIndent, int nFillNum);

        // ============================================================
        // 运动与开关光方式
        // ============================================================

        /// <summary>
        /// 设置运动字符串方式（0-运动指令，1-运动函数）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetMoveInstruct",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetMoveInstruct(int nMoveType);

        /// <summary>
        /// 设置运动函数名称
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetMoveFunName",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetMoveFunName(string pStrMoveFun);

        /// <summary>
        /// 设置开关光字符串方式
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetLightInstruct",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetLightInstruct(int nMoveType);

        /// <summary>
        /// 设置开关光字符串内容
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetLightString",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetLightString(string pStrOpen, string pStrClose);

        // ============================================================
        // 功率控制
        // ============================================================

        /// <summary>
        /// 设置功率修改有效性
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetPowerEnable",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetPowerEnable(bool bValid);

        /// <summary>
        /// 设置功率模式（0-模拟量，1-PWM，2-输出口，3-PWM+输出口，4-模拟量+输出口）
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetPowerMode",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetPowerMode(int nPowerMode);

        /// <summary>
        /// 设置功率有效范围
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetValidPowerLimit",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetValidPowerLimit(int nMinPower, int nMaxPower);

        /// <summary>
        /// 设置 PWM 输出口编号
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetPwmNum",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetPwmNum(int nPwmNum);

        // ============================================================
        // 图形打标
        // ============================================================

        /// <summary>打点</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_Point",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_Point(int nTechnology, double dX, double dY);

        /// <summary>打直线</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_Line",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_Line(int nTechnology, double dSx, double dSy,
            double dEx, double dEy);

        /// <summary>打多段线（pState: 0-空移，1-打标）</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_Polyline",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_Polyline(int nTechnology, double[] pdx,
            double[] pdy, int[] pState, int nLineNum);

        /// <summary>打圆</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_Circle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_Circle(int nTechnology, double dCenterX,
            double dCenterY, double dRadius, double dAccuracy);

        /// <summary>打圆弧</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_Arc",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_Arc(int nTechnology, double dCenterX,
            double dCenterY, double dRadius, double dStartAngle, double dSweepAngle, double dAccuracy);

        /// <summary>打文字</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_Text",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_Text(int nTechnology, string pStrText,
            double dX, double dY, double dAccuracy);

        /// <summary>打条码</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_BarCode",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_BarCode(int nTechnology, string pStrText,
            string pStrType, double dX, double dY, double dHeight, double dTopLen,
            double dBottomLen, double dAccuracy);

        /// <summary>打位图文件</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_FileBmp",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_FileBmp(string strFilePath, double dX, double dY,
            double dWidth, double dHeight, double dAngle);

        /// <summary>打矢量文件（DXF）</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_FileVector",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_FileVector(string strFilePath, double dX,
            double dY, double dWidth, double dHeight, double dAngle);

        // ============================================================
        // 填充 / 螺旋异化
        // ============================================================

        /// <summary>填充开始</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_FillStart",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_FillStart();

        /// <summary>填充结束</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_FillEnd",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_FillEnd();

        /// <summary>添加用户自定义加工字符串</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_AddString",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_AddString(string pString, int nType);

        /// <summary>线段螺旋异化开始</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SpiralStart",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SpiralStart(double dRadio, double dDis,
            double dAccuracy, bool bClockwise);

        /// <summary>线段螺旋异化结束</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SpiralEnd",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SpiralEnd();

        // ============================================================
        // 加工文件生成
        // ============================================================

        /// <summary>开启三次文件生成</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_OpenFile3",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_OpenFile3(IntPtr handle);

        /// <summary>生成加工的三次文件</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_CreateFile3",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_CreateFile3(string strFilePath);

        /// <summary>关闭三次文件生成</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_CloseFile3",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_CloseFile3();

        /// <summary>获取加工字符串</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_GetProcessString",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ZmotionLaser_GetProcessString();

        /// <summary>获取加工结束字符串（行数）</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_GetProEndString",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ZmotionLaser_GetProEndString(ref int nRow);

        /// <summary>清空新生成的字符串</summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_ClearNewString",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_ClearNewString();

        // ============================================================
        // 平台联动
        // ============================================================

        /// <summary>
        /// 设置平台参数
        /// </summary>
        [DllImport(DllName, EntryPoint = "ZmotionLaser_SetPlatParam",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 ZmotionLaser_SetPlatParam(int nAxisX, int nAxisY,
            double dSpeed, double dAccel, double dDecel, double dXLinkDelay, double dYLinkDelay);

        // ============================================================
        // 辅助方法
        // ============================================================

        /// <summary>检查返回值是否为成功（0 = 成功）</summary>
        public static bool IsSuccess(UInt32 ret)
        {
            return ret == 0;
        }

        /// <summary>获取错误码描述（振镜库错误码从 11000 起）</summary>
        public static string GetLaserErrorDescription(UInt32 errorCode)
        {
            switch (errorCode)
            {
                case 0: return "成功";
                case 11000: return "参数为空";
                case 11001: return "参数无效";
                case 11002: return "文本长度太长";
                case 11003: return "未开启加工";
                case 11004: return "不存在的工艺";
                case 11005: return "系统库没有的字体";
                case 11006: return "不能填充";
                case 11007: return "位图文件错误";
                case 11008: return "设置激光功率输出口错误";
                case 11009: return "输出口号重叠";
                case 11010: return "文件无效(路径错误/文件无效)";
                default: return string.Format("未知振镜错误码 {0}", errorCode);
            }
        }
    }
}