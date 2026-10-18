using System;
using System.IO;
using System.Reflection;

namespace DailyShutdown
{
    internal static partial class Tests
    {
        private static void TestNativePlanningAndRemoval(string app)
        {
            string id = Guid.NewGuid().ToString("N");
            var schedule = new ScheduleStore("DailyShutdownHelper-Test-" + id);
            var startup = new StartupRegistration("DailyShutdownHelper-Test-" + id);
            var otherStartup = new StartupRegistration("DailyShutdownHelper-Test-Other-" + id);
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calendar-tests-" + id);
            var calendars = new HolidayCalendar(directory);
            string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calendar-fixture-" + id + ".xml");
            string marker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-" + id + ".txt");
            try
            {
                foreach (RepeatKind kind in Enum.GetValues(typeof(RepeatKind)))
                {
                    var rule = new RepeatRule { Kind = kind, DaysMask = 42, OnceDate = DateTime.Today.AddDays(1) };
                    schedule.Save(DateTime.Now.AddHours(1), app, "--probe \"" + marker + "\"", rule);
                    Check(schedule.Read().Rule.ToData() == rule.ToData(), "real task stores and reads recurrence " + kind);
                }
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("calendar-2026.xml"))
                using (var file = File.Create(fixture)) resource.CopyTo(file);
                Check(calendars.Import(fixture) == 2026 && File.Exists(Path.Combine(directory, "calendar-2026.xml")), "calendar import validates and persists annual data");
                string sentinel = Path.Combine(directory, "keep-user-note.txt"); File.WriteAllText(sentinel, "preserve");
                startup.SetEnabled(true, app); otherStartup.SetEnabled(true, app);
                int cancelled = 0;
                ConfigurationRemoval.Remove(schedule, startup, calendars, () => cancelled++);
                Check(cancelled == 1, "one-click removal cancels current countdown");
                Check(schedule.Read() == null, "one-click removal deletes only its scheduled task");
                Check(startup.ReadCommand() == null && otherStartup.ReadCommand() != null, "one-click removal preserves unrelated startup entries");
                Check(!File.Exists(Path.Combine(directory, "calendar-2026.xml")) && File.Exists(sentinel), "one-click removal clears imported calendar and preserves unrelated file");
                ConfigurationRemoval.Remove(schedule, startup, calendars, () => { });
                Check(true, "repeated configuration removal remains safe");
            }
            finally
            {
                schedule.Delete(); startup.Clear(); otherStartup.Clear(); calendars.ClearImported();
                foreach (string file in new[] { fixture, marker, Path.Combine(directory, "keep-user-note.txt") })
                    if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }
        }
    }
}
