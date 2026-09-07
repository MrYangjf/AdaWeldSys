using System.Collections.Generic;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>气缸注册表</summary>
    /// <remarks>
    /// 统一管理业务层用到的全部气缸。气缸由若干到位传感器与电磁阀复合而成，
    /// AttachAll 时会把每个气缸占用的全部 IO 逐个注册进控制器，避免逐个手工登记。
    /// </remarks>
    public class CylinderRegistry
    {
        #region 私有变量

        private const string Tag = "气缸注册表";

        private readonly Dictionary<string, Cylinder> _items;

        #endregion

        #region 公共变量

        /// <summary>已登记数量</summary>
        public int Count { get { return _items.Count; } }

        #endregion

        #region 构造函数

        /// <summary>创建空注册表</summary>
        public CylinderRegistry()
        {
            _items = new Dictionary<string, Cylinder>();
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

        /// <summary>登记气缸，同名覆盖</summary>
        /// <param name="name">气缸键名</param>
        /// <param name="cylinder">气缸对象</param>
        public void Register(string name, Cylinder cylinder)
        {
            if (cylinder == null || string.IsNullOrEmpty(name)) return;
            _items[name] = cylinder;
        }

        /// <summary>取气缸</summary>
        /// <param name="name">气缸键名</param>
        /// <returns>气缸对象，未找到返回 null</returns>
        public Cylinder Get(string name)
        {
            Cylinder cylinder;
            return _items.TryGetValue(name, out cylinder) ? cylinder : null;
        }

        /// <summary>判断是否已登记</summary>
        /// <param name="name">气缸键名</param>
        /// <returns>已登记返回 true</returns>
        public bool Contains(string name)
        {
            return _items.ContainsKey(name);
        }

        /// <summary>取全部气缸</summary>
        /// <returns>气缸集合</returns>
        public List<Cylinder> GetAll()
        {
            return new List<Cylinder>(_items.Values);
        }

        /// <summary>取全部气缸键名</summary>
        /// <returns>键名集合</returns>
        public List<string> GetNames()
        {
            return new List<string>(_items.Keys);
        }

        /// <summary>移除单个气缸</summary>
        /// <param name="name">气缸键名</param>
        public void Remove(string name)
        {
            _items.Remove(name);
        }

        /// <summary>清空注册表</summary>
        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>把全部气缸占用的 IO 注册进控制器</summary>
        /// <param name="control">目标控制器，为空时直接返回</param>
        public void AttachAll(MontionControl control)
        {
            if (control == null) return;

            int ioCount = 0;
            foreach (Cylinder cylinder in _items.Values)
            {
                foreach (MontionIO io in cylinder.GetAllIO())
                {
                    control.AddIO(io);
                    ioCount++;
                }
            }

            Log(string.Format("已向控制器注册气缸 {0} 个 占用 IO {1} 个", _items.Count, ioCount));
        }

        #endregion
    }
}
