using AntdUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AdaWeldSystem.Sub1UI
{
    /// <summary>
    /// 统一导航页面（替代 ImagePage / ParamPage）。
    /// 通过 LoadTabs() 接受不同子页配置，实例化两次实现不同导航内容。
    /// 左侧 Menu 导航 + 右侧 Tabs 内容区（TabMenuVisible=false, Vertical 模式, BackActive=White, BackHover=Gray, Round=false）。
    /// 支持懒加载：LoadTabsLazy 接收工厂函数，Tab 首次选中时才实例化子页。
    /// </summary>
    public partial class NavPage : UserControl
    {
        private List<UserControl> _childPages = new List<UserControl>();
        // 懒加载：Tab 索引 -> 工厂函数
        private Dictionary<int, Func<UserControl>> _lazyFactories = new Dictionary<int, Func<UserControl>>();
        private bool _syncing;

        public NavPage()
        {
            InitializeComponent();
            this.Disposed += NavPage_Disposed;

            // Menu 选中 -> 切换 Tab
            uiNavMenu1.SelectChanged += (s, e) =>
            {
                if (_syncing) return;
                int idx = uiNavMenu1.Items.IndexOf(e.Value);
                if (idx >= 0)
                {
                    EnsureTabLoaded(idx);
                    _syncing = true;
                    try
                    {
                        uiTabControl1.SelectedIndex = idx;
                    }
                    finally
                    {
                        _syncing = false;
                    }
                }
            };

            // Tab 切换 -> 反写 Menu 选中
            uiTabControl1.SelectedIndexChanged += (s, e) =>
            {
                if (_syncing) return;
                int idx = uiTabControl1.SelectedIndex;
                if (idx >= 0 && idx < uiNavMenu1.Items.Count)
                {
                    EnsureTabLoaded(idx);
                    _syncing = true;
                    try
                    {
                        uiNavMenu1.SelectIndex(idx, 0, true);
                    }
                    finally
                    {
                        _syncing = false;
                    }
                }
            };
        }

        /// <summary>
        /// 加载子页配置（必须在 InitializeComponent 之后调用）。
        /// 子页立即实例化（旧版签名，保持兼容）。
        /// </summary>
        /// <param name="tabs">子页列表：(标签文本, UserControl 实例, string iconSvg)</param>
        public void LoadTabs(params (string text, UserControl control, string iconSvg)[] tabs)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                // TabPage 包装子页
                var tab = new AntdUI.TabPage { Text = tabs[i].text, Tag = i };
                tabs[i].control.Dock = DockStyle.Fill;
                tab.Controls.Add(tabs[i].control);
                uiTabControl1.AddTabSelect(tab);

                // 左侧 Menu 导航项
                uiNavMenu1.Items.Add(new AntdUI.MenuItem
                {
                    Text = tabs[i].text,
                    IconSvg = tabs[i].iconSvg
                });

                _childPages.Add(tabs[i].control);
            }

            uiTabControl1.SelectedIndex = 0;
            uiNavMenu1.SelectIndex(0, 0, true);
        }

        /// <summary>
        /// 兼容旧版签名（无图标），自动根据索引分配默认图标。
        /// 子页立即实例化（旧版签名，保持兼容）。
        /// </summary>
        public void LoadTabs(params (string text, UserControl control)[] tabs)
        {
            string[] defaultIcons = {
                "ControlOutlined", "ToolOutlined", "ApartmentOutlined", "FileOutlined",
                "PictureOutlined", "CloudOutlined", "SettingOutlined", "InfoOutlined"
            };
            for (int i = 0; i < tabs.Length; i++)
            {
                string icon = i < defaultIcons.Length ? defaultIcons[i] : "SettingOutlined";
                LoadTabsWithItems(tabs[i].text, tabs[i].control, icon, i);
            }
            uiTabControl1.SelectedIndex = 0;
            uiNavMenu1.SelectIndex(0, 0, true);
        }

        /// <summary>
        /// 懒加载版本：接收工厂函数，Tab 首次选中时才实例化子页。
        /// 有图标版本。
        /// </summary>
        /// <param name="tabs">子页列表：(标签文本, 工厂函数, 图标名)</param>
        public void LoadTabsLazy(params (string text, Func<UserControl> factory, string iconSvg)[] tabs)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                // 只创建 TabPage 占位，不实例化子页
                var tab = new AntdUI.TabPage { Text = tabs[i].text, Tag = i };
                uiTabControl1.AddTabSelect(tab);

                // 左侧 Menu 导航项
                uiNavMenu1.Items.Add(new AntdUI.MenuItem
                {
                    Text = tabs[i].text,
                    IconSvg = tabs[i].iconSvg
                });

                // 记录工厂函数，供懒加载使用
                _lazyFactories[i] = tabs[i].factory;
            }

            // 第一个 Tab 立即加载（确保有内容显示）
            if (tabs.Length > 0)
            {
                EnsureTabLoaded(0);
            }

            uiTabControl1.SelectedIndex = 0;
            uiNavMenu1.SelectIndex(0, 0, true);
        }

        /// <summary>
        /// 懒加载版本：接收工厂函数，Tab 首次选中时才实例化子页。
        /// 无图标版本，自动分配默认图标。
        /// </summary>
        public void LoadTabsLazy(params (string text, Func<UserControl> factory)[] tabs)
        {
            string[] defaultIcons = {
                "ControlOutlined", "ToolOutlined", "ApartmentOutlined", "FileOutlined",
                "PictureOutlined", "CloudOutlined", "SettingOutlined", "InfoOutlined"
            };
            var withIcons = new (string text, Func<UserControl> factory, string iconSvg)[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                string icon = i < defaultIcons.Length ? defaultIcons[i] : "SettingOutlined";
                withIcons[i] = (tabs[i].text, tabs[i].factory, icon);
            }
            LoadTabsLazy(withIcons);
        }

        /// <summary>
        /// 确保指定索引的 Tab 子页已加载（懒加载核心方法）。
        /// 仅在首次选中时调用工厂函数创建实例。
        /// </summary>
        private void EnsureTabLoaded(int index)
        {
            // 已在 _childPages 中说明已加载
            if (index < _childPages.Count && _childPages[index] != null)
                return;

            // 没有对应的工厂函数，跳过
            if (!_lazyFactories.ContainsKey(index))
                return;

            var factory = _lazyFactories[index];
            if (factory == null)
                return;

            // 调用工厂函数创建实例
            var control = factory();
            if (control == null)
                return;

            control.Dock = DockStyle.Fill;

            if (index < uiTabControl1.Pages.Count)
            {
                uiTabControl1.Pages[index].Controls.Add(control);
            }

            // 填充 _childPages 列表（确保 index 位置有效）
            while (_childPages.Count <= index)
            {
                _childPages.Add(null);
            }
            _childPages[index] = control;
        }

        private void LoadTabsWithItems(string text, UserControl control, string iconSvg, int index)
        {
            var tab = new AntdUI.TabPage { Text = text, Tag = index };
            control.Dock = DockStyle.Fill;
            tab.Controls.Add(control);
            uiTabControl1.Controls.Add(tab);
            uiTabControl1.Pages.Add(tab);

            uiNavMenu1.Items.Add(new AntdUI.MenuItem
            {
                Text = text,
                IconSvg = iconSvg
            });

            _childPages.Add(control);
        }

        private void NavPage_Disposed(object sender, EventArgs e)
        {
            foreach (var page in _childPages)
            {
                if (page != null)
                {
                    page.Dispose();
                }
            }
        }
    }
}
