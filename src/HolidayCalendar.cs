using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace DailyShutdown
{
    internal sealed class HolidayCalendar
    {
        private sealed class YearData
        {
            public int Year;
            public readonly HashSet<DateTime> Holidays = new HashSet<DateTime>();
            public readonly HashSet<DateTime> Workdays = new HashSet<DateTime>();
        }
        private static HolidayCalendar current;
        public static HolidayCalendar Current { get { return current ?? (current = new HolidayCalendar()); } }
        private readonly Dictionary<int, YearData> years = new Dictionary<int, YearData>();
        private readonly string folder;
        public int LastYear { get { return years.Count == 0 ? 0 : years.Keys.Max(); } }
        public string Coverage { get { return String.Join("、", years.Keys.OrderBy(x => x)); } }

        public HolidayCalendar(string directory = null)
        {
            folder = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyShutdownHelper", "calendars");
            Reload();
        }
        public void Reload()
        {
            years.Clear();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("calendar-2026.xml"))
                if (stream != null) { YearData year = Parse(stream); years[year.Year] = year; }
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "calendar-*.xml"))
            {
                try { using (Stream stream = File.OpenRead(file)) { YearData year = Parse(stream); years[year.Year] = year; } }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is XmlException || ex is ArgumentException || ex is FormatException || ex is InvalidOperationException)) throw;
                    // Invalid cached years never become weekday guesses.
                }
            }
        }
        public bool? IsHoliday(DateTime date)
        {
            YearData year;
            return years.TryGetValue(date.Year, out year) ? (bool?)year.Holidays.Contains(date.Date) : null;
        }
        public bool? IsWorkday(DateTime date)
        {
            YearData year;
            if (!years.TryGetValue(date.Year, out year)) return null;
            if (year.Holidays.Contains(date.Date)) return false;
            if (year.Workdays.Contains(date.Date)) return true;
            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }
        public int Import(string file)
        {
            YearData year;
            if (new FileInfo(file).Length > 1024 * 1024) throw new ArgumentException("日历文件过大。");
            byte[] bytes = File.ReadAllBytes(file);
            using (var stream = new MemoryStream(bytes)) year = Parse(stream);
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, "calendar-" + year.Year + ".xml");
            string staging = target + ".tmp";
            try
            {
                File.WriteAllBytes(staging, bytes);
                if (File.Exists(target)) File.Replace(staging, target, null);
                else File.Move(staging, target);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
            years[year.Year] = year;
            return year.Year;
        }
        public void ClearImported()
        {
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "calendar-*.xml"))
                {
                    int year;
                    string token = Path.GetFileNameWithoutExtension(file).Substring("calendar-".Length);
                    if (Int32.TryParse(token, out year) && year >= 1900 && year <= 2200) File.Delete(file);
                }
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            Reload();
        }
        private static YearData Parse(Stream stream)
        {
            if (stream.Length > 1024 * 1024) throw new ArgumentException("日历文件过大。");
            XDocument doc;
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                doc = XDocument.Load(reader);
            XElement root = doc.Root;
            if (root == null || root.Name != "Calendar") throw new ArgumentException("日历格式应为 Calendar XML。");
            int year;
            if (!Int32.TryParse((string)root.Attribute("year"), out year) || year < 1900 || year > 2200)
                throw new ArgumentException("日历年份无效。");
            var data = new YearData { Year = year };
            Func<string, DateTime> parseDate = value => {
                DateTime day = DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (day.Year != year) throw new ArgumentException("日历中的日期必须属于声明年份。");
                return day;
            };
            XElement holidays = root.Element("Holidays"), workdays = root.Element("Workdays");
            if (holidays == null || workdays == null) throw new ArgumentException("日历缺少 Holidays 或 Workdays。");
            foreach (XElement range in holidays.Elements("Range"))
            {
                DateTime start = parseDate((string)range.Attribute("start")), end = parseDate((string)range.Attribute("end"));
                if (end < start) throw new ArgumentException("放假结束日期早于开始日期。");
                for (DateTime day = start; day <= end; day = day.AddDays(1)) data.Holidays.Add(day);
            }
            foreach (XElement day in workdays.Elements("Day")) data.Workdays.Add(parseDate((string)day.Attribute("date")));
            if (data.Holidays.Count == 0 || data.Holidays.Overlaps(data.Workdays)) throw new ArgumentException("日历的放假或补班日期无效。");
            return data;
        }
    }
}
