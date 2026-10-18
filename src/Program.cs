using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 0 || (args.Length == 1 && args[0] == "--tray"))
                {
                    bool hidden = args.Length == 1;
                    using (var instance = new SingleInstance())
                    {
                        if (!instance.IsPrimary)
                        {
                            if (!hidden && instance.Activation != null) instance.Activation.Set();
                            return 0;
                        }
                        using (var window = new MainForm(hidden, instance.Activation)) Application.Run(window);
                    }
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--probe")
                {
                    File.WriteAllText(args[1], "Task Scheduler probe succeeded.");
                    return 0;
                }
                if (args.Length != 1 || (args[0] != "--run" && args[0] != "--simulate")) return 2;
                bool simulation = args[0] == "--simulate";
                bool created;
                string mutexName = "Local\\DailyShutdownCountdown-" + ScheduleStore.UserId + (simulation ? "-demo" : "-live");
                using (var single = new Mutex(true, mutexName, out created))
                {
                    if (!created) return 0;
                    try
                    {
                        ScheduleState state = simulation ? null : new ScheduleStore().Read();
                        if (!simulation && !ShutdownCountdown.IsCurrent(state, DateTime.Now)) return 0;
                        Application.Run(new ShutdownCountdown(simulation, state));
                    }
                    finally { single.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("操作未完成：" + ex.Message, "每天定时关机", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
