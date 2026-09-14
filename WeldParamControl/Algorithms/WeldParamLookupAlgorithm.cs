using System;
using System.Collections.Generic;
using AdaWeldSystem.WeldParamControl;

namespace AdaWeldSystem.WeldParamControl.Algorithms
{
    /// <summary>
    /// 焊接参数查表调整算法实现（非单例）。
    /// </summary>
    /// <remarks>
    /// 维护焊缝宽度查找表，按焊缝宽度匹配送丝速度、激光功率、焊接速度。由 <see cref="WeldParamManager"/> 持有。
    /// 公式：FeedSpeed = 60 × KVar × SeamWidth × PlateThickness × RobotSpeed / (π/4 × WireDiameter²)；
    /// LaserPower = JVar × (π/4 × WireDiameter² × FeedSpeed/60) / MVar。
    /// </remarks>
    public class WeldParamLookupAlgorithm : Abstractions.WeldAlgorithmBase
    {
        #region 公共变量

        /// <summary>算法名称。</summary>
        public override string Name { get { return "焊接参数查表调整"; } }

        /// <summary>算法是否已就绪（查找表已生成）。</summary>
        public override bool IsReady { get { return SeamWidthList != null && SeamWidthList.Count > 0; } }

        /// <summary>焊缝宽度列表（参数查找表的键）。</summary>
        public List<double> SeamWidthList { get; set; }

        /// <summary>激光功率查找表（焊缝宽度 -> 激光功率）。</summary>
        public Dictionary<double, double> LaserPowerDic { get; set; }

        /// <summary>送丝速度查找表（焊缝宽度 -> 送丝速度）。</summary>
        public Dictionary<double, double> FeedSpeedDic { get; set; }

        /// <summary>焊接速度查找表（焊缝宽度 -> 焊接速度）。</summary>
        public Dictionary<double, double> RobotSpeedDic { get; set; }

        #endregion

        #region 构造函数

        /// <summary>构造函数：初始化空查找表。</summary>
        public WeldParamLookupAlgorithm()
        {
            SeamWidthList = new List<double>();
            LaserPowerDic = new Dictionary<double, double>();
            FeedSpeedDic = new Dictionary<double, double>();
            RobotSpeedDic = new Dictionary<double, double>();
        }

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
        }

        #endregion

        #region 公共函数

        /// <summary>载入工艺参数并重建查找表。</summary>
        /// <param name="config">配置对象，应为 <see cref="AutoParam"/></param>
        public override void LoadConfig(object config)
        {
            AutoParam autoParam = config as AutoParam;
            if (autoParam != null)
            {
                BuildLookupTable(autoParam);
            }
        }

        /// <summary>
        /// 生成参数查找表：按焊缝宽度范围与灵敏度，生成焊缝宽度与送丝速度/激光功率/焊接速度的映射字典。
        /// </summary>
        /// <param name="autoParam">自动参数配置</param>
        public void BuildLookupTable(AutoParam autoParam)
        {
            if (autoParam == null)
            {
                throw new ArgumentNullException(nameof(autoParam));
            }

            autoParam.CalVar();
            SeamWidthList = new List<double>();
            LaserPowerDic = new Dictionary<double, double>();
            FeedSpeedDic = new Dictionary<double, double>();
            RobotSpeedDic = new Dictionary<double, double>();

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

            // 与旧实现一致：初值 100，只有差值不超过 100 的档位才可能命中
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
        }

        #endregion
    }
}
