using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PclDownloadPatch.Ui
{
    /// <summary>
    /// 圆形状态图标，支持下载箭头、旋转进度、对勾、叉号与信息五种形态。
    /// </summary>
    internal sealed class StatusIcon : Control
    {
        public enum IconKind
        {
            Download,
            Spinner,
            Check,
            Cross,
            Info
        }

        private const float CircleInset = 2F;

        private IconKind kind = IconKind.Download;
        private Color glyphColor = Color.FromArgb(37, 99, 235);
        private Color circleColor = Color.FromArgb(239, 246, 255);
        private float spinAngle;
        private Timer spinTimer;

        public StatusIcon()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.Transparent;
            Size = new Size(72, 72);
            TabStop = false;
        }

        public IconKind Kind
        {
            get { return kind; }
            set
            {
                if (kind == value) return;
                kind = value;
                UpdateSpinTimer();
                Invalidate();
            }
        }

        public Color GlyphColor
        {
            get { return glyphColor; }
            set { glyphColor = value; Invalidate(); }
        }

        public Color CircleColor
        {
            get { return circleColor; }
            set { circleColor = value; Invalidate(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && spinTimer != null)
            {
                spinTimer.Stop();
                spinTimer.Dispose();
                spinTimer = null;
            }
            base.Dispose(disposing);
        }

        private void UpdateSpinTimer()
        {
            if (kind == IconKind.Spinner)
            {
                if (spinTimer == null)
                {
                    spinTimer = new Timer { Interval = 40 };
                    spinTimer.Tick += OnSpinTick;
                }
                spinTimer.Start();
            }
            else if (spinTimer != null)
            {
                spinTimer.Stop();
            }
        }

        private void OnSpinTick(object sender, EventArgs e)
        {
            spinAngle = (spinAngle + 15F) % 360F;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float side = Math.Min(Width, Height);
            float cx = Width / 2F;
            float cy = Height / 2F;
            float r = side / 2F - CircleInset;

            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, cx - r, cy - r, r * 2F, r * 2F);
            }

            using (Pen pen = new Pen(glyphColor, side * 0.08F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                switch (kind)
                {
                    case IconKind.Download:
                        UiPaint.DrawDownloadArrow(g, pen, cx, cy, r);
                        break;
                    case IconKind.Spinner:
                        pen.Width = r * 0.18F;
                        float d = r * 1.1F;
                        g.DrawArc(pen, cx - d / 2F, cy - d / 2F, d, d, spinAngle, 110F);
                        break;
                    case IconKind.Check:
                        pen.Width = r * 0.18F;
                        g.DrawLine(pen, cx - r * 0.34F, cy + r * 0.02F, cx - r * 0.08F, cy + r * 0.28F);
                        g.DrawLine(pen, cx - r * 0.08F, cy + r * 0.28F, cx + r * 0.36F, cy - r * 0.24F);
                        break;
                    case IconKind.Cross:
                        pen.Width = r * 0.18F;
                        float a = r * 0.28F;
                        g.DrawLine(pen, cx - a, cy - a, cx + a, cy + a);
                        g.DrawLine(pen, cx - a, cy + a, cx + a, cy - a);
                        break;
                    case IconKind.Info:
                        pen.Width = r * 0.16F;
                        using (SolidBrush dot = new SolidBrush(pen.Color))
                        {
                            g.FillEllipse(dot, cx - r * 0.09F, cy - r * 0.42F, r * 0.18F, r * 0.18F);
                        }
                        g.DrawLine(pen, cx, cy - r * 0.06F, cx, cy + r * 0.4F);
                        break;
                }
            }
        }
    }
}
