using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed partial class MainForm
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void BuildLayout()
        {
            Text = "每天定时关机"; ClientSize = new Size(560, 716);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background; ForeColor = Theme.Ink;
            Font = new Font("Microsoft YaHei UI", 9);
            DoubleBuffered = true;
            var logo = new PictureBox { Bounds = new Rectangle(26, 27, 42, 42), SizeMode = PictureBoxSizeMode.Zoom };
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                if (stream != null) using (var icon = new Icon(stream, 48, 48)) logo.Image = icon.ToBitmap();
            logo.Disposed += (s, e) => { if (logo.Image != null) logo.Image.Dispose(); };
            Controls.Add(logo);
            var title = AddText(this, "每天定时关机", 82, 24, 330, 30, 17, true, Theme.Ink);
            var subtitle = AddText(this, "每天按时收尾，安心休息", 83, 59, 330, 23, 9, false, Theme.Muted);
            MouseEventHandler drag = (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
            title.MouseDown += drag; subtitle.MouseDown += drag; logo.MouseDown += drag;
            MouseDown += (s, e) => { if (e.Y < 95) drag(s, e); };
            var minimize = new SoftButton { Text = "−", TextOnly = true, Bounds = new Rectangle(458, 19, 34, 34), ForeColor = Theme.Muted, AccessibleName = "最小化到托盘" };
            var close = new SoftButton { Text = "×", TextOnly = true, Bounds = new Rectangle(502, 19, 34, 34), ForeColor = Theme.Muted, AccessibleName = "关闭窗口并收进托盘" };
            minimize.Click += (s, e) => HideToTray(); close.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { minimize, close });
            var clockCard = new SoftCard { Bounds = new Rectangle(24, 107, 512, 286) };
            Controls.Add(clockCard);
            AddText(clockCard, "关机倒计时时间", 24, 21, 260, 26, 11, true, Theme.Ink);
            status.Bounds = new Rectangle(407, 22, 80, 27); status.TextAlign = ContentAlignment.MiddleCenter;
            status.Font = new Font("Microsoft YaHei UI", 9); clockCard.Controls.Add(status);
            time.Bounds = new Rectangle(19, 57, 266, 88); clockCard.Controls.Add(time);
            var enable = (SoftButton)CreateButton("保存并启用", 326, 76, 162, true);
            enable.Height = 46;
            enable.Click += (s, e) => Perform(() => {
                store.Save(time.Value, Application.ExecutablePath, "--run", selectedRule);
                if (startup.Checked) startupStore.SetEnabled(true, Application.ExecutablePath);
                ShutdownCountdown.CancelRunning(); RefreshSafely(true);
            });
            clockCard.Controls.Add(enable);
            AddText(clockCard, "点击时 / 分输入数字，也可用 ↑ ↓ 调整", 24, 150, 455, 23, 9, false, Theme.Muted);
            AddText(clockCard, "重复", 24, 189, 80, 27, 10, false, Theme.Ink);
            repeat = new SoftButton { Text = "每天  ›", TextOnly = true, ForeColor = Theme.Green, Bounds = new Rectangle(128, 177, 360, 42), AccessibleName = "选择重复与规划" };
            repeat.Click += (s, e) => Perform(ChooseRepeat); clockCard.Controls.Add(repeat);
            clockCard.Controls.Add(new Panel { BackColor = Color.FromArgb(235, 241, 237), Bounds = new Rectangle(24, 236, 464, 1) });
            next.Bounds = new Rectangle(24, 253, 464, 25); next.ForeColor = Theme.Muted; next.BackColor = Color.Transparent; clockCard.Controls.Add(next);
            var settings = new SoftCard { Bounds = new Rectangle(24, 409, 512, 142) };
            Controls.Add(settings);
            AddText(settings, "开机自动启动", 24, 19, 390, 24, 10, true, Theme.Ink);
            startupNote.Bounds = new Rectangle(24, 49, 398, 23); startupNote.ForeColor = Theme.Muted; startupNote.BackColor = Color.Transparent; settings.Controls.Add(startupNote);
            startup.Location = new Point(439, 26); settings.Controls.Add(startup);
            settings.Controls.Add(new Panel { BackColor = Color.FromArgb(235, 241, 237), Bounds = new Rectangle(24, 87, 464, 1) });
            AddText(settings, "关闭窗口后收进托盘", 24, 104, 300, 23, 9, false, Theme.Ink);
            AddText(settings, "随时查看状态", 370, 104, 122, 23, 9, false, Theme.Muted);
            pause = (SoftButton)CreateButton("暂停计划", 24, 571, 248, false);
            pause.Click += (s, e) => Perform(TogglePause);
            var demo = CreateButton("体验倒计时", 288, 571, 248, false);
            demo.Click += (s, e) => Perform(() => Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--simulate") { UseShellExecute = true }));
            var cancel = new SoftButton { Text = "取消本次倒计时", TextOnly = true, Bounds = new Rectangle(24, 629, 148, 34), ForeColor = Theme.Muted };
            cancel.Click += (s, e) => Perform(CancelCountdown);
            remove = new SoftButton { Text = "删除计划", TextOnly = true, Bounds = new Rectangle(276, 629, 98, 34), ForeColor = Theme.Muted };
            remove.Click += (s, e) => Perform(() => { store.Delete(); ShutdownCountdown.CancelRunning(); RefreshSafely(true); });
            var clear = new SoftButton { Text = "移除全部配置", TextOnly = true, Bounds = new Rectangle(388, 629, 148, 34), ForeColor = Theme.Muted };
            clear.Click += (s, e) => Perform(RemoveConfiguration);
            Controls.AddRange(new Control[] { pause, demo, cancel, remove, clear });
            AddText(this, "到点倒计时 60 秒  ·  可取消本次  ·  睡眠时跳过", 24, 679, 512, 23, 9, false, Theme.Muted).TextAlign = ContentAlignment.MiddleCenter;
            SizeChanged += (s, e) => UpdateCorners(); UpdateCorners();
        }

        private void UpdateCorners()
        {
            using (var path = Theme.Round(new RectangleF(0, 0, Width, Height), 22))
            { Region old = Region; Region = new Region(path); if (old != null) old.Dispose(); }
        }

        internal static Label AddText(Control parent, string text, int x, int y, int width, int height, float size, bool bold, Color color)
        {
            var label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height), ForeColor = color,
                Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular), BackColor = Color.Transparent };
            parent.Controls.Add(label); return label;
        }

        public static Button CreateButton(string text, int x, int y, int width, bool primary)
        {
            return new SoftButton { Text = text, Bounds = new Rectangle(x, y, width, 46), Primary = primary, ForeColor = Theme.Ink };
        }
    }
}
