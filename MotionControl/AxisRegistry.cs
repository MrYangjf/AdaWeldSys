using System.Collections.Generic;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>轴注册表</summary>
    /// <remarks>
    /// 统一管理业务层用到的全部轴，按键名登记并保证它们都已注册进控制器。
    /// 轴对象在构造时已向所属控制器自注册，AttachAll 再做一次幂等回灌，
    /// 用于校验注册表与控制器集合一致，避免遗漏或重复。
    /// </remarks>
    public class AxisRegistry
    {
        #region 私有变量

        private const string Tag = "轴注册表";

        private readonly Dictionary<string, MontionAxis> _items;

        #endregion

        #region 公共变量

        /// <summary>已登记数量</summary>
        public int Count { get { return _items.Count; } }

        #endregion

        #region 构造函数

        /// <summary>创建空注册表</summary>
        public AxisRegistry()
        {
            _items = new Dictionary<string, MontionAxis>();
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private static void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #endregion

        #region 公共函数

        /// <summary>登记轴，同名覆盖</summary>
        /// <param name="name">轴键名</param>
        /// <param name="axis">轴对象</param>
        public void Register(string name, MontionAxis axis)
        {
            if (axis == null || string.IsNullOrEmpty(name)) return;
            _items[name] = axis;
        }

        /// <summary>取轴</summary>
        /// <param name="name">轴键名</param>
        /// <returns>轴对象，未找到返回 null</returns>
        public MontionAxis Get(string name)
        {
            MontionAxis axis;
            return _items.TryGetValue(name, out axis) ? axis : null;
        }

        /// <summary>判断是否已登记</summary>
        /// <param name="name">轴键名</param>
        /// <returns>已登记返回 true</returns>
        public bool Contains(string name)
        {
            return _items.ContainsKey(name);
        }

        /// <summary>取全部轴</summary>
        /// <returns>轴集合</returns>
        public List<MontionAxis> GetAll()
        {
            return new List<MontionAxis>(_items.Values);
        }

        /// <summary>取全部轴键名</summary>
        /// <returns>键名集合</returns>
        public List<string> GetNames()
        {
            return new List<string>(_items.Keys);
        }

        /// <summary>移除单个轴</summary>
        /// <param name="name">轴键名</param>
        public void Remove(string name)
        {
            _items.Remove(name);
        }

        /// <summary>清空注册表</summary>
        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>把全部轴回灌注册进控制器</summary>
        /// <param name="control">目标控制器，为空时直接返回</param>
        public void AttachAll(MontionControl control)
        {
            if (control == null) return;

            foreach (MontionAxis axis in _items.Values)
            {
                control.AddAxis(axis);
            }
            Log(string.Format("已向控制器注册轴 {0} 个", _items.Count));
        }

        #endregion
    }
}
