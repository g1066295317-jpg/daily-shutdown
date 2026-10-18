using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal static partial class Tests
    {
        private static void PaintControl(Control control, Bitmap bitmap)
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // A dirty backing surface exposes skipped background pixels and native remnants.
                graphics.Clear(Color.Magenta);
                control.GetType().GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(control, new object[] { new PaintEventArgs(graphics, new Rectangle(Point.Empty, bitmap.Size)) });
            }
        }

        private static void CheckSurface(Control control, Color expected, string name)
        {
            using (var bitmap = new Bitmap(control.Width, control.Height))
            {
                PaintControl(control, bitmap);
                bool clean = bitmap.GetPixel(0, 0).ToArgb() == expected.ToArgb()
                    && bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).ToArgb() == expected.ToArgb();
                for (int y = 0; y < bitmap.Height && clean; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.A != 255 || pixel.ToArgb() == Color.Magenta.ToArgb() || pixel.ToArgb() == Color.Black.ToArgb())
                        { clean = false; break; }
                    }
                Check(clean, name);
            }
        }

        private static void TestPainting()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
            using (var parent = new Panel { BackColor = Theme.Background })
            using (var card = new SoftCard())
            using (var button = new SoftButton { Text = "保存并启用", Primary = true, ForeColor = Theme.Ink })
            using (var link = new SoftButton { Text = "取消本次倒计时", TextOnly = true, ForeColor = Theme.Muted })
            using (var toggle = new ToggleSwitch())
            using (var chip = new WeekdayChip { Text = "三" })
            {
                string suffix = " at " + (scale * 100).ToString("0") + "%";
                button.Size = new Size((int)(162 * scale), (int)(46 * scale));
                link.Size = new Size((int)(148 * scale), (int)(34 * scale));
                toggle.Size = new Size((int)(48 * scale), (int)(28 * scale));
                chip.Size = new Size((int)(56 * scale), (int)(38 * scale)); parent.Controls.Add(chip);
                card.Controls.Add(button); parent.Controls.Add(link); card.Controls.Add(toggle);
                CheckSurface(button, Color.White, "primary button fully overwrites dirty pixels" + suffix);
                button.Primary = false; button.Enabled = false;
                CheckSurface(button, Color.White, "disabled button has clean rounded corners" + suffix);
                CheckSurface(link, Theme.Background, "text-only button has clean parent background" + suffix);
                CheckSurface(toggle, Color.White, "unchecked switch fully repaints" + suffix);
                toggle.Checked = true;
                CheckSurface(toggle, Color.White, "checked switch fully repaints" + suffix);
                CheckSurface(chip, Theme.Background, "unchecked weekday chip fully repaints" + suffix);
                chip.Checked = true;
                CheckSurface(chip, Theme.Background, "checked weekday chip fully repaints" + suffix);
            }
        }

        private static void RenderScaledWindows(string output)
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
            using (var main = new MainForm(false, null, new ScheduleStore("DailyShutdownHelper-Test-Preview-" + Guid.NewGuid().ToString("N")),
                new StartupRegistration("DailyShutdownHelper-Test-Preview-" + Guid.NewGuid().ToString("N"))))
            {
                if (scale != 1) main.Scale(new SizeF(scale, scale));
                Capture(main, Path.Combine(output, "界面检查-" + (scale * 100).ToString("0") + ".png"));
                main.ExitApplication();
            }
        }
    }
}
