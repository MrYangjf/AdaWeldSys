namespace AdaWeldSystem.ConfigFileControl.Abstractions
{
    /// <summary>配置载体抽象：一份配置文档（INI / XML）的读写契约。</summary>
    public interface IConfigFileProvider
    {
        /// <summary>配置文件名（含扩展名）。</summary>
        string FileName { get; }

        /// <summary>文件相对程序基目录的完整路径</summary>
        string RelativePath { get; }

        /// <summary>读取字符串值。</summary>
        /// <param name="section">节名（XML 为元素路径，INI 为节名）。</param>
        /// <param name="key">键名（XML 传 null，取整节文本）。</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>读取结果</returns>
        string Read(string section, string key, string defaultValue);

        /// <summary>写入字符串值。</summary>
        /// <param name="section">节名（XML 为元素路径，INI 为节名）。</param>
        /// <param name="key">键名（XML 为子元素名；传 null 表示整节文本）。</param>
        /// <param name="value">写入值</param>
        void Write(string section, string key, string value);

        /// <summary>节（元素）是否存在。</summary>
        /// <param name="section">节名</param>
        /// <returns>存在返回 true</returns>
        bool SectionExists(string section);

        /// <summary>键是否存在。</summary>
        /// <param name="section">节名</param>
        /// <param name="key">键名</param>
        /// <returns>存在返回 true</returns>
        bool KeyExists(string section, string key);

        /// <summary>从磁盘重新载入（丢弃内存中的未落盘改动）。</summary>
        void Reload();

        /// <summary>落盘。</summary>
        void Save();
    }
}
