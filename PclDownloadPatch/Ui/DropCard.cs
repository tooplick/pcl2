using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PclDownloadPatch.Ui
{
    /// <summary>圆角卡片容器，填充色与描边色可随状态切换。</summary>
    internal sealed class DropCard : Control
    {
        private Color fillColor = Color.White;
        private Color borderColor = Color.FromArgb(203, 213, 225);
        private float borderWidth = 1.6F;
        private float radius = 18F;

        public DropCard()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        public Color FillColor
        {
            get { return fillColor; }
            set { fillColor = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get { return borderColor; }
            set { borderColor = value; Invalidate(); }
        }

        public float BorderWidth
        {
            get { return borderWidth; }
            set { borderWidth = value; Invalidate(); }
        }

        public float Radius
        {
            get { return radius; }
            set { radius = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF bounds = new RectangleF(
                borderWidth / 2F,
                borderWidth / 2F,
                Width - borderWidth - 1F,
                Height - borderWidth - 1F);
            using (GraphicsPath path = UiPaint.RoundedPath(bounds, radius))
            {
                using (SolidBrush brush = new SolidBrush(fillColor))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(borderColor, borderWidth))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }
}
