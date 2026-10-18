using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed class WeekdayChip : CheckBox
    {
        public WeekdayChip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand; BackColor = Theme.Background;
            Font = new Font("Microsoft YaHei UI", 10);
            CheckedChanged += (s, e) => Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface(Parent)); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(new RectangleF(1, 1, Width - 2, Height - 2), 10))
            using (var brush = new SolidBrush(Checked ? Theme.Mint : Color.White))
            using (var pen = new Pen(Checked ? Theme.Green : Color.FromArgb(219, 231, 223)))
            { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Checked ? Theme.Green : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
        }
    }
}
