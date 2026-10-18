using System;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed partial class MainForm
    {
        private RepeatRule selectedRule = new RepeatRule();
        private SoftButton repeat;

        private void ChooseRepeat()
        {
            using (var dialog = new RepeatDialog(selectedRule, time.Value))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                selectedRule = dialog.SelectedRule.Copy();
                repeat.Text = selectedRule.Summary + "  ›";
            }
        }

        private void RemoveConfiguration()
        {
            var answer = MessageBox.Show(this,
                "将移除本软件的关机计划、开机自动启动和导入的节假日日历，取消正在进行的倒计时，然后退出软件。\n\n程序文件会保留。确定移除吗？",
                "一键移除配置", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            ConfigurationRemoval.Remove(store, startupStore, HolidayCalendar.Current);
            ExitApplication();
        }

        private static string NextDescription(ScheduleState state)
        {
            if (state == null) return "启用后，这里会显示下次执行时间";
            if (!state.Enabled) return "计划已暂停，恢复后按所选重复方式执行";
            if (state.NextRun != DateTime.MinValue) return "下次倒计时   " + state.NextRun.ToString("MM月dd日  HH:mm");
            return state.Rule.UsesCalendar ? "没有已知的下一日期，请导入新年度日历" : "单次计划已结束，可重新选择日期";
        }
    }
}
