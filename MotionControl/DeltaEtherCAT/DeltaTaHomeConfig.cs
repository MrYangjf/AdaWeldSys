﻿using System;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>TA（送丝力矩电机）扭矩回零参数组</summary>
    /// <remarks>独立于通用 AXIS 轴模板字段，落 XML 的 ROOT/TA_HOME；
    /// 仅供 TA 轴使用，Y 轴仍读 ROOT/AXES/AXIS 原参数，互不覆盖。
    /// 模式为有符号逻辑值（-4~35），下发时按 16 位补码强转 ushort。</remarks>
    public class DeltaTaHomeConfig
    {
        /// <summary>回零模式 16#6098（有符号，-4~35；-3 为扭矩回零）</summary>
        public short HomeMode { get; set; }

        /// <summary>寻找速度 16#6099:01（rpm）</summary>
        public double HomeFirstVel { get; set; }

        /// <summary>爬行速度 16#6099:02（rpm）</summary>
        public double HomeSecondVel { get; set; }

        /// <summary>回零加速时间 16#609A（毫秒）</summary>
        public double HomeAcc { get; set; }

        /// <summary>工作零位目标位置（mm，CSP 绝对点位；≠ 回零 HomeOffset）</summary>
        /// <remarks>缺键默认 0，不覆盖现场现值；⛔ 与 Home/6098 无关。
        /// 速度与容差复用姿态位来源，⛔ 不再单独配置。</remarks>
        public double WorkOriginMm { get; set; }

        /// <summary>TA 100% 力量基准（N，默认 37；力值→CST 千分比换算的唯一基准）</summary>
        /// <remarks>100% 力量=PM46*PM49 按实测 37N 配置，⛔ 不运行时读。
        /// 缺键默认 37，不覆盖现场现值；≤0 视为未配置，TA_Auto 拒绝执行（⛔ 不按 1:1 兜底）。</remarks>
        public double Force100PctN { get; set; }

        /// <summary>TA 扭矩上限（N，默认 37 = 100% 力量基准，即不降额）</summary>
        /// <remarks>CSP 阻抗上限与 CST 最大出力共用本值。软件侧由 <see cref="DeltaMontionAxis.TorqueLimitPerMille"/> 钳制目标。
        /// 驱动侧 16#6072 在使能窗口写满量程。
        /// TA 回零确认后在线回落本值。
        /// 改本值后须回零或重新使能才作用于位置模式。
        /// ⛔ 须 ≥ 位置保持所需转矩，否则打滑、被拖走。</remarks>
        public double TorqueLimitN { get; set; }

        private int _torqueDirection = 1;

        /// <summary>TA 扭矩方向（+1 正向出力 / -1 反向出力，默认 +1）</summary>
        /// <remarks>工艺页只填力的大小；方向属出厂标定参数，与扭矩爬升速率相邻。非法值一律回落 +1。</remarks>
        public int TorqueDirection
        {
            get { return _torqueDirection; }
            set { _torqueDirection = value < 0 ? -1 : 1; }
        }

        /// <summary>读盘时方向键是否存在（仅内存，供迁移判定）</summary>
        public bool TorqueDirectionKeyPresent { get; set; }

        /// <summary>创建 TA 回零参数组并填入默认值</summary>
        public DeltaTaHomeConfig()
        {
            // TA 为扭矩回零轴，缺省即 -3；旧配置文件缺 ROOT/TA_HOME 时由 AXIS 既有值 seed，不改既有现场参数
            HomeMode = -3;
            HomeFirstVel = 20.0;
            HomeSecondVel = 5.0;
            HomeAcc = 100.0;
            // 工作零位缺省目标 0mm；速度/容差复用姿态位同一来源，不再单独配置
            WorkOriginMm = 0.0;
            // 力量基准=业主实测送丝轴峰值 37N，⛔ 不读驱动器
            Force100PctN = 37.0;
            // 扭矩上限缺省取 100% 力量基准，上线不降额，
            // 避免「默认值过低 ⇒ 推不动 / 保压打滑 / 跟随误差」。现场按实测下调，并保证 ≥ 下压力目标。
            TorqueLimitN = 37.0;
            // 扭矩方向默认正向；现场反向轴由配置键显式指定
            TorqueDirection = 1;
            TorqueDirectionKeyPresent = false;
        }

        /// <summary>从通用轴配置复制回零参数（旧文件兼容 seed 用）</summary>
        /// <param name="axis">TA 轴的原 AXIS 配置</param>
        public void CopyFrom(DeltaAxisConfig axis)
        {
            if (axis == null) return;
            HomeMode = axis.HomeMode;
            HomeFirstVel = axis.HomeFirstVel;
            HomeSecondVel = axis.HomeSecondVel;
            HomeAcc = axis.HomeAcc;
        }

        /// <summary>镜像回通用轴配置</summary>
        /// <remarks>保存前保证 AXIS 与 TA_HOME 同值，避免 CSV 导出/导入读到旧值</remarks>
        /// <param name="axis">TA 轴的原 AXIS 配置</param>
        public void CopyTo(DeltaAxisConfig axis)
        {
            if (axis == null) return;
            axis.HomeMode = HomeMode;
            axis.HomeFirstVel = HomeFirstVel;
            axis.HomeSecondVel = HomeSecondVel;
            axis.HomeAcc = HomeAcc;
        }

        /// <summary>扭矩上限换算为额定千分比</summary>
        /// <remarks>唯一换算点：16#6072 与 CST 钳制都取本结果，⛔ 勿另换。
        /// 上限 ≤0 或基准非法按不降额处理，防推不动。 与「力值→CST 千分比」同一基准，结果 1~1000</remarks>
        /// <returns>额定千分比（1 = 0.1%，1000 = 额定 / 100% 力量基准）</returns>
        public int GetTorqueLimitPerMille()
        {
            double basis = Force100PctN > 0.0 ? Force100PctN : 37.0;
            double limit = TorqueLimitN > 0.0 ? TorqueLimitN : basis;
            double perMille = Math.Round(limit / basis * 1000.0, MidpointRounding.AwayFromZero);
            if (perMille < 1.0) return 1;
            if (perMille > 1000.0) return 1000;
            return (int)perMille;
        }
    }
}
