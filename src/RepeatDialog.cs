using System;
using System.Drawing;
using System.Windows.Forms;

namespace DailyShutdown
{
    internal sealed class RepeatDialog : Form
    {
        internal readonly RadioButton[] Choices = new RadioButton[6];
        internal readonly CheckBox[] Days = new CheckBox[7];
        internal readonly DateTimePicker OnceDate = new DateTimePicker();
        internal readonly Label Detail = new Label();
        private readonly Label coverage = new Label();
        private readonly Panel extra = new Panel();
        private readonly Label dayHeading = new Label();
        public RepeatRule SelectedRule { get; private set; }

        public RepeatDialog(RepeatRule initial, DateTime time)
        {
            SelectedRule = initial.Copy();
            Text = "重复与规划"; ClientSize = new Size(470, 562);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Background;
            Font = new Font("Microsoft YaHei UI", 9); ForeColor = Theme.Ink;
            MainForm.AddText(this, "重复", 24, 20, 422, 34, 19, true, Theme.Ink);
            MainForm.AddText(this, "选择这项关机计划在哪些日期执行", 24, 61, 422, 25, 9, false, Theme.Muted);
            string[] names = { "只执行一次", "每天", "法定工作日", "法定节假日", "周一至周五", "自定义星期" };
            for (int i = 0; i < Choices.Length; i++)
            {
                var choice = new RadioButton { Text = names[i], Bounds = new Rectangle(28, 99 + i * 39, 414, 31),
                    Font = new Font("Microsoft YaHei UI", 11), BackColor = Theme.Background, Tag = i };
                choice.CheckedChanged += (s, e) => UpdateDetails();
                Choices[i] = choice; Controls.Add(choice);
            }
            extra.Bounds = new Rectangle(24, 344, 422, 93); Controls.Add(extra);
            Detail.Bounds = new Rectangle(0, 0, 422, 90); Detail.ForeColor = Theme.Muted; extra.Controls.Add(Detail);
            OnceDate.Bounds = new Rectangle(0, 31, 310, 32); OnceDate.Font = new Font("Microsoft YaHei UI", 11);
            OnceDate.Format = DateTimePickerFormat.Custom; OnceDate.CustomFormat = "yyyy 年 MM 月 dd 日";
            OnceDate.MinDate = DateTime.Today;
            DateTime date = initial.Kind == RepeatKind.Once ? initial.OnceDate : ScheduleStore.NextTime(DateTime.Now, time).Date;
            OnceDate.Value = date < DateTime.Today ? DateTime.Today : date;
            extra.Controls.Add(OnceDate);
            dayHeading.Text = "选择星期，可多选"; dayHeading.ForeColor = Theme.Muted; dayHeading.Bounds = new Rectangle(0, 0, 422, 25); extra.Controls.Add(dayHeading);
            string[] days = { "一", "二", "三", "四", "五", "六", "日" };
            for (int i = 0; i < 7; i++)
            {
                var day = new WeekdayChip { Text = days[i], Bounds = new Rectangle(i * 60, 34, 56, 38),
                    Checked = (initial.DaysMask & (1 << ((i + 1) % 7))) != 0, AccessibleName = "周" + days[i] };
                Days[i] = day; extra.Controls.Add(day);
            }
            coverage.Bounds = new Rectangle(24, 450, 265, 28); coverage.ForeColor = Theme.Muted; Controls.Add(coverage);
            var import = new SoftButton { Text = "导入日历…", TextOnly = true, ForeColor = Theme.Green, Bounds = new Rectangle(319, 443, 127, 36) };
            import.Click += (s, e) => ImportCalendar(); Controls.Add(import);
            var cancel = MainForm.CreateButton("取消", 24, 501, 203, false); cancel.DialogResult = DialogResult.Cancel;
            var save = MainForm.CreateButton("确定", 243, 501, 203, true);
            save.Click += (s, e) => {
                try { SelectedRule = ReadSelection(); DialogResult = DialogResult.OK; Close(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "请完善规划"); }
            };
            Controls.AddRange(new Control[] { cancel, save }); CancelButton = cancel; AcceptButton = save;
            Choices[(int)initial.Kind].Checked = true; UpdateDetails();
        }

        internal RepeatRule ReadSelection()
        {
            RepeatKind kind = RepeatKind.Daily;
            for (int i = 0; i < Choices.Length; i++) if (Choices[i].Checked) kind = (RepeatKind)i;
            int mask = 0;
            for (int i = 0; i < Days.Length; i++) if (Days[i].Checked) mask |= 1 << ((i + 1) % 7);
            var rule = new RepeatRule { Kind = kind, DaysMask = mask, OnceDate = OnceDate.Value.Date };
            rule.Validate(); return rule;
        }

        private void UpdateDetails()
        {
            if (Days[6] == null) return;
            int selected = 1;
            for (int i = 0; i < Choices.Length; i++) if (Choices[i].Checked) selected = i;
            bool once = selected == 0, custom = selected == 5;
            OnceDate.Visible = once; dayHeading.Visible = custom;
            foreach (CheckBox day in Days) day.Visible = custom;
            Detail.Visible = !custom;
            Detail.Text = once ? "执行日期" : selected == 2 ? "按中国大陆安排，包含调休补班日。\n跳过周末和国务院通知中的放假日期。"
                : selected == 3 ? "仅国务院通知中的放假日期，不含普通周末。\n未覆盖的年份会跳过，需先导入新日历。"
                : selected == 4 ? "固定周一至周五执行，不跟随节假日调休。" : "每天执行，包括周末与节假日。";
            Detail.Height = once ? 25 : 90;
            coverage.Text = "日历已覆盖：" + HolidayCalendar.Current.Coverage + " 年";
        }

        private void ImportCalendar()
        {
            using (var picker = new OpenFileDialog { Title = "导入年度节假日日历", Filter = "节假日日历 XML|*.xml", CheckFileExists = true })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try {
                    int year = HolidayCalendar.Current.Import(picker.FileName);
                    UpdateDetails(); MessageBox.Show(this, "已导入 " + year + " 年日历，现有计划也会使用此日历。请核对文件中的官方来源与日期。", "日历已更新");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "导入失败"); }
            }
        }
    }
}
