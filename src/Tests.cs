using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace DailyShutdown
{
    internal static partial class Tests
    {
        private static int passed;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 2 && args[0] == "--render-only") { RenderWindows(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--painting")
                {
                    TestPainting(); TestTrayLifecycle(); RenderWindows(args[1]); RenderScaledWindows(args[1]);
                    Console.WriteLine("SUCCESS: " + passed + " painting and lifecycle checks passed. No shutdown command was run.");
                    return 0;
                }
                TestSchedulingRules();
                TestCountdown();
                TestXml();
                TestTimeInput();
                TestSingleInstance();
                TestIconResources();
                TestRepeatRules();
                if (args.Length > 0 && args[0] == "--unit-only") return 0;
                string app = Path.GetFullPath(args[0]);
                string output = Path.GetFullPath(args[1]);
                TestRealScheduler(app);
                TestStartupRegistration(app);
                TestNativePlanningAndRemoval(app);
                TestTrayLifecycle();
                TestPlanningWindows(output);
                RenderWindows(output);
                Console.WriteLine("SUCCESS: " + passed + " checks passed. No shutdown command was run.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void TestSchedulingRules()
        {
            DateTime now = new DateTime(2026, 10, 18, 22, 0, 0);
            Check(ScheduleStore.NextTime(now, now.AddHours(1)) == now.AddHours(1), "future time stays today");
            Check(ScheduleStore.NextTime(now, now) == now.AddDays(1), "equal time starts tomorrow");
            Check(ScheduleStore.NextTime(now, now.AddHours(-1)) == now.AddDays(1).AddHours(-1), "past time starts tomorrow");
            now = new DateTime(2026, 12, 31, 23, 59, 45);
            Check(ScheduleStore.NextTime(now, now.Date) == new DateTime(2027, 1, 1), "year rollover");
            var state = new ScheduleState { Enabled = true, Time = now, Executable = Application.ExecutablePath, Arguments = "--run" };
            Check(ShutdownCountdown.IsCurrent(state, now), "current valid task accepted");
            state.Enabled = false;
            Check(!ShutdownCountdown.IsCurrent(state, now), "paused task cannot shut down");
            state.Enabled = true;
            state.Executable = "C:\\different.exe";
            Check(!ShutdownCountdown.IsCurrent(state, now), "moved executable cannot run stale task");
            state.Executable = Application.ExecutablePath;
            Check(!ShutdownCountdown.IsCurrent(state, now.AddMinutes(-1)), "early execution rejected");
            Check(!ShutdownCountdown.IsCurrent(state, now.AddMinutes(3)), "late or missed execution rejected");
        }

        private static void TestCountdown()
        {
            DateTime now = DateTime.UtcNow;
            var clock = new CountdownClock(now);
            Check(clock.Remaining(now) == 60, "countdown starts at 60 seconds");
            Check(clock.Remaining(now.AddMilliseconds(59001)) == 1, "countdown rounds up");
            Check(!clock.Due(now.AddSeconds(59)), "shutdown cannot run before deadline");
            Check(clock.Due(now.AddSeconds(60)), "deadline reached after 60 seconds");
            clock.Cancel();
            Check(!clock.Due(now.AddHours(1)), "cancel prevents execution permanently");
            Check(ShutdownAction.Arguments == "/s /t 0", "no forced shutdown or system countdown");
            string testSignal = "Local\\DailyShutdownCancel-Test-" + Guid.NewGuid().ToString("N");
            using (var form = new ShutdownCountdown(false, null, testSignal))
            {
                Check(ShutdownCountdown.CancelRunning(testSignal), "cross-process cancel signal available");
                using (var handle = EventWaitHandle.OpenExisting(testSignal))
                    Check(handle.WaitOne(0), "cancel signal reaches countdown");
                form.Show();
                DateTime until = DateTime.UtcNow.AddSeconds(1);
                while (!form.IsDisposed && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
                Check(form.IsDisposed, "cancel signal closes real countdown without shutdown");
            }
        }

        private static void TestXml()
        {
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            string path = "D:\\测试 & 文件夹\\每天定时关机.exe";
            XElement root = XDocument.Parse(ScheduleStore.BuildXml(DateTime.Today.AddHours(23), path, "--run")).Root;
            XElement settings = root.Element(ns + "Settings");
            Check(root.Element(ns + "Actions").Element(ns + "Exec").Element(ns + "Command").Value == path, "Unicode and XML escaping");
            Check(root.Element(ns + "Triggers").Element(ns + "CalendarTrigger").Element(ns + "ScheduleByDay").Element(ns + "DaysInterval").Value == "1", "daily recurrence");
            Check(settings.Element(ns + "StartWhenAvailable").Value == "false", "missed tasks are skipped");
            Check(settings.Element(ns + "WakeToRun").Value == "false", "does not wake computer");
            Check(settings.Element(ns + "DisallowStartIfOnBatteries").Value == "false", "works on battery power");
            Check(root.Element(ns + "Principals").Element(ns + "Principal").Element(ns + "LogonType").Value == "InteractiveToken", "current signed-in user only");
        }

        private static void TestRealScheduler(string app)
        {
            string marker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-" + Guid.NewGuid().ToString("N") + ".txt");
            var store = new ScheduleStore("DailyShutdownHelper-Test-" + Guid.NewGuid().ToString("N"));
            try
            {
                store.Save(DateTime.Now.AddHours(1), app, "--probe \"" + marker + "\"");
                Check(store.Read().Enabled, "real task registered and read back");
                store.RunForTest();
                DateTime until = DateTime.UtcNow.AddSeconds(15);
                while (!File.Exists(marker) && DateTime.UtcNow < until) Thread.Sleep(100);
                Check(File.Exists(marker) && File.ReadAllText(marker).Contains("succeeded"), "real task launches executable in user session");
                store.Pause();
                Check(!store.Read().Enabled, "real task pause verified");
                store.Save(DateTime.Now.AddHours(2), app, "--probe \"" + marker + "\"");
                Check(store.Read().Enabled, "real task update and resume verified");
                store.Delete();
                Check(store.Read() == null, "real task deletion verified");
            }
            finally
            {
                store.Delete();
                if (File.Exists(marker)) File.Delete(marker);
            }
        }

        private static void RenderWindows(string output)
        {
            string preview = "DailyShutdownHelper-Test-Preview-" + Guid.NewGuid().ToString("N");
            using (var main = new MainForm(false, null, new ScheduleStore(preview), new StartupRegistration(preview)))
            { Capture(main, Path.Combine(output, "软件界面.png")); main.ExitApplication(); }
            using (var demo = new ShutdownCountdown(true, null)) Capture(demo, Path.Combine(output, "倒计时演示.png"));
            Check(true, "main window and demo rendered");
        }

        private static void Capture(Form form, string filename)
        {
            form.Show();
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            }
            form.Close();
        }
    }
}
