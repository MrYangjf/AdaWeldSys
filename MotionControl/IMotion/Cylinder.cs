using System.Collections.Generic;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.MotionControl.IMotion
{
    /// <summary>气缸</summary>
    /// <remarks>
    /// 气缸是把若干输入（到位传感器）与若干输出（电磁阀）捆绑成一个动作的复合对象，
    /// 依赖输入输出抽象，与厂商无关；传感器缺失时按到位处理。
    /// </remarks>
    public class Cylinder
    {
        #region 私有变量

        private readonly string _name;

        #endregion

        #region 公共变量

        /// <summary>名称</summary>
        public string Name { get { return _name; } }

        /// <summary>伸出位传感器</summary>
        public InputIO InStretchSensor { get; set; }

        /// <summary>缩回位传感器</summary>
        public InputIO InRetractSensor { get; set; }

        /// <summary>中间位传感器</summary>
        public InputIO InCenterSensor { get; set; }

        /// <summary>伸出输出</summary>
        public OutputIO OutCylinderStretch { get; set; }

        /// <summary>缩回输出</summary>
        public OutputIO OutCylinderRetract { get; set; }

        /// <summary>伸出动作名</summary>
        public string StretchName { get; set; }

        /// <summary>缩回动作名</summary>
        public string RetractName { get; set; }

        #endregion

        #region 构造函数

        private Cylinder(string name)
        {
            _name = name;
            StretchName = "伸出";
            RetractName = "缩回";
        }

        #endregion

        #region 公共函数

        /// <summary>创建气缸</summary>
        /// <param name="name">气缸名称</param>
        /// <returns>气缸对象</returns>
        public static Cylinder Builder(string name)
        {
            return new Cylinder(name);
        }

        /// <summary>配置伸出位传感器</summary>
        /// <param name="inputIO">输入点</param>
        /// <returns>气缸自身，用于链式配置</returns>
        public Cylinder SetInStretchSensor(InputIO inputIO)
        {
            InStretchSensor = inputIO;
            return this;
        }

        /// <summary>配置缩回位传感器</summary>
        /// <param name="inputIO">输入点</param>
        /// <returns>气缸自身，用于链式配置</returns>
        public Cylinder SetInRetractSensor(InputIO inputIO)
        {
            InRetractSensor = inputIO;
            return this;
        }

        /// <summary>配置中间位传感器</summary>
        /// <param name="inputIO">输入点</param>
        /// <returns>气缸自身，用于链式配置</returns>
        public Cylinder SetInCenterSensor(InputIO inputIO)
        {
            InCenterSensor = inputIO;
            return this;
        }

        /// <summary>配置伸出输出</summary>
        /// <param name="outputIO">输出点</param>
        /// <param name="showName">动作显示名，为空时保留默认</param>
        /// <returns>气缸自身，用于链式配置</returns>
        public Cylinder SetOutCylinderStretch(OutputIO outputIO, string showName = null)
        {
            OutCylinderStretch = outputIO;
            if (!string.IsNullOrEmpty(showName)) StretchName = showName;
            return this;
        }

        /// <summary>配置缩回输出</summary>
        /// <param name="outputIO">输出点</param>
        /// <param name="showName">动作显示名，为空时保留默认</param>
        /// <returns>气缸自身，用于链式配置</returns>
        public Cylinder SetOutCylinderRetract(OutputIO outputIO, string showName = null)
        {
            OutCylinderRetract = outputIO;
            if (!string.IsNullOrEmpty(showName)) RetractName = showName;
            return this;
        }

        /// <summary>判断是否处于伸出位</summary>
        /// <returns>伸出到位返回 true</returns>
        public bool IsInStretch()
        {
            return InStretchSensor == null || InStretchSensor.IsON();
        }

        /// <summary>判断是否处于缩回位</summary>
        /// <returns>缩回到位返回 true</returns>
        public bool IsInRetract()
        {
            return InRetractSensor == null || InRetractSensor.IsON();
        }

        /// <summary>判断是否处于中间位</summary>
        /// <returns>中间到位返回 true</returns>
        public bool IsInCenter()
        {
            return InCenterSensor == null || InCenterSensor.IsON();
        }

        /// <summary>执行伸出</summary>
        public void Stretch()
        {
            if (OutCylinderRetract != null) OutCylinderRetract.SetOFF();
            if (OutCylinderStretch != null) OutCylinderStretch.SetON();
            GlobalCommData.ShowLog(_name, StretchName + " 已输出", MessageLevel.Info);
        }

        /// <summary>执行缩回</summary>
        public void Retract()
        {
            if (OutCylinderStretch != null) OutCylinderStretch.SetOFF();
            if (OutCylinderRetract != null) OutCylinderRetract.SetON();
            GlobalCommData.ShowLog(_name, RetractName + " 已输出", MessageLevel.Info);
        }

        /// <summary>枚举本气缸占用的全部 IO</summary>
        /// <returns>输入与输出混合序列，未配置的点位不出现在序列中</returns>
        public IEnumerable<MontionIO> GetAllIO()
        {
            if (InStretchSensor != null) yield return InStretchSensor;
            if (InRetractSensor != null) yield return InRetractSensor;
            if (InCenterSensor != null) yield return InCenterSensor;
            if (OutCylinderStretch != null) yield return OutCylinderStretch;
            if (OutCylinderRetract != null) yield return OutCylinderRetract;
        }

        #endregion
    }
}
