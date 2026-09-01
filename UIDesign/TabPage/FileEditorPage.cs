using AdaWeldSystem.Comm;
using AdaWeldSystem.Comm.Robot.KUKARobot;
using AdaWeldSystem.FileOperate;
using AntdUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AdaWeldSystem.Sub2UI
{
    /// <summary>
    /// 文件编辑器页面，提供对服务器、客户端、发送及接收XML文件的树形浏览与属性编辑功能。
    /// </summary>
    public partial class FileEditorPage : UserControl
    {
        /// <summary>
        /// AntdUI.Table 行模型：用于展示和编辑 XML 节点的属性信息。
        /// 每行对应一个可编辑项（元素名称 / 元素值 / 某个 XML 属性 / 统计信息）。
        /// </summary>
        class XmlPropertyRow
        {
            /// <summary>
            /// 属性名（表头 Key = Name）。
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 属性值（表头 Key = Value）。
            /// </summary>
            public string Value { get; set; }

            /// <summary>
            /// 该行对应的 XML 属性名；null 表示非属性行（元素名称/元素值/统计）。
            /// </summary>
            public XName AttributeName { get; set; }

            /// <summary>
            /// 该行是否为元素名称行。
            /// </summary>
            public bool IsElementName { get; set; }

            /// <summary>
            /// 该行是否为元素文本值行。
            /// </summary>
            public bool IsElementValue { get; set; }

            /// <summary>
            /// 该行是否为统计信息（只读）。
            /// </summary>
            public bool IsReadOnly { get; set; }
        }


        /// <summary>
        /// 当前选中的XML元素。
        /// </summary>
        private XElement _currentElement;

        /// <summary>
        /// 当前操作的XML文档读写器。
        /// </summary>
        private XdocumentReaderWriter _xmlDocumentFile;

        /// <summary>
        /// TreeItem 与 XElement 的映射字典。
        /// </summary>
        private Dictionary<TreeItem, XElement> _nodeToXml = new Dictionary<TreeItem, XElement>();

        /// <summary>
        /// 当前打开的文档类型描述。
        /// </summary>
        private string _currentDocType = string.Empty;

        /// <summary>
        /// 与 _xmlPropertyGrid.Binding() 绑定的属性行列表。
        /// </summary>
        private AntdUI.AntList<XmlPropertyRow> _propertyRows;

        /// <summary>
        /// 初始化 <see cref="FileEditorPage"/> 类的新实例。
        /// </summary>
        public FileEditorPage()
        {
            InitializeComponent();

            InitConfigSelector();
            InitPropertyGridTable();
        }

        /// <summary>
        /// 初始化右侧属性表格的列与编辑模式（AntdUI.Table 固定范式：Columns 代码后置）。
        /// </summary>
        private void InitPropertyGridTable()
        {
            _xmlPropertyGrid.EditMode = AntdUI.TEditMode.DoubleClick;
            _xmlPropertyGrid.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            _xmlPropertyGrid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Name", "属性") { Width = "220", ReadOnly = true },
                new AntdUI.Column("Value", "值") { Width = "430" }
            };
            _propertyRows = new AntdUI.AntList<XmlPropertyRow>();
            _xmlPropertyGrid.Binding(_propertyRows);
        }

        /// <summary>
        /// 递归加载XML元素到Tree节点，返回构建好的根节点。
        /// </summary>
        /// <param name="element">要加载的XML元素。</param>
        /// <returns>对应的 TreeItem 节点。</returns>
        private TreeItem LoadXmlToTree(XElement element)
        {
            string nodeText = BuildNodeDisplayText(element);
            TreeItem node = new TreeItem(nodeText);
            _nodeToXml.Add(node, element);

            if (element.HasElements)
            {
                foreach (XElement child in element.Elements())
                {
                    node.Sub.Add(LoadXmlToTree(child));
                }
            }

            return node;
        }

        /// <summary>
        /// 构建Tree节点的显示文本，包含元素名和关键属性信息。
        /// </summary>
        /// <param name="element">XML元素。</param>
        /// <returns>格式化后的节点显示文本。</returns>
        private string BuildNodeDisplayText(XElement element)
        {
            string name = element.Name.ToString();
            var attributes = element.Attributes().ToList();
            if (attributes.Count == 0)
            {
                return name;
            }

            List<string> attrParts = new List<string>();
            foreach (var attr in attributes)
            {
                attrParts.Add(string.Format("{0}=\"{1}\"", attr.Name, attr.Value));
            }
            return string.Format("{0} [{1}]", name, string.Join(", ", attrParts));
        }

        /// <summary>
        /// 清除所有XML相关信息，包括树节点和映射字典。
        /// </summary>
        private void ClearXmlAllInfo()
        {
            _xmlTreeView.Items.Clear();
            _nodeToXml.Clear();
            if (_propertyRows != null)
            {
                _propertyRows.Clear();
            }
        }

        /// <summary>
        /// 加载指定类型的XML文档到编辑器。
        /// </summary>
        /// <param name="doc">XML文档读写器。</param>
        /// <param name="docType">文档类型描述。</param>
        private void LoadDocument(XdocumentReaderWriter doc, string docType)
        {
            ClearXmlAllInfo();
            _xmlDocumentFile = doc;
            _currentDocType = docType;
            _statusLine.Text = $"当前打开文档：{docType}";

            if (_xmlDocumentFile?._xDocument?.Root != null)
            {
                _xmlTreeView.Items.Add(LoadXmlToTree(_xmlDocumentFile._xDocument.Root));
            }

            SelectFirstNode();
        }

        /// <summary>
        /// 选中树中第一个节点并加载其属性到右侧表格。
        /// </summary>
        private void SelectFirstNode()
        {
            if (_xmlTreeView.Items.Count == 0)
            {
                return;
            }

            TreeItem first = _xmlTreeView.Items[0];
            if (first != null && _nodeToXml.ContainsKey(first))
            {
                _currentElement = _nodeToXml[first];
                _xmlTreeView.SelectItem = first;
                LoadNodeToTable(_currentElement);
            }
        }

        /// <summary>
        /// 刷新按钮点击事件，重新加载当前文档。
        /// </summary>
        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            if (_xmlDocumentFile == null)
            {
                AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, "请先选择一个XML文件");
                return;
            }

            switch (_currentDocType)
            {
                case "服务器配置":
                    LoadDocument(GlobalCommData.mCommunicationManager.ServerXdoc, _currentDocType);
                    break;
                case "客户端配置":
                    LoadDocument(GlobalCommData.mCommunicationManager.ClientXdoc, _currentDocType);
                    break;
                case "发送数据格式":
                    LoadDocument(GlobalCommData.mCommunicationManager.SendXdoc, _currentDocType);
                    break;
                case "接收数据格式":
                    LoadDocument(GlobalCommData.mCommunicationManager.ReciveXdoc, _currentDocType);
                    break;
                default:
                    LoadDocument(_xmlDocumentFile, _currentDocType);
                    break;
            }
        }

        /// <summary>
        /// 保存按钮点击事件，保存当前XML文档。
        /// </summary>
        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_xmlDocumentFile != null)
            {
                _xmlDocumentFile.SaveXdocument();
                AntdUI.Message.success(System.Windows.Forms.Form.ActiveForm, "保存成功");
            }
            else
            {
                AntdUI.Message.error(System.Windows.Forms.Form.ActiveForm, "未初始化文档，无法保存！");
            }
        }

        /// <summary>
        /// 展开全部按钮点击事件。
        /// </summary>
        private void BtnExpandAll_Click(object sender, EventArgs e)
        {
            _xmlTreeView.ExpandAll();
        }

        /// <summary>
        /// 折叠全部按钮点击事件。
        /// </summary>
        private void BtnCollapseAll_Click(object sender, EventArgs e)
        {
            _xmlTreeView.ExpandAll(false);
        }

        /// <summary>
        /// Tree节点选择变更事件，加载选中 XML 节点的属性信息到右侧表格。
        /// </summary>
        private void XmlTreeView_SelectChanged(object sender, TreeSelectEventArgs e)
        {
            if (e.Item == null || !_nodeToXml.ContainsKey(e.Item))
            {
                return;
            }

            _currentElement = _nodeToXml[e.Item];
            LoadNodeToTable(_currentElement);
        }

        /// <summary>
        /// 将选中 XML 节点的元素名称、元素值、各属性及统计信息加载为表格行。
        /// </summary>
        /// <param name="element">选中的 XML 元素。</param>
        private void LoadNodeToTable(XElement element)
        {
            if (_propertyRows == null)
            {
                return;
            }

            _propertyRows.Clear();

            // 元素名称行（可编辑，校验非空且不以数字开头）
            _propertyRows.Add(new XmlPropertyRow
            {
                Name = "元素名称",
                Value = element.Name.ToString(),
                IsElementName = true
            });

            // 元素文本值行（可编辑）
            _propertyRows.Add(new XmlPropertyRow
            {
                Name = "元素值",
                Value = element.Value,
                IsElementValue = true
            });

            // 每个 XML 属性一行（值可编辑）
            foreach (XAttribute attr in element.Attributes())
            {
                _propertyRows.Add(new XmlPropertyRow
                {
                    Name = attr.Name.LocalName,
                    Value = attr.Value,
                    AttributeName = attr.Name
                });
            }

            // 统计信息行（只读）
            _propertyRows.Add(new XmlPropertyRow
            {
                Name = "属性数量",
                Value = element.Attributes().Count().ToString(),
                IsReadOnly = true
            });
            _propertyRows.Add(new XmlPropertyRow
            {
                Name = "子元素数量",
                Value = element.Elements().Count().ToString(),
                IsReadOnly = true
            });
        }

        /// <summary>
        /// 属性表格单元格编辑结束事件，将编辑后的值写回 XML 元素并刷新树节点文本。
        /// </summary>
        private bool XmlTable_CellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            XmlPropertyRow row = e.Record as XmlPropertyRow;
            if (row == null || row.IsReadOnly || _currentElement == null)
            {
                return true;
            }

            string newValue = e.Value == null ? string.Empty : e.Value;
            TreeItem treeNode = FindTreeNode(_currentElement);

            if (row.AttributeName != null)
            {
                // 写回 XML 属性值
                XAttribute attr = _currentElement.Attribute(row.AttributeName);
                if (attr != null)
                {
                    attr.Value = newValue;
                }
                row.Value = newValue;
            }
            else if (row.IsElementValue)
            {
                _currentElement.Value = newValue;
                row.Value = newValue;
            }
            else if (row.IsElementName)
            {
                if (string.IsNullOrEmpty(newValue))
                {
                    AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, "元素名称不能为空");
                    return false;
                }
                if (newValue[0] >= '0' && newValue[0] <= '9')
                {
                    AntdUI.Message.warn(System.Windows.Forms.Form.ActiveForm, "元素名不能以数字开头");
                    return false;
                }
                _currentElement.Name = newValue;
                row.Value = newValue;
            }

            if (treeNode != null)
            {
                treeNode.Text = BuildNodeDisplayText(_currentElement);
            }
            return true;
        }

        /// <summary>
        /// 根据 XElement 在 _nodeToXml 映射中查找对应的树节点。
        /// </summary>
        /// <param name="element">XML 元素。</param>
        /// <returns>对应的树节点；未找到返回 null。</returns>
        private TreeItem FindTreeNode(XElement element)
        {
            foreach (KeyValuePair<TreeItem, XElement> kv in _nodeToXml)
            {
                if (ReferenceEquals(kv.Value, element))
                {
                    return kv.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// 读取服务器XML文件。
        /// </summary>
        private void ReadServerXdocument_Click(object sender, EventArgs e)
        {
            LoadDocument(GlobalCommData.mCommunicationManager.ServerXdoc, "服务器配置");
        }

        /// <summary>
        /// 读取客户端XML文件。
        /// </summary>
        private void ReadClientXdocument_Click(object sender, EventArgs e)
        {
            LoadDocument(GlobalCommData.mCommunicationManager.ClientXdoc, "客户端配置");
        }

        /// <summary>
        /// 读取发送XML格式文件。
        /// </summary>
        private void 读取发送XML格式文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadDocument(GlobalCommData.mCommunicationManager.SendXdoc, "发送数据格式");
        }

        /// <summary>
        /// 读取接收XML格式文件。
        /// </summary>
        private void 读取接受XML格式文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadDocument(GlobalCommData.mCommunicationManager.ReciveXdoc, "接收数据格式");
        }

        /// <summary>
        /// 保存XML文档（菜单项）。
        /// </summary>
        private void SaveXdocument_Click(object sender, EventArgs e)
        {
            BtnSave_Click(sender, e);
        }

        #region 配置选择下拉

        /// <summary>
        /// 初始化配置选择下拉，包含所有可编辑的通讯 XML 配置类型。
        /// </summary>
        private void InitConfigSelector()
        {
            _configSelector.Items.Clear();
            _configSelector.Items.Add("请选择XML配置");
            _configSelector.Items.Add("服务器配置");
            _configSelector.Items.Add("客户端配置");
            _configSelector.Items.Add("发送数据格式");
            _configSelector.Items.Add("接收数据格式");
            _configSelector.Items.Add("机器人RSI配置");
            _configSelector.Items.Add("机器人EKI配置");
        }

        /// <summary>
        /// 配置选择变更事件，根据所选类型加载对应 XML 文档到编辑器。
        /// </summary>
        private void ConfigSelector_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            int index = _configSelector.SelectedIndex;
            if (index < 0 || index >= _configSelector.Items.Count)
            {
                return;
            }

            string selection = _configSelector.Items[index]?.ToString();
            switch (selection)
            {
                case "服务器配置":
                    LoadDocument(GlobalCommData.mCommunicationManager.ServerXdoc, "服务器配置");
                    break;
                case "客户端配置":
                    LoadDocument(GlobalCommData.mCommunicationManager.ClientXdoc, "客户端配置");
                    break;
                case "发送数据格式":
                    LoadDocument(GlobalCommData.mCommunicationManager.SendXdoc, "发送数据格式");
                    break;
                case "接收数据格式":
                    LoadDocument(GlobalCommData.mCommunicationManager.ReciveXdoc, "接收数据格式");
                    break;
                case "机器人RSI配置":
                    LoadKUKARobotConfig(GetKUKARSIPath(), "机器人RSI配置");
                    break;
                case "机器人EKI配置":
                    LoadKUKARobotConfig(GetKUKAEKIPath(), "机器人EKI配置");
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 加载 KUKA 机器人通讯 XML 配置；文件不存在时以默认配置生成并保存。
        /// </summary>
        private void LoadKUKARobotConfig(string filePath, string docType)
        {
            try
            {
                XdocumentReaderWriter doc = new XdocumentReaderWriter();
                if (File.Exists(filePath))
                {
                    doc.LoadFromFilePath(filePath);
                }
                else
                {
                    if (docType.Contains("RSI"))
                    {
                        KUKAXMLConfig.SaveRSIConfig(filePath, new RSIConfig());
                    }
                    else
                    {
                        KUKAXMLConfig.SaveEKIConfig(filePath, new EKIConfig());
                    }
                    doc.LoadFromFilePath(filePath);
                }
                LoadXdocument(doc.GetXDocument(), docType + ": " + filePath);
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(System.Windows.Forms.Form.ActiveForm, "加载" + docType + "失败：" + ex.Message);
            }
        }

        private string GetKUKARSIPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Comm", "KUKARobot", "RSI_Ethernet.xml");
        }

        private string GetKUKAEKIPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Comm", "KUKARobot", "EKI_Connection.xml");
        }

        #endregion

        /// <summary>
        /// 加载 XDocument 到编辑器。
        /// </summary>
        private void LoadXdocument(System.Xml.Linq.XDocument doc, string docType)
        {
            ClearXmlAllInfo();
            _currentDocType = docType;
            _statusLine.Text = "当前打开文档：" + docType;

            if (doc == null || doc.Root == null)
            {
                return;
            }

            TreeItem root = new TreeItem(doc.Root.Name.LocalName);
            _xmlTreeView.Items.Add(root);
            _nodeToXml.Add(root, doc.Root);

            AddNodesRecursively(doc.Root, root);
            _xmlTreeView.ExpandAll();

            SelectFirstNode();
        }

        private void AddNodesRecursively(XElement parentElement, TreeItem parentNode)
        {
            foreach (XElement child in parentElement.Elements())
            {
                string nodeText = BuildNodeDisplayText(child);
                TreeItem childNode = new TreeItem(nodeText);
                parentNode.Sub.Add(childNode);
                _nodeToXml.Add(childNode, child);

                AddNodesRecursively(child, childNode);
            }
        }

    }


}