using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.WeldParamControl;
using AdaWeldSystem.WeldParamControl.Abstractions;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>
    /// 焊接参数查表调整算法实现（非单例）。
    /// </summary>
    /// <remarks>
    /// 维护宽度查找表，匹配送丝/功率/焊接速度。
    /// 由 <see cref="WeldParamManager"/> 持有。
    /// 公式：FeedSpeed = 60 × KVar × SeamWidth × PlateThickness × RobotSpeed / (π/4 × WireDiameter²)；
    /// LaserPower = JVar × (π/4 × WireDiameter² × FeedSpeed/60) / MVar。
    /// </remarks>
    public class WeldParamLookupAlgorithm : WeldAlgorithmBase
    {
        #region 公共变量

        /// <summary>日志标签。</summary>
        private const string Tag = "焊接参数查表";

        /// <summary>光斑宽度系数 W（默认 1.0），重建前同步。</summary>
        private double _spotWidthGain = 1.0;

        /// <summary>焊缝宽度列表（参数查找表的键）。</summary>
        public List<double> SeamWidthList { get; set; }

        /// <summary>算法名称。</summary>
        public override string Name { get { return "焊接参数查表调整"; } }
        /// <summary>激光功率查找表（焊缝宽度 -> 激光功率）。</summary>
        public Dictionary<double, double> LaserPowerDic { get; set; }

        /// <summary>送丝速度查找表（焊缝宽度 -> 送丝速度）。</summary>
        public Dictionary<double, double> FeedSpeedDic { get; set; }

        /// <summary>焊接速度查找表（焊缝宽度 -> 焊接速度）。</summary>
        public Dictionary<double, double> RobotSpeedDic { get; set; }

        /// <summary>光斑宽度查找表（焊缝宽 -> 光斑宽 = 宽度 × W）。</summary>
        public Dictionary<double, double> SpotWidthDic { get; set; }

        /// <summary>摆幅宽度查找表（焊缝宽 -> 摆幅宽）。</summary>
        /// <remarks>初值 = 光斑摆幅系数 × 焊缝宽，可手动修正。
        /// 单一配置内光斑大小不变，靠摆动适配焊缝。</remarks>
        public Dictionary<double, double> SwingWidthDic { get; set; }

        /// <summary>摆幅频率查找表（焊缝宽 -> 频率 Hz）。</summary>
        /// <remarks>初值按摆幅宽度反算，摆幅越大频率越低。
        /// 可手动核对，最终输出用查表值。</remarks>
        public Dictionary<double, int> SwingFrequencyDic { get; set; }

        /// <summary>算法是否已就绪（查找表已生成）。</summary>
        public override bool IsReady { get { return SeamWidthList != null && SeamWidthList.Count > 0; } }

        /// <summary>构造函数：初始化空查找表。</summary>
        public WeldParamLookupAlgorithm()
        {
            SeamWidthList = new List<double>();
            LaserPowerDic = new Dictionary<double, double>();
            FeedSpeedDic = new Dictionary<double, double>();
            RobotSpeedDic = new Dictionary<double, double>();
            SpotWidthDic = new Dictionary<double, double>();
            SwingWidthDic = new Dictionary<double, double>();
            SwingFrequencyDic = new Dictionary<double, int>();
        }


        #endregion

        #region 构造函数

        #endregion

        #region 私有函数

        /// <summary>向查找表追加一档参数。</summary>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="seamWidthValue">焊缝宽度档位</param>
        private void AddLookupEntry(AutoParam autoParam, double seamWidthValue)
        {
            double robotSpeed = autoParam.RobotSpeed;
            double feedSpeed = FeedSpeedCal(autoParam, robotSpeed, seamWidthValue);
            double laserPower = LaserPowerCal(autoParam, feedSpeed);
            SeamWidthList.Add(seamWidthValue);
            RobotSpeedDic.Add(seamWidthValue, robotSpeed);
            FeedSpeedDic.Add(seamWidthValue, feedSpeed);
            LaserPowerDic.Add(seamWidthValue, laserPower);
            SpotWidthDic.Add(seamWidthValue, seamWidthValue * _spotWidthGain);
            // 摆幅初值可手动修正，重建整表会覆盖
            double swingWidth = seamWidthValue * autoParam.SpotSwingGain;
            SwingWidthDic.Add(seamWidthValue, swingWidth);
            // 摆幅频率初值按摆幅宽度反算。
            // 摆幅越大频率越低，可手动核对。
            SwingFrequencyDic.Add(seamWidthValue, autoParam.CalSwingFrequency(swingWidth));
        }

        /// <summary>同步光斑宽度系数 W。</summary>
        /// <remarks>须重建查找表方生效。</remarks>
        /// <param name="gain">光斑宽度系数（须大于 0）</param>
        public void SetSpotWidthGain(double gain)
        {
            if (gain > 0.0)
            {
                _spotWidthGain = gain;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>统一日志出口。</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>载入工艺参数并重建查找表。</summary>
        /// <param name="config">配置对象，应为 <see cref="AutoParam"/></param>
        public override void LoadConfig(object config)
        {
            if (config is AutoParam autoParam)
            {
                BuildLookupTable(autoParam);
            }
        }

        /// <summary>生成参数查找表：按宽度范围与灵敏度。</summary>
        /// <remarks>生成宽度到送丝/功率/速度的映射。</remarks>
        /// <param name="autoParam">自动参数配置</param>
        public void BuildLookupTable(AutoParam autoParam)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            // 基准参数防呆：灵敏度非正或范围倒置即拒建。
            if (autoParam.Sensitivity <= 0.0 || autoParam.SeamWidthMax < autoParam.SeamWidthMin)
            {
                Log(string.Format(
                    "基准参数非法 灵敏度 {0} 宽度范围 {1}~{2} 查找表未生成",
                    autoParam.Sensitivity, autoParam.SeamWidthMin, autoParam.SeamWidthMax), MessageLevel.Warning);
                return;
            }

            autoParam.CalVar();
            SeamWidthList = new List<double>();
            LaserPowerDic = new Dictionary<double, double>();
            FeedSpeedDic = new Dictionary<double, double>();
            RobotSpeedDic = new Dictionary<double, double>();
            SpotWidthDic = new Dictionary<double, double>();
            SwingWidthDic = new Dictionary<double, double>();
            SwingFrequencyDic = new Dictionary<double, int>();

            int arrayAddCount = (int)(Math.Truncate((autoParam.SeamWidthMax - autoParam.SeamWidth) / autoParam.Sensitivity) + 2);
            int arrayInclineCount = (int)(Math.Truncate((autoParam.SeamWidth - autoParam.SeamWidthMin) / autoParam.Sensitivity) + 2);

            for (int i = 0; i < arrayAddCount; i++)
            {
                double seamWidthValue = autoParam.SeamWidth + i * autoParam.Sensitivity;
                AddLookupEntry(autoParam, seamWidthValue);
            }

            for (int i = 0; i < arrayInclineCount; i++)
            {
                if (i != 0)
                {
                    double seamWidthValue = autoParam.SeamWidth - i * autoParam.Sensitivity;
                    AddLookupEntry(autoParam, seamWidthValue);
                }
            }

            SeamWidthList.Sort();
        }

        /// <summary>按光斑宽度系数 W 重算光斑宽度列。</summary>
        /// <remarks>
        /// 只影响光斑宽度 = 焊缝宽 × W，其余三表不变。
        /// 整表重建会覆盖分段手工修正，非常规路径。
        /// </remarks>
        public void RefreshSpotWidthDic()
        {
            if (SeamWidthList == null)
            {
                return;
            }

            SpotWidthDic = new Dictionary<double, double>();
            foreach (double width in SeamWidthList)
            {
                SpotWidthDic[width] = width * _spotWidthGain;
            }
        }

        /// <summary>按焊缝宽度在查找表中匹配最近的一档参数。</summary>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="laserPower">匹配到的激光功率</param>
        /// <param name="feedSpeed">匹配到的送丝速度</param>
        /// <param name="robotSpeed">匹配到的焊接速度</param>
        /// <returns>匹配成功返回 true</returns>
        public bool TryLookup(double seamWidth, out double laserPower, out double feedSpeed, out double robotSpeed)
        {
            laserPower = 0.0;
            feedSpeed = 0.0;
            robotSpeed = 0.0;

            if (SeamWidthList == null || SeamWidthList.Count == 0)
            {
                return false;
            }

            // 与旧实现一致：容差 100，超出不命中。
            double findSeamWidth = -1;
            double diffTemp = 100;
            for (int i = 0; i < SeamWidthList.Count; i++)
            {
                double diffAbs = Math.Abs(seamWidth - SeamWidthList[i]);
                if (diffAbs <= diffTemp)
                {
                    diffTemp = diffAbs;
                    findSeamWidth = SeamWidthList[i];
                }
            }

            if (findSeamWidth == -1)
            {
                return false;
            }

            if (!FeedSpeedDic.ContainsKey(findSeamWidth)) return false;
            if (!RobotSpeedDic.ContainsKey(findSeamWidth)) return false;
            if (!LaserPowerDic.ContainsKey(findSeamWidth)) return false;

            feedSpeed = FeedSpeedDic[findSeamWidth];
            robotSpeed = RobotSpeedDic[findSeamWidth];
            laserPower = LaserPowerDic[findSeamWidth];
            return true;
        }

        /// <summary>匹配最近一档参数（含光斑宽度）。</summary>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="laserPower">匹配到的激光功率</param>
        /// <param name="feedSpeed">匹配到的送丝速度</param>
        /// <param name="robotSpeed">匹配到的焊接速度</param>
        /// <param name="spotWidth">匹配到的光斑宽度（= 焊缝宽度 × 系数 W）</param>
        /// <returns>匹配成功返回 true</returns>
        public bool TryLookupWithSpot(double seamWidth, out double laserPower, out double feedSpeed,
                                      out double robotSpeed, out double spotWidth)
        {
            spotWidth = 0.0;
            if (!TryLookup(seamWidth, out laserPower, out feedSpeed, out robotSpeed))
            {
                return false;
            }

            // 查找表键同源，命中 TryLookup 后光斑宽度表按同一键取值；缺失时按公式现算兜底
            double findSeamWidth = -1;
            double diffTemp = 100;
            for (int i = 0; i < SeamWidthList.Count; i++)
            {
                double diffAbs = Math.Abs(seamWidth - SeamWidthList[i]);
                if (diffAbs <= diffTemp)
                {
                    diffTemp = diffAbs;
                    findSeamWidth = SeamWidthList[i];
                }
            }

            if (SpotWidthDic != null && SpotWidthDic.ContainsKey(findSeamWidth))
            {
                spotWidth = SpotWidthDic[findSeamWidth];
            }
            else
            {
                spotWidth = findSeamWidth * _spotWidthGain;
            }
            return true;
        }

        /// <summary>按焊缝宽度查摆幅宽度（可手动修正）。</summary>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="swingWidth">命中的摆幅宽度（毫米）</param>
        /// <returns>命中返回 true；否则由调用方按公式兜底</returns>
        public bool TryLookupSwingWidth(double seamWidth, out double swingWidth)
        {
            swingWidth = 0.0;
            if (SwingWidthDic == null || SwingWidthDic.Count == 0 || SeamWidthList == null || SeamWidthList.Count == 0)
            {
                return false;
            }

            // 与 TryLookup 同口径：最近档匹配，容差 100
            double findSeamWidth = -1;
            double diffTemp = 100;
            for (int i = 0; i < SeamWidthList.Count; i++)
            {
                double diffAbs = Math.Abs(seamWidth - SeamWidthList[i]);
                if (diffAbs <= diffTemp)
                {
                    diffTemp = diffAbs;
                    findSeamWidth = SeamWidthList[i];
                }
            }

            if (findSeamWidth == -1)
            {
                return false;
            }

            return SwingWidthDic.TryGetValue(findSeamWidth, out swingWidth);
        }

        /// <summary>按焊缝宽度查摆幅频率（可手动核对）。</summary>
        /// <param name="seamWidth">当前焊缝宽度</param>
        /// <param name="frequencyHz">命中的摆幅频率（Hz）</param>
        /// <returns>命中返回 true；无该档记录返回 false，由调用方按基准公式兜底</returns>
        public bool TryLookupSwingFrequency(double seamWidth, out int frequencyHz)
        {
            frequencyHz = 0;
            if (SwingFrequencyDic == null || SwingFrequencyDic.Count == 0
                || SeamWidthList == null || SeamWidthList.Count == 0)
            {
                return false;
            }

            // 与 TryLookup 同口径：最近档匹配，容差 100
            double findSeamWidth = -1;
            double diffTemp = 100;
            for (int i = 0; i < SeamWidthList.Count; i++)
            {
                double diffAbs = Math.Abs(seamWidth - SeamWidthList[i]);
                if (diffAbs <= diffTemp)
                {
                    diffTemp = diffAbs;
                    findSeamWidth = SeamWidthList[i];
                }
            }

            if (findSeamWidth == -1)
            {
                return false;
            }

            return SwingFrequencyDic.TryGetValue(findSeamWidth, out frequencyHz);
        }

        /// <summary>
        /// 送丝速度计算公式：FeedSpeed = 60 × KVar × SeamWidth × PlateThickness × RobotSpeed / (π/4 × WireDiameter²)
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="robotSpeed">机器人焊接速度</param>
        /// <param name="seamWidth">焊缝宽度</param>
        /// <returns>计算得到的送丝速度</returns>
        public static double FeedSpeedCal(AutoParam autoParam, double robotSpeed, double seamWidth)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            return 60 * autoParam.KVar * seamWidth * autoParam.Platethickness * robotSpeed / ((3.14 / 4) * autoParam.WireDiameter * autoParam.WireDiameter);
        }

        /// <summary>
        /// 激光功率计算公式：LaserPower = JVar × (π/4 × WireDiameter² × FeedSpeed/60) / MVar
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        /// <param name="feedSpeed">送丝速度</param>
        /// <returns>计算得到的激光功率</returns>
        public static double LaserPowerCal(AutoParam autoParam, double feedSpeed)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            return (autoParam.JVar * (3.14 / 4) * autoParam.WireDiameter * autoParam.WireDiameter * (feedSpeed / 60)) / autoParam.MVar;
        }

        /// <summary>复位查找表。</summary>
        public override void Reset()
        {
            SeamWidthList = new List<double>();
            LaserPowerDic = new Dictionary<double, double>();
            FeedSpeedDic = new Dictionary<double, double>();
            RobotSpeedDic = new Dictionary<double, double>();
            SpotWidthDic = new Dictionary<double, double>();
            SwingWidthDic = new Dictionary<double, double>();
            SwingFrequencyDic = new Dictionary<double, int>();
        }

        #endregion
    }
}
