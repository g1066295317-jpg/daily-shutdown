using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed class TimeInput : Control
    {
        private int hour = 23, minute, part, pending = -1;
        private DateTime lastDigit;
        public DateTime Value
        {
            get { return DateTime.Today.AddHours(hour).AddMinutes(minute); }
            set { hour = value.Hour; minute = value.Minute; pending = -1; Invalidate(); }
        }
        public TimeInput()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true; BackColor = Color.White; Size = new Size(266, 88);
            AccessibleName = "每日关机时间，左右选择时分，上下调整，或输入数字";
            AccessibleRole = AccessibleRole.SpinButton;
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }
        private void Step(int delta)
        {
            if (part == 0) hour = (hour + delta + 24) % 24;
            else minute = (minute + delta + 60) % 60;
            pending = -1; Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            if (e.X > Width - 30) { Step(e.Y < Height / 2 ? 1 : -1); return; }
            part = e.X < Width * .45 ? 0 : 1; pending = -1; Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Step(e.Delta > 0 ? 1 : -1); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { pending = -1; base.OnLostFocus(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { part = e.KeyCode == Keys.Left ? 0 : 1; pending = -1; Invalidate(); e.Handled = true; }
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { Step(e.KeyCode == Keys.Up ? 1 : -1); e.Handled = true; }
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.KeyChar < '0' || e.KeyChar > '9') return;
            int digit = e.KeyChar - '0';
            int max = part == 0 ? 23 : 59;
            int value = pending >= 0 && (DateTime.UtcNow - lastDigit).TotalSeconds < 1.5 ? pending * 10 + digit : digit;
            if (value > max) value = digit;
            bool complete = pending >= 0 && (DateTime.UtcNow - lastDigit).TotalSeconds < 1.5;
            if (part == 0) hour = value; else minute = value;
            pending = complete ? -1 : digit; lastDigit = DateTime.UtcNow;
            if (complete && part == 0) part = 1;
            Invalidate(); e.Handled = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = Width / 266f;
            e.Graphics.ScaleTransform(scale, Height / 88f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Focused)
            {
                using (var path = Theme.Round(new RectangleF(part == 0 ? 0 : 126, 4, 100, 76), 12))
                using (var brush = new SolidBrush(Theme.Mint)) e.Graphics.FillPath(brush, path);
            }
            using (var font = new Font("Segoe UI", 44, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Theme.Ink))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.DrawString(hour.ToString("00"), font, brush, new RectangleF(-4, -4, 110, 92), format);
                e.Graphics.DrawString(":", font, brush, new RectangleF(97, -7, 33, 92), format);
                e.Graphics.DrawString(minute.ToString("00"), font, brush, new RectangleF(122, -4, 110, 92), format);
            }
            using (var pen = new Pen(Theme.Muted, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                e.Graphics.DrawLines(pen, new[] { new PointF(246, 30), new PointF(251, 25), new PointF(256, 30) });
                e.Graphics.DrawLines(pen, new[] { new PointF(246, 55), new PointF(251, 60), new PointF(256, 55) });
            }
        }
    }
}
