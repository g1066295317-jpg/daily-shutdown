using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal static partial class Tests
    {
        private static void TestIconResources()
        {
            using (var icon = TrayController.LoadIcon("app.ico"))
            using (var bitmap = icon.ToBitmap())
            {
                Check(bitmap.Width == 32 && bitmap.Height == 32, "tray icon loads at expected native resolution");
                var pixel = bitmap.GetPixel(3, 16);
                Check(pixel.A > 240 && pixel.G > pixel.R + 40, "native icon decodes the intended mint color");
                Check(bitmap.GetPixel(0, 0).A == 0, "native icon preserves transparent corners");
            }
        }
        private static void Key(TimeInput input, string method, object args)
        {
            typeof(TimeInput).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, new[] { args });
        }
        private static void TestTimeInput()
        {
            using (var input = new TimeInput())
            {
                input.Value = DateTime.Today.AddHours(23).AddMinutes(59);
                Key(input, "OnKeyDown", new KeyEventArgs(Keys.Up));
                Check(input.Value.Hour == 0 && input.Value.Minute == 59, "hour input wraps at 24");
                Key(input, "OnKeyDown", new KeyEventArgs(Keys.Right));
                Key(input, "OnKeyDown", new KeyEventArgs(Keys.Up));
                Check(input.Value.Minute == 0, "minute input wraps at 60");
                Key(input, "OnKeyDown", new KeyEventArgs(Keys.Left));
                Key(input, "OnKeyPress", new KeyPressEventArgs('2'));
                Key(input, "OnKeyPress", new KeyPressEventArgs('1'));
                Key(input, "OnKeyPress", new KeyPressEventArgs('3'));
                Key(input, "OnKeyPress", new KeyPressEventArgs('0'));
                Check(input.Value.Hour == 21 && input.Value.Minute == 30, "four-digit keyboard input edits time");
            }
        }

        private static void TestSingleInstance()
        {
            string suffix = "Test-" + Guid.NewGuid().ToString("N");
            using (var first = new SingleInstance(suffix))
            using (var second = new SingleInstance(suffix))
            {
                Check(first.IsPrimary && !second.IsPrimary, "one primary window instance");
                second.Activation.Set();
                Check(first.Activation.WaitOne(1000), "second instance activates first window");
                Check(!first.Activation.WaitOne(0), "activation signal automatically resets");
            }
        }

        private static void TestStartupRegistration(string app)
        {
            var startup = new StartupRegistration("DailyShutdownHelper-Test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Check(startup.ReadCommand() == null, "startup test value initially absent");
                startup.SetEnabled(true, app);
                Check(startup.ReadCommand() == "\"" + app + "\" --tray", "startup command quotes full executable path and starts hidden");
                startup.SetEnabled(false, app);
                Check(startup.ReadCommand() == null, "startup setting can be disabled and removed");
                Check(StartupRegistration.CommandFor("D:\\含 空格\\程序.exe").Contains("\" --tray"), "startup supports spaces and Unicode");
            }
            finally { startup.SetEnabled(false, app); }
        }

        private static void Pump(int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
        }

        private static void TestTrayLifecycle()
        {
            string suffix = "Test-" + Guid.NewGuid().ToString("N");
            var schedules = new ScheduleStore("DailyShutdownHelper-" + suffix);
            var startup = new StartupRegistration("DailyShutdownHelper-" + suffix);
            using (var activate = new EventWaitHandle(false, EventResetMode.AutoReset))
            using (var main = new MainForm(true, activate, schedules, startup))
            {
                main.Show(); Pump(200);
                Check(!main.Visible && main.Tray.Visible, "logon startup is hidden with tray icon visible");
                activate.Set(); Pump(650);
                Check(main.Visible && main.ShowInTaskbar, "activation signal restores main window");
                main.Close(); Pump(150);
                Check(!main.IsDisposed && !main.Visible && main.Tray.Visible, "closing main window keeps tray and process alive");
                main.ShowMain(); Pump(100);
                Check(main.Visible, "tray action reopens hidden window");
                main.Tray.Update(new ScheduleState { Enabled = true, Time = DateTime.Today.AddHours(23), NextRun = DateTime.Today.AddDays(1).AddHours(23) }, false, true);
                Check(main.Tray.Tooltip.Contains("23:00") && main.Tray.Summary.Contains("已启用"), "tray tooltip shows saved daily schedule");
                main.Tray.Update(null, true, false);
                Check(main.Tray.Summary.Contains("倒计时"), "tray reflects active countdown");
                main.Tray.Update(null, false, false, "test error");
                Check(main.Tray.Summary.Contains("失败"), "tray reports status errors");
                main.ExitApplication(); Pump(100);
                Check(main.IsDisposed && !main.Tray.Visible, "explicit exit removes notification icon");
            }
        }
    }
}
