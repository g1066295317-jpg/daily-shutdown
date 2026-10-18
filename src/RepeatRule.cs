using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DailyShutdown
{
    internal enum RepeatKind { Once, Daily, Workday, Holiday, Weekdays, Custom }

    internal sealed class RepeatRule
    {
        public RepeatKind Kind = RepeatKind.Daily;
        public int DaysMask = 62;
        public DateTime OnceDate = DateTime.Today;
        public bool UsesCalendar { get { return Kind == RepeatKind.Workday || Kind == RepeatKind.Holiday; } }
        public int EffectiveMask { get { return Kind == RepeatKind.Weekdays ? 62 : DaysMask; } }
        public string Summary
        {
            get
            {
                switch (Kind)
                {
                    case RepeatKind.Once: return "只执行一次 · " + OnceDate.ToString("yyyy-MM-dd");
                    case RepeatKind.Workday: return "法定工作日";
                    case RepeatKind.Holiday: return "法定节假日";
                    case RepeatKind.Weekdays: return "周一至周五";
                    case RepeatKind.Custom:
                        string[] names = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
                        return String.Join("、", Enumerable.Range(1, 7).Select(x => x % 7)
                            .Where(day => (DaysMask & (1 << day)) != 0).Select(day => names[day]));
                    default: return "每天";
                }
            }
        }

        public RepeatRule Copy() { return new RepeatRule { Kind = Kind, DaysMask = DaysMask, OnceDate = OnceDate }; }
        public bool Equivalent(RepeatRule other)
        {
            return other != null && Kind == other.Kind && (Kind != RepeatKind.Custom || DaysMask == other.DaysMask)
                && (Kind != RepeatKind.Once || OnceDate.Date == other.OnceDate.Date);
        }
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(RepeatKind), Kind)) throw new ArgumentException("不支持的重复方式。");
            if (Kind == RepeatKind.Custom && (DaysMask <= 0 || DaysMask > 127)) throw new ArgumentException("请至少选择一个星期。");
        }
        public bool Matches(DateTime date, HolidayCalendar calendar = null)
        {
            switch (Kind)
            {
                case RepeatKind.Once: return date.Date == OnceDate.Date;
                case RepeatKind.Daily: return true;
                case RepeatKind.Weekdays:
                case RepeatKind.Custom: return (EffectiveMask & (1 << (int)date.DayOfWeek)) != 0;
                case RepeatKind.Workday: return (calendar ?? HolidayCalendar.Current).IsWorkday(date) == true;
                case RepeatKind.Holiday: return (calendar ?? HolidayCalendar.Current).IsHoliday(date) == true;
                default: return false;
            }
        }
        public DateTime Next(DateTime now, DateTime time, HolidayCalendar calendar = null)
        {
            Validate();
            if (Kind == RepeatKind.Once)
            {
                DateTime once = OnceDate.Date.AddHours(time.Hour).AddMinutes(time.Minute);
                return once > now ? once : DateTime.MinValue;
            }
            DateTime day = now.Date;
            int lastYear = UsesCalendar ? (calendar ?? HolidayCalendar.Current).LastYear : now.Year + 1;
            while (day.Year <= lastYear)
            {
                DateTime candidate = day.AddHours(time.Hour).AddMinutes(time.Minute);
                if (candidate > now && Matches(day, calendar)) return candidate;
                day = day.AddDays(1);
            }
            return DateTime.MinValue;
        }

        public string ToData()
        {
            Validate();
            return new XElement("ShutdownPlan", new XAttribute("version", 1),
                new XAttribute("repeat", Kind), new XAttribute("days", DaysMask),
                new XAttribute("date", OnceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).ToString();
        }
        public static RepeatRule FromData(string data)
        {
            if (String.IsNullOrWhiteSpace(data)) return new RepeatRule(); // Original daily tasks.
            XElement root = XElement.Parse(data);
            if (root.Name != "ShutdownPlan" || (string)root.Attribute("version") != "1")
                throw new InvalidOperationException("计划格式无法识别，请重新保存。");
            var rule = new RepeatRule {
                Kind = (RepeatKind)Enum.Parse(typeof(RepeatKind), (string)root.Attribute("repeat")),
                DaysMask = (int)root.Attribute("days"),
                OnceDate = DateTime.ParseExact((string)root.Attribute("date"), "yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            rule.Validate(); return rule;
        }
    }
}
