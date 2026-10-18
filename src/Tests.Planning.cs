using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace DailyShutdown
{
    internal static partial class Tests
    {
        private static void TestRepeatRules()
        {
            var daily = new RepeatRule();
            Check(daily.Next(new DateTime(2026, 10, 18, 23, 1, 0), new DateTime(2026, 1, 1, 23, 0, 0))
                == new DateTime(2026, 10, 19, 23, 0, 0), "daily recurrence continues tomorrow after passed time");
            var weekdays = new RepeatRule { Kind = RepeatKind.Weekdays };
            Check(weekdays.Next(new DateTime(2026, 10, 23, 23, 1, 0), new DateTime(2026, 1, 1, 23, 0, 0))
                == new DateTime(2026, 10, 26, 23, 0, 0), "Monday to Friday skips weekend");
            var custom = new RepeatRule { Kind = RepeatKind.Custom, DaysMask = 42 };
            Check(custom.Matches(new DateTime(2026, 10, 19)) && !custom.Matches(new DateTime(2026, 10, 20))
                && custom.Matches(new DateTime(2026, 10, 21)), "custom recurrence follows selected weekdays");
            Check(RepeatRule.FromData(custom.ToData()).DaysMask == 42, "custom weekdays survive serialization");
            Check(RepeatRule.FromData(null).Kind == RepeatKind.Daily, "old daily tasks remain compatible");
            bool rejected = false;
            try { new RepeatRule { Kind = RepeatKind.Custom, DaysMask = 0 }.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "empty custom selection rejected");
            var once = new RepeatRule { Kind = RepeatKind.Once, OnceDate = new DateTime(2026, 10, 19) };
            Check(once.Next(new DateTime(2026, 10, 18), new DateTime(2026, 1, 1, 21, 0, 0)) == new DateTime(2026, 10, 19, 21, 0, 0), "single execution honors chosen date");
            Check(once.Next(new DateTime(2026, 10, 20), DateTime.Today) == DateTime.MinValue, "expired single execution never repeats tomorrow");

            var calendar = HolidayCalendar.Current;
            string[] makeup = { "2026-01-04", "2026-02-14", "2026-02-28", "2026-05-09", "2026-09-20", "2026-10-10" };
            Check(makeup.All(x => calendar.IsWorkday(DateTime.Parse(x)) == true && calendar.IsHoliday(DateTime.Parse(x)) == false), "all six official makeup workdays included");
            string[] holidays = { "2026-01-01", "2026-01-03", "2026-02-15", "2026-02-23", "2026-04-04", "2026-04-06", "2026-05-01", "2026-05-05", "2026-06-19", "2026-06-21", "2026-09-25", "2026-09-27", "2026-10-01", "2026-10-07" };
            Check(holidays.All(x => calendar.IsHoliday(DateTime.Parse(x)) == true && calendar.IsWorkday(DateTime.Parse(x)) == false), "all seven official holiday interval boundaries honored");
            Check(calendar.IsHoliday(new DateTime(2026, 1, 10)) == false && calendar.IsWorkday(new DateTime(2026, 1, 10)) == false, "ordinary Saturday is neither statutory holiday nor workday");
            Check(calendar.IsWorkday(new DateTime(2026, 10, 19)) == true, "ordinary weekday remains a workday");
            Check(calendar.IsWorkday(new DateTime(2099, 1, 4)) == null && calendar.IsHoliday(new DateTime(2099, 1, 4)) == null, "uncovered years remain unknown");
            var workday = new RepeatRule { Kind = RepeatKind.Workday };
            var holiday = new RepeatRule { Kind = RepeatKind.Holiday };
            Check(workday.Matches(new DateTime(2026, 1, 4)) && !weekdays.Matches(new DateTime(2026, 1, 4)), "statutory workdays differ from fixed Monday to Friday");
            Check(!holiday.Matches(new DateTime(2026, 1, 10)) && holiday.Matches(new DateTime(2026, 1, 3)), "holiday mode excludes ordinary weekends");
            Check(!workday.Matches(new DateTime(2099, 1, 4)) && !holiday.Matches(new DateTime(2099, 1, 4)), "unknown calendar years cannot trigger shutdown");

            var live = new ScheduleState { Enabled = true, Time = new DateTime(2026, 10, 19, 23, 59, 0), Executable = Application.ExecutablePath, Arguments = "--run", Rule = once };
            Check(ShutdownCountdown.IsCurrent(live, new DateTime(2026, 10, 20, 0, 0, 0)), "countdown crossing midnight retains original eligible date");
            var changed = new ScheduleState { Enabled = true, Time = live.Time, Executable = live.Executable, Arguments = "--run", Rule = daily };
            Check(!ShutdownCountdown.CanFinish(changed, live, new DateTime(2026, 10, 20, 0, 0, 0)), "changing recurrence during countdown prevents stale execution");
            var oldDaily = new ScheduleState { Enabled = true, Time = live.Time, Executable = live.Executable, Arguments = "--run", Rule = new RepeatRule { OnceDate = new DateTime(2026, 10, 19) } };
            var newDaily = new ScheduleState { Enabled = true, Time = live.Time, Executable = live.Executable, Arguments = "--run", Rule = new RepeatRule { OnceDate = new DateTime(2026, 10, 20) } };
            Check(ShutdownCountdown.CanFinish(newDaily, oldDaily, new DateTime(2026, 10, 20, 0, 0, 0)), "legacy daily countdown survives midnight metadata defaults");
            TestRepeatXml();
        }

        private static void TestRepeatXml()
        {
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            foreach (RepeatKind kind in Enum.GetValues(typeof(RepeatKind)))
            {
                var rule = new RepeatRule { Kind = kind, DaysMask = 42, OnceDate = DateTime.Today.AddDays(1) };
                var root = XDocument.Parse(ScheduleStore.BuildXml(DateTime.Now.AddHours(1), Application.ExecutablePath, "--probe test", rule)).Root;
                var trigger = root.Element(ns + "Triggers").Elements().Single();
                Check((kind == RepeatKind.Once ? trigger.Name.LocalName == "TimeTrigger" : trigger.Name.LocalName == "CalendarTrigger")
                    && RepeatRule.FromData((string)root.Element(ns + "Data")).Kind == kind, "task trigger and persisted rule correct for " + kind);
                if (kind == RepeatKind.Custom)
                {
                    var days = trigger.Element(ns + "ScheduleByWeek").Element(ns + "DaysOfWeek").Elements().Select(x => x.Name.LocalName).ToArray();
                    Check(String.Join(",", days) == "Monday,Wednesday,Friday", "weekly task includes only selected native weekdays");
                }
            }
        }

        private static void TestPlanningWindows(string output)
        {
            using (var dialog = new RepeatDialog(new RepeatRule { Kind = RepeatKind.Custom, DaysMask = 42 }, DateTime.Today.AddHours(23)))
            {
                Check(dialog.Choices.Length == 6, "planning dialog has six choices without shift scheduling");
                Check(dialog.ReadSelection().DaysMask == 42, "weekday chips preserve custom selections");
                foreach (var day in dialog.Days) day.Checked = false;
                bool rejected = false; try { dialog.ReadSelection(); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "planning dialog rejects no weekdays selected");
                dialog.Days[0].Checked = true; dialog.Days[2].Checked = true; dialog.Days[4].Checked = true;
                Capture(dialog, Path.Combine(output, "自定义星期.png"));
            }
            using (var dialog = new RepeatDialog(new RepeatRule { Kind = RepeatKind.Holiday }, DateTime.Today))
            {
                Check(dialog.Detail.Text.Contains("不含普通周末"), "holiday description matches requested statutory dates only");
                Capture(dialog, Path.Combine(output, "重复规划.png"));
            }
        }
    }
}
