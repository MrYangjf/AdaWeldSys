namespace AdaWeldSystem.ConfigFileControl.Abstractions
{
    /// <summary>配置区域抽象：按 UI 分区（TabPage）读写一组配置项。</summary>
    public interface IConfigRegion
    {
        /// <summary>区域标识（UI 分区名，如「激光工艺设置」）。</summary>
        string Name { get; }

        /// <summary>区域所属的配置文件名。</summary>
        string FileName { get; }

        /// <summary>区域涉及的节名清单。</summary>
        string[] Sections { get; }

        /// <summary>从磁盘载入本区域涉及的节</summary>
        void Load();

        /// <summary>保存本区域涉及的键</summary>
        void Save();
    }
}
