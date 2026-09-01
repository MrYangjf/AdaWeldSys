using System;
using System.Runtime.InteropServices;
using System.Text;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 正运动（ZMotion）激光振镜控制高级封装
    /// 基于正运动振镜控制 Demo5 封装，提供面向业务的振镜/激光打标控制接口。
    /// 依赖 ZmotionScanLaser.dll（与 zauxdll.dll 配套），需先设置控制器句柄。
    /// 
    /// 功能覆盖：
    ///   - 激光输出口配置（开关光、使能、红光、功率 DAC）
    ///   - 振镜轴设置（X/Y 轴号）
    ///   - 矢量 / 位图打标工艺参数
    ///   - 文本样式 / 位图样式
    ///   - 图形打标（点、直线、多段线、圆、圆弧、文字、条码、位图、矢量文件）
    ///   - 填充 / 螺旋异化
    ///   - 功率模式配置（模拟量 / PWM / 输出口）
    ///   - 平台联动参数
    ///   - 加工文件生成（三次文件）
    /// </summary>
    public class ZMotionGalvo
    {
        private const string Tag = "ZMotionGalvo";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        // ===== 激光输出口配置 =====
        private int _laserAp;      // 激光开关输出口
        private int _laserMo;      // 激光使能输出口
        private int _laserRed;     // 红光输出口
        private int _laserAout;    // 功率 DAC 输出口
        private int _laserAoutLimit; // 功率 DAC 范围
        private int _scanAxisX;    // 振镜 X 轴轴号
        private int _scanAxisY;    // 振镜 Y 轴轴号

        /// <summary>振镜是否已初始化</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>振镜 X 轴轴号</summary>
        public int ScanAxisX { get { return _scanAxisX; } }

        /// <summary>振镜 Y 轴轴号</summary>
        public int ScanAxisY { get { return _scanAxisY; } }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scanAxisX">振镜 X 轴轴号（默认 4）</param>
        /// <param name="scanAxisY">振镜 Y 轴轴号（默认 5）</param>
        public ZMotionGalvo(int scanAxisX = 4, int scanAxisY = 5)
        {
            _scanAxisX = scanAxisX;
            _scanAxisY = scanAxisY;
            _laserAp = 47;
            _laserMo = 8;
            _laserRed = 48;
            _laserAout = 3;
            _laserAoutLimit = 255;
            IsInitialized = false;
        }

        /// <summary>
        /// 初始化振镜控制器
        /// 需在运动控制器连接成功后调用（设置控制器句柄）。
        /// </summary>
        /// <param name="controllerHandle">运动控制器句柄</param>
        /// <returns>是否成功</returns>
        public bool Initialize(IntPtr controllerHandle)
        {
            if (controllerHandle == IntPtr.Zero)
            {
                Log("振镜初始化失败 原因是控制器句柄为空");
                return false;
            }

            // 设置振镜轴
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetScanAxis(_scanAxisX, _scanAxisY);
            if (!CheckResult(ret, "设置振镜轴")) return false;

            // 设置激光输出口
            ret = ZMotionNativeLaser.ZmotionLaser_SetOutput(_laserAp, _laserMo, _laserRed, 0);
            if (!CheckResult(ret, "设置激光输出口")) return false;

            // 初始化功率 DAC
            ret = ZMotionNativeLaser.ZmotionLaser_AoutInit(_laserAout, _laserAoutLimit);
            if (!CheckResult(ret, "初始化功率DAC")) return false;

            IsInitialized = true;
            Log(string.Format(
                "振镜初始化完成 X轴 {0} Y轴 {1} 开关光 {2} 使能 {3} 红光 {4} 功率DAC {5}",
                _scanAxisX, _scanAxisY, _laserAp, _laserMo, _laserRed, _laserAout));
            return true;
        }

        /// <summary>
        /// 设置振镜轴号
        /// </summary>
        public bool SetScanAxis(int scanAxisX, int scanAxisY)
        {
            _scanAxisX = scanAxisX;
            _scanAxisY = scanAxisY;
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetScanAxis(_scanAxisX, _scanAxisY);
            return CheckResult(ret, "设置振镜轴");
        }

        /// <summary>
        /// 设置激光输出口
        /// </summary>
        /// <param name="laserAp">激光开关输出口</param>
        /// <param name="laserMo">激光使能输出口</param>
        /// <param name="laserRed">红光输出口</param>
        /// <param name="moDelayMs">使能延时（ms）</param>
        public bool SetLaserOutput(int laserAp, int laserMo, int laserRed, double moDelayMs = 0)
        {
            _laserAp = laserAp;
            _laserMo = laserMo;
            _laserRed = laserRed;
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetOutput(_laserAp, _laserMo, _laserRed, moDelayMs);
            return CheckResult(ret, "设置激光输出口");
        }

        /// <summary>
        /// 设置激光急停与功率锁存输出口
        /// </summary>
        /// <param name="stopOut">急停输出口（&lt;0 表示无）</param>
        /// <param name="powerLatchOut">功率锁存输出口（&lt;0 表示无）</param>
        public bool SetOtherOutDeal(int stopOut = -1, int powerLatchOut = -1)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetOtherOutDeal(stopOut, powerLatchOut);
            return CheckResult(ret, "设置额外输出口");
        }

        /// <summary>
        /// 设置功率 DAC 输出口
        /// </summary>
        public bool SetAoutInit(int aout, int limit)
        {
            _laserAout = aout;
            _laserAoutLimit = limit;
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_AoutInit(_laserAout, _laserAoutLimit);
            return CheckResult(ret, "初始化功率DAC");
        }

        /// <summary>
        /// 设置矢量打标工艺参数
        /// </summary>
        /// <param name="power">打标功率（%）</param>
        /// <param name="freq">打标频率（Hz）</param>
        /// <param name="markSpeed">打标速度（mm/s）</param>
        /// <param name="jumpSpeed">跳转速度（mm/s）</param>
        /// <param name="openDelay">开光延时（us）</param>
        /// <param name="closeDelay">关光延时（us）</param>
        /// <param name="jumpDelay">跳转延时（us）</param>
        /// <param name="corAngle">拐角开始角度（°）</param>
        /// <param name="corDelay">拐角延时（us）</param>
        /// <param name="endDelay">结束延时（us）</param>
        /// <param name="technology">打标工艺编号（1-256）</param>
        public bool SetVectPar(int power, double freq, double markSpeed, double jumpSpeed,
            double openDelay, double closeDelay, double jumpDelay, double corAngle,
            double corDelay, double endDelay, int technology = 1)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetVectPar(power, freq, markSpeed,
                jumpSpeed, openDelay, closeDelay, jumpDelay, corAngle, corDelay, endDelay, technology);
            return CheckResult(ret, "设置矢量打标参数");
        }

        /// <summary>
        /// 设置位图打标工艺参数
        /// </summary>
        public bool SetBmpPar(int power, double freq, double markSpeed, double jumpSpeed,
            double openDelay, double closeDelay, double jumpDelay, double endDelay)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetBmpPar(power, freq, markSpeed,
                jumpSpeed, openDelay, closeDelay, jumpDelay, endDelay);
            return CheckResult(ret, "设置位图打标参数");
        }

        /// <summary>
        /// 设置文本样式
        /// </summary>
        /// <param name="fontName">字体名称</param>
        /// <param name="thickness">是否加粗</param>
        /// <param name="italics">是否倾斜</param>
        /// <param name="underline">是否下划线</param>
        /// <param name="delete">是否删除线</param>
        /// <param name="height">文本高度（mm）</param>
        /// <param name="widthRatio">宽度比例 (0,100]</param>
        /// <param name="angle">旋转角度（°）</param>
        /// <param name="tilt">倾斜角度（°）</param>
        public bool SetTextStyle(string fontName, bool thickness, bool italics,
            bool underline, bool delete, double height, double widthRatio, double angle, double tilt)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetTextStyle(fontName, thickness,
                italics, underline, delete, height, widthRatio, angle, tilt);
            return CheckResult(ret, "设置文本样式");
        }

        /// <summary>
        /// 设置功率模式
        /// </summary>
        /// <param name="powerMode">0-模拟量，1-PWM，2-输出口，3-PWM+输出口，4-模拟量+输出口</param>
        public bool SetPowerMode(int powerMode)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetPowerMode(powerMode);
            return CheckResult(ret, "设置功率模式");
        }

        /// <summary>
        /// 设置功率有效范围
        /// </summary>
        public bool SetValidPowerLimit(int minPower, int maxPower)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetValidPowerLimit(minPower, maxPower);
            return CheckResult(ret, "设置功率有效范围");
        }

        /// <summary>
        /// 设置 PWM 输出口编号
        /// </summary>
        public bool SetPwmNum(int pwmNum)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetPwmNum(pwmNum);
            return CheckResult(ret, "设置PWM输出口");
        }

        /// <summary>
        /// 设置功率修改有效性
        /// </summary>
        public bool SetPowerEnable(bool valid)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetPowerEnable(valid);
            return CheckResult(ret, "设置功率有效性");
        }

        // ============================================================
        // 图形打标
        // ============================================================

        /// <summary>打点</summary>
        public bool MarkPoint(int technology, double x, double y)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_Point(technology, x, y);
            return CheckResult(ret, "打点");
        }

        /// <summary>打直线</summary>
        public bool MarkLine(int technology, double sx, double sy, double ex, double ey)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_Line(technology, sx, sy, ex, ey);
            return CheckResult(ret, "打直线");
        }

        /// <summary>
        /// 打多段线
        /// </summary>
        /// <param name="xs">X坐标数组</param>
        /// <param name="ys">Y坐标数组</param>
        /// <param name="states">状态数组（0-空移，1-打标）</param>
        /// <param name="technology">工艺编号</param>
        public bool MarkPolyline(int technology, double[] xs, double[] ys, int[] states)
        {
            if (xs == null || ys == null || states == null ||
                xs.Length == 0 || xs.Length != ys.Length || xs.Length != states.Length)
            {
                Log("打多段线失败 原因是数组参数无效");
                return false;
            }
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_Polyline(technology, xs, ys, states, xs.Length);
            return CheckResult(ret, "打多段线");
        }

        /// <summary>打圆</summary>
        public bool MarkCircle(int technology, double centerX, double centerY,
            double radius, double accuracy)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_Circle(technology, centerX, centerY,
                radius, accuracy);
            return CheckResult(ret, "打圆");
        }

        /// <summary>打圆弧</summary>
        public bool MarkArc(int technology, double centerX, double centerY, double radius,
            double startAngle, double sweepAngle, double accuracy)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_Arc(technology, centerX, centerY,
                radius, startAngle, sweepAngle, accuracy);
            return CheckResult(ret, "打圆弧");
        }

        /// <summary>打文字</summary>
        public bool MarkText(int technology, string text, double x, double y, double accuracy)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_Text(technology, text, x, y, accuracy);
            return CheckResult(ret, "打文字");
        }

        /// <summary>打条码</summary>
        public bool MarkBarCode(int technology, string text, string barcodeType,
            double x, double y, double height, double topLen, double bottomLen, double accuracy)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_BarCode(technology, text, barcodeType,
                x, y, height, topLen, bottomLen, accuracy);
            return CheckResult(ret, "打条码");
        }

        /// <summary>打位图文件</summary>
        public bool MarkBmpFile(string filePath, double x, double y, double width,
            double height, double angle)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_FileBmp(filePath, x, y, width, height, angle);
            return CheckResult(ret, "打位图文件");
        }

        /// <summary>打矢量文件（DXF）</summary>
        public bool MarkVectorFile(string filePath, double x, double y, double width,
            double height, double angle)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_FileVector(filePath, x, y, width, height, angle);
            return CheckResult(ret, "打矢量文件");
        }

        // ============================================================
        // 填充 / 螺旋异化
        // ============================================================

        /// <summary>设置填充参数</summary>
        public bool SetFillParam(bool enable, bool outline, double lineSpace, int fillNum = 1)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetFillParam(enable, outline, false,
                false, false, true, 0, 0, 0, lineSpace, 0, 0, 0, 0, fillNum);
            return CheckResult(ret, "设置填充参数");
        }

        /// <summary>填充开始</summary>
        public bool FillStart()
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_FillStart();
            return CheckResult(ret, "填充开始");
        }

        /// <summary>填充结束</summary>
        public bool FillEnd()
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_FillEnd();
            return CheckResult(ret, "填充结束");
        }

        /// <summary>线段螺旋异化开始</summary>
        public bool SpiralStart(double radius, double distance, double accuracy, bool clockwise)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SpiralStart(radius, distance, accuracy, clockwise);
            return CheckResult(ret, "螺旋异化开始");
        }

        /// <summary>线段螺旋异化结束</summary>
        public bool SpiralEnd()
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SpiralEnd();
            return CheckResult(ret, "螺旋异化结束");
        }

        /// <summary>添加用户自定义加工字符串</summary>
        public bool AddString(string processString, int type = 0)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_AddString(processString, type);
            return CheckResult(ret, "添加加工字符串");
        }

        // ============================================================
        // 加工文件生成
        // ============================================================

        /// <summary>开启三次文件生成</summary>
        public bool OpenFile3(IntPtr handle)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_OpenFile3(handle);
            return CheckResult(ret, "开启三次文件生成");
        }

        /// <summary>生成加工的三次文件</summary>
        public bool CreateFile3(string filePath)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_CreateFile3(filePath);
            return CheckResult(ret, "生成三次文件");
        }

        /// <summary>关闭三次文件生成</summary>
        public bool CloseFile3()
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_CloseFile3();
            return CheckResult(ret, "关闭三次文件生成");
        }

        /// <summary>获取加工字符串</summary>
        public string GetProcessString()
        {
            IntPtr ptr = ZMotionNativeLaser.ZmotionLaser_GetProcessString();
            return (ptr == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>获取加工结束字符串集合</summary>
        public string[] GetProEndStrings()
        {
            int row = 0;
            IntPtr ptr = ZMotionNativeLaser.ZmotionLaser_GetProEndString(ref row);
            if (ptr == IntPtr.Zero || row <= 0) return new string[0];
            return SplitAnsiLines(Marshal.PtrToStringAnsi(ptr), row);
        }

        /// <summary>清空新生成的字符串</summary>
        public bool ClearNewString()
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_ClearNewString();
            return CheckResult(ret, "清空加工字符串");
        }

        // ============================================================
        // 平台联动
        // ============================================================

        /// <summary>
        /// 设置平台参数（振镜与平台联动）
        /// </summary>
        /// <param name="axisX">平台 X 轴轴号</param>
        /// <param name="axisY">平台 Y 轴轴号</param>
        /// <param name="maxSpeed">平台最大速度</param>
        /// <param name="accel">平台加速度</param>
        /// <param name="decel">平台减速度</param>
        /// <param name="xLinkDelay">X 轴联动延时</param>
        /// <param name="yLinkDelay">Y 轴联动延时</param>
        public bool SetPlatformParam(int axisX, int axisY, double maxSpeed, double accel,
            double decel, double xLinkDelay = 0, double yLinkDelay = 0)
        {
            UInt32 ret = ZMotionNativeLaser.ZmotionLaser_SetPlatParam(axisX, axisY, maxSpeed,
                accel, decel, xLinkDelay, yLinkDelay);
            return CheckResult(ret, "设置平台参数");
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        /// <summary>检查振镜调用结果并记录日志</summary>
        private bool CheckResult(UInt32 ret, string action)
        {
            if (ZMotionNativeLaser.IsSuccess(ret))
            {
                Log(string.Format("{0} 成功", action));
                return true;
            }
            Log(string.Format(
                "{0} 失败 原因是 {1}", action,
                ZMotionNativeLaser.GetLaserErrorDescription(ret)));
            return false;
        }

        /// <summary>将 ANSI 字符串按行拆分</summary>
        private static string[] SplitAnsiLines(string input, int expectedRows)
        {
            if (string.IsNullOrEmpty(input)) return new string[0];
            string[] raw = input.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            int count = raw.Length > expectedRows ? expectedRows : raw.Length;
            string[] result = new string[count];
            Array.Copy(raw, result, count);
            return result;
        }
    }
}