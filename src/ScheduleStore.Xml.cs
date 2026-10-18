using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace DailyShutdown
{
    internal sealed partial class ScheduleStore
    {
        public static string BuildXml(DateTime time, string executable, string arguments, RepeatRule rule = null)
        {
            rule = rule ?? new RepeatRule(); rule.Validate();
            DateTime start = rule.Next(DateTime.Now, time);
            if (start == DateTime.MinValue) start = NextTime(DateTime.Now, time);
            var trigger = new XElement(Ns + (rule.Kind == RepeatKind.Once ? "TimeTrigger" : "CalendarTrigger"),
                new XElement(Ns + "StartBoundary", start.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)),
                new XElement(Ns + "Enabled", true));
            if (rule.Kind == RepeatKind.Custom || rule.Kind == RepeatKind.Weekdays)
            {
                var days = new XElement(Ns + "DaysOfWeek");
                for (int day = 0; day < 7; day++)
                    if ((rule.EffectiveMask & (1 << day)) != 0) days.Add(new XElement(Ns + ((DayOfWeek)day).ToString()));
                trigger.Add(new XElement(Ns + "ScheduleByWeek", new XElement(Ns + "WeeksInterval", 1), days));
            }
            else if (rule.Kind != RepeatKind.Once)
                trigger.Add(new XElement(Ns + "ScheduleByDay", new XElement(Ns + "DaysInterval", 1)));
            return new XDocument(new XElement(Ns + "Task", new XAttribute("version", "1.2"),
                new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description",
                    "定时关机 · " + rule.Summary + "；倒计时 60 秒，可取消本次。")),
                new XElement(Ns + "Triggers", trigger),
                new XElement(Ns + "Principals", new XElement(Ns + "Principal", new XAttribute("id", "Author"),
                    new XElement(Ns + "UserId", UserId), new XElement(Ns + "LogonType", "InteractiveToken"),
                    new XElement(Ns + "RunLevel", "LeastPrivilege"))),
                new XElement(Ns + "Settings",
                    new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(Ns + "DisallowStartIfOnBatteries", false),
                    new XElement(Ns + "StopIfGoingOnBatteries", false),
                    new XElement(Ns + "AllowHardTerminate", true),
                    new XElement(Ns + "StartWhenAvailable", false),
                    new XElement(Ns + "RunOnlyIfNetworkAvailable", false),
                    new XElement(Ns + "AllowStartOnDemand", true),
                    new XElement(Ns + "Enabled", true),
                    new XElement(Ns + "Hidden", false),
                    new XElement(Ns + "WakeToRun", false),
                    new XElement(Ns + "ExecutionTimeLimit", "PT5M"),
                    new XElement(Ns + "Priority", 7)),
                new XElement(Ns + "Data", rule.ToData()),
                new XElement(Ns + "Actions", new XAttribute("Context", "Author"),
                    new XElement(Ns + "Exec", new XElement(Ns + "Command", executable),
                        new XElement(Ns + "Arguments", arguments),
                        new XElement(Ns + "WorkingDirectory", Path.GetDirectoryName(executable)))))).ToString();
        }
    }
}
