using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Drawing;
using System.Windows.Forms;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.Comm;

namespace AdaWeldSystem.EmguALG
{
    /// <summary>
    /// 图像算法窗口ROI工具
    /// 负责算法窗口的ROI绘制与管理，使用FileOperate.INIFile保存和加载
    /// </summary>
    public class ImageRoiTools
    {
        private const string Tag = "ImageRoiTools";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        #region 字段与属性

        private Rectangle _roiRectangle;
        private bool _isDrawing;
        private Point _startPoint;
        private readonly PictureBox _pictureBox;

        /// <summary>ROI配置INI文件固定路径</summary>
        private static readonly string DefaultIniFilePath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "INI", "ImageRoi.ini");

        /// <summary>ROI配置INI固定节名</summary>
        private const string DefaultIniSection = "ImageROI";

        /// <summary>ROI矩形区域</summary>
        public Rectangle RoiRectangle => _roiRectangle;

        /// <summary>是否正在绘制ROI</summary>
        public bool IsDrawing => _isDrawing;

        /// <summary>ROI是否有效</summary>
        public bool HasRoi => _roiRectangle.Width > 0 && _roiRectangle.Height > 0;

        /// <summary>ROI颜色</summary>
        public Color RoiColor { get; set; }
        /// <summary>ROI变更事件</summary>
        public event EventHandler RoiChanged;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建ROI工具实例，绑定PictureBox控件
        /// INI文件路径和节名在类内部固定，无需外部传入
        /// </summary>
        /// <param name="pictureBox">目标PictureBox控件</param>
        public ImageRoiTools(PictureBox pictureBox)
        {
            RoiColor = Color.Red;
            _pictureBox = pictureBox ?? throw new ArgumentNullException(nameof(pictureBox));

            // 绑定鼠标事件
            _pictureBox.MouseDown += PictureBox_MouseDown;
            _pictureBox.MouseMove += PictureBox_MouseMove;
            _pictureBox.MouseUp += PictureBox_MouseUp;
            _pictureBox.Paint += PictureBox_Paint;

            // 加载已保存的ROI
            LoadRoiFromIni();
        }

        #endregion

        #region ROI绘制事件处理

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDrawing = true;
                _startPoint = e.Location;
                _roiRectangle = new Rectangle(e.Location, Size.Empty);
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing)
            {
                int x = Math.Min(_startPoint.X, e.X);
                int y = Math.Min(_startPoint.Y, e.Y);
                int width = Math.Abs(e.X - _startPoint.X);
                int height = Math.Abs(e.Y - _startPoint.Y);
                _roiRectangle = new Rectangle(x, y, width, height);
                _pictureBox.Invalidate();
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDrawing)
            {
                _isDrawing = false;
                if (_roiRectangle.Width > 5 && _roiRectangle.Height > 5)
                {
                    SaveRoiToIni();
                    RoiChanged?.Invoke(this, EventArgs.Empty);
                }
                _pictureBox.Invalidate();
            }
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (HasRoi)
            {
                using (Pen pen = new Pen(RoiColor, 2))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, _roiRectangle);
                }

                // 绘制ROI信息
                string info = $"ROI 起点 X {_roiRectangle.X} Y {_roiRectangle.Y} 宽 {_roiRectangle.Width} 高 {_roiRectangle.Height}";
                using (Font font = new Font("Microsoft YaHei", 9))
                using (Brush brush = new SolidBrush(RoiColor))
                {
                    e.Graphics.DrawString(info, font, brush, _roiRectangle.X, _roiRectangle.Y - 20);
                }
            }
        }

        #endregion

        #region ROI操作

        /// <summary>
        /// 清除当前ROI
        /// </summary>
        public void ClearRoi()
        {
            _roiRectangle = Rectangle.Empty;
            _pictureBox.Invalidate();
            SaveRoiToIni();
        }

        /// <summary>
        /// 设置ROI矩形
        /// </summary>
        public void SetRoi(Rectangle roi)
        {
            _roiRectangle = roi;
            _pictureBox.Invalidate();
            SaveRoiToIni();
        }

        /// <summary>
        /// 获取ROI内的图像区域
        /// </summary>
        public Mat GetRoiImage(Mat sourceImage)
        {
            if (!HasRoi || sourceImage == null || sourceImage.IsEmpty)
                return sourceImage?.Clone();

            // 将PictureBox坐标转换为图像坐标
            Rectangle imageRoi = ConvertToImageCoordinates(sourceImage.Size);
            
            // 确保ROI在图像范围内
            imageRoi.X = Math.Max(0, Math.Min(imageRoi.X, sourceImage.Width - 1));
            imageRoi.Y = Math.Max(0, Math.Min(imageRoi.Y, sourceImage.Height - 1));
            imageRoi.Width = Math.Min(imageRoi.Width, sourceImage.Width - imageRoi.X);
            imageRoi.Height = Math.Min(imageRoi.Height, sourceImage.Height - imageRoi.Y);

            if (imageRoi.Width <= 0 || imageRoi.Height <= 0)
                return sourceImage.Clone();

            Mat roiImage = new Mat(sourceImage, imageRoi);
            return roiImage.Clone();
        }

        /// <summary>
        /// 获取当前ROI矩形（图像坐标）
        /// </summary>
        public RectangleF GetRoi()
        {
            if (!HasRoi)
                return RectangleF.Empty;

            // 如果有图像，转换为图像坐标
            Size imageSize = GetCurrentImageSize();
            if (imageSize.Width > 0 && imageSize.Height > 0)
            {
                Rectangle imageRoi = ConvertToImageCoordinates(imageSize);
                return new RectangleF(imageRoi.X, imageRoi.Y, imageRoi.Width, imageRoi.Height);
            }

            return new RectangleF(_roiRectangle.X, _roiRectangle.Y, _roiRectangle.Width, _roiRectangle.Height);
        }

        /// <summary>
        /// 获取当前显示的图像尺寸
        /// </summary>
        private Size GetCurrentImageSize()
        {
            if (_pictureBox != null && _pictureBox.Image != null)
            {
                return new Size(_pictureBox.Image.Width, _pictureBox.Image.Height);
            }
            return Size.Empty;
        }

        /// <summary>
        /// 将PictureBox坐标转换为图像坐标
        /// </summary>
        private Rectangle ConvertToImageCoordinates(Size imageSize)
        {
            if (_pictureBox.Image == null)
                return _roiRectangle;

            float scaleX = (float)imageSize.Width / _pictureBox.ClientSize.Width;
            float scaleY = (float)imageSize.Height / _pictureBox.ClientSize.Height;

            int x = (int)(_roiRectangle.X * scaleX);
            int y = (int)(_roiRectangle.Y * scaleY);
            int width = (int)(_roiRectangle.Width * scaleX);
            int height = (int)(_roiRectangle.Height * scaleY);

            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 将图像坐标转换为PictureBox坐标
        /// </summary>
        private Rectangle ConvertToPictureBoxCoordinates(Rectangle imageRoi, Size imageSize)
        {
            if (_pictureBox.ClientSize.Width == 0 || _pictureBox.ClientSize.Height == 0)
                return imageRoi;

            float scaleX = (float)_pictureBox.ClientSize.Width / imageSize.Width;
            float scaleY = (float)_pictureBox.ClientSize.Height / imageSize.Height;

            int x = (int)(imageRoi.X * scaleX);
            int y = (int)(imageRoi.Y * scaleY);
            int width = (int)(imageRoi.Width * scaleX);
            int height = (int)(imageRoi.Height * scaleY);

            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 获取ROI的图像坐标矩形
        /// </summary>
        public Rectangle GetRoiImageRectangle()
        {
            if (!HasRoi)
                return Rectangle.Empty;

            Size imageSize = GetCurrentImageSize();
            if (imageSize.Width > 0 && imageSize.Height > 0)
            {
                return ConvertToImageCoordinates(imageSize);
            }

            return _roiRectangle;
        }

        #endregion

        #region INI文件持久化

        /// <summary>
        /// 保存ROI到INI文件（使用FileOperate.INIFile）
        /// </summary>
        public void SaveRoiToIni()
        {
            try
            {
                var ini = new INIFile(DefaultIniFilePath);
                ini.WriteInt(DefaultIniSection, "X", _roiRectangle.X);
                ini.WriteInt(DefaultIniSection, "Y", _roiRectangle.Y);
                ini.WriteInt(DefaultIniSection, "Width", _roiRectangle.Width);
                ini.WriteInt(DefaultIniSection, "Height", _roiRectangle.Height);
                ini.WriteBool(DefaultIniSection, "Enabled", HasRoi);
                ini.SaveToFile();
            }
            catch (Exception ex)
            {
                Log($"保存ROI到INI失败 原因是 {ex.Message}", MessageLevel.Error);
            }
        }

        /// <summary>
        /// 从INI文件加载ROI（使用FileOperate.INIFile）
        /// </summary>
        public void LoadRoiFromIni()
        {
            try
            {
                if (!System.IO.File.Exists(DefaultIniFilePath))
                    return;

                var ini = new INIFile(DefaultIniFilePath);
                if (!ini.KeyExists(DefaultIniSection, "Enabled") || !ini.ReadBool(DefaultIniSection, "Enabled", false))
                    return;

                int x = ini.ReadInt(DefaultIniSection, "X", 0);
                int y = ini.ReadInt(DefaultIniSection, "Y", 0);
                int width = ini.ReadInt(DefaultIniSection, "Width", 0);
                int height = ini.ReadInt(DefaultIniSection, "Height", 0);

                if (width > 0 && height > 0)
                {
                    _roiRectangle = new Rectangle(x, y, width, height);
                    _pictureBox.Invalidate();
                }
            }
            catch (Exception ex)
            {
                Log($"从INI加载ROI失败 原因是 {ex.Message}", MessageLevel.Error);
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 释放资源，取消事件绑定
        /// </summary>
        public void Dispose()
        {
            if (_pictureBox != null)
            {
                _pictureBox.MouseDown -= PictureBox_MouseDown;
                _pictureBox.MouseMove -= PictureBox_MouseMove;
                _pictureBox.MouseUp -= PictureBox_MouseUp;
                _pictureBox.Paint -= PictureBox_Paint;
            }
        }

        #endregion
    }
}
