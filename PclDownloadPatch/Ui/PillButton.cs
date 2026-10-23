using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PclDownloadPatch.Ui
{
    /// <summary>圆角主色按钮，带悬停与按下状态。</summary>
    internal sealed class PillButton : Control
    {
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);
        private static readonly Color AccentHover = Color.FromArgb(29, 84, 205);
        private static readonly Color AccentPressed = Color.FromArgb(24, 70, 176);
        private static readonly Color DisabledBack = Color.FromArgb(226, 232, 240);
        private static readonly Color DisabledText = Color.FromArgb(148, 163, 184);

        private bool hover;
        private bool pressed;

        public PillButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(200, 46);
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            hover = false;
            pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF bounds = new RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F);
            Color back;
            Color text;
            if (!Enabled)
            {
                back = DisabledBack;
                text = DisabledText;
            }
            else if (pressed)
            {
                back = AccentPressed;
                text = Color.White;
            }
            else if (hover)
            {
                back = AccentHover;
                text = Color.White;
            }
            else
            {
                back = Accent;
                text = Color.White;
            }

            using (GraphicsPath path = UiPaint.RoundedPath(bounds, bounds.Height / 2F))
            {
                using (SolidBrush brush = new SolidBrush(back))
                {
                    g.FillPath(brush, path);
                }
            }

            TextRenderer.DrawText(
                g,
                Text,
                Font,
                new Rectangle(0, 0, Width, Height),
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
