using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed class CountdownClock
    {
        public readonly DateTime Deadline;
        public bool Cancelled { get; private set; }
        public CountdownClock(DateTime now) { Deadline = now.AddSeconds(60); }
        public void Cancel() { Cancelled = true; }
        public int Remaining(DateTime now) { return Math.Max(0, (int)Math.Ceiling((Deadline - now).TotalSeconds)); }
        public bool Due(DateTime now) { return !Cancelled && now >= Deadline; }
    }

    internal static class ShutdownAction
    {
        public const string Arguments = "/s /t 0";
        public static void Execute()
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), Arguments) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
            };
            using (Process process = Process.Start(info))
            {
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(5000)) throw new InvalidOperationException("关机命令未及时响应。");
                if (process.ExitCode != 0) throw new InvalidOperationException("Windows 未接受关机请求：" + error);
            }
        }
    }

    internal sealed class ShutdownCountdown : Form
    {
        private readonly bool simulation;
        private readonly ScheduleState scheduled;
        private readonly CountdownClock clock = new CountdownClock(DateTime.UtcNow);
        private readonly EventWaitHandle cancellation;
        private readonly Label remaining = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private DateTime lastTick = DateTime.UtcNow;

        private static string EventName { get { return "Local\\DailyShutdownCancel-" + ScheduleStore.UserId; } }

        public static bool CancelRunning(string name = null)
        {
            try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting(name ?? EventName)) { return signal.Set(); } }
            catch (WaitHandleCannotBeOpenedException) { return false; }
        }

        public static bool IsRunning()
        {
            try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting(EventName)) { return !signal.WaitOne(0); } }
            catch (WaitHandleCannotBeOpenedException) { return false; }
        }

        public static bool IsCurrent(ScheduleState state, DateTime now)
        {
            if (state == null || !state.Enabled || state.Arguments != "--run") return false;
            if (!String.Equals(state.Executable, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return false;
            DateTime occurrence = now.Date.AddHours(state.Time.Hour).AddMinutes(state.Time.Minute);
            if (occurrence > now) occurrence = occurrence.AddDays(-1);
            double lateness = (now - occurrence).TotalSeconds;
            return lateness >= 0 && lateness < 120 && state.Rule.Matches(occurrence.Date);
        }

        internal static bool CanFinish(ScheduleState current, ScheduleState original, DateTime now)
        {
            return original != null && IsCurrent(current, now) && current.Time.Hour == original.Time.Hour
                && current.Time.Minute == original.Time.Minute && current.Rule.Equivalent(original.Rule);
        }

        public ShutdownCountdown(bool demo, ScheduleState state, string cancellationName = null)
        {
            simulation = demo;
            scheduled = state;
            if (!simulation) cancellation = new EventWaitHandle(false, EventResetMode.ManualReset, cancellationName ?? EventName);
            Text = simulation ? "倒计时演示 · 不会关机" : "每天定时关机 · 倒计时";
            ClientSize = new Size(420, 390);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background;
            Font = new Font("Microsoft YaHei UI", 10);
            TopMost = true;
            Icon = TrayController.LoadIcon("countdown.ico");
            using (var path = Theme.Round(new RectangleF(0, 0, Width, Height), 22)) Region = new Region(path);
            MainForm.AddText(this, simulation ? "演示模式 · 不会关机" : "每日关机提醒", 30, 30, 360, 25, 9, false, Theme.Green).TextAlign = ContentAlignment.MiddleCenter;
            MainForm.AddText(this, simulation ? "提前感受一下提醒" : "即将关闭电脑", 30, 78, 360, 35, 19, true, Theme.Ink).TextAlign = ContentAlignment.MiddleCenter;
            MainForm.AddText(this, simulation ? "你可以随时结束这次演示" : "请先保存正在编辑的文件", 30, 125, 360, 26, 9, false, Theme.Muted).TextAlign = ContentAlignment.MiddleCenter;
            remaining.Bounds = new Rectangle(30, 164, 360, 88);
            remaining.Font = new Font("Segoe UI", 43, FontStyle.Regular);
            remaining.ForeColor = Theme.Green;
            remaining.TextAlign = ContentAlignment.MiddleCenter;
            MainForm.AddText(this, state != null && state.Rule.Kind == RepeatKind.Once ? "取消后，本次单次计划不再执行" : "取消仅跳过本次，之后按所选规划执行", 30, 269, 360, 25, 9, false, Theme.Muted).TextAlign = ContentAlignment.MiddleCenter;
            var cancel = MainForm.CreateButton(simulation ? "结束演示" : "取消本次关机", 40, 316, 340, true);
            cancel.Click += (s, e) => Close();
            var close = new SoftButton { Text = "×", TextOnly = true, ForeColor = Theme.Muted, Bounds = new Rectangle(374, 14, 30, 30), AccessibleName = "取消本次并关闭" };
            close.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { remaining, cancel, close });
            timer.Interval = 200;
            timer.Tick += TickCountdown;
            FormClosing += (s, e) => { clock.Cancel(); timer.Stop(); };
            FormClosed += (s, e) => { timer.Dispose(); if (cancellation != null) cancellation.Dispose(); };
            UpdateRemaining();
            timer.Start();
        }

        private void UpdateRemaining() { remaining.Text = clock.Remaining(DateTime.UtcNow) + " 秒"; }

        private void TickCountdown(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            // Skip a suspended or stalled countdown instead of shutting down on resume.
            if (!simulation && (now - lastTick).TotalSeconds > 5) { Close(); return; }
            lastTick = now;
            UpdateRemaining();
            if (cancellation != null && cancellation.WaitOne(0)) { Close(); return; }
            if (!clock.Due(DateTime.UtcNow)) return;
            timer.Stop();
            if (simulation) { MessageBox.Show(this, "演示结束，没有执行关机。", "演示"); Close(); return; }
            try
            {
                ScheduleState current = new ScheduleStore().Read();
                if (CanFinish(current, scheduled, DateTime.Now)) ShutdownAction.Execute();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "关机未执行", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            Close();
        }
    }
}
