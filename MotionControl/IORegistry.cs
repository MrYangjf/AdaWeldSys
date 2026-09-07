using System.Collections.Generic;
using AdaWeldSystem.Comm;
using AdaWeldSystem.MotionControl.IMotion;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>IO 注册表</summary>
    /// <remarks>
    /// 统一管理业务层用到的全部数字输入与输出，按键名登记并保证它们都已注册进控制器。
    /// 输入与输出分表存放，键名空间彼此独立，允许同名。
    /// </remarks>
    public class IORegistry
    {
        #region 私有变量

        private const string Tag = "IO 注册表";

        private readonly Dictionary<string, InputIO> _inputs;
        private readonly Dictionary<string, OutputIO> _outputs;

        #endregion

        #region 公共变量

        /// <summary>已登记输入数量</summary>
        public int InputCount { get { return _inputs.Count; } }

        /// <summary>已登记输出数量</summary>
        public int OutputCount { get { return _outputs.Count; } }

        #endregion

        #region 构造函数

        /// <summary>创建空注册表</summary>
        public IORegistry()
        {
            _inputs = new Dictionary<string, InputIO>();
            _outputs = new Dictionary<string, OutputIO>();
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

        /// <summary>登记输入，同名覆盖</summary>
        /// <param name="name">输入键名</param>
        /// <param name="io">输入对象</param>
        public void RegisterInput(string name, InputIO io)
        {
            if (io == null || string.IsNullOrEmpty(name)) return;
            _inputs[name] = io;
        }

        /// <summary>取输入</summary>
        /// <param name="name">输入键名</param>
        /// <returns>输入对象，未找到返回 null</returns>
        public InputIO GetInput(string name)
        {
            InputIO io;
            return _inputs.TryGetValue(name, out io) ? io : null;
        }

        /// <summary>取全部输入</summary>
        /// <returns>输入集合</returns>
        public List<InputIO> GetAllInputs()
        {
            return new List<InputIO>(_inputs.Values);
        }

        /// <summary>移除单个输入</summary>
        /// <param name="name">输入键名</param>
        public void RemoveInput(string name)
        {
            _inputs.Remove(name);
        }

        /// <summary>登记输出，同名覆盖</summary>
        /// <param name="name">输出键名</param>
        /// <param name="io">输出对象</param>
        public void RegisterOutput(string name, OutputIO io)
        {
            if (io == null || string.IsNullOrEmpty(name)) return;
            _outputs[name] = io;
        }

        /// <summary>取输出</summary>
        /// <param name="name">输出键名</param>
        /// <returns>输出对象，未找到返回 null</returns>
        public OutputIO GetOutput(string name)
        {
            OutputIO io;
            return _outputs.TryGetValue(name, out io) ? io : null;
        }

        /// <summary>取全部输出</summary>
        /// <returns>输出集合</returns>
        public List<OutputIO> GetAllOutputs()
        {
            return new List<OutputIO>(_outputs.Values);
        }

        /// <summary>移除单个输出</summary>
        /// <param name="name">输出键名</param>
        public void RemoveOutput(string name)
        {
            _outputs.Remove(name);
        }

        /// <summary>清空注册表</summary>
        public void Clear()
        {
            _inputs.Clear();
            _outputs.Clear();
        }

        /// <summary>把全部输入输出回灌注册进控制器</summary>
        /// <param name="control">目标控制器，为空时直接返回</param>
        public void AttachAll(MontionControl control)
        {
            if (control == null) return;

            foreach (InputIO io in _inputs.Values)
            {
                control.AddIO(io);
            }

            foreach (OutputIO io in _outputs.Values)
            {
                control.AddIO(io);
            }

            Log(string.Format("已向控制器注册输入 {0} 个 输出 {1} 个", _inputs.Count, _outputs.Count));
        }

        #endregion
    }
}
