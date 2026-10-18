using System;
using System.Collections.Generic;

namespace DailyShutdown
{
    internal static class ConfigurationRemoval
    {
        public static void Remove(ScheduleStore schedule, StartupRegistration startup, HolidayCalendar calendars,
            Action cancel = null)
        {
            var failures = new List<string>();
            Action<string, Action> attempt = (name, action) => {
                try { action(); } catch (Exception ex) { failures.Add(name + "：" + ex.Message); }
            };
            attempt("取消倒计时", cancel ?? (() => { ShutdownCountdown.CancelRunning(); }));
            attempt("删除关机计划", schedule.Delete);
            attempt("移除自动启动", startup.Clear);
            attempt("清理导入日历", calendars.ClearImported);
            if (failures.Count != 0) throw new InvalidOperationException("部分配置未能移除，请重试：\n" + String.Join("\n", failures));
        }
    }
}
