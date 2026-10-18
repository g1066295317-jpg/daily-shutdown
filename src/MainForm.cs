using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed partial class MainForm : Form
    {
        private readonly ScheduleStore store;
        private readonly StartupRegistration startupStore;
        private readonly EventWaitHandle activation;
        private readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
        internal readonly TrayController Tray;
        private readonly TimeInput time = new TimeInput();
        private readonly Label status = new Label();
        private readonly Label next = new Label();
        private readonly Label startupNote = new Label();
        private readonly ToggleSwitch startup = new ToggleSwitch();
        private SoftButton pause, remove;
        private bool startHidden, exiting, syncingStartup, notified;
        private DateTime nextPoll;

        public MainForm(bool hidden = false, EventWaitHandle signal = null,
            ScheduleStore schedules = null, StartupRegistration startupSettings = null)
        {
            startHidden = hidden; activation = signal;
            store = schedules ?? new ScheduleStore(); startupStore = startupSettings ?? new StartupRegistration();
            BuildLayout();
            Tray = new TrayController(ShowMain, () => Perform(TogglePause), () => Perform(CancelCountdown),
                () => Perform(() => SetStartup(!startup.Checked)), ExitApplication,
                () => Perform(() => { if (store.Read() != null) store.Pause(); ShutdownCountdown.CancelRunning(); ExitApplication(); }),
                () => RefreshSafely(false));
            Icon = Tray.AppIcon;
            startup.CheckedChanged += (s, e) => { if (!syncingStartup) Perform(() => SetStartup(startup.Checked)); };
            FormClosing += (s, e) => {
                if (e.CloseReason != CloseReason.UserClosing || exiting) return;
                e.Cancel = true; HideToTray();
            };
            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
            poll.Interval = 400;
            poll.Tick += (s, e) => {
                if (activation != null && activation.WaitOne(0)) ShowMain();
                if (DateTime.UtcNow >= nextPoll) RefreshSafely(false);
            };
            RefreshSafely(true);
            poll.Start();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden) { value = false; if (!IsHandleCreated) CreateHandle(); }
            base.SetVisibleCore(value);
        }

        internal void ShowMain()
        {
            startHidden = false; ShowInTaskbar = true; WindowState = FormWindowState.Normal;
            Show(); BringToFront(); Activate(); RefreshSafely(false);
        }

        internal void HideToTray()
        {
            Hide(); ShowInTaskbar = false;
            if (!notified) { notified = true; Tray.NotifyHidden(); }
        }

        internal void ExitApplication() { exiting = true; Close(); }

        private void TogglePause()
        {
            ScheduleState state = store.Read();
            if (state == null) return;
            if (state.Enabled) { store.Pause(); ShutdownCountdown.CancelRunning(); }
            else store.Save(state.Time, Application.ExecutablePath, "--run", state.Rule);
            RefreshSafely(true);
        }

        private void CancelCountdown()
        {
            MessageBox.Show(this, ShutdownCountdown.CancelRunning() ? "已取消本次倒计时，之后按所选重复方式执行。"
                : "当前没有正在进行的关机倒计时。", "取消本次");
            RefreshSafely(false);
        }

        private void SetStartup(bool enabled)
        {
            try { startupStore.SetEnabled(enabled, Application.ExecutablePath); }
            finally { SyncStartup(); }
            RefreshSafely(false);
        }

        private bool SyncStartup()
        {
            string command = startupStore.ReadCommand();
            syncingStartup = true;
            try { startup.Checked = !String.IsNullOrEmpty(command); }
            finally { syncingStartup = false; }
            startupNote.Text = startup.Checked && !String.Equals(command, StartupRegistration.CommandFor(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)
                ? "程序位置有变化，请重新开关此项更新路径" : "登录 Windows 后自动进入托盘";
            return startup.Checked;
        }

        private void RefreshSafely(bool loadTime)
        {
            nextPoll = DateTime.UtcNow.AddSeconds(5);
            try
            {
                ScheduleState state = store.Read(); bool autoStart = SyncStartup();
                bool running = ShutdownCountdown.IsRunning();
                pause.Enabled = state != null; remove.Enabled = state != null;
                pause.Text = state != null && state.Enabled ? "暂停计划" : "恢复计划";
                bool enabled = state != null && state.Enabled;
                status.Text = running ? "正在倒计时" : state == null ? "未启用" : !enabled ? "已暂停"
                    : state.NextRun == DateTime.MinValue ? (state.Rule.UsesCalendar ? "待更新日历" : "已结束") : "已启用";
                status.BackColor = enabled ? Theme.Mint : Color.FromArgb(240, 244, 241);
                status.ForeColor = enabled ? Theme.Green : Theme.Muted;
                next.Text = NextDescription(state);
                if (state != null && loadTime) { time.Value = state.Time; selectedRule = state.Rule.Copy(); repeat.Text = selectedRule.Summary + "  ›"; }
                if (state != null && !String.Equals(state.Executable, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    next.Text = "程序位置已变化，点击“保存并启用”更新计划";
                Tray.Update(state, running, autoStart);
            }
            catch (Exception ex)
            {
                status.Text = "读取失败"; status.ForeColor = Color.FromArgb(175, 112, 42);
                next.Text = "状态读取失败：" + ex.Message;
                Tray.Update(null, false, startup.Checked, ex.Message);
            }
        }

        private void Perform(Action action)
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Error); RefreshSafely(false); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { HideToTray(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { poll.Dispose(); if (Tray != null) Tray.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
