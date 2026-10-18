using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed class TrayController : IDisposable
    {
        private readonly NotifyIcon icon;
        private readonly Icon enabledIcon = LoadIcon("app.ico");
        private readonly Icon pausedIcon = LoadIcon("paused.ico");
        private readonly Icon activeIcon = LoadIcon("countdown.ico");
        public readonly ContextMenuStrip Menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem status = new ToolStripMenuItem { Enabled = false };
        private readonly ToolStripMenuItem next = new ToolStripMenuItem { Enabled = false };
        private readonly ToolStripMenuItem pause;
        private readonly ToolStripMenuItem startup;
        private bool disposed;
        public Icon AppIcon { get { return enabledIcon; } }
        public bool Visible { get { return icon.Visible; } }
        public string Tooltip { get { return icon.Text; } }
        public string Summary { get { return status.Text; } }

        internal static Icon LoadIcon(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) return (Icon)SystemIcons.Application.Clone();
                using (var original = new Icon(stream, new Size(32, 32))) return (Icon)original.Clone();
            }
        }

        public TrayController(Action show, Action togglePause, Action cancel, Action toggleStartup,
            Action exit, Action pauseAndExit, Action refresh)
        {
            Menu.Font = new Font("Microsoft YaHei UI", 9);
            Menu.ShowImageMargin = false;
            Menu.Items.Add(status);
            Menu.Items.Add(next);
            Menu.Items.Add(new ToolStripSeparator());
            Menu.Items.Add("打开主界面", null, (s, e) => show());
            pause = new ToolStripMenuItem("暂停每日计划", null, (s, e) => togglePause());
            Menu.Items.Add(pause);
            Menu.Items.Add("取消本次倒计时", null, (s, e) => cancel());
            startup = new ToolStripMenuItem("登录后自动启动", null, (s, e) => toggleStartup());
            Menu.Items.Add(startup);
            Menu.Items.Add(new ToolStripSeparator());
            Menu.Items.Add("暂停计划并退出", null, (s, e) => pauseAndExit());
            Menu.Items.Add("退出软件（保留关机计划）", null, (s, e) => exit());
            Menu.Opening += (s, e) => refresh();
            icon = new NotifyIcon { Icon = pausedIcon, Text = "每天定时关机 · 正在读取状态", ContextMenuStrip = Menu, Visible = true };
            icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) show(); };
        }

        public void Update(ScheduleState state, bool running, bool autoStart, string error = null)
        {
            bool enabled = state != null && state.Enabled;
            bool ended = enabled && state.NextRun == DateTime.MinValue && !state.Rule.UsesCalendar;
            status.Text = error != null ? "计划状态读取失败" : running ? "正在倒计时，可取消本次" : state == null
                ? "尚未启用关机计划" : ended ? "单次计划已结束" : enabled ? state.Rule.Summary + " " + state.Time.ToString("HH:mm") + " · 已启用" : "关机计划已暂停";
            next.Text = error != null ? "打开主界面查看错误" : enabled && state.NextRun != DateTime.MinValue ? "下次 " + state.NextRun.ToString("MM-dd HH:mm")
                : enabled && state.Rule.UsesCalendar ? "没有已知的下一日期，请导入新年度日历" : ended ? "打开主界面重新选择日期" : "启用后会显示下次执行时间";
            pause.Enabled = state != null && error == null;
            pause.Text = enabled ? "暂停计划" : "恢复计划";
            startup.Checked = autoStart;
            icon.Icon = error != null || running || (enabled && state.Rule.UsesCalendar && state.NextRun == DateTime.MinValue)
                ? activeIcon : enabled && !ended ? enabledIcon : pausedIcon;
            string tooltip = "每天定时关机 · " + status.Text;
            icon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
        }

        public void NotifyHidden()
        {
            icon.ShowBalloonTip(3000, "已收进右下角托盘", "点击图标可打开；右键可查看状态、暂停计划。", ToolTipIcon.Info);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            icon.Visible = false;
            icon.Dispose();
            Menu.Dispose();
            enabledIcon.Dispose(); pausedIcon.Dispose(); activeIcon.Dispose();
        }
    }
}
