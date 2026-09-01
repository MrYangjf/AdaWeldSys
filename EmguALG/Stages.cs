using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AdaWeldSystem.EmguALG.EmguConfiger;
using Emgu.CV.CvEnum;

namespace AdaWeldSystem.EmguALG
{   
    /// <summary>
    /// 阶段配置抽象基类 - 提供属性变更通知与算子参数管理
    /// </summary>
    public abstract class StageBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected abstract Dictionary<string, string> GetDefaultParametersForOperation(Enum operation);

        protected void ResetParametersForOperation(Enum newOp)
        {
            Parameters = GetDefaultParametersForOperation(newOp);
            OnPropertyChanged(nameof(Parameters));
        }

        private Dictionary<string, string> _parameters = new Dictionary<string, string>();
        [Category("参数"), Description("算子参数")]
        public Dictionary<string, string> Parameters
        {
            get => _parameters;
            set
            {
                _parameters = value ?? new Dictionary<string, string>();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 滤波阶段配置 - 管理滤波算子类型与参数
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class FilterStage : StageBase
    {
        private FilterOperation _operation = FilterOperation.GaussianBlur;
        private bool _enabled = true;

        [Category("启用"), Description("是否启用滤波阶段")]
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        [Category("滤波算子"), Description("滤波算法类型")]
        public FilterOperation Operation
        {
            get => _operation;
            set
            {
                if (!_operation.Equals(value))
                {
                    _operation = value;
                    ResetParametersForOperation(value);
                    OnPropertyChanged();
                }
            }
        }

        // 根据滤波算子类型返回默认参数字典
        protected override Dictionary<string, string> GetDefaultParametersForOperation(Enum op)
        {
            var defaults = new Dictionary<string, string>();
            switch ((FilterOperation)op)
            {
                case FilterOperation.GaussianBlur:
                    defaults["ksize"] = "5";
                    defaults["sigmaX"] = "1.5";
                    break;
                case FilterOperation.MedianBlur:
                    defaults["ksize"] = "5";
                    break;
                case FilterOperation.MeanBlur:
                    defaults["ksize"] = "5";
                    break;
                case FilterOperation.BilateralFilter:
                    defaults["d"] = "9";
                    defaults["sigmaColor"] = "75";
                    defaults["sigmaSpace"] = "75";
                    break;
                case FilterOperation.Sharpen:
                    break;
            }
            return defaults;
        }

        // 深拷贝当前滤波阶段配置
        public FilterStage Clone()
        {
            return new FilterStage
            {
                Enabled = this.Enabled,
                Operation = this.Operation,
                Parameters = new Dictionary<string, string>(this.Parameters)
            };
        }
    }

    /// <summary>
    /// 形态学阶段配置 - 管理形态学算子类型与参数
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class MorphologyStage : StageBase
    {
        private MorphologyOperation _operation = MorphologyOperation.Dilate;
        private bool _enabled = false;

        [Category("启用"), Description("是否启用形态学阶段")]
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        [Category("形态学算子"), Description("形态学操作类型")]
        public MorphologyOperation Operation
        {
            get => _operation;
            set
            {
                if (!_operation.Equals(value))
                {
                    _operation = value;
                    ResetParametersForOperation(value);
                    OnPropertyChanged();
                }
            }
        }

        // 形态学算子统一返回 ksize 默认参数
        protected override Dictionary<string, string> GetDefaultParametersForOperation(Enum op)
        {
            var defaults = new Dictionary<string, string>();
            defaults["ksize"] = "3";
            return defaults;
        }

        // 深拷贝当前形态学阶段配置
        public MorphologyStage Clone()
        {
            return new MorphologyStage
            {
                Enabled = this.Enabled,
                Operation = this.Operation,
                Parameters = new Dictionary<string, string>(this.Parameters)
            };
        }
    }

    /// <summary>
    /// 边缘检测阶段配置 - 管理边缘检测算子类型与参数
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EdgeDetectionStage : StageBase
    {
        private EdgeDetectionOperation _operation = EdgeDetectionOperation.Canny;
        private bool _enabled = false;

        [Category("启用"), Description("是否启用边缘检测阶段")]
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        [Category("边缘检测算子"), Description("边缘检测算法类型")]
        public EdgeDetectionOperation Operation
        {
            get => _operation;
            set
            {
                if (!_operation.Equals(value))
                {
                    _operation = value;
                    ResetParametersForOperation(value);
                    OnPropertyChanged();
                }
            }
        }

        // 根据边缘检测算子类型返回默认参数字典
        protected override Dictionary<string, string> GetDefaultParametersForOperation(Enum op)
        {
            var defaults = new Dictionary<string, string>();
            switch ((EdgeDetectionOperation)op)
            {
                case EdgeDetectionOperation.Canny:
                    defaults["threshold1"] = "50";
                    defaults["threshold2"] = "150";
                    break;
                case EdgeDetectionOperation.Gradient:
                    defaults["ksize"] = "3";
                    break;
                case EdgeDetectionOperation.Laplacian:
                    defaults["ksize"] = "3";
                    break;
                case EdgeDetectionOperation.Roberts:
                    break;
            }
            return defaults;
        }

        // 深拷贝当前边缘检测阶段配置
        public EdgeDetectionStage Clone()
        {
            return new EdgeDetectionStage
            {
                Enabled = this.Enabled,
                Operation = this.Operation,
                Parameters = new Dictionary<string, string>(this.Parameters)
            };
        }
    }

    /// <summary>
    /// 特征提取阶段配置 - 管理特征提取算子类型与轮廓近似算法
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class FeatureExtractionStage : StageBase
    {
        private FeatureExtractionOperation _operation = FeatureExtractionOperation.ContourExtraction;
        private ChainApproxMethod _approximationMethod = ChainApproxMethod.ChainApproxTc89L1;

        [Category("特征提取算子"), Description("特征提取算法类型")]
        public FeatureExtractionOperation Operation
        {
            get => _operation;
            set
            {
                if (!_operation.Equals(value))
                {
                    _operation = value;
                    ResetParametersForOperation(value);
                    OnPropertyChanged();
                }
            }
        }

        [Category("参数"), Description("轮廓提取时的近似算法")]
        public ChainApproxMethod ApproximationMethod
        {
            get => _approximationMethod;
            set
            {
                if (_approximationMethod != value)
                {
                    _approximationMethod = value;
                    OnPropertyChanged();
                    Parameters["approximationMethod"] = value.ToString();
                }
            }
        }

        // 根据特征提取算子类型返回默认参数字典
        protected override Dictionary<string, string> GetDefaultParametersForOperation(Enum op)
        {
            var defaults = new Dictionary<string, string>();
            switch ((FeatureExtractionOperation)op)
            {
                case FeatureExtractionOperation.ContourExtraction:
                    defaults["binaryThreshold"] = "127";
                    defaults["contourThickness"] = "2";
                    defaults["approximationMethod"] = _approximationMethod.ToString();
                    break;
                case FeatureExtractionOperation.SkeletonExtraction:
                    defaults["binaryThreshold"] = "127";
                    defaults["maxIter"] = "100";
                    break;
            }
            return defaults;
        }

        // 深拷贝当前特征提取阶段配置
        public FeatureExtractionStage Clone()
        {
            return new FeatureExtractionStage
            {
                Operation = this.Operation,
                Parameters = new Dictionary<string, string>(this.Parameters),
                ApproximationMethod = this.ApproximationMethod
            };
        }
    }

    /// <summary>
    /// 模板匹配阶段配置 - 管理模板匹配算子类型与结果显示开关
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class TemplateMatchStage : StageBase
    {
        private TemplateMatchOperation _operation = TemplateMatchOperation.TemplateMatchGray;
        private bool _showResult = true;

        [Category("模板匹配算子"), Description("模板匹配算法类型")]
        public TemplateMatchOperation Operation
        {
            get => _operation;
            set
            {
                if (!_operation.Equals(value))
                {
                    _operation = value;
                    ResetParametersForOperation(value);
                    OnPropertyChanged();
                }
            }
        }

        [Category("显示"), Description("是否在匹配结果图像上绘制矩形和文字")]
        public bool ShowResult
        {
            get => _showResult;
            set { _showResult = value; OnPropertyChanged(); }
        }

        // 模板匹配算子统一返回 template 默认参数
        protected override Dictionary<string, string> GetDefaultParametersForOperation(Enum op)
        {
            var defaults = new Dictionary<string, string>();
            defaults["template"] = "template.jpg";
            return defaults;
        }

        // 深拷贝当前模板匹配阶段配置
        public TemplateMatchStage Clone()
        {
            return new TemplateMatchStage
            {
                Operation = this.Operation,
                ShowResult = this.ShowResult,
                Parameters = new Dictionary<string, string>(this.Parameters)
            };
        }
    }

    /// <summary>
    /// 流水线完整配置 - 聚合所有阶段配置并提供深拷贝
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class PipelineConfig
    {
        // 构造函数：初始化各阶段配置并写入默认参数
        public PipelineConfig()
        {
            Filter = new FilterStage();
            Morphology = new MorphologyStage();
            EdgeDetection = new EdgeDetectionStage();
            FeatureExtraction = new FeatureExtractionStage();
            TemplateMatch = new TemplateMatchStage();

            Filter.Parameters["ksize"] = "5";
            Filter.Parameters["sigmaX"] = "1.5";

            Morphology.Parameters["ksize"] = "3";

            EdgeDetection.Parameters["threshold1"] = "50";
            EdgeDetection.Parameters["threshold2"] = "150";

            FeatureExtraction.Parameters["binaryThreshold"] = "127";
            FeatureExtraction.Parameters["contourThickness"] = "2";

            TemplateMatch.Parameters["template"] = "template.jpg";
        }

        [Category("1. 滤波"), DisplayName("滤波阶段")]
        public FilterStage Filter { get; set; }

        [Category("2. 形态学"), DisplayName("形态学阶段")]
        public MorphologyStage Morphology { get; set; }

        [Category("3. 边缘检测"), DisplayName("边缘检测阶段")]
        public EdgeDetectionStage EdgeDetection { get; set; }

        [Category("4. 特征提取"), DisplayName("特征提取阶段")]
        public FeatureExtractionStage FeatureExtraction { get; set; }

        [Category("5. 模板匹配"), DisplayName("模板匹配阶段")]
        public TemplateMatchStage TemplateMatch { get; set; }

        // 深拷贝整个流水线配置
        public PipelineConfig Clone()
        {
            var clone = new PipelineConfig();
            clone.Filter = Filter?.Clone();
            clone.Morphology = Morphology?.Clone();
            clone.EdgeDetection = EdgeDetection?.Clone();
            clone.FeatureExtraction = FeatureExtraction?.Clone();
            clone.TemplateMatch = TemplateMatch?.Clone();
            return clone;
        }
    }

    public enum TemplateMatchOperation
    {
        TemplateMatchEdge,
        TemplateMatchGray,
        TemplateMatchPixel,
        TemplateMatchColor
    }
}