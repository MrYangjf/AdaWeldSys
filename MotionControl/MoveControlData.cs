using System;
using AdaWeldSystem.MotionControl.DeltaEtherCAT;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.MotionControl
{
    /// <summary>运控数据：台达运控配置的持有者。</summary>
    public class MoveControlData
    {
        #region 私有变量

        private const string Tag = "运控调整";

        #endregion

        #region 公共变量

        /// <summary>台达运控配置，运控参数的持久化载体</summary>
        public DeltaMotionConfig MotionConfig { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建运控数据并载入配置</summary>
        public MoveControlData()
        {
            MotionConfig = new DeltaMotionConfig();
        }

        #endregion

        #region 私有函数

        /// <summary>输出日志</summary>
        /// <param name="message">日志内容</param>
        /// <param name="level">日志级别</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #endregion

        #region 配置持久化

        /// <summary>从 XML 加载运控配置，文件缺失时保留默认值</summary>
        public void Load()
        {
            try
            {
                MotionConfig.LoadConfig();
                Log("运控配置已加载", MessageLevel.Debug);
            }
            catch (Exception ex)
            {
                Log("运控配置加载异常 " + ex.Message, MessageLevel.Error);
            }
        }

        /// <summary>保存运控配置到 XML 文件</summary>
        public void Save()
        {
            try
            {
                MotionConfig.SaveConfig();
                Log("运控配置已保存");
            }
            catch (Exception ex)
            {
                Log("运控配置保存异常 " + ex.Message, MessageLevel.Error);
            }
        }

        #endregion
    }
}
