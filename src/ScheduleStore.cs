using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;

namespace DailyShutdown
{
    internal sealed class ScheduleState
    {
        public bool Enabled;
        public DateTime Time;
        public DateTime NextRun;
        public string Executable;
        public string Arguments;
        public RepeatRule Rule = new RepeatRule();
    }

    internal sealed partial class ScheduleStore
    {
        public static readonly string UserId = WindowsIdentity.GetCurrent().User.Value;
        public readonly string Name;
        private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        public ScheduleStore(string name = null)
        {
            Name = name ?? ("DailyShutdownHelper-" + UserId);
        }

        public static DateTime NextTime(DateTime now, DateTime time)
        {
            DateTime next = now.Date.AddHours(time.Hour).AddMinutes(time.Minute);
            return next > now ? next : next.AddDays(1);
        }

        private T WithFolder<T>(Func<dynamic, T> action)
        {
            dynamic service = null;
            dynamic folder = null;
            try
            {
                service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
                service.Connect();
                folder = service.GetFolder("\\");
                return action(folder);
            }
            finally
            {
                if (folder != null) Marshal.FinalReleaseComObject(folder);
                if (service != null) Marshal.FinalReleaseComObject(service);
            }
        }

        public ScheduleState Read()
        {
            return WithFolder<ScheduleState>(folder =>
            {
                dynamic task = null;
                try
                {
                    task = folder.GetTask(Name);
                    XDocument doc = XDocument.Parse((string)task.Xml);
                    XElement trigger = doc.Root.Element(Ns + "Triggers").Elements().First();
                    XElement exec = doc.Root.Element(Ns + "Actions").Element(Ns + "Exec");
                    RepeatRule rule = RepeatRule.FromData((string)doc.Root.Element(Ns + "Data"));
                    DateTime time = DateTime.Parse(trigger.Element(Ns + "StartBoundary").Value, CultureInfo.InvariantCulture);
                    return new ScheduleState {
                        Enabled = task.Enabled,
                        Time = time,
                        Rule = rule,
                        NextRun = rule.Next(DateTime.Now, time),
                        Executable = exec.Element(Ns + "Command").Value,
                        Arguments = exec.Element(Ns + "Arguments").Value
                    };
                }
                catch (FileNotFoundException) { return null; }
                catch (COMException ex)
                {
                    if ((uint)ex.ErrorCode == 0x80070002) return null;
                    throw;
                }
                finally { if (task != null) Marshal.FinalReleaseComObject(task); }
            });
        }

        public void Save(DateTime time, string executable, string arguments = "--run", RepeatRule rule = null)
        {
            rule = rule ?? new RepeatRule();
            rule.Validate();
            if (rule.Kind == RepeatKind.Once && rule.Next(DateTime.Now, time) == DateTime.MinValue)
                throw new ArgumentException("单次计划的日期和时间已过，请重新选择。");
            string xml = BuildXml(time, executable, arguments, rule);
            WithFolder<object>(folder =>
            {
                object task = folder.RegisterTask(Name, xml, 6, UserId, null, 3, null);
                Marshal.FinalReleaseComObject(task);
                return null;
            });
            ScheduleState saved = Read();
            if (saved == null || !saved.Enabled || saved.Time.Hour != time.Hour || saved.Time.Minute != time.Minute
                || !String.Equals(saved.Executable, executable, StringComparison.OrdinalIgnoreCase)
                || saved.Arguments != arguments || saved.Rule.ToData() != rule.ToData())
                throw new InvalidOperationException("保存后核对计划失败，请重新打开软件检查状态。");
        }

        public void Pause()
        {
            WithFolder<object>(folder =>
            {
                dynamic task = folder.GetTask(Name);
                try { task.Enabled = false; }
                finally { Marshal.FinalReleaseComObject(task); }
                return null;
            });
            if (Read().Enabled) throw new InvalidOperationException("暂停计划失败。");
        }

        public void Delete()
        {
            if (Read() == null) return;
            WithFolder<object>(folder => { folder.DeleteTask(Name, 0); return null; });
            if (Read() != null) throw new InvalidOperationException("删除计划失败。");
        }

        public void RunForTest()
        {
            if (!Name.StartsWith("DailyShutdownHelper-Test-", StringComparison.Ordinal))
                throw new InvalidOperationException("只允许手动运行测试计划。");
            WithFolder<object>(folder =>
            {
                dynamic task = folder.GetTask(Name);
                try { object running = task.Run(null); Marshal.FinalReleaseComObject(running); }
                finally { Marshal.FinalReleaseComObject(task); }
                return null;
            });
        }
    }
}
