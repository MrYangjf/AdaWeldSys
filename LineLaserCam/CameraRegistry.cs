using System.Collections.Generic;
using AdaWeldSystem.LineLaserCam.ILineLaser;

namespace AdaWeldSystem.LineLaserCam
{
    /// <summary>线激光相机注册表</summary>
    /// <remarks>
    /// 统一管理业务层用到的全部线激光相机，按键名登记并保证同一键名只有一个实例。
    /// 登记与退订由 LineLaserManager 负责（登记即订阅事件），本表只做索引与枚举。
    /// </remarks>
    public class CameraRegistry
    {
        #region 私有变量

        private readonly Dictionary<string, LineLaserCameraBase> _items;

        #endregion

        #region 公共变量

        /// <summary>已登记数量</summary>
        public int Count { get { return _items.Count; } }

        #endregion

        #region 构造函数

        /// <summary>创建空注册表</summary>
        public CameraRegistry()
        {
            _items = new Dictionary<string, LineLaserCameraBase>();
        }

        #endregion

        #region 公共函数

        /// <summary>登记相机，同名覆盖</summary>
        /// <param name="name">相机键名</param>
        /// <param name="camera">相机对象</param>
        public void Register(string name, LineLaserCameraBase camera)
        {
            if (camera == null || string.IsNullOrEmpty(name)) return;
            _items[name] = camera;
        }

        /// <summary>取相机</summary>
        /// <param name="name">相机键名</param>
        /// <returns>相机对象，未找到返回 null</returns>
        public LineLaserCameraBase Get(string name)
        {
            LineLaserCameraBase camera;
            return _items.TryGetValue(name, out camera) ? camera : null;
        }

        /// <summary>判断是否已登记</summary>
        /// <param name="name">相机键名</param>
        /// <returns>已登记返回 true</returns>
        public bool Contains(string name)
        {
            return _items.ContainsKey(name);
        }

        /// <summary>取全部相机</summary>
        /// <returns>相机集合</returns>
        public List<LineLaserCameraBase> GetAll()
        {
            return new List<LineLaserCameraBase>(_items.Values);
        }

        /// <summary>取全部相机键名</summary>
        /// <returns>键名集合</returns>
        public List<string> GetNames()
        {
            return new List<string>(_items.Keys);
        }

        /// <summary>移除单个相机</summary>
        /// <param name="name">相机键名</param>
        public void Remove(string name)
        {
            _items.Remove(name);
        }

        /// <summary>清空注册表</summary>
        public void Clear()
        {
            _items.Clear();
        }

        #endregion
    }
}
