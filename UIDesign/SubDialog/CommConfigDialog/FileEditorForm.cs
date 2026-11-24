using AdaWeldSystem.Comm.Communication;
using AdaWeldSystem.FileOperate;
using AntdUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using AdaWeldSystem.Comm.Communication.Implementations;
using AdaWeldSystem.Comm.SystemComm;

namespace AdaWeldSystem.UIDesign.SubDialog.CommConfigDialog
{
    /// <summary>服务器/客户端/收发 XML 文件的树形浏览与编辑</summary>
    public partial class FileEditorForm : UserControl
    {
        /// <summary>AntdUI.Table 行模型：用于展示和编辑 XML 节点的属性信息。 每行对</summary>
        class XmlPropertyRow
        {
            /// <summary>属性名（表头 Key = Name）。</summary>
            public string Name { get; set; }

            /// <summary>属性值（表头 Key = Value）。</summary>
            public string Value { get; set; }

            /// <summary>该行对应的 XML 属性名；null 表示非属性行</summary>
            public XName AttributeName { get; set; }

            /// <summary>该行是否为元素名称行。</summary>
            public bool IsElementName { get; set; }

            /// <summary>该行是否为元素文本值行。</summary>
            public bool IsElementValue { get; set; }

            /// <summary>该行是否为统计信息（只读）。</summary>
            public bool IsReadOnly { get; set; }
        }


        /// <summary>当前选中的XML元素。</summary>
        private XElement _currentElement;

        /// <summary>当前操作的XML文档读写器。</summary>
        private XdocumentReaderWriter _xmlDocumentFile;

        /// <summary>TreeItem 与 XElement 的映射字典。</summary>
        private Dictionary<TreeItem, XElement> _nodeToXml = new Dictionary<TreeItem, XElement>();

        /// <summary>当前打开的文档类型描述。</summary>
        private string _currentDocType = string.Empty;

        /// <summary>与 _xmlPropertyGrid.Binding() 绑定的属性行列表。</summary>
        private AntdUI.AntList<XmlPropertyRow> _propertyRows;

        /// <summary>初始化 <see cref="FileEditorForm"/> 类的新实例。</summary>
        public FileEditorForm()
        {
            InitializeComponent();
            InitPropertyGridTable();
        }

        /// <summary>初始化右侧属性表格的列与编辑模式（AntdUI.Table 固定范式：Columns</summary>
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

        /// <summary>递归加载XML元素到Tree节点，返回构建好的根节点。</summary>
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

        /// <summary>构建Tree节点的显示文本，包含元素名和关键属性信</summary>

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

        /// <summary>清除所有XML相关信息，包括树节点和映射字典。</summary>
        private void ClearXmlAllInfo()
        {
            _xmlTreeView.Items.Clear();
            _nodeToXml.Clear();
            _propertyRows?.Clear();
        }

        /// <summary>加载指定类型的XML文档到编辑器。</summary>
        /// <param name="doc">XML文档读写器。</param>
        /// <param name="docType">文档类型描述。</param>
        private void LoadDocument(XdocumentReaderWriter doc, string docType)
        {
            ClearXmlAllInfo();
            _xmlDocumentFile = doc;
            _currentDocType = docType;


            if (_xmlDocumentFile?._xDocument?.Root != null)
            {
                _xmlTreeView.Items.Add(LoadXmlToTree(_xmlDocumentFile._xDocument.Root));
            }

            SelectFirstNode();
        }

        /// <summary>选中树中第一个节点并加载其属性到右侧表格。</summary>
        private void SelectFirstNode()
        {
            if (_xmlTreeView.Items.Count == 0)
            {
                return;
            }

            TreeItem first = _xmlTreeView.Items[0];
            if (first != null && _nodeToXml.TryGetValue(first, out XElement firstElement))
            {
                _currentElement = firstElement;
                _xmlTreeView.SelectItem = first;
                LoadNodeToTable(_currentElement);
            }
        }

        /// <summary>刷新按钮点击事件，重新加载当前文档。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
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

        /// <summary>保存按钮点击事件，保存当前XML文档。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
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

        /// <summary>展开全部按钮点击事件。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnExpandAll_Click(object sender, EventArgs e)
        {
            _xmlTreeView.ExpandAll();
        }

        /// <summary>折叠全部按钮点击事件。</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void BtnCollapseAll_Click(object sender, EventArgs e)
        {
            _xmlTreeView.ExpandAll(false);
        }

        /// <summary>Tree节点选择变更事件，加载选中 XML 节点的属性信息到</summary>
        /// <param name="sender">发送方。</param>
        /// <param name="e">事件参数。</param>
        private void XmlTreeView_SelectChanged(object sender, TreeSelectEventArgs e)
        {
            if (e.Item == null || !_nodeToXml.TryGetValue(e.Item, out XElement element))
            {
                return;
            }

            _currentElement = element;
            LoadNodeToTable(_currentElement);
        }

        /// <summary>将选中 XML 节点的元素名称、元素值、各属性及统计信</summary>

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

        /// <summary>打开指定的XML文档（对外入口，按文档类型描述标注）。</summary>
        /// <param name="doc">XML文档读写器。</param>
        /// <param name="docType">文档类型描述。</param>
        public void OpenDocument(XdocumentReaderWriter doc, string docType)
        {
            LoadDocument(doc, docType);
        }

    }


}