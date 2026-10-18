using System;
using System.IO;
using Microsoft.Win32;

namespace DailyShutdown
{
    internal sealed class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public readonly string ValueName;
        public StartupRegistration(string name = "DailyShutdownHelper") { ValueName = name; }

        public string ReadCommand()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key == null ? null : key.GetValue(ValueName) as string;
        }

        public static string CommandFor(string executable)
        {
            if (!Path.IsPathRooted(executable) || executable.Contains("\""))
                throw new ArgumentException("自启动需要有效的程序绝对路径。");
            string command = "\"" + executable + "\" --tray";
            if (command.Length > 260) throw new ArgumentException("程序路径过长，请先移到较短的路径。");
            return command;
        }

        public void SetEnabled(bool enabled, string executable)
        {
            string command = enabled ? CommandFor(executable) : null;
            if (enabled && !File.Exists(executable)) throw new FileNotFoundException("程序文件不存在。", executable);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, command, RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
            if (!String.Equals(ReadCommand(), command, StringComparison.Ordinal))
                throw new InvalidOperationException("自动启动设置未能保存，请检查 Windows 的启动项设置。");
        }
        public void Clear()
        {
            SetEnabled(false, String.Empty);
            foreach (string area in new[] { "Run", "Run32" })
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + area, true))
                if (key != null) key.DeleteValue(ValueName, false);
        }
    }
}
