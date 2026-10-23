using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PclDownloadPatch.Ui
{
    /// <summary>小型 GDI+ 绘制工具集。</summary>
    internal static class UiPaint
    {
        /// <summary>创建抗锯齿圆角矩形路径。</summary>
        internal static GraphicsPath RoundedPath(RectangleF bounds, float radius)
        {
            float diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0F, 90F);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        /// <summary>绘制“下载”箭头符号（竖线 + 箭头 + 底部托盘线）。</summary>
        internal static void DrawDownloadArrow(Graphics g, Pen pen, float cx, float cy, float r)
        {
            pen.Width = r * 0.16F;
            g.DrawLine(pen, cx, cy - r * 0.42F, cx, cy + r * 0.18F);
            g.DrawLine(pen, cx - r * 0.26F, cy - r * 0.06F, cx, cy + r * 0.26F);
            g.DrawLine(pen, cx + r * 0.26F, cy - r * 0.06F, cx, cy + r * 0.26F);
            g.DrawLine(pen, cx - r * 0.42F, cy + r * 0.52F, cx + r * 0.42F, cy + r * 0.52F);
        }
    }
}
