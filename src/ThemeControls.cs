using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(35, 57, 51);
        public static readonly Color Muted = Color.FromArgb(120, 139, 132);
        public static readonly Color Green = Color.FromArgb(41, 143, 113);
        public static readonly Color Mint = Color.FromArgb(232, 245, 237);
        public static readonly Color Background = Color.FromArgb(245, 249, 246);
        public static Color Surface(Control parent)
        {
            if (parent is SoftCard) return Color.White;
            return parent != null && parent.BackColor.A == 255 ? parent.BackColor : Background;
        }
        public static GraphicsPath Round(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SoftCard : Panel
    {
        public SoftCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 18))
            using (var pen = new Pen(Color.FromArgb(229, 238, 232)))
            { e.Graphics.FillPath(Brushes.White, path); e.Graphics.DrawPath(pen, path); }
            base.OnPaint(e);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (var path = Theme.Round(new RectangleF(0, 0, Width, Height), 18))
            { Region old = Region; Region = new Region(path); if (old != null) old.Dispose(); }
        }
    }

    internal sealed class SoftButton : Button
    {
        public bool Primary;
        public bool TextOnly;
        private bool hover;
        public SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Background; Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 10);
        }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); BackColor = Theme.Surface(Parent); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Theme.Surface(Parent)); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            // Paint every pixel; ButtonBase's simulated transparency can retain unrelated pixels.
            e.Graphics.Clear(Theme.Surface(Parent));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = !Enabled ? Color.FromArgb(239, 243, 240) : Primary
                ? (hover ? Color.FromArgb(30, 123, 95) : Theme.Green) : hover ? Theme.Mint : Color.White;
            if (!TextOnly)
            {
                using (var path = Theme.Round(new RectangleF(1, 1, Width - 2, Height - 2), 12))
                using (var brush = new SolidBrush(fill))
                using (var pen = new Pen(Primary ? fill : Color.FromArgb(219, 231, 223)))
                { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                !Enabled ? Color.FromArgb(168, 182, 173) : Primary ? Color.White : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
        }
    }

    internal sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            Size = new Size(48, 28); Cursor = Cursors.Hand;
            AccessibleName = "登录 Windows 后自动启动";
            CheckedChanged += (s, e) => Invalidate();
        }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); BackColor = Theme.Surface(Parent); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Theme.Surface(Parent)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface(Parent));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(new RectangleF(1, 2, Width - 2, Height - 4), (Height - 4) / 2f))
            using (var brush = new SolidBrush(Checked ? Theme.Green : Color.FromArgb(211, 222, 215)))
                e.Graphics.FillPath(brush, path);
            float size = Height - 10;
            float x = Checked ? Width - size - 5 : 5;
            e.Graphics.FillEllipse(Brushes.White, x, 5, size, size);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
        }
    }
}
