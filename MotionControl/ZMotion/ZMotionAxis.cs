using System;
using System.Threading;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.API;

namespace AdaWeldSystem.MotionControl.ZMotion
{
    /// <summary>
    /// 正运动单轴实现，封装 ZMotion 控制器单轴操作，实现 IAxis 接口
    /// </summary>
    public class ZMotionAxis : IAxis
    {
        private const string Tag = "ZMotionAxis";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        private readonly IntPtr _handle;
        private readonly int _axisIndex;
        private string _axisName;
        private bool _isJogging;
        private int _jogDirection;

        /// <summary>轴名称</summary>
        public string AxisName
        {
            get { return _axisName; }
            private set { _axisName = value; }
        }

        /// <summary>轴号（接口要求 ushort，内部用 int 适配正运动 API）</summary>
        public ushort AxisNumber
        {
            get { return (ushort)_axisIndex; }
            set { /* 轴号由构造函数确定，不可修改 */ }
        }

        /// <summary>当前位置（mm），读取规划位置 Dpos</summary>
        public double CurrentPosition
        {
            get
            {
                if (_handle == IntPtr.Zero) return 0.0;
                float pos = 0;
                int ret = ZMotionNative.ZAux_Direct_GetDpos(_handle, _axisIndex, ref pos);
                if (!ZMotionNative.IsSuccess(ret))
                {
                    Log(string.Format(
                        "{0} 读取位置失败 原因是 {1}", _axisName,
                        ZMotionNative.GetErrorDescription(ret)));
                    return 0.0;
                }
                return (double)pos;
            }
        }

        /// <summary>轴是否正在运动</summary>
        public bool IsMoving
        {
            get
            {
                if (_handle == IntPtr.Zero) return false;
                int idle = 0;
                int ret = ZMotionNative.ZAux_Direct_GetIfIdle(_handle, _axisIndex, ref idle);
                if (!ZMotionNative.IsSuccess(ret)) return false;
                return idle == 0; // 0=运动中, 1=停止
            }
        }

        /// <summary>伺服是否使能</summary>
        public bool IsServoOn
        {
            get
            {
                if (_handle == IntPtr.Zero) return false;
                int enable = 0;
                int ret = ZMotionNative.ZAux_Direct_GetAxisEnable(_handle, _axisIndex, ref enable);
                if (!ZMotionNative.IsSuccess(ret)) return false;
                return enable == 1;
            }
        }

        /// <summary>轴是否已连接（通过控制器连接状态间接判断）</summary>
        public bool IsConnected
        {
            get { return _handle != IntPtr.Zero; }
        }

        /// <summary>当前速度（mm/s），读取规划速度 VpSpeed</summary>
        public double CurrentSpeed
        {
            get
            {
                if (_handle == IntPtr.Zero) return 0.0;
                float speed = 0;
                int ret = ZMotionNative.ZAux_Direct_GetVpSpeed(_handle, _axisIndex, ref speed);
                if (!ZMotionNative.IsSuccess(ret)) return 0.0;
                return (double)speed;
            }
        }

        /// <summary>停止事件</summary>
        public event EventHandler Stopped;

        /// <summary>构造函数。</summary>
        /// <param name="handle">控制器连接句柄</param>
        /// <param name="axisIndex">轴号（0-based）</param>
        /// <param name="axisName">轴名称</param>
        public ZMotionAxis(IntPtr handle, int axisIndex, string axisName)
        {
            _handle = handle;
            _axisIndex = axisIndex;
            _axisName = axisName ?? string.Format("轴{0}", axisIndex);
            _isJogging = false;
            _jogDirection = 0;
        }

        /// <summary>绝对运动（阻塞等待完成）。</summary>
        /// <param name="positionMm">目标位置 mm</param>
        /// <param name="speed">运动速度 mm/s</param>
        /// <returns>是否成功</returns>
        public bool MoveTo(double positionMm, double speed = 10.0)
        {
            if (_handle == IntPtr.Zero)
            {
                Log(string.Format("{0} MoveTo 失败 原因是未连接", _axisName));
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_SetSpeed(_handle, _axisIndex, (float)speed);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 设置速度失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            ret = ZMotionNative.ZAux_Direct_Single_MoveAbs(_handle, _axisIndex, (float)positionMm);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 绝对运动启动失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 绝对运动 目标位置毫米 {1:F3} 速度毫米每秒 {2:F1}",
                _axisName, positionMm, speed));

            // 等待运动完成
            return WaitMoveDone(0);
        }

        /// <summary>
        /// 绝对运动（非阻塞，立即返回）
        /// </summary>
        public void MoveToNoBlock(double positionMm, double speed = 10.0)
        {
            if (_handle == IntPtr.Zero)
            {
                Log(string.Format("{0} MoveToNoBlock 失败 原因是未连接", _axisName));
                return;
            }

            int ret = ZMotionNative.ZAux_Direct_SetSpeed(_handle, _axisIndex, (float)speed);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 设置速度失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return;
            }

            ret = ZMotionNative.ZAux_Direct_Single_MoveAbs(_handle, _axisIndex, (float)positionMm);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 非阻塞绝对运动失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return;
            }

            Log(string.Format(
                "{0} 非阻塞绝对运动 目标位置毫米 {1:F3}", _axisName, positionMm));
        }

        /// <summary>
        /// 相对运动（阻塞等待完成）
        /// </summary>
        public bool MoveRelative(double deltaMm, double speed = 10.0)
        {
            if (_handle == IntPtr.Zero)
            {
                Log(string.Format("{0} MoveRelative 失败 原因是未连接", _axisName));
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_SetSpeed(_handle, _axisIndex, (float)speed);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 设置速度失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            ret = ZMotionNative.ZAux_Direct_Single_Move(_handle, _axisIndex, (float)deltaMm);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 相对运动启动失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 相对运动 增量毫米 {1:F3} 速度毫米每秒 {2:F1}",
                _axisName, deltaMm, speed));

            return WaitMoveDone(0);
        }

        /// <summary>
        /// 停止轴运动
        /// </summary>
        /// <param name="immediate">是否立即停止（true=急停，false=减速停止）</param>
        public void Stop(bool immediate = false)
        {
            if (_handle == IntPtr.Zero) return;

            // 停止点动
            if (_isJogging)
            {
                _isJogging = false;
                _jogDirection = 0;
            }

            int mode = immediate ? 2 : 0; // 0=减速停止, 2=直接切断
            int ret = ZMotionNative.ZAux_Direct_Single_Cancel(_handle, _axisIndex, mode);
            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 停止（{1}）", _axisName, immediate ? "急停" : "减速停止"));
            }
            else
            {
                Log(string.Format(
                    "{0} 停止失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
            }

            var handler = Stopped;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 伺服使能
        /// </summary>
        public bool ServoOn()
        {
            if (_handle == IntPtr.Zero) return false;
            int ret = ZMotionNative.ZAux_Direct_SetAxisEnable(_handle, _axisIndex, 1);
            bool success = ZMotionNative.IsSuccess(ret);
            Log(string.Format(
                "{0} 伺服使能状态 {1}", _axisName, success ? "成功" : "失败"));
            return success;
        }

        /// <summary>
        /// 伺服关闭
        /// </summary>
        public bool ServoOff()
        {
            if (_handle == IntPtr.Zero) return false;
            int ret = ZMotionNative.ZAux_Direct_SetAxisEnable(_handle, _axisIndex, 0);
            bool success = ZMotionNative.IsSuccess(ret);
            Log(string.Format(
                "{0} 伺服关闭状态 {1}", _axisName, success ? "成功" : "失败"));
            return success;
        }

        /// <summary>
        /// 回零（使用原点回归模式0）
        /// </summary>
        public bool Home()
        {
            if (_handle == IntPtr.Zero)
            {
                Log(string.Format("{0} 回零失败 原因是未连接", _axisName));
                return false;
            }

            int ret = ZMotionNative.ZAux_Direct_Single_Home(_handle, _axisIndex, 0);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 回零启动失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format("{0} 开始回零", _axisName));

            // 等待回零完成
            bool done = WaitMoveDone(30000); // 30秒超时
            Log(string.Format(
                "{0} 回零{1}", _axisName, done ? "完成" : "超时"));
            return done;
        }

        /// <summary>
        /// 点动（连续运动）
        /// </summary>
        /// <param name="direction">方向：true=正向，false=负向</param>
        /// <param name="speed">点动速度（mm/s）</param>
        public void JogMove(bool direction, double speed = 10.0)
        {
            if (_handle == IntPtr.Zero) return;

            int dir = direction ? 1 : -1;

            // 如果已在同方向点动，不重复发送
            if (_isJogging && _jogDirection == dir) return;

            int ret = ZMotionNative.ZAux_Direct_SetSpeed(_handle, _axisIndex, (float)speed);
            if (!ZMotionNative.IsSuccess(ret)) return;

            ret = ZMotionNative.ZAux_Direct_Single_Vmove(_handle, _axisIndex, dir);
            if (ZMotionNative.IsSuccess(ret))
            {
                _isJogging = true;
                _jogDirection = dir;
                Log(string.Format(
                    "{0} 点动 方向 {1} 速度毫米每秒 {2:F1}",
                    _axisName, direction ? "正向" : "负向", speed));
            }
        }

        /// <summary>
        /// 设置当前位置为原点（设置 Dpos = 0）
        /// </summary>
        public void SetHomePosition()
        {
            if (_handle == IntPtr.Zero) return;
            int ret = ZMotionNative.ZAux_Direct_SetDpos(_handle, _axisIndex, 0);
            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format("{0} 当前位置设为零点", _axisName));
            }
        }

        /// <summary>
        /// 等待运动完成
        /// </summary>
        /// <param name="timeoutMs">超时时间（ms），0表示无限等待</param>
        /// <returns>是否完成</returns>
        public bool WaitMoveDone(int timeoutMs = 0)
        {
            if (_handle == IntPtr.Zero) return false;

            DateTime startTime = DateTime.Now;
            while (true)
            {
                int idle = 0;
                int ret = ZMotionNative.ZAux_Direct_GetIfIdle(_handle, _axisIndex, ref idle);
                if (!ZMotionNative.IsSuccess(ret))
                {
                    Thread.Sleep(10);
                    continue;
                }

                if (idle == 1) // 1=停止
                {
                    return true;
                }

                if (timeoutMs > 0)
                {
                    TimeSpan elapsed = DateTime.Now - startTime;
                    if (elapsed.TotalMilliseconds >= timeoutMs)
                    {
                        Log(string.Format(
                            "{0} 等待运动完成超时", _axisName));
                        return false;
                    }
                }

                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// 设置轴默认参数（脉冲当量、速度、加速度等）
        /// </summary>
        /// <param name="units">脉冲当量（脉冲数/mm）</param>
        /// <param name="lspeed">起始速度（mm/s）</param>
        /// <param name="speed">运行速度（mm/s）</param>
        /// <param name="accel">加速度（mm/s²）</param>
        /// <param name="decel">减速度（mm/s²）</param>
        /// <param name="sramp">S曲线时间（ms）</param>
        internal void SetDefaultParams(float units, float lspeed, float speed,
            float accel, float decel, float sramp)
        {
            if (_handle == IntPtr.Zero) return;

            ZMotionNative.ZAux_Direct_SetUnits(_handle, _axisIndex, units);
            ZMotionNative.ZAux_Direct_SetLspeed(_handle, _axisIndex, lspeed);
            ZMotionNative.ZAux_Direct_SetSpeed(_handle, _axisIndex, speed);
            ZMotionNative.ZAux_Direct_SetAccel(_handle, _axisIndex, accel);
            ZMotionNative.ZAux_Direct_SetDecel(_handle, _axisIndex, decel);
            ZMotionNative.ZAux_Direct_SetSramp(_handle, _axisIndex, sramp);

            Log(string.Format(
                "{0} 参数设置完成 单位 {1} 速度 {2} 加速度 {3}",
                _axisName, units, speed, accel));
        }

        // ============================================================
        // PTPVT 自定义曲线运动
        // ============================================================

        /// <summary>
        /// PT模式运动：多点位置+时间的绝对运动
        /// </summary>
        public bool MovePt(uint[] timeArray, double[] positionArray)
        {
            if (_handle == IntPtr.Zero)
            {
                Log(string.Format("{0} MovePt 失败 原因是未连接", _axisName));
                return false;
            }
            if (timeArray == null || positionArray == null ||
                timeArray.Length == 0 || timeArray.Length != positionArray.Length)
            {
                Log(string.Format("{0} MovePt 失败 原因是参数无效", _axisName));
                return false;
            }

            int pointCount = timeArray.Length;
            int[] axisList = new int[] { _axisIndex };
            float[] posFloat = new float[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                posFloat[i] = (float)positionArray[i];
            }

            int ret = ZMotionNative.ZAux_Direct_MultiMovePtAbs(
                _handle, pointCount, 1, axisList, timeArray, posFloat);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} PT运动启动 点数 {1}", _axisName, pointCount));
                return true;
            }

            Log(string.Format(
                "{0} PT运动启动失败 原因是 {1}", _axisName,
                ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        /// <summary>
        /// PVT模式运动：多点位置+速度+时间的绝对运动
        /// </summary>
        public bool MovePvt(uint[] timeArray, double[] positionArray, double[] speedArray)
        {
            if (_handle == IntPtr.Zero)
            {
                Log(string.Format("{0} MovePvt 失败 原因是未连接", _axisName));
                return false;
            }
            if (timeArray == null || positionArray == null || speedArray == null ||
                timeArray.Length == 0 || timeArray.Length != positionArray.Length ||
                timeArray.Length != speedArray.Length)
            {
                Log(string.Format("{0} MovePvt 失败 原因是参数无效", _axisName));
                return false;
            }

            int pointCount = timeArray.Length;
            int[] axisList = new int[] { _axisIndex };
            float[] posFloat = new float[pointCount];
            float[] spdFloat = new float[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                posFloat[i] = (float)positionArray[i];
                spdFloat[i] = (float)speedArray[i];
            }

            int ret = ZMotionNative.ZAux_Direct_MultiMovePvtAbs(
                _handle, pointCount, 1, axisList, timeArray, posFloat, spdFloat);

            if (ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} PVT运动启动 点数 {1}", _axisName, pointCount));
                return true;
            }

            Log(string.Format(
                "{0} PVT运动启动失败 原因是 {1}", _axisName,
                ZMotionNative.GetErrorDescription(ret)));
            return false;
        }

        // ============================================================
        // 高级运动指令（方案C完善）
        // ============================================================

        /// <summary>
        /// 设置轴软限位
        /// </summary>
        /// <param name="positiveLimit">正软限位（mm）</param>
        /// <param name="negativeLimit">负软限位（mm）</param>
        /// <returns>是否成功</returns>
        public bool SetSoftLimit(double positiveLimit, double negativeLimit)
        {
            if (_handle == IntPtr.Zero) return false;

            int ret = ZMotionNative.ZAux_Direct_SetFsLimit(_handle, _axisIndex, (float)positiveLimit);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 设置正软限位失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            ret = ZMotionNative.ZAux_Direct_SetRsLimit(_handle, _axisIndex, (float)negativeLimit);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 设置负软限位失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 软限位设置 正向毫米 {1:F3} 负向毫米 {2:F3}",
                _axisName, positiveLimit, negativeLimit));
            return true;
        }

        /// <summary>
        /// 软件位置比较输出（Pswitch）
        /// 轴运动到指定位置时触发输出口电平翻转。
        /// </summary>
        /// <param name="pswitchNum">比较输出编号（0-3）</param>
        /// <param name="enable">是否启用</param>
        /// <param name="outNum">输出口编号（-1 表示无输出）</param>
        /// <param name="outState">输出状态（0-关闭，1-打开）</param>
        /// <param name="setPos">正向比较位置</param>
        /// <param name="resetPos">负向复位位置</param>
        /// <returns>是否成功</returns>
        public bool SetPswitch(int pswitchNum, bool enable, int outNum, int outState,
            double setPos, double resetPos)
        {
            if (_handle == IntPtr.Zero) return false;

            int ret = ZMotionNative.ZAux_Direct_Pswitch(_handle, pswitchNum, enable ? 1 : 0,
                _axisIndex, outNum, outState, (float)setPos, (float)resetPos);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 位置比较输出失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 位置比较输出 {1} 输出口 {2} 位置 {3:F3}",
                _axisName, pswitchNum, outNum, setPos));
            return true;
        }

        /// <summary>
        /// 硬件位置比较输出（HwPswitch2）
        /// 通过 TABLE 存储多组比较点，实现高速位置触发输出。
        /// </summary>
        /// <param name="mode">模式（1-开启，2-清除）</param>
        /// <param name="opNum">输出口编号</param>
        /// <param name="opState">输出状态</param>
        /// <param name="tableStart">比较点 TABLE 起始编号</param>
        /// <param name="tableEnd">比较点 TABLE 结束编号</param>
        /// <param name="direction">比较方向</param>
        /// <returns>是否成功</returns>
        public bool SetHwPswitch2(int mode, int opNum, int opState,
            float tableStart, float tableEnd, int direction)
        {
            if (_handle == IntPtr.Zero) return false;

            int ret = ZMotionNative.ZAux_Direct_HwPswitch2(_handle, _axisIndex, mode,
                opNum, opState, tableStart, tableEnd, direction, 0);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 硬件位置比较输出失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 硬件位置比较 模式 {1} 输出口 {2} 表范围 {3} 到 {4}",
                _axisName, mode, opNum, tableStart, tableEnd));
            return true;
        }

        /// <summary>
        /// 触发编码器锁存
        /// </summary>
        /// <param name="registMode">锁存模式（0-4 为 A 锁存，10-14 为 B 锁存）</param>
        /// <returns>是否成功</returns>
        public bool Regist(int registMode)
        {
            if (_handle == IntPtr.Zero) return false;

            int ret = ZMotionNative.ZAux_Direct_Regist(_handle, _axisIndex, registMode);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 编码器锁存失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 编码器锁存触发 模式 {1}", _axisName, registMode));
            return true;
        }

        /// <summary>
        /// 读取锁存触发状态
        /// </summary>
        /// <returns>锁存位置（无锁存时返回 null）</returns>
        public double? GetRegistPosition()
        {
            if (_handle == IntPtr.Zero) return null;

            int mark = 0;
            int ret = ZMotionNative.ZAux_Direct_GetMark(_handle, _axisIndex, ref mark);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 读取锁存状态失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return null;
            }

            if (mark == 0) return null; // 尚未触发锁存

            float regPos = 0;
            ret = ZMotionNative.ZAux_Direct_GetRegPos(_handle, _axisIndex, ref regPos);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 读取锁存位置失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return null;
            }

            return (double)regPos;
        }

        /// <summary>
        /// 设置轴螺距补偿（单向）
        /// </summary>
        /// <param name="enable">是否启用</param>
        /// <param name="startPos">补偿起始位置</param>
        /// <param name="compInterval">补偿间隔</param>
        /// <param name="tableStart">补偿表起始引导地址</param>
        /// <param name="compensations">补偿距离数组</param>
        /// <returns>是否成功</returns>
        public bool SetPitchCompensation(bool enable, double startPos, double compInterval,
            uint tableStart, float[] compensations)
        {
            if (_handle == IntPtr.Zero) return false;
            if (compensations == null || compensations.Length == 0) return false;

            int ret = ZMotionNative.ZAux_Direct_Pitchset(_handle, _axisIndex, enable ? 1 : 0,
                (float)startPos, (uint)compensations.Length, (float)compInterval,
                tableStart, compensations);
            if (!ZMotionNative.IsSuccess(ret))
            {
                Log(string.Format(
                    "{0} 螺距补偿失败 原因是 {1}", _axisName,
                    ZMotionNative.GetErrorDescription(ret)));
                return false;
            }

            Log(string.Format(
                "{0} 螺距{1} 点数 {2} 间隔 {3:F3}",
                _axisName, enable ? "补偿开启" : "补偿关闭",
                compensations.Length, compInterval));
            return true;
        }

        /// <summary>
        /// 读取轴螺距补偿状态
        /// </summary>
        /// <returns>元组：是否启用 + 补偿间隔</returns>
        public Tuple<bool, double> GetPitchStatus()
        {
            if (_handle == IntPtr.Zero) return new Tuple<bool, double>(false, 0);

            int enable = 0;
            float dist = 0;
            int ret = ZMotionNative.ZAux_Direct_GetPitchStatus(_handle, _axisIndex, ref enable, ref dist);
            if (!ZMotionNative.IsSuccess(ret)) return new Tuple<bool, double>(false, 0);

            return new Tuple<bool, double>(enable == 1, (double)dist);
        }
    }
}
